using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public class PlayerMovementView : MonoBehaviour
{
    // Reference to movement components
    private PlayerMovementController controller;
    private PlayerMovementModel model;

    // Reference to status components (if stamina is used)
    private PlayerStatusModel statusModel;
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
            statusModel = GetComponent<PlayerStatusModel>();
            statusController = GetComponent<PlayerStatusController>();
        }
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

        // Apply the movement
        controller.ApplyMovement();
    }

    // Input system callback for jumping
    public void OnJump(InputAction.CallbackContext value)
    {
        // Check if jump button is pressed and the player is grounded
        if (!value.started || !controller.IsGrounded()) return;

        // Check if there is enough stamina for the jump
        if (model.ShouldConsumeStamina)
        {

            if (statusController.HasEnoughStamina(model.AmountOfJumpStaminaCost))
            {
                // Consume stamina and set vertical velocity for jump
                statusController.ConsumeStamina(model.AmountOfJumpStaminaCost);
                model.VerticalVelocity = model.IsRunning ? model.JumpForce * 1.75f : model.JumpForce;
            }
            return;
        }
        model.VerticalVelocity = model.JumpForce;
    }

    // Input system callback for sprinting
    public void OnSprint(InputAction.CallbackContext value)
    {
        // Check if sprint button is pressed
        if (value.started) model.IsRunning = true;

        // Check if stamina is used
        if (model.ShouldConsumeStamina)
        {
            // Start or stop stamina consumption based on button state
            if (value.started)
            {
                model.SprintCoroutine = StartCoroutine(statusController.ConsumeStaminaRoutine(model.AmountOfSprintStaminaCost, 0.8f));
                statusModel.IsConsumingStamina = true;
            }

            if (value.canceled)
            {
                model.IsRunning = false;
                if (model.SprintCoroutine !=null ) StopCoroutine(model.SprintCoroutine);
                statusModel.IsConsumingStamina = false;
            }
        }

        // If button is released, stop sprinting
        if (value.canceled) model.IsRunning = false;
    }

    // Input system callback for crouching
    public void OnCrouch(InputAction.CallbackContext value)
    {
        // Check if crouch button is pressed
        if (value.started) model.IsCrouching = true;

        // Check if stamina is used
        if (model.ShouldConsumeStamina)
        {
            // Start or stop stamina consumption based on button state
            if (value.started)
            {
                model.CrouchCoroutine = StartCoroutine(statusController.ConsumeStaminaRoutine(model.AmountOfCrouchStaminaCost, 1f));
                statusModel.IsConsumingStamina = true;
            }

            if (value.canceled)
            {
                model.IsCrouching = false;
                StopCoroutine(model.CrouchCoroutine);
                statusModel.IsConsumingStamina = false;
            }
        }

        // If button is released, stop crouching
        if (value.canceled) model.IsCrouching = false;
    }


}
