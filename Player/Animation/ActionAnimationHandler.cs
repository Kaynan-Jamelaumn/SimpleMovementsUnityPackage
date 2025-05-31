using System;
using System.Collections;
using UnityEngine;

public class ActionAnimationHandler : MonoBehaviour
{
    private PlayerAnimationModel model;

    // Animation layers
    private const int ACTION_LAYER = 1;

    public void Initialize(PlayerAnimationModel animModel)
    {
        model = animModel;
    }

    public void TriggerRollAnimation() => TriggerActionAnimation("RollTrigger", () => model.IsRolling = true, model.OnRollStart, HandleRollAnimation);
    public void TriggerDashAnimation() => TriggerActionAnimation("DashTrigger", () => model.IsDashing = true, model.OnDashStart, HandleDashAnimation);

    public void EndRollAnimation() => ResetActionState(true, false);
    public void EndDashAnimation() => ResetActionState(false, true);

    private void TriggerActionAnimation(string trigger, Action setState, Action onStart, Func<IEnumerator> handler)
    {
        if (model.IsInputLocked) return;

        TriggerAnimation(trigger);
        setState?.Invoke();
        onStart?.Invoke();
        StartCoroutine(handler());
    }

    private IEnumerator HandleRollAnimation()
    {
        yield return HandleActionAnimation(() =>
        {
            model.IsRolling = false;
            model.SetParameterSafe("IsRolling", false);
            model.OnRollEnd?.Invoke();
        });
    }

    private IEnumerator HandleDashAnimation()
    {
        yield return HandleActionAnimation(() =>
        {
            model.IsDashing = false;
            model.SetParameterSafe("IsDashing", false);
            model.OnDashEnd?.Invoke();
        });
    }

    private IEnumerator HandleActionAnimation(Action endAction)
    {
        yield return null;
        var stateInfo = model.Anim.GetCurrentAnimatorStateInfo(ACTION_LAYER);
        yield return new WaitForSeconds(stateInfo.length);
        endAction?.Invoke();
    }

    private void ResetActionState(bool resetRoll, bool resetDash)
    {
        if (resetRoll) model.IsRolling = false;
        if (resetDash) model.IsDashing = false;
        model.IsInputLocked = false;
    }

    private void TriggerAnimation(string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName)) return;
        model.SetParameterSafe(triggerName, true);
    }
}