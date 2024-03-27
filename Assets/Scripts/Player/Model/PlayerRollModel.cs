using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class PlayerRollModel : PlayerActionModelBase
{
    [SerializeField] private float rollSpeedModifier;
    [SerializeField] private float rollDuration = 0.25f;
    [SerializeField] private float rollCoolDown = 4f;
    [SerializeField] private float amountOfRollStaminaCost;
    private float lastRollTime = 0f;
    private Coroutine rollRoutine;
    [SerializeField] public new bool ShouldConsumeStamina;

    public float RollSpeedModifier { get => rollSpeedModifier; set => rollSpeedModifier = value; }
    public float RollDuration { get => rollDuration; set => rollDuration = value; }

    public float RollCoolDown { get => rollCoolDown; set => rollCoolDown = value; }

    public Coroutine RollRoutine { get => rollRoutine; set => rollRoutine = value; }
    public float LastRollTime { get => lastRollTime; set => lastRollTime = value; }
    public float AmountOfRollStaminaCost { get => amountOfRollStaminaCost; set => amountOfRollStaminaCost = value; }

}
