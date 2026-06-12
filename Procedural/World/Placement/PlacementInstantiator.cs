using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

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

    private static GameObject Spawn(Batch batch, ObjectPlacement placement)
    {
        PlacementType type = batch.Plan.Types[placement.Type];
        if (type.Prefab == null)
            return null;

        GameObject instance = Object.Instantiate(type.Prefab, placement.Position, placement.Rotation, batch.Parent);
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
