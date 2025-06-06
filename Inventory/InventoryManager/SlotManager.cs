using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotManager
{
    [Header("Slot Configuration")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform hotbarSlotsParent;
    [SerializeField] private Transform inventorySlotsParent;
    [SerializeField] private GridLayoutGroup inventoryGridLayout;

    [Header("Slot Size Constraints")]
    [SerializeField] private float minSlotSize = 60f;
    [SerializeField] private float maxSlotSize = 120f;
    [SerializeField] private float slotSpacing = 5f;

    // Slot arrays
    private GameObject[] hotbarSlots;
    private GameObject[] inventorySlots;

    // Dependencies
    private PlayerStatusController playerStatusController;
    private GameObject player;

    // Events
    public System.Action OnSlotsChanged;

    // Properties
    public GameObject[] HotbarSlots => hotbarSlots;
    public GameObject[] InventorySlots => inventorySlots;

    public void Initialize(PlayerStatusController playerController, GameObject playerObject)
    {
        this.playerStatusController = playerController;
        this.player = playerObject;

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (slotPrefab == null)
            Debug.LogError("Slot Prefab is not assigned in SlotManager");
        if (hotbarSlotsParent == null)
            Debug.LogError("Hotbar Slots Parent is not assigned in SlotManager");
        if (inventorySlotsParent == null)
            Debug.LogError("Inventory Slots Parent is not assigned in SlotManager");
        if (inventoryGridLayout == null)
            Debug.LogError("Inventory Grid Layout is not assigned in SlotManager");
    }

    public void CreateAllSlots(int hotbarSlotCount, int inventorySlotCount)
    {
        ClearAllExistingSlots();
        CreateHotbarSlots(hotbarSlotCount);
        CreateInventorySlots(inventorySlotCount);
        UpdateGridLayout(inventorySlotCount);
        OnSlotsChanged?.Invoke();
    }

    public void UpdateHotbarSlots(int newSlotCount)
    {
        if (Application.isPlaying)
        {
            ClearSlotsFromParent(hotbarSlotsParent);
            CreateHotbarSlots(newSlotCount);
            OnSlotsChanged?.Invoke();
        }
    }

    public void UpdateInventorySlots(int newSlotCount)
    {
        if (Application.isPlaying)
        {
            ClearSlotsFromParent(inventorySlotsParent);
            CreateInventorySlots(newSlotCount);
            UpdateGridLayout(newSlotCount);
            OnSlotsChanged?.Invoke();
        }
    }

    private void CreateHotbarSlots(int slotCount)
    {
        // Handle edge cases gracefully
        slotCount = Mathf.Max(0, slotCount);

        // Create new array and slots
        hotbarSlots = new GameObject[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            hotbarSlots[i] = CreateSlot(hotbarSlotsParent, SlotType.Common, $"HotbarSlot_{i}");
        }
    }

    private void CreateInventorySlots(int slotCount)
    {
        // Handle edge cases gracefully
        slotCount = Mathf.Max(0, slotCount);

        // Create new array and slots
        inventorySlots = new GameObject[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            inventorySlots[i] = CreateSlot(inventorySlotsParent, SlotType.Common, $"InventorySlot_{i}");
        }
    }

    private GameObject CreateSlot(Transform parent, SlotType slotType, string slotName)
    {
        if (slotPrefab == null || parent == null)
        {
            Debug.LogError("Cannot create slot: slotPrefab or parent is null");
            return null;
        }

        GameObject newSlot = Object.Instantiate(slotPrefab, parent);
        newSlot.name = slotName;

        // Setup slot component
        InventorySlot slotComponent = newSlot.GetComponent<InventorySlot>();
        if (slotComponent == null)
            slotComponent = newSlot.AddComponent<InventorySlot>();

        slotComponent.SlotType = slotType;

        // Ensure the slot has necessary UI components
        EnsureSlotUIComponents(newSlot);

        return newSlot;
    }

    private void EnsureSlotUIComponents(GameObject slot)
    {
        // Ensure Image component with raycast target enabled
        Image slotImage = slot.GetComponent<Image>();
        if (slotImage == null)
            slotImage = slot.AddComponent<Image>();
        slotImage.raycastTarget = true;

        // Set default slot appearance if needed
        if (slotImage.sprite == null)
        {
            // You can assign a default slot sprite here if available
        }
    }

    private void ClearAllExistingSlots()
    {
        ClearSlotsFromParent(hotbarSlotsParent);
        ClearSlotsFromParent(inventorySlotsParent);
    }

    private void ClearSlotsFromParent(Transform parent)
    {
        if (parent == null) return;

        // Clear all existing children
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null)
            {
                // Check if child has an inventory slot with items and drop them
                InventorySlot slotComponent = child.GetComponent<InventorySlot>();
                if (slotComponent?.heldItem != null)
                {
                    DropItemFromSlot(slotComponent);
                }

                if (Application.isPlaying)
                    Object.Destroy(child.gameObject);
                else
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private void DropItemFromSlot(InventorySlot slot)
    {
        if (slot?.heldItem == null) return;

        InventoryItem item = slot.heldItem.GetComponent<InventoryItem>();
        if (item != null)
        {
            Vector3 dropPosition = GetDropPosition();
            CreateDroppedItem(item, dropPosition);

            // Update player weight
            if (playerStatusController?.WeightManager != null)
            {
                playerStatusController.WeightManager.ConsumeWeight(item.totalWeight);
            }

            slot.heldItem = null;
            Object.Destroy(item.gameObject);
        }
    }

    private Vector3 GetDropPosition()
    {
        if (player != null)
            return player.transform.position + player.transform.forward * 2f;
        return Vector3.zero;
    }

    private void CreateDroppedItem(InventoryItem item, Vector3 position)
    {
        if (item?.itemScriptableObject?.Prefab == null) return;

        GameObject droppedItem = Object.Instantiate(item.itemScriptableObject.Prefab, position, Quaternion.identity);
        ItemPickable pickableComponent = droppedItem.GetComponent<ItemPickable>();

        if (pickableComponent != null)
        {
            pickableComponent.itemScriptableObject = item.itemScriptableObject;
            pickableComponent.quantity = item.stackCurrent;
            pickableComponent.DurabilityList = new List<int>(item.DurabilityList);
        }
    }

    private void UpdateGridLayout(int slotCount)
    {
        if (inventoryGridLayout == null)
        {
            Debug.LogError("Inventory Grid Layout is not assigned in SlotManager");
            return;
        }

        CalculateOptimalGridSettings(slotCount);
    }

    private void CalculateOptimalGridSettings(int slotCount)
    {
        RectTransform panelRect = inventorySlotsParent.GetComponent<RectTransform>();
        if (panelRect == null) return;

        // Handle edge case where no slots are needed
        slotCount = Mathf.Max(1, slotCount);

        float panelWidth = panelRect.rect.width;
        float panelHeight = panelRect.rect.height;

        // Calculate optimal grid dimensions
        float aspectRatio = panelWidth / panelHeight;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(slotCount * aspectRatio));
        int rows = Mathf.CeilToInt((float)slotCount / columns);

        // Ensure we don't exceed slot count
        while (columns * (rows - 1) >= slotCount && rows > 1)
        {
            rows--;
        }

        // Calculate cell size based on available space
        float availableWidth = panelWidth - (slotSpacing * (columns + 1));
        float availableHeight = panelHeight - (slotSpacing * (rows + 1));

        float cellWidth = availableWidth / columns;
        float cellHeight = availableHeight / rows;

        // Use the smaller dimension to maintain square slots
        float cellSize = Mathf.Min(cellWidth, cellHeight);
        cellSize = Mathf.Clamp(cellSize, minSlotSize, maxSlotSize);

        // Apply settings to grid layout
        inventoryGridLayout.cellSize = new Vector2(cellSize, cellSize);
        inventoryGridLayout.spacing = new Vector2(slotSpacing, slotSpacing);
        inventoryGridLayout.padding = new RectOffset(
            Mathf.RoundToInt(slotSpacing),
            Mathf.RoundToInt(slotSpacing),
            Mathf.RoundToInt(slotSpacing),
            Mathf.RoundToInt(slotSpacing)
        );

        // Set constraint based on calculated columns
        inventoryGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        inventoryGridLayout.constraintCount = columns;

        // Set child alignment
        inventoryGridLayout.childAlignment = TextAnchor.UpperLeft;
        inventoryGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        inventoryGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
    }

    // Public utility methods
    public void SetSlotSizeConstraints(float minSize, float maxSize, float spacing)
    {
        minSlotSize = minSize;
        maxSlotSize = maxSize;
        slotSpacing = spacing;
    }

    public void RefreshGridLayout(int slotCount)
    {
        UpdateGridLayout(slotCount);
    }
}