using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A craft a player gets better at by practising it. Serialized as numbers: add at the END.</summary>
public enum CraftDiscipline { Smithing, Leatherwork, Woodwork, Tailoring, Jewellery, Alchemy }

/// <summary>How far a player has come in a craft (<see cref="CraftMastery.RankOf"/>).</summary>
public enum CraftRank { Novice, Apprentice, Journeyman, Expert, Master }

/// <summary>
/// Work a practised hand does without thinking: one action, at the quality the player's practice gives (never a
/// careful master's best). Serialized as numbers: add at the END.
/// </summary>
public enum CraftRoutine
{
    /// <summary>Winds a bobbin onto the niddy-noddy and counts it (Apprentice tailor).</summary>
    WindSkein,
    /// <summary>Takes the wearer's measurements (Apprentice tailor).</summary>
    MeasureWearer,
    /// <summary>Sizes the finger with the ring sizer (Apprentice jeweller).</summary>
    SizeFinger,
    /// <summary>Picks the burrs and seeds out of a batch of fibre (Journeyman tailor).</summary>
    PickFibre,
    /// <summary>Teases a batch's matted locks open (Journeyman tailor).</summary>
    TeaseFibre,
    /// <summary>Scrubs the crust off a rough stone with the brush (Journeyman jeweller).</summary>
    CleanGem,
    /// <summary>Searches a stone under the loupe, methodically, at every depth (Expert jeweller).</summary>
    SearchGem,
}

/// <summary>A player's practice in one craft (kept in the journal).</summary>
[Serializable]
public class CraftMasteryNote
{
    public CraftDiscipline discipline;
    [Tooltip("Experience: from finished work (more for better work) and, a little, from every piece of work done.")]
    public float xp;
    [Tooltip("Items, stones, lengths of cloth and potions finished in this craft.")]
    public int finished;
    // Practice from small work comes only so fast (what is left of the allowance, and when it was last taken).
    [NonSerialized] public double budget = -1, budgetAt;
}

/// <summary>How practice is earned and what it gives (on the Crafting Database: Simulation → Mastery).</summary>
[Serializable]
public class CraftMasterySettings
{
    [Tooltip("The experience for 63% mastery: mastery = 1 - e^(-experience / this). Every item teaches a little less than the last.")]
    [Min(1f)] public float experienceScale = 45f;
    [Tooltip("Experience for finishing an item: × (0.5 + its quality) × how much work the design is.")]
    [Min(0f)] public float perItem = 1.2f;
    [Tooltip("Experience from small work (blows, strokes, a bobbin spun) comes no faster than this a minute in one craft.")]
    [Min(0f)] public float workPerMinute = 0.35f;
    [Tooltip("Experience for each insight (understood, it is practised).")]
    [Min(0f)] public float perInsight = 0.5f;

    [Tooltip("The mastery each rank starts at: Apprentice, Journeyman, Expert, Master.")]
    [Range(0f, 1f)] public float apprentice = 0.15f;
    [Range(0f, 1f)] public float journeyman = 0.35f;
    [Range(0f, 1f)] public float expert = 0.55f;
    [Range(0f, 1f)] public float master = 0.78f;

    [Tooltip("How much less a master tires (fatigue × (1 - this) at full mastery).")]
    [Range(0f, 1f)] public float fatigueRelief = 0.4f;
    [Tooltip("A master's finishing touches: an item's quality gains this share of what it still lacks, at full mastery.")]
    [Range(0f, 0.5f)] public float finishingTouch = 0.12f;
    [Tooltip("The quality of work done by routine: at the rank that allows it, and at full mastery.")]
    [Range(0f, 1f)] public float routineLow = 0.55f;
    [Range(0f, 1f)] public float routineHigh = 0.9f;
}

