using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusModel : MonoBehaviour
{
    [Tooltip("Isso é um exemplo de tool tip.")]
    // Stamina-related fields
    [Header(" Stamina-related fields")]
    [SerializeField] private float stamina;
    [SerializeField] private float maxStamina;
    [SerializeField] private float staminaRegen = 1;
    [SerializeField] private float staminaTickRegen= 0.5f;
    [SerializeField] private bool isRegeneratingStamina;
    [SerializeField] private bool isConsumingStamina;
    [SerializeField] private Image staminaImage;
    private Coroutine staminaRegenerationRoutine;
    private List<Coroutine> staminaBuffRoutines;

    // Health-related fields
    [Header("Health-related fields")]
    [SerializeField] private float hp;
    [SerializeField] private float maxHp;
    [SerializeField] private float hpRegen = 1;
    [SerializeField] private float hpTickRegen = 0.5f;
    [SerializeField] private bool isRegeneratingHp;
    [SerializeField] private bool isConsumingHp;
    [SerializeField] private Image hpImage;
    private Coroutine hpRegenerationRoutine;
    private List<Coroutine> hpBuffRoutines;

    // Hunger-related fields
    [Header("Hunger-related fields")]
    [SerializeField] private float food;
    [SerializeField] private float maxFood;
    [SerializeField] private float foodConsumption = 1;
    [SerializeField] private float foodTickConsumption = 0.5f;
    [SerializeField] private bool shouldConsumeFood;
    [SerializeField] private bool isRegeneratingFood;
    [SerializeField] private bool isConsumingFood;
    [SerializeField] private Image foodImage;
    private Coroutine foodConsumptionRoutine;
    private List<Coroutine> foodBuffRoutines;
    // Thirst-related fields
    [Header("Thirst-related fields")]
    [SerializeField] private float drink;
    [SerializeField] private float maxDrink;
    [SerializeField] private float drinkConsumption = 1;
    [SerializeField] private float drinkTickConsumption = 0.5f;
    [SerializeField] private bool shouldConsumeDrink;
    [SerializeField] private bool isRegeneratingDrink;
    [SerializeField] private bool isConsumingDrink;
    [SerializeField] private Image drinkImage;
    private Coroutine drinkConsumptionRoutine;
    private List<Coroutine> drinkBuffRoutines;

    // Other player status fields
    [Header("Other player status fields")]
    [SerializeField] private float maxWeight;
    [SerializeField] private float weight;
    [SerializeField] private float defense;

    // Flag to control regeneration
    public bool ShouldStopStaminaRegeneration;
    public bool ShouldStopHpRegeneration;

    // Properties for external access to coroutines
    public Coroutine StaminaRegenerationRoutine { get => staminaRegenerationRoutine; set => staminaRegenerationRoutine = value; }
    public Coroutine HpRegenerationRoutine { get => hpRegenerationRoutine; set => hpRegenerationRoutine = value; }
    public List<Coroutine> StaminaBuffRoutines { get => staminaBuffRoutines; set => staminaBuffRoutines = value; }
    public List<Coroutine> HpBuffRoutines { get => hpBuffRoutines; set => hpBuffRoutines = value; }

    // Properties for external access to status values
    public float Stamina { get => stamina; set => stamina = value; }
    public float MaxStamina { get => maxStamina; set => maxStamina = value; }
    public float StaminaRegen { get => staminaRegen; set => staminaRegen = value; }
    public float StaminaTickRegen { get => staminaTickRegen; set => staminaTickRegen = value; }
    public bool IsRegeneratingStamina { get => isRegeneratingStamina; set => isRegeneratingStamina = value; }
    public bool IsConsumingStamina { get => isConsumingStamina; set => isConsumingStamina = value; }
    public Image StaminaImage { get => staminaImage; set => staminaImage = value; }
    public float Hp { get => hp; set => hp = value; }
    public float MaxHp { get => maxHp; set => maxHp = value; }
    public float HpRegen { get => hpRegen; set => hpRegen = value; }
    public float HpTickRegen { get => hpTickRegen; set => hpTickRegen = value; }
    public bool IsRegeneratingHp { get => isRegeneratingHp; set => isRegeneratingHp = value; }
    public bool IsConsumingHp { get => isConsumingHp; set => isConsumingHp = value; }
    public Image HpImage { get => hpImage; set => hpImage = value; }
    public float Food
    {
        get => food;
        set => food = value;
    }

    public float MaxFood
    {
        get => maxFood;
        set => maxFood = value;
    }

    public float FoodConsumption
    {
        get => foodConsumption;
        set => foodConsumption = value;
    }
    public float FoodTickConsumption
    {
        get => foodTickConsumption;
        set => foodTickConsumption = value;
    }

    public bool IsRegeneratingFood
    {
        get => isRegeneratingFood;
        set => isRegeneratingFood = value;
    } 
    public bool ShouldConsumeFood
    {
        get => shouldConsumeFood;
        set => shouldConsumeFood = value;
    }

    public bool IsConsumingFood
    {
        get => isConsumingFood;
        set => isConsumingFood = value;
    }

    public Image FoodImage
    {
        get => foodImage;
        set => foodImage = value;
    }

    public float Drink
    {
        get => drink;
        set => drink = value;
    }

    public float MaxDrink
    {
        get => maxDrink;
        set => maxDrink = value;
    }

    public float DrinkConsumption
    {
        get => drinkConsumption;
        set => drinkConsumption = value;
    }
    
    public float DrinkTickConsumption
    {
        get => drinkTickConsumption;
        set => drinkTickConsumption = value;
    }

    public bool IsRegeneratingDrink
    {
        get => isRegeneratingDrink;
        set => isRegeneratingDrink = value;
    }
    
    public bool ShouldConsumeDrink
    {
        get => shouldConsumeDrink;
        set => shouldConsumeDrink = value;
    }

    public bool IsConsumingDrink
    {
        get => isConsumingDrink;
        set => isConsumingDrink = value;
    }

    public Image DrinkImage
    {
        get => drinkImage;
        set => drinkImage = value;
    }

    // Coroutine property for external access
    public Coroutine FoodConsumptionRoutine
    {
        get => foodConsumptionRoutine;
        set => foodConsumptionRoutine = value;
    }
    public List<Coroutine> FoodBuffRoutines
    {
        get => foodBuffRoutines;
        set => foodBuffRoutines = value;
    }

    public Coroutine DrinkConsumptionRoutine
    {
        get => drinkConsumptionRoutine;
        set => drinkConsumptionRoutine = value;
    }   
    public List<Coroutine> DrinkBuffRoutines
    {
        get => drinkBuffRoutines;
        set => drinkBuffRoutines = value;
    }
    public float MaxWeight { get => maxWeight; set => maxWeight = value; }
    public float Weight { get => weight; set => weight = value; }
    public float Defense { get => defense; set => defense = value; }
}
