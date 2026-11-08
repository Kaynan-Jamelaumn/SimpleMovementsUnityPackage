using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The metallurgical state of one part of a segment's cross-section: the <b>edge</b> (the thin part toward the cutting
/// edge, or the whole section when there is no bevel) or the <b>body</b> (the spine and the middle). They heat and cool
/// at their own pace (a thin edge cools first in the quench and hardens more), so a blade can be hard at the edge and
/// tough at the spine.
/// </summary>
[Serializable]
public class CraftZone
{
    [Tooltip("°C.")]
    public double temp = 18;
    [Tooltip("Last step's temperature (for the cooling rate).")]
    public double prevTemp = 18;
    [Tooltip("Steel only: fraction that is austenite (above the critical temperature).")]
    public double aust;
    [Tooltip("Fraction that is martensite (hard, brittle until tempered).")]
    public double mart;
    [Tooltip("Fraction that is ferrite and pearlite (soft) - all of a metal that does not harden.")]
    public double pearl = 1;
    [Tooltip("How fine that pearlite is (0 coarse, annealed - 1 fine, normalized).")]
    public double fine = 0.3;
    [Tooltip("How far the austenite has gone toward turning into pearlite while cooling (1 = it starts to).")]
    public double progress;
    [Tooltip("Martensite fraction that would have formed at the lowest temperature reached so far (Koistinen-Marburger).")]
    public double martReached;
    [Tooltip("The tempering reached by the martensite (Hollomon-Jaffe parameter; 0 = untempered).")]
    public double temperP;
    [Tooltip("Grain size (1 fine; it grows when held too hot - coarse grain is weak).")]
    public double grain = 1;
    [Tooltip("Cold work (worked below its recrystallisation temperature): harder, less ductile, until annealed.")]
    public double cold;
    [Tooltip("Residual stress (from the quench, cold work): cracks it if too high; tempering relieves it.")]
    public double stress;
    [Tooltip("Surface carbon lost (0-1).")]
    public double decarb;
    [Tooltip("How burnt it is (0-1): irreversible.")]
    public double burnt;
    [Tooltip("Seconds spent fully austenitic in this heat.")]
    public double soak;

    public CraftZone Clone() => (CraftZone)MemberwiseClone();

    public void CopyFrom(CraftZone z)
    {
        temp = z.temp; prevTemp = z.prevTemp; aust = z.aust; mart = z.mart; pearl = z.pearl; fine = z.fine; progress = z.progress;
        martReached = z.martReached; temperP = z.temperP; grain = z.grain; cold = z.cold; stress = z.stress; decarb = z.decarb;
        burnt = z.burnt; soak = z.soak;
    }

    /// <summary>A weighted mix of two zones (resampling, welding).</summary>
    public static void Mix(CraftZone into, CraftZone a, double wa, CraftZone b, double wb)
    {
        double s = wa + wb;
        if (s <= 1e-12) { into.CopyFrom(a); return; }
        double x = wa / s, y = wb / s;
        into.temp = a.temp * x + b.temp * y;
        into.prevTemp = a.prevTemp * x + b.prevTemp * y;
        into.aust = a.aust * x + b.aust * y;
        into.mart = a.mart * x + b.mart * y;
        into.pearl = a.pearl * x + b.pearl * y;
        into.fine = a.fine * x + b.fine * y;
        into.progress = a.progress * x + b.progress * y;
        into.martReached = a.martReached * x + b.martReached * y;
        into.temperP = Math.Max(a.temperP, b.temperP) * 0.5 + (a.temperP * x + b.temperP * y) * 0.5;
        into.grain = a.grain * x + b.grain * y;
        into.cold = a.cold * x + b.cold * y;
        into.stress = a.stress * x + b.stress * y;
        into.decarb = a.decarb * x + b.decarb * y;
        into.burnt = a.burnt * x + b.burnt * y;
        into.soak = Math.Min(a.soak, b.soak);
    }
}

/// <summary>
/// One slice of a metal (or wooden) piece along its length: its dimensions and cross-section (bevels toward the edge,
/// a fuller, a hole), its bend and dish, its surface (scale, roughness, polish, the colours of heat on bright steel),
/// its edge (apex, burr, chips), what is trapped in it (slag, porosity), and the metallurgy of its edge and body.
/// </summary>
[Serializable]
public class MetalSegment
{
    [Tooltip("mm along the piece.")]
    public double length = 10;
    [Tooltip("mm across (the flat).")]
    public double width = 30;
    [Tooltip("mm thick at the spine.")]
    public double thickness = 6;
    [Tooltip("The bevels toward the edge on the left and right face: how much thickness they take off at the edge, and how far up the width they reach (mm).")]
    public double bevelDepthL, bevelWidthL, bevelDepthR, bevelWidthR;
    [Tooltip("Depth of a fuller groove (mm, both faces together).")]
    public double fuller;
    [Tooltip("Bend over this segment (degrees; positive curves toward the spine, like a sabre).")]
    public double bend;
    [Tooltip("Dish of a plate here (mm).")]
    public double dish;
    [Tooltip("Diameter of a hole here (an eye, a slot, a socket), mm.")]
    public double hole;
    [Tooltip("How far a cut on the hardy or with the hot chisel has gone through (0-1).")]
    public double cut;
    [Tooltip("The cut is driven down from above (the hot chisel), not up from below (the hardy): the last blow's tool decides.")]
    public bool cutFromAbove;
    [Tooltip("This segment's carbon above or below the piece's (a bloom is uneven).")]
    public double carbonShift;
    [Tooltip("Grams of slag trapped in it.")]
    public double slag;
    [Tooltip("Porosity (0-1): a bloom's sponge, gas holes in a casting.")]
    public double porosity;
    [Tooltip("Oxide scale on it (mm).")]
    public double scale;
    [Tooltip("Scale hammered in (pit depth, mm).")]
    public double pit;
    [Tooltip("Surface roughness (µm): hammer marks, file and wheel scratches, polish.")]
    public double rough = 40;
    [Tooltip("How clean and bright the surface is (0 black, scaled - 1 polished): heat colours show on bright steel.")]
    public double polish;
    [Tooltip("The hottest it has been since it was last polished (°C): the colour of its oxide film.")]
    public double temperFilm;
    [Tooltip("A protective coating (oil, wax, a blued finish), 0-1.")]
    public double coating;
    [Tooltip("Edge apex radius (µm): small is sharp. Only meaningful where there is an edge.")]
    public double apex = 300;
    [Tooltip("The whetstone's micro-bevel (inclusive degrees; 0 = none).")]
    public double microAngle;
    [Tooltip("The micro-bevel's half-angle on each face (degrees), and how far each face has been honed to the apex (0-1).")]
    public double microL, microR, honeL, honeR;
    [Tooltip("A burr along the edge (0-1).")]
    public double burr;
    [Tooltip("Damage along the edge (0-1): chips, a rolled edge.")]
    public double chip;
    [Tooltip("Clay on it (mm): it cools slower in the quench (a soft, tough spine).")]
    public double clay;
    [Tooltip("Accumulated strain toward a crack (forging too cold, a brittle metal).")]
    public double crackRisk;
    [Tooltip("How much of a forge weld's seam here is still open (0 none - 1 just folded or stacked): setting blows at welding heat close it; one that cools open is a delamination.")]
    public double seamOpen;
    [Tooltip("Flux on this stretch (0-1): molten from a red heat, it takes the scale off and keeps new scale off, so a weld can take.")]
    public double flux;
    [Tooltip("Wood: bark still on this stretch (0-1).")]
    public double bark;
    public CraftZone edge = new CraftZone();
    public CraftZone body = new CraftZone();

