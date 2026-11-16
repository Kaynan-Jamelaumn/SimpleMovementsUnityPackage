#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace InjurySystem.Tests
{
    /// <summary>
    /// Edit Mode tests (Window ▸ General ▸ Test Runner) of the illnesses and the newer injury rules: wounds that heal
    /// while the infection clock runs, stitches torn open, area hits, the Whole Body part, eyes and their sides,
    /// immunity, complications, incubation, saves, the network state, upgrading an older settings asset, picking the
    /// best treatment, amputation, sprains. Only compiled when the Unity Test Framework is present.
    /// </summary>
    public class InjuryIllnessTests
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

        private static int Count(InjuryController c, string typeId) => c.AllInjuries().Count(i => i.Type.id == typeId);

        // ------------------------------------------------------------------ configuration
        [Test]
        public void Defaults_HaveTheWholeBodyAndEveryNewInjury()
        {
            InjurySettings s = Settings();
            var errors = new List<string>();
            var warnings = new List<string>();
            s.Validate(errors, warnings);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.IsNotNull(s.FindWholeBody());
            foreach (string id in InjuryDefaults.NewTypeIds)
                Assert.IsNotNull(s.FindType(id), id);
            Assert.IsFalse(InjuryDefaults.NeedsIllnesses(s));
            // Every medicine names injury types that exist.
            foreach (InjuryDefaults.MedicalPreset p in InjuryDefaults.MedicalPresets())
                foreach (InjuryTreatment t in p.treatments)
                    foreach (string id in t.target.injuryTypes)
                        Assert.IsNotNull(s.FindType(id), $"{p.name}: {id}");
        }

        [Test]
        public void ApplyIllnesses_UpgradesAnOlderAsset_KeepingItsSettings()
        {
            InjurySettings s = Settings();
            // An asset from before illnesses: no Whole Body part, none of the new types, the old links.
            s.bodyParts.RemoveAll(p => p.kind == BodyPartKind.WholeBody);
            s.injuryTypes.RemoveAll(t => InjuryDefaults.NewTypeIds.Contains(t.id));
            InjuryTypeDefinition infection = s.FindType(InjuryDefaults.Infection);
            infection.stages[1].worsenAfterSeconds = 0f;
            infection.stages[1].worsensInto = "";
            infection.stages[0].healSeconds = 111f; // a change made by hand
            s.FindType(InjuryDefaults.Frostbite).stages[1].worsensInto = InjuryDefaults.Infection;
            foreach (InjuryTypeDefinition t in s.injuryTypes)
                t.complications.Clear();
            s.illness.exposures.Clear();
            s.InvalidateLookups();
            Assert.IsTrue(InjuryDefaults.NeedsIllnesses(s));

            // It works before the upgrade too: a built-in Whole Body part stands in.
            InjuryController old = Controller(s);
            Assert.IsNotNull(old.WholeBody);

            List<string> done = InjuryDefaults.ApplyIllnesses(s);
            Assert.IsNotEmpty(done);
            Assert.IsFalse(InjuryDefaults.NeedsIllnesses(s));
            Assert.AreEqual(InjuryDefaults.Sepsis, infection.stages[1].worsensInto);
            Assert.AreEqual(111f, infection.stages[0].healSeconds, "kept");
            Assert.AreEqual(InjuryDefaults.Gangrene, s.FindType(InjuryDefaults.Frostbite).stages[1].worsensInto);
            Assert.IsNotEmpty(s.FindType(InjuryDefaults.Puncture).complications, "punctures give tetanus");
            Assert.IsNotEmpty(s.illness.exposures);
            var errors = new List<string>();
            s.Validate(errors, new List<string>());
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.IsEmpty(InjuryDefaults.ApplyIllnesses(s), "twice changes nothing");
            CollectionAssert.AreEqual(InjuryDefaults.InjuryTypes().Select(t => t.id).ToList(), s.injuryTypes.Select(t => t.id).ToList(),
                "the new types sit where the defaults put them (the order matters for hits)");
        }

        [Test]
        public void Lookup_FollowsTheListWhenAScriptChangesIt()
        {
            InjurySettings s = Settings();
            InjuryTypeDefinition scratch = s.FindType("scratch");
            Assert.AreEqual(InjuryDefaults.Scratch, scratch.id, "by id, any case");
            Assert.AreSame(s.FindType(InjuryDefaults.DeepWound), s.FindType("Deep Wound"), "by name");
            s.injuryTypes.Remove(scratch);
            var mine = new InjuryTypeDefinition { id = "Mine", displayName = "My Injury", stages = new List<InjuryStage> { new InjuryStage() } };
            s.injuryTypes.Add(mine); // as many as before
            Assert.IsNull(s.FindType("Scratch"), "removed");
            Assert.AreSame(mine, s.FindType("My Injury"), "added");
        }

        // ------------------------------------------------------------------ rules
        [Test]
        public void Wounds_HealWhileTheInfectionClockRuns()
        {
            InjurySettings s = Settings();
            InjuryController bandaged = Controller(s), open = Controller(s);
            Injury a = bandaged.AddInjury("LeftArm", InjuryDefaults.Laceration);
            Injury b = open.AddInjury("LeftArm", InjuryDefaults.Laceration);
            Assert.IsTrue(bandaged.ApplyTreatment(Preset("Bandage"), bandaged.GetPart("LeftArm"), a, TreatmentSource.Script));

            bandaged.Simulate(250f);
            open.Simulate(250f);
            Assert.AreEqual(0, bandaged.AllInjuries().Count, "a bandaged cut closes (4 min) before it gets infected (10 min)");
            Assert.Greater(b.HealProgress, 0.3f, "an open cut heals too, slowly");

            open.Simulate(360f);
            Assert.AreEqual(1, Count(open, InjuryDefaults.Infection), "but it gets infected first");
            open.Simulate(130f);
            Assert.AreEqual(0, Count(open, InjuryDefaults.Laceration), "and still closes in the end");
        }

        [Test]
        public void Stitches_TornOpenByAHit_CanBeRedone()
        {
            InjuryController c = Controller(Settings());
            InjuryPartState arm = c.GetPart("LeftArm");
            Injury wound = c.AddInjury("LeftArm", InjuryDefaults.DeepWound);
            InjuryTreatment stitch = Preset("Suture Kit");
            Assert.IsTrue(c.ApplyTreatment(stitch, arm, wound, TreatmentSource.Script));
            Assert.IsFalse(wound.IsBleeding);
            Assert.IsFalse(c.CanTreat(stitch, arm, wound, TreatmentSource.Script, null, out _), "already stitched");

            Assert.IsNull(c.AddInjury(arm, wound.Type, 0, null, true), "a new hit there makes no second wound");
            Assert.IsTrue(wound.IsBleeding, "it tears the stitches");
            Assert.IsTrue(wound.Reopened);
            Assert.IsFalse(wound.Treated);
            Assert.IsTrue(c.CanTreat(stitch, arm, wound, TreatmentSource.Script, null, out string why), why);
            Assert.IsTrue(c.ApplyTreatment(stitch, arm, wound, TreatmentSource.Script));
            Assert.IsFalse(wound.Reopened);
        }

        [Test]
        public void AreaHit_IsSpreadOverSeveralParts()
        {
            InjurySettings s = Settings();
            s.injuryTypes.Clear(); // only the condition matters here
            InjuryController c = Controller(s);
            c.ApplyAreaHit(new DamageInfo { amount = 30f, type = DamageType.Magical, delivery = DamageDelivery.Area });
            Assert.AreEqual(s.areaHitParts, c.Parts.Count(p => p.Condition < p.MaxCondition - 0.001f));
            Assert.AreEqual(c.WholeBody.MaxCondition, c.WholeBody.Condition, "never the whole body");
        }

        [Test]
        public void Illness_SitsOnTheWholeBody_AndHitsNeverLandThere()
        {
            InjuryController c = Controller(Settings());
            Injury cold = c.AddInjury("LeftArm", InjuryDefaults.CommonCold);
            Assert.IsNotNull(cold);
            Assert.AreSame(c.WholeBody, cold.Part, "whichever part it was given to");
            Assert.IsTrue(cold.IsIllness);
            Assert.AreEqual(0, c.GetPart("LeftArm").Injuries.Count);
            Assert.IsNull(c.AddInjury(c.WholeBody, c.Settings.FindType(InjuryDefaults.Fracture), 0, null, false), "no broken bone on the whole body");
            Assert.AreEqual("UpperTorso", c.ResolvePart(new DamageInfo { aimedBodyPart = InjuryDefaults.WholeBodyId }).Id);
            c.ApplyHit(c.WholeBody, new DamageInfo { amount = 50f, type = DamageType.Physical });
            Assert.AreEqual(c.WholeBody.MaxCondition, c.WholeBody.Condition);
        }

        [Test]
        public void EyeInjury_TakesASide_AndDarkensThatHalfOfTheSight()
        {
            InjuryController c = Controller(Settings());
            Injury first = c.AddInjury("Head", InjuryDefaults.EyeInjury, 2);
            Assert.AreNotEqual(BodyPartSide.Center, first.Side);
            StringAssert.StartsWith(first.Side.ToString(), first.Name, "named after its side");
            c.RefreshEffects();
            InjuryVision sight = c.Vision;
            Assert.AreEqual(1f, first.Side == BodyPartSide.Left ? sight.left : sight.right, 1e-4f, "that eye sees nothing");
            Assert.AreEqual(0f, first.Side == BodyPartSide.Left ? sight.right : sight.left, 1e-4f, "the other is fine");
            Assert.IsFalse(sight.Blind);

            Injury second = c.AddInjury("Head", InjuryDefaults.EyeInjury, 2);
            Assert.AreNotSame(first, second);
            Assert.AreNotEqual(first.Side, second.Side, "the other eye");
            c.RefreshEffects();
            Assert.IsTrue(c.Vision.Blind);
            Assert.IsNull(c.AddInjury("Head", InjuryDefaults.EyeInjury, 2), "two eyes only");
        }

        [Test]
        public void Immunity_AfterAnIllness()
        {
            InjuryController c = Controller(Settings());
            Injury cold = c.AddInjury("", InjuryDefaults.CommonCold);
            c.RemoveInjury(cold);
            Assert.Greater(c.ImmunityLeft(InjuryDefaults.CommonCold), 0f);
            Assert.IsNull(c.AddInjury("", InjuryDefaults.CommonCold), "immune");
            c.SetImmunity(InjuryDefaults.CommonCold, 0f);
            Assert.IsNotNull(c.AddInjury("", InjuryDefaults.CommonCold));
        }

        [Test]
        public void SevereInfection_LeadsToSepsis_AndAntibioticsLetItHeal()
        {
            InjuryController c = Controller(Settings());
            c.AddInjury("LeftArm", InjuryDefaults.Infection, 1);
            c.Simulate(605f);
            Injury sepsis = c.AllInjuries().FirstOrDefault(i => i.Type.id == InjuryDefaults.Sepsis);
            Assert.IsNotNull(sepsis);
            Assert.AreSame(c.WholeBody, sepsis.Part);
            Assert.IsFalse(sepsis.CanHeal, "sepsis does not pass by itself");
            Assert.IsTrue(c.ApplyTreatment(Preset("Antibiotics"), c.WholeBody, sepsis, TreatmentSource.Script));
            Assert.IsTrue(sepsis.CanHeal);
            Assert.IsFalse(sepsis.CanWorsen);
        }

        [Test]
        public void Incubation_IsNotShown_ButMedicineFindsIt()
        {
            InjuryController c = Controller(Settings());
            Injury tetanus = c.AddInjury("", InjuryDefaults.Tetanus);
            Assert.IsTrue(tetanus.Hidden);
            Assert.AreEqual(0, c.VisibleInjuryCount);
            c.Simulate(305f);
            Assert.IsFalse(tetanus.Hidden, "it shows after the incubation");
            Assert.AreEqual(1, c.VisibleInjuryCount);

            InjuryController early = Controller(c.Settings);
            Injury incubating = early.AddInjury("", InjuryDefaults.Tetanus);
            Assert.IsTrue(early.ApplyTreatment(Preset("Antitoxin"), early.WholeBody, incubating, TreatmentSource.Script), "in time: prevented");
            Assert.AreEqual(0, early.AllInjuries().Count);
            Assert.IsFalse(c.CanTreat(Preset("Antitoxin"), c.WholeBody, tetanus, TreatmentSource.Script, null, out _), "the first treatment is for the incubation only");
            Assert.IsTrue(c.ApplyTreatment(Preset("Antitoxin", 1), c.WholeBody, tetanus, TreatmentSource.Script), "once it shows: it can heal");
            Assert.IsTrue(tetanus.CanHeal);
        }

        [Test]
        public void Sprain_ComesFromCrushingHitsOnTheFoot_AndStopsSprinting()
        {
            InjurySettings s = Settings();
            InjuryTypeDefinition sprain = s.FindType(InjuryDefaults.Sprain);
            Assert.AreEqual(0f, sprain.trigger.ChanceFor(DamageForm.Slash), "a blade does not sprain an ankle");
            Assert.Greater(sprain.trigger.ChanceFor(DamageForm.Crush), 0f, "a fall does");
            s.injuryTypes = new List<InjuryTypeDefinition> { sprain };
            sprain.trigger.chance = 1f;
            InjuryController c = Controller(s);
            InjuryPartState foot = c.GetPart("LeftFoot");
            c.ApplyHit(foot, new DamageInfo { amount = 0.2f * foot.MaxCondition, type = DamageType.Physical, form = DamageForm.Crush });
            Assert.AreEqual(1, foot.Injuries.Count);
            Assert.AreEqual("Sprained Ankle", foot.Injuries[0].Name);
            c.RefreshEffects();
            Assert.IsFalse(c.Allows(BodyAction.Sprint));
            Assert.AreEqual(LocomotionMode.Limping, c.Locomotion.mode);
            // A bandage wraps it, a splint holds it.
            Assert.IsTrue(c.CanTreat(Preset("Bandage"), foot, foot.Injuries[0], TreatmentSource.Script, null, out _));
            Assert.IsTrue(c.CanTreat(Preset("Splint"), foot, foot.Injuries[0], TreatmentSource.Script, null, out _));
        }

        [Test]
        public void Falls_AreSoftenedByStatsAndResistances()
        {
            InjurySettings s = Settings();
            InjuryController c = Controller(s);
            Assert.AreEqual(s.falls.safeHeight, c.SafeFallHeight, 1e-4f, "no stat: the settings' safe height");
            Assert.AreEqual(1f, c.FallDamageFactor, 1e-4f);
            Assert.AreEqual(0f, c.OwnJumpHeight, 1e-4f, "no movement model: no jump to credit");

            object half = c.Resistances.Add(new InjuryResistance("Soft Landing") { fallDamage = -50f });
            Assert.AreEqual(0.5f, c.FallDamageFactor, 1e-4f);
            object none = c.Resistances.Add(new InjuryResistance("Feather Fall") { fallDamage = -100f });
            Assert.AreEqual(0f, c.FallDamageFactor, 1e-4f, "-100: never hurt by a fall");
            c.Resistances.Remove(none);
            c.Resistances.Remove(half);
            Assert.AreEqual(1f, c.FallDamageFactor, 1e-4f, "removed exactly");

            var feather = new InjuryResistance("Feather Fall") { fallDamage = -100f };
            Assert.IsFalse(feather.IsEmpty);
            StringAssert.Contains("no fall damage", feather.Describe());
            Assert.IsTrue(TraitStats.IsAggregate(TraitStat.FallDamage) && TraitStats.IsAggregate(TraitStat.SafeFallHeight));
            Assert.IsFalse(TraitStats.Get(TraitStat.FallDamage).higherIsBetter);
            Assert.IsTrue(TraitStats.Get(TraitStat.SafeFallHeight).higherIsBetter);
        }

        [Test]
        public void Gangrene_SurgeryInTime_ElseAmputation()
        {
            InjuryController c = Controller(Settings());
            InjuryPartState hand = c.GetPart("LeftHand");
            Injury rot = c.AddInjury("LeftHand", InjuryDefaults.Gangrene, 0);
            InjuryTreatment cutOut = Preset("Surgical Kit", 2), amputate = Preset("Surgical Kit", 3);
            Assert.IsTrue(c.CanTreat(cutOut, hand, rot, TreatmentSource.Script, null, out _), "necrosis can still be cut out");
            c.Simulate(425f);
            Assert.AreEqual(1, rot.StageIndex, "gangrene");
            Assert.IsFalse(c.CanTreat(cutOut, hand, rot, TreatmentSource.Script, null, out _), "too late");
            Assert.IsTrue(c.ApplyTreatment(amputate, hand, rot, TreatmentSource.Script));
            Assert.IsTrue(hand.Lost);
            Assert.AreEqual(0, Count(c, InjuryDefaults.Gangrene), "it went with the hand");
            Injury stump = hand.Injuries[0];
            Assert.IsFalse(stump.IsBleeding, "a surgeon closes the stump");
            Assert.IsTrue(hand.LostLimb.Expired, "the limb is not put back");
            Injury chest = c.AddInjury("UpperTorso", InjuryDefaults.Gangrene, 1);
            Assert.IsFalse(c.CanTreat(amputate, c.GetPart("UpperTorso"), chest, TreatmentSource.Script, null, out _));
        }

        [Test]
        public void Tourniquet_StopsAnArteryOnALimb_AndASplintResetsAJoint()
        {
            InjuryController c = Controller(Settings());
            Injury artery = c.AddInjury("RightThigh", InjuryDefaults.ArterialBleed);
            Assert.Greater(artery.BleedingPerSecond, 2f);
            Assert.IsTrue(c.ApplyTreatment(Preset("Tourniquet"), c.GetPart("RightThigh"), artery, TreatmentSource.Script));
            Assert.IsFalse(artery.IsBleeding);
            Assert.AreEqual("Tourniquet", artery.TreatmentLabel);
            Assert.IsFalse(artery.CanHeal, "stopped, not closed");
            Injury neck = c.AddInjury("Neck", InjuryDefaults.ArterialBleed);
            Assert.IsFalse(c.CanTreat(Preset("Tourniquet"), c.GetPart("Neck"), neck, TreatmentSource.Script, null, out _), "not on a neck");

            Injury joint = c.AddInjury("LeftUpperArm", InjuryDefaults.Dislocation, 1);
            Assert.IsTrue(c.ApplyTreatment(Preset("Splint", 1), c.GetPart("LeftUpperArm"), joint, TreatmentSource.Script));
            Assert.AreEqual(0, joint.StageIndex, "only sore now");
            Assert.IsTrue(joint.CanHeal);
        }

        [Test]
        public void Treatment_ScoresWhatTheInjuryNeedsMost()
        {
            InjuryController c = Controller(Settings());
            Injury cut = c.AddInjury("LeftArm", InjuryDefaults.Laceration);
            Assert.Greater(Preset("Bandage").ScoreFor(cut), Preset("Disinfectant").ScoreFor(cut), "bleeding first");
            Assert.Greater(Preset("Sterile Bandage").ScoreFor(cut), Preset("Bandage").ScoreFor(cut), "the one that also keeps it clean");
            Injury bone = c.AddInjury("LeftShin", InjuryDefaults.Fracture, 1);
            Assert.Greater(Preset("Splint").ScoreFor(bone), 5f, "a broken bone needs what lets it heal");
            Assert.AreEqual(0f, Preset("Surgical Kit", 3).ScoreFor(bone), "an amputation is never picked by itself");
        }

        // ------------------------------------------------------------------ saves and the network
        [Test]
        public void Save_RestoresTheSameBody()
        {
            InjurySettings s = Settings();
            InjuryController a = Controller(s);
            Injury bone = a.AddInjury("LeftLeg", InjuryDefaults.Fracture, 1);
            a.ApplyTreatment(Preset("Splint"), a.GetPart("LeftLeg"), bone, TreatmentSource.Script);
            a.AddInjury("", InjuryDefaults.CommonCold, 1);
            Injury eye = a.AddInjury("Head", InjuryDefaults.EyeInjury, 1);
            a.LoseLimb("RightFoot");
            a.SetImmunity(InjuryDefaults.Tetanus, 500f);

            InjuryController b = Controller(s);
            Assert.IsTrue(b.RestoreSave(a.CaptureSave()));
            Assert.IsTrue(b.GetPart("RightFoot").Lost);
            Assert.IsTrue(b.GetPart("RightFoot").LostLimb.Gone, "the limb itself did not last");
            Injury bone2 = b.GetPart("LeftLeg").Injuries[0];
            Assert.AreEqual(1, bone2.StageIndex);
            Assert.AreEqual("Splinted", bone2.TreatmentLabel);
            Assert.IsTrue(bone2.CanHeal);
            Assert.AreEqual(1, b.WholeBody.Injuries.Count, "the flu");
            Assert.AreEqual(eye.Side, b.GetPart("Head").Injuries[0].Side);
            Assert.Greater(b.ImmunityLeft(InjuryDefaults.Tetanus), 400f);
            Assert.IsFalse(b.Allows(BodyAction.Sprint), "effects apply at once");
            Assert.IsFalse(b.RestoreSave(new byte[] { 9, 9, 9, 9, 9, 9, 9 }), "garbage is refused");
            Assert.IsTrue(b.GetPart("RightFoot").Lost, "and changes nothing");
        }

        [Test]
        public void Snapshot_WhatOthersSeeIsFarSmaller()
        {
            InjuryController c = Controller(Settings());
            c.AddInjury("LeftLeg", InjuryDefaults.Fracture, 1);
            c.AddInjury("RightForearm", InjuryDefaults.Laceration);
            c.AddInjury("", InjuryDefaults.CommonCold);
            byte[] full = c.CaptureSnapshot(), visible = c.CapturePublicSnapshot();
            Assert.Less(visible.Length, full.Length / 2);
            Assert.IsFalse(c.ApplySnapshot(visible), "the rules run here: snapshots are for client copies");
        }
    }
}
#endif