/// <summary>
/// Practice, craft by craft. Every finished item (stone, length of cloth, potion) and, more slowly, every piece of work
/// done teaches the craft it belongs to - smithing, leatherwork, woodwork, tailoring, jewellery, alchemy - so a master
/// smith is still a novice at the loom. Mastery is <c>1 - e^(-experience / scale)</c>: quick at first, then each step
/// is harder won. Its ranks are Novice, Apprentice, Journeyman, Expert and Master.
/// <para>What practice gives, in its own craft: steadier hands (the tremble, slips, the window's pace, the timing bars'
/// zones), an arm that tires less, more done with each action (stitches a click, holes a beat, rows a beat, fewer
/// facets to cut one by one, a needleful that lasts longer), the tedious steps done <b>by routine</b> in one action
/// (<see cref="CraftRoutine"/>), and a master's finishing touches on what is made.</para>
/// </summary>
public static class CraftMastery
{
    public static readonly CraftDiscipline[] All =
        { CraftDiscipline.Smithing, CraftDiscipline.Leatherwork, CraftDiscipline.Woodwork, CraftDiscipline.Tailoring, CraftDiscipline.Jewellery, CraftDiscipline.Alchemy };
    private static readonly string[] Names = { "Smithing", "Leatherwork", "Woodwork", "Tailoring", "Jewellery", "Alchemy" };
    private static readonly string[] Crafters = { "smith", "leatherworker", "woodworker", "tailor", "jeweller", "alchemist" };
    private static readonly string[] Ranks = { "Novice", "Apprentice", "Journeyman", "Expert", "Master" };
    private static readonly CraftMasterySettings Defaults = new CraftMasterySettings();

    private static CraftMasterySettings S(CraftMasterySettings s) => s ?? Defaults;

    public static string Name(CraftDiscipline d) => Names[Mathf.Clamp((int)d, 0, Names.Length - 1)];
    public static string Crafter(CraftDiscipline d) => Crafters[Mathf.Clamp((int)d, 0, Crafters.Length - 1)];
    public static string RankName(CraftRank r) => Ranks[Mathf.Clamp((int)r, 0, Ranks.Length - 1)];

    // ------------------------------------------------------------------ levels and ranks
    /// <summary>Mastery (0-1) from experience.</summary>
    public static double Level(double xp, CraftMasterySettings s = null) => xp <= 0 ? 0 : 1 - Math.Exp(-xp / Math.Max(1, S(s).experienceScale));

    /// <summary>The experience a mastery takes.</summary>
    public static double ExperienceFor(double level, CraftMasterySettings s = null) =>
        level <= 0 ? 0 : -Math.Max(1, S(s).experienceScale) * Math.Log(Math.Max(1e-6, 1 - Math.Min(0.999999, level)));

    public static double RankStart(CraftRank r, CraftMasterySettings s = null)
    {
        s = S(s);
        switch (r)
        {
            case CraftRank.Apprentice: return s.apprentice;
            case CraftRank.Journeyman: return s.journeyman;
            case CraftRank.Expert: return s.expert;
            case CraftRank.Master: return s.master;
            default: return 0;
        }
    }

    public static CraftRank RankOf(double m, CraftMasterySettings s = null)
    {
        s = S(s);
        return m >= s.master ? CraftRank.Master : m >= s.expert ? CraftRank.Expert : m >= s.journeyman ? CraftRank.Journeyman : m >= s.apprentice ? CraftRank.Apprentice : CraftRank.Novice;
    }

    /// <summary>How far through its rank a mastery is (0-1; a master's is how near to complete mastery).</summary>
    public static double RankProgress(double m, CraftMasterySettings s = null)
    {
        CraftRank r = RankOf(m, s);
        double a = RankStart(r, s), b = r == CraftRank.Master ? 1 : RankStart(r + 1, s);
        return b > a ? CraftMath.Clamp01((m - a) / (b - a)) : 1;
    }

    /// <summary>"Journeyman tailor".</summary>
    public static string Title(CraftDiscipline d, double m, CraftMasterySettings s = null) => $"{RankName(RankOf(m, s))} {Crafter(d)}";

    // ------------------------------------------------------------------ the journal's notes
    public static CraftMasteryNote Note(List<CraftMasteryNote> notes, CraftDiscipline d, bool create = false)
    {
        if (notes == null)
            return null;
        foreach (CraftMasteryNote n in notes)
            if (n.discipline == d) return n;
        if (!create)
            return null;
        var c = new CraftMasteryNote { discipline = d };
        notes.Add(c);
        return c;
    }

    /// <summary>The player's mastery of a craft (0 novice - 1 complete).</summary>
    public static double Of(List<CraftMasteryNote> notes, CraftDiscipline d, CraftMasterySettings s = null) => Level(Note(notes, d)?.xp ?? 0, s);

