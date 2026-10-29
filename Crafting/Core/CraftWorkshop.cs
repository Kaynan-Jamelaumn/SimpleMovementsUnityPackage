using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a workshop has. A station sets them; operations that need a missing tool are refused.</summary>
[Flags]
public enum CraftTools
{
    None = 0,
    /// <summary>A breaking stone, a washing trough, sieves and a sorting tray (ore cleaning).</summary>
    OreBench = 1 << 0,
    /// <summary>A smelting furnace (bloomery): roasting, reducing ores, tapping slag, cupellation.</summary>
    Furnace = 1 << 1,
    /// <summary>A crucible and moulds: melting, alloying, casting.</summary>
    Crucible = 1 << 2,
    /// <summary>A forge hearth with bellows.</summary>
    Forge = 1 << 3,
    /// <summary>An anvil with hammers, a horn, a hardy, drifts, fullers and a flatter.</summary>
    Anvil = 1 << 4,
    /// <summary>A quench tank.</summary>
    QuenchTank = 1 << 5,
    /// <summary>A regulated oven (tempering, annealing).</summary>
    Oven = 1 << 6,
    /// <summary>A grinding wheel.</summary>
    Grinder = 1 << 7,
    /// <summary>Files, rasps, brushes and scrapers.</summary>
    Files = 1 << 8,
    /// <summary>Sanding, polishing and buffing.</summary>
    Polishing = 1 << 9,
    /// <summary>Whetstones and a strop.</summary>
    Whetstone = 1 << 10,
    /// <summary>A fleshing beam with scraping knives.</summary>
    FleshingBeam = 1 << 11,
    /// <summary>Vats (soaking, liming, tanning, pickling).</summary>
    Vats = 1 << 12,
    /// <summary>A stretching frame.</summary>
    Frame = 1 << 13,
    /// <summary>A drying rack.</summary>
    Rack = 1 << 14,
    /// <summary>A leather bench: cutting, skiving, staking, punching, dyeing, sewing.</summary>
    LeatherBench = 1 << 15,
    /// <summary>An assembly bench: fitting, riveting, peening, wedging, wrapping.</summary>
    AssemblyBench = 1 << 16,
    /// <summary>A box of warm ashes (slow cooling).</summary>
    Ashes = 1 << 17,
    /// <summary>A chopping block with a froe, a mallet and wedges: riving wood along its grain.</summary>
    ChoppingBlock = 1 << 18,
    /// <summary>A sawhorse with rip and crosscut saws.</summary>
    Sawhorse = 1 << 19,
    /// <summary>A steam box and bending forms with a strap.</summary>
    SteamBox = 1 << 20,
    /// <summary>A drying stack under a roof: stickers, end sealer, a cover (seasoning wood).</summary>
    DryingStack = 1 << 21,
    /// <summary>A finishing bench: sandpaper and pumice, brushes, oil and varnish.</summary>
    FinishingBench = 1 << 22,
    /// <summary>A spinning wheel (and a drop spindle): fibre into thread.</summary>
    SpinningWheel = 1 << 23,
    /// <summary>A tailor's table: shears, chalk, pins, needles and thimbles, a tape measure and a dress form.</summary>
    TailorTable = 1 << 24,
    /// <summary>A shaving horse with a drawknife and a bark spud: debarking logs, shaving.</summary>
    ShavingHorse = 1 << 25,
    /// <summary>A fibre bench: a picking table, scouring tubs over a fire, hand cards, wool combs and a flax hackle.</summary>
    FibreBench = 1 << 26,
    /// <summary>A lapidary bench: loupe and lamp, refractometer, hardness points, a hydrostatic balance, cleaving chisels and a trim saw.</summary>
    GemBench = 1 << 27,
    /// <summary>A faceting machine: a mast with an angle quadrant and index gear, cutting laps and polishing laps.</summary>
    FacetingMachine = 1 << 28,
    /// <summary>A jeweller's bench: ring mandrel, piercing saw, burrs, a soldering torch, setting pushers and burnishers.</summary>
    JewelersBench = 1 << 29,
    /// <summary>A floor loom: warp beam, heddles and treadles, a reed to beat the weft in, shuttles.</summary>
    Loom = 1 << 30,

    Smithy = Forge | Anvil | QuenchTank | Grinder | Files | Polishing | Whetstone | Ashes | Oven | AssemblyBench,
    Smeltery = OreBench | Furnace | Crucible,
    Tannery = FleshingBeam | Vats | Frame | Rack,
    Leatherworks = LeatherBench | AssemblyBench,
    Woodshop = ChoppingBlock | Sawhorse | SteamBox | DryingStack | FinishingBench | Files | AssemblyBench | ShavingHorse,
    Tailor = SpinningWheel | TailorTable | AssemblyBench | Loom,
    /// <summary>A smithy with a leather bench and a tailor's table: plate and mixed armour (metal on leather and cloth) is assembled here.</summary>
    Armourer = Smithy | LeatherBench | TailorTable,
    /// <summary>Fibre made ready, spun and woven: cleaning, scouring, opening, carding, combing, spinning, plying, reeling, weaving.</summary>
    Spinnery = FibreBench | SpinningWheel | Loom,
    /// <summary>Rough stones into cut gems: identifying, evaluating, orienting, cleaving and sawing, faceting, polishing.</summary>
    Lapidary = GemBench | FacetingMachine,
    /// <summary>Settings, shanks and chains made, stones set, the piece finished.</summary>
    Jeweller = JewelersBench | AssemblyBench | Polishing | Files,
    Everything = Smithy | Smeltery | Tannery | Leatherworks | Woodshop | Tailor | Spinnery | Lapidary | Jeweller,
}

/// <summary>
/// One operation of a workshop's history, stamped with the simulation step it happened before. Replaying the history
/// on the same rules gives the same workshop. Pieces brought in are stored whole (<see cref="pieces"/>), and pieces an
/// operation creates keep their id (<see cref="other"/>), so a history is self-contained.
/// </summary>
[Serializable]
public class CraftOperation
{
    public long step;
    public CraftOpKind kind;
    [Tooltip("The piece it is done to.")]
    public string piece = "";
    [Tooltip("A second piece (welded on, the piece created).")]
    public string other = "";
    [Tooltip("A tool, fuel, medium, supply, mould, design, role, pattern or joint id.")]
    public string id = "";
    public string id2 = "";
    public string label = "";
    public int i0, i1, i2;
    public float a, b, c, d, e;
    public bool flag;
    [Tooltip("A path (x, y pairs): scraping, staking, dyeing strokes.")]
    public List<float> points = new List<float>();
    [Tooltip("AddPiece: the pieces as they came in.")]
    public List<CraftPiece> pieces = new List<CraftPiece>();

