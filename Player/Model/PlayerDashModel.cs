using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class PlayerDashModel : PlayerActionModelBase
{
    [SerializeField] private float dashSpeed = 35f;
    [SerializeField] private float dashDuration = 1f;
    [SerializeField] private float dashCoolDown = 4f;
    [SerializeField] private float amountOfDashStaminaCost = 10f;
    [SerializeField] public new bool ShouldConsumeStamina = true;

    private Coroutine dashRoutine;
    private float lastDashTime = 0f;

    public float DashSpeed { get => dashSpeed; set => dashSpeed = value; }
    public float DashDuration { get => dashDuration; set => dashDuration = value; }
    public Coroutine DashRoutine { get => dashRoutine; set => dashRoutine = value; }

    public override float StaminaCost => amountOfDashStaminaCost;
    public override float CooldownDuration => dashCoolDown;
    public override float LastActionTime { get => lastDashTime; set => lastDashTime = value; }
}