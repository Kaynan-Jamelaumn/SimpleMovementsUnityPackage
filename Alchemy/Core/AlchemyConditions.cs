using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What conditions and outcomes can read of a mixture: its temperature, stirring, pressure, volume and time, the
/// equipment's tags, any named variable (the station's surroundings: night, ambient temperature, humidity... or your
/// own <see cref="IAlchemyConditionSource"/>), and its composition (concentrations, properties, roles).
/// </summary>
public interface IAlchemyState
{
    AlchemyRules Rules { get; }
    double Temperature { get; }
    double Agitation { get; }
    double Pressure { get; }
    /// <summary>Every substance in the vessel (liquids and settled solids), in units.</summary>
    double Volume { get; }
    /// <summary>The liquid part (what would be bottled).</summary>
    double LiquidVolume { get; }
    /// <summary>Seconds of preparation so far.</summary>
    double Elapsed { get; }
    /// <summary>A named variable: built-in ones (temperature, agitation, pressure, volume, liquid, time, heat, ambient),
    /// the equipment's own, and the surroundings'. 0 when unknown.</summary>
    double Variable(string name);
    /// <summary>Does the vessel have this tag (copper, glass, sealed...)?</summary>
    bool HasTag(string tag);
    /// <summary>Units of substance <paramref name="index"/>.</summary>
    double Amount(int index);
    /// <summary>Units of everything <paramref name="selector"/> picks.</summary>
    double Amount(SubstanceSelector selector);
    /// <summary>Share of the liquid (0-1) taken by what <paramref name="selector"/> picks.</summary>
    double Concentration(SubstanceSelector selector);
    /// <summary>The mixture's value of a property: the concentration-weighted sum over the liquid.</summary>
    double PropertyValue(string propertyId);
    /// <summary>Share (0-1) of a role among the liquid that is not solvent (<paramref name="ofNonSolvent"/>) or among all of it.</summary>
    double RoleShare(SubstanceRole role, bool ofNonSolvent);
    /// <summary>Index of the most abundant substance of <paramref name="role"/> in the liquid (-1 = none).</summary>
    int Dominant(SubstanceRole role);
}

/// <summary>
/// A rule about the state of a mixture. Reactions need all of theirs to run (or get a rate bonus from them), and
/// outcomes need all of theirs to be what a preparation becomes. Pick one from the dropdown, or write your own: derive
/// from it, mark it [Serializable] and it appears in the list (new crafting conditions need no change to the core).
/// </summary>
[Serializable]
public abstract class AlchemyCondition
{
    /// <summary>Does the mixture meet it?</summary>
    public abstract bool IsMet(IAlchemyState state);

    /// <summary>"between 55 and 130 °C" (the journal shows understood reactions with their conditions).</summary>
    public abstract string Describe(AlchemyRules rules);

    /// <summary>Configuration problems (the database inspector and the Project Validator list them).</summary>
    public virtual void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings) { }

    /// <summary>A fresh copy (defaults and tests build conditions from code).</summary>
    public AlchemyCondition Clone() => (AlchemyCondition)MemberwiseClone();

    public static bool AllMet(List<AlchemyCondition> conditions, IAlchemyState state)
    {
        if (conditions == null)
            return true;
        for (int i = 0; i < conditions.Count; i++)
            if (conditions[i] != null && !conditions[i].IsMet(state))
                return false;
        return true;
    }

    public static string DescribeAll(List<AlchemyCondition> conditions, AlchemyRules rules, string separator = ", ")
    {
        if (conditions == null || conditions.Count == 0)
            return "";
        var parts = new List<string>();
        foreach (AlchemyCondition c in conditions)
            if (c != null)
                parts.Add(c.Describe(rules));
        return string.Join(separator, parts);
    }

    protected static string Range(double min, double max, string unit, double lowest, double highest)
    {
        bool hasMin = min > lowest, hasMax = max < highest;
        if (hasMin && hasMax) return $"{min:0.##}–{max:0.##}{unit}";
        if (hasMin) return $"≥ {min:0.##}{unit}";
        if (hasMax) return $"≤ {max:0.##}{unit}";
        return "any";
    }
}

/// <summary>A named variable inside a range: temperature, agitation (stirring), pressure, volume, time, or the surroundings.</summary>
[Serializable, AbilityMenu("Conditions/Variable Range", "A built-in or external variable inside a range (temperature, agitation, pressure, time, night...).", 0)]
public class VariableCondition : AlchemyCondition
{
    [Tooltip("temperature, agitation, pressure, volume, liquid, time, heat, ambient, ph, fire, fuel, turbulence, aeration, " +
             "emulsion, cooling, swing, bath, layered — or a surroundings variable (night, humidity, rain...) or one of the equipment's.")]
    public string variable = "temperature";
    public float min = -1000f;
    public float max = 100000f;

