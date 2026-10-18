using System;
using System.Collections.Generic;
using UnityEngine;

// Ability effects of the survival systems: sanity, revives and body heat. They appear in an ability's effect dropdown
// (Support ▸ ...), so skills, traits, racial abilities, items with abilities and mob attacks can all use them.

[Serializable, AbilityMenu("Support/Sanity", "Restores (positive) or drains (negative) sanity: a calming song, a prayer, a horror's scream.", 6)]
public class SanityEffect : AbilityEffect
{
    [Tooltip("Sanity changed per hit: positive restores, negative drains (reduced by the target's Sanity Resistance).")]
    public float amount = 15f;
    [Tooltip("0 = at once. Otherwise spread over this many seconds.")]
    [Min(0f)] public float duration = 0f;
    [Tooltip("Seconds between ticks when spread over time.")]
    [Min(0.1f)] public float tickInterval = 1f;

    public SanityEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public override bool IsHarmful => amount < 0f;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null)
            return;
        SanityManager s = ctx.target.GetComponentInChildren<SanityManager>();
        if (s == null)
            return;
        float total = amount * ctx.multiplier * (amount > 0f ? ctx.stats.heal : ctx.stats.damage);
        if (duration <= 0f)
            s.ChangeSanity(total, ctx.caster);
        else
            s.AddSanityEffect(ctx.Ability != null ? ctx.Ability.DisplayName + " (sanity)" : "Sanity", total, duration * ctx.stats.duration, tickInterval, true, true);
    }

    public override string Describe(in AbilityStats s) =>
        (amount >= 0f ? $"Restores {amount * s.heal:0.#} sanity" : $"Drains {-amount * s.damage:0.#} sanity") +
        (duration > 0f ? $" over {duration * s.duration:0.#}s" : "") + Chance(chance);
}

[Serializable, AbilityMenu("Support/Revive", "Wakes an unconscious ally with some health (optionally brings the dead back too).", 7)]
public class ReviveEffect : AbilityEffect
{
    [Tooltip("Health the revived character gets back (fraction of its max).")]
    [Range(0.01f, 1f)] public float healthFraction = 0.35f;
    [Tooltip("Also resurrects characters that are already dead (a resurrection spell). Off = only unconscious ones.")]
    public bool alsoResurrectDead = false;
    [Tooltip("Seconds of invulnerability after the revive (0 = the Unconsciousness rules decide).")]
    [Min(0f)] public float invulnerability = 0f;

    public ReviveEffect()
    {
        onlyAffects = TargetFilter.Allies | TargetFilter.Self;
    }

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity t = ctx.target;
        if (t == null || t.IsReplica)
            return;
        float fraction = Mathf.Clamp01(healthFraction * Mathf.Max(0.01f, ctx.stats.heal));
        bool done = false;
        if (t.IsUnconscious)
            done = t.Downed.Revive(fraction, ctx.caster);
        else if (alsoResurrectDead && t.IsDead)
        {
            t.Revive(fraction);
            done = true;
        }
        if (done && invulnerability > 0f)
            t.SetInvulnerable(invulnerability);
    }

    public override string Describe(in AbilityStats s) =>
        $"Revives {(alsoResurrectDead ? "a fallen" : "an unconscious")} ally with {healthFraction * 100f:0}% health{Chance(chance)}";
}

[Serializable, AbilityMenu("Support/Body Heat", "Warms (positive) or chills (negative) the target's body temperature: a warming spell, a frost breath.", 8)]
public class BodyHeatEffect : AbilityEffect
{
    [Tooltip("Body-heat points (0-100 scale, about 1.6 per °C): positive warms, negative chills. Heat / Cold Resistance reduce it.")]
    public float amount = 10f;
    [Tooltip("Also protects for this many seconds: the body temperature drifts this much slower toward the weather (0 = no protection).")]
    [Min(0f)] public float protectionSeconds = 0f;
    [Tooltip("With protection: percent less drift toward cold and heat while it lasts.")]
    [Range(0f, 100f)] public float protectionPercent = 50f;

    public BodyHeatEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public override bool IsHarmful => amount < 0f;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null)
            return;
        BodyHeatManager b = ctx.target.GetComponentInChildren<BodyHeatManager>();
        if (b == null)
            return;
        b.ApplyHeatChange(amount * ctx.multiplier);
        if (protectionSeconds > 0f && protectionPercent > 0f)
        {
            var mods = new List<CombatStatModifier>
            {
                new CombatStatModifier(CombatStatType.ColdResistance, protectionPercent),
                new CombatStatModifier(CombatStatType.HeatResistance, protectionPercent),
            };
            TimedStatModifiers.For(ctx.target)?.Apply(this, amount >= 0f ? "Warmed" : "Cooled", ctx.caster, false, ElementType.None,
                protectionSeconds * ctx.stats.duration, 1f, mods, null, null, EffectStacking.Refresh);
        }
    }

    public override string Describe(in AbilityStats s) =>
        (amount >= 0f ? $"Warms by {amount:0.#}" : $"Chills by {-amount:0.#}") +
        (protectionSeconds > 0f ? $", {protectionPercent:0}% temperature protection for {protectionSeconds * s.duration:0.#}s" : "") + Chance(chance);
}
