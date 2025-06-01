using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the types of effects an attack can have.
/// </summary>
public enum AttackEffectType
{
    Hp,
    Stamina,
    Food,
    Drink,
    Weight,
    Speed,
    HpRegeneration,
    StaminaRegeneration,
    HpHealFactor,
    StaminaHealFactor,
    HpDamageFactor,
    StaminaDamageFactor,
    SpeedFactor,        // Modifies the increment/decrement factor for speed effects
    SpeedMultiplier     // Modifies the running/crouching multipliers
}

/// <summary>
/// Represents the effect of an attack in terms of type, amount, duration, and other attributes.
/// </summary>
[System.Serializable]
public class AttackEffect
{
    /// <summary>
    /// The type of effect.
    /// </summary>
    public AttackEffectType effectType;

    /// <summary>
    /// The name of the effect.
    /// </summary>
    public string effectName;

    /// <summary>
    /// The amount of the effect.
    /// </summary>
    public float amount;

    /// <summary>
    /// The duration of the effect.
    /// </summary>
    [Tooltip("The duration of the effect")] public float timeBuffEffect;

    /// <summary>
    /// The cooldown time for the effect to tick again.
    /// </summary>
    [Tooltip("How much time for the effect to tick again")] public float tickCooldown;

    /// <summary>
    /// Indicates if the effect is procedural (the amount is divided by the time).
    /// </summary>
    [Tooltip("Will the amount be divided by the time")] public bool isProcedural;

    /// <summary>
    /// Indicates if the effect can be applied again or only reset the time.
    /// </summary>
    [Tooltip("If the same effect is being applied, can it be applied again or only reset the time")]
    public bool isStackable;

    /// <summary>
    /// Indicates if the effect is applied to the enemy.
    /// </summary>
    [Tooltip("Enemy effect = true, a player effect= false")] public bool enemyEffect = true;

    [Header("Random effectPower")]

    /// <summary>
    /// Indicates if the amount is random.
    /// </summary>
    public bool randomAmount;

    /// <summary>
    /// Indicates if the time buff effect duration is random.
    /// </summary>
    public bool randomTimeBuffEffect;

    /// <summary>
    /// Indicates if the tick cooldown is random.
    /// </summary>
    public bool randomTickCooldown;

    /// <summary>
    /// The minimum amount of the effect.
    /// </summary>
    public float minAmount;

    /// <summary>
    /// The maximum amount of the effect.
    /// </summary>
    public float maxAmount;

    /// <summary>
    /// The minimum time buff effect duration.
    /// </summary>
    public float minTimeBuffEffect;

    /// <summary>
    /// The maximum time buff effect duration.
    /// </summary>
    public float maxTimeBuffEffect;

    /// <summary>
    /// The minimum tick cooldown time.
    /// </summary>
    public float minTickCooldown;

    /// <summary>
    /// The maximum tick cooldown time.
    /// </summary>
    public float maxTickCooldown;

    /// <summary>
    /// The multiplier for critical damage.
    /// </summary>
    public float criticalDamageMultiplier = 1.0f;

    /// <summary>
    /// The probability of applying the effect.
    /// </summary>
    [Tooltip("Probability of applying the effect")][Range(0f, 1f)] public float probabilityToApply = 1;

    /// <summary>
    /// The critical chance of the effect.
    /// </summary>
    [Range(0f, 1f)] public float criticalChance;

    /// <summary>
    /// The list of attack casts associated with this effect.
    /// </summary>
    public List<AttackCast> attackCast;

    /// <summary>
    /// The maximum number of targets this ability can be applied to.
    /// </summary>
    [Tooltip("Max number of targets this ability can be applied to")] public int maxHitTimes;
}
