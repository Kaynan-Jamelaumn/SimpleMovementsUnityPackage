using UnityEngine;
using UnityEngine.InputSystem.XR;

public class JumpingState : MovementState
{
    public JumpingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }

    public override void EnterState()
    {
        // Use SpeedManager for movement speed during jump
        bool isRunning = Context.AnimationModel.GetAnimationBool("IsRunning");

        // Set horizontal speed based on current state
        if (isRunning)
        {
            Context.MovementModel.CurrentSpeed = Context.StatusController.SpeedManager.GetSpeedWithMultiplier(
                Context.StatusController.SpeedManager.SpeedWhileRunningMultiplier);
        }
        else
        {
            Context.MovementModel.CurrentSpeed = Context.StatusController.SpeedManager.Speed;
        }

        // Set vertical velocity for jump
        Context.MovementModel.VerticalVelocity = isRunning ?
            Context.MovementModel.JumpForce * 1.75f :
            Context.MovementModel.JumpForce;

        // Consume stamina for jumping
        if (Context.MovementModel.ShouldConsumeStamina)
        {
            Context.StatusController.StaminaManager.ConsumeStamina(Context.MovementModel.AmountOfJumpStaminaCost);
        }
    }

    public override void ExitState()
    {
        // Reset vertical velocity when landing
        Context.MovementModel.VerticalVelocity = 0f;
    }

    public override void UpdateState()
    {
        // Apply gravity
        Context.MovementModel.VerticalVelocity += Context.MovementModel.Gravity * Context.MovementModel.GravityMultiplier * Time.deltaTime;

        // Move player with both horizontal and vertical movement
        Vector3 currentDirection = Context.MovementModel.Direction;
        RotatePlayerByCameraAngle(ref currentDirection);

        float speedAfterWeight = Context.StatusController.WeightManager.CalculateSpeedBasedOnWeight(Context.MovementModel.CurrentSpeed);

        Vector3 horizontalMovement = speedAfterWeight * currentDirection * Time.deltaTime;
        Vector3 verticalMovement = new Vector3(0, Context.MovementModel.VerticalVelocity, 0) * Time.deltaTime;

        Context.MovementModel.Controller.Move(horizontalMovement + verticalMovement);
    }

    public override MovementStateMachine.EMovementState GetNextState()
    {
        // Stay in jumping state while not grounded
        if (!Context.MovementController.IsGrounded())
            return StateKey;

        // Once grounded, transition based on input
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;
        if (TriggeredSprint() && IsMoving()) return MovementStateMachine.EMovementState.Running;
        if (TriggeredCrouch() && IsMoving()) return MovementStateMachine.EMovementState.Crouching;
        if (IsMoving()) return MovementStateMachine.EMovementState.Walking;

        return MovementStateMachine.EMovementState.Idle;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }
}