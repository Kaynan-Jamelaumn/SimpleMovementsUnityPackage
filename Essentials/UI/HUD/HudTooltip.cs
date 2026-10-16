using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The HUD's hover panel: a title, a body and a footer, shown above the hovered slot and kept inside the screen.
/// Fades in; never blocks the pointer.
/// </summary>
public class HudTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI body;
    [SerializeField] private TextMeshProUGUI footer;
    [SerializeField] private CanvasGroup group;

    private RectTransform target;
    private float alpha;

    public static HudTooltip Create(Transform parent, HudPalette p, float width = 320f)
    {
        Image frame = HudStyle.Frame("Tooltip", parent, p, 12);
        var tip = frame.gameObject.AddComponent<HudTooltip>();
        tip.panel = frame.rectTransform;
        tip.group = frame.gameObject.AddComponent<CanvasGroup>();
        tip.group.blocksRaycasts = false;
        tip.group.interactable = false;
        tip.group.alpha = 0f;
        frame.rectTransform.pivot = new Vector2(0.5f, 0f);
        frame.rectTransform.sizeDelta = new Vector2(width, 100f);

        var v = frame.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 12, 12);
        v.spacing = 6f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        var fit = frame.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // The inner fill of the frame must not take part in the layout.
        Transform fill = frame.transform.Find("Fill");
        if (fill != null) fill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        tip.title = Line(frame.transform, "Title", 19f, p.accent, FontStyles.Bold);
        tip.body = Line(frame.transform, "Body", 15f, p.text, FontStyles.Normal);
        tip.footer = Line(frame.transform, "Footer", 13.5f, p.muted, FontStyles.Italic);
        frame.gameObject.SetActive(false);
        return tip;
    }

    private static TextMeshProUGUI Line(Transform parent, string name, float size, Color color, FontStyles style)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text(name, parent, "", size, color, TextAlignmentOptions.TopLeft, style);
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        return t;
    }

    /// <summary>Shows the panel above <paramref name="over"/>.</summary>
    public void Show(RectTransform over, string titleText, string bodyText, string footerText)
    {
        target = over;
        title.text = titleText;
        body.text = bodyText;
        body.gameObject.SetActive(!string.IsNullOrEmpty(bodyText));
        footer.text = footerText;
        footer.gameObject.SetActive(!string.IsNullOrEmpty(footerText));
        if (!gameObject.activeSelf) { gameObject.SetActive(true); alpha = 0f; }
        transform.SetAsLastSibling();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        Position();
    }

    /// <summary>Updates the texts while shown (cooldowns tick).</summary>
    public void Refresh(string titleText, string bodyText, string footerText)
    {
        if (title.text != titleText) title.text = titleText;
        if (body.text != bodyText) body.text = bodyText;
        if (footer.text != footerText) footer.text = footerText;
        footer.gameObject.SetActive(!string.IsNullOrEmpty(footerText));
    }

    public void Hide(RectTransform from = null)
    {
        if (from != null && from != target) return;
        target = null;
    }

    public bool IsShowing(RectTransform over) => target == over && target != null;

    private void LateUpdate()
    {
        alpha = Mathf.MoveTowards(alpha, target != null ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        group.alpha = alpha;
        if (target == null && alpha <= 0f) { gameObject.SetActive(false); return; }
        if (target != null) Position();
    }

    /// <summary>Centred above the target, kept inside its canvas.</summary>
    private void Position()
    {
        if (target == null || panel.parent == null) return;
        var parent = (RectTransform)panel.parent;
        Vector3 top = target.TransformPoint(new Vector3(target.rect.center.x, target.rect.yMax + 12f, 0f));
        Vector2 local = parent.InverseTransformPoint(top);
        Rect area = parent.rect;
        Vector2 size = panel.rect.size;
        local.x = Mathf.Clamp(local.x, area.xMin + size.x * 0.5f + 8f, area.xMax - size.x * 0.5f - 8f);
        if (local.y + size.y > area.yMax - 8f)
        {
            // No room above: below the target.
            Vector3 bottom = target.TransformPoint(new Vector3(target.rect.center.x, target.rect.yMin - 12f, 0f));
            local.y = ((Vector2)parent.InverseTransformPoint(bottom)).y - size.y;
        }
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = local - area.center;
    }
}
