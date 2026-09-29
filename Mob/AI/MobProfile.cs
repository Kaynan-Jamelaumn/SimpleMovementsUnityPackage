using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How readily a mob starts fights.</summary>
public enum MobAggression
{
    /// <summary>Never starts a fight. Reacts to attacks according to "When Attacked".</summary>
    Passive,
    /// <summary>Only fights when attacked or when an ally calls for help.</summary>
    Defensive,
    /// <summary>Attacks enemies that come inside its territory around home.</summary>
    Territorial,
    /// <summary>Attacks every enemy it notices (players if "Player" is in its Preys list, and its prey mobs).</summary>
    Aggressive,
}

/// <summary>What a mob does when something hurts it.</summary>
public enum MobReaction
{
    /// <summary>Fights back.</summary>
    Fight,
    /// <summary>Runs away.</summary>
    Flee,
    /// <summary>Fights back while its health is above the flee threshold, otherwise runs.</summary>
    FightIfHealthy,
}

/// <summary>How a mob positions itself in a fight.</summary>
public enum MobCombatStyle
{
    /// <summary>Decided from its abilities (ranged abilities make it keep distance).</summary>
    Auto,
    /// <summary>Closes in and fights at melee range.</summary>
    Melee,
    /// <summary>Keeps its distance and uses ranged abilities; backs away when approached.</summary>
    Ranged,
    /// <summary>Hit and run: attacks, then backs off before coming in again.</summary>
    Skirmisher,
    /// <summary>Ranged and constantly kiting: retreats while the target chases it.</summary>
    Kiter,
    /// <summary>Heavy melee: never backs off, rarely dodges.</summary>
    Brute,
}

/// <summary>Order of patrol points.</summary>
public enum MobPatrolMode
{
    Loop,
    PingPong,
    Random,
}

/// <summary>
/// Everything that makes a mob type behave the way it does: temperament, senses, movement, fighting style, dodging,
/// fleeing, animation parameters and performance. One profile can be shared by every mob of a type; different types
/// get different profiles. Mobs without a profile build one from their old Mob fields automatically.
/// Create with Assets > Create > Scriptable Objects > Mob > Mob Profile (or a preset from the same menu).
/// </summary>
[CreateAssetMenu(fileName = "MobProfile", menuName = "Scriptable Objects/Mob/Mob Profile", order = 0)]
public class MobProfile : ScriptableObject
{
    // ------------------------------------------------------------------ temperament
    [Header("Temperament")]
    [Tooltip("Passive: never starts fights.\nDefensive: fights only when attacked.\nTerritorial: attacks enemies that come near its home.\nAggressive: attacks every enemy it notices.\nWho counts as an enemy: players when 'Player' is in the mob's Preys list, the mob types in Preys, and anyone who hurts it.")]
    public MobAggression aggression = MobAggression.Aggressive;

    [Tooltip("What it does when attacked while not already fighting.")]
    public MobReaction whenAttacked = MobReaction.Fight;

    [Tooltip("0 = cowardly, 1 = fearless. Lower courage makes it more likely to flee at low health and less likely to take risks.")]
    [Range(0f, 1f)] public float courage = 0.7f;

    [Tooltip("Health fraction at which it considers fleeing (then Courage decides). 0 = never flees because of health.")]
    [Range(0f, 1f)] public float fleeHealthThreshold = 0.2f;

    [Tooltip("Territorial mobs defend this radius around home (metres).")]
    [Min(1f)] public float territoryRadius = 12f;

    [Tooltip("Passive/Defensive creatures run from players closer than this (skittish animals). 0 = they do not care.")]
    [Min(0f)] public float fleeFromPlayersWithin = 0f;

    [Tooltip("Mob types (their 'type' name) this mob fears and flees from, in addition to predators that hunt it.")]
    public List<string> fears = new List<string>();

    // ------------------------------------------------------------------ perception
    [Header("Perception")]
    [Tooltip("How far it can see (metres).")]
    [Min(1f)] public float sightRange = 18f;