    public static double Of(CraftJournalData j, CraftDiscipline d, CraftMasterySettings s = null) => j == null ? 0 : Of(j.mastery, d, s);

    /// <summary>
    /// Practice: <paramref name="xp"/> experience in <paramref name="d"/>. Small work (<paramref name="work"/>) comes no
    /// faster than the settings allow a minute (<paramref name="now"/>, seconds), so idle blows teach little; finished
    /// work always counts. Returns the line to tell the player when a new rank is reached, else null.
    /// </summary>
    public static string Practise(List<CraftMasteryNote> notes, CraftDiscipline d, double xp, bool work, double now, bool finishedOne = false, CraftMasterySettings s = null)
    {
        if (notes == null || !(xp > 0))
            return null;
        s = S(s);
        CraftMasteryNote n = Note(notes, d, true);
        if (work)
        {
            double cap = s.workPerMinute * 2;
            if (n.budget < 0) { n.budget = cap; n.budgetAt = now; }
            n.budget = Math.Min(cap, n.budget + Math.Max(0, now - n.budgetAt) / 60 * s.workPerMinute);
            n.budgetAt = now;
            xp = Math.Min(xp, n.budget);
            n.budget -= xp;
            if (!(xp > 0))
                return null;
        }
        CraftRank before = RankOf(Level(n.xp, s), s);
        n.xp += (float)xp;
        if (finishedOne) n.finished++;
        CraftRank after = RankOf(Level(n.xp, s), s);
        if (after <= before)
            return null;
        string what = string.Join("; ", Unlocked(d, after, s));
        return $"You are a {RankName(after).ToLowerInvariant()} {Crafter(d)} now." + (what.Length > 0 ? $" {what}." : "");
    }

    /// <summary>Experience for an insight, in its craft (understood, it is practised a little).</summary>
    public static double InsightExperience(CraftMasterySettings s = null) => S(s).perInsight;

    /// <summary>Experience for finishing an item of <paramref name="quality"/> (a design with more parts and joints is more work).</summary>
    public static double ItemExperience(CraftDesign d, double quality, CraftMasterySettings s = null)
    {
        int parts = d != null ? d.components.Count + d.joints.Count : 2;
        double work = CraftMath.Clamp(0.6 + 0.12 * parts, 0.8, 2);
        return S(s).perItem * (0.5 + CraftMath.Clamp01(quality)) * work;
    }

    // ------------------------------------------------------------------ what practice gives
    /// <summary>How much the arm tires with a movement, × this (a master's movements are sure and economical).</summary>
    public static double FatigueFactor(double m, CraftMasterySettings s = null) => 1 - S(s).fatigueRelief * CraftMath.Clamp01(m);

    /// <summary>A master's finishing touches: the quality an item of <paramref name="quality"/> ends with.</summary>
    public static double FinishingTouch(double quality, double m, CraftMasterySettings s = null) =>
        CraftMath.Clamp01(quality + (1 - CraftMath.Clamp01(quality)) * S(s).finishingTouch * CraftMath.Clamp01(m));

    /// <summary>Stitches laid with each click of the needle or beat of the awl: 1, 2 from Journeyman, 3 for a master.</summary>
    public static int StitchesPerAction(double m, CraftMasterySettings s = null) => m >= S(s).master ? 3 : m >= S(s).journeyman ? 2 : 1;

    /// <summary>Holes a pricking iron punches with each beat (its teeth).</summary>
    public const int HolesPerBeat = 4;

    /// <summary>Rows of weft thrown and beaten in with each beat of the reed: 1, 2 from Journeyman, 3 for a master.</summary>
    public static int RowsPerBeat(double m, CraftMasterySettings s = null) => m >= S(s).master ? 3 : m >= S(s).journeyman ? 2 : 1;

    /// <summary>Stitches a needleful of thread lasts (a practised hand wastes less of it).</summary>
    public static int NeedlefulStitches(double m) => 40 + (int)Math.Round(40 * CraftMath.Clamp01(m));

