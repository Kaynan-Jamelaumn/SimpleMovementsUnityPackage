using System;
using System.Collections.Generic;

/// <summary>Why a material can (or cannot) be taken from a carcass right now.</summary>
public enum HarvestAvailability
{
    Available,
    /// <summary>Already taken (all of it).</summary>
    Taken,
    /// <summary>Destroyed in the fight.</summary>
    Destroyed,
    /// <summary>This creature does not have it (a rare find that is not there).</summary>
    Missing,
    /// <summary>There, but the player cannot tell (not enough Anatomy).</summary>
    Hidden,
    /// <summary>The hide must come off first.</summary>
    NeedsHide,
    /// <summary>Something else must be taken first.</summary>
    Blocked,
    /// <summary>The player's skill is too low to try.</summary>
    NeedsSkill,
    /// <summary>Someone else is working on it.</summary>
    Busy,
    /// <summary>It has spoiled.</summary>
    Spoiled,
}

/// <summary>A bulk material's share taken (or wasted).</summary>
[Serializable]
public class HarvestTaken
{
    public int material;
    public float share;
}

/// <summary>A material someone is working on (one player at a time per material).</summary>
[Serializable]
public class HarvestLock
{
    public int material;
    public int player;
    /// <summary>Carcass age (seconds) the lock lapses at.</summary>
    public double until;
}

/// <summary>
/// A dead creature being harvested: its condition from the fight, how long it has lain (and how far it has spoiled -
/// warmth hastens it, cold slows it, frost stops it, gutting slows the meat, a torn gut hastens it), what is already off
/// it and who is working on what. Plain data: the server keeps it, every client sees a copy.
/// </summary>
[Serializable]
public class HarvestCarcass
{
    public HarvestCondition condition = new HarvestCondition();
    /// <summary>Seconds since death.</summary>
    public double age;
    /// <summary>Spoiling of organs and tendons: minutes at 20 °C since death.</summary>
    public double clock;
    /// <summary>Spoiling of meat, fat and hide (gutting slows it, a torn gut hastens it).</summary>
    public double meatClock;
    /// <summary>Temperature the carcass lies in (°C), last measured.</summary>
    public float temperature = 18f;
    public bool hideOff;
    public bool cavityOpen;
    public bool gutted;
    public List<HarvestTaken> taken = new List<HarvestTaken>();
    public List<HarvestLock> locks = new List<HarvestLock>();
    public int tasks;
    /// <summary>Age of the last work done on it (-1 = none).</summary>
    public double lastWork = -1;

    /// <summary>The carcass of a creature that died in <paramref name="c"/>'s condition.</summary>
    public static HarvestCarcass From(HarvestAnatomyData a, HarvestCondition c)
    {
        var k = new HarvestCarcass { condition = c ?? new HarvestCondition() };
        k.hideOff = !HasHide(a);
        return k;
    }

    /// <summary>Does the creature have a hide (pelt, skin, feathers) that comes off before the meat?</summary>
    public static bool HasHide(HarvestAnatomyData a)
    {
        if (a?.materials == null)
            return false;
        foreach (HarvestMaterial m in a.materials)
            if (IsHide(m)) return true;
        return false;
    }

    /// <summary>Is <paramref name="m"/> the creature's hide (the one skinning takes off whole)?</summary>
    public static bool IsHide(HarvestMaterial m) =>
        m != null && m.method == HarvestMethod.Skinning && !m.countable &&
        (m.kind == HarvestMaterialKind.Hide || m.kind == HarvestMaterialKind.Feathers || m.kind == HarvestMaterialKind.Scales);

    /// <summary>
    /// Does taking <paramref name="m"/> take the hide off (and free what is under it)? A pelt or skin does; feathers or
    /// scales do only on a creature that has no pelt besides them (a bird, a fish) - plucking a griffon leaves its pelt on.
    /// </summary>
    public static bool TakesHideOff(HarvestAnatomyData a, HarvestMaterial m)
    {
        if (!IsHide(m))
            return false;
        if (m.kind == HarvestMaterialKind.Hide || a?.materials == null)
            return true;
        foreach (HarvestMaterial x in a.materials)
            if (IsHide(x) && x.kind == HarvestMaterialKind.Hide) return false;
        return true;
    }

