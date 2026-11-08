using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The true properties of a piece, as they are (the test station shows them; instruments and the final inspection reveal
/// parts of them). Nothing here is a bonus: it is read off the piece's state.
/// </summary>
public sealed class CraftPieceReport
{
    public CraftPiece Piece;
    public CraftPieceKind Kind;
    public string MaterialName = "";
    public CraftAlloyProfile Profile;
    public double Mass, Length, Width, Thickness, Temperature;

    // Metal
    /// <summary>Hardness (HV) along the edge (or the striking face) and of the body.</summary>
    public double EdgeHardness, BodyHardness;
    /// <summary>Toughness (J): the weakest part of the working length, and its mean.</summary>
    public double MinToughness, Toughness, Ductility;
    /// <summary>Share hardened (martensite), how far tempered (0-1), residual stress, grain (1 fine).</summary>
    public double Hardened, Tempered, Stress, Grain, Decarb;
    /// <summary>Edge: sharpness 0-1 (from the apex), inclusive angle (degrees), symmetry 0-1, chips, burr, edge thickness (mm).</summary>
    public double Sharpness, EdgeAngle, Symmetry, Chip, Burr, EdgeThickness;
    /// <summary>
    /// The inclusive angle (degrees) its design wants along the same stretches, as the design's section carries it there
    /// (<see cref="CraftSurface.EdgeHalfAngle"/>: steeper where the section is too narrow for the design's angle); 0
    /// without a design.
    /// </summary>
    public double IdealEdgeAngle;
    public bool HasEdge;
    /// <summary>Surface: roughness (µm), scale (mm), polish 0-1, coating 0-1, corrosion resistance 0-1.</summary>
    public double Roughness, Scale, Polish, Coating, Corrosion;
    /// <summary>Total bend (degrees), mean dish (mm), the largest eye (mm), slag share, porosity.</summary>
    public double Bend, Dish, Eye, SlagShare, Porosity;
    /// <summary>Streaks of a metal never mixed in (0-1: a casting of a melt not stirred even, <see cref="CraftPiece.inhomogeneity"/>): it is weaker where they meet.</summary>
    public double Streaks;
    /// <summary>0-1: what the defects leave of it.</summary>
    public double Integrity = 1;
    /// <summary>How close it is to its intended shape (0-1), and why not.</summary>
    public double Shape = -1;
    public readonly List<string> ShapeNotes = new List<string>();

    // Leather
    public double Tannage, CoreTannage, Suppleness, Strength, WaterResistance, Damage, Shrink, DyeEvenness = 1, Moisture, Stiffness, Hard, Fire;
    /// <summary>Leather: how far its grain is slicked and sealed (0-1); how much it puckered drying unstretched (0-0.3); how far out of square it dried (0-1).</summary>
    public double Slicked, Puckered, Distortion;
    /// <summary>Leather: how much of its hair it was tanned with (0 unhaired - 1 all it grew, <see cref="CraftLeather.HairLeft(CraftHideDefinition, double)"/>).</summary>
    public double HairLeft;
    /// <summary>A sheet of cloth (a textile hide) rather than leather.</summary>
    public bool Cloth;
    public int Cells;

    // Wood
    public double WoodToughness, WoodHardness, Split, Charred;

    // Fasteners
    public int Count;
    public double FastenerQuality;

    // Gems, fibre and yarn
    public CraftGemGrade Gem;
    public double FibrePrep, YarnQuality;

    public readonly List<string> Defects = new List<string>();
}

/// <summary>What a finished item is: its numbers (as a weapon, a tool, armour or a shield) and why.</summary>
public sealed class CraftItemReport
{
    public CraftDesign Design;
    public string Name = "";
    public string MaterialWord = "";
    public double Quality;
    public string Tier = "";
    /// <summary>The parts of the quality (0-1).</summary>
    public double Material, Workmanship, Integrity, Edge, HeatTreatment, Assembly, Protection;

    // Weapons and tools.
    public float DamageMin, DamageMax, CritChance, CritMultiplier, Knockback, AttackSpeed = 1, ToolDamage, ElementBuildup;
    public DamageForm DamageForm;
    public WeaponCategory Category;
    public WeaponScaling Scaling;
    public ElementType Element = ElementType.None;
    /// <summary>The balance point (mm in front of the guard, or from the hand on a haft).</summary>
    public double Balance;

    // Armour and shields.
    public float Defense, MagicDefense;
    public readonly List<ElementalResistanceValue> Resistances = new List<ElementalResistanceValue>();

    // Both.
    public int Durability;
    public float Weight;
    public float Price;
    public Color Color = new Color(0.6f, 0.6f, 0.62f);
    public readonly List<string> Notes = new List<string>();
    public readonly List<string> Parts = new List<string>();
    /// <summary>The pieces it is made of (role, piece, mass): the parts named as a player knows them (<see cref="CraftEvaluator.KnownParts"/>).</summary>
    public readonly List<CraftItemPart> PartPieces = new List<CraftItemPart>();
    /// <summary>A weapon whose main part is wood (a bow, a staff, a club): it has no edge and no heat treatment - its <see cref="Wood"/> says how well the wood suits it.</summary>
    public bool Wooden;
    /// <summary>A steel part of it is coarse-grained (<see cref="CraftEvaluator.CoarseGrain"/>): held too hot too long and never normalised - what the journal learns from it.</summary>
    public bool CoarseGrained;
    /// <summary>How well the main part's wood suits the weapon (0-1): a bow wants a springy, tough wood, a club or staff a tough, hard one.</summary>
    public double Wood;
}

/// <summary>One piece of a finished item, as the evaluator read it (or a supply that went into it: no piece).</summary>
public struct CraftItemPart
{
    /// <summary>The part's role and name in the design ("blade", "Blade").</summary>
    public string role, name;
    /// <summary>The piece; null for the supplies that went into it (its padding, its thread), named by their role.</summary>
    public CraftPiece piece;
    public double mass;
    /// <summary>It is (one of) the design's main part.</summary>
    public bool main;
}

/// <summary>An elemental resistance as a plain value (kept here so the core needs no item types).</summary>
[Serializable]
public struct ElementalResistanceValue
{
    public ElementType element;
    public float percent;

    public ElementalResistanceValue(ElementType element, float percent)
    {
        this.element = element;
        this.percent = percent;
    }
}

/// <summary>
/// Reads pieces and finished items. Pieces: hardness and toughness of their edge and body, the edge's sharpness,
/// angle and symmetry, the surface, the bend, the defects, how close they are to their intended shape; leather's
/// tannage, suppleness, strength and water resistance; wood; fasteners. Items: every part read, the balance and mass
/// of the whole, every joint, into the item's numbers - a hard but brittle edge chips (less durability), a soft one
/// cuts less, a cracked blade breaks early, a loose joint wastes the blow, a well-made plate badly riveted is weak
/// armour. Pure: it changes nothing.
/// </summary>
public static class CraftEvaluator
{
    /// <summary>
    /// A steel whose grain (1 fine; it grows while held hot past its grain-growth heat, and heating it back through its
    /// critical heat mostly resets it) is coarser than this on average is coarse-grained: weak, and the inspection says so.
    /// Forged and then hardened from a fresh heat it ends about 1.5; quenched straight from a long forging heat, over 2.
    /// </summary>
    public const double CoarseGrain = 2.0;

    /// <summary>The grain of a metal piece by its length (the coarser of each stretch's two zones), as <see cref="CraftPieceReport.Grain"/> reads it without the tang.</summary>
    public static double MeanGrain(CraftPiece p)
    {
        double g = 0, l = 0;
        if (p != null)
            foreach (MetalSegment s in p.segments) { g += Math.Max(s.edge.grain, s.body.grain) * s.length; l += s.length; }
        return l > 1e-9 ? g / l : 1;
    }

    // ------------------------------------------------------------------ pieces
    public static CraftPieceReport Piece(CraftRules rules, CraftPiece p)
    {
        var r = new CraftPieceReport { Piece = p, Kind = p.kind };
        if (p == null)
            return r;
        r.Mass = p.Mass(rules);
        switch (p.kind)
        {
            case CraftPieceKind.Metal:
            case CraftPieceKind.Fasteners:
                Metal(rules, p, r);
                break;
            case CraftPieceKind.Wood:
                Wood(rules, p, r);
                break;
            case CraftPieceKind.Hide:
                Leather(rules, p, r);
                break;
            case CraftPieceKind.Ore:
                r.MaterialName = rules.Ore(p.ore.ore)?.Name ?? p.ore.ore;
                break;
            case CraftPieceKind.Assembly:
                r.MaterialName = rules.Design(p.assembly.design)?.Name ?? "";
                break;
            case CraftPieceKind.Gem:
                r.MaterialName = rules.Gem(p.gem.gem)?.Name ?? p.gem.gem;
                r.Gem = CraftLapidary.Grade(rules, p);
                r.Integrity = r.Gem.Integrity;
                r.Polish = r.Gem.Polish;
                r.Width = r.Length = p.gem.girdle;
                break;
            case CraftPieceKind.Fibre:
                r.MaterialName = rules.Supply(p.fibre.fibre)?.Name ?? p.fibre.fibre;
                r.FibrePrep = CraftFibre.Prep(p.fibre);
                r.Moisture = p.fibre.wet;
                break;
            case CraftPieceKind.Yarn:
                r.MaterialName = rules.Supply(p.yarn.fibre)?.Name ?? p.yarn.fibre;
                r.YarnQuality = CraftFibre.Quality(p.yarn);
                r.Strength = p.yarn.strength;
                r.Length = p.yarn.length * 1000;
                break;
        }
        foreach (CraftDefect d in p.defects)
            r.Defects.Add($"{d.kind} ({d.severity:0.00}{(d.at >= 0 ? $" at {d.at}" : "")}{(d.surface ? ", visible" : ", hidden")}): {d.cause}");
        return r;
    }

    private static void Geometry(CraftPiece p, CraftPieceReport r)
    {
        r.Length = p.Length;
        double w = 0, t = 0, l = 0;
        foreach (MetalSegment s in p.segments)
        {
            w += s.width * s.length;
            t += s.thickness * s.length;
            l += s.length;
            r.Bend += s.bend;
            r.Dish += s.dish * s.length;
            r.Eye = Math.Max(r.Eye, s.hole);
        }
        if (l > 0)
        {
            r.Width = w / l;
            r.Thickness = t / l;
            r.Dish /= l;
        }
        r.Temperature = p.MeanTemperature;
    }

    private static void Metal(CraftRules rules, CraftPiece p, CraftPieceReport r)
    {
        Geometry(p, r);
        CraftAlloyProfile prof = p.Profile(rules);
        r.Profile = prof;
        r.MaterialName = prof.ClassName;
        if (p.kind == CraftPieceKind.Fasteners)
        {
            r.Count = p.fasteners.count;
            r.FastenerQuality = p.fasteners.quality;
        }
        CraftShapeTarget target = rules.Design(p.intentDesign)?.Component(p.intentRole)?.shape;
        double tangTo = target?.tangTo ?? 0;
        double edgeFrom = target != null && target.edge != CraftEdgeKind.None ? target.edgeFrom : 0, edgeTo = target != null ? target.edgeTo : 1;
        double he = 0, hb = 0, wsum = 0, tough = 0, minTough = double.MaxValue, duct = 0, mart = 0, temp = 0, stress = 0, grain = 0, decarb = 0;
        double sharp = 0, angle = 0, ideal = 0, sym = 0, chip = 0, burr = 0, edgeT = 0, esum = 0;
        int targetEdges = target != null && target.edge == CraftEdgeKind.Double ? 2 : 1;
        double rough = 0, scale = 0, polish = 0, coat = 0, slag = 0, poro = 0;
        int n = p.segments.Count;
        for (int i = 0; i < n; i++)
        {
            MetalSegment s = p.segments[i];
            double t = p.PositionOf(i);
            double w = s.length;
            double edgeThick = Math.Max(0.2, (s.EdgeThickness + s.thickness) * 0.5);
            double hE = CraftMetallurgy.Hardness(rules, prof, s, s.edge, edgeThick), hB = CraftMetallurgy.Hardness(rules, prof, s, s.body, s.thickness);
            double tB = CraftMetallurgy.Toughness(rules, prof, s, s.body, s.thickness), tE = CraftMetallurgy.Toughness(rules, prof, s, s.edge, edgeThick);
            double tg = Math.Min(tB, tE * 1.3);
            rough += s.rough * w;
            scale += s.scale * w;
            polish += s.polish * w;
            coat += s.coating * w;
            slag += s.slag;
            poro += s.porosity * w;
            wsum += w;
            if (t < tangTo)
                continue; // the tang: hidden in the handle
            he += hE * w;
            hb += hB * w;
            tough += tg * w;
            minTough = Math.Min(minTough, tg);
            duct += CraftMetallurgy.Ductility(rules, prof, s, s.body, s.thickness) * w;
            mart += (s.edge.mart + s.body.mart) * 0.5 * w;
            temp += (CraftMetallurgy.TemperFraction(s.edge.temperP) + CraftMetallurgy.TemperFraction(s.body.temperP)) * 0.5 * w;
            stress += Math.Max(s.edge.stress, s.body.stress) * w;
            grain += Math.Max(s.edge.grain, s.body.grain) * w;
            decarb += s.edge.decarb * w;
            bool inEdge = t >= edgeFrom - 1e-6 && t <= edgeTo + 1e-6;
            if (inEdge && (target == null || target.edge != CraftEdgeKind.None) && (s.Beveled || target != null))
            {
                sharp += Sharpness(s) * w;
                angle += s.EdgeAngle * w;
                if (target != null)
                {
                    target.At((float)t, out float tw, out float tt);
                    ideal += 2 * CraftSurface.EdgeHalfAngle(tw, tt, target.edgeAngle, targetEdges) * w;
                }
                double a = Math.Abs((s.microL > 0 ? s.microL : s.AngleL) - (s.microR > 0 ? s.microR : s.AngleR));
                double depths = s.bevelDepthL + s.bevelDepthR;
                double off = depths > 1e-6 ? Math.Abs(s.bevelDepthL - s.bevelDepthR) / depths : 0;
                // the faces honed alike, as far as the honing goes (a face is honed to the apex at 1: its bar full)
                sym += CraftMath.Clamp01(1 - a / 12 - off * (p.edges == 2 ? 0.8 : 0.2) - Math.Abs(Math.Min(1, s.honeL) - Math.Min(1, s.honeR)) * 0.2) * w;
                chip += s.chip * w;
                burr += s.burr * w;
                edgeT += s.EdgeThickness * w;
                esum += w;
            }
        }
        double work = Math.Max(1e-9, wsum * (1 - Math.Min(0.95, tangTo)));
        double worked = 0;
        for (int i = 0; i < n; i++) if (p.PositionOf(i) >= tangTo) worked += p.segments[i].length;
        work = Math.Max(1e-9, worked);
        r.EdgeHardness = he / work;
        r.BodyHardness = hb / work;
        r.Toughness = tough / work;
        r.MinToughness = minTough == double.MaxValue ? r.Toughness : minTough;
        // A casting of a melt not mixed even: streaks of different metal run through it, and it breaks where they meet.
        r.Streaks = CraftMath.Clamp01(p.inhomogeneity);
        r.Toughness *= 1 - CraftSmelting.StreakWeakness * r.Streaks;
        r.MinToughness *= 1 - CraftSmelting.StreakWeakness * r.Streaks;
        r.Ductility = duct / work;
        r.Hardened = mart / work;
        r.Tempered = temp / work;
        r.Stress = stress / work;
        r.Grain = grain / work;
        r.Decarb = decarb / work;
        r.HasEdge = esum > 0 && (p.edges > 0 || target != null && target.edge != CraftEdgeKind.None);
        if (esum > 0)
        {
            r.Sharpness = sharp / esum;
            r.EdgeAngle = angle / esum;
            r.IdealEdgeAngle = ideal / esum;
            r.Symmetry = sym / esum;
            r.Chip = chip / esum;
            r.Burr = burr / esum;
            r.EdgeThickness = edgeT / esum;
        }
        r.Roughness = rough / Math.Max(1e-9, wsum);
        r.Scale = scale / Math.Max(1e-9, wsum);
        r.Polish = polish / Math.Max(1e-9, wsum);
        r.Coating = coat / Math.Max(1e-9, wsum);
        r.Porosity = poro / Math.Max(1e-9, wsum);
        r.SlagShare = r.Mass > 0 ? slag / r.Mass : 0;
        r.Corrosion = CraftMath.Clamp01(prof.Corrosion + 0.25 * r.Polish * (1 - r.Scale * 5) + 0.35 * r.Coating);
        r.Integrity = Integrity(p, r);
        if (target != null)
            r.Shape = ShapeScore(rules, p, target, r.ShapeNotes);
    }

    /// <summary>Sharpness 0-1 from the apex radius (1 µm razor - 300 µm blunt), less for a burr and chips.</summary>
    public static double Sharpness(MetalSegment s)
    {
        double apex = Math.Max(0.5, s.apex) * (1 + 1.5 * s.burr);
        double v = 1 - Math.Log10(apex) / Math.Log10(300);
        return CraftMath.Clamp01(v) * (1 - 0.6 * s.chip);
    }

    private static double Integrity(CraftPiece p, CraftPieceReport r)
    {
        double k = 1;
        foreach (CraftDefect d in p.defects)
        {
            switch (d.kind)
            {
                case CraftDefectKind.Crack: k *= Math.Exp(-d.severity * 1.2); break;
                case CraftDefectKind.Delamination: k *= Math.Exp(-d.severity * 0.8); break;
                case CraftDefectKind.ColdShut: k *= Math.Exp(-d.severity * 0.6); break;
                case CraftDefectKind.Burnt: k *= 1 - 0.7 * CraftMath.Clamp01(d.severity); break;
                case CraftDefectKind.Overground: k *= Math.Exp(-d.severity * 0.6); break;
                case CraftDefectKind.Misrun: k *= 1 - 0.6 * CraftMath.Clamp01(d.severity); break;
                case CraftDefectKind.ScalePit: k *= Math.Exp(-d.severity * 0.3); break;
                case CraftDefectKind.CrackedRivet: k *= 0.9; break;
                case CraftDefectKind.Hole: k *= Math.Exp(-d.severity * 0.5); break;
                case CraftDefectKind.Score: k *= Math.Exp(-d.severity * 0.3); break;
                case CraftDefectKind.Tear: k *= Math.Exp(-d.severity * 0.6); break;
                case CraftDefectKind.Rot: k *= 1 - 0.6 * CraftMath.Clamp01(d.severity); break;
                case CraftDefectKind.CaseHardened: k *= 0.8; break;
                case CraftDefectKind.Shrunk: k *= 1 - 0.5 * CraftMath.Clamp01(d.severity * 2); break;
            }
        }
        k *= 1 - Math.Min(0.6, r.SlagShare * 3);
        k *= 1 - Math.Min(0.6, r.Porosity * 2);
        k *= 1 - CraftSmelting.StreakWeakness * CraftMath.Clamp01(p.inhomogeneity); // streaks of a metal never mixed in
        return CraftMath.Clamp01(k);
    }

    /// <summary>A part's length is noted as too short or too long (and the guide asks for it to be put right) once <see cref="LengthScore"/> falls below this.</summary>
    public const double LengthNoted = 0.95;

    /// <summary>
    /// How well a length (mm) suits a range: 1 inside it, falling away outside (8% of the range's middle beyond it is
    /// about 0.37). One rule for the evaluator's score and note and for the guide's "saw it to length".
    /// </summary>
    public static double LengthScore(double length, double min, double max) =>
        length >= min && length <= max ? 1 : Math.Exp(-Math.Pow((length < min ? min - length : length - max) / Math.Max(1, (min + max) * 0.5) / 0.08, 2));

    /// <summary>A part's profile is noted as off ("the width is off along it") once its fit (<see cref="ProfileFit"/>) falls below this.</summary>
    public const double ProfileNoted = 0.9;

    /// <summary>The profile's fit (0-1) for an error of <paramref name="error"/> (<see cref="ProfileError"/>): 1 exact, about 0.9 at 6% off.</summary>
    public static double ProfileFit(double error) => Math.Exp(-Math.Pow(Math.Max(0, error) / 0.18, 2));

    /// <summary>
    /// How far a piece's width and thickness are off a target's profile, on the whole (0 exact): the length-weighted
    /// error of each stretch against the shape there, width counting 0.6 and thickness 0.4 - one rule for the score, its
    /// note and the guide's "carve it to its shape". <paramref name="overOnly"/>: only where it is wider or thicker than
    /// the shape (what a knife can still take off). <paramref name="widthMore"/>: is the width more off than the thickness?
    /// </summary>
    public static double ProfileError(CraftPiece p, CraftShapeTarget target, bool overOnly, out bool widthMore) =>
        ProfileError(p, target, overOnly, out widthMore, out _, out _, out _, out _);

    private static double ProfileError(CraftPiece p, CraftShapeTarget target, bool overOnly, out bool widthMore, out double worstW, out int widest, out double worstT, out int thickest)
    {
        widthMore = false;
        worstW = worstT = 0;
        widest = thickest = -1;
        if (p == null || target == null || target.profile == null || target.profile.Count < 2 || p.segments.Count == 0)
            return 0;
        double err = 0, wsum = 0, errW = 0, errT = 0;
        for (int i = 0; i < p.segments.Count; i++)
        {
            MetalSegment s = p.segments[i];
            target.At((float)p.PositionOf(i), out float tw, out float tt);
            double dw = s.width - tw, dt = s.thickness - tt;
            if (overOnly) { dw = Math.Max(0, dw); dt = Math.Max(0, dt); }
            double ew = Math.Abs(dw) / Math.Max(5, tw), et = Math.Abs(dt) / Math.Max(1, tt);
            err += (0.6 * ew + 0.4 * et) * s.length;
            errW += 0.6 * ew * s.length;
            errT += 0.4 * et * s.length;
            wsum += s.length;
            if (ew > worstW) { worstW = ew; widest = i; }
            if (et > worstT) { worstT = et; thickest = i; }
        }
        widthMore = errW >= errT;
        return err / Math.Max(1e-9, wsum);
    }

    /// <summary>How close a piece is to a target shape (0-1), with what is off in <paramref name="notes"/>.</summary>
    public static double ShapeScore(CraftRules rules, CraftPiece p, CraftShapeTarget target, List<string> notes = null)
    {
        double length = p.Length;
        double score = 1;
        double lenScore = LengthScore(length, target.lengthMin, target.lengthMax);
        if (lenScore < LengthNoted) notes?.Add(length < target.lengthMin ? $"too short ({length:0} mm, wants {target.lengthMin:0}-{target.lengthMax:0})" : $"too long ({length:0} mm, wants {target.lengthMin:0}-{target.lengthMax:0})");
        score *= Math.Pow(lenScore, 0.15);
        if (target.profile != null && target.profile.Count >= 2)
        {
            double err = ProfileError(p, target, false, out _, out double worstW, out int widest, out double worstT, out int thickest);
            double prof = ProfileFit(err);
            if (prof < ProfileNoted && notes != null)
            {
                if (worstW > 0.15) notes.Add($"the width is off along it (worst {worstW * 100:0}% at {p.PositionOf(widest) * 100:0}% of its length)");
                if (worstT > 0.15) notes.Add($"the thickness is off along it (worst {worstT * 100:0}% at {p.PositionOf(thickest) * 100:0}%)");
            }
            score *= Math.Pow(prof, 0.4);
        }
        if (target.edge != CraftEdgeKind.None)
        {
            double beveled = 0, total = 0;
            for (int i = 0; i < p.segments.Count; i++)
            {
                double t = p.PositionOf(i);
                if (t < target.edgeFrom || t > target.edgeTo) continue;
                MetalSegment s = p.segments[i];
                total += s.length;
                if (s.EdgeThickness < 0.6 * s.thickness) beveled += s.length;
            }
            double e = total > 0 ? beveled / total : 0;
            if (e < 0.9) notes?.Add($"the edge is not ground along all of it ({e * 100:0}%)");
            score *= Math.Pow(0.2 + 0.8 * e, 0.15);
        }
        if (target.eye)
        {
            double best = 0;
            for (int i = 0; i < p.segments.Count; i++)
            {
                MetalSegment s = p.segments[i];
                if (s.hole <= 0) continue;
                double where = 1 - Math.Min(1, Math.Abs(p.PositionOf(i) - target.eyeAt) / 0.25);
                double size = target.eyeDiameter > 0 ? Math.Exp(-Math.Pow((s.hole - target.eyeDiameter) / (target.eyeDiameter * 0.3), 2)) : 1;
                best = Math.Max(best, where * size);
            }
            if (best < 0.6) notes?.Add(best <= 0 ? "it has no eye" : "the eye is the wrong size or in the wrong place");
            score *= Math.Pow(0.15 + 0.85 * best, 0.1);
        }
        double bend = 0;
        foreach (MetalSegment s in p.segments) bend += s.bend;
        double curve = Math.Exp(-Math.Pow((bend - target.curvature) / 8, 2));
        if (curve < 0.85) notes?.Add(Math.Abs(target.curvature) < 1 ? $"it is bent ({Math.Abs(bend):0}°)" : $"its curve is off ({bend:0}°, wants {target.curvature:0}°)");
        score *= Math.Pow(curve, 0.1);
        if (target.dish > 0)
        {
            double dish = 0, l = 0;
            foreach (MetalSegment s in p.segments) { dish += s.dish * s.length; l += s.length; }
            dish /= Math.Max(1e-9, l);
            double ds = Math.Exp(-Math.Pow((dish - target.dish) / (0.3 * target.dish + 1), 2));
            if (ds < 0.85) notes?.Add(dish < target.dish ? $"not dished enough ({dish:0} mm, wants {target.dish:0})" : $"dished too deep ({dish:0} mm)");
            score *= Math.Pow(ds, 0.1);
        }
        double mass = p.Mass(rules);
        double ms = mass >= target.massMin && mass <= target.massMax ? 1 : Math.Exp(-Math.Pow((mass < target.massMin ? target.massMin - mass : mass - target.massMax) / Math.Max(1, target.massMin) / 0.15, 2));
        if (ms < 0.9) notes?.Add(mass < target.massMin ? $"too light ({CraftText.Mass(mass)})" : $"too heavy ({CraftText.Mass(mass)})");
        score *= Math.Pow(ms, 0.1);
        return CraftMath.Clamp01(score);
    }

    private static void Wood(CraftRules rules, CraftPiece p, CraftPieceReport r)
    {
        Geometry(p, r);
        CraftWoodDefinition w = rules.Wood(p.wood.wood);
        r.MaterialName = w?.Name ?? p.wood.wood;
        // Fibres that run out of the piece break along themselves; drying checks are cracks waiting to open.
        r.WoodToughness = (w?.toughness ?? 0.5) * (1 - p.wood.split) * (1 - 0.6 * p.wood.charred) * (1 - 0.55 * CraftMath.Clamp01(p.wood.runout)) * (1 - 0.7 * CraftMath.Clamp01(p.wood.checks));
        r.WoodHardness = w?.hardness ?? 0.5;
        r.Split = p.wood.split;
        r.Charred = p.wood.charred;
        // Bark left on, rot under it and gouges from the drawknife weaken it too.
        double bark = CraftWoodwork.BarkLeft(p);
        r.Integrity = CraftMath.Clamp01((1 - p.wood.split) * (1 - 0.7 * p.wood.charred) * (1 - 0.6 * CraftMath.Clamp01(p.wood.checks))
                                        * (1 - 0.4 * bark) * (1 - 0.8 * CraftMath.Clamp01(p.wood.rot)) * (1 - 0.3 * CraftMath.Clamp01(p.wood.gouged)));
        r.WoodToughness *= (1 - 0.7 * CraftMath.Clamp01(p.wood.rot));
        if (bark > 0.1) r.ShapeNotes.Add("bark is still on it");
        if (p.wood.rot > 0.1) r.ShapeNotes.Add("rot has got into it under the bark");
        if (p.wood.gouged > 0.15) r.ShapeNotes.Add("gouged by the drawknife");
        r.WaterResistance = CraftWoodwork.FinishProtection(p.wood) * (0.85 + 0.15 * CraftWoodwork.FinishWear(p.wood) / Math.Max(0.05, CraftWoodwork.FinishProtection(p.wood) + 0.05));
        r.Moisture = p.wood.moisture;
        double rough = 0, l = 0;
        foreach (MetalSegment s in p.segments) { rough += s.rough * s.length; l += s.length; }
        r.Roughness = l > 0 ? rough / l : 0;
        CraftShapeTarget target = rules.Design(p.intentDesign)?.Component(p.intentRole)?.shape;
        if (target != null)
            r.Shape = ShapeScore(rules, p, target, r.ShapeNotes);
    }

    private static void Leather(CraftRules rules, CraftPiece p, CraftPieceReport r)
    {
        HideState h = p.hide;
        CraftHideDefinition def = rules.Hide(h.hide);
        CraftTanningAgent agent = rules.Agent(h.agent);
        bool cloth = def != null && def.textile;
        r.MaterialName = cloth ? def.Name : (def?.Name ?? "Hide") + (agent != null && agent.kind != CraftAgentKind.Water && agent.kind != CraftAgentKind.Lime ? $", {agent.Name.ToLowerInvariant()}" : ", raw");
        double n = 0, thick = 0, tan = 0, core = double.MaxValue, stiff = 0, dmg = 0, oil = 0, wax = 0, shrink = 0, moist = 0, hard = 0, grain = 0, flesh = 0, rot = 0, slick = 0, hair = 0;
        double dyeMean = 0, dyeSq = 0;
        foreach (HideCell c in h.cells)
        {
            if (!c.present) continue;
            n++;
            hair += c.hair;
            thick += CraftLeather.CellThickness(c);
            tan += c.Tannage;
            core = Math.Min(core, c.tanC);
            stiff += c.stiff;
            dmg += c.damage;
            oil += c.oil;
            wax += c.wax;
            shrink += c.shrink;
            moist += c.moisture;
            hard += c.hard;
            grain += c.grain;
            flesh += c.flesh;
            rot += c.rot;
            dyeMean += c.dye;
            dyeSq += c.dye * c.dye;
            slick += c.smooth * c.grain;
        }
        r.Cells = (int)n;
        if (n <= 0)
            return;
        r.Thickness = thick / n;
        r.Tannage = tan / n;
        r.CoreTannage = core == double.MaxValue ? 0 : core;
        r.Stiffness = stiff / n;
        r.Damage = dmg / n;
        r.Shrink = shrink / n;
        r.Moisture = moist / n;
        r.Hard = hard / n;
        double supple = (def?.suppleness ?? 0.4) * 0.4 + (1 - r.Stiffness) * 0.6 + oil / n * 0.25 - Math.Max(0, r.Thickness - 3) * 0.05;
        r.Suppleness = CraftMath.Clamp01(supple);
        double fiber = def?.fiberStrength ?? 1;
        double tanned = CraftMath.Clamp01(r.Tannage * 1.15);
        r.Strength = fiber * (0.3 + 0.7 * tanned) * (1 - 0.7 * r.Damage) * (0.6 + 0.4 * grain / n) * (1 - 0.6 * rot / n) * (cloth ? 1 : agent?.durability ?? 0.6) * (1 - 0.5 * flesh / n);
        if (cloth)
        {
            // Cloth cut across its grain stretches out of shape and frays; a ragged cut frays too.
            if (!string.IsNullOrEmpty(h.pattern) && !CraftTextiles.OnGrain(h))
            {
                r.Strength *= 0.85;
                r.Defects.Add("cut across the grain");
            }
            r.Strength *= 0.75 + 0.25 * CraftMath.Clamp01(h.cutQuality);
        }
        // A slicked grain is sealed a little against water; leather that dried unstretched puckered, unevenly framed it is askew.
        r.Cloth = cloth;
        r.Slicked = cloth ? 0 : CraftMath.Clamp01(slick / n);
        r.Puckered = cloth ? 0 : p.Tag(CraftLeather.PuckeredTag);
        r.Distortion = cloth ? 0 : CraftMath.Clamp01(p.Tag(CraftLeather.DistortionTag));
        // Leather tanned with its hair still on: a hairy grain neither slicks, dyes nor finishes (workmanship).
        r.HairLeft = cloth ? 0 : CraftLeather.HairLeft(def, hair / n);
        r.WaterResistance = CraftMath.Clamp01((agent?.waterResistance ?? 0) * (agent?.fixation ?? 1) * tanned + 0.3 * oil / n + 0.5 * wax / n + 0.15 * r.Slicked);
        r.Fire = h.fireResistance;
        double variance = dyeSq / n - (dyeMean / n) * (dyeMean / n);
        r.DyeEvenness = dyeMean > 0.05 * n ? CraftMath.Clamp01(1 - Math.Sqrt(Math.Max(0, variance)) * 2.5) : 1;
        r.Integrity = Integrity(p, r) * (1 - 0.5 * r.Damage);
        if (!h.dried && r.Tannage > 0.3)
            r.Defects.Add("not dried yet");
        if (r.CoreTannage < 0.4 && r.Tannage > 0.2)
            r.Defects.Add("the core is not tanned through");
        if (r.Puckered > 0.02)
            r.Defects.Add("dried puckered: it was not stretched");
        if (r.Distortion > 0.05)
            r.Defects.Add("dried out of square");
        if (r.HairLeft > HairNoted)
            r.Defects.Add(HairWords(r.HairLeft));
    }

    /// <summary>A leather part is said to have its hair on (and the item notes it) once this much of its hair is left (<see cref="CraftPieceReport.HairLeft"/>).</summary>
    public const double HairNoted = 0.1;

    /// <summary>How much hair a leather part was left with, in words: "the hair was left on", "tufts of hair left on it".</summary>
    private static string HairWords(double hairLeft) => hairLeft > 0.5 ? "the hair was left on" : "tufts of hair were left on it";

    // ------------------------------------------------------------------ finished items
    /// <summary>The finished item an assembly makes (finished or not: it says what it would be).</summary>
    public static CraftItemReport Item(CraftRules rules, CraftWorkshop ws, CraftPiece assembly)
    {
        var parts = new Dictionary<string, List<CraftPiece>>(StringComparer.OrdinalIgnoreCase);
        CraftDesign d = rules.Design(assembly?.assembly.design);
        if (d == null)
            return new CraftItemReport { Name = "Nothing", Tier = "" };
        foreach (AssemblyPart a in assembly.assembly.parts)
        {
            CraftPiece p = ws.Piece(a.piece);
            if (p == null) continue;
            if (!parts.TryGetValue(a.role, out List<CraftPiece> list)) parts[a.role] = list = new List<CraftPiece>();
            list.Add(p);
        }
        return Item(rules, d, parts, assembly.assembly, assembly.defects);
    }

    /// <summary>The item made of <paramref name="parts"/> (role → pieces) joined as <paramref name="joints"/> says.</summary>
    public static CraftItemReport Item(CraftRules rules, CraftDesign d, Dictionary<string, List<CraftPiece>> parts, AssemblyState joints, List<CraftDefect> assemblyDefects = null)
    {
        var r = new CraftItemReport { Design = d, Category = d.weaponCategory, Scaling = d.scaling, DamageForm = d.damageForm };
        var reports = new Dictionary<string, List<CraftPieceReport>>(StringComparer.OrdinalIgnoreCase);
        double mass = 0, price = 0;
        foreach (KeyValuePair<string, List<CraftPiece>> kv in parts)
        {
            var list = new List<CraftPieceReport>();
            foreach (CraftPiece p in kv.Value)
            {
                CraftComponentSpec spec = d.Component(kv.Key);
                CraftPieceReport pr = Piece(rules, p);
                if (d.kind == CraftDesignKind.Jewelry && p.kind == CraftPieceKind.Metal)
                {
                    // a jeweller's part: judged by the hands' own work on it, not by a smith's target shape
                    pr.ShapeNotes.Clear();
                    pr.Shape = CraftJewelry.PartWorkmanship(p, JewelStone(parts), pr.ShapeNotes);
                }
                else if (spec != null && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Wood) && spec.shape?.profile != null && spec.shape.profile.Count >= 2)
                {
                    pr.ShapeNotes.Clear();
                    pr.Shape = ShapeScore(rules, p, spec.shape, pr.ShapeNotes);
                }
                list.Add(pr);
                mass += pr.Mass;
                price += CraftValue.Of(rules, p);
                r.CoarseGrained |= p.kind == CraftPieceKind.Metal && pr.Profile != null && pr.Profile.Steel && pr.Grain > CoarseGrain;
                r.Parts.Add($"{spec?.Name ?? kv.Key}: {pr.MaterialName}, {CraftText.Mass(pr.Mass)}");
                r.PartPieces.Add(new CraftItemPart { role = kv.Key, name = spec?.Name ?? kv.Key, piece = p, mass = pr.Mass, main = string.Equals(d.MainComponent?.role, kv.Key, StringComparison.OrdinalIgnoreCase) });
            }
            reports[kv.Key] = list;
        }
        // What went into it besides its parts weighs in it and is worth what it cost: the batting quilted into a gambeson,
        // the thread in its seams, the solder in a ring's joint (as many items as went in, of the supply that did).
        foreach (CraftComponentSpec c in d.components)
        {
            if (!c.supply) continue;
            CraftAssembly.SupplyIn(rules, joints, c, out double grams, out double worth);
            if (grams <= 0 && worth <= 0) continue;
            mass += grams;
            price += worth;
            r.PartPieces.Add(new CraftItemPart { role = c.role, name = c.Name, mass = grams });
        }
        r.Weight = (float)(mass / 1000);

        CraftComponentSpec main = d.MainComponent;
        List<CraftPieceReport> ml = null;
        CraftPieceReport m = main != null && reports.TryGetValue(main.role, out ml) && ml.Count > 0 ? ml[0] : null;
        // A main part of several pieces (three throwing knives) is rated by all of them: their mean, the weakest's toughness.
        CraftPieceReport mainParts = m != null && ml.Count > 1 ? Mean(ml) : m;
        r.MaterialWord = MaterialWord(rules, m);
        // a leather or cloth item is the colour of what it is cut from: the tan's, the cloth's own - the dye's where it took
        r.Color = m?.Profile != null ? m.Profile.Color : m != null && m.Kind == CraftPieceKind.Hide ? CraftLeather.Colour(rules, m.Piece.hide) : r.Color;
        r.Name = ItemName(rules, d, r.MaterialWord, m?.Piece);

        // Material, workmanship (shape and finish), heat treatment, per component, by importance; soundness by importance
        // too, but as a product (a weighted geometric mean): one broken part - a blade cracked through - is not made good by
        // sound fittings, it drags the whole item down.
        double wSum = 0, mat = 0, work = 0, integ = 0, heat = 0;
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.supply || !reports.TryGetValue(c.role, out var list)) continue;
            foreach (CraftPieceReport pr in list)
            {
                double w = c.importance;
                wSum += w;
                mat += w * MaterialScore(rules, c, pr);
                work += w * Workmanship(c, pr);
                LeatherNotes(c, pr, r.Notes);
                integ += w * Math.Log(Math.Max(1e-3, pr.Integrity));
                heat += w * HeatScore(c, pr, r.Notes);
                if (pr.Integrity < Unsound)
                    r.Notes.Add($"{c.Name}: {UnsoundWords(pr)}");
            }
        }
        // Parts that are not there: the item is incomplete (a design never finishes without them, but reports are asked of unfinished work).
        double missing = 0;
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.supply || c.optional) continue;
            int have = reports.TryGetValue(c.role, out var got) ? got.Count : 0;
            if (have < c.count)
            {
                missing += c.importance * (c.count - have);
                r.Notes.Add($"{c.Name}: missing");
            }
        }
        double complete = wSum + missing > 0 ? wSum / (wSum + missing) : 0;
        if (wSum > 0)
        {
            r.Material = mat / wSum;
            r.Workmanship = work / wSum;
            r.Integrity = Math.Exp(integ / wSum);
            r.HeatTreatment = heat / wSum;
        }
        // Heat treatment is the metal's (and a hide's tanning): a wooden grip or a gem takes no part in it.
        double treatedWeight = 0, treated = 0;
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.supply || !reports.TryGetValue(c.role, out var list)) continue;
            foreach (CraftPieceReport pr in list)
                if (pr.Kind == CraftPieceKind.Metal || pr.Kind == CraftPieceKind.Hide)
                {
                    treatedWeight += c.importance;
                    treated += c.importance * HeatScore(c, pr, null);
                }
        }
        r.HeatTreatment = treatedWeight > 0 ? treated / treatedWeight : 1;
        if (assemblyDefects != null)
        {
            var told = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CraftDefect df in assemblyDefects)
            {
                // a wedge's split is the haft's own (its wood and the joint read it): it is not counted again here
                bool haftSplit = df.kind == CraftDefectKind.Crack && df.cause != null && df.cause.Contains("split the haft");
                if (df.kind == CraftDefectKind.Crack && !haftSplit) r.Integrity *= Math.Exp(-df.severity);
                string note = DefectNote(df);
                told[note] = told.TryGetValue(note, out int n) ? n + 1 : 1;
            }
            foreach (KeyValuePair<string, int> kv in told)
                r.Notes.Add(kv.Value > 1 ? $"{kv.Key} ({kv.Value} times)" : kv.Key);
        }
        r.Integrity *= complete;
        r.Assembly = AssemblyScore(rules, d, joints, parts, r.Notes);

        if (d.kind == CraftDesignKind.Jewelry)
            CraftJewelry.Evaluate(rules, d, reports, joints, r);
        else if (d.IsWeaponLike)
            Weapon(rules, d, reports, mainParts, mass, r);
        else
            Armor(rules, d, reports, joints, mass, r);
        // Wooden parts: a finish keeps the weather out (it lasts longer); green wood shrinks loose as it dries.
        double woodFinish = 0, woodParts = 0, green = 0;
        foreach (KeyValuePair<string, List<CraftPieceReport>> kv in reports)
            foreach (CraftPieceReport pr in kv.Value)
                if (pr.Kind == CraftPieceKind.Wood)
                {
                    woodFinish += pr.WaterResistance;
                    green = Math.Max(green, pr.Moisture);
                    woodParts++;
                }
        if (woodParts > 0)
        {
            woodFinish /= woodParts;
            r.Durability = Math.Max(5, (int)Math.Round(r.Durability * (0.92 + 0.16 * woodFinish)));
            if (green > CraftWoodwork.DryEnough)
            {
                r.Integrity *= 0.85;
                r.Notes.Add("green wood: it will shrink loose as it dries");
            }
        }

        // Notes on what shows.
        if (m != null)
        {
            foreach (string s in mainParts.ShapeNotes) r.Notes.Add($"{main.Name}: {s}");
            if (m.Kind == CraftPieceKind.Metal && mainParts.Scale > 0.03) r.Notes.Add($"{main.Name}: still scaled");
            if (m.Kind == CraftPieceKind.Metal && mainParts.Roughness > 25) r.Notes.Add($"{main.Name}: rough surface (hammer marks, scratches)");
            if (m.Kind == CraftPieceKind.Metal && mainParts.Streaks > CraftSmelting.Streaky) r.Notes.Add($"{main.Name}: streaky metal, cast from a melt not stirred even");
            if (m.Kind == CraftPieceKind.Metal && m.Profile != null && m.Profile.Steel && mainParts.Grain > CoarseGrain) r.Notes.Add($"{main.Name}: a coarse, weak grain - held too hot too long, never normalised");
        }
        foreach (KeyValuePair<string, List<CraftPieceReport>> kv in reports)
            foreach (CraftPieceReport pr in kv.Value)
                if (pr.Kind == CraftPieceKind.Wood && pr.Split > 0.05)
                {
                    string split = $"{d.Component(kv.Key)?.Name ?? kv.Key}: split along the grain";
                    if (!r.Notes.Contains(split)) r.Notes.Add(split);
                }
        double quality = d.kind == CraftDesignKind.Jewelry
            ? (r.Material * 0.15 + r.Workmanship * 0.2 + r.Integrity * 0.15 + r.Protection * 0.25 + r.Assembly * 0.25) * (0.85 + 0.15 * (joints?.finish ?? 0))
            : d.IsWeaponLike
            // a wooden weapon has no edge and no temper: how well its wood suits it counts for both
            ? r.Material * 0.22 + r.Workmanship * 0.18 + r.Integrity * 0.2 + (r.Wooden ? r.Wood * 0.27 : r.Edge * 0.15 + r.HeatTreatment * 0.12) + r.Assembly * 0.13
            : r.Material * 0.2 + r.Workmanship * 0.18 + r.Integrity * 0.2 + r.Protection * 0.17 + r.HeatTreatment * 0.07 + r.Assembly * 0.18;
        quality *= FitFactor(d, reports, joints, r.Notes);
        quality *= SoundnessFactor(r.Integrity);
        r.Quality = CraftMath.Clamp01(quality * complete * complete);
        if (joints != null && joints.mastery > 0)
        {
            // the finishing touches of a practised hand: what is left to put right, a master sees and does
            CraftMasterySettings ms = rules.Settings?.mastery;
            r.Quality = CraftMastery.FinishingTouch(r.Quality, joints.mastery, ms);
            if (joints.mastery >= CraftMastery.RankStart(CraftRank.Expert, ms)) r.Notes.Add($"{(joints.mastery >= CraftMastery.RankStart(CraftRank.Master, ms) ? "a master's" : "an expert's")} finishing touches");
        }
        r.Tier = TierName(r.Quality);
        r.Price = (float)(d.price * (0.3 + 1.4 * r.Quality) + price);
        return r;
    }

    /// <summary>The stone set in a piece of jewellery (its first gem), or null.</summary>
    private static CraftPiece JewelStone(Dictionary<string, List<CraftPiece>> parts)
    {
        foreach (KeyValuePair<string, List<CraftPiece>> kv in parts)
            foreach (CraftPiece p in kv.Value)
                if (p != null && p.kind == CraftPieceKind.Gem) return p;
        return null;
    }

    /// <summary>
    /// How well it fits its wearer: clothes and rings made to measurements fit (as well as the measuring was); made
    /// without measuring, a garment fits like any other and a ring is a guess. Only a ring is sized to a finger: a
    /// brooch's pin, a circlet or a cuff fits as it is.
    /// </summary>
    private static double FitFactor(CraftDesign d, Dictionary<string, List<CraftPieceReport>> reports, AssemblyState a, List<string> notes)
    {
        // a garment (the measure's own rule: what takes a chest's measure is what its fit counts for)
        bool cloth = CraftTextiles.MadeToMeasure(d), ring = false;
        double band = 0;
        if (d.kind == CraftDesignKind.Jewelry && d.armorSlot == ArmorSlotType.Ring)
            foreach (KeyValuePair<string, List<CraftPieceReport>> kv in reports)
                foreach (CraftPieceReport pr in kv.Value)
                    if (pr.Kind == CraftPieceKind.Metal && CraftJewelry.PartOf(pr.Piece) == CraftJewelPart.Band)
                    {
                        ring = true;
                        band = pr.Piece.Tag(CraftJewelry.SizeTag);
                    }
        if (!cloth && !ring)
            return 1;
        if (a == null || !a.measured)
        {
            notes.Add(ring ? "sized by guess: it may not fit" : "made without measuring the wearer: a loose, common fit");
            return CraftTextiles.FitOf(false, 0);
        }
        double f = CraftTextiles.FitOf(true, a.fit);
        if (ring && a.fitSize > 0 && band > 0)
        {
            double off = Math.Abs(band - a.fitSize);
            if (off > 0.4) notes.Add(off > 1.2 ? "the band is the wrong size for the finger" : "the band is a little off the size measured");
            f *= Math.Max(0.75, 1 - Math.Max(0, off - 0.25) * 0.12);
        }
        else if (a.fit > CraftTextiles.FitCareful)
            notes.Add("made to measure");
        return f;
    }

    /// <summary>
    /// A part less sound than this (its <see cref="CraftPieceReport.Integrity"/>: cracked through, a casting more hole than
    /// metal) is what breaks: the item's notes say so, and an item this unsound is rated by it (<see cref="SoundnessFactor"/>).
    /// </summary>
    public const double Unsound = 0.5;

    /// <summary>
    /// What an item's soundness (<see cref="CraftItemReport.Integrity"/>) leaves of its quality: all of it while it is at
    /// least <see cref="Unsound"/> (its flaws count already in the quality's soundness share), less and less below it - a
    /// quarter at nothing: a dagger whose blade is cracked through rates poor, however fine its guard and grip.
    /// </summary>
    public static double SoundnessFactor(double integrity) => Math.Min(1, 0.25 + 1.5 * CraftMath.Clamp01(integrity));

    /// <summary>Why a part is unsound (<see cref="Unsound"/>), in the item's notes: a metal part's cracks, its pores, its slag, or its flaws; wood or leather weakened.</summary>
    private static string UnsoundWords(CraftPieceReport pr)
    {
        bool cracked = false;
        if (pr.Piece != null)
            foreach (CraftDefect df in pr.Piece.defects) cracked |= df.kind == CraftDefectKind.Crack;
        if (pr.Kind == CraftPieceKind.Wood) return "weakened - it will break in use"; // (a split is noted as such)
        if (pr.Kind == CraftPieceKind.Hide) return "weakened - it will tear in use";
        return cracked ? "cracked - it will break in use"
            : pr.Porosity > 0.1 ? "full of pores - it will break in use"
            : pr.SlagShare > 0.05 ? "full of slag - it will break in use"
            : "unsound - it will break in use";
    }

    public static string TierName(double q) =>
        q < 0.2 ? "Crude" : q < 0.4 ? "Poor" : q < 0.6 ? "Serviceable" : q < 0.75 ? "Good" : q < 0.88 ? "Fine" : q < 0.95 ? "Superior" : "Masterwork";

    private static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    /// <summary>
    /// A short word for what an item is made of ("Steel", "Bronze", "Starmetal", "Cow leather", "Hardened cow leather",
    /// "Snakeskin"; cloth by its cloth: "Linen", "Wool", "Canvas").
    /// </summary>
    public static string MaterialWord(CraftRules rules, CraftPieceReport m)
    {
        if (m == null)
            return "";
        if (m.Kind == CraftPieceKind.Hide)
        {
            CraftHideDefinition def = rules.Hide(m.Piece.hide.hide);
            if (def != null && def.textile)
                return ClothWord(def);
            string animal = (def?.Name ?? "Leather").Replace(" hide", "").Replace(" pelt", "");
            string leather = animal.EndsWith("skin", StringComparison.OrdinalIgnoreCase) || animal == "Leather" ? animal : animal + " leather";
            return m.Hard > 0.5 ? "Hardened " + leather.ToLowerInvariant() : leather;
        }
        if (m.Kind == CraftPieceKind.Wood)
            return m.MaterialName;
        CraftMaterialClass c = m.Profile?.Class;
        if (c == null)
            return m.Profile?.BaseElement?.Name ?? "";
        switch (c.id)
        {
            case "mild_steel": case "medium_steel": case "high_carbon_steel": case "ultra_high_carbon_steel": case "nickel_steel": return "Steel";
            case "wrought_iron": case "phosphoric_iron": return "Iron";
            case "starmetal_steel": return "Starmetal";
            case "mythril_steel": return "Mythril-steel";
            case "copper_metal": return "Copper";
            default: return c.Name;
        }
    }

    /// <summary>A cloth's word ("Linen cloth" → "Linen", "Canvas").</summary>
    private static string ClothWord(CraftHideDefinition def)
    {
        string n = def?.Name ?? "Cloth";
        return n.EndsWith(" cloth", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 6) : n;
    }

    /// <summary>
    /// An item's name from its design and what its main part is made of (<paramref name="word"/>, <see cref="MaterialWord"/>):
    /// "Steel arming sword", "Cow leather cap" and "Studded cow leather armour" (the animal before the design's own
    /// "leather"; "Hardened" in front), "Linen cloak" (the cloth in place of the design's own), "Wool hood", "Yew longbow".
    /// No word: the design's name.
    /// </summary>
    public static string ItemName(CraftRules rules, CraftDesign d, string word, CraftPiece main)
    {
        if (d == null)
            return word ?? "";
        string name = d.Name;
        if (string.IsNullOrEmpty(word))
            return name;
        if (main != null && main.kind == CraftPieceKind.Hide)
        {
            CraftHideDefinition def = rules?.Hide(main.hide.hide);
            if (def != null && def.textile)
            {
                // "Wool cloak" sewn of linen is a linen cloak
                int space = name.IndexOf(' ');
                string first = space > 0 ? name.Substring(0, space) : "";
                if (first.Length > 0 && rules != null)
                    foreach (CraftHideDefinition h in rules.Hides)
                        if (h.textile && string.Equals(ClothWord(h), first, StringComparison.OrdinalIgnoreCase))
                            return word + name.Substring(space);
                return $"{word} {LowerFirst(name)}";
            }
            if (name.IndexOf("cuir", StringComparison.OrdinalIgnoreCase) >= 0)
                return name; // boiled leather is its own name
            bool hardened = word.StartsWith("Hardened ", StringComparison.Ordinal);
            string leather = (hardened ? word.Substring(9) : word).ToLowerInvariant();
            int at = name.IndexOf("leather", StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                string named = name.Substring(0, at) + leather + name.Substring(at + 7);
                return hardened ? "Hardened " + LowerFirst(named) : CraftText.Capital(named);
            }
            return $"{word} {LowerFirst(name)}";
        }
        return $"{word} {LowerFirst(name)}";
    }

    /// <summary>
    /// The item's name as a player who knows what <paramref name="known"/> holds would call it: its metal named only when
    /// they know it (else the design's name alone); a jewel's stone by the name they gave it, by sight if they know it,
    /// else by what it looks like. The report's own <see cref="CraftItemReport.Name"/> is the true one.
    /// </summary>
    public static string KnownName(CraftRules rules, CraftItemReport r, CraftJournalData known)
    {
        if (r?.Design == null)
            return r?.Name ?? "";
        CraftPiece main = null, stone = null;
        foreach (CraftItemPart p in r.PartPieces)
        {
            if (p.main && main == null) main = p.piece;
            if (p.piece != null && p.piece.kind == CraftPieceKind.Gem && stone == null) stone = p.piece;
        }
        string word = r.MaterialWord;
        if (main != null && (main.kind == CraftPieceKind.Metal || main.kind == CraftPieceKind.Fasteners))
        {
            CraftAlloyProfile prof = main.Profile(rules);
            if (prof.Class != null && !CraftJournal.KnowsClass(known, rules, prof.Class))
                word = "";
        }
        string name = ItemName(rules, r.Design, word, main);
        if (r.Design.kind == CraftDesignKind.Jewelry && stone != null)
        {
            string s = CraftLook.StoneName(rules, stone, known).ToLowerInvariant();
            name += $" with {CraftText.A(s)} {s}";
        }
        return name;
    }

    /// <summary>The item's parts as a player who knows what <paramref name="known"/> holds would name them ("Blade of dull grey metal, 1.1 kg").</summary>
    public static List<string> KnownParts(CraftRules rules, CraftItemReport r, CraftJournalData known)
    {
        var lines = new List<string>();
        if (r == null)
            return lines;
        foreach (CraftItemPart p in r.PartPieces)
            if (p.piece != null) lines.Add($"{CraftLook.Name(rules, p.piece, known)}, {CraftText.Mass(p.mass)}");
            else if (p.mass >= 1) lines.Add($"{p.name}, {CraftText.Mass(p.mass)}"); // what went in besides its parts: its padding, its thread
        return lines;
    }

    /// <summary>
    /// What trying a finished item tells (the final inspection): its qualities in words, then every note on it - its
    /// joints, its defects, its edge, its fit, its wood - once each.
    /// </summary>
    public static List<string> InspectionLines(CraftItemReport r)
    {
        var lines = new List<string>();
        if (r == null)
            return lines;
        string Word(double v) => v > 0.88 ? "excellent" : v > 0.75 ? "very good" : v > 0.6 ? "good" : v > 0.4 ? "fair" : v > 0.2 ? "poor" : "bad";
        lines.Add($"Material: {Word(r.Material)}; workmanship: {Word(r.Workmanship)}; soundness: {Word(r.Integrity)}.");
        // a blunt head (a mace's, a war hammer's) has no edge: what it strikes with is rated in its stead
        bool edged = r.Design?.MainComponent?.shape != null && r.Design.MainComponent.shape.edge != CraftEdgeKind.None;
        if (r.Design != null && r.Design.IsWeaponLike)
            lines.Add(r.Wooden ? $"Wood: {Word(r.Wood)}; assembly: {Word(r.Assembly)}."
                : $"{(edged ? "Edge" : "Striking face")}: {Word(r.Edge)}; heat treatment: {Word(r.HeatTreatment)}; assembly: {Word(r.Assembly)}.");
        else if (r.Design != null && r.Design.kind == CraftDesignKind.Jewelry)
            lines.Add($"Stone: {Word(r.Protection)}; setting and solder: {Word(r.Assembly)}.");
        else
            lines.Add($"Protection: {Word(r.Protection)}; assembly: {Word(r.Assembly)}.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string n in r.Notes)
        {
            if (string.IsNullOrWhiteSpace(n) || !seen.Add(n)) continue;
            string line = CraftText.Capital(n.Trim());
            lines.Add(line.EndsWith(".", StringComparison.Ordinal) || line.EndsWith("!", StringComparison.Ordinal) ? line : line + ".");
        }
        return lines;
    }

    /// <summary>A defect of the item in words: what caused it (already a phrase: "a rivet badly set"), or what it is.</summary>
    private static string DefectNote(CraftDefect df)
    {
        if (!string.IsNullOrWhiteSpace(df.cause))
            return df.cause;
        switch (df.kind)
        {
            case CraftDefectKind.LooseJoint: return "something on it is loose";
            case CraftDefectKind.CrackedRivet: return "a rivet head is split";
            case CraftDefectKind.Misaligned: return "holes that do not line up";
            case CraftDefectKind.Gap: return "a gap where the parts meet";
            case CraftDefectKind.Crack: return "a crack";
            default: return df.kind.ToString().ToLowerInvariant();
        }
    }

    /// <summary>
    /// The main part when it is several pieces (three throwing knives), as one: their mean (each one's mass, edge,
    /// hardness), the weakest one's toughness, every one's shape notes.
    /// </summary>
    private static CraftPieceReport Mean(List<CraftPieceReport> list)
    {
        CraftPieceReport first = list[0];
        var m = new CraftPieceReport { Piece = first.Piece, Kind = first.Kind, MaterialName = first.MaterialName, Profile = first.Profile, HasEdge = true, MinToughness = double.MaxValue };
        int n = list.Count, shaped = 0;
        double shape = 0;
        foreach (CraftPieceReport pr in list)
        {
            m.Mass += pr.Mass / n;
            m.Length += pr.Length / n;
            m.Width += pr.Width / n;
            m.Thickness += pr.Thickness / n;
            m.EdgeHardness += pr.EdgeHardness / n;
            m.BodyHardness += pr.BodyHardness / n;
            m.Toughness += pr.Toughness / n;
            m.MinToughness = Math.Min(m.MinToughness, pr.MinToughness);
            m.Hardened += pr.Hardened / n;
            m.Tempered += pr.Tempered / n;
            m.Stress += pr.Stress / n;
            m.Grain += pr.Grain / n;
            m.Sharpness += pr.Sharpness / n;
            m.EdgeAngle += pr.EdgeAngle / n;
            m.IdealEdgeAngle += pr.IdealEdgeAngle / n;
            m.Symmetry += pr.Symmetry / n;
            m.Chip += pr.Chip / n;
            m.Burr += pr.Burr / n;
            m.Roughness += pr.Roughness / n;
            m.Scale += pr.Scale / n;
            m.Polish += pr.Polish / n;
            m.Corrosion += pr.Corrosion / n;
            m.Streaks = Math.Max(m.Streaks, pr.Streaks);
            m.Integrity += pr.Integrity / n;
            m.WoodHardness += pr.WoodHardness / n;
            m.WoodToughness += pr.WoodToughness / n;
            m.WaterResistance += pr.WaterResistance / n;
            m.Moisture += pr.Moisture / n;
            m.Split = Math.Max(m.Split, pr.Split);
            m.HasEdge &= pr.HasEdge;
            if (pr.Shape >= 0) { shape += pr.Shape; shaped++; }
            foreach (string s in pr.ShapeNotes)
                if (!m.ShapeNotes.Contains(s)) m.ShapeNotes.Add(s);
        }
        m.Shape = shaped > 0 ? shape / shaped : -1;
        return m;
    }

    /// <summary>How good the material is for its part (0-1).</summary>
    private static double MaterialScore(CraftRules rules, CraftComponentSpec c, CraftPieceReport pr)
    {
        switch (pr.Kind)
        {
            case CraftPieceKind.Metal:
            {
                double toughness = CraftMath.Smooth(3, 35, pr.MinToughness);
                double hard = CraftMath.Smooth(80, c.function == CraftComponentFunction.Edge ? 650 : 400, c.function == CraftComponentFunction.Edge ? pr.EdgeHardness : pr.BodyHardness);
                double magic = pr.Profile != null ? 0.1 * pr.Profile.Magic + 0.1 * pr.Profile.Supernatural : 0;
                if (c.function == CraftComponentFunction.Guard || c.function == CraftComponentFunction.Counterweight)
                    return CraftMath.Clamp01(0.5 + 0.3 * toughness + 0.2 * hard);
                return CraftMath.Clamp01(0.45 * hard + 0.45 * toughness + 0.1 * pr.Corrosion + magic);
            }
            case CraftPieceKind.Hide:
            {
                CraftLeatherSpec l = c.leather;
                double thick = l != null ? (pr.Thickness >= l.thicknessMin && pr.Thickness <= l.thicknessMax ? 1 : Math.Exp(-Math.Pow((pr.Thickness < l.thicknessMin ? l.thicknessMin - pr.Thickness : pr.Thickness - l.thicknessMax) / 0.8, 2))) : 1;
                double supple = l != null ? (pr.Suppleness >= l.suppleMin && pr.Suppleness <= l.suppleMax ? 1 : Math.Exp(-Math.Pow((pr.Suppleness < l.suppleMin ? l.suppleMin - pr.Suppleness : pr.Suppleness - l.suppleMax) / 0.2, 2))) : 1;
                double tan = CraftMath.Smooth(0.2, 0.85, pr.Tannage) * (0.6 + 0.4 * CraftMath.Smooth(0.2, 0.7, pr.CoreTannage));
                return CraftMath.Clamp01((0.35 * tan + 0.25 * CraftMath.Clamp01(pr.Strength) + 0.2 * thick + 0.2 * supple) * (pr.Piece.hide.dried ? 1 : 0.7));
            }
            case CraftPieceKind.Wood:
            {
                double tough = c.wood != null ? CraftMath.Smooth(c.wood.toughnessMin - 0.2, c.wood.toughnessMin + 0.2, pr.WoodToughness) : pr.WoodToughness;
                return CraftMath.Clamp01(0.7 * tough + 0.3 * pr.WoodHardness);
            }
            case CraftPieceKind.Fasteners:
                return CraftMath.Clamp01(pr.FastenerQuality * (0.6 + 0.4 * CraftMath.Smooth(10, 40, pr.Ductility)));
            case CraftPieceKind.Gem:
                return pr.Gem != null ? CraftMath.Clamp01(0.5 * pr.Gem.Clarity + 0.5 * pr.Gem.Color) : 0.5;
            default:
                return 0.5;
        }
    }

    private static double Workmanship(CraftComponentSpec c, CraftPieceReport pr)
    {
        double shape = pr.Shape >= 0 ? pr.Shape : 0.8;
        switch (pr.Kind)
        {
            case CraftPieceKind.Metal:
            {
                double finish = CraftMath.Clamp01(1 - pr.Roughness / 60) * (1 - Math.Min(1, pr.Scale * 8));
                return CraftMath.Clamp01(shape * (0.8 + 0.2 * finish));
            }
            case CraftPieceKind.Hide:
                // the cut, the dye, the burnished edges; for leather also a slicked grain, how flat and square it dried, and
                // the hair taken off it (tanned with its hair on, it is half the work)
                return CraftMath.Clamp01(pr.Piece.hide.cutQuality * (0.8 + 0.2 * pr.DyeEvenness) * (0.9 + 0.1 * pr.Piece.hide.edgeFinish)
                                         * (pr.Cloth ? 1 : (0.96 + 0.04 * pr.Slicked) * (1 - 1.5 * Math.Min(0.2, pr.Puckered)) * (1 - 0.5 * pr.Distortion) * (1 - 0.5 * pr.HairLeft)));
            case CraftPieceKind.Wood:
                return CraftMath.Clamp01(shape * (0.8 + 0.2 * CraftMath.Clamp01(1 - pr.Roughness / 80)));
            case CraftPieceKind.Gem:
                return pr.Gem != null ? CraftMath.Clamp01((0.7 * pr.Gem.Cut + 0.3 * pr.Gem.Polish) * pr.Gem.Brilliance) : 0.5;
            default:
                return 0.8;
        }
    }

    /// <summary>
    /// What is off about a leather or cloth part for its place in the item, by its spec ("Sole: too thin (3.4 mm, wants
    /// 5-9)", "Glove: too stiff"), and how it dried - so the player can see why the item came out as it did.
    /// </summary>
    private static void LeatherNotes(CraftComponentSpec c, CraftPieceReport pr, List<string> notes)
    {
        if (pr.Kind != CraftPieceKind.Hide || pr.Cells <= 0)
            return;
        string name = c.Name;
        void Note(string text)
        {
            if (!notes.Contains(text)) notes.Add(text); // a pair of gloves is said once
        }
        CraftLeatherSpec l = c.leather;
        if (l != null)
        {
            string range = $"{l.thicknessMin:0.#}-{l.thicknessMax:0.#}";
            if (pr.Thickness < l.thicknessMin - 0.05) Note($"{name}: too thin ({pr.Thickness:0.0} mm, wants {range})");
            else if (pr.Thickness > l.thicknessMax + 0.05) Note($"{name}: too thick ({pr.Thickness:0.0} mm, wants {range})");
            if (pr.Suppleness < l.suppleMin - 0.02) Note($"{name}: too stiff for its use");
            else if (pr.Suppleness > l.suppleMax + 0.02) Note($"{name}: too soft for its use");
        }
        if (pr.Puckered > 0.02) Note($"{name}: dried puckered (it was not stretched)");
        if (pr.Distortion > 0.05) Note($"{name}: dried out of square");
        if (pr.HairLeft > HairNoted) Note($"{name}: {HairWords(pr.HairLeft)}");
        if (pr.DyeEvenness < PatchyDye) Note($"{name}: dyed patchily");
    }

    /// <summary>A dye job less even than this is patchy (the look says so, the item notes it; workmanship × (0.8 + 0.2 × evenness)).</summary>
    public const double PatchyDye = 0.7;

    /// <summary>Is it heat-treated as it should be: hardened if it should, and tempered (0-1)?</summary>
    private static double HeatScore(CraftComponentSpec c, CraftPieceReport pr, List<string> notes)
    {
        if (pr.Kind != CraftPieceKind.Metal || pr.Profile == null)
            return pr.Kind == CraftPieceKind.Hide ? CraftMath.Smooth(0.2, 0.9, pr.Tannage) : 0.8;
        bool wants = c.shape != null && c.shape.hardened;
        double grain = 1 / (1 + 0.3 * Math.Max(0, pr.Grain - 1.3));
        if (!pr.Profile.HardensByQuench)
        {
            // Bronze and the like: work-hardened edges are their heat treatment.
            return wants ? CraftMath.Clamp01(0.4 + 0.6 * CraftMath.Smooth(pr.Profile.Hardness, pr.Profile.Hardness * 1.8, pr.EdgeHardness)) * grain : 0.8 * grain;
        }
        if (!wants)
            return CraftMath.Clamp01(0.7 + 0.3 * (1 - pr.Hardened * (1 - pr.Tempered))) * grain;
        if (pr.Hardened < 0.3)
        {
            notes?.Add($"{c.Name}: it was never hardened");
            return 0.35 * grain;
        }
        if (pr.Tempered < 0.05)
        {
            notes?.Add($"{c.Name}: hardened but never tempered: brittle");
            return 0.45 * grain;
        }
        double temper = Math.Exp(-Math.Pow((pr.Tempered - 0.22) / 0.2, 2)); // a blade's temper: around 200-260 °C
        return CraftMath.Clamp01((0.5 + 0.5 * Math.Min(1, pr.Hardened / 0.8)) * (0.55 + 0.45 * temper) * grain * (1 - 0.4 * Math.Min(1, pr.Stress)));
    }

    // ------------------------------------------------------------------ joints
    /// <summary>
    /// How well a joint is made (0-1) as it stands: every stitch, rivet and turn it takes counts, a wedge or a peen as far
    /// as it is driven. <paramref name="soFar"/>: how well the work done on it so far is made, what is still to do left out
    /// (a quarter of a seam sewn well rates as well sewn, a wedge struck true as well struck however far it has gone, holes
    /// punched and nothing in them yet as well as they were punched) - the bench colours a joint in progress by it, and the
    /// bar's length shows how far it has come.
    /// </summary>
    public static double JointQuality(CraftRules rules, CraftJointSpec spec, JointState j, CraftDesign d, Dictionary<string, List<CraftPiece>> parts, AssemblyState a = null,
                                      bool soFar = false)
    {
        if (j == null)
            return 0;
        // the share of the joint's work done (so far: all of what was done)
        double Share(double done, double want) => soFar && done > 0 ? 1 : Math.Min(1, done / Math.Max(1, want));
        // only its holes are made so far: as true as they are (a rivet's sits in its hole however it is spaced)
        if (soFar && j.holes > 0 && j.stitches <= 0 && j.rivets <= 0
            && (spec.kind == CraftJointKind.Stitch || spec.kind == CraftJointKind.Rivet || spec.kind == CraftJointKind.Buckle))
        {
            bool riveted = spec.kind != CraftJointKind.Stitch && CraftAssembly.RivetPart(d, spec) != null;
            double holes = (riveted ? 1 : 1 - 0.5 * Math.Min(1, j.spacingError)) * (1 - 0.5 * Math.Min(1, j.alignError));
            return CraftMath.Clamp01(holes * (1 - j.damage));
        }
        double q;
        switch (spec.kind)
        {
            case CraftJointKind.Fit:
                // a slot closed by blows out of the good band is battered round the tang
                q = (j.gap < 0 ? 0 : Math.Exp(-j.gap / 0.8)) * CraftAssembly.StruckWell(j);
                break;
            case CraftJointKind.Peen:
                q = (soFar ? 1 : CraftMath.Smooth(0, 1, j.peen)) * (j.peen > CraftAssembly.PeenGoodHigh ? 0.8 : 1) * CraftAssembly.StruckWell(j);
                break;
            case CraftJointKind.Rivet:
                // each rivet's quality already holds its hole's alignment (CraftAssembly.Work): counted once
                q = j.rivetQuality * Share(j.rivets, spec.count);
                break;
            case CraftJointKind.Stitch:
            {
                double thread = 1;
                foreach (CraftComponentSpec c in d.components)
                    if (c.supply && c.supplyKind == CraftSupplyKind.Thread)
                    {
                        // The thread it was sewn with: spun fine or coarse, linen, wool or silk (unrecorded: ordinary thread).
                        double strength = a != null ? a.Supply(c.role + CraftWorkshop.StrengthSuffix) : 0;
                        thread = 0.9 * (strength > 0 ? CraftMath.Clamp(0.75 + 0.25 * strength, 0.8, 1.15) : 1);
                    }
                q = Share(j.stitches, spec.count) * thread * (1 - Math.Min(0.8, j.tensionSpread)) * (1 - Math.Min(0.6, Math.Abs(j.tension - CraftAssembly.StitchTensionIdeal)))
                    * (1 - 0.5 * Math.Min(1, j.spacingError)) * (1 - 0.5 * Math.Min(1, j.alignError)) * ThreadSuits(rules, d, spec, parts, a);
                break;
            }
            case CraftJointKind.Solder:
            case CraftJointKind.BezelSet:
            case CraftJointKind.ProngSet:
            {
                q = CraftJewelry.JointQuality(spec, j);
                if (spec.kind == CraftJointKind.Solder)
                {
                    // a harder, stronger solder makes a stronger seam (gold solder over silver's)
                    double strength = SupplyStrength(d, a, CraftSupplyKind.Solder);
                    q *= 0.85 + 0.15 * Math.Min(1.2, strength > 0 ? strength : 1);
                }
                if (spec.kind != CraftJointKind.Solder)
                {
                    // a wide seat never holds the stone as a snug one does
                    CraftPiece setting = null, stone = null;
                    foreach (string role in new[] { spec.roleA, spec.roleB })
                        if (role != null && parts.TryGetValue(role, out List<CraftPiece> list) && list.Count > 0)
                        {
                            if (list[0].kind == CraftPieceKind.Metal) setting = list[0];
                            if (list[0].kind == CraftPieceKind.Gem) stone = list[0];
                        }
                    if (setting != null && stone != null) q *= 0.6 + 0.4 * CraftJewelry.SeatFit(setting, stone);
                }
                break;
            }
            case CraftJointKind.Wrap:
                q = Share(j.turns, spec.count) * (1 - Math.Min(0.8, j.wrapSpread)) * (1 - Math.Min(0.6, Math.Abs(j.wrapTension - CraftAssembly.WrapTensionIdeal)))
                    * StringSuits(rules, d, spec, parts, a);
                break;
            case CraftJointKind.Wedge:
            case CraftJointKind.Socket:
            {
                // the play the wedge has not taken up (a socket's stays) rattles; blows out of the good band drive it crooked
                double play = CraftAssembly.PlayLeft(spec, j);
                double tight = play > CraftAssembly.PlayFree ? Math.Exp(-(play - CraftAssembly.PlayFree) / 1.5) : 1;
                q = (soFar ? 1 : CraftMath.Smooth(0, CraftAssembly.WedgeGoodLow, j.wedge)) * tight * CraftAssembly.StruckWell(j);
                // a socket's pins add to the drive's hold (as many of them as are set)
                if (spec.kind == CraftJointKind.Socket && j.rivets > 0) q = Math.Min(1, q + 0.3 * j.rivetQuality * Share(j.rivets, CraftAssembly.RivetsWanted(spec, d)));
                double split = 0;
                foreach (KeyValuePair<string, List<CraftPiece>> kv in parts)
                    foreach (CraftPiece p in kv.Value)
                        if (p.kind == CraftPieceKind.Wood) split = Math.Max(split, p.wood.split);
                q *= 1 - split;
                break;
            }
            case CraftJointKind.Buckle:
            {
                // each buckle riveted on, or sewn on with its stitches (pulled and spaced as any seam, with a thread that suits)
                int buckles = Math.Max(1, spec.count);
                if (j.rivets > 0)
                    q = j.rivetQuality * Share(j.rivets, buckles); // each rivet's quality holds its hole's alignment
                else if (j.stitches > 0)
                {
                    double strength = SupplyStrength(d, a, CraftSupplyKind.Thread);
                    double thread = 0.9 * (strength > 0 ? CraftMath.Clamp(0.75 + 0.25 * strength, 0.8, 1.15) : 1);
                    q = Share(j.stitches, CraftAssembly.StitchesPerBuckle * buckles) * thread * (1 - Math.Min(0.8, j.tensionSpread))
                        * (1 - Math.Min(0.6, Math.Abs(j.tension - CraftAssembly.StitchTensionIdeal))) * (1 - 0.5 * Math.Min(1, j.spacingError)) * ThreadSuits(rules, d, spec, parts, a);
                }
                else
                    q = 0;
                break;
            }
            default:
                q = 1;
                break;
        }
        return CraftMath.Clamp01(q * (1 - j.damage));
    }

    /// <summary>How strong the supply of a kind that went into it is (its recorded strength; 0 = not recorded).</summary>
    private static double SupplyStrength(CraftDesign d, AssemblyState a, CraftSupplyKind kind)
    {
        double s = 0;
        if (a != null)
            foreach (CraftComponentSpec c in d.components)
                if (c.supply && c.supplyKind == kind)
                    s = Math.Max(s, a.Supply(c.role + CraftWorkshop.StrengthSuffix));
        return s;
    }

    /// <summary>
    /// A wrap wound from a thread supply - a bowstring: as good as it was spun (the thread factor of a seam), and strong
    /// enough for the bow's draw - a thicker stave pulls harder (about 1 for a hunting bow's, 1.3 for a longbow's). A
    /// wrap of anything else (a leather grip wrap): 1.
    /// </summary>
    public static double StringSuits(CraftRules rules, CraftDesign d, CraftJointSpec spec, Dictionary<string, List<CraftPiece>> parts, AssemblyState a)
    {
        CraftComponentSpec cord = null;
        string stave = null;
        foreach (string role in new[] { spec.roleA, spec.roleB })
        {
            CraftComponentSpec c = string.IsNullOrEmpty(role) ? null : d.Component(role);
            if (c != null && c.supply && c.supplyKind == CraftSupplyKind.Thread) cord = c;
            else if (c != null) stave = role;
        }
        if (cord == null)
            return 1;
        double strength = a != null ? a.Supply(cord.role + CraftWorkshop.StrengthSuffix) : 0;
        if (strength <= 0)
            return 0.9; // a string of ordinary thread
        double need = BowStringNeed(d, stave, parts);
        double suits = strength < need ? Math.Exp(-(need - strength) / 0.45) : 1;
        return 0.9 * CraftMath.Clamp(0.75 + 0.25 * strength, 0.8, 1.15) * suits;
    }

    /// <summary>How strong a bowstring the stave wants: about 1 for a stave 30 mm thick, 0.075 more for every mm more.</summary>
    public static double BowStringNeed(CraftDesign d, string staveRole, Dictionary<string, List<CraftPiece>> parts)
    {
        double section = 30;
        if (staveRole != null && parts != null && parts.TryGetValue(staveRole, out List<CraftPiece> list) && list.Count > 0 && list[0].segments.Count > 0)
        {
            double w = 0, t = 0, l = 0;
            foreach (MetalSegment s in list[0].segments) { w += s.width * s.length; t += s.thickness * s.length; l += s.length; }
            if (l > 0) section = Math.Sqrt(Math.Max(1, w / l * (t / l)));
        }
        else if (staveRole != null && d.Component(staveRole)?.wood is CraftWoodSpec ws)
            section = (ws.diameterMin + ws.diameterMax) * 0.5;
        return Math.Max(0.6, 1 + (section - 30) * 0.075);
    }

    /// <summary>How well the thread suits the material it sewed (heavy leather wants strong thread; fine cloth puckers under coarse).</summary>
    private static double ThreadSuits(CraftRules rules, CraftDesign d, CraftJointSpec spec, Dictionary<string, List<CraftPiece>> parts, AssemblyState a) =>
        SeamThread(rules, d, spec, parts, a, out double strength, out double thick, out _) ? CraftTextiles.ThreadSuits(thick, strength) : 1;

    /// <summary>A seam's thread is called too weak (or too heavy) for what it sewed below this (<see cref="CraftTextiles.ThreadSuits"/>).</summary>
    public const double ThreadSuitsWell = 0.8;

    /// <summary>
    /// The thread a seam was sewn with (its recorded strength) and the thickest leather or cloth it went through
    /// (<paramref name="cloth"/>: that was cloth). False when either is not known (ordinary thread, nothing sewn).
    /// </summary>
    private static bool SeamThread(CraftRules rules, CraftDesign d, CraftJointSpec spec, Dictionary<string, List<CraftPiece>> parts, AssemblyState a,
                                   out double strength, out double thick, out bool cloth)
    {
        strength = thick = 0;
        cloth = false;
        foreach (CraftComponentSpec c in d.components)
            if (c.supply && c.supplyKind == CraftSupplyKind.Thread && a != null)
                strength = Math.Max(strength, a.Supply(c.role + CraftWorkshop.StrengthSuffix));
        if (strength <= 0)
            return false;
        foreach (string role in new[] { spec.roleA, spec.roleB })
            if (!string.IsNullOrEmpty(role) && parts.TryGetValue(role, out List<CraftPiece> list))
                foreach (CraftPiece p in list)
                {
                    if (p.kind != CraftPieceKind.Hide) continue;
                    double t = CraftLeather.MeanThickness(rules, p);
                    if (t <= thick) continue;
                    thick = t;
                    cloth = CraftTextiles.IsCloth(rules, p);
                }
        return thick > 0;
    }

    /// <summary>
    /// Why a sewn seam's thread does not suit what it sewed, in words ("Crown seam: sewn with too weak a thread for the
    /// cloth (0.6; it wants 0.92)") - null when it suits it (<see cref="ThreadSuitsWell"/>) or nothing is known of it.
    /// </summary>
    private static string ThreadNote(CraftRules rules, CraftDesign d, CraftJointSpec spec, JointState j, Dictionary<string, List<CraftPiece>> parts, AssemblyState a)
    {
        bool sewn = j != null && j.stitches > 0 && (spec.kind == CraftJointKind.Stitch || spec.kind == CraftJointKind.Buckle && j.rivets == 0);
        if (!sewn || !SeamThread(rules, d, spec, parts, a, out double strength, out double thick, out bool cloth))
            return null;
        if (CraftTextiles.ThreadSuits(thick, strength) >= ThreadSuitsWell)
            return null;
        double need = CraftTextiles.ThreadNeed(thick);
        string what = cloth ? "cloth" : "leather";
        return strength < need ? $"{spec.Name}: sewn with too weak a thread for the {what} ({strength:0.##}; it wants {need:0.##})"
            : $"{spec.Name}: sewn with too heavy a thread for the {what}: it puckers";
    }

    private static double AssemblyScore(CraftRules rules, CraftDesign d, AssemblyState a, Dictionary<string, List<CraftPiece>> parts, List<string> notes)
    {
        if (d.joints == null || d.joints.Count == 0)
            return 1;
        double sum = 0, w = 0;
        foreach (CraftJointSpec spec in d.joints)
        {
            JointState j = a?.Joint(spec.id);
            // A joint to an optional part left off (either side of it) is not made: it neither counts nor costs.
            bool LeftOff(string role) => !string.IsNullOrEmpty(role) && d.Component(role)?.optional == true && (j == null || !parts.ContainsKey(role));
            if (LeftOff(spec.roleA) || LeftOff(spec.roleB)) continue;
            double q = JointQuality(rules, spec, j, d, parts, a);
            if (q < 0.5) notes.Add($"{spec.Name}: weak ({q * 100:0}%)");
            // and why, where it is the thread (too weak for the leather or cloth it went through)
            string thread = ThreadNote(rules, d, spec, j, parts, a);
            if (thread != null) notes.Add(thread);
            if (spec.kind == CraftJointKind.Wrap && j != null && j.turns > 0 && StringSuits(rules, d, spec, parts, a) < 0.65)
                notes.Add("a weak string for the bow's draw: it will stretch and snap");
            sum += q * spec.importance;
            w += spec.importance;
        }
        return w > 0 ? sum / w : 1;
    }

    // ------------------------------------------------------------------ weapons
    private static void Weapon(CraftRules rules, CraftDesign d, Dictionary<string, List<CraftPieceReport>> reports, CraftPieceReport m, double mass, CraftItemReport r)
    {
        bool edged = d.MainComponent != null && d.MainComponent.shape != null && d.MainComponent.shape.edge != CraftEdgeKind.None;
        double hardF = 0.3, toughF = 0.5, edgeQ = 0.5, sym = 1, sharp = 0;
        if (m != null && m.Kind == CraftPieceKind.Metal)
        {
            hardF = 0.25 + 0.75 * CraftMath.Smooth(120, 600, m.EdgeHardness);
            toughF = CraftMath.Smooth(3, 30, m.MinToughness);
            if (edged)
            {
                // The design's angle as its section carries it along the edge (a narrow blade, a point: steeper).
                double ideal = m.IdealEdgeAngle > 0 ? m.IdealEdgeAngle : Math.Min(d.MainComponent.shape.edgeAngle, 2 * CraftSurface.MaxBevelAngle);
                sharp = m.Sharpness;
                // Near the design's angle is best; an obtuse edge cuts poorly (never worse than none), a very acute one is fragile.
                double diff = m.EdgeAngle - ideal;
                double angleF = m.EdgeAngle <= 0 || m.EdgeAngle >= 179 ? 0.1
                    : diff > 0 ? Math.Max(0.1, Math.Exp(-Math.Pow(diff / 25, 2))) : Math.Max(0.25, Math.Exp(-Math.Pow(diff / 15, 2)));
                sym = m.Symmetry;
                edgeQ = Math.Pow(Math.Max(0.01, sharp), 0.6) * Math.Pow(angleF, 0.4) * Math.Pow(Math.Max(0.05, sym), 0.3) * (1 - m.Chip) * (1 - 0.4 * m.Burr);
                if (m.EdgeHardness > 700 && m.MinToughness < 12) r.Notes.Add("a very hard, brittle edge: it will chip");
                if (m.EdgeHardness < 250) r.Notes.Add("a soft edge: it will dull and roll");
                int keen = CraftSurface.KeenStep(sharp); // the look's and the stone's own ladder
                if (keen == 0) r.Notes.Add("the edge is blunt");
                else if (keen == 1) r.Notes.Add("the edge cuts, but it is not keen");
                if (sym < 0.7) r.Notes.Add("the edge is uneven from side to side");
                if (m.EdgeAngle > ideal + 15) r.Notes.Add("the edge is thick and obtuse");
                if (m.EdgeAngle > 0 && m.EdgeAngle < ideal - 12) r.Notes.Add("the edge is very acute: keen but fragile");
            }
            else
                edgeQ = 0.6 + 0.4 * CraftMath.Smooth(150, 450, m.BodyHardness);
        }
        // A wooden weapon (a bow, a staff, a club): its wood's hardness and toughness, and how well the wood suits it - a
        // bow wants a springy, tough wood, a staff or a club a tough, hard one - and it must be dry. It has no edge.
        bool wooden = m != null && m.Kind == CraftPieceKind.Wood;
        bool bow = d.weaponCategory == WeaponCategory.Bow || d.weaponCategory == WeaponCategory.Crossbow;
        double flex = 0.4;
        if (wooden)
        {
            CraftWoodDefinition wd = rules.Wood(m.Piece.wood.wood);
            flex = wd?.flexibility ?? 0.4;
            hardF = 0.25 + 0.75 * CraftMath.Clamp01(m.WoodHardness);
            toughF = CraftMath.Smooth(0.15, 0.85, m.WoodToughness);
            double fit = bow ? 0.55 * CraftMath.Smooth(0.3, 0.9, flex) + 0.45 * toughF : 0.6 * toughF + 0.4 * CraftMath.Clamp01(m.WoodHardness);
            fit *= 1 - 0.6 * CraftMath.Smooth(0.15, 0.4, m.Moisture);
            r.Wooden = true;
            r.Wood = CraftMath.Clamp01(fit);
            if (bow && flex < 0.5) r.Notes.Add("a stiff wood for a bow: it will not cast an arrow far");
            if (!bow && toughF < 0.4) r.Notes.Add("a brittle wood for a weapon: it will crack on a hard blow");
            edgeQ = 0;
        }
        r.Edge = CraftMath.Clamp01(edgeQ);

        // Balance and speed.
        r.Balance = Balance(rules, d, reports);
        double balanceF = Math.Exp(-Math.Pow((r.Balance - d.idealBalance) / (0.5 * Math.Abs(d.idealBalance) + 40), 2));
        if (balanceF < 0.6 && !bow) r.Notes.Add(r.Balance > d.idealBalance ? "it is tip-heavy" : "its weight is all in the hand");
        double refMass = Math.Max(1, d.referenceMass);
        double working = m != null ? m.Mass : mass;
        double speed = Math.Pow(refMass / Math.Max(1, working), 0.35) * (0.85 + 0.15 * balanceF);
        // An overweight head is slower, but hits no harder than the heaviest the design allows (or half again its reference).
        double cap = Math.Min(d.MainComponent?.shape != null && d.MainComponent.shape.massMax > 0 ? d.MainComponent.shape.massMax : double.MaxValue, 1.5 * refMass);
        double hitting = Math.Min(Math.Max(1, working), cap);
        double massF = Math.Pow(hitting / refMass, 0.5), knock = Math.Pow(hitting / refMass, 0.7);
        if (bow)
        {
            // A bow's power is its draw - the stave's section and its spring - not its mass; nor is it slowed as a sword is.
            double section = Math.Sqrt(Math.Max(1, (m?.Width ?? 30) * (m?.Thickness ?? 30)));
            CraftWoodSpec spec = d.MainComponent?.wood;
            double reference = spec != null ? (spec.diameterMin + spec.diameterMax) * 0.5 : 30;
            double draw = CraftMath.Clamp(section / Math.Max(1, reference), 0.6, 1.4) * (0.7 + 0.3 * CraftMath.Smooth(0.3, 0.9, flex));
            massF = knock = draw;
            speed = 1.05 - 0.25 * (draw - 1);
            if (draw < 0.75) r.Notes.Add("a light draw: its arrows fly weakly");
        }
        r.AttackSpeed = (float)CraftMath.Clamp(speed, 0.6, 1.4);

        // Damage: the mass behind the blow (a bow's draw), the edge, the hardness; a loose head wastes it.
        double cut = edged ? 0.45 + 0.55 * r.Edge * Math.Sqrt(hardF) : 0.8 + 0.2 * hardF;
        double security = 0.75 + 0.25 * r.Assembly;
        double dmg = massF * cut * security * (0.85 + 0.15 * r.Workmanship) * (wooden ? 0.85 + 0.15 * r.Wood : 1);
        r.DamageMin = (float)Math.Max(1, d.damageMin * dmg);
        r.DamageMax = (float)Math.Max(r.DamageMin, d.damageMax * dmg);
        r.CritChance = (float)CraftMath.Clamp(d.critChance * (edged ? 0.6 + 0.8 * sharp * sym : 0.8 + 0.2 * hardF), 0, 0.5);
        r.CritMultiplier = Math.Max(1f, d.critMultiplier);
        r.Knockback = (float)(d.knockback * knock);
        r.ToolDamage = (float)(d.toolDamage * (0.5 + 0.5 * hardF) * (edged ? 0.6 + 0.4 * r.Edge : 1) * security);

        // Durability: toughness against breaking, hardness against wear, integrity, joints, rust (rot for wood: its finish).
        double brittle = m != null && m.EdgeHardness > 650 && m.MinToughness < 10 ? 0.6 : 1;
        double treated = 1;
        if (m != null && m.Profile != null && m.Profile.HardensByQuench && d.MainComponent.shape.hardened)
        {
            if (m.Hardened < 0.3) treated = 0.75;
            else if (m.Tempered < 0.05) treated = 0.5;
        }
        double corrosion = wooden ? 0.4 + 0.6 * CraftMath.Clamp01(m.WaterResistance) : m != null ? m.Corrosion : 0.4;
        double durability = d.durability * Math.Pow(Math.Max(0.05, toughF), 0.7) * (0.5 + 0.5 * hardF) * r.Integrity * (0.6 + 0.4 * r.Assembly) * (0.8 + 0.2 * corrosion) * brittle * treated;
        r.Durability = Math.Max(5, (int)Math.Round(durability));

        // Elements and magic: from the metal, and from what it was quenched in.
        ElementType element = ElementType.None;
        double strength = 0;
        if (m?.Profile != null && m.Profile.Affinity != ElementType.None)
        {
            element = m.Profile.Affinity;
            strength = m.Profile.AffinityStrength;
        }
        if (m?.Piece != null)
            foreach (CraftVariable v in m.Piece.tags)
                if (v.name.StartsWith("affinity.", StringComparison.Ordinal) && v.value > strength && Enum.TryParse(v.name.Substring(9), out ElementType e))
                {
                    element = e;
                    strength = v.value;
                }
        if (strength > 0.05)
        {
            r.Element = element;
            r.ElementBuildup = (float)(0.5 + strength);
        }
        if (m?.Profile != null && m.Profile.Magic > 0.6)
            r.Scaling = WeaponScaling.Intelligence;
        r.Protection = 0;
    }

    /// <summary>
    /// Where the weapon balances: mm in front of the guard (hilted), or from the hand on a haft (the hand a sixth of the
    /// way up from the butt).
    /// </summary>
    private static double Balance(CraftRules rules, CraftDesign d, Dictionary<string, List<CraftPieceReport>> reports)
    {
        double moment = 0, mass = 0;
        CraftPieceReport Get(string role) => reports.TryGetValue(role, out var l) && l.Count > 0 ? l[0] : null;
        CraftPieceReport blade = Get("blade");
        if (blade != null)
        {
            CraftShapeTarget s = d.Component("blade")?.shape;
            double tang = blade.Length * (s?.tangTo ?? 0.15);
            // The blade's own mass distribution, from the guard.
            CraftPiece p = blade.Piece;
            double pos = -tang;
            for (int i = 0; i < p.segments.Count; i++)
            {
                double m = p.SegmentMass(rules, i);
                moment += m * (pos + p.segments[i].length * 0.5);
                mass += m;
                pos += p.segments[i].length;
            }
            CraftPieceReport grip = Get("grip"), pommel = Get("pommel"), guard = Get("guard");
            double gl = grip?.Length ?? tang;
            if (guard != null) { mass += guard.Mass; }
            if (grip != null) { moment += grip.Mass * -gl * 0.5; mass += grip.Mass; }
            if (pommel != null) { moment += pommel.Mass * -(gl + pommel.Length * 0.5); mass += pommel.Mass; }
            return mass > 0 ? moment / mass : 0;
        }
        CraftPieceReport head = Get("head"), haft = Get("haft");
        if (head != null && haft != null)
        {
            double hand = haft.Length / 6;
            moment += haft.Mass * (haft.Length * 0.5 - hand) + head.Mass * (haft.Length - hand);
            mass += haft.Mass + head.Mass;
            return mass > 0 ? moment / mass : 0;
        }
        return d.idealBalance;
    }

    // ------------------------------------------------------------------ armour
    private static void Armor(CraftRules rules, CraftDesign d, Dictionary<string, List<CraftPieceReport>> reports, AssemblyState a, double mass, CraftItemReport r)
    {
        double protect = 0, reference = 0, magic = 0;
        var resist = new Dictionary<ElementType, double>();
        bool roughEdges = false;
        bool plated = d.components.Exists(c => c.function == CraftComponentFunction.Protection || c.function == CraftComponentFunction.Backing);
        foreach (CraftComponentSpec c in d.components)
        {
            int count = Math.Max(1, c.count);
            // What guards is weighed against what the design's Defense is for, part for part (Guards); the studs set in it
            // against the studs it takes. Everything else - a jerkin's shoulder straps, a shield's enarmes - only holds it on.
            bool guards = Guards(c, plated), studs = c.kind == CraftPieceKind.Fasteners && c.fastener == CraftFastenerKind.Stud;
            if (guards)
                reference += Reference(c) * count * (c.function == CraftComponentFunction.Backing ? 0.4 : 1);
            if (c.supply)
            {
                if (c.supplyKind == CraftSupplyKind.Padding)
                {
                    // the padding laid in, as good as it is (felt over linen); added before its kind was kept: the first padding's
                    double pad = a != null ? a.Supply(c.role) : 0;
                    double strength = a != null ? a.Supply(c.role + CraftWorkshop.StrengthSuffix) : 0;
                    if (strength <= 0) strength = rules.FirstSupply(CraftSupplyKind.Padding)?.strength ?? 1;
                    protect += Math.Min(pad, c.supplyCount) * 0.6 * strength;
                    reference += c.supplyCount * 0.6;
                }
                continue;
            }
            if (!reports.TryGetValue(c.role, out var list)) continue;
            foreach (CraftPieceReport pr in list)
            {
                double v = 0;
                switch (pr.Kind)
                {
                    case CraftPieceKind.Metal:
                    {
                        double hard = 0.35 + 0.65 * CraftMath.Smooth(80, 450, pr.BodyHardness);
                        double tough = CraftMath.Smooth(3, 25, pr.MinToughness);
                        double cover = pr.Shape >= 0 ? 0.5 + 0.5 * pr.Shape : 0.8;
                        v = pr.Thickness * hard * (0.4 + 0.6 * tough) * cover * pr.Integrity;
                        if (pr.Profile != null)
                        {
                            magic += pr.Profile.Magic * 4;
                            if (pr.Profile.Affinity != ElementType.None && pr.Profile.AffinityStrength > 0.05)
                                Add(resist, pr.Profile.Affinity, pr.Profile.AffinityStrength * 25);
                            if (pr.Profile.Supernatural > 0.2)
                                Add(resist, ElementType.Dark, pr.Profile.Supernatural * 15);
                        }
                        if (pr.Roughness > 30 || pr.Scale > 0.02) roughEdges = true;
                        break;
                    }
                    case CraftPieceKind.Hide:
                    {
                        v = pr.Thickness * (0.25 + 0.35 * pr.Stiffness + 0.4 * pr.Hard) * CraftMath.Smooth(0.1, 0.8, pr.Tannage) * CraftMath.Clamp01(pr.Strength) * pr.Integrity;
                        if (c.function == CraftComponentFunction.Backing) v *= 0.4;
                        CraftHideDefinition h = rules.Hide(pr.Piece.hide.hide);
                        if (h != null && h.affinity != ElementType.None)
                            Add(resist, h.affinity, h.affinityStrength * 40 * (c.function == CraftComponentFunction.Protection ? 1 : 0.4));
                        if (pr.Fire > 0.05)
                            Add(resist, ElementType.Fire, pr.Fire * 35);
                        break;
                    }
                    case CraftPieceKind.Wood:
                        v = pr.Thickness * 0.2 * (0.5 + pr.WoodToughness) * pr.Integrity * (pr.Shape >= 0 ? 0.5 + 0.5 * pr.Shape : 0.8);
                        break;
                    case CraftPieceKind.Fasteners:
                        if (pr.Piece.fasteners.kind == CraftFastenerKind.Stud)
                        {
                            // the studs set in it (not the ones still in the set), as well as they were set
                            int set = 0;
                            double how = 0;
                            if (a != null)
                                foreach (CraftJointSpec js in d.joints)
                                {
                                    if (js.kind != CraftJointKind.Rivet || CraftAssembly.RivetPart(d, js) != c) continue;
                                    JointState j = a.Joint(js.id);
                                    if (j == null) continue;
                                    set += j.rivets;
                                    how += j.rivetQuality * j.rivets;
                                }
                            int counted = Math.Min(Math.Max(1, c.fastenerCount), set);
                            v = counted * 0.12 * (0.4 + 0.6 * CraftMath.Smooth(60, 300, pr.BodyHardness)) * (set > 0 ? 0.5 + 0.5 * how / set : 0);
                        }
                        break;
                }
                // a strap's leather or a rivet set's metal still bears its resistances (above), but it guards nothing
                if (guards || studs)
                    protect += v;
            }
            if (studs)
                reference += c.fastenerCount * 0.12 * 0.8;
        }
        double ratio = reference > 1e-6 ? protect / reference : 0;
        r.Protection = CraftMath.Clamp01(ratio);
        r.Defense = (float)(d.defense * Math.Min(ratio, GuardsAtMost) * (0.7 + 0.3 * r.Assembly));
        r.MagicDefense = (float)(d.magicDefense + magic);
        foreach (KeyValuePair<ElementType, double> kv in resist)
            r.Resistances.Add(new ElementalResistanceValue(kv.Key, (float)Math.Min(60, kv.Value)));
        // Leather that sheds water lasts (one that soaks it up stiffens and rots); cloth was never tanned and has no tannage
        // to lose to the wet: a garment lasts as it was woven and sewn.
        double leatherWater = 1;
        foreach (KeyValuePair<string, List<CraftPieceReport>> kv in reports)
            foreach (CraftPieceReport pr in kv.Value)
                if (pr.Kind == CraftPieceKind.Hide && !pr.Cloth)
                    leatherWater = Math.Min(leatherWater, 0.7 + 0.3 * pr.WaterResistance);
        if (roughEdges && HasLeather(d))
            r.Notes.Add($"unfinished metal edges will chafe the {(d.components.Exists(c => c.kind == CraftPieceKind.Hide && c.leather?.fabric != CraftFabric.Cloth) ? "leather" : "cloth")}");
        double durability = d.durability * r.Integrity * (0.4 + 0.6 * r.Assembly) * leatherWater * (roughEdges && HasLeather(d) ? 0.85 : 1) * (0.7 + 0.3 * r.Material);
        r.Durability = Math.Max(5, (int)Math.Round(durability));
        r.Edge = 0;
    }

    private static bool HasLeather(CraftDesign d)
    {
        foreach (CraftComponentSpec c in d.components)
            if (c.kind == CraftPieceKind.Hide) return true;
        return false;
    }

    /// <summary>
    /// An armour guards as its design's Defense says as far as its parts guard like well-made ones (<see cref="Reference"/>)
    /// - more where they guard better (a hardened panel, a thicker plate, canvas where linen would do), but never more than
    /// this many times its design's, however thick or hard: a leather jerkin never guards like a breastplate.
    /// </summary>
    public const double GuardsAtMost = 1.5;

    /// <summary>
    /// Does a part of this spec guard the wearer - is what it gives weighed against what the design's Defense is for? Its
    /// plates and panels (<see cref="CraftComponentFunction.Protection"/>), the leather a plate sits on
    /// (<see cref="CraftComponentFunction.Backing"/>, at 0.4), and in an item of nothing but straps (a belt, a sash:
    /// <paramref name="plated"/> false) its straps. The straps and enarmes of anything else hold it on and guard nothing:
    /// a jerkin's thick shoulder straps do not make it a breastplate.
    /// </summary>
    private static bool Guards(CraftComponentSpec c, bool plated) =>
        c.function == CraftComponentFunction.Protection
        || c.function == CraftComponentFunction.Backing && c.kind == CraftPieceKind.Hide
        || !plated && c.function == CraftComponentFunction.Strap && c.kind == CraftPieceKind.Hide;

    /// <summary>
    /// The cloth a cloth part is weighed against (<see cref="Reference"/>): a sound plain weave as supple and as strong as
    /// wool cloth, the everyday garment cloth (linen, silk and canvas are stronger for their thickness, cotton and felt a
    /// little weaker). Leather's is tanned leather, middling stiff and strong - no cloth comes near that for its thickness.
    /// </summary>
    public const double ClothStiffness = 0.15, ClothStrength = 0.45;

    /// <summary>
    /// The protection a well-made part of this spec gives (what the design's Defense is for). A leather or cloth part is
    /// as thick as the middle of its spec's range: leather tanned through, middling stiff and strong; cloth (a spec cut
    /// from cloth) a sound plain weave (<see cref="ClothStiffness"/>, <see cref="ClothStrength"/>).
    /// </summary>
    private static double Reference(CraftComponentSpec c)
    {
        switch (c.kind)
        {
            case CraftPieceKind.Metal:
                if (c.supply || c.shape?.profile == null || c.shape.profile.Count == 0) return 0;
                double t = 0;
                foreach (CraftProfilePoint p in c.shape.profile) t += p.thickness;
                return t / c.shape.profile.Count * 0.9;
            case CraftPieceKind.Hide:
                double mid = c.leather != null ? (c.leather.thicknessMin + c.leather.thicknessMax) * 0.5 : 3;
                return c.leather != null && c.leather.fabric == CraftFabric.Cloth
                    ? mid * (0.25 + 0.35 * ClothStiffness) * ClothStrength * 0.9
                    : mid * (0.25 + 0.35 * 0.6) * 0.85 * 0.9;
            case CraftPieceKind.Wood:
                return (c.wood != null ? (c.wood.diameterMin + c.wood.diameterMax) * 0.5 : 14) * 0.2 * 1.1;
            default:
                return 0;
        }
    }

    private static void Add(Dictionary<ElementType, double> d, ElementType e, double v)
    {
        d.TryGetValue(e, out double x);
        d[e] = x + v;
    }
}