    public MetalSegment Clone()
    {
        var s = (MetalSegment)MemberwiseClone();
        s.edge = edge.Clone();
        s.body = body.Clone();
        return s;
    }

    /// <summary>Thickness left at the edge after the bevels (mm).</summary>
    public double EdgeThickness => Math.Max(0, thickness - bevelDepthL - bevelDepthR);

    /// <summary>The bevel half-angles (degrees) of the left and right face.</summary>
    public double AngleL => bevelWidthL > 1e-6 ? Math.Atan(bevelDepthL / bevelWidthL) * 180 / Math.PI : 0;
    public double AngleR => bevelWidthR > 1e-6 ? Math.Atan(bevelDepthR / bevelWidthR) * 180 / Math.PI : 0;

    /// <summary>The edge's inclusive angle (degrees): the micro-bevel when there is one, else the two bevels.</summary>
    public double EdgeAngle => microAngle > 0 ? microAngle : AngleL + AngleR > 0 ? AngleL + AngleR : 180;

    /// <summary>Is it beveled at all (an edge has been ground or forged)?</summary>
    public bool Beveled => bevelDepthL + bevelDepthR > 1e-4;

    /// <summary>Cross-section (mm²) with <paramref name="edges"/> beveled edges and the fuller.</summary>
    public double Area(int edges)
    {
        double a = width * thickness;
        double bevels = 0.5 * (bevelDepthL * bevelWidthL + bevelDepthR * bevelWidthR);
        a -= bevels * Math.Max(1, edges);
        a -= fuller * width * 0.3;
        return Math.Max(1e-4, a);
    }

    /// <summary>Volume (mm³): the cross-section along its length, less the hole.</summary>
    public double Volume(int edges)
    {
        double v = Area(edges) * length;
        if (hole > 0)
            v -= Math.PI * hole * hole * 0.25 * thickness;
        return Math.Max(1e-4, v);
    }

    /// <summary>Surface over volume (1/mm) of the body and of the edge zone: thin parts heat and cool fastest.</summary>
    public double BodyExposure => 2.0 / Math.Max(0.3, width) + 2.0 / Math.Max(0.3, thickness);

    public double EdgeExposure
    {
        get
        {
            double t = Beveled ? Math.Max(0.2, (EdgeThickness + thickness) * 0.5 * 0.6) : thickness;
            return 2.0 / Math.Max(0.3, width) + 2.0 / Math.Max(0.2, t);
        }
    }

    /// <summary>The average temperature of the segment.</summary>
    public double Temperature => (edge.temp + body.temp) * 0.5;
}

/// <summary>One fragment of an ore lump: its mass, what it is made of, its size and where it lies (the sorting tray).</summary>
[Serializable]
public class OreFragment
{
    [Tooltip("Grams.")]
    public double mass;
    [Tooltip("Shares of the fragment (they add up to 1): the ore mineral, the loose rock, dirt and clay.")]
    public double mineral, gangue, dirt;
    [Tooltip("Size (cm).")]
    public double size = 8;
    [Tooltip("Position on the tray (0-1).")]
    public double x = 0.5, y = 0.5;
    [Tooltip("How much of its surface each brush has gone over (0-1): a brush takes only so much of the dirt and the rind, however long it scrubs. A fresh break starts at 0.")]
    public double brushedSoft, brushedStiff, brushedWire;
    [Tooltip("How much of its rock is the weathered rind on its surface (0-1): what the wire brush knocks off. A lump's comes from its size; the stones a blow breaks it into share what was left of it with their rock - a fresh break has none of its own. -1: kept from before the rind was counted (a lump's share for its size).")]
    public double rind = -1;

    public OreFragment Clone() => (OreFragment)MemberwiseClone();

    /// <summary>The share of its rock that is the weathered rind (0-1): <see cref="rind"/>, or for a stone from before it was kept, the share a lump of its size has.</summary>
    public double RindShare => rind >= 0 ? CraftMath.Clamp01(rind) : CraftOres.Rind(size);

    /// <summary>How much of its surface <paramref name="brush"/> has gone over (0-1).</summary>
    public double Brushed(CraftOreBrush brush) => brush == CraftOreBrush.Soft ? brushedSoft : brush == CraftOreBrush.Stiff ? brushedStiff : brushedWire;

    public void SetBrushed(CraftOreBrush brush, double cover)
    {
        cover = CraftMath.Clamp01(cover);
        if (brush == CraftOreBrush.Soft) brushedSoft = cover;
        else if (brush == CraftOreBrush.Stiff) brushedStiff = cover;
        else brushedWire = cover;
    }
}

/// <summary>An ore lump's state: the deposit it came from, what its mineral holds, its fragments.</summary>
[Serializable]
public class OreState
{
    public string ore = "";
    [Tooltip("The deposit's seed and the lump's index: they decide what it holds.")]
    public int deposit;
    public int lump;
    [Tooltip("What a gram of its mineral gives (by element, g/g).")]
    public List<CraftAmount> bound = new List<CraftAmount>();
    public List<OreFragment> fragments = new List<OreFragment>();
    [Tooltip("Share of its sulfur driven off by roasting (0-1).")]
    public double roasted;
    [Tooltip("Share of its mineral reduced to metal in the furnace (0-1).")]
    public double reduced;
    [Tooltip("Water in it (0-1).")]
    public double moisture;
    [Tooltip("°C (in the furnace).")]
    public double temperature = 18;
    [Tooltip("Times it has been crushed (each crushing has its own deterministic breakage).")]
    public int crushes;

    public double Mass
    {
        get
        {
            double m = 0;
            foreach (OreFragment f in fragments) m += f.mass;
            return m;
        }
    }

    public double MineralMass
    {
        get
        {
            double m = 0;
            foreach (OreFragment f in fragments) m += f.mass * f.mineral;
            return m;
        }
    }

