using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Who receives an effect when an action hits someone.</summary>
public enum EffectRecipient
{
    /// <summary>The character that was hit.</summary>
    HitTarget,
    /// <summary>The caster, once for each character hit (life steal, "heal yourself per enemy hit").</summary>
    Caster,
}

/// <summary>What happens when the same over-time effect is applied again to the same target.</summary>
public enum EffectStacking
{
    /// <summary>Restart the duration (most common).</summary>
    Refresh,
    /// <summary>Add another independent copy (up to Max Stacks).</summary>
    Stack,
    /// <summary>Keep the running copy, ignore the new one.</summary>
    Ignore,
}

/// <summary>Everything an effect needs to know about one hit.</summary>
public struct EffectContext
{
    public AbilityCastInstance cast;
    /// <summary>The caster (null if it died or was destroyed before the hit).</summary>
    public CombatEntity caster;
    /// <summary>The character receiving this effect.</summary>
    public CombatEntity target;
    /// <summary>The character that was hit (equals target unless the recipient is the caster).</summary>
    public CombatEntity hitEntity;
    /// <summary>Centre of the hit: area centre, explosion centre, projectile position.</summary>
    public Vector3 origin;
    /// <summary>Point on the hit character.</summary>
    public Vector3 point;
    /// <summary>Flat direction of the hit (aim direction, projectile travel).</summary>
    public Vector3 direction;
    /// <summary>Scale for this hit (edge falloff, reduced damage per pierce...). 1 = full.</summary>
    public float multiplier;
    public AbilityStats stats;

    public AbilityDefinition Ability => cast != null ? cast.Definition : null;
}

/// <summary>
/// Something an ability does to a character it hits: damage, heal, stun, knockback... Add effects to the Hit settings
/// of an action. Write a new effect by deriving from this class; it appears in the dropdown automatically.
/// </summary>
[Serializable]
public abstract class AbilityEffect
{
    [Tooltip("Chance (0-1) that this effect is applied on each hit.")]
    [Range(0f, 1f)] public float chance = 1f;

    [Tooltip("Hit Target: the character that was hit.\nCaster: the caster, once per character hit (life steal, self-buff on hit).")]
    public EffectRecipient recipient = EffectRecipient.HitTarget;

    [Tooltip("Only characters with this relation to the caster get this effect (the action's Hit Filter is checked first). E.g. one nova that damages Enemies and heals Allies.")]
    public TargetFilter onlyAffects = TargetFilter.All;

    /// <summary>Name shown in the dropdown and in tooltips.</summary>
    public virtual string MenuName => AbilityTypeNames.Nice(GetType());

    /// <summary>Applies the effect. Called only when chance and filters passed.</summary>
    public abstract void Apply(ref EffectContext ctx);

    /// <summary>Damage dealt to one target (AI and tooltips).</summary>
    public virtual float EstimateDamage(in AbilityStats s) => 0f;

    /// <summary>Seconds of hard control (stun/root/silence) applied (AI).</summary>
    public virtual float EstimateControl(in AbilityStats s) => 0f;

    /// <summary>One line for tooltips, e.g. "12 damage".</summary>
    public virtual string Describe(in AbilityStats s) => MenuName;

    public virtual void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (chance <= 0f)
            warnings.Add($"{owner}: {MenuName} has 0% chance and will never apply.");
        if (onlyAffects == TargetFilter.None)
            errors.Add($"{owner}: {MenuName} 'Only Affects' is empty, it will never apply.");
    }

    public virtual AbilityEffect Clone() => (AbilityEffect)MemberwiseClone();

    protected static string Chance(float chance) => chance < 0.999f ? $" ({chance * 100f:0}% chance)" : "";
}