    [Tooltip("Field of view in degrees (360 = sees all around).")]
    [Range(10f, 360f)] public float fieldOfView = 140f;

    [Tooltip("Within this distance it notices characters all around it, even behind (metres).")]
    [Min(0f)] public float closeSenseRadius = 4f;

    [Tooltip("Eye height above the feet (metres). 0 = 90% of the body height.")]
    [Min(0f)] public float eyeHeight = 0f;

    [Tooltip("Multiplies how far it hears noises (footsteps, fights, spells). 0 = deaf.")]
    [Range(0f, 4f)] public float hearingMultiplier = 1f;

    [Tooltip("How fast it notices someone in view (awareness per second at close range). Higher = harder to sneak past.")]
    [Min(0.1f)] public float awarenessGainRate = 2.5f;

    [Tooltip("Sight range multiplier against crouching/sneaking players.")]
    [Range(0.1f, 1f)] public float sneakDetectionMultiplier = 0.55f;

    [Tooltip("Seconds it remembers a target after losing sight of it (then it searches the last known position).")]
    [Min(0.5f)] public float memoryDuration = 8f;

    [Tooltip("Seconds before it reacts to something new (a spotted enemy, an incoming attack). Lower = sharper, harder mob.")]
    [Range(0f, 2f)] public float reactionTime = 0.3f;

    [Tooltip("When it spots an enemy or is hurt, allies within this radius join the fight (metres). 0 = never calls for help.")]
    [Min(0f)] public float callForHelpRadius = 15f;

    [Tooltip("It answers allies' calls for help from up to this far (metres). 0 = ignores calls.")]
    [Min(0f)] public float respondToHelpRadius = 20f;

    // ------------------------------------------------------------------ movement
    [Header("Movement")]
    [Tooltip("Walking speed as a fraction of the Speed Manager's speed (wandering, patrolling, investigating).")]
    [Range(0.05f, 1.5f)] public float walkSpeedMultiplier = 0.45f;

    [Tooltip("Running speed as a fraction of the Speed Manager's speed (chasing, fleeing).")]
    [Range(0.1f, 3f)] public float runSpeedMultiplier = 1f;

    [Tooltip("Speed while circling or backing off in combat, as a fraction of the Speed Manager's speed.")]
    [Range(0.05f, 1.5f)] public float strafeSpeedMultiplier = 0.5f;

    [Tooltip("Turning speed in degrees per second.")]
    [Min(30f)] public float turnSpeed = 540f;

    [Tooltip("NavMeshAgent acceleration (metres/second²).")]
    [Min(1f)] public float acceleration = 24f;

    [Tooltip("Radius around home for idle wandering (metres).")]
    [Min(0f)] public float wanderRadius = 12f;

    [Tooltip("Seconds it stands still between walks (random between X and Y).")]
    public Vector2 idleTimeRange = new Vector2(2f, 6f);

    [Tooltip("Longest a single calm walk may take before it gives up and idles (seconds).")]
    [Min(1f)] public float maxWalkTime = 10f;

    [Tooltip("Order in which patrol points are visited.")]
    public MobPatrolMode patrolMode = MobPatrolMode.Loop;

    [Tooltip("Seconds it waits at each patrol point.")]
    [Min(0f)] public float patrolWaitTime = 2f;

    [Tooltip("Maximum distance from home while fighting; beyond it the mob gives up and returns (metres). 0 = no leash.")]
    [Min(0f)] public float leashDistance = 45f;

    [Tooltip("Longest it chases a target it cannot reach or hit (seconds). 0 = no limit.")]
    [Min(0f)] public float maxChaseTime = 0f;

    [Tooltip("Go back home after a fight ends.")]
    public bool returnHomeAfterCombat = true;

    [Tooltip("Ignore attacks and cannot be damaged while walking back home after being leashed (classic 'evade').")]
    public bool evadeWhileReturning = false;

    [Tooltip("Fraction of max health restored when it gets home after being leashed.")]
    [Range(0f, 1f)] public float healOnReturn = 0.5f;

