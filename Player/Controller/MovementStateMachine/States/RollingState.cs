using UnityEngine;
public class RollingState : MovementState
{
    public RollingState(MovementContext context, MovementStateMachine.EMovementState estate) : base(context, estate)
    {
        MovementContext Context = context;
    }
    private float startTime;
    public override void EnterState()
    {
        Context.AnimationModel.IsRolling = true;
        Context.StatusController.RollController.Roll();
        startTime = Time.time;


    }
    public override void ExitState()
    {
        Context.AnimationModel.IsRolling = false;
    }
    public override void UpdateState()
    {
        MovePlayer();
    }
    public override
    MovementStateMachine.EMovementState GetNextState()
    {
        if (Time.time - startTime < Context.StatusController.RollModel.RollDuration) return StateKey;


        if (TriggeredJump()) return MovementStateMachine.EMovementState.Jumping;
        if (!IsMoving()) return MovementStateMachine.EMovementState.Idle;
        if (IsMoving()) return MovementStateMachine.EMovementState.Walking;
        if (TriggeredSprint()) return MovementStateMachine.EMovementState.Running;
        if (TriggeredCrouch()) return MovementStateMachine.EMovementState.Crouching;
        if (TriggeredDash()) return MovementStateMachine.EMovementState.Dashing;

        if (!Context.AnimationModel.IsRolling)
            return MovementStateMachine.EMovementState.Idle;



        return StateKey;
    }

    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }


}