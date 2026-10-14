#if UNITY_EDITOR
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleMovements.Setup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Player Setup Validator inspector: the full Player Prefab check of the Scene and Prefab Setup (the same list, so both
/// always agree), grouped by area - movement, status, animation / camera / input, character, items and combat, object
/// settings, references, input, interaction, multiplayer, project assets and the hierarchy's health. Each line says what
/// was found, why it matters and how to do it by hand, with Select and Fix buttons; Fix All buttons apply the automatic
/// fixes by level. Below: the checks of the scene the player is in (EventSystem, pause menu, camera...), a text report
/// for the Console or the clipboard, and the component's own settings.
/// </summary>
[CustomEditor(typeof(PlayerSetupValidator))]
public class PlayerSetupValidatorEditor : Editor
{
    private static SetupProfile profile;
    private SetupReportView view;
    private List<SetupLine> lines;
    private SetupContext context;
    private bool dirty = true;
    private double nextCheck;
    private static bool showScene = true;
    private static GUIStyle ok, warn, muted, header;

    private static SetupProfile Profile => profile ?? (profile = PrefabProfiles.Player());

    private void OnEnable()
    {
        view = new SetupReportView("PlayerValidator");
        view.Changed += MarkDirty;
        EditorApplication.hierarchyChanged += MarkDirty;
        Undo.undoRedoPerformed += MarkDirty;
    }

    private void OnDisable()
    {
        EditorApplication.hierarchyChanged -= MarkDirty;
        Undo.undoRedoPerformed -= MarkDirty;
    }

    private void MarkDirty()
    {
        dirty = true;
        Repaint();
    }

    private static void Styles()
    {
        if (ok != null)
            return;
        ok = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.35f, 0.8f, 0.4f) } };
        warn = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.95f, 0.75f, 0.3f) } };
        muted = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
        header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
    }

    public override bool RequiresConstantRepaint() => false;

    public override void OnInspectorGUI()
    {
        Styles();
        var validator = (PlayerSetupValidator)target;
        GameObject go = validator.gameObject;

        // Field edits do not raise hierarchyChanged: check again every two seconds while the inspector is drawn.
        if (Event.current.type == EventType.Layout && (dirty || EditorApplication.timeSinceStartup >= nextCheck))
        {
            dirty = false;
            nextCheck = EditorApplication.timeSinceStartup + 2.0;
            context = SetupContext.ForObject(go);
            lines = context.IsValid ? SetupRunner.Evaluate(Profile, context) : null;
        }
        if (lines == null || context == null)
        {
            DrawDefaultInspector();
            return;
        }

        GUILayout.Label("Player Setup", header);
        EditorGUILayout.LabelField(Where(go), muted);
        DrawToolbar(go);
        EditorGUILayout.Space(4f);
        view.DrawSummary(Profile, context, lines);
        EditorGUILayout.Space(4f);
        view.DrawLines(context, lines);

        EditorGUILayout.Space(6f);
        showScene = EditorGUILayout.Foldout(showScene, "The scene this player is in", true, EditorStyles.foldoutHeader);
        if (showScene)
            DrawScene(go);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Validator settings", EditorStyles.boldLabel);
        DrawDefaultInspector();
    }

    /// <summary>What is being checked: the prefab, the open prefab, or an instance in a scene (whose changes are overrides).</summary>
    private static string Where(GameObject go)
    {
        if (EditorUtility.IsPersistent(go))
            return $"Prefab asset {AssetDatabase.GetAssetPath(go)}: fixes are saved to the prefab.";
        PrefabStage stage = PrefabStageUtility.GetPrefabStage(go);
        if (stage != null)
            return $"Open in Prefab Mode ({stage.assetPath}): fixes change the open prefab - save it afterwards.";
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
        return source != null
            ? $"Instance of '{source.name}' in the scene '{go.scene.name}': fixes become overrides on this instance only. Check the prefab to change every player."
            : $"Object in the scene '{go.scene.name}' (not a prefab).";
    }

    private void DrawToolbar(GameObject go)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Refresh", "Check again now.")))
                MarkDirty();
            GameObject source = EditorUtility.IsPersistent(go) ? go : PrefabUtility.GetCorrespondingObjectFromSource(go);
            string prefabPath = source != null ? AssetDatabase.GetAssetPath(source) : null;
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(prefabPath)))
                if (GUILayout.Button(new GUIContent("Check The Prefab", "Opens the Scene and Prefab Setup window on this player's prefab asset.")))
                    SceneAndPrefabSetupWindow.Open("player", prefabPath);
            if (GUILayout.Button(new GUIContent("Log Report", "Writes every line of the check to the Console (click the line to select the player).")))
                Debug.Log(Report(go), go);
            if (GUILayout.Button(new GUIContent("Copy Report", "Copies the report to the clipboard (e.g. to share it).")))
            {
                EditorGUIUtility.systemCopyBuffer = Report(go);
                Debug.Log("[Player Setup] Report copied to the clipboard.");
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            using (new EditorGUI.DisabledScope(go.GetComponentInChildren<PlayerStatusController>(true) == null))
                if (GUILayout.Button(new GUIContent("Run Status Setup Wizard", "Creates and wires every status manager, the experience and trait managers and the dash / roll models (the Player Status Controller's Complete Setup Wizard).")))
                    EditorApplication.delayCall += () =>
                    {
                        SetupRequirement wizard = Profile.Requirements.FirstOrDefault(r => r.Title == "Status bars wired");
                        if (wizard != null)
                            SetupRunner.ApplyFix(wizard, SetupContext.ForObject(go));
                        MarkDirty();
                    };
            if (GUILayout.Button(new GUIContent("Auto-assign References", "Fills the empty references of the movement and ability components from the player's hierarchy.")))
                EditorApplication.delayCall += () =>
                {
                    AutoAssign(go);
                    MarkDirty();
                };
        }
    }

    /// <summary>The whole check as text.</summary>
    private string Report(GameObject go)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Player Setup] {go.name} - {SetupReportView.SummaryText(lines)}");
        sb.AppendLine(Where(go));
        foreach (IGrouping<string, SetupLine> group in lines.GroupBy(l => l.requirement.Group))
        {
            sb.AppendLine();
            sb.AppendLine($"== {group.Key}");
            foreach (SetupLine l in group)
            {
                sb.Append(SetupReportView.Icon(l.result.status, l.requirement.Level)).Append(' ')
                  .Append(l.requirement.Title).Append(" [").Append(SetupReportView.LevelText(l.requirement.Level)).Append(']');
                if (!string.IsNullOrEmpty(l.result.detail))
                    sb.Append(" - ").Append(l.result.detail.Replace("\n", " "));
                sb.AppendLine();
                if (l.IsProblem && !string.IsNullOrEmpty(l.requirement.How))
                    sb.AppendLine("    By hand: " + l.requirement.How);
            }
        }
        return sb.ToString();
    }

    private static void AutoAssign(GameObject go)
    {
        Undo.RegisterFullObjectHierarchyUndo(go, "Auto-assign References");
        go.GetComponent<MovementStateMachine>()?.AutoAssignReferences();
        go.GetComponent<PlayerMovementController>()?.AutoAssignReferences();
        go.GetComponentInChildren<AbilitiesStateMachine>(true)?.AutoAssignReferences();
        EditorUtility.SetDirty(go);
        if (!EditorUtility.IsPersistent(go) && go.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // ------------------------------------------------------------------ the scene

    private void DrawScene(GameObject go)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool inScene = !EditorUtility.IsPersistent(go) && PrefabStageUtility.GetPrefabStage(go) == null;
        if (!inScene)
        {
            EditorGUILayout.LabelField("Place the player in a scene (or select its instance there) to check that scene's EventSystem, pause menu, " +
                                       "camera and character creation. Whole scenes are checked by Tools ▸ SimpleMovements ▸ Scene and Prefab Setup.", muted);
            EditorGUILayout.EndVertical();
            return;
        }
        bool eventSystem = UIBuildKit.HasEventSystem();
        Row(eventSystem, "EventSystem", eventSystem ? "Found." : "UI cannot be clicked without one (the inventory creates one at runtime, scene menus need it now).",
            eventSystem ? null : "Create", UIBuildKit.EnsureEventSystem);
        bool pause = UnityEngine.Object.FindAnyObjectByType<PauseMenuManager>() != null;
        Row(pause, "Pause & settings menu", pause ? "Found: Escape opens it in game." : "No pause menu: Escape does nothing.",
            pause ? null : "Build", () => GameMenuBuilder.Build());
        bool creation = UnityEngine.Object.FindAnyObjectByType<CharacterCreationUI>() != null;
        Row(creation, "Character creation screen (optional)", creation ? "Found." : "Only needed when players create their character in this scene.",
            creation ? null : "Build", () => CharacterCreationSceneBuilder.Build(null));
        bool camera = go.GetComponentInChildren<Camera>(true) != null || UnityEngine.Object.FindAnyObjectByType<Camera>() != null;
        Row(camera, "A camera", camera ? "Found." : "Add a camera (with a CinemachineBrain for the player's Cinemachine cameras).", null, null);
        int players = UnityEngine.Object.FindObjectsByType<PlayerStatusController>(FindObjectsInactive.Include).Length;
        Row(players <= 1, "One player in the open scenes", players <= 1 ? "Yes." : $"{players} players: in single player keep one; in multiplayer the server spawns them (keep none).", null, null);
        if (GUILayout.Button(new GUIContent("Check The Whole Scene", "Opens the Scene and Prefab Setup on the page of this scene (City, World...).")))
            EditorApplication.ExecuteMenuItem(SMMenu.Root + "Scene and Prefab Setup/Check Selection Or Active Scene");
        EditorGUILayout.EndVertical();
    }

    private static void Row(bool good, string label, string detail, string fixLabel, Action fix)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(good ? "✔" : "!", good ? ok : warn, GUILayout.Width(16f));
            EditorGUILayout.LabelField(new GUIContent(label, detail), GUILayout.MinWidth(120f));
            if (!good && fix != null && fixLabel != null)
            {
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                    if (GUILayout.Button(fixLabel, GUILayout.Width(120f)))
                        fix();
            }
        }
        EditorGUILayout.LabelField(detail, muted);
    }
}

