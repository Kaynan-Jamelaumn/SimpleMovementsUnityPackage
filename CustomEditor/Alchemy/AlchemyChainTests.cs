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
    }
}
#endif
