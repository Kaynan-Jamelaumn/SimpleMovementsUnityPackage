using System;
using System.Collections.Generic;

/// <summary>
/// What comes next for a piece on the station, read from its state: "Clean the crust off it" (Lapidary → Clean),
/// "Card it" (Fibre bench → Card), "Sew its seams" (Tailor → Sew)... - the work area (by a stable key: the type name of
/// its <c>CraftMode</c>) and the place in it. It only says what the state shows, by the model's own thresholds: steps
/// that are optional are named as such, and nothing is suggested where the piece's path is the player's own choice (a
/// bar of steel can become anything). Where the station lacks what the step needs, the step says where to take the
/// piece instead and names no area (the window then offers no Go). With nothing picked, it says what the furnace holds
/// once its charge is spent (a bloom, a bead, metal to run off); the bloom pulled is closed up at a forge (the anvil at
/// welding heat) until it is a sound bar. A wooden blank meant for a part, and a wooden part
/// fitted to an item, are held to the length the part is judged by: a stock blank is sawn to it before it is fitted.
/// <para>The rules are here, where the tests reach them (<see cref="Next(CraftWorkshop, CraftTools, CraftPiece, CraftJournalData, Func{string, bool})"/>);
/// the station window asks through its own overload, with the station's tools and the items it has tried.</para>
/// </summary>
public static partial class CraftGuide
{
    public struct Step
    {
        /// <summary>What to do, in a few words.</summary>
        public string text;
        /// <summary>
        /// The work area, by its stable key - the type name of its <c>CraftMode</c> (<see cref="Lapidary"/> is
        /// "CraftLapidaryMode") - and the place in it ("" = the area's own choice). An empty area: the step is done
        /// elsewhere (no Go).
        /// </summary>
        public string area, place;
        /// <summary>The piece to pick for it when it is not the selected one (a part fitted to the item: its blade), else null.</summary>
        public string piece;
    }

    /// <summary>The work areas' keys: the type names of their <c>CraftMode</c>s (the window finds an area by its type's name).</summary>
    public const string Lapidary = "CraftLapidaryMode", Jeweller = "CraftJewellerMode", Fibres = "CraftFibreMode", Tailor = "CraftTailorMode",
        Tannery = "CraftTanneryMode", Cutting = "CraftCuttingMode", Assemble = "CraftAssemblyMode", Ore = "CraftOreMode",
        Furnace = "CraftFurnaceMode", Crucible = "CraftCrucibleMode", Woodshop = "CraftWoodshopMode", Grind = "CraftGrindMode",
        Sharpen = "CraftSharpenMode", Forge = "CraftForgeMode", Carve = "CraftCarveMode", Anvil = "CraftAnvilMode";

    /// <summary>Every area key the guide names (the window checks each names an area it has).</summary>
    public static readonly string[] AreaKeys = { Lapidary, Jeweller, Fibres, Tailor, Tannery, Cutting, Assemble, Ore, Furnace, Crucible, Woodshop, Grind, Sharpen, Forge, Carve, Anvil };

    private static Step S(string text, string area, string place = "", string piece = null) => new Step { text = text, area = area ?? "", place = place ?? "", piece = piece };
    /// <summary>A step done somewhere else (the station lacks what it needs): no area, so no Go.</summary>
    private static Step Elsewhere(string text) => S(text, "");

    private static bool Has(CraftTools tools, CraftTools t) => (tools & t) == t;

