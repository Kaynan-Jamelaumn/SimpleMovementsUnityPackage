using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EquippableEffectType
{
    MaxHp,
    MaxStamina,
    MaxWeight,
    Speed,
    HpRegeneration,
    StaminaRegeneration,
    HpHealFactor,
    StaminaHealFactor,
    HpDamageFactor,
    StaminaDamageFactor,
}



[System.Serializable]
public class EquippableEffect
{
    public EquippableEffectType effectType;
    public float amount;
}


[CreateAssetMenu(fileName = "Equippable", menuName = "Scriptable Objects/Item/Equippable")]
public class EquippableSO : ItemSO
{
    //public int armor;
    //public int requiredLevel;

    [Header("Equippable Effect")]
    [SerializeField]
    private List<EquippableEffect> effects;

    private Dictionary<EquippableEffectType, System.Action<float>> effectActions;



    public override void ApplyEquippedStats(bool shouldApply, PlayerStatusController statusController)
    {

        InitializeEffectActions(statusController);

        foreach (var effect in effects)
        {
            ApplyEffect(effect, shouldApply, statusController);

        }
    }


    private void InitializeEffectActions(PlayerStatusController statusController)
    {
        // Create the dictionary with the effect types and corresponding actions
        effectActions = new Dictionary<EquippableEffectType, System.Action<float>>()
        {
            { EquippableEffectType.MaxWeight, amount => statusController.WeightManager.ModifyMaxWeight(amount) },
            { EquippableEffectType.Speed, amount => statusController.ModifySpeed(amount) },
            { EquippableEffectType.MaxStamina, amount => statusController.StaminaManager.ModifyMaxStamina(amount) },
            { EquippableEffectType.StaminaRegeneration, amount => statusController.StaminaManager.ModifyStaminaRegeneration(amount) },
            { EquippableEffectType.StaminaHealFactor, amount => statusController.StaminaManager.ModifyStaminaHealFactor(amount) },
            { EquippableEffectType.StaminaDamageFactor, amount => statusController.StaminaManager.ModifyStaminaDamageFactor(amount) },
            { EquippableEffectType.MaxHp, amount => statusController.HpManager.ModifyMaxHp(amount) },
            { EquippableEffectType.HpRegeneration, amount => statusController.HpManager.ModifyHpRegeneration(amount) },
            { EquippableEffectType.HpHealFactor, amount => statusController.HpManager.ModifyHpHealFactor(amount) },
            { EquippableEffectType.HpDamageFactor, amount => statusController.HpManager.ModifyHpDamageFactor(amount) }
        };
    }

    private void ApplyEffect(EquippableEffect effect, bool shouldApply, PlayerStatusController statusController)
    {
        // Reverse the effect amount if shouldApply is false
        float amount = effect.amount;
        if (!shouldApply) amount *= -1;

        // Try to get the action from the dictionary and invoke it
        if (effectActions.TryGetValue(effect.effectType, out var action))
        {
            action.Invoke(amount);
        }
        else
        {
            Debug.LogWarning($"Effect type {effect.effectType} is not supported.");
        }
    }
}

/*    private void ApplyEffect(EquippableEffect effect, bool shouldApply, PlayerStatusController statusController)
    {
        
        float amount = effect.amount;
        if (shouldApply == false) amount *= -1; 
        switch (effect.effectType)
        {
            case EquippableEffectType.MaxWeight:
                statusController.WeightManager.ModifyMaxWeight(amount);
                break;
            case EquippableEffectType.Speed: 
                statusController.ModifySpeed(amount);
                break;
            case EquippableEffectType.MaxStamina:
                statusController.StaminaManager.ModifyMaxStamina(amount);
                break;
            case EquippableEffectType.StaminaRegeneration: 
                statusController.StaminaManager.ModifyStaminaRegeneration(amount);
                break;
            case EquippableEffectType.StaminaHealFactor: 
                statusController.StaminaManager.ModifyStaminaHealFactor(amount);
                break;
            case EquippableEffectType.StaminaDamageFactor: 
                statusController.StaminaManager.ModifyStaminaDamageFactor(amount);
                break;
            case EquippableEffectType.MaxHp:
                statusController.HpManager.ModifyMaxHp(amount);
                break;
            case EquippableEffectType.HpRegeneration:
                statusController.HpManager.ModifyHpRegeneration(amount);
                break;
            case EquippableEffectType.HpHealFactor:
                statusController.HpManager.ModifyHpHealFactor(amount);
                break;
            case EquippableEffectType.HpDamageFactor:
                statusController.HpManager.ModifyHpDamageFactor(amount);
                break;
        }
    }
*/