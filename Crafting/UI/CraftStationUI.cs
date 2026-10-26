using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window of the crafting station the local player is working (built from code, one per machine).
/// <list type="bullet">
/// <item>Left: the materials in the inventory the station takes (and its free supplies) with Put; the pieces on the
/// station - pick one to work it, take it back.</item>
/// <item>Middle: the work areas the station has (forge, anvil, quench and temper, grinding and finishing, sharpening,
/// ore bench, furnace, crucible, tannery, cutting table, assembly bench), each a work surface drawn from the real
/// state and worked with the hands - hold and release to strike, drag to grind, stroke the stone, scrape the hide,
/// tilt the crucible, keep the rhythm of the awl.</item>
/// <item>Right: what the selected piece looks, sounds and feels like (and what the station's instruments say), the
/// state of the fires, vats and tank, what just happened. A test station adds the numbers behind everything.</item>
/// </list>
/// Everything goes through the player's <see cref="CraftKnowledge"/>: metals are named only once known, heat is a
/// colour until a thermometer has taught what it means. It is an open panel of the player's inventory while shown.
/// <para>The forge's bellows, the anvil, the quench tank, the wheel, the whetstone and the crucible can also be worked
/// with the hands in 3D (<see cref="CraftHandsOn"/>, the area's "by hand" button or the By Hand key, H by default): the window steps aside and
/// comes back when the tool is put down.</para>
/// </summary>
public class CraftStationUI : MonoBehaviour, IExclusivePanel
{
    private static CraftStationUI instance;

    private CraftStation station;
    private CombatEntity player;
    private InventoryManager registeredWith;
    private bool managesCursor, hudFocus, journalOpen;
    private CursorLockMode previousLock;
    private bool previousVisible;
    private float refreshTimer, drawTimer;
    private string selected, materialSignature, pieceSignature, tabSignature;
    private int journalTab;

    private Canvas canvas;
    private TextMeshProUGUI title, subtitle, hint, lookText, statusText, logText, journalText, message, assayText;
    private RectTransform materialList, pieceList, tabRow, controls, journalPanel, testRow;
    private Button takeButton, scrapButton, assayButton, turnButton;
    private CraftCanvasView view;
    private readonly List<CraftMode> modes = new List<CraftMode>();
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<Button> journalTabs = new List<Button>();
    private CraftMode mode;
    private CraftHandsOn handsOn;
    private readonly List<string> log = new List<string>();
    private float messageTime;

    private const int Order = 58;
    public const int SurfaceWidth = 720, SurfaceHeight = 400;
    private static readonly string[] JournalTabs = { "Metals", "Ores", "Heat", "Quenching", "Made", "Insights", "Experiments" };

    public string PanelName => station != null ? "Crafting: " + station.DisplayName : "Crafting";
    public CraftStation Station => station;
    public CombatEntity Player => player;
    /// <summary>The player's hands (their tremble; practice steadies them).</summary>
    public CraftHands Hands { get; private set; } = new CraftHands(1);
    public string SelectedId => selected;
    public CraftPiece SelectedPiece => station?.Workshop?.Piece(selected);
    public CraftJournalData Known => player != null ? CraftKnowledge.For(player)?.Data : null;
    /// <summary>Is it dark here (heat colours show earlier)?</summary>
    public bool Dark => station != null && station.Workshop != null && station.Workshop.Variable("night") > 0.5;

    // ------------------------------------------------------------------ opening
    public static void Show(CraftStation s, CombatEntity p)
    {
        if (s == null || p == null)
            return;
        if (instance == null)
        {
            InventoryEventSystems.EnsureSingle();
            var go = new GameObject("Craft Station UI");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CraftStationUI>();
            instance.Build();
        }
        if (instance.station != s || instance.player != p)
        {
            instance.Detach();
            instance.station = s;
            instance.player = p;
            s.BeginTending(p);
            s.WorkshopEvent += instance.OnEvent;
            s.LocalMessage += instance.Message;
            instance.Hands = new CraftHands(s.GetHashCode() ^ p.GetHashCode(), Skill(CraftKnowledge.For(p)?.Data));
            instance.materialSignature = instance.pieceSignature = instance.tabSignature = null;
            instance.log.Clear();
            instance.selected = null;
            instance.SetMode(null);
        }
        instance.canvas.gameObject.SetActive(true);
        instance.Register();
        instance.Refresh(true);
    }

    /// <summary>How steady the hands are from practice (items made, insights had).</summary>
    private static double Skill(CraftJournalData d) => CraftJournal.HandSkill(d);

    /// <summary>Closes the window if it shows <paramref name="s"/> (the server ended the visit).</summary>
    public static void Close(CraftStation s)
    {
        if (instance != null && instance.station == s && s != null)
            instance.Hide();
    }

