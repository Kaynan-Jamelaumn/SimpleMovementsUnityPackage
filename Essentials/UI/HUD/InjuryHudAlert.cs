using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The local player's injuries on the screen, outside the inventory (Injury Settings ▸ Show Hud Alert):
/// <list type="bullet">
/// <item>a small body figure (Show Hud Body): every part coloured by its state, missing limbs dark, the part that was
/// just hit flashing - so the player sees where a blow landed without opening anything;</item>
/// <item>warnings, the most serious first, each with its icon: bleeding (and how fast), missing limbs with the time
/// left to reattach them, and every injury whose type has a Hud Alert - a broken bone, an infection (with the time
/// before it gets worse when that is near), an illness;</item>
/// <item>the treatment being applied, with its progress.</item>
/// </list>
/// Nothing shows while the body is fine. Which injuries warn, and how loudly, is data: each injury type's Hud Alert.
/// Created by the player's <see cref="InjuryController"/>.
/// </summary>
public class InjuryHudAlert : MonoBehaviour
{
    private sealed class Line
    {
        public RectTransform root;
        public Image icon;
        public TextMeshProUGUI text;
    }

    private struct Entry
    {
        public int level;       // InjuryAlertLevel as a number: sorted high to low
        public Sprite icon;
        public Color color;
        public string text;
    }

    private sealed class PartView
    {
        public InjuryPartState state;
        public BodyShapeGraphic shape;
    }

    private const int MaxLines = 5;
    private const float FigureWidth = 58f, FigureHeight = 94f, LineHeight = 22f;

    [SerializeField] private InjuryController injuries;
    [SerializeField] private RectTransform root;
    [SerializeField] private Image background;
    [SerializeField] private RectTransform figure;
    [SerializeField] private RectTransform lineColumn;
    [SerializeField] private RectTransform progressTrack;
    [SerializeField] private RectTransform progressFill;

    private readonly List<Line> lines = new List<Line>();
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<PartView> partViews = new List<PartView>();
    private readonly Dictionary<InjuryTypeDefinition, int> worstByType = new Dictionary<InjuryTypeDefinition, int>();
    private readonly List<InjuryTypeDefinition> typeOrder = new List<InjuryTypeDefinition>();
    private readonly StringBuilder sb = new StringBuilder();
    private InjurySettings builtFor;
    private int builtParts = -1;
    private int shownVersion = -1;
    private float nextRefresh;
    private float nextTimers;
    private bool critical;
    private bool figureWanted;

    private static readonly Color Healthy = new Color(0.36f, 0.72f, 0.45f);
    private static readonly Color Hurt = new Color(0.93f, 0.74f, 0.3f);
    private static readonly Color Bad = new Color(0.86f, 0.27f, 0.24f);
    private static readonly Color Missing = new Color(0.12f, 0.12f, 0.15f);
    private static readonly Color PanelColor = new Color(0.05f, 0.055f, 0.075f, 0.86f);
    private static readonly Color PanelCritical = new Color(0.36f, 0.04f, 0.06f, 0.9f);

