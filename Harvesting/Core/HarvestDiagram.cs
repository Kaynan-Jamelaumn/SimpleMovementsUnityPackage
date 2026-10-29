using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The drawings of bodies, for the anatomy view and the work surfaces: for each body plan, the outline of every region
/// slot (a quadruped from the side with its head to the right, a biped from the front, a bird from below with its wings
/// spread, a fish from the side, a serpent from above, an arthropod from above), the open body cavity with where each
/// organ lies, and the shapes of pieces (claws, fangs, horns, bones). Points are in a unit box: x right, y UP (0 bottom
/// .. 1 top); <see cref="Aspect"/> is the box's width over its height. Drawing only: hits use the regions' bands.
/// </summary>
public static class HarvestDiagram
{
    /// <summary>Width over height of a plan's drawing.</summary>
    public static float Aspect(HarvestBodyPlan plan)
    {
        switch (plan)
        {
            case HarvestBodyPlan.Quadruped: return 1.9f;
            case HarvestBodyPlan.Biped: return 0.62f;
            case HarvestBodyPlan.Avian: return 1.5f;
            case HarvestBodyPlan.Fish: return 2.3f;
            case HarvestBodyPlan.Serpent: return 1.5f;
            default: return 1.1f;
        }
    }

    /// <summary>The slots a plan draws (its regions name them).</summary>
    public static string[] Slots(HarvestBodyPlan plan)
    {
        switch (plan)
        {
            case HarvestBodyPlan.Quadruped:
                return new[] { "tail", "hindleg_r", "foreleg_r", "haunch", "back", "ribs", "belly", "shoulder", "neck", "head", "hindleg_l", "foreleg_l", "wing_r", "wing_l" };
            case HarvestBodyPlan.Biped:
                return new[] { "tail", "head", "neck", "chest", "belly", "pelvis", "arm_l", "arm_r", "leg_l", "leg_r" };
            case HarvestBodyPlan.Avian:
                return new[] { "tail", "wing_l", "wing_r", "leg_l", "leg_r", "belly", "breast", "neck", "head" };
            case HarvestBodyPlan.Fish:
                return new[] { "tail", "fins", "back", "belly", "head" };
            case HarvestBodyPlan.Serpent:
                return new[] { "tail", "body", "neck", "head" };
            default:
                return new[] { "legs_l", "legs_r", "tail", "abdomen", "thorax", "head" };
        }
    }

    private static readonly Dictionary<string, List<Vector2[]>> cache = new Dictionary<string, List<Vector2[]>>();

    /// <summary>The outline(s) of slot <paramref name="slot"/> of <paramref name="plan"/> (several polygons for legs), or an empty list.</summary>
    public static List<Vector2[]> Shape(HarvestBodyPlan plan, string slot)
    {
        if (string.IsNullOrEmpty(slot))
            return new List<Vector2[]>();
        string key = (int)plan + ":" + slot.ToLowerInvariant();
        if (cache.TryGetValue(key, out List<Vector2[]> s))
            return s;
        s = Build(plan, slot.ToLowerInvariant());
        cache[key] = s;
        return s;
    }

    /// <summary>The centre of a slot's drawing (its first polygon's middle), or the middle of the box.</summary>
    public static Vector2 Centre(HarvestBodyPlan plan, string slot)
    {
        List<Vector2[]> s = Shape(plan, slot);
        if (s.Count == 0)
            return new Vector2(0.5f, 0.5f);
        return Middle(s[0]);
    }

    /// <summary>Slots drawn behind the others (the far legs and wing), darker.</summary>
    public static bool IsFarSide(HarvestBodyPlan plan, string slot) =>
        plan == HarvestBodyPlan.Quadruped && (slot == "foreleg_r" || slot == "hindleg_r" || slot == "wing_r");

    private static List<Vector2[]> Build(HarvestBodyPlan plan, string slot)
    {
        var list = new List<Vector2[]>();
        switch (plan)
        {
            case HarvestBodyPlan.Quadruped: Quadruped(slot, list); break;
            case HarvestBodyPlan.Biped: Biped(slot, list); break;
            case HarvestBodyPlan.Avian: Avian(slot, list); break;
            case HarvestBodyPlan.Fish: Fish(slot, list); break;
            case HarvestBodyPlan.Serpent: Serpent(slot, list); break;
            default: Arthropod(slot, list); break;
        }
        return list;
    }

