using UnityEngine;

/// <summary>
/// Project-wide combat settings (layers, telegraph look, AI budgets). Optional: create one with
/// Assets > Create > Scriptable Objects > Combat > Combat Settings and put it in a Resources folder named exactly
/// "CombatSettings" - otherwise the defaults below are used.
/// </summary>
[CreateAssetMenu(fileName = "CombatSettings", menuName = "Scriptable Objects/Combat/Combat Settings", order = 0)]
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
