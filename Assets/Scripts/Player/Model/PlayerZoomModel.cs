using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerZoomModel : MonoBehaviour
{
    [Header("Zoom parameters")]
    [SerializeField] private float timeToZoom = 0.3f;
    [SerializeField] private float zoomFOV = 30f;
    private float defaultFOV;//gets the ´fov of the current camera when the zoom is called
    private Coroutine zoomRoutine;

    public float ZoomFOV { get => zoomFOV; set => zoomFOV = value; }
    public float TimeToZoom { get => timeToZoom; set => timeToZoom = value; }

    public float DefaultFOV { get => defaultFOV; set => defaultFOV = value; }
    public Coroutine ZoomRoutine { get => zoomRoutine; set => zoomRoutine = value; }
}
