using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Is a timed stat change helpful or harmful (cleanses, resistances and stats treat them differently)?</summary>
public enum StatBuffKind
{
    /// <summary>Decided by its values: any lowered stat makes it a debuff.</summary>
    Auto,
    Buff,
    Debuff,
}

/// <summary>
/// Timed stat changes on a character (buffs and debuffs from abilities, weapons, consumables): combat stats,
/// elemental resistances and character stats (max health, move speed, damage taken...). Each one is applied as one
/// set of modifiers and removed EXACTLY when it expires, is cleansed, or the character dies - values never drift.
/// Added to a character automatically the first time it receives one.
/// </summary>
[DisallowMultipleComponent]
public class TimedStatModifiers : MonoBehaviour
{
    /// <summary>One active buff or debuff.</summary>
    public sealed class Active
    {
        public string name;
        public object key;
        public CombatEntity source;
        public bool debuff;
        public ElementType element;
        public float until;
        public float duration;
        public float strength;
        internal object statsToken;
        internal TraitManager.AppliedModifiers traitHandle;
        internal GameObject vfx;

        public float Remaining => Mathf.Max(0f, until - Time.time);
    }

    private readonly List<Active> active = new List<Active>();
    private CombatEntity entity;
    private CombatStats stats;
    private TraitManager traits;

    /// <summary>Raised when a buff or debuff starts or ends (status icons).</summary>
    public event Action Changed;

    public IReadOnlyList<Active> All => active;

    /// <summary>The timed modifiers of a character (added when missing).</summary>
    public static TimedStatModifiers For(CombatEntity e)
    {
        if (e == null)
            return null;
        TimedStatModifiers t = e.GetComponent<TimedStatModifiers>();
        return t != null ? t : e.gameObject.AddComponent<TimedStatModifiers>();
    }

    private void Awake()
    {
        entity = GetComponent<CombatEntity>();
        if (entity != null)
        {
            entity.Died += OnDied;
            entity.Revived += OnRevived;
        }
    }

    private void OnDestroy()
    {
        if (entity != null)
        {
            entity.Died -= OnDied;
            entity.Revived -= OnRevived;
        }
        RemoveAll(_ => true);
    }

    private void OnDied(CombatEntity killer) => RemoveAll(_ => true);
    private void OnRevived() => RemoveAll(_ => true);

    private void Update()
    {
        if (active.Count == 0)
            return;
        float now = Time.time;
        bool removed = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (now >= active[i].until)
            {
                Revert(active[i]);
                active.RemoveAt(i);
                removed = true;
            }
        }
        if (removed)
            Changed?.Invoke();
    }

    /// <summary>
    /// Applies a buff / debuff for <paramref name="duration"/> seconds at <paramref name="strength"/> (1 = as authored).
    /// <paramref name="key"/> identifies it for stacking (the effect asset); <paramref name="stacking"/>: Refresh restarts
    /// the running copy, Stack adds copies up to <paramref name="maxStacks"/>, Ignore keeps the running one.
    /// </summary>
    public Active Apply(object key, string label, CombatEntity source, bool debuff, ElementType element, float duration, float strength,
        IList<CombatStatModifier> combatStats, IList<ElementalResistance> resistances, IList<TraitModifier> characterStats,
        EffectStacking stacking = EffectStacking.Refresh, int maxStacks = 1, GameObject attachedVfx = null)
    {
        if (duration <= 0f)
            return null;
        CacheSystems();
        int copies = 0;
        Active latest = null;
        for (int i = 0; i < active.Count; i++)
        {
            if (!Equals(active[i].key, key) || active[i].source != source)
                continue;
            copies++;
            latest = active[i];
        }
        if (latest != null)
        {
            switch (stacking)
            {
                case EffectStacking.Ignore:
                    return latest;
                case EffectStacking.Refresh:
                    // Restart: re-applied with the new strength, the timer starts over.
                    Revert(latest);
                    active.Remove(latest);
                    break;
                case EffectStacking.Stack:
                    if (copies >= Mathf.Max(1, maxStacks))
                    {
                        latest.until = Time.time + duration; // at the cap: refresh the newest copy
                        Changed?.Invoke();
                        return latest;
                    }
                    break;
            }
        }

        var a = new Active
        {
            name = string.IsNullOrEmpty(label) ? (debuff ? "Debuff" : "Buff") : label,
            key = key,
            source = source,
            debuff = debuff,
            element = element,
            duration = duration,
            until = Time.time + duration,
            strength = strength,
        };
        if (stats != null && ((combatStats != null && combatStats.Count > 0) || (resistances != null && resistances.Count > 0)))
            a.statsToken = stats.AddModifiers(a.name, combatStats, resistances, strength);
        if (traits != null && characterStats != null && characterStats.Count > 0)
            a.traitHandle = traits.ApplyModifiers(characterStats, strength, a.name);
        if (attachedVfx != null)
            a.vfx = Instantiate(attachedVfx, transform);
        active.Add(a);
        Changed?.Invoke();
        return a;
    }

    /// <summary>Removes the debuffs (cleanse). Returns how many.</summary>
    public int RemoveDebuffs() => RemoveAll(a => a.debuff);

    /// <summary>Removes the buffs (dispel). Returns how many.</summary>
    public int RemoveBuffs() => RemoveAll(a => !a.debuff);

    public int RemoveAll(Predicate<Active> match)
    {
        int n = 0;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (!match(active[i]))
                continue;
            Revert(active[i]);
            active.RemoveAt(i);
            n++;
        }
        if (n > 0)
            Changed?.Invoke();
        return n;
    }

    private void Revert(Active a)
    {
        if (a.statsToken != null && stats != null)
            stats.RemoveModifiers(a.statsToken);
        a.statsToken = null;
        if (a.traitHandle != null && traits != null)
            traits.RevertModifiers(a.traitHandle);
        a.traitHandle = null;
        if (a.vfx != null)
            Destroy(a.vfx);
        a.vfx = null;
    }

    private void CacheSystems()
    {
        if (stats == null)
            stats = CombatStats.For(this, addIfMissing: true);
        if (traits == null)
        {
            traits = GetComponentInParent<TraitManager>();
            if (traits == null)
                traits = GetComponentInChildren<TraitManager>();
        }
    }
}

