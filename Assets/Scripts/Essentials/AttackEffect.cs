using System;
using System.Collections.Generic;
using UnityEngine;

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

}
[System.Serializable]
public class AttackEffect
{
    public AttackEffectType effectType;
    public string effectName;
    public float amount;
    [Tooltip("the duration of the effect")] public float timeBuffEffect;
    [Tooltip("how much time for the effect to tick again")] public float tickCooldown;
    [Tooltip("the amount will be divided by the time")] public bool isProcedural;
    [Tooltip("if the same affect is being applied can it be applied again or it wont and only reset the time")] public bool isStackable;
    [Tooltip("if it is applied to the caster or the enemy")] public bool enemyEffect = true;
    [Header("Random effectPower")]
    public bool randomAmount;
    public bool randomTimeBuffEffect;
    public bool randomTickCooldown;
    public float minAmount;
    public float maxAmount;
    public float minTimeBuffEffect;
    public float maxTimeBuffEffect;
    public float minTickCooldown;
    public float maxTickCooldown;
    public float criticalDamageMultiplier = 1.0f;
    [Tooltip("probability of applying the effect")][Range(0f, 1f)] public float probabilityToApply =1;
    [Range(0f, 1f)] public float criticalChance;
    public List<AttackCast> attackCast;
    [Tooltip("max number of targets this ability can be applied")] public int maxHitTimes;
}