    /// <summary>Facets of a tier cut on the timing bar one by one (the rest follow the same setting): 6 for a novice, 3 for a master.</summary>
    public static int FacetHits(int facets, double m, CraftMasterySettings s = null)
    {
        s = S(s);
        int most = m >= s.master ? 3 : m >= s.expert ? 4 : m >= s.journeyman ? 5 : 6;
        return Math.Max(1, Math.Min(facets, most));
    }

    /// <summary>The rank a routine needs.</summary>
    public static CraftRank RoutineRank(CraftRoutine r)
    {
        switch (r)
        {
            case CraftRoutine.WindSkein:
            case CraftRoutine.MeasureWearer:
            case CraftRoutine.SizeFinger: return CraftRank.Apprentice;
            case CraftRoutine.SearchGem: return CraftRank.Expert;
            default: return CraftRank.Journeyman;
        }
    }

    /// <summary>The craft a routine belongs to.</summary>
    public static CraftDiscipline RoutineDiscipline(CraftRoutine r) =>
        r == CraftRoutine.SizeFinger || r == CraftRoutine.CleanGem || r == CraftRoutine.SearchGem ? CraftDiscipline.Jewellery : CraftDiscipline.Tailoring;

    public static bool CanRoutine(CraftRoutine r, double m, CraftMasterySettings s = null) => m >= RankStart(RoutineRank(r), s) - 1e-9;

    /// <summary>How well a routine is done at mastery <paramref name="m"/> (0-1): fair at its rank, good for a master.</summary>
    public static double RoutineQuality(CraftRoutine r, double m, CraftMasterySettings s = null)
    {
        s = S(s);
        double from = RankStart(RoutineRank(r), s), t = from < 1 ? CraftMath.Clamp01((m - from) / (1 - from)) : 1;
        return s.routineLow + (s.routineHigh - s.routineLow) * t;
    }

    public static string RoutineName(CraftRoutine r)
    {
        switch (r)
        {
            case CraftRoutine.WindSkein: return "wind and count a skein";
            case CraftRoutine.MeasureWearer: return "measure the wearer";
            case CraftRoutine.SizeFinger: return "size a finger";
            case CraftRoutine.PickFibre: return "pick fibre clean";
            case CraftRoutine.TeaseFibre: return "tease fibre open";
            case CraftRoutine.CleanGem: return "clean a rough stone";
            case CraftRoutine.SearchGem: return "search a stone under the loupe";
            default: return r.ToString();
        }
    }

    /// <summary>What a rank newly allows in a craft (for the line that announces it).</summary>
    public static List<string> Unlocked(CraftDiscipline d, CraftRank rank, CraftMasterySettings s = null)
    {
        var lines = new List<string>();
        foreach (CraftRoutine r in Enum.GetValues(typeof(CraftRoutine)))
            if (RoutineDiscipline(r) == d && RoutineRank(r) == rank)
                lines.Add($"you can {RoutineName(r)} by routine");
        if (rank == CraftRank.Journeyman || rank == CraftRank.Master)
        {
            if (d == CraftDiscipline.Tailoring) lines.Add($"{StitchesPerAction(RankStart(rank, s), s)} stitches a click, {RowsPerBeat(RankStart(rank, s), s)} rows a beat");
            if (d == CraftDiscipline.Leatherwork) lines.Add($"{StitchesPerAction(RankStart(rank, s), s)} stitches a beat of the awl");
        }
        if (d == CraftDiscipline.Jewellery && rank >= CraftRank.Journeyman)
            lines.Add($"{FacetHits(99, RankStart(rank, s), s)} facets of a tier cut by hand, the rest following");
        return lines;
    }

