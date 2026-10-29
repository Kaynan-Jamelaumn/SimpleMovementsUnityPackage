using System;
using System.Collections.Generic;

/// <summary>
/// Works out what a harvest gives: the pieces, how many and how good, from everything that matters - the condition the
/// fight left the material in, how the hands did at each stage, how fresh it still is, the tools, the player's skill and
/// the creature's size, age and rank - and applies the harvest to the carcass (pieces taken, the hide off, the body
/// opened, gutted, guts spilled, meat ruined by cutting a bone out first). The fight's damage and the harvest's are kept
/// apart in every piece's marks. Deterministic: the same carcass and results always give the same pieces.
/// </summary>
public static class HarvestResolver
{
    /// <summary>
    /// Resolves <paramref name="result"/> on <paramref name="c"/> for a player (<paramref name="player"/>, with
    /// <paramref name="skills"/>). A refused outcome changes nothing.
    /// </summary>
    public static HarvestOutcome Resolve(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, HarvestTaskResult result, HarvestSkillData skills, int player = 0)
    {
        if (a == null || s == null || c == null || result == null)
            return HarvestOutcome.Refused("Nothing to harvest.");
        int mi = a.MaterialIndex(result.material);
        if (mi < 0)
            return HarvestOutcome.Refused("This creature has no such material.");
        HarvestMaterial m = a.materials[mi];
        HarvestAvailability av = c.Availability(a, s, mi, skills, player, out string reason);
        if (av != HarvestAvailability.Available && !(result.rough && av == HarvestAvailability.NeedsHide))
            return HarvestOutcome.Refused(reason.Length > 0 ? reason : "It cannot be taken now.");

        var o = new HarvestOutcome { skill = m.Skill, performance = result.Performance };
        if (result.rough)
            return StripRoughly(a, s, c, o, player);

        var f = new Factors(a, s, c, m, result, skills);
        if (m.countable)
            Countable(a, s, c, mi, m, result, f, o);
        else if (HarvestCarcass.IsHide(m))
            Hide(a, s, c, mi, m, result, f, o);
        else
            Bulk(a, s, c, mi, m, result, f, o);

        Spills(a, s, c, mi, m, result, o);
        c.tasks++;
        c.lastWork = c.age;
        c.Unlock(mi, player);

        // Experience: the method's skill, and a share for Anatomy (more for a species new to the player).
        int pieces = o.Count;
        o.xp = HarvestSkills.XpFor(s, m, pieces + o.discarded, o.performance);
        bool newSpecies = skills != null && skills.Species(a.id) == null;
        o.anatomyXp = o.xp * s.anatomyShare + (newSpecies ? 10f : 0f);
        return o;
    }

    // ------------------------------------------------------------------ what every piece shares
    /// <summary>The factors that do not depend on the piece: freshness, tools, skill, the creature's traits.</summary>
    private readonly struct Factors
    {
        public readonly double fresh, freshFactor, toolFitness, toolFactor, skill, bonus, precision;
        public readonly HarvestMarks harvestMarks, timeMarks;
        /// <summary>A tool far from right for some stage: the work is rough.</summary>
        public readonly bool wrongTool;
        public readonly bool flawlessPelts, primeCuts, noWaste, glandCare;

        public Factors(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, HarvestMaterial m, HarvestTaskResult r, HarvestSkillData skills)
        {
            fresh = c.Freshness(m);
            freshFactor = 1 - s.freshnessWeight * (1 - fresh);
            timeMarks = c.TimeMarks(s, m);
            double fit = 0, prec = 0;
            int n = 0;
            HarvestMarks marks = HarvestMarks.None;
            bool wrong = false;
            if (r.stages != null)
                foreach (HarvestStageResult st in r.stages)
                {
                    if (st == null) continue;
                    fit += HarvestMath.Clamp01(st.toolFitness);
                    prec += HarvestMath.Clamp01(st.precision);
                    marks |= st.marks;
                    if (st.toolFitness < s.wrongToolBelow) wrong = true;
                    n++;
                }
            toolFitness = n > 0 ? fit / n : 0;
            precision = n > 0 ? prec / n : 0;
            toolFactor = 1 - s.toolWeight * (1 - toolFitness);
            wrongTool = wrong;
            if (wrong) marks |= HarvestMarks.Rough;
            harvestMarks = marks & HarvestMarksInfo.Harvest;
            skill = HarvestSkills.Factor(s, skills, m.Skill);
            bonus = s.skillQualityBonus * skill + (HarvestSkills.Knows(s, skills, HarvestSkills.HuntersKnowledge) ? 0.02 : 0);
            HarvestCondition k = c.condition;
            if (k.elite) bonus += 0.04;
            flawlessPelts = HarvestSkills.Knows(s, skills, HarvestSkills.FlawlessPelts);
            primeCuts = HarvestSkills.Knows(s, skills, HarvestSkills.PrimeCuts);
            noWaste = HarvestSkills.Knows(s, skills, HarvestSkills.NoWaste);
            glandCare = HarvestSkills.Knows(s, skills, HarvestSkills.GlandCare);
        }
    }

