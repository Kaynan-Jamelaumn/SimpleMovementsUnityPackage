using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
public abstract class PlayerActionModelBase : MonoBehaviour
{
    public bool ShouldConsumeStamina { get; set; }
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


}