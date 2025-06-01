using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementModel : MonoBehaviour
{
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
    [SerializeField] private PlayerState playerState = PlayerState.Idle;

    [Header("Movement Config")]
    [SerializeField] public float SpeedIncrementFactor = 1;

    [SerializeField] private float amountOfSprintStaminaCost;
    [SerializeField] private float amountOfCrouchStaminaCost;

    [Header("Jumping Config")]
    [SerializeField] private float gravity;
    [SerializeField] private float gravityMultiplier;
    [SerializeField] private float jumpForce;
    [SerializeField] private float amountOfJumpStaminaCost;

    [Header("References")]
    [SerializeField] private GameObject playerShellObject;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController controller;
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

    public float timeSinceLastStaminaConsume = 0f;
    public float staminaConsumeInterval = 0.2f;
    public Vector2 Movement2D { get; set; }
    public Vector3 Direction { get; set; }
    public float LastRotation { get; set; }
    public float VerticalVelocity { get; set; }
    public bool actionPressed;
    public float CurrentSpeed;
    public Coroutine SprintCoroutine { get; set; }
    public Coroutine CrouchCoroutine { get; set; }
}