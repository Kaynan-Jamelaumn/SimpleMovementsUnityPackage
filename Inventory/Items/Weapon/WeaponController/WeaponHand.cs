using UnityEngine;

/// <summary>
/// One hand of a <see cref="WeaponController"/>: the weapon and its inventory item, the hand's own chains and combos, and
/// the ranged state of its weapon (rounds loaded, reload, fire interval, bloom). The main hand holds the weapon of the
/// selected hotbar slot; the off hand the one-handed weapon of the Off Hand equipment slot (dual wielding).
/// </summary>
public sealed class WeaponHand
{
    public WeaponHandSide Side { get; }
    /// <summary>The weapon held (null = none).</summary>
    public WeaponSO Weapon { get; internal set; }
    /// <summary>Its inventory item (durability, rounds loaded), or null for weapons given by scripts.</summary>
    public InventoryItem Item { get; internal set; }
    /// <summary>The chains (variations) of this hand's weapon.</summary>
    public VariationSystem Variations { get; internal set; }
    /// <summary>The combo state of this hand's weapon.</summary>
    public ComboSystem Combo { get; internal set; }

    // Ranged state
    internal int loadedWithoutItem = -1;
    internal AmmoSO loadedAmmo;
    internal float nextFireTime;
    internal float bloom;
    internal bool reloading;
    internal float reloadEndTime;
    internal float reloadDuration;
    internal float reloadSpeedDelta;
    internal bool reloadMoveApplied;

    public WeaponHand(WeaponHandSide side)
    {
        Side = side;
    }

    public bool IsMain => Side == WeaponHandSide.Main;
    public bool IsReloading => reloading;
    /// <summary>0-1 progress of the reload in progress.</summary>
    public float ReloadProgress => reloading && reloadDuration > 0f ? Mathf.Clamp01(1f - (reloadEndTime - Time.time) / reloadDuration) : 0f;
    public RangedMechanic Ranged => Weapon != null ? Weapon.Ranged : null;

    /// <summary>Rounds loaded in this hand's weapon (magazine weapons; -1 = not loaded yet).</summary>
    internal int Loaded
    {
        get => Item != null ? Item.loadedRounds : loadedWithoutItem;
        set
        {
            if (Item != null) Item.loadedRounds = value;
            else loadedWithoutItem = value;
        }
    }

    internal void ResetRangedState()
    {
        reloading = false;
        bloom = 0f;
        nextFireTime = 0f;
    }
}
