using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraView : MonoBehaviour
{
    [SerializeField] private PlayerCameraModel model;
    [SerializeField] private PlayerCameraController controller;

    [Header("Input Settings")]
    [SerializeField] private bool enableCameraShakeInput = true;

    // Input System variables
    private Keyboard keyboard;

    private void Awake()
    {
        controller = this.CheckComponent(controller, nameof(controller));
        model = this.CheckComponent(model, nameof(model));

        // Initialize Input System
        keyboard = Keyboard.current;

        // Subscribe to events
        model.OnCameraChanged += OnCameraChangedHandler;
        model.OnFirstPersonChanged += OnFirstPersonChangedHandler;
        model.OnFOVChanged += OnFOVChangedHandler;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (model != null)
        {
            model.OnCameraChanged -= OnCameraChangedHandler;
            model.OnFirstPersonChanged -= OnFirstPersonChangedHandler;
            model.OnFOVChanged -= OnFOVChangedHandler;
        }
    }

    private void Update()
    {
        // Test camera shake using Input System (remove in production)
        if (enableCameraShakeInput && keyboard != null && keyboard.tKey.wasPressedThisFrame)
        {
            controller.ShakeCamera(2f, 0.5f, 15f);
        }
    }

    // Input callbacks
    public void OnChangeCamera(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        controller.ChangeCamera();
    }
    public void OnLook(InputAction.CallbackContext context)
    {
        if (model.IsFirstPerson)
        {
            Vector2 lookDelta = context.ReadValue<Vector2>();


            // Clamp vertical rotation
            model.CurrentVerticalRotation += lookDelta.y;
            model.CurrentVerticalRotation = Mathf.Clamp(model.CurrentVerticalRotation, model.MaxLookDown, model.MaxLookUp);

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
    public void OnZoom(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            controller.ToggleZoom();
        }
    }

    public void OnSwitchToFirstPerson(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        // Find first available first-person camera
        foreach (var kvp in model.CameraDictionary)
        {
            if (kvp.Value.isFirstPerson)
            {
                controller.SwitchToCamera(kvp.Value.cameraName);
                break;
            }
        }
    }

    public void OnSwitchToThirdPerson(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        // Find first available third-person camera
        foreach (var kvp in model.CameraDictionary)
        {
            if (!kvp.Value.isFirstPerson)
            {
                controller.SwitchToCamera(kvp.Value.cameraName);
                break;
            }
        }
    }

    // Event handlers
    private void OnCameraChangedHandler(GameObject oldCamera, GameObject newCamera)
    {
     //   Debug.Log($"Camera changed from {(oldCamera ? oldCamera.name : "None")} to {newCamera.name}");

    }

    private void OnFirstPersonChangedHandler(bool isFirstPerson)
    {
       // Debug.Log($"First person mode: {isFirstPerson}");

    }

    private void OnFOVChangedHandler(float fov)
    {
    }
}