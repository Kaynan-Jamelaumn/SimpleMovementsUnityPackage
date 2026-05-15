using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// RiverGenerator, part 2 of 5: tracing a river downhill from its spring or lake outlet (see RiverGenerator.cs).
public static partial class RiverGenerator
{
    private static RiverPath Trace(Vector2 start, Vector2 initialDirection, float surfaceCap, LakeFeature sourceLake,
        float phase, bool isSpring, WaterSettings s, TerrainHeightSampler sampler)
    {
        int maxPoints = Mathf.Max(3, Mathf.CeilToInt(s.RiverMaxLength / TraceStep) + 1);
        List<Vector2> points = new List<Vector2>(maxPoints + 8);
        List<float> natural = new List<float>(maxPoints + 8);
        List<bool> insideSource = new List<bool>(maxPoints + 8);
        Queue<Vector2> spillPath = new Queue<Vector2>();

        Vector2 position = start;
        Vector2 direction = initialDirection.sqrMagnitude > 1e-6f ? initialDirection.normalized : Vector2.zero;
        bool leavingSource = sourceLake != null;
        float lowest = float.PositiveInfinity;
        int lowestIndex = 0;
        int spills = 0;
        bool followingSpill = false;
        WaterBodyType mouth = WaterBodyType.None;
        LakeFeature receivingLake = null;

        while (points.Count < maxPoints)
        {
            float height = sampler.SampleBaseHeight(position.x, position.y);
            int index = points.Count;
            points.Add(position);
            natural.Add(height);

            if (leavingSource)
            {
                // An outlet heads straight out through its lake's lowest rim point before following the
                // terrain - otherwise the lake's own basin could pull it right back in.
                bool stillInside = sourceLake.TryGetLocal(position.x, position.y, out float rho, out _) && rho < 1.25f;
                insideSource.Add(stillInside);
                if (stillInside)
                {
                    position += direction * TraceStep;
                    continue;
                }

                leavingSource = false;
            }
            else
            {
                insideSource.Add(false);
            }

            if (followingSpill)
            {
                // Just arrived past the saddle: measure progress from here, not from the pit bottom
                // (the ground past a saddle only has to be lower than the saddle, not than the pit).
                if (spillPath.Count == 0)
                {
                    followingSpill = false;
                    lowest = height;
                    lowestIndex = index;
                }
            }
            else if (height < lowest - 0.05f)
            {
                lowest = height;
                lowestIndex = index;
            }

            if (index > 0)
            {
                if (s.OceansEnabled && OceanGenerator.LandSide(s, position.x, position.y) < 0f)
                {
                    mouth = WaterBodyType.Ocean;
                    break;
                }

                // Slightly beyond the outline: a river passing that close flows into the lake as an inlet
                // rather than grazing (and cutting through) its rim.
                LakeFeature lake = LakeGenerator.FindLakeContaining(position, sourceLake, s, sampler, InletReach);
                if (lake != null)
                {
                    mouth = WaterBodyType.Lake;
                    receivingLake = lake;
                    break;
                }
            }

            if (spillPath.Count > 0)
            {
                // Following a spill path out of a pit: the water surface stays at the pit's level along it,
                // so crossing the rim becomes a gorge cut through its lowest saddle.
                Vector2 next = spillPath.Dequeue();
                direction = (next - position).sqrMagnitude > 1e-6f ? (next - position).normalized : direction;
                position = next;
                continue;
            }

            if (index - lowestIndex > PitSteps)
            {
                // No lower ground for a while: trapped in a pit. Back up to its lowest point and overflow
                // it the way water would - through the lowest saddle of its rim. A basin too deep to fill
                // (or out of spills) ends the river in a terminal lake instead.
                TruncateAfter(points, natural, insideSource, lowestIndex);
                position = points[lowestIndex];

                if (spills >= MaxPitSpills || !TryFindSpillPath(position, lowest, sourceLake, s, sampler, spillPath))
                    break;

                spills++;
                followingSpill = true;
                // Trimmed back to the pit bottom, so the first spill waypoint comes next.
                Vector2 first = spillPath.Dequeue();
                direction = (first - position).sqrMagnitude > 1e-6f ? (first - position).normalized : direction;
                position = first;
                lowestIndex = points.Count - 1;
                continue;
            }

            // Downhill direction from a fairly wide finite difference, so small noise bumps don't steer it.
            float heightX = sampler.SampleBaseHeight(position.x + GradientEpsilon, position.y) - sampler.SampleBaseHeight(position.x - GradientEpsilon, position.y);
            float heightY = sampler.SampleBaseHeight(position.x, position.y + GradientEpsilon) - sampler.SampleBaseHeight(position.x, position.y - GradientEpsilon);
            Vector2 downhill = new Vector2(-heightX, -heightY);
            if (downhill.sqrMagnitude > 1e-10f)
                downhill.Normalize();
            else
                downhill = direction.sqrMagnitude > 0f ? direction : new Vector2(1f, 0f);

            if (s.OceansEnabled)
            {
                // Continental drainage: a gentle pull toward the nearest coast (the continent field's
                // downhill direction, sampled wide so it reflects the landmass rather than local detail).
                // Local terrain still dominates - this only tilts a step by up to ~27 degrees - but on
                // flat or undulating ground it keeps rivers heading for the sea instead of wandering.
                float sideX = OceanGenerator.LandSide(s, position.x + CoastBiasEpsilon, position.y) - OceanGenerator.LandSide(s, position.x - CoastBiasEpsilon, position.y);
                float sideY = OceanGenerator.LandSide(s, position.x, position.y + CoastBiasEpsilon) - OceanGenerator.LandSide(s, position.x, position.y - CoastBiasEpsilon);
                Vector2 seaward = new Vector2(-sideX, -sideY);
                if (seaward.sqrMagnitude > 1e-12f)
                    downhill = (downhill + seaward.normalized * CoastBias).normalized;
            }

            if (direction.sqrMagnitude == 0f)
                direction = downhill;

            // Meander: rotate the downhill direction by a smooth, arc-length-driven wobble (two octaves so
            // bends aren't all the same size). Capped below 90 degrees, so every step still goes downhill.
            float arc = index * TraceStep;
            float wiggle = s.RiverMeander * (
                (Mathf.PerlinNoise(arc / s.RiverMeanderWavelength + phase, 0.37f) * 2f - 1f)
                + 0.5f * (Mathf.PerlinNoise(arc / (s.RiverMeanderWavelength * 0.37f) + phase * 1.7f, 5.1f) * 2f - 1f));
            Vector2 desired = Rotate(downhill, wiggle);

            Vector2 blended = desired * (1f - Inertia) + direction * Inertia;
            direction = blended.sqrMagnitude > 1e-8f ? blended.normalized : desired;
            position += direction * TraceStep;
        }

        if (mouth == WaterBodyType.None)
        {
            // Trapped in an enclosed basin, or out of length: end at the lowest point reached, where a
            // terminal lake will form.
            TruncateAfter(points, natural, insideSource, Mathf.Max(1, lowestIndex));
        }

        if (s.RiverMeanderCutoffs)
            CutOffLoops(points, natural, insideSource, s, sampler);

        if (points.Count < 3)
            return null;

        float length = (points.Count - 1) * TraceStep;
        if (isSpring && length < s.RiverMinLength)
            return null;

        // Run the channel a little way into whatever the river flows into, so the two waters connect.
        if (mouth == WaterBodyType.Ocean)
        {
            Vector2 last = points[points.Count - 1];
            Vector2 heading = (last - points[points.Count - 2]).normalized;
            for (int k = 1; k <= 3; k++)
                AddPoint(points, natural, insideSource, last + heading * (TraceStep * k), sampler);
        }
        else if (mouth == WaterBodyType.Lake)
        {
            Vector2 last = points[points.Count - 1];
            Vector2 target = last + (receivingLake.Center - last) * 0.45f;
            for (int k = 1; k <= 3; k++)
                AddPoint(points, natural, insideSource, Vector2.Lerp(last, target, k / 3f), sampler);
        }

        LakeFeature terminal = null;
        if (mouth == WaterBodyType.None)
        {
            float radius = Mathf.Clamp(18f + length * 0.012f, 18f, MaxTerminalLakeRadius);
            terminal = LakeGenerator.CreateTerminalLake(points[points.Count - 1], radius, s, sampler);
            mouth = WaterBodyType.Lake;
            receivingLake = terminal;
        }

        RiverPath river = BuildPath(points, natural, insideSource, length, surfaceCap, mouth, receivingLake, terminal, phase, s, sampler);
        river.SourceLake = sourceLake;
        return river;
    }

