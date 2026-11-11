#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Phase 9 balance pass: the built-in numbers keep the loop of the design spec (section 21) at a human pace, and the numbers of the
    /// spec's own examples (sections 5, 16.3 and 18) are what the defaults give. The tests do not pin every number: they pin the
    /// ranges inside which the game works, so a designer's change that breaks the pace fails here and in Tools ▸ SimpleMovements ▸
    /// Rune Magic ▸ Balance Report shows why.
    /// </summary>
    public class BalanceReportTests
    {
        private static RuneCatalog Catalog() => RuneDefaults.Catalog();

        private static float Value(IList<BalanceLine> lines, string section, string label)
        {
            BalanceLine l = BalanceReport.Find(lines, section, label);
            Assert.IsNotNull(l, $"{section}: {label}");
            return l.value;
        }

        [Test]
        public void TheReportCoversEverySectionAndPrintsAsText()
        {
            List<BalanceLine> lines = BalanceReport.Build(Catalog());
            foreach (string section in new[] { "Capacity", "Energy", "Drops", "Synthesis", "Casting", "Recovery", "Spelling", "Discovery" })
                Assert.IsNotNull(lines.Find(l => l.section == section), section);
            string text = BalanceReport.ToText(lines);
            StringAssert.Contains("CASTING", text);
            StringAssert.Contains("Soul Damage of a perfect synthesis", text);
            Assert.IsNull(BalanceReport.Find(lines, "Casting", "no such line"));
            foreach (BalanceLine l in lines)
            {
                Assert.IsFalse(float.IsNaN(l.value) || float.IsInfinity(l.value), l.label);
                Assert.IsFalse(string.IsNullOrEmpty(l.label));
            }
        }

        [Test]
        public void TheEnergyEconomyIsTheSpecs()
        {
            List<BalanceLine> lines = BalanceReport.Build(Catalog());
            // Spec section 5: rank 1..5 hold 1000, 5000, 25000, 125000, 625000; a normal kill is about 150.
            Assert.AreEqual(1000f, Value(lines, "Energy", "Maximum energy of a rank 1"));
            Assert.AreEqual(25000f, Value(lines, "Energy", "Maximum energy of a rank 3"));
            Assert.AreEqual(625000f, Value(lines, "Energy", "Maximum energy of a rank 5"));
            float normal = Value(lines, "Energy", "Energy of a normal kill (tier 0)");
            Assert.That(normal, Is.InRange(100f, 200f));
            Assert.Greater(Value(lines, "Energy", "Energy of a normal kill (tier 3)"), normal, "harder monsters give more");
            Assert.Greater(Value(lines, "Energy", "Energy of a boss kill"), normal * 3f, "a boss is worth several normal kills");
            // The first rune must charge in a handful of kills, not hundreds and not one.
            Assert.That(Value(lines, "Energy", "Normal kills to fill a rank 1"), Is.InRange(3f, 20f));
        }

        [Test]
        public void ARuneDropsEveryFewKillsAndABossAlwaysDrops()
        {
            List<BalanceLine> lines = BalanceReport.Build(Catalog());
            Assert.That(Value(lines, "Drops", "Normal kills per rune drop (tier 0)"), Is.InRange(3f, 25f));
            Assert.LessOrEqual(Value(lines, "Drops", "Normal kills per rune drop (tier 3)"), Value(lines, "Drops", "Normal kills per rune drop (tier 0)"), "deeper tiers drop at least as often");
            Assert.AreEqual(1f, Value(lines, "Drops", "Boss always drops"));
        }

        [Test]
        public void EverySynthesisCostsTheSoulSomethingAndABotchedOneCostsMore()
        {
            RuneCatalog c = Catalog();
            List<BalanceLine> lines = BalanceReport.Build(c);
            int measured = 0;
            foreach (OutcomeArchetype arch in c.Archetypes)
            {
                BalanceLine good = BalanceReport.Find(lines, "Synthesis", arch.Name + ": Soul Damage of a perfect");
                BalanceLine bad = BalanceReport.Find(lines, "Synthesis", arch.Name + ": Soul Damage of a botched");
                if (good == null) continue;
                measured++;
                // Spec 14: magic always costs the soul something, and nothing a Mage can do in one attempt should end the character.
                Assert.That(good.value, Is.InRange(1f, 40f), arch.Name + " perfect");
                if (bad != null) Assert.GreaterOrEqual(bad.value, good.value - 1e-3f, arch.Name + ": a botched attempt is never cheaper");
                if (bad != null) Assert.Less(bad.value, 60f, arch.Name + ": one botched attempt never wrecks the soul");
                float energy = Value(lines, "Synthesis", arch.Name + ": ideal energy in all");
                Assert.That(energy, Is.InRange(100f, 5000f), arch.Name + " energy");
            }
            Assert.GreaterOrEqual(measured, c.Archetypes.Count - 1, "(almost) every archetype can be measured");
            // The spec's worked example (section 18): its seven runes cost about ten normal kills of energy and a tenth of the soul.
            Assert.AreEqual(7f, Value(lines, "Synthesis", "Pyroclastic Resonance: runes"));
            Assert.That(Value(lines, "Synthesis", "Pyroclastic Resonance: Soul Damage of a perfect"), Is.InRange(5f, 20f));
            // The darker the archetype, the more it costs the soul.
            Assert.Greater(Value(lines, "Synthesis", "Void Collapse: Soul Damage of a perfect"), Value(lines, "Synthesis", "Eternal Bloom: Soul Damage of a perfect"));
        }

        [Test]
        public void TheSpecsWorkedExampleOfCastingHoldsWithTheDefaults()
        {
            // Spec 16.3: Soul Integrity 100. A powerful Rank 3 spell consumes 500 Rune Energy and causes 12 Soul Damage (Integrity 88); another
            // powerful spell causes more (73: Strained, longer casting time, reduced stability); continuing without recovery ends in no casting.
            RuneCatalog c = Catalog();
            var sp = new SpellRecord { spellId = "x", effectId = "offense.pierce", rank = 3, complexity = 0.5f, power = 169f, stability = 90f, anchors = new List<string>() };
            CompiledSpell spell = SpellCompiler.Compile(c, sp);
            CastPlan first = SpellCasting.Plan(c, spell, SoulRestrictions.None);
            Assert.That(first.energyCost, Is.InRange(400f, 600f), "about 500 Rune Energy");
            Assert.That(first.strainScaled, Is.InRange(11f, 13f), "about 12 Soul Damage");

            float max = c.Balance.maxSoulIntegrity;
            var soul = new SoulState { initialized = true, integrity = max };
            SoulRules.Normalize(c.Balance, soul, max);
            SoulRules.Damage(c.Balance, soul, max, first.strainScaled, c.Spells.strainKind);
            Assert.That(soul.integrity, Is.InRange(87f, 89f), "Integrity becomes about 88");
            Assert.AreEqual("stable", SoulRules.Condition(c.Balance, soul, max).id);

            CastPlan second = SpellCasting.Plan(c, spell, SoulRestrictions.None);
            SoulRules.Damage(c.Balance, soul, max, second.strainScaled, c.Spells.strainKind);
            Assert.That(soul.integrity, Is.InRange(70f, 79f), "after another powerful spell about 73 to 76");
            SoulCondition now = SoulRules.Condition(c.Balance, soul, max);
            Assert.AreEqual("strained", now.id, "now Strained");
            Assert.Greater(now.castTimeMultiplier, 1f, "longer casting time");
            Assert.Less(now.synthesisStability, 1f, "reduced stability");

            int casts = 2;
            while (SoulRules.Condition(c.Balance, soul, max).castingAllowed && casts < 100)
            {
                SoulRules.Damage(c.Balance, soul, max, SpellCasting.Plan(c, spell, SoulRestrictions.None).strainScaled, c.Spells.strainKind);
                casts++;
            }
            Assert.Less(casts, 20, "continuing without recovery eventually makes the player unable to cast");
            Assert.IsFalse(SoulRules.Condition(c.Balance, soul, max).castingAllowed);
        }

        [Test]
        public void ASpellOfEveryRankStrainsTheSoulMoreThanTheRankBelowAndCostsAboutTheSameNumberOfCasts()
        {
            RuneCatalog c = Catalog();
            float lastStrain = 0f;
            for (int rank = 1; rank <= 5; rank++)
            {
                var sp = new SpellRecord { spellId = "x", effectId = "offense.pierce", rank = rank, complexity = 0.5f, power = 100f, stability = 90f, anchors = new List<string>() };
                CastPlan plan = SpellCasting.Plan(c, SpellCompiler.Compile(c, sp), SoulRestrictions.None);
                Assert.Greater(plan.strainScaled, lastStrain, "rank " + rank + " strains more");
                lastStrain = plan.strainScaled;
                // From a full rune: neither a single cast (too dear) nor hundreds (energy would never matter).
                float castsPerRune = c.Balance.MaxEnergy(rank) / plan.energyCost;
                Assert.That(castsPerRune, Is.InRange(10f, 200f), "rank " + rank);
                // A beginner's spell must leave room to play before the soul is strained; a rank 5 one is a decision.
                float castsToStrained = c.Balance.maxSoulIntegrity * 0.2f / plan.strainScaled;
                if (rank == 1) Assert.GreaterOrEqual(castsToStrained, 4f);
                if (rank == 5) Assert.LessOrEqual(castsToStrained, 2f);
            }
        }

        [Test]
        public void RecoveryMakesMeditationWorthItButNotInstant()
        {
            List<BalanceLine> lines = BalanceReport.Build(Catalog());
            float meditation = Value(lines, "Recovery", "Integrity per minute meditating");
            float rest = Value(lines, "Recovery", "Integrity per minute resting");
            Assert.Greater(meditation, rest, "meditating is the faster way");
            Assert.Greater(rest, 0f, "the soul heals by itself, slowly");
            Assert.That(Value(lines, "Recovery", "Minutes of meditation"), Is.InRange(0.5f, 10f));
            Assert.That(Value(lines, "Recovery", "Minutes of rest"), Is.InRange(5f, 60f));
        }

        [Test]
        public void TheSpellingGetsLongerAndHarderWithTheRank()
        {
            List<BalanceLine> lines = BalanceReport.Build(Catalog());
            float l1 = Value(lines, "Spelling", "Letters (rank 1");
            float l3 = Value(lines, "Spelling", "Letters (rank 3");
            float l5 = Value(lines, "Spelling", "Letters (rank 5");
            Assert.That(l1, Is.InRange(2f, 6f));
            Assert.Greater(l3, l1);
            Assert.GreaterOrEqual(l5, l3);
            Assert.LessOrEqual(l5, 12f, "a sequence no human can type in the time of a fight is no gameplay");
            Assert.Greater(Value(lines, "Spelling", "Time limit (rank 5"), Value(lines, "Spelling", "Time limit (rank 1"));
        }

        [Test]
        public void EveryConceptCanBeLearnedAndTheHarderTiersAskMore()
        {
            RuneCatalog c = Catalog();
            List<BalanceLine> lines = BalanceReport.Build(c);
            Assert.AreEqual(0f, Value(lines, "Discovery", "Concepts no source can teach"));
            for (int i = 1; i < c.Discovery.tiers.Count; i++)
            {
                DiscoveryTierDef easier = c.Discovery.tiers[i - 1], harder = c.Discovery.tiers[i];
                Assert.GreaterOrEqual(harder.minigameNodes, easier.minigameNodes, "band " + i + " asks at least as many glyphs");
                Assert.LessOrEqual(harder.attempts, easier.attempts, "band " + i + " forgives no more tries");
            }
        }
    }
}
#endif
