using System;
using System.Collections.Generic;

/// <summary>A hit on a creature, as harvesting sees it: where, what kind, how hard.</summary>
public struct HarvestHit
{
    /// <summary>Index of the region hit (-1 = unknown: the anatomy's default region).</summary>
    public int region;
    public DamageForm form;
    public ElementType element;
    /// <summary>Area damage (an explosion, a slam): it spreads over the body.</summary>
    public bool area;
    /// <summary>Damage over time (burning, poison): no new wound, the element only.</summary>
    public bool periodic;
    public bool critical;
    /// <summary>How hard: the damage against the creature's max health × the settings' Severity Per Health.</summary>
    public float severity;
    /// <summary>Where on the region it landed (0-1 across and along; -1 = unknown).</summary>
    public float u, v;
    /// <summary>What dealt it ("Iron arrow", "Fireball"), for the wound list.</summary>
    public string source;
    /// <summary>When (seconds), for the wound list.</summary>
    public float time;
}

/// <summary>The state of one region's tissues (1 = intact).</summary>
[Serializable]
public class HarvestRegionState
{
    public float meat = 1f;
    public float bone = 1f;
    public float tendon = 1f;
    public float organs = 1f;
    /// <summary>Soiled by a torn gut, bladder or gall (0-1+): it spoils faster and is not fit to eat.</summary>
    public float contamination;
    /// <summary>Poison in its flesh (0-1+).</summary>
    public float poison;
    /// <summary>Withered by dark magic (0-1+).</summary>
    public float taint;
    /// <summary>Charred (0-1+).</summary>
    public float burn;
    /// <summary>
    /// The feathers or scales of a part with no hide cells of its own (a griffon's wing): a small hide of its own, 2 × 2
    /// cells, that the fight marks and time heals as it does the hide's. Empty until the fight first reaches it.
    /// </summary>
    public List<HarvestPeltCell> cover = new List<HarvestPeltCell>();
    /// <summary>Frozen (0-1+): it keeps.</summary>
    public float frost;
    public HarvestMarks marks;
    public int hits;

    public HarvestRegionState Clone()
    {
        var c = (HarvestRegionState)MemberwiseClone();
        c.cover = new List<HarvestPeltCell>();
        if (cover != null)
            foreach (HarvestPeltCell x in cover)
                if (x != null) c.cover.Add(x.Clone());
        return c;
    }

    /// <summary>The condition from the fight of its <see cref="cover"/> (0-1), judged as the hide's cells are.</summary>
    public double CoverCondition()
    {
        double sum = 0;
        int n = 0;
        if (cover != null)
            for (int i = 0; i < cover.Count; i++)
                if (cover[i] != null && cover[i].present)
                {
                    sum += cover[i].FightCondition;
                    n++;
                }
        return n > 0 ? sum / n : 1;
    }

    /// <summary>The marks of the fight on its <see cref="cover"/>.</summary>
    public HarvestMarks CoverMarks()
    {
        HarvestMarks m = HarvestMarks.None;
        if (cover != null)
            for (int i = 0; i < cover.Count; i++)
                if (cover[i] != null && cover[i].present) m |= cover[i].marks;
        return m;
    }

    public float Get(HarvestTissue t)
    {
        switch (t)
        {
            case HarvestTissue.Meat: return meat;
            case HarvestTissue.Bone: return bone;
            case HarvestTissue.Tendon: return tendon;
            case HarvestTissue.Organs: return organs;
            default: return 1f;
        }
    }
}

/// <summary>
/// One countable piece of a creature (a claw, a tooth, a bone, an organ, a tendon), with its own condition: the arrow
/// that went through the heart ruined that heart, not the liver beside it.
/// </summary>
[Serializable]
public class HarvestUnit
{
    /// <summary>Index of its material in the anatomy.</summary>
    public int material;
    /// <summary>Which piece of that material (0..).</summary>
    public int index;
    /// <summary>Index of the region it is in.</summary>
    public int region;
    public float integrity = 1f;
    public HarvestMarks marks;
    /// <summary>Taken off the carcass.</summary>
    public bool taken;

    public HarvestUnit Clone() => (HarvestUnit)MemberwiseClone();
}

/// <summary>
/// One cell of the hide laid flat (flesh side up). The fight marks it (<see cref="damage"/>, <see cref="burn"/>); the
/// skinning marks it again (<see cref="harvest"/>: tears and nicks) and decides how much flesh is left on it. The cells
/// become the cells of the tannery's hide.
/// </summary>
[Serializable]
public class HarvestPeltCell
{
    /// <summary>Part of the hide (inside its outline).</summary>
    public bool present;
    public int region = -1;
    /// <summary>Face, paws, tail, joints: hard to skin.</summary>
    public bool difficult;
    /// <summary>Damage from the fight (0 intact .. 1 a hole).</summary>
    public float damage;
    /// <summary>Burned (0-1+): hair singed at 0.3, burned through past 1.</summary>
    public float burn;
    /// <summary>Damage done while skinning (tears, nicks).</summary>
    public float harvest;
    /// <summary>Flesh left on it (0-1) when it came off.</summary>
    public float flesh;
    /// <summary>Separated from the body (skinning progress).</summary>
    public bool peeled;
    /// <summary>Torn off or cut away while skinning: not on the hide that came off.</summary>
    public bool lost;
    public HarvestMarks marks;

    /// <summary>All the damage it has (fight and harvest).</summary>
    public float Total => Math.Min(1f, damage + harvest + Math.Max(0f, burn - 0.6f));
    /// <summary>What the fight left of it (0-1): its damage, and how badly it burned.</summary>
    public float FightCondition => 1 - Math.Min(1f, damage + Math.Max(0f, burn - 0.3f) * 0.6f);
    /// <summary>A hole (from anything).</summary>
    public bool Hole => Total >= 0.85f || lost;

    public HarvestPeltCell Clone() => (HarvestPeltCell)MemberwiseClone();
}

/// <summary>A wound the creature took (the inspection lists them).</summary>
[Serializable]
public class HarvestWound
{
    public int region;
    public DamageForm form;
    public ElementType element;
    public float severity;
    public bool critical;
    public bool area;
    public bool periodic;
    public string source = "";
    public float time;
    /// <summary>What it did that matters ("pierced the heart", "broke the left femur").</summary>
    public string effect = "";

    public HarvestWound Clone() => (HarvestWound)MemberwiseClone();
}

