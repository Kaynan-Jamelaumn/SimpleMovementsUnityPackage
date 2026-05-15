using System.Collections.Generic;
using UnityEngine;

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
        ErosionTiles.ClearCache();
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
