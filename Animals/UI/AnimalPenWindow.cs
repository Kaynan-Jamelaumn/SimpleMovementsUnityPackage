using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window of the pen the local player is tending (built from code, one per machine).
/// <list type="bullet">
/// <item><b>Left - the animals</b>: every animal in the pen as it looks (name, kind, stage, how it seems and how its body
/// looks), and the player's own animals elsewhere - in their other pens and at the boarding stable - to choose and see to
/// from here, to bring here, or (the remains of one that died boarded) to clear away.</item>
/// <item><b>Middle - the animal</b>, in five pages. <i>Overview</i>: what is seen of it (its needs, how clean, comfortable
/// and calm it is, its hooves and its welfare as gauges and words, the signs a keeper notices - never the numbers it hides),
/// <i>Look closely</i>, and the work that cannot be undone: slaughtering its owner's living animal (two clicks), butchering
/// or clearing away the remains of a dead one. <i>Care</i>: what it gives (eggs, milk, wool: how much waits and of what
/// quality, how it is making it and the tool collecting it takes - collected from it, or from every animal here), then its
/// species' care (brushing, washing, petting, hoof care, a species' own), each with whether it can be done now, the tool it
/// needs and whether the player carries one, the water it takes and a supply such as soap - done to it, or to every animal
/// here; <i>Look it over</i>. <i>Health</i>: the illnesses
/// its keepers know of, the signs it shows and what a careful look found, the medicines the player carries (for it, or a
/// drench or feed remedy for the whole pen) and sending for the veterinarian for the fee shown. <i>History</i>: its health,
/// the care it had and how its last days went, asked for when wanted. <i>Family</i>: its bond with its owner and the perks
/// it has brought, whether it is expecting, its parents, generation and young, its companions and rivals, what its owner
/// knows of its breeding in words, and the partners it could be bred with here - each with Breed, or why not; a bonded horse
/// can be called to the fence. Naming it is below the pages.</item>
/// <item><b>Right - the pen</b>: its feeder, trough and grass, how clean its floor, bedding and water are, its shelter and
/// the weather; filling the trough, scrubbing out fouled water, mucking out (laying fresh straw when the player carries
/// some), collecting all that waits in it, feeding the chosen animal by hand, stocking the feeder and letting a young animal out of its crate - with what
/// the player carries.</item>
/// </list>
/// Everything is asked of the rules (the server in a session) and answered; one request at a time. What the player carries
/// is read from their own inventory (on a client, its copy) only to show what can be done: the rules check it again. Work
/// that went well is heard at the animal (the Animal Database's sounds). It is an exclusive panel of the player's inventory
/// while shown; walking away closes it.
/// </summary>
public class AnimalPenWindow : MonoBehaviour, IExclusivePanel
{
    private static AnimalPenWindow instance;

    /// <summary>The pages of the middle column.</summary>
    private enum Page { Overview, Care, Health, History, Family, Pen }

    private static readonly string[] PageNames = { "Overview", "Care", "Health", "History", "Family", "Pen" };

    private AnimalPen pen;
    private CombatEntity player;
    private InventoryManager registeredWith;
    private bool managesCursor, hudFocus;
    private CursorLockMode previousLock;
    private bool previousVisible;

    private Canvas canvas;
    private TextMeshProUGUI title, subtitle, penText, animalText, healthText, historyText, message, strawLabel, collectLabel, familyText;
    private RectTransform animalList, elsewhereList, carriedFood, carriedLivestock, careList, medicineList, partnerList;
    private Button callButton;
    private string partnerSignature = "";
    private RectTransform[] pages;
    private Button[] pageButtons, stockButtons;
    private TMP_InputField nameInput;
    private Button lookButton, slaughterButton, butcherButton, remainsButton, renameButton, crateButton;
    // Phase 5: the pen's level, machines and upkeep (the Pen page).
    private TextMeshProUGUI upgradesText;
    private Button levelButton, payButton, aheadButton, hopperButton, watererButton, traysButton;
    private Button careExamineButton, examineButton, vetButton, historyButton;
    private Button waterButton, scrubButton, muckButton, collectButton;
    private RectTransform collectRow;
    private Toggle strawToggle;

    private string selected = "";
    private Page page = Page.Overview;
    private int stockCount = 1;
    /// <summary>Lay fresh straw when mucking out (when straw is carried and the pen is bedded).</summary>
    private bool withStraw = true;
    /// <summary>Use the supply of this tag (soap) in the care that takes it (off until the player ticks it).</summary>
    private readonly Dictionary<string, bool> withSupply = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    private bool waiting;
    private int requestSeq, waitingSeq;
    private float waitingSince, messageTime, refreshTimer, listTimer;
    private AnimalRequest lastRequest;
    private string listSignature = "", carriedSignature = "", careSignature = "", medicineSignature = "";
    private readonly List<string> closeLook = new List<string>(), examined = new List<string>(), history = new List<string>();
    private string closeLookOf = "", examinedOf = "", historyOf = "", historyTitle = "";

    /// <summary>Slaughter takes two clicks: the animal the first click was for, and when.</summary>
    private string slaughterArmed = "";
    private float slaughterArmedAt;

    private readonly Carried carried = new Carried();
    private readonly List<InventoryItem> stacks = new List<InventoryItem>();

    private const int Order = 58;
    /// <summary>Seconds within which the second click slaughters.</summary>
    private const float ArmSeconds = 4f;
    /// <summary>The height of a page of the middle column (the tabs above, the name below).</summary>
    private const float PageHeight = 548f;
    private const string BeddingTag = "bedding";
    private const string Muted = "#9aa4b5", Warn = "#ef7b6e", Note = "#e2c46a";

    public string PanelName => pen != null ? "Pen: " + pen.DisplayName : "Pen";
    public static bool IsOpen => instance != null && instance.pen != null && instance.canvas.gameObject.activeSelf;
    public static bool IsShowing(AnimalPen p) => p != null && IsOpen && instance.pen == p;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    // ------------------------------------------------------------------ what the player carries
    /// <summary>One kind of item the player carries (by its key): its name, how many, and what it is to animals.</summary>
    private sealed class Carry
    {
        public string key = "", name = "";
        public int count;
        /// <summary>A crate holding one of the world's animals: its copy's code (0: a plain crate; plain crates of a kind are counted together).</summary>
        public int code;
        /// <summary>A care supply (soap, straw): used up.</summary>
        public AnimalToolData tool;
        /// <summary>A medicine for animals.</summary>
        public AnimalTreatmentData treatment;
    }

    /// <summary>One care tool the player carries (a copy that wears): its key, its data and what is left of it.</summary>
    private struct CarriedTool
    {
        public string key;
        public AnimalToolData tool;
        public float edge;
    }

    /// <summary>What the player carries that the pen's work can use (read every half second, sorted by key).</summary>
    private sealed class Carried
    {
        public readonly SortedDictionary<string, Carry> food = new SortedDictionary<string, Carry>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, Carry> young = new SortedDictionary<string, Carry>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, Carry> medicine = new SortedDictionary<string, Carry>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, Carry> supplies = new SortedDictionary<string, Carry>(StringComparer.Ordinal);
        public readonly List<CarriedTool> tools = new List<CarriedTool>();
        /// <summary>The litres in the container the player would pour from.</summary>
        public float water;

        public void Clear()
        {
            food.Clear();
            young.Clear();
            medicine.Clear();
            supplies.Clear();
            tools.Clear();
            water = 0f;
        }
    }

    // ------------------------------------------------------------------ opening
    public static void Show(AnimalPen p, CombatEntity who)
    {
        if (p == null || who == null) return;
        if (instance == null)
        {
            InventoryEventSystems.EnsureSingle();
            var go = new GameObject("Animal Pen Window");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AnimalPenWindow>();
            instance.Build();
        }
        if (instance.pen != p || instance.player != who)
        {
            instance.Detach();
            instance.pen = p;
            instance.player = who;
            p.Answered += instance.OnAnswer;
            p.Changed += instance.OnChanged;
            instance.Forget();
            instance.message.text = "";
        }
        instance.canvas.gameObject.SetActive(true);
        instance.Register();
        instance.AskForPicture();
        instance.ReadCarried();
        instance.Refresh();
    }

    public static void Close(AnimalPen p)
    {
        if (instance != null && instance.pen == p && p != null) instance.Hide();
    }

    /// <summary>A new pen or player: nothing chosen, nothing remembered of the last one's answers.</summary>
    private void Forget()
    {
        selected = "";
        slaughterArmed = "";
        page = Page.Overview;
        closeLook.Clear();
        examined.Clear();
        history.Clear();
        closeLookOf = examinedOf = historyOf = historyTitle = "";
        ResetSignatures();
        MarkPage();
    }

    private void ResetSignatures() => listSignature = carriedSignature = careSignature = medicineSignature = partnerSignature = "";

    private void OnDestroy()
    {
        Hide();
        if (instance == this) instance = null;
    }

    /// <summary>Where the rules run the picture is made here; on a client the pen is watched (the server sends it).</summary>
    private void AskForPicture()
    {
        if (pen == null || player == null) return;
        if (AnimalPen.RulesHere) pen.ReceiveView(AnimalDirector.ViewFor(pen, player));
        else AnimalNet.Watch(pen.PenId, true);
    }

