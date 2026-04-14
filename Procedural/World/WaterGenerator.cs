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

    public readonly int Size;

    public WaterMapData(int size)
    {
        Size = size;
        Surface = new float[size, size];
        ShoreLevel = new float[size, size];
        Type = new WaterBodyType[size, size];
    }

    public bool IsWet(int x, int y) => Type[x, y] != WaterBodyType.None;
}

/// <summary>
/// Immutable snapshot of every water-related <see cref="TerrainGenerator"/> setting, taken once per chunk
/// so the (background-thread) generators below don't read dozens of live properties per cell.
/// </summary>
public sealed class WaterSettings
{
    public int Seed;
    public float SeaLevel;
    public int LodCells;

    public bool OceansEnabled;
    public float ContinentScale;
    public float ContinentGradient;
    public float OceanThreshold;
    public float BeachWidth;
    public float CoastBlendWidth;
    public float BeachHeight;
    public float ShelfWidth;
    public float OceanDepth;
    public float InlandRise;
    public float InlandRiseDistance;
    public float IslandThreshold;
    public float IslandScale;
    public float IslandPeakHeight;
    public float SpawnLandRadius;

    public float CliffFrequency;
    public float CliffHeight;
    public float CliffTerraces;
    public float StackChance;
    public float StackSpacing;
    public float StackMaxHeight;

    public bool LakesEnabled;
    public float LakeSpacing;
    public float LakeChance;
    public float LakeMinRadius;
    public float LakeMaxRadius;
    public float LakeMaxDepth;
    public float LakeMaxSiteSlope;
    public float LakeOutletChance;

    public bool PondsEnabled;
    public float PondSpacing;
    public float PondChance;
    public float PondMinRadius;
    public float PondMaxRadius;
    public float PondDepth;
    public float PondMaxSiteSlope;

    public float ShoreRimWidth;
    public float ShoreFreeboard;

    public bool RiversEnabled;
    public float RiverSpacing;
    public float RiverChance;
    public float RiverMinSpringElevation;
    public float RiverMinLength;
    public float RiverMaxLength;
    public float RiverSourceWidth;
    public float RiverMouthWidth;
    public float RiverWidthVariation;
    public float RiverMeander;
    public float RiverMeanderWavelength;
    public float RiverDepth;
    public float RiverValleySlopeTan;
    public float RiverMaxValleyHalfWidth;
    public float RiverBankFreeboard;

    public bool WaterfallsEnabled;
    public float WaterfallMinDrop;
    public float WaterfallTierHeight;