    /// <summary>
    /// The next step for <paramref name="p"/> on workshop <paramref name="ws"/> at a station with <paramref name="tools"/>
    /// (<paramref name="inspected"/>: has this finished item been tried here?), or null when nothing in particular is next.
    /// With no piece: what the furnace holds once its charge is spent.
    /// </summary>
    public static Step? Next(CraftWorkshop ws, CraftTools tools, CraftPiece p, CraftJournalData known, Func<string, bool> inspected = null)
    {
        if (ws == null)
            return null;
        if (p == null)
            return FurnaceDone(ws, tools);
        if (p.location == CraftLocation.Assembled)
        {
            // A part fitted to an item (a blade being ground in its hilt): the item's own steps say what is next.
            CraftPiece item = ws.Piece(p.parent);
            return item != null && item != p ? Next(ws, tools, item, known, inspected) : null;
        }
        CraftRules rules = ws.Rules;
        switch (p.kind)
        {
            case CraftPieceKind.Gem: return Gem(tools, p, known);
            case CraftPieceKind.Fibre: return Fibre(rules, p);
            case CraftPieceKind.Yarn: return Yarn(ws, tools, p);
            case CraftPieceKind.Hide: return Hide(ws, tools, p);
            case CraftPieceKind.Ore: return OreStep(ws, tools, p);
            case CraftPieceKind.Wood: return Wood(ws, tools, p);
            case CraftPieceKind.Metal: return Metal(ws, tools, p);
            case CraftPieceKind.Assembly: return Assembly(ws, tools, p, inspected);
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------ stones
    private static Step? Gem(CraftTools tools, CraftPiece p, CraftJournalData known)
    {
        GemState g = p.gem;
        // Named - rightly or not - the name sticks (the workshop takes no second one): the stone goes on to be planned.
        bool named = g.identified || !string.IsNullOrEmpty(g.identifiedAs) || CraftJournal.KnowsGem(known, g.gem);
        if (g.stage < CraftGemStage.Faceted && CraftLapidary.Dirt(g) > 0.15)
            return S("Clean the crust off it (it blurs the tests and hides flaws)", Lapidary, "Clean");
        if (!named && CraftLapidary.Tests(g) < 2)
            return S("Test it - the loupe, a hardness point, the refractometer, the balance", Lapidary, "Test");
        if (!named)
            return S("Name it from your readings and the gem book", Lapidary, "Identify");
        if (!g.oriented)
            return S(g.evaluated ? "Plan the cut: place the stone clear of the flaws" : "Search it under the loupe (optional), then plan the cut", Lapidary, g.evaluated ? "Orient" : "Evaluate");
        if (g.stage < CraftGemStage.Faceted && g.facets.Count == 0)
        {
            // The wheel takes on no more than GrindableExcess outside the planned stone; the lap no rough over PreformExcess.
            if (g.profile.Count == 0 && CraftLapidary.Excess(g) > CraftLapidary.GrindableExcess)
                return S("Saw (or split) the bulk of the rough off", Lapidary, "Saw");
            if (g.profile.Count == 0)
                return S(CraftLapidary.Excess(g) > CraftLapidary.PreformExcess ? "Grind it to its outline on the wheel"
                    : "Grind it to its outline on the wheel (faceted unground, its outline comes out lopsided)", Lapidary, "Grind");
            if (CraftLapidary.GroundExcess(g) > 0.15)
                return S("Grind it on to its outline (the lap takes it once less than 15% lies outside)", Lapidary, "Grind");
        }
        if (g.stage < CraftGemStage.Faceted)
            return S("Cut its facets, tier by tier", Lapidary, "Facet");
        if (g.stage < CraftGemStage.Polished)
            return S("Polish every part of it bright", Lapidary, "Polish");
        // Its seat is cut in the setting (loose, or already on the piece); the stone goes in once the soldering is done.
        return Has(tools, CraftTools.JewelersBench)
            ? S("Ready to set: cut a setting's seat to this stone", Jeweller, "Seat")
            : Elsewhere("Ready to set: take it to a jeweller's bench");
    }

    // ------------------------------------------------------------------ fibre and yarn
    private static Step? Fibre(CraftRules rules, CraftPiece p)
    {
        FibreState f = p.fibre;
        if (f.dirt > 0.15 && !f.cleaned)
            return S("Pick the burrs and seeds out", Fibres, "Pick");
        if (f.grease > 0.15 && !f.scoured)
            return S("Scour the grease out", Fibres, "Scour");
        if (f.wet > 0.3)
            return S("It is drying on the rack: wait (wet, it neither cards nor spins)", Fibres, "Scour");
        if (f.tangle > 0.3 && !f.opened)
            return S("Tease the locks open", Fibres, "Tease");
        if (!f.carded && !f.combed)
        {
            CraftSupplyDefinition d = CraftFibre.Def(rules, p);
            return d != null && d.staple >= 40 ? S("Card it - or comb it, for a smooth worsted", Fibres, "Card") : S("Card it", Fibres, "Card");
        }
        return S("Spin it at the wheel", Tailor, "Spin");
    }

    private static Step? Yarn(CraftWorkshop ws, CraftTools tools, CraftPiece p)
    {
        YarnState y = p.yarn;
        if (y.measured)
            return S("Reel it off as thread (or weave it)", Tailor, "Skein");
        if (!Has(tools, CraftTools.Loom))
            return S("Wind it into a skein and count it, then reel thread (ply it first for strength)", Tailor, "Skein");
        // Too little yarn for even the narrowest warp: another bobbin wound on at the loom makes it longer.
        if (CraftFibre.RowsFor(y.length, CraftFibre.MinWidth) < 1)
            return S("Too little yarn on this bobbin to weave: wind another bobbin on at the loom (Wind on), or wind a skein to reel thread", Tailor, "Weave");
        // The largest cloth part an item here still wants, cut along the grain: the cloth must be as wide and as long.
        CraftPattern largest = null;
        string forItem = "";
        foreach ((string pattern, int count, string item) in CraftTextiles.PatternsWanted(ws))
        {
            CraftPattern pat = ws.Rules.Pattern(pattern);
            if (pat == null) continue;
            pat.Size(0, out int w, out int h);
            if (w > CraftFibre.MaxWidth || h > CraftFibre.MaxRows) continue; // no loom weaves it whole
            if (largest == null || w * h > Area(largest)) { largest = pat; forItem = item; }
        }
        if (largest != null)
        {
            largest.Size(0, out int w, out int h);
            int rows = CraftFibre.RowsFor(y.length, Math.Max(CraftFibre.MinWidth, w));
            if (rows < h)
                return S($"Too short for the {forItem.ToLowerInvariant()}'s {largest.Name.ToLowerInvariant()} ({h} rows {w} wide; it weaves {rows}): wind another bobbin on at the loom (Wind on)", Tailor, "Weave");
        }
        return S("Weave it into cloth, or wind a skein to reel thread (ply it first for strength)", Tailor, "Weave");
    }

    private static int Area(CraftPattern pat)
    {
        pat.Size(0, out int w, out int h);
        return w * h;
    }

    // ------------------------------------------------------------------ hides and cloth
    private static Step? Hide(CraftWorkshop ws, CraftTools tools, CraftPiece p)
    {
        HideState h = p.hide;
        bool cloth = CraftTextiles.IsCloth(ws.Rules, p);
        // A part already cut goes into an item.
        if (!string.IsNullOrEmpty(h.pattern))
            return cloth ? S("Fit it into an item, then sew its seams", Assemble)
                : S("Fit it into an item (slick its edges burnished first)", Assemble);
        if (cloth)
            return Has(tools, CraftTools.TailorTable)
                ? S("Lay its patterns out (optional), then cut them", Tailor, h.layout.Count > 0 ? "Cut" : "Layout")
                : Has(tools, CraftTools.LeatherBench) ? S("Cut its patterns (along the grain)", Cutting) // the leather bench's table cuts cloth where there is no tailor's
                : Elsewhere("Take it to a tailor's table to lay out and cut its patterns");
        if (!h.dried)
            return Tanning(ws, tools, p);
        bool stakes = (tools & CraftWorkshop.FinishingTools) != 0;
        bool hung = p.location == CraftLocation.Frame || p.location == CraftLocation.Rack;
        if (p.location == CraftLocation.Vat)
            return S("Lift it out of the vat", Tannery, "Vats");
        if (Mean(h, c => c.stiff) >= 0.35 && stakes)
            return DriedStiff(ws, h, hung);
        if (hung)
            return S("It is dry: take it down onto the bench", Tannery, "Frame");
        // The path over the tannery ends in its finish (dye, then oil or wax, then slick): it may come before the cutting.
        bool unfinished = stakes && Finished(h) < 1;
        if (Has(tools, CraftTools.LeatherBench))
            return S(unfinished ? "Cut its patterns (dye it, oil or wax it and slick it first, if you like)" : "Cut its patterns", Cutting);
        return unfinished ? S("Dye it, oil or wax it and slick it (optional), then take it to a leather bench to cut its patterns", Tannery, "Beam")
            : Elsewhere("Take it to a leather bench to cut its patterns");
    }

    /// <summary>
    /// A hide that dried stiff, worked supple again the harmless way, at the beam or on the frame: bark-tanned grain
    /// cracks under the staking blade where it is dry (<see cref="CraftLeather.CracksStakedDry"/>), so it is oiled supple
    /// (oil softens dry leather and does it no harm; it keeps dye out, so a hide to be dyed is dyed first) - soaked and
    /// staked as it dries again it stiffens anew, and its thin edges dry first and crack; any other leather is staked dry
    /// (it softens slowly).
    /// </summary>
    private static Step DriedStiff(CraftWorkshop ws, HideState h, bool hung) =>
        S(CraftLeather.CracksStakedDry(ws.Rules, h)
            ? "It dried stiff: oil it supple, dyed first if it is to be dyed (staked dry, bark-tanned grain cracks)"
            : "It dried stiff: stake it supple (dry, it softens slowly)", Tannery, hung ? "Frame" : "Beam");

    /// <summary>How far the hide is finished, as the path over the tannery reads it: oil and wax, dye, or the grain slicked.</summary>
    private static double Finished(HideState h) =>
        CraftMath.Clamp01(Math.Max((Mean(h, c => c.oil) + Mean(h, c => c.wax)) / 0.2, Math.Max(Mean(h, c => c.dye) / 0.3, Mean(h, c => c.smooth * c.grain) / 0.3)));

    /// <summary>
    /// A hide still on the tannery's path, read the way the path over the tannery reads it: flesh it, lime it, scrape the
    /// hair, tan it, stretch, stake and dry it - each at the place of the station that does it, or where to take it. A hide
    /// soaking in a vat that is not doing what it needs now is lifted out first.
    /// </summary>
    private static Step? Tanning(CraftWorkshop ws, CraftTools tools, CraftPiece p)
    {
        HideState h = p.hide;
        bool beam = Has(tools, CraftTools.FleshingBeam), vats = Has(tools, CraftTools.Vats), frame = Has(tools, CraftTools.Frame), rack = Has(tools, CraftTools.Rack);
        bool stakes = (tools & CraftWorkshop.FinishingTools) != 0;
        double flesh = Mean(h, c => c.flesh), hair = Mean(h, c => c.hair), loose = Mean(h, c => c.hairLoose), core = Mean(h, c => c.tanC), wet = Mean(h, c => c.moisture);
        bool fleshed = flesh < 0.08, limed = hair < CraftLeather.UnhairedBelow || loose >= 0.7, unhaired = hair < CraftLeather.UnhairedBelow, tanned = core > 0.75;
        // What it needs before it is stretched and dried, in the path's order.
        string next = !fleshed ? "flesh it on the beam" : !limed ? "lime it in a vat of lime water" : !unhaired ? "scrape the hair off on the beam" : !tanned ? "tan it in a tanning liquor" : null;
        if (p.location == CraftLocation.Vat)
        {
            CraftVatState vat = p.vat >= 0 && p.vat < ws.Vats.Count ? ws.Vats[p.vat] : null;
            CraftTanningAgent soak = vat != null && vat.volume > 0.01 ? ws.Rules.Agent(vat.agent) : null;
            bool liquor = soak != null && soak.kind != CraftAgentKind.Water && soak.kind != CraftAgentKind.Lime && soak.kind != CraftAgentKind.Acid;
            if (fleshed && !limed && soak?.kind == CraftAgentKind.Lime)
                return S("It is liming in the vat: lift it out once LOOSE passes 70% (past 95% the grain spoils)", Tannery, "Vats");
            if (fleshed && unhaired && !tanned && liquor)
                return S("It is tanning in the vat: strengthen and stir the liquor until CORE passes 75%", Tannery, "Vats");
            return S(next == null ? "It is tanned through: lift it out of the vat" : fleshed && limed && !unhaired ? "It is limed: lift it out of the vat, then scrape the hair off on the beam"
                : $"Lift it out of the vat, then {next}", Tannery, "Vats");
        }
        if (p.location == CraftLocation.Frame || p.location == CraftLocation.Rack)
        {
            if (next != null)
                return S($"Take it down onto the bench, then {next}", Tannery, "Frame");
            string where = p.location == CraftLocation.Frame ? "It is drying in the frame: keep every side in the green, and stake it" : "It is drying on the rack: stake it";
            if (!stakes)
                return S($"{where} while it is damp, on a fleshing beam or a leather bench", Tannery, "Frame");
            return S(wet <= CraftLeather.StakeWet && wet > CraftLeather.DryAt ? "Stake it now, while it is damp, and again as it dries (the DRY bar's gold band)"
                : $"{where} while it is damp (the DRY bar's gold band)", Tannery, "Frame");
        }
        if (!fleshed)
            return beam ? S("Flesh it on the beam (flesh side up): take the meat and fat off", Tannery, "Beam") : Elsewhere("Take it to a fleshing beam to flesh it");
        if (!limed)
            return vats ? S("Lime it in a vat to loosen the hair (lift it out once LOOSE passes 70%)", Tannery, "Vats") : Elsewhere("Take it to a tannery's vats to lime it");
        if (!unhaired)
            return beam ? S("Scrape the hair off on the beam (grain side up)", Tannery, "Beam") : Elsewhere("Take it to a fleshing beam to scrape the hair off");
        if (!tanned)
            return vats ? S("Tan it in a vat until CORE passes 75% (start weak, strengthen and stir)", Tannery, "Vats") : Elsewhere("Take it to a tannery's vats to tan it");
        if (frame)
            return S("Stretch it in the frame (each side in the green) and stake it as it dries (or hang it on the rack)", Tannery, "Frame");
        if (rack)
            return S("Hang it on the rack to dry (it puckers a little) and stake it as it dries", Tannery, "Frame");
        return Elsewhere("Take it to a stretching frame or a drying rack to dry it (unstretched it puckers)");
    }

    private static double Mean(HideState h, Func<HideCell, double> of)
    {
        double sum = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c != null && c.present) { sum += of(c); n++; }
        return n > 0 ? sum / n : 0;
    }

    // ------------------------------------------------------------------ ore and metal
    private static Step? OreStep(CraftWorkshop ws, CraftTools tools, CraftPiece p)
    {
        CraftRules rules = ws.Rules;
        CraftOreDefinition o = rules.Ore(p.ore.ore);
        bool sulfide = o != null && o.sulfide && p.ore.roasted < CraftSmelting.RoastedEnough;
        CraftElement fumed = FumesAway(rules, p, o); // calamine: its zinc boils off in the furnace, a copper melt drinks it
        bool fumes = fumed != null;
        string metal = fumes ? fumed.Name.ToLowerInvariant() : "";
        if (p.location == CraftLocation.Furnace)
        {
            if (fumes)
                return S($"Its {metal} goes up the shaft as fumes, and little else stays (it gives its {metal} to molten copper in a covered crucible)", Furnace);
            if (sulfide)
                return S("It is roasting: keep the air open (a clear, oxidising flame) at a red heat until the sulfur is gone, then a reducing fire smelts it", Furnace);
            return S(MeltsInFurnace(rules, p)
                ? "It is smelting: keep the fire hot and reducing and tap the slag; then run the metal off into a mould"
                : "It is smelting: keep the fire hot and reducing and tap the slag; then pull the bloom", Furnace);
        }
        if (p.location == CraftLocation.Melt)
            return S(fumes ? $"Its {metal} goes into the copper (keep the cover on); lift it out with the tongs once it is spent"
                : "It gives its metal to the melt; lift it out with the tongs once it is spent", Crucible);
        if (p.location != CraftLocation.Air)
            return null;
        double dirt = 0, mass = 0;
        foreach (OreFragment f in p.ore.fragments) { dirt += f.dirt * f.mass; mass += f.mass; }
        bool clean = mass <= 0 || dirt <= mass * 0.05; // the look says "caked in dirt and clay" above this
        bool bench = Has(tools, CraftTools.OreBench), furnace = Has(tools, CraftTools.Furnace), crucible = Has(tools, CraftTools.Crucible);
        if (fumes)
        {
            if (!clean && bench)
                return S(crucible ? "Crush it, wash or brush it, and sort out the ore - then put it in a crucible of molten copper under a cover" : "Crush it, wash or brush it, and sort out the ore", Ore);
            return crucible ? S($"Put it in the crucible with molten copper, under a cover: the copper drinks its {metal} (a furnace would only boil it away)", Crucible)
                : Elsewhere($"Take it to a crucible: put in with molten copper under a cover, it gives the copper its {metal}");
        }
        string charge = sulfide ? "Charge it into the furnace and roast it there first in an oxidising blast (unroasted it gives a matte)"
            : !string.IsNullOrEmpty(o?.needsFlux) ? $"Charge it into the furnace with {rules.Supply(o.needsFlux)?.Name.ToLowerInvariant() ?? o.needsFlux}, and run it very hot"
            : "Charge it into the furnace";
        if (clean && furnace)
            return S(charge, Furnace);
        if (bench)
            return S(furnace ? "Crush it, wash or brush it, and sort out the ore - then charge it into the furnace" : "Crush it, wash or brush it, and sort out the ore", Ore);
        return furnace ? S($"{charge} (crushed and washed at an ore bench, it smelts cleaner)", Furnace)
            : Elsewhere("Take it to an ore bench to crush and wash it");
    }

    /// <summary>
    /// The ore's metal (the first it holds, as the crucible reads it) when it boils away below the heat the ore gives it
    /// up at (calamine's zinc: <see cref="CraftSmelting.Fumes"/>), else null.
    /// </summary>
    private static CraftElement FumesAway(CraftRules rules, CraftPiece p, CraftOreDefinition o)
    {
        if (o == null)
            return null;
        foreach (CraftAmount b in p.ore.bound)
        {
            CraftElement e = rules.Element(b.element);
            if (e == null || e.kind != CraftElementKind.Metal && e.kind != CraftElementKind.Fantasy) continue;
            return CraftSmelting.Fumes(e, 0, o) ? e : null;
        }
        return null;
    }

    /// <summary>Does the ore's main metal (the first that does not fume) run liquid at the furnace's heat (copper, lead), or grow into a bloom (iron)?</summary>
    private static bool MeltsInFurnace(CraftRules rules, CraftPiece p)
    {
        CraftOreDefinition o = rules.Ore(p.ore.ore);
        foreach (CraftAmount b in p.ore.bound)
        {
            CraftElement e = rules.Element(b.element);
            if (e == null || e.kind != CraftElementKind.Metal && e.kind != CraftElementKind.Fantasy || CraftSmelting.Fumes(e, 0, o)) continue;
            return e.meltingPoint <= MeltsBelow;
        }
        return false;
    }

    /// <summary>A bloomery melts out what melts below this (copper, tin, lead, silver, gold); iron grows into a bloom.</summary>
    private const double MeltsBelow = 1250;

    /// <summary>What the furnace holds once its charge is spent: metal to run off, a bead to lift out, a bloom to pull.</summary>
    private static Step? FurnaceDone(CraftWorkshop ws, CraftTools tools)
    {
        if (!Has(tools, CraftTools.Furnace))
            return null;
        foreach (CraftPiece q in ws.Pieces)
            if (q.location == CraftLocation.Furnace && (q.kind == CraftPieceKind.Ore || q.kind == CraftPieceKind.Metal))
                return null; // still smelting or melting down: its own step says what comes next
        CraftFurnaceState f = ws.Furnace;
        double pool = f.PoolMass;
        if (pool >= CraftSmelting.PourMinimum)
            return CraftSmelting.PoolLiquid(ws.Rules, f)
                ? S("Run the metal at the bottom of the furnace off into a mould (Pour, from the furnace)", Crucible)
                : S("The metal at the bottom has frozen: fire the furnace hotter, then run it off into a mould", Furnace);
        if (pool >= CraftSmelting.BeadMinimum)
            return S("Lift the bead out with the tongs: too little metal to pour", Furnace);
        if (f.BloomMass >= CraftSmelting.BloomMinimum)
            return S("Pull the bloom out with the tongs", Furnace);
        return null;
    }

    /// <summary>The share of an element in a piece's metal (0-1).</summary>
    private static double Share(CraftPiece p, string element)
    {
        double total = 0;
        foreach (CraftAmount a in p.composition) total += a.grams;
        return total > 0 ? p.Grams(element) / total : 0;
    }

    /// <summary>Metal still full of sulfur (a matte: the furnace roasts it - the default classes' "Copper matte" from 2%).</summary>
    private const double MatteSulfur = 0.02;
    /// <summary>Lead worth cupelling for its silver (the default classes' "Silver-bearing lead" from 0.05%).</summary>
    private const double CupelSilver = 0.0005;

    private static Step? Metal(CraftWorkshop ws, CraftTools tools, CraftPiece p)
    {
        CraftRules rules = ws.Rules;
        // A forge weld laid by hand and not yet set: strike it shut on the anvil while it is at welding heat.
        if (CraftForging.SeamOpen(p) > 0)
        {
            if (p.location == CraftLocation.Fire)
                return S("Set the weld: bring it to welding heat in the fire, then take it out and strike the seam shut", Forge);
            if (p.location != CraftLocation.Air)
                return S("Set the weld: back to the fire for a welding heat, then strike the seam shut", Forge);
            return AtWeldingHeat(rules, p, s => s.seamOpen > 0)
                ? S("Set the weld: strike the seam shut with the flat of the hammer while it is at welding heat", Anvil)
                : S("Set the weld: flux it again and take another welding heat in the fire, then strike the seam shut", Forge);
        }
        // A bloom drawn from the furnace is nothing yet: it is closed up at welding heat with flat blows until it is a
        // sound bar (struck cold, it crumbles) - at a forge, which a smeltery has not.
        if (CraftLook.Bloom(rules, p) && p.location != CraftLocation.Furnace)
        {
            if (!Has(tools, CraftTools.Forge))
                return Elsewhere("Take the bloom to a forge: close it up at welding heat with flat blows until it is a sound bar");
            if (p.location == CraftLocation.Fire)
                return S("Close the bloom up: bring it to welding heat in the fire, then take it out and strike it with flat blows until it is a sound bar", Forge);
            return p.location == CraftLocation.Air && AtWeldingHeat(rules, p, s => s.porosity > CraftForging.SoundPores || s.slag > 0)
                ? S("Close the bloom up: strike it with flat blows while it is at welding heat, until it is a sound bar (struck cold, it crumbles)", Anvil)
                : S("Close the bloom up: into the fire for a welding heat, then strike it with flat blows until it is a sound bar", Forge);
        }
        bool matte = Share(p, "sulfur") >= MatteSulfur;
        bool silverLead = p.Profile(rules).BaseElement?.id == "lead" && Share(p, "silver") >= CupelSilver;
        bool furnace = Has(tools, CraftTools.Furnace);
        if (p.location == CraftLocation.Furnace)
            return matte ? S("It is roasting in the furnace: keep the blast oxidising (the air open) and hot until it melts down", Furnace)
                : silverLead ? S("It is melting down onto the cupel: keep the blast hot and oxidising until the silver flashes bright", Furnace)
                : null;
        if (matte && p.location == CraftLocation.Air)
            return furnace ? S("Roast the matte in the furnace: an oxidising blast burns its sulfur away, and it melts down to smelt clean", Furnace)
                : Elsewhere("Take it to a furnace to roast the matte: an oxidising blast burns its sulfur away");
        if (silverLead && p.location == CraftLocation.Air)
        {
            string cupel = "a " + (rules.FirstSupply(CraftSupplyKind.Cupel)?.Name.ToLowerInvariant() ?? "bone-ash cupel");
            return furnace ? S($"Cupel the lead: in the furnace with {cupel}, in a hot, oxidising blast - the lead soaks away, the silver stays", Furnace)
                : Elsewhere("Take it to a furnace to cupel the lead on bone ash: the lead soaks away, the silver stays");
        }
        if (Has(tools, CraftTools.JewelersBench) && CraftJewelry.PartOf(p) == CraftJewelPart.None && JewelStock(rules, p))
            return S("Form it round the mandrel into a band or a setting", Jeweller, "Form");
        return null;
    }

    /// <summary>
    /// Is a stretch of <paramref name="p"/> that still wants the work (<paramref name="wants"/>: its seam open, its pores
    /// not closed) near welding heat - within 150 °C of it, where a flat blow still sets a weld or closes a bloom?
    /// </summary>
    private static bool AtWeldingHeat(CraftRules rules, CraftPiece p, Func<MetalSegment, bool> wants)
    {
        double heat = CraftForging.WeldingHeat(rules, p.Profile(rules)) - 150;
        foreach (MetalSegment s in p.segments)
            if (wants(s) && s.Temperature > heat) return true;
        return false;
    }

    /// <summary>
    /// Is a metal piece meant for jewellery: made for a jewellery design's part, or the jeweller's own stock - wire or a
    /// strip, thin and narrow (not a thin blade blank or an armour plate, whose path is the player's own)?
    /// </summary>
    private static bool JewelStock(CraftRules rules, CraftPiece p)
    {
        if (!string.IsNullOrEmpty(p.intentDesign))
            return rules.Design(p.intentDesign)?.kind == CraftDesignKind.Jewelry;
        if (p.segments.Count == 0 || p.Length > 650)
            return false;
        foreach (MetalSegment s in p.segments)
            if (s.width > 6 || s.thickness > 3) return false;
        return true;
    }

    // ------------------------------------------------------------------ wood
    /// <summary>A part short of its design's curve by more than this (°) wants bending: past it its look says its curve is off.</summary>
    private const double CurveShort = 3;

    private static Step? Wood(CraftWorkshop ws, CraftTools tools, CraftPiece p)
    {
        CraftRules rules = ws.Rules;
        WoodState w = p.wood;
        if (p.location == CraftLocation.Stack)
            return S(w.moisture > CraftWoodwork.DryEnough ? "It is seasoning in the stack: leave it until it is dry" : "It is seasoned: drag it off the stack", Woodshop, "Dry");
        if (p.location == CraftLocation.Steam)
            return S("It is in the steam box: once it is steamed through, drag it out and bend it at once", Woodshop, "Bend");
        if (p.location != CraftLocation.Air)
            return null;
        CraftComponentSpec part = !string.IsNullOrEmpty(p.intentDesign) ? rules.Design(p.intentDesign)?.Component(p.intentRole) : null;
        if (part != null && part.kind != CraftPieceKind.Wood) part = null;
        // Broken down along the grain while it pays: a round log split with the wedges (a small one rived); green stock
        // split on into billets before it seasons; stock meant for a part split or rived while each half still holds it.
        bool on = BreaksDown(p, part);
        bool splits = on && CraftWoodwork.SplitsWithWedges(p, out _), rives = on && !splits && (w.log || part != null) && CraftWoodwork.RivesWithFroe(p, out _);
        if (CraftWoodwork.BarkLeft(p) > 0.05 && Has(tools, CraftTools.ShavingHorse))
            return S(splits ? "Debark it, then split it into billets" : rives ? "Debark it, then rive it with the froe" : "Take the last of the bark off", Woodshop, "Debark");
        if (splits || rives)
        {
            if (!Has(tools, CraftTools.ChoppingBlock))
                return Elsewhere(splits ? "Take it to a chopping block to split it into billets" : "Take it to a chopping block to rive it with the froe");
            return splits ? S(SplitWords(p, part), Woodshop, "Split") : S(RiveWords(p, part), Woodshop, "Rive");
        }
        // A blank meant for a part is sawn to the part's length first (the stock blanks are long: a 2.2 m ash stave, a
        // metre of hickory) - before it is bent, since the bend is in the length it keeps.
        Step? length = SawToLength(tools, p, part);
        if (length.HasValue)
            return length;
        // A part meant for a curve (a scythe's snath): steamed and bent before it seasons.
        CraftShapeTarget shape = part?.shape;
        if (shape != null && Math.Abs(shape.curvature) >= 1)
        {
            double has = 0, want = shape.curvature;
            foreach (MetalSegment s in p.segments) has += s.bend;
            // short of its curve (a bend only adds to it: one bent too far stays so)
            if (want > 0 ? want - has > CurveShort : has - want > CurveShort)
                return Has(tools, CraftTools.SteamBox)
                    ? S($"Steam it and bend it to its curve: it wants {Math.Abs(want):0}°, it has {Math.Abs(has):0}° (it springs back: bend a little past)", Woodshop, "Bend")
                    : Elsewhere($"Take it to a steam box to bend it to its curve ({Math.Abs(want):0}°)");
        }
        // Green wood shrinks loose in a fit as it dries (the finished item says so): season it first.
        if (w.moisture > CraftWoodwork.DryEnough)
            return Has(tools, CraftTools.DryingStack) ? S("It is green: season it in the drying stack before it goes into a fit", Woodshop, "Dry")
                : Elsewhere("It is green: season it in a drying stack before it goes into a fit");
        if (part == null)
            return null; // meant for nothing in particular: the rest is the woodworker's own
        return Carving(ws, tools, p, part) ?? Finishing(tools, p, part);
    }

    /// <summary>
    /// Is it worth breaking down along the grain? A round log is split (or a small one rived); meant for nothing yet,
    /// green stock is split on while the wedges take it (log, halves, quarters, billets: green wood is split before it
    /// seasons); meant for a part, while each half of it would still hold the part (with a millimetre to carve clean).
    /// </summary>
    private static bool BreaksDown(CraftPiece p, CraftComponentSpec part)
    {
        if (part == null)
            return p.wood.log || p.wood.moisture > CraftWoodwork.DryEnough && CraftWoodwork.SplitsWithWedges(p, out _);
        if (!PartSection(part, out double big, out double small))
            return p.wood.log;
        // the half an even split leaves: the larger dimension halved (less what the split takes), the other as it is
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        w /= p.segments.Count;
        t /= p.segments.Count;
        double parted = Math.Max(w, t) * 0.5 - 1, kept = Math.Min(w, t);
        const double room = 1;
        return Math.Max(parted, kept) >= big + room && Math.Min(parted, kept) >= small + room;
    }

    /// <summary>The largest section (mm, the greater and the lesser of its width and thickness) a wooden part's shape asks for anywhere along it.</summary>
    private static bool PartSection(CraftComponentSpec part, out double big, out double small)
    {
        big = small = 0;
        CraftShapeTarget shape = part?.shape;
        double mw = 0, mt = 0;
        if (shape != null && shape.profile != null && shape.profile.Count > 0)
            foreach (CraftProfilePoint q in shape.profile) { mw = Math.Max(mw, q.width); mt = Math.Max(mt, q.thickness); }
        else if (part?.wood != null)
            mw = mt = part.wood.diameterMax;
        big = Math.Max(mw, mt);
        small = Math.Min(mw, mt);
        return big > 0;
    }

    /// <summary>"240 x 119 mm": a piece's section (each dimension its mean along it).</summary>
    private static string Section(CraftPiece p)
    {
        double w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        return $"{w / p.segments.Count:0} x {t / p.segments.Count:0} mm";
    }

    /// <summary>What the next split gives: a log halves, a half quarters, smaller billets - or, meant for a part, what the part wants.</summary>
    private static string SplitWords(CraftPiece p, CraftComponentSpec part)
    {
        int splits = 0;
        foreach (CraftRecordEntry e in p.record) if (e.kind == CraftOpKind.SplitLog) splits += Math.Max(1, e.count); // a repeated entry counts its times
        string into = p.wood.log ? "Split it into billets with the wedges" : splits == 1 ? "Split the half again with the wedges, into quarters" : "Split it on with the wedges, into billets";
        if (part != null && PartSection(part, out double big, out double small))
            return $"{into}: the {part.Name.ToLowerInvariant()} wants {big:0} x {small:0} mm, it is {Section(p)}";
        return p.wood.log ? into : $"{into} - green wood is split before it seasons";
    }

    private static string RiveWords(CraftPiece p, CraftComponentSpec part) =>
        part != null && PartSection(part, out double big, out double small)
            ? $"Rive it with the froe: the {part.Name.ToLowerInvariant()} wants {big:0} x {small:0} mm, it is {Section(p)}"
            : "Rive it with the froe";

    /// <summary>
    /// A seasoned blank for a part, still wider or thicker than the part's shape somewhere (as much as the evaluator notes:
    /// <see cref="CraftEvaluator.ProfileNoted"/>, counting only what a knife can still take off): carve it to its shape at
    /// the carving bench, where the shape is dashed over it. Then a grip's slot for the tang it goes on. Null when there is
    /// nothing left for the knife or the bit.
    /// </summary>
    private static Step? Carving(CraftWorkshop ws, CraftTools tools, CraftPiece p, CraftComponentSpec part)
    {
        bool bench = Has(tools, CraftTools.Files);
        CraftShapeTarget shape = part.shape;
        if (shape != null && shape.profile != null && shape.profile.Count >= 2)
        {
            double over = CraftEvaluator.ProfileError(p, shape, true, out bool width);
            if (CraftEvaluator.ProfileFit(over) < CraftEvaluator.ProfileNoted)
                return bench ? S($"Carve it to its shape: shave its {(width ? "width" : "thickness")} down to the dashed outline (it is {Section(p)})", Carve, width ? "Width" : "Thickness")
                    : Elsewhere("Take it to a carving bench to carve it to its shape");
        }
        if (CraftWoodwork.TangSlot(ws, p, out double need, out bool measured) && CraftAssembly.Eye(p) + 0.5 < need)
            return bench ? S($"Bore the slot for the {(measured ? "blade's tang" : "tang")}: it is {CraftAssembly.Eye(p):0} mm, the tang needs {need:0} mm", Carve, "Bore")
                : Elsewhere("Take it to a carving bench to bore the slot for the tang");
        return null;
    }

    /// <summary>
    /// Carved, a part may be sanded through the grits and sealed (optional: smoother and sealed, it lasts longer) at the
    /// finishing bench - sanded with the fine paper (as the look calls it smooth as silk) and a coat on it (as the look
    /// sees one). Null once it is sealed.
    /// </summary>
    private static Step? Finishing(CraftTools tools, CraftPiece p, CraftComponentSpec part)
    {
        if (CraftWoodwork.Sealed(p.wood))
            return null;
        bool sanded = p.wood.lastGrit >= CraftWoodwork.FineGrit;
        string what = part.Name.ToLowerInvariant();
        string text = sanded ? $"Seal the {what} (optional): varnish, oil or wax keep the weather out"
            : $"Sand the {what} through the grits, coarse to fine, and seal it (optional: smooth and sealed, it lasts longer)";
        return Has(tools, CraftTools.FinishingBench) ? S(text, Woodshop, "Finish") : Elsewhere($"{text} - at a finishing bench");
    }

    /// <summary>
    /// A blank meant for a wooden part whose length is off by as much as the finished item would note
    /// (<see cref="CraftWoodwork.LengthOff"/>): too long, saw it to length - at the carving bench, where the part's
    /// length is dashed over the blank, or at the sawhorse; too short, no cut makes it longer. Null when its length will
    /// do (or it is meant for nothing in particular).
    /// </summary>
    private static Step? SawToLength(CraftTools tools, CraftPiece p, CraftComponentSpec part)
    {
        if (!CraftWoodwork.LengthOff(part, p.Length) || !CraftWoodwork.LengthWanted(part, out double min, out double max))
            return null;
        string wants = $"the {part.Name.ToLowerInvariant()} wants {min:0}-{max:0} mm, it is {p.Length:0} mm";
        if (p.Length < min)
            return Elsewhere($"Too short for what it is meant for ({wants}): use a longer blank");
        string text = $"Saw it to length ({wants})";
        return Has(tools, CraftTools.Files) ? S(text, Carve, "Saw")
            : Has(tools, CraftTools.Sawhorse) ? S(text, Woodshop, "Saw")
            : Elsewhere($"Take it to a sawhorse or a carving bench to saw it to length ({wants})");
    }

    // ------------------------------------------------------------------ an item being made
    private static Step? Assembly(CraftWorkshop ws, CraftTools tools, CraftPiece a, Func<string, bool> inspected)
    {
        CraftRules rules = ws.Rules;
        CraftDesign d = rules.Design(a.assembly.design);
        if (d == null)
            return null;
        if (a.assembly.finished)
            return inspected != null && inspected(a.id) ? Elsewhere("Take it: it is done") : S("Try it to see what it is", Assemble);
        bool jewel = d.kind == CraftDesignKind.Jewelry;
        bool sewn = false, soldered = true;
        foreach (CraftJointSpec j in d.joints)
        {
            if (j.kind == CraftJointKind.Stitch) sewn = true;
            // Jewellery is soldered before the stone goes in: the torch's heat would crack it.
            if (jewel && j.kind == CraftJointKind.Solder && CraftAssembly.JointApplies(ws, a, d, j) && !(a.assembly.Joint(j.id)?.done ?? false)) soldered = false;
        }
        // A wooden part fitted at a length the finished item would be noted for (a stock blank fitted as it came): off
        // again, sawn to length and back on, before more work goes into it.
        foreach (CraftComponentSpec c in d.components)
        {
            if (!CraftWoodwork.LengthWanted(c, out double min, out double max))
                continue;
            for (int i = 0; i < Math.Max(1, c.count); i++)
            {
                CraftPiece part = ws.Piece(a.assembly.PieceIn(c.role, i));
                if (part == null || !CraftWoodwork.LengthOff(c, part.Length))
                    continue;
                string which = c.count > 1 ? $" {i + 1} of {c.count}" : "";
                return S(part.Length > max
                    ? $"{c.Name}{which}: too long ({part.Length:0} mm, wants {min:0}-{max:0} mm) - take it off, saw it to length and fit it again"
                    : $"{c.Name}{which}: too short ({part.Length:0} mm, wants {min:0}-{max:0} mm) - take it off and fit a longer one", Assemble);
            }
        }
        // A thread no joint names is added after the joints: a cord (a pendant's), or the thread of seams sewn at this
        // tailor's table, whose needle threads from the bag and lays the item's thread in as it sews. Leather and cloth sewn
        // at the bench are sewn with the item's own thread: added with the parts.
        CraftComponentSpec later = null;
        bool tableSews = sewn && Has(tools, CraftTools.TailorTable) && AllSeamsCloth(ws, a, d);
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.optional) continue;
            // The bench's own rule (CraftAssembly.Missing): every place of a part filled, every supply added, a buckle set
            // holding as many buckles as the design takes.
            bool missing = false;
            if (c.supply)
                missing = a.assembly.Supply(c.role) < Math.Max(1, c.supplyCount);
            else
                for (int i = 0; i < Math.Max(1, c.count) && !missing; i++)
                    missing = ws.Piece(a.assembly.PieceIn(c.role, i)) == null;
            if (!missing && c.kind == CraftPieceKind.Fasteners && c.fastener == CraftFastenerKind.Buckle)
            {
                CraftPiece set = ws.Piece(a.assembly.PieceIn(c.role, 0));
                if (set != null && set.fasteners.count < Math.Max(1, c.fastenerCount))
                    return S($"Fit another set of {c.Name.ToLowerInvariant()}: it holds {set.fasteners.count} of {c.fastenerCount}", Assemble);
            }
            if (!missing) continue;
            if (c.supply && c.supplyKind == CraftSupplyKind.Thread && !Joins(d, c.role) && (!sewn || tableSews))
            {
                later ??= c;
                continue;
            }
            if (jewel && c.kind == CraftPieceKind.Gem)
            {
                if (!soldered) continue; // after the soldering
                CraftPiece setting = SettingOf(ws, a, d);
                if (setting != null && setting.Tag(CraftJewelry.SeatTag) <= 0)
                    return S("Cut the setting's seat to the stone", Jeweller, "Seat");
            }
            // a wooden part says the length it is judged by (the stock blanks are longer: sawn to it first)
            string sawn = CraftWoodwork.LengthWanted(c, out double lo, out double hi) ? $" (wood, {lo:0}-{hi:0} mm long)" : "";
            return S(c.supply ? $"Add the {c.Name.ToLowerInvariant()}" : $"Fit its parts: {c.Name.ToLowerInvariant()} first{sawn}", Assemble);
        }
        foreach (CraftJointSpec j in d.joints)
        {
            JointState s = a.assembly.Joint(j.id);
            // done - and a socket's pins, fitted, set through it (the bench's own rule: CraftAssembly.Missing)
            if (s != null && CraftAssembly.JointDone(j, s) && !CraftAssembly.PinLeft(d, j, s, (role, i) => ws.Piece(a.assembly.PieceIn(role, i)), out _, out _)) continue;
            if (!CraftAssembly.JointApplies(ws, a, d, j)) continue; // a part left off (or not yet in: named above)
            if (CraftJewelry.IsJewelJoint(j.kind))
                return S($"{j.Name}: jeweller's work", Jeweller, j.kind == CraftJointKind.Solder ? "Solder" : "Set");
            if (j.kind == CraftJointKind.Fit && s != null && s.gap < 0)
            {
                CraftComponentSpec on = d.Component(j.roleB);
                string what = on?.Name.ToLowerInvariant() ?? "part";
                return S($"The {what} is too tight to go on: take it off and open its slot (a file on metal, the rasp or the bore on wood)", Assemble);
            }
            if (j.kind == CraftJointKind.Rivet || j.kind == CraftJointKind.Buckle || j.kind == CraftJointKind.Socket)
            {
                // the set ran out before every rivet (or a socket's pin) was in: the bench wants another set fitted
                CraftComponentSpec rivets = CraftAssembly.RivetPart(d, j);
                CraftPiece set = rivets != null ? ws.Piece(a.assembly.PieceIn(rivets.role)) : null;
                int want = CraftAssembly.RivetsWanted(j, d);
                if (set != null && set.fasteners.Left <= 0 && (s?.rivets ?? 0) < want)
                    return S($"Fit another set of {rivets.Name.ToLowerInvariant()}: {s?.rivets ?? 0} of {want} are set", Assemble);
            }
            bool cloth = j.kind == CraftJointKind.Stitch && CraftTextiles.ClothSeam(ws, a, j);
            // a seam of cloth is sewn at the tailor's table where there is one (its needle threads from the bag)
            return S(WorkWords(d, j, s, cloth, CraftAssembly.PinsWanted(ws, a, d, j) > 0), cloth && Has(tools, CraftTools.TailorTable) ? Tailor : Assemble, cloth && Has(tools, CraftTools.TailorTable) ? "Sew" : "");
        }
        if (later != null)
            return S($"Add the {later.Name.ToLowerInvariant()}", Assemble);
        Step? edge = Edge(ws, tools, a, d);
        if (edge.HasValue)
            return edge;
        if (jewel && a.assembly.finish <= 0)
            return S("Buff it, steam it, inspect and finish it", Jeweller, "Finish");
        return S("Finish it", Assemble);
    }

