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
/// while a player is in the game. Tools ▸ SimpleMovements ▸ Scene UI ▸ Build Pause &amp; Settings Menu, or the Pause Menu Manager's button.
/// </summary>
public static class GameMenuBuilder
{
    private const string RootName = "GameMenu";

    [MenuItem("Tools/SimpleMovements/Scene UI/Build Pause & Settings Menu")]
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
        var settings = root.AddComponent<PauseMenuSettings>();
        var sfx = root.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.ignoreListenerPause = true;

        // ---------------------------------------------------------------- pause window
        Image backdrop = InventoryUIFactory.Panel("PauseMenu", root.transform, UIBuildKit.Backdrop);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        var group1 = backdrop.gameObject.AddComponent<CanvasGroup>();

        Image main = UIBuildKit.Window("MainPanel", backdrop.transform, new Vector2(440f, 540f));
        RectTransform mainCol = UIBuildKit.Column("Content", main.transform, 36, 14f);
        InventoryUIFactory.Stretch(mainCol);
        UIBuildKit.Title(mainCol, "Paused", 38f);
        UIBuildKit.Button("Resume", mainCol, "Resume", manager.ResumeButton, 0f, 52f);
        UIBuildKit.Button("Character", mainCol, "Character", settings.OpenCharacterSheet, 0f, 52f);
        UIBuildKit.Button("Settings", mainCol, "Settings", settings.ShowSettingsPanel, 0f, 52f);
        Button mainMenu = UIBuildKit.Button("MainMenu", mainCol, "Main Menu", manager.ReturnToMainMenu, 0f, 52f);
        UIBuildKit.Button("Quit", mainCol, "Quit Game", manager.QuitGame, 0f, 52f);
        TextMeshProUGUI hint = InventoryUIFactory.Text("Hint", mainCol, "Esc / Start: resume", 14f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center);
        InventoryUIFactory.Size(hint, -1f, 22f);

        // ---------------------------------------------------------------- settings window
        Image settingsWin = UIBuildKit.Window("SettingsPanel", backdrop.transform, new Vector2(1100f, 700f));
        RectTransform tabs = UIBuildKit.Area("Tabs", settingsWin.transform, new Vector2(0f, 0f), new Vector2(0f, 1f));
        tabs.sizeDelta = new Vector2(260f, 0f);
        tabs.anchoredPosition = new Vector2(130f, 0f);
        InventoryUIFactory.Vertical(tabs.gameObject, 24, 10f, false);
        UIBuildKit.Title(tabs, "Settings", 30f);
        UIBuildKit.Button("AudioTab", tabs, "Audio", settings.ShowAudioPanel, 0f, 46f);
        UIBuildKit.Button("GraphicsTab", tabs, "Graphics", settings.ShowGraphicsPanel, 0f, 46f);
        UIBuildKit.Button("ControlsTab", tabs, "Controls", settings.ShowControlsPanel, 0f, 46f);
        UIBuildKit.Button("KeysTab", tabs, "Key Bindings", settings.ShowKeyBindingsPanel, 0f, 46f);
        UIBuildKit.Button("CharacterTab", tabs, "Character", settings.ShowCharacterPanel, 0f, 46f);
        RectTransform tabSpacer = InventoryUIFactory.Rect("Spacer", tabs);
        InventoryUIFactory.Size(tabSpacer, -1f, 10f).flexibleHeight = 1f;
        UIBuildKit.Button("ResetAll", tabs, "Reset All Settings", settings.ResetAllSettings, 0f, 40f);
        UIBuildKit.Button("Back", tabs, "Back", settings.BackToMainMenu, 0f, 46f);

        RectTransform pages = UIBuildKit.Area("Pages", settingsWin.transform, Vector2.zero, Vector2.one);
        pages.offsetMin = new Vector2(280f, 24f);
        pages.offsetMax = new Vector2(-28f, -24f);

        // Home: shown until a page is picked.
        RectTransform home = UIBuildKit.Column("Home", pages, 12, 10f);
        InventoryUIFactory.Stretch(home);
        UIBuildKit.Title(home, "Choose a category", 24f);
        UIBuildKit.Body("Text", home, "Audio: volumes.  Graphics: quality, resolution, display.  Controls: look sensitivity.  " +
                                       "Key Bindings: change any key for keyboard and mouse or gamepad.", 17f).alignment = TextAlignmentOptions.Center;

        // Audio
        RectTransform audio = Page("AudioPanel", pages, "Audio");
        Slider master = UIBuildKit.Slider(audio, "Master Volume", 0f, 1f, 0.75f, false, out TextMeshProUGUI masterText);
        Slider music = UIBuildKit.Slider(audio, "Music Volume", 0f, 1f, 0.75f, false, out TextMeshProUGUI musicText);
        Slider sfxVol = UIBuildKit.Slider(audio, "Effects Volume", 0f, 1f, 0.75f, false, out TextMeshProUGUI sfxText);

