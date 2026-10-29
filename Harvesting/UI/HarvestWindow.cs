using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The window of the carcass the local player is harvesting (built from code, one per machine).
/// <list type="bullet">
/// <item><b>Left - the body</b>: what the creature was (age, size, rank), how long it has lain and how fast it spoils
/// in this weather, the wounds it took (with Anatomy's Read The Body, what each did inside), and what happened at each
/// harvest.</item>
/// <item><b>Middle - the work surface</b>: while nothing is being worked, the body itself, coloured by the condition of
/// its hide, meat, bones, tendons or organs, its weak points and wounds marked (point at a part to read it, click it to
/// pick what it gives). While a material is worked, its stages one after another - each a small hands-on game with a
/// line of instructions, a gauge on the right (stay in the green) and what the hands feel.</item>
/// <item><b>Right - the materials</b>: everything it gives, grouped by how it is taken, each with its state (ready,
/// taken, destroyed in the fight, spoiled, hidden from an untrained eye, waiting for the hide to come off...), its
/// condition and how long it keeps. Pick one to see its details and its work, choose which pieces, and Harvest.</item>
/// </list>
/// It is an exclusive panel of the player's inventory while shown; walking away stops the work.
/// </summary>
public class HarvestWindow : MonoBehaviour, IExclusivePanel
{
    private static HarvestWindow instance;

    private HarvestableCreature creature;
    private CombatEntity player;
    private InventoryManager registeredWith;
    private bool managesCursor, hudFocus, skillsOpen;
    private CursorLockMode previousLock;
    private bool previousVisible;
    private float refreshTimer, drawTimer, messageTime, waitStart;
    private int seenVersion = -1;

    private Canvas canvas;
    private TextMeshProUGUI title, subtitle, bodyText, logText, regionText, stageTitle, stageText, feedback, detailText, message, skillsText;
    private RectTransform materialList, unitRow, layerRow, stagePanel, inspectPanel, skillsPanel;
    private Button harvestButton, stripButton, stopButton, acceptButton;
    private TextMeshProUGUI harvestLabel;
    private CraftCanvasView view;
    private readonly List<Button> layerButtons = new List<Button>();
    private readonly List<string> log = new List<string>();
    private string materialSignature, materialLayout, unitSignature;
    private readonly List<MaterialRowView> rows = new List<MaterialRowView>();
    // The work the selected material takes, as the details show it (planned again when it changes, or now and then for the tools at hand).
    private HarvestPlan detailsPlan;
    private string detailsKey;
    private float detailsTime;

    /// <summary>A material's row on the list: kept and updated in place (a row rebuilt under the pointer loses its click).</summary>
    private sealed class MaterialRowView
    {
        public int material;
        public Button button;
        public TextMeshProUGUI label;
        public Image icon;
        public string text;
    }

    // Inspection.
    private HarvestInspectLayer layer = HarvestInspectLayer.Overall;
    private int hoverRegion = -1, selected = -1;
    private readonly HashSet<int> chosenRegions = new HashSet<int>();

    // The work.
    private HarvestPlan plan;
    private int stageIndex, workMaterial = -1;
    private HarvestStageModel model;
    private readonly List<HarvestStageResult> results = new List<HarvestStageResult>();
    /// <summary>Waiting for the server: its answer to a harvest (or a strip), or the plan of the work started (<see cref="awaitingPlan"/>).</summary>
    private bool waiting, awaitingPlan;
    private CraftHands hands = new CraftHands(1);

    private const int Order = 58;

    public string PanelName => creature != null && creature.Anatomy != null ? "Harvesting: " + creature.Anatomy.Name : "Harvesting";
    public static bool IsOpen => instance != null && instance.creature != null && instance.canvas.gameObject.activeSelf;
    private HarvestSettings Settings => HarvestDatabase.Instance.Settings;
    private HarvestAnatomyData Anatomy => creature != null ? creature.Anatomy : null;
    private HarvestSkillData Skills => player != null ? HarvestKnowledge.For(player).Data : new HarvestSkillData();

    // ------------------------------------------------------------------ opening
    public static void Show(HarvestableCreature c, CombatEntity p)
    {
        if (c == null || p == null || c.Anatomy == null)
            return;
        if (instance == null)
        {
            InventoryEventSystems.EnsureSingle();
            var go = new GameObject("Harvest Window");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HarvestWindow>();
            instance.Build();
        }
        if (instance.creature != c || instance.player != p)
        {
            instance.Detach();
            instance.creature = c;
            instance.player = p;
            c.Harvested += instance.OnReport;
            c.Planned += instance.OnPlan;
            instance.hands = new CraftHands(c.GetHashCode() ^ p.GetHashCode());
            instance.selected = -1;
            instance.chosenRegions.Clear();
            instance.materialSignature = instance.materialLayout = instance.unitSignature = null;
            instance.detailsPlan = null;
            instance.log.Clear();
            instance.seenVersion = -1;
        }
        instance.canvas.gameObject.SetActive(true);
        instance.Register();
        instance.Refresh(true);
    }

