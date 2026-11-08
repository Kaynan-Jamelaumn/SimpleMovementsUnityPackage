using System;
using System.Collections.Generic;

/// <summary>
/// The furnace and the crucible.
/// <list type="bullet">
/// <item><b>Roasting</b>: a sulfide ore held at its roasting heat in an oxidising fire loses its sulfur (unroasted, its
/// copper comes out as a brittle matte).</item>
/// <item><b>Reduction</b>: in a hot, reducing furnace the mineral gives up its metal. What melts at the furnace's
/// heat (copper, tin, lead, silver, gold) runs to the bottom as liquid; iron does not melt - it grows into a spongy
/// bloom full of slag. A metal that boils below the heat its ore gives it up at (zinc from calamine) goes up the shaft
/// as fumes: zinc is won only by cementation in a covered crucible. The rock and the bound silica become slag; silica
/// takes iron with it unless there is lime (limestone flux, or a limestone rock). Phosphorus mostly goes into the
/// iron; sulfur too, from unroasted ore or a coal fire. A hotter furnace with more charcoal puts more carbon into the
/// bloom - too hot and it melts into cast iron. Slag that is not tapped chokes the draft.</item>
/// <item><b>Metal in the furnace</b> (a matte or lead carrying silver, cast and brought back): it roasts in an
/// oxidising fire and melts down into the pool; its sulfur joins the matte, which an oxidising blast roasts away.</item>
/// <item><b>Cupellation</b>: lead with silver in it, on a bone-ash cupel in a strong, hot draft: the lead soaks into
/// the cupel as litharge and the silver stays - a bead too small to pour is lifted off with the tongs.</item>
/// <item><b>The crucible</b>: metal put in melts at its own solidus; the melt follows the heat of the furnace (or
/// the forge), oxidises into dross (less under a flux cover), boils away what boils (zinc - slowly once it is
/// dissolved in copper), takes up gas when held too hot and lets it go again held just above its liquidus or frozen;
/// additions are mixed in by time and stirring. Only ore whose metal melts at a crucible's heat (or boils off into a
/// copper melt: cementation) goes in; its rock floats as slag. A melt that cannot be poured is let freeze and knocked
/// out as a cake.</item>
/// <item><b>Pouring</b>: the superheat, the pour's speed and steadiness and its interruptions decide whether the mould
/// fills, whether there are cold shuts, porosity or inclusions; the casting's composition is the melt's, and it never
/// holds more metal than the pour brought.</item>
/// </list>
/// </summary>
public static class CraftSmelting
{
    /// <summary>The least metal (g) at the bottom of the furnace that can be run into a mould; less is lifted off as a bead.</summary>
    public const double PourMinimum = 20;
    /// <summary>The least metal (g) at the bottom of the furnace worth lifting out as a bead.</summary>
    public const double BeadMinimum = 0.05;
    /// <summary>The least iron (g) a bloom must hold to be gripped with the tongs and pulled.</summary>
    public const double BloomMinimum = 50;
    /// <summary>A crucible melts out the metal of an ore only when that metal melts below this (°C): copper, tin, lead, silver and gold ores, not iron.</summary>
    public const double CrucibleOreHeat = 1250;
    /// <summary>A fire more oxidising than this (<see cref="CraftFireState.Oxidizing"/>) roasts sulfide ore and metal pieces; above <see cref="CupelAir"/> it drives lead into a cupel and burns a matte's sulfur.</summary>
    public const double RoastAir = 0.35, CupelAir = 0.45;
    /// <summary>The slag is too stiff to run from the tap hole below this (°C).</summary>
    public const double TapHeat = 1120;

    // ------------------------------------------------------------------ the furnace
    /// <summary>A metal (or a metal of legend), not an impurity or an alloying addition.</summary>
    private static bool IsMetal(CraftElement e) => e != null && (e.kind == CraftElementKind.Metal || e.kind == CraftElementKind.Fantasy);

    /// <summary>
    /// Does this metal go up the shaft as fumes when its ore is smelted at <paramref name="t"/>? It does when it boils
    /// at that heat - or below the heat its ore gives it up at at all (zinc from calamine: it is reduced as a vapour).
    /// </summary>
    public static bool Fumes(CraftElement e, double t, CraftOreDefinition o) => IsMetal(e) && (t > e.boilingPoint || o != null && e.boilingPoint <= o.reductionTemperature);

    public static void StepOre(CraftWorkshop ws, CraftPiece p, double dt)
    {
        CraftRules rules = ws.Rules;
        CraftOreDefinition o = rules.Ore(p.ore.ore);
        if (o == null)
            return;
        CraftFurnaceState f = ws.Furnace;
        double furnaceT = f.fire.temperature;
        OreState s = p.ore;
        s.temperature += (furnaceT - s.temperature) * (1 - Math.Exp(-dt / 20.0));
        double t = s.temperature, ox = f.fire.Oxidizing;
        if (t > 100)
            s.moisture = Math.Max(0, s.moisture - dt * 0.01);
        // Roasting: an oxidising fire at the roasting heat drives the sulfur off.
        if (s.Bound("sulfur") > 0 && t > o.roastTemperature - 150 && t < o.roastTemperature + 350 && ox > RoastAir)
            s.roasted = Math.Min(1, s.roasted + dt * 0.006 * ox * (1 + (t - o.roastTemperature + 150) / 250));
        // Reduction.
        double mineral = s.MineralMass;
        if (mineral <= 1e-6 || t < o.reductionTemperature - 50)
        {
            if (mineral <= 1e-6) Consumed(ws, p);
            return;
        }
        double reducing = 1 - ox;
        double rate = rules.Settings.reductionRate * o.reductionRate * CraftMath.Smooth(o.reductionTemperature - 50, o.reductionTemperature + 150, t) * Math.Pow(Math.Max(0, reducing), 1.5);
        if (o.native)
            rate = rules.Settings.reductionRate * 3 * CraftMath.Smooth(o.reductionTemperature - 50, o.reductionTemperature + 100, t);
        if (!string.IsNullOrEmpty(o.needsFlux))
        {
            double have = FluxOf(f, o.needsFlux);
            if (have <= 1e-6)
                rate = 0;
        }
        double dm = Math.Min(mineral, rate * dt);
        if (dm <= 0)
            return;
        double share = dm / mineral;
        if (!string.IsNullOrEmpty(o.needsFlux))
            UseFlux(f, o.needsFlux, dm * 0.05);
        // What the mineral gives.
        double silica = 0, boundSilica = 0, gangueSlag = 0, lime = 0;
        foreach (OreFragment fr in s.fragments)
        {
            double fm = fr.mass * fr.mineral * share, fg = fr.mass * fr.gangue * share * 0.9, fd = fr.mass * fr.dirt * share;
            double rest = fr.mass - fm - fg - fd;
            if (o.gangue == CraftGangue.Limestone) lime += fg * 0.56;
            else silica += fg * (o.gangue == CraftGangue.Clay ? 0.6 : 0.95) * rules.Settings.gangueReactivity;
            gangueSlag += fg + fd;
            double m = Math.Max(1e-9, rest);
            double mineralLeft = fr.mass * fr.mineral - fm, gangueLeft = fr.mass * fr.gangue - fg, dirtLeft = fr.mass * fr.dirt - fd;
            fr.mass = m;
            fr.mineral = mineralLeft / m;
            fr.gangue = gangueLeft / m;
            fr.dirt = dirtLeft / m;
        }
        bool matte = o.sulfide && s.roasted < RoastedEnough;
        // Where the metal goes: liquid to the bottom when the ore's main metal melts at this heat, else into the bloom.
        // What does not melt in a liquid smelt (iron from a copper ore) mostly goes into the slag. A metal that boils
        // (zinc) goes up the shaft as fumes, so the main metal is the first one that stays: calamine leaves only its
        // little iron, in the bloom.
        CraftElement primary = null;
        foreach (CraftAmount b in s.bound)
        {
            CraftElement e = rules.Element(b.element);
            if (IsMetal(e) && !Fumes(e, furnaceT, o)) { primary = e; break; }
        }
        bool liquid = primary != null && MeltsAt(primary, furnaceT);
        List<CraftAmount> product = liquid ? f.pool : f.bloom;
        CraftElement fumed = null;
        foreach (CraftAmount b in s.bound)
        {
            double g = dm * b.grams;
            if (g <= 0) continue;
            CraftElement e = rules.Element(b.element);
            if (e == null) continue;
            if (Fumes(e, furnaceT, o))
            {
                f.fumes += g;
                fumed = e;
                continue;
            }
            if (b.element == "silica")
            {
                silica += g;
                boundSilica += g;
                continue;
            }
            if (b.element == "sulfur")
            {
                double sulfur = g * (1 - s.roasted);
                if (matte) f.matte += sulfur * 0.9;
                else AddTo(product, "sulfur", sulfur * 0.3); // most of it leaves as gas
                continue;
            }
            if (b.element == "phosphorus")
            {
                double limeShare = CraftMath.Clamp01((f.flux * 0.56 + lime) / Math.Max(1, silica * 2 + 1));
                double toMetal = g * 0.75 * (1 - 0.6 * limeShare);
                AddTo(product, "phosphorus", toMetal);
                f.slag += g - toMetal;
                continue;
            }
            double slagged = g * e.slagAffinity * 0.25 * ox;
            if (liquid && e != primary && (e.kind == CraftElementKind.Metal || e.kind == CraftElementKind.Fantasy) && !MeltsAt(e, furnaceT))
                slagged = Math.Max(slagged, g * 0.8);
            f.slag += slagged * 1.3;
            AddTo(product, b.element, g - slagged);
        }
        // Slag: silica takes iron with it unless there is lime to take its place.
        double limeNeed = silica * 1.0;
        double limeHave = f.flux * 0.56 + lime;
        double cover = limeNeed > 1e-9 ? CraftMath.Clamp01(limeHave / limeNeed) : 1;
        f.flux = Math.Max(0, f.flux - Math.Min(f.flux, Math.Max(0, limeNeed - lime) / 0.56));
        int iron = rules.SteelBase;
        if (iron >= 0)
        {
            string ironId = rules.Elements[iron].id;
            double ironLost = Math.Min(Grams(f.bloom, ironId), silica * rules.Settings.ironPerSilica * (1 - cover));
            AddTo(f.bloom, ironId, -ironLost);
            f.slag += ironLost * 1.29;
            // Some of the slag stays caught in the bloom (less when the furnace runs hot and the slag is fluid).
            double newIron = dm * s.Bound(ironId);
            double trapped = newIron * CraftMath.Clamp(0.4 - (furnaceT - 1150) / 500, 0.05, 0.4) * (0.3 + 0.7 * f.slag / (f.slag + 400));
            f.bloomSlag += trapped;
        }
        f.slag += gangueSlag + boundSilica + cover * limeNeed;
        if (fumed != null && ws.Step % 20 == 0)
            ws.Raise(CraftEventKind.Note, p.id, FumesId + fumed.id, $"White fumes and green-white flames pour from the furnace's mouth: the {fumed.Name.ToLowerInvariant()} boils away.");
        if (s.MineralMass <= 1e-6)
            Consumed(ws, p);
    }

