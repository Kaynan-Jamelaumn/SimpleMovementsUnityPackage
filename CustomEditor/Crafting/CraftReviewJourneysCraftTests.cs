#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the rules the journeys from raw stock to a finished leather, cloth or jewelled item showed wrong:
    /// an armour's straps and a shield's enarmes guard nothing, and no armour guards more than half again its design's
    /// Defense; a cloth part is weighed against a cloth, not a leather, and a garment lasts without a tannage it never had;
    /// the batting, thread and solder that go into an item weigh in it and are worth what they cost, in the bag as in the
    /// item; the Orient place's expected yield is what the guided path keeps, a cabochon's too, and grinding to the
    /// outline is never worth less than leaving the wheel out; a bark-tanned hide that dried stiff is oiled supple, never
    /// sent to be staked dry. (The journeys themselves: <see cref="CraftCraftJourneyTests"/>.)
    /// </summary>
    public class CraftReviewJourneysCraftTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static double Mean(HideState h, Func<HideCell, double> of) => h.cells.Where(c => c.present).Select(of).Average();

        /// <summary>A hide tanned through and dried, as the tannery leaves it (set directly: the journeys walk the path).</summary>
        private static string TannedHide(CraftWorkshop w, int seed, string agent = "bark", double stiff = 0.35, string hide = "cow")
        {
            CraftPiece p = CraftLeather.NewHide(w.Rules, hide, seed);
            foreach (HideCell c in p.hide.cells) { c.flesh = 0; c.hair = 0; c.tanS = c.tanC = 1; c.moisture = 0.1; c.stiff = stiff; }
            p.hide.dried = true;
            p.hide.agent = agent;
            return w.AddPiece(p);
        }

        /// <summary>Cuts a pattern where it fits best on a hide (its cells in the thickness wanted, few scars).</summary>
        private static string Cut(CraftWorkshop w, string hide, string pattern, double tmin, double tmax)
        {
            HideState h = w.Piece(hide).hide;
            CraftPattern pat = w.Rules.Pattern(pattern);
            (int x, int y, int rot)? best = null;
            double bestScore = double.MaxValue;
            for (int rot = 0; rot < 4; rot++)
            {
                pat.Size(rot, out int pw, out int ph);
                for (int y = 0; y + ph <= h.height; y++)
                    for (int x = 0; x + pw <= h.width; x++)
                    {
                        if (!CraftLeather.Fits(h, pat, x, y, rot)) continue;
                        double score = 0;
                        for (int py = 0; py < ph; py++)
                            for (int px = 0; px < pw; px++)
                            {
                                if (!pat.Covers(px, py, rot)) continue;
                                HideCell c = h.Cell(x + px, y + py);
                                double t = CraftLeather.CellThickness(c);
                                score += Math.Max(0, tmin - t) * 4 + Math.Max(0, t - tmax) * 4 + c.damage * 3;
                            }
                        if (score < bestScore) { bestScore = score; best = (x, y, rot); }
                    }
            }
            Assert.IsTrue(best.HasValue, $"{pattern} fits on the hide");
            string id = w.CutPattern(hide, pattern, best.Value.x, best.Value.y, best.Value.rot, 0.95, out string why);
            Assert.IsNotNull(id, why);
            return id;
        }

        /// <summary>Every joint of an assembly worked complete and true, as the benches work them.</summary>
        private static void Work(CraftWorkshop w, string asm)
        {
            CraftDesign d = w.Rules.Design(w.Piece(asm).assembly.design);
            foreach (CraftJointSpec js in d.joints)
                for (int k = 0; k < 400 && !CraftAssembly.JointComplete(js, w.Piece(asm).assembly.Joint(js.id)); k++)
                {
                    JointState j = w.Piece(asm).assembly.Joint(js.id);
                    bool riveted = js.kind == CraftJointKind.Rivet || js.kind == CraftJointKind.Buckle && CraftAssembly.RivetPart(d, js) != null;
                    string why;
                    bool done = riveted
                        ? CraftAssembly.NeedsHoles(w, w.Piece(asm), d, js) && j.holes < j.rivets + 1
                            ? w.JointWork(asm, js.id, CraftJointWork.Punch, 1, 0.05, 0.05, out why)
                            : w.JointWork(asm, js.id, CraftJointWork.Rivet, 1, CraftAssembly.IdealCharge(w, w.Piece(asm), js, CraftJointWork.Rivet) * 4, 0, out why)
                        : j.holes <= j.stitches
                            ? w.JointWork(asm, js.id, CraftJointWork.Punch, 4, 0.05, 0.05, out why)
                            : w.JointWork(asm, js.id, CraftJointWork.Stitch, 4, 0.6, 0.05, out why);
                    Assert.IsTrue(done, $"{js.id}: {why}");
                }
        }

        /// <summary>A leather jerkin from one bark-tanned cow hide (body, two straps, buckles, waxed thread), finished. Returns the assembly.</summary>
        private static string Jerkin(CraftWorkshop w, out string body, out string[] straps)
        {
            string hide = TannedHide(w, 99);
            body = Cut(w, hide, "jerkin_body", 2.5, 4.5);
            straps = new[] { Cut(w, hide, "strap", 3, 5), Cut(w, hide, "strap", 3, 5) };
            string buckles = w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Buckle, 2, 30, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002)));
            string asm = w.StartAssembly("leather_jerkin", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "body", 0, body, out why), why);
            Assert.IsTrue(w.Attach(asm, "straps", 0, straps[0], out why), why);
            Assert.IsTrue(w.Attach(asm, "straps", 1, straps[1], out why), why);
            Assert.IsTrue(w.Attach(asm, "buckles", 0, buckles, out why), why);
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 1, 1.3, "waxed_thread"), why);
            Work(w, asm);
            Assert.IsTrue(w.Finish(asm, out why), why);
            return asm;
        }

        /// <summary>The share of its design's Defense an armour gives for what its parts guard (its Defense less the assembly's part).</summary>
        private static double Guarding(CraftItemReport r) => r.Defense / (r.Design.defense * (0.7 + 0.3 * r.Assembly));

        // ================================================================== what guards
        /// <summary>
        /// A jerkin's thick shoulder straps hold it on and guard nothing: however thick they are, the jerkin guards by its
        /// body alone, about as its design says (9) - never above a breastplate's design (24), as it did when every strap
        /// was counted as armour against a reference that left them out.
        /// </summary>
        [Test]
        public void Armour_ItsStrapsGuardNothing_HoweverThick()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Leatherworks, 18, 5, 0, "water", "straps");
            CraftDesign d = w.Rules.Design("leather_jerkin");
            string asm = Jerkin(w, out _, out string[] straps);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            // the straps twice as thick: they still guard nothing
            foreach (string s in straps)
                foreach (HideCell c in w.Piece(s).hide.cells) c.thickness *= 2;
            CraftItemReport thick = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Console.WriteLine($"leather jerkin: defence {r.Defense:0.0} (design {d.defense}), guarding {Guarding(r):0.00}; straps twice as thick: {thick.Defense:0.0}, guarding {Guarding(thick):0.00}");
            Assert.AreEqual(Guarding(r), Guarding(thick), 1e-6, "the straps' thickness makes no difference to what it guards");
            Assert.That(r.Defense, Is.InRange(d.defense * 0.7, d.defense * 1.5), "a well-made jerkin guards about as its design says");
            Assert.Less(r.Defense, w.Rules.Design("breastplate").defense * 0.5, "a leather jerkin is no breastplate");
        }

        /// <summary>An item of nothing but straps (a belt) is still rated by its straps: as its design says, its protection whole.</summary>
        [Test]
        public void Armour_AnItemOfNothingButStraps_IsRatedByItsStraps()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Leatherworks, 18, 5, 0, "water", "belt");
            string hide = TannedHide(w, 41);
            string strap = Cut(w, hide, "long_strap", 3, 5);
            string asm = w.StartAssembly("belt", out string why);
            Assert.IsNotNull(asm, why);
            Assert.IsTrue(w.Attach(asm, "strap", 0, strap, out why), why);
            Assert.IsTrue(w.Attach(asm, "buckle", 0, w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Buckle, 1, 30, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002))), out why), why);
            Assert.IsTrue(w.Attach(asm, "rivets", 0, w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Rivet, 4, 4, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002))), out why), why);
            Work(w, asm);
            Assert.IsTrue(w.Finish(asm, out why), why);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Console.WriteLine($"belt: protection {r.Protection:0.00}, defence {r.Defense:0.00} (design {r.Design.defense})");
            Assert.Greater(r.Protection, 0.9, "a sound strap is all a belt's protection");
            Assert.That(r.Defense, Is.InRange(r.Design.defense * 0.7, r.Design.defense * 1.5));
        }

        /// <summary>
        /// A part that guards better than a well-made one (hardened in hot water, cuir bouilli) guards more - up to half again
        /// the design's Defense (<see cref="CraftEvaluator.GuardsAtMost"/>), however hard: a leather jerkin never guards like
        /// a breastplate.
        /// </summary>
        [Test]
        public void Armour_APartBetterThanItsDesigns_GuardsAtMostHalfAgain()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Leatherworks, 18, 5, 0, "water", "hard");
            string asm = Jerkin(w, out string body, out _);
            CraftItemReport soft = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            foreach (HideCell c in w.Piece(body).hide.cells) { c.hard = 1; c.stiff = 1; c.thickness *= 1.3; }
            CraftItemReport hard = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Console.WriteLine($"jerkin: defence {soft.Defense:0.0}; its body hardened and thicker {hard.Defense:0.0} (design {hard.Design.defense}, at most {hard.Design.defense * CraftEvaluator.GuardsAtMost:0.0})");
            Assert.Greater(hard.Defense, soft.Defense, "hardened, it guards better");
            Assert.LessOrEqual(hard.Defense, hard.Design.defense * CraftEvaluator.GuardsAtMost + 1e-4, "never more than half again its design's");
        }

        // ================================================================== cloth
        /// <summary>A garment cut cleanly from a flawless bolt of its cloth and sewn true.</summary>
        private static CraftItemReport Garment(string design, string cloth, (string role, int index, string pattern, int x, int y)[] cuts, int seed = 12)
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Tailor, 18, 5, 0, "water", "cloth");
            CraftDesign d = w.Rules.Design(design);
            CraftPiece boltPiece = CraftLeather.NewHide(w.Rules, cloth, seed);
            foreach (HideCell c in boltPiece.hide.cells) { c.flaw = false; c.damage = 0; c.scar = 0; }
            string bolt = w.AddPiece(boltPiece);
            Assert.IsTrue(w.MeasureFit(null, 1, CraftTextiles.ChestOf("wearer"), out string why), why);
            string asm = w.StartAssembly(design, out why);
            Assert.IsNotNull(asm, why);
            foreach (var cut in cuts)
            {
                string part = w.CutPattern(bolt, cut.pattern, cut.x, cut.y, 0, 1, out why);
                Assert.IsNotNull(part, $"{cut.pattern}: {why}");
                Assert.IsTrue(w.Attach(asm, cut.role, cut.index, part, out why), why);
            }
            CraftComponentSpec thread = CraftAssembly.ThreadPart(d);
            Assert.IsTrue(w.Attach(asm, thread.role, 0, null, out why, Math.Max(1, thread.supplyCount), 1, "linen_thread"), why);
            Work(w, asm);
            Assert.IsTrue(w.Finish(asm, out why), why);
            return CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
        }

        /// <summary>
        /// A wool tunic cut cleanly from a flawless bolt of wool cloth and sewn true guards as its design says: a cloth part
        /// is weighed against a sound plain cloth (<see cref="CraftEvaluator.ClothStiffness"/>, <see cref="CraftEvaluator.ClothStrength"/>),
        /// not a leather part (no cloth comes near that), so its protection is no longer 'poor' and its quality no longer
        /// capped at Fine. A canvas robe - canvas far stronger than wool - guards more, never more than half again its design's.
        /// </summary>
        [Test]
        public void Cloth_AFlawlessGarment_GuardsAsItsDesignSays()
        {
            CraftItemReport tunic = Garment("wool_tunic", "wool_cloth", new[] { ("body", 0, "tunic_body", 0, 0), ("body", 1, "tunic_body", 6, 0), ("sleeves", 0, "sleeve", 0, 6), ("sleeves", 1, "sleeve", 3, 6) });
            Console.WriteLine($"a flawless wool tunic: protection {tunic.Protection:0.00}, defence {tunic.Defense:0.00} (design {tunic.Design.defense}), quality {tunic.Quality:0.00} {tunic.Tier}");
            Assert.Greater(tunic.Protection, 0.85, "made well of its own cloth, it gives about the protection its design is for");
            Assert.That(tunic.Defense, Is.InRange(tunic.Design.defense * 0.7, tunic.Design.defense * 1.5));
            Assert.GreaterOrEqual(tunic.Quality, 0.88, "nothing about it holds a flawless garment down to Fine");
            CraftItemReport robe = Garment("robe", "canvas", new[] { ("panels", 0, "robe_panel", 0, 0), ("panels", 1, "robe_panel", 6, 0), ("sleeves", 0, "sleeve", 0, 10), ("sleeves", 1, "sleeve", 3, 10) });
            Console.WriteLine($"a flawless canvas robe: protection {robe.Protection:0.00}, defence {robe.Defense:0.00} (design {robe.Design.defense})");
            Assert.Greater(robe.Defense, robe.Design.defense, "canvas guards better than the plain cloth the robe is weighed against");
            Assert.LessOrEqual(robe.Defense, robe.Design.defense * CraftEvaluator.GuardsAtMost + 1e-4, "never more than half again its design's");
        }

        /// <summary>
        /// A garment is never cut for a tannage that sheds no water - cloth was never tanned: a flawless canvas tabard lasts
        /// about as its design says. Leather that sheds water still outlasts leather that soaks it up (bark against alum).
        /// </summary>
        [Test]
        public void Durability_ClothIsNotJudgedByATannage_LeatherStillIs()
        {
            CraftItemReport tabard = Garment("tabard", "canvas", new[] { ("panels", 0, "tabard_panel", 0, 0), ("panels", 1, "tabard_panel", 6, 0) });
            Console.WriteLine($"a flawless canvas tabard: durability {tabard.Durability} of its design's {tabard.Design.durability} ({tabard.Tier})");
            Assert.GreaterOrEqual(tabard.Durability, tabard.Design.durability * 0.8, "a well-made garment lasts about as its design says");
            int Belt(string agent)
            {
                var w = new CraftWorkshop(Rules(), CraftTools.Leatherworks, 18, 5, 0, "water", "belt");
                string strap = Cut(w, TannedHide(w, 41, agent), "long_strap", 3, 5);
                string asm = w.StartAssembly("belt", out string why);
                Assert.IsTrue(w.Attach(asm, "strap", 0, strap, out why), why);
                Assert.IsTrue(w.Attach(asm, "buckle", 0, w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Buckle, 1, 30, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002))), out why), why);
                Assert.IsTrue(w.Attach(asm, "rivets", 0, w.AddPiece(CraftDefaults.FastenerSet(w.Rules, CraftFastenerKind.Rivet, 4, 4, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002))), out why), why);
                Work(w, asm);
                Assert.IsTrue(w.Finish(asm, out why), why);
                return CraftEvaluator.Item(w.Rules, w, w.Piece(asm)).Durability;
            }
            int bark = Belt("bark"), alum = Belt("alum");
            Console.WriteLine($"a belt of bark-tanned leather lasts {bark}, of alum-tawed {alum}");
            Assert.Greater(bark, alum, "leather that sheds water lasts longer");
        }

        // ================================================================== what goes into an item weighs in it
        /// <summary>
        /// One item of a supply weighs what its kind's item does (or what the author set): a batting of padding 300 g (the
        /// bag item weighed 0.3 kg), a skein of thread its 20 m of middling yarn (<see cref="CraftFibre.SkeinGrams"/>), a
        /// pallion of solder half a gram, a fibre its grams per item.
        /// </summary>
        [Test]
        public void Supplies_OneItemWeighsWhatItsKindsItemDoes()
        {
            CraftRules R = Rules();
            Assert.AreEqual(300, R.Supply("wool_padding").ItemGrams, 1e-9);
            Assert.AreEqual(CraftFibre.SkeinGrams, R.Supply("linen_thread").ItemGrams, 1e-9);
            Assert.That(CraftFibre.SkeinGrams, Is.InRange(3.0, 8.0), "a 20 m skein is a few grams");
            Assert.AreEqual(0.5, R.Supply("silver_solder").ItemGrams, 1e-9);
            Assert.AreEqual(R.Supply("cotton_fibre").gramsPerItem, R.Supply("cotton_fibre").ItemGrams, 1e-9);
            var own = new CraftSupplyDefinition { kind = CraftSupplyKind.Padding, itemGrams = 120 };
            Assert.AreEqual(120, own.ItemGrams, 1e-9, "an author's own weight is kept");
        }

        /// <summary>The gambeson's parts cut from bolts of canvas and linen: (assembly, the four panels' grams).</summary>
        private static (string asm, double cloth) Gambeson(CraftWorkshop w, string padding)
        {
            string canvas = w.AddPiece(CraftLeather.NewHide(w.Rules, "canvas", 3)), linen = w.AddPiece(CraftLeather.NewHide(w.Rules, "linen_cloth", 4));
            string asm = w.StartAssembly("padded_gambeson", out string why);
            Assert.IsNotNull(asm, why);
            double cloth = 0;
            foreach ((string role, string bolt, int i, int y) in new[] { ("shell", canvas, 0, 0), ("shell", canvas, 1, 8), ("lining", linen, 0, 0), ("lining", linen, 1, 8) })
            {
                string part = w.CutPattern(bolt, "gambeson_panel", 0, y, 0, 0.95, out why);
                Assert.IsNotNull(part, why);
                cloth += w.Piece(part).Mass(w.Rules);
                Assert.IsTrue(w.Attach(asm, role, i, part, out why), why);
            }
            Assert.IsTrue(w.Attach(asm, "padding", 0, null, out why, 3, w.Rules.Supply(padding).strength, padding), why);
            Assert.IsTrue(w.Attach(asm, "thread", 0, null, out why, 2, 1, "linen_thread"), why);
            return (asm, cloth);
        }

        /// <summary>
        /// The batting quilted into a gambeson weighs in it (three battings, 900 g) and is worth what it cost - the supply
        /// that went in, linen padding cheaper than wool; the balance weighs it so while it is being made, and the item's
        /// parts name it beside its panels.
        /// </summary>
        [Test]
        public void Supplies_TheBattingQuiltedIn_WeighsInTheItem_AndIsWorthWhatItCost()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Tailor, 18, 5, 0, "water", "padding");
            (string asm, double cloth) = Gambeson(w, "wool_padding");
            CraftDesign d = w.Rules.Design("padded_gambeson");
            double thread = 2 * CraftFibre.SkeinGrams;
            Assert.AreEqual(cloth + 900 + thread, CraftLook.AssembledMass(w.Rules, w, w.Piece(asm)), 1, "the balance weighs the batting and the thread in it");
            foreach (CraftJointSpec js in d.joints)
                for (int k = 0; k < 40 && w.Piece(asm).assembly.Joint(js.id).stitches < js.count; k++)
                {
                    Assert.IsTrue(w.JointWork(asm, js.id, CraftJointWork.Punch, 6, 0.05, 0.05, out string why), why);
                    Assert.IsTrue(w.JointWork(asm, js.id, CraftJointWork.Stitch, 6, 0.6, 0.05, out why), why);
                }
            Assert.IsTrue(w.Finish(asm, out string fin), fin);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Console.WriteLine($"padded gambeson: {r.Weight:0.000} kg; its four panels {cloth / 1000:0.000} kg; parts: {string.Join("; ", CraftEvaluator.KnownParts(w.Rules, r, null))}");
            Assert.AreEqual((cloth + 900 + thread) / 1000, r.Weight, 0.002, "its panels, three battings of 300 g and two skeins of thread");
            Assert.IsTrue(CraftEvaluator.KnownParts(w.Rules, r, null).Any(p => p.StartsWith(d.Component("padding").Name) && p.Contains("900 g")), "its parts name the batting and its weight");

            CraftAssembly.SupplyIn(w.Rules, w.Piece(asm).assembly, d.Component("padding"), out double grams, out double worth);
            Assert.AreEqual(900, grams, 1e-6);
            Assert.AreEqual(3 * w.Rules.Supply("wool_padding").price, worth, 1e-6, "three wool battings' worth");
            var w2 = new CraftWorkshop(Rules(), CraftTools.Tailor, 18, 5, 0, "water", "padding2");
            (string linen, _) = Gambeson(w2, "linen_padding");
            CraftAssembly.SupplyIn(w2.Rules, w2.Piece(linen).assembly, d.Component("padding"), out _, out double linenWorth);
            Assert.AreEqual(3 * w2.Rules.Supply("linen_padding").price, linenWorth, 1e-6, "the padding that went in is what it is worth: linen's");
        }

        /// <summary>
        /// Supplies put in before what they were was kept (no supply named) count as the first of their kind - and a named
        /// supply added later counts on top of them, never in their stead.
        /// </summary>
        [Test]
        public void Supplies_PutInUnnamed_CountAsTheFirstOfTheirKind()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Tailor, 18, 5, 0, "water", "unnamed");
            string asm = w.StartAssembly("padded_gambeson", out string why);
            Assert.IsNotNull(asm, why);
            CraftComponentSpec pad = w.Rules.Design("padded_gambeson").Component("padding");
            AssemblyState a = w.Piece(asm).assembly;
            // as an older save left it: a count and a strength, nothing of what it was
            a.supplies.Add(new CraftVariable("padding", 2));
            CraftSupplyDefinition first = w.Rules.FirstSupply(CraftSupplyKind.Padding);
            CraftAssembly.SupplyIn(w.Rules, a, pad, out double grams, out double worth);
            Assert.AreEqual(2 * first.ItemGrams, grams, 1e-6);
            Assert.AreEqual(2 * first.price, worth, 1e-6);
            Assert.IsTrue(w.Attach(asm, "padding", 0, null, out why, 1, 1.15, "felt_padding"), why);
            CraftAssembly.SupplyIn(w.Rules, a, pad, out grams, out worth);
            Assert.AreEqual(2 * first.ItemGrams + w.Rules.Supply("felt_padding").ItemGrams, grams, 1e-6, "the two before and the felt");
            Assert.AreEqual(2 * first.price + w.Rules.Supply("felt_padding").price, worth, 1e-6);
        }

        // ================================================================== the lapidary's plan
        /// <summary>Grinds a stone to its outline as the window teaches: pressed where it stands out, eased off near the outline.</summary>
        private static void GrindToOutline(CraftWorkshop w, string id)
        {
            for (int r = 0; r < 400 && CraftLapidary.Excess(w.Piece(id).gem) > 0.05; r++)
            {
                GemState g = w.Piece(id).gem;
                List<float> prof = g.profile.Count == CraftLapidary.ProfileRadii ? g.profile : CraftLapidary.Outline(g);
                var touches = new List<float>();
                for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
                {
                    double over = prof[k] - CraftLapidary.TargetRadius(g, k);
                    if (over > 0.01) { touches.Add(k * 360f / CraftLapidary.ProfileRadii); touches.Add((float)CraftMath.Clamp(over * 4, 0.03, 0.3)); }
                }
                if (touches.Count == 0) break;
                Assert.IsTrue(w.GrindGem(id, touches, 1, 0.9, out string why), why);
            }
        }

        /// <summary>
        /// For every cut, the Orient place's expected yield is what the stone ground to its outline (as the guide asks) keeps
        /// - a cabochon, an emerald cut or a cushion as much as a brilliant: the cut keeps at most all of what lies inside its
        /// outline (<see cref="CraftLapidary.CutKeeps"/>), so the plan no longer promises a cabochon nearly half as much again
        /// as the wheel leaves of it. Leaving the wheel out never gives a heavier stone, and its lopsided outline makes it
        /// worth less than the ground one.
        /// </summary>
        [Test]
        public void Orient_TheExpectedYield_IsWhatTheGroundStoneKeeps_AndGrindingIsNeverWorthLess()
        {
            var bad = new List<string>();
            foreach (CraftGemCut cut in Rules().Cuts)
            {
                var w = new CraftWorkshop(Rules(), CraftTools.Lapidary, 18, 3, 0, "water", "yield");
                string gem = cut.cabochon ? "turquoise" : "rock_crystal";
                string ground = w.AddPiece(CraftLapidary.NewRough(w.Rules, gem, 5, 6)), freehand = w.AddPiece(CraftLapidary.NewRough(w.Rules, gem, 5, 6));
                foreach (string id in new[] { ground, freehand })
                {
                    w.Piece(id).gem.inclusions.Clear();
                    w.Piece(id).gem.crust.Clear();
                    Assert.IsTrue(w.OrientGem(id, cut.id, w.Piece(id).gem.axis, 0, 0, 0.7, out string why), why);
                }
                double promised = CraftLapidary.Grade(w.Rules, w.Piece(ground)).Yield;
                GrindToOutline(w, ground);
                double n = w.Rules.Gem(gem).refractiveIndex;
                string compound = cut.cabochon ? "tripoli" : "cerium_oxide";
                foreach (string id in new[] { ground, freehand })
                {
                    for (int t = 0; t < cut.tiers.Count; t++)
                        Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], n), 0.05, 0, 1, out string why), why);
                    Assert.IsTrue(w.PolishGem(id, compound, 14, 0.3, 0.5, out string why2), why2);
                }
                double gc = w.Piece(ground).gem.carats, fc = w.Piece(freehand).gem.carats;
                CraftGemGrade gg = CraftLapidary.Grade(w.Rules, w.Piece(ground)), fg = CraftLapidary.Grade(w.Rules, w.Piece(freehand));
                Console.WriteLine($"{cut.id,-16} promised {promised:0.00} ct; ground {gc:0.00} ct worth {gg.Value:0.0}; freehand {fc:0.00} ct worth {fg.Value:0.0}");
                if (Math.Abs(gc - promised) > promised * 0.05) bad.Add($"{cut.id}: the Orient place promised {promised:0.00} ct, ground to its outline it keeps {gc:0.00}");
                if (fc > gc * 1.05) bad.Add($"{cut.id}: leaving the wheel out gives the heavier stone ({fc:0.00} against {gc:0.00} ct)");
                if (fg.Value > gg.Value) bad.Add($"{cut.id}: domed or faceted freehand it is worth more ({fg.Value:0.0} against {gg.Value:0.0})");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ================================================================== a hide that dried stiff
        private static List<float> Row(HideState h, int y)
        {
            var pts = new List<float>();
            for (double x = 0.1; x <= h.width - 0.1; x += 0.2) { pts.Add((float)x); pts.Add(y + 0.5f); }
            return pts;
        }

        /// <summary>
        /// A bark-tanned hide that dried stiff is told to be oiled supple - at a tannery as at a leather bench - never staked
        /// dry, where its grain cracks (the staking blade still cracks it: the rule the guide steers round). Oiled as the
        /// guide says, it comes supple and its grain whole, and the guide goes on to cutting it.
        /// </summary>
        [Test]
        public void Guide_ABarkHideThatDriedStiff_IsOiledSupple_NotStakedDry()
        {
            foreach (CraftTools tools in new[] { CraftTools.Tannery, CraftTools.Leatherworks })
            {
                var w = new CraftWorkshop(Rules(), tools, 18, 5, tools == CraftTools.Tannery ? 3 : 0, "water", "stiff");
                string hide = TannedHide(w, 7, "bark", 0.44);
                foreach (HideCell c in w.Piece(hide).hide.cells) c.moisture = 0.08;
                HideState h = w.Piece(hide).hide;
                CraftGuide.Step? g = CraftGuide.Next(w, w.Tools, w.Piece(hide), null);
                Console.WriteLine($"{tools}: {g?.text} [{g?.area}/{g?.place}]");
                Assert.IsTrue(g.HasValue && g.Value.text.Contains("oil it supple") && g.Value.area == CraftGuide.Tannery && g.Value.place == "Beam", $"{tools}: {g?.text}");
                Assert.IsFalse(g.Value.text.Contains("stake it supple"), "never sent to be staked dry");
                double grain = Mean(h, c => c.grain);
                for (int pass = 0; pass < 6 && Mean(h, c => c.stiff) >= 0.35; pass++)
                    for (int y = 0; y < h.height; y++)
                        Assert.IsTrue(w.Condition(hide, "neatsfoot_oil", Row(h, y), 0.35, out string why), why);
                Assert.Less(Mean(h, c => c.stiff), 0.35, "oiled, it is supple");
                Assert.AreEqual(grain, Mean(h, c => c.grain), 1e-9, "oiling never cracks the grain");
                CraftGuide.Step? next = CraftGuide.Next(w, w.Tools, w.Piece(hide), null);
                Assert.IsFalse(next.HasValue && next.Value.text.Contains("dried stiff"), $"supple, the guide goes on: {next?.text}");
            }
            // what the guide steers round: the staking blade on dry bark-tanned leather cracks its grain
            var s = new CraftWorkshop(Rules(), CraftTools.Tannery, 18, 5, 3, "water", "stake");
            string dry = TannedHide(s, 8, "bark", 0.44);
            HideState d = s.Piece(dry).hide;
            foreach (HideCell c in d.cells) c.moisture = 0.08;
            double before = Mean(d, c => c.grain);
            for (int y = 0; y < d.height; y++) Assert.IsTrue(s.Stake(dry, Row(d, y), 0.35, out string why), why);
            Assert.Less(Mean(d, c => c.grain), before, "staked dry, bark-tanned grain cracks");
            Assert.IsTrue(CraftLeather.CracksStakedDry(s.Rules, d));
        }

        /// <summary>Leather of another tannage that dried stiff is staked dry, as before: its grain takes no harm.</summary>
        [Test]
        public void Guide_AnAlumTawedHideThatDriedStiff_IsStakedDry_Harmlessly()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Tannery, 18, 5, 3, "water", "alum");
            string hide = TannedHide(w, 9, "alum", 0.44);
            HideState h = w.Piece(hide).hide;
            foreach (HideCell c in h.cells) c.moisture = 0.08;
            CraftGuide.Step? g = CraftGuide.Next(w, w.Tools, w.Piece(hide), null);
            Assert.IsTrue(g.HasValue && g.Value.text.Contains("stake it supple"), g?.text);
            Assert.IsFalse(CraftLeather.CracksStakedDry(w.Rules, h));
            double grain = Mean(h, c => c.grain);
            for (int y = 0; y < h.height; y++) Assert.IsTrue(w.Stake(hide, Row(h, y), 0.35, out string why), why);
            Assert.AreEqual(grain, Mean(h, c => c.grain), 1e-9, "alum-tawed grain takes no harm staked dry");
        }
    }
}
#endif