/// <summary>
/// A timed buff or debuff: combat stats (Defense, Critical Chance, penetration, resistances...), character stats
/// (move speed, max health, damage taken...) for a while. The caster's Buff / Debuff Duration and Strength scale it;
/// a debuff is shortened by the target's Status Resistance and can be resisted through its chance (Status Chance).
/// </summary>
[Serializable, AbilityMenu("Buff/Stat Buff or Debuff", "Changes stats for a while: +20% attack speed, -30 Defense (armour break), -25% move speed, +15% crit... Removed exactly when it ends or is cleansed.", 0)]
public class StatModifierEffect : AbilityEffect
{
    [Tooltip("Name shown on status icons and in tooltips (e.g. Battle Cry, Armor Break, Weakened).")]
    public string label = "";
    [Tooltip("Buff or debuff (Auto: any lowered stat makes it a debuff). Debuffs count as harmful, are shortened by Status " +
             "Resistance and removed by cleanses.")]
    public StatBuffKind kind = StatBuffKind.Auto;
    [Tooltip("Seconds it lasts (before the caster's Buff / Debuff Duration and the ability's duration modifiers).")]
    [Min(0.1f)] public float duration = 8f;
    [Tooltip("Element of a debuff, for element-limited stats: a poison's weakness uses Poison (Debuff Strength vs Poison, Poison resistance).")]
    public ElementType element = ElementType.None;
    [Tooltip("Combat stats changed while it lasts.")]
    public List<CombatStatModifier> combatStats = new List<CombatStatModifier>();
    [Tooltip("Elemental resistances changed while it lasts.")]
    public List<ElementalResistance> resistances = new List<ElementalResistance>();
    [Tooltip("Character stats changed while it lasts (players: max health, move speed, damage taken, ability cooldowns...).")]
    public List<TraitModifier> characterStats = new List<TraitModifier>();
    [Tooltip("Applying it again: Refresh restarts it, Stack adds a copy (up to Max Stacks), Ignore keeps the running one.")]
    public EffectStacking stacking = EffectStacking.Refresh;
    [Min(1)] public int maxStacks = 3;
    [Tooltip("Optional effect attached to the character while it lasts.")]
    public GameObject attachedVfx;

    public StatModifierEffect()
    {
        onlyAffects = TargetFilter.Self | TargetFilter.Allies;
    }

    /// <summary>Is it a debuff? Auto: only when nothing in it helps (a berserk "+damage, -defense" counts as a buff).</summary>
    public bool IsDebuff
    {
        get
        {
            if (kind != StatBuffKind.Auto)
                return kind == StatBuffKind.Debuff;
            int good = 0, bad = 0;
            if (combatStats != null)
                foreach (CombatStatModifier m in combatStats)
                    if (m != null && m.value != 0f) { if ((m.value > 0f) != LowerIsBetter(m.stat)) good++; else bad++; }
            if (resistances != null)
                foreach (ElementalResistance r in resistances)
                    if (r != null && r.percent != 0f) { if (r.percent > 0f) good++; else bad++; }
            if (characterStats != null)
                foreach (TraitModifier m in characterStats)
                    if (m != null) { if (m.IsBeneficial) good++; else bad++; }
            return bad > 0 && good == 0;
        }
    }

    private static bool LowerIsBetter(CombatStatType s) => CombatStatInfo.LowerIsBetter(s);

    public override bool IsHarmful => IsDebuff;

