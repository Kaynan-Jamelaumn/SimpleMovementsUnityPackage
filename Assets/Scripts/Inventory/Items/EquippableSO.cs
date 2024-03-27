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




    public override void ApplyEquippedStats(bool shouldApply, PlayerStatusController statusController)
    {
        foreach (var effect in effects)
        {
            ApplyEffect(effect, shouldApply, statusController);

        }
    }
    private void ApplyEffect(EquippableEffect effect, bool shouldApply, PlayerStatusController statusController)
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
}