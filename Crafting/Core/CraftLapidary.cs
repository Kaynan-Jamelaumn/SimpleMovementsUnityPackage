using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How far a gemstone has come. Serialized as numbers: add at the END.</summary>
public enum CraftGemStage
{
    /// <summary>As it came out of the ground: a crystal or a waterworn pebble.</summary>
    Rough,
    /// <summary>The excess rough sawn or cleaved away: a preform ready for the faceting machine.</summary>
    Preformed,
    /// <summary>Every tier of facets (or the dome of a cabochon) ground.</summary>
    Faceted,
    /// <summary>Polished: ready to be set.</summary>
    Polished,
}

/// <summary>What is inside a stone. Serialized as numbers: add at the END.</summary>
public enum CraftInclusionKind
{
    /// <summary>A crystal of another mineral caught inside.</summary>
    Crystal,
    /// <summary>A feather: a crack inside the stone (it weakens it, and can run when it is knocked or heated).</summary>
    Feather,
    /// <summary>A cloud of pinpoints that dulls the light.</summary>
    Cloud,
    /// <summary>Needles (silk): fine rutile needles - they soften the colour, and line up into a star in a cabochon.</summary>
    Needle,
}

/// <summary>A test made to tell what a stone is. Serialized as numbers: add at the END.</summary>
public enum CraftGemTest
{
    /// <summary>Looked at under the loupe: colour, lustre, transparency, the cleavage cracks.</summary>
    Loupe,
    /// <summary>Scratched with a hardness point (op <c>b</c> = the point's hardness): does it scratch, or is it scratched?</summary>
    Scratch,
    /// <summary>Its refractive index read on the refractometer (best on a polished face).</summary>
    Refractometer,
    /// <summary>Weighed in air and in water on the hydrostatic balance: its specific gravity.</summary>
    Gravity,
    /// <summary>Looked through the dichroscope: does its colour change with direction, and which way is its axis?</summary>
    Dichroscope,
}

/// <summary>Something inside a stone, where it is in the stone's plan (-1..1) and depth (0 table - 1 culet).</summary>
[Serializable]
public class CraftInclusion
{
    public CraftInclusionKind kind;
    public float x, y, depth, size;
    [Tooltip("Seen under the loupe during the evaluation.")]
    public bool found;

    public CraftInclusion Clone() => (CraftInclusion)MemberwiseClone();
}

/// <summary>A sawn or cleaved face: the stone keeps the points where <c>nx·x + ny·y ≤ offset</c>.</summary>
[Serializable]
public class CraftGemPlane
{
    public float nx, ny, offset;
    [Tooltip("Cleaved (a clean crystal face) rather than sawn.")]
    public bool cleaved;
}

/// <summary>One tier of facets as it was cut: the angle's error, how true the index was, how well the facets met.</summary>
[Serializable]
public class CraftFacetRecord
{
    public int tier;
    public int facets;
    [Tooltip("Degrees off the ideal angle (signed: + steeper, - shallower).")]
    public double angleError;
    [Tooltip("How far off the index stops the facets were (0 true - 1 a stop off).")]
    public double indexError;
    [Tooltip("How well the facets met (-1 undercut: they do not meet; +1 overcut: cut too deep).")]
    public double meet;
    [Tooltip("How unevenly the facets of the tier were cut (0 all alike - 1 every one different): an unsymmetrical stone.")]
    public double asymmetry;
    [Tooltip("Facets botched outright (cut through a neighbour, or chipped): the tier wants recutting.")]
    public int misses;
    [Tooltip("The chips its botched facets left on the stone: recutting the tier grinds them away with the facets.")]
    public double botchChips;
}

/// <summary>A gemstone's state: what it is, how big, what is in it, what tests said, how it is oriented and cut.</summary>
[Serializable]
public class GemState
{
    [Tooltip("The gem it really is (a gem id).")]
    public string gem = "";
    public int seed;
    public CraftGemStage stage;
    [Tooltip("Carats as it came, and now.")]
    public double roughCarats, carats;
    [Tooltip("Its colour's richness (0-1), from the stone itself.")]
    public double tone = 0.7;
    [Tooltip("The tilt (°) of its optic axis, of its cleavage plane, and the direction of the cleavage's trace in the plan (°).")]
    public double axis, cleaveTilt, cleaveTrace;
    public List<CraftInclusion> inclusions = new List<CraftInclusion>();
    [Tooltip("The faces it was sawn or cleaved along.")]
    public List<CraftGemPlane> bounds = new List<CraftGemPlane>();
    [Tooltip("Surface scratches (0-1), heat damage (0-1), chips (0-1).")]
    public double scratches, heat, chips;

    [Header("Tests")]
    public bool looked;
    [Tooltip("What the scratch tests narrowed its hardness to.")]
    public double hardLo = 1, hardHi = 10;
    [Tooltip("Readings (-1 = not read).")]
    public double readRI = -1, readSG = -1, readPleo = -1, readAxis = -1;
    [Tooltip("What it was identified as, and whether that was right.")]
    public string identifiedAs = "";
    public bool identified;
    [Tooltip("Evaluated under the loupe: the value estimated then.")]
    public bool evaluated;
    public double estimate;

    [Header("Orientation and cut")]
    public bool oriented;
    public string cut = "";
    [Tooltip("The table's tilt to the crystal (°), and where the finished stone sits in the rough (centre -1..1, radius).")]
    public double tableAngle, windowX, windowY, windowR = 0.6;
    public List<CraftFacetRecord> facets = new List<CraftFacetRecord>();
    [Tooltip("Girdle diameter of the cut stone (mm).")]
    public double girdle;
    public double polish;

    [Header("Cleaning, grinding and polishing")]
    [Tooltip("Matrix and dirt still crusted on it round its outline (16 sectors, 0 clean - 1 crusted). Empty: clean.")]
    public List<float> crust = new List<float>();
    [Tooltip("How well it was cleaned (0-1): how clean it was left, less the marks the tools left. -1 never cleaned.")]
    public double cleaned = -1;
    [Tooltip("The planned outline's length to its width (from the cut chosen).")]
    public double cutLength = 1;
    [Tooltip("Its outline as ground on the wheel (32 radii from the planned stone's centre, in the rough's radius); empty until it is ground.")]
    public List<float> profile = new List<float>();
    [Tooltip("How far the ground outline is from the planned one (0 true - 1 lopsided), and how much was ground into the planned stone (share).")]
    public double shapeError, overground;
    [Tooltip("The polish each of 9 regions of the stone has had (0 none, about 1 bright, above 2.5 over-polished: the edges round).")]
    public List<float> polishRegions = new List<float>();
    [Tooltip("The compound the lap is charged with for it (a supply id; empty: none yet). Another compound charges the lap afresh.")]
    public string compound = "";

    public GemState Clone()
    {
        var c = (GemState)MemberwiseClone();
        c.inclusions = new List<CraftInclusion>();
        foreach (CraftInclusion i in inclusions) c.inclusions.Add(i.Clone());
        c.bounds = new List<CraftGemPlane>();
        foreach (CraftGemPlane b in bounds) c.bounds.Add(new CraftGemPlane { nx = b.nx, ny = b.ny, offset = b.offset, cleaved = b.cleaved });
        c.facets = new List<CraftFacetRecord>();
        foreach (CraftFacetRecord f in facets) c.facets.Add(new CraftFacetRecord { tier = f.tier, facets = f.facets, angleError = f.angleError, indexError = f.indexError, meet = f.meet, asymmetry = f.asymmetry, misses = f.misses, botchChips = f.botchChips });
        c.crust = new List<float>(crust);
        c.profile = new List<float>(profile);
        c.polishRegions = new List<float>(polishRegions);
        return c;
    }
}

/// <summary>What a stone is worth and why (true values, or the estimate from what an evaluation found).</summary>
public sealed class CraftGemGrade
{
    public double Clarity, Color, Cut, Polish, Integrity, Brilliance = 1, Quality, Carats, Value;
    /// <summary>The carats a cut of the rough is expected to give (before cutting).</summary>
    public double Yield;
    public readonly List<string> Notes = new List<string>();
}

/// <summary>
/// The lapidary's work, from rough to a cut stone: <b>identification</b> (looking, scratching with hardness points,
/// reading the refractive index, the specific gravity on the balance and the pleochroism through the dichroscope, then
/// naming it), <b>evaluation</b> (searching it under the loupe at every depth for crystals, feathers, clouds and silk:
/// clarity, colour, soundness and what it should be worth), <b>orientation</b> (where in the rough the finished stone
/// sits - clear of the flaws - and how its table lies to the crystal: across the axis for the best colour, never along a
/// cleavage plane), <b>splitting and sawing</b> (a cleavable stone parts cleanly with one blow along its plane - off the
/// plane it shatters; others are sawn, cool and steady), <b>faceting</b> (each tier at its angle - the pavilion's sets
/// the brilliance and depends on the stone's refractive index - every facet cut to meet its neighbours, neither short nor
/// deep) and <b>polishing</b> (a compound hard enough for the stone, light and cool: heat cracks the sensitive ones).
/// </summary>
public static class CraftLapidary
{
    /// <summary>The most rough that may be left outside the planned stone when faceting starts (the rest is ground away as the first tier is cut).</summary>
    public const double PreformExcess = 0.55;

    /// <summary>The most rough an unground, oriented stone may have outside its planned outline for the wheel to take on (more: saw it off first).</summary>
    public const double GrindableExcess = 0.8;

    /// <summary>The trim saw's feed pressure above which a brittle stone (toughness under <see cref="SawChipToughness"/>) chips at the end of the cut.</summary>
    public const double SawChipPressure = 0.65, SawChipToughness = 0.55;

    /// <summary>
    /// Grinding heat: a touch's dry pressure (its amount × (1 - coolant)) up to <see cref="GrindSafeTouch"/> does not heat
    /// the stone; each unit above it adds <see cref="GrindHeatPerTouch"/> of heat (a quarter of it to a stone that is not
    /// heat-sensitive); a heat-sensitive stone cracks once its heat reaches <see cref="GrindCrackHeat"/>.
    /// </summary>
    public const double GrindSafeTouch = 0.2, GrindHeatPerTouch = 0.04, GrindCrackHeat = 0.25;

    /// <summary>The lap's pressure from which polishing goes at its full rate; the heat (pressure × lap speed) above which the stone heats.</summary>
    public const double PolishFullRate = 0.45, PolishHeatLimit = 0.45;

    /// <summary>
    /// The lap's pressure above which a table laid along a cleavage plane flakes as it is polished; each pass's flaking
    /// (its pressure past that, as a share of what is left above it) takes <see cref="FlakeLoss"/> of polish off the table.
    /// </summary>
    public const double FlakePressure = 0.3, FlakeLoss = 0.34;

    /// <summary>The heat one touch on the wheel puts into the stone (see <see cref="GrindSafeTouch"/>).</summary>
    public static double GrindHeat(double amount, double coolant, bool heatSensitive) =>
        Math.Max(0, CraftMath.Clamp01(amount) * (1 - CraftMath.Clamp01(coolant)) - GrindSafeTouch - 1e-6) * GrindHeatPerTouch * (heatSensitive ? 1 : 0.25);

    /// <summary>How much crust <paramref name="work"/> of scrubbing takes off a sector (the brush slow, the pick quick; a steady hand more).</summary>
    public static double CrustRemoval(double work, bool pick, double toolQuality, double control) =>
        CraftMath.Clamp(work, 0, 3) * (pick ? 0.9 : 0.35) * toolQuality * (0.6 + 0.4 * CraftMath.Clamp01(control));

    /// <summary>How far round a look of the loupe it finds what lies at the depth in focus (in the rough's radius), with a hand that steady.</summary>
    public static double LoupeReach(double steady, double toolQuality) => (0.1 + 0.08 * CraftMath.Clamp01(steady)) * Math.Min(1.3, toolQuality);

    /// <summary>
    /// Does a look of the loupe at (<paramref name="x"/>, <paramref name="y"/>) focused at depth <paramref name="z"/> see
    /// <paramref name="i"/>: near enough (<paramref name="reach"/>, more for a big inclusion, less under a thick crust - a
    /// thick crust is opaque) and in focus?
    /// </summary>
    public static bool Seen(GemState g, CraftInclusion i, double x, double y, double z, double reach)
    {
        double dx = i.x - x, dy = i.y - y, rr = (reach + i.size * 0.6) * CraftMath.Clamp01(1 - 1.4 * CrustAt(g, i.x, i.y));
        return dx * dx + dy * dy <= rr * rr && Math.Abs(i.depth - z) <= FocusDepth + i.size;
    }

