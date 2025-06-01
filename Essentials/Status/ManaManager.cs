using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ManaManager : StatusManager
{
    [Header("Mana-related fields")]
    [SerializeField] private float manaRegenerationCooldownDuration = 5f;
    private float manaRegenerationCooldownTimer = -5f;

    private Coroutine manaRegenerationRoutine;
    private List<CoroutineInfo> manaEffectRoutines = new List<CoroutineInfo>();

    public List<CoroutineInfo> ManaEffectRoutines { get => manaEffectRoutines; set => manaEffectRoutines = value; }

    private void Start()
    {
        currentValue = maxValue;
    }

    private void Update()
    {
        // Start mana regeneration if not already regenerating and mana is below max
        if (!isRegenerating && currentValue < maxValue && !isConsuming &&
            Time.time - manaRegenerationCooldownTimer > manaRegenerationCooldownDuration)
        {
            manaRegenerationCooldownTimer = Time.time;
            isRegenerating = true;
            manaRegenerationRoutine = StartCoroutine(RegenerateManaRoutine());
        }
    }

    public void UpdateManaBar()
    {
        UpdateBar(currentValue, maxValue, uiImage);
        currentValue = Mathf.Clamp(currentValue, 0, maxValue);
    }

    public void ConsumeMana(float amount)
    {
        if (currentValue <= 0) return;

        manaRegenerationCooldownTimer = Time.time;
        currentValue -= RemoveFactor(amount);
        currentValue = Mathf.Max(0, currentValue);
        isConsuming = true;
    }

    public void RestoreMana(float amount)
    {
        currentValue += AddFactor(amount);
        currentValue = Mathf.Min(maxValue, currentValue);
    }

    public override bool HasEnougCurrentValue(float amount) => currentValue - amount >= 0;

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    // Effect methods
    public void AddManaEffect(string effectName, float manaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(ManaEffectRoutines, effectName, manaAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyManaEffectRoutine);

    public void AddManaHealFactorEffect(string effectName, float manaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(ManaEffectRoutines, effectName, manaHealFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyManaHealFactorEffectRoutine);

    public void AddManaDamageFactorEffect(string effectName, float manaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(ManaEffectRoutines, effectName, manaDamageFactorAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyManaDamageFactorEffectRoutine);

    public void AddManaRegenEffect(string effectName, float manaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
        => AddEffect(ManaEffectRoutines, effectName, manaRegenAmount, timeBuffEffect, tickCooldown, isProcedural, isStackable, ApplyManaRegenEffectRoutine);

    public void RemoveManaEffect(string effectName = null, Coroutine effectRoutine = null)
        => RemoveEffect(ManaEffectRoutines, effectName, effectRoutine);

    public void StopAllManaEffects()
        => StopAllEffects(ManaEffectRoutines);

    public void StopAllManaEffectsByType(bool isBuff = true)
        => StopAllEffectsByType(ManaEffectRoutines, isBuff);

    public void StopManaRegeneration()
        => isConsuming = true;

    // Coroutines
    public IEnumerator RegenerateManaRoutine()
    {
        if (isConsuming)
        {
            isRegenerating = false;
            yield break;
        }

        isRegenerating = true;

        while (currentValue < maxValue)
        {
            currentValue += incrementValue;
            UpdateManaBar();

            if (isConsuming)
            {
                isConsuming = false;
                isRegenerating = false;
                yield break;
            }

            yield return new WaitForSeconds(tickRate);
        }

        isRegenerating = false;
    }

    public IEnumerator ApplyManaEffectRoutine(string effectName, float manaAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            manaAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick =>
            {
                float newAmount = perTick > 0 ? AddFactor(perTick) : RemoveFactor(perTick);
                currentValue += newAmount;
            },
            total =>
            {
                float newAmount = total > 0 ? AddFactor(total) : RemoveFactor(total);
                currentValue += newAmount;
            },
            _ => currentValue = Mathf.Min(currentValue, maxValue)
        );
    }

    public IEnumerator ApplyManaHealFactorEffectRoutine(string effectName, float manaHealFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            manaHealFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementFactor += perTick,
            total => incrementFactor += total,
            original => incrementFactor -= original
        );
    }

    public IEnumerator ApplyManaDamageFactorEffectRoutine(string effectName, float manaDamageFactorAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            manaDamageFactorAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => decrementFactor += perTick,
            total => decrementFactor += total,
            original => decrementFactor -= original
        );
    }

    public IEnumerator ApplyManaRegenEffectRoutine(string effectName, float manaRegenAmount, float timeBuffEffect, float tickCooldown, bool isProcedural = false, bool isStackable = false)
    {
        return ApplyEffectRoutine(
            effectName,
            manaRegenAmount,
            timeBuffEffect,
            tickCooldown,
            isProcedural,
            isStackable,
            perTick => incrementValue += perTick,
            total => incrementValue += total,
            original => incrementValue -= original
        );
    }
}