#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SimpleMovements.Setup
{
    /// <summary>How much a scene or prefab needs one requirement.</summary>
    public enum SetupLevel
    {
        /// <summary>It does not work without it.</summary>
        Required,
        /// <summary>A normal game has it; leaving it out loses a feature or causes a known problem.</summary>
        Recommended,
        /// <summary>Only for some games (a feature you may not use).</summary>
        Optional,
    }

    /// <summary>What a check found.</summary>
    public enum SetupStatus
    {
        /// <summary>Present and set up correctly.</summary>
        Ok,
        /// <summary>Not there at all.</summary>
        Missing,
        /// <summary>There, but set up in a way that does not work (or works badly).</summary>
        Wrong,
        /// <summary>Nothing to do: a note about how this scene / prefab is used.</summary>
        Note,
        /// <summary>Does not apply here (a module is not installed, the feature is off...).</summary>
        NotApplicable,
    }

    /// <summary>The result of one check: status, a sentence about what was found, and the object to select.</summary>
    public readonly struct SetupResult
    {
        public readonly SetupStatus status;
        public readonly string detail;
        public readonly Object target;

        public SetupResult(SetupStatus status, string detail, Object target = null)
        {
            this.status = status;
            this.detail = detail;
            this.target = target;
        }

        public bool IsProblem => status == SetupStatus.Missing || status == SetupStatus.Wrong;

        public static SetupResult Ok(string detail = null, Object target = null) => new SetupResult(SetupStatus.Ok, detail, target);
        public static SetupResult Missing(string detail = null, Object target = null) => new SetupResult(SetupStatus.Missing, detail, target);
        public static SetupResult Wrong(string detail, Object target = null) => new SetupResult(SetupStatus.Wrong, detail, target);
        public static SetupResult Note(string detail, Object target = null) => new SetupResult(SetupStatus.Note, detail, target);
        public static SetupResult Skip(string detail) => new SetupResult(SetupStatus.NotApplicable, detail);

        /// <summary>Ok when <paramref name="good"/>, else Missing.</summary>
        public static SetupResult Present(bool good, string okDetail, string missingDetail, Object target = null) =>
            good ? Ok(okDetail, target) : Missing(missingDetail, target);
    }

    /// <summary>
    /// One thing a scene or prefab needs: what it is (<see cref="Title"/>), how much it is needed (<see cref="Level"/>),
    /// what it does and why (<see cref="Why"/>), how to set it up by hand (<see cref="How"/>), the check, and - when it can
    /// be done automatically - the fix.
    /// </summary>
    public sealed class SetupRequirement
    {
        public string Group;
        public string Title;
        public SetupLevel Level;
        /// <summary>What it is for and what goes wrong without it.</summary>
        public string Why;
        /// <summary>How to do it by hand (shown next to, or instead of, the fix button).</summary>
        public string How;
        public Func<SetupContext, SetupResult> Check;
        public string FixLabel;
        /// <summary>Makes the change; returns whether something changed.</summary>
        public Func<SetupContext, bool> Fix;
        /// <summary>
        /// The fix changes the prefab asset itself (it loads and saves it): it gets the asset's root, never loaded
        /// contents. Off = the fix edits <see cref="SetupContext.Root"/>, which the runner loads and saves for it.
        /// </summary>
        public bool FixesAsset;
        /// <summary>Included in "Fix all" (off for fixes that open dialogs or need choices).</summary>
        public bool InFixAll = true;

        public bool CanFix => Fix != null;

        public static SetupRequirement Make(string group, string title, SetupLevel level) =>
            new SetupRequirement { Group = group, Title = title, Level = level };

        public SetupRequirement Explain(string why, string how = null)
        {
            Why = why;
            How = how;
            return this;
        }

        public SetupRequirement Checks(Func<SetupContext, SetupResult> check)
        {
            Check = check;
            return this;
        }

        public SetupRequirement Fixes(string label, Func<SetupContext, bool> fix, bool fixesAsset = false, bool inFixAll = true)
        {
            FixLabel = label;
            Fix = fix;
            FixesAsset = fixesAsset;
            InFixAll = inFixAll;
            return this;
        }

        /// <summary>Runs the check, turning exceptions into a Wrong result instead of breaking the window.</summary>
        public SetupResult Evaluate(SetupContext context)
        {
            if (Check == null)
                return SetupResult.Note(How ?? "");
            try
            {
                return Check(context);
            }
            catch (Exception e)
            {
                return SetupResult.Wrong($"The check failed: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    /// <summary>What is being set up.</summary>
    public enum SetupTargetKind
    {
        Scene,
        Prefab,
    }

    /// <summary>
    /// A kind of scene or prefab the package needs (the Bootstrap scene, the player prefab...): what it is for, where it
    /// usually is, how to create one, and every requirement.
    /// </summary>
    public sealed class SetupProfile
    {
        public string Id;
        public string Name;
        public SetupTargetKind Kind;
        /// <summary>One line for lists.</summary>
        public string Summary;
        /// <summary>What it is, how it fits with the rest, in a few sentences.</summary>
        public string Description;
        /// <summary>Guide under Documentation/ (e.g. "Networking/01-Architecture-and-Local-Setup.md").</summary>
        public string DocPath;
        public readonly List<SetupRequirement> Requirements = new List<SetupRequirement>();
        /// <summary>The project's asset of this kind, found automatically (null = none found).</summary>
        public Func<string> FindDefault;
        /// <summary>Prefabs: does this prefab look like one of this kind (for the picker)?</summary>
        public Func<GameObject, bool> IsCandidate;
        /// <summary>Creates the scene / prefab when the project has none (null = no automatic creation).</summary>
        public string CreateLabel;
        public Action Create;

        public SetupProfile Add(SetupRequirement r)
        {
            Requirements.Add(r);
            return this;
        }
    }

    /// <summary>
    /// The scene or prefab a profile is checked against, and the small helpers the checks and fixes share (find
    /// components, create objects in the right scene, add components with or without Undo).
    /// </summary>
    public sealed class SetupContext
    {
        public SetupTargetKind Kind { get; private set; }
        /// <summary>Scene targets: the (loaded) scene.</summary>
        public Scene Scene { get; private set; }
        /// <summary>Prefab targets: the root being checked (the asset, its loaded contents during a fix, or an object in a scene).</summary>
        public GameObject Root { get; private set; }
        /// <summary>The scene's or prefab's asset path (null for an object that is not a prefab).</summary>
        public string AssetPath { get; private set; }
        /// <summary>Changes are recorded with Undo (scenes, objects in scenes, the open prefab stage).</summary>
        public bool UseUndo { get; private set; }
        /// <summary>The root is the prefab asset itself (read-only: fixes get loaded contents or edit the asset).</summary>
        public bool IsPrefabAsset { get; private set; }

        private SetupContext() { }

        public static SetupContext ForScene(Scene scene) => new SetupContext
        {
            Kind = SetupTargetKind.Scene,
            Scene = scene,
            AssetPath = scene.path,
            UseUndo = true,
        };

        /// <summary>A prefab asset: checked on the asset; fixes run on its loaded contents, or on the open prefab stage.</summary>
        public static SetupContext ForPrefabAsset(string path) => new SetupContext
        {
            Kind = SetupTargetKind.Prefab,
            Root = AssetDatabase.LoadAssetAtPath<GameObject>(path),
            AssetPath = path,
            IsPrefabAsset = true,
        };

        /// <summary>An object in a scene or in the prefab stage (its changes use Undo).</summary>
        public static SetupContext ForObject(GameObject go)
        {
            string path = null;
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null)
                path = stage.assetPath;
            else if (EditorUtility.IsPersistent(go))
                return ForPrefabAsset(AssetDatabase.GetAssetPath(go));
            return new SetupContext { Kind = SetupTargetKind.Prefab, Root = go, AssetPath = path, UseUndo = true };
        }

        /// <summary>Loaded contents of a prefab (inside a fix: no Undo, the runner saves).</summary>
        internal static SetupContext ForContents(GameObject root, string path) => new SetupContext
        {
            Kind = SetupTargetKind.Prefab,
            Root = root,
            AssetPath = path,
        };

        public bool IsValid => Kind == SetupTargetKind.Scene ? Scene.IsValid() && Scene.isLoaded : Root != null;

        /// <summary>A name for messages.</summary>
        public string Label => Kind == SetupTargetKind.Scene ? Scene.name : Root != null ? Root.name : "(nothing)";

        // ------------------------------------------------------------------ finding

        /// <summary>The scene's root objects, or the prefab root.</summary>
        public IEnumerable<GameObject> Roots()
        {
            if (Kind == SetupTargetKind.Scene)
            {
                if (Scene.IsValid() && Scene.isLoaded)
                    foreach (GameObject g in Scene.GetRootGameObjects())
                        yield return g;
            }
            else if (Root != null)
                yield return Root;
        }

        /// <summary>Every <typeparamref name="T"/> in the scene or prefab (inactive ones too).</summary>
        public List<T> All<T>() where T : Component
        {
            var list = new List<T>();
            foreach (GameObject g in Roots())
                list.AddRange(g.GetComponentsInChildren<T>(true));
            return list;
        }

        public T First<T>() where T : Component
        {
            foreach (GameObject g in Roots())
            {
                T c = g.GetComponentInChildren<T>(true);
                if (c != null)
                    return c;
            }
            return null;
        }

        /// <summary>Every component of a type known by name (null type = none).</summary>
        public List<Component> All(Type type)
        {
            var list = new List<Component>();
            if (type == null)
                return list;
            foreach (GameObject g in Roots())
                list.AddRange(g.GetComponentsInChildren(type, true));
            return list;
        }

        public Component First(Type type) => type == null ? null : All(type).FirstOrDefault();

        /// <summary>A root object of the scene with this name (or null).</summary>
        public GameObject RootNamed(string name) => Roots().FirstOrDefault(g => g.name == name);

        // ------------------------------------------------------------------ changing

        /// <summary>A new object in the target scene (scene targets) or under the prefab root.</summary>
        public GameObject CreateObject(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);
            else if (Kind == SetupTargetKind.Scene)
                SceneManager.MoveGameObjectToScene(go, Scene);
            else if (Root != null)
                go.transform.SetParent(Root.transform, false);
            if (UseUndo)
                Undo.RegisterCreatedObjectUndo(go, "Setup: " + name);
            return go;
        }

        /// <summary>Moves an object made by another tool (in the active scene) into the target scene.</summary>
        public void Adopt(GameObject go)
        {
            if (go != null && Kind == SetupTargetKind.Scene && go.transform.parent == null && go.scene != Scene)
                SceneManager.MoveGameObjectToScene(go, Scene);
        }

        public T Add<T>(GameObject go) where T : Component => (T)Add(go, typeof(T));

        /// <summary>Adds a component (the existing one when there is one).</summary>
        public Component Add(GameObject go, Type type)
        {
            if (go == null || type == null)
                return null;
            Component existing = go.GetComponent(type);
            if (existing != null)
                return existing;
            Component c = UseUndo ? Undo.AddComponent(go, type) : go.AddComponent(type);
            Dirty(go);
            return c;
        }

        /// <summary>Records an object before a change (Undo where it applies).</summary>
        public void Record(Object o, string what)
        {
            if (o != null && UseUndo)
                Undo.RecordObject(o, what);
        }

        public void Dirty(Object o)
        {
            if (o == null)
                return;
            EditorUtility.SetDirty(o);
            if (Kind == SetupTargetKind.Scene && Scene.IsValid())
                EditorSceneManager.MarkSceneDirty(Scene);
            else if (o is Component c && c.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(c))
                EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            else if (o is GameObject g && g.scene.IsValid() && !EditorUtility.IsPersistent(g))
                EditorSceneManager.MarkSceneDirty(g.scene);
        }
    }

    /// <summary>One requirement with what its check found.</summary>
    public sealed class SetupLine
    {
        public SetupRequirement requirement;
        public SetupResult result;
        public bool IsProblem => result.IsProblem;
    }

    /// <summary>Runs checks and fixes.</summary>
    public static class SetupRunner
    {
        /// <summary>Checks every requirement of <paramref name="profile"/> against <paramref name="context"/>.</summary>
        public static List<SetupLine> Evaluate(SetupProfile profile, SetupContext context)
        {
            var lines = new List<SetupLine>(profile.Requirements.Count);
            foreach (SetupRequirement r in profile.Requirements)
                lines.Add(new SetupLine { requirement = r, result = r.Evaluate(context) });
            return lines;
        }

        /// <summary>
        /// Applies one fix where it belongs: in the scene (made active meanwhile, so tools that create objects put them
        /// there), on an object with Undo, in the open prefab stage, or on the prefab asset's loaded contents (saved when
        /// it changed). Returns whether something changed. Exceptions are logged, never thrown.
        /// </summary>
        public static bool ApplyFix(SetupRequirement requirement, SetupContext context)
        {
            if (requirement?.Fix == null || context == null)
                return false;
            try
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Setup: " + requirement.Title);
                if (context.Kind == SetupTargetKind.Scene)
                    return InScene(context, requirement.Fix);
                // A prefab open in Prefab Mode: always change the stage (saving the stage would overwrite the asset).
                PrefabStage open = context.IsPrefabAsset ? PrefabStageUtility.GetCurrentPrefabStage() : null;
                if (open != null && open.assetPath == context.AssetPath)
                    return OnStage(open, requirement.Fix);
                if (!context.IsPrefabAsset || requirement.FixesAsset)
                {
                    // An object in a scene / the stage (Undo), or a fix that loads and saves the asset itself.
                    bool changed = requirement.Fix(context);
                    if (changed && !context.IsPrefabAsset && context.Root != null)
                        context.Dirty(context.Root);
                    return changed;
                }
                // A prefab asset: the fix edits its loaded contents, saved when it changed.
                return Validation.ValidationUtility.EditPrefab(context.AssetPath, root => requirement.Fix(SetupContext.ForContents(root, context.AssetPath)));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Setup", $"'{requirement.FixLabel}' failed: {e.Message}\n\nSee the Console.", "OK");
                return false;
            }
        }

        private static bool OnStage(PrefabStage stage, Func<SetupContext, bool> fix)
        {
            SetupContext ctx = SetupContext.ForObject(stage.prefabContentsRoot);
            bool changed = fix(ctx);
            if (changed)
                EditorSceneManager.MarkSceneDirty(stage.scene);
            return changed;
        }

        private static bool InScene(SetupContext context, Func<SetupContext, bool> fix)
        {
            if (!context.Scene.IsValid() || !context.Scene.isLoaded)
                return false;
            Scene active = SceneManager.GetActiveScene();
            bool switched = active != context.Scene;
            if (switched)
                SceneManager.SetActiveScene(context.Scene);
            try
            {
                bool changed = fix(context);
                if (changed)
                    EditorSceneManager.MarkSceneDirty(context.Scene);
                return changed;
            }
            finally
            {
                if (switched && active.IsValid() && active.isLoaded)
                    SceneManager.SetActiveScene(active);
            }
        }

        /// <summary>
        /// Applies every fixable problem of <paramref name="lines"/> up to <paramref name="level"/> (Required = only
        /// required ones). Returns how many changed something.
        /// </summary>
        public static int FixAll(SetupProfile profile, SetupContext context, IEnumerable<SetupLine> lines, SetupLevel level)
        {
            int count = 0;
            List<SetupLine> todo = lines.Where(l => l.IsProblem && l.requirement.CanFix && l.requirement.InFixAll && l.requirement.Level <= level).ToList();
            try
            {
                for (int i = 0; i < todo.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Setup", todo[i].requirement.FixLabel, (float)i / Mathf.Max(1, todo.Count)))
                        break;
                    // Earlier fixes may have solved this one already (a whole object created at once).
                    SetupContext fresh = Refresh(context);
                    if (!todo[i].requirement.Evaluate(fresh).IsProblem)
                        continue;
                    if (ApplyFix(todo[i].requirement, fresh))
                        count++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }
            return count;
        }

        /// <summary>A context reloaded after a change (a prefab asset is re-read from disk).</summary>
        public static SetupContext Refresh(SetupContext context)
        {
            if (context == null)
                return null;
            if (context.Kind == SetupTargetKind.Prefab && context.IsPrefabAsset)
                return SetupContext.ForPrefabAsset(context.AssetPath);
            return context;
        }
    }

    /// <summary>Small helpers shared by the profiles.</summary>
    public static class SetupUtil
    {
        /// <summary>A type by full name in any loaded assembly (modules such as Fish-Net may be missing).</summary>
        public static Type TypeByName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return null;
            // Every name looked up here is a component (Fish-Net's, the network modules'): Unity's type cache knows them all.
            foreach (Type t in TypeCache.GetTypesDerivedFrom<Component>())
                if (t.FullName == fullName)
                    return t;
            return Type.GetType(fullName, false);
        }

        /// <summary>
        /// <paramref name="a"/> unless it is null by Unity's rules (destroyed, or the editor's "fake null" from GetComponent),
        /// else <paramref name="b"/>. Never use ?? on Unity objects.
        /// </summary>
        public static T Or<T>(T a, T b) where T : Object => a != null ? a : b;

        /// <summary>Short type name of "Namespace.Type".</summary>
        public static string ShortName(string fullName)
        {
            int i = fullName.LastIndexOf('.');
            return i >= 0 ? fullName.Substring(i + 1) : fullName;
        }

        /// <summary>The asset path of the first scene named <paramref name="sceneName"/> under Assets (null = none).</summary>
        public static string ScenePathByName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return null;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (System.IO.Path.GetFileNameWithoutExtension(s.path) == sceneName && System.IO.File.Exists(s.path))
                    return s.path;
            foreach (string guid in AssetDatabase.FindAssets(sceneName + " t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    return path;
            }
            return null;
        }

        /// <summary>The first prefab under Assets matching <paramref name="match"/> (null = none).</summary>
        public static string FindPrefab(Func<GameObject, bool> match)
        {
            foreach (string path in Validation.ValidationUtility.ProjectPrefabPaths())
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && match(go))
                    return path;
            }
            return null;
        }

        /// <summary>Index of a scene in Build Settings (-1 = not there or disabled).</summary>
        public static int BuildIndex(string path)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int index = 0;
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (!s.enabled)
                    continue;
                if (s.path == path)
                    return index;
                index++;
            }
            return -1;
        }

        /// <summary>Is a scene with this name enabled in Build Settings?</summary>
        public static bool InBuildByName(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && EditorBuildSettings.scenes.Any(s => s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == sceneName);

        /// <summary>Adds (or moves) a scene into Build Settings: first, or at the end.</summary>
        public static bool AddToBuild(string path, bool first)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == path);
            var entry = new EditorBuildSettingsScene(path, true);
            if (first)
                scenes.Insert(0, entry);
            else
                scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            return true;
        }

        /// <summary>The Hub World Settings asset in Resources (null = the built-in defaults are used).</summary>
        public static HubWorld.HubWorldSettings HubSettingsAsset() => SMAssets.Load<HubWorld.HubWorldSettings>("HubWorldSettings");

        /// <summary>The Hub World Settings in use (the asset, else the defaults).</summary>
        public static HubWorld.HubWorldSettings HubSettings()
        {
            HubWorld.HubWorldSettings s = HubSettingsAsset();
            return s != null ? s : HubWorld.HubWorldSettings.Instance;
        }

        /// <summary>A problem result naming the components found, or Ok.</summary>
        public static SetupResult Single<T>(List<T> found, string what, string missing) where T : Component
        {
            if (found.Count == 0)
                return SetupResult.Missing(missing);
            if (found.Count > 1)
                return SetupResult.Wrong($"{found.Count} {what} found (on {string.Join(", ", found.Take(3).Select(c => c.name))}): keep one.", found[1]);
            return SetupResult.Ok($"On '{found[0].name}'.", found[0]);
        }

        /// <summary>Sets an object reference field through serialization (private fields too). True when it changed.</summary>
        public static bool SetReference(Object owner, string field, Object value)
        {
            if (owner == null)
                return false;
            var so = new SerializedObject(owner);
            SerializedProperty p = so.FindProperty(field);
            if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == value)
                return false;
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(owner);
            return true;
        }

        /// <summary>The value of an object reference field (null when there is no such field).</summary>
        public static Object GetReference(Object owner, string field)
        {
            if (owner == null)
                return null;
            SerializedProperty p = new SerializedObject(owner).FindProperty(field);
            return p != null && p.propertyType == SerializedPropertyType.ObjectReference ? p.objectReferenceValue : null;
        }

        /// <summary>
        /// The package's own object checks on every object of the target: missing scripts, references to deleted objects
        /// and empty required references (the same as the Project Validator's), as one line with a "Fix what can be
        /// fixed" button.
        /// </summary>
        public static SetupRequirement HierarchyHealth(string group = "Health") =>
            SetupRequirement.Make(group, "No missing scripts or broken references", SetupLevel.Required)
                .Explain("A component whose script was deleted or renamed ('The referenced script is missing'), a field pointing to a " +
                         "deleted object ('Missing') or an empty [Required] field breaks the object silently at runtime.",
                         "Window ▸ SimpleMovements ▸ Project Validator lists each one with the object and a Fix button.")
                .Checks(ctx =>
                {
                    List<Validation.ValidationIssue> issues = HealthIssues(ctx);
                    if (issues.Count == 0)
                        return SetupResult.Ok("Nothing missing or broken.");
                    string first = string.Join("\n", issues.Take(3).Select(i => "• " + i.message + (string.IsNullOrEmpty(i.objectPath) ? "" : $"  ({i.objectPath})")));
                    return SetupResult.Wrong($"{issues.Count} problem(s){(issues.Count > 3 ? ", the first ones" : "")}:\n{first}", issues[0].context);
                })
                .Fixes("Fix What Can Be Fixed", ctx =>
                {
                    List<Validation.ValidationIssue> issues = HealthIssues(ctx);
                    return Validation.ProjectValidator.FixAll(issues) > 0;
                }, fixesAsset: true);

        private static List<Validation.ValidationIssue> HealthIssues(SetupContext ctx)
        {
            var issues = new List<Validation.ValidationIssue>();
            const Validation.ValidationChecks checks = Validation.ValidationChecks.MissingScripts | Validation.ValidationChecks.BrokenReferences |
                                                        Validation.ValidationChecks.RequiredReferences;
            foreach (GameObject root in ctx.Roots())
                Validation.ProjectValidator.ValidateGameObject(root, checks, issues, ctx.Kind == SetupTargetKind.Scene);
            return issues;
        }

        /// <summary>A Resources asset requirement (Combat Settings, databases...) with its Create menu as the fix.</summary>
        public static SetupRequirement ResourcesAsset<T>(string group, string title, string resourcesName, SetupLevel level, string why, Func<bool> create)
            where T : Object =>
            SetupRequirement.Make(group, title, level)
                .Explain(why, $"Tools ▸ SimpleMovements ▸ Project Setup creates it in {SMAssets.AssetPath(SMAssets.FolderOf(resourcesName), resourcesName + ".asset")}.")
                .Checks(_ =>
                {
                    T asset = SMAssets.Load<T>(resourcesName);
                    string where = "Resources/" + SMAssets.ResourcePath(SMAssets.FolderOf(resourcesName), resourcesName);
                    return SetupResult.Present(asset != null, $"{where} found.", $"No {where}: the built-in defaults are used.", asset);
                })
                .Fixes("Create", _ => create(), fixesAsset: true); // a project asset: the prefab itself is not touched
    }
}
#endif
