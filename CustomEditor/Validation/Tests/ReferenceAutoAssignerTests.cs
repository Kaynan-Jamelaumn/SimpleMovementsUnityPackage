#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SimpleMovements.Validation.Tests
{
    /// <summary>
    /// Edit Mode tests of the reference auto-assigner and the reference checks of the Project Validator, on a small
    /// temporary hierarchy:
    /// <code>
    /// Rig (Camera)
    ///  └ Player (AutoAssignProbe, Rigidbody, CapsuleCollider, AudioSource)
    ///     ├ Model (Animator)
    ///     ├ Hitbox (BoxCollider)
    ///     ├ WeaponSocket
    ///     ├ Grip
    ///     ├ Steps (AudioSource)
    ///     └ Music (AudioSource)
    /// </code>
    /// </summary>
    public class ReferenceAutoAssignerTests
    {
        private GameObject rig;
        private GameObject player;
        private AutoAssignProbe probe;
        private Animator modelAnimator;
        private BoxCollider hitboxCollider;
        private AudioSource ownAudio;

        private static AutoAssignOptions Options(bool dryRun = false) =>
            new AutoAssignOptions { dryRun = dryRun, searchScene = false, includeChildren = false };

        [SetUp]
        public void SetUp()
        {
            rig = new GameObject("Rig");
            rig.AddComponent<Camera>();
            player = new GameObject("Player");
            player.transform.SetParent(rig.transform);
            player.AddComponent<Rigidbody>();
            player.AddComponent<CapsuleCollider>();
            ownAudio = player.AddComponent<AudioSource>();
            probe = player.AddComponent<AutoAssignProbe>();
            modelAnimator = Child("Model").AddComponent<Animator>();
            hitboxCollider = Child("Hitbox").AddComponent<BoxCollider>();
            Child("WeaponSocket");
            Child("Grip");
            Child("Steps").AddComponent<AudioSource>();
            Child("Music").AddComponent<AudioSource>();
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null)
                Object.DestroyImmediate(rig);
        }

        private GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(player.transform);
            return go;
        }

        // ------------------------------------------------------------------ filling

        [Test]
        public void SingleComponentsAreTakenFromTheObjectAndItsChildren()
        {
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.AreSame(modelAnimator, probe.animator, "the only Animator, on a child");
            Assert.AreSame(player.GetComponent<Rigidbody>(), probe.body, "the only Rigidbody, on the same object");
            Assert.AreSame(modelAnimator, probe.PrivateAnimator, "[SerializeField] private fields too");
        }

        [Test]
        public void AnObjectNamedLikeTheFieldWinsOverACloserOne()
        {
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.AreSame(hitboxCollider, probe.hitbox);
        }

        [Test]
        public void TransformsAreOnlyFilledByName()
        {
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.IsNotNull(probe.weaponSocket);
            Assert.AreEqual("WeaponSocket", probe.weaponSocket.name);
            Assert.IsNull(probe.muzzle, "no object named Muzzle");
        }

        [Test]
        public void FieldsThatPointElsewhereAreNotFilledByALoneCandidate()
        {
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.IsNull(probe.ignoreCollider);
        }

        [Test]
        public void PrefabFieldsAndNeverFieldsAreLeftAlone()
        {
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.IsNull(probe.projectilePrefab);
            Assert.IsNull(probe.manualAnimator);
        }

        [Test]
        public void AttributesChooseWhereToLook()
        {
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.IsNotNull(probe.handle);
            Assert.AreEqual("Grip", probe.handle.name, "[AutoAssign(ChildName = \"Grip\")]");
            Assert.AreSame(rig.GetComponent<Camera>(), probe.rigCamera, "[AutoAssign(AutoAssignFrom.Parents)]");
        }

        [Test]
        public void ExistingValuesAreNeverReplaced()
        {
            AudioSource music = player.transform.Find("Music").GetComponent<AudioSource>();
            probe.existing = music;
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.AreSame(music, probe.existing);
        }

        [Test]
        public void SeveralEquallyGoodCandidatesAreReportedNotAssigned()
        {
            // Without the Player's own AudioSource: Steps and Music, and neither is called "voice".
            Object.DestroyImmediate(ownAudio);
            AutoAssignReport report = ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.IsNull(probe.voice);
            Assert.IsTrue(report.entries.Any(e => e.propertyPath == "voice" && e.outcome == AssignOutcome.Ambiguous), Describe(report));
        }

        [Test]
        public void ACandidateOnTheSameObjectComesBeforeTheChildren()
        {
            // Own AudioSource, Steps and Music: the nearest one decides when no name does.
            ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.AreSame(ownAudio, probe.voice);
        }

        [Test]
        public void DryRunReportsButChangesNothing()
        {
            AutoAssignReport report = ReferenceAutoAssigner.RunOnComponent(probe, Options(dryRun: true));
            Assert.IsNull(probe.animator);
            Assert.AreEqual(0, probe.autoAssignCalls, "components' own methods are not called in a dry run");
            Assert.IsTrue(report.entries.Any(e => e.propertyPath == "animator" && e.outcome == AssignOutcome.WouldAssign && e.value == modelAnimator), Describe(report));
        }

        [Test]
        public void TheComponentsOwnMethodRunsFirst()
        {
            AutoAssignReport report = ReferenceAutoAssigner.RunOnComponent(probe, Options());
            Assert.AreEqual(1, probe.autoAssignCalls);
            Assert.IsTrue(report.entries.Any(e => e.outcome == AssignOutcome.RanComponentMethod), Describe(report));
        }

        [Test]
        public void RunCoversTheWholeHierarchy()
        {
            AutoAssignProbe second = player.transform.Find("Model").gameObject.AddComponent<AutoAssignProbe>();
            AutoAssignReport report = ReferenceAutoAssigner.Run(rig, new AutoAssignOptions { searchScene = false });
            Assert.AreEqual(2, report.components);
            Assert.AreSame(modelAnimator, probe.animator);
            Assert.AreSame(modelAnimator, second.animator, "the Animator on its own object");
        }

        [Test]
        public void MissingRequiredReferencesAreReported()
        {
            AutoAssignReport report = ReferenceAutoAssigner.RunOnComponent(probe, Options());
            AssignEntry light = report.entries.FirstOrDefault(e => e.propertyPath == "requiredLight");
            Assert.IsNotNull(light, Describe(report));
            Assert.AreEqual(AssignOutcome.NotFound, light.outcome);
            Assert.IsTrue(light.required);
        }

        // ------------------------------------------------------------------ slots

        [Test]
        public void SlotsListTheReferenceFieldsWithTheirAttributes()
        {
            List<ReferenceSlot> slots = ReferenceAutoAssigner.Slots(probe);
            Assert.IsFalse(slots.Any(s => s.propertyPath == "m_Script"));
            ReferenceSlot animator = slots.Single(s => s.propertyPath == "animator");
            Assert.AreEqual(typeof(Animator), animator.fieldType);
            Assert.IsTrue(animator.isEmpty);
            Assert.IsFalse(animator.isMissing, "an empty field is not a broken one");
            Assert.IsNotNull(slots.Single(s => s.propertyPath == "requiredLight").required);
            Assert.IsTrue(slots.Single(s => s.propertyPath == "manualAnimator").autoAssign.Never);
            Assert.IsFalse(ReferenceAutoAssigner.IsAssignable(slots.Single(s => s.propertyPath == "projectilePrefab")));
            Assert.IsTrue(ReferenceAutoAssigner.IsAssignable(animator));
            Assert.IsFalse(ReferenceAutoAssigner.IsAssignable(animator, new AutoAssignOptions { onlyMarkedFields = true }));
        }

        // ------------------------------------------------------------------ validator

        [Test]
        public void ValidatorReportsEmptyRequiredReferences()
        {
            var issues = new List<ValidationIssue>();
            ProjectValidator.ValidateGameObject(rig, ValidationChecks.RequiredReferences, issues, searchScene: false);
            ValidationIssue issue = issues.SingleOrDefault(i => i.category == "Required reference");
            Assert.IsNotNull(issue, string.Join("\n", issues));
            Assert.AreEqual(IssueSeverity.Error, issue.severity);
            StringAssert.Contains("lights the test", issue.message);
            Assert.IsFalse(issue.CanFix, "there is no Light to assign");
        }

        [Test]
        public void ValidatorOffersToFillAssignableReferences()
        {
            var issues = new List<ValidationIssue>();
            ProjectValidator.ValidateGameObject(rig, ValidationChecks.AssignableReferences, issues, searchScene: false);
            ValidationIssue animator = issues.FirstOrDefault(i => i.message.StartsWith("AutoAssignProbe.Animator"));
            Assert.IsNotNull(animator, string.Join("\n", issues));
            Assert.AreEqual(IssueSeverity.Info, animator.severity);
            Assert.IsTrue(ProjectValidator.TryFix(animator));
            Assert.AreSame(modelAnimator, probe.animator);
        }

        [Test]
        public void RequiredReferenceFixAssignsWhenACandidateExists()
        {
            Light lamp = player.transform.Find("Grip").gameObject.AddComponent<Light>();
            var issues = new List<ValidationIssue>();
            ProjectValidator.ValidateGameObject(rig, ValidationChecks.RequiredReferences, issues, searchScene: false);
            ValidationIssue issue = issues.Single(i => i.category == "Required reference");
            Assert.IsTrue(issue.CanFix);
            Assert.IsTrue(ProjectValidator.TryFix(issue));
            Assert.AreSame(lamp, probe.requiredLight);
            Assert.IsFalse(issue.CanFix, "a fix is used once");
        }

        [Test]
        public void CleanHierarchyHasNoMissingScriptsOrBrokenReferences()
        {
            var issues = new List<ValidationIssue>();
            ProjectValidator.ValidateGameObject(rig, ValidationChecks.MissingScripts | ValidationChecks.BrokenReferences, issues, searchScene: false);
            CollectionAssert.IsEmpty(issues, string.Join("\n", issues));
        }

        private static string Describe(AutoAssignReport report) => report.Summary() + "\n" + string.Join("\n", report.entries);
    }
}
#endif
