using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Stage 3. Decides which areas of each floor connect (it doesn't carve yet):
    /// <list type="number">
    /// <item>Forced connections: maze edges from the layout, and caverns that are already open to each other.</item>
    /// <item>Candidates: the Gabriel graph of the area centres (no long skinny edges, always connected).</item>
    /// <item>Spanning tree (Kruskal on estimated corridor length): guarantees every area is connected.</item>
    /// <item>Loops: extra candidates added with a chance scaled by Openness - preferring ones that shortcut a long
    /// walk, so they create real alternative routes.</item>
    /// <item>Dead-end budget: if too many areas are dead ends for the floor's Complexity, some get an extra link.</item>
    /// </list>
    /// Then each connection gets a kind (door, corridor, tunnel, breach, secret) and width from the styles it joins.
    /// </summary>
    public sealed class ConnectivityStage : IDungeonStage
    {
        public string Name => "Connectivity";

        public void Run(DungeonContext ctx)
        {
            ctx.ForEachFloor(floor => Connect(ctx, floor, ctx.Random("Connectivity", floor.Index)));
        }

        private struct Candidate
        {
            public int A, B;
            public float Cost;
        }

        private static void Connect(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            ConnectionSettings cs = ctx.Profile.Connections;
            FloorSpec spec = floor.Spec;
            TileGrid g = floor.Grid;
            int n = floor.Areas.Count;
            if (n < 2)
                return;

            var sets = new UnionFind(n);

            // 1. Forced: layout presets.
            foreach (Vector2Int pr in floor.PresetConnections)
            {
                if (pr.x == pr.y || floor.FindConnection(pr.x, pr.y) != null)
                    continue;
                Connection c = floor.AddConnection(pr.x, pr.y, ConnectionKind.Corridor);
                c.Forced = true;
                c.InTree = sets.Union(pr.x, pr.y);
                c.IsLoop = !c.InTree;
                c.EstimatedCost = Vector2.Distance(floor.Areas[pr.x].Center, floor.Areas[pr.y].Center);
            }

            // 1a. Forced: links of a set kind from the layout (bridges, portal pads, moving platforms).
            foreach (PresetLink pl in floor.PresetLinks)
            {
                if (pl.A == pl.B || floor.FindConnection(pl.A, pl.B) != null)
                    continue;
                Connection c = floor.AddConnection(pl.A, pl.B, pl.Kind);
                c.Forced = true;
                c.InTree = sets.Union(pl.A, pl.B);
                c.IsLoop = !c.InTree;
                c.EstimatedCost = Vector2.Distance(floor.Areas[pl.A].Center, floor.Areas[pl.B].Center);
                c.Width = Mathf.Max(1, pl.Width);
                if (pl.Kind.IsJump())
                {
                    c.Routed = true;
                    c.DoorA = pl.CellA;
                    c.DoorB = pl.CellB;
                    if (pl.Track != null)
                        c.Track.AddRange(pl.Track);
                }
            }

            // 1b. Forced: areas already touching (neighbouring cavern chambers).
            var touching = new Dictionary<long, int>();
            for (int i = 0; i < g.Count; i++)
            {
                int a = g.Area[i];
                if (a < 0 || !g.IsWalkable(i))
                    continue;
                for (int d = 0; d < 2; d++)
                {
                    int nb = g.Neighbor(i, d);   // north and east are enough to see every pair once
                    if (nb < 0 || !g.IsWalkable(nb))
                        continue;
                    int b = g.Area[nb];
                    if (b < 0 || b == a)
                        continue;
                    long key = PairKey(a, b);
                    touching.TryGetValue(key, out int count);
                    touching[key] = count + 1;
                }
            }
            var touchingKeys = new List<long>(touching.Keys);
            touchingKeys.Sort();
            foreach (long key in touchingKeys)
            {
                int a = (int)(key >> 32), b = (int)(key & 0xFFFFFFFF);
                if (floor.FindConnection(a, b) != null)
                    continue;
                Connection c = floor.AddConnection(a, b, ConnectionKind.Opening);
                c.Forced = true;
                c.Routed = true;
                c.InTree = sets.Union(a, b);
                c.IsLoop = !c.InTree;
                c.Width = Mathf.Max(1, touching[key]);
            }

            // 2. Candidates from the Gabriel graph of the area centres.
            var centers = new List<Vector2>(n);
            var radius = new float[n];
            foreach (Area a in floor.Areas)
            {
                centers.Add(a.Center);
                radius[a.Id] = Mathf.Sqrt(a.Cells.Count / Mathf.PI);
            }
            List<GraphBuilders.Edge> gabriel = GraphBuilders.Gabriel(centers);
            var candidates = new List<Candidate>();
            foreach (GraphBuilders.Edge e in gabriel)
            {
                if (floor.FindConnection(e.A, e.B) != null)
                    continue;
                float cost = Mathf.Max(1f, e.Length - radius[e.A] - radius[e.B]);
                candidates.Add(new Candidate { A = e.A, B = e.B, Cost = cost });
            }
            candidates.Sort((x, y) =>
            {
                int c = x.Cost.CompareTo(y.Cost);
                if (c != 0) return c;
                c = x.A.CompareTo(y.A);
                return c != 0 ? c : x.B.CompareTo(y.B);
            });

            var used = new bool[candidates.Count];

            // 3. Spanning tree: short candidates first, long ones only if still needed.
            for (int pass = 0; pass < 2 && sets.Sets > 1; pass++)
            {
                for (int i = 0; i < candidates.Count && sets.Sets > 1; i++)
                {
                    Candidate cand = candidates[i];
                    if (used[i] || (pass == 0 && cand.Cost > cs.maxConnectionLength))
                        continue;
                    if (!sets.Union(cand.A, cand.B))
                        continue;
                    used[i] = true;
                    Connection c = floor.AddConnection(cand.A, cand.B, ConnectionKind.Corridor);
                    c.InTree = true;
                    c.EstimatedCost = cand.Cost;
                }
            }
            if (sets.Sets > 1)
            {
                // Should not happen (the Gabriel graph is connected); join components by nearest centres.
                var edges = new List<GraphBuilders.Edge>();
                foreach (Connection c in floor.Connections)
                    edges.Add(new GraphBuilders.Edge { A = c.A, B = c.B });
                int before = edges.Count;
                GraphBuilders.ConnectComponents(centers, edges);
                for (int i = before; i < edges.Count; i++)
                {
                    Connection c = floor.AddConnection(edges[i].A, edges[i].B, ConnectionKind.Corridor);
                    c.InTree = true;
                    c.EstimatedCost = edges[i].Length;
                    sets.Union(edges[i].A, edges[i].B);
                }
            }

            // 4. Loops that shortcut long walks (an undercity's street grid has its own).
            bool grid = spec.Style == FloorStyle.Undercity;
            float loopChance = grid ? 0f : cs.loopChance.Lerp(spec.Openness);
            var order = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
                if (!used[i])
                    order.Add(i);
            rng.Shuffle(order);
            foreach (int i in order)
            {
                Candidate cand = candidates[i];
                if (cand.Cost > cs.maxConnectionLength)
                    continue;
                int hops = Hops(floor, cand.A, cand.B, 12);
                if (hops < 3)
                    continue;
                float value = Mathf.Clamp01((hops - 1) / 4f);
                if (!rng.Chance(loopChance * value))
                    continue;
                used[i] = true;
                Connection c = floor.AddConnection(cand.A, cand.B, ConnectionKind.Corridor);
                c.IsLoop = true;
                c.EstimatedCost = cand.Cost;
            }

            // 5. Dead-end budget.
            int budget = Mathf.RoundToInt(n * cs.deadEndShare.Lerp(spec.Complexity) * Mathf.Lerp(1.2f, 0.7f, spec.Openness));
            var leaves = new List<int>();
            for (int a = 0; a < n; a++)
                if (floor.Degree(a) <= 1 && floor.Areas[a].Role != AreaRole.Entrance && floor.Areas[a].Role != AreaRole.Exit)
                    leaves.Add(a);
            rng.Shuffle(leaves);
            int excess = grid ? 0 : leaves.Count - budget;
            foreach (int leaf in leaves)
            {
                if (excess <= 0)
                    break;
                if (floor.Degree(leaf) > 1)
                    continue;
                int best = -1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (used[i] || (candidates[i].A != leaf && candidates[i].B != leaf))
                        continue;
                    if (candidates[i].Cost > cs.maxConnectionLength * 1.2f)
                        continue;
                    best = i;
                    break;   // sorted by cost: the first is the cheapest
                }
                if (best < 0)
                    continue;
                used[best] = true;
                Connection c = floor.AddConnection(candidates[best].A, candidates[best].B, ConnectionKind.Corridor);
                c.IsLoop = true;
                c.EstimatedCost = candidates[best].Cost;
                excess--;
            }

            // 6. Kinds and widths. Over a chasm everything walked is a bridge.
            bool chasm = spec.Style.HasChasm();
            IntRange bridgeWidth = ctx.Profile.Islands != null ? ctx.Profile.Islands.bridgeWidth : new IntRange(1, 2);
            foreach (Connection c in floor.Connections)
            {
                if (c.Kind == ConnectionKind.Opening || c.Kind.IsJump())
                    continue;
                if (c.Kind == ConnectionKind.Bridge || chasm)
                {
                    if (c.Kind != ConnectionKind.Bridge)
                        c.Width = spec.Style == FloorStyle.Astral ? 1 : Mathf.Max(1, bridgeWidth.Random(rng));
                    c.Kind = ConnectionKind.Bridge;
                    continue;
                }
                Area a = floor.Areas[c.A], b = floor.Areas[c.B];
                bool naturalA = a.Style == ZoneStyle.Cavern, naturalB = b.Style == ZoneStyle.Cavern;
                if (naturalA && naturalB)
                {
                    c.Kind = ConnectionKind.Tunnel;
                    c.Width = ctx.Profile.Caves.tunnelWidth.Random(rng);
                }
                else if (naturalA || naturalB)
                {
                    c.Kind = ConnectionKind.Breach;
                    c.Width = Mathf.Max(2, ctx.Profile.Caves.tunnelWidth.Random(rng) - 1);
                }
                else
                {
                    c.Kind = ConnectionKind.Corridor;
                    bool wide = rng.Chance(cs.wideCorridorChance * spec.Openness * 2f);
                    c.Width = wide ? Mathf.Max(cs.corridorWidth.min, cs.corridorWidth.max) : Mathf.Max(1, cs.corridorWidth.min);
                    if (c.IsLoop && !c.Forced && rng.Chance(cs.secretChance))
                    {
                        c.Kind = ConnectionKind.Secret;
                        c.Width = 1;
                    }
                }
            }
        }

        private static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>Hops between two areas over the connections so far (int.MaxValue beyond <paramref name="limit"/>).</summary>
        public static int Hops(FloorLayout floor, int from, int to, int limit)
        {
            if (from == to)
                return 0;
            var dist = new Dictionary<int, int> { { from, 0 } };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int a = queue.Dequeue();
                int d = dist[a];
                if (d >= limit)
                    continue;
                foreach (int id in floor.Areas[a].Connections)
                {
                    Connection c = floor.Connections[id];
                    if (c.Failed)
                        continue;
                    int b = c.Other(a);
                    if (dist.ContainsKey(b))
                        continue;
                    if (b == to)
                        return d + 1;
                    dist[b] = d + 1;
                    queue.Enqueue(b);
                }
            }
            return int.MaxValue;
        }
    }
}
