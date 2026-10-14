using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a <see cref="WorldArea"/> does to the terrain and the spawners.</summary>
[Flags]
public enum WorldAreaRules
{
    None = 0,
    /// <summary>The ground is levelled to the area's height (blended into the terrain around it).</summary>
    Flatten = 1 << 0,
    /// <summary>No trees, rocks or other placed objects.</summary>
    NoObjects = 1 << 1,
    /// <summary>No world mobs spawn.</summary>
    NoMobs = 1 << 2,
    /// <summary>No world portals appear.</summary>
    NoPortals = 1 << 3,
    /// <summary>
    /// No lakes, ponds, rivers or ocean in the area and its blend ring: water placed by the generator would otherwise
    /// keep its own level while the ground under it is levelled, leaving odd patches of water around the area.
    /// </summary>
    NoWater = 1 << 4,
    Everything = Flatten | NoObjects | NoMobs | NoPortals | NoWater,
}

/// <summary>
/// A circle of the world kept for something built there - a town, the hub city's outer walls: levelled ground, no
/// objects, no mobs, no portals (see <see cref="Rules"/>).
/// </summary>
[Serializable]
public struct WorldArea
{
    [Tooltip("Centre (world x, z).")]
    public Vector2 center;
    [Tooltip("Radius of the area itself (metres): fully levelled and kept clear.")]
    [Min(0f)] public float radius;
    [Tooltip("Width of the ring around it where levelled ground blends into the natural terrain (metres).")]
    [Min(0f)] public float blend;
    [Tooltip("Ground height of the levelled area (world y).")]
    public float height;
    [Tooltip("What the area does.")]
    public WorldAreaRules rules;

    public WorldArea(Vector2 center, float radius, float blend, float height, WorldAreaRules rules)
    {
        this.center = center;
        this.radius = radius;
        this.blend = blend;
        this.height = height;
        this.rules = rules;
    }

    public bool Has(WorldAreaRules r) => (rules & r) == r;
}

/// <summary>
/// The <see cref="WorldArea"/>s of the current world. Read from the terrain worker threads (heights, object placement)
/// and the spawners, so set them BEFORE the world is generated - e.g. before loading the world scene - and do not
/// change them while chunks are being generated (changing them later only affects chunks generated afterwards).
/// Coordinates are terrain cells, which are world units at the Terrain Generator's Scale Factor 1.
/// </summary>
public static class WorldAreas
{
    // Replaced as a whole (never modified in place), so worker threads always read a consistent array.
    private static volatile WorldArea[] areas = Array.Empty<WorldArea>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => areas = Array.Empty<WorldArea>();

    public static IReadOnlyList<WorldArea> All => areas;

    public static int Count => areas.Length;

    /// <summary>Replaces every area.</summary>
    public static void Set(IEnumerable<WorldArea> list)
    {
        areas = list != null ? new List<WorldArea>(list).ToArray() : Array.Empty<WorldArea>();
    }

    public static void Add(WorldArea area)
    {
        WorldArea[] old = areas;
        var next = new WorldArea[old.Length + 1];
        Array.Copy(old, next, old.Length);
        next[old.Length] = area;
        areas = next;
    }

    public static void Clear() => areas = Array.Empty<WorldArea>();

    /// <summary>Is (x, z) inside an area with <paramref name="rules"/> (its radius, not the blend ring)?</summary>
    public static bool Has(WorldAreaRules rules, float x, float z)
    {
        WorldArea[] a = areas;
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Has(rules))
                continue;
            float dx = x - a[i].center.x, dz = z - a[i].center.y;
            if (dx * dx + dz * dz <= a[i].radius * a[i].radius)
                return true;
        }
        return false;
    }

    /// <summary>Is (x, z) inside an area with <paramref name="rules"/>, its blend ring included?</summary>
    public static bool Within(WorldAreaRules rules, float x, float z)
    {
        WorldArea[] a = areas;
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Has(rules))
                continue;
            float reach = a[i].radius + a[i].blend;
            float dx = x - a[i].center.x, dz = z - a[i].center.y;
            if (dx * dx + dz * dz <= reach * reach)
                return true;
        }
        return false;
    }

    /// <summary>Does a circle (<paramref name="center"/>, <paramref name="radius"/>) touch an area with <paramref name="rules"/> or its blend ring?</summary>
    public static bool Touches(WorldAreaRules rules, Vector2 center, float radius)
    {
        WorldArea[] a = areas;
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Has(rules))
                continue;
            float reach = a[i].radius + a[i].blend + Mathf.Max(0f, radius);
            float dx = center.x - a[i].center.x, dz = center.y - a[i].center.y;
            if (dx * dx + dz * dz <= reach * reach)
                return true;
        }
        return false;
    }

    /// <summary>The height at (x, z) once the levelled areas are applied to <paramref name="height"/>.</summary>
    public static float Flatten(float x, float z, float height)
    {
        WorldArea[] a = areas;
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Has(WorldAreaRules.Flatten))
                continue;
            float dx = x - a[i].center.x, dz = z - a[i].center.y;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d <= a[i].radius)
                return a[i].height;
            if (a[i].blend <= 0f || d >= a[i].radius + a[i].blend)
                continue;
            float t = (d - a[i].radius) / a[i].blend;
            t = t * t * (3f - 2f * t); // smoothstep: flat at the edge, natural further out
            height = Mathf.Lerp(a[i].height, height, t);
        }
        return height;
    }

    /// <summary>
    /// Levels a block of heights whose cell [x, y] is at world (origin.x + x, origin.y + y). Cheap when no levelled
    /// area touches the block. Called by the height generator for every chunk.
    /// </summary>
    public static void Flatten(float[,] heights, Vector2 origin)
    {
        WorldArea[] a = areas;
        if (a.Length == 0 || heights == null)
            return;
        int w = heights.GetLength(0), h = heights.GetLength(1);
        bool touches = false;
        for (int i = 0; i < a.Length && !touches; i++)
        {
            if (!a[i].Has(WorldAreaRules.Flatten))
                continue;
            float reach = a[i].radius + a[i].blend;
            touches = a[i].center.x + reach >= origin.x && a[i].center.x - reach <= origin.x + w &&
                      a[i].center.y + reach >= origin.y && a[i].center.y - reach <= origin.y + h;
        }
        if (!touches)
            return;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                heights[x, y] = Flatten(origin.x + x, origin.y + y, heights[x, y]);
    }
}