        // Graphics (long: scrolls)
        RectTransform graphicsPage = Page("GraphicsPanel", pages, "Graphics");
        RectTransform graphics = UIBuildKit.ScrollList("Options", graphicsPage, 0f, out _);
        TMP_Dropdown quality = UIBuildKit.Dropdown(graphics, "Quality");
        TMP_Dropdown resolution = UIBuildKit.Dropdown(graphics, "Resolution");
        Toggle fullscreen = UIBuildKit.Toggle(graphics, "Fullscreen", true);
        Toggle vsync = UIBuildKit.Toggle(graphics, "VSync", true);
        Slider brightness = UIBuildKit.Slider(graphics, "Brightness", 0.2f, 2f, 1f, false, out TextMeshProUGUI brightnessText);
        Slider fps = UIBuildKit.Slider(graphics, "FPS Limit", 0f, 3f, 1f, true, out TextMeshProUGUI fpsText);
        Toggle showFps = UIBuildKit.Toggle(graphics, "Show FPS", false);
        Slider renderDistance = UIBuildKit.Slider(graphics, "Render Distance", 100f, 3000f, 1000f, true, out TextMeshProUGUI renderText);
        Toggle motionBlur = UIBuildKit.Toggle(graphics, "Motion Blur", true);
        Toggle antiAliasing = UIBuildKit.Toggle(graphics, "Anti-Aliasing", true);
        TMP_Dropdown shadows = UIBuildKit.Dropdown(graphics, "Shadows");
        UIBuildKit.Button("ResetGraphics", graphics, "Reset Graphics", settings.ResetGraphicsSettings, 0f, 40f);

        // Controls
        RectTransform controls = Page("ControlsPanel", pages, "Controls");
        Slider sensitivity = UIBuildKit.Slider(controls, "Look Sensitivity", 0.1f, 10f, LookSettings.DefaultSensitivity, false, out TextMeshProUGUI sensText);
        Toggle invert = UIBuildKit.Toggle(controls, "Invert Look Y", false);
        Button resetControls = UIBuildKit.Button("ResetControls", controls, "Reset Controls", null, 0f, 40f);

        // Character sheet: class, race, stats, traits, abilities (rows built at runtime, details on hover)
        RectTransform character = Page("CharacterPanel", pages, "Character");
        var sheet = character.gameObject.AddComponent<CharacterSheetMenu>();
        TextMeshProUGUI sheetHint = InventoryUIFactory.Text("Hint", character, "Hover a row for details: green helps, red hurts.", 14f, InventoryUIFactory.MutedColor);
        InventoryUIFactory.Size(sheetHint, -1f, 22f);
        RectTransform sheetList = UIBuildKit.ScrollList("Sheet", character, 0f, out _);
        Image hoverPanel = InventoryUIFactory.Panel("SheetHover", root.transform, new Color(0.03f, 0.04f, 0.06f, 0.97f));
        hoverPanel.raycastTarget = false;
        RectTransform hoverRt = hoverPanel.rectTransform;
        hoverRt.anchorMin = hoverRt.anchorMax = new Vector2(0.5f, 0.5f);
        hoverRt.pivot = new Vector2(0f, 1f);
        hoverRt.sizeDelta = new Vector2(420f, 10f);
        InventoryUIFactory.Vertical(hoverPanel.gameObject, 14, 4f, true);
        TextMeshProUGUI hoverText = InventoryUIFactory.Text("Text", hoverPanel.transform, "", 15f, InventoryUIFactory.TextColor, TextAlignmentOptions.TopLeft);
        hoverPanel.gameObject.SetActive(false);
        UIBuildKit.Assign(sheet, ("content", sheetList), ("hoverPanel", hoverRt), ("hoverText", hoverText));

