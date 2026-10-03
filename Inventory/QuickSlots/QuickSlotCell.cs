using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One cell of the <see cref="QuickSlotBar"/> UI: icon, how many are left, its key, the cooldown. Left click uses it,
/// right click clears it; dropping an inventory item on it assigns that item (the item itself stays in the inventory).
/// </summary>
public class QuickSlotCell : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private Image cooldown;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private TextMeshProUGUI keyText;

    private QuickSlotBar bar;
    private int index;
    private float flashUntil;
    private Color flashColor;
    private Color baseColor;

    public int Index => index;
    public QuickSlotBar Bar => bar;

    /// <summary>Builds a cell under <paramref name="parent"/>.</summary>
    public static QuickSlotCell Create(Transform parent, QuickSlotBar owner, int slotIndex, float size)
    {
        Image bg = InventoryUIFactory.Panel($"QuickSlot_{slotIndex + 1}", parent, new Color(1f, 1f, 1f, 0.12f));
        bg.raycastTarget = true;
        InventoryUIFactory.Size(bg, size, size, 0f);
        var cell = bg.gameObject.AddComponent<QuickSlotCell>();
        cell.background = bg;
        cell.baseColor = bg.color;
        cell.bar = owner;
        cell.index = slotIndex;

        cell.icon = InventoryUIFactory.Panel("Icon", bg.transform, Color.white);
        cell.icon.raycastTarget = false;
        cell.icon.preserveAspect = true;
        InventoryUIFactory.Stretch(cell.icon.rectTransform, 6f, 6f);

        cell.cooldown = InventoryUIFactory.Panel("Cooldown", bg.transform, new Color(0f, 0f, 0f, 0.6f));
        cell.cooldown.raycastTarget = false;
        cell.cooldown.type = Image.Type.Filled;
        cell.cooldown.fillMethod = Image.FillMethod.Radial360;
        cell.cooldown.fillOrigin = (int)Image.Origin360.Top;
        cell.cooldown.fillClockwise = false;
        cell.cooldown.fillAmount = 0f;
        InventoryUIFactory.Stretch(cell.cooldown.rectTransform);

        cell.countText = InventoryUIFactory.Text("Count", bg.transform, "", 16f, InventoryUIFactory.TextColor, TextAlignmentOptions.BottomRight, FontStyles.Bold);
        InventoryUIFactory.Stretch(cell.countText.rectTransform, 5f, 3f);
        cell.keyText = InventoryUIFactory.Text("Key", bg.transform, "", 13f, InventoryUIFactory.MutedColor, TextAlignmentOptions.TopLeft);
        InventoryUIFactory.Stretch(cell.keyText.rectTransform, 5f, 3f);
        return cell;
    }

    public void Bind(QuickSlotBar owner, int slotIndex)
    {
        bar = owner;
        index = slotIndex;
        if (background != null)
            baseColor = background.color;
    }

    /// <summary>Redraws the cell: item icon (grey when none is left), count, key, cooldown.</summary>
    public void Refresh(ItemSO item, int count, string key, float cooldownFraction)
    {
        if (icon != null)
        {
            icon.enabled = item != null && item.Icon != null;
            icon.sprite = item != null ? item.Icon : null;
            icon.color = item != null && count <= 0 ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        }
        if (countText != null)
            countText.text = item != null ? count.ToString() : "";
        if (keyText != null)
            keyText.text = key ?? "";
        if (cooldown != null)
            cooldown.fillAmount = Mathf.Clamp01(cooldownFraction);
        if (background != null)
            background.color = Time.unscaledTime < flashUntil ? flashColor : baseColor;
    }

    /// <summary>Briefly tints the cell (green: used, red: could not be used).</summary>
    public void Flash(bool ok)
    {
        flashColor = ok ? new Color(0.4f, 1f, 0.5f, 0.45f) : new Color(1f, 0.3f, 0.3f, 0.5f);
        flashUntil = Time.unscaledTime + 0.25f;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (bar == null)
            return;
        if (eventData.button == PointerEventData.InputButton.Right)
            bar.Clear(index);
        else if (eventData.button == PointerEventData.InputButton.Left)
            bar.TryUse(index);
    }
}
