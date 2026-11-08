using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a player knows of one material of a species: how many pieces they took, its price, what it is good for.</summary>
[Serializable]
public class HarvestMaterialNote
{
    public string id = "";
    /// <summary>Pieces taken.</summary>
    public int pieces;
    /// <summary>Its price is known (sold to a merchant, or enough taken).</summary>
    public bool appraised;
    /// <summary>The uses the player found by trying it (eating, brewing, selling, working it), as <see cref="HarvestUse"/> flags.</summary>
    public int uses;
}

/// <summary>What a player knows of one species: how often they harvested it and what they have found in it.</summary>
[Serializable]
public class HarvestSpeciesNote
{
    public string id = "";
    public int harvests;
    public int pieces;
    public float bestQuality;
    /// <summary>Materials of it the player has taken (they are named and valued from then on).</summary>
    public List<string> known = new List<string>();
    /// <summary>What the player knows of each of its materials (pieces taken, price, uses found).</summary>
    public List<HarvestMaterialNote> materials = new List<HarvestMaterialNote>();

    public HarvestMaterialNote Material(string materialId, bool add = false)
    {
        materials ??= new List<HarvestMaterialNote>();
        foreach (HarvestMaterialNote n in materials)
            if (n != null && string.Equals(n.id, materialId, StringComparison.OrdinalIgnoreCase))
                return n;
        if (!add || string.IsNullOrEmpty(materialId))
            return null;
        var note = new HarvestMaterialNote { id = materialId };
        materials.Add(note);
        return note;
    }
}

/// <summary>
/// A player's harvesting skills and journal: experience in each skill, what they know of each species, their best work
/// and the last things they took. Saved with the character (<c>HarvestKnowledge</c>), sent to them by the server.
/// </summary>
[Serializable]
public class HarvestSkillData
{
    /// <summary>Experience of each skill (by <see cref="HarvestSkill"/>).</summary>
    public List<float> xp = new List<float>();
    public List<HarvestSpeciesNote> species = new List<HarvestSpeciesNote>();
    public int harvests;
    public int pieces;
    /// <summary>The last results, newest last ("Bear pelt - Fine (82%)").</summary>
    public List<string> log = new List<string>();
    /// <summary>
    /// The world's discoveries (<see cref="HarvestDiscoveries"/>) as the server last sent them with these skills. Filled only
    /// on the copy sent to a network client; a saved file keeps none (the world saves its own).
    /// </summary>
    public List<string> discoveries = new List<string>();

    public float Xp(HarvestSkill s)
    {
        int i = (int)s;
        return i >= 0 && i < xp.Count ? xp[i] : 0f;
    }

    public void AddXp(HarvestSkill s, float amount)
    {
        int i = (int)s;
        while (xp.Count <= i) xp.Add(0f);
        xp[i] = Math.Max(0f, xp[i] + amount);
    }

    public HarvestSpeciesNote Species(string id, bool add = false)
    {
        foreach (HarvestSpeciesNote n in species)
            if (n != null && string.Equals(n.id, id, StringComparison.OrdinalIgnoreCase))
                return n;
        if (!add || string.IsNullOrEmpty(id))
            return null;
        var note = new HarvestSpeciesNote { id = id };
        species.Add(note);
        return note;
    }

    public void Log(string line, int keep = 40)
    {
        if (string.IsNullOrEmpty(line)) return;
        log.Add(line);
        while (log.Count > keep) log.RemoveAt(0);
    }

    public string ToJson() => JsonUtility.ToJson(this);

    public static HarvestSkillData FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new HarvestSkillData();
        try
        {
            HarvestSkillData d = JsonUtility.FromJson<HarvestSkillData>(json) ?? new HarvestSkillData();
            d.xp ??= new List<float>();
            d.species ??= new List<HarvestSpeciesNote>();
            d.log ??= new List<string>();
            d.discoveries ??= new List<string>();
            foreach (HarvestSpeciesNote n in d.species)
                if (n != null)
                {
                    n.known ??= new List<string>();
                    n.materials ??= new List<HarvestMaterialNote>();
                }
            return d;
        }
        catch (Exception)
        {
            return new HarvestSkillData();
        }
    }

    public HarvestSkillData Clone() => FromJson(ToJson());
}

/// <summary>Something a skill teaches at a level: wider margins, steadier hands, less waste.</summary>
public sealed class HarvestTechnique
{
    public readonly string id;
    public readonly string name;
    public readonly HarvestSkill skill;
    public readonly int level;
    public readonly string description;

    public HarvestTechnique(string id, string name, HarvestSkill skill, int level, string description)
    {
        this.id = id;
        this.name = name;
        this.skill = skill;
        this.level = level;
        this.description = description;
    }
}

