using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window of the crafting station the local player is working (built from code, one per machine).
/// <list type="bullet">
/// <item>Left: the materials in the inventory the station takes (and its free supplies) with Put; the pieces on the
/// station - pick one to work it, take it back. An assembly's fitted parts are listed under it, and can be picked by
/// the areas that finish a part in place (<see cref="CraftMode.AcceptsFitted"/>).</item>
/// <item>Middle: the work areas the station has (forge, anvil, quench and temper, grinding and finishing, sharpening,
/// ore bench, furnace, crucible, tannery, cutting table, assembly bench), each a work surface drawn from the real
/// state and worked with the hands - hold and release to strike, drag to grind, stroke the stone, scrape the hide,
/// tilt the crucible, keep the rhythm of the awl. Under the surface, two lines of what the station answered (also kept
/// in "What happened"), then how the area is worked, with the arm's tiredness at its end.</item>
/// <item>Right: what the selected piece looks, sounds and feels like (and what the station's instruments say), the
/// state of the fires, vats and tank, what just happened. A test station adds the numbers behind everything.</item>
/// </list>
/// Everything goes through the player's <see cref="CraftKnowledge"/>: metals are named only once known, heat is a
/// colour until a thermometer has taught what it means. It is an open panel of the player's inventory while shown.
/// <para>The forge's bellows, the anvil, the quench tank, the wheel, the whetstone, the crucible, the ore bench and the
/// carving bench can also be worked with the hands in 3D (<see cref="CraftHandsOn"/>, the area's "by hand" button or the
/// By Hand key, H by default): the window steps aside and comes back when the tool is put down.</para>
/// <para>Each visit (another station, or another player) starts with fresh work areas; closing and reopening the window
/// at the same station keeps them. The player's arm is theirs for the session: it is as tired as it was when the window
/// closed, and rested since. The window scales down to fit a smaller or wider screen; at 1920 x 1080 the work surface
/// is shown pixel for pixel.</para>
/// </summary>
public class CraftStationUI : MonoBehaviour, IExclusivePanel
{
    private static CraftStationUI instance;

    /// <summary>A player's arm, kept for the session: as tired as it was when the window closed, and rested since.</summary>
    private sealed class Arm
    {
        public CraftHands hands;
        public float putDownAt = -1f;
    }
    private static readonly Dictionary<CombatEntity, Arm> arms = new Dictionary<CombatEntity, Arm>();

    /// <summary>A row of the piece list, updated in place (a row is never pulled away under a click).</summary>
    private sealed class PieceRow
    {
        public string id, text;
        public int depth;
        public Button button;
        public TMP_Text label;
        public bool on, can = true;
    }

    /// <summary>A row of the material list, updated in place.</summary>
    private sealed class MaterialRow
    {
        public ItemSO item;
        public TextMeshProUGUI label;
        public Button all;
        public TMP_Text allLabel;
    }

    private CraftStation station, lastStation;
    private CombatEntity player, lastPlayer;
    private InventoryManager registeredWith;
    private bool managesCursor, hudFocus, journalOpen;
    private CursorLockMode previousLock;
    private bool previousVisible;
    private float refreshTimer, drawTimer;
    private string selected, tabSignature;
    private int journalTab;
    private bool changingSelection;
    private int quiet;

    private Canvas canvas;
    private RectTransform windowRect;
    private Vector2 fittedTo;
    private TextMeshProUGUI title, subtitle, hint, lookText, statusText, logText, journalText, message, assayText, guideText;
    private RectTransform materialList, pieceList, tabRow, craftRow, controls, journalPanel, testRow, guideRow, assayRow, armBar;
    private Image armFill;
    private Button takeButton, scrapButton, assayButton, turnButton, guideButton, clearButton;
    private CraftGuide.Step? guide;
    private int craftShown = -1;
    private CraftCanvasView view;
    private readonly List<CraftMode> modes = new List<CraftMode>();
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly List<Button> journalTabs = new List<Button>();
    private readonly List<(CraftPiece piece, int depth)> listed = new List<(CraftPiece, int)>();
    private readonly List<PieceRow> pieceRows = new List<PieceRow>();
    private readonly List<ItemSO> materialItems = new List<ItemSO>();
    private readonly List<MaterialRow> materialRows = new List<MaterialRow>();
    private bool listsBuilt;
    private CraftMode mode, reopen;
    private CraftHandsOn handsOn;
    private CraftKnowledge knowledge;
    private readonly List<string> log = new List<string>();
    private readonly CraftConfirm confirm = new CraftConfirm();
    /// <summary>A network client's requests still waiting for the station's answer, each with its callback.</summary>
    private readonly CraftAnswerWaits waits = new CraftAnswerWaits();
    /// <summary>Where the rules run: the settings the hand turned, only the latest of each, on their way (ten times a second).</summary>
    private readonly CraftSettingQueue settings = new CraftSettingQueue();
    private readonly List<CraftRequest> settingsDue = new List<CraftRequest>();
    /// <summary>The events the request being run here raised (offline and on the host), for its answer.</summary>
    private List<CraftEvent> capture;
    private IReadOnlyList<CraftEvent> answerEvents = NoEvents;
    private static readonly CraftEvent[] NoEvents = new CraftEvent[0];
    private float messageUntil;
    private int armLevel;

    private const int Order = 58;
    public const int SurfaceWidth = 720, SurfaceHeight = 400;
    /// <summary>The window at the reference resolution (1920 x 1080); it scales down to fit a smaller canvas.</summary>
    private static readonly Vector2 WindowSize = new Vector2(1620f, 960f);
    /// <summary>The dark frame round the work surface: the panel is the surface plus this on every side, so the drawing is shown pixel for pixel.</summary>
    private const float SurfaceInset = 4f;
    /// <summary>The most pieces "All" puts at once (each still a piece of its own); with more it says "+20".</summary>
    private const int PutAtOnce = 20;
    /// <summary>The arm's bar by its level (<see cref="CraftHands.Level"/>): fresh, tiring, heavy, spent - the steps its words change at.</summary>
    private static readonly Color[] ArmColours = { new Color(0.45f, 0.85f, 0.5f), new Color(1f, 0.8f, 0.35f), new Color(1f, 0.58f, 0.3f), new Color(1f, 0.4f, 0.3f) };
    private static readonly string[] JournalTabs = { "Mastery", "Metals", "Ores", "Heat", "Quenching", "Made", "Insights", "Experiments" };

