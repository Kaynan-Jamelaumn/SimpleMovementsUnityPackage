using UnityEngine;

public enum SlotType
{
    Common,
    Potion,
    Food,
    Helmet,
    Armor,
    Boots,
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

    private void SetupItemUI(GameObject item)
    {
        Transform targetContainer = GetSharedContainer();

        if (targetContainer == null)
        {
            Debug.LogError($"No shared container available for slot {gameObject.name}. " +
                          $"IsHotbar: {isHotbarSlot}, SlotType: {slotType}");
            return;
        }

        // Set the item's parent to the shared container
        item.transform.SetParent(targetContainer, false);

        // Position the item to match this slot's position
        PositionItemToSlot(item);

        // Ensure proper layering (newer items on top)
        item.transform.SetAsLastSibling();
    }

    private void PositionItemToSlot(GameObject item)
    {
        RectTransform itemRect = item.GetComponent<RectTransform>();
        RectTransform slotRect = GetComponent<RectTransform>();

        if (itemRect == null || slotRect == null) return;

        // Convert slot's world position to the container's local space
        Transform container = GetSharedContainer();
        RectTransform containerRect = container.GetComponent<RectTransform>();

        if (containerRect == null) return;

        // Get slot position in world space
        Vector3 slotWorldPos = slotRect.position;

        // Convert to container's local space
        Vector3 localPos = containerRect.InverseTransformPoint(slotWorldPos);

        // Set item position and size
        itemRect.localPosition = localPos;
        itemRect.localRotation = Quaternion.identity;
        itemRect.localScale = Vector3.one;

        // Size the item to match the slot
        itemRect.sizeDelta = slotRect.sizeDelta;

        // Center the anchors
        itemRect.anchorMin = Vector2.one * 0.5f;
        itemRect.anchorMax = Vector2.one * 0.5f;
        itemRect.pivot = Vector2.one * 0.5f;
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