using System;
using System.Collections.Generic;
using UnityEngine;

// ==================================================================== enums
/// <summary>What one animal is to another (saved as numbers: add at the END).</summary>
public enum AnimalBondKind
{
    /// <summary>A pen-mate it has grown used to.</summary>
    Companion,
    /// <summary>Its mother, its young, its litter-mates.</summary>
    Kin,
    /// <summary>The partner it was bred with.</summary>
    Mate,
    /// <summary>Another adult male of its kind it shares a pen with (a species whose males are rivals).</summary>
    Rival,
}

/// <summary>What a bond with its owner unlocks (an affinity perk: see <see cref="AnimalAffinityPerk"/>). Saved as numbers: add at the END.</summary>
public enum AnimalPerkKind
{
    /// <summary>It follows its owner along the fence.</summary>
    Follow,
    /// <summary>It comes to greet its owner (a hop, a nuzzle, a call).</summary>
    Greet,
    /// <summary>Its owner's handling (care, milking, shearing) no longer stresses it.</summary>
    CalmHandling,
    /// <summary>It comes to the fence when its owner calls it.</summary>
    ComeWhenCalled,
    /// <summary>It wears a ribbon of its owner's colours (a cosmetic mark of a deep bond).</summary>
    Ribbon,
}

// ==================================================================== data
/// <summary>A bond between two animals, as one of them holds it (each keeps its own).</summary>
[Serializable]
public class AnimalBond
{
    public string other = "";
    [Tooltip("The other's name when last together (it may be gone since).")]
    public string name = "";
    public AnimalBondKind kind;
    [Tooltip("How strong it is (0-100); for a rival, how fierce.")]
    [Range(0f, 100f)] public float level;
    public double since, lastTogether = -1.0;
    [Tooltip("They are apart, and the parting was told of.")]
    public bool apart;
}

/// <summary>How a species behaves (its bodies show it; the rules use only its company): configurable per species (FR-12).</summary>
[Serializable]
public class AnimalBehaviourSpec
{
    [Tooltip("How much it seeks the company of its own kind: it walks over to a pen-mate and they nuzzle or peck (0-1).")]
    [Range(0f, 1f)] public float sociability = 0.5f;
    [Tooltip("How often it grooms itself when idle (preens, licks, dust-bathes) (0-1).")]
    [Range(0f, 1f)] public float grooming = 0.2f;
    [Tooltip("How readily it shies from strangers at the fence (x 1 - its temperament) (0-1).")]
    [Range(0f, 1f)] public float flightiness = 0.4f;
    [Tooltip("How readily it comes to look at people at the fence (0-1).")]
    [Range(0f, 1f)] public float curiosity = 0.3f;
    [Tooltip("It keeps together with its herd (it wanders toward the others).")]
    public bool herds;
    [Tooltip("Its young stay close to their mother while they are young.")]
    public bool youngFollowMother = true;
    [Tooltip("Metres from the fence within which it notices people.")]
    [Range(1f, 30f)] public float noticeRange = 10f;
}

/// <summary>Something a bond with its owner unlocks for a species (FR-13: friendlier animals, easier handling, special behaviours, cosmetic marks).</summary>
[Serializable]
public class AnimalAffinityPerk
{
    public string id = "";
    public string name = "";
    [TextArea(1, 3)] public string description = "";
    public AnimalPerkKind kind;
    [Tooltip("The bond with its owner (affinity 0-100) from which it has it.")]
    [Range(0f, 100f)] public float minAffinity = 50f;
}

/// <summary>How animals keep company and bond (Animal Rules ▸ Social): relationships between animals, and the bond with the owner's company.</summary>
[Serializable]
public class AnimalSocialRules
{
    public bool enabled = true;

    [Header("Bonds between animals")]
    [Tooltip("Bond two animals of a kind gain a game day together in a pen.")]
    [Range(0f, 50f)] public float bondPerDay = 4f;
    [Tooltip("Bond two animals of different kinds gain a game day together.")]
    [Range(0f, 50f)] public float otherKindBondPerDay = 1f;
    [Tooltip("Bond lost a game day apart (kin lose half as much).")]
    [Range(0f, 50f)] public float bondFadePerDay = 2f;
    [Tooltip("A bond from which it is close (it misses the other when they part).")]
    [Range(0f, 100f)] public float closeFrom = 50f;
    [Tooltip("Bonds kept per animal (the weakest go first; kin are kept longest).")]
    [Range(2, 30)] public int keepBonds = 8;
    [Tooltip("Bond of a mother and her young, and of litter-mates, at birth.")]
    [Range(0f, 100f)] public float kinAtBirth = 80f;
    [Range(0f, 100f)] public float littermatesAtBirth = 60f;
    [Tooltip("Bond of a pair when they are bred.")]
    [Range(0f, 100f)] public float mateBond = 20f;

