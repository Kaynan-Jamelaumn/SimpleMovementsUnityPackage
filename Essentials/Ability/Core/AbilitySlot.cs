using System;
using UnityEngine;

/// <summary>Where an ability slot's ability comes from (cost / cooldown reduction can count for one origin only).</summary>
public enum AbilitySlotSource
{
    /// <summary>The character's own abilities (ability keys, mob abilities).</summary>
    Skill,
    /// <summary>Granted by a trait, a race or a class (innate).</summary>
    Innate,
    /// <summary>Granted by an item (equipment, a weapon).</summary>
    Item,
}

/// <summary>
/// One ability a character can use, with its own modifiers, charges and cooldown. The phase follows the player's
/// ability states: Ready → Casting → Launching → Active → InCooldown → Ready.
/// </summary>
[Serializable]
public class AbilitySlot
{
    [Tooltip("The ability in this slot. Empty = unused slot.")]
    public AbilityDefinition ability;

    [Tooltip("Changes applied on top of the ability (an absorbed variant, upgrades, elite bonuses). The ability asset is never modified.")]
    public AbilityModifierSet modifiers = new AbilityModifierSet();

    [Tooltip("Untick to disable this slot without removing the ability.")]
    public bool enabled = true;

    [Tooltip("Mobs only: multiplies how often the AI picks this ability (0 = never automatically, 2 = twice as often).")]
    [Min(0f)] public float aiWeight = 1f;

    [Tooltip("Optional name override shown in the UI (e.g. 'Absorbed: Lesser Firebolt'). Empty = the ability's name.")]
    public string label = "";

    // ------------------------------------------------------------------ runtime state
    [NonSerialized] private AbilityPhase phase = AbilityPhase.Ready;
    [NonSerialized] internal float phaseStart;
    [NonSerialized] internal float phaseEnd;
    [NonSerialized] internal int charges = -1;
    [NonSerialized] internal float rechargeEnd;
    [NonSerialized] internal float lockedUntil;
    [NonSerialized] internal AbilityCastInstance cast;
    [NonSerialized] internal float lastUseTime = -999f;
    [NonSerialized] internal float aiReadyTime;
    [NonSerialized] private AbilityStats stats;
    [NonSerialized] private bool statsValid;
    [NonSerialized] private AbilityModifierSet statsSource;
    [NonSerialized] private int statsOwnerVersion = -1;

    /// <summary>Position of this slot in its caster.</summary>
    [NonSerialized] public int Index = -1;
    /// <summary>The caster that owns this slot.</summary>
    [NonSerialized] public AbilityCaster Owner;
    /// <summary>What the slot was granted from (absorbed ability data), if anything.</summary>
    [NonSerialized] public AbilityGrant Grant;
    /// <summary>Where the ability comes from: the character's own skills, a trait / race / class, or an item (scoped stats).</summary>
    [NonSerialized] public AbilitySlotSource Source = AbilitySlotSource.Skill;

    /// <summary>Raised when the phase changes (slot, previous, new).</summary>
    public event Action<AbilitySlot, AbilityPhase, AbilityPhase> PhaseChanged;

    public AbilitySlot() { }

    public AbilitySlot(AbilityDefinition ability, AbilityModifierSet modifiers = null)
    {
        this.ability = ability;
        this.modifiers = modifiers != null ? modifiers.Clone() : new AbilityModifierSet();
    }

    public AbilityPhase Phase => phase;
    public bool IsEmpty => ability == null;
    public AbilityCastInstance CurrentCast => cast;

    /// <summary>Resolved multipliers of this slot (cached; call <see cref="InvalidateStats"/> after editing modifiers).</summary>
    public AbilityStats Stats
    {
        get
        {
            int ownerVersion = Owner != null ? Owner.CharacterModifiersVersion : 0;
            if (!statsValid || !ReferenceEquals(statsSource, modifiers) || statsOwnerVersion != ownerVersion)
            {
                // Slot modifiers, then the character's own (traits, buffs).
                stats = AbilityStats.From(modifiers, Owner != null ? Owner.CharacterModifiers : null);
                Owner?.ApplyLayers(ref stats, this);
                statsSource = modifiers;
                statsOwnerVersion = ownerVersion;
                statsValid = true;
            }
            return stats;
        }
    }

    public void InvalidateStats() => statsValid = false;