    /// <summary>How far either side of the depth in focus the loupe finds even the smallest inclusion (<see cref="Seen"/>).</summary>
    public const double FocusDepth = 0.12;

    /// <summary>The loupe's search as it is counted: cells of the plan (this many a side, over the rough's circle) and steps of depth (this many through the stone).</summary>
    public const int SearchCells = 20, SearchDepths = 20;

    /// <summary>
    /// Is the point (x, y) of the plan part of the stone as it is now: the cut stone's planned outline once it is faceted,
    /// its ground outline once it is ground, else what its faces leave of the rough?
    /// </summary>
    public static bool InStone(GemState g, double x, double y)
    {
        if (!Inside(g, x, y))
            return false;
        double dx = x - g.windowX, dy = y - g.windowY;
        if (g.oriented && g.stage >= CraftGemStage.Faceted)
        {
            double a = WindowRadius(g), b = a / Math.Max(1, g.cutLength);
            return a > 0 && dx * dx / (a * a) + dy * dy / (b * b) <= 1;
        }
        if (g.profile != null && g.profile.Count == ProfileRadii)
        {
            double t = Math.Atan2(dy, dx);
            if (t < 0) t += Math.PI * 2;
            double at = t / (Math.PI * 2) * ProfileRadii;
            int k0 = (int)Math.Floor(at) % ProfileRadii;
            double r = g.profile[k0] + (g.profile[(k0 + 1) % ProfileRadii] - g.profile[k0]) * (at - Math.Floor(at));
            return dx * dx + dy * dy <= r * r;
        }
        return true;
    }

    /// <summary>
    /// Marks the search cells (<see cref="SearchCells"/>² × <see cref="SearchDepths"/>, depth step by step) a look of the
    /// loupe at (<paramref name="x"/>, <paramref name="y"/>) focused at <paramref name="z"/> surely went over - where it
    /// finds whatever lies there: a cell whose middle lies within its <paramref name="reach"/> (less under a thick crust,
    /// which hides what is beneath it), at the steps of depth wholly within <see cref="FocusDepth"/> of the focus. A stone
    /// whose every cell is marked has had all it holds found (bar what lies at a cell's very corner).
    /// </summary>
    public static void MarkSearched(GemState g, bool[] searched, double x, double y, double z, double reach)
    {
        if (searched == null || searched.Length < SearchCells * SearchCells * SearchDepths || reach <= 0)
            return;
        int lo = Math.Max(0, (int)Math.Ceiling((z - FocusDepth) * SearchDepths - 1e-9));
        int hi = Math.Min(SearchDepths - 1, (int)Math.Floor((z + FocusDepth) * SearchDepths + 1e-9) - 1);
        if (hi < lo)
            return;
        for (int iy = 0; iy < SearchCells; iy++)
        {
            double cy = (iy + 0.5) / SearchCells * 2 - 1;
            if (Math.Abs(cy - y) > reach) continue;
            for (int ix = 0; ix < SearchCells; ix++)
            {
                double cx = (ix + 0.5) / SearchCells * 2 - 1, dx = cx - x, dy = cy - y;
                double rr = reach * CraftMath.Clamp01(1 - 1.4 * CrustAt(g, cx, cy));
                if (dx * dx + dy * dy > rr * rr) continue;
                for (int k = lo; k <= hi; k++) searched[(k * SearchCells + iy) * SearchCells + ix] = true;
            }
        }
    }

    /// <summary>The share of the stone (<see cref="InStone"/>), every step of its depth, the loupe has gone over: marked in <paramref name="searched"/> or in <paramref name="more"/>.</summary>
    public static double SearchedShare(GemState g, bool[] searched, bool[] more = null)
    {
        int inside = 0, done = 0, n = SearchCells * SearchCells * SearchDepths;
        for (int iy = 0; iy < SearchCells; iy++)
            for (int ix = 0; ix < SearchCells; ix++)
            {
                double x = (ix + 0.5) / SearchCells * 2 - 1, y = (iy + 0.5) / SearchCells * 2 - 1;
                if (!InStone(g, x, y)) continue;
                for (int k = 0; k < SearchDepths; k++)
                {
                    int at = (k * SearchCells + iy) * SearchCells + ix;
                    inside++;
                    if (searched != null && at < searched.Length && searched[at] || more != null && at < more.Length && more[at]) done++;
                }
            }
        return inside > 0 && n > 0 ? (double)done / inside : 0;
    }

    /// <summary>
    /// A defect that grows with what causes it (heat, flaking): added once, then raised to <paramref name="severity"/> -
    /// never compounded by being reported again request after request.
    /// </summary>
    private static void Worsen(CraftPiece p, CraftDefectKind kind, double severity, bool surface, string cause)
    {
        foreach (CraftDefect d in p.defects)
            if (d.kind == kind && d.at == -1)
            {
                if (severity > d.severity)
                {
                    d.severity = severity;
                    d.cause = cause;
                }
                d.surface |= surface;
                return;
            }
        p.AddDefect(kind, -1, severity, surface, cause);
    }

    /// <summary>The best pavilion angle for a stone of refractive index <paramref name="n"/> (40.75° for diamond, ~43° for quartz).</summary>
    public static double IdealPavilion(double n) => 40.75 + (2.42 - n) * 2.6;

    /// <summary>The best crown angle (34.5° for diamond, ~42° for quartz).</summary>
    public static double IdealCrown(double n) => 34.5 + (2.42 - n) * 8.5;

    /// <summary>The angle a tier is cut at on a stone of refractive index <paramref name="n"/>.</summary>
    public static double IdealAngle(CraftCutTier t, double n)
    {
        switch (t.kind)
        {
            case CraftCutTierKind.Pavilion: return IdealPavilion(n) + t.angleOffset;
            case CraftCutTierKind.Crown: return IdealCrown(n) + t.angleOffset;
            case CraftCutTierKind.Girdle: return 90;
            case CraftCutTierKind.Dome: return 38 + t.angleOffset;
            default: return 0;
        }
    }

    // ------------------------------------------------------------------ the rough
    /// <summary>A rough stone of <paramref name="gemId"/>; <paramref name="seed"/> decides its size, colour and what is in it.</summary>
    public static CraftPiece NewRough(CraftRules rules, string gemId, int seed, double carats = 0)
    {
        CraftGemDefinition d = rules.Gem(gemId);
        if (d == null)
            return null;
        var p = new CraftPiece { kind = CraftPieceKind.Gem, origin = "a rough stone" }; // what it is, the tests tell
        GemState g = p.gem;
        g.gem = d.id;
        g.seed = seed;
        g.roughCarats = g.carats = carats > 0 ? carats : Math.Round(d.typicalCarats * CraftMath.Range(seed, 1, 11, 0.5, 1.6), 2);
        g.tone = CraftMath.Range(seed, 2, 11, 0.45, 1);
        g.axis = CraftMath.Range(seed, 3, 11, 0, 90);
        g.cleaveTilt = CraftMath.Range(seed, 4, 11, 0, 90);
        g.cleaveTrace = CraftMath.Range(seed, 5, 11, 0, 180);
        double clarity = CraftMath.Clamp01(d.clarity + CraftMath.Range(seed, 6, 11, -0.2, 0.2));
        int n = (int)Math.Round((1 - clarity) * 8 + CraftMath.Range(seed, 7, 11, 0, 2));
        for (int i = 0; i < n; i++)
        {
            double r = Math.Sqrt(CraftMath.Noise(seed, 20 + i, 12)) * 0.92, a = CraftMath.Noise(seed, 20 + i, 13) * Math.PI * 2;
            double k = CraftMath.Noise(seed, 20 + i, 14);
            double feather = 0.18 + (d.cleavage >= CraftCleavage.Good ? 0.15 : 0);
            CraftInclusionKind kind = k < feather ? CraftInclusionKind.Feather : k < feather + 0.25 ? CraftInclusionKind.Cloud : k < feather + 0.4 ? CraftInclusionKind.Needle : CraftInclusionKind.Crystal;
            g.inclusions.Add(new CraftInclusion
            {
                kind = kind,
                x = (float)(r * Math.Cos(a)),
                y = (float)(r * Math.Sin(a)),
                depth = (float)CraftMath.Range(seed, 20 + i, 15, 0.05, 0.95),
                size = (float)(CraftMath.Range(seed, 20 + i, 16, 0.03, 0.14) * (1.2 - clarity)),
            });
        }
        // matrix and dirt crusted on it as it came out of the ground, thicker on one side
        double heavy = CraftMath.Range(seed, 8, 11, 0, Math.PI * 2);
        for (int k = 0; k < CrustSectors; k++)
        {
            double t = k * Math.PI * 2 / CrustSectors;
            g.crust.Add((float)CraftMath.Clamp01(0.25 + 0.35 * Math.Cos(t - heavy) + CraftMath.Range(seed, 60 + k, 11, -0.1, 0.25)));
        }
        return p;
    }

    /// <summary>Sectors of crust round a rough stone, and radii of a ground outline.</summary>
    public const int CrustSectors = 16, ProfileRadii = 32;

    /// <summary>How crusted it still is (0 clean - 1 all matrix and dirt). A cut stone has none: the lap took its skin.</summary>
    public static double Dirt(GemState g)
    {
        if (g.crust == null || g.crust.Count == 0 || g.stage >= CraftGemStage.Faceted)
            return 0;
        double s = 0;
        foreach (float c in g.crust) s += c;
        return CraftMath.Clamp01(s / g.crust.Count);
    }

    /// <summary>The crust over the sector of the outline nearest (x, y) in the plan.</summary>
    public static double CrustAt(GemState g, double x, double y)
    {
        if (g.crust == null || g.crust.Count == 0 || g.stage >= CraftGemStage.Faceted)
            return 0;
        double t = Math.Atan2(y, x);
        if (t < 0) t += Math.PI * 2;
        int k = (int)Math.Floor(t / (Math.PI * 2) * g.crust.Count) % g.crust.Count;
        // the crust lies on the skin: it hides what is near the outline most
        return g.crust[k] * CraftMath.Clamp01(0.35 + Math.Sqrt(x * x + y * y));
    }

    /// <summary>How hard a stone is to work true (the timing bars' zones narrow, the indicator runs faster): hard, brittle stones most.</summary>
    public static double Difficulty(CraftGemDefinition d) =>
        d == null ? 1 : CraftMath.Clamp(0.8 + (d.hardness - 6) * 0.08 + (0.6 - d.toughness) * 0.6, 0.7, 1.5);

    /// <summary>The blow that parts it: along a cleavage plane (<paramref name="snap"/> false) or nicked and snapped.</summary>
    public static double SplitForce(CraftGemDefinition d, bool snap) =>
        d == null ? 0.5 : snap ? 0.55 + 0.3 * d.toughness : 0.42 + 0.25 * d.toughness;

    /// <summary>Can it be nicked and snapped (no cleavage worth following, and not too tough to break where it is scored)?</summary>
    public static bool Snappable(CraftGemDefinition d) => d != null && d.cleavage <= CraftCleavage.Poor && d.toughness < 0.65;

    /// <summary>The timing bar the split's blow is judged on (the same zones the window shows).</summary>
    public static CraftTimingBar SplitBar(CraftRules rules, CraftGemDefinition d, bool snap)
    {
        var bar = new CraftTimingBar(rules?.Settings?.timing) { difficulty = (float)(Difficulty(d) * (snap ? 1.1 : d != null && d.cleavage == CraftCleavage.Perfect ? 0.9 : 1.05)) };
        bar.targets.Add(new CraftTimingTarget((float)SplitForce(d, snap)));
        return bar;
    }

    /// <summary>Is the point (x, y) of the plan still part of the stone?</summary>
    public static bool Inside(GemState g, double x, double y)
    {
        if (x * x + y * y > 1)
            return false;
        foreach (CraftGemPlane b in g.bounds)
            if (b.nx * x + b.ny * y > b.offset) return false;
        return true;
    }

    /// <summary>The grid (points a side) the plan is sampled on to weigh what is left of the rough.</summary>
    private const int AreaGrid = 28;

