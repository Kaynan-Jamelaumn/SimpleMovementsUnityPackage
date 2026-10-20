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
/// <item>a body diagram: every body part coloured by its condition, red dots on bleeding parts, orange dots on other
/// injuries, missing limbs greyed out. Hover a part for its condition, injuries and effects; click it to list only
/// its injuries; right-click it to treat its most urgent injury;</item>
/// <item>a summary (bleeding, injuries, missing limbs and the time left to reattach them, what the body cannot do);</item>
/// <item>the injuries: type, severity, bleeding, treatment, healing progress, time to heal or to get worse. Hover one for
/// everything about it; click it to pick a medical item you carry (bandage, splint, suture kit...) and apply it - or,
/// for a missing limb, to reattach it with the limb in your bag;</item>
/// <item>the treatment being applied, with its progress and a cancel button.</item>
/// </list>
/// Everything updates as the character is hurt, treated, loses or gets back a limb. Built by
/// <see cref="InventoryCharacterPanel"/>.
/// </summary>
public class InjuryHealthPanel : MonoBehaviour
{
    private sealed class PartView
    {
        public InjuryPartState state;
        public Graphic fill;
        public Outline outline;
        public TextMeshProUGUI label;
        public Image bleedDot;
        public Image injuryDot;
    }

    private sealed class RowView
    {
        public InjuryPartState part;
        public Injury injury;
        public TextMeshProUGUI status;
        public RectTransform progress;
        public Image progressImage;
    }

    [SerializeField] private InventoryManager inventory;
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private RectTransform treatmentBar;
    [SerializeField] private RectTransform treatmentFill;
    [SerializeField] private TextMeshProUGUI treatmentText;
    [SerializeField] private RectTransform diagram;
    [SerializeField] private TextMeshProUGUI listTitle;
    [SerializeField] private RectTransform list;
    [SerializeField] private RectTransform hoverPanel;
    [SerializeField] private TextMeshProUGUI hoverText;
    [SerializeField] private Transform overlayParent;

    private InjuryController injuries;
    private Canvas canvas;
    private readonly List<PartView> partViews = new List<PartView>();
    private readonly List<RowView> rows = new List<RowView>();
    private readonly List<GameObject> built = new List<GameObject>();
    private readonly List<TreatmentOption> options = new List<TreatmentOption>();
    private readonly StringBuilder sb = new StringBuilder();
    private string selectedPart = "";
    private int shownVersion = -1;
    private string shownSignature;
    private InjurySettings builtFor;
    private float nextDynamic;
    private GameObject menu;
    private GameObject menuBlocker;

    private static readonly Color Healthy = new Color(0.36f, 0.72f, 0.45f);
    private static readonly Color Hurt = new Color(0.93f, 0.74f, 0.3f);
    private static readonly Color Critical = new Color(0.86f, 0.27f, 0.24f);
    private static readonly Color Missing = new Color(0.16f, 0.16f, 0.19f);
    private static readonly Color BleedColor = new Color(1f, 0.16f, 0.16f);
    private static readonly Color InjuryColor = new Color(1f, 0.6f, 0.2f);

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
        VerticalLayoutGroup v = InventoryUIFactory.Vertical(host.gameObject, 0, 8f, false);
        v.childForceExpandHeight = false;

        p.summaryText = InventoryUIFactory.Text("Summary", host, "", 15f, UITheme.Text, TextAlignmentOptions.TopLeft);
        p.summaryText.richText = true;

        // Treatment in progress: a bar with a cancel button.
        Image bar = UITheme.Panel("Treatment", host, new Color(0.2f, 0.32f, 0.5f, 0.35f), UITheme.RadiusSmall);
        InventoryUIFactory.Size(bar, -1f, 34f);
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
        Button cancel = UITheme.Button("Cancel", bar.transform, "Cancel", () => p.injuries?.CancelTreatment("Cancelled."), UITheme.ButtonStyle.Ghost, 76f, 30f);
        RectTransform crt = (RectTransform)cancel.transform;
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
        crt.pivot = new Vector2(1f, 0.5f);
        crt.anchoredPosition = new Vector2(-4f, 0f);
        cancel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        bar.gameObject.SetActive(false);