// ====================================================================================================== damage
[Serializable, AbilityMenu("Damage/Damage", "Instant damage to the hit character.", 0)]
public class DamageEffect : AbilityEffect
{
    [Tooltip("Damage per hit (before the target's resistances).")]
    [Min(0f)] public float amount = 10f;
    [Tooltip("Random spread: 0.1 = between 90% and 110% of Amount.")]
    [Range(0f, 1f)] public float variance = 0.1f;
    [Tooltip("Chance (0-1) of a critical hit.")]
    [Range(0f, 1f)] public float criticalChance = 0f;
    [Tooltip("Damage multiplier of critical hits.")]
    [Min(1f)] public float criticalMultiplier = 1.5f;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null)
            return;
        float dmg = amount * ctx.stats.damage * ctx.multiplier;
        if (variance > 0f)
            dmg *= UnityEngine.Random.Range(1f - variance, 1f + variance);
        bool crit = criticalChance > 0f && UnityEngine.Random.value < criticalChance;
        if (crit)
            dmg *= criticalMultiplier;
        ctx.target.ApplyDamage(new DamageInfo
        {
            amount = dmg,
            source = ctx.caster,
            ability = ctx.Ability,
            point = ctx.point,
            direction = ctx.direction,
            isCritical = crit,
        });
    }

    public override float EstimateDamage(in AbilityStats s) => amount * s.damage * (1f + criticalChance * (criticalMultiplier - 1f));

    public override string Describe(in AbilityStats s) => $"{amount * s.damage:0.#} damage{Chance(chance)}";

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (amount <= 0f)
            warnings.Add($"{owner}: Damage amount is 0.");
    }
}

[Serializable, AbilityMenu("Damage/Damage Over Time", "Poison, burn, bleed: damage every tick for a duration.", 1)]
public class DamageOverTimeEffect : AbilityEffect
{
    [Tooltip("Damage of each tick.")]
    [Min(0f)] public float damagePerTick = 3f;
    [Tooltip("Seconds between ticks.")]
    [Min(0.05f)] public float tickInterval = 0.5f;
    [Tooltip("Total duration in seconds.")]
    [Min(0.05f)] public float duration = 4f;
    [Tooltip("What happens when it is applied again to the same target.")]
    public EffectStacking stacking = EffectStacking.Refresh;
    [Tooltip("Stack mode: maximum copies at once.")]
    [Min(1)] public int maxStacks = 3;
    [Tooltip("Optional particle attached to the target while it lasts.")]
    public GameObject attachedVfx;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null)
            return;
        float perTick = damagePerTick * ctx.stats.damage * ctx.multiplier;
        PeriodicEffectRunner.Apply(ctx.target, ctx.caster, ctx.Ability, this, -perTick, tickInterval, duration * ctx.stats.duration, stacking, maxStacks, attachedVfx);
    }

    public override float EstimateDamage(in AbilityStats s) => damagePerTick * s.damage * Mathf.Floor(duration * s.duration / tickInterval);

    public override string Describe(in AbilityStats s) =>
        $"{damagePerTick * s.damage:0.#} damage every {tickInterval:0.##}s for {duration * s.duration:0.#}s{Chance(chance)}";
}

// ====================================================================================================== support
[Serializable, AbilityMenu("Support/Heal", "Restores health instantly or over time.", 0)]
public class HealEffect : AbilityEffect
{
    [Tooltip("Health restored (total when over time).")]
    [Min(0f)] public float amount = 15f;
    [Tooltip("0 = instant. Otherwise the amount is spread over this many seconds.")]
    [Min(0f)] public float duration = 0f;
    [Tooltip("Seconds between heal ticks when over time.")]
    [Min(0.05f)] public float tickInterval = 0.5f;

    public HealEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null)
            return;
        float total = amount * ctx.stats.heal * ctx.multiplier;
        float d = duration * ctx.stats.duration;
        if (d <= 0f)
        {
            ctx.target.ApplyHeal(total, ctx.caster);
            return;
        }
        int ticks = Mathf.Max(1, Mathf.FloorToInt(d / tickInterval));
        PeriodicEffectRunner.Apply(ctx.target, ctx.caster, ctx.Ability, this, total / ticks, tickInterval, d, EffectStacking.Refresh, 1, null);
    }

    public override string Describe(in AbilityStats s) =>
        duration > 0f ? $"Heals {amount * s.heal:0.#} over {duration * s.duration:0.#}s" : $"Heals {amount * s.heal:0.#}";
}

[Serializable, AbilityMenu("Support/Cleanse", "Removes stun, silence, root, slow and taunt.", 2)]
public class CleanseEffect : AbilityEffect
{
    public CleanseEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target != null)
            ctx.target.ClearControl();
    }

    public override string Describe(in AbilityStats s) => "Removes crowd control";
}

[Serializable, AbilityMenu("Support/Invulnerability", "The target takes no damage for a short time.", 3)]
public class InvulnerabilityEffect : AbilityEffect
{
    [Min(0.05f)] public float duration = 1f;

