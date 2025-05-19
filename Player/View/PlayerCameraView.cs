using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class PlayerCameraView : MonoBehaviour
{
    [SerializeField] private PlayerCameraModel model;
    [SerializeField] private PlayerCameraController controller;

    private void Awake()
    {
        controller = this.CheckComponent(controller, nameof(controller));
        model = this.CheckComponent(model, nameof(model));
    }

    private void Start()
    {
        ValidateAsignments();
    }

    private void ValidateAsignments()
    {
        Assert.IsNotNull(controller, "PlayerCameraController is not assigned in controller.");
        Assert.IsNotNull(model, "PlayerCameraModel is not assigned in model.");
    }

    public void OnChangeCamera(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        controller.ChangeCamera();
    }

    public void OnShouldWalkBasedOnCamera(InputAction.CallbackContext value)
    {
        //model.PlayerShouldRotateByCameraAngle = value.performed;
    }

    // New method for look input
    public void OnLook(InputAction.CallbackContext context)
    {
        if (model.IsFirstPerson)
        {
            Vector2 lookDelta = context.ReadValue<Vector2>();
            CinemachineCore.GetInputAxis = (axisName) => {
                return axisName switch
                {
                    "Mouse X" => lookDelta.x,
                    "Mouse Y" => lookDelta.y,
                    _ => 0
                };
            };
        }
    }
}