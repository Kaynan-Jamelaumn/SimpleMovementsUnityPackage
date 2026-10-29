using System.Collections.Generic;

/// <summary>
/// What comes next for a piece on the station, read from its state: "Clean the crust off it" (Lapidary → Clean),
/// "Card it" (Fibre bench → Card), "Sew its seams" (Tailor → Sew)... - the work area (by its title) and the place in it.
/// It only says what the state shows: steps that are optional are named as such, and nothing is suggested where the
/// piece's path is the player's own choice (a bar of steel can become anything).
/// </summary>
public static class CraftGuide
{
    public struct Step
    {
        /// <summary>What to do, in a few words.</summary>
        public string text;
        /// <summary>The work area (its title) and the place in it ("" = the area's own choice).</summary>
        public string area, place;
    }

    private static Step S(string text, string area, string place = "") => new Step { text = text, area = area, place = place };

    /// <summary>The next step for <paramref name="p"/> at <paramref name="station"/>, or null when nothing in particular is next.</summary>
    public static Step? Next(CraftStation station, CraftPiece p, CraftJournalData known)
    {
        if (station == null || p == null || station.Workshop == null)
            return null;
        CraftRules rules = station.Rules;
        CraftWorkshop ws = station.Workshop;
        switch (p.kind)
        {
            case CraftPieceKind.Gem: return Gem(station, p, known);
            case CraftPieceKind.Fibre: return Fibre(rules, p);
            case CraftPieceKind.Yarn:
                if (p.yarn.measured)
                    return S("Reel it off as thread (or weave it)", "Tailor", "Skein");
                return S(station.Has(CraftTools.Loom) ? "Weave it into cloth, or wind a skein to reel thread (ply it first for strength)" : "Wind it into a skein and count it, then reel thread (ply it first for strength)",
                    "Tailor", station.Has(CraftTools.Loom) ? "Weave" : "Skein");
            case CraftPieceKind.Hide:
                if (CraftTextiles.IsCloth(rules, p))
                    return S("Lay its patterns out (optional), then cut them", "Tailor", p.hide.layout.Count > 0 ? "Cut" : "Layout");
                return p.hide.dried ? S("Cut its patterns", "Cut") : S("Work it at the tannery: the path above the hide shows what is next", station.Has(CraftTools.FleshingBeam) ? "Tannery" : "Finish leather");
            case CraftPieceKind.Ore:
                return S("Crush it, wash or brush it, and sort out the ore", "Ore");
            case CraftPieceKind.Wood:
                if (p.wood.log)
                    return S("Debark it, then split it into billets", "Woodshop", "Debark");
                return null;
            case CraftPieceKind.Metal:
                if (station.Has(CraftTools.JewelersBench) && CraftJewelry.PartOf(p) == CraftJewelPart.None && p.Length <= 650 && p.segments.Count > 0 && p.segments[0].thickness <= 3)
                    return S("Form it round the mandrel into a band or a setting", "Jeweller", "Form");
                return null;
            case CraftPieceKind.Assembly:
                return Assembly(station, ws, p);
            default:
                return null;
        }
    }