    /// <summary>The start of the id of the note told while a metal boils off as fumes (the furnace's mouth, its pool, a melt): the element's id follows ("fumes:zinc").</summary>
    public const string FumesId = "fumes:";

    /// <summary>A sulfide roasted at least this far smelts clean; less and its copper comes out as a matte (and its look says "partly roasted").</summary>
    public const double RoastedEnough = 0.7;

    /// <summary>Ore out of the furnace and the crucible (lifted out hot with the tongs) cools toward the air's heat.</summary>
    public static void CoolOre(CraftWorkshop ws, CraftPiece p, double dt)
    {
        if (p?.ore == null || Math.Abs(p.ore.temperature - ws.Ambient) < 0.01)
            return;
        p.ore.temperature += (ws.Ambient - p.ore.temperature) * (1 - Math.Exp(-dt / 60.0));
    }

    private static bool MeltsAt(CraftElement e, double t) => e.kind != CraftElementKind.Alloying && t >= e.meltingPoint - 30;

    private static void Consumed(CraftWorkshop ws, CraftPiece p)
    {
        ws.Furnace.slag += p.ore.Mass;
        ws.Destroy(p, "The charge has gone down into the furnace.");
        ws.Raise(CraftEventKind.Smelted, p.id, p.ore.ore, "");
    }

    public static void StepFurnace(CraftWorkshop ws, double dt)
    {
        CraftRules rules = ws.Rules;
        CraftFurnaceState f = ws.Furnace;
        double t = f.fire.temperature, ox = f.fire.Oxidizing;
        // Untapped slag (and fines in the charge) choke the draft.
        double choke = Choke(ws);
        if (choke > 0)
            f.fire.level *= Math.Exp(-dt * 0.03 * choke);

        // The bloom takes up carbon: more in a hotter furnace with plenty of charcoal; too much and it melts.
        int carbon = rules.SteelCarbon, iron = rules.SteelBase;
        double bloom = f.BloomMass;
        if (bloom > 1 && carbon >= 0 && iron >= 0 && t > 1050)
        {
            string c = rules.Elements[carbon].id;
            double cNow = Grams(f.bloom, c) / bloom;
            double target = CraftMath.Clamp01((t - 1100) / 300) * 0.015 * (1 - ox) * (0.5 + f.fire.carburizing) + (t > 1300 ? (t - 1300) / 100 * 0.01 : 0);
            double next = cNow + (target - cNow) * dt / 150;
            AddTo(f.bloom, c, (next - cNow) * bloom);
            f.bloomSpread = Math.Min(1, f.bloomSpread + dt * 0.0008 * Math.Abs(t - 1200) / 100);
            // Carbon melts iron lower: a bloom that takes up too much runs down as cast iron.
            if (ws.Step % 10 == 0)
                f.bloomSolidus = CraftMetallurgy.Analyze(rules, Fractions(rules, f.bloom)).Solidus;
            if (f.bloomSolidus > 0 && t > f.bloomSolidus)
            {
                foreach (CraftAmount a in f.bloom) AddTo(f.pool, a.element, a.grams);
                f.slag += f.bloomSlag;
                f.bloom.Clear();
                f.bloomSlag = 0;
                ws.Raise(CraftEventKind.Melted, "", "bloom", "The bloom runs down into liquid iron.");
            }
        }
        // Cupellation: lead oxidises into the cupel, noble metals stay.
        double pool = f.PoolMass;
        int lead = rules.IndexOf("lead");
        if (f.cupel > 0 && pool > 0 && lead >= 0 && ox > CupelAir && t > 900)
        {
            double pb = Grams(f.pool, "lead");
            if (pb > 0)
            {
                double take = Math.Min(pb, dt * 3 * ox * Math.Min(1, f.cupel / 100));
                AddTo(f.pool, "lead", -take);
                f.cupel = Math.Max(0, f.cupel - take * 0.6);
                foreach (CraftAmount a in f.pool)
                {
                    CraftElement e = rules.Element(a.element);
                    if (e != null && a.element != "lead" && e.slagAffinity > 0.3)
                        a.grams = Math.Max(0, a.grams - a.grams * dt * 0.002 * e.slagAffinity * ox);
                }
                if (Grams(f.pool, "lead") <= 0.01 * f.PoolMass && pb > 0.01 * pool)
                    ws.Raise(CraftEventKind.Note, "", "blick", "The bead flashes bright as the last of the lead goes: pure metal shines on the cupel.");
            }
        }
        // Metal pieces in the furnace (a matte or lead carrying silver brought back, metal to melt down): a matte loses
        // its sulfur in an oxidising fire, and the piece melts into the pool - its sulfur into the matte, its slag into
        // the furnace's slag. A bloom put back to soak melts only if it took up so much carbon that it melts like cast iron.
        foreach (CraftPiece p in new List<CraftPiece>(ws.Pieces))
        {
            if (p.kind != CraftPieceKind.Metal || p.location != CraftLocation.Furnace) continue;
            double sulfur = p.Grams("sulfur");
            double total = 0;
            foreach (CraftAmount a in p.composition) total += a.grams;
            if (sulfur > 0 && ox > RoastAir && p.MaxTemperature > 700 && total > 0)
                p.SetGrams("sulfur", sulfur * Math.Exp(-dt * 0.01 * ox));
            if (p.MaxTemperature >= p.Profile(rules).Solidus)
            {
                double mass = p.Mass(rules), slag = 0;
                foreach (MetalSegment seg in p.segments) slag += seg.slag;
                double metal = Math.Max(0, mass - slag);
                total = 0;
                foreach (CraftAmount a in p.composition) total += a.grams;
                foreach (CraftAmount a in p.composition)
                {
                    if (total <= 0 || a.grams <= 0) continue;
                    double g = a.grams / total * metal;
                    if (a.element == "sulfur") f.matte += g;
                    else AddTo(f.pool, a.element, g);
                }
                f.slag += slag;
                ws.Destroy(p, "It melts into the furnace.");
                ws.Raise(CraftEventKind.Melted, p.id, "furnace", "It slumps and runs down into the bottom of the furnace.");
            }
        }
        // What boils at the furnace's heat leaves the pool as fumes (zinc in brass melted down here).
        double poolMetal = f.PoolMass;
        if (poolMetal > 1e-9)
        {
            CraftElement boiled = null;
            CraftAmount main = null;
            foreach (CraftAmount a in f.pool)
                if (!CraftFurnaceState.IsFlux(a) && (main == null || a.grams > main.grams)) main = a;
            foreach (CraftAmount a in f.pool)
            {
                if (CraftFurnaceState.IsFlux(a)) continue;
                CraftElement e = rules.Element(a.element);
                double boil = e != null ? Boil(e, a.grams, a.grams / poolMetal, a == main, t, 0) * dt : 0;
                if (boil <= 0) continue;
                boil = Math.Min(a.grams, boil);
                a.grams -= boil;
                f.fumes += boil;
                boiled = e;
            }
            if (boiled != null && ws.Step % 20 == 0)
                ws.Raise(CraftEventKind.Note, "", FumesId + boiled.id, $"White fumes and green-white flames rise from the bottom of the furnace: the {boiled.Name.ToLowerInvariant()} boils away.");
        }
        // The matte loses sulfur in an oxidising blast too - as a piece of it does: a big matte roasts in minutes, not hours.
        if (f.matte > 0 && ox > CupelAir && t > 900)
            f.matte = Math.Max(0, f.matte * Math.Exp(-dt * 0.01 * ox) - dt * 0.02 * ox);
    }

    /// <summary>The share of the ore charged into the furnace (by mass) that is fines (stones under <see cref="CraftOres.FineSize"/>): it packs the charge and chokes the draft.</summary>
    public static double FinesInCharge(CraftWorkshop ws)
    {
        double fines = 0, charge = 0;
        foreach (CraftPiece p in ws.Pieces)
            if (p.kind == CraftPieceKind.Ore && p.location == CraftLocation.Furnace)
                foreach (OreFragment fr in p.ore.fragments)
                {
                    charge += fr.mass;
                    if (fr.size < CraftOres.FineSize) fines += fr.mass;
                }
        return charge > 0 ? fines / charge : 0;
    }

    /// <summary>
    /// How much the furnace's draft is choked (0 it runs free): by slag left untapped past 60 % of what the furnace holds,
    /// and by fine ore packing the charge (all fines choke it as much as slag at 120 %). The fire dies down under it.
    /// </summary>
    public static double Choke(CraftWorkshop ws) => Math.Max(0, ws.Furnace.slag / Math.Max(1, ws.Rules.Settings.slagCapacity) - 0.6) + FinesInCharge(ws) * 0.6;