    public override float ChanceFor(in EffectContext ctx)
    {
        if (!IsDebuff)
            return chance;
        CombatStats caster = ctx.caster != null ? ctx.caster.Stats : null;
        return Mathf.Clamp01(chance + (caster != null ? caster.StatusChanceBonus(element) : 0f));
    }

    public override void Apply(ref EffectContext ctx)
    {
        if (ctx.target == null)
            return;
        bool debuff = IsDebuff;
        float time = duration * ctx.stats.duration;
        float strength = 1f;
        CombatStats caster = ctx.caster != null ? ctx.caster.Stats : null;
        if (caster != null)
        {
            time *= debuff ? caster.DebuffDurationMultiplier(element) : caster.BuffDurationMultiplier;
            strength *= debuff ? caster.DebuffStrengthMultiplier(element) : caster.BuffStrengthMultiplier;
        }
        if (debuff && ctx.target.Stats != null)
            time *= ctx.target.Stats.StatusTakenMultiplier(element);
        if (time <= 0.05f)
            return;
        TimedStatModifiers.For(ctx.target).Apply(this, Label, ctx.caster, debuff, element, time, strength, combatStats, resistances, characterStats,
            stacking, maxStacks, attachedVfx);
    }

    private string Label => !string.IsNullOrEmpty(label) ? label : IsDebuff ? "Debuff" : "Buff";

    public override string Describe(in AbilityStats s)
    {
        var parts = new List<string>();
        if (combatStats != null) foreach (CombatStatModifier m in combatStats) if (m != null) parts.Add(m.Describe());
        if (resistances != null) foreach (ElementalResistance r in resistances) if (r != null) parts.Add(r.Describe());
        if (characterStats != null) foreach (TraitModifier m in characterStats) if (m != null) parts.Add(m.Describe());
        return $"{Label}: {string.Join(", ", parts)} for {duration * s.duration:0.#}s{Chance(chance)}";
    }

    public override void Validate(string owner, List<string> errors, List<string> warnings)
    {
        base.Validate(owner, errors, warnings);
        if ((combatStats == null || combatStats.Count == 0) && (resistances == null || resistances.Count == 0) && (characterStats == null || characterStats.Count == 0))
            warnings.Add($"{owner}: Stat Buff or Debuff changes nothing.");
        if (kind == StatBuffKind.Debuff && (onlyAffects & (TargetFilter.Enemies | TargetFilter.Neutral)) == 0)
            warnings.Add($"{owner}: a debuff that only affects self / allies (Only Affects) - intended?");
    }

    public override AbilityEffect Clone()
    {
        var c = (StatModifierEffect)base.Clone();
        c.combatStats = combatStats != null ? new List<CombatStatModifier>(combatStats) : new List<CombatStatModifier>();
        c.resistances = resistances != null ? new List<ElementalResistance>(resistances) : new List<ElementalResistance>();
        c.characterStats = characterStats != null ? new List<TraitModifier>(characterStats) : new List<TraitModifier>();
        return c;
    }
}

/// <summary>
/// Changes the hit mob's threat toward the caster: add threat (a taunt without forcing), multiply it, or drop it (a
/// rogue's vanish, a mage's fade).
/// </summary>
[Serializable, AbilityMenu("Support/Threat", "Raises or lowers how much the hit mobs want to attack the caster (taunt-lite, fade, vanish).", 4)]
public class ThreatEffect : AbilityEffect
{
    public enum Mode
    {
        /// <summary>Adds Amount threat (scaled by the caster's Threat stat).</summary>
        Add,
        /// <summary>Multiplies the caster's current threat (2 = doubles it, 0.5 = halves it).</summary>
        Multiply,
        /// <summary>Puts the caster at the top of the threat list (like a taunt, without forcing).</summary>
        Top,
    }

    public Mode mode = Mode.Add;
    [Tooltip("Add: threat added. Multiply: the factor.")]
    public float amount = 50f;

    public ThreatEffect()
    {
        onlyAffects = TargetFilter.Enemies;
    }

    public override bool IsHarmful => true;

    public override void Apply(ref EffectContext ctx)
    {
        CombatEntity mob = ctx.target, caster = ctx.caster;
        if (mob == null || caster == null || mob == caster)
            return;
        switch (mode)
        {
            case Mode.Add:
                mob.AddThreat(caster, Mathf.Max(0f, amount), ThreatKind.Other);
                break;
            case Mode.Multiply:
                float cur = mob.GetThreat(caster, float.MaxValue);
                float wanted = cur * Mathf.Max(0f, amount);
                if (wanted > cur) mob.AddThreat(caster, wanted - cur);
                else mob.ReduceThreat(caster, cur - wanted);
                break;
            case Mode.Top:
                mob.TauntThreat(caster);
                break;
        }
    }

    public override string Describe(in AbilityStats s) => mode == Mode.Add ? $"+{amount:0} threat" : mode == Mode.Multiply ? $"Threat ×{amount:0.##}" : "Becomes the top threat";
}