    [Header("What company does")]
    [Tooltip("Happiness a close companion near adds (at most this, from all of them).")]
    [Range(0f, 30f)] public float companionHappiness = 4f;
    [Tooltip("Stress a close companion near takes away (at most this).")]
    [Range(0f, 30f)] public float companionCalm = 4f;
    [Tooltip("Stress of missing a close companion just parted from (fading over Miss Hours).")]
    [Range(0f, 40f)] public float missStress = 8f;
    [Range(1f, 1000f)] public float missHours = 72f;
    [Tooltip("Stress of a fierce rival in the pen.")]
    [Range(0f, 40f)] public float rivalStress = 8f;
    [Tooltip("Rivalry two adult males of a rival kind build a game day together.")]
    [Range(0f, 50f)] public float rivalryPerDay = 10f;

    [Header("Time with the owner")]
    [Tooltip("Affinity an animal gains a game hour its owner spends near its pen.")]
    [Range(0f, 10f)] public float togetherPerHour = 0.5f;
    [Tooltip("Most affinity time together gives in a game day (apart from care's Affinity Per Day).")]
    [Range(0f, 50f)] public float togetherPerDay = 2f;
    [Tooltip("Metres from the pen's fence within which its owner counts as near.")]
    [Range(1f, 30f)] public float nearMetres = 8f;
    [Tooltip("How much a bond eases its owner's handling (care, milking, shearing): handling stress x (1 - this x affinity / 100).")]
    [Range(0f, 1f)] public float handlingRelief = 1f;

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (closeFrom <= 0f) warnings?.Add("Social: Close From is 0: every animal would miss every other.");
        if (missHours <= 0f) errors?.Add("Social: Miss Hours must be above 0.");
    }
}

// ==================================================================== rules
/// <summary>
/// Relationships between animals and the bond with their owner's company (FR-12, FR-13), pure C# and deterministic. Animals
/// sharing a pen grow used to each other (more quickly with their own kind); a mother and her young, and litter-mates, are
/// kin from birth; a bred pair are mates; two adult males of a rival kind (bulls, rams, boars, roosters, stallions) grow to
/// be rivals. Close companions near lift an animal's spirits and calm it; one parted from a close companion misses it for
/// a while; a fierce rival stresses it. Its owner's time near its pen deepens its bond with them a little each hour (apart
/// from care's daily share), and a deep bond unlocks its species' perks - yet a devoted animal can still be sick or hungry:
/// affinity is one part of welfare, never a stand-in for it.
/// </summary>
public static class AnimalSocial
{
    // Scratch (never state).
    private static readonly List<AnimalRecord> living = new List<AnimalRecord>();

    /// <summary>Its bond with another (null: none).</summary>
    public static AnimalBond BondWith(AnimalRecord a, string other)
    {
        if (a?.bonds == null || string.IsNullOrEmpty(other)) return null;
        foreach (AnimalBond b in a.bonds)
            if (b != null && b.other == other) return b;
        return null;
    }

    /// <summary>Makes (or strengthens to at least <paramref name="level"/>) a bond of one animal with another; a kin bond is never made a lesser kind.</summary>
    public static AnimalBond Bond(AnimalRecord a, AnimalRecord other, AnimalBondKind kind, float level, double now)
    {
        if (a == null || other == null || a.id == other.id) return null;
        a.bonds ??= new List<AnimalBond>();
        AnimalBond b = BondWith(a, other.id);
        if (b == null)
        {
            b = new AnimalBond { other = other.id, kind = kind, since = now };
            a.bonds.Add(b);
        }
        else if (b.kind != AnimalBondKind.Kin && kind != AnimalBondKind.Companion) b.kind = kind;
        b.name = other.name ?? "";
        b.level = Mathf.Max(b.level, AnimalMath.Clamp100(level));
        b.lastTogether = now;
        b.apart = false;
        return b;
    }

