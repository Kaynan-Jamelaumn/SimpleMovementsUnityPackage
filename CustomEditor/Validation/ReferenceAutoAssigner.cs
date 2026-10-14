#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleMovements.Validation
{
    /// <summary>Settings of an auto-assign run.</summary>
    public sealed class AutoAssignOptions
    {
        /// <summary>Only report what would be assigned; change nothing.</summary>
        public bool dryRun;
        /// <summary>Process every component under the root, not only the root's own components.</summary>
        public bool includeChildren = true;
        /// <summary>First run the components' own <c>AutoAssignReferences()</c> methods (most controllers have one).</summary>
        public bool callComponentMethods = true;
        /// <summary>
        /// When nothing is found in the object's own hierarchy, look in the rest of its scene (only a single candidate
        /// or a very clear name match is used). Never used for prefab assets.
        /// </summary>
        public bool searchScene = true;
        /// <summary>Only fill fields marked with <see cref="AutoAssignAttribute"/>.</summary>
        public bool onlyMarkedFields;

        public static AutoAssignOptions Preview => new AutoAssignOptions { dryRun = true };
    }

    public enum AssignOutcome
    {
        /// <summary>The field was filled.</summary>
        Assigned,
        /// <summary>Dry run: the field would be filled.</summary>
        WouldAssign,
        /// <summary>The component's own AutoAssignReferences() filled something.</summary>
        RanComponentMethod,
        /// <summary>Several objects fit equally well: left empty.</summary>
        Ambiguous,
        /// <summary>Nothing fits.</summary>
        NotFound,
        /// <summary>The field points to a deleted object: left alone (fix it by hand).</summary>
        MissingReference,
    }

    /// <summary>One line of an auto-assign report.</summary>
    public sealed class AssignEntry
    {
        public Component owner;
        /// <summary>Type name of <see cref="owner"/> (kept when the owner was in unloaded prefab contents).</summary>
        public string ownerType;
        public string ownerPath;
        public string propertyPath;
        public string fieldLabel;
        public Type fieldType;
        public AssignOutcome outcome;
        public Object value;
        /// <summary>Description of <see cref="value"/> (kept when the object was in prefab contents that are now unloaded).</summary>
        public string valueText;
        /// <summary>What to select for this entry (the component, or the prefab asset).</summary>
        public Object context;
        public string reason;
        public bool required;
        public List<Object> candidates = new List<Object>();

        public override string ToString()
        {
            string where = $"{ownerPath} ▸ {ownerType}.{fieldLabel}";
            switch (outcome)
            {
                case AssignOutcome.Assigned: return $"{where} = {valueText ?? Describe(value)} ({reason})";
                case AssignOutcome.WouldAssign: return $"{where} would be {valueText ?? Describe(value)} ({reason})";
                case AssignOutcome.RanComponentMethod: return $"{ownerPath} ▸ {ownerType}: {reason}";
                case AssignOutcome.Ambiguous: return $"{where}: {candidates.Count} candidates ({string.Join(", ", candidates.Take(5).Select(Describe))}) - left empty";
                case AssignOutcome.MissingReference: return $"{where}: points to a deleted object - assign it by hand";
                default: return $"{where}: {reason}";
            }
        }

        internal static string Describe(Object o)
        {
            if (o == null)
                return "None";
            if (o is Component c)
                return $"{c.gameObject.name} ({c.GetType().Name})";
            return o.name;
        }
    }

    /// <summary>The result of an auto-assign run.</summary>
    public sealed class AutoAssignReport
    {
        public readonly List<AssignEntry> entries = new List<AssignEntry>();
        /// <summary>Components processed.</summary>
        public int components;

        public int Count(AssignOutcome outcome) => entries.Count(e => e.outcome == outcome);

        public string Summary()
        {
            int assigned = Count(AssignOutcome.Assigned) + Count(AssignOutcome.WouldAssign);
            string verb = Count(AssignOutcome.WouldAssign) > 0 ? "would assign" : "assigned";
            return $"{components} component(s): {verb} {assigned} reference(s), {Count(AssignOutcome.RanComponentMethod)} filled by the components themselves, " +
                   $"{Count(AssignOutcome.Ambiguous)} ambiguous, {Count(AssignOutcome.NotFound)} required not found, {Count(AssignOutcome.MissingReference)} broken.";
        }

        /// <summary>Writes the report to the console; clicking a line selects its object.</summary>
        public void Log(string title, Object fallbackContext = null)
        {
            Debug.Log($"[Auto-Assign] {title}: {Summary()}", fallbackContext);
            foreach (AssignEntry e in entries)
            {
                Object context = e.context != null ? e.context : e.owner != null ? (Object)e.owner : fallbackContext;
                if (e.outcome == AssignOutcome.Ambiguous || e.outcome == AssignOutcome.MissingReference || (e.outcome == AssignOutcome.NotFound && e.required))
                    Debug.LogWarning("[Auto-Assign] " + e, context);
                else if (e.outcome != AssignOutcome.NotFound)
                    Debug.Log("[Auto-Assign] " + e, context);
            }
        }
    }

    /// <summary>A serialized object-reference field of a component.</summary>
    public struct ReferenceSlot
    {
        public string propertyPath;
        public string label;
        public FieldInfo field;
        public Type fieldType;
        public bool isEmpty;
        /// <summary>Points to an object that no longer exists ("Missing" in the inspector).</summary>
        public bool isMissing;
        public AutoAssignAttribute autoAssign;
        public RequiredReferenceAttribute required;
    }

    /// <summary>
    /// Fills empty object references of components from their own hierarchy: the components' own
    /// <c>AutoAssignReferences()</c> first, then every empty field whose object is unambiguous - the only component of the
    /// field's type nearby (same object, then children, parents, the rest of the hierarchy, optionally the scene), or the
    /// one named like the field. Never overwrites a value, never touches assets (prefabs, items, sounds) or fields marked
    /// <c>[AutoAssign(Never = true)]</c>, and Transform/GameObject fields are only filled by name.
    /// </summary>
    /// <remarks>
    /// Menus: Tools ▸ SimpleMovements ▸ Auto-Assign (selection, open scenes, prefabs, dry run), the
    /// "Auto-Assign Missing References" context menu of every component, and the Project Validator window.
    /// </remarks>
    public static class ReferenceAutoAssigner
    {
        private const string Menu = "Tools/SimpleMovements/Validate/Auto-Assign References/";
        private const string MethodName = "AutoAssignReferences";
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly Regex JsonReference = new Regex("\"([^\"]+)\":\\{\"(?:instanceID|entityId|m_InstanceID)\":(-?\\d+)\\}", RegexOptions.Compiled);

        private enum Tier { Self, Children, Parents, Hierarchy, Scene }

        /// <summary>A name this close to the field name wins over nearer candidates, and is needed for scene-wide matches.</summary>
        private const int StrongNameScore = 85;

        // ------------------------------------------------------------------ API

        /// <summary>Auto-assigns the components of <paramref name="root"/> (and of its children by default).</summary>
        public static AutoAssignReport Run(GameObject root, AutoAssignOptions options = null, AutoAssignReport report = null)
        {
            options ??= new AutoAssignOptions();
            report ??= new AutoAssignReport();
            if (root == null)
                return report;
            MonoBehaviour[] behaviours = options.includeChildren
                ? root.GetComponentsInChildren<MonoBehaviour>(true)
                : root.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour b in behaviours)
                if (b != null && ValidationUtility.IsProjectScript(b.GetType()))
                    RunOnComponent(b, options, report);
            return report;
        }

        /// <summary>Auto-assigns one component.</summary>
        public static AutoAssignReport RunOnComponent(Component component, AutoAssignOptions options = null, AutoAssignReport report = null)
        {
            options ??= new AutoAssignOptions();
            report ??= new AutoAssignReport();
            if (component == null)
                return report;
            report.components++;
            string ownerPath = ValidationUtility.PathOf(component.transform);
            bool undo = !options.dryRun && SupportsUndo(component);

            if (options.callComponentMethods && !options.dryRun)
                CallComponentMethod(component, undo, ownerPath, report);

            using var so = new SerializedObject(component);
            bool changed = false;
            foreach (ReferenceSlot slot in Slots(component, so))
            {
                if (slot.isMissing)
                {
                    report.entries.Add(new AssignEntry
                    {
                        owner = component, ownerType = component.GetType().Name, ownerPath = ownerPath, propertyPath = slot.propertyPath, fieldLabel = slot.label,
                        fieldType = slot.fieldType, outcome = AssignOutcome.MissingReference, required = slot.required != null,
                    });
                    continue;
                }
                if (!slot.isEmpty || !IsAssignable(slot, options))
                    continue;

                AssignEntry entry = Resolve(component, slot, options.searchScene);
                entry.ownerPath = ownerPath;
                if (entry.value != null)
                {
                    entry.valueText = AssignEntry.Describe(entry.value);
                    if (options.dryRun)
                    {
                        entry.outcome = AssignOutcome.WouldAssign;
                    }
                    else
                    {
                        so.FindProperty(slot.propertyPath).objectReferenceValue = entry.value;
                        changed = true;
                    }
                }
                if (entry.value != null || entry.outcome == AssignOutcome.Ambiguous || entry.required)
                    report.entries.Add(entry);
            }
            if (changed)
            {
                if (undo)
                    so.ApplyModifiedProperties();
                else
                    so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
                if (EditorUtility.IsPersistent(component))
                    AssetDatabase.SaveAssetIfDirty(component);
            }
            return report;
        }

        /// <summary>Opens a prefab asset, auto-assigns it and saves it if anything changed.</summary>
        public static AutoAssignReport RunOnPrefabAsset(string path, AutoAssignOptions options = null, AutoAssignReport report = null)
        {
            options ??= new AutoAssignOptions();
            report ??= new AutoAssignReport();
            var prefabOptions = new AutoAssignOptions
            {
                dryRun = options.dryRun,
                includeChildren = true,
                callComponentMethods = options.callComponentMethods,
                searchScene = false, // a prefab cannot reference scene objects
                onlyMarkedFields = options.onlyMarkedFields,
            };
            if (options.dryRun)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return asset != null ? Run(asset, prefabOptions, report) : report;
            }
            int before = report.entries.Count;
            ValidationUtility.EditPrefab(path, root =>
            {
                Run(root, prefabOptions, report);
                for (int i = before; i < report.entries.Count; i++)
                {
                    AssignEntry e = report.entries[i];
                    e.ownerPath = path + " ▸ " + e.ownerPath;
                    e.owner = null; // destroyed with the contents
                }
                return report.entries.Skip(before).Any(e => e.outcome == AssignOutcome.Assigned || e.outcome == AssignOutcome.RanComponentMethod);
            });
            // The contents were unloaded: point the entries at the asset instead of destroyed objects.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            for (int i = before; i < report.entries.Count; i++)
            {
                AssignEntry e = report.entries[i];
                e.context = prefab;
                e.value = null;
                e.candidates.Clear();
            }
            return report;
        }

        /// <summary>
        /// Finds the object for one field without assigning it: <see cref="AssignEntry.value"/> is set when one object
        /// clearly fits, otherwise <see cref="AssignEntry.outcome"/> says why not.
        /// </summary>
        public static AssignEntry Resolve(Component owner, ReferenceSlot slot, bool searchScene)
        {
            var entry = new AssignEntry
            {
                owner = owner,
                ownerType = owner != null ? owner.GetType().Name : "?",
                propertyPath = slot.propertyPath,
                fieldLabel = slot.label,
                fieldType = slot.fieldType,
                outcome = AssignOutcome.NotFound,
                required = slot.required != null,
                reason = "nothing found",
            };
            Type type = slot.fieldType;
            if (type == null)
                return entry;
            bool isGameObject = type == typeof(GameObject);
            if (!isGameObject && !typeof(Component).IsAssignableFrom(type))
            {
                entry.reason = "not a scene object type";
                return entry;
            }
            // An explicit [AutoAssign(...)] says exactly where to look; plain fields may also use the scene.
            AutoAssignFrom from = slot.autoAssign != null ? slot.autoAssign.From : AutoAssignFrom.Nearby;
            if (searchScene && slot.autoAssign == null)
                from |= AutoAssignFrom.Scene;
            string fieldName = slot.field != null ? slot.field.Name : slot.label;
            bool requireName = isGameObject || typeof(Transform).IsAssignableFrom(type) || type == typeof(Component)
                || type == typeof(Behaviour) || type == typeof(MonoBehaviour) || ReferenceMatcher.NeedsNameMatch(fieldName);
            string childName = slot.autoAssign?.ChildName;

            // Candidates per tier, nearest first; the scene is only gathered when the nearer tiers did not decide.
            var seen = new HashSet<Object>();
            var tiers = new List<KeyValuePair<Tier, List<Object>>>();
            foreach (Tier tier in Tiers(from))
                if (tier != Tier.Scene)
                    tiers.Add(new KeyValuePair<Tier, List<Object>>(tier, Gather(owner, type, tier).Where(o => o != null && o != owner && seen.Add(o)).ToList()));
            bool sawCandidates = tiers.Any(t => t.Value.Count > 0);

            if (string.IsNullOrEmpty(childName))
            {
                // An object named like the field wins over a lone candidate closer by ("hitbox" takes the child "Hitbox",
                // not the body collider on the same object).
                List<Object> nearby = tiers.SelectMany(t => t.Value).ToList();
                if (nearby.Count > 1)
                {
                    var named = nearby.Select(o => new ReferenceCandidate(GameObjectOf(o).name, o.GetType().Name)).ToList();
                    MatchResult strong = ReferenceMatcher.Pick(named, fieldName, type.Name, requireName: true);
                    if (strong.kind == MatchKind.NameMatch && strong.score >= StrongNameScore)
                    {
                        entry.value = nearby[strong.index];
                        entry.outcome = AssignOutcome.Assigned;
                        entry.reason = $"named like the field ({strong.score}%)";
                        return entry;
                    }
                }
            }

            if ((from & AutoAssignFrom.Scene) != 0)
                tiers.Add(new KeyValuePair<Tier, List<Object>>(Tier.Scene, null));
            foreach (KeyValuePair<Tier, List<Object>> pair in tiers)
            {
                Tier tier = pair.Key;
                List<Object> found = pair.Value ?? Gather(owner, type, tier).Where(o => o != null && o != owner && seen.Add(o)).ToList();
                if (found.Count == 0)
                    continue;
                sawCandidates = true;

                if (!string.IsNullOrEmpty(childName))
                {
                    string wanted = ReferenceMatcher.Normalize(childName);
                    Object named = found.FirstOrDefault(o => ReferenceMatcher.Normalize(GameObjectOf(o).name) == wanted);
                    if (named != null)
                    {
                        entry.value = named;
                        entry.outcome = AssignOutcome.Assigned;
                        entry.reason = $"named \"{childName}\" ({TierName(tier)})";
                        return entry;
                    }
                    continue;
                }

                var candidates = found.Select(o => new ReferenceCandidate(GameObjectOf(o).name, o.GetType().Name)).ToList();
                MatchResult match = ReferenceMatcher.Pick(candidates, fieldName, type.Name, requireName);
                // The rest of the scene is a wide net: only a lone candidate or a very clear name.
                if (tier == Tier.Scene && match.kind == MatchKind.NameMatch && match.score < StrongNameScore)
                    match.kind = MatchKind.Ambiguous;
                if (match.Found && match.kind != MatchKind.Ambiguous)
                {
                    entry.value = found[match.index];
                    entry.outcome = AssignOutcome.Assigned;
                    entry.reason = $"{match} on {TierName(tier)}";
                    return entry;
                }
                if (match.kind == MatchKind.Ambiguous)
                {
                    entry.outcome = AssignOutcome.Ambiguous;
                    entry.candidates = found;
                    entry.reason = $"ambiguous on {TierName(tier)}";
                    return entry;
                }
            }
            entry.reason = !string.IsNullOrEmpty(childName) ? $"no object named \"{childName}\""
                : sawCandidates ? "no object named like the field" : $"no {type.Name} found";
            return entry;
        }

        /// <summary>The object-reference fields of a component that the inspector shows (arrays and lists excluded).</summary>
        public static List<ReferenceSlot> Slots(Component component, SerializedObject so = null)
        {
            var slots = new List<ReferenceSlot>();
            if (component == null)
                return slots;
            bool ownsObject = so == null;
            so ??= new SerializedObject(component);
            try
            {
                CollectSlots(component, so, slots);
            }
            finally
            {
                if (ownsObject)
                    so.Dispose();
            }
            return slots;
        }

        private static void CollectSlots(Component component, SerializedObject so, List<ReferenceSlot> slots)
        {
            HashSet<string> missing = null;
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = it.propertyType == SerializedPropertyType.Generic && !it.isArray;
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.propertyPath == "m_Script" || it.propertyPath.Contains(".Array."))
                    continue;
                FieldInfo field = FieldOf(component.GetType(), it.propertyPath);
                Type fieldType = field != null ? field.FieldType : null;
                bool empty = it.objectReferenceValue == null;
                bool isMissing = false;
                if (empty)
                {
                    missing ??= MissingFieldNames(component);
                    isMissing = missing.Contains(it.name);
                }
                slots.Add(new ReferenceSlot
                {
                    propertyPath = it.propertyPath,
                    label = it.displayName,
                    field = field,
                    fieldType = fieldType,
                    isEmpty = empty && !isMissing,
                    isMissing = isMissing,
                    autoAssign = field?.GetCustomAttribute<AutoAssignAttribute>(true),
                    required = field?.GetCustomAttribute<RequiredReferenceAttribute>(true),
                });
            }
        }

        /// <summary>Whether the auto-assigner may fill this field at all (scene object types, not prefabs or assets).</summary>
        public static bool IsAssignable(ReferenceSlot slot, AutoAssignOptions options = null)
        {
            if (slot.fieldType == null || (slot.autoAssign != null && slot.autoAssign.Never))
                return false;
            if (options != null && options.onlyMarkedFields && slot.autoAssign == null)
                return false;
            if (slot.fieldType != typeof(GameObject) && !typeof(Component).IsAssignableFrom(slot.fieldType))
                return false;
            if (slot.autoAssign != null)
                return true;
            string name = slot.field != null ? slot.field.Name : slot.label;
            // Prefab and template references point at assets, never at objects of the hierarchy.
            return name.IndexOf("prefab", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("template", StringComparison.OrdinalIgnoreCase) < 0;
        }

        // ------------------------------------------------------------------ menus

        [MenuItem(SMMenu.Root + "Auto-Assign/References In Selection", false, SMMenu.AutoAssign + 0)]
        private static void AssignSelection() => RunOnSelection(new AutoAssignOptions());

        [MenuItem(SMMenu.Root + "Auto-Assign/Preview In Selection (changes nothing)", false, SMMenu.AutoAssign + 1)]
        private static void PreviewSelection() => RunOnSelection(AutoAssignOptions.Preview);

        [MenuItem(SMMenu.Root + "Auto-Assign/References In Open Scenes", false, SMMenu.AutoAssign + 20)]
        private static void AssignScenes()
        {
            if (!EditorUtility.DisplayDialog("Auto-Assign References", "Fill the empty references of every component in the open scenes?\n\nExisting values are never changed and everything can be undone.", "Assign", "Cancel"))
                return;
            RunOnOpenScenes(new AutoAssignOptions()).Log("Open scenes");
        }

        [MenuItem(SMMenu.Root + "Auto-Assign/Preview In Open Scenes (changes nothing)", false, SMMenu.AutoAssign + 21)]
        private static void PreviewScenes() => RunOnOpenScenes(AutoAssignOptions.Preview).Log("Open scenes (preview)");

        [MenuItem(SMMenu.Root + "Auto-Assign/References In All Prefabs", false, SMMenu.AutoAssign + 40)]
        private static void AssignAllPrefabs()
        {
            string[] paths = ValidationUtility.ProjectPrefabPaths();
            if (!EditorUtility.DisplayDialog("Auto-Assign References", $"Fill the empty references of {paths.Length} prefab(s) under Assets?\n\nExisting values are never changed. Prefab changes cannot be undone with Ctrl+Z (use version control).", "Assign", "Cancel"))
                return;
            var report = new AutoAssignReport();
            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Auto-Assign References", paths[i], (float)i / paths.Length))
                        break;
                    RunOnPrefabAsset(paths[i], new AutoAssignOptions(), report);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
            report.Log("All prefabs");
        }

        [MenuItem("CONTEXT/MonoBehaviour/Auto-Assign Missing References", false, 1000)]
        private static void AssignContext(MenuCommand command)
        {
            if (command.context is Component c)
            {
                int undoGroup = BeginUndoGroup();
                AutoAssignReport report = RunOnComponent(c, new AutoAssignOptions());
                Undo.CollapseUndoOperations(undoGroup);
                report.Log(c.GetType().Name, c);
            }
        }

        [MenuItem("CONTEXT/MonoBehaviour/Preview Auto-Assign (changes nothing)", false, 1001)]
        private static void PreviewContext(MenuCommand command)
        {
            if (command.context is Component c)
                RunOnComponent(c, AutoAssignOptions.Preview).Log(c.GetType().Name + " (preview)", c);
        }

        /// <summary>Auto-assigns the selected scene objects and prefab assets.</summary>
        public static AutoAssignReport RunOnSelection(AutoAssignOptions options)
        {
            var report = new AutoAssignReport();
            int undoGroup = BeginUndoGroup();
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Auto-Assign References", "Select objects in the Hierarchy or prefabs in the Project window first.", "OK");
                return report;
            }
            foreach (GameObject go in selected)
            {
                string assetPath = EditorUtility.IsPersistent(go) ? AssetDatabase.GetAssetPath(go) : null;
                if (!string.IsNullOrEmpty(assetPath))
                    RunOnPrefabAsset(assetPath, options, report);
                else
                    Run(go, options, report);
            }
            if (!options.dryRun)
                AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            report.Log(options.dryRun ? "Selection (preview)" : "Selection", Selection.activeObject);
            if (!options.dryRun)
                Debug.Log("[Auto-Assign] Ctrl+Z undoes the changes made to scene objects.");
            return report;
        }

        /// <summary>Auto-assigns every object of the open scenes.</summary>
        public static AutoAssignReport RunOnOpenScenes(AutoAssignOptions options)
        {
            var report = new AutoAssignReport();
            int undoGroup = BeginUndoGroup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    Run(root, options, report);
            }
            Undo.CollapseUndoOperations(undoGroup);
            return report;
        }

        /// <summary>Starts an undo group so a whole run is undone with one Ctrl+Z.</summary>
        private static int BeginUndoGroup()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Auto-Assign References");
            return Undo.GetCurrentGroup();
        }

        // ------------------------------------------------------------------ internals

        private static void CallComponentMethod(Component component, bool undo, string ownerPath, AutoAssignReport report)
        {
            MethodInfo method = component.GetType().GetMethod(MethodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (method == null)
                return;
            string before = EditorJsonUtility.ToJson(component);
            if (undo)
                Undo.RecordObject(component, "Auto-Assign References");
            try
            {
                method.Invoke(component, null);
            }
            catch (TargetInvocationException e)
            {
                Debug.LogWarning($"[Auto-Assign] {component.GetType().Name}.{MethodName}() failed: {e.InnerException?.Message}", component);
                return;
            }
            if (EditorJsonUtility.ToJson(component) == before)
                return;
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component);
            report.entries.Add(new AssignEntry
            {
                owner = component,
                ownerType = component.GetType().Name,
                ownerPath = ownerPath,
                outcome = AssignOutcome.RanComponentMethod,
                reason = $"{MethodName}() filled its references",
            });
        }

        private static IEnumerable<Tier> Tiers(AutoAssignFrom from)
        {
            if ((from & AutoAssignFrom.Self) != 0) yield return Tier.Self;
            if ((from & AutoAssignFrom.Children) != 0) yield return Tier.Children;
            if ((from & AutoAssignFrom.Parents) != 0) yield return Tier.Parents;
            if ((from & AutoAssignFrom.Hierarchy) != 0) yield return Tier.Hierarchy;
            if ((from & AutoAssignFrom.Scene) != 0) yield return Tier.Scene;
        }

        private static string TierName(Tier tier) => tier switch
        {
            Tier.Self => "the same object",
            Tier.Children => "a child",
            Tier.Parents => "a parent",
            Tier.Hierarchy => "the same hierarchy",
            _ => "the scene",
        };

        private static IEnumerable<Object> Gather(Component owner, Type type, Tier tier)
        {
            bool isGameObject = type == typeof(GameObject);
            Type componentType = isGameObject ? typeof(Transform) : type;
            Transform self = owner.transform;
            IEnumerable<Component> found;
            switch (tier)
            {
                case Tier.Self:
                    found = owner.GetComponents(componentType);
                    break;
                case Tier.Children:
                    found = owner.GetComponentsInChildren(componentType, true).Where(c => c.transform != self);
                    break;
                case Tier.Parents:
                    found = self.parent != null ? self.parent.GetComponentsInParent(componentType, true) : Array.Empty<Component>();
                    break;
                case Tier.Hierarchy:
                    found = self.root.GetComponentsInChildren(componentType, true);
                    break;
                default:
                    Scene scene = owner.gameObject.scene;
                    // A prefab asset has no scene; references across scenes are not saved, so only the object's own scene.
                    if (EditorUtility.IsPersistent(owner) || !scene.IsValid() || !scene.isLoaded)
                        return Array.Empty<Object>();
                    found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(componentType, true));
                    break;
            }
            return isGameObject ? found.Select(c => (Object)c.gameObject) : found.Cast<Object>();
        }

        private static GameObject GameObjectOf(Object o) => o is Component c ? c.gameObject : o as GameObject;

        /// <summary>Scene objects and objects open in Prefab Mode support undo; prefab assets and loaded prefab contents do not.</summary>
        private static bool SupportsUndo(Component component)
        {
            if (EditorUtility.IsPersistent(component))
                return false;
            Scene scene = component.gameObject.scene;
            if (!scene.IsValid())
                return false;
            if (!UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene))
                return true;
            UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && stage.scene == scene;
        }

        /// <summary>The field behind a property path ("settings.camera" walks into serializable classes).</summary>
        internal static FieldInfo FieldOf(Type host, string propertyPath)
        {
            FieldInfo field = null;
            Type type = host;
            foreach (string part in propertyPath.Split('.'))
            {
                if (part == "Array" || type == null)
                    return null;
                field = null;
                for (Type t = type; t != null && field == null; t = t.BaseType)
                    field = t.GetField(part, FieldFlags);
                if (field == null)
                    return null;
                type = field.FieldType;
            }
            return field;
        }

        /// <summary>
        /// Names of the fields that point to an object that no longer exists: the inspector's "Missing". Read from the
        /// component's serialized form, without the (deprecated) instance-id API of SerializedProperty.
        /// </summary>
        internal static HashSet<string> MissingFieldNames(Component component)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            string json;
            try
            {
                json = EditorJsonUtility.ToJson(component);
            }
            catch (Exception)
            {
                return names;
            }
            foreach (Match m in JsonReference.Matches(json))
                if (m.Groups[2].Value != "0")
                    names.Add(m.Groups[1].Value);
            return names;
        }

        internal static string Describe(AutoAssignReport report)
        {
            var sb = new StringBuilder(report.Summary());
            foreach (AssignEntry e in report.entries.Take(40))
                sb.Append("\n• ").Append(e);
            if (report.entries.Count > 40)
                sb.Append("\n…");
            return sb.ToString();
        }
    }
}
#endif
