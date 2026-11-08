#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the content review (the default content, the economy, mastery and the journal): a crack teaches
    /// the lesson of its real cause; every insight has something in the work that teaches it (a flaw the first time it
    /// shows, a note the work tells, steel passing the magnet's heat, leather dried stiff, a finished item's coarse grain);
    /// the journal's procedure words heats and vats as the player knew them; an insight is practice in its own craft.
    /// </summary>
    public class CraftReviewContentTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything, string medium = "water") =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, medium, "contentrev");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        private static string Bar(CraftWorkshop w, string metal, double length = 200, double width = 30, double thickness = 6) =>
            w.AddPiece(CraftDefaults.Bar(w.Rules, length, width, thickness, Metal(metal)));

        private static void SetHeat(CraftPiece p, double celsius)
        {
            foreach (MetalSegment s in p.segments)
                s.edge.temp = s.edge.prevTemp = s.body.temp = s.body.prevTemp = celsius;
        }

        private static void Hours(CraftWorkshop w, double hours) =>
            w.RunSteps((int)Math.Round(hours / w.Rules.Settings.processHoursPerSecond / w.Rules.Settings.stepSeconds));

        /// <summary>What the journal learns from the workshop's events from now on (the station's way: CraftKnowledge.Observe).</summary>
        private static List<string> Learning(CraftWorkshop w, CraftInstruments kit = CraftInstruments.All)
        {
            var learnt = new List<string>();
            w.EventRaised += e =>
            {
                string insight = CraftJournal.InsightOf(e, w.Piece(e.Piece), kit);
                if (insight != null && !learnt.Contains(insight)) learnt.Add(insight);
            };
            return learnt;
        }

        private static void LightForge(CraftWorkshop w)
        {
            Assert.IsTrue(w.AddFuel("charcoal", 6, out string why), why);
            for (int t = 0; t < 20; t++) { w.Bellows(1); w.RunSteps(10); }
        }

        private static void HeatTo(CraftWorkshop w, string id, double target)
        {
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Fire, out string why, 0.6), why);
            for (int t = 0; t < 4000 && w.Piece(id).MeanTemperature < target; t++)
            {
                if (t % 10 == 0) w.Bellows(0.4);
                if (t % 600 == 599) w.AddFuel("charcoal", 2, out _);
                w.RunSteps(1);
            }
            Assert.Greater(w.Piece(id).MeanTemperature, target, "it never got hot enough");
            w.RunSteps(60);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
        }

        private static void Strikes(CraftWorkshop w, string id, int blows, double force = 1.0)
        {
            for (int k = 0; k < blows; k++)
                w.Strike(id, 0.2 + 0.6 * (k % 7) / 6.0, CraftStrikeTool.Flat, CraftStrikeFace.Flat, force, 0, out _);
        }

        /// <summary>Fleshed on the beam, a stroke along every row, twice.</summary>
        private static string Fleshed(CraftWorkshop w, string hideId)
        {
            string id = w.AddPiece(CraftLeather.NewHide(w.Rules, hideId, 2));
            CraftPiece h = w.Piece(id);
            for (int pass = 0; pass < 2; pass++)
                for (int y = 0; y < h.hide.height; y++)
                    Assert.IsTrue(w.Scrape(id, new float[] { 0, y + 0.5f, h.hide.width, y + 0.5f }, false, 0.35, 0.7, out string why), why);
            return id;
        }

        private static void Soak(CraftWorkshop w, string id, int vat, string agent, int doses, double hours)
        {
            Assert.IsTrue(w.FillVat(vat, agent, doses, out string why), why);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Vat, out why, 1, 0, 9999, vat), why);
            Hours(w, hours);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Air, out why), why);
        }

        private static float[] AllOver(CraftWorkshop w, string id)
        {
            HideState h = w.Piece(id).hide;
            var path = new List<float>();
            for (int y = 0; y < h.height; y++)
            {
                path.Add(y % 2 == 0 ? 0 : h.width); path.Add(y + 0.5f);
                path.Add(y % 2 == 0 ? h.width : 0); path.Add(y + 0.5f);
            }
            return path.ToArray();
        }

        // ------------------------------------------------------------------ r04: a crack teaches the lesson of its cause
        [Test]
        public void Cracks_TeachTheLessonOfTheirRealCause()
        {
            // burnt mild steel crumbling under the hammer: burning, not cast iron
            CraftWorkshop burnt = Workshop();
            string b = Bar(burnt, "Mild steel", 300, 30, 10);
            SetHeat(burnt.Piece(b), 1100);
            foreach (MetalSegment s in burnt.Piece(b).segments) s.body.burnt = s.edge.burnt = 0.6;
            List<string> fromBurnt = Learning(burnt);
            Strikes(burnt, b, 6);
            CollectionAssert.Contains(fromBurnt, "burning", "burnt steel crumbles: the lesson is burning");
            CollectionAssert.DoesNotContain(fromBurnt, "cast_iron", "mild steel is not cast iron");

            // iron with 0.3 % sulfur torn at a good forging heat: sulfur, not forged too cold
            CraftWorkshop red = Workshop();
            string r = red.AddPiece(CraftDefaults.Bar(red.Rules, 300, 30, 10, (CraftDefaults.Iron, 0.995), (CraftDefaults.Carbon, 0.002), (CraftDefaults.Sulfur, 0.003)));
            SetHeat(red.Piece(r), 1000);
            List<string> fromRed = Learning(red);
            Strikes(red, r, 30);
            CollectionAssert.Contains(fromRed, "sulfur");
            CollectionAssert.DoesNotContain(fromRed, "cold_forging", "it was struck at 1000 °C");
            StringAssert.Contains("sulfur", red.Piece(r).defects.First(d => d.kind == CraftDefectKind.Crack).cause);

            // phosphoric iron cracked cold: phosphorus
            CraftWorkshop bog = Workshop();
            string p = bog.AddPiece(CraftDefaults.Bar(bog.Rules, 300, 30, 10, (CraftDefaults.Iron, 0.989), (CraftDefaults.Phosphorus, 0.01), (CraftDefaults.Carbon, 0.001)));
            List<string> fromBog = Learning(bog);
            Strikes(bog, p, 100);
            CollectionAssert.Contains(fromBog, "phosphorus", string.Join(", ", bog.Piece(p).defects.Select(d => d.cause)));

            // cast iron struck: cast iron; tool steel struck cold: forged too cold
            CraftWorkshop cast = Workshop();
            string c = cast.AddPiece(CraftDefaults.Bar(cast.Rules, 200, 30, 10, (CraftDefaults.Iron, 0.965), (CraftDefaults.Carbon, 0.035)));
            SetHeat(cast.Piece(c), 900);
            List<string> fromCast = Learning(cast);
            Strikes(cast, c, 10);
            CollectionAssert.Contains(fromCast, "cast_iron");
            CraftWorkshop cold = Workshop();
            string t = Bar(cold, "Tool steel", 200, 30, 6);
            List<string> fromCold = Learning(cold);
            Strikes(cold, t, 60);
            CollectionAssert.Contains(fromCold, "cold_forging");

            // a crack as the steel hardens, in the tank or out of it: the quench's lesson
            var air = new CraftPiece { id = "p", kind = CraftPieceKind.Metal, location = CraftLocation.Air };
            Assert.AreEqual("quench_crack", CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Cracked, 0, "p", CraftForging.CrackId + "hardening", "Something pings as it cools."), air));
            CraftWorkshop brine = Workshop(medium: "brine");
            string q = Bar(brine, "Tool steel", 200, 30, 5);
            List<string> fromBrine = Learning(brine);
            LightForge(brine);
            HeatTo(brine, q, 850);
            Assert.IsTrue(brine.MovePiece(q, CraftLocation.Quench, out string why), why);
            brine.Agitate(q, CraftQuenchMotion.UpDown, out _);
            brine.RunSteps(400);
            CollectionAssert.Contains(fromBrine, "quench_crack");
        }

        // ------------------------------------------------------------------ r05: every insight has a trigger
        /// <summary>Every id the work raises now (notes, crack causes, flaws first showing, the magnet, stiff drying) and the journal's own paths reach every insight.</summary>
        [Test]
        public void EveryInsight_HasSomethingThatTeachesIt()
        {
            var reached = new HashSet<string>();
            void Add(string s) { if (s != null) reached.Add(s); }
            var notes = new[] { "seam", "blick", "spilt", "cake", "skipped_grit", "gouged", "debarked", "fold", "weld", "gap", "loose_stone", "inspection", "measured",
                "skate", "temper_drawn", "shoulder", "felted", "neps", "skipped_pick", "crusted", "identified", "misidentified", "shattered", "freehand", "pavilion", "overpolished",
                "crazed", "flaked", "soft_compound", "short", "temper_colours", "unfluxed", "alum", "blotch", "warp", CraftSmelting.FumesId + "zinc" };
            foreach (string id in notes) Add(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Note, 0, "", id, ""), null));
            foreach (string cause in new[] { "burnt", "castiron", "bloom", "sulfur", "hotshort", "phosphorus", "coldshort", "cold", "overworked", "hardening" })
                Add(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Cracked, 0, "", CraftForging.CrackId + cause, ""), null));
            foreach (CraftDefectKind kind in Enum.GetValues(typeof(CraftDefectKind)))
                Add(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Defect, 0, "", kind.ToString(), ""), null));
            Add(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Burning, 0, "", "", ""), null));
            Add(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.MagnetLost, 0, "", "", ""), null, CraftInstruments.Magnet));
            Add(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Dried, 0, "", "stiff", ""), null));
            reached.Add("quench_crack"); reached.Add("untempered"); // CraftJournal.Quenched
            reached.Add("loose_joint"); reached.Add("normalise");    // CraftJournal.Made
            var never = CraftJournal.Insights.Keys.Where(k => !reached.Contains(k)).ToList();
            Assert.IsEmpty(never, "insights nothing teaches");
            Assert.IsNull(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.MagnetLost, 0, "", "", ""), null, CraftInstruments.None), "without a magnet nothing shows it");
            Assert.IsNull(CraftJournal.InsightOf(new CraftEvent(CraftEventKind.Note, 0, "", CraftSmelting.FumesId + "lead", ""), null), "the lesson is zinc's");
        }

        [Test]
        public void SmithingInsights_ComeFromTheWork()
        {
            // the magnet: steel heated past its Curie heat, where there is a magnet to show it
            CraftWorkshop m = Workshop();
            string bar = Bar(m, "Tool steel", 200, 30, 5);
            List<string> withMagnet = Learning(m, CraftInstruments.Magnet), without = Learning(m, CraftInstruments.Thermometer);
            LightForge(m);
            HeatTo(m, bar, 820);
            CollectionAssert.Contains(withMagnet, "magnet");
            CollectionAssert.DoesNotContain(without, "magnet");

            // temper colours: bright steel warming in the oven
            CraftWorkshop o = Workshop();
            string blade = Bar(o, "Tool steel", 200, 30, 5);
            foreach (MetalSegment s in o.Piece(blade).segments) { s.polish = 1; s.temperFilm = 18; }
            List<string> fromOven = Learning(o);
            Assert.IsTrue(o.SetOven(230, out string why), why);
            o.RunSteps(400);
            Assert.IsTrue(o.MovePiece(blade, CraftLocation.Oven, out why), why);
            o.RunSteps(1500);
            CollectionAssert.Contains(fromOven, "temper_colours");
            Assert.IsTrue(o.RecentEvents.Any(e => e.Id == "temper_colours" && e.Text.Length > 0), "the colours are seen");

            // scale hammered in
            CraftWorkshop sc = Workshop();
            string scaled = Bar(sc, "Mild steel", 200, 30, 8);
            SetHeat(sc.Piece(scaled), 1000);
            foreach (MetalSegment s in sc.Piece(scaled).segments) s.scale = 0.4;
            List<string> fromScale = Learning(sc);
            Strikes(sc, scaled, 6);
            CollectionAssert.Contains(fromScale, "scale_pits");
            Assert.AreEqual(1, sc.RecentEvents.Count(e => e.Kind == CraftEventKind.Defect && e.Id == nameof(CraftDefectKind.ScalePit)), "told the first time it shows, not every blow");

            // a quench side to side warps the blade; up and down it does not
            foreach (CraftQuenchMotion motion in new[] { CraftQuenchMotion.SideToSide, CraftQuenchMotion.UpDown })
            {
                CraftWorkshop q = Workshop(medium: "oil");
                string b = Bar(q, "Tool steel", 250, 35, 4);
                List<string> fromQuench = Learning(q);
                LightForge(q);
                HeatTo(q, b, 820);
                Assert.IsTrue(q.MovePiece(b, CraftLocation.Quench, out why), why);
                q.Agitate(b, motion, out _);
                q.RunSteps(400);
                if (motion == CraftQuenchMotion.SideToSide)
                    CollectionAssert.Contains(fromQuench, "warp", $"bent {CraftEvaluator.Piece(q.Rules, q.Piece(b)).Bend:0.0}°");
                else
                    CollectionAssert.DoesNotContain(fromQuench, "warp", $"bent {CraftEvaluator.Piece(q.Rules, q.Piece(b)).Bend:0.0}°");
            }

            // a seam never fluxed cools unset: without flux a weld does not take; fluxed, the lesson is not that
            foreach (bool flux in new[] { false, true })
            {
                CraftWorkshop f = Workshop();
                string folded = Bar(f, "Mild steel", 200, 30, 6);
                SetHeat(f.Piece(folded), 1250);
                if (flux) Assert.IsTrue(f.AddFlux("borax", 2, 2, out why, folded), why);
                List<string> fromFold = Learning(f);
                Assert.IsTrue(f.Fold(folded, out why, 1, true), why);
                SetHeat(f.Piece(folded), 400);
                f.RunSteps(2);
                Assert.IsTrue(f.RecentEvents.Any(e => e.Id == "seam"), "the seam cooled unset");
                if (flux) CollectionAssert.DoesNotContain(fromFold, "wrong_flux");
                else CollectionAssert.Contains(fromFold, "wrong_flux");
            }
        }

        [Test]
        public void ZincBoilingAway_TeachesCementation()
        {
            CraftWorkshop w = Workshop();
            List<string> learnt = Learning(w);
            w.AddFuel("charcoal", 10, out _, true);
            string oid = w.AddPiece(CraftOres.NewLump(w.Rules, "calamine", 3, 0));
            Assert.IsTrue(w.MovePiece(oid, CraftLocation.Furnace, out string why), why);
            for (int t = 0; t < 600; t++)
            {
                if (w.Furnace.fire.temperature < 1150) w.Bellows(0.7, true);
                if (t % 60 == 0) w.AddFuel("charcoal", 2, out _, true);
                w.RunSteps(10);
            }
            CollectionAssert.Contains(learnt, "cementation");
        }

        [Test]
        public void LeatherInsights_ComeFromTheWork()
        {
            // a raw hide left in plain water rots
            CraftWorkshop r = Workshop();
            string raw = Fleshed(r, "deer");
            List<string> fromRot = Learning(r);
            Soak(r, raw, 0, "water", 1, 120);
            CollectionAssert.Contains(fromRot, "rot");

            // a raw hide in a full-strength bark liquor closes its outside
            CraftWorkshop c = Workshop();
            string strong = Fleshed(c, "deer");
            List<string> fromStrong = Learning(c);
            Soak(c, strong, 0, "bark", 8, 30);
            CollectionAssert.Contains(fromStrong, "case_hardened");

            // tanned, then into scalding water: it shrinks; dried unstaked: stiff
            CraftWorkshop s = Workshop();
            string tanned = Fleshed(s, "deer");
            Soak(s, tanned, 0, "bark", 3, 120);
            string dried = s.AddPiece(s.Piece(tanned).Clone());
            List<string> fromHeat = Learning(s);
            Assert.IsTrue(s.FillVat(1, "water", 1, out string why), why);
            Assert.IsTrue(s.SetVatHeat(1, 1, out why), why);
            Hours(s, 2);
            Assert.IsTrue(s.MovePiece(tanned, CraftLocation.Vat, out why, 1, 0, 9999, 1), why);
            s.RunSteps(300);
            CollectionAssert.Contains(fromHeat, "shrink");
            Hours(s, 200);
            Assert.IsTrue(s.Piece(dried).hide.dried, "it has dried in the air");
            CollectionAssert.Contains(fromHeat, "stiff_leather");
            Assert.IsTrue(s.RecentEvents.Any(e => e.Kind == CraftEventKind.Dried && e.Text == "It has dried stiff."), "the words say so");

            // alum-tawed leather put back in water gives its tawing up to it
            CraftWorkshop a = Workshop();
            string tawed = Fleshed(a, "deer");
            Soak(a, tawed, 0, "alum", 3, 40);
            List<string> fromAlum = Learning(a);
            Soak(a, tawed, 1, "water", 1, 30);
            CollectionAssert.Contains(fromAlum, "alum");

            // dye over oil takes in blotches
            CraftWorkshop d = Workshop();
            string leather = Fleshed(d, "deer");
            Soak(d, leather, 0, "bark", 3, 120);
            Hours(d, 200);
            List<string> fromDye = Learning(d);
            Assert.IsTrue(d.Condition(leather, "neatsfoot_oil", AllOver(d, leather), 1, out why), why);
            Assert.IsTrue(d.Condition(leather, "neatsfoot_oil", AllOver(d, leather), 1, out why), why);
            CollectionAssert.DoesNotContain(fromDye, "blotch");
            Assert.IsTrue(d.Dye(leather, "dye_brown", AllOver(d, leather), 1, out why), why);
            CollectionAssert.Contains(fromDye, "blotch", $"oil {d.Piece(leather).hide.cells.Where(x => x.present).Average(x => x.oil):0.00}");
        }

        // ------------------------------------------------------------------ r05/r14: what a finished item teaches, and in which craft
        [Test]
        public void FinishedItems_TeachNormalising_AndALooseJointInTheirOwnCraft()
        {
            CraftRules rules = Rules();
            var d = new CraftJournalData();
            // a sword whose steel blade was quenched straight from a long forging heat: coarse-grained, the inspection says
            CraftDesign arming = rules.Design("arming_sword");
            (string, double)[] tool = Metal("Tool steel");
            var parts = new Dictionary<string, List<CraftPiece>>
            {
                ["blade"] = new List<CraftPiece> { CraftDefaults.Bar(rules, 760, 40, 5, tool) },
                ["guard"] = new List<CraftPiece> { CraftDefaults.Bar(rules, 200, 20, 8, tool) },
                ["grip"] = new List<CraftPiece> { CraftDefaults.WoodBlank(rules, "maple", 120, 30, 30) },
                ["pommel"] = new List<CraftPiece> { CraftDefaults.Bar(rules, 45, 45, 18, tool) },
            };
            foreach (MetalSegment s in parts["blade"][0].segments) s.edge.grain = s.body.grain = 2.6;
            CraftItemReport sword = CraftEvaluator.Item(rules, arming, parts, new AssemblyState { design = arming.id });
            Assert.IsTrue(sword.CoarseGrained);
            Assert.IsTrue(sword.Notes.Any(n => n.Contains("coarse")), string.Join(" | ", sword.Notes));
            sword.Assembly = 0.3; // and its guard rattles
            List<string> lines = CraftJournal.Made(d, sword, new List<string>(), 1);
            Assert.IsTrue(CraftJournal.HasInsight(d, "normalise"), string.Join(" | ", lines));
            Assert.IsTrue(CraftJournal.HasInsight(d, "loose_joint"));
            double smith = CraftMastery.Note(d.mastery, CraftDiscipline.Smithing)?.xp ?? 0;
            Assert.Greater(smith, 0.9, "both lessons are a smith's");
            Assert.IsNull(CraftMastery.Note(d.mastery, CraftDiscipline.Leatherwork), "a sword's loose guard is not leatherwork");

            var e = new CraftJournalData();
            var belt = new CraftItemReport { Design = rules.Design("belt"), Name = "Belt", Tier = "Common", Quality = 0.5, Assembly = 0.3 };
            CraftJournal.Made(e, belt, new List<string>(), 1);
            Assert.Greater(CraftMastery.Note(e.mastery, CraftDiscipline.Leatherwork)?.xp ?? 0, 0, "a belt's loose buckle is the leatherworker's lesson");
            Assert.IsFalse(CraftJournal.HasInsight(e, "normalise"), "nothing coarse in it");

            Assert.AreEqual(CraftDiscipline.Smithing, CraftMastery.OfInsight("warp"), "a quench's warp is the smith's");
            Assert.AreNotEqual(CraftDiscipline.Leatherwork, CraftMastery.OfInsight("loose_joint"));

            // a fresh bar is fine-grained; a bronze casting's coarse grain is no steel's lesson
            Assert.AreEqual(1.0, CraftEvaluator.MeanGrain(CraftDefaults.Bar(rules, 100, 20, 5, Metal("Tool steel"))), 1e-9);
            var bronzeParts = new Dictionary<string, List<CraftPiece>>(parts);
            bronzeParts["blade"] = new List<CraftPiece> { CraftDefaults.Bar(rules, 760, 40, 5, Metal("Bronze")) };
            foreach (MetalSegment s in bronzeParts["blade"][0].segments) s.edge.grain = s.body.grain = 2.6;
            Assert.IsFalse(CraftEvaluator.Item(rules, arming, bronzeParts, new AssemblyState { design = arming.id }).CoarseGrained, "only steel is normalised");
        }

        // ------------------------------------------------------------------ r01/r11: fibre, yarn and cloth by one chain of worth
        private static string Spun(CraftWorkshop w, string fibre, int grams)
        {
            string f = w.AddPiece(CraftFibre.NewFibre(w.Rules, fibre));
            string y = w.SpinFibre(f, 0.85, 0.5, 0.6, grams, out string why);
            Assert.IsNotNull(y, why);
            return y;
        }

        [Test]
        public void Spinning_AddsALittle_AndWeaving_KeepsTheYarnsWorth()
        {
            CraftWorkshop w = Workshop();
            double fibre = 0;
            string a = null;
            for (int k = 0; k < 8; k++)
            {
                string f = w.AddPiece(CraftFibre.NewFibre(w.Rules, "wool_fibre"));
                fibre += CraftValue.Of(w.Rules, w.Piece(f)) * 30 / w.Piece(f).fibre.grams;
                string y = w.SpinFibre(f, 0.85, 0.5, 0.6, 30, out string why);
                Assert.IsNotNull(y, why);
                if (a == null) a = y;
                else Assert.IsTrue(w.JoinYarn(a, y, out why), why);
            }
            double yarn = CraftValue.Of(w.Rules, w.Piece(a));
            Assert.Greater(yarn, fibre, "spinning adds to the fibre's worth");
            Assert.Less(yarn, fibre * CraftRules.SpinningAddsAtMost, $"but not many times: {fibre:0.00} of fibre spun to {yarn:0.00} of yarn");
            int rows = CraftFibre.RowsFor(w.Piece(a).yarn.length, 8);
            string cloth = w.WeaveCloth(a, 8, 0.9, 0.5, Enumerable.Repeat(0.9f, rows).ToList(), out string why2);
            Assert.IsNotNull(cloth, why2);
            double after = CraftValue.Of(w.Rules, w.Piece(cloth)) + (w.Piece(a) != null ? CraftValue.Of(w.Rules, w.Piece(a)) : 0);
            Assert.Greater(after, yarn, $"weaving adds to the yarn's worth: {yarn:0.00} of yarn woven to {after:0.00}");

            // every built-in fibre: its finest thread for a bolt's yarn is worth less than the bolt, and no skein is many times its fibre
            CraftRules r = w.Rules;
            foreach (CraftSupplyDefinition s in r.Supplies.Where(x => x.kind == CraftSupplyKind.Fibre && x.spinsInto != null && x.spinsInto.Count > 0))
            {
                CraftHideDefinition bolt = r.Hide(s.weavesInto);
                double dearest = s.spinsInto.Max(t => r.Supply(t)?.price ?? 0);
                Assert.Less(dearest * bolt.width * bolt.height * CraftFibre.MetresPerCell / CraftFibre.MetresPerThread, bolt.price, $"{bolt.id}: its yarn is worth less than the bolt");
            }
            Assert.IsFalse(r.CompileWarnings.Any(x => x.Contains("prints money") || x.Contains("weaving throws")), string.Join("\n", r.CompileWarnings));
            // the check finds a thread priced at its old 20-to-1
            CraftContent content = CraftDefaults.Content();
            content.supplies.First(x => x.id == "fine_wool_yarn").price = 1.5f;
            CraftRules dear = CraftRules.Compile(new CraftSimulationSettings(), content);
            Assert.IsTrue(dear.CompileWarnings.Any(x => x.Contains("fine_wool_yarn") && x.Contains("prints money")), string.Join("\n", dear.CompileWarnings));
            Assert.IsTrue(dear.CompileWarnings.Any(x => x.Contains("wool_cloth") && x.Contains("weaving throws")), string.Join("\n", dear.CompileWarnings));
        }

        [Test]
        public void AWovenLength_IsWorthItsCells_CutOrWhole()
        {
            CraftWorkshop w = Workshop();
            string a = Spun(w, "wool_fibre", 30);
            for (int k = 0; k < 50; k++) Assert.IsTrue(w.JoinYarn(a, Spun(w, "wool_fibre", 30), out string why), why);
            int rows = CraftFibre.RowsFor(w.Piece(a).yarn.length, 16);
            string cloth = w.WeaveCloth(a, 16, 0.9, 0.5, Enumerable.Repeat(0.9f, rows).ToList(), out string why2);
            Assert.IsNotNull(cloth, why2);
            CraftPiece c = w.Piece(cloth);
            int cells = c.hide.cells.Count(x => x.present), bolt = CraftValue.WholeHideCells(w.Rules, c.hide);
            Assert.Greater(cells, bolt, "longer than a bolt");
            CraftPiece half1 = c.Clone(), half2 = c.Clone();
            for (int k = 0; k < half1.hide.cells.Count; k++) { half1.hide.cells[k].present = k < cells / 2; half2.hide.cells[k].present = k >= cells / 2; }
            double whole = CraftValue.Of(w.Rules, c), halves = CraftValue.Of(w.Rules, half1) + CraftValue.Of(w.Rules, half2);
            Assert.AreEqual(halves, whole, 1e-6, "cut in two, its halves are worth the whole");
            Assert.AreEqual(w.Rules.Hide("wool_cloth").price * cells / (double)bolt, whole, 1e-6, "worth its cells");
            // a hide is still never worth more than the whole hide it came from
            CraftPiece deer = CraftLeather.NewHide(w.Rules, "deer", 3);
            Assert.AreEqual(w.Rules.Hide("deer").price, CraftValue.Of(w.Rules, deer), 1e-6);
        }

        // ------------------------------------------------------------------ r02: a dear metal alloyed into a cheap one
        [Test]
        public void ADearMetal_AlloyedIntoACheapOne_IsWorthItsParts()
        {
            CraftWorkshop w = Workshop();
            CraftPiece my = CraftDefaults.Bar(w.Rules, 200, 30, 6, Metal("Mythril"));
            CraftPiece fe = CraftDefaults.Bar(w.Rules, 200, 30, 6, Metal("Wrought iron"));
            SetHeat(my, 1150);
            SetHeat(fe, 1150);
            double before = CraftValue.Of(w.Rules, my) + CraftValue.Of(w.Rules, fe);
            string a = w.AddPiece(fe), b = w.AddPiece(my);
            Assert.IsTrue(w.Weld(a, b, out string why), why);
            CraftPiece welded = w.Piece(a);
            Assert.AreEqual("mythril_steel", welded.Profile(w.Rules).Class?.id);
            double after = CraftValue.Of(w.Rules, welded);
            Assert.LessOrEqual(after, before * 1.05, $"{before:0.0} welded into {after:0.0}");
            Assert.Greater(after, before * 0.95, "and about what its parts were");

            // orichalcum melted into copper: a fine bronze, not twice the orichalcum
            CraftRules r = w.Rules;
            CraftPiece ori = CraftDefaults.Bar(r, 100, 30, 6, (CraftDefaults.Orichalcum, 1.0));
            CraftPiece cu = CraftDefaults.Bar(r, 100, 30, 6, Metal("Copper"));
            double parts = CraftValue.Of(r, ori) * 0.25 + CraftValue.Of(r, cu) * 0.75;
            CraftPiece bronze = CraftDefaults.Bar(r, 100, 30, 6, (CraftDefaults.Copper, 0.75), (CraftDefaults.Orichalcum, 0.25));
            Assert.AreEqual("orichalcum_bronze", bronze.Profile(r).Class?.id);
            Assert.Less(CraftValue.Of(r, bronze) / bronze.Mass(r), parts / 100 * 1.6 / (cu.Mass(r) / 100), "no doubling");
            double perGram = CraftValue.Of(r, bronze) / bronze.Mass(r);
            double partsPerGram = 0.25 * CraftValue.Of(r, ori) / ori.Mass(r) + 0.75 * CraftValue.Of(r, cu) / cu.Mass(r);
            Assert.Less(perGram, partsPerGram * 1.05, "an orichalcum bronze is worth about its parts, gram for gram");

            // the making of a steel still doubles its iron's worth
            CraftPiece tool = CraftDefaults.Bar(r, 200, 30, 6, Metal("Tool steel")), iron = CraftDefaults.Bar(r, 200, 30, 6, Metal("Wrought iron"));
            Assert.Greater(CraftValue.Of(r, tool) / tool.Mass(r), 2 * CraftValue.Of(r, iron) / iron.Mass(r), "carburizing more than doubles iron's worth");
        }

        // ------------------------------------------------------------------ r13: a stock metal sits well inside its class
        [Test]
        public void StockMetals_SitWellInsideTheirClasses()
        {
            // a twentieth more or less of any element alloyed in (what the forge's scale or a melt's loss takes) names it the same
            CraftRules r = Rules();
            foreach (var m in CraftDefaults.StockMetals())
            {
                string cls = CraftDefaults.Bar(r, 200, 30, 6, m.composition).Profile(r).Class?.id;
                for (int k = 1; k < m.composition.Length; k++)
                    foreach (double by in new[] { 0.95, 1.05 })
                    {
                        var c = m.composition.Select(x => (x.element, x.fraction)).ToArray();
                        double d = c[k].fraction * (by - 1);
                        c[k].fraction += d;
                        c[0].fraction -= d;
                        Assert.AreEqual(cls, CraftDefaults.Bar(r, 200, 30, 6, c).Profile(r).Class?.id, $"{m.name}: {by:0.00} × its {c[k].element}");
                    }
            }
            Assert.AreEqual("mythril_metal", CraftDefaults.Bar(r, 200, 30, 6, Metal("Mythril")).Profile(r).Class?.id, "sold as Mythril, it assays as Mythril");
        }

        // ------------------------------------------------------------------ r17: green wood weighs its water
        [Test]
        public void GreenWood_WeighsItsWater()
        {
            CraftRules r = Rules();
            CraftPiece log = CraftDefaults.Log(r, "ash");
            double green = log.Mass(r);
            log.wood.moisture = CraftWoodwork.Seasoned;
            double dry = log.Mass(r);
            Assert.AreEqual(1.6 / 1.12, green / dry, 1e-6, "green (0.6) against seasoned (0.12): the water's weight");
            CraftPiece blank = CraftDefaults.WoodBlank(r, "ash");
            CraftWoodDefinition ash = r.Wood("ash");
            Assert.AreEqual(ash.blankLength * ash.blankWidth * ash.blankThickness * ash.density / 1000.0, blank.Mass(r), 0.01, "a seasoned blank weighs its species' density");
        }

        // ------------------------------------------------------------------ r06/r12/r20: practice
        [Test]
        public void Practice_ComesFromWorkThatDoesSomething()
        {
            foreach (CraftRequestKind kind in new[] { CraftRequestKind.TestGem, CraftRequestKind.EvaluateGem, CraftRequestKind.OrientGem, CraftRequestKind.MeasureFit, CraftRequestKind.LayoutPattern })
                Assert.AreEqual(0, CraftMastery.WorkExperience(new CraftRequest(kind, "p")), $"{kind}: reading, measuring and planning teach nothing by themselves");
            CraftWorkshop w = Workshop();
            string y = Spun(w, "wool_fibre", 30);
            var count = new CraftRequest(CraftRequestKind.MeasureYarn, y);
            Assert.Greater(CraftMastery.WorkExperience(count, w.Piece(y)), 0, "winding and counting a skein is work");
            Assert.IsTrue(w.MeasureYarn(y, w.Piece(y).yarn.length, out string why), why);
            Assert.AreEqual(0, CraftMastery.WorkExperience(count, w.Piece(y)), "counting the same skein again is not");
            var stone = new CraftPiece { id = "g", kind = CraftPieceKind.Gem };
            var name = new CraftRequest(CraftRequestKind.IdentifyGem, "g");
            Assert.Greater(CraftMastery.WorkExperience(name, stone), 0, "naming a stone is");
            stone.gem.identified = true;
            Assert.AreEqual(0, CraftMastery.WorkExperience(name, stone), "naming it again is not");

            // an hour at the refractometer on one stone now teaches nothing; a ring does
            var notes = new List<CraftMasteryNote>();
            for (int k = 0; k < 600; k++)
                CraftMastery.Practise(notes, CraftDiscipline.Jewellery, CraftMastery.WorkExperience(new CraftRequest(CraftRequestKind.TestGem, "g"), stone), true, k * 6.0);
            Assert.AreEqual(0, CraftMastery.Note(notes, CraftDiscipline.Jewellery)?.xp ?? 0, 1e-9);
            Assert.Greater(CraftMastery.ItemExperience(Rules().Design("bezel_ring"), 0.9), 1);
        }

        [Test]
        public void ItemPractice_FollowsTheWorkInTheItem_AndRepeatsTeachLess()
        {
            CraftRules r = Rules();
            double sash = CraftMastery.ItemExperience(r.Design("sash"), 0.5), gambeson = CraftMastery.ItemExperience(r.Design("padded_gambeson"), 0.5);
            double sword = CraftMastery.ItemExperience(r.Design("arming_sword"), 0.5), greatsword = CraftMastery.ItemExperience(r.Design("greatsword"), 0.5);
            Assert.Greater(gambeson / sash, 3.0, "a quilted gambeson is many times the work of a hemmed sash");
            Assert.Greater(sword, gambeson);
            Assert.Greater(greatsword, sword);
            Assert.AreEqual(sash / 2, CraftMastery.ItemExperience(r.Design("sash"), 0.5, null, 15), 1e-9, "the sixteenth sash teaches half the first");
            double toMaster = CraftMastery.ExperienceFor(new CraftMasterySettings().master);
            double sashes = 0;
            for (int k = 0; k < 200; k++) sashes += CraftMastery.ItemExperience(r.Design("sash"), 0.9, null, k);
            Assert.Less(sashes, toMaster / 2, "two hundred sashes are far from a master tailor");

            // an older journal turned into practice counts its repeats the same way
            var j = new CraftJournalData();
            j.designs.Add(new CraftDesignNote { designId = "sash", timesMade = 30, bestQuality = 0.5f });
            CraftMastery.Seed(j, r);
            double expect = Enumerable.Range(0, 30).Sum(k => CraftMastery.ItemExperience(r.Design("sash"), 0.5 * 0.85, null, k));
            Assert.AreEqual(expect, CraftMastery.Note(j.mastery, CraftDiscipline.Tailoring).xp, 1e-3);
        }

        [Test]
        public void TheMasteryTab_WordsTheRulesWidening()
        {
            var narrow = new CraftTimingSettings { skillWidening = 0.2f };
            StringAssert.Contains("zones 20% wider", CraftMastery.Benefits(CraftDiscipline.Smithing, 1, null, narrow)[0]);
            StringAssert.Contains("zones 35% wider", CraftMastery.Benefits(CraftDiscipline.Smithing, 1)[0], "the default timing");
            StringAssert.Contains($"Hands {Math.Round(100 * CraftHands.SkillSteadies)}% steadier", CraftMastery.Benefits(CraftDiscipline.Smithing, 1)[0]);
            var hands = new CraftHands(1, 1);
            Assert.AreEqual(0.025 * (1 - CraftHands.SkillSteadies), hands.Unsteadiness, 1e-12, "the same steadiness the hands have");
        }

        // ------------------------------------------------------------------ r07/r09/r10/r15/r19: the look
        [Test]
        public void AnUnknownMetal_IsNamedByItsWholeNounPhrase()
        {
            CraftRules r = Rules();
            var none = new CraftJournalData();
            var bars = new (string what, (string, double)[] metal, string name)[]
            {
                ("tin", Metal("Tin"), "Bar of soft, bright metal"),
                ("lead", new[] { (CraftDefaults.Lead, 1.0) }, "Bar of dull, heavy, soft metal"),
                ("mythril", new[] { (CraftDefaults.Mythril, 0.99), (CraftDefaults.Silver, 0.01) }, "Bar of pale blue, feather-light metal"),
                ("phosphoric iron", new[] { (CraftDefaults.Iron, 0.995), (CraftDefaults.Phosphorus, 0.004), (CraftDefaults.Carbon, 0.001) }, "Bar of bright, hard iron"),
                ("copper matte", new[] { (CraftDefaults.Copper, 0.8), (CraftDefaults.Sulfur, 0.2) }, "Bar of dark, brittle, metallic lump"),
                ("mythril steel", new[] { (CraftDefaults.Iron, 0.8), (CraftDefaults.Mythril, 0.2) }, "Bar of pale steel"),
                ("mild steel", Metal("Mild steel"), "Bar of grey metal"),
            };
            foreach (var (what, metal, name) in bars)
            {
                CraftPiece p = CraftDefaults.Bar(r, 200, 30, 6, metal);
                Assert.AreEqual(name, CraftLook.Name(r, p, none), what);
                string it = CraftLook.Describe(r, p, none, CraftInstruments.None, false, false).First(l => l.StartsWith("It is "));
                Assert.AreEqual($"It is a {name.Substring("Bar of ".Length)}.", it, what);
            }
            CraftPiece rivets = CraftDefaults.FastenerSet(r, CraftFastenerKind.Rivet, 12, 4, (CraftDefaults.Tin, 1.0));
            Assert.AreEqual("12 rivets of soft, bright metal", CraftLook.Name(r, rivets, none));
        }

        [Test]
        public void TheGlow_SaysWhereTheHotStretchIs()
        {
            CraftRules r = Rules();
            CraftPiece p = CraftDefaults.Bar(r, 400, 30, 8, Metal("Mild steel"));
            int n = p.segments.Count;
            for (int i = n / 2 - 1; i <= n / 2; i++) { p.segments[i].edge.temp = 1010; p.segments[i].body.temp = 990; }
            string glow = CraftLook.Describe(r, p, new CraftJournalData(), CraftInstruments.None, false, false).First(l => l.StartsWith("It glows"));
            StringAssert.Contains("in the middle", glow);
            CraftPiece q = CraftDefaults.Bar(r, 400, 30, 8, Metal("Mild steel"));
            q.segments[n - 1].edge.temp = 1010; q.segments[n - 1].body.temp = 990;
            StringAssert.Contains("at the far end", CraftLook.Describe(r, q, new CraftJournalData(), CraftInstruments.None, false, false).First(l => l.StartsWith("It glows")));
        }

        [Test]
        public void TheBalance_WeighsAnItemWithItsParts()
        {
            CraftWorkshop w = Workshop();
            string asm = w.StartAssembly("hatchet", out string why);
            Assert.IsNotNull(asm, why);
            string haft = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 430, 32, 32));
            Assert.IsTrue(w.Attach(asm, "haft", 0, haft, out why), why);
            double grams = w.Piece(haft).Mass(w.Rules);
            List<string> lines = CraftLook.Describe(w.Rules, w.Piece(asm), new CraftJournalData(), CraftInstruments.Scale, false, false, w);
            Assert.IsTrue(lines.Any(l => l.Contains($"Balance: {CraftText.Mass(grams)} with its parts")), string.Join(" / ", lines));
            Assert.IsFalse(CraftLook.Describe(w.Rules, w.Piece(asm), new CraftJournalData(), CraftInstruments.Scale, false, false).Any(l => l.Contains("Balance")),
                "without the workshop's parts it is not weighed as nothing");
        }

        [Test]
        public void AnOreLump_WeighsInTheHandAsItIs()
        {
            CraftRules r = Rules();
            CraftPiece quartz = CraftOres.NewLump(r, "gold_quartz", 12345, 0);
            Assert.Less(CraftLook.BulkDensity(r.Ore("gold_quartz"), quartz.ore), 3.2, "mostly quartz");
            List<string> lines = CraftLook.Describe(r, quartz, new CraftJournalData(), CraftInstruments.None, false, false);
            Assert.IsFalse(lines.Any(l => l.Contains("heavy")), string.Join(" / ", lines));
            CraftPiece star = CraftOres.NewLump(r, "meteorite", 7, 0);
            Assert.IsTrue(CraftLook.Describe(r, star, new CraftJournalData(), CraftInstruments.None, false, false).Any(l => l.Contains("heavy")), "iron from the sky is heavy");
        }

        [Test]
        public void PlankWoods_AreNamedAsPlanksAndLogs()
        {
            CraftRules r = Rules();
            var none = new CraftJournalData();
            Assert.AreEqual("Linden log", CraftLook.Name(r, CraftDefaults.Log(r, "linden"), none));
            Assert.AreEqual("Linden plank", CraftLook.Name(r, CraftDefaults.WoodBlank(r, "linden"), none));
            Assert.AreEqual("Ash log", CraftLook.Name(r, CraftDefaults.Log(r, "ash"), none));
            Assert.AreEqual("Ash blank", CraftLook.Name(r, CraftDefaults.WoodBlank(r, "ash"), none));
        }

        // ------------------------------------------------------------------ r08: the journal's procedure in the player's words
        [Test]
        public void Procedure_WordsHeatsAndVats_AsThePlayerKnewThem()
        {
            CraftWorkshop w = Workshop();
            Assert.IsTrue(w.FillVat(0, "bark", 2, out string why), why);
            string hide = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 3));
            Assert.IsTrue(w.MovePiece(hide, CraftLocation.Vat, out why, vat: 0), why);
            string rod = w.AddPiece(CraftDefaults.Bar(w.Rules, 150, 8, 8, Metal("Mild steel")));
            string rivets = w.FormFastener(rod, CraftFastenerKind.Rivet, out why);
            Assert.IsNotNull(rivets, why);
            var journal = new CraftJournalData();
            List<string> steps = CraftJournal.Procedure(w, w.Piece(hide), journal).Concat(CraftJournal.Procedure(w, w.Piece(rivets), journal)).ToList();
            Assert.IsFalse(steps.Any(l => l.Contains("°C") || l.Contains("% strength")), string.Join(" | ", steps));
            Assert.IsTrue(steps.Any(l => l.StartsWith("Put in oak bark liquor (") && (l.Contains("weak") || l.Contains("medium") || l.Contains("strong")) && l.Contains("cool")), string.Join(" | ", steps));
            Assert.IsTrue(steps.Any(l => l.StartsWith("Made ") && l.EndsWith(" cold")), string.Join(" | ", steps));

            // a heat by its glow - and, once the thermometer has taught the colour, with what it read
            var pour = new CraftRecordEntry { kind = CraftOpKind.Pour, text = "Cast in a sand mould at 1050 °C" };
            string glow = CraftMetallurgy.HeatName(1050, false);
            Assert.AreEqual($"Cast in a sand mould at {glow}", CraftJournal.Worded(pour, journal, w.Rules));
            for (int k = 0; k < w.Rules.Settings.readingsToLearnHeat; k++) CraftJournal.ReadHeat(journal, w.Rules, glow, 1040 + k);
            StringAssert.Contains("°C)", CraftJournal.Worded(pour, journal, w.Rules), "learnt, the colour carries the heats read");
            Assert.AreEqual("Hardened in hot water (hot)", CraftJournal.Worded(new CraftRecordEntry { kind = CraftOpKind.SetVatHeat, text = "Hardened in hot water at 78 °C" }, journal, w.Rules));
        }
    }
}
#endif