    public InvulnerabilityEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target != null)
            ctx.target.SetInvulnerable(duration * ctx.stats.duration);
    }

    public override string Describe(in AbilityStats s) => $"Invulnerable for {duration * s.duration:0.#}s";
}

// ====================================================================================================== status (legacy)
[Serializable, AbilityMenu("Status/Status Effect", "Changes a status through the status controller: HP, stamina, mana, hunger, speed, heal/damage factors, regeneration... Same as the old AttackEffect.", 0)]
public class StatEffect : AbilityEffect
{
    [Tooltip("Which status to change.")]
    public AttackEffectType effectType = AttackEffectType.Hp;
    [Tooltip("Name of the effect (effects with the same name replace each other unless Stackable).")]
    public string effectName = "Ability Effect";
    [Tooltip("Amount. Negative HP = damage, positive = heal.")]
    public float amount = -10f;
    [Tooltip("Random spread: 0.1 = between 90% and 110% of Amount.")]
    [Range(0f, 1f)] public float variance = 0f;
    [Tooltip("Duration in seconds. 0 = instant.")]
    [Min(0f)] public float duration = 0f;
    [Tooltip("Seconds between ticks for procedural effects.")]
    [Min(0.05f)] public float tickInterval = 1f;
    [Tooltip("Spread Amount over the duration in ticks (true) or apply it all at once and revert when it ends (false).")]
    public bool procedural = true;
    [Tooltip("Can several copies with the same name run at once?")]
    public bool stackable = false;
    [Range(0f, 1f)] public float criticalChance = 0f;
    [Min(1f)] public float criticalMultiplier = 1.5f;

    [NonSerialized] private AttackEffect cached;

    /// <summary>Builds a StatEffect from a legacy AttackEffect.</summary>
    public static StatEffect FromLegacy(AttackEffect e)
    {
        var s = new StatEffect
        {
            effectType = e.effectType,
            effectName = string.IsNullOrEmpty(e.effectName) ? e.effectType.ToString() : e.effectName,
            amount = e.randomAmount ? (e.minAmount + e.maxAmount) * 0.5f : e.amount,
            duration = e.randomTimeBuffEffect ? (e.minTimeBuffEffect + e.maxTimeBuffEffect) * 0.5f : e.timeBuffEffect,
            tickInterval = Mathf.Max(0.05f, e.randomTickCooldown ? (e.minTickCooldown + e.maxTickCooldown) * 0.5f : e.tickCooldown),
            procedural = e.isProcedural,
            stackable = e.isStackable,
            criticalChance = e.criticalChance,
            criticalMultiplier = Mathf.Max(1f, e.criticalDamageMultiplier),
            chance = e.probabilityToApply,
        };
        if (e.randomAmount)
        {
            float mid = Mathf.Abs(s.amount);
            s.variance = mid > 0f ? Mathf.Clamp01(Mathf.Abs(e.maxAmount - e.minAmount) * 0.5f / mid) : 0f;
        }
        return s;
    }

    private bool IsHealthDamage => effectType == AttackEffectType.Hp && amount < 0f;

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity target = ctx.target;
        if (target == null)
            return;

        float value = amount;
        if (variance > 0f)
            value *= UnityEngine.Random.Range(1f - variance, 1f + variance);
        if (criticalChance > 0f && UnityEngine.Random.value < criticalChance)
            value *= criticalMultiplier;
        if (effectType == AttackEffectType.Hp)
            value *= (value < 0f ? ctx.stats.damage : ctx.stats.heal) * ctx.multiplier;
        float time = duration * ctx.stats.duration;

        if (effectType == AttackEffectType.Hp && time <= 0f)
        {
            if (value < 0f)
                target.ApplyDamage(new DamageInfo { amount = -value, source = ctx.caster, ability = ctx.Ability, point = ctx.point, direction = ctx.direction });
            else
                target.ApplyHeal(value, ctx.caster);
            return;
        }

        BaseStatusController status = target.Status;
        if (status == null)
            return;
        if (IsHealthDamage)
            target.RegisterPeriodicSource(ctx.caster, time);

