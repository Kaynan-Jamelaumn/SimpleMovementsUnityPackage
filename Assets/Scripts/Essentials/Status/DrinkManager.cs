using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class DrinkManager : StatusManager
{
    private HealthManager healthManager;
    [Header("Thirst-related fields")]
    [SerializeField] private float drink;
    [SerializeField] private float maxDrink =100;
    [SerializeField] private float incrementFactor = 15;
    [SerializeField] private float drinkConsumption = 1;
    [SerializeField] private float drinkTickConsumption = 0.5f;
    [SerializeField] private float hpPenaltyWhileThirsty = 1f;
    [SerializeField] private float hpTickPenaltyCooldownWhileThirsty = 1f;
    [SerializeField] private float drinkFactor;
    [SerializeField] private float drinkRemoveFactor;
    [SerializeField] private bool shouldConsumeDrink = true;
    [SerializeField] private bool isRegeneratingDrink;
    [SerializeField] private bool isConsumingDrink;
    [SerializeField] private Image drinkImage;
    private Coroutine drinkConsumptionRoutine;
    private Coroutine drinkPenaltyRoutine;
    //private Coroutine drinkFactorRoutine;
    //private Coroutine drinkRemoveFactorRoutine;
    private List<CoroutineInfo> drinkEffectRoutines = new List<CoroutineInfo>();
    public Coroutine DrinkConsumptionRoutine { get => drinkConsumptionRoutine; set => drinkConsumptionRoutine = value; }
    public Coroutine DrinkPenaltyRoutine { get => drinkPenaltyRoutine; set => drinkPenaltyRoutine = value; }
    public List<CoroutineInfo> DrinkEffectRoutines { get => drinkEffectRoutines; set => drinkEffectRoutines = value; }
    public float Drink { get => drink; set => drink = value; }
    public float MaxDrink { get => maxDrink; set => maxDrink = value; }
    public float IncrementFactor { get => incrementFactor; set => incrementFactor = value; }
    public float DrinkConsumption { get => drinkConsumption; set => drinkConsumption = value; }
    public float DrinkTickConsumption { get => drinkTickConsumption; set => drinkTickConsumption = value; }
    public float HpPenaltyWhileThirsty { get => hpPenaltyWhileThirsty; set => hpPenaltyWhileThirsty = value; }
    public float HpTickPenaltyCooldownWhileThirsty { get => hpTickPenaltyCooldownWhileThirsty; set => hpTickPenaltyCooldownWhileThirsty = value; }
    public float DrinkFactor { get => drinkFactor; set => drinkFactor = value; }
    public float DrinkRemoveFactorProperty { get => drinkRemoveFactor; set => drinkRemoveFactor = value; }
    public bool IsRegeneratingDrink { get => isRegeneratingDrink; set => isRegeneratingDrink = value; }
    public bool ShouldConsumeDrink { get => shouldConsumeDrink; set => shouldConsumeDrink = value; }
    public bool IsConsumingDrink { get => isConsumingDrink; set => isConsumingDrink = value; }
    public Image DrinkImage { get => drinkImage; set => drinkImage = value; }
    private void Awake()
    {
        healthManager = GetComponent<HealthManager>();
    }

    private void Start()
    {
        Drink = MaxDrink;
        ValidateAssignments();
    }

    private void Update()
    {
        if (Drink <= 0 && DrinkPenaltyRoutine == null)
        {
            ApplyThirstPenalty();
        }
        DrinkConsumptionRoutine = ShouldConsumeDrink && !IsConsumingDrink ? StartCoroutine(ConsumeDrinkRoutine()) : null;

    }

    private void ValidateAssignments()
    {
        Assert.IsNotNull(healthManager, "HelathManager is not assigned in healthManager.");
    }
    public void UpdateDrinkBar()
    {
        UpdateBar(Drink, MaxDrink, DrinkImage);
        Drink = Mathf.Clamp(Drink, 0, MaxDrink);
    }
    public void ApplyThirstPenalty()
    {
        DrinkPenaltyRoutine = StartCoroutine(ThirstPenalty());
    }
    public void AddDrink(float amount)
    {
        Drink = Mathf.Min(Drink + DrinkAddFactor(amount), MaxDrink);
    }
    public bool HasEnoughDrink(float amount)
    {
        return Drink - amount >= 0;
    }
    public void ConsumeDrink(float amount)
    {
        if (Drink <= 0) return;
        Drink -= DrinkRemoveFactor(amount);
    }
    public void ModifyMaxDrink(float amount)
    {
        MaxDrink += amount;
    }
    public void ModifyDrinkFactor(float amount)
    {
        DrinkFactor += amount;
    }
    public void ModifyDrinkRemoveFactor(float amount)
    {
        drinkRemoveFactor += amount;
    }
    private float DrinkAddFactor(float amount)
    {
        return ApplyFactor(amount, DrinkFactor);
    }

    private float DrinkRemoveFactor(float amount)
    {
        return ApplyFactor(amount, drinkRemoveFactor);
    }

    public void AddDrinkEffect(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(DrinkEffectRoutines, effectName, drinkAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkEffectRoutine);
    }
    public void AddDrinkAddFactorEffect(string effectName, float drinkFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(DrinkEffectRoutines, effectName, drinkFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkAddFactorEffectRoutine);
    }

    public void AddDrinkRemoveFactorEffect(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(DrinkEffectRoutines, effectName, drinkRemoveFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyDrinkRemoveFactorEffectRoutine);
    }
    public void RemoveDrinkEffect(string effectName = null, Coroutine effectRoutine = null)
    {
        RemoveEffect(DrinkEffectRoutines, effectName, effectRoutine);
    }
    public void StopAllDrinkEffects()
    {
        StopAllEffects(DrinkEffectRoutines);
    }
    public void StopAllDrinkEffectsByType(bool isBuff = true)
    {
        StopAllEffectsByType(DrinkEffectRoutines, isBuff);
    }
    public IEnumerator ApplyDrinkEffectRoutine(string effectName, float drinkAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = drinkAmount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        while (Time.time < Time.time + timeBuffEffect)
        {
            float newAmount = amount;
            if (newAmount > 0) newAmount = DrinkAddFactor(newAmount);
            else newAmount = DrinkRemoveFactor(newAmount);
            Drink += newAmount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        Drink = Mathf.Min(Drink, MaxDrink);
    }
    public IEnumerator ConsumeDrinkRoutine()
    {
        if (IsRegeneratingDrink)
        {
            IsRegeneratingDrink = false;
            yield break;
        }
        IsConsumingDrink = true;

        while (Drink >= 0)
        {
            Drink -= DrinkConsumption;

            if (!IsConsumingDrink)
            {
                IsConsumingDrink = true;
                break;
            }
            yield return new WaitForSeconds(DrinkTickConsumption);
        }
    }

    public IEnumerator ThirstPenalty()
    {
        while (Drink <= 0)
        {
            healthManager.ConsumeHP(HpPenaltyWhileThirsty, true);
            yield return new WaitForSeconds(HpTickPenaltyCooldownWhileThirsty);
        }

        DrinkPenaltyRoutine = null; // Libera a referência para que a rotina possa ser reiniciada
    }
    public IEnumerator ApplyDrinkAddFactorEffectRoutine(string effectName, float drinkAddFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = drinkAddFactorAmount;
        float drinkAddFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                DrinkFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                DrinkFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        DrinkFactor -= drinkAddFactorOriginalAmount;
    }
    public IEnumerator ApplyDrinkRemoveFactorEffectRoutine(string effectName, float drinkRemoveFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = drinkRemoveFactorAmount;
        float drinkRemoveFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                drinkRemoveFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                drinkRemoveFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        drinkRemoveFactor -= drinkRemoveFactorOriginalAmount;
    }
}
