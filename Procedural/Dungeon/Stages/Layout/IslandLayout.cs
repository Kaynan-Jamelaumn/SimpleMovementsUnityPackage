using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Islands over a chasm (and, with <c>astral</c>, platforms floating in the void). The whole floor inside a rock rim
    /// becomes chasm; islands (natural rock) or platforms (crisp stone) are carved out of it on a jittered grid, the anchor
    /// rooms get an island of their own, and a ledge may run along the rim. A spanning tree over the islands' centres
    /// (plus a few loops) decides the joins: bridges carved across the chasm, moving platforms riding a straight track
    /// over it, or - on astral floors - portals (a pair of teleport pads). Falling off drops to the floor below (see the
    /// Mechanics settings).
    /// </summary>
    public sealed class IslandLayout : ILayoutStrategy
    {
        private readonly bool astral;

        public IslandLayout(bool astral)
        {
            this.astral = astral;
        }

        private sealed class Island
        {
            public Area Area;
            public Vector2 Center;
        }

        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            IslandSettings isl = ctx.Profile.Islands ?? new IslandSettings();
            AstralSettings ast = ctx.Profile.Astral ?? new AstralSettings();
            TileGrid g = floor.Grid;
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            int seed = ctx.Seed, salt = DungeonRandom.Salt(astral ? "Astral" : "Islands") + floor.Index * 389;
            ZoneStyle style = astral ? ZoneStyle.Built : ZoneStyle.Cavern;
            int rim = astral ? 1 : Mathf.Max(1, isl.rimWidth);

            // 1. Everything carvable inside the rim becomes chasm.
            for (int y = fp.yMin + 1; y < fp.yMax - 1; y++)
                for (int x = fp.xMin + 1; x < fp.xMax - 1; x++)
                {
                    int i = g.Index(x, y);
                    if (g.Type[i] == CellType.Solid && g.IsCarvable(i))
                        g.Set(i, CellFlags.Chasm);
                }

            var islands = new List<Island>();

            // 2. A ledge along the rock rim (islands floors): split into segments around the floor.
            if (!astral && isl.rimWidth > 0)
            {
                var center = new Vector2(fp.x + fp.width * 0.5f, fp.y + fp.height * 0.5f);
                int segments = Mathf.Clamp((fp.width + fp.height) / 14, 4, 10);
                var parts = new List<int>[segments];
                for (int k = 0; k < segments; k++)
                    parts[k] = new List<int>();
                for (int y = fp.yMin + 1; y < fp.yMax - 1; y++)
                    for (int x = fp.xMin + 1; x < fp.xMax - 1; x++)
                    {
                        int edge = Mathf.Min(Mathf.Min(x - fp.xMin, fp.xMax - 1 - x), Mathf.Min(y - fp.yMin, fp.yMax - 1 - y));
                        if (edge > rim)
                            continue;
                        int i = g.Index(x, y);
                        if (!g.IsChasm(i))
                            continue;
                        float a = Mathf.Repeat(Mathf.Atan2(y + 0.5f - center.y, x + 0.5f - center.x), Mathf.PI * 2f);
                        int k = Mathf.Min(segments - 1, Mathf.FloorToInt(a / (Mathf.PI * 2f / segments)));
                        // Gaps between segments (the chasm reaches the rock wall there).
                        float toEdge = Mathf.Min(a - k * Mathf.PI * 2f / segments, (k + 1) * Mathf.PI * 2f / segments - a) *
                                       (new Vector2(x + 0.5f, y + 0.5f) - center).magnitude;
                        if (toEdge < 2.5f)
                            continue;
                        parts[k].Add(i);
                    }
                foreach (List<int> part in parts)
                {
                    if (part.Count < 10 || !rng.Chance(0.7f))
                        continue;
                    Area ledge = LayoutUtil.StampCells(floor, part, AreaKind.Cavern, ZoneStyle.Cavern);
                    if (ledge == null)
                        continue;
                    LayoutUtil.KeepLargestPiece(floor, ledge);
                    ledge.Tag = "Ledge";
                    islands.Add(new Island { Area = ledge, Center = ledge.Center });
                }
            }

            // 3. The anchor rooms stand on islands of their own.
            foreach (Area a in floor.Areas)
            {
                if (!a.Fixed)
                    continue;
                AnchorAreas.SetStyle(floor, a, style);
                var grow = new List<int>();
                foreach (int c in a.Cells)
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int x = g.X(c) + dx, y = g.Y(c) + dy;
                            if (!g.InBounds(x, y) || dx * dx + dy * dy > 5)
                                continue;
                            int i = g.Index(x, y);
                            if (g.IsChasm(i) && !NearOtherArea(floor, g, i, a.Id))
                                grow.Add(i);
                        }
                foreach (int i in grow)
                {
                    if (!g.IsChasm(i))
                        continue;
                    g.Clear(i, CellFlags.Chasm);
                    g.SetFloor(i, a.Id, style == ZoneStyle.Cavern);
                }
                LayoutUtil.RebuildCells(floor, a);
                islands.Add(new Island { Area = a, Center = a.Center });
            }

            // 4. Islands on a jittered grid.
            FloatRange radiusRange = astral ? ast.platformRadius : isl.islandRadius;
            float radius = radiusRange.Lerp(spec.Openness);
            int gap = astral ? ast.gap.Random(rng) : isl.gap.Random(rng);
            float pitch = radius * 2f + gap;
            int n = 0;
            for (float y = fp.yMin + rim + radius + 1f; y <= fp.yMax - rim - radius - 1f; y += pitch)
                for (float x = fp.xMin + rim + radius + 1f; x <= fp.xMax - rim - radius - 1f; x += pitch)
                {
                    var c = new Vector2(x + rng.Range(-gap * 0.35f, gap * 0.35f), y + rng.Range(-gap * 0.35f, gap * 0.35f));
                    float r = radius * rng.Range(0.75f, 1.15f);
                    var cells = new List<int>();
                    bool blocked = false;
                    foreach (int i in LayoutUtil.Disc(g, c, r, astral ? 0f : 0.35f, seed, salt + n * 7 + 1))
                    {
                        if (!g.IsChasm(i))
                            continue;
                        if (NearOtherArea(floor, g, i, -1, 2))
                        {
                            blocked = true;
                            continue;
                        }
                        cells.Add(i);
                    }
                    n++;
                    if (cells.Count < (blocked ? 14 : 10))
                        continue;
                    Area island = LayoutUtil.StampCells(floor, cells, AreaKind.Cavern, style);
                    if (island == null)
                        continue;
                    if (astral)
                        island.Kind = cells.Count >= 70 ? AreaKind.Hall : AreaKind.Room;
                    LayoutUtil.KeepLargestPiece(floor, island);
                    island.Tag = astral ? "Platform" : "Island";
                    islands.Add(new Island { Area = island, Center = island.Center });
                }

            // The biggest island hosts the floor's fight (a boss arena on the last floor).
            Island biggest = null;
            foreach (Island i in islands)
                if (!i.Area.Fixed && (biggest == null || i.Area.Cells.Count > biggest.Area.Cells.Count))
                    biggest = i;
            if (biggest != null)
                biggest.Area.Hint = spec.IsLast ? AreaRole.Boss : AreaRole.Arena;

            // 5. Joins: a spanning tree over the island centres (shortest first), plus a few loops.
            var edges = new List<(int a, int b, float d)>();
            for (int a = 0; a < islands.Count; a++)
                for (int b = a + 1; b < islands.Count; b++)
                    edges.Add((a, b, (islands[a].Center - islands[b].Center).magnitude));
            edges.Sort((p, q) => p.d.CompareTo(q.d));
            var sets = new ProceduralCommon.UnionFind(islands.Count);
            float loops = Mathf.Lerp(0.05f, 0.25f, spec.Openness);
            var joined = new HashSet<long>();
            foreach (var e in edges)
            {
                bool tree = sets.Union(e.a, e.b);
                if (!tree && (e.d > pitch * 1.6f || !rng.Chance(loops)))
                    continue;
                long key = ((long)e.a << 32) | (uint)e.b;
                if (!joined.Add(key))
                    continue;
                Join(ctx, floor, islands[e.a], islands[e.b], rng, isl, ast);
            }
        }

        /// <summary>Decides how two islands are joined and records it as a preset link.</summary>
        private void Join(DungeonContext ctx, FloorLayout floor, Island a, Island b, DungeonRandom rng, IslandSettings isl, AstralSettings ast)
        {
            TileGrid g = floor.Grid;
            float roll = rng.Value();
            float portal = astral ? ast.portalShare : 0f;
            float platform = astral ? ast.platformShare : isl.platformShare;
            if (roll < portal)
            {
                int ca = PadCell(g, a.Area, b.Center), cb = PadCell(g, b.Area, a.Center);
                if (ca >= 0 && cb >= 0)
                {
                    floor.PresetLinks.Add(new PresetLink { A = a.Area.Id, B = b.Area.Id, Kind = ConnectionKind.Portal, CellA = ca, CellB = cb });
                    return;
                }
            }
            else if (roll < portal + platform && TryTrack(g, a.Area, b.Area, out int da, out int db, out List<int> track))
            {
                // Bridges and repairs must not be carved across the platform's track.
                foreach (int c in track)
                    g.Set(c, CellFlags.Reserved);
                floor.PresetLinks.Add(new PresetLink { A = a.Area.Id, B = b.Area.Id, Kind = ConnectionKind.Platform, CellA = da, CellB = db, Track = track });
                return;
            }
            int width = astral ? 1 : Mathf.Max(1, isl.bridgeWidth.Random(rng));
            floor.PresetLinks.Add(new PresetLink { A = a.Area.Id, B = b.Area.Id, Kind = ConnectionKind.Bridge, Width = width });
        }

        /// <summary>A cell for a teleport pad: inside the area, a little towards its partner, away from the edge.</summary>
        private static int PadCell(TileGrid g, Area area, Vector2 toward)
        {
            Vector2 dir = (toward - area.Center).normalized;
            Vector2 target = area.Center + dir * Mathf.Sqrt(area.Cells.Count / Mathf.PI) * 0.45f;
            int best = -1;
            float bestScore = float.MaxValue;
            foreach (int c in area.Cells)
            {
                if (g.Has(c, CellFlags.Landing) || EdgeCell(g, c))
                    continue;
                float d = (g.Center(c) - target).sqrMagnitude;
                if (d < bestScore)
                {
                    bestScore = d;
                    best = c;
                }
            }
            return best;
        }

        private static bool EdgeCell(TileGrid g, int c)
        {
            for (int d = 0; d < 4; d++)
                if (!g.IsWalkable(g.Neighbor(c, d)))
                    return true;
            return false;
        }

        /// <summary>
        /// A straight track across the chasm between the two areas' closest facing edge cells (axis-aligned, so the
        /// platform docks squarely): every cell between must be chasm.
        /// </summary>
        private static bool TryTrack(TileGrid g, Area a, Area b, out int dockA, out int dockB, out List<int> track)
        {
            dockA = dockB = -1;
            track = null;
            var inB = new HashSet<int>(b.Cells);
            int bestLen = int.MaxValue;
            foreach (int c in a.Cells)
            {
                for (int d = 0; d < 4; d++)
                {
                    var cells = new List<int>();
                    int cur = g.Neighbor(c, d);
                    while (cur >= 0 && g.IsChasm(cur) && cells.Count < 24)
                    {
                        cells.Add(cur);
                        cur = g.Neighbor(cur, d);
                    }
                    if (cur < 0 || !inB.Contains(cur) || cells.Count < 2 || cells.Count >= bestLen)
                        continue;
                    // The platform is a cell wide: the cells beside the track must not be other islands' ground.
                    bestLen = cells.Count;
                    dockA = c;
                    dockB = cur;
                    track = cells;
                }
            }
            return track != null;
        }

        /// <summary>An open cell of another area within <paramref name="range"/> cells.</summary>
        private static bool NearOtherArea(FloorLayout floor, TileGrid g, int i, int self, int range = 1)
        {
            int x = g.X(i), y = g.Y(i);
            for (int dy = -range; dy <= range; dy++)
                for (int dx = -range; dx <= range; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!g.InBounds(nx, ny))
                        continue;
                    int n = g.Index(nx, ny);
                    if (g.Type[n] == CellType.Link)
                        return true;
                    if (g.Type[n] != CellType.Solid && g.Area[n] != self)
                        return true;
                }
            return false;
        }
    }
}
