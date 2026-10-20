using System;
using UnityEngine;

/// <summary>
/// Where a body part sits on a human figure, for drawing it (the profile and controller inspectors, the Scene view):
/// which region a part is (from its name, else its aliases: "LeftForearm" is a forearm, "Arms" a whole arm), the shape of
/// that region on a front-view figure, and one colour per region (left and right the same). Drawing only: hits still
/// use the parts' heights, sides and hitboxes.
/// </summary>
public static class BodyPartLayout
{
    /// <summary>A region of a human figure.</summary>
    public enum Region
    {
        Other,
        Head,
        Neck,
        UpperTorso,
        LowerTorso,
        Groin,
        /// <summary>The whole torso (a simple profile).</summary>
        Torso,
        UpperArm,
        Forearm,
        Hand,
        /// <summary>The whole arm, hand included (a simple profile).</summary>
        Arm,
        Thigh,
        Shin,
        Foot,
        /// <summary>The whole leg, foot included (a simple profile).</summary>
        Leg,
    }

    /// <summary>The region of a part: from its name, else from its aliases (Other when nothing fits).</summary>
    public static Region Classify(BodyPartDefinition part)
    {
        if (part == null)
            return Region.Other;
        Region r = Classify(part.name);
        if (r != Region.Other || part.aliases == null)
            return r;
        foreach (string a in part.aliases)
        {
            r = Classify(a);
            if (r != Region.Other)
                return r;
        }
        return Region.Other;
    }

    /// <summary>The region a part name describes ("RightShin", "Abdomen", "Legs"...).</summary>
    public static Region Classify(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Region.Other;
        string n = name.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        // Most specific first: "upperarm" before "arm", "lowertorso" before "torso".
        if (Has(n, "head", "face", "skull", "eye", "jaw")) return Region.Head;
        if (Has(n, "neck", "throat")) return Region.Neck;
        if (Has(n, "uppertorso", "chest", "upperbody", "ribs", "breast")) return Region.UpperTorso;
        if (Has(n, "lowertorso", "abdomen", "belly", "stomach", "waist", "lowerbody")) return Region.LowerTorso;
        if (Has(n, "groin", "pelvis", "hip", "crotch")) return Region.Groin;
        if (Has(n, "upperarm", "shoulder", "bicep")) return Region.UpperArm;
        if (Has(n, "forearm", "lowerarm", "elbow", "wrist")) return Region.Forearm;
        if (Has(n, "hand", "fist", "finger", "palm")) return Region.Hand;
        if (Has(n, "thigh", "upperleg")) return Region.Thigh;
        if (Has(n, "shin", "lowerleg", "calf", "knee")) return Region.Shin;
        if (Has(n, "foot", "feet", "toe", "ankle", "heel")) return Region.Foot;
        if (Has(n, "arm")) return Region.Arm;
        if (Has(n, "leg")) return Region.Leg;
        if (Has(n, "torso", "body", "back", "spine", "trunk")) return Region.Torso;
        return Region.Other;
    }

    private static bool Has(string n, params string[] words)
    {
        foreach (string w in words)
            if (n.Contains(w))
                return true;
        return false;
    }

    /// <summary>Is the region on the sides (limbs) rather than the centre line?</summary>
    public static bool IsLimb(Region r) => r >= Region.UpperArm && r <= Region.Leg;

    /// <summary>
    /// Which sides a part is drawn on: a centre part (side 0), the character's left (-1), right (+1), or both (a part
    /// "on either side", or a limb part with no side such as "Arms").
    /// </summary>
    public static void Sides(BodyPartDefinition part, Region region, out bool left, out bool right)
    {
        BodySide s = part != null ? part.side : BodySide.Any;
        left = s == BodySide.Left || s == BodySide.EitherSide;
        right = s == BodySide.Right || s == BodySide.EitherSide;
        if (s == BodySide.Any && IsLimb(region))
        {
            // No side but a limb: guess it from the name ("LeftHand"), else both sides.
            string n = part != null && part.name != null ? part.name.ToLowerInvariant() : "";
            left = !n.StartsWith("right");
            right = !n.StartsWith("left");
        }
    }

