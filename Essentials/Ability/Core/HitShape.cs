using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Footprint of an ability's hit area.</summary>
public enum HitShapeType
{
    /// <summary>A disc of Radius on the ground (vertical reach: Height, starting Base Offset below the origin).</summary>
    Circle,
    /// <summary>A true 3D ball of Radius (hits things above and below too).</summary>
    Sphere,
    /// <summary>A vertical column: a disc of Radius extending Height upward from Base Offset.</summary>
    Cylinder,
    /// <summary>A pie slice: Radius long, Angle wide, apex at the origin, pointing forward (Inner Radius leaves a gap).</summary>
    Cone,
    /// <summary>An isosceles triangle: apex at the origin, a base of Width at Length forward.</summary>
    Triangle,
    /// <summary>A square Width x Width.</summary>
    Square,
    /// <summary>A rectangle Width (sideways) x Length (forward).</summary>
    Rectangle,
    /// <summary>A trapezoid: Near Width at the origin widening to Width at Length forward.</summary>
    Trapezoid,
    /// <summary>A thick line (capsule footprint) Length long and Width thick.</summary>
    Line,
    /// <summary>A donut between Inner Radius and Radius (safe centre).</summary>
    Ring,
}

/// <summary>
/// One hit area of an ability, placed relative to an aim frame (origin + facing). All shapes except Sphere are
/// footprints on the ground extruded vertically (2.5D): they ignore the aim's pitch.
/// </summary>
[Serializable]
public class HitShape
{
    [Tooltip("Shape of the hit area. Circle/Ring/Cone use Radius; Square/Rectangle/Triangle/Trapezoid/Line use Width and Length; Sphere is fully 3D.")]
    public HitShapeType type = HitShapeType.Circle;

    [Tooltip("Circle, Sphere, Cylinder, Ring: radius. Cone: how far the cone reaches (metres).")]
    [Min(0f)] public float radius = 2.5f;

    [Tooltip("Ring and Cone: the inner radius left untouched (a safe zone close to the origin). 0 = none.")]
    [Min(0f)] public float innerRadius = 0f;

    [Tooltip("Cone: total opening angle in degrees (90 = quarter circle, 360 = full circle).")]
    [Range(1f, 360f)] public float angle = 90f;

    [Tooltip("Square: side. Rectangle/Triangle: width across the aim. Trapezoid: width at the far end. Line: thickness (metres).")]
    [Min(0f)] public float width = 2f;

    [Tooltip("Trapezoid only: width at the near end (at the origin).")]
    [Min(0f)] public float nearWidth = 1f;

    [Tooltip("Rectangle, Triangle, Trapezoid, Line: how far forward the shape reaches (metres).")]
    [Min(0f)] public float length = 5f;

    [Tooltip("Vertical reach of the footprint (metres). Characters are hit when their body overlaps this band. Ignored by Sphere.")]
    [Min(0.05f)] public float height = 3f;

    [Tooltip("Bottom of the vertical band relative to the origin (metres). -1 reaches a little below the feet; 0 starts at the origin (use 0 for a Cylinder rising from the ground).")]
    public float baseOffset = -1f;

    [Tooltip("Shapes that extend forward (Rectangle, Square, Triangle, Trapezoid, Line) start at the origin when ON, or are centred on it when OFF.")]
    public bool startAtOrigin = true;

    [Tooltip("Offset from the aim frame in its local space (x = right, y = up, z = forward), metres.")]
    public Vector3 offset = Vector3.zero;

    [Tooltip("Extra rotation around the vertical axis (degrees), e.g. to aim a cone sideways.")]
    [Range(-180f, 180f)] public float yaw = 0f;

    /// <summary>A copy of this shape.</summary>
    public HitShape Clone() => (HitShape)MemberwiseClone();

