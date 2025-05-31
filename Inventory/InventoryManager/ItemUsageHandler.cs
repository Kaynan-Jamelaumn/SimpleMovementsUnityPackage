using UnityEngine;

public static class ItemUsageHandler
{
    public static InventoryItem GetHeldItem(InventorySlot selectedSlot)
    {
        if (selectedSlot?.heldItem == null)
        {
            Debug.LogWarning("Selected slot or held item is null");
            return null;
        }

        if (selectedSlot.heldItem.TryGetComponent<InventoryItem>(out var heldItemComponent))
        {
            return heldItemComponent;
        }

        Debug.LogWarning($"InventoryItem component not found on {selectedSlot.heldItem.name}");
        return null;
    }

    public static bool HandleCooldown(InventoryItem heldItem)
    {
        if (heldItem?.itemScriptableObject == null)
        {
            Debug.LogWarning("Held item or its scriptable object is null");
            return false;
        }

        if (Time.time < heldItem.timeSinceLastUse)
        {
            Debug.Log($"Item {heldItem.itemScriptableObject.Name} is on cooldown");
            return false;
        }

        var cooldown = heldItem.itemScriptableObject.Cooldown;
        if (cooldown > 0)
        {
            heldItem.timeSinceLastUse = Time.time + cooldown;
        }

        return true;
    }

    public static bool UseHeldItem(GameObject player, PlayerStatusController statusController, WeaponController weaponController, InventoryItem heldItem)
    {
        if (!ValidateUsageParameters(player, statusController, heldItem))
        {
            return false;
        }

        try
        {
            if (heldItem.itemScriptableObject is WeaponSO weaponSO)
            {
                if (weaponController != null)
                {
                    weaponSO.UseItem(player, statusController, weaponController, AttackType.Normal, heldItem);
                }
                else
                {
                    Debug.LogWarning("WeaponController is null but trying to use weapon");
                    return false;
                }
            }
            else
            {
                // For non-weapon items, use the standard method
                heldItem.itemScriptableObject.UseItem(player, statusController);
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error using item {heldItem.itemScriptableObject.Name}: {e.Message}");
            return false;
        }
    }

    private static bool ValidateUsageParameters(GameObject player, PlayerStatusController statusController, InventoryItem heldItem)
    {
        if (player == null)
        {
            Debug.LogError("Player is null");
            return false;
        }

        if (statusController == null)
        {
            Debug.LogError("PlayerStatusController is null");
            return false;
        }

        if (heldItem?.itemScriptableObject == null)
        {
            Debug.LogError("Held item or its scriptable object is null");
            return false;
        }

        return true;
    }

    public static void HandleItemDurabilityAndStack(GameObject player, Transform handParent, InventorySlot selectedSlot, InventoryItem heldItem)
    {
        if (!ValidateItemHandlingParameters(player, handParent, selectedSlot, heldItem))
        {
            return;
        }

        try
        {
            if (ShouldProcessDurability(heldItem))
            {
                ProcessItemDurability(player, heldItem);

                if (heldItem.stackCurrent <= 0)
                {
                    DestroyHeldItem(player, handParent, selectedSlot, heldItem);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error handling item durability: {e.Message}");
        }
    }

    private static bool ValidateItemHandlingParameters(GameObject player, Transform handParent, InventorySlot selectedSlot, InventoryItem heldItem)
    {
        return player != null && handParent != null && selectedSlot != null && heldItem?.itemScriptableObject != null;
    }

    private static bool ShouldProcessDurability(InventoryItem heldItem)
    {
        // Weapons now handle their own durability in the WeaponSO.UseItem method
        // Only process durability here for consumables and other non-weapon items
        return heldItem.itemScriptableObject is ConsumableSO ||
               (!(heldItem.itemScriptableObject is WeaponSO) && heldItem.itemScriptableObject.ShouldBeDestroyedOn0UsesLeft);
    }

    private static void ProcessItemDurability(GameObject player, InventoryItem heldItem)
    {
        if (heldItem.durability > 0)
        {
            heldItem.durability -= heldItem.itemScriptableObject.DurabilityReductionPerUse;

            if (heldItem.durability <= 0)
            {
                DecreaseItemStack(player, heldItem);

                if (heldItem.DurabilityList?.Count > 0)
                {
                    UpdateItemDurability(heldItem);
                }
                else if (heldItem.stackCurrent <= 0)
                {
                    heldItem.durability = 0;
                }
            }
        }
    }

    private static void UpdateItemDurability(InventoryItem heldItem)
    {
        if (heldItem?.DurabilityList?.Count > 0)
        {
            heldItem.durability = heldItem.DurabilityList[^1];
            heldItem.DurabilityList.RemoveAt(heldItem.DurabilityList.Count - 1);
        }
    }

    private static void DecreaseItemStack(GameObject player, InventoryItem heldItem)
    {
        if (player == null || heldItem?.itemScriptableObject == null) return;

        heldItem.stackCurrent--;

        var playerStatus = player.GetComponent<PlayerStatusController>();
        if (playerStatus?.WeightManager != null)
        {
            playerStatus.WeightManager.ConsumeWeight(heldItem.itemScriptableObject.Weight);
        }
    }

    private static void DestroyHeldItem(GameObject player, Transform handParent, InventorySlot selectedSlot, InventoryItem heldItem)
    {
        if (selectedSlot != null)
        {
            selectedSlot.heldItem = null;
        }

        if (heldItem?.gameObject != null)
        {
            Object.Destroy(heldItem.gameObject);
        }

        if (handParent != null)
        {
            for (int i = handParent.childCount - 1; i >= 0; i--)
            {
                var child = handParent.GetChild(i);
                if (child?.gameObject != null)
                {
                    Object.Destroy(child.gameObject);
                }
            }
        }
    }
}