    /// <summary>
    /// Someone other than <paramref name="player"/> working on the creature's hide - its pelt, or its feathers or scales -
    /// or 0. Nobody hacks a hide off under another player's hands.
    /// </summary>
    public int HideWorkedBy(HarvestAnatomyData a, HarvestSettings s, int player)
    {
        if (a?.materials == null)
            return 0;
        for (int i = 0; i < a.materials.Count; i++)
        {
            if (!IsHide(a.materials[i]) || Gone(a, s, i)) continue;
            int by = LockedBy(i);
            if (by != 0 && by != player) return by;
        }
        return 0;
    }

    /// <summary>The creature's hide still on it (its pelt or skin first; else its feathers or scales), or -1.</summary>
    public int HideIndex(HarvestAnatomyData a, HarvestSettings s)
    {
        int other = -1;
        if (a?.materials == null)
            return -1;
        for (int i = 0; i < a.materials.Count; i++)
        {
            HarvestMaterial m = a.materials[i];
            if (!TakesHideOff(a, m) || Gone(a, s, i)) continue;
            if (m.kind == HarvestMaterialKind.Hide) return i;
            if (other < 0) other = i;
        }
        return other;
    }

    // ------------------------------------------------------------------ spoiling
    /// <summary>How fast things spoil at <paramref name="temperature"/> against 20 °C (doubling every Doubling Degrees; 0 at or below freezing).</summary>
    public static double SpoilRate(HarvestSettings s, float temperature)
    {
        if (temperature <= s.freezingPoint)
            return 0;
        return Math.Pow(2, (temperature - 20.0) / Math.Max(1.0, s.doublingDegrees));
    }

    /// <summary>Time passes: <paramref name="seconds"/> at <paramref name="ambient"/> °C.</summary>
    public void Tick(HarvestSettings s, double seconds, float ambient)
    {
        if (seconds <= 0)
            return;
        temperature = ambient;
        age += seconds;
        double minutes = seconds / 60.0 * SpoilRate(s, ambient);
        clock += minutes;
        meatClock += minutes * (gutted ? s.guttedSpoilFactor : 1f) * (Soiled ? s.soiledSpoilFactor : 1f);
        // Locks lapse.
        for (int i = locks.Count - 1; i >= 0; i--)
            if (locks[i].until <= age) locks.RemoveAt(i);
    }

    /// <summary>Is the body soiled by a torn gut (it spoils faster)?</summary>
    public bool Soiled
    {
        get
        {
            foreach (HarvestRegionState r in condition.regions)
                if (r.contamination > 0.3f) return true;
            return false;
        }
    }

    private bool UsesMeatClock(HarvestMaterial m) =>
        m.kind == HarvestMaterialKind.Meat || m.kind == HarvestMaterialKind.Fat || m.kind == HarvestMaterialKind.Hide || m.kind == HarvestMaterialKind.Feathers;

    /// <summary>How fresh <paramref name="m"/> still is (1 fresh .. 0 spoiled; always 1 for what never spoils).</summary>
    public double Freshness(HarvestMaterial m)
    {
        if (m == null || m.spoilMinutes <= 0f)
            return 1;
        double t = UsesMeatClock(m) ? meatClock : clock;
        return HarvestMath.Clamp01(1 - t / m.spoilMinutes);
    }

    /// <summary>Real minutes until <paramref name="m"/> starts to turn at the current temperature (∞ when it never does or it is frozen; 0 when it already has).</summary>
    public double MinutesUntilTurning(HarvestSettings s, HarvestMaterial m)
    {
        if (m == null || m.spoilMinutes <= 0f)
            return double.PositiveInfinity;
        double rate = SpoilRate(s, temperature) * (UsesMeatClock(m) ? (gutted ? s.guttedSpoilFactor : 1f) * (Soiled ? s.soiledSpoilFactor : 1f) : 1f);
        double left = (Freshness(m) - s.spoilingBelow) * m.spoilMinutes;
        if (left <= 0)
            return 0;
        return rate <= 1e-6 ? double.PositiveInfinity : left / rate;
    }

