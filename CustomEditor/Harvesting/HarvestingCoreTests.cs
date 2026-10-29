#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static HarvestBuild;

namespace HarvestingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window ▸ General ▸ Test Runner) of the harvesting rules on the built-in creatures: the content is
    /// valid, hits mark bodies by their form and element (slashes cut hides and tendons, points reach organs, blunt blows
    /// break bones, fire burns hides, poison spoils meat, explosions spread), carcasses spoil with temperature, the
    /// requirements and availability, the resolver (fight and harvest damage kept apart, skill and freshness and tools),
    /// skills and techniques, item values and rare finds. Plain rules: no scene, no assets.
    /// </summary>
    public class HarvestingCoreTests
    {
        private static HarvestSettings Settings() => HarvestSettings.Defaults();

        private static HarvestHit Hit(HarvestAnatomyData a, string region, DamageForm form, float severity, ElementType element = ElementType.None,
            bool area = false, bool periodic = false) =>
            new HarvestHit { region = a.RegionIndex(region), form = form, element = element, severity = severity, area = area, periodic = periodic, u = -1f, v = -1f, source = "test" };

        private static HarvestStageResult Stage(HarvestStageKind kind, float precision, float damage = 0f, float completeness = 1f, int unit = -1, float tool = 0.9f) =>
            new HarvestStageResult { kind = kind, precision = precision, damage = damage, completeness = completeness, unit = unit, toolFitness = tool, seconds = 5f };

        private static HarvestTaskResult Task(string material, params HarvestStageResult[] stages) =>
            new HarvestTaskResult { material = material, stages = stages.ToList() };

        /// <summary>A perfect peel of every hide cell.</summary>
        private static HarvestStageResult CleanPeel(HarvestCondition c, float damage = 0f, float flesh = 0.1f)
        {
            var s = Stage(HarvestStageKind.Peel, 0.95f, damage);
            s.cellDamage = c.pelt.Select(_ => damage).ToList();
            s.cellFlesh = c.pelt.Select(_ => flesh).ToList();
            s.lostCells = new List<int>();
            return s;
        }

        private static (HarvestAnatomyData a, HarvestCarcass c) Fresh(string id, int seed = 7)
        {
            HarvestAnatomyData a = HarvestDefaults.Anatomy(id);
            HarvestCondition k = HarvestCondition.Create(a, seed);
            return (a, HarvestCarcass.From(a, k));
        }

        private static HarvestUnit Unit(HarvestAnatomyData a, HarvestCondition c, string material, int index = 0) =>
            c.units.First(u => a.materials[u.material].id == material && u.index == index);

        // ------------------------------------------------------------------ content
        [Test]
        public void BuiltInAnatomiesAreValid()
        {
            List<HarvestAnatomyData> all = HarvestDefaults.Anatomies();
            Assert.GreaterOrEqual(all.Count, 12);
            Assert.AreEqual(all.Count, all.Select(x => x.id).Distinct().Count(), "unique ids");
            foreach (HarvestAnatomyData a in all)
            {
                var errors = new List<string>();
                var warnings = new List<string>();
                a.Validate(errors, warnings);
                Assert.IsEmpty(errors, a.id + ": " + string.Join(" | ", errors));
                Assert.IsEmpty(warnings, a.id + ": " + string.Join(" | ", warnings));
                foreach (HarvestRegion r in a.regions)
                    Assert.IsNotEmpty(HarvestDiagram.Shape(a.plan, r.Slot), $"{a.id}/{r.id} has a drawing");
            }
        }

        [Test]
        public void UnlocatedHitsLandOnTheMeatiestPlainPart()
        {
            // A blast or a hit with no place: not a weak point (its multiplier would ride on every blast).
            foreach (string id in new[] { "bear", "wolf", "deer", "boar", "rabbit", "sheep", "drake" })
            {
                HarvestAnatomyData a = HarvestDefaults.Anatomy(id);
                Assert.AreEqual("haunch", a.regions[a.DefaultRegionIndex].id, id);
            }
            foreach (HarvestAnatomyData a in HarvestDefaults.Anatomies())
                Assert.IsFalse(a.regions[a.DefaultRegionIndex].weakPoint, a.id);
        }

        [Test]
        public void APartOnBothSidesGoesToTheSideHit()
        {
            HarvestAnatomyData t = HarvestDefaults.Troll();
            Assert.AreEqual("leg_r", t.regions[t.RegionOfPart("Legs", 0.2f, 0.4f, 0.5f)].id);
            Assert.AreEqual("leg_l", t.regions[t.RegionOfPart("Legs", 0.2f, -0.4f, 0.5f)].id);
            Assert.AreEqual("arm_r", t.regions[t.RegionOfPart("Arms", 0.6f, 0.6f, 0.5f)].id);
            Assert.AreEqual("arm_l", t.regions[t.RegionOfPart("Arms", 0.6f, -0.6f, 0.5f)].id);
            HarvestAnatomyData w = HarvestDefaults.Wolf();
            Assert.AreEqual("hindleg_r", w.regions[w.RegionOfPart("Legs", 0.2f, 0.5f, 0.15f)].id);
            Assert.AreEqual("foreleg_l", w.regions[w.RegionOfPart("Legs", 0.2f, -0.5f, 0.7f)].id);
            Assert.AreEqual(t.RegionOfPart("Head"), t.RegionOfPart("Head", 0.95f, 0f, 0.5f), "a part on one region is that region");
            Assert.AreEqual(-1, t.RegionOfPart("Tentacle", 0.5f, 0f, 0.5f));
        }

        [Test]
        public void TimesRoundBeforeTheyAreSplit()
        {
            Assert.AreEqual("30 s", HarvestText.Minutes(0.5));
            Assert.AreEqual("1 min", HarvestText.Minutes(0.995));
            Assert.AreEqual("59 min", HarvestText.Minutes(59.4));
            Assert.AreEqual("1 h 00", HarvestText.Minutes(59.7));
            Assert.AreEqual("1 h 05", HarvestText.Minutes(65.2));
            Assert.AreEqual("2 h 00", HarvestText.Minutes(119.7));
            Assert.AreEqual("never", HarvestText.Minutes(double.PositiveInfinity));
        }

        [Test]
        public void MobTypesMatchWholeWords()
        {
            HarvestAnatomyData bear = HarvestDefaults.Bear();
            Assert.IsTrue(bear.MatchesMob("Bear"));
            Assert.IsTrue(bear.MatchesMob("", "Black Bear (Clone)"));
            Assert.IsTrue(bear.MatchesMob("bears"));
            Assert.IsFalse(bear.MatchesMob("Bearded Vulture"));
            Assert.IsTrue(HarvestDefaults.Wolf().MatchesMob("Fox"));
        }

        [Test]
        public void ConditionIsRepeatableAndLaidOut()
        {
            HarvestAnatomyData a = HarvestDefaults.Bear();
            string one = UnityEngine.JsonUtility.ToJson(HarvestCondition.Create(a, 42));
            string two = UnityEngine.JsonUtility.ToJson(HarvestCondition.Create(a, 42));
            Assert.AreEqual(one, two, "same seed, same creature");
            HarvestCondition c = HarvestCondition.Create(a, 42);
            Assert.AreEqual(16 * 12, c.pelt.Count);
            int present = c.pelt.Count(p => p.present);
            Assert.Greater(present, 100, "most of the pelt is hide");
            Assert.IsTrue(c.pelt.Where(p => p.present).All(p => p.region >= 0 && a.regions[p.region].hide > 0f));
            Assert.AreEqual(20, c.units.Count(u => a.materials[u.material].id == "claws"), "5 claws a paw");
            Assert.IsTrue(c.pelt.Any(p => p.present && p.difficult), "paws and face are fiddly");
            HarvestCondition spider = HarvestCondition.Create(HarvestDefaults.GiantSpider(), 1);
            Assert.IsFalse(spider.pelt.Any(p => p.present), "no hide on a spider");
        }

        // ------------------------------------------------------------------ the fight
        [Test]
        public void SlashCutsHideAndTendonsNotOrgans()
        {
            HarvestAnatomyData a = HarvestDefaults.Bear();
            HarvestCondition c = HarvestCondition.Create(a, 3);
            HarvestSettings s = Settings();
            c.ApplyHit(a, s, Hit(a, "back", DamageForm.Slash, 0.6f));
            int back = a.RegionIndex("back");
            Assert.Less(c.HideIntegrity(back), 0.95, "the hide is cut");
            Assert.IsTrue((c.HideMarks(back) & HarvestMarks.Cut) != 0);
            Assert.Less(Unit(a, c, "back_sinew").integrity, 1f, "the back sinew is cut");
            Assert.IsTrue(c.units.Where(u => a.materials[u.material].method == HarvestMethod.Organs).All(u => u.integrity >= 0.999f), "no organ in the back");
            Assert.AreEqual(1, c.wounds.Count);
        }

        [Test]
        public void PierceReachesOrgansLeavesSmallHoles()
        {
            HarvestAnatomyData a = HarvestDefaults.Deer();
            HarvestCondition c = HarvestCondition.Create(a, 5);
            HarvestSettings s = Settings();
            c.ApplyHit(a, s, Hit(a, "ribs", DamageForm.Pierce, 0.8f));
            int ribs = a.RegionIndex("ribs");
            float worstOrgan = c.units.Where(u => u.region == ribs && a.materials[u.material].method == HarvestMethod.Organs).Min(u => u.integrity);
            Assert.Less(worstOrgan, 0.5f, "an arrow through the chest ruins the heart or the lungs");
            int holed = c.pelt.Count(p => p.present && p.damage > 0.5f);
            Assert.That(holed, Is.InRange(1, 3), "a hole and an exit hole");
            Assert.That(c.wounds[0].effect, Does.Contain("pierced").Or.Contain("heart").Or.Contain("lungs"));
        }

        [Test]
        public void BluntBreaksBonesAndTrophiesNotHide()
        {
            HarvestAnatomyData a = HarvestDefaults.Bear();
            HarvestCondition c = HarvestCondition.Create(a, 9);
            HarvestSettings s = Settings();
            c.ApplyHit(a, s, Hit(a, "head", DamageForm.Blunt, 0.9f));
            HarvestUnit skull = Unit(a, c, "skull");
            Assert.Less(skull.integrity, 0.6f);
            Assert.IsTrue((skull.marks & HarvestMarks.Fractured) != 0, "the skull is fractured");
            Assert.IsTrue(c.units.Any(u => a.materials[u.material].id == "fangs" && u.integrity < 1f), "a fang cracks");
            Assert.Greater(c.HideIntegrity(a.RegionIndex("head")), 0.8, "blunt blows hardly break the hide");
        }

        [Test]
        public void FireBurnsTheHideAndDrakesShrugItOff()
        {
            HarvestSettings s = Settings();
            HarvestAnatomyData bear = HarvestDefaults.Bear();
            HarvestCondition b = HarvestCondition.Create(bear, 1);
            b.ApplyHit(bear, s, Hit(bear, "back", DamageForm.None, 0.8f, ElementType.Fire));
            Assert.Less(b.HideIntegrity(), 0.97);
            Assert.IsTrue((b.HideMarks() & HarvestMarks.Burned) != 0);
            Assert.Greater(b.regions[bear.RegionIndex("back")].burn, 0f, "the meat under it is charred");

            HarvestAnatomyData drake = HarvestDefaults.Drake();
            HarvestCondition d = HarvestCondition.Create(drake, 1);
            d.ApplyHit(drake, s, Hit(drake, "back", DamageForm.None, 0.8f, ElementType.Fire));
            Assert.Greater(d.HideIntegrity(), b.HideIntegrity(), "fire-proof scales");
        }

        [Test]
        public void PoisonSpoilsTheMeatEverywhere()
        {
            HarvestAnatomyData a = HarvestDefaults.Boar();
            HarvestCondition c = HarvestCondition.Create(a, 2);
            HarvestSettings s = Settings();
            for (int i = 0; i < 6; i++)
                c.ApplyHit(a, s, Hit(a, "back", DamageForm.None, 0.5f, ElementType.Poison, periodic: true));
            Assert.IsTrue(c.regions.All(r => r.poison > 0f), "through the blood");
            Assert.AreEqual(1, c.wounds.Count, "ticks add up in one wound");
            var carcass = HarvestCarcass.From(a, c);
            carcass.hideOff = true;
            HarvestOutcome o = HarvestResolver.Resolve(a, s, carcass, Task("haunch", Stage(HarvestStageKind.Trace, 0.9f)), new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.IsTrue(o.pieces.All(p => (p.marks & HarvestMarks.Poisoned) != 0 && p.quality <= 0.25f), "poisoned meat is poor at best");
        }

        [Test]
        public void PoisonOverTimeReachesTheOrgans()
        {
            // Many small ticks (a poisoned blade's damage over time), none of them enough alone.
            HarvestAnatomyData a = HarvestDefaults.Bear();
            HarvestCondition c = HarvestCondition.Create(a, 3);
            HarvestSettings s = Settings();
            for (int i = 0; i < 12; i++)
                c.ApplyHit(a, s, Hit(a, "back", DamageForm.None, 0.1f, ElementType.Poison, periodic: true));
            Assert.IsTrue(c.units.Where(u => a.materials[u.material].method == HarvestMethod.Organs).All(u => (u.marks & HarvestMarks.Poisoned) != 0),
                "the blood carries it to every organ");
            HarvestOutcome o = HarvestResolver.Resolve(a, s, HarvestCarcass.From(a, c),
                Task("liver", Stage(HarvestStageKind.Steady, 0.9f), Stage(HarvestStageKind.Pull, 0.9f)), new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.IsNotEmpty(o.pieces);
            Assert.IsTrue(o.pieces.All(p => (p.marks & HarvestMarks.Poisoned) != 0 && p.quality <= 0.25f), "a poisoned liver is poor at best");
        }

        [Test]
        public void DarkMagicTaintsWithoutSoiling()
        {
            HarvestAnatomyData a = HarvestDefaults.Deer();
            HarvestCondition c = HarvestCondition.Create(a, 4);
            HarvestSettings s = Settings();
            c.ApplyHit(a, s, Hit(a, "haunch", DamageForm.None, 0.8f, ElementType.Dark));
            HarvestRegionState haunch = c.regions[a.RegionIndex("haunch")];
            Assert.Greater(haunch.taint, 0.12f);
            Assert.AreEqual(0f, haunch.contamination, "no gut was torn");
            var carcass = HarvestCarcass.From(a, c);
            Assert.IsFalse(carcass.Soiled, "taint does not make the meat spoil faster");
            carcass.hideOff = true;
            HarvestOutcome o = HarvestResolver.Resolve(a, s, carcass, Task("haunch", Stage(HarvestStageKind.Trace, 0.9f)), new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.IsNotEmpty(o.pieces);
            Assert.IsTrue(o.pieces.All(p => (p.marks & HarvestMarks.Tainted) != 0 && (p.marks & HarvestMarks.GutShot) == 0), "tainted, not gut-shot");
        }

        [Test]
        public void AnArrowThroughTheChestLeavesByTheChest()
        {
            // The exit hole is half way round the body, not on an arm or a wing flap at the end of the row.
            HarvestSettings s = Settings();
            foreach ((string id, string region) in new[] { ("troll", "chest"), ("eagle", "breast"), ("bear", "back"), ("bear", "haunch"), ("deer", "shoulder") })
            {
                HarvestAnatomyData a = HarvestDefaults.Anatomy(id);
                foreach (float u in new[] { 0.2f, 0.5f, 0.8f })
                foreach (float v in new[] { 0.05f, 0.5f, 0.95f })
                {
                    HarvestCondition c = HarvestCondition.Create(a, 5);
                    HarvestHit hit = Hit(a, region, DamageForm.Pierce, 2f);
                    hit.u = u;
                    hit.v = v;
                    c.ApplyHit(a, s, hit);
                    foreach (HarvestPeltCell cell in c.pelt.Where(x => x.damage > 0f))
                        Assert.AreEqual(BodySide.Any, a.regions[cell.region].side, $"{id} {region} ({u}, {v}): a hole in the {a.regions[cell.region].id}");
                }
            }
        }

        [Test]
        public void AHealedTrollBoneIsNoLongerBroken()
        {
            HarvestAnatomyData a = HarvestDefaults.Troll();
            HarvestSettings s = Settings();
            for (int seed = 1; seed < 40; seed++)
            {
                HarvestCondition c = HarvestCondition.Create(a, seed);
                c.ApplyHit(a, s, Hit(a, "leg_l", DamageForm.Blunt, 0.9f));
                HarvestUnit bone = c.units.FirstOrDefault(u => (u.marks & HarvestMarks.Fractured) != 0);
                if (bone == null) continue;
                c.Regenerate(a, 30.0);
                Assert.AreEqual(1f, bone.integrity, 1e-3f);
                Assert.AreEqual(HarvestMarks.None, bone.marks & HarvestMarks.Fractured, "the bone has knit");
                return;
            }
            Assert.Fail("no blow broke a bone");
        }

        [Test]
        public void ExplosionsSpreadAndDestroyMeat()
        {
            HarvestAnatomyData a = HarvestDefaults.Wolf();
            HarvestCondition c = HarvestCondition.Create(a, 4);
            HarvestSettings s = Settings();
            c.ApplyHit(a, s, Hit(a, "ribs", DamageForm.Crush, 1.2f, ElementType.Fire, area: true));
            Assert.IsTrue(c.regions.All(r => (r.marks & HarvestMarks.Blasted) != 0));
            Assert.IsTrue(c.regions.Where((r, i) => a.regions[i].meat > 0.5f).All(r => r.meat < 1f), "meat everywhere is hurt");
            Assert.Less(c.regions[a.RegionIndex("ribs")].meat, 0.5f, "and ruined where it struck");
        }

        [Test]
        public void GutShotSoilsTheBelly()
        {
            HarvestAnatomyData a = HarvestDefaults.Deer();
            HarvestSettings s = Settings();
            bool soiled = false;
            for (int seed = 1; seed < 40 && !soiled; seed++)
            {
                HarvestCondition c = HarvestCondition.Create(a, seed);
                c.ApplyHit(a, s, Hit(a, "belly", DamageForm.Pierce, 1.2f));
                HarvestRegionState belly = c.regions[a.RegionIndex("belly")];
                if ((belly.marks & HarvestMarks.GutShot) == 0) continue;
                soiled = true;
                Assert.Greater(belly.contamination, 0.3f);
                Assert.IsTrue(c.units.Where(u => u.region == a.RegionIndex("belly") && a.materials[u.material].method == HarvestMethod.Organs && !a.materials[u.material].hazard)
                    .All(u => (u.marks & HarvestMarks.GutShot) != 0), "the liver and kidneys are soiled too");
            }
            Assert.IsTrue(soiled, "some arrow through the belly tears a gut");
        }

        [Test]
        public void TrollsRegenerateUnlessBurned()
        {
            HarvestAnatomyData a = HarvestDefaults.Troll();
            HarvestCondition c = HarvestCondition.Create(a, 6);
            HarvestSettings s = Settings();
            c.ApplyHit(a, s, Hit(a, "leg_l", DamageForm.Slash, 1f));
            c.ApplyHit(a, s, Hit(a, "chest", DamageForm.None, 1f, ElementType.Fire));
            float leg = c.regions[a.RegionIndex("leg_l")].meat, chest = c.regions[a.RegionIndex("chest")].meat;
            c.Regenerate(a, 2.0);
            Assert.Greater(c.regions[a.RegionIndex("leg_l")].meat, leg, "the cut closes");
            Assert.AreEqual(chest, c.regions[a.RegionIndex("chest")].meat, 1e-4, "the burn does not");
        }

        // ------------------------------------------------------------------ the carcass
        [Test]
        public void TheHideComesFirst()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear");
            HarvestSettings s = Settings();
            var skills = new HarvestSkillData();
            int haunch = a.MaterialIndex("haunch");
            Assert.AreEqual(HarvestAvailability.NeedsHide, c.Availability(a, s, haunch, skills, 1, out _));
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(a, s, a.MaterialIndex("claws"), skills, 1, out _), "claws come off any time");
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(a, s, a.MaterialIndex("heart"), skills, 1, out _), "opening the body is part of the organ work");
            HarvestOutcome o = HarvestResolver.Resolve(a, s, c, new HarvestTaskResult { material = "haunch", rough = true }, skills);
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.IsTrue(c.hideOff);
            Assert.AreEqual(HarvestAvailability.Taken, c.Availability(a, s, a.MaterialIndex("pelt"), skills, 1, out _), "the hide is ruined");
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(a, s, haunch, skills, 1, out _));
            Assert.IsTrue(Fresh("giant_spider").c.hideOff, "nothing to skin on a spider");
        }

        [Test]
        public void NoOneHacksOffAHideSomeoneIsSkinning()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear");
            HarvestSettings s = Settings();
            var skills = new HarvestSkillData();
            int pelt = a.MaterialIndex("pelt");
            Assert.IsTrue(c.Lock(pelt, 2, 600));
            HarvestOutcome o = HarvestResolver.Resolve(a, s, c, new HarvestTaskResult { material = "haunch", rough = true }, skills, 1);
            Assert.IsFalse(o.accepted, "player 2 is skinning it");
            Assert.IsFalse(c.hideOff);
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(a, s, pelt, skills, 2, out _), "the skinner's work is safe");
            // The skinner may give up and hack it off.
            HarvestOutcome own = HarvestResolver.Resolve(a, s, c, new HarvestTaskResult { material = "haunch", rough = true }, skills, 2);
            Assert.IsTrue(own.accepted, own.refusal);
            Assert.IsTrue(c.hideOff);
            Assert.AreEqual(0, c.LockedBy(pelt), "and the lock goes with the hide");
        }

        [Test]
        public void NothingToOpenTheBodyForIsNoBar()
        {
            // A material that needs the body opened waits for an organ to be taken - unless no organ can be any more.
            HarvestSettings s = Settings();
            var skills = new HarvestSkillData();
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer");
            a.Material("femur").requires.Add(HarvestMethods.Cavity);
            c.hideOff = true;
            int femur = a.MaterialIndex("femur");
            Assert.AreEqual(HarvestAvailability.Blocked, c.Availability(a, s, femur, skills, 1, out string why));
            StringAssert.Contains("Open the body", why);
            c.Tick(s, 3600, 20f);
            Assert.IsTrue(Enumerable.Range(0, a.materials.Count).Where(i => a.materials[i].method == HarvestMethod.Organs).All(i => c.Unobtainable(a, s, i)),
                "an hour at 20 °C spoils every organ");
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(a, s, femur, skills, 1, out why), why);
        }

        [Test]
        public void ASloppyOpeningCutTearsNoOrgan()
        {
            // Hacking through the belly wall is not tearing the stomach (slips into the gut are spills).
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 6);
            HarvestUnit stomach = Unit(a, c.condition, "stomach");
            HarvestOutcome o = HarvestResolver.Resolve(a, Settings(), c, Task("stomach", Stage(HarvestStageKind.Trace, 0.3f, 0.6f),
                Stage(HarvestStageKind.Steady, 0.9f, 0.15f, unit: stomach.index), Stage(HarvestStageKind.Pull, 0.9f, 0.15f, unit: stomach.index)), new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.AreEqual(0f, c.condition.regions[stomach.region].contamination, "nothing soiled");
            Assert.IsFalse(c.Soiled);
        }

        /// <summary>A griffon as doc 05 builds it: a pelt, and flight feathers on wings that have no hide of their own.</summary>
        private static HarvestAnatomyData Griffon()
        {
            var g = new HarvestAnatomyData { id = "griffon", displayName = "Griffon", plan = HarvestBodyPlan.Quadruped, peltSize = new Vector2Int(12, 16) };
            g.regions = HarvestDefaults.QuadrupedRegions(wings: true);
            g.materials.Add(Material("pelt", "Griffon pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "pelt").Tannery("bear"));
            g.materials.Add(Material("feathers", "Flight feathers", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers")
                .From("wing_l").From("wing_r").Yield(20f, 40f, "feather"));
            g.materials.Add(Material("meat", "Griffon meat", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("haunch").Yield(2f, 3f).Needs(HarvestMethods.Hide));
            return g;
        }

        [Test]
        public void FeathersAreJudgedAlikeInTheWindowAndTheHand()
        {
            HarvestSettings s = Settings();
            var skills = new HarvestSkillData();
            // A griffon's wings grow only feathers (no hide cells of their own): fireballs still burn them, and blades cut them.
            HarvestAnatomyData g = Griffon();
            HarvestCondition k = HarvestCondition.Create(g, 3);
            k.ApplyHit(g, s, Hit(g, "back", DamageForm.None, 1f, ElementType.Fire, area: true));
            k.ApplyHit(g, s, Hit(g, "back", DamageForm.None, 1f, ElementType.Fire, area: true));
            var c = HarvestCarcass.From(g, k);
            int feathers = g.MaterialIndex("feathers");
            double shown = c.CombatIntegrity(g, feathers);
            Assert.Less(shown, 0.75, "burned wings");
            Assert.AreNotEqual(HarvestMarks.None, c.CombatMarks(g, feathers) & HarvestMarks.Burned);
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(g, s, feathers, skills, 1, out string why), why);
            HarvestOutcome o = HarvestResolver.Resolve(g, s, c, Task("feathers", Stage(HarvestStageKind.Scrape, 0.95f)), skills);
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.IsNotEmpty(o.pieces);
            Assert.IsTrue(o.pieces.All(p => p.quality < shown + 0.1 && (p.marks & HarvestMarks.Burned) != 0), "the feathers come out as burned as the window said");
            HarvestCondition cut = HarvestCondition.Create(g, 3);
            for (int i = 0; i < 4; i++) cut.ApplyHit(g, s, Hit(g, "wing_l", DamageForm.Slash, 1f));
            HarvestCarcass slashed = HarvestCarcass.From(g, cut);
            Assert.Less(slashed.CombatIntegrity(g, feathers), 0.75, "a slashed wing's feathers are cut");
            Assert.AreNotEqual(HarvestMarks.None, slashed.CombatMarks(g, feathers) & HarvestMarks.Cut);
            // Marked by what hurt them, as hide cells are: an arrow pierces them, a blunt blast does not touch them.
            HarvestCondition shot = HarvestCondition.Create(g, 3);
            shot.ApplyHit(g, s, Hit(g, "wing_l", DamageForm.Pierce, 0.1f));
            shot.ApplyHit(g, s, Hit(g, "back", DamageForm.Blunt, 1f, area: true));
            HarvestCarcass arrow = HarvestCarcass.From(g, shot);
            Assert.Less(arrow.CombatIntegrity(g, feathers), 1.0);
            Assert.AreEqual(HarvestMarks.Pierced, arrow.CombatMarks(g, feathers) & (HarvestMarks.Pierced | HarvestMarks.Blasted));
            // Burning over time reaches them as it does the griffon's own hide: cell by cell, as often as any of its cells -
            // a small hide (3 × 4, ten cells) as well as a large one.
            foreach ((Vector2Int size, int ticks) in new[] { (new Vector2Int(12, 16), 60), (new Vector2Int(3, 4), 8) })
            {
                HarvestAnatomyData b = Griffon();
                b.peltSize = size;
                double feathersBurnt = 0, peltBurnt = 0;
                for (int seed = 1; seed <= 200; seed++)
                {
                    HarvestCondition burning = HarvestCondition.Create(b, seed);
                    for (int i = 0; i < ticks; i++) burning.ApplyHit(b, s, Hit(b, "back", DamageForm.None, 0.3f, ElementType.Fire, periodic: true));
                    HarvestCarcass burnt = HarvestCarcass.From(b, burning);
                    feathersBurnt += burnt.CombatIntegrity(b, feathers) / 200;
                    peltBurnt += burnt.CombatIntegrity(b, b.MaterialIndex("pelt")) / 200;
                }
                Assert.Less(feathersBurnt, 0.97, $"a burning griffon's feathers singe ({size})");
                Assert.AreEqual(peltBurnt, feathersBurnt, 0.04, $"as its pelt does ({size})");
            }

            // The eagle's feathers are on its wings and tail: hits on its breast leave them be, hits on a wing do not.
            HarvestAnatomyData eagle = HarvestDefaults.Eagle();
            int plumage = eagle.MaterialIndex("feathers");
            HarvestCondition breast = HarvestCondition.Create(eagle, 7), wing = HarvestCondition.Create(eagle, 7);
            for (int i = 0; i < 4; i++)
            {
                breast.ApplyHit(eagle, s, Hit(eagle, "breast", DamageForm.Pierce, 1f));
                wing.ApplyHit(eagle, s, Hit(eagle, "wing_l", DamageForm.Pierce, 1f));
            }
            Assert.AreEqual(1.0, HarvestCarcass.From(eagle, breast).CombatIntegrity(eagle, plumage), 1e-6);
            HarvestCarcass hurt = HarvestCarcass.From(eagle, wing);
            Assert.Less(hurt.CombatIntegrity(eagle, plumage), 1.0);
            Assert.AreNotEqual(HarvestMarks.None, hurt.CombatMarks(eagle, plumage) & HarvestMarks.Pierced);
        }

        [Test]
        public void AGriffonsWingFaresAsAnEaglesWingDoes()
        {
            // A griffon's wing has no hide cells: its feathers grow on a small hide of its own, 2 × 2 cells, marked blow for
            // blow as the eagle's wing (2 × 2 cells of its hide) is - on average over many creatures, the same.
            HarvestSettings s = Settings();
            HarvestAnatomyData g = Griffon(), eagle = HarvestDefaults.Eagle();
            int gw = g.RegionIndex("wing_l"), ew = eagle.RegionIndex("wing_l");
            Assert.AreEqual(4, HarvestCondition.Create(eagle, 1).CellsOf(ew).Count, "the eagle's wing is 2 × 2 cells of its hide");
            var blows = new (DamageForm form, ElementType element, float severity, int times)[]
            {
                (DamageForm.Slash, ElementType.None, 0.2f, 1), (DamageForm.Slash, ElementType.None, 1f, 4), (DamageForm.Claw, ElementType.None, 0.8f, 2),
                (DamageForm.Pierce, ElementType.None, 0.5f, 3), (DamageForm.Bite, ElementType.None, 0.4f, 2), (DamageForm.Crush, ElementType.None, 1f, 1),
                (DamageForm.None, ElementType.Fire, 0.6f, 2), (DamageForm.None, ElementType.Lightning, 0.1f, 3),
            };
            foreach (var b in blows)
            {
                double griffon = 0, bird = 0;
                HarvestMarks gm = HarvestMarks.None, em = HarvestMarks.None;
                for (int seed = 1; seed <= 200; seed++)
                {
                    HarvestCondition k = HarvestCondition.Create(g, seed), e = HarvestCondition.Create(eagle, seed);
                    for (int i = 0; i < b.times; i++)
                    {
                        k.ApplyHit(g, s, Hit(g, "wing_l", b.form, b.severity, b.element));
                        e.ApplyHit(eagle, s, Hit(eagle, "wing_l", b.form, b.severity, b.element));
                    }
                    griffon += HarvestCarcass.From(g, k).RegionHideCondition(gw) / 200;
                    bird += e.HideIntegrity(ew) / 200;
                    gm |= k.regions[gw].CoverMarks();
                    em |= e.HideMarks(ew);
                }
                string what = $"{b.form} {b.element} {b.severity} ×{b.times}";
                Assert.Less(griffon, 1.0, what);
                Assert.AreEqual(bird, griffon, 0.04, what);
                Assert.AreEqual(em, gm, what);
            }
        }

        [Test]
        public void PluckingAGriffonLeavesItsPeltOn()
        {
            HarvestSettings s = Settings();
            HarvestAnatomyData g = Griffon();
            var errors = new List<string>();
            var warnings = new List<string>();
            g.Validate(errors, warnings);
            Assert.IsEmpty(errors, string.Join(" | ", errors));
            Assert.IsFalse(warnings.Any(w => w.Contains("more than one hide")), "feathers are not a second pelt");

            HarvestCarcass c = HarvestCarcass.From(g, HarvestCondition.Create(g, 3));
            var skills = new HarvestSkillData();
            HarvestOutcome o = HarvestResolver.Resolve(g, s, c, Task("feathers", Stage(HarvestStageKind.Scrape, 0.9f)), skills);
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.IsFalse(c.hideOff, "the pelt is still on");
            Assert.AreEqual(HarvestAvailability.NeedsHide, c.Availability(g, s, g.MaterialIndex("meat"), skills, 1, out _));
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(g, s, g.MaterialIndex("pelt"), skills, 1, out _));
            Assert.IsTrue(c.condition.pelt.Where(x => x.present).All(x => !x.peeled && x.flesh == 0f), "plucking peels nothing");

            // Nobody hacks the pelt off while someone plucks it - and hacking it off leaves the wings' feathers be.
            HarvestCarcass d = HarvestCarcass.From(g, HarvestCondition.Create(g, 3));
            Assert.IsTrue(d.Lock(g.MaterialIndex("feathers"), 2, 600));
            Assert.IsFalse(HarvestResolver.Resolve(g, s, d, new HarvestTaskResult { material = "meat", rough = true }, skills, 1).accepted, "player 2 is plucking it");
            d.Unlock(g.MaterialIndex("feathers"), 2);
            Assert.IsTrue(HarvestResolver.Resolve(g, s, d, new HarvestTaskResult { material = "meat", rough = true }, skills, 1).accepted);
            HarvestOutcome feathers = HarvestResolver.Resolve(g, s, d, Task("feathers", Stage(HarvestStageKind.Scrape, 0.9f)), skills);
            Assert.IsTrue(feathers.accepted, feathers.refusal);
            Assert.Greater(feathers.Count, 0, "the wings are not the pelt");

            // Nor are feathers on parts the pelt has cells on (an owlbear's head and neck): hacking the pelt off leaves them on.
            var owlbear = new HarvestAnatomyData { id = "owlbear", displayName = "Owlbear", plan = HarvestBodyPlan.Quadruped, peltSize = new Vector2Int(12, 16) };
            owlbear.regions = HarvestDefaults.QuadrupedRegions();
            owlbear.materials.Add(Material("pelt", "Owlbear pelt", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "pelt"));
            owlbear.materials.Add(Material("feathers", "Owlbear feathers", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers")
                .From("head").From("neck").Yield(20f, 40f, "feather"));
            owlbear.materials.Add(Material("meat", "Owlbear meat", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("haunch").Yield(2f, 3f).Needs(HarvestMethods.Hide));
            HarvestCarcass ob = HarvestCarcass.From(owlbear, HarvestCondition.Create(owlbear, 3));
            Assert.IsTrue(ob.condition.HasHideCells(owlbear.RegionIndex("head")) && ob.condition.HasHideCells(owlbear.RegionIndex("neck")));
            Assert.IsTrue(HarvestResolver.Resolve(owlbear, s, ob, new HarvestTaskResult { material = "meat", rough = true }, skills, 1).accepted);
            int plumage = owlbear.MaterialIndex("feathers");
            Assert.AreEqual(1.0, ob.CombatIntegrity(owlbear, plumage), 1e-6);
            Assert.AreEqual(HarvestAvailability.Available, ob.Availability(owlbear, s, plumage, skills, 1, out string why), why);
            HarvestOutcome plumes = HarvestResolver.Resolve(owlbear, s, ob, Task("feathers", Stage(HarvestStageKind.Scrape, 0.9f)), skills);
            Assert.IsTrue(plumes.accepted, plumes.refusal);
            Assert.Greater(plumes.Count, 0, "the window offered them whole, and so they come");

            // A bird has no pelt under its feathers: plucking it is skinning it.
            (HarvestAnatomyData eagle, HarvestCarcass e) = Fresh("eagle");
            HarvestOutcome plucked = HarvestResolver.Resolve(eagle, s, e, Task("feathers", Stage(HarvestStageKind.Scrape, 0.9f)), skills);
            Assert.IsTrue(plucked.accepted, plucked.refusal);
            Assert.IsTrue(e.hideOff);
        }

        [Test]
        public void CarcassesSpoilWithWarmth()
        {
            HarvestSettings s = Settings();
            (HarvestAnatomyData a, HarvestCarcass warm) = Fresh("deer");
            HarvestCarcass cold = warm.Clone(), frozen = warm.Clone();
            HarvestMaterial liver = a.Material("liver"), haunch = a.Material("haunch"), femur = a.Material("femur");
            warm.Tick(s, 60 * 10, 30f);
            cold.Tick(s, 60 * 10, 5f);
            frozen.Tick(s, 60 * 10, -5f);
            Assert.Less(warm.Freshness(liver), cold.Freshness(liver));
            Assert.AreEqual(1.0, frozen.Freshness(liver), 1e-9, "frost stops it");
            Assert.AreEqual(1.0, warm.Freshness(femur), 1e-9, "bones never spoil");
            Assert.AreEqual(0.0, warm.Freshness(liver), 0.05, "ten minutes at 30 °C ruin a liver");
            HarvestCarcass gutted = Fresh("deer").c;
            gutted.gutted = true;
            HarvestCarcass whole = Fresh("deer").c;
            gutted.Tick(s, 60 * 20, 20f);
            whole.Tick(s, 60 * 20, 20f);
            Assert.Greater(gutted.Freshness(haunch), whole.Freshness(haunch), "gutting keeps the meat");
            Assert.AreEqual(HarvestMarks.None, whole.TimeMarks(s, femur));
        }

        [Test]
        public void LocksKeepOnePlayerPerMaterial()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("wolf");
            int m = a.MaterialIndex("claws");
            Assert.IsTrue(c.Lock(m, 1, 30));
            Assert.IsFalse(c.Lock(m, 2, 30));
            Assert.AreEqual(HarvestAvailability.Busy, c.Availability(a, Settings(), m, new HarvestSkillData(), 2, out _));
            c.Tick(Settings(), 31, 20f);
            Assert.IsTrue(c.Lock(m, 2, 30), "locks lapse");
        }

        // ------------------------------------------------------------------ the resolver
        [Test]
        public void APerfectPeltFromAnUnhurtBear()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear");
            HarvestSettings s = Settings();
            HarvestOutcome o = HarvestResolver.Resolve(a, s, c, Task("pelt", Stage(HarvestStageKind.Trace, 0.95f), CleanPeel(c.condition)), new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.AreEqual(1, o.pieces.Count);
            Assert.GreaterOrEqual(o.pieces[0].quality, 0.85f);
            Assert.IsNotNull(o.hideSource, "the cells go to the tannery");
            Assert.IsTrue(c.hideOff);
            Assert.Greater(o.xp, 0f);
        }

        [Test]
        public void FightAndHarvestDamageAreToldApart()
        {
            HarvestSettings s = Settings();
            (HarvestAnatomyData a, HarvestCarcass shot) = Fresh("wolf", 11);
            for (int i = 0; i < 3; i++) shot.condition.ApplyHit(a, s, Hit(a, "back", DamageForm.Slash, 0.5f));
            HarvestOutcome fight = HarvestResolver.Resolve(a, s, shot, Task("pelt", Stage(HarvestStageKind.Trace, 0.95f), CleanPeel(shot.condition)), new HarvestSkillData());

            (_, HarvestCarcass clumsy) = Fresh("wolf", 11);
            HarvestOutcome hands = HarvestResolver.Resolve(a, s, clumsy, Task("pelt", Stage(HarvestStageKind.Trace, 0.95f), CleanPeel(clumsy.condition, 0.45f)), new HarvestSkillData());

            (_, HarvestCarcass clean) = Fresh("wolf", 11);
            HarvestOutcome best = HarvestResolver.Resolve(a, s, clean, Task("pelt", Stage(HarvestStageKind.Trace, 0.95f), CleanPeel(clean.condition)), new HarvestSkillData());

            Assert.Less(fight.pieces[0].quality, best.pieces[0].quality);
            Assert.Less(hands.pieces[0].quality, best.pieces[0].quality);
            Assert.IsTrue((fight.pieces[0].marks & HarvestMarks.Cut) != 0 && (fight.pieces[0].marks & HarvestMarksInfo.Harvest) == 0, "cut in the fight");
            Assert.IsTrue((hands.pieces[0].marks & HarvestMarks.Ripped) != 0 && (hands.pieces[0].marks & HarvestMarksInfo.Combat) == 0, "ripped while skinning");
            StringAssert.Contains("in the fight", HarvestMarksInfo.Story(fight.pieces[0].marks));
            StringAssert.Contains("while harvesting", HarvestMarksInfo.Story(hands.pieces[0].marks));
        }

        [Test]
        public void BetterHandsGiveMoreAndBetterMeat()
        {
            HarvestSettings s = Settings();
            HarvestOutcome Cut(float precision, float completeness, float damage)
            {
                (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 3);
                c.hideOff = true;
                return HarvestResolver.Resolve(a, s, c, Task("haunch", Stage(HarvestStageKind.Trace, precision, damage, completeness)), new HarvestSkillData());
            }
            HarvestOutcome good = Cut(0.95f, 1f, 0f), poor = Cut(0.4f, 0.5f, 0.4f);
            Assert.Greater(good.Count, poor.Count, "less waste");
            Assert.Greater(good.pieces[0].quality, poor.pieces[0].quality);
            Assert.Greater(good.xp, poor.xp);
            Assert.Greater(poor.xp, 0f, "mistakes teach too");
        }

        [Test]
        public void SkillHelpsButDoesNotDoTheWork()
        {
            HarvestSettings s = Settings();
            var master = new HarvestSkillData();
            master.AddXp(HarvestSkill.Butchering, HarvestSkills.XpFor(s, s.maxLevel) + 1f);
            HarvestOutcome Cut(HarvestSkillData skills, float precision)
            {
                (HarvestAnatomyData a, HarvestCarcass c) = Fresh("boar", 8);
                c.hideOff = true;
                return HarvestResolver.Resolve(a, s, c, Task("shoulder", Stage(HarvestStageKind.Trace, precision, 0f, precision)), skills);
            }
            HarvestOutcome novice = Cut(new HarvestSkillData(), 0.8f), expert = Cut(master, 0.8f), sloppyExpert = Cut(master, 0.3f);
            Assert.GreaterOrEqual(expert.Count, novice.Count);
            Assert.Greater(expert.pieces[0].quality, novice.pieces[0].quality);
            Assert.Less(sloppyExpert.pieces[0].quality, novice.pieces[0].quality, "a master's sloppy work is still sloppy");
        }

        [Test]
        public void StaleOrgansAndWrongToolsCost()
        {
            HarvestSettings s = Settings();
            HarvestOutcome Take(double minutes, float tool)
            {
                (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 2);
                c.Tick(s, minutes * 60, 20f);
                return HarvestResolver.Resolve(a, s, c, Task("heart", Stage(HarvestStageKind.Steady, 0.9f, tool: tool), Stage(HarvestStageKind.Pull, 0.9f, tool: tool)), new HarvestSkillData());
            }
            HarvestOutcome fresh = Take(0, 0.95f), stale = Take(12, 0.95f), hacked = Take(0, 0.1f);
            Assert.Greater(fresh.pieces[0].quality, stale.pieces[0].quality);
            Assert.IsTrue((stale.pieces[0].marks & HarvestMarks.Spoiling) != 0);
            Assert.Greater(fresh.pieces[0].quality, hacked.pieces[0].quality);
            Assert.IsTrue((hacked.pieces[0].marks & HarvestMarks.Rough) != 0, "the wrong tool");
        }

        [Test]
        public void EachPieceKeepsItsOwnCondition()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear", 21);
            HarvestSettings s = Settings();
            foreach (HarvestUnit u in c.condition.units.Where(u => a.materials[u.material].id == "claws" && a.regions[u.region].id == "foreleg_l"))
                u.integrity = u.index == 0 ? 0.4f : 1f;
            var stages = new List<HarvestStageResult>();
            foreach (HarvestUnit u in c.condition.units.Where(u => a.materials[u.material].id == "claws" && a.regions[u.region].id == "foreleg_l"))
                stages.Add(Stage(HarvestStageKind.Pry, 0.9f, unit: u.index));
            var result = new HarvestTaskResult { material = "claws", units = stages.Select(x => x.unit).ToList(), stages = stages };
            HarvestOutcome o = HarvestResolver.Resolve(a, s, c, result, new HarvestSkillData());
            Assert.AreEqual(5, o.Count, "one paw");
            Assert.Less(o.pieces.First(p => p.unit == 0).quality, o.pieces.First(p => p.unit == 1).quality);
            Assert.AreEqual(15, c.UnitsLeft(s, a.MaterialIndex("claws")));
        }

        [Test]
        public void ABoneCutOutFirstRuinsTheMeatAroundIt()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 4);
            HarvestSettings s = Settings();
            c.hideOff = true;
            Assert.Contains("Venison haunch", HarvestResolver.MeatAtRisk(a, s, c, a.Material("femur")));
            HarvestOutcome o = HarvestResolver.Resolve(a, s, c, new HarvestTaskResult
            {
                material = "femur", units = new List<int> { 0 },
                stages = new List<HarvestStageResult> { Stage(HarvestStageKind.Saw, 0.9f, unit: 0), Stage(HarvestStageKind.Scrape, 0.9f, unit: 0) },
            }, new HarvestSkillData());
            Assert.AreEqual(1, o.Count);
            Assert.Greater(c.TakenShare(a.MaterialIndex("haunch")), 0f, "part of the haunch is wasted");
        }

        [Test]
        public void FragileGlandsBurstAndSpillsSoil()
        {
            HarvestSettings s = Settings();
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear", 5);
            HarvestOutcome burst = HarvestResolver.Resolve(a, s, c, Task("gallbladder", Stage(HarvestStageKind.Steady, 0.5f, 0.9f), Stage(HarvestStageKind.Pull, 0.4f, 0.5f)), new HarvestSkillData());
            Assert.AreEqual(0, burst.Count);
            Assert.AreEqual(1, burst.discarded);

            (_, HarvestCarcass d) = Fresh("bear", 5);
            HarvestStageResult open = Stage(HarvestStageKind.Trace, 0.5f);
            open.spill = 0.8f;
            HarvestOutcome spilled = HarvestResolver.Resolve(a, s, d, Task("heart", open, Stage(HarvestStageKind.Steady, 0.9f), Stage(HarvestStageKind.Pull, 0.9f)), new HarvestSkillData());
            Assert.IsTrue(spilled.accepted);
            HarvestUnit liver = Unit(a, d.condition, "liver");
            Assert.IsTrue((liver.marks & HarvestMarks.Contaminated) != 0, "the cavity is soiled");
            Assert.IsTrue(d.cavityOpen);
        }

        [Test]
        public void AStomachTornWhileWorkingSoilsWhatIsAroundIt()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 6);
            HarvestUnit stomach = Unit(a, c.condition, "stomach");
            HarvestOutcome o = HarvestResolver.Resolve(a, Settings(), c,
                Task("stomach", Stage(HarvestStageKind.Steady, 0.5f, 0.7f), Stage(HarvestStageKind.Pull, 0.5f, 0.3f)), new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.Greater(c.condition.regions[stomach.region].contamination, 0.3f, "it soils its region");
            List<HarvestUnit> beside = c.condition.units.Where(u => u.region == stomach.region && !u.taken && a.materials[u.material].method == HarvestMethod.Organs).ToList();
            Assert.IsNotEmpty(beside);
            Assert.IsTrue(beside.All(u => (u.marks & HarvestMarks.Contaminated) != 0), "and the organs beside it");
            Assert.IsTrue(c.Soiled, "the meat spoils faster");
        }

        [Test]
        public void ARushedPieceIsRoughNotTheOthers()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("bear");
            HarvestStageResult rushed = Stage(HarvestStageKind.Pry, 0.8f, unit: 0);
            rushed.marks |= HarvestMarks.Rough;
            HarvestTaskResult r = Task("claws", rushed, Stage(HarvestStageKind.Pry, 0.9f, unit: 1));
            r.units = new List<int> { 0, 1 };
            HarvestOutcome o = HarvestResolver.Resolve(a, Settings(), c, r, new HarvestSkillData());
            Assert.IsTrue(o.accepted, o.refusal);
            Assert.AreNotEqual(HarvestMarks.None, o.pieces.Single(p => p.unit == 0).marks & HarvestMarks.Rough);
            Assert.AreEqual(HarvestMarks.None, o.pieces.Single(p => p.unit == 1).marks & HarvestMarks.Rough, "the other claw came out cleanly");
        }

        [Test]
        public void TakingTheGutsGutsTheCarcass()
        {
            (HarvestAnatomyData a, HarvestCarcass c) = Fresh("deer", 6);
            HarvestOutcome o = HarvestResolver.Resolve(a, Settings(), c, Task("intestines", Stage(HarvestStageKind.Steady, 0.9f), Stage(HarvestStageKind.Pull, 0.9f)), new HarvestSkillData());
            Assert.AreEqual(1, o.Count);
            Assert.IsTrue(c.gutted);
        }

        // ------------------------------------------------------------------ skills, values, rare finds
        [Test]
        public void LevelsFollowExperience()
        {
            HarvestSettings s = Settings();
            for (int level = 0; level <= s.maxLevel; level += 7)
                Assert.AreEqual(level, HarvestSkills.Level(s, HarvestSkills.XpFor(s, level) + 0.01f));
            Assert.AreEqual(s.maxLevel, HarvestSkills.Level(s, 1e9f));
            var d = new HarvestSkillData();
            List<string> said = HarvestSkills.Gain(s, d, HarvestSkill.Skinning, HarvestSkills.XpFor(s, 10) + 1f);
            Assert.IsTrue(said.Any(x => x.Contains("10")));
            Assert.IsTrue(said.Any(x => x.Contains("Steady pull")), "a technique is learnt");
            Assert.IsTrue(HarvestSkills.Knows(s, d, HarvestSkills.SteadyPull));
            Assert.IsFalse(HarvestSkills.Knows(s, d, HarvestSkills.FineWork));
            HarvestSkillData copy = HarvestSkillData.FromJson(d.ToJson());
            Assert.AreEqual(d.Xp(HarvestSkill.Skinning), copy.Xp(HarvestSkill.Skinning), 1e-3);
        }

        [Test]
        public void ItemValuesRoundTrip()
        {
            foreach (double q in new[] { 0.0, 0.123, 0.5, 0.999, 1.0 })
            {
                HarvestMarks marks = HarvestMarks.Burned | HarvestMarks.Nicked | HarvestMarks.Spoiled;
                int code = HarvestItemCode.Encode(q, marks);
                Assert.Greater(code, 0);
                Assert.IsTrue(HarvestItemCode.TryDecode(code, out double back, out HarvestMarks m));
                Assert.AreEqual(q, back, 0.0006);
                Assert.AreEqual(marks, m);
            }
            Assert.IsFalse(HarvestItemCode.TryDecode(0, out double unknown, out _));
            Assert.AreEqual(HarvestItemCode.UnknownQuality, unknown);
            // 1 is what the inventory gives a copy with no value of its own (an Item Spawner drop): not a ruined piece.
            Assert.IsFalse(HarvestItemCode.TryDecode(1, out double plain, out _), "a plain durability is no value");
            Assert.AreEqual(HarvestItemCode.UnknownQuality, plain);
            Assert.IsTrue(HarvestItemCode.TryDecode(HarvestItemCode.Encode(0, HarvestMarks.None), out double ruined, out _));
            Assert.AreEqual(0.0, ruined, 1e-9, "a real 0% piece keeps its value");
            int all = HarvestItemCode.Encode(1, (HarvestMarks)HarvestMarksInfo.Mask);
            Assert.Greater(all, 0, "every value fits an int");
            Assert.Less((double)all, (double)int.MaxValue, "a bite of food (int.MaxValue) eats any piece");
            Assert.IsTrue(HarvestItemCode.TryDecode(all, out _, out HarvestMarks every));
            Assert.AreEqual((HarvestMarks)HarvestMarksInfo.Mask, every);
            string summary = HarvestItemCode.Summary(new[] { HarvestItemCode.Encode(0.95, 0), HarvestItemCode.Encode(0.92, 0), HarvestItemCode.Encode(0.2, 0) });
            StringAssert.Contains("2 Pristine", summary);
            StringAssert.Contains("1 Poor", summary);
        }

        [Test]
        public void RareFindsAreRareAndNeedAnEye()
        {
            HarvestAnatomyData a = HarvestDefaults.Bear();
            HarvestSettings s = Settings();
            int bezoar = a.MaterialIndex("bezoar");
            int found = 0;
            for (int seed = 0; seed < 2000; seed++)
                if (HarvestCondition.Create(a, seed).Has(bezoar)) found++;
            Assert.That(found / 2000.0, Is.InRange(0.08, 0.17), "about 12%");
            int withOne = Enumerable.Range(0, 2000).First(seed => HarvestCondition.Create(a, seed).Has(bezoar));
            HarvestCarcass c = HarvestCarcass.From(a, HarvestCondition.Create(a, withOne));
            Assert.AreEqual(HarvestAvailability.Hidden, c.Availability(a, s, bezoar, new HarvestSkillData(), 1, out string why));
            StringAssert.Contains("Anatomy 15", why);
            var keen = new HarvestSkillData();
            keen.AddXp(HarvestSkill.Anatomy, HarvestSkills.XpFor(s, 15));
            Assert.AreEqual(HarvestAvailability.Available, c.Availability(a, s, bezoar, keen, 1, out _));
            int elite = 0;
            for (int seed = 0; seed < 2000; seed++)
                if (HarvestCondition.Create(a, seed, 1f, HarvestAge.Adult, true).Has(bezoar)) elite++;
            Assert.Greater(elite, found, "elites carry more rare finds");
        }

        [Test]
        public void BiggerAndOlderCreaturesYieldMore()
        {
            HarvestAnatomyData a = HarvestDefaults.Deer();
            Assert.Greater(HarvestResolver.YieldScale(HarvestCondition.Create(a, 1, 1.3f)), HarvestResolver.YieldScale(HarvestCondition.Create(a, 1, 1f)));
            Assert.Less(HarvestResolver.YieldScale(HarvestCondition.Create(a, 1, 1f, HarvestAge.Young)), 1.0);
        }
    }
}
#endif
