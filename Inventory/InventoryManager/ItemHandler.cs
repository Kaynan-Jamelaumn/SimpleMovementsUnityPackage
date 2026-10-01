using UnityEngine.InputSystem;
using UnityEngine;

/// <summary>
/// Moves items between slots (drop, swap, fill a stack) and into the world. It only MOVES items: after every move the
/// <see cref="EquipmentManager"/> syncs with the equipment slots, which equips what entered an equipment slot and
/// unequips what left one. (Equipping used to be done here by hand, with several paths that could apply an item twice
/// or never remove it, and casts from ItemType to SlotType that picked the wrong slot.)
/// </summary>
public static class ItemHandler
{
    /// <summary>Puts the dragged item into an empty slot (the caller checked that it fits).</summary>
    public static void PlaceItemInSlot(InventorySlot slot, GameObject draggedObject, PlayerStatusController playerStatusController)
    {
        if (slot == null || draggedObject == null)
        {
            Debug.LogError("[Inventory] Cannot place an item: missing slot or item.");
            return;
        }
        slot.SetHeldItem(draggedObject);
        ArmorEquipmentHandler.NotifyEquipmentChanged(playerStatusController);
    }

    /// <summary>Dropped onto an occupied slot: fills its stack when both are the same item, otherwise swaps.</summary>
    public static void SwitchOrFillStack(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        var slotHeldItem = slot.heldItem.GetComponent<InventoryItem>();
        var draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (CanStackItems(slotHeldItem, draggedItem))
            StackOperations.FillStack(slot, slotHeldItem, draggedItem, lastItemSlotObject);
        else
            SwitchItems(slot, draggedObject, lastItemSlotObject, playerStatusController);
        ArmorEquipmentHandler.NotifyEquipmentChanged(playerStatusController);
    }

    private static bool CanStackItems(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        return slotHeldItem != null && draggedItem != null &&
               slotHeldItem.itemScriptableObject == draggedItem.itemScriptableObject &&
               slotHeldItem.stackMax > 1 &&
               slotHeldItem.stackCurrent < slotHeldItem.stackMax;
    }

    /// <summary>Swaps the dragged item with the one in <paramref name="slot"/> when each fits the other's slot.</summary>
    public static void SwitchItems(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        var draggedItem = draggedObject.GetComponent<InventoryItem>();
        var lastSlot = lastItemSlotObject != null ? lastItemSlotObject.GetComponent<InventorySlot>() : null;
        var currentItem = slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;

        if (lastSlot == null || !CanSwitchItems(slot, lastSlot, currentItem, draggedItem))
        {
            ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
            return;
        }

        GameObject currentSlotItem = slot.heldItem;
        lastSlot.SetHeldItem(currentSlotItem);
        slot.SetHeldItem(draggedObject);
        ArmorEquipmentHandler.NotifyEquipmentChanged(playerStatusController);
    }

    private static bool CanSwitchItems(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem)
    {
        if (draggedItem == null || !SlotTypeHelper.CanPlace(draggedItem.itemScriptableObject, slot.SlotType))
            return false;
        return currentItem == null || SlotTypeHelper.CanPlace(currentItem.itemScriptableObject, lastSlot.SlotType);
    }

    // Utility methods
    public static void ReturnItemToLastSlot(GameObject lastItemSlotObject, GameObject draggedObject)
    {
        if (lastItemSlotObject == null || draggedObject == null)
            return;
        var lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        if (lastSlot != null)
        {
            lastSlot.SetHeldItem(draggedObject);
        }
        else
        {
            Debug.LogError("[Inventory] The item's previous slot has no InventorySlot component.", lastItemSlotObject);
            draggedObject.transform.SetParent(lastItemSlotObject.transform);
        }
    }

