using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An item that does something while it is worn in its equipment slot: classic stats, and any
/// <see cref="EquipmentEffect"/> (combat stats and resistances, traits, trait enhancements, passive behaviours,
/// abilities on keys, on-hit / when-hit effects, conditional effects, visuals). It can belong to an
/// <see cref="ArmorSet"/>. Equipping and unequipping are automatic (<see cref="EquipmentManager"/>).
/// </summary>
[CreateAssetMenu(fileName = "Equippable", menuName = "Scriptable Objects/Item/Equippable")]
public class EquippableSO : ItemSO
{
    [Header("Equippable Effect")]
    [Tooltip("Classic stat changes while equipped (Max Hp, Speed, regeneration, heal/damage factors, Strength, Defense...). Applied exactly once while equipped and removed exactly when unequipped.")]
    [SerializeField]
    private List<EquippableEffect> effects = new List<EquippableEffect>();

    [Tooltip("Everything else the item does while equipped: combat stats and elemental resistances, traits, trait enhancements, passive behaviours (double jump, life steal, thorns...), abilities on keys, on-hit and when-hit effects, conditional effects, visuals. Pick the type with the dropdown.")]
    [SerializeReference, SubclassSelector]
    private List<EquipmentEffect> equipEffects = new List<EquipmentEffect>();

    [Header("Armor Set Information")]
    [SerializeField]
    [Tooltip("Armor set this piece belongs to (if any). The set must also list this piece in its 'Set Pieces'.")]
    private ArmorSet belongsToArmorSet;

    [SerializeField]
    [Tooltip("Spawned on the wearer while at least one bonus of this piece's set is active.")]
    private GameObject setVisualEffect;

    [SerializeField]
    [Tooltip("Material for this piece's model while a bonus of its set is active (used by your armor visuals through ArmorSetManager.SetBonusChanged).")]
    private Material setActiveMaterial;

    // Properties
    public List<EquippableEffect> Effects => effects ?? (effects = new List<EquippableEffect>());
    public List<EquipmentEffect> EquipEffects => equipEffects ?? (equipEffects = new List<EquipmentEffect>());
    public ArmorSet BelongsToArmorSet => belongsToArmorSet;
    public GameObject SetVisualEffect => setVisualEffect;
    public Material SetActiveMaterial => setActiveMaterial;

    // Armor set related methods
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

    // Generate set piece ID dynamically when needed (replaces the removed setPieceId field)
    public virtual string GetSetPieceId()
    {
        if (!IsPartOfArmorSet()) return "";
        return $"{belongsToArmorSet.name}_{name}";
    }

    /// <summary>Sets the armor set reference (editor tools that keep both sides of the relation in sync).</summary>
    public void SetArmorSet(ArmorSet set) => belongsToArmorSet = set;

    public override void CollectEquipEffects(List<EquipmentEffect> into)
    {
        if (effects != null && effects.Count > 0)
            into.Add(new ClassicStatsEffect(effects));
        if (equipEffects != null)
        {
            for (int i = 0; i < equipEffects.Count; i++)
                if (equipEffects[i] != null)
                    into.Add(equipEffects[i]);
        }
    }

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        if (belongsToArmorSet != null)
            lines.Add($"Set: {belongsToArmorSet.SetName}");
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (effects != null)
            for (int i = 0; i < effects.Count; i++)
                effects[i]?.Validate($"Effects #{i + 1}", errors, warnings);
        EquipmentEffect.ValidateAll(equipEffects, "Equip Effects", errors, warnings);
        if (belongsToArmorSet != null)
        {
            if (!(this is ArmorSO armor))
                warnings.Add($"Belongs to the set '{belongsToArmorSet.SetName}', but only Armor items count toward armor sets.");
            else if (!belongsToArmorSet.ContainsPiece(armor))
                errors.Add($"Belongs to the set '{belongsToArmorSet.SetName}', but the set does not list this item in its Set Pieces (the piece would not count).");
        }
    }

    // Validation in editor
    protected override void OnValidate()
    {
        base.OnValidate();
        if (effects == null)
            effects = new List<EquippableEffect>();
        if (equipEffects == null)
            equipEffects = new List<EquipmentEffect>();
    }
}