    /// <summary>The share of the original outline that is left (0-1), sampled on a grid.</summary>
    public static double Area(GemState g)
    {
        const int N = AreaGrid;
        int inside = 0, total = 0;
        for (int iy = 0; iy < N; iy++)
            for (int ix = 0; ix < N; ix++)
            {
                double x = (ix + 0.5) / N * 2 - 1, y = (iy + 0.5) / N * 2 - 1;
                if (x * x + y * y > 1) continue;
                total++;
                if (Inside(g, x, y)) inside++;
            }
        return total > 0 ? (double)inside / total : 0;
    }

    /// <summary>The radius the finished stone can really have where it was placed (the window, shrunk by any face cut into it).</summary>
    public static double WindowRadius(GemState g)
    {
        double r = Math.Min(g.windowR, 1 - Math.Sqrt(g.windowX * g.windowX + g.windowY * g.windowY));
        foreach (CraftGemPlane b in g.bounds)
            r = Math.Min(r, b.offset - (b.nx * g.windowX + b.ny * g.windowY));
        return Math.Max(0, r);
    }

    /// <summary>The share of the rough left that is outside the finished stone (it must be sawn off before faceting).</summary>
    public static double Excess(GemState g)
    {
        if (g.profile != null && g.profile.Count == ProfileRadii)
            return GroundExcess(g);
        double area = Area(g), r = WindowRadius(g);
        return area > 0 ? CraftMath.Clamp01(1 - r * r / area) : 0;
    }

    /// <summary>
    /// Is an inclusion inside the finished stone - the planned outline: an ellipse in the window, as long as the cut
    /// (<see cref="TargetRadius"/>), its long axis across the plan?
    /// </summary>
    public static bool Kept(GemState g, CraftInclusion i)
    {
        double a = WindowRadius(g), b = a / Math.Max(1, g.cutLength), s = i.size * 0.5;
        double dx = i.x - g.windowX, dy = i.y - g.windowY, ea = a + s, eb = b + s;
        return ea > 0 && eb > 0 && dx * dx / (ea * ea) + dy * dy / (eb * eb) <= 1;
    }

    /// <summary>
    /// The carats the planned stone keeps once its first tier is cut: what lies inside the planned outline (the ground
    /// outline's share of it, or the planned ellipse's share of the rough left; unplanned, a fair window of the rough in
    /// the stone's own cut) less what faceting takes (<see cref="CutKeeps"/>). The same number is the rough's expected
    /// yield and what the first tier delivers - and the same whether the preform was ground to its outline first (the
    /// wheel already took what lay outside it, by the same measure: <see cref="Grind"/>) or not: the Orient place's
    /// expected yield is what the guided path keeps, and leaving the wheel out never makes the stone heavier.
    /// </summary>
    public static double PlannedCarats(GemState g, CraftGemCut cut)
    {
        if (cut == null)
            return g.carats;
        double share;
        if (g.profile.Count == ProfileRadii)
        {
            double have = ProfileArea(g.profile);
            var kept = new List<float>();
            for (int k = 0; k < ProfileRadii; k++) kept.Add((float)Math.Min(g.profile[k], TargetRadius(g, k)));
            share = ProfileArea(kept) / Math.Max(1e-4, have);
        }
        else
        {
            double a = g.oriented ? WindowRadius(g) : 0.62, length = Math.Max(1, g.oriented ? g.cutLength : cut.lengthRatio);
            share = a * a / length / Math.Max(0.01, Area(g)); // the ellipse's area (π a b, b = a / length) over the rough's
        }
        return Math.Max(0.01, g.carats * Math.Pow(CraftMath.Clamp01(share), 1.5) * CutKeeps(cut));
    }

    /// <summary>
    /// The share of what lies inside its outline a cut keeps (0-1, by its yield): a round brilliant's facets take about an
    /// eighth of it; a cushion, a step cut or a cabochon's dome keep all of it - never more (what lay outside the outline is
    /// not there to keep, ground off or not).
    /// </summary>
    public static double CutKeeps(CraftGemCut cut) => cut != null ? Math.Min(1, cut.yield * 2.2) : 1;

    /// <summary>How rich its colour shows with the table where it is (pleochroic stones are best across the axis).</summary>
    public static double ColorFactor(CraftGemDefinition d, GemState g)
    {
        double off = (g.tableAngle - g.axis) * Math.PI / 180;
        return 1 - (d?.pleochroism ?? 0) * 0.6 * Math.Sin(off) * Math.Sin(off);
    }

    /// <summary>Does the table lie within a few degrees of a cleavage plane (it will flake when polished)?</summary>
    public static bool TableOnCleavage(CraftGemDefinition d, GemState g) =>
        d != null && d.cleavage >= CraftCleavage.Good && Math.Abs(g.tableAngle - g.cleaveTilt) < 8;

    private static double Weight(CraftInclusionKind k) => k switch
    {
        CraftInclusionKind.Feather => 1.3,
        CraftInclusionKind.Cloud => 0.6,
        CraftInclusionKind.Needle => 0.4,
        _ => 1,
    };

    // ------------------------------------------------------------------ grading
    /// <summary>
    /// What a stone is (or, with <paramref name="foundOnly"/>, what an evaluation that found only the inclusions it found
    /// would say): clarity, colour, cut, polish, soundness, brilliance, quality and value. On rough, the cut is what the
    /// orientation (or the best clear window) would give.
    /// </summary>
    public static CraftGemGrade Grade(CraftRules rules, CraftPiece p, bool foundOnly = false)
    {
        var r = new CraftGemGrade();
        if (p == null || p.kind != CraftPieceKind.Gem)
            return r;
        GemState g = p.gem;
        CraftGemDefinition d = rules.Gem(g.gem);
        CraftGemCut cut = rules.Cut(string.IsNullOrEmpty(g.cut) ? d?.preferredCut : g.cut);
        bool rough = g.stage < CraftGemStage.Faceted;
        double flaws = 0, feathers = 0;
        foreach (CraftInclusion i in g.inclusions)
        {
            if (foundOnly && !i.found) continue;
            if (g.oriented && !Kept(g, i)) continue;
            flaws += i.size * Weight(i.kind);
            if (i.kind == CraftInclusionKind.Feather) feathers += i.size;
        }
        r.Clarity = CraftMath.Clamp01(1 - flaws * 2.2);
        r.Color = CraftMath.Clamp01((0.45 + 0.55 * g.tone) * (g.oriented ? ColorFactor(d, g) : 1) * (1 - 0.5 * g.heat * (d != null && d.heatSensitive ? 1 : 0.3)));
        r.Integrity = CraftMath.Clamp01(1 - feathers * 3 - g.heat * 0.5 - g.chips * 0.6 - (d != null && d.cleavage == CraftCleavage.Perfect ? 0.08 : 0) - (g.oriented && TableOnCleavage(d, g) ? 0.25 : 0));
        r.Polish = rough ? 0 : CraftMath.Clamp01(g.polish * (1 - g.scratches));
        if (cut != null && !rough)
        {
            double sum = 0, wSum = 0;
            for (int t = 0; t < cut.tiers.Count; t++)
            {
                CraftFacetRecord f = g.facets.Find(x => x.tier == t);
                double w = cut.tiers[t].kind == CraftCutTierKind.Pavilion ? 2 : cut.tiers[t].kind == CraftCutTierKind.Dome ? 2 : 1;
                wSum += w;
                if (f == null) continue;
                double angle = Math.Exp(-Math.Pow(f.angleError / 2.5, 2));
                sum += w * angle * (1 - 0.6 * CraftMath.Clamp01(f.indexError)) * (1 - 0.7 * Math.Min(1, Math.Abs(f.meet)))
                       * (1 - 0.5 * CraftMath.Clamp01(f.asymmetry)) * Math.Max(0.4, 1 - 0.15 * f.misses);
                if (f.misses > 0) r.Notes.Add($"{cut.tiers[t].name.ToLowerInvariant()}: {f.misses} botched facet{(f.misses == 1 ? "" : "s")} (recut the tier)");
                else if (f.asymmetry > 0.35) r.Notes.Add($"{cut.tiers[t].name.ToLowerInvariant()}: uneven facets");
                if (cut.tiers[t].kind == CraftCutTierKind.Pavilion && !cut.cabochon && d != null && !d.opaque)
                {
                    if (f.angleError < -3) { r.Brilliance *= Math.Max(0.3, 1 + (f.angleError + 3) * 0.12); r.Notes.Add("the pavilion is too shallow: light leaks through (a window)"); }
                    else if (f.angleError > 3) { r.Brilliance *= Math.Max(0.3, 1 - (f.angleError - 3) * 0.1); r.Notes.Add("the pavilion is too deep: it is dark in the middle"); }
                }
            }
            r.Cut = (wSum > 0 ? sum / wSum : 0) * (1 - 0.6 * Math.Min(1, 2 * g.shapeError));
            if (g.shapeError > 0.15) r.Notes.Add("its outline is lopsided (ground unevenly)");
            if (g.chips > 0.1) r.Notes.Add("chipped");
            if (g.polishRegions.Count > 0)
            {
                int dull = 0;
                foreach (float c in g.polishRegions) if (c < DullPolish) dull++;
                if (dull > 0) r.Notes.Add($"{dull} dull patch{(dull == 1 ? "" : "es")} the polish never reached");
            }
        }
        else if (cut != null)
        {
            // Rough: the cut it could take, as well as the cutter cuts (assume a good cut).
            r.Cut = 0.8;
        }
        double carats = g.carats;
        if (rough && cut != null)
        {
            // what the first tier will leave of it: the very number Facet delivers (once a tier is cut, it is what it weighs)
            r.Yield = g.facets.Count > 0 ? g.carats : PlannedCarats(g, cut);
            carats = r.Yield;
        }
        r.Carats = carats;
        bool cab = cut != null && cut.cabochon;
        double look = cab ? 1 : r.Brilliance;
        r.Quality = CraftMath.Clamp01((r.Clarity * 0.27 + r.Color * 0.23 + r.Cut * 0.25 + (rough ? 0.08 : r.Polish * 0.1) + r.Integrity * 0.15) * look);
        double perCarat = d?.valuePerCarat ?? 10;
        r.Value = perCarat * carats * (0.15 + 1.7 * r.Quality * r.Quality) * (1 + 0.18 * Math.Log(1 + carats));
        if (r.Clarity < 0.6) r.Notes.Add(foundOnly ? "visibly included (what was found)" : "visibly included");
        if (feathers > 0.08) r.Notes.Add("feathers: it may break if knocked");
        if (g.oriented && TableOnCleavage(d, g)) r.Notes.Add("the table lies along a cleavage plane");
        if (g.heat > 0.2) r.Notes.Add("heat damage");
        return r;
    }

