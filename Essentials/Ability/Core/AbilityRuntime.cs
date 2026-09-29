using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Something an ability left in the world that needs updating every frame (projectile, zone, beam...).</summary>
public interface IAbilityRuntimeObject
{
    /// <summary>Advances by <paramref name="deltaTime"/>. Return false when finished.</summary>
    bool Tick(float deltaTime);

    /// <summary>Releases visuals and resources. Called once, after Tick returned false or when the runtime is cleared.</summary>
    void Dispose();
}

/// <summary>
/// Updates everything abilities leave in the world - projectiles, zones, ground surges, beams, barriers, telegraphs -
/// and runs delayed actions, from one place with no coroutines and no per-frame allocations. Created automatically
/// the first time an ability needs it and kept across scene loads.
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class AbilityRuntime : MonoBehaviour
{
    private struct Scheduled
    {
        public float time;
        public Action action;
        public object owner;
        public int id;
    }

    private static AbilityRuntime instance;
    private static bool quitting;

    private readonly List<IAbilityRuntimeObject> objects = new List<IAbilityRuntimeObject>(128);
    private readonly List<Scheduled> scheduled = new List<Scheduled>(64);
    private readonly List<Scheduled> due = new List<Scheduled>(16);
    private int nextId = 1;

    /// <summary>The runtime (created on first use; null while the application quits).</summary>
    public static AbilityRuntime Instance
    {
        get
        {
            if (instance == null && !quitting && Application.isPlaying)
            {
                var go = new GameObject("[Ability Runtime]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AbilityRuntime>();
            }
            return instance;
        }
    }

    public static bool Exists => instance != null;

    /// <summary>Number of live runtime objects (debug).</summary>
    public int ObjectCount => objects.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        Application.quitting += OnQuitting;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Application.quitting -= OnQuitting;
        if (instance == this)
        {
            ClearAll();
            instance = null;
        }
    }

    private static void OnQuitting() => quitting = true;

    private void OnSceneUnloaded(Scene scene)
    {
        CombatEntity.ClearLookupCache();
    }

    /// <summary>Adds a runtime object to be ticked every frame.</summary>
    public static void Add(IAbilityRuntimeObject obj)
    {
        AbilityRuntime rt = Instance;
        if (rt == null || obj == null)
        {
            obj?.Dispose();
            return;
        }
        rt.objects.Add(obj);
    }

    /// <summary>Runs <paramref name="action"/> after <paramref name="delay"/> seconds. Returns an id for cancelling.</summary>
    public static int Schedule(float delay, Action action, object owner = null)
    {
        AbilityRuntime rt = Instance;
        if (rt == null || action == null)
            return 0;
        if (delay <= 0f)
        {
            SafeInvoke(action);
            return 0;
        }
        var s = new Scheduled { time = Time.time + delay, action = action, owner = owner, id = rt.nextId++ };
        // Keep sorted by time (small lists; insertion from the back is cheap).
        int i = rt.scheduled.Count;
        while (i > 0 && rt.scheduled[i - 1].time > s.time)
            i--;
        rt.scheduled.Insert(i, s);
        return s.id;
    }

    /// <summary>Cancels one scheduled action by id.</summary>
    public static void Cancel(int id)
    {
        if (instance == null || id == 0)
            return;
        List<Scheduled> list = instance.scheduled;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].id == id)
            {
                list.RemoveAt(i);
                return;
            }
        }
    }

    /// <summary>Cancels every scheduled action of <paramref name="owner"/> (an interrupted cast, a dead caster).</summary>
    public static void CancelAll(object owner)
    {
        if (instance == null || owner == null)
            return;
        List<Scheduled> list = instance.scheduled;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list[i].owner, owner))
                list.RemoveAt(i);
        }
    }

    /// <summary>Removes and disposes every runtime object and scheduled action.</summary>
    public void ClearAll()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            try { objects[i].Dispose(); }
            catch (Exception e) { Debug.LogException(e); }
        }
        objects.Clear();
        scheduled.Clear();
        HazardRegistry.Clear();
    }

    private void Update()
    {
        float now = Time.time;

        // Due actions (collected first: an action may schedule more).
        due.Clear();
        int n = 0;
        while (n < scheduled.Count && scheduled[n].time <= now)
            n++;
        for (int i = 0; i < n; i++)
            due.Add(scheduled[i]);
        if (n > 0)
            scheduled.RemoveRange(0, n);
        for (int i = 0; i < due.Count; i++)
            SafeInvoke(due[i].action);
        due.Clear();

        float dt = Time.deltaTime;
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (i >= objects.Count)
                continue;
            IAbilityRuntimeObject obj = objects[i];
            bool keep;
            try
            {
                keep = obj.Tick(dt);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                keep = false;
            }
            if (!keep)
            {
                int last = objects.Count - 1;
                objects[i] = objects[last];
                objects.RemoveAt(last);
                try { obj.Dispose(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        HazardRegistry.Cleanup(now);
    }

    private static void SafeInvoke(Action action)
    {
        try { action(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}

/// <summary>
/// Reuses GameObjects (VFX, projectile visuals, telegraphs, barrier segments) instead of instantiating and destroying
/// them every cast.
/// </summary>
public static class AbilityPool
{
    private static readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
    private static readonly Dictionary<GameObject, GameObject> prefabOf = new Dictionary<GameObject, GameObject>(ReferenceComparer<GameObject>.Instance);
    private static Transform container;
    private static readonly List<ParticleSystem> particleBuffer = new List<ParticleSystem>(8);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pools.Clear();
        prefabOf.Clear();
        container = null;
    }

    private static Transform Container
    {
        get
        {
            if (container == null)
            {
                AbilityRuntime rt = AbilityRuntime.Instance;
                if (rt != null)
                {
                    var go = new GameObject("[Pooled]");
                    go.transform.SetParent(rt.transform, false);
                    container = go.transform;
                }
            }
            return container;
        }
    }

    /// <summary>An active instance of <paramref name="prefab"/> (reused when possible). Null prefab returns null.</summary>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
            return null;
        GameObject go = null;
        if (pools.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0 && go == null)
                go = stack.Pop();
        }

        if (go == null)
        {
            go = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
            prefabOf[go] = prefab;
        }
        else
        {
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = prefab.transform.localScale;
            go.SetActive(true);
        }

        RestartParticles(go);
        return go;
    }

    /// <summary>Returns an instance to its pool (or destroys it if it was not spawned by the pool).</summary>
    public static void Release(GameObject go)
    {
        if (go == null)
            return;
        if (!prefabOf.TryGetValue(go, out GameObject prefab) || prefab == null)
        {
            UnityEngine.Object.Destroy(go);
            return;
        }
        if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            pools[prefab] = stack;
        }
        go.SetActive(false);
        Transform c = Container;
        if (c != null)
            go.transform.SetParent(c, false);
        stack.Push(go);
    }

    /// <summary>Returns an instance to its pool after <paramref name="seconds"/>.</summary>
    public static void ReleaseAfter(GameObject go, float seconds)
    {
        if (go == null)
            return;
        if (seconds <= 0f)
        {
            Release(go);
            return;
        }
        AbilityRuntime.Schedule(seconds, () => Release(go), go);
    }

    /// <summary>Creates <paramref name="count"/> inactive instances ahead of time (avoids hitches on the first cast).</summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null)
            return;
        for (int i = 0; i < count; i++)
            Release(Spawn(prefab, new Vector3(0f, -10000f, 0f), Quaternion.identity));
    }

    private static void RestartParticles(GameObject go)
    {
        particleBuffer.Clear();
        go.GetComponentsInChildren(true, particleBuffer);
        for (int i = 0; i < particleBuffer.Count; i++)
        {
            ParticleSystem ps = particleBuffer[i];
            ps.Clear(false);
            ps.Play(false);
        }
        particleBuffer.Clear();
    }

    /// <summary>Longest duration + lifetime of the particle systems in <paramref name="go"/> (0 if none).</summary>
    public static float EstimateLifetime(GameObject go)
    {
        if (go == null)
            return 0f;
        float longest = 0f;
        particleBuffer.Clear();
        go.GetComponentsInChildren(true, particleBuffer);
        for (int i = 0; i < particleBuffer.Count; i++)
        {
            ParticleSystem.MainModule main = particleBuffer[i].main;
            if (main.loop)
                continue;
            longest = Mathf.Max(longest, main.duration + main.startLifetime.constantMax);
        }
        particleBuffer.Clear();
        return longest;
    }

    /// <summary>
    /// Plays a VFX prefab: pooled, optionally attached to <paramref name="follow"/>, scaled, and released after
    /// <paramref name="lifetime"/> seconds (0 = the particles' own length, or 3 s).
    /// </summary>
    public static GameObject PlayVfx(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime = 0f, float scale = 1f, Transform follow = null)
    {
        if (prefab == null)
            return null;
        GameObject go = Spawn(prefab, position, rotation, follow);
        if (go == null)
            return null;
        if (follow != null)
            go.transform.SetPositionAndRotation(position, rotation);
        if (!Mathf.Approximately(scale, 1f))
        {
            go.transform.localScale = prefab.transform.localScale * scale;
            particleBuffer.Clear();
            go.GetComponentsInChildren(true, particleBuffer);
            for (int i = 0; i < particleBuffer.Count; i++)
            {
                ParticleSystem.MainModule main = particleBuffer[i].main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            particleBuffer.Clear();
        }
        if (lifetime <= 0f)
            lifetime = EstimateLifetime(go);
        if (lifetime <= 0f)
            lifetime = 3f;
        ReleaseAfter(go, lifetime);
        return go;
    }

    /// <summary>Plays a one-shot sound at a position (no pooling needed).</summary>
    public static void PlaySound(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, volume);
    }
}

/// <summary>
/// Compares objects by reference (identity), ignoring Unity's overloaded equality. Used as a dictionary comparer so
/// lookups do not depend on instance ids (GetInstanceID is obsolete in recent Unity versions) and destroyed objects
/// never match other objects.
/// </summary>
public sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
    public bool Equals(T a, T b) => ReferenceEquals(a, b);
    public int GetHashCode(T obj) => obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
