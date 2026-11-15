using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The kind of water body occupying a cell. Values double as a priority order (lower wins) when a
/// water mesh quad touches more than one type - e.g. a river mouth quad that also touches the ocean
/// is rendered as ocean.
/// </summary>
public enum WaterBodyType : byte
{
    None = 0,
    Ocean = 1,
    Lake = 2,
    Pond = 3,
    River = 4,
    /// <summary>
    /// Only used by the water mesh: the steep sheets of water where a river (or lake outlet) drops over a
    /// waterfall, so they can get their own material. Water map cells are never this type.
    /// </summary>
    Waterfall = 5,
}

/// <summary>
/// Per-chunk water result, same dimensions as the chunk's height map.
/// </summary>
public sealed class WaterMapData
{
    /// <summary>Water surface height (world Y) at each wet cell, <see cref="float.NaN"/> where dry.</summary>
    public readonly float[,] Surface;

    /// <summary>
    /// For dry cells close to a water body: that body's water level, so the water mesh can extend its
    /// edge under the shore instead of stopping short of it (see <see cref="MeshGenerator.GenerateWaterMesh"/>).
    /// Equal to <see cref="Surface"/> on wet cells, <see cref="float.NaN"/> where no water is nearby.
    /// </summary>
    public readonly float[,] ShoreLevel;

    /// <summary>Which kind of water body each cell belongs to (<see cref="WaterBodyType.None"/> = dry).</summary>
    public readonly WaterBodyType[,] Type;

    /// <summary>
    /// Direction and speed the water flows at each wet cell (world X/Z, units per second-ish; 0 for lakes,
    /// ponds and the ocean) - for water shaders that scroll ripples and foam downstream.
    /// </summary>
    public readonly float[,] FlowX;
    public readonly float[,] FlowY;

    /// <summary>
    /// 0-1 how wet the ground is: 1 in and right beside water, fading with distance from and height above the
    /// nearest river, lake or sea - for darker, glossier terrain and lusher plants near water.
    /// </summary>
    public readonly float[,] Wetness;

    public readonly int Size;

    /// <summary>
    /// True once <see cref="SetSummary"/> was called (the water step does, on its worker thread): <see cref="WetCells"/>,
    /// <see cref="LowestBed"/>, <see cref="HighestSurface"/> and <see cref="HasWetness"/> then say about the whole map
    /// what would otherwise take a scan of every cell. False for a map filled by hand - scan it instead.
    /// </summary>
    public bool HasSummary { get; private set; }

    /// <summary>How many cells have water (<see cref="IsWet"/>). Valid with <see cref="HasSummary"/>.</summary>
    public int WetCells { get; private set; }

    /// <summary>The lowest terrain height under a wet cell (float.MaxValue when none is wet). Valid with <see cref="HasSummary"/>.</summary>
    public float LowestBed { get; private set; }

    /// <summary>The highest water surface of a wet cell (float.MinValue when none is wet). Valid with <see cref="HasSummary"/>.</summary>
    public float HighestSurface { get; private set; }

    /// <summary>
    /// True when some cell's wetness is enough to show (its <see cref="WetnessTexel"/> isn't 0); false means a
    /// wetness texture of this map would be all zeros. Valid with <see cref="HasSummary"/>.
    /// </summary>
    public bool HasWetness { get; private set; }

    public WaterMapData(int size)
    {
        Size = size;
        Surface = new float[size, size];
        ShoreLevel = new float[size, size];
        Type = new WaterBodyType[size, size];
        FlowX = new float[size, size];
        FlowY = new float[size, size];
        Wetness = new float[size, size];
    }

    public bool IsWet(int x, int y) => Type[x, y] != WaterBodyType.None;

    /// <summary>Records what a scan of the finished map gives (see <see cref="HasSummary"/>).</summary>
    public void SetSummary(int wetCells, float lowestBed, float highestSurface, bool hasWetness)
    {
        WetCells = wetCells;
        LowestBed = lowestBed;
        HighestSurface = highestSurface;
        HasWetness = hasWetness;
        HasSummary = true;
    }

    /// <summary>A wetness value as the byte the chunk's wetness texture stores for it.</summary>
    public static byte WetnessTexel(float wetness)
    {
        return (byte)Mathf.RoundToInt(Mathf.Clamp01(wetness) * 255f);
    }

    /// <summary>
    /// The whole map's wetness as the bytes of the chunk's wetness texture (one per cell, row by row), or null
    /// when they would all be 0. For a worker thread, so the main thread only has to upload them.
    /// </summary>
    public byte[] WetnessTexels()
    {
        if (HasSummary && !HasWetness)
            return null;
        int size = Size;
        var texels = new byte[size * size];
        bool any = false;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                byte texel = WetnessTexel(Wetness[x, y]);
                texels[y * size + x] = texel;
                any |= texel != 0;
            }
        }
        return any ? texels : null;
    }

    /// <summary>A map with no water at all yet: every cell dry, with no water level near it (NaN) and no wetness.</summary>
    public static WaterMapData CreateDry(int size)
    {
        var map = new WaterMapData(size);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                map.Surface[x, y] = float.NaN;
                map.ShoreLevel[x, y] = float.NaN;
            }
        }
        return map;
    }

    // One all-dry map per size, shared by every chunk that has no water and none near it.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, WaterMapData> DryMaps =
        new System.Collections.Concurrent.ConcurrentDictionary<int, WaterMapData>();

    /// <summary>
    /// The map of a chunk with no water in or near it: the same values a map built for it holds (every cell dry,
    /// no level, no flow, no wetness), as one instance shared by all such chunks instead of a megabyte of arrays
    /// each. It is only ever read - don't write to a map you were given.
    /// </summary>
    public static WaterMapData Dry(int size)
    {
        return DryMaps.GetOrAdd(size, s =>
        {
            WaterMapData map = CreateDry(s);
            map.SetSummary(0, float.MaxValue, float.MinValue, false);
            return map;
        });
    }
}
