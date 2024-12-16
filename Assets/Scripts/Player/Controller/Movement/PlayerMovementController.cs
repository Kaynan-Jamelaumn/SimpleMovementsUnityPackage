using UnityEngine;
using UnityEngine.Assertions;
public class PlayerMovementController : MonoBehaviour
{
    // Models and Controllers
    private PlayerMovementModel model;
    private PlayerCameraModel cameraModel;
    private PlayerCameraController cameraController;
    //private StaminaManager staminaManager;
    private PlayerStatusController statusController;

    public PlayerAnimationController playerAnimationController;
    [SerializeField] private MovementStateMachine movementStateMachine;

    private PlayerInput input;
    private void Awake()
    {
        // Assign models and controllers
        model = GetComponent<PlayerMovementModel>();
        cameraModel = GetComponent<PlayerCameraModel>();
        cameraController = GetComponent<PlayerCameraController>();
        model.Controller = GetComponent<CharacterController>();
        playerAnimationController = GetComponent<PlayerAnimationController>();
        input = new PlayerInput();

        // Check for null components
        if (model.Controller == null || model == null || cameraModel == null || cameraController == null || model.Controller == null)
        {
            Debug.LogError("Some component is null.");
            return;
        }

        // If stamina consumption is required, assign status models and controllers
        if (model.ShouldConsumeStamina)
        {
            // staminaManager = GetComponent<StaminaManager>();
            statusController = GetComponent<PlayerStatusController>();
        }
    }
    private void Start()
    {
        ValidateAsignments();
    }
    private void Update()
    {
        bool sprintPressed = input.Player.Sprint.triggered;
        bool crouchPressed = input.Player.Crouch.triggered;

        model.actionPressed = sprintPressed || crouchPressed;
        // HandleMovementAnimation();
    }
    void FixedUpdate()
    {
        ApplyGravity();
        ApplyMovement();
        JumpingStateHandler();
    }
    private void LateUpdate()
    {
        // Apply rotation after movement
        ApplyRotation();
    }
    private void OnEnable()
    {
        input.Player.Enable();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(model, "PlayerMovementModel is not assigned in model.");
        Assert.IsNotNull(cameraModel, "PlayerCameraModel is not assigned in cameraModel.");
        Assert.IsNotNull(cameraController, "PlayerCameraController is not assigned in cameraController.");
        Assert.IsNotNull(model.Controller, "CharacterController is not assigned in  model.Controller.");
        Assert.IsNotNull(playerAnimationController, "PlayerAnimationController is not assigned in  playerAnimationController.");
        Assert.IsNotNull(statusController, "PlayerStatusController is not assigned in  statusController.");
    }

    // Check if the player is grounded using a raycast
    public bool IsGrounded()
    {
        Vector3 raycastOrigin = model.PlayerShellObject.transform.position + Vector3.up * model.Controller.stepOffset;
        float raycastLength = 0.5f; // Adjust this value based on the game's scale

        Debug.DrawRay(raycastOrigin, Vector3.down * raycastLength, Color.red); // Visualize the ray in the scene

        bool isHit = Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastLength);

        return isHit;
    }

    // Apply player movement based on input
    public void ApplyMovement()
    {
        float currentSpeed = model.Speed;
        switch (movementStateMachine.CurrentState.StateKey) 
        //switch (model.CurrentPlayerState)
        {
            case MovementStateMachine.EMovementState.Idle:
                //case PlayerMovementModel.PlayerState.Idle:
                currentSpeed = 0;
                break;

            case MovementStateMachine.EMovementState.Walking:
            //case PlayerMovementModel.PlayerState.Walking:
                currentSpeed *= 1.0f; // Adjust as needed

                break;

            case MovementStateMachine.EMovementState.Running:
            //case PlayerMovementModel.PlayerState.Running:
                currentSpeed *= model.SpeedWhileRunningMultiplier;
                statusController.StaminaManager.IsConsumingStamina = true;
                if (statusController.StaminaManager.HasEnoughStamina(model.AmountOfSprintStaminaCost) == false)
                {
                    // Check if the coroutine is running before stopping it
                    if (model.SprintCoroutine != null) StopCoroutine(model.SprintCoroutine);
                    model.CurrentPlayerState = PlayerMovementModel.PlayerState.Walking;
                    statusController.StaminaManager.IsConsumingStamina = false;
                    break;
                }
                break;

            case MovementStateMachine.EMovementState.Crouching:
            //case PlayerMovementModel.PlayerState.Crouching:
                currentSpeed *= model.SpeedWhileCrouchingMultiplier;
                statusController.StaminaManager.IsConsumingStamina = true;
                if (model.CrouchCoroutine != null && statusController.StaminaManager.HasEnoughStamina(model.AmountOfCrouchStaminaCost) == false)
                {

                    // Check if the coroutine is running before stopping it   
                    if (model.CrouchCoroutine != null) StopCoroutine(model.CrouchCoroutine);


                    model.CurrentPlayerState = PlayerMovementModel.PlayerState.Walking;
                    statusController.StaminaManager.IsConsumingStamina = false;
                }
                break;

            default:
                break;
        }
        Vector3 currentDirection = model.Direction;
        // If stamina consumption is required, handle it
        if (model.ShouldConsumeStamina)
        {
            //HandleStaminaConsumption();
        }

        currentSpeed = statusController.WeightManager.CalculateSpeedBasedOnWeight(currentSpeed);

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

    public Vector3 PlayerForwardPosition()
    {
        return model.PlayerTransform.rotation * Vector3.forward;
    }

    private void JumpingStateHandler()
    {
        if (model.CurrentPlayerState == PlayerMovementModel.PlayerState.Jumping)
            if (IsGrounded() == true) UpdateMovementState();
    }
    public void UpdateMovementState()
    {

        bool stillMoving = model.Movement2D.sqrMagnitude > 0;
        bool isNotInADefaultMovingState = model.CurrentPlayerState == PlayerMovementModel.PlayerState.Running || model.CurrentPlayerState == PlayerMovementModel.PlayerState.Crouching;
        if (stillMoving && !isNotInADefaultMovingState)
            model.CurrentPlayerState = PlayerMovementModel.PlayerState.Walking;
        // movementStateMachine.CurrentState = movementStateMachine.States[EMovementState.Walking];
        else if (!stillMoving ||
       (model.actionPressed && !stillMoving || !model.actionPressed && stillMoving && isNotInADefaultMovingState) &&
       (model.ShouldConsumeStamina && !statusController.StaminaManager.IsConsumingStamina))
        {
            if (stillMoving && !model.actionPressed) model.CurrentPlayerState = PlayerMovementModel.PlayerState.Walking;
            else model.CurrentPlayerState = PlayerMovementModel.PlayerState.Idle;
        }
    }

}