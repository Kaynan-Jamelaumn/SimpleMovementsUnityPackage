using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window of the soil field the local player is working (built from code, one per machine).
/// <list type="bullet">
/// <item><b>Left - the spot</b>: what is seen there (the soil, the plant), the last instrument reading (with <i>Write
/// down</i>, into the player's notebook) and the plant's label.</item>
/// <item><b>Middle - the work surface</b>: <i>Bed</i>, the soil around the player from above (click a spot or a plant);
/// <i>Plant</i>, the chosen plant up close (click a fruit, a flower, a leaf to pick it); <i>Soil</i>, a cut through the
/// soil at the spot - the hole as deep as it was dug (a ruler only with a depth gauge in the bag).</item>
/// <item><b>Right - the work</b>: everything the player can do with what they carry: work the soil, dig and pour (held),
/// sow, spread materials, treat, measure, prune, pollinate, pick, harvest, save seeds.</item>
/// </list>
/// Nothing here says what a plant needs or what is wrong with it: only what is seen and what instruments read.
/// It is an exclusive panel of the player's inventory while shown; walking away closes it.
/// </summary>
public class FarmWindow : MonoBehaviour, IExclusivePanel
{
    private static FarmWindow instance;

    private SoilField soilField;
    private CombatEntity player;
    private InventoryManager registeredWith;
    private bool managesCursor, hudFocus;
    private CursorLockMode previousLock;
    private bool previousVisible;

    private Canvas canvas;
    private TextMeshProUGUI title, subtitle, spotText, readingText, message, toolsInfo, waterInfo, selectedInfo, portionsLabel, pruneLabel, overlayTitle;
    private RectTransform seedList, materialList, instrumentList, overlayList;
    private GameObject overlay;
    private TMP_InputField labelInput, noteInput;
    private CraftCanvasView view;
    private Button bedTab, plantTab, soilTab;
    private readonly List<Button> methodButtons = new List<Button>();
    private FarmHoldButton digHold, pourHold;

    private enum Mode { Bed, Plant, Soil }
    private Mode mode = Mode.Bed;
    private Vector2 spot;
    private bool spotChosen;
    private int plantId, pieceId;
    private string seed = "", material = "";
    private int portions = 1, method, prune;
    private string lastNote = "";
    private float messageTime, drawTimer, listTimer, dateTimer, inspectTimer;
    private bool inspectWanted, waiting;
    private string listSignature = "";
    private bool notebookOpen, skillsOpen;
    private readonly List<(int id, Vector2 at, float r)> pieceHits = new List<(int, Vector2, float)>();

    private const int Width = 512, Height = 512, Order = 59;

    public string PanelName => soilField != null ? "Field: " + soilField.DisplayName : "Field";
    public static bool IsOpen => instance != null && instance.soilField != null && instance.canvas.gameObject.activeSelf;
    private AgriSettings Settings => AgricultureDatabase.Instance.Settings;

    // ------------------------------------------------------------------ opening
    public static void Show(SoilField f, CombatEntity p)
    {
        if (f == null || p == null) return;
        if (instance == null)
        {
            InventoryEventSystems.EnsureSingle();
            var go = new GameObject("Farm Window");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FarmWindow>();
            instance.Build();
        }
        if (instance.soilField != f || instance.player != p)
        {
            instance.Detach();
            instance.soilField = f;
            instance.player = p;
            f.Answered += instance.OnAnswer;
            instance.spot = f.Local(p.transform.position);
            instance.spotChosen = f.Contains(p.transform.position);
            instance.plantId = instance.pieceId = 0;
            instance.listSignature = "";
            instance.readingText.text = "";
            instance.lastNote = "";
        }
        instance.canvas.gameObject.SetActive(true);
        instance.Register();
        instance.inspectWanted = true;
        instance.Refresh();
    }

