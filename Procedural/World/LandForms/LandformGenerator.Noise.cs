using System.Collections.Generic;
using UnityEngine;

// LandformGenerator, part 4 of 4: the noise, hashing and smoothing helpers every landform is built from (see LandformGenerator.cs).
public static partial class LandformGenerator
{
    // ------------------------------------------------------------------ noise helpers

    /// <summary>1 at 0 down to 0 at 1, with zero slope at both ends.</summary>
    private static float Falloff(float t)
    {
        if (t <= 0f) return 1f;
        if (t >= 1f) return 0f;
        return 1f - t * t * (3f - 2f * t);
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static int OctavesFor(float wavelength, float smallestFeature)
    {
        int octaves = 1;
        float w = wavelength;
        while (w > smallestFeature * 2f && octaves < 8)
        {
            w *= 0.5f;
            octaves++;
        }
        return Mathf.Max(3, octaves);
    }

    /// <summary>
    /// Gradient noise in about [-1,1], offset by a per-(seed, landform, layer) key so layers and landforms
    /// are independent. Landforms use their own noise (rather than Mathf.PerlinNoise) so their value range -
    /// which the shaping thresholds depend on - is known exactly and identical on every platform.
    /// </summary>
    private static float Noise(float x, float y, int key, int salt)
    {
        uint h = Mix((uint)key + (uint)salt * 0x27D4EB2Fu);
        float ox = (h & 0xFFFF) * (1f / 65536f) * 256f;
        float oy = ((h >> 16) & 0xFFFF) * (1f / 65536f) * 256f;
        return Gradient(x + ox, y + oy);
    }

    private static readonly int[] Permutation = BuildPermutation();

    private static int[] BuildPermutation()
    {
        var p = new int[512];
        var source = new int[256];
        for (int i = 0; i < 256; i++)
            source[i] = i;
        uint state = 0x2F6B3A1Du;
        for (int i = 255; i > 0; i--)
        {
            state = Mix(state + (uint)i);
            int j = (int)(state % (uint)(i + 1));
            int t = source[i]; source[i] = source[j]; source[j] = t;
        }
        for (int i = 0; i < 512; i++)
            p[i] = source[i & 255];
        return p;
    }

    /// <summary>2D Perlin gradient noise, scaled to about [-1,1] (99% within +/-0.9); repeats every 256 units.</summary>
    private static float Gradient(float x, float y)
    {
        float fx = Mathf.Floor(x), fy = Mathf.Floor(y);
        int xi = (int)fx & 255, yi = (int)fy & 255;
        x -= fx; y -= fy;
        float u = x * x * x * (x * (x * 6f - 15f) + 10f);
        float v = y * y * y * (y * (y * 6f - 15f) + 10f);
        int[] p = Permutation;
        int a = p[xi] + yi, b = p[xi + 1] + yi;
        float n00 = Dot(p[a], x, y), n10 = Dot(p[b], x - 1f, y);
        float n01 = Dot(p[a + 1], x, y - 1f), n11 = Dot(p[b + 1], x - 1f, y - 1f);
        float nx0 = n00 + u * (n10 - n00);
        float nx1 = n01 + u * (n11 - n01);
        return Mathf.Clamp((nx0 + v * (nx1 - nx0)) * 1.96f, -1f, 1f);
    }

    // Eight unit gradient directions at 22.5 + k * 45 degrees. None is parallel to a grid axis: with
    // axis-aligned gradients the noise is exactly zero along some whole cell edges, which ridged shapes
    // (1 - |noise|) turn into straight lines.
    private static readonly float[] GradientX = { 0.9239f, 0.3827f, -0.3827f, -0.9239f, -0.9239f, -0.3827f, 0.3827f, 0.9239f };
    private static readonly float[] GradientY = { 0.3827f, 0.9239f, 0.9239f, 0.3827f, -0.3827f, -0.9239f, -0.9239f, -0.3827f };

    private static float Dot(int hash, float x, float y)
    {
        int h = hash & 7;
        return GradientX[h] * x + GradientY[h] * y;
    }

    private static float Noise01(float x, float y, int key)
    {
        return Mathf.Clamp01(0.5f + 0.5f * Noise(x, y, key, 0));
    }

    /// <summary>Layered noise, normalized to about [-1,1].</summary>
    private static float Fbm(float x, float y, int key, int octaves, float persistence)
    {
        float sum = 0f, norm = 0f, amplitude = 1f, frequency = 1f;
        for (int o = 0; o < octaves; o++)
        {
            sum += Noise(x * frequency, y * frequency, key, o) * amplitude;
            norm += amplitude;
            amplitude *= persistence;
            frequency *= 2.03f;
        }
        return norm > 0f ? Mathf.Clamp(sum / norm * 1.3f, -1f, 1f) : 0f;
    }

    private static float Fbm01(float x, float y, int key, int octaves, float persistence)
    {
        return 0.5f + 0.5f * Fbm(x, y, key, octaves, persistence);
    }

    private static int Key(int seed, int landform, int layer)
    {
        unchecked
        {
            return (int)Mix((uint)seed * 0x9E3779B1u ^ (uint)landform * 0x85EBCA77u ^ (uint)layer * 0xC2B2AE3Du);
        }
    }

    private static float Hash01(int seed, int landform, int layer)
    {
        return (Mix((uint)Key(seed, landform, layer)) & 0xFFFFFF) / 16777216f;
    }

    private static uint Mix(uint h)
    {
        unchecked
        {
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }

    /// <summary>String hash that is the same on every platform and run (string.GetHashCode is not guaranteed to be).</summary>
    /// <summary>StableHash of a biome's name (memoized per chunk by <see cref="LandformSettings.StableNameHash"/>).</summary>
    public static int StableNameHash(Biome biome)
    {
        return StableHash(biome.name);
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            uint h = 2166136261u;
            if (text != null)
            {
                foreach (char c in text)
                {
                    h ^= c;
                    h *= 16777619u;
                }
            }
            return (int)h;
        }
    }
}
