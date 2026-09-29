using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Room-based floors: rooms of weighted size classes and varied shapes (rectangles, L/T/cross, circles,
    /// composites, pillared halls), each placed at the best of a few random spots (the one farthest from existing
    /// rooms, so they spread out) with a random wall spacing - sometimes a single wall, so neighbours join through a
    /// door. Covers a share of the floor set by Openness; Complexity favours more, smaller rooms.
    /// </summary>
    public sealed class RoomScatterLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            Scatter(ctx, floor, rng, LayoutUtil.FootprintMask(floor), null, 1f);
        }

        /// <summary>
        /// Places rooms on the allowed cells. <paramref name="styleAt"/> (optional) gives the style of a room by its
        /// centre (hybrid floors: built or ruins).
        /// </summary>
        public static void Scatter(DungeonContext ctx, FloorLayout floor, DungeonRandom rng, bool[] allowed,
            Func<Vector2Int, ZoneStyle> styleAt, float coverageScale)
        {
            RoomSettings rs = ctx.Profile.Rooms;
            FloorSpec spec = floor.Spec;
            TileGrid g = floor.Grid;
            RectInt fp = spec.Footprint;

            int allowedCells = LayoutUtil.Count(allowed);
            float target = allowedCells * rs.coverage.Lerp(spec.Openness) * coverageScale;
            int placed = 0;
            foreach (Area a in floor.Areas)
                foreach (int c in a.Cells)
                    if (allowed[c])
                        placed++;

            var centers = new List<Vector2>();
            foreach (Area a in floor.Areas)
                centers.Add(a.Center);

            // Size class weights, shifted by openness and complexity.
            var weights = new float[rs.sizes.Length];
            for (int i = 0; i < rs.sizes.Length; i++)
            {
                RoomSizeClass s = rs.sizes[i];
                float open = Mathf.Max(0.05f, 1f + s.opennessBias * (spec.Openness * 2f - 1f));
                float complex = Mathf.Max(0.05f, 1f - s.opennessBias * 0.5f * (spec.Complexity * 2f - 1f));
                weights[i] = Mathf.Max(0f, s.weight) * open * complex;
            }

            int attempts = Mathf.Max(10, rs.placementAttempts);
            int failuresInARow = 0;
            while (placed < target && attempts-- > 0 && failuresInARow < 160)
            {
                int sizeIndex = rng.WeightedIndex(weights);
                if (sizeIndex < 0)
                    break;
                RoomSizeClass sizeClass = rs.sizes[sizeIndex];
                int side = sizeClass.size.Random(rng);
                // Shrink after many failures so the floor still fills up.
                if (failuresInARow > 60)
                    side = Mathf.Max(3, side - failuresInARow / 30);
                float aspect = 1f + rng.Triangular() * rs.aspectVariation;
                int w = Mathf.Max(3, Mathf.RoundToInt(side * aspect));
                int h = Mathf.Max(3, Mathf.RoundToInt(side / aspect));
                if (w >= fp.width - 2 || h >= fp.height - 2)
                {
                    failuresInARow++;
                    continue;
                }

                int spacing = rng.Chance(rs.tightPackingChance) ? 1 : Mathf.Max(1, rs.spacing.Random(rng));

                // Candidate positions: keep the one farthest from other rooms that fits.
                int k = Mathf.Max(1, rs.spreadCandidates);
                var candidates = new List<(float score, int x, int y)>(k);
                for (int c = 0; c < k; c++)
                {
                    int x = fp.xMin + 1 + rng.Range(0, Mathf.Max(1, fp.width - w - 2));
                    int y = fp.yMin + 1 + rng.Range(0, Mathf.Max(1, fp.height - h - 2));
                    var center = new Vector2(x + w * 0.5f, y + h * 0.5f);
                    float nearest = float.MaxValue;
                    foreach (Vector2 other in centers)
                        nearest = Mathf.Min(nearest, (other - center).sqrMagnitude);
                    candidates.Add((nearest, x, y));
                }
                candidates.Sort((a, b) => b.score.CompareTo(a.score));

                ZoneStyle style = ZoneStyle.Built;
                ShapeMask mask = null;
                bool done = false;
                foreach (var cand in candidates)
                {
                    if (styleAt != null)
                        style = styleAt(new Vector2Int(cand.x + w / 2, cand.y + h / 2));
                    if (mask == null || (styleAt != null && style == ZoneStyle.Ruins) != (mask.Shape == RoomShape.Ruined))
                    {
                        RoomShape shape = style == ZoneStyle.Ruins
                            ? RoomShape.Ruined
                            : LayoutUtil.PickShape(rs.shapes, w, h, sizeClass.hall, rng);
                        mask = RoomShapes.Generate(shape, w, h, rng, ctx.Profile.Hybrid.ruinsErosion);
                    }
                    if (!LayoutUtil.CanPlace(floor, mask, cand.x, cand.y, spacing, allowed))
                        continue;

                    AreaKind kind = sizeClass.hall || mask.FloorCount >= 110 ? AreaKind.Hall : AreaKind.Room;
                    Area area = LayoutUtil.Stamp(floor, mask, cand.x, cand.y, kind, style);
                    placed += area.Cells.Count;
                    centers.Add(area.Center);
                    done = true;
                    break;
                }
                failuresInARow = done ? 0 : failuresInARow + 1;
            }
        }
    }
}