    // ------------------------------------------------------------------ identification
    /// <summary>
    /// A test (<c>i0</c> = <see cref="CraftGemTest"/>): <c>a</c> = how carefully it was made (0-1, the minigame),
    /// <c>b</c> = the hardness point used (Scratch). Readings carry an error that shrinks with care and the instrument.
    /// </summary>
    public static void Test(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        GemState g = p.gem;
        CraftGemDefinition d = ws.Rules.Gem(g.gem);
        if (d == null)
            return;
        double care = CraftMath.Clamp01(op.a), tq = ws.ToolQuality, dirt = Dirt(g);
        int salt = 100 + p.record.Count;
        double noise = CraftMath.Noise(g.seed, salt, 31) * 2 - 1;
        if (dirt > 0.3 && (CraftGemTest)op.i0 != CraftGemTest.Loupe && (CraftGemTest)op.i0 != CraftGemTest.Scratch)
            ws.Raise(CraftEventKind.Note, p.id, "crusted", "The crust on it gets in the way of the reading.");
        switch ((CraftGemTest)op.i0)
        {
            case CraftGemTest.Loupe:
                g.looked = true;
                p.Record(ws.Elapsed, CraftOpKind.TestGem, dirt > 0.3 ? "Looked at under the loupe - the crust hides most of it" : "Looked at under the loupe");
                break;
            case CraftGemTest.Scratch:
            {
                double point = CraftMath.Clamp(op.b, 1, 10);
                // harder than the stone, the point scratches it: the reading brackets the stone's own hardness
                if (point > d.hardness)
                {
                    g.hardHi = Math.Min(g.hardHi, point);
                    g.scratches = CraftMath.Clamp01(g.scratches + 0.05 * (1.2 - care));
                    p.Record(ws.Elapsed, CraftOpKind.TestGem, $"Scratched by the {point:0.#} point", point);
                }
                else
                {
                    g.hardLo = Math.Max(g.hardLo, point);
                    p.Record(ws.Elapsed, CraftOpKind.TestGem, $"Not scratched by the {point:0.#} point", point);
                }
                if (care < 0.3 && d.toughness < 0.4)
                {
                    g.chips = CraftMath.Clamp01(g.chips + 0.05);
                    p.AddDefect(CraftDefectKind.Chip, -1, 0.1, true, "chipped by a careless scratch test");
                }
                break;
            }
            case CraftGemTest.Refractometer:
            {
                // crust on the stone keeps it off the prism: the reading blurs
                double spread = 0.035 * (1.15 - care) / tq * (g.stage < CraftGemStage.Faceted ? 2 : 1) * (1 + 3 * dirt);
                g.readRI = d.opaque ? Math.Round(d.refractiveIndex + noise * spread * 1.5, 1) : Math.Round(d.refractiveIndex + noise * spread, 2);
                p.Record(ws.Elapsed, CraftOpKind.TestGem, g.stage < CraftGemStage.Faceted ? "A spot reading on the refractometer" : "Read on the refractometer", g.readRI);
                break;
            }
            case CraftGemTest.Gravity:
            {
                double spread = 0.18 * (1.15 - care) / tq * (g.carats < 1 ? 2 : 1);
                // matrix stuck to it is lighter than the stone: it reads low
                g.readSG = Math.Round(d.specificGravity + noise * spread - dirt * 0.6, 2);
                p.Record(ws.Elapsed, CraftOpKind.TestGem, "Weighed in air and in water", g.readSG);
                break;
            }
            case CraftGemTest.Dichroscope:
            {
                g.readPleo = CraftMath.Clamp01(d.pleochroism + noise * 0.2 * (1.1 - care) * (1 + 2 * dirt));
                g.readAxis = CraftMath.Clamp(g.axis + noise * 25 * (1.1 - care) / tq, 0, 90);
                p.Record(ws.Elapsed, CraftOpKind.TestGem, "Looked through the dichroscope", g.readPleo);
                break;
            }
        }
    }

    /// <summary>How many tests have been read on it (looking counts as one).</summary>
    public static int Tests(GemState g) =>
        (g.looked ? 1 : 0) + (g.hardLo > 1 || g.hardHi < 10 ? 1 : 0) + (g.readRI >= 0 ? 1 : 0) + (g.readSG >= 0 ? 1 : 0) + (g.readPleo >= 0 ? 1 : 0);

    /// <summary>
    /// Names it (<c>id</c> = the gem it is named as), once: right or wrong, the name sticks with it (the workshop refuses a
    /// second name). The note it raises carries no words - only its id ("identified" or "misidentified"), so nothing tells
    /// the player whether the name was right.
    /// </summary>
    public static void Identify(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        p.gem.identifiedAs = op.id ?? "";
        p.gem.identified = string.Equals(op.id, p.gem.gem, StringComparison.OrdinalIgnoreCase);
        string name = ws.Rules.Gem(op.id)?.Name ?? op.id;
        p.Record(ws.Elapsed, CraftOpKind.IdentifyGem, $"Identified as {name}");
        ws.Raise(CraftEventKind.Note, p.id, p.gem.identified ? "identified" : "misidentified", "");
    }

    /// <summary>
    /// Searches it under the loupe: <c>points</c> = where the loupe looked (x, y, focus depth triples in the plan),
    /// <c>a</c> = how steady the loupe was (0-1). What lies near a look at the depth in focus is found; then its worth is
    /// estimated from what was found.
    /// </summary>
    public static void Evaluate(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        GemState g = p.gem;
        double reach = LoupeReach(op.a, ws.ToolQuality);
        int found = 0;
        for (int k = 0; k + 2 < op.points.Count; k += 3)
        {
            double x = op.points[k], y = op.points[k + 1], z = op.points[k + 2];
            foreach (CraftInclusion i in g.inclusions)
            {
                if (i.found || !Seen(g, i, x, y, z, reach)) continue;
                i.found = true;
                found++;
            }
        }
        g.evaluated = true;
        g.estimate = Grade(ws.Rules, p, true).Value;
        p.Record(ws.Elapsed, CraftOpKind.EvaluateGem, found > 0 ? $"Searched under the loupe: {found} inclusion{(found == 1 ? "" : "s")} found" : "Searched under the loupe", found);
    }

    // ------------------------------------------------------------------ orientation and preforming
    /// <summary>
    /// Plans the cut: <c>id</c> = the cut, <c>a</c> = the table's tilt to the crystal (°), <c>b</c>, <c>c</c> = the
    /// finished stone's centre in the plan, <c>d</c> = its radius (share of the rough).
    /// </summary>
    public static void Orient(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        GemState g = p.gem;
        g.cut = op.id ?? "";
        g.tableAngle = CraftMath.Clamp(op.a, 0, 90);
        g.windowX = CraftMath.Clamp(op.b, -1, 1);
        g.windowY = CraftMath.Clamp(op.c, -1, 1);
        g.windowR = CraftMath.Clamp(op.d, 0.15, 1);
        g.cutLength = Math.Max(1, ws.Rules.Cut(g.cut)?.lengthRatio ?? 1);
        g.profile.Clear();
        g.oriented = true;
        CraftGemDefinition d = ws.Rules.Gem(g.gem);
        int kept = 0;
        foreach (CraftInclusion i in g.inclusions)
            if (Kept(g, i)) kept++;
        string how = TableOnCleavage(d, g) ? "Oriented - the table lies along the cleavage" : ColorFactor(d, g) < 0.85 ? "Oriented - the colour will look washed out this way" : kept == 0 ? "Oriented clear of the flaws" : "Oriented";
        p.Record(ws.Elapsed, CraftOpKind.OrientGem, how, WindowRadius(g));
    }

    /// <summary>How far (°) a line drawn at <paramref name="angle"/> is from a cleavage's trace (lines, so 0-90).</summary>
    public static double CleaveError(double angle, double trace) => Math.Abs(((angle - trace) % 180 + 270) % 180 - 90);

    /// <summary>How near the cleavage's trace a line must lie for the stone to part along its plane (only a Good or Perfect cleavage is followed).</summary>
    public static double CleaveTolerance(CraftGemDefinition d) => d != null && d.cleavage == CraftCleavage.Perfect ? 4 : 2.5;

    /// <summary>
    /// A line of the plan as the hands drew it - its direction (°, whichever way it was drawn) and its signed distance from
    /// the centre along its normal (sin a, -cos a) - as the one line it is, its direction in [0°, 180°). Turning the
    /// direction half round turns the normal with it, so the offset changes sign too: a line drawn right to left, or
    /// downward, is cut where it was drawn, never at its mirror image through the centre.
    /// </summary>
    public static void NormalLine(double angle, double offset, out double a, out double b)
    {
        a = angle % 360;
        if (a < 0) a += 360;
        b = offset;
        while (a >= 180)
        {
            a -= 180;
            b = -b;
        }
    }

    /// <summary>
    /// The line drawn from (<paramref name="ax"/>, <paramref name="ay"/>) to (<paramref name="bx"/>, <paramref name="by"/>)
    /// of the plan, as the saw and the chisel take it: its direction and its offset (<see cref="NormalLine"/>).
    /// </summary>
    public static void LineThrough(double ax, double ay, double bx, double by, out double angle, out double offset)
    {
        double dx = bx - ax, dy = by - ay;
        double a = Math.Atan2(dy, dx) * 180 / Math.PI, r = a * Math.PI / 180;
        NormalLine(a, Math.Sin(r) * ax - Math.Cos(r) * ay, out angle, out offset);
    }

    /// <summary>
    /// Does a line of the plan (its direction °, its offset) run through what is left of the stone, with stone on both its
    /// sides? A line that misses it saws or parts nothing, and costs nothing: the workshop refuses it. Once the stone is
    /// ground, its ground outline is what is left; before, what its faces leave of the rough, on the grid <see cref="Area"/>
    /// weighs it by.
    /// </summary>
    public static bool LineCuts(GemState g, double angle, double offset)
    {
        double a = angle * Math.PI / 180, nx = Math.Sin(a), ny = -Math.Cos(a);
        bool before = false, beyond = false;
        if (g.profile != null && g.profile.Count == ProfileRadii)
        {
            for (int k = 0; k < ProfileRadii; k++)
            {
                double t = k * Math.PI * 2 / ProfileRadii, r = g.profile[k];
                double s = nx * (g.windowX + r * Math.Cos(t)) + ny * (g.windowY + r * Math.Sin(t)) - offset;
                before |= s < 0;
                beyond |= s > 0;
            }
            return before && beyond;
        }
        for (int iy = 0; iy < AreaGrid; iy++)
            for (int ix = 0; ix < AreaGrid; ix++)
            {
                double x = (ix + 0.5) / AreaGrid * 2 - 1, y = (iy + 0.5) / AreaGrid * 2 - 1;
                if (!Inside(g, x, y)) continue;
                double s = nx * x + ny * y - offset;
                before |= s < 0;
                beyond |= s > 0;
                if (before && beyond) return true;
            }
        return false;
    }

    /// <summary>
    /// Cleaves it along a line of the plan: <c>a</c> = the line's direction (°), <c>b</c> = its signed distance from the
    /// centre, <c>c</c> = the blow's force (0-1). Along the cleavage (within a few degrees) and struck firmly but not hard,
    /// it parts cleanly; off the plane, or struck too hard, it shatters - a feather runs into the stone and it chips.
    /// </summary>
    public static void Cleave(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        CraftGemDefinition d = ws.Rules.Gem(p.gem.gem);
        if (d == null)
            return;
        bool snap = op.i0 == 1;
        double force = CraftMath.Clamp01(op.c);
        if (force < 0.3)
        {
            p.Record(ws.Elapsed, CraftOpKind.CleaveGem, "It rang under the blow, but did not part", force);
            return;
        }
        // the blow judged on the split's timing bar: 100% is the force that parts this stone
        CraftTimingBar bar = SplitBar(ws.Rules, d, snap);
        float score = bar.Score((float)(force - bar.targets[0].ideal), bar.targets[0]);
        CraftTimingGrade grade = bar.Grade(score, (float)(force - bar.targets[0].ideal), bar.targets[0]);
        bool harmful = score < bar.settings.damageBelow;
        if (snap)
        {
            // nicked with the saw and snapped: never a clean face; the further from the right blow, the worse the break
            Divide(ws, p, op, 0.06 + (1 - score) * 0.15, false);
            p.gem.chips = CraftMath.Clamp01(p.gem.chips + 0.04 + (1 - score) * 0.15);
            if (harmful)
            {
                double sevS = CraftMath.Clamp01(0.2 + (bar.settings.damageBelow - score) * 2);
                AddFeatherNearCut(p.gem, op, sevS);
                p.AddDefect(CraftDefectKind.Chip, -1, sevS, true, force > bar.targets[0].ideal ? "snapped too hard: it broke into the stone" : "struck too weakly, then forced: it broke raggedly");
                ws.Raise(CraftEventKind.Note, p.id, "shattered", "It breaks raggedly, a crack running in from the break.");
            }
            p.Record(ws.Elapsed, CraftOpKind.CleaveGem, $"Nicked and snapped - {grade} ({score * 100:0}%)", score);
            return;
        }
        double err = CleaveError(op.a, p.gem.cleaveTrace);
        double tol = CleaveTolerance(d);
        bool clean = err <= tol && !harmful;
        // a true blow parts it with almost nothing lost; a blow off its measure steps the face
        Divide(ws, p, op, clean ? 0.01 + (1 - score) * 0.06 : 0.12, clean);
        if (clean && score < bar.settings.goodScore)
        {
            p.gem.chips = CraftMath.Clamp01(p.gem.chips + (bar.settings.goodScore - score) * 0.3);
            p.AddDefect(CraftDefectKind.Chip, -1, (bar.settings.goodScore - score) * 0.5, true, "the blow was off its measure: the face stepped as it parted");
        }
        if (!clean)
        {
            double sev = CraftMath.Clamp01((err - tol) / 20 + Math.Max(0, bar.settings.damageBelow - score) * 2 + 0.15);
            p.gem.chips = CraftMath.Clamp01(p.gem.chips + sev * 0.5);
            AddFeatherNearCut(p.gem, op, sev);
            p.AddDefect(CraftDefectKind.Chip, -1, sev, true, err > tol ? "cleaved off its plane: it shattered" : force > bar.targets[0].ideal ? "struck too hard: it shattered as it parted" : "struck too weakly, it crushed rather than parted");
            ws.Raise(CraftEventKind.Note, p.id, "shattered", "The stone shatters instead of parting.");
        }
        p.Record(ws.Elapsed, CraftOpKind.CleaveGem, clean ? $"Cleaved along its plane - {grade} ({score * 100:0}%)" : "Cleaved - it shattered", err);
    }