/// <summary>
/// Everything that happened to one creature's body: each region's tissues, every countable piece (claws, teeth, bones,
/// organs, tendons), its hide cell by cell and the wounds it took - with the cause of each mark. Hits fill it while the
/// creature lives (<see cref="ApplyHit"/>); the carcass is harvested from it. Plain data: saved, sent and tested as is.
/// </summary>
[Serializable]
public class HarvestCondition
{
    public string anatomy = "";
    public int seed;
    /// <summary>Its size against the anatomy's reference (1 = typical).</summary>
    public float sizeScale = 1f;
    public HarvestAge age = HarvestAge.Adult;
    public bool elite;
    public List<HarvestRegionState> regions = new List<HarvestRegionState>();
    public List<HarvestUnit> units = new List<HarvestUnit>();
    /// <summary>Indices of the materials this creature does not have (rare finds that are not there).</summary>
    public List<int> missing = new List<int>();
    public int peltWidth, peltHeight;
    public List<HarvestPeltCell> pelt = new List<HarvestPeltCell>();
    public List<HarvestWound> wounds = new List<HarvestWound>();
    public int hits;
    /// <summary>Burning (or frost...) over time not yet enough to mark the hide, per element: the ticks add up until they do.</summary>
    public List<float> periodicHide = new List<float>();
    /// <summary>The same for the coverings of parts with no hide cells (feathers on a griffon's wings), per element and region.</summary>
    public List<float> periodicCover = new List<float>();

    [NonSerialized] private List<int>[] regionCells;
    [NonSerialized] private List<int> presentCells;

    // ------------------------------------------------------------------ making one
    /// <summary>
    /// A fresh, unhurt creature of <paramref name="a"/>: its regions, its pieces (rare ones rolled from
    /// <paramref name="seed"/>, likelier by <paramref name="rareBonus"/>), its hide. The same seed always gives the same creature.
    /// </summary>
    public static HarvestCondition Create(HarvestAnatomyData a, int seed, float sizeScale = 1f, HarvestAge age = HarvestAge.Adult, bool elite = false, float rareBonus = 1f)
    {
        var c = new HarvestCondition
        {
            anatomy = a != null ? a.id : "",
            seed = seed,
            sizeScale = Math.Max(0.05f, sizeScale),
            age = age,
            elite = elite,
        };
        if (a == null)
            return c;
        int rc = a.regions != null ? a.regions.Count : 0;
        for (int i = 0; i < rc; i++)
            c.regions.Add(new HarvestRegionState());
        float bonus = Math.Max(0f, rareBonus) * (elite ? 2f : 1f) * (age == HarvestAge.Ancient ? 1.5f : age == HarvestAge.Old ? 1.2f : 1f);
        if (a.materials != null)
            for (int m = 0; m < a.materials.Count; m++)
            {
                HarvestMaterial mat = a.materials[m];
                if (mat == null)
                    continue;
                if (mat.chance < 0.999f && HarvestMath.Noise(seed, m, 7) >= Math.Min(1f, mat.chance * bonus))
                {
                    c.missing.Add(m);
                    continue;
                }
                if (!mat.countable || mat.sources == null)
                    continue;
                int index = 0;
                foreach (HarvestSource s in mat.sources)
                {
                    if (s == null) continue;
                    int r = a.RegionIndex(s.region);
                    for (int k = 0; k < s.units; k++)
                    {
                        var u = new HarvestUnit { material = m, index = index, region = Math.Max(0, r) };
                        // Old creatures have chipped claws and worn teeth.
                        if ((age == HarvestAge.Old || age == HarvestAge.Ancient) && mat.method == HarvestMethod.Trophies &&
                            HarvestMath.Noise(seed, m * 64 + index, 11) < (age == HarvestAge.Ancient ? 0.25 : 0.12))
                            u.integrity = (float)(0.6 + 0.3 * HarvestMath.Noise(seed, m * 64 + index, 12));
                        c.units.Add(u);
                        index++;
                    }
                }
            }
        c.BuildPelt(a);
        return c;
    }

    /// <summary>Lays the hide out: a cell belongs to the most specific region whose pelt area holds its centre.</summary>
    private void BuildPelt(HarvestAnatomyData a)
    {
        peltWidth = Math.Max(1, a.peltSize.x);
        peltHeight = Math.Max(1, a.peltSize.y);
        pelt.Clear();
        for (int y = 0; y < peltHeight; y++)
            for (int x = 0; x < peltWidth; x++)
            {
                int best = PeltOwner(a, x, y, peltWidth, peltHeight);
                pelt.Add(new HarvestPeltCell
                {
                    present = best >= 0,
                    region = best,
                    difficult = best >= 0 && a.regions[best].difficultSkin,
                });
            }
        regionCells = null;
        presentCells = null;
    }

    /// <summary>The region cell (<paramref name="x"/>, <paramref name="y"/>) of a hide <paramref name="width"/>×<paramref name="height"/> belongs to, or -1 (not part of the hide).</summary>
    public static int PeltOwner(HarvestAnatomyData a, int x, int y, int width, int height)
    {
        float u = (x + 0.5f) / Math.Max(1, width), v = (y + 0.5f) / Math.Max(1, height);
        int best = -1;
        float bestArea = float.MaxValue;
        for (int r = 0; r < a.regions.Count; r++)
        {
            HarvestRegion reg = a.regions[r];
            if (reg == null || reg.hide <= 0f) continue;
            UnityEngine.Rect p = reg.pelt;
            if (u < p.x || u > p.x + p.width || v < p.y || v > p.y + p.height) continue;
            float area = p.width * p.height;
            if (area < bestArea) { bestArea = area; best = r; }
        }
        return best;
    }

