#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the ore bench, the furnace and the crucible (the smelting area's wave-1 rules): a casting never
    /// holds more metal than the pour took, fastener moulds cast what the pour brought, the ore's rock floats as slag that a
    /// skim takes without the gold, matte and lead carrying silver go back into the furnace, zinc goes up the shaft as
    /// fumes, ore in the furnace is not worked at the bench, the last stone can be thrown out and a stale aim is refused,
    /// fluxes are not metal and go only where they work, a frozen pool is not run out, a bead is lifted out, the pour is
    /// checked before the hands start, the pour control (an upright crucible, the cup's reserve, the metal running out),
    /// which ores the crucible takes and the cake knocked out of it, brass keeping its zinc, gas leaving a frozen melt, the
    /// matte staying under the slag, the water's flow, the guard on the swirl and the sieve, hot ore, and the words.
    /// </summary>
    public class CraftSmeltAreaTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) => new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "smelt");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>The furnace blown hot for <paramref name="seconds"/> (charcoal topped up, the slag tapped now and then).</summary>
        private static void Blow(CraftWorkshop w, int seconds, double target = 1200, bool tap = true, double pump = 0.7)
        {
            for (int t = 0; t < seconds; t++)
            {
                if (w.Furnace.fire.temperature < target) w.Bellows(pump, true);
                if (t % 60 == 0) w.AddFuel("charcoal", 2, out _, true);
                w.RunSteps(10);
                if (tap && t % 150 == 149) w.TapSlag(out _);
            }
        }

        /// <summary>The forge blown hard under the crucible for <paramref name="seconds"/>.</summary>
        private static void HeatCrucible(CraftWorkshop w, int seconds)
        {
            w.AddFuel("charcoal", 10, out _);
            for (int t = 0; t < seconds; t++)
            {
                w.Bellows(0.8);
                if (t % 60 == 0) w.AddFuel("charcoal", 2, out _);
                w.RunSteps(10);
            }
        }

        /// <summary>A melt of <paramref name="grams"/> poured hot enough (the melt's state set directly).</summary>
        private static CraftWorkshop Melt(double grams, string metal = "Bronze", double superheat = 60)
        {
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            foreach ((string element, double fraction) in Metal(metal))
                w.Crucible.melt.Add(new CraftAmount(element, grams * fraction));
            w.Crucible.temperature = CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus + superheat;
            return w;
        }

        private static double MetalIn(CraftPiece p) => p.kind == CraftPieceKind.Fasteners ? p.Mass(Rules()) : p.Mass(Rules()) - p.segments.Sum(s => s.slag);

        // ------------------------------------------------------------------ casting
        [Test]
        public void Cast_NeverHoldsMoreMetalThanThePourTook()
        {
            foreach (double grams in new[] { 1.0, 5, 20, 60, 300, 900, 3000 })
                foreach (CraftMold m in Rules().AllMolds())
                {
                    CraftWorkshop w = Melt(grams);
                    double before = w.Crucible.Mass;
                    string id = w.Pour(m.id, 0.6, 0.9, 0, out string why);
                    Assert.IsNotNull(id, why);
                    CraftPiece cast = w.Piece(id);
                    double taken = before - w.Crucible.Mass;
                    if (cast == null)
                    {
                        Assert.AreEqual(0, taken, 1e-9, $"{m.id} from {grams} g: nothing set, nothing taken");
                        continue;
                    }
                    Assert.LessOrEqual(MetalIn(cast), taken + 1e-6, $"{m.id} from {grams} g: the casting holds no more metal than left the crucible");
                    Assert.LessOrEqual(cast.Length, m.length + 1e-6, $"{m.id}: never longer than the mould");
                }
        }

        [Test]
        public void Cast_ASmallMeltCastsAShortPiece_NotAFullOne()
        {
            CraftWorkshop w = Melt(60);
            CraftMold mold = w.Rules.Mold("blade_blank");
            double need = CraftSmelting.MoldMass(w.Rules, mold, CraftSmelting.MeltProfile(w.Rules, w.Crucible).Density);
            Assert.Greater(need, 600, "a blade blank takes far more bronze than 60 g");
            string id = w.Pour("blade_blank", 0.6, 0.9, 0, out string why);
            CraftPiece cast = w.Piece(id);
            Assert.IsNotNull(cast, why);
            Assert.LessOrEqual(MetalIn(cast), 60 + 1e-6, "it holds the 60 g there was");
            Assert.Less(cast.Length, mold.length * 0.15, "a short stub from the mouth, not a whole blade blank");
            Assert.IsTrue(cast.defects.Any(d => d.kind == CraftDefectKind.Misrun && d.cause.Contains("too little metal")));
        }

        [Test]
        public void FastenerMould_CastsOnlyWhatThePourBrought()
        {
            CraftWorkshop a = Melt(400);
            double before = a.Crucible.Mass;
            string id = a.Pour("rivets", 0.6, 0.9, 0, out string why, 0, 0, 0.05);
            Assert.IsNotNull(id, why);
            Assert.IsNull(a.Piece(id), "a trickle of 5 % fills not one rivet");
            Assert.AreEqual(before, a.Crucible.Mass, 1e-9, "and what ran in goes back into the melt");

            CraftWorkshop b = Melt(400);
            CraftMold rivets = b.Rules.Mold("rivets");
            CraftPiece half = b.Piece(b.Pour("rivets", 0.6, 0.9, 0, out why, 0, 0, 0.5));
            Assert.IsNotNull(half, why);
            Assert.LessOrEqual(half.fasteners.count, rivets.count / 2, "half a pour casts half a set at the most");
            Assert.Greater(half.fasteners.count, 0);
        }

        [Test]
        public void Pour_LosesWhatMissedTheMould_ByTheAmountMeasured()
        {
            CraftWorkshop w = Melt(1500);
            CraftMold ingot = w.Rules.Mold("ingot");
            double need = CraftSmelting.MoldMass(w.Rules, ingot, CraftSmelting.MeltProfile(w.Rules, w.Crucible).Density);
            double before = w.Crucible.Mass;
            CraftPiece cast = w.Piece(w.Pour("ingot", 0.6, 0.9, 0, out string why, 0.3, 0, 1, 0.3));
            Assert.IsNotNull(cast, why);
            double lost = before - w.Crucible.Mass - MetalIn(cast);
            Assert.AreEqual(0.3 * need, lost, need * 0.02, "0.3 of a mould on the sand is that much metal lost");

            CraftWorkshop s = Melt(1500);
            before = s.Crucible.Mass;
            string id = s.Pour("ingot", 0.6, 0.9, 0, out why, 2, 0, 0.001, 0.4);
            Assert.IsNotNull(id, why);
            Assert.IsNull(s.Piece(id), "nothing landed in the mould: nothing set");
            Assert.AreEqual(0.4 * need, before - s.Crucible.Mass, need * 0.02, "but what splashed on the sand is gone");
        }

        // ------------------------------------------------------------------ the crucible's slag and ores
        [Test]
        public void Skim_TakesTheOresRock_NotTheGold()
        {
            CraftWorkshop w = Workshop(CraftTools.OreBench | CraftTools.Forge | CraftTools.Crucible);
            string oid = w.AddPiece(CraftOres.NewLump(w.Rules, "gold_quartz", 11, 0));
            Assert.IsTrue(w.MovePiece(oid, CraftLocation.Melt, out string why), why);
            HeatCrucible(w, 400);
            Assert.IsNull(w.Piece(oid), "the ore is spent");
            double gold = CraftSmelting.Grams(w.Crucible.melt, "gold");
            Assert.Greater(gold, 10);
            Assert.Greater(w.Crucible.slag, 500, "its quartz floats on the melt as slag");
            Assert.Less(w.Crucible.dross, 50, "not as the oxide dross");
            Assert.IsTrue(w.SkimDross(1, out why), why);
            Assert.Greater(CraftSmelting.Grams(w.Crucible.melt, "gold"), gold * 0.97, "a careful skim keeps the gold");
            Assert.Less(w.Crucible.slag, 1, "and takes the rock");
        }

        [Test]
        public void Crucible_TakesOnlyOresItCanMelt()
        {
            CraftWorkshop w = Workshop();
            string Try(string ore)
            {
                string id = w.AddPiece(CraftOres.NewLump(w.Rules, ore, 3, 0));
                return w.MovePiece(id, CraftLocation.Melt, out string why) ? "" : why;
            }
            StringAssert.Contains("furnace", Try("hematite"), "iron ore is smelted in the furnace");
            StringAssert.Contains("furnace", Try("meteorite"));
            StringAssert.Contains("flux", Try("mythril_ore"));
            StringAssert.Contains("roast", Try("chalcopyrite"), "an unroasted sulfide ruins a melt");
            Assert.AreEqual("", Try("malachite"));
            Assert.AreEqual("", Try("native_silver"));
            Assert.AreEqual("", Try("gold_quartz"));
            Assert.AreEqual("", Try("calamine"), "zinc ore for cementation");
        }

        [Test]
        public void FrozenMelt_IsKnockedOutAsACake_WithAllItsMetal()
        {
            CraftWorkshop w = Melt(800);
            Assert.IsNull(w.KnockOut(out string why), "molten: it is poured, not knocked out");
            StringAssert.Contains("molten", why);
            CraftAlloyProfile prof = CraftSmelting.MeltProfile(w.Rules, w.Crucible);
            w.Crucible.temperature = prof.Solidus - 200;
            double metal = w.Crucible.Mass;
            Assert.IsFalse(w.StirMelt(1, out why), "a solid cake is not stirred");
            string id = w.KnockOut(out why);
            Assert.IsNotNull(id, why);
            CraftPiece cake = w.Piece(id);
            Assert.AreEqual(metal, MetalIn(cake), metal * 0.01, "the cake is the melt, no more and no less");
            Assert.AreEqual(0, w.Crucible.Mass, 1e-9);
        }

        [Test]
        public void CanPour_SaysWhyBeforeThePour()
        {
            CraftWorkshop w = Workshop(CraftTools.Forge | CraftTools.Crucible);
            Assert.IsFalse(w.CanPour(false, out string why));
            Assert.AreEqual("The crucible is empty.", why);
            Assert.IsFalse(w.AddFlux("borax", 1, 1, out why), "nothing to cover yet");
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 100, 20, 6, Metal("Bronze")));
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Melt, out why), why);
            Assert.IsFalse(w.CanPour(false, out why));
            Assert.AreEqual("Something in the crucible has not melted yet.", why, "a solid bar is not an empty crucible");
            Assert.IsNull(w.Pour("ingot", 0.6, 0.9, 0, out string pw));
            Assert.AreEqual(why, pw, "the pour refuses for the same reason");

            CraftWorkshop o = Workshop();
            string ore = o.AddPiece(CraftOres.NewLump(o.Rules, "malachite", 3, 0));
            Assert.IsTrue(o.MovePiece(ore, CraftLocation.Melt, out why), why);
            Assert.IsFalse(o.CanPour(false, out why));
            StringAssert.Contains("ore is still in the crucible", why);
        }

        [Test]
        public void Brass_KeepsItsZinc_UnderACover_WhilePureZincBoilsAway()
        {
            CraftWorkshop w = Melt(1000, "Brass", 40);
            Assert.IsTrue(w.AddFlux("borax", 1, 1, out string why, null, 1), why);
            double zinc0 = CraftSmelting.Grams(w.Crucible.melt, "zinc");
            for (int t = 0; t < 120; t++)
            {
                w.Crucible.temperature = CraftSmelting.MeltProfile(w.Rules, w.Crucible).Liquidus + 40; // held just above its liquidus
                w.RunSteps((int)Math.Round(1 / w.Rules.Settings.stepSeconds));
            }
            Assert.Greater(CraftSmelting.Grams(w.Crucible.melt, "zinc"), zinc0 * 0.9, "two minutes covered cost a covered brass little of its zinc");
            Assert.AreEqual("brass", CraftSmelting.MeltProfile(w.Rules, w.Crucible).Class?.id);

            CraftElement zn = w.Rules.Element("zinc");
            Assert.Greater(CraftSmelting.Boil(zn, 100, 1, true, 1100, 0), CraftSmelting.Boil(zn, 30, 0.3, false, 1100, 0) * 10, "pure zinc boils far faster than zinc dissolved in copper");
        }

        [Test]
        public void Gas_LeavesAMeltThatFroze()
        {
            CraftWorkshop w = Melt(500);
            w.Crucible.gas = 1;
            CraftAlloyProfile prof = CraftSmelting.MeltProfile(w.Rules, w.Crucible);
            for (int t = 0; t < 120; t++)
            {
                w.Crucible.temperature = prof.Solidus - 100;
                w.RunSteps((int)Math.Round(1 / w.Rules.Settings.stepSeconds));
            }
            Assert.Less(w.Crucible.gas, 0.01, "frozen, it lets its gas go: a reheat starts clean");
            Assert.Less(CraftSmelting.GasChange(30, 1), 0, "held just above its liquidus under a cover, it settles");
            Assert.Greater(CraftSmelting.GasChange(250, 0), 0, "held far too hot, it takes gas up");

            CraftWorkshop g = Melt(1500);
            g.Crucible.gas = 0.4;
            CraftPiece cast = g.Piece(g.Pour("ingot", 0.6, 0.95, 0, out string why));
            Assert.IsNotNull(cast, why);
            CraftDefect pores = cast.defects.FirstOrDefault(d => d.kind == CraftDefectKind.Porosity);
            Assert.IsNotNull(pores);
            StringAssert.Contains("took up", pores.cause, "the pores are blamed on the gas the melt held, not on a steady pour");
        }

        // ------------------------------------------------------------------ the furnace
        [Test]
        public void Calamine_ZincGoesUpTheShaftAsFumes()
        {
            CraftWorkshop w = Workshop();
            w.AddFuel("charcoal", 10, out _, true);
            string oid = w.AddPiece(CraftOres.NewLump(w.Rules, "calamine", 3, 0));
            Assert.IsTrue(w.MovePiece(oid, CraftLocation.Furnace, out string why), why);
            Blow(w, 600, 1150);
            Assert.AreEqual(0, CraftSmelting.Grams(w.Furnace.pool, "zinc"), 1e-9, "no liquid zinc pools in a furnace");
            Assert.Greater(w.Furnace.fumes, 100, "it went up the shaft");
            Assert.Less(w.Furnace.BloomMass, CraftSmelting.BloomMinimum, "only its little iron stays, in the bloom");
            Assert.IsNull(w.TakeMelt("ingot", 0.5, 0.9, out why), "there is nothing to pour");
        }

        [Test]
        public void MatteAndSilverLead_GoBackIntoTheFurnace()
        {
            CraftWorkshop w = Workshop();
            string matte = w.AddPiece(CraftDefaults.Bar(w.Rules, 60, 20, 10, ("copper", 0.75), ("sulfur", 0.25)));
            double copper = w.Piece(matte).Mass(w.Rules) * 0.75;
            Assert.IsTrue(w.MovePiece(matte, CraftLocation.Furnace, out string why), "a matte goes back in to be roasted: " + why);
            w.AddFuel("charcoal", 10, out _, true);
            Blow(w, 400, 1300, true, 1);
            Assert.IsNull(w.Piece(matte), "it melts down into the bottom");
            Assert.Greater(CraftSmelting.Grams(w.Furnace.pool, "copper"), copper * 0.8, "its copper joins the pool");
            Assert.AreEqual(0, CraftSmelting.Grams(w.Furnace.pool, "sulfur"), 1e-9, "its sulfur goes to the matte, where the blast can roast it");

            CraftWorkshop g = Workshop();
            string lead = g.AddPiece(CraftDefaults.Bar(g.Rules, 80, 20, 10, ("lead", 0.99), ("silver", 0.01)));
            double silver = g.Piece(lead).Mass(g.Rules) * 0.01;
            Assert.IsTrue(g.MovePiece(lead, CraftLocation.Furnace, out why), "lead carrying silver goes back in to be cupelled: " + why);
            for (int k = 0; k < 6; k++) Assert.IsTrue(g.AddFlux("bone_ash", 1, 0, out why), why);
            g.AddFuel("charcoal", 10, out _, true);
            Blow(g, 1500, 1300, true, 1);
            Assert.Less(g.Furnace.PoolMass, CraftSmelting.PourMinimum, "the lead has soaked into the cupels");
            string bead = g.LiftBead(out why);
            Assert.IsNotNull(bead, why);
            Assert.AreEqual(silver, g.Piece(bead).Grams("silver"), silver * 0.05, "the silver comes out as a bead");

            Assert.IsFalse(g.MovePiece(g.AddPiece(Fasteners(g)), CraftLocation.Furnace, out why), "fasteners are melted down in the crucible");
        }

        private static CraftPiece Fasteners(CraftWorkshop w)
        {
            var p = new CraftPiece { kind = CraftPieceKind.Fasteners };
            p.fasteners.kind = CraftFastenerKind.Rivet;
            p.fasteners.count = 4;
            p.fasteners.massEach = 5;
            p.composition.Add(new CraftAmount("copper", 1));
            p.segments.Add(new MetalSegment { length = 14, width = 5, thickness = 5 });
            return p;
        }

        [Test]
        public void Bead_IsLiftedFromTheFurnace_WhenTooLittleToPour()
        {
            CraftWorkshop w = Workshop();
            w.Furnace.pool.Add(new CraftAmount("silver", 1.3));
            w.Furnace.pool.Add(new CraftAmount("lead", 0.2));
            w.Furnace.fire.temperature = 1000;
            Assert.IsFalse(w.CanPour(true, out string why));
            StringAssert.Contains("bead", why, "too little to pour: the refusal says what to do instead");
            string bead = w.LiftBead(out why);
            Assert.IsNotNull(bead, why);
            Assert.AreEqual(1.5, w.Piece(bead).Mass(w.Rules), 0.05, "the bead is the metal there was");
            Assert.AreEqual(0, w.Furnace.PoolMass, 1e-9);

            CraftWorkshop big = Workshop();
            big.Furnace.pool.Add(new CraftAmount("copper", 300));
            Assert.IsNull(big.LiftBead(out why), "enough to pour is poured");
        }

        [Test]
        public void TakeMelt_RefusesAFrozenPool_AndABrokenStreamLeavesAColdShut()
        {
            CraftWorkshop w = Workshop();
            w.Furnace.pool.Add(new CraftAmount("copper", 900));
            w.Furnace.fire.temperature = 900;
            Assert.IsNull(w.TakeMelt("ingot", 0.6, 0.9, out string why));
            StringAssert.Contains("frozen", why);
            w.Furnace.fire.temperature = 1200;
            CraftPiece cast = w.Piece(w.TakeMelt("ingot", 0.6, 0.9, out why, 0, 0, 1, 2));
            Assert.IsNotNull(cast, why);
            Assert.IsTrue(cast.defects.Any(d => d.kind == CraftDefectKind.ColdShut), "a stream from the furnace that broke leaves a cold shut too");
        }

        [Test]
        public void FurnaceFluxes_AreNotMetal_AndGoOnlyWhereTheyWork()
        {
            CraftWorkshop w = Workshop();
            Assert.IsFalse(w.AddFlux("borax", 1, 0, out string why), "borax does nothing in the furnace");
            StringAssert.Contains("covers a melt", why);
            Assert.IsFalse(w.AddFlux("sand", 1, 0, out why));
            Assert.IsTrue(w.AddFlux("arcane_flux", 1, 0, out why), why);
            Assert.IsTrue(w.AddFlux("limestone", 1, 0, out why), why);
            Assert.IsTrue(w.AddFlux("bone_ash", 1, 0, out why), why);
            Assert.AreEqual(0, w.Furnace.PoolMass, 1e-9, "a flux waiting for its ore is not liquid metal");
            Assert.Greater(CraftSmelting.FluxOf(w.Furnace, "arcane_flux"), 0);
            StringAssert.DoesNotContain("Liquid metal", CraftLook.Furnace(w.Rules, w.Furnace, true));

            CraftWorkshop c = Melt(300);
            Assert.IsFalse(c.AddFlux("limestone", 1, 1, out why), "limestone does not cover a melt");
            Assert.IsTrue(c.AddFlux("borax", 1, 1, out why), why);
        }

        [Test]
        public void TappingTheSlag_LeavesTheMatte()
        {
            CraftWorkshop w = Workshop();
            w.Furnace.slag = 500;
            w.Furnace.matte = 40;
            w.Furnace.fire.temperature = 1250;
            Assert.IsTrue(w.TapSlag(out string why), why);
            Assert.AreEqual(40, w.Furnace.matte, 1e-9, "the matte lies under the slag: only roasting takes its sulfur");
        }

        // ------------------------------------------------------------------ the ore bench
        [Test]
        public void ChargedOre_IsNotWorkedAtTheBench()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
            for (int f = w.Piece(id).ore.fragments.Count - 1; f >= 0; f--) Assert.IsTrue(w.CrushOre(id, f, 1.2, out string cw), cw);
            Assert.IsTrue(w.MovePiece(id, CraftLocation.Furnace, out string why), why);
            int stones = w.Piece(id).ore.fragments.Count;
            Assert.IsFalse(w.WashOre(id, 0.5, 5, 0.5, out why));
            StringAssert.Contains("furnace", why);
            Assert.IsFalse(w.DiscardFragment(id, 0, out why));
            Assert.IsNull(w.SieveOre(id, 2, out why));
            Assert.IsFalse(w.CrushOre(id, 0, 1, out why));
            Assert.IsFalse(w.BrushOre(id, 0, CraftOreBrush.Stiff, 1, out why));
            Assert.AreEqual(stones, w.Piece(id).ore.fragments.Count, "nothing was taken out of the burning charge");
            Assert.IsFalse(w.MovePiece(id, CraftLocation.Air, out why), "it is not fished out of the charge either");

            CraftWorkshop bare = Workshop(CraftTools.Furnace);
            string lump = bare.AddPiece(CraftOres.NewLump(bare.Rules, "hematite", 4, 0));
            Assert.IsFalse(bare.DiscardFragment(lump, 0, out why), "stones are sorted at an ore bench");
        }

        [Test]
        public void Discard_TheLastStone_EmptiesTheLump_AndAStaleAimIsRefused()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
            int layout = CraftOres.Layout(w.Piece(id));
            Assert.IsTrue(w.CrushOre(id, 0, 1.2, out string why, layout), why);
            Assert.IsFalse(w.CrushOre(id, 0, 1.2, out why, layout), "aimed at the bench as it lay before that blow");
            StringAssert.Contains("moved", why);
            Assert.IsFalse(w.DiscardFragment(id, 0, out why, layout));
            Assert.IsTrue(w.DiscardFragment(id, 0, out why, CraftOres.Layout(w.Piece(id))), why);
            while (w.Piece(id) != null)
                Assert.IsTrue(w.DiscardFragment(id, 0, out why), "every stone can be thrown out, the last one too: " + why);
        }

        [Test]
        public void Washing_AGentleFlowKeepsMoreOreForTheSameWork()
        {
            double OreLost(double flow)
            {
                CraftWorkshop w = Workshop();
                string id = w.AddPiece(CraftOres.NewLump(w.Rules, "hematite", 4, 0));
                for (int pass = 0; pass < 3; pass++)
                    for (int f = w.Piece(id).ore.fragments.Count - 1; f >= 0; f--) w.CrushOre(id, f, 1.2, out _);
                double ore = w.Piece(id).ore.MineralMass, rock = w.Piece(id).ore.Mass - ore;
                Assert.IsTrue(w.WashOre(id, flow, 6 / flow, 0.6, out string why), why);
                double oreLost = ore - w.Piece(id).ore.MineralMass, rockLost = rock - (w.Piece(id).ore.Mass - w.Piece(id).ore.MineralMass);
                return oreLost / Math.Max(1e-9, rockLost);
            }
            Assert.Less(OreLost(0.2), OreLost(1) * 0.7, "a strong flow is quick, but it carries the heavy ore off with the rock");
        }

        [Test]
        public void Guard_HoldsTheSwirlAndTheSieveToTheHands()
        {
            var guard = new CraftRequestGuard(5, 0);
            var wash = new CraftRequest(CraftRequestKind.Wash, "x") { a = 0.5f, b = 6, c = 1 };
            Assert.IsTrue(guard.Check(ref wash, 10, out string why), why);
            Assert.LessOrEqual(wash.c, 1 - 4 * guard.Floor + 1e-6, "a swirl is never steadier than these hands");
            var sieve = new CraftRequest(CraftRequestKind.Sieve, "x") { a = 1, b = 0 };
            Assert.IsTrue(guard.Check(ref sieve, 11, out why), why);
            Assert.Greater(sieve.b, 0, "a sieving that claims nothing is not read as an old, unmeasured one (all of it)");
        }

        [Test]
        public void HotOre_CoolsOnTheBench_AndIsNotCarriedHot()
        {
            CraftWorkshop w = Workshop();
            string id = w.AddPiece(CraftOres.NewLump(w.Rules, "malachite", 4, 0));
            w.Piece(id).ore.temperature = 1100;
            Assert.IsFalse(CraftWorkshop.CanTake(w.Piece(id), out string why));
            StringAssert.Contains("too hot", why);
            Assert.IsFalse(w.WashOre(id, 0.5, 5, 0.5, out _), "nor worked at the bench while it glows");
            w.RunSteps((int)Math.Round(300 / w.Rules.Settings.stepSeconds));
            Assert.IsTrue(CraftWorkshop.CanTake(w.Piece(id), out why), why);
        }

        // ------------------------------------------------------------------ the pour control
        [Test]
        public void PourControl_AnUprightCrucibleNeverPours()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var hands = new CraftHands(seed) { fatigue = 1 };
                var p = new CraftPourControl();
                for (int k = 0; k < 100; k++) p.Tick(0.02, 0, hands);
                Assert.AreEqual(0, p.Poured, 1e-12, "the hands' tremble moves the tilt, not the stream");
                Assert.AreEqual(0, p.Interruptions);
            }
        }

        [Test]
        public void PourControl_APromptTipBackIsClean_ALateOneRunsOver()
        {
            foreach (double rate in new[] { 0.42, 0.6, 0.75 })
            {
                double tilt = 0.25 + rate * 0.6;
                var prompt = new CraftPourControl();
                for (int k = 0; k < 2000 && !prompt.Full; k++) prompt.Tick(0.016, tilt, null);
                for (int k = 0; k < 100; k++) prompt.Tick(0.016, 0, null);
                Assert.LessOrEqual(prompt.OverflowShare, CraftPourControl.Flash, $"rate {rate}: tipped back at once, the cup takes the last of the stream");

                var late = new CraftPourControl();
                for (int k = 0; k < 2000 && !late.Full; k++) late.Tick(0.016, tilt, null);
                for (int k = 0; k < 40; k++) late.Tick(0.016, tilt, null);
                for (int k = 0; k < 100; k++) late.Tick(0.016, 0, null);
                Assert.Greater(late.OverflowShare, CraftPourControl.Flash, $"rate {rate}: poured on, it runs over");
            }
        }

        [Test]
        public void PourControl_TheStreamStopsWhenTheMetalRunsOut_WithoutAColdShut()
        {
            var p = new CraftPourControl { Supply = 0.4 };
            for (int k = 0; k < 1000; k++) p.Tick(0.016, 0.7, null);
            Assert.AreEqual(0.4, p.Given, 1e-9, "no more leaves the crucible than it held");
            Assert.IsTrue(p.Empty);
            Assert.AreEqual(0, p.Interruptions, "running dry is not a broken stream");
        }

        // ------------------------------------------------------------------ the words
        [Test]
        public void Words_FollowTheHeatAndTheAir()
        {
            var fire = new CraftFireState { fuel = 10, level = 1, temperature = 950, air = 0.2, damper = 1 };
            StringAssert.Contains("orange", CraftLook.Fire(fire, false, true), "firewood's heat glows orange");
            fire.temperature = 1550;
            StringAssert.Contains("white", CraftLook.Fire(fire, false, true));
            fire.air = 1;
            fire.reducing = 0;
            StringAssert.Contains("oxidising", CraftLook.Fire(fire, false, true));

            CraftWorkshop w = Workshop();
            CraftPiece ore = w.Piece(w.AddPiece(CraftOres.NewLump(w.Rules, "chalcopyrite", 3, 0)));
            ore.ore.roasted = 0.5;
            Assert.IsTrue(CraftLook.Describe(w.Rules, ore, null, CraftInstruments.None, false, false).Any(l => l.Contains("partly roasted")), "half roasted is not roasted: it still gives a matte");

            CraftWorkshop c = Melt(1000, "Bronze", 15);
            StringAssert.Contains("sluggish", CraftLook.Crucible(c.Rules, c.Crucible, null, false), "too cold to fill a mould");
            c.Crucible.temperature = CraftSmelting.MeltProfile(c.Rules, c.Crucible).Liquidus + 60;
            StringAssert.Contains("bright and fluid", CraftLook.Crucible(c.Rules, c.Crucible, null, false));
            c.Crucible.gas = 0.5;
            StringAssert.Contains("spits and bubbles", CraftLook.Crucible(c.Rules, c.Crucible, null, false));

            CraftWorkshop f = Workshop();
            f.Furnace.pool.Add(new CraftAmount("copper", 400));
            f.Furnace.fire.temperature = 900;
            StringAssert.Contains("frozen", CraftLook.Furnace(f.Rules, f.Furnace, false));
        }
    }
}
#endif
