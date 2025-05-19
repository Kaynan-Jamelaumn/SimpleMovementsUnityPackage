using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class CameraEntry
{
    public GameObject camera;
    public bool isFirstPerson;
}

public class PlayerCameraModel : MonoBehaviour
{
    [SerializeField] private List<CameraEntry> cameraList = new List<CameraEntry>();
    private Dictionary<GameObject, bool> cameraDictionary = new Dictionary<GameObject, bool>();

    [SerializeField] private bool playerShouldRotateByCameraAngle;
    [SerializeField] private Transform cameraTransform;
    private GameObject currentCamera;
    private bool isFirstPerson;
    private bool isRightPressed;

    private void Awake()
    {
        this.ValidateList(cameraList, nameof(cameraList), allowEmpty: false);
        cameraTransform = this.CheckComponent(cameraTransform, nameof(cameraTransform));
        InitializeDictionary();
    }

    public bool IsRightPressed
    {
        get => Mouse.current?.rightButton.isPressed ?? false;
        set => isRightPressed = value;
    }

    public Dictionary<GameObject, bool> CameraDictionary
    {
        get
        {
            if (cameraDictionary.Count == 0 && cameraList.Count > 0)
                InitializeDictionary();
            return cameraDictionary;
        }
    }

    public bool PlayerShouldRotateByCameraAngle { get => playerShouldRotateByCameraAngle; set => playerShouldRotateByCameraAngle = value; }
    public Transform CameraTransform { get => cameraTransform; set => cameraTransform = value; }
    public GameObject CurrentCamera { get => currentCamera; set => currentCamera = value; }
    public bool IsFirstPerson { get => isFirstPerson; set => isFirstPerson = value; }

    private void InitializeDictionary()
    {
        cameraDictionary.Clear();
        foreach (var entry in cameraList)
        {
            if (entry.camera != null && !cameraDictionary.ContainsKey(entry.camera))
            {
                cameraDictionary.Add(entry.camera, entry.isFirstPerson);
            }
        }
    }

    public void AddCamera(GameObject camera, bool isFirstPersonCamera)
    {
        if (camera == null) return;

        cameraList.Add(new CameraEntry { camera = camera, isFirstPerson = isFirstPersonCamera });
        if (!cameraDictionary.ContainsKey(camera))
        {
            cameraDictionary.Add(camera, isFirstPersonCamera);
        }
    }
}