    public static InjuryHudAlert Create(InjuryController owner)
    {
        if (owner == null)
            return null;
        Canvas canvas = UITheme.Canvas("Injury Alert", 40);
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.enabled = false;
        var alert = canvas.gameObject.AddComponent<InjuryHudAlert>();
        alert.injuries = owner;

        alert.background = UITheme.Panel("Alert", canvas.transform, PanelColor, UITheme.Radius);
        alert.background.raycastTarget = false;
        alert.root = alert.background.rectTransform;
        alert.root.anchorMin = alert.root.anchorMax = new Vector2(0.5f, 1f);
        alert.root.pivot = new Vector2(0.5f, 1f);
        alert.root.anchoredPosition = new Vector2(0f, -18f);
        alert.root.sizeDelta = new Vector2(360f, FigureHeight + 16f);

        // The body figure, on the left.
        alert.figure = HudStyle.Rect("Figure", alert.root);
        alert.figure.anchorMin = alert.figure.anchorMax = new Vector2(0f, 0.5f);
        alert.figure.pivot = new Vector2(0f, 0.5f);
        alert.figure.anchoredPosition = new Vector2(10f, 0f);
        alert.figure.sizeDelta = new Vector2(FigureWidth, FigureHeight);

        // The warnings, to its right.
        alert.lineColumn = HudStyle.Rect("Lines", alert.root);
        alert.lineColumn.anchorMin = new Vector2(0f, 0f);
        alert.lineColumn.anchorMax = new Vector2(1f, 1f);
        for (int i = 0; i < MaxLines; i++)
        {
            var line = new Line { root = HudStyle.Rect("Line", alert.lineColumn) };
            line.root.anchorMin = new Vector2(0f, 1f);
            line.root.anchorMax = new Vector2(1f, 1f);
            line.root.pivot = new Vector2(0f, 1f);
            line.root.sizeDelta = new Vector2(0f, LineHeight);
            line.icon = HudStyle.Image("Icon", line.root, Color.white, InjuryIcons.Shape(InjuryIconShape.Dot));
            HudStyle.Place(line.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(16f, 16f));
            line.icon.preserveAspect = true;
            line.text = HudStyle.Text("Text", line.root, "", 15f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            RectTransform tr = line.text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(22f, 0f);
            tr.offsetMax = Vector2.zero;
            line.text.richText = true;
            line.root.gameObject.SetActive(false);
            alert.lines.Add(line);
        }

        // The treatment's progress, along the bottom.
        Image track = HudStyle.Image("Progress", alert.root, new Color(1f, 1f, 1f, 0.12f));
        alert.progressTrack = track.rectTransform;
        alert.progressTrack.anchorMin = new Vector2(0f, 0f);
        alert.progressTrack.anchorMax = new Vector2(1f, 0f);
        alert.progressTrack.pivot = new Vector2(0.5f, 0f);
        alert.progressTrack.offsetMin = new Vector2(10f, 4f);
        alert.progressTrack.offsetMax = new Vector2(-10f, 7f);
        Image fill = HudStyle.Image("Fill", track.transform, new Color(0.45f, 0.72f, 1f, 0.95f));
        alert.progressFill = fill.rectTransform;
        alert.progressFill.anchorMin = Vector2.zero;
        alert.progressFill.anchorMax = new Vector2(0f, 1f);
        alert.progressFill.offsetMin = alert.progressFill.offsetMax = Vector2.zero;
        track.gameObject.SetActive(false);

        alert.background.gameObject.SetActive(false);
        return alert;
    }

    /// <summary>The figure's parts (again when the settings or the tracked parts change).</summary>
    private void BuildFigure()
    {
        foreach (PartView pv in partViews)
            if (pv.shape != null) Destroy(pv.shape.gameObject);
        partViews.Clear();
        builtFor = injuries.Settings;
        builtParts = injuries.Parts.Count;
        foreach (InjuryPartState part in injuries.Parts)
        {
            if (part.IsWholeBody)
                continue;
            Vector2[] shape = InjuryHealthPanel.ShapeOf(part.Def);
            if (shape == null)
                continue;
            var g = HudStyle.Rect(part.Id, figure).gameObject.AddComponent<BodyShapeGraphic>();
            g.raycastTarget = false;
            g.color = Healthy;
            g.PlaceOnFigure(shape);
            partViews.Add(new PartView { state = part, shape = g });
        }
    }

    /// <summary>Redraws (called by the controller every frame; the texts are rebuilt a few times a second).</summary>
    public void Tick()
    {
        if (injuries == null || background == null)
            return;
        float now = Time.unscaledTime;
        if (now >= nextRefresh)
        {
            nextRefresh = now + 0.2f;
            // The texts: when something changed, and once a second for the countdowns.
            if (injuries.Version != shownVersion || now >= nextTimers)
            {
                shownVersion = injuries.Version;
                nextTimers = now + 1f;
                Rebuild();
            }
            RefreshProgress();
        }
        if (!background.gameObject.activeSelf)
            return;
        RefreshFigure();
        float pulse = critical ? 0.78f + 0.22f * Mathf.Sin(now * 6f) : 1f;
        Color c = critical ? PanelCritical : PanelColor;
        background.color = new Color(c.r, c.g, c.b, c.a * pulse);
    }

    private void Rebuild()
    {
        InjurySettings s = injuries.Settings;
        CollectEntries(s);
        figureWanted = s.showHudBody && FigureHasSomethingToShow();
        bool show = entries.Count > 0 || figureWanted;
        if (background.gameObject.activeSelf != show)
            background.gameObject.SetActive(show);
        if (!show)
            return;
        if (figureWanted && (builtFor != s || builtParts != injuries.Parts.Count))
            BuildFigure();
        if (figure.gameObject.activeSelf != figureWanted)
            figure.gameObject.SetActive(figureWanted);

        int shown = Mathf.Min(entries.Count, MaxLines);
        float widest = 0f;
        critical = false;
        for (int i = 0; i < lines.Count; i++)
        {
            Line line = lines[i];
            bool on = i < shown;
            if (line.root.gameObject.activeSelf != on)
                line.root.gameObject.SetActive(on);
            if (!on)
                continue;
            Entry e = entries[i];
            // The last line says how many more there are.
            string text = i == MaxLines - 1 && entries.Count > MaxLines ? $"{e.text}   <color=#b9bec7>+{entries.Count - MaxLines} more</color>" : e.text;
            if (line.text.text != text)
                line.text.text = text;
            line.text.color = e.level >= (int)InjuryAlertLevel.Critical ? Color.white : e.level >= (int)InjuryAlertLevel.Warning ? new Color(1f, 0.9f, 0.82f) : new Color(0.86f, 0.88f, 0.92f);
            line.icon.sprite = e.icon;
            line.icon.color = e.color;
            line.root.anchoredPosition = new Vector2(0f, -i * LineHeight);
            widest = Mathf.Max(widest, line.text.preferredWidth + 24f);
            if (e.level >= (int)InjuryAlertLevel.Critical)
                critical = true;
        }
        // The panel fits the figure and the lines.
        float left = figureWanted ? FigureWidth + 22f : 12f;
        float linesHeight = shown * LineHeight;
        float height = Mathf.Max(figureWanted ? FigureHeight + 16f : 0f, linesHeight + 16f);
        float width = Mathf.Clamp(left + widest + 12f, figureWanted && shown == 0 ? FigureWidth + 20f : 200f, 900f);
        root.sizeDelta = new Vector2(width, height);
        lineColumn.offsetMin = new Vector2(left, 0f);
        lineColumn.offsetMax = new Vector2(-10f, -(height - linesHeight) * 0.5f);
    }

    /// <summary>The figure shows while a part is hurt, missing or was just hit.</summary>
    private bool FigureHasSomethingToShow()
    {
        IReadOnlyList<InjuryPartState> parts = injuries.Parts;
        for (int i = 0; i < parts.Count; i++)
        {
            InjuryPartState p = parts[i];
            if (p.IsWholeBody)
                continue;
            if (p.Lost || p.VisibleInjuries > 0 || p.Condition01 < 0.97f || Time.time - p.LastHitTime < 4f)
                return true;
        }
        return false;
    }

    private void CollectEntries(InjurySettings s)
    {
        entries.Clear();
        float bleed = injuries.BleedingPerSecond;
        if (bleed > 1e-4f)
        {
            bool heavy = bleed >= s.bleeding.heavyBleedingPerSecond;
            entries.Add(new Entry
            {
                level = (int)(heavy ? InjuryAlertLevel.Critical : InjuryAlertLevel.Warning) * 10 + 5,
                icon = InjuryIcons.Shape(InjuryIconShape.Drop), color = new Color(1f, 0.25f, 0.25f),
                text = heavy ? $"HEAVY BLEEDING  {bleed:0.#}/s" : $"Bleeding  {bleed:0.##}/s",
            });
        }
        worstByType.Clear();
        typeOrder.Clear();
        IReadOnlyList<InjuryPartState> parts = injuries.Parts;
        for (int p = 0; p < parts.Count; p++)
        {
            InjuryPartState part = parts[p];
            if (part.Lost && part.LostWith == null)
            {
                SeveredLimbRecord r = part.LostLimb;
                bool inTime = r != null && !r.Consumed && !r.Gone && !r.Expired;
                entries.Add(new Entry
                {
                    level = (int)(inTime ? InjuryAlertLevel.Critical : InjuryAlertLevel.Warning) * 10 + 3,
                    icon = InjuryIcons.Cross(), color = new Color(1f, 0.55f, 0.5f),
                    text = inTime ? $"{part.Name} lost  {SeveredLimbRecord.FormatTime(r.Remaining)}" : $"{part.Name} lost",
                });
            }
            IReadOnlyList<Injury> list = part.Injuries;
            for (int i = 0; i < list.Count; i++)
            {
                Injury inj = list[i];
                if (inj.Hidden || inj.Type == null || inj.Type.hudAlert == InjuryAlertLevel.None)
                    continue;
                if (!worstByType.TryGetValue(inj.Type, out int count))
                    typeOrder.Add(inj.Type);
                worstByType[inj.Type] = count + 1;
            }
        }
        // One line per injury type that warns: its worst stage, how many, and when it gets worse if that is near.
        for (int t = 0; t < typeOrder.Count; t++)
        {
            InjuryTypeDefinition type = typeOrder[t];
            Injury worst = null;
            float soonest = float.PositiveInfinity;
            InjuryPartState where = null;
            for (int p = 0; p < parts.Count; p++)
            {
                IReadOnlyList<Injury> list = parts[p].Injuries;
                for (int i = 0; i < list.Count; i++)
                {
                    Injury inj = list[i];
                    if (inj.Type != type || inj.Hidden)
                        continue;
                    if (worst == null || inj.StageIndex > worst.StageIndex)
                    {
                        worst = inj;
                        where = parts[p];
                    }
                    if (inj.CanWorsen)
                        soonest = Mathf.Min(soonest, inj.WorsensIn);
                }
            }
            if (worst == null)
                continue;
            int n = worstByType[type];
            sb.Clear();
            sb.Append(worst.Name);
            if (n > 1)
                sb.Append(" ×").Append(n);
            else if (where != null && !where.IsWholeBody && worst.Side == BodyPartSide.Center)
                sb.Append("  <color=#b9bec7>").Append(where.Name).Append("</color>");
            if (soonest < 120f)
                sb.Append("  <color=#ffb46a>worse in ").Append(SeveredLimbRecord.FormatTime(soonest)).Append("</color>");
            else if (worst.Stage != null && worst.Stage.needsTreatmentToHeal && !worst.HealingAllowed)
                sb.Append("  <color=#ffb46a>needs treatment</color>");
            // A severe stage warns one step louder than its type.
            int level = (int)type.hudAlert + (worst.Severity >= 3 && type.hudAlert < InjuryAlertLevel.Critical ? 1 : 0);
            entries.Add(new Entry { level = level * 10 + Mathf.Clamp(worst.Severity, 0, 9) / 3, icon = InjuryIcons.For(type), color = type.color, text = sb.ToString() });
        }
        ActiveTreatment a = injuries.CurrentTreatment;
        if (a != null && a.Treatment != null)
            entries.Add(new Entry
            {
                level = 1, icon = InjuryIcons.Plus(), color = new Color(0.55f, 0.78f, 1f),
                text = $"{a.Treatment.name}  {a.Progress * 100f:0}%",
            });
        entries.Sort((x, y) => y.level.CompareTo(x.level));
        // The level as the alert level again (the sort used a finer number).
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            e.level /= 10;
            entries[i] = e;
        }
    }

