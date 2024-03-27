using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StaminaManager : StatusManager
{
    [Header("Stamina-related fields")]
    [SerializeField] private float stamina;
    [SerializeField] private float maxStamina = 100;
    [SerializeField] private float incrementFactor = 15;
    [SerializeField] private float staminaRegen = 1;
    [SerializeField] private float staminaTickRegen = 0.5f;
    [SerializeField] private float staminaHealFactor;
    [SerializeField] private float staminaDamageFactor;
    [SerializeField] private bool isRegeneratingStamina;
    [SerializeField] private bool isConsumingStamina;
    [SerializeField] private Image staminaImage;
    private Coroutine staminaRegenerationRoutine;
    //private Coroutine staminaHealFactorRoutine;
    //private Coroutine staminaDamageFactorRoutine;
    //private Coroutine staminaRegenEffectRoutine;
    private List<CoroutineInfo> staminaEffectRoutines = new List<CoroutineInfo>();
    public Coroutine StaminaRegenerationRoutine { get => staminaRegenerationRoutine; set => staminaRegenerationRoutine = value; }
    public List<CoroutineInfo> StaminaEffectRoutines { get => staminaEffectRoutines; set => staminaEffectRoutines = value; }
    public float Stamina { get => stamina; set => stamina = value; }
    public float MaxStamina { get => maxStamina; set => maxStamina = value; }
    public float IncrementFactor { get => incrementFactor; set => incrementFactor = value; }
    public float StaminaRegen { get => staminaRegen; set => staminaRegen = value; }
    public float StaminaTickRegen { get => staminaTickRegen; set => staminaTickRegen = value; }
    public float StaminaHealFactor { get => staminaHealFactor; set => staminaHealFactor = value; }
    public float StaminaDamageFactor { get => staminaDamageFactor; set => staminaDamageFactor = value; }
    public bool IsRegeneratingStamina { get => isRegeneratingStamina; set => isRegeneratingStamina = value; }
    public bool IsConsumingStamina { get => isConsumingStamina; set => isConsumingStamina = value; }
    public Image StaminaImage { get => staminaImage; set => staminaImage = value; }
    private void Start()
    {
        // Initialize player status variables
        Stamina = MaxStamina;
    }
    private void Update()
    {
        if (!IsRegeneratingStamina && Stamina < MaxStamina && !IsConsumingStamina)
        {
            IsRegeneratingStamina = true;
            StaminaRegenerationRoutine = StartCoroutine(RegenerateStaminaRoutine());
        }

    }
    public void UpdateStaminaBar()
    {
        UpdateBar(Stamina, MaxStamina, StaminaImage);
        Stamina = Mathf.Clamp(Stamina, 0, MaxStamina);
    }
    public void AddStamina(float amount)
    {
        Stamina = Mathf.Min(Stamina + StaminaAddFactor(amount), MaxStamina);
    }
    public bool HasEnoughStamina(float amount)
    {
        return Stamina - amount >= 1;
    }
    public void ConsumeStamina(float amount)
    {
        if (Stamina <= 0) return;
        Stamina -= StaminaRemoveFactor(amount);
    }
    public void ModifyMaxStamina(float amount)
    {
        MaxStamina += amount;
    }

    public void ModifyStaminaRegeneration(float amount)
    {
        StaminaRegen += amount;
    }
    public void ModifyStaminaHealFactor(float amount)
    {
        StaminaHealFactor += amount;
    }


    public void ModifyStaminaDamageFactor(float amount)
    {
        StaminaDamageFactor += amount;
    }

    private float StaminaAddFactor(float amount)
    {
        return ApplyFactor(amount, StaminaHealFactor);
    }
    private float StaminaRemoveFactor(float amount)
    {
        return ApplyFactor(amount, StaminaDamageFactor);
    }

    public void AddStaminaEffect(string effectName, float staminaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(StaminaEffectRoutines, effectName, staminaAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaEffectRoutine);
    }
    public void AddStaminaHealFactorEffect(string effectName, float staminaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(StaminaEffectRoutines, effectName, staminaHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaHealFactorEffectRoutine);
    }
    public void AddStaminaDamageFactorEffect(string effectName, float staminaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(StaminaEffectRoutines, effectName, staminaDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaDamageFactorEffectRoutine);
    }
    public void AddStaminaRegenEffect(string effectName, float staminaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(StaminaEffectRoutines, effectName, staminaRegenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyStaminaRegenEffectRoutine);
    }
    public void RemoveStaminaEffect(string effectName = null, Coroutine effectRoutine = null)
    {
        RemoveEffect(StaminaEffectRoutines, effectName, effectRoutine);
    }

    public void StopAllStaminaEffects()
    {
        StopAllEffects(StaminaEffectRoutines);
    }

    public void StopAllStaminaEffectsByType(bool isBuff = true)
    {
        StopAllEffectsByType(StaminaEffectRoutines, isBuff);
    }

    public IEnumerator RegenerateStaminaRoutine()
    {
        // Check if stamina consumption is active, exit coroutine if true
        if (IsConsumingStamina)
        {
            IsRegeneratingStamina = false;
            yield break; // Exit the coroutine
        }

        IsRegeneratingStamina = true;

        // Regenerate stamina while it is below the maximum
        while (Stamina < MaxStamina)
        {
            Stamina += StaminaRegen;

            // Check the flag and exit the coroutine if needed
            if (IsConsumingStamina)
            {
                IsConsumingStamina = false;
                IsRegeneratingStamina = false;
                yield break;
            }

            yield return new WaitForSeconds(StaminaTickRegen); // Delay between regen updates
        }

        IsRegeneratingStamina = false;
    }

    // Method to signal the regeneration coroutine to stop
    public void StopStaminaRegeneration()
    {
        IsConsumingStamina = true;
    }

    // Coroutine to consume stamina over time
    public IEnumerator ConsumeStaminaRoutine(float amount = 0, float staminaTickConsuption = 0.5f)
    {
        // Signal the regeneration coroutine to stop
        StopStaminaRegeneration();

        // Consume stamina while there is enough stamina available
        //while (Stamina >= 0 && Stamina - amount >= 1)
        //{
        //    Stamina -= amount;
        //    yield return new WaitForSeconds(staminaTickConsuption); // Delay between Consume Stamina
        //}
        while (true) {
            Stamina -= amount;
            if (Stamina - amount < 1)
            {
                IsConsumingStamina = false;
                yield break;
            }
            yield return new WaitForSeconds(staminaTickConsuption);
        }

       // IsConsumingStamina = false;
    }
    public IEnumerator ApplyStaminaEffectRoutine(string effectName, float staminaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = staminaAmount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            float newAmount = amount;
            if (newAmount > 0) newAmount = StaminaAddFactor(newAmount);
            else newAmount = StaminaRemoveFactor(newAmount);
            Stamina += newAmount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        Stamina = Mathf.Min(Stamina, MaxStamina);
    }

    public IEnumerator ApplyStaminaHealFactorEffectRoutine(string effectName, float staminaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = staminaHealFactorAmount;
        float staminaHealFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                StaminaHealFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                StaminaHealFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        StaminaHealFactor -= staminaHealFactorOriginalAmount;
    }
    public IEnumerator ApplyStaminaDamageFactorEffectRoutine(string effectName, float staminaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = staminaDamageFactorAmount;
        float staminaDamageFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                StaminaHealFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                StaminaDamageFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        StaminaDamageFactor -= staminaDamageFactorOriginalAmount;
    }
    public IEnumerator ApplyStaminaRegenEffectRoutine(string effectName, float staminaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = staminaRegenAmount;
        float staminaRegenOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                StaminaRegen += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                StaminaRegen = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        StaminaRegen -= staminaRegenOriginalAmount;
    }
}