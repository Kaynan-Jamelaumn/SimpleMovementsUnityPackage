using System;
using UnityEngine;

/// <summary>What made a noise. Listeners may treat kinds differently (the hearing HUD names and colours them).</summary>
public enum NoiseKind
{
    /// <summary>Anything else (older code that names no kind).</summary>
    Other,
    /// <summary>Walking, running, sneaking.</summary>
    Footstep,
    /// <summary>The push-off of a jump.</summary>
    Jump,
    /// <summary>Landing from a jump or a fall (louder the faster the fall).</summary>
    Landing,
    /// <summary>A dash or a roll.</summary>
    Dodge,
    /// <summary>Swimming.</summary>
    Swim,
    /// <summary>A weapon swing or a shot.</summary>
    Attack,
    /// <summary>A blow landing on a body.</summary>
    Impact,
    /// <summary>An ability being cast or going off.</summary>
    Ability,
    /// <summary>An explosion.</summary>
    Explosion,
    /// <summary>Using something: a chest, a lever, a door, a smashed barrel.</summary>
    Interaction,
    /// <summary>A shout, a whistle, a roar (see <see cref="CombatEntity.MakeNoise"/>).</summary>
    Voice,
    /// <summary>A death cry.</summary>
    Death,
    /// <summary>A thing in the world: a bell, an alarm, a thrown bottle (see NoiseEmitter).</summary>
    Object,
}

/// <summary>
/// How loud things are and how sound carries (Combat Settings ▸ Noise). Every radius is how far the noise is heard in
/// the open by a listener with normal hearing: a mob multiplies it by its profile's Hearing Multiplier (0 = deaf), a
/// player by Player Hearing; the Hearing stat adds to both and the Noise Reduction stat makes a character quieter.
/// </summary>
[Serializable]
public class CombatNoiseSettings
{
    [Tooltip("Multiplies how far every noise carries (1 = the distances below and the abilities' Noise Radius).")]
    [Range(0f, 3f)] public float loudness = 1f;

    [Header("Moving (metres the noise carries)")]
    [Tooltip("Sneaking (crouched and moving).")]
    [Min(0f)] public float crouchRadius = 2.5f;
    [Tooltip("Walking.")]
    [Min(0f)] public float walkRadius = 7f;
    [Tooltip("Running (sprinting).")]
    [Min(0f)] public float runRadius = 14f;
    [Tooltip("Swimming.")]
    [Min(0f)] public float swimRadius = 8f;
    [Tooltip("The push-off of a jump.")]
    [Min(0f)] public float jumpRadius = 8f;
    [Tooltip("Landing: X after a short drop, Y after a long fall (between them by how fast the character was falling).")]
    public Vector2 landingRadius = new Vector2(5f, 18f);
    [Tooltip("Falling speed (m/s) of the quietest landing that makes a noise (X) and of the loudest (Y).")]
    public Vector2 landingFallSpeed = new Vector2(4f, 16f);
    [Tooltip("A dash.")]
    [Min(0f)] public float dashRadius = 11f;
    [Tooltip("A roll.")]
    [Min(0f)] public float rollRadius = 8f;
    [Tooltip("Seconds between two footstep noises of a moving character.")]
    [Range(0.1f, 1f)] public float footstepInterval = 0.35f;

    [Header("Acting (metres the noise carries)")]
    [Tooltip("A weapon swing.")]
    [Min(0f)] public float meleeSwingRadius = 10f;
    [Tooltip("A shot (bows, crossbows, firearms).")]
    [Min(0f)] public float rangedShotRadius = 18f;
    [Tooltip("A blow landing on a body: heard around the one who was hit, blamed on the attacker. 0 = silent.")]
    [Min(0f)] public float hitRadius = 10f;
    [Tooltip("A death cry: allies of the dead come and look. 0 = silent.")]
    [Min(0f)] public float deathRadius = 12f;
    [Tooltip("Using something (a chest, a shrine) when the object sets no noise of its own.")]
    [Min(0f)] public float interactRadius = 5f;

    [Header("How sound carries")]
    [Tooltip("Share of its distance a noise still carries when a wall stands between it and the listener (1 = walls do nothing, " +
             "0.5 = half as far). Checked with one ray on the Obstacle Layers.")]
    [Range(0.1f, 1f)] public float wallMuffling = 0.6f;
    [Tooltip("Closer than this, walls don't muffle (a noise on the other side of a thin wall is heard all the same).")]
    [Min(0f)] public float muffleFreeDistance = 2.5f;

    [Header("What players hear")]
    [Tooltip("Players hear noises too: the local player's screen shows an arc in the direction of each noise made by someone " +
             "it can't see (other players, mobs, things). Adds a Player Hearing component to players that have none.")]
    public bool playerHearing = true;
    [Tooltip("Multiplies how far players hear (1 = as far as a mob with Hearing Multiplier 1).")]
    [Range(0f, 4f)] public float playerHearingMultiplier = 1f;
    [Tooltip("Also show the noises of the player's own party members and summons.")]
    public bool hearAllies = false;
    [Tooltip("Don't show a noise whose maker is on screen in plain sight (the player sees it anyway).")]
    public bool hideSeenSources = true;
    [Tooltip("Colour of the arc for noises made by other players.")]
    public Color playerNoiseColor = new Color(1f, 0.95f, 0.8f, 0.9f);
    [Tooltip("Colour of the arc for noises made by mobs.")]
    public Color mobNoiseColor = new Color(1f, 0.45f, 0.25f, 0.9f);
    [Tooltip("Colour of the arc for noises made by things (bells, explosions, traps).")]
    public Color objectNoiseColor = new Color(0.6f, 0.8f, 1f, 0.85f);

