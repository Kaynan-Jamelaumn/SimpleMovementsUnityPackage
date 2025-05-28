using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;

public abstract class PlayerActionModelBase : MonoBehaviour
{
    public bool ShouldConsumeStamina { get; set; }
    public abstract float StaminaCost { get; }
    public abstract float CooldownDuration { get; }
    public abstract float LastActionTime { get; set; }
}

public abstract class PlayerActionControllerBase<TModel> : MonoBehaviour where TModel : PlayerActionModelBase, new()
{
    [SerializeField] protected TModel model;
    [SerializeField] protected PlayerMovementModel movementModel;
    [SerializeField] protected PlayerMovementController controllerMovement;
    [SerializeField] protected PlayerStatusController statusController;
    [SerializeField] protected PlayerAnimationModel playerAnimationModel;

    protected virtual void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        movementModel = this.CheckComponent(movementModel, nameof(movementModel));
        controllerMovement = this.CheckComponent(controllerMovement, nameof(controllerMovement));
        statusController = this.CheckComponent(statusController, nameof(statusController));
        playerAnimationModel = this.CheckComponent(playerAnimationModel, nameof(playerAnimationModel));
    }

    public bool TryExecuteAction()
    {
        if (!CanExecuteAction()) return false;

        if (model.ShouldConsumeStamina)
        {
            statusController.StaminaManager.ConsumeStamina(model.StaminaCost);
        }

        ExecuteAction();
        model.LastActionTime = Time.time;
        return true;
    }

    protected virtual bool CanExecuteAction()
    {
        // Check cooldown
        if (Time.time - model.LastActionTime <= model.CooldownDuration)
            return false;

        // Check stamina if required
        if (model.ShouldConsumeStamina &&
            !statusController.StaminaManager.HasEnougCurrentValue(model.StaminaCost))
            return false;

        return true;
    }

    // Abstract method for specific action implementation
    protected abstract void ExecuteAction();
}