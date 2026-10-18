using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a condition does to the character while it lasts (tired, cold, losing their mind...): a status icon with combat
/// and character stats, health / stamina / sanity per second, faster or slower hunger and thirst, and a chance to faint.
/// Everything is optional and scaled by the condition's strength. Used by the sleep, sanity and body heat systems, so
/// all of their consequences are edited the same way in the inspector.
/// </summary>
[Serializable]
public class StatusConsequence
{
    [Tooltip("Name on the status icon (Tired, Freezing, Paranoid...). Empty = no icon (only the per-second effects).")]
    public string label = "Condition";
    [Tooltip("Shown as a debuff (red). Off = a buff.")]
    public bool debuff = true;
    [Tooltip("Element of the status (icon colour, element-limited stats).")]
    public ElementType element = ElementType.None;

    [Header("Stats while it lasts")]
    [Tooltip("Combat stats changed while it lasts (Weapon Damage, Critical Chance, Defense...).")]
    public List<CombatStatModifier> combatStats = new List<CombatStatModifier>();
    [Tooltip("Character stats changed while it lasts (Move Speed, Stamina Regen, Health Regen, Damage Taken, Ability Cast Time...).")]
    public List<TraitModifier> characterStats = new List<TraitModifier>();

    [Header("Per second")]
    [Tooltip("Health per second: negative = loss (True damage that can knock out or kill), positive = regeneration.")]
    public float healthPerSecond;
    [Tooltip("Stamina per second (negative = drain).")]
    public float staminaPerSecond;
    [Tooltip("Sanity per second (negative = loss, reduced by Sanity Resistance).")]
    public float sanityPerSecond;

    [Header("Needs")]
    [Tooltip("Hunger drains this many times as fast (1 = normal, 1.5 = 50% faster, 0.5 = half).")]
    [Min(0f)] public float hungerDrain = 1f;
    [Tooltip("Thirst drains this many times as fast.")]
    [Min(0f)] public float thirstDrain = 1f;

    [Header("Fainting")]
    [Tooltip("Chance per minute that the character is stunned for a moment (fainting, a panic attack, shivering). 0 = never.")]
    [Range(0f, 1f)] public float stunChancePerMinute;
    [Tooltip("Seconds of each stun.")]
    [Min(0f)] public float stunSeconds = 1.5f;

    public StatusConsequence() { }

    public StatusConsequence(string label, bool debuff = true)
    {
        this.label = label;
        this.debuff = debuff;
    }

    /// <summary>Does it do anything at all?</summary>
    public bool HasAnyEffect =>
        !string.IsNullOrEmpty(label) || (combatStats != null && combatStats.Count > 0) || (characterStats != null && characterStats.Count > 0) ||
        healthPerSecond != 0f || staminaPerSecond != 0f || sanityPerSecond != 0f ||
        !Mathf.Approximately(hungerDrain, 1f) || !Mathf.Approximately(thirstDrain, 1f) || stunChancePerMinute > 0f;

    /// <summary>"-10% Move Speed, -2 health/s" (tooltips, inspectors).</summary>
    public string Describe(float strength = 1f)
    {
        var parts = new List<string>();
        if (combatStats != null) foreach (CombatStatModifier m in combatStats) if (m != null) parts.Add(m.Describe(strength));
        if (characterStats != null) foreach (TraitModifier m in characterStats) if (m != null) parts.Add(m.Describe(strength));
        if (healthPerSecond != 0f) parts.Add($"{healthPerSecond * strength:+0.##;-0.##} health/s");
        if (staminaPerSecond != 0f) parts.Add($"{staminaPerSecond * strength:+0.##;-0.##} stamina/s");
        if (sanityPerSecond != 0f) parts.Add($"{sanityPerSecond * strength:+0.##;-0.##} sanity/s");
        if (!Mathf.Approximately(hungerDrain, 1f)) parts.Add($"hunger x{Mathf.Lerp(1f, hungerDrain, strength):0.##}");
        if (!Mathf.Approximately(thirstDrain, 1f)) parts.Add($"thirst x{Mathf.Lerp(1f, thirstDrain, strength):0.##}");
        if (stunChancePerMinute > 0f) parts.Add($"{stunChancePerMinute * strength * 100f:0}%/min to faint {stunSeconds:0.#} s");
        return parts.Count > 0 ? string.Join(", ", parts) : "no effect";
    }

    public StatusConsequence Clone()
    {
        var c = (StatusConsequence)MemberwiseClone();
        c.combatStats = new List<CombatStatModifier>(combatStats ?? new List<CombatStatModifier>());
        c.characterStats = new List<TraitModifier>(characterStats ?? new List<TraitModifier>());
        return c;
    }
}

/// <summary>
/// Runs one <see cref="StatusConsequence"/> at a time on a character (the current stage of a condition): shows its status
/// icon and stats through <see cref="TimedStatModifiers"/> (removed exactly when it changes or ends), applies the
/// per-second effects in <see cref="Tick"/>, and sets the hunger / thirst drain multipliers. Owned by a status manager.
/// Only where the game rules run (offline, server, host).
/// </summary>
public sealed class ConsequenceRunner
{
    private const float HoldSeconds = 6f;

    private readonly Component owner;
    private readonly object key;
    private StatusConsequence current;
    private float strength;
    private TimedStatModifiers.Active handle;
    private float nextRenew;
    private CombatEntity entity;
    private PlayerStatusController status;

