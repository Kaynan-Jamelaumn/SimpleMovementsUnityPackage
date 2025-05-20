using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class PlayerCameraController : MonoBehaviour
{
    [SerializeField] private PlayerCameraModel model;
    private List<GameObject> cameraOrder = new List<GameObject>();
    private int currentIndex = 0;

    private void Awake()
    {
        model = this.CheckComponent(model, nameof(model));
        model.CameraTransform = this.CheckComponent(model.CameraTransform, nameof(model.CameraTransform), searchChildren: true);

        this.ValidateDict(model.CameraDictionary, nameof(model.CameraDictionary), allowEmpty: false);

        foreach (var kvp in model.CameraDictionary)
        {
            if (kvp.Key != null)
            {
                cameraOrder.Add(kvp.Key);
                kvp.Key.SetActive(false);
            }
        }
    }

    private void Start()
    {
        ValidateAsignments();
        if (cameraOrder.Count > 0)
        {
            currentIndex = 0;
            ActivateCamera(cameraOrder[currentIndex]);
        }
    }

    private void Update()
    {
        if (model.CurrentCamera == null) return;

        if (model.CameraDictionary.TryGetValue(model.CurrentCamera, out bool isFirstPerson))
        {
            model.IsFirstPerson = isFirstPerson;
            model.PlayerShouldRotateByCameraAngle = isFirstPerson || (model.IsRightPressed && !isFirstPerson);
        }
    }

    private void ValidateAsignments()
    {
        Assert.IsNotNull(model, "PlayerCameraModel is not assigned in model.");
    }

    public Vector3 DirectionToMoveByCamera(Vector3 direction)
    {
        return Quaternion.AngleAxis(model.CameraTransform.rotation.eulerAngles.y, Vector3.up) * direction;
    }

    private void CursorLocker(bool shouldLock) => Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;

    public void SetCursorLock(bool shouldLock)
    {
        CursorLocker(shouldLock);
        Cursor.visible = !shouldLock;
    }

    public void ChangeCamera()
    {
        if (cameraOrder.Count == 0) return;

        SetCameraActive(model.CurrentCamera, false);
        currentIndex = (currentIndex + 1) % cameraOrder.Count;
        ActivateCamera(cameraOrder[currentIndex]);
    }

    private void ActivateCamera(GameObject camera)
    {
        if (camera == null) return;

        SetCameraActive(camera, true);
        model.CurrentCamera = camera;

        // Modern Cinemachine configuration
        if (camera.TryGetComponent<CinemachineCamera>(out var cmCamera))
        {
            // Input handling should be configured via CinemachineInputHandler component
            // Ensure your camera has CinemachineInputHandler component attached
            // and input actions are set up in the Inspector
        }

        if (model.CameraDictionary.TryGetValue(camera, out bool isFirstPerson))
        {
            SetCursorLock(isFirstPerson);
        }
    }

    public void SetCameraActive(GameObject camera, bool isActive)
    {
        if (camera != null)
        {
            camera.SetActive(isActive);
            if (isActive)
            {
                model.CameraTransform = camera.transform;
                var brain = CinemachineCore.FindPotentialTargetBrain(camera.GetComponent<CinemachineVirtualCameraBase>());
                brain?.ManualUpdate();
            }
        }
    }

    public void InterpolateFOV(float startingFOV, float playerFOV, float timeLapse, float timeToZoom)
    {
        float interpolatedFOV = Mathf.Lerp(startingFOV, playerFOV, timeLapse / timeToZoom);
        SetCameraFOV(interpolatedFOV);
    }

    public void SetCameraFOV(float fov)
    {
        if (model.CurrentCamera != null)
        {
            if (model.CurrentCamera.TryGetComponent<CinemachineCamera>(out var cmCamera))
            {
                cmCamera.Lens.FieldOfView = fov;
            }
            else if (model.CurrentCamera.TryGetComponent<Camera>(out var regularCamera))
            {
                regularCamera.fieldOfView = fov;
            }
        }
    }

    public float GetCameraFOV()
    {
        if (model.CurrentCamera != null)
        {
            if (model.CurrentCamera.TryGetComponent<CinemachineCamera>(out var cmCamera))
            {
                return cmCamera.Lens.FieldOfView;
            }
            else if (model.CurrentCamera.TryGetComponent<Camera>(out var regularCamera))
            {
                return regularCamera.fieldOfView;
            }
        }
        return 60f;
    }
}