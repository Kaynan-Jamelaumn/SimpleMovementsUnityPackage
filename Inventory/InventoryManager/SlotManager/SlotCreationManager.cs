using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SlotCreationManager
{
    [Header("Slot Parent Configuration")]
    [SerializeField, Tooltip("Parent transform for hotbar slots")]
    private Transform hotbarSlotsParent;

    [SerializeField, Tooltip("Parent transform for inventory slots")]
    private Transform inventorySlotsParent;

    [SerializeField, Tooltip("Parent transform for equipment slots")]
    private Transform equipmentSlotsParent;

    // Runtime slot prefab reference (set during initialization)
    [System.NonSerialized] private GameObject slotPrefab;

    // Slot arrays
    [System.NonSerialized] private GameObject[] hotbarSlots;
    [System.NonSerialized] private GameObject[] inventorySlots;

    // Dependencies
    [System.NonSerialized] private PlayerStatusController playerStatusController;
    [System.NonSerialized] private GameObject player;

    // Events
    [System.NonSerialized] public System.Action OnSlotsChanged;

    // Properties
    public GameObject[] HotbarSlots => hotbarSlots;
    public GameObject[] InventorySlots => inventorySlots;
    public Transform HotbarSlotsParent => hotbarSlotsParent;
    public Transform InventorySlotsParent => inventorySlotsParent;
    public Transform EquipmentSlotsParent => equipmentSlotsParent;
    public GameObject SlotPrefab => slotPrefab;

    public void Initialize(PlayerStatusController playerController, GameObject playerObject, GameObject prefab)
    {
        playerStatusController = playerController;
        player = playerObject;
        slotPrefab = prefab;
        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (hotbarSlotsParent == null)
            Debug.LogError("Hotbar Slots Parent is not assigned in SlotCreationManager");
        if (inventorySlotsParent == null)
            Debug.LogError("Inventory Slots Parent is not assigned in SlotCreationManager");
        if (slotPrefab == null)
            Debug.LogError("Slot Prefab is not assigned in SlotCreationManager");
    }

    // Slot Creation and Management
    public void CreateAllSlots(int hotbarSlotCount, int inventorySlotCount)
    {
        ClearAllExistingSlots();
        CreateHotbarSlots(hotbarSlotCount);
        CreateInventorySlots(inventorySlotCount);
        OnSlotsChanged?.Invoke();
    }

    public void UpdateHotbarSlots(int newSlotCount)
    {
        if (!Application.isPlaying) return;

        ClearSlotsFromParent(hotbarSlotsParent);
        CreateHotbarSlots(newSlotCount);
        OnSlotsChanged?.Invoke();
    }

    public void UpdateInventorySlots(int newSlotCount)
    {
        if (!Application.isPlaying) return;

        ClearSlotsFromParent(inventorySlotsParent);
        CreateInventorySlots(newSlotCount);
        OnSlotsChanged?.Invoke();
    }

    private void CreateHotbarSlots(int slotCount)
    {
        slotCount = Mathf.Max(0, slotCount);
        hotbarSlots = new GameObject[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            hotbarSlots[i] = CreateSlot(hotbarSlotsParent, SlotType.Common, $"HotbarSlot_{i}", true);
        }
    }

    private void CreateInventorySlots(int slotCount)
    {
        slotCount = Mathf.Max(0, slotCount);
        inventorySlots = new GameObject[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            inventorySlots[i] = CreateSlot(inventorySlotsParent, SlotType.Common, $"InventorySlot_{i}", false);
        }
    }

    private GameObject CreateSlot(Transform parent, SlotType slotType, string slotName, bool isHotbarSlot = false)
    {
        if (slotPrefab == null)
        {
            Debug.LogError($"Cannot create slot '{slotName}': Slot Prefab is null. Make sure the slot prefab is assigned in the SlotManager inspector.");
            return null;
        }

        if (parent == null)
        {
            Debug.LogError($"Cannot create slot '{slotName}': Parent transform is null. Check that the parent transforms are assigned in the SlotManager inspector.");
            return null;
        }

        GameObject newSlot = Object.Instantiate(slotPrefab, parent);
        newSlot.name = slotName;

        // Setup slot component
        InventorySlot slotComponent = newSlot.GetComponent<InventorySlot>();
        if (slotComponent == null)
            slotComponent = newSlot.AddComponent<InventorySlot>();

        slotComponent.SlotType = slotType;
        slotComponent.SetAsHotbarSlot(isHotbarSlot);

        EnsureSlotUIComponents(newSlot);

        // Validate the slot setup
        if (!slotComponent.ValidateSlotSetup())
        {
            Debug.LogWarning($"Slot setup validation failed for {slotName}");
        }

        return newSlot;
    }

    private void EnsureSlotUIComponents(GameObject slot)
    {
        // Ensure Image component exists for UI functionality
        Image slotImage = slot.GetComponent<Image>();
        if (slotImage == null)
            slotImage = slot.AddComponent<Image>();

        slotImage.raycastTarget = true;

        // Ensure RectTransform exists (required for UI)
        RectTransform rectTransform = slot.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError($"Slot {slot.name} is missing RectTransform component");
        }

        // Add CanvasGroup for fade effects (optional)
        if (slot.GetComponent<CanvasGroup>() == null)
        {
            slot.AddComponent<CanvasGroup>();
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

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null)
            {
                // Skip shared containers
                if (child.name.Contains("ItemsContainer")) continue;

                // Handle item cleanup before destroying slot
                InventorySlot slotComponent = child.GetComponent<InventorySlot>();
                if (slotComponent?.heldItem != null)
                {
                    DropItemFromSlot(slotComponent);
                }

                DestroySlot(child.gameObject);
            }
        }
    }

    private void DestroySlot(GameObject slot)
    {
        if (Application.isPlaying)
            Object.Destroy(slot);
        else
            Object.DestroyImmediate(slot);
    }

    private void DropItemFromSlot(InventorySlot slot)
    {
        if (slot?.heldItem == null) return;

        InventoryItem item = slot.heldItem.GetComponent<InventoryItem>();
        if (item != null)
        {
            Vector3 dropPosition = GetDropPosition();
            CreateDroppedItem(item, dropPosition);

            // Update weight manager
            if (playerStatusController?.WeightManager != null)
            {
                playerStatusController.WeightManager.ConsumeWeight(item.totalWeight);
            }

            slot.ClearSlot();
        }
    }

    private Vector3 GetDropPosition()
    {
        return player != null ? player.transform.position + player.transform.forward * 2f : Vector3.zero;
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

    public void RefreshAllItemPositions()
    {
        RefreshItemPositionsInSlots(hotbarSlots);
        RefreshItemPositionsInSlots(inventorySlots);
    }

    private void RefreshItemPositionsInSlots(GameObject[] slots)
    {
        if (slots == null) return;

        foreach (var slotObj in slots)
        {
            var slot = slotObj?.GetComponent<InventorySlot>();
            slot?.RefreshItemPosition();
        }
    }

    // Equipment slot management (if needed)
    public GameObject CreateEquipmentSlot(SlotType equipmentType, string slotName)
    {
        if (equipmentSlotsParent == null)
        {
            Debug.LogWarning("Equipment slots parent is not assigned");
            return null;
        }

        return CreateSlot(equipmentSlotsParent, equipmentType, slotName, false);
    }

    // Utility methods
    public int GetTotalSlotCount()
    {
        int total = 0;
        if (hotbarSlots != null) total += hotbarSlots.Length;
        if (inventorySlots != null) total += inventorySlots.Length;
        return total;
    }

    public int GetActiveSlotCount()
    {
        int active = 0;

        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                if (slot != null && slot.activeInHierarchy)
                    active++;
            }
        }

        if (inventorySlots != null)
        {
            foreach (var slot in inventorySlots)
            {
                if (slot != null && slot.activeInHierarchy)
                    active++;
            }
        }

        return active;
    }

    public void SetSlotParents(Transform hotbarParent, Transform inventoryParent, Transform equipmentParent = null)
    {
        hotbarSlotsParent = hotbarParent;
        inventorySlotsParent = inventoryParent;
        equipmentSlotsParent = equipmentParent;
    }

    // Validation and diagnostics
    public bool ValidateSlotIntegrity()
    {
        bool isValid = true;

        if (hotbarSlots != null)
        {
            for (int i = 0; i < hotbarSlots.Length; i++)
            {
                if (hotbarSlots[i] == null)
                {
                    Debug.LogWarning($"Hotbar slot {i} is null");
                    isValid = false;
                }
                else if (hotbarSlots[i].GetComponent<InventorySlot>() == null)
                {
                    Debug.LogWarning($"Hotbar slot {i} is missing InventorySlot component");
                    isValid = false;
                }
            }
        }

        if (inventorySlots != null)
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] == null)
                {
                    Debug.LogWarning($"Inventory slot {i} is null");
                    isValid = false;
                }
                else if (inventorySlots[i].GetComponent<InventorySlot>() == null)
                {
                    Debug.LogWarning($"Inventory slot {i} is missing InventorySlot component");
                    isValid = false;
                }
            }
        }

        return isValid;
    }

    public void LogSlotCreationStatus()
    {
        Debug.Log($"Slot Creation Status:");
        Debug.Log($"- Hotbar Slots: {hotbarSlots?.Length ?? 0}");
        Debug.Log($"- Inventory Slots: {inventorySlots?.Length ?? 0}");
        Debug.Log($"- Total Active Slots: {GetActiveSlotCount()}");
        Debug.Log($"- Slot Prefab: {(slotPrefab != null ? slotPrefab.name : "NULL")}");
        Debug.Log($"- Hotbar Parent: {(hotbarSlotsParent != null ? hotbarSlotsParent.name : "NULL")}");
        Debug.Log($"- Inventory Parent: {(inventorySlotsParent != null ? inventorySlotsParent.name : "NULL")}");
        Debug.Log($"- Equipment Parent: {(equipmentSlotsParent != null ? equipmentSlotsParent.name : "NULL")}");
    }
}