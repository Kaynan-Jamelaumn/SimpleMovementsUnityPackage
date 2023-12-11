using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public class PlayerMovementController : MonoBehaviour
{
    // Models and Controllers
    private PlayerMovementModel model;
    private PlayerCameraModel cameraModel;
    private PlayerCameraController cameraController;
    private PlayerStatusModel statusModel;
    private PlayerStatusController statusController;

    private void Awake()
    {
        // Assign models and controllers
        model = GetComponent<PlayerMovementModel>();
        cameraModel = GetComponent<PlayerCameraModel>();
        cameraController = GetComponent<PlayerCameraController>();
        model.Controller = GetComponent<CharacterController>();

        // Check for null components
        if (model.Controller == null || model == null || cameraModel == null || cameraController == null || model.Controller == null)
        {
            Debug.LogError("Some component is null.");
            return;
        }

        // If stamina consumption is required, assign status models and controllers
        if (model.ShouldConsumeStamina)
        {
            statusModel = GetComponent<PlayerStatusModel>();
            statusController = GetComponent<PlayerStatusController>();
        }
    }

    void FixedUpdate()
    {
        // Apply gravity and movement in fixed intervals
        ApplyGravity();
        ApplyMovement();
    }

    private void LateUpdate()
    {
        // Apply rotation after movement
        ApplyRotation();
    }

    // Check if the player is grounded using a raycast
    public bool IsGrounded()
    {
        Vector3 raycastOrigin = model.PlayerShellObject.transform.position + Vector3.up * model.Controller.stepOffset;
        float raycastLength = 0.5f; // Adjust this value based on the game's scale
        RaycastHit hit;

        Debug.DrawRay(raycastOrigin, Vector3.down * raycastLength, Color.red); // Visualize the ray in the scene

        bool isHit = Physics.Raycast(raycastOrigin, Vector3.down, out hit, raycastLength);

        return isHit;
    }

    // Apply player movement based on input
    public void ApplyMovement()
    {
        model.IsWalking = model.Direction.x != 0 || model.Direction.z != 0;
        float currentSpeed = model.Speed * (model.IsRunning && !model.IsCrouching ? model.SpeedWhileRunningMultiplier : model.IsCrouching ? model.SpeedWhileCrouchingMultiplier : 1);

        Vector3 currentDirection = model.Direction;

        // If stamina consumption is required, handle it
        if (model.ShouldConsumeStamina)
        {
            HandleStaminaConsumption(currentSpeed);
        }

        // Rotate player based on camera angle if required
        RotatePlayerByCameraAngle(ref currentDirection);

        model.Controller.Move(currentDirection * currentSpeed * Time.deltaTime);
    }

    // Apply player rotation
    public void ApplyRotation()
    {
        if (model.Movement2D.sqrMagnitude == 0) return;

        if (cameraModel.PlayerShouldRotateByCameraAngle || cameraModel.IsFirstPerson)
            model.PlayerTransform.rotation = Quaternion.Euler(0.0f, cameraModel.CameraTransform.transform.eulerAngles.y, 0.0f);
    }

    // Apply gravity to the player
    private void ApplyGravity()
    {
        if (IsGrounded() && model.VerticalVelocity < 0.0f)
        {
            model.VerticalVelocity = -1f;
        }
        else
        {
            model.VerticalVelocity += model.Gravity * model.GravityMultiplier * Time.deltaTime;
        }

        model.Controller.Move(Vector3.up * model.VerticalVelocity * Time.deltaTime);
    }

    // Apply animations based on player state
    public void ApplyAnimation()
    {
        model.Anim.SetBool("isWalking", model.IsWalking);
        model.Anim.SetBool("isRunning", model.IsRunning);
        model.Anim.SetBool("isCrouching", model.IsCrouching);
    }

    // Handle stamina consumption based on movement type
    private void HandleStaminaConsumption(float currentSpeed)
    {
        if (!statusModel.IsConsumingStamina)
        {
            if (model.IsRunning && !statusController.HasEnoughStamina(model.AmountOfSprintStaminaCost))
            {
                model.IsRunning = false;
                StopCoroutine(model.SprintCoroutine);
                statusModel.IsConsumingStamina = false;
            }
            else if (model.IsCrouching && !statusController.HasEnoughStamina(model.AmountOfCrouchStaminaCost))
            {
                model.IsCrouching = false;
                StopCoroutine(model.CrouchCoroutine);
                statusModel.IsConsumingStamina = false;
            }
        }
    }

    // Rotate player based on camera angle if required
    private void RotatePlayerByCameraAngle(ref Vector3 currentDirection)
    {
        if (cameraModel.PlayerShouldRotateByCameraAngle || cameraModel.IsFirstPerson)
        {
            currentDirection = cameraController.DirectionToMoveByCamera(model.Direction);
            model.LastRotation = model.PlayerTransform.rotation.eulerAngles.y;
        }
        else
        {
            currentDirection = Quaternion.AngleAxis(model.LastRotation, Vector3.up) * currentDirection;
        }
    }
}
