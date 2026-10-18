#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Severity = MobSetupValidator.Severity;
using Issue = MobSetupValidator.Issue;

/// <summary>
/// Checks every mob at once (the selection, the open scenes, or every mob prefab of the project) with
/// <see cref="MobSetupValidator"/>: problems grouped per mob, each with its Fix, plus Fix All Safe and Auto-Configure for
/// one mob or all of them. Prefabs are fixed in their prefab file (the instances follow).
/// Tools ▸ SimpleMovements ▸ Mobs ▸ Mob Setup Validator.
/// </summary>
public class MobSetupValidatorWindow : EditorWindow
{
    private enum Scope { Selection, OpenScenes, ProjectPrefabs }

    private sealed class Entry
    {
        public string name;
        /// <summary>Scene mob (null for a prefab asset).</summary>
        public GameObject sceneObject;
        /// <summary>Prefab asset path (null for a scene mob).</summary>
        public string prefabPath;
        public List<Issue> issues = new List<Issue>();
        public bool open;
        public int Count(Severity s) => issues.Count(i => i.severity == s);
    }

    private Scope scope = Scope.OpenScenes;
    private bool showTips = true;
    private string filter = "";
    private readonly List<Entry> entries = new List<Entry>();
    private Vector2 scroll;
    private bool ran;
    private Action pending;

    [MenuItem(SMMenu.Root + "Mobs/Mob Setup Validator...", false, SMMenu.Mobs + 2)]
    public static void Open()
    {
        var w = GetWindow<MobSetupValidatorWindow>("Mob Validator");
        w.minSize = new Vector2(520, 360);
        if (Selection.gameObjects.Any(g => g.GetComponentInChildren<Mob>(true) != null))
            w.scope = Scope.Selection;
        w.Run();
    }

    [MenuItem(SMMenu.Root + "Validate/Mobs", false, SMMenu.Validate + 24)]
    private static void ValidateMenu()
    {
        var w = GetWindow<MobSetupValidatorWindow>("Mob Validator");
        w.scope = Scope.ProjectPrefabs;
        w.Run();
    }