    [Tooltip("Avoid walking through dangerous areas (telegraphed attacks, fire, lava marked with AI Hazard Source).")]
    public bool avoidHazards = true;

    [Tooltip("How far it runs when fleeing (metres).")]
    [Min(3f)] public float fleeDistance = 25f;

    // ------------------------------------------------------------------ combat
    [Header("Combat")]
    [Tooltip("How it positions itself. Auto = from its abilities.")]
    public MobCombatStyle combatStyle = MobCombatStyle.Auto;

    [Tooltip("Preferred minimum distance to the target (metres). 0 = automatic from its abilities.")]
    [Min(0f)] public float preferredMinRange = 0f;

    [Tooltip("Preferred maximum distance to the target (metres). 0 = automatic from its abilities.")]
    [Min(0f)] public float preferredMaxRange = 0f;

    [Tooltip("0 = careful (waits for good openings), 1 = relentless (attacks whenever it can).")]
    [Range(0f, 1f)] public float aggressiveness = 0.7f;

    [Tooltip("Share melee slots with other mobs on the same target (Combat Settings > Max Simultaneous Melee Attackers). Others circle and wait for their turn. Off = always attacks (bosses).")]
    public bool useAttackTokens = true;

    [Tooltip("Extra distance beyond melee reach where it circles while waiting for its turn (metres).")]
    [Min(0.5f)] public float circleDistance = 2.5f;

    [Tooltip("0-1: how much it strafes sideways between attacks (harder to hit).")]
    [Range(0f, 1f)] public float strafeAmount = 0.5f;

    [Tooltip("Seconds between the end of one attack and the start of the next (on top of cooldowns).")]
    [Min(0f)] public float minTimeBetweenAttacks = 0.4f;

    [Header("Dodging")]
    [Tooltip("Chance to dodge a telegraphed attack it is standing in (per attack).")]
    [Range(0f, 1f)] public float dodgeChance = 0.35f;

    [Tooltip("Chance to jump back when its target swings a weapon at it.")]
    [Range(0f, 1f)] public float meleeDodgeChance = 0.15f;

    [Tooltip("Seconds between dodges.")]
    [Min(0.2f)] public float dodgeCooldown = 3f;

    [Tooltip("Dodge distance (metres).")]
    [Min(0.5f)] public float dodgeDistance = 3.5f;

    [Tooltip("Seconds a dodge takes.")]
    [Range(0.1f, 1f)] public float dodgeDuration = 0.3f;

    [Tooltip("Cannot be damaged while dodging.")]
    public bool dodgeInvulnerable = false;

    [Tooltip("Can cancel its own wind-up to dodge (smart but interruptible casters).")]
    public bool dodgeCancelsAttacks = false;

    [Header("Abilities")]
    [Tooltip("0 = always picks the best ability, 1 = very random choices.")]
    [Range(0f, 1f)] public float abilityRandomness = 0.15f;

    [Tooltip("Seconds between decisions at full rate (lower = more responsive, more CPU).")]
    [Range(0.05f, 1f)] public float decisionInterval = 0.2f;

    [Tooltip("Create a basic melee attack from the Mob's Bite Damage / Attack Distance / Bite Cooldown when it has no melee ability.")]
    public bool autoBasicAttack = true;

    [Tooltip("Multiplies the Absorb Chance Per Kill set on each mob's MobAbilityController (e.g. 2 for a rare elite type, 0.5 for a common one). 1 = unchanged.")]
    [Range(0f, 5f)] public float absorbChanceMultiplier = 1f;

