using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a substance is to the alchemist. The rules read it: potions come from <see cref="Active"/> compounds, a mixture
/// still full of <see cref="Raw"/> matter is an incomplete preparation, <see cref="Residue"/> is what is left of a
/// preparation that went too far. Serialized as numbers in assets: only add new values at the END.
/// </summary>
public enum SubstanceRole
{
    /// <summary>The base liquid everything dissolves in (water, spirit, oil). Dilutes the other substances.</summary>
    Solvent,
    /// <summary>Unrefined matter released by an ingredient (sap, oils, dusts) that has not reacted yet.</summary>
    Raw,
    /// <summary>A compound on its way to something else (not a potion by itself).</summary>
    Intermediate,
    /// <summary>A compound with an effect: the one that dominates a mixture decides the potion.</summary>
    Active,
    /// <summary>Something that changes how reactions go without being what the potion is about (quicksilver, salts).</summary>
    Reagent,
    /// <summary>A side product: harmless, sometimes useful (crystals, spirits).</summary>
    Byproduct,
    /// <summary>What degraded preparations are made of (ash, sludge, char).</summary>
    Residue,
}

/// <summary>How a substance behaves in the vessel. Serialized as numbers: only add new values at the END.</summary>
public enum SubstancePhase
{
    /// <summary>Dissolved in the liquid; boils away above its boiling point; poured into the bottles.</summary>
    Liquid,
    /// <summary>Settles at the bottom: never boils, stays behind when the liquid is bottled, harvested when emptied.</summary>
    Solid,
}

/// <summary>What happens to what boils off. Serialized as numbers: only add new values at the END.</summary>
public enum VaporHandling
{
    /// <summary>Open vessel (cauldron): vapour escapes and is lost.</summary>
    Vent,
    /// <summary>Distilling vessel (alembic): vapour is cooled and collected apart, as a distillate.</summary>
    Condense,
    /// <summary>Closed vessel (retort): vapour stays inside and builds pressure; it condenses back as it cools.</summary>
    Sealed,
}

/// <summary>What a selector picks in a mixture. Serialized as numbers: only add new values at the END.</summary>
public enum SubstanceSelectorMode
{
    /// <summary>One substance, by id.</summary>
    Substance,
    /// <summary>Every substance whose property (by id) is at least the minimum: "anything fiery", "anything organic".</summary>
    Property,
    /// <summary>Every substance of a role: "any active compound", "any raw matter".</summary>
    Role,
}

/// <summary>A preparation's verdict family. Serialized as numbers: only add new values at the END.</summary>
public enum AlchemyOutcomeKind
{
    /// <summary>A working potion.</summary>
    Potion,
    /// <summary>A useful material for other crafting (extracts, crystals...).</summary>
    Material,
    /// <summary>An unstable compound: reacts badly, dangerous.</summary>
    Unstable,
    /// <summary>Taken off the fire too early: mostly unreacted ingredients.</summary>
    Incomplete,
    /// <summary>Went too far: ash, sludge, char.</summary>
    Degraded,
    /// <summary>Nothing happened: plain solvent, no product.</summary>
    Inert,
    /// <summary>Anything else that matched no other rule.</summary>
    Failed,
}

/// <summary>
/// The layer a liquid goes to when the vessel holds a watery and an oily solvent at once (they do not mix: the lighter
/// floats). Serialized as numbers: only add new values at the END.
/// </summary>
public enum LiquidLayer
{
    /// <summary>Solvents: mixes with both (spirit). Other substances: the layer whose solvent holds it best.</summary>
    Auto,
    /// <summary>The watery layer (water and what dissolves in it).</summary>
    Aqueous,
    /// <summary>The oily layer (oils, turpentine and what dissolves in them).</summary>
    Oily,
}

/// <summary>How a substance gets along with another one. Serialized as numbers: only add new values at the END.</summary>
public enum InteractionKind
{
    /// <summary>The other slows every reaction that consumes this substance (it gets in the way).</summary>
    Interferes,
    /// <summary>They destroy each other (into products, or into nothing) as soon as they meet.</summary>
    Clashes,
    /// <summary>In the other's presence it comes out of solution (milk curdles in acid).</summary>
    Curdles,
}

