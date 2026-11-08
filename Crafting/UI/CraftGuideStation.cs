using System;
using UnityEngine;

/// <summary>The guide asked at a station (see the rules in Crafting/Core/CraftGuide.cs): with its tools and the items tried there.</summary>
public static partial class CraftGuide
{
    /// <summary>The next step for <paramref name="p"/> at <paramref name="station"/> (nothing picked: what its furnace holds), or null when nothing in particular is next.</summary>
    public static Step? Next(CraftStation station, CraftPiece p, CraftJournalData known)
    {
        if (station == null || station.Workshop == null)
            return null;
        return Next(station.Workshop, station.Tools, p, known, station.IsInspected);
    }

    /// <summary>
    /// The area keys name the work areas by their types' names: true when every one of <see cref="AreaKeys"/> is the name of
    /// one of these types (the window warns once when an area was renamed without its key, so no Go goes missing silently).
    /// </summary>
    public static bool KeysMatch(Type[] areas)
    {
        foreach (string key in AreaKeys)
            if (Array.Find(areas, t => t != null && t.Name == key) == null)
            {
                Debug.LogWarning($"[Crafting] The guide names the work area \"{key}\", which no area is called: rename the key in CraftGuide with the area.");
                return false;
            }
        return true;
    }
}
