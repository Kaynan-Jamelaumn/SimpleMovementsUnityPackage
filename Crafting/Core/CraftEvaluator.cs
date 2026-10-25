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
    public bool HasEdge;
    /// <summary>Surface: roughness (µm), scale (mm), polish 0-1, coating 0-1, corrosion resistance 0-1.</summary>
    public double Roughness, Scale, Polish, Coating, Corrosion;
    /// <summary>Total bend (degrees), mean dish (mm), the largest eye (mm), slag share, porosity.</summary>
    public double Bend, Dish, Eye, SlagShare, Porosity;
    /// <summary>0-1: what the defects leave of it.</summary>
    public double Integrity = 1;
    /// <summary>How close it is to its intended shape (0-1), and why not.</summary>
    public double Shape = -1;
    public readonly List<string> ShapeNotes = new List<string>();

    // Leather
    public double Tannage, CoreTannage, Suppleness, Strength, WaterResistance, Damage, Shrink, DyeEvenness = 1, Moisture, Stiffness, Hard, Fire;
    public int Cells;

    // Wood
    public double WoodToughness, WoodHardness, Split, Charred;

    // Fasteners
    public int Count;
    public double FastenerQuality;

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
        double sharp = 0, angle = 0, sym = 0, chip = 0, burr = 0, edgeT = 0, esum = 0;
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
                double a = Math.Abs((s.microL > 0 ? s.microL : s.AngleL) - (s.microR > 0 ? s.microR : s.AngleR));
                double depths = s.bevelDepthL + s.bevelDepthR;
                double off = depths > 1e-6 ? Math.Abs(s.bevelDepthL - s.bevelDepthR) / depths : 0;
                sym += CraftMath.Clamp01(1 - a / 12 - off * (p.edges == 2 ? 0.8 : 0.2) - Math.Abs(s.honeL - s.honeR) * 0.2) * w;
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
        return CraftMath.Clamp01(k);
    }

    /// <summary>How close a piece is to a target shape (0-1), with what is off in <paramref name="notes"/>.</summary>
    public static double ShapeScore(CraftRules rules, CraftPiece p, CraftShapeTarget target, List<string> notes = null)
    {
        double length = p.Length;
        double score = 1;
        double lenScore = length >= target.lengthMin && length <= target.lengthMax ? 1
            : Math.Exp(-Math.Pow((length < target.lengthMin ? target.lengthMin - length : length - target.lengthMax) / Math.Max(1, (target.lengthMin + target.lengthMax) * 0.5) / 0.08, 2));
        if (lenScore < 0.95) notes?.Add(length < target.lengthMin ? $"too short ({length:0} mm, wants {target.lengthMin:0}-{target.lengthMax:0})" : $"too long ({length:0} mm, wants {target.lengthMin:0}-{target.lengthMax:0})");
        score *= Math.Pow(lenScore, 0.15);
        if (target.profile != null && target.profile.Count >= 2)
        {
            double err = 0, wsum = 0;
            int widest = -1, thickest = -1;
            double worstW = 0, worstT = 0;
            for (int i = 0; i < p.segments.Count; i++)
            {
                MetalSegment s = p.segments[i];
                target.At((float)p.PositionOf(i), out float tw, out float tt);
                double ew = Math.Abs(s.width - tw) / Math.Max(5, tw), et = Math.Abs(s.thickness - tt) / Math.Max(1, tt);
                err += (0.6 * ew + 0.4 * et) * s.length;
                wsum += s.length;
                if (ew > worstW) { worstW = ew; widest = i; }
                if (et > worstT) { worstT = et; thickest = i; }
            }
            err /= Math.Max(1e-9, wsum);
            double prof = Math.Exp(-Math.Pow(err / 0.18, 2));
            if (prof < 0.9 && notes != null)
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
        r.WoodToughness = (w?.toughness ?? 0.5) * (1 - p.wood.split) * (1 - 0.6 * p.wood.charred);
        r.WoodHardness = w?.hardness ?? 0.5;
        r.Split = p.wood.split;
        r.Charred = p.wood.charred;
        r.Integrity = CraftMath.Clamp01((1 - p.wood.split) * (1 - 0.7 * p.wood.charred));
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
        r.MaterialName = (def?.Name ?? "Hide") + (agent != null && agent.kind != CraftAgentKind.Water && agent.kind != CraftAgentKind.Lime ? $", {agent.Name.ToLowerInvariant()}" : ", raw");
        double n = 0, thick = 0, tan = 0, core = double.MaxValue, stiff = 0, dmg = 0, oil = 0, wax = 0, shrink = 0, moist = 0, hard = 0, grain = 0, flesh = 0, rot = 0;
        double dyeMean = 0, dyeSq = 0;
        foreach (HideCell c in h.cells)
        {
            if (!c.present) continue;
            n++;
            thick += c.thickness * (1 - c.shrink * 0.3) * (1 + c.shrink * 0.5);
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
        r.Strength = fiber * (0.3 + 0.7 * tanned) * (1 - 0.7 * r.Damage) * (0.6 + 0.4 * grain / n) * (1 - 0.6 * rot / n) * (agent?.durability ?? 0.6) * (1 - 0.5 * flesh / n);
        r.WaterResistance = CraftMath.Clamp01((agent?.waterResistance ?? 0) * (agent?.fixation ?? 1) * tanned + 0.3 * oil / n + 0.5 * wax / n);
        r.Fire = h.fireResistance;
        double variance = dyeSq / n - (dyeMean / n) * (dyeMean / n);
        r.DyeEvenness = dyeMean > 0.05 * n ? CraftMath.Clamp01(1 - Math.Sqrt(Math.Max(0, variance)) * 2.5) : 1;
        r.Integrity = Integrity(p, r) * (1 - 0.5 * r.Damage);
        if (!h.dried && r.Tannage > 0.3)
            r.Defects.Add("not dried yet");
        if (r.CoreTannage < 0.4 && r.Tannage > 0.2)
            r.Defects.Add("the core is not tanned through");
    }

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
                if (spec != null && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Wood) && spec.shape?.profile != null && spec.shape.profile.Count >= 2)
                {
                    pr.ShapeNotes.Clear();
                    pr.Shape = ShapeScore(rules, p, spec.shape, pr.ShapeNotes);
                }
                list.Add(pr);
                mass += pr.Mass;
                price += Value(rules, p, pr);
                r.Parts.Add($"{spec?.Name ?? kv.Key}: {pr.MaterialName}, {CraftText.Mass(pr.Mass)}");
            }
            reports[kv.Key] = list;
        }
        r.Weight = (float)(mass / 1000);

        CraftComponentSpec main = d.MainComponent;
        CraftPieceReport m = main != null && reports.TryGetValue(main.role, out var ml) && ml.Count > 0 ? ml[0] : null;
        r.MaterialWord = MaterialWord(rules, m);
        r.Color = m?.Profile != null ? m.Profile.Color : m != null && m.Kind == CraftPieceKind.Hide ? (rules.Agent(m.Piece.hide.agent)?.color ?? new Color(0.6f, 0.45f, 0.3f)) : r.Color;
        r.Name = string.IsNullOrEmpty(r.MaterialWord) ? d.Name : $"{r.MaterialWord} {LowerFirst(d.Name)}";

        // Material, workmanship (shape and finish), integrity, heat treatment, per component, by importance.
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
                integ += w * pr.Integrity;
                heat += w * HeatScore(c, pr, r.Notes);
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
            r.Integrity = integ / wSum;
            r.HeatTreatment = heat / wSum;
        }
        if (assemblyDefects != null)
            foreach (CraftDefect df in assemblyDefects)
            {
                if (df.kind == CraftDefectKind.Crack) r.Integrity *= Math.Exp(-df.severity);
                r.Notes.Add($"{df.kind}: {df.cause}");
            }
        r.Integrity *= complete;
        r.Assembly = AssemblyScore(rules, d, joints, parts, r.Notes);

        if (d.IsWeaponLike)
            Weapon(rules, d, reports, m, mass, r);
        else
            Armor(rules, d, reports, joints, mass, r);

        // Notes on what shows.
        if (m != null)
        {
            foreach (string s in m.ShapeNotes) r.Notes.Add($"{main.Name}: {s}");
            if (m.Kind == CraftPieceKind.Metal && m.Scale > 0.03) r.Notes.Add($"{main.Name}: still scaled");
            if (m.Kind == CraftPieceKind.Metal && m.Roughness > 25) r.Notes.Add($"{main.Name}: rough surface (hammer marks, scratches)");
        }
        double quality = d.IsWeaponLike
            ? r.Material * 0.22 + r.Workmanship * 0.18 + r.Integrity * 0.2 + r.Edge * 0.15 + r.HeatTreatment * 0.12 + r.Assembly * 0.13
            : r.Material * 0.2 + r.Workmanship * 0.18 + r.Integrity * 0.2 + r.Protection * 0.17 + r.HeatTreatment * 0.07 + r.Assembly * 0.18;
        r.Quality = CraftMath.Clamp01(quality * complete * complete);
        r.Tier = TierName(r.Quality);
        r.Price = (float)(d.price * (0.3 + 1.4 * r.Quality) + price);
        return r;
    }

    public static string TierName(double q) =>
        q < 0.2 ? "Crude" : q < 0.4 ? "Poor" : q < 0.6 ? "Serviceable" : q < 0.75 ? "Good" : q < 0.88 ? "Fine" : q < 0.95 ? "Superior" : "Masterwork";

    private static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    /// <summary>A short word for what an item is made of ("Steel", "Bronze", "Starmetal", "Cow leather").</summary>
    public static string MaterialWord(CraftRules rules, CraftPieceReport m)
    {
        if (m == null)
            return "";
        if (m.Kind == CraftPieceKind.Hide)
        {
            string hide = rules.Hide(m.Piece.hide.hide)?.Name ?? "Leather";
            hide = hide.Replace(" hide", "").Replace(" pelt", "");
            return (m.Hard > 0.5 ? "Hardened " + hide.ToLowerInvariant() : hide) + " leather";
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

    private static double Value(CraftRules rules, CraftPiece p, CraftPieceReport pr)
    {
        switch (p.kind)
        {
            case CraftPieceKind.Metal:
            case CraftPieceKind.Fasteners:
                return pr.Mass * (pr.Profile?.PricePerGram ?? 0.004) * (pr.Profile?.Class?.priceMultiplier ?? 1);
            case CraftPieceKind.Hide:
                return (rules.Hide(p.hide.hide)?.price ?? 5) * pr.Cells / Math.Max(1, (rules.Hide(p.hide.hide)?.width ?? 10) * (rules.Hide(p.hide.hide)?.height ?? 10)) * 1.5;
            case CraftPieceKind.Wood:
                return (rules.Wood(p.wood.wood)?.price ?? 1) * pr.Length / 1000;
            default:
                return 0;
        }
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
                return CraftMath.Clamp01(pr.Piece.hide.cutQuality * (0.8 + 0.2 * pr.DyeEvenness) * (0.9 + 0.1 * pr.Piece.hide.edgeFinish));
            case CraftPieceKind.Wood:
                return CraftMath.Clamp01(shape * (0.8 + 0.2 * CraftMath.Clamp01(1 - pr.Roughness / 80)));
            default:
                return 0.8;
        }
    }

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
    public static double JointQuality(CraftRules rules, CraftJointSpec spec, JointState j, CraftDesign d, Dictionary<string, List<CraftPiece>> parts)
    {
        if (j == null)
            return 0;
        double q;
        switch (spec.kind)
        {
            case CraftJointKind.Fit:
                q = j.gap < 0 ? 0 : Math.Exp(-j.gap / 0.8);
                break;
            case CraftJointKind.Peen:
                q = CraftMath.Smooth(0, 1, j.peen) * (j.peen > 1.4 ? 0.8 : 1);
                break;
            case CraftJointKind.Rivet:
                q = j.rivetQuality * Math.Min(1, j.rivets / (double)Math.Max(1, spec.count)) * (1 - 0.5 * Math.Min(1, j.alignError));
                break;
            case CraftJointKind.Stitch:
            {
                double thread = 1;
                foreach (CraftComponentSpec c in d.components)
                    if (c.supply && c.supplyKind == CraftSupplyKind.Thread) thread = 0.9;
                q = Math.Min(1, j.stitches / (double)Math.Max(1, spec.count)) * thread * (1 - Math.Min(0.8, j.tensionSpread)) * (1 - Math.Min(0.6, Math.Abs(j.tension - 0.6)))
                    * (1 - 0.5 * Math.Min(1, j.spacingError)) * (1 - 0.5 * Math.Min(1, j.alignError));
                break;
            }
            case CraftJointKind.Wrap:
                q = Math.Min(1, j.turns / (double)Math.Max(1, spec.count)) * (1 - Math.Min(0.8, j.wrapSpread)) * (1 - Math.Min(0.6, Math.Abs(j.wrapTension - 0.65)));
                break;
            case CraftJointKind.Wedge:
            case CraftJointKind.Socket:
            {
                double tight = j.gap > 1 ? Math.Exp(-(j.gap - 1) / 1.5) : 1;
                q = CraftMath.Smooth(0, 0.6, j.wedge) * tight;
                if (spec.kind == CraftJointKind.Socket && j.rivets > 0) q = Math.Min(1, q + 0.3 * j.rivetQuality);
                double split = 0;
                foreach (KeyValuePair<string, List<CraftPiece>> kv in parts)
                    foreach (CraftPiece p in kv.Value)
                        if (p.kind == CraftPieceKind.Wood) split = Math.Max(split, p.wood.split);
                q *= 1 - split;
                break;
            }
            case CraftJointKind.Buckle:
                q = j.holes > 0 ? j.rivets > 0 ? j.rivetQuality : Math.Min(1, j.stitches / 4.0) * (1 - Math.Min(0.8, j.tensionSpread)) : 0;
                break;
            default:
                q = 1;
                break;
        }
        return CraftMath.Clamp01(q * (1 - j.damage));
    }

    private static double AssemblyScore(CraftRules rules, CraftDesign d, AssemblyState a, Dictionary<string, List<CraftPiece>> parts, List<string> notes)
    {
        if (d.joints == null || d.joints.Count == 0)
            return 1;
        double sum = 0, w = 0;
        foreach (CraftJointSpec spec in d.joints)
        {
            JointState j = a?.Joint(spec.id);
            bool optional = d.Component(spec.roleB)?.optional == true && (j == null || !parts.ContainsKey(spec.roleB));
            if (optional) continue;
            double q = JointQuality(rules, spec, j, d, parts);
            if (q < 0.5) notes.Add($"{spec.Name}: weak ({q * 100:0}%)");
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
                double ideal = d.MainComponent.shape.edgeAngle;
                sharp = m.Sharpness;
                // Near the design's angle is best; an obtuse edge cuts poorly (never worse than none), a very acute one is fragile.
                double diff = m.EdgeAngle - ideal;
                double angleF = m.EdgeAngle <= 0 || m.EdgeAngle >= 179 ? 0.1
                    : diff > 0 ? Math.Max(0.1, Math.Exp(-Math.Pow(diff / 25, 2))) : Math.Max(0.25, Math.Exp(-Math.Pow(diff / 15, 2)));
                sym = m.Symmetry;
                edgeQ = Math.Pow(Math.Max(0.01, sharp), 0.6) * Math.Pow(angleF, 0.4) * Math.Pow(Math.Max(0.05, sym), 0.3) * (1 - m.Chip) * (1 - 0.4 * m.Burr);
                if (m.EdgeHardness > 700 && m.MinToughness < 12) r.Notes.Add("a very hard, brittle edge: it will chip");
                if (m.EdgeHardness < 250) r.Notes.Add("a soft edge: it will dull and roll");
                if (sharp < 0.3) r.Notes.Add("the edge is dull");
                if (sym < 0.7) r.Notes.Add("the edge is uneven from side to side");
                if (m.EdgeAngle > ideal + 15) r.Notes.Add("the edge is thick and obtuse");
                if (m.EdgeAngle > 0 && m.EdgeAngle < ideal - 12) r.Notes.Add("the edge is very acute: keen but fragile");
            }
            else
                edgeQ = 0.6 + 0.4 * CraftMath.Smooth(150, 450, m.BodyHardness);
        }
        r.Edge = CraftMath.Clamp01(edgeQ);

        // Balance and speed.
        r.Balance = Balance(rules, d, reports);
        double balanceF = Math.Exp(-Math.Pow((r.Balance - d.idealBalance) / (0.5 * Math.Abs(d.idealBalance) + 40), 2));
        if (balanceF < 0.6) r.Notes.Add(r.Balance > d.idealBalance ? "it is tip-heavy" : "its weight is all in the hand");
        double refMass = Math.Max(1, d.referenceMass);
        double working = m != null ? m.Mass : mass;
        double speed = Math.Pow(refMass / Math.Max(1, working), 0.35) * (0.85 + 0.15 * balanceF);
        r.AttackSpeed = (float)CraftMath.Clamp(speed, 0.6, 1.4);

        // Damage: the mass behind the blow, the edge, the hardness; a loose head wastes it.
        double massF = Math.Pow(Math.Max(1, working) / refMass, 0.5);
        double cut = edged ? 0.45 + 0.55 * r.Edge * Math.Sqrt(hardF) : 0.8 + 0.2 * hardF;
        double security = 0.75 + 0.25 * r.Assembly;
        double dmg = massF * cut * security * (0.85 + 0.15 * r.Workmanship);
        r.DamageMin = (float)Math.Max(1, d.damageMin * dmg);
        r.DamageMax = (float)Math.Max(r.DamageMin, d.damageMax * dmg);
        r.CritChance = (float)CraftMath.Clamp(d.critChance * (edged ? 0.6 + 0.8 * sharp * sym : 0.8 + 0.2 * hardF), 0, 0.5);
        r.CritMultiplier = Math.Max(1f, d.critMultiplier);
        r.Knockback = (float)(d.knockback * Math.Pow(Math.Max(1, working) / refMass, 0.7));
        r.ToolDamage = (float)(d.toolDamage * (0.5 + 0.5 * hardF) * (edged ? 0.6 + 0.4 * r.Edge : 1) * security);

        // Durability: toughness against breaking, hardness against wear, integrity, joints, rust.
        double brittle = m != null && m.EdgeHardness > 650 && m.MinToughness < 10 ? 0.6 : 1;
        double treated = 1;
        if (m != null && m.Profile != null && m.Profile.HardensByQuench && d.MainComponent.shape.hardened)
        {
            if (m.Hardened < 0.3) treated = 0.75;
            else if (m.Tempered < 0.05) treated = 0.5;
        }
        double corrosion = m != null ? m.Corrosion : 0.4;
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
        // An item with nothing but straps (a belt) is rated by its straps.
        bool shields = d.components.Exists(c => c.function == CraftComponentFunction.Protection || c.function == CraftComponentFunction.Backing);
        foreach (CraftComponentSpec c in d.components)
        {
            int count = Math.Max(1, c.count);
            double refEach = Reference(c);
            if (c.function == CraftComponentFunction.Protection || c.function == CraftComponentFunction.Backing && c.kind == CraftPieceKind.Hide
                || !shields && c.function == CraftComponentFunction.Strap && c.kind == CraftPieceKind.Hide)
                reference += refEach * count * (c.function == CraftComponentFunction.Backing ? 0.4 : 1);
            if (c.supply)
            {
                if (c.supplyKind == CraftSupplyKind.Padding)
                {
                    double pad = a != null ? a.Supply(c.role) : 0;
                    CraftSupplyDefinition s = rules.FirstSupply(CraftSupplyKind.Padding);
                    protect += Math.Min(pad, c.supplyCount) * 0.6 * (s?.strength ?? 1);
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
                            v = pr.Count * 0.12 * (0.4 + 0.6 * CraftMath.Smooth(60, 300, pr.BodyHardness));
                        break;
                }
                protect += v;
            }
            if (c.kind == CraftPieceKind.Fasteners && c.fastener == CraftFastenerKind.Stud)
                reference += c.fastenerCount * 0.12 * 0.8;
        }
        double ratio = reference > 1e-6 ? protect / reference : 0;
        r.Protection = CraftMath.Clamp01(ratio);
        r.Defense = (float)(d.defense * ratio * (0.7 + 0.3 * r.Assembly));
        r.MagicDefense = (float)(d.magicDefense + magic);
        foreach (KeyValuePair<ElementType, double> kv in resist)
            r.Resistances.Add(new ElementalResistanceValue(kv.Key, (float)Math.Min(60, kv.Value)));
        double leatherWater = 1;
        foreach (KeyValuePair<string, List<CraftPieceReport>> kv in reports)
            foreach (CraftPieceReport pr in kv.Value)
                if (pr.Kind == CraftPieceKind.Hide)
                    leatherWater = Math.Min(leatherWater, 0.7 + 0.3 * pr.WaterResistance);
        if (roughEdges && HasLeather(d))
            r.Notes.Add("unfinished metal edges will chafe the leather");
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

    /// <summary>The protection a well-made part of this spec gives (what the design's Defense is for).</summary>
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
                return mid * (0.25 + 0.35 * 0.6) * 0.85 * 0.9;
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
