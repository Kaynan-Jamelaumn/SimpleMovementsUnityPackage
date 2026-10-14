using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Decides when a character is underwater, for its <c>OxygenManager</c>: its HEAD must be under the actual water
/// surface at its position (<see cref="WaterQuery"/>), checked every frame while the character is inside a water
/// trigger. The triggers (the terrain chunks' <see cref="WaterVolume"/> boxes, <see cref="SwimmableWater"/>) are only
/// a coarse "near water" filter: a chunk's box covers its whole footprint from the deepest bed to the highest surface,
/// so being inside it does not mean being in the water. Several overlapping triggers (chunk borders) count as one.
/// </summary>
public static class Submersion
{
    private sealed class Entry
    {
        public OxygenManager oxygen;
        public readonly HashSet<Object> sources = new HashSet<Object>();
        public bool underwater;
    }

    /// <summary>How far below the top of a character's collider its eyes are (metres).</summary>
    public static float EyeDepth = 0.2f;

    private static readonly Dictionary<OxygenManager, Entry> entries = new Dictionary<OxygenManager, Entry>();
    private static readonly List<OxygenManager> gone = new List<OxygenManager>();
    private static int evaluatedFrame = -1;
    private static Runner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries.Clear();
        gone.Clear();
        evaluatedFrame = -1;
        runner = null;
    }

    /// <summary>A character's collider entered a water trigger (<paramref name="source"/>).</summary>
    public static void Enter(Object source, Collider other)
    {
        OxygenManager oxygen = other != null ? other.GetComponentInParent<OxygenManager>() : null;
        if (oxygen == null)
            return;
        if (!entries.TryGetValue(oxygen, out Entry e))
            entries[oxygen] = e = new Entry { oxygen = oxygen };
        e.sources.Add(source);
        EnsureRunner();
    }

    /// <summary>A character's collider left a water trigger.</summary>
    public static void Exit(Object source, Collider other)
    {
        OxygenManager oxygen = other != null ? other.GetComponentInParent<OxygenManager>() : null;
        if (oxygen == null || !entries.TryGetValue(oxygen, out Entry e))
            return;
        e.sources.Remove(source);
        if (e.sources.Count == 0)
            Remove(e);
    }

    /// <summary>A water trigger went away (its chunk was hidden or unloaded): it no longer holds anyone.</summary>
    public static void ForgetSource(Object source)
    {
        gone.Clear();
        foreach (Entry e in entries.Values)
            if (e.sources.Remove(source) && e.sources.Count == 0)
                gone.Add(e.oxygen);
        foreach (OxygenManager o in gone)
            if (o != null && entries.TryGetValue(o, out Entry e))
                Remove(e);
        gone.Clear();
    }

    /// <summary>Is the character's head under the water surface right now?</summary>
    public static bool IsHeadUnderwater(Component character)
    {
        if (character == null)
            return false;
        Vector3 head = HeadOf(character, out GameObject body);
        return WaterQuery.IsUnderwater(head, body);
    }

    /// <summary>Where a character's eyes are: the top of its CharacterController, NavMeshAgent or colliders, minus <see cref="EyeDepth"/>.</summary>
    public static Vector3 HeadOf(Component character, out GameObject body)
    {
        CharacterController cc = character.GetComponentInParent<CharacterController>();
        if (cc == null)
            cc = character.transform.root.GetComponentInChildren<CharacterController>();
        if (cc != null && cc.enabled)
        {
            body = cc.gameObject;
            Bounds b = cc.bounds;
            return new Vector3(b.center.x, b.max.y - EyeDepth, b.center.z);
        }
        NavMeshAgent agent = character.GetComponentInParent<NavMeshAgent>();
        if (agent != null)
        {
            body = agent.gameObject;
            Vector3 p = agent.transform.position;
            return new Vector3(p.x, p.y + Mathf.Max(0.2f, agent.height - EyeDepth), p.z);
        }
        Transform root = character.transform.root;
        body = root.gameObject;
        bool any = false;
        Bounds all = default;
        foreach (Collider c in root.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled)
                continue;
            if (!any)
                all = c.bounds;
            else
                all.Encapsulate(c.bounds);
            any = true;
        }
        if (any)
            return new Vector3(all.center.x, all.max.y - EyeDepth, all.center.z);
        return root.position + Vector3.up * 1.6f;
    }

    private static void Evaluate()
    {
        if (evaluatedFrame == Time.frameCount)
            return;
        evaluatedFrame = Time.frameCount;
        gone.Clear();
        foreach (KeyValuePair<OxygenManager, Entry> pair in entries)
        {
            if (pair.Key == null)
            {
                gone.Add(pair.Key);
                continue;
            }
            Entry e = pair.Value;
            bool under = IsHeadUnderwater(e.oxygen);
            if (under != e.underwater)
            {
                e.underwater = under;
                e.oxygen.SetUnderwater(under);
            }
        }
        foreach (OxygenManager o in gone)
            entries.Remove(o);
        gone.Clear();
    }

    private static void Remove(Entry e)
    {
        entries.Remove(e.oxygen);
        if (e.underwater && e.oxygen != null)
            e.oxygen.SetUnderwater(false);
    }

    private static void EnsureRunner()
    {
        if (runner != null)
            return;
        var go = new GameObject("Submersion") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    private sealed class Runner : MonoBehaviour
    {
        private void Update() => Evaluate();
    }
}
