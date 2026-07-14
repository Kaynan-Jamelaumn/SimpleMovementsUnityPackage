using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>What placed objects switch off while their chunk is far from the viewer (see <see cref="FarObjectSwitcher"/>).</summary>
[Flags]
public enum FarObjectParts
{
    /// <summary>Colliders (Rigidbodies are made kinematic meanwhile, so nothing falls through the ground).</summary>
    Colliders = 1,
    /// <summary>Scripts (MonoBehaviours), except those implementing <see cref="IFarTerrainObject"/>.</summary>
    Scripts = 2,
    /// <summary>Animator and legacy Animation components.</summary>
    Animators = 4,
    /// <summary>Audio Sources.</summary>
    Audio = 8,
    /// <summary>Lights.</summary>
    Lights = 16,
}

/// <summary>
/// Optional, for scripts on placed objects: instead of being switched off while their chunk is far from the viewer,
/// the script stays on and is told, so it can do its own thing (go to sleep, drop to a cheap update...).
/// </summary>
public interface IFarTerrainObject
{
    /// <summary>The object's chunk is now beyond Full Object Distance (its colliders, animators... may be off).</summary>
    void OnFar();

    /// <summary>The object's chunk is near again: everything that was switched off is back on.</summary>
    void OnNear();
}

/// <summary>
/// Gives only the chunks near the viewer their full objects: in chunks beyond Full Object Distance, placed objects
/// keep their renderers (they look the same) but their colliders, scripts, animators, audio or lights - whichever
/// Far Object Parts lists - are switched off, and switched back on when the chunk comes near again. Switching is
/// spread over frames (within the Objects' spawn budget), nearest chunks first, and it only ever turns off what
/// was on, so it restores exactly the state it found.
/// </summary>
public sealed class FarObjectSwitcher
{
    /// <summary>One chunk's objects.</summary>
    public sealed class Chunk
    {
        internal Transform Parent;
        internal GameObject[] Objects;
        internal Func<float> Distance;
        internal bool WantFar;
        internal bool Queued;
        internal bool Removed;
        /// <summary>Objects [0, Next) have been switched to far.</summary>
        internal int Next;
        internal FarObjectParts AppliedParts;
        internal readonly List<Change> Changes = new List<Change>();
        internal readonly List<IFarTerrainObject> Told = new List<IFarTerrainObject>();

        /// <summary>Its objects are switched to far (all of them).</summary>
        public bool IsFar => WantFar && !Queued && Objects != null && Next >= Objects.Length;

        /// <summary>Something of its objects is switched off (or told it is far).</summary>
        public bool HasChanges => Changes.Count > 0 || Told.Count > 0;
    }

    internal enum ChangeKind : byte { Behaviour, Collider, Rigidbody }

    internal struct Change
    {
        public Object Target;
        public ChangeKind Kind;
        /// <summary>Index of the object it belongs to, so an interrupted restore knows where switching to far must resume.</summary>
        public int Index;
    }

    /// <summary>Chunks whose nearest edge is within this distance of the viewer have full objects. 0 = every chunk.</summary>
    public float FullDistance;
    /// <summary>What far objects switch off. Nothing = the feature is off.</summary>
    public FarObjectParts Parts;

    private readonly List<Chunk> queue = new List<Chunk>();
    private readonly List<Chunk> tracked = new List<Chunk>();
    private readonly Stopwatch watch = new Stopwatch();
    private static readonly List<Collider> Colliders = new List<Collider>();
    private static readonly List<Rigidbody> Bodies = new List<Rigidbody>();
    private static readonly List<Behaviour> Behaviours = new List<Behaviour>();

    /// <summary>Chunks whose objects are currently switched to far.</summary>
    public int FarChunks
    {
        get
        {
            int count = 0;
            foreach (Chunk chunk in tracked)
                if (chunk.IsFar)
                    count++;
            return count;
        }
    }

    /// <summary>Chunks waiting to be switched (either way).</summary>
    public int SwitchingChunks => queue.Count;

    /// <summary>Beyond Full Distance, a chunk only switches to far this much further out, so one near the limit doesn't flip back and forth.</summary>
    public float Margin => Mathf.Max(20f, FullDistance * 0.2f);

    private bool Enabled => FullDistance > 0f && Parts != 0;

