using System;
using UnityEngine;

/// <summary>Everything about one instance of damage.</summary>
public struct DamageInfo
{
    /// <summary>Health removed (positive number, before the target's damage factor).</summary>
    public float amount;
    /// <summary>Who caused it (may be null for environmental damage).</summary>
    public CombatEntity source;
    /// <summary>Who received it.</summary>
    public CombatEntity target;
    /// <summary>The ability that caused it (null for weapons, falls, hazards).</summary>
    public AbilityDefinition ability;
    public Vector3 point;
    public Vector3 direction;
    public bool isCritical;
    /// <summary>Damage over time tick (does not interrupt casts or trigger hit reactions by itself).</summary>
    public bool isPeriodic;
    /// <summary>True when the attacker was inferred (no source was reported).</summary>
    public bool sourceInferred;
    /// <summary>Damage sent back by a reaction (Thorns): other reactions ignore it, so two of them cannot loop.</summary>
    public bool isReflected;
    /// <summary>How the damage is resisted: Physical by Defense, Magical by Magic Resistance, True by neither.</summary>
    public DamageType type;
    /// <summary>Element of the damage (None = plain). Elemental resistances apply to it.</summary>
    public ElementType element;
    /// <summary>The weapon that dealt it (null for abilities, hazards, falls...).</summary>
    public WeaponSO weapon;
}

/// <summary>A sound made in the world that AI can hear (footsteps, spells, fights).</summary>
public struct NoiseEvent
{
    public Vector3 position;
    /// <summary>How far away it can be heard (metres).</summary>
    public float radius;
    public CombatEntity source;
    /// <summary>0-1: how alarming it is (combat 1, footsteps 0.3).</summary>
    public float intensity;
}

/// <summary>
/// Global combat event hub. Systems publish here, and anything (AI, UI, audio, quests) can listen without the
/// publishers knowing about them. All events are raised on the main thread.
/// </summary>
public static class CombatEvents
{
    /// <summary>A cast started its wind-up (telegraphs are already registered as hazards).</summary>
    public static event Action<AbilityCastInstance> CastStarted;
    /// <summary>A cast released its actions.</summary>
    public static event Action<AbilityCastInstance> CastReleased;
    /// <summary>A cast was interrupted.</summary>
    public static event Action<AbilityCastInstance, CastInterruptReason> CastInterrupted;
    /// <summary>Someone took damage.</summary>
    public static event Action<DamageInfo> Damaged;
    /// <summary>Someone died (victim, killer - killer may be null).</summary>
    public static event Action<CombatEntity, CombatEntity> Killed;
    /// <summary>A noise was made.</summary>
    public static event Action<NoiseEvent> Noise;
    /// <summary>A melee swing started (player weapon attacks, mob melee abilities): used by AI to dodge.</summary>
    public static event Action<CombatEntity> MeleeSwingStarted;
    /// <summary>A mob asks nearby allies for help against a target (caller, target).</summary>
    public static event Action<CombatEntity, CombatEntity> HelpRequested;

    public static void RaiseCastStarted(AbilityCastInstance cast) => CastStarted?.Invoke(cast);
    public static void RaiseCastReleased(AbilityCastInstance cast) => CastReleased?.Invoke(cast);
    public static void RaiseCastInterrupted(AbilityCastInstance cast, CastInterruptReason reason) => CastInterrupted?.Invoke(cast, reason);
    public static void RaiseDamaged(in DamageInfo info) => Damaged?.Invoke(info);
    public static void RaiseKilled(CombatEntity victim, CombatEntity killer) => Killed?.Invoke(victim, killer);
    public static void RaiseMeleeSwing(CombatEntity attacker) => MeleeSwingStarted?.Invoke(attacker);
    public static void RaiseHelpRequested(CombatEntity caller, CombatEntity target) => HelpRequested?.Invoke(caller, target);

    /// <summary>Makes a noise AI within <paramref name="radius"/> can hear.</summary>
    public static void EmitNoise(Vector3 position, float radius, CombatEntity source, float intensity = 0.5f)
    {
        if (radius <= 0f)
            return;
        Noise?.Invoke(new NoiseEvent { position = position, radius = radius, source = source, intensity = Mathf.Clamp01(intensity) });
    }

    /// <summary>Clears every subscriber (domain reload off / tests).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics()
    {
        CastStarted = null;
        CastReleased = null;
        CastInterrupted = null;
        Damaged = null;
        Killed = null;
        Noise = null;
        MeleeSwingStarted = null;
        HelpRequested = null;
    }
}
