using UnityEngine;

/// <summary>
/// Stateless, deterministic randomness for object placement. Every random choice is a hash of the world
/// seed, the object type and the world cell it concerns (plus a stream number per decision), never a shared
/// random generator - so results can't depend on thread timing, on which chunk asked first, or on how many
/// other random numbers were drawn before.
/// </summary>
public static class PlacementRandom
{
    /// <summary>A well-mixed 32-bit hash of the inputs.</summary>
    public static uint Hash(int seed, int salt, int x, int y, int stream)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h = Mix(h ^ (uint)salt * 0x85EBCA77u);
            h = Mix(h ^ (uint)x * 0xC2B2AE3Du);
            h = Mix(h ^ (uint)y * 0x27D4EB2Fu);
            h = Mix(h ^ (uint)stream * 0x165667B1u);
            return h;
        }
    }

    /// <summary>A number in [0, 1) from <see cref="Hash"/>.</summary>
    public static float Value(int seed, int salt, int x, int y, int stream)
    {
        return (Hash(seed, salt, x, y, stream) >> 8) * (1f / 16777216f);
    }

    /// <summary>A number in [min, max) from <see cref="Hash"/>.</summary>
    public static float Range(int seed, int salt, int x, int y, int stream, float min, float max)
    {
        return min + (max - min) * Value(seed, salt, x, y, stream);
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

    /// <summary>
    /// A stable hash of a string (FNV-1a). <see cref="string.GetHashCode"/> isn't used because it may differ
    /// between runs or platforms, which would move every object.
    /// </summary>
    public static int StableHash(string text)
    {
        unchecked
        {
            uint h = 2166136261u;
            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    h ^= text[i];
                    h *= 16777619u;
                }
            }
            return (int)h;
        }
    }

    /// <summary>
    /// Smooth value noise in [0, 1] (bilinear interpolation of hashed lattice values with smoothstep, summed over
    /// octaves). Pure function of position and seed, so every thread and chunk sees the same value.
    /// </summary>
    public static float Noise(float x, float y, int seed, int salt, int octaves)
    {
        float sum = 0f, amplitude = 1f, total = 0f;
        float frequency = 1f;
        for (int o = 0; o < Mathf.Max(1, octaves); o++)
        {
            sum += amplitude * ValueNoise(x * frequency, y * frequency, seed, salt + o * 7919);
            total += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.03f;
        }
        return sum / total;
    }

    private static float ValueNoise(float x, float y, int seed, int salt)
    {
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        float u = fx * fx * (3f - 2f * fx), v = fy * fy * (3f - 2f * fy);
        float a = Value(seed, salt, ix, iy, 911);
        float b = Value(seed, salt, ix + 1, iy, 911);
        float c = Value(seed, salt, ix, iy + 1, 911);
        float d = Value(seed, salt, ix + 1, iy + 1, 911);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
}
