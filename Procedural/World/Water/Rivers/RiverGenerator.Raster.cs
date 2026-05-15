using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// RiverGenerator, part 5 of 5: drawing rivers into a chunk's grid (see RiverGenerator.cs).
public static partial class RiverGenerator
{
    // Water flow speed for shaders: a gentle base current, faster with the surface's drop per unit length.
    private const float FlowBaseSpeed = 0.4f;
    private const float FlowGradeSpeed = 12f;
    private const float FlowMaxSpeed = 4f;

    /// <summary>
    /// Writes every river segment's influence into a chunk's per-cell arrays. All combinations are
    /// min/max, so the result doesn't depend on the order rivers or segments are processed in.
    /// </summary>
    public static void Rasterize(List<RiverPath> rivers, WaterSettings s, Vector2 origin, int size, RiverRaster raster)
    {
        Vector2 rectMin = origin;
        Vector2 rectMax = origin + new Vector2(size - 1, size - 1);
        float bankWidth = Mathf.Max(4f, 1.5f * s.LodCells);
        float valleyTan = s.RiverValleySlopeTan;

        for (int r = 0; r < rivers.Count; r++)
        {
            RiverPath river = rivers[r];
            if (!river.Intersects(rectMin, rectMax))
                continue;

            for (int i = 0; i < river.Points.Length - 1; i++)
            {
                Vector2 a = river.Points[i];
                Vector2 b = river.Points[i + 1];
                float reach = Mathf.Max(river.ValleyHalfWidth[i], river.ValleyHalfWidth[i + 1]) + ValleyFade;

                int x0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.x, b.x) - reach - origin.x));
                int x1 = Mathf.Min(size - 1, Mathf.FloorToInt(Mathf.Max(a.x, b.x) + reach - origin.x));
                int y0 = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.y, b.y) - reach - origin.y));
                int y1 = Mathf.Min(size - 1, Mathf.FloorToInt(Mathf.Max(a.y, b.y) + reach - origin.y));
                if (x0 > x1 || y0 > y1)
                    continue;

                Vector2 ab = b - a;
                float lengthSq = ab.sqrMagnitude;

                // Flow along this segment: its direction, faster where the water surface drops steeply.
                float segmentLength = Mathf.Sqrt(lengthSq);
                Vector2 flow = Vector2.zero;
                if (segmentLength > 1e-4f)
                {
                    float grade = Mathf.Max(0f, river.Surface[i] - river.Surface[i + 1]) / segmentLength;
                    flow = ab / segmentLength * Mathf.Clamp(FlowBaseSpeed + grade * FlowGradeSpeed, FlowBaseSpeed, FlowMaxSpeed);
                }

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = origin.x + x - a.x;
                        float py = origin.y + y - a.y;
                        float t = lengthSq > 1e-8f ? Mathf.Clamp01((px * ab.x + py * ab.y) / lengthSq) : 0f;
                        float dx = px - ab.x * t;
                        float dy = py - ab.y * t;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);

                        float valley = Mathf.Lerp(river.ValleyHalfWidth[i], river.ValleyHalfWidth[i + 1], t);
                        if (distance > valley + ValleyFade)
                            continue;

                        int index = y * size + x;
                        float surface = Mathf.Lerp(river.Surface[i], river.Surface[i + 1], t);
                        float bed = Mathf.Lerp(river.Bed[i], river.Bed[i + 1], t);
                        float halfWidth = Mathf.Lerp(river.HalfWidth[i], river.HalfWidth[i + 1], t);
                        float crest = surface + s.RiverBankFreeboard;
                        float target;

                        if (distance <= halfWidth)
                        {
                            // Rounded channel: bed at the centerline, back up to bank height at the edge.
                            float q = distance / halfWidth;
                            target = bed + (crest - bed) * q * q;
                            raster.Channel[index] = Mathf.Min(raster.Channel[index], target);

                            if (q < raster.OwnerNorm[index])
                            {
                                raster.OwnerNorm[index] = q;
                                raster.Surface[index] = surface;
                                raster.FlowX[index] = flow.x;
                                raster.FlowY[index] = flow.y;
                            }
                        }
                        else
                        {
                            target = crest + (distance - halfWidth) * valleyTan;
                            if (distance <= halfWidth + bankWidth)
                                raster.Bank[index] = Mathf.Max(raster.Bank[index], crest);
                        }

                        // Past the valley edge the carve fades out (by lifting its target far above any
                        // terrain), so the valley blends into the untouched land instead of ending in a step.
                        float fade = 1f - WaterGenerator.SmoothStep01((distance - valley) / ValleyFade);
                        raster.Carve[index] = Mathf.Min(raster.Carve[index], target + (1f - fade) * CarveFadeLift);

                        if (distance < raster.HintDistance[index])
                        {
                            raster.HintDistance[index] = distance;
                            raster.Hint[index] = surface;
                            raster.HintEdgeDistance[index] = distance - halfWidth;
                        }
                    }
                }
            }
        }
    }
}
