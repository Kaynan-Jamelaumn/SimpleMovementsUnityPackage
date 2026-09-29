using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Stage 4. Looks at the whole dungeon as one graph (areas joined by connections, stairs and one-way drops) and
    /// works out its structure: the main path from the entrance to the exit, how deep and how far along that path
    /// every area is, which areas are dead ends and hubs. Then it gives areas their roles by the profile's rules -
    /// required roles first, each placed in the area that best fits its constraints (floor, progress, dead end /
    /// main path / hub, size, style) - and assigns templates for roles that have them.
    /// </summary>
    public sealed class RolesStage : IDungeonStage
    {
        public string Name => "Roles and main path";

        private sealed class Graph
        {
            public readonly List<AreaRef> Nodes = new List<AreaRef>();
            public readonly Dictionary<long, int> Index = new Dictionary<long, int>();
            public readonly List<List<(int to, float cost, int conn, int link)>> Edges = new List<List<(int, float, int, int)>>();

            public int Id(int floor, int area) => Index[((long)floor << 32) | (uint)area];
        }

        public void Run(DungeonContext ctx)
        {
            DungeonLayout layout = ctx.Layout;
            Graph graph = BuildGraph(layout);

            // Entrance and exit.
            FloorLayout first = layout.Floors[0], last = layout.Floors[layout.Floors.Count - 1];
            layout.Entrance = new AreaRef(0, first.ArrivalArea);
            layout.Exit = new AreaRef(last.Index, last.DepartureArea);
            if (!layout.Entrance.IsValid || !layout.Exit.IsValid)
                ctx.Fail("Entrance or exit area missing.");

            int start = graph.Id(0, layout.Entrance.Area);
            int goal = graph.Id(last.Index, layout.Exit.Area);

            // Depth (hops from the entrance, following one-way drops only downwards).
            var depth = Bfs(graph, start, true);
            for (int i = 0; i < graph.Nodes.Count; i++)
                layout.GetArea(graph.Nodes[i]).Depth = depth[i];

            // Main path: cheapest walk entrance -> exit (drops allowed but discouraged).
            List<int> path = ShortestPath(graph, start, goal);
            if (path == null)
                ctx.Fail("The exit can't be reached from the entrance.");
            layout.MainPath.Clear();
            for (int k = 0; k < path.Count; k++)
            {
                AreaRef r = graph.Nodes[path[k]];
                layout.MainPath.Add(r);
                Area a = layout.GetArea(r);
                a.OnMainPath = true;
                a.Progress = path.Count > 1 ? k / (float)(path.Count - 1) : 0f;
                if (k > 0)
                {
                    AreaRef prev = graph.Nodes[path[k - 1]];
                    if (prev.Floor == r.Floor)
                    {
                        Connection c = layout.Floors[r.Floor].FindConnection(prev.Area, r.Area);
                        if (c != null)
                            c.OnMainPath = true;
                    }
                    else
                    {
                        foreach (VerticalLink link in layout.Links)
                            if (link.UpperFloor == prev.Floor && link.UpperArea == prev.Area && link.LowerArea == r.Area)
                                link.OnMainPath = true;
                    }
                }
            }

            // Branches: progress and depth of the main-path area they hang from.
            var branch = new int[graph.Nodes.Count];
            var origin = new int[graph.Nodes.Count];
            for (int i = 0; i < branch.Length; i++)
            {
                branch[i] = -1;
                origin[i] = -1;
            }
            var queue = new Queue<int>();
            foreach (int p in path)
            {
                branch[p] = 0;
                origin[p] = p;
                queue.Enqueue(p);
            }
            while (queue.Count > 0)
            {
                int a = queue.Dequeue();
                foreach (var e in graph.Edges[a])
                {
                    if (branch[e.to] >= 0)
                        continue;
                    branch[e.to] = branch[a] + 1;
                    origin[e.to] = origin[a];
                    queue.Enqueue(e.to);
                }
            }
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                Area a = layout.GetArea(graph.Nodes[i]);
                a.BranchDepth = Mathf.Max(0, branch[i]);
                if (!a.OnMainPath && origin[i] >= 0)
                    a.Progress = layout.GetArea(graph.Nodes[origin[i]]).Progress;
                int degree = graph.Edges[i].Count;
                a.IsLeaf = degree <= 1;
                a.IsHub = degree >= 3;
                a.Difficulty = layout.Floors[a.Floor].Spec.Difficulty * (0.75f + 0.5f * a.Progress);
            }

            AssignRoles(ctx, graph);
        }

        private static Graph BuildGraph(DungeonLayout layout)
        {
            var g = new Graph();
            foreach (FloorLayout f in layout.Floors)
            {
                foreach (Area a in f.Areas)
                {
                    g.Index[((long)f.Index << 32) | (uint)a.Id] = g.Nodes.Count;
                    g.Nodes.Add(new AreaRef(f.Index, a.Id));
                    g.Edges.Add(new List<(int, float, int, int)>());
                }
            }
            foreach (FloorLayout f in layout.Floors)
            {
                foreach (Connection c in f.Connections)
                {
                    if (c.Failed)
                        continue;
                    int a = g.Id(f.Index, c.A), b = g.Id(f.Index, c.B);
                    float cost = 1f + c.EstimatedCost * 0.05f;
                    g.Edges[a].Add((b, cost, c.Id, -1));
                    g.Edges[b].Add((a, cost, c.Id, -1));
                }
            }
            foreach (VerticalLink link in layout.Links)
            {
                int up = g.Id(link.UpperFloor, link.UpperArea), down = g.Id(link.LowerFloor, link.LowerArea);
                if (link.Kind == LinkKind.Drop)
                {
                    g.Edges[up].Add((down, 4f, -1, link.Id));
                }
                else
                {
                    g.Edges[up].Add((down, 1f, -1, link.Id));
                    g.Edges[down].Add((up, 1f, -1, link.Id));
                }
            }
            return g;
        }

        private static int[] Bfs(Graph g, int start, bool directed)
        {
            var dist = new int[g.Nodes.Count];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = -1;
            dist[start] = 0;
            var q = new Queue<int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                int a = q.Dequeue();
                foreach (var e in g.Edges[a])
                {
                    if (dist[e.to] >= 0)
                        continue;
                    dist[e.to] = dist[a] + 1;
                    q.Enqueue(e.to);
                }
            }
            return dist;
        }

        private static List<int> ShortestPath(Graph g, int start, int goal)
        {
            int n = g.Nodes.Count;
            var dist = new float[n];
            var prev = new int[n];
            for (int i = 0; i < n; i++)
            {
                dist[i] = float.MaxValue;
                prev[i] = -1;
            }
            dist[start] = 0f;
            var heap = new BinaryHeap<int>();
            heap.Push(start, 0f);
            while (heap.Count > 0)
            {
                int a = heap.Pop(out float d);
                if (d > dist[a])
                    continue;
                if (a == goal)
                    break;
                foreach (var e in g.Edges[a])
                {
                    float nd = d + e.cost;
                    if (nd < dist[e.to])
                    {
                        dist[e.to] = nd;
                        prev[e.to] = a;
                        heap.Push(e.to, nd);
                    }
                }
            }
            if (dist[goal] == float.MaxValue)
                return null;
            var path = new List<int>();
            for (int v = goal; v >= 0; v = prev[v])
                path.Add(v);
            path.Reverse();
            return path;
        }

        // ------------------------------------------------------------------ roles

        private static void AssignRoles(DungeonContext ctx, Graph graph)
        {
            DungeonLayout layout = ctx.Layout;
            CompiledProfile p = ctx.Profile;
            DungeonRandom rng = ctx.Random("Roles");
            int lastFloor = layout.Floors.Count - 1;

            var rules = new List<RoleRuleInfo>(p.Roles);
            // Required rules first (stable otherwise).
            rules.Sort((a, b) => (b.Required ? 1 : 0).CompareTo(a.Required ? 1 : 0));

            foreach (RoleRuleInfo rule in rules)
            {
                int total = 0;
                for (int f = 0; f < layout.Floors.Count; f++)
                {
                    if (rule.LastFloorOnly && f != lastFloor)
                        continue;
                    if (f < rule.MinFloor || (rule.MaxFloor >= 0 && f > rule.MaxFloor))
                        continue;
                    if (rule.MaxTotal >= 0 && total >= rule.MaxTotal)
                        break;

                    int count = rule.PerFloor.Random(rng);
                    if (rule.Required)
                        count = Mathf.Max(1, count);
                    else if (!rng.Chance(rule.Chance))
                        count = 0;
                    if (rule.MaxTotal >= 0)
                        count = Mathf.Min(count, rule.MaxTotal - total);

                    FloorLayout floor = layout.Floors[f];
                    for (int k = 0; k < count; k++)
                    {
                        Area area = Choose(floor, rule, rng, 0);
                        if (area == null && rule.Required)
                            for (int relax = 1; relax <= 4 && area == null; relax++)
                                area = Choose(floor, rule, rng, relax);
                        if (area == null)
                        {
                            if (rule.Required)
                                ctx.Fail($"Required role '{rule.Name}' has no suitable area on floor {f}.");
                            break;
                        }
                        Give(ctx, floor, area, rule, rng);
                        total++;
                    }
                }
                if (rule.Required && total == 0)
                    ctx.Fail($"Required role '{rule.Name}' was not placed (check its floor limits).");
            }
        }

        /// <summary>Best free area for a rule. <paramref name="relax"/> drops constraints: 1 progress, 2 placement, 3 style, 4 size.</summary>
        private static Area Choose(FloorLayout floor, RoleRuleInfo rule, DungeonRandom rng, int relax)
        {
            float averageCells = 0f;
            int counted = 0;
            foreach (Area a in floor.Areas)
                if (a.AnchorIndex < 0)
                {
                    averageCells += a.Cells.Count;
                    counted++;
                }
            averageCells = counted > 0 ? averageCells / counted : 1f;

            // End of the main path: the last free main-path area before the floor's departure.
            if (rule.Placement == RolePlacement.EndOfMainPath && relax < 2)
            {
                Area bestEnd = null;
                foreach (Area a in floor.Areas)
                {
                    if (!Free(a) || !a.OnMainPath)
                        continue;
                    if (relax < 3 && !rule.Styles.Contains(a.Style))
                        continue;
                    if (relax < 4 && a.Cells.Count < rule.MinCells)
                        continue;
                    if (bestEnd == null || a.Progress > bestEnd.Progress)
                        bestEnd = a;
                }
                if (bestEnd != null || relax < 1)
                    return bestEnd;
            }

            Area best = null;
            float bestScore = 0f;
            foreach (Area a in floor.Areas)
            {
                if (!Free(a))
                    continue;
                if (relax < 1 && !rule.Progress.Contains(a.Progress))
                    continue;
                if (relax < 2 && !PlacementFits(rule.Placement, a))
                    continue;
                if (relax < 3 && !rule.Styles.Contains(a.Style))
                    continue;
                if (relax < 4 && a.Cells.Count < rule.MinCells)
                    continue;

                float ratio = a.Cells.Count / Mathf.Max(1f, averageCells);
                float sizeScore;
                switch (rule.Size)
                {
                    case SizePreference.Large: sizeScore = ratio * ratio; break;
                    case SizePreference.Small: sizeScore = 1f / Mathf.Max(0.2f, ratio * ratio); break;
                    case SizePreference.Medium: sizeScore = 1f / (1f + Mathf.Abs(Mathf.Log(Mathf.Max(0.05f, ratio)))); break;
                    default: sizeScore = 1f; break;
                }
                float score = Mathf.Max(0.001f, rule.Weight) * sizeScore * (0.6f + 0.8f * rng.Value());
                if (rule.Templates.Length > 0)
                    score *= 1.5f;
                if (best == null || score > bestScore)
                {
                    best = a;
                    bestScore = score;
                }
            }
            return best;
        }

        private static bool Free(Area a) => a.Role == AreaRole.None && a.AnchorIndex < 0 && a.Cells.Count > 0;

        private static bool PlacementFits(RolePlacement placement, Area a)
        {
            switch (placement)
            {
                case RolePlacement.Leaf: return a.IsLeaf;
                case RolePlacement.OnMainPath: return a.OnMainPath;
                case RolePlacement.OffMainPath: return !a.OnMainPath;
                case RolePlacement.Hub: return a.IsHub;
                case RolePlacement.EndOfMainPath: return a.OnMainPath;
                default: return true;
            }
        }

        private static void Give(DungeonContext ctx, FloorLayout floor, Area area, RoleRuleInfo rule, DungeonRandom rng)
        {
            area.Role = rule.Role;
            area.Tag = !string.IsNullOrEmpty(rule.Tag) ? rule.Tag : (rule.Role == AreaRole.Custom ? rule.Name : "");

            if (rule.SecretEntrance && !area.OnMainPath)
            {
                foreach (int id in area.Connections)
                {
                    Connection c = floor.Connections[id];
                    Area other = floor.Areas[c.Other(area.Id)];
                    bool built = area.Style != ZoneStyle.Cavern && other.Style != ZoneStyle.Cavern;
                    if (built && (c.Kind == ConnectionKind.Corridor || c.Kind == ConnectionKind.Door))
                    {
                        c.Kind = ConnectionKind.Secret;
                        c.Width = 1;
                    }
                }
            }

            // A template for the role, if one fits the area's box.
            if (rule.Templates.Length > 0 && area.TemplateIndex < 0 && !area.Fixed)
            {
                var fitting = new List<int>();
                var weights = new List<float>();
                foreach (int t in rule.Templates)
                {
                    TemplateInfo info = ctx.Profile.Templates[t];
                    if (info.Width <= area.Bounds.width && info.Height <= area.Bounds.height && info.Styles.Contains(area.Style))
                    {
                        fitting.Add(t);
                        weights.Add(info.Weight);
                    }
                }
                int pick = rng.WeightedIndex(weights);
                if (pick >= 0)
                    area.TemplateIndex = fitting[pick];
            }
        }
    }
}
