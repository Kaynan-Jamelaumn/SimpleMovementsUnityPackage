#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared inspector helpers for the ability, player and mob editors: section titles, help foldouts, check lists
/// with Fix buttons, status rows with Add buttons, inline asset editors and asset saving.
/// </summary>
public static class AbilityEditorUI
{
    /// <summary>One setup problem or tip, optionally with a one-click fix.</summary>
    public struct Issue
    {
        public MessageType type;
        public string message;
        public string fixLabel;
        public System.Action fix;
    }

    // ------------------------------------------------------------------ performance helpers
    /// <summary>
    /// Limits how often an inspector re-runs its (expensive) checks: only on Layout events (so the drawn controls
    /// never change between Layout and Repaint), at most every <c>interval</c> seconds, or right after a change.
    /// </summary>
    public sealed class Throttle
    {
        private double next;
        private bool force = true;
        private readonly double interval;

        public Throttle(double interval = 0.5) => this.interval = interval;

        /// <summary>True when the checks should be recomputed now.</summary>
        public bool Due
        {
            get
            {
                Event e = Event.current;
                if (e == null || e.type != EventType.Layout)
                    return false;
                double now = EditorApplication.timeSinceStartup;
                if (!force && now < next)
                    return false;
                force = false;
                next = now + interval;
                return true;
            }
        }

        /// <summary>Recompute on the next Layout event (call after the user changed something).</summary>
        public void Invalidate() => force = true;
    }

    private static readonly HashSet<Editor> liveEditors = new HashSet<Editor>();
    private static double nextLiveRepaint;

    [InitializeOnLoadMethod]
    private static void InitLiveRepaint()
    {
        EditorApplication.update -= TickLiveRepaint;
        EditorApplication.update += TickLiveRepaint;
        EditorApplication.projectChanged -= ClearCaches;
        EditorApplication.projectChanged += ClearCaches;
        EditorApplication.hierarchyChanged -= ClearPresence;
        EditorApplication.hierarchyChanged += ClearPresence;
    }

    /// <summary>
    /// Keeps a Play-mode view refreshing about 8 times per second (instead of every editor frame). Call it from
    /// OnInspectorGUI while playing; it stops by itself when the inspector is no longer drawn.
    /// </summary>
    public static void KeepRepainting(Editor editor)
    {
        if (Application.isPlaying && editor != null)
            liveEditors.Add(editor);
    }

    private static void TickLiveRepaint()
    {
        if (liveEditors.Count == 0)
            return;
        double now = EditorApplication.timeSinceStartup;
        if (now < nextLiveRepaint)
            return;
        nextLiveRepaint = now + 0.12;
        var copy = new List<Editor>(liveEditors);
        liveEditors.Clear();
        foreach (Editor e in copy)
            if (e != null)
                e.Repaint();
    }

    /// <summary>Called when assets change: drops cached searches (input actions...).</summary>
    public static event System.Action CachesCleared;

    private static void ClearCaches() => CachesCleared?.Invoke();

    private static void ClearPresence() => presence.Clear();

    private static readonly Dictionary<Object, SerializedObject> serializedCache = new Dictionary<Object, SerializedObject>();

    /// <summary>A cached, up-to-date SerializedObject for <paramref name="target"/> (avoids creating one per redraw).</summary>
    public static SerializedObject Serialized(Object target)
    {
        if (target == null)
            return null;
        if (!serializedCache.TryGetValue(target, out SerializedObject so) || so == null || so.targetObject == null)
        {
            if (serializedCache.Count > 64)
                serializedCache.Clear();
            so = new SerializedObject(target);
            serializedCache[target] = so;
        }
        so.Update();
        return so;
    }

    private static readonly Dictionary<Object, Editor> inlineEditors = new Dictionary<Object, Editor>();
    private static readonly HashSet<int> openInline = new HashSet<int>();

