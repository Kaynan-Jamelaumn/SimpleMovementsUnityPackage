using UnityEngine;
using UnityEngine.Assertions;
/// <summary>
/// Applies gravity (FixedUpdate) and camera-based rotation (LateUpdate) to the player, and answers "is the player on
/// the ground?" with a short ray from the feet.
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

    private void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        cameraModel = this.CheckComponent(cameraModel, nameof(cameraModel));
        cameraController = this.CheckComponent(cameraController, nameof(cameraController));
    }
    void FixedUpdate() => ApplyGravity();
    private void LateUpdate() => ApplyRotation();
    public Vector3 PlayerForwardPosition() => model.PlayerTransform.rotation * Vector3.forward;



    // Check if the player is grounded using a raycast
    public bool IsGrounded()
    {
        Vector3 raycastOrigin = model.PlayerShellObject.transform.position + Vector3.up * model.Controller.stepOffset;
        float raycastLength = groundCheckDistance;

        Debug.DrawRay(raycastOrigin, Vector3.down * raycastLength, Color.red); // Visualize the ray in the scene

        bool isHit = Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastLength, groundLayers, QueryTriggerInteraction.Ignore);

        return isHit;
    }


    // Apply player rotation
    public void ApplyRotation()
    {
        if (model.Movement2D.sqrMagnitude == 0) return;

        if (cameraModel.PlayerShouldRotateByCameraAngle || cameraModel.IsFirstPerson)
            model.PlayerTransform.rotation = Quaternion.Euler(0.0f, cameraModel.CameraTransform.transform.eulerAngles.y, 0.0f);
    }

    // Apply gravity to the player
    private void ApplyGravity()
    {
        if (model.SuspendGravity)
            return; // a trait (wall climb) is holding the player
        if (IsGrounded() && model.VerticalVelocity < 0.0f)
            model.VerticalVelocity = -1f;
        else
            model.VerticalVelocity += model.Gravity * model.GravityMultiplier * Time.deltaTime;
        model.Controller.Move(Vector3.up * model.VerticalVelocity * Time.deltaTime);
    }


}