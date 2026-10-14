#if UNITY_INCLUDE_TESTS && UNITY_EDITOR && FISHNET
using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using GameNet;
using GameNet.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SimpleMovements.Validation.Tests
{
    /// <summary>Edit Mode tests of the network checks of the Project Validator (scene objects, Fish-Net installed).</summary>
    public class NetworkPrefabValidatorTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                if (go != null)
                    Object.DestroyImmediate(go);
            created.Clear();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.AddComponent<NetworkObject>();
            return go;
        }

        private static List<ValidationIssue> Validate(GameObject go)
        {
            // The validator registers its checks when the editor loads; make sure they are in place.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(NetworkPrefabValidator).TypeHandle);
            var issues = new List<ValidationIssue>();
            ProjectValidator.ValidateGameObject(go, ValidationChecks.Network, issues, searchScene: false);
            return issues;
        }

        [Test]
        public void ClientAuthoritativeMobTransformIsAnErrorWithAFix()
        {
            Type networkTransform = NetworkSetupMenu.FindType("FishNet.Component.Transforming.NetworkTransform");
            Assume.That(networkTransform, Is.Not.Null, "Fish-Net's NetworkTransform is needed");
            GameObject mob = Make("Wolf");
            mob.AddComponent<NetMob>();
            Component nt = mob.AddComponent(networkTransform);
            var so = new SerializedObject(nt);
            so.FindProperty("_clientAuthoritative").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            List<ValidationIssue> issues = Validate(mob);
            ValidationIssue issue = issues.FirstOrDefault(i => i.message.Contains("client authoritative"));
            Assert.IsNotNull(issue, string.Join("\n", issues));
            Assert.AreEqual(IssueSeverity.Error, issue.severity);
            Assert.IsTrue(ProjectValidator.TryFix(issue));
            so.Update();
            Assert.IsFalse(so.FindProperty("_clientAuthoritative").boolValue);
            Assert.IsFalse(Validate(mob).Any(i => i.message.Contains("client authoritative")));
        }

        [Test]
        public void MobWithoutNetworkTransformIsReported()
        {
            GameObject mob = Make("Boar");
            mob.AddComponent<NetMob>();
            List<ValidationIssue> issues = Validate(mob);
            Assert.IsTrue(issues.Any(i => i.severity == IssueSeverity.Error && i.message.Contains("no NetworkTransform")), string.Join("\n", issues));
        }

        [Test]
        public void MobMissingModuleComponentsIsAWarning()
        {
            GameObject mob = Make("Bear");
            mob.AddComponent<NetMob>();
            List<ValidationIssue> issues = Validate(mob);
            ValidationIssue modules = issues.FirstOrDefault(i => i.severity == IssueSeverity.Warning && i.message.Contains("(Mob) has no"));
            Assert.IsNotNull(modules, string.Join("\n", issues));
            StringAssert.Contains(nameof(NetCombatStatus), modules.message);
        }

        [Test]
        public void PlayerWithoutPredictionOrControllerIsReported()
        {
            GameObject player = Make("Player");
            player.AddComponent<NetPlayer>();
            var so = new SerializedObject(player.GetComponent<NetworkObject>());
            so.FindProperty("_enablePrediction").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            List<ValidationIssue> issues = Validate(player);
            Assert.IsTrue(issues.Any(i => i.severity == IssueSeverity.Error && i.message.Contains("prediction off")), string.Join("\n", issues));
            Assert.IsTrue(issues.Any(i => i.message.Contains("no CharacterController")), string.Join("\n", issues));
            Assert.IsTrue(issues.Any(i => i.message.Contains("Nothing smooths")), string.Join("\n", issues));
        }

        [Test]
        public void ObsoleteGraphicalObjectIsReportedForMigration()
        {
            GameObject player = Make("Player");
            player.AddComponent<NetPlayer>();
            player.AddComponent<CharacterController>();
            var model = new GameObject("Model");
            model.transform.SetParent(player.transform);
            var so = new SerializedObject(player.GetComponent<NetworkObject>());
            so.FindProperty("_enablePrediction").boolValue = true;
            so.FindProperty("_graphicalObject").objectReferenceValue = model.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            List<ValidationIssue> issues = Validate(player);
            Assert.IsTrue(issues.Any(i => i.message.Contains("obsolete Graphical Object")), string.Join("\n", issues));
        }

        [Test]
        public void GraphicalObjectMustNotBeTheRoot()
        {
            GameObject player = Make("Player");
            player.AddComponent<NetPlayer>();
            player.AddComponent<CharacterController>();
            var so = new SerializedObject(player.GetComponent<NetworkObject>());
            so.FindProperty("_enablePrediction").boolValue = true;
            so.FindProperty("_graphicalObject").objectReferenceValue = player.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            List<ValidationIssue> issues = Validate(player);
            Assert.IsTrue(issues.Any(i => i.severity == IssueSeverity.Error && i.message.Contains("is the root itself")), string.Join("\n", issues));
            Assert.IsFalse(issues.Any(i => i.message.Contains("prediction off")));
            Assert.IsFalse(issues.Any(i => i.message.Contains("no CharacterController")));
        }

        [Test]
        public void WellConfiguredPlayerHasNoPredictionIssues()
        {
            GameObject player = Make("Player");
            player.AddComponent<NetPlayer>();
            player.AddComponent<CharacterController>();
            var model = new GameObject("Model");
            model.transform.SetParent(player.transform);
            var so = new SerializedObject(player.GetComponent<NetworkObject>());
            so.FindProperty("_enablePrediction").boolValue = true;
            so.FindProperty("_predictionType").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            var smoother = model.AddComponent<FishNet.Component.Transforming.Beta.NetworkTickSmoother>();
            var smootherSo = new SerializedObject(smoother);
            smootherSo.FindProperty("_initializationSettings.TargetTransform").objectReferenceValue = player.transform;
            smootherSo.ApplyModifiedPropertiesWithoutUndo();

            List<ValidationIssue> issues = Validate(player);
            Assert.IsFalse(issues.Any(i => i.severity == IssueSeverity.Error), string.Join("\n", issues));
            Assert.IsFalse(issues.Any(i => i.message.Contains("smooth")), string.Join("\n", issues));
        }
    }
}
#endif