    public VariableCondition() { }

    public VariableCondition(string variable, float min, float max)
    {
        this.variable = variable;
        this.min = min;
        this.max = max;
    }

    public override bool IsMet(IAlchemyState state)
    {
        double v = state.Variable(variable);
        return v >= min && v <= max;
    }

    public override string Describe(AlchemyRules rules)
    {
        string v = string.IsNullOrEmpty(variable) ? "?" : variable;
        switch (v.ToLowerInvariant())
        {
            case AlchemyVariables.Temperature: return "at " + Range(min, max, " °C", -999, 99999);
            case AlchemyVariables.Agitation:
                if (max < 0.2f && min <= 0f) return "left perfectly still";
                return "stirred " + Range(min, max, "", 0, 1);
            case AlchemyVariables.Time: return "after " + Range(min, max, " s", 0, 99999);
            case AlchemyVariables.Night: return min >= 0.5f ? "at night" : max < 0.5f ? "by day" : "any time";
            case AlchemyVariables.Ph:
                if (max <= 7f && min <= 0f) return $"in acid (pH ≤ {max:0.#})";
                if (min >= 7f && max >= 14f) return $"in alkali (pH ≥ {min:0.#})";
                return "at pH " + Range(min, max, "", 0, 14);
            case AlchemyVariables.Aeration: return max < 1f && min <= 0f ? $"aeration ≤ {max:0.##}" : $"stirred hard enough to beat in air (aeration ≥ {min:0.##})";
            case AlchemyVariables.Swing: return max < 100f ? $"at a steady temperature (swinging ≤ {max:0.#} °C)" : $"swing {Range(min, max, " °C", 0, 99)}";
            case AlchemyVariables.Heat:
                if (min > 0f && max >= 1f) return min >= 0.75f ? $"over a roaring fire (heat ≥ {min:0.##})" : $"over a fire (heat ≥ {min:0.##})";
                return "heat " + Range(min, max, "", -1, 1);
            case AlchemyVariables.Liquid:
                if (min <= 0f && max < 1f) return $"(almost) no liquid in the vessel (≤ {max:0.##} u)";
                return "liquid " + Range(min, max, " u", 0, 99999);
            case AlchemyVariables.Bath: // 0 none, 1 water, 2 ice
                if (min > 1f) return "in an ice bath";
                if (max < 1f) return "not in a bath";
                if (min > 0f) return max < 2f ? "in a water bath" : "in a bath";
                return max < 2f ? "not in an ice bath" : "any bath";
            default: return $"{v} {Range(min, max, "", -999, 99999)}";
        }
    }
}

/// <summary>The vessel has (or must not have) a tag: copper, glass, sealed...</summary>
[Serializable, AbilityMenu("Conditions/Equipment Tag", "The vessel has (or lacks) a tag such as copper, glass or sealed.", 1)]
public class EquipmentTagCondition : AlchemyCondition
{
    public string tag = "copper";
    [Tooltip("On: the vessel must NOT have the tag.")]
    public bool forbidden;

    public EquipmentTagCondition() { }

    public EquipmentTagCondition(string tag, bool forbidden = false)
    {
        this.tag = tag;
        this.forbidden = forbidden;
    }

    public override bool IsMet(IAlchemyState state) => state.HasTag(tag) != forbidden;

    public override string Describe(AlchemyRules rules)
    {
        string a = !string.IsNullOrEmpty(tag) && "aeiouAEIOU".IndexOf(tag[0]) >= 0 ? "an" : "a";
        return forbidden ? $"not in {a} {tag} vessel" : $"in {a} {tag} vessel";
    }
}

/// <summary>How much of something is in the liquid (share 0-1, or units).</summary>
[Serializable, AbilityMenu("Conditions/Substance Amount", "How much of a substance (or of anything with a property or role) is in the mixture.", 2)]
public class SubstanceCondition : AlchemyCondition
{
    public SubstanceSelector substance = new SubstanceSelector();
    [Tooltip("Share of the liquid (0-1) instead of units.")]
    public bool asConcentration = true;
    [Min(0f)] public float min = 0.05f;
    [Min(0f)] public float max = 1000f;

    public SubstanceCondition() { }

    public SubstanceCondition(SubstanceSelector substance, float min, float max = 1000f, bool asConcentration = true)
    {
        this.substance = substance;
        this.min = min;
        this.max = max;
        this.asConcentration = asConcentration;
    }

    public override bool IsMet(IAlchemyState state)
    {
        double v = asConcentration ? state.Concentration(substance) : state.Amount(substance);
        return v >= min && v <= max;
    }

