using UnityEngine;
using UnityEngine.InputSystem;

public static class HotbarHandler
{
    // Constants
    private const float SELECTED_SLOT_SCALE = 1.25f;
    private const float NORMAL_SLOT_SCALE = 1f;
    private const int MAX_HOTBAR_SLOTS = 9;

    // State (static: one player's hotbar)
    private static int selectedHotbarSlot = 0;
    private static int lastSelectedSlot = -1;
    private static GameObject lastHeldItem;

    // Cached components for performance
    private static GameObject currentHandItem;

    // Properties
    public static int SelectedHotbarSlot => selectedHotbarSlot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        selectedHotbarSlot = 0;
        lastSelectedSlot = -1;
        lastHeldItem = null;
        currentHandItem = null;
    }

    /// <summary>Number keys 1-9 select a hotbar slot. Returns true when the selection changed.</summary>
    public static bool CheckForHotbarInput(GameObject[] hotbarSlots, Transform handParent)
    {
        if (hotbarSlots == null || hotbarSlots.Length == 0) return false;

        int newSelectedSlot = GetPressedSlotIndex(hotbarSlots.Length);
        if (newSelectedSlot == -1 || newSelectedSlot == selectedHotbarSlot) return false;

        selectedHotbarSlot = newSelectedSlot;
        HotbarItemChanged(hotbarSlots, handParent);
        return true;
    }

    // Keyboard.current is read every time (it used to be cached once and could stay null forever).
    private static int GetPressedSlotIndex(int slotCount)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) return Valid(0, slotCount);
            if (keyboard.digit2Key.wasPressedThisFrame) return Valid(1, slotCount);
            if (keyboard.digit3Key.wasPressedThisFrame) return Valid(2, slotCount);
            if (keyboard.digit4Key.wasPressedThisFrame) return Valid(3, slotCount);
            if (keyboard.digit5Key.wasPressedThisFrame) return Valid(4, slotCount);
            if (keyboard.digit6Key.wasPressedThisFrame) return Valid(5, slotCount);
            if (keyboard.digit7Key.wasPressedThisFrame) return Valid(6, slotCount);
            if (keyboard.digit8Key.wasPressedThisFrame) return Valid(7, slotCount);
            if (keyboard.digit9Key.wasPressedThisFrame) return Valid(8, slotCount);
        }
        return -1;
    }

    private static int Valid(int index, int count) => index < count ? index : -1;

    // Optimized hotbar update - only update when selection changes
    public static void HotbarItemChanged(GameObject[] hotbarSlots, Transform handParent)
    {
        if (hotbarSlots == null || handParent == null) return;

        // Only update if selection actually changed
        if (lastSelectedSlot == selectedHotbarSlot) return;

        UpdateSlotVisuals(hotbarSlots);
        UpdateHandItem(hotbarSlots, handParent);

        lastSelectedSlot = selectedHotbarSlot;
        lastHeldItem = CurrentSlotItem(hotbarSlots);
    }

    /// <summary>Rebuilds the hand model when the item in the selected slot changed (dragged in or out, used up).</summary>
    public static void RefreshIfItemChanged(GameObject[] hotbarSlots, Transform handParent)
    {
        if (hotbarSlots == null || handParent == null) return;
        if (lastSelectedSlot != selectedHotbarSlot || lastHeldItem != CurrentSlotItem(hotbarSlots))
            ForceRefresh(hotbarSlots, handParent);
    }

    private static GameObject CurrentSlotItem(GameObject[] hotbarSlots)
    {
        if (hotbarSlots == null || selectedHotbarSlot >= hotbarSlots.Length || hotbarSlots[selectedHotbarSlot] == null) return null;
        var slot = hotbarSlots[selectedHotbarSlot].GetComponent<InventorySlot>();
        return slot != null ? slot.heldItem : null;
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

        InventoryItem heldItem = CurrentSlotInventoryItem(hotbarSlots);

        // Equipment Visuals show (and sheathe) the item in hand themselves: one model, moved between hand and sheath.
        EquipmentVisuals visuals = EquipmentVisuals.For(handParent);
        if (visuals != null && visuals.isActiveAndEnabled)
        {
            visuals.SetMainHandSocket(handParent);
            visuals.SetMainHandItem(heldItem);
            return;
        }

        if (heldItem.Live()?.itemScriptableObject.Live()?.Prefab != null)
        {
            InstantiateHandItem(heldItem, handParent);
        }
    }

    private static InventoryItem CurrentSlotInventoryItem(GameObject[] hotbarSlots)
    {
        if (hotbarSlots == null || selectedHotbarSlot >= hotbarSlots.Length) return null;
        var slotObject = hotbarSlots[selectedHotbarSlot];
        if (slotObject == null) return null;
        var selectedSlot = slotObject.GetComponent<InventorySlot>();
        if (selectedSlot == null || selectedSlot.heldItem == null) return null;
        return selectedSlot.heldItem.GetComponent<InventoryItem>();
    }

    /// <summary>
    /// Removes the models the hotbar put in the hand. Only those: the hand bone's own children (fingers, sockets) used to
    /// be destroyed with them when the hand parent was the hand bone itself.
    /// </summary>
    public static void ClearHand(Transform handParent) => ClearHandItems(handParent);

    private static void ClearHandItems(Transform handParent)
    {
        if (handParent == null) return;

        for (int i = handParent.childCount - 1; i >= 0; i--)
        {
            var child = handParent.GetChild(i);
            if (child != null && child.GetComponent<HeldItemModel>() != null)
                Object.Destroy(child.gameObject);
        }
        if (currentHandItem != null && currentHandItem.transform.parent == handParent)
            Object.Destroy(currentHandItem);
        currentHandItem = null;
    }

    // Create and configure hand item
    private static void InstantiateHandItem(InventoryItem inventoryItem, Transform handParent)
    {
        try
        {
            var itemSO = inventoryItem.itemScriptableObject;
            GameObject model = itemSO.Visuals.model != null ? itemSO.Visuals.model : itemSO.Prefab;
            var newItem = Object.Instantiate(model, handParent);
            newItem.AddComponent<HeldItemModel>();

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

        var slot = hotbarSlots[selectedHotbarSlot].Live()?.GetComponent<InventorySlot>();
        return slot.Live()?.heldItem.Live()?.GetComponent<InventoryItem>();
    }

    // Check if a specific slot has an item
    public static bool HasItemInSlot(GameObject[] hotbarSlots, int slotIndex)
    {
        if (hotbarSlots == null || slotIndex < 0 || slotIndex >= hotbarSlots.Length)
            return false;

        var slot = hotbarSlots[slotIndex].Live()?.GetComponent<InventorySlot>();
        return slot.Live()?.heldItem != null;
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
        int count = hotbarSlots != null ? Mathf.Min(hotbarSlots.Length, MAX_HOTBAR_SLOTS) : MAX_HOTBAR_SLOTS;
        if (slotIndex < 0 || slotIndex >= count) return;

        selectedHotbarSlot = slotIndex;
        HotbarItemChanged(hotbarSlots, handParent);
    }

    // Get next/previous slot with wrapping
    public static void SelectNextSlot(GameObject[] hotbarSlots, Transform handParent)
    {
        int count = hotbarSlots != null && hotbarSlots.Length > 0 ? Mathf.Min(hotbarSlots.Length, MAX_HOTBAR_SLOTS) : MAX_HOTBAR_SLOTS;
        int nextSlot = (selectedHotbarSlot + 1) % count;
        SelectSlot(nextSlot, hotbarSlots, handParent);
    }

    public static void SelectPreviousSlot(GameObject[] hotbarSlots, Transform handParent)
    {
        int count = hotbarSlots != null && hotbarSlots.Length > 0 ? Mathf.Min(hotbarSlots.Length, MAX_HOTBAR_SLOTS) : MAX_HOTBAR_SLOTS;
        int prevSlot = (selectedHotbarSlot - 1 + count) % count;
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