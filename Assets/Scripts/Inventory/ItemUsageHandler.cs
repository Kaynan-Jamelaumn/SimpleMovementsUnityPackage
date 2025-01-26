using UnityEngine;

public static class ItemUsageHandler
{
    public static InventoryItem GetHeldItem(InventorySlot selectedSlot)
    {
        return selectedSlot.heldItem?.GetComponent<InventoryItem>();
    }

    public static bool HandleCooldown(InventoryItem heldItem)
    {
        if (Time.time < heldItem.timeSinceLastUse) return false;

        var cooldown = heldItem.itemScriptableObject.Cooldown;
        if (cooldown != 0)
        {
            heldItem.timeSinceLastUse = Time.time + cooldown;
        }
        return true;
    }

    public static void UseHeldItem(GameObject player, PlayerStatusController statusController, WeaponController weaponController, InventoryItem heldItem)
    {
        if (heldItem.itemScriptableObject is WeaponSO)
        {
            heldItem.itemScriptableObject.UseItem(player, statusController, weaponController);
        }
        else
        {
            heldItem.itemScriptableObject.UseItem(player, statusController);
        }
    }

    public static void HandleItemDurabilityAndStack(GameObject player, Transform handParent, InventorySlot selectedSlot, InventoryItem heldItem)
    {
        if (heldItem.itemScriptableObject is ConsumableSO || heldItem.itemScriptableObject.ShouldBeDestroyedOn0UsesLeft)
        {
            ProcessItemDurability(player, heldItem);

            if (heldItem.stackCurrent <= 0)
            {
                DestroyHeldItem(player, handParent, selectedSlot, heldItem);
            }
        }
    }

    private static void ProcessItemDurability(GameObject player, InventoryItem heldItem)
    {
        if (heldItem.DurabilityList.Count > 0 && heldItem.DurabilityList[^1] <= 0)
        {
            DecreaseItemStack(player, heldItem);
            UpdateItemDurability(heldItem);
        }
    }

    private static void DecreaseItemStack(GameObject player, InventoryItem heldItem)
    {
        heldItem.stackCurrent--;
        player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(heldItem.itemScriptableObject.Weight);
    }

    private static void UpdateItemDurability(InventoryItem heldItem)
    {
        heldItem.durability = heldItem.DurabilityList[^1];
        heldItem.DurabilityList.RemoveAt(heldItem.DurabilityList.Count - 1);
    }

    private static void DestroyHeldItem(GameObject player, Transform handParent, InventorySlot selectedSlot, InventoryItem heldItem)
    {
        selectedSlot.heldItem = null;
        Object.Destroy(heldItem.gameObject);

        for (int i = handParent.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(handParent.GetChild(i).gameObject);
        }
    }
}
