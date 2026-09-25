using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Per-cell river data for one chunk (flattened, index = y * size + x), filled by <see cref="RiverGenerator.Rasterize"/>.
/// </summary>
public sealed class RiverRaster
{
    /// <summary>Pre-erosion valley/channel carve target (terrain is lowered to at most this).</summary>
    public readonly float[] Carve;
    /// <summary>Post-erosion channel profile (terrain is kept at or below this inside the channel).</summary>
    public readonly float[] Channel;
    /// <summary>Post-erosion bank height (terrain right beside the channel is kept at or above this).</summary>
    public readonly float[] Bank;
    /// <summary>Water surface of the river owning this cell's channel.</summary>
    public readonly float[] Surface;
    /// <summary>Distance to the owning river's centerline divided by its half-width (&lt;= 1 = inside the channel).</summary>
    public readonly float[] OwnerNorm;
    /// <summary>Flow (direction x speed) of the river owning this cell's channel.</summary>
    public readonly float[] FlowX;
    public readonly float[] FlowY;
    /// <summary>Water surface of the nearest river within its valley, for shoreline extension.</summary>
    public readonly float[] Hint;
    public readonly float[] HintDistance;
    /// <summary>Distance from that nearest river's channel edge (negative inside the channel).</summary>
    public readonly float[] HintEdgeDistance;

    public RiverRaster(int count)
    {
        Carve = Filled(count, float.PositiveInfinity);
        Channel = Filled(count, float.PositiveInfinity);
        Bank = Filled(count, float.NegativeInfinity);
        Surface = Filled(count, float.NaN);
        OwnerNorm = Filled(count, float.PositiveInfinity);
        FlowX = new float[count];
        FlowY = new float[count];
        Hint = Filled(count, float.NaN);
        HintDistance = Filled(count, float.PositiveInfinity);
        HintEdgeDistance = Filled(count, float.PositiveInfinity);
    }

    private static float[] Filled(int count, float value)
    {
        float[] array = new float[count];
        for (int i = 0; i < count; i++)
            array[i] = value;
        return array;
    }
}