/// <summary>
/// Levels, experience, techniques and what a player knows of a species. Skills make the work easier (wider margins,
/// steadier hands, more yield) - never automatic: the hands still do the work.
/// </summary>
public static class HarvestSkills
{
    public const string SteadyPull = "steady_pull", FineWork = "fine_work", FlawlessPelts = "flawless_pelts";
    public const string SeamSense = "seam_sense", PrimeCuts = "prime_cuts", NoWaste = "no_waste";
    public const string ReadTheBody = "read_the_body", KeenEye = "keen_eye", HuntersKnowledge = "hunters_knowledge";
    public const string EvenStrokes = "even_strokes", CleanBones = "clean_bones";
    public const string GentleDraw = "gentle_draw", WholeSinew = "whole_sinew";
    public const string SteadyHands = "steady_hands", GlandCare = "gland_care";
    public const string FeelTheRoot = "feel_the_root", LightTouch = "light_touch";

    /// <summary>Every technique, by skill and level.</summary>
    public static readonly HarvestTechnique[] Techniques =
    {
        new HarvestTechnique(SteadyPull, "Steady pull", HarvestSkill.Skinning, 10, "The hide separates over a wider band of pull."),
        new HarvestTechnique(FineWork, "Fine work", HarvestSkill.Skinning, 25, "Faces, paws and tails are no harder to skin than the back."),
        new HarvestTechnique(FlawlessPelts, "Flawless pelts", HarvestSkill.Skinning, 40, "Half as many nicks; hides come off a little finer."),
        new HarvestTechnique(SeamSense, "Seam sense", HarvestSkill.Butchering, 10, "You see the joints along a seam, and can cut through them faster."),
        new HarvestTechnique(PrimeCuts, "Prime cuts", HarvestSkill.Butchering, 25, "More meat from every cut, and better meat."),
        new HarvestTechnique(NoWaste, "No waste", HarvestSkill.Butchering, 40, "Even a hurried cut leaves little behind."),
        new HarvestTechnique(ReadTheBody, "Read the body", HarvestSkill.Anatomy, 5, "You can tell what every wound did inside: which organ, which bone."),
        new HarvestTechnique(KeenEye, "Keen eye", HarvestSkill.Anatomy, 15, "Rare things hidden in a body are easier to notice."),
        new HarvestTechnique(HuntersKnowledge, "Hunter's knowledge", HarvestSkill.Anatomy, 30, "You know where creatures are weak, and everything you take is a little finer."),
        new HarvestTechnique(EvenStrokes, "Even strokes", HarvestSkill.BoneWork, 10, "The saw keeps its rhythm over a wider beat."),
        new HarvestTechnique(CleanBones, "Clean bones", HarvestSkill.BoneWork, 30, "Scraping hardly ever gouges a bone."),
        new HarvestTechnique(GentleDraw, "Gentle draw", HarvestSkill.TendonWork, 10, "You feel a tendon catch before it does."),
        new HarvestTechnique(WholeSinew, "Whole sinew", HarvestSkill.TendonWork, 30, "Tendons stand far more strain before they snap."),
        new HarvestTechnique(SteadyHands, "Steady hands", HarvestSkill.OrganWork, 10, "The knife barely trembles in delicate work."),
        new HarvestTechnique(GlandCare, "Gland care", HarvestSkill.OrganWork, 30, "Fragile glands and sacs survive a slip."),
        new HarvestTechnique(FeelTheRoot, "Feel the root", HarvestSkill.TrophyWork, 10, "Claws and teeth work loose faster."),
        new HarvestTechnique(LightTouch, "Light touch", HarvestSkill.TrophyWork, 30, "Prying strains them far less."),
    };

    public static HarvestTechnique Technique(string id)
    {
        foreach (HarvestTechnique t in Techniques)
            if (t.id == id) return t;
        return null;
    }

    // ------------------------------------------------------------------ levels
    /// <summary>The level <paramref name="xp"/> experience reaches (0 .. max).</summary>
    public static int Level(HarvestSettings s, float xp)
    {
        int level = 0;
        float left = Math.Max(0f, xp);
        int max = Math.Max(1, s.maxLevel);
        while (level < max)
        {
            float need = s.XpToNext(level);
            if (left < need) break;
            left -= need;
            level++;
        }
        return level;
    }

    /// <summary>Experience from level 0 to <paramref name="level"/>.</summary>
    public static float XpFor(HarvestSettings s, int level)
    {
        float total = 0f;
        for (int i = 0; i < Math.Min(level, s.maxLevel); i++) total += s.XpToNext(i);
        return total;
    }

    /// <summary>How far toward the next level (0-1; 1 at the top).</summary>
    public static float Progress(HarvestSettings s, float xp)
    {
        int level = Level(s, xp);
        if (level >= s.maxLevel) return 1f;
        float into = xp - XpFor(s, level);
        return HarvestMathF.Clamp01(into / Math.Max(1e-3f, s.XpToNext(level)));
    }

