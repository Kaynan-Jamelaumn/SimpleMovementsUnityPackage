using UnityEngine;

// Enum covering all status effects available in PlayerStatusController
public enum EquippableEffectType
{
    // Core Stats
    MaxHp,
    MaxStamina,
    MaxMana,
    Speed,

    // Regeneration Effects
    HpRegeneration,
    StaminaRegeneration,
    ManaRegeneration,

    // Heal Factor Effects (affect healing received)
    HpHealFactor,
    StaminaHealFactor,
    ManaHealFactor,

    // Damage Factor Effects (affect damage taken)
    HpDamageFactor,
    StaminaDamageFactor,
    ManaDamageFactor,

    // Survival Stats
    MaxHunger,
    MaxThirst,
    MaxWeight,
    MaxSleep,
    MaxSanity,
    MaxBodyHeat,
    MaxOxygen,

    // Survival Regeneration
    HungerRegeneration,
    ThirstRegeneration,
    SleepRegeneration,
    SanityRegeneration,
    BodyHeatRegeneration,
    OxygenRegeneration,

    // Survival Factors
    HungerHealFactor,
    ThirstHealFactor,
    SleepHealFactor,
    SanityHealFactor,
    BodyHeatHealFactor,
    OxygenHealFactor,

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

    // REMOVED: Special Mechanics - these should be handled via SpecialMechanic system
    // GravityReduction, DoubleJump, WaterWalking, FireResistance, IceResistance, 
    // PoisonResistance, MovementSilence, NightVision, BetterLoot, ExperienceBonus
}

// Represents a single equippable effect that can be applied by equipment
[System.Serializable]
public class EquippableEffect
{
    [Header("Effect Configuration")]
    public EquippableEffectType effectType;

    [Tooltip("The amount of the effect to apply")]
    public float amount;

    [Tooltip("Human-readable description of this effect")]
    public string effectDescription;

    [Header("Duration Settings")]
    [Tooltip("Duration of the effect (0 = permanent while equipped)")]
    public float duration = 0f;

    [Tooltip("Whether this is a temporary effect")]
    public bool isTemporary = false;

    [Header("Stacking")]
    [Tooltip("Can this effect stack with similar effects")]
    public bool canStack = true;

    [Tooltip("Maximum number of stacks allowed")]
    public int maxStacks = 1;

    [Header("Conditional Application")]
    [Tooltip("Chance for this effect to apply (0-1)")]
    [Range(0f, 1f)]
    public float applicationChance = 1f;

    [Tooltip("Minimum player level required for this effect")]
    public int minimumLevel = 1;

    [Header("Visual/Audio")]
    [Tooltip("Particle effect to play when this effect is applied")]
    public GameObject effectPrefab;

    [Tooltip("Sound to play when this effect is applied")]
    public AudioClip effectSound;

    // Get a formatted description of this effect
    public string GetFormattedDescription()
    {
        if (!string.IsNullOrEmpty(effectDescription))
            return effectDescription;

        // Generate description based on effect type and amount
        string sign = amount >= 0 ? "+" : "";
        return effectType switch
        {
            EquippableEffectType.MaxHp => $"{sign}{amount} Health",
            EquippableEffectType.MaxStamina => $"{sign}{amount} Stamina",
            EquippableEffectType.MaxMana => $"{sign}{amount} Mana",
            EquippableEffectType.Speed => $"{sign}{amount} Speed",
            EquippableEffectType.HpRegeneration => $"{sign}{amount} HP/sec",
            EquippableEffectType.StaminaRegeneration => $"{sign}{amount} Stamina/sec",
            EquippableEffectType.ManaRegeneration => $"{sign}{amount} Mana/sec",
            EquippableEffectType.HpHealFactor => $"{sign}{(amount * 100f):F0}% Healing Received",
            EquippableEffectType.HpDamageFactor => $"{sign}{(amount * 100f):F0}% Damage Taken",
            EquippableEffectType.MaxWeight => $"{sign}{amount} Carry Weight",
            EquippableEffectType.Strength => $"{sign}{amount} Strength",
            EquippableEffectType.Agility => $"{sign}{amount} Agility",
            EquippableEffectType.Intelligence => $"{sign}{amount} Intelligence",
            EquippableEffectType.Endurance => $"{sign}{amount} Endurance",
            EquippableEffectType.Defense => $"{sign}{amount} Defense",
            EquippableEffectType.MagicResistance => $"{sign}{amount} Magic Resistance",
            EquippableEffectType.CriticalChance => $"{sign}{amount:F1}% Critical Chance",
            EquippableEffectType.CriticalDamage => $"{sign}{amount:F0}% Critical Damage",
            _ => $"{effectType}: {sign}{amount}"
        };
    }

    // Check if this effect should be applied based on conditions
    public bool ShouldApply(int playerLevel, float randomValue = -1f)
    {
        if (playerLevel < minimumLevel)
            return false;

        if (randomValue < 0f)
            randomValue = Random.Range(0f, 1f);

        return randomValue <= applicationChance;
    }


    // Check if this effect modifies a core stat
    public bool IsCoreStat()
    {
        return effectType switch
        {
            EquippableEffectType.MaxHp or
            EquippableEffectType.MaxStamina or
            EquippableEffectType.MaxMana or
            EquippableEffectType.Speed => true,
            _ => false
        };
    }

    // Check if this effect is a resistance (basic resistances only - special ones via SpecialMechanic)
    public bool IsResistance()
    {
        return effectType switch
        {
            EquippableEffectType.MagicResistance => true,
            _ => false
        };
    }
}