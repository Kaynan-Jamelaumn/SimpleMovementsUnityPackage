using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds simple UI pieces from code at runtime (panels, texts, buttons, slider, number field), so inventory panels
/// such as the item hover panel and the split popup work without being built or wired in the editor.
/// </summary>
public static class InventoryUIFactory
{
    public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.09f, 0.96f);
    public static readonly Color FieldColor = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.14f);
    public static readonly Color AccentColor = new Color(0.35f, 0.6f, 1f, 1f);
    public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    public static readonly Color MutedColor = new Color(0.7f, 0.72f, 0.76f, 1f);

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image Panel(string name, Transform parent, Color color)
    {
        RectTransform rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static VerticalLayoutGroup Vertical(GameObject go, int padding, float spacing, bool fitHeight)
    {
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(padding, padding, padding, padding);
        v.spacing = spacing;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        if (fitHeight)
        {
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        return v;
    }

    public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing)
    {
        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        h.childAlignment = TextAnchor.MiddleLeft;
        return h;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
    {
        RectTransform rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    public static LayoutElement Size(Component c, float minWidth = -1f, float minHeight = -1f, float flexibleWidth = -1f)
    {
        var le = c.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        if (minWidth >= 0f) { le.minWidth = minWidth; le.preferredWidth = minWidth; }
        if (minHeight >= 0f) { le.minHeight = minHeight; le.preferredHeight = minHeight; }
        if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
        return le;
    }

    public static Button Button(string name, Transform parent, string label, UnityAction onClick, float width = 0f)
    {
        Image img = Panel(name, parent, ButtonColor);
        var button = img.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(onClick);
        TextMeshProUGUI t = Text("Label", img.transform, label, 16f, TextColor, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        Size(img, width > 0f ? width : -1f, 32f, width > 0f ? 0f : 1f);
        return button;
    }

    public static Slider Slider(string name, Transform parent)
    {
        RectTransform root = Rect(name, parent);
        Size(root, -1f, 22f, 1f);
        var bg = root.gameObject.AddComponent<Image>();
        bg.color = FieldColor;

        RectTransform fillArea = Rect("Fill Area", root);
        Stretch(fillArea, 8f, 7f);
        RectTransform fill = Rect("Fill", fillArea);
        Stretch(fill);
        fill.gameObject.AddComponent<Image>().color = AccentColor;

        RectTransform handleArea = Rect("Handle Area", root);
        Stretch(handleArea, 8f, 0f);
        RectTransform handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(16f, 0f);
        var hImg = handle.gameObject.AddComponent<Image>();
        hImg.color = Color.white;

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = hImg;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        return slider;
    }

    public static TMP_InputField NumberField(string name, Transform parent, float width)
    {
        Image bg = Panel(name, parent, FieldColor);
        Size(bg, width, 32f, 0f);
        RectTransform area = Rect("Text Area", bg.transform);
        Stretch(area, 6f, 2f);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI text = Text("Text", area, "", 18f, TextColor, TextAlignmentOptions.Center);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(text.rectTransform);
        var input = bg.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.targetGraphic = bg;
        return input;
    }

    public static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    /// <summary>Keeps <paramref name="rt"/> (pivot anywhere) fully inside its canvas.</summary>
    public static void ClampToCanvas(RectTransform rt, Canvas canvas)
    {
        if (canvas == null) return;
        var canvasRect = (RectTransform)canvas.rootCanvas.transform;
        Vector3[] c = new Vector3[4];
        rt.GetWorldCorners(c);
        Vector3[] k = new Vector3[4];
        canvasRect.GetWorldCorners(k);
        Vector3 shift = Vector3.zero;
        if (c[0].x < k[0].x) shift.x = k[0].x - c[0].x;
        else if (c[2].x > k[2].x) shift.x = k[2].x - c[2].x;
        if (c[0].y < k[0].y) shift.y = k[0].y - c[0].y;
        else if (c[2].y > k[2].y) shift.y = k[2].y - c[2].y;
        rt.position += shift;
    }
}
