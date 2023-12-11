using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCameraModel : MonoBehaviour
{
    [SerializeField] private GameObject[] cameraList;
    [SerializeField] private bool playerShouldRotateByCameraAngle;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private int currentIndex;
    private bool isFirstPerson;

    public GameObject[] CameraList { get => cameraList; set => cameraList = value; }
    public bool PlayerShouldRotateByCameraAngle { get => playerShouldRotateByCameraAngle; set => playerShouldRotateByCameraAngle = value; }
    public Transform CameraTransform { get => cameraTransform; set => cameraTransform = value; }
    public int CurrentIndex { get => currentIndex; set => currentIndex = value; }
    public bool IsFirstPerson { get => isFirstPerson; set => isFirstPerson = value; }



}