    /// <summary>The time marks of <paramref name="m"/> now (spoiling, spoiled).</summary>
    public HarvestMarks TimeMarks(HarvestSettings s, HarvestMaterial m)
    {
        double f = Freshness(m);
        if (m == null || m.spoilMinutes <= 0f) return HarvestMarks.None;
        if (f <= 0.05) return HarvestMarks.Spoiled;
        return f < s.spoilingBelow ? HarvestMarks.Spoiling : HarvestMarks.None;
    }

    // ------------------------------------------------------------------ what is left
    /// <summary>The share of a bulk material taken (or wasted) so far.</summary>
    public float TakenShare(int material)
    {
        foreach (HarvestTaken t in taken)
            if (t.material == material) return t.share;
        return 0f;
    }

    /// <summary>Takes (or wastes) <paramref name="share"/> of a bulk material.</summary>
    public void TakeShare(int material, float share)
    {
        foreach (HarvestTaken t in taken)
            if (t.material == material)
            {
                t.share = HarvestMathF.Clamp01(t.share + share);
                return;
            }
        taken.Add(new HarvestTaken { material = material, share = HarvestMathF.Clamp01(share) });
    }

    /// <summary>Pieces of a countable material still on the carcass and not destroyed.</summary>
    public int UnitsLeft(HarvestSettings s, int material)
    {
        int n = 0;
        foreach (HarvestUnit u in condition.units)
            if (u.material == material && !u.taken && u.integrity >= s.destroyedBelow) n++;
        return n;
    }

    /// <summary>
    /// The condition of a material from the fight (0-1): its pieces' (mean of those left), or for bulk materials the
    /// condition of where it comes from (the hide's cells, the regions' meat).
    /// </summary>
    public double CombatIntegrity(HarvestAnatomyData a, int material)
    {
        HarvestMaterial m = a.materials[material];
        if (m.countable)
        {
            double sum = 0;
            int n = 0;
            foreach (HarvestUnit u in condition.units)
                if (u.material == material && !u.taken) { sum += u.integrity; n++; }
            return n > 0 ? sum / n : 0;
        }
        if (IsHide(m))
            return HideCondition(a, m);
        return RegionIntegrity(a, m, HarvestTissue.Meat);
    }

    /// <summary>
    /// The condition from the fight of a hide material: a pelt's or skin's over all its cells; feathers' or scales' over
    /// the parts they grow on, by their shares, each part as <see cref="RegionHideCondition"/> judges it.
    /// </summary>
    public double HideCondition(HarvestAnatomyData a, HarvestMaterial m)
    {
        if (m.kind == HarvestMaterialKind.Hide || m.sources == null)
            return condition.HideIntegrity();
        double sum = 0, w = 0;
        foreach (HarvestSource s in m.sources)
        {
            int r = a.RegionIndex(s.region);
            if (r < 0 || r >= condition.regions.Count) continue;
            double share = Math.Max(0.0001f, s.share);
            sum += RegionHideCondition(r) * share;
            w += share;
        }
        return w > 0 ? sum / w : 1;
    }

    /// <summary>
    /// The condition from the fight of what covers one region: its hide cells'; or with none of its own (a griffon's
    /// wing, which only grows feathers), its covering's, marked as a small part of a hide would be - an eagle's wing, which
    /// has cells, fares much the same under the same blows and fire.
    /// </summary>
    public double RegionHideCondition(int region)
    {
        if (condition.HasHideCells(region))
            return condition.HideIntegrity(region);
        HarvestRegionState st = region >= 0 && region < condition.regions.Count ? condition.regions[region] : null;
        return st == null ? 1 : st.CoverCondition();
    }

    /// <summary>A tissue's condition over a material's sources, weighted by their shares.</summary>
    public double RegionIntegrity(HarvestAnatomyData a, HarvestMaterial m, HarvestTissue t)
    {
        double sum = 0, w = 0;
        if (m.sources != null)
            foreach (HarvestSource s in m.sources)
            {
                int r = a.RegionIndex(s.region);
                if (r < 0 || r >= condition.regions.Count) continue;
                double share = Math.Max(0.0001f, s.share);
                sum += condition.regions[r].Get(t) * share;
                w += share;
            }
        return w > 0 ? sum / w : 1;
    }

