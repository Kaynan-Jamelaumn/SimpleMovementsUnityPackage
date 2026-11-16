using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pictures of the injury system's UI, drawn in code (no art needed): one white icon per
/// <see cref="InjuryIconShape"/> - a drop for bleeding, a bone for fractures, a flame, an eye, a germ... - and the small
/// badges of the Health tab's body figure (treated, bleeding). An injury type with its own Icon sprite uses that instead.
/// The icons are white: the UI tints them (the type's colour, the severity's).
/// </summary>
public static class InjuryIcons
{
    private const int Size = 64;

    private static readonly Dictionary<InjuryIconShape, Sprite> cache = new Dictionary<InjuryIconShape, Sprite>();
    private static Sprite plus, cross, ring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cache.Clear();
        plus = cross = ring = null;
    }

    /// <summary>The icon of an injury type: its own sprite, else the picture of its Icon Shape (Auto: from its tags).</summary>
    public static Sprite For(InjuryTypeDefinition type)
    {
        if (type == null)
            return Shape(InjuryIconShape.Dot);
        if (type.icon != null)
            return type.icon;
        return Shape(Resolve(type));
    }

    /// <summary>The shape drawn for a type: the one it names, else chosen from its tags.</summary>
    public static InjuryIconShape Resolve(InjuryTypeDefinition type)
    {
        if (type == null)
            return InjuryIconShape.Dot;
        if (type.iconShape != InjuryIconShape.Auto)
            return type.iconShape;
        if (type.HasTag(InjuryDefaults.TagFracture)) return InjuryIconShape.Bone;
        if (type.HasTag(InjuryDefaults.TagCold)) return InjuryIconShape.Frost;
        if (type.HasTag(InjuryDefaults.TagBurn)) return InjuryIconShape.Flame;
        if (type.HasTag(InjuryDefaults.TagEye)) return InjuryIconShape.Eye;
        if (type.HasTag(InjuryDefaults.TagEar)) return InjuryIconShape.Ear;
        if (type.HasTag(InjuryDefaults.TagStump)) return InjuryIconShape.Stump;
        if (type.HasTag(InjuryDefaults.TagBite)) return InjuryIconShape.Fang;
        if (type.HasTag(InjuryDefaults.TagConcussion)) return InjuryIconShape.Dizzy;
        if (type.HasTag(InjuryDefaults.TagBruise)) return InjuryIconShape.Bruise;
        if (type.HasTag(InjuryDefaults.TagGangrene)) return InjuryIconShape.Skull;
        if (type.HasTag(InjuryDefaults.TagGut)) return InjuryIconShape.Stomach;
        if (type.HasTag(InjuryDefaults.TagPoison)) return InjuryIconShape.Fang;
        if (type.HasTag(InjuryDefaults.TagInfection)) return InjuryIconShape.Biohazard;
        if (type.systemic || type.HasTag(InjuryDefaults.TagIllness)) return InjuryIconShape.Germ;
        if (type.HasTag(InjuryDefaults.TagSprain) || type.HasTag(InjuryDefaults.TagDislocation)) return InjuryIconShape.Joint;
        if (type.HasTag(InjuryDefaults.TagWound)) return InjuryIconShape.Cut;
        if (type.HasTag(InjuryDefaults.TagBleeding)) return InjuryIconShape.Drop;
        return InjuryIconShape.Dot;
    }

    /// <summary>The white picture of a shape (made once).</summary>
    public static Sprite Shape(InjuryIconShape shape)
    {
        if (shape == InjuryIconShape.Auto)
            shape = InjuryIconShape.Dot;
        if (cache.TryGetValue(shape, out Sprite s) && s != null)
            return s;
        InjuryIconShape captured = shape;
        s = Make("Injury Icon " + shape, (x, y) => Distance(captured, x, y));
        cache[shape] = s;
        return s;
    }

    /// <summary>A plus: "treated" (bandaged, splinted, stitched).</summary>
    public static Sprite Plus() => plus != null ? plus : plus = Make("Injury Plus", (x, y) =>
        Mathf.Min(Box(x, y, 0f, 0f, 0.26f, 0.8f), Box(x, y, 0f, 0f, 0.8f, 0.26f)));

    /// <summary>A cross: a missing limb.</summary>
    public static Sprite Cross() => cross != null ? cross : cross = Make("Injury Cross", (x, y) =>
        Mathf.Min(Segment(x, y, -0.62f, -0.62f, 0.62f, 0.62f, 0.2f), Segment(x, y, -0.62f, 0.62f, 0.62f, -0.62f, 0.2f)));

    /// <summary>A ring: the selection around something.</summary>
    public static Sprite Ring() => ring != null ? ring : ring = Make("Injury Ring", (x, y) =>
        Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.8f) - 0.12f);

    // ------------------------------------------------------------------ drawing
    private delegate float Field(float x, float y);

    /// <summary>A white sprite whose alpha is the inside of a distance field (x and y from -1 to 1, y up; negative = inside).</summary>
    private static Sprite Make(string name, Field field)
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[Size * Size];
        float pixel = 2f / Size;
        for (int py = 0; py < Size; py++)
        {
            for (int pxl = 0; pxl < Size; pxl++)
            {
                float x = (pxl + 0.5f) / Size * 2f - 1f;
                float y = (py + 0.5f) / Size * 2f - 1f;
                float a = Mathf.Clamp01(0.5f - field(x, y) / pixel);
                px[py * Size + pxl] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        Sprite s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        s.name = name;
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    private static float Distance(InjuryIconShape shape, float x, float y)
    {
        switch (shape)
        {
            case InjuryIconShape.Drop:
            {
                // A round bottom and a pointed top.
                float body = Circle(x, y, 0f, -0.28f, 0.5f);
                float tip = Triangle(x, y, 0f, 0.86f, -0.46f, -0.1f, 0.46f, -0.1f);
                return Mathf.Min(body, tip);
            }
            case InjuryIconShape.Cut:
            {
                // A slash with two stitches across it.
                float slash = Segment(x, y, -0.62f, -0.6f, 0.62f, 0.6f, 0.13f);
                float a = Segment(x, y, -0.42f, 0.02f, -0.02f, -0.4f, 0.07f);
                float b = Segment(x, y, 0.02f, 0.42f, 0.42f, 0f, 0.07f);
                return Mathf.Min(slash, Mathf.Min(a, b));
            }
            case InjuryIconShape.Bone:
            {
                float shaft = Segment(x, y, -0.42f, -0.42f, 0.42f, 0.42f, 0.14f);
                float k1 = Mathf.Min(Circle(x, y, -0.62f, -0.4f, 0.2f), Circle(x, y, -0.4f, -0.62f, 0.2f));
                float k2 = Mathf.Min(Circle(x, y, 0.62f, 0.4f, 0.2f), Circle(x, y, 0.4f, 0.62f, 0.2f));
                float bone = Mathf.Min(shaft, Mathf.Min(k1, k2));
                // The break.
                return Mathf.Max(bone, -Segment(x, y, -0.22f, 0.22f, 0.22f, -0.22f, 0.07f));
            }
            case InjuryIconShape.Flame:
            {
                float outer = Mathf.Min(Circle(x, y, 0f, -0.3f, 0.52f), Triangle(x, y, 0.08f, 0.9f, -0.5f, -0.12f, 0.5f, -0.12f));
                float inner = Mathf.Min(Circle(x, y, 0f, -0.42f, 0.24f), Triangle(x, y, -0.04f, 0.2f, -0.23f, -0.34f, 0.23f, -0.34f));
                return Mathf.Max(outer, -inner);
            }
            case InjuryIconShape.Frost:
            {
                float d = float.MaxValue;
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    float cx = Mathf.Cos(a) * 0.8f, cy = Mathf.Sin(a) * 0.8f;
                    d = Mathf.Min(d, Segment(x, y, -cx, -cy, cx, cy, 0.09f));
                    // Small barbs near each tip.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float tx = cx * 0.62f * side, ty = cy * 0.62f * side;
                        float b1 = a + side * 0.9f, b2 = a - side * 0.9f;
                        d = Mathf.Min(d, Segment(x, y, tx, ty, tx + Mathf.Cos(b1) * 0.24f * side, ty + Mathf.Sin(b1) * 0.24f * side, 0.06f));
                        d = Mathf.Min(d, Segment(x, y, tx, ty, tx + Mathf.Cos(b2) * 0.24f * side, ty + Mathf.Sin(b2) * 0.24f * side, 0.06f));
                    }
                }
                return d;
            }
            case InjuryIconShape.Biohazard:
            {
                // Three rings around a core.
                float d = Circle(x, y, 0f, 0f, 0.16f);
                for (int i = 0; i < 3; i++)
                {
                    float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                    float cx = Mathf.Cos(a) * 0.4f, cy = Mathf.Sin(a) * 0.4f;
                    d = Mathf.Min(d, Mathf.Abs(Circle(x, y, cx, cy, 0.36f)) - 0.09f);
                }
                return d;
            }
            case InjuryIconShape.Bruise:
            {
                float blot = Mathf.Min(Circle(x, y, -0.12f, 0.05f, 0.5f), Circle(x, y, 0.3f, -0.18f, 0.36f));
                return Mathf.Max(blot, -Circle(x, y, -0.1f, 0.08f, 0.2f));
            }
            case InjuryIconShape.Eye:
            {
                // An almond with a pupil.
                float almond = Mathf.Max(Circle(x, y, 0f, -0.62f, 1.02f), Circle(x, y, 0f, 0.62f, 1.02f));
                float iris = Mathf.Abs(Circle(x, y, 0f, 0f, 0.26f)) - 0.07f;
                float outline = Mathf.Abs(almond) - 0.08f;
                return Mathf.Min(outline, Mathf.Min(iris, Circle(x, y, 0f, 0f, 0.1f)));
            }
            case InjuryIconShape.Germ:
            {
                float d = Circle(x, y, 0f, 0f, 0.42f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                    d = Mathf.Min(d, Segment(x, y, cx * 0.4f, cy * 0.4f, cx * 0.72f, cy * 0.72f, 0.06f));
                    d = Mathf.Min(d, Circle(x, y, cx * 0.78f, cy * 0.78f, 0.1f));
                }
                d = Mathf.Max(d, -Circle(x, y, -0.14f, 0.1f, 0.1f));
                return Mathf.Max(d, -Circle(x, y, 0.16f, -0.12f, 0.08f));
            }
            case InjuryIconShape.Stomach:
            {
                // A bean with the gullet coming in at the top.
                float bean = Mathf.Min(Circle(x, y, -0.2f, -0.18f, 0.48f), Circle(x, y, 0.26f, 0.0f, 0.4f));
                bean = Mathf.Max(bean, -Circle(x, y, 0.12f, 0.52f, 0.3f));
                float gullet = Segment(x, y, 0.42f, 0.3f, 0.42f, 0.84f, 0.11f);
                return Mathf.Min(bean, gullet);
            }
            case InjuryIconShape.Dizzy:
            {
                // Rings going round a dot.
                float d = Circle(x, y, 0f, 0f, 0.14f);
                d = Mathf.Min(d, Mathf.Max(Mathf.Abs(Circle(x, y, 0f, 0f, 0.42f)) - 0.08f, -Box(x, y, 0.4f, 0.3f, 0.3f, 0.2f)));
                return Mathf.Min(d, Mathf.Max(Mathf.Abs(Circle(x, y, 0f, 0f, 0.76f)) - 0.08f, -Box(x, y, -0.5f, -0.5f, 0.4f, 0.3f)));
            }
            case InjuryIconShape.Stump:
            {
                // A limb cut straight, with drops under the cut.
                float limb = Box(x, y, 0f, 0.36f, 0.34f, 0.5f);
                float d = Mathf.Min(limb, Box(x, y, 0f, -0.16f, 0.46f, 0.08f));
                d = Mathf.Min(d, Circle(x, y, -0.2f, -0.5f, 0.12f));
                return Mathf.Min(d, Circle(x, y, 0.18f, -0.7f, 0.12f));
            }
            case InjuryIconShape.Joint:
            {
                float upper = Segment(x, y, -0.6f, 0.62f, 0f, 0f, 0.15f);
                float lower = Segment(x, y, 0f, 0f, 0.62f, -0.5f, 0.15f);
                float joint = Mathf.Abs(Circle(x, y, 0f, 0f, 0.3f)) - 0.08f;
                return Mathf.Min(Mathf.Min(upper, lower), joint);
            }
            case InjuryIconShape.Skull:
            {
                float head = Circle(x, y, 0f, 0.16f, 0.6f);
                float jaw = Box(x, y, 0f, -0.5f, 0.32f, 0.24f);
                float d = Mathf.Min(head, jaw);
                d = Mathf.Max(d, -Circle(x, y, -0.25f, 0.14f, 0.17f));
                d = Mathf.Max(d, -Circle(x, y, 0.25f, 0.14f, 0.17f));
                d = Mathf.Max(d, -Box(x, y, -0.12f, -0.6f, 0.035f, 0.16f));
                return Mathf.Max(d, -Box(x, y, 0.12f, -0.6f, 0.035f, 0.16f));
            }
            case InjuryIconShape.Ear:
            {
                // An open ring with a lobe.
                float outer = Mathf.Abs(Circle(x, y, 0.05f, 0.14f, 0.56f)) - 0.11f;
                outer = Mathf.Max(outer, -Box(x, y, -0.5f, -0.2f, 0.4f, 0.3f));
                float inner = Mathf.Abs(Circle(x, y, 0.08f, 0.1f, 0.24f)) - 0.08f;
                inner = Mathf.Max(inner, -Box(x, y, -0.2f, 0.1f, 0.2f, 0.4f));
                return Mathf.Min(Mathf.Min(outer, inner), Circle(x, y, 0.1f, -0.62f, 0.2f));
            }
            case InjuryIconShape.Lungs:
            {
                float left = Ellipse(x, y, -0.4f, -0.12f, 0.32f, 0.56f);
                float right = Ellipse(x, y, 0.4f, -0.12f, 0.32f, 0.56f);
                float pipe = Segment(x, y, 0f, 0.86f, 0f, 0.3f, 0.08f);
                float fork = Mathf.Min(Segment(x, y, 0f, 0.3f, -0.28f, 0.08f, 0.07f), Segment(x, y, 0f, 0.3f, 0.28f, 0.08f, 0.07f));
                return Mathf.Min(Mathf.Min(left, right), Mathf.Min(pipe, fork));
            }
            case InjuryIconShape.Fang:
            {
                float gum = Box(x, y, 0f, 0.62f, 0.72f, 0.14f);
                float a = Triangle(x, y, -0.38f, -0.78f, -0.66f, 0.52f, -0.1f, 0.52f);
                float b = Triangle(x, y, 0.38f, -0.78f, 0.1f, 0.52f, 0.66f, 0.52f);
                return Mathf.Min(gum, Mathf.Min(a, b));
            }
            default:
                return Circle(x, y, 0f, 0f, 0.5f);
        }
    }

    // ------------------------------------------------------------------ distance fields
    private static float Circle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        // Near enough for icons: the circle's distance in the ellipse's own space, scaled back by its smaller radius.
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
    }

    private static float Box(float x, float y, float cx, float cy, float hx, float hy)
    {
        float dx = Mathf.Abs(x - cx) - hx, dy = Mathf.Abs(y - cy) - hy;
        float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
        return Mathf.Min(Mathf.Max(dx, dy), 0f) + Mathf.Sqrt(ox * ox + oy * oy);
    }

    private static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
    {
        float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
        float len = bax * bax + bay * bay;
        float h = len > 1e-8f ? Mathf.Clamp01((pax * bax + pay * bay) / len) : 0f;
        float dx = pax - bax * h, dy = pay - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    private static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
    {
        // Inside when on the same side of the three edges; outside, the distance to the nearest edge.
        float d1 = Edge(x, y, ax, ay, bx, by), d2 = Edge(x, y, bx, by, cx, cy), d3 = Edge(x, y, cx, cy, ax, ay);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
        float edge = Mathf.Min(Segment(x, y, ax, ay, bx, by, 0f), Mathf.Min(Segment(x, y, bx, by, cx, cy, 0f), Segment(x, y, cx, cy, ax, ay, 0f)));
        return neg && pos ? edge : -edge;
    }

    private static float Edge(float x, float y, float ax, float ay, float bx, float by) => (x - bx) * (ay - by) - (ax - bx) * (y - by);
}
