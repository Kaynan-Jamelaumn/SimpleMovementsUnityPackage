#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the built-in content's numbers: every part of every design can be had from the stock and made
    /// to its own target, the rules' check finds the content's contradictions, and a material is worth the same as stock,
    /// as a worked piece and in a finished item (no cut, alloy or trip through the bag makes more of it).
    /// </summary>
    public class CraftContentFixTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static (string element, double fraction)[] Steel => new[] { ("iron", 0.995), ("carbon", 0.005) };
        private static (string element, double fraction)[] Bronze => new[] { ("copper", 0.89), ("tin", 0.11) };
        private static double Density(CraftRules r, (string, double)[] metal) => CraftDefaults.Bar(r, 100, 10, 10, metal).Profile(r).Density;

        // ------------------------------------------------------------------ the rules' check
        [Test]
        public void Check_FindsNoContradiction_InTheBuiltInContent()
        {
            CraftRules r = Rules();
            Assert.IsEmpty(r.CompileErrors, string.Join("\n", r.CompileErrors));
            Assert.IsEmpty(r.CompileWarnings, string.Join("\n", r.CompileWarnings));
        }

        [Test]
        public void Check_FindsEachKindOfContradiction()
        {
            CraftContent c = CraftDefaults.Content();
            CraftDesign mace = c.designs.First(d => d.id == "mace");
            mace.components[0].shape.profile = new List<CraftProfilePoint> { new CraftProfilePoint(0, 60, 60), new CraftProfilePoint(1, 60, 60) };
            CraftDesign spear = c.designs.First(d => d.id == "spear");
            spear.components[0].shape.eyeDiameter = 32;
            spear.referenceMass = 2000;
            c.designs.First(d => d.id == "longbow").Component("stave").wood.lengthMin = 2500;
            c.designs.First(d => d.id == "leather_boots").Component("soles").leather.thicknessMax = 14;
            c.ores.First(o => o.id == "gold_quartz").price = 500;
            c.ores.First(o => o.id == "hematite").price = 0.05f;
            c.classes.First(x => x.id == "brass").ranges.Clear();
            c.classes.First(x => x.id == "brass").With(CraftDefaults.Zinc, 0.05f, 0.45f);
            c.classes.First(x => x.id == "coin_silver").With(CraftDefaults.Copper, 0.3f, 0.2f);
            c.supplies.First(s => s.id == "wool_fibre").spinsInto.Add("beeswax");
            c.supplies.First(s => s.id == "flax_fibre").weavesInto = "cow";
            c.ores.First(o => o.id == "mythril_ore").needsFlux = "linen_thread";
            List<string> w = CraftRules.Compile(new CraftSimulationSettings(), c).CompileWarnings;
            string all = string.Join("\n", w);
            void Has(string what, string because) => Assert.IsTrue(w.Any(x => x.Contains(what)), $"{because}:\n{all}");
            Has("Design 'mace', head: its own profile weighs", "a pattern heavier than its own range");
            Has("Design 'spear', head: its 32 mm eye leaves less than a millimetre", "an eye as wide as its socket");
            Has("Design 'spear': its reference mass (2000 g) is outside", "a weapon rated for a mass its head cannot have");
            Has("Design 'longbow', stave: no blank or log", "a stave longer than any wood");
            Has("Design 'leather_boots': its seam 'Soles sewn on' through leather up to 14 mm", "leather too thick for any thread");
            Has("Ore 'gold_quartz': a lump costs 500", "an ore dearer than its poorest lump's metal");
            Has("Ore 'hematite': a lump costs 0.05", "an ore that prints money");
            Has("Material classes 'bronze' and 'brass'", "equal priorities that overlap");
            Has("Material class 'coin_silver': its copper range runs backwards", "a range that runs backwards");
            Has("Supply 'wool_fibre': it spins into 'beeswax'", "a fibre spun into what is not a thread");
            Has("Supply 'flax_fibre': it weaves into 'cow'", "a fibre woven into a hide");
            Has("Ore 'mythril_ore': it needs 'linen_thread', which is not a flux", "a flux that is not a flux");
        }

        [Test]
        public void Classes_OfEqualPriority_AreTriedInTheOrderListed()
        {
            CraftContent c = CraftDefaults.Content();
            // two overlapping classes of one priority: whichever is listed first names the metal, run after run
            c.classes.Insert(0, new CraftMaterialClass { id = "first", displayName = "First", baseElement = CraftDefaults.Copper, priority = 85 }.With(CraftDefaults.Tin, 0.04f, 0.2f));
            c.classes.Add(new CraftMaterialClass { id = "last", displayName = "Last", baseElement = CraftDefaults.Copper, priority = 85 }.With(CraftDefaults.Tin, 0.04f, 0.2f));
            CraftRules r = CraftRules.Compile(new CraftSimulationSettings(), c);
            Assert.AreEqual("first", Class(r, (CraftDefaults.Copper, 0.9), (CraftDefaults.Tin, 0.1)));
            int first = Array.FindIndex(r.Classes, x => x.id == "first"), bronze = Array.FindIndex(r.Classes, x => x.id == "bronze"), last = Array.FindIndex(r.Classes, x => x.id == "last");
            Assert.Less(first, bronze);
            Assert.Less(bronze, last, "equal priorities keep the content's order");
            // the built-in classes do not overlap: tin and zinc, silver and copper each name one thing
            Assert.AreEqual("brass", Class(Rules(), (CraftDefaults.Copper, 0.7), (CraftDefaults.Tin, 0.1), (CraftDefaults.Zinc, 0.2)), "a tenth of zinc or more: a brass, tin or no tin");
            Assert.AreEqual("bronze", Class(Rules(), (CraftDefaults.Copper, 0.88), (CraftDefaults.Tin, 0.1), (CraftDefaults.Zinc, 0.02)), "a gunmetal is a bronze");
            Assert.AreEqual("electrum", Class(Rules(), (CraftDefaults.Gold, 0.6), (CraftDefaults.Silver, 0.2), (CraftDefaults.Copper, 0.2)));
            Assert.AreEqual("rose_gold", Class(Rules(), (CraftDefaults.Gold, 0.75), (CraftDefaults.Silver, 0.03), (CraftDefaults.Copper, 0.22)));
        }

        private static string Class(CraftRules r, params (string element, double fraction)[] metal) =>
            CraftDefaults.Bar(r, 100, 10, 10, metal).Profile(r).Class?.id;

        [Test]
        public void EveryClass_ButOnesOfMetalsNotFoundAlone_CanBeReached()
        {
            CraftRules r = Rules();
            // poor galena smelts to plain lead; the green nickel ore to nickel (and with copper and zinc, nickel silver)
            CraftValue.OreMetal(r, r.Ore("galena"), 0, out CraftAlloyProfile lead, out _);
            Assert.AreEqual("lead_metal", lead.Class?.id);
            CraftValue.OreMetal(r, r.Ore("galena"), 0.5, out CraftAlloyProfile rich, out _);
            Assert.AreEqual("silver_lead", rich.Class?.id, "a usual galena carries silver worth the cupel");
            CraftValue.OreMetal(r, r.Ore("garnierite"), 0.5, out CraftAlloyProfile nickel, out _);
            Assert.AreEqual("nickel_metal", nickel.Class?.id);
            Assert.AreEqual("nickel_silver", Class(r, (CraftDefaults.Copper, 0.6), (CraftDefaults.Nickel, 0.2), (CraftDefaults.Zinc, 0.2)));
            Assert.IsNull(r.Class("starmetal_metal"), "starmetal is never found alone: no class names it pure");
        }

        [Test]
        public void AddMissing_BringsANewMetal_WithWhatItDoesInAlloys()
        {
            CraftContent built = CraftDefaults.Content();
            CraftContent project = CraftDefaults.Content();
            project.elements.RemoveAll(e => e.id == CraftDefaults.Orichalcum);
            project.effects.RemoveAll(e => e.element == CraftDefaults.Orichalcum || e.baseElement == CraftDefaults.Orichalcum);
            int had = project.effects.Count;
            int added = project.AddMissing(built);
            Assert.IsTrue(project.elements.Any(e => e.id == CraftDefaults.Orichalcum));
            int orichalcum = built.effects.Count(e => e.element == CraftDefaults.Orichalcum || e.baseElement == CraftDefaults.Orichalcum);
            Assert.AreEqual(had + orichalcum, project.effects.Count, "its alloy effects come with it, and no other effect twice");
            Assert.GreaterOrEqual(added, 1 + orichalcum);
            Assert.AreEqual(0, project.AddMissing(built), "a second time adds nothing");
        }

        // ------------------------------------------------------------------ every part from stock, made to its target
        [Test]
        public void EveryPart_OfEveryDesign_CanBeHadFromTheStock()
        {
            CraftRules r = Rules();
            double hottest = r.Fuels.Max(f => f.maxTemperature);
            var problems = new List<string>();
            foreach (CraftDesign d in r.Designs)
                foreach (CraftComponentSpec c in d.components)
                {
                    string why = StockFor(r, d, c, hottest);
                    if (why != null) problems.Add($"{d.id}/{c.role}: {why}");
                }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>Why the stock gives no material for a part (null: it does).</summary>
        private static string StockFor(CraftRules r, CraftDesign d, CraftComponentSpec c, double hottest)
        {
            if (c.supply)
                return r.FirstSupply(c.supplyKind) != null ? null : $"no {c.supplyKind} supply";
            switch (c.kind)
            {
                case CraftPieceKind.Wood:
                {
                    CraftWoodSpec w = c.wood;
                    double across = Math.Max(w.diameterMin, c.shape.profile.Max(p => p.width)), through = Math.Max(w.diameterMin, c.shape.profile.Max(p => Math.Min(p.width, p.thickness)));
                    foreach (CraftWoodDefinition wood in r.Woods.Where(x => x.toughness >= w.toughnessMin))
                    {
                        // the stock blank as it goes on the bench, and the stock log (rived down, as long as it is)
                        CraftPiece blank = CraftDefaults.WoodBlank(r, wood.id), log = CraftDefaults.Log(r, wood.id);
                        double bw = blank.segments[0].width, bt = blank.segments[0].thickness;
                        if (blank.Length >= w.lengthMin && Math.Max(bw, bt) >= across && Math.Min(bw, bt) >= through) return null;
                        if (log.Length >= w.lengthMin && log.segments[0].width >= across) return null;
                    }
                    return $"no blank or log is {w.lengthMin} mm long and {across} mm across";
                }
                case CraftPieceKind.Hide:
                {
                    CraftLeatherSpec l = c.leather;
                    CraftPattern pat = r.Pattern(l.pattern);
                    foreach (CraftHideDefinition h in r.Hides)
                    {
                        if (l.fabric == CraftFabric.Cloth && !h.textile || l.fabric == CraftFabric.Leather && h.textile) continue;
                        if (Place(r, h, pat, l, h.textile)) return null;
                    }
                    return $"no {(l.fabric == CraftFabric.Cloth ? "cloth" : "hide")} takes the {pat.Name.ToLowerInvariant()} at {l.thicknessMin}-{l.thicknessMax} mm";
                }
                case CraftPieceKind.Fasteners:
                    return r.Molds.Any(m => m.fasteners && m.fastenerKind == c.fastener) ? null : $"no mould casts {c.fastener}s";
                case CraftPieceKind.Gem:
                    return r.Gems.Any(g => r.Cut(g.preferredCut) is CraftGemCut cut && (cut.cabochon ? c.gem.cabochonOk : c.gem.facetedOk)
                                           && g.typicalCarats * cut.yield >= c.gem.caratsMin && g.typicalCarats * cut.yield <= c.gem.caratsMax) ? null : "no stone cuts to its size";
                case CraftPieceKind.Metal:
                    if (d.kind == CraftDesignKind.Jewelry)
                        return CraftDefaults.JewelleryMetals.Length > 0 && CraftDefaults.JewelleryForms.Length > 0 ? null : "no jeweller's wire or strip";
                    if (!CraftRules.Castable(c))
                        return "no profile to cast or forge to";
                    // cast in its own pattern mould from stock bars the crucible melts (or forged from them to the same profile)
                    bool melts = CraftDefaults.StockMetals().Any(m => CraftDefaults.Bar(r, 100, 10, 10, m.composition).Profile(r).Liquidus < hottest - 50);
                    return melts ? null : "no stock metal melts in the crucible";
            }
            return null;
        }

        /// <summary>Does a fresh hide (or bolt) of <paramref name="h"/> take the pattern somewhere, as thick as the part wants (leather can be skived thinner)?</summary>
        private static bool Place(CraftRules r, CraftHideDefinition h, CraftPattern pat, CraftLeatherSpec l, bool cloth)
        {
            HideState hide = CraftLeather.NewHide(r, h.id, 1).hide;
            for (int rot = 0; rot < 4; rot++)
            {
                pat.Size(rot, out int pw, out int ph);
                for (int y = 0; y + ph <= hide.height; y++)
                    for (int x = 0; x + pw <= hide.width; x++)
                    {
                        if (!CraftLeather.Fits(hide, pat, x, y, rot)) continue;
                        double t = 0;
                        int n = 0;
                        for (int py = 0; py < ph; py++)
                            for (int px = 0; px < pw; px++)
                                if (pat.Covers(px, py, rot)) { t += hide.Cell(x + px, y + py).thickness; n++; }
                        t /= Math.Max(1, n);
                        if (t >= l.thicknessMin - 0.05 && (!cloth || t <= l.thicknessMax + 0.05)) return true;
                    }
            }
            return false;
        }

        [Test]
        public void EveryMetalPart_MadeToItsOwnProfile_IsInsideItsMassRange_InSteelAndBronze()
        {
            CraftRules r = Rules();
            double steel = Density(r, Steel), bronze = Density(r, Bronze);
            var problems = new List<string>();
            foreach (CraftDesign d in r.Designs)
                foreach (CraftComponentSpec c in d.components)
                {
                    if (!CraftRules.Castable(c)) continue;
                    CraftMold mold = r.PatternMold(CraftRules.PatternMoldId(d.id, c.role));
                    foreach (var (metal, density) in new[] { ("steel", steel), ("bronze", bronze) })
                    {
                        double g = CraftSmelting.MoldMass(r, mold, density);
                        if (g < c.shape.massMin || g > c.shape.massMax)
                            problems.Add($"{d.id}/{c.role}: {g:0} g in {metal}, wants {c.shape.massMin}-{c.shape.massMax}");
                    }
                    // and a piece cast to it scores its own target as well made, never "too heavy" or "too light"
                    var notes = new List<string>();
                    CraftPiece cast = new CraftPiece { kind = CraftPieceKind.Metal, segments = CraftSmelting.MoldSegments(r, mold) };
                    foreach (var (element, fraction) in Steel) cast.composition.Add(new CraftAmount(element, fraction * 1000));
                    cast.CompositionChanged();
                    CraftEvaluator.ShapeScore(r, cast, c.shape, notes);
                    if (notes.Any(n => n.StartsWith("too light") || n.StartsWith("too heavy")))
                        problems.Add($"{d.id}/{c.role}: its own profile is scored {string.Join(", ", notes)}");
                }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void Eyes_LeaveMetalRoundThem_AndWeapons_AreRatedForTheirMainPartsMass()
        {
            CraftRules r = Rules();
            foreach (CraftDesign d in r.Designs)
            {
                foreach (CraftComponentSpec c in d.components.Where(x => CraftRules.Castable(x) && x.shape.eye))
                {
                    c.shape.At(c.shape.eyeAt, out float width, out _);
                    Assert.GreaterOrEqual(width - c.shape.eyeDiameter, 2, $"{d.id}/{c.role}: a {c.shape.eyeDiameter} mm eye in {width} mm of metal");
                }
                CraftComponentSpec main = d.MainComponent;
                if (d.IsWeaponLike && main.kind == CraftPieceKind.Metal)
                {
                    Assert.GreaterOrEqual(d.referenceMass, main.shape.massMin, d.id);
                    Assert.LessOrEqual(d.referenceMass, main.shape.massMax, d.id);
                }
            }
            CraftDesign scythe = r.Design("scythe");
            scythe.Component("head").shape.At(scythe.Component("head").shape.eyeAt, out float heel, out _);
            Assert.Greater(heel, scythe.Component("head").shape.eyeDiameter, "the scythe's heel is wider than the snath's eye");
        }

        [Test]
        public void WoodStock_IsTheSpeciesBlank_AndLogsAreAsLongAsIt()
        {
            CraftRules r = Rules();
            CraftPiece ash = CraftDefaults.WoodBlank(r, "ash");
            Assert.AreEqual(2200, ash.Length, 1e-6, "an ash blank is a spear shaft's stave");
            Assert.AreEqual(40, ash.segments[0].width, 1e-6);
            CraftPiece plank = CraftDefaults.WoodBlank(r, "linden");
            Assert.AreEqual(1100, plank.Length, 1e-6);
            Assert.AreEqual(650, plank.segments[0].width, 1e-6, "a linden plank wide enough for a kite shield");
            Assert.AreEqual(2200, CraftDefaults.Log(r, "ash").Length, 1e-6, "a log is as long as its blank");
            Assert.AreEqual(900, CraftDefaults.Log(r, "pine").Length, 1e-6, "and never shorter than 90 cm");
            Assert.AreEqual(CraftDefaults.LogDiameter, CraftDefaults.Log(r, "oak").segments[0].width, 1e-6);
            // every long part comes from a blank of a wood tough enough for it
            Assert.GreaterOrEqual(ash.Length, r.Design("spear").Component("haft").wood.lengthMin);
            Assert.GreaterOrEqual(CraftDefaults.WoodBlank(r, "yew").Length, r.Design("longbow").Component("stave").wood.lengthMin);
        }

        // ------------------------------------------------------------------ what things are worth
        [Test]
        public void ABar_IsNeverWorthLessThanTheOreItTook()
        {
            CraftRules r = Rules();
            foreach (CraftOreDefinition o in r.Ores)
            {
                double poorest = CraftValue.OreMetal(r, o, 0, out CraftAlloyProfile prof, out double grams), usual = CraftValue.OreMetal(r, o, 0.5);
                Assert.Greater(grams, 0, o.id);
                Assert.GreaterOrEqual(poorest, o.price, $"{o.id}: the metal of its poorest lump ({prof.ClassName}, {grams:0} g) is worth {poorest:0.00}, the lump {o.price}");
                Assert.LessOrEqual(usual, 5 * o.price, $"{o.id}: a usual lump's metal is worth {usual:0.00}: buying it to smelt would print money");
                // the same metal cast into a bar is worth just as much: the class's multiplier is in both
                var bar = new CraftPiece { kind = CraftPieceKind.Metal };
                foreach (int i in Enumerable.Range(0, r.ElementCount).Where(i => prof.Fractions[i] > 0))
                    bar.composition.Add(new CraftAmount(r.Elements[i].id, prof.Fractions[i]));
                bar.CompositionChanged();
                bar = CraftDefaults.Bar(r, grams / prof.Density * 1000 / (30 * 6), 30, 6, bar.composition.Select(a => (a.element, a.grams)).ToArray());
                Assert.AreEqual(poorest, CraftValue.Of(r, bar), poorest * 0.01 + 0.01, $"{o.id}: its bar");
            }
        }

        [Test]
        public void TheClassMultiplier_CountsTheSame_RawWorkedAndFinished()
        {
            CraftRules r = Rules();
            (string, double)[] tool = CraftDefaults.StockMetals().First(m => m.name == "Tool steel").composition;
            CraftPiece bar = CraftDefaults.Bar(r, 200, 30, 6, tool);
            CraftAlloyProfile prof = bar.Profile(r);
            Assert.AreEqual("high_carbon_steel", prof.Class.id);
            // its class's making on its matrix (the iron and carbon; the manganese, dearer than iron, at its own worth)
            Assert.AreEqual(bar.Mass(r) * (prof.PricePerGram + (prof.Class.priceMultiplier - 1) * prof.MatrixPricePerGram), CraftValue.Of(r, bar), 1e-9, "a bar of tool steel is worth its class");
            // a dagger of it: the finished item's price is its design's (by quality) and its materials', each worth what it was
            CraftDesign dagger = r.Design("dagger");
            var parts = new Dictionary<string, List<CraftPiece>>
            {
                ["blade"] = new List<CraftPiece> { CraftDefaults.Bar(r, 440, 24, 5, tool) },
                ["guard"] = new List<CraftPiece> { CraftDefaults.Bar(r, 90, 14, 8, CraftDefaults.StockMetals().First(m => m.name == "Bronze").composition) },
                ["grip"] = new List<CraftPiece> { CraftDefaults.WoodBlank(r, "maple", 100, 26, 26) },
                ["pommel"] = new List<CraftPiece> { CraftDefaults.Bar(r, 35, 30, 12, CraftDefaults.StockMetals().First(m => m.name == "Brass").composition) },
            };
            CraftItemReport item = CraftEvaluator.Item(r, dagger, parts, new AssemblyState { design = dagger.id });
            double materials = parts.Values.SelectMany(p => p).Sum(p => CraftValue.Of(r, p));
            Assert.AreEqual(dagger.price * (0.3 + 1.4 * item.Quality) + materials, item.Price, 0.01);
            CraftPiece blade = parts["blade"][0];
            Assert.AreEqual(prof.Class, blade.Profile(r).Class);
            Assert.AreEqual(blade.Mass(r) * (blade.Profile(r).PricePerGram + (prof.Class.priceMultiplier - 1) * blade.Profile(r).MatrixPricePerGram), CraftValue.Of(r, blade), 1e-9, "the blade's steel at its class, as the bar was");
        }

        [Test]
        public void Wood_IsWorthItsShareOfABlank_SoSawingMakesNoMoreOfIt()
        {
            CraftRules r = Rules();
            CraftWoodDefinition oak = r.Wood("oak");
            Assert.AreEqual(oak.price, CraftValue.Of(r, CraftDefaults.WoodBlank(r, "oak")), 1e-9, "a blank is worth its price");
            var w = new CraftWorkshop(r, CraftTools.Everything, 18, 1, 3, "water", "content");
            string whole = w.AddPiece(CraftDefaults.WoodBlank(r, "oak"));
            double before = CraftValue.Of(r, w.Piece(whole));
            string far = w.SawCut(whole, 0.3, 0.02, 0.98, out string why);
            Assert.IsNotNull(far, why);
            string last = w.SawCut(far, 0.5, 0.02, 0.98, out why);
            Assert.IsNotNull(last, why);
            double after = CraftValue.Of(r, w.Piece(whole)) + CraftValue.Of(r, w.Piece(far)) + CraftValue.Of(r, w.Piece(last));
            Assert.AreEqual(before, after, before * 0.001, "three offcuts are worth the blank they came from");
            // a green log in its bark is worth half its volume of seasoned stock: riving and seasoning are still to do
            CraftPiece log = CraftDefaults.Log(r, "oak");
            double volume = log.Length * log.segments[0].width * log.segments[0].thickness;
            Assert.AreEqual(oak.price * volume / (oak.blankLength * oak.blankWidth * oak.blankThickness) * CraftValue.Unseasoned, CraftValue.Of(r, log), 1e-6);
            log.wood.moisture = CraftWoodwork.Seasoned;
            Assert.AreEqual(oak.price * volume / (oak.blankLength * oak.blankWidth * oak.blankThickness), CraftValue.Of(r, log), 1e-6, "seasoned, all of it");
        }

        [Test]
        public void HidesAndCloth_WeighAsLeatherAndClothDo_AndAreWorthTheirShare()
        {
            CraftRules r = Rules();
            CraftPiece cow = CraftLeather.NewHide(r, "cow", 1);
            int cells = cow.hide.cells.Count(c => c.present);
            double area = cells * r.Settings.cellSize * r.Settings.cellSize / 1e4; // m²
            Assert.Greater(cow.Mass(r), 7000, "a wet cow hide weighs several kilograms");
            CraftPiece linen = CraftLeather.NewHide(r, "linen_cloth", 1);
            double gsm = linen.Mass(r) / (linen.hide.cells.Count(c => c.present) * r.Settings.cellSize * r.Settings.cellSize / 1e4);
            Assert.That(gsm, Is.InRange(150, 250), "linen weighs 150-250 g a square metre");
            // shrinking draws a cell in and thickens it: its mass stays
            double m0 = cow.Mass(r);
            foreach (HideCell c in cow.hide.cells) c.shrink = 0.3;
            Assert.AreEqual(m0, cow.Mass(r), 1e-6);
            // drying makes it lighter (the water goes), as leather does
            foreach (HideCell c in cow.hide.cells) c.moisture = 0.12;
            Assert.Less(cow.Mass(r), m0 * 0.8);
            Assert.Greater(cow.Mass(r) / (area * 1e4 * CraftLeather.MeanThickness(r, cow) / 10), 0.75, "dry leather about 0.8 g/cm³");
            // a whole raw hide is worth its price; a bolt of cloth its price, not twice it (it has nothing to tan)
            Assert.AreEqual(r.Hide("deer").price, CraftValue.Of(r, CraftLeather.NewHide(r, "deer", 3)), 1e-6);
            Assert.AreEqual(r.Hide("canvas").price, CraftValue.Of(r, CraftLeather.NewHide(r, "canvas", 3)), 1e-6);
            Assert.AreNotEqual(r.Hide("deer").hairColor, r.Hide("wolf").hairColor, "each animal its own coat");
            Assert.AreNotEqual(r.Hide("cow").color, r.Hide("bear").color);
        }

        [Test]
        public void Fasteners_AreWorthTheirMetalAndTheirMaking()
        {
            CraftRules r = Rules();
            CraftPiece rivets = CraftDefaults.FastenerSet(r, CraftFastenerKind.Rivet, 10, 4, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002));
            CraftAlloyProfile p = rivets.Profile(r);
            Assert.AreEqual(40 * p.PricePerGram * (p.Class?.priceMultiplier ?? 1) + 10 * CraftValue.FastenerMaking, CraftValue.Of(r, rivets), 1e-9);
            CraftPiece buckles = CraftDefaults.FastenerSet(r, CraftFastenerKind.Buckle, 2, 30, (CraftDefaults.Iron, 1.0));
            Assert.Greater(CraftValue.Of(r, buckles), 2 * CraftValue.BuckleMaking, "a buckle's making is worth more than a rivet's");
        }
    }
}
#endif
