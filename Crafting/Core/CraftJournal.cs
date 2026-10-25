using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A metal (by its name) the player has worked.</summary>
[Serializable]
public class CraftClassNote
{
    public string classId = "";
    public int timesWorked;
    [Tooltip("Known by name: assayed, or worked often enough to recognise.")]
    public bool identified;
    public bool assayed;
}

/// <summary>An ore the player has handled.</summary>
[Serializable]
public class CraftOreNote
{
    public string oreId = "";
    public int timesHandled;
    public int timesSmelted;
    public bool identified;
    [Tooltip("Deposits the player has smelted from, and what they gave (a note per deposit).")]
    public List<string> deposits = new List<string>();
}

/// <summary>A heat colour and what the thermometer said while it showed.</summary>
[Serializable]
public class CraftHeatNote
{
    public string color = "";
    public int readings;
    public float min = 9999, max = -9999;
}

/// <summary>What happened when a metal was quenched in a medium.</summary>
[Serializable]
public class CraftQuenchNote
{
    public string classId = "";
    public string medium = "";
    public int times;
    public int hardened, soft, cracked;
}

/// <summary>An item the player has made, with the best result and how it was made.</summary>
[Serializable]
public class CraftDesignNote
{
    public string designId = "";
    public string name = "";
    public int timesMade;
    public float bestQuality;
    public string bestTier = "";
    public string bestName = "";
    public long firstUtc, lastUtc;
    [Tooltip("The steps of the best one (its pieces' records).")]
    public List<string> bestProcedure = new List<string>();
}

/// <summary>One finished piece of work: what it became and what was noticed.</summary>
[Serializable]
public class CraftExperiment
{
    public long utc;
    public string name = "";
    public float quality;
    public string tier = "";
    public List<string> notes = new List<string>();
    public List<string> procedure = new List<string>();
}

/// <summary>
/// Everything a player knows about smithing and leatherworking (JsonUtility-friendly): the metals they can name, the
/// ores they know, the heat colours they have learnt to read, how their quenches went, what they have made and how, the
/// insights they have had. Nothing is revealed up front: the station window shows only what is in here.
/// </summary>
[Serializable]
public class CraftJournalData
{
    public int version = 1;
    public List<CraftClassNote> classes = new List<CraftClassNote>();
    public List<CraftOreNote> ores = new List<CraftOreNote>();
    public List<string> elements = new List<string>();
    public List<CraftHeatNote> heats = new List<CraftHeatNote>();
    public List<CraftQuenchNote> quenches = new List<CraftQuenchNote>();
    public List<CraftDesignNote> designs = new List<CraftDesignNote>();
    public List<string> insights = new List<string>();
    public List<string> agents = new List<string>();
    public List<CraftExperiment> experiments = new List<CraftExperiment>();

    public const int MaxExperiments = 40;

    public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

    public static CraftJournalData FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new CraftJournalData();
        try
        {
            CraftJournalData d = JsonUtility.FromJson<CraftJournalData>(json) ?? new CraftJournalData();
            d.classes ??= new List<CraftClassNote>();
            d.ores ??= new List<CraftOreNote>();
            d.elements ??= new List<string>();
            d.heats ??= new List<CraftHeatNote>();
            d.quenches ??= new List<CraftQuenchNote>();
            d.designs ??= new List<CraftDesignNote>();
            d.insights ??= new List<string>();
            d.agents ??= new List<string>();
            d.experiments ??= new List<CraftExperiment>();
            return d;
        }
        catch (Exception)
        {
            return new CraftJournalData();
        }
    }
}

/// <summary>
/// How players learn: the rules of the journal. Working a metal often enough lets the smith recognise it (its colour,
/// its sparks, how it moves); an assay names it at once and shows what it is made of. Reading a thermometer while a
/// colour shows teaches what that colour means. Quenches are remembered by metal and medium (hardened, soft, cracked).
/// Mistakes and surprises become insights - burning steel, a quench crack, a case-hardened hide - each explained once,
/// as the smith would understand it. Finished items are recorded with the steps that made them.
/// </summary>
public static class CraftJournal
{
    /// <summary>How steady the hands are from practice (items made, insights had): 0 novice .. 1 master.</summary>
    public static double HandSkill(CraftJournalData d)
    {
        if (d == null) return 0;
        int made = 0;
        foreach (CraftDesignNote n in d.designs) made += n.timesMade;
        return CraftMath.Clamp01(made / 40.0 + d.insights.Count / 60.0);
    }