    /// <summary>Landing radius and how alarming it is (0-1) for a fall that ended at <paramref name="fallSpeed"/> m/s. 0 radius = too soft to hear.</summary>
    public float LandingRadius(float fallSpeed, out float intensity)
    {
        intensity = 0f;
        float soft = Mathf.Min(landingFallSpeed.x, landingFallSpeed.y), hard = Mathf.Max(landingFallSpeed.x, landingFallSpeed.y);
        if (fallSpeed < soft)
            return 0f;
        float t = hard - soft > 0.01f ? Mathf.Clamp01((fallSpeed - soft) / (hard - soft)) : 1f;
        intensity = Mathf.Lerp(0.35f, 0.8f, t);
        return Mathf.Lerp(landingRadius.x, landingRadius.y, t);
    }
}

/// <summary>
/// The rules of noise shared by everything that listens (mob AI, the players' hearing): how far a noise carries to a
/// listener - its radius, the listener's hearing, walls in between - and the noises of a fight (blows, deaths).
/// Noises are made with <see cref="CombatEvents.EmitNoise"/> or <see cref="CombatEntity.MakeNoise"/>.
/// </summary>
public static class CombatNoise
{
    /// <summary>Is the noise made by the character's own body (the Noise Reduction stat makes it quieter)?</summary>
    public static bool MadeByBody(NoiseKind kind)
    {
        switch (kind)
        {
            case NoiseKind.Footstep:
            case NoiseKind.Jump:
            case NoiseKind.Landing:
            case NoiseKind.Dodge:
            case NoiseKind.Swim:
            case NoiseKind.Attack:
            case NoiseKind.Interaction:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// How far <paramref name="radius"/> carries for a listener <paramref name="distance"/> away with
    /// <paramref name="hearing"/> (1 = normal, 0 = deaf), <paramref name="muffled"/> when a wall is in between.
    /// </summary>
    public static float Range(float radius, float hearing, bool muffled, float distance, CombatNoiseSettings settings)
    {
        float range = Mathf.Max(0f, radius) * Mathf.Max(0f, hearing);
        if (muffled && settings != null && distance > settings.muffleFreeDistance)
            range *= Mathf.Clamp(settings.wallMuffling, 0.1f, 1f);
        return range;
    }

    /// <summary>
    /// Does the noise reach a listener whose ears are at <paramref name="ear"/>? <paramref name="loudness"/> is how
    /// loud it arrives: 1 next to it, 0 at the edge of where it can be heard. One ray is cast (walls muffle), and only
    /// for a noise that would be heard in the open.
    /// </summary>
    public static bool Reaches(in NoiseEvent noise, Vector3 ear, float hearing, out float distance, out float loudness)
    {
        loudness = 0f;
        distance = Vector3.Distance(noise.position, ear);
        CombatNoiseSettings settings = CombatSettings.Instance.noise;
        float open = Range(noise.radius, hearing, false, distance, settings);
        if (open <= 0f || distance > open)
            return false;
        float range = open;
        if (settings.wallMuffling < 0.999f && distance > settings.muffleFreeDistance &&
            !CombatQuery.HasLineOfSight(ear, noise.position + Vector3.up))
        {
            range = Range(noise.radius, hearing, true, distance, settings);
            if (distance > range)
                return false;
        }
        loudness = range > 0.001f ? Mathf.Clamp01(1f - distance / range) : 1f;
        return true;
    }

    /// <summary>How far a character with the Hearing stat hears, as a multiplier (1 = no bonus).</summary>
    public static float HearingStat(CombatEntity listener)
    {
        if (listener == null || listener.Stats == null)
            return 1f;
        return Mathf.Max(0f, 1f + listener.Stats.Get(CombatStatType.Hearing) / 100f);
    }

    /// <summary>How loud a character is, as a multiplier of its body's noises (1 = normal, 0 = silent): the Noise Reduction stat.</summary>
    public static float LoudnessOf(CombatEntity source)
    {
        if (source == null || source.Stats == null)
            return 1f;
        return Mathf.Max(0f, 1f - source.Stats.Get(CombatStatType.NoiseReduction) / 100f);
    }

    /// <summary>A hit landed (called for every damage event): the blow is heard around the victim, blamed on the attacker.</summary>
    internal static void OnDamaged(in DamageInfo info)
    {
        CombatEntity victim = info.target;
        if (victim == null || info.isPeriodic || info.amount <= 0f)
            return;
        float radius = CombatSettings.Instance.noise.hitRadius;
        if (radius <= 0f || Time.time < victim.nextImpactNoise)
            return;
        victim.nextImpactNoise = Time.time + 0.3f;
        CombatEvents.EmitNoise(victim.Position, radius, info.source != null ? info.source : victim, 0.7f, NoiseKind.Impact);
    }

    /// <summary>Someone died: its death cry (its allies come and look).</summary>
    internal static void OnKilled(CombatEntity victim)
    {
        if (victim == null)
            return;
        float radius = CombatSettings.Instance.noise.deathRadius;
        if (radius > 0f)
            CombatEvents.EmitNoise(victim.Position, radius, victim, 0.9f, NoiseKind.Death);
    }
}
