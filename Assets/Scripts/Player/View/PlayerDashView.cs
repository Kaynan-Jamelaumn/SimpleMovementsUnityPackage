using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
public class PlayerDashView : MonoBehaviour
{
    private PlayerDashController controller;
    public PlayerDashModel model;
    private void Awake()
    {
        controller = GetComponent<PlayerDashController>();
        model = GetComponent<PlayerDashModel>();
    }
    private void Start()
    {
        ValidateAsignments();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(controller, "PlayerDashController is not assigned in controller.");
        Assert.IsNotNull(model, "PlayerDashModel is not assigned in model.");
    }
    public void OnDash(InputAction.CallbackContext value)
    {
        //if (!value.started || model.DashRoutine != null ) return;
        //controller.Dash();
    }
}
