using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a vessel behaves: how much it holds, how hot its fire gets and how fast it heats, whether it can cool, what it
/// does with vapour (vent, distil, seal), how well it can be stirred, and its tags and variables (a copper vessel
/// has the "copper" tag, which some reactions like). Equipment is a real variable of the rules, not a skin.
/// </summary>
[Serializable]
public class AlchemyEquipmentProfile
{
    [Tooltip("Name shown in the station window and the journal.")]
    public string displayName = "Iron Cauldron";
    [TextArea(1, 3)] public string description = "";

    [Header("Capacity")]
    [Tooltip("Most units the vessel holds (ingredients still dissolving count).")]
    [Min(1f)] public float capacity = 60f;
    [Tooltip("Thermal mass of the empty vessel: a heavy cauldron heats and cools slowly.")]
    [Min(0.1f)] public float vesselMass = 25f;

    [Header("Heat")]
    [Tooltip("Temperature (°C) the fire drives the contents toward at full heat.")]
    public float maxHeatTemperature = 240f;
    [Tooltip("The vessel can be cooled below the surroundings (ice bath, frost runes).")]
    public bool canCool;
    [Tooltip("Temperature (°C) full cooling drives the contents toward.")]
    public float coolingTemperature = -15f;
    [Tooltip("How strongly the fire (or the cooling) moves the temperature (thermal mass per second).")]
    [Min(0f)] public float heatTransfer = 2.5f;
    [Tooltip("How fast the contents drift toward the surroundings' temperature with the heat off.")]
    [Min(0f)] public float ambientTransfer = 0.8f;

    [Header("Vapour")]
    [Tooltip("Vent: vapour escapes. Condense: it is collected apart as a distillate (alembic). Sealed: it stays inside and builds pressure.")]
    public VaporHandling vapor = VaporHandling.Vent;
    [Tooltip("Sealed vessels burst above this pressure, venting and spilling part of their contents.")]
    [Min(0.1f)] public float maxPressure = 2.5f;
    [Tooltip("How much liquid surface it has: liquids evaporate below their boiling point this many times faster (a " +
             "shallow evaporating dish 6, a narrow retort 0.5).")]
    [Min(0f)] public float surface = 1f;
    [Tooltip("Distilling vessels: a fractionating column sends back what boils at higher temperatures, so the flask " +
             "collects the most volatile part almost alone (0 = everything that boils off is collected).")]
    [Min(0f)] public float fractionation;

    [Header("Handling")]
    [Tooltip("How much one stir agitates the mixture (0-1).")]
    [Range(0f, 1f)] public float stirEfficiency = 0.6f;
    [Tooltip("Transparent vessel (glass): the player identifies what they can see in it.")]
    public bool revealsSubstances;
    [Tooltip("Tags reactions can require or forbid (copper, glass, sealed, silver...).")]
    public List<string> tags = new List<string>();
    [Tooltip("Variables the vessel adds to the conditions (\"resonance\" 0.5...).")]
    public List<AlchemyVariable> variables = new List<AlchemyVariable>();

    public bool HasTag(string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag))
            return false;
        for (int i = 0; i < tags.Count; i++)
            if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>The vessel's own variable (0 when it has none; tags count as 1).</summary>
    public bool TryVariable(string name, out double value)
    {
        if (variables != null)
            for (int i = 0; i < variables.Count; i++)
                if (variables[i] != null && string.Equals(variables[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = variables[i].value;
                    return true;
                }
        if (HasTag(name))
        {
            value = 1;
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>Where the heat setting (-1 full cooling … 0 off … 1 full heat) drives the contents.</summary>
    public double TargetTemperature(double heat, double ambient)
    {
        if (heat >= 0)
            return ambient + Math.Min(1.0, heat) * (maxHeatTemperature - ambient);
        if (!canCool)
            return ambient;
        return ambient + Math.Min(1.0, -heat) * (coolingTemperature - ambient);
    }

    public AlchemyEquipmentProfile Clone()
    {
        var c = (AlchemyEquipmentProfile)MemberwiseClone();
        c.tags = tags != null ? new List<string>(tags) : new List<string>();
        c.variables = new List<AlchemyVariable>();
        if (variables != null)
            foreach (AlchemyVariable v in variables)
                if (v != null) c.variables.Add(new AlchemyVariable(v.name, v.value));
        return c;
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (maxHeatTemperature < 30f)
            warnings.Add($"{displayName}: the fire barely warms the vessel (Max Heat Temperature {maxHeatTemperature:0} °C).");
        if (canCool && coolingTemperature >= 20f)
            warnings.Add($"{displayName}: Can Cool is on but the Cooling Temperature is not below room temperature.");
        if (heatTransfer <= 0f)
            warnings.Add($"{displayName}: Heat Transfer is 0, the fire does nothing.");
    }

    /// <summary>The built-in iron cauldron (stations without equipment).</summary>
    public static AlchemyEquipmentProfile DefaultCauldron() => new AlchemyEquipmentProfile
    {
        displayName = "Iron Cauldron",
        description = "A heavy open cauldron over a fire. Holds a lot, heats slowly, lets vapour escape.",
        tags = new List<string> { "iron", "open" },
    };
}

/// <summary>
/// A vessel asset (Iron Cauldron, Copper Alembic, Glass Retort, Frost Basin...): put it on an <see cref="AlchemyStation"/>.
/// Create presets with Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Equipment Presets.
/// </summary>
[CreateAssetMenu(fileName = "Alchemy Equipment", menuName = "SimpleMovements/Alchemy/Equipment", order = 2)]
public class AlchemyEquipment : ScriptableObject
{
    [SerializeField] private AlchemyEquipmentProfile profile = AlchemyEquipmentProfile.DefaultCauldron();

    public AlchemyEquipmentProfile Profile => profile ?? (profile = AlchemyEquipmentProfile.DefaultCauldron());

    /// <summary>Sets the profile (editor tools, presets).</summary>
    public void SetProfile(AlchemyEquipmentProfile p) => profile = p != null ? p.Clone() : AlchemyEquipmentProfile.DefaultCauldron();
}
