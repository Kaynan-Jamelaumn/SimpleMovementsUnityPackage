using System;
using System.Collections.Generic;

/// <summary>
/// The furnace and the crucible.
/// <list type="bullet">
/// <item><b>Roasting</b>: a sulfide ore held at its roasting heat in an oxidising fire loses its sulfur (unroasted, its
/// copper comes out as a brittle matte).</item>
/// <item><b>Reduction</b>: in a hot, reducing furnace the mineral gives up its metal. What melts at the furnace's
/// heat (copper, tin, lead, silver, gold) runs to the bottom as liquid; iron does not melt - it grows into a spongy
/// bloom full of slag. The rock and the bound silica become slag; silica takes iron with it unless there is lime
/// (limestone flux, or a limestone rock). Phosphorus mostly goes into the iron; sulfur too, from unroasted ore or a
/// coal fire. A hotter furnace with more charcoal puts more carbon into the bloom - too hot and it melts into cast
/// iron. Slag that is not tapped chokes the draft.</item>
/// <item><b>Cupellation</b>: lead with silver in it, on a bone-ash cupel in a strong, hot draft: the lead soaks into
/// the cupel as litharge and the silver stays.</item>
/// <item><b>The crucible</b>: metal put in melts at its own solidus; the melt follows the heat of the furnace (or
/// the forge), oxidises into dross (less under a flux cover), boils away what boils (zinc), takes up gas when held too
/// hot; additions are mixed in by time and stirring.</item>
/// <item><b>Pouring</b>: the superheat, the pour's speed and steadiness and its interruptions decide whether the mould
/// fills, whether there are cold shuts, porosity or inclusions; the casting's composition is the melt's.</item>
/// </list>
/// </summary>
public static class CraftSmelting
{
    // ------------------------------------------------------------------ the furnace
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
        if (s.Bound("sulfur") > 0 && t > o.roastTemperature - 150 && t < o.roastTemperature + 350 && ox > 0.35)
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
        bool matte = o.sulfide && s.roasted < 0.7;
        // Where the metal goes: liquid to the bottom when the ore's main metal melts at this heat, else into the bloom.
        // What does not melt in a liquid smelt (iron from a copper ore) mostly goes into the slag.
        CraftElement primary = null;
        foreach (CraftAmount b in s.bound)
        {
            CraftElement e = rules.Element(b.element);
            if (e != null && (e.kind == CraftElementKind.Metal || e.kind == CraftElementKind.Fantasy)) { primary = e; break; }
        }
        bool liquid = primary != null && MeltsAt(primary, furnaceT);
        List<CraftAmount> product = liquid ? f.pool : f.bloom;
        foreach (CraftAmount b in s.bound)
        {
            double g = dm * b.grams;
            if (g <= 0) continue;
            CraftElement e = rules.Element(b.element);
            if (e == null) continue;
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
        if (s.MineralMass <= 1e-6)
            Consumed(ws, p);
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
        double fines = 0, charge = 0;
        foreach (CraftPiece p in ws.Pieces)
            if (p.kind == CraftPieceKind.Ore && p.location == CraftLocation.Furnace)
                foreach (OreFragment fr in p.ore.fragments)
                {
                    charge += fr.mass;
                    if (fr.size < CraftOres.FineSize) fines += fr.mass;
                }
        double choke = Math.Max(0, f.slag / rules.Settings.slagCapacity - 0.6) + (charge > 0 ? fines / charge * 0.6 : 0);
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
        if (f.cupel > 0 && pool > 0 && lead >= 0 && ox > 0.45 && t > 900)
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
        // Metal pieces in the furnace: a matte loses its sulfur in an oxidising fire, and melts into the pool.
        foreach (CraftPiece p in new List<CraftPiece>(ws.Pieces))
        {
            if (p.kind != CraftPieceKind.Metal || p.location != CraftLocation.Furnace) continue;
            double sulfur = p.Grams("sulfur");
            double total = 0;
            foreach (CraftAmount a in p.composition) total += a.grams;
            if (sulfur > 0 && ox > 0.35 && p.MaxTemperature > 700 && total > 0)
                p.SetGrams("sulfur", sulfur * Math.Exp(-dt * 0.01 * ox));
            if (!p.bloom && p.MaxTemperature >= p.Profile(rules).Solidus)
            {
                double mass = p.Mass(rules);
                foreach (CraftAmount a in p.composition)
                    if (total > 0) AddTo(f.pool, a.element, a.grams / total * mass);
                ws.Destroy(p, "It melts into the furnace.");
            }
        }
        // The matte loses sulfur in an oxidising blast too.
        if (f.matte > 0 && ox > 0.45 && t > 900)
            f.matte = Math.Max(0, f.matte - dt * 0.05 * ox);
    }

