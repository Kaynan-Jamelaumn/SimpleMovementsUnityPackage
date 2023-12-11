using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerZoom : PlayerCamera
{

    [Header("Zoom parameters")]
    [SerializeField] private float timeToZoom = 0.3f;
    [SerializeField] private float zoomFOV = 30f; 
    private float defaultFOV;//gets the ´fov of the current camera when the zoom is called
    private Coroutine zoomRoutine;


    public void OnZoom(InputAction.CallbackContext value)
    {
        if (value.started && zoomRoutine == null) //checking if the zoom routine exists so the field of view does not get glitched upon zoon deactivation and activation
        {

            defaultFOV = cameraList[currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView;//get the current camera fov
            Zoom(true);
        }
        if (value.canceled)
            Zoom(false);
    }
    // Start is called before the first frame update

    public void Zoom(bool zoomType)
    {
        if (zoomRoutine != null)
        {
            StopCoroutine(zoomRoutine);
            zoomRoutine = null;
        }
        zoomRoutine = StartCoroutine(ZoomRoutine(zoomType));
    }
    private IEnumerator ZoomRoutine(bool zoomType)
    {
        float playerFOV = zoomType ? zoomFOV : defaultFOV; // if is zooming receives the zoom fov if is not go back to normal
        float startingFOV = cameraList[currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView; // this to clamp the fov
        float timeLapse = 0;
        while (timeLapse < timeToZoom)
        {
            cameraList[currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView = Mathf.Lerp(startingFOV, playerFOV, timeLapse / timeToZoom);
            timeLapse += Time.deltaTime;
            yield return null;
        }
        cameraList[currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView = playerFOV;
        zoomRoutine = null;
    }

}







//using Cinemachine;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.InputSystem;

//public class PlayerZoom : MonoBehaviour
//{

//    [Header("Zoom parameters")]
//    [SerializeField] private float timeToZoom = 0.3f;
//    [SerializeField] private float zoomFOV = 30f;
//    [SerializeField] private float jumpForce = 9.8f;
//    [Header("PLAYER CAMERA SCRIPT REFERENCE")][Tooltip("PlayerCameraScript")][SerializeField] public PlayerCamera PlayerCamera;
//    private float defaultFOV;
//    private Coroutine zoomRoutine;


//    public void OnZoom(InputAction.CallbackContext value)
//    {
//        if (value.started)
//        {

//            defaultFOV = PlayerCamera.camera[PlayerCamera.currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView;
//            Zoom(true);
//        }
//        if (value.canceled)
//            Zoom(false);
//    }
//    // Start is called before the first frame update

//    public void Zoom(bool zoomType)
//    {
//        if (zoomRoutine != null)
//        {
//            StopCoroutine(zoomRoutine);
//            zoomRoutine = null;
//        }
//        zoomRoutine = StartCoroutine(ZoomRoutine(zoomType));
//    }
//    private IEnumerator ZoomRoutine(bool zoomType)
//    {
//        float playerFOV = zoomType ? zoomFOV : defaultFOV;
//        float startingFOV = PlayerCamera.camera[PlayerCamera.currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView;
//        float timeLapse = 0;
//        while (timeLapse < timeToZoom)
//        {
//            PlayerCamera.camera[PlayerCamera.currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView = Mathf.Lerp(startingFOV, playerFOV, timeLapse / timeToZoom);
//            timeLapse += Time.deltaTime;
//            yield return null;
//        }
//        PlayerCamera.camera[PlayerCamera.currentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView = playerFOV;
//        zoomRoutine = null;
//    }

//}
