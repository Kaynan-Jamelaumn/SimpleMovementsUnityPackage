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
        //Context.MovementModel.CurrentSpeed = Context.MovementModel.Speed;
        Context.MovementModel.VerticalVelocity = Context.AnimationModel.Anim.GetBool(Context.AnimationModel.IsRunningHash) ? Context.MovementModel.JumpForce * 1.75f : Context.MovementModel.JumpForce;
    }
    public override void ExitState() { }
    public override void UpdateState()
    {
        MovePlayer();
        GetNextState();
    }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (!Context.MovementController.IsGrounded())
            return StateKey;
        if (!IsMoving()) return MovementStateMachine.EMovementState.Idle;
        if (TriggeredSprint()) return MovementStateMachine.EMovementState.Running;
        if (TriggeredCrouch()) return MovementStateMachine.EMovementState.Crouching;
        if (IsMoving()) return MovementStateMachine.EMovementState.Walking;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;
        if (TriggeredRoll()) return MovementStateMachine.EMovementState.Rolling;

        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }


}