        // The body diagram.
        Image diagramBg = UITheme.Panel("Body", host, new Color(1f, 1f, 1f, 0.03f), UITheme.Radius);
        InventoryUIFactory.Size(diagramBg, -1f, 300f);
        RectTransform area = UITheme.Area("Diagram", diagramBg.transform, Vector2.zero, Vector2.one, new Vector2(10f, 22f), new Vector2(-10f, -8f));
        p.diagram = area;
        TextMeshProUGUI view = InventoryUIFactory.Text("View", diagramBg.transform, "front view  ·  click a part to filter, right-click to treat", 11f, UITheme.Muted, TextAlignmentOptions.Bottom);
        InventoryUIFactory.Stretch(view.rectTransform, 6f, 4f);

        p.listTitle = UITheme.Header(host, "Injuries");
        p.list = UITheme.ScrollList("Injuries", host, 0f, out _, new Color(0f, 0f, 0f, 0f), 2, 6f);

        GameMenuFactory.HoverPanel(p.overlayParent, out p.hoverPanel, out p.hoverText);
        p.hoverText.richText = true;
        return p;
    }

    private void Awake() => canvas = GetComponentInParent<Canvas>();

    private void OnDisable()
    {
        HideHover();
        CloseMenu();
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
    /// does not know (a tail, wings) are drawn as rectangles at their Diagram Rect.
    /// </summary>
    private void BuildDiagram()
    {
        foreach (PartView pv in partViews)
            if (pv.fill != null) Destroy(pv.fill.gameObject);
        partViews.Clear();
        if (figure != null)
            Destroy(figure.gameObject);
        builtFor = injuries != null ? injuries.Settings : null;
        if (injuries == null || diagram == null)
            return;

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
            outline.effectColor = new Color(0f, 0f, 0f, 0.35f);
            TextMeshProUGUI label = InventoryUIFactory.Text("Label", rt, "", 11f, Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Stretch(label.rectTransform);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = 12f;
            var pv = new PartView
            {
                state = part,
                fill = img,
                outline = outline,
                label = label,
                bleedDot = Dot(rt, BleedColor, new Vector2(1f, 1f)),
                injuryDot = Dot(rt, InjuryColor, new Vector2(0f, 1f)),
            };
            InjuryPartState captured = part;
            var trigger = img.gameObject.AddComponent<InjuryUITrigger>();
            trigger.panel = this;
            trigger.hover = () => PartHover(captured);
            trigger.click = e => ClickPart(captured, e);
            partViews.Add(pv);
        }
    }

    /// <summary>Width / height of the figure (a little broader than real proportions, so the limbs can be clicked).</summary>
    private const float FigureAspect = 0.62f;

    private RectTransform figure;

    /// <summary>The outline of a part on the figure (by its id, else its name, else its aliases), or null.</summary>
    private static Vector2[] ShapeOf(InjuryBodyPart def)
    {
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

    private static Image Dot(RectTransform parent, Color color, Vector2 corner)
    {
        Image d = UITheme.Panel("Dot", parent, color, 6);
        d.raycastTarget = false;
        RectTransform rt = d.rectTransform;
        // At the top middle of the part, side by side (the corners of a shape's bounds may be outside the shape).
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(9f, 9f);
        rt.anchoredPosition = new Vector2(corner.x > 0.5f ? 5f : -5f, -5f);
        d.gameObject.SetActive(false);
        return d;
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
        if (builtFor != injuries.Settings || partViews.Count != injuries.Parts.Count)
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
        Pulse();
        FollowMouse();
    }

    /// <summary>Redraws everything that changed (rows are rebuilt only when injuries came or went).</summary>
    public void Refresh()
    {
        if (injuries == null)
            return;
        string signature = Signature();
        if (signature != shownSignature)
        {
            shownSignature = signature;
            RebuildRows();
        }
        RefreshDynamic();
    }

    private void RefreshDynamic()
    {
        nextDynamic = Time.unscaledTime + 0.25f;
        if (injuries == null)
            return;
        RefreshDiagram();
        RefreshSummary();
        RefreshTreatmentBar();
        foreach (RowView row in rows)
            UpdateRow(row);
    }

    /// <summary>What the rows depend on: injuries (and their stages, treatments), missing parts, the selected part.</summary>
    private string Signature()
    {
        sb.Clear();
        sb.Append(selectedPart).Append('|');
        foreach (InjuryPartState p in injuries.Parts)
        {
            sb.Append(p.Lost ? 'L' : 'p');
            foreach (Injury i in p.Injuries)
                sb.Append(i.Uid).Append(':').Append(i.StageIndex).Append(i.Bandaged ? 'b' : '-').Append(i.Treated ? 't' : '-').Append(',');
            sb.Append(';');
        }
        return sb.ToString();
    }

    private void RefreshDiagram()
    {
        foreach (PartView pv in partViews)
        {
            InjuryPartState p = pv.state;
            if (pv.fill == null)
                continue;
            bool selected = string.Equals(selectedPart, p.Id, System.StringComparison.OrdinalIgnoreCase);
            if (p.Lost)
            {
                pv.fill.color = Missing;
                pv.label.text = "LOST";
                pv.label.color = new Color(0.85f, 0.4f, 0.4f);
            }
            else
            {
                float c = p.Condition01;
                Color col = c > 0.5f ? Color.Lerp(Hurt, Healthy, (c - 0.5f) * 2f) : Color.Lerp(Critical, Hurt, c * 2f);
                if (p.Impairment >= 0.6f)
                    col = Color.Lerp(col, Critical, 0.5f);
                pv.fill.color = col;
                pv.label.text = $"{c * 100f:0}%";
                pv.label.color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
            }
            pv.outline.effectColor = selected ? Color.white : new Color(0f, 0f, 0f, 0.35f);
            // The percentage only where it fits (the head and the torso); colour and hover show every part.
            Rect pr = ((RectTransform)pv.fill.transform).rect;
            bool fits = pr.width >= 26f && pr.height >= 13f;
            if (pv.label.enabled != fits) pv.label.enabled = fits;
            bool bleeding = p.IsBleeding;
            if (pv.bleedDot.gameObject.activeSelf != bleeding) pv.bleedDot.gameObject.SetActive(bleeding);
            bool injured = false; // an injury that is not bleeding (the red dot shows the bleeding ones)
            foreach (Injury i in p.Injuries)
                if (!i.IsBleeding) { injured = true; break; }
            if (pv.injuryDot.gameObject.activeSelf != injured) pv.injuryDot.gameObject.SetActive(injured);
        }
    }

    private void Pulse()
    {
        float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 7f);
        foreach (PartView pv in partViews)
            if (pv.bleedDot != null && pv.bleedDot.gameObject.activeSelf)
                pv.bleedDot.color = new Color(BleedColor.r, BleedColor.g, BleedColor.b, a);
    }

    private void RefreshSummary()
    {
        if (summaryText == null)
            return;
        sb.Clear();
        float bleed = injuries.BleedingPerSecond;
        int count = 0;
        foreach (InjuryPartState p in injuries.Parts)
            count += p.Injuries.Count;
        if (bleed > 1e-4f)
            Chip(StatText.Paint($"Bleeding {bleed:0.##} HP/s", -1));
        if (count > 0)
            Chip($"{count} injur{(count == 1 ? "y" : "ies")}");
        foreach (InjuryPartState p in injuries.Parts)
        {
            if (!p.Lost || p.LostWith != null)
                continue;
            SeveredLimbRecord r = p.LostLimb;
            string time = r == null || r.Consumed ? "" : r.Expired ? " (too late to reattach)" : $" (reattach within {SeveredLimbRecord.FormatTime(r.Remaining)})";
            Chip(StatText.Paint($"Missing {p.Name}{time}", -1));
        }
        if (sb.Length == 0)
            Chip(StatText.Paint("Healthy", 1));
        BodyAction blocked = injuries.Blocked & ~(BodyAction.HandOnThisSide);
        if (blocked != BodyAction.None)
            sb.Append('\n').Append(StatText.Paint(BodyActions.DescribeAll(blocked), -1));
        string resist = injuries.Resistances != null ? injuries.Resistances.ShortSummary() : "";
        if (!string.IsNullOrEmpty(resist))
            sb.Append('\n').Append(StatText.Paint("Resistances: " + resist, 0));
        string city = CityInjuryTreatment.StatusLine;
        if (!string.IsNullOrEmpty(city))
            sb.Append('\n').Append(StatText.Paint("City treatment: " + city, 0));
        summaryText.text = sb.ToString();
    }

    private void Chip(string text)
    {
        if (sb.Length > 0)
            sb.Append("   ·   ");
        sb.Append(text);
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
        string target = a.Injury != null ? $"{a.Injury.Name}, {a.Part.Name}" : a.Part != null ? a.Part.Name : "";
        treatmentText.text = $"{a.Treatment.name}: {target}  {a.Progress * 100f:0}%";
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
        if (listTitle != null)
            listTitle.text = only != null ? only.Name.ToUpperInvariant() : "INJURIES";
        if (only != null)
        {
            PartHeader(only, true);
            PartRows(only, true);
            Button all = UITheme.Button("All", list, "Show all parts", () => Select(""), UITheme.ButtonStyle.Ghost, 0f, 30f);
            built.Add(all.gameObject);
            return;
        }
        bool any = false;
        foreach (InjuryPartState p in injuries.Parts)
        {
            if (p.Injuries.Count == 0 && !(p.Lost && p.LostWith == null))
                continue;
            any = true;
            PartHeader(p, false);
            PartRows(p, false);
        }
        if (!any)
            Note("No injuries. Hits can cause scratches, cuts, fractures and worse; treat them here with medical items.");
    }

    private void PartHeader(InjuryPartState p, bool detailed)
    {
        string head = p.Lost ? $"<b>{p.Name}</b>  {StatText.Paint("missing", -1)}" : $"<b>{p.Name}</b>  condition {p.Condition01 * 100f:0}%";
        if (!p.Lost && p.Impairment >= 0.05f)
            head += $"  ·  impaired {p.Impairment * 100f:0}%";
        TextMeshProUGUI t = InventoryUIFactory.Text("Part", list, head, 15f, UITheme.Text, TextAlignmentOptions.Left);
        t.richText = true;
        built.Add(t.gameObject);
        if (!detailed)
            return;
        string effects = PartEffects(p);
        if (!string.IsNullOrEmpty(effects))
        {
            TextMeshProUGUI e = InventoryUIFactory.Text("Effects", list, StatText.Paint(effects, -1), 13f, UITheme.Muted, TextAlignmentOptions.Left);
            e.richText = true;
            built.Add(e.gameObject);
        }
    }

    private void PartRows(InjuryPartState p, bool detailed)
    {
        if (p.Lost && p.LostWith != null)
        {
            Note($"Lost with the {p.LostWith.Name.ToLowerInvariant()}.");
            return;
        }
        if (p.Lost)
            Row(p, null);
        foreach (Injury i in p.Injuries)
            Row(p, i);
        if (detailed && !p.Lost && p.Injuries.Count == 0)
            Note("No injuries here.");
    }

    private void Note(string text)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text("Note", list, text, 14f, UITheme.Muted, TextAlignmentOptions.Left);
        built.Add(t.gameObject);
    }

    /// <summary>One injury (or the missing limb when <paramref name="injury"/> is null): name, severity, status, progress.</summary>
    private void Row(InjuryPartState part, Injury injury)
    {
        InjurySettings s = injuries.Settings;
        Color sevColor = injury != null ? s.Severity(injury.Severity).color : Critical;
        Image bg = UITheme.Panel("Injury", list, new Color(1f, 1f, 1f, 0.045f), UITheme.RadiusSmall);
        VerticalLayoutGroup v = InventoryUIFactory.Vertical(bg.gameObject, 0, 2f, false);
        v.padding = new RectOffset(14, 10, 6, 8);
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

        string title = injury != null
            ? $"<b>{injury.Name}</b>  <color=#{ColorUtility.ToHtmlStringRGB(sevColor)}>{s.SeverityName(injury.Severity)}</color>"
            : $"<b>Missing {part.Name}</b>";
        TextMeshProUGUI t = InventoryUIFactory.Text("Title", bg.transform, title, 15f, UITheme.Text, TextAlignmentOptions.Left);
        t.richText = true;
        TextMeshProUGUI status = InventoryUIFactory.Text("Status", bg.transform, "", 13f, UITheme.Muted, TextAlignmentOptions.Left);
        status.richText = true;

        Image track = InventoryUIFactory.Panel("Progress", bg.transform, new Color(1f, 1f, 1f, 0.07f));
        InventoryUIFactory.Size(track, -1f, 4f);
        Image bar = InventoryUIFactory.Panel("Fill", track.transform, Healthy);
        bar.raycastTarget = false;
        RectTransform brt = bar.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = new Vector2(0f, 1f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;

        var row = new RowView { part = part, injury = injury, status = status, progress = brt, progressImage = bar };
        rows.Add(row);
        var trigger = bg.gameObject.AddComponent<InjuryUITrigger>();
        trigger.panel = this;
        trigger.hover = () => injury != null ? InjuryHover(part, injury) : MissingHover(part);
        trigger.click = e => OpenMenu(part, injury);
        UpdateRow(row);
    }

    private void UpdateRow(RowView row)
    {
        if (row.status == null)
            return;
        InjurySettings s = injuries.Settings;
        Injury i = row.injury;
        if (i == null)
        {
            SeveredLimbRecord r = row.part.LostLimb;
            bool carried = injuries.FindLimb(row.part, false, out _, out _) != null;
            string limbState = r == null || r.Consumed ? "The limb is gone." : r.Expired
                ? StatText.Paint("Too late to reattach normally.", -1)
                : $"Reattach within {StatText.Paint(SeveredLimbRecord.FormatTime(r.Remaining), 0)}  ·  " +
                  (carried ? StatText.Paint("limb in your bag", 1) : StatText.Paint("limb not carried", -1));
            row.status.text = limbState + "\n" + StatText.Paint("Click to reattach.", 0);
            float left = r != null && r.TimeLimit > 0f ? r.Remaining / r.TimeLimit : 0f;
            row.progress.anchorMax = new Vector2(Mathf.Clamp01(left), 1f);
            row.progressImage.color = Critical;
            return;
        }
        var bits = new List<string>();
        float bleed = i.BleedingPerSecond;
        if (bleed > 1e-4f) bits.Add(StatText.Paint($"bleeding {bleed:0.##}/s", -1));
        else if (i.Stage != null && i.Stage.bleedingPerSecond > 0f) bits.Add(i.Clotted ? "stopped bleeding" : "bleeding controlled");
        string label = string.IsNullOrEmpty(i.TreatmentLabel) ? "bandaged" : i.TreatmentLabel.ToLowerInvariant();
        if (i.Treated || i.Bandaged) bits.Add(StatText.Paint(label, 1));
        if (i.CanWorsen) bits.Add(StatText.Paint($"worsens in {SeveredLimbRecord.FormatTime(i.WorsensIn)}", -1));
        float healsIn = i.HealsIn(s.healingSpeedMultiplier);
        if (!float.IsInfinity(healsIn)) bits.Add($"heals in {SeveredLimbRecord.FormatTime(healsIn)}");
        else if (i.Stage != null && i.Stage.needsTreatmentToHeal && !i.HealingAllowed) bits.Add(StatText.Paint("needs treatment to heal", -1));
        if (i.Impairment >= 0.05f) bits.Add($"impairs {i.Impairment * 100f:0}%");
        row.status.text = string.Join("  ·  ", bits);
        row.progress.anchorMax = new Vector2(Mathf.Clamp01(i.HealProgress), 1f);
        row.progressImage.color = i.HealRate > 0f ? Healthy : new Color(0.6f, 0.6f, 0.65f);
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
            else if (p.LostLimb != null && !p.LostLimb.Consumed)
                t.Append('\n').Append(p.LostLimb.Expired ? StatText.Paint("Too late to reattach normally.", -1) : $"Reattach within {SeveredLimbRecord.FormatTime(p.LostLimb.Remaining)}.");
        }
        else
        {
            t.Append($"\nCondition {p.Condition:0} / {p.MaxCondition:0}");
            if (p.Impairment >= 0.05f)
                t.Append($"  ·  impaired {p.Impairment * 100f:0}%");
        }
        foreach (Injury i in p.Injuries)
            t.Append("\n• ").Append(i.Name).Append(" (").Append(injuries.Settings.SeverityName(i.Severity)).Append(')')
             .Append(i.IsBleeding ? StatText.Paint(" bleeding", -1) : "");
        string effects = PartEffects(p);
        if (!string.IsNullOrEmpty(effects))
            t.Append('\n').Append(StatText.Paint(effects, -1));
        t.Append('\n').Append(StatText.Paint("Click to list its injuries · right-click to treat.", 0));
        return t.ToString();
    }

    private string InjuryHover(InjuryPartState p, Injury i)
    {
        InjurySettings s = injuries.Settings;
        InjuryStage st = i.Stage;
        var t = new StringBuilder();
        Color sev = s.Severity(i.Severity).color;
        t.Append("<b>").Append(i.Name).Append("</b> on the ").Append(p.Name.ToLowerInvariant())
         .Append($"  <color=#{ColorUtility.ToHtmlStringRGB(sev)}>{s.SeverityName(i.Severity)}</color>");
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
            if (i.Impairment > 0f)
                t.Append($"\nImpairs the {p.Name.ToLowerInvariant()} {i.Impairment * 100f:0}%");
            if (st.consequence != null && st.consequence.HasAnyEffect)
            {
                string d = st.consequence.Describe();
                if (d != "no effect") t.Append('\n').Append(StatText.Paint(d, -1));
            }
            if (st.blocks != BodyAction.None)
                t.Append('\n').Append(StatText.Paint(BodyActions.DescribeAll(st.blocks), -1));
        }
        if (i.Bandaged) t.Append('\n').Append(StatText.Paint($"Bandaged ({i.BandageEffectiveness * 100f:0}% of the bleeding stopped)", 1));
        if (i.Treated) t.Append('\n').Append(StatText.Paint("Treated: " + i.TreatmentLabel, 1));
        float healsIn = i.HealsIn(s.healingSpeedMultiplier);
        if (!float.IsInfinity(healsIn))
            t.Append($"\nHeals in {SeveredLimbRecord.FormatTime(healsIn)} ({i.HealProgress * 100f:0}% done)");
        else if (st != null && st.needsTreatmentToHeal && !i.HealingAllowed)
            t.Append('\n').Append(StatText.Paint("Does not heal until treated.", -1));
        if (i.CanWorsen)
            t.Append('\n').Append(StatText.Paint($"Gets worse in {SeveredLimbRecord.FormatTime(i.WorsensIn)} if left untreated.", -1));
        AppendTreatmentHints(t, p, i);
        t.Append('\n').Append(StatText.Paint("Click to treat it.", 0));
        return t.ToString();
    }

    private string MissingHover(InjuryPartState p)
    {
        var t = new StringBuilder();
        t.Append("<b>Missing ").Append(p.Name).Append("</b>");
        SeveredLimbRecord r = p.LostLimb;
        if (r != null && !r.Consumed)
        {
            t.Append($"\nLimb condition {r.Condition * 100f:0}%");
            t.Append('\n').Append(r.Expired
                ? StatText.Paint("Its time limit ran out: only special abilities (or a city surgeon, later) can reattach it.", -1)
                : $"Reattach it within {SeveredLimbRecord.FormatTime(r.Remaining)} with a surgical kit (or a healing ability).");
            if (r.IncludedParts.Count > 0)
                t.Append("\nCame off with: ").Append(string.Join(", ", r.IncludedParts));
            if (r.DroppedItems.Count > 0)
                t.Append("\nDropped with it: ").Append(string.Join(", ", r.DroppedItems));
        }
        bool carried = injuries.FindLimb(p, false, out _, out _) != null;
        t.Append('\n').Append(carried ? StatText.Paint("The limb is in your bag.", 1) : StatText.Paint("Pick the limb up first (it lies where you lost it).", -1));
        string effects = PartEffects(p);
        if (!string.IsNullOrEmpty(effects))
            t.Append('\n').Append(StatText.Paint(effects, -1));
        AppendTreatmentHints(t, p, null);
        t.Append('\n').Append(StatText.Paint("Click to reattach.", 0));
        return t.ToString();
    }

    /// <summary>"You can treat it with: Bandage (3), Suture Kit (1)" from the bag.</summary>
    private void AppendTreatmentHints(StringBuilder t, InjuryPartState p, Injury i)
    {
        injuries.FindItemTreatments(p, i, options);
        if (options.Count == 0)
        {
            t.Append('\n').Append(StatText.Paint("You carry nothing that treats it.", 0));
            return;
        }
        var names = new List<string>();
        foreach (TreatmentOption o in options)
            names.Add($"{o.item.Name} ({o.count})");
        t.Append("\nTreat with: ").Append(string.Join(", ", names));
    }

    /// <summary>What a part does to the character now: its consequence and the actions it stops.</summary>
    private string PartEffects(InjuryPartState p)
    {
        var bits = new List<string>();
        float imp = p.Lost ? 1f : p.Impairment;
        StatusConsequence c = p.Lost ? (p.Def.lost != null && p.Def.lost.HasAnyEffect ? p.Def.lost : p.Def.impaired) : imp >= 0.05f ? p.Def.impaired : null;
        if (c != null)
        {
            string d = c.Describe(p.Lost ? 1f : imp);
            if (d != "no effect") bits.Add(d);
        }
        BodyAction blocked = BodyAction.None;
        BodyPartSide main = injuries.Settings.mainHandSide;
        if (p.Def.actionRules != null)
            foreach (BodyActionRule rule in p.Def.actionRules)
                if (rule != null && imp + 1e-4f >= rule.fromImpairment)
                    blocked |= BodyActions.ResolveSide(rule.actions, p.Def.side, main);
        foreach (Injury i in p.Injuries)
            if (i.Stage != null)
                blocked |= BodyActions.ResolveSide(i.Stage.blocks, p.Def.side, main);
        if (blocked != BodyAction.None)
            bits.Add(BodyActions.DescribeAll(blocked));
        return string.Join("; ", bits);
    }

    // ------------------------------------------------------------------ clicks
    private void ClickPart(InjuryPartState p, PointerEventData e)
    {
        if (e != null && e.button == PointerEventData.InputButton.Right)
        {
            Injury urgent = null;
            foreach (Injury i in p.Injuries)
                if (urgent == null || i.BleedingPerSecond > urgent.BleedingPerSecond || (i.BleedingPerSecond == urgent.BleedingPerSecond && i.Severity > urgent.Severity))
                    urgent = i;
            if (urgent != null || (p.Lost && p.LostWith == null))
                OpenMenu(p, urgent);
            return;
        }
        Select(string.Equals(selectedPart, p.Id, System.StringComparison.OrdinalIgnoreCase) ? "" : p.Id);
    }

    private void Select(string partId)
    {
        selectedPart = partId ?? "";
        shownSignature = null;
        Refresh();
    }

    // ------------------------------------------------------------------ treatment menu
    /// <summary>The medical items the player carries that treat this injury (or reattach this limb), to pick one.</summary>
    private void OpenMenu(InjuryPartState part, Injury injury)
    {
        CloseMenu();
        HideHover();
        if (injuries == null || part == null)
            return;
        injuries.FindItemTreatments(part, injury, options);

        Image blocker = InventoryUIFactory.Panel("Treatment Menu Blocker", overlayParent, new Color(0f, 0f, 0f, 0.001f));
        InventoryUIFactory.Stretch(blocker.rectTransform);
        Button close = blocker.gameObject.AddComponent<Button>();
        close.transition = Selectable.Transition.None;
        close.onClick.AddListener(CloseMenu);
        menuBlocker = blocker.gameObject;

        Image panel = UITheme.Panel("Treatment Menu", overlayParent, new Color(0.04f, 0.05f, 0.07f, 0.98f), UITheme.Radius);
        menu = panel.gameObject;
        RectTransform rt = panel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(360f, 10f);
        InventoryUIFactory.Vertical(panel.gameObject, 12, 6f, true);
        var shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -4f);

        string title = injury != null ? $"Treat {injury.Name} ({part.Name})" : $"Reattach the {part.Name.ToLowerInvariant()}";
        TextMeshProUGUI t = InventoryUIFactory.Text("Title", panel.transform, title, 16f, UITheme.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        t.richText = true;
        if (options.Count == 0)
        {
            InventoryUIFactory.Text("None", panel.transform, injury != null
                ? "You carry nothing that treats this injury."
                : "You need a surgical kit (or a healing ability) and the limb in your bag.", 14f, UITheme.Muted, TextAlignmentOptions.Left);
        }
        foreach (TreatmentOption o in options)
        {
            TreatmentOption opt = o;
            float seconds = opt.treatment.applySeconds * injuries.Settings.treatmentTimeMultiplier;
            string label = $"{opt.item.Name} ×{opt.count}  -  {opt.treatment.name}" + (seconds > 0f ? $"  ({seconds:0.#} s)" : "");
            Button b = UITheme.Button(opt.item.Name, panel.transform, label, () =>
            {
                CloseMenu();
                injuries.BeginTreatment(opt.treatment, part, injury, TreatmentSource.Item, opt.item);
            }, opt.usable ? UITheme.ButtonStyle.Primary : UITheme.ButtonStyle.Normal, 0f, 38f);
            TMP_Text bl = b.GetComponentInChildren<TMP_Text>(true);
            if (bl != null) bl.fontSize = 15f;
            b.interactable = opt.usable;
            if (!opt.usable && !string.IsNullOrEmpty(opt.reason))
                InventoryUIFactory.Text("Why", panel.transform, opt.reason, 13f, UITheme.Bad, TextAlignmentOptions.Left);
        }
        string city = CityInjuryTreatment.StatusLine;
        if (!string.IsNullOrEmpty(city))
            InventoryUIFactory.Text("City", panel.transform, "City healer: " + city, 13f, UITheme.Muted, TextAlignmentOptions.Left);
        UITheme.Button("Close", panel.transform, "Close", CloseMenu, UITheme.ButtonStyle.Ghost, 0f, 32f);

        menuBlocker.transform.SetAsLastSibling();
        menu.transform.SetAsLastSibling();
        Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : (Vector2)rt.position;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        rt.position = mouse + new Vector2(8f, -8f);
        InventoryUIFactory.ClampToCanvas(rt, canvas != null ? canvas : GetComponentInParent<Canvas>());
    }

    private void CloseMenu()
    {
        if (menu != null) Destroy(menu);
        if (menuBlocker != null) Destroy(menuBlocker);
        menu = null;
        menuBlocker = null;
    }

    // ------------------------------------------------------------------ hover
    internal void ShowHover(string text)
    {
        if (hoverPanel == null || hoverText == null || menu != null)
            return;
        hoverText.text = text;
        hoverPanel.gameObject.SetActive(true);
        hoverPanel.SetAsLastSibling();
        FollowMouse();
    }

    internal void HideHover()
    {
        if (hoverPanel != null && hoverPanel.gameObject.activeSelf)
            hoverPanel.gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (hoverPanel == null || !hoverPanel.gameObject.activeSelf)
            return;
        Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
        hoverPanel.position = mouse + new Vector2(18f, -18f);
        InventoryUIFactory.ClampToCanvas(hoverPanel, canvas != null ? canvas : GetComponentInParent<Canvas>());
    }
}
