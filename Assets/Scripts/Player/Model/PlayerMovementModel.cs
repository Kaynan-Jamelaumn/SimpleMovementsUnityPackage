using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementModel : MonoBehaviour
{
    // Movement-related fields
    [SerializeField] private float speed;
    [SerializeField] private float speedWhileRunningMultiplier;
    [SerializeField] private float speedWhileCrouchingMultiplier;
    [SerializeField] private float amountOfSprintStaminaCost;
    [SerializeField] private float amountOfCrouchStaminaCost;

    // Jumping-related fields
    [Header("Jumping Config")]
    [SerializeField] private float gravity;
    [SerializeField] private float gravityMultiplier;
    [SerializeField] private float jumpForce;
    [SerializeField] private float amountOfJumpStaminaCost;

    // Player components and configuration
    [Tooltip("YourPlayerObject - because you will need to get the script PlayerCamera")]
    [SerializeField] private GameObject playerShellObject;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController controller;
    [SerializeField] private bool shouldConsumeStamina;

    // Stamina consumption interval
    public float timeSinceLastStaminaConsume = 0f;
    public float staminaConsumeInterval = 0.2f;

    // Movement state flags
    private bool isWalking, isRunning, isCrouching, isJumping = false;

    // Movement vectors and values
    private Vector2 movement2D;
    private Vector3 direction;
    private float lastRotation = 0;
    private float verticalVelocity = 0f;

    // Coroutines for sprinting and crouching
    private Coroutine sprintCoroutine;
    private Coroutine crouchCoroutine;

    // Animator for character animation
    private Animator anim;

    // Property for dash state
    public bool IsDashing { get; set; }

    // Properties for accessing the fields
    public float Speed { get => speed; set => speed = value; }
    public float SpeedWhileRunningMultiplier { get => speedWhileRunningMultiplier; set => speedWhileRunningMultiplier = value; }
    public float SpeedWhileCrouchingMultiplier { get => speedWhileCrouchingMultiplier; set => speedWhileCrouchingMultiplier = value; }
    public float AmountOfSprintStaminaCost { get => amountOfSprintStaminaCost; set => amountOfSprintStaminaCost = value; }
    public float AmountOfCrouchStaminaCost { get => amountOfCrouchStaminaCost; set => amountOfCrouchStaminaCost = value; }
    public float Gravity { get => gravity; set => gravity = value; }
    public float GravityMultiplier { get => gravityMultiplier; set => gravityMultiplier = value; }
    public float JumpForce { get => jumpForce; set => jumpForce = value; }
    public float AmountOfJumpStaminaCost { get => amountOfJumpStaminaCost; set => amountOfJumpStaminaCost = value; }
    public GameObject PlayerShellObject { get => playerShellObject; set => playerShellObject = value; }
    public Transform PlayerTransform { get => playerTransform; set => playerTransform = value; }
    public CharacterController Controller { get => controller; set => controller = value; }
    public bool ShouldConsumeStamina { get => shouldConsumeStamina; set => shouldConsumeStamina = value; }
    public bool IsWalking { get => isWalking; set => isWalking = value; }
    public bool IsRunning { get => isRunning; set => isRunning = value; }
    public bool IsCrouching { get => isCrouching; set => isCrouching = value; }
    public bool IsJumping { get => isJumping; set => isJumping = value; }
    public Vector2 Movement2D { get => movement2D; set => movement2D = value; }
    public Vector3 Direction { get => direction; set => direction = value; }
    public float LastRotation { get => lastRotation; set => lastRotation = value; }
    public float VerticalVelocity { get => verticalVelocity; set => verticalVelocity = value; }
    public Coroutine SprintCoroutine { get => sprintCoroutine; set => sprintCoroutine = value; }
    public Coroutine CrouchCoroutine { get => crouchCoroutine; set => crouchCoroutine = value; }
    public Animator Anim { get => anim; set => anim = value; }
}
