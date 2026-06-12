using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Features your game adds to the world - roads, paths, settlements, camps - that object placement rules can
/// keep objects near to or away from (a Distances To Features rule with Feature = Custom and the same tag).
/// Register them as points or line segments with a width, before the chunks around them are generated:
/// placement reads them when a chunk is generated, so registering later only affects chunks generated afterwards
/// (for the same seed and registered features, placement is always the same).
/// Thread-safe.
/// </summary>
public static class PlacementFeatures
{
    private const float CellSize = 64f;
    // Distances beyond this are reported as "far".
    private const float MaxSearch = 1024f;

    private sealed class Segment
    {
        public Vector2 A, B;
        public float HalfWidth;
    }

    private sealed class Layer
    {
        public readonly List<Segment> Segments = new List<Segment>();
        public readonly Dictionary<long, List<int>> Cells = new Dictionary<long, List<int>>();
    }

    private static readonly Dictionary<string, Layer> Layers = new Dictionary<string, Layer>(System.StringComparer.OrdinalIgnoreCase);
    private static readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();

    /// <summary>A point feature (e.g. a settlement centre) with a radius.</summary>
    public static void RegisterPoint(string tag, Vector2 position, float radius = 0f)
    {
        RegisterSegment(tag, position, position, radius);
    }

    /// <summary>A straight piece of a road or path, <paramref name="halfWidth"/> wide on each side.</summary>
    public static void RegisterSegment(string tag, Vector2 a, Vector2 b, float halfWidth = 0f)
    {
        if (string.IsNullOrEmpty(tag))
            return;
        Lock.EnterWriteLock();
        try
        {
            if (!Layers.TryGetValue(tag, out Layer layer))
                Layers[tag] = layer = new Layer();
            int index = layer.Segments.Count;
            layer.Segments.Add(new Segment { A = a, B = b, HalfWidth = Mathf.Max(0f, halfWidth) });

            float pad = Mathf.Max(0f, halfWidth);
            int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - pad) / CellSize), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + pad) / CellSize);
            int y0 = Mathf.FloorToInt((Mathf.Min(a.y, b.y) - pad) / CellSize), y1 = Mathf.FloorToInt((Mathf.Max(a.y, b.y) + pad) / CellSize);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    long key = Key(x, y);
                    if (!layer.Cells.TryGetValue(key, out List<int> list))
                        layer.Cells[key] = list = new List<int>();
                    list.Add(index);
                }
            }
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }

    /// <summary>A road or path given as a line through points.</summary>
    public static void RegisterPolyline(string tag, IList<Vector2> points, float halfWidth = 0f)
    {
        if (points == null)
            return;
        for (int i = 0; i + 1 < points.Count; i++)
            RegisterSegment(tag, points[i], points[i + 1], halfWidth);
        if (points.Count == 1)
            RegisterPoint(tag, points[0], halfWidth);
    }

    /// <summary>Removes every feature with a tag (or all features when <paramref name="tag"/> is null).</summary>
    public static void Clear(string tag = null)
    {
        Lock.EnterWriteLock();
        try
        {
            if (tag == null)
                Layers.Clear();
            else
                Layers.Remove(tag);
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }

    /// <summary>Distance from a world position (x, z) to the nearest feature with a tag (0 inside its width), or 100000 if there is none within 1024 units.</summary>
    public static float Distance(string tag, float x, float z)
    {
        if (string.IsNullOrEmpty(tag))
            return PlacementFields.FarDistance;
        Lock.EnterReadLock();
        try
        {
            if (!Layers.TryGetValue(tag, out Layer layer) || layer.Segments.Count == 0)
                return PlacementFields.FarDistance;

            Vector2 p = new Vector2(x, z);
            int cx = Mathf.FloorToInt(x / CellSize), cy = Mathf.FloorToInt(z / CellSize);
            float best = float.PositiveInfinity;
            int maxRing = Mathf.CeilToInt(MaxSearch / CellSize);
            for (int ring = 0; ring <= maxRing; ring++)
            {
                // Anything in a further ring is at least (ring - 1) cells away.
                if ((ring - 1) * CellSize > best)
                    break;
                for (int dy = -ring; dy <= ring; dy++)
                {
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring)
                            continue;
                        if (!layer.Cells.TryGetValue(Key(cx + dx, cy + dy), out List<int> list))
                            continue;
                        foreach (int index in list)
                        {
                            Segment s = layer.Segments[index];
                            best = Mathf.Min(best, Mathf.Max(0f, SegmentDistance(p, s.A, s.B) - s.HalfWidth));
                        }
                    }
                }
            }
            return float.IsInfinity(best) ? PlacementFields.FarDistance : best;
        }
        finally
        {
            Lock.ExitReadLock();
        }
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
}
