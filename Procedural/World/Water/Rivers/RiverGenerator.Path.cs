using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// RiverGenerator, part 4 of 5: turning a traced course into a river path - water levels, widths, depths and valleys (see RiverGenerator.cs).
public static partial class RiverGenerator
{
    /// <summary>
    /// A lake's actual water level: its natural <see cref="LakeFeature.Level"/>, lowered to the surface of
    /// any river that cuts through its rim below that level (other than its own inlets, which never sit
    /// below it, and its own outlet). Such a river would otherwise leave the lake's water standing next
    /// to a lower channel - it drains the lake instead, the way a river cutting into a pond does.
    /// </summary>
    public static float ComputeLakeWaterLevel(LakeFeature lake, WaterSettings s, TerrainHeightSampler sampler)
    {
        float level = lake.Level;
        if (!s.RiversEnabled)
            return level;

        List<RiverPath> rivers = new List<RiverPath>();
        Vector2 extent = new Vector2(lake.BoundRadius, lake.BoundRadius);
        Gather(lake.Center - extent, lake.Center + extent, s, sampler, rivers);

        for (int r = 0; r < rivers.Count; r++)
        {
            RiverPath river = rivers[r];
            if (river.TerminalLake == lake || river.SourceLake == lake)
                continue;

            for (int i = 0; i < river.Points.Length; i++)
            {
                Vector2 point = river.Points[i];
                if (!lake.TryGetLocal(point.x, point.y, out _, out float beyond))
                    continue;

                if (beyond - river.HalfWidth[i] <= lake.RimWidth && river.Surface[i] < level)
                    level = river.Surface[i];
            }
        }

        return level;
    }

    /// <summary>
    /// At a sharp bend (more than about 60 degrees) the channel just before the corner overlaps the channel just
    /// after it on the inside of the bend. If the water dropped along that approach, the upper water would stand
    /// against ground the lower stretch carved away. So the approach within reach of the corner is levelled to
    /// the corner's water height, moving the drop upstream onto the straight part. Water only ever gets lower,
    /// and the surface still never rises downstream.
    /// </summary>
    private static void LevelSharpBends(List<Vector2> points, List<float> halfWidth, List<float> surfaces, WaterSettings s)
    {
        float bank = Mathf.Max(4f, 1.5f * s.LodCells);
        for (int k = 1; k < points.Count - 1; k++)
        {
            Vector2 incoming = points[k] - points[k - 1];
            Vector2 outgoing = points[k + 1] - points[k];
            if (incoming.sqrMagnitude < 1e-6f || outgoing.sqrMagnitude < 1e-6f)
                continue;
            if (Vector2.Dot(incoming.normalized, outgoing.normalized) > 0.5f)
                continue;   // turns less than 60 degrees

            float reach = 2f * halfWidth[k] + bank;
            int first = k;
            for (int j = k - 1; j >= 0 && (points[j] - points[k]).sqrMagnitude < reach * reach; j--)
                first = j;
            for (int j = first; j < k; j++)
                surfaces[j] = Mathf.Min(surfaces[j], surfaces[k]);
        }
    }

    private static void TruncateAfter(List<Vector2> points, List<float> natural, List<bool> insideSource, int lastIndex)
    {
        int keep = lastIndex + 1;
        if (keep >= points.Count)
            return;

        points.RemoveRange(keep, points.Count - keep);
        natural.RemoveRange(keep, natural.Count - keep);
        insideSource.RemoveRange(keep, insideSource.Count - keep);
    }

    private static void AddPoint(List<Vector2> points, List<float> natural, List<bool> insideSource, Vector2 point, TerrainHeightSampler sampler)
    {
        points.Add(point);
        natural.Add(sampler.SampleBaseHeight(point.x, point.y));
        insideSource.Add(false);
    }

