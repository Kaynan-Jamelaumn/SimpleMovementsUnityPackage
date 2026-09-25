using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Turns steep stretches of a river into waterfalls. Wherever the water surface drops faster than a
/// steep-rapids grade over a meaningful height (a river running off a cliff, a plateau edge, a volcano
/// flank or into a valley), the drop is rebuilt as one or more falls: a flat pool, a rock lip, a sheer
/// drop and a plunge pool, repeated as a multi-tier fall when the drop is taller than one tier. Each
/// pool sits at the level the original surface had at the pool's downstream end, so the water is never
/// raised above its banks, and the surface still only ever goes downhill.
/// </summary>
public static class Waterfalls
{
    private const float FallGrade = 0.3f;   // surface drop per unit of river length that counts as "falling"
    private const float LipHalfGap = 0.35f; // the sheer drop happens over twice this distance

    public static RiverFall[] Shape(List<Vector2> points, List<float> natural, List<float> halfWidth, List<float> depth,
        List<float> surface, List<float> extraDepth, WaterSettings s)
    {
        int n = points.Count;
        if (n < 3)
            return new RiverFall[0];

        var arc = new float[n];
        for (int i = 1; i < n; i++)
            arc[i] = arc[i - 1] + Vector2.Distance(points[i - 1], points[i]);

        // Find steep zones (steep segments, allowing one short gentler segment between them).
        var lipArc = new List<float>();
        var lipTop = new List<float>();
        var lipBottom = new List<float>();
        var pointLevel = new float[n];
        for (int i = 0; i < n; i++)
            pointLevel[i] = float.NaN;

        int k = 0;
        while (k < n - 1)
        {
            if (!Steep(k, arc, surface))
            {
                k++;
                continue;
            }

            int a = k, b = k + 1, j = k + 1;
            while (j < n - 1)
            {
                if (Steep(j, arc, surface)) { b = j + 1; j++; }
                else if (j + 1 < n - 1 && Steep(j + 1, arc, surface) && arc[j + 1] - arc[j] < 12f) { j++; }
                else break;
            }
            k = b;

            float drop = surface[a] - surface[b];
            if (drop < s.WaterfallMinDrop)
                continue;

            float start = arc[a], end = arc[b], length = end - start;
            int tiers = Mathf.Clamp(Mathf.CeilToInt(drop / s.WaterfallTierHeight), 1, 4);
            tiers = Mathf.Max(1, Mathf.Min(tiers, Mathf.FloorToInt(length / 6f)));

            var lips = new float[tiers + 1];
            for (int t = 0; t < tiers; t++)
                lips[t] = Mathf.Min(start + length * t / tiers + 0.6f, end - 0.6f);
            lips[tiers] = end;

            var levels = new float[tiers];
            for (int t = 0; t < tiers; t++)
                levels[t] = SurfaceAt(lips[t + 1], arc, surface);

            for (int t = 0; t < tiers; t++)
            {
                lipArc.Add(lips[t]);
                lipTop.Add(t == 0 ? SurfaceAt(lips[0], arc, surface) : levels[t - 1]);
                lipBottom.Add(levels[t]);
            }

            for (int q = a + 1; q <= b; q++)
            {
                int passed = 0;
                while (passed < tiers && lips[passed] < arc[q])
                    passed++;
                if (passed > 0)
                    pointLevel[q] = levels[passed - 1];
            }
        }

        if (lipArc.Count == 0)
            return new RiverFall[0];

        // Rebuild the lists with two points around each lip (upper and lower water level).
        var newPoints = new List<Vector2>(n + lipArc.Count * 2);
        var newNatural = new List<float>(newPoints.Capacity);
        var newHalfWidth = new List<float>(newPoints.Capacity);
        var newDepth = new List<float>(newPoints.Capacity);
        var newSurface = new List<float>(newPoints.Capacity);
        var newArc = new List<float>(newPoints.Capacity);
        var falls = new List<RiverFall>(lipArc.Count);
        int lip = 0;
        for (int i = 0; i < n; i++)
        {
            while (i > 0 && lip < lipArc.Count && lipArc[lip] < arc[i])
            {
                float p = lipArc[lip];
                float gap = Mathf.Min(LipHalfGap, 0.25f * (arc[i] - arc[i - 1]));
                AddAt(p - gap, lipTop[lip], i - 1, arc, points, natural, halfWidth, depth, newPoints, newNatural, newHalfWidth, newDepth, newSurface, newArc);
                AddAt(p + gap, lipBottom[lip], i - 1, arc, points, natural, halfWidth, depth, newPoints, newNatural, newHalfWidth, newDepth, newSurface, newArc);
                falls.Add(new RiverFall { Position = PointAt(p, i - 1, arc, points), Top = lipTop[lip], Bottom = lipBottom[lip] });
                lip++;
            }

            newPoints.Add(points[i]);
            newNatural.Add(natural[i]);
            newHalfWidth.Add(halfWidth[i]);
            newDepth.Add(depth[i]);
            newSurface.Add(float.IsNaN(pointLevel[i]) ? surface[i] : pointLevel[i]);
            newArc.Add(arc[i]);
        }

        // Plunge pools below each fall; a shallower rock lip just above it.
        var extra = new float[newPoints.Count];
        for (int f = 0; f < falls.Count; f++)
        {
            float p = lipArc[f];
            float fallHeight = falls[f].Top - falls[f].Bottom;
            for (int i = 0; i < newPoints.Count; i++)
            {
                float d = newArc[i] - p;
                float pool = Mathf.Max(3f, 1.5f * newHalfWidth[i]);
                if (d > 0f && d < pool)
                    extra[i] = Mathf.Max(extra[i], Mathf.Min(0.4f * fallHeight, 1.5f * newDepth[i]) * (1f - d / pool));
                else if (d <= 0f && d > -2f)
                    extra[i] = Mathf.Min(extra[i], -0.4f * newDepth[i]);
            }
        }

        points.Clear(); points.AddRange(newPoints);
        natural.Clear(); natural.AddRange(newNatural);
        halfWidth.Clear(); halfWidth.AddRange(newHalfWidth);
        depth.Clear(); depth.AddRange(newDepth);
        surface.Clear(); surface.AddRange(newSurface);
        extraDepth.Clear(); extraDepth.AddRange(extra);
        return falls.ToArray();
    }