    /// <summary>Largest horizontal distance from the shape origin covered by the footprint (for AI range checks).</summary>
    public float Reach(float scale = 1f)
    {
        ResolvedShape r = ResolvedShape.Resolve(this, Vector3.zero, Quaternion.identity, scale);
        return r.LocalReach() + offset.magnitude * scale;
    }

    public static HitShape CircleShape(float radius) => new HitShape { type = HitShapeType.Circle, radius = radius };
    public static HitShape ConeShape(float radius, float angle) => new HitShape { type = HitShapeType.Cone, radius = radius, angle = angle };
    public static HitShape SphereShape(float radius) => new HitShape { type = HitShapeType.Sphere, radius = radius };
    public static HitShape LineShape(float length, float width) => new HitShape { type = HitShapeType.Line, length = length, width = width };
    public static HitShape RectangleShape(float width, float length) => new HitShape { type = HitShapeType.Rectangle, width = width, length = length };

    /// <summary>Human readable size, e.g. "Cone 6m 90°".</summary>
    public string Describe(float scale = 1f)
    {
        switch (type)
        {
            case HitShapeType.Circle: return $"Circle r{radius * scale:0.#}m";
            case HitShapeType.Sphere: return $"Sphere r{radius * scale:0.#}m";
            case HitShapeType.Cylinder: return $"Cylinder r{radius * scale:0.#}m h{height:0.#}m";
            case HitShapeType.Cone: return $"Cone {radius * scale:0.#}m {angle:0}°";
            case HitShapeType.Triangle: return $"Triangle {width * scale:0.#}x{length * scale:0.#}m";
            case HitShapeType.Square: return $"Square {width * scale:0.#}m";
            case HitShapeType.Rectangle: return $"Rectangle {width * scale:0.#}x{length * scale:0.#}m";
            case HitShapeType.Trapezoid: return $"Trapezoid {nearWidth * scale:0.#}-{width * scale:0.#}x{length * scale:0.#}m";
            case HitShapeType.Line: return $"Line {length * scale:0.#}m x{width * scale:0.#}m";
            case HitShapeType.Ring: return $"Ring {innerRadius * scale:0.#}-{radius * scale:0.#}m";
            default: return type.ToString();
        }
    }

    /// <summary>Adds problems with this shape's settings to <paramref name="errors"/>.</summary>
    public void Validate(string owner, List<string> errors)
    {
        switch (type)
        {
            case HitShapeType.Circle:
            case HitShapeType.Sphere:
            case HitShapeType.Cylinder:
                if (radius <= 0f) errors.Add($"{owner}: {type} needs a Radius above 0.");
                break;
            case HitShapeType.Cone:
                if (radius <= 0f) errors.Add($"{owner}: Cone needs a Radius above 0.");
                if (innerRadius >= radius) errors.Add($"{owner}: Cone Inner Radius must be smaller than Radius.");
                break;
            case HitShapeType.Ring:
                if (radius <= innerRadius) errors.Add($"{owner}: Ring Radius must be larger than Inner Radius.");
                break;
            case HitShapeType.Trapezoid:
                if (length <= 0f || (width <= 0f && nearWidth <= 0f)) errors.Add($"{owner}: Trapezoid needs Length and a width above 0.");
                break;
            default:
                if (width <= 0f) errors.Add($"{owner}: {type} needs a Width above 0.");
                if (type != HitShapeType.Square && length <= 0f) errors.Add($"{owner}: {type} needs a Length above 0.");
                break;
        }
    }
}

/// <summary>A character approximated as an upright cylinder: feet position, radius and height.</summary>
public struct TargetVolume
{
    public Vector3 basePosition;
    public float radius;
    public float height;

    public TargetVolume(Vector3 basePosition, float radius, float height)
    {
        this.basePosition = basePosition;
        this.radius = radius;
        this.height = height;
    }

    public Vector3 Center => basePosition + Vector3.up * (height * 0.5f);
}