    /// <summary>
    /// A step of a pen's relationships (after its animals' step): pairs of living animals together grow closer (or, two adult
    /// males of a rival kind, fiercer rivals); bonds with those not here fade. In id order: the same every time.
    /// </summary>
    public static void StepHerd(AnimalHerd herd, IAnimalCatalog cat, AnimalRules rules, float dt, double end)
    {
        AnimalSocialRules r = rules?.social;
        if (herd == null || r == null || !r.enabled || dt <= 0f) return;
        living.Clear();
        foreach (AnimalRecord a in herd.animals)
            if (a != null && a.Alive) living.Add(a);
        living.Sort((x, y) => string.CompareOrdinal(x.id, y.id));
        for (int i = 0; i < living.Count; i++)
            for (int j = i + 1; j < living.Count; j++)
            {
                AnimalRecord a = living[i], b = living[j];
                bool sameKind = a.species == b.species;
                int keep = Math.Max(2, r.keepBonds);
                if (sameKind && Rivals(a, b, cat))
                {
                    float fierce = r.rivalryPerDay / 24f * dt;
                    Grow(a, b, AnimalBondKind.Rival, fierce, end, keep);
                    Grow(b, a, AnimalBondKind.Rival, fierce, end, keep);
                    continue;
                }
                float gain = (sameKind ? r.bondPerDay : r.otherKindBondPerDay) / 24f * dt;
                Grow(a, b, AnimalBondKind.Companion, gain, end, keep);
                Grow(b, a, AnimalBondKind.Companion, gain, end, keep);
            }
        foreach (AnimalRecord a in living) FadeApart(a, herd, r, dt, end);
    }

    /// <summary>
    /// A boarded animal's step: its owner's other animals at the boarding stable (<paramref name="here"/>) are with it - a
    /// mother and the young born there are not parted - and every other bond is with someone not here.
    /// </summary>
    public static void StepAlone(AnimalRecord a, AnimalRules rules, float dt, double end, HashSet<string> here = null)
    {
        AnimalSocialRules r = rules?.social;
        if (a == null || r == null || !r.enabled || !a.Alive) return;
        if (here != null && a.bonds != null)
            foreach (AnimalBond b in a.bonds)
                if (b != null && here.Contains(b.other))
                {
                    b.lastTogether = end;
                    b.apart = false;
                }
        FadeApart(a, null, r, dt, end);
    }

    private static void Grow(AnimalRecord a, AnimalRecord other, AnimalBondKind kind, float gain, double end, int keep)
    {
        AnimalBond b = BondWith(a, other.id);
        if (b == null)
        {
            a.bonds ??= new List<AnimalBond>();
            // A rival is always noticed; a new companion only where there is room - or in place of the weakest bond with
            // one not here - so a crowded pen does not make and drop bonds every step.
            if (kind != AnimalBondKind.Rival && Companions(a) >= keep && !FreeOne(a, end)) return;
            b = new AnimalBond { other = other.id, kind = kind, since = end };
            a.bonds.Add(b);
        }
        else if (kind == AnimalBondKind.Rival && b.kind != AnimalBondKind.Rival)
        {
            // Grown males become rivals: their rivalry starts afresh (old company does not make it fierce at once).
            b.kind = AnimalBondKind.Rival;
            b.level = 0f;
        }
        b.name = other.name ?? "";
        b.level = AnimalMath.Clamp100(b.level + gain);
        b.lastTogether = end;
        b.apart = false;
    }

    /// <summary>Its companion and mate bonds (kin and rivals are not counted against Keep Bonds).</summary>
    private static int Companions(AnimalRecord a)
    {
        int n = 0;
        if (a.bonds != null)
            foreach (AnimalBond b in a.bonds)
                if (b != null && (b.kind == AnimalBondKind.Companion || b.kind == AnimalBondKind.Mate)) n++;
        return n;
    }

    /// <summary>Lets go of its weakest companion bond with one not here (true: room was made).</summary>
    private static bool FreeOne(AnimalRecord a, double end)
    {
        int weakest = -1;
        for (int i = 0; i < a.bonds.Count; i++)
        {
            AnimalBond b = a.bonds[i];
            if (b == null || b.kind != AnimalBondKind.Companion || b.lastTogether >= end - 1e-6) continue;
            if (weakest < 0 || b.level < a.bonds[weakest].level) weakest = i;
        }
        if (weakest < 0) return false;
        a.bonds.RemoveAt(weakest);
        return true;
    }

