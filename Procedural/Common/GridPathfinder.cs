using System;
using System.Collections.Generic;

namespace ProceduralCommon
{
    /// <summary>
    /// A* over a 4-connected grid, with multiple sources, a target predicate and an optional cost for turning
    /// (the search state includes the direction of the last step, so straight corridors can be preferred).
    /// Buffers are reused between searches through a generation stamp, so many searches on the same grid don't
    /// allocate. Plain C#: one instance per thread.
    /// </summary>
    public sealed class GridPathfinder
    {
        /// <summary>Cost of stepping from one cell to a neighbour in a direction (0 = +y, 1 = +x, 2 = -y, 3 = -x). Negative = blocked.</summary>
        public delegate float StepCost(int from, int to, int direction);

        public static readonly int[] DX = { 0, 1, 0, -1 };
        public static readonly int[] DY = { 1, 0, -1, 0 };

        private const int States = 5;          // four arrival directions plus "no direction yet"
        private const int NoDirection = 4;

        private readonly int width, height;
        private readonly float[] g;
        private readonly int[] parent;
        private readonly int[] seen;
        private readonly int[] closed;
        private readonly BinaryHeap<int> open = new BinaryHeap<int>(1024);
        private int stamp;

        public GridPathfinder(int width, int height)
        {
            this.width = width;
            this.height = height;
            int n = width * height * States;
            g = new float[n];
            parent = new int[n];
            seen = new int[n];
            closed = new int[n];
        }

        public int Width => width;
        public int Height => height;

        /// <summary>Cells expanded by the last search (for statistics).</summary>
        public int Expanded { get; private set; }

        /// <summary>Total cost of the last path found.</summary>
        public float LastCost { get; private set; }

        /// <summary>
        /// Finds the cheapest path from any of <paramref name="sources"/> to any cell accepted by
        /// <paramref name="isTarget"/>. <paramref name="path"/> receives the cells from the source to the target,
        /// both included. <paramref name="heuristic"/> estimates the remaining cost from a cell (return 0 for
        /// Dijkstra); keep it at or below the true cost for shortest paths.
        /// </summary>
        public bool FindPath(IReadOnlyList<int> sources, Func<int, bool> isTarget, Func<int, float> heuristic,
            StepCost cost, float turnPenalty, List<int> path, int maxExpansions = 500000)
        {
            path?.Clear();
            Expanded = 0;
            LastCost = 0f;
            if (sources == null || sources.Count == 0)
                return false;

            if (++stamp == int.MaxValue)
            {
                Array.Clear(seen, 0, seen.Length);
                Array.Clear(closed, 0, closed.Length);
                stamp = 1;
            }
            open.Clear();

            for (int i = 0; i < sources.Count; i++)
            {
                int cell = sources[i];
                if (cell < 0 || cell >= width * height)
                    continue;
                int s = cell * States + NoDirection;
                if (seen[s] == stamp)
                    continue;
                seen[s] = stamp;
                g[s] = 0f;
                parent[s] = -1;
                open.Push(s, heuristic != null ? heuristic(cell) : 0f);
            }

            while (open.Count > 0)
            {
                int s = open.Pop();
                if (closed[s] == stamp)
                    continue;
                closed[s] = stamp;

                int cell = s / States;
                int dir = s % States;
                if (isTarget(cell))
                {
                    LastCost = g[s];
                    if (path != null)
                    {
                        for (int t = s; t >= 0; t = parent[t])
                            path.Add(t / States);
                        path.Reverse();
                    }
                    return true;
                }

                if (++Expanded > maxExpansions)
                    break;

                int x = cell % width, y = cell / width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], ny = y + DY[d];
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        continue;
                    int next = nx + ny * width;
                    float c = cost(cell, next, d);
                    if (c < 0f)
                        continue;
                    if (dir != NoDirection && dir != d)
                        c += turnPenalty;

                    int ns = next * States + d;
                    if (closed[ns] == stamp)
                        continue;
                    float ng = g[s] + c;
                    if (seen[ns] == stamp && ng >= g[ns])
                        continue;
                    seen[ns] = stamp;
                    g[ns] = ng;
                    parent[ns] = s;
                    open.Push(ns, ng + (heuristic != null ? heuristic(next) : 0f));
                }
            }

            return false;
        }
    }
}
