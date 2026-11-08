#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of what the station window shows from the model, after the third review of the window: the guide
    /// holds a wooden part to the length it is judged by (a stock blank is sawn to it before it is fitted, a part fitted
    /// at the wrong length is taken off again, and the item is never "finished" so); firewood burns the cheapest wood
    /// first; the arm's steps and words change together, in the work's own terms; the workshop's events are sentences and
    /// a piece's origin is words, never an id; every name of the content has its glyphs in the surfaces' one lettering,
    /// whose cutting and wrapping keep to the room given.
    /// </summary>
    public class CraftReviewShellTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "review-shell");

        private static CraftGuide.Step? Next(CraftWorkshop w, CraftTools t, string id) => CraftGuide.Next(w, t, w.Piece(id), new CraftJournalData());

        // ------------------------------------------------------------------ the guide: a wooden part's length
        [Test]
        public void Guide_AStockBlankMeantForAPart_IsSawnToItsLength_WhereTheStationCanSawIt()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            string stave = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash")); // the 2.2 m stock stave
            Assert.AreEqual(2200, w.Piece(stave).Length, 1);
            Assert.IsTrue(w.SetIntent(stave, "hatchet", "haft", out string why), why);
            CraftGuide.Step s = Next(w, CraftTools.Woodshop, stave).Value;
            StringAssert.StartsWith("Saw it to length", s.text);
            StringAssert.Contains("380-480 mm", s.text, "the length the haft is judged by");
            StringAssert.Contains("2200 mm", s.text);
            Assert.AreEqual(CraftGuide.Carve, s.area, "the carving bench dashes the part's length over the blank");
            Assert.AreEqual("Saw", s.place);
            CollectionAssert.Contains(CraftGuide.AreaKeys, s.area);

            // a sawhorse without the carving knives and files: the woodshop's saw; neither: somewhere else, no Go
            CraftTools horse = CraftTools.Sawhorse | CraftTools.ChoppingBlock;
            CraftGuide.Step h = Next(w, horse, stave).Value;
            Assert.AreEqual(CraftGuide.Woodshop, h.area);
            Assert.AreEqual("Saw", h.place);
            CraftGuide.Step none = Next(w, CraftTools.Tannery, stave).Value;
            Assert.AreEqual("", none.area);
            StringAssert.Contains("sawhorse or a carving bench", none.text);

            // a linden plank meant for a round shield's board
            string plank = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "linden"));
            Assert.IsTrue(w.SetIntent(plank, "round_shield", "board", out why), why);
            StringAssert.StartsWith("Saw it to length", Next(w, CraftTools.Woodshop, plank).Value.text);

            // sawn to length, nothing more is asked of its length
            string offcut = w.SawCut(stave, 430.0 / 2200.0, 0.05, 0.95, out why);
            Assert.IsNotNull(offcut, why);
            Assert.AreEqual(430, w.Piece(stave).Length, 2);
            CraftGuide.Step carve = Next(w, CraftTools.Woodshop, stave).Value;
            StringAssert.StartsWith("Carve it to its shape", carve.text, "a seasoned haft blank of the right length: its shape next (the review of the wood)");
            Assert.AreEqual(CraftGuide.Carve, carve.area);
            Assert.IsNull(Next(w, CraftTools.Woodshop, offcut), "the offcut is meant for nothing in particular");
        }

        [Test]
        public void Guide_SawsBeforeItBends_AndSaysWhenABlankIsTooShort()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            // a snath (1400-1700 mm, a 25° sweep) from the stock stave: sawn first, since the bend is in the length it keeps
            string snath = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash"));
            Assert.IsTrue(w.SetIntent(snath, "scythe", "haft", out string why), why);
            w.Piece(snath).wood.moisture = 0.5;
            StringAssert.StartsWith("Saw it to length", Next(w, CraftTools.Woodshop, snath).Value.text);
            Assert.IsNotNull(w.SawCut(snath, 1550.0 / 2200.0, 0.05, 0.95, out why), why);
            CraftGuide.Step bend = Next(w, CraftTools.Woodshop, snath).Value;
            Assert.AreEqual("Bend", bend.place, bend.text);

            // too short for the part: no cut lengthens it (and no Go)
            string stub = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "hickory", 300, 32, 32));
            Assert.IsTrue(w.SetIntent(stub, "hatchet", "haft", out why), why);
            CraftGuide.Step s = Next(w, CraftTools.Woodshop, stub).Value;
            StringAssert.StartsWith("Too short", s.text);
            StringAssert.Contains("longer blank", s.text);
            Assert.AreEqual("", s.area);
        }

        [Test]
        public void Guide_LengthStep_KeepsToTheEvaluatorsOwnNote()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            CraftComponentSpec haft = w.Rules.Design("hatchet").Component("haft");
            Assert.IsTrue(CraftWoodwork.LengthWanted(haft, out double min, out double max));
            Assert.AreEqual(haft.shape.lengthMin, min, 1e-6);
            Assert.AreEqual(haft.shape.lengthMax, max, 1e-6);
            foreach (double length in new[] { max, max + 5, max + 9, max + 20, min - 9, min - 5 })
            {
                string id = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "hickory", length, 32, 32));
                Assert.IsTrue(w.SetIntent(id, "hatchet", "haft", out string why), why);
                var notes = new List<string>();
                CraftEvaluator.ShapeScore(w.Rules, w.Piece(id), haft.shape, notes);
                bool noted = notes.Any(n => n.StartsWith("too long") || n.StartsWith("too short"));
                CraftGuide.Step? s = Next(w, CraftTools.Woodshop, id);
                bool asked = s.HasValue && (s.Value.text.StartsWith("Saw it to length") || s.Value.text.StartsWith("Too short"));
                Assert.AreEqual(noted, asked, $"{length:0} mm: the item notes its length ({noted}) exactly when the guide asks for it ({s?.text})");
                Assert.AreEqual(noted, CraftWoodwork.LengthOff(haft, length));
            }
            Assert.IsFalse(CraftWoodwork.LengthWanted(w.Rules.Design("hatchet").Component("head"), out _, out _), "a metal part is not held to a wooden length");
        }

        [Test]
        public void Guide_AQuarterstaffFittedWithTheStockStave_IsTakenApartAndSawn_BeforeItIsFinished()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            string qs = w.StartAssembly("quarterstaff", out string why);
            Assert.IsNotNull(qs, why);
            CraftGuide.Step first = Next(w, CraftTools.Woodshop, qs).Value;
            Assert.AreEqual("Fit its parts: staff first (wood, 1700-2000 mm long)", first.text);
            string staff = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash"));
            Assert.IsTrue(w.Attach(qs, "staff", 0, staff, out why), why);
            CraftGuide.Step s = Next(w, CraftTools.Woodshop, qs).Value;
            Assert.AreEqual("Staff: too long (2200 mm, wants 1700-2000 mm) - take it off, saw it to length and fit it again", s.text);
            Assert.AreEqual(CraftGuide.Assemble, s.area);
            StringAssert.Contains("Staff: too long", string.Join("; ", CraftEvaluator.Item(w.Rules, w, w.Piece(qs)).Notes), "the item's own note");

            // taken off, sawn and fitted again: now it may be finished, and its length is no longer noted
            Assert.IsTrue(w.Detach(qs, "staff", 0, out why), why);
            CraftGuide.Step off = Next(w, CraftTools.Woodshop, staff).Value;
            Assert.AreEqual("Saw it to length (the staff wants 1700-2000 mm, it is 2200 mm)", off.text, "taken off, it is still meant for the staff");
            Assert.AreEqual(CraftGuide.Carve, off.area);
            Assert.IsNotNull(w.SawCut(staff, 1850.0 / 2200.0, 0.05, 0.95, out why), why);
            Assert.IsTrue(w.Attach(qs, "staff", 0, staff, out why), why);
            Assert.AreEqual("Finish it", Next(w, CraftTools.Woodshop, qs).Value.text);
            Assert.IsFalse(CraftEvaluator.Item(w.Rules, w, w.Piece(qs)).Notes.Any(n => n.Contains("too long")));
            Assert.IsTrue(w.Finish(qs, out why), why);

            // too short: off again, and a longer one fitted (a hatchet is put together at the smithy)
            CraftWorkshop smithy = Workshop(CraftTools.Smithy);
            string hatchet = smithy.StartAssembly("hatchet", out why);
            Assert.IsNotNull(hatchet, why);
            string stub = smithy.AddPiece(CraftDefaults.WoodBlank(smithy.Rules, "hickory", 300, 32, 32));
            Assert.IsTrue(smithy.Attach(hatchet, "haft", 0, stub, out why), why);
            CraftGuide.Step shortHaft = Next(smithy, CraftTools.Smithy, hatchet).Value;
            Assert.AreEqual("Haft: too short (300 mm, wants 380-480 mm) - take it off and fit a longer one", shortHaft.text, "before the head is asked for: no more work goes into it so");
            // the blank before it is fitted, at the smithy: its files saw it at the carving bench
            Assert.IsTrue(smithy.Detach(hatchet, "haft", 0, out why), why);
            string blank = smithy.AddPiece(CraftDefaults.WoodBlank(smithy.Rules, "hickory"));
            Assert.IsTrue(smithy.SetIntent(blank, "hatchet", "haft", out why), why);
            CraftGuide.Step saw = Next(smithy, CraftTools.Smithy, blank).Value;
            Assert.AreEqual(CraftGuide.Carve, saw.area, saw.text);
            Assert.AreEqual("Saw", saw.place);
        }

        // ------------------------------------------------------------------ firewood
        [Test]
        public void Firewood_BurnsWhatIsNamedAsFirewoodFirst_ThenTheCheapest()
        {
            CraftRules r = Rules();
            // the stock blanks by their own prices: an ironwood blank (the dearest wood) last, after a pine plank
            var bag = new List<(string name, double price)>();
            foreach (CraftWoodDefinition wd in r.Woods) bag.Add(($"{wd.Name} blank", wd.price));
            bag.Add(("Oak offcuts", 0.6));
            bag.Add(("Bundle of kindling", 2));
            var order = bag.OrderBy(b => CraftFuel.BurnOrder(b.name, b.price)).Select(b => b.name).ToList();
            CollectionAssert.AreEqual(new[] { "Oak offcuts", "Bundle of kindling" }, order.Take(2), "what is named as firewood goes first, whatever it cost");
            Assert.AreEqual("Ironwood blank", order.Last(), "the dearest blank burns last");
            double pine = CraftFuel.BurnOrder("Pine plank blank", r.Wood("pine").price), iron = CraftFuel.BurnOrder("Ironwood blank", r.Wood("ironwood").price);
            Assert.Less(pine, iron, "never an ironwood blank while a pine plank is left");
            Assert.Less(CraftFuel.BurnOrder("Firewood", 9), CraftFuel.BurnOrder("Birch blank", 0.1), "a firewood item before any other wood");
        }

        // ------------------------------------------------------------------ the lettering of the work surfaces
        /// <summary>Every name of the content a work surface may write (a design, its parts and joints, a metal, a wood, a stone...).</summary>
        private static List<string> ContentNames(CraftRules r)
        {
            var names = new List<string>();
            names.AddRange(r.Elements.Select(x => x.Name));
            names.AddRange(r.Classes.Select(x => x.Name));
            names.AddRange(r.Ores.Select(x => x.Name));
            names.AddRange(r.Fuels.Select(x => x.Name));
            names.AddRange(r.Quenchants.Select(x => x.Name));
            names.AddRange(r.Agents.Select(x => x.Name));
            names.AddRange(r.Hides.Select(x => x.Name));
            names.AddRange(r.Woods.Select(x => x.Name));
            names.AddRange(r.Supplies.Select(x => x.Name));
            names.AddRange(r.Gems.Select(x => x.Name));
            foreach (CraftGemCut cut in r.Cuts)
            {
                names.Add(cut.Name);
                names.AddRange(cut.tiers.Select(t => t.name));
            }
            names.AddRange(r.Abrasives.Select(x => x.Name));
            names.AddRange(r.AllMolds().Select(x => x.Name));
            names.AddRange(r.Patterns.Select(x => x.Name));
            foreach (CraftDesign d in r.Designs)
            {
                names.Add(d.Name);
                names.AddRange(d.components.Select(c => c.Name));
                names.AddRange(d.joints.Select(j => j.Name));
            }
            return names.Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
        }

        [Test]
        public void Lettering_EveryNameOfTheContent_HasItsGlyphs_AndFitAndWrapKeepToTheirWidths()
        {
            CraftRules r = Rules();
            List<string> names = ContentNames(r);
            Assert.Greater(names.Count, 300, "the whole default content");
            var missing = new List<string>();
            foreach (string n in names)
                foreach (char ch in n)
                    if (!CraftLettering.HasGlyph(ch)) missing.Add($"'{ch}' in \"{n}\"");
            Assert.IsEmpty(missing, "written as a space for want of a glyph: " + string.Join(", ", missing.Take(20)));

            // the glyphs' own rules: small letters as capitals, a letter with an accent as the plain one, an unknown sign as a space
            Assert.AreEqual(CraftLettering.Width("MIRROR"), CraftLettering.Width("mirror"));
            Assert.AreEqual(CraftLettering.Width("ELVEN"), CraftLettering.Width("Élven"));
            Assert.IsFalse(CraftLettering.HasGlyph('₂'));
            Assert.AreEqual(CraftLettering.Width(" "), CraftLettering.Width("₂"));
            foreach (char digit in "0123456789")
                Assert.AreEqual(3, CraftLettering.Of(digit).Width, "a changing reading does not jump about");
            Assert.Greater(CraftLettering.Of('M').Width, CraftLettering.Of('H').Width, "M is not taken for H");
            Assert.AreEqual(2 * CraftLettering.Width("ASH"), CraftLettering.Width("ASH", 2));

            // cut to fit and broken into lines: never wider than the room given (the assembly plan's columns either side of
            // its joints at x 380 included), the whole text when it fits
            var texts = new List<string>(names);
            texts.AddRange(names.Select(n => n.ToUpperInvariant()));
            texts.Add("THE BIG HAMMER'S HEAD (WROUGHT IRON, 3 KG) - TOO LONG: 2200 MM, WANTS 380-480 MM");
            foreach (string t in texts)
                foreach (float w in new[] { 6f, 30f, 62f, 120f, 240f, 324f, 696f })
                {
                    string fit = CraftLettering.Fit(t, w);
                    Assert.LessOrEqual(CraftLettering.Width(fit), w, $"\"{t}\" fitted to {w}: \"{fit}\"");
                    if (CraftLettering.Width(t) <= w) Assert.AreEqual(t, fit);
                    for (int lines = 1; lines <= 3; lines++)
                    {
                        List<string> wrapped = CraftLettering.Wrap(t, w, lines);
                        Assert.LessOrEqual(wrapped.Count, lines);
                        foreach (string line in wrapped)
                            Assert.LessOrEqual(CraftLettering.Width(line), w, $"\"{t}\" wrapped to {w} in {lines}: \"{line}\"");
                    }
                }
        }

        // ------------------------------------------------------------------ the arm
        [Test]
        public void Arm_StepsAndWords_ChangeTogether_InTheWorksOwnTerms()
        {
            var hands = new CraftHands(3);
            int last = 0;
            for (double f = 0; f <= 1.0001; f += 0.01)
            {
                hands.fatigue = Math.Min(1, f);
                int level = CraftHands.Level(hands.fatigue);
                Assert.GreaterOrEqual(level, last, "the steps only rise as it tires");
                Assert.GreaterOrEqual(hands.fatigue + 1e-9, CraftHands.LevelFrom(level), $"{f:0.00}");
                Assert.AreEqual(hands.Feeling, CraftHands.FeelingAt(level, true), "the old words are the blows'");
                Assert.AreEqual(level == 0, CraftHands.FeelingAt(level, false) == "", $"{f:0.00}: words exactly when it is no longer fresh");
                last = level;
            }
            Assert.AreEqual(new[] { 0.0, 0.35, 0.6, 0.85 }, Enumerable.Range(0, 4).Select(CraftHands.LevelFrom).ToArray());
            for (int level = 1; level <= 2; level++)
            {
                StringAssert.Contains("blows", CraftHands.FeelingAt(level, true));
                StringAssert.DoesNotContain("blow", CraftHands.FeelingAt(level, false), "drafting at the wheel or stroking a hide strikes no blows");
                StringAssert.DoesNotContain("hammer", CraftHands.FeelingAt(level, false));
            }
        }

        // ------------------------------------------------------------------ what the workshop says happened
        /// <summary>The events <paramref name="act"/> raised, as (kind, text).</summary>
        private static List<(CraftEventKind kind, string text)> Raised(CraftWorkshop w, Action act)
        {
            var got = new List<(CraftEventKind, string)>();
            Action<CraftEvent> on = e => got.Add((e.Kind, e.Text));
            w.EventRaised += on;
            try { act(); } finally { w.EventRaised -= on; }
            return got;
        }

        /// <summary>A sentence: a capital first, a stop at the end, no id in it.</summary>
        private static void Sentence(string text, string what)
        {
            Assert.IsTrue(char.IsUpper(text[0]), $"{what}: \"{text}\" starts a sentence");
            Assert.IsTrue(text.EndsWith(".") || text.EndsWith("!") || text.EndsWith("?"), $"{what}: \"{text}\" ends one");
            StringAssert.DoesNotContain(":" + "", text.Replace(": ", " "), $"{what}: \"{text}\" has no id in it");
        }

        [Test]
        public void Events_SaySentences_AndAPieceMadeSaysWhereItCameFromInWords()
        {
            CraftRules r = Rules();
            // where a piece first came from: words, never an id, and never the name the tests must find out
            foreach (CraftOreDefinition o in r.Ores)
            {
                string origin = CraftOres.NewLump(r, o.id, 3, 0).origin;
                Assert.IsFalse(origin.Contains(":") || origin.Contains(o.id) || origin.Contains(o.Name.ToLowerInvariant()), $"{o.id}: \"{origin}\"");
            }
            foreach (CraftGemDefinition g in r.Gems)
            {
                string origin = CraftLapidary.NewRough(r, g.id, 5).origin;
                Assert.IsFalse(origin.Contains(":") || origin.Contains(g.id) || origin.Contains(g.Name.ToLowerInvariant()), $"{g.id}: \"{origin}\"");
            }
            foreach (CraftWoodDefinition wd in r.Woods)
            {
                Assert.IsFalse(CraftDefaults.WoodBlank(r, wd.id).origin.Contains(":"), wd.id);
                StringAssert.EndsWith(" log", CraftDefaults.Log(r, wd.id).origin, wd.id);
            }
            foreach (CraftHideDefinition h in r.Hides)
                Assert.IsFalse(CraftLeather.NewHide(r, h.id, 1).origin.Contains(":"), h.id);
            foreach (CraftSupplyDefinition f in r.Supplies.Where(x => x.kind == CraftSupplyKind.Fibre))
                StringAssert.StartsWith("a batch of ", CraftFibre.NewFibre(r, f.id).origin, f.id);
            Assert.AreEqual("an ash blank", CraftDefaults.WoodBlank(r, "ash").origin);
            Assert.AreEqual("a linden plank", CraftDefaults.WoodBlank(r, "linden").origin);

            // made from another: the first thing it came from, however many cuts lie between
            CraftWorkshop w = Workshop(CraftTools.Everything);
            string log = w.AddPiece(CraftDefaults.Log(r, "ash", 900, 120));
            w.Piece(log).wood.bark = 0;
            foreach (MetalSegment seg in w.Piece(log).segments) seg.bark = 0;
            var all = new List<(CraftEventKind kind, string text)>();
            string half = null, billet = null, sawn = null;
            all.AddRange(Raised(w, () => half = w.SplitLog(log, 0, 1, 1, 3, out _)));
            Assert.IsNotNull(half, "split");
            Assert.AreEqual("split from an ash log", w.Piece(half).origin);
            all.AddRange(Raised(w, () => billet = w.Rive(half, 0.05, 0.9, out _, 0.5)));
            Assert.IsNotNull(billet, "rived");
            Assert.AreEqual("rived from an ash log", w.Piece(billet).origin, "not \"rived from split from ...\"");
            all.AddRange(Raised(w, () => sawn = w.SawCut(billet, 0.5, 0.05, 0.95, out _)));
            Assert.IsNotNull(sawn, "sawn");
            Assert.AreEqual("sawn from an ash log", w.Piece(sawn).origin);

            // ore sieved, fuel shovelled, fibre spun to the end and woven, a stone sawn in two
            string lump = w.AddPiece(CraftOres.NewLump(r, "galena", 9, 0));
            for (int f = w.Piece(lump).ore.fragments.Count - 1; f >= 0; f--) w.CrushOre(lump, f, 1.2, out _);
            double mesh = (w.Piece(lump).ore.fragments.Max(f => f.size) + w.Piece(lump).ore.fragments.Min(f => f.size)) * 0.5;
            all.AddRange(Raised(w, () => Assert.IsNotNull(w.SieveOre(lump, mesh, out string why, 1), why)));
            all.AddRange(Raised(w, () => Assert.IsTrue(w.AddFuel("charcoal", 1, out string why), why)));
            all.AddRange(Raised(w, () => Assert.IsTrue(w.AddFuel("charcoal", 2, out string why, true), why)));
            string batch = w.AddPiece(CraftFibre.NewFibre(r, "wool_fibre", 30));
            string yarn = null;
            all.AddRange(Raised(w, () => yarn = w.SpinFibre(batch, 0.85, 0.5, 0.6, 100, out _)));
            Assert.IsNotNull(yarn, "spun");
            Assert.IsNull(w.Piece(batch), "the batch is all spun");
            string cloth = null;
            all.AddRange(Raised(w, () => cloth = w.WeaveCloth(yarn, 2, 0.9, 0.5, Enumerable.Repeat(0.8f, 40).ToList(), out _)));
            Assert.IsNotNull(cloth, "woven");
            Assert.AreEqual("woven from raw wool", w.Piece(cloth).origin);
            string stone = w.AddPiece(CraftLapidary.NewRough(r, "ruby", 7, 3));
            all.AddRange(Raised(w, () => Assert.IsTrue(w.SawGem(stone, 0, 0.1, 0.9, 1, 0.3, out string why), why)));

            Assert.IsTrue(all.Any(e => e.kind == CraftEventKind.PieceCreated && e.text == "sieved from a lump of ore"), "the fines");
            Assert.IsTrue(all.Any(e => e.kind == CraftEventKind.PieceCreated && e.text == "an offcut of a larger stone"), "the offcut never names the stone");
            Assert.IsTrue(all.Any(e => e.kind == CraftEventKind.PieceDestroyed), "a batch all spun, a yarn all woven");
            foreach ((CraftEventKind kind, string text) in all)
            {
                if (string.IsNullOrEmpty(text)) continue;
                if (kind == CraftEventKind.PieceCreated)
                    Assert.IsFalse(text.Contains(":") || text.Contains("galena") || text.Contains("ruby"), $"a piece made: \"{text}\"");
                else
                    Sentence(text, kind.ToString());
            }
        }
    }
}
#endif
