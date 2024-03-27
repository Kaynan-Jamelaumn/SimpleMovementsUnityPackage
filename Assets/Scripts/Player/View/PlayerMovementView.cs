using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class PlayerMovementView : MonoBehaviour
{
    // Reference to movement components
    private PlayerMovementController controller;
    private PlayerMovementModel model;

    // Reference to status components (if stamina is used)
    private StaminaManager staminaManager;
    private PlayerStatusController statusController;
    // Initialization method
    private void Awake()
    {
        // Get references to movement components
        controller = GetComponent<PlayerMovementController>();
        model = GetComponent<PlayerMovementModel>();

        // Check if stamina is used and get references to status components
        if (model.ShouldConsumeStamina)
        {
            staminaManager = GetComponent<StaminaManager>();
            statusController = GetComponent<PlayerStatusController>();
        }
    }
    private void Start()
    {
        ValidateAsignments();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(controller, "PlayerMovementController is not assigned in controller.");
        Assert.IsNotNull(model, "PlayerMovementModel is not assigned in model.");
        Assert.IsNotNull(staminaManager, "StaminaManager is not assigned in staminaManager.");
        Assert.IsNotNull(statusController, "PlayerStatusController is not assigned in statusController.");
        Assert.IsNotNull(staminaManager, "StaminaManager is not assigned in staminaManager.");
        Assert.IsNotNull(statusController, "PlayerStatusController is not assigned in statusController.");
    }
    // Input system callback for movement
    public void OnMovement(InputAction.CallbackContext value)
    {
        // Read and normalize 2D movement input
        model.Movement2D = value.ReadValue<Vector2>();
        model.Movement2D.Normalize();

        // Set movement direction in the player's local space
        model.Direction = new Vector3(model.Movement2D.x, 0, model.Movement2D.y);
        model.Direction.Normalize();

        // Update movement state based on input
        controller.UpdateMovementState();
    }
    // Input system callback for jumping
    public void OnJump(InputAction.CallbackContext value)
    {
        // Check if jump button is pressed and the player is grounded
        if (!value.started || !controller.IsGrounded()) return;

        model.CurrentPlayerState = PlayerMovementModel.PlayerState.Jumping;
        // Check if there is enough stamina for the jump
        if (model.ShouldConsumeStamina)
        {

            if (statusController.StaminaManager.HasEnoughStamina(model.AmountOfJumpStaminaCost))
            {
                // Consume stamina and set vertical velocity for jump
                statusController.StaminaManager.ConsumeStamina(model.AmountOfJumpStaminaCost);
                model.VerticalVelocity = controller.playerAnimationController.model.Anim.GetBool(controller.playerAnimationController.model.IsRunningHash)? model.JumpForce * 1.75f : model.JumpForce;
            }
            return;
        }
        model.VerticalVelocity = model.JumpForce;

        // Update movement state based on input
        controller.UpdateMovementState();
    }

    // Input system callback for sprinting
    public void OnSprint(InputAction.CallbackContext value)
    {
        //if (value.started) model.CurrentPlayerState = PlayerMovementModel.PlayerState.Running;

        //// Check if stamina is used
        //if (model.ShouldConsumeStamina)
        //{
        //    // Start or stop stamina consumption based on button state
        //    if (value.performed)
        //    {
        //        model.SprintCoroutine = StartCoroutine(statusController.StaminaManager.ConsumeStaminaRoutine(model.AmountOfSprintStaminaCost, 0.8f));
        //        staminaManager.IsConsumingStamina = true;
        //    }
        //    if (value.canceled)
        //    {
        //        if (model.SprintCoroutine != null) StopCoroutine(model.SprintCoroutine);
        //        staminaManager.IsConsumingStamina = false;

        //    }
        //}
        //if (value.canceled) controller.UpdateMovementState();

    }

    // Input system callback for crouching
    public void OnCrouch(InputAction.CallbackContext value)
    {
    //    if (value.started) model.CurrentPlayerState = PlayerMovementModel.PlayerState.Crouching;

    //    // Check if stamina is used
    //    if (model.ShouldConsumeStamina)
    //    {
    //        // Start or stop stamina consumption based on button state
    //        if (value.performed)
    //        {
    //            model.CrouchCoroutine = StartCoroutine(statusController.StaminaManager.ConsumeStaminaRoutine(model.AmountOfCrouchStaminaCost, 1f));
    //            staminaManager.IsConsumingStamina = true;
    //        }
    //        if (value.canceled)
    //        {
    //            if (model.CrouchCoroutine != null) StopCoroutine(model.CrouchCoroutine);
    //            staminaManager.IsConsumingStamina = false;
    //        }
    //    }
    //    if (value.canceled) controller.UpdateMovementState();
    }

    // Helper method to update the movement state based on input

}