    /// <summary>Everything practice gives now in a craft, for the journal.</summary>
    public static List<string> Benefits(CraftDiscipline d, double m, CraftMasterySettings s = null)
    {
        s = S(s);
        var lines = new List<string>();
        if (d != CraftDiscipline.Alchemy)
        {
            lines.Add($"Hands {Math.Round(60 * m)}% steadier; the timing bars' zones {Math.Round(100 * m * 0.35)}% wider");
            lines.Add($"The arm tires {Math.Round(100 * (1 - FatigueFactor(m, s)))}% less");
        }
        else
            lines.Add($"A steadier pestle, spoon and jug: {Math.Round(35 * m)}% more even");
        if (m > 0.005)
            lines.Add($"Finishing touches: +{Math.Round(100 * s.finishingTouch * m)}% of the quality a piece still lacks");
        switch (d)
        {
            case CraftDiscipline.Tailoring:
                lines.Add($"{StitchesPerAction(m, s)} stitch{(StitchesPerAction(m, s) == 1 ? "" : "es")} a click; a needleful lasts {NeedlefulStitches(m)} stitches; {RowsPerBeat(m, s)} row{(RowsPerBeat(m, s) == 1 ? "" : "s")} a beat at the loom");
                break;
            case CraftDiscipline.Leatherwork:
                lines.Add($"The pricking iron punches {HolesPerBeat} holes a beat; {StitchesPerAction(m, s)} stitch{(StitchesPerAction(m, s) == 1 ? "" : "es")} a beat of the awl");
                break;
            case CraftDiscipline.Jewellery:
                lines.Add($"{FacetHits(99, m, s)} facets of a tier cut by hand, the rest following the same setting");
                break;
            case CraftDiscipline.Alchemy:
                if (RankOf(m, s) >= CraftRank.Journeyman) lines.Add("You can tell what a brew you have made before is becoming");
                if (RankOf(m, s) >= CraftRank.Expert) lines.Add("... and roughly how good it will be");
                break;
        }
        var routines = new List<string>();
        foreach (CraftRoutine r in Enum.GetValues(typeof(CraftRoutine)))
            if (RoutineDiscipline(r) == d && CanRoutine(r, m, s)) routines.Add(RoutineName(r));
        if (routines.Count > 0) lines.Add("By routine: " + string.Join(", ", routines));
        return lines;
    }

    // ------------------------------------------------------------------ doing more with each action
    /// <summary>
    /// What a practised hand does with one action, worked out where the rules run from the player's practice (the
    /// request only says which tool it was): the needle (JointWork <c>i1</c> = 1) lays a run of stitches and pierces as
    /// many holes; the pricking iron (<c>i1</c> = 2) punches its teeth's holes a beat and the awl a run of stitches; the
    /// reed (WeaveCloth) beats in rows together.
    /// </summary>
    public static void ByPractice(ref CraftRequest r, double m, CraftMasterySettings s = null)
    {
        if (r.kind == CraftRequestKind.JointWork && (r.i1 == 1 || r.i1 == 2))
        {
            var work = (CraftJointWork)r.i0;
            if (work == CraftJointWork.Punch) r.count *= r.i1 == 2 ? HolesPerBeat : StitchesPerAction(m, s);
            else if (work == CraftJointWork.Stitch) r.count *= StitchesPerAction(m, s);
            r.i1 = 0;
        }
        else if (r.kind == CraftRequestKind.WeaveCloth && r.points != null)
        {
            int per = RowsPerBeat(m, s), beats = Math.Min(r.points.Count, Math.Max(0, r.i1));
            if (per <= 1)
                return;
            var rows = new List<float>();
            for (int k = 0; k < beats && rows.Count < 30; k++)
                for (int j = 0; j < per && rows.Count < 30; j++) rows.Add(r.points[k]);
            r.points = rows;
            r.i1 = rows.Count;
        }
    }

