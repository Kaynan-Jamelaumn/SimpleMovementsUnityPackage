#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;

namespace SimpleMovements.Validation.Tests
{
    /// <summary>Edit Mode tests of the name matching used by the reference auto-assigner (no Unity objects involved).</summary>
    public class ReferenceMatcherTests
    {
        private static List<ReferenceCandidate> Candidates(string typeName, params string[] names)
        {
            var list = new List<ReferenceCandidate>();
            foreach (string n in names)
                list.Add(new ReferenceCandidate(n, typeName));
            return list;
        }

        // ------------------------------------------------------------------ words and normalization

        [Test]
        public void NormalizeDropsCasePrefixesSpacesAndClone()
        {
            Assert.AreEqual("weaponsocket", ReferenceMatcher.Normalize("m_WeaponSocket"));
            Assert.AreEqual("maincamera", ReferenceMatcher.Normalize("Main Camera"));
            Assert.AreEqual("hitbox", ReferenceMatcher.Normalize("_hit_box"));
            Assert.AreEqual("arrow", ReferenceMatcher.Normalize("Arrow(Clone)"));
            Assert.AreEqual("", ReferenceMatcher.Normalize(null));
        }

        [Test]
        public void WordsSplitCamelCaseAcronymsDigitsAndSeparators()
        {
            CollectionAssert.AreEqual(new[] { "right", "hand", "socket", "2" }, ReferenceMatcher.Words("rightHand_Socket2"));
            CollectionAssert.AreEqual(new[] { "ui", "camera" }, ReferenceMatcher.Words("UICamera"));
            CollectionAssert.AreEqual(new[] { "hand", "right" }, ReferenceMatcher.Words("Hand.R"));
            CollectionAssert.AreEqual(new[] { "main", "camera" }, ReferenceMatcher.Words("main-cam"));
            CollectionAssert.IsEmpty(ReferenceMatcher.Words(""));
        }

        [Test]
        public void FieldWordsDropReferenceSuffixesAndTheTypeName()
        {
            CollectionAssert.AreEqual(new[] { "weapon" }, ReferenceMatcher.FieldWords("weaponTransform", "Transform"));
            CollectionAssert.AreEqual(new[] { "hit" }, ReferenceMatcher.FieldWords("m_hitColliderRef", "Collider"));
            CollectionAssert.AreEqual(new[] { "hit", "box" }, ReferenceMatcher.FieldWords("hitBoxCollider", "Collider"));
            CollectionAssert.AreEqual(new[] { "animator" }, ReferenceMatcher.FieldWords("animator", "Animator"));
            CollectionAssert.AreEqual(new[] { "character", "controller" }, ReferenceMatcher.FieldWords("characterController", "CharacterController"));
            CollectionAssert.AreEqual(new[] { "status", "controller" }, ReferenceMatcher.FieldWords("statusController", "BaseStatusController"));
        }

        // ------------------------------------------------------------------ scores

        [Test]
        public void ExactNamesScoreHighestAndUnrelatedNamesZero()
        {
            Assert.AreEqual(100, ReferenceMatcher.Score("weaponSocket", "Transform", "WeaponSocket"));
            Assert.AreEqual(100, ReferenceMatcher.Score("weaponTransform", "Transform", "Weapon"));
            Assert.AreEqual(0, ReferenceMatcher.Score("weaponSocket", "Transform", "LeftFoot"));
        }

        [Test]
        public void PartialNamesScoreBetween()
        {
            int contains = ReferenceMatcher.Score("weapon", "Transform", "WeaponSocket");
            int glued = ReferenceMatcher.Score("socket", "Transform", "playerweaponsocket");
            int partOf = ReferenceMatcher.Score("rightHandSocket", "Transform", "RightHand");
            Assert.That(contains, Is.InRange(70, 99));
            Assert.That(glued, Is.InRange(70, 99));
            Assert.That(partOf, Is.InRange(ReferenceMatcher.MinimumNameScore, 99));
            Assert.Greater(ReferenceMatcher.Score("weapon", "Transform", "Weapon"), contains);
        }

        [Test]
        public void ShortFormsAreExpanded()
        {
            Assert.That(ReferenceMatcher.Score("rightHand", "Transform", "Hand_R"), Is.GreaterThanOrEqualTo(95));
            Assert.AreEqual(100, ReferenceMatcher.Score("mainCam", "Camera", "Main Camera"));
        }

        [Test]
        public void CandidateTypeHelpsBaseClassFields()
        {
            int withType = ReferenceMatcher.Score("movementController", "MonoBehaviour", "Player", "PlayerMovementController");
            int withoutType = ReferenceMatcher.Score("movementController", "MonoBehaviour", "Player");
            Assert.Greater(withType, withoutType);
        }

