using System;
using UnityEngine;
using UnityEngine.Assertions;

public class PlayerAnimationController : MonoBehaviour
{
    [SerializeField] public PlayerAnimationModel model;
    private PlayerMovementModel movementModel;
    [SerializeField] private MovementStateMachine movementStateMachine;

    private void Awake()
    {
        // Assign models and controllers
        if (model == null) model = GetComponent<PlayerAnimationModel>();
        if (movementModel == null) movementModel = GetComponent<PlayerMovementModel>();
    }
    private void Start()
    {
        ValidateAsignments();
        model.IsWalkingHash = Animator.StringToHash("IsWalking");
        model.IsRunningHash = Animator.StringToHash("IsRunning");

        model.IsJumpingHash = Animator.StringToHash("IsJumping");
        model.IsRollingHash = Animator.StringToHash("IsRolling");
        model.IsDashingHash = Animator.StringToHash("IsDashing");

        model.IsAttackingHash = Animator.StringToHash("IsAttacking");
        model.IsCrouchingHash = Animator.StringToHash("IsCrouching");
        model.MovementXHash = Animator.StringToHash("MovementX");
        model.MovementZHash = Animator.StringToHash("MovementZ");
    }
    private void Update()
    {
        ApplyAnimation();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(model, "PlayerAnimationModel is not assigned in model.");
        Assert.IsNotNull(movementModel, "PlayerMovementModel is not assigned in movementModel.");
    }
    public void ApplyAnimation()
    {
        //model.Anim.SetBool(model., movementStateMachine.CurrentState.StateKey == MovementStateMachine.EMovementState.Walking);
        model.Anim.SetBool(model.IsWalkingHash, movementStateMachine.CurrentState.StateKey == MovementStateMachine.EMovementState.Walking);
        model.Anim.SetBool(model.IsRunningHash, movementStateMachine.CurrentState.StateKey == MovementStateMachine.EMovementState.Running);
        model.Anim.SetBool(model.IsCrouchingHash, movementStateMachine.CurrentState.StateKey == MovementStateMachine.EMovementState.Crouching);
        model.Anim.SetBool(model.IsJumpingHash, movementStateMachine.CurrentState.StateKey == MovementStateMachine.EMovementState.Jumping);
        model.Anim.SetBool(model.IsRollingHash, movementStateMachine.CurrentState.StateKey == MovementStateMachine.EMovementState.Rolling);
        model.Anim.SetBool(model.IsDashingHash, movementStateMachine.CurrentState.StateKey == MovementStateMachine.EMovementState.Dashing);
        //        model.Anim.SetBool(model.IsWalkingHash, movementModel.CurrentPlayerState == PlayerMovementModel.PlayerState.Walking);
        //model.Anim.SetBool(model.IsRunningHash, movementModel.CurrentPlayerState == PlayerMovementModel.PlayerState.Running);
        //model.Anim.SetBool(model.IsCrouchingHash, movementModel.CurrentPlayerState == PlayerMovementModel.PlayerState.Crouching);
        //model.Anim.SetBool(model.IsJumpingHash, movementModel.CurrentPlayerState == PlayerMovementModel.PlayerState.Jumping);

        model.Anim.SetBool(model.IsAttackingHash, model.IsAttacking);
        //model.Anim.SetBool(model.IsRollingHash, model.IsRolling);
        //model.Anim.SetBool(model.IsDashingHash, model.IsDashing);

        model.Anim.SetFloat(model.MovementXHash, movementModel.Movement2D.x);
        model.Anim.SetFloat(model.MovementZHash, movementModel.Movement2D.y);
        //model.Anim.SetBool("isJumping", model.CurrentPlayerState == PlayerState.Jumping); // Adicione esta linha
        //... (handle other animations)
    }
    public void EndAttackAnimation()
    {
        model.IsAttacking = false;
        model.Anim.SetBool("IsAttacking", false);
    }
    public void EndRollAnimation()
    {
        model.IsRolling = false;
        model.Anim.SetBool("IsRolling", false);
    }
    public void EndDashAnimation()
    {
        model.IsDashing = false;
        model.Anim.SetBool("IsDashing", false);
    }

}
