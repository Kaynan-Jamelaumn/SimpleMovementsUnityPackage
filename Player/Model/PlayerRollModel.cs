using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class PlayerRollModel : PlayerActionModelBase
{
    [SerializeField] private float rollSpeedModifier = 2f;
    [SerializeField] private float rollDuration = 0.25f;
    [SerializeField] private float rollCoolDown = 4f;
    [SerializeField] private float amountOfRollStaminaCost = 15f;
    [SerializeField] public new bool ShouldConsumeStamina = true;

    private float lastRollTime = 0f;
    private Coroutine rollRoutine;

    // Properties
    public float RollSpeedModifier { get => rollSpeedModifier; set => rollSpeedModifier = value; }
    public float RollDuration { get => rollDuration; set => rollDuration = value; }
    public Coroutine RollRoutine { get => rollRoutine; set => rollRoutine = value; }

    // Abstract implementations
    public override float StaminaCost => amountOfRollStaminaCost;
    public override float CooldownDuration => rollCoolDown;
    public override float LastActionTime { get => lastRollTime; set => lastRollTime = value; }
}