    /// <summary>The marks of the fight on a material: its pieces', the hide cells', or its regions' (only those that matter to it).</summary>
    public HarvestMarks CombatMarks(HarvestAnatomyData a, int material)
    {
        HarvestMaterial m = a.materials[material];
        HarvestMarks marks = HarvestMarks.None;
        if (m.countable)
        {
            foreach (HarvestUnit u in condition.units)
                if (u.material == material && !u.taken) marks |= u.marks;
        }
        else if (IsHide(m) && (m.kind == HarvestMaterialKind.Hide || m.sources == null))
        {
            marks = condition.HideMarks();
        }
        else if (IsHide(m))
        {
            // Feathers or scales: the hide cells of the parts they grow on; a part with none of its own, its own marks.
            foreach (HarvestSource s in m.sources)
            {
                int r = a.RegionIndex(s.region);
                if (r < 0 || r >= condition.regions.Count) continue;
                marks |= condition.HasHideCells(r) ? condition.HideMarks(r) : condition.regions[r].CoverMarks();
            }
        }
        else if (m.sources != null)
        {
            foreach (HarvestSource s in m.sources)
            {
                int r = a.RegionIndex(s.region);
                if (r >= 0 && r < condition.regions.Count) marks |= condition.regions[r].marks;
            }
        }
        return marks & Relevant(m.kind);
    }

    /// <summary>The marks that matter to a kind of material (a pierced region does not make its meat "pierced").</summary>
    public static HarvestMarks Relevant(HarvestMaterialKind kind)
    {
        switch (kind)
        {
            case HarvestMaterialKind.Meat:
            case HarvestMaterialKind.Fat:
                return HarvestMarks.Burned | HarvestMarks.Frozen | HarvestMarks.Poisoned | HarvestMarks.Tainted | HarvestMarks.GutShot |
                       HarvestMarks.Blasted | HarvestMarks.Crushed | HarvestMarks.Bruised | HarvestMarks.Contaminated;
            case HarvestMaterialKind.Hide:
            case HarvestMaterialKind.Feathers:
            case HarvestMaterialKind.Scales:
                return HarvestMarks.Cut | HarvestMarks.Pierced | HarvestMarks.Torn | HarvestMarks.Burned | HarvestMarks.Frozen | HarvestMarks.Crushed |
                       HarvestMarks.Blasted | HarvestMarks.Tainted;
            case HarvestMaterialKind.Bone:
                return HarvestMarks.Fractured | HarvestMarks.Crushed | HarvestMarks.Burned | HarvestMarks.Blasted | HarvestMarks.Cut | HarvestMarks.Pierced;
            case HarvestMaterialKind.Tendon:
                return HarvestMarks.Cut | HarvestMarks.Torn | HarvestMarks.Burned | HarvestMarks.Blasted;
            case HarvestMaterialKind.Organ:
            case HarvestMaterialKind.Gland:
            case HarvestMaterialKind.Essence:
                return HarvestMarks.Pierced | HarvestMarks.Cut | HarvestMarks.Crushed | HarvestMarks.Burned | HarvestMarks.Poisoned | HarvestMarks.Tainted |
                       HarvestMarks.GutShot | HarvestMarks.Blasted | HarvestMarks.Bruised | HarvestMarks.Torn | HarvestMarks.Contaminated;
            default:
                return HarvestMarks.Fractured | HarvestMarks.Cut | HarvestMarks.Burned | HarvestMarks.Frozen | HarvestMarks.Crushed | HarvestMarks.Torn | HarvestMarks.Blasted;
        }
    }

    /// <summary>Can nothing more of <paramref name="material"/> be had (gone, or spoiled)?</summary>
    public bool Unobtainable(HarvestAnatomyData a, HarvestSettings s, int material)
    {
        if (Gone(a, s, material))
            return true;
        HarvestMaterial m = a.materials[material];
        return m != null && m.spoilMinutes > 0f && Freshness(m) <= 0.01;
    }

