using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the default shop window and its parts from code, using a <see cref="MerchantUISkin"/> for prefabs, sprites,
/// font, colours and sizes - anything the skin leaves empty gets the default look. Every generated part is wired to the
/// window, so a shop works without building or connecting any UI in the editor. Uses <see cref="InventoryUIFactory"/>,
/// so it matches the inventory's generated panels.
/// </summary>
public static class MerchantUIBuilder
{
    // ------------------------------------------------------------------ the window
    /// <summary>The complete window under <paramref name="parent"/> (a canvas), hidden.</summary>
    public static MerchantWindow BuildWindow(Transform parent, MerchantUISkin skin)
    {
        skin = skin != null ? skin : MerchantUISkin.Default;
        Image root = Background("MerchantWindow", parent, skin.windowColor, skin.windowBackground, skin);
        RectTransform rt = root.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = skin.windowSize;
        VerticalLayoutGroup column = InventoryUIFactory.Vertical(root.gameObject, 16, 8f, false);
        column.childForceExpandHeight = false;
        MerchantWindow window = root.gameObject.AddComponent<MerchantWindow>();

        // Header: portrait, shop name and greeting, wallet, close.
        RectTransform header = Row("Header", rt, 64f, 12f);
        Image frame = InventoryUIFactory.Panel("PortraitFrame", header, InventoryUIFactory.FieldColor);
        frame.raycastTarget = false;
        InventoryUIFactory.Size(frame, 64f, 64f, 0f);
        Image portrait = InventoryUIFactory.Panel("Portrait", frame.transform, Color.white);
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        InventoryUIFactory.Stretch(portrait.rectTransform, 3f, 3f);
        RectTransform titles = InventoryUIFactory.Rect("Titles", header);
        InventoryUIFactory.Vertical(titles.gameObject, 0, 2f, false).childAlignment = TextAnchor.MiddleLeft;
        InventoryUIFactory.Size(titles, -1f, -1f, 1f);
        TextMeshProUGUI shopName = Text("ShopName", titles, "Shop", 26f, skin.textColor, skin, TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI greeting = Text("Greeting", titles, "", 15f, skin.mutedTextColor, skin, TextAlignmentOptions.Left, FontStyles.Italic);
        RectTransform wallet = InventoryUIFactory.Rect("Wallet", header);
        HorizontalLayoutGroup wh = InventoryUIFactory.Horizontal(wallet.gameObject, 8f);
        wh.childAlignment = TextAnchor.MiddleRight;
        InventoryUIFactory.Size(wallet, 260f, 64f, 0f);
        Image walletIcon = InventoryUIFactory.Panel("Icon", wallet, Color.white);
        walletIcon.preserveAspect = true;
        walletIcon.raycastTarget = false;
        InventoryUIFactory.Size(walletIcon, 30f, 30f, 0f);
        TextMeshProUGUI walletText = Text("Amount", wallet, "0", 24f, skin.priceColor, skin, TextAlignmentOptions.Right, FontStyles.Bold);
        walletText.textWrappingMode = TextWrappingModes.NoWrap;
        Button close = Button("Close", header, "✕", skin, 44f, null);

        // Buy / Sell, search, sort, filters.
        RectTransform tools = Row("Tools", rt, 38f, 8f);
        MerchantTabButton buy = CreateTab(tools, skin, "Buy", true);
        MerchantTabButton sell = CreateTab(tools, skin, "Sell", true);
        RectTransform spacer = InventoryUIFactory.Rect("Spacer", tools);
        InventoryUIFactory.Size(spacer, -1f, -1f, 1f);
        TMP_InputField search = SearchField(tools, skin, 260f);
        Button sort = Button("Sort", tools, "Sort: Default", skin, 170f, null);
        TextMeshProUGUI sortLabel = sort.GetComponentInChildren<TextMeshProUGUI>(true);
        MerchantTabButton affordable = CreateTab(tools, skin, "Affordable", false);
        MerchantTabButton inStock = CreateTab(tools, skin, "In stock", false);

        // Category and subcategory tabs (scroll sideways when they do not fit).
        RectTransform categories = TabRow("Categories", rt, 36f, skin, out GameObject _);
        RectTransform subcategories = TabRow("Subcategories", rt, 30f, skin, out GameObject subRow);

        // Items and details.
        RectTransform body = InventoryUIFactory.Rect("Body", rt);
        InventoryUIFactory.Horizontal(body.gameObject, 12f).childForceExpandHeight = true;
        LayoutElement bodyLayout = InventoryUIFactory.Size(body, -1f, 120f);
        bodyLayout.flexibleHeight = 1f;
        ScrollRect itemsScroll = Scroll("Items", body, skin, out RectTransform itemsContent);
        InventoryUIFactory.Size(itemsScroll, -1f, -1f, 1f);
        TextMeshProUGUI empty = Text("Empty", itemsScroll.transform, "", 17f, skin.mutedTextColor, skin, TextAlignmentOptions.Top, FontStyles.Italic);
        RectTransform emptyRt = empty.rectTransform;
        emptyRt.anchorMin = new Vector2(0f, 1f);
        emptyRt.anchorMax = new Vector2(1f, 1f);
        emptyRt.pivot = new Vector2(0.5f, 1f);
        emptyRt.sizeDelta = new Vector2(-40f, 60f);
        emptyRt.anchoredPosition = new Vector2(0f, -40f);
        MerchantDetailsPanel details = BuildDetails(body, skin);

        // Status line and confirmation.
        TextMeshProUGUI status = Text("Status", rt, "", 15f, skin.successColor, skin, TextAlignmentOptions.Left);
        InventoryUIFactory.Size(status, -1f, 22f);
        MerchantConfirmDialog confirm = BuildConfirm(rt, skin);

        window.Configure(portrait, frame.gameObject, shopName, greeting, close, walletText, walletIcon, buy, sell, categories, subcategories,
            subRow, search, sort, sortLabel, affordable, inStock, itemsScroll, itemsContent, empty, details, status, confirm);
        root.gameObject.SetActive(false);
        return window;
    }

    // ------------------------------------------------------------------ parts
    /// <summary>An item entry (slot, or a block in the grid layout).</summary>
    public static MerchantItemEntryUI CreateEntry(Transform parent, MerchantUISkin skin, bool grid)
    {
        skin = skin != null ? skin : MerchantUISkin.Default;
        Image bg = InventoryUIFactory.Panel(grid ? "GridItem" : "Slot", parent, grid ? skin.gridItemColor : skin.slotColor);
        if (!grid && skin.slotBackground != null)
        {
            bg.sprite = skin.slotBackground;
            bg.type = skin.backgroundImageType;
        }
        var entry = bg.gameObject.AddComponent<MerchantItemEntryUI>();
        var group = bg.gameObject.AddComponent<CanvasGroup>();

        Image icon = InventoryUIFactory.Panel("Icon", bg.transform, Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        InventoryUIFactory.Stretch(icon.rectTransform, 6f, 6f);

        GameObject hover = Frame("Hover", bg.transform, new Color(1f, 1f, 1f, 0.35f), null, 1f);
        GameObject selected = Frame("Selected", bg.transform, skin.selectedColor, skin.selectedFrame, 2f);

        TextMeshProUGUI quantity = Text("Quantity", bg.transform, "", 12f, skin.textColor, skin, TextAlignmentOptions.TopRight, FontStyles.Bold);
        InventoryUIFactory.Stretch(quantity.rectTransform, 4f, 2f);
        quantity.textWrappingMode = TextWrappingModes.NoWrap;

        Image priceBack = InventoryUIFactory.Panel("PriceBack", bg.transform, new Color(0f, 0f, 0f, 0.45f));
        priceBack.raycastTarget = false;
        RectTransform pb = priceBack.rectTransform;
        pb.anchorMin = new Vector2(0f, 0f);
        pb.anchorMax = new Vector2(1f, 0f);
        pb.pivot = new Vector2(0.5f, 0f);
        pb.sizeDelta = new Vector2(0f, 17f);
        pb.anchoredPosition = Vector2.zero;
        TextMeshProUGUI price = Text("Price", bg.transform, "", 12f, skin.priceColor, skin, grid ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.Bottom, FontStyles.Bold);
        RectTransform pr = price.rectTransform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(-8f, 17f);
        pr.anchoredPosition = new Vector2(0f, 0f);
        price.textWrappingMode = TextWrappingModes.NoWrap;
        price.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI badge = Text("Badge", bg.transform, "", 11f, skin.unaffordableColor, skin, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Stretch(badge.rectTransform, 2f, 0f);
        badge.gameObject.SetActive(false);

        entry.Configure(bg, icon, price, priceBack.gameObject, quantity, badge, selected, hover, group);
        return entry;
    }

    /// <summary>A tab (category, subcategory, Buy / Sell, filter toggle). <paramref name="big"/> = a fixed-width main tab.</summary>
    public static MerchantTabButton CreateTab(Transform parent, MerchantUISkin skin, string label, bool big)
    {
        skin = skin != null ? skin : MerchantUISkin.Default;
        if (skin.tabPrefab != null)
        {
            MerchantTabButton p = Object.Instantiate(skin.tabPrefab, parent, false);
            p.SetText(label);
            return p;
        }
        Image bg = Background("Tab", parent, skin.tabColor, skin.tabBackground, skin);
        var button = bg.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colors;
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 6f);
        h.padding = new RectOffset(12, 12, 3, 3);
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childForceExpandHeight = false;
        Image icon = InventoryUIFactory.Panel("Icon", bg.transform, Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        InventoryUIFactory.Size(icon, 20f, 20f, 0f);
        icon.gameObject.SetActive(false);
        TextMeshProUGUI text = Text("Label", bg.transform, label, big ? 18f : 15f, skin.textColor, skin, TextAlignmentOptions.Center, big ? FontStyles.Bold : FontStyles.Normal);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.richText = true;
        if (big)
            InventoryUIFactory.Size(bg, 120f, -1f, 0f);
        var tab = bg.gameObject.AddComponent<MerchantTabButton>();
        tab.Configure(button, text, icon, bg);
        tab.SetSelected(false, skin);
        return tab;
    }

    /// <summary>A button: the skin's button prefab, else a generated one.</summary>
    public static Button Button(string name, Transform parent, string label, MerchantUISkin skin, float width, Color? color, UnityAction onClick = null)
    {
        skin = skin != null ? skin : MerchantUISkin.Default;
        Button b;
        if (skin.buttonPrefab != null)
        {
            b = Object.Instantiate(skin.buttonPrefab, parent, false);
            b.name = name;
            TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null) t.text = label;
            if (onClick != null) b.onClick.AddListener(onClick);
            InventoryUIFactory.Size(b, width > 0f ? width : -1f, 34f, width > 0f ? 0f : 1f);
            return b;
        }
        b = InventoryUIFactory.Button(name, parent, label, onClick, width);
        Image img = b.GetComponent<Image>();
        if (img != null)
        {
            img.color = color ?? skin.buttonColor;
            if (skin.buttonBackground != null)
            {
                img.sprite = skin.buttonBackground;
                img.type = skin.backgroundImageType;
            }
        }
        TextMeshProUGUI label2 = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label2 != null)
            ApplyFont(label2, skin, 16f);
        return b;
    }

    /// <summary>A text with the skin's font and scale.</summary>
    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, MerchantUISkin skin,
        TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text(name, parent, text, size, color, align, style);
        ApplyFont(t, skin, size);
        return t;
    }