    /// <summary>
    /// The outline of a region on a front-view figure, for the character's RIGHT side (which a front view shows on the
    /// viewer's left; <see cref="Mirror"/> gives the left side). Points are (x across 0-1, y down from the top 0-1) of a
    /// box as wide as half the figure's height, in order around the outline (star-shaped from its centre). Centre regions
    /// are symmetric. Null for Other.
    /// </summary>
    public static Vector2[] Shape(Region r)
    {
        switch (r)
        {
            case Region.Head: return Ellipse(0.5f, 0.065f, 0.085f, 0.068f, 14);
            case Region.Neck: return Quad(0.455f, 0.13f, 0.545f, 0.13f, 0.555f, 0.165f, 0.445f, 0.165f);
            case Region.UpperTorso: return Poly(0.36f, 0.165f, 0.64f, 0.165f, 0.69f, 0.18f, 0.71f, 0.22f, 0.66f, 0.37f, 0.34f, 0.37f, 0.29f, 0.22f, 0.31f, 0.18f);
            case Region.LowerTorso: return Quad(0.34f, 0.37f, 0.66f, 0.37f, 0.65f, 0.48f, 0.35f, 0.48f);
            case Region.Groin: return Poly(0.35f, 0.48f, 0.65f, 0.48f, 0.64f, 0.52f, 0.5f, 0.56f, 0.36f, 0.52f);
            case Region.Torso: return Poly(0.36f, 0.165f, 0.64f, 0.165f, 0.69f, 0.18f, 0.71f, 0.22f, 0.66f, 0.37f, 0.65f, 0.52f, 0.5f, 0.56f, 0.35f, 0.52f, 0.34f, 0.37f, 0.29f, 0.22f, 0.31f, 0.18f);
            case Region.UpperArm: return Quad(0.225f, 0.185f, 0.295f, 0.19f, 0.275f, 0.35f, 0.195f, 0.345f);
            case Region.Forearm: return Quad(0.195f, 0.35f, 0.275f, 0.355f, 0.245f, 0.5f, 0.17f, 0.495f);
            case Region.Hand: return Poly(0.17f, 0.5f, 0.245f, 0.505f, 0.25f, 0.545f, 0.225f, 0.585f, 0.175f, 0.58f, 0.155f, 0.54f);
            case Region.Arm: return Poly(0.225f, 0.185f, 0.295f, 0.19f, 0.275f, 0.355f, 0.245f, 0.505f, 0.25f, 0.545f, 0.225f, 0.585f, 0.175f, 0.58f, 0.155f, 0.54f, 0.17f, 0.495f, 0.195f, 0.345f);
            case Region.Thigh: return Quad(0.355f, 0.52f, 0.495f, 0.555f, 0.475f, 0.75f, 0.375f, 0.75f);
            case Region.Shin: return Quad(0.378f, 0.75f, 0.472f, 0.75f, 0.462f, 0.935f, 0.39f, 0.935f);
            case Region.Foot: return Poly(0.39f, 0.935f, 0.462f, 0.935f, 0.468f, 0.985f, 0.34f, 0.985f, 0.345f, 0.96f);
            case Region.Leg: return Poly(0.355f, 0.52f, 0.495f, 0.555f, 0.475f, 0.75f, 0.462f, 0.935f, 0.468f, 0.985f, 0.34f, 0.985f, 0.345f, 0.96f, 0.39f, 0.935f, 0.375f, 0.75f);
            default: return null;
        }
    }

    /// <summary>The same outline on the other side of the figure.</summary>
    public static Vector2[] Mirror(Vector2[] shape)
    {
        if (shape == null)
            return null;
        var m = new Vector2[shape.Length];
        for (int i = 0; i < shape.Length; i++)
            m[shape.Length - 1 - i] = new Vector2(1f - shape[i].x, shape[i].y); // reversed: the winding stays the same
        return m;
    }

    /// <summary>The bounds of an outline (x and y as in <see cref="Shape"/>).</summary>
    public static Rect Bounds(Vector2[] shape)
    {
        if (shape == null || shape.Length == 0)
            return Rect.zero;
        Vector2 min = shape[0], max = shape[0];
        foreach (Vector2 p in shape)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>Is <paramref name="p"/> inside the outline?</summary>
    public static bool Contains(Vector2[] shape, Vector2 p)
    {
        if (shape == null || shape.Length < 3)
            return false;
        bool inside = false;
        for (int i = 0, j = shape.Length - 1; i < shape.Length; j = i++)
        {
            Vector2 a = shape[i], b = shape[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>The colour of a region (left and right the same; a whole limb takes its upper segment's).</summary>
    public static Color RegionColor(Region r)
    {
        switch (r)
        {
            case Region.Head: return new Color(0.95f, 0.38f, 0.38f);
            case Region.Neck: return new Color(1f, 0.62f, 0.3f);
            case Region.UpperTorso:
            case Region.Torso: return new Color(0.35f, 0.62f, 1f);
            case Region.LowerTorso: return new Color(0.4f, 0.8f, 0.95f);
            case Region.Groin: return new Color(0.35f, 0.85f, 0.75f);
            case Region.UpperArm:
            case Region.Arm: return new Color(0.4f, 0.82f, 0.42f);
            case Region.Forearm: return new Color(0.62f, 0.9f, 0.4f);
            case Region.Hand: return new Color(0.88f, 0.9f, 0.35f);
            case Region.Thigh:
            case Region.Leg: return new Color(0.68f, 0.5f, 1f);
            case Region.Shin: return new Color(0.85f, 0.55f, 0.95f);
            case Region.Foot: return new Color(1f, 0.55f, 0.75f);
            default: return new Color(0.7f, 0.7f, 0.7f);
        }
    }

    /// <summary>A short label: "Forearm" for "LeftForearm" (the side shows on the figure), "Upper Torso"...</summary>
    public static string ShortName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        string n = name;
        if (n.StartsWith("Left", StringComparison.Ordinal) && n.Length > 4 && char.IsUpper(n[4])) n = n.Substring(4);
        else if (n.StartsWith("Right", StringComparison.Ordinal) && n.Length > 5 && char.IsUpper(n[5])) n = n.Substring(5);
        var sb = new System.Text.StringBuilder(n.Length + 4);
        for (int i = 0; i < n.Length; i++)
        {
            if (i > 0 && char.IsUpper(n[i]) && char.IsLower(n[i - 1]))
                sb.Append(' ');
            sb.Append(n[i]);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ shapes
    private static Vector2[] Quad(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3) =>
        new[] { new Vector2(x0, y0), new Vector2(x1, y1), new Vector2(x2, y2), new Vector2(x3, y3) };

    private static Vector2[] Poly(params float[] xy)
    {
        var v = new Vector2[xy.Length / 2];
        for (int i = 0; i < v.Length; i++)
            v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        return v;
    }

    private static Vector2[] Ellipse(float cx, float cy, float rx, float ry, int n)
    {
        var v = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            v[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
        }
        return v;
    }
}
