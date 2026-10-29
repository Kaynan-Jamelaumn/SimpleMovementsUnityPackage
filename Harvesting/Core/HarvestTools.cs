using System;
using UnityEngine;

/// <summary>How well a tool does each kind of harvesting work (0 = useless for it .. 1 = made for it).</summary>
[Serializable]
public class HarvestToolFit
{
    [Range(0f, 1f)] public float skinning;
    [Range(0f, 1f)] public float butchering;
    [Tooltip("Organs, glands, tendons: delicate cuts.")]
    [Range(0f, 1f)] public float fine;
    [Range(0f, 1f)] public float saw;
    [Range(0f, 1f)] public float scraper;
    [Tooltip("Gripping and prying: teeth, claws, scales; drawing out tendons.")]
    [Range(0f, 1f)] public float pliers;
    [Tooltip("Hacking through joints and bone: fast and crude.")]
    [Range(0f, 1f)] public float chopping;

    public HarvestToolFit() { }

    public HarvestToolFit(float skinning, float butchering, float fine, float saw, float scraper, float pliers, float chopping)
    {
        this.skinning = skinning;
        this.butchering = butchering;
        this.fine = fine;
        this.saw = saw;
        this.scraper = scraper;
        this.pliers = pliers;
        this.chopping = chopping;
    }

    /// <summary>How well it does one kind of work.</summary>
    public float For(HarvestToolKind kind)
    {
        switch (kind)
        {
            case HarvestToolKind.Skinning: return skinning;
            case HarvestToolKind.Butchering: return butchering;
            case HarvestToolKind.Fine: return fine;
            case HarvestToolKind.Saw: return saw;
            case HarvestToolKind.Scraper: return scraper;
            case HarvestToolKind.Pliers: return pliers;
            case HarvestToolKind.Chopping: return chopping;
            default: return 0f;
        }
    }

    /// <summary>The best it does among <paramref name="kinds"/> (and which kind that is).</summary>
    public float Best(HarvestToolKind kinds, out HarvestToolKind which)
    {
        which = HarvestToolKind.None;
        float best = 0f;
        foreach (HarvestToolKind k in HarvestTools.Kinds)
        {
            if ((kinds & k) == 0) continue;
            float v = For(k);
            if (v > best) { best = v; which = k; }
        }
        return best;
    }

    /// <summary>The kinds it does at least <paramref name="threshold"/> well.</summary>
    public HarvestToolKind Kinds(float threshold = 0.5f)
    {
        HarvestToolKind k = HarvestToolKind.None;
        foreach (HarvestToolKind x in HarvestTools.Kinds)
            if (For(x) >= threshold) k |= x;
        return k;
    }

    public HarvestToolFit Clone() => (HarvestToolFit)MemberwiseClone();
}

/// <summary>
/// A harvesting tool as the work sees it: what it is good for, how keen its edge is (a worn knife is duller), how well
/// made it is and how fast it works. Made from a harvesting tool item, a crafted knife (its quality), a weapon (by its
/// category: a dagger skins after a fashion, an axe hacks through joints) or the bare hands.
/// </summary>
[Serializable]
public class HarvestToolProfile
{
    [Tooltip("The item's id (or a name for tools that are not items).")]
    public string id = "";
    public string displayName = "Bare hands";
    public HarvestToolFit fit = new HarvestToolFit();
    [Tooltip("The edge (0 dull .. 1 keen): margins and speed of cutting work.")]
    [Range(0f, 1f)] public float sharpness = 1f;
    [Tooltip("How well made (0 crude .. 1 masterwork): steadier, cleaner work.")]
    [Range(0f, 1f)] public float quality = 0.5f;
    [Tooltip("How fast it works (× progress).")]
    [Range(0.25f, 3f)] public float speed = 1f;