    /// <summary>Starts managing a chunk's created objects (the array is read live, so objects removed later are skipped).</summary>
    public Chunk Track(Transform parent, GameObject[] objects, Func<float> distance)
    {
        var chunk = new Chunk { Parent = parent, Objects = objects, Distance = distance };
        tracked.Add(chunk);
        if (distance != null)
            Refresh(chunk, distance());
        return chunk;
    }

    /// <summary>Decides from the chunk's distance to the viewer whether its objects should be full or far (call every frame or so).</summary>
    public void Refresh(Chunk chunk, float distance)
    {
        if (chunk == null || chunk.Removed)
            return;
        bool want = chunk.WantFar;
        if (!Enabled || distance <= FullDistance)
            want = false;
        else if (distance > FullDistance + Margin)
            want = true;
        // Far Object Parts changed while it was far: bring it back first, then switch again with the new parts.
        if (want && chunk.Next > 0 && chunk.AppliedParts != Parts)
            want = false;

        if (want == chunk.WantFar)
        {
            // A far chunk whose parts changed back to full ends up here too once restored; nothing else to do.
            if (want && !chunk.IsFar && !chunk.Queued)
                Enqueue(chunk);
            return;
        }
        chunk.WantFar = want;
        Enqueue(chunk);
    }

    /// <summary>
    /// Stops managing a chunk (it is being unloaded). With <paramref name="restore"/>, everything switched off is
    /// switched back on right away - do it with the chunk's objects inactive (or pooled), so no script wakes up.
    /// </summary>
    public void Remove(Chunk chunk, bool restore)
    {
        if (chunk == null)
            return;
        chunk.Removed = true;
        queue.Remove(chunk);
        tracked.Remove(chunk);
        if (restore)
            RestoreAll(chunk, float.MaxValue);
    }

    /// <summary>Switches every tracked chunk back to full right away (e.g. when the feature is turned off).</summary>
    public void RestoreEverything()
    {
        foreach (Chunk chunk in tracked)
        {
            RestoreAll(chunk, float.MaxValue);
            chunk.WantFar = false;
        }
        queue.Clear();
        foreach (Chunk chunk in tracked)
            chunk.Queued = false;
    }

    /// <summary>
    /// Switches the colliders a far chunk turned off on or off again, without changing its state - for gathering
    /// NavMesh sources from colliders.
    /// </summary>
    public void SetSwitchedCollidersEnabled(Chunk chunk, bool enabled)
    {
        if (chunk == null)
            return;
        foreach (Change change in chunk.Changes)
        {
            if (change.Kind != ChangeKind.Collider)
                continue;
            var collider = change.Target as Collider;
            if (collider != null)
                collider.enabled = enabled;
        }
    }

    /// <summary>Switches waiting chunks, nearest first and chunks coming near before chunks going far, within the budget.</summary>
    public void Update(float budgetMs)
    {
        if (queue.Count == 0)
            return;
        watch.Restart();
        // At least one object (or 32 restored parts) per call, then until the budget is spent.
        while (queue.Count > 0)
        {
            Chunk chunk = Next();
            if (chunk == null)
                break;

            bool done = chunk.WantFar ? SwitchToFar(chunk, budgetMs) : RestoreAll(chunk, budgetMs);
            if (done)
            {
                queue.Remove(chunk);
                chunk.Queued = false;
            }
            if (watch.Elapsed.TotalMilliseconds >= budgetMs)
                break;
        }
    }

    private void Enqueue(Chunk chunk)
    {
        if (chunk.Queued)
            return;
        chunk.Queued = true;
        queue.Add(chunk);
    }

