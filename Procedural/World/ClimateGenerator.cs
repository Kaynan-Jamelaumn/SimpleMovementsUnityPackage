using UnityEngine;

/// <summary>
/// Generates deterministic temperature and moisture fields used for natural, climate-driven biome
/// placement (so deserts land somewhere hot/dry, tundras somewhere cold, rainforests somewhere hot/wet, etc.)
/// and for modulating erosion strength - wetter regions get more water erosion, mimicking real climates.
/// </summary>
public static class ClimateGenerator
{
    // Large, distinct offsets so temperature/moisture noise never samples the same Perlin lattice
    // cell as height noise or each other, avoiding correlated artifacts between the fields.
    private const float TemperatureSeedOffset = 10007f;
    private const float MoistureSeedOffset = 30011f;

    /// <summary>
    /// Returns temperature in [0,1] (0 = coldest, 1 = hottest) at a world position. Combines
    /// multi-octave noise with a latitude-style gradient (colder away from an "equator" line)
    /// so climate bands read naturally instead of being pure noise.
    /// latitudeInfluence defaults low (0.18) rather than 0.5: at 0.5 the latitude term dominates,
    /// and since it barely changes within the first several thousand units of the equator line
    /// (y = equatorY, i.e. world Y = 0 by default - right around a typical spawn point), that
    /// pinned it near maximum heat for dozens of chunks around spawn, starving out every biome
    /// except the one or two with a matching idealTemperature. At a low influence, local noise
    /// drives most of the variation, with the latitude term only contributing a gentle regional
    /// trend on top - still directional, but no longer locking biome variety out near spawn.
    /// </summary>
    public static float GetTemperature(Vector2 worldPosition, int seed, float noiseScale, float latitudeInfluence = 0.18f, float equatorY = 0f)
    {
        float noise = FractalNoise(worldPosition, seed, TemperatureSeedOffset, noiseScale, octaves: 3, persistence: 0.5f, lacunarity: 2f);

        float latitudeSpan = Mathf.Max(1f, noiseScale * 4f);
        float latitudeFactor = 1f - Mathf.Clamp01(Mathf.Abs(worldPosition.y - equatorY) / latitudeSpan);

        float temperature = Mathf.Lerp(noise, latitudeFactor, Mathf.Clamp01(latitudeInfluence));
        return Mathf.Clamp01(temperature);
    }

    /// <summary>
    /// Returns moisture/rainfall in [0,1] (0 = driest, 1 = wettest) at a world position.
    /// </summary>
    public static float GetMoisture(Vector2 worldPosition, int seed, float noiseScale)
    {
        return Mathf.Clamp01(FractalNoise(worldPosition, seed, MoistureSeedOffset, noiseScale, octaves: 4, persistence: 0.5f, lacunarity: 2f));
    }

    /// <summary>
    /// Normalized (average-weighted) fractal Perlin noise, always in [0,1].
    /// </summary>
    private static float FractalNoise(Vector2 worldPosition, int seed, float seedOffset, float noiseScale, int octaves, float persistence, float lacunarity)
    {
        noiseScale = Mathf.Max(0.0001f, noiseScale);
        float seedShift = (seed * 0.0001f) + seedOffset;

        float amplitude = 1f;
        float frequency = 1f;
        float sum = 0f;
        float amplitudeSum = 0f;

        for (int o = 0; o < octaves; o++)
        {
            float sampleX = (worldPosition.x / noiseScale) * frequency + seedShift;
            float sampleY = (worldPosition.y / noiseScale) * frequency + seedShift;

            sum += Mathf.PerlinNoise(sampleX, sampleY) * amplitude;
            amplitudeSum += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return amplitudeSum > 0f ? sum / amplitudeSum : 0f;
    }
}