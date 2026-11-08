using System;
using System.Collections.Generic;

/// <summary>
/// The world's discoveries about harvested materials. A material can name a discovery (<see cref="HarvestMaterial.discovery"/>):
/// until it is made, the material is worth its Value and good for its own uses; once a script makes it known -
/// <c>HarvestDiscoveries.Unlock("troll_regeneration")</c> when a recipe is found, a scholar's research is done, a quest
/// ends - it is worth its Discovered Value and gains its Discovered Uses, for everyone, pieces already harvested
/// included. A troll's blood is a curiosity until someone learns to brew a regeneration tonic from it.
/// <para>Unlock on the server or in single player; the runtime saves them (<c>persistentDataPath/Harvesting/discoveries.json</c>)
/// and a network session sends them to its players with their harvesting skills.</para>
/// </summary>
public static class HarvestDiscoveries
{
    private static readonly HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>A discovery was made (its key).</summary>
    public static event Action<string> Unlocked;
    /// <summary>The set of discoveries changed (made, forgotten, replaced).</summary>
    public static event Action Changed;

    /// <summary>The discoveries made.</summary>
    public static IReadOnlyCollection<string> Keys => keys;

    public static bool IsUnlocked(string key) => !string.IsNullOrWhiteSpace(key) && keys.Contains(key.Trim());

    /// <summary>Makes the discovery <paramref name="key"/>. Returns false when it was already made (or the key is blank).</summary>
    public static bool Unlock(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !keys.Add(key.Trim()))
            return false;
        Unlocked?.Invoke(key.Trim());
        Changed?.Invoke();
        return true;
    }

    /// <summary>Forgets the discovery <paramref name="key"/> (debug, a new world).</summary>
    public static bool Forget(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !keys.Remove(key.Trim()))
            return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Replaces every discovery (loading a save, a client receiving the server's).</summary>
    public static void SetAll(IEnumerable<string> all)
    {
        var next = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (all != null)
            foreach (string k in all)
                if (!string.IsNullOrWhiteSpace(k)) next.Add(k.Trim());
        if (next.SetEquals(keys))
            return;
        keys.Clear();
        keys.UnionWith(next);
        Changed?.Invoke();
    }

    /// <summary>Forgets every discovery.</summary>
    public static void Clear() => SetAll(null);

    /// <summary>The materials of <paramref name="anatomies"/> that name <paramref name="key"/> ("Troll blood"), for messages and tools.</summary>
    public static List<string> MaterialsOf(string key, IEnumerable<HarvestAnatomyData> anatomies)
    {
        var l = new List<string>();
        if (string.IsNullOrWhiteSpace(key) || anatomies == null)
            return l;
        foreach (HarvestAnatomyData a in anatomies)
            if (a?.materials != null)
                foreach (HarvestMaterial m in a.materials)
                    if (m != null && string.Equals(m.discovery?.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
                        l.Add(m.Name);
        return l;
    }
}
