using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// How a creature's body is built. It decides the drawing of its anatomy, where hits land when it has no hitboxes (a
/// front-to-back axis for horizontal bodies), how its hide lies flat and where it is cut open.
/// Serialized as numbers: only add new values at the END.
/// </summary>
public enum HarvestBodyPlan
{
    /// <summary>Four legs and a horizontal body: bears, wolves, deer, boars, drakes.</summary>
    Quadruped,
    /// <summary>Two legs, upright: trolls, ogres, apes, goblins.</summary>
    Biped,
    /// <summary>Wings and two legs: birds, bats, the eagle half of a griffon.</summary>
    Avian,
    /// <summary>Fins and a tail: fish, sharks.</summary>
    Fish,
    /// <summary>One long body without limbs: snakes, wyrms, giant worms.</summary>
    Serpent,
    /// <summary>A shell and many jointed legs: spiders, scorpions, giant beetles, crabs.</summary>
    Arthropod,
}

/// <summary>The tissues a region of a body is made of. Hits damage them by their form and element; materials are taken from them.</summary>
public enum HarvestTissue
{
    Hide,
    Meat,
    Bone,
    Tendon,
    Organs,
}

/// <summary>How a material is taken from a carcass: each has its own work (minigame), tools and skill. Serialized as numbers: add at the END.</summary>
public enum HarvestMethod
{
    /// <summary>The hide, pelt, skin, scales or feathers: cut it open along the right lines, then separate it from the flesh.</summary>
    Skinning,
    /// <summary>Cuts of meat and fat: follow the seams between the muscles, find the joints.</summary>
    Butchering,
    /// <summary>Tendons and sinew: free their ends, then draw them out whole.</summary>
    Tendons,
    /// <summary>Bones and skulls: saw them free at the joints, then clean them.</summary>
    Bones,
    /// <summary>Hearts, livers, glands: open the body, cut them free without nicking them (or what is around them), lift them out.</summary>
    Organs,
    /// <summary>Claws, teeth, horns, antlers, talons, scales, shell plates: work them loose without cracking them, or saw them off.</summary>
    Trophies,
}

/// <summary>The harvesting skills. Serialized as numbers: add at the END.</summary>
public enum HarvestSkill
{
    /// <summary>Hides, pelts, scales and feathers come off whole.</summary>
    Skinning,
    /// <summary>More meat, better cuts, less waste.</summary>
    Butchering,
    /// <summary>Knowing bodies: where to cut, where the joints are, what is valuable, rare finds. Grows with every harvest.</summary>
    Anatomy,
    /// <summary>Bones come out clean and unbroken.</summary>
    BoneWork,
    /// <summary>Tendons and sinew come out long and whole.</summary>
    TendonWork,
    /// <summary>Organs and glands come out intact.</summary>
    OrganWork,
    /// <summary>Claws, teeth, horns and scales come off without cracking.</summary>
    TrophyWork,
}

/// <summary>What a harvesting tool is for. A tool can serve several (a hunting knife skins and butchers, not as well as each proper tool).</summary>
[Flags]
public enum HarvestToolKind
{
    None = 0,
    /// <summary>A skinning knife: curved, keen, a rounded tip that does not pierce the hide.</summary>
    Skinning = 1 << 0,
    /// <summary>A butcher's knife or cleaver: long, stiff, for meat and joints.</summary>
    Butchering = 1 << 1,
    /// <summary>A lancet, scalpel or fine knife: organs, glands, tendons, delicate work.</summary>
    Fine = 1 << 2,
    /// <summary>A bone saw: bones, horns, antlers.</summary>
    Saw = 1 << 3,
    /// <summary>A scraper or fleshing blade: cleaning bones and membranes.</summary>
    Scraper = 1 << 4,
    /// <summary>Pliers, tongs or a pry bar: teeth, claws, scales.</summary>
    Pliers = 1 << 5,
    /// <summary>A hatchet or axe: crude, fast, splinters bone.</summary>
    Chopping = 1 << 6,
}

/// <summary>What a harvested material is. Serialized as numbers: add at the END.</summary>
public enum HarvestMaterialKind
{
    Hide,
    Scales,
    Feathers,
    Meat,
    Fat,
    Tendon,
    Bone,
    Organ,
    Gland,
    Claw,
    Tooth,
    Horn,
    Chitin,
    Essence,
    Other,
}

/// <summary>How good a harvested piece is (from its quality, 0-1).</summary>
public enum HarvestGrade
{
    Ruined,
    Poor,
    Fair,
    Good,
    Fine,
    Pristine,
}