    /// <summary>How the creature's age changes a material's quality (tender young meat, an old stag's antlers).</summary>
    private static double AgeQuality(HarvestAge age, HarvestMaterialKind kind)
    {
        bool trophy = kind == HarvestMaterialKind.Horn || kind == HarvestMaterialKind.Claw || kind == HarvestMaterialKind.Tooth || kind == HarvestMaterialKind.Bone;
        bool meat = kind == HarvestMaterialKind.Meat || kind == HarvestMaterialKind.Fat || kind == HarvestMaterialKind.Organ;
        switch (age)
        {
            case HarvestAge.Young: return trophy ? 0.85 : meat ? 1.06 : 1.02;
            case HarvestAge.Old: return trophy ? 1.08 : meat ? 0.92 : 0.97;
            case HarvestAge.Ancient: return trophy ? 1.15 : meat ? 0.85 : 0.95;
            default: return 1;
        }
    }

    /// <summary>How much a creature's size and age scale its yields.</summary>
    public static double YieldScale(HarvestCondition c)
    {
        double size = Math.Max(0.25, Math.Min(4.0, Math.Pow(Math.Max(0.05, c.sizeScale), 2.4)));
        switch (c.age)
        {
            case HarvestAge.Young: return size * 0.7;
            case HarvestAge.Old: return size * 1.05;
            case HarvestAge.Ancient: return size * 1.15;
            default: return size;
        }
    }

    private static float Final(double q) => (float)HarvestMath.Clamp01(q);

