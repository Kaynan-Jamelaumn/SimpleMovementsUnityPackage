using UnityEngine;
using UnityEngine.Assertions;
public class PlayerMovementController : MonoBehaviour
{
    // Models and Controllers
    [SerializeField] private PlayerMovementModel model;
    [SerializeField] private PlayerCameraModel cameraModel;
    [SerializeField] private PlayerCameraController cameraController;

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
        float raycastLength = 0.5f; // Adjust this value based on the game's scale

        Debug.DrawRay(raycastOrigin, Vector3.down * raycastLength, Color.red); // Visualize the ray in the scene

        bool isHit = Physics.Raycast(raycastOrigin, Vector3.down, out RaycastHit hit, raycastLength);

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
        if (IsGrounded() && model.VerticalVelocity < 0.0f)
            model.VerticalVelocity = -1f;
        else
            model.VerticalVelocity += model.Gravity * model.GravityMultiplier * Time.deltaTime;
        model.Controller.Move(Vector3.up * model.VerticalVelocity * Time.deltaTime);
    }


}