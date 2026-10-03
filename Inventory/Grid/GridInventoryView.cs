using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the grid inventory: lays the cells out (columns × rows, cell size, spacing), shows each item over the cells it
/// covers (sized W × H, turned when rotated, with a backdrop), and gives feedback: the hovered item, the dragged item's
/// origin, and a placement preview (green fits, yellow stacks / swaps, red does not fit). Created and configured by the
/// <see cref="InventoryManager"/> on the inventory slots' parent; it only draws - every decision is the grid's.
/// </summary>
[DisallowMultipleComponent]
public class GridInventoryView : MonoBehaviour
{
    private struct IconLayout
    {
        public Vector2 anchorMin, anchorMax, offsetMin, offsetMax, pivot;
        public Quaternion rotation;
        public bool preserveAspect;
        public Sprite sprite;
    }

    private InventoryManager manager;
    private GridInventory grid;
    private GridInventorySettings settings;
    private RectTransform area;
    private RectTransform itemLayer;
    private RectTransform previewLayer;
    private GridLayoutGroup layoutGroup;
    private bool layoutGroupWasEnabled;
    private Image preview, origin, hover;
    private TMPro.TextMeshProUGUI hint;
    private float cell = 56f;
    private Vector2 lastAreaSize;
    private float flashUntil;
    private readonly Dictionary<RectTransform, IconLayout> iconDefaults = new Dictionary<RectTransform, IconLayout>(ReferenceComparer<RectTransform>.Instance);
    private readonly HashSet<InventoryItem> styled = new HashSet<InventoryItem>(ReferenceComparer<InventoryItem>.Instance);
    private readonly HashSet<InventoryItem> pendingRestore = new HashSet<InventoryItem>(ReferenceComparer<InventoryItem>.Instance);
    private readonly List<InventoryItem> toRestore = new List<InventoryItem>();
    private bool active;

    public float CellSize => cell;
    public float Step => cell + settings.spacing;
    public RectTransform Area => area;
    public bool IsActive => active;

    /// <summary>Puts the view on the inventory slots' parent (<paramref name="slotsParent"/>) and lays the grid out.</summary>
    public static GridInventoryView Attach(RectTransform slotsParent, InventoryManager manager, GridInventory grid)
    {
        GridInventoryView v = slotsParent.GetComponent<GridInventoryView>();
        if (v == null)
            v = slotsParent.gameObject.AddComponent<GridInventoryView>();
        v.Initialize(manager, grid);
        return v;
    }

    public void Initialize(InventoryManager owner, GridInventory g)
    {
        manager = owner;
        grid = g;
        settings = g.Settings;
        area = (RectTransform)transform;
        layoutGroup = GetComponent<GridLayoutGroup>();
        EnsureLayers();
        Activate();
    }

    public void Activate()
    {
        if (layoutGroup != null && !active)
        {
            layoutGroupWasEnabled = layoutGroup.enabled;
            layoutGroup.enabled = false; // the grid places its cells itself
        }
        active = true;
        if (itemLayer != null) itemLayer.gameObject.SetActive(true);
        if (previewLayer != null) previewLayer.gameObject.SetActive(true);
        LayoutCells();
    }

    /// <summary>Shows <paramref name="text"/> in a small line just above the grid (empty = hidden).</summary>
    public void SetHint(string text)
    {
        if (previewLayer == null)
            return;
        if (hint == null)
        {
            hint = InventoryUIFactory.Text("ControlsHint", previewLayer, "", 13f, InventoryUIFactory.MutedColor, TMPro.TextAlignmentOptions.BottomLeft);
            hint.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            hint.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            RectTransform rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 0f);
            rt.offsetMin = new Vector2(settings.padding, 0f);
            rt.offsetMax = new Vector2(-settings.padding, 18f);
            rt.anchoredPosition = new Vector2(settings.padding, 1f);
        }
        hint.text = text ?? "";
        hint.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>Gives the slots back to the normal layout (switching back to slot mode).</summary>
    public void Deactivate()
    {
        if (!active)
            return;
        active = false;
        RestoreAll();
        if (layoutGroup != null)
            layoutGroup.enabled = true; // slot mode lays the slots out with it
        if (itemLayer != null) itemLayer.gameObject.SetActive(false);
        if (previewLayer != null) previewLayer.gameObject.SetActive(false);
        HideAll();
    }

    private void EnsureLayers()
    {
        Transform parent = area.parent;
        itemLayer = FindOrCreateLayer(parent, "GridItemLayer");
        previewLayer = FindOrCreateLayer(parent, "GridPreviewLayer");
        itemLayer.SetSiblingIndex(area.GetSiblingIndex() + 1);
        previewLayer.SetSiblingIndex(itemLayer.GetSiblingIndex() + 1);
        if (preview == null) preview = MakeFrame("Preview");
        if (origin == null) origin = MakeFrame("Origin");
        if (hover == null) hover = MakeFrame("Hover");
        HideAll();
    }

