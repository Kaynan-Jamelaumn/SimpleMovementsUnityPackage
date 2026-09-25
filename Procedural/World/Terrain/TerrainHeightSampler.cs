using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Samples the deterministic base terrain (biome-blended land height, then ocean/coast shaping and
/// volcanoes) at any world position. <see cref="HeightGenerator"/> builds each chunk from exactly these
/// functions, and the water feature generators (<see cref="LakeGenerator"/>, <see cref="RiverGenerator"/>)
/// use the same ones to pick lake sites and trace rivers - so a feature is always placed against the
/// same terrain it ends up carved into, no matter which chunk asks first.
///
/// Biomes come in placement roles (<see cref="BiomePlacement"/>): land biomes form the normal Voronoi
/// layout; ocean biomes form their own, separate layout under the sea (which shapes the seafloor and, via
/// <see cref="GetTextureBlend"/>, takes over texturing at the coastline); a volcanic biome is painted over
/// volcanoes.
/// </summary>
public sealed class TerrainHeightSampler
{
    // The ocean biome layout is the same Voronoi layout function, looked up this far east, so it gets its
    // own independent cells (and cache entries). It is looked up at a reduced scale so ocean provinces
    // are a few times larger than land biome cells, as seafloor regions are.
    private const float OceanLayerOffset = 262144f;
    private const float OceanLayerScale = 2.5f;

    private readonly TerrainGenerator terrainGenerator;
    private readonly List<Biome> availableBiomes;
    private readonly List<Biome> oceanBiomes;
    private readonly Biome volcanicBiome;
    private readonly float inverseChunkSize;
    private readonly float boundaryMaxSlopeTangent;
    private readonly WaterSettings water;
    private readonly LandformSettings landforms;
    private readonly VoronoiBiomeGenerator.LayoutOptions layout;
    private readonly VoronoiBiomeGenerator.LayoutOptions oceanLayout;

    public TerrainHeightSampler(TerrainGenerator terrainGenerator, WaterSettings water)
    {
        this.terrainGenerator = terrainGenerator;
        this.water = water;
        List<Biome> all = terrainGenerator.BiomeDefinitions.Select(biomeInstance => biomeInstance.BiomePrefab).ToList();
        availableBiomes = all.Where(b => b == null || b.placement == BiomePlacement.Land).ToList();
        if (availableBiomes.Count == 0)
            availableBiomes = all;

        List<Biome> ocean = all.Where(b => b != null && b.placement == BiomePlacement.Ocean).ToList();
        oceanBiomes = ocean.Count > 0 && water != null && water.OceansEnabled ? ocean : null;
        volcanicBiome = all.FirstOrDefault(b => b != null && b.placement == BiomePlacement.Volcanic);

        inverseChunkSize = 1f / terrainGenerator.ChunkSize;
        boundaryMaxSlopeTangent = terrainGenerator.BiomeBoundaryMaxSlopeTangent;
        landforms = LandformSettings.From(terrainGenerator);
        layout = terrainGenerator.BiomeLayout;
        // The ocean layout is looked up at shifted positions, where the terrain climate would read the wrong place.
        oceanLayout = layout != null ? layout.WithoutClimate() : null;
    }

    /// <summary>True when ocean or volcanic biomes change texturing (see <see cref="GetTextureBlend"/>).</summary>
    public bool HasSpecialBiomes => oceanBiomes != null || (volcanicBiome != null && landforms.VolcanoesEnabled);

    /// <summary>The land biome blend (the normal Voronoi layout) at a world position.</summary>
    public List<VoronoiBiomeGenerator.BiomeWeight> GetBlend(float x, float y)
    {
        return Layout(availableBiomes, new Vector2(x, y));
    }

    private List<VoronoiBiomeGenerator.BiomeWeight> GetOceanBlend(float x, float y)
    {
        return Layout(oceanBiomes, new Vector2(x / OceanLayerScale + OceanLayerOffset, y / OceanLayerScale), oceanLayout);
    }

    private List<VoronoiBiomeGenerator.BiomeWeight> Layout(List<Biome> biomes, Vector2 position)
    {
        return Layout(biomes, position, layout);
    }

    private List<VoronoiBiomeGenerator.BiomeWeight> Layout(List<Biome> biomes, Vector2 position, VoronoiBiomeGenerator.LayoutOptions options)
    {
        TerrainGenerator tg = terrainGenerator;
        return VoronoiBiomeGenerator.GetBiomeBlend(
            position,
            tg.VoronoiScale,
            tg.NumVoronoiPoints,
            biomes,
            tg.VoronoiSeed,
            tg.useWeightedBiome,
            tg.UseNaturalClimatePlacement,
            tg.ClimateNoiseScale,
            tg.VoronoiWarpStrength,
            tg.VoronoiWarpScale,
            tg.BiomeBlendRange,
            tg.BiomeClusterStrength,
            tg.BiomeClusterRadius,
            tg.BiomeRepeatPenalty,
            tg.Octaves,
            boundaryMaxSlopeTangent,
            options
        );
    }

    public float LandHeight(List<VoronoiBiomeGenerator.BiomeWeight> blend, float x, float y, out float relief)
    {
        return LandformGenerator.LandHeight(blend, x, y, landforms, out relief);
    }

    /// <summary>
    /// Everything applied on top of the biome-blended land height: ocean/coast shaping (cliffs, sea stacks,
    /// an ocean biome's seafloor) and volcanoes. <paramref name="landSide"/> is the continent field
    /// (<see cref="OceanGenerator.LandSide"/>), float.MaxValue when oceans are off.
    /// </summary>
    public float ShapeLand(float x, float y, float land, float relief, out float landSide)
    {
        float height = ShapeCoast(x, y, land, relief, out landSide);
        return VolcanoGenerator.Apply(landforms, this, x, y, height);
    }

    private float ShapeCoast(float x, float y, float land, float relief, out float landSide)
    {
        landSide = float.MaxValue;
        if (water == null || !water.OceansEnabled)
            return land;

        landSide = OceanGenerator.LandSide(water, x, y);
        float seafloor = float.NaN;
        if (landSide < 0f && oceanBiomes != null)
            seafloor = LandformGenerator.LandHeight(GetOceanBlend(x, y), x, y, landforms, out _);
        return OceanGenerator.ShapeHeight(water, x, y, land, relief, landSide, seafloor);
    }

    /// <summary>The continent field (<see cref="OceanGenerator.LandSide"/>) exactly as <see cref="ShapeLand"/> reports it.</summary>
    public float LandSideAt(float x, float y)
    {
        if (water == null || !water.OceansEnabled)
            return float.MaxValue;
        return OceanGenerator.LandSide(water, x, y);
    }

    /// <summary>Terrain height before erosion and before any lake/river carving.</summary>
    public float SampleBaseHeight(float x, float y)
    {
        float land = LandHeight(GetBlend(x, y), x, y, out float relief);
        return ShapeLand(x, y, land, relief, out _);
    }

    /// <summary>Base terrain without volcanoes - what a volcano is built on top of.</summary>
    public float SamplePreVolcanoHeight(float x, float y)
    {
        float land = LandHeight(GetBlend(x, y), x, y, out float relief);
        return ShapeCoast(x, y, land, relief, out _);
    }

    /// <summary>
    /// Biome blend for texturing and objects: the land layout, handing over to the ocean biome layout
    /// along the coastline (within about 10 units of the waterline) when there are ocean biomes, and to
    /// the volcanic biome over volcanoes. Sorted by weight, largest first; identical to
    /// <see cref="GetBlend"/> when there are no ocean/volcanic biomes.
    /// </summary>
    public List<VoronoiBiomeGenerator.BiomeWeight> GetTextureBlend(float x, float y)
    {
        List<VoronoiBiomeGenerator.BiomeWeight> blend = GetBlend(x, y);
        if (!HasSpecialBiomes)
            return blend;

        var weights = new List<KeyValuePair<Biome, float>>(blend.Count + 2);
        float landFactor = 1f;
        if (oceanBiomes != null)
        {
            float side = OceanGenerator.LandSide(water, x, y);
            landFactor = WaterGenerator.SmoothStep01((side / water.ContinentGradient + 10f) / 20f);
        }
        for (int i = 0; i < blend.Count; i++)
            AddWeight(weights, blend[i].Biome, blend[i].Weight * landFactor);
        if (landFactor < 1f)
        {
            List<VoronoiBiomeGenerator.BiomeWeight> ocean = GetOceanBlend(x, y);
            for (int i = 0; i < ocean.Count; i++)
                AddWeight(weights, ocean[i].Biome, ocean[i].Weight * (1f - landFactor));
        }

        if (volcanicBiome != null)
        {
            float volcanic = VolcanoGenerator.SurfaceMask(landforms, this, x, y);
            if (volcanic > 0f)
            {
                for (int i = 0; i < weights.Count; i++)
                    weights[i] = new KeyValuePair<Biome, float>(weights[i].Key, weights[i].Value * (1f - volcanic));
                AddWeight(weights, volcanicBiome, volcanic);
            }
        }

        weights.Sort((a, b) => b.Value.CompareTo(a.Value));
        var result = new List<VoronoiBiomeGenerator.BiomeWeight>(weights.Count);
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i].Value > 0f)
                result.Add(new VoronoiBiomeGenerator.BiomeWeight(weights[i].Key, weights[i].Value));
        }
        return result;
    }

    private static void AddWeight(List<KeyValuePair<Biome, float>> weights, Biome biome, float weight)
    {
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i].Key == biome)
            {
                weights[i] = new KeyValuePair<Biome, float>(biome, weights[i].Value + weight);
                return;
            }
        }
        weights.Add(new KeyValuePair<Biome, float>(biome, weight));
    }

    /// <summary>The biome used for a cell's objects/texture (nearest land biome, or the top of <see cref="GetTextureBlend"/>).</summary>
    public Biome SampleBiome(float x, float y)
    {
        if (HasSpecialBiomes)
        {
            List<VoronoiBiomeGenerator.BiomeWeight> blend = GetTextureBlend(x, y);
            return blend.Count > 0 ? blend[0].Biome : null;
        }

        TerrainGenerator tg = terrainGenerator;
        return VoronoiBiomeGenerator.GetBiomeAtPosition(
            new Vector2(x, y),
            tg.VoronoiScale,
            tg.NumVoronoiPoints,
            availableBiomes,
            tg.VoronoiSeed,
            tg.useWeightedBiome,
            tg.UseNaturalClimatePlacement,
            tg.ClimateNoiseScale,
            tg.VoronoiWarpStrength,
            tg.VoronoiWarpScale,
            tg.BiomeClusterStrength,
            tg.BiomeClusterRadius,
            tg.BiomeRepeatPenalty,
            layout
        );
    }
}
