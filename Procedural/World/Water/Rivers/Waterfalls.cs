using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Turns steep stretches of a river into waterfalls. Wherever the water surface drops at least as steeply as
/// Waterfall Min Slope over a meaningful height (a river running off a cliff, a ledge, a plateau edge or a
/// sea cliff), the drop is rebuilt as one or more falls: a flat pool, a rock lip, a sheer
/// drop and a plunge pool, repeated as a multi-tier fall when the drop is taller than one tier. Each
/// pool sits at the level the original surface had at the pool's downstream end, so the water is never
/// raised above its banks, and the surface still only ever goes downhill.
///
/// The land takes part too: below each lip a rounded plunge basin is carved into the slope (see
/// <see cref="BasinCarve"/>), so the drop is a ledge running across the valley - highest at the river and
/// dying out to both sides, like a fall that has cut back into a horseshoe - instead of a slot with walls
/// cut into an otherwise smooth hillside.
/// </summary>
public static class Waterfalls
{
    private const float LipHalfGap = 0.35f; // the sheer drop happens over twice this distance

    /// <summary>
    /// Rebuilds the steep stretches of a river (given per point) as waterfalls, adding two points around each lip.
    /// <paramref name="inFall"/> receives, per resulting point, whether it lies in a fall - below a lip, in a pool
    /// lowered into the drop - where the land beside the water is the fall's own rock face rather than ground the
    /// river has to cut a valley through.
    /// </summary>
    public static RiverFall[] Shape(List<Vector2> points, List<float> natural, List<float> halfWidth, List<float> depth,
        List<float> surface, List<float> extraDepth, List<bool> inFall, WaterSettings s)
    {
        int n = points.Count;
        inFall.Clear();
        for (int i = 0; i < n; i++)
            inFall.Add(false);
        if (n < 3)
            return new RiverFall[0];

        var arc = new float[n];
        for (int i = 1; i < n; i++)
            arc[i] = arc[i - 1] + Vector2.Distance(points[i - 1], points[i]);

        // Find steep zones (steep segments, allowing one short gentler segment between them).
        var lipArc = new List<float>();
        var lipTop = new List<float>();
        var lipBottom = new List<float>();
        var lipLength = new List<float>();
        var pointLevel = new float[n];
        for (int i = 0; i < n; i++)
            pointLevel[i] = float.NaN;

        int k = 0;
        while (k < n - 1)
        {
            if (!Steep(k, arc, surface, s.WaterfallMinGrade))
            {
                k++;
                continue;
            }

            int a = k, b = k + 1, j = k + 1;
            while (j < n - 1)
            {
                if (Steep(j, arc, surface, s.WaterfallMinGrade)) { b = j + 1; j++; }
                else if (j + 1 < n - 1 && Steep(j + 1, arc, surface, s.WaterfallMinGrade) && arc[j + 1] - arc[j] < 12f) { j++; }
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
                lipLength.Add(lips[t + 1] - lips[t]);
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
        var newInFall = new List<bool>(newPoints.Capacity);
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
                newInFall.Add(false);
                newInFall.Add(true);
                Vector2 direction = points[i] - points[i - 1];
                float lipHalfWidth = Mathf.Lerp(halfWidth[i - 1], halfWidth[i], Mathf.InverseLerp(arc[i - 1], arc[i], p));
                float drop = lipTop[lip] - lipBottom[lip];
                falls.Add(new RiverFall
                {
                    Position = PointAt(p, i - 1, arc, points),
                    Direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : new Vector2(1f, 0f),
                    Top = lipTop[lip],
                    Bottom = lipBottom[lip],
                    Length = lipLength[lip],
                    BasinHalfWidth = Mathf.Clamp(0.8f * drop + 2.5f * lipHalfWidth + 3f, 6f, 40f),
                });
                lip++;
            }

            newPoints.Add(points[i]);
            newNatural.Add(natural[i]);
            newHalfWidth.Add(halfWidth[i]);
            newDepth.Add(depth[i]);
            newSurface.Add(float.IsNaN(pointLevel[i]) ? surface[i] : pointLevel[i]);
            newArc.Add(arc[i]);
            newInFall.Add(!float.IsNaN(pointLevel[i]));
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
        inFall.Clear(); inFall.AddRange(newInFall);
        return falls.ToArray();
    }

    /// <summary>How far from its lip a fall's plunge basin can reach.</summary>
    public static float BasinReach(RiverFall fall)
    {
        return fall.Length + 1.5f * fall.BasinHalfWidth;
    }

    /// <summary>
    /// Height the land is carved down to (keep the lower of this and the terrain) by a fall's plunge basin: a
    /// bowl below the lip, its floor just above the lower pool, widest at the lip and closing downstream over the
    /// tier's length. Where it cuts into the slope it leaves a rock ledge along the lip - the full height of the
    /// drop at the river, lower to the sides, gone at the basin's edge - so the fall reads as a step in the land.
    /// Upstream of the lip nothing is carved below the water above it plus 2 units.
    /// </summary>
    public static float BasinCarve(RiverFall fall, float x, float y, float freeboard)
    {
        float dx = x - fall.Position.x, dy = y - fall.Position.y;
        float along = dx * fall.Direction.x + dy * fall.Direction.y;
        float across = dy * fall.Direction.x - dx * fall.Direction.y;
        float drop = Mathf.Max(0f, fall.Top - fall.Bottom);
        float width = Mathf.Max(1f, fall.BasinHalfWidth);
        float lift = drop + 2f;

        float a = across / width;
        float b = Mathf.Max(0f, along) / Mathf.Max(1f, fall.Length + 0.5f * width);
        float floor = fall.Bottom + Mathf.Max(0.5f, freeboard) + lift * (a * a + b * b);
        // The ledge's face, just below the lip (so the banks of the pool above stay above its water), and
        // well above anything upstream.
        float face = Mathf.Clamp(0.15f * drop, 0.8f, 2.5f);
        if (along < face)
            floor += lift * (1f - SmoothStep01(along / face)) + 4f * Mathf.Max(0f, -along);
        return floor;
    }

    private static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static bool Steep(int i, float[] arc, List<float> surface, float grade)
    {
        float length = Mathf.Max(0.5f, arc[i + 1] - arc[i]);
        return surface[i] - surface[i + 1] >= grade * length;
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
