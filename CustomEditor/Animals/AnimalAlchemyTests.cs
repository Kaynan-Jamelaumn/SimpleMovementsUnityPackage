#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AnimalHusbandrySystem.Tests
{
    /// <summary>
    /// Edit Mode tests of animal medicine through Alchemy (<see cref="AnimalAlchemy"/>): the veterinary pack installs once
    /// and never changes a database asset (a wrapper takes its place), its outcomes compile and only name liquids the
    /// existing ingredients already make (no new substance or reaction), each is reached by its documented recipe and by a
    /// liquid made by hand - and only by what it should be - the documented existing recipes keep their outcome, tier and
    /// quality with the pack, and what a bottle or an ingredient is to the animal rules (its key, each copy's quality, the
    /// items made from code).
    /// </summary>
    public class AnimalAlchemyTests
    {
        private readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            AlchemyDatabase.Override(null); // find the project's database again
            foreach (UnityEngine.Object o in made)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            made.Clear();
        }

        // ---------------------------------------------------------------- helpers
        /// <summary>A database made in memory, like the built-in one: it takes the pack itself.</summary>
        private AlchemyDatabase BuiltIn()
        {
            var db = ScriptableObject.CreateInstance<AlchemyDatabase>();
            db.name = "Test Alchemy (built-in)";
            db.hideFlags = HideFlags.DontSave;
            AlchemyDefaults.Fill(db);
            made.Add(db);
            return db;
        }

        /// <summary>A database standing for a project's asset (not made in memory): it must never be changed.</summary>
        private AlchemyDatabase Asset()
        {
            var db = ScriptableObject.CreateInstance<AlchemyDatabase>();
            db.name = "Test Alchemy (asset)";
            AlchemyDefaults.Fill(db);
            made.Add(db);
            return db;
        }

        /// <summary>The built-in rules with the pack, installed as the game does it.</summary>
        private AlchemyRules WithPack()
        {
            AlchemyDatabase db = BuiltIn();
            Assert.AreSame(db, AnimalAlchemy.Install(db, false));
            Remember(db);
            return db.Rules;
        }

        /// <summary>Destroys the packs and wrappers a test made, with the rest.</summary>
        private void Remember(AlchemyDatabase db)
        {
            if (db == null) return;
            if (!made.Contains(db)) made.Add(db);
            foreach (AlchemyDatabase e in db.extensions)
                if (e != null && (e.hideFlags & HideFlags.DontSave) != 0 && !made.Contains(e)) made.Add(e);
        }

        private static void Add(AlchemyBrew b, string ingredient, int count = 1, string form = null)
        {
            Assert.IsTrue(b.AddIngredient(AlchemyDefaults.Spec(ingredient), count, out string why, form), why);
        }

        /// <summary>A preparation in the iron cauldron at 20 °C, run to <paramref name="seconds"/>.</summary>
        private static AlchemyBrew Brew(AlchemyRules rules, Action<AlchemyBrew> make, double seconds)
        {
            var b = new AlchemyBrew(rules, AlchemyEquipmentProfile.DefaultCauldron(), 20, 1);
            make(b);
            b.RunUntil(seconds);
            return b;
        }

        /// <summary>What the preparation would be bottled as now.</summary>
        private static string OutcomeOf(AlchemyBrew b) => b.Evaluate().OutcomeId;

        /// <summary>What a liquid of exactly these substances (units) is.</summary>
        private static AlchemyResult What(AlchemyRules rules, params (string id, double units)[] parts)
        {
            var a = new double[rules.SubstanceCount];
            foreach ((string id, double units) in parts)
            {
                int i = rules.IndexOf(id);
                Assert.GreaterOrEqual(i, 0, id);
                a[i] += units;
            }
            return AlchemyEvaluator.Evaluate(rules, a, null);
        }

        private const string Water = AlchemyDefaults.Water, Spirit = AlchemyDefaults.Spirit, Absinthin = AlchemyDefaults.Absinthin,
            Tannin = AlchemyDefaults.Tannin, Brine = AlchemyDefaults.Brine, Sugars = AlchemyDefaults.Sugars;

        // ---------------------------------------------------------------- installing
        [Test]
        public void Pack_InstallsOnce_AndAddsOnlyTheVetOutcomes()
        {
            AlchemyDatabase db = BuiltIn();
            AlchemyRules before = db.Rules;
            int substances = before.SubstanceCount, reactions = before.Reactions.Length, outcomes = before.Outcomes.Length;
            Assert.AreSame(db, AnimalAlchemy.Install(db, false), "the built-in database takes the pack itself");
            Assert.AreSame(db, AnimalAlchemy.Install(db, false));
            AlchemyDatabase.Override(db);
            AnimalAlchemy.EnsureInstalled();
            AnimalAlchemy.EnsureInstalled();
            Assert.AreSame(db, AlchemyDatabase.Instance, "extended, not replaced");
            Remember(db);
            int packs = 0;
            foreach (AlchemyDatabase e in db.extensions)
                if (e != null && e.name == AnimalAlchemy.PackName) packs++;
            Assert.AreEqual(1, packs, "installed once");

            AlchemyRules rules = db.Rules;
            Assert.IsEmpty(rules.CompileErrors, string.Join("\n", rules.CompileErrors));
            Assert.IsEmpty(rules.CompileWarnings, string.Join("\n", rules.CompileWarnings));
            Assert.AreEqual(substances, rules.SubstanceCount, "no new substance");
            Assert.AreEqual(reactions, rules.Reactions.Length, "no new reaction");
            Assert.AreEqual(outcomes + 3, rules.Outcomes.Length);
            foreach (string id in AnimalAlchemy.OutcomeIds)
            {
                AlchemyOutcome o = rules.Outcome(id);
                Assert.IsNotNull(o, id);
                Assert.That(o.priority, Is.InRange(41, 49), id + ": after every potion and material, before the failures");
                Assert.AreEqual(AlchemyOutcomeKind.Potion, o.kind, id);
                Assert.IsTrue(o.HasPotency, id);
                Assert.AreEqual(4, o.tiers.Count, id + ": Weak, Standard, Potent, Superior");
                Assert.IsNotEmpty(o.effects, id);
                Assert.Less(o.effects[0].amount, 0f, id + ": drinking it does a person a little harm");
                var errors = new List<string>();
                var warnings = new List<string>();
                o.Validate(rules, errors, warnings);
                Assert.IsEmpty(errors, string.Join("\n", errors));
                Assert.IsEmpty(warnings, string.Join("\n", warnings));
            }
            var all = new List<string>();
            var allWarnings = new List<string>();
            db.Validate(all, allWarnings);
            Assert.IsEmpty(all, string.Join("\n", all));
        }

        [Test]
        public void Pack_NeverChangesAnAsset_AWrapperTakesItsPlace()
        {
            AlchemyDatabase asset = Asset();
            // The project's own vermifuge wins over the pack's.
            asset.outcomes.Add(new AlchemyOutcome
            {
                id = AnimalAlchemy.VermifugeId, displayName = "House Vermifuge", priority = 44, potency = SubstanceSelector.Of(Absinthin),
            }.Requires(new SubstanceCondition(SubstanceSelector.Of(Absinthin), 0.05f)).Tier("", 0f, 1f));
            asset.MarkChanged();
            int extensions = asset.extensions.Count, ownOutcomes = asset.outcomes.Count;

            Assert.AreSame(asset, AnimalAlchemy.Install(asset, false), "outside play mode the asset is left as it is");
            Assert.IsNull(asset.Rules.Outcome(AnimalAlchemy.AntisepticId));

            AlchemyDatabase wrapper = AnimalAlchemy.Install(asset, true);
            Remember(wrapper);
            Assert.AreNotSame(asset, wrapper);
            Assert.AreEqual(extensions, asset.extensions.Count, "the asset itself is never changed");
            Assert.AreEqual(ownOutcomes, asset.outcomes.Count);
            Assert.IsTrue((wrapper.hideFlags & HideFlags.DontSave) != 0, "the wrapper is made in memory");
            Assert.AreEqual(0, wrapper.forms.Count, "no forms of its own: they would replace the asset's");
            Assert.AreSame(asset.settings, wrapper.settings, "the asset's settings rule");
            AlchemyRules rules = wrapper.Rules;
            Assert.IsEmpty(rules.CompileErrors, string.Join("\n", rules.CompileErrors));
            Assert.IsNotNull(rules.Outcome("antidote"), "the asset's content is all there");
            foreach (string id in AnimalAlchemy.OutcomeIds)
                Assert.IsNotNull(rules.Outcome(id), id);
            Assert.AreEqual("House Vermifuge", rules.Outcome(AnimalAlchemy.VermifugeId).displayName, "the project's own outcome is kept");
            Assert.AreSame(wrapper, AnimalAlchemy.Install(wrapper, true), "installed: nothing more to do");
            Assert.AreSame(wrapper, AnimalAlchemy.Install(asset, true), "the same asset keeps the same wrapper");
        }

        [Test]
        public void OutcomeExists_CountsTheVetOutcomes_EvenOnAnAssetLeftAsItIs()
        {
            Assert.IsFalse(Application.isPlaying);
            AlchemyDatabase asset = Asset();
            AlchemyDatabase.Override(asset);
            Assert.IsTrue(AnimalAlchemy.OutcomeExists(AnimalAlchemy.VermifugeId), "there as soon as the game runs");
            Assert.IsTrue(AnimalAlchemy.OutcomeExists(AnimalAlchemy.ElectrolytesId));
            Assert.IsTrue(AnimalAlchemy.OutcomeExists("antidote"));
            Assert.IsFalse(AnimalAlchemy.OutcomeExists("no_such_outcome"));
            Assert.IsFalse(AnimalAlchemy.OutcomeExists(""));
            Assert.AreSame(asset, AlchemyDatabase.Instance, "outside play mode the asset stays the database its tools work on");
            Assert.AreEqual(0, asset.extensions.Count);
        }

        [Test]
        public void VetOutcomes_AreOnlyWhatTheRulesCanMake()
        {
            List<AlchemySubstance> substances = AlchemyDefaults.Substances();
            substances.RemoveAll(s => s.id == Absinthin);
            List<AlchemyReaction> reactions = AlchemyDefaults.Reactions();
            reactions.RemoveAll(r => r.id == "wormwood_tempering");
            AlchemyRules noWormwood = AlchemyRules.Compile(new AlchemySimulationSettings(), AlchemyDefaults.Properties(), substances, reactions,
                AlchemyDefaults.Outcomes());
            List<AlchemyOutcome> list = AnimalAlchemy.VetOutcomes(noWormwood);
            Assert.AreEqual(2, list.Count);
            Assert.IsFalse(list.Exists(o => o.id == AnimalAlchemy.VermifugeId), "no absinthin: no vermifuge (the plain wormwood remedy covers worms)");
            Assert.AreEqual(3, AnimalAlchemy.VetOutcomes(AlchemyDefaults.Rules()).Count);
        }

        // ---------------------------------------------------------------- what a liquid is
        [Test]
        public void LiquidsMadeByHand_AreEachVetMedicine_AndNothingElseIs()
        {
            AlchemyRules rules = WithPack();
            // Wormwood steeped in plain water.
            AlchemyResult worm = What(rules, (Water, 20), (Absinthin, 3), (Tannin, 1.2));
            Assert.AreEqual(AnimalAlchemy.VermifugeId, worm.OutcomeId);
            Assert.AreEqual(2, worm.Tier, "Potent");
            Assert.AreNotEqual(AnimalAlchemy.VermifugeId, What(rules, (Water, 20), (Absinthin, 3), (Tannin, 1.2), (AlchemyDefaults.AceticAcid, 1)).OutcomeId,
                "in acid it is not a vermifuge (in a vessel the acid tempers it into antitoxin)");
            Assert.AreEqual("antidote", What(rules, (Water, 20), (Absinthin, 0.5), (AlchemyDefaults.Antitoxin, 2)).OutcomeId);
            Assert.AreNotEqual(AnimalAlchemy.VermifugeId, What(rules, (Water, 20), (Absinthin, 0.2)).OutcomeId, "too little of it");
            Assert.AreNotEqual(AnimalAlchemy.VermifugeId, What(rules, (Water, 20), (Absinthin, 1), (AlchemyDefaults.FungalPulp, 3)).OutcomeId,
                "a little wormwood in a mushroom broth is no vermifuge");

            // Tannin in strong spirit.
            AlchemyResult wash = What(rules, (Spirit, 12), (Tannin, 4));
            Assert.AreEqual(AnimalAlchemy.AntisepticId, wash.OutcomeId);
            Assert.AreNotEqual(AnimalAlchemy.AntisepticId, What(rules, (Water, 12), (Tannin, 4)).OutcomeId, "in water");
            Assert.AreNotEqual(AnimalAlchemy.AntisepticId, What(rules, (Water, 20), (Spirit, 6), (Tannin, 4)).OutcomeId, "in weak spirit");
            Assert.AreNotEqual(AnimalAlchemy.AntisepticId, What(rules, (Spirit, 12), (Tannin, 4), (AlchemyDefaults.Lye, 1)).OutcomeId, "in lye");

            // Salt and honey in clean water.
            AlchemyResult drench = What(rules, (Water, 31), (Brine, 3), (Sugars, 3));
            Assert.AreEqual(AnimalAlchemy.ElectrolytesId, drench.OutcomeId);
            Assert.AreNotEqual(AnimalAlchemy.ElectrolytesId, What(rules, (Water, 20), (Brine, 6), (Sugars, 3)).OutcomeId, "too salty");
            Assert.AreNotEqual(AnimalAlchemy.ElectrolytesId, What(rules, (Water, 20), (Brine, 3), (Sugars, 7)).OutcomeId, "too sweet");
            Assert.AreNotEqual(AnimalAlchemy.ElectrolytesId, What(rules, (Water, 30), (Brine, 3)).OutcomeId, "no honey");
            Assert.AreNotEqual(AnimalAlchemy.ElectrolytesId, What(rules, (Water, 31), (Brine, 3), (Sugars, 3), (AlchemyDefaults.Silt, 1)).OutcomeId, "muddy");
            Assert.AreNotEqual(AnimalAlchemy.ElectrolytesId, What(rules, (Water, 31), (Brine, 3), (Sugars, 3), (AlchemyDefaults.BitterResin, 2.9)).OutcomeId,
                "poisonous");
            Assert.AreNotEqual(AnimalAlchemy.ElectrolytesId, What(rules, (Water, 15), (Spirit, 16), (Brine, 3), (Sugars, 3)).OutcomeId, "in spirit");

            // A liquid with an active compound in it stays the potion it is (or is becoming).
            Assert.AreEqual("healing_draught", What(rules, (Water, 20), (Brine, 2), (Sugars, 2), (AlchemyDefaults.VitalEssence, 3)).OutcomeId);
            // A spoiled liquid stays spoiled.
            Assert.AreEqual("spoiled_mixture", What(rules, (Water, 20), (Absinthin, 1), (AlchemyDefaults.Sludge, 1.5)).OutcomeId);
        }

        [Test]
        public void DocumentedRecipes_MakeTheVetMedicines()
        {
            AlchemyRules rules = WithPack();
            // Wormwood steeped in warm water (fire 20 %, about 64 °C).
            AlchemyBrew worm = Brew(rules, b => { Add(b, "Spring Water", 2); Add(b, "Wormwood", 3, "ground"); b.SetHeat(0.2); }, 120);
            AlchemyResult r = worm.Evaluate();
            Assert.AreEqual(AnimalAlchemy.VermifugeId, r.OutcomeId);
            Assert.AreEqual(2, r.Tier, "Potent");
            Assert.IsNotNull(worm.Filter("paper", out string why), why);
            Assert.Greater(worm.Evaluate().Quality, r.Quality + 0.05, "filtered: the drifting herb no longer clouds it");
            Assert.AreEqual(AnimalAlchemy.VermifugeId, OutcomeOf(Brew(rules, b => { Add(b, "Spring Water", 3); Add(b, "Wormwood", 2, "ground"); b.SetHeat(0.2); }, 120)),
                "weaker, still a vermifuge");
            Assert.AreEqual("antidote", OutcomeOf(Brew(rules, b => { Add(b, "Vinegar"); Add(b, "Spring Water", 2); Add(b, "Wormwood", 3, "ground"); b.SetHeat(0.2); }, 300)),
                "steeped in vinegar the same wormwood is still an antidote");

            // A pinch of salt and a spoon of honey in clean water.
            AlchemyResult e = Brew(rules, b => { Add(b, "Spring Water", 3); Add(b, "Honey"); Add(b, "Rock Salt"); b.SetStirring(0.3); }, 60).Evaluate();
            Assert.AreEqual(AnimalAlchemy.ElectrolytesId, e.OutcomeId);
            Assert.AreEqual(2, e.Tier, "Potent");
            Assert.AreEqual(AnimalAlchemy.ElectrolytesId, OutcomeOf(Brew(rules, b => { Add(b, "Seawater", 2); Add(b, "Honey"); }, 60)), "seawater is salt enough");

            // Oak gall (or willow bark) steeped in strong spirit.
            AlchemyResult a = Brew(rules, b => { Add(b, "Grain Spirit", 2); Add(b, "Oak Gall", 2, "ground"); }, 60).Evaluate();
            Assert.AreEqual(AnimalAlchemy.AntisepticId, a.OutcomeId);
            Assert.AreEqual(2, a.Tier, "Potent");
            Assert.AreEqual(AnimalAlchemy.AntisepticId, OutcomeOf(Brew(rules, b => { Add(b, "Grain Spirit", 2); Add(b, "Willow Bark", 3, "ground"); }, 300)));
            Assert.AreNotEqual(AnimalAlchemy.AntisepticId,
                OutcomeOf(Brew(rules, b => { Add(b, "Grain Spirit"); Add(b, "Spring Water", 2); Add(b, "Oak Gall", 2, "ground"); }, 60)), "the spirit is too weak");
        }

        [Test]
        public void ExistingRecipes_KeepTheirOutcomeAndTier_WithThePack()
        {
            AlchemyRules plain = AlchemyDefaults.Rules(), pack = WithPack();
            Same(plain, pack, "antidote", b => { Add(b, "Vinegar"); Add(b, "Spring Water", 2); Add(b, "Wormwood", 3, "ground"); b.SetHeat(0.2); }, 300);
            Same(plain, pack, "antidote", b => { Add(b, "Spring Water", 2); Add(b, "Vinegar"); Add(b, "Snakeweed Root", 3); b.SetHeat(0.2); }, 600);
            Same(plain, pack, "willowbark_draught", b => { Add(b, "Spring Water", 2); Add(b, "Vinegar"); Add(b, "Willow Bark", 2, "ground"); b.SetHeat(0.25); }, 600);
            Same(plain, pack, "healing_salve", b => { Add(b, "Lamp Oil", 2); Add(b, "Sunpetal", 3); Add(b, "Beeswax"); b.SetHeat(0.24); }, 300);
            Same(plain, pack, "soft_soap", b => { Add(b, "Spring Water"); Add(b, "Potash"); Add(b, "Lamp Oil", 2); b.SetHeat(0.35); b.SetStirring(0.5); }, 300);
            Same(plain, pack, "honey_mead", b => { Add(b, "Spring Water", 2); Add(b, "Honey", 2); Add(b, "Brewer's Yeast"); b.SetHeat(0.05); }, 600);
        }

        /// <summary>
        /// The same preparation with and without the pack: the documented outcome at the end, and the same outcome, tier and
        /// quality all the way there (the pack adds no reaction, so the mixtures are identical).
        /// </summary>
        private static void Same(AlchemyRules plain, AlchemyRules pack, string expected, Action<AlchemyBrew> make, double seconds)
        {
            var a = new AlchemyBrew(plain, AlchemyEquipmentProfile.DefaultCauldron(), 20, 1);
            var b = new AlchemyBrew(pack, AlchemyEquipmentProfile.DefaultCauldron(), 20, 1);
            make(a);
            make(b);
            foreach (double t in new[] { 10.0, 30.0, 60.0, 120.0, seconds * 0.5, seconds })
            {
                a.RunUntil(t);
                b.RunUntil(t);
                AlchemyResult ra = a.Evaluate(), rb = b.Evaluate();
                Assert.AreEqual(ra.OutcomeId, rb.OutcomeId, $"{expected} at {t:0} s");
                Assert.AreEqual(ra.Tier, rb.Tier, $"{expected} tier at {t:0} s");
                Assert.AreEqual(ra.Quality, rb.Quality, 1e-9, $"{expected} quality at {t:0} s");
            }
            Assert.AreEqual(expected, a.Evaluate().OutcomeId, "the documented recipe");
        }

        // ---------------------------------------------------------------- what an item is
        [Test]
        public void ABottle_IsItsOutcomeToTheAnimals_AndEachCopyKeepsItsQuality()
        {
            AlchemyDatabase db = BuiltIn();
            AnimalAlchemy.Install(db, false);
            Remember(db);
            AlchemyDatabase.Override(db);
            AlchemyRules rules = db.Rules;
            AlchemyOutcome vermifuge = rules.Outcome(AnimalAlchemy.VermifugeId);
            BrewedPotionSO potent = ScriptableObject.CreateInstance<BrewedPotionSO>();
            made.Add(potent);
            potent.Setup(vermifuge, 2, rules, Color.white);
            Assert.AreEqual("potion:vet_vermifuge", AnimalAlchemy.KeyOf(potent), "whatever its tier or colour");
            Assert.IsNull(AnimalAlchemy.KeyOf(null));

            var go = new GameObject("Vermifuge Stack");
            made.Add(go);
            InventoryItem stack = go.AddComponent<InventoryItem>();
            stack.itemScriptableObject = potent;
            stack.stackCurrent = 3;
            // The top copy is the last code; the third copy (a merchant's bottle) has none.
            stack.DurabilityList = new List<int> { BrewedPotionSO.Encode(0.5, 0.9), BrewedPotionSO.Encode(1.0, 1.0) };
            Assert.AreEqual(1f, AnimalAlchemy.QualityOfCopy(stack, 0), 1e-4, "the top copy");
            Assert.AreEqual((float)BrewedPotionSO.QualityOf(0.5, 0.9), AnimalAlchemy.QualityOfCopy(stack, 1), 1e-4);
            Assert.AreEqual(0.75f, AnimalAlchemy.QualityOfCopy(stack, 2), 1e-6, "no code: its tier's typical quality");

            Assert.AreEqual(0.2f, AnimalAlchemy.TypicalQuality(vermifuge, 0), 1e-6);
            Assert.AreEqual(0.5f, AnimalAlchemy.TypicalQuality(vermifuge, 1), 1e-6);
            Assert.AreEqual(0.95f, AnimalAlchemy.TypicalQuality(vermifuge, 3), 1e-6);
            foreach (double q in new[] { 0.1, 0.6, 0.8, 0.97 })
            {
                Assert.IsTrue(BrewedPotionSO.TryDecode(AnimalAlchemy.CodeFor(q), out double potency, out double purity));
                Assert.AreEqual(q, BrewedPotionSO.QualityOf(potency, purity), 0.002, "a bottle made from code is of the quality asked");
            }

            ItemSO standard = AnimalAlchemy.PotionItem(AnimalAlchemy.AntisepticId);
            Assert.IsInstanceOf<BrewedPotionSO>(standard);
            Assert.AreEqual(1, ((BrewedPotionSO)standard).Tier, "Standard by default");
            Assert.AreEqual("potion:vet_antiseptic", AnimalAlchemy.KeyOf(standard));
            Assert.AreEqual(3, ((BrewedPotionSO)AnimalAlchemy.PotionItem(AnimalAlchemy.AntisepticId, 9)).Tier, "clamped to the best tier");
            Assert.AreSame(standard, AnimalAlchemy.PotionItem(AnimalAlchemy.AntisepticId), "one item per tier: bottles stack");
            Assert.IsNull(AnimalAlchemy.PotionItem("no_such_outcome"));

            ItemSO charcoal = AnimalAlchemy.IngredientItem("charcoal");
            Assert.IsNotNull(charcoal);
            Assert.AreEqual("ingredient:charcoal", AnimalAlchemy.KeyOf(charcoal));
            Assert.AreEqual("ingredient:brewer's_yeast", AnimalAlchemy.KeyOf(AnimalAlchemy.IngredientItem("Brewer's Yeast")));
            Assert.AreEqual("ingredient:wormwood", AnimalAlchemy.KeyOf(AnimalAlchemy.IngredientItem("ingredient:wormwood")));
            Assert.IsNull(AnimalAlchemy.IngredientItem("no such herb"));
        }
    }
}
#endif
