using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What happens when the key is let go before the minimum charge time.</summary>
public enum ChargeBelowMin
{
    /// <summary>The ability is cast without any charge (a tap casts it normally).</summary>
    CastUncharged = 0,
    /// <summary>Nothing is cast (the player changed their mind).</summary>
    Cancel = 1,
}

/// <summary>
/// How an ability is charged by holding its key: the longest it can be held, what a full charge adds, and what happens on a short tap. Press
/// the key to begin, hold to charge, let go to cast (or the cast goes off at the maximum when Auto Release At Max is on). The numbers are
/// multipliers a FULL charge reaches; a partial charge is in between along <see cref="curve"/>. Used by <see cref="AbilityCharger"/> for the
/// abilities a player lists, and by other systems (Rune Magic's spells) through <see cref="IChargeSource"/>.
/// </summary>
[Serializable]
public class ChargeProfile
{
    [Tooltip("Off: the ability is cast as usual.")]
    public bool enabled = true;
    [Tooltip("Seconds the key must be held to count as charging; a shorter tap follows Below Minimum.")]
    [Min(0f)] public float minSeconds = 0.35f;
    [Tooltip("Seconds of holding that give a full charge.")]
    [Min(0.1f)] public float maxSeconds = 2.5f;
    [Tooltip("Shape of the charge: 1 = steady, above 1 = slow at first and fast at the end, below 1 = fast at first.")]
    [Min(0.1f)] public float curve = 1f;
    [Tooltip("What a short tap does.")]
    public ChargeBelowMin belowMinimum = ChargeBelowMin.CastUncharged;
    [Tooltip("The cast goes off by itself once the maximum is reached. Off: it waits for the key to be let go.")]
    public bool autoReleaseAtMax = true;
    [Tooltip("Taking damage cancels the charge (nothing is cast).")]
    public bool cancelOnDamage = false;

    [Header("What a full charge multiplies")]
    [Min(0f)] public float damageAtFull = 2f;
    [Min(0f)] public float healAtFull = 2f;
    [Min(0f)] public float controlAtFull = 1.5f;
    [Min(0f)] public float rangeAtFull = 1.25f;
    [Min(0f)] public float areaAtFull = 1.25f;
    [Min(0f)] public float durationAtFull = 1.25f;
    [Tooltip("Mana, stamina and health costs (and a spell's Rune Energy).")]
    [Min(0f)] public float costAtFull = 1.5f;

    /// <summary>A copy (profiles are shared data: a system that changes one works on its own copy).</summary>
    public ChargeProfile Clone() => (ChargeProfile)MemberwiseClone();

    /// <summary>
    /// How charged a key held for <paramref name="seconds"/> is, 0 to 1: nothing before the minimum, then along the curve up to the maximum.
    /// </summary>
    public float Fraction(float seconds)
    {
        if (float.IsNaN(seconds) || seconds < minSeconds) return 0f;
        float span = Mathf.Max(0.0001f, maxSeconds - minSeconds);
        float t = Mathf.Clamp01((seconds - minSeconds) / span);
        return Mathf.Clamp01(Mathf.Pow(t, Mathf.Max(0.1f, curve)));
    }

    /// <summary>Seconds of holding that reach <paramref name="fraction"/> (the inverse of <see cref="Fraction"/>).</summary>
    public float SecondsFor(float fraction)
    {
        float f = Mathf.Clamp01(fraction);
        if (f <= 0f) return minSeconds;
        float t = Mathf.Pow(f, 1f / Mathf.Max(0.1f, curve));
        return minSeconds + t * Mathf.Max(0.0001f, maxSeconds - minSeconds);
    }

    /// <summary>The value a charge of <paramref name="fraction"/> reaches when a full charge multiplies by <paramref name="atFull"/> (1 at no charge).</summary>
    public static float Scale(float atFull, float fraction) => 1f + (Mathf.Max(0f, atFull) - 1f) * Mathf.Clamp01(fraction);

