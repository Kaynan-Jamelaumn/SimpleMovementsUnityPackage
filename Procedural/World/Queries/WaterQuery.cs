using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The water at a point, for swimming and drowning: the generated terrain's oceans, lakes, ponds and rivers (exact
/// per cell, from the loaded chunks' water maps) and hand-placed <see cref="SwimmableWater"/> volumes (pools, a lake in a
/// city or a dungeon). Main thread only.
/// </summary>
public static class WaterQuery
{
    private static readonly List<SwimmableWater> volumes = new List<SwimmableWater>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => volumes.Clear();

    internal static void Register(SwimmableWater volume)
    {
        if (volume != null && !volumes.Contains(volume))
            volumes.Add(volume);
    }

    internal static void Unregister(SwimmableWater volume) => volumes.Remove(volume);

    /// <summary>
    /// The water around <paramref name="position"/> (a character's feet), if any: its surface height and the height
    /// of the bottom under it. <paramref name="who"/> is the character asking - the terrain's water only counts for
    /// characters on the streamed terrain (not inside a dungeon standing over a lake of the world above).
    /// </summary>
    public static bool TryGetWater(Vector3 position, GameObject who, out float surface, out float bottom)
    {
        surface = float.NaN;
        bottom = float.NaN;

        // Hand-placed volumes first (they are where they are, in any scene).
        for (int i = volumes.Count - 1; i >= 0; i--)
        {
            SwimmableWater v = volumes[i];
            if (v == null)
            {
                volumes.RemoveAt(i);
                continue;
            }
            if (who != null && v.gameObject.scene != who.scene)
                continue;
            if (v.TryGetWater(position, out surface, out bottom))
                return true;
        }

        if (!StreamedGround.Applies(who))
            return false;
        if (LoadedTerrain.WaterAt(position.x, position.z, out surface) == WaterBodyType.None || float.IsNaN(surface))
            return false;
        if (!LoadedTerrain.TryGetHeight(position.x, position.z, out bottom))
            bottom = surface - 100f;
        // Below the bottom (in a cave, under the terrain): not in this water.
        if (position.y < bottom - 2f)
            return false;
        return true;
    }

    /// <summary>True when <paramref name="point"/> (a head, an eye) is under the water surface.</summary>
    public static bool IsUnderwater(Vector3 point, GameObject who) =>
        TryGetWater(point, who, out float surface, out _) && point.y < surface;
}
