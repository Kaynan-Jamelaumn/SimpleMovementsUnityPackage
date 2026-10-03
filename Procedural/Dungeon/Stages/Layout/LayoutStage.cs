using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>A way of filling a floor with areas (rooms, caverns...). Must honour the floor's anchors and reserved cells.</summary>
    public interface ILayoutStrategy
    {
        void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng);
    }

    /// <summary>
    /// Stage 2. Lays out every floor (in parallel): first the anchor areas (entrance, exit, landings, drop rooms,
    /// with their shafts), then the floor style's strategy fills the rest with areas. Output: areas stamped into
    /// the grid, not yet connected.
    /// </summary>
    public sealed class LayoutStage : IDungeonStage
    {
        public string Name => "Layout";

        /// <summary>Strategy per style; replace entries to plug in your own.</summary>
        public static readonly Dictionary<FloorStyle, ILayoutStrategy> Strategies = new Dictionary<FloorStyle, ILayoutStrategy>
        {
            { FloorStyle.Rooms, new RoomScatterLayout() },
            { FloorStyle.Bsp, new BspLayout() },
            { FloorStyle.Caverns, new CaveLayout() },
            { FloorStyle.Hybrid, new HybridLayout() },
            { FloorStyle.GridMaze, new GridMazeLayout() },
            { FloorStyle.Citadel, new CitadelLayout() },
            { FloorStyle.Catacombs, new CatacombLayout() },
            { FloorStyle.Tower, new TowerLayout() },
            { FloorStyle.Undercity, new UndercityLayout() },
            { FloorStyle.Hive, new HiveLayout() },
            { FloorStyle.Islands, new IslandLayout(false) },
            { FloorStyle.Den, new DenLayout() },
            { FloorStyle.Astral, new IslandLayout(true) },
        };

        public void Run(DungeonContext ctx)
        {
            ctx.ForEachFloor(floor =>
            {
                DungeonRandom rng = ctx.Random("Layout", floor.Index);
                AnchorAreas.Create(ctx, floor);
                if (!Strategies.TryGetValue(floor.Spec.Style, out ILayoutStrategy strategy) || strategy == null)
                    strategy = Strategies[FloorStyle.Rooms];
                strategy.Generate(ctx, floor, rng);

                LayoutUtil.RebuildAreaCells(floor);
                floor.RemoveEmptyAreas();
                foreach (Area a in floor.Areas)
                    a.RecomputeBounds(floor.Grid);

                floor.ArrivalArea = floor.Grid.Area[floor.ArrivalCell];
                floor.DepartureArea = floor.DepartureCell >= 0 ? floor.Grid.Area[floor.DepartureCell] : -1;
                if (floor.ArrivalArea < 0)
                    ctx.Fail($"Floor {floor.Index}: the arrival cell lost its area.");
            });

            // Tie the links to their anchor areas.
            foreach (VerticalLink link in ctx.Layout.Links)
            {
                link.UpperArea = AnchorArea(ctx.Layout.Floors[link.UpperFloor], link.Id);
                link.LowerArea = AnchorArea(ctx.Layout.Floors[link.LowerFloor], link.Id);
                if (link.UpperArea < 0 || link.LowerArea < 0)
                    ctx.Fail($"{link} lost its landing areas.");
            }
        }

        private static int AnchorArea(FloorLayout floor, int linkId)
        {
            foreach (Anchor a in floor.Anchors)
                if (a.LinkId == linkId || a.ExtraLinkId == linkId)
                    return a.AreaId;
            return -1;
        }
    }

    /// <summary>Turns a floor's anchors into fixed areas and reserves their shafts.</summary>
    public static class AnchorAreas
    {
        public static void Create(DungeonContext ctx, FloorLayout floor)
        {
            TileGrid g = floor.Grid;
            for (int i = 0; i < floor.Anchors.Count; i++)
            {
                Anchor anchor = floor.Anchors[i];
                AreaKind kind = anchor.Kind == AnchorKind.Entrance || anchor.Kind == AnchorKind.Exit ? AreaKind.Room : AreaKind.Landing;
                Area area = floor.AddArea(kind, ZoneStyle.Built);
                area.Fixed = true;
                area.AnchorIndex = i;
                area.Role = RoleOf(anchor.Kind);
                anchor.AreaId = area.Id;
                if (anchor.LinkId >= 0)
                    area.Links.Add(anchor.LinkId);
                if (anchor.ExtraLinkId >= 0)
                    area.Links.Add(anchor.ExtraLinkId);

                bool hasShaft = anchor.Shaft.width > 0 && anchor.Shaft.height > 0;
                if (hasShaft)
                {
                    foreach (Vector2Int c in anchor.Shaft.allPositionsWithin)
                    {
                        if (!g.InBounds(c))
                            continue;
                        int idx = g.Index(c);
                        g.Type[idx] = CellType.Link;
                        g.Area[idx] = -1;
                        g.Set(idx, CellFlags.Reserved);
                        if (anchor.Kind == AnchorKind.DropSource)
                            g.Set(idx, CellFlags.Pit);
                        else if (anchor.Kind == AnchorKind.ClimbTop)
                            g.Set(idx, CellFlags.Pit | CellFlags.Climb);
                    }
                    // Stair wells keep a ring of rock so nothing opens into their sides.
                    if (anchor.Kind == AnchorKind.StairsDownLanding || anchor.Kind == AnchorKind.StairsUpLanding)
                    {
                        var ring = new RectInt(anchor.Shaft.x - 1, anchor.Shaft.y - 1, anchor.Shaft.width + 2, anchor.Shaft.height + 2);
                        foreach (Vector2Int c in ring.allPositionsWithin)
                        {
                            if (!g.InBounds(c) || anchor.Shaft.Contains(c) || anchor.Room.Contains(c))
                                continue;
                            g.Set(g.Index(c), CellFlags.Reserved);
                        }
                    }
                }

                foreach (Vector2Int c in anchor.Room.allPositionsWithin)
                {
                    if (!g.InBounds(c))
                        continue;
                    int idx = g.Index(c);
                    if (g.Type[idx] == CellType.Link)
                        continue;
                    g.SetFloor(idx, area.Id, false);
                    if (anchor.NoCeiling.width > 0 && anchor.NoCeiling.Contains(c))
                        g.Set(idx, anchor.Kind == AnchorKind.ClimbBottom ? CellFlags.NoCeiling | CellFlags.Climb : CellFlags.NoCeiling);
                }
                if (g.InBounds(anchor.Cell))
                    g.Set(g.Index(anchor.Cell), CellFlags.Landing);

                LayoutUtil.RebuildCells(floor, area);
            }
        }

        public static AreaRole RoleOf(AnchorKind kind)
        {
            switch (kind)
            {
                case AnchorKind.Entrance: return AreaRole.Entrance;
                case AnchorKind.Exit: return AreaRole.Exit;
                case AnchorKind.StairsUpLanding: return AreaRole.StairsUp;
                case AnchorKind.StairsDownLanding: return AreaRole.StairsDown;
                case AnchorKind.DropSource:
                case AnchorKind.ClimbTop:
                    return AreaRole.DropSource;
                default: return AreaRole.DropLanding;
            }
        }

        /// <summary>Gives the anchor areas a style (caverns: organic surfaces).</summary>
        public static void SetStyle(FloorLayout floor, Area area, ZoneStyle style)
        {
            area.Style = style;
            bool organic = style == ZoneStyle.Cavern;
            foreach (int c in area.Cells)
            {
                if (organic)
                    floor.Grid.Set(c, CellFlags.Organic);
                else
                    floor.Grid.Clear(c, CellFlags.Organic);
            }
        }
    }

    /// <summary>Helpers shared by the layout strategies.</summary>
    public static class LayoutUtil
    {
        /// <summary>
        /// True if the plan <paramref name="m"/> at (ox, oy) only covers rock that may be carved (inside the footprint,
        /// allowed by <paramref name="allowed"/> when given) and no open cell lies within <paramref name="spacing"/>
        /// cells (Chebyshev) of it.
        /// </summary>
        public static bool CanPlace(FloorLayout f, ShapeMask m, int ox, int oy, int spacing, bool[] allowed)
        {
            TileGrid g = f.Grid;
            RectInt fp = f.Spec.Footprint;
            if (ox <= fp.xMin || oy <= fp.yMin || ox + m.W >= fp.xMax || oy + m.H >= fp.yMax)
                return false;

            // Cheap pass: every used cell must be carvable rock.
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    if (!m.Used(x + y * m.W))
                        continue;
                    int i = g.Index(ox + x, oy + y);
                    if (g.Type[i] != CellType.Solid || !g.IsCarvable(i) || (allowed != null && !allowed[i]))
                        return false;
                }
            }

            // Spacing pass.
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    if (!m.Used(x + y * m.W))
                        continue;
                    int cx = ox + x, cy = oy + y;
                    for (int dy = -spacing; dy <= spacing; dy++)
                    {
                        int ny = cy + dy;
                        if (ny < 0 || ny >= g.Height)
                            continue;
                        for (int dx = -spacing; dx <= spacing; dx++)
                        {
                            int nx = cx + dx;
                            if (nx < 0 || nx >= g.Width)
                                continue;
                            if (g.Type[nx + ny * g.Width] != CellType.Solid)
                                return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>Stamps a plan into the grid as a new area.</summary>
        public static Area Stamp(FloorLayout f, ShapeMask m, int ox, int oy, AreaKind kind, ZoneStyle style)
        {
            TileGrid g = f.Grid;
            Area area = f.AddArea(kind, style);
            bool organic = style == ZoneStyle.Cavern;
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int mi = x + y * m.W;
                    int i = g.Index(ox + x, oy + y);
                    if (m.Floor[mi])
                    {
                        g.SetFloor(i, area.Id, organic);
                        area.Cells.Add(i);
                    }
                    else if (m.Pillar[mi])
                    {
                        g.Set(i, CellFlags.Pillar | CellFlags.Reserved);
                    }
                }
            }
            area.RecomputeBounds(g);
            return area;
        }

        /// <summary>
        /// Makes a new area of <paramref name="cells"/> (rock cells a strategy decided to open). Cells that are not carvable
        /// rock are skipped; a chasm cell stops being a chasm. Returns null when nothing was opened.
        /// </summary>
        public static Area StampCells(FloorLayout f, List<int> cells, AreaKind kind, ZoneStyle style, CellFlags extra = CellFlags.None)
        {
            TileGrid g = f.Grid;
            Area area = null;
            bool organic = style == ZoneStyle.Cavern;
            foreach (int i in cells)
            {
                if (i < 0 || g.Type[i] != CellType.Solid || !g.IsCarvable(i))
                    continue;
                area = area ?? f.AddArea(kind, style);
                g.Clear(i, CellFlags.Chasm);
                g.SetFloor(i, area.Id, organic);
                if (extra != CellFlags.None)
                    g.Set(i, extra);
                area.Cells.Add(i);
            }
            area?.RecomputeBounds(g);
            return area;
        }

        /// <summary>
        /// Keeps the largest 4-connected piece of an area and turns its other pieces back into rock (an area must be one
        /// walkable piece: validation and population assume so).
        /// </summary>
        public static void KeepLargestPiece(FloorLayout f, Area area)
        {
            if (area == null || area.Cells.Count == 0)
                return;
            TileGrid g = f.Grid;
            var inside = new HashSet<int>(area.Cells);
            var seen = new HashSet<int>();
            List<int> best = null;
            foreach (int start in area.Cells)
            {
                if (seen.Contains(start))
                    continue;
                var piece = new List<int>();
                var stack = new Stack<int>();
                stack.Push(start);
                seen.Add(start);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    piece.Add(c);
                    for (int d = 0; d < 4; d++)
                    {
                        int nb = g.Neighbor(c, d);
                        if (nb >= 0 && inside.Contains(nb) && seen.Add(nb))
                            stack.Push(nb);
                    }
                }
                if (best == null || piece.Count > best.Count)
                    best = piece;
            }
            if (best.Count == area.Cells.Count)
                return;
            var keep = new HashSet<int>(best);
            foreach (int c in area.Cells)
                if (!keep.Contains(c))
                    g.SetSolid(c);
            RebuildCells(f, area);
        }

        /// <summary>Adds a preset connection between two areas (once).</summary>
        public static void Preset(FloorLayout floor, Area a, Area b)
        {
            if (a == null || b == null || a.Id == b.Id)
                return;
            foreach (Vector2Int p in floor.PresetConnections)
                if ((p.x == a.Id && p.y == b.Id) || (p.x == b.Id && p.y == a.Id))
                    return;
            floor.PresetConnections.Add(new Vector2Int(a.Id, b.Id));
        }

        /// <summary>A wobbly disc of cells around <paramref name="center"/> (cell units), inside the grid.</summary>
        public static List<int> Disc(TileGrid g, Vector2 center, float radius, float wobble, int seed, int salt)
        {
            var list = new List<int>();
            int r = Mathf.CeilToInt(radius * (1f + wobble)) + 1;
            int cx = Mathf.FloorToInt(center.x), cy = Mathf.FloorToInt(center.y);
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!g.InBounds(x, y))
                        continue;
                    Vector2 d = new Vector2(x + 0.5f - center.x, y + 0.5f - center.y);
                    float angle = Mathf.Atan2(d.y, d.x);
                    float edge = radius;
                    if (wobble > 0f)
                    {
                        // Smooth wobble around the outline: a few lobes from hashed phases.
                        float a1 = PlacementRandom.Value(seed, salt, 1, 0, 0) * Mathf.PI * 2f;
                        float a2 = PlacementRandom.Value(seed, salt, 2, 0, 0) * Mathf.PI * 2f;
                        edge *= 1f + wobble * 0.5f * (Mathf.Sin(angle * 2f + a1) * 0.6f + Mathf.Sin(angle * 3f + a2) * 0.4f);
                    }
                    if (d.magnitude <= edge)
                        list.Add(x + y * g.Width);
                }
            }
            return list;
        }

        /// <summary>Refreshes one area's cell list from the grid.</summary>
        public static void RebuildCells(FloorLayout f, Area area)
        {
            area.Cells.Clear();
            TileGrid g = f.Grid;
            for (int i = 0; i < g.Count; i++)
                if (g.Area[i] == area.Id && g.Type[i] != CellType.Solid && g.Type[i] != CellType.Link)
                    area.Cells.Add(i);
            area.RecomputeBounds(g);
        }

        /// <summary>Refreshes every area's cell list from the grid (one pass).</summary>
        public static void RebuildAreaCells(FloorLayout f)
        {
            foreach (Area a in f.Areas)
                a.Cells.Clear();
            TileGrid g = f.Grid;
            for (int i = 0; i < g.Count; i++)
            {
                int id = g.Area[i];
                if (id >= 0 && id < f.Areas.Count && (g.Type[i] == CellType.Floor || g.Type[i] == CellType.Door))
                    f.Areas[id].Cells.Add(i);
            }
            foreach (Area a in f.Areas)
                a.RecomputeBounds(g);
        }

        /// <summary>Cells allowed for a strategy: inside the footprint and not reserved.</summary>
        public static bool[] FootprintMask(FloorLayout f)
        {
            TileGrid g = f.Grid;
            var mask = new bool[g.Count];
            for (int i = 0; i < g.Count; i++)
                mask[i] = !g.Has(i, CellFlags.Reserved);
            return mask;
        }

        /// <summary>Walkable cell count of a boolean mask.</summary>
        public static int Count(bool[] mask)
        {
            int n = 0;
            foreach (bool b in mask)
                if (b)
                    n++;
            return n;
        }

        /// <summary>Picks a room shape by the settings' weights for a room of the given size.</summary>
        public static RoomShape PickShape(RoomShapeWeights w, int width, int height, bool hall, DungeonRandom rng)
        {
            var weights = new float[12];
            weights[(int)RoomShape.Rectangle] = w.rectangle;
            weights[(int)RoomShape.LShape] = w.lShape;
            weights[(int)RoomShape.TShape] = w.tShape;
            weights[(int)RoomShape.Cross] = w.cross;
            weights[(int)RoomShape.Circle] = w.circle;
            weights[(int)RoomShape.Composite] = w.composite;
            weights[(int)RoomShape.PillaredHall] = w.pillaredHall * (hall ? 3f : 1f);
            weights[(int)RoomShape.Ruined] = w.ruined;
            weights[(int)RoomShape.Octagon] = w.octagon;
            weights[(int)RoomShape.Ring] = w.ring * (hall ? 2f : 1f);
            weights[(int)RoomShape.Apse] = w.apse;
            weights[(int)RoomShape.Diamond] = w.diamond;
            int side = Mathf.Min(width, height);
            for (int s = 0; s < weights.Length; s++)
                if (side < RoomShapes.MinSide((RoomShape)s))
                    weights[s] = 0f;
            int pick = rng.WeightedIndex(weights);
            return pick < 0 ? RoomShape.Rectangle : (RoomShape)pick;
        }

        /// <summary>Labels 4-connected groups of cells where <paramref name="open"/> is true. Returns the group count.</summary>
        public static int Label(bool[] open, int width, int height, int[] labels, List<int> sizes)
        {
            int n = width * height;
            for (int i = 0; i < n; i++)
                labels[i] = -1;
            sizes?.Clear();
            int next = 0;
            var stack = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (!open[s] || labels[s] >= 0)
                    continue;
                int size = 0;
                labels[s] = next;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    size++;
                    int x = c % width, y = c / width;
                    if (x > 0 && open[c - 1] && labels[c - 1] < 0) { labels[c - 1] = next; stack.Push(c - 1); }
                    if (x < width - 1 && open[c + 1] && labels[c + 1] < 0) { labels[c + 1] = next; stack.Push(c + 1); }
                    if (y > 0 && open[c - width] && labels[c - width] < 0) { labels[c - width] = next; stack.Push(c - width); }
                    if (y < height - 1 && open[c + width] && labels[c + width] < 0) { labels[c + width] = next; stack.Push(c + width); }
                }
                sizes?.Add(size);
                next++;
            }
            return next;
        }
    }
}
