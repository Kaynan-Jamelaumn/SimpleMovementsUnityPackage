
using System;
using UnityEngine;
// Unified status effect type enum
public enum UnifiedStatusType
{
    // Core Stats
    Hp,
    Stamina,
    Mana,
    Speed,
    Weight,

    // Survival Stats
    Food,
    Drink,
    Sleep,
    Sanity,
    BodyHeat,
    Oxygen,

    // Max Values
    MaxHp,
    MaxStamina,
    MaxMana,
    MaxHunger,
    MaxThirst,
    MaxWeight,
    MaxSleep,
    MaxSanity,
    MaxBodyHeat,
    MaxOxygen,

    // Regeneration Effects
    HpRegeneration,
    StaminaRegeneration,
    ManaRegeneration,
    HungerRegeneration,
    ThirstRegeneration,
    SleepRegeneration,
    SanityRegeneration,
    BodyHeatRegeneration,
    OxygenRegeneration,

    // Heal Factor Effects
    HpHealFactor,
    StaminaHealFactor,
    ManaHealFactor,
    HungerHealFactor,
    ThirstHealFactor,
    SleepHealFactor,
    SanityHealFactor,
    BodyHeatHealFactor,
    OxygenHealFactor,

    // Damage Factor Effects
    HpDamageFactor,
    StaminaDamageFactor,
    ManaDamageFactor,
    HungerDamageFactor,
    ThirstDamageFactor,
    SleepDamageFactor,
    SanityDamageFactor,
    BodyHeatDamageFactor,
    OxygenDamageFactor,

    // Speed Modifiers
    SpeedFactor,
    SpeedMultiplier,

    // Combat Stats
    Strength,
    Agility,
    Intelligence,
    Endurance,
    Defense,
    MagicResistance,
    CriticalChance,
    CriticalDamage,
    AttackSpeed,
    CastingSpeed
}

// Unified status effect class
[System.Serializable]
public class UnifiedStatusEffect : IStatusEffect
{
    [Header("Effect Configuration")]
    public UnifiedStatusType statusType;
    public string effectName = "";
    public float amount = 0f;

    [Header("Duration Settings")]
    public float duration = 0f;
    public float tickCooldown = 0.5f;
    public bool isProcedural = false;
    public bool isStackable = false;

    [Header("Application Conditions")]
    [Range(0f, 1f)] public float applicationChance = 1f;
    public int minimumLevel = 1;

    [Header("Random Values")]
    public bool randomAmount = false;
    public bool randomDuration = false;
    public bool randomTickCooldown = false;
    public float minAmount = 0f;
    public float maxAmount = 0f;
    public float minDuration = 0f;
    public float maxDuration = 0f;
    public float minTickCooldown = 0f;
    public float maxTickCooldown = 0f;

    [Header("Visual/Audio")]
    public GameObject effectPrefab;
    public AudioClip effectSound;

    [Header("Description")]
    [TextArea(2, 3)] public string description = "";

    public string GetEffectName() => string.IsNullOrEmpty(effectName) ? statusType.ToString() : effectName;
    public float GetAmount() => randomAmount ? UnityEngine.Random.Range(minAmount, maxAmount) : amount;
    public float GetDuration() => randomDuration ? UnityEngine.Random.Range(minDuration, maxDuration) : duration;
    public float GetTickCooldown() => randomTickCooldown ? UnityEngine.Random.Range(minTickCooldown, maxTickCooldown) : tickCooldown;
    public bool IsProcedural() => isProcedural;
    public bool IsStackable() => isStackable;

    public bool ShouldApply(int playerLevel = 1)
    {
        return playerLevel >= minimumLevel && UnityEngine.Random.value <= applicationChance;
    }

    public string GetEffectDescription()
    {
        if (!string.IsNullOrEmpty(description)) return description;

        string sign = amount >= 0 ? "+" : "";
        return $"{sign}{amount} {statusType}";
    }
}