    public static void Section(string title, string help = null)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(help))
            EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
    }

    /// <summary>A collapsible help box. Returns the new open state.</summary>
    public static bool Help(bool open, string title, string text)
    {
        open = EditorGUILayout.BeginFoldoutHeaderGroup(open, title);
        if (open)
            EditorGUILayout.HelpBox(text, MessageType.Info);
        EditorGUILayout.EndFoldoutHeaderGroup();
        return open;
    }

    /// <summary>"✓ label" / "✗ label [Add]" for a component on <paramref name="go"/> (or its children / parents).</summary>
    public static void ComponentRow<T>(GameObject go, string label, string tooltip, bool inChildren = false, bool inParents = false) where T : Component
    {
        bool ok = Has<T>(go, inChildren, inParents);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(new GUIContent((ok ? "✓ " : "✗ ") + label, tooltip), ok ? EditorStyles.label : EditorStyles.boldLabel);
        if (!ok && GUILayout.Button(new GUIContent("Add", "Add " + label + " to this object."), GUILayout.Width(50)))
        {
            Undo.AddComponent<T>(go);
            presence.Clear();
        }
        EditorGUILayout.EndHorizontal();
    }

    private struct PresenceKey : System.IEquatable<PresenceKey>
    {
        public GameObject go;
        public System.Type type;
        public int flags;
        public bool Equals(PresenceKey o) => ReferenceEquals(go, o.go) && type == o.type && flags == o.flags;
        public override bool Equals(object obj) => obj is PresenceKey k && Equals(k);
        public override int GetHashCode() => (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(go) * 397 ^ type.GetHashCode()) * 31 + flags;
    }

    private static readonly Dictionary<PresenceKey, bool> presence = new Dictionary<PresenceKey, bool>();
    private static double presenceExpires;

    /// <summary>
    /// Whether <paramref name="go"/> (or its children / parents) has a <typeparamref name="T"/>. Cached for half a
    /// second and dropped whenever the hierarchy changes, so inspectors can ask on every redraw without slowing down.
    /// </summary>
    public static bool Has<T>(GameObject go, bool inChildren = false, bool inParents = false) where T : Component
    {
        if (go == null)
            return false;
        double now = EditorApplication.timeSinceStartup;
        if (now > presenceExpires)
        {
            presence.Clear();
            presenceExpires = now + 0.5;
        }
        var key = new PresenceKey { go = go, type = typeof(T), flags = (inChildren ? 1 : 0) | (inParents ? 2 : 0) };
        if (presence.TryGetValue(key, out bool ok))
            return ok;
        ok = go.GetComponent<T>() != null || (inChildren && go.GetComponentInChildren<T>(true) != null) ||
             (inParents && go.GetComponentInParent<T>() != null);
        presence[key] = ok;
        return ok;
    }

    public static void StatusRow(bool ok, string label, string tooltip = null)
    {
        EditorGUILayout.LabelField(new GUIContent((ok ? "✓ " : "✗ ") + label, tooltip), ok ? EditorStyles.label : EditorStyles.boldLabel);
    }

    /// <summary>
    /// Draws the issues (errors, warnings, tips) with their Fix buttons. Returns the fix the user clicked (run it
    /// after the inspector finished drawing), or null.
    /// </summary>
    public static System.Action DrawIssues(List<Issue> issues, ref bool open, string okText = "✓ Setup checks passed.")
    {
        if (issues.Count == 0)
        {
            EditorGUILayout.LabelField(okText, EditorStyles.miniLabel);
            return null;
        }
        int errors = 0, warnings = 0;
        foreach (Issue i in issues)
        {
            if (i.type == MessageType.Error) errors++;
            else if (i.type == MessageType.Warning) warnings++;
        }
        System.Action clicked = null;
        open = EditorGUILayout.BeginFoldoutHeaderGroup(open, $"Setup checks ({errors} errors, {warnings} warnings, {issues.Count - errors - warnings} tips)");
        if (open)
        {
            foreach (Issue i in issues)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(i.message, i.type);
                if (i.fix != null && GUILayout.Button(new GUIContent(i.fixLabel ?? "Fix", "Apply this fix (Undo reverts it)."), GUILayout.Width(96), GUILayout.MinHeight(38)))
                    clicked = i.fix;
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        return clicked;
    }

    public static void Add(List<Issue> issues, MessageType type, string message, string fixLabel = null, System.Action fix = null)
    {
        issues.Add(new Issue { type = type, message = message, fixLabel = fixLabel, fix = fix });
    }

    /// <summary>
    /// A toggle that shows the full inspector of <paramref name="asset"/> (an ability or profile) inline, so it can be
    /// edited without leaving this object. <paramref name="key"/> identifies the toggle (e.g. slot index + owner id).
    /// </summary>
    public static void InlineAssetEditor(Object asset, int key, string what, string sharedWarning = null)
    {
        if (asset == null)
            return;
        bool open = openInline.Contains(key);
        EditorGUILayout.BeginHorizontal();
        bool now = EditorGUILayout.Foldout(open, new GUIContent($"Edit {what} here", $"Show the {what}'s settings inline. You are editing the asset itself."), true);
        if (GUILayout.Button(new GUIContent("Select", $"Select the {what} asset in the Project window."), EditorStyles.miniButton, GUILayout.Width(52)))
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        EditorGUILayout.EndHorizontal();
        if (now != open)
        {
            if (now) openInline.Add(key);
            else openInline.Remove(key);
        }
        if (!now)
            return;

        if (!inlineEditors.TryGetValue(asset, out Editor ed) || ed == null || ed.target != asset)
        {
            Editor cached = null;
            Editor.CreateCachedEditor(asset, null, ref cached);
            ed = cached;
            inlineEditors[asset] = ed;
        }
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (!string.IsNullOrEmpty(sharedWarning))
            EditorGUILayout.LabelField(sharedWarning, EditorStyles.wordWrappedMiniLabel);
        if (!EditorUtility.IsPersistent(asset))
            EditorGUILayout.HelpBox("This one only exists while playing (created at runtime). Changes are lost when you stop - use 'Save As Asset'.", MessageType.Warning);
        EditorGUI.indentLevel++;
        ed.OnInspectorGUI();
        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
    }

    public static void MarkDirty(Object o)
    {
        if (o == null)
            return;
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    /// <summary>Saves <paramref name="obj"/> as a new asset (unique name) in <paramref name="folder"/> and pings it.</summary>
    public static T SaveAsset<T>(T obj, string folder, string fileName) where T : ScriptableObject
    {
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            folder = EnsureFolder("Assets/Generated");
        foreach (char ch in System.IO.Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(ch, '_');
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
        obj.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(obj, path);
        if (obj is AbilityDefinition def)
            def.SetId(AssetDatabase.AssetPathToGUID(path));
        EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(obj);
        return obj;
    }

    /// <summary>Saves a copy of a runtime-only ability (converted legacy ability, basic attack) as an asset.</summary>
    public static AbilityDefinition SaveCopy(AbilityDefinition runtime, string folder)
    {
        AbilityDefinition copy = Object.Instantiate(runtime);
        copy.name = runtime.DisplayName;
        copy.SetId("");
        return SaveAsset(copy, folder, runtime.DisplayName);
    }

    /// <summary>Creates the folder (and parents) if needed and returns it.</summary>
    public static string EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return path;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        return path;
    }

    /// <summary>The folder of the object's prefab (asset, instance or open prefab stage), else "Assets/Generated".</summary>
    public static string FolderFor(Component c)
    {
        string path = AssetDatabase.GetAssetPath(c.gameObject);
        if (string.IsNullOrEmpty(path))
            path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(c.gameObject);
#if UNITY_2021_2_OR_NEWER
        if (string.IsNullOrEmpty(path))
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                path = stage.assetPath;
        }
#endif
        return string.IsNullOrEmpty(path) ? "Assets/Generated" : System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
    }

    /// <summary>
    /// Converts a legacy AbilityEffectSO (with its holder's attack casts and particle) into an Ability Definition asset
    /// saved next to it.
    /// </summary>
    public static AbilityDefinition ConvertLegacy(AbilityEffectSO legacy, bool forMob, IList<AttackCast> casts, GameObject particle, string fallbackFolder)
    {
        AbilityDefinition def = LegacyAbilityConverter.CreateDefinition(legacy, forMob, casts, particle);
        string src = AssetDatabase.GetAssetPath(legacy);
        string folder = string.IsNullOrEmpty(src) ? fallbackFolder : System.IO.Path.GetDirectoryName(src).Replace('\\', '/');
        return SaveAsset(def, folder, legacy.name + (forMob ? " (Mob)" : " (Player)"));
    }

    /// <summary>A one-line summary of an ability's numbers.</summary>
    public static string AbilityLine(AbilityDefinition def, AbilityModifierSet mods = null)
    {
        if (def == null)
            return "-";
        AbilityStats st = AbilityStats.From(mods);
        return $"Cast {def.CastTime(st):0.##}s · CD {def.Cooldown(st):0.#}s · reach {def.MaxReach(st):0.#}m · ~{def.EstimateDamage(st):0.#} dmg" +
               (def.targeting.playerConfirmsTarget ? " · click to confirm" : "");
    }

    /// <summary>Menu of ability presets; the chosen one is saved in <paramref name="folder"/> and passed to <paramref name="onCreated"/>.</summary>
    public static void PresetMenu(string folder, System.Action<AbilityDefinition> onCreated)
    {
        var menu = new GenericMenu();
        foreach (AbilityPresets.Preset p in AbilityPresets.All)
        {
            AbilityPresets.Preset captured = p;
            menu.AddItem(new GUIContent(p.name, p.description), false, () =>
            {
                AbilityDefinition def = captured.create();
                onCreated(SaveAsset(def, folder, def.DisplayName));
            });
        }
        menu.ShowAsContext();
    }
}
#endif
