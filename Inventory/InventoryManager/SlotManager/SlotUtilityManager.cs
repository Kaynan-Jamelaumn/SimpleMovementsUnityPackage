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

    [Header("Container Settings")]
    [SerializeField, Tooltip("Automatically create containers if they don't exist")]
    private bool autoCreateContainers = true;

    [SerializeField, Tooltip("Z-order offset for item containers")]
    private int containerSiblingIndex = -1; // -1 means set as last sibling

    // Properties
    public Transform InventoryItemsContainer => inventoryItemsContainer;
    public Transform HotbarItemsContainer => hotbarItemsContainer;
    public Transform EquipmentItemsContainer => equipmentItemsContainer;
    public bool AutoCreateContainers => autoCreateContainers;

    public void Initialize(Transform inventorySlotsParent, Transform hotbarSlotsParent, Transform equipmentSlotsParent)
    {
        SetupSharedContainers(inventorySlotsParent, hotbarSlotsParent, equipmentSlotsParent);
    }

    private void SetupSharedContainers(Transform inventorySlotsParent, Transform hotbarSlotsParent, Transform equipmentSlotsParent)
    {
        // Create or find inventory items container
        if (inventoryItemsContainer == null && autoCreateContainers)
        {
            inventoryItemsContainer = CreateSharedContainer("InventoryItemsContainer", inventorySlotsParent);
        }

        // Create or find hotbar items container
        if (hotbarItemsContainer == null && autoCreateContainers)
        {
            hotbarItemsContainer = CreateSharedContainer("HotbarItemsContainer", hotbarSlotsParent);
        }

        // Create or find equipment items container
        if (equipmentItemsContainer == null && equipmentSlotsParent != null && autoCreateContainers)
        {
            equipmentItemsContainer = CreateSharedContainer("EquipmentItemsContainer", equipmentSlotsParent);
        }

        // Set the shared containers for all slots
        InventorySlot.SetSharedContainers(inventoryItemsContainer, hotbarItemsContainer, equipmentItemsContainer);

        Debug.Log($"Shared containers set up - Inventory: {inventoryItemsContainer?.name ?? "NULL"}, " +
                  $"Hotbar: {hotbarItemsContainer?.name ?? "NULL"}, Equipment: {equipmentItemsContainer?.name ?? "NULL"}");
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
            Debug.Log($"Found existing container: {containerName}");
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

        // Set z-order
        if (containerSiblingIndex >= 0)
        {
            containerObj.transform.SetSiblingIndex(containerSiblingIndex);
        }
        else
        {
            // Make sure it renders on top of slots
            containerObj.transform.SetAsLastSibling();
        }

        // Add CanvasGroup for potential fade effects
        CanvasGroup canvasGroup = containerObj.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false; // Items should not block slot interactions
        canvasGroup.blocksRaycasts = false;

        Debug.Log($"Created new container: {containerName}");
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

    // Manual container assignment methods
    public void SetInventoryContainer(Transform container)
    {
        inventoryItemsContainer = container;
        UpdateSharedContainerReferences();
    }

    public void SetHotbarContainer(Transform container)
    {
        hotbarItemsContainer = container;
        UpdateSharedContainerReferences();
    }

    public void SetEquipmentContainer(Transform container)
    {
        equipmentItemsContainer = container;
        UpdateSharedContainerReferences();
    }

    private void UpdateSharedContainerReferences()
    {
        InventorySlot.SetSharedContainers(inventoryItemsContainer, hotbarItemsContainer, equipmentItemsContainer);
    }

    // Container validation
    public bool ValidateContainers()
    {
        bool isValid = true;

        if (inventoryItemsContainer == null)
        {
            Debug.LogWarning("Inventory items container is null");
            isValid = false;
        }
        else if (inventoryItemsContainer.GetComponent<RectTransform>() == null)
        {
            Debug.LogWarning("Inventory items container is missing RectTransform");
            isValid = false;
        }

        if (hotbarItemsContainer == null)
        {
            Debug.LogWarning("Hotbar items container is null");
            isValid = false;
        }
        else if (hotbarItemsContainer.GetComponent<RectTransform>() == null)
        {
            Debug.LogWarning("Hotbar items container is missing RectTransform");
            isValid = false;
        }

        if (equipmentItemsContainer != null && equipmentItemsContainer.GetComponent<RectTransform>() == null)
        {
            Debug.LogWarning("Equipment items container is missing RectTransform");
            isValid = false;
        }

        return isValid;
    }

    // Container statistics
    public int GetTotalItemsInContainers()
    {
        int total = 0;
        if (inventoryItemsContainer != null) total += inventoryItemsContainer.childCount;
        if (hotbarItemsContainer != null) total += hotbarItemsContainer.childCount;
        if (equipmentItemsContainer != null) total += equipmentItemsContainer.childCount;
        return total;
    }

    public void SetAutoCreateContainers(bool enabled)
    {
        autoCreateContainers = enabled;
    }

    public void SetContainerSiblingIndex(int index)
    {
        containerSiblingIndex = index;

        // Update existing containers
        if (inventoryItemsContainer != null)
        {
            if (index >= 0)
                inventoryItemsContainer.SetSiblingIndex(index);
            else
                inventoryItemsContainer.SetAsLastSibling();
        }

        if (hotbarItemsContainer != null)
        {
            if (index >= 0)
                hotbarItemsContainer.SetSiblingIndex(index);
            else
                hotbarItemsContainer.SetAsLastSibling();
        }

        if (equipmentItemsContainer != null)
        {
            if (index >= 0)
                equipmentItemsContainer.SetSiblingIndex(index);
            else
                equipmentItemsContainer.SetAsLastSibling();
        }
    }

    // Force refresh all containers
    public void RefreshContainers()
    {
        ValidateContainers();
        UpdateSharedContainerReferences();
        LogSharedContainerInfo();
    }
}