    /// <summary>
    /// Grams a second of an element that boil off a melt at <paramref name="t"/>: pure (or the melt's base metal) it
    /// boils freely; dissolved in another metal much more slowly (<paramref name="share"/> × 0.2 - a covered brass held
    /// just above its liquidus loses about a percent of its zinc a minute). A cover (0-1) keeps most of it in.
    /// </summary>
    public static double Boil(CraftElement e, double grams, double share, bool isBase, double t, double cover)
    {
        if (e == null || grams <= 0 || t <= e.boilingPoint)
            return 0;
        double solution = isBase ? 1 : Math.Min(1, CraftMath.Clamp01(share) * 0.2);
        return grams * 0.01 * (t - e.boilingPoint) / 100 * solution * (1 - 0.7 * CraftMath.Clamp01(cover));
    }

    /// <summary>What the metal at the bottom of the furnace is (the fluxes kept there are not metal).</summary>
    public static CraftAlloyProfile PoolProfile(CraftRules rules, CraftFurnaceState f) => CraftMetallurgy.Analyze(rules, Fractions(rules, f.pool));

    /// <summary>Is the metal at the bottom of the furnace liquid (the furnace above its liquidus)?</summary>
    public static bool PoolLiquid(CraftRules rules, CraftFurnaceState f) => f.PoolMass > 1e-6 && f.fire.temperature >= PoolProfile(rules, f).Liquidus;

    public static double FluxOf(CraftFurnaceState f, string id) => Grams(f.pool, "#flux:" + id);

    private static void UseFlux(CraftFurnaceState f, string id, double grams) => AddTo(f.pool, "#flux:" + id, -grams);

    /// <summary>Taps the liquid slag out (<paramref name="thoroughness"/> 0-1).</summary>
    public static void TapSlag(CraftWorkshop ws, double thoroughness)
    {
        CraftFurnaceState f = ws.Furnace;
        double out_ = f.slag * 0.85 * CraftMath.Clamp01(thoroughness);
        f.slag -= out_;
        // The matte is heavier than the slag and lies under it: tapping the slag leaves it (only roasting takes its sulfur).
        ws.Raise(CraftEventKind.SlagTapped, "", "", "Glowing slag runs out of the tap hole.", (float)out_);
    }

    // ------------------------------------------------------------------ the crucible
    public static void StepCrucible(CraftWorkshop ws, double dt)
    {
        CraftRules rules = ws.Rules;
        CraftSimulationSettings s = rules.Settings;
        CraftCrucibleState c = ws.Crucible;
        // The crucible sits in whichever fire is hotter (the furnace's or the forge's).
        double source = Math.Max(ws.Has(CraftTools.Furnace) ? ws.Furnace.fire.temperature * 0.96 : ws.Ambient, ws.Has(CraftTools.Forge) ? ws.Forge.temperature * 0.92 : ws.Ambient);
        source = Math.Max(source, ws.Ambient);
        c.temperature += (source - c.temperature) * (1 - Math.Exp(-dt / s.crucibleLag));
        // Ore in it gives up its metal into the melt (cementation: copper drinks zinc from calamine under a cover).
        foreach (CraftPiece p in new List<CraftPiece>(ws.Pieces))
            if (p.location == CraftLocation.Melt && p.kind == CraftPieceKind.Ore)
                Cement(ws, p, dt);
        // Pieces in it melt at their own solidus and join the melt.
        foreach (CraftPiece p in new List<CraftPiece>(ws.Pieces))
        {
            if (p.location != CraftLocation.Melt) continue;
            if (p.kind != CraftPieceKind.Metal && p.kind != CraftPieceKind.Fasteners) continue;
            if (p.MaxTemperature < p.Profile(rules).Solidus) continue;
            double mass = p.Mass(rules), slag = 0, total = 0;
            foreach (MetalSegment seg in p.segments) slag += seg.slag;
            foreach (CraftAmount a in p.composition) total += a.grams;
            double metal = Math.Max(0, mass - slag);
            double before = c.Mass;
            double unlike = before > 1e-9 ? Difference(Fractions(rules, c.melt), Fractions(rules, p.composition)) : 0;
            foreach (CraftAmount a in p.composition)
                if (total > 0) AddTo(c.melt, a.element, a.grams / total * metal);
            c.dross += slag;
            c.mixed = MixedAfter(c.mixed, before, metal, unlike);
            ws.Destroy(p, "It melts into the crucible.");
            ws.Raise(CraftEventKind.Melted, p.id, "crucible", "It slumps and melts into the crucible.");
        }
        double m = c.Mass;
        if (m <= 1e-6)
        {
            c.gas = Math.Max(0, c.gas - dt * 0.01);
            return;
        }
        CraftAlloyProfile prof = MeltProfile(rules, c);
        double superheat = c.temperature - prof.Liquidus;
        if (superheat < 0)
        {
            // A melt that has frozen lets its gas go as it sets: heated again, it starts clean.
            if (c.temperature < prof.Solidus)
                c.gas *= Math.Exp(-dt / GasOutFrozen);
            return;
        }
        // Oxidation into dross, boiling off (a metal dissolved in another boils off slowly), gas, mixing.
        double cover = CraftMath.Clamp01(c.cover);
        CraftAmount main = null;
        foreach (CraftAmount a in c.melt)
            if (main == null || a.grams > main.grams) main = a;
        foreach (CraftAmount a in c.melt)
        {
            CraftElement e = rules.Element(a.element);
            if (e == null || a.grams <= 0) continue;
            double ox = a.grams * e.slagAffinity * 0.0004 * (1 - 0.8 * cover) * (1 + superheat / 200) * dt;
            double boil = Boil(e, a.grams, a.grams / m, a == main, c.temperature, cover) * dt;
            if (boil > 0.05 * dt && ws.Step % 20 == 0)
                ws.Raise(CraftEventKind.Note, "", FumesId + e.id, $"White fumes and green-white flames rise from the melt: the {e.Name.ToLowerInvariant()} is burning off.");
            a.grams = Math.Max(0, a.grams - ox - boil);
            c.dross += ox * 1.25;
            c.lost += boil;
        }
        c.gas = CraftMath.Clamp01(c.gas + dt * CrucibleGasChange(ws, superheat));
        c.mixed = Math.Min(1, c.mixed + dt * 0.004);
        c.cover = Math.Max(0, c.cover - dt * 0.0005);
    }

    /// <summary>
    /// How well mixed a melt of <paramref name="before"/> g (mixed <paramref name="mixed"/>, 0-1) is once
    /// <paramref name="added"/> g of metal melts into it, the two as different as <paramref name="difference"/> (0 the same
    /// metal - 1 nothing in common, <see cref="Difference"/>): the lesser of the two runs through the greater in streaks
    /// until time and the rod mix them, and what was not mixed yet is spread through more metal. Into an empty crucible,
    /// or a piece of the melt's own metal, it stays one even melt.
    /// </summary>
    public static double MixedAfter(double mixed, double before, double added, double difference)
    {
        before = Math.Max(0, before);
        added = Math.Max(0, added);
        if (before + added <= 1e-9)
            return 1;
        double streaks = (1 - CraftMath.Clamp01(mixed)) * before + CraftMath.Clamp01(difference) * Math.Min(before, added);
        return CraftMath.Clamp01(1 - streaks / (before + added));
    }

