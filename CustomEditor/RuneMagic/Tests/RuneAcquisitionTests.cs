#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of rune acquisition, absorption and energy (Phase 3): drop profiles and rolls (species, rank,
    /// alignment, rarity, dungeon tier, boss), energy rewards, the pending pool and the player's choice of rune, full and
    /// overflow rules, capacity-checked absorption, release, alignment strain, concept knowledge, rune item ids. Pure rules.
    /// </summary>
    public class RuneAcquisitionTests
    {
        private const float Eps = 0.0001f;

        private static RuneCatalog Catalog() => RuneDefaults.Catalog();

        private static RuneCatalog Custom(System.Action<RuneMagicData> edit)
        {
            RuneMagicData d = RuneDefaults.Create();
            edit(d);
            return RuneCatalog.Compile(d);
        }

        private static RuneDropContext Ctx(string species, int tier = 0, MonsterRarity rarity = MonsterRarity.Normal) =>
            RuneDropContext.For(species, tier, rarity);

        private static HashSet<string> RunesOf(RuneDropProfile p)
        {
            var set = new HashSet<string>();
            foreach (RuneDropEntry e in p.runes) set.Add(e.runeId);
            return set;
        }

        // ------------------------------------------------------------------ content (design spec 3.1)
        [Test]
        public void TheSpecMonstersCarryTheSpecRunes()
        {
            RuneCatalog c = Catalog();
            Assert.IsTrue(c.IsValid, string.Join("\n", c.Errors));
            CollectionAssert.IsSupersetOf(RunesOf(c.DropProfile("fire_elemental")), new[] { "fire", "heat", "energy" });
            CollectionAssert.IsSupersetOf(RunesOf(c.DropProfile("wind_beast")), new[] { "wind", "gale", "vibration" });
            CollectionAssert.IsSupersetOf(RunesOf(c.DropProfile("stone_golem")), new[] { "earth", "matter", "armor" });
            CollectionAssert.IsSupersetOf(RunesOf(c.DropProfile("shadow_creature")), new[] { "shadow", "mind", "absorption" });
            CollectionAssert.IsSupersetOf(RunesOf(c.DropProfile("crystal_guardian")), new[] { "crystal", "space", "reflection" });
            RuneDropProfile dragon = c.DropProfile("ancient_dragon");
            CollectionAssert.IsSupersetOf(RunesOf(dragon), new[] { "fire", "gravity" });
            Assert.Greater(dragon.baseRank, 1, "rare higher-rank runes");
            Assert.IsNotNull(c.DropProfile(c.Balance.acquisition.defaultProfile), "monsters nothing matches still carry runes");
        }

        [Test]
        public void ValidationReportsBrokenDropData()
        {
            RuneCatalog c = Custom(d =>
            {
                d.dropProfiles[0].runes.Add(new RuneDropEntry("no_such_rune", 1f));
                d.dropProfiles[1].qualityRange = new UnityEngine.Vector2(80f, 20f);
                d.balance.acquisition.defaultProfile = "missing";
            });
            Assert.IsTrue(new List<string>(c.Errors).Exists(e => e.Contains("unknown rune 'no_such_rune'")), string.Join("\n", c.Errors));
            Assert.IsTrue(new List<string>(c.Errors).Exists(e => e.Contains("quality range is reversed")), string.Join("\n", c.Errors));
            Assert.IsTrue(new List<string>(c.Errors).Exists(e => e.Contains("default drop profile 'missing'")), string.Join("\n", c.Errors));
        }

        [Test]
        public void OlderDatabasesGetTheDropDataOnUpgrade()
        {
            RuneMagicData d = RuneDefaults.Create();
            d.version = 2;
            d.dropProfiles.Clear();
            d.balance.acquisition.rarityWeights.Clear();
            Assert.IsTrue(d.Upgrade());
            Assert.AreEqual(RuneDefaults.DropProfiles().Count, d.dropProfiles.Count);
            Assert.AreEqual(5, d.balance.acquisition.rarityWeights.Count);
        }

        // ------------------------------------------------------------------ which profile
        [Test]
        public void MonstersAreMatchedBySpeciesNameFactionTagsAndPriority()
        {
            RuneCatalog c = Catalog();
            Assert.AreEqual("fire_elemental", DropRules.SelectProfile(c, Ctx("FireElemental")).id, "camel case is split into words");
            Assert.AreEqual("fire_elemental", DropRules.SelectProfile(c, Ctx("Fire Wolf")).id, "a higher priority wins (fire over beast)");
            Assert.AreEqual("ancient_dragon", DropRules.SelectProfile(c, Ctx("Ancient Red Dragon")).id);
            Assert.AreEqual("beast", DropRules.SelectProfile(c, Ctx("wolf")).id);
            Assert.AreEqual("beast", DropRules.SelectProfile(c, Ctx("Dire Wolfs")).id, "simple plurals");
            Assert.AreEqual("generic", DropRules.SelectProfile(c, Ctx("Slime")).id, "nothing matches: the default profile");
            Assert.AreEqual("generic", DropRules.SelectProfile(c, Ctx("Firefly")).id, "'firefly' is not 'fire'");

            RuneDropContext byFaction = Ctx("Thing");
            byFaction.faction = "Storm Cult";
            Assert.AreEqual("wind_beast", DropRules.SelectProfile(c, byFaction).id, "the faction's name counts");

            RuneDropContext named = Ctx("wolf");
            named.profileId = "crystal_guardian";
            Assert.AreEqual("crystal_guardian", DropRules.SelectProfile(c, named).id, "a Rune Source can name the profile");
            named.profileId = "nope";
            Assert.AreEqual("beast", DropRules.SelectProfile(c, named).id, "an unknown name falls back to matching");

            RuneCatalog tagged = Custom(d => d.dropProfiles.Add(new RuneDropProfile
            {
                id = "crystalline", matchTags = new List<string> { "crystalline" }, priority = 30,
                runes = new List<RuneDropEntry> { new RuneDropEntry("crystal", 1f) },
            }));
            RuneDropContext special = Ctx("wolf");
            special.tags = new[] { "Crystalline" };
            Assert.AreEqual("crystalline", DropRules.SelectProfile(tagged, special).id, "special properties match tags");

            RuneCatalog none = Custom(d => d.balance.acquisition.defaultProfile = "");
            Assert.IsNull(DropRules.SelectProfile(none, Ctx("Slime")), "no default profile: no runes");
            Assert.AreEqual(0f, DropRules.EnergyReward(none, Ctx("Slime")), Eps);
        }

        // ------------------------------------------------------------------ chance, rank, quality
        [Test]
        public void DifficultyRarityAndBossesRaiseTheChance()
        {
            RuneCatalog c = Catalog();
            RuneAcquisitionBalance a = c.Balance.acquisition;
            RuneDropProfile p = c.DropProfile("fire_elemental");
            float baseChance = DropRules.DropChance(a, p, Ctx("fire"));
            Assert.AreEqual(p.dropChance, baseChance, Eps);
            Assert.AreEqual(p.dropChance * 1.2f, DropRules.DropChance(a, p, Ctx("fire", 2)), Eps, "+10% per dungeon tier");
            Assert.AreEqual(p.dropChance * 2f, DropRules.DropChance(a, p, Ctx("fire", 0, MonsterRarity.Elite)), Eps);
            Assert.AreEqual(p.dropChance * 3f, DropRules.DropChance(a, p, Ctx("fire", 0, MonsterRarity.Rare)), Eps);
            Assert.AreEqual(p.dropChance * 5f, DropRules.DropChance(a, p, Ctx("fire", 0, MonsterRarity.Boss)), Eps);
            Assert.LessOrEqual(DropRules.DropChance(a, p, Ctx("fire", 100, MonsterRarity.Boss)), 1f);
            Assert.AreEqual(p.maxDrops + a.bossExtraDrops, DropRules.MaxDrops(a, p, Ctx("fire", 0, MonsterRarity.Boss)));
        }

        [Test]
        public void RollsAreDeterministicAndWithinTheRules()
        {
            RuneCatalog c = Catalog();
            List<RuneDrop> first = new List<RuneDrop>(), second = new List<RuneDrop>();
            var r1 = new System.Random(7);
            var r2 = new System.Random(7);
            for (int i = 0; i < 300; i++)
            {
                first.AddRange(DropRules.Roll(c, Ctx("Ancient Dragon", 3, MonsterRarity.Boss), r1));
                second.AddRange(DropRules.Roll(c, Ctx("Ancient Dragon", 3, MonsterRarity.Boss), r2));
            }
            Assert.AreEqual(first.Count, second.Count, "the same seed gives the same drops");
            for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i].ToString(), second[i].ToString());
            Assert.Greater(first.Count, 0);
            foreach (RuneDrop d in first)
            {
                RuneDef def = c.Rune(d.runeId);
                Assert.IsNotNull(def);
                Assert.GreaterOrEqual(d.rank, 3, "dragon base rank 2 + boss bonus 1");
                Assert.LessOrEqual(d.rank, System.Math.Min(def.maxRank, c.Balance.acquisition.maxDropRank));
                Assert.That(d.quality, Is.InRange(0f, 100f));
                Assert.AreEqual(System.Math.Round(d.quality), d.quality, Eps, "whole qualities");
                Assert.LessOrEqual(d.energy, c.Balance.MaxEnergy(d.rank, d.quality));
            }
        }

        [Test]
        public void BossesAlwaysDropAndOrdinaryKillsMostlyDoNot()
        {
            RuneCatalog c = Custom(d => d.dropProfiles.Find(p => p.id == "beast").dropChance = 0f);
            var rng = new System.Random(3);
            Assert.AreEqual(0, DropRules.Roll(c, Ctx("wolf"), rng).Count, "chance 0: nothing");
            Assert.AreEqual(1, DropRules.Roll(c, Ctx("wolf", 0, MonsterRarity.Boss), rng).Count, "a boss always drops a rune");
            RuneCatalog never = Custom(d =>
            {
                d.dropProfiles.Find(p => p.id == "beast").dropChance = 0f;
                d.balance.acquisition.bossAlwaysDrops = false;
            });
            Assert.AreEqual(0, DropRules.Roll(never, Ctx("wolf", 0, MonsterRarity.Boss), rng).Count);
        }

        [Test]
        public void RankAndQualityGrowWithDifficulty()
        {
            RuneCatalog c = Catalog();
            RuneAcquisitionBalance a = c.Balance.acquisition;
            RuneDropProfile p = c.DropProfile("fire_elemental");
            RuneDef fire = c.Rune("fire");
            RuneDropEntry entry = p.runes[0];
            var rng = new System.Random(1);
            Assert.AreEqual(1, DropRules.RollRank(a, p, entry, fire, Ctx("fire"), rng), "tier 0: the base rank");
            Assert.AreEqual(2, DropRules.RollRank(a, p, entry, fire, Ctx("fire", 0, MonsterRarity.Boss), rng), "boss +1");
            RuneDropContext ranked = Ctx("fire");
            ranked.monsterRank = 4;
            Assert.AreEqual(4, DropRules.RollRank(a, p, entry, fire, ranked, rng), "at least the monster's rank");
            ranked.monsterRank = 50;
            Assert.AreEqual(a.maxDropRank, DropRules.RollRank(a, p, entry, fire, ranked, rng), "never above Max Drop Rank");
            int ups = 0;
            for (int i = 0; i < 1000; i++)
                if (DropRules.RollRank(a, p, entry, fire, Ctx("fire", 4), rng) > 1) ups++;
            Assert.That(ups, Is.InRange(480, 720), "tier 4: 60% chance of a rank more");

            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 200; i++)
            {
                float q = DropRules.RollQuality(a, p, Ctx("fire"), rng);
                lo = System.Math.Min(lo, q);
                hi = System.Math.Max(hi, q);
            }
            Assert.GreaterOrEqual(lo, p.qualityRange.x);
            Assert.LessOrEqual(hi, p.qualityRange.y);
            float bossQ = DropRules.RollQuality(a, p, Ctx("fire", 5, MonsterRarity.Boss), new System.Random(1));
            float normalQ = DropRules.RollQuality(a, p, Ctx("fire"), new System.Random(1));
            Assert.AreEqual(normalQ + 5 * a.qualityPerTier + a.bossQualityBonus, bossQ, 1f, "tier and boss raise the quality");
        }

        [Test]
        public void AlignmentAndRarityShapeWhichRunesDrop()
        {
            RuneCatalog c = Catalog();
            RuneDropProfile generic = c.DropProfile("generic");
            RuneDropContext shadowy = Ctx("Slime");
            shadowy.alignments = new[] { "shadow" };
            List<RuneCandidate> list = DropRules.Candidates(c, generic, shadowy);
            Assert.IsTrue(list.Exists(k => k.rune.id == "shadow"), "the monster's own alignment drops even when the profile does not list it");

            RuneDropProfile fireP = c.DropProfile("fire_elemental");
            float plain = DropRules.Candidates(c, fireP, Ctx("fire")).Find(k => k.rune.id == "fire").weight;
            RuneDropContext fiery = Ctx("fire");
            fiery.alignments = new[] { "fire" };
            float aligned = DropRules.Candidates(c, fireP, fiery).Find(k => k.rune.id == "fire").weight;
            Assert.AreEqual(plain * c.Balance.acquisition.alignmentWeightMultiplier, aligned, Eps);

            RuneDropProfile dragon = c.DropProfile("ancient_dragon");
            float Share(RuneDropContext ctx, string id)
            {
                List<RuneCandidate> k = DropRules.Candidates(c, dragon, ctx);
                float total = 0f, mine = 0f;
                foreach (RuneCandidate x in k) { total += x.weight; if (x.rune.id == id) mine += x.weight; }
                return mine / total;
            }
            Assert.Greater(Share(Ctx("dragon", 0, MonsterRarity.Boss), "time"), Share(Ctx("dragon"), "time"),
                "rare monsters and bosses give unusual, powerful runes more often");
        }

        // ------------------------------------------------------------------ energy rewards (design spec 5.1)
        [Test]
        public void EnergyFromKillsFollowsTheMonster()
        {
            RuneCatalog c = Catalog();
            Assert.AreEqual(150f, DropRules.EnergyReward(c, Ctx("Fire Elemental")), Eps, "the spec's example: 150 energy");
            Assert.AreEqual(225f, DropRules.EnergyReward(c, Ctx("Fire Elemental", 2)), Eps, "+25% per tier");
            Assert.AreEqual(900f, DropRules.EnergyReward(c, Ctx("Fire Elemental", 0, MonsterRarity.Boss)), Eps, "bosses ×6");
            RuneDropContext rich = Ctx("Fire Elemental");
            rich.zoneRewardMultiplier = 2f;
            Assert.AreEqual(300f, DropRules.EnergyReward(c, rich), Eps, "richer zones far from the city");
            rich.energyMultiplier = 0.5f;
            Assert.AreEqual(150f, DropRules.EnergyReward(c, rich), Eps, "Rune Source multiplier");
            Assert.Greater(DropRules.EnergyReward(c, Ctx("dragon")), DropRules.EnergyReward(c, Ctx("wolf")));
        }

        // ------------------------------------------------------------------ pending energy and the player's choice (5.1, 5.2, 5.4)
        private static RuneSoulState SoulWith(RuneCatalog c, params (string rune, int rank, float energy)[] runes)
        {
            var s = new RuneSoulState();
            foreach (var r in runes)
                RuneRules.Absorb(c, s, new RuneDrop { runeId = r.rune, rank = r.rank, quality = 50f, energy = r.energy }, new RuneOrigin { kind = "test" }, null, 1L);
            return s;
        }

        [Test]
        public void OnlyTheChosenRuneReceivesTheEnergy()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, ("wind", 1, 0f), ("fire", 1, 0f), ("space", 1, 0f));
            Assert.AreEqual(150f, EnergyRules.AddPending(c.Balance, s, 150f), Eps);
            EnergyTransfer t = EnergyRules.Assign(c.Balance, s, s.runes[1].instanceId, 0f);
            Assert.IsTrue(t.Ok);
            Assert.AreEqual(150f, s.runes[1].energy, Eps, "Greater Fire +150");
            Assert.AreEqual(0f, s.runes[0].energy, Eps);
            Assert.AreEqual(0f, s.runes[2].energy, Eps);
            Assert.AreEqual(0f, s.pendingEnergy, Eps);
            Assert.AreEqual(EnergyRefusal.NothingPending, EnergyRules.Assign(c.Balance, s, s.runes[0].instanceId, 10f).refusal);
            Assert.AreEqual(EnergyRefusal.NoSuchRune, EnergyRules.Assign(c.Balance, s, "nope", 10f).refusal);
        }

        [Test]
        public void AFullRuneTakesNothingAndNothingAdvancesARank()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, ("wind", 1, 950f), ("fire", 1, 1000f));
            EnergyRules.AddPending(c.Balance, s, 200f);
            EnergyTransfer full = EnergyRules.Assign(c.Balance, s, s.runes[1].instanceId, 0f);
            Assert.AreEqual(EnergyRefusal.Full, full.refusal, "a full rune cannot absorb more");
            StringAssert.Contains("choose another rune", full.Describe("Greater Fire"));
            Assert.AreEqual(200f, s.pendingEnergy, Eps, "nothing is lost");

            EnergyTransfer partial = EnergyRules.Assign(c.Balance, s, s.runes[0].instanceId, 0f);
            Assert.AreEqual(50f, partial.moved, Eps, "only what fits");
            Assert.AreEqual(150f, s.pendingEnergy, Eps, "the overflow stays pending (choose another rune)");
            Assert.IsTrue(EnergyRules.IsFull(c.Balance, s.runes[0]));
            Assert.AreEqual(1, s.runes[0].rank, "full energy never advances the rank (5.2, 5.4)");
            Assert.AreEqual(2, s.runes.Count, "no higher-rank rune appears");

            RuneSoulState some = SoulWith(c, ("wind", 1, 0f));
            EnergyRules.AddPending(c.Balance, some, 500f);
            EnergyRules.Assign(c.Balance, some, some.runes[0].instanceId, 120f);
            Assert.AreEqual(120f, some.runes[0].energy, Eps, "a chosen amount");
            Assert.AreEqual(380f, some.pendingEnergy, Eps);
        }

        [Test]
        public void PendingEnergyCanBeCapped()
        {
            RuneCatalog c = Custom(d => d.balance.acquisition.maxPendingEnergy = 300f);
            var s = new RuneSoulState();
            Assert.AreEqual(200f, EnergyRules.AddPending(c.Balance, s, 200f), Eps);
            Assert.AreEqual(100f, EnergyRules.AddPending(c.Balance, s, 200f), Eps, "only up to the cap");
            Assert.AreEqual(300f, s.pendingEnergy, Eps);
            Assert.AreEqual(0f, EnergyRules.AddPending(c.Balance, s, float.NaN), Eps);
        }

        [Test]
        public void RankQualityEnergyAndKnowledgeStayIndependent()
        {
            RuneCatalog c = Catalog();
            Assert.AreEqual(c.Balance.MaxEnergy(1, 0f), c.Balance.MaxEnergy(1, 100f), Eps, "quality does not change the maximum (coupling off)");
            Assert.AreEqual(1000f, c.Balance.MaxEnergy(1), Eps);
            Assert.AreEqual(5000f, c.Balance.MaxEnergy(2), Eps);
            RuneSoulState s = SoulWith(c, ("wind", 3, 0f));
            float q = s.runes[0].quality;
            EnergyRules.AddPending(c.Balance, s, 999f);
            EnergyRules.Assign(c.Balance, s, s.runes[0].instanceId, 0f);
            Assert.AreEqual(3, s.runes[0].rank);
            Assert.AreEqual(q, s.runes[0].quality, Eps);
            Assert.AreEqual(0, RuneRules.KnownConcepts(s, "wind").Count, "a high rank grants no concept");
        }

        // ------------------------------------------------------------------ absorption and capacity (3.2, 4)
        [Test]
        public void AbsorbingNeedsFreeSoulSlots()
        {
            RuneCatalog c = Catalog();
            var s = new RuneSoulState();
            int capacity = CapacityRules.Capacity(c.Balance, 1, s.soul);
            Assert.AreEqual(7, capacity);
            for (int i = 0; i < 7; i++)
            {
                Assert.IsTrue(RuneRules.CanAbsorb(c, s, capacity, "wind", out string why), why);
                RuneRules.Absorb(c, s, new RuneDrop { runeId = "wind", rank = 1, quality = 20f }, null, null, 1L);
            }
            Assert.AreEqual(7, RuneRules.UsedSlots(c, s));
            Assert.IsFalse(RuneRules.CanAbsorb(c, s, capacity, "fire", out string full), "no capacity: cannot absorb");
            StringAssert.Contains("Free a slot", full);
            Assert.IsTrue(RuneRules.CanAbsorb(c, s, capacity + 1, "fire", out _), "more capacity: room again");

            RuneRules.Release(s, s.runes[0].instanceId);
            Assert.AreEqual(6, RuneRules.UsedSlots(c, s));
            Assert.IsFalse(RuneRules.CanAbsorb(c, s, capacity, "time", out _), "Time occupies 2 slots, 1 is free");
            Assert.IsTrue(RuneRules.CanAbsorb(c, s, capacity, "fire", out _));
            Assert.IsFalse(RuneRules.CanAbsorb(c, s, capacity, "unknown_rune", out _));
            Assert.AreEqual(7, s.runesAbsorbed);
            Assert.AreEqual(1, s.runesReleased);
        }

        [Test]
        public void AbsorbedEnergyIsKeptWithinTheMaximum()
        {
            RuneCatalog c = Catalog();
            var s = new RuneSoulState();
            RuneInstance r = RuneRules.Absorb(c, s, new RuneDrop { runeId = "fire", rank = 1, quality = 50f, energy = 99999f }, null, "id1", 5L);
            Assert.AreEqual(1000f, r.energy, Eps);
            Assert.AreEqual("id1", r.instanceId);
            Assert.AreEqual(5L, r.acquiredUtc);
            Assert.AreSame(r, s.Rune("id1"));
        }

        [Test]
        public void ConflictingAlignmentsStrainTheSoul()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, ("water", 1, 0f));
            Assert.AreEqual(0.4f, RuneRules.Conflict(c, s, c.Rune("fire")), Eps, "fire against water");
            Assert.AreEqual(10f, RuneRules.AbsorptionDamage(c, s, c.Rune("fire")), Eps, "25 per unit of conflict");
            Assert.AreEqual(0f, RuneRules.Conflict(c, SoulWith(c, ("wind", 1, 0f)), c.Rune("fire")), Eps, "wind feeds fire: no strain");
            Assert.AreEqual(0f, RuneRules.Conflict(c, new RuneSoulState(), c.Rune("fire")), Eps);
            Assert.IsNotNull(c.Balance.DamageKind("incompatible_absorption"));
        }

        [Test]
        public void ReleasingFollowsTheRules()
        {
            RuneCatalog c = Custom(d => { });
            RuneAcquisitionBalance a = c.Balance.acquisition;
            RuneSoulState s = SoulWith(c, ("fire", 1, 400f));
            string id = s.runes[0].instanceId;
            Assert.IsFalse(RuneRules.CanRelease(a, s, id, true, out string inCombat));
            StringAssert.Contains("combat", inCombat);
            Assert.IsTrue(RuneRules.CanRelease(a, s, id, false, out _));
            a.allowRelease = false;
            Assert.IsFalse(RuneRules.CanRelease(a, s, id, false, out _), "release can be forbidden");
            a.allowRelease = true;
            RuneInstance gone = RuneRules.Release(s, id);
            Assert.AreEqual(400f, gone.energy, Eps);
            Assert.AreEqual(0, s.runes.Count, "its energy is gone with it");
            Assert.IsNull(RuneRules.Release(s, id));
            Assert.IsFalse(RuneRules.CanRelease(a, s, id, false, out _));
        }

        // ------------------------------------------------------------------ concept knowledge (decision D13)
        [Test]
        public void ConceptKnowledgeBelongsToThePlayerNotTheRune()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, ("wind", 1, 0f));
            Assert.IsTrue(RuneRules.Learn(s, "wind", "wind.pressure"));
            Assert.IsFalse(RuneRules.Learn(s, "wind", "wind.pressure"), "once");
            RuneRules.Release(s, s.runes[0].instanceId);
            Assert.IsTrue(RuneRules.Knows(s, "wind", "wind.pressure"), "released and re-absorbed later: still known");
            Assert.IsFalse(RuneRules.Knows(s, "wind", "wind.vortex"));
            Assert.AreEqual(1, RuneRules.KnownConcepts(s, "wind").Count);
            Assert.AreEqual(0, RuneRules.KnownConcepts(s, "fire").Count);

            RuneSoulState back = RuneSoulState.FromJson(s.ToJson());
            Assert.IsTrue(RuneRules.Knows(back, "wind", "wind.pressure"), "saved");
        }

        // ------------------------------------------------------------------ saved state and items
        [Test]
        public void RunesAndPendingEnergyAreSaved()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, ("fire", 2, 321f), ("wind", 1, 0f));
            s.runes[0].history.Add("synthesis-1");
            s.runes[0].flawed = true;
            s.runes[0].flaws.Add("unstable");
            s.pendingEnergy = 77f;
            RuneSoulState back = RuneSoulState.FromJson(s.ToJson());
            Assert.AreEqual(2, back.runes.Count);
            Assert.AreEqual("fire", back.runes[0].runeId);
            Assert.AreEqual(2, back.runes[0].rank);
            Assert.AreEqual(321f, back.runes[0].energy, Eps);
            Assert.AreEqual("synthesis-1", back.runes[0].history[0]);
            Assert.IsTrue(back.runes[0].flawed);
            Assert.AreEqual(77f, back.pendingEnergy, Eps);
            Assert.AreEqual(s.runes[1].instanceId, back.runes[1].instanceId);

            RuneSoulState broken = RuneSoulState.FromJson("{\"runes\":[{\"runeId\":\"fire\",\"rank\":0,\"quality\":500,\"energy\":-4}],\"pendingEnergy\":-9}");
            Assert.AreEqual(1, broken.runes[0].rank);
            Assert.AreEqual(100f, broken.runes[0].quality, Eps);
            Assert.AreEqual(0f, broken.runes[0].energy, Eps);
            Assert.AreEqual(0f, broken.pendingEnergy, Eps);
        }

        [Test]
        public void RuneItemIdsCarryTheRune()
        {
            var d = new RuneDrop { runeId = "fire", rank = 2, quality = 41.6f, energy = 50.4f };
            string id = RuneItems.IdOf(d);
            Assert.AreEqual("rune:fire:2:42:50", id);
            Assert.IsTrue(RuneItems.TryParse(id, out RuneDrop back));
            Assert.AreEqual("fire", back.runeId);
            Assert.AreEqual(2, back.rank);
            Assert.AreEqual(42f, back.quality, Eps);
            Assert.AreEqual(50f, back.energy, Eps);
            foreach (string bad in new[] { null, "", "potion:x", "rune:", "rune:fire:1:42", "rune:fire:0:42:0", "rune:fire:1:101:0", "rune:fire:1:x:0", "rune::1:1:1" })
                Assert.IsFalse(RuneItems.TryParse(bad, out _), bad ?? "null");
            Assert.IsTrue(RuneItems.IsRuneId(id));
        }

        [Test]
        public void TheRuneListViewRoundTrips()
        {
            RuneCatalog c = Catalog();
            RuneSoulState s = SoulWith(c, ("fire", 1, 10f));
            RuneRules.Learn(s, "fire", "fire.combustion");
            var v = new RuneListView { version = 4 };
            foreach (RuneInstance r in s.runes) v.runes.Add(r.Clone());
            v.knowledge.Add(new ConceptKnowledge { runeId = "fire", concepts = new List<string> { "fire.combustion" } });
            RuneListView back = UnityEngine.JsonUtility.FromJson<RuneListView>(UnityEngine.JsonUtility.ToJson(v));
            Assert.AreEqual(4, back.version);
            Assert.AreEqual(s.runes[0].instanceId, back.runes[0].instanceId);
            Assert.AreEqual("fire.combustion", back.knowledge[0].concepts[0]);
            RuneInstance clone = s.runes[0].Clone();
            clone.history.Add("x");
            Assert.AreEqual(0, s.runes[0].history.Count, "a clone is independent");
        }
    }
}
#endif
