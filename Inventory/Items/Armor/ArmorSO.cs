using System.Collections.Generic;
using System.Linq;
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

    [Header("Armor Traits (Optional)")]
    [Tooltip("Optional traits applied when this armor is equipped - NOT required for armor set effects")]
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

    // Override the base GetSetPieceId to include armor slot type for better uniqueness
    public override string GetSetPieceId()
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

    // Validation method - SPECIFIC AND EXACT
    private new void OnValidate()
    {
        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();

        // CRITICAL CHECKS (will prevent armor set effects from working)

        // 1. Item Type Check
        if (itemType != ItemType.Armor)
        {
            errors.Add($"CRITICAL: Item Type is '{itemType}'. Must be 'Armor' for armor set system to work.");
            itemType = ItemType.Armor; // Auto-fix
        }

        // 2. Armor Set Reference Check
        if (BelongsToArmorSet == null)
        {
            warnings.Add($"INFO: This armor is not part of any armor set. If you want set bonuses, assign 'Belongs To Armor Set' field.");
        }
        else
        {
            // 3. Bidirectional Reference Check
            if (!BelongsToArmorSet.ContainsPiece(this))
            {
                errors.Add($"CRITICAL: Armor set '{BelongsToArmorSet.SetName}' does not include this armor in its 'Set Pieces' list.\n" +
                          $"FIX: Open the armor set '{BelongsToArmorSet.name}' and add this armor to its 'Set Pieces' list, OR remove the 'Belongs To Armor Set' reference from this armor.");
            }
        }

        // OPTIONAL CHECKS (won't prevent armor set effects, just best practices)

        // 4. Name Check
        if (string.IsNullOrEmpty(name) || name == "New Armor" || name.StartsWith("Armor"))
        {
            warnings.Add($"OPTIONAL: Armor name is '{name}'. Consider a more descriptive name like 'Iron Helmet' or 'Dragon Scale Boots'.");
        }

        // 5. Defense Values Check
        if (defenseValue <= 0 && magicDefenseValue <= 0)
        {
            warnings.Add($"OPTIONAL: Both Defense Value and Magic Defense Value are {defenseValue}/{magicDefenseValue}. Consider adding defensive stats.");
        }

        // 6. Inherent Traits Check (CLARIFIED AS OPTIONAL)
        if (inherentTraits != null)
        {
            int nullTraitCount = 0;
            int costlyTraitCount = 0;

            foreach (var trait in inherentTraits)
            {
                if (trait == null)
                {
                    nullTraitCount++;
                }
                else if (trait.cost > 0)
                {
                    costlyTraitCount++;
                }
            }

            if (nullTraitCount > 0)
            {
                warnings.Add($"OPTIONAL: {nullTraitCount} empty inherent trait slot(s). Remove empty slots or assign traits. Note: Inherent traits are NOT required for armor set effects.");
            }

            if (costlyTraitCount > 0)
            {
                warnings.Add($"OPTIONAL: {costlyTraitCount} inherent trait(s) have cost > 0. Armor traits are usually free (cost = 0).");
            }
        }

        //// Log errors and warnings
        //foreach (string error in errors)
        //{
        //    Debug.LogError($"ARMOR '{name}': {error}", this);
        //}

        //foreach (string warning in warnings)
        //{
        //    Debug.LogWarning($"ARMOR '{name}': {warning}", this);
        //}

        // Call base validation
        base.OnValidate();
    }

    // Get validation status for external systems
    public bool IsValidForArmorSets()
    {
        return itemType == ItemType.Armor &&
               BelongsToArmorSet != null &&
               BelongsToArmorSet.ContainsPiece(this);
    }

    // Get specific validation errors for debugging
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (itemType != ItemType.Armor)
            errors.Add($"Item Type is '{itemType}', must be 'Armor'");

        if (BelongsToArmorSet == null)
            errors.Add("Not assigned to any armor set");
        else if (!BelongsToArmorSet.ContainsPiece(this))
            errors.Add($"Armor set '{BelongsToArmorSet.SetName}' doesn't include this piece in its list");

        return errors;
    }

    // Get validation summary for UI display
    public string GetValidationSummary()
    {
        var errors = GetValidationErrors();
        if (errors.Count == 0)
            return "✓ Valid for armor set effects";

        return $"⚠️ Issues preventing armor set effects:\n" + string.Join("\n", errors.Select(e => $"• {e}"));
    }
}