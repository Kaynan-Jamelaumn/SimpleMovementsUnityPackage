using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


/// <summary>
/// Controller class for managing the status of a mob entity, including health, speed, abilities, and effects.
/// Inherits from <see cref="BaseStatusController"/>.
/// </summary>
public class MobStatusController : BaseStatusController
{
    [SerializeField, Tooltip("Manages the mob's health, including HP and related effects.")]
    private HealthManager healthManager;

    [SerializeField, Tooltip("Manages the mob's speed and related effects.")]
    private SpeedManager speedManager;

    private AbilitySpawner abilitySpawner;
    private ItemSpawner itemSpawner;
    private MobActionsController mobActionsController;
    private MobAbilityController mobAbilityController;

    /// <summary>
    /// Gets the HealthManager component.
    /// </summary>
    public HealthManager HealthManager => healthManager;

    /// <summary>
    /// Gets the SpeedManager component.
    /// </summary>
    public SpeedManager SpeedManager => speedManager;

    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> effectHandlers;

    /// <summary>
    /// Gets or initializes the dictionary of effect handlers based on attack effect types.
    /// </summary>
    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> EffectHandlers
    {
        get
        {
            if (effectHandlers == null)
            {
                InitializeEffectHandlers();
            }
            return effectHandlers;
        }
    }

    /// <summary>
    /// Caches required components and logs an error if any are missing.
    /// </summary>
    protected override void CacheComponents()
    {
        healthManager = GetComponentOrLogError(ref healthManager, "HealthManager");
        speedManager = GetComponentOrLogError(ref speedManager, "SpeedManager");
        abilitySpawner = GetComponentOrLogError(ref abilitySpawner, "AbilitySpawner");
        itemSpawner = GetComponentOrLogError(ref itemSpawner, "ItemSpawner");
        mobActionsController = GetComponentOrLogError(ref mobActionsController, "MobActionsController");
        mobAbilityController = GetComponent<MobAbilityController>();
    }

    /// <summary>
    /// Validates assignments of critical components.
    /// Logs warnings if components are not assigned.
    /// </summary>
    public override void ValidateAssignments()
    {
        Assert.IsNotNull(healthManager, "HealthManager is not assigned.");
        Assert.IsNotNull(speedManager, "SpeedManager is not assigned.");
    }

    /// <summary>
    /// Updates the mob's status each frame. Handles death if HP is below or equal to zero.
    /// </summary>
    private void Update()
    {
        if (healthManager.Hp <= 0)
        {
            HandleDeath();
        }
    }

    /// <summary>
    /// Invoked when the script starts. Initializes effect handlers.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        InitializeEffectHandlers();
    }

    /// <summary>
    /// Handles mob death by triggering spawners, stopping coroutines, and invoking the base class death logic.
    /// </summary>
    protected override void HandleDeath()
    {
        abilitySpawner?.SpawnAbility();
        itemSpawner?.SpawnItem(transform.position);
        mobActionsController?.StopAllCoroutines();
        mobAbilityController?.StopAllCoroutines();
        healthManager.StopAllCoroutines();
        speedManager.StopAllCoroutines();
        base.HandleDeath();
    }

    /// <summary>
    /// Initializes the dictionary of effect handlers for different attack effect types.
    /// </summary>
    private void InitializeEffectHandlers()
    {
        effectHandlers = new Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>>
        {
            { AttackEffectType.Hp, (effect, amount, time, cooldown) => HandleEffect(
                HealthManager.AddHp, HealthManager.AddHpEffect, effect, amount, time, cooldown) },

            { AttackEffectType.HpHealFactor, (effect, amount, time, cooldown) => HealthManager.AddHpHealFactorEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },

            { AttackEffectType.HpDamageFactor, (effect, amount, time, cooldown) => HealthManager.AddHpDamageFactorEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) },

            { AttackEffectType.HpRegeneration, (effect, amount, time, cooldown) => HealthManager.AddHpRegenEffect(
                effect.effectName, amount, time, cooldown, effect.isProcedural, effect.isStackable) }
        };
    }

    /// <summary>
    /// Applies an attack effect to the mob based on the provided effect type.
    /// </summary>
    /// <param name="effect">The attack effect to apply.</param>
    /// <param name="amount">The amount of the effect.</param>
    /// <param name="timeBuffEffect">The duration of the effect.</param>
    /// <param name="tickCooldown">The cooldown between effect ticks.</param>
    public override void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown)
    {
        if (EffectHandlers.TryGetValue(effect.effectType, out Action<AttackEffect, float, float, float> handler))
        {
            handler.Invoke(effect, amount, timeBuffEffect, tickCooldown);
        }
        else
        {
            Debug.LogWarning($"Unhandled effect type: {effect.effectType}");
        }
    }

    /// <summary>
    /// Handles the application of an effect. Determines whether to apply it directly or over time.
    /// </summary>
    /// <param name="directAction">The action to apply the effect immediately.</param>
    /// <param name="effectAction">The action to apply the effect over time.</param>
    /// <param name="effect">The attack effect being applied.</param>
    /// <param name="amount">The amount of the effect.</param>
    /// <param name="timeBuffEffect">The duration of the effect.</param>
    /// <param name="tickCooldown">The cooldown between effect ticks.</param>
    private void HandleEffect(Action<float> directAction,
        Action<string, float, float, float, bool, bool> effectAction,
        AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown)
    {
        if (timeBuffEffect == 0)
        {
            directAction(amount);
        }
        else
        {
            effectAction(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
        }
    }
}