    public static int Level(HarvestSettings s, HarvestSkillData d, HarvestSkill k) => d != null ? Level(s, d.Xp(k)) : 0;

    /// <summary>A skill as 0 (untrained) .. 1 (top level).</summary>
    public static double Factor(HarvestSettings s, HarvestSkillData d, HarvestSkill k) =>
        d == null ? 0 : HarvestMath.Clamp01(Level(s, d.Xp(k)) / (double)Math.Max(1, s.maxLevel));

    /// <summary>Does the player know <paramref name="techniqueId"/> (its skill is at its level)?</summary>
    public static bool Knows(HarvestSettings s, HarvestSkillData d, string techniqueId)
    {
        HarvestTechnique t = Technique(techniqueId);
        return t != null && d != null && Level(s, d.Xp(t.skill)) >= t.level;
    }

    /// <summary>
    /// Adds experience to a skill. Returns what the player should be told: the new level and any technique learnt (empty
    /// when the level did not change).
    /// </summary>
    public static List<string> Gain(HarvestSettings s, HarvestSkillData d, HarvestSkill k, float amount)
    {
        var said = new List<string>();
        if (d == null || amount <= 0f)
            return said;
        int before = Level(s, d.Xp(k));
        d.AddXp(k, amount);
        int after = Level(s, d.Xp(k));
        if (after > before)
        {
            said.Add($"{HarvestText.SkillName(k)} rises to {after}.");
            foreach (HarvestTechnique t in Techniques)
                if (t.skill == k && t.level > before && t.level <= after)
                    said.Add($"You learn {t.name}: {t.description}");
        }
        return said;
    }

    // ------------------------------------------------------------------ species
    /// <summary>How well the player knows a species (0 never harvested .. 1 known): its materials are named and valued from then on.</summary>
    public static double Familiarity(HarvestSettings s, HarvestSkillData d, string anatomyId)
    {
        HarvestSpeciesNote n = d?.Species(anatomyId);
        return n == null ? 0 : HarvestMath.Clamp01(n.harvests / (double)Math.Max(1, s.harvestsToKnowSpecies));
    }

    /// <summary>
    /// Can the player notice <paramref name="m"/> in a body: always for common materials; rare finds need its Reveal Skill
    /// of Anatomy (Keen Eye lowers it by a third), or to have taken it from that species before.
    /// </summary>
    public static bool Notices(HarvestSettings s, HarvestSkillData d, string anatomyId, HarvestMaterial m)
    {
        if (m == null || m.revealSkill <= 0)
            return true;
        int anatomy = Level(s, d, HarvestSkill.Anatomy);
        int need = Knows(s, d, KeenEye) ? (int)Math.Ceiling(m.revealSkill * 0.67) : m.revealSkill;
        if (anatomy >= need)
            return true;
        HarvestSpeciesNote n = d?.Species(anatomyId);
        return n != null && n.known.Contains(m.id);
    }

    // ------------------------------------------------------------------ what a material is worth and good for
    /// <summary>
    /// How well the player knows what <paramref name="m"/> is worth. <b>Known</b> (its price): sold to a merchant, Pieces
    /// To Know Value pieces taken, or the species well known (familiarity a half). <b>Estimate</b> (a rough word): taken at
    /// least once, or Anatomy at Anatomy To Estimate Value. Otherwise <b>Unknown</b>: "worth ?". So a player learns by
    /// trying which parts are worth the trouble.
    /// </summary>
    public static HarvestValueKnowledge ValueKnowledge(HarvestSettings s, HarvestSkillData d, string anatomyId, HarvestMaterial m)
    {
        if (m == null)
            return HarvestValueKnowledge.Unknown;
        HarvestSpeciesNote species = d?.Species(anatomyId);
        HarvestMaterialNote n = species?.Material(m.id);
        if (n != null && (n.appraised || n.pieces >= Math.Max(1, s.piecesToKnowValue)))
            return HarvestValueKnowledge.Known;
        if (Familiarity(s, d, anatomyId) >= 0.5)
            return HarvestValueKnowledge.Known;
        if ((n != null && n.pieces > 0) || (species != null && species.known.Contains(m.id)))
            return HarvestValueKnowledge.Estimate;
        if (Level(s, d, HarvestSkill.Anatomy) >= s.anatomyToEstimateValue)
            return HarvestValueKnowledge.Estimate;
        return HarvestValueKnowledge.Unknown;
    }

