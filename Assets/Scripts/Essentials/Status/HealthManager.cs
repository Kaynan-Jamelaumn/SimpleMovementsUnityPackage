using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthManager : StatusManager
{
    [Header("Health-related fields")]
    [SerializeField] private float hp;
    [SerializeField] private float maxHp = 100;
    [SerializeField] private float incrementFactor = 15;
    [SerializeField] private float hpRegen = 1;
    [SerializeField] private float hpTickRegen = 0.5f;
    [SerializeField] private float hpHealFactor;
    [SerializeField] private float hpDamageFactor;
    [SerializeField] private float healthRegenCooldownDuration = 15;
    [SerializeField] private float healthRegenCooldownTimer = -15;
    [SerializeField] private bool isRegeneratingHp;
    [SerializeField] private bool isConsumingHp;
    [SerializeField] private Image hpImage;
    private Coroutine hpRegenerationRoutine;
    //private Coroutine hpHealFactorRoutine;
    //private Coroutine hpDamageFactorRoutine;
    //private Coroutine hpRegenEffectRoutine;
    private List<CoroutineInfo> hpEffectRoutines = new List<CoroutineInfo>();
    public Coroutine HpRegenerationRoutine { get => hpRegenerationRoutine; set => hpRegenerationRoutine = value; }
    public List<CoroutineInfo> HpEffectRoutines { get => hpEffectRoutines; set => hpEffectRoutines = value; }
    public float Hp { get => hp; set => hp = value; }
    public float MaxHp { get => maxHp; set => maxHp = value; }
    public float IncrementFactor { get => incrementFactor; set => incrementFactor = value; }
    public float HpRegen { get => hpRegen; set => hpRegen = value; }
    public float HpTickRegen { get => hpTickRegen; set => hpTickRegen = value; }
    public float HpHealFactor { get => hpHealFactor; set => hpHealFactor = value; }
    public float HpDamageFactor { get => hpDamageFactor; set => hpDamageFactor = value; }
    public float HealthRegenCooldownDuration { get => healthRegenCooldownDuration; }
    public float HealthRegenCooldownTimer { get => healthRegenCooldownTimer; set => healthRegenCooldownTimer = value; }
    public bool IsRegeneratingHp { get => isRegeneratingHp; set => isRegeneratingHp = value; }
    public bool IsConsumingHp { get => isConsumingHp; set => isConsumingHp = value; }
    public Image HpImage { get => hpImage; set => hpImage = value; }
    private void Start()
    {
        Hp = MaxHp;
    }

    private void Update()
    {
        if (!IsRegeneratingHp && Hp < MaxHp &&  Time.time - HealthRegenCooldownTimer > HealthRegenCooldownDuration)
        {
            HealthRegenCooldownTimer = Time.time;
            IsRegeneratingHp = true;
            HpRegenerationRoutine = StartCoroutine(RegenerateHpRoutine());
        }

    }
    public void UpdateHpBar()
    {
        UpdateBar(Hp, MaxHp, HpImage);
        Hp = Mathf.Clamp(Hp, 0, MaxHp);
    }

    public void AddHp(float amount)
    {
        Hp = Mathf.Min(Hp + HpAddFactor(amount), MaxHp);
    }
    public bool HasEnoughHp(float amount)
    {
        return Hp - amount >= 0;
    }

    public void ConsumeHP(float amount, bool scapeBounds= false)
    {
        if (Hp <= 0) return;
        HealthRegenCooldownTimer = Time.time;
        if (scapeBounds) hp -= amount;
        else Hp -= HpRemoveFactor(amount);
        IsConsumingHp = true;
    }

    public void ModifyMaxHp(float amount)
    {
        MaxHp += amount;
    }

    public void ModifyHpRegeneration(float amount)
    {
        HpRegen += amount;
    }
    public void ModifyHpHealFactor(float amount)
    {
        HpHealFactor += amount;
    }


    public void ModifyHpDamageFactor(float amount)
    {
        HpDamageFactor += amount;
    }


    private float HpAddFactor(float amount)
    {
        return ApplyFactor(amount, HpHealFactor);
    }
    private float HpRemoveFactor(float amount)
    {
        return ApplyFactor(amount, HpDamageFactor);
    }

    public void AddHpEffect(string effectName, float hpAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(HpEffectRoutines, effectName, hpAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpEffectRoutine);
    }

    public void AddHpHealFactorEffect(string effectName, float hpHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(HpEffectRoutines, effectName, hpHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpHealFactorEffectRoutine);
    }
    public void AddHpDamageFactorEffect(string effectName, float hpDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(HpEffectRoutines, effectName, hpDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpDamageFactorEffectRoutine);
    }

    public void AddHpRegenEffect(string effectName, float hpRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        AddEffect(HpEffectRoutines, effectName, hpRegenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyHpRegenEffectRoutine);
    }

    public void RemoveHpEffect(string effectName = null, Coroutine effectRoutine = null)
    {
        RemoveEffect(HpEffectRoutines, effectName, effectRoutine);
    }



    public void StopAllHpEffects()
    {
        StopAllEffects(HpEffectRoutines);
    }

    public void StopAllHpEffectsByType(bool isBuff = true)
    {
        StopAllEffectsByType(HpEffectRoutines, isBuff);
    }

    public void StopHpRegeneration()
    {
        IsConsumingHp = true;
    }
    public IEnumerator RegenerateHpRoutine()
    {
        // Check if stamina consumption is active, exit coroutine if true
        if (IsConsumingHp)
        {
            IsRegeneratingHp = false;
            yield break; // Exit the coroutine
        }

        IsRegeneratingHp = true;

        // Regenerate stamina while it is below the maximum
        while (Hp < MaxHp)
        {
            Hp += HpRegen;

            // Check the flag and exit the coroutine if needed
            if (IsConsumingHp)
            {
                IsConsumingHp = false;
                IsRegeneratingHp = false;
                yield break;
            }

            yield return new WaitForSeconds(HpTickRegen); // Delay between regen updates
        }

        IsRegeneratingHp = false;
    }

    public IEnumerator ApplyHpEffectRoutine(string effectName, float hpAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = hpAmount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;

        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            float newAmount = amount;
            if (newAmount > 0) newAmount = HpAddFactor(newAmount);
            else newAmount = HpRemoveFactor(newAmount);
            Hp += newAmount;

            // Wait for the specified tickCooldown duration
            yield return new WaitForSeconds(tickCooldown);
        }

        // Ensure the final stamina value is within the maximum limit
        Hp = Mathf.Min(Hp, MaxHp);
    }
    public IEnumerator ApplyHpHealFactorEffectRoutine(string effectName, float hpHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = hpHealFactorAmount;
        float hpHealFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                HpHealFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                HpHealFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        HpHealFactor -= hpHealFactorOriginalAmount;
    }
    public IEnumerator ApplyHpDamageFactorEffectRoutine(string effectName, float hpDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = hpDamageFactorAmount;
        float hpDamageFactorOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                HpDamageFactor += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                HpDamageFactor = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
    }

    public IEnumerator ApplyHpRegenEffectRoutine(string effectName, float hpRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        float amount = hpRegenAmount;
        float hpRegenOriginalAmount = amount;
        amount = isProcedural ? amount / (timeBuffEffect / tickCooldown) : amount;


        float startTime = Time.time;
        while (Time.time < startTime + timeBuffEffect)
        {
            if (isProcedural)
            {
                HpRegen += amount;
                yield return new WaitForSeconds(tickCooldown);
                // Wait for the specified tickCooldown duration
            }
            else
            {
                HpRegen = amount;
            }

        }

        // Ensure the final stamina value is within the maximum limit
        HpRegen -= hpRegenOriginalAmount;
    }


}
