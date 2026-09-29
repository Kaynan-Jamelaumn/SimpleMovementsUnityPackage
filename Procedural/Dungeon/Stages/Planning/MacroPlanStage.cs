using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Stage 1. Decides the dungeon's shape before any floor exists: floor count, each floor's size, position,
    /// style, openness, complexity and difficulty; then the anchors every floor must honour - the entrance, the
    /// exit, and the stair wells and drops between consecutive floors (placed where both floors have room, with each
    /// floor's way down far from its way in). Because anchors are fixed first, each floor can be laid out on its own
    /// (in parallel) and still line up with its neighbours.
    /// </summary>
    public sealed class MacroPlanStage : IDungeonStage
    {
        public string Name => "Macro plan";

        private const int GridPad = 2;

        public void Run(DungeonContext ctx)
        {
            CompiledProfile p = ctx.Profile;
            DungeonRequest request = ctx.Request;
            DungeonLayout layout = ctx.Layout;
            DungeonRandom rng = ctx.Random("MacroPlan");

            SizeClassSettings size = p.GetSize(request.size);
            Vector2Int range = size.floorCount.x > 0
                ? new Vector2Int(size.floorCount.x, Mathf.Max(size.floorCount.x, size.floorCount.y))
                : p.FloorCount;
            int floorCount = request.floorCount > 0 ? request.floorCount : rng.Range(range.x, range.y + 1);
            floorCount = Mathf.Clamp(floorCount, 1, 64);

            // A floor must hold a stair well plus a landing at each end, with margins.
            int minSide = p.StairLength + p.Links.landingSize.max * 2 + p.Links.edgeMargin * 2 + 2;
            var baseCells = new Vector2Int(Mathf.Max(minSide, size.floorCells.x), Mathf.Max(minSide, size.floorCells.y));
            float variation = Mathf.Clamp(p.FootprintVariation, 0f, 0.4f);
            int gridW = Mathf.CeilToInt(baseCells.x * (1f + variation)) + GridPad * 2;
            int gridH = Mathf.CeilToInt(baseCells.y * (1f + variation)) + GridPad * 2;
            layout.Width = gridW;
            layout.Height = gridH;

            FloorStyle previous = FloorStyle.Rooms;
            for (int f = 0; f < floorCount; f++)
            {
                int w = Mathf.Clamp(Mathf.RoundToInt(baseCells.x * (1f + rng.Range(-variation, variation))), minSide, gridW - GridPad * 2);
                int h = Mathf.Clamp(Mathf.RoundToInt(baseCells.y * (1f + rng.Range(-variation, variation))), minSide, gridH - GridPad * 2);
                int x = GridPad + rng.Range(0, gridW - GridPad * 2 - w + 1);
                int y = GridPad + rng.Range(0, gridH - GridPad * 2 - h + 1);

                var spec = new FloorSpec
                {
                    Index = f,
                    Footprint = new RectInt(x, y, w, h),
                    IsFirst = f == 0,
                    IsLast = f == floorCount - 1,
                    BaseY = -f * p.FloorSpacing,
                    Openness = p.Openness.Lerp(rng.Value()),
                    Complexity = p.Complexity.Lerp(rng.Value()),
                    Difficulty = Mathf.Max(0.1f, request.difficulty) * (1f + p.DifficultyPerFloor * f + 0.25f * request.depth),
                    BuiltWeight = p.Hybrid.builtWeight,
                    CavernWeight = p.Hybrid.cavernWeight,
                    RuinsWeight = p.Hybrid.ruinsWeight,
                };
                spec.Style = request.overrideStyle ? request.style : PickStyle(p, f, f == 0 ? (FloorStyle?)null : previous, rng);
                previous = spec.Style;

                var floor = new FloorLayout { Index = f, Spec = spec, Grid = new TileGrid(gridW, gridH) };
                TileGrid grid = floor.Grid;
                for (int cy = 0; cy < gridH; cy++)
                    for (int cx = 0; cx < gridW; cx++)
                        if (!spec.Footprint.Contains(new Vector2Int(cx, cy)))
                            grid.Set(grid.Index(cx, cy), CellFlags.Reserved);
                layout.Floors.Add(floor);
            }

            new AnchorPlanner(ctx, rng).Plan();
        }

        private static FloorStyle PickStyle(CompiledProfile p, int floor, FloorStyle? previous, DungeonRandom rng)
        {
            var weights = new float[5];
            for (int s = 0; s < 5; s++)
            {
                var style = (FloorStyle)s;
                float w = Mathf.Max(0f, p.Styles.Get(style));
                if (style == FloorStyle.Caverns || style == FloorStyle.Hybrid)
                    w += w > 0f ? Mathf.Min(p.NaturalWeightPerFloor * floor, 1.2f) : 0f;
                if (previous.HasValue && previous.Value == style)
                    w *= p.RepeatStylePenalty;
                weights[s] = w;
            }
            int pick = rng.WeightedIndex(weights);
            return pick < 0 ? FloorStyle.Rooms : (FloorStyle)pick;
        }
    }

    /// <summary>Places the entrance, exit, stair wells and drops (see <see cref="MacroPlanStage"/>).</summary>
    internal sealed class AnchorPlanner
    {
        private readonly DungeonContext ctx;
        private readonly CompiledProfile p;
        private readonly DungeonLayout layout;
        private readonly DungeonRandom rng;
        private readonly List<RectInt>[] occupied;

        private const int RoomEdgeMargin = 2;

        public AnchorPlanner(DungeonContext ctx, DungeonRandom rng)
        {
            this.ctx = ctx;
            p = ctx.Profile;
            layout = ctx.Layout;
            this.rng = rng;
            occupied = new List<RectInt>[layout.Floors.Count];
            for (int i = 0; i < occupied.Length; i++)
                occupied[i] = new List<RectInt>();
        }

        public void Plan()
        {
            int n = layout.Floors.Count;

            Anchor entrance = PlaceRoomAnchor(0, AnchorKind.Entrance, p.Links.entranceSize, true, null);
            if (entrance == null)
                ctx.Fail("No room for the entrance.");
            layout.Floors[0].ArrivalCell = layout.Floors[0].Grid.Index(entrance.Cell);
            Vector2Int arrival = entrance.Cell;

            for (int f = 0; f < n - 1; f++)
            {
                VerticalLink main = PlaceStairs(f, arrival, true);
                if (main == null)
                    ctx.Fail($"No room for stairs between floors {f} and {f + 1}.");
                layout.Floors[f].DepartureCell = layout.Floors[f].Grid.Index(main.UpperLanding);
                layout.Floors[f + 1].ArrivalCell = layout.Floors[f + 1].Grid.Index(main.LowerLanding);

                for (int k = 0; k < p.Links.maxExtraStairs; k++)
                    if (rng.Chance(p.Links.extraStairChance))
                        PlaceStairs(f, arrival, false);
                if (rng.Chance(p.Links.dropChance))
                    PlaceDrop(f);

                arrival = main.LowerLanding;
            }

            Anchor exit = PlaceRoomAnchor(n - 1, AnchorKind.Exit, p.Links.exitSize, false, arrival);
            if (exit == null)
                ctx.Fail("No room for the exit.");
            layout.Floors[n - 1].DepartureCell = layout.Floors[n - 1].Grid.Index(exit.Cell);
        }

        // ------------------------------------------------------------------ fitting

        private static RectInt Shrink(RectInt r, int by) => new RectInt(r.x + by, r.y + by, r.width - by * 2, r.height - by * 2);

        private static RectInt Expand(RectInt r, int by) => new RectInt(r.x - by, r.y - by, r.width + by * 2, r.height + by * 2);

        private static bool Inside(RectInt inner, RectInt outer)
        {
            return inner.width > 0 && inner.height > 0 && inner.xMin >= outer.xMin && inner.yMin >= outer.yMin && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
        }

        private bool Free(int floor, RectInt r, int spacing)
        {
            foreach (RectInt o in occupied[floor])
                if (Expand(o, spacing).Overlaps(r))
                    return false;
            return true;
        }

        private bool FitsRoom(int floor, RectInt r)
        {
            RectInt area = Shrink(layout.Floors[floor].Spec.Footprint, RoomEdgeMargin);
            return Inside(r, area) && Free(floor, r, p.Links.anchorSpacing);
        }

        private bool FitsShaft(int floor, RectInt r)
        {
            RectInt area = Shrink(layout.Floors[floor].Spec.Footprint, p.Links.edgeMargin);
            return Inside(r, area) && Free(floor, r, p.Links.anchorSpacing);
        }

        private static float Distance(Vector2Int a, Vector2Int b) => Vector2.Distance(a, b);

        private static Vector2Int CenterOf(RectInt r) => new Vector2Int(r.x + r.width / 2, r.y + r.height / 2);

        /// <summary>Picks the candidate whose score is closest to a random share of the best score.</summary>
        private int PickFar(List<float> scores, FloatRange share)
        {
            float max = 0f;
            foreach (float s in scores)
                max = Mathf.Max(max, s);
            float target = max * Mathf.Clamp01(share.Random(rng));
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < scores.Count; i++)
            {
                float d = Mathf.Abs(scores[i] - target);
                if (d < bestDiff)
                {
                    bestDiff = d;
                    best = i;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ entrance / exit

        private Anchor PlaceRoomAnchor(int floor, AnchorKind kind, IntRange size, bool preferEdge, Vector2Int? farFrom)
        {
            FloorLayout f = layout.Floors[floor];
            RectInt area = Shrink(f.Spec.Footprint, RoomEdgeMargin);
            var rects = new List<RectInt>();
            var sides = new List<Dir4>();
            for (int attempt = 0; attempt < 160 && rects.Count < 30; attempt++)
            {
                int s = Mathf.Max(4, size.Random(rng) - (attempt > 80 ? 1 : 0));
                int w = Mathf.Max(4, s + rng.Range(-1, 2));
                int h = Mathf.Max(4, s + rng.Range(-1, 2));
                if (area.width < w || area.height < h)
                    continue;
                int x = area.x + rng.Range(0, area.width - w + 1);
                int y = area.y + rng.Range(0, area.height - h + 1);
                Dir4 side = Dir4.South;
                if (preferEdge)
                {
                    side = (Dir4)rng.Range(0, 4);
                    switch (side)
                    {
                        case Dir4.North: y = area.yMax - h; break;
                        case Dir4.South: y = area.y; break;
                        case Dir4.East: x = area.xMax - w; break;
                        default: x = area.x; break;
                    }
                }
                var r = new RectInt(x, y, w, h);
                if (!FitsRoom(floor, r))
                    continue;
                rects.Add(r);
                sides.Add(side);
                if (!farFrom.HasValue)
                    break;
            }
            if (rects.Count == 0)
                return null;

            int pick = 0;
            if (farFrom.HasValue)
            {
                var scores = new List<float>();
                foreach (RectInt r in rects)
                    scores.Add(Distance(CenterOf(r), farFrom.Value));
                pick = PickFar(scores, p.Links.departureDistance);
            }

            RectInt room = rects[pick];
            Vector2Int center = CenterOf(room);
            Dir4 facing;
            if (preferEdge)
                facing = sides[pick].Opposite();
            else if (farFrom.HasValue)
                facing = Dir4Util.FromDelta(farFrom.Value.x - center.x, farFrom.Value.y - center.y);
            else
                facing = Dir4.North;

            var anchor = new Anchor { Kind = kind, Floor = floor, Room = room, Cell = center, Facing = facing };
            f.Anchors.Add(anchor);
            occupied[floor].Add(room);
            return anchor;
        }

        // ------------------------------------------------------------------ stairs

        /// <summary>Well, upper landing room and lower landing room for a stair descending in direction d.</summary>
        private static void StairRects(RectInt well, Dir4 d, int roomWidth, int roomDepth, int across,
            out RectInt upperRoom, out RectInt lowerRoom, out Vector2Int upperLanding, out Vector2Int lowerLanding)
        {
            // "across" offsets the rooms sideways (0 .. roomWidth - wellWidth).
            switch (d)
            {
                case Dir4.North: // walking north goes down: the upper end is the south edge.
                    upperLanding = new Vector2Int(well.x + well.width / 2, well.y - 1);
                    lowerLanding = new Vector2Int(well.x + well.width / 2, well.yMax);
                    upperRoom = new RectInt(well.x - across, well.y - roomDepth, roomWidth, roomDepth);
                    lowerRoom = new RectInt(well.x - across, well.yMax, roomWidth, roomDepth);
                    break;
                case Dir4.South:
                    upperLanding = new Vector2Int(well.x + well.width / 2, well.yMax);
                    lowerLanding = new Vector2Int(well.x + well.width / 2, well.y - 1);
                    upperRoom = new RectInt(well.x - across, well.yMax, roomWidth, roomDepth);
                    lowerRoom = new RectInt(well.x - across, well.y - roomDepth, roomWidth, roomDepth);
                    break;
                case Dir4.East: // walking east goes down: the upper end is the west edge.
                    upperLanding = new Vector2Int(well.x - 1, well.y + well.height / 2);
                    lowerLanding = new Vector2Int(well.xMax, well.y + well.height / 2);
                    upperRoom = new RectInt(well.x - roomDepth, well.y - across, roomDepth, roomWidth);
                    lowerRoom = new RectInt(well.xMax, well.y - across, roomDepth, roomWidth);
                    break;
                default:
                    upperLanding = new Vector2Int(well.xMax, well.y + well.height / 2);
                    lowerLanding = new Vector2Int(well.x - 1, well.y + well.height / 2);
                    upperRoom = new RectInt(well.xMax, well.y - across, roomDepth, roomWidth);
                    lowerRoom = new RectInt(well.x - roomDepth, well.y - across, roomDepth, roomWidth);
                    break;
            }
        }

        private static bool TryIntersect(RectInt a, RectInt b, out RectInt r)
        {
            int x0 = Mathf.Max(a.xMin, b.xMin), y0 = Mathf.Max(a.yMin, b.yMin);
            int x1 = Mathf.Min(a.xMax, b.xMax), y1 = Mathf.Min(a.yMax, b.yMax);
            r = new RectInt(x0, y0, x1 - x0, y1 - y0);
            return r.width > 0 && r.height > 0;
        }

        private VerticalLink PlaceStairs(int upper, Vector2Int arrival, bool main)
        {
            int lower = upper + 1;
            int length = p.StairLength;
            int width = Mathf.Clamp(p.Links.stairWidth, 1, 4);
            if (!TryIntersect(layout.Floors[upper].Spec.Footprint, layout.Floors[lower].Spec.Footprint, out RectInt shared))
                return null;
            shared = Shrink(shared, p.Links.edgeMargin);
            if (shared.width <= 0 || shared.height <= 0)
                return null;

            var options = new List<(RectInt well, Dir4 d, RectInt up, RectInt low, Vector2Int upL, Vector2Int lowL)>();
            var scores = new List<float>();
            for (int attempt = 0; attempt < 220 && options.Count < 40; attempt++)
            {
                var d = (Dir4)rng.Range(0, 4);
                bool vertical = d.IsVertical();
                int ww = vertical ? width : length, wh = vertical ? length : width;
                if (shared.width < ww || shared.height < wh)
                    continue;
                var well = new RectInt(shared.x + rng.Range(0, shared.width - ww + 1), shared.y + rng.Range(0, shared.height - wh + 1), ww, wh);

                int depth = Mathf.Max(3, p.Links.landingSize.Random(rng) - (attempt > 150 ? 1 : 0));
                int roomWidth = Mathf.Max(width + 2, p.Links.landingSize.Random(rng));
                int across = rng.Range(0, roomWidth - width + 1);
                StairRects(well, d, roomWidth, depth, across, out RectInt upRoom, out RectInt lowRoom, out Vector2Int upL, out Vector2Int lowL);

                if (!FitsShaft(upper, well) || !FitsShaft(lower, well))
                    continue;
                if (!FitsRoom(upper, upRoom) || !FitsRoom(lower, lowRoom))
                    continue;

                options.Add((well, d, upRoom, lowRoom, upL, lowL));
                scores.Add(main ? Distance(upL, arrival) : rng.Value());
            }
            if (options.Count == 0)
                return null;

            int pick = main ? PickFar(scores, p.Links.departureDistance) : rng.Range(0, options.Count);
            var o = options[pick];

            var link = new VerticalLink
            {
                Id = layout.Links.Count,
                Kind = LinkKind.Stairs,
                UpperFloor = upper,
                LowerFloor = lower,
                Footprint = o.well,
                Descend = o.d,
                UpperLanding = o.upL,
                LowerLanding = o.lowL,
                OnMainPath = main,
            };
            layout.Links.Add(link);

            layout.Floors[upper].Anchors.Add(new Anchor
            {
                Kind = AnchorKind.StairsDownLanding, Floor = upper, Room = o.up, Cell = o.upL, LinkId = link.Id,
                Facing = o.d.Opposite(), Shaft = o.well,
            });
            layout.Floors[lower].Anchors.Add(new Anchor
            {
                Kind = AnchorKind.StairsUpLanding, Floor = lower, Room = o.low, Cell = o.lowL, LinkId = link.Id,
                Facing = o.d, Shaft = o.well,
            });
            occupied[upper].Add(o.well);
            occupied[upper].Add(o.up);
            occupied[lower].Add(o.well);
            occupied[lower].Add(o.low);
            return link;
        }

        // ------------------------------------------------------------------ drops

        private VerticalLink PlaceDrop(int upper)
        {
            int lower = upper + 1;
            int size = Mathf.Clamp(p.Links.dropSize, 1, 3);
            if (!TryIntersect(layout.Floors[upper].Spec.Footprint, layout.Floors[lower].Spec.Footprint, out RectInt shared))
                return null;
            shared = Shrink(shared, p.Links.edgeMargin);
            for (int attempt = 0; attempt < 60; attempt++)
            {
                int ring = rng.Range(2, 4);
                int roomSide = size + ring * 2;
                if (shared.width < roomSide || shared.height < roomSide)
                    return null;
                var room = new RectInt(shared.x + rng.Range(0, shared.width - roomSide + 1), shared.y + rng.Range(0, shared.height - roomSide + 1), roomSide, roomSide);
                var pit = new RectInt(room.x + ring, room.y + ring, size, size);
                if (!FitsRoom(upper, room) || !FitsRoom(lower, room))
                    continue;

                var upL = new Vector2Int(pit.x - 1, pit.y + size / 2);
                var lowL = new Vector2Int(pit.x + size / 2, pit.y + size / 2);
                var link = new VerticalLink
                {
                    Id = layout.Links.Count,
                    Kind = LinkKind.Drop,
                    UpperFloor = upper,
                    LowerFloor = lower,
                    Footprint = pit,
                    Descend = Dir4.North,
                    UpperLanding = upL,
                    LowerLanding = lowL,
                };
                layout.Links.Add(link);
                layout.Floors[upper].Anchors.Add(new Anchor
                {
                    Kind = AnchorKind.DropSource, Floor = upper, Room = room, Cell = upL, LinkId = link.Id, Facing = Dir4.East, Shaft = pit,
                });
                layout.Floors[lower].Anchors.Add(new Anchor
                {
                    Kind = AnchorKind.DropLanding, Floor = lower, Room = room, Cell = lowL, LinkId = link.Id, Facing = Dir4.North, NoCeiling = pit,
                });
                occupied[upper].Add(room);
                occupied[lower].Add(room);
                return link;
            }
            return null;
        }
    }
}