    public double Bound(string element)
    {
        foreach (CraftAmount a in bound)
            if (string.Equals(a.element, element, StringComparison.OrdinalIgnoreCase))
                return a.grams;
        return 0;
    }
}

/// <summary>One cell of a hide (the settings' Cell Size square).</summary>
[Serializable]
public class HideCell
{
    [Tooltip("False where the hide was cut away.")]
    public bool present = true;
    [Tooltip("mm.")]
    public double thickness = 4;
    [Tooltip("Flesh and fat left on the flesh side (0-1).")]
    public double flesh;
    [Tooltip("Hair on the grain side (0-1), and how loose the lime has made it (0-1).")]
    public double hair;
    public double hairLoose;
    [Tooltip("How intact the grain (the outer surface) is (0-1).")]
    public double grain = 1;
    [Tooltip("Holes, cuts and scores (0-1).")]
    public double damage;
    [Tooltip("A natural flaw (a scar, a brand, a wound).")]
    public bool flaw;
    [Tooltip("The damage the hide was born with here (its scars, 0-1): the rest of the damage is the work's (0 in saves older than this).")]
    public double scar;
    [Tooltip("Water in it (0-1).")]
    public double moisture = 0.7;
    [Tooltip("Tannage of the outside and of the core (0-1).")]
    public double tanS, tanC;
    [Tooltip("How open the outside still is to the liquor (1; it closes when tanned too fast).")]
    public double open = 1;
    [Tooltip("Stiffness (0 limp - 1 board-like).")]
    public double stiff;
    [Tooltip("Oil and wax worked in (0-1).")]
    public double oil, wax;
    [Tooltip("Dye coverage (0-1) and its colour.")]
    public double dye;
    public double dyeR, dyeG, dyeB;
    [Tooltip("How smooth the surface is (0-1).")]
    public double smooth;
    [Tooltip("Rot (0-1).")]
    public double rot;
    [Tooltip("How far it is stretched (strain, 0-0.3) and how far it shrank.")]
    public double stretch, shrink;
    [Tooltip("Hardened in hot water (cuir bouilli), 0-1.")]
    public double hard;

    public HideCell Clone() => (HideCell)MemberwiseClone();

    /// <summary>Average tannage through its thickness.</summary>
    public double Tannage => tanS * 0.35 + tanC * 0.65;
}

/// <summary>A hide's (or a cut leather part's) state: the grid of cells and what was done to all of it.</summary>
[Serializable]
public class HideState
{
    public string hide = "";
    public int seed;
    public int width, height;
    public List<HideCell> cells = new List<HideCell>();
    [Tooltip("The tanning agent that did most of the tanning (empty = raw).")]
    public string agent = "";
    [Tooltip("Shrink temperature now (°C, wet).")]
    public double shrinkTemperature = 60;
    [Tooltip("Hours spent raw and wet.")]
    public double hoursRaw;
    [Tooltip("Tension on each side of the frame (0-1): north, east, south, west.")]
    public double tensionN, tensionE, tensionS, tensionW;
    [Tooltip("Fire resistance from an alchemical tanning (0-1).")]
    public double fireResistance;
    [Tooltip("A cut part: the pattern it was cut with and its rotation (quarter turns); how cleanly it was cut (0-1).")]
    public string pattern = "";
    public int rotation;
    public double cutQuality = 1;
    [Tooltip("Edges bevelled and burnished (0-1).")]
    public double edgeFinish;
    [Tooltip("Cloth: the patterns laid out and chalked on it before cutting.")]
    public List<CraftLayoutPlacement> layout = new List<CraftLayoutPlacement>();
    [Tooltip("Has it dried since it was tanned? (leather is only finished dry)")]
    public bool dried;

    public HideCell Cell(int x, int y) => x >= 0 && y >= 0 && x < width && y < height ? cells[y * width + x] : null;
}

/// <summary>A piece of wood's own state (its shape is the piece's segments).</summary>
[Serializable]
public class WoodState
{
    public string wood = "";
    [Tooltip("Water in it (0-1): green wood shrinks and loosens a fit as it dries.")]
    public double moisture = 0.12;
    [Tooltip("Charred by hot metal (0-1).")]
    public double charred;
    [Tooltip("Split (0-1): from an overdriven wedge.")]
    public double split;
    [Tooltip("Oiled (0-1).")]
    public double oil;
    [Tooltip("How far the fibres run out of the piece (0 straight-grained - 1 cross-grained): rived wood follows its grain, sawn stock cuts across it.")]
    public double runout = 0.2;
    [Tooltip("Checks: cracks from drying too fast (0-1).")]
    public double checks;
    [Tooltip("How long it has steamed, for its thickness (0-1: through).")]
    public double steamed;
    [Tooltip("Finish (varnish or oil) built up (0-1), how even it is (0-1), and how wet the last coat still is (0-1).")]
    public double finish, finishEven = 1, finishWet;
    [Tooltip("In the drying stack: how evenly it is stickered (0-1), its ends sealed (0-1), the cover over it (0 open sun - 1 shade).")]
    public double stackEven = 0.5, endsSealed, shade = 0.5;
    [Tooltip("Still a round log (not yet split).")]
    public bool log;
    [Tooltip("Bark left on it (0-1, the mean of its stretches).")]
    public double bark;
    [Tooltip("Gouged into the wood while debarking (0-1).")]
    public double gouged;
    [Tooltip("Rot and beetle from bark left on wet wood (0-1).")]
    public double rot;
    [Tooltip("The finest grit it has been sanded with so far (0-1); -1 = never sanded.")]
    public double lastGrit = -1;
    [Tooltip("Wax rubbed in (0-1) and a hard resin film (0-1): sealers besides varnish and oil.")]
    public double wax, resin;
    [Tooltip("Process hours the last coat of varnish or oil takes to dry (its supply's; 0 = four).")]
    public double finishHours;
}

/// <summary>A set of fasteners: kind, count, size and what they are like.</summary>
[Serializable]
public class FastenerState
{
    public CraftFastenerKind kind = CraftFastenerKind.Rivet;
    [Min(0)] public int count;
    [Tooltip("Of the set fitted to an item, how many are set into its joints: they stay part of it (its mass and worth count all of the set); the rest are left to set (0 for a loose set and in older saves).")]
    [Min(0)] public int used;
    [Tooltip("Grams each.")]
    public double massEach = 5;
    [Tooltip("mm.")]
    public double diameter = 5, length = 14;
    [Tooltip("How well they were made (0-1): cast with flaws, cut unevenly.")]
    public double quality = 1;

    /// <summary>How many of the set are still to set (the count less those set into the item).</summary>
    public int Left => Math.Max(0, count - used);
}

