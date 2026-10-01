using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// The item tooltip panel (right click on a slot): name, price, weight, quantity, type, description, and - when the
/// Item Stats text is assigned - what the item does (stats, effects, attacks, set bonuses), durability and whether it
/// is equipped. Every text field is optional.
/// </summary>
public class ItemInfo : MonoBehaviour
{
    [Tooltip("Found automatically when empty.")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private InventoryItem clickedItem;
    [SerializeField] private TextMeshProUGUI itemName;
    [SerializeField] private TextMeshProUGUI itemPrice;
    [SerializeField] private TextMeshProUGUI itemWeight;
    [SerializeField] private TextMeshProUGUI itemType;
    [SerializeField] private TextMeshProUGUI itemDescription;
    [SerializeField] private TextMeshProUGUI itemQuantity;
    [Tooltip("Optional: what the item does (stats, effects, attacks, set bonuses), durability and equipped state.")]
    [SerializeField] private TextMeshProUGUI itemStats;

    private readonly List<string> lines = new List<string>();

    public InventoryItem ShownItem => clickedItem;

    private void Awake()
    {
        if (inventoryManager == null)
        {
            // This player's inventory first; a scene-wide search only when there is exactly one (multiplayer-safe).
            inventoryManager = GetComponentInParent<InventoryManager>();
            if (inventoryManager == null)
                inventoryManager = transform.root.GetComponentInChildren<InventoryManager>(true);
            if (inventoryManager == null)
                inventoryManager = InventoryUtils.OnlyInstance<InventoryManager>();
        }
    }

    private static void Set(TextMeshProUGUI field, string text)
    {
        if (field != null)
            field.text = text;
    }

    private void PositionRelativeToMouse(Vector2 mousePosition)
    {
        RectTransform itemInfoRectTransform = GetComponent<RectTransform>();
        RectTransform canvasRectTransform = itemInfoRectTransform.root.GetComponent<RectTransform>();
        if (canvasRectTransform == null)
            return;
        float canvasWidth = canvasRectTransform.rect.width;
        float imageHeight = itemInfoRectTransform.rect.height;
        float imageWidth = itemInfoRectTransform.rect.width;

        Vector2 newPosition;
        if (mousePosition.y - imageHeight * 0.5f < 0)
        {
            newPosition = new Vector2(mousePosition.x + imageWidth, imageHeight * 0.5f);
        }
        else
        {
            newPosition = new Vector2(
                Mathf.Min(mousePosition.x + imageWidth, canvasWidth - imageWidth * 0.5f),
                Mathf.Max(mousePosition.y - imageHeight * 0.5f, imageHeight * 0.5f));
        }
        itemInfoRectTransform.position = newPosition;
    }

    /// <summary>Splits the shown stack in two (button on the panel).</summary>
    public void SplitItem()
    {
        if (inventoryManager == null || clickedItem == null)
            return;
        SplitItemHandler.SplitItemIntoNewStack(inventoryManager, clickedItem, inventoryManager.Slots, inventoryManager.Player);
        gameObject.SetActive(false);
    }

    public void ShowItemInfo(InventoryItem itemToShow)
    {
        if (itemToShow == null || itemToShow.itemScriptableObject == null)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);
        clickedItem = itemToShow;
        ItemSO so = clickedItem.itemScriptableObject;
        Set(itemName, so.Name);
        Set(itemPrice, $"Price: {so.Price}");
        Set(itemWeight, $"Weight: {clickedItem.totalWeight:0.##} Kg");
        Set(itemQuantity, $"Quantity: {clickedItem.stackCurrent}/{clickedItem.stackMax}");
        Set(itemType, so is ArmorSO armor ? $"{so.ItemType} ({armor.ArmorSlotType})" : so is WeaponSO weapon && weapon.Category != WeaponCategory.None ? $"{so.ItemType} ({weapon.Category})" : so.ItemType.ToString());
        Set(itemDescription, so.Description);
        if (itemStats != null)
            itemStats.text = BuildStats(clickedItem);

        if (Mouse.current != null)
            PositionRelativeToMouse(Mouse.current.position.ReadValue());
    }

    /// <summary>The tooltip body: effects, set bonuses, durability, equipped state.</summary>
    public string BuildStats(InventoryItem item)
    {
        lines.Clear();
        ItemSO so = item.itemScriptableObject;
        so.AppendTooltip(lines);

        if (so is EquippableSO eq && eq.BelongsToArmorSet != null)
        {
            ArmorSet set = eq.BelongsToArmorSet;
            int worn = inventoryManager != null && inventoryManager.ArmorSetManager != null ? inventoryManager.ArmorSetManager.GetEquippedPiecesCount(set) : 0;
            lines.Add($"{set.SetName} ({worn}/{set.SetPieces.Count}):");
            foreach (string tier in set.DescribeTiers(worn))
                lines.Add("  " + tier);
        }

        if (so.MaxDurability > 1 || so.DurabilityReductionPerUse > 0 && !(so is ConsumableSO))
            lines.Add($"Durability: {item.durability:0}/{so.MaxDurability}");
        if (item.isEquipped)
            lines.Add("Equipped");

        var sb = new StringBuilder();
        foreach (string l in lines)
            sb.AppendLine(l);
        return sb.ToString();
    }
}
