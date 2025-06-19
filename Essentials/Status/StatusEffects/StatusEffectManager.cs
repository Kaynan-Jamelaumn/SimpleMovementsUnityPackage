// Status effect manager - central hub for all status modifications
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


// Active effect tracking
[System.Serializable]
public class ActiveStatusEffect
{
    public UnifiedStatusEffect effect;
    public string sourceId;
    public string effectName;
    public float startTime;
    public float duration;

    public bool IsExpired => duration > 0 && Time.time - startTime >= duration;
    public float RemainingTime => Mathf.Max(0, duration - (Time.time - startTime));
}

public class StatusEffectManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private PlayerStatusController playerController;

    [Header("Effect Tracking")]
    [SerializeField] private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogging = false;

    private Dictionary<UnifiedStatusType, System.Action<float, bool>> statusActions;
    private Dictionary<UnifiedStatusType, System.Action<string, float, float, float, bool, bool>> timedStatusActions;

    public static StatusEffectManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (playerController == null)
            playerController = GetComponent<PlayerStatusController>();

        InitializeStatusActions();
    }

    private void InitializeStatusActions()
    {
        statusActions = new Dictionary<UnifiedStatusType, System.Action<float, bool>>
        {
            // Core Stats - Immediate Effects
            { UnifiedStatusType.Hp, (amount, apply) => ApplyImmediateEffect(() => playerController.HpManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Stamina, (amount, apply) => ApplyImmediateEffect(() => playerController.StaminaManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Mana, (amount, apply) => ApplyImmediateEffect(() => playerController.ManaManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Food, (amount, apply) => ApplyImmediateEffect(() => playerController.HungerManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Drink, (amount, apply) => ApplyImmediateEffect(() => playerController.ThirstManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Sleep, (amount, apply) => ApplyImmediateEffect(() => playerController.SleepManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Sanity, (amount, apply) => ApplyImmediateEffect(() => playerController.SanityManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.BodyHeat, (amount, apply) => ApplyImmediateEffect(() => playerController.BodyHeatManager.ModifyBodyHeat(apply ? amount : -amount)) },
            { UnifiedStatusType.Oxygen, (amount, apply) => ApplyImmediateEffect(() => playerController.OxygenManager.AddCurrentValue(apply ? amount : -amount)) },
            { UnifiedStatusType.Speed, (amount, apply) => ApplyImmediateEffect(() => playerController.SpeedManager.ModifySpeed(apply ? amount : -amount)) },
            
            // Max Values
            { UnifiedStatusType.MaxHp, (amount, apply) => ApplyImmediateEffect(() => playerController.HpManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxStamina, (amount, apply) => ApplyImmediateEffect(() => playerController.StaminaManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxMana, (amount, apply) => ApplyImmediateEffect(() => playerController.ManaManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxHunger, (amount, apply) => ApplyImmediateEffect(() => playerController.HungerManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxThirst, (amount, apply) => ApplyImmediateEffect(() => playerController.ThirstManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxWeight, (amount, apply) => ApplyImmediateEffect(() => playerController.WeightManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxSleep, (amount, apply) => ApplyImmediateEffect(() => playerController.SleepManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxSanity, (amount, apply) => ApplyImmediateEffect(() => playerController.SanityManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxBodyHeat, (amount, apply) => ApplyImmediateEffect(() => playerController.BodyHeatManager.ModifyMaxValue(apply ? amount : -amount)) },
            { UnifiedStatusType.MaxOxygen, (amount, apply) => ApplyImmediateEffect(() => playerController.OxygenManager.ModifyMaxValue(apply ? amount : -amount)) }
        };

        timedStatusActions = new Dictionary<UnifiedStatusType, System.Action<string, float, float, float, bool, bool>>
        {
            // Core Stats - Timed Effects
            { UnifiedStatusType.Hp, (name, amount, duration, cooldown, procedural, stackable) => playerController.HpManager.AddHpEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Stamina, (name, amount, duration, cooldown, procedural, stackable) => playerController.StaminaManager.AddStaminaEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Mana, (name, amount, duration, cooldown, procedural, stackable) => playerController.ManaManager.AddManaEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Food, (name, amount, duration, cooldown, procedural, stackable) => playerController.HungerManager.AddFoodEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Drink, (name, amount, duration, cooldown, procedural, stackable) => playerController.ThirstManager.AddDrinkEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Sleep, (name, amount, duration, cooldown, procedural, stackable) => playerController.SleepManager.AddSleepEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Sanity, (name, amount, duration, cooldown, procedural, stackable) => playerController.SanityManager.AddSanityEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.BodyHeat, (name, amount, duration, cooldown, procedural, stackable) => playerController.BodyHeatManager.AddBodyHeatEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Oxygen, (name, amount, duration, cooldown, procedural, stackable) => playerController.OxygenManager.AddOxygenEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.Speed, (name, amount, duration, cooldown, procedural, stackable) => playerController.SpeedManager.AddSpeedEffect(name, amount, duration, cooldown, procedural, stackable) },
            
            // Regeneration Effects
            { UnifiedStatusType.HpRegeneration, (name, amount, duration, cooldown, procedural, stackable) => playerController.HpManager.AddHpRegenEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.StaminaRegeneration, (name, amount, duration, cooldown, procedural, stackable) => playerController.StaminaManager.AddStaminaRegenEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.ManaRegeneration, (name, amount, duration, cooldown, procedural, stackable) => playerController.ManaManager.AddManaRegenEffect(name, amount, duration, cooldown, procedural, stackable) },
            
            // Heal Factor Effects
            { UnifiedStatusType.HpHealFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.HpManager.AddHpHealFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.StaminaHealFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.StaminaManager.AddStaminaHealFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.ManaHealFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.ManaManager.AddManaHealFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.SanityHealFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.SanityManager.AddSanityHealFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            
            // Damage Factor Effects
            { UnifiedStatusType.HpDamageFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.HpManager.AddHpDamageFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.StaminaDamageFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.StaminaManager.AddStaminaDamageFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.ManaDamageFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.ManaManager.AddManaDamageFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.SanityDamageFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.SanityManager.AddSanityDamageFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            
            // Speed Modifiers
            { UnifiedStatusType.SpeedFactor, (name, amount, duration, cooldown, procedural, stackable) => playerController.SpeedManager.AddSpeedFactorEffect(name, amount, duration, cooldown, procedural, stackable) },
            { UnifiedStatusType.SpeedMultiplier, (name, amount, duration, cooldown, procedural, stackable) => playerController.SpeedManager.AddSpeedMultiplierEffect(name, amount, duration, cooldown, procedural, stackable) }
        };
    }

    // Main method to apply any status effect
    public void ApplyStatusEffect(IStatusEffect effect, int playerLevel = 1, string sourceId = "")
    {
        if (effect == null || !effect.ShouldApply(playerLevel))
        {
            LogDebug($"Effect {effect?.GetEffectName()} not applied - failed conditions");
            return;
        }

        var unifiedEffect = ConvertToUnifiedEffect(effect);
        if (unifiedEffect == null)
        {
            LogDebug($"Failed to convert effect {effect.GetEffectName()} to unified format");
            return;
        }

        ApplyUnifiedEffect(unifiedEffect, sourceId);
    }

    // Apply unified status effect
    public void ApplyUnifiedEffect(UnifiedStatusEffect effect, string sourceId = "")
    {
        if (effect == null) return;

        float amount = effect.GetAmount();
        float duration = effect.GetDuration();
        float cooldown = effect.GetTickCooldown();
        string effectName = string.IsNullOrEmpty(sourceId) ? effect.GetEffectName() : $"{sourceId}_{effect.GetEffectName()}";

        LogDebug($"Applying effect: {effectName}, Amount: {amount}, Duration: {duration}");

        // Play effects
        PlayEffectAudio(effect);
        ShowEffectVisual(effect);

        // Apply the effect
        if (duration <= 0)
        {
            // Immediate effect
            ApplyImmediateStatusEffect(effect.statusType, amount);
        }
        else
        {
            // Timed effect
            ApplyTimedStatusEffect(effect.statusType, effectName, amount, duration, cooldown, effect.IsProcedural(), effect.IsStackable());

            // Track active effect
            TrackActiveEffect(effect, sourceId, effectName);
        }
    }

    // Remove status effect by name
    public void RemoveStatusEffect(string effectName, UnifiedStatusType? statusType = null)
    {
        LogDebug($"Removing effect: {effectName}");

        // Remove from appropriate manager
        if (statusType.HasValue)
        {
            RemoveFromSpecificManager(statusType.Value, effectName);
        }
        else
        {
            // Try to remove from all managers
            RemoveFromAllManagers(effectName);
        }

        // Remove from tracking
        activeEffects.RemoveAll(e => e.effectName == effectName);
    }

    private void ApplyImmediateStatusEffect(UnifiedStatusType statusType, float amount)
    {
        if (statusActions.TryGetValue(statusType, out var action))
        {
            action.Invoke(amount, true);
        }
        else
        {
            LogDebug($"No immediate action defined for status type: {statusType}");
        }
    }

    private void ApplyTimedStatusEffect(UnifiedStatusType statusType, string effectName, float amount, float duration, float cooldown, bool isProcedural, bool isStackable)
    {
        if (timedStatusActions.TryGetValue(statusType, out var action))
        {
            action.Invoke(effectName, amount, duration, cooldown, isProcedural, isStackable);
        }
        else
        {
            LogDebug($"No timed action defined for status type: {statusType}");
        }
    }

    private void RemoveFromSpecificManager(UnifiedStatusType statusType, string effectName)
    {
        switch (statusType)
        {
            case UnifiedStatusType.Hp:
                playerController.HpManager.RemoveHpEffect(effectName);
                break;
            case UnifiedStatusType.Stamina:
                playerController.StaminaManager.RemoveStaminaEffect(effectName);
                break;
            case UnifiedStatusType.Mana:
                playerController.ManaManager.RemoveManaEffect(effectName);
                break;
            case UnifiedStatusType.Food:
                playerController.HungerManager.RemoveFoodEffect(effectName);
                break;
            case UnifiedStatusType.Drink:
                playerController.ThirstManager.RemoveDrinkEffect(effectName);
                break;
            case UnifiedStatusType.Sleep:
                playerController.SleepManager.RemoveSleepEffect(effectName);
                break;
            case UnifiedStatusType.Sanity:
                playerController.SanityManager.RemoveSanityEffect(effectName);
                break;
            case UnifiedStatusType.BodyHeat:
                playerController.BodyHeatManager.RemoveBodyHeatEffect(effectName);
                break;
            case UnifiedStatusType.Oxygen:
                playerController.OxygenManager.RemoveOxygenEffect(effectName);
                break;
            case UnifiedStatusType.Speed:
                playerController.SpeedManager.RemoveSpeedEffect(effectName);
                break;
        }
    }

    private void RemoveFromAllManagers(string effectName)
    {
        playerController.HpManager.RemoveHpEffect(effectName);
        playerController.StaminaManager.RemoveStaminaEffect(effectName);
        playerController.ManaManager.RemoveManaEffect(effectName);
        playerController.HungerManager.RemoveFoodEffect(effectName);
        playerController.ThirstManager.RemoveDrinkEffect(effectName);
        playerController.SleepManager.RemoveSleepEffect(effectName);
        playerController.SanityManager.RemoveSanityEffect(effectName);
        playerController.BodyHeatManager.RemoveBodyHeatEffect(effectName);
        playerController.OxygenManager.RemoveOxygenEffect(effectName);
        playerController.SpeedManager.RemoveSpeedEffect(effectName);
    }

    private UnifiedStatusEffect ConvertToUnifiedEffect(IStatusEffect effect)
    {
        // If it's already a unified effect, return it
        if (effect is UnifiedStatusEffect unifiedEffect)
            return unifiedEffect;

        // Try to convert from legacy effect types
        return ConvertLegacyEffect(effect);
    }

    private UnifiedStatusEffect ConvertLegacyEffect(IStatusEffect effect)
    {
        // Convert from ConsumableEffect
        if (effect is ConsumableEffectWrapper consumableWrapper)
        {
            return ConvertConsumableEffect(consumableWrapper.ConsumableEffect);
        }

        // Convert from EquippableEffect
        if (effect is EquippableEffectWrapper equippableWrapper)
        {
            return ConvertEquippableEffect(equippableWrapper.EquippableEffect);
        }

        // Convert from AttackEffect
        if (effect is AttackEffectWrapper attackWrapper)
        {
            return ConvertAttackEffect(attackWrapper.AttackEffect);
        }

        LogDebug($"No conversion available for effect type: {effect.GetType().Name}");
        return null;
    }

    private UnifiedStatusEffect ConvertConsumableEffect(ConsumableEffect effect)
    {
        var unified = new UnifiedStatusEffect
        {
            statusType = ConvertConsumableEffectType(effect.effectType),
            effectName = effect.effectName,
            amount = effect.GetRandomizedAmount(),
            duration = effect.GetRandomizedDuration(),
            tickCooldown = effect.GetRandomizedTickCooldown(),
            isProcedural = effect.isProcedural,
            isStackable = effect.isStackable,
            applicationChance = effect.applicationChance,
            minimumLevel = effect.minimumLevel
        };

        return unified;
    }

    private UnifiedStatusEffect ConvertEquippableEffect(EquippableEffect effect)
    {
        var unified = new UnifiedStatusEffect
        {
            statusType = ConvertEquippableEffectType(effect.effectType),
            effectName = string.IsNullOrEmpty(effect.effectDescription) ? effect.effectType.ToString() : effect.effectDescription,
            amount = effect.amount,
            duration = effect.duration,
            tickCooldown = 0.5f,
            isProcedural = false,
            isStackable = effect.canStack,
            applicationChance = effect.applicationChance,
            minimumLevel = effect.minimumLevel,
            effectPrefab = effect.effectPrefab,
            effectSound = effect.effectSound
        };

        return unified;
    }

    private UnifiedStatusEffect ConvertAttackEffect(AttackEffect effect)
    {
        var unified = new UnifiedStatusEffect
        {
            statusType = ConvertAttackEffectType(effect.effectType),
            effectName = effect.effectName,
            amount = effect.amount,
            duration = effect.timeBuffEffect,
            tickCooldown = effect.tickCooldown,
            isProcedural = effect.isProcedural,
            isStackable = effect.isStackable,
            applicationChance = effect.probabilityToApply,
            minimumLevel = 1,
            randomAmount = effect.randomAmount,
            minAmount = effect.minAmount,
            maxAmount = effect.maxAmount,
            randomDuration = effect.randomTimeBuffEffect,
            minDuration = effect.minTimeBuffEffect,
            maxDuration = effect.maxTimeBuffEffect,
            randomTickCooldown = effect.randomTickCooldown,
            minTickCooldown = effect.minTickCooldown,
            maxTickCooldown = effect.maxTickCooldown
        };

        return unified;
    }

    private UnifiedStatusType ConvertConsumableEffectType(ConsumableEffectType effectType)
    {
        return effectType switch
        {
            ConsumableEffectType.Hp => UnifiedStatusType.Hp,
            ConsumableEffectType.Stamina => UnifiedStatusType.Stamina,
            ConsumableEffectType.Food => UnifiedStatusType.Food,
            ConsumableEffectType.Drink => UnifiedStatusType.Drink,
            ConsumableEffectType.Weight => UnifiedStatusType.Weight,
            ConsumableEffectType.Speed => UnifiedStatusType.Speed,
            ConsumableEffectType.HpRegeneration => UnifiedStatusType.HpRegeneration,
            ConsumableEffectType.StaminaRegeneration => UnifiedStatusType.StaminaRegeneration,
            ConsumableEffectType.HpHealFactor => UnifiedStatusType.HpHealFactor,
            ConsumableEffectType.StaminaHealFactor => UnifiedStatusType.StaminaHealFactor,
            ConsumableEffectType.HpDamageFactor => UnifiedStatusType.HpDamageFactor,
            ConsumableEffectType.StaminaDamageFactor => UnifiedStatusType.StaminaDamageFactor,
            ConsumableEffectType.SpeedFactor => UnifiedStatusType.SpeedFactor,
            ConsumableEffectType.SpeedMultiplier => UnifiedStatusType.SpeedMultiplier,
            ConsumableEffectType.Sleep => UnifiedStatusType.Sleep,
            ConsumableEffectType.SleepFactor => UnifiedStatusType.SleepHealFactor,
            ConsumableEffectType.Sanity => UnifiedStatusType.Sanity,
            ConsumableEffectType.SanityHealFactor => UnifiedStatusType.SanityHealFactor,
            ConsumableEffectType.SanityDamageFactor => UnifiedStatusType.SanityDamageFactor,
            ConsumableEffectType.Mana => UnifiedStatusType.Mana,
            ConsumableEffectType.ManaRegeneration => UnifiedStatusType.ManaRegeneration,
            ConsumableEffectType.ManaHealFactor => UnifiedStatusType.ManaHealFactor,
            ConsumableEffectType.ManaDamageFactor => UnifiedStatusType.ManaDamageFactor,
            ConsumableEffectType.BodyHeat => UnifiedStatusType.BodyHeat,
            ConsumableEffectType.BodyHeatFactor => UnifiedStatusType.BodyHeatHealFactor,
            ConsumableEffectType.Oxygen => UnifiedStatusType.Oxygen,
            ConsumableEffectType.OxygenFactor => UnifiedStatusType.OxygenHealFactor,
            ConsumableEffectType.MaxHp => UnifiedStatusType.MaxHp,
            ConsumableEffectType.MaxStamina => UnifiedStatusType.MaxStamina,
            ConsumableEffectType.MaxMana => UnifiedStatusType.MaxMana,
            ConsumableEffectType.MaxHunger => UnifiedStatusType.MaxHunger,
            ConsumableEffectType.MaxThirst => UnifiedStatusType.MaxThirst,
            ConsumableEffectType.MaxWeight => UnifiedStatusType.MaxWeight,
            ConsumableEffectType.MaxSleep => UnifiedStatusType.MaxSleep,
            ConsumableEffectType.MaxSanity => UnifiedStatusType.MaxSanity,
            ConsumableEffectType.MaxBodyHeat => UnifiedStatusType.MaxBodyHeat,
            ConsumableEffectType.MaxOxygen => UnifiedStatusType.MaxOxygen,
            _ => UnifiedStatusType.Hp
        };
    }

    private UnifiedStatusType ConvertEquippableEffectType(EquippableEffectType effectType)
    {
        return effectType switch
        {
            EquippableEffectType.MaxHp => UnifiedStatusType.MaxHp,
            EquippableEffectType.MaxStamina => UnifiedStatusType.MaxStamina,
            EquippableEffectType.MaxMana => UnifiedStatusType.MaxMana,
            EquippableEffectType.Speed => UnifiedStatusType.Speed,
            EquippableEffectType.HpRegeneration => UnifiedStatusType.HpRegeneration,
            EquippableEffectType.StaminaRegeneration => UnifiedStatusType.StaminaRegeneration,
            EquippableEffectType.ManaRegeneration => UnifiedStatusType.ManaRegeneration,
            EquippableEffectType.HpHealFactor => UnifiedStatusType.HpHealFactor,
            EquippableEffectType.StaminaHealFactor => UnifiedStatusType.StaminaHealFactor,
            EquippableEffectType.ManaHealFactor => UnifiedStatusType.ManaHealFactor,
            EquippableEffectType.HpDamageFactor => UnifiedStatusType.HpDamageFactor,
            EquippableEffectType.StaminaDamageFactor => UnifiedStatusType.StaminaDamageFactor,
            EquippableEffectType.ManaDamageFactor => UnifiedStatusType.ManaDamageFactor,
            EquippableEffectType.MaxHunger => UnifiedStatusType.MaxHunger,
            EquippableEffectType.MaxThirst => UnifiedStatusType.MaxThirst,
            EquippableEffectType.MaxWeight => UnifiedStatusType.MaxWeight,
            EquippableEffectType.MaxSleep => UnifiedStatusType.MaxSleep,
            EquippableEffectType.MaxSanity => UnifiedStatusType.MaxSanity,
            EquippableEffectType.MaxBodyHeat => UnifiedStatusType.MaxBodyHeat,
            EquippableEffectType.MaxOxygen => UnifiedStatusType.MaxOxygen,
            EquippableEffectType.SpeedFactor => UnifiedStatusType.SpeedFactor,
            EquippableEffectType.SpeedMultiplier => UnifiedStatusType.SpeedMultiplier,
            EquippableEffectType.Strength => UnifiedStatusType.Strength,
            EquippableEffectType.Agility => UnifiedStatusType.Agility,
            EquippableEffectType.Intelligence => UnifiedStatusType.Intelligence,
            EquippableEffectType.Endurance => UnifiedStatusType.Endurance,
            EquippableEffectType.Defense => UnifiedStatusType.Defense,
            EquippableEffectType.MagicResistance => UnifiedStatusType.MagicResistance,
            EquippableEffectType.CriticalChance => UnifiedStatusType.CriticalChance,
            EquippableEffectType.CriticalDamage => UnifiedStatusType.CriticalDamage,
            EquippableEffectType.AttackSpeed => UnifiedStatusType.AttackSpeed,
            EquippableEffectType.CastingSpeed => UnifiedStatusType.CastingSpeed,
            _ => UnifiedStatusType.Hp
        };
    }

    private UnifiedStatusType ConvertAttackEffectType(AttackEffectType effectType)
    {
        return effectType switch
        {
            AttackEffectType.Hp => UnifiedStatusType.Hp,
            AttackEffectType.Stamina => UnifiedStatusType.Stamina,
            AttackEffectType.Food => UnifiedStatusType.Food,
            AttackEffectType.Drink => UnifiedStatusType.Drink,
            AttackEffectType.Weight => UnifiedStatusType.Weight,
            AttackEffectType.Speed => UnifiedStatusType.Speed,
            AttackEffectType.HpRegeneration => UnifiedStatusType.HpRegeneration,
            AttackEffectType.StaminaRegeneration => UnifiedStatusType.StaminaRegeneration,
            AttackEffectType.HpHealFactor => UnifiedStatusType.HpHealFactor,
            AttackEffectType.StaminaHealFactor => UnifiedStatusType.StaminaHealFactor,
            AttackEffectType.HpDamageFactor => UnifiedStatusType.HpDamageFactor,
            AttackEffectType.StaminaDamageFactor => UnifiedStatusType.StaminaDamageFactor,
            AttackEffectType.SpeedFactor => UnifiedStatusType.SpeedFactor,
            AttackEffectType.SpeedMultiplier => UnifiedStatusType.SpeedMultiplier,
            AttackEffectType.Sleep => UnifiedStatusType.Sleep,
            AttackEffectType.SleepFactor => UnifiedStatusType.SleepHealFactor,
            AttackEffectType.Sanity => UnifiedStatusType.Sanity,
            AttackEffectType.SanityHealFactor => UnifiedStatusType.SanityHealFactor,
            AttackEffectType.SanityDamageFactor => UnifiedStatusType.SanityDamageFactor,
            AttackEffectType.Mana => UnifiedStatusType.Mana,
            AttackEffectType.ManaRegeneration => UnifiedStatusType.ManaRegeneration,
            AttackEffectType.ManaHealFactor => UnifiedStatusType.ManaHealFactor,
            AttackEffectType.ManaDamageFactor => UnifiedStatusType.ManaDamageFactor,
            AttackEffectType.BodyHeat => UnifiedStatusType.BodyHeat,
            AttackEffectType.BodyHeatFactor => UnifiedStatusType.BodyHeatHealFactor,
            AttackEffectType.Oxygen => UnifiedStatusType.Oxygen,
            AttackEffectType.OxygenFactor => UnifiedStatusType.OxygenHealFactor,
            _ => UnifiedStatusType.Hp
        };
    }

    private void TrackActiveEffect(UnifiedStatusEffect effect, string sourceId, string effectName)
    {
        var activeEffect = new ActiveStatusEffect
        {
            effect = effect,
            sourceId = sourceId,
            effectName = effectName,
            startTime = Time.time,
            duration = effect.GetDuration()
        };

        activeEffects.Add(activeEffect);
    }

    private void ApplyImmediateEffect(System.Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying immediate effect: {e.Message}");
        }
    }

    private void PlayEffectAudio(UnifiedStatusEffect effect)
    {
        if (effect.effectSound != null)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = playerController.GetComponent<AudioSource>();

            if (audioSource != null)
                audioSource.PlayOneShot(effect.effectSound);
        }
    }

    private void ShowEffectVisual(UnifiedStatusEffect effect)
    {
        if (effect.effectPrefab != null)
        {
            var visual = Instantiate(effect.effectPrefab, playerController.transform.position, Quaternion.identity);
            Destroy(visual, 3f); // Auto-cleanup after 3 seconds
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
            Debug.Log($"[StatusEffectManager] {message}");
    }

    // Public API methods
    public List<ActiveStatusEffect> GetActiveEffects() => new List<ActiveStatusEffect>(activeEffects);

    public void RemoveAllEffectsFromSource(string sourceId)
    {
        var effectsToRemove = activeEffects.Where(e => e.sourceId == sourceId).ToList();
        foreach (var effect in effectsToRemove)
        {
            RemoveStatusEffect(effect.effectName, effect.effect.statusType);
        }
    }

}