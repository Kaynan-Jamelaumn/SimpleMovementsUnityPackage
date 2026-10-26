#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace AlchemySystem.Tests
{
    /// <summary>Reaction chains and alternative pathways of the built-in rules.</summary>
    public class AlchemyChainTests
    {
        private static AlchemyRules Rules() => AlchemyDefaults.Rules();

        private static void Add(AlchemyBrew b, string ingredient, int count = 1, string form = null)
        {
            Assert.IsTrue(b.AddIngredient(AlchemyDefaults.Spec(ingredient), count, out string why, form), why);
        }

        [Test]
        public void Substances_HaveSeveralProducers()
        {
            AlchemyRules rules = Rules();
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.Venom).Count, 2, "resin and latex");
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.VitalEssence).Count, 2, "sap and sugars + fungi");
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.SereneEssence).Count, 2, "frost oil and lavender");
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.ArdentEssence).Count, 2);
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.SalamanderEssence).Count, 2);
        }

        [Test]
        public void Pathways_FollowChainsAcrossSeveralReactions()
        {
            AlchemyRules rules = Rules();
            List<List<int>> routes = AlchemyChains.PathwaysTo(rules, AlchemyDefaults.SalamanderEssence);
            Assert.IsTrue(routes.Exists(r => r.Count >= 3), "sap → vital → ardent → salamander");
            Assert.IsTrue(routes.Exists(r => r.Count == 1), "the direct route is listed too");
            StringAssert.Contains("→", AlchemyChains.DescribeRoute(rules, routes[0]));
            Assert.IsEmpty(rules.CompileErrors);
            Assert.IsEmpty(rules.CompileWarnings, string.Join("\n", rules.CompileWarnings));
        }

        [Test]
        public void Venom_CanBeMadeFromLatexInsteadOfResin()
        {
            AlchemyRules rules = Rules();
            var b = new AlchemyBrew(rules, AlchemyEquipmentProfile.DefaultCauldron(), 20, 1);
            Add(b, "Grain Spirit", 2); Add(b, "Sunpetal", 3, "ground"); b.SetHeat(0.18);
            b.RunUntil(120);
            Assert.Greater(b.TotalOf(rules.ReactionIndex("latex_drawing")), 0.05, "no bitterroot involved");
        }

        [Test]
        public void SereneEssence_ChainsFromMoonDewThroughMana()
        {
            AlchemyRules rules = Rules();
            var b = new AlchemyBrew(rules, AlchemyEquipmentProfile.DefaultCauldron(), 20, 1);
            Add(b, "Grain Spirit", 2); Add(b, "Moonbloom", 2); Add(b, "Lavender", 2, "ground"); b.SetHeat(0.15);
            b.RunUntil(240);
            Assert.Greater(b.TotalOf(rules.ReactionIndex("moon_infusion")), 0.1, "step 1");
            Assert.Greater(b.TotalOf(rules.ReactionIndex("lavender_calming")), 0.02, "step 2 consumes step 1's product");
        }

        [Test]
        public void Journal_ListsOnlyRoutesTheseReactionsUnderstood()
        {
            AlchemyRules rules = Rules();
            var d = new AlchemyJournalData();
            AlchemySubstance venom = rules.Substance(AlchemyDefaults.Venom);
            Assert.IsEmpty(AlchemyChains.KnownRoutes(d, rules, venom));
            d.reactions.Add(new AlchemyReactionNote { reactionId = "venom_drawing", timesSeen = 2 });
            d.reactions.Add(new AlchemyReactionNote { reactionId = "latex_drawing", timesSeen = 2 });
            List<string> lines = AlchemyChains.KnownRoutes(d, rules, venom);
            Assert.IsTrue(lines.Exists(l => l.Contains("2 known routes")));
        }

        // ------------------------------------------------------------------ the herbal pack: several recipes per potion
        private static string Brew(double heat, double seconds, double stirring, params (string ingredient, int count, string form)[] adds)
        {
            AlchemyRules rules = Rules();
            var b = new AlchemyBrew(rules, AlchemyEquipmentProfile.DefaultCauldron(), 20, 1);
            foreach (var a in adds) Add(b, a.ingredient, a.count, a.form);
            b.SetHeat(heat);
            if (stirring > 0) b.SetStirring(stirring);
            b.RunUntil(seconds);
            return b.Evaluate().OutcomeId;
        }

        [Test]
        public void HerbalPack_EveryPotionHasSeveralRecipes()
        {
            AlchemyRules rules = Rules();
            Assert.IsEmpty(rules.CompileErrors, string.Join("\n", rules.CompileErrors));
            foreach (string s in new[]
            {
                AlchemyDefaults.StoneskinEssence, AlchemyDefaults.SwiftEssence, AlchemyDefaults.ClarityEssence, AlchemyDefaults.HearthwardEssence,
                AlchemyDefaults.TitanEssence, AlchemyDefaults.NourishingBroth, AlchemyDefaults.ManaEssence, AlchemyDefaults.Antitoxin,
                AlchemyDefaults.Somnine, AlchemyDefaults.FirewardEssence,
            })
                Assert.GreaterOrEqual(AlchemyChains.Producers(rules, s).Count, 2, s);
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.VitalEssence).Count, 4, "sunpetal, fungi, moonlit blood, nettles");
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.SereneEssence).Count, 3, "frost oil, lavender, chamomile");
            Assert.GreaterOrEqual(AlchemyChains.Producers(rules, AlchemyDefaults.Venom).Count, 3, "resin, latex, nightshade");
        }

        [Test]
        public void HerbalPack_SecondRecipesForOldPotions()
        {
            Assert.AreEqual("healing_draught", Brew(0.2, 240, 0, ("Spring Water", 2, null), ("Stinging Nettle", 3, "ground")), "nettles");
            Assert.AreEqual("vigor_draught", Brew(0.28, 240, 0, ("Spring Water", 2, null), ("Dragontongue", 3, "ground")), "dragontongue");
            Assert.AreEqual("salamander_draught", Brew(0.2, 240, 0.5, ("Grain Spirit", 2, null), ("Dragontongue", 2, "ground"), ("Fire Pepper", 2, "ground")), "no healing essence needed");
            Assert.AreEqual("mana_potion", Brew(0.15, 240, 0, ("Spring Water", 2, null), ("Starlight Lotus", 2, null)), "by day");
            Assert.AreEqual("antidote", Brew(0.2, 300, 0, ("Vinegar", 1, null), ("Spring Water", 2, null), ("Wormwood", 3, "ground")), "wormwood");
            Assert.AreEqual("sleep_draught", Brew(0.15, 300, 0, ("Spring Water", 2, null), ("Poppy Pod", 3, "ground")), "poppies");
            Assert.AreEqual("calming_tonic", Brew(0.22, 300, 0, ("Spring Water", 2, null), ("Chamomile", 3, "ground")), "chamomile");
            Assert.AreEqual("fireward_tonic", Brew(0, 300, 0, ("Spring Water", 2, null), ("Salamander Scale", 2, "ground")), "cold steeped");
            Assert.AreEqual("venom_extract", Brew(0.18, 240, 0, ("Grain Spirit", 2, null), ("Nightshade", 3, "ground")), "nightshade");
        }

        [Test]
        public void HerbalPack_NewPotions_TwoWaysEach()
        {
            Assert.AreEqual("stoneskin_elixir", Brew(0.2, 240, 0, ("Spring Water", 2, null), ("Troll Moss", 3, "ground"), ("Iron Filings", 1, null)));
            Assert.AreEqual("swiftness_draught", Brew(0.2, 240, 0, ("Spring Water", 2, null), ("Harefoot Clover", 3, "ground")), "clover, warm");
            Assert.AreEqual("swiftness_draught", Brew(0, 240, 0, ("Grain Spirit", 2, null), ("Windchime Seeds", 3, "ground")), "windchime, cool spirit");
            Assert.AreEqual("clarity_elixir", Brew(0.15, 240, 0, ("Grain Spirit", 2, null), ("Sage Leaves", 3, "ground")), "sage");
            Assert.AreEqual("clarity_elixir", Brew(0.15, 300, 0, ("Grain Spirit", 1, null), ("Spring Water", 1, null), ("Moonbloom", 1, null), ("Rose Petals", 4, "ground")), "moon dew -> mana -> clarity");
            Assert.AreEqual("hearthward_tonic", Brew(0.35, 240, 0.5, ("Lamp Oil", 2, null), ("Bear Tallow", 2, null), ("Fire Pepper", 2, "ground")), "tallow and pepper");
            Assert.AreEqual("hearthward_tonic", Brew(0.45, 300, 0, ("Lamp Oil", 2, null), ("Emberroot", 3, "ground")), "emberroot in hot oil");
            Assert.AreEqual("titan_draught", Brew(0.15, 240, 0, ("Grain Spirit", 2, null), ("Giantcap", 3, "ground")));
            Assert.AreEqual("travellers_broth", Brew(0.4, 300, 0, ("Spring Water", 2, null), ("Wild Barley", 3, "ground")), "barley");
            Assert.AreEqual("travellers_broth", Brew(0.25, 300, 0.3, ("Goat Milk", 2, null), ("Honey", 1, null)), "milk and honey");
        }
    }
}
#endif
