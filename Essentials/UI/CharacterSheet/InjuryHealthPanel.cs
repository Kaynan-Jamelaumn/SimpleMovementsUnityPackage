using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The Health page of the inventory's character panel (only while the project's injury system is on), in the spirit of
/// Project Zomboid's health panel:
/// <list type="bullet">
/// <item>a summary (bleeding, injuries, illnesses, missing limbs, what the body cannot do) and two quick actions:
/// <b>Treat most urgent</b> (the worst thing something in the bag can treat, with the item that suits it best) and
/// <b>Stop all bleeding</b> (every bleeding wound in turn);</item>
/// <item>a body figure: every part coloured by its condition, with the icon of its worst injury (a pulsing drop while it
/// bleeds), a plus once everything on it is treated, a flash when it is hit, missing limbs dark. Beside it the whole
/// body's illnesses, and the details of the part under the pointer or the selection;</item>
/// <item>the injuries, the most urgent first: icon, name, severity, part, bleeding, treatment, time to heal and to get
/// worse (a bar each).</item>
/// </list>
/// With the mouse: hover for details, click a part to list only its injuries, click an injury (or right-click a part) to
/// pick a medical item - or drag one from the bag and drop it on a part or an injury. With a keyboard or a gamepad:
/// move the selection over the parts and the injuries (their details show beside them), Submit opens the treatments,
/// Cancel closes them. Everything updates as the character is hurt, treated, loses or gets back a limb. Built by
/// <see cref="InventoryCharacterPanel"/>.
/// </summary>
public class InjuryHealthPanel : MonoBehaviour
{
    private sealed class PartView
    {
        public InjuryPartState state;
        public RectTransform root;
        public Graphic fill;
        public Outline outline;
        public TextMeshProUGUI label;
        public Image icon;
        public Image badge;
        public Color baseColor;
        public bool focused;
        public bool bleeding;
        public int placed = -1; // -1 not yet, 0 small (icon only), 1 roomy (icon above the label)
    }

    private sealed class RowView
    {
        public InjuryPartState part;
        public Injury injury;
        public int uid;
        public Image background;
        public TextMeshProUGUI status;
        public RectTransform healFill;
        public Image healImage;
        public GameObject worsenTrack;
        public RectTransform worsenFill;
        public string shownStatus;
        public GameObject root;
    }

    private struct Listed
    {
        public InjuryPartState part;
        public Injury injury;
        public float urgency;
    }

    [SerializeField] private InventoryManager inventory;
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private Button treatUrgentButton;
    [SerializeField] private Button stopBleedingButton;
    [SerializeField] private RectTransform treatmentBar;
    [SerializeField] private RectTransform treatmentFill;
    [SerializeField] private TextMeshProUGUI treatmentText;
    [SerializeField] private RectTransform diagram;
    [SerializeField] private RectTransform wholeBodyCard;
    [SerializeField] private RectTransform wholeBodyIcons;
    [SerializeField] private TextMeshProUGUI wholeBodyText;
    [SerializeField] private Outline wholeBodyOutline;
    [SerializeField] private TextMeshProUGUI focusText;
    [SerializeField] private TextMeshProUGUI listTitle;
    [SerializeField] private RectTransform list;
    [SerializeField] private ScrollRect listScroll;
    [SerializeField] private RectTransform hoverPanel;
    [SerializeField] private TextMeshProUGUI hoverText;
    [SerializeField] private Transform overlayParent;

    private InjuryController injuries;
    private Canvas canvas;
    private RectTransform figure;
    private readonly List<PartView> partViews = new List<PartView>();
    private readonly List<RowView> rows = new List<RowView>();
    private readonly List<GameObject> built = new List<GameObject>();
    private readonly List<Image> illnessIcons = new List<Image>();
    private readonly List<TreatmentOption> options = new List<TreatmentOption>();
    private readonly List<Listed> listed = new List<Listed>();
    private readonly StringBuilder sb = new StringBuilder();
    private readonly StringBuilder line = new StringBuilder();
    private string selectedPart = "";
    private InjuryPartState focusPart;
    private bool wholeBodyFocused;
    private int shownVersion = -1;
    private int shownSignature;
    private bool signatureKnown;
    private InjurySettings builtFor;
    private int builtParts = -1;
    private float nextDynamic;
    private float nextButtons;
    private string shownSummary, shownFocus;
    private GameObject menu;
    private GameObject menuBlocker;
    private GameObject menuReturn;
    private RectTransform hoverAnchor;

