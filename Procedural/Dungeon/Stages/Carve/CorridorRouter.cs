using System;
using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Routes and carves one connection with A* over the floor grid. Corridors prefer straight lines (turn penalty),
    /// merge into existing corridors (cheap to reuse), keep clear of other areas and shafts (a corridor may only open
    /// into the two areas it joins), enter template rooms only through their sockets, and get doors at their ends.
    /// Tunnels wander (noise-weighted costs) and vary in width. One router per floor (it owns its search buffers).
    /// </summary>
    public sealed class CorridorRouter
    {
        private readonly DungeonContext ctx;
        private readonly FloorLayout floor;
        private readonly TileGrid g;
        private readonly GridPathfinder finder;
        private readonly ConnectionSettings cs;
        private readonly int seed;
        private readonly int noiseSalt;
        private readonly List<int> path = new List<int>();

        private const float WallHugPenalty = 3f;

        public CorridorRouter(DungeonContext ctx, FloorLayout floor)
        {
            this.ctx = ctx;
            this.floor = floor;
            g = floor.Grid;
            finder = new GridPathfinder(g.Width, g.Height);
            cs = ctx.Profile.Connections;
            seed = ctx.Seed;
            noiseSalt = DungeonRandom.Salt("TunnelNoise") + floor.Index * 31;
        }

        /// <summary>Per-search description of the two ends.</summary>
        private sealed class Ends
        {
            public int A, B;
            public HashSet<int> Targets;
            /// <summary>For socketed (template) targets: the direction the last step must take into the socket.</summary>
            public Dictionary<int, int> TargetDirection;
            /// <summary>Outside cells allowed next to a socketed area (the cells in front of its sockets); null = any.</summary>
            public HashSet<int> OutsideA, OutsideB;
            public RectInt TargetBounds;
            public bool Natural;
            /// <summary>Repairs: any reached cell is a target.</summary>
            public Func<int, bool> TargetPredicate;
        }

        public bool Route(Connection c)
        {
            Area a = floor.Areas[c.A], b = floor.Areas[c.B];
            var ends = new Ends
            {
                A = a.Id,
                B = b.Id,
                Natural = c.Kind == ConnectionKind.Tunnel || c.Kind == ConnectionKind.Breach,
                TargetBounds = b.Bounds,
            };
            List<int> sources = EntryCells(a, out ends.OutsideA, out _);
            List<int> targets = EntryCells(b, out ends.OutsideB, out ends.TargetDirection);
            ends.Targets = new HashSet<int>(targets);
            if (sources.Count == 0 || targets.Count == 0)
            {
                c.Failed = true;
                return false;
            }

            float turn = ends.Natural ? 0.15f : cs.turnPenalty;
            float minStep = ends.Natural ? 0.3f : Mathf.Min(1f, cs.reuseCost);
            bool found = finder.FindPath(sources, cell => ends.Targets.Contains(cell),
                cell => minStep * DistanceToRect(cell, ends.TargetBounds),
                (from, to, dir) => StepCost(from, to, dir, ends), turn, path, 400000);
            if (!found)
            {
                c.Failed = true;
                return false;
            }
            Carve(c, path, ends);
            return true;
        }

        /// <summary>
        /// Repairs an unreachable area: carves a connection from it to the nearest cell accepted by
        /// <paramref name="reached"/>. Returns the new connection or null.
        /// </summary>
        public Connection RouteToReached(Area area, Func<int, bool> reached)
        {
            var ends = new Ends
            {
                A = area.Id,
                B = -1,
                Natural = area.Style == ZoneStyle.Cavern,
                TargetPredicate = reached,
            };
            List<int> sources = EntryCells(area, out ends.OutsideA, out _);
            if (sources.Count == 0)
                return null;
            bool found = finder.FindPath(sources, cell => g.Area[cell] != area.Id && reached(cell), null,
                (from, to, dir) => StepCost(from, to, dir, ends), ends.Natural ? 0.15f : cs.turnPenalty, path, 400000);
            if (!found)
                return null;

            int end = path[path.Count - 1];
            int other = g.Area[end];
            if (other < 0)
            {
                // Reached a corridor: join the area on one of that corridor's ends.
                int conn = g.Connection[end];
                other = conn >= 0 ? floor.Connections[conn].A : -1;
            }
            if (other < 0 || other == area.Id)
                other = floor.ArrivalArea;
            ConnectionKind kind = ends.Natural || floor.Areas[other].Style == ZoneStyle.Cavern ? (ends.Natural && floor.Areas[other].Style == ZoneStyle.Cavern ? ConnectionKind.Tunnel : ConnectionKind.Breach) : ConnectionKind.Corridor;
            Connection c = floor.AddConnection(area.Id, other, kind);
            c.IsRepair = true;
            c.Width = 1;
            ends.B = other;
            Carve(c, path, ends);
            return c;
        }

        // ------------------------------------------------------------------ costs

        /// <summary>Manhattan distance (cells) from a cell to a rectangle (0 inside).</summary>
        private float DistanceToRect(int cell, RectInt r)
        {
            int x = cell % g.Width, y = cell / g.Width;
            int dx = x < r.xMin ? r.xMin - x : (x >= r.xMax ? x - r.xMax + 1 : 0);
            int dy = y < r.yMin ? r.yMin - y : (y >= r.yMax ? y - r.yMax + 1 : 0);
            return dx + dy;
        }

        private float StepCost(int from, int to, int dir, Ends e)
        {
            int ta = g.Area[to];
            if (ta >= 0)
            {
                if (ta == e.A)
                    return 0f;
                if (e.Targets != null && e.Targets.Contains(to))
                {
                    if (e.TargetDirection != null && e.TargetDirection.TryGetValue(to, out int needed) && needed != dir)
                        return -1f;
                    return 1f;
                }
                if (e.TargetPredicate != null)
                    return 1f;   // repairs: reached areas end the search, unreached ones are walked through
                if (e.Natural && floor.Areas[ta].Style == ZoneStyle.Cavern)
                    return 0.8f; // a tunnel may run through another chamber (it just joins it too)
                return -1f;
            }

            switch (g.Type[to])
            {
                case CellType.Link:
                    return -1f;
                case CellType.Floor:
                case CellType.Door:
                    // An existing corridor or tunnel: walking it carves nothing.
                    if (e.TargetPredicate != null && e.TargetPredicate(to))
                        return 1f;
                    return e.Natural ? 0.6f : cs.reuseCost;
            }

            if (!g.IsCarvable(to))
                return -1f;

            int fromArea = g.Area[from];
            bool leaving = fromArea >= 0 && (fromArea == e.A || fromArea == e.B);
            bool hug = false;
            float extra = 0f;
            for (int d = 0; d < 4; d++)
            {
                int nb = g.Neighbor(to, d);
                if (nb < 0)
                    continue;
                if (g.Type[nb] == CellType.Link)
                    return -1f;
                int na = g.Area[nb];
                if (na < 0)
                    continue;
                if (na == e.A)
                {
                    if (e.OutsideA != null && !e.OutsideA.Contains(to))
                        return -1f;
                    hug = true;
                }
                else if (na == e.B)
                {
                    if (e.OutsideB != null && !e.OutsideB.Contains(to))
                        return -1f;
                    hug = true;
                }
                else if (e.TargetPredicate != null)
                {
                    // Repairs are the last resort: they may open into anything.
                }
                else if (e.Natural && floor.Areas[na].Style == ZoneStyle.Cavern)
                {
                    // Underground, a tunnel brushing past another chamber just makes one more natural opening
                    // (recorded as a connection after carving) - mildly discouraged.
                    extra += 0.75f;
                }
                else
                {
                    // Carving here would open into an unrelated room.
                    return -1f;
                }
            }

            float cost = 1f + extra;
            if (hug && !leaving)
                cost += WallHugPenalty;
            if (e.Natural)
            {
                int x = to % g.Width, y = to / g.Width;
                float noise = PlacementRandom.Noise(x * 0.14f, y * 0.14f, seed, noiseSalt, 2);
                cost *= 0.3f + noise * noise * 2.4f * ctx.Profile.Caves.tunnelWinding;
            }
            return cost;
        }

        /// <summary>
        /// Cells a route may start from / end in. Plain areas: every cell. Template areas: their socket cells only,
        /// entered straight through (and only the cells in front of the sockets may be carved next to the area).
        /// </summary>
        private List<int> EntryCells(Area area, out HashSet<int> outside, out Dictionary<int, int> direction)
        {
            outside = null;
            direction = null;
            if (area.TemplateIndex >= 0 && area.TemplateApplied)
            {
                TemplateInfo t = ctx.Profile.Templates[area.TemplateIndex];
                var list = new List<int>();
                outside = new HashSet<int>();
                direction = new Dictionary<int, int>();
                foreach (Vector2Int s in TemplateStamp.SocketCells(floor, area, t))
                {
                    int front = g.Neighbor(s.x, s.y);
                    if (front < 0)
                        continue;
                    list.Add(s.x);
                    outside.Add(front);
                    direction[s.x] = (s.y + 2) & 3;   // stepping into the socket from the front cell
                }
                return list;
            }
            return new List<int>(area.Cells);
        }

        // ------------------------------------------------------------------ carving

        private void Carve(Connection c, List<int> route, Ends e)
        {
            var outsideCells = new List<int>();
            foreach (int cell in route)
            {
                int a = g.Area[cell];
                if (a >= 0 && (a == e.A || a == e.B))
                    continue;
                if (a >= 0)
                {
                    // Another area on the way (a chamber a tunnel runs through, or what a repair walks through).
                    if (e.TargetPredicate != null && e.TargetPredicate(cell))
                        break;
                    continue;
                }
                outsideCells.Add(cell);
                if (e.TargetPredicate != null && e.TargetPredicate(cell) && g.IsWalkable(cell))
                    break;
            }

            c.Routed = true;
            c.Cells.Clear();
            if (outsideCells.Count == 0)
            {
                c.Kind = ConnectionKind.Opening;
                return;
            }

            bool natural = c.Kind == ConnectionKind.Tunnel || c.Kind == ConnectionKind.Breach;
            foreach (int cell in outsideCells)
            {
                if (g.Type[cell] == CellType.Solid)
                    Open(cell, c, natural);
                c.Cells.Add(cell);
            }

            if (c.Kind == ConnectionKind.Breach)
            {
                // Rubble where the rough passage meets built structure.
                for (int k = 0; k < outsideCells.Count; k++)
                {
                    int cell = outsideCells[k];
                    for (int d = 0; d < 4; d++)
                    {
                        int nb = g.Neighbor(cell, d);
                        if (nb >= 0 && g.Area[nb] >= 0 && floor.Areas[g.Area[nb]].Style != ZoneStyle.Cavern)
                            g.Set(cell, CellFlags.Rubble);
                    }
                }
            }

            // Doors at built ends of constructed corridors.
            bool constructed = c.Kind == ConnectionKind.Corridor || c.Kind == ConnectionKind.Door || c.Kind == ConnectionKind.Secret;
            if (constructed)
            {
                if (outsideCells.Count == 1)
                    c.Kind = c.Kind == ConnectionKind.Secret ? ConnectionKind.Secret : ConnectionKind.Door;
                int first = outsideCells[0], last = outsideCells[outsideCells.Count - 1];
                if (e.A >= 0 && IsDoorable(floor.Areas[e.A], first))
                {
                    MakeDoor(first);
                    c.DoorA = first;
                }
                if (e.B >= 0 && IsDoorable(floor.Areas[e.B], last))
                {
                    MakeDoor(last);
                    c.DoorB = last;
                }
                if (c.Kind == ConnectionKind.Secret)
                {
                    // Hide the end that opens into the ordinary area.
                    bool bIsSecret = e.B >= 0 && floor.Areas[e.B].Role == AreaRole.Secret;
                    int hidden = bIsSecret ? c.DoorA : (c.DoorB >= 0 ? c.DoorB : c.DoorA);
                    if (hidden < 0)
                        hidden = bIsSecret ? first : last;
                    MakeDoor(hidden);
                    g.Set(hidden, CellFlags.Secret);
                }
            }

            // Width.
            if (c.Width >= 2 && outsideCells.Count >= 3)
            {
                for (int k = 1; k < outsideCells.Count - 1; k++)
                {
                    int cell = outsideCells[k];
                    int nextCell = outsideCells[k + 1];
                    int dx = g.X(nextCell) - g.X(cell), dy = g.Y(nextCell) - g.Y(cell);
                    Dir4 forward = Dir4Util.FromDelta(dx, dy);
                    int width = c.Width;
                    if (natural)
                    {
                        float n = PlacementRandom.Noise(g.X(cell) * 0.2f, g.Y(cell) * 0.2f, seed, noiseSalt + 7, 1);
                        width = Mathf.Clamp(Mathf.RoundToInt(c.Width - 0.6f + n * 1.4f), 1, c.Width + 1);
                    }
                    if (width >= 2)
                        TryWiden(g.Neighbor(cell, (int)forward.Right()), c, e, natural);
                    if (width >= 3)
                        TryWiden(g.Neighbor(cell, (int)forward.Left()), c, e, natural);
                }
            }
        }

        private bool IsDoorable(Area area, int cell)
        {
            if (area.Style == ZoneStyle.Cavern)
                return false;
            if (area.TemplateIndex >= 0 && area.TemplateApplied && ctx.Profile.Templates[area.TemplateIndex].IsPrefab)
                return false;   // the prefab brings its own doorway
            return g.Type[cell] == CellType.Floor;
        }

        private void MakeDoor(int cell)
        {
            if (g.Type[cell] == CellType.Floor)
                g.Type[cell] = CellType.Door;
        }

        private void Open(int cell, Connection c, bool natural)
        {
            g.SetFloor(cell, -1, natural);
            g.Connection[cell] = c.Id;
            g.Set(cell, CellFlags.Corridor);
        }

        private void TryWiden(int cell, Connection c, Ends e, bool natural)
        {
            if (cell < 0 || g.Type[cell] != CellType.Solid || !g.IsCarvable(cell))
                return;
            for (int d = 0; d < 4; d++)
            {
                int nb = g.Neighbor(cell, d);
                if (nb < 0)
                    continue;
                if (g.Type[nb] == CellType.Link || g.Type[nb] == CellType.Door)
                    return;
                int na = g.Area[nb];
                if (na >= 0 && na != e.A && na != e.B)
                    return;
                if (na >= 0 && ((na == e.A && e.OutsideA != null) || (na == e.B && e.OutsideB != null)))
                    return;
            }
            Open(cell, c, natural);
            c.Cells.Add(cell);
        }
    }
}