    // ------------------------------------------------------------------ plans
    private static void Quadruped(string slot, List<Vector2[]> l)
    {
        switch (slot)
        {
            case "tail": l.Add(P(0.02f, 0.72f, 0.11f, 0.65f, 0.13f, 0.73f, 0.06f, 0.82f)); break;
            case "haunch": l.Add(P(0.11f, 0.63f, 0.13f, 0.78f, 0.2f, 0.85f, 0.34f, 0.86f, 0.35f, 0.72f, 0.36f, 0.55f, 0.34f, 0.43f, 0.22f, 0.41f, 0.13f, 0.49f)); break;
            case "back": l.Add(P(0.34f, 0.86f, 0.62f, 0.89f, 0.64f, 0.75f, 0.35f, 0.72f)); break;
            case "ribs": l.Add(P(0.35f, 0.72f, 0.64f, 0.75f, 0.65f, 0.57f, 0.36f, 0.55f)); break;
            case "belly": l.Add(P(0.36f, 0.55f, 0.65f, 0.57f, 0.64f, 0.45f, 0.5f, 0.42f, 0.36f, 0.43f)); break;
            case "shoulder": l.Add(P(0.62f, 0.89f, 0.73f, 0.91f, 0.8f, 0.81f, 0.81f, 0.56f, 0.75f, 0.45f, 0.64f, 0.45f, 0.65f, 0.57f, 0.64f, 0.75f)); break;
            case "neck": l.Add(P(0.73f, 0.91f, 0.84f, 0.88f, 0.87f, 0.75f, 0.81f, 0.62f, 0.81f, 0.56f, 0.8f, 0.81f)); break;
            case "head": l.Add(P(0.83f, 0.89f, 0.88f, 0.96f, 0.93f, 0.91f, 0.99f, 0.79f, 0.98f, 0.73f, 0.9f, 0.71f, 0.86f, 0.73f, 0.87f, 0.75f)); break;
            case "foreleg_l": l.Add(P(0.66f, 0.47f, 0.75f, 0.47f, 0.74f, 0.23f, 0.76f, 0.04f, 0.68f, 0.02f, 0.67f, 0.23f)); break;
            case "foreleg_r": l.Add(Shift(P(0.66f, 0.47f, 0.75f, 0.47f, 0.74f, 0.23f, 0.76f, 0.04f, 0.68f, 0.02f, 0.67f, 0.23f), 0.045f, 0.01f)); break;
            case "hindleg_l": l.Add(P(0.16f, 0.49f, 0.27f, 0.44f, 0.25f, 0.27f, 0.27f, 0.04f, 0.2f, 0.02f, 0.19f, 0.25f, 0.15f, 0.37f)); break;
            case "hindleg_r": l.Add(Shift(P(0.16f, 0.49f, 0.27f, 0.44f, 0.25f, 0.27f, 0.27f, 0.04f, 0.2f, 0.02f, 0.19f, 0.25f, 0.15f, 0.37f), -0.045f, 0.01f)); break;
            case "wing_l": l.Add(P(0.4f, 0.87f, 0.47f, 0.99f, 0.66f, 1f, 0.6f, 0.88f)); break;
            case "wing_r": l.Add(Shift(P(0.4f, 0.87f, 0.47f, 0.99f, 0.66f, 1f, 0.6f, 0.88f), -0.05f, -0.015f)); break;
        }
    }

    private static void Biped(string slot, List<Vector2[]> l)
    {
        switch (slot)
        {
            case "head": l.Add(Ellipse(0.5f, 0.925f, 0.085f, 0.065f, 16)); break;
            case "neck": l.Add(P(0.45f, 0.865f, 0.55f, 0.865f, 0.56f, 0.83f, 0.44f, 0.83f)); break;
            case "chest": l.Add(P(0.35f, 0.83f, 0.65f, 0.83f, 0.71f, 0.8f, 0.68f, 0.64f, 0.32f, 0.64f, 0.29f, 0.8f)); break;
            case "belly": l.Add(P(0.32f, 0.64f, 0.68f, 0.64f, 0.67f, 0.52f, 0.33f, 0.52f)); break;
            case "pelvis": l.Add(P(0.33f, 0.52f, 0.67f, 0.52f, 0.65f, 0.46f, 0.5f, 0.43f, 0.35f, 0.46f)); break;
            case "arm_l": l.Add(P(0.71f, 0.8f, 0.78f, 0.79f, 0.81f, 0.62f, 0.85f, 0.43f, 0.79f, 0.41f, 0.74f, 0.6f, 0.69f, 0.66f)); break;
            case "arm_r": l.Add(Mirror(P(0.71f, 0.8f, 0.78f, 0.79f, 0.81f, 0.62f, 0.85f, 0.43f, 0.79f, 0.41f, 0.74f, 0.6f, 0.69f, 0.66f))); break;
            case "leg_l": l.Add(P(0.51f, 0.45f, 0.65f, 0.47f, 0.63f, 0.24f, 0.62f, 0.04f, 0.67f, 0.01f, 0.53f, 0.01f, 0.54f, 0.24f)); break;
            case "leg_r": l.Add(Mirror(P(0.51f, 0.45f, 0.65f, 0.47f, 0.63f, 0.24f, 0.62f, 0.04f, 0.67f, 0.01f, 0.53f, 0.01f, 0.54f, 0.24f))); break;
            case "tail": l.Add(P(0.5f, 0.44f, 0.55f, 0.44f, 0.75f, 0.2f, 0.72f, 0.17f)); break;
        }
    }

