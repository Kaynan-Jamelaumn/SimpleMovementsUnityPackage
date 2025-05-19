using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the transition between day and night cycles in the game.
/// </summary>
public class DayNightManager : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of the DayNightManager.
    /// </summary>
    public static DayNightManager instance;

    /// <summary>
    /// Current time of day represented as a value between 0 and 1 (0 = start of day, 1 = end of night).
    /// </summary>
    [Range(0, 1)]
    [SerializeField]
    [Tooltip("Current time of day represented as a value between 0 and 1 (0 = start of day, 1 = end of night)")]
    public float timeOfDay;

    /// <summary>
    /// Duration of the day in minutes.
    /// </summary>
    [SerializeField]
    [Tooltip("Duration of the day in minutes")]
    public float dayDurationMins;

    /// <summary>
    /// Duration of the night in minutes.
    /// </summary>
    [SerializeField]
    [Tooltip("Duration of the night in minutes")]
    public float nightDurationMins;

    /// <summary>
    /// Current time tracked by the manager.
    /// </summary>
    [SerializeField]
    [Tooltip("Current time tracked by the manager")]
    public float currentTime;

    /// <summary>
    /// Indicates if it is currently daytime.
    /// </summary>
    [SerializeField]
    [Tooltip("Is it currently daytime?")]
    public bool isDayTime;

    /// <summary>
    /// Indicates if the cycle is transitioning to night.
    /// </summary>
    [SerializeField]
    [Tooltip("Is the cycle transitioning to night?")]
    private bool cyclingToNight;

    /// <summary>
    /// Material used for the daytime skybox.
    /// </summary>
    [SerializeField]
    [Tooltip("Material used for daytime skybox")]
    private Material dayMat;

    /// <summary>
    /// Material used for the nighttime skybox.
    /// </summary>
    [SerializeField]
    [Tooltip("Material used for nighttime skybox")]
    private Material nightMat;

    /// <summary>
    /// Material used for the current skybox.
    /// </summary>
    [SerializeField]
    [Tooltip("Material used for the current skybox")]
    private Material skyBoxMat;

    /// <summary>
    /// Initializes the DayNightManager.
    /// </summary>
    void Start()
    {
        cyclingToNight = true;
        isDayTime = true;
        skyBoxMat = new Material(dayMat); // Initialize skybox material to daytime material
        RenderSettings.skybox = skyBoxMat; // Apply the skybox material
    }

    /// <summary>
    /// Updates the time of day and the skybox material.
    /// </summary>
    void Update()
    {
        UpdateTime(); // Update the time of day
        skyBoxMat.Lerp(dayMat, nightMat, timeOfDay); // Lerp between day and night materials based on the time of day
        RenderSettings.skybox = skyBoxMat; // Apply the updated skybox material
    }

    /// <summary>
    /// Updates the current time and manages the transition between day and night.
    /// </summary>
    private void UpdateTime()
    {
        // Calculate the time increment based on whether it's day or night
        float timeIncrement = Time.deltaTime / (isDayTime ? (dayDurationMins * 60f) : (nightDurationMins * 60f));
        currentTime += cyclingToNight ? timeIncrement : -timeIncrement;

        // Update isDayTime based on the time of day
        if (timeOfDay > 0.5f)
            isDayTime = false;
        else
            isDayTime = true;

        // Update cyclingToNight based on the time of day
        if (timeOfDay <= 0f)
            cyclingToNight = true;
        if (timeOfDay > 1f)
            cyclingToNight = false;

        timeOfDay = currentTime; // Set the timeOfDay to the updated currentTime
    }
}
