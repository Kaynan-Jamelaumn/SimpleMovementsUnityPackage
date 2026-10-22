using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a filter takes out of the liquid besides particles: a share of everything a selector picks.</summary>
[Serializable]
public class FilterAdsorption
{
    [Tooltip("What it holds back (a substance, anything with a property, any compound of a role).")]
    public SubstanceSelector substance = new SubstanceSelector();
    [Tooltip("Share of it that stays in the filter.")]
    [Range(0f, 1f)] public float share = 0.5f;

    public FilterAdsorption() { }

    public FilterAdsorption(SubstanceSelector substance, float share)
    {
        this.substance = substance;
        this.share = share;
    }
}

/// <summary>
/// A way of pouring the liquid through something: a cloth strainer catches the spent herbs and coarse particles, filter
/// paper every particle, a bed of charcoal also holds back toxins and residues (and a little of everything else).
/// Filters are data (the database's Filters): what they catch, what they adsorb, how much liquid they soak up, and the
/// item they use up.
/// </summary>
[Serializable]
public class AlchemyFilter
{
    [Tooltip("Stable id (history, stations).")]
    public string id = "strainer";
    public string displayName = "Cloth Strainer";
    [TextArea(1, 3)] public string description = "";
    [Tooltip("Strains out the ingredients still in the vessel (spent herbs, roots): what they still hold leaves with them.")]
    public bool strainsIngredients = true;
    [Tooltip("Share of the undissolved matter it catches (crystals, powders, silt, curds...).")]
    [Range(0f, 1f)] public float catchesUndissolved = 0.6f;
    [Tooltip("Share of the solids at the bottom it catches.")]
    [Range(0f, 1f)] public float catchesSolids = 0.8f;
    [Tooltip("Dissolved substances it holds back (a charcoal bed adsorbs toxins and residues).")]
    public List<FilterAdsorption> adsorbs = new List<FilterAdsorption>();
    [Tooltip("Share of the liquid that stays soaked in the filter.")]
    [Range(0f, 1f)] public float liquidLoss = 0.02f;
    [Tooltip("An item used up each time (its name or ingredient id: \"Filter Paper\", \"Charcoal\"). Empty = reusable.")]
    public string medium = "";
    [Tooltip("What the player sees.")]
    public string observation = "";

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    public AlchemyFilter Adsorbs(SubstanceSelector s, float share)
    {
        adsorbs.Add(new FilterAdsorption(s, share));
        return this;
    }
}

/// <summary>
/// Something the fire of a manual fire burns: how much fuel one item is worth (units: one burns about 50 s at full fire)
/// and whether it flares up when it catches (resin). Fuels are data (the database's Fuels).
/// </summary>
[Serializable]
public class AlchemyFuel
{
    [Tooltip("Stable id (history, stations).")]
    public string id = "firewood";
    public string displayName = "Firewood";
    [Tooltip("Fuel units per item (firewood 1 = about 50 s at full fire).")]
    [Min(0.01f)] public float energy = 1f;
    [Tooltip("A burst of flame when it catches (0-1, like a pump of the bellows): resin flares, charcoal does not.")]
    [Range(0f, 1f)] public float flare;
    [Tooltip("The item used up (its name or ingredient id). Empty = free: a woodpile at the station.")]
    public string item = "";
    [Tooltip("What the player sees.")]
    public string observation = "";

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}