    /// <summary>
    /// Saws it along a line of the plan: <c>a</c> = direction (°), <c>b</c> = offset, <c>c</c> = how steady the cut was
    /// (0-1), <c>d</c> = coolant (0-1), <c>e</c> = feed pressure (0-1). The kerf eats a little; a wandering cut eats more;
    /// a dry cut heats it (heat-sensitive stones crack); pressing hard on a brittle stone chips it.
    /// </summary>
    public static void Saw(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        CraftGemDefinition d = ws.Rules.Gem(p.gem.gem);
        if (d == null)
            return;
        double steady = CraftMath.Clamp01(op.c), cool = CraftMath.Clamp01(op.d), press = CraftMath.Clamp01(op.e), tq = ws.ToolQuality;
        int misses = Math.Max(0, op.i0);
        // the kerf, a wandering cut, a blade that jumped out of it (each miss), crust that throws the blade
        double loss = 0.03 + (1 - steady) * 0.08 / tq + misses * 0.02 + Dirt(p.gem) * 0.03;
        Divide(ws, p, op, Math.Min(0.4, loss), false);
        if (misses > 0)
        {
            double sevM = CraftMath.Clamp01(misses * 0.08 * (1.3 - d.toughness));
            p.gem.chips = CraftMath.Clamp01(p.gem.chips + sevM * 0.5);
            p.AddDefect(CraftDefectKind.Chip, -1, sevM, true, $"the blade jumped the kerf {misses} time{(misses == 1 ? "" : "s")}: the face is chipped");
        }
        double heat = (1 - cool) * (0.4 + press) * (d.hardness > 8 ? 1.3 : 1);
        if (heat > 0.5)
        {
            p.gem.heat = CraftMath.Clamp01(p.gem.heat + (heat - 0.5) * (d.heatSensitive ? 1.2 : 0.4));
            if (d.heatSensitive)
            {
                AddFeatherNearCut(p.gem, op, heat - 0.4);
                p.AddDefect(CraftDefectKind.Crack, -1, heat - 0.4, false, "the saw ran dry and the heat cracked it");
            }
        }
        if (press > SawChipPressure && d.toughness < SawChipToughness)
        {
            double sev = (press - SawChipPressure) * 2 * (1 - d.toughness);
            p.gem.chips = CraftMath.Clamp01(p.gem.chips + sev);
            p.AddDefect(CraftDefectKind.Chip, -1, sev, true, "pressed too hard on the saw: it chipped at the end of the cut");
        }
        p.Record(ws.Elapsed, CraftOpKind.SawGem, heat > 0.5 ? "Sawn - it got hot" : misses > 0 ? $"Sawn ({steady * 100:0}%) - the blade jumped" : steady < 0.5 ? $"Sawn ({steady * 100:0}%), the cut wandered" : $"Sawn ({steady * 100:0}%)", steady);
    }

    private static void AddFeatherNearCut(GemState g, CraftOperation op, double sev)
    {
        double a = op.a * Math.PI / 180, nx = Math.Sin(a), ny = -Math.Cos(a);
        double s = Side(g, nx, ny, op.b);
        // a little way into the side kept
        double px = nx * (op.b - s * 0.12), py = ny * (op.b - s * 0.12);
        g.inclusions.Add(new CraftInclusion { kind = CraftInclusionKind.Feather, x = (float)px, y = (float)py, depth = 0.5f, size = (float)CraftMath.Clamp(sev * 0.15, 0.03, 0.15) });
    }

    /// <summary>Which side of the line the stone keeps: the side with the planned stone (or the centre of what is left).</summary>
    private static double Side(GemState g, double nx, double ny, double offset)
    {
        double cx = g.oriented ? g.windowX : 0, cy = g.oriented ? g.windowY : 0;
        if (!g.oriented)
        {
            // the centre of what is left
            double sx = 0, sy = 0;
            int n = 0;
            for (int iy = 0; iy < 16; iy++)
                for (int ix = 0; ix < 16; ix++)
                {
                    double x = (ix + 0.5) / 8 - 1, y = (iy + 0.5) / 8 - 1;
                    if (!Inside(g, x, y)) continue;
                    sx += x; sy += y; n++;
                }
            if (n > 0) { cx = sx / n; cy = sy / n; }
        }
        return nx * cx + ny * cy <= offset ? 1 : -1;
    }

    /// <summary>
    /// Divides the stone along a line: this piece keeps the planned stone's side; the other side, if there is enough of
    /// it, becomes a stone of its own (an offcut that may be cut too). <paramref name="loss"/> = carats lost in the cut.
    /// A stone already ground on the wheel keeps its ground outline, cut back where the new face crosses it (its carats
    /// follow the outline, not the rough it came from).
    /// </summary>
    private static void Divide(CraftWorkshop ws, CraftPiece p, CraftOperation op, double loss, bool cleaved)
    {
        GemState g = p.gem;
        double a = op.a * Math.PI / 180, nx = Math.Sin(a), ny = -Math.Cos(a), off = op.b;
        double s = Side(g, nx, ny, off);
        double before = Math.Max(0.001, Area(g));
        var keep = new CraftGemPlane { nx = (float)(nx * s), ny = (float)(ny * s), offset = (float)(off * s), cleaved = cleaved };
        var other = new CraftGemPlane { nx = (float)(-nx * s), ny = (float)(-ny * s), offset = (float)(-off * s), cleaved = cleaved };
        GemState rest = g.Clone();
        g.bounds.Add(keep);
        rest.bounds.Add(other);
        double mine = Area(g), theirs = Area(rest);
        if (mine <= 0.001)
        {
            g.bounds.Remove(keep);
            return;
        }
        double carats = g.carats, myShare = mine / before, theirShare = theirs / before;
        if (g.profile.Count == ProfileRadii)
        {
            // the ground outline, cut back to the new face along every radius that reaches past it
            double had = Math.Max(1e-4, ProfileArea(g.profile)), centre = keep.nx * g.windowX + keep.ny * g.windowY;
            for (int k = 0; k < ProfileRadii; k++)
            {
                double t = k * Math.PI * 2 / ProfileRadii, along = keep.nx * Math.Cos(t) + keep.ny * Math.Sin(t);
                if (along <= 1e-6) continue; // this radius runs away from the face
                double reach = (keep.offset - centre) / along;
                if (reach < g.profile[k]) g.profile[k] = (float)Math.Max(0.05, reach);
            }
            myShare = CraftMath.Clamp01(ProfileArea(g.profile) / had);
            theirShare = 1 - myShare;
        }
        g.carats = Math.Max(0.01, carats * Math.Pow(myShare, 1.5) * (1 - loss));
        g.inclusions.RemoveAll(i => !Inside(g, i.x, i.y));
        double restCarats = carats * Math.Pow(theirShare, 1.5) * (1 - loss);
        if (restCarats >= 0.15)
        {
            CraftPiece o = ws.Create(op, CraftPieceKind.Gem, "an offcut of a larger stone"); // never its name: the tests tell what it is
            o.gem = rest;
            rest.carats = restCarats;
            rest.roughCarats = restCarats;
            // an offcut is rough: never preformed as a stone of its own, it is planned and sawn (or ground) like any rough
            rest.stage = CraftGemStage.Rough;
            rest.polish = 0;
            rest.polishRegions.Clear();
            rest.compound = "";
            rest.oriented = false;
            rest.cut = "";
            rest.cutLength = 1;
            rest.profile.Clear(); // an offcut is rough: it was never ground as a stone of its own
            rest.shapeError = rest.overground = 0;
            rest.facets.Clear();
            rest.evaluated = false;
            rest.inclusions.RemoveAll(i => !Inside(rest, i.x, i.y));
            o.Record(ws.Elapsed, op.kind, "Cut off a larger stone");
        }
        if (g.oriented && Excess(g) <= PreformExcess && g.stage == CraftGemStage.Rough)
            g.stage = CraftGemStage.Preformed;
    }

    // ------------------------------------------------------------------ cleaning and grinding
    /// <summary>
    /// Cleans the rough (<c>i0</c> = the tool: 0 a stiff brush and water, 1 a steel pick): <c>points</c> = how much
    /// each of the 16 sectors of its outline was worked, <c>a</c> = how controlled the tool was (0-1). The brush is
    /// slow on hard crust but never harms the stone; the pick is quick, but worked on bare stone it scratches it and, on
    /// a brittle one handled carelessly, chips it. Crust left on blurs the tests and hides what is beneath it.
    /// </summary>
    public static void Clean(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        GemState g = p.gem;
        CraftGemDefinition d = ws.Rules.Gem(g.gem);
        if (d == null)
            return;
        bool pick = op.i0 == 1;
        double control = CraftMath.Clamp01(op.a), tq = ws.ToolQuality, bare = 0;
        while (g.crust.Count < CrustSectors) g.crust.Add(0);
        for (int k = 0; k < CrustSectors && k < op.points.Count; k++)
        {
            double removal = CrustRemoval(op.points[k], pick, tq, control);
            double c = g.crust[k];
            g.crust[k] = (float)Math.Max(0, c - removal);
            bare += Math.Max(0, removal - c);
        }
        double marks = 0;
        if (pick && bare > 0)
        {
            // the pick worked on stone with nothing left to take off
            marks = bare * 0.05 * (1.3 - control) * (d.hardness < 7 ? 1.5 : 0.6);
            g.scratches = CraftMath.Clamp01(g.scratches + marks);
            if (d.toughness < 0.5 && control < 0.5 && bare > 0.4)
            {
                double sev = CraftMath.Clamp01((bare - 0.4) * 0.15 * (1 - control));
                g.chips = CraftMath.Clamp01(g.chips + sev);
                p.AddDefect(CraftDefectKind.Chip, -1, sev, true, "the pick slipped on the bare stone and chipped it");
            }
        }
        double dirt = Dirt(g);
        g.cleaned = CraftMath.Clamp01(1 - dirt - g.scratches * 0.5);
        int crusted = 0;
        foreach (float c in g.crust) if (c > 0.15) crusted++;
        string how = crusted == 0 ? "Cleaned to the bare stone" : $"Cleaned - {crusted} patch{(crusted == 1 ? "" : "es")} still crusted";
        if (marks > 0.02) how += ", the pick marked it";
        p.Record(ws.Elapsed, CraftOpKind.CleanGem, how, g.cleaned);
    }

    /// <summary>The planned outline's radius toward profile radius <paramref name="k"/> (an ellipse as long as the cut, inside the window).</summary>
    public static double TargetRadius(GemState g, int k)
    {
        double t = k * Math.PI * 2 / ProfileRadii, a = WindowRadius(g), b = a / Math.Max(1, g.cutLength);
        double ca = b * Math.Cos(t), sa = a * Math.Sin(t);
        return a * b / Math.Max(1e-6, Math.Sqrt(ca * ca + sa * sa));
    }

    /// <summary>The area of an outline of radii (in the rough's radius units).</summary>
    public static double ProfileArea(IList<float> r)
    {
        double s = 0, w = Math.Sin(Math.PI * 2 / Math.Max(3, r.Count));
        for (int k = 0; k < r.Count; k++) s += 0.5 * r[k] * r[(k + 1) % r.Count] * w;
        return s;
    }

    /// <summary>The share of the ground stone still outside the planned outline (the wheel must take it off).</summary>
    public static double GroundExcess(GemState g)
    {
        if (g.profile.Count != ProfileRadii)
            return 1;
        double have = ProfileArea(g.profile), into = 0;
        var kept = new List<float>();
        for (int k = 0; k < ProfileRadii; k++) kept.Add((float)Math.Min(g.profile[k], TargetRadius(g, k)));
        into = ProfileArea(kept);
        return have > 0 ? CraftMath.Clamp01(1 - into / have) : 0;
    }