    /// <summary>
    /// Where the course bends back so tightly that it passes right beside (or over) an earlier stretch of
    /// itself, the river takes the shortcut, as real rivers cut through a meander neck: the loop in between is
    /// dropped. Otherwise the two stretches' channels would overlap at different water levels, leaving the
    /// upper stretch's water standing against ground the lower stretch had carved away.
    /// </summary>
    private static void CutOffLoops(List<Vector2> points, List<float> natural, List<bool> insideSource, WaterSettings s, TerrainHeightSampler sampler)
    {
        float bank = Mathf.Max(4f, 1.5f * s.LodCells);
        int i = 4;
        while (i < points.Count)
        {
            int n = points.Count;
            float halfI = TypicalHalfWidth(i, n, s);
            int cut = -1;
            for (int j = 0; j <= i - 4; j++)
            {
                if (insideSource[j] || insideSource[i])
                    continue;
                float reach = halfI + TypicalHalfWidth(j, n, s) + bank + 2f;
                if ((points[i] - points[j]).sqrMagnitude < reach * reach)
                {
                    cut = j;
                    break;
                }
            }

            if (cut < 0)
            {
                i++;
                continue;
            }

            int remove = i - cut - 1;
            points.RemoveRange(cut + 1, remove);
            natural.RemoveRange(cut + 1, remove);
            insideSource.RemoveRange(cut + 1, remove);

            // The shortcut can be a few steps long: fill it with points at the usual spacing, so the water level
            // steps down along it like anywhere else instead of sloping across one long segment.
            Vector2 from = points[cut], to = points[cut + 1];
            int extra = Mathf.CeilToInt(Vector2.Distance(from, to) / TraceStep) - 1;
            for (int k = 1; k <= extra; k++)
            {
                Vector2 point = Vector2.Lerp(from, to, k / (float)(extra + 1));
                points.Insert(cut + k, point);
                natural.Insert(cut + k, sampler.SampleBaseHeight(point.x, point.y));
                insideSource.Insert(cut + k, false);
            }
            i = cut + 2 + Mathf.Max(0, extra);
        }
    }

    /// <summary>A river's widest likely half-width at a point, from the same taper <see cref="BuildPath"/> uses.</summary>
    private static float TypicalHalfWidth(int index, int count, WaterSettings s)
    {
        float progress = count > 1 ? Mathf.Clamp01(index / (float)(count - 1)) : 0f;
        return 0.5f * Mathf.Lerp(s.RiverSourceWidth, s.RiverMouthWidth, Mathf.Pow(progress, 0.6f)) * (1f + s.RiverWidthVariation);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