    private RectTransform FindOrCreateLayer(Transform parent, string layerName)
    {
        Transform t = parent != null ? parent.Find(layerName) : null;
        RectTransform rt = t as RectTransform;
        if (rt == null)
        {
            rt = InventoryUIFactory.Rect(layerName, parent != null ? parent : area);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
        // Exactly over the cells' area.
        rt.anchorMin = area.anchorMin;
        rt.anchorMax = area.anchorMax;
        rt.pivot = area.pivot;
        rt.offsetMin = area.offsetMin;
        rt.offsetMax = area.offsetMax;
        rt.anchoredPosition = area.anchoredPosition;
        rt.sizeDelta = area.sizeDelta;
        return rt;
    }

    private Image MakeFrame(string frameName)
    {
        Image img = InventoryUIFactory.Panel(frameName, previewLayer, Color.clear);
        img.raycastTarget = false;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        return img;
    }

    private void HideAll()
    {
        if (preview != null) preview.enabled = false;
        if (origin != null) origin.enabled = false;
        if (hover != null) hover.enabled = false;
    }

    // ------------------------------------------------------------------ layout
    /// <summary>Computes the cell size and places every cell (and the panel when it resizes to fit).</summary>
    public void LayoutCells()
    {
        if (grid == null || area == null)
            return;
        float pad = settings.padding, sp = settings.spacing;
        int cols = grid.Columns, rows = grid.Rows;
        if (settings.cellSize > 0f)
        {
            cell = settings.cellSize;
            if (settings.resizePanelToFit)
                FitPanel(cols * cell + (cols - 1) * sp + pad * 2f, rows * cell + (rows - 1) * sp + pad * 2f);
        }
        else
        {
            Vector2 size = area.rect.size;
            float cw = (size.x - pad * 2f - (cols - 1) * sp) / cols;
            float ch = (size.y - pad * 2f - (rows - 1) * sp) / rows;
            cell = Mathf.Max(8f, Mathf.Floor(Mathf.Min(cw, ch)));
        }
        lastAreaSize = area.rect.size;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                InventorySlot s = grid.CellAt(x, y);
                if (s == null) continue;
                var rt = (RectTransform)s.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cell, cell);
                rt.anchoredPosition = CellPosition(x, y);
                rt.localScale = Vector3.one;
                Image img = s.GetComponent<Image>();
                if (img != null)
                    img.color = settings.cellColor;
            }
        // The layers follow the area exactly.
        CopyRect(itemLayer);
        CopyRect(previewLayer);
    }

    private void CopyRect(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = area.anchorMin;
        rt.anchorMax = area.anchorMax;
        rt.pivot = area.pivot;
        rt.offsetMin = area.offsetMin;
        rt.offsetMax = area.offsetMax;
    }

    /// <summary>Grows / shrinks the inventory panel so the area holds exactly <paramref name="w"/> × <paramref name="h"/> pixels.</summary>
    private void FitPanel(float w, float h)
    {
        var panel = area.parent as RectTransform;
        if (panel == null)
            return;
        Vector2 have = area.rect.size;
        if (Mathf.Abs(have.x - w) < 0.5f && Mathf.Abs(have.y - h) < 0.5f)
            return;
        bool stretchedX = !Mathf.Approximately(area.anchorMin.x, area.anchorMax.x);
        bool stretchedY = !Mathf.Approximately(area.anchorMin.y, area.anchorMax.y);
        Vector2 delta = new Vector2(w - have.x, h - have.y);
        if (stretchedX || stretchedY)
        {
            // The area stretches inside the panel: the panel grows by the difference.
            panel.sizeDelta += new Vector2(stretchedX ? delta.x : 0f, stretchedY ? delta.y : 0f);
            if (!stretchedX) area.sizeDelta = new Vector2(w, area.sizeDelta.y);
            if (!stretchedY) area.sizeDelta = new Vector2(area.sizeDelta.x, h);
        }
        else
        {
            area.sizeDelta = new Vector2(w, h);
        }
    }

    /// <summary>Top-left position of a cell in the area (anchored at its top-left corner).</summary>
    public Vector2 CellPosition(int x, int y)
    {
        float step = cell + settings.spacing;
        return new Vector2(settings.padding + x * step, -(settings.padding + y * step));
    }

    /// <summary>Pixel size of a W × H block of cells.</summary>
    public Vector2 BlockSize(int w, int h) => new Vector2(w * cell + (w - 1) * settings.spacing, h * cell + (h - 1) * settings.spacing);

    /// <summary>The cell under a screen point (may be outside the grid: x / y below 0 or past the edges). False when not over the area.</summary>
    public bool CellAtScreen(Vector2 screen, out int x, out int y)
    {
        x = y = -1;
        if (area == null)
            return false;
        Canvas canvas = area.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, cam, out Vector2 local))
            return false;
        Rect r = area.rect;
        float fromLeft = local.x - r.xMin - settings.padding;
        float fromTop = r.yMax - local.y - settings.padding;
        float step = cell + settings.spacing;
        x = Mathf.FloorToInt(fromLeft / step);
        y = Mathf.FloorToInt(fromTop / step);
        return r.Contains(local);
    }

    // ------------------------------------------------------------------ items
    private void LateUpdate()
    {
        if (!active || grid == null || area == null)
            return;
        if (settings.cellSize <= 0f && (area.rect.size - lastAreaSize).sqrMagnitude > 1f)
            LayoutCells();
        LayoutItems();
        if (flashUntil > 0f && Time.unscaledTime > flashUntil)
        {
            flashUntil = 0f;
            if (!manager.IsDraggingGridItem)
                preview.enabled = false;
        }
    }

    /// <summary>Puts every item of the grid over its cells (and gives back the look of items that left the grid).</summary>
    public void LayoutItems()
    {
        GridInventoryModel<InventoryItem> m = grid.Model;
        toRestore.Clear();
        foreach (InventoryItem it in styled)
            if (it == null || !m.Contains(it))
                toRestore.Add(it);
        foreach (InventoryItem it in toRestore)
        {
            styled.Remove(it);
            if (it == null)
                continue;
            if (manager.IsBeingDragged(it))
                pendingRestore.Add(it); // keeps its grid look while dragged; decided when it lands
            else
                Restore(it);
        }
        if (pendingRestore.Count > 0)
        {
            toRestore.Clear();
            foreach (InventoryItem it in pendingRestore)
                if (it == null || !manager.IsBeingDragged(it))
                    toRestore.Add(it);
            foreach (InventoryItem it in toRestore)
            {
                pendingRestore.Remove(it);
                if (it != null && !m.Contains(it))
                    Restore(it); // landed outside the grid (hotbar, equipment, storage)
            }
        }
        foreach (KeyValuePair<InventoryItem, GridRect> kv in m.Placements)
            PlaceItem(kv.Key, kv.Value);
    }

    private void PlaceItem(InventoryItem it, GridRect r)
    {
        if (it == null)
            return;
        var rt = (RectTransform)it.transform;
        if (rt.parent != itemLayer)
            rt.SetParent(itemLayer, false);
        Vector2 pos = CellPosition(r.X, r.Y);
        Vector2 size = BlockSize(r.W, r.H);
        if (rt.anchorMin != new Vector2(0f, 1f) || rt.anchorMax != new Vector2(0f, 1f) || rt.pivot != new Vector2(0f, 1f) ||
            rt.anchoredPosition != pos || rt.sizeDelta != size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }
        Style(it, it.gridRotated, size);
        styled.Add(it);
    }

    /// <summary>The look of an item in the grid: a backdrop over its cells, the icon turned when it is rotated.</summary>
    public void Style(InventoryItem it, bool rotated, Vector2 blockSize)
    {
        Image backdrop = Backdrop(it, true);
        if (backdrop != null)
        {
            backdrop.enabled = true;
            backdrop.color = settings.itemBackdropColor;
        }
        Image icon = it.IconImage;
        if (icon == null)
            return;
        RectTransform irt = icon.rectTransform;
        if (!iconDefaults.ContainsKey(irt))
            iconDefaults[irt] = new IconLayout
            {
                anchorMin = irt.anchorMin,
                anchorMax = irt.anchorMax,
                offsetMin = irt.offsetMin,
                offsetMax = irt.offsetMax,
                pivot = irt.pivot,
                rotation = irt.localRotation,
                preserveAspect = icon.preserveAspect,
                sprite = icon.sprite,
            };
        ItemSO so = it.itemScriptableObject;
        if (so != null && so.GridIcon != null && icon.sprite != so.GridIcon)
            icon.sprite = so.GridIcon;

        // The icon is laid out for the upright shape (W × H cells, minus the padding) and turned 90° with the item.
        float inset = Mathf.Max(0f, settings.iconPadding);
        Vector2 upright = rotated ? new Vector2(blockSize.y, blockSize.x) : blockSize;
        Vector2 area = new Vector2(Mathf.Max(4f, upright.x - inset * 2f), Mathf.Max(4f, upright.y - inset * 2f));
        float angle = so != null && !so.HasGridIcon ? so.GridIconAngle : 0f;
        Vector2 size = area;
        bool keep = settings.iconFitMode == GridIconFit.KeepProportions;
        if (Mathf.Abs(angle) > 0.01f)
        {
            // A picture turned by 'angle' is as big as possible while its turned outline stays inside the area along
            // the long side: a diagonal sword turned 45° stands upright over the item's whole length.
            Sprite sp = icon.sprite;
            float aspect = sp != null && sp.rect.height > 0f ? sp.rect.width / sp.rect.height : 1f;
            float rad = angle * Mathf.Deg2Rad, cos = Mathf.Abs(Mathf.Cos(rad)), sin = Mathf.Abs(Mathf.Sin(rad));
            // Outline of a w × h picture turned: (w cos + h sin) × (w sin + h cos); with w = aspect × h.
            float hByWidth = area.x / (aspect * cos + sin), hByHeight = area.y / (aspect * sin + cos);
            float longSide = Mathf.Max(area.x, area.y);
            float h = area.y >= area.x ? hByHeight : hByWidth; // fit the long side; the transparent corners may overhang
            h = Mathf.Min(h, longSide);
            size = new Vector2(h * aspect, h);
            keep = true;
        }
        else if (so != null && so.HasGridIcon)
        {
            keep = true; // drawn for this shape: fills it as drawn
        }
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;
        irt.sizeDelta = size;
        irt.localRotation = Quaternion.Euler(0f, 0f, (rotated ? 90f : 0f) + angle);
        icon.preserveAspect = keep;
    }

    /// <summary>Gives back the item's own look (it left the grid: hotbar, equipment, storage).</summary>
    public void Restore(InventoryItem it)
    {
        if (it == null)
            return;
        Image backdrop = Backdrop(it, false);
        if (backdrop != null)
            backdrop.enabled = false;
        Image icon = it.IconImage;
        if (icon == null)
            return;
        RectTransform irt = icon.rectTransform;
        if (!iconDefaults.TryGetValue(irt, out IconLayout d))
            return;
        irt.anchorMin = d.anchorMin;
        irt.anchorMax = d.anchorMax;
        irt.pivot = d.pivot;
        irt.offsetMin = d.offsetMin;
        irt.offsetMax = d.offsetMax;
        irt.localRotation = d.rotation;
        icon.preserveAspect = d.preserveAspect;
        if (it.itemScriptableObject != null && it.itemScriptableObject.Icon != null)
            icon.sprite = it.itemScriptableObject.Icon; // the grid picture is only for the grid
        else if (d.sprite != null)
            icon.sprite = d.sprite;
    }

    private void RestoreAll()
    {
        foreach (InventoryItem it in styled)
            if (it != null) Restore(it);
        styled.Clear();
    }

    private static Image Backdrop(InventoryItem it, bool create)
    {
        Transform t = it.transform.Find("GridBackdrop");
        if (t == null)
        {
            if (!create)
                return null;
            Image img = InventoryUIFactory.Panel("GridBackdrop", it.transform, Color.clear);
            img.raycastTarget = false;
            InventoryUIFactory.Stretch(img.rectTransform);
            img.transform.SetAsFirstSibling();
            return img;
        }
        return t.GetComponent<Image>();
    }

    // ------------------------------------------------------------------ feedback
    /// <summary>The placement preview over <paramref name="r"/> (clipped to the grid).</summary>
    public void ShowPreview(GridRect r, Color color)
    {
        Frame(preview, r, color, true);
        preview.transform.SetAsLastSibling();
    }

    public void HidePreview()
    {
        if (preview != null) preview.enabled = false;
        flashUntil = 0f;
    }

    /// <summary>Briefly shows <paramref name="r"/> in the invalid colour (a rotation that does not fit).</summary>
    public void FlashInvalid(GridRect r)
    {
        ShowPreview(r, settings.invalidColor);
        flashUntil = Time.unscaledTime + 0.35f;
    }

    public void ShowOrigin(GridRect r) => Frame(origin, r, settings.originColor, true);
    public void HideOrigin() { if (origin != null) origin.enabled = false; }

    public void ShowHover(GridRect r) => Frame(hover, r, settings.hoverColor, true);
    public void HideHover() { if (hover != null) hover.enabled = false; }

    private void Frame(Image img, GridRect r, Color color, bool clip)
    {
        if (img == null)
            return;
        int x0 = r.X, y0 = r.Y, x1 = r.Right, y1 = r.Bottom;
        if (clip)
        {
            x0 = Mathf.Clamp(x0, 0, grid.Columns);
            y0 = Mathf.Clamp(y0, 0, grid.Rows);
            x1 = Mathf.Clamp(x1, 0, grid.Columns);
            y1 = Mathf.Clamp(y1, 0, grid.Rows);
        }
        if (x1 <= x0 || y1 <= y0)
        {
            img.enabled = false;
            return;
        }
        RectTransform rt = img.rectTransform;
        rt.anchoredPosition = CellPosition(x0, y0);
        rt.sizeDelta = BlockSize(x1 - x0, y1 - y0);
        img.color = color;
        img.enabled = true;
    }
}
