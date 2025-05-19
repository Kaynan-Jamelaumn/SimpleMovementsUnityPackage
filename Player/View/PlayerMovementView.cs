using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class PlayerMovementView : MonoBehaviour
{
    // Reference to movement components
    [SerializeField] private PlayerMovementModel model;
    // Initialization method
    private void Awake()
    {
        model = GetComponent<PlayerMovementModel>();
    }
    private void Start()
    {
        ValidateAsignments();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(model, "PlayerMovementModel is not assigned in model.");

    }
    // Input system callback for movement
    public void OnMovement(InputAction.CallbackContext value)
    {
        // Read and normalize 2D movement input
        model.Movement2D = value.ReadValue<Vector2>();
        model.Movement2D.Normalize();

        // Set movement direction in the player's local space
        model.Direction = new Vector3(model.Movement2D.x, 0, model.Movement2D.y);
        model.Direction.Normalize();
    }

}