    /// <summary>Is everything of <paramref name="material"/> gone (taken, wasted or destroyed)?</summary>
    public bool Gone(HarvestAnatomyData a, HarvestSettings s, int material)
    {
        HarvestMaterial m = a.materials[material];
        if (!condition.Has(material))
            return true;
        if (m.countable)
            return UnitsLeft(s, material) == 0;
        return TakenShare(material) >= 0.999f || CombatIntegrity(a, material) < s.destroyedBelow;
    }

    /// <summary>
    /// Is a requirement of <paramref name="forMaterial"/> met: a material taken (or no longer to be had: spoiled,
    /// destroyed, absent), the hide off, the body opened (organs open it as part of their own work; with no organ left to
    /// take, there is nothing to open it for), gutted (or no guts left to take)?
    /// </summary>
    public bool Meets(HarvestAnatomyData a, HarvestSettings s, string token, HarvestMaterial forMaterial = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;
        if (string.Equals(token, HarvestMethods.Hide, StringComparison.OrdinalIgnoreCase)) return hideOff;
        if (string.Equals(token, HarvestMethods.Cavity, StringComparison.OrdinalIgnoreCase))
            return cavityOpen || forMaterial == null || forMaterial.method == HarvestMethod.Organs || !OrgansLeft(a, s);
        if (string.Equals(token, HarvestMethods.Gutted, StringComparison.OrdinalIgnoreCase)) return gutted || !GutsLeft(a, s);
        int i = a.MaterialIndex(token);
        return i < 0 || Unobtainable(a, s, i);
    }

    /// <summary>Is there an organ still to be taken (whose work opens the body)?</summary>
    private bool OrgansLeft(HarvestAnatomyData a, HarvestSettings s)
    {
        for (int i = 0; i < a.materials.Count; i++)
            if (a.materials[i] != null && a.materials[i].method == HarvestMethod.Organs && !Unobtainable(a, s, i))
                return true;
        return false;
    }