/// <summary>The age of a creature: young ones give tender meat and small trophies, old ones tough meat and big horns.</summary>
public enum HarvestAge
{
    Young,
    Adult,
    Old,
    Ancient,
}

/// <summary>What the whole body of a creature does on a hit to a region, in a generated body-part profile.</summary>
public enum HarvestRegionReaction
{
    None,
    /// <summary>A short stun (head hits).</summary>
    Stagger,
    /// <summary>Slows it (leg and wing hits).</summary>
    Slow,
}

/// <summary>
/// What marked a piece: what the fight did to it (cut, pierced, burned...), what the harvest did (nicked, ripped,
/// contaminated...) and time (spoiling). Every piece keeps its marks with its quality, so a burned pelt and a pelt torn
/// while skinning are told apart. Exactly 20 flags (they are kept in an item's value, see <see cref="HarvestItemCode"/>).
/// </summary>
[Flags]
public enum HarvestMarks
{
    None = 0,
    // ---- the fight
    /// <summary>Cut by a blade.</summary>
    Cut = 1 << 0,
    /// <summary>Pierced: an arrow, a spear, a horn.</summary>
    Pierced = 1 << 1,
    /// <summary>Bruised by blows.</summary>
    Bruised = 1 << 2,
    /// <summary>Broken bone.</summary>
    Fractured = 1 << 3,
    /// <summary>Torn by teeth or claws.</summary>
    Torn = 1 << 4,
    /// <summary>Crushed by a great weight.</summary>
    Crushed = 1 << 5,
    /// <summary>Burned or charred.</summary>
    Burned = 1 << 6,
    /// <summary>Frozen (it keeps, but the hide goes brittle).</summary>
    Frozen = 1 << 7,
    /// <summary>Soiled by a gut, bladder or gall torn open in the fight (a gut-shot).</summary>
    GutShot = 1 << 8,
    /// <summary>Poisoned: not fit to eat.</summary>
    Poisoned = 1 << 9,
    /// <summary>Tainted by dark (or searing holy) magic.</summary>
    Tainted = 1 << 10,
    /// <summary>Blasted by an explosion.</summary>
    Blasted = 1 << 11,
    // ---- the harvest
    /// <summary>Nicked by the knife while harvesting.</summary>
    Nicked = 1 << 12,
    /// <summary>Ripped by pulling too hard while harvesting.</summary>
    Ripped = 1 << 13,
    /// <summary>Soiled by a ruptured gut, bladder or gall while harvesting.</summary>
    Contaminated = 1 << 14,
    /// <summary>Splintered by a crooked or rushed saw.</summary>
    Splintered = 1 << 15,
    /// <summary>Cracked while worked loose.</summary>
    Cracked = 1 << 16,
    /// <summary>Hacked out with the wrong tool or in a hurry: ragged and wasteful.</summary>
    Rough = 1 << 17,
    // ---- time
    /// <summary>Starting to turn.</summary>
    Spoiling = 1 << 18,
    /// <summary>Gone off.</summary>
    Spoiled = 1 << 19,
}

/// <summary>Marks grouped by their cause.</summary>
public static class HarvestMarksInfo
{
    /// <summary>Everything the fight can leave.</summary>
    public const HarvestMarks Combat = HarvestMarks.Cut | HarvestMarks.Pierced | HarvestMarks.Bruised | HarvestMarks.Fractured | HarvestMarks.Torn |
                                       HarvestMarks.Crushed | HarvestMarks.Burned | HarvestMarks.Frozen | HarvestMarks.GutShot | HarvestMarks.Poisoned |
                                       HarvestMarks.Tainted | HarvestMarks.Blasted;
    /// <summary>Everything the harvest can leave.</summary>
    public const HarvestMarks Harvest = HarvestMarks.Nicked | HarvestMarks.Ripped | HarvestMarks.Contaminated | HarvestMarks.Splintered |
                                        HarvestMarks.Cracked | HarvestMarks.Rough;
    /// <summary>What time does.</summary>
    public const HarvestMarks Time = HarvestMarks.Spoiling | HarvestMarks.Spoiled;
    /// <summary>The 20 flags (what fits in an item's value).</summary>
    public const int Mask = (1 << 20) - 1;

