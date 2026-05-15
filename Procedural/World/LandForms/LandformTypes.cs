using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The shape of the ground a biome sits on. A biome describes an environment (textures, climate,
/// objects, water); its landform decides what the terrain there actually looks like.
/// </summary>
public enum LandformType
{
    /// <summary>The original terrain: layered smooth noise from the biome's amplitude/frequency/persistence.</summary>
    Classic = 0,
    /// <summary>Broad, low undulation with occasional shallow basins.</summary>
    Plains = 1,
    /// <summary>Rounded, rolling hills with gentle slopes and broad lows between them.</summary>
    Hills = 2,
    /// <summary>Mountain ranges: connected peaks and ridges of varied height and shape, separated by valleys.</summary>
    Mountains = 3,
    /// <summary>Wind-aligned sand dunes (gentle windward side, steep lee side) in fields separated by flat pans.</summary>
    Dunes = 4,
    /// <summary>Flat, low ground with hummocks and shallow hollows.</summary>
    Wetland = 5,
    /// <summary>Flat-topped tablelands with stepped cliffs, cut by narrow canyons.</summary>
    Plateau = 6,
    /// <summary>Rugged uplands: big rolling relief broken by rock ledges and ravines you have to walk around (e.g. forests).</summary>
    Highlands = 7,
    /// <summary>High mountain terrain carved by glaciers: broad, flat-floored U-shaped valleys with steep walls and hanging side valleys.</summary>
    Glacial = 8,
    /// <summary>Ocean biomes: deep, gently rolling seafloor with scattered seamounts.</summary>
    SeaPlain = 9,
    /// <summary>Ocean biomes: seafloor cut by deep submarine ravines and canyons.</summary>
    SeaRavines = 10,
    /// <summary>Ocean biomes: shallow reef banks and atoll rings rising to just below the surface, with lagoons.</summary>
    SeaReef = 11,
    /// <summary>Ocean biomes: rough rocky seabed with ledges and boulder fields.</summary>
    SeaRocky = 12,
}

/// <summary>Where a biome may be placed (see <see cref="Biome.placement"/>).</summary>
public enum BiomePlacement
{
    /// <summary>A normal land biome, placed by the Voronoi biome layout.</summary>
    Land = 0,
    /// <summary>Only used on the ocean floor (textures, objects, and a seafloor landform). Needs Oceans enabled.</summary>
    Ocean = 1,
    /// <summary>Only used on volcanoes: painted over the volcano's cone, caldera and lava fields.</summary>
    Volcanic = 2,
}

/// <summary>How the terrain generator decides each biome's landform (see <see cref="TerrainGenerator"/>).</summary>
public enum TerrainShapeMode
{
    /// <summary>Every biome uses the original Classic terrain; landform settings are ignored.</summary>
    ClassicOnly = 0,
    /// <summary>Each biome uses its own Landform setting (Classic keeps the original terrain for that biome).</summary>
    PerBiome = 1,
    /// <summary>Every biome uses a landform; biomes still set to Classic get a suggested one.</summary>
    LandformsOnly = 2,
}
