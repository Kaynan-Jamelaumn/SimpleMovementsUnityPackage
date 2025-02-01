using UnityEngine;

public static class ItemUsageHandler
{

    public static InventoryItem GetHeldItem(InventorySlot selectedSlot)
    {
        if (selectedSlot == null || selectedSlot.heldItem == null)
        {
            return null;
        }

        InventoryItem heldItemComponent;
        if (selectedSlot.heldItem.TryGetComponent(out heldItemComponent))
        {
            return heldItemComponent;
        }
        else
        {
            return null;
        }
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
        // Reduce durability if it exists and is greater than zero
        if (heldItem.durability > 0)
        {
            heldItem.durability -= heldItem.itemScriptableObject.DurabilityReductionPerUse;

            // If durability reaches zero or below, update stack and durability
            if (heldItem.durability <= 0)
            {
                DecreaseItemStack(player, heldItem);

                if (heldItem.DurabilityList.Count > 0)
                {
                    UpdateItemDurability(heldItem);
                }
                else if (heldItem.stackCurrent <= 0)
                {
                    heldItem.durability = 0; // Prevents negative durability display
                }
            }
        }
    }

    private static void UpdateItemDurability(InventoryItem heldItem)
    {
        if (heldItem.DurabilityList.Count > 0)
        {
            heldItem.durability = heldItem.DurabilityList[^1];
            heldItem.DurabilityList.RemoveAt(heldItem.DurabilityList.Count - 1);
        }
    }

    private static void DecreaseItemStack(GameObject player, InventoryItem heldItem)
    {
        heldItem.stackCurrent--;
        player.GetComponent<PlayerStatusController>().WeightManager.ConsumeWeight(heldItem.itemScriptableObject.Weight);
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