    private static void FadeApart(AnimalRecord a, AnimalHerd herd, AnimalSocialRules r, float dt, double end)
    {
        if (a.bonds == null || a.bonds.Count == 0) return;
        for (int i = a.bonds.Count - 1; i >= 0; i--)
        {
            AnimalBond b = a.bonds[i];
            if (b == null || string.IsNullOrEmpty(b.other))
            {
                a.bonds.RemoveAt(i);
                continue;
            }
            if (b.lastTogether >= end - 1e-6) continue; // together this step
            float fade = r.bondFadePerDay / 24f * dt * (b.kind == AnimalBondKind.Kin ? 0.5f : 1f);
            b.level = Mathf.Max(0f, b.level - fade);
            if (b.level <= 0.01f) a.bonds.RemoveAt(i);
        }
        // Past the keep, the weakest companions go first (kin and rivals are kept: they are few, and they matter).
        int keep = Math.Max(2, r.keepBonds);
        while (Companions(a) > keep)
        {
            int weakest = -1;
            for (int i = 0; i < a.bonds.Count; i++)
            {
                AnimalBond b = a.bonds[i];
                if (b == null || (b.kind != AnimalBondKind.Companion && b.kind != AnimalBondKind.Mate)) continue;
                if (weakest < 0 || b.level < a.bonds[weakest].level || (b.level == a.bonds[weakest].level && string.CompareOrdinal(b.other, a.bonds[weakest].other) > 0)) weakest = i;
            }
            if (weakest < 0) break;
            a.bonds.RemoveAt(weakest);
        }
    }

    /// <summary>Two adult males of a kind whose males are rivals.</summary>
    public static bool Rivals(AnimalRecord a, AnimalRecord b, IAnimalCatalog cat)
    {
        if (a == null || b == null || a.species != b.species) return false;
        if (a.sex != AnimalSex.Male || b.sex != AnimalSex.Male) return false;
        AnimalSpeciesData sp = cat?.Species(a.species);
        if (sp?.breeding == null || !sp.breeding.malesRival) return false;
        return Grown(a, sp) && Grown(b, sp);
    }

    /// <summary>Old enough to breed (or to be a rival): a stage of its life that breeds, or later.</summary>
    public static bool Grown(AnimalRecord a, AnimalSpeciesData sp)
    {
        if (a == null || sp?.stages == null) return false;
        AnimalStageSpec st = sp.StageOrNearest(a.stage);
        return st != null && (st.breedingEnabled || a.stage >= GrowthStage.Adult);
    }

    /// <summary>
    /// What its company does to its spirits this step (added to its happiness and stress targets): close companions near
    /// lift it and calm it; a close one it has just parted from it misses (fading over Miss Hours; its owner is told once);
    /// a fierce rival in the pen stresses it. <paramref name="here"/>: the living animals of its pen (null: the boarding stable).
    /// </summary>
    public static void MoodTargets(AnimalRecord a, AnimalSpeciesData sp, AnimalRules rules, HashSet<string> here, double now, ref float happinessAdd,
        ref float stressAdd, AnimalSimContext ctx = null)
    {
        AnimalSocialRules r = rules?.social;
        if (a?.bonds == null || r == null || !r.enabled) return;
        float company = 0f, missing = 0f, rivalry = 0f;
        foreach (AnimalBond b in a.bonds)
        {
            if (b == null) continue;
            bool near = here != null && here.Contains(b.other);
            if (b.kind == AnimalBondKind.Rival)
            {
                if (near) rivalry = Mathf.Max(rivalry, AnimalMath.Clamp01(b.level / 50f));
                continue;
            }
            if (near)
            {
                company += AnimalMath.Clamp01(b.level / Mathf.Max(1f, r.closeFrom)) * 0.5f;
                continue;
            }
            if (b.level < r.closeFrom || b.lastTogether < 0.0) continue;
            double apartFor = now - b.lastTogether;
            if (apartFor > r.missHours) continue;
            float m = AnimalMath.Clamp01(b.level / 100f) * (1f - (float)(apartFor / Math.Max(1f, r.missHours)));
            missing = Mathf.Max(missing, m);
            if (!b.apart)
            {
                b.apart = true;
                string who = string.IsNullOrWhiteSpace(b.name) ? "a companion" : b.name;
                ctx?.Notice(a, AnimalEventKind.MissesCompanion, $"{AnimalSim.NameOf(a, sp)} seems to miss {who}.");
            }
        }
        company = Mathf.Min(1f, company);
        happinessAdd += r.companionHappiness * company - r.missStress * 0.5f * missing;
        stressAdd += -r.companionCalm * company + r.missStress * missing + r.rivalStress * rivalry;
    }

