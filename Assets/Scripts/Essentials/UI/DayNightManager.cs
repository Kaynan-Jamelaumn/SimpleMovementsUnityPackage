using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DayNightManager : MonoBehaviour
{
    public static DayNightManager instance;


    [Range(0,1)][SerializeField] public float timeOfDay;
    [SerializeField] public float dayDurationMins;
    [SerializeField] public float nightDurationMins;
    
    
    [SerializeField] public float currentTime;
    [SerializeField] public bool isDayTime;
    [SerializeField] private bool cyclingToNight;

    [SerializeField] private Material dayMat;
    [SerializeField] private Material nightMat;
    [SerializeField] private Material skyBoxMat;
    void Start()
    {

        cyclingToNight = true;
        isDayTime = true;
        skyBoxMat = new Material(dayMat);
        RenderSettings.skybox = skyBoxMat;
    }
    void Update()
    {
        UpdaeTime();
        skyBoxMat.Lerp(dayMat, nightMat, timeOfDay);
        RenderSettings.skybox = skyBoxMat;
    }
    private void UpdaeTime()
    {
        float timeIncrement = Time.deltaTime / (isDayTime ? (dayDurationMins * 60f) : (nightDurationMins * 60f));
        currentTime += cyclingToNight ? timeIncrement : -timeIncrement;
        if (timeOfDay > 0.5f) 
            isDayTime = false;
        else
            isDayTime = true;

        if (timeOfDay <= 0f)
            cyclingToNight = true;
        if (timeOfDay > 1f)
            cyclingToNight = false;

        timeOfDay = currentTime;
    }
}