    /// <summary>How many cells of the creature's hide each of its regions gets.</summary>
    public static int[] PeltCells(HarvestAnatomyData a)
    {
        var n = new int[a?.regions?.Count ?? 0];
        if (n.Length == 0)
            return n;
        int w = Math.Max(1, a.peltSize.x), h = Math.Max(1, a.peltSize.y);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int r = PeltOwner(a, x, y, w, h);
                if (r >= 0) n[r]++;
            }
        return n;
    }

    // ------------------------------------------------------------------ lookups
    public HarvestPeltCell Cell(int x, int y) => x < 0 || y < 0 || x >= peltWidth || y >= peltHeight ? null : pelt[y * peltWidth + x];

    /// <summary>Does the creature have material <paramref name="material"/> (rare finds may be missing)?</summary>
    public bool Has(int material) => !missing.Contains(material);

    /// <summary>The pieces of material <paramref name="material"/>.</summary>
    public List<HarvestUnit> UnitsOf(int material, List<HarvestUnit> into = null)
    {
        into ??= new List<HarvestUnit>();
        into.Clear();
        foreach (HarvestUnit u in units)
            if (u.material == material) into.Add(u);
        return into;
    }

    /// <summary>The cells of region <paramref name="region"/> (present ones).</summary>
    public List<int> CellsOf(int region)
    {
        if (regionCells == null || regionCells.Length != regions.Count)
        {
            regionCells = new List<int>[regions.Count];
            for (int i = 0; i < regionCells.Length; i++) regionCells[i] = new List<int>();
            for (int k = 0; k < pelt.Count; k++)
                if (pelt[k].present && pelt[k].region >= 0 && pelt[k].region < regionCells.Length)
                    regionCells[pelt[k].region].Add(k);
        }
        return region >= 0 && region < regionCells.Length ? regionCells[region] : new List<int>();
    }

    /// <summary>Every cell the hide has.</summary>
    private List<int> PresentCells()
    {
        if (presentCells == null || regionCells == null)
        {
            presentCells = new List<int>();
            for (int k = 0; k < pelt.Count; k++)
                if (pelt[k].present) presentCells.Add(k);
        }
        return presentCells;
    }

    /// <summary>The hide's condition from the fight (0-1): over one region, or the whole hide (-1).</summary>
    public double HideIntegrity(int region = -1)
    {
        double sum = 0;
        int n = 0;
        foreach (HarvestPeltCell c in pelt)
        {
            if (!c.present || (region >= 0 && c.region != region)) continue;
            sum += c.FightCondition;
            n++;
        }
        return n > 0 ? sum / n : 1;
    }

    /// <summary>The marks of the fight on a region's hide cells.</summary>
    public HarvestMarks HideMarks(int region = -1)
    {
        HarvestMarks m = HarvestMarks.None;
        foreach (HarvestPeltCell c in pelt)
            if (c.present && (region < 0 || c.region == region)) m |= c.marks;
        return m;
    }

    /// <summary>Does the hide have cells on <paramref name="region"/>?</summary>
    public bool HasHideCells(int region) => CellsOf(region).Count > 0;

    // ------------------------------------------------------------------ hits
    /// <summary>
    /// A hit: marks the region's tissues by what the hit's form and element do to each (and its severity), hurts one of
    /// the organs or bones there (whichever it reaches), cracks claws or horns, marks the hide cells where it landed,
    /// spreads area damage over the body, and remembers the wound. Natural armour and the creature's toughness and
    /// resistances soften it.
    /// </summary>
    public void ApplyHit(HarvestAnatomyData a, HarvestSettings s, HarvestHit hit)
    {
        if (a == null || s == null || regions.Count == 0 || a.regions == null || a.regions.Count != regions.Count)
            return;
        int r = hit.region >= 0 && hit.region < regions.Count ? hit.region : a.DefaultRegionIndex;
        if (r < 0) r = 0;
        float sev = Math.Min(Math.Max(0f, hit.severity), s.maxSeverity) / Math.Max(0.05f, a.toughness);
        if (hit.critical) sev *= s.criticalMultiplier;
        if (hit.periodic) sev *= s.periodicScale;
        if (sev <= 0.0005f)
            return;
        hits++;

        HarvestDamageResponse form = hit.periodic ? null : s.Form(hit.form);
        HarvestDamageResponse element = s.Element(hit.element);
        float keep = 1f - a.Resistance(hit.element);
        var w = new Weights(form, element, keep);
        if (hit.periodic)
        {
            // Burning spreads over the hide; poison and taint run through the blood (every region).
            if (element == null)
                return;
            if (w.contaminate > 0f)
            {
                for (int q = 0; q < regions.Count; q++)
                    Contaminate(a, q, sev * w.contaminate * 0.5f, element.marks);
            }
            if (element.hide > 0f && element.hideMark != HarvestHideMark.None && keep > 0f)
            {
                // Each tick adds a little; the hide is marked once it amounts to something (as much as a hit of that size would).
                int q = RandomHideRegion(a, hits);
                if (q >= 0 && a.regions[q] != null)
                {
                    HarvestRegion qr = a.regions[q];
                    int e = Math.Max(0, (int)hit.element);
                    while (periodicHide.Count <= e) periodicHide.Add(0f);
                    periodicHide[e] += sev * Math.Min(1.5f, element.hide) * (1f - 0.6f * qr.armor) * qr.hide * keep;
                    if (periodicHide[e] >= 0.1f)
                    {
                        Stamp(a, q, element, periodicHide[e], -1f, -1f, hits * 31 + 5);
                        periodicHide[e] = 0f;
                    }
                }
            }
            // Parts with no hide of their own that grow feathers or scales (a griffon's wings) take their share too.
            if (element.hide > 0f && element.hideMark != HarvestHideMark.None && keep > 0f)
                CoveringsOverTime(a, Math.Max(0, (int)hit.element), element, sev * Math.Min(1.5f, element.hide) * keep, hits * 31 + 6);
            string over = (element.marks & HarvestMarks.Burned) != 0 ? "burning" : (element.marks & HarvestMarks.Frozen) != 0 ? "frostbite" : "";
            NoteWound(a, s, r, hit, sev, over, true);
            return;
        }

        string effect;
        if (hit.area)
        {
            effect = ApplyToRegion(a, s, r, w, form, element, keep, sev, hit, true);
            float spread = sev * s.areaSpread;
            HarvestHit elsewhere = hit;
            elsewhere.u = elsewhere.v = -1f;
            for (int q = 0; q < regions.Count; q++)
                if (q != r)
                    ApplyToRegion(a, s, q, w, form, element, keep, spread * (0.6f + 0.4f * (float)HarvestMath.Noise(seed, hits * 17 + q, 3)), elsewhere, true);
            for (int q = 0; q < regions.Count; q++)
                regions[q].marks |= HarvestMarks.Blasted;
            if (string.IsNullOrEmpty(effect)) effect = "blasted the whole body";
        }
        else
        {
            effect = ApplyToRegion(a, s, r, w, form, element, keep, sev, hit, false);
        }
        NoteWound(a, s, r, hit, sev, effect, false);
    }

    /// <summary>The weights of one hit on each tissue: its form's plus its element's (× what the creature does not resist).</summary>
    private struct Weights
    {
        public float hide, meat, bone, tendon, organs, trophies, contaminate;
        public HarvestMarks marks;

        public Weights(HarvestDamageResponse form, HarvestDamageResponse element, float keep)
        {
            hide = meat = bone = tendon = organs = trophies = contaminate = 0f;
            marks = HarvestMarks.None;
            if (form != null)
            {
                hide += form.hide; meat += form.meat; bone += form.bone; tendon += form.tendon; organs += form.organs; trophies += form.trophies;
                contaminate += form.contaminate;
                marks |= form.marks;
            }
            if (element != null && keep > 0f)
            {
                hide += element.hide * keep; meat += element.meat * keep; bone += element.bone * keep; tendon += element.tendon * keep;
                organs += element.organs * keep; trophies += element.trophies * keep; contaminate += element.contaminate * keep;
                marks |= element.marks;
            }
        }
    }

    /// <summary>One region takes a hit; returns what it did that matters most (or "").</summary>
    private string ApplyToRegion(HarvestAnatomyData a, HarvestSettings s, int r, Weights w, HarvestDamageResponse form, HarvestDamageResponse element, float keep,
        float sev, HarvestHit hit, bool area)
    {
        HarvestRegion reg = a.regions[r];
        HarvestRegionState st = regions[r];
        if (reg == null)
            return "";
        st.hits++;
        float armor = reg.armor;
        float soft = 1f - 0.6f * armor;
        string effect = "";
        float worst = 0f;

        // The region's tissues.
        float meatHit = sev * w.meat * (area ? s.areaMeatMultiplier : 1f) * soft * reg.Tissue(HarvestTissue.Meat);
        float boneHit = sev * w.bone * reg.bone * (1f - 0.3f * armor) * (area ? 1.3f : 1f);
        float tendonHit = sev * w.tendon * reg.tendon * soft;
        float organHit = sev * w.organs * reg.organs * (1f - 0.4f * armor);
        st.meat = Math.Max(0f, st.meat - meatHit);
        st.bone = Math.Max(0f, st.bone - boneHit);
        st.tendon = Math.Max(0f, st.tendon - tendonHit);
        st.organs = Math.Max(0f, st.organs - organHit);
        if (meatHit > 0.02f || organHit > 0.02f || boneHit > 0.02f)
            st.marks |= w.marks & ~HarvestMarks.Frozen;
        if (element != null && (element.marks & HarvestMarks.Burned) != 0 && keep > 0f)
            st.burn += sev * element.meat * keep;
        if (element != null && (element.marks & HarvestMarks.Frozen) != 0 && keep > 0f)
        {
            st.frost += sev * keep;
            st.marks |= HarvestMarks.Frozen;
        }
        if (w.contaminate > 0f)
            Contaminate(a, r, sev * w.contaminate, (form != null ? form.marks : 0) | (element != null ? element.marks : 0));

        bool blunt = hit.form == DamageForm.Blunt || hit.form == DamageForm.Crush || hit.element == ElementType.Earth;
        // An organ: the one the hit reaches (bigger ones are likelier).
        if (organHit > 0.01f)
        {
            HarvestUnit u = PickUnit(a, r, HarvestMethod.Organs, hits * 7 + 1);
            if (u != null)
            {
                float before = u.integrity;
                u.integrity = Math.Max(0f, u.integrity - organHit * 1.6f);
                u.marks |= w.marks & (HarvestMarks.Pierced | HarvestMarks.Cut | HarvestMarks.Crushed | HarvestMarks.Burned | HarvestMarks.Poisoned |
                                      HarvestMarks.Tainted | HarvestMarks.Bruised | HarvestMarks.Torn);
                HarvestMaterial m = a.materials[u.material];
                if (before - u.integrity > worst)
                {
                    worst = before - u.integrity;
                    effect = $"{Verb(hit, u.integrity < 0.35f)} the {UnitName(a, u)}";
                }
                // A torn gut, bladder or gall soils everything around it.
                if (m.hazard && before >= 0.5f && u.integrity < 0.5f)
                {
                    SoilAround(a, u, HarvestMarks.GutShot);
                    effect = $"tore the {UnitName(a, u)} open";
                    worst = 1f;
                }
            }
        }
        // A bone.
        if (boneHit > 0.01f)
        {
            HarvestUnit u = PickUnit(a, r, HarvestMethod.Bones, hits * 7 + 2);
            if (u != null)
            {
                float before = u.integrity;
                u.integrity = Math.Max(0f, u.integrity - boneHit * 1.5f);
                if (blunt && before - u.integrity > 0.12f) u.marks |= HarvestMarks.Fractured;
                else u.marks |= w.marks & (HarvestMarks.Cut | HarvestMarks.Pierced | HarvestMarks.Burned | HarvestMarks.Crushed);
                if (before - u.integrity > worst && before - u.integrity > 0.12f)
                {
                    worst = before - u.integrity;
                    effect = blunt ? $"broke the {UnitName(a, u)}" : $"{Verb(hit, false)} the {UnitName(a, u)}";
                }
            }
        }
        // Tendons and sinew of the region.
        if (tendonHit > 0.01f)
            foreach (HarvestUnit u in units)
            {
                if (u.region != r || u.taken || a.materials[u.material].method != HarvestMethod.Tendons) continue;
                float before = u.integrity;
                u.integrity = Math.Max(0f, u.integrity - tendonHit * (0.6f + 0.8f * (float)HarvestMath.Noise(seed, hits * 13 + u.index, u.material)));
                u.marks |= w.marks & (HarvestMarks.Cut | HarvestMarks.Torn | HarvestMarks.Burned);
                if (before - u.integrity > worst && before - u.integrity > 0.25f)
                {
                    worst = before - u.integrity;
                    effect = $"cut the {UnitName(a, u)}";
                }
            }
        // Countable skins of the region (a drake's wing membranes) tear like a hide.
        if (w.hide > 0f)
            foreach (HarvestUnit u in units)
            {
                if (u.region != r || u.taken || a.materials[u.material].method != HarvestMethod.Skinning) continue;
                float before = u.integrity;
                u.integrity = Math.Max(0f, u.integrity - sev * w.hide * 0.8f * soft);
                u.marks |= w.marks & (HarvestMarks.Cut | HarvestMarks.Pierced | HarvestMarks.Torn | HarvestMarks.Burned | HarvestMarks.Crushed);
                if (before - u.integrity > worst && before - u.integrity > 0.25f)
                {
                    worst = before - u.integrity;
                    effect = $"tore the {UnitName(a, u)}";
                }
            }
        // Claws, teeth, horns, scales: one to three of them.
        if (w.trophies > 0f)
        {
            int count = 1 + (int)(HarvestMath.Noise(seed, hits, 41) * Math.Min(3f, 1f + sev * 2f));
            for (int k = 0; k < count; k++)
            {
                HarvestUnit u = PickUnit(a, r, HarvestMethod.Trophies, hits * 7 + 3 + k * 101);
                if (u == null) break;
                float amount = sev * w.trophies * (0.5f + (float)HarvestMath.Noise(seed, hits * 5 + k, 43)) * (1f + 0.5f * armor);
                float before = u.integrity;
                u.integrity = Math.Max(0f, u.integrity - amount);
                if (blunt || (element != null && (element.marks & HarvestMarks.Frozen) != 0)) u.marks |= HarvestMarks.Fractured;
                else u.marks |= w.marks & (HarvestMarks.Cut | HarvestMarks.Burned | HarvestMarks.Torn | HarvestMarks.Crushed);
                if (before - u.integrity > worst && before - u.integrity > 0.2f)
                {
                    worst = before - u.integrity;
                    effect = $"{(blunt ? "cracked" : "chipped")} a {UnitName(a, u, false)}";
                }
            }
        }

        // The hide where it landed: the form's mark, then the element's. A part with no hide cells of its own that grows
        // feathers or scales (a griffon's wing) has a small hide of its own, marked as the hide is.
        if (!HasHideCells(r) && Covered(a, r))
        {
            HarvestCondition covering = Covering(r, false);
            float shield = 1f - 0.6f * armor;
            if (form != null && form.hide > 0f && form.hideMark != HarvestHideMark.None)
                covering.Stamp(a, r, form, sev * Math.Min(1.5f, form.hide) * shield, -1f, -1f, hits * 31);
            if (element != null && element.hide > 0f && element.hideMark != HarvestHideMark.None && keep > 0f)
                covering.Stamp(a, r, element, sev * Math.Min(1.5f, element.hide) * shield * keep, -1f, -1f, hits * 31 + 7);
        }
        if (reg.hide > 0f)
        {
            float shield = (1f - 0.6f * armor) * reg.hide;
            int salt = hits * 31;
            float holes = 0f;
            if (form != null && form.hide > 0f && form.hideMark != HarvestHideMark.None)
                holes += Stamp(a, r, form, sev * Math.Min(1.5f, form.hide) * shield, hit.u, hit.v, salt);
            if (element != null && element.hide > 0f && element.hideMark != HarvestHideMark.None && keep > 0f)
                holes += Stamp(a, r, element, sev * Math.Min(1.5f, element.hide) * shield * keep, hit.u, hit.v, salt + 7);
            if (holes > 0.5f && worst < 0.3f && effect.Length == 0)
                effect = (element != null && (element.marks & HarvestMarks.Burned) != 0 ? "burned " : "holed ") + "the hide on the " + reg.Name.ToLowerInvariant();
        }
        if (effect.Length == 0 && meatHit > 0.25f)
            effect = $"tore into the {reg.Name.ToLowerInvariant()}";
        return effect;
    }

    /// <summary>Soils (poison, taint, a torn gut) a region's meat and organs.</summary>
    private void Contaminate(HarvestAnatomyData a, int r, float amount, HarvestMarks marks)
    {
        if (r < 0 || r >= regions.Count || amount <= 0f)
            return;
        HarvestRegionState st = regions[r];
        bool through;
        if ((marks & HarvestMarks.Poisoned) != 0)
        {
            st.poison += amount;
            through = st.poison > 0.12f;
            if (through) st.marks |= HarvestMarks.Poisoned;
        }
        else
        {
            st.taint += amount;
            through = st.taint > 0.12f;
            if (through) st.marks |= marks & HarvestMarks.Tainted;
        }
        foreach (HarvestUnit u in units)
            if (u.region == r && a.materials[u.material].method == HarvestMethod.Organs)
            {
                u.integrity = Math.Max(0f, u.integrity - amount * 0.25f);
                // One strong dose, or enough of them: the organs there are poisoned (or tainted) too.
                if (amount > 0.05f || through) u.marks |= marks & (HarvestMarks.Poisoned | HarvestMarks.Tainted);
            }
    }

    /// <summary>A piece of <paramref name="method"/> in region <paramref name="r"/> (bigger organs and bones are likelier), or null.</summary>
    private HarvestUnit PickUnit(HarvestAnatomyData a, int r, HarvestMethod method, int salt)
    {
        float total = 0f;
        foreach (HarvestUnit u in units)
            if (u.region == r && !u.taken && a.materials[u.material].method == method)
                total += SizeOf(a.materials[u.material]);
        if (total <= 0f)
            return null;
        double x = HarvestMath.Noise(seed, salt, 19) * total;
        HarvestUnit last = null;
        foreach (HarvestUnit u in units)
        {
            if (u.region != r || u.taken || a.materials[u.material].method != method) continue;
            last = u;
            x -= SizeOf(a.materials[u.material]);
            if (x <= 0) return u;
        }
        return last;
    }

    /// <summary>How big a target a piece is inside its region.</summary>
    public static float SizeOf(HarvestMaterial m)
    {
        switch (m.Shape)
        {
            case "lungs": return 2f;
            case "intestines": return 2.2f;
            case "liver": return 1.6f;
            case "stomach": return 1.4f;
            case "heart": return 1f;
            case "kidney": return 0.6f;
            case "spleen": return 0.6f;
            case "bladder": return 0.5f;
            case "gall": return 0.3f;
            case "gland": return 0.45f;
            case "ribs": return 1.6f;
            case "skull": return 1.3f;
            case "spine": return 1.2f;
            case "pelvis": return 1f;
            case "jaw": return 0.6f;
            case "longbone": return 1f;
            default: return 0.8f;
        }
    }

    /// <summary>
    /// Damage over time on the parts that grow feathers or scales but have no hide cells of their own, as on the hide: the
    /// ticks add up per element (softened by the part's armour) to a mark of 0.1 or more, which falls on one cell of a
    /// hide - on the part's small hide or the cells around it as often as on any 16 cells of the creature's (on a hide of
    /// fewer cells than that, more than once at a time).
    /// </summary>
    private void CoveringsOverTime(HarvestAnatomyData a, int e, HarvestDamageResponse element, float amount, int salt)
    {
        int present = PresentCells().Count;
        for (int r = 0; r < regions.Count && r < a.regions.Count; r++)
        {
            if (HasHideCells(r) || !Covered(a, r)) continue;
            int k = e * regions.Count + r;
            while (periodicCover.Count <= k) periodicCover.Add(0f);
            periodicCover[k] += amount * (1f - 0.6f * a.regions[r].armor);
            if (periodicCover[k] < 0.1f) continue;
            float x = periodicCover[k];
            periodicCover[k] = 0f;
            double times = present > 0 ? 16.0 / present : 1.0;
            for (int t = 0; times > 0.0; t++, times -= 1.0)
                if (times >= 1.0 || HarvestMath.Noise(seed, salt * 7 + r + 977 * t, 61) < times)
                    Covering(r, true).Stamp(a, r, element, x, -1f, -1f, salt + 13 * r + 101 * t);
        }
    }

    /// <summary>A cell beside a small hide, not part of it.</summary>
    private static readonly HarvestPeltCell Outside = new HarvestPeltCell();

    /// <summary>
    /// The small hide of region <paramref name="r"/> (<see cref="HarvestRegionState.cover"/>), laid out for
    /// <see cref="Stamp"/> to mark as it marks the hide: its 2 × 2 cells in the middle of 6 × 6, with room for what runs on
    /// past them. With <paramref name="around"/> the cells around them are hide too (marked, then thrown away): a mark
    /// may start there and reach it.
    /// </summary>
    private HarvestCondition Covering(int r, bool around)
    {
        HarvestRegionState st = regions[r];
        st.cover ??= new List<HarvestPeltCell>();
        while (st.cover.Count < 4) st.cover.Add(new HarvestPeltCell());
        var piece = new HarvestCondition { anatomy = anatomy, seed = seed + 7919 * (r + 1), peltWidth = 6, peltHeight = 6, regions = regions };
        for (int y = 0; y < 6; y++)
            for (int x = 0; x < 6; x++)
            {
                bool own = x >= 2 && x <= 3 && y >= 2 && y <= 3, near = around && x >= 1 && x <= 4 && y >= 1 && y <= 4;
                HarvestPeltCell c = own ? st.cover[(y - 2) * 2 + x - 2] : near ? new HarvestPeltCell() : Outside;
                if (own || near)
                {
                    c.present = true;
                    c.region = r;
                }
                piece.pelt.Add(c);
            }
        return piece;
    }

    /// <summary>Does region <paramref name="r"/> grow feathers or scales taken as the hide?</summary>
    public static bool Covered(HarvestAnatomyData a, int r)
    {
        if (a?.regions == null || r < 0 || r >= a.regions.Count)
            return false;
        string id = a.regions[r]?.id;
        if (id == null || a.materials == null)
            return false;
        foreach (HarvestMaterial m in a.materials)
        {
            if (!HarvestCarcass.IsHide(m) || m.kind == HarvestMaterialKind.Hide || m.sources == null) continue;
            foreach (HarvestSource s in m.sources)
                if (s != null && string.Equals(s.region, id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>A region that has hide (weighted by its cells), for burning that spreads.</summary>
    private int RandomHideRegion(HarvestAnatomyData a, int salt)
    {
        int present = 0;
        foreach (HarvestPeltCell c in pelt) if (c.present) present++;
        if (present == 0)
            return -1;
        int pick = (int)(HarvestMath.Noise(seed, salt, 23) * present);
        foreach (HarvestPeltCell c in pelt)
        {
            if (!c.present) continue;
            if (pick-- <= 0) return c.region;
        }
        return -1;
    }

    // ------------------------------------------------------------------ the hide
    /// <summary>
    /// Marks the hide cells of region <paramref name="r"/> with <paramref name="resp"/>'s mark at (<paramref name="u"/>,
    /// <paramref name="v"/>) of the region (random when unknown). Returns how much it holed (cells × damage).
    /// </summary>
    private float Stamp(HarvestAnatomyData a, int r, HarvestDamageResponse resp, float sev, float u, float v, int salt)
    {
        List<int> cells = CellsOf(r);
        if (cells.Count == 0)
        {
            // A region too small to hold a cell of its own (a tail on a narrow pelt): the cells nearest its place.
            cells = PresentCells();
            if (u < 0f || v < 0f) u = v = 0.5f;
        }
        if (cells.Count == 0 || sev <= 0f)
            return 0f;
        int start = StartCell(a, r, cells, u, v, salt);
        int sx = start % peltWidth, sy = start / peltWidth;
        float size = Math.Max(0f, resp.hideCells) * sev;
        float sum = 0f;
        HarvestMarks marks = resp.marks & (HarvestMarks.Cut | HarvestMarks.Pierced | HarvestMarks.Torn | HarvestMarks.Burned | HarvestMarks.Frozen |
                                           HarvestMarks.Bruised | HarvestMarks.Crushed | HarvestMarks.Tainted);
        switch (resp.hideMark)
        {
            case HarvestHideMark.Hole:
            {
                float d = 0.55f + 0.45f * Math.Min(1f, sev * 2f);
                sum += Mark(sx, sy, d, 0f, marks);
                // Through and through: the exit hole on the other side of the body - half way round the hide laid flat.
                if (sev > 0.3f)
                {
                    int exit = ExitCell(a, sx, sy);
                    if (exit >= 0 && exit != start)
                        sum += Mark(exit % peltWidth, sy, d * 0.8f, 0f, marks);
                }
                break;
            }
            case HarvestHideMark.Line:
            {
                int n = Clamp(1 + (int)Math.Round(size), 1, Math.Max(peltWidth, peltHeight));
                double angle = HarvestMath.Noise(seed, salt, 29) * Math.PI;
                sum += Line(sx, sy, angle, n, 0.3f + 0.5f * Math.Min(1f, sev), marks);
                break;
            }
            case HarvestHideMark.Scratches:
            {
                int n = Clamp(1 + (int)Math.Round(size * 0.6f), 1, Math.Max(peltWidth, peltHeight));
                double angle = HarvestMath.Noise(seed, salt, 29) * Math.PI;
                double px = -Math.Sin(angle), py = Math.Cos(angle);
                for (int k = -1; k <= 1; k++)
                    sum += Line((int)Math.Round(sx + px * k), (int)Math.Round(sy + py * k), angle, n, 0.25f + 0.4f * Math.Min(1f, sev), marks);
                break;
            }
            case HarvestHideMark.Tear:
                for (int dy = 0; dy <= 1; dy++)
                    for (int dx = 0; dx <= 1; dx++)
                        sum += Mark(sx + dx, sy + dy, 0.35f + 0.5f * Math.Min(1f, sev), 0f, marks);
                break;
            case HarvestHideMark.Bruise:
                sum += Mark(sx, sy, 0.04f + 0.12f * sev, 0f, marks);
                if (sev > 0.5f) sum += Mark(sx + 1, sy, 0.04f + 0.1f * sev, 0f, marks);
                break;
            case HarvestHideMark.Crush:
            {
                int k = sev > 0.8f ? 1 : 0;
                for (int dy = -k; dy <= 1; dy++)
                    for (int dx = -k; dx <= 1; dx++)
                        sum += Mark(sx + dx, sy + dy, 0.15f + 0.35f * Math.Min(1f, sev), 0f, marks);
                break;
            }
            case HarvestHideMark.Patch:
            {
                bool fire = (resp.marks & HarvestMarks.Burned) != 0;
                float radius = 0.5f + size * 0.5f;
                int ri = (int)Math.Ceiling(radius);
                for (int dy = -ri; dy <= ri; dy++)
                    for (int dx = -ri; dx <= ri; dx++)
                    {
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (dist > radius) continue;
                        float fall = 1f - dist / (radius + 0.5f);
                        if (fire)
                            sum += Mark(sx + dx, sy + dy, (0.2f + 0.5f * Math.Min(1f, sev)) * fall, (0.4f + 0.8f * sev) * fall, marks);
                        else
                            sum += Mark(sx + dx, sy + dy, (0.05f + 0.2f * Math.Min(1f, sev)) * fall, 0f, marks);
                    }
                break;
            }
            case HarvestHideMark.Branch:
            {
                int x = sx, y = sy;
                int n = Clamp(2 + (int)Math.Round(size), 2, peltWidth + peltHeight);
                for (int k = 0; k < n; k++)
                {
                    sum += Mark(x, y, 0.15f + 0.3f * Math.Min(1f, sev), 0.3f + 0.5f * sev, marks);
                    double turn = HarvestMath.Noise(seed, salt + k, 31);
                    if (turn < 0.33) x--; else if (turn > 0.66) x++;
                    y += HarvestMath.Noise(seed, salt + k, 37) < 0.5 ? 1 : -1;
                    if (HarvestMath.Noise(seed, salt + k, 39) < 0.2)
                        sum += Mark(x + 1, y, 0.1f + 0.2f * sev, 0.2f + 0.4f * sev, marks);
                }
                break;
            }
        }
        return sum;
    }

    /// <summary>
    /// Where a hit that went through leaves the body: half way round from (<paramref name="sx"/>, <paramref name="sy"/>).
    /// The hide is laid flat around the body, so that is half way along the run of cells of that row it went through -
    /// the body's (a limb's flaps at the row's ends are not the far side of the chest), or the limb's own when it struck a
    /// limb. -1 when the run is a single cell.
    /// </summary>
    private int ExitCell(HarvestAnatomyData a, int sx, int sy)
    {
        int Kind(int x)
        {
            int k = sy * peltWidth + x;
            if (x < 0 || x >= peltWidth || k >= pelt.Count || !pelt[k].present || pelt[k].region < 0) return int.MinValue;
            int r = pelt[k].region;
            // The body's own parts are one run; each limb (a side part) is its own.
            return r < a.regions.Count && a.regions[r] != null && a.regions[r].side == BodySide.Any ? -1 : r;
        }
        int kind = Kind(sx);
        if (kind == int.MinValue)
            return -1;
        int x0 = sx, x1 = sx;
        while (Kind(x0 - 1) == kind) x0--;
        while (Kind(x1 + 1) == kind) x1++;
        int len = x1 - x0 + 1;
        if (len <= 1)
            return -1;
        return sy * peltWidth + x0 + (sx - x0 + len / 2) % len;
    }

    /// <summary>
    /// A gut, bladder or gall (<paramref name="torn"/>) torn open soils its region: the meat there and the organs beside it
    /// (marked <paramref name="mark"/>: GutShot in the fight, Contaminated while harvesting).
    /// </summary>
    public void SoilAround(HarvestAnatomyData a, HarvestUnit torn, HarvestMarks mark)
    {
        int r = torn != null ? torn.region : -1;
        if (r < 0 || r >= regions.Count)
            return;
        regions[r].contamination += 0.6f;
        regions[r].marks |= mark;
        foreach (HarvestUnit o in units)
            if (o.region == r && o != torn && !o.taken && a.materials[o.material].method == HarvestMethod.Organs)
            {
                o.marks |= mark;
                o.integrity = Math.Max(0f, o.integrity - 0.1f);
            }
    }

    /// <summary>The cell a hit starts from: where it landed on the region when known, else a repeatable random one of its cells.</summary>
    private int StartCell(HarvestAnatomyData a, int r, List<int> cells, float u, float v, int salt)
    {
        if (u >= 0f && v >= 0f && a.regions[r] != null)
        {
            UnityEngine.Rect p = a.regions[r].pelt;
            float tx = (p.x + Math.Min(1f, u) * p.width) * peltWidth, ty = (p.y + Math.Min(1f, v) * p.height) * peltHeight;
            int best = cells[0];
            float bestD = float.MaxValue;
            foreach (int k in cells)
            {
                float dx = k % peltWidth + 0.5f - tx, dy = k / peltWidth + 0.5f - ty;
                float d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = k; }
            }
            return best;
        }
        return cells[Math.Min(cells.Count - 1, (int)(HarvestMath.Noise(seed, salt, 47) * cells.Count))];
    }

    private float Line(int sx, int sy, double angle, int n, float damage, HarvestMarks marks)
    {
        double dx = Math.Cos(angle), dy = Math.Sin(angle);
        double x = sx - dx * (n - 1) * 0.5, y = sy - dy * (n - 1) * 0.5;
        float sum = 0f;
        int lastX = int.MinValue, lastY = int.MinValue;
        for (int k = 0; k < n * 2; k++)
        {
            int cx = (int)Math.Round(x + dx * k * 0.5), cy = (int)Math.Round(y + dy * k * 0.5);
            if (cx == lastX && cy == lastY) continue;
            lastX = cx;
            lastY = cy;
            sum += Mark(cx, cy, damage, 0f, marks);
        }
        return sum;
    }

    /// <summary>Damages one cell (if it is part of the hide). Returns the damage added.</summary>
    private float Mark(int x, int y, float damage, float burn, HarvestMarks marks)
    {
        HarvestPeltCell c = Cell(x, y);
        if (c == null || !c.present || damage <= 0f && burn <= 0f)
            return 0f;
        float before = c.damage;
        c.damage = Math.Min(1f, c.damage + Math.Max(0f, damage));
        c.burn += Math.Max(0f, burn);
        if (c.burn >= 1f) c.damage = Math.Max(c.damage, 0.9f);
        c.marks |= marks;
        return c.damage - before;
    }

    private static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;

    // ------------------------------------------------------------------ wounds
    private void NoteWound(HarvestAnatomyData a, HarvestSettings s, int r, HarvestHit hit, float sev, string effect, bool periodic)
    {
        if (periodic)
        {
            // Ticks of the same burning or poison add up in one wound.
            for (int i = wounds.Count - 1; i >= 0 && i >= wounds.Count - 4; i--)
            {
                HarvestWound w = wounds[i];
                if (w.periodic && w.element == hit.element)
                {
                    w.severity += sev;
                    w.time = hit.time;
                    return;
                }
            }
        }
        wounds.Add(new HarvestWound
        {
            region = r, form = hit.form, element = hit.element, severity = sev, critical = hit.critical, area = hit.area, periodic = periodic,
            source = hit.source ?? "", time = hit.time, effect = effect ?? "",
        });
        int max = Math.Max(4, s.maxWounds);
        while (wounds.Count > max)
        {
            // Forget the slightest of the older half.
            int drop = 0;
            for (int i = 1; i < wounds.Count / 2; i++)
                if (wounds[i].severity < wounds[drop].severity) drop = i;
            wounds.RemoveAt(drop);
        }
    }

    private static string Verb(HarvestHit hit, bool ruined)
    {
        if (hit.element == ElementType.Fire || hit.element == ElementType.Lightning) return ruined ? "cooked" : "scorched";
        if (hit.element == ElementType.Poison) return "poisoned";
        switch (hit.form)
        {
            case DamageForm.Slash: return ruined ? "split" : "cut";
            case DamageForm.Pierce: return "pierced";
            case DamageForm.Blunt: return ruined ? "burst" : "bruised";
            case DamageForm.Crush: return "crushed";
            case DamageForm.Bite: return "tore";
            case DamageForm.Claw: return "raked";
            default: return ruined ? "ruined" : "hurt";
        }
    }

    /// <summary>"heart", "left femur", "claw" (its material's name, with the side when it has several sources).</summary>
    public static string UnitName(HarvestAnatomyData a, HarvestUnit u, bool withSide = true)
    {
        HarvestMaterial m = a.materials[u.material];
        string name = m.Name.ToLowerInvariant();
        if (!withSide || m.TotalUnits <= 1 || u.region < 0 || u.region >= a.regions.Count)
            return Singular(name);
        string rid = a.regions[u.region].id;
        string side = rid.EndsWith("_l", StringComparison.OrdinalIgnoreCase) ? "left " : rid.EndsWith("_r", StringComparison.OrdinalIgnoreCase) ? "right " : "";
        return side + Singular(name);
    }

    private static string Singular(string name)
    {
        if (name.EndsWith("teeth", StringComparison.Ordinal)) return name.Substring(0, name.Length - 5) + "tooth";
        if (name.EndsWith("ies", StringComparison.Ordinal)) return name.Substring(0, name.Length - 3) + "y";
        if (name.EndsWith("s", StringComparison.Ordinal) && !name.EndsWith("ss", StringComparison.Ordinal) && !name.EndsWith("us", StringComparison.Ordinal) && !name.EndsWith("lungs", StringComparison.Ordinal))
            return name.Substring(0, name.Length - 1);
        return name;
    }

    // ------------------------------------------------------------------ while alive
    /// <summary>
    /// A regenerating creature's wounds close (<see cref="HarvestAnatomyData.regeneration"/> a minute): tissues, pieces and
    /// hide heal - except where they burned.
    /// </summary>
    public void Regenerate(HarvestAnatomyData a, double minutes)
    {
        if (a == null || a.regeneration <= 0f || minutes <= 0)
            return;
        float heal = (float)(a.regeneration * minutes);
        const HarvestMarks Healed = HarvestMarks.Cut | HarvestMarks.Pierced | HarvestMarks.Torn | HarvestMarks.Bruised | HarvestMarks.Crushed |
                                    HarvestMarks.Fractured;
        for (int r = 0; r < regions.Count; r++)
        {
            HarvestRegionState st = regions[r];
            float h = heal * (st.burn > 0.3f ? 0f : 1f);
            st.meat = Math.Min(1f, st.meat + h);
            st.bone = Math.Min(1f, st.bone + h * 0.5f);
            st.tendon = Math.Min(1f, st.tendon + h);
            st.organs = Math.Min(1f, st.organs + h);
            st.contamination = Math.Max(0f, st.contamination - h);
            if (st.contamination <= 0.12f) st.marks &= ~HarvestMarks.GutShot;
            if (h > 0f && st.meat >= 0.999f && st.bone >= 0.999f && st.tendon >= 0.999f && st.organs >= 0.999f)
                st.marks &= ~Healed;
            // The small hide of a part with no cells of its own heals as the hide does.
            if (st.cover != null)
                foreach (HarvestPeltCell c in st.cover)
                    Mend(c, heal, Healed);
        }
        foreach (HarvestUnit u in units)
            if ((u.marks & HarvestMarks.Burned) == 0 && !u.taken)
            {
                u.integrity = Math.Min(1f, u.integrity + heal * 0.8f);
                if (u.integrity >= 0.99f)
                {
                    // Healed whole: the wound leaves no mark (a mended bone is no longer fractured).
                    u.marks &= ~Healed;
                    if (u.region >= 0 && u.region < regions.Count && regions[u.region].contamination <= 0.12f) u.marks &= ~HarvestMarks.GutShot;
                }
            }
        foreach (HarvestPeltCell c in pelt)
            Mend(c, heal, Healed);
    }

    /// <summary>A hide cell heals by <paramref name="heal"/> unless burned; healed whole, its wounds leave no mark.</summary>
    private static void Mend(HarvestPeltCell c, float heal, HarvestMarks healed)
    {
        if (c == null || !c.present || c.burn >= 0.3f)
            return;
        c.damage = Math.Max(0f, c.damage - heal);
        if (c.damage <= 0f) c.marks &= ~healed;
    }

    // ------------------------------------------------------------------ copies
    public HarvestCondition Clone()
    {
        var c = (HarvestCondition)MemberwiseClone();
        c.regions = new List<HarvestRegionState>();
        foreach (HarvestRegionState r in regions) c.regions.Add(r.Clone());
        c.units = new List<HarvestUnit>();
        foreach (HarvestUnit u in units) c.units.Add(u.Clone());
        c.missing = new List<int>(missing);
        c.pelt = new List<HarvestPeltCell>();
        foreach (HarvestPeltCell p in pelt) c.pelt.Add(p.Clone());
        c.wounds = new List<HarvestWound>();
        foreach (HarvestWound w in wounds) c.wounds.Add(w.Clone());
        c.periodicHide = new List<float>(periodicHide ?? new List<float>());
        c.periodicCover = new List<float>(periodicCover ?? new List<float>());
        c.regionCells = null;
        c.presentCells = null;
        return c;
    }
}