    private static void Avian(string slot, List<Vector2[]> l)
    {
        switch (slot)
        {
            case "head": l.Add(Ellipse(0.5f, 0.86f, 0.065f, 0.07f, 14)); l.Add(P(0.47f, 0.84f, 0.53f, 0.84f, 0.5f, 0.76f)); break;
            case "neck": l.Add(P(0.46f, 0.81f, 0.54f, 0.81f, 0.555f, 0.76f, 0.445f, 0.76f)); break;
            case "breast": l.Add(P(0.42f, 0.76f, 0.58f, 0.76f, 0.62f, 0.63f, 0.56f, 0.5f, 0.44f, 0.5f, 0.38f, 0.63f)); break;
            case "belly": l.Add(P(0.44f, 0.5f, 0.56f, 0.5f, 0.55f, 0.4f, 0.45f, 0.4f)); break;
            case "wing_l": l.Add(P(0.58f, 0.75f, 0.75f, 0.81f, 0.95f, 0.79f, 1f, 0.7f, 0.86f, 0.61f, 0.7f, 0.57f, 0.61f, 0.62f)); break;
            case "wing_r": l.Add(Mirror(P(0.58f, 0.75f, 0.75f, 0.81f, 0.95f, 0.79f, 1f, 0.7f, 0.86f, 0.61f, 0.7f, 0.57f, 0.61f, 0.62f))); break;
            case "tail": l.Add(P(0.46f, 0.41f, 0.54f, 0.41f, 0.61f, 0.2f, 0.5f, 0.15f, 0.39f, 0.2f)); break;
            case "leg_l": l.Add(P(0.515f, 0.42f, 0.55f, 0.42f, 0.565f, 0.28f, 0.61f, 0.23f, 0.54f, 0.23f, 0.52f, 0.3f)); break;
            case "leg_r": l.Add(Mirror(P(0.515f, 0.42f, 0.55f, 0.42f, 0.565f, 0.28f, 0.61f, 0.23f, 0.54f, 0.23f, 0.52f, 0.3f))); break;
        }
    }

    private static void Fish(string slot, List<Vector2[]> l)
    {
        switch (slot)
        {
            case "tail": l.Add(P(0f, 0.86f, 0.17f, 0.57f, 0.17f, 0.45f, 0f, 0.14f, 0.07f, 0.5f)); break;
            case "back": l.Add(P(0.16f, 0.61f, 0.4f, 0.77f, 0.7f, 0.79f, 0.84f, 0.71f, 0.84f, 0.55f, 0.16f, 0.52f)); break;
            case "belly": l.Add(P(0.16f, 0.52f, 0.84f, 0.55f, 0.82f, 0.4f, 0.6f, 0.28f, 0.35f, 0.31f, 0.16f, 0.44f)); break;
            case "head": l.Add(P(0.84f, 0.71f, 0.94f, 0.65f, 1f, 0.54f, 0.96f, 0.44f, 0.82f, 0.4f, 0.84f, 0.55f)); break;
            case "fins": l.Add(P(0.42f, 0.77f, 0.5f, 0.94f, 0.63f, 0.79f)); l.Add(P(0.74f, 0.45f, 0.64f, 0.27f, 0.69f, 0.43f)); break;
        }
    }

    private static void Serpent(string slot, List<Vector2[]> l)
    {
        switch (slot)
        {
            case "tail": l.Add(SerpentBand(0f, 0.22f)); break;
            case "body": l.Add(SerpentBand(0.22f, 0.76f)); break;
            case "neck": l.Add(SerpentBand(0.76f, 0.88f)); break;
            case "head": l.Add(SerpentBand(0.88f, 1f)); break;
        }
    }

