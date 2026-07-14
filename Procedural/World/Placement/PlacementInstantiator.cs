using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Optional, for components on placed objects' prefabs (root object only) that keep state while the object is in
/// use - health, harvested fruit, open doors: with object pooling on, an object of an unloaded chunk is reused
/// for another chunk, and these calls are the chance to reset that state.
/// </summary>
public interface IPooledTerrainObject
{
    /// <summary>The object was taken from the pool and placed again (it is active, at its new pose).</summary>
    void OnTakenFromPool();

    /// <summary>The object is going into the pool (its chunk was unloaded).</summary>
    void OnReturnedToPool();
}

/// <summary>
/// Creates the objects that object placement decided (see <see cref="ObjectPlacementEngine"/>) on the main
/// thread, within a time budget per frame and nearest chunk first - so the hundreds of trees and rocks of a
/// newly generated chunk appear over a few frames instead of in one hitch, and the chunks next to the
/// player fill in before distant ones.
/// </summary>
public sealed class PlacementInstantiator
{
    /// <summary>One chunk's objects waiting to be created.</summary>
    public sealed class Batch
    {
        /// <summary>Parent of the created objects (normally the chunk's GameObject).</summary>
        public Transform Parent;
        public PlacementPlan Plan;
        public List<ObjectPlacement> Objects;
        /// <summary>The batch's distance to the viewer: the nearest batch is served first (re-evaluated every frame).</summary>
        public Func<float> Distance;
        /// <summary>Cancelled = the chunk was unloaded: the batch is dropped.</summary>
        public WorkToken Token;
        /// <summary>Placement indices not to create (e.g. objects the game removed). May be null.</summary>
        public Func<int, bool> Skip;
        /// <summary>Created objects by placement index (null where skipped).</summary>
        public GameObject[] Created;
        /// <summary>Called once every object of the batch exists.</summary>
        public Action<Batch> Completed;

        internal int Next;

        public bool IsDone => Objects == null || Next >= Objects.Count;
    }

    private readonly List<Batch> batches = new List<Batch>();
    private readonly Stopwatch watch = new Stopwatch();
    private static readonly List<Renderer> Renderers = new List<Renderer>();
    private static readonly List<IPooledTerrainObject> PoolListeners = new List<IPooledTerrainObject>();

    // Object pool: switched-off objects of unloaded chunks, per prefab, reused before creating new ones.
    private readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
    private Transform poolRoot;
    private int pooledCount;

    /// <summary>Reuse objects of unloaded chunks (see <see cref="Release"/>).</summary>
    public bool PoolingEnabled = true;
    /// <summary>Most objects kept in the pool, all prefabs together.</summary>
    public int MaxPooled = 4000;
    /// <summary>Where the pool's (inactive) holder object goes in the hierarchy.</summary>
    public Transform PoolParent;

    /// <summary>Objects waiting in the pool.</summary>
    public int PooledObjects => pooledCount;

    /// <summary>Batches with objects still to create.</summary>
    public int PendingBatches => batches.Count;

    /// <summary>Objects still to create, over all batches.</summary>
    public int PendingObjects
    {
        get
        {
            int count = 0;
            foreach (Batch batch in batches)
                count += batch.Objects.Count - batch.Next;
            return count;
        }
    }

    /// <summary>
    /// Queues a chunk's placements for creation. <paramref name="completed"/> runs (on the main thread) once all
    /// of them exist - right away, from inside this call, when there is nothing to create.
    /// </summary>
    public Batch Enqueue(Transform parent, PlacementPlan plan, PlacementResult result, Func<float> distance,
        WorkToken token, Func<int, bool> skip, Action<Batch> completed)
    {
        var batch = new Batch
        {
            Parent = parent,
            Plan = plan,
            Objects = result != null ? result.Objects : new List<ObjectPlacement>(),
            Distance = distance,
            Token = token,
            Skip = skip,
            Completed = completed,
        };
        batch.Created = new GameObject[batch.Objects.Count];
        if (batch.IsDone)
        {
            completed?.Invoke(batch);
            return batch;
        }
        batches.Add(batch);
        return batch;
    }

    /// <summary>Drops every waiting batch (their objects are not created and nothing is called).</summary>
    public void Clear()
    {
        batches.Clear();
    }

    /// <summary>
    /// Takes back a placed object whose chunk is going away: kept (switched off) for reuse when pooling is on and
    /// the pool has room, destroyed otherwise.
    /// </summary>
    public void Release(GameObject prefab, GameObject instance)
    {
        if (instance == null)
            return;
        Transform root = PoolingEnabled && prefab != null && pooledCount < MaxPooled ? PoolRoot() : null;
        if (root == null)
        {
            Object.Destroy(instance);
            return;
        }

        Notify(instance, false);
        // Under the inactive holder it is switched off without touching its own active flag.
        instance.transform.SetParent(root, false);
        if (!pool.TryGetValue(prefab, out Stack<GameObject> stack))
            pool[prefab] = stack = new Stack<GameObject>();
        stack.Push(instance);
        pooledCount++;
    }