    private void Update()
    {
        if (pen == null || player == null || !pen.isActiveAndEnabled || !player.isActiveAndEnabled)
        {
            Hide();
            return;
        }
        if (pen.DistanceFromOutline(player.transform.position) > pen.Rules.workRange + pen.extraReach + 1.5f)
        {
            Hide();
            return;
        }
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        SetHudFocus(true);
        float dt = Time.unscaledDeltaTime;
        refreshTimer -= dt;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 1f;
            Set(subtitle, Subtitle());
            if (AnimalPen.RulesHere) pen.ReceiveView(AnimalDirector.ViewFor(pen, player));
        }
        listTimer -= dt;
        if (listTimer <= 0f)
        {
            listTimer = 0.5f;
            ReadCarried();
            Refresh();
        }
        if (slaughterArmed.Length > 0 && Time.unscaledTime - slaughterArmedAt > ArmSeconds)
        {
            slaughterArmed = "";
            Refresh();
        }
        // A two-click action not confirmed in time goes back to its first label.
        if (armedKey.Length > 0 && Time.unscaledTime - armedAt > ArmSeconds)
        {
            armedKey = "";
            Refresh();
        }
        if (waiting && Time.unscaledTime - waitingSince > 5f)
        {
            waiting = false;
            Message("No answer came (it may still have been done).");
            Refresh();
        }
        if (messageTime > 0f && Time.unscaledTime - messageTime > 8f)
        {
            message.text = "";
            messageTime = 0f;
        }
    }

    private void SetHudFocus(bool on)
    {
        if (on == hudFocus) return;
        hudFocus = on;
        if (on) HudFocus.Take();
        else HudFocus.Release();
    }

    private void Hide()
    {
        Detach();
        SetHudFocus(false);
        Unregister();
        if (canvas != null && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
    }

    private void Detach()
    {
        if (pen != null)
        {
            pen.Answered -= OnAnswer;
            pen.Changed -= OnChanged;
        }
        pen = null;
        player = null;
        waiting = false;
    }

    private void Register()
    {
        InputBindingStore.BlockGameplayInput(this);
        if (registeredWith != null || managesCursor) return;
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

    public void ClosePanel() => Hide();

    // ------------------------------------------------------------------ building
    private void Build()
    {
        canvas = UITheme.Canvas("Animal Pen Window", Order, transform);
        Transform c = canvas.transform;
        Image backdrop = UITheme.Panel("Backdrop", c, new Color(0f, 0f, 0f, 0.35f), 0);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        Image window = UITheme.Window("Window", c, new Vector2(1500f, 900f));
        Transform w = window.transform;

        title = UITheme.Label(w, "Pen", 30f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.7f, 1f), new Vector2(28f, -60f), new Vector2(0f, -16f));
        subtitle = UITheme.Label(w, "", 16f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Subtitle");
        UITheme.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0.8f, 1f), new Vector2(28f, -88f), new Vector2(0f, -60f));
        RectTransform header = UITheme.Area("Header Buttons", w, new Vector2(0.7f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(-24f, -22f));
        InventoryUIFactory.Horizontal(header.gameObject, 10f).childAlignment = TextAnchor.MiddleRight;
        UITheme.Button("Close", header, "Close", Hide, UITheme.ButtonStyle.Normal, 120f, 42f);

        // Left: the animals.
        RectTransform left = Column(w, "Animals", 24f, 444f);
        UITheme.Header(left, "Animals here");
        animalList = UITheme.ScrollList("Animal List", left, 440f, out _);
        UITheme.Header(left, "Yours elsewhere");
        elsewhereList = UITheme.ScrollList("Elsewhere List", left, 200f, out _);

        // Middle: the animal, in pages.
        RectTransform mid = Column(w, "Animal", 460f, 1000f);
        UITheme.Header(mid, "The animal");
        RectTransform tabs = UITheme.Row("Pages", mid, 36f, 6f);
        pageButtons = new Button[PageNames.Length];
        pages = new RectTransform[PageNames.Length];
        for (int i = 0; i < PageNames.Length; i++)
        {
            Page p = (Page)i;
            pageButtons[i] = UITheme.Button("Page " + PageNames[i], tabs, PageNames[i], () => ShowPage(p), UITheme.ButtonStyle.Tab, 84f, 34f);
        }
        RectTransform area = InventoryUIFactory.Rect("Page Area", mid);
        InventoryUIFactory.Size(area, -1f, PageHeight);
        for (int i = 0; i < PageNames.Length; i++)
        {
            pages[i] = UITheme.Column(PageNames[i], area, 0, 8f);
            InventoryUIFactory.Stretch(pages[i]);
        }
        BuildOverview(pages[(int)Page.Overview]);
        BuildCare(pages[(int)Page.Care]);
        BuildHealth(pages[(int)Page.Health]);
        BuildHistory(pages[(int)Page.History]);
        BuildFamily(pages[(int)Page.Family]);
        BuildPen(pages[(int)Page.Pen]);
        MarkPage();
        UITheme.Header(mid, "Name");
        RectTransform nameRow = UITheme.Row("Name Row", mid, 44f, 8f);
        nameInput = UITheme.Input("Name", nameRow, "A name for it (or for the next one you let out)...", 24, 42f);
        InventoryUIFactory.Size(nameInput, -1f, 42f, 1f);
        renameButton = UITheme.Button("Rename", nameRow, "Name it", () => Do(new AnimalRequest { kind = AnimalActionKind.Rename, animal = selected, text = nameInput.text }), UITheme.ButtonStyle.Normal, 110f, 42f);

        // Right: the pen.
        RectTransform right = Column(w, "Pen", 1016f, 1476f);
        UITheme.Header(right, "The pen");
        penText = UITheme.Label(right, "", 14f, UITheme.Muted);
        InventoryUIFactory.Size(penText, -1f, 150f);
        RectTransform waterRow = UITheme.Row("Water Row", right, 40f, 8f);
        waterButton = UITheme.Button("Water", waterRow, "Fill the trough", () => Do(new AnimalRequest { kind = AnimalActionKind.FillWater }), UITheme.ButtonStyle.Normal, 0f, 40f);
        scrubButton = UITheme.Button("Scrub", waterRow, "Scrub the trough", () => Do(new AnimalRequest { kind = AnimalActionKind.CleanPen, text = AnimalCommands.TroughText }), UITheme.ButtonStyle.Normal, 0f, 40f);
        RectTransform muckRow = UITheme.Row("Muck Row", right, 40f, 8f);
        Middle(muckRow);
        muckButton = UITheme.Button("Muck Out", muckRow, "Muck out", MuckOut, UITheme.ButtonStyle.Normal, 0f, 40f);
        strawToggle = UITheme.BareToggle("Straw", muckRow, withStraw);
        strawToggle.onValueChanged.AddListener(on => withStraw = on);
        strawLabel = UITheme.Label(muckRow, "with straw", 14f, UITheme.Muted);
        InventoryUIFactory.Size(strawLabel, 160f, 36f, 0f);
        // What waits in the pen to be collected (eggs, milk, wool), from the animals the player may collect from.
        collectRow = UITheme.Row("Collect Row", right, 40f, 8f);
        Middle(collectRow);
        collectButton = UITheme.Button("Collect All", collectRow, "Collect all", () => Do(new AnimalRequest { kind = AnimalActionKind.Collect }), UITheme.ButtonStyle.Primary, 150f, 40f);
        collectLabel = UITheme.Label(collectRow, "", 13f, UITheme.Muted);
        InventoryUIFactory.Size(collectLabel, -1f, 38f, 1f);
        UITheme.Header(right, "Food you carry (feed the animal / stock the feeder)");
        RectTransform counts = UITheme.Row("Stock Count", right, 36f, 6f);
        UITheme.Label(counts, "Stock:", 14f, UITheme.Muted);
        int[] amounts = { 1, 5, 20 };
        stockButtons = new Button[amounts.Length];
        for (int i = 0; i < amounts.Length; i++)
        {
            int n = amounts[i];
            stockButtons[i] = UITheme.Button("Stock " + n, counts, "x" + n, () => { stockCount = n; MarkStock(); }, UITheme.ButtonStyle.Tab, 70f, 34f);
        }
        MarkStock();
        carriedFood = UITheme.ScrollList("Carried Food", right, 150f, out _);
        UITheme.Header(right, "Young animals you carry");
        carriedLivestock = UITheme.ScrollList("Carried Livestock", right, 118f, out _);

        message = UITheme.Label(w, "", 15f, UITheme.Gold, TextAlignmentOptions.Center, FontStyles.Normal, "Message");
        UITheme.Place(message.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 14f), new Vector2(-24f, 54f));
    }

    /// <summary>The Overview page: what is seen of the animal; a close look; slaughter, butchering, clearing the remains.</summary>
    private void BuildOverview(RectTransform p)
    {
        RectTransform textArea = UITheme.ScrollList("Animal Text", p, 498f, out _);
        animalText = UITheme.Label(textArea, "Choose an animal.", 15f, UITheme.Text);
        RectTransform row = UITheme.Row("Animal Row", p, 42f, 8f);
        lookButton = UITheme.Button("Look", row, "Look closely", () => Do(new AnimalRequest { kind = AnimalActionKind.Inspect, animal = selected }), UITheme.ButtonStyle.Normal, 0f, 40f);
        slaughterButton = UITheme.Button("Slaughter", row, "Slaughter", Slaughter, UITheme.ButtonStyle.Danger, 0f, 40f);
        butcherButton = UITheme.Button("Butcher", row, "Butcher", () => Do(new AnimalRequest { kind = AnimalActionKind.Butcher, animal = selected }), UITheme.ButtonStyle.Danger, 0f, 40f);
        remainsButton = UITheme.Button("Remains", row, "Clear the remains", () => Do(new AnimalRequest { kind = AnimalActionKind.RemoveRemains, animal = selected }), UITheme.ButtonStyle.Danger, 0f, 40f);
        crateButton = UITheme.Button("Crate", row, "Crate it", CrateIt, UITheme.ButtonStyle.Normal, 0f, 40f);
    }

    /// <summary>The Care page: a card per care action of its species (filled from the picture), and a careful look.</summary>
    private void BuildCare(RectTransform p)
    {
        careList = UITheme.ScrollList("Care List", p, 498f, out _);
        RectTransform row = UITheme.Row("Care Row", p, 42f, 8f);
        careExamineButton = UITheme.Button("Look It Over", row, "Look it over", Examine, UITheme.ButtonStyle.Normal, 0f, 40f);
    }

    /// <summary>The Health page: what ails it and shows, the medicines carried, a careful look and the veterinarian.</summary>
    private void BuildHealth(RectTransform p)
    {
        RectTransform textArea = UITheme.ScrollList("Health Text", p, 250f, out _);
        healthText = UITheme.Label(textArea, "", 15f, UITheme.Text);
        UITheme.Header(p, "Medicine you carry");
        medicineList = UITheme.ScrollList("Medicines", p, 198f, out _);
        RectTransform row = UITheme.Row("Health Row", p, 42f, 8f);
        examineButton = UITheme.Button("Look Over", row, "Look it over", Examine, UITheme.ButtonStyle.Normal, 0f, 40f);
        vetButton = UITheme.Button("Vet", row, "Send for the vet", CallVet, UITheme.ButtonStyle.Primary, 0f, 40f);
    }

    /// <summary>The History page: asked for when wanted (it is not sent with every picture).</summary>
    private void BuildHistory(RectTransform p)
    {
        RectTransform row = UITheme.Row("History Row", p, 42f, 8f);
        historyButton = UITheme.Button("Show History", row, "Show its history", () => Do(new AnimalRequest { kind = AnimalActionKind.History, animal = selected }), UITheme.ButtonStyle.Normal, 0f, 40f);
        RectTransform textArea = UITheme.ScrollList("History Text", p, 498f, out _);
        historyText = UITheme.Label(textArea, "", 14f, UITheme.Text);
    }

    /// <summary>The Family page: its bond with its owner and what it brought, its parents and young, its companions, its breeding in words, and the partners it could be bred with.</summary>
    private void BuildFamily(RectTransform p)
    {
        RectTransform textArea = UITheme.ScrollList("Family Text", p, 300f, out _);
        familyText = UITheme.Label(textArea, "", 14f, UITheme.Text);
        UITheme.Header(p, "Breed it with");
        partnerList = UITheme.ScrollList("Partners", p, 148f, out _);
        RectTransform row = UITheme.Row("Family Row", p, 42f, 8f);
        callButton = UITheme.Button("Call", row, "Call it", CallIt, UITheme.ButtonStyle.Normal, 0f, 40f);
    }

    /// <summary>
    /// The Pen page (Phase 5): the pen's level and what the next one does, its machines (a feed hopper, an automatic
    /// waterer, egg trays), its upkeep - and the buttons to improve it, fit or take out a machine, and pay what it owes.
    /// </summary>
    private void BuildPen(RectTransform p)
    {
        upgradesText = UITheme.Label(p, "", 15f, UITheme.Text);
        InventoryUIFactory.Size(upgradesText, -1f, 300f);
        RectTransform row = UITheme.Row("Pen Row", p, 40f, 8f);
        levelButton = UITheme.Button("Improve", row, "Improve", Improve, UITheme.ButtonStyle.Primary, 0f, 40f);
        payButton = UITheme.Button("Pay Upkeep", row, "Pay upkeep", () => PayUpkeep(false), UITheme.ButtonStyle.Normal, 0f, 40f);
        aheadButton = UITheme.Button("Pay Ahead", row, "Pay a week ahead", () => PayUpkeep(true), UITheme.ButtonStyle.Normal, 0f, 40f);
        RectTransform machines = UITheme.Row("Machines Row", p, 40f, 8f);
        hopperButton = UITheme.Button("Hopper", machines, "Hopper", () => Machine(AnimalDevice.Hopper), UITheme.ButtonStyle.Normal, 0f, 40f);
        watererButton = UITheme.Button("Waterer", machines, "Waterer", () => Machine(AnimalDevice.Waterer), UITheme.ButtonStyle.Normal, 0f, 40f);
        traysButton = UITheme.Button("Trays", machines, "Egg trays", () => Machine(AnimalDevice.Trays), UITheme.ButtonStyle.Normal, 0f, 40f);
    }

    private static RectTransform Column(Transform window, string name, float x0, float x1)
    {
        Image bg = UITheme.Panel(name, window, UITheme.Card, UITheme.Radius);
        UITheme.Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(x0, 60f), new Vector2(x1, -100f));
        RectTransform col = UITheme.Column("Column", bg.transform, 12, 8f);
        InventoryUIFactory.Stretch(col);
        return col;
    }

    /// <summary>A row whose children keep their own heights, centred (a switch beside a button).</summary>
    private static void Middle(RectTransform row)
    {
        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        if (h == null) return;
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
    }

    private void MarkStock()
    {
        int[] amounts = { 1, 5, 20 };
        for (int i = 0; i < stockButtons.Length; i++) UITheme.SetSelected(stockButtons[i], amounts[i] == stockCount);
    }

    private void ShowPage(Page p)
    {
        page = p;
        MarkPage();
        Refresh();
    }

    private void MarkPage()
    {
        if (pages == null || pageButtons == null) return;
        for (int i = 0; i < pages.Length; i++)
        {
            bool on = i == (int)page;
            if (pages[i].gameObject.activeSelf != on) pages[i].gameObject.SetActive(on);
            UITheme.SetSelected(pageButtons[i], on);
        }
    }

    private static void Clear(RectTransform list)
    {
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
    }

    // ------------------------------------------------------------------ showing
    private void OnChanged() => Refresh();

    private string Subtitle()
    {
        AnimalPenView v = pen != null ? pen.View : null;
        string date = FarmClock.Date.Full();
        if (v == null) return date;
        return $"{date} · {v.kind} · {v.area:0} m², {v.spaceUsed:0} m² taken";
    }

    private void Refresh()
    {
        if (pen == null) return;
        Set(title, pen.DisplayName);
        Set(subtitle, Subtitle());
        AnimalPenView v = pen.View;
        if (v == null)
        {
            Set(penText, "Looking over the fence...");
            Set(animalText, "");
            return;
        }
        AnimalRights rights = (AnimalRights)v.rights;
        ShowPen(v, rights);
        ShowList(v, rights);
        AnimalView a = Find(v, selected, out bool here);
        // Only the page shown is kept up to date (another is brought up to date when it is shown).
        switch (page)
        {
            case Page.Overview: ShowOverview(v, a, here, rights); break;
            case Page.Care: ShowCare(v, a, here, rights); break;
            case Page.Health: ShowHealth(v, a, here, rights); break;
            case Page.History: ShowHistory(a, here, rights); break;
            case Page.Family: ShowFamily(a, here); break;
            case Page.Pen: ShowPenPage(v); break;
        }
        renameButton.interactable = a != null && here && a.mine && a.status != AnimalStatus.Dead && !waiting;
        RefreshCarried(a, here, rights);
    }

    /// <summary>The chosen animal: one in this pen (<paramref name="here"/>), or one of the player's own elsewhere; null: none.</summary>
    private static AnimalView Find(AnimalPenView v, string id, out bool here)
    {
        here = false;
        if (v == null || string.IsNullOrEmpty(id)) return null;
        foreach (AnimalView a in v.animals)
            if (a != null && a.id == id)
            {
                here = true;
                return a;
            }
        foreach (AnimalView a in v.elsewhere)
            if (a != null && a.id == id) return a;
        return null;
    }

    private void Choose(string id)
    {
        if (selected != id) slaughterArmed = "";
        selected = id ?? "";
        Refresh();
    }

    // ------------------------------------------------------------------ the pen
    private void ShowPen(AnimalPenView v, AnimalRights rights)
    {
        bool tend = (rights & AnimalRights.Tend) != 0;
        AnimalToolUse fork = BestTool(AnimalCommands.MuckOutTools);
        var sb = new StringBuilder();
        Line(sb, v.feeder);
        Line(sb, v.water);
        Line(sb, v.grass);
        Line(sb, Join(v.cleanliness, v.beddingState));
        Line(sb, Join(v.shelter, v.weather));
        if (!tend) Line(sb, "<color=#c9a86a>You may look, not tend: this pen is not yours.</color>");
        else if (fork.has) Line(sb, $"<color={Muted}>You can muck it out with your {ToolName(fork)}{(fork.edge <= 0f ? " (worn out)" : "")}.</color>");
        else Line(sb, $"<color={Muted}>{Capital(AnimalCare.ToolNames(AnimalDatabase.Instance, AnimalCommands.MuckOutTools))} mucks it out: you carry neither.</color>");
        Set(penText, sb.ToString());

        waterButton.interactable = tend && !waiting;
        Visible(scrubButton, v.scrubReady);
        scrubButton.interactable = !waiting;
        muckButton.interactable = tend && v.cleanReady && fork.has && !waiting;
        Carry straw = SupplyFor(BeddingTag);
        bool strawShown = tend && v.usesBedding && straw != null;
        Visible(strawToggle, strawShown);
        Visible(strawLabel, strawShown);
        if (strawShown) Set(strawLabel, $"with {straw.name.ToLowerInvariant()} ({straw.count})");
        Visible(collectRow, v.collectReady);
        if (v.collectReady) Set(collectLabel, "Waiting: " + v.productsWaiting);
        collectButton.interactable = !waiting;
    }

    /// <summary>Mucking out, laying fresh straw when it is carried, the pen is bedded and the player wants it.</summary>
    private void MuckOut()
    {
        AnimalPenView v = pen != null ? pen.View : null;
        Carry straw = SupplyFor(BeddingTag);
        bool lay = withStraw && straw != null && v != null && v.usesBedding;
        Do(new AnimalRequest { kind = AnimalActionKind.CleanPen, item = lay ? straw.key : "" });
    }

    // ------------------------------------------------------------------ the animals (left)
    private void ShowList(AnimalPenView v, AnimalRights rights)
    {
        var sig = new StringBuilder();
        foreach (AnimalView a in v.animals)
            if (a != null) sig.Append(a.id).Append(a.summary).Append(a.stage).Append(a.name).Append(a.status).Append(a.looks).Append('|');
        foreach (AnimalView a in v.elsewhere)
            if (a != null) sig.Append(a.id).Append(a.status).Append(a.name).Append('#');
        sig.Append(selected).Append((int)rights);
        string signature = sig.ToString();
        if (signature == listSignature) return;
        listSignature = signature;

        Clear(animalList);
        if (v.animals.Count == 0) UITheme.Label(animalList, "No animals live here.", 14f, UITheme.Muted);
        foreach (AnimalView a in v.animals)
        {
            if (a == null) continue;
            string id = a.id;
            string looks = LooksWords(a.looks);
            string label = $"{a.name} - {a.speciesName.ToLowerInvariant()}, {AnimalSim.Stage(a.stage)}\n<size=12><color={Muted}>{a.summary}{(looks.Length > 0 ? " · " + looks : "")}</color></size>";
            Button b = UITheme.Button("Animal " + id, animalList, label, () => Choose(id), UITheme.ButtonStyle.ListItem, 0f, 58f);
            UITheme.SetSelected(b, id == selected);
        }

        Clear(elsewhereList);
        bool keep = (rights & AnimalRights.Keep) != 0;
        if (v.elsewhere.Count == 0) UITheme.Label(elsewhereList, keep ? "None." : "You cannot keep animals here.", 13f, UITheme.Muted);
        foreach (AnimalView a in v.elsewhere)
        {
            if (a == null) continue;
            string id = a.id;
            string where = a.status == AnimalStatus.Dead ? "died boarded" : a.status == AnimalStatus.Boarded ? "boarded" : "in another pen";
            RectTransform row = UITheme.Row("Elsewhere " + id, elsewhereList, 40f, 6f);
            Button pick = UITheme.Button("Choose " + id, row, $"<size=14>{a.name} ({a.speciesName.ToLowerInvariant()}, {where})</size>", () => Choose(id), UITheme.ButtonStyle.ListItem, 0f, 38f);
            UITheme.SetSelected(pick, id == selected);
            if (a.status == AnimalStatus.Dead)
                UITheme.Button("Clear " + id, row, "Clear", () => Do(new AnimalRequest { kind = AnimalActionKind.RemoveRemains, animal = id }), UITheme.ButtonStyle.Danger, 80f, 38f);
            else if (keep)
                UITheme.Button("Bring " + id, row, "Bring here", () => Do(new AnimalRequest { kind = AnimalActionKind.Move, animal = id }), UITheme.ButtonStyle.Normal, 110f, 38f);
        }
    }

    /// <summary>How its body looks, in a few words for the list ("looks sick, lame").</summary>
    private static string LooksWords(int flags)
    {
        var l = (AnimalLooks)flags;
        var parts = new List<string>(3);
        if ((l & AnimalLooks.Sick) != 0) parts.Add("looks sick");
        if ((l & AnimalLooks.Lame) != 0) parts.Add("lame");
        if ((l & AnimalLooks.Thin) != 0) parts.Add("thin");
        if ((l & AnimalLooks.Dirty) != 0) parts.Add("dirty");
        if ((l & AnimalLooks.Stressed) != 0) parts.Add("on edge");
        if (parts.Count == 0 && (l & AnimalLooks.Content) != 0) parts.Add("thriving");
        return string.Join(", ", parts);
    }

    // ------------------------------------------------------------------ Overview
    private void ShowOverview(AnimalPenView v, AnimalView a, bool here, AnimalRights rights)
    {
        bool alive = a != null && a.status != AnimalStatus.Dead;
        bool stable = AtStable(a, here);
        bool mine = a != null && a.mine;
        lookButton.interactable = a != null && (here || stable) && !waiting;

        // The work that cannot be undone: its owner's slaughter of it, butchering or clearing what is left of it.
        bool slaughter = alive && here && mine && pen.Rules.allowSlaughter;
        Visible(slaughterButton, slaughter);
        if (slaughter)
        {
            bool armed = slaughterArmed == a.id && Time.unscaledTime - slaughterArmedAt <= ArmSeconds;
            SetLabel(slaughterButton, armed ? "Click again to slaughter" : "Slaughter");
            slaughterButton.interactable = !waiting;
        }
        bool remains = a != null && !alive && (here || stable);
        bool butcher = remains && mine && Butcherable(a) && (here || (rights & AnimalRights.Keep) != 0);
        Visible(butcherButton, butcher);
        butcherButton.interactable = !waiting;
        Visible(remainsButton, remains && (mine || (here && (rights & AnimalRights.CareForOthers) != 0)));
        remainsButton.interactable = !waiting;
        // Crating it, to sell or trade it (its owner's, while it is fit to travel): two clicks, as it leaves the pen.
        bool crate = alive && here && mine;
        Visible(crateButton, crate);
        if (crate)
        {
            SetLabel(crateButton, Armed("crate:" + a.id) ? "Click again to crate it" : "Crate it");
            crateButton.interactable = a.crateReady && !waiting;
        }

        if (a == null)
        {
            Set(animalText, v.animals.Count > 0 || v.elsewhere.Count > 0 ? "Choose an animal." : "");
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{a.name}</b>");
        sb.AppendLine($"{(string.IsNullOrEmpty(a.breedName) ? "" : a.breedName + " ")}{a.speciesName.ToLowerInvariant()}, {AnimalCommands.Sex(a.sex)}, {AnimalSim.Stage(a.stage)}, {AnimalObservation.Age(a.ageDays * 24.0)} old");
        sb.AppendLine(a.mine ? "Yours." : $"Kept by {(string.IsNullOrEmpty(a.ownerName) ? "someone else" : a.ownerName)}.");
        if (!here)
            sb.AppendLine($"<color={Muted}>{(a.status == AnimalStatus.Dead ? "It died at the boarding stable." : a.status == AnimalStatus.Boarded ? "It is at the boarding stable: its health can be seen to from here." : "It lives in another of your pens.")}</color>");
        sb.AppendLine();
        sb.AppendLine(a.summary + ".");
        if (alive)
        {
            sb.AppendLine();
            GaugeLine(sb, "Fed", a.hunger);
            GaugeLine(sb, "Watered", a.thirst);
            GaugeLine(sb, "Health", a.health);
            GaugeLine(sb, "Spirits", a.mood);
            GaugeLine(sb, "Condition", a.condition);
            GaugeLine(sb, "Clean", a.hygiene);
            GaugeLine(sb, "Comfort", a.comfort);
            GaugeLine(sb, "Calm", a.calm);
            if (a.hooves >= 0) GaugeLine(sb, "Hooves", a.hooves);
            GaugeLine(sb, "Welfare", a.welfare, a.welfareWord);
            if (a.bond >= 0) GaugeLine(sb, "Bond", a.bond, a.bondWord);
            if (!string.IsNullOrEmpty(a.expecting)) sb.AppendLine($"<color={Note}>{Sentence(a.expecting)}</color>");
            if (a.mine && here && a.grade >= 0)
                sb.AppendLine($"<color={Muted}>In trade: {a.worthWord} stock, worth about x{a.worth:0.0} a young one.{(a.crateReady || string.IsNullOrEmpty(a.crateHint) ? "" : " " + a.crateHint)}</color>");
        }
        if (a.signs != null && a.signs.Count > 0)
        {
            sb.AppendLine();
            foreach (string s in a.signs) sb.AppendLine("- " + s);
        }
        if (closeLookOf == a.id && closeLook.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"<color={Muted}>A close look:</color>");
            foreach (string s in closeLook) sb.AppendLine(s);
        }
        Set(animalText, sb.ToString());
    }

    /// <summary>Crating takes two clicks within a few seconds: the first only asks (the animal leaves the pen for a crate in the bag).</summary>
    private void CrateIt()
    {
        if (string.IsNullOrEmpty(selected)) return;
        if (!Confirm("crate:" + selected)) return;
        Do(new AnimalRequest { kind = AnimalActionKind.Crate, animal = selected });
    }

    // What a two-click action has armed (its key) and when: a second click on it within ArmSeconds goes ahead.
    private string armedKey = "";
    private float armedAt;

    private bool Armed(string key) => armedKey == key && Time.unscaledTime - armedAt <= ArmSeconds;

    /// <summary>True on the second click of a two-click action (within ArmSeconds); the first arms it and redraws the window.</summary>
    private bool Confirm(string key)
    {
        if (Armed(key))
        {
            armedKey = "";
            return true;
        }
        armedKey = key;
        armedAt = Time.unscaledTime;
        Refresh();
        return false;
    }

    /// <summary>Slaughter takes two clicks within a few seconds: the first only asks.</summary>
    private void Slaughter()
    {
        if (string.IsNullOrEmpty(selected)) return;
        if (slaughterArmed != selected || Time.unscaledTime - slaughterArmedAt > ArmSeconds)
        {
            slaughterArmed = selected;
            slaughterArmedAt = Time.unscaledTime;
            Message("Click Slaughter again to do it. It cannot be undone.");
            Refresh();
            return;
        }
        slaughterArmed = "";
        Do(new AnimalRequest { kind = AnimalActionKind.Slaughter, animal = selected });
    }

    /// <summary>Can its remains be butchered (its species has a Harvesting anatomy)?</summary>
    private static bool Butcherable(AnimalView a)
    {
        AnimalSpeciesData sp = a != null ? AnimalDatabase.Instance.Species(a.species) : null;
        return sp != null && !string.IsNullOrWhiteSpace(sp.harvestAnatomy);
    }

    /// <summary>One of the player's own at the boarding stable (living, or its remains), seen from another pen's window.</summary>
    private static bool AtStable(AnimalView a, bool here) => a != null && !here && (a.status == AnimalStatus.Boarded || a.status == AnimalStatus.Dead);

    private static void GaugeLine(StringBuilder sb, string label, int level, string word = null)
    {
        sb.Append(label).Append("<pos=24%>").Append(Gauge(level));
        if (!string.IsNullOrEmpty(word)) sb.Append("  ").Append(word);
        sb.AppendLine();
    }

    private static string Gauge(int level)
    {
        level = Mathf.Clamp(level, 0, 4);
        string col = level >= 3 ? "#7fd18c" : level == 2 ? "#e2c46a" : "#ef7b6e";
        return $"<color={col}>{new string('■', level + 1)}</color><color=#3a4150>{new string('■', 4 - level)}</color>";
    }

    // ------------------------------------------------------------------ Care
    private void ShowCare(AnimalPenView v, AnimalView a, bool here, AnimalRights rights)
    {
        bool alive = a != null && a.status != AnimalStatus.Dead;
        bool carer = a != null && (a.mine || (rights & AnimalRights.CareForOthers) != 0);
        careExamineButton.interactable = alive && carer && (here || AtStable(a, here)) && !waiting;
        string blocked = CareBlocked(a, here, carer);
        bool products = ProductsShown(a, here, carer);
        int living = Living(v);
        var sig = new StringBuilder();
        sig.Append(a != null ? a.id : "").Append('|').Append(blocked).Append('|').Append(waiting).Append(living > 1).Append(products).Append('|');
        if (products)
            foreach (AnimalProductView pv in a.products)
            {
                if (pv == null) continue;
                AnimalToolUse use = BestTool(pv.tools);
                sig.Append(pv.id).Append(pv.waiting).Append(pv.tier).Append(pv.status).Append(pv.progress).Append(pv.toolText)
                    .Append(use.has).Append(use.tool).Append(use.edge <= 0f).Append(use.edge < 0.2f).Append(';');
            }
        sig.Append('#');
        if (blocked == null)
            foreach (AnimalCareView c in a.care)
            {
                if (c == null) continue;
                AnimalToolUse use = BestTool(c.tools);
                sig.Append(c.id).Append(c.ready).Append(c.hint).Append(c.waterLitres.ToString("0.#")).Append(c.toolText)
                    .Append(use.has).Append(use.tool).Append(use.edge <= 0f).Append(use.edge < 0.2f);
                Carry supply = SupplyFor(c.supply);
                if (supply != null) sig.Append(supply.key).Append(supply.count);
                sig.Append(';');
            }
        sig.Append(carried.water.ToString("0.#"));
        string signature = sig.ToString();
        if (signature == careSignature) return;
        careSignature = signature;
        Clear(careList);
        if (products)
        {
            UITheme.Header(careList, "What it gives");
            foreach (AnimalProductView pv in a.products)
                if (pv != null) ProductCard(pv, here && living > 1);
            UITheme.Header(careList, "Its care");
        }
        if (blocked != null)
        {
            UITheme.Label(careList, blocked, 14f, UITheme.Muted);
            return;
        }
        foreach (AnimalCareView c in a.care)
            if (c != null) CareCard(c, living > 1);
    }

    /// <summary>
    /// Whether what it gives is shown: a living animal its viewer may collect from (its owner or a carer of all), here or
    /// its owner's at the boarding stable (what it made before it went there waits for its owner), that makes something.
    /// </summary>
    private static bool ProductsShown(AnimalView a, bool here, bool carer) =>
        a != null && a.status != AnimalStatus.Dead && carer && (here || (a.mine && a.status == AnimalStatus.Boarded)) && a.products != null && a.products.Count > 0;

    /// <summary>
    /// One product: its button (collect it from this animal: "Collect", "Milk", "Shear"), "From all here" (every animal in
    /// the pen with some waiting), how much waits and of what quality, how it is making it and how far the next lot is, and
    /// the tool collecting it takes and whether the player carries one.
    /// </summary>
    private void ProductCard(AnimalProductView pv, bool herd)
    {
        AnimalToolUse use = BestTool(pv.tools);
        bool tool = pv.tools == null || pv.tools.Count == 0 || use.has;
        RectTransform card = UITheme.CardColumn("Product " + pv.id, careList, 8, 4f);
        RectTransform row = UITheme.Row("Buttons", card, 38f, 6f);
        bool can = pv.ready && tool;
        string verb = string.IsNullOrEmpty(pv.verb) ? "Collect" : pv.verb;
        string label = pv.ready ? $"{verb}: {pv.waitingText}{(string.IsNullOrEmpty(pv.tier) ? "" : " (" + pv.tier + ")")}" : $"{verb} ({pv.name.ToLowerInvariant()})";
        string id = pv.id;
        Button one = UITheme.Button("Collect", row, label, () => Collect(id, false), can ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Normal, 0f, 36f);
        one.interactable = can && !waiting;
        if (herd)
        {
            Button all = UITheme.Button("All", row, "From all here", () => Collect(id, true), UITheme.ButtonStyle.Normal, 150f, 36f);
            all.interactable = tool && !waiting;
        }
        UITheme.Label(card, ProductStatus(pv, use), 13f, UITheme.Muted);
    }

    /// <summary>How a product is coming, in words: how it is making it, how far the next lot is, the tool collecting it takes.</summary>
    private string ProductStatus(AnimalProductView pv, AnimalToolUse use)
    {
        var sb = new StringBuilder();
        sb.Append(pv.name).Append(": ");
        sb.Append(string.IsNullOrEmpty(pv.status) ? "" : Sentence(pv.status) + " ");
        if (!pv.ready || pv.status == "making it") sb.Append("Next: ").Append(Gauge(pv.progress)).Append('.');
        if (pv.tools != null && pv.tools.Count > 0)
        {
            if (!use.has) sb.Append($" <color={Warn}>It takes {(string.IsNullOrEmpty(pv.toolText) ? "a tool" : pv.toolText)}: you carry none.</color>");
            else if (use.edge <= 0f) sb.Append($" With your {ToolName(use)} <color={Note}>(worn out: it is rough on the animal)</color>.");
            else if (use.edge < 0.2f) sb.Append($" With your {ToolName(use)} (nearly worn out).");
            else sb.Append($" With your {ToolName(use)}.");
        }
        else sb.Append(" By hand.");
        return sb.ToString();
    }

    /// <summary>Collecting a product: from the chosen animal, or from every animal here that has some waiting.</summary>
    private void Collect(string product, bool all) =>
        Do(new AnimalRequest { kind = AnimalActionKind.Collect, animal = all ? "" : selected, text = product ?? "" });

    /// <summary>Why the chosen animal cannot be cared for from here (null: it can).</summary>
    private static string CareBlocked(AnimalView a, bool here, bool carer)
    {
        if (a == null) return "Choose an animal.";
        if (a.status == AnimalStatus.Dead) return "It is dead.";
        if (!here) return a.status == AnimalStatus.Boarded ? "The boarding stable grooms and sees to it." : "Care for it in its own pen.";
        if (!carer) return "It is not yours to care for.";
        if (a.care == null || a.care.Count == 0) return "Its kind takes no care of this sort.";
        return null;
    }

    /// <summary>
    /// One care action: its button (for this animal), "To all here" (every animal in the pen that can take it), what it
    /// needs and whether the player has it, and the switch for its supply (soap) when one is carried.
    /// </summary>
    private void CareCard(AnimalCareView c, bool herd)
    {
        AnimalToolUse use = BestTool(c.tools);
        bool tool = c.tools == null || c.tools.Count == 0 || use.has;
        RectTransform card = UITheme.CardColumn("Care " + c.id, careList, 8, 4f);
        RectTransform row = UITheme.Row("Buttons", card, 38f, 6f);
        bool can = c.ready && tool;
        Button one = UITheme.Button("Do", row, c.label, () => DoCare(c, false), can ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Normal, 0f, 36f);
        one.interactable = can && !waiting;
        if (herd)
        {
            Button all = UITheme.Button("All", row, "To all here", () => DoCare(c, true), UITheme.ButtonStyle.Normal, 130f, 36f);
            all.interactable = tool && !waiting;
        }
        UITheme.Label(card, CareStatus(c, use), 13f, UITheme.Muted);
        Carry supply = SupplyFor(c.supply);
        if (supply == null) return;
        RectTransform supplyRow = UITheme.Row("Supply", card, 30f, 8f);
        Middle(supplyRow);
        string tag = c.supply;
        Toggle on = UITheme.BareToggle("Use " + tag, supplyRow, SupplyOn(tag));
        on.onValueChanged.AddListener(v => withSupply[tag] = v);
        TextMeshProUGUI label = UITheme.Label(supplyRow, $"With {supply.name.ToLowerInvariant()} (you carry {supply.count})", 13f, UITheme.Text);
        InventoryUIFactory.Size(label, -1f, 28f, 1f);
    }

    /// <summary>What a care action needs, in words: ready or not, the tool (and which one the player would use), the water.</summary>
    private string CareStatus(AnimalCareView c, AnimalToolUse use)
    {
        var sb = new StringBuilder();
        sb.Append(c.ready ? "Ready." : Sentence(string.IsNullOrEmpty(c.hint) ? "not now" : c.hint));
        if (c.tools != null && c.tools.Count > 0)
        {
            if (!use.has) sb.Append($" <color={Warn}>It needs {(string.IsNullOrEmpty(c.toolText) ? "a tool" : c.toolText)}: you carry none.</color>");
            else if (use.edge <= 0f) sb.Append($" With your {ToolName(use)} <color={Note}>(worn out: it does a poor job)</color>.");
            else if (use.edge < 0.2f) sb.Append($" With your {ToolName(use)} (nearly worn out).");
            else sb.Append($" With your {ToolName(use)}.");
        }
        if (c.waterLitres > 0.001f)
        {
            string need = $"It takes about {c.waterLitres:0.#} L of water (you carry {carried.water:0.#} L).";
            bool enough = carried.water >= c.waterLitres * AnimalCare.LeastWater - 1e-4f;
            sb.Append(' ').Append(enough ? need : $"<color={Warn}>{need}</color>");
        }
        if (c.inappropriate) sb.Append($" <color={Note}>It does this animal no good.</color>");
        return sb.ToString();
    }

    private void DoCare(AnimalCareView c, bool all)
    {
        if (c == null) return;
        var r = new AnimalRequest { kind = AnimalActionKind.Care, animal = all ? "" : selected, text = c.id };
        Carry supply = SupplyFor(c.supply);
        if (supply != null && SupplyOn(c.supply)) r.item = supply.key;
        Do(r);
    }

    private bool SupplyOn(string tag) => !string.IsNullOrWhiteSpace(tag) && withSupply.TryGetValue(tag, out bool on) && on;

    // ------------------------------------------------------------------ Health
    private void ShowHealth(AnimalPenView v, AnimalView a, bool here, AnimalRights rights)
    {
        bool alive = a != null && a.status != AnimalStatus.Dead;
        bool carer = a != null && (a.mine || (rights & AnimalRights.CareForOthers) != 0);
        // A careful look, medicine and the vet: for one here, or its owner's at the boarding stable.
        bool reach = a != null && (here || AtStable(a, here));
        examineButton.interactable = alive && carer && reach && !waiting;
        // Whenever there is a veterinarian it may be sent for, a free one included (fee 0).
        bool vet = alive && carer && reach && a.vetCanCome;
        Visible(vetButton, vet);
        if (vet)
        {
            SetLabel(vetButton, $"Send for the vet ({(a.vetFee > 0 ? FeeText(a.vetFee) : "free")})");
            vetButton.interactable = !waiting;
        }
        Set(healthText, HealthText(a, alive, carer, reach));
        ShowMedicines(v, a, alive && carer && reach);
    }

    private string HealthText(AnimalView a, bool alive, bool carer, bool reach)
    {
        if (a == null) return "Choose an animal.";
        if (!alive) return $"<b>{a.name}</b>\n{a.summary}.";
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{a.name}</b>  <color={Muted}>{a.summary}.</color>");
        if (carer)
        {
            sb.AppendLine();
            sb.AppendLine("<b>What ails it</b>");
            if (a.conditions == null || a.conditions.Count == 0) sb.AppendLine($"<color={Muted}>Nothing has been named. Look it over to find out what ails it.</color>");
            else
                foreach (AnimalConditionView k in a.conditions)
                    if (k != null) sb.AppendLine("- " + ConditionLine(k));
        }
        sb.AppendLine();
        sb.AppendLine("<b>What shows</b>");
        if (a.signs == null || a.signs.Count == 0) sb.AppendLine($"<color={Muted}>Nothing out of the ordinary.</color>");
        else
            foreach (string s in a.signs) sb.AppendLine("- " + s);
        if (examinedOf == a.id && examined.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>A careful look</b>");
            foreach (string s in examined) sb.AppendLine(s);
        }
        if (!reach)
        {
            sb.AppendLine();
            sb.AppendLine($"<color={Muted}>See to its health in its own pen.</color>");
        }
        else if (!carer)
        {
            sb.AppendLine();
            sb.AppendLine($"<color={Muted}>Only its keepers may treat it.</color>");
        }
        return sb.ToString();
    }

    /// <summary>"Infection (moderate), being treated. Only the veterinarian can turn it now."</summary>
    private static string ConditionLine(AnimalConditionView k)
    {
        var sb = new StringBuilder();
        sb.Append(Capital(k.name));
        if (!string.IsNullOrEmpty(k.stage)) sb.Append(" (").Append(k.stage.ToLowerInvariant()).Append(')');
        if (k.treated && !k.recovering) sb.Append(", being treated");
        sb.Append('.');
        if (k.needsVet) sb.Append($" <color={Warn}>Only the veterinarian can turn it now.</color>");
        return sb.ToString();
    }

    /// <summary>The medicines the player carries: "Give" (to the chosen animal) and, for a drench or a feed remedy, "Give to all here" (every animal in this pen the player may care for).</summary>
    private void ShowMedicines(AnimalPenView v, AnimalView a, bool canGive)
    {
        int living = Living(v);
        var sig = new StringBuilder();
        sig.Append(a != null ? a.id : "").Append(canGive).Append(waiting).Append(living > 0).Append('|');
        if (a?.conditions != null)
            foreach (AnimalConditionView k in a.conditions)
                if (k != null) sig.Append(k.id).Append(k.recovering);
        foreach (Carry m in carried.medicine.Values) sig.Append(m.key).Append(m.count).Append('|');
        string signature = sig.ToString();
        if (signature == medicineSignature) return;
        medicineSignature = signature;
        Clear(medicineList);
        if (carried.medicine.Count == 0)
        {
            UITheme.Label(medicineList, "You carry no medicine for animals (merchants of the keeper's goods sell it; potions can also be brewed).", 13f, UITheme.Muted);
            return;
        }
        foreach (Carry m in carried.medicine.Values)
        {
            string key = m.key;
            AnimalTreatmentData tr = m.treatment;
            RectTransform row = UITheme.Row("Medicine " + key, medicineList, 44f, 6f);
            TextMeshProUGUI label = UITheme.Label(row, $"{m.name} ×{m.count}\n<size=12><color={Muted}>{Use(tr, a)}</color></size>", 14f, UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 42f, 1f);
            Button give = UITheme.Button("Give", row, "Give", () => Do(new AnimalRequest { kind = AnimalActionKind.Treat, animal = selected, item = key }), UITheme.ButtonStyle.Normal, 80f, 40f);
            give.interactable = canGive && !waiting;
            if (tr == null || tr.route == AnimalTreatmentRoute.Topical) continue;
            Button all = UITheme.Button("Give All", row, "Give to all here", () => Do(new AnimalRequest { kind = AnimalActionKind.Treat, animal = "", item = key }), UITheme.ButtonStyle.Normal, 150f, 40f);
            all.interactable = living > 0 && !waiting;
        }
    }

    /// <summary>What a medicine is for, as far as the chosen animal goes ("For its infection."), else how it is given.</summary>
    private static string Use(AnimalTreatmentData tr, AnimalView a)
    {
        if (tr == null) return "";
        if (a?.conditions != null)
            foreach (AnimalConditionView k in a.conditions)
                if (k != null && !k.recovering && !string.IsNullOrEmpty(k.id) && tr.EffectOn(k.id) != null)
                    return $"For its {(string.IsNullOrEmpty(k.name) ? k.id : k.name).ToLowerInvariant()}.";
        switch (tr.route)
        {
            case AnimalTreatmentRoute.Topical: return "A wash or a salve, put on one animal at a time.";
            case AnimalTreatmentRoute.Feed: return "Given in the feed.";
            default: return "A drench, given by mouth.";
        }
    }

    private void Examine() => Do(new AnimalRequest { kind = AnimalActionKind.Examine, animal = selected });

    /// <summary>Sends for the veterinarian for the fee the picture shows (0: a free visit; a fee changed since is refused, and the new one shown).</summary>
    private void CallVet()
    {
        AnimalView a = Find(pen != null ? pen.View : null, selected, out _);
        if (a == null || !a.vetCanCome) return;
        Do(new AnimalRequest { kind = AnimalActionKind.CallVet, animal = a.id, expected = a.vetFee });
    }

    /// <summary>A fee in the veterinarian's money ("42 gold"; no vet known on this machine: the number).</summary>
    private static string FeeText(long fee)
    {
        AnimalVeterinarian vet = AnimalVeterinarian.Current;
        return vet != null ? vet.FeeText(fee) : fee.ToString();
    }

    // ------------------------------------------------------------------ History
    private void ShowHistory(AnimalView a, bool here, AnimalRights rights)
    {
        bool carer = a != null && (a.mine || (rights & AnimalRights.CareForOthers) != 0);
        // Its keepers read the history of one here; its owner that of their own wherever it is.
        bool may = a != null && (here ? carer : a.mine);
        bool shown = a != null && historyOf == a.id && history.Count > 0;
        historyButton.interactable = may && !waiting;
        SetLabel(historyButton, shown ? "Show it again" : "Show its history");
        if (a == null)
        {
            Set(historyText, "Choose an animal.");
            return;
        }
        if (!shown)
        {
            Set(historyText, may ? $"<color={Muted}>Its health, the care it had and how its last days went: ask to see them.</color>" : "Its history is for its keepers.");
            return;
        }
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(historyTitle)) sb.AppendLine($"<b>{historyTitle}</b>");
        foreach (string line in history)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (line.EndsWith(":", StringComparison.Ordinal) && !line.StartsWith("-", StringComparison.Ordinal))
            {
                sb.AppendLine();
                sb.AppendLine($"<color={Muted}><b>{line.TrimEnd(':')}</b></color>");
            }
            else sb.AppendLine(line);
        }
        Set(historyText, sb.ToString());
    }

    // ------------------------------------------------------------------ Family
    private void ShowFamily(AnimalView a, bool here)
    {
        bool alive = a != null && a.status != AnimalStatus.Dead;
        Visible(callButton, alive && here && a.comesWhenCalled);
        callButton.interactable = !waiting;
        if (a == null)
        {
            Set(familyText, "Choose an animal.");
            Partners(null, false);
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{a.name}</b>");
        if (!string.IsNullOrEmpty(a.expecting)) sb.AppendLine($"<color={Note}>{Sentence(a.expecting)}</color>");
        if (a.bond >= 0)
        {
            GaugeLine(sb, "Bond", a.bond, a.bondWord);
            if (a.perks != null && a.perks.Count > 0) sb.AppendLine($"<color={Muted}>Your bond has brought:</color> {string.Join(", ", a.perks)}.");
            else sb.AppendLine($"<color={Muted}>Care for it, feed it from your hand and spend time by its pen: a deeper bond brings more.</color>");
        }
        bool family = !string.IsNullOrEmpty(a.motherName) || !string.IsNullOrEmpty(a.fatherName) || a.generation > 0 || a.offspring > 0;
        if (family)
        {
            sb.AppendLine();
            if (!string.IsNullOrEmpty(a.motherName) || !string.IsNullOrEmpty(a.fatherName))
                sb.AppendLine($"Out of {(string.IsNullOrEmpty(a.motherName) ? "a dam" : a.motherName)} by {(string.IsNullOrEmpty(a.fatherName) ? "a sire" : a.fatherName)}.");
            if (a.generation > 0) sb.AppendLine($"Bred on the farm: generation {a.generation}.");
            if (a.offspring > 0) sb.AppendLine($"{a.offspring} young so far.");
        }
        if (a.relations != null && a.relations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"<color={Muted}><b>Its companions</b></color>");
            foreach (string r in a.relations) sb.AppendLine("- " + Sentence(r));
        }
        if (a.traits != null && a.traits.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"<color={Muted}><b>What you know of its breeding</b></color>");
            foreach (string t in a.traits) sb.AppendLine(t);
        }
        if (a.bond < 0 && a.relations.Count == 0 && !family) sb.AppendLine($"<color={Muted}>Its family and bonds are for its keepers.</color>");
        Set(familyText, sb.ToString());
        Partners(a, alive && here && a.mine);
    }

    /// <summary>The partners the chosen animal could be bred with here, each with Breed (ready) or why not (rebuilt when they change).</summary>
    private void Partners(AnimalView a, bool mayBreed)
    {
        var sig = new StringBuilder();
        sig.Append(a != null ? a.id : "").Append(mayBreed).Append(waiting).Append(a != null && a.breedReady).Append(a != null ? a.breedHint : "").Append('|');
        if (a?.partners != null)
            foreach (AnimalPartnerView p in a.partners)
                if (p != null) sig.Append(p.id).Append(p.ready).Append(p.hint).Append(p.name).Append(';');
        string signature = sig.ToString();
        if (signature == partnerSignature) return;
        partnerSignature = signature;
        Clear(partnerList);
        if (a == null || !mayBreed)
        {
            UITheme.Label(partnerList, a == null ? "" : "Only its owner breeds it, in its own pen.", 13f, UITheme.Muted);
            return;
        }
        if (!a.breedReady) UITheme.Label(partnerList, Sentence(a.breedHint), 13f, UITheme.Muted);
        if (a.partners == null || a.partners.Count == 0)
        {
            if (a.breedReady) UITheme.Label(partnerList, "No partner for it here: one of its kind, of the other sex, and yours.", 13f, UITheme.Muted);
            return;
        }
        foreach (AnimalPartnerView p in a.partners)
        {
            if (p == null) continue;
            string id = p.id;
            RectTransform row = UITheme.Row("Partner " + id, partnerList, 40f, 6f);
            Button b = UITheme.Button("Breed " + id, row, $"Breed with {p.name}", () => Do(new AnimalRequest { kind = AnimalActionKind.Breed, animal = selected, text = id }),
                p.ready && a.breedReady ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Normal, 220f, 38f);
            b.interactable = p.ready && a.breedReady && !waiting;
            TextMeshProUGUI label = UITheme.Label(row, string.IsNullOrEmpty(p.hint) ? "Ready." : Sentence(p.hint), 13f, p.ready ? UITheme.Muted : UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 38f, 1f);
        }
    }

    /// <summary>Calls the chosen animal (one whose bond lets it come when called): its body comes to the fence where the player stands (this machine's look only).</summary>
    private void CallIt()
    {
        AnimalBody body = pen != null ? AnimalBodies.Of(pen, selected) : null;
        if (body == null || player == null) return;
        body.Called(player.transform.position);
        Message($"You call {Find(pen.View, selected, out _)?.name ?? "it"}.");
    }

    // ------------------------------------------------------------------ what the player carries (right)
    /// <summary>Reads what the player carries (their own inventory; a client's copy of it), sorted by key.</summary>
    private void ReadCarried()
    {
        carried.Clear();
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv == null) return;
        AnimalDatabase db = AnimalDatabase.Instance;
        foreach (InventoryItem it in AnimalToolkit.Stacks(inv, stacks))
        {
            ItemSO item = it != null ? it.itemScriptableObject : null;
            if (item == null || it.stackCurrent <= 0) continue;
            string key = AnimalToolkit.KeyOf(item);
            if (string.IsNullOrEmpty(key)) continue;
            if (item is LivestockItemSO)
            {
                // A crate holding one of the world's animals is its own line (to choose which to let out); plain crates are counted together.
                for (int i = 0; i < it.stackCurrent; i++)
                {
                    int code = AnimalToolkit.CodeOf(it, i);
                    if (AnimalItemCode.TryCrated(code, out AnimalSex sex, out GrowthStage stage, out int grade, out _))
                    {
                        Carry c = Add(carried.young, key + "#" + code, item, 1);
                        c.key = key;
                        c.code = code;
                        c.name = CratedName(item as LivestockItemSO, sex, stage, grade, item.Name);
                    }
                    else Add(carried.young, key, item, 1);
                }
                continue;
            }
            if (AnimalToolkit.FoodOf(item) != null) Add(carried.food, key, item, it.stackCurrent);
            AnimalTreatmentData tr = db.TreatmentOfItem(key);
            if (tr != null) Add(carried.medicine, key, item, it.stackCurrent).treatment = tr;
            AnimalToolData tool = db.ToolOfItem(key);
            if (tool == null) continue;
            if (tool.consumed) Add(carried.supplies, key, item, it.stackCurrent).tool = tool;
            else carried.tools.Add(new CarriedTool { key = key, tool = tool, edge = AnimalToolkit.EdgeOf(it) });
        }
        AnimalToolkit.WaterOf(inv, out carried.water, out _);
    }

    /// <summary>A crated animal in a line: "Leghorn chicken, adult female (fine) - crated".</summary>
    private static string CratedName(LivestockItemSO crate, AnimalSex sex, GrowthStage stage, int grade, string fallback)
    {
        AnimalSpeciesData sp = crate != null ? AnimalDatabase.Instance.Species(crate.SpeciesId) : null;
        if (sp == null) return $"{fallback}: {AnimalCommands.Sex(sex)}, {AnimalSim.Stage(stage).ToLowerInvariant()}";
        AnimalBreedData breed = sp.BreedOrDefault(crate.BreedId);
        string kind = (breed != null ? breed.name + " " : "") + sp.DisplayName.ToLowerInvariant();
        return $"{kind}, {AnimalSim.Stage(stage).ToLowerInvariant()} {AnimalCommands.Sex(sex)} ({AnimalTrade.GradeWord(grade)}) - crated";
    }

    private static Carry Add(SortedDictionary<string, Carry> into, string key, ItemSO item, int count)
    {
        if (!into.TryGetValue(key, out Carry c))
        {
            c = new Carry { key = key, name = item.Name ?? key };
            into[key] = c;
        }
        c.count += Mathf.Max(0, count);
        return c;
    }

    /// <summary>The best care tool the player carries with any of these tags, chosen as the rules choose it (quality x (0.5 + 0.5 x what is left of it)); none: has is false.</summary>
    private AnimalToolUse BestTool(IReadOnlyList<string> tags)
    {
        AnimalToolUse best = default(AnimalToolUse);
        if (tags == null || tags.Count == 0) return best;
        float bestScore = -1f;
        foreach (CarriedTool t in carried.tools)
        {
            if (t.tool == null || !t.tool.HasAny(tags)) continue;
            float quality = Mathf.Clamp01(t.tool.quality);
            float score = quality * (0.5f + 0.5f * t.edge);
            if (score <= bestScore) continue;
            bestScore = score;
            best = new AnimalToolUse(t.key, t.tool.id, quality, t.edge);
        }
        return best;
    }

    /// <summary>The first supply carried with this tag (soap, bedding), or null.</summary>
    private Carry SupplyFor(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        foreach (Carry c in carried.supplies.Values)
            if (c.tool != null && c.tool.HasTag(tag)) return c;
        return null;
    }

    private static string ToolName(AnimalToolUse use)
    {
        AnimalToolData t = AnimalDatabase.Instance.Tool(use.tool) ?? AnimalDatabase.Instance.ToolOfItem(use.key);
        return t != null ? t.DisplayName.ToLowerInvariant() : "tool";
    }

    /// <summary>Food and young animals the player carries (rebuilt only when it changed).</summary>
    private void RefreshCarried(AnimalView a, bool here, AnimalRights rights)
    {
        bool feedable = a != null && here && a.status != AnimalStatus.Dead;
        var sig = new StringBuilder();
        foreach (Carry c in carried.food.Values) sig.Append(c.key).Append(c.count).Append('|');
        sig.Append('#');
        foreach (Carry c in carried.young.Values) sig.Append(c.key).Append(c.code).Append(c.count).Append('|');
        sig.Append(pen != null && pen.View != null && pen.View.hopper);
        sig.Append(selected).Append(feedable).Append((int)rights).Append(waiting);
        string signature = sig.ToString();
        if (signature == carriedSignature) return;
        carriedSignature = signature;

        Clear(carriedFood);
        bool tend = (rights & AnimalRights.Tend) != 0;
        if (carried.food.Count == 0) UITheme.Label(carriedFood, "You carry no food for animals.", 13f, UITheme.Muted);
        foreach (Carry c in carried.food.Values)
        {
            string key = c.key;
            RectTransform row = UITheme.Row("Food " + key, carriedFood, 40f, 6f);
            TextMeshProUGUI label = UITheme.Label(row, $"{c.name} ×{c.count}", 14f, UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 38f, 1f);
            // Three buttons where there is a hopper: narrower, so the food's name keeps its room.
            bool withHopper = pen != null && pen.View != null && pen.View.hopper;
            float bw = withHopper ? 76f : 90f;
            Button feed = UITheme.Button("Feed", row, "Feed it", () => Do(new AnimalRequest { kind = AnimalActionKind.Feed, animal = selected, item = key }), UITheme.ButtonStyle.Normal, bw, 38f);
            feed.interactable = feedable && !waiting;
            Button stock = UITheme.Button("Stock", row, "Feeder", () => Do(new AnimalRequest { kind = AnimalActionKind.StockFeeder, item = key, count = stockCount }), UITheme.ButtonStyle.Normal, bw, 38f);
            stock.interactable = tend && !waiting;
            if (withHopper)
            {
                Button hopper = UITheme.Button("Hopper", row, "Hopper", () => Do(new AnimalRequest { kind = AnimalActionKind.StockFeeder, item = key, count = stockCount, text = AnimalUpgrades.HopperText }), UITheme.ButtonStyle.Normal, bw, 38f);
                hopper.interactable = tend && !waiting;
            }
        }

        Clear(carriedLivestock);
        bool keep = (rights & AnimalRights.Keep) != 0;
        if (carried.young.Count == 0) UITheme.Label(carriedLivestock, "Buy young animals in crates from a livestock merchant.", 13f, UITheme.Muted);
        foreach (Carry c in carried.young.Values)
        {
            string key = c.key;
            int code = c.code;
            RectTransform row = UITheme.Row("Young " + key + "#" + code, carriedLivestock, 40f, 6f);
            TextMeshProUGUI label = UITheme.Label(row, code != 0 ? c.name : $"{c.name} ×{c.count}", 14f, UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 38f, 1f);
            // A plain crate's line lets out a plain crate only (never a crated animal of the same kind).
            int chosen = code != 0 ? code : AnimalCommands.PlainCrate;
            Button release = UITheme.Button("Let out", row, "Let it out", () => Do(new AnimalRequest { kind = AnimalActionKind.Release, item = key, text = nameInput.text, code = chosen }), UITheme.ButtonStyle.Primary, 110f, 38f);
            release.interactable = keep && !waiting;
        }
    }

    // ------------------------------------------------------------------ the pen page (Phase 5)
    private void ShowPenPage(AnimalPenView v)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{v.levelName}</b> (level {v.level} of {v.maxLevel}): {v.area:0} m² of room ({v.baseArea:0} m² as built).");
        if (!v.upgradesOn) sb.AppendLine($"<color={Muted}>Pens are not improved here.</color>");
        else if (v.nextLevelPrice >= 0) sb.AppendLine($"Next: <b>{v.nextLevelName}</b> for {FeeText(v.nextLevelPrice)} ({v.nextLevelText}).");
        else sb.AppendLine($"<color={Muted}>It is as good as it gets.</color>");
        sb.AppendLine();
        sb.AppendLine("<b>Machines</b>");
        sb.AppendLine(v.hopper ? v.hopperText : $"<color={Muted}>Feed hopper ({FeeText(v.hopperPrice)}): fill it, and it keeps the feeder stocked; feed keeps better in it.</color>");
        sb.AppendLine(v.waterer ? v.watererText : $"<color={Muted}>Automatic waterer ({FeeText(v.watererPrice)}): it keeps the trough topped up with clean water.</color>");
        sb.AppendLine(v.trays ? v.traysText : $"<color={Muted}>Egg trays ({FeeText(v.traysPrice)}): eggs roll into them as they are laid, so the nests never fill.</color>");
        if (!string.IsNullOrEmpty(v.upkeepText))
        {
            sb.AppendLine();
            sb.AppendLine(v.disrepair ? $"<color=#d9826a>{v.upkeepText} Its machines stand still and its level counts for nothing but room until it is paid.</color>" : v.upkeepText);
        }
        if (!v.canBuild) sb.AppendLine($"<color=#c9a86a>Only those who keep animals here may build in it.</color>");
        Set(upgradesText, sb.ToString());

        bool levelShown = v.upgradesOn && v.nextLevelPrice >= 0;
        Visible(levelButton, levelShown);
        if (levelShown)
        {
            SetLabel(levelButton, $"Improve: {v.nextLevelName} ({FeeText(v.nextLevelPrice)})");
            levelButton.interactable = v.canBuild && !v.disrepair && !waiting;
        }
        Visible(payButton, v.upkeepOwed > 0);
        if (v.upkeepOwed > 0)
        {
            SetLabel(payButton, $"Pay upkeep ({FeeText(v.upkeepOwed)})");
            payButton.interactable = v.canPay && !waiting;
        }
        // Paying ahead keeps its machines working while its keepers are away.
        bool ahead = v.upkeepPerDay > 0f && v.upkeepWeekPrice > 0;
        Visible(aheadButton, ahead);
        if (ahead)
        {
            SetLabel(aheadButton, $"Pay a week ahead ({FeeText(v.upkeepWeekPrice)})");
            aheadButton.interactable = v.canPay && !waiting;
        }
        MachineButton(hopperButton, v, AnimalDevice.Hopper, v.hopper, v.hopperPrice);
        MachineButton(watererButton, v, AnimalDevice.Waterer, v.waterer, v.watererPrice);
        MachineButton(traysButton, v, AnimalDevice.Trays, v.trays, v.traysPrice);
    }

    private void MachineButton(Button b, AnimalPenView v, AnimalDevice d, bool fitted, long price)
    {
        // A fitted machine can be taken out even while upgrades are off (its feed is never stuck in it).
        Visible(b, v.upgradesOn || fitted);
        if (!v.upgradesOn && !fitted) return;
        string name = AnimalUpgrades.DeviceName(d);
        bool mayRemove = d == AnimalDevice.Hopper ? v.removeHopper : d == AnimalDevice.Waterer ? v.removeWaterer : v.removeTrays;
        if (fitted) SetLabel(b, Armed("remove:" + d) ? "Click again to take it out" : $"Take out the {name.ToLowerInvariant()}");
        else SetLabel(b, $"{name} ({FeeText(price)})");
        b.interactable = !waiting && (fitted ? mayRemove : v.canBuild && !v.disrepair);
    }

    private void Improve()
    {
        AnimalPenView v = pen != null ? pen.View : null;
        if (v == null || v.nextLevelPrice < 0) return;
        Do(new AnimalRequest { kind = AnimalActionKind.Upgrade, text = AnimalUpgrades.LevelText, expected = v.nextLevelPrice });
    }

    /// <summary>Pays what the pen owes (or that and a week ahead), for the sum shown.</summary>
    private void PayUpkeep(bool weekAhead)
    {
        AnimalPenView v = pen != null ? pen.View : null;
        if (v == null) return;
        long price = weekAhead ? v.upkeepWeekPrice : v.upkeepOwed;
        if (price <= 0) return;
        Do(new AnimalRequest { kind = AnimalActionKind.PayUpkeep, expected = price, count = weekAhead ? AnimalUpgrades.WeekAhead : 0 });
    }

    /// <summary>Fits a machine for the price shown, or takes out one that is fitted.</summary>
    private void Machine(AnimalDevice d)
    {
        AnimalPenView v = pen != null ? pen.View : null;
        if (v == null) return;
        bool fitted = d == AnimalDevice.Hopper ? v.hopper : d == AnimalDevice.Waterer ? v.waterer : v.trays;
        long price = d == AnimalDevice.Hopper ? v.hopperPrice : d == AnimalDevice.Waterer ? v.watererPrice : v.traysPrice;
        string text = AnimalUpgrades.DeviceText(d);
        // Taking one out takes two clicks (the hopper's feed that does not fit the feeder is lost).
        if (fitted && !Confirm("remove:" + d)) return;
        Do(fitted
            ? new AnimalRequest { kind = AnimalActionKind.Upgrade, text = AnimalUpgrades.RemovePrefix + text }
            : new AnimalRequest { kind = AnimalActionKind.Upgrade, text = text, expected = price });
    }

    // ------------------------------------------------------------------ doing
    private void Do(AnimalRequest r)
    {
        if (pen == null || player == null) return;
        if (waiting)
        {
            Message("...");
            return;
        }
        r.seq = ++requestSeq;
        r.token = Guid.NewGuid().ToString("N");
        r.pen = pen.PenId;
        lastRequest = r;
        AnimalResult result = pen.Perform(r, player);
        if (result == null)
        {
            waiting = true;
            waitingSeq = r.seq;
            waitingSince = Time.unscaledTime;
            Refresh();
            return;
        }
        Answer(r, result);
    }

    private void OnAnswer(CombatEntity who, AnimalResult result)
    {
        if (who != player || result == null || !waiting) return;
        if (result.seq != waitingSeq && !(result.seq == 0 && !result.ok)) return;
        waiting = false;
        Answer(lastRequest, result);
    }

    /// <summary>
    /// The answer to a request: a close look, a careful look and a history fill their pages; anything else is said in the
    /// message strip. Work that went well is heard at the animal.
    /// </summary>
    private void Answer(AnimalRequest req, AnimalResult r)
    {
        AnimalActionKind kind = req.kind;
        List<string> lines = r.lines ?? new List<string>();
        string about = !string.IsNullOrEmpty(r.animal) ? r.animal : req.animal ?? "";
        if (kind == AnimalActionKind.Inspect && r.ok)
        {
            closeLook.Clear();
            closeLook.AddRange(lines);
            closeLookOf = about;
        }
        else if (kind == AnimalActionKind.Examine && r.ok)
        {
            examined.Clear();
            examined.AddRange(lines);
            examinedOf = about;
            Message(r.message);
            page = Page.Health;
            MarkPage();
        }
        else if (kind == AnimalActionKind.History && r.ok)
        {
            history.Clear();
            history.AddRange(lines);
            historyOf = about;
            historyTitle = r.message ?? "";
        }
        else
        {
            string text = r.message + (lines.Count > 0 ? "\n" + string.Join(" ", lines) : "");
            if (r.ok && kind == AnimalActionKind.Butcher) text += "\nIts carcass lies by the fence: butcher it there.";
            Message(text);
        }
        if (kind == AnimalActionKind.Slaughter) slaughterArmed = "";
        if (r.ok)
        {
            if (kind == AnimalActionKind.Release && !string.IsNullOrEmpty(r.animal))
            {
                selected = r.animal;
                nameInput.text = "";
            }
            // Its record goes with its remains.
            if ((kind == AnimalActionKind.Butcher || kind == AnimalActionKind.RemoveRemains) && req.animal == selected) selected = "";
            if (!r.repeated) Sound(req, r);
        }
        if (AnimalPen.RulesHere && pen != null && player != null) pen.ReceiveView(AnimalDirector.ViewFor(pen, player));
        ResetSignatures();
        Refresh();
    }

    /// <summary>The sound of work that went well, at the animal's body (else where the player stands).</summary>
    private void Sound(AnimalRequest req, AnimalResult r)
    {
        string what = SoundOf(req);
        if (string.IsNullOrEmpty(what) || pen == null) return;
        string id = !string.IsNullOrEmpty(r.animal) ? r.animal : req.animal;
        AnimalBody body = AnimalBodies.Of(pen, id);
        Vector3 at = body != null ? body.transform.position : player != null ? player.transform.position : pen.CentreWorld;
        AnimalAudio.Play(what, at);
    }

    /// <summary>The name of a request's sound (see <see cref="AnimalSounds.Clip"/>), or null: it makes none.</summary>
    private string SoundOf(AnimalRequest req)
    {
        switch (req.kind)
        {
            case AnimalActionKind.Care: return AnimalCare.KindId(FamilyOf(req.text));
            case AnimalActionKind.CleanPen: return IsTrough(req.text) ? "wash" : "muck_out";
            case AnimalActionKind.Treat: return "treat";
            case AnimalActionKind.CallVet: return "vet";
            case AnimalActionKind.Slaughter: return "slaughter";
            case AnimalActionKind.Butcher: return "butcher";
            case AnimalActionKind.Collect: return CollectSound(req.text);
            case AnimalActionKind.Crate: return "crate";
            case AnimalActionKind.Upgrade: return "build";
            case AnimalActionKind.Release: return "content";
            default: return null;
        }
    }

    /// <summary>The sound of collecting a product ("milk", "shear"; "collect" for one gathered by hand, or all of them).</summary>
    private static string CollectSound(string product)
    {
        AnimalProductSpec p = AnimalProducts.Spec(product);
        string verb = p != null ? p.Verb.ToLowerInvariant() : "collect";
        return verb == "milk" || verb == "shear" ? verb : "collect";
    }

    /// <summary>The family of a care action by its id, from the care the animals here take (not found: a species' own).</summary>
    private AnimalCareKind FamilyOf(string careId)
    {
        AnimalPenView v = pen != null ? pen.View : null;
        if (v != null && !string.IsNullOrEmpty(careId))
            foreach (AnimalView a in v.animals)
            {
                if (a?.care == null) continue;
                foreach (AnimalCareView c in a.care)
                    if (c != null && string.Equals(c.id, careId, StringComparison.OrdinalIgnoreCase)) return (AnimalCareKind)c.kind;
            }
        return AnimalCareKind.Custom;
    }

    private static bool IsTrough(string text) => !string.IsNullOrWhiteSpace(text) && string.Equals(text.Trim(), AnimalCommands.TroughText, StringComparison.OrdinalIgnoreCase);

    private void Message(string text)
    {
        message.text = text ?? "";
        messageTime = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ small helpers
    /// <summary>Living animals in the pen (as its picture shows them).</summary>
    private static int Living(AnimalPenView v)
    {
        int n = 0;
        if (v?.animals != null)
            foreach (AnimalView a in v.animals)
                if (a != null && a.status != AnimalStatus.Dead) n++;
        return n;
    }

    private static void Set(TMP_Text t, string s)
    {
        if (t == null) return;
        s ??= "";
        if (t.text != s) t.text = s;
    }

    private static void SetLabel(Button b, string s)
    {
        if (b == null) return;
        Set(b.GetComponentInChildren<TMP_Text>(true), s);
    }

    private static void Visible(Component c, bool on)
    {
        if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    private static void Line(StringBuilder sb, string s)
    {
        if (!string.IsNullOrWhiteSpace(s)) sb.AppendLine(s);
    }

    private static string Join(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a)) return b ?? "";
        if (string.IsNullOrWhiteSpace(b)) return a;
        return a + " " + b;
    }

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);

    /// <summary>"brushed 2 hours ago" → "Brushed 2 hours ago."</summary>
    private static string Sentence(string s)
    {
        s = Capital((s ?? "").Trim());
        if (s.Length == 0) return s;
        char end = s[s.Length - 1];
        return end == '.' || end == '!' || end == '?' ? s : s + ".";
    }
}
