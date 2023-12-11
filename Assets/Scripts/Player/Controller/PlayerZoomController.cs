using Cinemachine;
using System.Collections;
using UnityEngine;

public class PlayerZoomController : MonoBehaviour
{
    // Models
    private PlayerZoomModel model;
    private PlayerCameraModel cameraModel;

    private void Awake()
    {
        // Assign models
        model = GetComponent<PlayerZoomModel>();
        cameraModel = GetComponent<PlayerCameraModel>();
    }

    /// <summary>
    /// Initiates zooming or unzooming based on the specified zoomType.
    /// </summary>
    /// <param name="zoomType">True for zooming, false for unzooming.</param>
    public void Zoom(bool zoomType)
    {
        // Stop the previous zoom routine if it exists
        if (model.ZoomRoutine != null)
        {
            StopCoroutine(model.ZoomRoutine);
            model.ZoomRoutine = null;
        }

        // Start a new zoom routine
        model.ZoomRoutine = StartCoroutine(ZoomRoutine(zoomType));
    }

    private IEnumerator ZoomRoutine(bool zoomType)
    {
        // Determine the target FOV based on zoomType
        float playerFOV = zoomType ? model.ZoomFOV : model.DefaultFOV;

        // Get the starting FOV of the current camera
        float startingFOV = cameraModel.CameraList[cameraModel.CurrentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView;

        // Interpolate FOV over time
        float timeLapse = 0;
        while (timeLapse < model.TimeToZoom)
        {
            // Update the FOV gradually
            cameraModel.CameraList[cameraModel.CurrentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView = Mathf.Lerp(startingFOV, playerFOV, timeLapse / model.TimeToZoom);

            // Increment timeLapse
            timeLapse += Time.deltaTime;

            // Wait for the next frame
            yield return null;
        }

        // Set the final FOV to the target FOV
        cameraModel.CameraList[cameraModel.CurrentIndex].GetComponent<CinemachineVirtualCamera>().m_Lens.FieldOfView = playerFOV;

        // Reset the ZoomRoutine to null
        model.ZoomRoutine = null;
    }
}
