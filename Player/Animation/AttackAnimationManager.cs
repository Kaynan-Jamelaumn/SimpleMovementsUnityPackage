using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackAnimationManager : MonoBehaviour
{
    private PlayerAnimationModel model;
    private AttackAnimationState attackAnimState = new AttackAnimationState();
    private Coroutine attackAnimationCoroutine;

    public AttackAnimationState AttackState => attackAnimState;

    public void Initialize(PlayerAnimationModel animModel)
    {
        model = animModel;
    }

    public void PlayAttackAnimationWithDuration(AnimationClip animationClip, float weaponAttackDuration, float animationSpeed = 1.0f)
    {
        if (!ValidateAnimationClip(animationClip)) return;

        LogAttackAnimationSetup(animationClip, weaponAttackDuration, animationSpeed);

        StopAttackAnimation();

        attackAnimState.CurrentAnimation = animationClip;
        attackAnimState.Speed = animationSpeed;
        attackAnimState.Duration = weaponAttackDuration;
        attackAnimState.IsLocked = true;
        attackAnimState.IsWeaponManaged = true;

        PrepareAttackState();
        attackAnimationCoroutine = StartCoroutine(PlayAttackAnimationCoroutine(animationClip, weaponAttackDuration, animationSpeed));
    }

    public void TriggerAttackAnimationWithDuration(string animationTrigger, float weaponDuration, int comboIndex = 0)
    {
        if (attackAnimState.IsLocked)
        {
            if (model.DebugAnimations) Debug.Log("Attack animation is locked, ignoring trigger request");
            return;
        }

        if (string.IsNullOrEmpty(animationTrigger))
        {
            if (model.DebugAnimations) Debug.LogWarning("Animation trigger is null or empty");
            return;
        }

        if (model.DebugAnimations)
            Debug.Log($"TriggerAttackAnimationWithDuration: {animationTrigger} with duration: {weaponDuration}s, combo: {comboIndex}");

        StopAttackAnimation();

        attackAnimState.Duration = weaponDuration;
        attackAnimState.IsLocked = true;
        attackAnimState.IsWeaponManaged = true;

        if (comboIndex > 0) model.SetParameterSafe("AttackCombo", comboIndex);

        PrepareAttackState();
        TriggerAnimation(animationTrigger);

        attackAnimationCoroutine = StartCoroutine(ControlAttackDuration(weaponDuration));
    }

    public void ForceEndAttackAnimation()
    {
        if (model.DebugAnimations) Debug.Log("Force ending attack animation");

        StopAttackAnimation();
        model.Anim.speed = 1.0f;
        attackAnimState.Reset();

        model.IsAttacking = false;
        model.IsInputLocked = false;

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["IsAttacking"] = false,
            ["AttackCombo"] = 0
        });

        model.OnAttackEnd?.Invoke();
    }

    public void EndAttackAnimation()
    {
        if (attackAnimState.IsLocked)
        {
            if (model.DebugAnimations) Debug.Log("EndAttackAnimation called but animation is locked");
            return;
        }

        if (!attackAnimState.IsWeaponManaged)
        {
            CompleteAttackAnimation();
        }
    }

    public void ResetAttackAnimation()
    {
        if (attackAnimationCoroutine != null)
        {
            StopCoroutine(attackAnimationCoroutine);
            attackAnimationCoroutine = null;
        }
        attackAnimState.Reset();
    }

    private IEnumerator PlayAttackAnimationCoroutine(AnimationClip clip, float weaponDuration, float speed)
    {
        float animationNaturalDuration = clip.length;
        bool needsLooping = weaponDuration > (animationNaturalDuration * 1.1f);
        float adjustedSpeed = speed;

        if (weaponDuration < animationNaturalDuration * 0.9f)
        {
            adjustedSpeed = (animationNaturalDuration / weaponDuration) * speed;
            needsLooping = false;
        }

        model.Anim.speed = adjustedSpeed;
        bool animationStarted = TryPlayAnimation(clip);

        yield return null;

        float elapsedTime = 0f;
        float adjustedAnimDuration = animationNaturalDuration / adjustedSpeed;

        while (elapsedTime < weaponDuration && attackAnimState.IsLocked)
        {
            elapsedTime += Time.deltaTime;

            if (needsLooping && animationStarted)
            {
                yield return HandleAnimationLooping(clip, elapsedTime, adjustedAnimDuration, weaponDuration);
            }

            if (ShouldCheckAnimationState(elapsedTime))
            {
                CheckAndMaintainAttackAnimation(elapsedTime, weaponDuration);
            }

            yield return null;
        }

        CompleteAttackAnimation();
    }

    private IEnumerator ControlAttackDuration(float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration && attackAnimState.IsLocked)
        {
            elapsedTime += Time.deltaTime;
            MaintainAttackState();
            yield return null;
        }

        CompleteAttackAnimation();
    }

    private bool TryPlayAnimation(AnimationClip clip)
    {
        try
        {
            for (int layer = 0; layer < model.Anim.layerCount; layer++)
            {
                if (model.Anim.HasState(layer, Animator.StringToHash(clip.name)))
                {
                    model.Anim.Play(clip.name, layer, 0f);
                    if (model.DebugAnimations) Debug.Log($"✓ Playing animation: {clip.name} on layer {layer}");
                    return true;
                }
            }

            return TryTriggerFallback();
        }
        catch (Exception e)
        {
            if (model.DebugAnimations) Debug.LogError($"Failed to play animation: {e.Message}");
            return TryTriggerFallback();
        }
    }

    private bool TryTriggerFallback()
    {
        string[] triggers = { "AttackTrigger", "LightAttackTrigger", "HeavyAttackTrigger", "NormalAttackTrigger", "BasicAttackTrigger" };

        foreach (string trigger in triggers)
        {
            if (model.HasAnimationParameter(trigger))
            {
                model.SetParameterSafe(trigger, true);
                if (model.DebugAnimations) Debug.Log($"✓ Triggered: {trigger}");
                return true;
            }
        }

        if (model.HasAnimationParameter("IsAttacking"))
        {
            model.SetParameterSafe("IsAttacking", true);
            return true;
        }

        return false;
    }

    private void PrepareAttackState()
    {
        model.IsAttacking = true;
        model.IsInputLocked = true;
        model.SetParameterSafe("IsAttacking", true);
        model.OnAttackStart?.Invoke();
    }

    private void CompleteAttackAnimation()
    {
        if (model.DebugAnimations) Debug.Log("Completing attack animation");

        model.Anim.speed = 1.0f;
        attackAnimState.Reset();

        model.IsAttacking = false;
        model.IsInputLocked = false;

        SetAnimationParameters(new Dictionary<string, object>
        {
            ["IsAttacking"] = false,
            ["AttackCombo"] = 0
        });

        attackAnimationCoroutine = null;
        model.OnAttackEnd?.Invoke();
    }

    private void StopAttackAnimation()
    {
        if (attackAnimationCoroutine != null)
        {
            StopCoroutine(attackAnimationCoroutine);
            attackAnimationCoroutine = null;
        }
    }

    private bool ValidateAnimationClip(AnimationClip clip)
    {
        if (clip == null)
        {
            if (model.DebugAnimations) Debug.LogError("AnimationClip is null!");
            return false;
        }
        return true;
    }

    private void LogAttackAnimationSetup(AnimationClip clip, float weaponDuration, float speed)
    {
        if (!model.DebugAnimations) return;

        Debug.Log($"=== ATTACK ANIMATION SETUP ===\n" +
                  $"Animation: {clip.name}\n" +
                  $"Length: {clip.length}s\n" +
                  $"Weapon Duration: {weaponDuration}s\n" +
                  $"Speed: {speed}\n" +
                  $"Will Loop: {weaponDuration > clip.length * 1.1f}");
    }

    private bool ShouldCheckAnimationState(float elapsedTime)
    {
        return elapsedTime % 0.2f < Time.deltaTime;
    }

    private void CheckAndMaintainAttackAnimation(float elapsedTime, float duration)
    {
        bool isPlaying = IsAnyAttackAnimationPlaying();

        if (!isPlaying && elapsedTime < duration - 0.2f)
        {
            if (model.DebugAnimations) Debug.Log("No attack animation detected, using fallback");
            TryTriggerFallback();
        }
    }

    private void MaintainAttackState()
    {
        if (!model.IsAttacking && attackAnimState.IsLocked)
        {
            model.IsAttacking = true;
            model.SetParameterSafe("IsAttacking", true);
        }
    }

    private bool IsAnyAttackAnimationPlaying()
    {
        for (int layer = 0; layer < model.Anim.layerCount; layer++)
        {
            var stateInfo = model.Anim.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.IsTag("Attack") || IsAttackStateName(stateInfo))
                return true;
        }
        return model.GetAnimationBool("IsAttacking");
    }

    private bool IsAttackStateName(AnimatorStateInfo stateInfo)
    {
        string[] attackStates = { "Attack", "LightAttack", "HeavyAttack", "NormalAttack", "BasicAttack" };
        return Array.Exists(attackStates, state => stateInfo.IsName(state));
    }

    private IEnumerator HandleAnimationLooping(AnimationClip clip, float elapsedTime, float animDuration, float totalDuration)
    {
        // Implementation depends on specific looping requirements
        yield return null;
    }

    private void SetAnimationParameters(Dictionary<string, object> parameters)
    {
        foreach (var kvp in parameters)
            model.SetParameterSafe(kvp.Key, kvp.Value);
    }

    private void TriggerAnimation(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName)) return;
        model.SetParameterSafe(triggerName, true);
    }

    public bool IsAttackAnimationLocked() => attackAnimState.IsLocked;

    // Backward Compatibility Methods
    public void PlayAttackAnimation(AnimationClip animationClip, float animationSpeed = 1.0f)
    {
        if (animationClip != null)
            PlayAttackAnimationWithDuration(animationClip, animationClip.length / animationSpeed, animationSpeed);
    }

    public void TriggerAttackAnimation(string animationTrigger, int comboIndex = 0)
    {
        TriggerAttackAnimationWithDuration(animationTrigger, 1.0f, comboIndex);
    }
}