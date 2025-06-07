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
    private GameObject slotPrefab;

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
            // Debug.LogWarning($"Slot setup validation failed for {slotName}");
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
}