    public static double FluxOf(CraftFurnaceState f, string id) => Grams(f.pool, "#flux:" + id);

    private static void UseFlux(CraftFurnaceState f, string id, double grams) => AddTo(f.pool, "#flux:" + id, -grams);

    /// <summary>Taps the liquid slag out (<paramref name="thoroughness"/> 0-1).</summary>
    public static void TapSlag(CraftWorkshop ws, double thoroughness)
    {
        CraftFurnaceState f = ws.Furnace;
        double out_ = f.slag * 0.85 * CraftMath.Clamp01(thoroughness);
        f.slag -= out_;
        f.matte *= 1 - 0.3 * thoroughness; // some matte runs out with it
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
            foreach (CraftAmount a in p.composition)
                if (total > 0) AddTo(c.melt, a.element, a.grams / total * metal);
            c.dross += slag;
            c.mixed = before + metal > 1e-9 ? c.mixed * before / (before + metal) : 1;
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
            return;
        // Oxidation into dross, boiling off, gas, mixing.
        double cover = CraftMath.Clamp01(c.cover);
        foreach (CraftAmount a in c.melt)
        {
            CraftElement e = rules.Element(a.element);
            if (e == null || a.grams <= 0) continue;
            double ox = a.grams * e.slagAffinity * 0.0004 * (1 - 0.8 * cover) * (1 + superheat / 200) * dt;
            double boil = c.temperature > e.boilingPoint ? a.grams * 0.01 * (c.temperature - e.boilingPoint) / 100 * (1 - 0.7 * cover) * dt : 0;
            if (boil > 0.05 * dt && ws.Step % 20 == 0)
                ws.Raise(CraftEventKind.Note, "", "fumes", "White fumes and green-white flames rise from the melt.");
            a.grams = Math.Max(0, a.grams - ox - boil);
            c.dross += ox * 1.25;
            c.lost += boil;
        }
        c.gas = CraftMath.Clamp01(c.gas + dt * (0.002 * Math.Max(0, superheat - 120) / 100 * (1 - 0.5 * cover) - 0.0005));
        c.mixed = Math.Min(1, c.mixed + dt * 0.004);
        c.cover = Math.Max(0, c.cover - dt * 0.0005);
    }