/// <summary>
/// A hit shape placed in the world with its size scaled - a plain value, cheap to create every evaluation.
/// All geometric tests are exact for the footprint (circle vs shape) and the vertical band.
/// </summary>
public struct ResolvedShape
{
    public HitShapeType type;
    public float radius, inner, halfAngle, width, nearWidth, length, height, baseOffset;
    public bool startAtOrigin;
    public Vector3 origin;
    /// <summary>Yaw-only rotation for footprint shapes; full rotation for Sphere offsets.</summary>
    public Quaternion rotation;

    public static ResolvedShape Resolve(HitShape s, Vector3 framePosition, Quaternion frameRotation, float scale)
    {
        scale = Mathf.Max(0.01f, scale);
        Quaternion yawOnly = FlattenRotation(frameRotation);
        Quaternion placement = s.type == HitShapeType.Sphere ? frameRotation : yawOnly;
        var r = new ResolvedShape
        {
            type = s.type,
            radius = s.radius * scale,
            inner = s.innerRadius * scale,
            halfAngle = Mathf.Clamp(s.angle, 1f, 360f) * 0.5f,
            width = s.width * scale,
            nearWidth = s.nearWidth * scale,
            length = s.length * scale,
            height = s.height,
            baseOffset = s.baseOffset,
            startAtOrigin = s.startAtOrigin,
            origin = framePosition + placement * (s.offset * scale),
            rotation = yawOnly * Quaternion.Euler(0f, s.yaw, 0f),
        };
        if (s.type == HitShapeType.Square)
            r.length = r.width;
        return r;
    }