/// <summary>Menu: adds the validator to the selected player; the Player section's help.</summary>
public static class PlayerSetupValidatorMenu
{
    [MenuItem(SMMenu.Root + "Player/Help", false, SMMenu.Player + 60)]
    private static void Help()
    {
        int choice = EditorUtility.DisplayDialogComplex("Setting up the player",
            "1. Build the player prefab: CharacterController + the movement, status and inventory scripts (Player ▸ Add Setup Validator To Selection, " +
            "then its Fix All buttons do it for you).\n" +
            "2. Put your model (with its Animator) under it; Player ▸ Complete Player Animator Controller adds the parameters the scripts use.\n" +
            "3. Assign the hands (weapon and held items), the camera rig and Unity's Player Input with your Input Actions asset.\n" +
            "4. Multiplayer: Network ▸ Make Selected Prefab Networked ▸ Player, then assign it on the Bootstrap scene's Net Player Spawner.\n\n" +
            "Scene and Prefab Setup ▸ Player Prefab checks all of this with explanations and Fix buttons; the Player Setup Validator component " +
            "shows the same check in the player's inspector.",
            "Open Player Prefab Check", "Close", "Open Guide");
        if (choice == 0)
            SceneAndPrefabSetupWindow.Open("player");
        else if (choice == 2)
            DocLinks.Open("Player/01-Player-Setup.md");
    }

    [MenuItem(SMMenu.Root + "Player/Add Setup Validator To Selection", false, SMMenu.Player + 0)]
    private static void AddToSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Player Setup Validator", "Select the player (in the scene or its prefab) first.", "OK");
            return;
        }
        PlayerSetupValidator v = go.GetComponent<PlayerSetupValidator>();
        if (v == null)
            v = Undo.AddComponent<PlayerSetupValidator>(go);
        Selection.activeObject = v;
    }
}
#endif
#endif
