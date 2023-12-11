using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem;

public class PlayerDash : MonoBehaviour
{
    [SerializeField] private float dashSpeed = 35;
    [SerializeField] private float dashDuration = 0.25f;
    [SerializeField] private float dashCoolDown = 4f;
    private float lastDashTime = 0f;
    [SerializeField] private PlayerMovement PlayerMovement;
 

    public void OnDash(InputAction.CallbackContext value)
    {
        if (!value.started) return; // O dash não foi acionado
        if (Time.time - lastDashTime > dashCoolDown)
        {
            StartCoroutine(DashRoutine());
            lastDashTime = Time.time;
        }
    }

    public IEnumerator DashRoutine()
    {
        PlayerMovement.IsDashing = true;
        float startTime = Time.time;

        while (Time.time - startTime < dashDuration)
        {
            Vector3 current = PlayerMovement.playerTransform.rotation * Vector3.forward; //pega a rotação do body do player
            PlayerMovement.Controller.Move(current * dashSpeed * Time.deltaTime);
            yield return null;
        }

        PlayerMovement.IsDashing = false;
    }
}
