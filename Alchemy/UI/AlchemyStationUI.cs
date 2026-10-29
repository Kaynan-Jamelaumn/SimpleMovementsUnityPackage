using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window of the alchemy station the local player is using (built from code, one per machine). Everything it shows
/// goes through the player's <see cref="AlchemyKnowledge"/>: unidentified substances are described by what they look
/// like, reactions by what is seen until they are understood, and the "if bottled now" verdict names only outcomes the
/// player has made before.
/// <para>Only a test bench (<see cref="AlchemyStation.RevealsResults"/>) shows amounts, reactions, the verdict and its
/// quality while brewing. Anywhere else the window says what can be seen, smelt and measured (<see cref="AlchemyLook"/>):
/// colour, clarity, layers, what floats or has settled, bubbles, steam, smoke, streaks - and the result and its quality
/// are known once it is bottled. The spoon, the bellows and the filter are worked with the hands at the vessel
/// (<see cref="AlchemyHandsOn"/>): the window steps aside and comes back when they are put down.</para>
/// <list type="bullet">
/// <item>Left: the alchemy ingredients in the inventory (and the station's free supplies, its water) with +1 / +5.</item>
/// <item>Middle: temperature and its trend, time, fill, pressure; the fire slider; Stir and Pause; the contents and
/// what is happening.</item>
/// <item>Right: what the liquid would be if bottled now, Bottle / Bottle One / Collect Distillate / Empty, and the
/// observation log. The Journal button opens the player's discoveries, reactions, substances, ingredients and experiments.</item>
/// </list>
/// It is an open panel of the player's inventory while shown (free cursor, clicks never attack); Escape closes it.
/// </summary>
public class AlchemyStationUI : MonoBehaviour, IExclusivePanel
{
    private static AlchemyStationUI instance;

    private AlchemyStation station;
    private CombatEntity player;
    private InventoryManager registeredWith;
    private bool managesCursor, hudFocus;
    private CursorLockMode previousLock;
    private bool previousVisible;
    private float refreshTimer, heatSentAt, pendingHeat = float.NaN, damperSentAt, pendingDamper = float.NaN, stirSentAt, sentStir, traceTimer;
    private double lastTemperature;
    private float trend;
    private bool settingSlider, confirmEmpty, journalOpen;
    private int journalTab;
    private string ingredientSignature, journalSignature, fuelSignature, filterSignature;

    // Fire, stirring and processing.
    private RectTransform regulatedRow, manualPanel, fuelRow, bathRow, traceRow;
    private TextMeshProUGUI contentsHeader, happeningHeader, verdictHeader;
    private readonly AlchemyLook look = new AlchemyLook();
    private readonly Queue<Color> colorHistory = new Queue<Color>();
    private AlchemyHandsOn handsOn;
    private TextMeshProUGUI heatLabel, fireText, traceText, stirText, processText, fireOffLabel;
    private Image fireFill;
    private Slider damper;
    private Button bellows, fireOff, filterButton, decant, skim, clean, spoon3D;
    private TMP_Dropdown filterDropdown;
    private AlchemyCirclePad stirPad;
    private AlchemyTrace trace;
    private readonly List<Button> bathButtons = new List<Button>();
    private readonly List<AlchemyFilter> shownFilters = new List<AlchemyFilter>();


    private Canvas canvas;
    private TextMeshProUGUI title, subtitle, temperature, status, heatValue, contents, happening, verdict, verdictBody, bottleInfo, log, journalText, confirmText;
    private Slider heat;
    private Image swatch, qualityBack, qualityFill;
    private Button water, stir, pause, bottleAll, bottleOne, distill, empty;
    private TextMeshProUGUI pauseLabel, bottleAllLabel, distillLabel;
    private RectTransform ingredientList, journalPanel, confirmBox;
    private ScrollRect journalScroll;
    private readonly List<Button> tabs = new List<Button>();
    private readonly List<ItemSO> shownItems = new List<ItemSO>();

    private const int Order = 58;
    private static readonly string[] TabNames = { "Discoveries", "Reactions", "Substances", "Ingredients", "Experiments" };

    public string PanelName => station != null ? "Alchemy: " + station.DisplayName : "Alchemy";

    /// <summary>Opens the window of <paramref name="s"/> for the local <paramref name="p"/>.</summary>
    public static void Show(AlchemyStation s, CombatEntity p)
    {
        if (s == null || p == null)
            return;
        if (instance == null)
        {
            InventoryEventSystems.EnsureSingle();
            var go = new GameObject("Alchemy Station UI");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AlchemyStationUI>();
            instance.Build();
        }
        if (instance.station != s || instance.player != p)
        {
            instance.Detach();
            instance.station = s;
            instance.player = p;
            s.BeginTending(p);
            instance.ingredientSignature = instance.journalSignature = instance.fuelSignature = instance.filterSignature = null;
            instance.confirmEmpty = false;
            instance.sentStir = 0f;
            instance.trace?.Clear();
            instance.colorHistory.Clear();
            AlchemyKnowledge k = AlchemyKnowledge.For(p);
            if (k != null)
            {
                k.Changed -= instance.OnKnowledgeChanged;
                k.Changed += instance.OnKnowledgeChanged;
            }
        }
        instance.canvas.gameObject.SetActive(true);
        instance.Register();
        instance.Refresh(true);
    }

    /// <summary>Is the window open (for any station)?</summary>
    public static bool IsOpen => instance != null && instance.station != null && instance.canvas.gameObject.activeSelf;