    public override string Describe(AlchemyRules rules)
    {
        string what = substance != null ? substance.Describe(rules) : "?";
        if (asConcentration)
        {
            if (max >= 1f && min > 0f) return $"{what} at least {min * 100f:0.#}% of the liquid";
            if (min <= 0f) return $"{what} at most {max * 100f:0.#}% of the liquid";
            return $"{what} {min * 100f:0.#}–{max * 100f:0.#}% of the liquid";
        }
        return $"{Range(min, max, " u", 0, 999)} of {what}";
    }

    public override void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings) =>
        AlchemyValidation.CheckSelector(rules, substance, where, errors, warnings);
}

/// <summary>The mixture's value of a property (concentration-weighted) inside a range: "toxicity below 0.05".</summary>
[Serializable, AbilityMenu("Conditions/Property Range", "The mixture's overall value of a property (fire, toxicity...) inside a range.", 3)]
public class PropertyCondition : AlchemyCondition
{
    public string property = "toxicity";
    public float min = -1000f;
    public float max = 1000f;

    public PropertyCondition() { }

    public PropertyCondition(string property, float min, float max)
    {
        this.property = property;
        this.min = min;
        this.max = max;
    }

    public override bool IsMet(IAlchemyState state)
    {
        double v = state.PropertyValue(property);
        return v >= min && v <= max;
    }

    public override string Describe(AlchemyRules rules) =>
        $"{(rules != null ? rules.PropertyName(property) : property)} {Range(min, max, "", -999, 999)}";

    public override void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings)
    {
        if (rules != null && rules.PropertyIndex(property) < 0)
            warnings.Add($"{where}: unknown property '{property}'.");
    }
}

/// <summary>A substance is the most abundant of its role: "the main active compound is Vital Essence".</summary>
[Serializable, AbilityMenu("Conditions/Dominant Substance", "A substance is the most abundant of its role (the main active compound...).", 4)]
public class DominantCondition : AlchemyCondition
{
    public string substance = "";
    [Tooltip("Among which substances it must be the most abundant.")]
    public SubstanceRole among = SubstanceRole.Active;

    public DominantCondition() { }

    public DominantCondition(string substance, SubstanceRole among = SubstanceRole.Active)
    {
        this.substance = substance;
        this.among = among;
    }

    public override bool IsMet(IAlchemyState state)
    {
        int d = state.Dominant(among);
        return d >= 0 && string.Equals(state.Rules.Substances[d].id, substance, StringComparison.OrdinalIgnoreCase);
    }

    public override string Describe(AlchemyRules rules) =>
        $"{(rules != null ? rules.SubstanceName(substance) : substance)} is the main {among.ToString().ToLowerInvariant()} compound";

    public override void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings)
    {
        if (rules != null && rules.IndexOf(substance) < 0)
            errors.Add($"{where}: unknown substance '{substance}'.");
    }
}

/// <summary>A role's share of the mixture: "mostly raw matter" (incomplete), "mostly residue" (degraded).</summary>
[Serializable, AbilityMenu("Conditions/Role Share", "How much of the mixture is of a role (raw matter, residue...).", 5)]
public class RoleShareCondition : AlchemyCondition
{
    public SubstanceRole role = SubstanceRole.Raw;
    [Tooltip("Share of what is not solvent (on), or of all the liquid (off).")]
    public bool ofNonSolvent = true;
    [Range(0f, 1f)] public float min = 0.5f;
    [Range(0f, 1f)] public float max = 1f;

    public RoleShareCondition() { }

    public RoleShareCondition(SubstanceRole role, float min, float max = 1f, bool ofNonSolvent = true)
    {
        this.role = role;
        this.min = min;
        this.max = max;
        this.ofNonSolvent = ofNonSolvent;
    }

    public override bool IsMet(IAlchemyState state)
    {
        double v = state.RoleShare(role, ofNonSolvent);
        return v >= min && v <= max;
    }

    public override string Describe(AlchemyRules rules) =>
        $"{role} matter {Range(min * 100f, max * 100f, "%", 0, 100)} of the {(ofNonSolvent ? "dissolved matter" : "liquid")}";
}

/// <summary>Met when at least one of its conditions is.</summary>
[Serializable, AbilityMenu("Conditions/Any Of", "Met when at least one of the listed conditions is.", 11)]
public class AnyOfCondition : AlchemyCondition
{
    [SerializeReference, SubclassSelector] public List<AlchemyCondition> conditions = new List<AlchemyCondition>();

    public AnyOfCondition() { }

    public AnyOfCondition(params AlchemyCondition[] any) => conditions.AddRange(any);

    public override bool IsMet(IAlchemyState state)
    {
        if (conditions == null || conditions.Count == 0)
            return true;
        foreach (AlchemyCondition c in conditions)
            if (c != null && c.IsMet(state))
                return true;
        return false;
    }