/// <summary>What the vessel stands in. Serialized as numbers: only add new values at the END.</summary>
public enum AlchemyBath
{
    /// <summary>On the fire.</summary>
    None,
    /// <summary>A pan of water over the fire: gentle, even heat that never goes much above 96 °C, and nothing scorches.</summary>
    Water,
    /// <summary>A tub of ice: cools fast toward 0 °C whatever the fire does (preserves what heat destroys, starts crystals).</summary>
    Ice,
}

/// <summary>A presentation hint a reaction gives while it runs (bubbles, smoke...). Never changes the rules.</summary>
public enum ReactionVisual
{
    None,
    Bubbles,
    Smoke,
    Sparks,
    Flash,
    Glow,
    Hiss,
}

/// <summary>Something that happened in a preparation (for the log, the journal, sounds and effects).</summary>
public enum AlchemyEventKind
{
    IngredientAdded,
    IngredientDissolved,
    SubstanceAdded,
    ReactionStarted,
    ReactionStopped,
    SubstanceAppeared,
    SubstanceDepleted,
    BoilingStarted,
    BoilingStopped,
    Eruption,
    VesselRuptured,
    Suspended,
    Resumed,
    Bottled,
    DistillateCollected,
    Emptied,
    Refused,
    /// <summary>A substance came out of solution (too little or the wrong solvent, cooling, boiling off): it settles undissolved.</summary>
    Precipitated,
    /// <summary>A saturated substance started to crystallise (slow cooling, stillness, a seed crystal).</summary>
    CrystalsForming,
    /// <summary>Stirred too hard: part of the contents sloshed over the rim.</summary>
    Splashed,
    /// <summary>The fire ran out of fuel (manual fire).</summary>
    FireOut,
    /// <summary>The liquid was poured through a filter (Text: the filter's name).</summary>
    Filtered,
    /// <summary>The top layer was poured off.</summary>
    Decanted,
    /// <summary>What floated on the liquid was skimmed off.</summary>
    Skimmed,
}

/// <summary>An amount of one substance (by id).</summary>
[Serializable]
public class SubstanceAmount
{
    [Tooltip("Id of the substance (see the Alchemy Database's Substances).")]
    public string substance = "";
    [Tooltip("How much, in units (u). A bottle holds 5 u by default.")]
    [Min(0f)] public float amount = 1f;

    public SubstanceAmount() { }

    public SubstanceAmount(string substance, float amount)
    {
        this.substance = substance;
        this.amount = amount;
    }

    public SubstanceAmount Clone() => new SubstanceAmount(substance, amount);
}

/// <summary>One property value of a substance ("fire 0.8").</summary>
[Serializable]
public class PropertyValue
{
    [Tooltip("Id of the property (see the Alchemy Database's Properties).")]
    public string property = "";
    [Tooltip("How strongly the substance has it (usually 0-1; negative values cancel it in a mixture).")]
    public float value = 0.5f;

    public PropertyValue() { }

    public PropertyValue(string property, float value)
    {
        this.property = property;
        this.value = value;
    }
}

/// <summary>A named number (an equipment's own condition variables: "copper" = 1).</summary>
[Serializable]
public class AlchemyVariable
{
    public string name = "";
    public float value = 1f;

    public AlchemyVariable() { }

    public AlchemyVariable(string name, float value)
    {
        this.name = name;
        this.value = value;
    }
}

/// <summary>How much of a substance one unit of a solvent can hold at 20 °C.</summary>
[Serializable]
public class SolventSolubility
{
    [Tooltip("Id of a Solvent substance (water, spirit, oil...).")]
    public string solvent = "";
    [Tooltip("Units of the substance one unit of this solvent holds dissolved at 20 °C (water holds 0.36 u of salt per u).")]
    [Min(0f)] public float unitsPerUnit = 1f;

    public SolventSolubility() { }

    public SolventSolubility(string solvent, float unitsPerUnit)
    {
        this.solvent = solvent;
        this.unitsPerUnit = unitsPerUnit;
    }
}

/// <summary>
/// How a substance gets along with another: interferes with its reactions, clashes with it, or curdles in its presence.
/// The rules turn clashes into reactions of their own and interferences into modifiers of the reactions it takes part in.
/// </summary>
[Serializable]
public class SubstanceInteraction
{
    public InteractionKind kind = InteractionKind.Interferes;
    [Tooltip("What it interacts with (a substance, anything with a property, any compound of a role).")]
    public SubstanceSelector with = new SubstanceSelector();
    [Tooltip("Interferes: its reactions run (1 + this) times slower once the other is 2% of the liquid. Clashes: the speed " +
             "of the clash (per second). Curdles: what the liquid holds of it is divided by (1 + this × the other's share in %).")]
    [Min(0f)] public float strength = 1f;
    [Tooltip("Clashes: what the two become (units per unit of each; empty = both are simply destroyed).")]
    public List<ReactionProduct> products = new List<ReactionProduct>();
    [Tooltip("What the player sees (\"it hisses and the green film vanishes\").")]
    public string observation = "";
}