    /// <summary>Does a joint of the design join this part (a bowstring is strung; a sewing thread joins nothing by name)?</summary>
    private static bool Joins(CraftDesign d, string role)
    {
        foreach (CraftJointSpec j in d.joints)
            if (j.roleA == role || j.roleB == role) return true;
        return false;
    }

    /// <summary>Is every seam of the item a seam of cloth (the tailor's needle sews it)?</summary>
    private static bool AllSeamsCloth(CraftWorkshop ws, CraftPiece a, CraftDesign d)
    {
        foreach (CraftJointSpec j in d.joints)
            if (j.kind == CraftJointKind.Stitch && !CraftTextiles.ClothSeam(ws, a, j)) return false;
        return true;
    }

    /// <summary>
    /// The work a joint still wants, in the bench's words, with how far it has come: "Crown seam: punch and stitch it (12
    /// of 20 stitches)". <paramref name="pinsFitted"/>: a socket's pins are fitted (driven home, then pinned).
    /// </summary>
    private static string WorkWords(CraftDesign d, CraftJointSpec j, JointState s, bool cloth, bool pinsFitted = false)
    {
        string name = j.Name;
        switch (j.kind)
        {
            case CraftJointKind.Rivet:
            {
                bool studs = CraftAssembly.RivetPart(d, j)?.fastener == CraftFastenerKind.Stud;
                return $"{name}: set the {(studs ? "studs" : "rivets")} ({s?.rivets ?? 0} of {CraftAssembly.RivetsWanted(j, d)})";
            }
            case CraftJointKind.Stitch:
                return $"{name}: {(cloth ? "sew it" : "punch and stitch it")} ({s?.stitches ?? 0} of {Math.Max(1, j.count)} stitches)";
            case CraftJointKind.Buckle:
            {
                CraftComponentSpec buckles = d.Component(j.roleB)?.kind == CraftPieceKind.Fasteners ? d.Component(j.roleB) : d.Component(j.roleA);
                string what = buckles?.Name.ToLowerInvariant() ?? "buckles";
                return CraftAssembly.RivetPart(d, j) != null ? $"{name}: rivet the {what} on ({s?.rivets ?? 0} of {Math.Max(1, j.count)})" : $"{name}: sew the {what} on";
            }
            case CraftJointKind.Wrap:
                foreach (string role in new[] { j.roleA, j.roleB })
                {
                    CraftComponentSpec c = string.IsNullOrEmpty(role) ? null : d.Component(role);
                    if (c != null && c.supply && c.supplyKind == CraftSupplyKind.Thread)
                        return $"{name}: wind the {c.Name.ToLowerInvariant()} on";
                }
                return $"{name}: wind it on ({s?.turns ?? 0} of {Math.Max(1, j.count)} turns)";
            case CraftJointKind.Wedge:
                return $"{name}: drive the wedge home";
            case CraftJointKind.Socket:
            {
                // driven home first; its pins (when they are fitted) through socket and shaft as they then sit
                CraftComponentSpec pin = CraftAssembly.RivetPart(d, j);
                bool pinned = pin != null && pinsFitted;
                string what = pin != null && CraftAssembly.RivetsWanted(j, d) > 1 ? $"rivet it ({s?.rivets ?? 0} of {CraftAssembly.RivetsWanted(j, d)})" : "pin it";
                return (s?.wedge ?? 0) < CraftAssembly.WedgeGoodLow || !pinned ? $"{name}: drive it home on the shaft{(pinned ? ", then " + what : "")}" : $"{name}: {what}";
            }
            case CraftJointKind.Peen:
                return $"{name}: peen the tang's end over it";
            default:
                return $"{name}: make it";
        }
    }