    /// <param name="owner">The status manager running it (finds the character).</param>
    /// <param name="key">Identifies its status and drain multipliers (one key per system).</param>
    public ConsequenceRunner(Component owner, object key)
    {
        this.owner = owner;
        this.key = key;
    }

    public StatusConsequence Current => current;
    public float Strength => strength;

    private CombatEntity Entity
    {
        get
        {
            if (entity == null && owner != null)
                entity = owner.GetComponentInParent<CombatEntity>() ?? owner.GetComponentInChildren<CombatEntity>();
            return entity;
        }
    }

    private PlayerStatusController Status
    {
        get
        {
            if (status == null && owner != null)
                status = owner.GetComponentInParent<PlayerStatusController>() ?? owner.GetComponentInChildren<PlayerStatusController>();
            return status;
        }
    }

    /// <summary>Makes <paramref name="c"/> the running consequence at <paramref name="newStrength"/> (null = none).</summary>
    public void Set(StatusConsequence c, float newStrength = 1f)
    {
        newStrength = Mathf.Clamp(newStrength, 0f, 5f);
        if (c != null && !c.HasAnyEffect)
            c = null;
        if (ReferenceEquals(c, current) && Mathf.Abs(newStrength - strength) < 0.1f)
            return;
        Clear();
        current = c;
        strength = newStrength;
        if (current == null)
            return;
        ApplyStatus();
        PlayerStatusController s = Status;
        if (s != null)
        {
            if (s.HungerManager != null) s.HungerManager.SetRateMultiplier(key, Mathf.Lerp(1f, current.hungerDrain, Mathf.Min(1f, strength)));
            if (s.ThirstManager != null) s.ThirstManager.SetRateMultiplier(key, Mathf.Lerp(1f, current.thirstDrain, Mathf.Min(1f, strength)));
        }
    }

    /// <summary>Ends the running consequence (stats and drain multipliers removed).</summary>
    public void Clear()
    {
        if (handle != null)
        {
            TimedStatModifiers mods = Entity != null ? Entity.GetComponent<TimedStatModifiers>() : null;
            TimedStatModifiers.Active h = handle;
            mods?.RemoveAll(a => ReferenceEquals(a, h));
            handle = null;
        }
        PlayerStatusController s = Status;
        if (s != null)
        {
            s.HungerManager?.ClearRateMultiplier(key);
            s.ThirstManager?.ClearRateMultiplier(key);
        }
        current = null;
        strength = 0f;
    }

    private void ApplyStatus()
    {
        CombatEntity e = Entity;
        if (e == null || current == null || string.IsNullOrEmpty(current.label) && !HasStats(current))
            return;
        TimedStatModifiers mods = TimedStatModifiers.For(e);
        if (mods == null)
            return;
        handle = mods.Apply(key, current.label, null, current.debuff, current.element, HoldSeconds, strength,
            current.combatStats, null, current.characterStats, EffectStacking.Refresh);
        nextRenew = Time.time + 1f;
    }

    private static bool HasStats(StatusConsequence c) =>
        (c.combatStats != null && c.combatStats.Count > 0) || (c.characterStats != null && c.characterStats.Count > 0);

    /// <summary>Keeps the status alive and applies the per-second effects. Call every frame.</summary>
    public void Tick(float dt)
    {
        if (current == null)
            return;
        CombatEntity e = Entity;
        if (e == null || e.IsDead)
            return;

        // Keep the status icon / stats: renew it, and put it back if a death, revive or cleanse removed it.
        if (Time.time >= nextRenew)
        {
            nextRenew = Time.time + 1f;
            TimedStatModifiers mods = e.GetComponent<TimedStatModifiers>();
            bool alive = false;
            if (handle != null && mods != null)
                foreach (TimedStatModifiers.Active a in mods.All)
                    if (ReferenceEquals(a, handle)) { alive = true; break; }
            if (alive)
                handle.until = Time.time + HoldSeconds;
            else
            {
                handle = null;
                ApplyStatus();
            }
        }

        float k = strength * dt;
        if (current.healthPerSecond < 0f && !e.IsUnconscious)
            StatusDamage.Apply(e, -current.healthPerSecond * k, current.element);
        else if (current.healthPerSecond > 0f && !e.IsUnconscious)
            e.ApplyHeal(current.healthPerSecond * k);

        PlayerStatusController s = Status;
        if (s != null)
        {
            if (current.staminaPerSecond != 0f && s.StaminaManager != null)
            {
                if (current.staminaPerSecond > 0f) s.StaminaManager.AddCurrentValue(current.staminaPerSecond * k);
                else s.StaminaManager.SetCurrentValue(s.StaminaManager.CurrentValue + current.staminaPerSecond * k);
            }
            if (current.sanityPerSecond != 0f && s.SanityManager != null)
                s.SanityManager.ChangeSanity(current.sanityPerSecond * k);
        }

        if (current.stunChancePerMinute > 0f && current.stunSeconds > 0f && !e.IsUnconscious)
        {
            float perSecond = 1f - Mathf.Pow(1f - Mathf.Clamp01(current.stunChancePerMinute * strength), 1f / 60f);
            if (UnityEngine.Random.value < perSecond * dt)
                e.ApplyControl(ControlType.Stun, current.stunSeconds, null);
        }
    }
}
