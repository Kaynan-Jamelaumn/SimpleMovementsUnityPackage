using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
public abstract class PlayerActionModelBase : MonoBehaviour
{
    public bool ShouldConsumeStamina { get; set; }
}


public abstract class PlayerActionControllerBase<TModel> : MonoBehaviour where TModel : PlayerActionModelBase, new()
{
    protected TModel model;
    protected PlayerMovementModel movementModel;
    protected PlayerMovementController controllerMovement;
    protected PlayerStatusController statusController;
    protected PlayerAnimationModel playerAnimationModel;

    protected virtual void Awake()
    {
        model = GetComponent<TModel>();
        movementModel = GetComponent<PlayerMovementModel>();
        controllerMovement = GetComponent<PlayerMovementController>();
        playerAnimationModel = GetComponent<PlayerAnimationModel>();
        statusController = GetComponent<PlayerStatusController>();
        //if (model.ShouldConsumeStamina)
    }

    protected void ValidateAssignments()
    {
        Assert.IsNotNull(model, $"{typeof(TModel).Name} is not assigned in model.");
        Assert.IsNotNull(movementModel, "PlayerMovementModel is not assigned in movementModel.");
        Assert.IsNotNull(controllerMovement, "PlayerMovementController is not assigned in controllerMovement.");
        Assert.IsNotNull(statusController, "PlayerStatusController is not assigned in statusController.");
        Assert.IsNotNull(playerAnimationModel, "PlayerAnimationModel is not assigned in playerAnimationModel.");
    }
}