    // ------------------------------------------------------------------ the hide
    private static void Hide(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, int mi, HarvestMaterial m, HarvestTaskResult r, Factors f, HarvestOutcome o)
    {
        HarvestCondition k = c.condition;
        HarvestStageResult peel = null, scrape = null;
        double edges = 0;
        int traces = 0;
        foreach (HarvestStageResult st in r.stages)
        {
            if (st == null) continue;
            if (st.kind == HarvestStageKind.Peel) peel = st;
            else if (st.kind == HarvestStageKind.Scrape) scrape = st;
            else if (st.kind == HarvestStageKind.Trace) { edges += st.precision; traces++; }
        }
        double edge = traces > 0 ? edges / traces : 1;
        // The cells as the hands left them (a hide peeled off; plucking or scraping leaves the skin as it is).
        if (peel != null)
        {
            for (int i = 0; i < k.pelt.Count; i++)
            {
                HarvestPeltCell cell = k.pelt[i];
                if (!cell.present) continue;
                float hd = peel.cellDamage != null && i < peel.cellDamage.Count ? HarvestMathF.Clamp01(peel.cellDamage[i]) : 0f;
                cell.harvest = Math.Max(cell.harvest, hd);
                cell.flesh = peel.cellFlesh != null && i < peel.cellFlesh.Count ? HarvestMathF.Clamp01(peel.cellFlesh[i]) : 0.3f;
                cell.peeled = true;
                if (hd >= 0.3f) cell.marks |= HarvestMarks.Ripped;
                else if (hd > 0.04f) cell.marks |= HarvestMarks.Nicked;
            }
            if (peel.lostCells != null)
                foreach (int i in peel.lostCells)
                    if (i >= 0 && i < k.pelt.Count && k.pelt[i].present) k.pelt[i].lost = true;
        }
        // The hide's cells: all of a pelt. Feathers or scales are judged part by part over the parts they grow on (by their
        // shares): each by its own cells - on a creature with a pelt as well, the pelt's other cells are not theirs, and
        // hacking the pelt off leaves those parts' cells on - and a part with no cells of its own (a griffon's wing) by
        // its small hide, as the window judges it.
        double area, integrity;
        HarvestMarks cellMarks = HarvestMarks.None;
        if (m.kind == HarvestMaterialKind.Hide || m.sources == null)
        {
            Cells(k, -1, out area, out integrity, ref cellMarks);
        }
        else
        {
            double wa = 0, wi = 0, w = 0;
            foreach (HarvestSource src in m.sources)
            {
                int ri = a.RegionIndex(src.region);
                if (ri < 0 || ri >= k.regions.Count) continue;
                double share = Math.Max(0.0001f, src.share), ar = 1, ir;
                if (k.HasHideCells(ri))
                    Cells(k, ri, out ar, out ir, ref cellMarks);
                else
                {
                    ir = c.RegionHideCondition(ri);
                    cellMarks |= k.regions[ri].CoverMarks();
                }
                wa += ar * share;
                wi += ir * share;
                w += share;
            }
            area = w > 0 ? wa / w : 0;
            integrity = w > 0 ? wi / w : 0;
        }
        double flawless = f.flawlessPelts ? 0.05 : 0.0;
        double q = Math.Pow(integrity, 1.15) * (0.85 + 0.15 * edge) * f.freshFactor * f.toolFactor * AgeQuality(k.age, m.kind) + f.bonus + flawless;
        HarvestMarks marks = (cellMarks & (HarvestCarcass.Relevant(m.kind) | HarvestMarksInfo.Harvest)) | f.harvestMarks | f.timeMarks;
        if (edge < 0.6) marks |= HarvestMarks.Rough;

        // Plucking a griffon's feathers leaves its pelt on: only the hide that covers the meat frees it.
        if (HarvestCarcass.TakesHideOff(a, m))
            c.hideOff = true;
        c.TakeShare(mi, 1f);
        if (m.kind == HarvestMaterialKind.Hide)
        {
            Give(s, o, new HarvestPiece(mi, -1, 1, Final(q), marks), m);
            o.hideSource = k.Clone();
            int holes = 0;
            foreach (HarvestPeltCell cell in k.pelt) if (cell.present && !cell.lost && cell.Hole) holes++;
            o.lines.Add($"The {m.Name.ToLowerInvariant()} comes off: {area * 100:0}% of it whole{(holes > 0 ? $", {holes} hole{(holes == 1 ? "" : "s")}" : "")}.");
        }
        else
        {
            // Feathers or loose scales: a count of good ones.
            HarvestStageResult work = scrape ?? peel;
            double completeness = work != null ? HarvestMath.Clamp01(work.completeness) : 0.5;
            double yield = Roll(k.seed, mi, c.tasks, m.yield) * YieldScale(k) * area * completeness * (1 + s.skillYieldBonus * f.skill);
            int count = Count(yield, k.seed, mi, c.tasks);
            double dq = q * (1 - 0.6 * (work != null ? HarvestMath.Clamp01(work.damage) : 0));
            if (count > 0) Give(s, o, new HarvestPiece(mi, -1, count, Final(dq), marks), m);
        }
    }