        if (cached == null)
            cached = new AttackEffect();
        cached.effectType = effectType;
        cached.effectName = effectName;
        cached.amount = value;
        cached.timeBuffEffect = time;
        cached.tickCooldown = tickInterval;
        cached.isProcedural = procedural;
        cached.isStackable = stackable;
        cached.enemyEffect = value < 0f;
        status.ApplyEffect(cached, value, time, tickInterval);
    }

    public override float EstimateDamage(in AbilityStats s) => IsHealthDamage ? -amount * s.damage : 0f;

    public override string Describe(in AbilityStats s)
    {
        float v = effectType == AttackEffectType.Hp ? amount * (amount < 0f ? s.damage : s.heal) : amount;
        string time = duration > 0f ? $" over {duration * s.duration:0.#}s" : "";
        return $"{effectType} {v:+0.#;-0.#}{time}{Chance(chance)}";
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if (Mathf.Approximately(amount, 0f))
            warnings.Add($"{owner}: Status Effect amount is 0.");
        if (string.IsNullOrEmpty(effectName))
            warnings.Add($"{owner}: Status Effect has no name; same-named effects replace each other.");
    }
}

// ====================================================================================================== control
/// <summary>Base of crowd-control effects (duration scaled by the Control modifier).</summary>
[Serializable]
public abstract class ControlEffect : AbilityEffect
{
    [Tooltip("Duration in seconds.")]
    [Min(0.05f)] public float duration = 1.5f;

    protected abstract ControlType Type { get; }
    protected virtual float Magnitude => 0f;

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target != null)
            ctx.target.ApplyControl(Type, duration * ctx.stats.control, ctx.caster, Magnitude);
    }

    public override float EstimateControl(in AbilityStats s) =>
        Type == ControlType.Stun || Type == ControlType.Root || Type == ControlType.Silence ? duration * s.control : 0f;

    public override string Describe(in AbilityStats s) => $"{Type} {duration * s.control:0.#}s{Chance(chance)}";
}

[Serializable, AbilityMenu("Control/Stun", "The target cannot move, attack or cast.", 0)]
public class StunEffect : ControlEffect
{
    protected override ControlType Type => ControlType.Stun;
}

[Serializable, AbilityMenu("Control/Silence", "The target cannot use abilities (can still move).", 1)]
public class SilenceEffect : ControlEffect
{
    public SilenceEffect() { duration = 2f; }
    protected override ControlType Type => ControlType.Silence;
}

[Serializable, AbilityMenu("Control/Root", "The target cannot move (can still attack and cast).", 2)]
public class RootEffect : ControlEffect
{
    public RootEffect() { duration = 2f; }
    protected override ControlType Type => ControlType.Root;
}

[Serializable, AbilityMenu("Control/Slow", "Reduces movement speed.", 3)]
public class SlowEffect : ControlEffect
{
    [Tooltip("Fraction of speed removed (0.4 = 40% slower).")]
    [Range(0.05f, 0.95f)] public float slowAmount = 0.4f;

    public SlowEffect() { duration = 3f; }
    protected override ControlType Type => ControlType.Slow;
    protected override float Magnitude => slowAmount;

    public override string Describe(in AbilityStats s) => $"Slow {slowAmount * 100f:0}% for {duration * s.control:0.#}s{Chance(chance)}";
}

[Serializable, AbilityMenu("Control/Taunt", "Forces the target (mobs) to attack the caster.", 4)]
public class TauntEffect : ControlEffect
{
    public TauntEffect() { duration = 3f; }
    protected override ControlType Type => ControlType.Taunt;
}

// ====================================================================================================== displacement
[Serializable, AbilityMenu("Displacement/Knockback", "Pushes the target away.", 0)]
public class KnockbackEffect : AbilityEffect
{
    [Tooltip("Distance pushed (metres).")]
    [Min(0f)] public float distance = 4f;
    [Tooltip("Seconds the push takes.")]
    [Min(0.05f)] public float duration = 0.35f;
    [Tooltip("Height of the arc (0 = sliding along the ground).")]
    [Min(0f)] public float arcHeight = 0.5f;
    [Tooltip("Away From Origin: away from the centre of the hit. Along Aim: in the ability's direction. Up: launched upward.")]
    public DisplacementDirection direction = DisplacementDirection.AwayFromOrigin;
    [Tooltip("The target cannot act while being pushed (short stun for the duration).")]
    public bool disableTarget = true;

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity t = ctx.target;
        if (t == null)
            return;
        Vector3 dir;
        switch (direction)
        {
            case DisplacementDirection.AlongAim:
                dir = ctx.direction;
                break;
            case DisplacementDirection.Up:
                dir = Vector3.zero;
                break;
            default:
                dir = t.Position - ctx.origin;
                break;
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f && direction != DisplacementDirection.Up)
            dir = ctx.direction.sqrMagnitude > 1e-4f ? ctx.direction : t.transform.forward * -1f;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;