    private void RefreshProgress()
    {
        ActiveTreatment a = injuries.CurrentTreatment;
        bool show = a != null && background.gameObject.activeSelf;
        if (progressTrack.gameObject.activeSelf != show)
            progressTrack.gameObject.SetActive(show);
        if (show)
            progressFill.anchorMax = new Vector2(a.Progress, 1f);
    }

    /// <summary>Every part's colour: its condition and impairment, dark when missing, a white flash just after a hit.</summary>
    private void RefreshFigure()
    {
        if (!figureWanted)
            return;
        float now = Time.time;
        for (int i = 0; i < partViews.Count; i++)
        {
            PartView pv = partViews[i];
            if (pv.shape == null)
                continue;
            InjuryPartState p = pv.state;
            Color c;
            if (p.Lost)
                c = Missing;
            else
            {
                c = ConditionColor(p);
                float since = now - p.LastHitTime;
                if (since >= 0f && since < 0.45f)
                    c = Color.Lerp(Color.white, c, since / 0.45f);
                else if (p.IsBleeding)
                    c = Color.Lerp(c, Bad, 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 7f));
            }
            if (pv.shape.color != c)
                pv.shape.color = c;
        }
    }

    /// <summary>Green to yellow to red with the part's condition; redder when it is badly impaired.</summary>
    public static Color ConditionColor(InjuryPartState p)
    {
        float c = p.Condition01;
        Color col = c > 0.5f ? Color.Lerp(Hurt, Healthy, (c - 0.5f) * 2f) : Color.Lerp(Bad, Hurt, c * 2f);
        if (p.Impairment >= 0.6f)
            col = Color.Lerp(col, Bad, 0.5f);
        else if (p.Impairment >= 0.2f)
            col = Color.Lerp(col, Hurt, 0.5f);
        return col;
    }
}