/// <summary>
/// A trait substances can have (Vitality, Fire, Toxicity, Volatility...). Properties make the rules general: a
/// reaction can take "anything organic" instead of naming every herb, and a potion can require "toxicity below 0.1"
/// whatever made it toxic. New properties need no code: add them here and give them to substances.
/// </summary>
[Serializable]
public class AlchemyPropertyDefinition
{
    [Tooltip("Stable id used by substances, reactions and outcomes (lower case, no spaces: \"fire\").")]
    public string id = "";
    [Tooltip("Name shown in the journal and the station window.")]
    public string displayName = "";
    [Tooltip("What it means (shown in the journal).")]
    [TextArea(1, 3)] public string description = "";
    [Tooltip("Colour of its bar in the station window.")]
    public Color color = Color.white;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>
/// One chemical species of the alchemy system: what ingredients release, what reactions turn into one another, what
/// potions are made of. It has a role, a phase, a boiling point, a colour and properties.
/// </summary>
[Serializable]
public class AlchemySubstance
{
    [Tooltip("Stable id used by ingredients, reactions, outcomes, saves and the journal (lower case, no spaces).")]
    public string id = "";
    [Tooltip("Name shown once the player has identified it.")]
    public string displayName = "";
    [Tooltip("What it is (journal).")]
    [TextArea(1, 3)] public string description = "";
    [Tooltip("What the player notices before identifying it (\"a golden, sweet-smelling liquid\").")]
    public string observation = "";
    [Tooltip("What it is to the rules: Solvent dilutes, Raw = unreacted ingredient matter, Active compounds make potions, " +
             "Residue is what degraded preparations are made of.")]
    public SubstanceRole role = SubstanceRole.Raw;
    [Tooltip("Liquid: dissolved, boils, is bottled. Solid: settles, never boils, stays behind and is harvested when the vessel is emptied.")]
    public SubstancePhase phase = SubstancePhase.Liquid;
    [Tooltip("Liquids boil away above this temperature (°C): the mixture stays near the boiling point while it boils.")]
    public float boilingPoint = 100f;
    [Tooltip("How fast it boils away once above its boiling point (1 = normal).")]
    [Min(0f)] public float evaporationMultiplier = 1f;
    [Tooltip("Colour it gives the mixture.")]
    public Color color = new Color(0.6f, 0.6f, 0.6f, 1f);
    [Tooltip("How strongly it colours the mixture compared with others (water is faint).")]
    [Min(0f)] public float colorStrength = 1f;
    [Tooltip("Its traits (vitality 1, fire 0.3...). Reactions and outcomes can read them.")]
    public List<PropertyValue> properties = new List<PropertyValue>();
    [Tooltip("Collected as this item when the vessel is emptied (solids) or the distillate is collected (liquids). Empty = not collected.")]
    public ItemSO harvestItem;
    [Tooltip("Units of the substance per harvested item.")]
    [Min(0.01f)] public float unitsPerItem = 5f;
    [Tooltip("Known from the start (water): never shown as unknown.")]
    public bool alwaysKnown;

    [Header("Solubility")]
    [Tooltip("How much of it each solvent holds. Empty = it dissolves without limit. A mixture of solvents holds what " +
             "their blend holds (log-linear: a spirit solution precipitates when much water is added). What does not fit " +
             "stays undissolved at the bottom (or floating): it is not bottled and does not react until it dissolves again.")]
    public List<SolventSolubility> solubility = new List<SolventSolubility>();
    [Tooltip("Solubility in solvents not in the list (units per unit).")]
    [Min(0f)] public float otherSolventSolubility = 0.05f;
    [Tooltip("Solubility doubles every this many °C above 20 °C (and halves below). 0 = the same at any temperature.")]
    public float solubilityDoublingDegrees = 40f;