    /// <summary>Closes the window if it shows <paramref name="c"/> (the server ended it, the carcass is gone).</summary>
    public static void Close(HarvestableCreature c)
    {
        if (instance != null && instance.creature == c && c != null)
            instance.Hide();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private void OnDestroy()
    {
        Hide();
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (creature == null || player == null || !creature.isActiveAndEnabled || !player.isActiveAndEnabled || !creature.IsCarcass)
        {
            Hide();
            return;
        }
        Vector3 a = creature.InteractionPoint.position, b = player.transform.position;
        a.y = b.y = 0f;
        if (Vector3.Distance(a, b) > creature.InteractionRange * 1.6f + 0.5f)
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
        if (model != null)
        {
            if (!model.Holding) hands.Rest(dt);
            model.Tick(dt);
            if (model.Done)
                NextStage();
        }
        else
            hands.Rest(dt);
        if (waiting && Time.unscaledTime - waitStart > 12f)
        {
            if (awaitingPlan)
                StopWork(false);
            waiting = false;
            Message("No answer: the work was lost.");
            Refresh(true);
        }
        drawTimer += dt;
        if (drawTimer >= 1f / 30f)
        {
            drawTimer = 0f;
            Draw();
        }
        refreshTimer += dt;
        if (refreshTimer >= 0.25f || creature.Version != seenVersion)
        {
            refreshTimer = 0f;
            Refresh(false);
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
        StopWork(true);
        Detach();
        SetHudFocus(false);
        Unregister();
        if (canvas != null && canvas.gameObject.activeSelf)
            canvas.gameObject.SetActive(false);
    }

    private void Detach()
    {
        if (creature != null)
        {
            creature.Harvested -= OnReport;
            creature.Planned -= OnPlan;
            if (player != null) creature.CancelAll(player);
        }
        creature = null;
        player = null;
        skillsOpen = false;
        waiting = awaitingPlan = false;
    }

    private void Register()
    {
        InputBindingStore.BlockGameplayInput(this);
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
        if (skillsOpen)
        {
            skillsOpen = false;
            Refresh(true);
            return;
        }
        if (model != null)
        {
            StopWork(true);
            Refresh(true);
            return;
        }
        Hide();
    }

    // ------------------------------------------------------------------ building
    private void Build()
    {
        canvas = UITheme.Canvas("Harvesting", Order, transform);
        Transform c = canvas.transform;
        Image backdrop = UITheme.Panel("Backdrop", c, new Color(0f, 0f, 0f, 0.35f), 0);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        Image window = UITheme.Window("Window", c, new Vector2(1620f, 960f));
        Transform w = window.transform;

        title = UITheme.Label(w, "", 28f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -56f), new Vector2(-380f, -14f));
        subtitle = UITheme.Label(w, "", 15f, UITheme.Muted, TextAlignmentOptions.Left, FontStyles.Normal, "Subtitle");
        UITheme.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -84f), new Vector2(-380f, -54f));
        RectTransform headerButtons = UITheme.Area("Header Buttons", w, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-360f, -64f), new Vector2(-24f, -18f));
        InventoryUIFactory.Horizontal(headerButtons.gameObject, 10f).childForceExpandWidth = true;
        UITheme.Button("Skills", headerButtons, "Skills", () => { skillsOpen = !skillsOpen; Refresh(true); }, UITheme.ButtonStyle.Primary, 0f, 42f);
        UITheme.Button("Close", headerButtons, "Close", Hide, UITheme.ButtonStyle.Normal, 0f, 42f);

        // Left: the body.
        RectTransform left = Column(w, "Body", 24f, 404f);
        UITheme.Header(left, "The body");
        RectTransform bodyArea = UITheme.ScrollList("Body Text", left, 470f, out _);
        bodyText = UITheme.Label(bodyArea, "", 14f, UITheme.Text);
        UITheme.Header(left, "What happened");
        RectTransform logArea = UITheme.ScrollList("Log", left, 0f, out _);
        logText = UITheme.Label(logArea, "", 13f, UITheme.Muted);

        // Middle: the work surface.
        RectTransform mid = Column(w, "Work", 424f, 1176f);
        Image surfaceBack = UITheme.Panel("Surface", mid, new Color(0f, 0f, 0f, 0.5f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(surfaceBack, -1f, 408f);
        var raw = new GameObject("Work Surface", typeof(RectTransform), typeof(RawImage));
        raw.transform.SetParent(surfaceBack.transform, false);
        InventoryUIFactory.Stretch((RectTransform)raw.transform, 4f, 4f);
        view = raw.AddComponent<CraftCanvasView>();
        view.Build(HarvestPlanner.Width, HarvestPlanner.Height);
        view.Pointer += OnPointer;
        feedback = UITheme.Label(mid, "", 16f, UITheme.Text, TextAlignmentOptions.Center, FontStyles.Bold, "Feedback");
        InventoryUIFactory.Size(feedback, -1f, 24f);

        // Under the surface while inspecting: the tissue to colour by, and the part pointed at.
        inspectPanel = UITheme.Column("Inspect", mid, 0, 6f);
        InventoryUIFactory.Size(inspectPanel, -1f, 300f).flexibleHeight = 1f;
        layerRow = UITheme.Row("Layers", inspectPanel, 34f, 4f);
        layerRow.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        foreach (HarvestInspectLayer l in Enum.GetValues(typeof(HarvestInspectLayer)))
        {
            HarvestInspectLayer layerOf = l;
            layerButtons.Add(UITheme.Button(l.ToString(), layerRow, l.ToString(), () => { layer = layerOf; Refresh(true); }, UITheme.ButtonStyle.Tab, 0f, 34f));
        }
        regionText = UITheme.Label(inspectPanel, "", 14f, UITheme.Text);
        InventoryUIFactory.Size(regionText, -1f, 200f);

        // Under the surface while working: the stage and its instructions.
        stagePanel = UITheme.Column("Stage", mid, 0, 6f);
        InventoryUIFactory.Size(stagePanel, -1f, 300f).flexibleHeight = 1f;
        stageTitle = UITheme.Label(stagePanel, "", 18f, UITheme.Gold, TextAlignmentOptions.Left, FontStyles.Bold, "Stage Title");
        InventoryUIFactory.Size(stageTitle, -1f, 28f);
        stageText = UITheme.Label(stagePanel, "", 15f, UITheme.Text, TextAlignmentOptions.TopLeft, FontStyles.Normal, "Instruction");
        InventoryUIFactory.Size(stageText, -1f, 120f);
        RectTransform stageButtons = UITheme.Row("Stage Buttons", stagePanel, 40f, 8f);
        stageButtons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        acceptButton = UITheme.Button("Accept", stageButtons, "Accept as it is", AcceptStage, UITheme.ButtonStyle.Primary, 0f, 40f);
        stopButton = UITheme.Button("Stop", stageButtons, "Stop (lose this work)", () => { StopWork(true); Refresh(true); }, UITheme.ButtonStyle.Danger, 0f, 40f);

        // Right: the materials.
        RectTransform right = Column(w, "Materials", 1196f, 1596f);
        UITheme.Header(right, "What it gives");
        materialList = UITheme.ScrollList("Material List", right, 360f, out _, null, 4, 4f);
        UITheme.Header(right, "Selected");
        RectTransform detailArea = UITheme.ScrollList("Details", right, 0f, out _);
        detailText = UITheme.Label(detailArea, "", 14f, UITheme.Text);
        unitRow = UITheme.Column("Pieces", right, 0, 4f);
        RectTransform actions = UITheme.Row("Actions", right, 44f, 8f);
        actions.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        harvestButton = UITheme.Button("Harvest", actions, "Harvest", StartWork, UITheme.ButtonStyle.Primary, 0f, 44f);
        harvestLabel = harvestButton.GetComponentInChildren<TextMeshProUGUI>(true);
        stripButton = UITheme.Button("Strip", actions, "Hack the hide off", StripHide, UITheme.ButtonStyle.Danger, 0f, 44f);

        message = UITheme.Label(w, "", 15f, UITheme.Gold, TextAlignmentOptions.Center);
        UITheme.Place(message.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(424f, 2f), new Vector2(-444f, 22f));

        // The skills, over the whole window.
        Image sp = UITheme.Panel("Skills", w, UITheme.Surface, UITheme.RadiusLarge);
        skillsPanel = sp.rectTransform;
        UITheme.Place(skillsPanel, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -96f));
        RectTransform scol = UITheme.Column("Column", sp.transform, 16, 10f);
        InventoryUIFactory.Stretch(scol);
        RectTransform slist = UITheme.ScrollList("Pages", scol, 0f, out _);
        skillsText = UITheme.Label(slist, "", 15f, UITheme.Text);
        skillsPanel.gameObject.SetActive(false);
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

    // ------------------------------------------------------------------ the surface
    private void OnPointer(CraftPointer phase, Vector2 p, bool rightButton, float scroll)
    {
        if (model != null)
        {
            if (rightButton || phase == CraftPointer.Scroll)
                return;
            HarvestPointer hp = phase == CraftPointer.Down ? HarvestPointer.Down : phase == CraftPointer.Drag ? HarvestPointer.Drag :
                phase == CraftPointer.Up ? HarvestPointer.Up : HarvestPointer.Move;
            model.OnPointer(hp, p);
            return;
        }
        HarvestAnatomyData a = Anatomy;
        if (a == null)
            return;
        hoverRegion = HarvestInspectView.RegionAt(a, p);
        if (phase == CraftPointer.Down && hoverRegion >= 0)
            SelectFromRegion(hoverRegion);
    }

    /// <summary>Clicking a part picks what it gives (again: the next thing it gives).</summary>
    private void SelectFromRegion(int region)
    {
        HarvestAnatomyData a = Anatomy;
        string rid = a.regions[region].id;
        var from = new List<int>();
        for (int i = 0; i < a.materials.Count; i++)
        {
            HarvestMaterial m = a.materials[i];
            if (m == null || !Shown(i)) continue;
            foreach (HarvestSource s in m.sources)
                if (string.Equals(s.region, rid, StringComparison.OrdinalIgnoreCase)) { from.Add(i); break; }
        }
        if (from.Count == 0)
            return;
        int at = from.IndexOf(selected);
        Select(from[(at + 1) % from.Count]);
    }

    private void Draw()
    {
        if (view == null || view.Pixels == null)
            return;
        CraftPixels c = view.Pixels;
        c.Clear(new Color(0.05f, 0.05f, 0.07f, 1f));
        float t = Time.unscaledTime;
        if (model != null)
            HarvestStageViews.Draw(c, model, t);
        else if (Anatomy != null)
            HarvestInspectView.Draw(c, Anatomy, creature.Condition, layer, SourcesOf(selected), hoverRegion, t);
        DrawArm(c);
        view.Present();
    }

    /// <summary>The arm, top right: a bar that empties as it tires.</summary>
    private void DrawArm(CraftPixels c)
    {
        float f = (float)hands.fatigue, x = c.Width - 96f, y = c.Height - 14f;
        Color col = f < 0.5f ? new Color(0.45f, 0.85f, 0.5f) : f < 0.8f ? new Color(1f, 0.8f, 0.35f) : new Color(1f, 0.4f, 0.3f);
        c.Disc(x - 8f, y + 3f, 5f, new Color(0.86f, 0.66f, 0.52f, 0.9f));
        c.Rect(x, y, x + 86f, y + 7f, new Color(0f, 0f, 0f, 0.6f));
        c.Rect(x + 1f, y + 1f, x + 1f + 84f * Mathf.Clamp01(1f - f), y + 6f, col);
    }

    private HashSet<int> SourcesOf(int material)
    {
        var set = new HashSet<int>();
        HarvestAnatomyData a = Anatomy;
        if (a == null || material < 0 || material >= a.materials.Count)
            return set;
        foreach (HarvestSource s in a.materials[material].sources)
        {
            int r = a.RegionIndex(s.region);
            if (r >= 0) set.Add(r);
        }
        return set;
    }

    // ------------------------------------------------------------------ the work
    private void Select(int material)
    {
        selected = material;
        chosenRegions.Clear();
        unitSignature = null;
        Refresh(true);
    }

    /// <summary>The pieces chosen of the selected (countable) material: those of the chosen parts (none chosen = all).</summary>
    private List<int> ChosenUnits()
    {
        var list = new List<int>();
        HarvestAnatomyData a = Anatomy;
        if (a == null || selected < 0 || !a.materials[selected].countable || chosenRegions.Count == 0)
            return list;
        foreach (HarvestUnit u in creature.Condition.units)
            if (u.material == selected && !u.taken && u.integrity >= Settings.destroyedBelow && chosenRegions.Contains(u.region))
                list.Add(u.index);
        return list;
    }

    private void StartWork()
    {
        if (creature == null || selected < 0 || model != null || waiting)
            return;
        List<int> units = ChosenUnits();
        HarvestWorker worker = creature.WorkerFor(player);
        HarvestPlan p = creature.PlanFor(player, selected, units, worker);
        if (!p.Valid)
        {
            Message(p.refusal);
            return;
        }
        if (!creature.Begin(player, selected, units, out string reason))
        {
            Message(reason);
            return;
        }
        workMaterial = selected;
        if (!creature.RulesHere)
        {
            // A network client: the server plans the work from its own carcass and the hands play its plan (OnPlan).
            awaitingPlan = waiting = true;
            waitStart = Time.unscaledTime;
            Refresh(true);
            return;
        }
        Play(p, worker.skills);
    }

    /// <summary>The server's plan of the work this client started.</summary>
    private void OnPlan(CombatEntity who, HarvestPlan p)
    {
        if (who != player || !awaitingPlan || p == null || !p.Valid || Anatomy == null || workMaterial < 0 ||
            !string.Equals(p.material, Anatomy.materials[workMaterial].id, StringComparison.OrdinalIgnoreCase))
            return;
        awaitingPlan = waiting = false;
        Play(p, Skills);
    }

    private void Play(HarvestPlan p, HarvestSkillData skills)
    {
        plan = p;
        stageIndex = 0;
        results.Clear();
        hands.skill = HarvestSkills.Factor(Settings, skills, Anatomy.materials[workMaterial].Skill);
        model = HarvestStageModel.Create(plan.stages[0], hands);
        Refresh(true);
    }

    private void NextStage()
    {
        results.AddRange(model.Finish());
        stageIndex++;
        if (plan != null && stageIndex < plan.stages.Count)
        {
            model = HarvestStageModel.Create(plan.stages[stageIndex], hands);
            Refresh(true);
            return;
        }
        // Done: the work goes to be resolved (here, or on the server).
        var result = new HarvestTaskResult { material = plan.material, units = new List<int>(plan.units) };
        result.stages.AddRange(results);
        model = null;
        plan = null;
        results.Clear();
        workMaterial = -1;
        waiting = true;
        waitStart = Time.unscaledTime;
        creature.Submit(player, result);
        Refresh(true);
    }

    private void AcceptStage()
    {
        if (model is HarvestScrapeModel s)
            s.Accept();
    }

    /// <summary>Stops the work in hand (nothing is taken; the material is free again).</summary>
    private void StopWork(bool tell)
    {
        if (model == null && plan == null && !awaitingPlan)
            return;
        int m = workMaterial;
        model = null;
        plan = null;
        results.Clear();
        workMaterial = -1;
        if (awaitingPlan)
            awaitingPlan = waiting = false;
        if (creature != null && player != null && m >= 0)
            creature.Cancel(player, m);
        if (tell)
            Message("You stop: nothing is taken.");
    }

    private void StripHide()
    {
        if (creature == null || model != null || waiting)
            return;
        waiting = true;
        waitStart = Time.unscaledTime;
        creature.StripHide(player);
    }

    private void OnReport(CombatEntity who, HarvestReport r)
    {
        if (who != player || r == null)
            return;
        waiting = false;
        if (!r.accepted)
        {
            // A refusal of the work in hand (the server said no to its start, or to its result).
            bool started = model != null && plan != null && string.Equals(plan.material, r.material, StringComparison.OrdinalIgnoreCase) ||
                           awaitingPlan && workMaterial >= 0 && string.Equals(Anatomy.materials[workMaterial].id, r.material, StringComparison.OrdinalIgnoreCase);
            if (started)
            {
                model = null;
                plan = null;
                results.Clear();
                workMaterial = -1;
                awaitingPlan = false;
            }
            Message(r.refusal);
            AddLog($"<color=#f0a090>{HarvestText.Capital(r.materialName)}: {r.refusal}</color>");
            Refresh(true);
            return;
        }
        var sb = new StringBuilder();
        if (r.pieces.Count > 0)
            foreach (HarvestReportPiece p in r.pieces)
            {
                string grade = $"<color={HarvestText.GradeHex(p.Grade)}>{p.Grade} ({p.quality * 100f:0}%)</color>";
                sb.Append($"<b>{p.name}</b>{(p.count > 1 ? $" ×{p.count}" : "")} - {grade}");
                if (p.tanneryHide) sb.Append(" <color=#9aa0a6>(a raw hide for the tannery)</color>");
                string story = HarvestMarksInfo.Story((HarvestMarks)p.marks);
                if (story.Length > 0) sb.Append($"\n   <color=#c9b38a>{story}.</color>");
                sb.Append('\n');
            }
        foreach (string line in r.lines)
            sb.Append($"<color=#9aa0a6>{line}</color>\n");
        if (r.xp > 0f)
            sb.Append($"<color=#8fd68f>+{r.xp:0} {HarvestText.SkillName((HarvestSkill)r.skill)}, +{r.anatomyXp:0} Anatomy</color>\n");
        foreach (string l in r.learnt)
            sb.Append($"<color=#ffcc61>{l}</color>\n");
        AddLog(sb.ToString().TrimEnd());
        Message(r.pieces.Count > 0 ? $"{HarvestText.Capital(r.materialName)}: {r.Count} piece{(r.Count == 1 ? "" : "s")} taken." : string.Join(" ", r.lines));
        Refresh(true);
    }

    private void AddLog(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        log.Add(text);
        if (log.Count > 30) log.RemoveAt(0);
    }

    public void Message(string text)
    {
        if (message == null)
            return;
        message.text = text ?? "";
        messageTime = Time.unscaledTime;
    }

    // ------------------------------------------------------------------ refreshing
    /// <summary>Is a material listed (a rare find the creature does not have, or one gone before the player could tell it was there, is not)?</summary>
    private bool Shown(int material) =>
        creature.Carcass != null && creature.Carcass.Availability(Anatomy, Settings, material, Skills, HarvestableCreature.PlayerKey(player), out _) != HarvestAvailability.Missing;

    private void Refresh(bool force)
    {
        if (creature == null || player == null || Anatomy == null || canvas == null)
            return;
        seenVersion = creature.Version;
        HarvestAnatomyData a = Anatomy;
        HarvestCarcass k = creature.Carcass;
        HarvestCondition cond = creature.Condition;
        HarvestSkillData skills = Skills;
        title.text = $"{a.Name} carcass";
        subtitle.text = Subtitle(a, k);

        bool working = model != null;
        skillsPanel.gameObject.SetActive(skillsOpen);
        if (skillsOpen)
            skillsText.text = SkillsPage(skills);
        inspectPanel.gameObject.SetActive(!working);
        stagePanel.gameObject.SetActive(working);
        for (int i = 0; i < layerButtons.Count; i++)
            UITheme.SetSelected(layerButtons[i], (int)layer == i);

        bodyText.text = BodyPage(a, k, cond, skills);
        logText.text = log.Count == 0 ? "Nothing taken yet." : string.Join("\n\n", log.ToArray());
        if (working)
            RefreshStage();
        else
        {
            feedback.text = waiting ? "<color=#9aa0a6>…</color>" : "";
            regionText.text = RegionPage(a, cond, hoverRegion, skills);
        }
        RefreshMaterials(force, a, k, skills);
        RefreshDetails(a, k, skills);
    }

    private string Subtitle(HarvestAnatomyData a, HarvestCarcass k)
    {
        if (k == null)
            return "";
        HarvestCondition c = k.condition;
        var parts = new List<string> { c.age.ToString() };
        if (Mathf.Abs(c.sizeScale - 1f) > 0.08f) parts.Add($"{c.sizeScale:0.0}× size");
        if (c.elite) parts.Add("<color=#ffcc61>elite</color>");
        parts.Add($"dead {HarvestText.Minutes(k.age / 60.0)}");
        double rate = HarvestCarcass.SpoilRate(Settings, k.temperature);
        string spoil = rate <= 0 ? "<color=#8ccfff>frozen: it keeps</color>" : rate > 1.3 ? $"<color=#f0a090>spoiling fast (×{rate:0.0})</color>" :
            rate < 0.75 ? $"<color=#8fd68f>spoiling slowly (×{rate:0.0})</color>" : "spoiling";
        parts.Add($"{k.temperature:0} °C, {spoil}");
        if (k.gutted) parts.Add("gutted");
        double left = creature.MinutesLeft;
        if (creature.IsPractice)
            parts.Add("<color=#8ccfff>a practice body</color>");
        else
            parts.Add(left < 3 ? $"<color=#f0a090>gone in {HarvestText.Minutes(left)}</color>" : $"stays {HarvestText.Minutes(left)}");
        return string.Join(" · ", parts);
    }

    private string BodyPage(HarvestAnatomyData a, HarvestCarcass k, HarvestCondition c, HarvestSkillData skills)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(a.description))
            sb.Append($"<color=#9aa0a6>{a.description}</color>\n\n");
        bool read = HarvestSkills.Knows(Settings, skills, HarvestSkills.ReadTheBody);
        if (c == null || c.wounds.Count == 0)
            sb.Append("No wounds: an unmarked body.\n");
        else
        {
            sb.Append($"<b>{c.wounds.Count} wound{(c.wounds.Count == 1 ? "" : "s")}</b>{(read ? "" : " <color=#9aa0a6>(Anatomy 5 to read what they did)</color>")}\n");
            for (int i = c.wounds.Count - 1; i >= 0; i--)
            {
                HarvestWound w = c.wounds[i];
                string where = w.region >= 0 && w.region < a.regions.Count ? a.regions[w.region].Name.ToLowerInvariant() : "the body";
                string what = w.periodic ? (w.element == ElementType.Poison ? "Poison" : w.element == ElementType.Fire ? "Burning" : $"{w.element}") :
                    w.area ? "A blast" : FormWord(w.form, w.element);
                Color sev = HarvestText.ConditionColor(1.0 - Math.Min(1.0, w.severity));
                sb.Append($"<color={HarvestText.Hex(sev)}>■</color> {what} in the {where}");
                if (!string.IsNullOrEmpty(w.source)) sb.Append($" <color=#9aa0a6>({w.source})</color>");
                if (w.critical) sb.Append(" <color=#ffcc61>critical</color>");
                if (read && !string.IsNullOrEmpty(w.effect)) sb.Append($"\n   <color=#c9b38a>{HarvestText.Capital(w.effect)}.</color>");
                sb.Append('\n');
            }
        }
        if (k != null)
        {
            if (k.Soiled) sb.Append("\n<color=#b8c870>A gut was torn: the cavity is soiled (the meat spoils faster).</color>\n");
            if (!k.hideOff && HarvestCarcass.HasHide(a)) sb.Append("\n<color=#9aa0a6>The hide is on: skin it (or hack it off) before the meat, bones and tendons.</color>\n");
        }
        return sb.ToString();
    }

    private static string FormWord(DamageForm f, ElementType e)
    {
        string el = e == ElementType.Fire ? "burning " : e == ElementType.Ice ? "freezing " : e == ElementType.Lightning ? "scorching " : e == ElementType.Poison ? "poisoned " : "";
        switch (f)
        {
            case DamageForm.Slash: return HarvestText.Capital(el + "cut");
            case DamageForm.Pierce: return HarvestText.Capital(el + "piercing wound");
            case DamageForm.Blunt: return HarvestText.Capital(el + "blow");
            case DamageForm.Bite: return HarvestText.Capital(el + "bite");
            case DamageForm.Claw: return HarvestText.Capital(el + "clawing");
            case DamageForm.Crush: return HarvestText.Capital(el + "crushing blow");
            default: return el.Length > 0 ? HarvestText.Capital(el.Trim() + " wound") : "A wound";
        }
    }

    private string RegionPage(HarvestAnatomyData a, HarvestCondition c, int r, HarvestSkillData skills)
    {
        if (r < 0 || r >= a.regions.Count || c == null)
            return "<color=#9aa0a6>Point at a part of the body to read it; click it to pick what it gives. The colours show the " + layer.ToString().ToLowerInvariant() +
                   ": <color=#66d173>whole</color>, <color=#f2bf40>damaged</color>, <color=#d94033>ruined</color>; grey where there is none. " +
                   "<color=#ff7350>◎</color> marks a weak point, <color=#8c0d0d>✕</color> a wound.</color>";
        HarvestRegion reg = a.regions[r];
        HarvestRegionState st = c.regions[r];
        var sb = new StringBuilder();
        sb.Append($"<b>{reg.Name}</b>");
        if (reg.weakPoint) sb.Append($"  <color=#ff7350>weak point ×{reg.damageMultiplier:0.0}</color>");
        else if (Mathf.Abs(reg.damageMultiplier - 1f) > 0.05f) sb.Append($"  <color=#9aa0a6>hits ×{reg.damageMultiplier:0.0}</color>");
        if (reg.armor > 0.05f) sb.Append($"  <color=#8ccfff>armoured</color>");
        sb.Append('\n');
        void Bar(string name, float v)
        {
            if (v < 0f) return;
            sb.Append($"{name} <color={HarvestText.Hex(HarvestText.ConditionColor(v))}>{HarvestText.Bar(v, 8)} {v * 100f:0}%</color>   ");
        }
        Bar("Hide", HarvestInspectView.Value(a, c, r, HarvestInspectLayer.Hide));
        Bar("Meat", HarvestInspectView.Value(a, c, r, HarvestInspectLayer.Meat));
        sb.Append('\n');
        Bar("Bone", HarvestInspectView.Value(a, c, r, HarvestInspectLayer.Bone));
        Bar("Tendons", HarvestInspectView.Value(a, c, r, HarvestInspectLayer.Tendons));
        Bar("Organs", HarvestInspectView.Value(a, c, r, HarvestInspectLayer.Organs));
        sb.Append('\n');
        string marks = HarvestMarksInfo.Describe(st.marks | c.HideMarks(r) | st.CoverMarks(), HarvestMarksInfo.Combat);
        if (marks.Length > 0) sb.Append($"<color=#c9b38a>{HarvestText.Capital(marks)}.</color>\n");
        if (st.contamination > 0.3f) sb.Append("<color=#b8c870>Soiled by a torn gut.</color>\n");
        if (st.poison > 0.12f) sb.Append("<color=#b8c870>Poisoned: its meat is not fit to eat.</color>\n");
        if (st.taint > 0.12f) sb.Append("<color=#b8a8e0>Tainted by dark magic: its meat is not fit to eat.</color>\n");
        if (st.burn > 0.3f) sb.Append("<color=#f0a090>Charred.</color>\n");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ the materials
    private void RefreshMaterials(bool force, HarvestAnatomyData a, HarvestCarcass k, HarvestSkillData skills)
    {
        if (k == null)
            return;
        int key = HarvestableCreature.PlayerKey(player);
        // The rows are built again only when what is listed changes; their states, counts and freshness change in place.
        var layout = new StringBuilder(a.id).Append('|');
        var sig = new StringBuilder();
        sig.Append(selected).Append('|');
        for (int i = 0; i < a.materials.Count; i++)
        {
            HarvestAvailability av = k.Availability(a, Settings, i, skills, key, out _);
            if (av == HarvestAvailability.Missing) continue;
            layout.Append(i).Append(',');
            sig.Append(i).Append(':').Append((int)av).Append(':').Append(k.UnitsLeft(Settings, i)).Append(':').Append((int)(k.Freshness(a.materials[i]) * 20)).Append(';');
        }
        string l = layout.ToString();
        if (l != materialLayout)
        {
            materialLayout = l;
            BuildMaterialRows(a);
            force = true;
        }
        string s = sig.ToString();
        if (!force && s == materialSignature)
            return;
        materialSignature = s;
        foreach (MaterialRowView row in rows)
            UpdateRow(a, k, row, skills, key);
    }

    private void BuildMaterialRows(HarvestAnatomyData a)
    {
        rows.Clear();
        for (int i = materialList.childCount - 1; i >= 0; i--)
            Destroy(materialList.GetChild(i).gameObject);
        foreach (HarvestMethod method in Enum.GetValues(typeof(HarvestMethod)))
        {
            bool header = false;
            for (int i = 0; i < a.materials.Count; i++)
            {
                HarvestMaterial m = a.materials[i];
                if (m == null || m.method != method || !Shown(i)) continue;
                if (!header)
                {
                    UITheme.Header(materialList, HarvestText.MethodName(method), 22f);
                    header = true;
                }
                rows.Add(MaterialRow(a, i));
            }
        }
    }

    private MaterialRowView MaterialRow(HarvestAnatomyData a, int i)
    {
        HarvestMaterial m = a.materials[i];
        int index = i;
        Button row = UITheme.Button("Material " + m.id, materialList, "", () => Select(index), UITheme.ButtonStyle.ListItem, 0f, 52f);
        TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 14f;
            UITheme.Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(48f, 2f), new Vector2(-6f, -2f));
        }
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(row.transform, false);
        var icon = iconGo.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        UITheme.Place((RectTransform)iconGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, -18f), new Vector2(42f, 18f));
        return new MaterialRowView { material = i, button = row, label = label, icon = icon };
    }

    private void UpdateRow(HarvestAnatomyData a, HarvestCarcass k, MaterialRowView row, HarvestSkillData skills, int key)
    {
        int i = row.material;
        if (row.button == null || i < 0 || i >= a.materials.Count)
            return;
        HarvestAvailability av = k.Availability(a, Settings, i, skills, key, out string why);
        UITheme.SetSelected(row.button, i == selected);
        string text = RowText(a, k, i, av, why);
        if (row.label != null && text != row.text)
        {
            row.label.text = text;
            row.text = text;
        }
        if (row.icon != null)
        {
            row.icon.sprite = av == HarvestAvailability.Hidden ? null : HarvestArt.Icon(a, a.materials[i]);
            row.icon.color = av == HarvestAvailability.Hidden ? new Color(0.5f, 0.4f, 0.7f, 0.6f) : av == HarvestAvailability.Available ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }
    }

    private string RowText(HarvestAnatomyData a, HarvestCarcass k, int i, HarvestAvailability av, string why)
    {
        HarvestMaterial m = a.materials[i];
        if (av == HarvestAvailability.Hidden)
            return $"<color=#b8a8e0><b>Something unusual</b></color>\n<size=12><color=#9aa0a6>{why}</color></size>";
        string name = $"<b>{m.Name}</b>";
        if (m.countable)
        {
            int left = k.UnitsLeft(Settings, i), total = 0;
            foreach (HarvestUnit u in k.condition.units) if (u.material == i) total++;
            name += $" <color=#9aa0a6>{left}/{total}</color>";
        }
        string state;
        switch (av)
        {
            case HarvestAvailability.Available:
            {
                double c = k.CombatIntegrity(a, i);
                state = $"<color={HarvestText.Hex(HarvestText.ConditionColor(c))}>{HarvestText.Bar(c, 6)}</color> {Keeps(k, m)}";
                break;
            }
            case HarvestAvailability.Taken: state = "<color=#7a7f88>taken</color>"; break;
            case HarvestAvailability.Destroyed: state = $"<color=#f0a090>{why}</color>"; break;
            case HarvestAvailability.Spoiled: state = "<color=#a08a60>spoiled</color>"; break;
            case HarvestAvailability.NeedsHide: state = "<color=#ffcc61>the hide must come off first</color>"; break;
            case HarvestAvailability.Busy: state = "<color=#8ccfff>someone is working on it</color>"; break;
            default: state = $"<color=#ffcc61>{why}</color>"; break;
        }
        return $"{name}\n<size=12>{state}</size>";
    }

    private string Keeps(HarvestCarcass k, HarvestMaterial m)
    {
        if (m.spoilMinutes <= 0f) return "<color=#9aa0a6>keeps</color>";
        double fresh = k.Freshness(m);
        double turn = k.MinutesUntilTurning(Settings, m);
        if (fresh < Settings.spoilingBelow) return $"<color=#d9a05a>turning ({fresh * 100:0}% fresh)</color>";
        return double.IsInfinity(turn) ? "<color=#8ccfff>frozen</color>" : $"<color=#9aa0a6>turns in {HarvestText.Minutes(turn)}</color>";
    }

    private void RefreshDetails(HarvestAnatomyData a, HarvestCarcass k, HarvestSkillData skills)
    {
        bool working = model != null;
        harvestButton.gameObject.SetActive(!working);
        bool strip = false;
        if (k != null && !k.hideOff)
        {
            int key = HarvestableCreature.PlayerKey(player);
            for (int i = 0; i < a.materials.Count && !strip; i++)
                if (k.Availability(a, Settings, i, skills, key, out _) == HarvestAvailability.NeedsHide) strip = true;
            // Not while someone else is skinning it, plucking it or scraping it (the server refuses that too).
            if (strip && k.HideWorkedBy(a, Settings, key) != 0) strip = false;
        }
        stripButton.gameObject.SetActive(!working && strip);
        if (k == null || selected < 0 || selected >= a.materials.Count || !Shown(selected))
        {
            detailText.text = "<color=#9aa0a6>Pick a material on the list (or a part of the body) to see it and how it is taken.</color>";
            harvestButton.interactable = false;
            if (harvestLabel != null) harvestLabel.text = "Harvest";
            RefreshUnits(a, k, true);
            return;
        }
        HarvestMaterial m = a.materials[selected];
        HarvestAvailability av = k.Availability(a, Settings, selected, skills, HarvestableCreature.PlayerKey(player), out string why);
        if (av == HarvestAvailability.Hidden)
        {
            detailText.text = $"<color=#b8a8e0>Something unusual is here, but you cannot tell what.</color>\n<color=#9aa0a6>{why}</color>";
            harvestButton.interactable = false;
            RefreshUnits(a, k, true);
            return;
        }
        var sb = new StringBuilder();
        sb.Append($"<b>{m.Name}</b> <color=#9aa0a6>· {HarvestText.KindName(m.kind)} · {HarvestText.MethodName(m.method)}</color>\n");
        if (!string.IsNullOrEmpty(m.description)) sb.Append($"<color=#9aa0a6>{m.description}</color>\n");
        double integrity = k.CombatIntegrity(a, selected);
        if (av == HarvestAvailability.Available || av == HarvestAvailability.NeedsHide || av == HarvestAvailability.Blocked || av == HarvestAvailability.NeedsSkill)
        {
            sb.Append($"From the fight: {HarvestText.Quality(integrity)}");
            string marks = HarvestMarksInfo.Describe(k.CombatMarks(a, selected), HarvestMarksInfo.Combat);
            if (marks.Length > 0) sb.Append($" <color=#c9b38a>({marks})</color>");
            sb.Append('\n');
            if (m.spoilMinutes > 0f) sb.Append($"Freshness: {k.Freshness(m) * 100:0}% · {Keeps(k, m)}\n");
            if (m.method == HarvestMethod.Bones)
            {
                List<string> risk = HarvestResolver.MeatAtRisk(a, Settings, k, m);
                if (risk.Count > 0) sb.Append($"<color=#ffcc61>Cutting it out now tears up the {string.Join(", ", risk).ToLowerInvariant()} around it: take the meat first.</color>\n");
            }
        }
        else
            sb.Append($"<color=#f0a090>{why}</color>\n");
        int level = HarvestSkills.Level(Settings, skills, m.Skill);
        sb.Append($"<color=#9aa0a6>{HarvestText.SkillName(m.Skill)} {level}{(m.minSkill > 0 ? $" (needs {m.minSkill})" : "")} · difficulty {HarvestText.Bar(m.difficulty, 5)}");
        if (HarvestSkills.Familiarity(Settings, skills, a.id) >= 0.5 || (skills.Species(a.id)?.known.Contains(m.id) ?? false))
            sb.Append($" · worth up to {m.value:0.#} each");
        sb.Append("</color>\n");
        if (av == HarvestAvailability.Available)
        {
            HarvestPlan p = working && plan != null && workMaterial == selected ? plan : DetailsPlan();
            if (p.Valid)
            {
                sb.Append("\n<b>The work</b>\n");
                for (int s = 0; s < p.stages.Count && s < 8; s++)
                {
                    HarvestStageSpec st = p.stages[s];
                    sb.Append($"{s + 1}. {st.title} <color=#9aa0a6>- {st.toolName} ({HarvestTools.FitnessWord(st.toolFitness, Settings.wrongToolBelow)})</color>\n");
                }
                if (p.stages.Count > 8) sb.Append($"<color=#9aa0a6>… and {p.stages.Count - 8} more</color>\n");
            }
            else
                sb.Append($"\n<color=#f0a090>{p.refusal}</color>\n");
            harvestButton.interactable = p.Valid && !waiting;
        }
        else
            harvestButton.interactable = false;
        if (harvestLabel != null) harvestLabel.text = waiting ? "…" : $"Harvest {m.Name.ToLowerInvariant()}";
        detailText.text = sb.ToString();
        RefreshUnits(a, k, av != HarvestAvailability.Available || !m.countable);
    }

    /// <summary>The work the selected material takes, for the details: planned again when the choice or the carcass changes, and every 2 s (for the tools at hand).</summary>
    private HarvestPlan DetailsPlan()
    {
        List<int> units = ChosenUnits();
        string key = selected + ":" + (units != null ? string.Join(",", units) : "") + ":" + creature.Version;
        if (detailsPlan == null || key != detailsKey || Time.unscaledTime - detailsTime > 2f)
        {
            detailsPlan = creature.PlanFor(player, selected, units);
            detailsKey = key;
            detailsTime = Time.unscaledTime;
        }
        return detailsPlan;
    }

    /// <summary>The parts the pieces of a countable material come from (a toggle each; none = all).</summary>
    private void RefreshUnits(HarvestAnatomyData a, HarvestCarcass k, bool none)
    {
        var counts = new SortedDictionary<int, int>();
        if (!none && k != null)
            foreach (HarvestUnit u in k.condition.units)
                if (u.material == selected && !u.taken && u.integrity >= Settings.destroyedBelow)
                    counts[u.region] = counts.TryGetValue(u.region, out int n) ? n + 1 : 1;
        // Built again only when the choice or the pieces left change.
        var sig = new StringBuilder(none || k == null ? "none" : selected + ":" + string.Join(",", chosenRegions) + ":");
        foreach (KeyValuePair<int, int> kv in counts) sig.Append(kv.Key).Append('=').Append(kv.Value).Append(';');
        string s = sig.ToString();
        if (s == unitSignature)
            return;
        unitSignature = s;
        for (int i = unitRow.childCount - 1; i >= 0; i--)
            Destroy(unitRow.GetChild(i).gameObject);
        if (none || k == null || counts.Count <= 1)
            return;
        RectTransform row = UITheme.Row("Regions", unitRow, 32f, 4f);
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        Button allButton = UITheme.Button("All", row, "All", () => { chosenRegions.Clear(); unitSignature = null; Refresh(true); }, UITheme.ButtonStyle.Tab, 0f, 32f);
        UITheme.SetSelected(allButton, chosenRegions.Count == 0);
        foreach (KeyValuePair<int, int> kv in counts)
        {
            int r = kv.Key;
            string label = $"{ShortRegion(a.regions[r])} {kv.Value}";
            Button b = UITheme.Button("Region " + r, row, label, () =>
            {
                if (!chosenRegions.Remove(r)) chosenRegions.Add(r);
                unitSignature = null;
                Refresh(true);
            }, UITheme.ButtonStyle.Tab, 0f, 32f);
            UITheme.SetSelected(b, chosenRegions.Contains(r));
        }
    }

    private static string ShortRegion(HarvestRegion r)
    {
        string n = r.Name;
        n = n.Replace("Left ", "L. ").Replace("Right ", "R. ");
        return n.Length > 12 ? n.Substring(0, 12) : n;
    }

    // ------------------------------------------------------------------ the stage
    private void RefreshStage()
    {
        if (model == null || plan == null)
            return;
        HarvestStageSpec s = model.Spec;
        string tool = $"{s.toolName} <color=#9aa0a6>({HarvestTools.FitnessWord(s.toolFitness, Settings.wrongToolBelow)})</color>";
        stageTitle.text = $"Stage {stageIndex + 1} of {plan.stages.Count} · {s.title}";
        stageText.text = $"{s.instruction}\n<color=#9aa0a6>Tool: {tool} · {Gauge(s.kind)}</color>";
        string arm = hands.Feeling;
        bool fresh = model.Time - model.FeedbackAt < 2.5;
        if (fresh && model.Feedback.Length > 0)
            feedback.text = model.FeedbackBad ? $"<color=#ff8070>{model.Feedback}</color>" : $"<color=#8fd68f>{model.Feedback}</color>";
        else if (arm.Length > 0)
            feedback.text = $"<color=#ffb070>{arm}</color>";
        else
            feedback.text = "";
        acceptButton.gameObject.SetActive(model is HarvestScrapeModel);
        acceptButton.interactable = model is HarvestScrapeModel sc && sc.Coverage > 0.25f;
    }

    private static string Gauge(HarvestStageKind k)
    {
        switch (k)
        {
            case HarvestStageKind.Trace: return "Gauge: knife speed - keep it in the coloured band.";
            case HarvestStageKind.Peel: return "Gauge: pull - keep it in the green band.";
            case HarvestStageKind.Steady: return "Gauge: how far the tip is off the mark - keep it under the green.";
            case HarvestStageKind.Pull: return "Gauges: pull (keep it in the green), strain (red: it is about to snap).";
            case HarvestStageKind.Saw: return "Gauge: how far through. The amber dot keeps the beat.";
            case HarvestStageKind.Pry: return "Gauges: how loose it is, and strain (red: it is about to crack).";
            default: return "Gauges: scraper speed (keep it in the green), how clean it is.";
        }
    }

    // ------------------------------------------------------------------ skills
    private string SkillsPage(HarvestSkillData d)
    {
        HarvestSettings s = Settings;
        var sb = new StringBuilder();
        sb.Append("<size=22><b>Harvesting skills</b></size>\n<color=#9aa0a6>Skills widen the margins, steady the hands, waste less and find more - they never do the work for you.</color>\n\n");
        foreach (HarvestSkill k in Enum.GetValues(typeof(HarvestSkill)))
        {
            float xp = d.Xp(k);
            int level = HarvestSkills.Level(s, xp);
            sb.Append($"<b>{HarvestText.SkillName(k)}</b> {level}  <color=#8ccfff>{HarvestText.Bar(HarvestSkills.Progress(s, xp), 12)}</color>\n");
            foreach (HarvestTechnique t in HarvestSkills.Techniques)
            {
                if (t.skill != k) continue;
                bool known = level >= t.level;
                sb.Append(known ? $"   <color=#8fd68f>✓ {t.name}</color> <color=#9aa0a6>- {t.description}</color>\n"
                                : $"   <color=#7a7f88>{t.name} (level {t.level}) - {t.description}</color>\n");
            }
        }
        sb.Append($"\n<b>Species</b> <color=#9aa0a6>({d.harvests} harvests, {d.pieces} pieces)</color>\n");
        if (d.species.Count == 0) sb.Append("<color=#9aa0a6>None yet.</color>\n");
        foreach (HarvestSpeciesNote n in d.species)
        {
            HarvestAnatomyData a = HarvestDatabase.Instance.Anatomy(n.id);
            double fam = HarvestSkills.Familiarity(s, d, n.id);
            sb.Append($"{(a != null ? a.Name : n.id)}: {n.harvests} harvests, best {HarvestText.Quality(n.bestQuality)} · known {fam * 100:0}%\n");
        }
        if (d.log.Count > 0)
        {
            sb.Append("\n<b>Last harvests</b>\n");
            for (int i = d.log.Count - 1; i >= 0 && i >= d.log.Count - 12; i--)
                sb.Append($"<color=#9aa0a6>{d.log[i]}</color>\n");
        }
        return sb.ToString();
    }
}