        // Key bindings
        RectTransform keys = Page("KeyBindingsPanel", pages, "Key Bindings");
        var rebind = keys.gameObject.AddComponent<KeyRebindingMenu>();
        RectTransform keyTabs = UIBuildKit.Row("Devices", keys, 40f);
        Button kbTab = UIBuildKit.Button("KeyboardTab", keyTabs, "Keyboard & Mouse", null, 220f, 40f);
        Button padTab = UIBuildKit.Button("GamepadTab", keyTabs, "Gamepad", null, 160f, 40f);
        RectTransform keySpacer = InventoryUIFactory.Rect("Spacer", keyTabs);
        InventoryUIFactory.Size(keySpacer, -1f, -1f, 1f);
        Button resetKeys = UIBuildKit.Button("ResetKeys", keyTabs, "Reset All Keys", null, 180f, 40f);
        RectTransform keyList = UIBuildKit.ScrollList("Bindings", keys, 0f, out _);
        TextMeshProUGUI keyStatus = InventoryUIFactory.Text("Status", keys, "Click a key to change it.", 15f, InventoryUIFactory.MutedColor);
        InventoryUIFactory.Size(keyStatus, -1f, 24f);
        RectTransform templates = InventoryUIFactory.Rect("Templates", root.transform);
        GameObject rowTemplate = KeyRowTemplate(templates);
        Image waiting = InventoryUIFactory.Panel("Waiting", keys, new Color(0f, 0f, 0f, 0.85f));
        waiting.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        InventoryUIFactory.Stretch(waiting.rectTransform);
        TextMeshProUGUI waitingText = InventoryUIFactory.Text("Text", waiting.transform, "Press a key", 26f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
        InventoryUIFactory.Stretch(waitingText.rectTransform);
        waiting.gameObject.SetActive(false);

        // ---------------------------------------------------------------- countdown and FPS counter
        TextMeshProUGUI countdown = InventoryUIFactory.Text("Countdown", root.transform, "3", 96f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(countdown.rectTransform);
        countdown.gameObject.SetActive(false);
        TextMeshProUGUI fpsCounter = InventoryUIFactory.Text("FPSCounter", root.transform, "FPS", 18f, InventoryUIFactory.TextColor);
        RectTransform fpsRt = fpsCounter.rectTransform;
        fpsRt.anchorMin = fpsRt.anchorMax = fpsRt.pivot = new Vector2(0f, 1f);
        fpsRt.anchoredPosition = new Vector2(16f, -12f);
        fpsRt.sizeDelta = new Vector2(160f, 30f);
        fpsCounter.gameObject.SetActive(false);

        // Pages start hidden; the settings window too.
        audio.gameObject.SetActive(false);
        graphicsPage.gameObject.SetActive(false);
        controls.gameObject.SetActive(false);
        keys.gameObject.SetActive(false);
        character.gameObject.SetActive(false);
        settingsWin.gameObject.SetActive(false);

        // ---------------------------------------------------------------- wiring
        UIBuildKit.Assign(manager,
            ("pauseMenuUI", backdrop.gameObject), ("pauseCanvasGroup", group1), ("pauseSFX", sfx),
            ("countdownUI", countdown.gameObject), ("countdownText", countdown), ("mainMenuButton", mainMenu));
        UIBuildKit.Assign(settings,
            ("mainMenuPanel", main.gameObject), ("settingsPanel", settingsWin.gameObject), ("settingsMainContent", home.gameObject),
            ("audioPanel", audio.gameObject), ("graphicsPanel", graphicsPage.gameObject), ("controlsPanel", controls.gameObject),
            ("keyBindingsPanel", keys.gameObject), ("characterPanel", character.gameObject),
            ("masterVolumeSlider", master), ("musicVolumeSlider", music), ("sfxVolumeSlider", sfxVol),
            ("masterVolumeText", masterText), ("musicVolumeText", musicText), ("sfxVolumeText", sfxText),
            ("qualityDropdown", quality), ("resolutionDropdown", resolution), ("fullscreenToggle", fullscreen), ("vsyncToggle", vsync),
            ("brightnessSlider", brightness), ("brightnessText", brightnessText),
            ("fpsLimitSlider", fps), ("fpsLimitText", fpsText), ("showFpsToggle", showFps),
            ("renderDistanceSlider", renderDistance), ("renderDistanceText", renderText),
            ("motionBlurToggle", motionBlur), ("antiAliasingToggle", antiAliasing), ("shadowQualityDropdown", shadows),
            ("mouseSensitivitySlider", sensitivity), ("mouseSensitivityText", sensText), ("invertMouseToggle", invert),
            ("resetControlsButton", resetControls),
            ("fpsCounterObject", fpsCounter.gameObject), ("fpsCounterText", fpsCounter));
        UIBuildKit.Assign(rebind,
            ("rowContainer", keyList), ("rowTemplate", rowTemplate), ("keyboardTab", kbTab), ("gamepadTab", padTab),
            ("resetAllButton", resetKeys), ("waitingOverlay", waiting.gameObject), ("waitingText", waitingText), ("statusText", keyStatus));

        backdrop.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeObject = root;
        Debug.Log("[UI Builder] Pause & Settings menu built. Escape / Start opens it while a player is in the game. " +
                  "Set 'Main Menu Scene Name' on the Pause Menu Manager (the button hides itself when that scene is not in Build Settings).", root);
        return manager;
    }

    /// <summary>A settings page: title and a column of options, stretched over the pages area.</summary>
    private static RectTransform Page(string name, Transform pages, string title)
    {
        RectTransform page = UIBuildKit.Column(name, pages, 8, 12f);
        InventoryUIFactory.Stretch(page);
        UIBuildKit.Title(page, title, 26f).alignment = TextAlignmentOptions.Left;
        return page;
    }

    /// <summary>One key row: action name, binding button, reset button (inactive template).</summary>
    private static GameObject KeyRowTemplate(Transform parent)
    {
        RectTransform row = UIBuildKit.Row("KeyRowTemplate", parent, 40f, 10f);
        TextMeshProUGUI action = InventoryUIFactory.Text("Action", row, "Action", 17f, InventoryUIFactory.TextColor);
        InventoryUIFactory.Size(action, -1f, 40f, 1f);
        Button binding = InventoryUIFactory.Button("Binding", row, "Key", null, 220f);
        InventoryUIFactory.Size(binding, 220f, 38f, 0f);
        Button reset = InventoryUIFactory.Button("Reset", row, "Reset", null, 90f);
        InventoryUIFactory.Size(reset, 90f, 38f, 0f);
        row.gameObject.SetActive(false);
        return row.gameObject;
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
