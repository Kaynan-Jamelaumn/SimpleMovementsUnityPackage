using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One substance an ingredient contains: how much (per item) and how easily it comes out of the ingredient. An herb can
/// hold a healing sap that comes out readily, a volatile oil, and a bitter toxin locked in its fibres that a whole leaf
/// barely gives up - but a ground one does.
/// </summary>
[Serializable]
public class IngredientRelease : SubstanceAmount
{
    [Tooltip("How easily it comes out of the ingredient (1 = at once with the rest, 0.1 = locked in). Whole ingredients " +
             "favour what comes out easily; grinding brings out the rest too (see the database's Ingredient Forms).")]
    [Range(0.01f, 1f)] public float extractability = 1f;

    public IngredientRelease() { }

    public IngredientRelease(string substance, float amount, float extractability = 1f) : base(substance, amount)
    {
        this.extractability = extractability;
    }

    public new IngredientRelease Clone() => new IngredientRelease(substance, amount, extractability);
}

/// <summary>
/// The state an ingredient goes into a vessel in: whole, ground, an extract... Each form releases at its own speed and
/// with its own selectivity: rate of a substance = Release Speed × extractability ^ Selectivity ÷ dissolve seconds. A
/// whole herb (selectivity 2) gives its easy substances and little of the locked ones; ground (×3, selectivity 0.5) gives
/// everything faster - wanted or not; an extract gives what it holds at once. New forms are data.
/// </summary>
[Serializable]
public class AlchemyIngredientForm
{
    [Tooltip("Stable id (ingredient profiles and operations name it): whole, ground, extract...")]
    public string id = "whole";
    public string displayName = "Whole";
    [Tooltip("Release speed × this.")]
    [Min(0.01f)] public float releaseSpeed = 1f;
    [Tooltip("How much it favours easily extracted substances: rate × extractability ^ this. 0 = all alike; 2 = locked " +
             "substances barely come out.")]
    [Min(0f)] public float selectivity = 1f;
    [Tooltip("The form an ingredient of this form becomes when ground at a station with a mortar (empty = it cannot be ground).")]
    public string groundInto = "";
    [Tooltip("What the player sees (\"ground to a coarse paste\").")]
    public string observation = "";
    [Tooltip("Grinding this fine warms the ingredient: share of its volatile substances (boiling below 150 °C) lost to " +
             "the air before it goes in - the more volatile, the more is lost.")]
    [Range(0f, 1f)] public float volatileLoss;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    /// <summary>Share of <paramref name="s"/> lost to the air when ground to this form (volatile oils, not solvents or solids).</summary>
    public double LossFor(AlchemySubstance s)
    {
        if (volatileLoss <= 0f || s == null || s.phase != SubstancePhase.Liquid || s.role == SubstanceRole.Solvent)
            return 0;
        return volatileLoss * Math.Max(0.0, Math.Min(1.0, (150.0 - s.boilingPoint) / 100.0));
    }

    /// <summary>Share of a substance released per second at 20 °C, still (infinity = at once).</summary>
    public double RatePerSecond(float dissolveSeconds, float extractability)
    {
        if (dissolveSeconds <= 1e-4f)
            return double.PositiveInfinity;
        double e = Math.Max(0.01, Math.Min(1.0, extractability));
        return releaseSpeed * Math.Pow(e, selectivity) / dissolveSeconds;
    }

    /// <summary>
    /// The built-in forms. Grinding goes whole → partially ground → finely ground → pulverised: each level gives up its
    /// substances faster and less selectively (the locked-in, unwanted parts too); pulverising also warms the herb and its
    /// volatile oils escape.
    /// </summary>
    public static List<AlchemyIngredientForm> Defaults() => new List<AlchemyIngredientForm>
    {
        new AlchemyIngredientForm { id = "whole", displayName = "Whole", releaseSpeed = 1f, selectivity = 2f, groundInto = "coarse" },
        new AlchemyIngredientForm { id = "coarse", displayName = "Partially ground", releaseSpeed = 1.8f, selectivity = 1.6f, groundInto = "ground", observation = "crushed into coarse pieces" },
        new AlchemyIngredientForm { id = "ground", displayName = "Finely ground", releaseSpeed = 3f, selectivity = 0.5f, groundInto = "powder", observation = "ground to a fine paste" },
        new AlchemyIngredientForm { id = "powder", displayName = "Pulverised", releaseSpeed = 4f, selectivity = 0.15f, volatileLoss = 0.4f, observation = "pounded to a fine dust - its scent fills the air" },
        new AlchemyIngredientForm { id = "extract", displayName = "Extract", releaseSpeed = 8f, selectivity = 0f },
    };
}

