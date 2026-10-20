using System;
using System.Collections.Generic;
using UnityEngine;

// The ways to give a character injury resistances (see InjuryResistance): a trait behaviour (traits; races and classes
// through their innate traits), an equipment effect (items, armour sets, races and classes' effect lists) and an ability
// effect (a timed buff such as Stone Skin). The common cases also exist as character stats (Wound Chance, Fracture
// Chance, Slash Damage Taken...) usable in any modifier list.

/// <summary>A trait that makes the character resist injuries: Tough Skin, Strong Bones, Thick Blood, Fast Healer...</summary>
[Serializable, AbilityMenu("Passive/Injury Resistances", "Resists injuries and damage forms: fewer or milder wounds, fractures, burns or infections, " +
                                                          "less bleeding, faster healing, sturdier limbs, less damage from slashes, piercing or blunt hits.", 0)]
public class InjuryResistanceTrait : TraitBehaviour
{
    [Tooltip("The resistances (or weaknesses) the trait gives. Percent changes: negative = better.")]
    public List<InjuryResistance> resistances = new List<InjuryResistance>();

    [NonSerialized] private InjuryResistances target;
    [NonSerialized] private object token;

    public override string Describe()
    {
        var parts = new List<string>();
        if (resistances != null)
            foreach (InjuryResistance r in resistances)
                if (r != null && !r.IsEmpty)
                    parts.Add(r.Describe(Mathf.Max(0.1f, multiplier)));
        return parts.Count > 0 ? string.Join("; ", parts) : "Injury resistances (none set).";
    }

    public override void OnAdded(TraitContext ctx) => TryApply(ctx);

    // The combat entity can appear after the trait (it is made by the ability system): try again until it is there.
    public override void Tick(TraitContext ctx, float dt)
    {
        if (token == null)
            TryApply(ctx);
    }

    private void TryApply(TraitContext ctx)
    {
        if (token != null || ctx == null)
            return;
        CombatEntity e = ctx.Entity;
        Component host = e != null ? (Component)e : ctx.Root;
        target = InjuryResistances.For(host);
        if (target != null)
            token = target.Add(resistances, Mathf.Max(0.1f, multiplier), ctx.Manager != null ? "Trait" : "");
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (target != null && token != null)
            target.Remove(token);
        target = null;
        token = null;
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var c = (InjuryResistanceTrait)base.CreateRuntimeCopy();
        c.target = null;
        c.token = null;
        return c;
    }

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (resistances == null || resistances.Count == 0)
            warnings.Add("Injury Resistances: no resistance configured.");
        else
            foreach (InjuryResistance r in resistances)
                if (r != null && r.IsEmpty)
                    warnings.Add($"Injury Resistances: '{r.label}' changes nothing (every value is 0).");
    }
}

/// <summary>Injury resistances while an item is worn or held (thick leather against scratches, a helmet against concussions).</summary>
[Serializable, AbilityMenu("Stats/Injury Resistances", "Fewer or milder injuries (wounds, fractures, burns, concussions...), less bleeding, sturdier limbs, " +
                                                        "less damage from slashes, piercing or blunt hits while it is worn.", 3)]
public class InjuryResistanceEquipEffect : EquipmentEffect
{
    [Tooltip("The resistances (or weaknesses) given while equipped. Percent changes: negative = better.")]
    public List<InjuryResistance> resistances = new List<InjuryResistance>();

    public override string Describe(float strength = 1f)
    {
        var parts = new List<string>();
        if (resistances != null)
            foreach (InjuryResistance r in resistances)
                if (r != null && !r.IsEmpty)
                    parts.Add(r.Describe(strength));
        return string.Join("; ", parts);
    }

    public override EquipmentEffectHandle Apply(EquipmentContext ctx, float strength = 1f)
    {
        if (resistances == null || resistances.Count == 0 || ctx == null || ctx.Owner == null)
            return null;
        InjuryResistances target = InjuryResistances.For(ctx.Owner);
        if (target == null)
            return null;
        object token = target.Add(resistances, strength, ctx.CurrentSource);
        return new ActionHandle(() => { if (target != null) target.Remove(token); });
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (resistances == null || resistances.Count == 0)
            warnings.Add($"{owner}: no injury resistance configured.");
    }

    public override EquipmentEffect Clone()
    {
        var c = (InjuryResistanceEquipEffect)base.Clone();
        c.resistances = new List<InjuryResistance>();
        if (resistances != null)
            foreach (InjuryResistance r in resistances)
                if (r != null) c.resistances.Add(r.Clone());
        return c;
    }
}

/// <summary>A timed injury resistance from an ability (Stone Skin, Iron Bones, a coagulant potion's spell).</summary>
[Serializable, AbilityMenu("Buff/Injury Resistance", "For a while: fewer or milder injuries, less bleeding, faster healing, less damage from slashes, " +
                                                      "piercing or blunt hits. Casting it again refreshes it.", 2)]
public class InjuryResistanceBuffEffect : AbilityEffect
{
    [Tooltip("Name of the buff (casting it again refreshes the one of the same name).")]
    public string label = "Stone Skin";
    [Tooltip("Seconds it lasts (before the ability's duration modifiers).")]
    [Min(0.1f)] public float duration = 20f;
    [Tooltip("The resistances given while it lasts. Percent changes: negative = better.")]
    public List<InjuryResistance> resistances = new List<InjuryResistance>();

    public InjuryResistanceBuffEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null || resistances == null || resistances.Count == 0)
            return;
        float time = duration * ctx.stats.duration;
        float strength = 1f;
        CombatStats caster = ctx.caster != null ? ctx.caster.Stats : null;
        if (caster != null)
        {
            time *= caster.BuffDurationMultiplier;
            strength *= caster.BuffStrengthMultiplier;
        }
        if (time <= 0.05f)
            return;
        InjuryResistances.For(ctx.target)?.Refresh(resistances, strength, string.IsNullOrEmpty(label) ? "Injury Resistance" : label, time);
    }

    public override string Describe(in AbilityStats s)
    {
        var parts = new List<string>();
        if (resistances != null)
            foreach (InjuryResistance r in resistances)
                if (r != null && !r.IsEmpty)
                    parts.Add(r.Describe());
        return $"{label}: {string.Join("; ", parts)} for {duration * s.duration:0.#}s{Chance(chance)}";
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (resistances == null || resistances.Count == 0)
            warnings.Add($"{owner}: Injury Resistance has no resistance configured.");
    }

    public override AbilityEffect Clone()
    {
        var c = (InjuryResistanceBuffEffect)base.Clone();
        c.resistances = new List<InjuryResistance>();
        if (resistances != null)
            foreach (InjuryResistance r in resistances)
                if (r != null) c.resistances.Add(r.Clone());
        return c;
    }
}