    [Header("Thermal stability")]
    [Tooltip("Above this temperature (°C) it breaks down (into Degrades Into). 2000 = never.")]
    public float stableMax = 2000f;
    [Tooltip("Below this temperature (°C) it breaks down. -273 = never.")]
    public float stableMin = -273f;
    [Tooltip("How fast it breaks down 10 °C outside its stable range (share per second); faster the further out.")]
    [Min(0f)] public float degradeRate = 0.02f;
    [Tooltip("The breakdown doubles every this many °C further outside the range.")]
    [Min(1f)] public float degradeDoublingDegrees = 10f;
    [Tooltip("What it breaks down into (empty = it is simply destroyed).")]
    public List<ReactionProduct> degradesInto = new List<ReactionProduct>();
    [Tooltip("What the player sees while it breaks down (\"the rose turns a muddy brown\").")]
    public string degradeObservation = "";

    [Header("Density and layers")]
    [Tooltip("Density relative to water (1). The lighter of two layers floats on top (decant it); undissolved matter " +
             "lighter than the liquid floats (skim it), heavier sinks; close to the liquid's density it stays suspended and " +
             "clouds the bottles (filter it).")]
    [Min(0.05f)] public float density = 1f;
    [Tooltip("Solvents: the layer they form (water Aqueous, oil Oily; Auto = mixes with both, like spirit). Other " +
             "substances: Auto = the layer whose solvent holds them best.")]
    public LiquidLayer layer = LiquidLayer.Auto;

    [Header("Acidity")]
    [Tooltip("The pH (0-14) at which it comes out of ingredients best (with pH Sensitivity above 0). The mixture's pH " +
             "comes from its acid and alkali properties.")]
    [Range(0f, 14f)] public float extractionPh = 7f;
    [Tooltip("How much the pH changes how fast it is extracted: ×2 per (2 ÷ this) pH units closer to its best pH than " +
             "neutral is. 0 = the pH does not matter.")]
    [Min(0f)] public float phSensitivity;
    [Tooltip("Below this pH it breaks down (0 = never).")]
    [Range(0f, 14f)] public float stablePhMin;
    [Tooltip("Above this pH it breaks down (14 = never).")]
    [Range(0f, 14f)] public float stablePhMax = 14f;
    [Tooltip("Share per second it loses outside its pH range at 20 °C (×2 per 20 °C hotter). It breaks down into Degrades Into.")]
    [Min(0f)] public float phDegradeRate = 0.02f;
    [Tooltip("What the player sees while acid or alkali destroys it.")]
    public string phDegradeObservation = "";

    [Header("Crystals")]
    [Tooltip("The solid substance it crystallises as when the liquid cannot hold it (empty = it comes out as an undissolved " +
             "powder). Crystals are collected when filtering or emptying, and dissolve again where there is room.")]
    public string crystal = "";
    [Tooltip("Supersaturation (dissolved ÷ what the liquid holds) at which crystals start by themselves. With crystals " +
             "already in the vessel (a seed) they grow as soon as it is saturated; below this, unseeded, it stays supersaturated.")]
    [Min(1f)] public float nucleation = 1.3f;
    [Tooltip("Crystals grow only while the agitation is at most this; stirred, it comes out as a fine powder instead.")]
    [Range(0f, 1f)] public float crystalStillness = 0.15f;
    [Tooltip("Crystals grow only while the liquid cools at most this fast (°C per second); crash-cooled, it comes out as a powder.")]
    [Min(0f)] public float maxCoolingRate = 0.4f;
    [Tooltip("Share of the excess that crystallises per second.")]
    [Min(0f)] public float crystalGrowth = 0.06f;

    [Header("Compatibility")]
    [Tooltip("What it does not get along with: substances that interfere with its reactions, that it clashes with, or " +
             "in whose presence it curdles out of solution.")]
    public List<SubstanceInteraction> interactions = new List<SubstanceInteraction>();

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    /// <summary>Is there a limit to how much of it dissolves?</summary>
    public bool HasSolubilityLimit => phase == SubstancePhase.Liquid && role != SubstanceRole.Solvent && solubility != null && solubility.Count > 0;

    public bool BreaksDownWhenHot => stableMax < 1999f;
    public bool BreaksDownWhenCold => stableMin > -272f;
    public bool BreaksDownInAcid => stablePhMin > 0.01f;
    public bool BreaksDownInAlkali => stablePhMax < 13.99f;
    public bool Crystallises => !string.IsNullOrWhiteSpace(crystal);

