using UnityEngine;

public static class ItemUsageHandler
{
    public static InventoryItem GetHeldItem(InventorySlot selectedSlot)
    {
        if (selectedSlot.Live()?.heldItem == null)
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

    public static bool HandleCooldown(InventoryItem heldItem) => HandleCooldown(heldItem, null);

    /// <summary>Checks and starts the item's cooldown; <paramref name="owner"/>'s Cooldown Reduction (items) shortens it.</summary>
    public static bool HandleCooldown(InventoryItem heldItem, Component owner)
    {
        if (heldItem.Live()?.itemScriptableObject == null)
        {
            Debug.LogWarning("Held item or its scriptable object is null");
            return false;
        }

        if (Time.time < heldItem.timeSinceLastUse)
        {
            Debug.Log($"Item {heldItem.itemScriptableObject.Name} is on cooldown");
            return false;
        }

        float cooldown = owner != null ? CombatStats.ItemCooldown(owner, heldItem.itemScriptableObject.Cooldown) : heldItem.itemScriptableObject.Cooldown;
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

        if (heldItem.Live()?.itemScriptableObject == null)
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
        return player != null && handParent != null && selectedSlot != null && heldItem.Live()?.itemScriptableObject != null;
    }

    private static bool ShouldProcessDurability(InventoryItem heldItem)
    {
        // Weapons now handle their own durability in the WeaponSO.UseItem method
        // Only process durability here for consumables and other non-weapon items
        return heldItem.itemScriptableObject is ConsumableSO ||
               (!(heldItem.itemScriptableObject is WeaponSO) && heldItem.itemScriptableObject.ShouldBeDestroyedOn0UsesLeft);
    }

    private static void ProcessItemDurability(GameObject player, InventoryItem heldItem) => ConsumeOneUse(player, heldItem);

    /// <summary>
    /// One use of a stack item (a sip of a potion, a bite of food): its uses left (durability) go down, and when the unit is
    /// used up one leaves the stack (the player's weight follows) and the next unit starts with its own uses. Returns true
    /// when a whole unit was used up. The stack may reach 0: the caller removes the emptied item.
    /// </summary>
    public static bool ConsumeOneUse(GameObject player, InventoryItem heldItem)
    {
        if (heldItem == null || heldItem.itemScriptableObject == null || heldItem.stackCurrent <= 0)
            return false;
        int perUse = heldItem.itemScriptableObject.DurabilityReductionPerUse;
        if (perUse <= 0)
            return false; // reusable: never used up
        // A unit with no uses recorded is a fresh one (the next unit of a stack used to keep 0 here, so only the
        // first unit of a stack was ever spent).
        if (heldItem.durability <= 0f)
            heldItem.durability = InventoryItem.StartingDurability(heldItem.itemScriptableObject);
        heldItem.durability -= perUse;
        if (heldItem.durability > 0f)
            return false;

        DecreaseItemStack(player, heldItem);
        if (heldItem.DurabilityList?.Count > 0)
            UpdateItemDurability(heldItem);
        else
            heldItem.durability = heldItem.stackCurrent > 0 ? InventoryItem.StartingDurability(heldItem.itemScriptableObject) : 0f;
        heldItem.UpdateTotalWeight();
        heldItem.RefreshUI();
        return true;
    }

    private static void UpdateItemDurability(InventoryItem heldItem)
    {
        if (heldItem.Live()?.DurabilityList?.Count > 0)
        {
            heldItem.durability = heldItem.DurabilityList[^1];
            heldItem.DurabilityList.RemoveAt(heldItem.DurabilityList.Count - 1);
        }
    }

    private static void DecreaseItemStack(GameObject player, InventoryItem heldItem)
    {
        if (player == null || heldItem.Live()?.itemScriptableObject == null) return;

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

        if (heldItem.Live()?.gameObject != null)
        {
            Object.Destroy(heldItem.gameObject);
        }

        // Only the hotbar's model (the hand bone's own children stay).
        HotbarHandler.ClearHand(handParent);
    }
}