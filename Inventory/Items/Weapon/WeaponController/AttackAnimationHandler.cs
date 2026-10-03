using UnityEngine;

/// <summary>Plays the attack, charge and fallback animations of weapon attacks, and the attack sound.</summary>
public class AttackAnimationHandler
{
    private readonly WeaponController controller;
    private bool charging;

    public AttackAnimationHandler(WeaponController controller)
    {
        this.controller = controller;
    }

    /// <summary>Plays the attack's clip stretched to <paramref name="duration"/> (or the animator trigger of its input).</summary>
    public void TriggerAttackAnimation(IAttackComponent component, AttackType input, float duration, float speed) =>
        TriggerAttackAnimation(component, input, duration, speed, null);

    /// <summary>As above; <paramref name="weapon"/> is the weapon attacking (its Attack Sound plays when the attack has none).</summary>
    public void TriggerAttackAnimation(IAttackComponent component, AttackType input, float duration, float speed, WeaponSO weapon)
    {
        PlayAttackSound(component, weapon);
        var animController = controller.GetAnimController();
        if (animController == null)
            return;

        if (component.AnimationClip != null)
            animController.PlayAttackAnimationWithDuration(component.AnimationClip, duration, speed);
        else
            animController.TriggerAttackAnimationWithDuration(GetFallbackAttackTrigger(input), duration, 0);
    }

    /// <summary>Old overload (duration and speed from the attack itself).</summary>
    public void TriggerAttackAnimation(IAttackComponent component)
    {
        AttackType input = component is AttackAction a ? a.actionType : controller.CurrentAttackAction != null ? controller.CurrentAttackAction.actionType : AttackType.Normal;
        TriggerAttackAnimation(component, input, component.GetTotalDuration(), component.AnimationSpeed);
    }

    public void PlayChargeAnimation(AnimationClip clip)
    {
        var animController = controller.GetAnimController();
        if (animController == null || clip == null)
            return;
        charging = true;
        animController.PlayAnimation(clip);
    }

    public void StopChargeAnimation()
    {
        if (!charging)
            return;
        charging = false;
        controller.GetAnimController()?.ResetAnimationState();
    }

    public void ForceEnd()
    {
        charging = false;
        controller.GetAnimController()?.ForceEndAttackAnimation();
    }

    public static string GetFallbackAttackTrigger(AttackType attackType)
    {
        return attackType switch
        {
            AttackType.Light => "LightAttackTrigger",
            AttackType.Heavy => "HeavyAttackTrigger",
            AttackType.Special => "SpecialAttackTrigger",
            AttackType.Alternate => "AlternateAttackTrigger",
            AttackType.OffHand => "OffHandAttackTrigger",
            _ => "AttackTrigger"
        };
    }

    private void PlayAttackSound(IAttackComponent component, WeaponSO weapon)
    {
        if (weapon == null)
            weapon = controller.EquippedWeapon;
        AudioClip clip = component.AttackSound != null ? component.AttackSound : weapon != null ? weapon.AttackSound : null;
        controller.PlaySound(clip);
    }
}
