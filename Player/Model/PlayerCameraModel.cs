using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;
using Unity.Cinemachine;
[System.Serializable]
public class CameraEntry
{
    [SerializeField] public GameObject camera;
    [SerializeField] public bool isFirstPerson;
    [SerializeField] public string cameraName;
    [SerializeField] public CameraTransitionSettings transitionSettings;
}

[System.Serializable]
public class CameraTransitionSettings
{
    [SerializeField] public bool useSmoothing = true;
    [SerializeField] public float transitionDuration = 0.5f;
    [SerializeField] public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] public bool maintainVelocity = false;
}

[System.Serializable]
public class CameraShakeSettings
{
    [SerializeField] public float intensity = 1f;
    [SerializeField] public float duration = 0.5f;
    [SerializeField] public float frequency = 10f;
    [SerializeField] public bool diminishOverTime = true;
}

public class PlayerCameraModel : MonoBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private List<CameraEntry> cameraList = new List<CameraEntry>();
    private Dictionary<GameObject, CameraEntry> cameraDictionary = new Dictionary<GameObject, CameraEntry>();

    [Header("Movement Settings")]
    [SerializeField] private bool playerShouldRotateByCameraAngle;
    [SerializeField] private Transform cameraTransform;


    [Header("FOV Settings")]
    [SerializeField] private float defaultFOV = 60f;
    [SerializeField] private float zoomedFOV = 30f;
    [SerializeField] private float fovTransitionSpeed = 5f;

    [Header("Camera Limits")]
    [SerializeField] private float maxLookUp = 80f;
    [SerializeField] private float maxLookDown = -80f;


    [SerializeField] private NoiseSettings defaultNoiseSettings;

    // Private fields
    private GameObject currentCamera;
    private bool isFirstPerson;
    private bool isRightPressed;
    private bool isZooming;
    private float currentVerticalRotation;

    // Events
    public event Action<GameObject, GameObject> OnCameraChanged; // oldCamera, newCamera
    public event Action<bool> OnFirstPersonChanged;
    public event Action<float> OnFOVChanged;

    private void Awake()
    {
        this.ValidateList(cameraList, nameof(cameraList), allowEmpty: false);
        cameraTransform = this.CheckComponent(cameraTransform, nameof(cameraTransform));
        InitializeDictionary();
    }

    // Properties
    public bool IsRightPressed
    {
        get => Mouse.current?.rightButton.isPressed ?? false;
        set => isRightPressed = value;
    }

    public Dictionary<GameObject, CameraEntry> CameraDictionary
    {
        get
        {
            if (cameraDictionary.Count == 0 && cameraList.Count > 0)
                InitializeDictionary();
            return cameraDictionary;
        }
    }

    public float DefaultFOV { get => defaultFOV; set => defaultFOV = value; }
    public float ZoomedFOV { get => zoomedFOV; set => zoomedFOV = value; }
    public float FOVTransitionSpeed { get => fovTransitionSpeed; set => fovTransitionSpeed = value; }
    public float MaxLookUp { get => maxLookUp; set => maxLookUp = value; }
    public float MaxLookDown { get => maxLookDown; set => maxLookDown = value; }
    public bool IsZooming { get => isZooming; set => isZooming = value; }
    public float CurrentVerticalRotation { get => currentVerticalRotation; set => currentVerticalRotation = value; }
    public bool PlayerShouldRotateByCameraAngle { get => playerShouldRotateByCameraAngle; set => playerShouldRotateByCameraAngle = value; }
    public Transform CameraTransform { get => cameraTransform; set => cameraTransform = value; }
    public GameObject CurrentCamera { get => currentCamera; set => currentCamera = value; }
    public bool IsFirstPerson { get => isFirstPerson; set => isFirstPerson = value; }
    public NoiseSettings DefaultNoiseSettings { get => defaultNoiseSettings; set => defaultNoiseSettings = value; }

    private void InitializeDictionary()
    {
        cameraDictionary.Clear();
        foreach (var entry in cameraList)
        {
            if (entry.camera != null && !cameraDictionary.ContainsKey(entry.camera))
            {
                cameraDictionary.Add(entry.camera, entry);
            }
        }
    }

    public void AddCamera(GameObject camera, bool isFirstPersonCamera, string cameraName = "")
    {
        if (camera == null) return;

        var entry = new CameraEntry
        {
            camera = camera,
            isFirstPerson = isFirstPersonCamera,
            cameraName = string.IsNullOrEmpty(cameraName) ? camera.name : cameraName,
            transitionSettings = new CameraTransitionSettings()
        };

        cameraList.Add(entry);
        if (!cameraDictionary.ContainsKey(camera))
        {
            cameraDictionary.Add(camera, entry);
        }
    }

    public CameraEntry GetCameraEntry(GameObject camera)
    {
        return cameraDictionary.TryGetValue(camera, out var entry) ? entry : null;
    }

    // Event triggers
    public void TriggerCameraChanged(GameObject oldCamera, GameObject newCamera)
    {
        OnCameraChanged?.Invoke(oldCamera, newCamera);
    }

    public void TriggerFirstPersonChanged(bool isFirstPerson)
    {
        OnFirstPersonChanged?.Invoke(isFirstPerson);
    }

    public void TriggerFOVChanged(float fov)
    {
        OnFOVChanged?.Invoke(fov);
    }
}