    // ---------------------------------------------------------------- the owner
    /// <summary>
    /// Its owner spent <paramref name="hours"/> (game hours) near its pen: its bond with them grows by Together Per Hour an
    /// hour, at most Together Per Day in a game day (apart from care's share), and the time counts as its owner's company (its
    /// bond does not fade meanwhile). Returns the affinity given.
    /// </summary>
    public static float TimeTogether(AnimalRecord a, float hours, double now, AnimalRules rules)
    {
        AnimalSocialRules r = rules?.social;
        if (a == null || !a.Alive || r == null || !r.enabled || hours <= 0f) return 0f;
        a.lastOwnerNearAt = now;
        // Counted on the record (the day's history entries are the simulation's: none is made ahead of it).
        int day = (int)Math.Floor(now / 24.0);
        if (a.togetherDay != day)
        {
            a.togetherDay = day;
            a.togetherGiven = 0f;
        }
        float room = Mathf.Max(0f, r.togetherPerDay - a.togetherGiven);
        float give = Mathf.Min(r.togetherPerHour * hours, room, 100f - a.affinity);
        if (give <= 0f) return 0f;
        a.affinity = AnimalMath.Clamp100(a.affinity + give);
        a.togetherGiven += give;
        a.revision++;
        return give;
    }

    /// <summary>The perks its bond with its owner has unlocked (its species' perks at or below its affinity).</summary>
    public static void Perks(AnimalRecord a, AnimalSpeciesData sp, List<AnimalAffinityPerk> into)
    {
        into?.Clear();
        if (a == null || sp?.perks == null || into == null || !a.Alive) return;
        foreach (AnimalAffinityPerk p in sp.perks)
            if (p != null && a.affinity >= p.minAffinity) into.Add(p);
    }

    /// <summary>Has its bond unlocked a perk of this kind?</summary>
    public static bool HasPerk(AnimalRecord a, AnimalSpeciesData sp, AnimalPerkKind kind)
    {
        if (a == null || sp?.perks == null || !a.Alive) return false;
        foreach (AnimalAffinityPerk p in sp.perks)
            if (p != null && p.kind == kind && a.affinity >= p.minAffinity) return true;
        return false;
    }

    /// <summary>
    /// How much handling stresses it (x the handling stress of care or collecting): less as its bond with its owner grows
    /// (1 - Handling Relief x affinity / 100: a trusting animal is easier for anyone), and its owner's none at all once
    /// Calm Handling is unlocked.
    /// </summary>
    public static float HandlingFactor(AnimalRecord a, AnimalSpeciesData sp, bool byOwner, AnimalRules rules)
    {
        if (a == null) return 1f;
        if (byOwner && HasPerk(a, sp, AnimalPerkKind.CalmHandling)) return 0f;
        float relief = rules?.social != null ? AnimalMath.Clamp01(rules.social.handlingRelief) : 1f;
        return 1f - relief * AnimalMath.Clamp01(a.affinity / 100f);
    }

    /// <summary>Its bond with its owner in a word: a stranger, wary, it knows you, friendly, devoted.</summary>
    public static string BondWord(float affinity) =>
        affinity >= 80f ? "devoted" : affinity >= 60f ? "friendly" : affinity >= 25f ? "it knows you" : affinity >= 8f ? "wary" : "a stranger to you";

    /// <summary>The bond a relationship is, in words, from the animal's side ("close to Bella", "rival of Ferdinand").</summary>
    public static string RelationWord(AnimalBond b, AnimalSocialRules r, bool here)
    {
        if (b == null) return "";
        string who = string.IsNullOrWhiteSpace(b.name) ? "another" : b.name;
        float close = r != null ? r.closeFrom : 50f;
        switch (b.kind)
        {
            case AnimalBondKind.Rival: return b.level >= 50f ? $"a fierce rival of {who}" : $"wary of {who}";
            case AnimalBondKind.Kin: return here ? $"close to {who} (kin)" : $"misses {who} (kin)";
            case AnimalBondKind.Mate: return here ? $"paired with {who}" : $"was paired with {who}";
            default: return b.level >= close ? (here ? $"close to {who}" : $"misses {who}") : here ? $"used to {who}" : "";
        }
    }
}