    // ------------------------------------------------------------------ animation
    [Header("Animation (parameters that do not exist are ignored)")]
    [Tooltip("Float set to the current movement speed (metres/second).")]
    public string speedParameter = "Speed";
    [Tooltip("Float set to the movement speed divided by the run speed (0-1).")]
    public string normalizedSpeedParameter = "SpeedPercent";
    [Tooltip("Bool true while moving.")]
    public string movingBool = "IsMoving";
    [Tooltip("Bool true while fighting.")]
    public string inCombatBool = "InCombat";
    [Tooltip("Floats for strafing blend trees: sideways (X) and forward (Y) velocity relative to facing, -1..1.")]
    public string moveXParameter = "MoveX";
    public string moveYParameter = "MoveY";
    [Tooltip("Trigger when hurt.")]
    public string hitTrigger = "Hit";
    [Tooltip("Trigger when it notices an enemy.")]
    public string alertTrigger = "Alert";
    [Tooltip("Trigger when dodging.")]
    public string dodgeTrigger = "Dodge";
    [Tooltip("Bool true while stunned.")]
    public string stunnedBool = "Stunned";
    [Tooltip("Trigger on death.")]
    public string deathTrigger = "Die";
    [Tooltip("Also cross-fade to Animator states named like the old states (Idle, Moving, Chasing, Patrol) - keeps old mob controllers working.")]
    public bool crossFadeLegacyStates = true;
    [Min(0f)] public float crossFadeTime = 0.25f;

    // ------------------------------------------------------------------ death & performance
    [Header("Death")]
    [Tooltip("Seconds between death and removal (lets the death animation play).")]
    [Min(0f)] public float destroyDelay = 3f;
    [Tooltip("Disable its colliders when it dies (players walk through the corpse).")]
    public bool disableCollidersOnDeath = true;

    [Header("Performance")]
    [Tooltip("Always think at full rate even far from players (bosses, important NPCs).")]
    public bool alwaysFullRate = false;

    // ------------------------------------------------------------------ helpers
    public float EffectiveEyeHeight(float bodyHeight) => eyeHeight > 0f ? eyeHeight : bodyHeight * 0.9f;

    /// <summary>A plain-language summary of this behaviour (shown by the mob inspectors).</summary>
    public string DescribeBehaviour()
    {
        string temper;
        switch (aggression)
        {
            case MobAggression.Passive: temper = "Never starts fights"; break;
            case MobAggression.Defensive: temper = "Fights only when attacked"; break;
            case MobAggression.Territorial: temper = $"Defends {territoryRadius:0}m around home"; break;
            default: temper = "Attacks enemies on sight"; break;
        }
        string react = whenAttacked == MobReaction.Flee ? "flees when hit" : (whenAttacked == MobReaction.FightIfHealthy ? "fights back while healthy" : "fights back");
        string flee = fleeHealthThreshold > 0f ? $", {(1f - courage) * 100f:0}% chance to flee below {fleeHealthThreshold * 100f:0}% health" : "";
        return $"{temper}; {react}{flee}.\n" +
               $"Sees {sightRange:0}m in a {fieldOfView:0}° cone (feels {closeSenseRadius:0}m around), remembers {memoryDuration:0}s, reacts in {reactionTime:0.##}s.\n" +
               $"Style {combatStyle}, aggressiveness {aggressiveness * 100f:0}%, dodges {dodgeChance * 100f:0}% of telegraphs (every {dodgeCooldown:0.#}s).\n" +
               $"Leash {(leashDistance > 0f ? leashDistance.ToString("0") + "m" : "none")}, wanders {wanderRadius:0}m, calls for help within {callForHelpRadius:0}m.";
    }

    public float RandomIdleTime()
    {
        float a = Mathf.Max(0f, Mathf.Min(idleTimeRange.x, idleTimeRange.y));
        float b = Mathf.Max(a, Mathf.Max(idleTimeRange.x, idleTimeRange.y));
        return UnityEngine.Random.Range(a, b);
    }

    public MobProfile Clone()
    {
        MobProfile p = Instantiate(this);
        p.fears = new List<string>(fears);
        p.hideFlags = HideFlags.DontSave;
        return p;
    }