    // ------------------------------------------------------------------ insights
    /// <summary>The insights there are: id → what the player now understands.</summary>
    public static readonly Dictionary<string, string> Insights = new Dictionary<string, string>
    {
        ["burning"] = "Steel heated to a sparkling white burns: its grain falls apart and nothing brings it back. Forge below a yellow heat.",
        ["quench_crack"] = "A hard quench cracks high-carbon steel: the faster the medium, the higher the carbon, the thinner the edge and the more abrupt the shape, the likelier. Oil is gentler than water, water than brine.",
        ["untempered"] = "Steel straight from the quench is glass-hard and brittle. Heat it gently again (temper it) and it gives up a little hardness for a lot of toughness.",
        ["temper_colours"] = "On bright steel, heat paints colours: pale straw, straw, bronze, purple, blue. They tell the tempering heat.",
        ["magnet"] = "Steel stops sticking to a magnet at a cherry-red heat: that is the heat to harden it from.",
        ["cold_forging"] = "Forged too cold, steel tears and cracks. Iron and steel move at a red to yellow heat; bronze and copper move cold, but harden as they are worked until heat softens them again.",
        ["cast_iron"] = "Cast iron shatters under the hammer: it has too much carbon. It can be cast, not forged.",
        ["slag"] = "A bloom is spongy and full of slag: hammer it at welding heat until it closes up and the slag runs out, or it will crumble and hide flaws.",
        ["scale_pits"] = "Scale left on the metal is hammered into it as pits. Brush it off, or keep the fire reducing.",
        ["grind_heat"] = "Pressed too hard on the wheel, a thin edge overheats and loses its temper: it turns blue and goes soft. Cool it often, press lightly.",
        ["file_skate"] = "A file skates on hardened steel: file and shape before hardening.",
        ["wrong_flux"] = "Without flux, a forge weld does not take; without lime, a bloomery's slag eats the iron.",
        ["sulfur"] = "Iron with sulfur in it (from a coal fire, or an unroasted ore) tears at forging heat. Charcoal is cleaner; roast sulfide ores first.",
        ["phosphorus"] = "Iron from bog ore is hard but brittle in the cold: it is full of phosphorus.",
        ["cupel"] = "On a bone-ash cupel in a strong hot draft, lead soaks away and leaves the silver.",
        ["cementation"] = "Zinc boils away before copper melts; covered in the crucible with molten copper, its ore gives its zinc to the copper: brass.",
        ["case_hardened"] = "A tanning liquor too strong on a raw hide tans its outside fast and closes it: the core stays raw. Start weak, then strengthen.",
        ["rot"] = "A raw hide left wet rots: the hair slips, the grain spoils. Lime it, tan it, or dry it.",
        ["alum"] = "Alum-tawed leather is soft and pale, but water washes the tawing out.",
        ["shrink"] = "Wet leather shrinks and hardens in hot water: a little makes hard armour (cuir bouilli), too much ruins it.",
        ["stiff_leather"] = "Leather left to dry on its own dries stiff: work it (stake it) while it is still damp.",
        ["blotch"] = "Oiled leather will not take dye evenly: dye first, then oil.",
        ["warp"] = "A blade quenched unevenly - held still, moved side to side, or heated on one face - warps. Turn it in the fire and plunge it point first, moving it up and down.",
        ["normalise"] = "Steel held too long too hot grows a coarse, weak grain. Heating it just past the critical heat and letting it cool in the air refines it again.",
        ["loose_joint"] = "A well-made part badly fitted is a weak item: a loose guard rattles, a loose head flies off, a badly peened rivet lets go.",
    };

    public static bool HasInsight(CraftJournalData d, string id) => d != null && d.insights.Contains(id);

    /// <summary>Learns an insight (returns the line to tell the player, or null if already known).</summary>
    public static string Learn(CraftJournalData d, string id)
    {
        if (d == null || string.IsNullOrEmpty(id) || d.insights.Contains(id) || !Insights.TryGetValue(id, out string text))
            return null;
        d.insights.Add(id);
        return "Insight: " + text;
    }

    // ------------------------------------------------------------------ metals and ores
    public static CraftClassNote ClassNote(CraftJournalData d, string classId, bool create = false)
    {
        if (d == null || string.IsNullOrEmpty(classId))
            return null;
        foreach (CraftClassNote n in d.classes)
            if (n.classId == classId) return n;
        if (!create)
            return null;
        var c = new CraftClassNote { classId = classId };
        d.classes.Add(c);
        return c;
    }

    /// <summary>Does the player know this metal by name (iron and copper they always do)?</summary>
    public static bool KnowsClass(CraftJournalData d, CraftRules rules, CraftMaterialClass c)
    {
        if (c == null)
            return false;
        CraftClassNote n = ClassNote(d, c.id);
        if (n != null && n.identified)
            return true;
        CraftElement b = rules.Element(c.baseElement);
        return b != null && b.alwaysKnown && (c.ranges == null || c.ranges.Count == 0 || c.id == "copper_metal" || c.id == "wrought_iron");
    }