    private static readonly (HarvestMarks mark, string word)[] Words =
    {
        (HarvestMarks.Cut, "cut"), (HarvestMarks.Pierced, "pierced"), (HarvestMarks.Bruised, "bruised"), (HarvestMarks.Fractured, "fractured"),
        (HarvestMarks.Torn, "torn"), (HarvestMarks.Crushed, "crushed"), (HarvestMarks.Burned, "burned"), (HarvestMarks.Frozen, "frozen"),
        (HarvestMarks.GutShot, "soiled by a torn gut"), (HarvestMarks.Poisoned, "poisoned"), (HarvestMarks.Tainted, "tainted"),
        (HarvestMarks.Blasted, "blasted"), (HarvestMarks.Nicked, "nicked"), (HarvestMarks.Ripped, "ripped"),
        (HarvestMarks.Contaminated, "soiled"), (HarvestMarks.Splintered, "splintered"), (HarvestMarks.Cracked, "cracked"),
        (HarvestMarks.Rough, "roughly cut"), (HarvestMarks.Spoiling, "starting to turn"), (HarvestMarks.Spoiled, "spoiled"),
    };

    /// <summary>"burned, pierced" (the marks among <paramref name="filter"/>, in a fixed order; "" when none).</summary>
    public static string Describe(HarvestMarks marks, HarvestMarks filter = (HarvestMarks)Mask)
    {
        marks &= filter;
        if (marks == HarvestMarks.None)
            return "";
        var sb = new StringBuilder();
        foreach ((HarvestMarks mark, string word) in Words)
        {
            if ((marks & mark) == 0)
                continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(word);
        }
        return sb.ToString();
    }

    /// <summary>
    /// "Burned and pierced in the fight; nicked while harvesting; spoiling" - the causes told apart (empty when unmarked).
    /// </summary>
    public static string Story(HarvestMarks marks)
    {
        var parts = new List<string>(3);
        string combat = Describe(marks, Combat);
        if (combat.Length > 0) parts.Add(HarvestText.Capital(combat) + " in the fight");
        string harvest = Describe(marks, Harvest);
        if (harvest.Length > 0) parts.Add((parts.Count == 0 ? HarvestText.Capital(harvest) : harvest) + " while harvesting");
        string time = Describe(marks, Time);
        if (time.Length > 0) parts.Add(parts.Count == 0 ? HarvestText.Capital(time) : time);
        return string.Join("; ", parts);
    }
}

/// <summary>Grades, names and colours shared by the window, the tooltips and the journal.</summary>
public static class HarvestText
{
    /// <summary>The grade of a quality (0-1): Ruined below 0.15, Poor, Fair, Good, Fine, Pristine from 0.9.</summary>
    public static HarvestGrade Grade(double quality)
    {
        if (quality < 0.15) return HarvestGrade.Ruined;
        if (quality < 0.35) return HarvestGrade.Poor;
        if (quality < 0.55) return HarvestGrade.Fair;
        if (quality < 0.75) return HarvestGrade.Good;
        if (quality < 0.9) return HarvestGrade.Fine;
        return HarvestGrade.Pristine;
    }

    /// <summary>The colour of a grade (red for ruined to gold for pristine).</summary>
    public static Color GradeColor(HarvestGrade g)
    {
        switch (g)
        {
            case HarvestGrade.Ruined: return new Color(0.75f, 0.32f, 0.3f);
            case HarvestGrade.Poor: return new Color(0.9f, 0.55f, 0.35f);
            case HarvestGrade.Fair: return new Color(0.85f, 0.8f, 0.5f);
            case HarvestGrade.Good: return new Color(0.55f, 0.85f, 0.55f);
            case HarvestGrade.Fine: return new Color(0.45f, 0.75f, 1f);
            default: return new Color(1f, 0.82f, 0.38f);
        }
    }

    /// <summary>A condition (0 destroyed .. 1 intact) as a colour: green, yellow, red, black.</summary>
    public static Color ConditionColor(double integrity)
    {
        float t = (float)HarvestMath.Clamp01(integrity);
        if (t <= 0.05f) return new Color(0.16f, 0.12f, 0.12f);
        if (t < 0.5f) return LerpColor(new Color(0.85f, 0.25f, 0.2f), new Color(0.95f, 0.75f, 0.25f), (t - 0.05f) / 0.45f);
        return LerpColor(new Color(0.95f, 0.75f, 0.25f), new Color(0.4f, 0.82f, 0.45f), (t - 0.5f) / 0.5f);
    }

    public static Color LerpColor(Color a, Color b, float t)
    {
        t = t < 0f ? 0f : t > 1f ? 1f : t;
        return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
    }

    /// <summary>"Pristine", "Good"...</summary>
    public static string GradeName(HarvestGrade g) => g.ToString();

    /// <summary>Hex of a grade's colour for rich text ("#8CD98C").</summary>
    public static string GradeHex(HarvestGrade g) => Hex(GradeColor(g));

