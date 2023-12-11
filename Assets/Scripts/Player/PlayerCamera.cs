using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    public GameObject[] cameraList;
    public bool PlayerShouldRotateByCameraAngle = false;
    [Tooltip("As It's the main camera that rotates (cinemachinebrain), it should be the main camera")]
    public Transform cameraTransform;
    public int currentIndex;
    public bool isFirstPerson = false;

    private void Awake() { CursorLocker(false); }

    //protected void Start() => currentIndex = 1;

    private void Start() { currentIndex = 1; }
    private void Update()
    {
        isFirstPerson = currentIndex == 1 ? true : false;
    }
    private void CursorLocker(bool shouldLock) => Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;

    // protected void Awake() => CursorLocker(false);
    private void OnChangeCamera(InputAction.CallbackContext value)
    {
        if (!value.started) return;
        cameraList[currentIndex].SetActive(false);
        currentIndex = (currentIndex + 1) % cameraList.Length;
        if (currentIndex < 0) // Handle negative index.
            currentIndex += cameraList.Length;
        cameraList[currentIndex].SetActive(true);
    }

    public Vector3 DirectionToMoveByCamera(Vector3 direction)
    {
            return Quaternion.AngleAxis(cameraTransform.transform.rotation.eulerAngles.y, Vector3.up) * direction;
    }
}
