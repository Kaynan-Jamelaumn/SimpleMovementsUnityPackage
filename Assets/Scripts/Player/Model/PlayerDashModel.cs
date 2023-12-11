using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDashModel : MonoBehaviour
{
    [SerializeField] private float dashSpeed = 35;
    [SerializeField] private float dashDuration = 0.25f;
    [SerializeField] private float dashCoolDown = 4f;
    [SerializeField] private float amountOfDashStaminaCost;
    private float lastDashTime = 0f;
    public bool ShouldConsumeStamina;

    public float DashSpeed { get => dashSpeed; set => dashSpeed = value; }
    public float DashDuration { get => dashDuration; set => dashDuration = value; }

    public float DashCoolDown { get => dashCoolDown; set => dashCoolDown = value; }
    public float LastDashTime { get => lastDashTime; set => lastDashTime = value; }
    public float AmountOfDashStaminaCost { get => amountOfDashStaminaCost; set => amountOfDashStaminaCost = value; }

}