    public CraftOperation Clone()
    {
        var c = (CraftOperation)MemberwiseClone();
        c.points = new List<float>(points);
        c.pieces = new List<CraftPiece>();
        foreach (CraftPiece p in pieces) c.pieces.Add(p.Clone());
        return c;
    }
}

/// <summary>A fire (the forge's, or the furnace's): its fuel, flames, air and temperature.</summary>
[Serializable]
public class CraftFireState
{
    [Tooltip("Burn units on the fire.")]
    public double fuel;
    [Tooltip("What the fuel on it is like (blended by amount).")]
    public double maxTemperature = 1350, reducing = 0.6, carburizing = 0.5, sulfur, burnRate = 1, smoke = 0.2;
    [Tooltip("Flames (0-1), air from the bellows (fades), the air vent (0 choked - 1 open), the heart's temperature (°C).")]
    public double level, air, damper = 1, temperature = 18;
    public bool lit;

    /// <summary>How oxidising the fire is at its heart (0 reducing - 1 a roaring blast).</summary>
    public double Oxidizing => CraftMath.Clamp(0.25 + 0.75 * air * damper - 0.45 * reducing * fuel / (fuel + 2.0), 0.03, 1);
}

/// <summary>The smelting furnace: its fire, slag, flux, the bloom growing in it, liquid metal at the bottom.</summary>
[Serializable]
public class CraftFurnaceState
{
    public CraftFireState fire = new CraftFireState();
    [Tooltip("Slag in the furnace (g), and the share of it liquid enough to run.")]
    public double slag;
    [Tooltip("Limestone flux charged (g, not yet used up).")]
    public double flux;
    [Tooltip("A bone-ash cupel in it (g): lead oxidises into it, silver stays.")]
    public double cupel;
    [Tooltip("The bloom growing at the bottom (solid metal, by element).")]
    public List<CraftAmount> bloom = new List<CraftAmount>();
    [Tooltip("Slag trapped in the bloom (g), how unevenly carbon went into it, and where it would start to melt (°C).")]
    public double bloomSlag, bloomSpread, bloomSolidus;
    [Tooltip("Liquid metal at the bottom (by element): copper, tin, lead, silver, gold, cast iron.")]
    public List<CraftAmount> pool = new List<CraftAmount>();
    [Tooltip("Sulfur in the liquid: a matte, not a metal, until it is roasted.")]
    public double matte;

    public double BloomMass
    {
        get
        {
            double m = 0;
            foreach (CraftAmount a in bloom) m += a.grams;
            return m;
        }
    }

    public double PoolMass
    {
        get
        {
            double m = 0;
            foreach (CraftAmount a in pool) m += a.grams;
            return m;
        }
    }
}

/// <summary>The crucible: its melt, temperature, dross, gas, flux cover and how well it is mixed.</summary>
[Serializable]
public class CraftCrucibleState
{
    public List<CraftAmount> melt = new List<CraftAmount>();
    public double temperature = 18;
    [Tooltip("Oxide skin on the melt (g).")]
    public double dross;
    [Tooltip("Gas dissolved (0-1): rises when overheated and held; porosity in the casting.")]
    public double gas;
    [Tooltip("Flux cover (borax, charcoal): less oxidation and less loss of what boils off (0-1).")]
    public double cover;
    [Tooltip("How evenly mixed the melt is (0-1).")]
    public double mixed = 1;
    [Tooltip("The melt took a fire's sulfur, oxygen...: grams lost to the air (zinc boiling off) so far.")]
    public double lost;

    public double Mass
    {
        get
        {
            double m = 0;
            foreach (CraftAmount a in melt) m += a.grams;
            return m;
        }
    }
}

/// <summary>The quench tank: its medium, temperature and size.</summary>
[Serializable]
public class CraftQuenchState
{
    public string medium = "water";
    public double temperature = 18;
    [Tooltip("Litres.")]
    public double volume = 40;
}

/// <summary>A regulated oven.</summary>
[Serializable]
public class CraftOvenState
{
    public double setpoint;
    public double temperature = 18;
}

/// <summary>A vat: what is in it, how strong, how warm.</summary>
[Serializable]
public class CraftVatState
{
    public string agent = "";
    [Tooltip("Strength (0-1).")]
    public double strength;
    public double temperature = 18;
    [Tooltip("A fire under it (0-1): hot water for hardening leather.")]
    public double heat;
    [Tooltip("Litres (empty = 0).")]
    public double volume;
    [Tooltip("Stirred lately (0-1): evens the tanning.")]
    public double stirred;
}

/// <summary>
/// A crafting workshop in progress: its pieces, its fires and furnace, crucible, quench tank, oven and vats, its clock,
/// history and events. A <see cref="CraftStation"/> drives one, but it is a plain object: tests, editor tools and a
/// server run workshops without a scene.
/// <list type="bullet">
/// <item><b>Operations</b> (a blow of the hammer, a pass on the wheel, a stroke of the knife, moving a piece into the
/// fire) change the pieces at once and are recorded with the current step.</item>
/// <item><b>Time</b>: <see cref="Advance"/> runs whole fixed steps (the remainder carries over): fires burn, pieces heat
/// and cool segment by segment, steel transforms, scale grows, ores reduce, melts oxidise, hides tan and dry.</item>
/// <item><b>Determinism</b>: the same operations at the same steps always give the same workshop
/// (<see cref="Replay"/>); <see cref="Capture"/> / <see cref="Restore"/> keep everything.</item>
/// </list>
/// </summary>
public sealed partial class CraftWorkshop
{
    private const int MaxRecentEvents = 120;

    private readonly List<CraftOperation> history = new List<CraftOperation>();
    private readonly List<CraftEvent> recent = new List<CraftEvent>();
    private readonly List<CraftPiece> pieces = new List<CraftPiece>();
    private double carry;
    private int sequence;

    public string Id { get; private set; }
    public CraftRules Rules { get; }
    public int Seed { get; }
    public CraftTools Tools { get; private set; }
    public long Step { get; private set; }
    public double Elapsed => Step * Rules.Settings.stepSeconds;
    public double Ambient { get; private set; }
    public bool IsSuspended { get; private set; }