    /// <summary>
    /// Does <paramref name="routine"/> to <paramref name="piece"/> as well as practice <paramref name="m"/> does it (the
    /// caller checks the rank: <see cref="CanRoutine"/>). <paramref name="wearer"/> is whose body or finger is measured.
    /// </summary>
    public static bool DoRoutine(CraftWorkshop w, CraftRoutine routine, double m, string piece, string wearer, CraftMasterySettings s, out string reason)
    {
        reason = "";
        CraftPiece p = w.Piece(piece);
        // as well as practice does it, a little better or worse from one time to the next
        double q = CraftMath.Clamp01(RoutineQuality(routine, m, s) + (CraftMath.Noise(CraftMath.Hash(piece ?? "routine"), (int)w.Step, (int)routine + 3) - 0.5) * 0.06);
        switch (routine)
        {
            case CraftRoutine.WindSkein:
                if (p == null || p.kind != CraftPieceKind.Yarn) return Fail("Pick a bobbin of yarn.", out reason);
                // wound and counted: a short count now and then, never more than is there
                return w.MeasureYarn(piece, p.yarn.length * (1 - (1 - q) * 0.08), out reason);
            case CraftRoutine.MeasureWearer:
                return w.MeasureFit(p != null && p.kind == CraftPieceKind.Assembly ? p.id : null, q, CraftTextiles.ChestOf(wearer) + (1 - q) * 60, out reason);
            case CraftRoutine.SizeFinger:
                return w.MeasureFit(p != null && p.kind == CraftPieceKind.Assembly ? p.id : null, q, CraftJewelry.FingerOf(wearer) + (1 - q) * 0.6, out reason);
            case CraftRoutine.PickFibre:
                return w.CleanFibre(piece, q, 0.1 * (1 - q), out reason);
            case CraftRoutine.TeaseFibre:
                return w.OpenFibre(piece, q, 0.3 * (1 - q), out reason);
            case CraftRoutine.CleanGem:
            {
                if (p == null || p.kind != CraftPieceKind.Gem) return Fail("Pick a rough stone.", out reason);
                // the brush, sector by sector, as long as each needs
                var work = new List<float>();
                double rate = 0.35 * w.ToolQuality * (0.6 + 0.4 * q);
                for (int k = 0; k < CraftLapidary.CrustSectors; k++)
                    work.Add((float)Math.Min(3, (k < p.gem.crust.Count ? p.gem.crust[k] : 0) / Math.Max(0.05, rate) + 0.05));
                return w.CleanGem(piece, false, work, q, out reason);
            }
            case CraftRoutine.SearchGem:
            {
                if (p == null || p.kind != CraftPieceKind.Gem) return Fail("Pick a stone.", out reason);
                // methodically, at every depth: the finer the practice, the closer the looks
                var looks = new List<float>();
                double step = 0.26 - 0.1 * q;
                for (double z = 0.05; z < 1; z += 0.15)
                    for (double y = -0.9; y <= 0.9 + 1e-9; y += step)
                        for (double x = -0.9; x <= 0.9 + 1e-9; x += step)
                        {
                            looks.Add((float)x); looks.Add((float)y); looks.Add((float)z);
                        }
                return w.EvaluateGem(piece, looks, q, out reason);
            }
        }
        return Fail("There is no such work.", out reason);
    }

    private static bool Fail(string why, out string reason)
    {
        reason = why;
        return false;
    }

    // ------------------------------------------------------------------ which craft
    /// <summary>The craft an item belongs to: jewellery, or the craft of the parts that matter most in it.</summary>
    public static CraftDiscipline OfDesign(CraftDesign d)
    {
        if (d == null)
            return CraftDiscipline.Smithing;
        if (d.kind == CraftDesignKind.Jewelry)
            return CraftDiscipline.Jewellery;
        var weight = new double[All.Length];
        foreach (CraftComponentSpec c in d.components)
        {
            double w = Math.Max(0.1, c.importance) * Math.Max(1, c.count);
            switch (c.kind)
            {
                case CraftPieceKind.Metal: case CraftPieceKind.Fasteners: weight[(int)CraftDiscipline.Smithing] += w; break;
                case CraftPieceKind.Wood: weight[(int)CraftDiscipline.Woodwork] += w; break;
                case CraftPieceKind.Gem: weight[(int)CraftDiscipline.Jewellery] += w; break;
                case CraftPieceKind.Hide:
                    weight[(int)(c.leather != null && c.leather.fabric == CraftFabric.Cloth ? CraftDiscipline.Tailoring : CraftDiscipline.Leatherwork)] += w;
                    break;
            }
        }
        int best = 0;
        for (int i = 1; i < weight.Length; i++)
            if (weight[i] > weight[best]) best = i;
        return (CraftDiscipline)best;
    }

