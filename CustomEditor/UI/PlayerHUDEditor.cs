#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Player HUD inspector: <b>Build Status UI</b>, <b>Build Abilities UI</b> (saved in the prefab / scene, then yours to
/// move, restyle or replace), Remove, Select, and Create … Prefab buttons that make a ready slot / gauge / frame prefab
/// to customise and assign. Works on a scene object, in Prefab Mode, and on a prefab asset selected in the Project.
/// </summary>
[CustomEditor(typeof(PlayerHUD))]
public class PlayerHUDEditor : Editor
{
    /// <summary>Where the HUD prefabs made here go (Resources/SimpleMovements/UI/HUD).</summary>
    public static string PrefabFolder => SMAssets.FolderPath(SMAssets.HUD);
    private static GUIStyle ok, warn, muted;

    public override void OnInspectorGUI()
    {
        if (ok == null)
        {
            ok = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.35f, 0.8f, 0.4f) } };
            warn = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.95f, 0.75f, 0.3f) } };
            muted = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }
        var hud = (PlayerHUD)target;
        EditorGUILayout.HelpBox("Build the status UI and the abilities UI here: they are saved in this prefab / scene, so you can move, " +
                                "resize, restyle or replace any part, and the game uses them. Parts you do not build are generated " +
                                "when the game starts. For your own design, assign your objects or prefabs below (Your Own UI).", MessageType.None);

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            Part("Status UI", "Portrait, name, level, health / mana / stamina, experience, needs gauges.", hud.statusUI,
                () => Run(hud, "Build Status UI", h => h.BuildStatusUI()), () => Run(hud, "Remove Status UI", h => h.RemoveStatusUI()));
            Part("Abilities UI", "A slot per ability key (icon, key, casting, cooldown, hover) and the active trait.", hud.abilityUI,
                () => Run(hud, "Build Abilities UI", h => h.BuildAbilityUI()), () => Run(hud, "Remove Abilities UI", h => h.RemoveAbilityUI()));
            if (GUILayout.Button(new GUIContent("Build Both", "Builds (or rebuilds) the status UI and the abilities UI."), GUILayout.Height(28f)))
                Run(hud, "Build Player HUD", h => h.BuildAll());

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Make a prefab to customise (assigned below)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Ability Slot Prefab", "A ready ability slot prefab in " + PrefabFolder + ", assigned as Ability Slot Prefab.")))
                    CreateSlotPrefab(hud, false);
                if (GUILayout.Button(new GUIContent("Trait Slot Prefab", "A ready (gold) trait slot prefab, assigned as Trait Slot Prefab.")))
                    CreateSlotPrefab(hud, true);
                if (GUILayout.Button(new GUIContent("Need Gauge Prefab", "A ready needs gauge prefab, assigned as Need Gauge Prefab.")))
                    CreateGaugePrefab(hud);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(hud.statusUI == null))
                    if (GUILayout.Button(new GUIContent("Save Status UI As Prefab", "Saves the built status UI (with your changes) as a prefab, assigned as Status Frame Prefab.")))
                        SavePartAsPrefab(hud, hud.statusUI != null ? hud.statusUI.gameObject : null, "HUD Status", (h, go) => h.statusFramePrefab = go.GetComponent<HudStatusFrame>());
                using (new EditorGUI.DisabledScope(hud.abilityUI == null))
                    if (GUILayout.Button(new GUIContent("Save Abilities UI As Prefab", "Saves the built abilities UI as a prefab, assigned as Ability Bar Prefab.")))
                        SavePartAsPrefab(hud, hud.abilityUI != null ? hud.abilityUI.gameObject : null, "HUD Abilities", (h, go) => h.abilityBarPrefab = go.GetComponent<HudAbilityBar>());
            }
        }
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("In Play Mode: component menu ▸ Rebuild HUD tries palette changes live (not saved).", MessageType.None);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    private static void Part(string label, string detail, UnityEngine.Object built, Action build, Action remove)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(built != null ? "✔" : "○", built != null ? ok : warn, GUILayout.Width(16f));
            EditorGUILayout.LabelField(label + (built != null ? "  (built)" : "  (built when the game starts)"), EditorStyles.boldLabel);
            if (built != null && GUILayout.Button("Select", GUILayout.Width(60f)))
            {
                Selection.activeObject = built;
                EditorGUIUtility.PingObject(built);
            }
        }
        EditorGUILayout.LabelField(detail, muted);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(built != null ? "Rebuild " + label : "Build " + label, GUILayout.Height(24f)))
                build();
            using (new EditorGUI.DisabledScope(built == null))
                if (GUILayout.Button("Remove", GUILayout.Width(80f), GUILayout.Height(24f)))
                    remove();
        }
        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the HUD: directly on a scene object / in Prefab Mode (with Undo), or inside the
    /// prefab asset (loaded, changed and saved) when a prefab asset is selected in the Project window.
    /// </summary>
    public static void Run(PlayerHUD hud, string undoName, Action<PlayerHUD> action)
    {
        if (hud == null) return;
        try
        {
            if (PrefabUtility.IsPartOfPrefabAsset(hud) && PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                string path = AssetDatabase.GetAssetPath(hud);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    PlayerHUD inside = contents.GetComponentInChildren<PlayerHUD>(true);
                    if (inside == null) return;
                    action(inside);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                Debug.Log($"[HUD] {undoName}: saved in the prefab '{path}'.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RecordObject(hud, undoName);
            // Removing what will be rebuilt, with Undo.
            if (undoName.Contains("Status") || undoName.Contains("Player HUD"))
                if (hud.statusUI != null) { Undo.DestroyObjectImmediate(hud.statusUI.gameObject); hud.statusUI = null; }
            if (undoName.Contains("Abilities") || undoName.Contains("Player HUD"))
                if (hud.abilityUI != null) { Undo.DestroyObjectImmediate(hud.abilityUI.gameObject); hud.abilityUI = null; }
            RectTransform rootBefore = hud.hudRoot;
            HudTooltip tipBefore = hud.tooltipUI;
            action(hud);
            if (hud.hudRoot != null && hud.hudRoot != rootBefore) Undo.RegisterCreatedObjectUndo(TopCreated(hud.hudRoot.gameObject), undoName);
            else
            {
                if (hud.statusUI != null) Undo.RegisterCreatedObjectUndo(hud.statusUI.gameObject, undoName);
                if (hud.abilityUI != null) Undo.RegisterCreatedObjectUndo(hud.abilityUI.gameObject, undoName);
                if (hud.tooltipUI != null && hud.tooltipUI != tipBefore) Undo.RegisterCreatedObjectUndo(hud.tooltipUI.gameObject, undoName);
            }
            Undo.CollapseUndoOperations(group);
            EditorUtility.SetDirty(hud);
            if (PrefabUtility.IsPartOfPrefabInstance(hud)) PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            if (hud.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            if (hud.statusUI != null || hud.abilityUI != null)
                EditorGUIUtility.PingObject(hud.abilityUI != null ? (UnityEngine.Object)hud.abilityUI : hud.statusUI);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Player HUD", $"{undoName} failed: {e.Message}", "OK");
        }
    }

    /// <summary>The HUD root, or the canvas made for it (when the player had none).</summary>
    private static GameObject TopCreated(GameObject root)
    {
        Transform t = root.transform.parent;
        return t != null && t.name == "HUD Canvas" ? t.gameObject : root;
    }

    // ------------------------------------------------------------------ prefabs to customise
    private static string NewPath(string name)
    {
        return AssetDatabase.GenerateUniqueAssetPath($"{SMAssetsEditor.Folder(SMAssets.HUD)}/{name}.prefab");
    }

    private static void Assign(PlayerHUD hud, string undoName, Action<PlayerHUD> set)
    {
        if (PrefabUtility.IsPartOfPrefabAsset(hud) && PrefabStageUtility.GetCurrentPrefabStage() == null)
        {
            Run(hud, undoName, set);
            return;
        }
        Undo.RecordObject(hud, undoName);
        set(hud);
        EditorUtility.SetDirty(hud);
        if (PrefabUtility.IsPartOfPrefabInstance(hud)) PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
    }

    public static void CreateSlotPrefab(PlayerHUD hud, bool trait)
    {
        HudAbilitySlot slot = HudAbilitySlot.Create(null, trait ? "Trait Slot" : "Ability Slot", trait ? hud.traitSlotSize : hud.abilitySlotSize, hud.palette, trait);
        slot.Preview(null, trait ? "J" : "Q");
        string path = NewPath(trait ? "HUD Trait Slot" : "HUD Ability Slot");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(slot.gameObject, path);
        UnityEngine.Object.DestroyImmediate(slot.gameObject);
        var prefab = saved.GetComponent<HudAbilitySlot>();
        Assign(hud, "Assign Slot Prefab", h => { if (trait) h.traitSlotPrefab = prefab; else h.abilitySlotPrefab = prefab; });
        Debug.Log($"[HUD] Slot prefab created at {path}: open it to restyle; rebuild the Abilities UI to use it.", saved);
        EditorGUIUtility.PingObject(saved);
    }

    public static void CreateGaugePrefab(PlayerHUD hud)
    {
        HudNeedGauge g = HudNeedGauge.Create(null, "Hunger", hud.palette);
        string path = NewPath("HUD Need Gauge");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(g.gameObject, path);
        UnityEngine.Object.DestroyImmediate(g.gameObject);
        var prefab = saved.GetComponent<HudNeedGauge>();
        Assign(hud, "Assign Gauge Prefab", h => h.needGaugePrefab = prefab);
        Debug.Log($"[HUD] Need gauge prefab created at {path}: restyle it; rebuild the Status UI to use it (its Status Key is set per status).", saved);
        EditorGUIUtility.PingObject(saved);
    }

    private static void SavePartAsPrefab(PlayerHUD hud, GameObject part, string name, Action<PlayerHUD, GameObject> set)
    {
        if (part == null) return;
        string path = NewPath(name);
        // A copy: the part may sit inside another prefab (the player's), which cannot be saved as a prefab of its own.
        GameObject copy = UnityEngine.Object.Instantiate(part);
        copy.name = part.name;
        GameObject saved;
        try { saved = PrefabUtility.SaveAsPrefabAsset(copy, path); }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
        Assign(hud, "Assign HUD Prefab", h => set(h, saved));
        Debug.Log($"[HUD] Saved '{part.name}' as {path} (assigned): other players / scenes building the HUD use it.", saved);
        EditorGUIUtility.PingObject(saved);
    }
}

/// <summary>Tools ▸ SimpleMovements ▸ Player ▸ Player HUD ▸ … on the selected player.</summary>
public static class PlayerHUDMenu
{
    private const string Path = SMMenu.Root + "Player/Player HUD/";

    [MenuItem(Path + "Build Status UI", false, SMMenu.Player + 40)]
    private static void BuildStatus() => WithHud("Build Status UI", h => h.BuildStatusUI());

    [MenuItem(Path + "Build Abilities UI", false, SMMenu.Player + 41)]
    private static void BuildAbilities() => WithHud("Build Abilities UI", h => h.BuildAbilityUI());

    [MenuItem(Path + "Build Both", false, SMMenu.Player + 42)]
    private static void BuildBoth() => WithHud("Build Player HUD", h => h.BuildAll());

    [MenuItem(Path + "Add Player HUD To Selection", false, SMMenu.Player + 43)]
    private static void Add()
    {
        PlayerHUD hud = HudOf(Selection.activeGameObject, true);
        if (hud != null) Selection.activeObject = hud;
    }

    private static void WithHud(string undoName, Action<PlayerHUD> action)
    {
        PlayerHUD hud = HudOf(Selection.activeGameObject, true);
        if (hud == null) return;
        PlayerHUDEditor.Run(hud, undoName, action);
        Selection.activeObject = hud;
    }

    /// <summary>The selected player's Player HUD (added on the Inventory Manager's object when missing).</summary>
    public static PlayerHUD HudOf(GameObject go, bool add)
    {
        if (go == null)
        {
            EditorUtility.DisplayDialog("Player HUD", "Select the player (in the scene, in Prefab Mode or its prefab in the Project).", "OK");
            return null;
        }
        PlayerHUD hud = go.GetComponentInChildren<PlayerHUD>(true);
        if (hud == null) hud = go.GetComponentInParent<PlayerHUD>();
        if (hud != null || !add) return hud;
        InventoryManager inv = go.GetComponentInChildren<InventoryManager>(true);
        if (inv == null) inv = go.GetComponentInParent<InventoryManager>();
        GameObject host = inv != null ? inv.gameObject : go;
        if (PrefabUtility.IsPartOfPrefabAsset(host) && PrefabStageUtility.GetCurrentPrefabStage() == null)
        {
            string path = AssetDatabase.GetAssetPath(host);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                InventoryManager i2 = contents.GetComponentInChildren<InventoryManager>(true);
                (i2 != null ? i2.gameObject : contents).AddComponent<PlayerHUD>();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.Refresh();
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return asset != null ? asset.GetComponentInChildren<PlayerHUD>(true) : null;
        }
        return Undo.AddComponent<PlayerHUD>(host);
    }
}
#endif
