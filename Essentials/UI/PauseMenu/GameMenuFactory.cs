using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the in-game menu from code: the pause window (Resume, Character, Settings, Main Menu, Quit) and the settings
/// window — a sidebar of tabs and the Audio, Graphics, Controls, Key Bindings and Character pages — wired to
/// <see cref="PauseMenuManager"/>, <see cref="PauseMenuSettings"/>, <see cref="KeyRebindingMenu"/> and
/// <see cref="CharacterSheetMenu"/>. The same layout is used three ways:
/// <list type="bullet">
/// <item>at runtime, when a game scene has no menu (<see cref="CreateRuntimeMenu"/>, or a Pause Menu Manager without UI);</item>
/// <item>by the start screens, for their Settings button (<see cref="BuildSettings"/> on its own);</item>
/// <item>by Tools ▸ SimpleMovements ▸ Scene ▸ Build Pause &amp; Settings Menu, which saves it in the scene (buttons are
/// then wired as persistent events through the <see cref="Wire"/> callback).</item>
/// </list>
/// </summary>
public static class GameMenuFactory
{
    /// <summary>Connects a button to an action (runtime: a listener; editor builder: a saved persistent event).</summary>
    public delegate void Wire(Button button, UnityAction action);

    private static void RuntimeWire(Button button, UnityAction action)
    {
        if (button != null && action != null)
            button.onClick.AddListener(action);
    }

    /// <summary>A menu for a game scene that has none (created by the character system once a player is in the game).</summary>
    public static PauseMenuManager CreateRuntimeMenu()
    {
        if (PauseMenuManager.Instance != null)
            return PauseMenuManager.Instance;
        InventoryEventSystems.EnsureSingle();
        Canvas canvas = UITheme.Canvas("Game Menu (runtime)", 500);
        var manager = canvas.gameObject.AddComponent<PauseMenuManager>();
        Build(canvas.gameObject, manager, null);
        return manager;
    }