    /// <summary>The player worked a piece of this metal (it is recognised after enough work). Returns what was learnt.</summary>
    public static string Worked(CraftJournalData d, CraftRules rules, CraftAlloyProfile p)
    {
        if (d == null || p?.Class == null)
            return null;
        CraftClassNote n = ClassNote(d, p.Class.id, true);
        n.timesWorked++;
        if (!n.identified && n.timesWorked >= rules.Settings.workingsToRecognise)
        {
            n.identified = true;
            return $"You recognise it now: {p.Class.Name}.";
        }
        return null;
    }

    /// <summary>An assay: names the metal and its elements.</summary>
    public static string Assayed(CraftJournalData d, CraftRules rules, CraftAlloyProfile p)
    {
        if (d == null || p == null)
            return null;
        string line = null;
        if (p.Class != null)
        {
            CraftClassNote n = ClassNote(d, p.Class.id, true);
            if (!n.identified) line = $"The assay names it: {p.Class.Name}.";
            n.identified = true;
            n.assayed = true;
        }
        for (int i = 0; i < rules.ElementCount; i++)
            if (p.F(i) > 0.0005 && !d.elements.Contains(rules.Elements[i].id))
                d.elements.Add(rules.Elements[i].id);
        return line;
    }

    public static bool KnowsElement(CraftJournalData d, CraftRules rules, string id) =>
        rules.Element(id)?.alwaysKnown == true || d != null && d.elements.Contains(id);

    public static CraftOreNote OreNote(CraftJournalData d, string ore, bool create = false)
    {
        if (d == null || string.IsNullOrEmpty(ore))
            return null;
        foreach (CraftOreNote n in d.ores)
            if (n.oreId == ore) return n;
        if (!create)
            return null;
        var c = new CraftOreNote { oreId = ore };
        d.ores.Add(c);
        return c;
    }

    public static bool KnowsOre(CraftJournalData d, string ore) => OreNote(d, ore)?.identified == true;

    /// <summary>The player handled (cleaned) an ore; after smelting it once, it is known by name.</summary>
    public static string HandledOre(CraftJournalData d, CraftRules rules, string ore, bool smelted, int deposit = 0, string result = null)
    {
        CraftOreNote n = OreNote(d, ore, true);
        if (n == null)
            return null;
        if (smelted)
        {
            n.timesSmelted++;
            if (!string.IsNullOrEmpty(result))
            {
                string key = $"deposit {deposit & 0xffff:X4}: {result}";
                if (!n.deposits.Contains(key)) n.deposits.Add(key);
                if (n.deposits.Count > 12) n.deposits.RemoveAt(0);
            }
        }
        else
            n.timesHandled++;
        if (!n.identified && (n.timesSmelted >= 1 || n.timesHandled >= rules.Settings.workingsToRecognise * 2))
        {
            n.identified = true;
            return $"You know this ore now: {rules.Ore(ore)?.Name ?? ore}.";
        }
        return null;
    }

    // ------------------------------------------------------------------ heat
    public static CraftHeatNote HeatNote(CraftJournalData d, string color, bool create = false)
    {
        if (d == null || string.IsNullOrEmpty(color))
            return null;
        foreach (CraftHeatNote n in d.heats)
            if (n.color == color) return n;
        if (!create)
            return null;
        var h = new CraftHeatNote { color = color };
        d.heats.Add(h);
        return h;
    }

    /// <summary>The player read the thermometer while a colour showed.</summary>
    public static string ReadHeat(CraftJournalData d, CraftRules rules, string color, double celsius)
    {
        CraftHeatNote n = HeatNote(d, color, true);
        if (n == null)
            return null;
        n.readings++;
        n.min = Mathf.Min(n.min, (float)celsius);
        n.max = Mathf.Max(n.max, (float)celsius);
        return n.readings == rules.Settings.readingsToLearnHeat ? $"You know what {color} means now: about {n.min:0}-{n.max:0} °C." : null;
    }

    /// <summary>"cherry red (about 780-820 °C)" when learnt, else just the colour.</summary>
    public static string HeatWords(CraftJournalData d, CraftRules rules, string color)
    {
        CraftHeatNote n = HeatNote(d, color);
        return n != null && n.readings >= rules.Settings.readingsToLearnHeat ? $"{color} (about {Mathf.Round(n.min / 10) * 10:0}-{Mathf.Round(n.max / 10) * 10:0} °C)" : color;
    }