    private static void Arthropod(string slot, List<Vector2[]> l)
    {
        switch (slot)
        {
            case "head": l.Add(Ellipse(0.5f, 0.82f, 0.075f, 0.065f, 14)); l.Add(P(0.47f, 0.77f, 0.49f, 0.7f, 0.455f, 0.73f)); l.Add(P(0.53f, 0.77f, 0.51f, 0.7f, 0.545f, 0.73f)); break;
            case "thorax": l.Add(Ellipse(0.5f, 0.67f, 0.13f, 0.1f, 18)); break;
            case "abdomen": l.Add(Ellipse(0.5f, 0.36f, 0.18f, 0.22f, 22)); break;
            case "legs_l":
            case "legs_r":
                for (int i = 0; i < 4; i++)
                {
                    float y = 0.73f - i * 0.045f;
                    float reach = 0.3f + (i == 1 || i == 2 ? 0.05f : 0f);
                    float knee = i < 2 ? 0.12f : -0.1f;
                    Vector2[] leg = Limb(new Vector2(0.6f, y), new Vector2(0.6f + reach * 0.55f, y + 0.08f + knee * 0.3f), new Vector2(0.6f + reach, y - 0.16f + knee), 0.018f);
                    l.Add(slot == "legs_r" ? Mirror(leg) : leg);
                }
                break;
            case "tail": l.Add(P(0.47f, 0.15f, 0.53f, 0.15f, 0.55f, 0.04f, 0.5f, 0f, 0.45f, 0.04f)); break;
        }
    }

    /// <summary>A band of the serpent's S-shaped body from <paramref name="t0"/> to <paramref name="t1"/> (0 the tail tip .. 1 the snout).</summary>
    private static Vector2[] SerpentBand(float t0, float t1)
    {
        const int steps = 14;
        var left = new List<Vector2>();
        var right = new List<Vector2>();
        for (int i = 0; i <= steps; i++)
        {
            float t = t0 + (t1 - t0) * i / steps;
            Vector2 p = SerpentSpine(t);
            Vector2 d = SerpentSpine(Math.Min(1f, t + 0.01f)) - SerpentSpine(Math.Max(0f, t - 0.01f));
            Vector2 n = new Vector2(-d.y, d.x).normalized;
            float w = SerpentWidth(t);
            left.Add(p + n * w);
            right.Add(p - n * w);
        }
        right.Reverse();
        left.AddRange(right);
        return left.ToArray();
    }

    /// <summary>The serpent's spine: an S across the box.</summary>
    public static Vector2 SerpentSpine(float t) =>
        new Vector2(0.06f + 0.86f * t, 0.5f + 0.28f * (float)Math.Sin(t * Math.PI * 2.2) * (0.4f + 0.6f * t));

    private static float SerpentWidth(float t) => t < 0.15f ? 0.012f + 0.33f * t * 0.15f : t > 0.92f ? 0.065f - (t - 0.92f) * 0.4f : t > 0.82f ? 0.06f : 0.05f + 0.02f * (float)Math.Sin(t * Math.PI);

    // ------------------------------------------------------------------ opening the hide
    /// <summary>Width over height of the belly-up drawing (a quadruped on its back; the others as drawn).</summary>
    public static float BellyUpAspect(HarvestBodyPlan plan) => plan == HarvestBodyPlan.Quadruped ? 0.85f : Aspect(plan);