/// <summary>Which piece fills a role of an assembly (the second of two bracers is index 1).</summary>
[Serializable]
public class AssemblyPart
{
    public string role = "";
    public int index;
    public string piece = "";
}

/// <summary>How a joint of an assembly has been made so far.</summary>
[Serializable]
public class JointState
{
    public string joint = "";
    [Tooltip("Finished (the item can be completed).")]
    public bool done;
    [Tooltip("Fit joints: the gap between the parts (mm; negative = it does not go on).")]
    public double gap;
    [Tooltip("Holes punched, and how evenly spaced and how well lined up (errors, 0 = perfect).")]
    public int holes;
    public double spacingError, alignError;
    [Tooltip("Stitches made, their mean tension and how much it varied.")]
    public int stitches;
    public double tension, tensionSpread;
    [Tooltip("Rivets set and how well (0-1 mean).")]
    public int rivets;
    public double rivetQuality;
    [Tooltip("Peening done on a tang or a guard (0-1+).")]
    public double peen;
    [Tooltip("Wrap: turns, tension, overlap consistency.")]
    public int turns;
    public double wrapTension, wrapSpread;
    [Tooltip("Wedge tightness (0-1+) - too much splits the haft.")]
    public double wedge;
    [Tooltip("Damage done to the parts while joining them (0-1).")]
    public double damage;
    [Tooltip("Solder: how well it flowed into the joint (0-1); overheated (0-1).")]
    public double solder, overheat;
    [Tooltip("Setting a stone: pushes made, how secure it is (0-1), how far it sits tilted (0-1).")]
    public int pushes;
    public double security, tilt;
    [Tooltip("Fit joints: how much of the gap peening has closed (mm). A slot is closed onto its tang only so far (0 in older saves).")]
    public double peenClosed;
    [Tooltip("The hammer's blows that drove, peened or closed it (a rivet's are in its quality) and how well they were struck against the joint's ideal blow (0-1 mean; 0 blows in older saves).")]
    public int blows;
    public double blowQuality;
}

/// <summary>An item being put together: its design, the pieces in each role, the joints.</summary>
[Serializable]
public class AssemblyState
{
    public string design = "";
    public List<AssemblyPart> parts = new List<AssemblyPart>();
    public List<JointState> joints = new List<JointState>();
    [Tooltip("Supplies used in it (padding, thread), by id, with counts as variables.")]
    public List<CraftVariable> supplies = new List<CraftVariable>();
    public bool finished;
    [Tooltip("The wearer was measured (tailoring, rings): how accurately (0-1).")]
    public bool measured;
    public double fit;
    [Tooltip("The size measured (mm: a finger's ring size inside, a chest...).")]
    public double fitSize;
    [Tooltip("Jewellery: how well the metal was finished and polished (0-1).")]
    public double finish;
    [Tooltip("The mastery of whoever finished it (0-1): a master's finishing touches.")]
    public double mastery;
    [Tooltip("Who finished it (their name; empty in older saves): its maker - the item's record names them, and their journal notes it once it is tried.")]
    public string maker = "";

    public JointState Joint(string id)
    {
        foreach (JointState j in joints)
            if (string.Equals(j.joint, id, StringComparison.OrdinalIgnoreCase))
                return j;
        return null;
    }

    public string PieceIn(string role, int index = 0)
    {
        foreach (AssemblyPart p in parts)
            if (p.index == index && string.Equals(p.role, role, StringComparison.OrdinalIgnoreCase))
                return p.piece;
        return null;
    }

    public double Supply(string id)
    {
        foreach (CraftVariable v in supplies)
            if (string.Equals(v.name, id, StringComparison.OrdinalIgnoreCase))
                return v.value;
        return 0;
    }
}

/// <summary>A flaw in a piece: where it is, how bad, whether it shows on the surface and whether the player has seen it.</summary>
[Serializable]
public class CraftDefect
{
    public CraftDefectKind kind;
    [Tooltip("The segment or cell it is at (-1 = the whole piece; a gem's tier of facets: -2 - the tier, CraftLapidary.BotchAt).")]
    public int at = -1;
    [Tooltip("How bad (0-1+).")]
    public double severity;
    [Tooltip("It shows on the surface (else it takes a loupe, a ring test or breaking it to find).")]
    public bool surface;
    [Tooltip("The player has noticed it.")]
    public bool seen;
    [Tooltip("What caused it (for the journal and the test station).")]
    public string cause = "";

    public CraftDefect Clone() => (CraftDefect)MemberwiseClone();
}

/// <summary>One line of what was done to a piece (its processing history, summarised).</summary>
[Serializable]
public class CraftRecordEntry
{
    public double time;
    public CraftOpKind kind;
    [Tooltip("What was done, with the real numbers (the test station shows it; the journal words it by what the player knew).")]
    public string text = "";
    [Tooltip("How many times in a row (blows, strokes) it was done.")]
    public int count = 1;
    public double min, max;
}

/// <summary>
/// One piece in a crafting workshop: an ore lump, a metal (bloom, ingot, bar, blade, plate), a hide or leather part, a
/// piece of wood, fasteners or an assembly - with its whole state, its defects and the record of what was done to it.
/// Every operation changes the state; nothing is a bonus for completing a step. Plain data (JsonUtility-friendly), so
/// a piece survives being carried, saved and restored exactly.
/// </summary>
[Serializable]
public class CraftPiece
{
    public string id = "";
    public CraftPieceKind kind;
    [Tooltip("What it came from, in words for the player (\"a lump of ore\", \"poured from the crucible\", \"rived from an ash blank\"): a taken piece's description.")]
    public string origin = "";
    public double createdAt;

    [Header("Where it is")]
    public CraftLocation location = CraftLocation.Air;
    [Tooltip("Fire: how deep in the coals (0 the edge - 1 the heart). Quench: how deep (0-1).")]
    public double depth = 1;
    [Tooltip("The segments in the fire (from - to, inclusive).")]
    public int from, to = 9999;
    [Tooltip("Vat: which one.")]
    public int vat;
    [Tooltip("Which face is up on the anvil or in the fire (0 left, 1 right).")]
    public int faceUp;
    [Tooltip("How much hotter one face got from not turning it in the fire (-1 - 1).")]
    public double faceBias;
    [Tooltip("Quench: how it is moved, and how it went in (0 point first, upright - 1 flat).")]
    public CraftQuenchMotion motion;
    public double entryAngle;
    [Tooltip("Flux on its surface (0-1): it welds and does not scale.")]
    public double flux;
    [Tooltip("The assembly it is part of.")]
    public string parent = "";

    [Header("What it is meant to be")]
    [Tooltip("The design and part the smith is working it toward (guides only: it is judged by what it is).")]
    public string intentDesign = "";
    public string intentRole = "";

