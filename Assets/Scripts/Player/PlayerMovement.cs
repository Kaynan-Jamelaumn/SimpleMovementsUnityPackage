using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Config Player")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private float speedWhileRunningMultiplier = 1.5f;
    [SerializeField] private float speedWhileCrouchingMultiplier = 1.25f;
    //[Tooltip("This is the Time for the player rotation - it makes smooth")]
    //[SerializeField] private float smoothTime = 0.05f;

    [Header("Jumping Config")]
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private float gravityMultiplier = 3f;
    [SerializeField] private float jumpForce = 9.8f;
    [Tooltip("YourPlayerObject - because you will need to get the script PlayerCamera")][SerializeField] private GameObject playerShellObject;
    [Tooltip("Main camera")] private PlayerCamera PlayerCamera;

    [Tooltip("Player physical body")][SerializeField] public Transform playerTransform;

    public CharacterController Controller { get; private set; }
    private Animator anim;

    private bool isWalking, isRunning, isCrouching, isJumping = false; // for animation purposes
    public bool IsDashing { get; set; }
    private float verticalVelocity = 0f;
    private Vector2 movement2D;
    private Vector3 direction;
    float lastRotation = 0;

    private bool IsGrounded()
    {
      //  Debug.Log(Controller.isGrounded + "askakaa");
        //return Controller.isGrounded
        float margin = Controller.stepOffset + 0.1f; // It calculates if the character is grounded with making up with some kind of "error margin"
        return Physics.Raycast(transform.position, Vector3.down, margin);
    }

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
        // anim = GetComponent<Animator>(); // in case you want the character to have animations 
    }

    private void Start() => PlayerCamera = playerShellObject.GetComponent<PlayerCamera>();

    void FixedUpdate()
    {
        ApplyGravity();
        ApplyMovement();
    }

    private void LateUpdate() => ApplyRotation();

    public void OnMovement(InputAction.CallbackContext value)
    {
        movement2D = value.ReadValue<Vector2>();
        movement2D.Normalize();
        direction = new Vector3(movement2D.x, 0, movement2D.y);
        direction.Normalize();

    }

    public void OnJump(InputAction.CallbackContext value)
    {
        if (!value.started) return; // O pulo não foi acionado
        if (!IsGrounded()) return; // Não está no chão
        verticalVelocity = jumpForce;
    }

    public void OntRun(InputAction.CallbackContext value)
    {
        isRunning = value.performed;
    }

    public void OnCrouch(InputAction.CallbackContext value)
    {
        isCrouching = value.performed;
    }

    public void OnShouldWalkBasedOnCamera(InputAction.CallbackContext value)
    {
        if (value.started) PlayerCamera.PlayerShouldRotateByCameraAngle = true;

        if (value.canceled) PlayerCamera.PlayerShouldRotateByCameraAngle = false;
    }

    void ApplyMovement()
    {
        isWalking = direction.x != 0 || direction.z != 0;
        float currentSpeed = speed * (isRunning && !isCrouching ? speedWhileRunningMultiplier : isCrouching ? speedWhileCrouchingMultiplier : 1);
        Vector3 currentDirection = direction;
        if (PlayerCamera.PlayerShouldRotateByCameraAngle || PlayerCamera.isFirstPerson)
        {
            currentDirection = PlayerCamera.DirectionToMoveByCamera(direction);
            lastRotation = playerTransform.rotation.eulerAngles.y;
        }
        else { 
            currentDirection = Quaternion.AngleAxis(lastRotation, Vector3.up) * currentDirection;
        }
        Controller.Move(currentDirection * currentSpeed * Time.deltaTime);
        // ApplyAnimation(); If you've created you animations call this function to work with it.
    }

    void ApplyRotation()
    {
        if (movement2D.sqrMagnitude == 0) return;

        if (PlayerCamera.PlayerShouldRotateByCameraAngle || PlayerCamera.isFirstPerson)
            playerTransform.rotation = Quaternion.Euler(0.0f, PlayerCamera.cameraTransform.transform.eulerAngles.y, 0.0f);
    }

    void ApplyGravity()
    {
        if (IsGrounded() && verticalVelocity < 0.0f)
        {
            verticalVelocity = -1f;
        }
        else
        {
            verticalVelocity += gravity * gravityMultiplier * Time.deltaTime;
        }

        Controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    void ApplyAnimation()
    {
        anim.SetBool("isWalking", isWalking);
        //anim.SetBool("isJumping", isJumping);
        anim.SetBool("isRunning", isRunning);
        anim.SetBool("isCrouching", isCrouching);
    }
}