    /// <summary>
    /// Builds the pause and settings windows under <paramref name="host"/> (its canvas; one is created under it when
    /// it has none) and wires them to <paramref name="manager"/>. <paramref name="wire"/> null = runtime listeners.
    /// </summary>
    public static PauseMenuSettings Build(GameObject host, PauseMenuManager manager, Wire wire)
    {
        wire = wire ?? RuntimeWire;
        Canvas canvas = host.GetComponentInParent<Canvas>();
        Transform root = canvas != null ? host.transform : UITheme.Canvas("Menu", 500, host.transform).transform;
        PauseMenuSettings settings = host.GetComponent<PauseMenuSettings>();
        if (settings == null) settings = host.AddComponent<PauseMenuSettings>();
        AudioSource sfx = host.GetComponent<AudioSource>();
        if (sfx == null)
        {
            sfx = host.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.ignoreListenerPause = true;
        }

        // ---------------------------------------------------------------- pause window
        Image backdrop = InventoryUIFactory.Panel("PauseMenu", root, UITheme.Backdrop);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        CanvasGroup group = backdrop.gameObject.AddComponent<CanvasGroup>();

        Image main = UITheme.Window("MainPanel", backdrop.transform, new Vector2(460f, 600f));
        RectTransform mainCol = UITheme.Column("Content", main.transform, 40, 12f);
        InventoryUIFactory.Stretch(mainCol);
        mainCol.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
        TextMeshProUGUI kicker = UITheme.Label(mainCol, "GAME MENU", 14f, UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        kicker.characterSpacing = 6f;
        InventoryUIFactory.Size(kicker, -1f, 22f);
        UITheme.Title(mainCol, "Paused", 44f, TextAlignmentOptions.Center);
        UITheme.Spacer(mainCol, false).GetComponent<LayoutElement>().minHeight = 6f;
        Button resume = UITheme.Button("Resume", mainCol, "Resume", null, UITheme.ButtonStyle.Primary, 0f, 54f);
        Button character = UITheme.Button("Character", mainCol, "Character", null, UITheme.ButtonStyle.Normal, 0f, 50f);
        Button settingsBtn = UITheme.Button("Settings", mainCol, "Settings", null, UITheme.ButtonStyle.Normal, 0f, 50f);
        Button mainMenu = UITheme.Button("MainMenu", mainCol, "Main Menu", null, UITheme.ButtonStyle.Normal, 0f, 50f);
        Button quit = UITheme.Button("Quit", mainCol, "Quit Game", null, UITheme.ButtonStyle.Ghost, 0f, 50f);
        UITheme.Spacer(mainCol, false);
        TextMeshProUGUI hint = UITheme.Label(mainCol, "Esc / Start: resume", 14f, UITheme.Muted, TextAlignmentOptions.Center);
        InventoryUIFactory.Size(hint, -1f, 22f);
        if (manager != null)
        {
            wire(resume, manager.ResumeButton);
            wire(mainMenu, manager.ReturnToMainMenu);
            wire(quit, manager.QuitGame);
        }
        wire(character, settings.OpenCharacterSheet);
        wire(settingsBtn, settings.ShowSettingsPanel);

        // ---------------------------------------------------------------- settings window
        BuildSettings(backdrop.transform, settings, main.gameObject, wire, true, root);

        // ---------------------------------------------------------------- countdown and FPS counter
        TextMeshProUGUI countdown = InventoryUIFactory.Text("Countdown", root, "3", 110f, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(countdown.rectTransform);
        countdown.gameObject.SetActive(false);
        Image fpsBg = UITheme.Panel("FPSCounter", root, new Color(0f, 0f, 0f, 0.45f), UITheme.RadiusSmall);
        RectTransform fpsRt = fpsBg.rectTransform;
        fpsRt.anchorMin = fpsRt.anchorMax = fpsRt.pivot = new Vector2(0f, 1f);
        fpsRt.anchoredPosition = new Vector2(14f, -12f);
        fpsRt.sizeDelta = new Vector2(120f, 30f);
        fpsBg.raycastTarget = false;
        TextMeshProUGUI fpsText = UITheme.Label(fpsBg.transform, "FPS", 16f, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(fpsText.rectTransform);
        fpsBg.gameObject.SetActive(false);
        settings.fpsCounterObject = fpsBg.gameObject;
        settings.fpsCounterText = fpsText;

        if (manager != null)
        {
            manager.pauseMenuUI = backdrop.gameObject;
            manager.pauseCanvasGroup = group;
            manager.pauseSFX = sfx;
            manager.countdownUI = countdown.gameObject;
            manager.countdownText = countdown;
            manager.mainMenuButton = mainMenu;
        }
        backdrop.gameObject.SetActive(false);
        return settings;
    }

    /// <summary>
    /// The settings window (sidebar + Audio, Graphics, Controls, Key Bindings and, with <paramref name="characterPage"/>,
    /// Character pages) under <paramref name="parent"/>, wired to <paramref name="settings"/>. Back shows
    /// <paramref name="backTarget"/> again. Returns the window (hidden).
    /// </summary>
    public static GameObject BuildSettings(Transform parent, PauseMenuSettings settings, GameObject backTarget, Wire wire, bool characterPage, Transform overlayRoot = null)
    {
        wire = wire ?? RuntimeWire;
        overlayRoot = overlayRoot != null ? overlayRoot : parent;
        Image window = UITheme.Window("SettingsPanel", parent, new Vector2(1240f, 760f));

        // ---------------------------------------------------------------- sidebar
        Image side = UITheme.Panel("Sidebar", window.transform, UITheme.Sidebar, UITheme.RadiusLarge);
        UITheme.Place(side.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(290f, 0f));
        RectTransform tabs = UITheme.Column("Tabs", side.transform, 24, 8f);
        InventoryUIFactory.Stretch(tabs);
        TextMeshProUGUI kicker = UITheme.Label(tabs, "OPTIONS", 13f, UITheme.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        kicker.characterSpacing = 5f;
        InventoryUIFactory.Size(kicker, -1f, 20f);
        UITheme.Title(tabs, "Settings", 32f);
        UITheme.Spacer(tabs, false).GetComponent<LayoutElement>().minHeight = 8f;
        Button audioTab = UITheme.Button("AudioTab", tabs, "Audio", null, UITheme.ButtonStyle.Tab, 0f, 50f);
        Button graphicsTab = UITheme.Button("GraphicsTab", tabs, "Graphics", null, UITheme.ButtonStyle.Tab, 0f, 50f);
        Button controlsTab = UITheme.Button("ControlsTab", tabs, "Controls", null, UITheme.ButtonStyle.Tab, 0f, 50f);
        Button keysTab = UITheme.Button("KeysTab", tabs, "Key Bindings", null, UITheme.ButtonStyle.Tab, 0f, 50f);
        Button characterTab = characterPage ? UITheme.Button("CharacterTab", tabs, "Character", null, UITheme.ButtonStyle.Tab, 0f, 50f) : null;
        UITheme.Spacer(tabs, false);
        Button resetAll = UITheme.Button("ResetAll", tabs, "Reset All Settings", null, UITheme.ButtonStyle.Ghost, 0f, 44f);
        Button back = UITheme.Button("Back", tabs, "Back", null, UITheme.ButtonStyle.Normal, 0f, 50f);
        wire(audioTab, settings.ShowAudioPanel);
        wire(graphicsTab, settings.ShowGraphicsPanel);
        wire(controlsTab, settings.ShowControlsPanel);
        wire(keysTab, settings.ShowKeyBindingsPanel);
        if (characterTab != null) wire(characterTab, settings.ShowCharacterPanel);
        wire(resetAll, settings.ResetAllSettings);
        wire(back, settings.BackToMainMenu);

        RectTransform pages = UITheme.Area("Pages", window.transform, Vector2.zero, Vector2.one, new Vector2(318f, 26f), new Vector2(-30f, -26f));

        // ---------------------------------------------------------------- home
        RectTransform home = UITheme.Column("Home", pages, 12, 14f);
        InventoryUIFactory.Stretch(home);
        UITheme.Title(home, "Choose a category", 28f);
        HomeCard(home, "Audio", "Master, music and effects volume.");
        HomeCard(home, "Graphics", "Quality, resolution, display mode, frame rate, shadows and view distance.");
        HomeCard(home, "Controls", "Look sensitivity and inverted look.");
        HomeCard(home, "Key Bindings", "Change any key for keyboard and mouse or gamepad. Click a key, then press the new one.");
        if (characterPage) HomeCard(home, "Character", "Your class, race, stats, traits and abilities.");

        // ---------------------------------------------------------------- audio
        RectTransform audio = Page("AudioPanel", pages, "Audio", "Saved for every player of this computer.");
        Slider master = UITheme.Slider(audio, "Master Volume", 0f, 1f, 0.75f, false, out TextMeshProUGUI masterText);
        Slider music = UITheme.Slider(audio, "Music Volume", 0f, 1f, 0.75f, false, out TextMeshProUGUI musicText);
        Slider sfxVol = UITheme.Slider(audio, "Effects Volume", 0f, 1f, 0.75f, false, out TextMeshProUGUI sfxText);

        // ---------------------------------------------------------------- graphics (long: scrolls)
        RectTransform graphicsPage = Page("GraphicsPanel", pages, "Graphics", "Display and rendering options.");
        RectTransform graphics = UITheme.ScrollList("Options", graphicsPage, 0f, out _, new Color(0f, 0f, 0f, 0f), 2, 8f);
        UITheme.Header(graphics, "Display");
        TMP_Dropdown quality = UITheme.Dropdown(graphics, "Quality");
        TMP_Dropdown resolution = UITheme.Dropdown(graphics, "Resolution");
        Toggle fullscreen = UITheme.Toggle(graphics, "Fullscreen", true);
        Toggle vsync = UITheme.Toggle(graphics, "VSync", true);
        Slider fps = UITheme.Slider(graphics, "FPS Limit", 0f, 3f, 1f, true, out TextMeshProUGUI fpsLimitText);
        Toggle showFps = UITheme.Toggle(graphics, "Show FPS", false);
        UITheme.Header(graphics, "Image");
        Slider brightness = UITheme.Slider(graphics, "Brightness", 0.2f, 2f, 1f, false, out TextMeshProUGUI brightnessText);
        TMP_Dropdown shadows = UITheme.Dropdown(graphics, "Shadows");
        Slider renderDistance = UITheme.Slider(graphics, "Render Distance", 100f, 3000f, 1000f, true, out TextMeshProUGUI renderText);
        Toggle motionBlur = UITheme.Toggle(graphics, "Motion Blur", true);
        Toggle antiAliasing = UITheme.Toggle(graphics, "Anti-Aliasing", true);
        Button resetGraphics = UITheme.Button("ResetGraphics", graphics, "Reset Graphics", null, UITheme.ButtonStyle.Ghost, 0f, 44f);
        wire(resetGraphics, settings.ResetGraphicsSettings);

        // ---------------------------------------------------------------- controls
        RectTransform controls = Page("ControlsPanel", pages, "Controls", "How the camera follows your mouse or stick.");
        Slider sensitivity = UITheme.Slider(controls, "Look Sensitivity", 0.1f, 10f, LookSettings.DefaultSensitivity, false, out TextMeshProUGUI sensText);
        Toggle invert = UITheme.Toggle(controls, "Invert Look Y", false);
        RectTransform controlsRow = UITheme.Row("Buttons", controls, 46f, 10f);
        UITheme.Spacer(controlsRow);
        Button resetControls = UITheme.Button("ResetControls", controlsRow, "Reset Controls", null, UITheme.ButtonStyle.Ghost, 220f, 44f);

        // ---------------------------------------------------------------- key bindings
        RectTransform keys = Page("KeyBindingsPanel", pages, "Key Bindings", "Click a key and press the new one. Escape cancels.");
        keys.gameObject.SetActive(false); // the rebinding menu wakes up only once it is configured
        var rebind = keys.gameObject.AddComponent<KeyRebindingMenu>();
        RectTransform keyTabs = UITheme.Row("Devices", keys, 46f, 8f);
        Button kbTab = UITheme.Button("KeyboardTab", keyTabs, "Keyboard & Mouse", null, UITheme.ButtonStyle.Tab, 220f, 44f);
        Button padTab = UITheme.Button("GamepadTab", keyTabs, "Gamepad", null, UITheme.ButtonStyle.Tab, 160f, 44f);
        foreach (Button b in new[] { kbTab, padTab })
        {
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.alignment = TextAlignmentOptions.Center;
        }
        UITheme.Spacer(keyTabs);
        Button resetKeys = UITheme.Button("ResetKeys", keyTabs, "Reset All Keys", null, UITheme.ButtonStyle.Ghost, 190f, 44f);
        RectTransform keyList = UITheme.ScrollList("Bindings", keys, 0f, out _, null, 6, 6f);
        TextMeshProUGUI keyStatus = UITheme.Label(keys, "Click a key to change it.", 15f, UITheme.Muted);
        InventoryUIFactory.Size(keyStatus, -1f, 24f);
        RectTransform templates = InventoryUIFactory.Rect("Templates", window.transform);
        templates.gameObject.SetActive(false);
        GameObject rowTemplate = KeyRowTemplate(templates);
        Image waiting = UITheme.Panel("Waiting", keys, new Color(0.02f, 0.03f, 0.05f, 0.92f), UITheme.RadiusLarge);
        waiting.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        InventoryUIFactory.Stretch(waiting.rectTransform);
        TextMeshProUGUI waitingText = UITheme.Label(waiting.transform, "Press a key", 28f, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(waitingText.rectTransform);
        waiting.gameObject.SetActive(false);
        rebind.Configure(keyList, rowTemplate, kbTab, padTab, resetKeys, waiting.gameObject, waitingText, keyStatus);

        // ---------------------------------------------------------------- character sheet
        RectTransform characterPanel = null;
        if (characterPage)
        {
            characterPanel = Page("CharacterPanel", pages, "Character", "Hover a row for every source: green helps, red hurts.");
            characterPanel.gameObject.SetActive(false);
            var sheet = characterPanel.gameObject.AddComponent<CharacterSheetMenu>();
            RectTransform sheetList = UITheme.ScrollList("Sheet", characterPanel, 0f, out _, null, 8, 4f);
            HoverPanel(overlayRoot, out RectTransform hoverRt, out TextMeshProUGUI hoverText);
            sheet.Configure(sheetList, hoverRt, hoverText, true, 0f);
        }

        // ---------------------------------------------------------------- tabs highlight the open page
        var tabGroup = window.gameObject.AddComponent<UITabGroup>();
        tabGroup.Add(audioTab, audio.gameObject);
        tabGroup.Add(graphicsTab, graphicsPage.gameObject);
        tabGroup.Add(controlsTab, controls.gameObject);
        tabGroup.Add(keysTab, keys.gameObject);
        if (characterTab != null) tabGroup.Add(characterTab, characterPanel.gameObject);

        audio.gameObject.SetActive(false);
        graphicsPage.gameObject.SetActive(false);
        controls.gameObject.SetActive(false);
        keys.gameObject.SetActive(false);
        if (characterPanel != null) characterPanel.gameObject.SetActive(false);
        window.gameObject.SetActive(false);

        // ---------------------------------------------------------------- wiring
        settings.mainMenuPanel = backTarget;
        settings.settingsPanel = window.gameObject;
        settings.settingsMainContent = home.gameObject;
        settings.audioPanel = audio.gameObject;
        settings.graphicsPanel = graphicsPage.gameObject;
        settings.controlsPanel = controls.gameObject;
        settings.keyBindingsPanel = keys.gameObject;
        settings.characterPanel = characterPanel != null ? characterPanel.gameObject : null;
        settings.masterVolumeSlider = master;
        settings.musicVolumeSlider = music;
        settings.sfxVolumeSlider = sfxVol;
        settings.masterVolumeText = masterText;
        settings.musicVolumeText = musicText;
        settings.sfxVolumeText = sfxText;
        settings.qualityDropdown = quality;
        settings.resolutionDropdown = resolution;
        settings.fullscreenToggle = fullscreen;
        settings.vsyncToggle = vsync;
        settings.brightnessSlider = brightness;
        settings.brightnessText = brightnessText;
        settings.fpsLimitSlider = fps;
        settings.fpsLimitText = fpsLimitText;
        settings.showFpsToggle = showFps;
        settings.renderDistanceSlider = renderDistance;
        settings.renderDistanceText = renderText;
        settings.motionBlurToggle = motionBlur;
        settings.antiAliasingToggle = antiAliasing;
        settings.shadowQualityDropdown = shadows;
        settings.mouseSensitivitySlider = sensitivity;
        settings.mouseSensitivityText = sensText;
        settings.invertMouseToggle = invert;
        settings.resetControlsButton = resetControls;
        return window.gameObject;
    }

    /// <summary>A settings page: title, subtitle and a column of options, stretched over the pages area.</summary>
    private static RectTransform Page(string name, Transform pages, string title, string subtitle)
    {
        RectTransform page = UITheme.Column(name, pages, 8, 10f);
        InventoryUIFactory.Stretch(page);
        UITheme.Title(page, title, 30f);
        TextMeshProUGUI sub = UITheme.Label(page, subtitle, 15f, UITheme.Muted);
        InventoryUIFactory.Size(sub, -1f, 26f);
        return page;
    }

    private static void HomeCard(Transform parent, string title, string text)
    {
        RectTransform card = UITheme.CardColumn(title, parent, 18, 4f);
        UITheme.Label(card, title, 19f, null, TextAlignmentOptions.Left, FontStyles.Bold);
        UITheme.Label(card, text, 15f, UITheme.Muted);
    }

    /// <summary>The panel that follows the mouse with a hovered row's details.</summary>
    public static void HoverPanel(Transform parent, out RectTransform panel, out TextMeshProUGUI text)
    {
        Image hover = UITheme.Panel("SheetHover", parent, new Color(0.03f, 0.04f, 0.06f, 0.97f), UITheme.Radius);
        hover.raycastTarget = false;
        panel = hover.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(420f, 10f);
        InventoryUIFactory.Vertical(hover.gameObject, 16, 4f, true);
        text = InventoryUIFactory.Text("Text", hover.transform, "", 15f, UITheme.Text, TextAlignmentOptions.TopLeft);
        hover.gameObject.SetActive(false);
    }

    /// <summary>One key row: action name, binding button, reset button (inactive template).</summary>
    private static GameObject KeyRowTemplate(Transform parent)
    {
        Image bg = UITheme.Panel("KeyRowTemplate", parent, UITheme.Card, UITheme.Radius);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 10f);
        h.padding = new RectOffset(16, 8, 5, 5);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        InventoryUIFactory.Size(bg, -1f, 50f);
        TextMeshProUGUI action = InventoryUIFactory.Text("Action", bg.transform, "Action", 17f, UITheme.Text);
        InventoryUIFactory.Size(action, -1f, 40f, 1f);
        Button binding = UITheme.Button("Binding", bg.transform, "Key", null, UITheme.ButtonStyle.Normal, 230f, 40f);
        Button reset = UITheme.Button("Reset", bg.transform, "Reset", null, UITheme.ButtonStyle.Ghost, 90f, 40f);
        TMP_Text rt = reset.GetComponentInChildren<TMP_Text>(true);
        if (rt != null) { rt.fontSize = 14f; rt.color = UITheme.Muted; }
        bg.gameObject.SetActive(false);
        return bg.gameObject;
    }
}
