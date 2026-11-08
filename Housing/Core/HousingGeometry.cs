using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Exact plane geometry for areas on the ground (plots, house footprints, rooms, zones): area, containment and overlap of
/// any simple polygons, convex or not, measured as the area they share - so two plots that only touch along a fence do
/// not overlap, and a plot crossing another in a "+" does. Pure C#, unit tested.
/// <para>How: each polygon is cut into triangles (ear clipping); the shared area is the sum of the areas shared by each
/// pair of triangles (a triangle clipped by another is a convex polygon).</para>
/// </summary>
public static class HousingGeometry
{
    /// <summary>Shared area below which two areas are taken to touch, not overlap (square metres).</summary>
    public const float DefaultOverlapArea = 0.01f;

    /// <summary>The polygon's area (square metres, whatever its winding).</summary>
    public static float Area(IList<Vector2> poly)
    {
        if (poly == null || poly.Count < 3) return 0f;
        return Mathf.Abs(SignedArea(poly));
    }

    /// <summary>Positive when the points go counter-clockwise.</summary>
    public static float SignedArea(IList<Vector2> poly)
    {
        if (poly == null || poly.Count < 3) return 0f;
        double a = 0.0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            a += (double)poly[j].x * poly[i].y - (double)poly[i].x * poly[j].y;
        return (float)(a * 0.5);
    }

