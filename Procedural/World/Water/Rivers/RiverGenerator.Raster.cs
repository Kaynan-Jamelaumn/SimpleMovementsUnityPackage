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
    // How far (world units) past a stretch's end its water level keeps its slope (see Rasterize).
    private const float CapSlopeReach = 6f;
    // Steepness (rise per unit) of the rock face left where a valley below a drop meets the ground above it.
    private const float DropFaceTan = 3f;
    // A bank is raised to the level of the channel nearest to it, or of any other within this much further - more
    // than a cell's diagonal, so every channel with water in a neighboring cell counts.
    private const float BankNearestTolerance = 1.5f;

    private struct BankCandidate
    {
        public int Index;
        public float EdgeDistance;
        public float Crest;
    }

    [ThreadStatic] private static List<BankCandidate> bankCandidates;

    /// <summary>
    /// A river's sheer drops - segments whose water falls more than twice their length - with, per segment, the
    /// nearest drop segment before it and after it (-1 = none), and each point's distance along the river.
    /// </summary>
    private static void FindDrops(RiverPath river, out bool[] isDrop, out float[] arc, out int[] dropBefore, out int[] dropAfter)
    {
        int segments = river.Points.Length - 1;
        isDrop = new bool[Mathf.Max(0, segments)];
        arc = new float[river.Points.Length];
        dropBefore = new int[isDrop.Length];
        dropAfter = new int[isDrop.Length];
        for (int i = 0; i < segments; i++)
        {
            float lengthSq = (river.Points[i + 1] - river.Points[i]).sqrMagnitude;
            float drop = river.Surface[i] - river.Surface[i + 1];
            isDrop[i] = drop > 1f && drop * drop > 4f * lengthSq;
            arc[i + 1] = arc[i] + Mathf.Sqrt(lengthSq);
        }
        int last = -1;
        for (int i = 0; i < segments; i++)
        {
            dropBefore[i] = last;
            if (isDrop[i])
                last = i;
        }
        last = -1;
        for (int i = segments - 1; i >= 0; i--)
        {
            dropAfter[i] = last;
            if (isDrop[i])
                last = i;
        }
    }

    /// <summary>
    /// How far a valley's carve is lifted <paramref name="beyond"/> units past its edge: the valley wall steepens
    /// gradually (about 45 degrees a quarter of the way out, over 60 halfway) so it meets higher ground as a steep
    /// slope rather than a sheer step, then rises out of reach before the carve ends.
    /// </summary>
    private static float ValleyEdgeLift(float beyond)
    {
        if (beyond <= 0f)
            return 0f;
        float u = beyond / ValleyFade;
        return 2f * ValleyFade * u * u + CarveFadeLift * WaterGenerator.SmoothStep01((u - 0.8f) / 0.2f);
    }

    /// <summary>
    /// The line across a valley at a sheer drop, separating the ground above it from the ground below. It is a
    /// horseshoe - crossing the river at the drop and curving downstream to both sides, the way a fall cuts back
    /// into its ledge - with a slight wobble, so the ledge dies out into the valley sides instead of running
    /// straight across it.
    /// </summary>
    private struct DropLine
    {
        private readonly Vector2 point, direction;
        private readonly float bend, wobble, phase;

        public DropLine(RiverPath river, int segment, Vector2 point)
        {
            this.point = point;
            Vector2 axis = river.Points[segment + 1] - river.Points[segment];
            direction = axis.sqrMagnitude > 1e-10f ? axis.normalized : Vector2.zero;
            float drop = river.Surface[segment] - river.Surface[segment + 1];
            float width = Mathf.Max(10f, 1.5f * drop + 3f * river.HalfWidth[segment]);
            bend = 1f / (3f * width);
            wobble = 0.12f * width;
            phase = WaterGenerator.Hash(Mathf.RoundToInt(point.x * 8f), Mathf.RoundToInt(point.y * 8f), 0, 0x5EDE) * (Mathf.PI * 2f / int.MaxValue);
        }

        /// <summary>Which side of the line a position lies on: negative above it, positive below, about in world units.</summary>
        public float Side(float x, float y)
        {
            float dx = x - point.x, dy = y - point.y;
            float along = dx * direction.x + dy * direction.y;
            float across = dy * direction.x - dx * direction.y;
            float wander = wobble * (Mathf.Sin(across * 0.21f + phase) + 0.6f * Mathf.Sin(across * 0.53f + 1.7f * phase));
            // No wobble right at the river, so the lip itself stays exactly where the water drops.
            return along - bend * across * across - wander * Mathf.Min(1f, Mathf.Abs(across) / 8f);
        }
    }

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
        List<BankCandidate> banks = bankCandidates ?? (bankCandidates = new List<BankCandidate>());
        banks.Clear();

        for (int r = 0; r < rivers.Count; r++)
        {
            RiverPath river = rivers[r];
            if (!river.Intersects(rectMin, rectMax))
                continue;

            // Sheer drops (a waterfall's lip, or a cliff the river runs over): the stretches on either side stand
            // at very different water levels, so neither reaches past the drop's line - the valley below doesn't
            // cut into the ground above it, and the banks above aren't raised beside the pool below. That leaves
            // a clean ledge across the valley instead of a raised channel standing in a carved-out hollow.
            FindDrops(river, out bool[] isDrop, out float[] arc, out int[] dropBefore, out int[] dropAfter);

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

                // The drop line just upstream of this stretch (it keeps to the side below it) and the one just
                // downstream (it keeps to the side above it), when within the stretch's reach.
                int above = dropBefore[i] >= 0 && arc[i] - arc[dropBefore[i] + 1] <= reach ? dropBefore[i] : -1;
                int below = dropAfter[i] >= 0 && arc[dropAfter[i]] - arc[i + 1] <= reach ? dropAfter[i] : -1;
                DropLine aboveLine = above >= 0 ? new DropLine(river, above, river.Points[above + 1]) : default;
                DropLine belowLine = below >= 0 ? new DropLine(river, below, river.Points[below]) : default;

                // Flow along this segment: its direction, faster where the water surface drops steeply.
                float segmentLength = Mathf.Sqrt(lengthSq);

                // Past a stretch's ends (the round caps that fill the outside of bends) its water level carries on
                // at the stretch's own slope for a few units, instead of staying at the end's level: on a steep
                // stretch that level would otherwise spill beside the next stretch and raise flat steps of bank
                // there, or cut the valley of the stretch above lower than its own. Not past the river's source
                // or mouth.
                float capReach = segmentLength > 1e-4f ? Mathf.Min(1f, CapSlopeReach / segmentLength) : 0f;
                float levelMin = i == 0 ? 0f : -capReach;
                float levelMax = i == river.Points.Length - 2 ? 1f : 1f + capReach;

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
                        float along = lengthSq > 1e-8f ? (px * ab.x + py * ab.y) / lengthSq : 0f;
                        if (isDrop[i] && (along < 0f || along > 1f))
                            continue;
                        if (below >= 0 && belowLine.Side(origin.x + x, origin.y + y) > 0f)
                            continue;
                        // Above the drop just upstream, this stretch only carves - and less the further above the
                        // line, leaving a steep rock face rather than a sheer wall.
                        float aboveFace = above >= 0 ? Mathf.Max(0f, -aboveLine.Side(origin.x + x, origin.y + y)) : 0f;
                        if (aboveFace * DropFaceTan > ValleyFade * 2f)
                            continue;
                        float t = Mathf.Clamp01(along);
                        float dx = px - ab.x * t;
                        float dy = py - ab.y * t;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);

                        float valley = Mathf.Lerp(river.ValleyHalfWidth[i], river.ValleyHalfWidth[i + 1], t);
                        if (distance > valley + ValleyFade)
                            continue;

                        int index = y * size + x;
                        float faceLift = aboveFace * DropFaceTan;
                        float level = Mathf.Clamp(along, levelMin, levelMax);
                        float surface = Mathf.LerpUnclamped(river.Surface[i], river.Surface[i + 1], level);
                        float bed = Mathf.LerpUnclamped(river.Bed[i], river.Bed[i + 1], level);
                        float halfWidth = Mathf.Lerp(river.HalfWidth[i], river.HalfWidth[i + 1], t);
                        float crest = surface + s.RiverBankFreeboard;
                        float target;

                        if (faceLift > 0f)
                        {
                            float faceTarget = crest + Mathf.Max(0f, distance - halfWidth) * valleyTan + faceLift;
                            raster.Carve[index] = Mathf.Min(raster.Carve[index], faceTarget + ValleyEdgeLift(distance - valley));
                            continue;
                        }

                        // Banks are decided once every river is drawn (see below): note how near this channel is.
                        float edgeDistance = distance - halfWidth;
                        if (edgeDistance <= bankWidth)
                        {
                            raster.BankEdgeDistance[index] = Mathf.Min(raster.BankEdgeDistance[index], edgeDistance);
                            if (edgeDistance > 0f)
                                banks.Add(new BankCandidate { Index = index, EdgeDistance = edgeDistance, Crest = crest });
                        }

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
                        }

                        // Past the valley edge the carve fades out, so the valley blends into the untouched land.
                        raster.Carve[index] = Mathf.Min(raster.Carve[index], target + ValleyEdgeLift(distance - valley));

                        if (distance < raster.HintDistance[index])
                        {
                            raster.HintDistance[index] = distance;
                            raster.Hint[index] = surface;
                            raster.HintEdgeDistance[index] = distance - halfWidth;
                            raster.HintDirX[index] = segmentLength > 1e-4f ? ab.x / segmentLength : 0f;
                            raster.HintDirY[index] = segmentLength > 1e-4f ? ab.y / segmentLength : 0f;
                        }
                    }
                }
            }

            // Waterfalls' plunge basins, carved into the land below each lip.
            foreach (RiverFall fall in river.Falls)
            {
                float basin = Waterfalls.BasinReach(fall);
                int x0 = Mathf.Max(0, Mathf.CeilToInt(fall.Position.x - basin - origin.x));
                int x1 = Mathf.Min(size - 1, Mathf.FloorToInt(fall.Position.x + basin - origin.x));
                int y0 = Mathf.Max(0, Mathf.CeilToInt(fall.Position.y - basin - origin.y));
                int y1 = Mathf.Min(size - 1, Mathf.FloorToInt(fall.Position.y + basin - origin.y));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        int index = y * size + x;
                        raster.Carve[index] = Mathf.Min(raster.Carve[index], Waterfalls.BasinCarve(fall, origin.x + x, origin.y + y, s.RiverBankFreeboard));
                    }
                }
            }
        }

        // Banks: raised to the water level of the channel they are beside - the nearest, or another about as near -
        // not of every stretch within reach: a stretch's reach goes past its ends, so on steep water the level of
        // the stretch above would otherwise stand beside the lower water below as raised steps.
        for (int k = 0; k < banks.Count; k++)
        {
            BankCandidate bank = banks[k];
            if (bank.EdgeDistance <= raster.BankEdgeDistance[bank.Index] + BankNearestTolerance)
                raster.Bank[bank.Index] = Mathf.Max(raster.Bank[bank.Index], bank.Crest);
        }
        banks.Clear();
    }
}
