using System.Collections;
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
    public override void ApplyEffect(AttackEffect effect, float amount, float timeBuffEffect, float tickCooldown)
    {
        switch (effect.effectType)
        {
            case AttackEffectType.Hp:
                if (timeBuffEffect == 0) HealthManager.AddHp(amount);
                else HealthManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpHealFactor:
                HealthManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpDamageFactor:
                HealthManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpRegeneration:
                HealthManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
        }
    }
}