    /// <summary>
    /// The hide's cells over <paramref name="region"/> (-1: all): how much of it came off whole (<paramref name="area"/>:
    /// cells not lost) and its condition (fight and harvest damage, lost cells as nothing), and their marks.
    /// </summary>
    private static void Cells(HarvestCondition k, int region, out double area, out double integrity, ref HarvestMarks marks)
    {
        int present = 0, kept = 0;
        double sum = 0;
        foreach (HarvestPeltCell cell in k.pelt)
        {
            if (!cell.present || region >= 0 && cell.region != region) continue;
            present++;
            if (cell.lost) continue;
            kept++;
            sum += 1 - cell.Total;
            marks |= cell.marks;
        }
        area = present > 0 ? kept / (double)present : 0;
        integrity = present > 0 ? sum / present : 0;
    }

    // ------------------------------------------------------------------ bulk yields (meat, fat)
    private static void Bulk(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, int mi, HarvestMaterial m, HarvestTaskResult r, Factors f, HarvestOutcome o)
    {
        HarvestCondition k = c.condition;
        double combat = c.RegionIntegrity(a, m, HarvestTissue.Meat);
        double contamination = 0, poison = 0, taint = 0, burn = 0, w = 0;
        foreach (HarvestSource src in m.sources)
        {
            int ri = a.RegionIndex(src.region);
            if (ri < 0 || ri >= k.regions.Count) continue;
            double share = Math.Max(0.0001f, src.share);
            contamination += k.regions[ri].contamination * share;
            poison += k.regions[ri].poison * share;
            taint += k.regions[ri].taint * share;
            burn += k.regions[ri].burn * share;
            w += share;
        }
        if (w > 0) { contamination /= w; poison /= w; taint /= w; burn /= w; }

        double precision = 0, completeness = 0, damage = 0;
        int n = 0;
        foreach (HarvestStageResult st in r.stages)
        {
            if (st == null) continue;
            precision += st.precision;
            completeness += st.completeness;
            damage = 1 - (1 - damage) * (1 - HarvestMath.Clamp01(st.damage));
            n++;
        }
        precision = n > 0 ? precision / n : 0;
        completeness = n > 0 ? completeness / n : 0;
        bool butchering = m.method == HarvestMethod.Butchering;
        if (butchering && f.noWaste)
            completeness = Math.Max(completeness, 0.85);
        double skillYield = (butchering ? s.butcheringYieldBonus : s.skillYieldBonus) * f.skill + (butchering && f.primeCuts ? 0.1 : 0);
        double left = 1 - c.TakenShare(mi);
        double yield = Roll(k.seed, mi, c.tasks, m.yield) * YieldScale(k) * left * combat * HarvestMath.Clamp01(completeness) * (1 + skillYield);
        int count = Count(yield, k.seed, mi, c.tasks);

        double q = (0.7 + 0.3 * combat) * (1 - 0.5 * damage) * (0.75 + 0.25 * precision) * (1 - Math.Min(0.8, contamination * 0.8)) * (1 - Math.Min(0.5, burn * 0.5))
                   * (1 - Math.Min(0.6, taint * 0.6)) * f.freshFactor * f.toolFactor * AgeQuality(k.age, m.kind) + f.bonus + (butchering && f.primeCuts ? 0.03 : 0);
        HarvestMarks marks = c.CombatMarks(a, mi) | f.harvestMarks | f.timeMarks;
        if (poison > 0.12 || (marks & HarvestMarks.Poisoned) != 0)
        {
            q = Math.Min(q, 0.25);
            marks |= HarvestMarks.Poisoned;
        }
        if (taint > 0.12) marks |= HarvestMarks.Tainted;
        // Soiled by a torn gut: in the fight (gut-shot) or while working (a spill).
        if (contamination > 0.3) marks |= (marks & HarvestMarks.Contaminated) != 0 ? HarvestMarks.Contaminated : HarvestMarks.GutShot;
        if (burn > 0.3) marks |= HarvestMarks.Burned;
        c.TakeShare(mi, 1f);
        if (count <= 0)
        {
            o.lines.Add($"Too little of the {m.Name.ToLowerInvariant()} is left to take.");
            return;
        }
        Give(s, o, new HarvestPiece(mi, -1, count, Final(q), marks), m);
        o.lines.Add($"{count} {Plural(m.unit, count)} of {m.Name.ToLowerInvariant()}{(completeness < 0.7 ? " - a lot of it wasted" : "")}.");
    }