    [Header("What it is")]
    [Tooltip("Metal: what it is made of (by element; relative grams).")]
    public List<CraftAmount> composition = new List<CraftAmount>();
    [Tooltip("Metal and wood: the slices along its length.")]
    public List<MetalSegment> segments = new List<MetalSegment>();
    [Tooltip("Edges it has (0, 1 or 2): from its intent, else from how it was ground.")]
    public int edges;
    [Tooltip("Layers from folding and welding; how much they differ (a pattern shows when etched).")]
    public int layers = 1;
    public double contrast;
    [Tooltip("Uneven composition (a cast that was not stirred), 0-1.")]
    public double inhomogeneity;
    public bool cast;
    public bool bloom;
    public OreState ore = new OreState();
    public HideState hide = new HideState();
    public WoodState wood = new WoodState();
    public FastenerState fasteners = new FastenerState();
    public AssemblyState assembly = new AssemblyState();
    public FibreState fibre = new FibreState();
    public YarnState yarn = new YarnState();
    public GemState gem = new GemState();
    public List<CraftDefect> defects = new List<CraftDefect>();
    [Tooltip("Named extras (an element affinity from the quench: \"affinity.Fire\").")]
    public List<CraftVariable> tags = new List<CraftVariable>();
    public List<CraftRecordEntry> record = new List<CraftRecordEntry>();

    [NonSerialized] private int profileVersion = -1;
    [NonSerialized] private CraftAlloyProfile profile;
    [NonSerialized] private CraftRules profileRules;
    [NonSerialized] public int compositionVersion;

    // ------------------------------------------------------------------ copying
    /// <summary>An exact, independent copy.</summary>
    public CraftPiece Clone() => JsonUtility.FromJson<CraftPiece>(JsonUtility.ToJson(this));

    /// <summary>The ways a piece is made from another, as <see cref="Derived"/> words them ("rived from ...").</summary>
    private static readonly string[] DerivedFrom = { "rived from ", "split from ", "sawn from ", "cut from ", "sieved from ", "spun from ", "woven from " };

    /// <summary>
    /// Where a piece made from this one comes from, in words: <paramref name="verb"/> from the first thing this one came
    /// from, however many cuts lie between ("rived from an ash log" - never "rived from split from an ash log").
    /// </summary>
    public string Derived(string verb)
    {
        string root = origin ?? "";
        for (bool cut = true; cut; )
        {
            cut = false;
            foreach (string d in DerivedFrom)
                if (root.StartsWith(d, StringComparison.OrdinalIgnoreCase))
                {
                    root = root.Substring(d.Length);
                    cut = true;
                }
        }
        return root.Length > 0 ? $"{verb} from {root}" : verb;
    }

    // ------------------------------------------------------------------ metal
    public int SegmentCount => segments.Count;

    /// <summary>Total length (mm).</summary>
    public double Length
    {
        get
        {
            double l = 0;
            foreach (MetalSegment s in segments) l += s.length;
            return l;
        }
    }

    /// <summary>The position (0-1) of the middle of segment <paramref name="i"/> along the length.</summary>
    public double PositionOf(int i)
    {
        double l = 0, total = Length;
        for (int k = 0; k < i && k < segments.Count; k++) l += segments[k].length;
        if (i >= 0 && i < segments.Count) l += segments[i].length * 0.5;
        return total > 1e-9 ? l / total : 0;
    }

