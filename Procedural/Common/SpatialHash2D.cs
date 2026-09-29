using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralCommon
{
    /// <summary>
    /// A uniform grid of points (each with an optional radius) for "is anything within R of here" queries -
    /// spacing between placed objects, relation checks, nearest-neighbour lookups. Plain C#: safe on worker threads
    /// (one instance per thread).
    /// </summary>
    public sealed class SpatialHash2D<T>
    {
        public struct Entry
        {
            public Vector2 Position;
            public float Radius;
            public T Value;
        }

        private readonly float cellSize;
        private readonly Dictionary<long, List<Entry>> cells = new Dictionary<long, List<Entry>>();
        private float maxRadius;

        public SpatialHash2D(float cellSize = 4f)
        {
            this.cellSize = Mathf.Max(0.01f, cellSize);
        }

        public int Count { get; private set; }

        public void Clear()
        {
            cells.Clear();
            Count = 0;
            maxRadius = 0f;
        }

        public void Add(Vector2 position, T value, float radius = 0f)
        {
            long key = Key(Mathf.FloorToInt(position.x / cellSize), Mathf.FloorToInt(position.y / cellSize));
            if (!cells.TryGetValue(key, out List<Entry> list))
                cells[key] = list = new List<Entry>(4);
            list.Add(new Entry { Position = position, Radius = radius, Value = value });
            if (radius > maxRadius)
                maxRadius = radius;
            Count++;
        }

        /// <summary>
        /// True if an entry lies within <paramref name="radius"/> (plus the entry's own radius) of
        /// <paramref name="position"/> and passes <paramref name="filter"/> (null = any entry).
        /// </summary>
        public bool AnyWithin(Vector2 position, float radius, Func<T, bool> filter = null)
        {
            bool found = false;
            Visit(position, radius, (entry, distance) =>
            {
                if (filter != null && !filter(entry.Value))
                    return true;
                found = true;
                return false;
            });
            return found;
        }

        /// <summary>Adds every entry within range to <paramref name="results"/>.</summary>
        public void Query(Vector2 position, float radius, List<Entry> results, Func<T, bool> filter = null)
        {
            Visit(position, radius, (entry, distance) =>
            {
                if (filter == null || filter(entry.Value))
                    results.Add(entry);
                return true;
            });
        }

        /// <summary>Distance from <paramref name="position"/> to the nearest entry's edge within <paramref name="searchRadius"/>; float.MaxValue if none.</summary>
        public float NearestDistance(Vector2 position, float searchRadius, Func<T, bool> filter = null)
        {
            float best = float.MaxValue;
            Visit(position, searchRadius, (entry, distance) =>
            {
                if (filter != null && !filter(entry.Value))
                    return true;
                float d = Mathf.Max(0f, distance - entry.Radius);
                if (d < best)
                    best = d;
                return true;
            });
            return best;
        }

        /// <summary>
        /// Calls <paramref name="visit"/> with each entry whose edge is within <paramref name="radius"/> and its
        /// centre distance; stops when it returns false.
        /// </summary>
        public void Visit(Vector2 position, float radius, Func<Entry, float, bool> visit)
        {
            if (Count == 0)
                return;
            float reach = radius + maxRadius;
            int x0 = Mathf.FloorToInt((position.x - reach) / cellSize), x1 = Mathf.FloorToInt((position.x + reach) / cellSize);
            int y0 = Mathf.FloorToInt((position.y - reach) / cellSize), y1 = Mathf.FloorToInt((position.y + reach) / cellSize);
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (!cells.TryGetValue(Key(cx, cy), out List<Entry> list))
                        continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        Entry e = list[i];
                        float dx = e.Position.x - position.x, dy = e.Position.y - position.y;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        if (distance <= radius + e.Radius && !visit(e, distance))
                            return;
                    }
                }
            }
        }

        private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
