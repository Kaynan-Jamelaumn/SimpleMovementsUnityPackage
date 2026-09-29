using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>Roll settings: a short evasive roll (Roll input). Read by the Rolling movement state.</summary>
public class PlayerRollModel : PlayerActionModelBase
{
    [Tooltip("Roll speed multiplier (× the current movement speed).")]
    [SerializeField] private float rollSpeedModifier = 2f;
    [Tooltip("How long the roll lasts (seconds).")]
    [SerializeField] private float rollDuration = 0.25f;
    [Tooltip("Seconds before the player can roll again.")]
    [SerializeField] private float rollCoolDown = 4f;
    [Tooltip("Stamina spent per roll (when Should Consume Stamina is on).")]
    [SerializeField] private float amountOfRollStaminaCost = 15f;
    [Tooltip("Rolling spends stamina and needs enough of it.")]
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