    /// <summary>The segment at position <paramref name="t"/> (0-1) along the length.</summary>
    public int SegmentAt(double t)
    {
        double total = Length, want = CraftMath.Clamp01(t) * total, l = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            l += segments[i].length;
            if (want <= l) return i;
        }
        return segments.Count - 1;
    }

    /// <summary>
    /// Makes a boundary between segments at <paramref name="t"/> (0-1 along the length), so that a cut there is exactly
    /// there (a saw on its mark): the segment it falls in becomes two, their lengths in proportion, each a copy of it (its
    /// bend and slag shared by length; its hole kept in both). Defects past it, and the fire's span, move up by one.
    /// Within <paramref name="snap"/> mm of a boundary that one is used. Returns the segment the cut goes after (-1 when
    /// <paramref name="t"/> is at an end: there is nothing to cut off).
    /// </summary>
    public int InsertBoundary(double t, double snap = 0.5)
    {
        double total = Length, want = CraftMath.Clamp01(t) * total;
        if (segments.Count == 0 || want <= snap || want >= total - snap)
            return -1;
        double start = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            MetalSegment s = segments[i];
            double end = start + s.length;
            if (want > end + 1e-9)
            {
                start = end;
                continue;
            }
            if (want - start <= snap)
                return i - 1;
            if (end - want <= snap)
                return i;
            double share = (want - start) / Math.Max(1e-9, s.length);
            MetalSegment far = s.Clone();
            far.length = s.length * (1 - share);
            far.bend = s.bend * (1 - share);
            far.slag = s.slag * (1 - share);
            s.length *= share;
            s.bend *= share;
            s.slag *= share;
            segments.Insert(i + 1, far);
            foreach (CraftDefect d in defects)
                if (d.at > i) d.at++;
            if (from > i) from++;
            if (to >= i && to < 9999) to++;
            return i;
        }
        return -1;
    }

    /// <summary>Grams of an element in the composition (relative).</summary>
    public double Grams(string element)
    {
        foreach (CraftAmount a in composition)
            if (string.Equals(a.element, element, StringComparison.OrdinalIgnoreCase))
                return a.grams;
        return 0;
    }

    public void SetGrams(string element, double grams)
    {
        foreach (CraftAmount a in composition)
            if (string.Equals(a.element, element, StringComparison.OrdinalIgnoreCase))
            {
                a.grams = Math.Max(0, grams);
                compositionVersion++;
                return;
            }
        if (grams > 0)
            composition.Add(new CraftAmount(element, grams));
        compositionVersion++;
    }

    /// <summary>Mass fractions over the rules' elements.</summary>
    public double[] Fractions(CraftRules rules)
    {
        var f = new double[rules.ElementCount];
        double total = 0;
        foreach (CraftAmount a in composition)
        {
            int i = rules.IndexOf(a.element);
            if (i < 0 || a.grams <= 0) continue;
            f[i] += a.grams;
            total += a.grams;
        }
        if (total > 1e-12)
            for (int i = 0; i < f.Length; i++) f[i] /= total;
        return f;
    }

    /// <summary>What the metal's composition makes of it (cached until the composition changes).</summary>
    public CraftAlloyProfile Profile(CraftRules rules)
    {
        if (profile == null || profileRules != rules || profileVersion != compositionVersion)
        {
            profile = CraftMetallurgy.Analyze(rules, Fractions(rules));
            profileRules = rules;
            profileVersion = compositionVersion;
        }
        return profile;
    }

    /// <summary>Forget the cached profile (after editing the composition list directly).</summary>
    public void CompositionChanged() => compositionVersion++;

    /// <summary>
    /// How many edges a metal piece is to have: its purpose's - the design part's shape, double-edged 2, single-edged 1,
    /// none 0 - however the purpose was given (said, cast in the part's mould, fitted to an item, kept through a weld);
    /// <paramref name="otherwise"/> when it has no purpose with a shape (or is not metal).
    /// </summary>
    public int IntendedEdges(CraftRules rules, int otherwise)
    {
        if (kind != CraftPieceKind.Metal || string.IsNullOrEmpty(intentDesign) || rules == null)
            return otherwise;
        CraftShapeTarget shape = rules.Design(intentDesign)?.Component(intentRole)?.shape;
        return shape == null ? otherwise : shape.edge == CraftEdgeKind.Double ? 2 : shape.edge == CraftEdgeKind.Single ? 1 : 0;
    }

    /// <summary>Density (g/cm³) of the material.</summary>
    public double Density(CraftRules rules)
    {
        switch (kind)
        {
            case CraftPieceKind.Wood:
                // the species' density is seasoned wood's (CraftWoodwork.Seasoned); the water in it weighs too: a green log
                // in its bark is about half again as heavy as the same log seasoned
                return (rules.Wood(wood.wood)?.density ?? 0.7) * (1 + Math.Max(0, wood.moisture)) / (1 + CraftWoodwork.Seasoned);
            case CraftPieceKind.Metal:
            case CraftPieceKind.Fasteners:
                return Profile(rules).Density;
            case CraftPieceKind.Gem:
                return rules.Gem(gem.gem)?.specificGravity ?? 2.65;
            default:
                return 1;
        }
    }

    /// <summary>Mass of segment <paramref name="i"/> (g), with its trapped slag.</summary>
    public double SegmentMass(CraftRules rules, int i)
    {
        MetalSegment s = segments[i];
        double rho = Density(rules) * (1 - CraftMath.Clamp(s.porosity, 0, 0.9));
        return s.Volume(edges) * rho / 1000.0 + s.slag;
    }

    /// <summary>
    /// g/cm³ of a hide's or a cloth's cell at a moisture (0-1): leather about 0.8 dry and 1.1 soaking wet (raw hide is
    /// mostly water); woven cloth, measured with the air between its threads, about 0.25 (linen 0.7 mm thick weighs some
    /// 175 g a square metre, wool 1.6 mm some 400).
    /// </summary>
    public static double HideDensity(bool cloth, double moisture) =>
        cloth ? 0.23 + 0.25 * CraftMath.Clamp01(moisture) : 0.75 + 0.45 * CraftMath.Clamp01(moisture);

    /// <summary>Mass (g) of the whole piece.</summary>
    public double Mass(CraftRules rules)
    {
        switch (kind)
        {
            case CraftPieceKind.Ore:
                return ore.Mass;
            case CraftPieceKind.Hide:
            {
                // A cell is Cell Size (cm) square and its thickness (mm) thick: cell² × thickness / 10 cm³. Shrinking draws a
                // cell in and thickens it, so its mass stays as it was.
                bool cloth = rules.Hide(hide.hide)?.textile ?? false;
                double m = 0, cell = rules.Settings.cellSize;
                foreach (HideCell c in hide.cells)
                    if (c.present)
                        m += cell * cell * c.thickness / 10.0 * HideDensity(cloth, c.moisture) * (1 + 0.4 * c.oil);
                return m;
            }
            case CraftPieceKind.Fasteners:
                return fasteners.count * fasteners.massEach;
            case CraftPieceKind.Assembly:
                return 0;
            case CraftPieceKind.Fibre:
                return fibre.grams * (1 + fibre.wet);
            case CraftPieceKind.Yarn:
                return yarn.grams;
            case CraftPieceKind.Gem:
                return gem.carats * 0.2;
            default:
            {
                double m = 0;
                for (int i = 0; i < segments.Count; i++) m += SegmentMass(rules, i);
                return m;
            }
        }
    }

    /// <summary>The hottest a segment is (°C).</summary>
    public double MaxTemperature
    {
        get
        {
            double t = double.MinValue;
            foreach (MetalSegment s in segments) t = Math.Max(t, Math.Max(s.edge.temp, s.body.temp));
            return t == double.MinValue ? 0 : t;
        }
    }

    /// <summary>The average temperature (°C, by length).</summary>
    public double MeanTemperature
    {
        get
        {
            if (kind == CraftPieceKind.Hide || kind == CraftPieceKind.Ore || segments.Count == 0)
                return 0;
            double t = 0, l = 0;
            foreach (MetalSegment s in segments)
            {
                t += s.Temperature * s.length;
                l += s.length;
            }
            return l > 0 ? t / l : 0;
        }
    }

    /// <summary>
    /// Rebuilds the segments as <paramref name="n"/> slices of equal length, keeping the volume and every property
    /// (length-weighted): after a cut, a fold or a weld, so the piece keeps its resolution.
    /// </summary>
    public void Resample(int n)
    {
        if (segments.Count == 0 || n <= 0)
            return;
        double total = Length;
        if (total <= 1e-9)
            return;
        var old = segments;
        var result = new List<MetalSegment>(n);
        double slice = total / n;
        int k = 0;
        double used = 0; // length of old[k] already consumed
        for (int i = 0; i < n; i++)
        {
            var parts = new List<(MetalSegment seg, double len)>();
            double need = slice;
            while (need > 1e-9 && k < old.Count)
            {
                double avail = old[k].length - used;
                double take = Math.Min(avail, need);
                parts.Add((old[k], take));
                need -= take;
                used += take;
                if (old[k].length - used <= 1e-9)
                {
                    k++;
                    used = 0;
                }
            }
            result.Add(Blend(parts, slice));
        }
        segments = result;
    }

    private static MetalSegment Blend(List<(MetalSegment seg, double len)> parts, double length)
    {
        var s = parts.Count > 0 ? parts[0].seg.Clone() : new MetalSegment();
        s.length = length;
        if (parts.Count <= 1)
        {
            if (parts.Count == 1 && parts[0].seg.length > 1e-9)
            {
                double share = parts[0].len / parts[0].seg.length;
                s.slag = parts[0].seg.slag * share;
                s.bend = parts[0].seg.bend * share;
                s.hole = parts[0].len >= parts[0].seg.length * 0.5 ? parts[0].seg.hole : 0;
            }
            return s;
        }
        double sum = 0;
        foreach (var p in parts) sum += p.len;
        double W(Func<MetalSegment, double> f)
        {
            double v = 0;
            foreach (var p in parts) v += f(p.seg) * p.len;
            return v / Math.Max(1e-12, sum);
        }
        s.width = W(x => x.width);
        s.thickness = W(x => x.thickness);
        s.bevelDepthL = W(x => x.bevelDepthL);
        s.bevelWidthL = W(x => x.bevelWidthL);
        s.bevelDepthR = W(x => x.bevelDepthR);
        s.bevelWidthR = W(x => x.bevelWidthR);
        s.fuller = W(x => x.fuller);
        s.dish = W(x => x.dish);
        s.carbonShift = W(x => x.carbonShift);
        s.porosity = W(x => x.porosity);
        s.scale = W(x => x.scale);
        s.pit = W(x => x.pit);
        s.rough = W(x => x.rough);
        s.polish = W(x => x.polish);
        s.temperFilm = W(x => x.temperFilm);
        s.coating = W(x => x.coating);
        s.apex = W(x => x.apex);
        s.microAngle = W(x => x.microAngle);
        s.burr = W(x => x.burr);
        s.chip = W(x => x.chip);
        s.clay = W(x => x.clay);
        s.crackRisk = W(x => x.crackRisk);
        s.seamOpen = W(x => x.seamOpen);
        s.flux = W(x => x.flux);
        double slag = 0, bend = 0, hole = 0, cut = 0;
        bool fromAbove = false;
        foreach (var p in parts)
        {
            double share = p.seg.length > 1e-9 ? p.len / p.seg.length : 0;
            slag += p.seg.slag * share;
            bend += p.seg.bend * share;
            hole = Math.Max(hole, share > 0.5 ? p.seg.hole : 0);
            double c = share >= 0.5 ? p.seg.cut : 0; // a cut half made on the hardy (or with the chisel) stays where it is
            if (c > cut) { cut = c; fromAbove = p.seg.cutFromAbove; }
        }
        s.slag = slag;
        s.bend = bend;
        s.hole = hole;
        s.cut = cut;
        s.cutFromAbove = fromAbove;
        // Zones: the length-weighted mix (pairwise).
        CraftZone e = parts[0].seg.edge.Clone(), b = parts[0].seg.body.Clone();
        double acc = parts[0].len;
        for (int i = 1; i < parts.Count; i++)
        {
            CraftZone.Mix(e, e, acc, parts[i].seg.edge, parts[i].len);
            CraftZone.Mix(b, b, acc, parts[i].seg.body, parts[i].len);
            acc += parts[i].len;
        }
        s.edge = e;
        s.body = b;
        return s;
    }

    // ------------------------------------------------------------------ defects and record
    /// <summary>Adds a defect (or worsens the same kind at the same place).</summary>
    public CraftDefect AddDefect(CraftDefectKind kind, int at, double severity, bool surface, string cause)
    {
        foreach (CraftDefect d in defects)
            if (d.kind == kind && d.at == at)
            {
                d.severity = Math.Max(d.severity, severity) + Math.Min(d.severity, severity) * 0.3;
                d.surface |= surface;
                if (!string.IsNullOrEmpty(cause)) d.cause = cause;
                return d;
            }
        var n = new CraftDefect { kind = kind, at = at, severity = severity, surface = surface, cause = cause ?? "" };
        defects.Add(n);
        return n;
    }

    /// <summary>
    /// A flaw that is a state the piece goes on being in (rot, an outside closed by too strong a liquor, shrinking), told
    /// again as long as it lasts: it is as bad as the state is now - it grows as the state worsens, and never stacks on
    /// itself as <see cref="AddDefect"/> stacks separate harms done at one place.
    /// </summary>
    public CraftDefect HoldDefect(CraftDefectKind kind, int at, double severity, bool surface, string cause)
    {
        foreach (CraftDefect d in defects)
            if (d.kind == kind && d.at == at)
            {
                d.severity = Math.Max(d.severity, severity);
                d.surface |= surface;
                if (!string.IsNullOrEmpty(cause)) d.cause = cause;
                return d;
            }
        var n = new CraftDefect { kind = kind, at = at, severity = severity, surface = surface, cause = cause ?? "" };
        defects.Add(n);
        return n;
    }

    public bool HasDefect(CraftDefectKind kind)
    {
        foreach (CraftDefect d in defects)
            if (d.kind == kind) return true;
        return false;
    }

    public double DefectSeverity(CraftDefectKind kind, int at = -2)
    {
        double s = 0;
        foreach (CraftDefect d in defects)
            if (d.kind == kind && (at == -2 || d.at == at))
                s += d.severity;
        return s;
    }

    /// <summary>
    /// Records what was done: repeated operations of one kind fold into one line ("37 blows at 820-1050 °C").
    /// </summary>
    public void Record(double time, CraftOpKind op, string text, double value = double.NaN)
    {
        if (record.Count > 0)
        {
            CraftRecordEntry last = record[record.Count - 1];
            if (last.kind == op && last.text == text)
            {
                last.count++;
                if (!double.IsNaN(value))
                {
                    last.min = Math.Min(last.min, value);
                    last.max = Math.Max(last.max, value);
                }
                return;
            }
        }
        record.Add(new CraftRecordEntry { time = time, kind = op, text = text ?? "", min = double.IsNaN(value) ? 0 : value, max = double.IsNaN(value) ? 0 : value });
        if (record.Count > 400)
            record.RemoveAt(0);
    }

    public double Tag(string name)
    {
        foreach (CraftVariable v in tags)
            if (string.Equals(v.name, name, StringComparison.OrdinalIgnoreCase))
                return v.value;
        return 0;
    }

    public void SetTag(string name, double value)
    {
        foreach (CraftVariable v in tags)
            if (string.Equals(v.name, name, StringComparison.OrdinalIgnoreCase))
            {
                v.value = value;
                return;
            }
        tags.Add(new CraftVariable(name, value));
    }
}

