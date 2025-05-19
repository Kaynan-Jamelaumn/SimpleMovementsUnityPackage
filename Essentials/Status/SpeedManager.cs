using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpeedManager : MonoBehaviour
{

    [SerializeField] private float speed;
    [SerializeField] private float speedWhileRunningMultiplier;
    [SerializeField] private float incrementFactor = 15;
    public float Speed { get => speed; set => speed = value; }
    public float IncrementFactor { get => incrementFactor; set => incrementFactor = value; }
    public float SpeedWhileRunningMultiplier { get => speedWhileRunningMultiplier; set => speedWhileRunningMultiplier = value; }
    public void ModifySpeed(float amount)
    {
       Speed += amount;
    }


}

