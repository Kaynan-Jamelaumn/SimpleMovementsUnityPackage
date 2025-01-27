using UnityEngine.InputSystem;
using UnityEngine;

public static class ItemHandler
{
    public static void PlaceItemInSlot(InventorySlot slot, GameObject draggedObject, PlayerStatusController playerStatusController)
    {
        SetItemInSlot(slot, draggedObject);
        HandleItemEquipping(slot, draggedObject.GetComponent<InventoryItem>(), playerStatusController);
    }

    private static void SetItemInSlot(InventorySlot slot, GameObject draggedObject)
    {
        slot.SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(slot.transform.parent.parent.GetChild(2));
    }

    private static void HandleItemEquipping(InventorySlot slot, InventoryItem draggedItem, PlayerStatusController playerStatusController)
    {
        if (IsEquippable(slot, draggedItem))
        {
            EquipItem(draggedItem, playerStatusController);
        }
        else if (ShouldUnequip(slot, draggedItem))
        {
            UnequipItem(draggedItem, playerStatusController);
        }
    }

    private static bool IsEquippable(InventorySlot slot, InventoryItem item)
    {
        return slot.SlotType != SlotType.Common && slot.SlotType == (SlotType)item.itemScriptableObject.ItemType && !item.isEquipped;
    }

    private static bool ShouldUnequip(InventorySlot slot, InventoryItem item)
    {
        return item.isEquipped && slot.SlotType == SlotType.Common && slot.SlotType != (SlotType)item.itemScriptableObject.ItemType;
    }

    private static void EquipItem(InventoryItem item, PlayerStatusController playerStatusController)
    {
        item.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
        item.isEquipped = true;
    }

    private static void UnequipItem(InventoryItem item, PlayerStatusController playerStatusController)
    {
        item.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
        item.isEquipped = false;
    }

    public static void SwitchOrFillStack(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        InventoryItem slotHeldItem = slot.heldItem.GetComponent<InventoryItem>();
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (ShouldSwitchItems(slot, slotHeldItem, draggedItem))
        {
            SwitchItems(slot, draggedObject, lastItemSlotObject, playerStatusController);
        }
        else if (CanFillStack(slotHeldItem, draggedItem))
        {
            FillStack(slot, slotHeldItem, draggedItem, lastItemSlotObject);
        }
    }

    private static bool ShouldSwitchItems(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        return slot.heldItem != null && (slotHeldItem.stackCurrent == slotHeldItem.stackMax || slotHeldItem.itemScriptableObject != draggedItem.itemScriptableObject);
    }

    private static bool CanFillStack(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        return slotHeldItem.stackCurrent < slotHeldItem.stackMax && slotHeldItem.itemScriptableObject == draggedItem.itemScriptableObject;
    }

    public static void SwitchItems(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();
        InventorySlot lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        InventoryItem currentItem = slot.heldItem.GetComponent<InventoryItem>();

        if (CannotSwitchItems(slot, lastSlot, currentItem, draggedItem))
        {
            ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
            return;
        }

        UpdateEquippedStates(slot, lastSlot, currentItem, draggedItem, playerStatusController);
        SwapItems(slot, lastSlot, draggedObject);
    }

    private static bool CannotSwitchItems(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem)
    {
        return (currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType && slot.SlotType != SlotType.Common) ||
               (lastSlot.SlotType != SlotType.Common && currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType);
    }

    private static void UpdateEquippedStates(InventorySlot slot, InventorySlot lastSlot, InventoryItem currentItem, InventoryItem draggedItem, PlayerStatusController playerStatusController)
    {
        if (slot.SlotType != SlotType.Common)
        {
            UnequipItem(currentItem, playerStatusController);
            EquipItem(draggedItem, playerStatusController);
        }
        else if (lastSlot.SlotType != SlotType.Common)
        {
            EquipItem(currentItem, playerStatusController);
            UnequipItem(draggedItem, playerStatusController);
        }
    }

    private static void SwapItems(InventorySlot slot, InventorySlot lastSlot, GameObject draggedObject)
    {
        lastSlot.SetHeldItem(slot.heldItem);
        slot.heldItem.transform.SetParent(slot.transform.parent.parent.GetChild(2));

        slot.SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(slot.transform.parent.parent.GetChild(2));
    }

    public static void FillStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem, GameObject lastItemSlotObject)
    {
        int itemsToFillStack = slotHeldItem.stackMax - slotHeldItem.stackCurrent;

        if (itemsToFillStack >= draggedItem.stackCurrent)
        {
            FillEntireStack(slotHeldItem, draggedItem);
        }
        else
        {
            FillPartialStack(slotHeldItem, draggedItem, itemsToFillStack, lastItemSlotObject);
        }
    }

    private static void FillEntireStack(InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        slotHeldItem.stackCurrent += draggedItem.stackCurrent;
        slotHeldItem.DurabilityList.AddRange(draggedItem.DurabilityList);
        slotHeldItem.totalWeight += draggedItem.totalWeight;

        Object.Destroy(draggedItem.gameObject);
    }

    private static void FillPartialStack(InventoryItem slotHeldItem, InventoryItem draggedItem, int itemsToFillStack, GameObject lastItemSlotObject)
    {
        for (int j = 0; j < itemsToFillStack; j++)
        {
            slotHeldItem.DurabilityList.Add(draggedItem.DurabilityList[^1]);
            draggedItem.DurabilityList.RemoveAt(draggedItem.DurabilityList.Count - 1);
        }

        slotHeldItem.totalWeight += draggedItem.itemScriptableObject.Weight * itemsToFillStack;
        draggedItem.totalWeight -= draggedItem.itemScriptableObject.Weight * itemsToFillStack;

        slotHeldItem.stackCurrent += itemsToFillStack;
        draggedItem.stackCurrent -= itemsToFillStack;

        lastItemSlotObject.GetComponent<InventorySlot>().SetHeldItem(draggedItem.gameObject);
    }

    public static void ReturnItemToLastSlot(GameObject lastItemSlotObject, GameObject draggedObject)
    {
        lastItemSlotObject.GetComponent<InventorySlot>().SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(lastItemSlotObject.transform);
    }

    public static void DropItem(GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController, Camera cam, GameObject player)
    {
        InventorySlot lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (lastSlot.SlotType != SlotType.Common)
        {
            UnequipItem(draggedItem, playerStatusController);
        }

        Vector3 dropPosition = GetDropPosition(cam);
        CreateDroppedItem(draggedItem, dropPosition, player);

        lastSlot.heldItem = null;
        Object.Destroy(draggedObject);
    }

    private static Vector3 GetDropPosition(Camera cam)
    {
        if (!cam) cam = Camera.main;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        return ray.GetPoint(3);
    }

    private static void CreateDroppedItem(InventoryItem draggedItem, Vector3 position, GameObject player)
    {
        GameObject newItem = Object.Instantiate(draggedItem.itemScriptableObject.Prefab, position, Quaternion.identity);

        ItemPickable itemPickableComponent = newItem.GetComponent<ItemPickable>();
        itemPickableComponent.itemScriptableObject = draggedItem.itemScriptableObject;
        itemPickableComponent.quantity = draggedItem.stackCurrent;
        itemPickableComponent.DurabilityList = draggedItem.DurabilityList;
        itemPickableComponent.InteractionTime = draggedItem.itemScriptableObject.PickUpTime;

        player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(itemPickableComponent.itemScriptableObject.Weight * itemPickableComponent.quantity);
    }
}
