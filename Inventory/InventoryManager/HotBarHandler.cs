using UnityEngine;
using UnityEngine.InputSystem;

public static class HotbarHandler
{
    // Constants
    private const float SELECTED_SLOT_SCALE = 1.25f;
    private const float NORMAL_SLOT_SCALE = 1f;
    private const int MAX_HOTBAR_SLOTS = 8;

    // State
    private static int selectedHotbarSlot = 0;
    private static int lastSelectedSlot = -1;
    private static Keyboard keyboard;

    // Cached components for performance
    private static GameObject currentHandItem;

    // Properties
    public static int SelectedHotbarSlot => selectedHotbarSlot;

    // Initialize the keyboard reference
    static HotbarHandler()
    {
        keyboard = Keyboard.current;
    }

    // Main input checking method with optimized key detection
    public static void CheckForHotbarInput(GameObject[] hotbarSlots, Transform handParent)
    {
        if (keyboard == null || hotbarSlots == null) return;

        int newSelectedSlot = GetPressedSlotIndex();

        if (newSelectedSlot != -1 && newSelectedSlot != selectedHotbarSlot)
        {
            selectedHotbarSlot = newSelectedSlot;
            HotbarItemChanged(hotbarSlots, handParent);
        }
    }

    // Optimized key detection
    private static int GetPressedSlotIndex()
    {
        if (keyboard.digit1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame) return 2;
        if (keyboard.digit4Key.wasPressedThisFrame) return 3;
        if (keyboard.digit5Key.wasPressedThisFrame) return 4;
        return -1;
    }

    // Optimized hotbar update - only update when selection changes
    public static void HotbarItemChanged(GameObject[] hotbarSlots, Transform handParent)
    {
        if (hotbarSlots == null || handParent == null) return;

        // Only update if selection actually changed
        if (lastSelectedSlot == selectedHotbarSlot) return;

        UpdateSlotVisuals(hotbarSlots);
        UpdateHandItem(hotbarSlots, handParent);

        lastSelectedSlot = selectedHotbarSlot;
    }

    // Update visual states of hotbar slots
    private static void UpdateSlotVisuals(GameObject[] hotbarSlots)
    {
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] == null) continue;

            float scale = (i == selectedHotbarSlot) ? SELECTED_SLOT_SCALE : NORMAL_SLOT_SCALE;
            hotbarSlots[i].transform.localScale = Vector3.one * scale;
        }
    }

    // Handle hand item instantiation and cleanup
    private static void UpdateHandItem(GameObject[] hotbarSlots, Transform handParent)
    {
        // Clean up current hand item
        ClearHandItems(handParent);

        // Get selected slot with safety checks
        if (hotbarSlots == null || selectedHotbarSlot >= hotbarSlots.Length) return;

        var slotObject = hotbarSlots[selectedHotbarSlot];
        if (slotObject == null) return;

        var selectedSlot = slotObject.GetComponent<InventorySlot>();
        if (selectedSlot == null) return;

        // Check if heldItem exists and is properly assigned
        if (selectedSlot.heldItem == null) return;

        var heldItem = selectedSlot.heldItem.GetComponent<InventoryItem>();
        if (heldItem?.itemScriptableObject?.Prefab != null)
        {
            InstantiateHandItem(heldItem, handParent);
        }
    }

    // Efficiently clear all hand items
    private static void ClearHandItems(Transform handParent)
    {
        if (handParent == null) return;

        // Destroy in reverse order for performance
        for (int i = handParent.childCount - 1; i >= 0; i--)
        {
            var child = handParent.GetChild(i);
            if (child != null)
                Object.Destroy(child.gameObject);
        }
    }

    // Create and configure hand item
    private static void InstantiateHandItem(InventoryItem inventoryItem, Transform handParent)
    {
        try
        {
            var itemSO = inventoryItem.itemScriptableObject;
            var newItem = Object.Instantiate(itemSO.Prefab, handParent);

            ConfigureHandItem(newItem, itemSO);
            currentHandItem = newItem;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error instantiating hand item: {e.Message}");
        }
    }

    // Configure the instantiated hand item
    private static void ConfigureHandItem(GameObject handItem, ItemSO itemSO)
    {
        // Remove physics components that aren't needed in hand
        RemoveComponent<Rigidbody>(handItem);
        RemoveComponent<ItemPickable>(handItem);
        RemoveComponent<Collider>(handItem);

        // Set transform properties
        var transform = handItem.transform;
        transform.localPosition = itemSO.Position;
        transform.localRotation = Quaternion.Euler(itemSO.Rotation);
        transform.localScale = itemSO.Scale;

        // Ensure proper layer (optional, for rendering order)
        SetLayerRecursively(handItem, LayerMask.NameToLayer("Default"));
    }

    // Utility method to remove components safely
    private static void RemoveComponent<T>(GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        if (component != null)
            Object.Destroy(component);
    }

    // Utility method to set layer recursively
    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // Get currently held item in hotbar
    public static InventoryItem GetCurrentHeldItem(GameObject[] hotbarSlots)
    {
        if (hotbarSlots == null || selectedHotbarSlot >= hotbarSlots.Length)
            return null;

        var slot = hotbarSlots[selectedHotbarSlot]?.GetComponent<InventorySlot>();
        return slot?.heldItem?.GetComponent<InventoryItem>();
    }

    // Check if a specific slot has an item
    public static bool HasItemInSlot(GameObject[] hotbarSlots, int slotIndex)
    {
        if (hotbarSlots == null || slotIndex < 0 || slotIndex >= hotbarSlots.Length)
            return false;

        var slot = hotbarSlots[slotIndex]?.GetComponent<InventorySlot>();
        return slot?.heldItem != null;
    }

    // Get item from specific slot
    public static InventoryItem GetItemFromSlot(GameObject[] hotbarSlots, int slotIndex)
    {
        if (!HasItemInSlot(hotbarSlots, slotIndex)) return null;

        var slot = hotbarSlots[slotIndex].GetComponent<InventorySlot>();
        return slot.heldItem.GetComponent<InventoryItem>();
    }

    // Select specific slot programmatically
    public static void SelectSlot(int slotIndex, GameObject[] hotbarSlots, Transform handParent)
    {
        if (slotIndex < 0 || slotIndex >= MAX_HOTBAR_SLOTS) return;

        selectedHotbarSlot = slotIndex;
        HotbarItemChanged(hotbarSlots, handParent);
    }

    // Get next/previous slot with wrapping
    public static void SelectNextSlot(GameObject[] hotbarSlots, Transform handParent)
    {
        int nextSlot = (selectedHotbarSlot + 1) % MAX_HOTBAR_SLOTS;
        SelectSlot(nextSlot, hotbarSlots, handParent);
    }

    public static void SelectPreviousSlot(GameObject[] hotbarSlots, Transform handParent)
    {
        int prevSlot = (selectedHotbarSlot - 1 + MAX_HOTBAR_SLOTS) % MAX_HOTBAR_SLOTS;
        SelectSlot(prevSlot, hotbarSlots, handParent);
    }

    // Force refresh the hotbar display
    public static void ForceRefresh(GameObject[] hotbarSlots, Transform handParent)
    {
        lastSelectedSlot = -1; // Force update
        HotbarItemChanged(hotbarSlots, handParent);
    }

    // Cleanup method for when hotbar is disabled
    public static void Cleanup(Transform handParent)
    {
        ClearHandItems(handParent);
        currentHandItem = null;
        lastSelectedSlot = -1;
    }
}