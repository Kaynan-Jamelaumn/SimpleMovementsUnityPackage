using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCommon
{
    /// <summary>
    /// Neighbourhood graphs over point sets, used to decide which areas could sensibly be joined.
    /// The Gabriel graph links two points when no third point lies inside the circle that has them as a diameter:
    /// it always contains the Euclidean minimum spanning tree (so it is connected) and is a subgraph of the Delaunay
    /// triangulation, without its long skinny edges - a good set of candidate corridors.
    /// </summary>
    public static class GraphBuilders
    {
        public struct Edge
        {
            public int A, B;
            public float Length;
        }

        /// <summary>Gabriel graph of <paramref name="points"/>. O(n^2) pairs, each checked against a spatial hash.</summary>
        public static List<Edge> Gabriel(IReadOnlyList<Vector2> points)
        {
            var edges = new List<Edge>();
            int n = points.Count;
            if (n < 2)
                return edges;

            // Hash cell size from the average spacing of the points.
            Vector2 min = points[0], max = points[0];
            for (int i = 1; i < n; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }
            float area = Mathf.Max(1f, (max.x - min.x) * (max.y - min.y));
            var hash = new SpatialHash2D<int>(Mathf.Max(1f, Mathf.Sqrt(area / n)));
            for (int i = 0; i < n; i++)
                hash.Add(points[i], i);

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    Vector2 a = points[i], b = points[j];
                    Vector2 mid = (a + b) * 0.5f;
                    float radius = Vector2.Distance(a, b) * 0.5f;
                    int ci = i, cj = j;
                    bool blocked = radius > 1e-4f && hash.AnyWithin(mid, radius * 0.999f, k => k != ci && k != cj);
                    if (!blocked)
                        edges.Add(new Edge { A = i, B = j, Length = radius * 2f });
                }
            }
            return edges;
        }

        /// <summary>
        /// Adds the shortest edges needed to join separate components of a graph (by nearest point pairs), so a
        /// candidate graph built with a distance limit - or from several sources - is always connected.
        /// </summary>
        public static void ConnectComponents(IReadOnlyList<Vector2> points, List<Edge> edges)
        {
            int n = points.Count;
            if (n < 2)
                return;
            var sets = new UnionFind(n);
            foreach (Edge e in edges)
                sets.Union(e.A, e.B);
            while (sets.Sets > 1)
            {
                float best = float.MaxValue;
                int bi = -1, bj = -1;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (sets.Connected(i, j))
                            continue;
                        float d = (points[i] - points[j]).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            bi = i;
                            bj = j;
                        }
                    }
                }
                if (bi < 0)
                    break;
                sets.Union(bi, bj);
                edges.Add(new Edge { A = bi, B = bj, Length = Mathf.Sqrt(best) });
            }
        }
    }

    /// <summary>
    /// Picks well-spaced points: walks the candidates in the given order (shuffle them first for randomness) and keeps
    /// each one that is at least <c>spacing</c> from every point kept so far (and from the <c>blockers</c>).
    /// </summary>
    public static class PoissonSampler
    {
        public static List<int> Sample(IReadOnlyList<Vector2> orderedCandidates, float spacing, int maxCount,
            SpatialHash2D<int> blockers = null, float blockerSpacing = 0f)
        {
            var kept = new List<int>();
            var hash = new SpatialHash2D<int>(Mathf.Max(0.5f, spacing));
            for (int i = 0; i < orderedCandidates.Count && kept.Count < maxCount; i++)
            {
                Vector2 p = orderedCandidates[i];
                if (spacing > 0f && hash.AnyWithin(p, spacing))
                    continue;
                if (blockers != null && blockers.AnyWithin(p, blockerSpacing))
                    continue;
                kept.Add(i);
                hash.Add(p, i);
            }
            return kept;
        }
    }
}
