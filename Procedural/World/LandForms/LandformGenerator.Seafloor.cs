using System.Collections.Generic;
using UnityEngine;

// LandformGenerator, part 3 of 4: the seafloor landforms used by ocean biomes (see LandformGenerator.cs).
public static partial class LandformGenerator
{
    /// <summary>Ocean biomes: deep rolling seafloor with scattered seamounts (relief relative to the normal seafloor).</summary>
    private static float SeaPlain(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.SeaPlain;
        float inv = 1f / wavelength;
        float roll = Fbm(x * inv / 2.5f, y * inv / 2.5f, Key(seed, T, 1), 2, 0.5f) * 0.25f;
        float seamount = Mathf.Pow(SmoothStep(0.72f, 0.98f, Noise01(x * inv / 1.6f, y * inv / 1.6f, Key(seed, T, 2))), 1.5f) * 1.2f;
        return amplitude * (-0.25f + roll + seamount);
    }

    /// <summary>Ocean biomes: seafloor cut by deep, branching submarine ravines (relief relative to the normal seafloor).</summary>
    private static float SeaRavines(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.SeaRavines;
        float inv = 1f / wavelength;
        float warpX = Fbm(x * inv * 0.4f, y * inv * 0.4f, Key(seed, T, 1), 2, 0.5f);
        float warpY = Fbm(x * inv * 0.4f, y * inv * 0.4f, Key(seed, T, 2), 2, 0.5f);
        float qx = x + warpX * 0.5f * wavelength;
        float qy = y + warpY * 0.5f * wavelength;

        float main = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Noise(qx * inv / 1.8f, qy * inv / 1.8f, Key(seed, T, 3), 0))), 10f);
        float branch = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Noise(qx * inv / 0.7f, qy * inv / 0.7f, Key(seed, T, 4), 0))), 14f);
        float zone = 0.4f + 0.6f * SmoothStep(0.3f, 0.6f, Noise01(x * inv / 4f, y * inv / 4f, Key(seed, T, 5)));
        float base0 = Fbm(x * inv / 3f, y * inv / 3f, Key(seed, T, 6), 2, 0.5f) * 0.2f;
        return amplitude * (base0 - (main + 0.5f * branch) * zone);
    }

    /// <summary>Ocean biomes: reef banks and atoll rings rising toward the surface, with lagoons and coral heads.</summary>
    private static float SeaReef(float x, float y, float wavelength, float amplitude, int seed)
    {
        const int T = (int)LandformType.SeaReef;
        float inv = 1f / wavelength;
        float bank = SmoothStep(0.58f, 0.72f, Fbm01(x * inv, y * inv, Key(seed, T, 1), 3, 0.5f));
        // Lagoons: the middle of the larger banks sits lower, leaving a reef ring (an atoll) around it.
        float lagoon = SmoothStep(0.72f, 0.82f, Fbm01(x * inv, y * inv, Key(seed, T, 1), 3, 0.5f));
        float heads = SmoothStep(0.55f, 0.9f, Noise01(x / 9f, y / 9f, Key(seed, T, 3)));
        return amplitude * (bank * (1f - 0.55f * lagoon) + 0.08f * heads * bank);
    }

    /// <summary>Ocean biomes: rough rocky seabed with stepped ledges and boulder fields.</summary>
    private static float SeaRocky(float x, float y, float wavelength, float amplitude, float roughness, int seed)
    {
        const int T = (int)LandformType.SeaRocky;
        float inv = 1f / wavelength;
        float sum = 0f, norm = 0f, a = 1f, f = inv;
        for (int o = 0; o < 3; o++)
        {
            float ridge = 1f - Mathf.Abs(Noise(x * f, y * f, Key(seed, T, 1 + o), 0));
            sum += ridge * ridge * a;
            norm += a;
            a *= roughness;
            f *= 2f;
        }
        float rock = sum / norm;
        float stepped = Mathf.Lerp(rock, Mathf.Round(rock * 4f) / 4f, 0.5f);
        float boulders = SmoothStep(0.6f, 0.9f, Noise01(x / 6f, y / 6f, Key(seed, T, 5))) * 0.15f;
        return amplitude * (0.6f * stepped - 0.3f + boulders);
    }
}
