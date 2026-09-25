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
}
