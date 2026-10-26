using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a piece on a crafting station is. Every material between the raw resource and the finished item is a piece
/// with its own state (<see cref="CraftPiece"/>): an ore lump, a bloom, an ingot, a bar being forged, a hide being
/// tanned, a cut strap, a carved grip, a set of rivets, an item being assembled. Serialized as numbers: only add new
/// values at the END.
/// </summary>
public enum CraftPieceKind
{
    /// <summary>A lump of ore: mineral, loose rock and dirt, broken into fragments while it is cleaned.</summary>
    Ore,
    /// <summary>Metal of any composition and shape: a bloom, a cake, an ingot, a bar, a blank, a blade, a plate.</summary>
    Metal,
    /// <summary>A hide, raw or tanned (leather): a grid of cells, each with its own thickness, tannage and damage.</summary>
    Hide,
    /// <summary>A piece of wood (a grip, a haft, a shaft, a board): shaped like a bar.</summary>
    Wood,
    /// <summary>Rivets, studs or buckles: a counted set made of one metal.</summary>
    Fasteners,
    /// <summary>An item being put together from its components (<see cref="CraftDesign"/>).</summary>
    Assembly,
}

/// <summary>Where a piece is in the workshop: decides how it heats, cools, dries and soaks. Serialized as numbers: add at the END.</summary>
public enum CraftLocation
{
    /// <summary>On the bench, the anvil, the grinder, the rack of tools: in the air of the room.</summary>
    Air,
    /// <summary>In the forge fire (at a depth, with part of its length in the coals).</summary>
    Fire,
    /// <summary>Charged into the smelting furnace (ore being roasted or reduced).</summary>
    Furnace,
    /// <summary>Melted into the crucible (it is part of the melt now).</summary>
    Melt,
    /// <summary>Immersed in the quench tank.</summary>
    Quench,
    /// <summary>In the regulated oven (tempering, annealing, stress relief).</summary>
    Oven,
    /// <summary>Buried in warm ashes (very slow cooling: annealing).</summary>
    Ashes,
    /// <summary>Soaking in one of the vats (water, lime, tanning liquor, pickle).</summary>
    Vat,
    /// <summary>Laced in the stretching frame.</summary>
    Frame,
    /// <summary>Hung on the drying rack.</summary>
    Rack,
    /// <summary>Part of an assembly (a component).</summary>
    Assembled,
    /// <summary>In the steam box (wood softening for bending).</summary>
    Steam,
    /// <summary>Stacked to season in the drying stack.</summary>
    Stack,
}

/// <summary>What the player (or a script) did in a workshop. Serialized as numbers: only add new values at the END.</summary>
public enum CraftOpKind
{
    AddPiece,
    RemovePiece,
    MovePiece,
    TurnPiece,
    SetAmbient,
    SetVariable,
    Suspend,
    Resume,
    AddFuel,
    Bellows,
    SetDamper,
    SetOven,
    CrushOre,
    DiscardFragment,
    WashOre,
    SieveOre,
    AddFlux,
    TapSlag,
    ExtractBloom,
    StirMelt,
    SkimDross,
    Pour,
    Strike,
    Fold,
    Weld,
    FormFastener,
    Agitate,
    ApplyClay,
    Descale,
    Grind,
    File,
    Polish,
    Coat,
    Sharpen,
    Scrape,
    FillVat,
    DrainVat,
    SetVatHeat,
    StirVat,
    Stretch,
    Stake,
    Condition,
    Dye,
    Slick,
    Skive,
    CutPattern,
    StartAssembly,
    Attach,
    Detach,
    JointWork,
    Finish,
    SplitPiece,
    TakeMelt,
    SetIntent,
    Carve,
    BrushOre,
    Rive,
    BendWood,
    SawCut,
    StackWood,
    SandWood,
    Varnish,
}

