using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Natural cave floors: rock is scattered with a noise-varied density (so some parts open into big caverns and
    /// others stay tight), smoothed by a cellular automaton, cleaned (diagonal-only contacts resolved, one-cell
    /// passages widened, tiny pockets filled), then split into chambers: the widest points (peaks of the distance to
    /// the rock) become chamber centres and every open cell joins the chamber it reaches first. Neighbouring
    /// chambers are already open to each other; separate cave systems get joined by tunnels in the Connectivity stage.
    /// </summary>
    public sealed class CaveLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            foreach (Area a in floor.Areas)
                AnchorAreas.SetStyle(floor, a, ZoneStyle.Cavern);
            CaveField.Run(ctx, floor, rng, LayoutUtil.FootprintMask(floor));
        }
    }

    public static class CaveField
    {
        private const int MinChamberCells = 14;

        /// <summary>Carves caves into the allowed cells and turns them into Cavern areas.</summary>
        public static void Run(DungeonContext ctx, FloorLayout floor, DungeonRandom rng, bool[] allowed)
        {
            CaveSettings cs = ctx.Profile.Caves;
            TileGrid g = floor.Grid;
            int w = g.Width, h = g.Height, n = g.Count;
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            int seed = ctx.Seed;
            int salt = DungeonRandom.Salt("CaveFill") + floor.Index * 7919;

            var open = new bool[n];
            var free = new bool[n];
            var pinnedOpen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                int x = i % w, y = i / w;
                if (g.Type[i] == CellType.Link)
                    continue;
                if (g.Type[i] != CellType.Solid)
                {
                    open[i] = true;
                    pinnedOpen[i] = true;
                    continue;
                }
                bool inside = x > fp.xMin && y > fp.yMin && x < fp.xMax - 1 && y < fp.yMax - 1;
                free[i] = inside && allowed[i] && g.IsCarvable(i);
            }

            // Keep a cell of rock around existing open areas that aren't caves (rooms on hybrid floors).
            for (int i = 0; i < n; i++)
            {
                if (!pinnedOpen[i] || g.Has(i, CellFlags.Organic))
                    continue;
                int x = i % w, y = i / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && !pinnedOpen[nx + ny * w])
                            free[nx + ny * w] = false;
                    }
            }

            // Initial scatter.
            float fill = cs.solidFill.Lerp(1f - spec.Openness);
            for (int i = 0; i < n; i++)
            {
                if (!free[i])
                    continue;
                int x = i % w, y = i / w;
                float noise = PlacementRandom.Noise(x * cs.fillNoiseScale, y * cs.fillNoiseScale, seed, salt, 3);
                float roll = PlacementRandom.Value(seed, salt, x, y, 1);
                open[i] = roll >= fill + (noise - 0.5f) * 2f * cs.fillNoiseStrength;
            }

            // Cellular automaton.
            var next = new bool[n];
            for (int it = 0; it < cs.smoothingIterations; it++)
            {
                System.Array.Copy(open, next, n);
                for (int i = 0; i < n; i++)
                {
                    if (!free[i])
                        continue;
                    int rock = RockNeighbors(open, i, w, h);
                    if (rock >= cs.rockThreshold)
                        next[i] = false;
                    else if (rock <= cs.openThreshold)
                        next[i] = true;
                }
                System.Array.Copy(next, open, n);
            }

            ResolveSaddles(open, free, w, h, rng);
            if (cs.minPassageWidth >= 2)
            {
                Widen(open, free, w, h, rng);
                Widen(open, free, w, h, rng);
                ResolveSaddles(open, free, w, h, rng);
            }

            // Fill pockets that are too small (unless they touch an existing area).
            var labels = new int[n];
            var sizes = new List<int>();
            int groups = LayoutUtil.Label(open, w, h, labels, sizes);
            var keep = new bool[groups];
            for (int i = 0; i < n; i++)
                if (open[i] && pinnedOpen[i])
                    keep[labels[i]] = true;
            for (int gi = 0; gi < groups; gi++)
                if (sizes[gi] >= cs.minRegionCells)
                    keep[gi] = true;
            for (int i = 0; i < n; i++)
                if (open[i] && free[i] && !keep[labels[i]])
                    open[i] = false;

            // Write the caves.
            var cave = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (open[i] && free[i])
                {
                    g.SetFloor(i, -1, true);
                    cave[i] = true;
                }
            }

            MakeChambers(ctx, floor, cave, rng);
        }

        private static int RockNeighbors(bool[] open, int i, int w, int h)
        {
            int x = i % w, y = i / w, rock = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || !open[nx + ny * w])
                        rock++;
                }
            }
            return rock;
        }

        /// <summary>
        /// Two open cells touching only at a corner (with rock on the other diagonal) are ambiguous: the grid says they're
        /// apart, a smooth mesh would join them. Open one of the rock cells so they really connect.
        /// </summary>
        private static void ResolveSaddles(bool[] open, bool[] free, int w, int h, DungeonRandom rng)
        {
            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    int a = x + y * w, b = a + 1, c = a + w, d = c + 1;
                    bool diag1 = open[a] && open[d] && !open[b] && !open[c];
                    bool diag2 = open[b] && open[c] && !open[a] && !open[d];
                    if (!diag1 && !diag2)
                        continue;
                    int first = diag1 ? b : a, second = diag1 ? c : d;
                    if (rng.Chance(0.5f))
                    {
                        int t = first;
                        first = second;
                        second = t;
                    }
                    if (free[first])
                        open[first] = true;
                    else if (free[second])
                        open[second] = true;
                    else if (diag1)
                    {
                        if (free[a]) open[a] = false;
                        else if (free[d]) open[d] = false;
                    }
                    else
                    {
                        if (free[b]) open[b] = false;
                        else if (free[c]) open[c] = false;
                    }
                }
            }
        }

        /// <summary>Opens a neighbour of every open cell squeezed between rock on opposite sides (1-cell passages).</summary>
        private static void Widen(bool[] open, bool[] free, int w, int h, DungeonRandom rng)
        {
            var toOpen = new List<int>();
            for (int y = 1; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    int i = x + y * w;
                    if (!open[i])
                        continue;
                    bool horizontal = !open[i - 1] && !open[i + 1];
                    bool vertical = !open[i - w] && !open[i + w];
                    if (horizontal)
                    {
                        int pick = rng.Chance(0.5f) ? i - 1 : i + 1;
                        int other = pick == i - 1 ? i + 1 : i - 1;
                        if (free[pick]) toOpen.Add(pick);
                        else if (free[other]) toOpen.Add(other);
                    }
                    if (vertical)
                    {
                        int pick = rng.Chance(0.5f) ? i - w : i + w;
                        int other = pick == i - w ? i + w : i - w;
                        if (free[pick]) toOpen.Add(pick);
                        else if (free[other]) toOpen.Add(other);
                    }
                }
            }
            foreach (int i in toOpen)
                open[i] = true;
        }

        /// <summary>Splits the new cave cells into Cavern areas around their widest points.</summary>
        private static void MakeChambers(DungeonContext ctx, FloorLayout floor, bool[] cave, DungeonRandom rng)
        {
            CaveSettings cs = ctx.Profile.Caves;
            TileGrid g = floor.Grid;
            int w = g.Width, h = g.Height, n = g.Count;

            var solid = new bool[n];
            for (int i = 0; i < n; i++)
                solid[i] = !g.IsWalkable(i);
            float[] dist = DistanceField.Compute(solid, w, h);

            // Peaks of the distance field.
            var peaks = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!cave[i] || dist[i] < cs.chamberMinRadius)
                    continue;
                int x = i % w, y = i / w;
                bool peak = true;
                for (int dy = -1; dy <= 1 && peak; dy++)
                    for (int dx = -1; dx <= 1 && peak; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < w && ny < h && dist[nx + ny * w] > dist[i])
                            peak = false;
                    }
                if (peak)
                    peaks.Add(i);
            }
            peaks.Sort((a, b) =>
            {
                int c = dist[b].CompareTo(dist[a]);
                return c != 0 ? c : a.CompareTo(b);
            });

            float spacing = cs.chamberSpacing * Mathf.Lerp(1.3f, 0.8f, floor.Spec.Complexity);
            var centers = new List<int>();
            var hash = new SpatialHash2D<int>(Mathf.Max(2f, spacing));
            foreach (int p in peaks)
            {
                Vector2 pos = g.Center(p);
                float r = Mathf.Max(spacing, dist[p] * 1.4f);
                if (hash.AnyWithin(pos, r))
                    continue;
                centers.Add(p);
                hash.Add(pos, p);
            }

            // Every separate cave pocket needs at least one centre.
            var labels = new int[n];
            var sizes = new List<int>();
            int groups = LayoutUtil.Label(cave, w, h, labels, sizes);
            var covered = new bool[groups];
            foreach (int c in centers)
                covered[labels[c]] = true;
            var best = new int[groups];
            for (int gi = 0; gi < groups; gi++)
                best[gi] = -1;
            for (int i = 0; i < n; i++)
            {
                if (!cave[i])
                    continue;
                int gi = labels[i];
                if (best[gi] < 0 || dist[i] > dist[best[gi]])
                    best[gi] = i;
            }
            for (int gi = 0; gi < groups; gi++)
                if (!covered[gi] && best[gi] >= 0)
                    centers.Add(best[gi]);

            // Geodesic partition: breadth-first from all centres at once.
            var queue = new Queue<int>();
            foreach (int c in centers)
            {
                Area area = floor.AddArea(AreaKind.Cavern, ZoneStyle.Cavern);
                g.Area[c] = area.Id;
                queue.Enqueue(c);
            }
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb < 0 || !cave[nb] || g.Area[nb] >= 0)
                        continue;
                    g.Area[nb] = g.Area[c];
                    queue.Enqueue(nb);
                }
            }

            LayoutUtil.RebuildAreaCells(floor);
            MergeSmallChambers(floor);
            foreach (Area a in floor.Areas)
                if (a.Kind == AreaKind.Cavern && a.Cells.Count > 0)
                    a.Region = labels[a.Cells[0]];
        }

        /// <summary>Folds chambers smaller than a few cells into the neighbour they share the most boundary with.</summary>
        private static void MergeSmallChambers(FloorLayout floor)
        {
            TileGrid g = floor.Grid;
            bool changed = true;
            int guard = 0;
            while (changed && guard++ < 8)
            {
                changed = false;
                foreach (Area a in floor.Areas)
                {
                    if (a.Kind != AreaKind.Cavern || a.Fixed || a.Cells.Count == 0 || a.Cells.Count >= MinChamberCells)
                        continue;
                    var shared = new Dictionary<int, int>();
                    foreach (int c in a.Cells)
                    {
                        for (int d = 0; d < 4; d++)
                        {
                            int nb = g.Neighbor(c, d);
                            if (nb < 0)
                                continue;
                            int other = g.Area[nb];
                            if (other < 0 || other == a.Id || floor.Areas[other].Kind != AreaKind.Cavern || floor.Areas[other].Fixed)
                                continue;
                            shared.TryGetValue(other, out int count);
                            shared[other] = count + 1;
                        }
                    }
                    int target = -1, most = 0;
                    foreach (var kv in shared)
                        if (kv.Value > most || (kv.Value == most && kv.Key < target))
                        {
                            most = kv.Value;
                            target = kv.Key;
                        }
                    if (target < 0)
                        continue;
                    Area into = floor.Areas[target];
                    foreach (int c in a.Cells)
                    {
                        g.Area[c] = target;
                        into.Cells.Add(c);
                    }
                    a.Cells.Clear();
                    into.RecomputeBounds(g);
                    changed = true;
                }
            }
        }
    }
}
