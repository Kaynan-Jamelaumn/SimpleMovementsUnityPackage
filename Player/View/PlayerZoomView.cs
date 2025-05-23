using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
public class PlayerZoomView : MonoBehaviour
{
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private PlayerZoomModel model;
    [SerializeField] private PlayerZoomController controller;
    private void Awake()
    {
        controller = this.CheckComponent(controller, nameof(controller));
        model = this.CheckComponent(model, nameof(model));
        cameraController = this.CheckComponent(cameraController, nameof(cameraController));
    }

    private void Start()
    {
        ValidateAsignments();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(controller, "PlayerZoomController is not assigned in controller.");
        Assert.IsNotNull(model, "PlayerZoomModel is not assigned in model.");
        Assert.IsNotNull(cameraController, "PlayerCameraController is not assigned in cameraController.");
    }
    public void OnZoom(InputAction.CallbackContext value)
    {

        if (value.started && model.ZoomRoutine == null) //checking if the zoom routine exists so the field of view does not get glitched upon zoon deactivation and activation
        {

            model.DefaultFOV = cameraController.GetCameraFOV();//get the current camera fov
            controller.Zoom(true);
        }
        if (value.canceled)
            controller.Zoom(false);
    }
}