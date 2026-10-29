using System;
using System.Collections.Generic;

/// <summary>
/// The hands-on work a harvest is made of. Each is a small game measured by a model (<see cref="HarvestStageModel"/>):
/// the same seven are combined differently for every material. Serialized as numbers: add at the END.
/// </summary>
public enum HarvestStageKind
{
    /// <summary>Draw the knife along a line (opening cuts, seams between muscles): stay on it, keep the speed down where it is hard.</summary>
    Trace,
    /// <summary>Separate a hide from the flesh: pull it back, keeping the pull in the band, while the knife frees it.</summary>
    Peel,
    /// <summary>Cut through small attachments with the knife tip held still on each: don't let it wander into what must not be cut.</summary>
    Steady,
    /// <summary>Draw something out (a tendon, an organ, a tooth): keep the pull in the band as it catches and gives.</summary>
    Pull,
    /// <summary>Saw through bone or horn: even strokes along the line, in rhythm.</summary>
    Saw,
    /// <summary>Work a claw, tooth or scale loose: rock it side to side until it gives, without straining it until it cracks.</summary>
    Pry,
    /// <summary>Scrape a bone (or pluck a bird) clean: cover it all without gouging what is already bare.</summary>
    Scrape,
}

/// <summary>How one stage went, as its model measured it. Sent to the server, which bounds it and works out the pieces.</summary>
[Serializable]
public class HarvestStageResult
{
    public HarvestStageKind kind;
    /// <summary>The piece this stage was for (countable materials), -1 = the whole material.</summary>
    public int unit = -1;
    /// <summary>How well it was done (0 clumsy .. 1 perfect).</summary>
    public float precision;
    /// <summary>Harm done to the piece (0 none .. 1 ruined): tears, nicks, cracks, splinters.</summary>
    public float damage;
    /// <summary>How much of it came away (0-1): the rest is wasted.</summary>
    public float completeness = 1f;
    /// <summary>Seconds it took.</summary>
    public float seconds;
    /// <summary>What it left: Nicked, Ripped, Contaminated, Splintered, Cracked, Rough.</summary>
    public HarvestMarks marks;
    /// <summary>Mistakes made (slips, tears, punctures...).</summary>
    public int mistakes;
    /// <summary>A gut, bladder or gall cut open while working (0-1).</summary>
    public float spill;
    /// <summary>The tool used (its item id; empty = bare hands) and how fit it was.</summary>
    public string tool = "";
    public float toolFitness;
    /// <summary>Skinning: the harvest damage of every hide cell (row by row), the flesh left on it, and the cells lost.</summary>
    public List<float> cellDamage;
    public List<float> cellFlesh;
    public List<int> lostCells;

    public HarvestStageResult Clone()
    {
        var c = (HarvestStageResult)MemberwiseClone();
        c.cellDamage = cellDamage != null ? new List<float>(cellDamage) : null;
        c.cellFlesh = cellFlesh != null ? new List<float>(cellFlesh) : null;
        c.lostCells = lostCells != null ? new List<int>(lostCells) : null;
        return c;
    }
}

/// <summary>A whole harvest of one material: which pieces were worked and how each stage went.</summary>
[Serializable]
public class HarvestTaskResult
{
    /// <summary>The material's id.</summary>
    public string material = "";
    /// <summary>Countable materials: the pieces worked (their index); empty = every piece left (or the bulk).</summary>
    public List<int> units = new List<int>();
    public List<HarvestStageResult> stages = new List<HarvestStageResult>();
    /// <summary>Taken roughly in a hurry (the quick strip of a hide): no minigame, little of value.</summary>
    public bool rough;

    public float Seconds
    {
        get
        {
            float s = 0f;
            foreach (HarvestStageResult r in stages) if (r != null) s += r.seconds;
            return s;
        }
    }

    /// <summary>How well the work went overall (0-1): precision less damage, over every stage.</summary>
    public double Performance
    {
        get
        {
            if (stages == null || stages.Count == 0)
                return rough ? 0.2 : 0;
            double sum = 0;
            foreach (HarvestStageResult r in stages)
                if (r != null) sum += HarvestMath.Clamp01(r.precision) * (1 - 0.6 * HarvestMath.Clamp01(r.damage)) * (0.5 + 0.5 * HarvestMath.Clamp01(r.completeness));
            return sum / stages.Count;
        }
    }

