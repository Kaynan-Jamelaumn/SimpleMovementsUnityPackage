using UnityEngine;

/// <summary>
/// Equips and unequips the weapon in hand: registers it with the <see cref="EquipmentManager"/> (its passive effects
/// and traits), tells <see cref="CombatStats"/> what is wielded (weapon-limited bonuses), swaps the animator
/// controller when the weapon has one, and plays the equip sounds and animations.
/// </summary>
public class WeaponManager
{
    private readonly WeaponController controller;
    private WeaponSO equippedWeapon;
    private InventoryItem heldItem;
    private WeaponStateCoordinator stateCoordinator;
    private RuntimeAnimatorController originalAnimator;
    private bool swappedAnimator;

    public WeaponSO EquippedWeapon => equippedWeapon;
    public InventoryItem HeldItem => heldItem;

    public WeaponManager(WeaponController controller)
    {
        this.controller = controller;
    }

    public void SetStateCoordinator(WeaponStateCoordinator stateCoordinator)
    {
        this.stateCoordinator = stateCoordinator;
    }

    public void EquipWeapon(WeaponSO weaponSO, InventoryItem item = null)
    {
        if (weaponSO == null)
        {
            UnequipWeapon();
            return;
        }
        if (equippedWeapon == weaponSO && heldItem == item)
            return;

        controller.LogDebug($"Equipping weapon: {weaponSO.Name}");
        if (equippedWeapon != null)
            UnequipWeapon(playEffects: false);

        equippedWeapon = weaponSO;
        heldItem = item;
        stateCoordinator?.ResetAllStates();

        EquipmentManager eq = controller.Equipment;
        if (eq != null)
            eq.Equip(controller, weaponSO, "Main Hand");
        CombatStats stats = controller.Stats;
        if (stats != null)
            stats.SetWielded(weaponSO, weaponSO.Category);

        controller.PlaySound(weaponSO.EquipSound);
        AnimationClip equipAnim = weaponSO.GetEquipAnimation();
        if (equipAnim != null)
            controller.GetAnimController()?.PlayAnimation(equipAnim);
        SetupAnimatorController(weaponSO);
        controller.RaiseWeaponChanged();
        controller.LogDebug($"Weapon equipped. Available actions: {controller.GetAvailableActions()}");
    }

    public void UnequipWeapon() => UnequipWeapon(true);

    private void UnequipWeapon(bool playEffects)
    {
        if (equippedWeapon == null) return;

        controller.LogDebug("Unequipping weapon");
        WeaponSO old = equippedWeapon;
        stateCoordinator?.CleanupAll();
        RestoreAnimatorController();

        controller.Equipment?.Unequip(controller);
        CombatStats stats = controller.Stats;
        if (stats != null)
            stats.SetWielded(null, WeaponCategory.None);

        equippedWeapon = null;
        heldItem = null;
        if (playEffects)
        {
            controller.PlaySound(old.UnequipSound);
            AnimationClip anim = old.GetUnequipAnimation();
            if (anim != null)
                controller.GetAnimController()?.PlayAnimation(anim);
        }
        controller.RaiseWeaponChanged();
    }

    private void SetupAnimatorController(WeaponSO weaponSO)
    {
        RuntimeAnimatorController custom = weaponSO.GetAnimatorController();
        var animController = controller.GetAnimController();
        if (custom == null || animController == null || animController.Model == null || animController.Model.Anim == null)
            return;

        Animator anim = animController.Model.Anim;
        if (!swappedAnimator)
            originalAnimator = anim.runtimeAnimatorController;
        anim.runtimeAnimatorController = custom;
        animController.Model.InitializeAnimationHashes();
        swappedAnimator = true;
    }

    private void RestoreAnimatorController()
    {
        var animController = controller.GetAnimController();
        animController?.ForceEndAttackAnimation();
        if (!swappedAnimator || animController == null || animController.Model == null || animController.Model.Anim == null)
            return;
        // The weapon's animator controller used to stay after the weapon was put away.
        animController.Model.Anim.runtimeAnimatorController = originalAnimator;
        animController.Model.InitializeAnimationHashes();
        swappedAnimator = false;
    }
}
