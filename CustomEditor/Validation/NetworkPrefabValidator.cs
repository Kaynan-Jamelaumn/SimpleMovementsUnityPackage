#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using GameNet.EditorTools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
#if FISHNET
using FishNet.Object;
using GameNet;
#endif

namespace SimpleMovements.Validation
{
    /// <summary>
    /// Network checks for the Project Validator (Fish-Net): network components without a NetworkObject, player prefabs
    /// whose prediction is off or that have no smoothed model, mobs and items without a server-authoritative
    /// NetworkTransform, mob prefabs that are not networked, missing module components, and the project-wide network
    /// setup (Bootstrap scene, world item carrier...). Most issues can be fixed from the window: the fix re-applies
    /// Tools ▸ SimpleMovements ▸ Network ▸ Make Selected Prefab Networked to the prefab.
    /// </summary>
    [InitializeOnLoad]
    public static class NetworkPrefabValidator
    {
        private const string NetworkTransformType = "FishNet.Component.Transforming.NetworkTransform";
        private const string Category = "Network";

        static NetworkPrefabValidator()
        {
            ProjectValidator.ValidateRoot += ValidateRoot;
            ProjectValidator.ValidateProject += ValidateProject;
        }

        // ------------------------------------------------------------------ menus

        [MenuItem(SMMenu.Root + "Validate/Network Prefabs", false, SMMenu.Validate + 27)]
        private static void ValidateAllMenu()
        {
            List<ValidationIssue> issues = ProjectValidator.Run(ValidationScope.Everything, ValidationChecks.Network);
            foreach (ValidationIssue issue in issues)
                issue.Log();
            string summary = issues.Count == 0 ? "No network problems found." : $"{issues.Count} network issue(s) - see the Console.";
            if (issues.Count > 0 && EditorUtility.DisplayDialog("Network validation", summary, "Open Project Validator", "Close"))
                ValidationWindow.Open(ValidationScope.Everything, ValidationChecks.Network);
            else if (issues.Count == 0)
                EditorUtility.DisplayDialog("Network validation", summary, "OK");
        }

        // ------------------------------------------------------------------ project-wide

        private static void ValidateProject(ValidationChecks checks, List<ValidationIssue> issues)
        {
            if ((checks & ValidationChecks.Network) == 0)
                return;
            foreach (string problem in NetworkSetupMenu.CollectSetupProblems())
            {
                // The item database is also a project-asset check: do not list it twice.
                if ((checks & ValidationChecks.ProjectAssets) != 0 && problem.Contains("ItemDatabase"))
                    continue;
#if FISHNET
                var issue = new ValidationIssue(IssueSeverity.Warning, Category + " setup", problem);
                if (problem.Contains("NetWorldItem"))
                    issue.WithFix("Create", () => EditorApplication.ExecuteMenuItem("Tools/SimpleMovements/Network/Create World Item Prefab"));
                else if (problem.Contains("ItemDatabase"))
                    issue.WithFix("Build", () => EditorApplication.ExecuteMenuItem("Tools/SimpleMovements/Project Setup/Build Item Database"));
#else
                var issue = new ValidationIssue(IssueSeverity.Info, Category + " setup", problem + " Network checks were skipped.");
#endif
                issues.Add(issue);
            }
        }

        // ------------------------------------------------------------------ per object

