using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which injuries (or missing limbs) a treatment works on: by injury type, tag, severity and body part.</summary>
[Serializable]
public class TreatmentTarget
{
    [Tooltip("Injury type ids it works on (Fracture). Empty = any type (then Tags decide).")]
    public List<string> injuryTypes = new List<string>();
    [Tooltip("Injury tags it works on (wound, bleeding, fracture, burn, infection). Empty = any tag. A type OR a tag match is enough " +
             "when both are filled.")]
    public List<string> tags = new List<string>();
    [Tooltip("Least severity it works on (index in the Severity Levels).")]
    [Min(0)] public int minSeverity = 0;
    [Tooltip("Worst severity it works on (a splint sets a broken bone, a compound fracture needs surgery).")]
    [Min(0)] public int maxSeverity = 99;
    [Tooltip("Only on these kinds of parts (empty = any).")]
    public List<BodyPartKind> partKinds = new List<BodyPartKind>();
    [Tooltip("Skips injuries that already have this treatment's effect (a bandaged wound for a bandage).")]
    public bool skipAlreadyTreated = true;

    /// <summary>Does it work on <paramref name="injury"/> (on <paramref name="part"/>)?</summary>
    public bool Matches(Injury injury, InjuryPartState part)
    {
        if (injury == null || injury.Type == null)
            return false;
        if (part != null && partKinds != null && partKinds.Count > 0 && !partKinds.Contains(part.Def.kind))
            return false;
        int sev = injury.Severity;
        if (sev < minSeverity || sev > maxSeverity)
            return false;
        bool anyType = injuryTypes == null || injuryTypes.Count == 0;
        bool anyTag = tags == null || tags.Count == 0;
        if (anyType && anyTag)
            return true;
        if (!anyType)
            foreach (string t in injuryTypes)
                if (!string.IsNullOrWhiteSpace(t) && (string.Equals(t.Trim(), injury.Type.id, StringComparison.OrdinalIgnoreCase) ||
                                                      string.Equals(t.Trim(), injury.Type.displayName, StringComparison.OrdinalIgnoreCase)))
                    return true;
        if (!anyTag)
            foreach (string t in tags)
                if (injury.Type.HasTag(t))
                    return true;
        return false;
    }

    /// <summary>Does it work on missing parts of this kind (reattachment)?</summary>
    public bool MatchesPart(InjuryPartState part) =>
        part != null && (partKinds == null || partKinds.Count == 0 || partKinds.Contains(part.Def.kind));

    public string Describe(InjurySettings settings)
    {
        var bits = new List<string>();
        if (injuryTypes != null && injuryTypes.Count > 0)
        {
            var names = new List<string>();
            foreach (string id in injuryTypes)
            {
                InjuryTypeDefinition t = settings != null ? settings.FindType(id) : null;
                names.Add(t != null ? t.Name : id);
            }
            bits.Add(string.Join(", ", names));
        }
        if (tags != null && tags.Count > 0)
            bits.Add("injuries tagged " + string.Join(" / ", tags));
        if (bits.Count == 0)
            bits.Add("any injury");
        string sev = "";
        int levels = settings != null && settings.severityLevels != null ? settings.severityLevels.Count : 0;
        if (levels > 0)
        {
            bool fromMildest = minSeverity <= 0, toWorst = maxSeverity >= levels - 1;
            if (!fromMildest && toWorst)
                sev = $" ({settings.SeverityName(minSeverity)} or worse)";
            else if (fromMildest && !toWorst)
                sev = $" (up to {settings.SeverityName(maxSeverity)})";
            else if (!fromMildest)
                sev = minSeverity == maxSeverity
                    ? $" ({settings.SeverityName(minSeverity)})"
                    : $" ({settings.SeverityName(minSeverity)} to {settings.SeverityName(maxSeverity)})";
        }
        string parts = partKinds != null && partKinds.Count > 0 ? " on " + string.Join(", ", partKinds) : "";
        return string.Join(" or ", bits) + sev + parts;
    }
}

/// <summary>Everything a treatment effect needs about one application.</summary>
public struct TreatmentContext
{
    public InjuryController patient;
    public InjuryPartState part;
    /// <summary>The injury treated (null for a missing limb).</summary>
    public Injury injury;
    public InjuryTreatment treatment;
    public TreatmentSource source;
    /// <summary>Who applies it (the patient for self-treatment, a healer, null for scripts).</summary>
    public CombatEntity healer;
    /// <summary>The medical item used (null for abilities).</summary>
    public ItemSO item;
    /// <summary>Strength (an ability's healing stat; 1 for items).</summary>
    public float potency;
    /// <summary>The severed limb being reattached (reattachment only).</summary>
    public SeveredLimbRecord limb;
}

