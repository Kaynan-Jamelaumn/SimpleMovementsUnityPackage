using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fate's Debt: once per fight (or per duel), a killing blow is ignored: the character keeps the health it had before
/// the hit. Fate then collects: weaker for a while. It is ready again after a duel ends or some time out of combat.
/// </summary>
[Serializable, AbilityMenu("Triggered/Fate's Debt", "Once per fight or duel, a killing blow is ignored; fate collects with a debuff.", 0)]
public class FatesDebtTrait : TraitBehaviour
{
    [Tooltip("Seconds out of combat before it is ready again.")]
    [Min(1f)] public float resetOutOfCombat = 20f;
    [Tooltip("Seconds of invulnerability after the saved blow.")]
    [Min(0f)] public float invulnerableSeconds = 1f;
    [Tooltip("The debt: percent weapon and ability damage for the seconds below (negative = weaker).")]
    public float debtDamagePercent = -20f;
    [Min(0f)] public float debtSeconds = 15f;

    [NonSerialized] private bool spent;
    [NonSerialized] private bool wasDueling;
    [NonSerialized] private float lastHealth;
    [NonSerialized] private CombatEntity subscribed;

    protected override bool ReadsStrengthLive => true;

    public override string Describe() => "Once per fight or duel, ignore a killing blow (fate collects: weaker for a while).";

    public override string LiveStatus => spent ? "spent (ready after the fight)" : "ready";

    public override void OnAdded(TraitContext ctx)
    {
        spent = false;
        Subscribe(ctx.Entity);
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        CombatEntity e = ctx.Entity;
        if (subscribed == null)
            Subscribe(e);
        if (e == null || e.Health == null)
            return;
        if (e.Health.CurrentValue > 0f)
            lastHealth = e.Health.CurrentValue;
        bool dueling = PlayerBonds.IsDueling(e);
        if (spent && ((wasDueling && !dueling) || (!dueling && !ctx.InCombat(resetOutOfCombat))))
            spent = false;
        wasDueling = dueling;
    }

    private void Subscribe(CombatEntity e)
    {
        if (e == null || e == subscribed)
            return;
        if (subscribed != null)
            subscribed.LethalDamage -= OnLethal;
        subscribed = e;
        e.LethalDamage += OnLethal;
    }

    private bool OnLethal(DamageInfo info)
    {
        if (spent || subscribed == null || subscribed.Health == null)
            return false;
        spent = true;
        subscribed.Health.AddCurrentValue(Mathf.Max(1f, lastHealth) - subscribed.Health.CurrentValue);
        if (invulnerableSeconds > 0f)
            subscribed.SetInvulnerable(invulnerableSeconds);
        if (debtDamagePercent != 0f && debtSeconds > 0f)
            TimedStatModifiers.For(subscribed).Apply("fates debt", "Fate's Debt", subscribed, debtDamagePercent < 0f, ElementType.None, debtSeconds, 1f,
                null, null, new List<TraitModifier>
                {
                    TraitModifier.Percent(TraitStat.WeaponDamage, debtDamagePercent),
                    TraitModifier.Percent(TraitStat.AbilityDamage, debtDamagePercent),
                });
        return true;
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (subscribed != null)
            subscribed.LethalDamage -= OnLethal;
        subscribed = null;
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var c = (FatesDebtTrait)base.CreateRuntimeCopy();
        c.subscribed = null;
        c.spent = false;
        return c;
    }
}

/// <summary>
/// Acid Blood (the Corpse Golem essence): attackers in melee get splashed: poisoned and their equipment corrodes. The
/// acid eats the character's own equipment a little too.
/// </summary>
[Serializable, AbilityMenu("Triggered/Acid Blood", "Melee attackers are splashed with acid blood: poisoned, their equipment corrodes; your own equipment corrodes a little.", 0)]
public class AcidBloodTrait : TraitBehaviour
{
    [Range(0f, 1f)] public float chance = 0.4f;
    [Tooltip("Only attackers this close (metres) are splashed.")]
    [Min(0.5f)] public float splashRange = 3.5f;
    [Min(0f)] public float cooldown = 1.5f;
    [Min(0f)] public float poisonPerTick = 2f;
    [Min(0.5f)] public float poisonSeconds = 4f;
    [Tooltip("Durability taken from one of the attacker's equipped items.")]
    [Min(0)] public int corrodeAttacker = 6;
    [Tooltip("Durability taken from one of your own equipped items each time.")]
    [Min(0)] public int corrodeSelf = 2;