    // ------------------------------------------------------------------ quenches and items
    public static string Quenched(CraftJournalData d, CraftAlloyProfile p, string medium, bool hardened, bool cracked)
    {
        if (d == null || p?.Class == null)
            return null;
        CraftQuenchNote n = null;
        foreach (CraftQuenchNote q in d.quenches)
            if (q.classId == p.Class.id && q.medium == medium) n = q;
        if (n == null)
        {
            n = new CraftQuenchNote { classId = p.Class.id, medium = medium };
            d.quenches.Add(n);
        }
        n.times++;
        if (cracked) n.cracked++;
        else if (hardened) n.hardened++;
        else n.soft++;
        return cracked ? Learn(d, "quench_crack") : hardened ? Learn(d, "untempered") : null;
    }

    public static CraftDesignNote DesignNote(CraftJournalData d, string design)
    {
        if (d == null)
            return null;
        foreach (CraftDesignNote n in d.designs)
            if (n.designId == design) return n;
        return null;
    }

    /// <summary>A finished item: recorded with the steps of its parts (the best one is kept).</summary>
    public static List<string> Made(CraftJournalData d, CraftItemReport r, List<string> procedure, long utc)
    {
        var learnt = new List<string>();
        if (d == null || r?.Design == null)
            return learnt;
        CraftDesignNote n = DesignNote(d, r.Design.id);
        if (n == null)
        {
            n = new CraftDesignNote { designId = r.Design.id, name = r.Design.Name, firstUtc = utc };
            d.designs.Add(n);
            learnt.Add($"First {r.Design.Name.ToLowerInvariant()} made: {r.Name} ({r.Tier}).");
        }
        n.timesMade++;
        n.lastUtc = utc;
        if (r.Quality >= n.bestQuality)
        {
            if (n.timesMade > 1 && r.Quality > n.bestQuality + 0.02)
                learnt.Add($"Your best {r.Design.Name.ToLowerInvariant()} yet: {r.Tier}.");
            n.bestQuality = (float)r.Quality;
            n.bestTier = r.Tier;
            n.bestName = r.Name;
            n.bestProcedure = new List<string>(procedure ?? new List<string>());
        }
        var e = new CraftExperiment { utc = utc, name = r.Name, quality = (float)r.Quality, tier = r.Tier };
        e.notes.AddRange(r.Notes);
        if (procedure != null) e.procedure.AddRange(procedure);
        d.experiments.Add(e);
        if (d.experiments.Count > CraftJournalData.MaxExperiments)
            d.experiments.RemoveRange(0, d.experiments.Count - CraftJournalData.MaxExperiments);
        if (r.Assembly < 0.5)
        {
            string s = Learn(d, "loose_joint");
            if (s != null) learnt.Add(s);
        }
        return learnt;
    }

    /// <summary>The steps of a piece (and an assembly's parts), in the player's words with what they could know.</summary>
    public static List<string> Procedure(CraftWorkshop ws, CraftPiece p, CraftJournalData d)
    {
        var lines = new List<string>();
        if (p == null)
            return lines;
        void Add(CraftPiece q, string prefix)
        {
            foreach (CraftRecordEntry e in q.record)
            {
                string text = e.text;
                if (e.kind == CraftOpKind.Strike || e.kind == CraftOpKind.Grind || e.kind == CraftOpKind.File || e.kind == CraftOpKind.Sharpen || e.kind == CraftOpKind.Scrape)
                    text += e.count > 1 ? $" ×{e.count}" : "";
                if (e.kind == CraftOpKind.Strike && e.max > 0)
                    text += $", at {CraftMetallurgy.HeatName(e.min, false)} to {CraftMetallurgy.HeatName(e.max, false)}";
                lines.Add(prefix + text);
            }
        }
        if (p.kind == CraftPieceKind.Assembly && ws != null)
            foreach (CraftPiece c in ws.Components(p))
                Add(c, $"{CraftText.Capital(c.intentRole)}: ");
        Add(p, "");
        return lines;
    }

    /// <summary>What an event teaches (an insight to learn), if anything.</summary>
    public static string InsightOf(CraftEvent e, CraftPiece p)
    {
        switch (e.Kind)
        {
            case CraftEventKind.Burning: return "burning";
            case CraftEventKind.Cracked: return p != null && p.location == CraftLocation.Quench ? "quench_crack" : e.Text.Contains("crumbles") ? (p != null && p.bloom ? "slag" : "cast_iron") : "cold_forging";
            case CraftEventKind.Note:
                switch (e.Id)
                {
                    case "skate": return "file_skate";
                    case "blick": return "cupel";
                }
                return null;
            default: return null;
        }
    }
}
