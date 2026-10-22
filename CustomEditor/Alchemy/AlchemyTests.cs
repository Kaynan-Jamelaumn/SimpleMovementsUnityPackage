#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AlchemySystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window ▸ General ▸ Test Runner) of the alchemy simulation on the built-in rules: reproducibility
    /// (same steps, replay, saved state), time / temperature / order / quantity / equipment / surroundings changing the
    /// result, the same ingredient doing different things by the mixture's state, catalysts and inhibitors,
    /// interruption, partial bottling, failures, the journal, and extending the rules without code changes.
    /// </summary>
    public class AlchemyTests
    {
        private static AlchemyRules Rules() => AlchemyDefaults.Rules();

        private static AlchemyEquipmentProfile Cauldron() => AlchemyEquipmentProfile.DefaultCauldron();

        private static AlchemyEquipmentProfile Vessel(string name) => AlchemyDefaults.Equipment().Find(e => e.displayName == name);

        private static void Add(AlchemyBrew b, string ingredient, int count = 1)
        {
            Assert.IsTrue(b.AddIngredient(AlchemyDefaults.Spec(ingredient), count, out string why), why);
        }

        /// <summary>2 water + 3 sunpetals on the fire at <paramref name="heat"/>.</summary>
        private static AlchemyBrew Sunpetals(AlchemyRules rules, double heat, AlchemyEquipmentProfile vessel = null, int seed = 1)
        {
            var b = new AlchemyBrew(rules, vessel ?? Cauldron(), 20, seed);
            Add(b, "Spring Water", 2);
            Add(b, "Sunpetal", 3);
            b.SetHeat(heat);
            return b;
        }

        private static string Outcome(AlchemyBrew b) => b.Evaluate().OutcomeId;

        private static double Difference(AlchemyBrew a, AlchemyBrew b)
        {
            double d = Math.Abs(a.Mixture.Temperature - b.Mixture.Temperature);
            for (int i = 0; i < a.Rules.SubstanceCount; i++)
                d += Math.Abs(a.Mixture.Amount(i) - b.Mixture.Amount(i));
            return d;
        }

        // ------------------------------------------------------------------ configuration
        [Test]
        public void BuiltInRules_CompileAndValidate()
        {
            AlchemyRules rules = Rules();
            Assert.IsEmpty(rules.CompileErrors, string.Join("\n", rules.CompileErrors));
            Assert.IsEmpty(rules.CompileWarnings, string.Join("\n", rules.CompileWarnings));
            var errors = new List<string>();
            var warnings = new List<string>();
            foreach (AlchemyReaction r in rules.ReactionDefinitions) r.Validate(rules, errors, warnings);
            foreach (AlchemyOutcome o in rules.Outcomes) o.Validate(rules, errors, warnings);
            foreach (AlchemyDefaults.IngredientPreset p in AlchemyDefaults.Ingredients()) p.profile.Validate(rules, p.name, errors, warnings);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.Greater(rules.Reactions.Length, 15);
            Assert.Greater(rules.Outcomes.Length, 10);
        }

        // ------------------------------------------------------------------ reproducibility
        [Test]
        public void SameOperations_GiveTheSameMixture()
        {
            AlchemyRules rules = Rules();
            AlchemyBrew a = Sunpetals(rules, 0.35), b = Sunpetals(rules, 0.35);
            a.RunUntil(70); b.RunUntil(70);
            a.Stir(); b.Stir();
            Add(a, "Frostmint"); Add(b, "Frostmint");
            a.Advance(133.37); b.RunUntil(a.Elapsed);
            Assert.AreEqual(0.0, Difference(a, b), 1e-12);
            Assert.AreEqual(a.Evaluate().DisplayName, b.Evaluate().DisplayName);
        }

        [Test]
        public void Replay_OfTheHistory_ReproducesThePreparation()
        {
            AlchemyRules rules = Rules();
            AlchemyBrew a = Sunpetals(rules, 0.35, null, 7);
            a.RunUntil(50); a.Stir(); Add(a, "Frostmint"); a.RunUntil(140); a.Bottle(2); a.SetHeat(0.5); a.RunUntil(260);
            AlchemyBrew b = AlchemyBrew.Replay(rules, Cauldron(), 20, 7, a.History, a.Mixture.Step);
            Assert.AreEqual(a.Mixture.Step, b.Mixture.Step);
            Assert.AreEqual(0.0, Difference(a, b), 1e-12);
        }

        [Test]
        public void History_CarriesTheSurroundings()
        {
            // A station sets its surroundings as the first operations: the history alone replays the preparation.
            AlchemyRules rules = Rules();
            var a = new AlchemyBrew(rules, Cauldron(), rules.Settings.defaultAmbient, 2);
            a.SetAmbient(4);
            a.SetVariable(AlchemyVariables.Night, 1);
            Assert.AreEqual(4.0, a.Mixture.Temperature, 1e-9, "an empty vessel starts at the surroundings' temperature");
            Add(a, "Spring Water", 2); Add(a, "Moonbloom", 2); a.SetHeat(0.15);
            a.RunUntil(60); a.SetAmbient(9); a.RunUntil(150);
            AlchemyBrew b = AlchemyBrew.Replay(rules, Cauldron(), rules.Settings.defaultAmbient, 2, a.History, a.Mixture.Step);
            Assert.AreEqual(0.0, Difference(a, b), 1e-12);
        }

        [Test]
        public void SavedState_ContinuesExactly()
        {
            AlchemyRules rules = Rules();
            AlchemyBrew a = Sunpetals(rules, 0.35);
            a.RunUntil(45);
            Add(a, "Bitterroot");
            a.RunUntil(52.4);
            AlchemyBrew b = AlchemyBrew.Restore(rules, AlchemyBrewState.FromJson(a.Capture().ToJson()));
            Assert.AreEqual(0.0, Difference(a, b), 1e-9);
            Assert.AreEqual(a.Mixture.Pending.Count, b.Mixture.Pending.Count, "the bitterroot still dissolving is kept");
            a.RunUntil(400); b.RunUntil(400);
            Assert.AreEqual(0.0, Difference(a, b), 1e-9);
            Assert.AreEqual(a.History.Count, b.History.Count);
        }

        [Test]
        public void RandomVariable_IsReproducibleBySeed()
        {
            AlchemyRules rules = Rules();
            foreach (CompiledReaction r in rules.Reactions) r.Definition.rateVariance = 0.5f;
            AlchemyBrew a = Sunpetals(rules, 0.35, null, 3), b = Sunpetals(rules, 0.35, null, 3), c = Sunpetals(rules, 0.35, null, 4);
            a.RunUntil(200); b.RunUntil(200); c.RunUntil(200);
            Assert.AreEqual(0.0, Difference(a, b), 1e-12, "same seed, same result");
            Assert.Greater(Difference(a, c), 1e-6, "another seed varies");
        }

        // ------------------------------------------------------------------ time and temperature
        [Test]
        public void Time_MovesThePreparationFromOneResultToTheNext()
        {
            AlchemyBrew b = Sunpetals(Rules(), 0.35);
            b.RunUntil(60);
            Assert.AreEqual("healing_draught", Outcome(b), "type A first");
            b.RunUntil(180);
            Assert.AreEqual("vigor_draught", Outcome(b), "then type B");
            b.RunUntil(1200);
            Assert.AreEqual("spoiled_mixture", Outcome(b), "too long: degraded");
        }

        [Test]
        public void Temperature_ChangesWhatTheSameIngredientsBecome()
        {
            AlchemyBrew cool = Sunpetals(Rules(), 0.24), hot = Sunpetals(Rules(), 0.35);
            cool.RunUntil(900); hot.RunUntil(900);
            Assert.AreEqual("healing_draught", Outcome(cool), "below 75 °C vital essence keeps");
            Assert.AreNotEqual(Outcome(cool), Outcome(hot));
            Assert.Greater(cool.Evaluate().Quality, 0.6);
        }

        [Test]
        public void Boiling_HoldsTheTemperatureAndConcentrates()
        {
            AlchemyBrew b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(b, "Spring Water", 2);
            b.SetHeat(0.8);
            b.RunUntil(90);
            Assert.That(b.Mixture.Temperature, Is.InRange(99.0, 102.0), "boiling water stays near 100 °C");
            Assert.Less(b.Mixture.Amount(AlchemyDefaults.Water), 19.0, "water boils away");
            Assert.IsTrue(b.IsBoiling);
            b.RunUntil(600);
            Assert.Greater(b.Mixture.Temperature, 150.0, "once it has all boiled off the vessel heats up");
        }

        // ------------------------------------------------------------------ adding during the process
        [Test]
        public void SameIngredient_RedirectsByTheMixturesState()
        {
            AlchemyRules rules = Rules();
            AlchemyBrew a = Sunpetals(rules, 0.35);
            a.RunUntil(90);
            a.SetHeat(0.1);
            Add(a, "Frostmint", 3);
            a.RunUntil(300);
            Assert.AreEqual("calming_tonic", Outcome(a), "into vital essence: type C");

            AlchemyBrew b = Sunpetals(rules, 0.35);
            b.RunUntil(300);
            Add(b, "Frostmint", 3);
            b.RunUntil(480);
            Assert.AreEqual("fortitude_elixir", Outcome(b), "into ardent essence: type D");
        }

        [Test]
        public void VoidSalt_GivesTheSameResultWhateverTheMixtureWasBecoming()
        {
            foreach (double at in new[] { 90.0, 300.0 })
            {
                AlchemyBrew b = Sunpetals(Rules(), 0.35);
                b.RunUntil(at);
                Add(b, "Void Salt", 2);
                b.RunUntil(at + 60);
                Assert.AreEqual("stillwater_draught", Outcome(b), $"added at {at} s");
            }
        }

        [Test]
        public void Bitterroot_TaintsAHealingPreparation()
        {
            AlchemyBrew b = Sunpetals(Rules(), 0.24);
            b.RunUntil(90);
            Add(b, "Bitterroot");
            b.RunUntil(160);
            Assert.AreEqual("tainted_healing", Outcome(b));
        }

        [Test]
        public void Order_OfAdditionMatters()
        {
            AlchemyRules rules = Rules();
            // Lead dust first: nothing is ever extracted. After the extraction: it stops the healing essence turning into ardent essence.
            var first = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(first, "Spring Water", 2); Add(first, "Lead Dust"); Add(first, "Sunpetal", 3); first.SetHeat(0.35);
            AlchemyBrew later = Sunpetals(rules, 0.35);
            later.RunUntil(60); Add(later, "Lead Dust");
            AlchemyBrew none = Sunpetals(rules, 0.35);
            foreach (AlchemyBrew b in new[] { first, later, none }) b.RunUntil(600);
            Assert.AreEqual(AlchemyOutcomeKind.Incomplete, first.Evaluate().Kind);
            Assert.AreEqual("healing_draught", Outcome(later), "the transformation was interrupted");
            Assert.AreNotEqual("healing_draught", Outcome(none));
        }

        // ------------------------------------------------------------------ what speeds up, slows down, stops
        [Test]
        public void Catalysts_Inhibitors_AndStabilisers()
        {
            AlchemyRules rules = Rules();
            int vital = rules.IndexOf(AlchemyDefaults.VitalEssence), ardent = rules.IndexOf(AlchemyDefaults.ArdentEssence);
            AlchemyBrew plain = Sunpetals(rules, 0.35);
            AlchemyBrew quick = Sunpetals(rules, 0.35); Add(quick, "Quicksilver Drop");
            AlchemyBrew lead = Sunpetals(rules, 0.35); Add(lead, "Lead Dust");
            AlchemyBrew salt = Sunpetals(rules, 0.35); Add(salt, "Rock Salt");
            foreach (AlchemyBrew b in new[] { plain, quick, lead, salt }) b.RunUntil(40);
            Assert.Greater(quick.Mixture.Amount(vital), plain.Mixture.Amount(vital) * 1.1, "quicksilver speeds the extraction");
            Assert.Less(lead.Mixture.Amount(vital), 0.05, "lead dust stops it");
            foreach (AlchemyBrew b in new[] { plain, salt }) b.RunUntil(400);
            Assert.Greater(salt.Mixture.Amount(vital) + salt.Mixture.Amount(ardent), plain.Mixture.Amount(vital) + plain.Mixture.Amount(ardent),
                "brine slows the kindling and the burn-out");
        }

        // ------------------------------------------------------------------ quantity, equipment, surroundings
        [Test]
        public void Quantity_DilutionLowersTheQuality()
        {
            AlchemyRules rules = Rules();
            AlchemyBrew strong = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(strong, "Spring Water", 1); Add(strong, "Sunpetal", 3); strong.SetHeat(0.24);
            AlchemyBrew weak = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(weak, "Spring Water", 4); Add(weak, "Sunpetal", 3); weak.SetHeat(0.24);
            strong.RunUntil(400); weak.RunUntil(400);
            Assert.AreEqual("healing_draught", Outcome(strong));
            Assert.AreEqual("healing_draught", Outcome(weak));
            Assert.Greater(strong.Evaluate().Quality, weak.Evaluate().Quality + 0.2);
            Assert.Greater(weak.Evaluate().Bottles, strong.Evaluate().Bottles, "more liquid, more bottles");
        }

        [Test]
        public void Equipment_AlembicCatchesWhatTheCauldronLoses()
        {
            AlchemyRules rules = Rules();
            AlchemyBrew Make(AlchemyEquipmentProfile v)
            {
                var b = new AlchemyBrew(rules, v, 20, 1);
                Add(b, "Lamp Oil"); Add(b, "Sunpetal", 3); b.SetHeat(0.4);
                b.RunUntil(300); b.SetHeat(0.55); b.RunUntil(330); Add(b, "Frostmint"); b.RunUntil(420);
                return b;
            }
            AlchemyBrew alembic = Make(Vessel("Copper Alembic")), cauldron = Make(Cauldron());
            Assert.AreEqual("quickstep_elixir", alembic.EvaluateDistillate().OutcomeId, "the ember spirit is distilled");
            Assert.AreEqual(0.0, cauldron.Mixture.DistillateVolume, 1e-9, "an open cauldron collects nothing");
            Assert.Greater(cauldron.Tracker.Vented[rules.IndexOf(AlchemyDefaults.EmberSpirit)], 0.1, "it escaped");
        }

        [Test]
        public void Surroundings_NightAndStillnessGrowCrystals()
        {
            AlchemyRules rules = Rules();
            int crystal = rules.IndexOf(AlchemyDefaults.MoonCrystal);
            AlchemyBrew Make(bool night, bool stir)
            {
                var b = new AlchemyBrew(rules, Vessel("Frost Basin"), 20, 1);
                if (night) b.SetVariable(AlchemyVariables.Night, 1, 0);
                Add(b, "Spring Water", 2); Add(b, "Moonbloom", 2); Add(b, "Rock Salt", 2); b.SetHeat(-0.8);
                for (int t = 5; t <= 60; t += 5) { b.RunUntil(t); if (stir) b.Stir(); }
                return b;
            }
            AlchemyBrew dayStill = Make(false, false), nightStill = Make(true, false), nightStirred = Make(true, true);
            Assert.Greater(nightStill.Mixture.Amount(crystal), dayStill.Mixture.Amount(crystal) * 1.5, "faster at night");
            Assert.Less(nightStirred.Mixture.Amount(crystal), nightStill.Mixture.Amount(crystal) * 0.5, "stirring stops crystals forming");
            AlchemyHarvest h = nightStill.Empty();
            Assert.IsTrue(h.Items.Exists(i => i.Substance == AlchemyDefaults.MoonCrystal && i.Count > 0), "crystals are harvested");
        }

        [Test]
        public void SealedVessel_BurstsWhenOverheated()
        {
            var b = new AlchemyBrew(Rules(), Vessel("Glass Retort"), 20, 1);
            Add(b, "Spring Water", 1); Add(b, "Sunpetal");
            bool burst = false;
            b.EventRaised += e => burst |= e.Kind == AlchemyEventKind.VesselRuptured;
            b.SetHeat(1);
            b.RunUntil(200);
            Assert.IsTrue(burst);
            Assert.IsTrue(b.Mixture.Ruptured);
        }

        [Test]
        public void ViolentReaction_Erupts()
        {
            var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(b, "Grain Spirit", 2);
            b.SetHeat(0.2);
            b.RunUntil(60);
            double before = b.Mixture.Volume + b.Mixture.PendingVolume;
            int eruptions = 0;
            b.EventRaised += e => { if (e.Kind == AlchemyEventKind.Eruption) eruptions++; };
            Add(b, "Embercap");
            b.RunUntil(100);
            Assert.Greater(eruptions, 0);
            Assert.Less(b.Mixture.Volume, before);
        }

        // ------------------------------------------------------------------ ingredients: substances, forms, grinding
        [Test]
        public void Ingredient_GivesUpEachSubstanceAtItsOwnPace()
        {
            AlchemyRules rules = Rules();
            var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(b, "Spring Water", 2); Add(b, "Sunpetal", 3); b.SetHeat(0.24);
            b.RunUntil(60);
            PendingIngredient petals = b.Mixture.Pending[0];
            double sap = 0, latex = 0;
            for (int k = 0; k < petals.Substances.Length; k++)
            {
                string id = rules.Substances[petals.Substances[k]].id;
                if (id == AlchemyDefaults.SolarSap) sap = petals.Released[k] / petals.Totals[k];
                if (id == AlchemyDefaults.BitterLatex) latex = petals.Released[k] / petals.Totals[k];
            }
            Assert.Greater(sap, 0.95, "the sap comes out readily");
            Assert.Less(latex, 0.1, "the latex locked in the stems barely does");
        }

        [Test]
        public void Grinding_IsFaster_ButBringsOutTheBitterness()
        {
            AlchemyRules rules = Rules();
            int sap = rules.IndexOf(AlchemyDefaults.SolarSap);
            AlchemyBrew Make(string form)
            {
                var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
                Add(b, "Spring Water", 2);
                Assert.IsTrue(b.AddIngredient(AlchemyDefaults.Spec("Sunpetal"), 3, out string why, form), why);
                b.SetHeat(0.24);
                return b;
            }
            AlchemyBrew whole = Make(null), ground = Make("ground");
            whole.RunUntil(5); ground.RunUntil(5);
            Assert.Greater(ground.Mixture.Amount(sap), whole.Mixture.Amount(sap) * 1.5, "ground petals give up their sap faster");
            whole.RunUntil(300); ground.RunUntil(300);
            Assert.AreEqual("healing_draught", Outcome(whole));
            Assert.AreEqual("tainted_healing", Outcome(ground), "and their latex too");
            StringAssert.Contains("(finely ground)", string.Join("|", AlchemyJournal.Procedure(ground)));
        }

        [Test]
        public void ThermalStability_AShortBoilDestroysTheToxin_ThenTheBrewCools()
        {
            var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(b, "Spring Water", 2);
            Assert.IsTrue(b.AddIngredient(AlchemyDefaults.Spec("Sunpetal"), 3, out string why, "ground"), why);
            b.SetHeat(0.5);
            b.RunUntil(30);
            b.SetHeat(0.2);
            b.RunUntil(300);
            Assert.AreEqual("healing_draught", Outcome(b), "the latex broke down above 85 °C, then the essence was kept below 75 °C");
            Assert.Less(b.Mixture.Amount(AlchemyDefaults.BitterLatex), 0.3);
            Assert.Greater(b.Mixture.Amount(AlchemyDefaults.ArdentEssence), 0.5, "the price: some essence cooked on during the boil");
        }

        [Test]
        public void Extract_GivesItsSubstancesAtOnce_AndPurer()
        {
            AlchemyRules rules = Rules();
            int sap = rules.IndexOf(AlchemyDefaults.SolarSap);
            var extract = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(extract, "Spring Water", 2); Add(extract, "Sunpetal Extract", 3); extract.SetHeat(0.24);
            AlchemyBrew petals = Sunpetals(rules, 0.24);
            extract.RunUntil(3); petals.RunUntil(3);
            Assert.Greater(extract.Mixture.Amount(sap), petals.Mixture.Amount(sap) * 3, "an extract is already drawn out");
            extract.RunUntil(240); petals.RunUntil(240);
            Assert.Greater(extract.Evaluate().Purity, petals.Evaluate().Purity, "no oil, no latex");
        }

        // ------------------------------------------------------------------ solubility
        [Test]
        public void Solubility_TheWrongSolventLeavesItUndissolved()
        {
            AlchemyRules rules = Rules();
            int dew = rules.IndexOf(AlchemyDefaults.MoonDew);
            AlchemyBrew Make(string liquid)
            {
                var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
                Add(b, liquid, 2); Add(b, "Moonbloom", 2); b.SetHeat(0.15);
                b.RunUntil(240);
                return b;
            }
            AlchemyBrew water = Make("Spring Water"), oil = Make("Lamp Oil");
            Assert.AreEqual("mana_potion", Outcome(water));
            Assert.Greater(oil.Mixture.UndissolvedAmount(dew) + oil.Mixture.UndissolvedAmount(rules.IndexOf(AlchemyDefaults.ManaEssence)), 5.0,
                "oil holds neither moon dew nor mana essence");
            Assert.Less(oil.Evaluate().Quality, water.Evaluate().Quality - 0.5, "what little the oil holds makes a feeble potion");
            Assert.IsTrue(oil.Evaluate().Notes.Exists(n => n.Contains("will not dissolve")));

            AlchemyBrew petals = Sunpetals(rules, 0.24);
            petals.RunUntil(120);
            Assert.Greater(petals.Mixture.UndissolvedAmount(rules.IndexOf(AlchemyDefaults.PetalOil)), 0.3, "water barely holds the petal oil: it floats");
        }

        [Test]
        public void Solubility_TooLittleWaterThenMoreWater()
        {
            AlchemyRules rules = Rules();
            int brine = rules.IndexOf(AlchemyDefaults.Brine);
            var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(b, "Spring Water", 1); Add(b, "Rock Salt", 3);
            b.RunUntil(60);
            Assert.Greater(b.Mixture.UndissolvedAmount(brine), 4.0, "10 u of water holds 3.6 u of salt");
            Add(b, "Spring Water", 2);
            b.Stir();
            b.RunUntil(180);
            Assert.Less(b.Mixture.UndissolvedAmount(brine), 0.2, "more water dissolves the rest");
        }

        [Test]
        public void Solubility_BoilingDownOrAnotherSolventPrecipitates()
        {
            AlchemyRules rules = Rules();
            int brine = rules.IndexOf(AlchemyDefaults.Brine);
            var boil = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(boil, "Spring Water", 2); Add(boil, "Rock Salt", 2);
            boil.RunUntil(30);
            Assert.Less(boil.Mixture.UndissolvedAmount(brine), 0.05);
            boil.SetHeat(1);
            boil.RunUntil(90);
            Assert.Greater(boil.Mixture.UndissolvedAmount(brine), 1.0, "salt crystallises out as the water boils away");

            var spirit = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(spirit, "Spring Water", 2); Add(spirit, "Rock Salt", 2);
            spirit.RunUntil(30);
            bool precipitated = false;
            spirit.EventRaised += e => precipitated |= e.Kind == AlchemyEventKind.Precipitated && e.Id == AlchemyDefaults.Brine;
            Add(spirit, "Grain Spirit", 3);
            spirit.RunUntil(60);
            Assert.Greater(spirit.Mixture.UndissolvedAmount(brine), 2.0, "spirit salts it out");
            Assert.IsTrue(precipitated);
            AlchemyHarvest h = spirit.Empty();
            Assert.IsTrue(h.Items.Exists(i => i.Substance == AlchemyDefaults.Brine && i.Count > 0), "the settled salt is collected");
        }

        [Test]
        public void Solubility_TooMuchWaterPrecipitatesAVenomDrawnInSpirit()
        {
            AlchemyRules rules = Rules();
            int venom = rules.IndexOf(AlchemyDefaults.Venom);
            var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(b, "Grain Spirit", 2); Add(b, "Bitterroot", 2); b.SetHeat(0.18);
            b.RunUntil(180);
            Assert.AreEqual("venom_extract", Outcome(b));
            double before = b.Mixture.Amount(venom);
            Add(b, "Spring Water", 3);
            b.RunUntil(240);
            Assert.Greater(b.Mixture.UndissolvedAmount(venom), 0.5);
            Assert.Less(b.Mixture.Amount(venom), before * 0.8);
        }

        // ------------------------------------------------------------------ thermal stability
        [Test]
        public void ThermalStability_HotterIsNotAlwaysBetter()
        {
            AlchemyRules rules = Rules();
            Assert.IsNotNull(rules.Reaction("breakdown_hot." + AlchemyDefaults.ManaEssence), "stability becomes a reaction of its own");
            int mana = rules.IndexOf(AlchemyDefaults.ManaEssence);
            AlchemyBrew Make(double heat)
            {
                // Extracted gently, then (or not) put on a strong fire.
                var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Moonbloom", 3); b.SetHeat(0.15);
                b.RunUntil(200);
                b.SetHeat(heat);
                b.RunUntil(500);
                return b;
            }
            AlchemyBrew gentle = Make(0.15), hard = Make(0.4);
            Assert.AreEqual("mana_potion", Outcome(gentle));
            Assert.Less(hard.Mixture.Amount(mana), gentle.Mixture.Amount(mana) * 0.5, "mana essence breaks down above 85 °C");
            Assert.Greater(hard.TotalOf(rules.ReactionIndex("breakdown_hot." + AlchemyDefaults.ManaEssence)), 1.0);

            var d = new AlchemyJournalData();
            for (int i = 0; i < 2; i++)
            {
                AlchemyBrew again = Make(0.4);
                AlchemyJournal.Observe(d, again);
            }
            AlchemyJournal.Identify(d, rules, AlchemyDefaults.ManaEssence);
            List<string> known = AlchemyJournal.SubstanceBehaviour(d, rules, rules.Substance(AlchemyDefaults.ManaEssence));
            Assert.IsTrue(known.Exists(l => l.Contains("85")), "once understood, the journal says where it breaks down");
        }

        // ------------------------------------------------------------------ interrupting, bottling, failures
        [Test]
        public void Suspend_StopsTimeAndKeepsTheState()
        {
            AlchemyBrew b = Sunpetals(Rules(), 0.35);
            b.RunUntil(40);
            AlchemyBrewState before = b.Capture();
            b.Suspend();
            Assert.AreEqual(0, b.Advance(300));
            AlchemyBrew copy = AlchemyBrew.Restore(b.Rules, before);
            Assert.AreEqual(0.0, Difference(b, copy), 1e-12);
            b.Resume();
            Assert.Greater(b.Advance(10), 0);
        }

        [Test]
        public void PartialBottling_LeavesTheRestToTransform()
        {
            AlchemyBrew b = Sunpetals(Rules(), 0.35);
            b.RunUntil(60);
            AlchemyResult first = b.Bottle(3);
            Assert.AreEqual("healing_draught", first.OutcomeId);
            Assert.AreEqual(3, first.Bottles);
            Assert.Greater(b.Mixture.LiquidVolume, 5.0);
            b.RunUntil(200);
            Assert.AreEqual("vigor_draught", Outcome(b), "what was left went on");
        }

        [Test]
        public void FailedExperiments_AreResultsToo()
        {
            AlchemyRules rules = Rules();
            AlchemyBrew early = Sunpetals(rules, 0.3);
            early.RunUntil(15);
            Assert.AreEqual(AlchemyOutcomeKind.Incomplete, early.Evaluate().Kind);

            var water = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(water, "Spring Water", 2);
            AlchemyResult plain = water.Evaluate();
            Assert.AreEqual(AlchemyOutcomeKind.Inert, plain.Kind);
            Assert.AreEqual(0, water.Bottle().Bottles, "plain water bottles nothing");

            AlchemyBrew lead = Sunpetals(rules, 0.35);
            Add(lead, "Lead Dust");
            lead.RunUntil(300);
            Assert.AreEqual(AlchemyOutcomeKind.Incomplete, lead.Evaluate().Kind, "nothing reacted");

            var hot = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Add(hot, "Spring Water", 2); Add(hot, "Moonbloom", 3); hot.SetHeat(0.15); hot.RunUntil(200);
            Add(hot, "Quicksilver Drop"); hot.SetHeat(0.3); hot.RunUntil(250);
            Assert.AreEqual(AlchemyOutcomeKind.Unstable, hot.Evaluate().Kind);
        }

        [Test]
        public void Capacity_IsRespected()
        {
            var b = new AlchemyBrew(Rules(), Vessel("Glass Retort"), 20, 1);
            Add(b, "Spring Water", 2);
            Assert.IsFalse(b.AddIngredient(AlchemyDefaults.Spec("Sunpetal"), 1, out string why));
            StringAssert.Contains("room", why);
            Assert.AreEqual(0, b.HowManyFit(AlchemyDefaults.Spec("Sunpetal")));
        }

        // ------------------------------------------------------------------ learning
        [Test]
        public void Journal_LearnsFromRepeatedExperiments()
        {
            AlchemyRules rules = Rules();
            var d = new AlchemyJournalData();
            AlchemyBrew first = Sunpetals(rules, 0.24);
            first.RunUntil(240);
            AlchemyJournal.Observe(d, first);
            Assert.IsFalse(AlchemyJournal.IsUnderstood(d, rules, "vital_extraction"), "seen once");
            Assert.IsFalse(AlchemyJournal.IsIdentified(d, rules, AlchemyDefaults.VitalEssence));
            AlchemyJournal.Observe(d, first);
            Assert.AreEqual(1, AlchemyJournal.ReactionNote(d, "vital_extraction").timesSeen, "once per preparation");

            AlchemyResult r = first.Bottle();
            List<string> learnt = AlchemyJournal.RecordResult(d, first, r, r.Bottles, 1);
            Assert.IsTrue(AlchemyJournal.IsDiscovered(d, "healing_draught"));
            Assert.IsTrue(AlchemyJournal.IsIdentified(d, rules, AlchemyDefaults.VitalEssence), "bottling identifies the compound");
            Assert.IsTrue(learnt.Exists(l => l.Contains("Healing Draught")));
            Assert.IsNotEmpty(AlchemyJournal.Discovery(d, "healing_draught").bestProcedure);

            AlchemyBrew second = Sunpetals(rules, 0.24);
            second.RunUntil(240);
            AlchemyJournal.Observe(d, second);
            Assert.IsTrue(AlchemyJournal.IsUnderstood(d, rules, "vital_extraction"), "seen in two preparations");
            Assert.IsTrue(AlchemyJournal.IsIdentified(d, rules, AlchemyDefaults.SolarSap));

            AlchemyJournalData back = AlchemyJournalData.FromJson(d.ToJson());
            Assert.IsTrue(AlchemyJournal.IsUnderstood(back, rules, "vital_extraction"));
            Assert.AreEqual(d.experiments.Count, back.experiments.Count);
        }

        [Test]
        public void Procedure_ReadsLikeTheStepsTaken()
        {
            AlchemyBrew b = Sunpetals(Rules(), 0.35);
            b.RunUntil(10); b.Stir(); b.Stir(); b.RunUntil(60); b.Bottle(2);
            List<string> p = AlchemyJournal.Procedure(b);
            Assert.IsTrue(p.Exists(l => l.Contains("Added 3 × Sunpetal")));
            Assert.IsTrue(p.Exists(l => l.Contains("Fire 35%")));
            Assert.IsTrue(p.Exists(l => l.Contains("Stirred × 2")));
            Assert.IsTrue(p.Exists(l => l.Contains("Bottled 2")));
        }

        // ------------------------------------------------------------------ extending without code changes
        [Test]
        public void Extensions_AddAndReplaceRules()
        {
            var baseDb = ScriptableObject.CreateInstance<AlchemyDatabase>();
            var pack = ScriptableObject.CreateInstance<AlchemyDatabase>();
            try
            {
                AlchemyDefaults.Fill(baseDb);
                pack.substances.Add(new AlchemySubstance { id = "glow_essence", displayName = "Glow Essence", role = SubstanceRole.Active, boilingPoint = 200f });
                pack.reactions.Add(new AlchemyReaction { id = "glowing" }.Consumes(AlchemyDefaults.MoonDew).Makes("glow_essence").Speed(0.2f, 20f, 0f));
                pack.reactions.Add(new AlchemyReaction { id = "moon_infusion", enabled = false });
                pack.outcomes.Add(new AlchemyOutcome { id = "glow_potion", displayName = "Glow Potion", priority = 65, potency = SubstanceSelector.Of("glow_essence") }
                    .Requires(new DominantCondition("glow_essence")).Tier("", 0f, 1f));
                baseDb.extensions.Add(pack);
                baseDb.MarkChanged();
                AlchemyRules rules = baseDb.Rules;
                Assert.IsEmpty(rules.CompileErrors);
                Assert.IsNotNull(rules.Reaction("glowing"));
                Assert.IsNull(rules.Reaction("moon_infusion"), "disabled by the pack");
                var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Moonbloom", 2); b.SetHeat(0.15);
                b.RunUntil(120);
                Assert.AreEqual("glow_potion", Outcome(b), "the existing Moonbloom takes part in the new reaction");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseDb);
                UnityEngine.Object.DestroyImmediate(pack);
            }
        }

        // Not [Serializable]: it would show in the designers' condition dropdowns whenever the tests compile.
        private class ResonanceCondition : AlchemyCondition
        {
            public override bool IsMet(IAlchemyState state) => state.Variable("resonance") >= 0.5;
            public override string Describe(AlchemyRules rules) => "near a resonance stone";
        }

        [Test]
        public void CustomConditions_PlugIntoReactions()
        {
            var props = AlchemyDefaults.Properties();
            var subs = AlchemyDefaults.Substances();
            var reactions = AlchemyDefaults.Reactions();
            AlchemyReaction infusion = reactions.Find(r => r.id == "moon_infusion");
            infusion.conditions.Add(new ResonanceCondition());
            AlchemyRules rules = AlchemyRules.Compile(new AlchemySimulationSettings(), props, subs, reactions, AlchemyDefaults.Outcomes());
            int mana = rules.IndexOf(AlchemyDefaults.ManaEssence);
            AlchemyBrew Make(double resonance)
            {
                var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
                b.SetVariable("resonance", resonance, 0);
                Add(b, "Spring Water", 2); Add(b, "Moonbloom", 2); b.SetHeat(0.15);
                b.RunUntil(120);
                return b;
            }
            Assert.Less(Make(0).Mixture.Amount(mana), 0.01);
            Assert.Greater(Make(1).Mixture.Amount(mana), 1.0);
            StringAssert.Contains("resonance", infusion.Describe(rules));
        }

        [Test]
        public void BrewedPotion_PouredBack_ReleasesWhatItIs()
        {
            AlchemyRules rules = Rules();
            AlchemyOutcome healing = rules.Outcome("healing_draught");
            AlchemyIngredientProfile p = BrewedPotionSO.Composition(healing, healing.tiers[2], rules);
            var b = new AlchemyBrew(rules, Cauldron(), 20, 1);
            Assert.IsTrue(b.AddIngredient("potion", "Healing Draught", p, 2, out string why), why);
            b.RunSteps(1);
            Assert.AreEqual("healing_draught", Outcome(b));
        }

        // ------------------------------------------------------------------ fire, stirring, baths
        private static void Add(AlchemyBrew b, string ingredient, int count, string form, float evenness = 1f)
        {
            Assert.IsTrue(b.AddIngredient(AlchemyDefaults.Spec(ingredient), count, out string why, form, evenness), why);
        }

        private static double Units(AlchemyBrew b, string substance) => b.Mixture.Amount(b.Rules.IndexOf(substance));

        private static double Undissolved(AlchemyBrew b, string substance) => b.Mixture.UndissolvedAmount(b.Rules.IndexOf(substance));

        [Test]
        public void ManualFire_BurnsDownUnlessFed_TheBellowsRaiseIt()
        {
            var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(b, "Spring Water", 2); Add(b, "Sunpetal", 3);
            bool died = false;
            b.EventRaised += e => died |= e.Kind == AlchemyEventKind.FireOut;
            Assert.IsTrue(b.AddFuel("firewood", 1, out string why), why);
            b.RunUntil(60);
            Assert.Greater(b.Mixture.FireLevel, 0.2);
            Assert.Greater(b.Mixture.Temperature, 60.0);
            double before = b.Mixture.FireLevel;
            for (int i = 0; i < 6; i++) { b.Bellows(1); b.RunUntil(b.Elapsed + 1); }
            Assert.Greater(b.Mixture.FireLevel, before + 0.05, "the bellows fan the flames");
            b.RunUntil(1500);
            Assert.IsTrue(died, "unfed, the fire goes out");
            Assert.Less(b.Mixture.Temperature, 30.0);
        }

        [Test]
        public void Stirring_SpeedsExtraction_TooHardSlopsOverAndBrowns()
        {
            AlchemyBrew Run(double speed, double until)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Sunpetal", 3); b.SetHeat(0.24); b.SetStirring(speed);
                b.RunUntil(until);
                return b;
            }
            Assert.Greater(Units(Run(0.5, 10), AlchemyDefaults.SolarSap), Units(Run(0, 10), AlchemyDefaults.SolarSap) * 1.5, "stirring speeds extraction");
            AlchemyBrew hard = Run(1, 180), calm = Run(0.4, 180);
            Assert.Less(hard.Mixture.LiquidVolume, calm.Mixture.LiquidVolume - 3, "stirred too hard, it slops over");
            Assert.Greater(Units(hard, AlchemyDefaults.Sludge), Units(calm, AlchemyDefaults.Sludge) + 0.5, "air beaten in browns it");
        }

        [Test]
        public void PoorFireControl_ScorchesAnUnstirredBottom_AWaterBathDoesNot()
        {
            AlchemyBrew Run(int mode)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Sunpetal", 3); b.SetHeat(1);
                if (mode == 1) b.SetStirring(0.4);
                if (mode == 2) b.SetBath(AlchemyBath.Water);
                b.RunUntil(90);
                return b;
            }
            Assert.Greater(Units(Run(0), AlchemyDefaults.Char), 0.3, "a roaring fire under a still pot burns the bottom");
            Assert.Less(Units(Run(1), AlchemyDefaults.Char), 0.05, "stirring keeps it from catching");
            AlchemyBrew bath = Run(2);
            Assert.Less(Units(bath, AlchemyDefaults.Char), 0.05);
            Assert.Less(bath.Mixture.Temperature, 97.0, "a water bath never goes much above 96 °C");
        }

        [Test]
        public void Evaporation_BelowBoiling_ConcentratesFasterInAWideDish()
        {
            double Left(AlchemyEquipmentProfile vessel)
            {
                var b = new AlchemyBrew(Rules(), vessel, 20, 1);
                Add(b, "Seawater"); b.SetHeat(0.3);
                b.RunUntil(600);
                Assert.Less(b.Mixture.Temperature, 99.0, "it never boils");
                return Units(b, AlchemyDefaults.Water);
            }
            double dish = Left(Vessel("Evaporating Dish")), cauldron = Left(Cauldron());
            Assert.Less(cauldron, 9.0, "a simmer loses water");
            Assert.Less(dish, cauldron - 0.5, "a wide dish loses it faster");
        }

        [Test]
        public void BoilingSeawaterDry_LeavesAllItsSalt()
        {
            var b = new AlchemyBrew(Rules(), Vessel("Evaporating Dish"), 20, 1);
            Add(b, "Seawater", 2); b.SetHeat(0.6);
            b.RunUntil(360);
            int brine = b.Rules.IndexOf(AlchemyDefaults.Brine);
            Assert.Less(Units(b, AlchemyDefaults.Water), 0.05, "dry");
            Assert.Greater(b.Mixture.Amount(brine) + b.Mixture.UndissolvedAmount(brine), 2.95, "the salt does not leave with the steam");
            AlchemyHarvest h = b.Empty();
            Assert.IsTrue(h.Items.Exists(i => i.Substance == AlchemyDefaults.Brine && i.Count == 1), "two seawater give a rock salt");
        }

        [Test]
        public void FractionatingStill_CollectsAPurerSpirit()
        {
            double WaterShare(string vessel)
            {
                var b = new AlchemyBrew(Rules(), Vessel(vessel), 20, 1);
                Add(b, "Spring Water"); Add(b, "Grain Spirit", 2); b.SetHeat(0.45);
                b.RunUntil(100);
                double d = b.Mixture.DistillateVolume;
                Assert.Greater(d, 0.3);
                return b.Mixture.DistillateAmount(b.Rules.IndexOf(AlchemyDefaults.Water)) / d;
            }
            Assert.Less(WaterShare("Fractionating Still"), WaterShare("Copper Alembic") * 0.6);
        }

        [Test]
        public void Fermentation_TurnsHoneyIntoMead_TooHotKillsTheYeast()
        {
            AlchemyBrew Run(double heat)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Honey", 2); Add(b, "Brewer's Yeast"); b.SetHeat(heat);
                b.RunUntil(900);
                return b;
            }
            AlchemyBrew warm = Run(0.05), hot = Run(0.15);
            Assert.AreEqual("honey_mead", Outcome(warm));
            Assert.Greater(Units(warm, AlchemyDefaults.Spirit), 2.0);
            Assert.Less(Units(hot, AlchemyDefaults.Yeast), 0.01, "above 42 °C the yeast dies");
            Assert.AreNotEqual("honey_mead", Outcome(hot));
        }

        // ------------------------------------------------------------------ crystals, layers, separation
        [Test]
        public void Crystals_GrowStillAndSlow_StirredOrCrashCooledItIsAPowder()
        {
            AlchemyBrew Run(int mode)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Alum Powder", 4); b.SetHeat(0.4);
                b.RunUntil(120);
                Assert.Greater(Units(b, AlchemyDefaults.Alum), 7.9, "hot water holds it all");
                b.SetHeat(0);
                if (mode == 1) b.SetStirring(0.5);
                if (mode == 2) b.SetBath(AlchemyBath.Ice);
                b.RunUntil(1200);
                return b;
            }
            AlchemyBrew still = Run(0), stirred = Run(1), iced = Run(2);
            Assert.Greater(Units(still, AlchemyDefaults.AlumCrystal), 5.0, "left still to cool: crystals");
            Assert.Less(Units(stirred, AlchemyDefaults.AlumCrystal), 0.1);
            Assert.Greater(Undissolved(stirred, AlchemyDefaults.Alum), 5.0, "stirred: a powder");
            Assert.Greater(Undissolved(iced, AlchemyDefaults.Alum), Units(iced, AlchemyDefaults.AlumCrystal) * 3, "crash-cooled: mostly powder");
            Assert.AreEqual(3, still.Empty().Items.Find(i => i.Substance == AlchemyDefaults.AlumCrystal).Count);
        }

        [Test]
        public void Supersaturation_WaitsForASeedCrystal()
        {
            var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(b, "Spring Water"); Add(b, "Saltpetre Powder", 2); b.SetHeat(0.12);
            b.RunUntil(400);
            b.SetHeat(0);
            b.RunUntil(1000);
            int niter = b.Rules.IndexOf(AlchemyDefaults.Niter);
            Assert.Greater(b.Mixture.Amount(niter), b.Mixture.Capacity(niter) * 1.1, "unseeded, it stays supersaturated");
            Assert.Less(Units(b, AlchemyDefaults.NiterCrystal), 0.01);
            Add(b, "Saltpetre Crystals");
            b.RunUntil(1400);
            Assert.Less(b.Mixture.Amount(niter), b.Mixture.Capacity(niter) * 1.03, "a seed crystal sets it crystallising");
            Assert.Greater(Units(b, AlchemyDefaults.NiterCrystal), 2.5);
        }

        [Test]
        public void Layers_KeepReactantsApart_StirringOrAnEmulsifierBringsThemTogether()
        {
            AlchemyBrew Run(int mode)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Sunpetal", 3); b.SetHeat(0.24);
                b.RunUntil(180);
                Add(b, "Lamp Oil", 2); Add(b, "Fire Pepper", 4, "ground");
                if (mode == 2) Add(b, "Egg Yolk");
                if (mode >= 1) b.SetStirring(0.5);
                if (mode == 2) { b.RunUntil(200); b.SetStirring(0); }
                b.RunUntil(480);
                Assert.IsTrue(b.Mixture.IsLayered);
                return b;
            }
            AlchemyBrew still = Run(0), stirred = Run(1), egg = Run(2);
            Assert.Greater(Units(stirred, AlchemyDefaults.SalamanderEssence), Units(still, AlchemyDefaults.SalamanderEssence) * 2, "oil and essence meet when stirred");
            Assert.AreEqual("salamander_draught", Outcome(stirred));
            Assert.Greater(egg.Mixture.Emulsion, 0.3, "egg yolk keeps the emulsion after the stirring stops");
            Assert.Greater(Units(egg, AlchemyDefaults.SalamanderEssence), Units(still, AlchemyDefaults.SalamanderEssence) * 2);
        }

        [Test]
        public void Decant_PoursOffTheOilLayerAndWhatItHolds()
        {
            var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(b, "Spring Water", 2); Add(b, "Lamp Oil", 2); Add(b, "Lavender", 3, "ground"); b.SetHeat(0.24);
            b.RunUntil(300);
            double lavender = Units(b, AlchemyDefaults.LavenderOil);
            AlchemyHarvest top = b.Decant(out string why);
            Assert.IsNotNull(top, why);
            Assert.AreEqual("lavender_tincture", top.Liquid.OutcomeId);
            Assert.Less(Units(b, AlchemyDefaults.LavenderOil), lavender * 0.1, "the oil took the lavender oil with it");
            Assert.Less(Units(b, AlchemyDefaults.Oil), 0.01);
            Assert.Greater(Units(b, AlchemyDefaults.Water), 18.0, "the water stays");
            Assert.IsFalse(b.Mixture.IsLayered);
        }

        [Test]
        public void Filtration_RemovesParticles_CharcoalAdsorbsToxins()
        {
            var muddy = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(muddy, "Muddy Water", 2); Add(muddy, "Sunpetal", 3); muddy.SetHeat(0.24);
            muddy.RunUntil(180);
            Assert.AreEqual("murky_healing", Outcome(muddy), "unfiltered, the silt spoils it");
            Assert.IsNotNull(muddy.Filter("paper", out string why), why);
            Assert.AreEqual("healing_draught", Outcome(muddy));

            var bitter = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(bitter, "Spring Water", 2); Add(bitter, "Sunpetal", 3, "ground"); bitter.SetHeat(0.24);
            bitter.RunUntil(180);
            Assert.AreEqual("tainted_healing", Outcome(bitter));
            Assert.IsNotNull(bitter.Filter("charcoal", out why), why);
            Assert.AreEqual("healing_draught", Outcome(bitter), "the charcoal bed held back the latex");
        }

        [Test]
        public void Compatibility_ClashesBindsAndCurdles()
        {
            var venom = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(venom, "Grain Spirit", 2); Add(venom, "Bitterroot", 2); venom.SetHeat(0.18);
            venom.RunUntil(300);
            double before = Units(venom, AlchemyDefaults.Venom);
            Add(venom, "Holy Water");
            venom.RunUntil(340);
            Assert.Less(Units(venom, AlchemyDefaults.Venom), before - 1.0, "holy water destroys venom");

            var milk = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(milk, "Goat Milk"); Add(milk, "Vinegar");
            milk.RunUntil(60);
            Assert.Greater(Undissolved(milk, AlchemyDefaults.Casein), 0.5, "milk curdles in acid");
            Assert.Less(milk.Mixture.Ph, 5.0);

            var bark = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(bark, "Spring Water", 2); Add(bark, "Oak Gall", 2, "ground");
            bark.RunUntil(200);
            double tannin = Units(bark, AlchemyDefaults.Tannin);
            Add(bark, "Egg Yolk");
            bark.RunUntil(260);
            Assert.Less(Units(bark, AlchemyDefaults.Tannin), tannin - 1.0, "egg white binds tannin");
            Assert.Greater(Units(bark, AlchemyDefaults.Lees), 1.0);
        }

        [Test]
        public void Ph_DecidesWhatComesOut_WhatReacts_AndWhatSurvives()
        {
            AlchemyBrew Berries(string extra)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); if (extra != null) Add(b, extra); Add(b, "Dreamshade Berries", 3); b.SetHeat(0.14);
                b.RunUntil(120);
                return b;
            }
            Assert.Greater(Units(Berries("Vinegar"), AlchemyDefaults.Somnine), Units(Berries(null), AlchemyDefaults.Somnine) * 1.4, "acid draws the alkaloid out");
            Assert.Less(Units(Berries("Potash"), AlchemyDefaults.Somnine), 0.05, "alkali destroys it");

            AlchemyBrew Willow(bool acid)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); if (acid) Add(b, "Vinegar"); Add(b, "Willow Bark", 2, "ground"); b.SetHeat(0.25);
                b.RunUntil(600);
                return b;
            }
            Assert.AreEqual("willowbark_draught", Outcome(Willow(true)));
            Assert.Less(Units(Willow(false), AlchemyDefaults.WillowEssence), 0.01, "without acid the bark stays bitter");
        }

        [Test]
        public void Contamination_SlowsReactions_AndADirtyVesselCarriesItOver()
        {
            double Vital(string water)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, water, 2); Add(b, "Sunpetal", 3); b.SetHeat(0.24); b.SetStirring(0.3);
                b.RunUntil(60);
                return Units(b, AlchemyDefaults.VitalEssence);
            }
            Assert.Less(Vital("Muddy Water"), Vital("Spring Water") * 0.97, "stirred-up silt gets in the way");

            var spoiled = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(spoiled, "Spring Water", 2); Add(spoiled, "Sunpetal", 3); spoiled.SetHeat(0.35);
            spoiled.RunUntil(900);
            AlchemyHarvest h = spoiled.Empty();
            Assert.IsNotEmpty(h.Leftovers, "residue clings to the vessel");
        }

        [Test]
        public void Grinding_LevelsAndEvenness_ChangeWhatComesOut()
        {
            double Latex(string form, float evenness = 1f)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water", 2); Add(b, "Sunpetal", 3, form, evenness); b.SetHeat(0.24);
                b.RunUntil(30);
                return Units(b, AlchemyDefaults.BitterLatex);
            }
            double whole = Latex(null), coarse = Latex("coarse"), fine = Latex("ground");
            Assert.Less(whole, coarse);
            Assert.Less(coarse * 3, fine, "the finer, the more of the locked-in latex");
            Assert.Greater(Latex("coarse", 0.2f), coarse * 1.5, "an uneven grind has fine dust in it");
            var oil = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(oil, "Spring Water", 2); Add(oil, "Sunpetal", 3, "powder");
            oil.RunUntil(60);
            int petal = oil.Rules.IndexOf(AlchemyDefaults.PetalOil);
            Assert.Less(oil.Mixture.Amount(petal) + oil.Mixture.UndissolvedAmount(petal), 1.3, "pulverising drives off part of the fragrant oil");
        }

        [Test]
        public void BlastingPowder_MixedDry_GoesOffWhenHot()
        {
            var dry = new AlchemyBrew(Rules(), Vessel("Evaporating Dish"), 20, 1);
            Add(dry, "Saltpetre Crystals"); Add(dry, "Fire Ash", 2); Add(dry, "Charcoal"); dry.SetStirring(0.5);
            dry.RunUntil(60);
            Assert.Greater(Units(dry, AlchemyDefaults.BlastingPowder), 2.0);
            var hot = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
            Add(hot, "Lamp Oil", 2); Add(hot, "Blasting Powder"); hot.SetHeat(1);
            bool boom = false;
            hot.EventRaised += e => boom |= e.Kind == AlchemyEventKind.Eruption;
            hot.RunUntil(300);
            Assert.IsTrue(boom);
        }

        [Test]
        public void Soap_NeedsStirring_ThenHoldsItselfTogether()
        {
            AlchemyBrew Run(double speed)
            {
                var b = new AlchemyBrew(Rules(), Cauldron(), 20, 1);
                Add(b, "Spring Water"); Add(b, "Potash"); Add(b, "Lamp Oil", 2); b.SetHeat(0.35); b.SetStirring(speed);
                b.RunUntil(300);
                return b;
            }
            Assert.Less(Units(Run(0), AlchemyDefaults.Soap), 0.5, "lye and oil lie apart");
            AlchemyBrew stirred = Run(0.5);
            Assert.Greater(Units(stirred, AlchemyDefaults.Soap), 5.0);
            Assert.AreEqual("soft_soap", Outcome(stirred));
        }

        [Test]
        public void NewOperations_SaveAndReplayExactly()
        {
            AlchemyRules rules = Rules();
            var b = new AlchemyBrew(rules, Cauldron(), 20, 7);
            Add(b, "Muddy Water", 2); Add(b, "Sunpetal", 3, "coarse", 0.4f);
            b.AddFuel("firewood", 2, out _);
            b.RunUntil(30); b.Bellows(1); b.RunUntil(32); b.Bellows(1);
            b.RunUntil(40); b.SetStirring(0.35); b.RunUntil(55); b.SetStirring(0.6); b.RunUntil(70); b.SetStirring(0);
            b.RunUntil(90); b.SetDamper(0.5); b.SetBath(AlchemyBath.Water);
            b.RunUntil(120); b.Filter("paper", out _);
            b.RunUntil(150);
            AlchemyBrew restored = AlchemyBrew.Restore(rules, AlchemyBrewState.FromJson(b.Capture().ToJson()) ?? b.Capture());
            AlchemyBrew replayed = AlchemyBrew.Replay(rules, Cauldron(), 20, 7, b.History, b.Mixture.Step);
            b.RunUntil(400); restored.RunUntil(400); replayed.RunUntil(400);
            for (int i = 0; i < rules.SubstanceCount; i++)
            {
                Assert.AreEqual(b.Mixture.Amount(i), restored.Mixture.Amount(i), 1e-9);
                Assert.AreEqual(b.Mixture.Amount(i), replayed.Mixture.Amount(i), 1e-9);
            }
            Assert.AreEqual(b.Mixture.FireLevel, restored.Mixture.FireLevel, 1e-9);
            Assert.AreEqual(b.Mixture.Temperature, replayed.Mixture.Temperature, 1e-9);
            StringAssert.Contains("Stirred at about", string.Join("|", AlchemyJournal.Procedure(b)));
            StringAssert.Contains("Worked the bellows", string.Join("|", AlchemyJournal.Procedure(b)));
        }
    }
}
#endif