    /// <summary>Units of it one unit of <paramref name="solventId"/> holds at 20 °C.</summary>
    public float SolubilityIn(string solventId)
    {
        if (solubility != null)
            for (int i = 0; i < solubility.Count; i++)
                if (solubility[i] != null && string.Equals(solubility[i].solvent, solventId, StringComparison.OrdinalIgnoreCase))
                    return solubility[i].unitsPerUnit;
        return otherSolventSolubility;
    }

    public AlchemySubstance Dissolves(string solventId, float unitsPerUnit)
    {
        solubility.Add(new SolventSolubility(solventId, unitsPerUnit));
        return this;
    }

    /// <summary>Breaks down above <paramref name="max"/> °C into <paramref name="into"/> (substance, ratio pairs).</summary>
    public AlchemySubstance StableUpTo(float max, float rate, string seen, params (string substance, float ratio)[] into)
    {
        stableMax = max;
        degradeRate = rate;
        degradeObservation = seen;
        foreach (var p in into)
            degradesInto.Add(new ReactionProduct(p.substance, p.ratio));
        return this;
    }

    /// <summary>Breaks down below <paramref name="min"/> °C into <paramref name="into"/>.</summary>
    public AlchemySubstance StableDownTo(float min, float rate, string seen, params (string substance, float ratio)[] into)
    {
        stableMin = min;
        degradeRate = rate;
        degradeObservation = seen;
        foreach (var p in into)
            degradesInto.Add(new ReactionProduct(p.substance, p.ratio));
        return this;
    }

    public AlchemySubstance WithDensity(float relative)
    {
        density = relative;
        return this;
    }

    public AlchemySubstance InLayer(LiquidLayer l)
    {
        layer = l;
        return this;
    }

    /// <summary>Extracted best at <paramref name="ph"/>, the more so the higher <paramref name="sensitivity"/>.</summary>
    public AlchemySubstance ExtractsBestAtPh(float ph, float sensitivity)
    {
        extractionPh = ph;
        phSensitivity = sensitivity;
        return this;
    }

    /// <summary>Breaks down below <paramref name="min"/> or above <paramref name="max"/> pH (into Degrades Into).</summary>
    public AlchemySubstance StableBetweenPh(float min, float max, float rate, string seen)
    {
        stablePhMin = min;
        stablePhMax = max;
        phDegradeRate = rate;
        phDegradeObservation = seen;
        return this;
    }

    /// <summary>Crystallises as the solid <paramref name="crystalId"/> when the liquid cannot hold it.</summary>
    public AlchemySubstance CrystallisesAs(string crystalId, float startsAt = 1.3f, float growth = 0.06f, float coolingAtMost = 0.4f)
    {
        crystal = crystalId;
        nucleation = startsAt;
        crystalGrowth = growth;
        maxCoolingRate = coolingAtMost;
        return this;
    }

    public AlchemySubstance Interacts(InteractionKind kind, SubstanceSelector with, float strength, string seen = "", params (string substance, float ratio)[] into)
    {
        var i = new SubstanceInteraction { kind = kind, with = with, strength = strength, observation = seen ?? "" };
        foreach (var p in into)
            i.products.Add(new ReactionProduct(p.substance, p.ratio));
        interactions.Add(i);
        return this;
    }

    /// <summary>The value of one property (0 when it does not have it).</summary>
    public float Property(string propertyId)
    {
        if (properties == null)
            return 0f;
        for (int i = 0; i < properties.Count; i++)
            if (properties[i] != null && string.Equals(properties[i].property, propertyId, StringComparison.OrdinalIgnoreCase))
                return properties[i].value;
        return 0f;
    }

    public AlchemySubstance WithProperty(string propertyId, float value)
    {
        properties.Add(new PropertyValue(propertyId, value));
        return this;
    }
}

/// <summary>
/// Picks substances in a mixture: one substance, every substance with enough of a property, or every substance of a
/// role. Reactions use selectors for what they consume and what speeds them up, outcomes for what they require.
/// </summary>
[Serializable]
public class SubstanceSelector
{
    [Tooltip("Substance: one substance by id. Property: every substance whose property is at least Min Value. Role: every substance of a role.")]
    public SubstanceSelectorMode mode = SubstanceSelectorMode.Substance;
    [Tooltip("The substance id (Substance), the property id (Property).")]
    public string id = "";
    [Tooltip("The role (Role mode).")]
    public SubstanceRole role = SubstanceRole.Active;
    [Tooltip("Property mode: the least value of the property a substance needs to be picked.")]
    public float minValue = 0.5f;
    [Tooltip("Substances never picked (comma separated ids): \"any active compound except null essence\".")]
    public string except = "";

