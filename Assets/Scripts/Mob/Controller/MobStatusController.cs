using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


public class MobStatusController : BaseStatusController
{
    [SerializeField] private HealthManager healthManager;
    [SerializeField] private SpeedManager speedManager;

    private AbilitySpawner abilitySpawner;
    private ItemSpawner itemSpawner;
    private MobActionsController mobActionsController;
    private MobAbilityController mobAbilityController;

    public HealthManager HealthManager => healthManager;
    public SpeedManager SpeedManager => speedManager;

    private Dictionary<AttackEffectType, Action<AttackEffect, float, float, float>> effectHandlers;

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

    protected override void CacheComponents()
    {
        healthManager = GetComponentOrLogError(ref healthManager, "HealthManager");
        speedManager = GetComponentOrLogError(ref speedManager, "SpeedManager");
        abilitySpawner = GetComponentOrLogError(ref abilitySpawner, "AbilitySpawner");
        itemSpawner = GetComponentOrLogError(ref itemSpawner, "ItemSpawner");
        mobActionsController = GetComponentOrLogError(ref mobActionsController, "MobActionsController");
        mobAbilityController = GetComponent<MobAbilityController>();
    }

    public override void ValidateAssignments()
    {
        Assert.IsNotNull(healthManager, "HealthManager is not assigned.");
        Assert.IsNotNull(speedManager, "SpeedManager is not assigned.");
    }

    private void Update()
    {
        if (healthManager.Hp <= 0)
        {
            HandleDeath();
        }
    }

    protected override void Start()
    {
        base.Start();
        InitializeEffectHandlers();
    }


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