    /// <summary>Destroys every pooled object.</summary>
    public void ClearPool()
    {
        foreach (Stack<GameObject> stack in pool.Values)
            foreach (GameObject instance in stack)
                if (instance != null)
                    Object.Destroy(instance);
        pool.Clear();
        pooledCount = 0;
        if (poolRoot != null)
            Object.Destroy(poolRoot.gameObject);
        poolRoot = null;
    }

    private Transform PoolRoot()
    {
        if (poolRoot == null)
        {
            var holder = new GameObject("Terrain Object Pool");
            holder.SetActive(false);
            if (PoolParent != null)
                holder.transform.SetParent(PoolParent, false);
            poolRoot = holder.transform;
        }
        return poolRoot;
    }

    /// <summary>A pooled copy of the prefab placed at the pose, or null when the pool has none.</summary>
    private GameObject TakeFromPool(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (!pool.TryGetValue(prefab, out Stack<GameObject> stack))
            return null;
        while (stack.Count > 0)
        {
            GameObject instance = stack.Pop();
            pooledCount--;
            if (instance == null)
                continue;   // destroyed while pooled
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            Notify(instance, true);
            return instance;
        }
        return null;
    }

    private static void Notify(GameObject instance, bool taken)
    {
        instance.GetComponents(PoolListeners);
        foreach (IPooledTerrainObject listener in PoolListeners)
        {
            if (taken)
                listener.OnTakenFromPool();
            else
                listener.OnReturnedToPool();
        }
        PoolListeners.Clear();
    }

    /// <summary>
    /// Creates objects until <paramref name="budgetMs"/> milliseconds or <paramref name="maxObjects"/> objects are
    /// spent (at least one object per call while any is waiting). Returns how many were created.
    /// </summary>
    public int Update(float budgetMs, int maxObjects)
    {
        if (batches.Count == 0)
            return 0;

        watch.Restart();
        int created = 0;
        while (true)
        {
            Batch batch = Nearest();
            if (batch == null)
                break;

            while (!batch.IsDone)
            {
                if (created > 0 && (created >= maxObjects || watch.Elapsed.TotalMilliseconds >= budgetMs))
                    return created;
                int index = batch.Next++;
                if (batch.Skip != null && batch.Skip(index))
                    continue;
                batch.Created[index] = Spawn(batch, batch.Objects[index]);
                created++;
            }

            batches.Remove(batch);
            batch.Completed?.Invoke(batch);
        }
        return created;
    }

    /// <summary>The waiting batch nearest the viewer, dropping batches whose chunk is gone.</summary>
    private Batch Nearest()
    {
        Batch best = null;
        float bestDistance = float.MaxValue;
        for (int i = batches.Count - 1; i >= 0; i--)
        {
            Batch batch = batches[i];
            if ((batch.Token != null && batch.Token.IsCancelled) || batch.Parent == null)
            {
                batches.RemoveAt(i);
                continue;
            }
            float distance = batch.Distance != null ? batch.Distance() : 0f;
            if (best == null || distance < bestDistance)
            {
                best = batch;
                bestDistance = distance;
            }
        }
        return best;
    }

    private GameObject Spawn(Batch batch, ObjectPlacement placement)
    {
        PlacementType type = batch.Plan.Types[placement.Type];
        if (type.Prefab == null)
            return null;

        GameObject instance = TakeFromPool(type.Prefab, placement.Position, placement.Rotation, batch.Parent)
            ?? Object.Instantiate(type.Prefab, placement.Position, placement.Rotation, batch.Parent);
        instance.transform.localScale = placement.Scale;
        if (!float.IsNaN(placement.ExpectedMinY))
            CorrectHeight(instance, placement.ExpectedMinY);
        return instance;
    }

    /// <summary>
    /// The after-spawn check (Ground Contact > Verify After Spawn): measures the created object's renderers and
    /// moves it so their bottom is where placement meant it to be - catching prefabs whose shape changes when
    /// they are created (scripts, animators, LOD groups).
    /// </summary>
    private static void CorrectHeight(GameObject instance, float expectedMinY)
    {
        instance.GetComponentsInChildren(false, Renderers);
        bool any = false;
        float minY = 0f;
        foreach (Renderer renderer in Renderers)
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                continue;
            float y = renderer.bounds.min.y;
            minY = any ? Mathf.Min(minY, y) : y;
            any = true;
        }
        Renderers.Clear();

        float delta = expectedMinY - minY;
        if (any && Mathf.Abs(delta) > 0.005f)
            instance.transform.position += new Vector3(0f, delta, 0f);
    }
}