    /// <summary>How different two compositions (element shares, as <see cref="Fractions"/> gives them) are: 0 the same - 1 nothing in common.</summary>
    public static double Difference(double[] a, double[] b)
    {
        if (a == null || b == null)
            return 0;
        double d = 0;
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++) d += Math.Abs(a[i] - b[i]);
        return CraftMath.Clamp01(d * 0.5);
    }

    /// <summary>Seconds over which a frozen melt lets its dissolved gas go.</summary>
    public const double GasOutFrozen = 20;
    /// <summary>The superheat (°C over the liquidus) above which a melt takes up gas faster than it lets it go (without a cover).</summary>
    public const double GasOnset = 145;

    /// <summary>
    /// How fast a liquid melt's dissolved gas changes (per second): held far too hot it takes gas up (less under a cover);
    /// held just above its liquidus it lets it go - faster under a cover.
    /// </summary>
    public static double GasChange(double superheat, double cover)
    {
        cover = CraftMath.Clamp01(cover);
        double gain = 0.002 * Math.Max(0, superheat - 120) / 100 * (1 - 0.5 * cover);
        double loss = 0.0005 + 0.004 * (1 - CraftMath.Smooth(20, 120, superheat)) * (0.5 + 0.5 * cover);
        return gain - loss;
    }

    /// <summary>
    /// Does solid metal still lie in the crucible's melt - a piece put in that has not melted yet (the copper, while the
    /// tin put in with it has long run)? It chills the melt round it: the melt takes up no gas until the last of it has
    /// melted (<see cref="CrucibleGasChange"/>), so a charge melted together casts as sound as its alloy poured from bars.
    /// </summary>
    public static bool ChargeUnmelted(CraftWorkshop ws)
    {
        if (ws == null)
            return false;
        foreach (CraftPiece p in ws.Pieces)
            if (p.location == CraftLocation.Melt && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Fasteners))
                return true;
        return false;
    }

    /// <summary>
    /// How fast the crucible's melt takes up gas (+) or lets it go (-), per second, at <paramref name="superheat"/>:
    /// <see cref="GasChange"/> at its cover - but none taken up while a solid charge still lies in it
    /// (<see cref="ChargeUnmelted"/>).
    /// </summary>
    public static double CrucibleGasChange(CraftWorkshop ws, double superheat)
    {
        double change = GasChange(superheat, ws != null ? ws.Crucible.cover : 0);
        return change > 0 && ChargeUnmelted(ws) ? 0 : change;
    }

    /// <summary>
    /// An ore in the crucible: once hot enough it reduces, its metal going into the melt. What boils at the crucible's
    /// heat (zinc) is caught by a copper melt under a cover - else it is lost as fumes. The rock, and the impurities the
    /// mineral gives, float on the melt as slag (not the oxide dross: skimming it takes no metal with it); of a roasted
    /// sulfide's last sulfur a little goes into the metal, the rest leaves as gas, as in the furnace.
    /// </summary>
    private static void Cement(CraftWorkshop ws, CraftPiece p, double dt)
    {
        CraftRules rules = ws.Rules;
        CraftCrucibleState c = ws.Crucible;
        CraftOreDefinition o = rules.Ore(p.ore.ore);
        if (o == null)
            return;
        p.ore.temperature += (c.temperature - p.ore.temperature) * (1 - Math.Exp(-dt / 15.0));
        double mineral = p.ore.MineralMass;
        if (mineral <= 1e-6)
        {
            c.slag += p.ore.Mass;
            ws.Destroy(p, "The ore is spent: its rock floats on the melt as slag.");
            return;
        }
        if (p.ore.temperature < o.reductionTemperature - 50)
            return;
        double dm = Math.Min(mineral, rules.Settings.reductionRate * 0.5 * o.reductionRate * CraftMath.Smooth(o.reductionTemperature - 50, o.reductionTemperature + 150, p.ore.temperature) * dt);
        double share = dm / mineral;
        double copper = c.Mass > 0 ? Grams(c.melt, Copper) / c.Mass : 0;
        foreach (CraftAmount b in p.ore.bound)
        {
            double g = dm * b.grams;
            CraftElement e = rules.Element(b.element);
            if (e == null || g <= 0) continue;
            if (b.element == "sulfur")
            {
                AddTo(c.melt, "sulfur", g * (1 - p.ore.roasted) * 0.3); // what roasting left: most of it leaves as gas
                continue;
            }
            if (e.kind == CraftElementKind.Impurity && b.element != "phosphorus")
            {
                c.slag += g;
                continue;
            }
            if (c.temperature > e.boilingPoint)
            {
                double caught = copper > 0.5 ? 0.4 + 0.6 * CraftMath.Clamp01(c.cover) : 0;
                AddTo(c.melt, b.element, g * caught);
                c.lost += g * (1 - caught);
                continue;
            }
            AddTo(c.melt, b.element, g);
        }
        // The mineral reduced is accounted for by the metal and impurities it gave (its oxygen leaves as gas).
        foreach (OreFragment fr in p.ore.fragments)
        {
            double fm = fr.mass * fr.mineral * share;
            double rest = Math.Max(1e-9, fr.mass - fm);
            double mineralLeft = fr.mass * fr.mineral - fm;
            fr.mineral = mineralLeft / rest;
            fr.gangue = fr.gangue * fr.mass / rest;
            fr.dirt = fr.dirt * fr.mass / rest;
            fr.mass = rest;
        }
        c.mixed *= 0.999;
    }

    private const string Copper = "copper";

    /// <summary>What the melt is (its composition's profile).</summary>
    public static CraftAlloyProfile MeltProfile(CraftRules rules, CraftCrucibleState c) => CraftMetallurgy.Analyze(rules, Fractions(rules, c.melt));

    public static double[] Fractions(CraftRules rules, List<CraftAmount> amounts)
    {
        var f = new double[rules.ElementCount];
        double total = 0;
        foreach (CraftAmount a in amounts)
        {
            int i = rules.IndexOf(a.element);
            if (i < 0 || a.grams <= 0) continue;
            f[i] += a.grams;
            total += a.grams;
        }
        if (total > 0)
            for (int i = 0; i < f.Length; i++) f[i] /= total;
        return f;
    }

    public static void Stir(CraftWorkshop ws, double strength)
    {
        CraftCrucibleState c = ws.Crucible;
        c.mixed = Math.Min(1, c.mixed + 0.35 * CraftMath.Clamp01(strength));
        c.cover = Math.Max(0, c.cover - 0.1 * strength);
    }

    /// <summary>
    /// The skimmer drawn over the melt with <paramref name="care"/> (0-1): it takes most of the oxide dross and of the
    /// ore's floating slag. Some metal goes with the dross - less with care, and never more than a little of the melt;
    /// the slag of an ore's rock floats clear and takes none. From a crucible poured empty the crust left in it is
    /// scraped out whole: there is no metal under it to lose.
    /// </summary>
    public static void Skim(CraftWorkshop ws, double care)
    {
        CraftCrucibleState c = ws.Crucible;
        care = CraftMath.Clamp01(care);
        double share = c.Mass <= EmptyCrucible ? 1 : 0.6 + 0.4 * care;
        double oxide = c.dross * share, rock = c.slag * share;
        double m = c.Mass;
        double metalLost = Math.Min(oxide * 0.08, m * 0.04) * (1.5 - care);
        if (m > 0)
            foreach (CraftAmount a in c.melt) a.grams = Math.Max(0, a.grams - metalLost * a.grams / m);
        c.dross -= oxide;
        c.slag -= rock;
    }

    /// <summary>
    /// Does the crucible take this ore? Only one whose metal melts at a crucible's heat (copper, tin, lead, silver and
    /// gold ores, native ones too) or boils off into a copper melt (calamine: cementation) - never iron ore, nor one
    /// that gives its metal only with its flux in a very hot furnace; and a sulfide only once it is roasted.
    /// </summary>
    public static bool CrucibleTakes(CraftRules rules, CraftPiece p, out string reason)
    {
        reason = "";
        CraftOreDefinition o = p?.ore != null ? rules.Ore(p.ore.ore) : null;
        if (o == null)
        {
            reason = "That is not an ore the crucible can take.";
            return false;
        }
        CraftElement main = null;
        foreach (CraftAmount b in p.ore.bound)
        {
            CraftElement e = rules.Element(b.element);
            if (IsMetal(e)) { main = e; break; }
        }
        if (main == null)
        {
            reason = "There is no metal in it to melt out.";
            return false;
        }
        if (!string.IsNullOrEmpty(o.needsFlux))
        {
            reason = $"{o.Name} gives up its metal only in a very hot furnace with its flux: smelt it there.";
            return false;
        }
        bool boilsOff = main.boilingPoint <= CrucibleOreHeat;
        if (!boilsOff && main.meltingPoint > CrucibleOreHeat)
        {
            reason = $"{main.Name} ore is not melted in a crucible: smelt it in the furnace.";
            return false;
        }
        if (o.sulfide && p.ore.roasted < RoastedEnough)
        {
            reason = "A sulfide ore smelts in the furnace: roast it there in an oxidising blast, then smelt it.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// How fluid a melt runs into a mould at <paramref name="rate"/> (0-1 of the stream): from its superheat and how well
    /// it casts; a stream slower than <see cref="SlowPour"/> freezes sooner. At <see cref="FullFill"/> or more it fills a
    /// whole mould (the rule of <see cref="Cast"/>).
    /// </summary>
    public static double Fill(CraftRules rules, CraftAlloyProfile prof, double superheat, double rate = 0.5)
    {
        CraftSimulationSettings s = rules.Settings;
        double fluid = CraftMath.Smooth(s.pourSuperheat * 0.25, s.pourSuperheat, superheat) * (0.5 + 0.5 * prof.Castability);
        double slow = rate < SlowPour ? (SlowPour - rate) / SlowPour : 0;
        return CraftMath.Clamp01(fluid * (1 - 0.6 * slow) + 0.1);
    }

    /// <summary>How far along a mould (0-1 of its length) a melt of this <paramref name="fill"/> runs before it freezes.</summary>
    public static double Reach(double fill) => fill < FullFill ? 0.5 + 0.5 * fill / FullFill : 1;

    /// <summary>The melt's fluidity at which it fills a whole mould.</summary>
    public const double FullFill = 0.85;
    /// <summary>A stream slower than this (0-1) freezes short; faster than <see cref="FastPour"/> it churns and traps air.</summary>
    public const double SlowPour = 0.35, FastPour = 0.8;
    /// <summary>The middle of the good stream (between <see cref="SlowPour"/> and <see cref="FastPour"/>): where the aim before a pour is shown.</summary>
    public const double GoodPour = (SlowPour + FastPour) * 0.5;
    /// <summary>
    /// A casting is porous once its pores add up past this - the gas the melt held, a melt poured too hot, a churning
    /// stream: a melt holding this much gas casts with pores however it is poured, and its look says so.
    /// </summary>
    public const double PoreGas = 0.06;
    /// <summary>A crucible holding no more metal than this (g) is empty: what is left in it is a crust to scrape out.</summary>
    public const double EmptyCrucible = 0.5;
    /// <summary>A casting unmixed past this (<see cref="CraftPiece.inhomogeneity"/>) is streaky: the melt was not stirred even (the crucible's look shows the streaks from there).</summary>
    public const double Streaky = 0.3;
    /// <summary>How much of its soundness and toughness a casting of an unmixed melt loses at the most (all streaks): where the streaks of different metal meet it breaks.</summary>
    public const double StreakWeakness = 0.3;
    /// <summary>A casting shorter than this (mm) does not set at all: the metal stays in the runner and goes back into the melt.</summary>
    public const double ShortestCasting = 1;

    // ------------------------------------------------------------------ casting
    /// <summary>
    /// The segments of a shaped mould's cavity, from its mouth to its far end (with its eye), full length: together exactly
    /// the mould's length, so a full pour is a full casting (cut in float, the stretches came up a hair short of a 150 mm
    /// mould and a full casting read as short).
    /// </summary>
    public static List<MetalSegment> MoldSegments(CraftRules rules, CraftMold mold)
    {
        int n = Math.Max(4, Math.Min(rules.Settings.segments, (int)Math.Round(mold.length / 8)));
        var segs = new List<MetalSegment>(n);
        CraftShapeTarget shape = mold.profile != null && mold.profile.Count >= 2 ? new CraftShapeTarget { profile = mold.profile } : null;
        double step = (double)mold.length / n;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            double w = mold.width, th = mold.thickness;
            if (shape != null)
            {
                shape.At(t, out float fw, out float ft);
                w = fw;
                th = ft;
            }
            segs.Add(new MetalSegment { length = step, width = w, thickness = th });
        }
        if (mold.eyeAt >= 0 && mold.eyeDiameter > 0)
            segs[Math.Min(n - 1, (int)(mold.eyeAt * n))].hole = mold.eyeDiameter;
        return segs;
    }

    /// <summary>
    /// The metal (g) that fills <paramref name="mold"/>: its cavity's volume in a metal of <paramref name="density"/>
    /// (g/cm³), or a fastener mould's whole set. What a pour needs from the crucible to fill it.
    /// </summary>
    public static double MoldMass(CraftRules rules, CraftMold mold, double density)
    {
        if (mold == null)
            return 0;
        if (mold.fasteners)
            return mold.count * mold.fastenerMass;
        double vol = 0;
        foreach (MetalSegment s in MoldSegments(rules, mold)) vol += s.Volume(0);
        return vol * Math.Max(0.1, density) / 1000;
    }

    /// <summary>
    /// A casting in <paramref name="mold"/> from a melt: its composition, what fills, cold shuts from interruptions,
    /// porosity from gas and overheating, inclusions from dross and a turbulent pour. The metal fills the cavity from its
    /// mouth - whole stretches while it lasts, then part of the next - and stops sooner where the melt freezes; the
    /// casting never holds more metal than the pour brought (<c>op.i1</c>, thousandths of the mould; 0 = all of it) nor
    /// than the melt has. <paramref name="used"/>: grams taken. Null when nothing set (too little ran in).
    /// </summary>
    internal static CraftPiece Cast(CraftWorkshop ws, CraftOperation op, List<CraftAmount> melt, double temperature, double dross, double gas, double mixed,
        CraftMold mold, double rate, double steadiness, int interruptions, string origin, out double used)
    {
        CraftRules rules = ws.Rules;
        CraftSimulationSettings s = rules.Settings;
        used = 0;
        double total = 0;
        foreach (CraftAmount a in melt) total += a.grams;
        if (total <= 1e-6 || mold == null)
            return null;
        double[] fr = Fractions(rules, melt);
        CraftAlloyProfile prof = CraftMetallurgy.Analyze(rules, fr);
        double superheat = temperature - prof.Liquidus;
        // Its flaws are seeded by the id the casting takes (the one Create gives it below, which the operation then carries):
        // the server and every copy that applies the operation over the network, or a replay, seed them alike.
        int seed = CraftMath.Hash(string.IsNullOrEmpty(op.other) ? ws.NextId : op.other) ^ CraftMath.Hash(mold.id);

        // How it pours.
        double fill = Fill(rules, prof, superheat, rate);
        double turbulence = Math.Max(0, rate - FastPour) / 0.2 * 0.6 + (1 - CraftMath.Clamp01(steadiness)) * 0.7;
        double slow = rate < SlowPour ? (SlowPour - rate) / SlowPour : 0;
        // How much of the mould the pour brought (op.i1 in thousandths; 0 = all of it).
        double brought = op.i1 > 0 ? Math.Min(1, op.i1 / 1000.0) : 1;

        // Streaks of a metal not mixed in yet: where they meet, the casting is weak (CraftEvaluator).
        double streaks = 1 - CraftMath.Clamp01(mixed);
        // What went in with the metal: the crucible's dross - or, run out of the furnace, its slag.
        bool fromFurnace = op.kind == CraftOpKind.TakeMelt;

        CraftPiece p;
        if (mold.fasteners)
        {
            // The cavities fill one after another off the runner, as a shaped mould fills along its length: a fluid melt
            // fills every one the metal brought reaches, a sluggish one freezes in the runner before the last of them.
            double mass = Math.Min(total, mold.count * mold.fastenerMass * brought);
            int count = Math.Max(0, Math.Min(mold.count, (int)Math.Floor(mass / mold.fastenerMass * Reach(fill) + 1e-6)));
            if (count == 0)
                return null; // not one cavity filled: the metal goes back into the melt
            p = ws.Create(op, CraftPieceKind.Fasteners, origin);
            p.fasteners.kind = mold.fastenerKind;
            p.fasteners.count = count;
            p.fasteners.massEach = mold.fastenerMass;
            p.fasteners.diameter = mold.width;
            p.fasteners.length = mold.length;
            p.fasteners.quality = CraftMath.Clamp01(fill * (1 - gas) * (1 - 0.4 * turbulence) * (1 - StreakWeakness * streaks));
            p.segments.Add(new MetalSegment { length = mold.length, width = mold.width, thickness = mold.thickness });
            p.inhomogeneity = streaks;
            used = count * mold.fastenerMass;
        }
        else
        {
            List<MetalSegment> segs = MoldSegments(rules, mold);
            int n = segs.Count;
            double moldMass = 0;
            foreach (MetalSegment seg in segs) moldMass += seg.Volume(0) * prof.Density / 1000;
            // Gas the melt held, a melt poured too hot, a churning stream: pores, more toward the far end.
            double gasTerm = gas, hotTerm = Math.Max(0, superheat - s.porositySuperheat) / 300, churnTerm = turbulence * 0.08;
            double poro = gasTerm + hotTerm + churnTerm;
            for (int i = 0; i < n; i++)
                segs[i].porosity = CraftMath.Clamp(poro * (0.6 + 0.8 * (i + 0.5) / n), 0, 0.5);
            // The metal runs in from the mouth: whole stretches while it lasts, then part of the next; where the melt
            // freezes (too cold, too slow) it stops sooner.
            double avail = Math.Min(total, moldMass * brought);
            double room = Reach(fill) * mold.length, metal = 0;
            bool froze = false;
            var kept = new List<MetalSegment>(n);
            foreach (MetalSegment seg in segs)
            {
                double rho = prof.Density * (1 - seg.porosity) / 1000;
                double whole = seg.Volume(0) * rho;
                if (seg.length <= room + 1e-9 && whole <= avail - metal + 1e-9)
                {
                    kept.Add(seg);
                    metal += whole;
                    room -= seg.length;
                    continue;
                }
                // The last stretch the metal reaches: as long as what is left fills (an eye only forms in a whole stretch).
                seg.hole = 0;
                double perMm = seg.Area(0) * rho;
                double byMetal = perMm > 1e-12 ? Math.Max(0, avail - metal) / perMm : 0;
                froze = room < byMetal;
                double len = Math.Max(0, Math.Min(room, byMetal));
                if (len >= ShortestCasting)
                {
                    seg.length = len;
                    kept.Add(seg);
                    metal += seg.Volume(0) * rho;
                }
                break;
            }
            if (kept.Count == 0)
                return null; // not even the mouth of the mould filled: the metal goes back into the melt
            metal = Math.Min(metal, total);
            p = ws.Create(op, CraftPieceKind.Metal, origin);
            double length = 0;
            foreach (MetalSegment seg in kept) length += seg.length;
            if (length < mold.length - 1e-6)
            {
                double filled = length / mold.length;
                p.AddDefect(CraftDefectKind.Misrun, kept.Count - 1, 1 - filled, true, froze ? "the mould did not fill: the melt froze"
                    : brought < 1 && total > moldMass * brought ? "the pour stopped before the mould was full" : "too little metal to fill the mould");
            }
            p.segments = kept;
            // Cast in the shape of a design's part: it is meant for that part (the target shows at the anvil and the wheel),
            // with that part's edges (a sword's blade both).
            if (!string.IsNullOrEmpty(mold.designId))
            {
                p.intentDesign = mold.designId;
                p.intentRole = mold.designRole ?? "";
            }
            p.edges = p.IntendedEdges(rules, 0);
            double inclusion = total > 0 ? Math.Min(0.2, dross / total * (0.3 + turbulence)) : 0;
            foreach (MetalSegment seg in kept) seg.rough = 60;
            // Metal that ran over the top of the mould: thin fins of flash along the parting line, to be filed off.
            if (op.d > CraftPourControl.Flash)
            {
                foreach (MetalSegment seg in kept) seg.rough = Math.Max(seg.rough, 60 + 80 * Math.Min(1, op.d));
                p.Record(ws.Elapsed, CraftOpKind.Pour, "It ran over the mould: fins of flash along the edges", op.d);
            }
            if (poro > PoreGas)
                p.AddDefect(CraftDefectKind.Porosity, -1, poro, poro > 0.25, gasTerm >= hotTerm && gasTerm >= churnTerm ? "gas the melt took up when it was held too hot"
                    : hotTerm >= churnTerm ? "gas from a melt poured too hot" : "air trapped by a churning, unsteady stream");
            for (int k = 0; k < interruptions; k++)
            {
                int at = (int)(CraftMath.Noise(seed, k, 1) * kept.Count);
                p.AddDefect(CraftDefectKind.ColdShut, Math.Min(kept.Count - 1, at), 0.5, true, "the pour stopped and started again");
            }
            if (superheat < s.pourSuperheat && slow > 0.3)
                p.AddDefect(CraftDefectKind.ColdShut, kept.Count - 1, 0.4, true, "poured too slowly, too cold");
            // What the shape holds: the metal, and slag from the dross (as inclusions).
            if (inclusion > 0.01)
            {
                for (int i = 0; i < kept.Count; i++)
                    kept[i].slag = metal * inclusion / kept.Count * (0.5 + CraftMath.Noise(seed, i, 2));
                p.AddDefect(CraftDefectKind.Inclusion, -1, inclusion * 5, false, fromFurnace ? "slag from the furnace ran in with the metal" : "dross poured in with the metal");
            }
            used = metal;
            p.inhomogeneity = streaks;
        }
        foreach (CraftAmount a in melt)
            if (a.grams > 0) p.composition.Add(new CraftAmount(a.element, a.grams / total * used));
        p.CompositionChanged();
        p.cast = true;
        double tSolid = Math.Min(temperature, prof.Solidus);
        double grain = 2.5 + Math.Max(0, superheat) / 120;
        foreach (MetalSegment seg in p.segments)
            foreach (CraftZone z in new[] { seg.edge, seg.body })
            {
                z.temp = z.prevTemp = tSolid;
                z.grain = grain;
                z.fine = 0.15;
                if (prof.HardensByQuench && tSolid > prof.CriticalTemperature)
                {
                    z.aust = 1;
                    z.pearl = 0;
                }
            }
        p.Record(ws.Elapsed, CraftOpKind.Pour, $"Cast in a {mold.Name.ToLowerInvariant()} mould at {CraftText.Celsius(temperature)}", superheat);
        return p;
    }

    /// <summary>Metal lost on the floor (mould-fulls) above which a pour's verdict names it.</summary>
    public const double LostNamed = 0.004;

    /// <summary>
    /// What came of a pour, in the workshop's own terms (the window's and the 3D pour's words, told once the answer is in):
    /// the casting or the fasteners - and of a fastener mould how many of the cavities the pour brought filled - short and
    /// why, a cold shut, pores and their cause, what went in with the metal (the crucible's dross to skim, or the furnace's
    /// slag to tap), flash, streaks of a melt not stirred even, a stream too fast, and the metal lost on the floor.
    /// <paramref name="cast"/>: what the pour made (null: nothing set); <paramref name="need"/>: the grams a full mould takes.
    /// </summary>
    public static string PourVerdict(CraftRules rules, CraftPiece cast, CraftPourControl p, double need, CraftMold mold, bool fromFurnace)
    {
        if (p == null)
            return "";
        string floor = p.Lost > LostNamed ? $" {CraftText.Mass(p.Lost * need)} of metal is lost on the floor ({(p.Spilled >= p.Overflow ? "it missed the cup" : "it ran over the top")})." : "";
        if (cast == null)
            return "Nothing set in the mould: it all ran onto the sand." + floor;
        var bad = new List<string>();
        bool shut = false;
        foreach (CraftDefect d in cast.defects)
            switch (d.kind)
            {
                case CraftDefectKind.Misrun: bad.Add($"it came out short - {d.cause}"); break;
                case CraftDefectKind.ColdShut:
                    if (!shut) bad.Add(d.cause == "poured too slowly, too cold" ? "a cold shut: it ran too slowly, too cold" : "the stream broke: a cold shut");
                    shut = true;
                    break;
                case CraftDefectKind.Porosity: bad.Add($"expect pores - {d.cause}"); break;
                case CraftDefectKind.Inclusion:
                    bad.Add(fromFurnace ? "slag from the furnace went in with it (tap the slag before running the metal)" : "dross went in with the metal (skim it first)");
                    break;
            }
        if (cast.kind == CraftPieceKind.Fasteners && mold != null && mold.fasteners)
        {
            // The cavities the metal poured was enough for, against those that filled before the melt froze.
            int brought = Math.Min(mold.count, (int)Math.Floor(Math.Min(1, p.Poured) * mold.count + 1e-6));
            if (cast.fasteners.count < brought)
                bad.Add($"{cast.fasteners.count} of the {brought} cavities filled - the melt froze in the runner before it reached the rest");
            if (brought < mold.count)
                bad.Add($"{(p.Empty ? "the metal ran out" : "the pour stopped")} with {mold.count - brought} of the {mold.count} cavities empty");
        }
        foreach (CraftRecordEntry e in cast.record)
            if (e.text.Contains("flash")) { bad.Add("it ran over the top: file the flash off"); break; }
        if (cast.inhomogeneity > Streaky)
            bad.Add("streaky: the melt was not mixed even - stir it before you pour");
        if (p.MeanRate > FastPour) bad.Add("the stream ran too fast and churned");
        string what = cast.kind == CraftPieceKind.Fasteners ? $"{cast.fasteners.count} {cast.fasteners.kind.ToString().ToLowerInvariant()}s" : $"a casting of {CraftText.Mass(cast.Mass(rules))}";
        return (bad.Count == 0 ? $"A clean pour: {what}, full and sound." : $"Cast {what}: {string.Join("; ", bad)}.") + floor;
    }

    internal static void AddTo(List<CraftAmount> list, string element, double grams)
    {
        if (Math.Abs(grams) < 1e-15)
            return;
        foreach (CraftAmount a in list)
            if (a.element == element)
            {
                a.grams = Math.Max(0, a.grams + grams);
                return;
            }
        if (grams > 0)
            list.Add(new CraftAmount(element, grams));
    }

    internal static double Grams(List<CraftAmount> list, string element)
    {
        foreach (CraftAmount a in list)
            if (a.element == element) return a.grams;
        return 0;
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ fluxes
    /// <summary>
    /// Adds a flux: into the furnace (<paramref name="where"/> 0: limestone, an arcane flux; a cupel), over the crucible's
    /// melt (1: borax, charcoal), or onto a piece in the fire (2: sand or borax for welding - it also keeps scale off).
    /// <paramref name="coverage"/> (0-1): how evenly it was spread over the melt or the piece (sprinkled by hand).
    /// </summary>
    public bool AddFlux(string supplyId, int count, int where, out string reason, string piece = null, double coverage = 1)
    {
        reason = "";
        CraftSupplyDefinition s = Rules.Supply(supplyId);
        if (s == null || s.kind != CraftSupplyKind.Flux && s.kind != CraftSupplyKind.Cupel)
            return Refuse("That is not a flux.", out reason);
        if (count <= 0)
            return Refuse("Nothing to add.", out reason);
        switch (where)
        {
            case 0:
                if (!Has(CraftTools.Furnace)) return Refuse("There is no furnace here.", out reason);
                if (!FurnaceTakes(s)) return Refuse($"{s.Name} does nothing in the furnace: {FluxUses(s)}.", out reason);
                break;
            case 1:
                if (!Has(CraftTools.Crucible)) return Refuse("There is no crucible here.", out reason);
                if (!s.FluxFor(CraftFluxUse.Cover)) return Refuse($"{s.Name} does not cover a melt: {FluxUses(s)}.", out reason);
                if (Crucible.Mass < 1) return Refuse("There is no melt to cover yet: melt the metal first.", out reason);
                break;
            default:
                // On a hot piece to be welded (the smith's rule: CanFluxPiece; FluxPiece takes it stretch by stretch).
                if (!CanFluxPiece(s, Piece(piece), out reason)) return Refuse(reason, out reason, piece);
                break;
        }
        Record(new CraftOperation { kind = CraftOpKind.AddFlux, id = s.id, i0 = where, i1 = count, piece = piece ?? "", label = s.Name, a = (float)CraftMath.Clamp01(coverage) });
        return true;
    }

    /// <summary>The share of a hand-spread amount that did its work (operations recorded before the hands counted: all of it).</summary>
    internal static double Spread(float coverage) => coverage > 0f ? CraftMath.Clamp01(coverage) : 1;

    /// <summary>
    /// Does the furnace take this supply? A cupel, a furnace flux (<see cref="CraftFluxUse.Furnace"/>: limestone, an arcane
    /// flux), or a flux an ore of the rules needs to smelt (<see cref="CraftOreDefinition.needsFlux"/>).
    /// </summary>
    public bool FurnaceTakes(CraftSupplyDefinition s)
    {
        if (s == null)
            return false;
        if (s.kind == CraftSupplyKind.Cupel || s.FluxFor(CraftFluxUse.Furnace))
            return true;
        if (s.kind != CraftSupplyKind.Flux)
            return false;
        foreach (CraftOreDefinition o in Rules.Ores)
            if (o != null && o.needsFlux == s.id) return true;
        return false;
    }

    /// <summary>What a flux is for, in words ("it covers a melt and helps a forge weld").</summary>
    public static string FluxUses(CraftSupplyDefinition s)
    {
        if (s == null || s.kind != CraftSupplyKind.Flux)
            return "it is not a flux";
        var uses = new List<string>();
        if ((s.fluxUse & CraftFluxUse.Furnace) != 0) uses.Add("takes the rock into the furnace's slag");
        if ((s.fluxUse & CraftFluxUse.Cover) != 0) uses.Add("covers a melt");
        if ((s.fluxUse & CraftFluxUse.Weld) != 0) uses.Add("helps a forge weld");
        return uses.Count == 0 ? "it is not used here" : "it " + string.Join(" and ", uses);
    }

    private void ApplyFlux(CraftOperation op)
    {
        CraftSupplyDefinition s = Rules.Supply(op.id);
        if (s == null)
            return;
        double grams = 100 * op.i1 * Math.Max(0.1, s.strength);
        switch (op.i0)
        {
            case 0:
                if (s.kind == CraftSupplyKind.Cupel) Furnace.cupel += grams;
                else if (s.id == "limestone") Furnace.flux += grams;
                else CraftSmelting.AddTo(Furnace.pool, "#flux:" + s.id, grams);
                break;
            case 1:
                // Sprinkled over the whole melt, it covers the whole melt; over part of it, that part.
                Crucible.cover = Math.Min(1, Crucible.cover + op.i1 * s.strength * Spread(op.a));
                Crucible.dross *= 1 - 0.4 * Spread(op.a);
                break;
            default:
                Fluxed(this, Piece(op.piece), s, op); // on a piece, where it was sprinkled (CraftForging.cs)
                break;
        }
    }

    // ------------------------------------------------------------------ the furnace
    /// <summary>Taps the slag (it must be hot enough to run).</summary>
    public bool TapSlag(out string reason, double thoroughness = 1)
    {
        reason = "";
        if (!Has(CraftTools.Furnace))
            return Refuse("There is no furnace here.", out reason);
        if (Furnace.slag < 10)
            return Refuse("There is hardly any slag to tap.", out reason);
        if (Furnace.fire.temperature < CraftSmelting.TapHeat)
            return Refuse("The slag is too stiff to run: the furnace is not hot enough.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.TapSlag, a = (float)CraftMath.Clamp01(thoroughness) });
        return true;
    }

    /// <summary>Pulls the bloom out of the furnace (a hot, spongy lump of iron and slag). Returns its piece id.</summary>
    public string ExtractBloom(out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Furnace))
        {
            Refuse("There is no furnace here.", out reason);
            return null;
        }
        if (Furnace.BloomMass < CraftSmelting.BloomMinimum)
        {
            Refuse(Furnace.BloomMass >= 1 ? $"Only a little iron has gathered at the bottom ({Furnace.BloomMass:0} g): it comes out with the bloom once there is enough to grip."
                : "There is no bloom in the furnace yet.", out reason);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.ExtractBloom };
        Record(op);
        return op.other;
    }

    private void ApplyExtract(CraftOperation op)
    {
        CraftFurnaceState f = Furnace;
        double metal = f.BloomMass;
        if (metal <= 0)
            return;
        CraftPiece p = Create(op, CraftPieceKind.Metal, "bloom from the furnace");
        foreach (CraftAmount a in f.bloom)
            if (a.grams > 0) p.composition.Add(new CraftAmount(a.element, a.grams));
        p.CompositionChanged();
        p.bloom = true;
        CraftAlloyProfile prof = p.Profile(Rules);
        int n = Math.Max(6, Rules.Settings.segments / 2);
        const double porosity = 0.3;
        double volume = metal / (prof.Density * (1 - porosity)) * 1000; // mm³
        double length = Math.Pow(volume, 1.0 / 3) * 1.4, width = length * 0.75, thickness = volume / (length * width);
        int seed = CraftMath.Hash(p.id);
        double temperature = Math.Min(f.fire.temperature, prof.Solidus - 40);
        for (int i = 0; i < n; i++)
        {
            var seg = new MetalSegment
            {
                length = length / n, width = width * (0.85 + 0.3 * CraftMath.Noise(seed, i, 1)), thickness = thickness, porosity = porosity,
                slag = f.bloomSlag / n * (0.5 + CraftMath.Noise(seed, i, 2)), rough = 200,
                carbonShift = (CraftMath.Noise(seed, i, 3) * 2 - 1) * f.bloomSpread * Math.Max(0.002, prof.Carbon),
            };
            foreach (CraftZone z in new[] { seg.edge, seg.body })
            {
                z.temp = z.prevTemp = temperature;
                z.grain = 3;
                if (prof.HardensByQuench && temperature > prof.CriticalTemperature) { z.aust = 1; z.pearl = 0; }
            }
            p.segments.Add(seg);
        }
        p.Resample(n);
        if (f.bloomSlag > metal * 0.05)
            p.AddDefect(CraftDefectKind.Inclusion, -1, f.bloomSlag / metal * 3, true, "slag in the bloom: work it at welding heat");
        f.bloom.Clear();
        f.bloomSlag = 0;
        f.bloomSpread = 0;
        f.bloomSolidus = 0;
        p.Record(Elapsed, op.kind, $"Drawn from the furnace at {CraftText.Celsius(temperature)}", temperature);
        Raise(CraftEventKind.Smelted, p.id, "bloom", "");
    }

    // ------------------------------------------------------------------ the crucible
    /// <summary>Is the crucible's melt at least partly liquid (above its solidus)? A solid cake cannot be stirred or skimmed.</summary>
    public bool MeltIsLiquid => Crucible.Mass > 0.5 && Crucible.temperature >= CraftSmelting.MeltProfile(Rules, Crucible).Solidus;

    public bool StirMelt(double strength, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Crucible) || Crucible.Mass <= 0.5)
            return Refuse("There is nothing melted to stir.", out reason);
        if (!MeltIsLiquid)
            return Refuse("The metal in the crucible is solid: there is nothing to stir until it melts.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.StirMelt, a = (float)CraftMath.Clamp01(strength) });
        return true;
    }

    /// <summary>
    /// Skims the dross and the slag off the melt with <paramref name="care"/> (0-1) - or scrapes the crust left in a
    /// crucible poured empty out of it. Not off a solid melt: the dross is stuck in it.
    /// </summary>
    public bool SkimDross(double care, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Crucible) || Crucible.dross + Crucible.slag < 0.5)
            return Refuse("There is no dross to skim.", out reason);
        if (Crucible.Mass > CraftSmelting.EmptyCrucible && !MeltIsLiquid)
            return Refuse("The metal in the crucible is solid: the dross is stuck in it until it melts.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.SkimDross, a = (float)CraftMath.Clamp01(care) });
        return true;
    }

    /// <summary>
    /// Can metal be poured now - from the crucible, or (<paramref name="fromFurnace"/>) run out of the furnace into a
    /// mould? The same check the pour itself makes, so the hands are told before they start.
    /// </summary>
    public bool CanPour(bool fromFurnace, out string reason)
    {
        reason = "";
        if (fromFurnace)
        {
            if (!Has(CraftTools.Furnace))
                return Fail("There is no furnace here.", out reason);
            double metal = Furnace.PoolMass;
            if (metal < CraftSmelting.PourMinimum)
                return Fail(metal >= CraftSmelting.BeadMinimum ? "Too little metal to pour: lift the bead out with the tongs instead." : "There is no liquid metal in the furnace.", out reason);
            if (!CraftSmelting.PoolLiquid(Rules, Furnace))
                return Fail("The metal at the bottom has frozen: the furnace is not hot enough to run it.", out reason);
            return true;
        }
        if (!Has(CraftTools.Crucible))
            return Fail("There is no crucible here.", out reason);
        foreach (CraftPiece p in pieces)
            if (p.location == CraftLocation.Melt)
                return Fail(p.kind == CraftPieceKind.Ore ? "An ore is still in the crucible: wait until it is spent, or lift it out with the tongs."
                    : "Something in the crucible has not melted yet.", out reason);
        if (Crucible.Mass < 1)
            return Fail("The crucible is empty.", out reason);
        CraftAlloyProfile prof = CraftSmelting.MeltProfile(Rules, Crucible);
        if (Crucible.temperature < prof.Liquidus)
            return Fail(Crucible.temperature < prof.Solidus ? "The metal in the crucible is solid: let it melt, or knock the cake out once it is cold."
                : "It is not fully melted: it will not pour.", out reason);
        return true;
    }

    private static bool Fail(string text, out string reason)
    {
        reason = text;
        return false;
    }

    /// <summary>
    /// Pours the crucible into a mould: <paramref name="rate"/> (0-1, how fast), <paramref name="steadiness"/> (0-1),
    /// <paramref name="interruptions"/> (times the stream stopped). <paramref name="lost"/>: the metal that missed the
    /// mould or ran over it, in mould-fulls (0: as <paramref name="spilled"/> and <paramref name="overflow"/> say, per
    /// gram cast). Returns the casting's piece id ("" when nothing set: everything missed the mould).
    /// </summary>
    public string Pour(string moldId, double rate, double steadiness, int interruptions, out string reason, double spilled = 0, double overflow = 0, double filled = 1, double lost = 0)
    {
        reason = "";
        CraftMold mold = Rules.Mold(moldId);
        if (!Has(CraftTools.Crucible))
        {
            Refuse("There is no crucible here.", out reason);
            return null;
        }
        if (mold == null)
        {
            Refuse("There is no such mould.", out reason);
            return null;
        }
        if (!CanPour(false, out string why))
        {
            Refuse(why, out reason);
            return null;
        }
        var op = new CraftOperation
        {
            kind = CraftOpKind.Pour, id = mold.id, a = (float)CraftMath.Clamp01(rate), b = (float)CraftMath.Clamp01(steadiness), i0 = Math.Max(0, interruptions),
            c = (float)CraftMath.Clamp(spilled, 0, 2), d = (float)CraftMath.Clamp01(overflow), i1 = FilledShare(filled), i2 = LostShare(lost),
        };
        Record(op);
        return op.other;
    }

    /// <summary>How much of the mould the pour brought, in thousandths (0 = all of it, as before it was measured).</summary>
    private static int FilledShare(double filled) => filled >= 0.9995 ? 0 : Math.Max(1, (int)Math.Round(CraftMath.Clamp01(filled) * 1000));

    /// <summary>The metal lost on the floor, in thousandths of the mould (0 = counted per gram cast, as before it was measured).</summary>
    private static int LostShare(double lost) => lost > 0 ? Math.Max(1, (int)Math.Round(Math.Min(3, lost) * 1000)) : 0;

    /// <summary>
    /// The metal a pour lost on the floor (g): <c>op.i2</c> thousandths of the mould when the hands measured it, else
    /// what <c>op.c</c> (missed) and <c>op.d</c> (ran over) say per gram cast - never more than is left.
    /// </summary>
    private double PourLoss(CraftOperation op, CraftMold mold, double density, double used, double have)
    {
        double lost = op.i2 > 0 ? op.i2 / 1000.0 * CraftSmelting.MoldMass(Rules, mold, density) : used * Math.Max(0, op.c + op.d * 0.3);
        return Math.Min(Math.Max(0, have - used), lost);
    }

    private void ApplyPour(CraftOperation op)
    {
        CraftCrucibleState c = Crucible;
        if (op.flag)
        {
            ApplyKnockOut(op);
            return;
        }
        CraftMold mold = Rules.Mold(op.id);
        double m = c.Mass, density = CraftSmelting.MeltProfile(Rules, c).Density;
        CraftPiece p = CraftSmelting.Cast(this, op, c.melt, c.temperature, c.dross + c.slag, c.gas, c.mixed, mold, op.a, op.b, op.i0, "poured from the crucible", out double used);
        if (p == null)
            used = 0;
        // What missed the mould (or ran over it) is lost on the floor.
        double lost = mold != null ? PourLoss(op, mold, density, used, m) : 0;
        if (p == null && lost <= 0)
            return;
        if (m > 0)
            foreach (CraftAmount a in c.melt) a.grams = Math.Max(0, a.grams - (used + lost) * a.grams / m);
        c.melt.RemoveAll(a => a.grams < 1e-6);
        if (p == null)
        {
            Raise(CraftEventKind.Note, "", "spilt", "It all ran onto the sand: nothing set in the mould.");
            return;
        }
        c.dross *= 0.7;
        c.slag *= 0.7;
        Raise(CraftEventKind.Poured, p.id, op.id, "");
    }

    /// <summary>
    /// A melt that cannot be poured (frozen in the crucible - or that never will be, iron having got into it) is let
    /// freeze and knocked out of the crucible as a cake, with its dross and slag in it. Returns the cake's piece id.
    /// </summary>
    public string KnockOut(out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Crucible))
        {
            Refuse("There is no crucible here.", out reason);
            return null;
        }
        foreach (CraftPiece p in pieces)
            if (p.location == CraftLocation.Melt)
            {
                Refuse("Lift out what is still in it first.", out reason);
                return null;
            }
        if (Crucible.Mass < 1)
        {
            Refuse("The crucible is empty.", out reason);
            return null;
        }
        if (Crucible.temperature >= CraftSmelting.MeltProfile(Rules, Crucible).Solidus)
        {
            Refuse("It is still molten: pour it, or let the fire die and knock the cake out once it has frozen.", out reason);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.Pour, flag = true };
        Record(op);
        return op.other;
    }

    private void ApplyKnockOut(CraftOperation op)
    {
        CraftCrucibleState c = Crucible;
        double metal = c.Mass;
        if (metal <= 0)
            return;
        CraftPiece p = Create(op, CraftPieceKind.Metal, "knocked out of the crucible");
        foreach (CraftAmount a in c.melt)
            if (a.grams > 0) p.composition.Add(new CraftAmount(a.element, a.grams));
        p.CompositionChanged();
        CraftAlloyProfile prof = p.Profile(Rules);
        // A cake the shape of the crucible's bottom: about 70 mm across.
        double volume = metal / prof.Density * 1000, across = 70;
        double thickness = Math.Max(1, volume / (across * across * 0.8));
        const int n = 6;
        double slag = c.dross + c.slag, temperature = Math.Min(c.temperature, prof.Solidus - 1);
        for (int i = 0; i < n; i++)
        {
            double t = (i + 0.5) / n;
            var seg = new MetalSegment
            {
                length = across / n, width = across * Math.Sqrt(Math.Max(0.1, 1 - (2 * t - 1) * (2 * t - 1))), thickness = thickness,
                slag = slag / n, rough = 200, porosity = CraftMath.Clamp(c.gas * 0.5, 0, 0.5),
            };
            foreach (CraftZone z in new[] { seg.edge, seg.body })
            {
                z.temp = z.prevTemp = temperature;
                z.grain = 4;
            }
            p.segments.Add(seg);
        }
        // Its thickness made up for the round outline: the cake holds the melt's metal, no more and no less.
        double made = 0;
        foreach (MetalSegment s in p.segments) made += s.Volume(0) * prof.Density * (1 - s.porosity) / 1000;
        if (made > 1e-9)
            foreach (MetalSegment s in p.segments) s.thickness *= metal / made;
        p.cast = true;
        p.inhomogeneity = 1 - CraftMath.Clamp01(c.mixed); // frozen as it was mixed
        if (slag > metal * 0.02)
            p.AddDefect(CraftDefectKind.Inclusion, -1, Math.Min(1, slag / metal * 3), slag > metal * 0.1, "dross and slag frozen into the cake");
        p.Record(Elapsed, CraftOpKind.Pour, "Knocked out of the crucible as a cake", temperature);
        c.melt.Clear();
        c.dross = c.slag = c.gas = c.cover = 0;
        c.mixed = 1;
        Raise(CraftEventKind.Note, p.id, "cake", "The cake drops out of the crucible.");
    }

    /// <summary>Runs the furnace's liquid metal out into a mould (copper, tin, lead, silver, cast iron). Returns the casting's id ("" when nothing set).</summary>
    public string TakeMelt(string moldId, double rate, double steadiness, out string reason, double spilled = 0, double overflow = 0, double filled = 1, int interruptions = 0, double lost = 0)
    {
        reason = "";
        if (!CanPour(true, out string why))
        {
            Refuse(why, out reason);
            return null;
        }
        CraftMold mold = Rules.Mold(moldId);
        if (mold == null)
        {
            Refuse("There is no such mould.", out reason);
            return null;
        }
        var op = new CraftOperation
        {
            kind = CraftOpKind.TakeMelt, id = mold.id, a = (float)CraftMath.Clamp01(rate), b = (float)CraftMath.Clamp01(steadiness), i0 = Math.Max(0, interruptions),
            c = (float)CraftMath.Clamp(spilled, 0, 2), d = (float)CraftMath.Clamp01(overflow), i1 = FilledShare(filled), i2 = LostShare(lost),
        };
        Record(op);
        return op.other;
    }

    /// <summary>
    /// Lifts the last of the metal off the bottom of the furnace with the tongs - a bead, less than can be poured: the
    /// silver left on the cupel, a prill of copper. Returns its piece id.
    /// </summary>
    public string LiftBead(out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Furnace))
        {
            Refuse("There is no furnace here.", out reason);
            return null;
        }
        double metal = Furnace.PoolMass;
        if (metal < CraftSmelting.BeadMinimum)
        {
            Refuse("There is no metal at the bottom of the furnace.", out reason);
            return null;
        }
        if (metal >= CraftSmelting.PourMinimum)
        {
            Refuse("There is enough metal to pour: run it into a mould.", out reason);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.TakeMelt, flag = true };
        Record(op);
        return op.other;
    }

    private void ApplyTakeMelt(CraftOperation op)
    {
        CraftFurnaceState f = Furnace;
        var metal = new List<CraftAmount>();
        foreach (CraftAmount a in f.pool)
            if (!CraftFurnaceState.IsFlux(a) && a.grams > 0) metal.Add(new CraftAmount(a.element, a.grams));
        double total = 0;
        foreach (CraftAmount a in metal) total += a.grams;
        if (total <= 0)
            return;
        if (op.flag)
        {
            ApplyBead(op, metal, total);
            return;
        }
        if (f.matte > 0)
            metal.Add(new CraftAmount("sulfur", f.matte));
        double all = total + f.matte;
        CraftMold mold = Rules.Mold(op.id);
        double density = CraftMetallurgy.Analyze(Rules, CraftSmelting.Fractions(Rules, metal)).Density;
        // The pool holds no gas of its own (its pores come from the heat and the stream), and the blast keeps it one metal;
        // a little of the slag over it runs out with it.
        CraftPiece p = CraftSmelting.Cast(this, op, metal, f.fire.temperature, f.slag * 0.02, 0, 1, mold, op.a, op.b, op.i0, "run out of the furnace", out double used);
        if (p == null)
            used = 0;
        double lost = mold != null ? PourLoss(op, mold, density, used, all) : 0; // spilled on the floor
        if (p == null && lost <= 0)
            return;
        used += lost;
        foreach (CraftAmount a in f.pool)
            if (!CraftFurnaceState.IsFlux(a)) a.grams = Math.Max(0, a.grams - used * a.grams / all);
        f.matte = Math.Max(0, f.matte - used * f.matte / all);
        f.pool.RemoveAll(a => a.grams < 1e-6);
        if (p == null)
            Raise(CraftEventKind.Note, "", "spilt", "It all ran onto the sand: nothing set in the mould.");
        else
            Raise(CraftEventKind.Poured, p.id, op.id, "");
    }

    /// <summary>
    /// The bead: all the metal at the bottom, a small round lump hot from the hearth - with what matte clings to it (no
    /// more sulfur than a sulfide of that metal holds); the rest of the matte stays to be roasted away.
    /// </summary>
    private void ApplyBead(CraftOperation op, List<CraftAmount> metal, double total)
    {
        CraftFurnaceState f = Furnace;
        double clinging = Math.Min(f.matte, total * 0.25);
        if (clinging > 1e-9)
            metal.Add(new CraftAmount("sulfur", clinging));
        double all = total + clinging;
        CraftPiece p = Create(op, CraftPieceKind.Metal, "a bead lifted from the furnace");
        foreach (CraftAmount a in metal)
            if (a.grams > 0) p.composition.Add(new CraftAmount(a.element, a.grams));
        p.CompositionChanged();
        CraftAlloyProfile prof = p.Profile(Rules);
        const int n = 4;
        double volume = all / prof.Density * 1000, size = Math.Pow(volume, 1.0 / 3);
        double temperature = Math.Min(f.fire.temperature, prof.Solidus - 20);
        for (int i = 0; i < n; i++)
        {
            double t = (i + 0.5) / n;
            var seg = new MetalSegment { length = size / n, width = size * (0.7 + 0.6 * Math.Sqrt(Math.Max(0, 1 - (2 * t - 1) * (2 * t - 1)))), thickness = size * 0.8, rough = 40 };
            foreach (CraftZone z in new[] { seg.edge, seg.body })
            {
                z.temp = z.prevTemp = temperature;
                z.grain = 3;
            }
            p.segments.Add(seg);
        }
        double made = 0;
        foreach (MetalSegment s in p.segments) made += s.Volume(0) * prof.Density / 1000;
        if (made > 1e-9)
            foreach (MetalSegment s in p.segments) s.thickness *= all / made;
        p.Record(Elapsed, op.kind, $"Lifted off the bottom of the furnace as a bead at {CraftText.Celsius(temperature)}", temperature);
        foreach (CraftAmount a in f.pool)
            if (!CraftFurnaceState.IsFlux(a)) a.grams = 0;
        f.pool.RemoveAll(a => a.grams < 1e-6);
        f.matte = Math.Max(0, f.matte - clinging);
        Raise(CraftEventKind.Smelted, p.id, "bead", "");
    }
}
