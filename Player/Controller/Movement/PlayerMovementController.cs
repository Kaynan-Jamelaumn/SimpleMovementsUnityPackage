using UnityEngine;
using UnityEngine.Assertions;
/// <summary>
/// Applies gravity and camera-based rotation to the player, and answers "is the player on the ground?" with a short
/// ray from the feet. Normally the <see cref="MovementStateMachine"/> calls <see cref="ApplyGravity(float)"/> and
/// <see cref="ApplyRotation(float, bool)"/> from its simulation step (so a network replay runs them again in the same
/// order); without a running movement machine they run on their own in FixedUpdate and LateUpdate as before.
/// </summary>
public class PlayerMovementController : MonoBehaviour
{
    // Models and Controllers
    [Tooltip("The player's movement settings. Empty = found on this object.")]
    [SerializeField] private PlayerMovementModel model;
    [Tooltip("Camera settings (rotate with camera, first person). Empty = found on this object.")]
    [SerializeField] private PlayerCameraModel cameraModel;
    [Tooltip("Converts input into camera-relative directions. Empty = found on this object.")]
    [SerializeField] private PlayerCameraController cameraController;

    [Tooltip("Length of the ground check ray below the feet (metres).")]
    [SerializeField, Min(0.05f)] private float groundCheckDistance = 0.5f;
    [Tooltip("Layers counted as ground. Leave out the player's own layer.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    /// <summary>Fills empty references from this object, its children and parents (inspector button).</summary>
    public void AutoAssignReferences()
    {
        if (model == null) model = GetComponentInParent<PlayerMovementModel>();
        if (cameraModel == null) cameraModel = GetComponentInParent<PlayerCameraModel>();
        if (cameraController == null) cameraController = GetComponentInParent<PlayerCameraController>();
        if (model == null) model = GetComponentInChildren<PlayerMovementModel>(true);
        if (cameraModel == null) cameraModel = GetComponentInChildren<PlayerCameraModel>(true);
        if (cameraController == null) cameraController = GetComponentInChildren<PlayerCameraController>(true);
    }

    public float GroundCheckDistance => groundCheckDistance;

    /// <summary>
    /// Set by the <see cref="MovementStateMachine"/> while it applies gravity and rotation from its simulation step:
    /// FixedUpdate and LateUpdate then leave them alone (they would apply twice, outside the simulated tick).
    /// </summary>
    [System.NonSerialized] public bool DrivenBySimulation;

    private void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        cameraModel = this.CheckComponent(cameraModel, nameof(cameraModel));
        cameraController = this.CheckComponent(cameraController, nameof(cameraController));
    }
    void FixedUpdate()
    {
        if (!DrivenBySimulation)
            ApplyGravity(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!DrivenBySimulation)
            ApplyRotation();
    }

    public Vector3 PlayerForwardPosition() => model.PlayerTransform.rotation * Vector3.forward;



    // Check if the player is grounded using a raycast
    public bool IsGrounded()
    {
        Vector3 raycastOrigin = model.PlayerShellObject.transform.position + Vector3.up * model.Controller.stepOffset;
        float raycastLength = groundCheckDistance;

        Debug.DrawRay(raycastOrigin, Vector3.down * raycastLength, Color.red); // Visualize the ray in the scene

        bool isHit = ScenePhysics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastLength, groundLayers, QueryTriggerInteraction.Ignore);

        return isHit;
    }


    /// <summary>Turns the body to the active camera's yaw while moving in rotate-with-camera mode (reads the camera).</summary>
    public void ApplyRotation()
    {
        if (cameraModel == null || cameraModel.CameraTransform == null)
            return;
        ApplyRotation(cameraModel.CameraTransform.eulerAngles.y, cameraModel.PlayerShouldRotateByCameraAngle || cameraModel.IsFirstPerson);
    }

    /// <summary>
    /// Turns the body to <paramref name="cameraYaw"/> (degrees) while the player moves with
    /// <paramref name="rotateWithCamera"/> on - the camera values come with the movement input, so the server and a
    /// replay turn the body exactly like the owner's machine did.
    /// </summary>
    public void ApplyRotation(float cameraYaw, bool rotateWithCamera)
    {
        if (model.Movement2D.sqrMagnitude == 0) return;

        if (rotateWithCamera)
            model.PlayerTransform.rotation = Quaternion.Euler(0.0f, cameraYaw, 0.0f);
    }

    /// <summary>
    /// One gravity step of <paramref name="dt"/> seconds: on the ground the vertical speed rests at -1 (keeps the
    /// controller pressed down), in the air gravity accumulates; then the controller moves vertically.
    /// </summary>
    public void ApplyGravity(float dt)
    {
        if (model.SuspendGravity)
            return; // a trait (wall climb) is holding the player
        if (IsGrounded() && model.VerticalVelocity < 0.0f)
            model.VerticalVelocity = -1f;
        else
            model.VerticalVelocity += model.Gravity * model.GravityMultiplier * dt;
        CharacterController cc = model.Controller;
        // A switched-off controller (travel, teleport in progress) cannot move and would only log a warning.
        if (cc != null && cc.enabled && cc.gameObject.activeInHierarchy)
            cc.Move(Vector3.up * model.VerticalVelocity * dt);
    }


}