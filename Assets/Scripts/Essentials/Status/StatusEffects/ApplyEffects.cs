
using UnityEngine;

public  class ApplyEffects : MonoBehaviour
{
    public void ApplyEffectsToController(GameObject targetGameObject, AttackEffect effect)
    {
        if (Random.value <= effect.probabilityToApply)
            ApplyEffect(effect, targetGameObject.GetComponent<MonoBehaviour>());
        //if (effect.enemyEffect == false)
        //{
        //    ApplyEffect(effect, targetGameObject.GetComponent<MonoBehaviour>());
        //}
        //else
        //{
        //    ApplyEffect(effect, targetGameObject.GetComponent<MonoBehaviour>());
        //}
    }

    public void ApplyEffect<T>(AttackEffect effect, T statusController) where T : MonoBehaviour
    {
        float amount = GenericMethods.GetRandomValue(effect.amount, effect.randomAmount, effect.minAmount, effect.maxAmount);
        float criticalMultiplier = (Random.value <= effect.criticalChance) ? effect.criticalDamageMultiplier : 1.0f;
        amount *= criticalMultiplier;
        float timeBuffEffect = GenericMethods.GetRandomValue(effect.timeBuffEffect, effect.randomTimeBuffEffect, effect.minTimeBuffEffect, effect.maxTimeBuffEffect);
        float tickCooldown = GenericMethods.GetRandomValue(effect.tickCooldown, effect.randomTickCooldown, effect.minTickCooldown, effect.maxTickCooldown);
        PlayerStatusController playerController = statusController.GetComponent<PlayerStatusController>();
        if (playerController != null)
            ApplyEffectToPlayer(effect, playerController, amount, timeBuffEffect, tickCooldown);
        MobStatusController mobController = statusController.GetComponent<MobStatusController>();
        if (mobController != null)
            ApplyEffectToMob(effect, mobController, amount, timeBuffEffect, tickCooldown);
    }

    public void ApplyEffectToPlayer(AttackEffect effect, PlayerStatusController playerController, float amount, float timeBuffEffect, float tickCooldown)
    {
        switch (effect.effectType)
        {
            case AttackEffectType.Stamina:
                if (effect.timeBuffEffect == 0) playerController.StaminaManager.AddStamina(effect.amount);
                else playerController.StaminaManager.AddStaminaEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Hp:
                if (effect.timeBuffEffect == 0) playerController.HpManager.AddHp(effect.amount);
                else playerController.HpManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Food:
                if (effect.timeBuffEffect == 0) playerController.FoodManager.AddFood(effect.amount);
                else playerController.FoodManager.AddFoodEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Drink:
                if (effect.timeBuffEffect == 0) playerController.DrinkManager.AddDrink(effect.amount);
                else playerController.DrinkManager.AddDrinkEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.Weight:
                if (effect.timeBuffEffect == 0) playerController.WeightManager.AddWeight(effect.amount);
                else playerController.WeightManager.AddWeightEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpHealFactor:
                playerController.HpManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpDamageFactor:
                playerController.HpManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaHealFactor:
                playerController.StaminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaDamageFactor:
                playerController.StaminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.StaminaRegeneration:
                playerController.StaminaManager.AddStaminaRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpRegeneration:
                playerController.HpManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
        }
    }

    public void ApplyEffectToMob(AttackEffect effect, MobStatusController mobController, float amount, float timeBuffEffect, float tickCooldown)
    {

        switch (effect.effectType)
        {
            case AttackEffectType.Hp:
                if (effect.timeBuffEffect == 0) mobController.HealthManager.AddHp(effect.amount);
                else mobController.HealthManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpHealFactor:
                mobController.HealthManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpDamageFactor:
                mobController.HealthManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case AttackEffectType.HpRegeneration:
                mobController.HealthManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
        }
    }
}
