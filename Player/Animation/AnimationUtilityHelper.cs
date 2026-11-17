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

    /// <summary>Plays clips that have no state in the controller (optional: null unless the character has the component).</summary>
    public AnimationClipOverlay Overlay { get; set; }

    /// <summary>
    /// Plays the state named after the clip. Where that state is on <paramref name="layerIndex"/> this does exactly
    /// what it always did. Where it is not, it used to ask the Animator for a state that does not exist (an error in
    /// the Console, nothing on screen); now the state is looked for on the other layers, then the clip itself is
    /// played by the <see cref="AnimationClipOverlay"/> when the character has one, else nothing happens.
    /// </summary>
    public void PlayAnimation(AnimationClip animationClip, int layerIndex = BASE_LAYER, float crossfadeTime = 0.1f)
    {
        if (animationClip == null) return;
        Animator anim = model.Anim;
        if (anim == null) return;

        int state = Animator.StringToHash(animationClip.name);
        if (StateOn(anim, layerIndex, state))
        {
            // As before: cross-faded when the controller holds a clip of that name, started at once when it does not.
            if (HasAnimationState(animationClip.name, layerIndex))
                anim.CrossFade(animationClip.name, crossfadeTime, layerIndex);
            else
                anim.PlayInFixedTime(animationClip.name, layerIndex);
            return;
        }

        int other = LayerWithState(anim, state);
        if (other >= 0)
        {
            anim.CrossFade(state, crossfadeTime, other);
            return;
        }

        if (Overlay != null && Overlay.PlayActionClips)
            Overlay.Play(animationClip, 1f, 0f, crossfadeTime);
    }

    /// <summary>
    /// Plays a one-off action (using an item, a gesture) without touching an attack: the state named after
    /// <paramref name="clip"/> when the controller has one, else the clip itself through the overlay, else
    /// <paramref name="fallbackTrigger"/> when the controller has that trigger. False when nothing played.
    /// </summary>
    public bool PlayActionClip(AnimationClip clip, string fallbackTrigger, float crossfadeTime)
    {
        Animator anim = model.Anim;
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
            return false;
        // Never over an attack: the weapon's hits follow the attack's animation.
        var attackManager = model.GetComponent<AttackAnimationManager>();
        if (model.IsAttacking || (attackManager != null && attackManager.AttackState.IsLocked))
            return false;

        if (clip != null)
        {
            int state = Animator.StringToHash(clip.name);
            int layer = LayerWithState(anim, state);
            if (layer >= 0)
            {
                anim.CrossFadeInFixedTime(state, crossfadeTime, layer, 0f);
                return true;
            }
            if (Overlay != null && Overlay.PlayActionClips && Overlay.Play(clip, 1f, 0f, crossfadeTime))
                return true;
        }
        return !string.IsNullOrEmpty(fallbackTrigger) && model.SetParameterSafe(fallbackTrigger, true);
    }

    private static bool StateOn(Animator anim, int layer, int state) =>
        anim.runtimeAnimatorController != null && anim.isActiveAndEnabled && layer >= 0 && layer < anim.layerCount && anim.HasState(layer, state);

    private static int LayerWithState(Animator anim, int state)
    {
        if (anim.runtimeAnimatorController == null || !anim.isActiveAndEnabled)
            return -1;
        for (int layer = 0; layer < anim.layerCount; layer++)
        {
            if (anim.HasState(layer, state))
                return layer;
        }
        return -1;
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