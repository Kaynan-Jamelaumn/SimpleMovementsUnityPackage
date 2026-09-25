using System;
using System.Collections.Concurrent;
using UnityEngine;

/// <summary>
/// One volcano: a stratovolcano (tall concave cone with a summit crater) or a caldera (a broad volcanic
/// massif whose summit collapsed into a wide, flat-floored depression ringed by steep, stepped walls,
/// sometimes with a young cone rising from the floor). Both carry radial gullies and lava-flow lobes on
/// their flanks and a wide apron of lava plains that buries and smooths the land around them.
/// </summary>
public sealed class VolcanoFeature
{
    public Vector2 Center;
    /// <summary>Radius of the main edifice (world units); its apron reaches about 1.9x this.</summary>
    public float Radius;
    /// <summary>Summit (or caldera rim) height above the surrounding land.</summary>
    public float Height;
    public bool IsCaldera;
    /// <summary>Average height of the land the volcano stands on, measured before it was added.</summary>
    public float BaseLevel;

    // Shape variation.
    public float Harmonic1, Harmonic2, Phase1, Phase2;
    public float CraterRadius;
    public float CalderaRim;
    public float CalderaFloor;
    public bool HasInnerCone;
    public Vector2 InnerConeOffset;
    public int Salt;

    public const float ApronReach = 1.9f;

    public float Influence => Radius * ApronReach * 1.15f;

    /// <summary>Normalized distance from the center (1 = edge of the main edifice), with an irregular outline.</summary>
    public float Rho(float x, float y, out float cos, out float sin)
    {
        float dx = x - Center.x;
        float dy = y - Center.y;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        if (r < 1e-4f)
        {
            cos = 1f;
            sin = 0f;
            return 0f;
        }
        cos = dx / r;
        sin = dy / r;
        float angle = Mathf.Atan2(dy, dx);
        float wobble = 1f + Harmonic1 * Mathf.Sin(angle + Phase1) + Harmonic2 * Mathf.Sin(2f * angle + Phase2)
                       + 0.06f * WaterGenerator.Fbm(cos * 2.5f + Salt * 0.013f, sin * 2.5f, Salt, 31.7f, 2);
        return r / (Radius * wobble);
    }

    /// <summary>
    /// Height the volcano adds on top of <see cref="BaseLevel"/> at a normalized distance, before the
    /// surrounding land is blended in (see <see cref="VolcanoGenerator.Apply"/>).
    /// </summary>
    public float Elevation(float x, float y, float rho, float cos, float sin)
    {
        // Wide apron of lava plains (continues beyond the edifice, fading out at ApronReach).
        float apron = 0.1f * (1f - WaterGenerator.SmoothStep01(rho / ApronReach));
        float e;
        float upper;

        if (!IsCaldera)
        {
            // Concave cone, steepening toward the summit, with a summit crater.
            float cone = Mathf.Pow(Mathf.Max(0f, 1f - rho), 2.1f);
            e = Height * (0.9f * cone + apron);
            if (rho < CraterRadius * 1.4f)
            {
                float q = rho / CraterRadius;
                e -= Height * 0.14f * Mathf.Max(0f, 1f - q * q);
            }
            upper = 0.22f;
        }
        else
        {
            // The collapse outline is scalloped (slump scars), and the rim crest rises and dips around it.
            float rim = CalderaRim * (1f + 0.09f * WaterGenerator.Fbm(cos * 1.6f + Salt * 0.017f, sin * 1.6f, Salt, 21.3f, 2)
                                         + 0.05f * WaterGenerator.Fbm(cos * 5f, sin * 5f + Salt * 0.011f, Salt, 23.9f, 2));
            float crest = 1f - 0.2f * WaterGenerator.SmoothStep01(0.5f + 1.5f * WaterGenerator.Fbm(cos * 2.2f, sin * 2.2f + Salt * 0.019f, Salt, 27.1f, 2));

            // Broad massif up to the rim, then steep stepped walls down to a flat floor.
            float outer = rho >= rim ? Mathf.Pow(Mathf.Max(0f, 1f - (rho - rim) / (1f - rim)), 1.5f) : 1f;
            e = Height * (0.9f * outer * crest + apron);
            if (rho < rim)
            {
                float floor = Height * CalderaFloor;
                // Two ring-fault steps: a broad bench partway down the wall.
                float wall = WaterGenerator.SmoothStep01((rho - (rim - 0.07f)) / 0.035f) * 0.45f
                             + WaterGenerator.SmoothStep01((rho - (rim - 0.035f)) / 0.035f) * 0.55f;
                float basin = floor - Height * 0.05f * (1f - rho / rim);   // floor dips gently toward the middle
                e = Mathf.Lerp(basin, e, wall);

                if (HasInnerCone)
                {
                    float ix = (x - Center.x) / Radius - InnerConeOffset.x;
                    float iy = (y - Center.y) / Radius - InnerConeOffset.y;
                    float ir = Mathf.Sqrt(ix * ix + iy * iy) / 0.12f;
                    if (ir < 1f)
                        e = Mathf.Max(e, floor + Height * 0.28f * Mathf.Pow(1f - ir, 1.6f));
                }
            }
            upper = rim + 0.05f;
        }

        // Radial gullies on the flanks: lines running down the slope, starting below the summit (or outside
        // a caldera's rim), wandering and unevenly spaced, strongest mid-flank.
        float flank = WaterGenerator.SmoothStep01((rho - upper) / 0.15f) * (1f - WaterGenerator.SmoothStep01((rho - 0.7f) / 0.45f));
        if (flank > 0f)
        {
            // Angle coordinates bent a little by a slow field so the gullies curve gently and their spacing varies.
            float bend = 0.14f * WaterGenerator.Fbm(x / (Radius * 0.8f), y / (Radius * 0.8f), Salt, 43.7f, 2);
            float bc = Mathf.Cos(bend), bs = Mathf.Sin(bend);
            float ux = cos * bc - sin * bs, uy = sin * bc + cos * bs;
            float n = WaterGenerator.Fbm(ux * 4f + rho * 0.4f + Salt * 0.01f, uy * 4f, Salt, 57.1f, 3);
            float gully = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(n) * 2.2f), 2.5f);
            e -= Height * 0.09f * gully * flank;

            // Lava-flow tongues: long, narrow raised lobes running straight down the flanks (noise that
            // varies with direction around the volcano but only slowly with distance from it).
            float tongue = WaterGenerator.SmoothStep01((WaterGenerator.Fbm(ux * 7f + Salt * 0.02f, uy * 7f + rho * 0.5f, Salt, 73.3f, 2) - 0.2f) / 0.25f);
            e += Height * 0.035f * tongue * WaterGenerator.SmoothStep01((rho - upper - 0.05f) / 0.2f)
                 * (1f - WaterGenerator.SmoothStep01((rho - 1.1f) / 0.5f));
        }

        return e;
    }

    /// <summary>0-1: how much of the ground here is volcanic rock/ash (for a Volcanic biome's texture).</summary>
    public float SurfaceMask(float x, float y)
    {
        float rho = Rho(x, y, out float cos, out float sin);
        float edge = 1.05f + 0.25f * WaterGenerator.Fbm(x / 160f, y / 160f, Salt, 91.9f, 3);
        return 1f - WaterGenerator.SmoothStep01((rho - edge) / 0.3f);
    }
}