    private static bool Steep(int i, float[] arc, List<float> surface)
    {
        float length = Mathf.Max(0.5f, arc[i + 1] - arc[i]);
        return surface[i] - surface[i + 1] >= FallGrade * length;
    }

    private static float SurfaceAt(float p, float[] arc, List<float> surface)
    {
        int i = Segment(p, arc);
        float t = Mathf.InverseLerp(arc[i], arc[i + 1], p);
        return Mathf.Lerp(surface[i], surface[i + 1], t);
    }

    private static Vector2 PointAt(float p, int segment, float[] arc, List<Vector2> points)
    {
        float t = Mathf.InverseLerp(arc[segment], arc[segment + 1], p);
        return Vector2.Lerp(points[segment], points[segment + 1], t);
    }

    private static int Segment(float p, float[] arc)
    {
        int i = 0;
        while (i < arc.Length - 2 && arc[i + 1] < p)
            i++;
        return i;
    }

    private static void AddAt(float p, float level, int segment, float[] arc, List<Vector2> points, List<float> natural, List<float> halfWidth, List<float> depth,
        List<Vector2> outPoints, List<float> outNatural, List<float> outHalfWidth, List<float> outDepth, List<float> outSurface, List<float> outArc)
    {
        float t = Mathf.InverseLerp(arc[segment], arc[segment + 1], p);
        outPoints.Add(Vector2.Lerp(points[segment], points[segment + 1], t));
        outNatural.Add(Mathf.Lerp(natural[segment], natural[segment + 1], t));
        outHalfWidth.Add(Mathf.Lerp(halfWidth[segment], halfWidth[segment + 1], t));
        outDepth.Add(Mathf.Lerp(depth[segment], depth[segment + 1], t));
        outSurface.Add(level);
        outArc.Add(p);
    }
}