/// <summary>A defect a piece can carry. Visible or hidden, recoverable or not (<see cref="CraftDefect"/>). Serialized as numbers: add at the END.</summary>
public enum CraftDefectKind
{
    /// <summary>A crack: from a hard quench, forging too cold, hot shortness, a brittle metal under the hammer.</summary>
    Crack,
    /// <summary>A fold hammered into the surface (a cold shut): a seam that never welded.</summary>
    ColdShut,
    /// <summary>Slag or dross trapped in the metal.</summary>
    Inclusion,
    /// <summary>Gas holes from casting.</summary>
    Porosity,
    /// <summary>Layers that did not weld (a bad fold or forge weld).</summary>
    Delamination,
    /// <summary>Steel overheated until its grain boundaries melted: irreversible.</summary>
    Burnt,
    /// <summary>Scale hammered into the surface.</summary>
    ScalePit,
    /// <summary>Ground or filed too thin.</summary>
    Overground,
    /// <summary>A chipped or rolled edge.</summary>
    EdgeChip,
    /// <summary>A hole through a hide (a wound, a slip of the knife).</summary>
    Hole,
    /// <summary>A cut or score in a hide from the scraping knife.</summary>
    Score,
    /// <summary>Leather that shrank (overheated while wet).</summary>
    Shrunk,
    /// <summary>Leather tanned too fast on the outside: the core stayed raw.</summary>
    CaseHardened,
    /// <summary>Rot or hair slip: a hide left raw and wet too long.</summary>
    Rot,
    /// <summary>A rivet or a stitch that holds badly.</summary>
    LooseJoint,
    /// <summary>A rivet that cracked as it was set.</summary>
    CrackedRivet,
    /// <summary>Holes or parts that do not line up.</summary>
    Misaligned,
    /// <summary>A gap between parts that should fit tightly.</summary>
    Gap,
    /// <summary>A misrun casting: the mould did not fill.</summary>
    Misrun,
    /// <summary>A hide or leather that tore under tension.</summary>
    Tear,
    /// <summary>Uneven dye.</summary>
    Blotch,
    /// <summary>A sharp, unfinished metal edge that chafes leather.</summary>
    RoughEdge,
}

/// <summary>The face of a bar the hammer works. Serialized as numbers: add at the END.</summary>
public enum CraftStrikeFace
{
    /// <summary>On the flat: thins the thickness, spreads the metal.</summary>
    Flat,
    /// <summary>On the edge (the bar turned 90°): narrows the width, thickens it (upsetting the width).</summary>
    Edge,
}

/// <summary>What the hammer (or the tool under it) is. Serialized as numbers: add at the END.</summary>
public enum CraftStrikeTool
{
    /// <summary>The flat face of the hammer: spreads the metal both ways, smooths.</summary>
    Flat,
    /// <summary>The cross peen, across the bar: draws it out (makes it longer).</summary>
    PeenAcross,
    /// <summary>The cross peen, along the bar: spreads it (makes it wider).</summary>
    PeenAlong,
    /// <summary>A fuller: a groove down the middle (lighter, stiffer).</summary>
    Fuller,
    /// <summary>Over the horn of the anvil: bends the bar.</summary>
    Horn,
    /// <summary>On the hardy: cuts the bar where it is struck.</summary>
    Hardy,
    /// <summary>A drift or punch: makes and opens a hole (an eye, a socket, a slot).</summary>
    Drift,
    /// <summary>A flatter or set hammer: evens the surface without moving much metal.</summary>
    Flatter,
    /// <summary>A dishing hammer over a hollow stump: dishes a plate.</summary>
    Dishing,
    /// <summary>On the face of the anvil, struck to straighten a bend or a warp.</summary>
    Straighten,
}

/// <summary>Which part of a bar's cross-section a grinder, file, stone or whetstone works. Serialized as numbers: add at the END.</summary>
public enum CraftSurfaceSide
{
    /// <summary>The left face toward the edge: the left bevel.</summary>
    LeftBevel,
    /// <summary>The right face toward the edge: the right bevel.</summary>
    RightBevel,
    /// <summary>Both flats (flattening, cleaning up hammer marks).</summary>
    Faces,
    /// <summary>The edge line itself (grinding it back: narrower).</summary>
    EdgeLine,
    /// <summary>The spine (the back).</summary>
    Spine,
    /// <summary>The tang (its width and thickness: to fit the handle and the guard).</summary>
    Tang,
    /// <summary>The inside of an eye, socket or slot (opening it up).</summary>
    Eye,
}

/// <summary>Which edge a design or component has along its length. Serialized as numbers: add at the END.</summary>
public enum CraftEdgeKind
{
    None,
    /// <summary>One cutting edge (a knife, a falchion, an axe bit).</summary>
    Single,
    /// <summary>Both sides cut (a sword, a dagger, a spear head).</summary>
    Double,
}

/// <summary>What a vat holds is used for. Serialized as numbers: add at the END.</summary>
public enum CraftAgentKind
{
    /// <summary>Plain water: soaking, rinsing, boiling (hardening leather).</summary>
    Water,
    /// <summary>Lime or wood ash: loosens hair.</summary>
    Lime,
    /// <summary>Bark liquor: vegetable tanning - firm, durable leather.</summary>
    Vegetable,
    /// <summary>Alum and salt: tawing - soft, pale, but it washes out.</summary>
    Mineral,
    /// <summary>Oils (brain, fish): soft, supple, stretchy leather.</summary>
    Oil,
    /// <summary>An alchemical tanning essence.</summary>
    Alchemical,
    /// <summary>Vinegar or another acid: pickling metal (eats scale, etches).</summary>
    Acid,
}