    // ------------------------------------------------------------------ scanning
    private void Run()
    {
        entries.Clear();
        ran = true;
        switch (scope)
        {
            case Scope.Selection:
                foreach (GameObject g in Selection.gameObjects)
                {
                    string path = AssetDatabase.GetAssetPath(g);
                    if (!string.IsNullOrEmpty(path)) AddPrefab(path);
                    else foreach (Mob m in g.GetComponentsInChildren<Mob>(true)) AddScene(m.gameObject);
                }
                break;
            case Scope.OpenScenes:
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    Scene scene = SceneManager.GetSceneAt(s);
                    if (!scene.isLoaded) continue;
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (Mob m in root.GetComponentsInChildren<Mob>(true)) AddScene(m.gameObject);
                }
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                    foreach (Mob m in stage.prefabContentsRoot.GetComponentsInChildren<Mob>(true)) AddScene(m.gameObject);
                break;
            case Scope.ProjectPrefabs:
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset != null && asset.GetComponentInChildren<Mob>(true) != null) AddPrefab(path);
                }
                break;
        }
        entries.Sort((a, b) => b.Count(Severity.Error) != a.Count(Severity.Error)
            ? b.Count(Severity.Error).CompareTo(a.Count(Severity.Error))
            : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        foreach (Entry e in entries) e.open = e.Count(Severity.Error) + e.Count(Severity.Warning) > 0 && entries.Count <= 12;
        Repaint();
    }

    private void AddScene(GameObject go)
    {
        if (entries.Any(e => e.sceneObject == go)) return;
        entries.Add(new Entry { name = go.name, sceneObject = go, issues = MobSetupValidator.Check(go) });
    }

    private void AddPrefab(string path)
    {
        if (entries.Any(e => e.prefabPath == path)) return;
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null || asset.GetComponentInChildren<Mob>(true) == null) return;
        var e = new Entry { name = asset.name, prefabPath = path };
        // Checked on the prefab contents (the same objects the fixes edit).
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Mob m in root.GetComponentsInChildren<Mob>(true))
                e.issues.AddRange(MobSetupValidator.Check(m.gameObject).Select(Detach));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        entries.Add(e);
    }

    /// <summary>A prefab issue's fix runs later on freshly loaded contents (found again by its id).</summary>
    private static Issue Detach(Issue i) => new Issue
    {
        severity = i.severity, area = i.area, id = i.id, message = i.message, fixLabel = i.fixLabel, fix = i.fix != null ? () => { } : null,
        safe = i.safe, context = null,
    };

    // ------------------------------------------------------------------ fixing
    private void Fix(Entry e, Issue issue)
    {
        if (e.sceneObject != null)
        {
            issue.fix?.Invoke();
            Refresh(e);
            return;
        }
        EditPrefab(e.prefabPath, root =>
        {
            foreach (Mob m in root.GetComponentsInChildren<Mob>(true))
            {
                Issue same = MobSetupValidator.Check(m.gameObject).FirstOrDefault(i => i.id == issue.id && i.fix != null);
                if (same == null) continue;
                same.fix();
                return true;
            }
            return false;
        });
        Refresh(e);
    }

    private void FixAllSafe(Entry e)
    {
        if (e.sceneObject != null)
            MobSetupValidator.FixAllSafe(e.sceneObject);
        else
            EditPrefab(e.prefabPath, root => root.GetComponentsInChildren<Mob>(true).Sum(m => MobSetupValidator.FixAllSafe(m.gameObject)) > 0);
        Refresh(e);
    }

    private void Configure(Entry e)
    {
        if (e.sceneObject != null)
            MobSetupUtility.SetupMob(e.sceneObject, false);
        else
            EditPrefab(e.prefabPath, root =>
            {
                foreach (Mob m in root.GetComponentsInChildren<Mob>(true)) MobSetupUtility.SetupMob(m.gameObject, false);
                return true;
            });
        Refresh(e);
    }

    /// <summary>Opens the prefab's contents, edits them and saves them when <paramref name="edit"/> changed something.</summary>
    private static void EditPrefab(string path, Func<GameObject, bool> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (edit(root))
                PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private void Refresh(Entry e)
    {
        int index = entries.IndexOf(e);
        if (index < 0) return;
        bool open = e.open;
        entries.RemoveAt(index);
        int before = entries.Count;
        if (e.sceneObject != null) AddScene(e.sceneObject);
        else AddPrefab(e.prefabPath);
        if (entries.Count > before)
        {
            Entry fresh = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            fresh.open = open;
            entries.Insert(index, fresh);
        }
    }

    // ------------------------------------------------------------------ drawing
    private void OnGUI()
    {
        pending = null;
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            scope = (Scope)EditorGUILayout.EnumPopup(scope, EditorStyles.toolbarPopup, GUILayout.Width(130));
            if (GUILayout.Button("Run", EditorStyles.toolbarButton, GUILayout.Width(50))) pending = Run;
            GUILayout.Space(6);
            showTips = GUILayout.Toggle(showTips, "Tips", EditorStyles.toolbarButton, GUILayout.Width(40));
            filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80));
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(entries.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("Fix All Safe (all)", "Every mob: adds missing components, wires references and corrects " +
                                                                          "settings that are plainly wrong. Design choices are left to you."), EditorStyles.toolbarButton))
                    pending = () => { foreach (Entry e in entries.ToList()) FixAllSafe(e); };
                if (GUILayout.Button(new GUIContent("Auto-Configure (all)", "Every mob: the full Quick Setup (components, profile and basic attack assets, safe fixes)."),
                        EditorStyles.toolbarButton))
                    pending = () => { foreach (Entry e in entries.ToList()) Configure(e); };
            }
        }

        int errors = entries.Sum(e => e.Count(Severity.Error)), warnings = entries.Sum(e => e.Count(Severity.Warning)), tips = entries.Sum(e => e.Count(Severity.Tip));
        if (!ran)
            EditorGUILayout.HelpBox("Pick what to check and press Run.", MessageType.Info);
        else if (entries.Count == 0)
            EditorGUILayout.HelpBox(scope == Scope.Selection ? "No mob in the selection." : scope == Scope.OpenScenes ? "No mob in the open scenes." : "No mob prefab in the project.", MessageType.Info);
        else
            EditorGUILayout.LabelField($"{entries.Count} mob(s): {errors} error(s), {warnings} warning(s), {tips} tip(s).", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Entry e in entries)
        {
            if (!string.IsNullOrEmpty(filter) && e.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            DrawEntry(e);
        }
        EditorGUILayout.EndScrollView();

        if (pending != null)
        {
            Action a = pending;
            pending = null;
            a();
            GUIUtility.ExitGUI();
        }
    }

    private void DrawEntry(Entry e)
    {
        int er = e.Count(Severity.Error), wa = e.Count(Severity.Warning), ti = e.Count(Severity.Tip);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        using (new EditorGUILayout.HorizontalScope())
        {
            string state = er > 0 ? "✗" : wa > 0 ? "!" : "✓";
            e.open = EditorGUILayout.Foldout(e.open, $"{state} {e.name}   {(e.prefabPath != null ? "(prefab)" : "(scene)")}   {er} errors · {wa} warnings · {ti} tips", true);
            if (GUILayout.Button("Select", GUILayout.Width(56)))
            {
                Object o = e.sceneObject != null ? (Object)e.sceneObject : AssetDatabase.LoadAssetAtPath<GameObject>(e.prefabPath);
                Selection.activeObject = o;
                EditorGUIUtility.PingObject(o);
            }
            using (new EditorGUI.DisabledScope(!e.issues.Any(i => i.safe)))
                if (GUILayout.Button("Fix Safe", GUILayout.Width(64))) pending = () => FixAllSafe(e);
            if (GUILayout.Button(new GUIContent("Configure", "Quick Setup on this mob."), GUILayout.Width(72))) pending = () => Configure(e);
        }
        if (e.open)
        {
            if (e.issues.Count == 0)
                EditorGUILayout.LabelField("No problems.", EditorStyles.miniLabel);
            foreach (Issue i in e.issues)
            {
                if (i.severity == Severity.Tip && !showTips) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    MessageType t = i.severity == Severity.Error ? MessageType.Error : i.severity == Severity.Warning ? MessageType.Warning : MessageType.Info;
                    EditorGUILayout.HelpBox($"[{i.area}] {i.message}", t);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(96)))
                    {
                        if (i.fix != null && GUILayout.Button(new GUIContent(i.fixLabel ?? "Fix", i.safe ? "Safe fix (Fix All applies it too)." : "Changes a choice of yours: check it."), GUILayout.MinHeight(30)))
                        {
                            Issue captured = i;
                            pending = () => Fix(e, captured);
                        }
                        if (i.context != null && GUILayout.Button("Show", EditorStyles.miniButton))
                        {
                            Selection.activeObject = i.context;
                            EditorGUIUtility.PingObject(i.context);
                        }
                    }
                }
            }
        }
        EditorGUILayout.EndVertical();
    }
}
#endif
