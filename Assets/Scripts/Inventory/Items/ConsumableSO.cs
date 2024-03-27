using System.Collections.Generic;
using UnityEngine;

public enum ConsumableEffectType
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
public enum ConsumableType
{
   Potion,
   Food
}


[System.Serializable]
public class ConsumableEffect
{
    public ConsumableEffectType effectType;
    public ConsumableType itemType;
    public string effectName;
    public float amount;
    [Tooltip("the duration of the effect")] public float timeBuffEffect;
    [Tooltip("how much time for the effect to tick again")] public float tickCooldown;
    [Tooltip("the amount will be divided by the time")] public bool isProcedural;
    [Tooltip("if the same affect is being applied can it be applied again or it wont and only reset the time")] public bool isStackable;
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
}


[CreateAssetMenu(fileName = "Consumable", menuName = "Scriptable Objects/Item/Consumable")]
public class ConsumableSO : ItemSO
{
    [Header("Consumable Buff")]
    [SerializeField] private List<ConsumableEffect> effects;

    public List<ConsumableEffect> Effects
    {
        get { return effects; }
    }
    public ConsumableSO()
    {
        durabilityReductionPerUse = 1;
    }
    public override void UseItem(GameObject playerObject, PlayerStatusController statusController = null)
    {
        base.UseItem(playerObject, statusController);

        foreach (var effect in effects)
        {
            ApplyEffect(effect, statusController);

        }
    }

    private void ApplyEffect(ConsumableEffect effect, PlayerStatusController statusController)
    {
        float amount = effect.randomAmount ? Random.Range(effect.minAmount, effect.maxAmount) : effect.amount;
        float timeBuffEffect = effect.randomTimeBuffEffect ? Random.Range(effect.minTimeBuffEffect, effect.maxTimeBuffEffect) : effect.timeBuffEffect;
        float tickCooldown = effect.randomTickCooldown ? Random.Range(effect.minTickCooldown, effect.maxTickCooldown) : effect.tickCooldown;

        switch (effect.effectType)
        {
            case ConsumableEffectType.Stamina:
                if (effect.timeBuffEffect == 0) statusController.StaminaManager.AddStamina(effect.amount);
                else statusController.StaminaManager.AddStaminaEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.Hp:
                if  (effect.timeBuffEffect ==0) statusController.HpManager.AddHp(effect.amount);
                else statusController.HpManager.AddHpEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.Food:
                if (effect.timeBuffEffect == 0) statusController.FoodManager.AddFood(effect.amount);
                else statusController.FoodManager.AddFoodEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.Drink:
                if (effect.timeBuffEffect == 0) statusController.DrinkManager.AddDrink(effect.amount);
                else statusController.DrinkManager.AddDrinkEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.Weight:
                statusController.WeightManager.AddWeightEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.HpHealFactor:
                statusController.HpManager.AddHpHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.HpDamageFactor:
                statusController.HpManager.AddHpDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.StaminaHealFactor:
                statusController.StaminaManager.AddStaminaHealFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.StaminaDamageFactor:
                statusController.StaminaManager.AddStaminaDamageFactorEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.StaminaRegeneration:
                statusController.StaminaManager.AddStaminaRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
            case ConsumableEffectType.HpRegeneration:
                statusController.HpManager.AddHpRegenEffect(effect.effectName, amount, timeBuffEffect, tickCooldown, effect.isProcedural, effect.isStackable);
                break;
        }
    }
}