    /// <summary>
    /// The craft a request practises (null: none - the bellows, putting things down, waiting). Work on an item being
    /// assembled is the item's craft; cutting cloth is tailoring and cutting leather leatherwork; rasping wood woodwork.
    /// </summary>
    public static CraftDiscipline? OfRequest(CraftRules rules, CraftWorkshop ws, in CraftRequest r)
    {
        CraftPiece p = ws?.Piece(r.piece);
        switch (r.kind)
        {
            case CraftRequestKind.Strike: case CraftRequestKind.Fold: case CraftRequestKind.Weld: case CraftRequestKind.Split:
            case CraftRequestKind.FormFastener: case CraftRequestKind.Agitate: case CraftRequestKind.Clay: case CraftRequestKind.Descale:
            case CraftRequestKind.Coat: case CraftRequestKind.Sharpen: case CraftRequestKind.Crush: case CraftRequestKind.Wash:
            case CraftRequestKind.Sieve: case CraftRequestKind.Flux: case CraftRequestKind.TapSlag: case CraftRequestKind.ExtractBloom:
            case CraftRequestKind.StirMelt: case CraftRequestKind.Skim: case CraftRequestKind.Pour: case CraftRequestKind.BrushOre:
                return CraftDiscipline.Smithing;
            case CraftRequestKind.Grind: case CraftRequestKind.File: case CraftRequestKind.Polish:
                return p != null && p.kind == CraftPieceKind.Wood ? CraftDiscipline.Woodwork : CraftDiscipline.Smithing;
            case CraftRequestKind.FillVat: case CraftRequestKind.StirVat: case CraftRequestKind.Scrape: case CraftRequestKind.Stretch:
            case CraftRequestKind.Stake: case CraftRequestKind.Condition: case CraftRequestKind.Dye: case CraftRequestKind.Slick:
            case CraftRequestKind.Skive:
                return CraftDiscipline.Leatherwork;
            case CraftRequestKind.Cut:
                return rules != null && CraftTextiles.IsCloth(rules, p) ? CraftDiscipline.Tailoring : CraftDiscipline.Leatherwork;
            case CraftRequestKind.Carve: case CraftRequestKind.Rive: case CraftRequestKind.BendWood: case CraftRequestKind.SawCut:
            case CraftRequestKind.StackWood: case CraftRequestKind.SandWood: case CraftRequestKind.Varnish: case CraftRequestKind.DebarkWood:
            case CraftRequestKind.SplitLog:
                return CraftDiscipline.Woodwork;
            case CraftRequestKind.Spin: case CraftRequestKind.CleanFibre: case CraftRequestKind.ScourFibre: case CraftRequestKind.OpenFibre:
            case CraftRequestKind.CardFibre: case CraftRequestKind.CombFibre: case CraftRequestKind.SpinFibre: case CraftRequestKind.PlyYarn:
            case CraftRequestKind.MeasureYarn: case CraftRequestKind.ReelYarn: case CraftRequestKind.LayoutPattern: case CraftRequestKind.WeaveCloth:
                return CraftDiscipline.Tailoring;
            case CraftRequestKind.TestGem: case CraftRequestKind.IdentifyGem: case CraftRequestKind.EvaluateGem: case CraftRequestKind.OrientGem:
            case CraftRequestKind.CleaveGem: case CraftRequestKind.SawGem: case CraftRequestKind.FacetGem: case CraftRequestKind.PolishGem:
            case CraftRequestKind.FormShank: case CraftRequestKind.SeatSetting: case CraftRequestKind.FinishJewel: case CraftRequestKind.CleanGem:
            case CraftRequestKind.GrindGem:
                return CraftDiscipline.Jewellery;
            case CraftRequestKind.Routine:
                return RoutineDiscipline((CraftRoutine)r.i0);
            case CraftRequestKind.MeasureFit:
                // the ring sizer measures a finger (a few mm), the tape a body
                return r.b > 0 && r.b < 60 ? CraftDiscipline.Jewellery : CraftDiscipline.Tailoring;
            case CraftRequestKind.StartAssembly:
                return OfDesign(rules?.Design(r.id));
            case CraftRequestKind.Attach: case CraftRequestKind.Detach: case CraftRequestKind.JointWork: case CraftRequestKind.Finish:
            case CraftRequestKind.Inspect:
                return p != null && p.kind == CraftPieceKind.Assembly ? OfDesign(rules?.Design(p.assembly.design)) : (CraftDiscipline?)null;
            default:
                return null;
        }
    }