/// <summary>
/// What a treatment does: stop bleeding, splint, stitch, cure, restore condition, reattach a limb... Add effects to a
/// treatment with the type dropdown; write new ones by deriving from this class.
/// </summary>
[Serializable]
public abstract class TreatmentEffect
{
    public string MenuName => AbilityTypeNames.Nice(GetType());

    /// <summary>Works on missing limbs (reattachment) instead of injuries.</summary>
    public virtual bool ForMissingLimbs => false;

    /// <summary>Would it do anything to this injury now? (a bandage on a bandaged wound does nothing).</summary>
    public virtual bool WouldHelp(in TreatmentContext ctx) => true;

    /// <summary>Can it be applied now? <paramref name="reason"/> says why not.</summary>
    public virtual bool CanApply(in TreatmentContext ctx, out string reason)
    {
        reason = "";
        return true;
    }

    public abstract void Apply(ref TreatmentContext ctx);
    public abstract string Describe();

    public virtual void Validate(string owner, List<string> errors, List<string> warnings) { }
}

[Serializable, AbilityMenu("Injury/Stop Bleeding (Bandage)", "Covers the wound: most of its bleeding stops and it heals faster.", 0)]
public class StopBleedingTreatment : TreatmentEffect
{
    [Tooltip("Share of the bleeding stopped (0.8 = bleeds 20% as much; 1 = stops it).")]
    [Range(0f, 1f)] public float effectiveness = 0.85f;
    [Tooltip("Healing speed while bandaged (x the stage's speed).")]
    [Min(0f)] public float healRateMultiplier = 1.5f;
    [Tooltip("Sterile: the wound stops getting worse (no infection).")]
    public bool sterile = false;

    public override bool WouldHelp(in TreatmentContext ctx) =>
        ctx.injury != null && (!ctx.injury.Bandaged || ctx.injury.BandageEffectiveness < effectiveness - 0.01f || sterile && !ctx.injury.WorseningStopped);

    public override void Apply(ref TreatmentContext ctx)
    {
        Injury i = ctx.injury;
        if (i == null)
            return;
        i.Bandaged = true;
        i.BandageEffectiveness = Mathf.Max(i.BandageEffectiveness, Mathf.Clamp01(effectiveness * Mathf.Max(0.1f, ctx.potency)));
        i.TreatedHealMultiplier = Mathf.Max(i.TreatedHealMultiplier, healRateMultiplier);
        if (sterile)
            i.WorseningStopped = true;
        if (string.IsNullOrEmpty(i.TreatmentLabel))
            i.TreatmentLabel = sterile ? "Sterile bandage" : "Bandaged";
    }

    public override string Describe() =>
        $"stops {effectiveness * 100f:0}% of the bleeding, heals x{healRateMultiplier:0.#}" + (sterile ? ", sterile (no infection)" : "");
}

[Serializable, AbilityMenu("Injury/Treat (Splint, Stitch, Disinfect)", "Treats the injury: it may heal (splinted bone, stitched wound), heals faster, impairs less and stops getting worse.", 1)]
public class TreatInjuryTreatment : TreatmentEffect
{
    [Tooltip("Shown on the injury (\"Splinted\", \"Stitched\").")]
    public string label = "Treated";
    [Tooltip("Lets injuries that need treatment heal (broken bones, deep wounds).")]
    public bool allowsHealing = true;
    [Tooltip("Healing speed once treated (x the stage's speed).")]
    [Min(0f)] public float healRateMultiplier = 1.5f;
    [Tooltip("Share of the injury's impairment removed while treated (a splinted leg limps less).")]
    [Range(0f, 1f)] public float impairmentReduction = 0.4f;
    [Tooltip("The injury stops getting worse.")]
    public bool stopsWorsening = true;
    [Tooltip("Share of the bleeding stopped as well (stitches close a wound: 1).")]
    [Range(0f, 1f)] public float stopsBleeding = 0f;

    public override bool WouldHelp(in TreatmentContext ctx) =>
        ctx.injury != null && (!ctx.injury.Treated || !string.Equals(ctx.injury.TreatmentLabel, label, StringComparison.OrdinalIgnoreCase));

    public override void Apply(ref TreatmentContext ctx)
    {
        Injury i = ctx.injury;
        if (i == null)
            return;
        i.Treated = true;
        i.TreatmentLabel = string.IsNullOrWhiteSpace(label) ? "Treated" : label;
        if (allowsHealing)
            i.HealingAllowed = true;
        i.TreatedHealMultiplier = Mathf.Max(i.TreatedHealMultiplier, healRateMultiplier * Mathf.Max(0.1f, ctx.potency));
        i.ImpairmentReduction = Mathf.Max(i.ImpairmentReduction, impairmentReduction);
        if (stopsWorsening)
            i.WorseningStopped = true;
        if (stopsBleeding > 0f)
        {
            i.Bandaged = true;
            i.BandageEffectiveness = Mathf.Max(i.BandageEffectiveness, stopsBleeding);
        }
    }