    private static Step? Gem(CraftStation station, CraftPiece p, CraftJournalData known)
    {
        GemState g = p.gem;
        bool known1 = g.identified || CraftJournal.KnowsGem(known, g.gem);
        if (g.stage < CraftGemStage.Faceted && CraftLapidary.Dirt(g) > 0.15)
            return S("Clean the crust off it (it blurs the tests and hides flaws)", "Lapidary", "Clean");
        if (!known1 && CraftLapidary.Tests(g) < 2)
            return S("Test it - the loupe, a hardness point, the refractometer, the balance", "Lapidary", "Test");
        if (!known1)
            return S("Name it from your readings and the gem book", "Lapidary", "Identify");
        if (!g.oriented)
            return S(g.evaluated ? "Plan the cut: place the stone clear of the flaws" : "Search it under the loupe (optional), then plan the cut", "Lapidary", g.evaluated ? "Orient" : "Evaluate");
        if (g.stage < CraftGemStage.Faceted && g.facets.Count == 0)
        {
            if (g.profile.Count == 0 && CraftLapidary.Excess(g) > 0.8)
                return S("Saw (or split) the bulk of the rough off", "Lapidary", "Saw");
            if (g.profile.Count == 0 || CraftLapidary.GroundExcess(g) > 0.15)
                return S("Grind it to its outline on the wheel", "Lapidary", "Grind");
        }
        if (g.stage < CraftGemStage.Faceted)
            return S("Cut its facets, tier by tier", "Lapidary", "Facet");
        if (g.stage < CraftGemStage.Polished)
            return S("Polish every part of it bright", "Lapidary", "Polish");
        return station.Has(CraftTools.JewelersBench)
            ? S("Ready to set: start a piece of jewellery, then cut its seat", "Jeweller", "Seat")
            : S("Ready to set: take it to a jeweller's bench", "Lapidary", "Polish");
    }

    private static Step? Fibre(CraftRules rules, CraftPiece p)
    {
        FibreState f = p.fibre;
        if (f.dirt > 0.15 && !f.cleaned)
            return S("Pick the burrs and seeds out", "Fibre bench", "Pick");
        if (f.grease > 0.15 && !f.scoured)
            return S("Scour the grease out", "Fibre bench", "Scour");
        if (f.wet > 0.3)
            return S("It is drying on the rack: wait", "Fibre bench", "Scour");
        if (f.tangle > 0.3 && !f.opened)
            return S("Tease the locks open", "Fibre bench", "Tease");
        if (!f.carded && !f.combed)
        {
            CraftSupplyDefinition d = CraftFibre.Def(rules, p);
            return d != null && d.staple >= 40 ? S("Card it - or comb it, for a smooth worsted", "Fibre bench", "Card") : S("Card it", "Fibre bench", "Card");
        }
        return S("Spin it at the wheel", "Tailor", "Spin");
    }

    private static Step? Assembly(CraftStation station, CraftWorkshop ws, CraftPiece a)
    {
        CraftDesign d = station.Rules.Design(a.assembly.design);
        if (d == null)
            return null;
        if (a.assembly.finished)
            return station.IsInspected(a.id) ? S("Take it: it is done", "Assemble") : S("Try it (inspect) to see what it is", "Assemble");
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.optional) continue;
            bool missing = c.supply ? a.assembly.Supply(c.role) < System.Math.Max(1, c.supplyCount) : ws.Piece(a.assembly.PieceIn(c.role, System.Math.Max(0, c.count - 1))) == null;
            if (missing && !(c.supply && c.supplyKind == CraftSupplyKind.Thread))
                return S($"Fit its parts: {c.Name.ToLowerInvariant()} first", "Assemble");
        }
        foreach (CraftJointSpec j in d.joints)
        {
            JointState s = a.assembly.Joint(j.id);
            if (s != null && s.done) continue;
            if (CraftJewelry.IsJewelJoint(j.kind))
                return S($"{j.Name}: jeweller's work", "Jeweller", j.kind == CraftJointKind.Solder ? "Solder" : "Set");
            if (j.kind == CraftJointKind.Stitch && OfCloth(d) && station.Has(CraftTools.TailorTable))
                return S($"Sew the {j.Name.ToLowerInvariant()}", "Tailor", "Sew");
            return S($"Make the {j.Name.ToLowerInvariant()}", "Assemble");
        }
        if (d.kind == CraftDesignKind.Jewelry && a.assembly.finish <= 0)
            return S("Buff it, steam it, inspect and finish it", "Jeweller", "Finish");
        return S("Finish it", "Assemble");
    }

    private static bool OfCloth(CraftDesign d)
    {
        foreach (CraftComponentSpec c in d.components)
            if (c.kind == CraftPieceKind.Hide && c.leather != null && c.leather.fabric == CraftFabric.Cloth) return true;
        return false;
    }
}
