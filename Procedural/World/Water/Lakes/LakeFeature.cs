using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// One lake or pond: an irregular outline, its own water level, and the bowl it carves into the terrain.
///
/// Radial coordinates used throughout: <c>rho</c> = distance from the center divided by the outline
/// radius in that direction (1 = on the nominal outline), <c>beyond</c> = world distance past the
/// outline (negative inside). Water can only exist where <c>rho &lt; InnerFraction</c>; from there out to
/// <see cref="RimWidth"/> past the outline the terrain is guaranteed to stay above <see cref="Level"/>,
/// which is what makes the shoreline a closed loop no matter what erosion or the surrounding terrain do.
/// </summary>
public sealed class LakeFeature
{
    public const float InnerFraction = 0.9f;
    private const float MinRimClearance = 0.15f;
    private const float DamSlope = 0.58f; // ~30 degrees: outer face of a rim that had to be raised

    public WaterBodyType Type;
    public Vector2 Center;
    public float Radius;
    /// <summary>Natural water level of the site (just below the lowest point of its rim).</summary>
    public float Level;
    public float BowlDepth;
    public float RimWidth;
    public float DamFadeLength;
    public float Freeboard;
    public float[] HarmonicAmplitude;
    public float[] HarmonicPhase;
    public float BoundRadius;
    public bool HasOutlet;
    public Vector2 OutletPoint;
    public Vector2 OutletDirection;

    private Lazy<float> waterLevel;

    /// <summary>
    /// The level the water actually sits at: <see cref="Level"/>, unless a river that isn't this lake's
    /// own inlet or outlet cuts through its rim lower down - then the lake drains to that river's level
    /// instead of spilling into it (see <see cref="RiverGenerator.ComputeLakeWaterLevel"/>). Computed once
    /// from the global river data, so every chunk agrees on it.
    /// </summary>
    public float WaterLevel => waterLevel != null ? waterLevel.Value : Level;

    public void SetWaterLevelSource(Func<float> compute)
    {
        waterLevel = new Lazy<float>(compute, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public float ShapeRadius(float angle)
    {
        float factor = 1f;
        for (int i = 0; i < HarmonicAmplitude.Length; i++)
            factor += HarmonicAmplitude[i] * Mathf.Cos((i + 2) * angle + HarmonicPhase[i]);
        return Radius * factor;
    }

    public bool TryGetLocal(float x, float y, out float rho, out float beyond)
    {
        rho = float.MaxValue;
        beyond = float.MaxValue;

        float dx = x - Center.x;
        float dy = y - Center.y;
        if (dx > BoundRadius || dx < -BoundRadius || dy > BoundRadius || dy < -BoundRadius)
            return false;

        float r = Mathf.Sqrt(dx * dx + dy * dy);
        if (r > BoundRadius)
            return false;

        float shape = r > 0.0001f ? ShapeRadius(Mathf.Atan2(dy, dx)) : Radius;
        rho = r / shape;
        beyond = r - shape;
        return true;
    }

    /// <summary>How much the lake's bowl lowers the terrain at <paramref name="rho"/> (smoothly 0 at the outline).</summary>
    public float BowlCarve(float rho)
    {
        if (rho >= 1f)
            return 0f;

        float q = 1f - rho * rho;
        return BowlDepth * q * q;
    }

    /// <summary>
    /// Raises the terrain wherever it would otherwise dip to/below the water level between the water zone
    /// and <see cref="RimWidth"/> past the outline - i.e. fills any gap in the natural rim with a sill.
    /// Past that, the raised rim falls away as a dam slope that fades back into the untouched terrain.
    /// Terrain already higher than required is never touched.
    /// </summary>
    public float ApplyRim(float rho, float beyond, float height)
    {
        if (rho < InnerFraction)
            return height;

        float crest = WaterLevel + MinRimClearance + Freeboard * WaterGenerator.SmoothStep01((rho - InnerFraction) / (1f - InnerFraction));
        float required;
        float weight;

        if (beyond <= RimWidth)
        {
            required = crest;
            weight = 1f;
        }
        else
        {
            float d = beyond - RimWidth;
            required = crest - d * DamSlope;
            weight = 1f - WaterGenerator.SmoothStep01(d / DamFadeLength);
        }

        if (weight <= 0f || height >= required)
            return height;

        return height + (required - height) * weight;
    }

    public bool IsInWaterZone(float rho) => rho < InnerFraction;

    public bool IsInShoreZone(float beyond) => beyond <= RimWidth + DamFadeLength;

    public bool Intersects(Vector2 min, Vector2 max)
    {
        return Center.x + BoundRadius >= min.x && Center.x - BoundRadius <= max.x
            && Center.y + BoundRadius >= min.y && Center.y - BoundRadius <= max.y;
    }
}
