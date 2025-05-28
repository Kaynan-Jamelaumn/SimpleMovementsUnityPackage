using System.Collections;
using UnityEngine;
public class PlayerRollController : PlayerActionControllerBase<PlayerRollModel>
{
    public void Roll() => TryExecuteAction();

    protected override void ExecuteAction()
    {
        model.RollRoutine = StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        playerAnimationModel.IsRolling = true;

        float elapsed = 0f;
        while (elapsed < model.RollDuration)
        {
            Vector3 movement = controllerMovement.PlayerForwardPosition() *
                             model.RollSpeedModifier *
                             statusController.WeightManager.CalculateSpeedBasedOnWeight(movementModel.Speed) *
                             Time.deltaTime;

            movementModel.Controller.Move(movement);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerAnimationModel.IsRolling = false;
        model.RollRoutine = null;
        model.LastActionTime = Time.time;
    }
    public bool Ready => Time.time - model.LastActionTime > model.CooldownDuration &&
        (!model.ShouldConsumeStamina || statusController.StaminaManager.HasEnougCurrentValue(model.StaminaCost));
}