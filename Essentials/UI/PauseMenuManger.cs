using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenuManger : MonoBehaviour
{
    bool isPaused;
    public GameObject BindingMenu;
    void Start()
    {
        isPaused = false;
    }
    public void OnPause(InputAction.CallbackContext ctxt)
    {
        if (ctxt.performed)
        {
             isPaused = !isPaused;
            if (isPaused)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                BindingMenu.SetActive(true); 
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                BindingMenu.SetActive(false);
            }
        }
    }
    public void OnOk()
    {
        Time.timeScale = 1f;
        BindingMenu.SetActive(false);
    }
}
