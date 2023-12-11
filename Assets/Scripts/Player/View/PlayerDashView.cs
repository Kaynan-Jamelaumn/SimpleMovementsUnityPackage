using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDashView : MonoBehaviour
{
    private PlayerDashController controller;
    private void Awake()
    {
        controller = GetComponent<PlayerDashController>();
    }
    public void OnDash(InputAction.CallbackContext value)
    {
        if (!value.started) return; 
        controller.Dash();
    }
}
