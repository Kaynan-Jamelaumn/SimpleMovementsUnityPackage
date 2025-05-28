using System.Collections;
using UnityEngine;

public class PlayerDashController : PlayerActionControllerBase<PlayerDashModel>
{
    public void Dash() => TryExecuteAction();

    protected override void ExecuteAction()
    {
        model.DashRoutine = StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        playerAnimationModel.IsDashing = true;

        float elapsed = 0f;
        while (elapsed < model.DashDuration)
        {
            Vector3 movement = model.DashSpeed *
                             statusController.WeightManager.CalculateSpeedBasedOnWeight(movementModel.Speed) *
                             Time.deltaTime *
                             controllerMovement.PlayerForwardPosition();

            movementModel.Controller.Move(movement);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerAnimationModel.IsDashing = false;
        model.DashRoutine = null;
        model.LastActionTime = Time.time;
    }
    public bool Ready => Time.time - model.LastActionTime > model.CooldownDuration &&
        (!model.ShouldConsumeStamina || statusController.StaminaManager.HasEnougCurrentValue(model.StaminaCost));

}

