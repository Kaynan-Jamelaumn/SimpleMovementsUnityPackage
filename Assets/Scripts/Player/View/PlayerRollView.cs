
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
public class PlayerRollView : MonoBehaviour
{
    private PlayerRollController controller;
    public PlayerRollModel model;
    private void Awake()
    {
        controller = GetComponent<PlayerRollController>();
        model = GetComponent<PlayerRollModel>();
    }
    private void Start()
    {
        ValidateAsignments();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(controller, "PlayerRollController is not assigned in controller.");
        Assert.IsNotNull(model, "PlayerRollModel is not assigned in model.");
    }
    public void OnRoll(InputAction.CallbackContext value)
    {
        //if (!value.started || model.RollRoutine != null ) return;
        //controller.Roll();
    }
}
