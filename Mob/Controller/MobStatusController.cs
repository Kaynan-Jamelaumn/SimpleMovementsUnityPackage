using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A mob's health and speed, the status effects it accepts (health, heal/damage factors, regeneration, speed) and
/// its death: death animation, item drops, ability absorption (automatic through <see cref="CombatEvents"/>) and
/// removal after a delay.
/// </summary>
public class MobStatusController : BaseStatusController
{
    [SerializeField, Tooltip("The mob's health (HP, max HP, regeneration, damage/heal factors). Required: without it the mob cannot be hurt or die. Empty = found on this object or its children.")]
    private HealthManager healthManager;

    [SerializeField, Tooltip("The mob's base movement speed and speed effects (slows, hastes). The AI multiplies it by the profile's walk/run multipliers. Empty = found on this object or its children.")]
    private SpeedManager speedManager;

    [Header("Death")]
    [SerializeField, Tooltip("Seconds before the body is removed. Negative = use the Mob Profile's Destroy Delay.")]
    private float destroyDelayOverride = -1f;

    private ItemSpawner itemSpawner;
    private Mob mob;
    private CombatEntity entity;
    private bool dead;
    private HashSet<AttackEffectType> warnedTypes;

    public HealthManager HealthManager => healthManager;

    /// <summary>Fills empty Health/Speed Manager references from this object or its children (inspector button).</summary>
    public void AutoAssignReferences()
    {
        if (healthManager == null) healthManager = GetComponent<HealthManager>();
        if (healthManager == null) healthManager = GetComponentInChildren<HealthManager>();
        if (speedManager == null) speedManager = GetComponent<SpeedManager>();
        if (speedManager == null) speedManager = GetComponentInChildren<SpeedManager>();
    }

    /// <summary>Seconds between death and removal (the override, or the Mob Profile's Destroy Delay).</summary>
    public float DestroyDelayOverride => destroyDelayOverride;
    public SpeedManager SpeedManager => speedManager;
    public bool IsDead => dead;

    /// <summary>Raised once when the mob dies (killer may be null).</summary>
    public event Action<CombatEntity> Died;

    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> effectHandlers;

    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> EffectHandlers
    {
        get
        {
            if (effectHandlers == null)
                InitializeEffectHandlers();
            return effectHandlers;
        }
    }

    private void Awake()
    {
        if (healthManager == null) healthManager = GetComponent<HealthManager>();
        if (healthManager == null) healthManager = GetComponentInChildren<HealthManager>();
        if (speedManager == null) speedManager = GetComponent<SpeedManager>();
        if (speedManager == null) speedManager = GetComponentInChildren<SpeedManager>();
        itemSpawner = GetComponent<ItemSpawner>();
        mob = GetComponent<Mob>();
        entity = CombatEntity.GetOrAdd(gameObject);
    }

    private void Start()
    {
        InitializeEffectHandlers();
        if (healthManager == null)
            Debug.LogWarning($"[{name}] MobStatusController has no HealthManager; the mob cannot take damage or die.", this);
    }

    private void Update()
    {
        if (!dead && healthManager != null && healthManager.CurrentValue <= 0)
            HandleDeath();
    }

    /// <summary>Kills the mob now.</summary>
    public void Kill(CombatEntity killer = null) => Die(killer);

    protected override void HandleDeath() => Die(null);

    private void Die(CombatEntity killer)
    {
        if (dead)
            return;
        dead = true;
        if (killer == null && entity != null)
            killer = entity.RecentAttacker(15f);

        // Combat death first: interrupts casts, raises Killed (ability absorption, quests, UI).
        if (entity != null)
            entity.HandleDeath(killer);

        if (itemSpawner != null)
            itemSpawner.SpawnItem(transform.position);
        if (healthManager != null)
            healthManager.StopAllCoroutines();
        if (speedManager != null)
            speedManager.StopAllCoroutines();

        Died?.Invoke(killer);
        if (mob != null)
            mob.OnDeath(killer);

        float delay = destroyDelayOverride >= 0f ? destroyDelayOverride : (mob != null ? mob.Profile.destroyDelay : 0f);
        Destroy(gameObject, Mathf.Max(0f, delay));
    }

    private void InitializeEffectHandlers()
    {
        effectHandlers = new Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>>();
        if (healthManager != null)
        {
            effectHandlers[AttackEffectType.Hp] = (effect, amount, time, cooldown) => HandleEffect(
                healthManager.AddCurrentValue, healthManager.AddHpEffect, effect, amount, time, cooldown);
            effectHandlers[AttackEffectType.HpHealFactor] = (effect, amount, time, cooldown) => healthManager.AddHpHealFactorEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable);
            effectHandlers[AttackEffectType.HpDamageFactor] = (effect, amount, time, cooldown) => healthManager.AddHpDamageFactorEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable);
            effectHandlers[AttackEffectType.HpRegeneration] = (effect, amount, time, cooldown) => healthManager.AddHpRegenEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable);
        }
        if (speedManager != null)
        {
            effectHandlers[AttackEffectType.Speed] = (effect, amount, time, cooldown) => HandleEffect(
                speedManager.ModifySpeed, speedManager.AddSpeedEffect, effect, amount, time, cooldown);
            effectHandlers[AttackEffectType.SpeedFactor] = (effect, amount, time, cooldown) => speedManager.AddSpeedFactorEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable);
            effectHandlers[AttackEffectType.SpeedMultiplier] = (effect, amount, time, cooldown) => speedManager.AddSpeedMultiplierEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable);
        }
    }

    /// <summary>Applies an attack effect (types a mob does not have, like hunger, are ignored).</summary>
    public override void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown)
    {
        if (effect == null || dead)
            return;
        if (EffectHandlers.TryGetValue(effect.effectType, out Action<AttackEffect, float, float, float> handler))
        {
            handler.Invoke(effect, amount, timeBuffEffect, tickCooldown);
            return;
        }
#if UNITY_EDITOR
        if (warnedTypes == null)
            warnedTypes = new HashSet<AttackEffectType>();
        if (warnedTypes.Add(effect.effectType))
            Debug.Log($"[{name}] mobs ignore '{effect.effectType}' effects.", this);
#endif
    }

    private void HandleEffect(Action<float> directAction, Action<string, float, float, float, bool, bool> effectAction,
        AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown)
    {
        if (timeBuffEffect == 0)
            directAction(amount);
        else
            effectAction(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
    }
}