    /// <summary>Drops the dragged item into the world in front of the camera (its weight leaves the player).</summary>
    public static void DropItem(GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController, Camera cam, GameObject player)
    {
        var lastSlot = lastItemSlotObject != null ? lastItemSlotObject.GetComponent<InventorySlot>() : null;
        var draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (draggedItem == null || draggedItem.itemScriptableObject == null || draggedItem.itemScriptableObject.Prefab == null)
        {
            Debug.LogWarning("[Inventory] This item has no prefab and cannot be dropped; it went back to its slot.", draggedObject);
            ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
            return;
        }

        Vector3 dropPosition = GetDropPosition(cam, player);
        CreateDroppedItem(draggedItem, dropPosition, player);

        if (lastSlot != null && lastSlot.heldItem == draggedObject)
            lastSlot.heldItem = null;
        Object.Destroy(draggedObject);
        ArmorEquipmentHandler.NotifyEquipmentChanged(playerStatusController);
    }

    private static Vector3 GetDropPosition(Camera cam, GameObject player)
    {
        if (!cam) cam = Camera.main;
        if (cam == null)
            return player != null ? player.transform.position + player.transform.forward * 1.5f + Vector3.up : Vector3.zero;

        Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = cam.ScreenPointToRay(mousePosition);
        return ray.GetPoint(3);
    }

    private static void CreateDroppedItem(InventoryItem draggedItem, Vector3 position, GameObject player)
    {
        SpawnWorldItem(draggedItem.itemScriptableObject, draggedItem.stackCurrent, draggedItem.DurabilityList, position, player);
        InventoryUtils.UpdatePlayerWeight(player, -draggedItem.itemScriptableObject.Weight * draggedItem.stackCurrent);
    }

    /// <summary>
    /// Spawns an item in the world that the player can pick up again: an <see cref="ItemPickable"/> on the root, an
    /// enabled collider (a box fitted to the model when the prefab has none - the hand prefab of a weapon often has
    /// none), a layer the interaction raycast sees, and resting on the ground below <paramref name="position"/>.
    /// </summary>
    public static GameObject SpawnWorldItem(ItemSO item, int quantity, System.Collections.Generic.IList<int> durabilities, Vector3 position, GameObject player)
    {
        if (item == null || item.Prefab == null)
            return null;
        GameObject go = Object.Instantiate(item.Prefab, position, Quaternion.identity);
        go.name = item.Prefab.name;
        if (!go.activeSelf)
            go.SetActive(true);

        ItemPickable pickable = go.GetComponent<ItemPickable>();
        if (pickable == null)
            pickable = go.AddComponent<ItemPickable>();
        pickable.itemScriptableObject = item;
        pickable.quantity = Mathf.Max(1, quantity);
        pickable.DurabilityList = durabilities != null ? new System.Collections.Generic.List<int>(durabilities) : new System.Collections.Generic.List<int>();
        pickable.InteractionTime = item.PickUpTime;

        MakeInteractable(go);
        PlaceOnGround(go, player);
        return go;
    }

    private static void MakeInteractable(GameObject go)
    {
        // The interaction raycast skips the Ignore Raycast layer.
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            if (t.gameObject.layer == ignoreRaycast)
                t.gameObject.layer = 0;

        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        bool anyEnabled = false;
        foreach (Collider c in colliders)
            if (c.enabled && c.gameObject.activeInHierarchy) anyEnabled = true;
        if (anyEnabled)
            return;
        if (colliders.Length > 0)
        {
            foreach (Collider c in colliders)
                c.enabled = true;
            return;
        }

        // No collider at all: a box around the model.
        var box = go.AddComponent<BoxCollider>();
        if (TryGetRendererBounds(go, out Bounds b))
        {
            Vector3 scale = go.transform.lossyScale;
            box.center = go.transform.InverseTransformPoint(b.center);
            box.size = new Vector3(
                b.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                b.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                b.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }
        else
        {
            box.size = Vector3.one * 0.4f;
        }
    }

    private static void PlaceOnGround(GameObject go, GameObject player)
    {
        Vector3 origin = go.transform.position + Vector3.up * 0.5f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit hit in hits)
        {
            Transform t = hit.collider.transform;
            if (t.IsChildOf(go.transform) || (player != null && t.IsChildOf(player.transform)))
                continue;
            float bottom = TryGetRendererBounds(go, out Bounds b) ? go.transform.position.y - b.min.y : 0.2f;
            go.transform.position = hit.point + Vector3.up * (bottom + 0.05f);
            return;
        }
    }

    private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }
}