        private static void ValidateRoot(GameObject root, List<ValidationIssue> issues)
        {
#if FISHNET
            string prefabPath = ValidationUtility.PrefabAssetPath(root);

            foreach (NetworkBehaviour nb in root.GetComponentsInChildren<NetworkBehaviour>(true))
            {
                if (nb == null || nb.GetComponentInParent<NetworkObject>(true) != null)
                    continue;
                GameObject go = nb.gameObject;
                var issue = Issue(IssueSeverity.Error, $"{nb.GetType().Name} on \"{go.name}\" has no NetworkObject on its object or a parent: it will never run", go, prefabPath);
                if (prefabPath != null)
                    issue.WithFix("Add NetworkObject", () => ValidationUtility.EditPrefab(prefabPath, r =>
                    {
                        if (r.GetComponent<NetworkObject>() != null)
                            return false;
                        r.AddComponent<NetworkObject>();
                        return true;
                    }));
                else
                    issue.WithFix("Add NetworkObject", () => go != null && Undo.AddComponent<NetworkObject>(go) != null);
                issues.Add(issue);
            }

            foreach (NetPlayer player in root.GetComponentsInChildren<NetPlayer>(true))
                CheckPlayer(player, prefabPath, issues);
            foreach (NetMob mob in root.GetComponentsInChildren<NetMob>(true))
                CheckServerTransform(mob.gameObject, "mob", "Mob", prefabPath, issues, required: true);
            foreach (NetWorldItem item in root.GetComponentsInChildren<NetWorldItem>(true))
                CheckServerTransform(item.gameObject, "world item", "Pickup / Loot", prefabPath, issues, required: false);

            // A mob prefab without a NetworkObject only ever exists on the server.
            if (prefabPath != null && root.GetComponent<Mob>() != null && root.GetComponent<NetworkObject>() == null)
                issues.Add(Issue(IssueSeverity.Warning, $"The mob prefab \"{root.name}\" is not networked: in network sessions clients will not see it", root, prefabPath)
                    .WithFix("Make networked", () => MakeNetworked(prefabPath, "Mob")));

            foreach (NetworkObject nob in root.GetComponentsInChildren<NetworkObject>(true))
            {
                var so = new SerializedObject(nob);
                SerializedProperty networked = so.FindProperty("_isNetworked");
                if (networked != null && !networked.boolValue)
                    issues.Add(Issue(IssueSeverity.Info, $"The NetworkObject on \"{nob.name}\" has Is Networked off: it acts as a plain object", nob.gameObject, prefabPath));
            }
#endif
        }

#if FISHNET
        private static void CheckPlayer(NetPlayer player, string prefabPath, List<ValidationIssue> issues)
        {
            GameObject go = player.gameObject;
            NetworkObject nob = go.GetComponent<NetworkObject>();
            if (nob == null)
            {
                issues.Add(Issue(IssueSeverity.Error, $"NetPlayer on \"{go.name}\" must be next to the NetworkObject (on the prefab root)", go, prefabPath));
                return;
            }
            var so = new SerializedObject(nob);
            SerializedProperty prediction = so.FindProperty("_enablePrediction");
            if (prediction != null && !prediction.boolValue)
                issues.Add(FixWithMake(Issue(IssueSeverity.Error, $"The player \"{go.name}\" has prediction off: its movement will not be predicted or reconciled", go, prefabPath), prefabPath, "Player"));
            SerializedProperty predictionType = so.FindProperty("_predictionType");
            if (predictionType != null && predictionType.intValue != 0)
                issues.Add(FixWithMake(Issue(IssueSeverity.Warning, $"The player \"{go.name}\" uses a Rigidbody prediction type: the character controller needs \"Other\"", go, prefabPath), prefabPath, "Player"));
            SerializedProperty graphical = so.FindProperty("_graphicalObject");
            var legacy = graphical != null ? graphical.objectReferenceValue as Transform : null;
            Type tickSmootherType = NetworkSetupMenu.FindType("FishNet.Component.Transforming.Beta.NetworkTickSmoother");
            Component tickSmoother = tickSmootherType != null ? go.GetComponentInChildren(tickSmootherType, true) : null;
            PlayerMovementModel movementModel = go.GetComponentInChildren<PlayerMovementModel>(true);
            Transform body = movementModel != null ? movementModel.PlayerTransform : null;
            // NetPlayerMovement smooths the turned body itself when the model sits under it (Smooth Body).
            bool bodySmoothed = body != null && body != go.transform && body.IsChildOf(go.transform) && body.GetComponentInChildren<Renderer>(true) != null;
            if (body == go.transform && tickSmoother != null && tickSmoother.transform.parent == go.transform)
                issues.Add(FixWithMake(Issue(IssueSeverity.Error, $"\"{go.name}\" turns its root (PlayerMovementModel ▸ Player Transform = the root): cameras under the root spin in first person - set Player Transform back to '{tickSmoother.name}'", go, prefabPath), prefabPath, "Player"));
            if (tickSmoother == null && legacy == null && !bodySmoothed)
                issues.Add(FixWithMake(Issue(IssueSeverity.Warning, $"Nothing smooths the model of \"{go.name}\" between ticks (no NetworkTickSmoother): walking looks jerky", go, prefabPath), prefabPath, "Player"));
            else if (tickSmoother != null && legacy != null)
                issues.Add(FixWithMake(Issue(IssueSeverity.Error, $"\"{go.name}\" is smoothed twice (NetworkTickSmoother and the NetworkObject's Graphical Object): they fight", go, prefabPath), prefabPath, "Player"));
            else if (tickSmoother == null && legacy != null && tickSmootherType != null)
                issues.Add(FixWithMake(Issue(IssueSeverity.Warning, $"\"{go.name}\" uses the NetworkObject's obsolete Graphical Object (jitters for the host): move it to a NetworkTickSmoother", go, prefabPath), prefabPath, "Player"));
            if (legacy != null && legacy == nob.transform)
                issues.Add(Issue(IssueSeverity.Error, $"The Graphical Object of \"{go.name}\" is the root itself: it must be the visual child (model)", go, prefabPath));
            Type networkTransform = NetworkSetupMenu.FindType(NetworkTransformType);
            if (networkTransform != null)
                foreach (Component nt in go.GetComponentsInChildren(networkTransform, true))
                {
                    Component found = nt;
                    issues.Add(Issue(IssueSeverity.Error, $"The player \"{go.name}\" has a NetworkTransform on \"{nt.name}\": it moves the player as well as the predicted movement (stutter, cannot walk) - remove it", go, prefabPath)
                        .WithFix("Remove NetworkTransform", () => RemoveComponent(found, prefabPath)));
                }
            if (go.GetComponent<CharacterController>() == null)
                issues.Add(Issue(IssueSeverity.Warning, $"The player \"{go.name}\" has no CharacterController on its root (next to the NetworkObject): prediction moves the root", go, prefabPath));
            CheckModules(go, "Player", prefabPath, issues);
        }