    /// <summary>Are there guts (a stomach, intestines) still to be taken out?</summary>
    private bool GutsLeft(HarvestAnatomyData a, HarvestSettings s)
    {
        for (int i = 0; i < a.materials.Count; i++)
        {
            HarvestMaterial m = a.materials[i];
            if (m != null && m.method == HarvestMethod.Organs && m.hazard && (m.Shape == "stomach" || m.Shape == "intestines") && !Unobtainable(a, s, i))
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ who works on what
    /// <summary>Who is working on <paramref name="material"/> (0 = nobody).</summary>
    public int LockedBy(int material)
    {
        foreach (HarvestLock l in locks)
            if (l.material == material && l.until > age) return l.player;
        return 0;
    }

    /// <summary>Starts (or keeps) <paramref name="player"/> working on a material for <paramref name="seconds"/>. False when someone else is.</summary>
    public bool Lock(int material, int player, double seconds)
    {
        foreach (HarvestLock l in locks)
        {
            if (l.material != material || l.until <= age) continue;
            if (l.player != player) return false;
            l.until = age + seconds;
            return true;
        }
        locks.Add(new HarvestLock { material = material, player = player, until = age + seconds });
        return true;
    }

    public void Unlock(int material, int player)
    {
        for (int i = locks.Count - 1; i >= 0; i--)
            if (locks[i].material == material && (player == 0 || locks[i].player == player)) locks.RemoveAt(i);
    }

    // ------------------------------------------------------------------ availability
    /// <summary>
    /// Can a player with <paramref name="skills"/> take <paramref name="material"/> now? <paramref name="reason"/> says
    /// why not, in words.
    /// </summary>
    public HarvestAvailability Availability(HarvestAnatomyData a, HarvestSettings s, int material, HarvestSkillData skills, int player, out string reason)
    {
        reason = "";
        if (a?.materials == null || material < 0 || material >= a.materials.Count || a.materials[material] == null)
        {
            reason = "Unknown material.";
            return HarvestAvailability.Missing;
        }
        HarvestMaterial m = a.materials[material];
        if (!condition.Has(material))
            return HarvestAvailability.Missing;
        if (!HarvestSkills.Notices(s, skills, a.id, m))
        {
            // A find this player cannot tell is there: once it is gone (taken by someone else, ruined, spoiled) there is nothing to sense.
            if (Unobtainable(a, s, material))
                return HarvestAvailability.Missing;
            int need = HarvestSkills.Knows(s, skills, HarvestSkills.KeenEye) ? (int)Math.Ceiling(m.revealSkill * 0.67) : m.revealSkill;
            reason = $"Something unusual is here (Anatomy {need} to tell what).";
            return HarvestAvailability.Hidden;
        }
        if (m.countable)
        {
            bool any = false, intact = false;
            foreach (HarvestUnit u in condition.units)
            {
                if (u.material != material || u.taken) continue;
                any = true;
                if (u.integrity >= s.destroyedBelow) intact = true;
            }
            if (!any)
            {
                reason = "Taken.";
                return HarvestAvailability.Taken;
            }
            if (!intact)
            {
                reason = DestroyedReason(a, material);
                return HarvestAvailability.Destroyed;
            }
        }
        else
        {
            if (TakenShare(material) >= 0.999f)
            {
                reason = "Taken.";
                return HarvestAvailability.Taken;
            }
            if (CombatIntegrity(a, material) < s.destroyedBelow)
            {
                reason = DestroyedReason(a, material);
                return HarvestAvailability.Destroyed;
            }
        }
        if (m.spoilMinutes > 0f && Freshness(m) <= 0.01)
        {
            reason = "It has spoiled.";
            return HarvestAvailability.Spoiled;
        }
        if (m.requires != null)
            foreach (string q in m.requires)
            {
                if (Meets(a, s, q, m)) continue;
                if (string.Equals(q, HarvestMethods.Hide, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "The hide must come off first (skin it, or strip it roughly).";
                    return HarvestAvailability.NeedsHide;
                }
                if (string.Equals(q, HarvestMethods.Gutted, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Gut it first.";
                    return HarvestAvailability.Blocked;
                }
                if (string.Equals(q, HarvestMethods.Cavity, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Open the body first (take an organ).";
                    return HarvestAvailability.Blocked;
                }
                HarvestMaterial before = a.Material(q);
                // A find the player cannot tell is there is not named.
                reason = before != null && !HarvestSkills.Notices(s, skills, a.id, before) ? "Something else must come out first." :
                    $"Take the {(before != null ? before.Name.ToLowerInvariant() : q)} first.";
                return HarvestAvailability.Blocked;
            }
        int level = HarvestSkills.Level(s, skills, m.Skill);
        if (level < m.minSkill)
        {
            reason = $"Needs {HarvestText.SkillName(m.Skill)} {m.minSkill} (you have {level}).";
            return HarvestAvailability.NeedsSkill;
        }
        int by = LockedBy(material);
        if (by != 0 && by != player)
        {
            reason = "Someone is working on it.";
            return HarvestAvailability.Busy;
        }
        return HarvestAvailability.Available;
    }

    /// <summary>"Destroyed in the fight: burned, pierced."</summary>
    public string DestroyedReason(HarvestAnatomyData a, int material)
    {
        string marks = HarvestMarksInfo.Describe(CombatMarks(a, material), HarvestMarksInfo.Combat);
        return marks.Length > 0 ? $"Destroyed in the fight ({marks})." : "Destroyed in the fight.";
    }

    /// <summary>Is nothing worth taking left (everything taken, wasted, destroyed, missing or spoiled)?</summary>
    public bool IsEmpty(HarvestAnatomyData a, HarvestSettings s)
    {
        if (a?.materials == null)
            return true;
        for (int i = 0; i < a.materials.Count; i++)
        {
            HarvestMaterial m = a.materials[i];
            if (m == null || Unobtainable(a, s, i)) continue;
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ copies
    public HarvestCarcass Clone()
    {
        var c = (HarvestCarcass)MemberwiseClone();
        c.condition = condition.Clone();
        c.taken = new List<HarvestTaken>();
        foreach (HarvestTaken t in taken) c.taken.Add(new HarvestTaken { material = t.material, share = t.share });
        c.locks = new List<HarvestLock>();
        foreach (HarvestLock l in locks) c.locks.Add(new HarvestLock { material = l.material, player = l.player, until = l.until });
        return c;
    }
}
