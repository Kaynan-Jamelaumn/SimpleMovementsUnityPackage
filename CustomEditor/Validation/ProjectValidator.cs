#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleMovements.Validation
{
    /// <summary>What a validation run looks at.</summary>
    public enum ValidationScope
    {
        /// <summary>The selected scene objects and prefab assets.</summary>
        Selection,
        /// <summary>Every object of the open scenes.</summary>
        OpenScenes,
        /// <summary>Every prefab under Assets/.</summary>
        AllPrefabs,
        /// <summary>The open scenes and every prefab.</summary>
        Everything,
    }

    /// <summary>Which checks a validation run makes.</summary>
    [Flags]
    public enum ValidationChecks
    {
        /// <summary>Components whose script was deleted or renamed ("The referenced script is missing").</summary>
        MissingScripts = 1,
        /// <summary>Fields pointing to deleted objects ("Missing" in the inspector).</summary>
        BrokenReferences = 2,
        /// <summary>Empty fields marked <see cref="RequiredReferenceAttribute"/>.</summary>
        RequiredReferences = 4,
        /// <summary>Empty fields the auto-assigner can fill without doubt (information, with a fix).</summary>
        AssignableReferences = 8,
        /// <summary>Network prefabs and network components (needs Fish-Net).</summary>
        Network = 16,
        /// <summary>Settings and databases the package expects in Resources.</summary>
        ProjectAssets = 32,
        /// <summary>The Input Actions asset (every action the scripts read) and the open scenes' setup (weather, day/night, relays).</summary>
        GameSetup = 64,
        /// <summary>Every mob's setup: components, references, physics, animation, profile, attacks, drops (Mob Setup Validator).</summary>
        Mobs = 128,
    }

    /// <summary>
    /// Checks scenes and prefabs for the problems that break a setup silently: missing scripts, references to deleted
    /// objects, empty required references, references that could be filled automatically, network prefab mistakes and
    /// missing project assets. Each issue says where it is and, when possible, offers a fix. Use the Project Validator
    /// window (Window ▸ SimpleMovements ▸ Project Validator) or the Tools ▸ SimpleMovements ▸ Validate menu.
    /// </summary>
    public static class ProjectValidator
    {
        private const string Menu = "Tools/SimpleMovements/Validate/";

        /// <summary>Every check.</summary>
        public const ValidationChecks AllChecks = ValidationChecks.MissingScripts | ValidationChecks.BrokenReferences
            | ValidationChecks.RequiredReferences | ValidationChecks.AssignableReferences | ValidationChecks.Network | ValidationChecks.ProjectAssets
            | ValidationChecks.GameSetup | ValidationChecks.Mobs;

        /// <summary>Every check except the (informative) assignable references.</summary>
        public const ValidationChecks DefaultChecks = AllChecks & ~ValidationChecks.AssignableReferences;

        /// <summary>Extra checks run on each root object (scene root or prefab asset root). The network validator registers one.</summary>
        public static event Action<GameObject, List<ValidationIssue>> ValidateRoot;

        /// <summary>Extra checks run on each root object with the checks of the run (the mob validator registers one).</summary>
        public static event Action<GameObject, ValidationChecks, List<ValidationIssue>> ValidateRootWithChecks;

        /// <summary>Extra project-wide checks (run once per validation, when <see cref="ValidationChecks.ProjectAssets"/> or Network is on).</summary>
        public static event Action<ValidationChecks, List<ValidationIssue>> ValidateProject;

        // ------------------------------------------------------------------ API

        /// <summary>Runs the checks on a scope. Shows a progress bar for long runs; can be cancelled.</summary>
        public static List<ValidationIssue> Run(ValidationScope scope, ValidationChecks checks = DefaultChecks)
        {
            var issues = new List<ValidationIssue>();
            try
            {
                if (scope == ValidationScope.Selection)
                {
                    foreach (GameObject go in Selection.gameObjects)
                    {
                        string path = EditorUtility.IsPersistent(go) ? AssetDatabase.GetAssetPath(go) : null;
                        ValidateGameObject(!string.IsNullOrEmpty(path) ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : go, checks, issues);
                    }
                }
                if (scope == ValidationScope.OpenScenes || scope == ValidationScope.Everything)
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        Scene scene = SceneManager.GetSceneAt(i);
                        if (!scene.isLoaded)
                            continue;
                        foreach (GameObject root in scene.GetRootGameObjects())
                            ValidateGameObject(root, checks, issues);
                    }
                }
                if (scope == ValidationScope.AllPrefabs || scope == ValidationScope.Everything)
                {
                    string[] paths = ValidationUtility.ProjectPrefabPaths();
                    for (int i = 0; i < paths.Length; i++)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar("Validating prefabs", paths[i], (float)i / Math.Max(1, paths.Length)))
                        {
                            issues.Add(new ValidationIssue(IssueSeverity.Info, "Validator", $"Cancelled after {i} of {paths.Length} prefabs."));
                            break;
                        }
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                        if (prefab != null)
                            ValidateGameObject(prefab, checks, issues);
                    }
                }
                if ((checks & (ValidationChecks.ProjectAssets | ValidationChecks.Network | ValidationChecks.GameSetup)) != 0)
                {
                    if ((checks & ValidationChecks.ProjectAssets) != 0)
                        CheckProjectAssets(issues);
                    ValidateProject?.Invoke(checks, issues);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            issues.Sort((a, b) => b.severity.CompareTo(a.severity));
            return issues;
        }

        /// <summary>
        /// Checks one object and everything under it (a scene root, a prefab asset or any object).
        /// <paramref name="searchScene"/>: fixes for empty references of scene objects may use objects elsewhere in their
        /// scene (off = only their own hierarchy).
        /// </summary>
        public static void ValidateGameObject(GameObject root, ValidationChecks checks, List<ValidationIssue> issues, bool searchScene = true)
        {
            if (root == null)
                return;
            string prefabPath = ValidationUtility.PrefabAssetPath(root);

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                string where = prefabPath != null ? prefabPath + " ▸ " + ValidationUtility.PathOf(t) : ScenePath(go);
                Object context = go;

                if ((checks & ValidationChecks.MissingScripts) != 0)
                {
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (count > 0)
                        issues.Add(MissingScriptIssue(go, count, prefabPath, where, context));
                }

                if ((checks & (ValidationChecks.BrokenReferences | ValidationChecks.RequiredReferences | ValidationChecks.AssignableReferences)) == 0)
                    continue;
                foreach (MonoBehaviour b in go.GetComponents<MonoBehaviour>())
                {
                    if (b == null || !ValidationUtility.IsProjectScript(b.GetType()))
                        continue;
                    CheckReferences(b, checks, where, issues, searchScene && prefabPath == null);
                }
            }

            if ((checks & ValidationChecks.Network) != 0)
                ValidateRoot?.Invoke(root, issues);
            ValidateRootWithChecks?.Invoke(root, checks, issues);
        }

        /// <summary>Applies every available fix. Returns how many changed something.</summary>
        public static int FixAll(IEnumerable<ValidationIssue> issues)
        {
            int fixedCount = 0;
            var list = issues.Where(i => i.CanFix).ToList();
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Fixing", list[i].message, (float)i / list.Count))
                        break;
                    if (TryFix(list[i]))
                        fixedCount++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
            return fixedCount;
        }

        /// <summary>Applies one fix, logging (not throwing) failures.</summary>
        public static bool TryFix(ValidationIssue issue)
        {
            if (issue == null || issue.fix == null)
                return false;
            try
            {
                bool changed = issue.fix();
                if (changed)
                    issue.fix = null;
                return changed;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Validator] Could not fix \"{issue.message}\": {e.Message}", issue.context);
                return false;
            }
        }

        // ------------------------------------------------------------------ checks

        private static ValidationIssue MissingScriptIssue(GameObject go, int count, string prefabPath, string where, Object context)
        {
            var issue = new ValidationIssue(IssueSeverity.Error, "Missing script",
                $"{count} component(s) whose script no longer exists on \"{go.name}\"", context, where);
            if (prefabPath != null)
            {
                // Inside a prefab asset: open the prefab, remove them on the same object, save.
                string relative = RelativePath(go.transform);
                return issue.WithFix("Remove", () => ValidationUtility.EditPrefab(prefabPath, root =>
                {
                    Transform target = string.IsNullOrEmpty(relative) ? root.transform : root.transform.Find(relative);
                    return target != null && GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target.gameObject) > 0;
                }));
            }
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                // Components that come from the prefab asset can only be removed there.
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                issue.message += string.IsNullOrEmpty(source) ? "" : $" (if they come from the prefab, fix {source})";
            }
            return issue.WithFix("Remove", () =>
            {
                if (go == null)
                    return false;
                Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                return GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go) > 0;
            });
        }

        private static void CheckReferences(MonoBehaviour behaviour, ValidationChecks checks, string where, List<ValidationIssue> issues, bool searchScene)
        {
            List<ReferenceSlot> slots = ReferenceAutoAssigner.Slots(behaviour);
            string type = behaviour.GetType().Name;
            foreach (ReferenceSlot slot in slots)
            {
                if (slot.isMissing)
                {
                    if ((checks & ValidationChecks.BrokenReferences) != 0)
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, "Broken reference",
                            $"{type}.{slot.label} points to an object that was deleted", behaviour, where));
                    continue;
                }
                if (!slot.isEmpty)
                    continue;
                bool required = slot.required != null;
                bool wantAssignable = (checks & ValidationChecks.AssignableReferences) != 0;
                if (!(required && (checks & ValidationChecks.RequiredReferences) != 0) && !wantAssignable)
                    continue;

                AssignEntry match = ReferenceAutoAssigner.IsAssignable(slot)
                    ? ReferenceAutoAssigner.Resolve(behaviour, slot, searchScene && !EditorUtility.IsPersistent(behaviour))
                    : null;
                bool canFill = match != null && match.value != null;
                if (required && (checks & ValidationChecks.RequiredReferences) != 0)
                {
                    string reason = string.IsNullOrEmpty(slot.required.Reason) ? "" : $" ({slot.required.Reason})";
                    var issue = new ValidationIssue(IssueSeverity.Error, "Required reference", $"{type}.{slot.label} is empty{reason}", behaviour, where);
                    if (canFill)
                        issue.WithFix("Assign " + AssignEntry.Describe(match.value), () => Assign(behaviour, slot.propertyPath, match.value));
                    issues.Add(issue);
                }
                else if (canFill)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Info, "Assignable reference",
                            $"{type}.{slot.label} is empty; {AssignEntry.Describe(match.value)} fits ({match.reason})", behaviour, where)
                        .WithFix("Assign", () => Assign(behaviour, slot.propertyPath, match.value)));
                }
            }
        }

        /// <summary>Settings and databases the package looks for in Resources.</summary>
        private static void CheckProjectAssets(List<ValidationIssue> issues)
        {
            const string setup = "Tools/SimpleMovements/Project Setup/";
            CheckResource<CombatSettings>(issues, SMAssets.Settings, "CombatSettings", "Combat Settings", setup + "Create Combat Settings (Resources)");
            CheckResource<AbsorptionSettings>(issues, SMAssets.Settings, "AbsorptionSettings", "Absorption Settings", setup + "Create Absorption Settings (Resources)");
            CheckResource<AbilityDatabase>(issues, SMAssets.Databases, "AbilityDatabase", "Ability Database", setup + "Create Ability Database (Resources)");
            CheckResource<TraitDatabase>(issues, SMAssets.Databases, "TraitDatabase", "Trait Database", setup + "Create Trait Database (Resources)");
            CheckResource<ItemCategoryDatabase>(issues, SMAssets.Databases, ItemCategoryDatabase.ResourcesName, "Item Category Database", setup + "Create Item Category Database (Resources)");
            CheckResource<CurrencyDefinition>(issues, SMAssets.Economy, CurrencyDefinition.ResourcesName, "Default Currency", setup + "Create Default Currency (Resources)");
            CheckResource<HubWorld.HubWorldSettings>(issues, SMAssets.Settings, "HubWorldSettings", "Hub World Settings", setup + "Create Hub World Settings (Resources)");
            CheckInjurySettings(issues);
            CheckAlchemyDatabase(issues);
            if (SMAssets.Load<ItemDatabase>(SMAssets.Databases, ItemDatabase.ResourcesName) == null)
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Project",
                    $"No {SMAssets.ResourcePath(SMAssets.Databases, ItemDatabase.ResourcesName)}: items have no stable ids (needed for network sessions and saves)")
                    .WithFix("Build", () => EditorApplication.ExecuteMenuItem("Tools/SimpleMovements/Project Setup/Build Item Database")));
            int toMove = SMAssetsEditor.PlanMoves().Count;
            if (toMove > 0)
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Project",
                        $"{toMove} generated asset(s) are where older versions put them (Resources root, Assets/Generated...): " +
                        $"they work, but can be moved into {SMAssets.ProjectRoot}")
                    .WithFix("Organise", () => { SMAssetsEditor.OrganiseMenu(); return true; }));
            CheckScenesInBuild(issues);
        }

        private static void CheckResource<T>(List<ValidationIssue> issues, string folder, string resourceName, string label, string createMenu) where T : Object
        {
            if (SMAssets.Load<T>(folder, resourceName) != null)
                return;
            var issue = new ValidationIssue(IssueSeverity.Info, "Project",
                $"No Resources/{SMAssets.ResourcePath(folder, resourceName)} ({label}): built-in defaults are used");
            if (!string.IsNullOrEmpty(createMenu))
                issue.WithFix("Create", () => EditorApplication.ExecuteMenuItem(createMenu));
            issues.Add(issue);
        }

        /// <summary>The injury system's settings (optional: without them the system is off).</summary>
        private static void CheckInjurySettings(List<ValidationIssue> issues)
        {
            InjurySettings s = SMAssets.Load<InjurySettings>(SMAssets.Settings, InjurySettings.ResourcesName);
            if (s == null || !s.enabled)
                return;
            var errors = new List<string>();
            var warnings = new List<string>();
            s.Validate(errors, warnings);
            foreach (string e in errors)
                issues.Add(new ValidationIssue(IssueSeverity.Error, "Injuries", e, s));
            foreach (string w in warnings)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Injuries", w, s));
            bool missingLimbItems = s.bodyParts.Any(p => p != null && p.limbLoss != null && p.limbLoss.canBeLost && p.limbLoss.limbItem == null);
            if (missingLimbItems)
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Injuries",
                        "Some body parts that can be lost have no Severed Limb item: limbs are made at runtime offline, but a network session cannot drop them", s)
                    .WithFix("Create Limb Items", () => InjuryEditorTools.CreateLimbItems(s, false) > 0));
            if (InjuryEditorTools.FindMedicalItems().Count == 0)
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Injuries", "The injury system is on but the project has no medical items (bandages, splints...)", s)
                    .WithFix("Create Presets", () => InjuryEditorTools.CreateMedicalItems(false).Count > 0));
        }

        /// <summary>The alchemy rules (optional: without a database the built-in alchemy is used).</summary>
        private static void CheckAlchemyDatabase(List<ValidationIssue> issues)
        {
            AlchemyDatabase db = SMAssets.Load<AlchemyDatabase>(SMAssets.Settings, AlchemyDatabase.ResourcesName);
            if (db == null)
                return;
            var errors = new List<string>();
            var warnings = new List<string>();
            db.MarkChanged();
            db.Validate(errors, warnings);
            foreach (string e in errors)
                issues.Add(new ValidationIssue(IssueSeverity.Error, "Alchemy", e, db));
            foreach (string w in warnings)
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "Alchemy", w, db));
            bool missingItems = db.outcomes.Any(o => o != null && o.producesItems && o.tiers != null && o.tiers.Any(t => t != null && t.item == null));
            if (missingItems)
                issues.Add(new ValidationIssue(IssueSeverity.Info, "Alchemy",
                        "Some outcome tiers have no item: bottles are made at runtime offline, but a network session or a save cannot carry them", db)
                    .WithFix("Create Potion Items", () => AlchemyEditorTools.CreatePotionItems(db, false) > 0));
        }

        private static void CheckScenesInBuild(List<ValidationIssue> issues)
        {
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (!s.enabled)
                    continue;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) == null)
                {
                    string path = s.path;
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, "Build settings", $"The build lists a scene that does not exist: {path}")
                        .WithFix("Remove from build", () =>
                        {
                            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(x => x.path != path).ToArray();
                            return true;
                        }));
                }
            }
        }

        internal static bool Assign(Component owner, string propertyPath, Object value)
        {
            if (owner == null || value == null)
                return false;
            var so = new SerializedObject(owner);
            SerializedProperty p = so.FindProperty(propertyPath);
            if (p == null || p.objectReferenceValue != null)
                return false;
            p.objectReferenceValue = value;
            if (EditorUtility.IsPersistent(owner))
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(owner);
                AssetDatabase.SaveAssetIfDirty(owner);
            }
            else
            {
                so.ApplyModifiedProperties();
            }
            return true;
        }

        private static string ScenePath(GameObject go) =>
            (go.scene.IsValid() ? go.scene.name + " ▸ " : "") + ValidationUtility.PathOf(go.transform);

        /// <summary>Path below the root ("Model/Hand_R"), empty for the root itself (for Transform.Find in prefab contents).</summary>
        private static string RelativePath(Transform t)
        {
            if (t.parent == null)
                return "";
            string path = t.name;
            for (Transform p = t.parent; p.parent != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }

        // ------------------------------------------------------------------ menus

        [MenuItem(SMMenu.Root + "Validate/Selection", false, SMMenu.Validate + 0)]
        private static void ValidateSelection() => RunAndReport(ValidationScope.Selection);

        [MenuItem(SMMenu.Root + "Validate/Open Scenes", false, SMMenu.Validate + 1)]
        private static void ValidateScenes() => RunAndReport(ValidationScope.OpenScenes);

        [MenuItem(SMMenu.Root + "Validate/All Prefabs", false, SMMenu.Validate + 2)]
        private static void ValidatePrefabs() => RunAndReport(ValidationScope.AllPrefabs);

        [MenuItem(SMMenu.Root + "Validate/Remove Missing Scripts/In Selection", false, SMMenu.Validate + 60)]
        private static void RemoveMissingSelection() => RemoveMissing(ValidationScope.Selection);

        [MenuItem(SMMenu.Root + "Validate/Remove Missing Scripts/In Open Scenes", false, SMMenu.Validate + 61)]
        private static void RemoveMissingScenes() => RemoveMissing(ValidationScope.OpenScenes);

        [MenuItem(SMMenu.Root + "Validate/Remove Missing Scripts/In All Prefabs", false, SMMenu.Validate + 62)]
        private static void RemoveMissingPrefabs() => RemoveMissing(ValidationScope.AllPrefabs);

        private static void RunAndReport(ValidationScope scope)
        {
            List<ValidationIssue> issues = Run(scope);
            foreach (ValidationIssue issue in issues)
                issue.Log();
            int errors = issues.Count(i => i.severity == IssueSeverity.Error);
            int warnings = issues.Count(i => i.severity == IssueSeverity.Warning);
            string summary = issues.Count == 0 ? "No problems found." : $"{errors} error(s), {warnings} warning(s), {issues.Count - errors - warnings} note(s) - see the Console, or open the Project Validator window to fix them.";
            Debug.Log($"[Validator] {scope}: {summary}");
            if (issues.Count > 0 && EditorUtility.DisplayDialog("Validation", summary, "Open Project Validator", "Close"))
                ValidationWindow.Open(scope);
        }

        private static void RemoveMissing(ValidationScope scope)
        {
            List<ValidationIssue> issues = Run(scope, ValidationChecks.MissingScripts);
            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Missing scripts", "No missing scripts found.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Missing scripts", $"Remove the missing scripts of {issues.Count} object(s)?\n\n" +
                string.Join("\n", issues.Take(15).Select(i => i.objectPath)) + (issues.Count > 15 ? "\n…" : ""), "Remove", "Cancel"))
                return;
            int fixedCount = FixAll(issues);
            Debug.Log($"[Validator] Removed missing scripts from {fixedCount} of {issues.Count} object(s).");
        }
    }
}
#endif
