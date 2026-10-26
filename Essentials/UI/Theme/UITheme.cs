using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The look of every menu built from code (start menu, character select and creation, pause and settings menu,
/// inventory character panel): one palette, rounded panels (a procedural 9-sliced sprite, so no image asset is needed)
/// and styled controls. Everything works at runtime and in edit mode (the scene builders call the same helpers), and
/// matches <see cref="InventoryUIFactory"/>'s colours so generated windows sit together.
/// </summary>
public static class UITheme
{
    // ------------------------------------------------------------------ palette
    public static readonly Color Background = new Color(0.03f, 0.035f, 0.05f, 1f);
    public static readonly Color Backdrop = new Color(0.01f, 0.012f, 0.02f, 0.78f);
    public static readonly Color Surface = new Color(0.07f, 0.08f, 0.105f, 0.98f);
    public static readonly Color SurfaceRaised = new Color(0.11f, 0.125f, 0.16f, 1f);
    public static readonly Color SurfaceHover = new Color(0.15f, 0.17f, 0.22f, 1f);
    public static readonly Color Sidebar = new Color(0.05f, 0.058f, 0.08f, 1f);
    public static readonly Color Field = new Color(1f, 1f, 1f, 0.06f);
    public static readonly Color Card = new Color(1f, 1f, 1f, 0.035f);
    public static readonly Color Accent = new Color(0.36f, 0.62f, 1f, 1f);
    public static readonly Color AccentDark = new Color(0.2f, 0.38f, 0.72f, 1f);
    public static readonly Color AccentSoft = new Color(0.36f, 0.62f, 1f, 0.2f);
    public static readonly Color Danger = new Color(0.78f, 0.27f, 0.25f, 1f);
    public static readonly Color Gold = new Color(1f, 0.8f, 0.38f, 1f);
    public static readonly Color Good = new Color(0.44f, 0.82f, 0.55f, 1f);
    public static readonly Color Bad = new Color(0.94f, 0.48f, 0.43f, 1f);
    public static Color Text => InventoryUIFactory.TextColor;
    public static Color Muted => InventoryUIFactory.MutedColor;

    public const int RadiusSmall = 6;
    public const int Radius = 10;
    public const int RadiusLarge = 16;

    public enum ButtonStyle { Normal, Primary, Danger, Ghost, Tab, ListItem }

    // ------------------------------------------------------------------ sprites
    private static readonly Dictionary<int, Sprite> rounded = new Dictionary<int, Sprite>();
    private static Sprite triangle;

    /// <summary>A white rounded rectangle (9-sliced: any size keeps the corner radius in canvas pixels).</summary>
    public static Sprite RoundedSprite(int radius)
    {
        radius = Mathf.Clamp(radius, 1, 64);
        if (rounded.TryGetValue(radius, out Sprite s) && s != null)
            return s;
        int size = radius * 2 + 4;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "UITheme Rounded " + radius,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[size * size];
        float r = radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, r, size - r), cy = Mathf.Clamp(fy, r, size - r);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        s = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(r + 1f, r + 1f, r + 1f, r + 1f));
        s.name = tex.name;
        s.hideFlags = HideFlags.DontSave;
        rounded[radius] = s;
        return s;
    }

    /// <summary>A small white triangle pointing down (dropdown arrows).</summary>
    public static Sprite TriangleSprite()
    {
        if (triangle != null)
            return triangle;
        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "UITheme Triangle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Point at the bottom centre, base at the top (y = 24).
                float fy = y + 0.5f, fx = x + 0.5f;
                float t = Mathf.InverseLerp(8f, 24f, fy);
                float half = t * 11f;
                float a = fy > 24.5f || fy < 7.5f ? 0f : Mathf.Clamp01(half - Mathf.Abs(fx - 16f) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        triangle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        triangle.hideFlags = HideFlags.DontSave;
        return triangle;
    }

    /// <summary>Gives an image rounded corners (kept at runtime by a <see cref="UIRoundedImage"/>, so scene-built UI keeps them).</summary>
    public static Image Round(Image img, int radius = Radius)
    {
        if (img == null)
            return null;
        UIRoundedImage r = img.GetComponent<UIRoundedImage>();
        if (r == null)
            r = img.gameObject.AddComponent<UIRoundedImage>();
        r.radius = radius;
        r.Apply();
        return img;
    }

    // ------------------------------------------------------------------ layout
    /// <summary>A screen-space canvas scaled for 1920×1080.</summary>
    public static Canvas Canvas(string name, int sortingOrder, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Places <paramref name="rt"/> on an anchor box of its parent with pixel offsets.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    /// <summary>An area on an anchor box of its parent (no graphic).</summary>
    public static RectTransform Area(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        RectTransform rt = InventoryUIFactory.Rect(name, parent);
        return Place(rt, anchorMin, anchorMax, offsetMin, offsetMax);
    }

    /// <summary>A rounded panel.</summary>
    public static Image Panel(string name, Transform parent, Color color, int radius = Radius)
    {
        Image img = InventoryUIFactory.Panel(name, parent, color);
        return Round(img, radius);
    }

    /// <summary>A centred window of a fixed size with a soft shadow.</summary>
    public static Image Window(string name, Transform parent, Vector2 size)
    {
        Image img = Panel(name, parent, Surface, RadiusLarge);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var shadow = img.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -6f);
        return img;
    }

    /// <summary>A vertical stack (children fill the width).</summary>
    public static RectTransform Column(string name, Transform parent, int padding = 0, float spacing = 10f, bool fitHeight = false)
    {
        RectTransform rt = InventoryUIFactory.Rect(name, parent);
        InventoryUIFactory.Vertical(rt.gameObject, padding, spacing, fitHeight);
        return rt;
    }

    /// <summary>A horizontal row of a fixed height.</summary>
    public static RectTransform Row(string name, Transform parent, float height, float spacing = 12f)
    {
        RectTransform rt = InventoryUIFactory.Rect(name, parent);
        InventoryUIFactory.Horizontal(rt.gameObject, spacing);
        InventoryUIFactory.Size(rt, -1f, height);
        return rt;
    }

    /// <summary>Takes the space left in a layout (horizontal or vertical).</summary>
    public static RectTransform Spacer(Transform parent, bool horizontal = true)
    {
        RectTransform rt = InventoryUIFactory.Rect("Spacer", parent);
        LayoutElement le = InventoryUIFactory.Size(rt, horizontal ? -1f : 0f, horizontal ? 0f : -1f);
        if (horizontal) le.flexibleWidth = 1f;
        else le.flexibleHeight = 1f;
        return rt;
    }

    /// <summary>A rounded card (padded vertical stack whose height fits its content).</summary>
    public static RectTransform CardColumn(string name, Transform parent, int padding = 16, float spacing = 8f, Color? color = null)
    {
        Image bg = Panel(name, parent, color ?? Card, Radius);
        InventoryUIFactory.Vertical(bg.gameObject, padding, spacing, true);
        return bg.rectTransform;
    }

    // ------------------------------------------------------------------ text
    public static TextMeshProUGUI Title(Transform parent, string text, float size = 34f, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text("Title", parent, text, size, Text, align, FontStyles.Bold);
        InventoryUIFactory.Size(t, -1f, size + 14f);
        return t;
    }

    /// <summary>A small upper-case section header in the accent colour.</summary>
    public static TextMeshProUGUI Header(Transform parent, string text, float height = 26f)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text(text, parent, text.ToUpperInvariant(), 14f, Accent, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        t.characterSpacing = 4f;
        InventoryUIFactory.Size(t, -1f, height);
        return t;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, float size = 17f, Color? color = null,
        TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal, string name = "Label")
    {
        return InventoryUIFactory.Text(name, parent, text, size, color ?? Text, align, style);
    }

    // ------------------------------------------------------------------ buttons
    /// <summary>A styled button. <paramref name="width"/> 0 = stretches with the layout.</summary>
    public static Button Button(string name, Transform parent, string label, UnityAction onClick = null, ButtonStyle style = ButtonStyle.Normal,
        float width = 0f, float height = 44f)
    {
        Button b = InventoryUIFactory.Button(name, parent, label, onClick, width);
        InventoryUIFactory.Size(b, width > 0f ? width : -1f, height);
        Style(b, style);
        return b;
    }

    /// <summary>Applies a style to a button (colours, corners, label).</summary>
    public static void Style(Button b, ButtonStyle style)
    {
        if (b == null)
            return;
        Colors(b, style);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.fontSize = style == ButtonStyle.Tab || style == ButtonStyle.ListItem ? 17f : 18f;
            t.fontStyle = style == ButtonStyle.Primary || style == ButtonStyle.Danger ? FontStyles.Bold : FontStyles.Normal;
            t.color = Text;
            if (style == ButtonStyle.Tab || style == ButtonStyle.ListItem)
            {
                t.alignment = TextAlignmentOptions.Left;
                InventoryUIFactory.Stretch(t.rectTransform, 16f, 0f);
            }
        }
    }

    /// <summary>Applies a style's colours and corners only (buttons with their own content).</summary>
    public static void Colors(Button b, ButtonStyle style)
    {
        if (b == null)
            return;
        var img = b.targetGraphic as Image;
        if (img != null)
            Round(img, Radius);
        Color normal;
        switch (style)
        {
            case ButtonStyle.Primary: normal = Accent; break;
            case ButtonStyle.Danger: normal = Danger; break;
            case ButtonStyle.Ghost: normal = new Color(1f, 1f, 1f, 0f); break;
            case ButtonStyle.Tab: normal = new Color(1f, 1f, 1f, 0.0f); break;
            case ButtonStyle.ListItem: normal = Card; break;
            default: normal = SurfaceRaised; break;
        }
        if (img != null)
            img.color = Color.white;
        ColorBlock c = b.colors;
        c.normalColor = normal;
        c.highlightedColor = style == ButtonStyle.Primary ? Color.Lerp(Accent, Color.white, 0.18f)
            : style == ButtonStyle.Danger ? Color.Lerp(Danger, Color.white, 0.15f)
            : style == ButtonStyle.Ghost || style == ButtonStyle.Tab ? new Color(1f, 1f, 1f, 0.07f) : SurfaceHover;
        c.selectedColor = c.highlightedColor;
        c.pressedColor = style == ButtonStyle.Primary ? AccentDark : new Color(1f, 1f, 1f, 0.12f);
        c.disabledColor = new Color(normal.r, normal.g, normal.b, Mathf.Min(normal.a, 0.35f));
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        b.colors = c;
    }

    /// <summary>Shows a tab / list button as the chosen one (accent fill and bold label) or not.</summary>
    public static void SetSelected(Button b, bool selected)
    {
        if (b == null)
            return;
        UIMarker marker = b.GetComponent<UIMarker>();
        if (marker == null)
            marker = b.gameObject.AddComponent<UIMarker>();
        marker.SetOn(selected);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
            t.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
    }

    // ------------------------------------------------------------------ controls
    /// <summary>"Label ........ [control]" row inside a card; returns the row the control is added to.</summary>
    public static RectTransform OptionRow(Transform parent, string label, float labelWidth = 260f, float height = 52f)
    {
        Image bg = Panel(label, parent, Card, Radius);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 14f);
        h.padding = new RectOffset(18, 18, 6, 6);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        InventoryUIFactory.Size(bg, -1f, height);
        TextMeshProUGUI t = InventoryUIFactory.Text("Label", bg.transform, label, 17f, Text, TextAlignmentOptions.Left);
        InventoryUIFactory.Size(t, labelWidth, height - 12f, 0f);
        return bg.rectTransform;
    }

    /// <summary>A styled slider (accent fill, round handle).</summary>
    public static Slider BareSlider(string name, Transform parent, float min, float max, float value, bool whole)
    {
        RectTransform root = InventoryUIFactory.Rect(name, parent);
        InventoryUIFactory.Size(root, -1f, 26f, 1f);

        Image track = Panel("Track", root, Field, RadiusSmall);
        Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
        track.raycastTarget = true;

        RectTransform fillArea = Area("Fill Area", root, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(-10f, 4f));
        Image fill = Panel("Fill", fillArea, Accent, RadiusSmall);
        InventoryUIFactory.Stretch(fill.rectTransform);
        fill.rectTransform.offsetMax = new Vector2(10f, 0f);

        RectTransform handleArea = Area("Handle Slide Area", root, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        Image handle = Panel("Handle", handleArea, Color.white, 10);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        ColorBlock c = slider.colors;
        c.highlightedColor = new Color(0.85f, 0.9f, 1f, 1f);
        c.pressedColor = new Color(0.7f, 0.8f, 1f, 1f);
        slider.colors = c;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.value = value;
        return slider;
    }

    /// <summary>A labelled slider with its value on the right.</summary>
    public static Slider Slider(Transform parent, string label, float min, float max, float value, bool whole, out TextMeshProUGUI valueText)
    {
        RectTransform row = OptionRow(parent, label);
        Slider s = BareSlider("Slider", row, min, max, value, whole);
        valueText = InventoryUIFactory.Text("Value", row, "", 16f, Muted, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(valueText, 90f, 36f, 0f);
        return s;
    }

    /// <summary>A switch-like checkbox.</summary>
    public static Toggle BareToggle(string name, Transform parent, bool value)
    {
        Image box = Panel(name, parent, Field, 13);
        InventoryUIFactory.Size(box, 52f, 28f, 0f);
        Image knob = Panel("Checkmark", box.transform, Accent, 10);
        Place(knob.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(4f, 4f), new Vector2(-4f, -4f));
        var toggle = box.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = knob;
        toggle.isOn = value;
        ColorBlock c = toggle.colors;
        c.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        toggle.colors = c;
        return toggle;
    }

    /// <summary>A labelled checkbox (the whole row can be clicked).</summary>
    public static Toggle Toggle(Transform parent, string label, bool value)
    {
        RectTransform row = OptionRow(parent, label);
        Spacer(row);
        return BareToggle("Toggle", row, value);
    }

    /// <summary>A labelled TextMeshPro dropdown (options are filled later).</summary>
    public static TMP_Dropdown Dropdown(Transform parent, string label)
    {
        RectTransform row = OptionRow(parent, label);
        TMP_Dropdown d = BareDropdown("Dropdown", row);
        InventoryUIFactory.Size((RectTransform)d.transform, -1f, 38f, 1f);
        return d;
    }

    /// <summary>A styled TextMeshPro dropdown.</summary>
    public static TMP_Dropdown BareDropdown(string name, Transform parent)
    {
        GameObject go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        var d = go.GetComponent<TMP_Dropdown>();
        Image bg = go.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = Color.white;
            Round(bg, Radius);
            ColorBlock c = d.colors;
            c.normalColor = SurfaceRaised;
            c.highlightedColor = SurfaceHover;
            c.selectedColor = SurfaceHover;
            c.pressedColor = Surface;
            d.colors = c;
        }
        if (d.captionText != null)
        {
            d.captionText.color = Text;
            d.captionText.fontSize = 16f;
            d.captionText.textWrappingMode = TextWrappingModes.NoWrap;
            d.captionText.overflowMode = TextOverflowModes.Ellipsis;
        }
        Transform arrow = go.transform.Find("Arrow");
        if (arrow != null && arrow.TryGetComponent(out Image a))
        {
            a.sprite = TriangleSprite();
            a.color = Muted;
        }
        if (d.template != null)
        {
            if (d.template.TryGetComponent(out Image tImg))
            {
                tImg.color = Surface;
                Round(tImg, Radius);
            }
            foreach (Image img in d.template.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "Item Background")
                    img.color = new Color(1f, 1f, 1f, 0.0f);
                else if (img.name == "Item Checkmark")
                {
                    img.color = Accent;
                    Round(img, 4);
                    img.rectTransform.sizeDelta = new Vector2(8f, 8f);
                }
                else if (img.name == "Scrollbar")
                    img.color = new Color(0f, 0f, 0f, 0.2f);
                else if (img.name == "Handle")
                {
                    img.color = new Color(1f, 1f, 1f, 0.25f);
                    Round(img, 4);
                }
            }
            foreach (Toggle item in d.template.GetComponentsInChildren<Toggle>(true))
            {
                ColorBlock c = item.colors;
                c.normalColor = new Color(1f, 1f, 1f, 0f);
                c.highlightedColor = AccentSoft;
                c.selectedColor = AccentSoft;
                c.pressedColor = Accent;
                item.colors = c;
                if (item.targetGraphic != null)
                    item.targetGraphic.color = Color.white;
            }
            foreach (TMP_Text t in d.template.GetComponentsInChildren<TMP_Text>(true))
            {
                t.color = Text;
                t.fontSize = 16f;
                // One line per option: a long name ends in "...", it never wraps onto the next option.
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.alignment = TextAlignmentOptions.MidlineLeft;
            }
            // Options tall enough for the font, a list tall enough to show a good few, and a wheel that scrolls it.
            const float itemHeight = 30f;
            if (d.template.Find("Viewport/Content/Item") is RectTransform itemRect)
                itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, itemHeight);
            if (d.template.Find("Viewport/Content") is RectTransform contentRect)
                contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, itemHeight + 4f);
            d.template.sizeDelta = new Vector2(d.template.sizeDelta.x, Mathf.Max(d.template.sizeDelta.y, 300f));
            if (d.template.TryGetComponent(out ScrollRect templateScroll))
            {
                templateScroll.scrollSensitivity = 30f;
                templateScroll.movementType = ScrollRect.MovementType.Clamped;
            }
        }
        return d;
    }

    /// <summary>A styled TextMeshPro input field.</summary>
    public static TMP_InputField Input(string name, Transform parent, string placeholder, int characterLimit = 0, float height = 46f)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        InventoryUIFactory.Size((RectTransform)go.transform, -1f, height, 1f);
        var field = go.GetComponent<TMP_InputField>();
        field.characterLimit = characterLimit;
        if (go.TryGetComponent(out Image bg))
        {
            bg.color = Color.white;
            Round(bg, Radius);
            ColorBlock c = field.colors;
            c.normalColor = Field;
            c.highlightedColor = new Color(1f, 1f, 1f, 0.1f);
            c.selectedColor = new Color(1f, 1f, 1f, 0.12f);
            c.pressedColor = new Color(1f, 1f, 1f, 0.12f);
            field.colors = c;
        }
        if (field.textComponent != null)
        {
            field.textComponent.color = Text;
            field.textComponent.fontSize = 18f;
            field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (field.placeholder is TMP_Text ph)
        {
            ph.text = placeholder;
            ph.color = new Color(1f, 1f, 1f, 0.3f);
            ph.fontSize = 18f;
            ph.fontStyle = FontStyles.Italic;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (field.textViewport != null)
            InventoryUIFactory.Stretch(field.textViewport, 14f, 6f);
        field.caretColor = Accent;
        field.customCaretColor = true;
        field.selectionColor = AccentSoft;
        return field;
    }

    /// <summary>
    /// A scrolling vertical list with a thin scrollbar; returns the content (items laid out top to bottom). The list
    /// takes <paramref name="height"/> pixels, or the remaining space when 0.
    /// </summary>
    public static RectTransform ScrollList(string name, Transform parent, float height, out ScrollRect scroll, Color? background = null, int padding = 6, float spacing = 6f)
    {
        Image bg = Panel(name, parent, background ?? new Color(1f, 1f, 1f, 0.025f), Radius);
        if (height > 0f) InventoryUIFactory.Size(bg, -1f, height);
        else InventoryUIFactory.Size(bg, -1f, 80f).flexibleHeight = 1f;
        scroll = bg.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        RectTransform viewport = InventoryUIFactory.Rect("Viewport", bg.transform);
        Place(viewport, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-14f, -4f));
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = InventoryUIFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        InventoryUIFactory.Vertical(content.gameObject, padding, spacing, true);

        // Scrollbar: thin, on the right.
        Image bar = InventoryUIFactory.Panel("Scrollbar", bg.transform, new Color(0f, 0f, 0f, 0f));
        Place(bar.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-10f, 6f), new Vector2(-4f, -6f));
        RectTransform sliding = Area("Sliding Area", bar.transform, Vector2.zero, Vector2.one);
        Image handle = Panel("Handle", sliding, new Color(1f, 1f, 1f, 0.18f), 3);
        InventoryUIFactory.Stretch(handle.rectTransform);
        var sb = bar.gameObject.AddComponent<Scrollbar>();
        sb.handleRect = handle.rectTransform;
        sb.targetGraphic = handle;
        sb.direction = Scrollbar.Direction.BottomToTop;
        ColorBlock cb = sb.colors;
        cb.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        sb.colors = cb;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = sb;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return content;
    }

    /// <summary>A round colour button (colour pickers).</summary>
    public static Button Swatch(Transform parent, Color color, UnityAction onClick, float size = 34f)
    {
        Image ring = InventoryUIFactory.Panel("Swatch", parent, new Color(1f, 1f, 1f, 0f));
        Round(ring, 17);
        InventoryUIFactory.Size(ring, size, size, 0f);
        Image fill = InventoryUIFactory.Panel("Colour", ring.transform, color);
        Round(fill, 14);
        Place(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        fill.raycastTarget = false;
        var b = ring.gameObject.AddComponent<Button>();
        b.targetGraphic = ring;
        ColorBlock c = b.colors;
        c.normalColor = new Color(1f, 1f, 1f, 0f);
        c.highlightedColor = new Color(1f, 1f, 1f, 0.35f);
        c.selectedColor = new Color(1f, 1f, 1f, 0.35f);
        c.pressedColor = Accent;
        b.colors = c;
        if (onClick != null) b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>Marks a swatch as the chosen colour (white ring).</summary>
    public static void SetSwatchSelected(Button swatch, bool selected)
    {
        if (swatch == null || !(swatch.targetGraphic is Image ring))
            return;
        ColorBlock c = swatch.colors;
        c.normalColor = selected ? Color.white : new Color(1f, 1f, 1f, 0f);
        swatch.colors = c;
        ring.color = Color.white;
    }
}