    private static RiverPath BuildPath(List<Vector2> points, List<float> natural, List<bool> insideSource, float length, float surfaceCap,
        WaterBodyType mouth, LakeFeature receivingLake, LakeFeature terminal, float phase, WaterSettings s, TerrainHeightSampler sampler)
    {
        int n = points.Count;
        var halfWidth = new List<float>(n);
        var depth = new List<float>(n);
        for (int i = 0; i < n; i++)
        {
            float progress = Mathf.Clamp01(i * TraceStep / Mathf.Max(TraceStep, length));
            float widthNoise = Mathf.PerlinNoise(i * TraceStep / 110f + phase * 2.3f, 7.7f) * 2f - 1f;
            float width = Mathf.Lerp(s.RiverSourceWidth, s.RiverMouthWidth, Mathf.Pow(progress, 0.6f)) * (1f + s.RiverWidthVariation * widthNoise);
            halfWidth.Add(Mathf.Max(0.75f, width * 0.5f));
            depth.Add(s.RiverDepth * Mathf.Lerp(0.4f, 1f, progress));
        }

        // Water surface: running minimum of the terrain beside the river (minus freeboard), so it only
        // ever goes downhill and always sits below both banks where they were sampled.
        var surfaces = new List<float>(n);
        float surface = surfaceCap;
        for (int i = 0; i < n; i++)
        {
            if (!insideSource[i])
            {
                Vector2 tangent = points[Mathf.Min(i + 1, n - 1)] - points[Mathf.Max(i - 1, 0)];
                tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : new Vector2(1f, 0f);
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float offset = halfWidth[i] + 2f;
                Vector2 left = points[i] + normal * offset;
                Vector2 right = points[i] - normal * offset;
                float bankMin = Mathf.Min(natural[i], Mathf.Min(sampler.SampleBaseHeight(left.x, left.y), sampler.SampleBaseHeight(right.x, right.y)));
                surface = Mathf.Min(surface, bankMin - s.RiverBankFreeboard);
            }

            surfaces.Add(surface);
        }

        // Never below whatever it flows into (still non-increasing: max of a non-increasing series and a constant).
        float receivingLevel = mouth == WaterBodyType.Ocean ? s.SeaLevel : receivingLake.Level;
        for (int i = 0; i < n; i++)
            surfaces[i] = Mathf.Max(surfaces[i], receivingLevel);

        if (s.RiverMeanderCutoffs)
            LevelSharpBends(points, halfWidth, surfaces, s);

        var extraDepth = new List<float>(new float[n]);
        RiverFall[] falls = s.WaterfallsEnabled
            ? Waterfalls.Shape(points, natural, halfWidth, depth, surfaces, extraDepth, s)
            : new RiverFall[0];

        n = points.Count;
        RiverPath river = new RiverPath
        {
            Points = points.ToArray(),
            Surface = surfaces.ToArray(),
            Bed = new float[n],
            HalfWidth = halfWidth.ToArray(),
            ValleyHalfWidth = new float[n],
            MouthType = mouth,
            TerminalLake = terminal,
            Length = length,
            Falls = falls,
        };

        float maxValley = 0f;
        Vector2 boundsMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 boundsMax = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < n; i++)
        {
            river.Bed[i] = river.Surface[i] - depth[i] - extraDepth[i];

            // How far below the untouched terrain the river runs decides the valley: a shallow cut makes
            // a wide gentle valley, a deep one (cutting through a rise) a gorge, capped at the max width.
            float cut = Mathf.Max(0f, natural[i] - river.Surface[i] - s.RiverBankFreeboard);
            float valley = river.HalfWidth[i] * 2.2f + 6f + cut / s.RiverValleySlopeTan;
            valley = Mathf.Min(valley, Mathf.Max(river.HalfWidth[i] * 2.2f + 6f, s.RiverMaxValleyHalfWidth));
            river.ValleyHalfWidth[i] = valley;

            maxValley = Mathf.Max(maxValley, valley);
            boundsMin = Vector2.Min(boundsMin, points[i]);
            boundsMax = Vector2.Max(boundsMax, points[i]);
        }

        Vector2 pad = new Vector2(maxValley + ValleyFade, maxValley + ValleyFade);
        boundsMin -= pad;
        boundsMax += pad;
        if (terminal != null)
        {
            Vector2 lakePad = new Vector2(terminal.BoundRadius, terminal.BoundRadius);
            boundsMin = Vector2.Min(boundsMin, terminal.Center - lakePad);
            boundsMax = Vector2.Max(boundsMax, terminal.Center + lakePad);
        }

        river.BoundsMin = boundsMin;
        river.BoundsMax = boundsMax;
        return river;
    }
}
