using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Enum representing the different types of effects that can be applied by equippable items.
/// </summary>
public enum EquippableEffectType
{
    MaxHp,               // Increases the player's maximum health.
    MaxStamina,          // Increases the player's maximum stamina.
    MaxWeight,           // Increases the player's maximum weight capacity.
    Speed,               // Increases the player's movement speed.
    HpRegeneration,      // Increases the player's health regeneration rate.
    StaminaRegeneration, // Increases the player's stamina regeneration rate.
    HpHealFactor,        // Modifies the player's health healing factor.
    StaminaHealFactor,   // Modifies the player's stamina healing factor.
    HpDamageFactor,      // Modifies the player's damage output based on health.
    StaminaDamageFactor, // Modifies the player's damage output based on stamina.
}

[System.Serializable]
/// <summary>
/// Class representing an individual effect that an equippable item can have.
/// </summary>
public class EquippableEffect
{
    /// <summary>
    /// The type of the effect (e.g., MaxHp, Speed, etc.).
    /// </summary>
    public EquippableEffectType effectType;

    /// <summary>
    /// The amount of the effect that is applied.
    /// </summary>
    public float amount;
}

[CreateAssetMenu(fileName = "Equippable", menuName = "Scriptable Objects/Item/Equippable")]
/// <summary>
/// ScriptableObject class for equippable items that can have various effects on the player.
/// </summary>
public class EquippableSO : ItemSO
{
    [Header("Equippable Effect")]
    [SerializeField]
    private List<EquippableEffect> effects; // List of effects this equippable item provides.

    private Dictionary<EquippableEffectType, System.Action<float>> effectActions; // Maps effect types to actions that apply them.

    /// <summary>
    /// Applies or removes the stats associated with the equippable item to/from the player.
    /// </summary>
    /// <param name="shouldApply">True if the stats should be applied, false to remove them.</param>
    /// <param name="statusController">The player's status controller to modify.</param>
    public override void ApplyEquippedStats(bool shouldApply, PlayerStatusController statusController)
    {
        // Initialize the effect actions dictionary before applying effects.
        InitializeEffectActions(statusController);

        // Iterate through each effect and apply/remove it based on the 'shouldApply' flag.
        foreach (var effect in effects)
        {
            ApplyEffect(effect, shouldApply, statusController);
        }
    }

    /// <summary>
    /// Initializes the dictionary of effect actions based on the player's status controller.
    /// </summary>
    /// <param name="statusController">The player's status controller.</param>
    private void InitializeEffectActions(PlayerStatusController statusController)
    {
        // Create the dictionary that maps each effect type to its corresponding action.
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

    /// <summary>
    /// Applies or removes an individual effect from the player.
    /// </summary>
    /// <param name="effect">The equippable effect to apply or remove.</param>
    /// <param name="shouldApply">True if the effect should be applied, false if it should be removed.</param>
    /// <param name="statusController">The player's status controller to modify.</param>
    private void ApplyEffect(EquippableEffect effect, bool shouldApply, PlayerStatusController statusController)
    {
        // Reverse the effect amount if the effect is to be removed (shouldApply is false).
        float amount = effect.amount;
        if (!shouldApply) amount *= -1;

        // Try to find the corresponding action for the effect type and invoke it.
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