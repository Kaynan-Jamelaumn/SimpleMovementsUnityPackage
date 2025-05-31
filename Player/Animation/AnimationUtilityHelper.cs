using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class AnimationUtilityHelper
{
    private readonly PlayerAnimationModel model;

    // Animation layers
    private const int BASE_LAYER = 0;

    public AnimationUtilityHelper(PlayerAnimationModel model)
    {
        this.model = model;
    }

    public void PlayAnimation(AnimationClip animationClip, int layerIndex = BASE_LAYER, float crossfadeTime = 0.1f)
    {
        if (animationClip == null) return;

        if (HasAnimationState(animationClip.name, layerIndex))
        {
            model.Anim.CrossFade(animationClip.name, crossfadeTime, layerIndex);
        }
        else
        {
            model.Anim.PlayInFixedTime(animationClip.name, layerIndex);
        }
    }

    public void TriggerAnimation(string triggerName, float? lockDuration = null, MonoBehaviour owner = null)
    {
        if (string.IsNullOrEmpty(triggerName)) return;

        var attackManager = model.GetComponent<AttackAnimationManager>();
        if (attackManager != null && attackManager.AttackState.IsLocked && triggerName.Contains("Attack"))
        {
            if (model.DebugAnimations) Debug.Log($"Ignoring {triggerName} - attack locked");
            return;
        }

        model.SetParameterSafe(triggerName, true);

        if (lockDuration.HasValue && owner != null)
            owner.StartCoroutine(LockInputForDuration(lockDuration.Value));
    }

    public void SetAnimationParameters(Dictionary<string, object> parameters)
    {
        foreach (var kvp in parameters)
            model.SetParameterSafe(kvp.Key, kvp.Value);
    }

    public bool HasAnimationState(string stateName, int layerIndex)
    {
        if (model.Anim == null || model.Anim.runtimeAnimatorController == null) return false;
        if (layerIndex >= model.Anim.layerCount) return false;

        foreach (var clip in model.Anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == stateName) return true;
        }
        return false;
    }

    public bool IsAnimationPlaying(string animationName, int layerIndex = 0) =>
        model.Anim.GetCurrentAnimatorStateInfo(layerIndex).IsName(animationName);

    public float GetAnimationProgress(int layerIndex = 0) =>
        model.Anim.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;

    public void SetAnimationSpeed(float speed, int layerIndex = 0) => model.Anim.speed = speed;

    private IEnumerator LockInputForDuration(float duration)
    {
        model.IsInputLocked = true;
        yield return new WaitForSeconds(duration);
        model.IsInputLocked = false;
    }

    public void DebugCurrentAnimations()
    {
        var states = new List<string>();
        if (model.IsAttacking) states.Add("Attacking");

        var attackManager = model.GetComponent<AttackAnimationManager>();
        if (attackManager != null)
        {
            if (attackManager.AttackState.IsLocked) states.Add("Locked");
            if (attackManager.AttackState.IsWeaponManaged) states.Add("WeaponManaged");
        }

        if (model.IsDashing) states.Add("Dashing");
        if (model.IsRolling) states.Add("Rolling");
        if (model.IsInputLocked) states.Add("InputLocked");

        if (states.Count > 0) Debug.Log($"[AnimController] States: {string.Join(", ", states)}");
    }
}