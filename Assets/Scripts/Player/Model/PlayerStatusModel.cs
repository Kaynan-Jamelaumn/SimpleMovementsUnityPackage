using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusModel : MonoBehaviour
{
    // Stamina-related fields
    [SerializeField] private float stamina;
    [SerializeField] private float maxStamina;
    [SerializeField] private float staminaRegen = 1;
    [SerializeField] private bool isRegeneratingStamina;
    [SerializeField] private bool isConsumingStamina = false;
    [SerializeField] private Image staminaImage;
    private Coroutine staminaRegenerationRoutine;

    // Health-related fields
    [SerializeField] private float hp;
    [SerializeField] private float maxHp;
    [SerializeField] private float hpRegen = 1;
    [SerializeField] private bool isRegeneratingHp;
    [SerializeField] private bool isConsumingHp = false;
    [SerializeField] private Image hpImage;
    private Coroutine hpRegenerationRoutine;

    // Other player status fields
    [SerializeField] private float maxWeight;
    [SerializeField] private float weight;
    [SerializeField] private float defense;

    // Flag to control regeneration
    public bool ShouldStopStaminaRegeneration;
    public bool ShouldStopHpRegeneration;

    // Properties for external access to coroutines
    public Coroutine StaminaRegenerationRoutine { get => staminaRegenerationRoutine; set => staminaRegenerationRoutine = value; }
    public Coroutine HpRegenerationRoutine { get => hpRegenerationRoutine; set => hpRegenerationRoutine = value; }

    // Properties for external access to status values
    public float Stamina { get => stamina; set => stamina = value; }
    public float MaxStamina { get => maxStamina; set => maxStamina = value; }
    public float StaminaRegen { get => staminaRegen; set => staminaRegen = value; }
    public bool IsRegeneratingStamina { get => isRegeneratingStamina; set => isRegeneratingStamina = value; }
    public bool IsConsumingStamina { get => isConsumingStamina; set => isConsumingStamina = value; }
    public Image StaminaImage { get => staminaImage; set => staminaImage = value; }
    public float Hp { get => hp; set => hp = value; }
    public float MaxHp { get => maxHp; set => maxHp = value; }
    public float HpRegen { get => hpRegen; set => hpRegen = value; }
    public bool IsRegeneratingHp { get => isRegeneratingHp; set => isRegeneratingHp = value; }
    public bool IsConsumingHp { get => isConsumingHp; set => isConsumingHp = value; }
    public Image HpImage { get => hpImage; set => hpImage = value; }
    public float MaxWeight { get => maxWeight; set => maxWeight = value; }
    public float Weight { get => weight; set => weight = value; }
    public float Defense { get => defense; set => defense = value; }
}