    /// <summary>The outline of what is left, seen from the planned stone's centre (32 radii).</summary>
    public static List<float> Outline(GemState g)
    {
        var r = new List<float>();
        for (int k = 0; k < ProfileRadii; k++)
        {
            double t = k * Math.PI * 2 / ProfileRadii, cx = Math.Cos(t), cy = Math.Sin(t), lo = 0, hi = 2.2;
            for (int it = 0; it < 18; it++)
            {
                double m = (lo + hi) / 2;
                if (Inside(g, g.windowX + cx * m, g.windowY + cy * m)) lo = m; else hi = m;
            }
            r.Add((float)lo);
        }
        return r;
    }

    /// <summary>
    /// Grinds the preform on the wheel toward the planned outline: <c>points</c> = (angle °, amount 0-1) pairs where
    /// the stone was pressed to the wheel and how hard, <c>a</c> = coolant (0-1), <c>b</c> = how steady the hands were.
    /// Each touch takes material off round that part of the outline (slower on a hard stone); pressing on past the
    /// outline grinds into the planned stone (it comes out smaller and lopsided). Dry grinding heats it.
    /// </summary>
    public static void Grind(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        GemState g = p.gem;
        CraftGemDefinition d = ws.Rules.Gem(g.gem);
        CraftGemCut cut = ws.Rules.Cut(g.cut);
        if (d == null || cut == null)
            return;
        if (g.profile.Count != ProfileRadii)
        {
            g.profile.Clear();
            g.profile.AddRange(Outline(g));
        }
        double before = ProfileArea(g.profile), steady = CraftMath.Clamp01(op.b), cool = CraftMath.Clamp01(op.a);
        double rate = 0.05 * ws.ToolQuality * 7 / Math.Max(4, d.hardness) * (0.6 + 0.4 * steady), heatIn = 0;
        for (int i = 0; i + 1 < op.points.Count; i += 2)
        {
            double t = ((op.points[i] % 360) + 360) % 360, amount = CraftMath.Clamp01(op.points[i + 1]);
            double at = t / 360 * ProfileRadii;
            int k0 = (int)Math.Floor(at);
            double f = at - k0;
            // the wheel's face is wider than one radius: it takes the neighbours too (more where it touched)
            for (int j = -2; j <= 3; j++)
            {
                int k = ((k0 + j) % ProfileRadii + ProfileRadii) % ProfileRadii;
                double dist = Math.Abs(j - f), w = Math.Max(0, 1 - dist / 2.2);
                g.profile[k] = (float)Math.Max(0.05, g.profile[k] - amount * rate * w);
            }
            // touch by touch: the same pressing heats it the same however the touches were sent
            heatIn += GrindHeat(amount, cool, d.heatSensitive);
        }
        double after = Math.Max(1e-4, ProfileArea(g.profile));
        g.carats = Math.Max(0.01, g.carats * Math.Pow(Math.Min(1, after / Math.Max(1e-4, before)), 1.5));
        // how far it is from the planned outline
        double into = 0, tsum = 0, mean = 0, sq = 0;
        var ratio = new double[ProfileRadii];
        for (int k = 0; k < ProfileRadii; k++)
        {
            double tr = TargetRadius(g, k);
            tsum += tr;
            into += Math.Max(0, tr - g.profile[k]);
            ratio[k] = Math.Min(1.3, g.profile[k] / Math.Max(1e-4, tr));
            mean += ratio[k];
        }
        mean /= ProfileRadii;
        for (int k = 0; k < ProfileRadii; k++) sq += (ratio[k] - mean) * (ratio[k] - mean);
        g.overground = CraftMath.Clamp01(into / Math.Max(1e-4, tsum));
        double excess = GroundExcess(g);
        // lopsided: ground into the outline, or (once the excess is off) uneven round it
        g.shapeError = CraftMath.Clamp01(g.overground * 2.5 + (excess < 0.2 ? Math.Sqrt(sq / ProfileRadii) * 1.5 : 0));
        g.inclusions.RemoveAll(i =>
        {
            double dx = i.x - g.windowX, dy = i.y - g.windowY, t = Math.Atan2(dy, dx);
            if (t < 0) t += Math.PI * 2;
            int k = (int)Math.Round(t / (Math.PI * 2) * ProfileRadii) % ProfileRadii;
            return Math.Sqrt(dx * dx + dy * dy) > g.profile[k] + i.size * 0.5;
        });
        // the wheel takes the crust with it
        for (int k = 0; k < g.crust.Count; k++) g.crust[k] = (float)Math.Max(0, g.crust[k] - 0.15 * op.points.Count / 2.0 / Math.Max(1, CrustSectors));
        if (heatIn > 0)
        {
            // dry and hard against the wheel the stone heats - a sensitive one cracks once it is hot enough
            g.heat = CraftMath.Clamp01(g.heat + heatIn);
            if (d.heatSensitive && g.heat >= GrindCrackHeat) Worsen(p, CraftDefectKind.Crack, g.heat, false, "ground dry, the heat cracked it");
        }
        if (excess <= 0.15 && g.stage == CraftGemStage.Rough)
            g.stage = CraftGemStage.Preformed;
        string how = g.overground > 0.08 ? "Ground - into the planned stone" : excess <= 0.15 ? (g.shapeError < 0.1 ? "Ground to its outline" : "Ground to an uneven outline") : $"Ground ({excess * 100:0}% still to take off)";
        p.Record(ws.Elapsed, CraftOpKind.GrindGem, how, excess);
    }

    // ------------------------------------------------------------------ faceting and polishing
    /// <summary>
    /// Where a tier's botched facets are kept among the stone's flaws (<see cref="CraftDefect.at"/> = -2 - the tier): a
    /// flaw of that tier alone, apart from the stone's other chips (at -1), so recutting the tier takes it away.
    /// </summary>
    public static int BotchAt(int tier) => -2 - Math.Max(0, tier);

    /// <summary>
    /// How a tier comes out of the hits that cut its facets on the timing bar (each hit's offset from the target, in Poor
    /// zones, and its score): the angle off the quill and how the facets meet, both as far off as the hits were on the
    /// whole; how far off the index stops they were; how unevenly they were cut - facets cut alternately steep and
    /// shallow are uneven however well their mean lies, as are hits of mixed quality; and the botched ones (the misses).
    /// One rule for the window and the tests.
    /// </summary>
    public static void TierFromHits(IReadOnlyList<CraftTimingHit> hits, out double angleOff, out double indexError, out double meet, out double asymmetry, out int misses)
    {
        angleOff = indexError = meet = asymmetry = 0;
        misses = 0;
        int n = hits?.Count ?? 0;
        if (n == 0)
            return;
        double meanOff = 0, mean = 0, sqScore = 0, sqOff = 0;
        foreach (CraftTimingHit h in hits)
        {
            double off = CraftMath.Clamp(h.offset, -1.5, 1.5);
            angleOff += off * 2.5;
            indexError += (1 - h.score) * 0.5;
            meet += CraftMath.Clamp(off * 0.8, -1, 1);
            meanOff += off;
            mean += h.score;
            if (h.damage) misses++;
        }
        angleOff /= n; indexError /= n; meet /= n; meanOff /= n; mean /= n;
        foreach (CraftTimingHit h in hits)
        {
            double off = CraftMath.Clamp(h.offset, -1.5, 1.5);
            sqScore += (h.score - mean) * (h.score - mean);
            sqOff += (off - meanOff) * (off - meanOff);
        }
        asymmetry = CraftMath.Clamp01(Math.Sqrt(sqOff / n) * 0.6 + Math.Sqrt(sqScore / n) * 2.5 + (1 - mean) * 0.3);
    }

    /// <summary>
    /// Cuts a tier of facets (<c>i0</c> = the tier, <c>i1</c> = facets cut): <c>a</c> = the angle set on the quill (°),
    /// <c>b</c> = how far off the index stops it was (0-1), <c>c</c> = how the facets met (-1 short - +1 overcut),
    /// <c>d</c> = coolant (0-1). Recutting a tier takes more weight off, and grinds its botched facets away.
    /// </summary>
    public static void Facet(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        GemState g = p.gem;
        CraftGemDefinition d = ws.Rules.Gem(g.gem);
        CraftGemCut cut = ws.Rules.Cut(g.cut);
        if (d == null || cut == null || op.i0 < 0 || op.i0 >= cut.tiers.Count)
            return;
        CraftCutTier tier = cut.tiers[op.i0];
        double tq = ws.ToolQuality;
        double err = op.a - IdealAngle(tier, d.refractiveIndex);
        // a worn machine's quill does not hold its angle
        err += Math.Sign(err == 0 ? 1 : err) * (1 / tq - 1) * 0.8;
        if (g.stage < CraftGemStage.Faceted && g.facets.Count == 0)
        {
            double rw = WindowRadius(g);
            // the planned stone is what is left: ground to its outline on the wheel, the facets take it from there; never
            // ground, the first tier grinds the preform down freehand (to the same planned outline, but lopsided)
            bool ground = g.profile.Count == ProfileRadii;
            g.carats = PlannedCarats(g, cut);
            if (!ground)
            {
                g.shapeError = Math.Max(g.shapeError, 0.25);
                ws.Raise(CraftEventKind.Note, p.id, "freehand", "Ground freehand on the lap, the outline comes out uneven: grind the preform first.");
            }
            g.inclusions.RemoveAll(i => !Kept(g, i));
            g.crust.Clear(); // the lap grinds through the skin, crust and all
            if (rw > 0.9)
            {
                g.chips = CraftMath.Clamp01(g.chips + 0.1);
                p.AddDefect(CraftDefectKind.Chip, -1, 0.15, true, "the stone was planned so near the rough's skin that the girdle has pits (naturals)");
            }
        }
        CraftFacetRecord rec = g.facets.Find(f => f.tier == op.i0);
        if (rec != null)
        {
            // recut: the tier is ground afresh (a little weight), and its botched facets go - their chips with them
            g.carats *= 0.97;
            g.chips = Math.Max(0, g.chips - rec.botchChips);
            rec.botchChips = 0;
            p.defects.RemoveAll(x => x.kind == CraftDefectKind.Chip && x.at == BotchAt(op.i0));
        }
        else
            g.facets.Add(rec = new CraftFacetRecord { tier = op.i0 });
        rec.facets = Math.Max(1, op.i1);
        rec.angleError = err;
        rec.indexError = CraftMath.Clamp01(op.b);
        rec.meet = CraftMath.Clamp(op.c, -1, 1);
        rec.asymmetry = CraftMath.Clamp01(op.e);
        rec.misses = Math.Max(0, Math.Min(rec.facets, op.i2));
        if (rec.misses > 0)
        {
            // a facet cut through its neighbour, or chipped at the meet: the tier's own flaw, until it is recut
            g.carats *= 1 - 0.01 * rec.misses;
            double chipped = g.chips;
            g.chips = CraftMath.Clamp01(g.chips + 0.03 * rec.misses * (1.2 - d.toughness));
            rec.botchChips = g.chips - chipped;
            p.AddDefect(CraftDefectKind.Chip, BotchAt(op.i0), CraftMath.Clamp01(0.08 * rec.misses), true, $"{tier.name.ToLowerInvariant()}: {rec.misses} facet{(rec.misses == 1 ? "" : "s")} botched - recut the tier");
        }
        if (tier.kind == CraftCutTierKind.Pavilion && !cut.cabochon && Math.Abs(err) > 3)
            ws.Raise(CraftEventKind.Note, p.id, "pavilion", err < 0 ? "Light falls straight through the shallow pavilion." : "The deep pavilion goes dark.");
        if (rec.meet > 0)
            g.carats *= 1 - rec.meet * 0.04;
        double cool = CraftMath.Clamp01(op.d);
        if (d.heatSensitive && cool < 0.4)
        {
            g.heat = CraftMath.Clamp01(g.heat + (0.4 - cool) * 0.6);
            p.AddDefect(CraftDefectKind.Crack, -1, (0.4 - cool) * 0.8, false, "ground dry, the heat cracked it");
        }
        g.scratches = Math.Max(0, g.scratches - 0.2);
        if (g.facets.Count >= cut.tiers.Count && g.stage < CraftGemStage.Faceted)
        {
            g.stage = CraftGemStage.Faceted;
            double v = g.carats * 200 / Math.Max(1, d.specificGravity); // mm³
            g.girdle = Math.Round(Math.Pow(v / (cut.cabochon ? 0.3 : 0.35), 1.0 / 3) / Math.Sqrt(Math.Max(0.5, cut.lengthRatio)), 2);
        }
        string how = rec.misses > 0 ? $"{rec.misses} facet{(rec.misses == 1 ? "" : "s")} botched" : rec.asymmetry > 0.35 ? "cut unevenly" : Math.Abs(err) <= 1 && Math.Abs(rec.meet) < 0.25 ? "cut true" : Math.Abs(err) > 3 ? "cut off its angle" : rec.meet < -0.4 ? "the facets do not meet" : rec.meet > 0.4 ? "cut too deep" : "cut";
        p.Record(ws.Elapsed, CraftOpKind.FacetGem, $"{tier.name}: {how}", err);
    }