    /// <summary>
    /// The experience a piece of work gives (before the minute's allowance): products more than single blows or strokes;
    /// nothing for work done by routine, for fuelling a fire or moving things about.
    /// </summary>
    public static double WorkExperience(in CraftRequest r)
    {
        switch (r.kind)
        {
            case CraftRequestKind.Strike: case CraftRequestKind.Scrape: case CraftRequestKind.Carve: case CraftRequestKind.Grind:
            case CraftRequestKind.Sharpen: case CraftRequestKind.File: case CraftRequestKind.SandWood: case CraftRequestKind.GrindGem:
            case CraftRequestKind.JointWork: case CraftRequestKind.CardFibre: case CraftRequestKind.Stake: case CraftRequestKind.Slick:
            case CraftRequestKind.Skive: case CraftRequestKind.Polish: case CraftRequestKind.PolishGem: case CraftRequestKind.CleanGem:
            case CraftRequestKind.BrushOre: case CraftRequestKind.Crush:
                return 0.01;
            case CraftRequestKind.Spin: case CraftRequestKind.SpinFibre: case CraftRequestKind.Pour: case CraftRequestKind.ExtractBloom:
            case CraftRequestKind.WeaveCloth:
                return 0.25;
            case CraftRequestKind.PlyYarn: case CraftRequestKind.Cut: case CraftRequestKind.Weld: case CraftRequestKind.BendWood:
            case CraftRequestKind.FormShank: case CraftRequestKind.SeatSetting: case CraftRequestKind.FacetGem: case CraftRequestKind.IdentifyGem:
                return 0.12;
            case CraftRequestKind.Routine: case CraftRequestKind.PutItem: case CraftRequestKind.TakePiece: case CraftRequestKind.Move:
            case CraftRequestKind.AddFuel: case CraftRequestKind.Bellows: case CraftRequestKind.Damper: case CraftRequestKind.Oven:
            case CraftRequestKind.Wait: case CraftRequestKind.Suspend: case CraftRequestKind.Resume: case CraftRequestKind.Inspect:
            case CraftRequestKind.Finish: case CraftRequestKind.StartAssembly: case CraftRequestKind.Detach: case CraftRequestKind.Turn:
            case CraftRequestKind.SetIntent: case CraftRequestKind.ScrapPiece: case CraftRequestKind.Assay: case CraftRequestKind.TakeMelt:
            case CraftRequestKind.DiscardFragment: case CraftRequestKind.DrainVat: case CraftRequestKind.VatHeat:
                return 0;
            default:
                return 0.04;
        }
    }

    // ------------------------------------------------------------------ an older journal
    /// <summary>The craft an insight was learnt in.</summary>
    public static CraftDiscipline OfInsight(string id)
    {
        switch (id)
        {
            case "case_hardened": case "rot": case "alum": case "shrink": case "stiff_leather": case "blotch": case "loose_joint":
                return CraftDiscipline.Leatherwork;
            case "warp": case "drawknife": case "grits":
                return CraftDiscipline.Woodwork;
            case "felting": case "carding": case "weaving_beat":
                return CraftDiscipline.Tailoring;
            case "cleavage": case "table_cleavage": case "heat_gem": case "polish_compound": case "pavilion_angle": case "seat_fit":
            case "clean_rough": case "preform_grind": case "overpolish":
                return CraftDiscipline.Jewellery;
            default:
                return CraftDiscipline.Smithing;
        }
    }

    /// <summary>
    /// A journal kept before practice was counted craft by craft: what it records (the items made, the insights had, the
    /// gems known) is turned into experience once.
    /// </summary>
    public static void Seed(CraftJournalData j, CraftRules rules, CraftMasterySettings s = null)
    {
        if (j == null || j.masterySeeded)
            return;
        j.masterySeeded = true;
        s = S(s);
        foreach (CraftDesignNote n in j.designs)
        {
            CraftDesign d = rules?.Design(n.designId);
            CraftMasteryNote m = Note(j.mastery, OfDesign(d), true);
            m.xp += (float)(ItemExperience(d, n.bestQuality * 0.85, s) * n.timesMade);
            m.finished += n.timesMade;
        }
        foreach (string insight in j.insights)
            Note(j.mastery, OfInsight(insight), true).xp += (float)InsightExperience(s);
        if (j.gems.Count > 0)
            Note(j.mastery, CraftDiscipline.Jewellery, true).xp += 0.3f * j.gems.Count;
    }
}
