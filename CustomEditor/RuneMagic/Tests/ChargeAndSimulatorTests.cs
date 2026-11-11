#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Hold-to-charge (the generic charge profile, the way a held key becomes multipliers, and rune spells' energy, strain and instability) and the
    /// spell-build simulator, without the engine. The charger component and the charge registry's use of ability assets need the engine and
    /// are in <see cref="ChargeRuntimeTests"/>.
    /// </summary>
    public class ChargeAndSimulatorTests
    {
        private static RuneCatalog Catalog() => RuneDefaults.Catalog();

        // ================================================================== the charge profile
        [Test]
        public void NothingIsChargedBeforeTheMinimumAndAFullChargeNeedsTheMaximum()
        {
            var p = new ChargeProfile { minSeconds = 0.5f, maxSeconds = 2.5f, curve = 1f };
            Assert.AreEqual(0f, p.Fraction(0f));
            Assert.AreEqual(0f, p.Fraction(0.49f), "a tap is no charge");
            Assert.AreEqual(0f, p.Fraction(0.5f), "the minimum starts the charge");
            Assert.AreEqual(0.5f, p.Fraction(1.5f), 1e-5f);
            Assert.AreEqual(1f, p.Fraction(2.5f));
            Assert.AreEqual(1f, p.Fraction(30f), "holding longer adds nothing");
            Assert.AreEqual(0f, p.Fraction(float.NaN));
            Assert.AreEqual(0f, p.Fraction(-5f));
        }

        [Test]
        public void TheCurveShapesTheChargeAndSecondsForIsItsInverse()
        {
            var slow = new ChargeProfile { minSeconds = 0f, maxSeconds = 2f, curve = 2f };
            Assert.AreEqual(0.25f, slow.Fraction(1f), 1e-5f, "above 1: slow at first");
            var fast = new ChargeProfile { minSeconds = 0f, maxSeconds = 2f, curve = 0.5f };
            Assert.Greater(fast.Fraction(1f), 0.7f, "below 1: fast at first");
            foreach (ChargeProfile p in new[] { slow, fast, new ChargeProfile() })
                foreach (float f in new[] { 0.1f, 0.5f, 0.9f, 1f })
                    Assert.AreEqual(f, p.Fraction(p.SecondsFor(f)), 1e-3f, "curve " + p.curve);
        }

        [Test]
        public void AChargeScalesEachNumberFromOneToWhatAFullChargeReaches()
        {
            var p = new ChargeProfile { damageAtFull = 3f, healAtFull = 2f, controlAtFull = 1.5f, rangeAtFull = 1.4f, areaAtFull = 1.2f, durationAtFull = 2f, costAtFull = 1.8f };
            AbilityModifierSet none = p.ToModifiers(0f);
            Assert.AreEqual(1f, none.damageMultiplier);
            Assert.AreEqual(1f, none.costMultiplier);
            AbilityModifierSet half = p.ToModifiers(0.5f);
            Assert.AreEqual(2f, half.damageMultiplier, 1e-5f);
            Assert.AreEqual(1.5f, half.healMultiplier, 1e-5f);
            Assert.AreEqual(1.4f, half.costMultiplier, 1e-5f);
            AbilityModifierSet full = p.ToModifiers(1f);
            Assert.AreEqual(3f, full.damageMultiplier, 1e-5f);
            Assert.AreEqual(1.4f, full.rangeMultiplier, 1e-5f);
            Assert.AreEqual(1.2f, full.areaMultiplier, 1e-5f);
            Assert.AreEqual(2f, full.durationMultiplier, 1e-5f);
            Assert.AreEqual(1.8f, full.costMultiplier, 1e-5f);
            Assert.AreEqual(3f, p.ToModifiers(7f).damageMultiplier, "more than a full charge is a full charge");
            Assert.AreEqual(1f, p.ToModifiers(-1f).damageMultiplier);
            Assert.AreEqual(1f, ChargeProfile.Scale(0.5f, 0f));
            Assert.AreEqual(0.5f, ChargeProfile.Scale(0.5f, 1f), "a charge can also weaken (a cost that falls)");
        }

        [Test]
        public void ABrokenChargeProfileIsReportedAndADisabledOneIsNot()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            new ChargeProfile { minSeconds = 3f, maxSeconds = 2f }.Validate("X", errors, warnings);
            Assert.AreEqual(1, errors.Count);
            errors.Clear();
            new ChargeProfile { enabled = false, minSeconds = 3f, maxSeconds = 2f }.Validate("X", errors, warnings);
            Assert.IsEmpty(errors);
            new ChargeProfile { maxSeconds = 20f, damageAtFull = 0.5f }.Validate("X", errors, warnings);
            Assert.AreEqual(2, warnings.Count);
            Assert.IsEmpty(errors);
            var b = new ChargeProfile();
            Assert.AreNotSame(b, b.Clone());
            Assert.AreEqual(b.maxSeconds, b.Clone().maxSeconds);
        }

        [Test]
        public void ACastRequestIsNotChargedUnlessItSaysSo()
        {
            Assert.AreEqual(0f, default(CastRequest).charge);
            Assert.AreEqual(0f, CastRequest.Self.charge);
            Assert.AreEqual(0f, CastRequest.InDirection(Vector3.forward).charge);
            Assert.IsNotNull(typeof(PlayerAbilityController).GetField("PreCast"), "the hook the charger shares with the Spelling System is still there");
            Assert.IsNotNull(typeof(PlayerAbilityController).GetMethod("InputOfSlot"));
        }

        // ================================================================== rune spells
        [Test]
        public void ChargingASpellRaisesItsEnergyItsStrainAndItsChanceToMisfire()
        {
            RuneCatalog c = Catalog();
            var sp = new SpellRecord { spellId = "x", effectId = "offense.pierce", rank = 3, complexity = 0.5f, power = 169f, stability = 90f, anchors = new List<string>() };
            CompiledSpell cs = SpellCompiler.Compile(c, sp);
            CastPlan plain = SpellCasting.Plan(c, cs, SoulRestrictions.None);
            CastPlan none = plain;
            SpellCasting.ApplyCharge(ref none, c.Spells, 0f);
            Assert.AreEqual(plain.energyCost, none.energyCost, "no charge, no change");
            Assert.AreEqual(plain.strainScaled, none.strainScaled);

            CastPlan full = plain;
            SpellCasting.ApplyCharge(ref full, c.Spells, 1f);
            Assert.AreEqual(plain.energyCost * c.Spells.charge.costAtFull, full.energyCost, 0.01f);
            Assert.AreEqual(plain.strainScaled * c.Spells.chargeStrainAtFull, full.strainScaled, 0.01f);
            Assert.AreEqual(plain.failureScaled * c.Spells.chargeStrainAtFull, full.failureScaled, 0.01f);
            Assert.AreEqual(plain.instability + c.Spells.chargeInstabilityAtFull, full.instability, 1e-4f);

            CastPlan half = plain;
            SpellCasting.ApplyCharge(ref half, c.Spells, 0.5f);
            Assert.Greater(half.energyCost, plain.energyCost);
            Assert.Less(half.energyCost, full.energyCost, "a partial charge is in between");
            CastPlan over = plain;
            SpellCasting.ApplyCharge(ref over, c.Spells, 9f);
            Assert.AreEqual(full.energyCost, over.energyCost, 0.01f, "more than full is full");
            SpellCasting.ApplyCharge(ref over, null, 1f); // no balance: nothing to apply
        }

        [Test]
        public void TheAttacksAreChargeableAndTheRestAreNot()
        {
            RuneCatalog c = Catalog();
            var chargeable = c.SpellForms.Where(f => f.chargeable).Select(f => f.id).OrderBy(x => x).ToList();
            CollectionAssert.AreEqual(RuneDefaults.ChargeableForms.OrderBy(x => x).ToList(), chargeable);
            Assert.IsFalse(c.SpellForm("ritual").chargeable, "a ritual has nothing to hit with");
            Assert.IsFalse(c.SpellForm("ward").chargeable);
            Assert.IsTrue(c.Spells.charge.enabled);
            var errors = new List<string>();
            c.Spells.Validate(errors, new List<string>());
            Assert.IsEmpty(errors);
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
        }

        [Test]
        public void OlderDataGetsTheChargeableAttacksButADesignersChoiceIsKept()
        {
            RuneMagicData d = RuneDefaults.Create();
            Assert.AreEqual(9, RuneMagicData.CurrentVersion, "charging is data version 9");
            d.version = 8;
            foreach (SpellFormDef f in d.spellForms) f.chargeable = false;
            d.balance.spells.charge = null;
            Assert.IsTrue(d.Upgrade());
            Assert.IsNotNull(d.balance.spells.charge);
            Assert.IsTrue(d.spellForms.Find(f => f.id == "bolt").chargeable);
            Assert.IsFalse(d.spellForms.Find(f => f.id == "ritual").chargeable);

            RuneMagicData mine = RuneDefaults.Create();
            mine.version = 8;
            foreach (SpellFormDef f in mine.spellForms) f.chargeable = f.id == "wall";
            mine.Upgrade();
            Assert.IsTrue(mine.spellForms.Find(f => f.id == "wall").chargeable, "a form the designer made chargeable stays");
            Assert.IsFalse(mine.spellForms.Find(f => f.id == "bolt").chargeable, "and the designer's list is not overwritten");
        }

        [Test]
        public void TheTooltipTellsAChargeableSpellCanBeHeldAndTheChargeSurvivesTheAssetsJson()
        {
            RuneCatalog c = Catalog();
            var sp = new SpellRecord { spellId = "x", effectId = "offense.pierce", rank = 1, complexity = 0.5f, power = 100f, stability = 90f, anchors = new List<string>() };
            CompiledSpell cs = SpellCompiler.Compile(c, sp);
            CastPlan plan = SpellCasting.Plan(c, cs, SoulRestrictions.None);
            Assert.IsFalse(SpellCompiler.Describe(c, cs, plan).Any(l => l.Contains("Hold the key")));
            cs.chargeable = true;
            Assert.IsTrue(SpellCompiler.Describe(c, cs, plan).Any(l => l.Contains("Hold the key")));

            RuneMagicData d = RuneDefaults.Create();
            d.balance.spells.charge.maxSeconds = 4f;
            d.balance.spells.chargeStrainAtFull = 2.5f;
            RuneMagicData back = d.Clone();
            Assert.AreEqual(4f, back.balance.spells.charge.maxSeconds);
            Assert.AreEqual(2.5f, back.balance.spells.chargeStrainAtFull);
            Assert.IsTrue(back.spellForms.Find(f => f.id == "bolt").chargeable);
        }

        [Test]
        public void ASpellThatUsesTheSpellingSystemIsNotChargeable()
        {
            // The runtime sets Chargeable = form allows it AND no spelling; the rule is the same one the Spelling System's mode decides.
            RuneCatalog all = SynthLab.Custom(d => d.balance.spelling.mode = SpellingMode.All);
            var sp = new SpellRecord { spellId = "x", effectId = "offense.pierce", rank = 1, complexity = 0.5f, power = 100f, stability = 90f, anchors = new List<string>() };
            Assert.IsTrue(SpellingGenerator.Uses(all, sp));
            RuneCatalog none = SynthLab.Custom(d => d.balance.spelling.mode = SpellingMode.None);
            Assert.IsFalse(SpellingGenerator.Uses(none, sp));
        }

        // ================================================================== the simulator
        [Test]
        public void TheSimulatorBuildsTheSamplesAndTheSameSeedGivesTheSameReport()
        {
            RuneCatalog c = Catalog();
            SimulationReport a = SpellSimulator.Run(c, new SimulationOptions { samples = 500, seed = 5 });
            SimulationReport b = SpellSimulator.Run(c, new SimulationOptions { samples = 500, seed = 5 });
            SimulationReport other = SpellSimulator.Run(c, new SimulationOptions { samples = 500, seed = 6 });
            Assert.AreEqual(500, a.builds.Count);
            Assert.AreEqual(a.ToText(), b.ToText(), "reproducible");
            Assert.AreNotEqual(a.ToText(), other.ToText());
            Assert.AreEqual(0, SpellSimulator.Run(c, new SimulationOptions { samples = 0 }).builds.Count);
            StringAssert.Contains("SPELL SIMULATOR", a.ToText());
            StringAssert.Contains("OUTLIERS", a.ToText());
        }

        [Test]
        public void TheSimulatorSeesThatEnergyAndStrainGrowWithTheRankAndTheNumbersAreOrdered()
        {
            SimulationReport r = SpellSimulator.Run(Catalog(), new SimulationOptions { samples = 1500, seed = 2 });
            for (int i = 1; i < r.byRank.Count; i++)
            {
                Assert.Greater(r.byRank[i].energy, r.byRank[i - 1].energy, r.byRank[i].key + " costs more energy");
                Assert.Greater(r.byRank[i].strain, r.byRank[i - 1].strain, r.byRank[i].key + " strains more");
                Assert.Less(r.byRank[i].castsToStrained, r.byRank[i - 1].castsToStrained);
            }
            Assert.AreEqual(5, r.byRank.Count);
            foreach (MetricSummary m in r.metrics)
            {
                Assert.LessOrEqual(m.min, m.p5, m.name);
                Assert.LessOrEqual(m.p5, m.median, m.name);
                Assert.LessOrEqual(m.median, m.p95, m.name);
                Assert.LessOrEqual(m.p95, m.max, m.name);
            }
            Assert.IsNotNull(r.Metric("energy"));
            Assert.IsNull(r.Metric("nothing"));
        }

        [Test]
        public void TheBuiltInContentHasNoAttackThatStandsOutFromItsRank()
        {
            SimulationReport r = SpellSimulator.Run(Catalog(), new SimulationOptions { samples = 3000, seed = 7 });
            Assert.IsEmpty(r.outliers.Select(o => o.metric + ": " + o.spell.Describe()), "a build far outside its rank is a balance problem (run Tools ▸ Rune Magic ▸ Spell Simulator)");
            SimulationReport charged = SpellSimulator.Run(Catalog(), new SimulationOptions { samples = 3000, seed = 7, charge = 1f });
            Assert.IsEmpty(charged.outliers.Select(o => o.metric + ": " + o.spell.Describe()), "nor a fully charged one");
            // A full charge makes the chargeable attacks hit harder but holding the key is time: not twice the rate.
            float plainRate = r.builds.Where(s => s.chargeable && s.offensive).Average(s => s.rate);
            float chargedRate = charged.builds.Where(s => s.chargeable && s.offensive).Average(s => s.rate);
            Assert.Greater(chargedRate, plainRate * 0.8f);
            Assert.Less(chargedRate, plainRate * 1.8f, "the hold is counted as time");
        }

        [Test]
        public void TheSimulatorFindsABuildThatIsMuchCheaperThanItsRankAllows()
        {
            RuneCatalog broken = SynthLab.Custom(d => d.spellForms.Find(f => f.id == "bolt").energyMultiplier = 0.001f);
            SimulationReport r = SpellSimulator.Run(broken, new SimulationOptions { samples = 3000, seed = 7 });
            Assert.IsTrue(r.outliers.Any(o => o.high && o.metric == "punch per 1000 energy" && o.spell.formId == "bolt"), r.ToText());
            Assert.LessOrEqual(r.outliers.Count, new SimulationOptions().maxOutliers);
        }

        [Test]
        public void PercentilesAreInterpolatedAndASummaryOfNothingIsEmpty()
        {
            var v = new List<float> { 1f, 2f, 3f, 4f, 5f };
            Assert.AreEqual(3f, SpellSimulator.Percentile(v, 0.5f));
            Assert.AreEqual(1f, SpellSimulator.Percentile(v, 0f));
            Assert.AreEqual(5f, SpellSimulator.Percentile(v, 1f));
            Assert.AreEqual(1.4f, SpellSimulator.Percentile(v, 0.1f), 1e-5f);
            Assert.AreEqual(0f, SpellSimulator.Percentile(new List<float>(), 0.5f));
            MetricSummary m = SpellSimulator.Summarize("x", v);
            Assert.AreEqual(3f, m.mean, 1e-5f);
            Assert.AreEqual(Math.Sqrt(2.0), m.stdDev, 1e-4);
            Assert.AreEqual(0f, SpellSimulator.Summarize("x", new List<float>()).max);
        }
    }

    /// <summary>The charge registry and the charger with Unity's engine (ability assets are scriptable objects): Test Runner, category RequiresEngine.</summary>
    [Category("RequiresEngine")]
    public class ChargeRuntimeTests
    {
        private sealed class Source : IChargeSource
        {
            public ChargeProfile profile = new ChargeProfile();
            public bool affordable = true;
            public bool TryGetProfile(AbilityCaster caster, AbilitySlot slot, out ChargeProfile p) { p = profile; return profile != null; }
            public bool CanAfford(AbilityCaster caster, AbilitySlot slot, ChargeProfile p, float fraction) => affordable;
        }

        private readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            typeof(AbilityCharging).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            foreach (UnityEngine.Object o in made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            made.Clear();
        }

        private AbilitySlot SlotWithAbility()
        {
            var def = ScriptableObject.CreateInstance<AbilityDefinition>();
            made.Add(def);
            return new AbilitySlot { ability = def };
        }

        [Test]
        public void TheChargeAnAbilityGetsIsWhatWasAskedForWithinWhatItsSourcesAllow()
        {
            AbilitySlot slot = SlotWithAbility();
            Assert.AreEqual(0f, AbilityCharging.Resolve(null, slot, 0.7f, out ChargeProfile none), "no source: not chargeable");
            Assert.IsNull(none);
            var src = new Source();
            AbilityCharging.Register(src);
            AbilityCharging.Register(src); // asking again is harmless
            Assert.AreEqual(0.7f, AbilityCharging.Resolve(null, slot, 0.7f, out ChargeProfile p), 1e-5f);
            Assert.AreSame(src.profile, p);
            Assert.AreEqual(1f, AbilityCharging.Resolve(null, slot, 9f, out _), "clamped to a full charge");
            Assert.AreEqual(0f, AbilityCharging.Resolve(null, slot, 0f, out _));
            Assert.AreEqual(0f, AbilityCharging.Resolve(null, slot, -1f, out _));
            Assert.AreEqual(0f, AbilityCharging.Resolve(null, slot, float.NaN, out _));
            src.affordable = false;
            Assert.AreEqual(0f, AbilityCharging.Resolve(null, slot, 0.7f, out ChargeProfile refused), "a charge that cannot be paid is an ordinary cast");
            Assert.IsNull(refused);
            src.affordable = true;
            src.profile.enabled = false;
            Assert.AreEqual(0f, AbilityCharging.Resolve(null, slot, 0.7f, out _), "a disabled profile does not charge");
            AbilityCharging.Unregister(src);
            src.profile.enabled = true;
            Assert.IsNull(AbilityCharging.ProfileOf(null, slot));
            Assert.IsNull(AbilityCharging.ProfileOf(null, null));
        }

        [Test]
        public void AChargeMultipliesTheNumbersOfTheCastAndAnUnchargedCastIsUntouched()
        {
            var p = new ChargeProfile { damageAtFull = 3f };
            AbilityStats plain = AbilityStats.Identity;
            Assert.AreEqual(plain.damage, AbilityCharging.Apply(plain, p, 0f).damage);
            Assert.AreEqual(plain.damage, AbilityCharging.Apply(plain, null, 1f).damage);
            Assert.AreEqual(plain.damage * 3f, AbilityCharging.Apply(plain, p, 1f).damage, 1e-4f);
        }
    }
}
#endif