    public CraftFireState Forge { get; private set; } = new CraftFireState();
    public CraftFurnaceState Furnace { get; private set; } = new CraftFurnaceState();
    public CraftCrucibleState Crucible { get; private set; } = new CraftCrucibleState();
    public CraftQuenchState Quench { get; private set; } = new CraftQuenchState();
    public CraftOvenState Oven { get; private set; } = new CraftOvenState();
    public List<CraftVatState> Vats { get; private set; } = new List<CraftVatState>();
    /// <summary>Surroundings (humidity, night, wind...): read by drying and by what can be seen.</summary>
    public readonly SortedDictionary<string, double> Variables = new SortedDictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    /// <summary>The ash box's temperature (°C).</summary>
    public double AshTemperature { get; private set; }

    public IReadOnlyList<CraftPiece> Pieces => pieces;
    public IReadOnlyList<CraftOperation> History => history;
    public IReadOnlyList<CraftEvent> RecentEvents => recent;
    /// <summary>Raised for every event (blows, sparks, cracks, a crucible pour...).</summary>
    public event Action<CraftEvent> EventRaised;
    /// <summary>Raised after every operation is recorded (a server sends it to the copies of this workshop on the clients).</summary>
    public event Action<CraftOperation> OperationRecorded;

    public CraftWorkshop(CraftRules rules, CraftTools tools, double ambient = double.NaN, int seed = 0, int vats = 3, string quenchMedium = "water", string id = null)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        Tools = tools;
        Seed = seed;
        Id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString("N").Substring(0, 12) : id;
        Ambient = double.IsNaN(ambient) ? rules.Settings.defaultAmbient : ambient;
        Forge.temperature = Furnace.fire.temperature = Crucible.temperature = Quench.temperature = Oven.temperature = Ambient;
        AshTemperature = Ambient;
        Quench.medium = rules.Quenchant(quenchMedium) != null ? quenchMedium : rules.Quenchants.Length > 0 ? rules.Quenchants[0].id : "";
        for (int i = 0; i < Math.Max(0, vats); i++)
            Vats.Add(new CraftVatState { temperature = Ambient });
    }

    public bool Has(CraftTools t) => (Tools & t) == t;

    public CraftPiece Piece(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        for (int i = 0; i < pieces.Count; i++)
            if (pieces[i].id == id) return pieces[i];
        return null;
    }

    public double Variable(string name) => Variables.TryGetValue(name, out double v) ? v : 0;

    /// <summary>
    /// How good the station's tools are (the "toolQuality" variable the station sets: 0.6 crude - 1 ordinary - 1.4
    /// masterwork): sharper drawknives and cards, truer laps and quadrants do more with each stroke and less damage.
    /// </summary>
    public double ToolQuality => Variables.TryGetValue("toolQuality", out double q) && q > 0 ? CraftMath.Clamp(q, 0.3, 2) : 1;

    /// <summary>The air's humidity (0-1; 0.5 when the station reads none): wood seasons and fibre dries slower in damp air.</summary>
    public double Humidity => Variables.TryGetValue("humidity", out double h) ? CraftMath.Clamp01(h) : 0.5;

    /// <summary>A new piece id, unique to this workshop (and the same in a replay).</summary>
    private string NewId() => $"{Id}-{++sequence}";

    /// <summary>
    /// Drops all but the last <paramref name="keepLast"/> operations of the history (long sessions at a station). The
    /// state is untouched; only replaying from the start is no longer possible.
    /// </summary>
    public void ForgetHistory(int keepLast)
    {
        keepLast = Math.Max(0, keepLast);
        if (history.Count > keepLast)
            history.RemoveRange(0, history.Count - keepLast);
    }

    // ------------------------------------------------------------------ recording
    private bool Refuse(string text, out string reason, string piece = "")
    {
        reason = text;
        Raise(CraftEventKind.Refused, piece, "", text);
        return false;
    }

    private void Record(CraftOperation op)
    {
        op.step = Step;
        Apply(op);
        history.Add(op);
        OperationRecorded?.Invoke(op);
    }

    internal void Raise(CraftEventKind kind, string piece, string id, string text, float value = 0f)
    {
        var e = new CraftEvent(kind, Elapsed, piece, id, text, value);
        recent.Add(e);
        if (recent.Count > MaxRecentEvents)
            recent.RemoveRange(0, recent.Count - MaxRecentEvents);
        EventRaised?.Invoke(e);
    }

    /// <summary>Applies an operation (live and in replays).</summary>
    private void Apply(CraftOperation op)
    {
        switch (op.kind)
        {
            case CraftOpKind.AddPiece: ApplyAddPiece(op); break;
            case CraftOpKind.RemovePiece: ApplyRemovePiece(op); break;
            case CraftOpKind.MovePiece: ApplyMove(op); break;
            case CraftOpKind.TurnPiece: ApplyTurn(op); break;
            case CraftOpKind.SetAmbient: Ambient = op.a; break;
            case CraftOpKind.SetVariable: Variables[op.id] = op.a; break;
            case CraftOpKind.Suspend: IsSuspended = true; Raise(CraftEventKind.Suspended, "", "", ""); break;
            case CraftOpKind.Resume: IsSuspended = false; Raise(CraftEventKind.Resumed, "", "", ""); break;
            case CraftOpKind.AddFuel: ApplyFuel(op); break;
            case CraftOpKind.Bellows:
            {
                CraftFireState f = op.i0 == 1 ? Furnace.fire : Forge;
                f.air = Math.Min(1.0, f.air + Rules.Settings.bellowsGain * CraftMath.Clamp01(op.a));
                break;
            }
            case CraftOpKind.SetDamper:
                (op.i0 == 1 ? Furnace.fire : Forge).damper = CraftMath.Clamp01(op.a);
                break;
            case CraftOpKind.SetOven: Oven.setpoint = Math.Max(0, op.a); break;
            case CraftOpKind.CrushOre: CraftOres.Crush(this, Piece(op.piece), op.i0, op.a); break;
            case CraftOpKind.DiscardFragment: CraftOres.Discard(this, Piece(op.piece), op.i0); break;
            case CraftOpKind.WashOre: CraftOres.Wash(this, Piece(op.piece), op.a, op.b, op.c); break;
            case CraftOpKind.SieveOre: ApplySieve(op); break;
            case CraftOpKind.AddFlux: ApplyFlux(op); break;
            case CraftOpKind.TapSlag: CraftSmelting.TapSlag(this, op.a); break;
            case CraftOpKind.ExtractBloom: ApplyExtract(op); break;
            case CraftOpKind.StirMelt: CraftSmelting.Stir(this, op.a); break;
            case CraftOpKind.SkimDross: CraftSmelting.Skim(this, op.a); break;
            case CraftOpKind.Pour: ApplyPour(op); break;
            case CraftOpKind.TakeMelt: ApplyTakeMelt(op); break;
            case CraftOpKind.Strike: CraftForging.Strike(this, Piece(op.piece), op); break;
            case CraftOpKind.Fold: CraftForging.Fold(this, Piece(op.piece), op.a, op.b); break;
            case CraftOpKind.Weld: ApplyWeld(op); break;
            case CraftOpKind.FormFastener: ApplyFormFastener(op); break;
            case CraftOpKind.SplitPiece: ApplySplit(op); break;
            case CraftOpKind.Agitate: ApplyAgitate(op); break;
            case CraftOpKind.ApplyClay: CraftSurface.ApplyClay(this, Piece(op.piece), op.i0, op.i1, op.a, op.points); break;
            case CraftOpKind.Descale: CraftSurface.Descale(this, Piece(op.piece), op); break;
            case CraftOpKind.Grind: CraftSurface.Grind(this, Piece(op.piece), op); break;
            case CraftOpKind.File: CraftSurface.File(this, Piece(op.piece), op); break;
            case CraftOpKind.Polish: CraftSurface.Polish(this, Piece(op.piece), op); break;
            case CraftOpKind.Coat: CraftSurface.Coat(this, Piece(op.piece), op); break;
            case CraftOpKind.Sharpen: CraftSurface.Sharpen(this, Piece(op.piece), op); break;
            case CraftOpKind.Scrape: CraftLeather.Scrape(this, Piece(op.piece), op); break;
            case CraftOpKind.FillVat: ApplyFillVat(op); break;
            case CraftOpKind.DrainVat: ApplyDrainVat(op); break;
            case CraftOpKind.SetVatHeat: if (op.i0 >= 0 && op.i0 < Vats.Count) Vats[op.i0].heat = CraftMath.Clamp01(op.a); break;
            case CraftOpKind.StirVat: if (op.i0 >= 0 && op.i0 < Vats.Count) Vats[op.i0].stirred = 1; break;
            case CraftOpKind.Stretch: CraftLeather.Stretch(this, Piece(op.piece), op); break;
            case CraftOpKind.Stake: CraftLeather.Stake(this, Piece(op.piece), op); break;
            case CraftOpKind.Condition: CraftLeather.Condition(this, Piece(op.piece), op); break;
            case CraftOpKind.Dye: CraftLeather.Dye(this, Piece(op.piece), op); break;
            case CraftOpKind.Slick: CraftLeather.Slick(this, Piece(op.piece), op); break;
            case CraftOpKind.Skive: CraftLeather.Skive(this, Piece(op.piece), op); break;
            case CraftOpKind.CutPattern: ApplyCutPattern(op); break;
            case CraftOpKind.StartAssembly: ApplyStartAssembly(op); break;
            case CraftOpKind.Attach: ApplyAttach(op); break;
            case CraftOpKind.Detach: ApplyDetach(op); break;
            case CraftOpKind.JointWork: CraftAssembly.Work(this, Piece(op.piece), op); break;
            case CraftOpKind.Finish: ApplyFinish(op); break;
            case CraftOpKind.SetIntent: ApplyIntent(op); break;
            case CraftOpKind.Carve: CraftSurface.Carve(this, Piece(op.piece), op); break;
            case CraftOpKind.BrushOre: CraftOres.Brush(this, Piece(op.piece), op); break;
            case CraftOpKind.Rive: CraftWoodwork.Rive(this, Piece(op.piece), op); break;
            case CraftOpKind.SawCut: CraftWoodwork.SawCut(this, Piece(op.piece), op); break;
            case CraftOpKind.BendWood: CraftWoodwork.Bend(this, Piece(op.piece), op); break;
            case CraftOpKind.StackWood: CraftWoodwork.Stack(this, Piece(op.piece), op); break;
            case CraftOpKind.SandWood: CraftWoodwork.Sand(this, Piece(op.piece), op); break;
            case CraftOpKind.Varnish: CraftWoodwork.Varnish(this, Piece(op.piece), op); break;
            case CraftOpKind.DebarkWood: CraftWoodwork.Debark(this, Piece(op.piece), op); break;
            case CraftOpKind.SplitLog: CraftWoodwork.SplitLog(this, Piece(op.piece), op); break;
            default: ApplyMore(op); break;
        }
    }

    // ------------------------------------------------------------------ pieces in and out
    /// <summary>
    /// Brings a piece in (from an inventory, another station, a script): it is copied whole into the history. An
    /// assembly brings its components with it (<paramref name="components"/>). Returns its id.
    /// </summary>
    public string AddPiece(CraftPiece piece, IEnumerable<CraftPiece> components = null)
    {
        if (piece == null)
            return null;
        var op = new CraftOperation { kind = CraftOpKind.AddPiece, piece = piece.id };
        op.pieces.Add(piece.Clone());
        if (components != null)
            foreach (CraftPiece c in components)
                if (c != null) op.pieces.Add(c.Clone());
        // Pieces brought from elsewhere may carry ids this workshop already uses: they get new ones, and an assembly's
        // parts keep pointing at each other.
        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (CraftPiece p in op.pieces)
        {
            if (string.IsNullOrEmpty(p.id) || Piece(p.id) != null || !seen.Add(p.id))
            {
                string fresh = NewId();
                if (!string.IsNullOrEmpty(p.id)) renamed[p.id] = fresh;
                p.id = fresh;
                seen.Add(fresh);
            }
        }
        if (renamed.Count > 0)
            foreach (CraftPiece p in op.pieces)
            {
                if (!string.IsNullOrEmpty(p.parent) && renamed.TryGetValue(p.parent, out string parent)) p.parent = parent;
                if (p.kind == CraftPieceKind.Assembly)
                    foreach (AssemblyPart part in p.assembly.parts)
                        if (!string.IsNullOrEmpty(part.piece) && renamed.TryGetValue(part.piece, out string moved)) part.piece = moved;
            }
        op.piece = op.pieces[0].id;
        Record(op);
        return op.piece;
    }

    private void ApplyAddPiece(CraftOperation op)
    {
        for (int i = 0; i < op.pieces.Count; i++)
        {
            CraftPiece p = op.pieces[i].Clone();
            if (Piece(p.id) != null) continue;
            if (i == 0 && p.location != CraftLocation.Assembled) p.location = CraftLocation.Air;
            pieces.Add(p);
            BumpSequence(p.id);
        }
        Raise(CraftEventKind.PieceAdded, op.piece, "", "");
    }

    /// <summary>Keeps new ids past the ones brought in from an earlier run of this workshop.</summary>
    private void BumpSequence(string id)
    {
        string prefix = Id + "-";
        if (id != null && id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(id.Substring(prefix.Length), out int n) && n > sequence)
            sequence = n;
    }

    /// <summary>Takes a piece out (and an assembly's components): returns copies of them (the piece first), or null.</summary>
    public List<CraftPiece> RemovePiece(string id, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null)
        {
            Refuse("There is no such piece here.", out reason);
            return null;
        }
        if (p.location == CraftLocation.Assembled)
        {
            Refuse("It is part of an assembly: take it off first.", out reason, id);
            return null;
        }
        if (p.location == CraftLocation.Melt)
        {
            Refuse("It is in the crucible: lift it out with the tongs first.", out reason, id);
            return null;
        }
        var result = new List<CraftPiece> { p.Clone() };
        foreach (CraftPiece c in Components(p))
            result.Add(c.Clone());
        Record(new CraftOperation { kind = CraftOpKind.RemovePiece, piece = id });
        result[0].location = CraftLocation.Air;
        return result;
    }

    private void ApplyRemovePiece(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        foreach (CraftPiece c in Components(p))
            pieces.Remove(c);
        pieces.Remove(p);
        Raise(CraftEventKind.PieceRemoved, op.piece, "", "");
    }

    /// <summary>The components of an assembly (the pieces whose parent it is).</summary>
    public List<CraftPiece> Components(CraftPiece assembly)
    {
        var list = new List<CraftPiece>();
        if (assembly == null || assembly.kind != CraftPieceKind.Assembly)
            return list;
        foreach (CraftPiece p in pieces)
            if (p.parent == assembly.id) list.Add(p);
        return list;
    }

    /// <summary>A piece created by an operation (it takes the id the operation carries, so replays create the same).</summary>
    internal CraftPiece Create(CraftOperation op, CraftPieceKind kind, string origin)
    {
        if (string.IsNullOrEmpty(op.other))
            op.other = NewId();
        else
            BumpSequence(op.other);
        var p = new CraftPiece { id = op.other, kind = kind, origin = origin ?? "", createdAt = Elapsed };
        pieces.Add(p);
        Raise(CraftEventKind.PieceCreated, p.id, "", origin);
        return p;
    }

    internal void Destroy(CraftPiece p, string text)
    {
        if (p == null)
            return;
        foreach (CraftPiece c in Components(p))
            pieces.Remove(c);
        pieces.Remove(p);
        Raise(CraftEventKind.PieceDestroyed, p.id, "", text);
    }

    // ------------------------------------------------------------------ moving pieces
    /// <summary>
    /// Moves a piece: into the fire (at <paramref name="depth"/>: 0 the edge of the coals - 1 the heart, with the
    /// segments <paramref name="from"/>-<paramref name="to"/> in it), the furnace, the crucible, the quench (going in at
    /// <paramref name="entryAngle"/>: 0 point first, 1 flat), the oven, the ashes, a vat (<paramref name="vat"/>), the
    /// frame, the rack, or back into the air.
    /// </summary>
    public bool MovePiece(string id, CraftLocation to, out string reason, double depth = 1, int from = 0, int toSegment = 9999, int vat = 0, double entryAngle = 0)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null)
            return Refuse("There is no such piece here.", out reason);
        if (p.location == CraftLocation.Assembled)
            return Refuse("It cannot be moved now.", out reason, id);
        // A piece in the crucible that has not melted yet can be lifted out with the tongs (onto the bench only):
        // once it melts it is part of the melt.
        if (p.location == CraftLocation.Melt && to != CraftLocation.Air && to != CraftLocation.Melt)
            return Refuse("Lift it out of the crucible first.", out reason, id);
        if (!CanGo(p, to, vat, out reason))
            return Refuse(reason, out reason, id);
        Record(new CraftOperation
        {
            kind = CraftOpKind.MovePiece, piece = id, i0 = (int)to, a = (float)CraftMath.Clamp01(depth), i1 = Math.Max(0, from),
            i2 = Math.Max(0, toSegment), b = (float)CraftMath.Clamp01(entryAngle), c = vat,
        });
        return true;
    }

    private bool CanGo(CraftPiece p, CraftLocation to, int vat, out string reason)
    {
        reason = "";
        bool metal = p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Fasteners;
        switch (to)
        {
            case CraftLocation.Air: return true;
            case CraftLocation.Fire:
                if (!Has(CraftTools.Forge)) { reason = "There is no forge here."; return false; }
                if (!metal && p.kind != CraftPieceKind.Wood) { reason = "That does not belong in the fire."; return false; }
                return true;
            case CraftLocation.Furnace:
                if (!Has(CraftTools.Furnace)) { reason = "There is no furnace here."; return false; }
                if (p.kind != CraftPieceKind.Ore && !(metal && p.bloom)) { reason = "Only ore is charged into the furnace."; return false; }
                return true;
            case CraftLocation.Melt:
                if (!Has(CraftTools.Crucible)) { reason = "There is no crucible here."; return false; }
                if (!metal && p.kind != CraftPieceKind.Ore) { reason = "Only metal (or an ore to cement) goes into the crucible."; return false; }
                return true;
            case CraftLocation.Quench:
                if (!Has(CraftTools.QuenchTank)) { reason = "There is no quench tank here."; return false; }
                if (p.kind == CraftPieceKind.Hide || p.kind == CraftPieceKind.Ore) { reason = "That does not go in the quench tank."; return false; }
                return true;
            case CraftLocation.Oven:
                if (!Has(CraftTools.Oven)) { reason = "There is no oven here."; return false; }
                if (!metal) { reason = "The oven is for metal."; return false; }
                return true;
            case CraftLocation.Ashes:
                if (!Has(CraftTools.Ashes)) { reason = "There is no ash box here."; return false; }
                if (!metal) { reason = "The ashes are for hot metal."; return false; }
                return true;
            case CraftLocation.Vat:
                if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count) { reason = "There is no such vat here."; return false; }
                if (Vats[vat].volume <= 0.01) { reason = "That vat is empty: fill it first."; return false; }
                if (p.kind != CraftPieceKind.Hide && !(metal && Rules.Agent(Vats[vat].agent)?.kind == CraftAgentKind.Acid) || CraftTextiles.IsCloth(Rules, p))
                {
                    reason = "Only hides soak in the vats (and metal in a pickle).";
                    return false;
                }
                foreach (CraftPiece q in pieces)
                    if (q != p && q.location == CraftLocation.Vat && q.vat == vat && q.kind == CraftPieceKind.Hide && p.kind == CraftPieceKind.Hide)
                    {
                        reason = "That vat already holds a hide.";
                        return false;
                    }
                return true;
            case CraftLocation.Frame:
                if (!Has(CraftTools.Frame)) { reason = "There is no stretching frame here."; return false; }
                if (p.kind != CraftPieceKind.Hide || CraftTextiles.IsCloth(Rules, p)) { reason = "Only hides are laced in the frame."; return false; }
                return true;
            case CraftLocation.Rack:
                if (!Has(CraftTools.Rack)) { reason = "There is no drying rack here."; return false; }
                if (p.kind != CraftPieceKind.Hide && p.kind != CraftPieceKind.Wood) { reason = "The rack is for hides."; return false; }
                return true;
            case CraftLocation.Steam:
                if (!Has(CraftTools.SteamBox)) { reason = "There is no steam box here."; return false; }
                if (p.kind != CraftPieceKind.Wood) { reason = "Only wood goes in the steam box."; return false; }
                return true;
            case CraftLocation.Stack:
                if (!Has(CraftTools.DryingStack)) { reason = "There is no drying stack here."; return false; }
                if (p.kind != CraftPieceKind.Wood) { reason = "Only wood seasons in the stack."; return false; }
                return true;
        }
        reason = "It cannot go there.";
        return false;
    }

    private void ApplyMove(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        CraftLocation was = p.location;
        var to = (CraftLocation)op.i0;
        p.location = to;
        p.depth = op.a;
        p.from = op.i1;
        p.to = op.i2;
        p.vat = (int)op.c;
        if (to == CraftLocation.Quench)
        {
            p.entryAngle = op.b;
            p.motion = CraftQuenchMotion.Still;
            p.Record(Elapsed, op.kind, $"Quenched in {Rules.Quenchant(Quench.medium)?.Name ?? Quench.medium}", p.MaxTemperature);
            CraftThermal.OnQuenchEntry(this, p);
        }
        else if (to == CraftLocation.Melt)
        {
            p.Record(Elapsed, op.kind, "Put in the crucible");
        }
        else if (to != was)
            p.Record(Elapsed, op.kind, $"Moved to {to}");
        if (to == CraftLocation.Ashes && was != CraftLocation.Ashes)
            AshTemperature = Math.Max(AshTemperature, Rules.Settings.ashTemperature);
        if (to == CraftLocation.Vat)
            CraftLeather.OnEnterVat(this, p);
        Raise(CraftEventKind.PieceMoved, p.id, to.ToString(), "");
    }

    /// <summary>Turns a piece over (the other face up): it heats evenly in the fire, and the hammer works the other face.</summary>
    public bool TurnPiece(string id, out string reason)
    {
        reason = "";
        if (Piece(id) == null)
            return Refuse("There is no such piece here.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.TurnPiece, piece = id });
        return true;
    }

    private void ApplyTurn(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        p.faceUp = 1 - p.faceUp;
        p.faceBias = -p.faceBias * 0.5;
    }

    /// <summary>Says what the piece is meant to become (a design's part): guides only, it is judged by what it is.</summary>
    public bool SetIntent(string id, string design, string role, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null)
            return Refuse("There is no such piece here.", out reason);
        if (!string.IsNullOrEmpty(design) && Rules.Design(design)?.Component(role) == null)
            return Refuse("That design has no such part.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.SetIntent, piece = id, id = design ?? "", id2 = role ?? "" });
        return true;
    }

    private void ApplyIntent(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        p.intentDesign = op.id;
        p.intentRole = op.id2;
        CraftComponentSpec spec = Rules.Design(op.id)?.Component(op.id2);
        if (spec != null && spec.shape != null && p.kind == CraftPieceKind.Metal)
            p.edges = spec.shape.edge == CraftEdgeKind.Double ? 2 : spec.shape.edge == CraftEdgeKind.Single ? 1 : 0;
    }

    // ------------------------------------------------------------------ fires
    /// <summary>Feeds the forge (<paramref name="furnace"/> false) or the furnace a fuel of the rules.</summary>
    public bool AddFuel(string fuelId, int count, out string reason, bool furnace = false)
    {
        reason = "";
        CraftFuel f = Rules.Fuel(fuelId);
        if (f == null)
            return Refuse($"Unknown fuel '{fuelId}'.", out reason);
        if (furnace ? !Has(CraftTools.Furnace) : !Has(CraftTools.Forge))
            return Refuse(furnace ? "There is no furnace here." : "There is no forge here.", out reason);
        if (count <= 0)
            return Refuse("Nothing to add.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.AddFuel, id = f.id, i0 = furnace ? 1 : 0, i1 = count, label = f.Name });
        return true;
    }

    private void ApplyFuel(CraftOperation op)
    {
        CraftFuel f = Rules.Fuel(op.id);
        if (f == null)
            return;
        CraftFireState fire = op.i0 == 1 ? Furnace.fire : Forge;
        double add = f.energy * op.i1;
        double total = fire.fuel + add;
        double w = total > 1e-9 ? add / total : 1;
        fire.maxTemperature = fire.maxTemperature * (1 - w) + f.maxTemperature * w;
        fire.reducing = fire.reducing * (1 - w) + f.reducing * w;
        fire.carburizing = fire.carburizing * (1 - w) + f.carburizing * w;
        fire.sulfur = fire.sulfur * (1 - w) + f.sulfur * w;
        fire.burnRate = fire.burnRate * (1 - w) + f.burnRate * w;
        fire.smoke = fire.smoke * (1 - w) + f.smoke * w;
        fire.fuel = total;
        fire.lit = true;
        Raise(CraftEventKind.FuelAdded, "", f.id, f.Name, op.i1);
    }

    /// <summary>One pump of the bellows (0-1) on the forge or the furnace.</summary>
    public void Bellows(double strength, bool furnace = false) =>
        Record(new CraftOperation { kind = CraftOpKind.Bellows, a = (float)CraftMath.Clamp01(strength), i0 = furnace ? 1 : 0 });

    /// <summary>The air vent of the forge or the furnace: 1 open, 0 choked.</summary>
    public void SetDamper(double open, bool furnace = false)
    {
        open = CraftMath.Clamp01(open);
        if (Math.Abs(open - (furnace ? Furnace.fire.damper : Forge.damper)) < 1e-3)
            return;
        Record(new CraftOperation { kind = CraftOpKind.SetDamper, a = (float)open, i0 = furnace ? 1 : 0 });
    }

    /// <summary>The oven's setting (°C; 0 = off).</summary>
    public bool SetOven(double celsius, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Oven))
            return Refuse("There is no oven here.", out reason);
        if (Math.Abs(celsius - Oven.setpoint) < 0.5)
            return true;
        Record(new CraftOperation { kind = CraftOpKind.SetOven, a = (float)Math.Max(0, celsius) });
        return true;
    }

    /// <summary>The surroundings' temperature (recorded when it changes by more than half a degree).</summary>
    public void SetAmbient(double celsius)
    {
        if (Math.Abs(celsius - Ambient) <= 0.5)
            return;
        Record(new CraftOperation { kind = CraftOpKind.SetAmbient, a = (float)celsius });
    }

    /// <summary>A surroundings variable (humidity, night...): recorded when it changes by more than <paramref name="epsilon"/>.</summary>
    public void SetVariable(string name, double value, double epsilon = 0.02)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        if (Variables.TryGetValue(name, out double old) && Math.Abs(old - value) <= epsilon)
            return;
        Record(new CraftOperation { kind = CraftOpKind.SetVariable, id = name, a = (float)value });
    }

    public void Suspend()
    {
        if (!IsSuspended) Record(new CraftOperation { kind = CraftOpKind.Suspend });
    }

    public void Resume()
    {
        if (IsSuspended) Record(new CraftOperation { kind = CraftOpKind.Resume });
    }

    // ------------------------------------------------------------------ time
    /// <summary>Lets <paramref name="seconds"/> pass (nothing while suspended): whole steps, the remainder carried over. Returns the steps run.</summary>
    public int Advance(double seconds, int maxSteps = 2000)
    {
        if (IsSuspended || seconds <= 0)
            return 0;
        double step = Rules.Settings.stepSeconds;
        carry += seconds;
        int n = (int)Math.Floor(carry / step + 1e-9);
        if (n > maxSteps)
        {
            n = maxSteps;
            carry = 0;
        }
        else
            carry -= n * step;
        RunSteps(n);
        return n;
    }

    /// <summary>Runs exactly <paramref name="steps"/> steps (tools and replays: ignores suspension).</summary>
    public void RunSteps(int steps)
    {
        for (int i = 0; i < steps; i++)
            StepOnce();
    }

    /// <summary>Runs until the clock reaches <paramref name="seconds"/>.</summary>
    public void RunUntil(double seconds)
    {
        int n = (int)Math.Round((seconds - Elapsed) / Rules.Settings.stepSeconds);
        if (n > 0) RunSteps(n);
    }

    private void StepOnce()
    {
        double dt = Rules.Settings.stepSeconds;
        Step++;
        CraftThermal.StepFire(this, Forge, dt, false);
        if (Has(CraftTools.Furnace))
            CraftThermal.StepFire(this, Furnace.fire, dt, true);
        CraftThermal.StepEnvironment(this, dt);
        for (int i = pieces.Count - 1; i >= 0; i--)
        {
            if (i >= pieces.Count) continue;
            CraftPiece p = pieces[i];
            switch (p.kind)
            {
                case CraftPieceKind.Metal:
                case CraftPieceKind.Wood:
                case CraftPieceKind.Fasteners:
                    CraftThermal.StepPiece(this, p, dt);
                    if (p.kind == CraftPieceKind.Wood) CraftWoodwork.Step(this, p, dt);
                    break;
                case CraftPieceKind.Ore:
                    if (p.location == CraftLocation.Furnace) CraftSmelting.StepOre(this, p, dt);
                    break;
                case CraftPieceKind.Hide:
                    CraftLeather.StepHide(this, p, dt);
                    break;
                case CraftPieceKind.Fibre:
                    CraftFibre.Step(this, p, dt);
                    break;
            }
        }
        if (Has(CraftTools.Furnace))
            CraftSmelting.StepFurnace(this, dt);
        if (Has(CraftTools.Crucible))
            CraftSmelting.StepCrucible(this, dt);
    }

    // ------------------------------------------------------------------ following a workshop that runs elsewhere
    /// <summary>
    /// A copy following a workshop that runs elsewhere (a network client following the server): simulates up to the step
    /// <paramref name="op"/> was recorded at, then applies it - the same operations at the same steps give the same
    /// workshop. False (nothing done) when the copy is already past that step, or more than
    /// <paramref name="maxCatchUp"/> steps behind: restore it from a snapshot instead.
    /// </summary>
    public bool ApplyRemote(CraftOperation op, int maxCatchUp = 3000)
    {
        if (op == null)
            return true;
        if (op.step < Step || op.step - Step > maxCatchUp)
            return false;
        RunSteps((int)(op.step - Step));
        CraftOperation copy = op.Clone();
        Apply(copy);
        history.Add(copy);
        return true;
    }

    /// <summary>
    /// Simulates up to <paramref name="step"/> (a copy following the server's clock, whether or not it is suspended).
    /// False when that step is in the past or more than <paramref name="maxCatchUp"/> steps away.
    /// </summary>
    public bool RunTo(long step, int maxCatchUp = 3000)
    {
        if (step < Step || step - Step > maxCatchUp)
            return false;
        RunSteps((int)(step - Step));
        return true;
    }

    /// <summary>
    /// A fingerprint of what can be seen of the workshop (the clock, the fires, every piece's kind, place, mass, heat
    /// and flaws, the crucible, the vats), rounded so that the last digits of a number do not count. A copy that gives
    /// the same number at the same step shows the same workshop.
    /// </summary>
    public uint Checksum()
    {
        var h = new CraftChecksum();
        h.Add(Step);
        h.Add(IsSuspended ? 1 : 0);
        h.Add(pieces.Count);
        h.Add(Ambient, 1);
        h.Fire(Forge);
        h.Fire(Furnace.fire);
        h.Add(Furnace.slag, 0.1);
        h.Add(Furnace.BloomMass, 0.1);
        h.Add(Crucible.Mass, 0.1);
        h.Add(Crucible.temperature, 1);
        h.Add(Quench.temperature, 0.5);
        h.Add(Oven.temperature, 1);
        foreach (CraftVatState v in Vats)
        {
            h.Add(v.agent);
            h.Add(v.strength, 0.01);
            h.Add(v.temperature, 0.5);
        }
        foreach (CraftPiece p in pieces)
        {
            h.Add(p.id);
            h.Add((int)p.kind);
            h.Add((int)p.location);
            h.Add(p.Mass(Rules), 0.05);
            h.Add(p.MaxTemperature, 1);
            h.Add(p.defects.Count);
            h.Add(p.segments.Count);
            if (p.hide != null && p.hide.cells.Count > 0)
            {
                double tan = 0, wet = 0;
                foreach (HideCell c in p.hide.cells)
                {
                    tan += c.Tannage;
                    wet += c.moisture;
                }
                h.Add(p.hide.cells.Count);
                h.Add(tan / p.hide.cells.Count, 0.002);
                h.Add(wet / p.hide.cells.Count, 0.002);
            }
        }
        return h.Value;
    }

    // ------------------------------------------------------------------ saving and replaying
    /// <summary>Everything about the workshop, ready for JsonUtility (saves, the network, the editor).</summary>
    public CraftWorkshopState Capture() => Capture(true);

    /// <summary>Everything about the workshop; without the history when <paramref name="withHistory"/> is false (network snapshots, saves).</summary>
    public CraftWorkshopState Capture(bool withHistory)
    {
        var s = new CraftWorkshopState
        {
            id = Id, seed = Seed, tools = (int)Tools, step = Step, carry = carry, sequence = sequence, ambient = Ambient,
            suspended = IsSuspended, ashTemperature = AshTemperature,
            forge = Copy(Forge), furnace = Copy(Furnace), crucible = Copy(Crucible), quench = Copy(Quench), oven = Copy(Oven),
        };
        foreach (CraftVatState v in Vats) s.vats.Add(Copy(v));
        foreach (CraftPiece p in pieces) s.pieces.Add(p.Clone());
        foreach (KeyValuePair<string, double> v in Variables) s.variables.Add(new CraftVariable(v.Key, v.Value));
        if (withHistory)
            foreach (CraftOperation op in history) s.history.Add(op.Clone());
        return s;
    }

    private static T Copy<T>(T o) => JsonUtility.FromJson<T>(JsonUtility.ToJson(o));

    /// <summary>A workshop exactly as captured.</summary>
    public static CraftWorkshop Restore(CraftRules rules, CraftWorkshopState s)
    {
        if (s == null)
            throw new ArgumentNullException(nameof(s));
        var w = new CraftWorkshop(rules, (CraftTools)s.tools, s.ambient, s.seed, 0, s.quench?.medium ?? "water", s.id)
        {
            Step = s.step, carry = s.carry, sequence = s.sequence, IsSuspended = s.suspended, AshTemperature = s.ashTemperature,
            Forge = s.forge ?? new CraftFireState(), Furnace = s.furnace ?? new CraftFurnaceState(), Crucible = s.crucible ?? new CraftCrucibleState(),
            Quench = s.quench ?? new CraftQuenchState(), Oven = s.oven ?? new CraftOvenState(),
        };
        w.Vats = new List<CraftVatState>();
        if (s.vats != null)
            foreach (CraftVatState v in s.vats) w.Vats.Add(Copy(v));
        if (s.pieces != null)
            foreach (CraftPiece p in s.pieces) w.pieces.Add(p.Clone());
        if (s.variables != null)
            foreach (CraftVariable v in s.variables) w.Variables[v.name] = v.value;
        if (s.history != null)
            foreach (CraftOperation op in s.history) w.history.Add(op.Clone());
        return w;
    }

    /// <summary>
    /// Rebuilds a workshop from its history: the operations at their steps, the time between simulated. With the same
    /// rules, tools, surroundings and seed it is the same workshop - the proof that a discovery can be made again.
    /// </summary>
    public static CraftWorkshop Replay(CraftRules rules, CraftTools tools, double ambient, int seed, string id, IEnumerable<CraftOperation> operations,
        long untilStep = -1, int vats = 3, string quenchMedium = "water")
    {
        var w = new CraftWorkshop(rules, tools, ambient, seed, vats, quenchMedium, id);
        if (operations != null)
            foreach (CraftOperation op in operations)
            {
                if (op == null) continue;
                if (op.step > w.Step)
                    w.RunSteps((int)(op.step - w.Step));
                CraftOperation copy = op.Clone();
                w.Apply(copy);
                w.history.Add(copy);
            }
        if (untilStep > w.Step)
            w.RunSteps((int)(untilStep - w.Step));
        return w;
    }
}

