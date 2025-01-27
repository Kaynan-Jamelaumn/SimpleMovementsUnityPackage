using UnityEngine.InputSystem;
using UnityEngine;

public static class ItemHandler
{
    public static void PlaceItemInSlot(InventorySlot slot, GameObject draggedObject, PlayerStatusController playerStatusController)
    {
        slot.SetHeldItem(draggedObject);
        draggedObject.transform.SetParent(slot.transform.parent.parent.GetChild(2));

        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();
        if (slot.SlotType != SlotType.Common && slot.SlotType == (SlotType)draggedItem.itemScriptableObject.ItemType && !draggedItem.isEquipped)
        {
            draggedItem.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
            draggedItem.isEquipped = true;
        }
        else if (draggedItem.isEquipped && slot.SlotType == SlotType.Common && slot.SlotType != (SlotType)draggedItem.itemScriptableObject.ItemType)
        {
            draggedItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
            draggedItem.isEquipped = false;
        }
    }

    public static void SwitchOrFillStack(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        InventoryItem slotHeldItem = slot.heldItem.GetComponent<InventoryItem>();
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();

        if (slot.heldItem != null && (slotHeldItem.stackCurrent == slotHeldItem.stackMax || slotHeldItem.itemScriptableObject != draggedItem.itemScriptableObject))
        {
            SwitchItems(slot, draggedObject, lastItemSlotObject, playerStatusController);
        }
        else if (slotHeldItem.stackCurrent < slotHeldItem.stackMax && slotHeldItem.itemScriptableObject == draggedItem.itemScriptableObject)
        {
            FillStack(slot, slotHeldItem, draggedItem, lastItemSlotObject);
        }
    }

    public static void SwitchItems(InventorySlot slot, GameObject draggedObject, GameObject lastItemSlotObject, PlayerStatusController playerStatusController)
    {
        InventoryItem draggedItem = draggedObject.GetComponent<InventoryItem>();
        InventorySlot lastSlot = lastItemSlotObject.GetComponent<InventorySlot>();
        InventoryItem currentItem = slot.heldItem.GetComponent<InventoryItem>();

        if ((currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType && slot.SlotType != SlotType.Common) ||
            (lastSlot.SlotType != SlotType.Common && currentItem.itemScriptableObject.ItemType != draggedItem.itemScriptableObject.ItemType))
        {
            ReturnItemToLastSlot(lastItemSlotObject, draggedObject);
            return;
        }

        if (slot.SlotType != SlotType.Common || (lastSlot.SlotType != SlotType.Common && draggedItem.itemScriptableObject.ItemType == currentItem.itemScriptableObject.ItemType))
        {
            if (slot.SlotType != SlotType.Common)
            {
                currentItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
                currentItem.isEquipped = false;
                draggedItem.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
                draggedItem.isEquipped = true;
            }
            else
            {
                currentItem.itemScriptableObject.ApplyEquippedStats(true, playerStatusController);
                currentItem.isEquipped = true;
                draggedItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
                draggedItem.isEquipped = false;
            }
        }

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
            FillEntireStack(slot, slotHeldItem, draggedItem);
        }
        else
        {
            FillPartialStack(slot, slotHeldItem, draggedItem, itemsToFillStack, lastItemSlotObject);
        }
    }

    public static void FillEntireStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem)
    {
        slotHeldItem.stackCurrent += draggedItem.stackCurrent;
        slotHeldItem.DurabilityList.AddRange(draggedItem.DurabilityList);
        slotHeldItem.totalWeight += draggedItem.totalWeight;

        Object.Destroy(draggedItem.gameObject);
    }

    public static void FillPartialStack(InventorySlot slot, InventoryItem slotHeldItem, InventoryItem draggedItem, int itemsToFillStack, GameObject lastItemSlotObject)
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
            draggedItem.isEquipped = false;
            draggedItem.itemScriptableObject.ApplyEquippedStats(false, playerStatusController);
        }

        if (!cam) cam = Camera.main;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 position = ray.GetPoint(3);

        GameObject newItem = Object.Instantiate(draggedItem.itemScriptableObject.Prefab, position, Quaternion.identity);

        ItemPickable itemPickableComponent = newItem.GetComponent<ItemPickable>();
        itemPickableComponent.itemScriptableObject = draggedItem.itemScriptableObject;
        itemPickableComponent.quantity = draggedItem.stackCurrent;
        itemPickableComponent.DurabilityList = draggedItem.DurabilityList;
        itemPickableComponent.InteractionTime = draggedItem.itemScriptableObject.PickUpTime;

        player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(itemPickableComponent.itemScriptableObject.Weight * itemPickableComponent.quantity);

        lastSlot.heldItem = null;
        Object.Destroy(draggedObject);
    }
}
