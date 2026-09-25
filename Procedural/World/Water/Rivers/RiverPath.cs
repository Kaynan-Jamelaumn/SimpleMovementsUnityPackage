using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// One traced river: a polyline plus, per point, its water surface, channel bed, channel half-width and
/// valley half-width. The surface never rises downstream.
/// </summary>
public sealed class RiverPath
{
    public Vector2[] Points;
    public float[] Surface;
    public float[] Bed;
    public float[] HalfWidth;
    public float[] ValleyHalfWidth;
    public Vector2 BoundsMin;
    public Vector2 BoundsMax;
    public WaterBodyType MouthType;
    public LakeFeature TerminalLake;
    /// <summary>The lake this river is the outlet of, if any.</summary>
    public LakeFeature SourceLake;
    public float Length;
    /// <summary>Waterfalls along this river (empty when none) - lip position and the water level above and below.</summary>
    public RiverFall[] Falls = new RiverFall[0];
    /// <summary>Stable per-river number (from its source cell) that breaks ties when two rivers meet.</summary>
    public int Rank;
    /// <summary>The river this one flows into, if it ends at a junction (see <see cref="RiverGenerator"/> junctions).</summary>
    public RiverPath JoinsRiver;

    // This river after junctions are applied (computed once, on first use).
    internal System.Lazy<RiverPath> Resolved;

    public bool Intersects(Vector2 min, Vector2 max)
    {
        return BoundsMax.x >= min.x && BoundsMin.x <= max.x && BoundsMax.y >= min.y && BoundsMin.y <= max.y;
    }
}

/// <summary>One waterfall drop on a river.</summary>
public struct RiverFall
{
    public Vector2 Position;
    public float Top;
    public float Bottom;
}
