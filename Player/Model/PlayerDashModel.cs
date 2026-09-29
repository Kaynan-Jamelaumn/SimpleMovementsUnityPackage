using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>Dash settings: a quick burst forward (Dash input). Read by the Dashing movement state.</summary>
public class PlayerDashModel : PlayerActionModelBase
{
    [Tooltip("Dash speed multiplier (× the current movement speed). Distance ≈ Dash Speed × Speed × Dash Duration.")]
    [SerializeField] private float dashSpeed = 35f;
    [Tooltip("How long the dash lasts (seconds).")]
    [SerializeField] private float dashDuration = 1f;
    [Tooltip("Seconds before the player can dash again.")]
    [SerializeField] private float dashCoolDown = 4f;
    [Tooltip("Stamina spent per dash (when Should Consume Stamina is on).")]
    [SerializeField] private float amountOfDashStaminaCost = 10f;
    [Tooltip("Dashing spends stamina and needs enough of it.")]
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