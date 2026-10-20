using System;
using System.Collections.Generic;
using UnityEngine;

// Ability effects of the injury system. They appear in an ability's effect dropdown (Injury ▸ ...), so skills, traits,
// racial abilities, items with abilities and mob attacks can heal injuries, reattach limbs, or cause injuries.

/// <summary>Which injuries an ability treats first.</summary>
public enum InjuryPick
{
    /// <summary>Bleeding injuries first, then the most severe.</summary>
    MostUrgent,
    /// <summary>The most severe first.</summary>
    MostSevere,
    /// <summary>Every matching injury (up to Max Injuries).</summary>
    All,
}

[Serializable, AbilityMenu("Injury/Treat Injuries", "Applies an injury treatment to the target: close wounds, mend bones, cure infections, or reattach a lost limb (optionally even after its time limit, or regrowing it).", 0)]
public class TreatInjuriesEffect : AbilityEffect
{
    [Tooltip("What the ability does to injuries (or missing limbs, with a Reattach Limb effect). Apply Seconds is ignored: the " +
             "ability's own cast time counts.")]
    public InjuryTreatment treatment = new InjuryTreatment
    {
        name = "Mend",
        applySeconds = 0f,
        effects = new List<TreatmentEffect> { new TreatInjuryTreatment { label = "Mended", healRateMultiplier = 3f, impairmentReduction = 0.5f, stopsBleeding = 1f } },
    };
    [Tooltip("Which injuries first.")]
    public InjuryPick pick = InjuryPick.MostUrgent;
    [Tooltip("Most injuries (or limbs) treated per hit.")]
    [Min(1)] public int maxInjuries = 1;

    public TreatInjuriesEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public override void Apply(ref EffectContext ctx)
    {
        InjuryController c = ctx.target != null ? InjuryController.For(ctx.target) : null;
        if (c == null || !c.RulesHere || treatment == null)
            return;
        float potency = Mathf.Max(0.1f, ctx.stats.heal * ctx.multiplier);
        var targets = new List<(InjuryPartState part, Injury injury, float score)>();
        foreach (InjuryPartState part in c.Parts)
        {
            if (part.Lost && part.LostWith == null && treatment.ForMissingLimbs && c.CanTreat(treatment, part, null, TreatmentSource.Ability, null, out _))
                targets.Add((part, null, 1000f));
            foreach (Injury i in part.Injuries) // a lost part's injury is its stump
            {
                if (!c.CanTreat(treatment, part, i, TreatmentSource.Ability, null, out _))
                    continue;
                float score = pick == InjuryPick.MostSevere ? i.Severity * 10f + i.BleedingPerSecond : i.BleedingPerSecond * 100f + i.Severity * 10f;
                targets.Add((part, i, score));
            }
        }
        targets.Sort((a, b) => b.score.CompareTo(a.score));
        int n = pick == InjuryPick.All ? targets.Count : Mathf.Min(targets.Count, Mathf.Max(1, maxInjuries));
        for (int k = 0; k < n && k < targets.Count; k++)
            c.ApplyTreatment(treatment, targets[k].part, targets[k].injury, TreatmentSource.Ability, ctx.caster, potency);
    }

    public override string Describe(in AbilityStats s)
    {
        string count = pick == InjuryPick.All ? "every matching injury" : maxInjuries == 1 ? "1 injury" : $"{maxInjuries} injuries";
        return (treatment != null ? treatment.Describe(InjurySettings.Instance) : "Treats injuries") + $" ({count})" + Chance(chance);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (treatment == null)
            errors.Add($"{owner}: Treat Injuries has no treatment.");
        else
            treatment.Validate(owner, errors, warnings);
    }

    public override AbilityEffect Clone()
    {
        var c = (TreatInjuriesEffect)base.Clone();
        c.treatment = treatment != null ? treatment.Clone() : null;
        return c;
    }
}

[Serializable, AbilityMenu("Injury/Inflict Injury", "Causes an injury on the target (a bleeding bite, a bone-crushing slam) and may cut a limb off, on top of the injuries hits cause by themselves.", 1)]
public class InflictInjuryEffect : AbilityEffect
{
    [Tooltip("Injury type id (Laceration, Fracture...).")]
    public string injuryType = InjuryDefaults.Laceration;
    [Tooltip("Stage of the injury (0 = mildest).")]
    [Min(0)] public int stage = 0;
    [Tooltip("Body part id (LeftLeg). Empty = a random part of the kinds below (or any part).")]
    public string part = "";
    [Tooltip("With no Part: only parts of these kinds (a wolf bites legs). Empty = any.")]
    public List<BodyPartKind> partKinds = new List<BodyPartKind>();
    [Tooltip("Chance (0-1) to cut the chosen part off instead (parts that can be lost).")]
    [Range(0f, 1f)] public float severChance = 0f;

    public override bool IsHarmful => true;

    public override void Apply(ref EffectContext ctx)
    {
        InjuryController c = ctx.target != null ? InjuryController.For(ctx.target) : null;
        if (c == null || !c.RulesHere || !c.Settings.enabled)
            return;
        InjuryPartState p = PickPart(c);
        if (p == null)
            return;
        if (severChance > 0f && UnityEngine.Random.value < severChance && p.Def.limbLoss != null && p.Def.limbLoss.canBeLost)
        {
            c.LoseLimb(p, ctx.caster, ctx.direction, p.Condition01);
            return;
        }
        InjuryTypeDefinition type = c.Settings.FindType(injuryType);
        if (type != null)
            c.AddInjury(p, type, stage, ctx.caster, true);
    }

    private InjuryPartState PickPart(InjuryController c)
    {
        if (!string.IsNullOrWhiteSpace(part))
        {
            InjuryPartState p = c.GetPart(part);
            return p != null && !p.Lost ? p : null;
        }
        var list = new List<InjuryPartState>();
        foreach (InjuryPartState p in c.Parts)
            if (!p.Lost && (partKinds == null || partKinds.Count == 0 || partKinds.Contains(p.Def.kind)))
                list.Add(p);
        return list.Count > 0 ? list[UnityEngine.Random.Range(0, list.Count)] : null;
    }

    public override string Describe(in AbilityStats s)
    {
        InjuryTypeDefinition t = InjurySettings.Instance.FindType(injuryType);
        string where = !string.IsNullOrWhiteSpace(part) ? part : partKinds != null && partKinds.Count > 0 ? string.Join("/", partKinds) : "a random part";
        string text = $"Causes {(t != null ? t.StageName(stage) : injuryType)} on {where}";
        if (severChance > 0f) text += $", {severChance * 100f:0}% to cut it off";
        return text + Chance(chance);
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (string.IsNullOrWhiteSpace(injuryType))
            errors.Add($"{owner}: Inflict Injury has no injury type.");
        else if (InjurySettings.Instance.enabled && InjurySettings.Instance.FindType(injuryType) == null)
            warnings.Add($"{owner}: injury type '{injuryType}' is not in the Injury Settings.");
    }

    public override AbilityEffect Clone()
    {
        var c = (InflictInjuryEffect)base.Clone();
        c.partKinds = new List<BodyPartKind>(partKinds ?? new List<BodyPartKind>());
        return c;
    }
}
