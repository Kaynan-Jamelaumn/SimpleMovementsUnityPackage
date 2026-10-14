using System;
using UnityEngine;

/// <summary>
/// The zone of a place as the world spawners see it. A game layer with zones answers it through
/// <see cref="WorldSpawnHooks.ZoneAt"/> (the Hub World: its rings around the city - Safe Zone, Frontier, Dangerous
/// Zone, The Abyss). Without one every place is <see cref="Unknown"/> and zone rules let everything through.
/// </summary>
public struct SpawnZone
{
    /// <summary>False when no zone system is installed (every <see cref="ZoneFilter"/> passes).</summary>
    public bool Known;
    /// <summary>Ring index: -1 inside the city, 0 the first ring around it, 1 the next one...</summary>
    public int Index;
    /// <summary>The zone's name ("Safe Zone", "Frontier"...).</summary>
    public string Name;
    /// <summary>Players may fight each other here.</summary>
    public bool Pvp;

    /// <summary>No zone system: every zone rule passes.</summary>
    public static readonly SpawnZone Unknown = default;

    public override string ToString() => Known ? $"{Name} (ring {Index}{(Pvp ? ", PvP" : "")})" : "no zones";
}

/// <summary>Where a <see cref="ZoneFilter"/> allows things by PvP.</summary>
public enum ZonePvpRule
{
    /// <summary>PvP zones and peaceful zones alike.</summary>
    Any,
    /// <summary>Only where players may fight each other.</summary>
    OnlyPvp,
    /// <summary>Only where players may not fight each other.</summary>
    OnlyNonPvp,
}

/// <summary>
/// In which zones of the world a portal type or mob type may appear (e.g. a beginner dungeon only in the Safe Zone, a
/// raid portal only in the Dangerous Zone and beyond, a PvP arena portal only where PvP is on). Zones come from the
/// game (see <see cref="WorldSpawnHooks.ZoneAt"/>); without zones the filter lets everything through.
/// </summary>
[Serializable]
public class ZoneFilter
{
    [Tooltip("Limit where this appears by the world's zones (the Hub World's rings around the city). Off = any zone. " +
             "Ignored in a world without zones.")]
    public bool enabled;

    [Tooltip("Names of the zones it may appear in, as written in Hub World Settings > Zones > Rings (e.g. \"Safe Zone\", " +
             "\"Frontier\", \"Dangerous Zone\"; case does not matter). Empty = any name (the ring range and PvP rule still apply).")]
    public string[] zoneNames = new string[0];

    [Tooltip("Nearest ring it may appear in: 0 = the first ring around the city (the Safe Zone with the default rings), 1 = the next...")]
    [Min(0)] public int minRing;

    [Tooltip("Farthest ring it may appear in (-1 = no limit).")]
    [Min(-1)] public int maxRing = -1;

    [Tooltip("Only where players may fight each other, only where they may not, or either.")]
    public ZonePvpRule pvp = ZonePvpRule.Any;

    /// <summary>May it appear in <paramref name="zone"/>?</summary>
    public bool Allows(in SpawnZone zone)
    {
        if (!enabled || !zone.Known)
            return true;
        if (zone.Index < minRing)
            return false;
        if (maxRing >= 0 && zone.Index > maxRing)
            return false;
        if (pvp == ZonePvpRule.OnlyPvp && !zone.Pvp)
            return false;
        if (pvp == ZonePvpRule.OnlyNonPvp && zone.Pvp)
            return false;
        if (zoneNames == null || zoneNames.Length == 0)
            return true;
        bool anyName = false;
        foreach (string n in zoneNames)
        {
            if (string.IsNullOrWhiteSpace(n))
                continue;
            anyName = true;
            if (zone.Name != null && string.Equals(n.Trim(), zone.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return !anyName;
    }

    /// <summary>Clamps the values (the farthest ring is never nearer than the nearest one).</summary>
    public void Validate()
    {
        minRing = Mathf.Max(0, minRing);
        if (maxRing < -1)
            maxRing = -1;
        if (maxRing >= 0 && maxRing < minRing)
            maxRing = minRing;
    }

    /// <summary>The rule in words (inspectors and logs).</summary>
    public string Describe()
    {
        if (!enabled)
            return "any zone";
        var parts = new System.Collections.Generic.List<string>();
        if (zoneNames != null)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (string n in zoneNames)
                if (!string.IsNullOrWhiteSpace(n))
                    names.Add(n.Trim());
            if (names.Count > 0)
                parts.Add(string.Join(" / ", names));
        }
        if (minRing > 0 || maxRing >= 0)
            parts.Add(maxRing >= 0 ? $"rings {minRing}-{maxRing}" : $"ring {minRing} and beyond");
        if (pvp != ZonePvpRule.Any)
            parts.Add(pvp == ZonePvpRule.OnlyPvp ? "PvP zones only" : "peaceful zones only");
        return parts.Count > 0 ? string.Join(", ", parts) : "any zone";
    }
}