    /// <summary>Rotation keeping only the heading (yaw) of <paramref name="q"/>.</summary>
    public static Quaternion FlattenRotation(Quaternion q)
    {
        Vector3 f = q * Vector3.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f)
        {
            f = q * Vector3.up;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-6f)
                return Quaternion.identity;
        }
        return Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    /// <summary>Position of a world point in the shape's local frame (x right, y up, z forward).</summary>
    public Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(rotation) * (world - origin);

    public Vector3 ToWorld(Vector3 local) => origin + rotation * local;

    /// <summary>True if the character volume overlaps the shape.</summary>
    public bool Overlaps(in TargetVolume t)
    {
        if (type == HitShapeType.Sphere)
            return SphereOverlap(t);

        Vector3 local = ToLocal(t.basePosition);
        float bottom = local.y, top = local.y + Mathf.Max(0.01f, t.height);
        float bandMin = baseOffset, bandMax = baseOffset + height;
        if (top < bandMin || bottom > bandMax)
            return false;
        return FootprintDistance(new Vector2(local.x, local.z)) <= t.radius;
    }

    /// <summary>True if a point lies inside the shape (radius 0 target, any height inside the band).</summary>
    public bool ContainsPoint(Vector3 world)
    {
        if (type == HitShapeType.Sphere)
            return (world - origin).sqrMagnitude <= radius * radius;
        Vector3 local = ToLocal(world);
        if (local.y < baseOffset || local.y > baseOffset + height)
            return false;
        return FootprintDistance(new Vector2(local.x, local.z)) <= 0f;
    }

    private bool SphereOverlap(in TargetVolume t)
    {
        float r = Mathf.Max(0.01f, t.radius);
        float h = Mathf.Max(0.01f, t.height);
        // Closest point on the body's vertical axis segment (a capsule of radius r).
        float yMin = t.basePosition.y + Mathf.Min(r, h * 0.5f);
        float yMax = t.basePosition.y + Mathf.Max(h - r, h * 0.5f);
        float y = Mathf.Clamp(origin.y, yMin, yMax);
        Vector3 closest = new Vector3(t.basePosition.x, y, t.basePosition.z);
        float reach = radius + r;
        return (closest - origin).sqrMagnitude <= reach * reach;
    }

    /// <summary>
    /// Distance from a local 2D point (x, z) to the footprint: 0 inside, positive outside.
    /// </summary>
    public float FootprintDistance(Vector2 p)
    {
        switch (type)
        {
            case HitShapeType.Circle:
            case HitShapeType.Cylinder:
                return Mathf.Max(0f, p.magnitude - radius);
            case HitShapeType.Ring:
            {
                float d = p.magnitude;
                if (d > radius) return d - radius;
                if (d < inner) return inner - d;
                return 0f;
            }
            case HitShapeType.Cone:
                return ShapeMath.SectorDistance(p, radius, inner, halfAngle);
            case HitShapeType.Line:
            {
                GetLongitudinalRange(out float z0, out float z1);
                float d = ShapeMath.DistancePointSegment(p, new Vector2(0f, z0), new Vector2(0f, z1));
                return Mathf.Max(0f, d - width * 0.5f);
            }
            default:
            {
                GetPolygon(out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d, out int count);
                return ShapeMath.ConvexPolygonDistance(p, a, b, c, d, count);
            }
        }
    }

    private void GetLongitudinalRange(out float z0, out float z1)
    {
        if (startAtOrigin)
        {
            z0 = 0f;
            z1 = length;
        }
        else
        {
            z0 = -length * 0.5f;
            z1 = length * 0.5f;
        }
    }

    /// <summary>Footprint polygon corners in local 2D (x, z), counter-clockwise seen from above is not required.</summary>
    public void GetPolygon(out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d, out int count)
    {
        GetLongitudinalRange(out float z0, out float z1);
        switch (type)
        {
            case HitShapeType.Triangle:
                a = new Vector2(0f, z0);
                b = new Vector2(width * 0.5f, z1);
                c = new Vector2(-width * 0.5f, z1);
                d = c;
                count = 3;
                return;
            case HitShapeType.Trapezoid:
                a = new Vector2(-nearWidth * 0.5f, z0);
                b = new Vector2(nearWidth * 0.5f, z0);
                c = new Vector2(width * 0.5f, z1);
                d = new Vector2(-width * 0.5f, z1);
                count = 4;
                return;
            default: // Square, Rectangle
                a = new Vector2(-width * 0.5f, z0);
                b = new Vector2(width * 0.5f, z0);
                c = new Vector2(width * 0.5f, z1);
                d = new Vector2(-width * 0.5f, z1);
                count = 4;
                return;
        }
    }

    /// <summary>Largest horizontal distance of the footprint from its origin.</summary>
    public float LocalReach()
    {
        switch (type)
        {
            case HitShapeType.Circle:
            case HitShapeType.Cylinder:
            case HitShapeType.Ring:
            case HitShapeType.Cone:
            case HitShapeType.Sphere:
                return radius;
            case HitShapeType.Line:
            {
                GetLongitudinalRange(out float z0, out float z1);
                return Mathf.Max(Mathf.Abs(z0), Mathf.Abs(z1)) + width * 0.5f;
            }
            default:
            {
                GetPolygon(out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d, out int count);
                float m = Mathf.Max(a.magnitude, Mathf.Max(b.magnitude, c.magnitude));
                return count == 4 ? Mathf.Max(m, d.magnitude) : m;
            }
        }
    }

    /// <summary>World-space bounding sphere of the shape (for the physics broad phase).</summary>
    public void GetBounds(out Vector3 center, out float boundRadius)
    {
        if (type == HitShapeType.Sphere)
        {
            center = origin;
            boundRadius = radius;
            return;
        }

        // Footprint bounding box in local 2D.
        Vector2 min, max;
        switch (type)
        {
            case HitShapeType.Circle:
            case HitShapeType.Cylinder:
            case HitShapeType.Ring:
                min = new Vector2(-radius, -radius);
                max = new Vector2(radius, radius);
                break;
            case HitShapeType.Cone:
                if (halfAngle >= 90f)
                {
                    // Wider than a half circle: reaches sideways fully and partly behind the apex.
                    float back = halfAngle >= 180f ? -radius : Mathf.Min(0f, radius * Mathf.Cos(halfAngle * Mathf.Deg2Rad));
                    min = new Vector2(-radius, back);
                    max = new Vector2(radius, radius);
                }
                else
                {
                    float s = Mathf.Sin(halfAngle * Mathf.Deg2Rad) * radius;
                    min = new Vector2(-s, 0f);
                    max = new Vector2(s, radius);
                }
                break;
            case HitShapeType.Line:
            {
                GetLongitudinalRange(out float z0, out float z1);
                float hw = width * 0.5f;
                min = new Vector2(-hw, z0 - hw);
                max = new Vector2(hw, z1 + hw);
                break;
            }
            default:
            {
                GetPolygon(out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d, out int count);
                min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
                max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
                break;
            }
        }

        Vector2 mid = (min + max) * 0.5f;
        Vector2 half = (max - min) * 0.5f;
        float halfHeight = height * 0.5f;
        center = ToWorld(new Vector3(mid.x, baseOffset + halfHeight, mid.y));
        boundRadius = Mathf.Sqrt(half.x * half.x + half.y * half.y + halfHeight * halfHeight);
    }

    /// <summary>
    /// Horizontal direction that leaves the shape fastest from <paramref name="world"/> (for dodging), and how far
    /// that is (0 if the point is already outside).
    /// </summary>
    public Vector3 EscapeDirection(Vector3 world, out float distance)
    {
        Vector3 local = ToLocal(world);
        Vector2 p = new Vector2(local.x, local.z);
        Vector2 dir;
        distance = 0f;

        switch (type)
        {
            case HitShapeType.Sphere:
            case HitShapeType.Circle:
            case HitShapeType.Cylinder:
            {
                float d = p.magnitude;
                dir = d > 1e-4f ? p / d : new Vector2(1f, 0f);
                distance = Mathf.Max(0f, radius - d);
                break;
            }
            case HitShapeType.Ring:
            {
                float d = p.magnitude;
                Vector2 radial = d > 1e-4f ? p / d : new Vector2(1f, 0f);
                float outward = radius - d, inward = d - inner;
                if (inward < outward)
                {
                    dir = -radial;
                    distance = Mathf.Max(0f, inward);
                }
                else
                {
                    dir = radial;
                    distance = Mathf.Max(0f, outward);
                }
                break;
            }
            case HitShapeType.Cone:
            {
                // Sideways out of the nearest edge, or out through the arc - whichever is shorter.
                float d = p.magnitude;
                float a = Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg;
                float toEdge = (halfAngle - Mathf.Abs(a)) * Mathf.Deg2Rad * Mathf.Max(d, 0.5f);
                float toArc = radius - d;
                if (halfAngle < 180f && toEdge < toArc)
                {
                    float side = a >= 0f ? 1f : -1f;
                    float edgeAngle = (halfAngle * side) * Mathf.Deg2Rad;
                    Vector2 edgeDir = new Vector2(Mathf.Sin(edgeAngle), Mathf.Cos(edgeAngle));
                    dir = new Vector2(edgeDir.y * side, -edgeDir.x * side); // perpendicular, pointing outward
                    distance = Mathf.Max(0f, toEdge);
                }
                else
                {
                    dir = d > 1e-4f ? p / d : new Vector2(0f, 1f);
                    distance = Mathf.Max(0f, toArc);
                }
                break;
            }
            case HitShapeType.Line:
            {
                float side = p.x >= 0f ? 1f : -1f;
                dir = new Vector2(side, 0f);
                distance = Mathf.Max(0f, width * 0.5f - Mathf.Abs(p.x));
                break;
            }
            default:
            {
                GetPolygon(out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d, out int count);
                dir = ShapeMath.ConvexPolygonExit(p, a, b, c, d, count, out distance);
                break;
            }
        }

        Vector3 world3 = rotation * new Vector3(dir.x, 0f, dir.y);
        world3.y = 0f;
        return world3.sqrMagnitude > 1e-6f ? world3.normalized : Vector3.right;
    }

    /// <summary>
    /// Writes the footprint outline in world space (at the base of the vertical band + <paramref name="lift"/>).
    /// <paramref name="inner"/> receives a second loop for rings. Both lists are cleared first.
    /// </summary>
    public void GetOutline(List<Vector3> outer, List<Vector3> innerLoop, int segments = 32, float lift = 0.05f)
    {
        outer.Clear();
        innerLoop?.Clear();
        float y = lift; // drawn at the origin's height (the ground for ground-placed abilities)
        segments = Mathf.Max(8, segments);

        switch (type)
        {
            case HitShapeType.Circle:
            case HitShapeType.Cylinder:
            case HitShapeType.Sphere:
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments * Mathf.PI * 2f;
                    outer.Add(ToWorld(new Vector3(Mathf.Sin(t) * radius, y, Mathf.Cos(t) * radius)));
                }
                break;
            case HitShapeType.Ring:
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments * Mathf.PI * 2f;
                    outer.Add(ToWorld(new Vector3(Mathf.Sin(t) * radius, y, Mathf.Cos(t) * radius)));
                    innerLoop?.Add(ToWorld(new Vector3(Mathf.Sin(t) * inner, y, Mathf.Cos(t) * inner)));
                }
                break;
            case HitShapeType.Cone:
            {
                int arcSegments = Mathf.Max(4, Mathf.CeilToInt(segments * halfAngle / 180f));
                if (inner <= 0.001f && halfAngle < 180f)
                    outer.Add(ToWorld(new Vector3(0f, y, 0f)));
                for (int i = 0; i <= arcSegments; i++)
                {
                    float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)arcSegments) * Mathf.Deg2Rad;
                    outer.Add(ToWorld(new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius)));
                }
                if (inner > 0.001f)
                {
                    for (int i = arcSegments; i >= 0; i--)
                    {
                        float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)arcSegments) * Mathf.Deg2Rad;
                        outer.Add(ToWorld(new Vector3(Mathf.Sin(a) * inner, y, Mathf.Cos(a) * inner)));
                    }
                }
                outer.Add(outer[0]);
                break;
            }
            case HitShapeType.Line:
            {
                GetLongitudinalRange(out float z0, out float z1);
                float hw = width * 0.5f;
                int cap = 6;
                for (int i = 0; i <= cap; i++)
                {
                    float a = Mathf.Lerp(-90f, 90f, i / (float)cap) * Mathf.Deg2Rad;
                    outer.Add(ToWorld(new Vector3(Mathf.Sin(a) * hw, y, z1 + Mathf.Cos(a) * hw)));
                }
                for (int i = 0; i <= cap; i++)
                {
                    float a = Mathf.Lerp(90f, 270f, i / (float)cap) * Mathf.Deg2Rad;
                    outer.Add(ToWorld(new Vector3(Mathf.Sin(a) * hw, y, z0 + Mathf.Cos(a) * hw)));
                }
                outer.Add(outer[0]);
                break;
            }
            default:
            {
                GetPolygon(out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d, out int count);
                outer.Add(ToWorld(new Vector3(a.x, y, a.y)));
                outer.Add(ToWorld(new Vector3(b.x, y, b.y)));
                outer.Add(ToWorld(new Vector3(c.x, y, c.y)));
                if (count == 4)
                    outer.Add(ToWorld(new Vector3(d.x, y, d.y)));
                outer.Add(outer[0]);
                break;
            }
        }
    }
}