    public int MaxCharges => ability != null ? ability.MaxCharges(Stats) : 1;
    public int Charges => charges < 0 ? MaxCharges : charges;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(label))
                return label;
            if (ability == null)
                return "(empty)";
            return modifiers != null && !string.IsNullOrEmpty(modifiers.label) ? $"{ability.DisplayName} ({modifiers.label})" : ability.DisplayName;
        }
    }

    public float Cooldown => ability != null ? ability.Cooldown(Stats) : 0f;

    /// <summary>Seconds until the slot can be used again (0 when ready).</summary>
    public float CooldownRemaining
    {
        get
        {
            float now = Time.time;
            float r = Mathf.Max(0f, lockedUntil - now);
            if (Charges <= 0 && rechargeEnd > 0f)
                r = Mathf.Max(r, rechargeEnd - now);
            return r;
        }
    }

    /// <summary>0 = ready, 1 = just started cooling down (for UI fill).</summary>
    public float CooldownFraction
    {
        get
        {
            float cd = Cooldown;
            if (cd <= 0f)
                return 0f;
            return Mathf.Clamp01(CooldownRemaining / cd);
        }
    }

    /// <summary>Seconds in the current phase.</summary>
    public float PhaseElapsed => Time.time - phaseStart;

    /// <summary>Seconds left in the current timed phase.</summary>
    public float PhaseRemaining => Mathf.Max(0f, phaseEnd - Time.time);

    /// <summary>0-1 progress of the current timed phase.</summary>
    public float PhaseProgress => phaseEnd > phaseStart ? Mathf.Clamp01((Time.time - phaseStart) / (phaseEnd - phaseStart)) : 1f;

    /// <summary>True when the slot could start a cast (ignores the caster's own state).</summary>
    public bool IsReady => ability != null && enabled && phase == AbilityPhase.Ready && Charges > 0 && Time.time >= lockedUntil;

    public float LastUseTime => lastUseTime;

    /// <summary>Replaces the ability and modifiers (resets cooldown and charges).</summary>
    public void Set(AbilityDefinition newAbility, AbilityModifierSet newModifiers)
    {
        ability = newAbility;
        modifiers = newModifiers != null ? newModifiers.Clone() : new AbilityModifierSet();
        statsValid = false;
        charges = -1;
        rechargeEnd = 0f;
        lockedUntil = 0f;
        aiReadyTime = 0f;
        cast = null;
        SetPhase(AbilityPhase.Ready, 0f);
    }

    /// <summary>Makes the slot ready immediately (all charges back).</summary>
    public void ResetCooldown()
    {
        charges = MaxCharges;
        rechargeEnd = 0f;
        lockedUntil = 0f;
        aiReadyTime = 0f;
        if (phase == AbilityPhase.InCooldown)
            SetPhase(AbilityPhase.Ready, 0f);
    }

    /// <summary>
    /// Shortens the current cooldown and the recharge of the next charge by <paramref name="seconds"/>. The phase
    /// returns to Ready on the caster's next update when the slot can be used again.
    /// </summary>
    public void ReduceCooldown(float seconds)
    {
        if (seconds <= 0f)
            return;
        float now = Time.time;
        if (lockedUntil > now)
            lockedUntil = Mathf.Max(now, lockedUntil - seconds);
        if (rechargeEnd > 0f)
            rechargeEnd = Mathf.Max(now, rechargeEnd - seconds);
        TickCharges(now);
    }

    internal void SetPhase(AbilityPhase newPhase, float duration)
    {
        AbilityPhase old = phase;
        phase = newPhase;
        phaseStart = Time.time;
        phaseEnd = Time.time + Mathf.Max(0f, duration);
        if (old != newPhase)
            PhaseChanged?.Invoke(this, old, newPhase);
    }

    /// <summary>Recharges charges over time. Called every frame by the caster.</summary>
    internal void TickCharges(float now)
    {
        int max = MaxCharges;
        if (charges < 0)
            charges = max;
        if (charges >= max)
        {
            charges = max;
            rechargeEnd = 0f;
            return;
        }
        if (rechargeEnd <= 0f)
        {
            // A charge is missing but no timer runs: a modifier raised the maximum (e.g. an "Ability Charges" trait).
            // Start refilling now, unless a cast is running (it starts the timer when it finishes).
            if (phase == AbilityPhase.Ready || phase == AbilityPhase.InCooldown)
                rechargeEnd = now + Mathf.Max(0.01f, Cooldown);
            return;
        }
        while (charges < max && now >= rechargeEnd)
        {
            charges++;
            rechargeEnd = charges < max ? rechargeEnd + Mathf.Max(0.01f, Cooldown) : 0f;
        }
    }

    /// <summary>Starts the recharge timer if a charge is missing and no timer runs.</summary>
    internal void StartRecharge(float now)
    {
        if (Charges < MaxCharges && rechargeEnd <= 0f)
            rechargeEnd = now + Mathf.Max(0.01f, Cooldown);
    }

    public override string ToString() => $"{DisplayName} [{phase}]";
}
