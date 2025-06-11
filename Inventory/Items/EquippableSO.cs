using System.Collections.Generic;
using UnityEngine;

// Extended EquippableSO class with armor set support
[CreateAssetMenu(fileName = "Equippable", menuName = "Scriptable Objects/Item/Equippable")]
public class EquippableSO : ItemSO
{
    [Header("Equippable Effect")]
    [SerializeField]
    private List<EquippableEffect> effects; // List of effects this equippable item provides.

    [Header("Armor Set Information")]
    [SerializeField]
    [Tooltip("Armor set this piece belongs to (if any)")]
    private ArmorSet belongsToArmorSet; // Single field for armor set reference

    [SerializeField]
    [Tooltip("Unique identifier for this piece within the set")]
    private string setPieceId;

    [SerializeField]
    [Tooltip("Visual indicator when part of an active set")]
    private GameObject setVisualEffect;

    [SerializeField]
    [Tooltip("Material override when set effects are active")]
    private Material setActiveMaterial;

    private Dictionary<EquippableEffectType, System.Action<float>> effectActions; // Maps effect types to actions that apply them.

    // Properties - Clear naming to avoid confusion
    public List<EquippableEffect> Effects => effects;
    public ArmorSet BelongsToArmorSet => belongsToArmorSet; // Main armor set reference
    public string SetPieceId => setPieceId;
    public GameObject SetVisualEffect => setVisualEffect;
    public Material SetActiveMaterial => setActiveMaterial;

    // Armor set related methods - renamed for clarity
    public bool IsPartOfArmorSet()
    {
        return belongsToArmorSet != null;
    }

    public bool IsPartOfArmorSet(ArmorSet armorSet)
    {
        return belongsToArmorSet == armorSet;
    }

    public string GetSetName()
    {
        return belongsToArmorSet != null ? belongsToArmorSet.SetName : "No Set";
    }

    public bool IsCompatibleWithSet(ArmorSet armorSet)
    {
        if (armorSet == null) return false;
        return armorSet.ContainsPiece(this as ArmorSO);
    }

    // Apply or remove equipment stats
    public override void ApplyEquippedStats(bool shouldApply, PlayerStatusController statusController)
    {
        // Initialize the effect actions dictionary before applying effects.
        InitializeEffectActions(statusController);

        // Iterate through each effect and apply/remove it based on the 'shouldApply' flag.
        foreach (EquippableEffect effect in effects)
        {
            ApplyEffect(effect, shouldApply, statusController);
        }
    }

    // Initialize the effect actions dictionary with functions that modify player stats.
    private void InitializeEffectActions(PlayerStatusController statusController)
    {
        effectActions = new Dictionary<EquippableEffectType, System.Action<float>>
        {
            { EquippableEffectType.MaxWeight, amount => statusController.WeightManager.ModifyMaxWeight(amount) },
            { EquippableEffectType.Speed, amount => statusController.SpeedManager.ModifyBaseSpeed(amount) },
            { EquippableEffectType.MaxStamina, amount => statusController.StaminaManager.ModifyMaxValue(amount) },
            { EquippableEffectType.StaminaRegeneration, amount => statusController.StaminaManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.StaminaHealFactor, amount => statusController.StaminaManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.StaminaDamageFactor, amount => statusController.StaminaManager.ModifyDecrementFactor(amount) },
            { EquippableEffectType.MaxHp, amount => statusController.HpManager.ModifyMaxValue(amount) },
            { EquippableEffectType.HpRegeneration, amount => statusController.HpManager.ModifyIncrementValue(amount) },
            { EquippableEffectType.HpHealFactor, amount => statusController.HpManager.ModifyIncrementFactor(amount) },
            { EquippableEffectType.HpDamageFactor, amount => statusController.HpManager.ModifyDecrementFactor(amount) }
        };
    }

    // Apply a specific effect to the player stats.
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

    // Get formatted description including set information
    public override string ToString()
    {
        string description = $"{Name}";

        if (IsPartOfArmorSet())
        {
            description += $" ({GetSetName()})";
        }

        return description;
    }

    // Validation and setup
    private new void OnValidate()
    {
        // Auto-generate setPieceId if empty and part of a set
        if (IsPartOfArmorSet() && string.IsNullOrEmpty(setPieceId))
        {
            setPieceId = $"{itemType}_{name}";
        }

        // Validate that this piece is actually in the armor set's piece list
        if (belongsToArmorSet != null && this is ArmorSO armorSO && !belongsToArmorSet.ContainsPiece(armorSO))
        {
            Debug.LogWarning($"Armor piece '{name}' claims to belong to set '{belongsToArmorSet.SetName}' but is not in the set's piece list!");
        }
    }
}