        float d = distance * ctx.stats.displacement * ctx.multiplier;
        float arc = direction == DisplacementDirection.Up ? Mathf.Max(arcHeight, d) : arcHeight;
        if (t.ApplyDisplacement(dir * d, duration, arc, false, ctx.caster) && disableTarget)
            t.ApplyControl(ControlType.Stun, duration, ctx.caster);
    }

    public override float EstimateControl(in AbilityStats s) => disableTarget ? duration : 0f;

    public override string Describe(in AbilityStats s) => $"Knockback {distance * s.displacement:0.#}m{Chance(chance)}";
}

[Serializable, AbilityMenu("Displacement/Pull", "Drags the target toward the caster or the centre of the hit (hooks, vortexes).", 1)]
public class PullEffect : AbilityEffect
{
    [Tooltip("Maximum distance the target is dragged (metres).")]
    [Min(0f)] public float maxDistance = 8f;
    [Tooltip("The target stops this far from the anchor (metres).")]
    [Min(0f)] public float stopDistance = 1.5f;
    [Tooltip("Seconds the pull takes.")]
    [Min(0.05f)] public float duration = 0.3f;
    [Tooltip("Caster: toward the caster (hook). Hit Origin: toward the centre of the hit (vortex).")]
    public PullAnchor anchorTo = PullAnchor.Caster;
    [Tooltip("The target cannot act while being pulled.")]
    public bool disableTarget = true;

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity t = ctx.target;
        if (t == null)
            return;
        Vector3 anchorPos;
        if (anchorTo == PullAnchor.Caster && ctx.caster != null)
            anchorPos = ctx.caster.Position;
        else if (anchorTo == PullAnchor.Caster && ctx.cast != null)
            anchorPos = ctx.cast.CasterPosition;
        else
            anchorPos = ctx.origin;

        Vector3 delta = anchorPos - t.Position;
        delta.y = 0f;
        float dist = delta.magnitude;
        float stop = stopDistance + (ctx.caster != null && anchorTo == PullAnchor.Caster ? ctx.caster.Radius + t.Radius : t.Radius);
        float travel = Mathf.Min(maxDistance * ctx.stats.displacement, dist - stop);
        if (travel <= 0.05f || dist < 1e-4f)
            return;
        if (t.ApplyDisplacement(delta / dist * travel, duration, 0f, true, ctx.caster) && disableTarget)
            t.ApplyControl(ControlType.Stun, duration, ctx.caster);
    }

    public override float EstimateControl(in AbilityStats s) => disableTarget ? duration : 0f;

    public override string Describe(in AbilityStats s) =>
        $"Pulls {(anchorTo == PullAnchor.Caster ? "toward the caster" : "to the centre")} up to {maxDistance * s.displacement:0.#}m{Chance(chance)}";
}

// ====================================================================================================== runner
/// <summary>
/// Runs damage/heal over time on a target without coroutines. Keyed by (target, ability, effect) so re-applying
/// refreshes, stacks or is ignored according to <see cref="EffectStacking"/>.
/// </summary>
public sealed class PeriodicEffectRunner : IAbilityRuntimeObject
{
    private struct Key : IEquatable<Key>
    {
        public CombatEntity target, source;
        public object effect;
        public bool Equals(Key o) => ReferenceEquals(target, o.target) && ReferenceEquals(source, o.source) && ReferenceEquals(effect, o.effect);
        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public override int GetHashCode() => (RefHash(target) * 397) ^ RefHash(source) ^ (effect != null ? effect.GetHashCode() : 0);
        private static int RefHash(object o) => o is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }

    private static readonly Dictionary<Key, List<PeriodicEffectRunner>> active = new Dictionary<Key, List<PeriodicEffectRunner>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    private Key key;
    private CombatEntity target;
    private CombatEntity source;
    private AbilityDefinition ability;
    private float perTick;          // negative = damage, positive = heal
    private float interval;
    private float remaining;
    private float nextTick;
    private GameObject vfx;
    private bool disposed;

    /// <summary>Applies an over-time effect. <paramref name="perTick"/> negative = damage, positive = heal.</summary>
    public static void Apply(CombatEntity target, CombatEntity source, AbilityDefinition ability, object effect, float perTick, float interval, float duration,
        EffectStacking stacking, int maxStacks, GameObject attachedVfx)
    {
        if (target == null || duration <= 0f || Mathf.Approximately(perTick, 0f))
            return;
        var key = new Key { target = target, source = source != null ? source : null, effect = effect };
        if (!active.TryGetValue(key, out List<PeriodicEffectRunner> list))
        {
            list = new List<PeriodicEffectRunner>(1);
            active[key] = list;
        }

        if (list.Count > 0)
        {
            if (stacking == EffectStacking.Ignore)
                return;
            if (stacking == EffectStacking.Refresh)
            {
                PeriodicEffectRunner r = list[0];
                r.remaining = Mathf.Max(r.remaining, duration);
                r.perTick = perTick;
                return;
            }
            if (list.Count >= Mathf.Max(1, maxStacks))
            {
                // Refresh the oldest instead of adding more.
                PeriodicEffectRunner oldest = list[0];
                oldest.remaining = duration;
                return;
            }
        }

        var runner = new PeriodicEffectRunner
        {
            key = key,
            target = target,
            source = source,
            ability = ability,
            perTick = perTick,
            interval = Mathf.Max(0.05f, interval),
            remaining = duration,
            nextTick = Mathf.Max(0.05f, interval),
        };
        if (attachedVfx != null)
            runner.vfx = AbilityPool.Spawn(attachedVfx, target.Center, target.transform.rotation, target.transform);
        list.Add(runner);
        if (perTick < 0f)
            target.RegisterPeriodicSource(source, duration);
        AbilityRuntime.Add(runner);
    }

    public bool Tick(float dt)
    {
        if (target == null || !target.IsAlive)
            return false;
        remaining -= dt;
        nextTick -= dt;
        while (nextTick <= 0f && remaining > -interval * 0.5f)
        {
            nextTick += interval;
            if (perTick < 0f)
            {
                target.ApplyDamage(new DamageInfo
                {
                    amount = -perTick,
                    source = source,
                    ability = ability,
                    point = target.Center,
                    isPeriodic = true,
                });
            }
            else
            {
                target.ApplyHeal(perTick, source);
            }
            if (target == null || !target.IsAlive)
                return false;
        }
        return remaining > 0f;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        if (active.TryGetValue(key, out List<PeriodicEffectRunner> list))
        {
            list.Remove(this);
            if (list.Count == 0)
                active.Remove(key);
        }
        if (vfx != null)
            AbilityPool.Release(vfx);
        vfx = null;
        target = null;
        source = null;
    }
}

/// <summary>Turns type names into readable labels ("AreaHitAction" -> "Area Hit").</summary>
public static class AbilityTypeNames
{
    private static readonly Dictionary<Type, string> cache = new Dictionary<Type, string>();

    public static string Nice(Type t)
    {
        if (t == null)
            return "(none)";
        if (cache.TryGetValue(t, out string s))
            return s;
        var menu = (AbilityMenuAttribute)Attribute.GetCustomAttribute(t, typeof(AbilityMenuAttribute), false);
        if (menu != null && !string.IsNullOrEmpty(menu.path))
        {
            int slash = menu.path.LastIndexOf('/');
            s = slash >= 0 ? menu.path.Substring(slash + 1) : menu.path;
        }
        else
        {
            string n = t.Name;
            if (n.EndsWith("Action")) n = n.Substring(0, n.Length - 6);
            else if (n.EndsWith("Effect")) n = n.Substring(0, n.Length - 6);
            var sb = new System.Text.StringBuilder(n.Length + 4);
            for (int i = 0; i < n.Length; i++)
            {
                if (i > 0 && char.IsUpper(n[i]) && !char.IsUpper(n[i - 1]))
                    sb.Append(' ');
                sb.Append(n[i]);
            }
            s = sb.ToString();
        }
        cache[t] = s;
        return s;
    }
}