    public static WaterSettings From(TerrainGenerator tg)
    {
        int lod = tg.LevelOfDetail;
        float continentScale = Mathf.Max(100f, tg.ContinentScale);

        return new WaterSettings
        {
            Seed = tg.VoronoiSeed,
            SeaLevel = tg.SeaLevel,
            LodCells = lod > 0 ? lod * 2 : 1,

            OceansEnabled = tg.EnableOceans,
            ContinentScale = continentScale,
            // Typical magnitude of the continent field's gradient per world unit, used to express the
            // coastline bands below in world units even though they're applied to a noise value.
            ContinentGradient = 1.5f / continentScale,
            OceanThreshold = tg.OceanThreshold,
            BeachWidth = Mathf.Max(1f, tg.BeachWidth),
            CoastBlendWidth = Mathf.Max(tg.BeachWidth + 1f, tg.CoastBlendWidth),
            BeachHeight = Mathf.Max(0f, tg.BeachHeight),
            ShelfWidth = Mathf.Max(1f, tg.ContinentalShelfWidth),
            OceanDepth = Mathf.Max(1f, tg.OceanDepth),
            InlandRise = tg.InlandRise,
            InlandRiseDistance = Mathf.Max(1f, tg.InlandRiseDistance),
            IslandThreshold = tg.IslandFrequency > 0f ? Mathf.Lerp(0.55f, 0.05f, Mathf.Clamp01(tg.IslandFrequency)) : float.PositiveInfinity,
            IslandScale = Mathf.Max(10f, tg.IslandScale),
            IslandPeakHeight = Mathf.Max(0f, tg.IslandPeakHeight),
            SpawnLandRadius = Mathf.Max(0f, tg.SpawnLandRadius),

            CliffFrequency = Mathf.Clamp01(tg.CoastCliffFrequency),
            CliffHeight = Mathf.Max(0f, tg.CoastCliffHeight),
            CliffTerraces = Mathf.Clamp01(tg.CoastCliffTerraces),
            StackChance = Mathf.Clamp01(tg.SeaStackChance),
            StackSpacing = Mathf.Max(60f, tg.SeaStackSpacing),
            StackMaxHeight = Mathf.Max(4f, tg.SeaStackMaxHeight),

            LakesEnabled = tg.EnableLakes,
            LakeSpacing = Mathf.Max(10f, tg.LakeSpacing),
            LakeChance = Mathf.Clamp01(tg.LakeChance),
            LakeMinRadius = Mathf.Max(2f, tg.LakeMinRadius),
            LakeMaxRadius = Mathf.Max(Mathf.Max(2f, tg.LakeMinRadius), tg.LakeMaxRadius),
            LakeMaxDepth = Mathf.Max(0.5f, tg.LakeMaxDepth),
            LakeMaxSiteSlope = Mathf.Max(0.01f, tg.LakeMaxSiteSlope),
            LakeOutletChance = Mathf.Clamp01(tg.LakeOutletChance),

            PondsEnabled = tg.EnablePonds,
            PondSpacing = Mathf.Max(10f, tg.PondSpacing),
            PondChance = Mathf.Clamp01(tg.PondChance),
            PondMinRadius = Mathf.Max(1f, tg.PondMinRadius),
            PondMaxRadius = Mathf.Max(Mathf.Max(1f, tg.PondMinRadius), tg.PondMaxRadius),
            PondDepth = Mathf.Max(0.2f, tg.PondDepth),
            PondMaxSiteSlope = Mathf.Max(0.01f, tg.PondMaxSiteSlope),

            ShoreRimWidth = Mathf.Max(1f, tg.ShoreRimWidth),
            ShoreFreeboard = Mathf.Max(0.05f, tg.ShoreFreeboard),

            RiversEnabled = tg.EnableRivers,
            RiverSpacing = Mathf.Max(50f, tg.RiverSpacing),
            RiverChance = Mathf.Clamp01(tg.RiverChance),
            RiverMinSpringElevation = Mathf.Max(0f, tg.RiverMinSpringElevation),
            RiverMinLength = Mathf.Max(0f, tg.RiverMinLength),
            RiverMaxLength = Mathf.Max(50f, tg.RiverMaxLength),
            RiverSourceWidth = Mathf.Max(0.5f, tg.RiverSourceWidth),
            RiverMouthWidth = Mathf.Max(0.5f, tg.RiverMouthWidth),
            RiverWidthVariation = Mathf.Clamp(tg.RiverWidthVariation, 0f, 0.9f),
            RiverMeander = Mathf.Clamp(tg.RiverMeander, 0f, 1f),
            RiverMeanderWavelength = Mathf.Max(10f, tg.RiverMeanderWavelength),
            RiverDepth = Mathf.Max(0.3f, tg.RiverDepth),
            RiverValleySlopeTan = Mathf.Tan(Mathf.Clamp(tg.RiverValleySlope, 5f, 80f) * Mathf.Deg2Rad),
            RiverMaxValleyHalfWidth = Mathf.Max(5f, tg.RiverMaxValleyWidth),
            RiverBankFreeboard = Mathf.Max(0.1f, tg.RiverBankFreeboard),

            WaterfallsEnabled = tg.EnableWaterfalls,
            WaterfallMinDrop = Mathf.Max(1f, tg.WaterfallMinDrop),
            WaterfallTierHeight = Mathf.Max(2f, tg.WaterfallTierHeight),
        };
    }
}

/// <summary>
/// Entry point for water generation, plus small helpers shared by the individual water body generators
/// (<see cref="OceanGenerator"/>, <see cref="LakeGenerator"/>, <see cref="RiverGenerator"/>).
///
/// Water bodies are geographic features, not height ranges: each type has its own placement rules,
/// its own water level and its own way of shaping the terrain, and none of them is decided by biome.
/// Every feature is a pure function of world position + seed (like the Voronoi biome points), computed
/// once and cached globally, so all chunks see exactly the same oceans, lakes and river paths - which
/// is what keeps them seamless across chunk borders.
/// </summary>
public static class WaterGenerator
{
    /// <summary>
    /// Forgets every cached lake, pond and river. Must be called whenever a new world is generated
    /// (seed or settings changed), for the same reason as <see cref="VoronoiBiomeGenerator.ClearCache"/>.
    /// </summary>
    public static void ClearCaches()
    {
        LakeGenerator.ClearCache();
        RiverGenerator.ClearCache();
        SeaStacks.ClearCache();
        VolcanoGenerator.ClearCache();
    }

