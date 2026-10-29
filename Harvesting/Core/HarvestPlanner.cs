using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plans a harvest: the stages a material is taken in (opening cuts then separating for a hide; following a seam for a
/// cut of meat; freeing then drawing out a tendon or an organ; sawing then scraping a bone; working claws loose or
/// sawing horns off), each with its geometry on the work surface, its picture, its tool (the best one the player has
/// for it) and its margins - from the material's difficulty and condition, the player's skill and techniques, and the
/// tool's fitness. The same carcass, player and tools always give the same plan; the server makes the plan too, to
/// know how fast real hands could have done it.
/// </summary>
public static class HarvestPlanner
{
    /// <summary>The work surface (pixels): the station window's surface size.</summary>
    public const int Width = 720, Height = 400;
    /// <summary>Where the work is drawn (the right of the surface is kept for the gauges).</summary>
    public static readonly Rect WorkArea = new Rect(20f, 18f, 560f, 364f);

    private static readonly Color Ivory = new Color(0.9f, 0.86f, 0.76f), Cavity = new Color(0.32f, 0.08f, 0.09f), Sinew = new Color(0.9f, 0.86f, 0.74f),
        Line = new Color(0f, 0f, 0f, 0.55f);

    /// <summary>What a plan needs to know about the work.</summary>
    private sealed class Ctx
    {
        public HarvestAnatomyData a;
        public HarvestCarcass c;
        public HarvestMaterial m;
        public int mi;
        public HarvestWorker w;
        public HarvestSettings s;
        public float skill, anatomy, difficulty;
        public int seed;

        public bool Knows(string technique) => HarvestSkills.Knows(s, w.skills, technique);
    }

    /// <summary>
    /// The plan for taking <paramref name="material"/> (its pieces <paramref name="units"/>, or all of them) from
    /// <paramref name="c"/>. A plan with a refusal when it cannot be done now (and why).
    /// </summary>
    public static HarvestPlan Plan(HarvestAnatomyData a, HarvestCarcass c, int material, List<int> units, HarvestWorker w)
    {
        var plan = new HarvestPlan();
        if (a == null || c == null || w == null || material < 0 || a.materials == null || material >= a.materials.Count)
        {
            plan.refusal = "Nothing to harvest.";
            return plan;
        }
        HarvestSettings s = w.settings ?? HarvestSettings.Defaults();
        HarvestMaterial m = a.materials[material];
        plan.material = m.id;
        HarvestAvailability av = c.Availability(a, s, material, w.skills, w.player, out string why);
        if (av != HarvestAvailability.Available)
        {
            plan.refusal = why.Length > 0 ? why : "It cannot be taken now.";
            return plan;
        }
        var x = new Ctx
        {
            a = a, c = c, m = m, mi = material, w = w, s = s,
            skill = (float)HarvestSkills.Factor(s, w.skills, m.Skill),
            anatomy = (float)HarvestSkills.Factor(s, w.skills, HarvestSkill.Anatomy),
            seed = HarvestMath.Hash(c.condition.seed, material, w.seed),
        };
        double integrity = c.CombatIntegrity(a, material);
        x.difficulty = HarvestMathF.Clamp01(m.difficulty + (m.fragile ? 0.1f : 0f) + (integrity < 0.6 ? 0.1f : 0f));

        List<HarvestUnit> work = Units(x, units);
        if (m.countable && work.Count == 0)
        {
            plan.refusal = "There is nothing of it left to take.";
            return plan;
        }
        foreach (HarvestUnit u in work) plan.units.Add(u.index);

        switch (m.method)
        {
            case HarvestMethod.Skinning:
                if (m.countable) foreach (HarvestUnit u in work) Membrane(x, plan, u);
                else if (m.kind == HarvestMaterialKind.Hide) Hide(x, plan);
                else Pluck(x, plan);
                break;
            case HarvestMethod.Butchering:
                if (m.kind == HarvestMaterialKind.Fat) Trim(x, plan);
                else Cut(x, plan);
                break;
            case HarvestMethod.Tendons:
                foreach (HarvestUnit u in work) Tendon(x, plan, u);
                break;
            case HarvestMethod.Bones:
                foreach (HarvestUnit u in work) Bone(x, plan, u);
                break;
            case HarvestMethod.Organs:
                Organs(x, plan, work);
                break;
            default:
                Trophies(x, plan, work);
                break;
        }
        foreach (HarvestStageSpec st in plan.stages)
        {
            plan.minSeconds += st.minSeconds;
            if (st.toolFitness <= 0.01f && NeedsTool(st.kind))
                plan.refusal = $"You need a tool for this: {Needed(st.kind, m)}.";
        }
        plan.summary = $"{m.Name}: {plan.stages.Count} stage{(plan.stages.Count == 1 ? "" : "s")}";
        return plan;
    }

    private static bool NeedsTool(HarvestStageKind k) => k == HarvestStageKind.Trace || k == HarvestStageKind.Peel || k == HarvestStageKind.Steady || k == HarvestStageKind.Saw;

    private static string Needed(HarvestStageKind k, HarvestMaterial m)
    {
        switch (k)
        {
            case HarvestStageKind.Saw: return "a bone saw, or a hatchet at a pinch";
            case HarvestStageKind.Steady: return "a fine blade or a knife";
            default: return m.method == HarvestMethod.Butchering ? "a butcher's knife or any blade" : "a skinning knife or any blade";
        }
    }

    /// <summary>The pieces to work: those asked for (or all) that are still there and not destroyed.</summary>
    private static List<HarvestUnit> Units(Ctx x, List<int> wanted)
    {
        var list = new List<HarvestUnit>();
        if (!x.m.countable)
            return list;
        foreach (HarvestUnit u in x.c.condition.units)
        {
            if (u.material != x.mi || u.taken || u.integrity < x.s.destroyedBelow) continue;
            if (wanted != null && wanted.Count > 0 && !wanted.Contains(u.index)) continue;
            list.Add(u);
        }
        return list;
    }

    // ================================================================== stages
    private static float Margin(Ctx x, float fit) =>
        x.s.forgiveness * (1f + x.s.skillMargin * x.skill) * (0.6f + 0.4f * fit) * (1.25f - 0.5f * x.difficulty);

    private static float Tremble(Ctx x) => 7f * x.s.tremble * (1f + 0.6f * x.difficulty);

    /// <summary>
    /// A stage with the best tool for <paramref name="kinds"/> chosen (only the material's own tools among them, when it
    /// names some of them).
    /// </summary>
    private static HarvestStageSpec Stage(Ctx x, HarvestStageKind kind, string title, string instruction, HarvestToolKind kinds, float handsFitness = 0f, int unit = -1)
    {
        if (x.m.tools != HarvestToolKind.None && (kinds & x.m.tools) != 0)
            kinds &= x.m.tools;
        HarvestToolProfile t = x.w.tools.Best(kinds, out HarvestToolKind usedAs, out float fit, handsFitness);
        return new HarvestStageSpec
        {
            kind = kind, title = title, instruction = instruction, unit = unit,
            tool = usedAs, toolId = t.id ?? "", toolName = t.displayName, toolFitness = fit,
            toolSpeed = Math.Max(0.15f, t.speed * (0.4f + 0.6f * fit)),
            tremble = Tremble(x), workColor = x.a.hideColor, underColor = x.a.meatColor, furColor = x.a.hairColor,
        };
    }

    private static void Trace(Ctx x, HarvestStageSpec st)
    {
        float margin = Margin(x, st.toolFitness);
        st.tolerance = 9f * margin;
        st.speedLimit = 170f * (1f + 0.5f * x.skill) * (0.7f + 0.3f * st.toolFitness) * x.s.workSpeed / (0.75f + 0.5f * x.difficulty);
        st.strain = 1.6f - 0.6f * st.toolFitness;
        // Careful hands: the safe speed on the plain stretches, the slower one in the zones (a quarter over it still counts).
        float seconds = 0f;
        foreach (HarvestPath p in st.paths)
        {
            float length = p.Length, zoned = 0f;
            foreach (HarvestZone z in p.zones)
            {
                float l = Math.Max(0f, Math.Min(1f, z.to) - Math.Max(0f, z.from)) * length;
                zoned += l;
                seconds += l / (st.speedLimit * (z.kind == HarvestZoneKind.Fiddly ? 0.55f : 0.5f));
            }
            seconds += Math.Max(0f, length - zoned) / st.speedLimit;
        }
        st.minSeconds = seconds / 1.25f;
    }

    // ------------------------------------------------------------------ skinning
    private static void Hide(Ctx x, HarvestPlan plan)
    {
        HarvestBodyPlan bp = x.a.plan;
        Rect r = Fit(HarvestDiagram.BellyUpAspect(bp), WorkArea);
        HarvestStageSpec open = Stage(x, HarvestStageKind.Trace, "Open the hide",
            "Cut along each dashed line, from its green dot to its flag, keeping the knife's speed in the gauge's band. Slow down where the " +
            "line is amber (paws, face) and where it is red - the gut lies right under the belly skin: there the band turns amber and drops.",
            HarvestToolKind.Skinning | HarvestToolKind.Fine | HarvestToolKind.Butchering);
        foreach (Vector2[] shape in HarvestDiagram.BellyUp(bp))
            open.scene.Add(new HarvestSceneShape(Map(shape, r), x.a.hairColor, Darker(x.a.hairColor), 1.5f));
        foreach (HarvestDiagram.CutLine cut in HarvestDiagram.CutLines(bp))
            open.paths.Add(new HarvestPath { name = cut.name, points = Map(cut.points, r), zones = new List<HarvestZone>(cut.zones) });
        if (open.paths.Count == 0)
            open.paths.Add(new HarvestPath { name = "the belly", points = new List<Vector2> { Map(new Vector2(0.5f, 0.9f), r), Map(new Vector2(0.5f, 0.1f), r) } });
        open.guides = true;
        Trace(x, open);
        plan.stages.Add(open);

        HarvestCondition k = x.c.condition;
        HarvestStageSpec peel = Stage(x, HarvestStageKind.Peel, "Separate the hide",
            "Press on the edge of the hide still on (the bright line) and drag outward: the further you pull, the harder. Keep the pull in the " +
            "green band - slack does nothing, too hard tears. Paws, face and tail (outlined) have a narrow band. Dark holes are from the fight.",
            HarvestToolKind.Skinning | HarvestToolKind.Fine | HarvestToolKind.Butchering);
        peel.gridW = k.peltWidth;
        peel.gridH = k.peltHeight;
        peel.area = Fit(k.peltWidth / (float)Math.Max(1, k.peltHeight), new Rect(WorkArea.x + 70f, WorkArea.y, WorkArea.width - 140f, WorkArea.height));
        int cells = 0;
        foreach (HarvestPeltCell cell in k.pelt)
        {
            byte kind = 0;
            if (cell.present)
            {
                kind = cell.damage >= 0.5f || cell.burn >= 1f ? (byte)3 : cell.difficult ? (byte)2 : (byte)1;
                cells++;
            }
            peel.cells.Add(kind);
        }
        float fit = peel.toolFitness;
        float margin = Margin(x, fit);
        float half = 0.17f * margin * (x.Knows(HarvestSkills.SteadyPull) ? 1.15f : 1f);
        float centre = 0.55f + (1f - fit) * 0.15f;
        peel.bandLow = centre - half;
        peel.bandHigh = centre + half;
        float fiddlyHalf = half * (x.Knows(HarvestSkills.FineWork) ? 1f : 0.55f);
        peel.fiddlyLow = centre - fiddlyHalf;
        peel.fiddlyHigh = centre + fiddlyHalf;
        peel.rate = 2.4f * x.s.workSpeed * (0.5f + 0.5f * fit) / (0.6f + x.difficulty);
        peel.strain = 1.2f * (1f + x.difficulty) * (1.4f - 0.4f * fit);
        peel.nick = 0.06f * (1f - 0.7f * x.skill) * (1.5f - fit) * (x.Knows(HarvestSkills.FlawlessPelts) ? 0.5f : 1f);
        peel.minSeconds = cells / (peel.rate * peel.toolSpeed * 2f) * 0.6f;
        plan.stages.Add(peel);
    }

    /// <summary>Feathers plucked or scales scraped off, over the regions they come from.</summary>
    private static void Pluck(Ctx x, HarvestPlan plan)
    {
        bool feathers = x.m.kind == HarvestMaterialKind.Feathers;
        HarvestStageSpec st = Stage(x, HarvestStageKind.Scrape, feathers ? "Pluck the feathers" : "Scrape the scales off",
            feathers
                ? "Hold and stroke over the feathered parts (lit): brisk, even strokes pull them clean. Frantic strokes break them."
                : "Hold and scrape over the scaled parts (lit), tail to head: brisk, even strokes. Accept when it is clean enough.",
            HarvestToolKind.Scraper | HarvestToolKind.Pliers | HarvestToolKind.Skinning, feathers ? 0.7f : 0.25f);
        Rect r = Fit(HarvestDiagram.Aspect(x.a.plan), WorkArea);
        // The parts they come from, lit, as bare skin: the feathers (or scales) still on them stand out, plucked ones show pale.
        var shapes = new List<(int region, Vector2[] poly)>();
        var lit = new List<int>();
        foreach (HarvestSource src in x.m.sources)
        {
            int ri = x.a.RegionIndex(src.region);
            if (ri < 0 || lit.Contains(ri)) continue;
            lit.Add(ri);
            foreach (Vector2[] poly in HarvestDiagram.Shape(x.a.plan, x.a.regions[ri].Slot)) shapes.Add((ri, Map(poly, r).ToArray()));
        }
        BodyScene(x, st, r, false, lit);
        foreach ((int ri, Vector2[] poly) in shapes)
        {
            bool far = HarvestDiagram.IsFarSide(x.a.plan, x.a.regions[ri].Slot);
            st.scene.Add(new HarvestSceneShape(poly, far ? Color.Lerp(x.a.hideColor, Color.black, 0.35f) : x.a.hideColor, new Color(1f, 0.92f, 0.6f), 2.5f) { faint = far });
        }
        Grid(st, r, 26, 14, p =>
        {
            foreach ((int ri, Vector2[] poly) in shapes)
                if (HarvestDiagram.Contains(poly, p))
                    return (byte)Math.Round(10 * Math.Max(0.2, x.c.RegionHideCondition(ri)));
            return 255;
        });
        st.rate = 1.8f * x.s.workSpeed * (0.5f + 0.5f * st.toolFitness);
        st.strain = 1f;
        st.tissueColor = x.m.color;
        st.minSeconds = TissueSeconds(st);
        plan.stages.Add(st);
    }

    /// <summary>A countable skin (a wing membrane): cut around it, then draw it off.</summary>
    private static void Membrane(Ctx x, HarvestPlan plan, HarvestUnit u)
    {
        HarvestRegion reg = x.a.regions[u.region];
        Rect r = Fit(HarvestDiagram.Aspect(x.a.plan), WorkArea);
        Vector2[] shape = Largest(HarvestDiagram.Shape(x.a.plan, reg.Slot));
        string name = HarvestCondition.UnitName(x.a, u);
        HarvestStageSpec cut = Stage(x, HarvestStageKind.Trace, $"Cut around the {name}", "Follow its edge from the green dot all the way round.",
            HarvestToolKind.Fine | HarvestToolKind.Skinning, 0f, u.index);
        BodyScene(x, cut, r, false, u.region);
        var path = new HarvestPath { name = name };
        if (shape != null)
        {
            path.points = Map(StartAtTop(shape), r);
            path.points.Add(path.points[0]);
        }
        else
        {
            path.points = new List<Vector2> { Map(new Vector2(0.4f, 0.9f), r), Map(new Vector2(0.6f, 0.9f), r) };
        }
        path.zones.Add(new HarvestZone(0f, 0.15f, HarvestZoneKind.Fiddly));
        cut.paths.Add(path);
        Trace(x, cut);
        plan.stages.Add(cut);
        Vector2 c = shape != null ? Map(HarvestDiagram.Middle(shape), r) : r.center;
        HarvestStageSpec pull = Stage(x, HarvestStageKind.Pull, $"Draw the {name} off", "Grip its edge (the ring) and pull along the arrow, keeping the pull in the band.",
            HarvestToolKind.Pliers, 0.6f, u.index);
        pull.scene.AddRange(cut.scene);
        Pull(x, pull, c, c + Away(c, 150f), 0.16f);
        plan.stages.Add(pull);
    }

    // ------------------------------------------------------------------ butchering
    /// <summary>A cut of meat: the seam around each region it comes from (all the big ones: a shank is four legs).</summary>
    private static void Cut(Ctx x, HarvestPlan plan)
    {
        List<int> regions = MainRegions(x);
        Rect r = Fit(HarvestDiagram.Aspect(x.a.plan), WorkArea);
        HarvestStageSpec st = Stage(x, HarvestStageKind.Trace, $"Cut out the {x.m.Name.ToLowerInvariant()}",
            "Follow the seam around the cut (dashed) from the green dot. On the seam nothing is wasted; off it, meat is left on the bone or " +
            "cut into. Slow down at the joints (orange) to find the gap - rushing them hacks bone.",
            HarvestToolKind.Butchering | HarvestToolKind.Skinning | HarvestToolKind.Fine | HarvestToolKind.Chopping);
        BodyScene(x, st, r, true, regions);
        foreach (int ri in regions)
        {
            HarvestRegion reg = x.a.regions[ri];
            Vector2[] shape = Largest(HarvestDiagram.Shape(x.a.plan, reg.Slot));
            if (shape == null) continue;
            var path = new HarvestPath { name = $"the seam around the {reg.Name.ToLowerInvariant()}" };
            path.points = Map(StartAtTop(shape), r);
            path.points.Add(path.points[0]);
            path.zones = JointZones(x, path.points, r, ri);
            st.paths.Add(path);
        }
        if (st.paths.Count == 0)
            st.paths.Add(new HarvestPath { name = "the seam", points = new List<Vector2> { Map(new Vector2(0.3f, 0.5f), r), Map(new Vector2(0.7f, 0.5f), r) } });
        st.guides = x.Knows(HarvestSkills.SeamSense) || x.anatomy * x.s.maxLevel >= 10f;
        Trace(x, st);
        plan.stages.Add(st);
    }

    /// <summary>Fat trimmed off: a scrape over the regions it comes from.</summary>
    private static void Trim(Ctx x, HarvestPlan plan)
    {
        Rect r = Fit(HarvestDiagram.Aspect(x.a.plan), WorkArea);
        HarvestStageSpec st = Stage(x, HarvestStageKind.Scrape, $"Trim the {x.m.Name.ToLowerInvariant()}",
            "Scrape the fat off the lit parts with brisk, even strokes. Accept when it is done.",
            HarvestToolKind.Scraper | HarvestToolKind.Butchering | HarvestToolKind.Skinning);
        var shapes = new List<Vector2[]>();
        var lit = new List<int>();
        foreach (HarvestSource src in x.m.sources)
        {
            int ri = x.a.RegionIndex(src.region);
            if (ri < 0) continue;
            lit.Add(ri);
            foreach (Vector2[] poly in HarvestDiagram.Shape(x.a.plan, x.a.regions[ri].Slot)) shapes.Add(Map(poly, r).ToArray());
        }
        BodyScene(x, st, r, true, lit);
        Grid(st, r, 26, 14, p =>
        {
            foreach (Vector2[] s in shapes)
                if (HarvestDiagram.Contains(s, p)) return (byte)8;
            return 255;
        });
        st.rate = 1.8f * x.s.workSpeed * (0.5f + 0.5f * st.toolFitness);
        st.strain = 0.8f;
        st.tissueColor = x.m.color;
        st.minSeconds = TissueSeconds(st);
        plan.stages.Add(st);
    }

    /// <summary>The regions a bulk material mostly comes from: its biggest share, and those at least half as big.</summary>
    private static List<int> MainRegions(Ctx x)
    {
        float most = 0f;
        foreach (HarvestSource s in x.m.sources)
            if (x.a.RegionIndex(s.region) >= 0) most = Math.Max(most, s.share);
        var list = new List<int>();
        foreach (HarvestSource s in x.m.sources)
        {
            int i = x.a.RegionIndex(s.region);
            if (i >= 0 && s.share >= most * 0.5f && !list.Contains(i)) list.Add(i);
        }
        if (list.Count == 0) list.Add(Math.Max(0, x.a.DefaultRegionIndex));
        return list;
    }

    /// <summary>
    /// The joints along a seam: where a cut of the body meets a limb or a wing (the hip under a haunch, the elbow under a
    /// shoulder), or where a limb meets the body. Far-side parts of the drawing do not count.
    /// </summary>
    private static List<HarvestZone> JointZones(Ctx x, List<Vector2> pts, Rect r, int own)
    {
        bool ownLimb = x.a.regions[own].tendon >= 0.6f;
        var limbs = new List<Vector2[]>();
        for (int i = 0; i < x.a.regions.Count; i++)
        {
            HarvestRegion reg = x.a.regions[i];
            if (i == own || reg == null || HarvestDiagram.IsFarSide(x.a.plan, reg.Slot)) continue;
            bool limb = reg.tendon >= 0.6f;
            if (ownLimb ? limb || reg.bone < 0.5f : !limb) continue;
            foreach (Vector2[] poly in HarvestDiagram.Shape(x.a.plan, reg.Slot)) limbs.Add(Map(poly, r).ToArray());
        }
        var zones = new List<HarvestZone>();
        if (limbs.Count == 0)
            return zones;
        float total = 0f;
        for (int i = 1; i < pts.Count; i++) total += (pts[i] - pts[i - 1]).magnitude;
        float walked = 0f, start = -1f;
        for (int i = 1; i < pts.Count; i++)
        {
            float seg = (pts[i] - pts[i - 1]).magnitude;
            Vector2 mid = (pts[i] + pts[i - 1]) * 0.5f;
            bool near = false;
            foreach (Vector2[] limb in limbs)
            {
                if (HarvestDiagram.Contains(limb, mid)) { near = true; break; }
                for (int k = 0; k < limb.Length && !near; k++)
                    if (HarvestDiagram.SegmentDistance(mid, limb[k], limb[(k + 1) % limb.Length], out _) < 12f) near = true;
            }
            if (near && start < 0f) start = walked;
            if (!near && start >= 0f)
            {
                zones.Add(new HarvestZone(start / total, walked / total, HarvestZoneKind.Joint));
                start = -1f;
            }
            walked += seg;
        }
        if (start >= 0f) zones.Add(new HarvestZone(start / total, 1f, HarvestZoneKind.Joint));
        return zones;
    }

    // ------------------------------------------------------------------ tendons
    private static void Tendon(Ctx x, HarvestPlan plan, HarvestUnit u)
    {
        string name = HarvestCondition.UnitName(x.a, u);
        bool back = x.m.Shape == "sinew";
        Rect r = Fit(back ? 2.2f : 0.7f, WorkArea);
        var scene = new List<HarvestSceneShape>();
        Vector2 a, b;
        if (back)
        {
            scene.Add(new HarvestSceneShape(Map(HarvestDiagram.P(0.04f, 0.32f, 0.96f, 0.32f, 0.98f, 0.66f, 0.02f, 0.66f), r), x.a.meatColor, Line, 2f));
            scene.Add(new HarvestSceneShape(Map(HarvestDiagram.P(0.04f, 0.58f, 0.96f, 0.58f, 0.96f, 0.62f, 0.04f, 0.62f), r), new Color(Ivory.r, Ivory.g, Ivory.b, 0.45f), Color.clear));
            a = Map(new Vector2(0.9f, 0.5f), r);
            b = Map(new Vector2(0.1f, 0.5f), r);
        }
        else
        {
            scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Limb(new Vector2(0.45f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.46f, 0.05f), 0.16f), r), x.a.meatColor, Line, 2f));
            scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Limb(new Vector2(0.44f, 0.93f), new Vector2(0.48f, 0.5f), new Vector2(0.45f, 0.07f), 0.035f), r), new Color(Ivory.r, Ivory.g, Ivory.b, 0.45f), Color.clear));
            a = Map(new Vector2(0.6f, 0.12f), r);
            b = Map(new Vector2(0.62f, 0.86f), r);
        }
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * 5f;
        Color sinew = Color.Lerp(x.a.meatColor, Sinew, 0.35f + 0.65f * Math.Max(0f, Math.Min(1f, u.integrity)));
        scene.Add(new HarvestSceneShape(new[] { a + n, b + n, b - n, a - n }, sinew, Line, 1f));

        HarvestStageSpec free = Stage(x, HarvestStageKind.Steady, $"Free the ends of the {name}",
            "Press on a yellow mark and hold the knife on it until it is cut through. Your hand wanders: keep correcting. Don't cut into the tendon itself.",
            HarvestToolKind.Fine | HarvestToolKind.Skinning | HarvestToolKind.Butchering, 0f, u.index);
        free.scene.AddRange(scene);
        float margin = Margin(x, free.toolFitness);
        float radius = 9f * margin;
        free.targets.Add(new HarvestTarget { at = a - d * 6f, radius = radius, need = Need(x, free) });
        free.targets.Add(new HarvestTarget { at = b + d * 6f, radius = radius, need = Need(x, free) });
        free.hazards.Add(new HarvestHazard
        {
            name = name, self = true, severity = x.m.fragile ? 2f : 1f,
            shape = new List<Vector2> { a + d * 22f + n * 1.6f, b - d * 22f + n * 1.6f, b - d * 22f - n * 1.6f, a + d * 22f - n * 1.6f },
        });
        free.tolerance = radius;
        free.strain = 1f;
        free.minSeconds = (free.targets[0].need + free.targets[1].need) / free.toolSpeed * 0.8f;
        plan.stages.Add(free);

        HarvestStageSpec pull = Stage(x, HarvestStageKind.Pull, $"Draw out the {name}",
            "Grip its end (the ring) and pull along the arrow, keeping the pull in the green band. When it catches the band jumps up - follow it, " +
            "then ease off as it gives, or it snaps.",
            HarvestToolKind.Pliers, 0.6f, u.index);
        pull.scene.AddRange(scene);
        Pull(x, pull, a, a + (b - a) * 1.15f, 0.16f * (x.Knows(HarvestSkills.WholeSinew) ? 1.1f : 1f));
        if (x.Knows(HarvestSkills.WholeSinew)) pull.strain *= 0.7f;
        pull.guides = x.Knows(HarvestSkills.GentleDraw) || x.anatomy * x.s.maxLevel >= 10f;
        plan.stages.Add(pull);
    }

    private static float Need(Ctx x, HarvestStageSpec st) => 0.9f * (0.6f + x.difficulty) / (0.4f + 0.6f * st.toolFitness);

    /// <summary>
    /// An arrow up to <paramref name="length"/> long from <paramref name="from"/> toward the side of the work area with the
    /// most room (up or down), so the hand has room to pull.
    /// </summary>
    private static Vector2 Away(Vector2 from, float length)
    {
        float up = WorkArea.yMax - from.y, down = from.y - WorkArea.y;
        float l = Math.Max(60f, Math.Min(length, Math.Max(up, down) - 6f));
        return new Vector2(0f, up >= down ? l : -l);
    }

    private static void Pull(Ctx x, HarvestStageSpec st, Vector2 from, Vector2 to, float half)
    {
        float margin = Margin(x, st.toolFitness);
        st.pullFrom = from;
        st.pullTo = to;
        st.bandLow = 0.5f - half * margin;
        st.bandHigh = 0.5f + half * margin;
        st.count = 2 + (int)Math.Round(3 * x.difficulty);
        st.rate = 0.32f * x.s.workSpeed * (0.5f + 0.5f * st.toolFitness) / (0.7f + 0.6f * x.difficulty);
        st.strain = 2f * (1f + x.difficulty) * (x.m.fragile ? 1.5f : 1f);
        st.minSeconds = 1f / (st.rate * st.toolSpeed) * 0.8f;
    }

    // ------------------------------------------------------------------ bones
    private static void Bone(Ctx x, HarvestPlan plan, HarvestUnit u)
    {
        string shape = x.m.Shape, name = HarvestCondition.UnitName(x.a, u);
        Rect r = Fit(PieceAspect(shape), new Rect(WorkArea.x + 30f, WorkArea.y + 30f, WorkArea.width - 60f, WorkArea.height - 60f));
        List<Vector2[]> piece = HarvestDiagram.Piece(shape);
        bool fractured = (u.marks & HarvestMarks.Fractured) != 0;
        var scene = new List<HarvestSceneShape>();
        foreach (Vector2[] poly in piece)
            scene.Add(new HarvestSceneShape(Map(poly, r), fractured ? new Color(0.8f, 0.74f, 0.62f) : Ivory, Line, 1.5f));

        HarvestStageSpec saw = Stage(x, HarvestStageKind.Saw, $"Saw the {name} free",
            "Put the saw on the line and stroke back and forth along it, in time with the beat (the swinging dot). Snatched strokes jump; crooked ones bind - both splinter it.",
            HarvestToolKind.Saw | HarvestToolKind.Chopping | HarvestToolKind.Butchering, 0f, u.index);
        saw.scene.AddRange(scene);
        GetCut(shape, out Vector2 c0, out Vector2 c1);
        saw.paths.Add(new HarvestPath { name = "the cut", points = new List<Vector2> { Map(c0, r), Map(c1, r) } });
        float margin = Margin(x, saw.toolFitness);
        saw.count = 6 + (int)Math.Round(10 * x.difficulty);
        saw.seconds = 0.55f;
        saw.window = 0.22f * margin * (x.Knows(HarvestSkills.EvenStrokes) ? 1.25f : 1f);
        saw.tolerance = 9f * margin;
        saw.strain = (1.4f - 0.4f * saw.toolFitness) * (saw.tool == HarvestToolKind.Chopping ? 1.6f : 1f) * (fractured ? 1.3f : 1f);
        saw.minSeconds = saw.count * saw.seconds * 0.45f / saw.toolSpeed;
        plan.stages.Add(saw);

        HarvestStageSpec scrape = Stage(x, HarvestStageKind.Scrape, $"Clean the {name}",
            "Scrape the meat and gristle off with brisk strokes. Don't dawdle on what is already bare: it gouges the bone. Accept when it is clean enough.",
            HarvestToolKind.Scraper | HarvestToolKind.Skinning | HarvestToolKind.Butchering, 0f, u.index);
        scrape.scene.AddRange(scene);
        var polys = new List<Vector2[]>();
        foreach (Vector2[] poly in piece) polys.Add(Map(poly, r).ToArray());
        double meatLeft = MeatLeft(x, u.region);
        int salt = u.index * 31 + x.mi;
        Grid(scrape, r, 30, 18, p =>
        {
            foreach (Vector2[] s in polys)
                if (HarvestDiagram.Contains(s, p))
                    return (byte)Math.Round(Math.Max(1, (5 + 5 * HarvestMath.Noise(x.seed, (int)(p.x * 7 + p.y * 131), salt)) * (0.5 + 0.5 * meatLeft)));
            return 255;
        });
        scrape.rate = 1.8f * x.s.workSpeed * (0.5f + 0.5f * scrape.toolFitness);
        scrape.strain = (shape == "skull" || shape == "jaw" || shape == "ribs" ? 1.8f : 1f) * (x.Knows(HarvestSkills.CleanBones) ? 0.4f : 1f);
        scrape.tissueColor = Color.Lerp(x.a.meatColor, new Color(0.85f, 0.75f, 0.68f), 0.3f);
        scrape.minSeconds = TissueSeconds(scrape);
        plan.stages.Add(scrape);
    }

    /// <summary>How much meat is still around a region's bones (0-1): what the cuts from it have not taken.</summary>
    private static double MeatLeft(Ctx x, int region)
    {
        if (region < 0 || region >= x.a.regions.Count) return 1;
        string rid = x.a.regions[region].id;
        double left = 1;
        for (int i = 0; i < x.a.materials.Count; i++)
        {
            HarvestMaterial m = x.a.materials[i];
            if (m == null || m.countable || m.method != HarvestMethod.Butchering) continue;
            foreach (HarvestSource s in m.sources)
                if (string.Equals(s.region, rid, StringComparison.OrdinalIgnoreCase))
                    left = Math.Min(left, 1 - x.c.TakenShare(i) * 0.8);
        }
        return left;
    }

    private static float PieceAspect(string shape)
    {
        switch (shape)
        {
            case "longbone": case "spine": return 2.4f;
            case "skull": return 1.4f;
            case "ribs": return 1.5f;
            case "jaw": return 1.9f;
            case "pelvis": return 1.2f;
            case "horn": case "tusk": case "fang": case "tooth": case "claw": case "talon": return 0.6f;
            case "antler": return 0.9f;
            default: return 1.4f;
        }
    }

    /// <summary>Where a bone is sawn (in its piece box): at the joint.</summary>
    private static void GetCut(string shape, out Vector2 a, out Vector2 b)
    {
        switch (shape)
        {
            case "ribs": a = new Vector2(0.03f, 0.88f); b = new Vector2(0.97f, 0.88f); break;
            case "pelvis": a = new Vector2(0.2f, 0.82f); b = new Vector2(0.8f, 0.82f); break;
            case "jaw": a = new Vector2(0.12f, 0.35f); b = new Vector2(0.12f, 0.95f); break;
            case "skull": a = new Vector2(0.86f, 0.25f); b = new Vector2(0.86f, 0.75f); break;
            case "spine": a = new Vector2(0.93f, 0.25f); b = new Vector2(0.93f, 0.75f); break;
            default: a = new Vector2(0.82f, 0.18f); b = new Vector2(0.82f, 0.82f); break;
        }
    }

    // ------------------------------------------------------------------ organs
    private static void Organs(Ctx x, HarvestPlan plan, List<HarvestUnit> work)
    {
        Rect r = Fit(HarvestDiagram.Aspect(x.a.plan) > 1f ? 1.7f : 0.85f, WorkArea);
        var layout = OrganLayout(x, r);
        var scene = new List<HarvestSceneShape> { new HarvestSceneShape(Map(HarvestDiagram.Cavity(x.a.plan), r), Cavity, Line, 2f) };
        foreach (KeyValuePair<HarvestUnit, List<Vector2[]>> o in layout)
        {
            HarvestMaterial om = x.a.materials[o.Key.material];
            Color c = Color.Lerp(new Color(0.25f, 0.2f, 0.18f), om.color, 0.35f + 0.65f * Math.Max(0f, Math.Min(1f, o.Key.integrity)));
            if ((o.Key.marks & (HarvestMarks.GutShot | HarvestMarks.Contaminated)) != 0) c = Color.Lerp(c, new Color(0.45f, 0.5f, 0.2f), 0.4f);
            foreach (Vector2[] poly in o.Value)
                scene.Add(new HarvestSceneShape(poly, c, Line, 1.5f) { unit = o.Key.material * 1000 + o.Key.index });
        }
        if (!x.c.cavityOpen)
        {
            bool upright = HarvestDiagram.Aspect(x.a.plan) <= 1f;
            HarvestStageSpec open = Stage(x, HarvestStageKind.Trace, "Open the body",
                "Cut through the belly wall along the dashed line, slowly: the gut lies right under it (red). Too fast and the knife opens it, soiling everything inside.",
                HarvestToolKind.Fine | HarvestToolKind.Skinning | HarvestToolKind.Butchering);
            open.scene.AddRange(scene);
            var path = new HarvestPath { name = "the belly wall" };
            if (upright) path.points = new List<Vector2> { Map(new Vector2(0.5f, 0.92f), r), Map(new Vector2(0.5f, 0.08f), r) };
            else path.points = new List<Vector2> { Map(new Vector2(0.92f, 0.3f), r), Map(new Vector2(0.08f, 0.3f), r) };
            path.zones.Add(new HarvestZone(0.25f, 0.85f, HarvestZoneKind.Gut));
            open.paths.Add(path);
            open.guides = true;
            Trace(x, open);
            open.strain *= 1.2f;
            plan.stages.Add(open);
        }
        foreach (HarvestUnit u in work)
        {
            if (!layout.TryGetValue(u, out List<Vector2[]> outline) || outline.Count == 0)
                continue;
            string name = HarvestCondition.UnitName(x.a, u);
            Vector2[] main = Largest(outline);
            Vector2 centre = HarvestDiagram.Middle(main);
            HarvestStageSpec free = Stage(x, HarvestStageKind.Steady, $"Cut the {name} free",
                $"Press on each yellow mark around the {name} and hold the knife on it until it is cut. Don't let it wander into the {name} itself, " +
                "or into the gut, the bladder or the gall (red when near): those spill.",
                HarvestToolKind.Fine | HarvestToolKind.Skinning | HarvestToolKind.Butchering, 0f, u.index);
            free.scene.AddRange(scene);
            free.scene.Add(new HarvestSceneShape(main, Color.clear, new Color(1f, 0.9f, 0.45f), 2.5f));
            float margin = Margin(x, free.toolFitness);
            float radius = 9f * margin;
            int marks = x.m.fragile ? 2 : 3;
            float turn = (float)(HarvestMath.Noise(x.seed, u.index, 5) * Math.PI);
            var guts = new List<Vector2[]>();
            foreach (KeyValuePair<HarvestUnit, List<Vector2[]>> o in layout)
            {
                HarvestMaterial om = x.a.materials[o.Key.material];
                if (o.Key == u || !om.hazard) continue;
                foreach (Vector2[] poly in o.Value)
                {
                    guts.Add(poly);
                    free.hazards.Add(new HarvestHazard { name = om.Name.ToLowerInvariant(), gut = true, severity = 1.2f, shape = new List<Vector2>(poly) });
                }
            }
            // The marks go where the knife can cut without wandering into a gut, a bladder or a gall.
            foreach (Vector2 at in MarkPlaces(main, centre, marks, turn, guts, radius + Math.Max(4f, free.tremble)))
                free.targets.Add(new HarvestTarget { at = at, radius = radius, need = Need(x, free) });
            free.hazards.Insert(0, new HarvestHazard { name = name, self = true, severity = x.m.fragile ? 2f : 1f, shape = Shrink(main, centre, 0.78f) });
            free.tolerance = radius;
            free.strain = x.m.fragile ? 1.6f : 1f;
            free.tremble *= x.Knows(HarvestSkills.SteadyHands) ? 0.8f : 1f;
            free.guides = x.Knows(HarvestSkills.ReadTheBody) || x.anatomy * x.s.maxLevel >= 5f;
            float need = 0f;
            foreach (HarvestTarget t in free.targets) need += t.need;
            free.minSeconds = need / free.toolSpeed * 0.8f;
            plan.stages.Add(free);

            HarvestStageSpec lift = Stage(x, HarvestStageKind.Pull, $"Lift the {name} out",
                "Grip it (the ring) and lift along the arrow, keeping the pull in the green band; ease off as it gives or it tears.",
                HarvestToolKind.Pliers, 0.6f, u.index);
            lift.scene.AddRange(scene);
            Pull(x, lift, centre, centre + Away(centre, 130f), 0.17f);
            lift.count = x.m.fragile ? 3 : 1 + (int)Math.Round(2 * x.difficulty);
            plan.stages.Add(lift);
        }
    }

    /// <summary>
    /// Where to put <paramref name="count"/> cutting marks round an organ (<paramref name="main"/>): evenly spaced from
    /// <paramref name="turn"/>, turned until every mark is <paramref name="want"/> clear of the <paramref name="guts"/>; when
    /// no turn clears them (an organ under another), the places round it, kept apart, whose closest one to a gut is the
    /// clearest, if they are clearer.
    /// </summary>
    private static List<Vector2> MarkPlaces(Vector2[] main, Vector2 centre, int count, float turn, List<Vector2[]> guts, float want)
    {
        const int Places = 36;
        var at = new Vector2[Places];
        var clear = new float[Places];
        for (int k = 0; k < Places; k++)
        {
            double ang = turn + k * Math.PI * 2 / Places;
            var dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
            at[k] = EdgePoint(main, centre, dir) + dir * 4f;
            clear[k] = Clearance(at[k], guts);
        }
        count = Math.Max(1, Math.Min(count, Places));
        int step = Places / count, bestTurn = 0;
        float bestWorst = float.MinValue;
        for (int j = 0; j < step; j++)
        {
            float worst = float.MaxValue;
            for (int k = 0; k < count; k++) worst = Math.Min(worst, clear[(j + k * step) % Places]);
            if (worst > bestWorst) { bestWorst = worst; bestTurn = j; }
            if (worst >= want) break; // the first turn that clears (no turn at all when nothing is near)
        }
        var list = new List<Vector2>();
        if (bestWorst < want)
        {
            // From each place in turn as the first mark, the clearest places kept apart; the set whose worst is best.
            int apart = Math.Max(1, Places / (count * 2));
            List<int> chosen = null;
            float chosenWorst = bestWorst;
            for (int first = 0; first < Places; first++)
            {
                var picked = new List<int> { first };
                while (picked.Count < count)
                {
                    int best = -1;
                    for (int k = 0; k < Places; k++)
                    {
                        bool near = false;
                        foreach (int p in picked)
                        {
                            int d = Math.Abs(p - k);
                            if (Math.Min(d, Places - d) < apart) { near = true; break; }
                        }
                        if (!near && (best < 0 || clear[k] > clear[best])) best = k;
                    }
                    if (best < 0) break;
                    picked.Add(best);
                }
                if (picked.Count < count) continue;
                float worst = float.MaxValue;
                foreach (int p in picked) worst = Math.Min(worst, clear[p]);
                if (worst > chosenWorst)
                {
                    chosenWorst = worst;
                    chosen = picked;
                }
            }
            if (chosen != null)
            {
                chosen.Sort();
                foreach (int p in chosen) list.Add(at[p]);
                return list;
            }
        }
        for (int k = 0; k < count; k++) list.Add(at[(bestTurn + k * step) % Places]);
        return list;
    }

    /// <summary>How far <paramref name="p"/> is from the nearest of <paramref name="shapes"/> (negative inside one; very far when there are none).</summary>
    private static float Clearance(Vector2 p, List<Vector2[]> shapes)
    {
        float best = float.MaxValue;
        foreach (Vector2[] s in shapes)
        {
            if (s == null || s.Length < 2) continue;
            float d = float.MaxValue;
            for (int i = 0; i < s.Length; i++)
                d = Math.Min(d, HarvestDiagram.SegmentDistance(p, s[i], s[(i + 1) % s.Length], out _));
            if (s.Length >= 3 && HarvestDiagram.Contains(s, p)) d = -d;
            best = Math.Min(best, d);
        }
        return best;
    }

    /// <summary>Where every organ still in the body that the player can tell is there lies (its outline on the surface).</summary>
    private static Dictionary<HarvestUnit, List<Vector2[]>> OrganLayout(Ctx x, Rect r)
    {
        var layout = new Dictionary<HarvestUnit, List<Vector2[]>>();
        var seen = new Dictionary<string, int>();
        int free = 0;
        foreach (HarvestUnit u in x.c.condition.units)
        {
            HarvestMaterial m = x.a.materials[u.material];
            if (u.taken || m.method != HarvestMethod.Organs || !HarvestSkills.Notices(x.s, x.w.skills, x.a.id, m)) continue;
            string shape = m.Shape == "organ" ? "liver" : m.Shape;
            seen.TryGetValue(shape, out int n);
            seen[shape] = n + 1;
            Vector2 c, half;
            if (!HarvestDiagram.OrganSpot(x.a.plan, shape, n, out c, out half))
            {
                // A shape without its own place: around the middle.
                double ang = free++ * 2.4;
                c = new Vector2(0.5f + 0.22f * (float)Math.Cos(ang), 0.5f + 0.18f * (float)Math.Sin(ang));
                half = new Vector2(0.04f, 0.04f);
            }
            var polys = new List<Vector2[]>();
            foreach (Vector2[] poly in HarvestDiagram.OrganOutline(shape == "venom" ? "gland" : shape, c, half, x.c.condition.seed + u.material * 13 + u.index))
                polys.Add(Map(poly, r).ToArray());
            layout[u] = polys;
        }
        return layout;
    }

    // ------------------------------------------------------------------ trophies
    private static void Trophies(Ctx x, HarvestPlan plan, List<HarvestUnit> work)
    {
        string shape = x.m.Shape;
        if (HarvestMethods.IsSawn(shape))
        {
            foreach (HarvestUnit u in work)
            {
                string name = HarvestCondition.UnitName(x.a, u);
                Rect r = Fit(0.9f, WorkArea);
                HarvestStageSpec saw = Stage(x, HarvestStageKind.Saw, $"Saw the {name} off",
                    "Put the saw on the line at its base and stroke back and forth along it, in time with the beat. Snatched or crooked strokes splinter it.",
                    HarvestToolKind.Saw | HarvestToolKind.Chopping | HarvestToolKind.Butchering, 0f, u.index);
                if (shape == "paw")
                {
                    saw.scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Limb(new Vector2(0.5f, 0.98f), new Vector2(0.52f, 0.5f), new Vector2(0.5f, 0.3f), 0.09f), r), x.a.hairColor, Line, 2f));
                    saw.scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Ellipse(0.5f, 0.2f, 0.14f, 0.12f, 16), r), x.a.hairColor, Line, 2f));
                    saw.paths.Add(new HarvestPath { name = "the joint", points = new List<Vector2> { Map(new Vector2(0.3f, 0.36f), r), Map(new Vector2(0.7f, 0.36f), r) } });
                }
                else
                {
                    saw.scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Ellipse(0.5f, 0.18f, 0.42f, 0.2f, 24), r), x.a.hairColor, Line, 2f));
                    Rect pr = new Rect(r.x + r.width * 0.3f, r.y + r.height * 0.3f, r.width * 0.4f, r.height * 0.66f);
                    foreach (Vector2[] poly in HarvestDiagram.Piece(shape))
                    {
                        // A beak is drawn sideways (its base on the left): turned base-down like a horn, so the line crosses its base.
                        Vector2[] piece = shape == "beak" ? Array.ConvertAll(poly, q => new Vector2(q.y, (q.x - 0.2f) / 0.8f)) : poly;
                        saw.scene.Add(new HarvestSceneShape(Map(piece, pr), x.m.color, Line, 1.5f) { unit = u.index });
                    }
                    saw.paths.Add(new HarvestPath { name = "the base", points = new List<Vector2> { Map(new Vector2(0.22f, 0.36f), r), Map(new Vector2(0.78f, 0.36f), r) } });
                }
                float margin = Margin(x, saw.toolFitness);
                saw.count = 5 + (int)Math.Round(8 * x.difficulty);
                saw.seconds = 0.55f;
                saw.window = 0.22f * margin;
                saw.tolerance = 9f * margin;
                saw.strain = (1.4f - 0.4f * saw.toolFitness) * (2f - Math.Max(0.2f, u.integrity));
                saw.minSeconds = saw.count * saw.seconds * 0.45f / saw.toolSpeed;
                plan.stages.Add(saw);
            }
            return;
        }

        HarvestStageSpec pry = Stage(x, HarvestStageKind.Pry, $"Work the {x.m.Name.ToLowerInvariant()}s loose",
            "Grip the lit piece and rock it side to side: each swing past the green marks loosens it. Don't swing past the red marks or snatch - " +
            "strain cracks it (cracked ones from the fight give way sooner).",
            HarvestToolKind.Pliers | HarvestToolKind.Fine, 0.2f);
        Rect area = new Rect(WorkArea.x + 30f, WorkArea.y + 40f, WorkArea.width - 60f, WorkArea.height - 60f);
        bool teeth = shape == "fang" || shape == "tooth";
        bool plates = shape == "scale" || shape == "plate";
        int count = work.Count;
        var spots = new List<(HarvestUnit unit, Vector2 at, Rect box)>();
        if (teeth)
        {
            // A jaw, the teeth along its gum line (its top edge, from (0.15, 0.75) to (0.9, 0.5) of its box), tips up.
            var jaw = new Rect(area.x, area.y, area.width, area.height * 0.5f);
            pry.scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Piece("jaw")[0], jaw), x.a.hideColor, Line, 2f));
            float w = Math.Min(30f, jaw.width * 0.6f / Math.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                float jx = 0.2f + 0.62f * (i + 0.5f) / count, jy = 0.75f - (jx - 0.15f) / 0.75f * 0.25f;
                Vector2 at = Map(new Vector2(jx, jy), jaw);
                spots.Add((work[i], at, new Rect(at.x - w * 0.5f, at.y + w * 1.6f, w, -w * 1.7f)));
            }
        }
        else if (plates)
        {
            pry.scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Ellipse(0.5f, 0.5f, 0.48f, 0.46f, 28), area), x.a.hideColor, Line, 2f));
            int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * 1.6))), rows = (count + cols - 1) / cols;
            float w = Math.Min(40f, area.width * 0.7f / cols);
            for (int i = 0; i < count; i++)
            {
                int row = i / cols, col = i % cols;
                float fy = rows > 1 ? 0.78f - 0.62f * row / (rows - 1) : 0.45f;
                Vector2 at = new Vector2(area.x + area.width * (0.12f + 0.76f * (col + 0.5f) / cols), area.y + area.height * fy);
                spots.Add((work[i], at, new Rect(at.x - w * 0.5f, at.y, w, w * 1.3f)));
            }
        }
        else
        {
            // A paw (hand, foot) for each region the pieces come from, the pieces in an arc over it.
            var groups = new List<List<HarvestUnit>>();
            foreach (HarvestUnit u in work)
            {
                List<HarvestUnit> g = groups.Find(l => l[0].region == u.region);
                if (g == null) groups.Add(g = new List<HarvestUnit>());
                g.Add(u);
            }
            float colW = area.width / Math.Max(1, groups.Count);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                List<HarvestUnit> g = groups[gi];
                var cell = new Rect(area.x + gi * colW, area.y, colW, area.height);
                pry.scene.Add(new HarvestSceneShape(Map(HarvestDiagram.Ellipse(0.5f, 0.2f, 0.34f, Math.Min(0.16f, 0.16f * colW / 160f + 0.06f), 24), cell), x.a.hairColor, Line, 2f));
                float span = 0.68f * colW;
                float w = Math.Max(14f, Math.Min(40f, g.Count > 1 ? span / (g.Count - 1) * 1.1f : 40f));
                for (int i = 0; i < g.Count; i++)
                {
                    float t = g.Count > 1 ? i / (float)(g.Count - 1) : 0.5f;
                    double ang = Math.PI * (0.85 - 0.7 * t);
                    Vector2 at = new Vector2(cell.x + cell.width * (0.5f + 0.34f * (float)Math.Cos(ang)), cell.y + cell.height * (0.22f + 0.3f * (float)Math.Sin(ang)));
                    spots.Add((g[i], at, new Rect(at.x - w * 0.5f, at.y, w, w * 1.4f)));
                }
            }
        }
        foreach ((HarvestUnit u, Vector2 at, Rect box) in spots)
        {
            pry.pieces.Add(new HarvestPieceSpot { unit = u.index, at = at, integrity = u.integrity, name = HarvestCondition.UnitName(x.a, u) });
            Color c = Color.Lerp(new Color(0.3f, 0.25f, 0.2f), x.m.color, 0.4f + 0.6f * Math.Max(0f, Math.Min(1f, u.integrity)));
            foreach (Vector2[] poly in HarvestDiagram.Piece(shape))
                pry.scene.Add(new HarvestSceneShape(Map(poly, box), c, Line, 1.5f) { unit = u.index });
        }
        float margin2 = Margin(x, pry.toolFitness);
        pry.count = Math.Max(2, (int)Math.Round((4 + 6 * x.difficulty) * (x.Knows(HarvestSkills.FeelTheRoot) ? 0.8f : 1f)));
        pry.tolerance = 16f;
        pry.speedLimit = 46f * margin2;
        pry.strain = (1f + x.difficulty) * (x.Knows(HarvestSkills.LightTouch) ? 0.7f : 1f) * (1.4f - 0.4f * pry.toolFitness);
        pry.minSeconds = count * pry.count * 0.2f / pry.toolSpeed;
        plan.stages.Add(pry);
    }

    // ================================================================== pictures
    /// <summary>
    /// The whole body in the side (or front) view: hide colour, or meat with the hide off; the <paramref name="lit"/>
    /// regions lit (none: all alike).
    /// </summary>
    private static void BodyScene(Ctx x, HarvestStageSpec st, Rect r, bool skinned, List<int> lit)
    {
        foreach (string slot in HarvestDiagram.Slots(x.a.plan))
        {
            int ri = -1;
            for (int i = 0; i < x.a.regions.Count; i++)
                if (x.a.regions[i] != null && string.Equals(x.a.regions[i].Slot, slot, StringComparison.OrdinalIgnoreCase)) { ri = i; break; }
            if (ri < 0) continue;
            Color c = skinned ? x.a.meatColor : x.a.hairColor;
            if (skinned) c = Color.Lerp(new Color(0.3f, 0.12f, 0.1f), c, 0.4f + 0.6f * x.c.condition.regions[ri].meat);
            bool far = HarvestDiagram.IsFarSide(x.a.plan, slot);
            if (far) c = Color.Lerp(c, Color.black, 0.35f);
            bool on = lit != null && lit.Contains(ri);
            if (lit != null && lit.Count > 0 && !on) c = Color.Lerp(c, new Color(0.1f, 0.1f, 0.12f), 0.45f);
            foreach (Vector2[] poly in HarvestDiagram.Shape(x.a.plan, slot))
                st.scene.Add(new HarvestSceneShape(Map(poly, r), c, on ? new Color(1f, 0.92f, 0.6f) : Line, on ? 2.5f : 1.2f) { faint = far });
        }
    }

    private static void BodyScene(Ctx x, HarvestStageSpec st, Rect r, bool skinned, int lit) =>
        BodyScene(x, st, r, skinned, lit >= 0 ? new List<int> { lit } : null);

    /// <summary>Fills a Scrape stage's grid over <paramref name="r"/> from <paramref name="tissue"/> (tenths; 255 = not part of it).</summary>
    private static void Grid(HarvestStageSpec st, Rect r, int w, int h, Func<Vector2, int> tissue)
    {
        st.area = r;
        st.gridW = w;
        st.gridH = h;
        st.cells.Clear();
        float cw = r.width / w, ch = r.height / h;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(r.x + (x + 0.5f) * cw, r.yMax - (y + 0.5f) * ch);
                int t = tissue(p);
                st.cells.Add((byte)Math.Max(0, Math.Min(255, t)));
            }
    }

    private static float TissueSeconds(HarvestStageSpec st)
    {
        float total = 0f;
        foreach (byte b in st.cells) if (b != 255) total += b / 10f;
        float cellArea = st.gridW > 0 && st.gridH > 0 ? st.area.width / st.gridW * (st.area.height / st.gridH) : 100f;
        float perSecond = st.rate * st.toolSpeed * (float)(Math.PI * 16 * 16) / Math.Max(1f, cellArea) * 0.5f;
        return total / Math.Max(0.1f, perSecond) * 0.5f;
    }

    // ================================================================== geometry
    /// <summary>The largest box of <paramref name="aspect"/> (width / height) inside <paramref name="into"/>, centred.</summary>
    public static Rect Fit(float aspect, Rect into)
    {
        aspect = Math.Max(0.05f, aspect);
        float w = into.width, h = w / aspect;
        if (h > into.height)
        {
            h = into.height;
            w = h * aspect;
        }
        return new Rect(into.x + (into.width - w) * 0.5f, into.y + (into.height - h) * 0.5f, w, h);
    }

    public static Vector2 Map(Vector2 unit, Rect r) => new Vector2(r.x + unit.x * r.width, r.y + unit.y * r.height);

    public static List<Vector2> Map(IEnumerable<Vector2> unit, Rect r)
    {
        var l = new List<Vector2>();
        foreach (Vector2 p in unit) l.Add(Map(p, r));
        return l;
    }

    private static Vector2[] Largest(List<Vector2[]> shapes)
    {
        Vector2[] best = null;
        float area = -1f;
        foreach (Vector2[] s in shapes)
        {
            Rect b = HarvestDiagram.Bounds(new[] { s });
            float a = b.width * b.height;
            if (a > area) { area = a; best = s; }
        }
        return best;
    }

    /// <summary>The polygon starting at its top-most point (seams start at the top).</summary>
    private static Vector2[] StartAtTop(Vector2[] s)
    {
        int top = 0;
        for (int i = 1; i < s.Length; i++)
            if (s[i].y > s[top].y) top = i;
        var r = new Vector2[s.Length];
        for (int i = 0; i < s.Length; i++) r[i] = s[(top + i) % s.Length];
        return r;
    }

    /// <summary>Where a ray from <paramref name="centre"/> along <paramref name="dir"/> leaves a polygon.</summary>
    private static Vector2 EdgePoint(Vector2[] poly, Vector2 centre, Vector2 dir)
    {
        float best = 0f;
        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
            Vector2 e = b - a;
            float den = dir.x * e.y - dir.y * e.x;
            if (Math.Abs(den) < 1e-6f) continue;
            Vector2 ca = a - centre;
            float t = (ca.x * e.y - ca.y * e.x) / den;
            float u = (ca.x * dir.y - ca.y * dir.x) / den;
            if (t > 0f && u >= 0f && u <= 1f) best = Math.Max(best, t);
        }
        return centre + dir * best;
    }

    private static List<Vector2> Shrink(Vector2[] poly, Vector2 centre, float k)
    {
        var l = new List<Vector2>(poly.Length);
        foreach (Vector2 p in poly) l.Add(centre + (p - centre) * k);
        return l;
    }

    private static Color Darker(Color c) => new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f, 1f);
}