    /// <summary>Closes the window if it shows <paramref name="f"/>.</summary>
    public static void Close(SoilField f)
    {
        if (instance != null && instance.soilField == f && f != null) instance.Hide();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private void OnDestroy()
    {
        Hide();
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (soilField == null || player == null || !soilField.isActiveAndEnabled || !player.isActiveAndEnabled)
        {
            Hide();
            return;
        }
        if (soilField.DistanceFromOutline(player.transform.position) > Settings.workRange + soilField.extraReach + 1.5f)
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
        dateTimer -= dt;
        if (dateTimer <= 0f)
        {
            dateTimer = 1f;
            subtitle.text = FarmClock.Date.Full();
        }
        listTimer -= dt;
        if (listTimer <= 0f)
        {
            listTimer = 0.5f;
            RefreshLists();
        }
        // An answer that never came (a lost message, a server that went away) does not hold the window forever.
        if (waiting && Time.unscaledTime - waitingSince > 5f)
        {
            waiting = false;
            Message("No answer came (it may still have been done).");
        }
        inspectTimer -= dt;
        if (inspectWanted && inspectTimer <= 0f && !waiting)
        {
            inspectWanted = false;
            inspectTimer = 0.4f;
            Inspect();
        }
        drawTimer -= dt;
        if (drawTimer <= 0f)
        {
            drawTimer = 1f / 24f;
            Draw();
        }
        if (messageTime > 0f && Time.unscaledTime - messageTime > 7f)
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
        if (soilField != null) soilField.Answered -= OnAnswer;
        soilField = null;
        player = null;
        waiting = false;
        notebookOpen = skillsOpen = false;
        if (overlay != null) overlay.SetActive(false);
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

    public void ClosePanel()
    {
        if (notebookOpen || skillsOpen)
        {
            notebookOpen = skillsOpen = false;
            overlay.SetActive(false);
            return;
        }
        Hide();
    }

    // ------------------------------------------------------------------ building
    private void Build()
    {
        canvas = UITheme.Canvas("Farm Window", Order, transform);
        Transform c = canvas.transform;
        Image backdrop = UITheme.Panel("Backdrop", c, new Color(0f, 0f, 0f, 0.35f), 0);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        Image window = UITheme.Window("Window", c, new Vector2(1640f, 980f));
        Transform w = window.transform;

        title = UITheme.Label(w, "Field", 30f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(28f, -60f), new Vector2(0f, -16f));
        subtitle = UITheme.Label(w, "", 16f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Date");
        UITheme.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(28f, -88f), new Vector2(0f, -60f));
        RectTransform header = UITheme.Area("Header Buttons", w, new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(-24f, -22f));
        InventoryUIFactory.Horizontal(header.gameObject, 10f).childAlignment = TextAnchor.MiddleRight;
        UITheme.Button("Notebook", header, "Notebook", () => OpenOverlay(true), UITheme.ButtonStyle.Normal, 160f, 42f);
        UITheme.Button("Skills", header, "Skills", () => OpenOverlay(false), UITheme.ButtonStyle.Normal, 130f, 42f);
        UITheme.Button("Close", header, "Close", Hide, UITheme.ButtonStyle.Normal, 120f, 42f);

        // Left: the spot.
        RectTransform left = Column(w, "Spot", 24f, 444f);
        UITheme.Header(left, "What you see");
        RectTransform spotArea = UITheme.ScrollList("Spot Text", left, 410f, out _);
        spotText = UITheme.Label(spotArea, "", 15f, UITheme.Text);
        UITheme.Header(left, "Readings");
        RectTransform readArea = UITheme.ScrollList("Readings", left, 170f, out _);
        readingText = UITheme.Label(readArea, "", 14f, UITheme.Text);
        RectTransform noteRow = UITheme.Row("Note Row", left, 40f, 8f);
        UITheme.Button("Write Down", noteRow, "Write it down", WriteDown, UITheme.ButtonStyle.Normal, 0f, 38f);
        UITheme.Header(left, "Label");
        RectTransform labelRow = UITheme.Row("Label Row", left, 44f, 8f);
        labelInput = UITheme.Input("Label", labelRow, "Write on its label...", 40, 42f);
        InventoryUIFactory.Size(labelInput, -1f, 42f, 1f);
        UITheme.Button("Set Label", labelRow, "Mark", () => Do(new FarmRequest { kind = FarmActionKind.Label, text = labelInput.text }), UITheme.ButtonStyle.Normal, 90f, 42f);

        // Middle: the work surface.
        Image midBack = UITheme.Panel("Work", w, UITheme.Card, UITheme.Radius);
        UITheme.Place(midBack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(460f, 60f), new Vector2(1176f, -100f));
        RectTransform tabs = UITheme.Area("Tabs", midBack.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -54f), new Vector2(-12f, -10f));
        InventoryUIFactory.Horizontal(tabs.gameObject, 8f);
        bedTab = UITheme.Button("Bed Tab", tabs, "Bed", () => SetMode(Mode.Bed), UITheme.ButtonStyle.Tab, 0f, 40f);
        plantTab = UITheme.Button("Plant Tab", tabs, "Plant", () => SetMode(Mode.Plant), UITheme.ButtonStyle.Tab, 0f, 40f);
        soilTab = UITheme.Button("Soil Tab", tabs, "Soil", () => SetMode(Mode.Soil), UITheme.ButtonStyle.Tab, 0f, 40f);
        Image surface = UITheme.Panel("Surface", midBack.transform, new Color(0f, 0f, 0f, 0.55f), UITheme.RadiusSmall);
        UITheme.Place(surface.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-340f, -742f), new Vector2(340f, -62f));
        var raw = new GameObject("Work Surface", typeof(RectTransform), typeof(RawImage));
        raw.transform.SetParent(surface.transform, false);
        InventoryUIFactory.Stretch((RectTransform)raw.transform, 4f, 4f);
        view = raw.AddComponent<CraftCanvasView>();
        view.Build(Width, Height);
        view.Pointer += OnPointer;
        selectedInfo = UITheme.Label(midBack.transform, "", 14f, UITheme.Muted, TextAlignmentOptions.Center, FontStyles.Normal, "Selected");
        UITheme.Place(selectedInfo.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 12f), new Vector2(-16f, 48f));