    /// <summary>The multipliers of a charge of <paramref name="fraction"/>, as modifiers the ability system applies to the cast.</summary>
    public AbilityModifierSet ToModifiers(float fraction)
    {
        float f = Mathf.Clamp01(fraction);
        return new AbilityModifierSet
        {
            label = "Charged",
            damageMultiplier = Scale(damageAtFull, f),
            healMultiplier = Scale(healAtFull, f),
            controlMultiplier = Scale(controlAtFull, f),
            rangeMultiplier = Scale(rangeAtFull, f),
            areaMultiplier = Scale(areaAtFull, f),
            durationMultiplier = Scale(durationAtFull, f),
            costMultiplier = Scale(costAtFull, f),
        };
    }

    /// <summary>Adds the profile's problems.</summary>
    public void Validate(string where, List<string> errors, List<string> warnings)
    {
        if (!enabled) return;
        if (maxSeconds <= minSeconds) errors?.Add($"{where}: Max Seconds must be above Min Seconds.");
        if (maxSeconds > 10f) warnings?.Add($"{where}: a charge of more than 10 seconds is a long time to hold a key.");
        if (damageAtFull < 1f) warnings?.Add($"{where}: a full charge does less damage than none.");
    }
}

/// <summary>A system that decides which abilities can be charged (and with what profile) and may refuse a charge it cannot pay for.</summary>
public interface IChargeSource
{
    /// <summary>The profile of the ability in <paramref name="slot"/> of <paramref name="caster"/>, or false when this source does not charge it.</summary>
    bool TryGetProfile(AbilityCaster caster, AbilitySlot slot, out ChargeProfile profile);

    /// <summary>Can the caster pay for a charge of <paramref name="fraction"/> now? (A system with its own costs says no when it cannot; the cast then goes off uncharged.)</summary>
    bool CanAfford(AbilityCaster caster, AbilitySlot slot, ChargeProfile profile, float fraction);
}

/// <summary>The sources of charge profiles (every system registers one) and how a charged cast is worked out.</summary>
public static class AbilityCharging
{
    private static readonly List<IChargeSource> sources = new List<IChargeSource>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => sources.Clear();

    /// <summary>Registers a source (once; asking again is harmless).</summary>
    public static void Register(IChargeSource source)
    {
        if (source != null && !sources.Contains(source)) sources.Add(source);
    }

    public static void Unregister(IChargeSource source) => sources.Remove(source);

    /// <summary>The first source's profile for the ability in the slot (null: it cannot be charged).</summary>
    public static ChargeProfile ProfileOf(AbilityCaster caster, AbilitySlot slot)
    {
        if (slot == null || slot.ability == null) return null;
        for (int i = 0; i < sources.Count; i++)
        {
            IChargeSource s = sources[i];
            if (s != null && s.TryGetProfile(caster, slot, out ChargeProfile p) && p != null && p.enabled) return p;
        }
        return null;
    }

    /// <summary>
    /// The charge a cast really gets: the one asked for (clamped to 0-1), or 0 when the slot cannot be charged or its sources cannot pay for it.
    /// Where the rules run.
    /// </summary>
    public static float Resolve(AbilityCaster caster, AbilitySlot slot, float requested, out ChargeProfile profile)
    {
        profile = null;
        if (float.IsNaN(requested) || requested <= 0f) return 0f;
        float f = Mathf.Clamp01(requested);
        profile = ProfileOf(caster, slot);
        if (profile == null) return 0f;
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null && !sources[i].CanAfford(caster, slot, profile, f)) { profile = null; return 0f; }
        return f;
    }

    /// <summary>The slot's stats with a charge of <paramref name="fraction"/> on top.</summary>
    public static AbilityStats Apply(in AbilityStats stats, ChargeProfile profile, float fraction)
    {
        AbilityStats s = stats;
        if (profile != null && fraction > 0f) s.Apply(profile.ToModifiers(fraction));
        return s;
    }
}