    /// <summary>
    /// The uses of <paramref name="m"/> the player knows: the obvious ones (meat is food, a hide is for crafting, a skull a
    /// trophy), those its discovery made known to all, those they found by trying it, and - with Anatomy at Anatomy To
    /// Sense Uses - every one (a trained eye knows a gall bladder is worth a lot to an alchemist).
    /// </summary>
    public static HarvestUse KnownUses(HarvestSettings s, HarvestSkillData d, string anatomyId, HarvestMaterial m)
    {
        if (m == null)
            return HarvestUse.None;
        HarvestUse all = m.Uses;
        if (Level(s, d, HarvestSkill.Anatomy) >= s.anatomyToSenseUses)
            return all;
        HarvestMaterialNote n = d?.Species(anatomyId)?.Material(m.id);
        HarvestUse tried = n != null ? (HarvestUse)n.uses : HarvestUse.None;
        return all & (m.ObviousUses() | tried);
    }

    /// <summary>Uses the player has not found yet (they show as "something else?" when it has any).</summary>
    public static bool HasUnknownUses(HarvestSettings s, HarvestSkillData d, string anatomyId, HarvestMaterial m) =>
        m != null && (m.Uses & ~KnownUses(s, d, anatomyId, m)) != HarvestUse.None;

    /// <summary>The player took <paramref name="pieces"/> of <paramref name="m"/>. Returns true when they now know its price for the first time.</summary>
    public static bool NotePieces(HarvestSettings s, HarvestSkillData d, string anatomyId, HarvestMaterial m, int pieces)
    {
        if (d == null || m == null || pieces <= 0)
            return false;
        HarvestMaterialNote n = d.Species(anatomyId, true)?.Material(m.id, true);
        if (n == null)
            return false;
        bool before = n.appraised || n.pieces >= Math.Max(1, s.piecesToKnowValue);
        n.pieces += pieces;
        return !before && n.pieces >= Math.Max(1, s.piecesToKnowValue);
    }

    /// <summary>
    /// The player used <paramref name="m"/> as <paramref name="use"/> (ate it, put it in a vessel, sold it...). Returns the
    /// uses they learnt by it (none when they knew them), so the game can tell them ("Bear liver: food.").
    /// </summary>
    public static HarvestUse LearnUse(HarvestSkillData d, string anatomyId, HarvestMaterial m, HarvestUse use)
    {
        if (d == null || m == null)
            return HarvestUse.None;
        HarvestUse real = use & m.Uses;
        HarvestMaterialNote n = d.Species(anatomyId, true)?.Material(m.id, true);
        if (n == null || real == HarvestUse.None)
            return HarvestUse.None;
        HarvestUse fresh = real & ~(HarvestUse)n.uses & ~m.ObviousUses();
        n.uses |= (int)real;
        return fresh;
    }

    /// <summary>The player learnt the price of <paramref name="m"/> (a merchant paid for it). Returns true when it is new to them.</summary>
    public static bool Appraise(HarvestSkillData d, string anatomyId, HarvestMaterial m)
    {
        if (d == null || m == null)
            return false;
        HarvestMaterialNote n = d.Species(anatomyId, true)?.Material(m.id, true);
        if (n == null || n.appraised)
            return false;
        n.appraised = true;
        return true;
    }

    /// <summary>A rough word for a price per piece: "worth nothing", "worth little", "worth something", "valuable", "very valuable", "a fortune".</summary>
    public static string ValueWord(float value)
    {
        if (value < 0.25f) return "worth nothing";
        if (value < 2f) return "worth little";
        if (value < 8f) return "worth something";
        if (value < 25f) return "valuable";
        if (value < 80f) return "very valuable";
        return "worth a fortune";
    }

    /// <summary>The uses as words: "food, alchemy" ("no known use" when none).</summary>
    public static string UsesWords(HarvestUse u)
    {
        var l = new List<string>();
        if ((u & HarvestUse.Food) != 0) l.Add("food");
        if ((u & HarvestUse.Alchemy) != 0) l.Add("alchemy");
        if ((u & HarvestUse.Crafting) != 0) l.Add("crafting");
        if ((u & HarvestUse.Trophy) != 0) l.Add("a trophy");
        if ((u & HarvestUse.Trade) != 0) l.Add("trade");
        return l.Count == 0 ? "no known use" : string.Join(", ", l);
    }

    /// <summary>
    /// The experience of taking pieces of <paramref name="m"/>: its xp per piece, more for hard work, less for a botched
    /// job (but never less than the settings' Failure Share: mistakes teach too).
    /// </summary>
    public static float XpFor(HarvestSettings s, HarvestMaterial m, int pieces, double performance)
    {
        if (m == null)
            return 0f;
        double perf = s.failureShare + (1 - s.failureShare) * HarvestMath.Clamp01(performance);
        double n = m.countable ? Math.Max(1, pieces) : 2 + Math.Min(4, pieces * 0.5);
        return (float)(m.xp * n * (0.5 + m.difficulty) * perf);
    }
}
