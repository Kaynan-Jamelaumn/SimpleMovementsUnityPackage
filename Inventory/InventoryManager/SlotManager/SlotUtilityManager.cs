using UnityEngine;

[System.Serializable]
public class SlotUtilityManager
{
    [Header("Shared Item Containers")]
    [SerializeField, Tooltip("Container for all inventory item visuals (optional - will auto-create)")]
    private Transform inventoryItemsContainer;

    [SerializeField, Tooltip("Container for all hotbar item visuals (optional - will auto-create)")]
    private Transform hotbarItemsContainer;

    [SerializeField, Tooltip("Container for all equipment item visuals (optional - will auto-create)")]
    private Transform equipmentItemsContainer;

    // Properties
    public Transform InventoryItemsContainer => inventoryItemsContainer;
    public Transform HotbarItemsContainer => hotbarItemsContainer;
    public Transform EquipmentItemsContainer => equipmentItemsContainer;

    public void Initialize(Transform inventorySlotsParent, Transform hotbarSlotsParent, Transform equipmentSlotsParent)
    {
        SetupSharedContainers(inventorySlotsParent, hotbarSlotsParent, equipmentSlotsParent);
    }

    private void SetupSharedContainers(Transform inventorySlotsParent, Transform hotbarSlotsParent, Transform equipmentSlotsParent)
    {
        // Create or find inventory items container
        if (inventoryItemsContainer == null)
        {
            inventoryItemsContainer = CreateSharedContainer("InventoryItemsContainer", inventorySlotsParent);
        }

        // Create or find hotbar items container
        if (hotbarItemsContainer == null)
        {
            hotbarItemsContainer = CreateSharedContainer("HotbarItemsContainer", hotbarSlotsParent);
        }

        // Create or find equipment items container
        if (equipmentItemsContainer == null && equipmentSlotsParent != null)
        {
            equipmentItemsContainer = CreateSharedContainer("EquipmentItemsContainer", equipmentSlotsParent);
        }

        // Set the shared containers for all slots
        InventorySlot.SetSharedContainers(inventoryItemsContainer, hotbarItemsContainer, equipmentItemsContainer);

        Debug.Log($"Shared containers set up - Inventory: {inventoryItemsContainer?.name}, " +
                  $"Hotbar: {hotbarItemsContainer?.name}, Equipment: {equipmentItemsContainer?.name}");
    }

    private Transform CreateSharedContainer(string containerName, Transform parentTransform)
    {
        if (parentTransform == null)
        {
            Debug.LogWarning($"Cannot create {containerName} - parent transform is null");
            return null;
        }

        // Check if container already exists
        Transform existingContainer = parentTransform.Find(containerName);
        if (existingContainer != null)
        {
            return existingContainer;
        }

        // Create new container
        GameObject containerObj = new GameObject(containerName);
        containerObj.transform.SetParent(parentTransform, false);

        // Set up RectTransform to cover the entire parent area
        RectTransform rectTransform = containerObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        // Make sure it renders on top of slots
        containerObj.transform.SetAsLastSibling();

        return containerObj.transform;
    }

    public void ClearSharedContainers()
    {
        // Clear all items from shared containers
        ClearContainer(inventoryItemsContainer);
        ClearContainer(hotbarItemsContainer);
        ClearContainer(equipmentItemsContainer);
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (child != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    public Transform GetSharedContainer(bool isHotbar, SlotType slotType)
    {
        if (isHotbar)
            return hotbarItemsContainer;

        if (slotType != SlotType.Common)
            return equipmentItemsContainer;

        return inventoryItemsContainer;
    }

    public void LogSharedContainerInfo()
    {
        Debug.Log($"Shared Containers - Inventory: {inventoryItemsContainer?.name ?? "NULL"} " +
                  $"({inventoryItemsContainer?.childCount ?? 0} items), " +
                  $"Hotbar: {hotbarItemsContainer?.name ?? "NULL"} " +
                  $"({hotbarItemsContainer?.childCount ?? 0} items), " +
                  $"Equipment: {equipmentItemsContainer?.name ?? "NULL"} " +
                  $"({equipmentItemsContainer?.childCount ?? 0} items)");
    }
}