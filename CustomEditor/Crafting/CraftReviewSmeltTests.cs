#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the smelting review (ore bench, furnace, crucible): one unbroken stream stopped early is short,
    /// not a cold shut; a fluid melt fills every cavity of a fastener mould the pour brought; an unstirred melt casts
    /// streaky and weaker, and a fold evens it; the wire brush takes only the lump's own weathered rind; a piece melted
    /// into an empty crucible is one even melt; the crust left in a crucible poured empty is scraped out; the furnace's
    /// pool holds no gas of its own and its slag is named; a melt that would cast porous says so; a whole sprinkle of
    /// borax covers the melt; a pour held in the cup and tipped back promptly loses nothing; the hammer's bar shows the
    /// blow that lands; the sieve's rings show what these hands shake through.
    /// </summary>
    public class CraftReviewSmeltTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "smeltrev");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>A crucible holding <paramref name="grams"/> of <paramref name="metal"/>, <paramref name="superheat"/> over its liquidus.</summary>
        private static CraftWorkshop Melt(double grams, string metal = "Bronze", double superheat = 60)
        {
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            foreach ((string element, double fraction) in Metal(metal))
                w.Crucible.melt.Add(new CraftAmount(element, grams * fraction));
            w.Crucible.temperature = CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus + superheat;
            return w;
        }

        private static string Causes(CraftPiece p) => string.Join("; ", p.defects.Select(d => d.kind + ": " + d.cause));

        /// <summary>A pour held at <paramref name="tilt"/> until <paramref name="until"/> of the mould is in, then let go (it tips back and settles).</summary>
        private static CraftPourControl PourTo(double until, double tilt = 0.6, double fillSeconds = 4)
        {
            var p = new CraftPourControl();
            const double dt = 1 / 60.0;
            for (int k = 0; k < 4000 && p.Poured < until && !p.Full; k++) p.Tick(dt, tilt, null, fillSeconds);
            for (int k = 0; k < 600 && !p.Settled(true); k++) p.Tick(dt, 0, null, fillSeconds);
            Assert.IsTrue(p.Settled(true), "the stream died away");
            return p;
        }

        // ------------------------------------------------------------------ r03: a stream stopped short is short
        [Test]
        public void Pour_OneStreamStoppedEarlyIsShort_AStreamStartedAgainLeavesAColdShut()
        {
            CraftPourControl early = PourTo(0.6);
            Assert.Less(early.Poured, 1);
            Assert.AreEqual(0, early.Interruptions, "it stopped once and never ran again: nothing for new metal to meet half frozen");
            Assert.IsTrue(early.Request("ingot", false, out CraftRequest r));
            CraftWorkshop w = Melt(3000);
            CraftPiece cast = w.Piece(w.Pour(r.id, r.a, r.b, r.i0, out string why, r.c, r.d, r.i1 / 1000.0, r.i2 / 1000.0));
            Assert.IsNotNull(cast, why);
            Assert.IsTrue(cast.defects.Any(d => d.kind == CraftDefectKind.Misrun), "it came out short: " + Causes(cast));
            Assert.IsFalse(cast.defects.Any(d => d.kind == CraftDefectKind.ColdShut), "but whole: " + Causes(cast));

            // Stopped half way and tipped again: the new metal meets the old half frozen.
            var broken = new CraftPourControl();
            const double dt = 1 / 60.0;
            for (int k = 0; k < 4000 && broken.Poured < 0.4; k++) broken.Tick(dt, 0.6, null, 4);
            for (int k = 0; k < 60; k++) broken.Tick(dt, 0, null, 4);
            Assert.AreEqual(0, broken.Interruptions, "stopped, not yet started again");
            for (int k = 0; k < 4000 && !broken.Full; k++) broken.Tick(dt, 0.6, null, 4);
            Assert.AreEqual(1, broken.Interruptions, "started again: one break");
        }

        // ------------------------------------------------------------------ r02: fastener moulds fill as shaped moulds do
        [Test]
        public void FastenerMould_AFluidMeltFillsEveryCavityThePourBrought_ASluggishOneFreezesInTheRunner()
        {
            CraftMold rivets = Rules().Mold("rivets");
            int fluid = 0;
            foreach (var m in CraftDefaults.StockMetals())
            {
                CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
                foreach ((string element, double fraction) in m.composition) w.Crucible.melt.Add(new CraftAmount(element, 2000 * fraction));
                CraftAlloyProfile prof = CraftSmelting.MeltProfile(w.Rules, w.Crucible);
                w.Crucible.temperature = prof.Liquidus + 60;
                if (CraftSmelting.Fill(w.Rules, prof, 60, 0.6) < CraftSmelting.FullFill) continue;
                fluid++;
                CraftPiece all = w.Piece(w.Pour("rivets", 0.6, 0.9, 0, out string why, 0, 0, 1));
                Assert.IsNotNull(all, why);
                Assert.AreEqual(rivets.count, all.fasteners.count, $"{m.name}: a fluid melt fills every cavity of a full pour");
                CraftPiece half = w.Piece(w.Pour("rivets", 0.6, 0.9, 0, out why, 0, 0, 0.5));
                Assert.IsNotNull(half, why);
                Assert.AreEqual(rivets.count / 2, half.fasteners.count, $"{m.name}: half a pour fills half the cavities");
            }
            Assert.Greater(fluid, 3, "most stock metals run fluid 60 °C over their liquidus");

            // A sluggish melt freezes before it reaches the last cavities, as it stops short in a shaped mould; the verdict says so.
            CraftWorkshop s = Melt(2000, "Bronze", 4);
            CraftAlloyProfile sp = CraftSmelting.MeltProfile(s.Rules, s.Crucible);
            double fill = CraftSmelting.Fill(s.Rules, sp, 4, 0.6);
            Assert.Less(fill, CraftSmelting.FullFill, "barely melted, it is sluggish");
            CraftPourControl pour = PourTo(1.0, 0.6, CraftPourControl.FillSeconds(CraftSmelting.MoldMass(s.Rules, rivets, sp.Density)));
            Assert.IsTrue(pour.Request("rivets", false, out CraftRequest r));
            CraftPiece cold = s.Piece(s.Pour(r.id, r.a, r.b, r.i0, out string no, r.c, r.d, r.i1 / 1000.0, r.i2 / 1000.0));
            Assert.IsNotNull(cold, no);
            Assert.AreEqual((int)Math.Floor(rivets.count * CraftSmelting.Reach(fill) + 1e-6), cold.fasteners.count, "the cavities as far as the melt runs before it freezes");
            Assert.Less(cold.fasteners.count, rivets.count);
            string verdict = CraftSmelting.PourVerdict(s.Rules, cold, pour, CraftSmelting.MoldMass(s.Rules, rivets, sp.Density), rivets, false);
            StringAssert.Contains($"{cold.fasteners.count} of the {rivets.count} cavities filled", verdict);
            StringAssert.DoesNotContain("clean pour", verdict);
        }

        // ------------------------------------------------------------------ r04: stirring counts
        [Test]
        public void Stirring_AnUnstirredMeltCastsStreakyAndWeaker_AndAFoldEvensIt()
        {
            CraftPiece Cast(double mixed)
            {
                CraftWorkshop w = Melt(1500);
                w.Crucible.mixed = mixed;
                return w.Piece(w.Pour("ingot", 0.6, 0.9, 0, out _));
            }
            CraftPiece streaky = Cast(0.05), even = Cast(1.0);
            CraftPieceReport a = CraftEvaluator.Piece(Rules(), streaky), b = CraftEvaluator.Piece(Rules(), even);
            Assert.AreEqual(0.95, streaky.inhomogeneity, 1e-6);
            Assert.Less(a.Integrity, b.Integrity * 0.8, "streaks of different metal: it breaks where they meet");
            Assert.Less(a.Toughness, b.Toughness * 0.8);
            Assert.Less(a.MinToughness, b.MinToughness * 0.8);
            Assert.AreEqual(b.Integrity, CraftEvaluator.Piece(Rules(), Cast(0.999)).Integrity, 0.002, "a melt stirred even casts as sound as one");
            Assert.IsTrue(CraftLook.Describe(Rules(), streaky, null, CraftInstruments.None, false, false).Any(l => l.Contains("Streaks")), "the casting shows its streaks");
            Assert.IsFalse(CraftLook.Describe(Rules(), even, null, CraftInstruments.None, false, false).Any(l => l.Contains("Streaks")));
            var pour = new CraftPourControl();
            for (int k = 0; k < 4000 && !pour.Full; k++) pour.Tick(1 / 60.0, 0.6, null, 4);
            StringAssert.Contains("streaky", CraftSmelting.PourVerdict(Rules(), streaky, pour, 1000, Rules().Mold("ingot"), false));
            StringAssert.DoesNotContain("streaky", CraftSmelting.PourVerdict(Rules(), even, pour, 1000, Rules().Mold("ingot"), false));

            // The streaks are drawn out thin and laid together by a fold, as a bloom's carbon evens.
            CraftWorkshop f = Workshop();
            string id = f.AddPiece(streaky.Clone());
            CraftForging.Fold(f, f.Piece(id), 0.5);
            Assert.AreEqual(0.475, f.Piece(id).inhomogeneity, 1e-6);
        }

        // ------------------------------------------------------------------ r05: the rind is the lump's outside
        [Test]
        public void WireBrush_TakesOnlyTheLumpsWeatheredRind_FreshBreaksHaveNone()
        {
            CraftWorkshop w = Workshop(CraftTools.OreBench);
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 5, 0));
            CraftPiece p = w.Piece(id);
            OreFragment lump = p.ore.fragments[0];
            double rock0 = lump.mass * lump.gangue, rind0 = rock0 * lump.RindShare;
            Assert.AreEqual(CraftOres.Rind(lump.size), lump.RindShare, 1e-9, "a lump comes out of the seam with the rind of its size");

            // A blow shares the rind out with the rock: no new rind on the fresh faces.
            Assert.IsTrue(w.CrushOre(id, 0, 1.5, out string why), why);
            Assert.Greater(p.ore.fragments.Count, 1);
            Assert.AreEqual(rind0, p.ore.fragments.Sum(f => f.mass * f.gangue * f.RindShare), rind0 * 0.01, "the broken stones carry the lump's rind between them, no more");

            // Crushed and brushed, crushed and brushed: never more rock off than the rind the lump came with.
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = 0; i < p.ore.fragments.Count; i++)
                    w.BrushOre(id, i, CraftOreBrush.Wire, 1, out why);
                for (int i = p.ore.fragments.Count - 1; i >= 0 && p.ore.fragments.Count < CraftOres.MaxFragments; i--)
                    w.CrushOre(id, i, 1.5, out why);
            }
            for (int i = 0; i < p.ore.fragments.Count; i++)
                w.BrushOre(id, i, CraftOreBrush.Wire, 1, out why);
            double rock = p.ore.fragments.Sum(f => f.mass * f.gangue);
            Assert.GreaterOrEqual(rock, rock0 - rind0 - 1e-6, $"the rock inside the lump is not a rind: {rock0:0} g of rock, {rind0:0} g of it rind, {rock:0} g left");
            Assert.Less(rock, rock0 - rind0 * 0.8, "and the rind comes off");
        }

        // ------------------------------------------------------------------ r09: the first piece is one even melt
        [Test]
        public void Crucible_APieceMeltedIntoAnEmptyCrucibleIsEven_AnotherMetalStreaksIt()
        {
            Assert.AreEqual(1, CraftSmelting.MixedAfter(0, 0, 500, 1), 1e-12, "into an empty crucible: one even melt");
            Assert.AreEqual(1, CraftSmelting.MixedAfter(1, 800, 200, 0), 1e-12, "more of the melt's own metal");
            Assert.AreEqual(0.88, CraftSmelting.MixedAfter(1, 880, 120, 1), 1e-9, "tin into copper streaks it by the tin's share");
            Assert.AreEqual(0.88, CraftSmelting.MixedAfter(1, 120, 880, 1), 1e-9, "the lesser through the greater, whichever went in first");
            Assert.AreEqual(1, CraftSmelting.MixedAfter(1, 0.01, 1000, 1), 1e-4, "a smear left from the last melt does not streak a new one");
            Assert.AreEqual(0.75, CraftSmelting.MixedAfter(0.5, 500, 500, 0), 1e-9, "what was unmixed is spread through more metal");

            // In the workshop: one bar of copper melted into the empty crucible, then a bar of tin.
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            void MeltIn(string metal, double grams)
            {
                CraftPiece bar = CraftDefaults.Bar(w.Rules, 100, 20, grams / (100 * 20 * 8.9 / 1000) / 1, (metal, 1.0));
                string bid = w.AddPiece(bar);
                Assert.IsTrue(w.MovePiece(bid, CraftLocation.Melt, out string why), why);
                for (int t = 0; t < 200 && w.Piece(bid) != null; t++)
                {
                    foreach (MetalSegment s in w.Piece(bid).segments)
                        foreach (CraftZone z in new[] { s.edge, s.body }) z.temp = z.prevTemp = 1250;
                    w.Crucible.temperature = 1250;
                    w.RunSteps(1);
                }
                Assert.IsNull(w.Piece(bid), "it melted");
            }
            MeltIn("copper", 300);
            Assert.GreaterOrEqual(w.Crucible.mixed, 0.999, "one bar of pure copper is all one metal");
            StringAssert.DoesNotContain("Streaks", CraftLook.Crucible(w.Rules, w.Crucible, null, false));
            MeltIn("copper", 200);
            Assert.GreaterOrEqual(w.Crucible.mixed, 0.999, "and more copper is still all one metal");
            MeltIn("tin", 400);
            Assert.Less(w.Crucible.mixed, 0.7, "tin run into copper streaks it until it is stirred");
            StringAssert.Contains("Streaks", CraftLook.Crucible(w.Rules, w.Crucible, null, false));
        }

        // ------------------------------------------------------------------ r12: the crust of a crucible poured empty
        [Test]
        public void EmptiedCrucible_TheCrustTheLookNamesIsScrapedOut()
        {
            CraftWorkshop w = Melt(300);
            w.Crucible.slag = 40;
            w.Crucible.dross = 10;
            Assert.IsNotNull(w.Pour("blade_blank", 0.6, 0.9, 0, out string why), why); // a mould bigger than the melt takes it all
            Assert.Less(w.Crucible.Mass, CraftSmelting.EmptyCrucible, "the crucible is empty");
            string look = CraftLook.Crucible(w.Rules, w.Crucible, null, false);
            StringAssert.Contains("scrape it out", look);
            w.Crucible.temperature = 18; // cold or hot, there is no metal to hold it
            Assert.IsTrue(w.SkimDross(0.2, out why), $"the look says '{look}', the skim says '{why}'");
            Assert.Less(w.Crucible.slag + w.Crucible.dross, 0.5, "the crust comes out whole: there is no metal under it to lose");
            Assert.AreEqual("The crucible is empty.", CraftLook.Crucible(w.Rules, w.Crucible, null, false));

            // A melt frozen under its dross still holds it.
            CraftWorkshop f = Melt(500);
            f.Crucible.dross = 20;
            f.Crucible.temperature = CraftSmelting.MeltProfile(f.Rules, f.Crucible).Solidus - 100;
            Assert.IsFalse(f.SkimDross(1, out why));
            StringAssert.Contains("solid", why);
        }

        // ------------------------------------------------------------------ r13: the furnace's pour
        [Test]
        public void FurnacePour_ThePoolHoldsNoGasOfItsOwn_AndItsSlagIsNamedAsTheFurnaces()
        {
            CraftWorkshop w = Workshop();
            w.Furnace.pool.Add(new CraftAmount("copper", 1500));
            w.Furnace.fire.temperature = 1120; // just above copper's liquidus
            Assert.IsTrue(w.CanPour(true, out string why), why);
            CraftPiece cast = w.Piece(w.TakeMelt("ingot", 0.6, 0.8, out why));
            Assert.IsNotNull(cast, why);
            CraftDefect pores = cast.defects.FirstOrDefault(d => d.kind == CraftDefectKind.Porosity);
            Assert.IsTrue(pores == null || !pores.cause.Contains("held too hot"), $"a pool barely above its liquidus: '{pores?.cause}'");
            Assert.AreEqual(0, cast.inhomogeneity, 1e-9, "the pool is one metal");

            CraftWorkshop s = Workshop();
            s.Furnace.pool.Add(new CraftAmount("copper", 300));
            s.Furnace.slag = 2000;
            s.Furnace.fire.temperature = 1150;
            CraftPiece small = s.Piece(s.TakeMelt("ingot", 0.6, 0.9, out why));
            Assert.IsNotNull(small, why);
            CraftDefect slag = small.defects.FirstOrDefault(d => d.kind == CraftDefectKind.Inclusion);
            Assert.IsNotNull(slag, "the furnace's slag runs in with a small pour under a lot of it");
            StringAssert.Contains("furnace", slag.cause);
            var pour = new CraftPourControl();
            for (int k = 0; k < 4000 && !pour.Full; k++) pour.Tick(1 / 60.0, 0.6, null, 4);
            string verdict = CraftSmelting.PourVerdict(s.Rules, small, pour, 1000, s.Rules.Mold("ingot"), true);
            StringAssert.Contains("tap the slag", verdict);
            StringAssert.DoesNotContain("skim", verdict, "there is nothing to skim in a furnace");
        }

        // ------------------------------------------------------------------ r14: gas the look shows
        [Test]
        public void Gas_AMeltThatWouldCastPorousSaysSo_OneThatWouldNotDoesNot()
        {
            foreach (double gas in new[] { 0.03, 0.08, 0.2, 0.5 })
            {
                CraftWorkshop w = Melt(1500);
                w.Crucible.gas = gas;
                w.Crucible.cover = 1; // held just above its liquidus under a cover: nothing else for pores
                string look = CraftLook.Crucible(w.Rules, w.Crucible, null, false);
                CraftPiece cast = w.Piece(w.Pour("ingot", 0.6, 1.0, 0, out string why));
                Assert.IsNotNull(cast, why);
                bool porous = cast.defects.Any(d => d.kind == CraftDefectKind.Porosity);
                bool warned = look.Contains("bubbles");
                Assert.AreEqual(porous, warned, $"gas {gas}: the look '{look}' - the casting {(porous ? "has" : "has no")} pores");
            }
        }

        // ------------------------------------------------------------------ r15: a whole cover
        [Test]
        public void Cover_AWholeSprinkleKeepsTheAirOff_HalfASprinkleHalf()
        {
            CraftWorkshop w = Melt(1000);
            Assert.IsTrue(w.AddFlux("borax", 1, 1, out string why, null, 1), why);
            Assert.GreaterOrEqual(w.Crucible.cover, 0.99, "sprinkled over the whole melt, it covers the whole melt");
            CraftWorkshop h = Melt(1000);
            Assert.IsTrue(h.AddFlux("borax", 1, 1, out why, null, 0.5), why);
            Assert.AreEqual(0.5, h.Crucible.cover, 1e-6, "over half of it, half");
        }

        // ------------------------------------------------------------------ r01: the window's pour
        /// <summary>
        /// The crucible area's pour (CraftCrucibleMode.Tick), its numbers: the stream lands at the lip + 18 + 95 × how far it
        /// reaches (<see cref="CraftPourControl.Reach"/>; the hands' drift 0 here), the cup 12 px round at x 474, the lip at
        /// 150 + shift + 160; let go, it tips back with the landing point kept where it was, and the pour is over once the
        /// stream has died away.
        /// </summary>
        private static CraftPourControl WindowPour(double rate, double fillSeconds, float aimOff = 0f)
        {
            const float Cup = 474f, CupRadius = 12f;
            float shift = Math.Max(-60f, Math.Min(136f, Cup - (150f + 160f + 18f + 95f * (float)rate))) + aimOff; // aimed for that stream
            float tilt = 0.25f + 0.6f * (float)rate;
            var p = new CraftPourControl();
            bool back = false;
            float landX = 0f;
            for (int f = 0; f < 60 * 60; f++)
            {
                if (p.Full) back = true; // let go the moment it is full, as the words say
                if (!back) landX = 150f + shift + 160f + 18f + 95f * (float)p.Reach;
                p.Tick(1 / 60.0, back ? 0 : tilt, null, fillSeconds, Math.Abs(landX - Cup) <= CupRadius);
                if (p.Settled(back) && p.Started) break;
            }
            return p;
        }

        [Test]
        public void WindowPour_AStreamHeldInTheCupAndTippedBackWhenFullIsClean_AMissStillSpills()
        {
            foreach (double rate in new[] { 0.45, CraftSmelting.GoodPour, 0.7 })
                foreach (double fill in new[] { 2.0, 4.0, 8.0 })
                {
                    CraftPourControl clean = WindowPour(rate, fill);
                    Assert.IsTrue(clean.Full);
                    Assert.LessOrEqual(clean.Lost, CraftSmelting.LostNamed, $"rate {rate}, {fill} s: aimed and let go when full, {clean.Spilled:0.0000} missed and {clean.Overflow:0.0000} ran over");
                    CraftPourControl miss = WindowPour(rate, fill, 30f);
                    Assert.Greater(miss.Spilled, 0.5, $"rate {rate}, {fill} s: carried past the cup, it splashes on the sand");
                }
        }

        // ------------------------------------------------------------------ r10: the hammer's bar
        [Test]
        public void Hammer_TheBlowTheBarShowsIsTheBlowThatLands_HeldTooLongItSinks()
        {
            foreach (double held in new[] { 0.3, 0.8, 1.5, 2.0, 2.5, 3.0 })
            {
                var hands = new CraftHands(7, 1);
                var hammer = new CraftHammer { fullCharge = 0.7 }; // the ore bench's hammer
                hammer.Tick(5);
                hammer.Raise();
                for (int k = 0; k < (int)(held * 60); k++) hammer.Tick(1 / 60.0);
                double shown = hammer.Expected(hands), unrushed = hammer.Charge * hands.Strength;
                bool wavers = hammer.Wavers;
                CraftBlow b = hammer.Release(0.5, 0, hands);
                Assert.AreEqual(shown, b.force, shown * 0.05, $"held {held} s: the bar shows {shown:0.00}, the blow lands at {b.force:0.00}");
                Assert.AreEqual(held > 0.7 + 0.6, wavers, "it wavers once held past full and the arm's patience");
                if (wavers) Assert.Less(shown, unrushed, "held too long, the bar sinks as the blow weakens");
                if (held >= 2) Assert.Less(shown, unrushed * 0.8, $"held {held} s: well down");
            }
            var tired = new CraftHands(7, 1) { fatigue = 0.8 };
            var h2 = new CraftHammer { fullCharge = 0.7 };
            h2.Tick(5);
            h2.Raise();
            h2.Tick(1);
            Assert.AreEqual(h2.Charge * tired.Strength, h2.Expected(tired), 1e-9, "a tired arm's blow lands lighter, as the bar shows");
        }

        // ------------------------------------------------------------------ r16: the sieve's rings
        [Test]
        public void Sieve_AStoneRingedForTheseHandsFallsThrough_OneJustUnderTheMeshMayNot()
        {
            foreach (double skill in new[] { 0.0, 1.0 })
            {
                var guard = new CraftRequestGuard(1, skill);
                var r = new CraftRequest(CraftRequestKind.Sieve, "x") { a = 1f, b = 1f }; // eight perfectly even shakes
                Assert.IsTrue(guard.Check(ref r, 100, out string note), note);
                double best = CraftRequestGuard.SieveCap(CraftRequestGuard.FloorOf(skill));
                Assert.AreEqual(best, r.b, 1e-6, "the guard holds the shaking to what these hands can do");
                double through = 1.0 * CraftWorkshop.SieveReach(best); // what the window rings white
                CraftWorkshop w = Workshop(CraftTools.OreBench);
                CraftPiece lump = CraftOres.NewLump(w.Rules, "cassiterite", 2, 0);
                lump.ore.fragments.Clear();
                lump.ore.fragments.Add(new OreFragment { mass = 20, mineral = 0.5, gangue = 0.5, size = through - 0.005 }); // ringed
                lump.ore.fragments.Add(new OreFragment { mass = 20, mineral = 0.5, gangue = 0.5, size = through + 0.005 }); // under the mesh, not ringed
                lump.ore.fragments.Add(new OreFragment { mass = 200, mineral = 0.5, gangue = 0.5, size = 5 });
                string id = w.AddPiece(lump);
                string fines = w.SieveOre(id, r.a, out string why, r.b);
                Assert.IsNotNull(fines, why);
                Assert.AreEqual(1, w.Piece(fines).ore.fragments.Count, $"skill {skill}: the ringed stone ({through - 0.005:0.000} cm) falls through, the one just over the ring stays");
                Assert.Less(w.Piece(fines).ore.fragments[0].size, through);
            }
        }

        // ------------------------------------------------------------------ r06: stones lie where they lie
        /// <summary>A lump of hematite crushed a few times on the bench.</summary>
        private static (CraftWorkshop w, string id) Crushed(int blows)
        {
            CraftWorkshop w = Workshop(CraftTools.OreBench);
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 5, 0));
            for (int k = 0; k < blows; k++)
                for (int i = w.Piece(id).ore.fragments.Count - 1; i >= 0 && w.Piece(id).ore.fragments.Count < CraftOres.MaxFragments - 3; i--)
                    w.CrushOre(id, i, 1.0, out _);
            return (w, id);
        }

        [Test]
        public void Tray_StonesLieWhereTheyLie_AThrowOrABlowMovesOnlyThoseItTouches()
        {
            (CraftWorkshop w, string id) = Crushed(3);
            CraftPiece p = w.Piece(id);
            Assert.Greater(p.ore.fragments.Count, 12);
            var lay = new Dictionary<(double, double), (double, double)>();
            List<(double x, double y, double r)> first = CraftOres.Tray(p.ore.fragments, 520, 360, 4.5, 5, 72, lay);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.GreaterOrEqual(first[i].x - first[i].r, -1e-6, "inside the tray");
                Assert.LessOrEqual(first[i].x + first[i].r, 520 + 1e-6);
                Assert.GreaterOrEqual(first[i].y - first[i].r, -1e-6);
                Assert.LessOrEqual(first[i].y + first[i].r, 360 + 1e-6);
                for (int j = i + 1; j < first.Count; j++)
                {
                    double d = Math.Sqrt(Math.Pow(first[i].x - first[j].x, 2) + Math.Pow(first[i].y - first[j].y, 2));
                    Assert.GreaterOrEqual(d, first[i].r + first[j].r - 0.5, $"stones {i} and {j} lie apart");
                }
            }
            // A stone thrown out: the others stay exactly where they lay.
            var before = new Dictionary<(double, double), (double, double)>();
            for (int i = 0; i < first.Count; i++) before[(p.ore.fragments[i].x, p.ore.fragments[i].y)] = (first[i].x, first[i].y);
            Assert.IsTrue(w.DiscardFragment(id, 3, out string why), why);
            List<(double x, double y, double r)> after = CraftOres.Tray(p.ore.fragments, 520, 360, 4.5, 5, 72, lay);
            for (int i = 0; i < after.Count; i++)
            {
                (double x, double y) was = before[(p.ore.fragments[i].x, p.ore.fragments[i].y)];
                Assert.AreEqual(was.x, after[i].x, 1e-6, "a stone thrown out leaves the others where they lie");
                Assert.AreEqual(was.y, after[i].y, 1e-6);
            }
            // A blow: its pieces lie round where the stone lay; the stones away from it stay put.
            (double x, double y) struck = (after[0].x, after[0].y);
            double reach = after[0].r * 3 + 60;
            before.Clear();
            for (int i = 0; i < after.Count; i++) before[(p.ore.fragments[i].x, p.ore.fragments[i].y)] = (after[i].x, after[i].y);
            int n = p.ore.fragments.Count;
            Assert.IsTrue(w.CrushOre(id, 0, 1.5, out why), why);
            Assert.Greater(p.ore.fragments.Count, n, "it split");
            List<(double x, double y, double r)> broken = CraftOres.Tray(p.ore.fragments, 520, 360, 4.5, 5, 72, lay);
            int stayed = 0, far = 0;
            for (int i = 0; i < broken.Count; i++)
            {
                if (!before.TryGetValue((p.ore.fragments[i].x, p.ore.fragments[i].y), out (double x, double y) was))
                {
                    double d = Math.Sqrt(Math.Pow(broken[i].x - struck.x, 2) + Math.Pow(broken[i].y - struck.y, 2));
                    Assert.Less(d, reach, "a piece of the blow lies by the stone it came from");
                    continue;
                }
                if (Math.Sqrt(Math.Pow(was.x - struck.x, 2) + Math.Pow(was.y - struck.y, 2)) < reach) continue;
                far++;
                if (Math.Abs(was.x - broken[i].x) < 1e-6 && Math.Abs(was.y - broken[i].y) < 1e-6) stayed++;
            }
            Assert.Greater(far, 0);
            Assert.AreEqual(far, stayed, "the stones away from the blow stay where they lay");
        }

        // ------------------------------------------------------------------ r20: no more pieces than the bench has room for
        [Test]
        public void Hammer_ABlowMakesNoMorePiecesThanTheBenchHasRoomFor()
        {
            CraftWorkshop w = Workshop(CraftTools.OreBench);
            CraftPiece lump = CraftOres.NewLump(w.Rules, "hematite", 5, 0);
            OreFragment one = lump.ore.fragments[0];
            lump.ore.fragments.Clear();
            for (int i = 0; i < CraftOres.MaxFragments - 1; i++)
            {
                OreFragment f = one.Clone();
                f.mass = 20;
                f.size = 3;
                f.x = 0.05 + 0.9 * (i % 7) / 6.0;
                f.y = 0.05 + 0.9 * (i / 7) / 6.0;
                lump.ore.fragments.Add(f);
            }
            string id = w.AddPiece(lump);
            Assert.AreEqual(2, CraftOres.MostPieces(w.Piece(id)), "room for one more stone: a split in two at the most");
            Assert.IsTrue(w.CrushOre(id, 0, 1.5, out string why), why); // a blow in the 4 band
            Assert.AreEqual(CraftOres.MaxFragments, w.Piece(id).ore.fragments.Count, "it split in two");
            Assert.AreEqual(1, CraftOres.MostPieces(w.Piece(id)), "the bench is full: blows only chip");
        }

        // ------------------------------------------------------------------ r08: the window's brush, side by side
        [Test]
        public void WindowBrush_ASideBrushedIsNotCountedAgain_AndTheStationAgreesWithTheSides()
        {
            CraftWorkshop w = Workshop(CraftTools.OreBench);
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
            CraftOreDefinition o = w.Rules.Ore("hematite");
            OreFragment f = w.Piece(id).ore.fragments[0];
            double[] takesDirt = { CraftOres.BrushOnDirt(CraftOreBrush.Soft, o), CraftOres.BrushOnDirt(CraftOreBrush.Stiff, o), CraftOres.BrushOnDirt(CraftOreBrush.Wire, o) };
            double[] takesRind = { CraftOres.BrushOnRind(CraftOreBrush.Soft), CraftOres.BrushOnRind(CraftOreBrush.Stiff), CraftOres.BrushOnRind(CraftOreBrush.Wire) };
            var cover = new CraftBrushCover();
            double[] eight = Enumerable.Repeat(1.0, 8).ToArray();
            cover.Reset(eight, Enumerable.Repeat(f.dirt, 8).ToArray(), Enumerable.Repeat(f.gangue * f.RindShare, 8).ToArray(), takesDirt, takesRind, new double[3]);
            const double pace = 0.6; // a novice's window: the station counts this share of what is sent
            double Going(int[] scrubbedSides)
            {
                cover.Begin();
                var start = new double[8];
                for (int i = 0; i < 8; i++) start[i] = cover.Done(i, CraftOreBrush.Soft);
                for (int pass = 0; pass < 10; pass++) // scrubbed over and over in one going-over: each side counts once
                    foreach (int i in scrubbedSides)
                        cover.Scrub(i, CraftOreBrush.Soft, Math.Min(start[i] + pace - cover.Done(i, CraftOreBrush.Soft), 0.3 * pace));
                return cover.GoingOver(CraftOreBrush.Soft);
            }
            int[] left = { 3, 4 };
            for (int k = 0; k < 3; k++)
            {
                double going = Going(left);
                if (going > 1e-9)
                {
                    Assert.IsTrue(w.BrushOre(id, 0, CraftOreBrush.Soft, Math.Min(1, going / pace) * pace, out string why), why); // what the station counts of it
                }
                Assert.AreEqual(cover.Covered(CraftOreBrush.Soft), w.Piece(id).ore.fragments[0].brushedSoft, 1e-6, $"going-over {k}: the station counts what the sides show");
            }
            Assert.AreEqual(0.25, w.Piece(id).ore.fragments[0].brushedSoft, 1e-6, "the left quarter scrubbed again and again is a quarter of the stone");
            Assert.AreEqual(0, Going(left), 1e-9, "scrubbed again, it sends nothing more");
            Assert.Greater(Going(new[] { 0, 1, 2, 5, 6, 7 }), 0.4, "the rest of the stone is still to go over");
        }

        // ------------------------------------------------------------------ r17: the furnace's draft
        [Test]
        public void Furnace_FineOreChokesTheDraft_AndTheFurnaceSaysSo()
        {
            CraftWorkshop w = Workshop(CraftTools.Furnace | CraftTools.OreBench);
            CraftPiece lumps = CraftOres.NewLump(w.Rules, "hematite", 3, 0), fines = CraftOres.NewLump(w.Rules, "hematite", 3, 1);
            foreach (OreFragment f in fines.ore.fragments) f.size = 0.6;
            string a = w.AddPiece(lumps), b = w.AddPiece(fines);
            Assert.IsTrue(w.MovePiece(a, CraftLocation.Furnace, out string why), why);
            Assert.AreEqual(0, CraftSmelting.FinesInCharge(w), 1e-9);
            Assert.AreEqual(0, CraftSmelting.Choke(w), 1e-9, "lumps let the air through");
            StringAssert.DoesNotContain("Fine ore", CraftLook.Furnace(w.Rules, w.Furnace, false, CraftSmelting.FinesInCharge(w)));
            Assert.IsTrue(w.MovePiece(b, CraftLocation.Furnace, out why), why);
            double share = CraftSmelting.FinesInCharge(w);
            Assert.AreEqual(fines.ore.Mass / (fines.ore.Mass + lumps.ore.Mass), share, 1e-6);
            Assert.AreEqual(share * 0.6, CraftSmelting.Choke(w), 1e-9, "fines pack the charge");
            StringAssert.Contains("Fine ore packs the charge", CraftLook.Furnace(w.Rules, w.Furnace, false, share));
            w.Furnace.slag = w.Rules.Settings.slagCapacity * 0.9;
            Assert.AreEqual(share * 0.6 + 0.3, CraftSmelting.Choke(w), 1e-9, "and slag left untapped past its tick");
        }
    }
}
#endif