    /// <summary>
    /// Gathers every water feature that can influence the given (padded) chunk area and prepares the
    /// per-cell data used while its height map is generated.
    /// </summary>
    public static ChunkWaterContext CreateChunkContext(WaterSettings settings, TerrainHeightSampler sampler, Vector2 origin, int size)
    {
        Vector2 rectMin = origin;
        Vector2 rectMax = origin + new Vector2(size - 1, size - 1);

        List<LakeFeature> lakes = new List<LakeFeature>();
        LakeGenerator.GatherForRect(rectMin, rectMax, settings, sampler, lakes);

        List<RiverPath> rivers = new List<RiverPath>();
        RiverGenerator.Gather(rectMin, rectMax, settings, sampler, rivers);

        // Lakes created where a river got trapped aren't on the lake grid - they come with their river.
        for (int i = 0; i < rivers.Count; i++)
        {
            LakeFeature terminal = rivers[i].TerminalLake;
            if (terminal != null && terminal.Intersects(rectMin, rectMax) && !lakes.Contains(terminal))
                lakes.Add(terminal);
        }

        // Resolve every lake's final water level up front (it's computed once, lazily, from the global
        // river data) rather than on first touch inside the per-cell loops.
        for (int i = 0; i < lakes.Count; i++)
        {
            float unused = lakes[i].WaterLevel;
        }

        return new ChunkWaterContext(settings, origin, size, lakes, rivers);
    }

    public static float SmoothStep01(float v)
    {
        if (v <= 0f) return 0f;
        if (v >= 1f) return 1f;
        return v * v * (3f - 2f * v);
    }

    /// <summary>Normalized fractal Perlin noise, roughly in [-1, 1].</summary>
    public static float Fbm(float x, float y, int seed, float salt, int octaves)
    {
        float shift = seed * 0.0001f + salt;
        float amplitude = 1f;
        float frequency = 1f;
        float sum = 0f;
        float norm = 0f;

        for (int o = 0; o < octaves; o++)
        {
            sum += (Mathf.PerlinNoise(x * frequency + shift, y * frequency + shift * 1.31f) * 2f - 1f) * amplitude;
            norm += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.07f;
        }

        return norm > 0f ? sum / norm : 0f;
    }

    /// <summary>Well-mixed deterministic hash (Squirrel Eiserloh's bit noise) for seeding per-cell RNGs.</summary>
    public static int Hash(int x, int y, int seed, int salt)
    {
        unchecked
        {
            uint h = (uint)x * 0xB5297A4Du;
            h += (uint)seed;
            h ^= (uint)y * 0x68E31DA4u;
            h += (uint)salt * 0x1B56C4E9u;
            h ^= h >> 8;
            h += h << 13 ^ 0x68E31DA4u;
            h ^= h >> 7;
            h *= 0x1B56C4E9u;
            h ^= h << 17;
            h ^= h >> 11;
            return (int)h;
        }
    }

    public static long CellKey(Vector2Int cell)
    {
        return ((long)cell.x << 32) | (uint)cell.y;
    }

    public static Vector2Int CellOf(Vector2 position, float spacing)
    {
        return new Vector2Int(Mathf.FloorToInt(position.x / spacing), Mathf.FloorToInt(position.y / spacing));
    }
}

/// <summary>
/// Applies every water feature touching one chunk to that chunk's (padded) height map, in the stages
/// <see cref="HeightGenerator"/> calls it:
///
/// 1. <see cref="RecordLandSide"/> - where the coastline is (the shaping itself happens in <see cref="TerrainHeightSampler.ShapeLand"/>).
/// 2. <see cref="ApplyPreErosion"/> - lake/pond bowls and rims, river valleys and channels, carved
///    before erosion so thermal/hydraulic erosion weathers them into natural-looking shapes.
/// 3. <see cref="ApplyPostErosion"/> - hard guarantees re-applied after erosion (coastline above sea
///    level, lake rims above lake level, river banks above the river, river channels below it), so
///    erosion can never open a leak or fill a channel.
/// 4. <see cref="BuildWaterMap"/> - which cells are wet, with which surface height and water type.
///
/// Every step is a per-cell function of world position and the globally shared feature data, which is
/// why neighboring chunks agree along their shared border.
/// </summary>
public sealed class ChunkWaterContext
{
    // Minimum height land keeps above sea level right at the shoreline, so the water plane and the beach
    // never end up coplanar (z-fighting).
    private const float CoastMargin = 0.25f;

    private readonly WaterSettings settings;
    private readonly Vector2 origin;
    private readonly int size;
    private readonly float[] landSide;
    private readonly LakeFeature[] lakes;
    private readonly RiverRaster rivers;
    private readonly float coastClampLandSide;

    public ChunkWaterContext(WaterSettings settings, Vector2 origin, int size, List<LakeFeature> lakes, List<RiverPath> rivers)
    {
        this.settings = settings;
        this.origin = origin;
        this.size = size;
        this.lakes = lakes.ToArray();
        landSide = new float[size * size];

        if (rivers.Count > 0)
        {
            this.rivers = new RiverRaster(size * size);
            RiverGenerator.Rasterize(rivers, settings, origin, size, this.rivers);
        }

        // Land this close to the ocean (in continent-noise units) is kept above sea level after erosion.
        // Wider than the beach itself by two mesh vertices, so the water mesh's edge always lands on it.
        coastClampLandSide = (settings.BeachWidth + 2f * settings.LodCells) * settings.ContinentGradient;
    }

