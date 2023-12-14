using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public class PlayerDashController : MonoBehaviour
{
    // Reference to the dash model and movement model
    private PlayerDashModel model;
    private PlayerMovementModel modelMovement;
    private PlayerMovementController controllerMovement;

    private PlayerStatusController statusController;


    // Initialization method
    private void Awake()
    {
        // Get references to dash and movement models
        model = GetComponent<PlayerDashModel>();
        modelMovement = GetComponent<PlayerMovementModel>();
        controllerMovement = GetComponent<PlayerMovementController>();

        if (model.ShouldConsumeStamina)
        {
            statusController = GetComponent<PlayerStatusController>();
        }
    }

    // Method to initiate a dash
    public void Dash()
    {
        // Check if enough time has passed since the last dash
        if (Time.time - model.LastDashTime > model.DashCoolDown)
        {
            // Start the dash routine and update the last dash time
            if (model.ShouldConsumeStamina)
            {

                if (statusController.HasEnoughStamina(model.AmountOfDashStaminaCost))
                {
                    statusController.ConsumeStamina(model.AmountOfDashStaminaCost);
                    StartCoroutine(DashRoutine());
                    model.LastDashTime = Time.time;
                }
                return;
            }
            StartCoroutine(DashRoutine());
            model.LastDashTime = Time.time;
        }
        
    }

    // Coroutine for handling the dash movement
    public IEnumerator DashRoutine()
    {
        // Set the dashing state to true
        modelMovement.IsDashing = true;

        // Record the start time of the dash
        float startTime = Time.time;

        // Perform the dash for the specified duration
        while (Time.time - startTime < model.DashDuration)
        {
            // Get the current forward direction of the player's rotation
            Vector3 current = controllerMovement.PlayerForwardPosition();

            // Move the player using the dash speed
            modelMovement.Controller.Move(current * model.DashSpeed * Time.deltaTime);

            // Yield until the next frame
            yield return null;
        }

        // Set the dashing state to false after the dash is complete
        modelMovement.IsDashing = false;
    }
}