    public override string Describe(AlchemyRules rules) => "(" + DescribeAll(conditions, rules, " or ") + ")";

    public override void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings)
    {
        foreach (AlchemyCondition c in conditions)
            c?.Validate(rules, where, errors, warnings);
    }
}

/// <summary>Met when all of its conditions are (to group conditions inside an Any Of).</summary>
[Serializable, AbilityMenu("Conditions/All Of", "Met when every listed condition is (useful inside Any Of).", 10)]
public class AllOfCondition : AlchemyCondition
{
    [SerializeReference, SubclassSelector] public List<AlchemyCondition> conditions = new List<AlchemyCondition>();

    public AllOfCondition() { }

    public AllOfCondition(params AlchemyCondition[] all) => conditions.AddRange(all);

    public override bool IsMet(IAlchemyState state) => AllMet(conditions, state);

    public override string Describe(AlchemyRules rules) => "(" + DescribeAll(conditions, rules, " and ") + ")";

    public override void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings)
    {
        foreach (AlchemyCondition c in conditions)
            c?.Validate(rules, where, errors, warnings);
    }
}

/// <summary>Met when its condition is not.</summary>
[Serializable, AbilityMenu("Conditions/Not", "Met when the inner condition is not.", 12)]
public class NotCondition : AlchemyCondition
{
    [SerializeReference, SubclassSelector] public AlchemyCondition condition;

    public NotCondition() { }

    public NotCondition(AlchemyCondition condition) => this.condition = condition;

    public override bool IsMet(IAlchemyState state) => condition == null || !condition.IsMet(state);

    public override string Describe(AlchemyRules rules) => condition != null ? "not " + condition.Describe(rules) : "always";

    public override void Validate(AlchemyRules rules, string where, List<string> errors, List<string> warnings) =>
        condition?.Validate(rules, where, errors, warnings);
}

/// <summary>Names of the built-in condition variables.</summary>
public static class AlchemyVariables
{
    public const string Temperature = "temperature";
    public const string Agitation = "agitation";
    public const string Pressure = "pressure";
    public const string Volume = "volume";
    public const string Liquid = "liquid";
    public const string Time = "time";
    public const string Heat = "heat";
    public const string Ambient = "ambient";
    /// <summary>The liquid's pH, 0-14 (7 neutral), from its acid and alkali properties.</summary>
    public const string Ph = "ph";
    /// <summary>A manual fire's flames 0-1 (the heat setting otherwise).</summary>
    public const string Fire = "fire";
    /// <summary>Fuel left on a manual fire (units).</summary>
    public const string Fuel = "fuel";
    /// <summary>How unevenly it is being stirred (0 = steady).</summary>
    public const string Turbulence = "turbulence";
    /// <summary>Air beaten into the liquid by hard stirring, 0-1.</summary>
    public const string Aeration = "aeration";
    /// <summary>How mixed two liquid layers are, 0 (apart) - 1 (emulsified).</summary>
    public const string Emulsion = "emulsion";
    /// <summary>How fast it is cooling (°C per second; negative while heating).</summary>
    public const string Cooling = "cooling";
    /// <summary>How far the temperature strays from its recent average (°C): 0 = held steady.</summary>
    public const string Swing = "swing";
    /// <summary>What the vessel stands in: 0 nothing, 1 a water bath, 2 an ice bath.</summary>
    public const string Bath = "bath";
    /// <summary>1 while the liquid forms two layers.</summary>
    public const string Layered = "layered";
    /// <summary>1 at night, 0 by day (set by <see cref="AlchemySurroundings"/> from the world clock).</summary>
    public const string Night = "night";
    /// <summary>Air humidity 0-1 (from the weather).</summary>
    public const string Humidity = "humidity";
    /// <summary>Rain strength 0-1 (from the weather).</summary>
    public const string Rain = "rain";
}

/// <summary>Checks shared by the conditions, reactions and outcomes.</summary>
public static class AlchemyValidation
{
    public static void CheckSelector(AlchemyRules rules, SubstanceSelector s, string where, List<string> errors, List<string> warnings)
    {
        if (s == null)
        {
            errors.Add($"{where}: empty selector.");
            return;
        }
        if (rules == null)
            return;
        if (s.mode == SubstanceSelectorMode.Substance && rules.IndexOf(s.id) < 0)
            errors.Add($"{where}: unknown substance '{s.id}'.");
        else if (s.mode == SubstanceSelectorMode.Property && rules.PropertyIndex(s.id) < 0)
            warnings.Add($"{where}: unknown property '{s.id}'.");
        else if (rules.Match(s).Length == 0)
            warnings.Add($"{where}: '{s.Describe(rules)}' picks no substance.");
    }
}
