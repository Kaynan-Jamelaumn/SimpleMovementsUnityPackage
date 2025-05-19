using Unity.Cinemachine;
using System.Collections;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Assertions;
public class PlayerZoomController : MonoBehaviour
{
    // Models
    private PlayerZoomModel model;
    private PlayerCameraController cameraController;

    private void Awake()
    {
        // Assign models
        model = GetComponent<PlayerZoomModel>();
        cameraController = GetComponent<PlayerCameraController>();
    }
    private void Start()
    {
        ValidateAsignments();
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(model, "PlayerZoomModel is not assigned in model.");
        Assert.IsNotNull(cameraController, "PlayerCameraController is not assigned in cameraController.");
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
        float playerFOV = zoomType ? model.ZoomFOV : model.DefaultFOV;
        float startingFOV = cameraController.GetCameraFOV();
        float timeLapse = 0;
        while (timeLapse < model.TimeToZoom)
        {
            cameraController.InterpolateFOV(startingFOV, playerFOV, timeLapse, model.TimeToZoom);
            timeLapse += Time.deltaTime;
            yield return null;
        }

        cameraController.SetCameraFOV(playerFOV);

        model.ZoomRoutine = null;
    }

}