    public override string Describe()
    {
        var bits = new List<string> { $"{label.ToLowerInvariant()}" };
        if (allowsHealing) bits.Add("lets it heal");
        if (healRateMultiplier != 1f) bits.Add($"heals x{healRateMultiplier:0.#}");
        if (impairmentReduction > 0f) bits.Add($"{impairmentReduction * 100f:0}% less impairment");
        if (stopsWorsening) bits.Add("stops it getting worse");
        if (stopsBleeding > 0f) bits.Add($"stops {stopsBleeding * 100f:0}% of the bleeding");
        return string.Join(", ", bits);
    }
}

[Serializable, AbilityMenu("Injury/Cure", "Removes the injury at once (magic, strong medicine), or lowers its severity.", 2)]
public class CureInjuryTreatment : TreatmentEffect
{
    [Tooltip("Removes the injury completely. Off = lowers its severity by Stages Down.")]
    public bool removeCompletely = true;
    [Tooltip("Stages lowered when not removed (a stage-0 injury is removed).")]
    [Min(1)] public int stagesDown = 1;

    public override void Apply(ref TreatmentContext ctx)
    {
        if (ctx.injury == null || ctx.patient == null)
            return;
        if (removeCompletely || ctx.injury.StageIndex - stagesDown < 0)
            ctx.patient.RemoveInjury(ctx.injury);
        else
            ctx.patient.ChangeStage(ctx.injury, ctx.injury.StageIndex - stagesDown);
    }

    public override string Describe() => removeCompletely ? "cures it" : $"lowers it {stagesDown} stage{(stagesDown == 1 ? "" : "s")}";
}

[Serializable, AbilityMenu("Injury/Restore Condition", "Gives the body part condition back.", 3)]
public class RestoreConditionTreatment : TreatmentEffect
{
    [Tooltip("Condition restored (points, or percent of the part's max).")]
    [Min(0f)] public float amount = 25f;
    public bool percent = true;

    public override bool WouldHelp(in TreatmentContext ctx) => ctx.part != null && ctx.part.Condition < ctx.part.MaxCondition - 0.01f;

    public override void Apply(ref TreatmentContext ctx)
    {
        if (ctx.part == null || ctx.patient == null)
            return;
        float a = (percent ? ctx.part.MaxCondition * amount / 100f : amount) * Mathf.Max(0.1f, ctx.potency);
        ctx.patient.RestoreCondition(ctx.part, a);
    }

    public override string Describe() => percent ? $"+{amount:0}% part condition" : $"+{amount:0} part condition";
}

[Serializable, AbilityMenu("Injury/Reattach Limb", "Puts a lost limb back on (it must be the character's own limb, carried, and in time unless the time limit is ignored).", 4)]
public class ReattachLimbTreatment : TreatmentEffect
{
    [Tooltip("Works after the limb's time limit ran out (special abilities, city surgeons). Off = only in time.")]
    public bool ignoreTimeLimit = false;
    [Tooltip("Works on limbs in any condition (ignores the Reattachment rules' Min Limb Condition).")]
    public bool ignoreLimbCondition = false;
    [Tooltip("Regrows the limb without needing it (powerful magic). Off = the lost limb must be carried (or near).")]
    public bool regrowWithoutLimb = false;
    [Tooltip("Condition (fraction) the part gets back. Negative = the Reattachment rules' Restored Condition x the limb's condition.")]
    [Range(-1f, 1f)] public float restoredCondition = -1f;
    [Tooltip("Skips the injury added after a reattachment (the part works fully at once).")]
    public bool skipAfterInjury = false;

    public override bool ForMissingLimbs => true;

    public override bool CanApply(in TreatmentContext ctx, out string reason)
    {
        reason = "";
        if (ctx.part == null || !ctx.part.Lost)
        {
            reason = "Nothing to reattach.";
            return false;
        }
        if (ctx.part.LostWith != null)
        {
            reason = $"It came off with the {ctx.part.LostWith.Name}: reattach that.";
            return false;
        }
        if (regrowWithoutLimb)
            return true;
        if (ctx.limb == null)
        {
            reason = ctx.patient != null && ctx.patient.Settings.reattachment.limbMustBeCarried
                ? $"You need the lost {ctx.part.Name} in your inventory."
                : $"You need the lost {ctx.part.Name} (in your inventory or near you).";
            return false;
        }
        if (ctx.limb.Expired && !ignoreTimeLimit)
        {
            reason = "Too late: the limb's time limit ran out (only special abilities can reattach it now).";
            return false;
        }
        float min = ctx.patient != null ? ctx.patient.Settings.reattachment.minLimbCondition : 0f;
        if (!ignoreLimbCondition && ctx.limb.Condition < min)
        {
            reason = $"The limb is too damaged ({ctx.limb.Condition * 100f:0}%, needs {min * 100f:0}%).";
            return false;
        }
        return true;
    }