        private static void CheckServerTransform(GameObject go, string what, string kind, string prefabPath, List<ValidationIssue> issues, bool required)
        {
            Type networkTransform = NetworkSetupMenu.FindType(NetworkTransformType);
            if (networkTransform == null)
                return;
            Component nt = go.GetComponent(networkTransform);
            if (nt == null)
            {
                if (required)
                    issues.Add(FixWithMake(Issue(IssueSeverity.Error, $"The {what} \"{go.name}\" has no NetworkTransform: clients will not see it move", go, prefabPath), prefabPath, kind));
                return;
            }
            var so = new SerializedObject(nt);
            SerializedProperty clientAuth = so.FindProperty("_clientAuthoritative");
            if (clientAuth != null && clientAuth.boolValue)
                issues.Add(Issue(IssueSeverity.Error, $"The NetworkTransform of the {what} \"{go.name}\" is client authoritative: the server cannot move it", go, prefabPath)
                    .WithFix("Make server authoritative", () => SetBool(nt, "_clientAuthoritative", false)));
            if (kind == "Mob")
                CheckModules(go, kind, prefabPath, issues);
        }

        private static void CheckModules(GameObject go, string kind, string prefabPath, List<ValidationIssue> issues)
        {
            if (!NetworkSetupMenu.PrefabComponents.TryGetValue(kind, out string[] typeNames))
                return;
            var missing = new List<string>();
            foreach (string name in typeNames)
            {
                Type type = NetworkSetupMenu.FindType(name);
                if (type != null && go.GetComponent(type) == null)
                    missing.Add(type.Name);
            }
            // A harvestable mob needs its carcass networked (clients could not harvest it otherwise) - also one Auto Attach
            // makes harvestable when it plays.
            if (kind == "Mob" && NetworkSetupMenu.FindType("GameNet.NetCarcass") is Type carcass)
            {
                foreach (HarvestableCreature creature in go.GetComponentsInChildren<HarvestableCreature>(true))
                    if (creature.GetComponent(carcass) == null && !missing.Contains(carcass.Name))
                        missing.Add(carcass.Name);
                if (HarvestingEditorTools.AutoAttachHost(go) != null && !missing.Contains(carcass.Name))
                    missing.Add(carcass.Name);
            }
            if (missing.Count > 0)
                issues.Add(FixWithMake(Issue(IssueSeverity.Warning, $"\"{go.name}\" ({kind}) has no {string.Join(", ", missing)}", go, prefabPath), prefabPath, kind));
        }

        private static ValidationIssue FixWithMake(ValidationIssue issue, string prefabPath, string kind) =>
            prefabPath != null ? issue.WithFix("Re-apply Make Networked", () => MakeNetworked(prefabPath, kind)) : issue;

        private static bool MakeNetworked(string prefabPath, string kind)
        {
            string result = NetworkSetupMenu.MakePrefab(prefabPath, kind);
            Debug.Log("[Validator] " + result, AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            return true;
        }

        private static bool RemoveComponent(Component component, string prefabPath)
        {
            if (component == null)
                return false;
            if (prefabPath == null)
            {
                Undo.DestroyObjectImmediate(component);
                return true;
            }
            string typeName = component.GetType().FullName;
            string relative = component.transform == component.transform.root ? "" : AnimationUtility.CalculateTransformPath(component.transform, component.transform.root);
            return ValidationUtility.EditPrefab(prefabPath, root =>
            {
                Transform t = string.IsNullOrEmpty(relative) ? root.transform : root.transform.Find(relative);
                Component c = t != null ? t.GetComponent(NetworkSetupMenu.FindType(typeName)) : null;
                if (c == null)
                    return false;
                Object.DestroyImmediate(c, true);
                return true;
            });
        }

        private static bool SetBool(Component component, string property, bool value)
        {
            if (component == null)
                return false;
            var so = new SerializedObject(component);
            SerializedProperty p = so.FindProperty(property);
            if (p == null || p.boolValue == value)
                return false;
            p.boolValue = value;
            if (EditorUtility.IsPersistent(component))
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
                AssetDatabase.SaveAssetIfDirty(component);
            }
            else
            {
                so.ApplyModifiedProperties();
            }
            return true;
        }
#endif

        private static ValidationIssue Issue(IssueSeverity severity, string message, GameObject go, string prefabPath)
        {
            string where = prefabPath != null
                ? prefabPath + " ▸ " + ValidationUtility.PathOf(go.transform)
                : (go.scene.IsValid() ? go.scene.name + " ▸ " : "") + ValidationUtility.PathOf(go.transform);
            Object context = go;
            return new ValidationIssue(severity, Category, message, context, where);
        }
    }
}
#endif