    /// <summary>Regions of a stone the polish is followed over, and the polish past which a region's edges round.</summary>
    public const int PolishRegions = 9;
    public const float OverPolish = 2.5f;
    /// <summary>The polish region of the table (the middle of the 3 × 3).</summary>
    public const int TableRegion = 4;
    /// <summary>A region's polish below which it is a dull patch (its shine 1 - e^(-1.6 × polish) under about 0.62).</summary>
    public const float DullPolish = 0.6f;

    /// <summary>How well a compound polishes a stone: one as hard as the stone well, a little softer slowly, much softer hardly at all.</summary>
    public static double CompoundFit(CraftSupplyDefinition c, CraftGemDefinition d) =>
        c == null || d == null ? 0 : c.polishHardness >= d.hardness - 0.5 ? 1 : c.polishHardness >= d.hardness - 1.5 ? 0.45 : 0.12;

    /// <summary>A compound's fit under which the lap slides over the stone (it is told so, pass by pass).</summary>
    public const double SoftCompound = 0.5;

    /// <summary>
    /// Does a pass with <paramref name="compound"/> charge the lap afresh - one of the compound taken from the bag (and
    /// refused without it)? The first pass on a stone does, and so does a change of compound; passes with the compound the
    /// lap is already charged with for it (<see cref="GemState.compound"/>) need nothing more.
    /// </summary>
    public static bool ChargesLap(GemState g, string compound) =>
        g == null || !string.Equals(g.compound ?? "", compound ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The seconds a pass of the lap takes (the window sends one each half second held): what a pass polishes is the time
    /// it spent on the stone - a pass that only grazed it polishes as little as that graze, time off the stone nothing.
    /// </summary>
    public const double PassSeconds = 0.5;

    /// <summary>
    /// A facet's meet made worse by <paramref name="x"/> (heat, rounding): further from 0 whichever way it was off - a meet
    /// of 0 goes short (rounded edges no longer meet). Neither heat nor polishing on ever brings a bad cut right.
    /// </summary>
    public static double WorseMeet(double meet, double x) => (meet > 0 ? 1 : -1) * Math.Min(1, Math.Abs(meet) + Math.Max(0, x));

    /// <summary>
    /// Polishes it on the lap: <c>id</c> = the compound, <c>i0</c> = passes, <c>a</c> = pressure (0-1), <c>b</c> = speed
    /// (0-1), <c>points</c> = the seconds the lap spent on each of the 9 regions (a pass is <see cref="PassSeconds"/>;
    /// none given: every region evenly, the whole passes). Every rule is per pass and follows the time on the stone, so it
    /// is the same however the passes were sent: a compound softer than the stone hardly polishes it (and it is said, pass
    /// by pass); the rate rises with the pressure up to <see cref="PolishFullRate"/>; heat (pressure × speed above
    /// <see cref="PolishHeatLimit"/>) crazes heat-sensitive stones and rounds the facets' edges; a table along a cleavage
    /// plane flakes under more than <see cref="FlakePressure"/> as far as the lap worked on it (its middle region loses its
    /// polish). The stone keeps the compound the lap is charged with (<see cref="GemState.compound"/>).
    /// </summary>
    public static void Polish(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return;
        GemState g = p.gem;
        CraftGemDefinition d = ws.Rules.Gem(g.gem);
        CraftSupplyDefinition c = ws.Rules.Supply(op.id);
        if (d == null || c == null)
            return;
        g.compound = c.id;
        int n = Math.Max(1, op.i0);
        double press = CraftMath.Clamp01(op.a), speed = CraftMath.Clamp01(op.b);
        double fit = CompoundFit(c, d);
        double rate = fit * (0.4 + 0.6 * Math.Min(1, press / PolishFullRate)) * (0.5 + 0.5 * speed) * Math.Min(1.2, ws.ToolQuality);
        // where on the stone the lap worked (9 regions: the table, the crown's ring, the pavilion's ring), for how long; a
        // pass evenly over every region if not said
        while (g.polishRegions.Count < PolishRegions) g.polishRegions.Add((float)(-Math.Log(Math.Max(1e-4, 1 - g.polish)) / 1.6));
        bool where = op.points.Count >= PolishRegions;
        double sum = 0;
        if (where)
            for (int k = 0; k < PolishRegions; k++) sum += Math.Max(0, op.points[k]);
        // a region's share of the passes (the whole passes spread evenly give each one 1 a pass): its seconds against the
        // passes' own - no more is counted than the passes took, and the time off the stone polishes nothing
        double perSecond = PolishRegions / Math.Max(sum, n * PassSeconds);
        double Share(int k) => where ? Math.Max(0, op.points[k]) * perSecond : 1;
        // passes' worth of time on the stone: its heat, the rounding and the scratches taken off follow it
        double worked = where ? n * Math.Min(1, sum / (n * PassSeconds)) : n;
        // a table along a cleavage plane flakes under more than the flake pressure, pass by pass, the harder the more -
        // as far as the lap pressed on the table (a pass that never touched it does not flake it)
        double onTable = Math.Min(1, Share(TableRegion));
        bool flaking = TableOnCleavage(d, g) && press > FlakePressure + 1e-6 && onTable > 0; // (a hand pressing exactly at the mark does not flake it)
        double flake = flaking ? n * onTable * (press - FlakePressure) / (1 - FlakePressure) : 0;
        double over = 0;
        for (int k = 0; k < PolishRegions; k++)
        {
            double before = g.polishRegions[k];
            double gain = 0.1125 * n * rate * Share(k) - (k == TableRegion ? FlakeLoss * flake : 0); // the flakes lift off the table
            g.polishRegions[k] = (float)Math.Max(0, before + gain);
            over += Math.Max(0, g.polishRegions[k] - Math.Max(before, OverPolish));
        }
        double shine = 0;
        foreach (float pr in g.polishRegions) shine += 1 - Math.Exp(-1.6 * pr);
        g.polish = CraftMath.Clamp01(shine / PolishRegions);
        g.scratches = Math.Max(0, g.scratches * Math.Exp(-0.3 * worked * rate));
        if (over > 0)
        {
            // polished on past bright: the lap rounds the facet edges and takes weight - it never puts a bad cut right
            g.carats *= 1 - 0.004 * over;
            foreach (CraftFacetRecord f in g.facets) f.meet = WorseMeet(f.meet, 0.04 * over);
            if (over > 0.3) ws.Raise(CraftEventKind.Note, p.id, "overpolished", "The facet edges are rounding: it is polished enough there.");
        }
        double heat = press * speed;
        if (heat > PolishHeatLimit && worked > 0)
        {
            g.heat = CraftMath.Clamp01(g.heat + (heat - PolishHeatLimit) * 0.1 * worked * (d.heatSensitive ? 1 : 0.25));
            // hot, the lap rounds the facet edges: the facets meet worse, whichever way they were off
            foreach (CraftFacetRecord f in g.facets) f.meet = WorseMeet(f.meet, (heat - PolishHeatLimit) * 0.05 * worked);
            if (d.heatSensitive && g.heat > 0.25)
            {
                Worsen(p, CraftDefectKind.Crack, g.heat, false, "polished hot: it crazed");
                ws.Raise(CraftEventKind.Note, p.id, "crazed", "Fine cracks craze the stone: it got too hot.");
            }
        }
        if (flaking)
        {
            // the flakes chip the table (its polish was taken back above, so the stage follows the regions)
            g.chips = CraftMath.Clamp01(g.chips + 0.05 * flake);
            Worsen(p, CraftDefectKind.Chip, CraftMath.Clamp01(0.15 + g.chips), true, "the table lay along the cleavage and flaked as it was polished");
            ws.Raise(CraftEventKind.Note, p.id, "flaked", "A flake lifts off the table.");
        }
        if (g.polish > 0.7 && g.stage == CraftGemStage.Faceted)
            g.stage = CraftGemStage.Polished;
        if (fit < SoftCompound) ws.Raise(CraftEventKind.Note, p.id, "soft_compound", "The lap slides over the stone: the compound is softer than it.");
        p.Record(ws.Elapsed, CraftOpKind.PolishGem, fit < SoftCompound ? $"Polished with {c.Name.ToLowerInvariant()} - too soft for it" : g.polish > 0.85 ? "Polished bright" : "Polished", g.polish);
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ the lapidary's bench and the faceting machine
    /// <summary>What keeps a stone from being worked here (null: nothing) - a check that changes nothing and raises nothing.</summary>
    private string GemProblem(string id, CraftTools tool, string missing, out CraftPiece p)
    {
        p = Piece(id);
        if ((Tools & tool) == 0)
            return missing;
        if (p == null || p.kind != CraftPieceKind.Gem)
            return "That is not a gemstone.";
        if (p.location != CraftLocation.Air)
            return "Take it out first.";
        return null;
    }

    private bool GemCheck(string id, CraftTools tool, string missing, out CraftPiece p, out string reason)
    {
        reason = "";
        string problem = GemProblem(id, tool, missing, out p);
        return problem == null || Refuse(problem, out reason, id);
    }

    public bool TestGem(string id, CraftGemTest test, double care, double point, out string reason)
    {
        if (!GemCheck(id, CraftTools.GemBench | CraftTools.JewelersBench, "There is no gem bench here.", out _, out reason))
            return false;
        Record(new CraftOperation { kind = CraftOpKind.TestGem, piece = id, i0 = (int)test, a = (float)CraftMath.Clamp01(care), b = (float)CraftMath.Clamp(point, 1, 10) });
        return true;
    }

    /// <summary>Names the stone, once: the name sticks, right or wrong (a second name is refused).</summary>
    public bool IdentifyGem(string id, string gem, out string reason)
    {
        if (!GemCheck(id, CraftTools.GemBench | CraftTools.JewelersBench, "There is no gem bench here.", out CraftPiece p, out reason))
            return false;
        if (Rules.Gem(gem) == null)
            return Refuse("There is no such gemstone.", out reason, id);
        if (!string.IsNullOrEmpty(p.gem.identifiedAs))
            return Refuse($"You named it {Rules.Gem(p.gem.identifiedAs)?.Name.ToLowerInvariant() ?? p.gem.identifiedAs}: the name sticks.", out reason, id);
        if (CraftLapidary.Tests(p.gem) < 2)
            return Refuse("Test it first: look at it, scratch it, read it - at least two tests.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.IdentifyGem, piece = id, id = gem });
        return true;
    }

    public bool EvaluateGem(string id, IList<float> looks, double steadiness, out string reason)
    {
        if (!GemCheck(id, CraftTools.GemBench | CraftTools.JewelersBench, "There is no gem bench here.", out _, out reason))
            return false;
        var op = new CraftOperation { kind = CraftOpKind.EvaluateGem, piece = id, a = (float)CraftMath.Clamp01(steadiness) };
        if (looks != null)
            for (int i = 0; i + 2 < looks.Count && i < 6000; i += 3)
            {
                op.points.Add((float)CraftMath.Clamp(looks[i], -1, 1));
                op.points.Add((float)CraftMath.Clamp(looks[i + 1], -1, 1));
                op.points.Add((float)CraftMath.Clamp01(looks[i + 2]));
            }
        Record(op);
        return true;
    }

    /// <summary>
    /// Plans the cut (marks the stone out). Refused once faceting has begun, and once the stone is ground on the wheel to
    /// its planned outline: the plan is then fixed (a new plan would grind its carats off a second time).
    /// </summary>
    public bool OrientGem(string id, string cut, double tableAngle, double x, double y, double radius, out string reason)
    {
        if (!GemCheck(id, CraftTools.GemBench, "There is no gem bench here.", out CraftPiece p, out reason))
            return false;
        CraftGemCut c = Rules.Cut(cut);
        CraftGemDefinition d = Rules.Gem(p.gem.gem);
        if (c == null)
            return Refuse("There is no such cut.", out reason, id);
        if (p.gem.stage >= CraftGemStage.Faceted || p.gem.facets.Count > 0)
            return Refuse("It is already being cut.", out reason, id);
        if (p.gem.profile.Count == CraftLapidary.ProfileRadii)
            return Refuse("It is ground to its planned outline: the plan is fixed.", out reason, id);
        if (d != null && d.opaque && !c.cabochon)
            return Refuse("It is opaque: facets would show nothing - cut it en cabochon.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.OrientGem, piece = id, id = c.id, a = (float)CraftMath.Clamp(tableAngle, 0, 90), b = (float)CraftMath.Clamp(x, -1, 1), c = (float)CraftMath.Clamp(y, -1, 1), d = (float)CraftMath.Clamp(radius, 0.15, 1) });
        return true;
    }

    /// <summary>Can the stone be sawn or parted along the line (<paramref name="angle"/>°, <paramref name="offset"/>): not faceted yet, and the line runs through what is left of it?</summary>
    private bool LineCheck(CraftPiece p, double angle, double offset, out string reason)
    {
        reason = "";
        if (p.gem.stage >= CraftGemStage.Faceted || p.gem.facets.Count > 0)
            return Refuse("It is already being faceted.", out reason, p.id);
        // a line through nothing of the stone saws nothing, and costs nothing
        if (Math.Abs(offset) >= 0.97 || !CraftLapidary.LineCuts(p.gem, angle, offset))
            return Refuse("That misses the stone.", out reason, p.id);
        return true;
    }

    /// <summary>
    /// Splits it along a line with one blow (its force on the split's timing bar): along its cleavage plane - only a Good
    /// or Perfect cleavage can be followed - or, a stone with no cleavage worth following and not too tough, nicked with
    /// the saw and snapped (<paramref name="snap"/>). The line is where it was drawn, whichever way
    /// (<see cref="CraftLapidary.NormalLine"/>); one that misses what is left of the stone is refused. The refusals do not
    /// name the stone (it may not be known yet).
    /// </summary>
    public bool CleaveGem(string id, double angle, double offset, double force, out string reason, bool snap = false)
    {
        if (!GemCheck(id, CraftTools.GemBench, "There is no gem bench here.", out CraftPiece p, out reason))
            return false;
        CraftGemDefinition d = Rules.Gem(p.gem.gem);
        if (d == null)
            return Refuse("That is not a gemstone.", out reason, id);
        if (snap && !CraftLapidary.Snappable(d))
            return Refuse(d.cleavage >= CraftCleavage.Good ? "It cleaves: split it along its plane." : "It is too tough to snap where it is nicked: saw it.", out reason, id);
        if (!snap && d.cleavage < CraftCleavage.Good)
            return Refuse(d.cleavage == CraftCleavage.None
                ? CraftLapidary.Snappable(d) ? "It has no cleavage: nick and snap it, or saw it." : "It has no cleavage and is too tough to snap: saw it."
                : CraftLapidary.Snappable(d) ? "Its cleavage is too poor to follow: nick and snap it, or saw it." : "Its cleavage is too poor to follow: saw it.", out reason, id);
        CraftLapidary.NormalLine(angle, offset, out double a, out double b);
        if (!LineCheck(p, a, b, out reason))
            return false;
        Record(new CraftOperation { kind = CraftOpKind.CleaveGem, piece = id, i0 = snap ? 1 : 0, a = (float)a, b = (float)CraftMath.Clamp(b, -1, 1), c = (float)CraftMath.Clamp01(force) });
        return true;
    }

    /// <summary>Cleans the rough with a brush (<paramref name="pick"/> false) or a pick: <paramref name="work"/> = how much each of its 16 sectors was worked.</summary>
    public bool CleanGem(string id, bool pick, IList<float> work, double control, out string reason)
    {
        if (!GemCheck(id, CraftTools.GemBench | CraftTools.JewelersBench, "There is no gem bench here.", out CraftPiece p, out reason))
            return false;
        if (p.gem.stage >= CraftGemStage.Faceted)
            return Refuse("It is already cut: there is nothing on it to clean off.", out reason, id);
        var op = new CraftOperation { kind = CraftOpKind.CleanGem, piece = id, i0 = pick ? 1 : 0, a = (float)CraftMath.Clamp01(control) };
        for (int k = 0; k < CraftLapidary.CrustSectors; k++)
            op.points.Add(work != null && k < work.Count ? (float)CraftMath.Clamp(work[k], 0, 3) : 0f);
        Record(op);
        return true;
    }

    /// <summary>Grinds the preform toward its planned outline: <paramref name="touches"/> = (angle °, amount 0-1) pairs.</summary>
    public bool GrindGem(string id, IList<float> touches, double coolant, double steadiness, out string reason)
    {
        if (!GemCheck(id, CraftTools.GemBench | CraftTools.FacetingMachine, "There is no grinding wheel here.", out CraftPiece p, out reason))
            return false;
        if (!p.gem.oriented || Rules.Cut(p.gem.cut) == null)
            return Refuse("Orient it first: the wheel needs an outline to grind to.", out reason, id);
        if (p.gem.stage >= CraftGemStage.Faceted || p.gem.facets.Count > 0)
            return Refuse("It is already being faceted.", out reason, id);
        if (p.gem.profile.Count == 0 && CraftLapidary.Excess(p.gem) > CraftLapidary.GrindableExcess)
            return Refuse("Saw the bulk of the rough off first: there is too much to grind away.", out reason, id);
        var op = new CraftOperation { kind = CraftOpKind.GrindGem, piece = id, a = (float)CraftMath.Clamp01(coolant), b = (float)CraftMath.Clamp01(steadiness) };
        if (touches != null)
            for (int i = 0; i + 1 < touches.Count && i < 800; i += 2)
            {
                op.points.Add((float)(((touches[i] % 360) + 360) % 360));
                op.points.Add((float)CraftMath.Clamp01(touches[i + 1]));
            }
        if (op.points.Count == 0)
            return Refuse("Touch it to the wheel.", out reason, id);
        Record(op);
        return true;
    }

    /// <summary>
    /// Saws it along a line (its direction °, its offset: where it was drawn, whichever way -
    /// <see cref="CraftLapidary.NormalLine"/>); a line that misses what is left of the stone is refused, and costs nothing.
    /// </summary>
    public bool SawGem(string id, double angle, double offset, double steadiness, double coolant, double pressure, out string reason, int misses = 0)
    {
        if (!GemCheck(id, CraftTools.GemBench, "There is no trim saw here.", out CraftPiece p, out reason))
            return false;
        CraftLapidary.NormalLine(angle, offset, out double a, out double b);
        if (!LineCheck(p, a, b, out reason))
            return false;
        Record(new CraftOperation { kind = CraftOpKind.SawGem, piece = id, i0 = Math.Max(0, Math.Min(50, misses)), a = (float)a, b = (float)CraftMath.Clamp(b, -1, 1), c = (float)CraftMath.Clamp01(steadiness), d = (float)CraftMath.Clamp01(coolant), e = (float)CraftMath.Clamp01(pressure) });
        return true;
    }

    /// <summary>
    /// Can <paramref name="tier"/> of the stone's cut be cut now (the faceting machine here, the stone planned, its rough
    /// sawn or ground off, not polished, the tiers in order)? Changes nothing and raises nothing: the window asks it before
    /// the first facet of a tier is cut, so no hit is spent on a tier the station would refuse. <see cref="FacetGem"/>
    /// refuses with the same words.
    /// </summary>
    public bool CanFacet(string id, int tier, out string reason)
    {
        reason = FacetProblem(id, tier) ?? "";
        return reason.Length == 0;
    }

    private string FacetProblem(string id, int tier)
    {
        string problem = GemProblem(id, CraftTools.FacetingMachine, "There is no faceting machine here.", out CraftPiece p);
        if (problem != null)
            return problem;
        CraftGemCut c = Rules.Cut(p.gem.cut);
        if (!p.gem.oriented || c == null)
            return "Orient it first: choose the cut and where the stone sits in the rough.";
        if (p.gem.stage == CraftGemStage.Polished)
            return "It is already polished.";
        if (p.gem.facets.Count == 0 && p.gem.profile.Count == CraftLapidary.ProfileRadii && CraftLapidary.GroundExcess(p.gem) > 0.15)
            return "Grind it closer to its outline first.";
        if (p.gem.stage == CraftGemStage.Rough && CraftLapidary.Excess(p.gem) > CraftLapidary.PreformExcess)
            return "Saw (or cleave) the excess rough off first.";
        if (tier < 0 || tier >= c.tiers.Count)
            return "The cut has no such tier.";
        bool cut = p.gem.facets.Exists(f => f.tier == tier);
        if (!cut && tier != p.gem.facets.Count)
            return $"Cut the tiers in order: next is the {c.tiers[Math.Min(p.gem.facets.Count, c.tiers.Count - 1)].name.ToLowerInvariant()}.";
        return null;
    }

    /// <summary>
    /// The facets of a tier of the stone's cut (the cut decides how many a tier has; 0 when there is no such tier). The
    /// workshop cuts, and the guard times, a tier by its own count - never by the count a caller claims.
    /// </summary>
    public int TierFacets(string id, int tier)
    {
        CraftPiece p = Piece(id);
        CraftGemCut c = p != null && p.kind == CraftPieceKind.Gem ? Rules.Cut(p.gem.cut) : null;
        return c != null && tier >= 0 && tier < c.tiers.Count ? Math.Max(1, Math.Min(96, c.tiers[tier].facets)) : 0;
    }

    /// <summary>
    /// Cuts a tier of facets: as many facets as the cut gives the tier (<paramref name="facets"/>, the caller's count, is
    /// not trusted: <see cref="TierFacets"/>), at the quill's angle, the hits' index error, meet, unevenness and botches.
    /// </summary>
    public bool FacetGem(string id, int tier, int facets, double angle, double indexError, double meet, double coolant, out string reason, double asymmetry = 0, int misses = 0)
    {
        reason = "";
        string problem = FacetProblem(id, tier);
        if (problem != null)
            return Refuse(problem, out reason, id);
        int count = TierFacets(id, tier);
        Record(new CraftOperation { kind = CraftOpKind.FacetGem, piece = id, i0 = tier, i1 = count > 0 ? count : Math.Max(1, Math.Min(96, facets)), i2 = Math.Max(0, Math.Min(96, misses)), a = (float)CraftMath.Clamp(angle, 0, 90), b = (float)CraftMath.Clamp01(indexError), c = (float)CraftMath.Clamp(meet, -1, 1), d = (float)CraftMath.Clamp01(coolant), e = (float)CraftMath.Clamp01(asymmetry) });
        return true;
    }

    public bool PolishGem(string id, string compound, int passes, double pressure, double speed, out string reason, IList<float> regions = null)
    {
        if (!GemCheck(id, CraftTools.FacetingMachine | CraftTools.Polishing, "There is no polishing lap here.", out CraftPiece p, out reason))
            return false;
        if (Rules.Supply(compound)?.kind != CraftSupplyKind.Compound)
            return Refuse("That is not a polishing compound.", out reason, id);
        if (p.gem.stage < CraftGemStage.Faceted)
            return Refuse("Cut every tier first.", out reason, id);
        var op = new CraftOperation { kind = CraftOpKind.PolishGem, piece = id, id = compound, i0 = Math.Max(1, Math.Min(30, passes)), a = (float)CraftMath.Clamp01(pressure), b = (float)CraftMath.Clamp01(speed) };
        if (regions != null && regions.Count >= CraftLapidary.PolishRegions)
            for (int k = 0; k < CraftLapidary.PolishRegions; k++) op.points.Add((float)CraftMath.Clamp(regions[k], 0, 3));
        Record(op);
        return true;
    }
}
