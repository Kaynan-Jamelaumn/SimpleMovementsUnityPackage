#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace InjurySystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window ▸ General ▸ Test Runner) of the injury system: the default configuration, stages and
    /// triggers, treatment targets and effects, bleeding, the hand rules with injured or missing hands, limb loss and
    /// reattachment (with the time limit). Only compiled when the Unity Test Framework is present.
    /// </summary>
    public class InjurySystemTests
    {
        private readonly List<Object> created = new List<Object>();

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private InjurySettings Settings()
        {
            var s = Track(ScriptableObject.CreateInstance<InjurySettings>());
            InjuryDefaults.Fill(s);
            return s;
        }

        /// <summary>A character with an injury controller using <paramref name="s"/> (Edit Mode: parts built by hand).</summary>
        private InjuryController Controller(InjurySettings s)
        {
            var go = Track(new GameObject("Injured"));
            var c = go.AddComponent<InjuryController>();
            var so = new SerializedObject(c);
            so.FindProperty("settingsOverride").objectReferenceValue = s;
            so.ApplyModifiedPropertiesWithoutUndo();
            c.BuildParts();
            return c;
        }

        private static InjuryTreatment Preset(string itemName, int index = 0)
        {
            foreach (InjuryDefaults.MedicalPreset p in InjuryDefaults.MedicalPresets())
                if (p.name == itemName)
                    return p.treatments[index];
            Assert.Fail("No preset " + itemName);
            return null;
        }

        // ------------------------------------------------------------------ configuration
        [Test]
        public void Defaults_AreValid()
        {
            InjurySettings s = Settings();
            var errors = new List<string>();
            var warnings = new List<string>();
            s.Validate(errors, warnings);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(17, s.bodyParts.Count, "head, neck, upper / lower torso, groin, and per side upper arm, forearm, hand, thigh, shin, foot");
            foreach (string id in new[] { "Head", "Neck", "UpperTorso", "LowerTorso", "Groin", "LeftUpperArm", "RightForearm", "LeftHand",
                                          "RightThigh", "LeftShin", "RightFoot" })
                Assert.IsNotNull(s.FindPart(id), id);
            // The old ids still find their part (scripts, treatments, saves).
            Assert.AreEqual("UpperTorso", s.FindPart("Torso").id);
            Assert.AreEqual("LeftUpperArm", s.FindPart("LeftArm").id);
            Assert.AreEqual("RightThigh", s.FindPart("RightLeg").id);
        }

        [Test]
        public void Defaults_HandsHangOnArmsAndFeetOnLegs()
        {
            InjurySettings s = Settings();
            var attached = new List<InjuryBodyPart>();
            s.CollectAttached(s.FindPart("LeftUpperArm"), attached);
            Assert.AreEqual(2, attached.Count, "the forearm and the hand come off with the upper arm");
            attached.Clear();
            s.CollectAttached(s.FindPart("LeftForearm"), attached);
            Assert.AreEqual(1, attached.Count);
            Assert.AreEqual("LeftHand", attached[0].id);
            attached.Clear();
            s.CollectAttached(s.FindPart("RightShin"), attached);
            Assert.AreEqual(1, attached.Count);
            Assert.AreEqual("RightFoot", attached[0].id);
            attached.Clear();
            s.CollectAttached(s.FindPart("UpperTorso"), attached);
            Assert.AreEqual(16, attached.Count, "every other part hangs on the upper torso, directly or not");
        }

        [Test]
        public void Stage_StrongestReachedStageIsChosen()
        {
            InjuryTypeDefinition fracture = Settings().FindType(InjuryDefaults.Fracture);
            Assert.AreEqual(-1, fracture.StageForHit(0.1f), "too weak for a fracture");
            Assert.AreEqual(0, fracture.StageForHit(0.25f));
            Assert.AreEqual(1, fracture.StageForHit(0.4f));
            Assert.AreEqual(2, fracture.StageForHit(0.9f));
            Assert.AreEqual(1, fracture.StageForSeverity(2), "Broken Bone is Severe");
        }

        [Test]
        public void Trigger_FiltersElementsKindsAndStrength()
        {
            InjurySettings s = Settings();
            InjuryBodyPart arm = s.FindPart("LeftArm");
            InjuryTrigger burn = s.FindType(InjuryDefaults.Burn).trigger;
            var fire = new DamageInfo { type = DamageType.Magical, element = ElementType.Fire, delivery = DamageDelivery.Projectile };
            var cut = new DamageInfo { type = DamageType.Physical, delivery = DamageDelivery.Melee };
            Assert.IsTrue(burn.Matches(fire, arm, 0.2f));
            Assert.IsFalse(burn.Matches(cut, arm, 0.2f), "no fire, no burn");
            Assert.IsFalse(burn.Matches(fire, arm, 0.01f), "too weak");
            InjuryTrigger laceration = s.FindType(InjuryDefaults.Laceration).trigger;
            Assert.IsTrue(laceration.Matches(cut, arm, 0.2f));
            Assert.IsFalse(laceration.Matches(fire, arm, 0.2f), "magical damage does not cut");
        }

        [Test]
        public void BodyActions_HandOnThisSideFollowsTheMainHandSide()
        {
            Assert.AreEqual(BodyAction.MainHand, BodyActions.ResolveSide(BodyAction.HandOnThisSide, BodyPartSide.Right, BodyPartSide.Right));
            Assert.AreEqual(BodyAction.OffHand, BodyActions.ResolveSide(BodyAction.HandOnThisSide, BodyPartSide.Left, BodyPartSide.Right));
            Assert.AreEqual(BodyAction.Sprint, BodyActions.ResolveSide(BodyAction.Sprint | BodyAction.HandOnThisSide, BodyPartSide.Center, BodyPartSide.Right));
        }

        // ------------------------------------------------------------------ injuries and treatment
        [Test]
        public void Bandage_StopsMostBleeding_AndOnlyFitsWounds()
        {
            InjurySettings s = Settings();
            InjuryController c = Controller(s);
            InjuryPartState arm = c.GetPart("LeftArm");
            Injury cut = c.AddInjury("LeftArm", InjuryDefaults.Laceration);
            Assert.IsNotNull(cut);
            Assert.That(cut.BleedingPerSecond, Is.EqualTo(0.4f).Within(1e-4f));

            InjuryTreatment bandage = Preset("Bandage");
            Assert.IsTrue(c.ApplyTreatment(bandage, arm, cut, TreatmentSource.Script));
            Assert.IsTrue(cut.Bandaged);
            Assert.That(cut.BleedingPerSecond, Is.EqualTo(0.4f * 0.15f).Within(1e-4f));
            Assert.IsFalse(c.CanTreat(bandage, arm, cut, TreatmentSource.Script, null, out _), "already bandaged");

            Injury bone = c.AddInjury("LeftArm", InjuryDefaults.Fracture, 1);
            Assert.IsFalse(c.CanTreat(bandage, arm, bone, TreatmentSource.Script, null, out _), "a bandage does not set a bone");
        }

        [Test]
        public void Splint_SetsBrokenBonesButNotCompoundFractures()
        {
            InjuryController c = Controller(Settings());
            InjuryPartState leg = c.GetPart("RightLeg");
            Injury broken = c.AddInjury("RightLeg", InjuryDefaults.Fracture, 1);
            Assert.IsFalse(broken.CanHeal, "a broken bone does not heal until treated");
            Assert.IsTrue(float.IsInfinity(broken.HealsIn(1f)));
            InjuryTreatment splint = Preset("Splint");
            Assert.IsTrue(c.ApplyTreatment(splint, leg, broken, TreatmentSource.Script));
            Assert.IsTrue(broken.CanHeal);
            Assert.Less(broken.Impairment, 0.75f, "a splinted leg limps less");

            c.ChangeStage(broken, 2, keepTreatment: false);
            Assert.IsFalse(c.CanTreat(splint, leg, broken, TreatmentSource.Script, null, out _), "compound fractures need surgery");
        }

        [Test]
        public void SameType_MergesIntoTheWorseStage()
        {
            InjuryController c = Controller(Settings());
            Injury first = c.AddInjury("Torso", InjuryDefaults.Fracture, 0);
            Injury again = c.AddInjury("Torso", InjuryDefaults.Fracture, 1);
            Assert.AreSame(first, again);
            Assert.AreEqual(1, first.StageIndex);
            Assert.AreEqual(1, c.GetPart("Torso").Injuries.Count);
        }

        [Test]
        public void Impairment_StopsActions_AndBrokenArmStopsTwoHandedWeapons()
        {
            InjuryController c = Controller(Settings());
            c.RefreshEffects();
            Assert.IsTrue(c.Allows(BodyAction.Sprint));
            Assert.IsTrue(c.Hands.IsFull);

            c.AddInjury("LeftLeg", InjuryDefaults.Fracture, 1); // broken: 75% impairment
            c.AddInjury("LeftArm", InjuryDefaults.Fracture, 1);
            c.RefreshEffects();
            Assert.IsFalse(c.Allows(BodyAction.Sprint), "a broken leg stops sprinting");
            Assert.IsFalse(c.Allows(BodyAction.Jump));
            Assert.IsNotEmpty(c.ReasonFor(BodyAction.Sprint));
            Assert.IsFalse(c.Hands.twoHanded, "a broken arm cannot hold a two-handed weapon");
            Assert.IsTrue(c.Hands.offHand, "but still holds a shield (useless only from 95%)");
            Assert.IsTrue(c.Hands.mainHand);

            c.HealAll(false);
            c.RefreshEffects();
            Assert.IsTrue(c.Allows(BodyAction.Sprint), "healed again");
            Assert.IsTrue(c.Hands.twoHanded);
        }

        // ------------------------------------------------------------------ hands
        [Test]
        public void Hands_TwoHandedWeaponNeedsBothHands()
        {
            WeaponSO hammer = Track(ScriptableObject.CreateInstance<WeaponSO>());
            hammer.SetStats(WeaponCategory.Hammer, 5f, 10f, 0f, 1.5f, 0f, WeaponScaling.None);
            var oneHand = new HandCapability { mainHand = true, offHand = false, twoHanded = false, offReason = "Left hand is missing", twoHandedReason = "Left hand is missing" };
            HandState s = HandRules.Evaluate(hammer, null, oneHand);
            Assert.IsTrue(s.mainHandBlocked);
            Assert.IsNull(s.mainWeapon);
            Assert.IsNotEmpty(s.mainHandReason);
        }

        [Test]
        public void Hands_LostMainHandCarriesAOneHandedItemInTheOtherHand()
        {
            WeaponSO sword = Track(ScriptableObject.CreateInstance<WeaponSO>());
            sword.SetStats(WeaponCategory.Sword, 5f, 10f, 0f, 1.5f, 0f, WeaponScaling.None);
            ArmorSO shield = Track(ScriptableObject.CreateInstance<ArmorSO>());
            var so = new SerializedObject(shield);
            so.FindProperty("armorSlotType").enumValueIndex = (int)ArmorSlotType.Shield;
            so.ApplyModifiedPropertiesWithoutUndo();

            var noMain = new HandCapability { mainHand = false, offHand = true, twoHanded = false, mainReason = "Right arm is missing" };
            HandState s = HandRules.Evaluate(sword, shield, noMain);
            Assert.IsFalse(s.mainHandBlocked, "the other hand holds the sword");
            Assert.AreSame(sword, s.mainWeapon);
            Assert.IsTrue(s.offHandSuppressed, "no hand left for the shield");

            var none = new HandCapability { mainHand = false, offHand = false, twoHanded = false, mainReason = "No hands" };
            Assert.IsTrue(HandRules.Evaluate(sword, null, none).mainHandBlocked);

            var noOff = new HandCapability { mainHand = true, offHand = false, twoHanded = false, offReason = "Left hand is broken" };
            HandState t = HandRules.Evaluate(sword, shield, noOff);
            Assert.IsTrue(t.offHandSuppressed);
            Assert.AreSame(sword, t.mainWeapon);

            HandState full = HandRules.Evaluate(sword, shield, HandCapability.Full);
            Assert.AreSame(shield, full.OffHandShield, "no injuries: the old rules");
        }

        // ------------------------------------------------------------------ limbs
        [Test]
        public void LimbLoss_TakesTheHandAlong_StumpBleeds_AndBlocksTheHand()
        {
            InjurySettings s = Settings();
            InjuryController c = Controller(s);
            InjuryPartState arm = c.GetPart("LeftArm");
            arm.Def.limbLoss.chance = 1f;
            c.ApplyHit(arm, new DamageInfo { amount = 80f, type = DamageType.Physical, delivery = DamageDelivery.Melee });

            Assert.IsTrue(arm.Lost);
            InjuryPartState hand = c.GetPart("LeftHand");
            Assert.IsTrue(hand.Lost);
            Assert.AreSame(arm, hand.LostWith);
            SeveredLimbRecord record = arm.LostLimb;
            Assert.IsNotNull(record);
            Assert.AreEqual(s.reattachment.timeLimitSeconds, record.TimeLimit);
            Assert.Contains(hand.Name, record.IncludedParts);
            Assert.IsTrue(arm.IsBleeding, "the stump bleeds");
            Assert.IsFalse(c.Hands.offHand, "the left (off) hand is gone");
            Assert.IsFalse(c.Hands.twoHanded);
            Assert.IsTrue(c.Hands.mainHand);

            Assert.IsTrue(c.GetPart("LeftForearm").Lost, "the whole arm from the upper arm down");

            // Hits on the missing hand go to... the missing arm's parent: the upper torso.
            InjuryPartState hit = c.ResolvePart(new DamageInfo { aimedBodyPart = "LeftHand" });
            Assert.AreEqual("UpperTorso", hit.Id);
        }

        [Test]
        public void LimbLoss_CutsFromTheSegmentHitDown()
        {
            InjurySettings s = Settings();
            InjuryController c = Controller(s);
            InjuryPartState shin = c.GetPart("LeftShin");
            shin.Def.limbLoss.chance = 1f;
            c.ApplyHit(shin, new DamageInfo { amount = 79f, type = DamageType.Physical, delivery = DamageDelivery.Melee });
            Assert.IsTrue(shin.Lost);
            Assert.IsTrue(c.GetPart("LeftFoot").Lost, "the foot comes off with the shin");
            Assert.IsFalse(c.GetPart("LeftThigh").Lost, "the thigh stays");

            InjuryPartState forearm = c.GetPart("RightForearm");
            forearm.Def.limbLoss.chance = 1f;
            c.ApplyHit(forearm, new DamageInfo { amount = 79f, type = DamageType.Physical, delivery = DamageDelivery.Melee });
            Assert.IsTrue(forearm.Lost);
            Assert.IsTrue(c.GetPart("RightHand").Lost);
            Assert.IsFalse(c.GetPart("RightUpperArm").Lost);
            Assert.AreEqual(1, c.Locomotion.missingArms);
        }

        [Test]
        public void Hits_GoToTheExactSegmentOfTheHitbox()
        {
            InjuryController c = Controller(Settings());
            var forearm = new BodyPartDefinition { name = "LeftForearm", aliases = new List<string> { "Arms", "Forearms" } };
            for (int i = 0; i < 20; i++)
                Assert.AreEqual("LeftForearm", c.ResolvePart(new DamageInfo { bodyPart = forearm }).Id, "never the upper arm or the hand");
            var thigh = new BodyPartDefinition { name = "RightThigh", aliases = new List<string> { "Legs" } };
            Assert.AreEqual("RightThigh", c.ResolvePart(new DamageInfo { bodyPart = thigh }).Id);
            // A simple profile's broad part still lands on one of its segments.
            var arms = new BodyPartDefinition { name = "Arms" };
            InjuryPartState p = c.ResolvePart(new DamageInfo { bodyPart = arms });
            Assert.IsTrue(p.Def.HoldsAHand, p.Id);
        }

        // ------------------------------------------------------------------ movement
        [Test]
        public void Locomotion_LimpHopAndCrawl()
        {
            InjurySettings s = Settings();
            InjuryController c = Controller(s);
            c.RefreshEffects();
            Assert.AreEqual(LocomotionMode.Normal, c.Locomotion.mode);

            c.AddInjury("LeftThigh", InjuryDefaults.Fracture, 1); // broken: 75%
            c.RefreshEffects();
            Assert.AreEqual(LocomotionMode.Limping, c.Locomotion.mode);
            Assert.Less(c.Locomotion.limpSide, 0f, "the left leg is the bad one");
            Assert.Greater(c.Locomotion.limp, 0.5f);

            c.HealAll(false);
            c.LoseLimb("RightFoot");
            Assert.AreEqual(LocomotionMode.Limping, c.Locomotion.mode, "walks on the stump");
            Assert.IsTrue(c.Locomotion.painful);
            Assert.AreEqual(1, c.Locomotion.missingFeet);
            Assert.AreEqual(s.locomotion.missingFootSpeed, c.Locomotion.speedMultiplier, 1e-4f);
            Assert.IsFalse(c.Allows(BodyAction.Sprint));

            c.LoseLimb("RightShin");
            Assert.AreEqual(LocomotionMode.Hopping, c.Locomotion.mode, "one leg left: hop");
            Assert.AreEqual(1, c.Locomotion.missingLegs);
            Assert.IsFalse(c.Allows(BodyAction.Jump));
            Assert.AreEqual(1f, c.Locomotion.heightFactor, 1e-4f, "standing on the other leg");

            c.LoseLimb("LeftThigh");
            Assert.AreEqual(LocomotionMode.Crawling, c.Locomotion.mode, "no legs: crawl");
            Assert.AreEqual(2, c.Locomotion.missingLegs);
            Assert.Less(c.Locomotion.heightFactor, 1f, "the body is shorter without legs");
            Assert.Greater(c.Locomotion.heightFactor, s.locomotion.leglessHeight, "the right knee is still there");
            Assert.Less(c.Locomotion.speedMultiplier, s.locomotion.hopSpeed);

            c.LoseLimb("RightThigh");
            Assert.AreEqual(s.locomotion.leglessHeight, c.Locomotion.heightFactor, 1e-4f, "from the waist up");
        }

        [Test]
        public void Locomotion_CompoundFractureMeansHopping()
        {
            InjuryController c = Controller(Settings());
            c.AddInjury("RightShin", InjuryDefaults.Fracture, 2); // compound: 95%
            c.RefreshEffects();
            Assert.AreEqual(LocomotionMode.Hopping, c.Locomotion.mode);
            Assert.AreEqual(0, c.Locomotion.missingLegs);
        }

        [Test]
        public void Locomotion_HopsMoveInBurstsThatAverageTheHopSpeed()
        {
            var l = new BodyLocomotion { mode = LocomotionMode.Hopping, speedMultiplier = 0.45f, hopPeriod = 0.5f, hopMoveShare = 0.6f, heightFactor = 1f };
            float sum = 0f;
            const int steps = 1000;
            for (int i = 0; i < steps; i++)
                sum += l.SpeedAt(i * 0.5f / steps);
            Assert.AreEqual(0.45f, sum / steps, 0.01f);
            Assert.AreEqual(0f, l.SpeedAt(0.45f), "standing still on landing");
            Assert.AreEqual(1f, BodyLocomotion.Normal.SpeedAt(3.3f));
        }

        [Test]
        public void Cleanup_LimbsLieUntilTheirTimeLimitAndAWhile()
        {
            InjurySettings s = Settings();
            InjuryBodyPart foot = s.FindPart("LeftFoot");
            Assert.AreEqual(s.TimeLimitFor(foot) + 120f, s.LimbDespawnSecondsFor(foot), 1e-3f);
            s.cleanup.limbSeconds = 30f;
            Assert.AreEqual(30f, s.LimbDespawnSecondsFor(foot));
            s.cleanup.limbSeconds = 0f;
            Assert.AreEqual(0f, s.LimbDespawnSecondsFor(foot), "never");
        }

        [Test]
        public void Reattachment_RespectsTheTimeLimit_UnlessTheAbilityIgnoresIt()
        {
            InjurySettings s = Settings();
            InjuryController c = Controller(s);
            InjuryPartState foot = c.GetPart("RightFoot");
            SeveredLimbRecord record = c.LoseLimb("RightFoot");
            Assert.IsNotNull(record);

            var normal = new ReattachLimbTreatment();
            var ctx = new TreatmentContext { patient = c, part = foot, limb = record, source = TreatmentSource.Item };
            Assert.IsTrue(normal.CanApply(ctx, out string why), why);

            record.LostAt = Time.time - record.TimeLimit - 10f; // too late
            Assert.IsTrue(record.Expired);
            Assert.IsFalse(normal.CanApply(ctx, out why));
            StringAssert.Contains("Too late", why);
            Assert.IsTrue(new ReattachLimbTreatment { ignoreTimeLimit = true, ignoreLimbCondition = true }.CanApply(ctx, out why), why);

            ctx.limb = null;
            Assert.IsFalse(normal.CanApply(ctx, out _), "the limb is needed");
            Assert.IsTrue(new ReattachLimbTreatment { regrowWithoutLimb = true }.CanApply(ctx, out _));

            c.ReattachLimb(foot, record, new ReattachLimbTreatment { ignoreTimeLimit = true }, TreatmentSource.Ability);
            Assert.IsFalse(foot.Lost);
            Assert.Greater(foot.Condition, 0f);
            Assert.IsTrue(record.Consumed);
            Assert.AreEqual(1, foot.Injuries.Count, "the knitting injury");
            Assert.AreEqual(InjuryDefaults.Reattached, foot.Injuries[0].Type.id);
        }

        [Test]
        public void Snapshot_IsCapturedWhereTheRulesRun()
        {
            InjuryController c = Controller(Settings());
            c.AddInjury("Head", InjuryDefaults.Scratch);
            byte[] data = c.CaptureSnapshot();
            Assert.IsNotNull(data);
            Assert.Greater(data.Length, 10);
            Assert.IsFalse(c.ApplySnapshot(data), "the rules run here: snapshots are for client copies");
        }
    }
}
#endif
