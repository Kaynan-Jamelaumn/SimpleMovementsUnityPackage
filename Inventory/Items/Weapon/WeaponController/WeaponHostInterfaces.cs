using UnityEngine;

/// <summary>
/// Where a weapon controller gets ammunition and spends thrown items: the player's inventory implements it
/// (<see cref="InventoryManager"/>). Without one (scripts, AI), ammo is unlimited and thrown items are not counted.
/// </summary>
public interface IAmmoSource
{
    /// <summary>How much ammo of <paramref name="ammoType"/> there is; <paramref name="best"/> = the ammo item fired next (<paramref name="preferred"/> first).</summary>
    int CountAmmo(string ammoType, AmmoSO preferred, out AmmoSO best);

    /// <summary>Spends ammo. Returns how many were spent.</summary>
    int ConsumeAmmo(AmmoSO ammo, int amount);

    /// <summary>Removes units of a held stack (a thrown knife). Returns how many were removed.</summary>
    int ConsumeItem(InventoryItem item, int amount);

    /// <summary>How many of an item the inventory holds (shot costs paid in items).</summary>
    int CountItem(ItemSO item, bool includeHotbar);

    /// <summary>Removes units of an item from the inventory. Returns how many were removed.</summary>
    int RemoveItems(ItemSO item, int amount);
}

/// <summary>Shows the hands' items on the character (implemented by <see cref="EquipmentVisuals"/>).</summary>
public interface IWeaponVisualHost
{
    /// <summary>The model of a hand's item (in the hand or sheathed), or null.</summary>
    Transform ModelFor(WeaponHandSide side);

    /// <summary>Puts the hands' items in the hands now (an attack, a block, aiming).</summary>
    void DrawWeapons();
}
