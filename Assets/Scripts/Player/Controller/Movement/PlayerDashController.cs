using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
public class PlayerDashController : PlayerActionControllerBase<PlayerDashModel>
{
    private void Start()
    {
        ValidateAssignments();
    }

    public void Dash()
    {
        if (Time.time - model.LastDashTime > model.DashCoolDown)
        {
            if (model.ShouldConsumeStamina && statusController.StaminaManager.HasEnougCurrentValue(model.AmountOfDashStaminaCost))
            {
                statusController.StaminaManager.ConsumeStamina(model.AmountOfDashStaminaCost);
                StartAction();
            }
            else if (!model.ShouldConsumeStamina)
            {
                StartAction();
            }
        }
    }

    private void StartAction()
    {
       // movementModel.CurrentPlayerState = PlayerMovementModel.PlayerState.Dashing;
        model.DashRoutine = StartCoroutine(DashRoutine());
        model.LastDashTime = Time.time;
    }

    private IEnumerator DashRoutine()
    {
        playerAnimationModel.IsDashing = true;
        float startTime = Time.time;
        while (Time.time - startTime < model.DashDuration)
        {
            movementModel.Controller.Move(model.DashSpeed * statusController.WeightManager.CalculateSpeedBasedOnWeight(movementModel.Speed) * Time.deltaTime * controllerMovement.PlayerForwardPosition());
            yield return null;
        }
        playerAnimationModel.IsDashing = false;
        model.DashRoutine = null;
    }
}