    // ------------------------------------------------------------------ countable pieces
    private static void Countable(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, int mi, HarvestMaterial m, HarvestTaskResult r, Factors f, HarvestOutcome o)
    {
        HarvestCondition k = c.condition;
        var units = new List<HarvestUnit>();
        foreach (HarvestUnit u in k.units)
        {
            if (u.material != mi || u.taken || u.integrity < s.destroyedBelow) continue;
            if (r.units != null && r.units.Count > 0 && !r.units.Contains(u.index)) continue;
            units.Add(u);
        }
        if (units.Count == 0)
        {
            o.lines.Add($"There is no {m.Name.ToLowerInvariant()} left to take.");
            return;
        }
        double age = AgeQuality(k.age, m.kind);
        foreach (HarvestUnit u in units)
        {
            double damage = 0, worked = 0, precision = 0, completeness = 1, clean = -1;
            int n = 0;
            HarvestMarks own = HarvestMarks.None;
            foreach (HarvestStageResult st in r.stages)
            {
                if (st == null || (st.unit >= 0 && st.unit != u.index)) continue;
                damage = 1 - (1 - damage) * (1 - HarvestMath.Clamp01(st.damage));
                // The harm done to this piece itself: not the cut that opened the body for all of them (its slips into
                // the gut are spills).
                if (st.unit >= 0 || st.kind != HarvestStageKind.Trace)
                    worked = 1 - (1 - worked) * (1 - HarvestMath.Clamp01(st.damage));
                precision += st.precision;
                completeness = Math.Min(completeness, HarvestMath.Clamp01(st.completeness));
                if (st.kind == HarvestStageKind.Scrape) clean = HarvestMath.Clamp01(st.precision);
                own |= st.marks;
                n++;
            }
            precision = n > 0 ? precision / n : 0;
            double q = u.integrity * (1 - damage) * (0.8 + 0.2 * precision) * f.freshFactor * f.toolFactor * age + f.bonus;
            HarvestMarks marks = (u.marks & HarvestCarcass.Relevant(m.kind)) | (own & HarvestMarksInfo.Harvest) | f.timeMarks;
            if (f.wrongTool) marks |= HarvestMarks.Rough;
            switch (m.method)
            {
                case HarvestMethod.Bones:
                    if ((u.marks & HarvestMarks.Fractured) != 0) q *= 0.6;
                    if (clean >= 0) q *= 0.6 + 0.4 * clean;
                    break;
                case HarvestMethod.Tendons:
                    if (damage > 0.55)
                    {
                        q *= 0.6;
                        marks |= HarvestMarks.Ripped;
                        o.lines.Add($"The {HarvestCondition.UnitName(a, u)} snaps: a short length comes out.");
                    }
                    break;
                case HarvestMethod.Organs:
                {
                    int ri = u.region;
                    HarvestRegionState st = ri >= 0 && ri < k.regions.Count ? k.regions[ri] : null;
                    if (st != null && st.contamination > 0.3f)
                    {
                        q *= 1 - Math.Min(0.6, st.contamination * 0.5);
                        marks |= (st.marks & HarvestMarks.Contaminated) != 0 ? HarvestMarks.Contaminated : HarvestMarks.GutShot;
                    }
                    if (st != null && st.taint > 0.12f)
                    {
                        q *= 1 - Math.Min(0.6, st.taint * 0.6);
                        marks |= HarvestMarks.Tainted;
                    }
                    if ((u.marks & HarvestMarks.Poisoned) != 0 || st != null && st.poison > 0.12f)
                    {
                        q = Math.Min(q, 0.25);
                        marks |= HarvestMarks.Poisoned;
                    }
                    bool bursts = m.fragile && worked > (f.glandCare ? 0.85 : 0.6);
                    // A gut, bladder or gall torn open while working it out soils what is around it.
                    if (m.hazard && (bursts || worked > 0.5))
                    {
                        k.SoilAround(a, u, HarvestMarks.Contaminated);
                        marks |= HarvestMarks.Contaminated;
                        o.lines.Add($"The {HarvestCondition.UnitName(a, u)} tears open and soils what is around it.");
                    }
                    if (bursts)
                    {
                        u.taken = true;
                        o.discarded++;
                        o.lines.Add($"The {HarvestCondition.UnitName(a, u)} bursts in your hands.");
                        continue;
                    }
                    break;
                }
            }
            u.taken = true;
            if (completeness < 0.5) q *= 0.5 + completeness;
            Give(s, o, new HarvestPiece(mi, u.index, 1, Final(q), marks), m);
            if (m.method == HarvestMethod.Bones)
                WasteMeatAround(a, s, c, u.region, o);
        }
        if (m.method == HarvestMethod.Organs)
        {
            c.cavityOpen = true;
            if (m.hazard && (m.Shape == "intestines" || m.Shape == "stomach") && c.UnitsLeft(s, mi) == 0)
            {
                c.gutted = true;
                o.lines.Add("The carcass is gutted: its meat keeps longer.");
            }
        }
    }

