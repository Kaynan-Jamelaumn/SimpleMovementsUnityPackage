using UnityEngine;

/// <summary>
/// The classic equipment stats. Serialized as numbers in assets: only add new values at the END.
/// </summary>
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

/// <summary>
/// One classic equipment stat: an effect type and an amount (+20 Max Hp, +0.5 Hp Regeneration...). Used by the items'
/// "Effects" list and the armor set bonuses' "Stat Bonuses". Applied and removed exactly by the equipment system
/// (see <see cref="LegacyStatPool"/>); the combat stats (Strength ... Casting Speed) go to <see cref="CombatStats"/>.
/// </summary>
[System.Serializable]
public class EquippableEffect
{
    [Header("Effect Configuration")]
    [Tooltip("What this changes. Max values and regeneration are flat amounts; the Heal/Damage Factors are percentages (+15 = 15%); Speed Factor 0.2 = +20% speed; Speed Multiplier 1.2 = +20% speed; the combat stats are Combat Stats points/percentages.")]
    public EquippableEffectType effectType;

    [Tooltip("The amount (see the effect type for its units). Negative values are penalties.")]
    public float amount;

    [Tooltip("Optional text for tooltips. Empty = generated from the type and amount.")]
    public string effectDescription;

    [Header("Duration Settings")]
    [Tooltip("Seconds the effect lasts after the item is equipped (0 = the whole time it is equipped).")]
    public float duration = 0f;

    [Tooltip("Marks the effect as temporary. It only expires when Duration is above 0.")]
    public bool isTemporary = false;

    [Header("Stacking")]
    [Tooltip("On: adds up with the same effect type from other items. Off: of all non-stacking effects of this type, only the strongest counts.")]
    public bool canStack = true;

    [Tooltip("Stacking effects: at most this many of this effect type count (strongest first). 0 or 1 = no limit.")]
    public int maxStacks = 1;

    [Header("Conditional Application")]
    [Tooltip("Chance (0-1) that the effect applies, rolled once each time the item is equipped.")]
    [Range(0f, 1f)]
    public float applicationChance = 1f;

    [Tooltip("The effect only applies from this player level on (it turns on by itself when the player levels up).")]
    public int minimumLevel = 1;

    [Header("Visual/Audio")]
    [Tooltip("Spawned on the character while the effect is active.")]
    public GameObject effectPrefab;

    [Tooltip("Played when the effect turns on.")]
    public AudioClip effectSound;

    /// <summary>True when the effect expires after <see cref="duration"/> seconds.</summary>
    public bool IsTimed => duration > 0f;

    /// <summary>Tooltip text: the custom description, or a generated one ("+20 Max Health").</summary>
    public string GetFormattedDescription(float strength = 1f)
    {
        if (!string.IsNullOrEmpty(effectDescription))
            return effectDescription;
        string text = LegacyEquipmentStats.Describe(effectType, amount * strength);
        if (IsTimed)
            text += $" for {duration:0.#}s";
        if (minimumLevel > 1)
            text += $" (level {minimumLevel}+)";
        if (applicationChance < 1f)
            text += $" ({applicationChance * 100f:0}% chance)";
        return text;
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

    /// <summary>Reports configuration problems.</summary>
    public void Validate(string owner, System.Collections.Generic.List<string> errors, System.Collections.Generic.List<string> warnings)
    {
        if (Mathf.Approximately(amount, 0f))
            warnings.Add($"{owner}: {effectType} has an amount of 0 (it does nothing).");
        if (isTemporary && duration <= 0f)
            warnings.Add($"{owner}: {effectType} is marked temporary but its Duration is 0, so it never expires.");
        if (applicationChance <= 0f)
            warnings.Add($"{owner}: {effectType} has a 0% application chance and never applies.");
        if (effectType == EquippableEffectType.SpeedMultiplier && amount > 0f && amount < 0.5f)
            warnings.Add($"{owner}: Speed Multiplier {amount} slows the character down a lot (1 = unchanged, 1.2 = +20%). Did you mean Speed Factor?");
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
