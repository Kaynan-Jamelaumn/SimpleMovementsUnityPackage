using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Equipment slot of an armor piece. Serialized as numbers in assets: only add new values at the END.</summary>
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

/// <summary>
/// A piece of armor (helmet, chestplate, ring, amulet...). While worn in its slot it gives its Defense and Magic
/// Defense (which reduce Physical and Magical damage, see <see cref="CombatStats"/>), its elemental resistances, its
/// inherent traits and every effect of <see cref="EquippableSO"/>. It can belong to an <see cref="ArmorSet"/>, whose
/// bonuses activate as more pieces are worn.
/// </summary>
[CreateAssetMenu(fileName = "Armor", menuName = "Scriptable Objects/Item/Armor")]
public class ArmorSO : EquippableSO
{
    [Header("Armor Specific")]
    [Tooltip("The equipment slot this piece is worn in.")]
    [SerializeField] private ArmorSlotType armorSlotType;
    [Tooltip("Defense points while worn: reduce Physical damage (100 Defense = 50% less with the default curve).")]
    [SerializeField] private float defenseValue;
    [Tooltip("Magic Resistance points while worn: reduce Magical damage (same curve as Defense).")]
    [SerializeField] private float magicDefenseValue;
    [Tooltip("Multiplies Defense and Magic Defense (e.g. 0.8 for a worn-out piece, 1.2 for a reinforced one).")]
    [SerializeField] private float durabilityModifier = 1f;
    [Tooltip("Less (or more, when negative) damage from an element while worn, e.g. +20% Fire.")]
    [SerializeField] private List<ElementalResistance> elementalResistances = new List<ElementalResistance>();

    [Header("Armor Traits (Optional)")]
    [Tooltip("Traits given while this piece is worn (free; the player cannot remove them). Not needed for armor set bonuses.")]
    [SerializeField] private List<Trait> inherentTraits = new List<Trait>();
    [Tooltip("Give the Inherent Traits while worn.")]
    [SerializeField] private bool applyTraitsWhenEquipped = true;

    [Header("Visual & Audio")]
    [Tooltip("The piece's model (for your character visuals; listen to EquipmentManager.ItemEquipped).")]
    [SerializeField] private GameObject armorModel;
    [Tooltip("Played when the piece is put on.")]
    [SerializeField] private AudioClip equipArmorSound;
    [Tooltip("Played when the piece is taken off.")]
    [SerializeField] private AudioClip unequipArmorSound;

    // Properties
    public ArmorSlotType ArmorSlotType => armorSlotType;

    // Use the inherited BelongsToArmorSet from EquippableSO instead of duplicate field
    public ArmorSet BelongsToSet => BelongsToArmorSet;

    public float DefenseValue => defenseValue;
    public float MagicDefenseValue => magicDefenseValue;
    public float DurabilityModifier => durabilityModifier;
    public List<ElementalResistance> ElementalResistances => elementalResistances ?? (elementalResistances = new List<ElementalResistance>());
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
    public SlotType GetSlotType() => SlotTypeHelper.ArmorSlotTypeToSlotType(armorSlotType);

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

    /// <summary>Defense, Magic Defense, resistances, inherent traits, then the equippable effects.</summary>
    public override void CollectEquipEffects(List<EquipmentEffect> into)
    {
        var stats = new List<CombatStatModifier>(2);
        float def = GetEffectiveDefense();
        float mdef = GetEffectiveMagicDefense();
        if (!Mathf.Approximately(def, 0f))
            stats.Add(new CombatStatModifier(CombatStatType.Defense, def));
        if (!Mathf.Approximately(mdef, 0f))
            stats.Add(new CombatStatModifier(CombatStatType.MagicResistance, mdef));
        bool anyResist = elementalResistances != null && elementalResistances.Count > 0;
        if (stats.Count > 0 || anyResist)
            into.Add(new CombatStatsEffect(stats, elementalResistances));

        if (applyTraitsWhenEquipped && inherentTraits != null && inherentTraits.Count > 0)
            into.Add(new GrantTraitsEffect(inherentTraits));

        base.CollectEquipEffects(into);
    }

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add($"{SlotTypeHelper.GetDisplayName(armorSlotType)}");
        base.AppendTooltip(lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (itemType != ItemType.Armor)
            errors.Add($"Item Type is '{itemType}'; armor must use 'Armor' (fixed automatically when the asset is saved).");
        if (defenseValue < 0f || magicDefenseValue < 0f)
            warnings.Add("Negative Defense or Magic Defense makes the wearer take MORE damage.");
        if (durabilityModifier <= 0f)
            warnings.Add("Durability Modifier is 0 or less: the piece gives no Defense.");
        if (elementalResistances != null && elementalResistances.Any(r => r != null && r.element == ElementType.None))
            errors.Add("An elemental resistance has no element (pick Fire, Ice...).");
        if (inherentTraits != null && inherentTraits.Contains(null))
            warnings.Add("Inherent Traits has empty entries.");
        if (inherentTraits != null && inherentTraits.Count > 0 && !applyTraitsWhenEquipped)
            warnings.Add("Inherent Traits are set but 'Apply Traits When Equipped' is off, so they are never given.");
    }

    // Validation method - SPECIFIC AND EXACT
    protected override void OnValidate()
    {
        // Armor always uses the Armor item type; the slot comes from Armor Slot Type.
        if (itemType != ItemType.Armor)
            itemType = ItemType.Armor;
        if (elementalResistances == null)
            elementalResistances = new List<ElementalResistance>();
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