    /// <summary>The setting fitted to a piece of jewellery (its Setting part), or null.</summary>
    private static CraftPiece SettingOf(CraftWorkshop ws, CraftPiece a, CraftDesign d)
    {
        foreach (CraftComponentSpec c in d.components)
            if (c.function == CraftComponentFunction.Setting)
            {
                CraftPiece p = ws.Piece(a.assembly.PieceIn(c.role));
                if (p != null) return p;
            }
        return null;
    }

    /// <summary>
    /// The edge of an item whose main part should have one (a blade, an axe's head): still to grind (no edge ground on
    /// it yet - <see cref="CraftSurface.HasEdge(MetalSegment)"/>, the stone's own test) or to sharpen (not keen), done in
    /// place on the fitted part - where the station has the wheel or files, or the whetstone. Null when it is keen, or
    /// nothing here can make it so.
    /// </summary>
    private static Step? Edge(CraftWorkshop ws, CraftTools tools, CraftPiece a, CraftDesign d)
    {
        CraftComponentSpec main = d.MainComponent;
        CraftShapeTarget shape = main?.shape;
        if (shape == null || shape.edge == CraftEdgeKind.None)
            return null;
        bool wheel = Has(tools, CraftTools.Grinder) || Has(tools, CraftTools.Files) && (Has(tools, CraftTools.Anvil) || Has(tools, CraftTools.Forge));
        bool stone = Has(tools, CraftTools.Whetstone);
        // each of them (three throwing knives): the first one not yet keen
        for (int k = 0; k < Math.Max(1, main.count); k++)
        {
            CraftPiece part = ws.Piece(a.assembly.PieceIn(main.role, k));
            if (part == null || part.kind != CraftPieceKind.Metal || part.segments.Count == 0)
                continue;
            double keen = 0;
            int n = 0;
            bool edged = false;
            for (int i = 0; i < part.segments.Count; i++)
            {
                double t = part.PositionOf(i);
                if (t < shape.edgeFrom || t > shape.edgeTo) continue;
                keen += CraftEvaluator.Sharpness(part.segments[i]);
                edged |= CraftSurface.HasEdge(part.segments[i]);
                n++;
            }
            if (n == 0 || keen / n >= CraftSurface.Keen) // keen, as the stone and the look say it
                continue;
            string which = main.count > 1 ? $" ({main.Name.ToLowerInvariant()} {k + 1} of {main.count})" : "";
            if (!edged && wheel)
                return S((stone ? "Grind and sharpen its edge: grind the bevels first" : "Grind its edge: the bevels") + which, Grind, "", part.id);
            if (edged && stone)
                return S("Grind and sharpen its edge: hone it on the whetstone" + which, Sharpen, "", part.id);
        }
        return null;
    }
}
