using UnityEngine;

public class AttackAnimationHandler
{
    private WeaponController controller;

    public AttackAnimationHandler(WeaponController controller)
    {
        this.controller = controller;
    }

    public void TriggerAttackAnimation(IAttackComponent component)
    {
        var animController = controller.GetAnimController();
        if (animController == null)
        {
            controller.LogDebug("Animation controller is null!", true);
            return;
        }

        float duration = component.GetTotalDuration();
        string attackName = (component as AttackVariation)?.variationName ??
                           (component as AttackAction)?.actionName ?? "Unknown";
        controller.LogDebug($"Triggering animation for {attackName} with duration: {duration}s");

        if (component.AnimationClip != null)
        {
            animController.PlayAttackAnimationWithDuration(
                component.AnimationClip,
                duration,
                component.AnimationSpeed
            );
        }
        else
        {
            // Use fallback trigger based on action type
            // For variations, we need to get the action type from the current attack state
            AttackType attackType = AttackType.Normal;

            if (component is AttackAction action)
            {
                attackType = action.actionType;
            }
            else
            {
                // For AttackVariation, we need to get the attack type from the controller's current attack action
                var currentAction = controller.CurrentAttackAction;
                if (currentAction != null)
                {
                    attackType = currentAction.actionType;
                }
            }

            string fallbackTrigger = GetFallbackAttackTrigger(attackType);
            animController.TriggerAttackAnimationWithDuration(fallbackTrigger, duration, 0);
        }

        PlayAttackSound(component);
    }

    private string GetFallbackAttackTrigger(AttackType attackType)
    {
        return attackType switch
        {
            AttackType.Light => "LightAttackTrigger",
            AttackType.Heavy => "HeavyAttackTrigger",
            AttackType.Special => "SpecialAttackTrigger",
            AttackType.Normal => "AttackTrigger",
            _ => "AttackTrigger"
        };
    }

    private void PlayAttackSound(IAttackComponent component)
    {
        AudioClip soundToPlay = component.AttackSound ?? controller.EquippedWeapon.AttackSound;
        if (soundToPlay != null)
        {
            var player = controller.GetComponent<Player>();
            player?.PlayerAudioSource?.PlayOneShot(soundToPlay);
        }
    }
}