    /// <summary>A bone cut out before the meat around it was taken ruins half of that meat.</summary>
    private static void WasteMeatAround(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, int region, HarvestOutcome o)
    {
        if (region < 0 || region >= a.regions.Count)
            return;
        string rid = a.regions[region].id;
        for (int i = 0; i < a.materials.Count; i++)
        {
            HarvestMaterial m = a.materials[i];
            if (m == null || m.countable || (m.kind != HarvestMaterialKind.Meat && m.kind != HarvestMaterialKind.Fat) || c.Gone(a, s, i)) continue;
            float total = 0f, here = 0f;
            foreach (HarvestSource src in m.sources)
            {
                total += Math.Max(0f, src.share);
                if (string.Equals(src.region, rid, StringComparison.OrdinalIgnoreCase)) here += Math.Max(0f, src.share);
            }
            if (here <= 0f || total <= 0f) continue;
            c.TakeShare(i, here / total * 0.5f);
            o.lines.Add($"Cutting the bone out tears up the {m.Name.ToLowerInvariant()} around it.");
        }
    }

    /// <summary>What the meat around a region's bones would lose (shown before a bone is taken).</summary>
    public static List<string> MeatAtRisk(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, HarvestMaterial bone)
    {
        var list = new List<string>();
        if (bone == null || bone.method != HarvestMethod.Bones)
            return list;
        foreach (HarvestSource src in bone.sources)
            for (int i = 0; i < a.materials.Count; i++)
            {
                HarvestMaterial m = a.materials[i];
                if (m == null || m.countable || (m.kind != HarvestMaterialKind.Meat && m.kind != HarvestMaterialKind.Fat) || c.Gone(a, s, i)) continue;
                foreach (HarvestSource ms in m.sources)
                    if (string.Equals(ms.region, src.region, StringComparison.OrdinalIgnoreCase) && !list.Contains(m.Name))
                        list.Add(m.Name);
            }
        return list;
    }

