using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The item panel shown next to the inventory while the pointer is over a slot: icon, name, type, description, what
/// the item does (stats and effects), durability with a bar, weight (per item and for the stack), stack and price.
/// Built from code by the <see cref="InventoryManager"/> when none is assigned; it places itself beside the inventory
/// panel (on whichever side has room).
/// </summary>
[DisallowMultipleComponent]
public class ItemHoverPanel : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private GameObject durabilityRow;
    [SerializeField] private TextMeshProUGUI durabilityText;
    [SerializeField] private RectTransform durabilityFill;
    [SerializeField] private Image durabilityFillImage;
    [SerializeField] private TextMeshProUGUI weightText;
    [SerializeField] private TextMeshProUGUI infoText;
    [Tooltip("Gap between the inventory panel and this panel.")]
    [SerializeField] private float gap = 12f;

    private InventoryItem shown;
    private int shownStack = -1;
    private int shownDurability = int.MinValue;
    private bool shownEquipped;
    private readonly List<string> lines = new List<string>();
    private readonly StringBuilder sb = new StringBuilder();

    public InventoryItem Shown => shown;

    /// <summary>Builds the panel under <paramref name="parent"/> (inactive until an item is hovered).</summary>
    public static ItemHoverPanel Create(Transform parent)
    {
        Image bg = InventoryUIFactory.Panel("ItemHoverPanel", parent, InventoryUIFactory.PanelColor);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(320f, 0f);
        bg.raycastTarget = false;
        InventoryUIFactory.Vertical(bg.gameObject, 14, 6f, true);
        var p = bg.gameObject.AddComponent<ItemHoverPanel>();

        RectTransform header = InventoryUIFactory.Rect("Header", rt);
        InventoryUIFactory.Horizontal(header.gameObject, 10f);
        InventoryUIFactory.Size(header, -1f, 56f);
        Image iconBg = InventoryUIFactory.Panel("IconFrame", header, InventoryUIFactory.FieldColor);
        iconBg.raycastTarget = false;
        InventoryUIFactory.Size(iconBg, 56f, 56f, 0f);
        p.icon = InventoryUIFactory.Panel("Icon", iconBg.transform, Color.white);
        p.icon.preserveAspect = true;
        p.icon.raycastTarget = false;
        InventoryUIFactory.Stretch(p.icon.rectTransform, 4f, 4f);

        RectTransform titles = InventoryUIFactory.Rect("Titles", header);
        InventoryUIFactory.Vertical(titles.gameObject, 0, 2f, false);
        InventoryUIFactory.Size(titles, -1f, -1f, 1f);
        p.nameText = InventoryUIFactory.Text("Name", titles, "Item", 20f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
        p.typeText = InventoryUIFactory.Text("Type", titles, "Type", 14f, InventoryUIFactory.MutedColor);

        p.descriptionText = InventoryUIFactory.Text("Description", rt, "", 14f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Left, FontStyles.Italic);
        p.statsText = InventoryUIFactory.Text("Stats", rt, "", 15f, new Color(0.75f, 0.9f, 1f));

        p.durabilityRow = InventoryUIFactory.Rect("Durability", rt).gameObject;
        InventoryUIFactory.Vertical(p.durabilityRow, 0, 3f, false);
        p.durabilityText = InventoryUIFactory.Text("Label", p.durabilityRow.transform, "Durability", 14f, InventoryUIFactory.TextColor);
        Image barBg = InventoryUIFactory.Panel("Bar", p.durabilityRow.transform, InventoryUIFactory.FieldColor);
        barBg.raycastTarget = false;
        InventoryUIFactory.Size(barBg, -1f, 8f);
        p.durabilityFillImage = InventoryUIFactory.Panel("Fill", barBg.transform, Color.green);
        p.durabilityFillImage.raycastTarget = false;
        p.durabilityFill = p.durabilityFillImage.rectTransform;
        p.durabilityFill.anchorMin = Vector2.zero;
        p.durabilityFill.anchorMax = Vector2.one;
        p.durabilityFill.offsetMin = p.durabilityFill.offsetMax = Vector2.zero;

        p.weightText = InventoryUIFactory.Text("Weight", rt, "", 14f, InventoryUIFactory.TextColor);
        p.infoText = InventoryUIFactory.Text("Info", rt, "", 13f, InventoryUIFactory.MutedColor);
        bg.gameObject.SetActive(false);
        return p;
    }

    /// <summary>Shows <paramref name="item"/> beside <paramref name="anchorPanel"/> (the inventory panel). Null hides.</summary>
    public void Show(InventoryItem item, RectTransform anchorPanel)
    {
        if (item == null || item.itemScriptableObject == null)
        {
            Hide();
            return;
        }
        int durabilityNow = item.DurabilityList != null && item.DurabilityList.Count > 0 ? item.DurabilityList[item.DurabilityList.Count - 1] : -1;
        bool changed = item != shown || item.stackCurrent != shownStack || durabilityNow != shownDurability || item.isEquipped != shownEquipped;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            changed = true;
        }
        if (changed)
        {
            shown = item;
            shownStack = item.stackCurrent;
            shownDurability = durabilityNow;
            shownEquipped = item.isEquipped;
            Fill(item, durabilityNow);
            var rt = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            PlaceBeside(anchorPanel);
        }
    }

    public void Hide()
    {
        shown = null;
        shownStack = -1;
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void Fill(InventoryItem item, int durabilityNow)
    {
        ItemSO so = item.itemScriptableObject;
        icon.sprite = so.Icon;
        icon.enabled = so.Icon != null;
        nameText.text = string.IsNullOrEmpty(so.Name) ? so.name : so.Name;
        typeText.text = TypeLine(so) + (item.isEquipped ? "  ·  <color=#7fd37f>Equipped</color>" : "");

        descriptionText.text = so.Description;
        descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(so.Description));

        lines.Clear();
        so.AppendTooltip(lines);
        statsText.text = string.Join("\n", lines);
        statsText.gameObject.SetActive(lines.Count > 0);

        // Durability of the unit in use (the last of the stack), out of the item's maximum.
        bool durable = so.MaxDurability > 0 && durabilityNow >= 0;
        durabilityRow.SetActive(durable);
        if (durable)
        {
            float ratio = Mathf.Clamp01(durabilityNow / (float)so.MaxDurability);
            durabilityText.text = $"Durability  {durabilityNow} / {so.MaxDurability}" + (item.stackCurrent > 1 ? "  (item in use)" : "");
            durabilityFill.anchorMax = new Vector2(ratio, 1f);
            durabilityFillImage.color = ratio > 0.5f ? new Color(0.35f, 0.8f, 0.4f) : ratio > 0.2f ? new Color(0.95f, 0.75f, 0.25f) : new Color(0.9f, 0.3f, 0.25f);
        }

        float each = so.Weight;
        weightText.text = item.stackCurrent > 1
            ? $"Weight  {each:0.##} each  ·  {each * item.stackCurrent:0.##} total"
            : $"Weight  {each:0.##}";

        sb.Clear();
        if (item.stackMax > 1 || item.stackCurrent > 1)
            sb.Append($"Stack  {item.stackCurrent} / {Mathf.Max(item.stackMax, so.StackMax)}");
        if (so.Price > 0f)
        {
            if (sb.Length > 0) sb.Append("   ·   ");
            sb.Append($"Value  {so.Price:0.##}" + (item.stackCurrent > 1 ? $" ({so.Price * item.stackCurrent:0.##})" : ""));
        }
        if (so.Cooldown > 0f)
        {
            if (sb.Length > 0) sb.Append("   ·   ");
            sb.Append($"Cooldown  {so.Cooldown:0.#}s");
        }
        infoText.text = sb.ToString();
        infoText.gameObject.SetActive(sb.Length > 0);
    }

    private static string TypeLine(ItemSO so)
    {
        switch (so)
        {
            case WeaponSO w: return w.Category != WeaponCategory.None ? $"Weapon · {w.Category}" : "Weapon";
            case ArmorSO a: return $"Armor · {SlotTypeHelper.GetDisplayName(a.ArmorSlotType)}";
            case ConsumableSO _: return so.ItemType == ItemType.Food ? "Food" : "Consumable";
            default: return ObjectNames(so.ItemType.ToString());
        }
    }

    private static string ObjectNames(string s)
    {
        var b = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) b.Append(' ');
            b.Append(s[i]);
        }
        return b.ToString();
    }

    /// <summary>Right of the inventory panel, or left of it when there is no room, top edges aligned.</summary>
    private void PlaceBeside(RectTransform anchorPanel)
    {
        var rt = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (anchorPanel == null || canvas == null)
            return;
        Vector3[] a = new Vector3[4];
        anchorPanel.GetWorldCorners(a); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
        Vector3[] k = new Vector3[4];
        ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(k);
        float scale = canvas.rootCanvas.transform.lossyScale.x;
        float width = rt.rect.width * scale;
        float g = gap * scale;

        bool roomRight = a[2].x + g + width <= k[2].x;
        rt.pivot = new Vector2(roomRight ? 0f : 1f, 1f);
        rt.position = roomRight ? new Vector3(a[2].x + g, a[2].y, 0f) : new Vector3(a[1].x - g, a[1].y, 0f);
        InventoryUIFactory.ClampToCanvas(rt, canvas);
    }
}
