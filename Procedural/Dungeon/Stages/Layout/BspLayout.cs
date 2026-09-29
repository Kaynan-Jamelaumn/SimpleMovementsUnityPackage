using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Constructed, fortress-like floors: the footprint is split recursively (binary space partition) into
    /// partitions, and each partition gets one room (mostly rectangular) with a margin, so rooms line up in an
    /// orderly way with room for corridors between them. Complexity makes partitions smaller (more rooms);
    /// Openness makes rooms fill more of their partition.
    /// </summary>
    public sealed class BspLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            BspSettings bs = ctx.Profile.Bsp;
            FloorSpec spec = floor.Spec;
            bool[] allowed = LayoutUtil.FootprintMask(floor);

            int minLeaf = Mathf.Max(6, Mathf.RoundToInt(bs.minLeafSize * Mathf.Lerp(1.25f, 0.8f, spec.Complexity)));
            RectInt root = new RectInt(spec.Footprint.x + 1, spec.Footprint.y + 1, spec.Footprint.width - 2, spec.Footprint.height - 2);
            var leaves = new List<RectInt>();
            Split(root, 0, minLeaf, bs, spec.Complexity, rng, leaves);

            float fillMin = Mathf.Lerp(0.45f, 0.65f, spec.Openness);
            foreach (RectInt leaf in leaves)
            {
                if (rng.Chance(bs.emptyLeafChance))
                    continue;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    int margin = Mathf.Max(1, bs.roomMargin.Random(rng));
                    int maxW = leaf.width - margin * 2, maxH = leaf.height - margin * 2;
                    if (maxW < 3 || maxH < 3)
                        break;
                    int w = rng.Range(Mathf.Max(3, Mathf.RoundToInt(maxW * fillMin)), maxW + 1);
                    int h = rng.Range(Mathf.Max(3, Mathf.RoundToInt(maxH * fillMin)), maxH + 1);
                    if (attempt > 0)
                    {
                        w = Mathf.Max(3, w - attempt * 2);
                        h = Mathf.Max(3, h - attempt * 2);
                    }
                    int x = leaf.x + margin + rng.Range(0, maxW - w + 1);
                    int y = leaf.y + margin + rng.Range(0, maxH - h + 1);

                    RoomShape shape = rng.Chance(bs.shapedRoomChance)
                        ? LayoutUtil.PickShape(ctx.Profile.Rooms.shapes, w, h, w * h >= 120, rng)
                        : RoomShape.Rectangle;
                    ShapeMask mask = RoomShapes.Generate(shape, w, h, rng);
                    if (!LayoutUtil.CanPlace(floor, mask, x, y, 1, allowed))
                        continue;
                    AreaKind kind = mask.FloorCount >= 110 ? AreaKind.Hall : AreaKind.Room;
                    LayoutUtil.Stamp(floor, mask, x, y, kind, ZoneStyle.Built);
                    break;
                }
            }
        }

        private static void Split(RectInt r, int depth, int minLeaf, BspSettings bs, float complexity, DungeonRandom rng, List<RectInt> leaves)
        {
            bool canSplitX = r.width >= minLeaf * 2;
            bool canSplitY = r.height >= minLeaf * 2;
            bool stop = depth > 2 && rng.Chance(bs.earlyStopChance * (1.3f - complexity));
            if ((!canSplitX && !canSplitY) || stop || depth > 12)
            {
                leaves.Add(r);
                return;
            }

            bool splitX;
            if (canSplitX && canSplitY)
                splitX = r.width > r.height * 1.25f || (r.height <= r.width * 1.25f && rng.Chance(0.5f));
            else
                splitX = canSplitX;

            float ratio = bs.splitRatio.Random(rng);
            if (splitX)
            {
                int cut = Mathf.Clamp(Mathf.RoundToInt(r.width * ratio), minLeaf, r.width - minLeaf);
                Split(new RectInt(r.x, r.y, cut, r.height), depth + 1, minLeaf, bs, complexity, rng, leaves);
                Split(new RectInt(r.x + cut, r.y, r.width - cut, r.height), depth + 1, minLeaf, bs, complexity, rng, leaves);
            }
            else
            {
                int cut = Mathf.Clamp(Mathf.RoundToInt(r.height * ratio), minLeaf, r.height - minLeaf);
                Split(new RectInt(r.x, r.y, r.width, cut), depth + 1, minLeaf, bs, complexity, rng, leaves);
                Split(new RectInt(r.x, r.y + cut, r.width, r.height - cut), depth + 1, minLeaf, bs, complexity, rng, leaves);
            }
        }
    }
}