    /// <summary>
    /// An ore in the crucible: once hot enough it reduces, its metal going into the melt. What boils at the crucible's
    /// heat (zinc) is caught by a copper melt under a cover - else it is lost as fumes. The rock goes to the dross.
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
            c.dross += p.ore.Mass;
            ws.Destroy(p, "The ore is spent.");
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
            if (e.kind == CraftElementKind.Impurity && b.element != "phosphorus")
            {
                c.dross += g;
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
        foreach (OreFragment fr in p.ore.fragments)
        {
            double fm = fr.mass * fr.mineral * share;
            double rest = Math.Max(1e-9, fr.mass - fm);
            double mineralLeft = fr.mass * fr.mineral - fm;
            c.dross += fm * 0.3;
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

    public static void Skim(CraftWorkshop ws, double care)
    {
        CraftCrucibleState c = ws.Crucible;
        double taken = c.dross * (0.6 + 0.4 * CraftMath.Clamp01(care));
        double metalLost = taken * 0.08 * (1.5 - care);
        double m = c.Mass;
        if (m > 0)
            foreach (CraftAmount a in c.melt) a.grams = Math.Max(0, a.grams - metalLost * a.grams / m);
        c.dross -= taken;
    }

    // ------------------------------------------------------------------ casting
    /// <summary>
    /// A casting in <paramref name="mold"/> from a melt: its composition, what fills, cold shuts from interruptions,
    /// porosity from gas and overheating, inclusions from dross and a turbulent pour. <paramref name="used"/>: grams taken.
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
        int seed = CraftMath.Hash(op.other ?? "") ^ CraftMath.Hash(mold.id);

        // How it pours.
        double fluid = CraftMath.Smooth(s.pourSuperheat * 0.25, s.pourSuperheat, superheat) * (0.5 + 0.5 * prof.Castability);
        double slow = rate < 0.35 ? (0.35 - rate) / 0.35 : 0;
        double turbulence = Math.Max(0, rate - 0.8) / 0.2 * 0.6 + (1 - CraftMath.Clamp01(steadiness)) * 0.7;
        double fill = CraftMath.Clamp01(fluid * (1 - 0.6 * slow) + 0.1);

        CraftPiece p;
        if (mold.fasteners)
        {
            p = ws.Create(op, CraftPieceKind.Fasteners, origin);
            double want = mold.count * mold.fastenerMass;
            double mass = Math.Min(total, want);
            int count = Math.Max(0, (int)Math.Floor(mass / mold.fastenerMass * fill + 1e-6));
            mass = count * mold.fastenerMass;
            p.fasteners.kind = mold.fastenerKind;
            p.fasteners.count = count;
            p.fasteners.massEach = mold.fastenerMass;
            p.fasteners.diameter = mold.width;
            p.fasteners.length = mold.length;
            p.fasteners.quality = CraftMath.Clamp01(fill * (1 - gas) * (1 - 0.4 * turbulence));
            p.segments.Add(new MetalSegment { length = mold.length, width = mold.width, thickness = mold.thickness });
            used = mass;
        }
        else
        {
            p = ws.Create(op, CraftPieceKind.Metal, origin);
            int n = Math.Max(4, Math.Min(s.segments, (int)Math.Round(mold.length / 8)));
            double vol = 0;
            var segs = new List<MetalSegment>(n);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                double w = mold.width, th = mold.thickness;
                if (mold.profile != null && mold.profile.Count >= 2)
                {
                    var shape = new CraftShapeTarget { profile = mold.profile };
                    shape.At(t, out float fw, out float ft);
                    w = fw;
                    th = ft;
                }
                var seg = new MetalSegment { length = mold.length / n, width = w, thickness = th };
                segs.Add(seg);
                vol += seg.Volume(0);
            }
            if (mold.eyeAt >= 0 && mold.eyeDiameter > 0)
                segs[Math.Min(n - 1, (int)(mold.eyeAt * n))].hole = mold.eyeDiameter;
            double moldMass = vol * prof.Density / 1000;
            double mass = Math.Min(total, moldMass);
            // Too little metal, or it froze: the far end does not fill.
            double filled = Math.Min(mass / moldMass, fill < 0.85 ? 0.5 + 0.5 * fill / 0.85 : 1);
            int keep = Math.Max(1, (int)Math.Round(n * filled));
            if (keep < n)
            {
                segs.RemoveRange(keep, n - keep);
                p.AddDefect(CraftDefectKind.Misrun, keep - 1, 1 - filled, true, filled < mass / moldMass - 1e-6 ? "the mould did not fill: the melt froze" : "too little metal to fill the mould");
            }
            p.segments = segs;
            p.edges = 0;
            double poro = gas + Math.Max(0, superheat - s.porositySuperheat) / 300 + turbulence * 0.08;
            double inclusion = total > 0 ? Math.Min(0.2, dross / total * (0.3 + turbulence)) : 0;
            for (int i = 0; i < segs.Count; i++)
            {
                double t = (i + 0.5) / n;
                segs[i].porosity = CraftMath.Clamp(poro * (0.6 + 0.8 * t), 0, 0.5);
                segs[i].rough = 60;
            }
            if (poro > 0.06)
                p.AddDefect(CraftDefectKind.Porosity, -1, poro, poro > 0.25, superheat > s.porositySuperheat ? "gas from a melt held too hot" : "gas and a turbulent pour");
            for (int k = 0; k < interruptions; k++)
            {
                int at = (int)(CraftMath.Noise(seed, k, 1) * segs.Count);
                p.AddDefect(CraftDefectKind.ColdShut, Math.Min(segs.Count - 1, at), 0.5, true, "the pour stopped and started again");
            }
            if (superheat < s.pourSuperheat && slow > 0.3)
                p.AddDefect(CraftDefectKind.ColdShut, segs.Count - 1, 0.4, true, "poured too slowly, too cold");
            // What the shape actually holds: slag from the dross (as inclusions) and the metal.
            double metalMass = 0;
            for (int i = 0; i < segs.Count; i++)
                metalMass += segs[i].Volume(0) * prof.Density * (1 - segs[i].porosity) / 1000;
            metalMass = Math.Min(metalMass, total);
            if (inclusion > 0.01)
            {
                for (int i = 0; i < segs.Count; i++)
                    segs[i].slag = metalMass * inclusion / segs.Count * (0.5 + CraftMath.Noise(seed, i, 2));
                p.AddDefect(CraftDefectKind.Inclusion, -1, inclusion * 5, false, "dross poured in with the metal");
            }
            used = metalMass;
            p.inhomogeneity = 1 - CraftMath.Clamp01(mixed);
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
    /// </summary>
    public bool AddFlux(string supplyId, int count, int where, out string reason, string piece = null)
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
                break;
            case 1:
                if (!Has(CraftTools.Crucible)) return Refuse("There is no crucible here.", out reason);
                break;
            default:
                CraftPiece p = Piece(piece);
                if (p == null || p.kind != CraftPieceKind.Metal) return Refuse("Flux goes on metal.", out reason, piece);
                if (p.MaxTemperature < 600) return Refuse("Flux only sticks to hot metal.", out reason, piece);
                break;
        }
        Record(new CraftOperation { kind = CraftOpKind.AddFlux, id = s.id, i0 = where, i1 = count, piece = piece ?? "", label = s.Name });
        return true;
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
                Crucible.cover = Math.Min(1, Crucible.cover + 0.5 * op.i1 * s.strength);
                Crucible.dross *= 0.6;
                break;
            default:
                CraftPiece p = Piece(op.piece);
                if (p == null) break;
                p.flux = Math.Min(1, p.flux + 0.6 * op.i1 * s.strength);
                p.Record(Elapsed, op.kind, $"Fluxed with {s.Name.ToLowerInvariant()}");
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
        if (Furnace.fire.temperature < 1120)
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
        if (Furnace.BloomMass < 50)
        {
            Refuse("There is no bloom in the furnace yet.", out reason);
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
    public bool StirMelt(double strength, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Crucible) || Crucible.Mass <= 0)
            return Refuse("There is nothing melted to stir.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.StirMelt, a = (float)CraftMath.Clamp01(strength) });
        return true;
    }

    public bool SkimDross(double care, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Crucible) || Crucible.dross < 0.5)
            return Refuse("There is no dross to skim.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.SkimDross, a = (float)CraftMath.Clamp01(care) });
        return true;
    }