    /// <summary>
    /// Stores the continent field value (<see cref="OceanGenerator.LandSide"/>) the terrain sampler computed
    /// for a cell while shaping it (float.MaxValue when oceans are off), for the coastline guarantee and
    /// ocean detection below.
    /// </summary>
    public void RecordLandSide(int x, int y, float side)
    {
        landSide[y * size + x] = settings.OceansEnabled ? side : float.MaxValue;
    }

    public float ApplyPreErosion(int x, int y, float worldX, float worldY, float height)
    {
        for (int i = 0; i < lakes.Length; i++)
        {
            LakeFeature lake = lakes[i];
            if (!lake.TryGetLocal(worldX, worldY, out float rho, out float beyond))
                continue;

            height -= lake.BowlCarve(rho);
            height = lake.ApplyRim(rho, beyond, height);
        }

        if (rivers != null)
            height = Mathf.Min(height, rivers.Carve[y * size + x]);

        return height;
    }

    public void ApplyPostErosion(float[,] heights)
    {
        for (int y = 0; y < size; y++)
        {
            float worldY = origin.y + y;
            for (int x = 0; x < size; x++)
            {
                float worldX = origin.x + x;
                int index = y * size + x;
                float height = heights[x, y];
                float side = landSide[index];

                if (settings.OceansEnabled && side >= 0f && side < coastClampLandSide)
                    height = Mathf.Max(height, settings.SeaLevel + CoastMargin);

                bool insideLakeWater = false;
                for (int i = 0; i < lakes.Length; i++)
                {
                    LakeFeature lake = lakes[i];
                    if (!lake.TryGetLocal(worldX, worldY, out float rho, out float beyond))
                        continue;

                    height = lake.ApplyRim(rho, beyond, height);
                    if (lake.IsInWaterZone(rho))
                        insideLakeWater = true;
                }

                if (rivers != null)
                {
                    // Banks first, channel last: a river is allowed to cut through a lake rim or the
                    // coastline (that's an inlet/outlet/mouth), but nothing is allowed to fill its channel.
                    float bank = rivers.Bank[index];
                    if (bank > float.NegativeInfinity && side >= 0f && !insideLakeWater)
                        height = Mathf.Max(height, bank);

                    height = Mathf.Min(height, rivers.Channel[index]);
                }

                heights[x, y] = height;
            }
        }
    }

    public WaterMapData BuildWaterMap(float[,] paddedHeights, int padding, int finalSize)
    {
        WaterMapData map = new WaterMapData(finalSize);

        for (int y = 0; y < finalSize; y++)
        {
            int paddedY = y + padding;
            float worldY = origin.y + paddedY;
            for (int x = 0; x < finalSize; x++)
            {
                int paddedX = x + padding;
                float worldX = origin.x + paddedX;
                int index = paddedY * size + paddedX;
                float height = paddedHeights[paddedX, paddedY];

                WaterBodyType type = WaterBodyType.None;
                float surface = float.NaN;
                float shore = float.NaN;

                for (int i = 0; i < lakes.Length; i++)
                {
                    LakeFeature lake = lakes[i];
                    if (!lake.TryGetLocal(worldX, worldY, out float rho, out float beyond))
                        continue;

                    float level = lake.WaterLevel;
                    if (lake.IsInWaterZone(rho) && height < level)
                    {
                        type = lake.Type;
                        surface = level;
                        break;
                    }

                    if (float.IsNaN(shore) && lake.IsInShoreZone(beyond))
                        shore = level;
                }

                float side = landSide[index];

                if (type == WaterBodyType.None && settings.OceansEnabled && side < 0f && height < settings.SeaLevel)
                {
                    type = WaterBodyType.Ocean;
                    surface = settings.SeaLevel;
                }

                if (type == WaterBodyType.None && rivers != null && rivers.OwnerNorm[index] <= 1f && height < rivers.Surface[index])
                {
                    type = WaterBodyType.River;
                    surface = rivers.Surface[index];
                }

                if (type != WaterBodyType.None)
                {
                    shore = surface;
                }
                else if (float.IsNaN(shore))
                {
                    if (rivers != null && !float.IsNaN(rivers.Hint[index]))
                        shore = rivers.Hint[index];
                    else if (settings.OceansEnabled && side < coastClampLandSide * 2f)
                        shore = settings.SeaLevel;
                }

                map.Type[x, y] = type;
                map.Surface[x, y] = surface;
                map.ShoreLevel[x, y] = shore;
            }
        }

        return map;
    }
}