    public static bool IsOpen => instance != null && instance.station != null && (instance.canvas.gameObject.activeSelf || instance.HandsOnActive);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private void Update()
    {
        if (station == null || player == null || !station.isActiveAndEnabled || !player.isActiveAndEnabled || station.Workshop == null)
        {
            Hide(); // the station is gone, or (a network client) the server's workshop is no longer followed
            return;
        }
        Vector3 a = station.InteractionPoint.position, b = player.transform.position;
        a.y = b.y = 0f;
        if (Vector3.Distance(a, b) > station.InteractionRange * 1.6f + 0.5f)
        {
            Hide();
            return;
        }
        if (HandsOnActive)
            return; // the hammer, the bellows, the wheel...: the 3D work place has the mouse and the view
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        bool inventoryOpen = InventoryManager.IsInventoryOpenFor(player);
        SetHudFocus(!inventoryOpen);
        if (!inventoryOpen && !journalOpen && mode != null && mode.HandsOnMode != CraftHandsOnMode.None && StationControls.Pressed(StationControl.ByHand))
        {
            mode.TakeUpByHand();
            return;
        }
        Hands.Rest(Time.unscaledDeltaTime);
        mode?.Tick(Time.unscaledDeltaTime);
        drawTimer += Time.unscaledDeltaTime;
        if (drawTimer >= 1f / 20f)
        {
            drawTimer = 0f;
            Draw();
        }
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= 0.25f)
        {
            refreshTimer = 0f;
            Refresh(false);
            string arm = Hands.Feeling;
            if (hint != null && mode != null) hint.text = arm.Length > 0 ? $"<color=#ffb070>{arm}</color>" : mode.Hint;
        }
        if (message != null && message.text.Length > 0 && Time.unscaledTime - messageTime > 6f)
            message.text = "";
    }

    private void SetHudFocus(bool on)
    {
        if (on == hudFocus)
            return;
        hudFocus = on;
        if (on) HudFocus.Take();
        else HudFocus.Release();
    }

    private void Hide()
    {
        if (HandsOnActive)
        {
            CraftHandsOn h = handsOn;
            handsOn = null;
            h.End(false, false);
        }
        handsOn = null;
        SetMode(null);
        Detach();
        SetHudFocus(false);
        Unregister();
        if (canvas != null && canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(false);
    }

    private void Detach()
    {
        if (station != null)
        {
            station.WorkshopEvent -= OnEvent;
            station.LocalMessage -= Message;
            if (player != null) station.EndTending(player);
        }
        station = null;
        player = null;
        journalOpen = false;
    }

    private void OnDestroy()
    {
        Hide();
        if (instance == this)
            instance = null;
    }

    private void Register()
    {
        InputBindingStore.BlockGameplayInput(this); // the station's keys only: no jumping, dashing or casting from the bench
        if (registeredWith != null || managesCursor)
            return;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv != null)
        {
            registeredWith = inv;
            inv.RegisterPanel(this);
        }
        else
        {
            managesCursor = true;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
        }
    }

    private void Unregister()
    {
        InputBindingStore.UnblockGameplayInput(this);
        if (registeredWith != null)
        {
            InventoryManager inv = registeredWith;
            registeredWith = null;
            inv.UnregisterPanel(this);
        }
        if (managesCursor)
        {
            managesCursor = false;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }

    public void ClosePanel()
    {
        if (HandsOnActive)
        {
            handsOn.End(false); // Escape at the anvil: back to the game (the work stays as it is)
            return;
        }
        if (journalOpen)
        {
            journalOpen = false;
            Refresh(true);
            return;
        }
        Hide();
    }

    // ------------------------------------------------------------------ building
    private void Build()
    {
        modes.Add(new CraftForgeMode());
        modes.Add(new CraftAnvilMode());
        modes.Add(new CraftQuenchMode());
        modes.Add(new CraftGrindMode());
        modes.Add(new CraftSharpenMode());
        modes.Add(new CraftCarveMode());
        modes.Add(new CraftWoodshopMode());
        modes.Add(new CraftOreMode());
        modes.Add(new CraftFurnaceMode());
        modes.Add(new CraftCrucibleMode());
        modes.Add(new CraftTanneryMode());
        modes.Add(new CraftCuttingMode());
        modes.Add(new CraftTailorMode());
        modes.Add(new CraftAssemblyMode());
        foreach (CraftMode m in modes) m.Attach(this);

        canvas = UITheme.Canvas("Craft Station", Order, transform);
        Transform c = canvas.transform;
        Image backdrop = UITheme.Panel("Backdrop", c, new Color(0f, 0f, 0f, 0.35f), 0);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        Image window = UITheme.Window("Window", c, new Vector2(1620f, 960f));
        Transform w = window.transform;

        title = UITheme.Label(w, "", 28f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -56f), new Vector2(-380f, -14f));
        subtitle = UITheme.Label(w, "", 15f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Subtitle");
        UITheme.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -82f), new Vector2(-380f, -54f));
        RectTransform headerButtons = UITheme.Area("Header Buttons", w, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-360f, -64f), new Vector2(-24f, -18f));
        InventoryUIFactory.Horizontal(headerButtons.gameObject, 10f).childForceExpandWidth = true;
        UITheme.Button("Journal", headerButtons, "Journal", () => { journalOpen = !journalOpen; Refresh(true); }, UITheme.ButtonStyle.Primary, 0f, 42f);
        UITheme.Button("Close", headerButtons, "Close", Hide, UITheme.ButtonStyle.Normal, 0f, 42f);

        // Left: materials and pieces.
        RectTransform left = Column(w, "Materials", 24f, 384f);
        UITheme.Header(left, "Your materials");
        materialList = UITheme.ScrollList("Material List", left, 300f, out _);
        UITheme.Header(left, "On the station");
        pieceList = UITheme.ScrollList("Piece List", left, 0f, out _);
        RectTransform pieceButtons = UITheme.Row("Piece Buttons", left, 38f, 8f);
        pieceButtons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        takeButton = UITheme.Button("Take", pieceButtons, "Take", () => Send(new CraftRequest(CraftRequestKind.TakePiece, selected)), UITheme.ButtonStyle.Primary, 0f, 38f);
        turnButton = UITheme.Button("Turn", pieceButtons, "Turn over", () => Send(new CraftRequest(CraftRequestKind.Turn, selected)), UITheme.ButtonStyle.Normal, 0f, 38f);
        scrapButton = UITheme.Button("Scrap", pieceButtons, "Scrap", () => Send(new CraftRequest(CraftRequestKind.ScrapPiece, selected)), UITheme.ButtonStyle.Danger, 0f, 38f);

        // Middle: the work areas.
        RectTransform mid = Column(w, "Work", 404f, 1144f);
        tabRow = UITheme.Row("Tabs", mid, 36f, 4f);
        tabRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        Image surfaceBack = UITheme.Panel("Surface", mid, new Color(0f, 0f, 0f, 0.5f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(surfaceBack, -1f, 400f);
        var raw = new GameObject("Work Surface", typeof(RectTransform), typeof(RawImage));
        raw.transform.SetParent(surfaceBack.transform, false);
        InventoryUIFactory.Stretch((RectTransform)raw.transform, 4f, 4f);
        view = raw.AddComponent<CraftCanvasView>();
        view.Build(SurfaceWidth, SurfaceHeight);
        view.Pointer += OnPointer;
        hint = UITheme.Label(mid, "", 13f, UITheme.Muted);
        InventoryUIFactory.Size(hint, -1f, 22f);
        controls = UITheme.Column("Controls", mid, 0, 6f);
        InventoryUIFactory.Size(controls, -1f, 300f).flexibleHeight = 1f;

        // Right: what is seen.
        RectTransform right = Column(w, "Look", 1164f, 1596f);
        UITheme.Header(right, "What you see");
        RectTransform lookArea = UITheme.ScrollList("Look", right, 300f, out _);
        lookText = UITheme.Label(lookArea, "", 15f, UITheme.Text);
        RectTransform assayRow = UITheme.Row("Assay", right, 34f, 8f);
        assayButton = UITheme.Button("Assay", assayRow, "Assay", () => Send(new CraftRequest(CraftRequestKind.Assay, selected)), UITheme.ButtonStyle.Normal, 120f, 32f);
        assayText = UITheme.Label(assayRow, "", 12f, UITheme.Muted);
        InventoryUIFactory.Size(assayText, -1f, 32f, 1f);
        UITheme.Header(right, "The workshop");
        RectTransform statusArea = UITheme.ScrollList("Status", right, 170f, out _);
        statusText = UITheme.Label(statusArea, "", 13f, UITheme.Muted);
        testRow = UITheme.Row("Test", right, 32f, 6f);
        testRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        foreach ((string label, float seconds) in new[] { ("+10 s", 10f), ("+1 min", 60f), ("+5 min", 300f), ("+30 min", 1800f) })
        {
            float s = seconds;
            UITheme.Button(label, testRow, label, () => Send(new CraftRequest(CraftRequestKind.Wait) { a = s }), UITheme.ButtonStyle.Ghost, 0f, 30f);
        }
        UITheme.Button("Clear", testRow, "Clear all", () => { station.ClearWorkshop(); selected = null; Refresh(true); }, UITheme.ButtonStyle.Danger, 0f, 30f);
        UITheme.Header(right, "What happened");
        RectTransform logArea = UITheme.ScrollList("Log", right, 0f, out _);
        logText = UITheme.Label(logArea, "", 13f, UITheme.Muted);
        message = UITheme.Label(w, "", 15f, UITheme.Gold, TextAlignmentOptions.Center);
        UITheme.Place(message.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(404f, 2f), new Vector2(-476f, 20f));

        // The journal, over the whole window.
        Image jp = UITheme.Panel("Journal", w, UITheme.Surface, UITheme.RadiusLarge);
        journalPanel = jp.rectTransform;
        UITheme.Place(journalPanel, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -96f));
        RectTransform jcol = UITheme.Column("Column", jp.transform, 16, 10f);
        InventoryUIFactory.Stretch(jcol);
        RectTransform jtabs = UITheme.Row("Tabs", jcol, 40f, 8f);
        jtabs.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        for (int i = 0; i < JournalTabs.Length; i++)
        {
            int index = i;
            journalTabs.Add(UITheme.Button(JournalTabs[i], jtabs, JournalTabs[i], () => { journalTab = index; Refresh(true); }, UITheme.ButtonStyle.Tab, 0f, 40f));
        }
        RectTransform jlist = UITheme.ScrollList("Pages", jcol, 0f, out _);
        journalText = UITheme.Label(jlist, "", 15f, UITheme.Text);
        journalPanel.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
    }

    private static RectTransform Column(Transform window, string name, float x0, float x1)
    {
        Image bg = UITheme.Panel(name, window, UITheme.Card, UITheme.Radius);
        UITheme.Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(x0, 24f), new Vector2(x1, -96f));
        RectTransform col = UITheme.Column("Column", bg.transform, 12, 8f);
        InventoryUIFactory.Stretch(col);
        return col;
    }

    // ------------------------------------------------------------------ modes
    private void SetMode(CraftMode m)
    {
        if (mode == m)
            return;
        mode?.Leave();
        mode = m;
        if (controls != null)
            for (int i = controls.childCount - 1; i >= 0; i--)
                Destroy(controls.GetChild(i).gameObject);
        if (mode != null && controls != null)
            mode.Build(controls);
        if (hint != null) hint.text = mode != null ? mode.Hint : "";
        tabSignature = null;
    }

    private void OnPointer(CraftPointer phase, Vector2 p, bool rightButton, float scroll) => mode?.Pointer(phase, p, rightButton, scroll);

    private void Draw()
    {
        if (view == null || view.Pixels == null)
            return;
        CraftPixels c = view.Pixels;
        c.Clear(new Color(0.05f, 0.05f, 0.07f, 1f));
        if (mode != null && station?.Workshop != null)
        {
            mode.Draw(c);
            DrawArm(c);
        }
        view.Present();
    }

    /// <summary>The arm, top right: a bar that empties as it tires (green, gold, red), and trembles when it is spent.</summary>
    private void DrawArm(CraftPixels c)
    {
        float f = (float)Hands.fatigue, x = c.Width - 96f, y = c.Height - 14f;
        if (f > 0.85f) x += Mathf.Sin(Time.unscaledTime * 40f) * 1.5f;
        Color col = f < 0.5f ? new Color(0.45f, 0.85f, 0.5f) : f < 0.8f ? new Color(1f, 0.8f, 0.35f) : new Color(1f, 0.4f, 0.3f);
        // A little fist, then the bar.
        c.Disc(x - 8f, y + 3f, 5f, new Color(0.86f, 0.66f, 0.52f, 0.9f));
        c.Rect(x, y, x + 86f, y + 7f, new Color(0f, 0f, 0f, 0.6f));
        c.Rect(x + 1f, y + 1f, x + 1f + 84f * Mathf.Clamp01(1f - f), y + 6f, col);
    }

    /// <summary>Where the pointer is on the work surface (pixels; (-1, -1) when it is not on it).</summary>
    public Vector2 Hover => view != null ? view.Hover : new Vector2(-1, -1);
    public bool PointerHeld => view != null && view.Held;

    // ------------------------------------------------------------------ actions
    /// <summary>Sends a request to the station; the reason it was refused shows in the window.</summary>
    public bool Send(CraftRequest r)
    {
        if (station == null || player == null)
            return false;
        bool ok = station.Request(player, r, out string reason);
        if (!ok && !string.IsNullOrEmpty(reason))
            Message(reason);
        if (ok && r.kind == CraftRequestKind.TakePiece && r.piece == selected)
            selected = null;
        Refresh(true);
        return ok;
    }

    // ------------------------------------------------------------------ by hand, in 3D
    private bool HandsOnActive => handsOn != null && handsOn.Active;

    /// <summary>The window steps aside and the player works <paramref name="what"/> in 3D; it comes back when they put the tool down.</summary>
    public bool EnterHandsOn(CraftHandsOnMode what, CraftHandsOnSetup setup)
    {
        if (station == null || player == null || what == CraftHandsOnMode.None)
            return false;
        handsOn = CraftHandsOn.For(station);
        canvas.gameObject.SetActive(false);
        SetHudFocus(true); // the hotbar, quick slots and ability bar stay hidden while the hands work
        if (handsOn.Begin(what, player, Hands, setup, OnHandsOnEnded, out string reason))
            return true;
        canvas.gameObject.SetActive(true);
        if (!string.IsNullOrEmpty(reason)) Message(reason);
        Refresh(true);
        return false;
    }

    private void OnHandsOnEnded(bool backToWindow)
    {
        if (station == null)
            return;
        if (backToWindow)
        {
            canvas.gameObject.SetActive(true);
            Refresh(true);
        }
        else
            Hide();
    }

    /// <summary>A line under the work surface for a few seconds.</summary>
    public void Message(string text)
    {
        if (message == null)
            return;
        message.text = text ?? "";
        messageTime = Time.unscaledTime;
    }

    public void Select(string id)
    {
        selected = id;
        Refresh(true);
    }

    private void OnEvent(CraftEvent e)
    {
        string text = e.Text;
        if (e.Kind == CraftEventKind.Struck)
            text = CraftLook.StrikeSound(e.Id);
        if (string.IsNullOrEmpty(text) || e.Kind == CraftEventKind.Refused)
            return;
        if (log.Count > 0 && log[log.Count - 1] == text)
            return;
        log.Add(text);
        if (log.Count > 40) log.RemoveAt(0);
    }

    // ------------------------------------------------------------------ refreshing
    private void Refresh(bool force)
    {
        if (station == null || player == null)
            return;
        CraftWorkshop ws = station.Workshop;
        CraftRules rules = station.Rules;
        CraftJournalData known = Known ?? new CraftJournalData();
        title.text = station.DisplayName;
        subtitle.text = (station.IsTestStation ? "<color=#ffcc61>Test station</color> · every tool and instrument, free materials, the numbers shown · " : "") + InstrumentsText(station.Instruments);
        if (ws == null)
            return;
        if (selected != null && ws.Piece(selected) == null)
            selected = null;

        RefreshTabs();
        RefreshMaterials(force);
        RefreshPieces(force);

        // The selected piece.
        CraftPiece p = SelectedPiece;
        var sb = new StringBuilder();
        if (p != null)
        {
            sb.Append($"<b>{CraftLook.Name(rules, p, known)}</b>  <color=#9aa0a6>({LocationText(p)})</color>\n");
            foreach (string line in CraftLook.Describe(rules, p, known, station.Instruments, Dark, station.Reveals))
                sb.Append(line).Append('\n');
            if (p.kind == CraftPieceKind.Assembly)
                sb.Append(AssemblyText(ws, p, known));
        }
        else
            sb.Append("<color=#9aa0a6>Pick a piece on the station to look at it and work it. Put materials from your inventory on the station first.</color>");
        lookText.text = sb.ToString();
        bool canAssay = p != null && station.HasInstrument(CraftInstruments.Assay) && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Ore || p.kind == CraftPieceKind.Fasteners);
        assayButton.gameObject.SetActive(canAssay);
        assayText.text = canAssay ? string.Join(" · ", station.AssayLines(selected, known)) : "";
        if (canAssay && !station.IsTestStation && !Assayed(rules, p, known)) assayText.text = "";
        takeButton.interactable = p != null && p.location != CraftLocation.Assembled && p.location != CraftLocation.Melt;
        turnButton.interactable = p != null && p.kind == CraftPieceKind.Metal;
        scrapButton.gameObject.SetActive(station.IsTestStation);
        testRow.gameObject.SetActive(station.IsTestStation);

        // The workshop.
        statusText.text = StatusText(ws, rules, known);
        var lb = new StringBuilder();
        for (int i = log.Count - 1; i >= 0 && i >= log.Count - 14; i--)
            lb.Append(log[i]).Append('\n');
        logText.text = lb.ToString();
        mode?.Refresh();

        journalPanel.gameObject.SetActive(journalOpen);
        if (journalOpen)
        {
            journalPanel.SetAsLastSibling();
            for (int i = 0; i < journalTabs.Count; i++) UITheme.SetSelected(journalTabs[i], i == journalTab);
            journalText.text = JournalText(rules, known, journalTab);
        }
    }

    private static bool Assayed(CraftRules rules, CraftPiece p, CraftJournalData known)
    {
        if (p.kind == CraftPieceKind.Ore) return CraftJournal.KnowsOre(known, p.ore.ore);
        CraftClassNote n = CraftJournal.ClassNote(known, p.Profile(rules).Class?.id);
        return n != null && n.assayed;
    }

    private void RefreshTabs()
    {
        var sb = new StringBuilder();
        foreach (CraftMode m in modes) sb.Append(m.Available(station) ? '1' : '0');
        sb.Append(mode != null ? modes.IndexOf(mode) : -1);
        string sig = sb.ToString();
        if (sig == tabSignature)
            return;
        tabSignature = sig;
        for (int i = tabRow.childCount - 1; i >= 0; i--) Destroy(tabRow.GetChild(i).gameObject);
        tabButtons.Clear();
        CraftMode first = null;
        foreach (CraftMode m in modes)
        {
            if (!m.Available(station)) continue;
            first ??= m;
            CraftMode captured = m;
            Button b = UITheme.Button(m.Title, tabRow, m.Title, () => { SetMode(captured); Refresh(true); }, UITheme.ButtonStyle.Tab, 0f, 34f);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) { t.enableAutoSizing = true; t.fontSizeMin = 9f; t.fontSizeMax = 14f; }
            UITheme.SetSelected(b, m == mode);
            tabButtons.Add(b);
        }
        if ((mode == null || !mode.Available(station)) && first != null)
        {
            SetMode(first);
            RefreshTabs();
        }
    }

    private void RefreshMaterials(bool force)
    {
        InventoryManager inv = InventoryManager.For(player);
        var items = new List<ItemSO>();
        foreach (ItemSO s in station.FreeSupplies())
            if (station.Accepts(s)) items.Add(s);
        var counts = new Dictionary<ItemSO, int>();
        if (inv != null)
            foreach (InventoryItem it in inv.GetBagItems())
            {
                ItemSO so = it.itemScriptableObject;
                if (so == null || !station.Accepts(so)) continue;
                if (!items.Contains(so)) items.Add(so);
                counts[so] = inv.GetItemCount(so);
            }
        var sig = new StringBuilder();
        foreach (ItemSO i in items) sig.Append(i.GetHashCode()).Append(':').Append(counts.TryGetValue(i, out int n) ? n : -1).Append(';');
        if (sig.ToString() == materialSignature)
            return;
        materialSignature = sig.ToString();
        for (int i = materialList.childCount - 1; i >= 0; i--) Destroy(materialList.GetChild(i).gameObject);
        if (items.Count == 0)
        {
            TextMeshProUGUI none = UITheme.Label(materialList, "Nothing in your inventory is worked here.", 13f, UITheme.Muted);
            InventoryUIFactory.Size(none, -1f, 40f);
            return;
        }
        foreach (ItemSO item in items)
        {
            RectTransform row = UITheme.Row(item.Name, materialList, 40f, 6f);
            var icon = UITheme.Panel("Icon", row, new Color(1f, 1f, 1f, 0.06f), UITheme.RadiusSmall);
            InventoryUIFactory.Size(icon, 36f, 36f, 0f);
            if (item.Icon != null) { icon.sprite = item.Icon; icon.color = Color.white; }
            bool free = station.IsFreeSupply(item);
            string count = free ? "free" : counts.TryGetValue(item, out int c) ? $"×{c}" : "";
            TextMeshProUGUI label = UITheme.Label(row, $"{item.Name} <color=#9aa0a6>{count}</color>", 14f, UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 38f, 1f);
            ItemSO captured = item;
            Button put = UITheme.Button("Put", row, "Put", () => Send(new CraftRequest(CraftRequestKind.PutItem) { item = captured, count = 1 }), UITheme.ButtonStyle.Normal, 60f, 34f);
            TMP_Text t = put.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.fontSize = 14f;
        }
    }

    private void RefreshPieces(bool force)
    {
        CraftWorkshop ws = station.Workshop;
        CraftJournalData known = Known;
        var sig = new StringBuilder(selected ?? "-");
        foreach (CraftPiece p in ws.Pieces)
        {
            if (p.location == CraftLocation.Assembled) continue;
            sig.Append(p.id).Append(p.location).Append((int)(p.MaxTemperature / 50)).Append(p.defects.Count).Append(p.assembly.finished).Append(p.intentRole).Append(';');
        }
        if (sig.ToString() == pieceSignature)
            return;
        pieceSignature = sig.ToString();
        for (int i = pieceList.childCount - 1; i >= 0; i--) Destroy(pieceList.GetChild(i).gameObject);
        int shown = 0;
        foreach (CraftPiece p in ws.Pieces)
        {
            if (p.location == CraftLocation.Assembled) continue;
            shown++;
            string id = p.id;
            Color glow = CraftMetallurgy.HeatColor(p.MaxTemperature);
            string swatch = glow.a > 0.05f ? $"<color=#{ColorUtility.ToHtmlStringRGB(glow)}>●</color> " : "";
            Button b = UITheme.Button(id, pieceList, $"{swatch}{CraftLook.Name(station.Rules, p, known)}  <color=#9aa0a6>{LocationText(p)}</color>", () => Select(id),
                UITheme.ButtonStyle.ListItem, 0f, 38f);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null) { t.alignment = TextAlignmentOptions.Left; t.enableAutoSizing = true; t.fontSizeMin = 10f; t.fontSizeMax = 14f; }
            UITheme.SetSelected(b, id == selected);
        }
        if (shown == 0)
        {
            TextMeshProUGUI none = UITheme.Label(pieceList, "Nothing on the station yet.", 13f, UITheme.Muted);
            InventoryUIFactory.Size(none, -1f, 40f);
        }
    }

    public static string LocationText(CraftPiece p)
    {
        switch (p.location)
        {
            case CraftLocation.Fire: return "in the fire";
            case CraftLocation.Furnace: return "in the furnace";
            case CraftLocation.Melt: return "in the crucible";
            case CraftLocation.Quench: return "in the tank";
            case CraftLocation.Oven: return "in the oven";
            case CraftLocation.Ashes: return "in the ashes";
            case CraftLocation.Vat: return $"in vat {p.vat + 1}";
            case CraftLocation.Frame: return "in the frame";
            case CraftLocation.Rack: return "drying";
            case CraftLocation.Assembled: return "fitted";
            default: return p.kind == CraftPieceKind.Assembly ? (p.assembly.finished ? "finished" : "on the bench") : "on the bench";
        }
    }

    private static string InstrumentsText(CraftInstruments i)
    {
        var parts = new List<string>();
        if ((i & CraftInstruments.Thermometer) != 0) parts.Add("pyrometer");
        if ((i & CraftInstruments.Calipers) != 0) parts.Add("calipers");
        if ((i & CraftInstruments.Scale) != 0) parts.Add("balance");
        if ((i & CraftInstruments.Loupe) != 0) parts.Add("loupe");
        if ((i & CraftInstruments.Magnet) != 0) parts.Add("magnet");
        if ((i & CraftInstruments.TestFile) != 0) parts.Add("test file");
        if ((i & CraftInstruments.Assay) != 0) parts.Add("assay kit");
        if ((i & CraftInstruments.StreakPlate) != 0) parts.Add("streak plate");
        return parts.Count > 0 ? "Instruments: " + string.Join(", ", parts) : "No instruments: only your eyes, ears and hands.";
    }

    private string StatusText(CraftWorkshop ws, CraftRules rules, CraftJournalData known)
    {
        bool thermo = station.HasInstrument(CraftInstruments.Thermometer);
        var sb = new StringBuilder();
        if (station.Has(CraftTools.Forge)) sb.Append(CraftLook.Fire(ws.Forge, thermo, false)).Append('\n');
        if (station.Has(CraftTools.Furnace)) sb.Append(CraftLook.Furnace(rules, ws.Furnace, thermo)).Append('\n');
        if (station.Has(CraftTools.Crucible)) sb.Append(CraftLook.Crucible(rules, ws.Crucible, known, thermo)).Append('\n');
        if (station.Has(CraftTools.QuenchTank)) sb.Append(CraftLook.Quench(rules, ws.Quench, thermo)).Append('\n');
        if (station.Has(CraftTools.Oven)) sb.Append(ws.Oven.setpoint > ws.Ambient ? $"The oven is lit{(thermo ? $" ({CraftText.Celsius(ws.Oven.temperature)})" : "")}.\n" : "The oven is cold.\n");
        if (station.Has(CraftTools.Vats))
            for (int i = 0; i < ws.Vats.Count; i++)
                sb.Append($"Vat {i + 1}: {CraftLook.Vat(rules, ws.Vats[i], thermo, station.Reveals)}\n");
        if (station.Reveals)
            sb.Append($"<color=#9aa0a6>Forge {ws.Forge.temperature:0} °C (fuel {ws.Forge.fuel:0.0}, air {ws.Forge.air:0.00}, level {ws.Forge.level:0.00}); " +
                      $"furnace {ws.Furnace.fire.temperature:0} °C, slag {ws.Furnace.slag:0} g, bloom {ws.Furnace.BloomMass:0} g, pool {ws.Furnace.PoolMass:0} g; " +
                      $"crucible {ws.Crucible.temperature:0} °C, {ws.Crucible.Mass:0} g; tank {ws.Quench.temperature:0} °C; time {CraftText.Clock(ws.Elapsed)}</color>");
        return sb.ToString();
    }

    private string AssemblyText(CraftWorkshop ws, CraftPiece asm, CraftJournalData known)
    {
        CraftRules rules = station.Rules;
        CraftDesign d = rules.Design(asm.assembly.design);
        if (d == null)
            return "";
        var sb = new StringBuilder();
        if (!asm.assembly.finished)
        {
            List<string> missing = CraftAssembly.Missing(ws, asm);
            sb.Append(missing.Count > 0 ? "<color=#ffb070>Still to do:</color> " + string.Join(" ", missing) + "\n" : "<color=#7fd18c>Ready to finish.</color>\n");
            return sb.ToString();
        }
        if (!station.IsInspected(asm.id))
        {
            sb.Append("<color=#9aa0a6>Finished. Try it (Inspect) to see what it is worth.</color>\n");
            return sb.ToString();
        }
        CraftItemReport r = CraftEvaluator.Item(rules, ws, asm);
        sb.Append($"<color=#ffcc61>{r.Name}: {r.Tier} work</color>\n");
        foreach (string line in CraftItems.InspectionLines(r)) sb.Append(line).Append('\n');
        if (d.IsWeaponLike)
            sb.Append($"Damage {r.DamageMin:0.#}-{r.DamageMax:0.#}, crit {r.CritChance * 100:0}% ×{r.CritMultiplier:0.##}, speed ×{r.AttackSpeed:0.00}" +
                      (r.ToolDamage > 0 ? $", tool {r.ToolDamage:0.#}" : "") + $", durability {r.Durability}, {r.Weight:0.##} kg\n");
        else
            sb.Append($"Defence {r.Defense:0.#}, magic defence {r.MagicDefense:0.#}, durability {r.Durability}, {r.Weight:0.##} kg\n");
        if (station.Reveals)
            sb.Append($"<color=#9aa0a6>quality {r.Quality:0.000}: material {r.Material:0.00}, workmanship {r.Workmanship:0.00}, integrity {r.Integrity:0.00}, edge {r.Edge:0.00}, " +
                      $"heat {r.HeatTreatment:0.00}, assembly {r.Assembly:0.00}, protection {r.Protection:0.00}, balance {r.Balance:0} mm</color>\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ the journal
    private static string JournalText(CraftRules rules, CraftJournalData d, int tab)
    {
        var sb = new StringBuilder();
        switch (tab)
        {
            case 0:
                sb.Append("<b>Metals you know</b>\n");
                foreach (CraftClassNote n in d.classes)
                {
                    CraftMaterialClass c = rules.Class(n.classId);
                    if (c == null) continue;
                    sb.Append(n.identified ? $"<color=#ffcc61>{c.Name}</color>" : $"<color=#9aa0a6>{c.observation}</color>");
                    sb.Append($" · worked {n.timesWorked}×{(n.assayed ? " · assayed" : "")}\n");
                    if (n.identified && !string.IsNullOrEmpty(c.description)) sb.Append($"   {c.description}\n");
                }
                if (d.elements.Count > 0)
                {
                    sb.Append("\n<b>Elements found by assay</b>\n");
                    foreach (string e in d.elements) sb.Append($"{rules.ElementName(e)}: {rules.Element(e)?.description}\n");
                }
                break;
            case 1:
                sb.Append("<b>Ores</b>\n");
                foreach (CraftOreNote n in d.ores)
                {
                    CraftOreDefinition o = rules.Ore(n.oreId);
                    if (o == null) continue;
                    sb.Append(n.identified ? $"<color=#ffcc61>{o.Name}</color> - {o.description}\n" : $"<color=#9aa0a6>{o.observation}</color>\n");
                    sb.Append($"   handled {n.timesHandled}×, smelted {n.timesSmelted}×\n");
                    foreach (string dep in n.deposits) sb.Append($"   {dep}\n");
                }
                break;
            case 2:
                sb.Append("<b>Heat colours</b> (learnt by reading a pyrometer while they show)\n");
                foreach (CraftHeatNote n in d.heats)
                    sb.Append(n.readings >= rules.Settings.readingsToLearnHeat ? $"{n.color}: about {n.min:0}-{n.max:0} °C\n" : $"{n.color}: read {n.readings}×\n");
                break;
            case 3:
                sb.Append("<b>Quenches</b>\n");
                foreach (CraftQuenchNote n in d.quenches)
                {
                    CraftMaterialClass c = rules.Class(n.classId);
                    string metal = c != null && CraftJournal.KnowsClass(d, rules, c) ? c.Name : c?.observation ?? n.classId;
                    sb.Append($"{CraftText.Capital(metal)} in {rules.Quenchant(n.medium)?.Name.ToLowerInvariant() ?? n.medium}: {n.times}× - hardened {n.hardened}, stayed soft {n.soft}, cracked {n.cracked}\n");
                }
                break;
            case 4:
                sb.Append("<b>What you have made</b>\n");
                foreach (CraftDesignNote n in d.designs)
                {
                    sb.Append($"<color=#ffcc61>{n.name}</color> ×{n.timesMade} · best: {n.bestName} ({n.bestTier})\n");
                    foreach (string step in n.bestProcedure) sb.Append($"   {step}\n");
                }
                break;
            case 5:
                sb.Append("<b>What you have understood</b>\n");
                foreach (string id in d.insights)
                    if (CraftJournal.Insights.TryGetValue(id, out string text)) sb.Append($"• {text}\n");
                if (d.insights.Count == 0) sb.Append("<color=#9aa0a6>Nothing yet: mistakes teach the most.</color>\n");
                break;
            default:
                sb.Append("<b>Recent work</b>\n");
                for (int i = d.experiments.Count - 1; i >= 0; i--)
                {
                    CraftExperiment e = d.experiments[i];
                    sb.Append($"<color=#ffcc61>{e.name}</color> ({e.tier}, {e.quality * 100:0}%)\n");
                    foreach (string n in e.notes) sb.Append($"   {n}\n");
                }
                break;
        }
        return sb.ToString();
    }
}
