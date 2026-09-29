using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's movement settings and runtime movement data: stamina costs, gravity and jump, and references to the
/// CharacterController and body. Read by the MovementStateMachine states and the PlayerMovementController.
/// </summary>
public class PlayerMovementModel : MonoBehaviour
{
    [Tooltip("Half of the player's height (metres), used by some ground and ceiling checks.")]
    public float playerHalfPoint = 1;
    public enum PlayerState
    {
        Idle,
        Walking,
        Running,
        Crouching,
        Jumping,
        Dashing,
        Rolling
    }
    [Tooltip("Current movement state (read only, set by the movement states).")]
    [SerializeField] private PlayerState playerState = PlayerState.Idle;

    [Header("Movement Config")]
    [Tooltip("How fast the movement speed ramps up. 1 = normal.")]
    [SerializeField] public float SpeedIncrementFactor = 1;

    [Tooltip("Stamina spent per tick while sprinting (only when Should Consume Stamina is on).")]
    [SerializeField] private float amountOfSprintStaminaCost;
    [Tooltip("Stamina spent per tick while crouch-walking (only when Should Consume Stamina is on).")]
    [SerializeField] private float amountOfCrouchStaminaCost;

    [Header("Jumping Config")]
    [Tooltip("Gravity acceleration (m/s², NEGATIVE, e.g. -9.81).")]
    [SerializeField] private float gravity;
    [Tooltip("Multiplies gravity (higher = snappier jumps, faster falls). 1 = real gravity.")]
    [SerializeField] private float gravityMultiplier;
    [Tooltip("Upward speed when jumping (m/s). Jump height ≈ Jump Force² / (2 × |Gravity × Multiplier|). Running jumps use 1.75x.")]
    [SerializeField] private float jumpForce;
    [Tooltip("Stamina spent per jump (only when Should Consume Stamina is on).")]
    [SerializeField] private float amountOfJumpStaminaCost;

    [Header("References")]
    [Tooltip("The object whose position is the player's feet (ground check origin). Usually the player root.")]
    [SerializeField] private GameObject playerShellObject;
    [Tooltip("The transform that rotates with the player (its forward is the facing direction).")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("The CharacterController that moves the player. Empty = found on this object.")]
    [SerializeField] private CharacterController controller;
    [Tooltip("Sprinting, crouching, jumping (and traits like wall climb) spend stamina.")]
    [SerializeField] private bool shouldConsumeStamina;

    private void Awake()
    {
        controller = this.CheckComponent(controller, nameof(controller));
        this.ValidateField(playerTransform, nameof(playerTransform));
        this.ValidateField(playerShellObject, nameof(playerShellObject));
    }

    public PlayerState CurrentPlayerState
    {
        get => playerState;
        set => playerState = value;
    }

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

    /// <summary>Approximate height of a standing jump (metres) with the current settings.</summary>
    public float EstimatedJumpHeight
    {
        get
        {
            float g = Mathf.Abs(gravity * (Mathf.Approximately(gravityMultiplier, 0f) ? 1f : gravityMultiplier));
            return g > 0.01f ? jumpForce * jumpForce / (2f * g) : 0f;
        }
    }

    /// <summary>
    /// Set by traits such as Wall Climb: while true the movement states and the gravity code do not pull the player
    /// down.
    /// </summary>
    public bool SuspendGravity { get; set; }

    [Tooltip("Runtime: time since stamina was last spent (set by the movement states).")]
    public float timeSinceLastStaminaConsume = 0f;
    [Tooltip("Seconds between stamina ticks while sprinting or crouching.")]
    public float staminaConsumeInterval = 0.2f;
    public Vector2 Movement2D { get; set; }
    public Vector3 Direction { get; set; }
    public float LastRotation { get; set; }
    public float VerticalVelocity { get; set; }
    [Tooltip("Runtime: an action button is held (set by the movement states).")]
    public bool actionPressed;
    [Tooltip("Runtime: current horizontal speed (set by the movement states).")]
    public float CurrentSpeed;
    public Coroutine SprintCoroutine { get; set; }
    public Coroutine CrouchCoroutine { get; set; }
}
