using UnityEngine;

public enum SlotType
{
    Common,
    Potion,
    Food,
    Helmet,
    Armor,
    Boots,
    Ring,
    Trinket,
    Wrist,     // Bracers
    Gloves,
    Shield,
    Belt,
    Cloak,
    Amulet,    // Necklace/Trinket alternative
    Shoulders,
    Leggings
}

public class InventorySlot : MonoBehaviour
{
    [Header("Slot Configuration")]
    [Tooltip("The inventory slot item that is being held in this slot")]
    public GameObject heldItem = null;

    [Tooltip("The slot type item the slot can support")]
    [SerializeField] private SlotType slotType = SlotType.Common;

    [Tooltip("Is this a hotbar slot?")]
    [SerializeField] private bool isHotbarSlot = false;

    // Shared container references (set by SlotManager)
    private static Transform sharedInventoryItemsContainer;
    private static Transform sharedHotbarItemsContainer;
    private static Transform sharedEquipmentItemsContainer;

    // Properties
    public SlotType SlotType
    {
        get => slotType;
        set => slotType = value;
    }

    public bool IsHotbarSlot => isHotbarSlot;

    // Static methods to set shared containers (called by SlotManager)
    public static void SetSharedContainers(Transform inventoryContainer, Transform hotbarContainer, Transform equipmentContainer)
    {
        sharedInventoryItemsContainer = inventoryContainer;
        sharedHotbarItemsContainer = hotbarContainer;
        sharedEquipmentItemsContainer = equipmentContainer;
    }

    // Get the appropriate shared container for this slot
    private Transform GetSharedContainer()
    {
        if (isHotbarSlot)
            return sharedHotbarItemsContainer;

        if (slotType != SlotType.Common) // Equipment slot
            return sharedEquipmentItemsContainer;

        return sharedInventoryItemsContainer; // Regular inventory slot
    }

    public void SetHeldItem(GameObject item)
    {
        // Clear previous item reference
        if (heldItem != null && heldItem != item)
        {
            ClearItemReference();
        }

        heldItem = item;

        if (item != null)
        {
            SetupItemUI(item);
        }
    }

    /// <summary>
    /// The item becomes a child of this slot, stretched over it. (Items used to go into a shared container and be
    /// moved to the slot's screen position at that moment: a slot of a closed panel had not been laid out yet, so
    /// picked-up items showed in the middle of the panel, and items dropped on the hotbar could end up hidden.)
    /// </summary>
    private void SetupItemUI(GameObject item)
    {
        item.transform.SetParent(transform, false);
        PositionItemToSlot(item);
        item.transform.SetAsLastSibling(); // drawn above the slot's own graphics
        if (!item.activeSelf)
            item.SetActive(true);
    }

    private void PositionItemToSlot(GameObject item)
    {
        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect == null) return;
        itemRect.anchorMin = Vector2.zero;
        itemRect.anchorMax = Vector2.one;
        itemRect.pivot = Vector2.one * 0.5f;
        itemRect.offsetMin = Vector2.zero;
        itemRect.offsetMax = Vector2.zero;
        itemRect.localRotation = Quaternion.identity;
        itemRect.localScale = Vector3.one;
    }

    private void ClearItemReference()
    {
        // Just clear the reference - don't destroy the item
        // The item will be repositioned by its new slot
        heldItem = null;
    }

    public void ClearSlot()
    {
        if (heldItem != null)
        {
            // Destroy the item completely
            Destroy(heldItem);
            heldItem = null;
        }
    }

    // Method to set if this is a hotbar slot (used by SlotManager)
    public void SetAsHotbarSlot(bool isHotbar)
    {
        isHotbarSlot = isHotbar;
    }

    // Validation method
    public bool ValidateSlotSetup()
    {
        Transform container = GetSharedContainer();
        if (container == null)
        {
            Debug.LogWarning($"Slot {gameObject.name} cannot find appropriate shared container. " +
                           $"IsHotbar: {isHotbarSlot}, SlotType: {slotType}");
            return false;
        }
        return true;
    }

    // Debug method to check container assignment
    public void LogContainerInfo()
    {
        Transform container = GetSharedContainer();
        Debug.Log($"Slot {gameObject.name}: Container={container?.name ?? "NULL"}, " +
                  $"IsHotbar={isHotbarSlot}, SlotType={slotType}");
    }

    // Update item position if slot moves (useful for dynamic layouts)
    public void RefreshItemPosition()
    {
        if (heldItem != null)
        {
            PositionItemToSlot(heldItem);
        }
    }
}