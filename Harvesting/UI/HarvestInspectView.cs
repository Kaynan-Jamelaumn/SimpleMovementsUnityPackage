using System.Collections.Generic;
using UnityEngine;

/// <summary>What the inspection colours the body by.</summary>
public enum HarvestInspectLayer
{
    /// <summary>The worst of what each region holds.</summary>
    Overall,
    Hide,
    Meat,
    Bone,
    Tendons,
    Organs,
}

/// <summary>
/// The body of a creature on the work surface, for inspecting it before taking anything: each region coloured by the
/// condition of one tissue (green whole, yellow damaged, red ruined, black destroyed; grey where it has none), its weak
/// points marked, a cross for each wound (a plus past six; dimmed on the far side), soiled, poisoned or tainted flesh
/// tinted, and the regions a material comes from lit.
/// </summary>
public static class HarvestInspectView
{
    private static readonly Color None = new Color(0.32f, 0.33f, 0.36f), Soil = new Color(0.45f, 0.55f, 0.2f);

    /// <summary>The box the body is drawn in (the planner's, so the work pictures line up with it).</summary>
    public static Rect Box(HarvestAnatomyData a) => HarvestPlanner.Fit(HarvestDiagram.Aspect(a.plan), HarvestPlanner.WorkArea);

    /// <summary>The condition of a region's <paramref name="layer"/> (0-1), or -1 when it has none of it.</summary>
    public static float Value(HarvestAnatomyData a, HarvestCondition k, int r, HarvestInspectLayer layer)
    {
        if (k == null || r < 0 || r >= k.regions.Count || r >= a.regions.Count)
            return -1f;
        HarvestRegion reg = a.regions[r];
        HarvestRegionState st = k.regions[r];
        switch (layer)
        {
            case HarvestInspectLayer.Hide:
                // A part with no hide cells of its own that grows feathers or scales: its own small hide.
                if (!k.HasHideCells(r) && HarvestCondition.Covered(a, r)) return (float)st.CoverCondition();
                return reg.hide > 0f ? (float)k.HideIntegrity(r) : -1f;
            case HarvestInspectLayer.Meat: return reg.meat > 0f ? st.meat : -1f;
            case HarvestInspectLayer.Bone: return reg.bone > 0f ? st.bone : -1f;
            case HarvestInspectLayer.Tendons: return reg.tendon > 0f ? st.tendon : -1f;
            case HarvestInspectLayer.Organs: return reg.organs > 0f ? st.organs : -1f;
            default:
            {
                float v = 1f;
                bool any = false;
                foreach (HarvestInspectLayer l in new[] { HarvestInspectLayer.Hide, HarvestInspectLayer.Meat, HarvestInspectLayer.Bone, HarvestInspectLayer.Tendons, HarvestInspectLayer.Organs })
                {
                    float x = Value(a, k, r, l);
                    if (x < 0f) continue;
                    v = Mathf.Min(v, x);
                    any = true;
                }
                return any ? v : -1f;
            }
        }
    }

    /// <summary>The region under <paramref name="p"/> (surface pixels), or -1.</summary>
    public static int RegionAt(HarvestAnatomyData a, Vector2 p)
    {
        if (a == null || p.x < 0f)
            return -1;
        Rect box = Box(a);
        int best = -1;
        float bestArea = float.MaxValue;
        for (int i = 0; i < a.regions.Count; i++)
        {
            HarvestRegion r = a.regions[i];
            if (r == null) continue;
            foreach (Vector2[] poly in HarvestDiagram.Shape(a.plan, r.Slot))
            {
                Vector2[] mapped = HarvestPlanner.Map(poly, box).ToArray();
                if (!HarvestDiagram.Contains(mapped, p)) continue;
                Rect b = HarvestDiagram.Bounds(new[] { mapped });
                // Near-side parts over far-side ones; the smallest of overlapping shapes.
                float area = b.width * b.height * (HarvestDiagram.IsFarSide(a.plan, r.Slot) ? 4f : 1f);
                if (area < bestArea) { bestArea = area; best = i; }
            }
        }
        return best;
    }