/// <summary>What a supply is for. Serialized as numbers: add at the END.</summary>
public enum CraftSupplyKind
{
    /// <summary>A flux for the furnace (limestone) or for welding and melting (borax, sand).</summary>
    Flux,
    /// <summary>Thread, sinew, cord for sewing and wrapping.</summary>
    Thread,
    /// <summary>Wool or linen padding under armour.</summary>
    Padding,
    /// <summary>Wax for leather or a metal finish.</summary>
    Wax,
    /// <summary>Oil for leather (neatsfoot) or metal (linseed).</summary>
    Oil,
    /// <summary>A dye.</summary>
    Dye,
    /// <summary>Clay for differential hardening.</summary>
    Clay,
    /// <summary>A bone-ash cupel (refining silver from lead).</summary>
    Cupel,
    /// <summary>Anything else.</summary>
    Other,
    /// <summary>Raw fibre for spinning (wool, flax, hemp).</summary>
    Fibre,
    /// <summary>A varnish or a finishing oil for wood.</summary>
    Varnish,
}

/// <summary>What a joint of a design is. Serialized as numbers: add at the END.</summary>
public enum CraftJointKind
{
    /// <summary>A part slid onto a tang that must fit it (a guard, a grip): file to fit, close a gap by peening.</summary>
    Fit,
    /// <summary>The end of the tang hammered over (peened) to lock a pommel.</summary>
    Peen,
    /// <summary>Rivets through punched holes, peened over.</summary>
    Rivet,
    /// <summary>Stitches through punched holes.</summary>
    Stitch,
    /// <summary>A leather strip or cord wound round a grip.</summary>
    Wrap,
    /// <summary>A head driven onto a haft and wedged.</summary>
    Wedge,
    /// <summary>A socket over a shaft, pinned through.</summary>
    Socket,
    /// <summary>A buckle on a strap.</summary>
    Buckle,
}

/// <summary>What an item design makes. Serialized as numbers: add at the END.</summary>
public enum CraftDesignKind
{
    Weapon,
    Armor,
    Tool,
    Shield,
}

/// <summary>What kind of fastener a set is. Serialized as numbers: add at the END.</summary>
public enum CraftFastenerKind
{
    Rivet,
    Stud,
    Buckle,
}

/// <summary>How a piece in the quench tank is moved. Serialized as numbers: add at the END.</summary>
public enum CraftQuenchMotion
{
    /// <summary>Held still: a vapour jacket clings to it and it cools unevenly.</summary>
    Still,
    /// <summary>Up and down along its length: breaks the vapour jacket evenly (the right way for a blade).</summary>
    UpDown,
    /// <summary>Side to side: breaks the vapour, but cools one face first - it warps.</summary>
    SideToSide,
    /// <summary>Swirled in circles.</summary>
    Swirl,
}

/// <summary>Something that happened in a workshop (the log, the journal, sounds, effects).</summary>
public enum CraftEventKind
{
    PieceAdded,
    PieceRemoved,
    PieceMoved,
    PieceCreated,
    PieceDestroyed,
    Refused,
    FireOut,
    FuelAdded,
    Struck,
    Cracked,
    Defect,
    Melted,
    Burning,
    ScaleFlaked,
    MagnetLost,
    Hardened,
    Quenched,
    QuenchFlare,
    Tempered,
    Smelted,
    SlagTapped,
    Poured,
    Cut,
    Ground,
    Sharpened,
    Scraped,
    Tanned,
    Dried,
    Torn,
    Joined,
    Finished,
    Suspended,
    Resumed,
    Note,
}

/// <summary>A named number on a piece's record or a workshop's surroundings.</summary>
[Serializable]
public class CraftVariable
{
    public string name = "";
    public double value;

    public CraftVariable() { }

    public CraftVariable(string name, double value)
    {
        this.name = name;
        this.value = value;
    }
}

/// <summary>An amount of one element (by id), in grams.</summary>
[Serializable]
public class CraftAmount
{
    [Tooltip("Id of the element (see the Crafting Database's Elements).")]
    public string element = "";
    [Tooltip("Grams.")]
    public double grams;

    public CraftAmount() { }

    public CraftAmount(string element, double grams)
    {
        this.element = element;
        this.grams = grams;
    }

    public CraftAmount Clone() => new CraftAmount(element, grams);
}

/// <summary>Something that happened in a workshop. Ids name what it is about; the UI words it by what the player knows.</summary>
public readonly struct CraftEvent
{
    public readonly CraftEventKind Kind;
    /// <summary>Workshop time (seconds) it happened at.</summary>
    public readonly double Time;
    /// <summary>The piece it is about (may be empty).</summary>
    public readonly string Piece;
    /// <summary>A tool, fuel, defect, element... id (may be empty).</summary>
    public readonly string Id;
    /// <summary>What can be seen, heard or felt - it never reveals what the player cannot know.</summary>
    public readonly string Text;
    public readonly float Value;

    public CraftEvent(CraftEventKind kind, double time, string piece, string id, string text, float value = 0f)
    {
        Kind = kind;
        Time = time;
        Piece = piece ?? "";
        Id = id ?? "";
        Text = text ?? "";
        Value = value;
    }

    public override string ToString() => $"[{CraftText.Clock(Time)}] {Kind} {Piece} {Id} {Text}";
}