    [NonSerialized] private float ready;
    [NonSerialized] private CombatEntity subscribed;

    public override string Describe() => $"Acid blood: {chance:P0} of melee hits taken splash the attacker (poison, corroded gear); your gear corrodes a little.";

    public override void OnAdded(TraitContext ctx) => Subscribe(ctx.Entity);

    public override void Tick(TraitContext ctx, float dt)
    {
        if (subscribed == null)
            Subscribe(ctx.Entity);
    }

    private void Subscribe(CombatEntity e)
    {
        if (e == null || e == subscribed)
            return;
        if (subscribed != null)
            subscribed.Damaged -= OnDamaged;
        subscribed = e;
        e.Damaged += OnDamaged;
    }

    private void OnDamaged(DamageInfo info)
    {
        CombatEntity attacker = info.source;
        if (attacker == null || attacker == subscribed || info.isReflected || info.isPeriodic || Time.time < ready || EquipmentProcs.Depth > 0)
            return;
        if ((attacker.Center - subscribed.Center).sqrMagnitude > splashRange * splashRange || UnityEngine.Random.value > chance * Mathf.Max(0.1f, multiplier))
            return;
        ready = Time.time + cooldown;
        var hit = new HitSettings
        {
            filter = TargetFilter.All,
            blockedByObstacles = false,
            effects = new List<AbilityEffect>
            {
                new DamageOverTimeEffect { element = ElementType.Poison, damagePerTick = poisonPerTick, tickInterval = 0.5f, duration = poisonSeconds },
                new CorrodeEquipmentEffect { amount = corrodeAttacker, items = 1 },
            },
        };
        EquipmentProcs.Apply(subscribed, attacker, hit, null, 1f, null, null);
        if (corrodeSelf > 0)
            CorrodeEquipmentEffect.Corrode(subscribed, corrodeSelf, 0f, 1);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (subscribed != null)
            subscribed.Damaged -= OnDamaged;
        subscribed = null;
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var c = (AcidBloodTrait)base.CreateRuntimeCopy();
        c.subscribed = null;
        return c;
    }
}

/// <summary>Born Gambler: while fighting, fate flips a coin every so often: a random stat goes up, or down.</summary>
[Serializable, AbilityMenu("Triggered/Born Gambler", "In combat, every few seconds a coin flip raises or lowers a random stat for a while.", 0)]
public class GamblerTrait : TraitBehaviour
{
    [Min(3f)] public float interval = 20f;
    [Tooltip("Chance the flip is good.")]
    [Range(0f, 1f)] public float luck = 0.6f;
    [Min(1f)] public float buffSeconds = 12f;

    [NonSerialized] private float next;

    public override string Describe() => $"In combat, every {interval:0} s a coin flip raises ({luck:P0}) or lowers a random stat for {buffSeconds:0} s.";

    public override void Tick(TraitContext ctx, float dt)
    {
        CombatEntity e = ctx.Entity;
        if (e == null || e.IsReplica || !ctx.InCombat(5f))
            return;
        if (Time.time < next)
            return;
        next = Time.time + interval;
        new StatGambleEffect { mode = StatGambleMode.CoinFlip, luck = Mathf.Clamp01(luck * Mathf.Max(0.1f, multiplier)), duration = buffSeconds }.ApplyTo(e, e);
    }
}

/// <summary>Immune to some crowd control or displacement for as long as the trait is owned.</summary>
[Serializable, AbilityMenu("Passive/Immune To Control", "The character ignores the chosen crowd control or displacement (knockback, pull, stun...).", 0)]
public class ControlImmunityTrait : TraitBehaviour
{
    public ControlImmunity immuneTo = ControlImmunity.AllDisplacement;

    [NonSerialized] private CombatEntity applied;

    public override string Describe() => $"Immune to {immuneTo}.";

    public override void OnAdded(TraitContext ctx) => Apply(ctx.Entity);

    public override void Tick(TraitContext ctx, float dt)
    {
        if (applied == null)
            Apply(ctx.Entity);
    }

    private void Apply(CombatEntity e)
    {
        if (e == null || applied != null)
            return;
        applied = e;
        e.AddImmunity(immuneTo);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (applied != null)
            applied.RemoveImmunity(immuneTo);
        applied = null;
    }

    public override TraitBehaviour CreateRuntimeCopy()
    {
        var c = (ControlImmunityTrait)base.CreateRuntimeCopy();
        c.applied = null;
        return c;
    }
}