    /// <summary>Checks the settings; errors break behaviour, warnings are likely mistakes.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        if (idleTimeRange.y < idleTimeRange.x)
            warnings.Add($"{name}: Idle Time Range max is smaller than min.");
        if (preferredMaxRange > 0f && preferredMaxRange < preferredMinRange)
            errors.Add($"{name}: Preferred Max Range is smaller than Preferred Min Range.");
        if (runSpeedMultiplier < walkSpeedMultiplier)
            warnings.Add($"{name}: runs slower than it walks.");
        if (leashDistance > 0f && leashDistance < sightRange)
            warnings.Add($"{name}: Leash Distance ({leashDistance}m) is shorter than Sight Range ({sightRange}m); it may give up as soon as it starts chasing.");
        if (aggression == MobAggression.Passive && whenAttacked == MobReaction.Fight && fleeFromPlayersWithin > 0f)
            warnings.Add($"{name}: flees from players but fights back when attacked - intended?");
        if (closeSenseRadius > sightRange)
            warnings.Add($"{name}: Close Sense Radius is larger than Sight Range.");
        if (fieldOfView < 60f)
            warnings.Add($"{name}: a {fieldOfView}° field of view is very narrow.");
    }

    // ------------------------------------------------------------------ legacy & presets
    /// <summary>A runtime profile from the old Mob fields (detection range, wander distance, chase time, preys...).</summary>
    public static MobProfile FromLegacy(Mob mob)
    {
        MobProfile p = CreateInstance<MobProfile>();
        p.name = (mob != null ? mob.type : "Mob") + " (legacy profile)";
        p.hideFlags = HideFlags.DontSave;
        if (mob == null)
            return p;

        bool huntsPlayer = mob.PreysReference != null && mob.PreysReference.Contains("Player");
        bool huntsMobs = mob.PreysReference != null && mob.PreysReference.Exists(s => s != "Player" && !string.IsNullOrEmpty(s));
        p.aggression = huntsPlayer || huntsMobs ? MobAggression.Aggressive : MobAggression.Passive;
        p.whenAttacked = huntsPlayer || huntsMobs ? MobReaction.Fight : MobReaction.Flee;
        p.fleeFromPlayersWithin = huntsPlayer || huntsMobs ? 0f : 5f;
        p.sightRange = Mathf.Max(4f, mob.DetectionRange);
        p.closeSenseRadius = Mathf.Min(4f, p.sightRange * 0.5f);
        p.wanderRadius = Mathf.Clamp(mob.WanderDistance, 2f, 60f);
        p.idleTimeRange = new Vector2(mob.IdleTime * 0.5f, mob.IdleTime * 1.5f);
        p.maxWalkTime = Mathf.Max(2f, mob.MaxWalkTime);
        p.maxChaseTime = mob.PlayerHasMaxChaseTime ? mob.MaxChaseTime : 0f;
        p.fleeDistance = Mathf.Clamp(mob.EscapeMaxDistance * 0.5f, 8f, 60f);
        p.leashDistance = Mathf.Max(p.sightRange * 2.5f, 30f);
        p.combatStyle = MobCombatStyle.Auto;
        return p;
    }

    public enum Preset
    {
        Brute,
        Skirmisher,
        Archer,
        Caster,
        Tank,
        Predator,
        PreyAnimal,
        Guard,
        Boss,
    }

    /// <summary>Fills this profile with a ready-made behaviour.</summary>
    public void ApplyPreset(Preset preset)
    {
        switch (preset)
        {
            case Preset.Brute:
                aggression = MobAggression.Aggressive; whenAttacked = MobReaction.Fight; courage = 0.85f; fleeHealthThreshold = 0.1f;
                combatStyle = MobCombatStyle.Brute; aggressiveness = 0.85f; dodgeChance = 0.1f; meleeDodgeChance = 0f; strafeAmount = 0.2f;
                runSpeedMultiplier = 1f; sightRange = 16f; fieldOfView = 150f;
                break;
            case Preset.Skirmisher:
                aggression = MobAggression.Aggressive; whenAttacked = MobReaction.Fight; courage = 0.6f; fleeHealthThreshold = 0.25f;
                combatStyle = MobCombatStyle.Skirmisher; aggressiveness = 0.6f; dodgeChance = 0.6f; meleeDodgeChance = 0.35f; dodgeCooldown = 2.2f;
                strafeAmount = 0.8f; runSpeedMultiplier = 1.15f; strafeSpeedMultiplier = 0.65f;
                break;
            case Preset.Archer:
                aggression = MobAggression.Aggressive; whenAttacked = MobReaction.Fight; courage = 0.5f; fleeHealthThreshold = 0.2f;
                combatStyle = MobCombatStyle.Kiter; preferredMinRange = 7f; preferredMaxRange = 16f; aggressiveness = 0.65f;
                dodgeChance = 0.45f; strafeAmount = 0.7f; sightRange = 24f; fieldOfView = 160f;
                break;
            case Preset.Caster:
                aggression = MobAggression.Aggressive; whenAttacked = MobReaction.Fight; courage = 0.45f; fleeHealthThreshold = 0.25f;
                combatStyle = MobCombatStyle.Ranged; preferredMinRange = 8f; preferredMaxRange = 18f; aggressiveness = 0.55f;
                dodgeChance = 0.3f; dodgeCancelsAttacks = false; strafeAmount = 0.4f; sightRange = 22f;
                break;
            case Preset.Tank:
                aggression = MobAggression.Territorial; whenAttacked = MobReaction.Fight; courage = 1f; fleeHealthThreshold = 0f;
                combatStyle = MobCombatStyle.Brute; aggressiveness = 0.5f; dodgeChance = 0f; meleeDodgeChance = 0f;
                runSpeedMultiplier = 0.8f; walkSpeedMultiplier = 0.35f; strafeAmount = 0f; territoryRadius = 14f;
                break;
            case Preset.Predator:
                aggression = MobAggression.Aggressive; whenAttacked = MobReaction.FightIfHealthy; courage = 0.6f; fleeHealthThreshold = 0.3f;
                combatStyle = MobCombatStyle.Skirmisher; aggressiveness = 0.75f; dodgeChance = 0.3f; meleeDodgeChance = 0.25f;
                runSpeedMultiplier = 1.25f; sightRange = 22f; fieldOfView = 200f; awarenessGainRate = 3.5f; callForHelpRadius = 25f; respondToHelpRadius = 30f;
                break;
            case Preset.PreyAnimal:
                aggression = MobAggression.Passive; whenAttacked = MobReaction.Flee; courage = 0.1f; fleeHealthThreshold = 1f;
                fleeFromPlayersWithin = 6f; fieldOfView = 300f; sightRange = 15f; runSpeedMultiplier = 1.2f; fleeDistance = 30f;
                callForHelpRadius = 10f; respondToHelpRadius = 0f; dodgeChance = 0.2f;
                break;
            case Preset.Guard:
                aggression = MobAggression.Territorial; whenAttacked = MobReaction.Fight; courage = 0.8f; fleeHealthThreshold = 0.1f;
                combatStyle = MobCombatStyle.Melee; territoryRadius = 18f; leashDistance = 30f; returnHomeAfterCombat = true;
                patrolMode = MobPatrolMode.PingPong; patrolWaitTime = 3f; callForHelpRadius = 25f; respondToHelpRadius = 30f;
                break;
            case Preset.Boss:
                aggression = MobAggression.Aggressive; whenAttacked = MobReaction.Fight; courage = 1f; fleeHealthThreshold = 0f;
                combatStyle = MobCombatStyle.Auto; aggressiveness = 0.8f; useAttackTokens = false; dodgeChance = 0.2f; meleeDodgeChance = 0.1f;
                reactionTime = 0.2f; abilityRandomness = 0.1f; decisionInterval = 0.12f; leashDistance = 0f; alwaysFullRate = true;
                sightRange = 30f; fieldOfView = 360f; evadeWhileReturning = false; destroyDelay = 6f;
                break;
        }
    }

    public static MobProfile CreatePreset(Preset preset)
    {
        MobProfile p = CreateInstance<MobProfile>();
        p.name = preset + " Profile";
        p.ApplyPreset(preset);
        return p;
    }
}
