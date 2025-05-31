using System;
using UnityEngine;

public class AnimationEventHandler
{
    private readonly PlayerAnimationModel model;

    public AnimationEventHandler(PlayerAnimationModel model)
    {
        this.model = model;
    }

    public void SetupAnimationEvents()
    {
        SetupActionEvents(model.OnAttackStart, model.OnAttackEnd, () =>
        {
            var attackManager = model.GetComponent<AttackAnimationManager>();
            if (attackManager != null && !attackManager.AttackState.IsWeaponManaged && !attackManager.AttackState.IsLocked)
            {
                model.IsAttacking = false;
                model.IsInputLocked = false;
            }
        });

        SetupActionEvents(model.OnDashStart, model.OnDashEnd, () => ResetActionState(false, true));
        SetupActionEvents(model.OnRollStart, model.OnRollEnd, () => ResetActionState(true, false));
    }

    private void SetupActionEvents(Action startAction, Action endAction, Action endLogic)
    {
        startAction += () => model.IsInputLocked = true;
        endAction += endLogic;
    }

    private void ResetActionState(bool resetRoll, bool resetDash)
    {
        if (resetRoll) model.IsRolling = false;
        if (resetDash) model.IsDashing = false;
        model.IsInputLocked = false;
    }

    // Animation Event Callbacks
    public void OnAttackHit() => DebugLog("Attack Hit!");
    public void OnFootstep() => DebugLog("Footstep!");
    public void OnLanding()
    {
        model.OnLandStart?.Invoke();
        DebugLog("Player Landed!");
    }

    private void DebugLog(string message)
    {
        if (model.DebugAnimations) Debug.Log($"[AnimController] {message}");
    }
}