#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Small helpers the scene UI builders share (character creation screen, pause and settings menu): canvas, event
/// system, labelled sliders / toggles / dropdowns, scroll lists, input fields and persistent button wiring, all
/// registered with Undo. Colours and texts come from <see cref="InventoryUIFactory"/> so every generated UI matches.
/// </summary>
public static class UIBuildKit
{
    public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.65f);
    public static readonly Color Section = new Color(1f, 1f, 1f, 0.04f);

    private static TMP_DefaultControls.Resources tmpResources;
    private static bool resourcesLoaded;

    private static TMP_DefaultControls.Resources Resources
    {
        get
        {
            if (!resourcesLoaded)
            {
                resourcesLoaded = true;
                tmpResources = new TMP_DefaultControls.Resources
                {
                    standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                    background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                    inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                    knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                    checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                    dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                    mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
                };
            }
            return tmpResources;
        }
    }

    /// <summary>A screen-space canvas scaled for 1920×1080, drawn above lower sorting orders.</summary>
    public static Canvas Canvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build " + name);
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

    /// <summary>The open scenes have an EventSystem (active or not).</summary>
    public static bool HasEventSystem() => Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length > 0;

    /// <summary>Adds an EventSystem with the Input System UI module when the scene has none.</summary>
    public static void EnsureEventSystem()
    {
        if (HasEventSystem())
            return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    /// <summary>A rect placed at an anchor box (0-1) of its parent with a margin in pixels.</summary>
    public static RectTransform Area(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float margin = 0f)
    {
        RectTransform rt = InventoryUIFactory.Rect(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
        return rt;
    }

    /// <summary>A centred window of a fixed size.</summary>
    public static Image Window(string name, Transform parent, Vector2 size)
    {
        Image img = InventoryUIFactory.Panel(name, parent, InventoryUIFactory.PanelColor);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return img;
    }

    public static TextMeshProUGUI Title(Transform parent, string text, float size = 30f)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text("Title", parent, text, size, InventoryUIFactory.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Size(t, -1f, size + 16f);
        return t;
    }

    public static TextMeshProUGUI Header(Transform parent, string text)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text(text, parent, text.ToUpperInvariant(), 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Left, FontStyles.Bold);
        InventoryUIFactory.Size(t, -1f, 24f);
        return t;
    }

    public static TextMeshProUGUI Body(string name, Transform parent, string text, float size = 17f)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text(name, parent, text, size, InventoryUIFactory.TextColor, TextAlignmentOptions.TopLeft);
        return t;
    }

    /// <summary>
    /// A text of any length in its own scroll area of <paramref name="height"/> pixels (0 = the remaining space), top
    /// aligned: long descriptions scroll instead of spilling over the controls around them.
    /// </summary>
    public static TextMeshProUGUI ScrollText(string name, Transform parent, float height, string text, float size, Color color)
    {
        RectTransform content = ScrollList(name, parent, height, out _);
        TextMeshProUGUI t = InventoryUIFactory.Text("Text", content, text, size, color, TextAlignmentOptions.TopLeft);
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>A button whose click calls <paramref name="call"/> (saved in the scene).</summary>
    public static Button Button(string name, Transform parent, string label, UnityAction call = null, float width = 0f, float height = 40f)
    {
        Button b = InventoryUIFactory.Button(name, parent, label, null, width);
        InventoryUIFactory.Size(b, width > 0f ? width : -1f, height);
        if (call != null)
            UnityEditor.Events.UnityEventTools.AddPersistentListener(b.onClick, call);
        return b;
    }

    /// <summary>A vertical stack with spacing (no fitter: it fills its parent).</summary>
    public static RectTransform Column(string name, Transform parent, int padding = 0, float spacing = 10f)
    {
        RectTransform rt = InventoryUIFactory.Rect(name, parent);
        InventoryUIFactory.Vertical(rt.gameObject, padding, spacing, false);
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

    /// <summary>"Label ........ [control] value" row; returns the row for the control to be added to.</summary>
    public static RectTransform LabeledRow(Transform parent, string label, float labelWidth = 240f, float height = 36f)
    {
        RectTransform row = Row(label, parent, height);
        TextMeshProUGUI t = InventoryUIFactory.Text("Label", row, label, 17f, InventoryUIFactory.TextColor);
        InventoryUIFactory.Size(t, labelWidth, height, 0f);
        return row;
    }

    /// <summary>A labelled slider with a value text on its right.</summary>
    public static Slider Slider(Transform parent, string label, float min, float max, float value, bool whole, out TextMeshProUGUI valueText)
    {
        RectTransform row = LabeledRow(parent, label);
        Slider s = InventoryUIFactory.Slider("Slider", row);
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = whole;
        s.value = value;
        valueText = InventoryUIFactory.Text("Value", row, "", 16f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(valueText, 80f, 36f, 0f);
        return s;
    }

    /// <summary>A labelled checkbox.</summary>
    public static Toggle Toggle(Transform parent, string label, bool value)
    {
        RectTransform row = LabeledRow(parent, label);
        Image box = InventoryUIFactory.Panel("Toggle", row, InventoryUIFactory.FieldColor);
        InventoryUIFactory.Size(box, 28f, 28f, 0f);
        Image check = InventoryUIFactory.Panel("Checkmark", box.transform, InventoryUIFactory.AccentColor);
        InventoryUIFactory.Stretch(check.rectTransform, 5f, 5f);
        var toggle = box.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.isOn = value;
        RectTransform spacer = InventoryUIFactory.Rect("Spacer", row);
        InventoryUIFactory.Size(spacer, -1f, -1f, 1f);
        return toggle;
    }

    /// <summary>A labelled TextMeshPro dropdown (options are filled at runtime).</summary>
    public static TMP_Dropdown Dropdown(Transform parent, string label)
    {
        RectTransform row = LabeledRow(parent, label);
        GameObject go = TMP_DefaultControls.CreateDropdown(Resources);
        go.name = "Dropdown";
        go.transform.SetParent(row, false);
        InventoryUIFactory.Size((RectTransform)go.transform, -1f, 34f, 1f);
        return go.GetComponent<TMP_Dropdown>();
    }

    /// <summary>A TextMeshPro input field.</summary>
    public static TMP_InputField InputField(string name, Transform parent, string placeholder, int characterLimit = 0)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(Resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        InventoryUIFactory.Size((RectTransform)go.transform, -1f, 42f, 1f);
        var field = go.GetComponent<TMP_InputField>();
        field.characterLimit = characterLimit;
        if (field.placeholder is TMP_Text ph)
            ph.text = placeholder;
        return field;
    }

    /// <summary>
    /// A scrolling vertical list; returns the content transform (items are laid out top to bottom). The list takes
    /// <paramref name="height"/> pixels, or the remaining space when 0.
    /// </summary>
    public static RectTransform ScrollList(string name, Transform parent, float height, out ScrollRect scroll)
    {
        Image bg = InventoryUIFactory.Panel(name, parent, Section);
        if (height > 0f) InventoryUIFactory.Size(bg, -1f, height);
        else InventoryUIFactory.Size(bg, -1f, 120f).flexibleHeight = 1f;
        scroll = bg.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        RectTransform viewport = InventoryUIFactory.Rect("Viewport", bg.transform);
        InventoryUIFactory.Stretch(viewport, 4f, 4f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = InventoryUIFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        InventoryUIFactory.Vertical(content.gameObject, 4, 6f, true);

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    /// <summary>A list-item template (button with a left-aligned label), kept inactive.</summary>
    public static GameObject ItemTemplate(string name, Transform parent, string label, float height = 40f)
    {
        Button b = InventoryUIFactory.Button(name, parent, label, null);
        InventoryUIFactory.Size(b, -1f, height);
        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null)
        {
            t.alignment = TextAlignmentOptions.Left;
            InventoryUIFactory.Stretch(t.rectTransform, 12f, 0f);
        }
        b.gameObject.SetActive(false);
        return b.gameObject;
    }

    /// <summary>Assigns serialized fields by name (missing names are reported).</summary>
    public static void Assign(Object target, params (string field, Object value)[] values)
    {
        var so = new SerializedObject(target);
        foreach ((string field, Object value) in values)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[UI Builder] {target.GetType().Name} has no field '{field}'.", target);
                continue;
            }
            p.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
