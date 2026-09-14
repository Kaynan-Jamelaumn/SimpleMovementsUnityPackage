using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// RiverGenerator, part 3 of 5: finding the way out of a pit over its lowest saddle, and smoothing that route (see RiverGenerator.cs).
public static partial class RiverGenerator
{
    /// <summary>
    /// Finds where a pit would overflow if it filled with water: a priority-flood on a coarse grid around
    /// the pit's lowest point, always expanding the cell that needs the least water to reach, until it
    /// reaches ground lower than the pit (or the ocean, or a lake). That's exactly how a basin fills and
    /// spills, so the route it took crosses the rim at its lowest saddle. Tried at a fine spacing first,
    /// then a coarse one to catch outflows across wide basins. Returns false for a basin that would need more than
    /// <see cref="MaxFillDepth"/> of water, or whose outflow is farther than the search area - a genuinely
    /// enclosed basin, where the river ends in a terminal lake.
    /// </summary>
    private static bool TryFindSpillPath(Vector2 pit, float pitHeight, LakeFeature sourceLake, WaterSettings s, TerrainHeightSampler sampler, Queue<Vector2> path)
    {
        return TryFindSpillPath(pit, pitHeight, SpillGridSpacing, sourceLake, s, sampler, path)
            || TryFindSpillPath(pit, pitHeight, SpillGridSpacing * 3f, sourceLake, s, sampler, path);
    }

    private static bool TryFindSpillPath(Vector2 pit, float pitHeight, float spacing, LakeFeature sourceLake, WaterSettings s, TerrainHeightSampler sampler, Queue<Vector2> path)
    {
        const int radius = SpillGridRadius;
        const int size = radius * 2 + 1;
        int cells = size * size;
        bool[] visited = new bool[cells];
        int[] parent = new int[cells];
        CellHeap open = new CellHeap(256);

        // The grid is aligned to the world (not to the pit), so every search - by other rivers draining into the same
        // basin, or by this one spilling on through a chain of pits - reads the same points, cached (SpillHeight).
        Vector2 center = new Vector2(Mathf.Round(pit.x / spacing) * spacing, Mathf.Round(pit.y / spacing) * spacing);
        int startIndex = radius * size + radius;
        visited[startIndex] = true;
        parent[startIndex] = -1;
        open.Push(pitHeight, startIndex);
        int found = -1;

        while (open.Count > 0 && found < 0)
        {
            open.Pop(out float level, out int current);
            if (level > pitHeight + MaxFillDepth)
                break;

            int cx = current % size;
            int cy = current / size;
            for (int dy = -1; dy <= 1 && found < 0; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                        continue;

                    int neighbor = ny * size + nx;
                    if (visited[neighbor])
                        continue;

                    visited[neighbor] = true;
                    parent[neighbor] = current;
                    Vector2 point = center + new Vector2((nx - radius) * spacing, (ny - radius) * spacing);

                    // Only ground lower than the pit's own bottom counts as an outflow: anything higher
                    // is still part of the same (combined) basin, and would just drain back into the pit.
                    float height = SpillHeight(point, sampler);
                    if (height < pitHeight - 1f
                        || (s.OceansEnabled && height < s.SeaLevel && OceanGenerator.LandSide(s, point.x, point.y) < 0f)
                        || LakeGenerator.FindLakeContaining(point, sourceLake, s, sampler) != null)
                    {
                        found = neighbor;
                        break;
                    }

                    open.Push(Mathf.Max(level, height), neighbor);
                }
            }
        }

        if (found < 0)
            return false;

        List<Vector2> route = new List<Vector2>();
        for (int cell = found; cell >= 0; cell = parent[cell])
            route.Add(center + new Vector2((cell % size - radius) * spacing, (cell / size - radius) * spacing));
        route.Reverse();
        route[0] = pit;   // leaves from the pit itself, not the grid point nearest to it

        // The grid route is a staircase of 45/90 degree moves - smooth it, then walk it at TraceStep.
        route = ChaikinSmooth(ChaikinSmooth(route));
        path.Clear();
        float carried = 0f;
        for (int i = 1; i < route.Count; i++)
        {
            Vector2 a = route[i - 1];
            Vector2 b = route[i];
            float segment = (b - a).magnitude;
            float t = TraceStep - carried;
            while (t <= segment)
            {
                path.Enqueue(Vector2.Lerp(a, b, t / segment));
                t += TraceStep;
            }
            carried = segment - (t - TraceStep);
        }

        if (path.Count == 0)
            path.Enqueue(route[route.Count - 1]);
        return true;
    }

    // Base heights at the spill grids' points (multiples of SpillGridSpacing - the coarse grid's are too), shared by
    // every search. The grid points are fixed, so what the cache holds never depends on which river asked first.
    private const int MaxSpillHeights = 1 << 18;
    private static readonly ConcurrentDictionary<long, float> SpillHeights = new ConcurrentDictionary<long, float>();
    private static int spillHeightCount;

    private static float SpillHeight(Vector2 point, TerrainHeightSampler sampler)
    {
        long key = ((long)Mathf.RoundToInt(point.x / SpillGridSpacing) << 32) ^ (uint)Mathf.RoundToInt(point.y / SpillGridSpacing);
        if (SpillHeights.TryGetValue(key, out float height))
            return height;
        height = sampler.SampleBaseHeight(point.x, point.y);
        if (Interlocked.Increment(ref spillHeightCount) > MaxSpillHeights)
            ClearSpillHeights();   // a long journey: start over rather than keep growing
        SpillHeights[key] = height;
        return height;
    }

    private static void ClearSpillHeights()
    {
        SpillHeights.Clear();
        Interlocked.Exchange(ref spillHeightCount, 0);
    }

    private static List<Vector2> ChaikinSmooth(List<Vector2> points)
    {
        if (points.Count < 3)
            return points;

        List<Vector2> smoothed = new List<Vector2>(points.Count * 2);
        smoothed.Add(points[0]);
        for (int i = 0; i < points.Count - 1; i++)
        {
            smoothed.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
            smoothed.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
        }
        smoothed.Add(points[points.Count - 1]);
        return smoothed;
    }

    /// <summary>Minimal binary min-heap of (key, cell index), for the spill priority-flood.</summary>
    private sealed class CellHeap
    {
        private float[] keys;
        private int[] values;
        public int Count;

        public CellHeap(int capacity)
        {
            keys = new float[capacity];
            values = new int[capacity];
        }

        public void Push(float key, int value)
        {
            if (Count == keys.Length)
            {
                Array.Resize(ref keys, Count * 2);
                Array.Resize(ref values, Count * 2);
            }

            int i = Count++;
            while (i > 0)
            {
                int parentIndex = (i - 1) / 2;
                if (keys[parentIndex] <= key)
                    break;
                keys[i] = keys[parentIndex];
                values[i] = values[parentIndex];
                i = parentIndex;
            }
            keys[i] = key;
            values[i] = value;
        }

        public void Pop(out float key, out int value)
        {
            key = keys[0];
            value = values[0];
            Count--;
            if (Count == 0)
                return;

            float lastKey = keys[Count];
            int lastValue = values[Count];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= Count)
                    break;
                if (child + 1 < Count && keys[child + 1] < keys[child])
                    child++;
                if (lastKey <= keys[child])
                    break;
                keys[i] = keys[child];
                values[i] = values[child];
                i = child;
            }
            keys[i] = lastKey;
            values[i] = lastValue;
        }
    }
}