    /// <summary>
    /// How fit it is for <paramref name="kind"/> of work, all told (0-1): what it is made for, its edge (for cutting work)
    /// and how well it is made.
    /// </summary>
    public float Fitness(HarvestToolKind kind)
    {
        float f = fit != null ? fit.For(kind) : 0f;
        if (f <= 0f)
            return 0f;
        bool cutting = kind == HarvestToolKind.Skinning || kind == HarvestToolKind.Butchering || kind == HarvestToolKind.Fine ||
                       kind == HarvestToolKind.Saw || kind == HarvestToolKind.Chopping;
        float edge = cutting ? 0.45f + 0.55f * HarvestMathF.Clamp01(sharpness) : 1f;
        return HarvestMathF.Clamp01(f * edge * (0.75f + 0.25f * HarvestMathF.Clamp01(quality)) / 0.97f);
    }

    /// <summary>The best fitness among <paramref name="kinds"/> (and which kind it is used as).</summary>
    public float Fitness(HarvestToolKind kinds, out HarvestToolKind usedAs)
    {
        usedAs = HarvestToolKind.None;
        float best = 0f;
        foreach (HarvestToolKind k in HarvestTools.Kinds)
        {
            if ((kinds & k) == 0) continue;
            float f = Fitness(k);
            if (f > best) { best = f; usedAs = k; }
        }
        return best;
    }

    public HarvestToolProfile Clone()
    {
        var c = (HarvestToolProfile)MemberwiseClone();
        c.fit = fit != null ? fit.Clone() : new HarvestToolFit();
        return c;
    }

    /// <summary>"Skinning ★★★ · Fine ★★" (the kinds it is decent at).</summary>
    public string DescribeFit()
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (HarvestToolKind k in HarvestTools.Kinds)
        {
            float f = fit != null ? fit.For(k) : 0f;
            if (f < 0.2f) continue;
            int stars = f >= 0.85f ? 3 : f >= 0.55f ? 2 : 1;
            parts.Add($"{HarvestTools.KindWord(k)} {new string('★', stars)}{new string('☆', 3 - stars)}");
        }
        return parts.Count > 0 ? string.Join(" · ", parts) : "not a harvesting tool";
    }
}

/// <summary>Tool kinds, words and the fitness of improvised tools.</summary>
public static class HarvestTools
{
    /// <summary>Every kind, in order.</summary>
    public static readonly HarvestToolKind[] Kinds =
    {
        HarvestToolKind.Skinning, HarvestToolKind.Butchering, HarvestToolKind.Fine, HarvestToolKind.Saw, HarvestToolKind.Scraper,
        HarvestToolKind.Pliers, HarvestToolKind.Chopping,
    };

    /// <summary>"Skinning", "Sawing"...</summary>
    public static string KindWord(HarvestToolKind k)
    {
        switch (k)
        {
            case HarvestToolKind.Skinning: return "Skinning";
            case HarvestToolKind.Butchering: return "Butchering";
            case HarvestToolKind.Fine: return "Fine cuts";
            case HarvestToolKind.Saw: return "Sawing";
            case HarvestToolKind.Scraper: return "Scraping";
            case HarvestToolKind.Pliers: return "Prying";
            case HarvestToolKind.Chopping: return "Chopping";
            default: return "Nothing";
        }
    }

    /// <summary>The bare hands: they can pull and pry a little, scrape a little, cut nothing.</summary>
    public static HarvestToolProfile BareHands() => new HarvestToolProfile
    {
        id = "", displayName = "Bare hands", sharpness = 0f, quality = 0.3f, speed = 0.7f,
        fit = new HarvestToolFit(0f, 0f, 0f, 0f, 0.12f, 0.2f, 0f),
    };

    /// <summary>
    /// Words for how fit a tool is: "made for it", "good", "makeshift", "the wrong tool" (below
    /// <paramref name="wrongBelow"/>), "useless".
    /// </summary>
    public static string FitnessWord(float fitness, float wrongBelow = 0.3f)
    {
        if (fitness <= 0.01f) return "useless";
        if (fitness < wrongBelow) return "the wrong tool";
        if (fitness < 0.55f) return "makeshift";
        if (fitness < 0.8f) return "good";
        return "made for it";
    }
}
