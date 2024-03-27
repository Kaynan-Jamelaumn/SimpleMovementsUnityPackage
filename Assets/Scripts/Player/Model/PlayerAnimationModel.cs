using System;
using UnityEngine;

public class PlayerAnimationModel : MonoBehaviour
{

    int isWalkingHash;
    int isRunningHash;
    int isCrouchingHash;
    int isDashingHash;
    int isRollingHash;
    int isJumpingHash;
    int isAttackingHash;
    int movementXHash;
    int movementZHash;

    [SerializeField] private Animator anim;

    private bool isAttacking = false;
    private bool isDashing = false;
    private bool isRolling = false;
    public bool IsAttacking { get => isAttacking; set => isAttacking = value; }
    public bool IsDashing { get => isDashing; set => isDashing = value; }
    public bool IsRolling { get => isRolling; set => isRolling = value; }
    public int IsWalkingHash { get => isWalkingHash; set => isWalkingHash = value; }
    public int IsDashingHash { get => isDashingHash; set => isDashingHash = value; }
    public int IsRollingHash { get => isRollingHash; set => isRollingHash = value; }
    public int IsRunningHash { get => isRunningHash; set => isRunningHash = value; }
    public int IsCrouchingHash { get => isCrouchingHash; set => isCrouchingHash = value; }
    public int MovementXHash { get => movementXHash; set => movementXHash = value; }
    public int MovementZHash { get => movementZHash; set => movementZHash = value; }
    public int IsJumpingHash { get => isJumpingHash; set => isJumpingHash = value; }
    public int IsAttackingHash { get => isAttackingHash; set => isAttackingHash = value; }
    public Animator Anim { get => anim; set => anim = value; }
}

