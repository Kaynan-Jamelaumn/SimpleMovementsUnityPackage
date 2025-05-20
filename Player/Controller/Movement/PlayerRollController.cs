using System.Collections;
using UnityEngine;

public class PlayerRollController : PlayerActionControllerBase<PlayerRollModel>
{
    public void Roll()
    {
        if (Time.time - model.LastRollTime > model.RollCoolDown)
        {
            if (model.ShouldConsumeStamina && statusController.StaminaManager.HasEnougCurrentValue(model.AmountOfRollStaminaCost))
            {
                statusController.StaminaManager.ConsumeStamina(model.AmountOfRollStaminaCost);
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
       // movementModel.CurrentPlayerState = PlayerMovementModel.PlayerState.Rolling;
        model.RollRoutine = StartCoroutine(RollRoutine());
        model.LastRollTime = Time.time;
    }

    private IEnumerator RollRoutine()
    {
        playerAnimationModel.IsRolling = true;
        float startTime = Time.time;
        while (Time.time - startTime < model.RollDuration)
        {
            movementModel.Controller.Move(controllerMovement.PlayerForwardPosition() * model.RollSpeedModifier * statusController.WeightManager.CalculateSpeedBasedOnWeight(movementModel.Speed) * Time.deltaTime);
            yield return null;
        }
        playerAnimationModel.IsRolling = false;
        model.RollRoutine = null;
    }
}
