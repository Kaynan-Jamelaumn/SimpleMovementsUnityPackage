using System.Collections.Generic;
using UnityEngine;

public enum ArmorSlotType
{
    Helmet,
    Chestplate,
    Leggings,
    Boots,
    Gloves,
    Shield,
    Ring,
    Trinket,
    Cloak,
    Belt,
    Shoulders,
    Bracers,
    Amulet
}

[CreateAssetMenu(fileName = "Armor", menuName = "Scriptable Objects/Item/Armor")]
public class ArmorSO : EquippableSO
{
    [Header("Armor Specific")]
    [SerializeField] private ArmorSlotType armorSlotType;
    [SerializeField] private float defenseValue;
    [SerializeField] private float magicDefenseValue;
    [SerializeField] private float durabilityModifier = 1f;

    [Header("Armor Traits")]
    [SerializeField] private List<Trait> inherentTraits = new List<Trait>();
    [SerializeField] private bool applyTraitsWhenEquipped = true;

    [Header("Visual & Audio")]
    [SerializeField] private GameObject armorModel;
    [SerializeField] private AudioClip equipArmorSound;
    [SerializeField] private AudioClip unequipArmorSound;

    // Properties
    public ArmorSlotType ArmorSlotType => armorSlotType;

    // Use the inherited BelongsToArmorSet from EquippableSO instead of duplicate field
    public ArmorSet BelongsToSet => BelongsToArmorSet;

    public float DefenseValue => defenseValue;
    public float MagicDefenseValue => magicDefenseValue;
    public float DurabilityModifier => durabilityModifier;
    public List<Trait> InherentTraits => inherentTraits;
    public bool ApplyTraitsWhenEquipped => applyTraitsWhenEquipped;
    public GameObject ArmorModel => armorModel;
    public AudioClip EquipArmorSound => equipArmorSound;
    public AudioClip UnequipArmorSound => unequipArmorSound;

    // Constructor to set armor as item type
    public ArmorSO()
    {
        // Ensure item type is always Armor, not subtypes
        itemType = ItemType.Armor;
    }

    // Awake is called when the ScriptableObject is loaded
    private void Awake()
    {
        // Ensure item type is always Armor, not subtypes like boots, leggings, etc.
        if (itemType != ItemType.Armor)
        {
            itemType = ItemType.Armor;
        }
    }

    // Get the corresponding SlotType for compatibility with existing system
    public SlotType GetSlotType()
    {
        return armorSlotType switch
        {
            ArmorSlotType.Helmet => SlotType.Helmet,
            ArmorSlotType.Chestplate => SlotType.Armor,
            ArmorSlotType.Leggings => SlotType.Leggings,
            ArmorSlotType.Boots => SlotType.Boots,
            ArmorSlotType.Gloves => SlotType.Gloves,
            ArmorSlotType.Shield => SlotType.Shield,
            ArmorSlotType.Ring => SlotType.Ring,
            ArmorSlotType.Trinket => SlotType.Trinket,
            ArmorSlotType.Cloak => SlotType.Cloak,
            ArmorSlotType.Belt => SlotType.Belt,
            ArmorSlotType.Shoulders => SlotType.Shoulders,
            ArmorSlotType.Bracers => SlotType.Wrist,
            ArmorSlotType.Amulet => SlotType.Amulet,
            _ => SlotType.Common
        };
    }

    // Check if this armor piece is part of a set
    public bool IsPartOfSet()
    {
        return BelongsToArmorSet != null;
    }

    // Get set piece identifier
    public string GetSetPieceId()
    {
        if (!IsPartOfSet()) return "";
        return $"{BelongsToArmorSet.name}_{armorSlotType}";
    }

    // Calculate effective defense with modifiers
    public float GetEffectiveDefense(float multiplier = 1f)
    {
        return defenseValue * durabilityModifier * multiplier;
    }

    public float GetEffectiveMagicDefense(float multiplier = 1f)
    {
        return magicDefenseValue * durabilityModifier * multiplier;
    }

    // Override ApplyEquippedStats to include armor-specific effects
    public override void ApplyEquippedStats(bool shouldApply, PlayerStatusController statusController)
    {
        // Apply base equippable effects
        base.ApplyEquippedStats(shouldApply, statusController);

        // Apply armor-specific defense
        float defenseModifier = shouldApply ? 1f : -1f;
        // Note: You'll need to add defense properties to PlayerStatusController
        // For now, we'll use the existing system

        // Apply inherent traits if enabled
        if (applyTraitsWhenEquipped && statusController?.TraitManager != null)
        {
            foreach (var trait in inherentTraits)
            {
                if (trait != null)
                {
                    if (shouldApply)
                    {
                        statusController.TraitManager.AddTrait(trait, true); // Skip cost for armor traits
                    }
                    else
                    {
                        statusController.TraitManager.RemoveTrait(trait, true); // Force remove
                    }
                }
            }
        }

        // Notify armor set manager of equipment change
        if (statusController?.ArmorSetManager != null)
        {
            statusController.ArmorSetManager.OnArmorEquipmentChanged(this, shouldApply);
        }
    }

    // Validation method
    private new void OnValidate()
    {
        // Ensure item type is set correctly to Armor only (not subtypes)
        if (itemType != ItemType.Armor)
        {
            itemType = ItemType.Armor;
        }

        // Validate that inherent traits don't have costs (armor traits are free)
        if (inherentTraits != null)
        {
            foreach (var trait in inherentTraits)
            {
                if (trait != null && trait.cost > 0)
                {
                    Debug.LogWarning($"Armor {name} has trait {trait.Name} with cost > 0. Armor traits should be free.");
                }
            }
        }

        // Auto-generate setPieceId if empty and part of a set
        if (IsPartOfArmorSet() && string.IsNullOrEmpty(SetPieceId))
        {
            // Update the inherited setPieceId through reflection or make it accessible
            // For now, we'll validate the set reference
        }

        // Validate that this piece is actually in the armor set's piece list
        if (BelongsToArmorSet != null && !BelongsToArmorSet.ContainsPiece(this))
        {
            Debug.LogWarning($"Armor piece '{name}' claims to belong to set '{BelongsToArmorSet.SetName}' but is not in the set's piece list!");
        }
    }
}