    public string PanelName => station != null ? "Crafting: " + station.DisplayName : "Crafting";
    public CraftStation Station => station;
    public CombatEntity Player => player;
    /// <summary>The player's hands (their tremble; practice steadies them). One arm per player for the session.</summary>
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
        instance.Open(s, p);
    }

    private void Open(CraftStation s, CombatEntity p)
    {
        if (station != s || player != p)
        {
            if (station != null)
            {
                // Another station or player while the window is open: the work in hand is finished at the old one, still attached.
                PutDown();
                if (player != null && arms.TryGetValue(player, out Arm left))
                    left.putDownAt = Time.unscaledTime;
                Detach();
            }
            // Detach forgets the station and player; the visit is new when they differ from the last ones.
            bool newVisit = s != lastStation || p != lastPlayer;
            if (newVisit)
            {
                CreateModes(); // fresh areas: nothing the window holds moves to another station or player
                log.Clear();
                craftShown = -1;
            }
            station = lastStation = s;
            player = lastPlayer = p;
            s.BeginTending(p);
            s.WorkshopEvent += OnEvent;
            s.LocalMessage += Message;
            s.Logged += OnLogged; // what the hands were told at a 3D work place
            s.Answered += OnAnswered;
            s.Learnt += OnLearnt; // a network client: what the server's journal learnt
            knowledge = CraftKnowledge.For(p);
            if (knowledge != null) knowledge.Learnt += OnLearnt;
            Hands = ArmOf(p, s);
            armLevel = 0;
            tabSignature = null;
            listsBuilt = false;
            selected = null;
            // Back at the same station: the area left open is open again, as it was.
            if (!newVisit && reopen != null && reopen.Available(s)) SetMode(reopen);
            reopen = null;
        }
        canvas.gameObject.SetActive(true);
        FitWindow();
        Register();
        Refresh(true);
    }

    /// <summary>How steady the hands are from practice (items made, insights had).</summary>
    private static double Skill(CraftJournalData d) => CraftJournal.HandSkill(d);

    /// <summary>
    /// The player's arm, the same at every station for the session: still as tired as it was when the window closed, and
    /// rested since (as an arm rests: properly after a couple of seconds still) - closing the window does not rest it at once.
    /// </summary>
    private static CraftHands ArmOf(CombatEntity p, CraftStation s)
    {
        List<CombatEntity> gone = null;
        foreach (CombatEntity k in arms.Keys)
            if (k == null) (gone ??= new List<CombatEntity>()).Add(k); // players that are gone
        if (gone != null)
            foreach (CombatEntity k in gone) arms.Remove(k);
        if (!arms.TryGetValue(p, out Arm arm))
            arms[p] = arm = new Arm { hands = new CraftHands(p.GetHashCode()) };
        else if (arm.putDownAt >= 0f)
            arm.hands.Rest(Mathf.Max(0f, Time.unscaledTime - arm.putDownAt));
        arm.putDownAt = -1f;
        arm.hands.skill = CraftMath.Clamp01(Skill(CraftKnowledge.For(p)?.Data));
        arm.hands.fatigueRelief = s.Rules?.Settings?.mastery?.fatigueRelief ?? 0.4f;
        return arm.hands;
    }

    /// <summary>The work areas, new: made for each visit, so that nothing the window holds moves to another station or player.</summary>
    private void CreateModes()
    {
        mode = reopen = null;
        modes.Clear();
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
        modes.Add(new CraftFibreMode());
        modes.Add(new CraftTailorMode());
        modes.Add(new CraftLapidaryMode());
        modes.Add(new CraftJewellerMode());
        modes.Add(new CraftAssemblyMode());
        foreach (CraftMode m in modes) m.Attach(this);
        tabSignature = null;
        if (!guideKeysChecked)
        {
            guideKeysChecked = true; // the guide names each area by its type's name: say once if one was renamed alone
            CraftGuide.KeysMatch(modes.ConvertAll(m => m.GetType()).ToArray());
        }
    }

    private static bool guideKeysChecked;

    /// <summary>The work area of type <typeparamref name="T"/> of this visit (one area reading another's choice: the anvil the forge's depth; the wheel the fibre bench's bath).</summary>
    public T Area<T>() where T : CraftMode
    {
        foreach (CraftMode m in modes)
            if (m is T t) return t;
        return null;
    }

    /// <summary>Closes the window if it shows <paramref name="s"/> (the server ended the visit).</summary>
    public static void Close(CraftStation s)
    {
        if (instance != null && instance.station == s && s != null)
            instance.Hide();
    }

    public static bool IsOpen => instance != null && instance.station != null && (instance.canvas.gameObject.activeSelf || instance.HandsOnActive);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        arms.Clear();
    }

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
        FitWindow();
        SendSettings(false); // what the hand turned, ten times a second
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
            mode.TakeUpByHand(); // not while the hands are at work on the drawing: it says so
            return;
        }
        float dt = Time.unscaledDeltaTime;
        Hands.Rest(dt);
        mode?.Tick(dt);
        // The surface: 20 times a second, or every frame (up to 60 a second) while the area shows something moving.
        drawTimer += dt;
        float every = mode != null && mode.LiveDraw ? 1f / 60f : 1f / 20f;
        if (drawTimer >= every)
        {
            drawTimer = Mathf.Min(drawTimer - every, every);
            Draw();
        }
        ShowArm();
        refreshTimer += dt;
        if (refreshTimer >= 0.25f)
        {
            refreshTimer = 0f;
            SendSettings(true); // in the workshop before the areas read it back into their controls
            Refresh(false);
            if (hint != null) hint.text = mode != null ? mode.Hint : "";
        }
        if (message != null && message.text.Length > 0 && Time.unscaledTime > messageUntil)
            message.text = "";
        if (waits.Count > 0 && waits.Expire(Time.unscaledTimeAsDouble) > 0)
            Message(CraftAnswerWaits.NoAnswer, true);
    }

    /// <summary>
    /// The window scales down to fit a canvas smaller than it (a wide or a small screen; never up), again whenever the
    /// canvas changes size. The work surface maps the pointer through its rectangle, so the hands stay exact.
    /// </summary>
    private void FitWindow()
    {
        if (windowRect == null || canvas == null)
            return;
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        if (size == fittedTo || size.x <= 0f || size.y <= 0f)
            return;
        fittedTo = size;
        float k = Mathf.Clamp(Mathf.Min((size.x - 20f) / WindowSize.x, (size.y - 20f) / WindowSize.y), 0.25f, 1f);
        windowRect.localScale = new Vector3(k, k, 1f);
    }

    /// <summary>
    /// The arm at the end of the hint row: a bar that empties as it tires, coloured by how tired it is - green fresh, gold
    /// tiring, orange heavy, red and trembling spent (<see cref="CraftHands.Level"/>, the steps its words change at); the
    /// words, in the open area's terms (<see cref="CraftMode.ArmWords"/>: blows or the hand's other work), go into the
    /// message row when it crosses into a worse state (it has to rest a little before they come again).
    /// </summary>
    private void ShowArm()
    {
        if (armFill == null)
            return;
        float f = (float)Hands.fatigue;
        RectTransform fill = armFill.rectTransform;
        var reach = new Vector2(Mathf.Clamp01(1f - f), 1f);
        if (fill.anchorMax != reach) fill.anchorMax = reach;
        int now = CraftHands.Level(f);
        Color col = ArmColours[now];
        if (armFill.color != col) armFill.color = col;
        var shake = new Vector2(now >= 3 ? Mathf.Sin(Time.unscaledTime * 40f) * 1.5f : 0f, 0f);
        if (armBar.anchoredPosition != shake) armBar.anchoredPosition = shake;
        int level = now;
        if (level < armLevel && f > CraftHands.LevelFrom(armLevel) - 0.1f) level = armLevel;
        if (level > armLevel) Say($"<color=#ffb070>{mode?.ArmWords(level) ?? CraftHands.FeelingAt(level, false)}</color>", false, false);
        armLevel = level;
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
        if (station != null)
        {
            CraftMode open = mode;
            PutDown(true);
            reopen = open; // reopened at the same station, the window opens on it again
        }
        else
            SetMode(null);
        if (player != null && arms.TryGetValue(player, out Arm arm))
            arm.putDownAt = Time.unscaledTime; // the arm rests from now
        Detach();
        confirm.Clear();
        SetHudFocus(false);
        Unregister();
        if (canvas != null && canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// The hands leave the work (the window closes, or turns to another station or player) while the station is still
    /// attached: the selected piece is let go and the area closed, so what is pending is sent to the piece it was done
    /// on. As the window closes (<paramref name="closing"/>), hand work the area keeps for when it is opened here again is
    /// said to wait (<see cref="CraftMode.Kept"/>); otherwise - or turning to another station or player, where the areas
    /// start afresh - hand work that cannot be kept (<see cref="CraftMode.Busy"/>) is said to be left unfinished.
    /// </summary>
    private void PutDown(bool closing = false)
    {
        string busy = mode?.Busy, kept = closing ? mode?.Kept : null;
        ChangeSelection(null);
        SetMode(null);
        if (!string.IsNullOrEmpty(kept))
            Tell(kept);
        else if (!string.IsNullOrEmpty(busy))
            Tell($"You stopped {busy} before it was done.");
    }

    private void Detach()
    {
        // The callbacks of requests never answered run now, as unanswered - also when the station itself is gone (they
        // must not run in a later visit).
        waits.DropAll();
        if (station != null)
        {
            SendSettings(true); // the last value the hand turned reaches the station before the hands leave it
            station.WorkshopEvent -= OnEvent;
            station.LocalMessage -= Message;
            station.Logged -= OnLogged;
            station.Answered -= OnAnswered;
            station.Learnt -= OnLearnt;
            if (player != null) station.EndTending(player);
        }
        if (knowledge != null) knowledge.Learnt -= OnLearnt; // the player's journal: also when the station is gone
        knowledge = null;
        waits.DropAll(); // one a callback above sent meanwhile
        settings.Clear(); // turned for a station that is gone
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
        CreateModes();

        canvas = UITheme.Canvas("Craft Station", Order, transform);
        Transform c = canvas.transform;
        Image backdrop = UITheme.Panel("Backdrop", c, new Color(0f, 0f, 0f, 0.35f), 0);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        Image window = UITheme.Window("Window", c, WindowSize);
        windowRect = window.rectTransform;
        Transform w = window.transform;

        title = UITheme.Label(w, "", 28f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -56f), new Vector2(-380f, -14f));
        subtitle = UITheme.Label(w, "", 15f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Subtitle");
        UITheme.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -82f), new Vector2(-380f, -54f));
        TwoLines(subtitle, 15f); // a lapidary's whole kit and the practice still fit their row
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
        takeButton = UITheme.Button("Take", pieceButtons, "Take", Take, UITheme.ButtonStyle.Primary, 0f, 38f);
        turnButton = UITheme.Button("Turn", pieceButtons, "Turn over", () => Send(new CraftRequest(CraftRequestKind.Turn, selected)), UITheme.ButtonStyle.Normal, 0f, 38f);
        scrapButton = UITheme.Button("Scrap", pieceButtons, "Scrap", Scrap, UITheme.ButtonStyle.Danger, 0f, 38f);

        // Middle: the work areas. The column's inside is as wide as the surface panel (the 720 x 400 drawing and its
        // frame), so the drawing is shown pixel for pixel at 1920 x 1080.
        RectTransform mid = Column(w, "Work", 404f, 404f + SurfaceWidth + 2f * SurfaceInset + 24f, 6f);
        // A workshop with many work areas groups them by craft: the crafts above, the areas of the one chosen below.
        craftRow = UITheme.Row("Crafts", mid, 28f, 4f);
        craftRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        tabRow = UITheme.Row("Tabs", mid, 32f, 4f);
        tabRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        Image surfaceBack = UITheme.Panel("Surface", mid, new Color(0f, 0f, 0f, 0.5f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(surfaceBack, -1f, SurfaceHeight + 2f * SurfaceInset);
        var raw = new GameObject("Work Surface", typeof(RectTransform), typeof(RawImage));
        raw.transform.SetParent(surfaceBack.transform, false);
        InventoryUIFactory.Stretch((RectTransform)raw.transform, SurfaceInset, SurfaceInset);
        view = raw.AddComponent<CraftCanvasView>();
        view.Build(SurfaceWidth, SurfaceHeight);
        view.Pointer += OnPointer;
        // What the station answered (two lines, under the surface where the eyes are), then how the area is worked and the arm.
        message = UITheme.Label(mid, "", 14f, UITheme.Gold, TextAlignmentOptions.Center, FontStyles.Normal, "Message");
        InventoryUIFactory.Size(message, -1f, 30f);
        TwoLines(message, 14f);
        RectTransform hintRow = UITheme.Row("Hint", mid, 30f, 8f);
        hint = UITheme.Label(hintRow, "", 13f, UITheme.Muted);
        InventoryUIFactory.Size(hint, -1f, 30f, 1f);
        TwoLines(hint, 13f);
        BuildArm(hintRow);
        controls = UITheme.Column("Controls", mid, 0, 6f);
        InventoryUIFactory.Size(controls, -1f, 0f).flexibleHeight = 1f;

        // Right: what is seen.
        RectTransform right = Column(w, "Look", 404f + SurfaceWidth + 2f * SurfaceInset + 44f, 1596f);
        UITheme.Header(right, "What you see");
        RectTransform lookArea = UITheme.ScrollList("Look", right, 300f, out _);
        lookText = UITheme.Label(lookArea, "", 15f, UITheme.Text);
        // What comes next for the piece picked, and a button to go to that work.
        guideRow = UITheme.Row("Next", right, 34f, 8f);
        guideText = UITheme.Label(guideRow, "", 13f, UITheme.Gold);
        InventoryUIFactory.Size(guideText, -1f, 32f, 1f);
        TwoLines(guideText, 13f);
        guideButton = UITheme.Button("Go", guideRow, "Go", GoToGuide, UITheme.ButtonStyle.Primary, 64f, 30f);
        // The assay kit (where the station has one): the readout goes under what the piece looks like, a line an element.
        assayRow = UITheme.Row("Assay", right, 34f, 8f);
        assayButton = UITheme.Button("Assay", assayRow, "Assay", () => Send(new CraftRequest(CraftRequestKind.Assay, selected)), UITheme.ButtonStyle.Normal, 120f, 32f);
        assayText = UITheme.Label(assayRow, "", 12f, UITheme.Muted);
        InventoryUIFactory.Size(assayText, -1f, 32f, 1f);
        TwoLines(assayText, 12f);
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
        clearButton = UITheme.Button("Clear", testRow, "Clear all", ClearAll, UITheme.ButtonStyle.Danger, 0f, 30f);
        UITheme.Header(right, "What happened");
        RectTransform logArea = UITheme.ScrollList("Log", right, 0f, out _);
        logText = UITheme.Label(logArea, "", 13f, UITheme.Muted);

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

    private static RectTransform Column(Transform window, string name, float x0, float x1, float spacing = 8f)
    {
        Image bg = UITheme.Panel(name, window, UITheme.Card, UITheme.Radius);
        UITheme.Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(x0, 24f), new Vector2(x1, -96f));
        RectTransform col = UITheme.Column("Column", bg.transform, 12, spacing);
        InventoryUIFactory.Stretch(col);
        return col;
    }

    /// <summary>A text of up to two lines that shrinks (down to 10 pt) to stay in its box, and ends in "..." if even that is not enough.</summary>
    private static void TwoLines(TextMeshProUGUI t, float size)
    {
        t.textWrappingMode = TextWrappingModes.Normal;
        t.enableAutoSizing = true;
        t.fontSizeMin = 10f;
        t.fontSizeMax = size;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.maxVisibleLines = 2;
    }

    /// <summary>The arm at the end of the hint row: a small label and a bar that empties as it tires (see <see cref="ShowArm"/>).</summary>
    private void BuildArm(RectTransform row)
    {
        TextMeshProUGUI label = UITheme.Label(row, "Arm", 11f, UITheme.Muted, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(label, 28f, 30f, 0f);
        RectTransform slot = InventoryUIFactory.Rect("Arm", row);
        InventoryUIFactory.Size(slot, 90f, 30f, 0f);
        Image back = UITheme.Panel("Bar", slot, new Color(0f, 0f, 0f, 0.6f), 3);
        armBar = back.rectTransform;
        UITheme.Place(armBar, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
        armFill = UITheme.Panel("Fill", armBar, ArmColours[0], 3);
        UITheme.Place(armFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    // ------------------------------------------------------------------ modes
    private void SetMode(CraftMode m)
    {
        if (mode == m)
            return;
        // A fitted part stays picked only in an area that works fitted parts; another opens on its assembly.
        CraftPiece sel = SelectedPiece;
        if (m != null && sel != null && sel.location == CraftLocation.Assembled && !m.AcceptsFitted(sel))
            ChangeSelection(station?.Workshop?.Piece(sel.parent) != null ? sel.parent : null);
        if (controls != null)
            foreach (CraftSliderGrip g in controls.GetComponentsInChildren<CraftSliderGrip>(true))
                g.LetGo(); // a setting the hand moved is sent before its slider goes
        mode?.Leave();
        mode = m;
        if (controls != null)
            for (int i = controls.childCount - 1; i >= 0; i--)
                Destroy(controls.GetChild(i).gameObject);
        if (mode != null && controls != null)
            mode.Build(controls);
        Practise();
        if (hint != null) hint.text = mode != null ? mode.Hint : "";
        tabSignature = null;
        drawTimer = 1f; // the new area is drawn at once
    }

    private void OnPointer(CraftPointer phase, Vector2 p, bool rightButton, float scroll) => mode?.Pointer(phase, p, rightButton, scroll);

    private void Draw()
    {
        if (view == null || view.Pixels == null)
            return;
        CraftPixels c = view.Pixels;
        c.Clear(new Color(0.05f, 0.05f, 0.07f, 1f));
        if (mode != null && station?.Workshop != null)
            mode.Draw(c); // the area owns the whole surface (the arm is shown in the hint row)
        view.Present();
    }

    /// <summary>Where the pointer is on the work surface (pixels; (-1, -1) when it is not on it).</summary>
    public Vector2 Hover => view != null ? view.Hover : new Vector2(-1, -1);
    /// <summary>Is a gesture under way on the work surface (the left button held since it pressed there)?</summary>
    public bool PointerHeld => view != null && view.Held;
    /// <summary>
    /// The time of the pointer sample being handled (read it in <see cref="CraftMode.Pointer"/>): a fast stroke's
    /// filled-in samples each have their own, between the frames - speeds are measured with it, not with the frame's time.
    /// </summary>
    public float PointerTime => view != null ? view.SampleTime : Time.unscaledTime;

    // ------------------------------------------------------------------ actions
    /// <summary>
    /// Sends a request. Returns false when it was refused at once (offline or host refusal, or a check on this machine such
    /// as an item missing from the Item Database); true when it was done (offline, host) or sent (network client).
    /// <paramref name="answered"/> (ok, reason) is called exactly once with the real outcome: before Send returns offline
    /// and on the host; on a network client when the station's answer has come AND the copy of the workshop shows every
    /// operation the request made, so reading the workshop inside the callback shows the result (no answer within
    /// <see cref="CraftAnswerWaits.Timeout"/> seconds, or the window closing first: (false, "No answer from the
    /// station.") - the outcome is then unknown, not refused: the station may have done it, so work kept for a retry is
    /// kept only on a real refusal and dropped on this). Inside the callback <see cref="AnswerEvents"/> holds the events
    /// the request's own operations raised. A refusal's reason - or the note on work granted only in part - is shown in
    /// the message row (and dimmed in "What happened") by Send itself. A setting the hand turns
    /// (<see cref="CraftRequest.IsSetting"/>) is answered (true) at once: only its latest value goes, with the other
    /// settings turned, at most ten times a second (<see cref="CraftSettingQueue"/>) - also where the rules run, so a
    /// slider dragged never uses up the pace of requests the guard keeps for the work - and a refusal of it shows then.
    /// </summary>
    public bool Send(CraftRequest r, Action<bool, string> answered = null)
    {
        if (station == null || player == null)
        {
            answered?.Invoke(false, "");
            return false;
        }
        if (!station.RulesHere && station.Net != null)
            return SendToServer(r, answered);
        if (station.RulesHere && CraftRequest.IsSetting(r.kind))
        {
            settings.Put(r); // goes with the next settings (Update), no full refresh for it
            Answer(answered, true, "", null);
            return true;
        }
        // Offline and on the host: done here and now, the events it raised caught for its answer.
        List<CraftEvent> outer = capture, mine = new List<CraftEvent>();
        capture = mine;
        bool ok;
        string reason;
        try { ok = station.Request(player, r, out reason); }
        finally { capture = outer; }
        if (!string.IsNullOrEmpty(reason))
            Message(reason, true);
        Answer(answered, ok, reason ?? "", mine);
        if (quiet == 0)
            Refresh(true);
        return ok;
    }

    /// <summary>A network client: the request goes to the server with a number, and its answer is waited for.</summary>
    private bool SendToServer(CraftRequest r, Action<bool, string> answered)
    {
        bool setting = CraftRequest.IsSetting(r.kind);
        // every other request is numbered, so that its refusal's reason comes back even when nobody waits for the outcome
        r.seq = setting ? 0 : waits.Expect(answered == null ? null : (ok, why) => Answer(answered, ok, why, answerEvents), Time.unscaledTimeAsDouble);
        if (!station.Request(player, r, out string reason))
        {
            waits.Forget(r.seq);
            if (!string.IsNullOrEmpty(reason)) Message(reason, true);
            Answer(answered, false, reason ?? "", null);
            return false;
        }
        if (!string.IsNullOrEmpty(reason)) Message(reason, true); // cut short before it went (a stroke too long to carry)
        if (setting) Answer(answered, true, "", null); // only its latest value matters: nothing comes back for it
        if (quiet == 0)
            Refresh(true);
        return true;
    }

    /// <summary>
    /// Where the rules run: the settings the hand turned since they last went (only the latest value of each) go to the
    /// station - at most ten times a second, or at once with <paramref name="all"/> (the hands leave the work, the areas
    /// are about to read them back). A refusal says why.
    /// </summary>
    private void SendSettings(bool all)
    {
        if (station == null || player == null || settings.Count == 0 || !settings.Take(Time.unscaledTimeAsDouble, settingsDue, all))
            return;
        foreach (CraftRequest r in settingsDue)
            if (!station.Request(player, r, out string reason) && !string.IsNullOrEmpty(reason))
                Message(reason, true);
        settingsDue.Clear();
    }

    /// <summary>A network client: the station answered one of the requests sent from here (its copy shows what it did).</summary>
    private void OnAnswered(int seq, bool ok, string reason)
    {
        if (station == null || !waits.Waiting(seq))
            return; // not ours, or given up on
        if (!string.IsNullOrEmpty(reason))
            Message(reason, true);
        IReadOnlyList<CraftEvent> outer = answerEvents;
        answerEvents = station.AnswerEvents;
        try { waits.Answer(seq, ok, reason); }
        finally { answerEvents = outer; }
        refreshTimer = 1f; // the window shows it next frame (a burst of answers refreshes it once)
    }

    /// <summary>Runs an answer's callback with the events its request raised (an area's mistake there does not break the window).</summary>
    private void Answer(Action<bool, string> answered, bool ok, string reason, IReadOnlyList<CraftEvent> events)
    {
        if (answered == null)
            return;
        IReadOnlyList<CraftEvent> outer = answerEvents;
        answerEvents = events ?? NoEvents;
        try { answered(ok, reason); }
        catch (Exception e) { Debug.LogException(e); }
        finally { answerEvents = outer; }
    }

    /// <summary>
    /// Inside the <c>answered</c> callback of <see cref="Send"/>: the events the request's own operations raised (a blow's
    /// ring, a solder's flash) - on a network client the ones its copy of the workshop raised as it followed them.
    /// Empty anywhere else.
    /// </summary>
    public IReadOnlyList<CraftEvent> AnswerEvents => answerEvents;

    /// <summary>Runs <paramref name="send"/> without the full window refresh that follows each <see cref="Send"/> (a setting being dragged).</summary>
    public void WithoutRefresh(Action send)
    {
        quiet++;
        try { send?.Invoke(); }
        finally { quiet--; }
    }

    /// <summary>
    /// Irreversible actions ask first: true on the second click on the same <paramref name="key"/> within 2.5 s
    /// (<see cref="CraftConfirm"/>); the first click shows <paramref name="question"/> in the message row.
    /// </summary>
    public bool Confirm(string key, string question)
    {
        if (confirm.Ask(key, Time.unscaledTime))
        {
            if (message != null && message.text == question) message.text = ""; // answered
            return true;
        }
        Say(question, false, false, CraftConfirm.Window); // it stands as long as the question does
        return false;
    }

    /// <summary>
    /// Take: the area first finishes its work on the piece (what is pending lands on it before it goes into the bag); the
    /// piece is let go only once the station has given it. A piece the station would not give (too hot to carry, inside
    /// a working place - read from the workshop here, a client's copy too) or work of several presses still under way
    /// (<see cref="CraftMode.Busy"/>) leaves the area undisturbed: it says why and nothing is sent.
    /// </summary>
    private void Take()
    {
        string id = selected;
        if (id == null)
            return;
        if (!CraftWorkshop.CanTake(SelectedPiece, out string why))
        {
            Message(why, true);
            return;
        }
        string busy = mode?.Busy;
        if (!string.IsNullOrEmpty(busy))
        {
            Message($"Finish {busy} first.", true);
            return;
        }
        mode?.SelectionChanging(id, null);
        Send(new CraftRequest(CraftRequestKind.TakePiece, id), (ok, reason) =>
        {
            if (selected != id)
                return;
            if (ok) ChangeSelection(null, true);
            else mode?.SelectionChanged(id); // still in hand: the area takes it up again
        });
    }

    /// <summary>
    /// Scrap: throws the piece away at a test station; at any station gives up an item nothing has gone into yet (a design
    /// started by mistake). Asked twice.
    /// </summary>
    private void Scrap()
    {
        string id = selected;
        bool empty = CraftAssembly.IsEmpty(SelectedPiece);
        if (id == null || !Confirm("scrap:" + id, empty ? "Click Scrap again to give up this item: nothing has gone into it yet." : "Click Scrap again to throw this piece away."))
            return;
        Send(new CraftRequest(CraftRequestKind.ScrapPiece, id));
    }

    /// <summary>Clear all (a test station, where the rules run): empties the whole workshop, asked twice.</summary>
    private void ClearAll()
    {
        if (station == null || !station.RulesHere || !Confirm("clear", "Click Clear all again to empty the whole workshop."))
            return;
        ChangeSelection(null);
        station.ClearWorkshop();
        Refresh(true);
    }

    // ------------------------------------------------------------------ by hand, in 3D
    private bool HandsOnActive => handsOn != null && handsOn.Active;

    /// <summary>The window steps aside and the player works <paramref name="what"/> in 3D; it comes back when they put the tool down.</summary>
    public bool EnterHandsOn(CraftHandsOnMode what, CraftHandsOnSetup setup)
    {
        if (station == null || player == null || what == CraftHandsOnMode.None)
            return false;
        SendSettings(true); // the work place is built from the workshop as the hand left it
        handsOn = CraftHandsOn.For(station);
        canvas.gameObject.SetActive(false);
        SetHudFocus(true); // the hotbar, quick slots and ability bar stay hidden while the hands work
        if (handsOn.Begin(what, player, Hands, setup, OnHandsOnEnded, out string reason))
            return true;
        canvas.gameObject.SetActive(true);
        if (!string.IsNullOrEmpty(reason)) Message(reason, true);
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
            if (handsOn != null) mode?.TakeBackHandsOn(handsOn.Setup); // what the hands chose there comes back to the window's controls
            Refresh(true);
        }
        else
        {
            if (handsOn != null) mode?.TakeBackHandsOn(handsOn.Setup); // kept for when the window is opened here again
            Hide();
        }
    }

    /// <summary>The hands as practised as the player is in the craft of the area in front of them.</summary>
    private void Practise()
    {
        if (mode == null || station == null)
            return;
        Hands.skill = CraftMastery.Of(Known, mode.Discipline, station.Rules?.Settings?.mastery);
    }

    /// <summary>
    /// What the player learnt (a new rank, an insight): under the work surface and in the log - from their journal, or on a
    /// network client from the server's (through the station).
    /// </summary>
    private void OnLearnt(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        Message(text);
    }

    /// <summary>"Tailoring: Journeyman tailor, 46% · next Expert".</summary>
    private string PracticeText()
    {
        if (mode == null || station == null)
            return "";
        CraftMasterySettings s = station.Rules?.Settings?.mastery;
        CraftDiscipline d = mode.Discipline;
        double m = CraftMastery.Of(Known, d, s);
        CraftRank r = CraftMastery.RankOf(m, s);
        string next = r < CraftRank.Master ? $", {CraftMastery.RankProgress(m, s) * 100:0}% to {CraftMastery.RankName(r + 1).ToLowerInvariant()}" : "";
        return $"<color=#ffcc61>{CraftMastery.Name(d)}: {CraftMastery.Title(d, m, s)}</color>{next}";
    }

    /// <summary>
    /// A message for the player: at once in the two lines under the work surface, kept long enough to read it
    /// (<see cref="CraftWindowRules.MessageSeconds"/>) unless a newer one replaces it, and in "What happened".
    /// </summary>
    public void Message(string text) => Say(text, false, true);

    /// <summary>A message; a refusal (<paramref name="refused"/>) is dimmed in "What happened".</summary>
    public void Message(string text, bool refused) => Say(text, refused, true);

    private void Say(string text, bool refused, bool keep, float seconds = 0f)
    {
        if (message == null)
            return;
        message.text = text ?? "";
        messageUntil = Time.unscaledTime + (seconds > 0f ? seconds : CraftWindowRules.MessageSeconds(text));
        if (keep && !string.IsNullOrEmpty(text))
            Log(refused ? $"<color=#7d838b>{text}</color>" : text);
    }

    /// <summary>What the hands were told at a 3D work place: into "What happened" (a refusal dimmed), as the window's own messages go.</summary>
    private void OnLogged(string text, bool refused)
    {
        if (!string.IsNullOrEmpty(text))
            Log(refused ? $"<color=#7d838b>{text}</color>" : text);
    }

    /// <summary>A line in "What happened" (the same line twice in a row is kept once).</summary>
    private void Log(string text)
    {
        if (log.Count > 0 && log[log.Count - 1] == text)
            return;
        log.Add(text);
        if (log.Count > 40) log.RemoveAt(0);
    }

    /// <summary>A message that must reach the player even as the window closes: also in the inventory's message line.</summary>
    private void Tell(string text)
    {
        Message(text);
        if (player != null) InventoryManager.For(player)?.ShowMessage(text, InventoryFeedback.Kind.Info);
    }

    /// <summary>Picks a piece to work (the piece list).</summary>
    public void Select(string id)
    {
        ChangeSelection(id);
        Refresh(true);
    }

    /// <summary>
    /// Every change of the selected piece comes through here (picked, taken, the bench cleared, the window closed or
    /// turned to another station): the open area is told before, while the old piece is still selected - it finishes the
    /// hand work done on it and drops its gestures - and after, to take up the new one. <paramref name="told"/>: the area
    /// was already told it is changing (a Take).
    /// </summary>
    private void ChangeSelection(string id, bool told = false)
    {
        if (id == selected || changingSelection)
            return;
        changingSelection = true;
        try
        {
            if (!told) mode?.SelectionChanging(selected, id);
            selected = id;
            mode?.SelectionChanged(id);
        }
        finally
        {
            changingSelection = false;
        }
    }

    /// <summary>
    /// The selected piece is no longer on the station (melted, cut up, the workshop replaced): nothing is selected any
    /// more FIRST, then the area drops what belonged to it - a flush with nothing selected sends nothing.
    /// </summary>
    private void SelectionVanished()
    {
        if (changingSelection)
            return;
        changingSelection = true;
        try
        {
            string old = selected;
            selected = null;
            mode?.SelectionChanging(old, null);
            mode?.SelectionChanged(null);
        }
        finally
        {
            changingSelection = false;
        }
    }

    private void OnEvent(CraftEvent e)
    {
        capture?.Add(e); // raised by the request being run here: its answer's
        string text = e.Text;
        if (e.Kind == CraftEventKind.Struck)
            text = CraftLook.StrikeSound(e.Id);
        if (string.IsNullOrEmpty(text) || e.Kind == CraftEventKind.Refused)
            return; // a refusal comes as the request's answer (a message)
        // The workshop's bookkeeping is no news: a piece made carries where it came from (its description, not a
        // sentence), and the fuel shovelled shows on the fire - the areas word both in their answers.
        if (e.Kind == CraftEventKind.PieceCreated || e.Kind == CraftEventKind.PieceAdded || e.Kind == CraftEventKind.PieceRemoved
            || e.Kind == CraftEventKind.PieceMoved || e.Kind == CraftEventKind.FuelAdded)
            return;
        Log(text);
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
        subtitle.text = (station.IsTestStation ? "<color=#ffcc61>Test station</color> · every tool and instrument, free materials, the numbers shown" : InstrumentsText(station.Instruments))
                        + (mode != null ? " · " + PracticeText() : "");
        Practise();
        if (ws == null)
            return;
        if (selected != null && ws.Piece(selected) == null)
            SelectionVanished();

        RefreshTabs();
        RefreshMaterials();
        RefreshPieces();

        // The selected piece, and what the assay kit has told of it (one element a line, under what it looks like).
        CraftPiece p = SelectedPiece;
        bool kit = station.HasInstrument(CraftInstruments.Assay);
        bool canAssay = p != null && kit && (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Ore || p.kind == CraftPieceKind.Fasteners);
        bool assayed = canAssay && (station.IsTestStation || Assayed(rules, p, known));
        var sb = new StringBuilder();
        if (p != null)
        {
            sb.Append($"<b>{CraftLook.Name(rules, p, known)}</b>  <color=#9aa0a6>({LocationText(p)})</color>\n");
            foreach (string line in CraftLook.Describe(rules, p, known, station.Instruments, Dark, station.Reveals, ws))
                sb.Append(line).Append('\n');
            if (assayed)
            {
                sb.Append("<color=#9aa0a6>The assay:</color>\n");
                foreach (string line in station.AssayLines(selected, known))
                    sb.Append(line).Append('\n');
            }
            if (p.kind == CraftPieceKind.Assembly)
                sb.Append(AssemblyText(ws, p, known));
        }
        else
            sb.Append("<color=#9aa0a6>Pick a piece on the station to look at it and work it. Put materials from your inventory on the station first.</color>");
        lookText.text = sb.ToString();
        // What comes next: the row stays in its place (the column never jumps); with no step it says so, dimmed, and has no Go.
        guide = CraftGuide.Next(station, p, known);
        if (guide.HasValue)
        {
            CraftGuide.Step g = guide.Value;
            CraftMode target = ModeKeyed(g.area);
            bool here = GuideHere(g);
            string where = target == null ? "" : $" <color=#9aa0a6>({target.Title}{(g.place.Length > 0 ? " → " + target.PlaceTitle(g.place) : "")}{(here ? "" : ": not at this station")})</color>";
            guideText.text = $"Next: {g.text}{where}";
            guideButton.gameObject.SetActive(here);
        }
        else
        {
            guideText.text = p != null ? "<color=#9aa0a6>Next: nothing in particular.</color>"
                : "<color=#9aa0a6>Next: pick a piece to see what comes next for it.</color>";
            guideButton.gameObject.SetActive(false);
        }
        // The assay kit's row, only where there is one (for the whole visit): its button for a metal, an ore or fasteners.
        if (assayRow.gameObject.activeSelf != kit) assayRow.gameObject.SetActive(kit);
        assayButton.gameObject.SetActive(canAssay);
        assayText.text = !kit ? "" : !canAssay ? "Pick a metal, an ore or fasteners to assay it."
            : assayed ? "Assayed: what it is made of is under what it looks like." : "What it is made of, element by element.";
        // Take stays clickable for a piece inside a working place: the station says why it must come out first.
        takeButton.interactable = p != null && p.location != CraftLocation.Assembled && p.location != CraftLocation.Melt;
        turnButton.interactable = p != null && p.kind == CraftPieceKind.Metal && p.location != CraftLocation.Assembled;
        // A test station throws anything away; elsewhere only an item nothing has gone into yet is given up.
        scrapButton.gameObject.SetActive(station.IsTestStation || CraftAssembly.IsEmpty(p));
        scrapButton.interactable = p != null && p.location != CraftLocation.Assembled;
        testRow.gameObject.SetActive(station.IsTestStation);
        clearButton.gameObject.SetActive(station.RulesHere); // a network client's copy cannot be cleared

        // The workshop.
        statusText.text = StatusText(ws, rules, known);
        var lb = new StringBuilder();
        for (int i = log.Count - 1; i >= 0 && i >= log.Count - 14; i--)
            lb.Append(log[i]).Append('\n');
        logText.text = lb.ToString();
        mode?.Refresh();
        mode?.RefreshPractice();

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

    /// <summary>The work area with this stable key - the type name of its <see cref="CraftMode"/> (the guide names them so) - or null.</summary>
    private CraftMode ModeKeyed(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        foreach (CraftMode m in modes)
            if (m.GetType().Name == key) return m;
        return null;
    }

    /// <summary>Can the guide's step be gone to here: its area is at this station, and so is its place (when it names one)?</summary>
    private bool GuideHere(CraftGuide.Step g)
    {
        CraftMode m = ModeKeyed(g.area);
        return m != null && station != null && m.Available(station) && (g.place.Length == 0 || m.HasPlace(g.place));
    }

    /// <summary>Go: to the work area and place the guide names (and the part it names, a blade in its hilt).</summary>
    private void GoToGuide()
    {
        if (!guide.HasValue || !GuideHere(guide.Value))
            return;
        CraftGuide.Step g = guide.Value;
        CraftMode m = ModeKeyed(g.area);
        SetMode(m);
        craftShown = -1;
        if (g.place.Length > 0) m.GoTo(g.place);
        CraftPiece part = !string.IsNullOrEmpty(g.piece) ? station.Workshop?.Piece(g.piece) : null;
        if (part != null && (part.location != CraftLocation.Assembled || m.AcceptsFitted(part)))
            ChangeSelection(part.id);
        Refresh(true);
    }

    /// <summary>The craft a work area is grouped under on the tabs (the assembly bench is its own).</summary>
    private static int CraftOf(CraftMode m) => m is CraftAssemblyMode ? 99 : m is CraftGrindMode ? (int)CraftDiscipline.Smithing : (int)m.Discipline;

    private void RefreshTabs()
    {
        var sb = new StringBuilder();
        foreach (CraftMode m in modes) sb.Append(m.Available(station) ? '1' : '0');
        sb.Append(mode != null ? modes.IndexOf(mode) : -1).Append(':').Append(craftShown);
        string sig = sb.ToString();
        if (sig == tabSignature)
            return;
        tabSignature = sig;
        for (int i = tabRow.childCount - 1; i >= 0; i--) Destroy(tabRow.GetChild(i).gameObject);
        for (int i = craftRow.childCount - 1; i >= 0; i--) Destroy(craftRow.GetChild(i).gameObject);
        tabButtons.Clear();
        // Many work areas (a complete workshop): a row of crafts, then the areas of the craft in hand.
        var crafts = new List<int>();
        int available = 0;
        foreach (CraftMode m in modes)
            if (m.Available(station))
            {
                available++;
                if (!crafts.Contains(CraftOf(m))) crafts.Add(CraftOf(m));
            }
        bool grouped = available > 8 && crafts.Count > 1;
        craftRow.gameObject.SetActive(grouped);
        if (grouped)
        {
            if (mode != null && craftShown < 0) craftShown = CraftOf(mode);
            if (!crafts.Contains(craftShown)) craftShown = crafts[0];
            foreach (int c in crafts)
            {
                int captured = c;
                string name = c == 99 ? "Assemble" : CraftMastery.Name((CraftDiscipline)c);
                Button b = UITheme.Button(name, craftRow, name, () =>
                {
                    craftShown = captured;
                    // the first area of that craft
                    foreach (CraftMode m in modes)
                        if (m.Available(station) && CraftOf(m) == captured) { SetMode(m); break; }
                    tabSignature = null;
                    Refresh(true);
                }, UITheme.ButtonStyle.Tab, 0f, 26f);
                TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
                if (t != null) { t.enableAutoSizing = true; t.fontSizeMin = 9f; t.fontSizeMax = 13f; }
                UITheme.SetSelected(b, c == craftShown);
            }
        }
        CraftMode first = null;
        foreach (CraftMode m in modes)
        {
            if (!m.Available(station)) continue;
            if (grouped && CraftOf(m) != craftShown) continue;
            first ??= m;
            CraftMode captured = m;
            Button b = UITheme.Button(m.Title, tabRow, m.Title, () => { SetMode(captured); Refresh(true); }, UITheme.ButtonStyle.Tab, 0f, 30f);
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

    /// <summary>
    /// The materials the station takes, each a row kept and updated in place (its count, Put, and All - up to twenty at
    /// once, "+20" when there are more); the rows are rebuilt only when the items listed change.
    /// </summary>
    private void RefreshMaterials()
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
        bool same = listsBuilt && items.Count == materialItems.Count;
        for (int i = 0; same && i < items.Count; i++) same = items[i] == materialItems[i];
        if (!same)
            BuildMaterialRows(items);
        foreach (MaterialRow row in materialRows)
        {
            bool free = station.IsFreeSupply(row.item);
            int have = counts.TryGetValue(row.item, out int c) ? c : 0;
            string count = free ? "free" : have > 0 ? $"×{have}" : "";
            string text = $"{row.item.Name} <color=#9aa0a6>{count}</color>";
            if (row.label.text != text) row.label.text = text;
            bool many = !free && have > 1;
            if (row.all.gameObject.activeSelf != many) row.all.gameObject.SetActive(many);
            string all = have > PutAtOnce ? $"+{PutAtOnce}" : "All";
            if (row.allLabel != null && row.allLabel.text != all) row.allLabel.text = all;
        }
    }

    private void BuildMaterialRows(List<ItemSO> items)
    {
        materialItems.Clear();
        materialItems.AddRange(items);
        materialRows.Clear();
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
            TextMeshProUGUI label = UITheme.Label(row, item.Name, 14f, UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 38f, 1f);
            ItemSO captured = item;
            Button put = UITheme.Button("Put", row, "Put", () => Send(new CraftRequest(CraftRequestKind.PutItem) { item = captured, count = 1 }), UITheme.ButtonStyle.Normal, 60f, 34f);
            TMP_Text t = put.GetComponentInChildren<TMP_Text>(true);
            if (t != null) t.fontSize = 14f;
            // all of them at once, up to twenty (each still a piece of its own): the count is read when it is clicked
            Button putAll = UITheme.Button("All", row, "All", () => PutAll(captured), UITheme.ButtonStyle.Ghost, 48f, 34f);
            TMP_Text ta = putAll.GetComponentInChildren<TMP_Text>(true);
            if (ta != null) ta.fontSize = 13f;
            putAll.gameObject.SetActive(false);
            materialRows.Add(new MaterialRow { item = item, label = label, all = putAll, allLabel = ta });
        }
    }

    /// <summary>Puts all of an item on the station - up to twenty at once.</summary>
    private void PutAll(ItemSO item)
    {
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        int have = inv != null ? inv.GetItemCount(item) : 0;
        if (have > 0)
            Send(new CraftRequest(CraftRequestKind.PutItem) { item = item, count = Mathf.Min(have, PutAtOnce) });
    }

    /// <summary>
    /// The pieces on the station, each a row kept and updated in place (its name, heat, place, whether it is picked), an
    /// assembly's fitted parts indented under it - pickable while the open area works fitted parts. The rows are rebuilt
    /// only when the pieces listed (or their order) change, so a row is never pulled away under a click.
    /// </summary>
    private void RefreshPieces()
    {
        station.Workshop.ListPieces(listed);
        CraftJournalData known = Known;
        bool same = listsBuilt && listed.Count == pieceRows.Count;
        for (int i = 0; same && i < listed.Count; i++) same = pieceRows[i].id == listed[i].piece.id && pieceRows[i].depth == listed[i].depth;
        if (!same)
            BuildPieceRows();
        listsBuilt = true;
        for (int i = 0; i < listed.Count; i++)
        {
            (CraftPiece p, int depth) = listed[i];
            PieceRow row = pieceRows[i];
            Color glow = CraftMetallurgy.HeatColor(p.MaxTemperature, Dark);
            string swatch = glow.a > 0.05f ? $"<color=#{ColorUtility.ToHtmlStringRGB(glow)}>●</color> " : "";
            string text = $"{swatch}{CraftLook.Name(station.Rules, p, known)}  <color=#9aa0a6>{LocationText(p)}</color>";
            if (row.text != text)
            {
                row.text = text;
                if (row.label != null) row.label.text = text;
            }
            bool can = depth == 0 || mode != null && mode.AcceptsFitted(p);
            if (row.can != can)
            {
                row.can = can;
                row.button.interactable = can;
            }
            bool on = p.id == selected;
            if (row.on != on)
            {
                row.on = on;
                UITheme.SetSelected(row.button, on);
            }
        }
    }

    private void BuildPieceRows()
    {
        pieceRows.Clear();
        for (int i = pieceList.childCount - 1; i >= 0; i--) Destroy(pieceList.GetChild(i).gameObject);
        if (listed.Count == 0)
        {
            TextMeshProUGUI none = UITheme.Label(pieceList, "Nothing on the station yet.", 13f, UITheme.Muted);
            InventoryUIFactory.Size(none, -1f, 40f);
            return;
        }
        foreach ((CraftPiece p, int depth) in listed)
        {
            string id = p.id;
            Button b = UITheme.Button(id, pieceList, "", () => Select(id), UITheme.ButtonStyle.ListItem, 0f, depth > 0 ? 30f : 38f);
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                t.alignment = TextAlignmentOptions.Left;
                t.enableAutoSizing = true;
                t.fontSizeMin = 10f;
                t.fontSizeMax = depth > 0 ? 12f : 14f;
                if (depth > 0) t.rectTransform.offsetMin = new Vector2(16f + 20f * depth, t.rectTransform.offsetMin.y); // a fitted part, under its assembly
            }
            pieceRows.Add(new PieceRow { id = id, depth = depth, button = b, label = t });
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
            case CraftLocation.Steam: return "in the steam box";
            case CraftLocation.Stack: return "seasoning in the stack";
            default: return p.kind == CraftPieceKind.Assembly ? (p.assembly.finished ? "finished" : "on the bench") : "on the bench";
        }
    }

    /// <summary>Every instrument the station has, each named as the station names it (<see cref="CraftStation.InstrumentName"/>).</summary>
    private static string InstrumentsText(CraftInstruments i)
    {
        var parts = new List<string>();
        foreach (CraftInstruments one in System.Enum.GetValues(typeof(CraftInstruments)))
        {
            int bits = (int)one;
            if (bits == 0 || (bits & (bits - 1)) != 0) continue; // none, and the kits of several (All, GemKit)
            if ((i & one) != 0) parts.Add(CraftStation.InstrumentName(one));
        }
        return parts.Count > 0 ? "Instruments: " + string.Join(", ", parts) : "No instruments: only your eyes, ears and hands.";
    }

    private string StatusText(CraftWorkshop ws, CraftRules rules, CraftJournalData known)
    {
        bool thermo = station.HasInstrument(CraftInstruments.Thermometer);
        var sb = new StringBuilder();
        if (station.Has(CraftTools.Forge)) sb.Append(CraftLook.Fire(ws.Forge, thermo, false)).Append('\n');
        if (station.Has(CraftTools.Furnace)) sb.Append(CraftLook.Furnace(rules, ws.Furnace, thermo, CraftSmelting.FinesInCharge(ws))).Append('\n');
        if (station.Has(CraftTools.Crucible)) sb.Append(CraftLook.Crucible(rules, ws.Crucible, known, thermo, ws)).Append('\n');
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
            sb.Append("<color=#9aa0a6>Finished. Try it to see what it is worth - or take it, and try it later from your bag at a bench where such things are made.</color>\n");
            return sb.ToString();
        }
        CraftItemReport r = CraftEvaluator.Item(rules, ws, asm);
        // named as the player knows its metals and stones; every note on it, worded
        sb.Append($"<color=#ffcc61>{CraftEvaluator.KnownName(rules, r, known)}: {r.Tier} work</color>\n");
        List<string> parts = CraftEvaluator.KnownParts(rules, r, known);
        if (parts.Count > 0) sb.Append($"<color=#9aa0a6>{string.Join("; ", parts)}</color>\n");
        foreach (string line in CraftEvaluator.InspectionLines(r)) sb.Append(line).Append('\n');
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
        switch (tab - 1)
        {
            case -1:
            {
                CraftMasterySettings ms = rules.Settings?.mastery;
                sb.Append("<b>Your practice, craft by craft</b> <color=#9aa0a6>(every finished piece teaches its craft - better work more - and so, slowly, does the work itself)</color>\n");
                foreach (CraftDiscipline c in CraftMastery.All)
                {
                    if (c == CraftDiscipline.Alchemy) continue; // the alchemy station keeps its own journal
                    CraftMasteryNote n = CraftMastery.Note(d.mastery, c);
                    double m = CraftMastery.Level(n?.xp ?? 0, ms);
                    CraftRank r = CraftMastery.RankOf(m, ms);
                    sb.Append($"\n<color=#ffcc61>{CraftMastery.Name(c)}</color>: {CraftMastery.Title(c, m, ms)} · {m * 100:0}%");
                    if (r < CraftRank.Master) sb.Append($" <color=#9aa0a6>({CraftMastery.RankProgress(m, ms) * 100:0}% of the way to {CraftMastery.RankName(r + 1).ToLowerInvariant()})</color>");
                    if (n != null && n.finished > 0) sb.Append($" · {n.finished} finished");
                    sb.Append('\n');
                    foreach (string line in CraftMastery.Benefits(c, m, ms, rules.Settings?.timing)) sb.Append($"   {line}\n");
                }
                break;
            }
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
