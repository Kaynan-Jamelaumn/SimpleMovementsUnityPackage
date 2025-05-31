using UnityEngine;

public class WeaponManager
{
    private WeaponController controller;
    private WeaponSO equippedWeapon;
    private WeaponStateCoordinator stateCoordinator;

    public WeaponSO EquippedWeapon => equippedWeapon;

    public WeaponManager(WeaponController controller)
    {
        this.controller = controller;
    }

    public void SetStateCoordinator(WeaponStateCoordinator stateCoordinator)
    {
        this.stateCoordinator = stateCoordinator;
    }

    public void EquipWeapon(WeaponSO weaponSO)
    {
        controller.LogDebug($"Equipping weapon: {weaponSO?.name}");

        if (equippedWeapon != null) UnequipWeapon();

        equippedWeapon = weaponSO;
        ResetAllStates();
        PlayEffects(weaponSO.EquipSound, weaponSO.GetEquipAnimation());
        SetupAnimatorController(weaponSO);
        SynchronizeWithAnimationController();

        controller.LogDebug($"Weapon equipped successfully. Available actions: {controller.GetAvailableActions()}");
    }

    public void UnequipWeapon()
    {
        if (equippedWeapon == null) return;

        controller.LogDebug("Unequipping weapon");
        PlayEffects(equippedWeapon.UnequipSound, equippedWeapon.GetUnequipAnimation());
        CleanupWeaponState();
        equippedWeapon = null;
    }

    private void ResetAllStates()
    {
        stateCoordinator?.ResetAllStates();
    }

    private void CleanupWeaponState()
    {
        stateCoordinator?.CleanupAll();
        CleanupAnimationController();
    }

    private void SetupAnimatorController(WeaponSO weaponSO)
    {
        if (!weaponSO.UseCustomAnimatorController || weaponSO.GetAnimatorController() == null) return;

        controller.LogDebug("Setting custom animator controller");
        var animController = controller.GetAnimController();
        animController.Model.Anim.runtimeAnimatorController = weaponSO.GetAnimatorController();
        animController.Model.InitializeAnimationHashes();
    }

    private void SynchronizeWithAnimationController()
    {
        var animController = controller.GetAnimController();
        if (animController == null) return;

        animController.Model.OnAttackEnd -= OnAnimationAttackEnd;
        animController.Model.OnAttackStart -= OnAnimationAttackStart;
        animController.Model.OnAttackEnd += OnAnimationAttackEnd;
        animController.Model.OnAttackStart += OnAnimationAttackStart;

        controller.LogDebug("Synchronized with animation controller");
    }

    private void CleanupAnimationController()
    {
        var animController = controller.GetAnimController();
        if (animController == null) return;

        animController.ForceEndAttackAnimation();
        animController.Model.OnAttackEnd -= OnAnimationAttackEnd;
        animController.Model.OnAttackStart -= OnAnimationAttackStart;
    }

    private void PlayEffects(AudioClip sound, AnimationClip animation)
    {
        if (sound != null) controller.GetComponent<AudioSource>()?.PlayOneShot(sound);
        if (animation != null) controller.GetAnimController()?.PlayAnimation(animation);
    }

    private void OnAnimationAttackStart()
    {
        controller.LogDebug("Animation attack started event received");
        // Handle movement locking logic here if needed
    }

    private void OnAnimationAttackEnd()
    {
        controller.LogDebug("Animation attack ended event received");
    }
}