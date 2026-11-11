#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of spellcasting (Phase 7, design spec 16.1-16.4, 14): spell forms, the compiled spell, what a cast costs and
    /// does in each soul condition, the energy drawn from the anchored runes and given back, misfires, Soul Strain (on success,
    /// on failure, none), and which spell is on which ability key. Pure: no game objects (what needs the engine is in
    /// <see cref="SpellsRuntimeTests"/>).
    /// </summary>
    public class SpellsCoreTests
    {
        // ------------------------------------------------------------------ fixtures
        private sealed class Book
        {
            public RuneCatalog c;
            public RuneSoulState s = new RuneSoulState();
            private int n;

            public Book(RuneCatalog catalog = null) { c = catalog ?? RuneDefaults.Catalog(); }

            public RuneInstance Rune(float energy, int rank = 1)
            {
                string id = "r" + n++;
                return RuneRules.Absorb(c, s, new RuneDrop { runeId = "fire", rank = rank, quality = 60f, energy = energy }, new RuneOrigin { kind = "test" }, id, 1L);
            }

            /// <summary>A spell anchoring runes holding <paramref name="energies"/>.</summary>
            public SpellRecord Spell(string name, string effect = "offense.pierce", int rank = 1, float complexity = 0.5f, float stability = 100f,
                float power = 100f, params float[] energies)
            {
                var sp = new SpellRecord
                {
                    spellId = "syn_" + n++, name = name, intentionId = "attack", effectId = effect, rank = rank, power = power, stability = stability,
                    complexity = complexity, energyCostMultiplier = 1f,
                };
                foreach (float e in energies) sp.anchors.Add(Rune(e, rank).instanceId);
                s.spells.Add(sp);
                return sp;
            }

            public CompiledSpell Compile(SpellRecord sp) => SpellCompiler.Compile(c, sp);
        }

        private static SoulRestrictions Condition(RuneCatalog c, float percent, bool locked = false)
        {
            float max = c.Balance.maxSoulIntegrity;
            var soul = new SoulState { initialized = true, integrity = max * percent / 100f, collapseLocked = locked };
            return SoulRules.Restrictions(c.Balance, soul, max);
        }

        private static RuneCatalog Custom(System.Action<RuneMagicData> edit) => SynthLab.Custom(edit);

        // ------------------------------------------------------------------ forms
        [Test]
        public void EveryDesiredEffectHasASpellForm()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            foreach (DesiredEffectDef fx in c.Effects)
                Assert.IsNotNull(c.FormOfEffect(fx.id), $"effect '{fx.id}' has no spell form");
            Assert.GreaterOrEqual(c.Effects.Count, 101);
        }

        [Test]
        public void TheBuiltInFormsAreBuiltOnPresetsThatShipWithTheAbilitySystem()
        {
            var names = new HashSet<string>(AbilityPresets.Basic.Select(p => p.name));
            foreach (SpellFormDef f in RuneDefaults.SpellForms())
                if (!string.IsNullOrEmpty(f.preset))
                    Assert.IsTrue(names.Contains(f.preset), $"form '{f.id}' names the preset '{f.preset}', which does not exist");
        }

        [Test]
        public void TheBuiltInFormsHaveUniqueValidIdsAndNoColonBecauseTheyArePartOfAnAbilityId()
        {
            List<SpellFormDef> forms = RuneDefaults.SpellForms();
            CollectionAssert.AllItemsAreUnique(forms.Select(f => f.id).ToList());
            foreach (SpellFormDef f in forms) Assert.IsFalse(f.id.Contains(":"), f.id);
        }

        [Test]
        public void AFormListingAnUnknownEffectIsAnError()
        {
            RuneCatalog c = Custom(d => d.spellForms[0].effects.Add("not.an.effect"));
            Assert.IsFalse(c.IsValid);
            Assert.IsTrue(c.Errors.Any(e => e.Contains("not.an.effect")));
        }

        [Test]
        public void AnEffectNoFormCoversIsAnErrorBecauseItsSpellsCouldNotBeCast()
        {
            RuneCatalog c = Custom(d =>
            {
                d.spellForms.RemoveAll(f => f.id == "ritual");
                foreach (SpellFormDef f in d.spellForms) f.fallbackFor.Clear();
            });
            Assert.IsFalse(c.IsValid);
            Assert.IsTrue(c.Errors.Any(e => e.Contains("has no spell form")), string.Join("\n", c.Errors));
        }

        [Test]
        public void ANewFormAddedByContentPicksUpItsEffectsWithoutCodeChanges()
        {
            RuneCatalog c = Custom(d =>
            {
                d.spellForms.Insert(0, new SpellFormDef { id = "zap", displayName = "Zap", preset = "Blink", effects = new List<string> { "offense.pierce" } });
            });
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            Assert.AreEqual("zap", c.FormOfEffect("offense.pierce").id);
            Assert.AreEqual("bolt", RuneDefaults.Catalog().FormOfEffect("offense.pierce").id);
        }

        // ------------------------------------------------------------------ the ability id
        [Test]
        public void ASpellAbilityIdRoundTripsAndOtherIdsAreNotSpells()
        {
            string id = SpellCompiler.AbilityId("bolt", "syn_0123456789ab");
            Assert.AreEqual("rune:bolt:syn_0123456789ab", id);
            Assert.IsTrue(SpellCompiler.TryParseAbilityId(id, out string form, out string spell));
            Assert.AreEqual("bolt", form);
            Assert.AreEqual("syn_0123456789ab", spell);
            foreach (string other in new[] { "", null, "runtime:Basic Attack", "rune:", "rune:bolt", "rune::x", "rune:bolt:", "4f1c9e2a0b7d4c6e8f3a1b2c3d4e5f60" })
                Assert.IsFalse(SpellCompiler.TryParseAbilityId(other, out _, out _), $"'{other}'");
        }

        // ------------------------------------------------------------------ compiling
        [Test]
        public void AHarderSpellCostsMoreEnergyAndAHigherRankCostsMoreStill()
        {
            var b = new Book();
            CompiledSpell easy = b.Compile(b.Spell("Easy", complexity: 0.1f, energies: 1000f));
            CompiledSpell hard = b.Compile(b.Spell("Hard", complexity: 0.9f, energies: 1000f));
            Assert.Greater(hard.energyCost, easy.energyCost);
            CompiledSpell high = b.Compile(b.Spell("High", rank: 3, complexity: 0.1f, energies: 5000f));
            Assert.Greater(high.energyCost, easy.energyCost);
            Assert.Greater(easy.energyCost, 0f);
        }

        [Test]
        public void ASpellsOwnCostMultiplierAndItsFormScaleTheCost()
        {
            var b = new Book();
            SpellRecord plain = b.Spell("Plain", "offense.pierce", energies: 1000f);
            float baseCost = b.Compile(plain).energyCost;
            plain.energyCostMultiplier = 0.5f;
            Assert.AreEqual(baseCost * 0.5f, b.Compile(plain).energyCost, baseCost * 0.001f);
            SpellRecord rush = b.Spell("Dash", "movement.dash", energies: 1000f);
            SpellRecord bolt = b.Spell("Bolt", "offense.pierce", energies: 1000f);
            Assert.Less(b.Compile(rush).energyCost, b.Compile(bolt).energyCost, "the Rush form is cheaper (0.8)");
        }

        [Test]
        public void TheCompiledSpellIsDeterministicAndKnowsItsForm()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("Firebolt", "offense.pierce", energies: 500f);
            CompiledSpell one = b.Compile(sp), two = b.Compile(sp);
            Assert.AreEqual("bolt", one.formId);
            Assert.AreEqual(SpellCompiler.AbilityId("bolt", sp.spellId), one.abilityId);
            Assert.AreEqual(one.energyCost, two.energyCost);
            Assert.AreEqual(one.damage, two.damage);
            Assert.AreEqual(one.castTime, two.castTime);
        }

        [Test]
        public void PowerBecomesDamageAndStabilityBecomesMisfireChance()
        {
            var b = new Book();
            CompiledSpell weak = b.Compile(b.Spell("Weak", power: 50f, stability: 100f, energies: 500f));
            CompiledSpell strong = b.Compile(b.Spell("Strong", power: 200f, stability: 100f, energies: 500f));
            Assert.Greater(strong.damage, weak.damage);
            Assert.AreEqual(0f, weak.baseInstability, 1e-5f, "a perfectly stable spell never misfires");
            CompiledSpell wobbly = b.Compile(b.Spell("Wobbly", stability: 40f, energies: 500f));
            Assert.Greater(wobbly.baseInstability, 0.1f);
            Assert.LessOrEqual(wobbly.baseInstability, 1f);
        }

        [Test]
        public void ANameDescribesTheCostsAndWhetherTheSpellingSystemIsUsed()
        {
            var b = new Book();
            CompiledSpell cs = b.Compile(b.Spell("Firebolt", energies: 500f));
            CastPlan plan = SpellCasting.Plan(b.c, cs, SoulRestrictions.None);
            string text = string.Join("\n", SpellCompiler.Describe(b.c, cs, plan));
            StringAssert.Contains("Rune Energy", text);
            StringAssert.Contains("Soul Strain", text);
            StringAssert.Contains("Spelling System: no", text);
        }

        // ------------------------------------------------------------------ casting debuffs by condition (spec 16.2)
        [Test]
        public void AStableSoulCastsAtFullPowerAtTheBaseCost()
        {
            var b = new Book();
            CompiledSpell cs = b.Compile(b.Spell("S", energies: 500f));
            CastPlan plan = SpellCasting.Plan(b.c, cs, Condition(b.c, 100f));
            Assert.IsTrue(plan.allowed);
            Assert.AreEqual(cs.energyCost, plan.energyCost, 1e-4f);
            Assert.AreEqual(1f, plan.powerMultiplier, 1e-5f);
            Assert.AreEqual(cs.castTime, plan.castTimeMultiplier, 1e-5f);
            Assert.AreEqual(cs.baseInstability, plan.instability, 1e-5f);
        }

        [Test]
        public void ADamagedSoulCastsSlowerWeakerAndAtAHigherCost()
        {
            var b = new Book();
            CompiledSpell cs = b.Compile(b.Spell("S", energies: 500f));
            CastPlan good = SpellCasting.Plan(b.c, cs, Condition(b.c, 100f));
            CastPlan bad = SpellCasting.Plan(b.c, cs, Condition(b.c, 50f)); // Damaged
            Assert.IsTrue(bad.allowed);
            Assert.Greater(bad.energyCost, good.energyCost);
            Assert.Greater(bad.castTimeMultiplier, good.castTimeMultiplier);
            Assert.Less(bad.powerMultiplier, good.powerMultiplier);
            SpellModifiers goodMods = SpellCompiler.ModifiersFor(cs, good), badMods = SpellCompiler.ModifiersFor(cs, bad);
            Assert.Less(badMods.damage, goodMods.damage, "the debuff is in the slot's modifiers");
            Assert.Greater(badMods.castTime, goodMods.castTime);
        }

        [Test]
        public void ACriticalSoulMakesEvenAStableSpellMisfire()
        {
            var b = new Book();
            CompiledSpell cs = b.Compile(b.Spell("S", stability: 100f, energies: 500f));
            Assert.AreEqual(0f, SpellCasting.Plan(b.c, cs, Condition(b.c, 100f)).instability, 1e-5f);
            CastPlan critical = SpellCasting.Plan(b.c, cs, Condition(b.c, 30f));
            Assert.AreEqual(0.15f, critical.instability, 1e-4f, "the condition's own instability is added to the spell's");
        }

        [Test]
        public void NearCollapseOnlyAllowsTheSimplestSpells()
        {
            var b = new Book();
            CompiledSpell rank1 = b.Compile(b.Spell("Low", rank: 1, energies: 500f));
            CompiledSpell rank2 = b.Compile(b.Spell("High", rank: 2, energies: 3000f));
            SoulRestrictions near = Condition(b.c, 10f);
            Assert.IsTrue(SpellCasting.Plan(b.c, rank1, near).allowed);
            CastPlan refused = SpellCasting.Plan(b.c, rank2, near);
            Assert.IsFalse(refused.allowed);
            StringAssert.Contains("rank 1", refused.reason);
        }

        [Test]
        public void ACollapsedSoulCannotCastAndNeitherCanOneStillLockedAfterACollapse()
        {
            var b = new Book();
            CompiledSpell cs = b.Compile(b.Spell("S", energies: 500f));
            CastPlan locked = SpellCasting.Plan(b.c, cs, Condition(b.c, 100f, locked: true));
            Assert.IsFalse(locked.allowed);
            StringAssert.Contains("collapsed", locked.reason);
            Assert.IsFalse(SpellCasting.Plan(b.c, cs, Condition(b.c, 0f)).allowed);
        }

        [Test]
        public void ACharacterWithoutASoulHasNoRestrictionsAtAll()
        {
            var b = new Book();
            CompiledSpell cs = b.Compile(b.Spell("S", energies: 500f));
            CastPlan plan = SpellCasting.Plan(b.c, cs, SoulRestrictions.None);
            Assert.IsTrue(plan.allowed);
            Assert.AreEqual(cs.baseInstability, plan.instability);
            Assert.AreEqual(1f, plan.powerMultiplier);
        }

        // ------------------------------------------------------------------ energy: drawn from the anchored runes, refunded
        [Test]
        public void ACastDrawsTheCostFromTheRunesThatAnchorTheSpell()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 100f, 100f, 100f });
            RuneInstance bystander = b.Rune(500f);
            float before = SpellRules.AnchoredEnergy(b.s, sp);
            CastDraw draw = SpellCasting.Draw(b.s, sp, 60f);
            Assert.IsNotNull(draw);
            Assert.AreEqual(60f, draw.Total, 1e-3f);
            Assert.AreEqual(before - 60f, SpellRules.AnchoredEnergy(b.s, sp), 1e-3f);
            Assert.AreEqual(500f, bystander.energy, "a rune that does not anchor the spell is never touched");
        }

        [Test]
        public void TheDrawTakesTheSameFromEachRuneUntilOneRunsDry()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 10f, 100f, 100f });
            CastDraw draw = SpellCasting.Draw(b.s, sp, 60f);
            var taken = draw.runes.ToDictionary(r => r.runeId, r => r.amount);
            Assert.AreEqual(10f, taken[sp.anchors[0]], 1e-3f, "the small rune is emptied");
            Assert.AreEqual(25f, taken[sp.anchors[1]], 1e-3f);
            Assert.AreEqual(25f, taken[sp.anchors[2]], 1e-3f);
            Assert.AreEqual(0f, b.s.Rune(sp.anchors[0]).energy, 1e-3f);
        }

        [Test]
        public void ACastThatTheRunesCannotPayForTakesNothing()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 20f, 20f });
            Assert.IsFalse(SpellCasting.CanAfford(b.s, sp, 50f, out string why));
            StringAssert.Contains("Not enough Rune Energy", why);
            Assert.IsNull(SpellCasting.Draw(b.s, sp, 50f));
            Assert.AreEqual(40f, SpellRules.AnchoredEnergy(b.s, sp), 1e-3f, "nothing was taken");
            Assert.IsTrue(SpellCasting.CanAfford(b.s, sp, 40f, out _), "exactly what the runes hold is enough");
        }

        [Test]
        public void AnInterruptedCastGivesTheEnergyBackToTheRunesItCameFrom()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 10f, 100f, 100f });
            var plan = new CastPlan { allowed = true };
            CastDraw draw = SpellCasting.Draw(b.s, sp, 60f);
            CastStrain strain = SpellCasting.End(b.c.Balance, b.s, draw, plan, misfire: false, out float refunded);
            Assert.AreEqual(60f, refunded, 1e-3f);
            Assert.AreEqual(210f, SpellRules.AnchoredEnergy(b.s, sp), 1e-3f, "back where it was");
            Assert.AreEqual(10f, b.s.Rune(sp.anchors[0]).energy, 1e-3f);
            Assert.IsFalse(strain.Any, "an interruption does not strain the soul by default");
        }

        [Test]
        public void ARefundNeverFillsARuneBeyondItsMaximum()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 50f });
            CastDraw draw = SpellCasting.Draw(b.s, sp, 30f);
            RuneInstance r = b.s.Rune(sp.anchors[0]);
            r.energy = b.c.Balance.MaxEnergy(r.rank, r.quality); // filled by other means meanwhile
            float given = SpellCasting.Refund(b.c.Balance, b.s, draw, 1f);
            Assert.AreEqual(0f, given, 1e-3f);
            Assert.AreEqual(b.c.Balance.MaxEnergy(r.rank, r.quality), r.energy, 1e-3f);
        }

        [Test]
        public void ARefundOfAFractionReturnsThatShare()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 100f });
            CastDraw draw = SpellCasting.Draw(b.s, sp, 40f);
            Assert.AreEqual(20f, SpellCasting.Refund(b.c.Balance, b.s, draw, 0.5f), 1e-3f);
            Assert.AreEqual(80f, b.s.Rune(sp.anchors[0]).energy, 1e-3f);
        }

        [Test]
        public void ASpellWhoseRunesAreGoneHasNoEnergyToCastWith()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 100f });
            b.s.runes.RemoveAll(r => r.instanceId == sp.anchors[0]);
            Assert.AreEqual(0f, SpellRules.AnchoredEnergy(b.s, sp));
            Assert.IsNull(SpellCasting.Draw(b.s, sp, 1f));
        }

        // ------------------------------------------------------------------ misfires
        [Test]
        public void AStableCastNeverMisfiresAndAnImpossibleOneAlwaysDoes()
        {
            SpellBalance sb = new SpellBalance();
            for (int seed = 0; seed < 500; seed++)
            {
                Assert.IsFalse(SpellCasting.Roll(sb, 0f, seed).misfire);
                Assert.IsTrue(SpellCasting.Roll(sb, 1f, seed).misfire);
            }
        }

        [Test]
        public void TheMisfireRateFollowsTheInstabilityAndTheRollIsReproducible()
        {
            SpellBalance sb = new SpellBalance();
            int hits = 0;
            const int n = 4000;
            for (int seed = 1; seed <= n; seed++)
            {
                CastRoll r = SpellCasting.Roll(sb, 0.3f, seed);
                CastRoll again = SpellCasting.Roll(sb, 0.3f, seed);
                Assert.AreEqual(r.misfire, again.misfire);
                Assert.AreEqual(r.fizzleAt, again.fizzleAt);
                if (r.misfire) hits++;
            }
            Assert.AreEqual(0.3f, hits / (float)n, 0.03f);
        }

        [Test]
        public void AMisfireCutsTheCastOffBetweenTheBalancesShares()
        {
            SpellBalance sb = new SpellBalance { fizzleMin = 0.4f, fizzleMax = 0.6f };
            for (int seed = 0; seed < 300; seed++)
            {
                float at = SpellCasting.Roll(sb, 1f, seed).fizzleAt;
                Assert.GreaterOrEqual(at, 0.4f);
                Assert.LessOrEqual(at, 0.6f);
            }
        }

        // ------------------------------------------------------------------ Soul Strain (spec 14.1): success, failure, none
        [Test]
        public void ByDefaultASuccessfulCastStrainsTheSoulALittleAndTheTooltipShowsWhatTheSoulWillLose()
        {
            var b = new Book();
            CompiledSpell cs = b.Compile(b.Spell("S", energies: 500f));
            CastPlan plan = SpellCasting.Plan(b.c, cs, Condition(b.c, 100f));
            CastStrain success = SpellCasting.Success(b.c.Balance, plan);
            Assert.AreEqual("spell_cast", success.kind);
            Assert.IsTrue(success.Any);
            Assert.Greater(plan.strainScaled, 0f);
            StringAssert.Contains("Soul Strain", string.Join("\n", SpellCompiler.Describe(b.c, cs, plan)));
            var soul = new SoulState { initialized = true, integrity = 100f };
            SoulDamageResult r = SoulRules.Damage(b.c.Balance, soul, 100f, success.amount, success.kind);
            Assert.AreEqual(plan.strainScaled, r.applied, 1e-3f, "the plan's scaled strain is what the soul loses");
            Assert.Less(soul.integrity, 100f);
        }

        [Test]
        public void ADamageKindWithMultiplierZeroMakesCastingFreeOfSoulDamage()
        {
            RuneCatalog c = Custom(d => d.balance.soulDamageKinds.Find(k => k.id == "spell_cast").multiplier = 0f);
            var b = new Book(c);
            CompiledSpell cs = b.Compile(b.Spell("S", energies: 500f));
            CastPlan plan = SpellCasting.Plan(c, cs, Condition(c, 100f));
            Assert.AreEqual(0f, plan.strainScaled, 1e-6f);
            StringAssert.Contains("Soul Strain: none", string.Join("\n", SpellCompiler.Describe(c, cs, plan)));
            CastStrain success = SpellCasting.Success(c.Balance, plan);
            var soul = new SoulState { initialized = true, integrity = 100f };
            Assert.AreEqual(0f, SoulRules.Damage(c.Balance, soul, 100f, success.amount, success.kind).applied, 1e-6f);
        }

        [Test]
        public void StrainOnSuccessCanBeSwitchedOffAndAHarderSpellStrainsMore()
        {
            RuneCatalog on = RuneDefaults.Catalog();
            RuneCatalog off = Custom(d => d.balance.spells.strainOnSuccess = false);
            var bOn = new Book(on);
            var bOff = new Book(off);
            Assert.IsTrue(SpellCasting.Success(on.Balance, SpellCasting.Plan(on, bOn.Compile(bOn.Spell("S", energies: 500f)), SoulRestrictions.None)).Any);
            Assert.IsFalse(SpellCasting.Success(off.Balance, SpellCasting.Plan(off, bOff.Compile(bOff.Spell("S", energies: 500f)), SoulRestrictions.None)).Any);
            CompiledSpell easy = bOn.Compile(bOn.Spell("E", complexity: 0.1f, energies: 500f));
            CompiledSpell hard = bOn.Compile(bOn.Spell("H", complexity: 0.9f, energies: 500f));
            Assert.Greater(hard.strain, easy.strain);
        }

        [Test]
        public void AMisfireCausesTheFailureDamageAndKeepsTheEnergyByDefault()
        {
            var b = new Book();
            SpellRecord sp = b.Spell("S", energies: new[] { 100f });
            CompiledSpell cs = b.Compile(sp);
            CastPlan plan = SpellCasting.Plan(b.c, cs, SoulRestrictions.None);
            CastDraw draw = SpellCasting.Draw(b.s, sp, plan.energyCost);
            CastStrain failure = SpellCasting.End(b.c.Balance, b.s, draw, plan, misfire: true, out float refunded);
            Assert.AreEqual("spell_failure", failure.kind);
            Assert.IsTrue(failure.Any);
            Assert.AreEqual(cs.failureDamage, failure.amount, 1e-4f);
            Assert.AreEqual(0f, refunded, "the energy of a misfired cast is spent");
            var soul = new SoulState { initialized = true, integrity = 100f };
            SoulDamageResult r = SoulRules.Damage(b.c.Balance, soul, 100f, failure.amount, failure.kind);
            Assert.Greater(r.applied, 0f, "the built-in failure kind hurts a little");
        }

        [Test]
        public void TheBalanceCanRefundAMisfiredCastAndHaveNoFailureDamage()
        {
            RuneCatalog c = Custom(d =>
            {
                d.balance.spells.misfireKeepsEnergy = false;
                d.balance.spells.failureDamageFactor = 0f;
            });
            var b = new Book(c);
            SpellRecord sp = b.Spell("S", energies: new[] { 100f });
            CastPlan plan = SpellCasting.Plan(c, b.Compile(sp), SoulRestrictions.None);
            CastDraw draw = SpellCasting.Draw(b.s, sp, plan.energyCost);
            CastStrain failure = SpellCasting.End(c.Balance, b.s, draw, plan, misfire: true, out float refunded);
            Assert.IsFalse(failure.Any, "no failure damage when the factor is 0");
            Assert.AreEqual(plan.energyCost, refunded, 1e-3f);
            Assert.AreEqual(100f, b.s.Rune(sp.anchors[0]).energy, 1e-3f);
        }

        [Test]
        public void AnInterruptionCanCauseTheFailureDamageWhenTheBalanceSaysSo()
        {
            RuneCatalog c = Custom(d => d.balance.spells.damageOnInterrupt = true);
            var b = new Book(c);
            SpellRecord sp = b.Spell("S", energies: new[] { 100f });
            CastPlan plan = SpellCasting.Plan(c, b.Compile(sp), SoulRestrictions.None);
            CastDraw draw = SpellCasting.Draw(b.s, sp, plan.energyCost);
            CastStrain failure = SpellCasting.End(c.Balance, b.s, draw, plan, misfire: false, out float refunded);
            Assert.IsTrue(failure.Any);
            Assert.AreEqual(plan.energyCost, refunded, 1e-3f, "an interruption still gives the energy back");
        }

        [Test]
        public void TheSoulsConditionMultipliesTheDamageOfACastAsItDoesAnyOtherSoulDamage()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            var b = new Book(c);
            CompiledSpell cs = b.Compile(b.Spell("S", energies: 500f));
            CastPlan fine = SpellCasting.Plan(c, cs, Condition(c, 100f));
            CastPlan critical = SpellCasting.Plan(c, cs, Condition(c, 30f));
            Assert.Greater(critical.strainScaled, fine.strainScaled);
            Assert.AreEqual(fine.strain, critical.strain, 1e-5f, "what is asked for is the same; the soul scales it");
        }

        // ------------------------------------------------------------------ assigning, swapping, unassigning (spec 16.4)
        [Test]
        public void ASpellCanBePutOnAnEmptyKey()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f);
            var touched = new List<int>();
            Assert.IsTrue(SpellSlotRules.Assign(b.s, a.spellId, 2, touched, out _));
            Assert.AreEqual(2, SpellSlotRules.SlotOf(b.s, a.spellId));
            Assert.AreEqual(a.spellId, SpellSlotRules.At(b.s, 2).spellId);
            CollectionAssert.Contains(touched, 2);
            Assert.AreEqual(-1, SpellSlotRules.SlotOf(b.s, "nope"));
        }

        [Test]
        public void ASpellIsOnlyEverOnOneKey()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 0, null, out _);
            SpellSlotRules.Assign(b.s, a.spellId, 3, null, out _);
            Assert.AreEqual(3, SpellSlotRules.SlotOf(b.s, a.spellId));
            Assert.IsNull(SpellSlotRules.At(b.s, 0), "the old key is free");
            Assert.AreEqual(1, b.s.spellSlots.Count);
        }

        [Test]
        public void PuttingASpellOnTheKeyOfAnotherSwapsThemWhenItAlreadyHadAKey()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f), c2 = b.Spell("B", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 0, null, out _);
            SpellSlotRules.Assign(b.s, c2.spellId, 1, null, out _);
            var touched = new List<int>();
            Assert.IsTrue(SpellSlotRules.Assign(b.s, a.spellId, 1, touched, out _));
            Assert.AreEqual(1, SpellSlotRules.SlotOf(b.s, a.spellId));
            Assert.AreEqual(0, SpellSlotRules.SlotOf(b.s, c2.spellId), "B takes the key A left");
            CollectionAssert.Contains(touched, 0);
            CollectionAssert.Contains(touched, 1);
        }

        [Test]
        public void PuttingASpellFromTheBookOnAnOccupiedKeySendsTheOtherBackToTheBook()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f), c2 = b.Spell("B", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 0, null, out _);
            Assert.IsTrue(SpellSlotRules.Assign(b.s, c2.spellId, 0, null, out _));
            Assert.AreEqual(0, SpellSlotRules.SlotOf(b.s, c2.spellId));
            Assert.AreEqual(-1, SpellSlotRules.SlotOf(b.s, a.spellId), "A is back in the spellbook (it is not lost)");
            Assert.IsNotNull(SpellRules.Find(b.s, a.spellId));
        }

        [Test]
        public void TwoKeysCanExchangeWhatTheyHoldEvenWhenOneIsEmpty()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f), c2 = b.Spell("B", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 0, null, out _);
            SpellSlotRules.Assign(b.s, c2.spellId, 1, null, out _);
            Assert.IsTrue(SpellSlotRules.Swap(b.s, 0, 1));
            Assert.AreEqual(1, SpellSlotRules.SlotOf(b.s, a.spellId));
            Assert.AreEqual(0, SpellSlotRules.SlotOf(b.s, c2.spellId));
            Assert.IsTrue(SpellSlotRules.Swap(b.s, 0, 5), "to an empty key");
            Assert.AreEqual(5, SpellSlotRules.SlotOf(b.s, c2.spellId));
            Assert.IsFalse(SpellSlotRules.Swap(b.s, 7, 8), "two empty keys: nothing to do");
        }

        [Test]
        public void AnUnassignedSpellStaysInTheSpellbook()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 4, null, out _);
            Assert.IsTrue(SpellSlotRules.Unassign(b.s, 4));
            Assert.IsFalse(SpellSlotRules.Unassign(b.s, 4), "nothing there any more");
            Assert.AreEqual(-1, SpellSlotRules.SlotOf(b.s, a.spellId));
            Assert.IsNotNull(SpellRules.Find(b.s, a.spellId));
        }

        [Test]
        public void ASpellThatIsNotInTheBookCannotBeAssignedAndANegativeKeyIsRefused()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f);
            Assert.IsFalse(SpellSlotRules.Assign(b.s, "syn_missing", 0, null, out string why));
            StringAssert.Contains("no longer", why);
            Assert.IsFalse(SpellSlotRules.Assign(b.s, a.spellId, -1, null, out _));
            Assert.AreEqual(0, b.s.spellSlots.Count);
        }

        [Test]
        public void NormalizingDropsKeysOfSpellsThatAreGoneAndDuplicates()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f), c2 = b.Spell("B", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 0, null, out _);
            SpellSlotRules.Assign(b.s, c2.spellId, 1, null, out _);
            b.s.spellSlots.Add(new SpellSlotAssignment(2, "syn_gone"));
            b.s.spellSlots.Add(new SpellSlotAssignment(1, a.spellId)); // a second key for A, on a key B holds
            b.s.spellSlots.Add(new SpellSlotAssignment(-3, c2.spellId));
            List<int> emptied = SpellSlotRules.Normalize(b.s);
            Assert.AreEqual(2, b.s.spellSlots.Count);
            CollectionAssert.Contains(emptied, 2);
            Assert.AreEqual(0, SpellSlotRules.SlotOf(b.s, a.spellId));
            Assert.AreEqual(1, SpellSlotRules.SlotOf(b.s, c2.spellId));
        }

        [Test]
        public void DismissingASpellFreesItsKeyOnTheNextNormalize()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 1, null, out _);
            SpellRules.Dismiss(b.s, a.spellId);
            SpellSlotRules.Normalize(b.s);
            Assert.AreEqual(0, b.s.spellSlots.Count);
        }

        [Test]
        public void TheKeysSurviveASaveAndAnOldSaveHasNone()
        {
            var b = new Book();
            SpellRecord a = b.Spell("A", energies: 100f);
            SpellSlotRules.Assign(b.s, a.spellId, 3, null, out _);
            RuneSoulState back = RuneSoulState.FromJson(b.s.ToJson());
            Assert.AreEqual(3, SpellSlotRules.SlotOf(back, a.spellId));

            RuneSoulState old = RuneSoulState.FromJson("{\"version\":4}");
            Assert.IsNotNull(old.spellSlots);
            Assert.AreEqual(0, old.spellSlots.Count);
            Assert.AreEqual(4, old.version, "the format version is only a record: fields are only ever added");
            Assert.AreEqual(6, RuneSoulState.CurrentVersion, "5 added the keys of spells; 6 the grimoire's serial");
        }

        [Test]
        public void ASavedKeyForASpellThatNoLongerExistsIsDroppedWhenTheSaveLoads()
        {
            var b = new Book();
            b.s.spellSlots.Add(new SpellSlotAssignment(0, "syn_ghost"));
            RuneSoulState back = RuneSoulState.FromJson(b.s.ToJson());
            Assert.AreEqual(0, back.spellSlots.Count);
        }

        // ------------------------------------------------------------------ data
        [Test]
        public void TheSpellDataIsPartOfTheDatabaseAndAnOldDatabaseIsUpgraded()
        {
            RuneMagicData d = RuneDefaults.Create();
            Assert.AreEqual(RuneMagicData.CurrentVersion, d.version);
            Assert.GreaterOrEqual(d.spellForms.Count, 10);
            Assert.IsNotNull(d.balance.spells);

            var old = RuneDefaults.Create();
            old.version = 5;
            old.spellForms.Clear();
            old.balance.spells = null;
            Assert.IsTrue(old.Upgrade());
            Assert.GreaterOrEqual(old.spellForms.Count, 10);
            Assert.IsNotNull(old.balance.spells);
            Assert.IsTrue(RuneCatalog.Compile(old).IsValid);
        }

        [Test]
        public void TheSpellBalanceIsValidatedAndRoundTripsThroughJson()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            new SpellBalance { fizzleMin = 0.9f, fizzleMax = 0.5f, strainKind = " " }.Validate(errors, warnings);
            Assert.GreaterOrEqual(errors.Count, 2);
            errors.Clear();
            new SpellBalance().Validate(errors, warnings);
            Assert.IsEmpty(errors);

            RuneMagicData d = RuneDefaults.Create();
            d.balance.spells.castEnergyFraction = 0.123f;
            d.balance.spells.replaceOtherAbilities = true;
            RuneMagicData back = UnityEngine.JsonUtility.FromJson<RuneMagicData>(UnityEngine.JsonUtility.ToJson(d));
            Assert.AreEqual(0.123f, back.balance.spells.castEnergyFraction, 1e-6f);
            Assert.IsTrue(back.balance.spells.replaceOtherAbilities);
        }

        [Test]
        public void ADatabaseExtensionCanAddAFormAndItsEffectsWithoutTouchingTheBase()
        {
            RuneMagicData ext = new RuneMagicData
            {
                version = RuneMagicData.CurrentVersion,
                spellForms = new List<SpellFormDef> { new SpellFormDef { id = "zap", displayName = "Zap", effects = new List<string> { "offense.pierce" } } },
            };
            RuneCatalog c = RuneCatalog.Compile(RuneDefaults.Create(), new List<RuneMagicData> { ext });
            Assert.IsNotNull(c.SpellForm("zap"));
            Assert.IsNotNull(c.SpellForm("bolt"), "the base forms are still there");
        }

        [Test]
        public void ARuneUserThatNeverCastsKeepsEveryDefaultOfTheEarlierPhases()
        {
            // Phase 7 only adds: the earlier balance, content and the soul save are untouched for anyone who never casts.
            RuneMagicData d = RuneDefaults.Create();
            Assert.AreEqual(100f, d.balance.maxSoulIntegrity);
            Assert.AreEqual(6, d.balance.soulConditions.Count);
            RuneSoulState s = new RuneSoulState();
            s.EnsureLists();
            Assert.AreEqual(0, s.spellSlots.Count);
            Assert.AreEqual(0, s.spells.Count);
        }
    }
}
#endif