        // ------------------------------------------------------------------ picking

        [Test]
        public void NoCandidatesFindsNothing()
        {
            MatchResult r = ReferenceMatcher.Pick(new List<ReferenceCandidate>(), "animator", "Animator");
            Assert.AreEqual(MatchKind.None, r.kind);
            Assert.IsFalse(r.Found);
        }

        [Test]
        public void SingleComponentCandidateIsTakenWhateverItsName()
        {
            MatchResult r = ReferenceMatcher.Pick(Candidates("Animator", "Model"), "animator", "Animator");
            Assert.AreEqual(MatchKind.Single, r.kind);
            Assert.AreEqual(0, r.index);
        }

        [Test]
        public void SeveralCandidatesNeedAClearNameMatch()
        {
            MatchResult r = ReferenceMatcher.Pick(Candidates("Collider", "Body", "Hitbox", "Feet"), "hitbox", "Collider");
            Assert.AreEqual(MatchKind.NameMatch, r.kind);
            Assert.AreEqual(1, r.index);

            MatchResult tie = ReferenceMatcher.Pick(Candidates("Collider", "Body", "Feet"), "hitbox", "Collider");
            Assert.AreEqual(MatchKind.Ambiguous, tie.kind);
            Assert.IsFalse(tie.Found);
        }

        [Test]
        public void TwoEquallyGoodNamesAreAmbiguous()
        {
            MatchResult r = ReferenceMatcher.Pick(Candidates("Transform", "WeaponSocket_L", "WeaponSocket_R"), "weaponSocket", "Transform", requireName: true);
            Assert.AreEqual(MatchKind.Ambiguous, r.kind);
            MatchResult right = ReferenceMatcher.Pick(Candidates("Transform", "WeaponSocket_L", "WeaponSocket_R"), "rightWeaponSocket", "Transform", requireName: true);
            Assert.AreEqual(MatchKind.NameMatch, right.kind);
            Assert.AreEqual(1, right.index);
        }

        [Test]
        public void TransformsAndGameObjectsAreOnlyMatchedByName()
        {
            MatchResult lone = ReferenceMatcher.Pick(Candidates("Transform", "Model"), "muzzle", "Transform", requireName: true);
            Assert.AreEqual(MatchKind.NoNameMatch, lone.kind);
            Assert.IsFalse(lone.Found);

            MatchResult named = ReferenceMatcher.Pick(Candidates("Transform", "Model", "Muzzle", "Grip"), "muzzle", "Transform", requireName: true);
            Assert.AreEqual(MatchKind.NameMatch, named.kind);
            Assert.AreEqual(1, named.index);
        }

        [Test]
        public void PartialNamesAreNotEnoughForTransforms()
        {
            // "RightHand" is only part of "rightHandSocket": fine to break a tie between components, not to pick a Transform.
            MatchResult r = ReferenceMatcher.Pick(Candidates("Transform", "RightHand"), "rightHandSocket", "Transform", requireName: true);
            Assert.AreEqual(MatchKind.NoNameMatch, r.kind);
        }

        [Test]
        public void FieldsThatPointElsewhereNeedAName()
        {
            Assert.IsTrue(ReferenceMatcher.NeedsNameMatch("targetTransform"));
            Assert.IsTrue(ReferenceMatcher.NeedsNameMatch("m_ignoreCollider"));
            Assert.IsTrue(ReferenceMatcher.NeedsNameMatch("overrideCamera"));
            Assert.IsTrue(ReferenceMatcher.NeedsNameMatch("fallbackAnimator"));
            Assert.IsFalse(ReferenceMatcher.NeedsNameMatch("animator"));
            Assert.IsFalse(ReferenceMatcher.NeedsNameMatch("healthManager"));
            Assert.IsFalse(ReferenceMatcher.NeedsNameMatch("firstHitPoint"));
        }

        [Test]
        public void ExactNameBeatsLongerNames()
        {
            MatchResult r = ReferenceMatcher.Pick(Candidates("Transform", "WeaponSocket", "Weapon"), "weaponTransform", "Transform", requireName: true);
            Assert.AreEqual(MatchKind.NameMatch, r.kind);
            Assert.AreEqual(1, r.index);
        }

        [Test]
        public void MainCameraWinsOverOtherCameras()
        {
            MatchResult r = ReferenceMatcher.Pick(Candidates("Camera", "UICamera", "Main Camera", "MinimapCamera"), "mainCamera", "Camera");
            Assert.AreEqual(MatchKind.NameMatch, r.kind);
            Assert.AreEqual(1, r.index);
        }
    }
}
#endif