    public static void Draw(CraftPixels c, HarvestAnatomyData a, HarvestCondition k, HarvestInspectLayer layer, ICollection<int> lit, int hover, float time)
    {
        if (c == null || a == null)
            return;
        Rect box = Box(a);
        var wounds = new int[a.regions.Count];
        if (k != null)
            foreach (HarvestWound w in k.wounds)
                if (w.region >= 0 && w.region < wounds.Length) wounds[w.region]++;
        foreach (string slot in HarvestDiagram.Slots(a.plan))
        {
            int ri = -1;
            for (int i = 0; i < a.regions.Count; i++)
                if (a.regions[i] != null && string.Equals(a.regions[i].Slot, slot, System.StringComparison.OrdinalIgnoreCase)) { ri = i; break; }
            if (ri < 0) continue;
            float v = Value(a, k, ri, layer);
            Color fill = v < 0f ? None : Color.Lerp(HarvestText.ConditionColor(v), a.hideColor, 0.2f);
            HarvestRegionState st = k != null && ri < k.regions.Count ? k.regions[ri] : null;
            if (st != null && (layer == HarvestInspectLayer.Meat || layer == HarvestInspectLayer.Organs || layer == HarvestInspectLayer.Overall) &&
                (st.contamination > 0.3f || st.poison > 0.12f || st.taint > 0.12f))
                fill = Color.Lerp(fill, Soil, 0.45f);
            bool far = HarvestDiagram.IsFarSide(a.plan, slot);
            if (far) fill = Color.Lerp(fill, Color.black, 0.35f);
            bool on = lit != null && lit.Contains(ri);
            if (lit != null && lit.Count > 0 && !on) fill = Color.Lerp(fill, new Color(0.08f, 0.08f, 0.1f), 0.35f);
            foreach (Vector2[] poly in HarvestDiagram.Shape(a.plan, slot))
            {
                List<Vector2> m = HarvestPlanner.Map(poly, box);
                c.Polygon(m, fill);
                Color line = ri == hover ? Color.white : on ? new Color(1f, 0.85f, 0.4f) : new Color(0f, 0f, 0f, 0.6f);
                c.Outline(m, line, ri == hover || on ? 2.5f : 1.2f);
            }
        }
        // Weak points (on the near side) and wounds on top: a cross for each wound, a plus past six; the far side's dimmed.
        for (int i = 0; i < a.regions.Count; i++)
        {
            HarvestRegion r = a.regions[i];
            if (r == null) continue;
            bool far = HarvestDiagram.IsFarSide(a.plan, r.Slot);
            Vector2 at = HarvestPlanner.Map(HarvestDiagram.Centre(a.plan, r.Slot), box);
            if (r.weakPoint && !far)
            {
                c.Ring(at.x, at.y, 9f, 2f, new Color(1f, 0.45f, 0.3f, 0.95f));
                c.Disc(at.x, at.y, 2.5f, new Color(1f, 0.45f, 0.3f, 0.95f));
            }
            var mark = new Color(0.55f, 0.05f, 0.05f, far ? 0.55f : 1f);
            int n = Mathf.Min(6, wounds[i]);
            for (int w = 0; w < n; w++)
            {
                Vector2 p = at + new Vector2(14f + w * 9f, far ? 8f : -10f);
                c.Line(p.x - 3.5f, p.y - 3.5f, p.x + 3.5f, p.y + 3.5f, mark, 2.5f);
                c.Line(p.x - 3.5f, p.y + 3.5f, p.x + 3.5f, p.y - 3.5f, mark, 2.5f);
            }
            if (wounds[i] > 6)
            {
                Vector2 p = at + new Vector2(14f + 6 * 9f, far ? 8f : -10f);
                c.Line(p.x - 4f, p.y, p.x + 4f, p.y, mark, 2.5f);
                c.Line(p.x, p.y - 4f, p.x, p.y + 4f, mark, 2.5f);
            }
        }
    }
}