    public HarvestTaskResult Clone()
    {
        var c = new HarvestTaskResult { material = material, units = new List<int>(units ?? new List<int>()), rough = rough };
        if (stages != null)
            foreach (HarvestStageResult r in stages)
                if (r != null) c.stages.Add(r.Clone());
        return c;
    }
}

/// <summary>One kind of piece a harvest gives: the material, which piece (-1 for a bulk yield), how many, and how good.</summary>
[Serializable]
public struct HarvestPiece
{
    public int material;
    public int unit;
    public int count;
    public float quality;
    public HarvestMarks marks;

    public HarvestPiece(int material, int unit, int count, float quality, HarvestMarks marks)
    {
        this.material = material;
        this.unit = unit;
        this.count = count;
        this.quality = quality;
        this.marks = marks;
    }

    public HarvestGrade Grade => HarvestText.Grade(quality);
}

/// <summary>What a harvest gave and taught, and what it changed on the carcass.</summary>
public sealed class HarvestOutcome
{
    public readonly List<HarvestPiece> pieces = new List<HarvestPiece>();
    /// <summary>What happened, in words, for the player.</summary>
    public readonly List<string> lines = new List<string>();
    /// <summary>Pieces thrown away (ruined).</summary>
    public int discarded;
    public HarvestSkill skill;
    public float xp;
    public float anatomyXp;
    /// <summary>How well it went (0-1).</summary>
    public double performance;
    /// <summary>Skinning: the hide as it came off (its cells), for the tannery.</summary>
    public HarvestCondition hideSource;
    public bool accepted = true;
    public string refusal = "";

    public static HarvestOutcome Refused(string why) => new HarvestOutcome { accepted = false, refusal = why ?? "" };

    public int Count
    {
        get
        {
            int n = 0;
            foreach (HarvestPiece p in pieces) n += p.count;
            return n;
        }
    }
}

/// <summary>One kind of piece a harvest gave, as the player is shown it.</summary>
[Serializable]
public class HarvestReportPiece
{
    public string name = "";
    public int count;
    public float quality;
    /// <summary>Its <see cref="HarvestMarks"/>.</summary>
    public int marks;
    /// <summary>The item given (its id), for its icon.</summary>
    public string item = "";
    /// <summary>It became a hide for the tannery (a worked piece of the crafting system).</summary>
    public bool tanneryHide;

    public HarvestGrade Grade => HarvestText.Grade(quality);
}

/// <summary>
/// What a harvest gave and taught a player, in words and numbers - or why it was refused. Made where the rules run and
/// sent to the player's window.
/// </summary>
[Serializable]
public class HarvestReport
{
    public string material = "";
    public string materialName = "";
    public bool accepted = true;
    public string refusal = "";
    public List<HarvestReportPiece> pieces = new List<HarvestReportPiece>();
    /// <summary>What happened ("The bear pelt comes off: 94% of it whole, 2 holes.").</summary>
    public List<string> lines = new List<string>();
    /// <summary>Levels and techniques gained.</summary>
    public List<string> learnt = new List<string>();
    /// <summary>The skill trained (<see cref="HarvestSkill"/>) and the experience given to it and to Anatomy.</summary>
    public int skill;
    public float xp;
    public float anatomyXp;
    public float performance;
    public int discarded;
    /// <summary>Some of the work was done faster than careful hands can do it (it counted as rushed).</summary>
    public bool rushed;

    public static HarvestReport Refused(string material, string materialName, string why) =>
        new HarvestReport { material = material ?? "", materialName = materialName ?? "", accepted = false, refusal = why ?? "" };

    public int Count
    {
        get
        {
            int n = 0;
            foreach (HarvestReportPiece p in pieces) n += p.count;
            return n;
        }
    }

    public string ToJson() => UnityEngine.JsonUtility.ToJson(this);

    public static HarvestReport FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            HarvestReport r = UnityEngine.JsonUtility.FromJson<HarvestReport>(json);
            if (r == null) return null;
            r.pieces ??= new List<HarvestReportPiece>();
            r.lines ??= new List<string>();
            r.learnt ??= new List<string>();
            return r;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
