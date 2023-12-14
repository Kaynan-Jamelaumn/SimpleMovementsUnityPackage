using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerZoomView : MonoBehaviour
{
    private PlayerCameraController cameraController;
    private PlayerZoomModel model;
    private PlayerZoomController controller;
    private void Awake()
    {
        controller = GetComponent<PlayerZoomController>();
        model = GetComponent<PlayerZoomModel>();
        cameraController = GetComponent<PlayerCameraController>();
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