    /// <summary>The chunk to work on: one coming near (nearest first), else one going far (nearest first). Drops chunks that are gone.</summary>
    private Chunk Next()
    {
        Chunk best = null;
        float bestDistance = float.MaxValue;
        bool bestNear = false;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            Chunk chunk = queue[i];
            if (chunk.Removed || chunk.Parent == null)
            {
                chunk.Queued = false;
                queue.RemoveAt(i);
                continue;
            }
            bool near = !chunk.WantFar;
            float distance = chunk.Distance != null ? chunk.Distance() : 0f;
            if (best == null || (near && !bestNear) || (near == bestNear && distance < bestDistance))
            {
                best = chunk;
                bestDistance = distance;
                bestNear = near;
            }
        }
        return best;
    }

    /// <summary>Switches more of the chunk's objects to far. True when all are done.</summary>
    private bool SwitchToFar(Chunk chunk, float budgetMs)
    {
        if (chunk.Objects == null)
            return true;
        if (chunk.Next == 0)
            chunk.AppliedParts = Parts;
        while (chunk.Next < chunk.Objects.Length)
        {
            int index = chunk.Next++;
            GameObject instance = chunk.Objects[index];
            // Objects the game moved out of the chunk (or removed) are no longer the chunk's to switch.
            if (instance != null && instance.transform.parent == chunk.Parent && instance.GetComponent<TerrainObjectKeepFull>() == null)
                SwitchOff(instance, index, chunk);
            if (watch.Elapsed.TotalMilliseconds >= budgetMs)
                return chunk.Next >= chunk.Objects.Length;
        }
        return true;
    }

    private static void SwitchOff(GameObject instance, int index, Chunk chunk)
    {
        FarObjectParts parts = chunk.AppliedParts;
        if ((parts & FarObjectParts.Colliders) != 0)
        {
            // Kinematic first, so a dynamic body never falls in the moment between.
            instance.GetComponentsInChildren(true, Bodies);
            foreach (Rigidbody body in Bodies)
            {
                if (body.isKinematic)
                    continue;
                body.isKinematic = true;
                chunk.Changes.Add(new Change { Target = body, Kind = ChangeKind.Rigidbody, Index = index });
            }
            Bodies.Clear();

            instance.GetComponentsInChildren(true, Colliders);
            foreach (Collider collider in Colliders)
            {
                if (!collider.enabled)
                    continue;
                collider.enabled = false;
                chunk.Changes.Add(new Change { Target = collider, Kind = ChangeKind.Collider, Index = index });
            }
            Colliders.Clear();
        }

        instance.GetComponentsInChildren(true, Behaviours);
        foreach (Behaviour behaviour in Behaviours)
        {
            if (!behaviour.enabled)
                continue;
            var listener = behaviour as IFarTerrainObject;
            if (listener != null)
            {
                // Already told (a restore was interrupted and the chunk went far again before it was told OnNear).
                if (chunk.Told.Contains(listener))
                    continue;
                chunk.Told.Add(listener);
                try { listener.OnFar(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e, behaviour); }
                continue;
            }
            if ((parts & PartOf(behaviour)) == 0)
                continue;
            behaviour.enabled = false;
            chunk.Changes.Add(new Change { Target = behaviour, Kind = ChangeKind.Behaviour, Index = index });
        }
        Behaviours.Clear();
    }

    /// <summary>Which of the parts a component belongs to (0 = none: left alone).</summary>
    private static FarObjectParts PartOf(Behaviour behaviour)
    {
        if (behaviour is Animator || behaviour is Animation)
            return FarObjectParts.Animators;
        if (behaviour is AudioSource)
            return FarObjectParts.Audio;
        if (behaviour is Light)
            return FarObjectParts.Lights;
        if (behaviour is MonoBehaviour)
            return FarObjectParts.Scripts;
        return 0;
    }

    /// <summary>Switches back on what the chunk switched off, newest first. True when all is restored.</summary>
    private bool RestoreAll(Chunk chunk, float budgetMs)
    {
        List<Change> changes = chunk.Changes;
        int steps = 0;
        while (changes.Count > 0)
        {
            Change change = changes[changes.Count - 1];
            changes.RemoveAt(changes.Count - 1);
            // Objects from here on may be (partly) back on: if the chunk goes far again before this restore
            // finishes, switching resumes from here (it only switches off what is on, so nothing is done twice).
            chunk.Next = Mathf.Min(chunk.Next, change.Index);
            if (change.Target != null)
            {
                switch (change.Kind)
                {
                    case ChangeKind.Behaviour: ((Behaviour)change.Target).enabled = true; break;
                    case ChangeKind.Collider: ((Collider)change.Target).enabled = true; break;
                    case ChangeKind.Rigidbody: ((Rigidbody)change.Target).isKinematic = false; break;
                }
            }
            if (++steps % 32 == 0 && watch.IsRunning && watch.Elapsed.TotalMilliseconds >= budgetMs)
                return false;
        }

        foreach (IFarTerrainObject listener in chunk.Told)
        {
            if (listener as Object == null)
                continue;   // destroyed
            try { listener.OnNear(); }
            catch (Exception e) { UnityEngine.Debug.LogException(e, listener as Object); }
        }
        chunk.Told.Clear();
        chunk.Next = 0;
        return true;
    }
}