/// <summary>
/// What materials are worth: one rule for stock, for a worked piece carried off a station and for the materials of a
/// finished item, so a metal, a wood or a hide is worth the same raw, worked or finished and no cut, fold or trip
/// through the bag makes more of it:
/// <list type="bullet">
/// <item>metal: its grams × its elements' value of a gram × its class's multiplier (a premium steel is worth its
/// premium as a bar, as a blade and in the sword);</item>
/// <item>wood: its share by volume of the species' blank (a blank is worth <see cref="CraftWoodDefinition.price"/>),
/// half that while it is unseasoned (a green log in its bark still has to be rived and seasoned);</item>
/// <item>an ore lump: its mineral against a lump's usual mineral; a hide: its share of the whole hide, twice that once
/// tanned through; cloth: its share of the bolt; fibre by weight, yarn by length, a stone by its grade;</item>
/// <item>fasteners: their metal and the making of each (a buckle more than a rivet).</item>
/// </list>
/// </summary>
public static class CraftValue
{
    /// <summary>Unseasoned wood (wetter than <see cref="CraftWoodwork.DryEnough"/>) is worth this share of seasoned wood.</summary>
    public const double Unseasoned = 0.5;
    /// <summary>The making of one rivet or stud, and of one buckle, over its metal.</summary>
    public const double FastenerMaking = 0.1, BuckleMaking = 1;