    /// <summary>Closes the window if it shows <paramref name="s"/> (the server ended the visit).</summary>
    public static void Close(AlchemyStation s)
    {
        if (instance != null && instance.station == s && s != null)
            instance.Hide();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private void OnKnowledgeChanged() => journalSignature = null;

    private void Update()
    {
        if (station == null || player == null || !station.isActiveAndEnabled || !player.isActiveAndEnabled)
        {
            Hide();
            return;
        }
        Vector3 a = station.InteractionPoint.position, b = player.transform.position;
        a.y = b.y = 0f;
        if (Vector3.Distance(a, b) > station.InteractionRange * 1.6f + 0.5f)
        {
            Hide();
            return;
        }
        if (handsOn != null && handsOn.Active)
            return; // the spoon, the bellows or the filter: the hands-on mode has the mouse and the view
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        bool inventoryOpen = InventoryManager.IsInventoryOpenFor(player);
        SetHudFocus(!inventoryOpen);
        if (!float.IsNaN(pendingHeat) && Time.unscaledTime - heatSentAt >= 0.1f)
            SetHeat(pendingHeat); // the last value of a drag that came too soon after the previous one
        if (!float.IsNaN(pendingDamper) && Time.unscaledTime - damperSentAt >= 0.1f)
            SetDamper(pendingDamper);
        StirFromPad();
        traceTimer += Time.unscaledDeltaTime;
        if (traceTimer >= 0.5f)
        {
            traceTimer = 0f;
            AlchemyBrew current = station.Brew;
            if (current != null && !current.IsEmpty) trace.Push((float)current.Mixture.Temperature);
            else trace.Clear();
        }
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= 0.2f)
        {
            refreshTimer = 0f;
            Refresh(false);
        }
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
        if (handsOn != null && handsOn.Active)
        {
            AlchemyHandsOn h = handsOn;
            handsOn = null;
            h.End(false, false);
        }
        handsOn = null;
        Detach();
        SetHudFocus(false);
        Unregister();
        if (canvas != null && canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(false);
    }

    private void Detach()
    {
        if (station != null && player != null && sentStir > 0f)
            station.Request(player, new AlchemyRequest(AlchemyRequestKind.SetStirring, 0f), out _); // nobody stirs a closed window
        sentStir = 0f;
        if (station != null && player != null)
            station.EndTending(player);
        if (player != null)
        {
            AlchemyKnowledge k = player.GetComponent<AlchemyKnowledge>();
            if (k != null) k.Changed -= OnKnowledgeChanged;
        }
        station = null;
        player = null;
        journalOpen = false;
        confirmEmpty = false;
    }

    private void OnDestroy()
    {
        Hide();
        if (instance == this)
            instance = null;
    }

    // ------------------------------------------------------------------ panel registration
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

    /// <summary>Escape or the pause menu: closes the journal or the confirmation first, then the window.</summary>
    public void ClosePanel()
    {
        if (handsOn != null && handsOn.Active)
        {
            handsOn.End(false); // Escape at the vessel: back to the game (the preparation goes on as it is)
            return;
        }
        if (confirmEmpty || journalOpen)
        {
            confirmEmpty = false;
            journalOpen = false;
            Refresh(true);
            return;
        }
        Hide();
    }

    // ------------------------------------------------------------------ building
    private void Build()
    {
        canvas = UITheme.Canvas("Alchemy Station", Order, transform);
        Transform c = canvas.transform;
        Image backdrop = UITheme.Panel("Backdrop", c, new Color(0f, 0f, 0f, 0.35f), 0);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        Image window = UITheme.Window("Window", c, new Vector2(1560f, 920f));
        Transform w = window.transform;

        // Header.
        title = UITheme.Label(w, "", 28f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -56f), new Vector2(-360f, -14f));
        subtitle = UITheme.Label(w, "", 15f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Subtitle");
        UITheme.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -82f), new Vector2(-360f, -54f));
        RectTransform headerButtons = UITheme.Area("Header Buttons", w, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-340f, -64f), new Vector2(-24f, -18f));
        InventoryUIFactory.Horizontal(headerButtons.gameObject, 10f).childForceExpandWidth = true;
        UITheme.Button("Journal", headerButtons, "Journal", () => { journalOpen = !journalOpen; confirmEmpty = false; journalSignature = null; Refresh(true); }, UITheme.ButtonStyle.Primary, 0f, 42f);
        UITheme.Button("Close", headerButtons, "Close", Hide, UITheme.ButtonStyle.Normal, 0f, 42f);

        // Left: ingredients.
        RectTransform left = Column(w, "Ingredients", 24f, 404f);
        UITheme.Header(left, "Ingredients");
        water = UITheme.Button("Water", left, "Pour water", () => Send(new AlchemyRequest(AlchemyRequestKind.AddWater)), UITheme.ButtonStyle.Normal, 0f, 40f);
        ingredientList = UITheme.ScrollList("Ingredient List", left, 0f, out _);
        TextMeshProUGUI tip = UITheme.Label(left, "Each ingredient holds several substances that come out at their own pace. Grind… takes them to the mortar: the finer, the faster - and the less selective. What happens next depends on what is already in the vessel, the heat, stirring and time.", 13f, UITheme.Muted);
        InventoryUIFactory.Size(tip, -1f, 72f);

        // Middle: state, the fire, stirring, the contents.
        RectTransform mid = Column(w, "State", 424f, 1070f);
        RectTransform card = UITheme.CardColumn("Status", mid, 12, 2f);
        temperature = UITheme.Label(card, "", 36f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold, "Temperature");
        InventoryUIFactory.Size(temperature, -1f, 44f);
        status = UITheme.Label(card, "", 14f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Status");
        InventoryUIFactory.Size(status, -1f, 40f);
        swatch = UITheme.Panel("Liquid", card, new Color(0.5f, 0.55f, 0.6f, 0.3f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(swatch, -1f, 10f);

        // The fire: a heat slider for a burner; flames, fuel, bellows and an air vent for a real fire.
        RectTransform fire = UITheme.CardColumn("Fire", mid, 10, 6f);
        regulatedRow = UITheme.Row("Heat", fire, 32f, 12f);
        heatLabel = UITheme.Label(regulatedRow, "Heat", 16f, UITheme.Text);
        InventoryUIFactory.Size(heatLabel, 70f, 30f, 0f);
        heat = UITheme.BareSlider("Heat", regulatedRow, 0f, 1f, 0f, false);
        heat.onValueChanged.AddListener(OnHeatChanged);
        heatValue = UITheme.Label(regulatedRow, "", 14f, UITheme.Muted, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(heatValue, 170f, 30f, 0f);
        manualPanel = UITheme.Column("Manual Fire", fire, 0, 6f);
        RectTransform flames = UITheme.Row("Flames", manualPanel, 24f, 10f);
        TextMeshProUGUI fireLabel = UITheme.Label(flames, "Fire", 16f, UITheme.Text);
        InventoryUIFactory.Size(fireLabel, 70f, 24f, 0f);
        Image fireBack = UITheme.Panel("Flame Bar", flames, new Color(0f, 0f, 0f, 0.4f), 5);
        InventoryUIFactory.Size(fireBack, -1f, 14f, 1f);
        fireFill = UITheme.Panel("Flames", fireBack.transform, new Color(1f, 0.55f, 0.15f), 5);
        fireFill.rectTransform.anchorMin = Vector2.zero;
        fireFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fireFill.rectTransform.offsetMin = fireFill.rectTransform.offsetMax = Vector2.zero;
        fireText = UITheme.Label(flames, "", 13f, UITheme.Muted, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(fireText, 220f, 24f, 0f);
        fuelRow = UITheme.Row("Fuel", manualPanel, 32f, 8f);
        RectTransform air = UITheme.Row("Air", manualPanel, 32f, 10f);
        bellows = Fit(UITheme.Button("Bellows", air, "Work the bellows…", () => EnterMode(AlchemyHandsOnMode.Bellows), UITheme.ButtonStyle.Normal, 180f, 32f));
        TextMeshProUGUI ventLabel = UITheme.Label(air, "Air vent", 14f, UITheme.Muted);
        InventoryUIFactory.Size(ventLabel, 64f, 30f, 0f);
        damper = UITheme.BareSlider("Air Vent", air, 0f, 1f, 1f, false);
        damper.onValueChanged.AddListener(OnDamperChanged);
        traceRow = UITheme.Row("Trace", fire, 28f, 10f);
        Image traceBack = UITheme.Panel("Temperature Trace", traceRow, new Color(1f, 1f, 1f, 0.03f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(traceBack, -1f, 28f, 1f);
        trace = traceBack.gameObject.AddComponent<AlchemyTrace>();
        trace.Build(60);
        traceText = UITheme.Label(traceRow, "", 13f, UITheme.Muted, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(traceText, 190f, 28f, 0f);
        bathRow = UITheme.Row("Bath", fire, 28f, 6f);
        TextMeshProUGUI standIn = UITheme.Label(bathRow, "Stand in", 14f, UITheme.Muted);
        InventoryUIFactory.Size(standIn, 64f, 28f, 0f);
        string[] baths = { "On the fire", "Water bath", "Ice bath" };
        for (int i = 0; i < baths.Length; i++)
        {
            float v = i;
            Button b = UITheme.Button(baths[i], bathRow, baths[i], () => Send(new AlchemyRequest(AlchemyRequestKind.SetBath, v)), UITheme.ButtonStyle.Tab, 0f, 28f);
            InventoryUIFactory.Size(b, -1f, 28f, 1f);
            bathButtons.Add(b);
        }

        // Stirring: drag circles on the pad; how fast and how steadily is what counts.
        RectTransform stirring = UITheme.Row("Stirring", mid, 118f, 10f);
        Image padBack = UITheme.Panel("Stir Pad", stirring, new Color(1f, 1f, 1f, 0.05f), 54);
        InventoryUIFactory.Size(padBack, 108f, 108f, 0f);
        Image ring = UITheme.Panel("Ring", padBack.transform, new Color(0.45f, 0.6f, 0.8f, 0.12f), 44);
        UITheme.Place(ring.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
        ring.raycastTarget = false;
        TextMeshProUGUI padHint = UITheme.Label(padBack.transform, "stir", 12f, UITheme.Muted, TextAlignmentOptions.Center);
        InventoryUIFactory.Stretch(padHint.rectTransform);
        padHint.raycastTarget = false;
        Image spoon = UITheme.Panel("Spoon", padBack.transform, UITheme.Gold, 9);
        spoon.rectTransform.sizeDelta = new Vector2(18f, 18f);
        spoon.raycastTarget = false;
        stirPad = padBack.gameObject.AddComponent<AlchemyCirclePad>();
        stirPad.SetMarker(spoon.rectTransform);
        stirText = UITheme.Label(stirring, "", 14f, UITheme.Text);
        InventoryUIFactory.Size(stirText, -1f, 104f, 1f);
        RectTransform stirButtons = UITheme.Column("Buttons", stirring, 0, 4f);
        InventoryUIFactory.Size(stirButtons, 150f, 116f, 0f);
        spoon3D = Fit(UITheme.Button("Spoon", stirButtons, "Take the spoon…", () => EnterMode(AlchemyHandsOnMode.Stir), UITheme.ButtonStyle.Primary, 0f, 26f));
        stir = Fit(UITheme.Button("Stir", stirButtons, "Stir once", () => Send(new AlchemyRequest(AlchemyRequestKind.Stir, 1f)), UITheme.ButtonStyle.Normal, 0f, 26f));
        pause = Fit(UITheme.Button("Pause", stirButtons, "Pause", TogglePause, UITheme.ButtonStyle.Normal, 0f, 26f));
        pauseLabel = pause.GetComponentInChildren<TextMeshProUGUI>(true);
        fireOff = Fit(UITheme.Button("Fire Off", stirButtons, "Fire off", FireOff, UITheme.ButtonStyle.Ghost, 0f, 26f));
        fireOffLabel = fireOff.GetComponentInChildren<TextMeshProUGUI>(true);

        contentsHeader = UITheme.Header(mid, "In the vessel");
        RectTransform contentsArea = UITheme.ScrollList("Contents", mid, 170f, out _);
        contents = UITheme.Label(contentsArea, "", 15f, UITheme.Text);
        happeningHeader = UITheme.Header(mid, "What is happening");
        RectTransform happeningArea = UITheme.ScrollList("Happening", mid, 0f, out _);
        happening = UITheme.Label(happeningArea, "", 15f, UITheme.Text);

        // Right: verdict, taking out, log.
        RectTransform right = Column(w, "Result", 1090f, 1536f);
        verdictHeader = UITheme.Header(right, "If bottled now");
        RectTransform vcard = UITheme.CardColumn("Verdict", right, 14, 4f);
        verdict = UITheme.Label(vcard, "", 21f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Verdict");
        InventoryUIFactory.Size(verdict, -1f, 30f);
        qualityBack = UITheme.Panel("Quality", vcard, new Color(0f, 0f, 0f, 0.4f), 5);
        InventoryUIFactory.Size(qualityBack, -1f, 10f);
        qualityFill = UITheme.Panel("Fill", qualityBack.transform, UITheme.Good, 5);
        qualityFill.rectTransform.anchorMin = Vector2.zero;
        qualityFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        qualityFill.rectTransform.offsetMin = qualityFill.rectTransform.offsetMax = Vector2.zero;
        verdictBody = UITheme.Label(vcard, "", 14f, UITheme.Muted);
        InventoryUIFactory.Size(verdictBody, -1f, 74f);
        bottleInfo = UITheme.Label(right, "", 14f, UITheme.Muted);
        InventoryUIFactory.Size(bottleInfo, -1f, 22f);
        RectTransform bottleRow = UITheme.Row("Bottle", right, 44f, 10f);
        bottleRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        bottleAll = UITheme.Button("Bottle All", bottleRow, "Bottle", () => Send(new AlchemyRequest(AlchemyRequestKind.Bottle)), UITheme.ButtonStyle.Primary, 0f, 44f);
        bottleAllLabel = bottleAll.GetComponentInChildren<TextMeshProUGUI>(true);
        bottleOne = UITheme.Button("Bottle One", bottleRow, "Bottle one", () => Send(new AlchemyRequest(AlchemyRequestKind.Bottle, 0f, 1)), UITheme.ButtonStyle.Normal, 0f, 44f);
        RectTransform takeRow = UITheme.Row("Take", right, 42f, 10f);
        takeRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        distill = UITheme.Button("Distillate", takeRow, "Collect distillate", () => Send(new AlchemyRequest(AlchemyRequestKind.CollectDistillate)), UITheme.ButtonStyle.Normal, 0f, 42f);
        distillLabel = distill.GetComponentInChildren<TextMeshProUGUI>(true);
        empty = UITheme.Button("Empty", takeRow, "Empty vessel", AskEmpty, UITheme.ButtonStyle.Danger, 0f, 42f);
        UITheme.Header(right, "Process");
        RectTransform filterRow = UITheme.Row("Filter", right, 38f, 8f);
        filterDropdown = UITheme.BareDropdown("Filters", filterRow);
        InventoryUIFactory.Size(filterDropdown, -1f, 38f, 1f);
        filterButton = Fit(UITheme.Button("Filter", filterRow, "Filter…", DoFilter, UITheme.ButtonStyle.Normal, 110f, 38f));
        RectTransform separate = UITheme.Row("Separate", right, 38f, 8f);
        separate.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        decant = UITheme.Button("Decant", separate, "Pour off top layer", () => Send(new AlchemyRequest(AlchemyRequestKind.Decant)), UITheme.ButtonStyle.Normal, 0f, 38f);
        skim = UITheme.Button("Skim", separate, "Skim", () => Send(new AlchemyRequest(AlchemyRequestKind.Skim)), UITheme.ButtonStyle.Normal, 0f, 38f);
        clean = UITheme.Button("Clean", separate, "Clean vessel", () => Send(new AlchemyRequest(AlchemyRequestKind.CleanVessel)), UITheme.ButtonStyle.Ghost, 0f, 38f);
        processText = UITheme.Label(right, "", 13f, UITheme.Muted);
        InventoryUIFactory.Size(processText, -1f, 20f);
        UITheme.Header(right, "Observations");
        RectTransform logArea = UITheme.ScrollList("Log", right, 0f, out _);
        log = UITheme.Label(logArea, "", 14f, UITheme.Muted);

        // The journal, over the whole window.
        Image jp = UITheme.Panel("Journal", w, UITheme.Surface, UITheme.RadiusLarge);
        journalPanel = jp.rectTransform;
        UITheme.Place(journalPanel, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -96f));
        RectTransform jcol = UITheme.Column("Column", jp.transform, 16, 10f);
        InventoryUIFactory.Stretch(jcol);
        RectTransform tabRow = UITheme.Row("Tabs", jcol, 40f, 8f);
        tabRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            tabs.Add(UITheme.Button(TabNames[i], tabRow, TabNames[i], () => { journalTab = index; journalSignature = null; Refresh(true); }, UITheme.ButtonStyle.Tab, 0f, 40f));
        }
        RectTransform jlist = UITheme.ScrollList("Pages", jcol, 0f, out journalScroll);
        journalText = UITheme.Label(jlist, "", 15f, UITheme.Text);


        // Pour-away confirmation.
        Image confirm = UITheme.Window("Confirm", w, new Vector2(560f, 200f));
        confirmBox = confirm.rectTransform;
        RectTransform cc = UITheme.Column("Column", confirm.transform, 22, 14f);
        InventoryUIFactory.Stretch(cc);
        confirmText = UITheme.Label(cc, "", 18f, UITheme.Text, TextAlignmentOptions.Center);
        RectTransform row = UITheme.Row("Buttons", cc, 46f);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        UITheme.Button("Keep", row, "Keep it", () => { confirmEmpty = false; Refresh(true); }, UITheme.ButtonStyle.Primary);
        UITheme.Button("Pour", row, "Empty the vessel", () => { confirmEmpty = false; Send(new AlchemyRequest(AlchemyRequestKind.Empty)); }, UITheme.ButtonStyle.Danger);

        journalPanel.gameObject.SetActive(false);
        confirmBox.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
    }

    /// <summary>A button whose label shrinks to fit it on one line (small buttons, long labels).</summary>
    private static Button Fit(Button b)
    {
        TMP_Text t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = 10f;
            t.fontSizeMax = 15f;
            t.margin = new Vector4(6f, 0f, 6f, 0f);
        }
        return b;
    }

    private static RectTransform Column(Transform window, string name, float x0, float x1)
    {
        Image bg = UITheme.Panel(name, window, UITheme.Card, UITheme.Radius);
        UITheme.Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(x0, 20f), new Vector2(x1, -96f));
        RectTransform col = UITheme.Column("Column", bg.transform, 14, 8f);
        InventoryUIFactory.Stretch(col);
        return col;
    }

    // ------------------------------------------------------------------ actions
    private void Send(AlchemyRequest r, bool refresh = true)
    {
        if (station == null || player == null)
            return;
        station.RequestAndTell(player, r);
        if (refresh) Refresh(true);
    }

    private void OnDamperChanged(float v)
    {
        if (settingSlider)
            return;
        if (Time.unscaledTime - damperSentAt < 0.1f && v > 0.001f && v < 0.999f)
        {
            pendingDamper = v;
            return;
        }
        SetDamper(v);
    }

    private void SetDamper(float v)
    {
        pendingDamper = float.NaN;
        damperSentAt = Time.unscaledTime;
        Send(new AlchemyRequest(AlchemyRequestKind.SetDamper, Mathf.Round(v * 100f) / 100f), false);
    }

    /// <summary>A burner is turned off; a real fire is choked (its air vent closed).</summary>
    private void FireOff()
    {
        if (station.HasManualFire)
        {
            settingSlider = true;
            damper.SetValueWithoutNotify(0f);
            settingSlider = false;
            SetDamper(0f);
        }
        else
            SetHeat(0f);
    }

    /// <summary>The stir pad's turning becomes the stirring speed (a few changes a second at most: each is a step of the history).</summary>
    private void StirFromPad()
    {
        AlchemyBrew b = station.Brew;
        if (stirPad == null || b == null || b.IsEmpty)
            return;
        float target = stirPad.Dragging ? Mathf.Clamp01(stirPad.TurnsPerSecond / 1.5f) : 0f;
        target = Mathf.Round(target * 20f) / 20f;
        bool stop = target <= 0f && sentStir > 0f;
        if ((stop || Mathf.Abs(target - sentStir) >= 0.05f) && Time.unscaledTime - stirSentAt >= 0.25f)
        {
            sentStir = target;
            stirSentAt = Time.unscaledTime;
            Send(new AlchemyRequest(AlchemyRequestKind.SetStirring, target), false);
        }
    }

    /// <summary>Filter…: the filtering minigame (pour the vessel through the filter at the table).</summary>
    private void DoFilter()
    {
        int i = filterDropdown.value;
        if (i >= 0 && i < shownFilters.Count)
            EnterMode(AlchemyHandsOnMode.Filter, shownFilters[i].id);
    }

    // ------------------------------------------------------------------ hands on: the spoon, the bellows, the filter
    /// <summary>The window steps aside and the player works <paramref name="mode"/> at the vessel; it comes back when they put it down.</summary>
    private void EnterMode(AlchemyHandsOnMode mode, string filterId = null, ItemSO item = null)
    {
        if (station == null || player == null)
            return;
        if (sentStir > 0f)
        {
            sentStir = 0f; // the pad stops: the spoon (or nobody) stirs now
            station.Request(player, new AlchemyRequest(AlchemyRequestKind.SetStirring, 0f), out _);
        }
        handsOn = AlchemyHandsOn.For(station);
        canvas.gameObject.SetActive(false);
        SetHudFocus(true); // the hotbar, quick slots and ability bar stay hidden while the hands work
        if (!handsOn.Begin(mode, player, OnModeEnded, out string reason, filterId, item))
        {
            canvas.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(reason)) station.Tell(player, reason);
            Refresh(true);
        }
    }

    private void OnModeEnded(bool backToWindow)
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

    private void OnHeatChanged(float v)
    {
        if (settingSlider)
            return;
        // Dragging: send at most ten changes a second (each one is a step of the preparation's history); the last
        // value is sent by Update when it came too soon.
        if (Time.unscaledTime - heatSentAt < 0.1f && Mathf.Abs(v) > 0.001f && v < 0.999f)
        {
            pendingHeat = v;
            return;
        }
        SetHeat(v);
    }

    private void SetHeat(float v)
    {
        pendingHeat = float.NaN;
        heatSentAt = Time.unscaledTime;
        Send(new AlchemyRequest(AlchemyRequestKind.SetHeat, Mathf.Round(v * 100f) / 100f));
    }

    private void TogglePause()
    {
        AlchemyBrew b = station != null ? station.Brew : null;
        if (b == null) return;
        Send(new AlchemyRequest(b.IsSuspended ? AlchemyRequestKind.Resume : AlchemyRequestKind.Suspend));
    }

    private void AskEmpty()
    {
        AlchemyBrew b = station != null ? station.Brew : null;
        if (b == null) return;
        AlchemyResult r = b.Evaluate();
        bool worth = b.Mixture.LiquidVolume > 0.5 && r.Kind != AlchemyOutcomeKind.Inert;
        if (!worth && b.Mixture.DistillateVolume < 0.1)
        {
            Send(new AlchemyRequest(AlchemyRequestKind.Empty));
            return;
        }
        confirmEmpty = true;
        confirmText.text = b.Mixture.DistillateVolume >= 0.1
            ? "Empty the vessel? The liquid and the distillate flask are poured away (solids at the bottom are collected)."
            : "Empty the vessel? The liquid is poured away (solids at the bottom are collected). Bottle it first to keep it.";
        Refresh(true);
    }

    // ------------------------------------------------------------------ showing
    private void Refresh(bool force)
    {
        if (station == null || player == null)
            return;
        AlchemyRules rules = station.Rules;
        AlchemyKnowledge knowledge = AlchemyKnowledge.For(player);
        AlchemyJournalData data = knowledge != null ? knowledge.Data : new AlchemyJournalData();
        AlchemyBrew brew = station.Brew;
        AlchemyMixture m = brew?.Mixture;
        AlchemyEquipmentProfile eq = brew != null ? brew.Equipment : station.EquipmentProfile; // a restored preparation keeps its vessel

        title.text = station.DisplayName;
        subtitle.text = $"{eq.displayName} · holds {eq.capacity:0} u · {VaporText(eq)}{(eq.canCool ? " · can cool" : "")}{(eq.revealsSubstances ? " · transparent" : "")} · {PracticeText()}";

        // What it looks like (a test bench also shows the numbers).
        bool reveal = station.RevealsResults, thermo = station.HasThermometer;
        look.Read(brew);
        if (!force || colorHistory.Count == 0)
        {
            colorHistory.Enqueue(look.Color);
            while (colorHistory.Count > 30) colorHistory.Dequeue(); // about six seconds of refreshes
        }

        // Temperature and status.
        double t = m != null ? m.Temperature : station.Rules.Settings.defaultAmbient;
        float delta = (float)(t - lastTemperature);
        trend = Mathf.Lerp(trend, delta / 0.2f, 0.3f);
        lastTemperature = t;
        string arrow = trend > 0.15f ? " <color=#ffb070>▲</color>" : trend < -0.15f ? " <color=#9fd8ff>▼</color>" : "";
        temperature.text = thermo || m == null || brew.IsEmpty ? AlchemyText.Celsius(t) + arrow : Capital(look.HeatWords(), "cold") + arrow;
        var sb = new StringBuilder();
        if (m == null || brew.IsEmpty)
            sb.Append("The vessel is empty. Pour a liquid in and add ingredients to start.");
        else
        {
            sb.Append(reveal ? $"Time {AlchemyText.Clock(m.Elapsed)} · {AlchemyText.Units(m.FilledVolume)} of {eq.capacity:0} u"
                             : $"Time {AlchemyText.Clock(m.Elapsed)} · {AlchemyLook.FillWords(look.Fill)}");
            if (brew.IsBoiling) sb.Append(" · <color=#ffd166>boiling</color>");
            if (m.VaporMode == VaporHandling.Sealed) sb.Append($" · pressure {m.Pressure:0.00} / {eq.maxPressure:0.0}");
            if (m.Ruptured) sb.Append(" · <color=#f0a090>cracked</color>");
            if (m.Bath == AlchemyBath.Water) sb.Append(" · in a water bath");
            else if (m.Bath == AlchemyBath.Ice) sb.Append(" · <color=#9fd8ff>in an ice bath</color>");
            if (brew.IsSuspended) sb.Append("\n<color=#ffd166>PAUSED: time has stopped for this preparation.</color>");
            bool night = m.Variable(AlchemyVariables.Night) >= 0.5;
            if (reveal)
            {
                double ph = m.Ph;
                string acidity = ph < 6.5 ? $"pH {ph:0.0}, acid" : ph > 7.5 ? $"pH {ph:0.0}, alkaline" : "pH neutral";
                sb.Append($"\nAround it: {AlchemyText.Celsius(m.Ambient)}{(night ? ", night" : "")} · {acidity}");
            }
            else if (thermo)
                sb.Append($"\nAround it: {AlchemyText.Celsius(m.Ambient)}{(night ? ", night" : "")}");
            else if (night)
                sb.Append("\nIt is night.");
        }
        status.text = sb.ToString();
        Color liquidColor = m != null && m.LiquidVolume > 0.05 ? m.LiquidColor : new Color(0.5f, 0.55f, 0.6f, 0.25f);
        swatch.color = liquidColor;

        // The fire.
        bool manual = station.HasManualFire;
        settingSlider = true;
        regulatedRow.gameObject.SetActive(!manual || eq.canCool);
        manualPanel.gameObject.SetActive(manual);
        heat.minValue = eq.canCool ? -1f : 0f;
        heat.maxValue = manual ? 0f : 1f;
        heatLabel.text = manual ? "Cooling" : "Heat";
        if (Time.unscaledTime - heatSentAt > 0.5f)
            heat.SetValueWithoutNotify(m != null ? (float)m.Heat : 0f);
        if (Time.unscaledTime - damperSentAt > 0.5f)
            damper.SetValueWithoutNotify(m != null ? (float)m.Damper : 1f);
        settingSlider = false;
        double h = m != null ? m.EffectiveHeat : heat.value;
        double ambient = m != null ? m.Ambient : rules.Settings.defaultAmbient;
        heatValue.text = !reveal ? (manual ? "" : AlchemyJournal.HeatText(h))
            : manual ? $"→ {AlchemyText.Celsius(eq.TargetTemperature(System.Math.Min(0.0, m != null ? m.Heat : heat.value), ambient))}"
                     : $"{AlchemyJournal.HeatText(h)} → {AlchemyText.Celsius(eq.TargetTemperature(h, ambient))}";
        if (manual)
        {
            float flames = m != null ? (float)m.FireLevel : 0f;
            fireFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(flames), 1f);
            fireFill.color = Color.Lerp(new Color(0.55f, 0.15f, 0.08f), new Color(1f, 0.85f, 0.3f), flames);
            double fuel = m != null ? m.Fuel : 0;
            double lasts = fuel / System.Math.Max(1e-4, rules.Settings.fuelBurnRate * System.Math.Max(0.1, flames));
            string flameWords = flames < 0.05f ? "embers" : flames < 0.3f ? "low flames" : flames < 0.65f ? "a steady fire" : flames < 0.9f ? "a strong fire" : "roaring";
            string fuelWords = lasts > 120 ? "plenty of fuel" : lasts > 40 ? "the fuel is burning down" : "the fuel is nearly gone";
            fireText.text = m == null || !m.FireManual && fuel <= 0 ? "unlit - add fuel"
                : flames < 0.01f && fuel <= 0 ? "<color=#f0a090>out</color>"
                : reveal ? $"{flames * 100f:0}% → {AlchemyText.Celsius(eq.TargetTemperature(flames, ambient))} · fuel ~{AlchemyText.Clock(lasts)}"
                : $"{flameWords} · {(fuel <= 0 ? "no fuel left" : fuelWords)}";
            Fuels(rules);
        }
        bellows.interactable = manual;
        fireOffLabel.text = manual ? "Choke fire" : "Fire off";
        traceRow.gameObject.SetActive(thermo);
        if (thermo) RefreshTrace(m);
        bathRow.gameObject.SetActive(station.HasWaterBath || station.HasIceBath);
        for (int i = 0; i < bathButtons.Count; i++)
        {
            bathButtons[i].gameObject.SetActive(i == 0 || i == 1 && station.HasWaterBath || i == 2 && station.HasIceBath);
            UITheme.SetSelected(bathButtons[i], (m != null ? (int)m.Bath : 0) == i);
        }

        // Stirring.
        stirText.text = StirText(m, reveal, look);
        bool hasContents = m != null && !brew.IsEmpty;
        stir.interactable = hasContents;
        spoon3D.interactable = hasContents;
        pause.interactable = hasContents;
        pauseLabel.text = brew != null && brew.IsSuspended ? "Resume" : "Pause";
        water.gameObject.SetActive(station.HasWater);
        water.GetComponentInChildren<TextMeshProUGUI>(true).text = $"Pour water ({station.WaterPerPour:0} u)";
        Process(rules, brew);

        Ingredients(rules, data);
        contentsHeader.text = reveal ? "IN THE VESSEL" : "WHAT YOU SEE";
        happeningHeader.text = reveal ? "WHAT IS HAPPENING" : "WHAT IT IS DOING";
        verdictHeader.text = reveal ? "IF BOTTLED NOW" : "THE BREW";
        contents.text = reveal ? Contents(rules, data, brew) : Observed(brew, eq);
        happening.text = reveal ? Happening(rules, data, brew) : ObservedActivity(brew, thermo);
        Verdict(rules, data, brew, reveal);
        log.text = Log(rules, data, brew);

        journalPanel.gameObject.SetActive(journalOpen);
        if (journalOpen)
        {
            for (int i = 0; i < tabs.Count; i++)
                UITheme.SetSelected(tabs[i], i == journalTab);
            string page = Journal(rules, data);
            if (page != journalSignature)
            {
                journalSignature = page;
                journalText.text = page;
            }
        }
        confirmBox.gameObject.SetActive(confirmEmpty);
        confirmBox.SetAsLastSibling();
    }

    private static string VaporText(AlchemyEquipmentProfile eq) => eq.vapor switch
    {
        VaporHandling.Condense => "distils what boils off",
        VaporHandling.Sealed => $"sealed (bursts above {eq.maxPressure:0.#} pressure)",
        _ => "open",
    };

    // ------------------------------------------------------------------ ingredients
    private void Ingredients(AlchemyRules rules, AlchemyJournalData data)
    {
        InventoryManager inv = InventoryManager.For(player);
        var counts = new Dictionary<ItemSO, int>();
        shownItems.Clear();
        foreach (ItemSO free in station.FreeSupplies())
            if (free != null && !counts.ContainsKey(free) && station.IngredientOf(free) != null) // supplies (filter paper, firewood) go into no vessel
            {
                counts[free] = -1;
                shownItems.Add(free);
            }
        if (inv != null)
            foreach (InventoryItem it in inv.GetBagItems())
            {
                ItemSO so = it.itemScriptableObject;
                if (so == null || station.IngredientOf(so) == null) continue;
                if (counts.TryGetValue(so, out int n))
                {
                    if (n >= 0) counts[so] = n + it.stackCurrent;
                }
                else
                {
                    counts[so] = it.stackCurrent;
                    shownItems.Add(so);
                }
            }
        var sig = new StringBuilder();
        sig.Append(data.identified.Count).Append('#'); // the hints ("releases Solar Sap, ?") follow what is identified
        foreach (ItemSO so in shownItems)
            sig.Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(so)).Append(':').Append(counts[so]).Append('|');
        string signature = sig.ToString();
        if (signature == ingredientSignature)
            return; // rebuilt only when it changed: a button rebuilt under the mouse would lose the click
        ingredientSignature = signature;
        for (int i = ingredientList.childCount - 1; i >= 0; i--)
            Destroy(ingredientList.GetChild(i).gameObject);
        if (shownItems.Count == 0)
        {
            TextMeshProUGUI none = UITheme.Label(ingredientList, "You carry no alchemy ingredients. Herbs, salts, dusts and liquids can be found, bought or harvested.", 14f, UITheme.Muted);
            InventoryUIFactory.Size(none, -1f, 70f);
            return;
        }
        foreach (ItemSO so in shownItems)
        {
            ItemSO item = so;
            int n = counts[so];
            Image rowBg = UITheme.Panel(item.Name, ingredientList, UITheme.Card, UITheme.RadiusSmall);
            HorizontalLayoutGroup hl = InventoryUIFactory.Horizontal(rowBg.gameObject, 8f);
            hl.padding = new RectOffset(6, 6, 4, 4);
            hl.childForceExpandHeight = false;
            InventoryUIFactory.Size(rowBg, -1f, 46f);
            Image icon = InventoryUIFactory.Panel("Icon", rowBg.transform, Color.white);
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            if (item.Icon == null) icon.color = new Color(1f, 1f, 1f, 0.1f);
            InventoryUIFactory.Size(icon, 36f, 36f, 0f);
            string count = n < 0 ? "<color=#8fd68f>free</color>" : $"× {n}";
            TextMeshProUGUI label = UITheme.Label(rowBg.transform, $"{item.Name}\n<size=12><color=#9aa0a6>{count}{FormText(rules, item)}{Hint(rules, data, item)}</color></size>", 15f, UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 38f, 1f);
            if (station.GrindLevels(item).Count > 0)
                Fit(UITheme.Button("Grind", rowBg.transform, "Grind…", () => EnterMode(AlchemyHandsOnMode.Grind, null, item), UITheme.ButtonStyle.Ghost, 64f, 34f));
            UITheme.Button("Add", rowBg.transform, "+1", () => Send(new AlchemyRequest(AlchemyRequestKind.AddItem, 0f, 1, item)), UITheme.ButtonStyle.Normal, 44f, 34f);
            Button five = UITheme.Button("Add 5", rowBg.transform, "+5", () => Send(new AlchemyRequest(AlchemyRequestKind.AddItem, 0f, 5, item)), UITheme.ButtonStyle.Normal, 44f, 34f);
            five.interactable = n < 0 || n > 1;
        }
    }

    // ------------------------------------------------------------------ fire, stirring, processing
    /// <summary>A button per fuel the player can put on the fire (rebuilt only when what is available changes).</summary>
    private void Fuels(AlchemyRules rules)
    {
        var sig = new StringBuilder();
        var available = new List<(AlchemyFuel fuel, int n)>();
        foreach (AlchemyFuel f in rules.Fuels)
        {
            int n = station.FuelAvailable(player, f);
            if (n == 0) continue;
            available.Add((f, n));
            sig.Append(f.id).Append(':').Append(n).Append('|');
        }
        string signature = sig.ToString();
        if (signature == fuelSignature)
            return;
        fuelSignature = signature;
        for (int i = fuelRow.childCount - 1; i >= 0; i--)
            Destroy(fuelRow.GetChild(i).gameObject);
        if (available.Count == 0)
        {
            TextMeshProUGUI none = UITheme.Label(fuelRow, "No fuel: bring firewood, charcoal, resin or lamp oil.", 13f, UITheme.Muted);
            InventoryUIFactory.Size(none, -1f, 30f, 1f);
            return;
        }
        foreach ((AlchemyFuel fuel, int n) in available.GetRange(0, Mathf.Min(4, available.Count)))
        {
            string id = fuel.id;
            Button b = UITheme.Button(fuel.Name, fuelRow, $"+ {fuel.Name}{(n > 0 ? $" ({n})" : "")}", () => Send(AlchemyRequest.For(AlchemyRequestKind.AddFuel, id)),
                UITheme.ButtonStyle.Normal, 0f, 32f);
            InventoryUIFactory.Size(b, -1f, 32f, 1f);
        }
    }

    /// <summary>The last half minute of the temperature: how well the fire is held.</summary>
    private void RefreshTrace(AlchemyMixture m)
    {
        trace.Redraw(0f, 140f, t => t < 25f ? new Color(0.45f, 0.7f, 1f, 0.8f) : t < 60f ? new Color(0.45f, 0.85f, 0.55f, 0.8f)
                                           : t < 100f ? new Color(1f, 0.7f, 0.25f, 0.85f) : new Color(1f, 0.4f, 0.3f, 0.9f));
        if (m == null || station.Brew.IsEmpty)
        {
            traceText.text = "";
            return;
        }
        string steady = m.Swing < 1.5 ? "held steady" : m.Swing < 5 ? "wavering" : "swinging";
        traceText.text = $"{steady} (±{m.Swing:0.#} °C){(m.CoolingRate > 0.3 ? "\n<color=#9fd8ff>cooling fast</color>" : "")}";
    }

    private static string StirText(AlchemyMixture m, bool reveal, AlchemyLook look)
    {
        if (m == null)
            return "<color=#9aa0a6>Take the spoon to stir in the vessel, or drag circles on the pad: faster stirs harder, steady circles stir evenly.</color>";
        if (!reveal)
        {
            var o = new StringBuilder();
            if (m.StirSpeed > 0.01)
            {
                string how = m.StirSpeed < 0.25 ? "gently" : m.StirSpeed < 0.6 ? "steadily" : m.StirSpeed < 0.85 ? "briskly" : "hard";
                string even = m.Turbulence < 0.15 ? "in even circles" : m.Turbulence < 0.5 ? "a little unevenly" : "erratically";
                o.Append($"Stirring {how}, {even}");
            }
            else
                o.Append(m.Agitation > 0.1 ? "<color=#9aa0a6>Still swirling from the last stirring</color>" : "<color=#9aa0a6>Not stirring</color>");
            if (look.Thickness > 0.45f) o.Append("\nIt drags on the spoon");
            if (m.Aeration > 0.3) o.Append("\n<color=#e8b86b>It froths as it is stirred</color>");
            if (look.Layered) o.Append(look.Emulsion > 0.5f ? "\nThe two layers swirl into one" : "\nTwo layers lie apart");
            if (look.Slopping) o.Append("\n<color=#f0a090>It slops over the rim!</color>");
            return o.ToString();
        }
        var sb = new StringBuilder();
        if (m.StirSpeed > 0.01)
        {
            string even = m.Turbulence < 0.15 ? "steady" : m.Turbulence < 0.5 ? "uneven" : "erratic";
            sb.Append($"Stirring {m.StirSpeed * 100:0}%, {even}");
        }
        else
            sb.Append("<color=#9aa0a6>Not stirring (drag circles on the pad)</color>");
        sb.Append($"\nAgitation {m.Agitation * 100:0}%");
        if (m.Aeration > 0.3) sb.Append("\n<color=#e8b86b>Air is being beaten in</color>");
        if (m.IsLayered) sb.Append(m.Emulsion > 0.5 ? $"\nThe layers are stirred into one ({m.Emulsion * 100:0}%)" : "\nTwo layers lie apart");
        if (m.Agitation > m.Rules.Settings.splashAgitation && m.VaporMode != VaporHandling.Sealed) sb.Append("\n<color=#f0a090>It slops over the rim!</color>");
        return sb.ToString();
    }

    private void Process(AlchemyRules rules, AlchemyBrew brew)
    {
        AlchemyMixture m = brew?.Mixture;
        var sig = new StringBuilder();
        shownFilters.Clear();
        foreach (AlchemyFilter f in station.AvailableFilters())
        {
            shownFilters.Add(f);
            sig.Append(f.id).Append(':').Append(station.FilterUses(player, f)).Append('|');
        }
        if (sig.ToString() != filterSignature)
        {
            filterSignature = sig.ToString();
            int keep = filterDropdown.value;
            filterDropdown.ClearOptions();
            var options = new List<string>();
            foreach (AlchemyFilter f in shownFilters)
            {
                int uses = station.FilterUses(player, f);
                options.Add(uses < 0 ? f.Name : uses == 0 ? $"{f.Name} (no {f.medium})" : $"{f.Name} ({uses} × {f.medium})");
            }
            filterDropdown.AddOptions(options);
            filterDropdown.SetValueWithoutNotify(Mathf.Clamp(keep, 0, Mathf.Max(0, options.Count - 1)));
        }
        bool liquid = m != null && m.LiquidVolume > rules.Settings.traceAmount;
        int chosen = filterDropdown.value;
        filterButton.interactable = liquid && chosen >= 0 && chosen < shownFilters.Count && station.FilterUses(player, shownFilters[chosen]) != 0;
        decant.interactable = m != null && m.IsLayered;
        bool floats = false;
        if (m != null)
            for (int i = 0; i < rules.SubstanceCount; i++)
                floats |= m.Floats(i) && (m.UndissolvedAmount(i) > rules.Settings.noticeableAmount ||
                                          rules.Substances[i].phase == SubstancePhase.Solid && m.Amount(i) > rules.Settings.noticeableAmount);
        skim.interactable = floats;
        bool empty = brew == null || brew.IsEmpty;
        clean.interactable = empty && station.IsDirty;
        processText.text = empty && station.IsDirty ? "<color=#e8b86b>A film of residue clings to the vessel.</color>"
            : m != null && m.IsLayered ? "It appears to sit in two layers."
            : floats ? "It appears there is something afloat." : "";
    }

    /// <summary>" · extract" for ingredients that are not whole.</summary>
    private string FormText(AlchemyRules rules, ItemSO item)
    {
        AlchemyIngredientProfile p = station.IngredientOf(item)?.AlchemyProfile;
        if (p == null || p.dissolveSeconds <= 0.01f || string.IsNullOrEmpty(p.form) || p.form == "whole")
            return "";
        return " · " + rules.Form(p.form).Name.ToLowerInvariant();
    }

    /// <summary>"  · releases Solar Sap, ?" (what the player knows of the ingredient).</summary>
    private string Hint(AlchemyRules rules, AlchemyJournalData data, ItemSO item)
    {
        IAlchemyIngredient ing = station.IngredientOf(item);
        AlchemyIngredientProfile p = ing?.AlchemyProfile;
        if (p?.releases == null || p.releases.Count == 0)
            return "";
        var parts = new List<string>();
        foreach (SubstanceAmount r in p.releases)
            if (r != null)
                parts.Add(AlchemyJournal.IsIdentified(data, rules, r.substance) ? rules.SubstanceName(r.substance) : "?");
        return "  · " + string.Join(", ", parts);
    }

    // ------------------------------------------------------------------ contents and happenings
    private static string Contents(AlchemyRules rules, AlchemyJournalData data, AlchemyBrew brew)
    {
        if (brew == null || brew.IsEmpty)
            return "<color=#9aa0a6>Empty.</color>";
        AlchemyMixture m = brew.Mixture;
        double liquid = m.LiquidVolume;
        var sb = new StringBuilder();
        if (m.LayerSummary(out double top, out double topDensity, out double bottom, out double bottomDensity, out bool oilOnTop))
            sb.Append($"<color=#9fd8ff>Two layers: {AlchemyText.Units(top)} {(oilOnTop ? "oily" : "watery")} on top, {AlchemyText.Units(bottom)} {(oilOnTop ? "watery" : "oily")} below" +
                      $"{(m.Emulsion > 0.05 ? $" (stirred into one: {m.Emulsion * 100:0}%)" : "")}</color>\n");
        var order = new List<int>();
        for (int i = 0; i < rules.SubstanceCount; i++)
            if (m.Amount(i) >= rules.Settings.noticeableAmount || m.UndissolvedAmount(i) >= rules.Settings.noticeableAmount) order.Add(i);
        order.Sort((a, b) => m.Amount(b).CompareTo(m.Amount(a)));
        foreach (int i in order)
        {
            AlchemySubstance s = rules.Substances[i];
            string label = AlchemyJournal.IsIdentified(data, rules, s.id) ? $"<b>{s.Name}</b>" : $"<i>{Capital(s.observation, "an unknown substance")}</i>";
            string share = s.phase == SubstancePhase.Solid ? "settled at the bottom" : liquid > 1e-6 ? $"{m.Amount(i) / liquid * 100:0}%" : "";
            string amount = m.Amount(i) >= rules.Settings.noticeableAmount ? $"{AlchemyText.Units(m.Amount(i))} · {share}" : "";
            string left = m.UndissolvedAmount(i) >= rules.Settings.noticeableAmount
                ? $"{(amount.Length > 0 ? " · " : "")}<color=#e8b86b>{AlchemyText.Units(m.UndissolvedAmount(i))} undissolved{(m.Floats(i) ? ", floating" : m.SuspendedShare(i) > 0.3 ? ", clouding it" : ", settled")}</color>" : "";
            if (rules.LimitedSolubility[i] && m.Amount(i) >= rules.Settings.noticeableAmount)
            {
                double cap = m.Capacity(i);
                if (m.Amount(i) > cap * 1.02) left += " · <color=#ffd166>supersaturated</color>";
                else if (m.Amount(i) >= cap * 0.97) left += " · saturated";
            }
            sb.Append($"<color={AlchemyText.Hex(s.color)}>●</color> {label}  <color=#9aa0a6>{amount}</color>{left}\n");
        }
        foreach (PendingIngredient p in m.Pending)
        {
            string form = string.IsNullOrEmpty(p.Form) || p.Form == "whole" ? "" : $" ({rules.Form(p.Form).Name.ToLowerInvariant()})";
            sb.Append($"<color=#9aa0a6>◌ {p.Name}{form} — {p.Progress * 100:0}% given up</color>\n");
        }
        if (m.VaporVolume > 0.05)
            sb.Append($"<color=#9aa0a6>Vapour held in the vessel: {AlchemyText.Units(m.VaporVolume)}</color>\n");
        if (m.DistillateVolume > 0.05)
        {
            var names = new List<string>();
            for (int i = 0; i < rules.SubstanceCount; i++)
                if (m.DistillateAmount(i) >= rules.Settings.noticeableAmount)
                    names.Add(AlchemyJournal.IsIdentified(data, rules, rules.Substances[i].id) ? rules.Substances[i].Name : "?");
            sb.Append($"<color=#ffd166>Distillate flask: {AlchemyText.Units(m.DistillateVolume)} ({string.Join(", ", names)})</color>\n");
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static string Happening(AlchemyRules rules, AlchemyJournalData data, AlchemyBrew brew)
    {
        if (brew == null || brew.IsEmpty)
            return "<color=#9aa0a6>Nothing.</color>";
        var sb = new StringBuilder();
        for (int j = 0; j < rules.Reactions.Length; j++)
        {
            if (!brew.IsReacting(j)) continue;
            double rate = brew.RateOf(j);
            string speed = rate > 0.1 ? "fast" : rate > 0.02 ? "steadily" : "slowly";
            sb.Append($"• {AlchemyJournal.DescribeReaction(data, rules, rules.Reactions[j])} <color=#9aa0a6>({speed})</color>\n");
        }
        if (brew.IsBoiling)
            sb.Append("• <color=#ffd166>It is boiling: what boils off leaves the liquid.</color>\n");
        AlchemyMixture m = brew.Mixture;
        if (m.FireManual)
        {
            if (m.FireLevel < 0.01 && m.Fuel <= 0) sb.Append("• The fire is out: it cools.\n");
            else if (m.Fuel < 0.25) sb.Append("• <color=#e8b86b>The fire is burning low: add fuel.</color>\n");
            if (m.Air > 0.1) sb.Append("• The bellows make the flames roar.\n");
        }
        if (m.Agitation > rules.Settings.splashAgitation && m.VaporMode != VaporHandling.Sealed)
            sb.Append("• <color=#f0a090>Stirred so hard it slops over the rim.</color>\n");
        if (brew.Mixture.Pending.Count > 0)
            sb.Append("• Ingredients are giving up their substances.\n");
        for (int i = 0; i < rules.SubstanceCount; i++)
        {
            string who = AlchemyJournal.IsIdentified(data, rules, rules.Substances[i].id) ? rules.Substances[i].Name : "Something";
            if (brew.Tracker.Precipitating[i])
                sb.Append($"• <color=#e8b86b>{who} is coming out of solution: the liquid cannot hold it.</color>\n");
            if (brew.Tracker.Crystallizing[i])
                sb.Append($"• <color=#c8e0ff>{who} is crystallising: leave it still and let it cool slowly.</color>\n");
        }
        if (sb.Length == 0)
            sb.Append("<color=#9aa0a6>Nothing seems to be happening right now.</color>");
        return sb.ToString().TrimEnd('\n');
    }

    /// <summary>"Alchemy: Journeyman alchemist, 40% to expert".</summary>
    private string PracticeText()
    {
        double m = AlchemyStation.Mastery(player);
        CraftRank r = CraftMastery.RankOf(m);
        string next = r < CraftRank.Master ? $", {CraftMastery.RankProgress(m) * 100:0}% to {CraftMastery.RankName(r + 1).ToLowerInvariant()}" : "";
        return $"<color=#ffcc61>{CraftMastery.Title(CraftDiscipline.Alchemy, m)}</color>{next}";
    }

    /// <summary>
    /// What a practised alchemist can tell from the colour, the smell and the feel of a brew: from journeyman, what a
    /// brew they have made before is becoming; from expert, roughly how good it will be.
    /// </summary>
    private string Intuition(AlchemyJournalData data, AlchemyResult r)
    {
        double m = AlchemyStation.Mastery(player);
        CraftRank rank = CraftMastery.RankOf(m);
        if (rank < CraftRank.Journeyman || r == null || r.Outcome == null || r.Kind == AlchemyOutcomeKind.Inert)
            return "";
        if (r.IsFailure)
            return "\n<color=#ffcc61>Your nose tells you it is going wrong.</color>";
        if (!AlchemyJournal.IsDiscovered(data, r.OutcomeId))
            return "\n<color=#9aa0a6>It is nothing you have made before.</color>";
        string how = rank < CraftRank.Expert ? "" : r.Quality >= 0.8 ? ", and a fine one" : r.Quality >= 0.55 ? ", a fair one" : r.Quality >= 0.3 ? ", but weak" : ", but a poor one";
        string name = r.DisplayName.ToLowerInvariant();
        return $"\n<color=#ffcc61>You know that smell: it is becoming {CraftText.A(name)} {name}{how}.</color>";
    }

    private void Verdict(AlchemyRules rules, AlchemyJournalData data, AlchemyBrew brew, bool reveal)
    {
        AlchemyResult r = brew != null && !brew.IsEmpty ? brew.Evaluate() : null;
        qualityBack.gameObject.SetActive(reveal);
        if (!reveal)
        {
            // Outside a test bench nobody knows what it is until it is bottled: only how it looks.
            verdict.text = look.HasLiquid ? Capital(AlchemyLook.ColorName(look.Color), "—") : "—";
            verdictBody.text = look.HasLiquid
                ? $"{Capital(look.LiquidWords(), "")}.\n<color=#9aa0a6>What it has become - and how good it is - shows once it is bottled.</color>" + Intuition(data, r)
                : brew != null && !brew.IsEmpty ? "There is no liquid to bottle." : "Nothing in the vessel.";
        }
        else if (r == null || r.LiquidUnits < rules.Settings.traceAmount)
        {
            verdict.text = "—";
            verdictBody.text = brew != null && !brew.IsEmpty ? "There is no liquid to bottle (only solids or vapour)." : "Nothing in the vessel.";
            qualityFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        }
        else
        {
            bool known = r.Kind == AlchemyOutcomeKind.Inert || AlchemyJournal.IsDiscovered(data, r.OutcomeId);
            verdict.text = known ? r.DisplayName : "Unknown result";
            float q = known ? (float)r.Quality : 0f;
            qualityFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(q), 1f);
            qualityFill.color = q >= 0.65f ? UITheme.Good : q >= 0.35f ? UITheme.Gold : UITheme.Bad;
            var sb = new StringBuilder();
            if (!known)
                sb.Append("You have never made this. Bottle it to find out what it is.\n");
            else if (r.Outcome.HasPotency && !r.IsFailure)
                sb.Append($"Quality {r.Quality * 100:0}% (strength {r.Potency * 100:0}%, purity {r.Purity * 100:0}%)\n");
            else if (!string.IsNullOrEmpty(r.Outcome.description))
                sb.Append(r.Outcome.description).Append('\n');
            foreach (string note in r.Notes)
                sb.Append("· ").Append(note).Append('\n');
            verdictBody.text = sb.ToString().TrimEnd('\n');
        }

        int possible = station.BottlesPossible(player, out AlchemyResult prediction);
        ItemSO vial = station.BottleItem;
        InventoryManager inv = InventoryManager.For(player);
        if (prediction != null && prediction.Bottles > 0 && prediction.Outcome != null && prediction.Outcome.producesItems)
            bottleInfo.text = vial != null
                ? $"Fills {prediction.Bottles} bottle{(prediction.Bottles > 1 ? "s" : "")} · you have {(inv != null ? inv.GetItemCount(vial) : 0)} × {vial.Name}"
                : $"Fills {prediction.Bottles} bottle{(prediction.Bottles > 1 ? "s" : "")} ({rules.UnitsPerBottle(prediction.Outcome):0.#} u each)";
        else
            bottleInfo.text = "";
        bottleAll.interactable = possible > 0;
        bottleOne.interactable = possible > 0;
        bottleAllLabel.text = possible > 0 ? $"Bottle {possible}" : "Bottle";
        AlchemyMixture m = brew?.Mixture;
        bool distills = (brew != null ? brew.Equipment : station.EquipmentProfile).vapor == VaporHandling.Condense;
        distill.gameObject.SetActive(distills);
        distill.interactable = m != null && m.DistillateVolume > rules.Settings.traceAmount;
        distillLabel.text = m != null && m.DistillateVolume > 0.05 ? $"Collect distillate ({AlchemyText.Units(m.DistillateVolume)})" : "Collect distillate";
        empty.interactable = brew != null;
    }

    /// <summary>What can be seen in the vessel (outside a test bench): the look in sentences, a shifting colour, the flask.</summary>
    private string Observed(AlchemyBrew brew, AlchemyEquipmentProfile eq)
    {
        if (brew == null || brew.IsEmpty)
            return "<color=#9aa0a6>Empty.</color>";
        var lines = new List<string>();
        look.Describe(lines, string.IsNullOrWhiteSpace(eq.displayName) ? "vessel" : eq.displayName.ToLowerInvariant(), station.HasThermometer);
        Color then = colorHistory.Count > 0 ? colorHistory.Peek() : look.Color;
        float shift = Mathf.Abs(then.r - look.Color.r) + Mathf.Abs(then.g - look.Color.g) + Mathf.Abs(then.b - look.Color.b);
        if (look.HasLiquid && then.a > 0f && shift > 0.06f)
            lines.Add(shift > 0.25f ? $"Its colour is changing fast, toward {AlchemyLook.ColorName(look.Color)}." : $"Its colour is slowly turning {AlchemyLook.ColorName(look.Color)}.");
        AlchemyMixture m = brew.Mixture;
        if (m.DistillateVolume > 0.05)
            lines.Add(m.DistillateVolume > 8 ? "The flask of the still is filling up." : "Drops have gathered in the flask of the still.");
        if (m.VaporMode == VaporHandling.Sealed && m.Pressure > m.Equipment.maxPressure * 0.75)
            lines.Add("<color=#f0a090>The lid strains and creaks.</color>");
        return string.Join("\n", lines);
    }

    /// <summary>What it is doing (outside a test bench): the heat's and the reactions' signs, the fire.</summary>
    private string ObservedActivity(AlchemyBrew brew, bool thermo)
    {
        if (brew == null || brew.IsEmpty)
            return "<color=#9aa0a6>Nothing.</color>";
        var lines = new List<string>();
        look.DescribeActivity(lines, thermo);
        AlchemyMixture m = brew.Mixture;
        if (m.FireManual)
        {
            if (m.FireLevel < 0.01 && m.Fuel <= 0) lines.Add("The fire is out.");
            else if (m.Fuel < 0.25) lines.Add("<color=#e8b86b>The fire is burning low.</color>");
            if (m.Air > 0.1) lines.Add("The flames roar under the bellows' breath.");
            if (m.Damper < 0.2 && m.FireLevel > 0.05) lines.Add("Choked of air, the fire smoulders and smokes.");
        }
        if (lines.Count == 0)
            lines.Add("<color=#9aa0a6>Nothing seems to be happening right now.</color>");
        return string.Join("\n", lines);
    }

    // ------------------------------------------------------------------ log
    private static string Log(AlchemyRules rules, AlchemyJournalData data, AlchemyBrew brew)
    {
        if (brew == null)
            return "<color=#9aa0a6>Nothing yet.</color>";
        IReadOnlyList<AlchemyEvent> events = brew.RecentEvents;
        var lines = new List<string>();
        for (int i = events.Count - 1; i >= 0 && lines.Count < 16; i--)
        {
            string line = Describe(rules, data, brew, events[i]);
            if (!string.IsNullOrEmpty(line))
                lines.Add($"<color=#6f7680>{AlchemyText.Clock(events[i].Time)}</color>  {line}");
        }
        return lines.Count > 0 ? string.Join("\n", lines) : "<color=#9aa0a6>Nothing yet.</color>";
    }

    /// <summary>An event in the words of what the player knows (empty = not worth a line).</summary>
    public static string Describe(AlchemyRules rules, AlchemyJournalData data, AlchemyBrew brew, AlchemyEvent e)
    {
        switch (e.Kind)
        {
            case AlchemyEventKind.IngredientAdded:
            {
                string n = IngredientName(brew, e.Id);
                return $"Added {(e.Value > 1 ? $"{e.Value:0} × " : "")}{n}{(string.IsNullOrEmpty(e.Text) ? "." : ": " + e.Text + ".")}";
            }
            case AlchemyEventKind.IngredientDissolved: return $"{e.Text} has dissolved.";
            case AlchemyEventKind.SubstanceAdded:
                return string.IsNullOrEmpty(e.Text) ? $"Poured {AlchemyText.Units(e.Value)} of {AlchemyJournal.SubstanceLabel(data, rules, e.Id)}."
                    : $"<color=#e8b86b>{AlchemyText.Units(e.Value)} of {AlchemyJournal.SubstanceLabel(data, rules, e.Id)} {e.Text}.</color>";
            case AlchemyEventKind.ReactionStarted:
            {
                CompiledReaction r = rules.Reaction(e.Id);
                if (r == null) return "";
                return AlchemyJournal.IsUnderstood(data, rules, e.Id) ? $"<color=#8fd68f>{r.Definition.Name}</color> begins." : Capital(e.Text, "Something starts to react") + ".";
            }
            case AlchemyEventKind.ReactionStopped:
            {
                CompiledReaction r = rules.Reaction(e.Id);
                return r != null && AlchemyJournal.IsUnderstood(data, rules, e.Id) ? $"{r.Definition.Name} stops." : "";
            }
            case AlchemyEventKind.SubstanceAppeared:
            {
                AlchemySubstance s = rules.Substance(e.Id);
                if (s == null || s.role == SubstanceRole.Solvent || s.role == SubstanceRole.Raw || s.role == SubstanceRole.Reagent) return "";
                return AlchemyJournal.IsIdentified(data, rules, e.Id) ? $"{s.Name} forms." : $"Something new appears: {e.Text}.";
            }
            case AlchemyEventKind.SubstanceDepleted:
            {
                AlchemySubstance s = rules.Substance(e.Id);
                if (s == null || s.role != SubstanceRole.Active || !AlchemyJournal.IsIdentified(data, rules, e.Id)) return "";
                return $"The {s.Name} is gone.";
            }
            case AlchemyEventKind.BoilingStarted: return "<color=#ffd166>It starts to boil.</color>";
            case AlchemyEventKind.BoilingStopped: return "It stops boiling.";
            case AlchemyEventKind.Eruption: return "<color=#f0a090>It erupts! Part of the mixture splashes out.</color>";
            case AlchemyEventKind.VesselRuptured: return "<color=#f0a090>The vessel cracks under the pressure!</color>";
            case AlchemyEventKind.Suspended: return "Paused.";
            case AlchemyEventKind.Resumed: return "Resumed.";
            case AlchemyEventKind.Bottled: return $"Bottled {e.Value:0} × {e.Text}.";
            case AlchemyEventKind.DistillateCollected: return $"Collected the distillate ({e.Text}).";
            case AlchemyEventKind.Refused: return $"<color=#f0a090>{e.Text}</color>";
            case AlchemyEventKind.CrystalsForming:
            {
                AlchemySubstance s = rules.Substance(e.Id);
                if (s == null) return "";
                return AlchemyJournal.IsIdentified(data, rules, e.Id) ? $"<color=#c8e0ff>{s.Name} starts to crystallise.</color>" : $"<color=#c8e0ff>Crystals start to grow: {e.Text}.</color>";
            }
            case AlchemyEventKind.Splashed: return "<color=#f0a090>It slops over the rim!</color>";
            case AlchemyEventKind.FireOut: return "The fire has gone out.";
            case AlchemyEventKind.Filtered: return $"Poured through the {e.Text.ToLowerInvariant()}.";
            case AlchemyEventKind.Decanted: return "Poured off the top layer.";
            case AlchemyEventKind.Skimmed: return "Skimmed the surface.";
            case AlchemyEventKind.Precipitated:
            {
                AlchemySubstance s = rules.Substance(e.Id);
                if (s == null) return "";
                return AlchemyJournal.IsIdentified(data, rules, e.Id)
                    ? $"<color=#e8b86b>{s.Name} comes out of solution.</color>"
                    : $"<color=#e8b86b>Something will not stay dissolved: {e.Text}.</color>";
            }
        }
        return "";
    }

    private static string IngredientName(AlchemyBrew brew, string id)
    {
        IReadOnlyList<AlchemyOperation> h = brew.History;
        for (int i = h.Count - 1; i >= 0; i--)
            if (h[i].kind == AlchemyOpKind.AddIngredient && h[i].id == id)
                return h[i].label;
        return id;
    }

    private static string Capital(string s, string fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) s = fallback;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    // ------------------------------------------------------------------ journal
    private string Journal(AlchemyRules rules, AlchemyJournalData d)
    {
        var sb = new StringBuilder();
        switch (journalTab)
        {
            case 0:
            {
                int known = 0, total = 0;
                foreach (AlchemyOutcome o in rules.Outcomes)
                {
                    if (o.kind == AlchemyOutcomeKind.Inert) continue;
                    total++;
                    if (AlchemyJournal.IsDiscovered(d, o.id)) known++;
                }
                sb.Append($"<color=#9aa0a6>{known} of {total} results found. Each was made by a mixture in a certain state - " +
                          "the steps below reproduce it.</color>\n\n");
                var list = new List<AlchemyDiscovery>(d.discoveries);
                list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
                foreach (AlchemyDiscovery x in list)
                {
                    AlchemyOutcome o = rules.Outcome(x.outcomeId);
                    string tier = string.IsNullOrEmpty(x.bestTier) ? "" : $" {x.bestTier}";
                    sb.Append($"<b><color=#ffd166>{x.name}</color></b>  <color=#9aa0a6>best{tier} {x.bestQuality * 100:0}% · made {x.timesMade}× · {x.bottlesMade} bottles</color>\n");
                    if (o != null && !string.IsNullOrEmpty(o.description))
                        sb.Append($"<i>{o.description}</i>\n");
                    if (!string.IsNullOrEmpty(x.bestEquipment))
                        sb.Append($"<size=13><color=#9aa0a6>Best made in a {x.bestEquipment}:</color></size>\n");
                    foreach (string step in x.bestProcedure)
                        sb.Append($"<size=13>    {step}</size>\n");
                    sb.Append('\n');
                }
                if (list.Count == 0)
                    sb.Append("Nothing yet. Put a liquid and ingredients in the vessel, heat it, wait, and bottle what it becomes.");
                break;
            }
            case 1:
            {
                var understood = new StringBuilder();
                var seen = new StringBuilder();
                foreach (CompiledReaction r in rules.Reactions)
                {
                    AlchemyReactionNote n = AlchemyJournal.ReactionNote(d, r.Definition.id);
                    if (n == null) continue;
                    if (AlchemyJournal.IsUnderstood(d, rules, r.Definition.id))
                    {
                        understood.Append(AlchemyJournal.DescribeReaction(d, rules, r)).Append('\n');
                        if (!string.IsNullOrEmpty(r.Definition.description))
                            understood.Append($"<size=13><i><color=#9aa0a6>{r.Definition.description}</color></i></size>\n");
                        understood.Append('\n');
                    }
                    else
                    {
                        seen.Append("• ").Append(AlchemyJournal.DescribeReaction(d, rules, r)).Append('\n');
                    }
                }
                sb.Append($"<color=#9aa0a6>A reaction is understood after seeing it in {rules.Settings.observationsToUnderstand} different preparations.</color>\n\n");
                sb.Append(understood.Length > 0 ? understood.ToString() : "No reaction understood yet.\n\n");
                if (seen.Length > 0)
                    sb.Append("<b>Seen, not understood yet</b>\n").Append(seen);
                break;
            }
            case 2:
            {
                int unknown = 0;
                foreach (AlchemySubstance s in rules.Substances)
                {
                    if (!AlchemyJournal.IsIdentified(d, rules, s.id)) { unknown++; continue; }
                    sb.Append($"<color={AlchemyText.Hex(s.color)}>●</color> <b>{s.Name}</b>  <color=#9aa0a6>{s.role}, {(s.phase == SubstancePhase.Solid ? "settles" : $"boils at {s.boilingPoint:0} °C")}</color>\n");
                    if (!string.IsNullOrEmpty(s.description))
                        sb.Append($"<size=13><i>{s.description}</i></size>\n");
                    var props = new List<string>();
                    foreach (PropertyValue p in s.properties)
                        if (p != null && Mathf.Abs(p.value) > 0.01f)
                            props.Add($"{rules.PropertyName(p.property)} {p.value:0.##}");
                    if (props.Count > 0)
                        sb.Append($"<size=13><color=#9aa0a6>{string.Join(" · ", props)}</color></size>\n");
                    foreach (string line in AlchemyJournal.SubstanceBehaviour(d, rules, s))
                        sb.Append($"<size=13><color=#e8b86b>{line}</color></size>\n");
                    sb.Append('\n');
                }
                sb.Append($"<color=#9aa0a6>{unknown} substance{(unknown == 1 ? "" : "s")} not identified yet. Understanding reactions and bottling results identifies them; a glass vessel shows what is inside.</color>");
                break;
            }
            case 3:
            {
                foreach (AlchemyIngredientNote n in d.ingredients)
                {
                    sb.Append($"<b>{n.name}</b>  <color=#9aa0a6>used {n.timesUsed}×</color>\n");
                    AlchemyIngredientProfile p = FindProfile(n.ingredientId);
                    if (p != null)
                    {
                        var parts = new List<string>();
                        foreach (SubstanceAmount r in p.releases)
                            if (r != null)
                                parts.Add(AlchemyJournal.IsIdentified(d, rules, r.substance) ? $"{rules.SubstanceName(r.substance)} ({r.amount:0.#} u)" : "?");
                        sb.Append($"<size=13>Releases {string.Join(", ", parts)} · dissolves in {(p.dissolveSeconds <= 0.01f ? "an instant" : $"~{p.dissolveSeconds:0} s")}" +
                                  $"{(p.temperature < 5f ? " · ice cold" : p.temperature > 35f ? " · warm" : "")}</size>\n");
                    }
                    sb.Append('\n');
                }
                if (d.ingredients.Count == 0)
                    sb.Append("No ingredient used yet.");
                break;
            }
            default:
            {
                for (int i = d.experiments.Count - 1; i >= 0 && i >= d.experiments.Count - 15; i--)
                {
                    AlchemyExperiment x = d.experiments[i];
                    sb.Append($"<b>{x.name}</b>  <color=#9aa0a6>{x.quality * 100:0}% · {x.bottles} bottles · {x.equipment} · {new System.DateTime(x.utc).ToLocalTime():g}</color>\n");
                    foreach (string step in x.procedure)
                        sb.Append($"<size=13>    {step}</size>\n");
                    foreach (string note in x.notes)
                        sb.Append($"<size=13><color=#9aa0a6>    · {note}</color></size>\n");
                    sb.Append('\n');
                }
                if (d.experiments.Count == 0)
                    sb.Append("No experiment finished yet.");
                break;
            }
        }
        return sb.ToString();
    }

    /// <summary>The profile of an ingredient the player knows by id (in the inventory, the free supplies or the presets).</summary>
    private AlchemyIngredientProfile FindProfile(string id)
    {
        foreach (ItemSO item in shownItems)
        {
            IAlchemyIngredient ing = station.IngredientOf(item);
            if (ing != null && ing.AlchemyId == id) return ing.AlchemyProfile;
        }
        return AlchemyDefaults.Ingredient(id)?.profile;
    }
}