/// <summary>Formatting shared by the station window, the journal and the tools.</summary>
public static class CraftText
{
    /// <summary>"1:05" (minutes:seconds), "2 h 05" past an hour.</summary>
    public static string Clock(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds)) seconds = 0;
        int s = (int)Math.Floor(seconds);
        return s >= 3600 ? $"{s / 3600} h {s / 60 % 60:00}" : $"{s / 60}:{s % 60:00}";
    }

    /// <summary>"850 °C".</summary>
    public static string Celsius(double t) => $"{Math.Round(t):0} °C";

    /// <summary>"1.25 kg", "340 g", "4.2 g".</summary>
    public static string Mass(double grams) =>
        grams >= 1000 ? $"{grams / 1000:0.00} kg" : grams >= 10 ? $"{grams:0} g" : $"{grams:0.0} g";

    /// <summary>"12.5 mm", "1.2 m".</summary>
    public static string Length(double mm) => mm >= 1000 ? $"{mm / 1000:0.00} m" : mm >= 10 ? $"{mm:0} mm" : $"{mm:0.0} mm";

    /// <summary>"0.45 %".</summary>
    public static string Percent(double fraction, int decimals = 2) => (fraction * 100).ToString("0." + new string('0', Math.Max(0, decimals))) + " %";

    /// <summary>"#RRGGBB" for rich text.</summary>
    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    /// <summary>"Hello" from "hello".</summary>
    public static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>"a", "an".</summary>
    public static string A(string word) => !string.IsNullOrEmpty(word) && "aeiouAEIOU".IndexOf(word[0]) >= 0 ? "an" : "a";
}

/// <summary>Deterministic helpers shared by the simulation (no UnityEngine.Random anywhere in the rules).</summary>
public static class CraftMath
{
    public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
    public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    public static double Lerp(double a, double b, double t) => a + (b - a) * Clamp01(t);
    public static double InverseLerp(double a, double b, double v) => Math.Abs(b - a) < 1e-12 ? 0 : Clamp01((v - a) / (b - a));

    /// <summary>0 below <paramref name="a"/>, 1 above <paramref name="b"/>, smooth in between.</summary>
    public static double Smooth(double a, double b, double v)
    {
        double t = InverseLerp(a, b, v);
        return t * t * (3 - 2 * t);
    }

    /// <summary>A stable hash of a text (string.GetHashCode differs between runs in .NET).</summary>
    public static int Hash(string s)
    {
        unchecked
        {
            int h = (int)2166136261;
            if (s != null)
                foreach (char c in s)
                    h = (h ^ c) * 16777619;
            return h;
        }
    }

    /// <summary>A stable hash of numbers.</summary>
    public static int Hash(int a, int b, int c = 0)
    {
        unchecked
        {
            uint h = 2166136261;
            h = (h ^ (uint)a) * 16777619;
            h = (h ^ (uint)b) * 16777619;
            h = (h ^ (uint)c) * 16777619;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (int)h;
        }
    }

    /// <summary>A deterministic number in [0, 1) from a seed and an index.</summary>
    public static double Noise(int seed, int index, int salt = 0) => (Hash(seed, index, salt) & 0x7fffffff) / 2147483648.0;

    /// <summary>A deterministic number between <paramref name="min"/> and <paramref name="max"/>.</summary>
    public static double Range(int seed, int index, int salt, double min, double max) => min + (max - min) * Noise(seed, index, salt);
}

/// <summary>A small deterministic random stream that can be saved as one number (for the workshop's defined random variable).</summary>
[Serializable]
public class CraftRandom
{
    // The 64-bit state as two ints: every serializer keeps them exactly.
    public int hi = unchecked((int)0x9E3779B9);
    public int lo = 0x7F4A7C15;

    public CraftRandom() { }

    public CraftRandom(int seed) => State = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    private ulong State
    {
        get => unchecked(((ulong)(uint)hi << 32) | (uint)lo);
        set
        {
            if (value == 0) value = 0x9E3779B97F4A7C15UL;
            hi = unchecked((int)(value >> 32));
            lo = unchecked((int)value);
        }
    }

    public double Next()
    {
        unchecked
        {
            ulong s = State;
            s ^= s << 13;
            s ^= s >> 7;
            s ^= s << 17;
            State = s;
            return (s >> 11) * (1.0 / 9007199254740992.0);
        }
    }

    public CraftRandom Clone() => new CraftRandom { hi = hi, lo = lo };
}