    /// <summary>
    /// A metal's value: grams × its elements' value of a gram, and its class's making on its matrix - the base and what is
    /// alloyed in no dearer than it (<see cref="CraftAlloyProfile.MatrixPricePerGram"/>): × the class's multiplier there.
    /// A steel's carburizing doubles its iron's worth, a bronze's alloying adds half to its copper; a dear metal alloyed into
    /// a cheap one counts at its own value, so welding mythril onto iron or melting orichalcum into copper never makes more
    /// of it.
    /// </summary>
    public static double Metal(CraftAlloyProfile profile, double grams)
    {
        double all = profile?.PricePerGram ?? 0.01, matrix = profile?.MatrixPricePerGram ?? all, m = profile?.Class?.priceMultiplier ?? 1;
        return Math.Max(0, grams) * Math.Max(0, all + (m - 1) * matrix);
    }

    /// <summary>Wood of a species: its share by volume (mm³) of the species' blank, half that while unseasoned.</summary>
    public static double Wood(CraftWoodDefinition w, double volume, double moisture)
    {
        double price = w?.price ?? 1;
        double blank = w != null ? (double)w.blankLength * w.blankWidth * w.blankThickness : 1000.0 * 40 * 40;
        return price * Math.Max(0, volume) / Math.Max(1, blank) * (moisture > CraftWoodwork.DryEnough ? Unseasoned : 1);
    }

    /// <summary>
    /// The metal a lump of <paramref name="ore"/> gives at most, and what it is worth by <see cref="Metal"/> (its class
    /// decides): every share of the lump at <paramref name="at"/> of its range - 0 the poorest lump of the poorest deposit,
    /// 0.5 a usual one, 1 the richest - and the lump's mass with it (80-120 % of a usual lump's). Its metals count, and the
    /// phosphorus that goes into iron; rock, sulfur (roasted off) and slag do not, nor does what the furnace loses.
    /// </summary>
    public static double OreMetal(CraftRules rules, CraftOreDefinition ore, double at, out CraftAlloyProfile profile, out double grams)
    {
        profile = null;
        grams = 0;
        if (rules == null || ore?.yields == null)
            return 0;
        at = CraftMath.Clamp01(at);
        double mineral = ore.lumpMass * (0.8 + 0.4 * at) * CraftMath.Lerp(ore.mineralMin, ore.mineralMax, at);
        var p = new CraftPiece { kind = CraftPieceKind.Metal };
        foreach (CraftOreYield y in ore.yields)
        {
            CraftElement e = rules.Element(y?.element);
            if (e == null || e.kind == CraftElementKind.Impurity && y.element != "phosphorus") continue;
            double g = mineral * CraftMath.Lerp(y.min, y.max, at) * (0.925 + 0.15 * at) * (y.element == "phosphorus" ? 0.75 : 1);
            if (g > 0) p.composition.Add(new CraftAmount(y.element, g));
        }
        p.CompositionChanged();
        foreach (CraftAmount a in p.composition) grams += a.grams;
        if (grams <= 0)
            return 0;
        profile = p.Profile(rules);
        return Metal(profile, grams);
    }

    public static double OreMetal(CraftRules rules, CraftOreDefinition ore, double at) => OreMetal(rules, ore, at, out _, out _);

    /// <summary>The cells a whole hide of the piece's animal has (the one it was cut from: hides of one seed are alike).</summary>
    public static int WholeHideCells(CraftRules rules, HideState h)
    {
        CraftPiece whole = CraftLeather.NewHide(rules, h.hide, h.seed);
        int n = 0;
        if (whole != null)
            foreach (HideCell c in whole.hide.cells)
                if (c.present) n++;
        return n;
    }

    /// <summary>What a piece is worth on its own (an assembly's parts are counted by whoever carries them).</summary>
    public static double Of(CraftRules rules, CraftPiece p)
    {
        if (rules == null || p == null)
            return 0;
        switch (p.kind)
        {
            case CraftPieceKind.Metal:
            {
                double slag = 0;
                foreach (MetalSegment s in p.segments) slag += s.slag;
                return Metal(p.Profile(rules), p.Mass(rules) - slag);
            }
            case CraftPieceKind.Fasteners:
                return Metal(p.Profile(rules), p.Mass(rules)) + p.fasteners.count * (p.fasteners.kind == CraftFastenerKind.Buckle ? BuckleMaking : FastenerMaking);
            case CraftPieceKind.Ore:
            {
                CraftOreDefinition o = rules.Ore(p.ore.ore);
                double usual = o != null ? o.lumpMass * (o.mineralMin + o.mineralMax) * 0.5 : 500;
                return (o?.price ?? 1) * p.ore.MineralMass / Math.Max(1, usual);
            }
            case CraftPieceKind.Hide:
            {
                CraftHideDefinition h = rules.Hide(p.hide.hide);
                int cells = 0;
                foreach (HideCell c in p.hide.cells) if (c.present) cells++;
                double share = h != null ? cells / (double)Math.Max(1, WholeHideCells(rules, p.hide)) : 0.5;
                // cloth comes finished: nothing to tan (its cells read as tanned through) - and it is worth its cells, a
                // length woven longer than a bolt more than a bolt (cut in two, its halves are worth the whole); a piece
                // of hide is never more than the whole hide it came from
                return h != null && h.textile ? h.price * share : (h?.price ?? 5) * Math.Min(1, share) * (1 + CraftLeather.MeanTannage(p.hide));
            }
            case CraftPieceKind.Wood:
            {
                double volume = 0;
                foreach (MetalSegment s in p.segments) volume += s.Volume(0);
                return Wood(rules.Wood(p.wood.wood), volume, p.wood.moisture);
            }
            case CraftPieceKind.Fibre:
            {
                CraftSupplyDefinition s = rules.Supply(p.fibre.fibre);
                return (s?.price ?? 0.5) * p.fibre.grams / Math.Max(1, s?.gramsPerItem ?? 100) * (1 + CraftFibre.Prep(p.fibre));
            }
            case CraftPieceKind.Yarn:
                return (rules.Supply(CraftFibre.ThreadOf(rules, p))?.price ?? 0.5) * p.yarn.length / CraftFibre.MetresPerThread;
            case CraftPieceKind.Gem:
                return CraftLapidary.Grade(rules, p).Value;
            default:
                return 0;
        }
    }
}