    public SubstanceSelector() { }

    public static SubstanceSelector Of(string substanceId) => new SubstanceSelector { mode = SubstanceSelectorMode.Substance, id = substanceId };

    public static SubstanceSelector WithProperty(string propertyId, float min, string except = "") =>
        new SubstanceSelector { mode = SubstanceSelectorMode.Property, id = propertyId, minValue = min, except = except };

    public static SubstanceSelector OfRole(SubstanceRole r, string except = "") =>
        new SubstanceSelector { mode = SubstanceSelectorMode.Role, role = r, except = except };

    /// <summary>A text that is the same for equal selectors (cache key).</summary>
    public string Key => mode switch
    {
        SubstanceSelectorMode.Substance => "s:" + id,
        SubstanceSelectorMode.Property => "p:" + id + ">=" + minValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "!" + except,
        _ => "r:" + (int)role + "!" + except,
    };

    /// <summary>Is <paramref name="s"/> picked?</summary>
    public bool Matches(AlchemySubstance s)
    {
        if (s == null || IsExcluded(s.id))
            return false;
        switch (mode)
        {
            case SubstanceSelectorMode.Substance: return string.Equals(s.id, id, StringComparison.OrdinalIgnoreCase);
            case SubstanceSelectorMode.Property: return s.Property(id) >= minValue;
            default: return s.role == role;
        }
    }

    private bool IsExcluded(string substanceId)
    {
        if (string.IsNullOrWhiteSpace(except))
            return false;
        foreach (string part in except.Split(','))
            if (string.Equals(part.Trim(), substanceId, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>"Vital Essence", "anything with Fire ≥ 0.5", "any Active compound".</summary>
    public string Describe(AlchemyRules rules)
    {
        string text;
        switch (mode)
        {
            case SubstanceSelectorMode.Substance:
                text = rules != null ? rules.SubstanceName(id) : id;
                break;
            case SubstanceSelectorMode.Property:
                text = $"anything with {(rules != null ? rules.PropertyName(id) : id)} ≥ {minValue:0.##}";
                break;
            default:
                text = $"any {role} compound";
                break;
        }
        if (!string.IsNullOrWhiteSpace(except))
        {
            var names = new List<string>();
            foreach (string part in except.Split(','))
                if (!string.IsNullOrWhiteSpace(part))
                    names.Add(rules != null ? rules.SubstanceName(part.Trim()) : part.Trim());
            text += " except " + string.Join(", ", names);
        }
        return text;
    }
}

/// <summary>Something that happened in a preparation. Ids name what it is about; the UI words it by what the player knows.</summary>
public readonly struct AlchemyEvent
{
    public readonly AlchemyEventKind Kind;
    /// <summary>Preparation time (seconds) it happened at.</summary>
    public readonly double Time;
    /// <summary>The ingredient, substance or reaction id it is about (may be empty).</summary>
    public readonly string Id;
    /// <summary>A plain description that reveals nothing the player does not know (an observation, a count...).</summary>
    public readonly string Text;
    public readonly float Value;

    public AlchemyEvent(AlchemyEventKind kind, double time, string id, string text, float value = 0f)
    {
        Kind = kind;
        Time = time;
        Id = id ?? "";
        Text = text ?? "";
        Value = value;
    }

    public override string ToString() => $"[{AlchemyText.Clock(Time)}] {Kind} {Id} {Text}";
}

/// <summary>Formatting shared by the station window, the journal and the tools.</summary>
public static class AlchemyText
{
    /// <summary>"1:05" (minutes:seconds) for a preparation time.</summary>
    public static string Clock(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds)) seconds = 0;
        int s = (int)Math.Floor(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>"23 °C".</summary>
    public static string Celsius(double t) => $"{Math.Round(t):0} °C";

    /// <summary>"4.2 u".</summary>
    public static string Units(double u) => u >= 10 ? $"{u:0} u" : u >= 0.995 ? $"{u:0.0} u" : $"{u:0.00} u";

    /// <summary>"#RRGGBB" for rich text.</summary>
    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