    public static string Hex(Color c)
    {
        int r = (int)Math.Round(HarvestMath.Clamp01(c.r) * 255), g = (int)Math.Round(HarvestMath.Clamp01(c.g) * 255), b = (int)Math.Round(HarvestMath.Clamp01(c.b) * 255);
        return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    /// <summary>"Pristine (94%)" in the grade's colour (rich text).</summary>
    public static string Quality(double quality)
    {
        HarvestGrade g = Grade(quality);
        return $"<color={GradeHex(g)}>{g} ({quality * 100:0}%)</color>";
    }

    public static string MethodName(HarvestMethod m)
    {
        switch (m)
        {
            case HarvestMethod.Skinning: return "Skinning";
            case HarvestMethod.Butchering: return "Butchering";
            case HarvestMethod.Tendons: return "Tendons";
            case HarvestMethod.Bones: return "Bones";
            case HarvestMethod.Organs: return "Organs";
            default: return "Claws, teeth & horns";
        }
    }

    /// <summary>The skill a method trains.</summary>
    public static HarvestSkill SkillOf(HarvestMethod m)
    {
        switch (m)
        {
            case HarvestMethod.Skinning: return HarvestSkill.Skinning;
            case HarvestMethod.Butchering: return HarvestSkill.Butchering;
            case HarvestMethod.Tendons: return HarvestSkill.TendonWork;
            case HarvestMethod.Bones: return HarvestSkill.BoneWork;
            case HarvestMethod.Organs: return HarvestSkill.OrganWork;
            default: return HarvestSkill.TrophyWork;
        }
    }

    public static string SkillName(HarvestSkill s)
    {
        switch (s)
        {
            case HarvestSkill.BoneWork: return "Bone Harvesting";
            case HarvestSkill.TendonWork: return "Tendon Extraction";
            case HarvestSkill.OrganWork: return "Organ Harvesting";
            case HarvestSkill.TrophyWork: return "Trophy Extraction";
            default: return s.ToString();
        }
    }

    /// <summary>"Skinning knife", "Bone saw"...</summary>
    public static string ToolName(HarvestToolKind k)
    {
        switch (k)
        {
            case HarvestToolKind.Skinning: return "Skinning knife";
            case HarvestToolKind.Butchering: return "Butcher's knife";
            case HarvestToolKind.Fine: return "Fine blade";
            case HarvestToolKind.Saw: return "Bone saw";
            case HarvestToolKind.Scraper: return "Scraper";
            case HarvestToolKind.Pliers: return "Pliers";
            case HarvestToolKind.Chopping: return "Hatchet";
            default: return "Bare hands";
        }
    }

    public static string KindName(HarvestMaterialKind k) => k == HarvestMaterialKind.Other ? "Material" : k.ToString();

    public static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>"3 min", "45 s", "1 h 05".</summary>
    public static string Minutes(double minutes)
    {
        if (double.IsInfinity(minutes) || double.IsNaN(minutes)) return "never";
        // Rounded first, then split: 59.7 min is "1 h 00", never "60 min".
        double seconds = Math.Round(Math.Max(0, minutes * 60), MidpointRounding.AwayFromZero);
        if (seconds < 60) return $"{seconds:0} s";
        long whole = (long)Math.Round(Math.Max(0, minutes), MidpointRounding.AwayFromZero);
        if (whole < 60) return $"{whole} min";
        return $"{whole / 60} h {whole % 60:00}";
    }

    /// <summary>A bar of <paramref name="width"/> characters filled to <paramref name="fraction"/> ("▰▰▰▱▱").</summary>
    public static string Bar(double fraction, int width = 10)
    {
        int n = (int)Math.Round(HarvestMath.Clamp01(fraction) * width);
        return new string('▰', n) + new string('▱', Math.Max(0, width - n));
    }
}

/// <summary>Maths for the harvesting rules (System only, so the rules run and are tested anywhere).</summary>
public static class HarvestMath
{
    public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
    public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    public static double Lerp(double a, double b, double t) => a + (b - a) * Clamp01(t);
    public static double InverseLerp(double a, double b, double v) => Math.Abs(b - a) < 1e-12 ? 0 : Clamp01((v - a) / (b - a));
    public static double Smooth(double a, double b, double v)
    {
        double t = InverseLerp(a, b, v);
        return t * t * (3 - 2 * t);
    }

    /// <summary>A repeatable 0-1 value (the same seed, index and salt always give the same value).</summary>
    public static double Noise(int seed, int index, int salt = 0) => CraftMath.Noise(seed, index, salt);

    public static int Hash(string s) => CraftMath.Hash(s);
    public static int Hash(int a, int b, int c = 0) => CraftMath.Hash(a, b, c);
}
