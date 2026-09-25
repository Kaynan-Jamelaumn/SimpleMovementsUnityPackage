using System;
using System.Collections.Concurrent;
using UnityEngine;

// OceanGenerator, part 2 of 2: coast character, sea cliffs and rocky shores (see OceanGenerator.cs).
public static partial class OceanGenerator
{
    /// <summary>
    /// 0 = beach, 1 = cliff, in between = rocky shore. Varies slowly along the coast, so beaches, rocky
    /// stretches and cliffs alternate; <see cref="WaterSettings.CliffFrequency"/> sets how much is cliff.
    /// </summary>
    public static float CoastCharacter(WaterSettings s, float x, float y)
    {
        if (s.CliffFrequency <= 0f)
            return 0f;
        float n = 0.5f + 1.7f * WaterGenerator.Fbm(x / 520f, y / 520f, s.Seed, CoastCharacterSalt, 3);
        float threshold = 1f - s.CliffFrequency;
        return WaterGenerator.SmoothStep01((n - threshold + 0.2f) / 0.4f);
    }

    /// <summary>Height of the cliff tops along this stretch of coast (varies along the coast).</summary>
    public static float CliffTop(WaterSettings s, float x, float y)
    {
        return s.CliffHeight * (0.55f + 0.45f * (0.5f + 0.7f * WaterGenerator.Fbm(x / 260f, y / 260f, s.Seed, CliffSalt, 2)));
    }

    /// <summary>
    /// Land height of a cliff coast at <paramref name="distance"/> world units inland. Along the coast the
    /// cliff changes height, swings in and out (headlands and coves), is cut by notches and ravines that
    /// drop toward the sea (natural ways up and down), and varies from sheer faces to steep slopes. Across
    /// it: a rocky foot at the waterline, a scree apron, then one to three rock faces separated by uneven
    /// ledges, with rough rock on the faces.
    /// </summary>
    private static float CliffProfile(WaterSettings s, float x, float y, float distance)
    {
        float top = CliffTop(s, x, y) * (0.75f + 0.5f * Noise01(s, x / 70f, y / 70f, CliffSalt + 5f));

        // Notches/ravines: narrow bands across the coast where the cliff drops most of the way down.
        float notch = Mathf.Clamp01(1f - Mathf.Abs(WaterGenerator.Fbm(x / 45f, y / 45f, s.Seed, CliffSalt + 71f, 2)) * 7f);
        top *= 1f - 0.75f * notch * notch;

        int tiers = 1 + Mathf.Clamp(Mathf.FloorToInt(s.CliffTerraces * 2.99f * Noise01(s, x / 150f, y / 150f, CliffSalt + 11f)), 0, 2);
        float edge = 2f + 16f * Noise01(s, x / 90f, y / 90f, CliffSalt + 23f);
        float inland = distance - edge;
        float face = 2.5f + 6f * Noise01(s, x / 50f, y / 50f, CliffSalt + 29f);
        float ledge = 6f + 12f * Noise01(s, x / 60f, y / 60f, CliffSalt + 37f);

        float rise = 0f;
        for (int t = 0; t < tiers; t++)
            rise += (top / tiers) * WaterGenerator.SmoothStep01((inland - t * (face + ledge)) / face);

        // Scree apron at the foot of the lowest face.
        rise += 0.12f * top * WaterGenerator.SmoothStep01((inland + 8f) / 8f) * (1f - WaterGenerator.SmoothStep01(inland / face));

        // Rough rock on and just above the faces.
        float faces = WaterGenerator.SmoothStep01((inland + 4f) / (face + 4f)) * (1f - WaterGenerator.SmoothStep01((inland - tiers * (face + ledge)) / 12f));
        float rough = 0.1f * top * WaterGenerator.Fbm(x / 7f, y / 7f, s.Seed, CliffSalt + 51f, 2) * faces;

        float foot = 0.8f * WaterGenerator.SmoothStep01(distance / 2f);
        return s.SeaLevel + foot + rise + rough;
    }

    /// <summary>Noise mapped to roughly 0-1 (clamped).</summary>
    private static float Noise01(WaterSettings s, float x, float y, float salt)
    {
        return Mathf.Clamp01(0.5f + 1.2f * WaterGenerator.Fbm(x, y, s.Seed, salt, 2));
    }
}
