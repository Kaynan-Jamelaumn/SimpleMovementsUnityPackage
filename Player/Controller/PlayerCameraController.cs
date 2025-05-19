using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
public class PlayerCameraController : MonoBehaviour
{
    // Reference to the camera model
    private PlayerCameraModel model;

    // Initialization method
    private void Awake()
    {
        // Get reference to the camera model
        model = GetComponent<PlayerCameraModel>();

        // Check if CameraTransform is not set, attempt to find the main camera
        if (model.CameraTransform == null)
        {
            GameObject mainCameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCameraObject != null)
                model.CameraTransform = mainCameraObject.transform;
            
            else
                Debug.LogError("Main camera not found!");
            
        }
    }

    // Start method
    private void Start()
    {
        ValidateAsignments();
        // Set initial cursor lock state and camera index
      //  SetCursorLock(false);
        model.CurrentIndex = 1;
    }

    // Update method
    private void Update()
    {
        // Update the first-person state based on the current index
        model.IsFirstPerson = model.CurrentIndex == 1;
    }
    private void ValidateAsignments()
    {
        Assert.IsNotNull(model, "PlayerCameraModel is not assigned in model.");
    }
    // Calculate movement direction relative to the camera
    public Vector3 DirectionToMoveByCamera(Vector3 direction)
    {
        return Quaternion.AngleAxis(model.CameraTransform.rotation.eulerAngles.y, Vector3.up) * direction;
    }

    // Set cursor lock state
    private void CursorLocker(bool shouldLock) => Cursor.lockState = CursorLockMode.Locked;
  //  private void CursorLocker(bool shouldLock) => Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;

    // Public method to set cursor lock state
    public void SetCursorLock(bool shouldLock)
    {
        CursorLocker(shouldLock);
    }

    // Set the active state of a camera GameObject
    public void SetCameraActive(GameObject camera, bool isActive)
    {
        camera.SetActive(isActive);
    }

    // Change to the next camera in the camera list
    public void ChangeCamera()
    {
        // Deactivate the current camera
        SetCameraActive(model.CameraList[model.CurrentIndex], false);

        // Increment the camera index and wrap around the list
        model.CurrentIndex = (model.CurrentIndex + 1) % model.CameraList.Length;
        if (model.CurrentIndex < 0)
            model.CurrentIndex += model.CameraList.Length;

        // Activate the new camera
        SetCameraActive(model.CameraList[model.CurrentIndex], true);
    }


    public void InterpolateFOV(float startingFOV, float playerFOV, float timeLapse, float timeToZoom)
    {
        float interpolatedFOV = Mathf.Lerp(startingFOV, playerFOV, timeLapse / timeToZoom);
        SetCameraFOV(interpolatedFOV);
    }

    public void SetCameraFOV(float fov)
    {
        model.CameraList[model.CurrentIndex]
            .GetComponent<CinemachineCamera>().Lens.FieldOfView = fov;
    }

    public float GetCameraFOV()
    {
        return model.CameraList[model.CurrentIndex].GetComponent<CinemachineCamera>().Lens.FieldOfView;

    }
}
