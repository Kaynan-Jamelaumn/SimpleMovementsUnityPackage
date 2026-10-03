using UnityEngine;

/// <summary>
/// Project-wide combat settings (layers, telegraph look, AI budgets). Optional: create one with
/// Assets > Create > SimpleMovements > Combat > Combat Settings and put it in a Resources folder named exactly
/// "CombatSettings" - otherwise the defaults below are used.
/// </summary>
[CreateAssetMenu(fileName = "CombatSettings", menuName = "SimpleMovements/Combat/Combat Settings", order = 0)]
public class CombatSettings : ScriptableObject
{
    [Header("Layers")]
    [Tooltip("Layers characters (player, mobs, summons) are on. Hit shapes and AI perception only look at these layers. Everything = no filtering (slower in busy scenes).")]
    public LayerMask characterLayers = ~0;

    [Tooltip("Layers that block line of sight, projectiles and beams (terrain, walls, rocks). Characters on these layers are ignored automatically.")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Layers treated as ground when snapping ground effects, telegraphs and summons.")]
    public LayerMask groundLayers = ~0;

    [Header("Rules")]
    [Tooltip("Players can damage other players with abilities (PvP). Off = players are allies.")]
    public bool playersCanHurtEachOther = false;

    [Tooltip("FRIENDLY FIRE between party members. Off: damage, control, knockback and debuffs never land on party members " +
             "(heals and buffs still do). On: harmful hits also hit party members caught in them, even when the attack " +
             "targets enemies only. Each attack / hit can override it (Target Rules ▸ Friendly Fire).")]
    public bool partyFriendlyFire = false;

    [Tooltip("FRIENDLY FIRE between allies that are not in the same party (same team or allied factions). Off: harmful " +
             "effects never land on them. On: harmful hits also hit allies caught in them.")]
    public bool allyFriendlyFire = false;

    [Tooltip("When a character loses health without a known attacker (e.g. a weapon hit that does not report its source), the nearest player within this distance is treated as the attacker (for aggro and absorption). 0 = never guess.")]
    [Min(0f)] public float unattributedDamageRadius = 8f;

    [Tooltip("Call AvailabilityStateMachine.Kill() on a player whose health reaches 0 through combat damage.")]
    public bool killPlayersAtZeroHealth = true;

    [Header("Body Parts")]
    [Tooltip("Give every character (players and mobs) body-part damage: head / torso / arms / legs multipliers, armour that " +
             "protects only where it covers, stagger and slows. Off = only characters with a Body Part Controller use it.")]
    public bool bodyPartsForEveryCharacter = false;
    [Tooltip("Profile used by Body Part Controllers without their own. Empty = a built-in humanoid (head, torso, arms, legs).")]
    public BodyPartProfile defaultBodyPartProfile;
    [Tooltip("Ability hits around the caster (mob claws, slams, swings) land at this height of the CASTER (0 = feet, 1 = top): a " +
             "wolf bites legs, a man hits the torso, an ogre hits heads. Projectiles land where they touch.")]
    [Range(0f, 1f)] public float abilityStrikeHeight = 0.65f;
    [Tooltip("Random height variation of those hits, as a fraction of the target's height (so they do not always land on the same part).")]
    [Range(0f, 0.5f)] public float abilityStrikeSpread = 0.15f;

    [Header("Threat (who mobs attack)")]
    [Tooltip("Threat per point of damage dealt to a mob.")]
    [Min(0f)] public float threatPerDamage = 1f;
    [Tooltip("Threat per point of healing, given by EACH mob fighting the healed character (shared between them).")]
    [Min(0f)] public float threatPerHealing = 0.5f;
    [Tooltip("Threat for applying crowd control (stun, root, slow...) to a mob, as a fraction of the mob's max health.")]
    [Min(0f)] public float threatPerControl = 0.02f;
    [Tooltip("Head start of the FIRST character to hit or be noticed by a mob, as a fraction of the mob's max health: it is " +
             "attacked first, but others can overtake it.")]
    [Min(0f)] public float firstContactThreat = 0.1f;
    [Tooltip("Threat when a mob notices an enemy (fraction of its max health): the first one noticed is preferred until someone does something.")]
    [Min(0f)] public float detectionThreat = 0.02f;
    [Tooltip("A taunt puts the taunter this much above the top threat (1.1 = 10% above) - it stays the target after the taunt ends.")]
    [Min(1f)] public float tauntThreatMultiplier = 1.1f;
    [Tooltip("Seconds without new threat from someone before their threat starts fading.")]
    [Min(0f)] public float threatDecayDelay = 5f;
    [Tooltip("Fraction of the threat lost per second once fading (0.05 = 5% per second).")]
    [Range(0f, 1f)] public float threatDecayPerSecond = 0.05f;
    [Tooltip("A mob switches to someone else only when their threat exceeds the current target's by this factor while in melee range (1.1 = 10% more).")]
    [Min(1f)] public float threatSwitchMarginMelee = 1.1f;
    [Tooltip("The same, when the other character is beyond melee range (1.3 = 30% more): ranged attackers pull less easily.")]
    [Min(1f)] public float threatSwitchMarginRanged = 1.3f;
    [Tooltip("Distance that counts as melee range for the switch margin (metres).")]
    [Min(0.5f)] public float threatMeleeRange = 4f;
    [Tooltip("Least seconds a mob stays on a target before switching by threat (taunts and dead targets switch at once).")]
    [Min(0f)] public float threatMinTimeOnTarget = 1.5f;

    [Header("AI Budgets")]
    [Tooltip("How many mobs may attack the same target in melee at once. Others circle and wait for their turn - fights stay readable and fair.")]
    [Range(1, 12)] public int maxSimultaneousMeleeAttackers = 3;

    [Tooltip("How many mobs may use ranged abilities on the same target at once.")]
    [Range(1, 12)] public int maxSimultaneousRangedAttackers = 4;

    [Tooltip("Within this distance of a player, mobs think at full rate.")]
    [Min(1f)] public float aiNearDistance = 25f;

    [Tooltip("Beyond this distance from every player, mobs think slowly.")]
    [Min(1f)] public float aiFarDistance = 70f;

    [Tooltip("Beyond this distance from every player, mobs sleep (no perception or decisions) until a player comes closer.")]
    [Min(1f)] public float aiSleepDistance = 160f;

    [Header("Telegraphs (attack warnings)")]
    [Tooltip("Show the area of hostile abilities on the ground while they are being cast so players can dodge.")]
    public bool showEnemyTelegraphs = true;

    [Tooltip("Material for telegraph lines. Empty = a built-in unlit material (Sprites/Default).")]
    public Material telegraphMaterial;

    [Tooltip("Colour of hostile telegraphs.")]
    public Color enemyTelegraphColor = new Color(1f, 0.25f, 0.1f, 0.85f);

    [Tooltip("Colour of friendly telegraphs (allies and the player's own abilities).")]
    public Color allyTelegraphColor = new Color(0.25f, 0.65f, 1f, 0.7f);

    [Tooltip("Colour of the player's aiming preview for abilities that need a confirmation click.")]
    public Color aimPreviewColor = new Color(1f, 1f, 1f, 0.6f);

    [Tooltip("Line width of telegraph outlines (metres).")]
    [Min(0.01f)] public float telegraphLineWidth = 0.08f;

    [Header("Defaults")]
    [Tooltip("Visual used for projectiles that have no prefab (a small glowing sphere is generated when empty).")]
    public GameObject defaultProjectilePrefab;

    [Tooltip("Visual for absorbable-ability pickups (a glowing orb is generated when empty).")]
    public GameObject defaultAbsorbPickupPrefab;

    [Tooltip("Segment used by walls and cages that have no prefab (a plain block is generated when empty).")]
    public GameObject defaultBarrierSegmentPrefab;

    [Tooltip("Material of generated wall/cage blocks. Empty = a lit grey material.")]
    public Material barrierMaterial;

    [Header("Debug")]
    [Tooltip("Draw hit shapes, hazards and AI decisions in the Scene view while playing.")]
    public bool drawDebug = false;

    private static CombatSettings instance;

    /// <summary>The settings asset from Resources/CombatSettings, or defaults.</summary>
    public static CombatSettings Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<CombatSettings>("CombatSettings");
                if (instance == null)
                {
                    instance = CreateInstance<CombatSettings>();
                    instance.name = "CombatSettings (defaults)";
                    instance.hideFlags = HideFlags.DontSave;
                }
            }
            return instance;
        }
    }

    /// <summary>Use a specific settings asset (tests, bootstrap scripts).</summary>
    public static void Override(CombatSettings settings) => instance = settings;

    private Material cachedTelegraphMaterial;
    private Material cachedBarrierMaterial;

    /// <summary>Material for generated barrier blocks.</summary>
    public Material BarrierMaterial
    {
        get
        {
            if (barrierMaterial != null)
                return barrierMaterial;
            if (cachedBarrierMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    cachedBarrierMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                    cachedBarrierMaterial.color = new Color(0.45f, 0.42f, 0.38f, 1f);
                }
            }
            return cachedBarrierMaterial;
        }
    }

    public Material TelegraphMaterial
    {
        get
        {
            if (telegraphMaterial != null)
                return telegraphMaterial;
            if (cachedTelegraphMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                cachedTelegraphMaterial = shader != null ? new Material(shader) : null;
                if (cachedTelegraphMaterial != null)
                    cachedTelegraphMaterial.hideFlags = HideFlags.DontSave;
            }
            return cachedTelegraphMaterial;
        }
    }
}
