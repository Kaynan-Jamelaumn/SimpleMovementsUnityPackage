using System;
using System.Collections.Generic;
using System.Text;
using GameNet;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The start screens, built from code: the main menu (Play Solo, Host, Join with address and port, Dedicated
/// Server, Settings, Quit), the character select (saved characters with a 3D preview, Play, Delete, New Character)
/// and the character creation screen (<see cref="CharacterCreatorPanel"/>). A new player with no saved character goes
/// straight to creation. When a character is picked it becomes <see cref="CharacterSession.Current"/> and
/// <see cref="PlayRequested"/> is raised with the chosen mode (the <c>NetBootstrap</c> then starts the game).
/// </summary>
public class FrontEndMenu : MonoBehaviour
{
    /// <summary>The player picked a mode and (except for a dedicated server) a character.</summary>
    public event Action<NetMode> PlayRequested;

    /// <summary>Quit was pressed (the default quits the application).</summary>
    public event Action QuitRequested;

    public string Address { get => addressInput != null ? addressInput.text : address; set { address = value; if (addressInput != null) addressInput.text = value; } }
    public string Port { get => portInput != null ? portInput.text : port; set { port = value; if (portInput != null) portInput.text = value; } }

    private string title = "Simple Movements";
    private string subtitle = "Choose how to play";
    private bool networkModes = true;
    private bool allowServer = true;
    private string address = NetLaunchOptions.DefaultAddress;
    private string port = NetLaunchOptions.DefaultPort.ToString();

    private Canvas canvas;
    private RectTransform mainPage, charactersPage;
    private GameObject settingsWindow;
    private PauseMenuSettings settings;
    private TMP_InputField addressInput, portInput;
    private TextMeshProUGUI statusText, modeText, detailText;
    private RectTransform characterList;
    private Button playButton, deleteButton;
    private TextMeshProUGUI deleteLabel;
    private RawImage selectPicture;
    private CharacterPreviewDrag selectDrag;
    private CharacterPreviewStage stage;
    private CharacterCreatorPanel creator;
    private readonly Dictionary<CharacterSave, Button> cards = new Dictionary<CharacterSave, Button>();
    private List<CharacterSave> saves = new List<CharacterSave>();
    private CharacterSave selected;
    private NetMode pendingMode = NetMode.Offline;
    private bool browsing; // opened with the Characters button: no mode chosen yet
    private GameObject browseRow;
    private bool deleteArmed;

    /// <summary>Builds and shows the start screens.</summary>
    public static FrontEndMenu Create(string gameTitle, bool offerNetworkModes, bool offerDedicatedServer, string defaultAddress, string defaultPort)
    {
        InventoryEventSystems.EnsureSingle();
        Canvas c = UITheme.Canvas("Start Screens", 300);
        var menu = c.gameObject.AddComponent<FrontEndMenu>();
        menu.canvas = c;
        if (!string.IsNullOrEmpty(gameTitle)) menu.title = gameTitle;
        menu.networkModes = offerNetworkModes;
        menu.allowServer = offerDedicatedServer;
        menu.address = defaultAddress;
        menu.port = defaultPort;
        menu.Build();
        menu.ShowMain();
        return menu;
    }

    private void OnDestroy()
    {
        if (stage != null)
            Destroy(stage.gameObject);
    }

    // ------------------------------------------------------------------ build
    private void Build()
    {
        Image bg = InventoryUIFactory.Panel("Background", transform, UITheme.Background);
        InventoryUIFactory.Stretch(bg.rectTransform);
        // Two soft glows give the backdrop some depth.
        Glow(new Vector2(0.15f, 0.85f), 1100f, new Color(0.36f, 0.62f, 1f, 0.07f));
        Glow(new Vector2(0.9f, 0.1f), 900f, new Color(0.62f, 0.4f, 1f, 0.05f));

        BuildMain();
        BuildCharacters();

        settings = gameObject.AddComponent<PauseMenuSettings>();
        settingsWindow = GameMenuFactory.BuildSettings(transform, settings, mainPage.gameObject, null, false, transform);
    }

    private void Glow(Vector2 anchor, float size, Color color)
    {
        Image g = UITheme.Panel("Glow", transform, color, 64);
        g.raycastTarget = false;
        RectTransform rt = g.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(size, size);
    }

    private void BuildMain()
    {
        mainPage = UITheme.Area("Main", transform, Vector2.zero, Vector2.one);

        RectTransform left = UITheme.Area("Title Block", mainPage, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(120f, 0f), new Vector2(-40f, 0f));
        VerticalLayoutGroup lv = InventoryUIFactory.Vertical(left.gameObject, 0, 8f, false);
        lv.childAlignment = TextAnchor.MiddleLeft;
        lv.childForceExpandHeight = false;
        TextMeshProUGUI kicker = UITheme.Label(left, "WELCOME", 16f, UITheme.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        kicker.characterSpacing = 8f;
        InventoryUIFactory.Size(kicker, -1f, 26f);
        TextMeshProUGUI big = UITheme.Label(left, title, 76f, null, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Size(big, -1f, 180f);
        TextMeshProUGUI sub = UITheme.Label(left, subtitle, 22f, UITheme.Muted, TextAlignmentOptions.TopLeft);
        InventoryUIFactory.Size(sub, -1f, 40f);

        Image card = UITheme.Window("Modes", mainPage, new Vector2(520f, networkModes ? 720f : 420f));
        RectTransform cr = card.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.72f, 0.5f);
        RectTransform col = UITheme.Column("Content", card.transform, 34, 12f);
        InventoryUIFactory.Stretch(col);
        UITheme.Header(col, "Play");
        UITheme.Button("Solo", col, "Play Solo", () => ChooseMode(NetMode.Offline), UITheme.ButtonStyle.Primary, 0f, 56f);
        if (networkModes)
        {
            UITheme.Button("Host", col, "Host Game  (server + play)", () => ChooseMode(NetMode.Host), UITheme.ButtonStyle.Normal, 0f, 52f);
            UITheme.Header(col, "Join a game");
            RectTransform row = UITheme.Row("Address", col, 48f, 10f);
            addressInput = UITheme.Input("Address", row, "127.0.0.1", 64, 46f);
            addressInput.text = address;
            portInput = UITheme.Input("Port", row, "Port", 5, 46f);
            InventoryUIFactory.Size((RectTransform)portInput.transform, 120f, 46f, 0f);
            portInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            portInput.text = port;
            UITheme.Button("Join", col, "Join Game", () => ChooseMode(NetMode.Client), UITheme.ButtonStyle.Normal, 0f, 52f);
            if (allowServer)
                UITheme.Button("Server", col, "Dedicated Server  (this window)", () => ChooseMode(NetMode.Server), UITheme.ButtonStyle.Ghost, 0f, 46f);
        }
        UITheme.Header(col, "Other");
        RectTransform other = UITheme.Row("Other", col, 50f, 10f);
        UITheme.Button("Characters", other, "Characters", () => { browsing = true; ShowCharacters(false); }, UITheme.ButtonStyle.Normal, 0f, 48f);
        UITheme.Button("Settings", other, "Settings", () => settings.ShowSettingsPanel(), UITheme.ButtonStyle.Normal, 0f, 48f);
        UITheme.Button("Quit", other, "Quit", Quit, UITheme.ButtonStyle.Ghost, 0f, 48f);
        UITheme.Spacer(col, false);
        statusText = UITheme.Label(col, "", 15f, UITheme.Muted, TextAlignmentOptions.Center);
        InventoryUIFactory.Size(statusText, -1f, 40f);
    }

    private void BuildCharacters()
    {
        charactersPage = UITheme.Area("Characters", transform, Vector2.zero, Vector2.one);

        RectTransform header = UITheme.Area("Header", charactersPage, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -100f), new Vector2(-40f, -20f));
        VerticalLayoutGroup hv = InventoryUIFactory.Vertical(header.gameObject, 0, 0f, false);
        hv.childForceExpandHeight = false;
        modeText = UITheme.Label(header, "", 14f, UITheme.Accent, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        modeText.characterSpacing = 4f;
        InventoryUIFactory.Size(modeText, -1f, 26f);
        UITheme.Label(header, "Choose Your Character", 36f, null, TextAlignmentOptions.TopLeft, FontStyles.Bold);

        // Left: the saved characters.
        Image listPanel = UITheme.Panel("List Panel", charactersPage, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(listPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(40f, 116f), new Vector2(600f, -116f));
        RectTransform listCol = UITheme.Column("Content", listPanel.transform, 20, 10f);
        InventoryUIFactory.Stretch(listCol);
        UITheme.Header(listCol, "Saved characters");
        characterList = UITheme.ScrollList("Characters", listCol, 0f, out _);
        UITheme.Button("New", listCol, "+  New Character", OpenCreator, UITheme.ButtonStyle.Normal, 0f, 52f);

        // Centre: the preview.
        RectTransform center = UITheme.Area("Preview Area", charactersPage, Vector2.zero, Vector2.one, new Vector2(628f, 116f), new Vector2(-548f, -116f));
        Image frame = UITheme.Panel("Preview", center, new Color(0.06f, 0.07f, 0.095f, 1f), UITheme.RadiusLarge);
        InventoryUIFactory.Stretch(frame.rectTransform);
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        RectTransform fit = UITheme.Area("Fit", frame.transform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        selectPicture = InventoryUIFactory.Rect("Picture", fit).gameObject.AddComponent<RawImage>();
        selectPicture.rectTransform.anchorMin = selectPicture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        selectPicture.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        selectPicture.color = new Color(1f, 1f, 1f, 0f);
        selectDrag = selectPicture.gameObject.AddComponent<CharacterPreviewDrag>();

        // Right: details and actions.
        Image info = UITheme.Panel("Info Panel", charactersPage, UITheme.Surface, UITheme.RadiusLarge);
        UITheme.Place(info.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-520f, 116f), new Vector2(-40f, -116f));
        RectTransform infoCol = UITheme.Column("Content", info.transform, 22, 10f);
        InventoryUIFactory.Stretch(infoCol);
        RectTransform detail = UITheme.ScrollList("Details", infoCol, 0f, out _, null, 10, 4f);
        detailText = UITheme.Label(detail, "", 15f, UITheme.Text, TextAlignmentOptions.TopLeft);
        playButton = UITheme.Button("Play", infoCol, "Play", PlaySelected, UITheme.ButtonStyle.Primary, 0f, 58f);
        // Opened from the Characters button: pick how to play here.
        RectTransform modes = UITheme.Row("Play Modes", infoCol, 56f, 8f);
        UITheme.Button("Solo", modes, "Play Solo", () => PlayAs(NetMode.Offline), UITheme.ButtonStyle.Primary, 0f, 54f);
        if (networkModes)
        {
            UITheme.Button("Host", modes, "Host", () => PlayAs(NetMode.Host), UITheme.ButtonStyle.Normal, 0f, 54f);
            UITheme.Button("Join", modes, "Join", () => PlayAs(NetMode.Client), UITheme.ButtonStyle.Normal, 0f, 54f);
        }
        browseRow = modes.gameObject;
        RectTransform row = UITheme.Row("Actions", infoCol, 48f, 10f);
        UITheme.Button("Back", row, "Back", ShowMain, UITheme.ButtonStyle.Ghost, 0f, 46f);
        deleteButton = UITheme.Button("Delete", row, "Delete", DeleteSelected, UITheme.ButtonStyle.Normal, 0f, 46f);
        deleteLabel = deleteButton.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private CharacterPreviewStage Stage()
    {
        if (stage == null)
        {
            stage = CharacterPreviewStage.Create(CharacterPreviewStage.DefaultSource());
            if (selectPicture != null && stage.Texture != null)
            {
                selectPicture.texture = stage.Texture;
                selectPicture.color = Color.white;
                selectPicture.GetComponent<AspectRatioFitter>().aspectRatio = stage.Texture.width / (float)stage.Texture.height;
                selectDrag.stage = stage;
            }
        }
        return stage;
    }

    // ------------------------------------------------------------------ pages
    public void ShowMain()
    {
        if (creator != null) creator.Close();
        charactersPage.gameObject.SetActive(false);
        if (settingsWindow != null) settingsWindow.SetActive(false);
        mainPage.gameObject.SetActive(true);
    }

    /// <summary>The character select for <paramref name="mode"/> (no saved character: the creation screen).</summary>
    public void ShowCharacters(NetMode mode)
    {
        pendingMode = mode;
        browsing = false;
        ShowCharacters(true);
    }

    private void ShowCharacters(bool forPlay)
    {
        mainPage.gameObject.SetActive(false);
        if (settingsWindow != null) settingsWindow.SetActive(false);
        if (creator != null) creator.Close();
        saves = CharacterSaveStore.LoadAll();
        if (saves.Count == 0 && forPlay)
        {
            OpenCreator();
            return;
        }
        charactersPage.gameObject.SetActive(true);
        Stage();
        modeText.text = browsing ? "YOUR CHARACTERS" : ModeLabel(pendingMode).ToUpperInvariant();
        playButton.gameObject.SetActive(!browsing);
        if (browseRow != null) browseRow.SetActive(browsing);
        RebuildList();
        Select(saves.Contains(selected) ? selected : saves.Count > 0 ? saves[0] : null);
    }

    private void OpenCreator()
    {
        mainPage.gameObject.SetActive(false);
        charactersPage.gameObject.SetActive(false);
        CharacterPreviewStage s = Stage();
        if (creator == null)
        {
            creator = new CharacterCreatorPanel(transform, CharacterCatalog.Instance, s);
            creator.Created += OnCreated;
            creator.Cancelled += () =>
            {
                if (CharacterSaveStore.LoadAll().Count > 0) ShowCharacters(false);
                else ShowMain();
            };
        }
        creator.Open();
    }

    private void OnCreated(CharacterSave save)
    {
        selected = save;
        Debug.Log($"[Start] Character '{save.characterName}' created ({save.Summary()}).");
        if (browsing)
            ShowCharacters(false); // no mode chosen yet: back to the list, the new character selected
        else
            Play(save);
    }

    private void PlayAs(NetMode mode)
    {
        if (selected == null) return;
        pendingMode = mode;
        browsing = false;
        Play(selected);
    }

    private static string ModeLabel(NetMode mode)
    {
        switch (mode)
        {
            case NetMode.Host: return "Hosting a game";
            case NetMode.Client: return "Joining a game";
            case NetMode.Server: return "Dedicated server";
            default: return "Single player";
        }
    }

    // ------------------------------------------------------------------ character list
    private void RebuildList()
    {
        for (int i = characterList.childCount - 1; i >= 0; i--)
            Destroy(characterList.GetChild(i).gameObject);
        cards.Clear();
        if (saves.Count == 0)
        {
            UITheme.Label(characterList, "No characters yet. Create one!", 16f, UITheme.Muted);
            return;
        }
        CharacterCatalog catalog = CharacterCatalog.Instance;
        foreach (CharacterSave s in saves)
        {
            CharacterSave save = s;
            Image bg = InventoryUIFactory.Panel(s.characterName, characterList, Color.white);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            UITheme.Colors(b, UITheme.ButtonStyle.ListItem);
            InventoryUIFactory.Size(bg, -1f, 86f);
            VerticalLayoutGroup v = InventoryUIFactory.Vertical(bg.gameObject, 0, 2f, false);
            v.padding = new RectOffset(22, 16, 12, 10);
            v.childForceExpandHeight = false;
            TextMeshProUGUI n = UITheme.Label(bg.transform, string.IsNullOrEmpty(s.characterName) ? "Unnamed" : s.characterName, 21f, null, TextAlignmentOptions.Left, FontStyles.Bold);
            InventoryUIFactory.Size(n, -1f, 28f);
            TextMeshProUGUI w = UITheme.Label(bg.transform, s.Summary(catalog), 15f, UITheme.Muted);
            InventoryUIFactory.Size(w, -1f, 20f);
            TextMeshProUGUI d = UITheme.Label(bg.transform, "Last played " + When(s.lastPlayedUtc), 13f, new Color(1f, 1f, 1f, 0.35f));
            InventoryUIFactory.Size(d, -1f, 18f);
            b.onClick.AddListener(() => Select(save));
            cards[s] = b;
        }
    }

    private static string When(long ticks)
    {
        if (ticks <= 0) return "never";
        DateTime t = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
        TimeSpan ago = DateTime.Now - t;
        if (ago.TotalMinutes < 2) return "just now";
        if (ago.TotalHours < 1) return $"{(int)ago.TotalMinutes} minutes ago";
        if (ago.TotalDays < 1) return $"{(int)ago.TotalHours} hours ago";
        return t.ToString("d MMM yyyy");
    }

    private void Select(CharacterSave save)
    {
        selected = save;
        deleteArmed = false;
        if (deleteLabel != null) deleteLabel.text = "Delete";
        foreach (KeyValuePair<CharacterSave, Button> kv in cards)
            UITheme.SetSelected(kv.Value, kv.Key == save);
        playButton.interactable = save != null;
        deleteButton.interactable = save != null;
        if (save == null)
        {
            detailText.text = StatText.Paint("No character selected.", 0);
            return;
        }
        CharacterPreviewStage s = Stage();
        CharacterBuild build = CharacterBuild.From(save, CharacterCatalog.Instance);
        if (s != null)
        {
            s.ShowRules(AppearanceRules.From(build.Archetypes));
            s.ShowAppearance(save.appearance);
            s.ShowHeight(build.ClampedHeight);
        }
        detailText.text = Describe(save, build);
        TextMeshProUGUI label = playButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = pendingMode == NetMode.Offline ? "Play Solo" : pendingMode == NetMode.Host ? "Host with " + save.characterName : "Join as " + save.characterName;
    }

    private static string Describe(CharacterSave save, CharacterBuild build)
    {
        var sb = new StringBuilder();
        sb.Append("<size=30><b>").Append(save.characterName).Append("</b></size>\n");
        var who = new List<string>();
        if (build.race != null) who.Add(build.race.Name);
        if (build.playerClass != null) who.Add(build.playerClass.GetClassName());
        if (save.appearance != null) who.Add(save.appearance.bodyType.ToString());
        if (build.ClampedHeight > 0f) who.Add($"{build.ClampedHeight:0.00} m");
        sb.Append(StatText.Paint(string.Join("  ·  ", who), 0)).Append('\n');
        if (build.playerClass != null && !string.IsNullOrEmpty(build.playerClass.classDescription))
            sb.Append('\n').Append(build.playerClass.classDescription).Append('\n');
        sb.Append("\n<color=").Append(StatText.HeaderColor).Append("><size=13><b>TRAITS</b></size></color>\n");
        List<Trait> all = build.AllTraits();
        if (all.Count == 0) sb.Append(StatText.Paint("None", 0)).Append('\n');
        foreach (Trait t in all)
        {
            if (t == null) continue;
            sb.Append("<b>").Append(t.Name).Append("</b>");
            if (!build.traits.Contains(t)) sb.Append(StatText.Paint("  (granted)", 0));
            sb.Append('\n');
            foreach (StatText.Line l in StatText.Of(t)) sb.Append("   • ").Append(l.Rich).Append('\n');
        }
        if (build.playerClass != null)
            sb.Append('\n').Append(StatText.Paint($"{Mathf.Max(0, build.RemainingPoints)} trait points left to spend in game", 0)).Append('\n');
        return sb.ToString().TrimEnd();
    }

    private void PlaySelected()
    {
        if (selected != null) Play(selected);
    }

    private void DeleteSelected()
    {
        if (selected == null) return;
        if (!deleteArmed)
        {
            deleteArmed = true;
            if (deleteLabel != null) deleteLabel.text = "Click again to delete";
            UITheme.Colors(deleteButton, UITheme.ButtonStyle.Danger);
            return;
        }
        CharacterSaveStore.Delete(selected);
        selected = null;
        UITheme.Colors(deleteButton, UITheme.ButtonStyle.Normal);
        ShowCharacters(false);
    }

    private void Play(CharacterSave save)
    {
        CharacterSaveStore.MarkPlayed(save);
        CharacterSession.Set(save);
        Debug.Log($"[Start] Playing '{save.characterName}': {ModeLabel(pendingMode)}.");
        if (PlayRequested == null)
        {
            SetStatus("Nothing starts the game: add a Net Bootstrap (or Offline Start Screens) to the scene.");
            ShowMain();
            return;
        }
        PlayRequested.Invoke(pendingMode);
    }

    // ------------------------------------------------------------------ main menu
    private void ChooseMode(NetMode mode)
    {
        pendingMode = mode;
        browsing = false;
        if (mode == NetMode.Server)
        {
            PlayRequested?.Invoke(mode); // a dedicated server plays no character
            return;
        }
        ShowCharacters(true);
    }

    private void Quit()
    {
        if (QuitRequested != null)
        {
            QuitRequested();
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>A line under the main menu (connection results, errors).</summary>
    public void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text ?? "";
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        bool back = (k != null && k.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (!back || KeyRebindingMenu.IsRebinding)
            return;
        if (settings != null && settings.HandleBack())
            return;
        if (creator != null && creator.GameObject.activeSelf)
        {
            if (saves.Count > 0) ShowCharacters(false);
            else ShowMain();
            return;
        }
        if (charactersPage.gameObject.activeSelf)
            ShowMain();
    }
}