/// <summary>Allocation-free 2D geometry used by the hit shapes (pure math; unit tested).</summary>
public static class ShapeMath
{
    public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    public static float DistancePointSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
        return (p - (a + ab * t)).magnitude;
    }

    /// <summary>Distance from p to a convex polygon (3 or 4 corners, either winding): 0 inside.</summary>
    public static float ConvexPolygonDistance(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int count)
    {
        if (count < 4)
            d = a;
        if (PointInConvex(p, a, b, c, d, count))
            return 0f;
        float m = Mathf.Min(DistancePointSegment(p, a, b), DistancePointSegment(p, b, c));
        if (count == 4)
            m = Mathf.Min(m, Mathf.Min(DistancePointSegment(p, c, d), DistancePointSegment(p, d, a)));
        else
            m = Mathf.Min(m, DistancePointSegment(p, c, a));
        return m;
    }

    public static bool PointInConvex(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int count)
    {
        float s1 = Cross(b - a, p - a);
        float s2 = Cross(c - b, p - b);
        float s3, s4;
        if (count == 4)
        {
            s3 = Cross(d - c, p - c);
            s4 = Cross(a - d, p - d);
        }
        else
        {
            s3 = Cross(a - c, p - c);
            s4 = s3;
        }
        bool hasNeg = s1 < 0f || s2 < 0f || s3 < 0f || s4 < 0f;
        bool hasPos = s1 > 0f || s2 > 0f || s3 > 0f || s4 > 0f;
        return !(hasNeg && hasPos);
    }

    /// <summary>Outward direction through the nearest polygon edge from an inside point (and the distance to it).</summary>
    public static Vector2 ConvexPolygonExit(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d, int count, out float distance)
    {
        Vector2 centroid = count == 4 ? (a + b + c + d) * 0.25f : (a + b + c) / 3f;
        distance = float.MaxValue;
        Vector2 best = new Vector2(1f, 0f);
        Edge(p, a, b, centroid, ref distance, ref best);
        Edge(p, b, c, centroid, ref distance, ref best);
        if (count == 4)
        {
            Edge(p, c, d, centroid, ref distance, ref best);
            Edge(p, d, a, centroid, ref distance, ref best);
        }
        else
        {
            Edge(p, c, a, centroid, ref distance, ref best);
        }
        if (!PointInConvex(p, a, b, c, d, count))
            distance = 0f;
        return best;
    }

    private static void Edge(Vector2 p, Vector2 a, Vector2 b, Vector2 centroid, ref float bestDistance, ref Vector2 bestDir)
    {
        Vector2 e = b - a;
        Vector2 n = new Vector2(e.y, -e.x);
        if (n.sqrMagnitude < 1e-8f)
            return;
        n.Normalize();
        if (Vector2.Dot(n, centroid - a) > 0f)
            n = -n; // make it point outward
        float dist = Vector2.Dot(a - p, n);
        dist = Mathf.Abs(dist);
        if (dist < bestDistance)
        {
            bestDistance = dist;
            bestDir = n;
        }
    }

    /// <summary>Distance from p to a circular sector (apex at the origin, facing +y): 0 inside.</summary>
    public static float SectorDistance(Vector2 p, float radius, float inner, float halfAngleDeg)
    {
        float d = p.magnitude;
        if (halfAngleDeg >= 180f)
        {
            if (d > radius) return d - radius;
            if (d < inner) return inner - d;
            return 0f;
        }

        float angle = Mathf.Abs(Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg);
        if (angle <= halfAngleDeg)
        {
            if (d > radius) return d - radius;
            if (d < inner) return inner - d;
            return 0f;
        }

        float h = halfAngleDeg * Mathf.Deg2Rad;
        Vector2 left = new Vector2(-Mathf.Sin(h), Mathf.Cos(h));
        Vector2 right = new Vector2(Mathf.Sin(h), Mathf.Cos(h));
        float dl = DistancePointSegment(p, left * inner, left * radius);
        float dr = DistancePointSegment(p, right * inner, right * radius);
        return Mathf.Min(dl, dr);
    }
}