/// <summary>
/// What one ingredient puts into a mixture: the substances it contains (per item, each with its extractability), the
/// form it comes in, how long it takes to give them up, and its own temperature (a frozen herb cools the pot).
/// Ingredients carry no recipe: what happens next is decided by the reactions those substances take part in, and each
/// substance of the same herb behaves its own way.
/// </summary>
[Serializable]
public class AlchemyIngredientProfile
{
    [Tooltip("Substances released per item, with how easily each comes out (they enter the mixture as it is extracted).")]
    public List<IngredientRelease> releases = new List<IngredientRelease>();
    [Tooltip("Seconds for an easily extracted substance to come out completely, whole, at 20 °C, without stirring (heat, " +
             "stirring and grinding speed it up). 0 = at once (liquids, powders).")]
    [Min(0f)] public float dissolveSeconds = 15f;
    [Tooltip("The form it comes in (an Ingredient Form id: whole, ground, extract).")]
    public string form = "whole";
    [Tooltip("It can be ground at a station with a mortar (herbs, roots, crystals; not liquids).")]
    public bool canBeGround = true;
    [Tooltip("How hard it is to grind (0 = soft petals, 1 = rock): harder ingredients need more turns of the pestle.")]
    [Range(0f, 1f)] public float hardness = 0.4f;
    [Tooltip("Its temperature (°C) when added: it warms or cools the mixture.")]
    public float temperature = 20f;
    [Tooltip("What the player sees when adding it (\"the petals float, then sink\").")]
    public string observation = "";

    public AlchemyIngredientProfile() { }

    public AlchemyIngredientProfile(float dissolveSeconds, params IngredientRelease[] releases)
    {
        this.dissolveSeconds = dissolveSeconds;
        this.releases.AddRange(releases);
    }

    /// <summary>Units released by one item.</summary>
    public float UnitsPerItem
    {
        get
        {
            float total = 0f;
            if (releases != null)
                foreach (IngredientRelease r in releases)
                    if (r != null) total += Mathf.Max(0f, r.amount);
            return total;
        }
    }

    public AlchemyIngredientProfile Clone()
    {
        var c = new AlchemyIngredientProfile
        {
            dissolveSeconds = dissolveSeconds, form = form, canBeGround = canBeGround, hardness = hardness, temperature = temperature, observation = observation,
        };
        if (releases != null)
            foreach (IngredientRelease r in releases)
                if (r != null) c.releases.Add(r.Clone());
        return c;
    }

    public void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings)
    {
        if (releases == null || releases.Count == 0)
            return; // a supply (filter paper, firewood): it goes into no vessel
        foreach (IngredientRelease r in releases)
        {
            if (r == null || string.IsNullOrWhiteSpace(r.substance))
                errors.Add($"{where}: an empty release.");
            else if (rules != null && rules.IndexOf(r.substance) < 0)
                errors.Add($"{where}: unknown substance '{r.substance}'.");
        }
        if (rules != null && !string.IsNullOrWhiteSpace(form) && !rules.HasForm(form))
            warnings.Add($"{where}: unknown form '{form}' (released as whole).");
    }
}

/// <summary>
/// Anything that can go into a vessel: <see cref="AlchemyIngredientSO"/> items, brewed potions poured back into a
/// new preparation (<see cref="BrewedPotionSO"/>), or your own item types.
/// </summary>
public interface IAlchemyIngredient
{
    /// <summary>Stable id (history, journal): the item's asset name.</summary>
    string AlchemyId { get; }
    /// <summary>Name shown in the station window and the journal.</summary>
    string AlchemyName { get; }
    AlchemyIngredientProfile AlchemyProfile { get; }
}

/// <summary>Makes an existing item (one that is not an Alchemy Ingredient) usable in alchemy, from the database.</summary>
[Serializable]
public class AlchemyItemIngredient
{
    [Tooltip("Any item: a food, a material, a monster part...")]
    public ItemSO item;
    public AlchemyIngredientProfile profile = new AlchemyIngredientProfile();
}

/// <summary>An ingredient given by code (tests, scripts, free supplies).</summary>
public sealed class AlchemyIngredientSpec : IAlchemyIngredient
{
    public string AlchemyId { get; }
    public string AlchemyName { get; }
    public AlchemyIngredientProfile AlchemyProfile { get; }

    public AlchemyIngredientSpec(string id, string name, AlchemyIngredientProfile profile)
    {
        AlchemyId = id;
        AlchemyName = string.IsNullOrWhiteSpace(name) ? id : name;
        AlchemyProfile = profile ?? new AlchemyIngredientProfile();
    }
}