    private static void ApplyFont(TextMeshProUGUI t, MerchantUISkin skin, float size)
    {
        if (skin == null || t == null)
            return;
        if (skin.font != null)
            t.font = skin.font;
        t.fontSize = skin.FontSize(size);
    }

    // ------------------------------------------------------------------ building blocks
    private static Image Background(string name, Transform parent, Color color, Sprite sprite, MerchantUISkin skin)
    {
        Image img = InventoryUIFactory.Panel(name, parent, color);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = skin.backgroundImageType;
        }
        return img;
    }

    private static RectTransform Row(string name, Transform parent, float height, float spacing)
    {
        RectTransform row = InventoryUIFactory.Rect(name, parent);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(row.gameObject, spacing);
        h.childAlignment = TextAnchor.MiddleLeft;
        InventoryUIFactory.Size(row, -1f, height);
        return row;
    }

    /// <summary>A frame of four thin bars (or a frame sprite) over its parent.</summary>
    private static GameObject Frame(string name, Transform parent, Color color, Sprite sprite, float thickness)
    {
        RectTransform frame = InventoryUIFactory.Rect(name, parent);
        InventoryUIFactory.Stretch(frame);
        if (sprite != null)
        {
            Image img = frame.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
        }
        else
        {
            Bar(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero, color);
            Bar(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), color);
            Bar(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), color);
            Bar(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero, color);
        }
        frame.gameObject.SetActive(false);
        return frame.gameObject;
    }

    private static void Bar(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        Image bar = InventoryUIFactory.Panel("Bar", parent, color);
        bar.raycastTarget = false;
        RectTransform r = bar.rectTransform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = offsetMin;
        r.offsetMax = offsetMax;
    }

    /// <summary>A vertical scroll view with a thin scrollbar; <paramref name="content"/> is anchored to its top.</summary>
    private static ScrollRect Scroll(string name, Transform parent, MerchantUISkin skin, out RectTransform content)
    {
        Image bg = Background(name, parent, skin.panelColor, skin.panelBackground, skin);
        var scroll = bg.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = InventoryUIFactory.Rect("Viewport", bg.transform);
        InventoryUIFactory.Stretch(viewport);
        viewport.offsetMax = new Vector2(-12f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = InventoryUIFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 100f);

        Image track = InventoryUIFactory.Panel("Scrollbar", bg.transform, new Color(1f, 1f, 1f, 0.04f));
        RectTransform tr = track.rectTransform;
        tr.anchorMin = new Vector2(1f, 0f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(1f, 0.5f);
        tr.sizeDelta = new Vector2(8f, -8f);
        tr.anchoredPosition = new Vector2(-2f, 0f);
        RectTransform area = InventoryUIFactory.Rect("Sliding Area", track.transform);
        InventoryUIFactory.Stretch(area);
        Image handle = InventoryUIFactory.Panel("Handle", area, new Color(1f, 1f, 1f, 0.3f));
        InventoryUIFactory.Stretch(handle.rectTransform);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.direction = Scrollbar.Direction.BottomToTop;

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    /// <summary>A row of tabs that scrolls sideways when they do not fit. Returns the tabs' parent.</summary>
    private static RectTransform TabRow(string name, Transform parent, float height, MerchantUISkin skin, out GameObject row)
    {
        RectTransform holder = InventoryUIFactory.Rect(name, parent);
        InventoryUIFactory.Size(holder, -1f, height);
        row = holder.gameObject;
        var img = holder.gameObject.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.001f); // catches the scroll wheel
        var scroll = holder.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = InventoryUIFactory.Rect("Viewport", holder);
        InventoryUIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = InventoryUIFactory.Rect("Tabs", viewport);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.sizeDelta = Vector2.zero;
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(content.gameObject, 6f);
        h.childAlignment = TextAnchor.MiddleLeft;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25f;
        return content;
    }

    private static TMP_InputField SearchField(Transform parent, MerchantUISkin skin, float width)
    {
        Image bg = Background("Search", parent, InventoryUIFactory.FieldColor, skin.panelBackground, skin);
        InventoryUIFactory.Size(bg, width, 34f, 0f);
        RectTransform area = InventoryUIFactory.Rect("Text Area", bg.transform);
        InventoryUIFactory.Stretch(area, 10f, 4f);
        area.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = Text("Placeholder", area, "Search…", 16f, skin.mutedTextColor, skin, TextAlignmentOptions.Left, FontStyles.Italic);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        InventoryUIFactory.Stretch(placeholder.rectTransform);
        TextMeshProUGUI text = Text("Text", area, "", 16f, skin.textColor, skin, TextAlignmentOptions.Left);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        InventoryUIFactory.Stretch(text.rectTransform);
        var input = bg.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.targetGraphic = bg;
        input.characterLimit = 40;
        return input;
    }

    private static MerchantDetailsPanel BuildDetails(Transform parent, MerchantUISkin skin)
    {
        Image bg = Background("Details", parent, skin.panelColor, skin.panelBackground, skin);
        InventoryUIFactory.Size(bg, skin.detailsWidth, -1f, 0f);
        var panel = bg.gameObject.AddComponent<MerchantDetailsPanel>();

        // Nothing selected.
        RectTransform empty = InventoryUIFactory.Rect("Empty", bg.transform);
        InventoryUIFactory.Stretch(empty, 20f, 20f);
        TextMeshProUGUI emptyText = Text("Text", empty, "", 16f, skin.mutedTextColor, skin, TextAlignmentOptions.Center, FontStyles.Italic);
        InventoryUIFactory.Stretch(emptyText.rectTransform);

        // The selected item.
        RectTransform content = InventoryUIFactory.Rect("Content", bg.transform);
        InventoryUIFactory.Stretch(content);
        VerticalLayoutGroup v = InventoryUIFactory.Vertical(content.gameObject, 14, 8f, false);
        v.childForceExpandHeight = false;

        RectTransform head = Row("Head", content, 64f, 10f);
        Image frame = InventoryUIFactory.Panel("IconFrame", head, InventoryUIFactory.FieldColor);
        frame.raycastTarget = false;
        InventoryUIFactory.Size(frame, 64f, 64f, 0f);
        Image icon = InventoryUIFactory.Panel("Icon", frame.transform, Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        InventoryUIFactory.Stretch(icon.rectTransform, 5f, 5f);
        RectTransform names = InventoryUIFactory.Rect("Names", head);
        InventoryUIFactory.Vertical(names.gameObject, 0, 2f, false).childAlignment = TextAnchor.MiddleLeft;
        InventoryUIFactory.Size(names, -1f, -1f, 1f);
        TextMeshProUGUI itemName = Text("Name", names, "", 20f, skin.textColor, skin, TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI category = Text("Category", names, "", 14f, skin.accentColor, skin);

        // Description and what the item does (scrolls when long).
        ScrollRect infoScroll = Scroll("Info", content, skin, out RectTransform infoContent);
        infoScroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        LayoutElement infoLayout = InventoryUIFactory.Size(infoScroll, -1f, 60f);
        infoLayout.flexibleHeight = 1f;
        InventoryUIFactory.Vertical(infoContent.gameObject, 2, 6f, true);
        TextMeshProUGUI description = Text("Description", infoContent, "", 14f, skin.mutedTextColor, skin, TextAlignmentOptions.Left, FontStyles.Italic);
        TextMeshProUGUI stats = Text("Stats", infoContent, "", 14f, new Color(0.75f, 0.9f, 1f), skin);
        TextMeshProUGUI info = Text("Info", content, "", 15f, skin.textColor, skin);

        RectTransform qty = Row("Quantity", content, 34f, 6f);
        Button minus = Button("Minus", qty, "−", skin, 34f, null);
        TMP_InputField field = InventoryUIFactory.NumberField("Field", qty, 70f);
        TextMeshProUGUI fieldText = field.textComponent as TextMeshProUGUI;
        if (fieldText != null) ApplyFont(fieldText, skin, 18f);
        Button plus = Button("Plus", qty, "+", skin, 34f, null);
        Button max = Button("Max", qty, "Max", skin, 64f, null);

        TextMeshProUGUI total = Text("Total", content, "", 20f, skin.priceColor, skin, TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI reason = Text("Reason", content, "", 14f, skin.errorColor, skin);
        Button action = Button("Action", content, "Buy", skin, -1f, skin.buyButtonColor);
        InventoryUIFactory.Size(action, -1f, 44f);
        TextMeshProUGUI actionLabel = action.GetComponentInChildren<TextMeshProUGUI>(true);
        if (actionLabel != null)
        {
            actionLabel.fontStyle = FontStyles.Bold;
            actionLabel.fontSize = skin.FontSize(19f);
        }

        panel.Configure(content.gameObject, empty.gameObject, emptyText, icon, itemName, category, description, stats, info, qty.gameObject,
            minus, plus, max, field, total, reason, action, actionLabel, action.GetComponent<Image>());
        content.gameObject.SetActive(false);
        return panel;
    }

    private static MerchantConfirmDialog BuildConfirm(Transform window, MerchantUISkin skin)
    {
        Image overlay = InventoryUIFactory.Panel("Confirm", window, new Color(0f, 0f, 0f, 0.6f));
        InventoryUIFactory.Stretch(overlay.rectTransform);
        overlay.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var dialog = overlay.gameObject.AddComponent<MerchantConfirmDialog>();

        Image box = Background("Box", overlay.transform, skin.windowColor, skin.windowBackground, skin);
        RectTransform b = box.rectTransform;
        b.anchorMin = b.anchorMax = b.pivot = new Vector2(0.5f, 0.5f);
        b.sizeDelta = new Vector2(440f, 0f);
        InventoryUIFactory.Vertical(box.gameObject, 18, 14f, true);
        TextMeshProUGUI message = Text("Message", box.transform, "", 19f, skin.textColor, skin, TextAlignmentOptions.Center);
        RectTransform buttons = Row("Buttons", box.transform, 40f, 10f);
        Button cancel = Button("Cancel", buttons, "Cancel", skin, -1f, null);
        Button confirm = Button("Confirm", buttons, "Confirm", skin, -1f, skin.buyButtonColor);
        dialog.Configure(message, confirm, confirm.GetComponentInChildren<TextMeshProUGUI>(true), cancel);
        overlay.gameObject.SetActive(false);
        return dialog;
    }
}