    public override void Apply(ref TreatmentContext ctx)
    {
        if (ctx.patient != null)
            ctx.patient.ReattachLimb(ctx.part, ctx.limb, this, ctx.source);
    }

    public override string Describe()
    {
        var bits = new List<string> { regrowWithoutLimb ? "regrows a lost limb" : "reattaches a lost limb" };
        if (ignoreTimeLimit) bits.Add("even after its time limit");
        if (ignoreLimbCondition) bits.Add("in any condition");
        return string.Join(", ", bits);
    }
}

/// <summary>
/// A way to treat injuries: what it works on (injury types, tags, severities, body parts), how long it takes, and its
/// effects. Medical items (<see cref="MedicalItemSO"/>) and ability effects (<see cref="TreatInjuriesEffect"/>) hold
/// treatments; scripts can apply them with <see cref="InjuryController.ApplyTreatment"/>.
/// </summary>
[Serializable]
public class InjuryTreatment
{
    [Tooltip("Name of the action in the health panel (\"Bandage\", \"Splint\").")]
    public string name = "Treat";
    [Tooltip("What it works on.")]
    public TreatmentTarget target = new TreatmentTarget();
    [Tooltip("Seconds to apply it (the injury panel shows the progress; taking damage may interrupt it). 0 = at once.")]
    [Min(0f)] public float applySeconds = 2f;
    [Tooltip("What it does. Pick with the dropdown; write new ones by deriving from TreatmentEffect.")]
    [SerializeReference, SubclassSelector] public List<TreatmentEffect> effects = new List<TreatmentEffect>();

    /// <summary>It works on missing limbs (it has a reattachment effect).</summary>
    public bool ForMissingLimbs
    {
        get
        {
            if (effects == null)
                return false;
            for (int i = 0; i < effects.Count; i++)
                if (effects[i] != null && effects[i].ForMissingLimbs)
                    return true;
            return false;
        }
    }

    /// <summary>Does it work on this injury (target filters and at least one effect that would help)?</summary>
    public bool Matches(in TreatmentContext ctx)
    {
        if (effects == null || effects.Count == 0)
            return false;
        if (ctx.part != null && ctx.part.Lost && ctx.injury == null)
            return ForMissingLimbs && target.MatchesPart(ctx.part); // the missing limb itself (its stump is an injury)
        if (ctx.injury == null || !target.Matches(ctx.injury, ctx.part))
            return false;
        if (!target.skipAlreadyTreated)
            return true;
        for (int i = 0; i < effects.Count; i++)
            if (effects[i] != null && !effects[i].ForMissingLimbs && effects[i].WouldHelp(ctx))
                return true;
        return false;
    }

    /// <summary>Can every effect be applied now?</summary>
    public bool CanApply(in TreatmentContext ctx, out string reason)
    {
        reason = "";
        if (effects == null)
            return false;
        for (int i = 0; i < effects.Count; i++)
            if (effects[i] != null && !effects[i].CanApply(ctx, out reason))
                return false;
        return true;
    }

    public void ApplyEffects(ref TreatmentContext ctx)
    {
        if (effects == null)
            return;
        for (int i = 0; i < effects.Count; i++)
        {
            TreatmentEffect e = effects[i];
            if (e == null)
                continue;
            try { e.Apply(ref ctx); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    /// <summary>"Bandage: stops 85% of the bleeding... (wound or bleeding injuries, 2 s)".</summary>
    public string Describe(InjurySettings settings)
    {
        var bits = new List<string>();
        if (effects != null)
            foreach (TreatmentEffect e in effects)
                if (e != null) bits.Add(e.Describe());
        string what = ForMissingLimbs ? "missing limbs" : target.Describe(settings);
        string time = applySeconds > 0f ? $", {applySeconds:0.#} s" : "";
        return $"{name}: {(bits.Count > 0 ? string.Join("; ", bits) : "does nothing")} ({what}{time})";
    }

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (effects == null || effects.Count == 0)
            warnings.Add($"{owner}: treatment '{name}' has no effects.");
        else
            foreach (TreatmentEffect e in effects)
                e?.Validate(owner, errors, warnings);
        if (target != null && target.minSeverity > target.maxSeverity)
            errors.Add($"{owner}: treatment '{name}' has a Min Severity above its Max Severity (it never matches).");
    }

    public InjuryTreatment Clone()
    {
        var c = (InjuryTreatment)MemberwiseClone();
        c.effects = new List<TreatmentEffect>(effects ?? new List<TreatmentEffect>());
        return c;
    }
}