    /// <summary>Is the point inside the polygon (even-odd)?</summary>
    public static bool Contains(IList<Vector2> poly, Vector2 p)
    {
        if (poly == null || poly.Count < 3) return false;
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>The distance from a point to the polygon's edges (0 on an edge).</summary>
    public static float DistanceToEdges(IList<Vector2> poly, Vector2 p)
    {
        if (poly == null || poly.Count == 0) return float.MaxValue;
        if (poly.Count == 1) return Vector2.Distance(poly[0], p);
        float best = float.MaxValue;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[j], b = poly[i], ab = b - a;
            float t = ab.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    /// <summary>The distance from a point outside to the polygon (0 inside).</summary>
    public static float Distance(IList<Vector2> poly, Vector2 p) => Contains(poly, p) ? 0f : DistanceToEdges(poly, p);

    /// <summary>The area two polygons share (square metres). Measured in double about a point between them, so the result does not
    /// depend on how far from the world's origin they lie (compare outlines given in one object's local space for the best
    /// precision: <see cref="Relative"/>).</summary>
    public static float SharedArea(IList<Vector2> a, IList<Vector2> b)
    {
        if (a == null || b == null || a.Count < 3 || b.Count < 3) return 0f;
        Rect ra = Bounds(a), rb = Bounds(b);
        if (!ra.Overlaps(rb)) return 0f;
        double ox = ((double)ra.center.x + rb.center.x) * 0.5, oy = ((double)ra.center.y + rb.center.y) * 0.5;
        List<D2[]> ta = TriangulateD(a, ox, oy), tb = TriangulateD(b, ox, oy);
        double total = 0.0;
        var clip = new List<D2>(8);
        var tmp = new List<D2>(8);
        foreach (D2[] x in ta)
        {
            BoundsD(x, out double x0, out double y0, out double x1, out double y1);
            foreach (D2[] y in tb)
            {
                BoundsD(y, out double u0, out double v0, out double u1, out double v1);
                if (x1 < u0 || u1 < x0 || y1 < v0 || v1 < y0) continue;
                total += ConvexSharedArea(x, y, clip, tmp);
            }
        }
        return (float)total;
    }

    /// <summary>
    /// The shared area below which two places placed at <paramref name="positionA"/> and <paramref name="positionB"/> only touch:
    /// <see cref="DefaultOverlapArea"/>, plus what the precision of their positions allows along a shared fence (the
    /// shorter outline's length times the spacing of floats that far from the origin: places kilometres out cannot be put
    /// closer than that).
    /// </summary>
    public static float TouchingSlack(IList<Vector2> a, IList<Vector2> b, Vector3 positionA, Vector3 positionB)
    {
        double far = Math.Max(Math.Max(Math.Abs(positionA.x), Math.Abs(positionA.z)), Math.Max(Math.Abs(positionB.x), Math.Abs(positionB.z)));
        double ulp = Math.Pow(2.0, Math.Floor(Math.Log(Math.Max(1.0, far), 2.0)) - 23.0);
        double fence = Math.Min(Perimeter(a), Perimeter(b));
        return (float)(DefaultOverlapArea + fence * ulp * 4.0);
    }

    /// <summary>The length of the polygon's outline, metres.</summary>
    public static float Perimeter(IList<Vector2> poly)
    {
        if (poly == null || poly.Count < 2) return 0f;
        double len = 0.0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            double dx = (double)poly[i].x - poly[j].x, dy = (double)poly[i].y - poly[j].y;
            len += Math.Sqrt(dx * dx + dy * dy);
        }
        return (float)len;
    }

    /// <summary>Do the two areas overlap by more than <paramref name="minArea"/> square metres (touching is not overlapping)?</summary>
    public static bool Overlap(IList<Vector2> a, IList<Vector2> b, float minArea = DefaultOverlapArea) => SharedArea(a, b) > minArea;

    /// <summary>Does <paramref name="outer"/> hold all of <paramref name="inner"/> (but for <paramref name="slack"/> square metres)?</summary>
    public static bool Holds(IList<Vector2> outer, IList<Vector2> inner, float slack = DefaultOverlapArea)
    {
        float area = Area(inner);
        if (area <= 0f) return inner != null && inner.Count > 0 && Contains(outer, inner[0]);
        return area - SharedArea(outer, inner) <= slack;
    }

    /// <summary>The share of <paramref name="inner"/>'s area outside <paramref name="outer"/> (0 = all inside).</summary>
    public static float OutsideShare(IList<Vector2> outer, IList<Vector2> inner)
    {
        float area = Area(inner);
        if (area <= 0f) return 0f;
        return Mathf.Clamp01((area - SharedArea(outer, inner)) / area);
    }

    /// <summary>The box around the points.</summary>
    public static Rect Bounds(IList<Vector2> poly)
    {
        if (poly == null || poly.Count == 0) return new Rect();
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (Vector2 p in poly)
        {
            x0 = Mathf.Min(x0, p.x);
            y0 = Mathf.Min(y0, p.y);
            x1 = Mathf.Max(x1, p.x);
            y1 = Mathf.Max(y1, p.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    /// <summary>
    /// Is the polygon simple (no two edges cross or touch) with at least three points and some area? A point repeated
    /// (twice in a row, or the last as the first) is one point: it does not make the outline cross itself.
    /// </summary>
    public static bool IsSimple(IList<Vector2> poly)
    {
        if (poly == null || poly.Count < 3) return false;
        List<D2> pts = Clean(poly, 0.0, 0.0, out double scale);
        int n = pts.Count;
        if (n < 3 || Math.Abs(SignedAreaD(pts)) < 1e-6) return false;
        double eps = Eps(scale);
        for (int i = 0; i < n; i++)
        {
            D2 a = pts[i], b = pts[(i + 1) % n];
            for (int j = i + 1; j < n; j++)
            {
                if ((j + 1) % n == i || (i + 1) % n == j) continue; // neighbours share a point
                if (SegmentsCross(a, b, pts[j], pts[(j + 1) % n], eps)) return false;
            }
        }
        return true;
    }

    /// <summary>Do two segments cross or touch?</summary>
    public static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        double scale = Math.Max(1.0, Math.Max(Math.Max(Math.Abs(a.x), Math.Abs(a.y)), Math.Max(Math.Abs(c.x), Math.Abs(c.y))));
        return SegmentsCross(new D2(a), new D2(b), new D2(c), new D2(d), Eps(scale));
    }

    private static bool SegmentsCross(D2 a, D2 b, D2 c, D2 d, double eps)
    {
        double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        if (((d1 > eps && d2 < -eps) || (d1 < -eps && d2 > eps)) && ((d3 > eps && d4 < -eps) || (d3 < -eps && d4 > eps))) return true;
        double e = Math.Sqrt(eps);
        return (Math.Abs(d1) <= eps && OnSegment(c, d, a, e)) || (Math.Abs(d2) <= eps && OnSegment(c, d, b, e)) ||
               (Math.Abs(d3) <= eps && OnSegment(a, b, c, e)) || (Math.Abs(d4) <= eps && OnSegment(a, b, d, e));
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

    private static double Cross(D2 a, D2 b, D2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

    private static bool OnSegment(D2 a, D2 b, D2 p, double e) =>
        p.x >= Math.Min(a.x, b.x) - e && p.x <= Math.Max(a.x, b.x) + e && p.y >= Math.Min(a.y, b.y) - e && p.y <= Math.Max(a.y, b.y) + e;

    /// <summary>A point in double precision (the geometry's own sums: exact enough for areas kilometres from the origin).</summary>
    private readonly struct D2
    {
        public readonly double x, y;
        public D2(double x, double y) { this.x = x; this.y = y; }
        public D2(Vector2 v) { x = v.x; y = v.y; }
    }

    /// <summary>The tolerance of cross products for an outline about <paramref name="scale"/> metres across (relative, not absolute).</summary>
    private static double Eps(double scale) => 1e-9 * Math.Max(1.0, scale * scale);

    /// <summary>The points about (<paramref name="ox"/>, <paramref name="oy"/>) without repeats (twice in a row, or the last as the first).</summary>
    private static List<D2> Clean(IList<Vector2> poly, double ox, double oy, out double scale)
    {
        var pts = new List<D2>(poly.Count);
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (Vector2 v in poly)
        {
            x0 = Math.Min(x0, v.x); y0 = Math.Min(y0, v.y); x1 = Math.Max(x1, v.x); y1 = Math.Max(y1, v.y);
        }
        scale = Math.Max(1.0, Math.Max(x1 - x0, y1 - y0));
        double same = 1e-12 * scale * scale;
        foreach (Vector2 v in poly)
        {
            var p = new D2(v.x - ox, v.y - oy);
            if (pts.Count == 0 || Sq(pts[pts.Count - 1], p) > same) pts.Add(p);
        }
        while (pts.Count > 1 && Sq(pts[0], pts[pts.Count - 1]) <= same) pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    private static double Sq(D2 a, D2 b) => (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y);

    private static double SignedAreaD(List<D2> pts)
    {
        double a = 0.0;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++) a += pts[j].x * pts[i].y - pts[i].x * pts[j].y;
        return a * 0.5;
    }

    private static void BoundsD(D2[] t, out double x0, out double y0, out double x1, out double y1)
    {
        x0 = y0 = double.MaxValue;
        x1 = y1 = double.MinValue;
        foreach (D2 p in t)
        {
            x0 = Math.Min(x0, p.x); y0 = Math.Min(y0, p.y); x1 = Math.Max(x1, p.x); y1 = Math.Max(y1, p.y);
        }
    }

    // ---------------------------------------------------------------- triangles
    /// <summary>The polygon cut into triangles (ear clipping; counter-clockwise triangles). A broken polygon gives what it can.</summary>
    public static List<Vector2[]> Triangulate(IList<Vector2> poly)
    {
        var result = new List<Vector2[]>();
        if (poly == null || poly.Count < 3) return result;
        Rect r = Bounds(poly);
        double ox = r.center.x, oy = r.center.y;
        foreach (D2[] t in TriangulateD(poly, ox, oy))
            result.Add(new[] { ToV(t[0], ox, oy), ToV(t[1], ox, oy), ToV(t[2], ox, oy) });
        return result;
    }

    private static Vector2 ToV(D2 p, double ox, double oy) => new Vector2((float)(p.x + ox), (float)(p.y + oy));

    /// <summary>
    /// Ear clipping in double about (<paramref name="ox"/>, <paramref name="oy"/>), with a tolerance relative to the polygon's size:
    /// an ear is cut only when no other corner lies in its triangle or on its edges (a corner on the diagonal would let it
    /// cut across the polygon); a corner on a straight line is dropped. When no such ear exists (a polygon touching itself)
    /// a corner on an edge is allowed, so something is always cut.
    /// </summary>
    private static List<D2[]> TriangulateD(IList<Vector2> poly, double ox, double oy)
    {
        var result = new List<D2[]>();
        if (poly == null || poly.Count < 3) return result;
        List<D2> pts = Clean(poly, ox, oy, out double scale);
        if (pts.Count < 3) return result;
        if (SignedAreaD(pts) < 0.0) pts.Reverse();
        double eps = Eps(scale);
        var idx = new List<int>(pts.Count);
        for (int i = 0; i < pts.Count; i++) idx.Add(i);
        int guard = pts.Count * pts.Count + 8;
        bool lenient = false;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool cut = false;
            for (int k = 0; k < idx.Count; k++)
            {
                int ia = idx[(k + idx.Count - 1) % idx.Count], ib = idx[k], ic = idx[(k + 1) % idx.Count];
                D2 a = pts[ia], b = pts[ib], c = pts[ic];
                double turn = Cross(a, b, c);
                if (Math.Abs(turn) <= eps)
                {
                    idx.RemoveAt(k); // a point on a straight line: no triangle, no loss
                    cut = true;
                    break;
                }
                if (turn < 0.0) continue; // a reflex corner is not an ear
                bool empty = true;
                for (int m = 0; m < idx.Count && empty; m++)
                {
                    int im = idx[m];
                    if (im == ia || im == ib || im == ic) continue;
                    if (InTriangle(a, b, c, pts[im], lenient ? -eps : eps)) empty = false;
                }
                if (!empty) continue;
                result.Add(new[] { a, b, c });
                idx.RemoveAt(k);
                cut = true;
                lenient = false;
                break;
            }
            if (cut) continue;
            if (!lenient) { lenient = true; continue; } // try again allowing corners on an edge
            break; // not a simple polygon: stop with what is cut
        }
        if (idx.Count == 3)
        {
            D2 a = pts[idx[0]], b = pts[idx[1]], c = pts[idx[2]];
            double turn = Cross(a, b, c);
            if (Math.Abs(turn) > eps) result.Add(turn > 0.0 ? new[] { a, b, c } : new[] { a, c, b });
        }
        return result;
    }

    /// <summary>Is <paramref name="p"/> in the triangle, its edges counting as in when <paramref name="margin"/> is positive?</summary>
    private static bool InTriangle(D2 a, D2 b, D2 c, D2 p, double margin) =>
        Cross(a, b, p) >= -margin && Cross(b, c, p) >= -margin && Cross(c, a, p) >= -margin;

    /// <summary>The area two convex counter-clockwise polygons share (Sutherland-Hodgman, in double).</summary>
    private static double ConvexSharedArea(D2[] subject, D2[] clipper, List<D2> output, List<D2> input)
    {
        output.Clear();
        output.AddRange(subject);
        for (int e = 0; e < clipper.Length && output.Count > 0; e++)
        {
            D2 c0 = clipper[e], c1 = clipper[(e + 1) % clipper.Length];
            input.Clear();
            input.AddRange(output);
            output.Clear();
            for (int i = 0; i < input.Count; i++)
            {
                D2 p = input[i], q = input[(i + 1) % input.Count];
                bool pIn = Cross(c0, c1, p) >= 0.0, qIn = Cross(c0, c1, q) >= 0.0;
                if (pIn) output.Add(p);
                if (pIn != qIn) output.Add(Intersection(p, q, c0, c1));
            }
        }
        if (output.Count < 3) return 0.0;
        double area = 0.0;
        for (int i = 0, j = output.Count - 1; i < output.Count; j = i++)
            area += output[j].x * output[i].y - output[i].x * output[j].y;
        return Math.Abs(area * 0.5);
    }

    private static D2 Intersection(D2 p, D2 q, D2 a, D2 b)
    {
        double rx = q.x - p.x, ry = q.y - p.y, sx = b.x - a.x, sy = b.y - a.y;
        double denom = rx * sy - ry * sx;
        if (Math.Abs(denom) < 1e-18) return p;
        double t = ((a.x - p.x) * sy - (a.y - p.y) * sx) / denom;
        return new D2(p.x + rx * t, p.y + ry * t);
    }

    /// <summary>
    /// Local ground-plane points of one object (at <paramref name="fromPosition"/>, turned <paramref name="fromRotation"/>) in the
    /// local space of another - computed in double from the two objects' offset, so two places kilometres from the world's
    /// origin are compared as precisely as at the origin (their world points, rounded to floats, would not be).
    /// </summary>
    public static List<Vector2> Relative(IList<Vector2> local, Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation)
    {
        var result = new List<Vector2>(local != null ? local.Count : 0);
        if (local == null) return result;
        double dx = (double)fromPosition.x - toPosition.x, dz = (double)fromPosition.z - toPosition.z;
        // Turns about the vertical only (places lie on the ground): the yaw of each rotation.
        double fromYaw = Yaw(fromRotation), toYaw = Yaw(toRotation);
        double cf = Math.Cos(fromYaw), sf = Math.Sin(fromYaw), ct = Math.Cos(-toYaw), st = Math.Sin(-toYaw);
        foreach (Vector2 p in local)
        {
            // From local to the world offset (Unity's yaw turns +Z towards +X), then into the other's local space.
            double wx = p.x * cf + p.y * sf + dx, wz = -p.x * sf + p.y * cf + dz;
            double lx = wx * ct + wz * st, lz = -wx * st + wz * ct;
            result.Add(new Vector2((float)lx, (float)lz));
        }
        return result;
    }

    /// <summary>The turn of a rotation about the vertical, radians (Unity's yaw: +Z towards +X).</summary>
    private static double Yaw(Quaternion q)
    {
        Vector3 f = q * new Vector3(0f, 0f, 1f);
        return Math.Atan2(f.x, f.z);
    }

    // ---------------------------------------------------------------- shapes and spaces
    /// <summary>A rectangle centred on <paramref name="centre"/>, turned <paramref name="degrees"/> (counter-clockwise points).</summary>
    public static List<Vector2> Rectangle(Vector2 centre, Vector2 size, float degrees = 0f)
    {
        float hx = Mathf.Abs(size.x) * 0.5f, hy = Mathf.Abs(size.y) * 0.5f;
        var pts = new List<Vector2> { new Vector2(-hx, -hy), new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy) };
        float r = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 p = pts[i];
            pts[i] = centre + new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
        }
        return pts;
    }

    /// <summary>Local ground-plane points (X, Z of an object at <paramref name="position"/> turned by <paramref name="rotation"/>) as world X, Z.</summary>
    public static List<Vector2> ToWorld(IList<Vector2> local, Vector3 position, Quaternion rotation)
    {
        var w = new List<Vector2>(local != null ? local.Count : 0);
        if (local == null) return w;
        foreach (Vector2 p in local)
        {
            Vector3 v = position + rotation * new Vector3(p.x, 0f, p.y);
            w.Add(new Vector2(v.x, v.z));
        }
        return w;
    }

    /// <summary>A world point as local ground-plane coordinates of an object.</summary>
    public static Vector2 ToLocal(Vector3 world, Vector3 position, Quaternion rotation)
    {
        Vector3 l = Quaternion.Inverse(rotation) * (world - position);
        return new Vector2(l.x, l.z);
    }

    /// <summary>The footprint of a box (centre, size, rotation in the world) on the ground: its four corners as world X, Z.</summary>
    public static List<Vector2> BoxFootprint(Vector3 centre, Vector3 size, Quaternion rotation)
    {
        // The box's outline seen from above: the hull of its eight corners on the ground plane.
        var corners = new List<Vector2>(8);
        for (int i = 0; i < 8; i++)
        {
            var c = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            Vector3 w = centre + rotation * Vector3.Scale(c, size);
            corners.Add(new Vector2(w.x, w.z));
        }
        return ConvexHull(corners);
    }

    /// <summary>The convex hull of points (counter-clockwise, monotone chain).</summary>
    public static List<Vector2> ConvexHull(IList<Vector2> points)
    {
        var pts = new List<Vector2>(points);
        pts.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
        if (pts.Count < 3) return pts;
        var hull = new List<Vector2>(pts.Count * 2);
        foreach (Vector2 p in pts)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        int lower = hull.Count + 1;
        for (int i = pts.Count - 2; i >= 0; i--)
        {
            Vector2 p = pts[i];
            while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        hull.RemoveAt(hull.Count - 1);
        return hull;
    }
}