    // ------------------------------------------------------------------ spills
    /// <summary>A gut, bladder or gall cut open while working soils the cavity: its organs and meat.</summary>
    private static void Spills(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, int mi, HarvestMaterial m, HarvestTaskResult r, HarvestOutcome o)
    {
        float spill = 0f;
        foreach (HarvestStageResult st in r.stages)
            if (st != null) spill = Math.Max(spill, st.spill);
        if (spill <= 0.01f)
            return;
        HarvestCondition k = c.condition;
        // The regions holding the body's organs.
        bool any = false;
        for (int ri = 0; ri < k.regions.Count && ri < a.regions.Count; ri++)
        {
            if (a.regions[ri] == null || a.regions[ri].organs < 0.3f) continue;
            k.regions[ri].contamination += spill;
            k.regions[ri].marks |= HarvestMarks.Contaminated;
            foreach (HarvestUnit u in k.units)
                if (u.region == ri && !u.taken && a.materials[u.material].method == HarvestMethod.Organs)
                {
                    u.marks |= HarvestMarks.Contaminated;
                    u.integrity = Math.Max(0f, u.integrity - spill * 0.3f);
                }
            any = true;
        }
        if (any)
            o.lines.Add(spill > 0.5f ? "The knife opens the gut: everything in the cavity is soiled." : "A nick in the gut leaks into the cavity.");
    }

    // ------------------------------------------------------------------ the quick way
    /// <summary>
    /// Strips the hide off roughly (so the meat can be reached): it is ruined, the work takes a moment. Refused while
    /// someone else is skinning it.
    /// </summary>
    private static HarvestOutcome StripRoughly(HarvestAnatomyData a, HarvestSettings s, HarvestCarcass c, HarvestOutcome o, int player)
    {
        if (c.HideWorkedBy(a, s, player) != 0)
            return HarvestOutcome.Refused("Someone is working on the hide.");
        int hide = c.HideIndex(a, s);
        c.hideOff = true;
        if (hide >= 0)
        {
            c.TakeShare(hide, 1f);
            c.Unlock(hide, player);
            // The hide is ruined - but a pelt hacked off leaves the feathers or scales on the parts that grow them, as
            // skinning it would.
            bool pelt = a.materials[hide].kind == HarvestMaterialKind.Hide;
            foreach (HarvestPeltCell cell in c.condition.pelt)
                if (cell.present && !(pelt && HarvestCondition.Covered(a, cell.region))) cell.lost = true;
        }
        o.lines.Add("You hack the hide off: the meat is free, the hide is ruined.");
        o.performance = 0.1;
        o.skill = HarvestSkill.Skinning;
        c.tasks++;
        c.lastWork = c.age;
        return o;
    }

    // ------------------------------------------------------------------ helpers
    private static void Give(HarvestSettings s, HarvestOutcome o, HarvestPiece p, HarvestMaterial m)
    {
        if (p.count <= 0)
            return;
        if (p.quality < s.discardBelow)
        {
            o.discarded += p.count;
            o.lines.Add($"The {m.Name.ToLowerInvariant()} is ruined: you throw it away.");
            return;
        }
        o.pieces.Add(p);
    }

    /// <summary>A repeatable roll between a material's yield min and max.</summary>
    private static double Roll(int seed, int material, int salt, UnityEngine.Vector2 range)
    {
        double lo = Math.Min(range.x, range.y), hi = Math.Max(range.x, range.y);
        return lo + (hi - lo) * HarvestMath.Noise(seed, material * 31 + salt, 101);
    }

    /// <summary>A whole number of pieces from a yield (the fraction is a repeatable chance of one more; a little is still one).</summary>
    private static int Count(double yield, int seed, int material, int salt)
    {
        if (yield <= 0.25)
            return 0;
        int whole = (int)Math.Floor(yield);
        if (HarvestMath.Noise(seed, material * 37 + salt, 103) < yield - whole) whole++;
        return Math.Max(1, whole);
    }

    private static string Plural(string unit, int n) => string.IsNullOrEmpty(unit) ? (n == 1 ? "piece" : "pieces") : n == 1 ? unit : unit.EndsWith("s", StringComparison.Ordinal) ? unit : unit + "s";
}