    private static readonly Color Healthy = new Color(0.36f, 0.72f, 0.45f);
    private static readonly Color Critical = new Color(0.86f, 0.27f, 0.24f);
    private static readonly Color Missing = new Color(0.16f, 0.16f, 0.19f);
    private static readonly Color IconInk = new Color(0.05f, 0.06f, 0.08f, 0.92f);
    private static readonly Color TreatedColor = new Color(0.96f, 0.97f, 1f, 0.95f);
    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.045f);
    private static readonly Color RowFocus = new Color(1f, 1f, 1f, 0.11f);
    private static readonly Color WorsenColor = new Color(1f, 0.6f, 0.2f);
    private static readonly Color Idle = new Color(0.6f, 0.6f, 0.65f);
    private static readonly Color OutlineDark = new Color(0f, 0f, 0f, 0.35f);

    /// <summary>Width / height of the figure (a little broader than real proportions, so the limbs can be clicked).</summary>
    private const float FigureAspect = 0.62f;

    /// <summary>Is there a Health page to show for this inventory's player (system on, panel on, a controller)?</summary>
    public static bool Available(InventoryManager inv)
    {
        InjurySettings s = InjurySettings.Instance;
        if (!s.enabled || !s.showHealthPanel || inv == null)
            return false;
        return Resolve(inv) != null;
    }

    private static InjuryController Resolve(InventoryManager inv)
    {
        if (inv == null)
            return null;
        if (inv.Player != null)
        {
            InjuryController c = InjuryController.For(inv.Player.transform);
            if (c != null)
                return c;
        }
        return inv.PlayerStatus != null ? InjuryController.For(inv.PlayerStatus) : null;
    }

    // ------------------------------------------------------------------ building
    /// <summary>Builds the page inside <paramref name="host"/>; hover text and menus go under <paramref name="overlay"/>.</summary>
    public static InjuryHealthPanel Create(RectTransform host, InventoryManager inventory, Transform overlay)
    {
        var p = host.gameObject.AddComponent<InjuryHealthPanel>();
        p.inventory = inventory;
        p.overlayParent = overlay != null ? overlay : host;
        VerticalLayoutGroup v = InventoryUIFactory.Vertical(host.gameObject, 0, 6f, false);
        v.childForceExpandHeight = false;

        p.summaryText = InventoryUIFactory.Text("Summary", host, "", 15f, UITheme.Text, TextAlignmentOptions.TopLeft);
        p.summaryText.richText = true;

        // Quick actions.
        RectTransform actions = UITheme.Row("Actions", host, 34f, 6f);
        p.treatUrgentButton = UITheme.Button("Treat Most Urgent", actions, "Treat most urgent", p.TreatMostUrgent, UITheme.ButtonStyle.Primary, 0f, 34f);
        p.stopBleedingButton = UITheme.Button("Stop All Bleeding", actions, "Stop all bleeding", p.StopAllBleeding, UITheme.ButtonStyle.Danger, 0f, 34f);
        SmallLabel(p.treatUrgentButton);
        SmallLabel(p.stopBleedingButton);

        // Treatment in progress: a bar with a cancel button.
        Image bar = UITheme.Panel("Treatment", host, new Color(0.2f, 0.32f, 0.5f, 0.35f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(bar, -1f, 32f);
        p.treatmentBar = bar.rectTransform;
        Image fill = UITheme.Panel("Fill", bar.transform, new Color(0.36f, 0.62f, 1f, 0.45f), UITheme.RadiusSmall);
        fill.raycastTarget = false;
        p.treatmentFill = fill.rectTransform;
        p.treatmentFill.anchorMin = Vector2.zero;
        p.treatmentFill.anchorMax = new Vector2(0f, 1f);
        p.treatmentFill.offsetMin = p.treatmentFill.offsetMax = Vector2.zero;
        p.treatmentText = InventoryUIFactory.Text("Text", bar.transform, "", 14f, UITheme.Text, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Stretch(p.treatmentText.rectTransform, 10f, 0f);
        p.treatmentText.rectTransform.offsetMax = new Vector2(-84f, 0f);
        Button cancel = UITheme.Button("Cancel", bar.transform, "Cancel", () => p.injuries?.CancelTreatment("Cancelled."), UITheme.ButtonStyle.Ghost, 76f, 28f);
        RectTransform crt = (RectTransform)cancel.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
        crt.pivot = new Vector2(1f, 0.5f);
        crt.anchoredPosition = new Vector2(-4f, 0f);
        cancel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        bar.gameObject.SetActive(false);

        // The body: the figure on the left, the whole body's illnesses and the focused part's details on the right.
        Image body = UITheme.Panel("Body", host, new Color(1f, 1f, 1f, 0.03f), UITheme.Radius);
        InventoryUIFactory.Size(body, -1f, 290f);
        p.diagram = UITheme.Area("Diagram", body.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(8f, 8f), new Vector2(-2f, -8f));
        RectTransform side = UITheme.Area("Side", body.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(4f, 8f), new Vector2(-8f, -8f));
        InventoryUIFactory.Vertical(side.gameObject, 0, 6f, false);
        p.BuildWholeBodyCard(side);
        Image focus = UITheme.Panel("Details", side, new Color(0f, 0f, 0f, 0.18f), UITheme.RadiusSmall);
        focus.raycastTarget = false;
        InventoryUIFactory.Size(focus, -1f, 60f).flexibleHeight = 1f;
        p.focusText = InventoryUIFactory.Text("Text", focus.transform, "", 12.5f, UITheme.Muted, TextAlignmentOptions.TopLeft);
        InventoryUIFactory.Stretch(p.focusText.rectTransform, 8f, 6f);
        p.focusText.richText = true;
        p.focusText.overflowMode = TextOverflowModes.Ellipsis;

        p.listTitle = UITheme.Header(host, "Injuries");
        p.list = UITheme.ScrollList("Injuries", host, 0f, out p.listScroll, new Color(0f, 0f, 0f, 0f), 2, 5f);

        GameMenuFactory.HoverPanel(p.overlayParent, out p.hoverPanel, out p.hoverText);
        p.hoverText.richText = true;
        return p;
    }

    private static void SmallLabel(Button b)
    {
        TMP_Text t = b != null ? b.GetComponentInChildren<TMP_Text>(true) : null;
        if (t != null)
            t.fontSize = 14f;
    }

    /// <summary>The card of the whole body: the illnesses (a cold, sepsis...), selectable like a part.</summary>
    private void BuildWholeBodyCard(RectTransform parent)
    {
        Image card = UITheme.Panel("Whole Body", parent, new Color(1f, 1f, 1f, 0.05f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(card, -1f, 82f);
        wholeBodyCard = card.rectTransform;
        wholeBodyOutline = card.gameObject.AddComponent<Outline>();
        wholeBodyOutline.effectDistance = new Vector2(1.5f, -1.5f);
        wholeBodyOutline.effectColor = OutlineDark;
        TextMeshProUGUI title = InventoryUIFactory.Text("Title", card.transform, "WHOLE BODY", 11f, UITheme.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        title.characterSpacing = 3f;
        UITheme.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -20f), new Vector2(-8f, -5f));
        wholeBodyIcons = UITheme.Area("Icons", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -44f), new Vector2(-8f, -22f));
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(wholeBodyIcons.gameObject, 4f);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        for (int i = 0; i < 6; i++)
        {
            Image icon = HudStyle.Image("Illness", wholeBodyIcons, Color.white, InjuryIcons.Shape(InjuryIconShape.Germ));
            icon.preserveAspect = true;
            InventoryUIFactory.Size(icon, 20f, 20f, 0f);
            icon.gameObject.SetActive(false);
            illnessIcons.Add(icon);
        }
        wholeBodyText = InventoryUIFactory.Text("Text", card.transform, "", 12.5f, UITheme.Text, TextAlignmentOptions.TopLeft);
        wholeBodyText.richText = true;
        wholeBodyText.overflowMode = TextOverflowModes.Ellipsis;
        UITheme.Place(wholeBodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 4f), new Vector2(-8f, -46f));

        var sel = card.gameObject.AddComponent<Selectable>();
        sel.transition = Selectable.Transition.None;
        var trigger = card.gameObject.AddComponent<InjuryUITrigger>();
        trigger.panel = this;
        trigger.hover = WholeBodyHover;
        trigger.click = e =>
        {
            InjuryPartState whole = injuries != null ? injuries.WholeBody : null;
            if (whole != null)
                ClickPart(whole, e);
        };
        trigger.focus = on =>
        {
            wholeBodyFocused = on;
            if (wholeBodyOutline != null)
                wholeBodyOutline.effectColor = OutlineOf(injuries != null ? injuries.WholeBody : null, on);
        };
    }

    private void Awake() => canvas = GetComponentInParent<Canvas>();

    private void OnDisable()
    {
        HideHover();
        CloseMenu();
        focusPart = null;
        wholeBodyFocused = false;
    }

    private void OnDestroy()
    {
        CloseMenu();
        if (hoverPanel != null)
            Destroy(hoverPanel.gameObject);
    }

    /// <summary>
    /// The body figure: a front view (the character's right on the left, as when facing them) with each part drawn as
    /// its own shape - head, neck, torso segments, every arm and leg segment - on a faint silhouette. Parts the figure
    /// does not know (a tail, wings) are drawn as rectangles at their Diagram Rect. The Whole Body part is not on the
    /// figure: it is the card beside it.
    /// </summary>
    private void BuildDiagram()
    {
        foreach (PartView pv in partViews)
            if (pv.root != null) Destroy(pv.root.gameObject);
        partViews.Clear();
        if (figure != null)
            Destroy(figure.gameObject);
        builtFor = injuries != null ? injuries.Settings : null;
        builtParts = injuries != null ? injuries.Parts.Count : -1;
        focusPart = null;
        if (injuries == null || diagram == null)
            return;
        if (wholeBodyCard != null)
        {
            InjuryUITrigger wb = wholeBodyCard.GetComponent<InjuryUITrigger>();
            if (wb != null)
                wb.part = injuries.WholeBody;
        }

        // The figure keeps its proportions whatever the panel's size.
        figure = NewRect("Figure", diagram);
        figure.anchorMin = Vector2.zero;
        figure.anchorMax = Vector2.one;
        figure.offsetMin = figure.offsetMax = Vector2.zero;
        var fit = figure.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = FigureAspect;
        foreach (BodyPartLayout.Region region in new[] { BodyPartLayout.Region.Head, BodyPartLayout.Region.Neck, BodyPartLayout.Region.Torso })
            Silhouette(BodyPartLayout.Shape(region));
        foreach (BodyPartLayout.Region region in new[] { BodyPartLayout.Region.Arm, BodyPartLayout.Region.Leg })
        {
            Silhouette(BodyPartLayout.Shape(region));
            Silhouette(BodyPartLayout.Mirror(BodyPartLayout.Shape(region)));
        }

        // Bigger shapes first, so smaller ones drawn over them stay clickable (the groin over the thighs).
        var order = new List<(InjuryPartState part, Vector2[] shape)>();
        foreach (InjuryPartState part in injuries.Parts)
            if (!part.IsWholeBody)
                order.Add((part, ShapeOf(part.Def)));
        order.Sort((a, b) => Area(b.shape).CompareTo(Area(a.shape)));
        foreach ((InjuryPartState part, Vector2[] shape) in order)
        {
            RectTransform rt = NewRect(part.Id, shape != null ? figure : diagram);
            var img = rt.gameObject.AddComponent<BodyShapeGraphic>();
            img.color = Healthy;
            if (shape != null)
                img.PlaceOnFigure(shape);
            else
            {
                Rect r = part.Def.diagramRect;
                rt.anchorMin = new Vector2(Mathf.Clamp01(r.xMin), Mathf.Clamp01(r.yMin));
                rt.anchorMax = new Vector2(Mathf.Clamp01(r.xMax), Mathf.Clamp01(r.yMax));
                rt.offsetMin = new Vector2(1f, 1f);
                rt.offsetMax = new Vector2(-1f, -1f);
                img.SetRectangle();
            }
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.effectColor = OutlineDark;
            TextMeshProUGUI label = InventoryUIFactory.Text("Label", rt, "", 11f, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Stretch(label.rectTransform);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = 12f;
            Image icon = HudStyle.Image("Icon", rt, IconInk, InjuryIcons.Shape(InjuryIconShape.Dot));
            icon.preserveAspect = true;
            icon.gameObject.SetActive(false);
            Image badge = HudStyle.Image("Treated", rt, TreatedColor, InjuryIcons.Plus());
            badge.preserveAspect = true;
            HudStyle.Place(badge.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-3f, 3f), new Vector2(10f, 10f));
            badge.gameObject.SetActive(false);

            var pv = new PartView { state = part, root = rt, fill = img, outline = outline, label = label, icon = icon, badge = badge, baseColor = Healthy };
            var sel = rt.gameObject.AddComponent<Selectable>();
            sel.transition = Selectable.Transition.None;
            sel.targetGraphic = img;
            InjuryPartState captured = part;
            var trigger = rt.gameObject.AddComponent<InjuryUITrigger>();
            trigger.panel = this;
            trigger.part = part;
            trigger.hover = () => PartHover(captured);
            trigger.click = e => ClickPart(captured, e);
            trigger.focus = on =>
            {
                pv.focused = on;
                if (on) focusPart = captured;
                else if (focusPart == captured) focusPart = null;
                if (pv.outline != null)
                    pv.outline.effectColor = OutlineOf(captured, on);
                shownFocus = null;
            };
            partViews.Add(pv);
        }
    }

    /// <summary>The outline of a part on the figure (by its id, else its name, else its aliases), or null.</summary>
    public static Vector2[] ShapeOf(InjuryBodyPart def)
    {
        if (def == null || def.kind == BodyPartKind.WholeBody)
            return null;
        BodyPartLayout.Region region = BodyPartLayout.Classify(def.id);
        if (region == BodyPartLayout.Region.Other) region = BodyPartLayout.Classify(def.displayName);
        if (region == BodyPartLayout.Region.Other && def.aliases != null)
            foreach (string a in def.aliases)
                if ((region = BodyPartLayout.Classify(a)) != BodyPartLayout.Region.Other)
                    break;
        Vector2[] shape = BodyPartLayout.Shape(region);
        if (shape == null)
            return null;
        if (!BodyPartLayout.IsLimb(region))
            return shape;
        // The figure's own outlines are the character's right side; a limb in the centre has no place on it.
        if (def.side == BodyPartSide.Center)
            return null;
        return def.side == BodyPartSide.Right ? shape : BodyPartLayout.Mirror(shape);
    }

    private static float Area(Vector2[] shape)
    {
        if (shape == null)
            return 0f;
        Rect b = BodyPartLayout.Bounds(shape);
        return b.width * b.height;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>A faint shape of the whole body behind the parts (a missing limb still shows where it was).</summary>
    private void Silhouette(Vector2[] shape)
    {
        if (shape == null)
            return;
        var g = NewRect("Silhouette", figure).gameObject.AddComponent<BodyShapeGraphic>();
        g.color = new Color(1f, 1f, 1f, 0.05f);
        g.raycastTarget = false;
        g.PlaceOnFigure(shape);
    }

    private Color OutlineOf(InjuryPartState part, bool focused)
    {
        if (focused)
            return UITheme.Accent;
        bool selected = part != null && !string.IsNullOrEmpty(selectedPart) && string.Equals(selectedPart, part.Id, System.StringComparison.OrdinalIgnoreCase);
        return selected ? Color.white : OutlineDark;
    }

    // ------------------------------------------------------------------ per frame
    private void Update()
    {
        if (injuries == null || !injuries.isActiveAndEnabled)
        {
            InjuryController c = Resolve(inventory);
            if (c != injuries)
            {
                injuries = c;
                shownVersion = -1;
                BuildDiagram();
            }
            if (injuries == null)
            {
                if (summaryText != null) summaryText.text = "No injuries are tracked for this character.";
                return;
            }
        }
        if (builtFor != injuries.Settings || builtParts != injuries.Parts.Count)
        {
            BuildDiagram();
            shownVersion = -1;
        }
        if (injuries.Version != shownVersion)
        {
            shownVersion = injuries.Version;
            Refresh();
        }
        else if (Time.unscaledTime >= nextDynamic)
        {
            RefreshDynamic();
        }
        Animate();
        FollowMouse();
        if (menu != null && CancelPressed())
            CloseMenu();
    }

    private static bool CancelPressed()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    /// <summary>Redraws everything that changed (rows are rebuilt only when injuries came or went).</summary>
    public void Refresh()
    {
        if (injuries == null)
            return;
        int signature = Signature();
        if (!signatureKnown || signature != shownSignature)
        {
            signatureKnown = true;
            shownSignature = signature;
            RebuildRows();
        }
        nextButtons = 0f;
        RefreshDynamic();
    }

    private void RefreshDynamic()
    {
        nextDynamic = Time.unscaledTime + 0.25f;
        if (injuries == null)
            return;
        RefreshDiagram();
        RefreshWholeBody();
        RefreshSummary();
        RefreshFocus();
        RefreshTreatmentBar();
        RefreshButtons();
        foreach (RowView row in rows)
            UpdateRow(row);
    }

    /// <summary>What the rows depend on: the injuries the player sees (and their stages, treatments), missing parts, the selected part.</summary>
    private int Signature()
    {
        unchecked
        {
            int h = 17;
            for (int c = 0; c < selectedPart.Length; c++)
                h = h * 31 + char.ToLowerInvariant(selectedPart[c]);
            foreach (InjuryPartState p in injuries.Parts)
            {
                h = h * 31 + (p.Lost ? 1 : 0) + (p.LostWith != null ? 2 : 0);
                foreach (Injury i in p.Injuries)
                {
                    if (i.Hidden)
                        continue;
                    h = h * 31 + i.Uid;
                    h = h * 31 + i.StageIndex;
                    h = h * 31 + (i.Bandaged ? 1 : 0) + (i.Treated ? 2 : 0) + (i.Reopened ? 4 : 0) + ((int)i.Side << 3);
                }
            }
            return h;
        }
    }

    private void RefreshDiagram()
    {
        InjurySettings s = injuries.Settings;
        foreach (PartView pv in partViews)
        {
            InjuryPartState p = pv.state;
            if (pv.fill == null)
                continue;
            // The percentage only where it fits (the head and the torso): there the icon sits above it.
            Rect pr = pv.root.rect;
            int roomy = pr.width >= 30f && pr.height >= 30f ? 1 : 0;
            if (pv.placed != roomy)
            {
                pv.placed = roomy;
                float size = roomy == 1 ? 15f : Mathf.Clamp(Mathf.Min(pr.width, pr.height) - 3f, 8f, 13f);
                HudStyle.Place(pv.icon.rectTransform, new Vector2(0.5f, roomy == 1 ? 0.7f : 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                RectTransform lr = pv.label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = new Vector2(1f, roomy == 1 ? 0.52f : 1f);
                lr.offsetMin = lr.offsetMax = Vector2.zero;
            }
            Injury worst = null;
            bool allTreated = true;
            int visible = 0;
            IReadOnlyList<Injury> onPart = p.Injuries;
            for (int k = 0; k < onPart.Count; k++)
            {
                Injury i = onPart[k];
                if (i.Hidden)
                    continue;
                visible++;
                if (worst == null || i.Urgency > worst.Urgency)
                    worst = i;
                if (!i.Treated && !i.Bandaged)
                    allTreated = false;
            }
            pv.bleeding = !p.Lost && p.IsBleeding;
            string text;
            if (p.Lost)
            {
                pv.baseColor = Missing;
                text = "LOST";
                pv.label.color = new Color(0.85f, 0.4f, 0.4f);
                SetIcon(pv.icon, InjuryIcons.Cross(), new Color(0.9f, 0.45f, 0.45f, 0.9f));
            }
            else
            {
                pv.baseColor = InjuryHudAlert.ConditionColor(p);
                text = roomy == 1 ? (p.Condition01 * 100f).ToString("0") + "%" : "";
                pv.label.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
                if (pv.bleeding)
                    SetIcon(pv.icon, InjuryIcons.Shape(InjuryIconShape.Drop), IconInk);
                else if (worst != null)
                    SetIcon(pv.icon, InjuryIcons.For(worst.Type), IconInk);
                else
                    SetIcon(pv.icon, null, IconInk);
            }
            bool showLabel = roomy == 1 || (p.Lost && pr.width >= 26f && pr.height >= 13f);
            if (pv.label.enabled != showLabel) pv.label.enabled = showLabel;
            if (showLabel && pv.label.text != text) pv.label.text = text;
            // A missing part too small for the word keeps the cross.
            if (p.Lost && showLabel && roomy == 0)
                SetIcon(pv.icon, null, IconInk);
            bool badge = !p.Lost && visible > 0 && allTreated && pr.width >= 18f;
            if (pv.badge.gameObject.activeSelf != badge) pv.badge.gameObject.SetActive(badge);
            pv.outline.effectColor = OutlineOf(p, pv.focused);
        }
    }

    private static void SetIcon(Image icon, Sprite sprite, Color color)
    {
        bool on = sprite != null;
        if (icon.gameObject.activeSelf != on)
            icon.gameObject.SetActive(on);
        if (!on)
            return;
        if (icon.sprite != sprite) icon.sprite = sprite;
        if (icon.color != color) icon.color = color;
    }

    /// <summary>Every frame: a part flashes white when it is hit, a bleeding part's drop pulses.</summary>
    private void Animate()
    {
        float now = Time.time;
        float pulse = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.5f));
        for (int i = 0; i < partViews.Count; i++)
        {
            PartView pv = partViews[i];
            if (pv.fill == null)
                continue;
            Color c = pv.baseColor;
            float since = now - pv.state.LastHitTime;
            if (since >= 0f && since < 0.45f && !pv.state.Lost)
                c = Color.Lerp(Color.white, c, since / 0.45f);
            if (pv.fill.color != c)
                pv.fill.color = c;
            if (pv.bleeding && pv.icon.gameObject.activeSelf)
                pv.icon.color = new Color(0.55f, 0.02f, 0.04f, pulse);
        }
    }

    /// <summary>The whole body's card: the illnesses the player knows of.</summary>
    private void RefreshWholeBody()
    {
        InjuryPartState whole = injuries.WholeBody;
        if (wholeBodyCard == null || whole == null)
            return;
        int shown = 0;
        line.Clear();
        IReadOnlyList<Injury> illnesses = whole.Injuries;
        for (int k = 0; k < illnesses.Count; k++)
        {
            Injury i = illnesses[k];
            if (i.Hidden)
                continue;
            if (shown < illnessIcons.Count)
            {
                Image icon = illnessIcons[shown];
                if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
                icon.sprite = InjuryIcons.For(i.Type);
                icon.color = i.Type != null ? i.Type.color : Color.white;
            }
            if (line.Length > 0)
                line.Append(", ");
            line.Append(i.Name);
            shown++;
        }
        for (int k = shown; k < illnessIcons.Count; k++)
            if (illnessIcons[k].gameObject.activeSelf) illnessIcons[k].gameObject.SetActive(false);
        string text = shown == 0 ? StatText.Paint("No illness", 1) : line.ToString();
        if (wholeBodyText.text != text)
            wholeBodyText.text = text;
        wholeBodyOutline.effectColor = OutlineOf(whole, wholeBodyFocused);
    }

    private void RefreshSummary()
    {
        if (summaryText == null)
            return;
        sb.Clear();
        float bleed = injuries.BleedingPerSecond;
        int count = injuries.VisibleInjuryCount;
        if (bleed > 1e-4f)
            Chip(StatText.Paint($"Bleeding {bleed:0.##} HP/s", -1));
        if (count > 0)
            Chip($"{count} injur{(count == 1 ? "y" : "ies")}");
        IReadOnlyList<InjuryPartState> all = injuries.Parts;
        for (int k = 0; k < all.Count; k++)
        {
            InjuryPartState p = all[k];
            if (!p.Lost || p.LostWith != null)
                continue;
            SeveredLimbRecord r = p.LostLimb;
            string time = r == null || r.Consumed || r.Gone ? "" : r.Expired ? " (too late to reattach)" : $" (reattach within {SeveredLimbRecord.FormatTime(r.Remaining)})";
            Chip(StatText.Paint($"Missing {p.Name}{time}", -1));
        }
        if (sb.Length == 0)
            Chip(StatText.Paint("Healthy", 1));
        BodyAction blocked = injuries.Blocked & ~(BodyAction.HandOnThisSide);
        if (blocked != BodyAction.None)
            sb.Append('\n').Append(StatText.Paint(BodyActions.DescribeAll(blocked), -1));
        InjuryVision sight = injuries.Vision;
        if (sight.Blind)
            sb.Append('\n').Append(StatText.Paint("You can barely see.", -1));
        else if (sight.left > 0.5f || sight.right > 0.5f)
            sb.Append('\n').Append(StatText.Paint(sight.left > sight.right ? "Your left eye sees little." : "Your right eye sees little.", -1));
        if (injuries.RulesHere)
        {
            float heal = injuries.BodyHealFactor;
            if (heal > 1.01f)
                sb.Append('\n').Append(StatText.Paint($"Resting: healing ×{heal:0.#}", 1));
            else if (heal < 0.99f)
                sb.Append('\n').Append(StatText.Paint($"Hungry or thirsty: healing ×{heal:0.#}", -1));
            if (injuries.BodyWorsenFactor > 1.01f && count > 0)
                sb.Append('\n').Append(StatText.Paint("Cold, hungry or thirsty: injuries get worse sooner", -1));
        }
        string resist = injuries.Resistances != null ? injuries.Resistances.ShortSummary() : "";
        if (!string.IsNullOrEmpty(resist))
            sb.Append('\n').Append(StatText.Paint("Resistances: " + resist, 0));
        string city = CityInjuryTreatment.StatusLine;
        if (!string.IsNullOrEmpty(city))
            sb.Append('\n').Append(StatText.Paint("City treatment: " + city, 0));
        string text = sb.ToString();
        if (text == shownSummary)
            return;
        shownSummary = text;
        summaryText.text = text;
    }

    private void Chip(string text)
    {
        if (sb.Length > 0)
            sb.Append("   ·   ");
        sb.Append(text);
    }

    /// <summary>The details beside the figure: the part under the pointer or the selection, else the part the list shows, else how to treat.</summary>
    private void RefreshFocus()
    {
        if (focusText == null)
            return;
        InjuryPartState p = focusPart;
        if (p == null && wholeBodyFocused)
            p = injuries.WholeBody;
        if (p == null && !string.IsNullOrEmpty(selectedPart))
            p = injuries.GetPart(selectedPart);
        string text;
        if (p == null)
        {
            text = "<b><color=#dfe3ea>Treating injuries</color></b>\n" +
                   "Click an injury below - or right-click a body part - and pick an item.\n" +
                   "Or drag a medical item from your bag onto a part.\n\n" +
                   "A part is <color=#5cb873>green</color> when healthy, <color=#edbd4d>yellow</color> when hurt, <color=#db4540>red</color> when bad.\n" +
                   "Its icon is its worst injury; a drop pulses while it bleeds; + means treated.";
        }
        else
        {
            line.Clear();
            line.Append("<b><color=#dfe3ea>").Append(p.Name).Append("</color></b>");
            if (p.Lost)
                line.Append("  ").Append(StatText.Paint("missing", -1));
            else if (!p.IsWholeBody)
            {
                line.Append('\n').Append("Condition ").Append((p.Condition01 * 100f).ToString("0")).Append('%');
                if (p.Impairment >= 0.05f)
                    line.Append("  ·  impaired ").Append((p.Impairment * 100f).ToString("0")).Append('%');
            }
            int n = 0;
            foreach (Injury i in p.Injuries)
            {
                if (i.Hidden)
                    continue;
                n++;
                line.Append("\n• ").Append(i.Name);
                if (i.IsBleeding) line.Append(StatText.Paint(" bleeding", -1));
                else if (i.Treated || i.Bandaged) line.Append(StatText.Paint(" treated", 1));
            }
            if (n == 0 && !p.Lost)
                line.Append('\n').Append(StatText.Paint(p.IsWholeBody ? "No illness." : "No injuries here.", 1));
            string effects = PartEffects(p);
            if (!string.IsNullOrEmpty(effects))
                line.Append('\n').Append(StatText.Paint(effects, -1));
            text = line.ToString();
        }
        if (text == shownFocus)
            return;
        shownFocus = text;
        focusText.text = text;
    }

    private void RefreshTreatmentBar()
    {
        if (treatmentBar == null)
            return;
        ActiveTreatment a = injuries.CurrentTreatment;
        bool show = a != null;
        if (treatmentBar.gameObject.activeSelf != show)
            treatmentBar.gameObject.SetActive(show);
        if (!show)
            return;
        treatmentFill.anchorMax = new Vector2(a.Progress, 1f);
        string target = a.Injury != null ? (a.Part != null && !a.Part.IsWholeBody ? $"{a.Injury.Name}, {a.Part.Name}" : a.Injury.Name) : a.Part != null ? a.Part.Name : "";
        string label = a.Treatment != null ? a.Treatment.name : "Treatment";
        string text = string.IsNullOrEmpty(target) ? $"{label}  {a.Progress * 100f:0}%" : $"{label}: {target}  {a.Progress * 100f:0}%";
        if (treatmentText.text != text)
            treatmentText.text = text;
    }

    /// <summary>The quick actions are lit only while they would do something (looked up once a second: it searches the bag).</summary>
    private void RefreshButtons()
    {
        if (Time.unscaledTime < nextButtons)
            return;
        nextButtons = Time.unscaledTime + 1f;
        bool busy = injuries.IsTreating;
        if (treatUrgentButton != null)
        {
            bool can = !busy && injuries.MostUrgentTreatable(out _, out _, out _);
            if (treatUrgentButton.interactable != can) treatUrgentButton.interactable = can;
        }
        if (stopBleedingButton != null)
        {
            bool can = !busy && injuries.IsBleeding && injuries.MostUrgentTreatable(out _, out _, out _, true);
            if (stopBleedingButton.interactable != can) stopBleedingButton.interactable = can;
        }
    }

    // ------------------------------------------------------------------ quick actions
    private void TreatMostUrgent()
    {
        if (injuries == null)
            return;
        CloseMenu();
        if (injuries.MostUrgentTreatable(out InjuryPartState part, out Injury injury, out TreatmentOption option))
            injuries.BeginTreatment(option.treatment, part, injury, TreatmentSource.Item, option.item);
        else if (inventory != null)
            inventory.ShowMessage("You carry nothing that treats your injuries.", InventoryFeedback.Kind.Info);
        nextButtons = 0f;
    }

    private void StopAllBleeding()
    {
        if (injuries == null)
            return;
        CloseMenu();
        injuries.TreatAllBleeding();
        nextButtons = 0f;
    }

    // ------------------------------------------------------------------ rows
    private void RebuildRows()
    {
        HideHover();
        foreach (GameObject go in built)
            if (go != null) Destroy(go);
        built.Clear();
        rows.Clear();
        if (list == null)
            return;
        InjuryPartState only = string.IsNullOrEmpty(selectedPart) ? null : injuries.GetPart(selectedPart);
        if (only == null)
            selectedPart = "";

        // What is listed: the selected part's, else everything, the most urgent first.
        listed.Clear();
        foreach (InjuryPartState p in injuries.Parts)
        {
            if (only != null && p != only)
                continue;
            if (p.Lost && p.LostWith == null)
            {
                SeveredLimbRecord r = p.LostLimb;
                bool inTime = r != null && !r.Consumed && !r.Gone && !r.Expired;
                listed.Add(new Listed { part = p, injury = null, urgency = inTime ? 150f : 8f });
            }
            foreach (Injury i in p.Injuries)
                if (!i.Hidden)
                    listed.Add(new Listed { part = p, injury = i, urgency = i.Urgency });
        }
        listed.Sort((a, b) => b.urgency.CompareTo(a.urgency));

        if (listTitle != null)
            listTitle.text = only != null ? only.Name.ToUpperInvariant() : listed.Count > 0 ? $"INJURIES ({listed.Count}), MOST URGENT FIRST" : "INJURIES";
        if (only != null)
        {
            if (only.Lost && only.LostWith != null)
                Note($"Lost with the {only.LostWith.Name.ToLowerInvariant()}.");
            else if (listed.Count == 0)
                Note(only.IsWholeBody ? "No illness." : "No injuries here.");
        }
        else if (listed.Count == 0)
        {
            Note("No injuries. Hits can cause scratches, cuts, fractures and worse; the cold, bad food and dirty wounds can make you ill. Treat it all here with medical items.");
        }
        foreach (Listed l in listed)
            Row(l.part, l.injury, only == null);
        listed.Clear();
        if (only != null)
        {
            Button all = UITheme.Button("All", list, "Show all parts", () => Select(""), UITheme.ButtonStyle.Ghost, 0f, 30f);
            built.Add(all.gameObject);
        }
    }

    private void Note(string text)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text("Note", list, text, 14f, UITheme.Muted, TextAlignmentOptions.Left);
        built.Add(t.gameObject);
    }

    /// <summary>One injury (or the missing limb when <paramref name="injury"/> is null): icon, name, severity, part, status, bars.</summary>
    private void Row(InjuryPartState part, Injury injury, bool showPart)
    {
        InjurySettings s = injuries.Settings;
        Color sevColor = injury != null ? s.Severity(injury.Severity).color : Critical;
        Image bg = UITheme.Panel("Injury", list, RowColor, UITheme.RadiusSmall);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 9f);
        h.padding = new RectOffset(14, 8, 6, 7);
        h.childAlignment = TextAnchor.UpperLeft;
        h.childForceExpandHeight = false;
        built.Add(bg.gameObject);

        Image stripe = InventoryUIFactory.Panel("Severity", bg.transform, sevColor);
        stripe.raycastTarget = false;
        RectTransform srt = stripe.rectTransform;
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(0f, 1f);
        srt.pivot = new Vector2(0f, 0.5f);
        srt.sizeDelta = new Vector2(5f, -8f);
        srt.anchoredPosition = new Vector2(4f, 0f);
        stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // The icon on a plate of the injury's colour.
        Color typeColor = injury != null && injury.Type != null ? injury.Type.color : Critical;
        Image plate = UITheme.Panel("Icon", bg.transform, new Color(typeColor.r, typeColor.g, typeColor.b, 0.22f), UITheme.RadiusSmall);
        plate.raycastTarget = false;
        InventoryUIFactory.Size(plate, 30f, 30f, 0f);
        Image glyph = HudStyle.Image("Glyph", plate.transform, Color.Lerp(typeColor, Color.white, 0.35f), injury != null ? InjuryIcons.For(injury.Type) : InjuryIcons.Cross());
        HudStyle.Stretch(glyph.rectTransform, 5f);
        glyph.preserveAspect = true;

        RectTransform column = InventoryUIFactory.Rect("Text", bg.transform);
        InventoryUIFactory.Vertical(column.gameObject, 0, 2f, false);
        InventoryUIFactory.Size(column, -1f, -1f, 1f);

        string where = showPart && !part.IsWholeBody ? $"  <color=#9aa0a6>{part.Name}</color>" : "";
        string title = injury != null
            ? $"<b>{injury.Name}</b>  <color=#{ColorUtility.ToHtmlStringRGB(sevColor)}>{s.SeverityName(injury.Severity)}</color>{where}"
            : $"<b>Missing {part.Name}</b>";
        TextMeshProUGUI t = InventoryUIFactory.Text("Title", column, title, 14.5f, UITheme.Text, TextAlignmentOptions.Left);
        t.richText = true;
        TextMeshProUGUI status = InventoryUIFactory.Text("Status", column, "", 12.5f, UITheme.Muted, TextAlignmentOptions.Left);
        status.richText = true;

        Image track = InventoryUIFactory.Panel("Healing", column, new Color(1f, 1f, 1f, 0.07f));
        track.raycastTarget = false;
        InventoryUIFactory.Size(track, -1f, 4f);
        Image heal = InventoryUIFactory.Panel("Fill", track.transform, Healthy);
        heal.raycastTarget = false;
        RectTransform hrt = heal.rectTransform;
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = new Vector2(0f, 1f);
        hrt.offsetMin = hrt.offsetMax = Vector2.zero;

        // The time left before it gets worse, running down.
        Image worsenTrack = InventoryUIFactory.Panel("Worsening", column, new Color(1f, 1f, 1f, 0.05f));
        worsenTrack.raycastTarget = false;
        InventoryUIFactory.Size(worsenTrack, -1f, 3f);
        Image worsen = InventoryUIFactory.Panel("Fill", worsenTrack.transform, WorsenColor);
        worsen.raycastTarget = false;
        RectTransform wrt = worsen.rectTransform;
        wrt.anchorMin = Vector2.zero;
        wrt.anchorMax = new Vector2(1f, 1f);
        wrt.offsetMin = wrt.offsetMax = Vector2.zero;
        worsenTrack.gameObject.SetActive(false);

        var row = new RowView
        {
            part = part, injury = injury, uid = injury != null ? injury.Uid : 0, background = bg, status = status, healFill = hrt, healImage = heal,
            worsenTrack = worsenTrack.gameObject, worsenFill = wrt, root = bg.gameObject,
        };
        rows.Add(row);
        var sel = bg.gameObject.AddComponent<Selectable>();
        sel.transition = Selectable.Transition.None;
        var trigger = bg.gameObject.AddComponent<InjuryUITrigger>();
        trigger.panel = this;
        trigger.part = part;
        trigger.injury = injury;
        trigger.hover = () => row.injury != null ? InjuryHover(row.part, row.injury) : MissingHover(row.part);
        trigger.click = e => OpenMenu(row.part, row.injury, row.root, e == null);
        trigger.focus = on =>
        {
            if (row.background != null)
                row.background.color = on ? RowFocus : RowColor;
        };
        trigger.navigatedTo = () => ScrollIntoView(bg.rectTransform);
        UpdateRow(row);
    }

    /// <summary>Scrolls the list so <paramref name="item"/> shows (the selection moved onto it with a keyboard or a gamepad).</summary>
    private void ScrollIntoView(RectTransform item)
    {
        if (listScroll == null || listScroll.content == null || listScroll.viewport == null || item == null)
            return;
        float contentHeight = listScroll.content.rect.height, viewHeight = listScroll.viewport.rect.height;
        float range = contentHeight - viewHeight;
        if (range <= 1f)
            return;
        // The item's top and bottom, measured down from the top of the content.
        item.GetWorldCorners(anchorCorners);
        float contentTop = listScroll.content.rect.yMax;
        float top = contentTop - listScroll.content.InverseTransformPoint(anchorCorners[1]).y;
        float bottom = contentTop - listScroll.content.InverseTransformPoint(anchorCorners[0]).y;
        float scrolled = (1f - listScroll.verticalNormalizedPosition) * range;
        if (top < scrolled)
            scrolled = top;
        else if (bottom > scrolled + viewHeight)
            scrolled = bottom - viewHeight;
        else
            return;
        listScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(scrolled / range);
    }

    private void UpdateRow(RowView row)
    {
        if (row.status == null)
            return;
        InjurySettings s = injuries.Settings;
        // The injury by its id (a network copy may hold a newer object for it).
        if (row.uid != 0)
        {
            Injury current = row.part.Find(row.uid);
            if (current != null)
                row.injury = current;
        }
        Injury i = row.injury;
        line.Clear();
        if (i == null)
        {
            SeveredLimbRecord r = row.part.LostLimb;
            bool carried = injuries.FindLimb(row.part, false, out _, out _) != null;
            if (r == null || r.Consumed || r.Gone)
                line.Append("The limb is gone: only an ability that regrows limbs brings it back.");
            else if (r.Expired)
                line.Append(StatText.Paint("Too late to reattach normally.", -1));
            else
                line.Append("Reattach within ").Append(StatText.Paint(SeveredLimbRecord.FormatTime(r.Remaining), 0)).Append("  ·  ")
                    .Append(carried ? StatText.Paint("limb in your bag", 1) : StatText.Paint("limb not carried", -1));
            SetStatus(row, line.ToString());
            float left = r != null && r.TimeLimit > 0f ? r.Remaining / r.TimeLimit : 0f;
            row.healFill.anchorMax = new Vector2(Mathf.Clamp01(left), 1f);
            row.healImage.color = Critical;
            return;
        }
        float bleed = i.BleedingPerSecond;
        if (bleed > 1e-4f) Bit(StatText.Paint($"bleeding {bleed:0.##}/s", -1));
        else if (i.Stage != null && i.Stage.bleedingPerSecond > 0f) Bit(i.Clotted ? "stopped bleeding" : "bleeding controlled");
        if (i.Reopened) Bit(StatText.Paint("torn open again", -1));
        if (i.Treated || i.Bandaged) Bit(StatText.Paint(string.IsNullOrEmpty(i.TreatmentLabel) ? "bandaged" : i.TreatmentLabel.ToLowerInvariant(), 1));
        if (i.CanWorsen) Bit(StatText.Paint($"worse in {SeveredLimbRecord.FormatTime(i.WorsensIn)}", -1));
        float healsIn = i.HealsIn(s.healingSpeedMultiplier);
        if (!float.IsInfinity(healsIn)) Bit($"heals in {SeveredLimbRecord.FormatTime(healsIn)}");
        else if (i.Stage != null && i.Stage.needsTreatmentToHeal && !i.HealingAllowed) Bit(StatText.Paint("needs treatment to heal", -1));
        if (i.Impairment >= 0.05f) Bit($"impairs {i.Impairment * 100f:0}%");
        SetStatus(row, line.ToString());
        row.healFill.anchorMax = new Vector2(Mathf.Clamp01(i.HealProgress), 1f);
        row.healImage.color = i.HealRate > 0f ? Healthy : Idle;
        bool worsening = i.CanWorsen && i.Stage != null && i.Stage.worsenAfterSeconds > 0f;
        if (row.worsenTrack.activeSelf != worsening)
            row.worsenTrack.SetActive(worsening);
        if (worsening)
            row.worsenFill.anchorMax = new Vector2(Mathf.Clamp01(1f - i.UntreatedTime / i.Stage.worsenAfterSeconds), 1f);
    }

    private void Bit(string text)
    {
        if (line.Length > 0)
            line.Append("  ·  ");
        line.Append(text);
    }

    private static void SetStatus(RowView row, string text)
    {
        if (text == row.shownStatus)
            return;
        row.shownStatus = text;
        row.status.text = text;
    }

    // ------------------------------------------------------------------ hover texts
    private string PartHover(InjuryPartState p)
    {
        var t = new StringBuilder();
        t.Append("<b>").Append(p.Name).Append("</b>");
        if (p.Lost)
        {
            t.Append("  ").Append(StatText.Paint("missing", -1));
            if (p.LostWith != null)
                t.Append("\nLost with the ").Append(p.LostWith.Name.ToLowerInvariant()).Append('.');
            else if (p.LostLimb != null && !p.LostLimb.Consumed && !p.LostLimb.Gone)
                t.Append('\n').Append(p.LostLimb.Expired ? StatText.Paint("Too late to reattach normally.", -1) : $"Reattach within {SeveredLimbRecord.FormatTime(p.LostLimb.Remaining)}.");
        }
        else
        {
            t.Append($"\nCondition {p.Condition:0} / {p.MaxCondition:0}");
            if (p.Impairment >= 0.05f)
                t.Append($"  ·  impaired {p.Impairment * 100f:0}%");
        }
        foreach (Injury i in p.Injuries)
        {
            if (i.Hidden)
                continue;
            t.Append("\n• ").Append(i.Name).Append(" (").Append(injuries.Settings.SeverityName(i.Severity)).Append(')')
             .Append(i.IsBleeding ? StatText.Paint(" bleeding", -1) : i.Treated || i.Bandaged ? StatText.Paint(" treated", 1) : "");
        }
        string effects = PartEffects(p);
        if (!string.IsNullOrEmpty(effects))
            t.Append('\n').Append(StatText.Paint(effects, -1));
        t.Append('\n').Append(StatText.Paint("Click: list its injuries · right-click: treat · or drop a medical item on it.", 0));
        return t.ToString();
    }

    private string WholeBodyHover()
    {
        var t = new StringBuilder();
        t.Append("<b>Whole body</b>");
        InjuryPartState whole = injuries != null ? injuries.WholeBody : null;
        if (whole == null)
            return t.ToString();
        int n = 0;
        foreach (Injury i in whole.Injuries)
        {
            if (i.Hidden)
                continue;
            n++;
            t.Append("\n• ").Append(i.Name).Append(" (").Append(injuries.Settings.SeverityName(i.Severity)).Append(')');
            if (i.Treated) t.Append(StatText.Paint(" " + (string.IsNullOrEmpty(i.TreatmentLabel) ? "treated" : i.TreatmentLabel.ToLowerInvariant()), 1));
        }
        if (n == 0)
            t.Append('\n').Append(StatText.Paint("No illness.", 1));
        t.Append('\n').Append(StatText.Paint("Illnesses come from dirty wounds, the cold, bad food and water, and other people. Some show nothing at first.", 0));
        t.Append('\n').Append(StatText.Paint("Click: list them · right-click: treat.", 0));
        return t.ToString();
    }

    private string InjuryHover(InjuryPartState p, Injury i)
    {
        InjurySettings s = injuries.Settings;
        InjuryStage st = i.Stage;
        var t = new StringBuilder();
        Color sev = s.Severity(i.Severity).color;
        t.Append("<b>").Append(i.Name).Append("</b>");
        if (!p.IsWholeBody)
            t.Append(" on the ").Append(p.Name.ToLowerInvariant());
        t.Append($"  <color=#{ColorUtility.ToHtmlStringRGB(sev)}>{s.SeverityName(i.Severity)}</color>");
        if (i.Type != null && !string.IsNullOrWhiteSpace(i.Type.description))
            t.Append('\n').Append(StatText.Paint(i.Type.description, 0));
        if (i.Form != DamageForm.None)
            t.Append('\n').Append(StatText.Paint($"Caused by a {DamageForms.Adjective(i.Form)} hit.", 0));
        if (st != null)
        {
            if (st.bleedingPerSecond > 0f)
            {
                float b = i.BleedingPerSecond;
                t.Append('\n').Append(b > 1e-4f ? StatText.Paint($"Bleeding {b:0.##} health/s", -1) : "Not bleeding");
                if (b > 1e-4f && st.clotsAfterSeconds > 0f)
                    t.Append($" (stops by itself in {SeveredLimbRecord.FormatTime((st.clotsAfterSeconds - i.BleedTime) / Mathf.Max(0.1f, i.HealingFactor))})");
                else if (b > 1e-4f)
                    t.Append(" (does not stop by itself)");
            }
            if (i.Impairment > 0f && !p.IsWholeBody)
                t.Append($"\nImpairs the {p.Name.ToLowerInvariant()} {i.Impairment * 100f:0}%");
            if (st.consequence != null && st.consequence.HasAnyEffect)
            {
                string d = st.consequence.Describe();
                if (d != "no effect") t.Append('\n').Append(StatText.Paint(d, -1));
            }
            if (st.vision != null && st.vision.HasAny)
                t.Append('\n').Append(StatText.Paint("Sight: " + st.vision.Describe(), -1));
            if (st.blocks != BodyAction.None)
                t.Append('\n').Append(StatText.Paint(BodyActions.DescribeAll(st.blocks), -1));
            if (st.contagionPerMinute > 0f)
                t.Append('\n').Append(StatText.Paint("Catching: people near you may get it.", -1));
        }
        if (i.Reopened) t.Append('\n').Append(StatText.Paint("A hit tore it open again: treat it again.", -1));
        if (i.Bandaged) t.Append('\n').Append(StatText.Paint($"Covered ({i.BandageEffectiveness * 100f:0}% of the bleeding stopped)", 1));
        if (i.Treated) t.Append('\n').Append(StatText.Paint("Treated: " + i.TreatmentLabel, 1));
        float healsIn = i.HealsIn(s.healingSpeedMultiplier);
        if (!float.IsInfinity(healsIn))
            t.Append($"\nHeals in {SeveredLimbRecord.FormatTime(healsIn)} ({i.HealProgress * 100f:0}% done)");
        else if (st != null && st.needsTreatmentToHeal && !i.HealingAllowed)
            t.Append('\n').Append(StatText.Paint("Does not heal until treated.", -1));
        if (i.CanWorsen)
            t.Append('\n').Append(StatText.Paint(st != null && st.worsenChance < 0.999f
                ? $"May get worse in {SeveredLimbRecord.FormatTime(i.WorsensIn)}."
                : $"Gets worse in {SeveredLimbRecord.FormatTime(i.WorsensIn)} if left untreated.", -1));
        AppendTreatmentHints(t, p, i);
        t.Append('\n').Append(StatText.Paint("Click to treat it.", 0));
        return t.ToString();
    }

    private string MissingHover(InjuryPartState p)
    {
        var t = new StringBuilder();
        t.Append("<b>Missing ").Append(p.Name).Append("</b>");
        SeveredLimbRecord r = p.LostLimb;
        if (r != null && !r.Consumed && !r.Gone)
        {
            t.Append($"\nLimb condition {r.Condition * 100f:0}%");
            t.Append('\n').Append(r.Expired
                ? StatText.Paint("Its time limit ran out: only special abilities (or a city surgeon, later) can reattach it.", -1)
                : $"Reattach it within {SeveredLimbRecord.FormatTime(r.Remaining)} with a surgical kit (or a healing ability).");
            if (r.IncludedParts.Count > 0)
                t.Append("\nCame off with: ").Append(string.Join(", ", r.IncludedParts));
            if (r.DroppedItems.Count > 0)
                t.Append("\nDropped with it: ").Append(string.Join(", ", r.DroppedItems));
            bool carried = injuries.FindLimb(p, false, out _, out _) != null;
            t.Append('\n').Append(carried ? StatText.Paint("The limb is in your bag.", 1) : StatText.Paint("Pick the limb up first (it lies where you lost it).", -1));
        }
        else
        {
            t.Append('\n').Append(StatText.Paint("The limb is gone. Only an ability that regrows limbs can bring it back.", -1));
        }
        string effects = PartEffects(p);
        if (!string.IsNullOrEmpty(effects))
            t.Append('\n').Append(StatText.Paint(effects, -1));
        AppendTreatmentHints(t, p, null);
        t.Append('\n').Append(StatText.Paint("Click to reattach.", 0));
        return t.ToString();
    }

    /// <summary>"Treat with: Bandage (3), Suture Kit (1)" from the bag.</summary>
    private void AppendTreatmentHints(StringBuilder t, InjuryPartState p, Injury i)
    {
        injuries.FindItemTreatments(p, i, options);
        if (options.Count == 0)
        {
            t.Append('\n').Append(StatText.Paint("You carry nothing that treats it.", 0));
            return;
        }
        t.Append("\nTreat with: ");
        MedicalItemSO last = null;
        bool first = true;
        foreach (TreatmentOption o in options)
        {
            if (o.item == last)
                continue; // an item with two treatments for it is named once
            last = o.item;
            if (!first) t.Append(", ");
            first = false;
            t.Append(o.item.Name).Append(" (").Append(o.count).Append(')');
        }
        options.Clear();
    }

    /// <summary>What a part does to the character now: its consequence and the actions it stops.</summary>
    private string PartEffects(InjuryPartState p)
    {
        var bits = new StringBuilder();
        float imp = p.Lost ? 1f : p.Impairment;
        StatusConsequence c = p.Lost ? (p.Def.lost != null && p.Def.lost.HasAnyEffect ? p.Def.lost : p.Def.impaired) : imp >= 0.05f ? p.Def.impaired : null;
        if (c != null)
        {
            string d = c.Describe(p.Lost ? 1f : imp);
            if (d != "no effect") bits.Append(d);
        }
        BodyAction blocked = BodyAction.None;
        BodyPartSide main = injuries.Settings.mainHandSide;
        if (p.Def.actionRules != null)
            foreach (BodyActionRule rule in p.Def.actionRules)
                if (rule != null && imp + 1e-4f >= rule.fromImpairment)
                    blocked |= BodyActions.ResolveSide(rule.actions, p.Def.side, main);
        foreach (Injury i in p.Injuries)
            if (i.Stage != null && !i.Hidden)
                blocked |= BodyActions.ResolveSide(i.Stage.blocks, p.Def.side, main);
        if (blocked != BodyAction.None)
        {
            if (bits.Length > 0) bits.Append("; ");
            bits.Append(BodyActions.DescribeAll(blocked));
        }
        return bits.ToString();
    }

    // ------------------------------------------------------------------ clicks
    private void ClickPart(InjuryPartState p, PointerEventData e)
    {
        if (e != null && e.button == PointerEventData.InputButton.Right)
        {
            Injury urgent = null;
            foreach (Injury i in p.Injuries)
                if (!i.Hidden && (urgent == null || i.Urgency > urgent.Urgency))
                    urgent = i;
            if (urgent != null || (p.Lost && p.LostWith == null))
                OpenMenu(p, urgent, null, false);
            return;
        }
        Select(string.Equals(selectedPart, p.Id, System.StringComparison.OrdinalIgnoreCase) ? "" : p.Id);
    }

    private void Select(string partId)
    {
        selectedPart = partId ?? "";
        signatureKnown = false;
        shownFocus = null;
        Refresh();
    }

    // ------------------------------------------------------------------ dropping a medical item
    /// <summary>
    /// A medical item dragged from the bag was released over <paramref name="under"/>: when that is a part or an injury of
    /// a Health page, the item treats it (the injury itself; on a part, the most urgent thing there the item treats).
    /// Returns true when the drop was on a Health page (the item goes back to its slot: the treatment uses it up when it
    /// finishes), false when it was somewhere else.
    /// </summary>
    public static bool TryDrop(GameObject under, MedicalItemSO item)
    {
        if (under == null || item == null)
            return false;
        InjuryUITrigger trigger = under.GetComponentInParent<InjuryUITrigger>();
        if (trigger == null || trigger.panel == null)
            return false;
        trigger.panel.DropItem(trigger, item);
        return true;
    }

    private void DropItem(InjuryUITrigger trigger, MedicalItemSO item)
    {
        if (injuries == null || trigger.part == null)
            return;
        InjuryPartState part = trigger.part;
        Injury target = null;
        InjuryTreatment treatment = null;
        float best = 0f;
        string why = null;
        if (trigger.injury != null)
            Consider(part.Find(trigger.injury.Uid));
        else
        {
            if (part.Lost && part.LostWith == null)
                Consider(null);
            foreach (Injury i in part.Injuries)
                if (!i.Hidden)
                    Consider(i);
        }
        if (treatment == null)
        {
            // A medicine taken to be safe (a vaccine): dropped on the body it is taken, as from the hotbar - it looks
            // the same whether something it treats is incubating or not.
            if (item.Preventive && why == null)
            {
                CloseMenu();
                injuries.QuickTreat(item);
                nextButtons = 0f;
                return;
            }
            if (inventory != null)
                inventory.ShowMessage(why ?? $"The {item.Name} does not help with that.", InventoryFeedback.Kind.Info);
            return;
        }
        CloseMenu();
        injuries.BeginTreatment(treatment, part, target, TreatmentSource.Item, item);
        nextButtons = 0f;

        void Consider(Injury injury)
        {
            if (injury == null && !(part.Lost && part.LostWith == null))
                return;
            for (int k = 0; k < item.Treatments.Count; k++)
            {
                InjuryTreatment t = item.Treatments[k];
                if (t == null)
                    continue;
                if (!injuries.CanTreat(t, part, injury, TreatmentSource.Item, item, out string reason))
                {
                    if (why == null && !string.IsNullOrEmpty(reason) && reason != "It does not help with this.")
                        why = reason;
                    continue;
                }
                // Dropped on an injury: that one. Dropped on a part: what the item answers best there.
                float score = t.ScoreFor(injury) + (injury != null ? injury.Urgency * 0.01f : 0.5f);
                if (score <= best)
                    continue;
                best = score;
                target = injury;
                treatment = t;
            }
        }
    }

    // ------------------------------------------------------------------ treatment menu
    /// <summary>The medical items the player carries that treat this injury (or reattach this limb), to pick one.</summary>
    private void OpenMenu(InjuryPartState part, Injury injury, GameObject from, bool fromKeyboard)
    {
        CloseMenu();
        HideHover();
        if (injuries == null || part == null)
            return;
        injuries.FindItemTreatments(part, injury, options);
        injuries.BestItemTreatment(part, injury, out TreatmentOption recommended);
        menuReturn = from;

        Image blocker = InventoryUIFactory.Panel("Treatment Menu Blocker", overlayParent, new Color(0f, 0f, 0f, 0.001f));
        InventoryUIFactory.Stretch(blocker.rectTransform);
        Button close = blocker.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.navigation = new Navigation { mode = Navigation.Mode.None };
        close.onClick.AddListener(CloseMenu);
        menuBlocker = blocker.gameObject;

        Image panel = UITheme.Panel("Treatment Menu", overlayParent, new Color(0.04f, 0.05f, 0.07f, 0.98f), UITheme.Radius);
        menu = panel.gameObject;
        RectTransform rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(380f, 10f);
        InventoryUIFactory.Vertical(panel.gameObject, 12, 6f, true);
        var shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -4f);

        string title = injury != null
            ? (part.IsWholeBody ? $"Treat {injury.Name}" : $"Treat {injury.Name} ({part.Name})")
            : $"Reattach the {part.Name.ToLowerInvariant()}";
        TextMeshProUGUI t = InventoryUIFactory.Text("Title", panel.transform, title, 16f, UITheme.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        t.richText = true;
        if (options.Count == 0)
        {
            InventoryUIFactory.Text("None", panel.transform, injury != null
                ? "You carry nothing that treats this."
                : "You need a surgical kit (or a healing ability) and the limb in your bag.", 14f, UITheme.Muted, TextAlignmentOptions.Left);
        }
        GameObject firstUsable = null;
        foreach (TreatmentOption o in options)
        {
            TreatmentOption opt = o;
            float seconds = opt.treatment.applySeconds * injuries.Settings.treatmentTimeMultiplier;
            bool best = opt.usable && recommended.treatment == opt.treatment && recommended.item == opt.item;
            string label = $"{opt.item.Name} ×{opt.count}  -  {opt.treatment.name}" + (seconds > 0f ? $"  ({seconds:0.#} s)" : "") + (best ? "  (best)" : "");
            Button b = UITheme.Button(opt.item.Name, panel.transform, label, () =>
            {
                CloseMenu();
                injuries.BeginTreatment(opt.treatment, part, injury, TreatmentSource.Item, opt.item);
                nextButtons = 0f;
            }, best ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Normal, 0f, 38f);
            TMP_Text bl = b.GetComponentInChildren<TMP_Text>(true);
            if (bl != null) bl.fontSize = 15f;
            b.interactable = opt.usable;
            if (opt.usable && (firstUsable == null || best))
                firstUsable = b.gameObject;
            // What the treatment does, under its button.
            string does = opt.usable ? Does(opt.treatment) : opt.reason;
            if (!string.IsNullOrEmpty(does))
                InventoryUIFactory.Text("Why", panel.transform, does, 12.5f, opt.usable ? UITheme.Muted : UITheme.Bad, TextAlignmentOptions.Left);
        }
        options.Clear();
        string city = CityInjuryTreatment.StatusLine;
        if (!string.IsNullOrEmpty(city))
            InventoryUIFactory.Text("City", panel.transform, "City healer: " + city, 13f, UITheme.Muted, TextAlignmentOptions.Left);
        Button closeButton = UITheme.Button("Close", panel.transform, "Close", CloseMenu, UITheme.ButtonStyle.Ghost, 0f, 32f);

        menuBlocker.transform.SetAsLastSibling();
        menu.transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        Vector2 at;
        if (fromKeyboard && from != null)
        {
            // Beside the row it was opened from.
            var corners = new Vector3[4];
            ((RectTransform)from.transform).GetWorldCorners(corners);
            at = corners[2];
        }
        else
        {
            at = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : (Vector2)rt.position;
            at += new Vector2(8f, -8f);
        }
        rt.position = at;
        InventoryUIFactory.ClampToCanvas(rt, canvas != null ? canvas : GetComponentInParent<Canvas>());
        // A keyboard or a gamepad: the selection goes into the menu.
        if (fromKeyboard && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstUsable != null ? firstUsable : closeButton.gameObject);
    }

    /// <summary>What a treatment's effects do, in a few words.</summary>
    private static string Does(InjuryTreatment t)
    {
        if (t == null || t.effects == null)
            return "";
        var bits = new StringBuilder();
        foreach (TreatmentEffect e in t.effects)
        {
            if (e == null)
                continue;
            if (bits.Length > 0) bits.Append("; ");
            bits.Append(e.Describe());
        }
        return bits.ToString();
    }

    private void CloseMenu()
    {
        bool was = menu != null;
        if (menu != null) Destroy(menu);
        if (menuBlocker != null) Destroy(menuBlocker);
        menu = null;
        menuBlocker = null;
        // The selection goes back to where the menu was opened from.
        if (was && menuReturn != null && EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(menuReturn);
        }
        menuReturn = null;
    }

    // ------------------------------------------------------------------ hover
    /// <summary>Shows the details text: at the pointer (<paramref name="anchor"/> null), or beside an element (a keyboard or gamepad selection).</summary>
    internal void ShowHover(string text, RectTransform anchor)
    {
        if (hoverPanel == null || hoverText == null || menu != null)
            return;
        hoverAnchor = anchor;
        hoverText.text = text;
        hoverPanel.gameObject.SetActive(true);
        hoverPanel.SetAsLastSibling();
        FollowMouse();
    }

    internal void HideHover()
    {
        hoverAnchor = null;
        if (hoverPanel != null && hoverPanel.gameObject.activeSelf)
            hoverPanel.gameObject.SetActive(false);
    }

    private readonly Vector3[] anchorCorners = new Vector3[4];

    private void FollowMouse()
    {
        if (hoverPanel == null || !hoverPanel.gameObject.activeSelf)
            return;
        if (hoverAnchor != null)
        {
            // Beside the selected element (its top right corner).
            hoverAnchor.GetWorldCorners(anchorCorners);
            hoverPanel.position = anchorCorners[2] + new Vector3(10f, 0f, 0f);
        }
        else
        {
            Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
            hoverPanel.position = mouse + new Vector2(18f, -18f);
        }
        InventoryUIFactory.ClampToCanvas(hoverPanel, canvas != null ? canvas : GetComponentInParent<Canvas>());
    }
}
