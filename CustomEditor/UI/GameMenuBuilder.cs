#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game menu in the open scene: the pause window (Resume, Settings, Main Menu, Quit) and the settings
/// window with Audio, Graphics, Controls and Key Bindings pages, wired to <see cref="PauseMenuManager"/>,
/// <see cref="PauseMenuSettings"/> and <see cref="KeyRebindingMenu"/>. Escape (or the gamepad Start button) opens it
/// while a player is in the game. Tools ▸ SimpleMovements ▸ Scene ▸ Build Pause &amp; Settings Menu, or the Pause Menu Manager's button.
/// </summary>
public static class GameMenuBuilder
{
    private const string RootName = "GameMenu";

    [MenuItem(SMMenu.Root + "Scene/Build Pause & Settings Menu", false, SMMenu.Scene + 1)]
    public static void BuildMenu()
    {
        PauseMenuManager existing = Object.FindAnyObjectByType<PauseMenuManager>();
        if (existing != null)
        {
            int choice = EditorUtility.DisplayDialogComplex("Pause & Settings Menu",
                $"The scene already has a Pause Menu Manager on '{existing.name}'.", "Rebuild It", "Cancel", "Select It");
            if (choice == 1)
                return;
            if (choice == 2)
            {
                Selection.activeObject = existing;
                return;
            }
            Rebuild(existing);
            return;
        }
        Build();
    }

    /// <summary>Deletes the menu of <paramref name="manager"/> and builds a new one (sounds and scene name are kept).</summary>
    public static PauseMenuManager Rebuild(PauseMenuManager manager)
    {
        string sceneName = manager != null ? manager.mainMenuSceneName : "MainMenu";
        AudioClip open = manager != null ? manager.pauseOpenSound : null, close = manager != null ? manager.pauseCloseSound : null;
        AudioClip click = manager != null ? manager.buttonClickSound : null;
        if (manager != null)
            Undo.DestroyObjectImmediate(manager.GetComponentInParent<Canvas>() != null ? manager.GetComponentInParent<Canvas>().gameObject : manager.gameObject);
        PauseMenuManager built = Build();
        built.mainMenuSceneName = sceneName;
        built.pauseOpenSound = open;
        built.pauseCloseSound = close;
        built.buttonClickSound = click;
        EditorUtility.SetDirty(built);
        return built;
    }

    public static PauseMenuManager Build()
    {
        Undo.SetCurrentGroupName("Build Pause & Settings Menu");
        int group = Undo.GetCurrentGroup();
        UIBuildKit.EnsureEventSystem();

        Canvas canvas = UIBuildKit.Canvas(RootName, 500);
        GameObject root = canvas.gameObject;
        var manager = root.AddComponent<PauseMenuManager>();
        // The same menu the game builds at runtime, saved in the scene: buttons become persistent events.
        PauseMenuSettings settings = GameMenuFactory.Build(root, manager, (button, action) =>
        {
            if (button != null && action != null)
                UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, action);
        });
        foreach (Component c in root.GetComponentsInChildren<Component>(true))
            if (c != null) EditorUtility.SetDirty(c);
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(manager);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeObject = root;
        Debug.Log("[UI Builder] Pause & Settings menu built. Escape / Start opens it while a player is in the game. " +
                  "Set 'Main Menu Scene Name' on the Pause Menu Manager (the button hides itself when that scene is not in Build Settings). " +
                  "A game scene without this menu gets the same one at runtime.", root);
        return manager;
    }
}

/// <summary>Pause Menu Manager inspector: what is wired, a Build / Rebuild button, and Play Mode controls.</summary>
[CustomEditor(typeof(PauseMenuManager))]
public class PauseMenuManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var m = (PauseMenuManager)target;
        EditorGUILayout.HelpBox("Escape (or the gamepad Start button) opens this menu while a player is in the game; it pauses time and the " +
                                "player's input. Settings: audio, graphics, controls and key bindings (saved for every player).", MessageType.None);
        if (m.pauseMenuUI == null)
            EditorGUILayout.HelpBox("No menu UI assigned. Press Build Menu UI to generate the pause and settings windows.", MessageType.Warning);
        if (!string.IsNullOrEmpty(m.mainMenuSceneName) && !m.HasMainMenuScene)
            EditorGUILayout.HelpBox($"Scene '{m.mainMenuSceneName}' is not in Build Settings: the Main Menu button is hidden.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button(new GUIContent(m.pauseMenuUI == null ? "Build Menu UI" : "Rebuild Menu UI",
                    "Generates the pause window and the settings window (Audio, Graphics, Controls, Key Bindings) and wires everything. " +
                    "Rebuild replaces the current menu (its sounds and main menu scene are kept)."), GUILayout.Height(26f)))
            {
                GameMenuBuilder.Rebuild(m);
                GUIUtility.ExitGUI();
            }
        }
        if (Application.isPlaying && GUILayout.Button(m.IsPaused ? "Resume" : "Pause"))
            m.TogglePause();
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
#endif