    /// <summary>The body seen from below, ready to be cut open (a quadruped on its back, legs spread; the others as drawn).</summary>
    public static List<Vector2[]> BellyUp(HarvestBodyPlan plan)
    {
        var l = new List<Vector2[]>();
        if (plan == HarvestBodyPlan.Quadruped)
        {
            l.Add(Limb(new Vector2(0.37f, 0.7f), new Vector2(0.24f, 0.78f), new Vector2(0.12f, 0.86f), 0.05f));
            l.Add(Mirror(Limb(new Vector2(0.37f, 0.7f), new Vector2(0.24f, 0.78f), new Vector2(0.12f, 0.86f), 0.05f)));
            l.Add(Limb(new Vector2(0.37f, 0.3f), new Vector2(0.24f, 0.2f), new Vector2(0.12f, 0.1f), 0.055f));
            l.Add(Mirror(Limb(new Vector2(0.37f, 0.3f), new Vector2(0.24f, 0.2f), new Vector2(0.12f, 0.1f), 0.055f)));
            l.Add(Limb(new Vector2(0.5f, 0.21f), new Vector2(0.5f, 0.12f), new Vector2(0.52f, 0.03f), 0.02f));
            l.Add(Ellipse(0.5f, 0.5f, 0.2f, 0.3f, 28));
            l.Add(P(0.44f, 0.76f, 0.56f, 0.76f, 0.58f, 0.81f, 0.42f, 0.81f));
            l.Add(Ellipse(0.5f, 0.86f, 0.1f, 0.09f, 18));
            l.Add(Ellipse(0.5f, 0.95f, 0.05f, 0.04f, 12));
            foreach (Vector2 paw in new[] { new Vector2(0.12f, 0.86f), new Vector2(0.88f, 0.86f), new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.1f) })
                l.Add(Ellipse(paw.x, paw.y, 0.045f, 0.035f, 10));
            return l;
        }
        foreach (string slot in Slots(plan))
            l.AddRange(Shape(plan, slot));
        return l;
    }

    /// <summary>One opening cut of a hide: its line on the belly-up drawing and the stretches that need care.</summary>
    public sealed class CutLine
    {
        public string name;
        public Vector2[] points;
        public List<HarvestZone> zones = new List<HarvestZone>();

        public CutLine(string name, Vector2[] points, params HarvestZone[] zones)
        {
            this.name = name;
            this.points = points;
            this.zones.AddRange(zones);
        }
    }

    /// <summary>
    /// Where a hide is cut open (on the <see cref="BellyUp"/> drawing): chin to tail along the belly - the gut lies right
    /// under the middle of it - then the inside of each limb from the paw, fiddly at the paw.
    /// </summary>
    public static List<CutLine> CutLines(HarvestBodyPlan plan)
    {
        var l = new List<CutLine>();
        switch (plan)
        {
            case HarvestBodyPlan.Quadruped:
                l.Add(new CutLine("the belly line", P(0.5f, 0.93f, 0.5f, 0.78f, 0.5f, 0.5f, 0.5f, 0.2f, 0.5f, 0.1f),
                    new HarvestZone(0f, 0.12f, HarvestZoneKind.Fiddly), new HarvestZone(0.42f, 0.78f, HarvestZoneKind.Gut)));
                Vector2[] fore = P(0.13f, 0.85f, 0.24f, 0.78f, 0.37f, 0.7f, 0.5f, 0.66f), hind = P(0.13f, 0.11f, 0.24f, 0.2f, 0.37f, 0.3f, 0.5f, 0.34f);
                l.Add(new CutLine("the left foreleg", fore, new HarvestZone(0f, 0.22f, HarvestZoneKind.Fiddly)));
                l.Add(new CutLine("the right foreleg", MirrorLine(fore), new HarvestZone(0f, 0.22f, HarvestZoneKind.Fiddly)));
                l.Add(new CutLine("the left hind leg", hind, new HarvestZone(0f, 0.22f, HarvestZoneKind.Fiddly)));
                l.Add(new CutLine("the right hind leg", MirrorLine(hind), new HarvestZone(0f, 0.22f, HarvestZoneKind.Fiddly)));
                break;
            case HarvestBodyPlan.Biped:
            {
                l.Add(new CutLine("the belly line", P(0.5f, 0.84f, 0.5f, 0.66f, 0.5f, 0.47f),
                    new HarvestZone(0f, 0.1f, HarvestZoneKind.Fiddly), new HarvestZone(0.4f, 0.88f, HarvestZoneKind.Gut)));
                Vector2[] arm = P(0.82f, 0.44f, 0.79f, 0.6f, 0.7f, 0.78f), leg = P(0.6f, 0.04f, 0.59f, 0.24f, 0.52f, 0.45f);
                l.Add(new CutLine("the left arm", arm, new HarvestZone(0f, 0.15f, HarvestZoneKind.Fiddly)));
                l.Add(new CutLine("the right arm", MirrorLine(arm), new HarvestZone(0f, 0.15f, HarvestZoneKind.Fiddly)));
                l.Add(new CutLine("the left leg", leg, new HarvestZone(0f, 0.15f, HarvestZoneKind.Fiddly)));
                l.Add(new CutLine("the right leg", MirrorLine(leg), new HarvestZone(0f, 0.15f, HarvestZoneKind.Fiddly)));
                break;
            }
            case HarvestBodyPlan.Avian:
            {
                l.Add(new CutLine("the breast line", P(0.5f, 0.78f, 0.5f, 0.6f, 0.5f, 0.42f), new HarvestZone(0.4f, 0.9f, HarvestZoneKind.Gut)));
                Vector2[] wing = P(0.96f, 0.74f, 0.75f, 0.7f, 0.6f, 0.66f);
                l.Add(new CutLine("the left wing", wing, new HarvestZone(0f, 0.2f, HarvestZoneKind.Fiddly)));
                l.Add(new CutLine("the right wing", MirrorLine(wing), new HarvestZone(0f, 0.2f, HarvestZoneKind.Fiddly)));
                break;
            }
            case HarvestBodyPlan.Serpent:
            {
                var spine = new List<Vector2>();
                for (int i = 0; i <= 24; i++) spine.Add(SerpentSpine(0.97f - 0.93f * i / 24f));
                l.Add(new CutLine("the belly scales", spine.ToArray(), new HarvestZone(0f, 0.08f, HarvestZoneKind.Fiddly), new HarvestZone(0.3f, 0.7f, HarvestZoneKind.Gut)));
                break;
            }
            case HarvestBodyPlan.Fish:
                l.Add(new CutLine("the belly", P(0.84f, 0.42f, 0.55f, 0.36f, 0.2f, 0.45f), new HarvestZone(0.2f, 0.8f, HarvestZoneKind.Gut)));
                break;
        }
        return l;
    }

    private static Vector2[] MirrorLine(Vector2[] s)
    {
        var m = new Vector2[s.Length];
        for (int i = 0; i < s.Length; i++) m[i] = new Vector2(1f - s[i].x, s[i].y);
        return m;
    }

    // ------------------------------------------------------------------ the body cavity
    /// <summary>The open body cavity of a plan (where the organs are drawn).</summary>
    public static Vector2[] Cavity(HarvestBodyPlan plan)
    {
        switch (plan)
        {
            case HarvestBodyPlan.Biped: return Ellipse(0.5f, 0.5f, 0.32f, 0.46f, 28);
            case HarvestBodyPlan.Avian: return Ellipse(0.5f, 0.52f, 0.36f, 0.44f, 28);
            case HarvestBodyPlan.Fish: return Ellipse(0.5f, 0.5f, 0.47f, 0.3f, 28);
            case HarvestBodyPlan.Serpent: return Ellipse(0.5f, 0.5f, 0.48f, 0.2f, 28);
            case HarvestBodyPlan.Arthropod: return Ellipse(0.5f, 0.48f, 0.3f, 0.45f, 28);
            default: return Ellipse(0.5f, 0.5f, 0.47f, 0.4f, 28);
        }
    }

    /// <summary>
    /// Where an organ of <paramref name="shape"/> lies in a plan's cavity (centre and half-size in the cavity's box); the
    /// <paramref name="index"/>th of a pair (kidneys) sits beside the first. False for shapes without a place (they are
    /// then placed in free space by the caller).
    /// </summary>
    public static bool OrganSpot(HarvestBodyPlan plan, string shape, int index, out Vector2 centre, out Vector2 half)
    {
        centre = new Vector2(0.5f, 0.5f);
        half = new Vector2(0.06f, 0.06f);
        bool vertical = plan == HarvestBodyPlan.Biped || plan == HarvestBodyPlan.Avian || plan == HarvestBodyPlan.Arthropod;
        // Positions along the body (front → back) and across, for horizontal bodies; turned for upright ones.
        float along, across, l, w;
        switch (shape)
        {
            case "heart": along = 0.74f; across = 0.42f; l = 0.08f; w = 0.09f; break;
            case "lungs": along = 0.78f; across = 0.62f; l = 0.12f; w = 0.1f; break;
            case "liver": along = 0.58f; across = 0.55f; l = 0.12f; w = 0.11f; break;
            case "gall": along = 0.55f; across = 0.44f; l = 0.03f; w = 0.03f; break;
            case "stomach": along = 0.44f; across = 0.62f; l = 0.09f; w = 0.09f; break;
            case "spleen": along = 0.44f; across = 0.79f; l = 0.06f; w = 0.03f; break;
            case "kidney": along = 0.32f + 0.08f * index; across = 0.78f; l = 0.04f; w = 0.05f; break;
            case "intestines": along = 0.32f; across = 0.4f; l = 0.13f; w = 0.13f; break;
            case "bladder": along = 0.13f; across = 0.42f; l = 0.04f; w = 0.045f; break;
            case "gland": along = 0.2f - 0.07f * index; across = 0.65f; l = 0.035f; w = 0.035f; break;
            case "venom": along = 0.93f; across = 0.5f - 0.1f * (index - 0.5f); l = 0.035f; w = 0.035f; break;
            default: return false;
        }
        if (plan == HarvestBodyPlan.Arthropod)
        {
            // A spider: venom glands in the head, a long heart along the abdomen, silk glands at its end.
            switch (shape)
            {
                case "heart": along = 0.45f; across = 0.5f; l = 0.15f; w = 0.035f; break;
                case "lungs": along = 0.6f; across = 0.5f; l = 0.05f; w = 0.12f; break;
                case "gland": along = 0.18f + 0.06f * index; across = 0.5f; l = 0.05f; w = 0.08f; break;
                case "intestines": along = 0.32f; across = 0.5f; l = 0.08f; w = 0.15f; break;
            }
        }
        if (vertical)
        {
            centre = new Vector2(across, along);
            half = new Vector2(w, l);
        }
        else
        {
            centre = new Vector2(along, across);
            half = new Vector2(l, w);
        }
        return true;
    }

    /// <summary>The outline of an organ (a soft blob, or two lobes for lungs, coils for intestines) in a box.</summary>
    public static List<Vector2[]> OrganOutline(string shape, Vector2 centre, Vector2 half, int seed = 0)
    {
        var list = new List<Vector2[]>();
        switch (shape)
        {
            case "lungs":
                list.Add(Blob(centre + new Vector2(-half.x * 0.48f, 0f), new Vector2(half.x * 0.5f, half.y), seed));
                list.Add(Blob(centre + new Vector2(half.x * 0.48f, 0f), new Vector2(half.x * 0.5f, half.y), seed + 1));
                break;
            case "intestines":
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.257f;
                    list.Add(Ellipse(centre.x + Mathf01(Math.Cos(a)) * half.x * 0.55f, centre.y + Mathf01(Math.Sin(a)) * half.y * 0.55f, half.x * 0.42f, half.y * 0.3f, 12));
                }
                break;
            case "heart":
                list.Add(P(centre.x, centre.y - half.y, centre.x + half.x, centre.y + half.y * 0.2f, centre.x + half.x * 0.6f, centre.y + half.y,
                    centre.x, centre.y + half.y * 0.6f, centre.x - half.x * 0.6f, centre.y + half.y, centre.x - half.x, centre.y + half.y * 0.2f));
                break;
            default:
                list.Add(Blob(centre, half, seed));
                break;
        }
        return list;
    }

    private static float Mathf01(double v) => (float)v;

    // ------------------------------------------------------------------ pieces
    /// <summary>
    /// The outline of a piece in a unit box (y up) for the trophy and bone work: claw, fang/tooth, horn, antler, tusk, beak,
    /// talon, scale, plate, longbone, skull, ribs, spine, pelvis, jaw. Unknown shapes are a rounded slab.
    /// </summary>
    public static List<Vector2[]> Piece(string shape)
    {
        var l = new List<Vector2[]>();
        switch (shape)
        {
            case "claw":
            case "talon":
                l.Add(P(0.35f, 0f, 0.65f, 0f, 0.7f, 0.35f, 0.62f, 0.7f, 0.42f, 0.98f, 0.5f, 0.6f, 0.4f, 0.3f)); break;
            case "fang":
            case "tooth":
                l.Add(P(0.32f, 1f, 0.68f, 1f, 0.66f, 0.62f, 0.56f, 0.2f, 0.5f, 0f, 0.44f, 0.2f, 0.34f, 0.62f)); break;
            case "horn":
            case "tusk":
                l.Add(P(0.3f, 0f, 0.7f, 0f, 0.74f, 0.3f, 0.66f, 0.62f, 0.5f, 0.88f, 0.3f, 1f, 0.42f, 0.72f, 0.5f, 0.4f, 0.36f, 0.2f)); break;
            case "antler":
                l.Add(P(0.44f, 0f, 0.56f, 0f, 0.56f, 0.4f, 0.8f, 0.62f, 0.86f, 0.94f, 0.8f, 0.96f, 0.74f, 0.68f, 0.56f, 0.55f, 0.56f, 0.8f,
                    0.48f, 1f, 0.44f, 0.8f, 0.44f, 0.55f, 0.24f, 0.72f, 0.18f, 0.96f, 0.12f, 0.94f, 0.18f, 0.62f, 0.44f, 0.4f)); break;
            case "beak":
                l.Add(P(0.2f, 0.2f, 0.2f, 0.8f, 0.7f, 0.62f, 1f, 0.38f, 0.8f, 0.3f)); break;
            case "scale":
            case "plate":
                l.Add(P(0.5f, 0f, 0.92f, 0.3f, 0.86f, 0.78f, 0.5f, 1f, 0.14f, 0.78f, 0.08f, 0.3f)); break;
            case "skull":
                l.Add(P(0.06f, 0.45f, 0.12f, 0.78f, 0.34f, 0.95f, 0.6f, 0.92f, 0.78f, 0.75f, 0.98f, 0.55f, 0.96f, 0.42f, 0.72f, 0.36f, 0.6f, 0.2f,
                    0.34f, 0.18f, 0.16f, 0.26f));
                l.Add(Ellipse(0.42f, 0.6f, 0.07f, 0.07f, 10));
                break;
            case "jaw":
                l.Add(P(0.05f, 0.6f, 0.15f, 0.75f, 0.9f, 0.5f, 0.98f, 0.3f, 0.85f, 0.25f, 0.2f, 0.42f)); break;
            case "ribs":
                for (int i = 0; i < 6; i++)
                {
                    float x = 0.12f + i * 0.15f;
                    l.Add(Limb(new Vector2(x, 0.95f), new Vector2(x + 0.06f, 0.55f), new Vector2(x - 0.01f, 0.08f), 0.025f));
                }
                break;
            case "spine":
                for (int i = 0; i < 7; i++)
                    l.Add(Ellipse(0.1f + i * 0.13f, 0.5f, 0.055f, 0.13f, 10));
                break;
            case "pelvis":
                l.Add(P(0.1f, 0.7f, 0.35f, 0.95f, 0.5f, 0.75f, 0.65f, 0.95f, 0.9f, 0.7f, 0.75f, 0.3f, 0.6f, 0.1f, 0.4f, 0.1f, 0.25f, 0.3f)); break;
            case "longbone":
                l.Add(P(0.12f, 0.3f, 0f, 0.42f, 0.02f, 0.62f, 0.14f, 0.7f, 0.24f, 0.58f, 0.76f, 0.58f, 0.86f, 0.7f, 0.98f, 0.62f, 1f, 0.42f, 0.88f, 0.3f,
                    0.76f, 0.42f, 0.24f, 0.42f)); break;
            default:
                l.Add(Ellipse(0.5f, 0.5f, 0.42f, 0.32f, 18)); break;
        }
        return l;
    }

    // ------------------------------------------------------------------ geometry helpers
    /// <summary>A polygon from x, y pairs.</summary>
    public static Vector2[] P(params float[] xy)
    {
        var v = new Vector2[xy.Length / 2];
        for (int i = 0; i < v.Length; i++)
            v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        return v;
    }

    public static Vector2[] Ellipse(float cx, float cy, float rx, float ry, int n)
    {
        var v = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            double a = i / (double)n * Math.PI * 2;
            v[i] = new Vector2(cx + (float)Math.Cos(a) * rx, cy + (float)Math.Sin(a) * ry);
        }
        return v;
    }

    /// <summary>A soft irregular blob (organs), repeatable by <paramref name="seed"/>.</summary>
    public static Vector2[] Blob(Vector2 centre, Vector2 half, int seed)
    {
        const int n = 16;
        var v = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            double a = i / (double)n * Math.PI * 2;
            float k = 0.86f + 0.22f * (float)HarvestMath.Noise(seed, i, 61);
            v[i] = new Vector2(centre.x + (float)Math.Cos(a) * half.x * k, centre.y + (float)Math.Sin(a) * half.y * k);
        }
        return v;
    }

    /// <summary>A limb from <paramref name="a"/> through the knee <paramref name="b"/> to <paramref name="c"/>, <paramref name="width"/> thick.</summary>
    public static Vector2[] Limb(Vector2 a, Vector2 b, Vector2 c, float width)
    {
        Vector2 n1 = Normal(b - a) * width, n2 = Normal(c - b) * (width * 0.7f), nk = Normal(c - a) * width;
        return new[] { a + n1, b + nk, c + n2, c - n2, b - nk, a - n1 };
    }

    private static Vector2 Normal(Vector2 d)
    {
        Vector2 n = new Vector2(-d.y, d.x);
        float m = n.magnitude;
        return m > 1e-5f ? n / m : new Vector2(0f, 1f);
    }

    public static Vector2[] Mirror(Vector2[] s)
    {
        var m = new Vector2[s.Length];
        for (int i = 0; i < s.Length; i++)
            m[s.Length - 1 - i] = new Vector2(1f - s[i].x, s[i].y);
        return m;
    }

    public static Vector2[] Shift(Vector2[] s, float dx, float dy)
    {
        var m = new Vector2[s.Length];
        for (int i = 0; i < s.Length; i++)
            m[i] = new Vector2(s[i].x + dx, s[i].y + dy);
        return m;
    }

    public static Vector2 Middle(Vector2[] s)
    {
        if (s == null || s.Length == 0)
            return new Vector2(0.5f, 0.5f);
        float x = 0f, y = 0f;
        foreach (Vector2 p in s) { x += p.x; y += p.y; }
        return new Vector2(x / s.Length, y / s.Length);
    }

    /// <summary>The bounds of polygons (min x, min y, max x, max y).</summary>
    public static Rect Bounds(IEnumerable<Vector2[]> shapes)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (Vector2[] s in shapes)
            if (s != null)
                foreach (Vector2 p in s)
                {
                    if (p.x < x0) x0 = p.x;
                    if (p.y < y0) y0 = p.y;
                    if (p.x > x1) x1 = p.x;
                    if (p.y > y1) y1 = p.y;
                }
        return x0 > x1 ? new Rect(0f, 0f, 0f, 0f) : new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>Is <paramref name="p"/> inside the polygon?</summary>
    public static bool Contains(Vector2[] s, Vector2 p)
    {
        if (s == null || s.Length < 3)
            return false;
        bool inside = false;
        for (int i = 0, j = s.Length - 1; i < s.Length; j = i++)
        {
            Vector2 a = s[i], b = s[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Distance from <paramref name="p"/> to segment <paramref name="a"/>-<paramref name="b"/>, and how far along it (0-1) the nearest point is.</summary>
    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out float t)
    {
        Vector2 ab = b - a;
        float len = ab.x * ab.x + ab.y * ab.y;
        t = len > 1e-9f ? Math.Max(0f, Math.Min(1f, ((p.x - a.x) * ab.x + (p.y - a.y) * ab.y) / len)) : 0f;
        Vector2 q = a + ab * t;
        return (p - q).magnitude;
    }
}