/// <summary>A whole workshop as plain data (JsonUtility-friendly): restoring it continues exactly where it was.</summary>
[Serializable]
public class CraftWorkshopState
{
    public int version = 1;
    public string id = "";
    public int seed;
    public int tools;
    public long step;
    public double carry;
    public int sequence;
    public double ambient;
    public bool suspended;
    public double ashTemperature;
    public CraftFireState forge = new CraftFireState();
    public CraftFurnaceState furnace = new CraftFurnaceState();
    public CraftCrucibleState crucible = new CraftCrucibleState();
    public CraftQuenchState quench = new CraftQuenchState();
    public CraftOvenState oven = new CraftOvenState();
    public List<CraftVatState> vats = new List<CraftVatState>();
    public List<CraftPiece> pieces = new List<CraftPiece>();
    public List<CraftVariable> variables = new List<CraftVariable>();
    public List<CraftOperation> history = new List<CraftOperation>();

    public string ToJson(bool pretty = false) => JsonUtility.ToJson(this, pretty);

    public static CraftWorkshopState FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonUtility.FromJson<CraftWorkshopState>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>A rounded fingerprint (FNV-1a over quantised numbers): see <see cref="CraftWorkshop.Checksum"/>.</summary>
public struct CraftChecksum
{
    private uint h;
    private bool started;

    public uint Value => started ? h : 2166136261u;

    public void Add(long v)
    {
        if (!started) { h = 2166136261u; started = true; }
        unchecked
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (byte)(v >> (i * 8));
                h *= 16777619u;
            }
        }
    }

    public void Add(int v) => Add((long)v);

    /// <summary>A number rounded to <paramref name="quantum"/> (NaN and infinities count as one value each).</summary>
    public void Add(double v, double quantum)
    {
        if (double.IsNaN(v)) { Add(long.MinValue); return; }
        if (double.IsInfinity(v)) { Add(v > 0 ? long.MaxValue : long.MinValue + 1); return; }
        double q = Math.Round(v / quantum);
        Add(q > 9e18 ? long.MaxValue : q < -9e18 ? long.MinValue + 2 : (long)q);
    }

    public void Add(string s)
    {
        if (s == null) { Add(-1); return; }
        Add(s.Length);
        foreach (char c in s) Add((int)c);
    }

    public void Fire(CraftFireState f)
    {
        if (f == null) { Add(-2); return; }
        Add(f.lit ? 1 : 0);
        Add(f.fuel, 0.05);
        Add(f.temperature, 1);
        Add(f.damper, 0.01);
    }
}