        // Right: the work.
        Image rightBack = UITheme.Panel("Actions", w, UITheme.Card, UITheme.Radius);
        UITheme.Place(rightBack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1192f, 60f), new Vector2(1616f, -100f));
        RectTransform actions = UITheme.ScrollList("Action List", rightBack.transform, 0f, out _, null, 10, 6f);
        InventoryUIFactory.Stretch((RectTransform)actions.parent.parent, 6f, 6f);

        UITheme.Header(actions, "Hands and tools");
        toolsInfo = UITheme.Label(actions, "", 13f, UITheme.Muted);
        RectTransform r1 = UITheme.Row("Row 1", actions, 40f, 6f);
        UITheme.Button("Look", r1, "Look", () => Inspect(true), UITheme.ButtonStyle.Normal, 0f, 38f);
        UITheme.Button("Till", r1, "Till", () => Do(new FarmRequest { kind = FarmActionKind.Till }), UITheme.ButtonStyle.Normal, 0f, 38f);
        UITheme.Button("Stones", r1, "Clear stones", () => Do(new FarmRequest { kind = FarmActionKind.ClearStones }), UITheme.ButtonStyle.Normal, 0f, 38f);
        RectTransform r2 = UITheme.Row("Row 2", actions, 40f, 6f);
        Button dig = UITheme.Button("Dig", r2, "Dig (hold)", null, UITheme.ButtonStyle.Normal, 0f, 38f);
        digHold = dig.gameObject.AddComponent<FarmHoldButton>();
        digHold.Released += s => { if (s > 0.05f) Do(new FarmRequest { kind = FarmActionKind.Dig, amount = s }); };
        UITheme.Button("Cover", r2, "Cover", () => Do(new FarmRequest { kind = FarmActionKind.Cover }), UITheme.ButtonStyle.Normal, 0f, 38f);
        UITheme.Button("Stake", r2, "Stake", () => Do(new FarmRequest { kind = FarmActionKind.Support }), UITheme.ButtonStyle.Normal, 0f, 38f);
        RectTransform r3 = UITheme.Row("Row 3", actions, 40f, 6f);
        UITheme.Button("Pests", r3, "Pick off pests", () => Do(new FarmRequest { kind = FarmActionKind.RemovePests, option = -1 }), UITheme.ButtonStyle.Normal, 0f, 38f);
        UITheme.Button("Uproot", r3, "Pull up", () => Do(new FarmRequest { kind = FarmActionKind.Uproot }), UITheme.ButtonStyle.Danger, 0f, 38f);

        UITheme.Header(actions, "Water");
        waterInfo = UITheme.Label(actions, "", 13f, UITheme.Muted);
        Button pour = UITheme.Button("Pour", actions, "Pour (hold)", null, UITheme.ButtonStyle.Normal, 0f, 40f);
        pourHold = pour.gameObject.AddComponent<FarmHoldButton>();
        pourHold.Released += s => { if (s > 0.05f) Do(new FarmRequest { kind = FarmActionKind.Water, amount = s }); };

        UITheme.Header(actions, "Sow");
        seedList = UITheme.Column("Seeds", actions, 0, 4f, true);
        UITheme.Button("Sow", actions, "Sow here", () => Do(new FarmRequest { kind = FarmActionKind.Sow, item = seed }), UITheme.ButtonStyle.Primary, 0f, 40f);

        UITheme.Header(actions, "Soil materials");
        materialList = UITheme.Column("Materials", actions, 0, 4f, true);
        RectTransform pr = UITheme.Row("Portions", actions, 38f, 6f);
        UITheme.Button("Less", pr, "-", () => { portions = Mathf.Max(1, portions - 1); UpdatePortions(); }, UITheme.ButtonStyle.Normal, 44f, 36f);
        portionsLabel = UITheme.Label(pr, "1 portion", 15f, UITheme.Text, TextAlignmentOptions.Center);
        InventoryUIFactory.Size(portionsLabel, -1f, 36f, 1f);
        UITheme.Button("More", pr, "+", () => { portions = Mathf.Min(20, portions + 1); UpdatePortions(); }, UITheme.ButtonStyle.Normal, 44f, 36f);
        RectTransform mr = UITheme.Row("Method", actions, 38f, 6f);
        string[] methods = { "Spread wide", "Around it", "One spot" };
        for (int i = 0; i < methods.Length; i++)
        {
            int m = i;
            methodButtons.Add(UITheme.Button("Method " + i, mr, methods[i], () => { method = m; UpdateMethod(); }, UITheme.ButtonStyle.Tab, 0f, 36f));
        }
        RectTransform ar = UITheme.Row("Apply Row", actions, 40f, 6f);
        UITheme.Button("Apply", ar, "Put on the soil", () => Do(new FarmRequest { kind = FarmActionKind.Amend, item = material, amount = portions, option = method }), UITheme.ButtonStyle.Primary, 0f, 38f);
        UITheme.Button("Treat", ar, "Treat the plant", () => Do(new FarmRequest { kind = FarmActionKind.Treat, item = material }), UITheme.ButtonStyle.Normal, 0f, 38f);

        UITheme.Header(actions, "Measure");
        instrumentList = UITheme.Column("Instruments", actions, 0, 4f, true);

        UITheme.Header(actions, "Care");
        RectTransform pk = UITheme.Row("Prune Kind", actions, 38f, 6f);
        UITheme.Button("Prev", pk, "<", () => { prune = (prune + 5) % 6; UpdatePrune(); }, UITheme.ButtonStyle.Normal, 44f, 36f);
        pruneLabel = UITheme.Label(pk, "", 14f, UITheme.Text, TextAlignmentOptions.Center);
        InventoryUIFactory.Size(pruneLabel, -1f, 36f, 1f);
        UITheme.Button("Next", pk, ">", () => { prune = (prune + 1) % 6; UpdatePrune(); }, UITheme.ButtonStyle.Normal, 44f, 36f);
        UITheme.Button("Prune", actions, "Prune", () => Do(new FarmRequest { kind = FarmActionKind.Prune, option = prune }), UITheme.ButtonStyle.Normal, 0f, 38f);
        RectTransform po = UITheme.Row("Pollen", actions, 40f, 6f);
        UITheme.Button("Collect Pollen", po, "Take pollen", () => Do(new FarmRequest { kind = FarmActionKind.CollectPollen }), UITheme.ButtonStyle.Normal, 0f, 38f);
        UITheme.Button("Pollinate", po, "Pollinate", () => Do(new FarmRequest { kind = FarmActionKind.Pollinate }), UITheme.ButtonStyle.Normal, 0f, 38f);

        UITheme.Header(actions, "Harvest");
        RectTransform h1 = UITheme.Row("Harvest 1", actions, 40f, 6f);
        UITheme.Button("Pick", h1, "Pick it", () => Do(new FarmRequest { kind = FarmActionKind.Harvest, piece = pieceId }), UITheme.ButtonStyle.Primary, 0f, 38f);
        UITheme.Button("Pick All", h1, "Pick everything", () => Do(new FarmRequest { kind = FarmActionKind.Harvest, piece = 0 }), UITheme.ButtonStyle.Normal, 0f, 38f);
        RectTransform h2 = UITheme.Row("Harvest 2", actions, 40f, 6f);
        UITheme.Button("Whole", h2, "Harvest the plant", () => Do(new FarmRequest { kind = FarmActionKind.HarvestPlant }), UITheme.ButtonStyle.Normal, 0f, 38f);
        UITheme.Button("Seeds", h2, "Save seeds", () => Do(new FarmRequest { kind = FarmActionKind.CollectSeeds, piece = pieceId }), UITheme.ButtonStyle.Normal, 0f, 38f);

        message = UITheme.Label(w, "", 15f, UITheme.Gold, TextAlignmentOptions.Center, FontStyles.Normal, "Message");
        UITheme.Place(message.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 14f), new Vector2(-24f, 52f));

        BuildOverlay(w);
        UpdatePortions();
        UpdateMethod();
        UpdatePrune();
        SetMode(Mode.Bed);
        canvas.gameObject.SetActive(false);
    }

    private static RectTransform Column(Transform window, string name, float x0, float x1)
    {
        Image bg = UITheme.Panel(name, window, UITheme.Card, UITheme.Radius);
        UITheme.Place(bg.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(x0, 60f), new Vector2(x1, -100f));
        RectTransform col = UITheme.Column("Column", bg.transform, 12, 8f);
        InventoryUIFactory.Stretch(col);
        return col;
    }

    private void BuildOverlay(Transform w)
    {
        Image o = UITheme.Panel("Overlay", w, UITheme.SurfaceRaised, UITheme.RadiusLarge);
        UITheme.Place(o.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-460f, -380f), new Vector2(460f, 380f));
        overlay = o.gameObject;
        RectTransform col = UITheme.Column("Overlay Column", o.transform, 18, 10f);
        InventoryUIFactory.Stretch(col);
        RectTransform top = UITheme.Row("Overlay Top", col, 44f, 8f);
        overlayTitle = UITheme.Label(top, "Notebook", 24f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Size(overlayTitle, -1f, 40f, 1f);
        UITheme.Button("Overlay Close", top, "Back", () => { notebookOpen = skillsOpen = false; overlay.SetActive(false); }, UITheme.ButtonStyle.Normal, 110f, 40f);
        overlayList = UITheme.ScrollList("Overlay List", col, 560f, out _);
        RectTransform input = UITheme.Row("Note Input", col, 46f, 8f);
        noteInput = UITheme.Input("Note", input, "Write a note...", 600, 44f);
        InventoryUIFactory.Size(noteInput, -1f, 44f, 1f);
        UITheme.Button("Write", input, "Write", () =>
        {
            if (string.IsNullOrWhiteSpace(noteInput.text) || player == null) return;
            AgriKnowledge.For(player).Note(soilField != null ? soilField.DisplayName : "", noteInput.text.Trim());
            noteInput.text = "";
            FillOverlay();
        }, UITheme.ButtonStyle.Primary, 110f, 44f);
        overlay.SetActive(false);
    }

    private void OpenOverlay(bool notebook)
    {
        notebookOpen = notebook;
        skillsOpen = !notebook;
        overlay.SetActive(true);
        noteInput.transform.parent.gameObject.SetActive(notebook);
        FillOverlay();
    }

    private void FillOverlay()
    {
        for (int i = overlayList.childCount - 1; i >= 0; i--) Destroy(overlayList.GetChild(i).gameObject);
        if (player == null) return;
        AgriKnowledge k = AgriKnowledge.For(player);
        AgriSettings s = Settings;
        if (skillsOpen)
        {
            overlayTitle.text = "Skills";
            for (int i = 0; i < AgriSkills.Count; i++)
            {
                var skill = (AgriSkill)i;
                float xp = k.Data.Xp(skill);
                UITheme.Label(overlayList, $"<b>{AgriText.Skill(skill)}</b>   level {s.Level(xp)}   {Bar(s.Progress(xp))}", 17f, UITheme.Text);
            }
            UITheme.Label(overlayList, $"Sown {k.Data.sown} · harvested {k.Data.harvested} · measured {k.Data.measured}", 14f, UITheme.Muted);
            return;
        }
        overlayTitle.text = "Notebook";
        List<AgriNote> notes = k.Notebook.notes;
        if (notes.Count == 0) UITheme.Label(overlayList, "Nothing written yet.", 15f, UITheme.Muted);
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            int index = i;
            AgriNote n = notes[i];
            RectTransform row = UITheme.Row("Note " + i, overlayList, 58f, 8f);
            TextMeshProUGUI label = UITheme.Label(row, $"<color=#a0a0a0>{new AgriDate(n.time, s).Describe()}{(string.IsNullOrEmpty(n.field) ? "" : " · " + n.field)}</color>\n{n.text}", 14f, UITheme.Text);
            InventoryUIFactory.Size(label, -1f, 40f, 1f);
            UITheme.Button("Remove " + i, row, "x", () => { k.RemoveNote(index); FillOverlay(); }, UITheme.ButtonStyle.Ghost, 40f, 36f);
        }
    }

    private static string Bar(float f)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(f * 12f), 0, 12);
        return "<color=#8fd18a>" + new string('|', n) + "</color><color=#555555>" + new string('|', 12 - n) + "</color>";
    }

    private void SetMode(Mode m)
    {
        mode = m;
        UITheme.SetSelected(bedTab, m == Mode.Bed);
        UITheme.SetSelected(plantTab, m == Mode.Plant);
        UITheme.SetSelected(soilTab, m == Mode.Soil);
        drawTimer = 0f;
    }

    private void UpdatePortions() => portionsLabel.text = portions == 1 ? "1 portion" : $"{portions} portions";

    private void UpdateMethod()
    {
        for (int i = 0; i < methodButtons.Count; i++) UITheme.SetSelected(methodButtons[i], i == method);
    }

    private void UpdatePrune() => pruneLabel.text = AgriText.Prune((PruneKind)prune);

    // ------------------------------------------------------------------ lists from the bag
    private void RefreshLists()
    {
        if (player == null) return;
        InventoryManager inv = InventoryManager.For(player);
        var seeds = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var materials = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var instruments = new List<SoilInstrumentSO>();
        var tools = new SortedSet<string>(StringComparer.Ordinal);
        string water = "You carry nothing to hold water.";
        float bestLitres = -1f;
        if (inv != null)
            foreach (InventoryItem it in AgriToolkit.Stacks(inv))
            {
                ItemSO item = it != null ? it.itemScriptableObject : null;
                switch (item)
                {
                    case SeedItemSO s:
                        seeds[s.SpeciesId] = (seeds.TryGetValue(s.SpeciesId, out int n) ? n : 0) + it.stackCurrent;
                        break;
                    case SoilAmendmentSO a when a.data != null:
                        materials[a.data.id] = (materials.TryGetValue(a.data.id, out int m) ? m : 0) + it.stackCurrent;
                        break;
                    case SoilInstrumentSO i:
                        if (!instruments.Exists(x => x.InstrumentId == i.InstrumentId)) instruments.Add(i);
                        break;
                    case FarmToolSO t:
                        tools.Add(t.Kind == FarmToolKind.Stakes ? $"{t.Name} ×{it.stackCurrent}" : t.Name);
                        break;
                    case WaterContainerSO c:
                    {
                        AgriItemInfo.Water(it, out float l, out WaterSourceKind kind);
                        if (l > bestLitres)
                        {
                            bestLitres = l;
                            water = l > 0.05f ? $"{c.Name}: {l:0.#} of {c.Capacity:0.#} L of {AgriText.Water(kind).ToLowerInvariant()}" : $"{c.Name}: empty";
                        }
                        break;
                    }
                }
            }
        toolsInfo.text = tools.Count > 0 ? string.Join(", ", tools) : "You carry no tools.";
        waterInfo.text = water;
        var sig = new StringBuilder();
        foreach (var kv in seeds) sig.Append(kv.Key).Append(kv.Value).Append(';');
        sig.Append('|');
        foreach (var kv in materials) sig.Append(kv.Key).Append(kv.Value).Append(';');
        sig.Append('|');
        foreach (SoilInstrumentSO i in instruments) sig.Append(i.InstrumentId).Append(';');
        sig.Append('|').Append(seed).Append('|').Append(material);
        string signature = sig.ToString();
        if (signature == listSignature) return;
        listSignature = signature;
        if (!seeds.ContainsKey(seed)) seed = "";
        if (!materials.ContainsKey(material)) material = "";
        if (seed == "") foreach (var kv in seeds) { seed = kv.Key; break; }
        if (material == "") foreach (var kv in materials) { material = kv.Key; break; }

        Clear(seedList);
        if (seeds.Count == 0) UITheme.Label(seedList, "You carry no seeds.", 13f, UITheme.Muted);
        foreach (var kv in seeds)
        {
            string id = kv.Key;
            PlantSpeciesData d = AgricultureDatabase.Instance.Species(id);
            Button b = UITheme.Button("Seed " + id, seedList, $"{(d != null ? d.name : id)} seeds ×{kv.Value}", () => { seed = id; listSignature = ""; RefreshLists(); }, UITheme.ButtonStyle.ListItem, 0f, 34f);
            UITheme.SetSelected(b, id == seed);
        }
        Clear(materialList);
        if (materials.Count == 0) UITheme.Label(materialList, "You carry no soil materials.", 13f, UITheme.Muted);
        foreach (var kv in materials)
        {
            string id = kv.Key;
            SoilAmendmentData a = AgricultureDatabase.Instance.Amendment(id);
            Button b = UITheme.Button("Material " + id, materialList, $"{(a != null ? a.name : id)} ×{kv.Value}", () => { material = id; listSignature = ""; RefreshLists(); }, UITheme.ButtonStyle.ListItem, 0f, 34f);
            UITheme.SetSelected(b, id == material);
        }
        Clear(instrumentList);
        if (instruments.Count == 0) UITheme.Label(instrumentList, "You carry no instruments.", 13f, UITheme.Muted);
        foreach (SoilInstrumentSO i in instruments)
        {
            string id = i.InstrumentId;
            UITheme.Button("Instrument " + id, instrumentList, i.Name, () => Do(new FarmRequest { kind = FarmActionKind.Measure, item = id }), UITheme.ButtonStyle.ListItem, 0f, 34f);
        }
    }

    private static void Clear(RectTransform list)
    {
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
    }

    // ------------------------------------------------------------------ doing
    private void Inspect(bool shown = false)
    {
        if (soilField == null || player == null || !spotChosen) return;
        if (waiting)
        {
            // One request at a time: the answer on its way is not taken for this look's.
            if (shown) Message("...");
            else inspectWanted = true;
            return;
        }
        int seq = ++requestSeq;
        FarmResult r = soilField.Perform(new FarmRequest { kind = FarmActionKind.Inspect, spot = spot, plant = plantId, seq = seq }, player);
        if (r != null) Show(r, FarmActionKind.Inspect, shown);
        else Wait(FarmActionKind.Inspect, shown, seq);
    }

    private void Do(FarmRequest r)
    {
        if (soilField == null || player == null) return;
        if (!spotChosen)
        {
            Message("Choose a spot on the bed first.");
            return;
        }
        if (waiting)
        {
            Message("...");
            return;
        }
        r.spot = spot;
        if (r.plant == 0) r.plant = plantId;
        r.seq = ++requestSeq;
        FarmResult result = soilField.Perform(r, player);
        if (result == null)
        {
            Wait(r.kind, true, r.seq);
            return;
        }
        Show(result, r.kind, true);
    }

    private FarmActionKind lastKind;
    private bool lastLoud;
    private float waitingSince;
    private int requestSeq, waitingSeq;

    /// <summary>A request went to the server: its answer (and only its) is shown as that kind of work, as loud as asked.</summary>
    private void Wait(FarmActionKind kind, bool loud, int seq)
    {
        waiting = true;
        lastKind = kind;
        lastLoud = loud;
        waitingSeq = seq;
        waitingSince = Time.unscaledTime;
    }

    private void OnAnswer(CombatEntity who, FarmResult result)
    {
        // An answer to a request this window gave up on (or never sent) is not taken for the one it waits for - except a
        // refusal of a request the server could not read (it has no number).
        if (who != player || result == null || !waiting) return;
        if (result.seq != waitingSeq && !(result.seq == 0 && !result.ok)) return;
        waiting = false;
        Show(result, lastKind, lastLoud);
    }

    private void Show(FarmResult r, FarmActionKind kind, bool loud)
    {
        if (kind == FarmActionKind.Inspect)
        {
            spotText.text = string.Join("\n", r.lines);
            if (loud && !string.IsNullOrEmpty(r.message)) Message(r.message);
            return;
        }
        if (kind == FarmActionKind.Measure && r.ok)
        {
            readingText.text = $"<b>{r.message}</b>\n" + string.Join("\n", r.lines);
            lastNote = r.note;
            return;
        }
        Message(r.lines.Count > 0 ? r.message + " " + r.lines[0] : r.message);
        if (r.ok)
        {
            if (kind == FarmActionKind.Sow && r.plant > 0) plantId = r.plant;
            if (kind == FarmActionKind.Uproot || kind == FarmActionKind.HarvestPlant) plantId = pieceId = 0;
            if (kind == FarmActionKind.Harvest || kind == FarmActionKind.CollectSeeds) pieceId = 0;
            inspectWanted = true;
            listSignature = "";
        }
    }

    private void Message(string text)
    {
        message.text = text ?? "";
        messageTime = Time.unscaledTime;
    }

    private void WriteDown()
    {
        if (player == null || string.IsNullOrEmpty(lastNote))
        {
            Message("There is no reading to write down.");
            return;
        }
        AgriKnowledge.For(player).Note(soilField.DisplayName, lastNote);
        Message("You write it in your notebook.");
    }

    private void Refresh()
    {
        if (soilField == null) return;
        title.text = soilField.DisplayName;
        subtitle.text = FarmClock.Date.Full();
        listSignature = "";
        RefreshLists();
        Draw();
    }

    // ------------------------------------------------------------------ the work surface
    private float ViewRadius => Settings.workRange + 0.6f;
    private float Scale => Width / (ViewRadius * 2f);
    private Vector2 Centre => player != null && soilField != null ? soilField.Local(player.transform.position) : spot;

    /// <summary>The view turns with the camera: up on the screen is where the camera looks.</summary>
    private float ViewAngle
    {
        get
        {
            Camera cam = Camera.main;
            if (cam == null || soilField == null) return 0f;
            Vector3 f = Quaternion.Inverse(soilField.transform.rotation) * cam.transform.forward;
            return Mathf.Atan2(f.x, f.z);
        }
    }

    private Vector2 ToPixel(Vector2 local, float angle, Vector2 centre)
    {
        Vector2 d = local - centre;
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        var r = new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
        return new Vector2(Width * 0.5f + r.x * Scale, Height * 0.5f + r.y * Scale);
    }

    private Vector2 ToLocal(Vector2 pixel, float angle, Vector2 centre)
    {
        var r = new Vector2((pixel.x - Width * 0.5f) / Scale, (pixel.y - Height * 0.5f) / Scale);
        float c = Mathf.Cos(-angle), s = Mathf.Sin(-angle);
        return centre + new Vector2(r.x * c - r.y * s, r.x * s + r.y * c);
    }

    private void OnPointer(CraftPointer phase, Vector2 pixel, bool right, float scroll)
    {
        if (phase != CraftPointer.Down || soilField == null || right) return;
        if (mode == Mode.Bed)
        {
            Vector2 local = ToLocal(pixel, ViewAngle, Centre);
            if (!soilField.State.Grid.TryIndex(local, out _))
            {
                Message("That is not part of the field.");
                return;
            }
            spot = local;
            spotChosen = true;
            PlantState p = PickPlant(local);
            plantId = p != null ? p.id : 0;
            if (p != null) spot = p.position;
            pieceId = 0;
            labelInput.text = p != null ? p.label : "";
            inspectWanted = true;
            inspectTimer = 0f;
        }
        else if (mode == Mode.Plant)
        {
            pieceId = 0;
            foreach (var h in pieceHits)
                if ((h.at - pixel).sqrMagnitude <= (h.r + 3f) * (h.r + 3f)) { pieceId = h.id; break; }
        }
        drawTimer = 0f;
    }

    private PlantState PickPlant(Vector2 local)
    {
        PlantState best = null;
        float bestD = float.MaxValue;
        var near = new List<PlantState>();
        soilField.State.Near(local, 2f, near);
        foreach (PlantState p in near)
        {
            if (p.data == null || (!p.Alive && p.deadHours > 1e5f)) continue;
            float r = Mathf.Max(0.07f, p.data.Look.width * 0.5f * p.size);
            float d = (p.position - local).magnitude;
            if (d <= r && d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    private void Draw()
    {
        if (soilField == null || view == null) return;
        CraftPixels c = view.Pixels;
        c.Clear(new Color(0.07f, 0.07f, 0.08f, 1f));
        PlantState p = plantId > 0 ? soilField.State.Find(plantId) : null;
        if (p != null && !p.Alive && p.deadHours > 1e5f) p = null;
        switch (mode)
        {
            case Mode.Bed: DrawBed(c); break;
            case Mode.Plant: DrawPlant(c, p); break;
            default: DrawSoil(c, p); break;
        }
        view.Present();
        selectedInfo.text = !spotChosen ? "Click the bed to choose a spot." :
            p != null ? (pieceId > 0 ? $"{p.data.name}: one {AgriText.Part(p.data.Produce.part)} chosen" : p.data.name) + (mode == Mode.Plant ? "  ·  click a fruit, flower or leaf to choose it" : "") :
            "A spot of bare soil";
    }

    private void DrawBed(CraftPixels c)
    {
        SoilGrid g = soilField.State.Grid;
        float angle = ViewAngle;
        Vector2 centre = Centre;
        float half = g.CellSize * 0.5f;
        float radius = ViewRadius * 1.45f;
        for (int i = 0; i < g.Count; i++)
        {
            if (!g.Inside[i]) continue;
            Vector2 ctr = g.Center(i);
            if ((ctr - centre).sqrMagnitude > radius * radius) continue;
            Color col = SoilColour(g, i);
            Vector2 a = ToPixel(ctr + new Vector2(-half, -half), angle, centre), b = ToPixel(ctr + new Vector2(half, -half), angle, centre);
            Vector2 cc = ToPixel(ctr + new Vector2(half, half), angle, centre), d = ToPixel(ctr + new Vector2(-half, half), angle, centre);
            c.Polygon(new[] { a, b, cc, d }, col);
            if (g.Holes[i] > 0) c.Disc(ToPixel(ctr, angle, centre).x, ToPixel(ctr, angle, centre).y, Mathf.Clamp(2f + g.Holes[i] * 0.4f, 2f, 9f), new Color(0.05f, 0.04f, 0.03f));
        }
        List<Vector2> outline = soilField.layout.outline;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            Vector2 a = ToPixel(outline[j], angle, centre), b = ToPixel(outline[i], angle, centre);
            c.Line(a.x, a.y, b.x, b.y, new Color(0.9f, 0.75f, 0.45f, 0.8f), 2f);
        }
        var near = new List<PlantState>();
        soilField.State.Near(centre, radius, near);
        foreach (PlantState p in near)
        {
            if (p.data == null || (!p.Alive && p.deadHours > 1e5f)) continue;
            Vector2 px = ToPixel(p.position, angle, centre);
            if (p.IsSeed)
            {
                if (!p.Alive) continue;
                c.Disc(px.x, px.y, p.covered ? 3f : 2f, p.covered ? new Color(0.2f, 0.14f, 0.09f) : new Color(0.75f, 0.65f, 0.45f));
                continue;
            }
            float r = Mathf.Max(3f, p.data.Look.width * 0.5f * Mathf.Max(0.05f, p.size) * Scale);
            Color leaf = SoilFieldView.Foliage(p, p.data);
            c.Disc(px.x, px.y, r, Color.Lerp(leaf, Color.black, 0.25f));
            c.Disc(px.x, px.y, r * 0.82f, leaf);
            if (p.supported) c.Disc(px.x, px.y, 2f, new Color(0.5f, 0.38f, 0.22f));
            ProduceSpec pr = p.data.Produce;
            foreach (FruitState f in p.fruits)
            {
                float a = (f.slot * 360f * 0.618f + p.id * 37f) * Mathf.Deg2Rad;
                Color fc = f.stage == ProduceStage.Bud && (pr.part == ProducePart.Fruit || pr.part == ProducePart.Grain) ? p.data.Look.flower : pr.ColorAt(f.stage, f.progress);
                c.Disc(px.x + Mathf.Cos(a) * r * 0.55f, px.y + Mathf.Sin(a) * r * 0.55f, Mathf.Max(1.5f, r * 0.16f), fc);
            }
            if (p.id == plantId) c.Ring(px.x, px.y, r + 3f, 2f, UITheme.Gold);
        }
        Vector2 me = ToPixel(centre, angle, centre);
        c.Ring(me.x, me.y, Settings.workRange * Scale, 1.5f, new Color(0.5f, 0.75f, 1f, 0.35f));
        c.Polygon(new[] { new Vector2(me.x, me.y + 9f), new Vector2(me.x - 6f, me.y - 6f), new Vector2(me.x + 6f, me.y - 6f) }, new Color(0.45f, 0.7f, 1f));
        if (spotChosen && plantId == 0)
        {
            Vector2 s = ToPixel(spot, angle, centre);
            c.Ring(s.x, s.y, 6f, 2f, UITheme.Gold);
        }
    }

    private Color SoilColour(SoilGrid g, int i)
    {
        int o = i * SoilGrid.Stride;
        float m = g.Values[o + (int)SoilChannel.Moisture];
        float fc = SoilChemistry.FieldCapacity(g.Values[o + (int)SoilChannel.WaterRetention]);
        Color col = Color.Lerp(SoilTypes.DryColor(g.TypeAt(i)), SoilTypes.WetColor(g.TypeAt(i)), AgriMath.Smooth(0.05f, fc + 0.05f, m));
        col *= 1f - 0.12f * g.Values[o + (int)SoilChannel.Tilth];
        float mulch = g.Values[o + (int)SoilChannel.Mulch];
        if (mulch > 0.02f) col = Color.Lerp(col, new Color(0.8f, 0.7f, 0.45f), Mathf.Clamp01(mulch * 1.4f));
        int top = g.PendingTop(i, out float amount);
        SoilAmendmentData a = top >= 0 ? soilField.State.Catalog?.Amendment(top) : null;
        if (a != null) col = Color.Lerp(col, a.color, Mathf.Clamp01(amount * 0.5f));
        if (g.Values[o + (int)SoilChannel.Stones] > 0.05f && AgriMath.Noise(i, 13) < g.Values[o + (int)SoilChannel.Stones]) col = Color.Lerp(col, new Color(0.62f, 0.6f, 0.56f), 0.55f);
        if (m > 0.93f) col = Color.Lerp(col, new Color(0.25f, 0.3f, 0.32f), 0.35f);
        col.a = 1f;
        return col;
    }

    private void DrawPlant(CraftPixels c, PlantState p)
    {
        pieceHits.Clear();
        float ground = 70f;
        c.Rect(0, 0, Width, ground, new Color(0.24f, 0.18f, 0.12f));
        if (p == null || p.data == null)
        {
            return;
        }
        PlantLook look = p.data.Look;
        ProduceSpec pr = p.data.Produce;
        float k = 380f / Mathf.Max(0.3f, look.height);
        float h = look.height * Mathf.Max(0.05f, p.size) * k * (p.Alive ? 1f - 0.3f * p.Symptom(PlantSymptom.Wilting) : 0.5f);
        float w = look.width * Mathf.Max(0.05f, p.size) * k * 0.5f;
        float cx = Width * 0.5f;
        if (p.IsSeed)
        {
            return;
        }
        Color leaf = SoilFieldView.Foliage(p, p.data);
        Color stem = p.Alive ? look.stem : new Color(0.42f, 0.32f, 0.18f);
        if (p.supported) c.Rect(cx + 14f, ground, cx + 19f, ground + look.height * k * 1.05f, new Color(0.5f, 0.38f, 0.22f));
        c.Line(cx, ground, cx, ground + h, stem, look.shape == PlantShape.Tree ? Mathf.Max(5f, w * 0.15f) : 4f);
        int blobs = look.shape == PlantShape.Tree ? 7 : look.shape == PlantShape.Grain || look.shape == PlantShape.RootCrop ? 0 : 5;
        if (!p.dormant)
            for (int i = 0; i < blobs; i++)
            {
                float t = blobs == 1 ? 0.6f : (float)i / (blobs - 1);
                float y = ground + h * (look.shape == PlantShape.Tree ? 0.45f + 0.45f * t : 0.25f + 0.65f * t);
                float x = cx + Mathf.Sin(i * 2.4f) * w * 0.55f;
                c.Disc(x, y, Mathf.Max(6f, w * (look.shape == PlantShape.Tree ? 0.55f : 0.45f)), Color.Lerp(leaf, Color.black, (i % 2) * 0.08f));
            }
        if (look.shape == PlantShape.Grain || look.shape == PlantShape.RootCrop)
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2) * 0.18f;
                c.Line(cx, ground, cx + Mathf.Sin(a) * h, ground + Mathf.Cos(a) * h, leaf, 3f);
            }
        // Pests, where they sit.
        for (int kk = 0; kk < 8; kk++)
        {
            float load = p.pests[kk];
            if (load < 0.12f || kk == (int)PestKind.RootParasites) continue;
            int n = Mathf.Min(14, Mathf.CeilToInt(load * 12f));
            for (int i = 0; i < n; i++)
            {
                float y = kk >= (int)PestKind.Slugs ? ground + 4f + AgriMath.Noise(p.id, kk, i) * 30f : ground + h * (0.25f + 0.7f * AgriMath.Noise(p.id, kk, i));
                float x = cx + AgriMath.Signed(p.id, kk + 20, i) * w * 0.7f;
                c.Disc(x, y, kk == (int)PestKind.Caterpillars || kk == (int)PestKind.Slugs || kk == (int)PestKind.Snails ? 4f : 2f, PestColour((PestKind)kk));
            }
        }
        // The produce, each piece where it hangs: click to choose it.
        foreach (FruitState f in p.fruits)
        {
            float a = (f.slot * 360f * 0.618f + p.id * 37f) * Mathf.Deg2Rad;
            float frac = 0.35f + 0.55f * ((f.slot * 7.31f) % 1f);
            float x, y;
            if (look.shape == PlantShape.Flower || look.shape == PlantShape.Grain)
            {
                x = cx + Mathf.Cos(a) * w * 0.4f * frac;
                y = ground + h * (0.86f + 0.12f * frac);
            }
            else if (look.shape == PlantShape.Rosette || look.shape == PlantShape.RootCrop)
            {
                x = cx + Mathf.Cos(a) * 6f;
                y = ground + (look.shape == PlantShape.Rosette ? h * 0.3f : 4f);
            }
            else
            {
                x = cx + Mathf.Cos(a) * w * 0.75f;
                y = ground + h * (look.shape == PlantShape.Tree ? 0.45f + 0.4f * frac : frac);
            }
            bool flower = f.stage == ProduceStage.Bud && (pr.part == ProducePart.Fruit || pr.part == ProducePart.Grain);
            Color col = flower ? look.flower : pr.ColorAt(f.stage, f.progress);
            float r = Mathf.Clamp(pr.pieceSize * k * Mathf.Max(0.35f, f.size) * (flower ? 0.7f : 1f), 4f, 34f);
            if (look.shape == PlantShape.RootCrop && f.stage < ProduceStage.Developing) continue;
            c.Disc(x, y, r, Color.Lerp(col, Color.black, 0.25f));
            c.Disc(x, y, r * 0.82f, col);
            if (flower) c.Disc(x, y, r * 0.3f, new Color(1f, 0.85f, 0.25f));
            if (f.stage == ProduceStage.Rotten) c.Disc(x + r * 0.3f, y, r * 0.35f, new Color(0.25f, 0.22f, 0.15f));
            pieceHits.Add((f.id, new Vector2(x, y), r));
            if (f.id == pieceId) c.Ring(x, y, r + 3f, 2f, UITheme.Gold);
        }
        if (p.genes.rare) c.Halo(cx, ground + h * 0.6f, Mathf.Max(30f, w), new Color(0.85f, 0.8f, 1f), 0.25f);
    }

    private static Color PestColour(PestKind k)
    {
        switch (k)
        {
            case PestKind.Aphids: return new Color(0.55f, 0.85f, 0.3f);
            case PestKind.Caterpillars: return new Color(0.4f, 0.7f, 0.2f);
            case PestKind.Mites: return new Color(0.85f, 0.2f, 0.1f);
            case PestKind.Beetles: return new Color(0.15f, 0.2f, 0.12f);
            case PestKind.SapSuckers: return new Color(0.96f, 0.96f, 0.92f);
            case PestKind.Slugs: return new Color(0.45f, 0.35f, 0.25f);
            default: return new Color(0.62f, 0.52f, 0.36f);
        }
    }

    private void DrawSoil(CraftPixels c, PlantState p)
    {
        SoilGrid g = soilField.State.Grid;
        if (!spotChosen || !g.TryIndex(spot, out int cell))
        {
            return;
        }
        int o = cell * SoilGrid.Stride;
        float surface = Height - 110f;
        const float pxPerCm = 10f;
        Color soil = SoilColour(g, cell);
        c.Rect(0, 0, Width, surface, soil);
        // Stones in the body of the soil.
        float stones = g.Values[o + (int)SoilChannel.Stones];
        int n = Mathf.RoundToInt(stones * 60f);
        for (int i = 0; i < n; i++)
            c.Disc(AgriMath.Noise(cell, i, 1) * Width, AgriMath.Noise(cell, i, 2) * (surface - 10f), 4f + AgriMath.Noise(cell, i, 3) * 9f, new Color(0.55f, 0.53f, 0.5f));
        // The worked top: crumbs.
        float tilth = g.Values[o + (int)SoilChannel.Tilth];
        int crumbs = Mathf.RoundToInt(tilth * 80f);
        for (int i = 0; i < crumbs; i++)
            c.Disc(AgriMath.Noise(cell, i, 4) * Width, surface - AgriMath.Noise(cell, i, 5) * 40f * tilth, 2f + AgriMath.Noise(cell, i, 6) * 3f, Color.Lerp(soil, Color.black, 0.2f));
        float mulch = g.Values[o + (int)SoilChannel.Mulch];
        if (mulch > 0.02f) c.Rect(0, surface, Width, surface + 6f + 14f * mulch, new Color(0.82f, 0.72f, 0.45f));
        int top = g.PendingTop(cell, out float amount);
        SoilAmendmentData a = top >= 0 ? soilField.State.Catalog?.Amendment(top) : null;
        if (a != null) c.Rect(0, surface, Width, surface + Mathf.Clamp(amount * 4f, 3f, 14f), a.color);
        if (g.Values[o + (int)SoilChannel.Moisture] > 0.93f) c.Rect(0, surface, Width, surface + 6f, new Color(0.35f, 0.45f, 0.55f, 0.8f));
        // The hole, as deep as it was dug.
        float depth = g.Holes[cell];
        float cx = Width * 0.5f;
        if (depth > 0f)
        {
            float bottom = surface - depth * pxPerCm;
            c.Polygon(new[] { new Vector2(cx - 34f, surface), new Vector2(cx + 34f, surface), new Vector2(cx + 14f, bottom), new Vector2(cx - 14f, bottom) }, new Color(0.06f, 0.05f, 0.04f));
        }
        // A seed the player put here and has not covered yet lies where it was put.
        PlantState seedHere = p != null && p.IsSeed && p.Alive ? p : soilField.State.Nearest(spot, 0.06f);
        if (seedHere != null && seedHere.IsSeed && seedHere.Alive && !seedHere.covered)
            c.Disc(cx, surface - seedHere.depthCm * pxPerCm + 3f, 4f, new Color(0.8f, 0.7f, 0.5f));
        else if (seedHere != null && seedHere.Emerged)
            c.Line(cx, surface, cx, surface + 60f, seedHere.data != null ? seedHere.data.Look.stem : Color.green, 3f);
        // A ruler only with a depth gauge in the bag.
        if (HasDepthGauge())
        {
            c.Rect(20f, surface - 30f * pxPerCm, 34f, surface + 10f, new Color(0.7f, 0.55f, 0.32f));
            for (int cm = 0; cm <= 30; cm++)
            {
                float y = surface - cm * pxPerCm;
                c.Line(34f, y, cm % 5 == 0 ? 50f : 42f, y, new Color(0.15f, 0.1f, 0.05f), cm % 5 == 0 ? 2f : 1f);
            }
        }
        if (digHold != null && digHold.Holding)
        {
            float t = Time.unscaledTime * 12f;
            for (int i = 0; i < 6; i++) c.Disc(cx + Mathf.Sin(t + i) * 40f, surface + 10f + Mathf.Abs(Mathf.Cos(t * 0.7f + i)) * 30f, 3f, Color.Lerp(soil, Color.black, 0.3f));
        }
        if (pourHold != null && pourHold.Holding)
            for (int i = 0; i < 12; i++) c.Line(cx - 30f + i * 5f, Height, cx - 30f + i * 5f + Mathf.Sin(Time.unscaledTime * 9f + i) * 3f, surface + 4f, new Color(0.55f, 0.75f, 0.95f, 0.7f), 1.5f);
    }

    private bool HasDepthGauge()
    {
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv == null) return false;
        foreach (InventoryItem it in AgriToolkit.Stacks(inv))
            if (it != null && it.itemScriptableObject is SoilInstrumentSO s && s.Kind == InstrumentKind.DepthGauge) return true;
        return false;
    }
}