    /// <summary>
    /// Pours the crucible into a mould: <paramref name="rate"/> (0-1, how fast), <paramref name="steadiness"/> (0-1),
    /// <paramref name="interruptions"/> (times the stream stopped). Returns the casting's piece id.
    /// </summary>
    public string Pour(string moldId, double rate, double steadiness, int interruptions, out string reason)
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
        if (Crucible.Mass < 1)
        {
            Refuse("The crucible is empty.", out reason);
            return null;
        }
        foreach (CraftPiece p in pieces)
            if (p.location == CraftLocation.Melt)
            {
                Refuse("Something in the crucible has not melted yet.", out reason);
                return null;
            }
        CraftAlloyProfile prof = CraftSmelting.MeltProfile(Rules, Crucible);
        if (Crucible.temperature < prof.Liquidus)
        {
            Refuse("It is not fully melted: it will not pour.", out reason);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.Pour, id = mold.id, a = (float)CraftMath.Clamp01(rate), b = (float)CraftMath.Clamp01(steadiness), i0 = Math.Max(0, interruptions) };
        Record(op);
        return op.other;
    }

    private void ApplyPour(CraftOperation op)
    {
        CraftCrucibleState c = Crucible;
        CraftPiece p = CraftSmelting.Cast(this, op, c.melt, c.temperature, c.dross, c.gas, c.mixed, Rules.Mold(op.id), op.a, op.b, op.i0, "poured from the crucible", out double used);
        if (p == null)
            return;
        double m = c.Mass;
        if (m > 0)
            foreach (CraftAmount a in c.melt) a.grams = Math.Max(0, a.grams - used * a.grams / m);
        c.melt.RemoveAll(a => a.grams < 1e-6);
        c.dross *= 0.7;
        Raise(CraftEventKind.Poured, p.id, op.id, "");
    }

    /// <summary>Runs the furnace's liquid metal out into a mould (copper, tin, lead, silver, cast iron). Returns the casting's id.</summary>
    public string TakeMelt(string moldId, double rate, double steadiness, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Furnace))
        {
            Refuse("There is no furnace here.", out reason);
            return null;
        }
        double metal = 0;
        foreach (CraftAmount a in Furnace.pool)
            if (!a.element.StartsWith("#", StringComparison.Ordinal)) metal += a.grams;
        if (metal < 20)
        {
            Refuse("There is no liquid metal in the furnace.", out reason);
            return null;
        }
        CraftMold mold = Rules.Mold(moldId);
        if (mold == null)
        {
            Refuse("There is no such mould.", out reason);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.TakeMelt, id = mold.id, a = (float)CraftMath.Clamp01(rate), b = (float)CraftMath.Clamp01(steadiness) };
        Record(op);
        return op.other;
    }

    private void ApplyTakeMelt(CraftOperation op)
    {
        CraftFurnaceState f = Furnace;
        var metal = new List<CraftAmount>();
        foreach (CraftAmount a in f.pool)
            if (!a.element.StartsWith("#", StringComparison.Ordinal) && a.grams > 0) metal.Add(new CraftAmount(a.element, a.grams));
        double total = 0;
        foreach (CraftAmount a in metal) total += a.grams;
        if (total <= 0)
            return;
        if (f.matte > 0)
            metal.Add(new CraftAmount("sulfur", f.matte));
        CraftPiece p = CraftSmelting.Cast(this, op, metal, f.fire.temperature, f.slag * 0.02, 0.05, 0.9, Rules.Mold(op.id), op.a, op.b, 0, "run out of the furnace", out double used);
        if (p == null)
            return;
        double all = total + f.matte;
        foreach (CraftAmount a in f.pool)
            if (!a.element.StartsWith("#", StringComparison.Ordinal)) a.grams = Math.Max(0, a.grams - used * a.grams / all);
        f.matte = Math.Max(0, f.matte - used * f.matte / all);
        f.pool.RemoveAll(a => a.grams < 1e-6);
        Raise(CraftEventKind.Poured, p.id, op.id, "");
    }
}
