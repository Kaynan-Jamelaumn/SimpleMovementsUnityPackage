using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
public class PlayerCameraView : MonoBehaviour
{
    private PlayerCameraModel model;
    private PlayerCameraController controller;
    private void Awake()
    {
        controller = GetComponent<PlayerCameraController>();
        model = GetComponent<PlayerCameraModel>();
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

    // Input system callback for walking based on camera angle
    public void OnShouldWalkBasedOnCamera(InputAction.CallbackContext value)
    {
        // Set the flag to rotate the player based on camera angle
        model.PlayerShouldRotateByCameraAngle = value.performed;
    }

}
