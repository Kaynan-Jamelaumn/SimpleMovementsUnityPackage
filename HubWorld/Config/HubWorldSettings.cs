using System;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace HubWorld
{
    /// <summary>The Dimensional Disorientation debuff given to players lost between dimensions.</summary>
    [Serializable]
    public class DisorientationSettings
    {
        [Tooltip("Give the debuff to players lost between dimensions.")]
        public bool enabled = true;
        [Tooltip("Name on status icons and messages.")]
        public string label = "Dimensional Disorientation";
        [Tooltip("Seconds it lasts.")]
        [Min(1f)] public float duration = 300f;
        [Tooltip("Character stats changed while it lasts (percent): slower, more fragile, slower to recover.")]
        public List<TraitModifier> characterStats = new List<TraitModifier>
        {
            TraitModifier.Percent(TraitStat.MoveSpeed, -20f),
            TraitModifier.Percent(TraitStat.DamageTaken, 15f),
            TraitModifier.Percent(TraitStat.StaminaRegen, -30f),
            TraitModifier.Percent(TraitStat.ManaRegen, -30f),
        };
        [Tooltip("Combat stats changed while it lasts.")]
        public List<CombatStatModifier> combatStats = new List<CombatStatModifier>
        {
            new CombatStatModifier(CombatStatType.AttackSpeed, -15f),
            new CombatStatModifier(CombatStatType.CastingSpeed, -15f),
        };
        [Tooltip("OPTIONAL. Effect attached to the player while it lasts.")]
        public GameObject attachedEffect;
    }

    /// <summary>What players lost between dimensions lose.</summary>
    [Serializable]
    public class LootLossSettings
    {
        [Tooltip("Share of the items carried that is lost (0.25 = a quarter of every stack on average; single items are " +
                 "lost with that chance). Equipped items are never lost.")]
        [Range(0f, 1f)] public float itemFraction = 0.25f;
        [Tooltip("The hotbar counts as carried loot too (off = only the bag).")]
        public bool includeHotbar = true;
        [Tooltip("Share of the money carried that is lost.")]
        [Range(0f, 1f)] public float currencyFraction = 0f;
        [Tooltip("Items that are never lost (quest items, keys...).")]
        public List<ItemSO> protectedItems = new List<ItemSO>();
    }

    /// <summary>The world's rings around the city.</summary>
    [Serializable]
    public class ZoneSettings
    {
        [Tooltip("Where the city stands in the world (x, z): the World scene's exterior copy of the city is built here.")]
        public Vector2 cityCenter = Vector2.zero;
        [Tooltip("Radius of the city walls (metres). Inside it is the city: never PvP.")]
        [Min(0f)] public float cityWallRadius = 80f;
        [Tooltip("The world keeps clear around the city up to this distance: no trees, rocks, mobs or portals.")]
        [Min(0f)] public float keepClearRadius = 140f;
        [Tooltip("Level the terrain under the city's exterior to City Ground Height (blended into the land around).")]
        public bool flattenCityGround = true;
        [Tooltip("Ground height of the levelled area (world y): where the World scene's city walls stand.")]
        public float cityGroundHeight = 20f;
        [Tooltip("Width of the ring where the levelled ground blends into the natural terrain (metres).")]
        [Min(0f)] public float flattenBlend = 80f;

        [Tooltip("The rings, from the city outwards. The last one goes on forever.")]
        public List<ZoneBand> rings = new List<ZoneBand>
        {
            new ZoneBand("Safe Zone", 600f, false, 0.8f, 1.2f, 0.7f, 1f, 0.75f, new Color(0.55f, 0.9f, 0.6f)),
            new ZoneBand("Frontier", 1500f, true, 1.2f, 2f, 1f, 1.4f, 1f, new Color(1f, 0.85f, 0.45f)),
            new ZoneBand("Dangerous Zone", 3000f, true, 2f, 3.2f, 1.4f, 2f, 1.4f, new Color(1f, 0.5f, 0.35f)),
            new ZoneBand("The Abyss", 6000f, true, 3.2f, 4.5f, 2f, 2.8f, 2f, new Color(0.85f, 0.35f, 1f)),
        };
        [Tooltip("Dungeon difficulty keeps growing by this much per km beyond the last ring.")]
        [Min(0f)] public float difficultyPerKmBeyond = 0.5f;
        [Tooltip("Dungeon difficulty never goes above this.")]
        [Min(0.1f)] public float maxDifficulty = 8f;
        [Tooltip("Scale the strength (health, damage) of world mobs with the ring they spawn in.")]
        public bool scaleWorldMobs = true;
        [Tooltip("Players in the same dungeon may fight each other (off = never PvP in dungeons).")]
        public bool pvpInDungeons = false;
        [Tooltip("Seconds between zone checks of each player (the HUD and PvP use the last result).")]
        [Range(0.05f, 2f)] public float checkInterval = 0.2f;
    }

    /// <summary>Duels.</summary>
    [Serializable]
    public class DuelSettings
    {
        [Tooltip("Players can challenge each other to duels.")]
        public bool enabled = true;
        [Tooltip("Duels may also be fought outside the city (off = only in the city).")]
        public bool allowOutsideCity = true;
        [Tooltip("Players must be this close to challenge (metres).")]
        [Min(1f)] public float challengeRange = 8f;
        [Tooltip("Seconds a challenge waits for an answer.")]
        [Min(5f)] public float challengeTimeout = 30f;
        [Tooltip("Seconds between accepting and fighting.")]
        [Min(0f)] public float countdown = 3f;
        [Tooltip("Longest fight (seconds) before it ends in a draw. 0 = no limit.")]
        [Min(0f)] public float maxDuration = 180f;
        [Tooltip("Radius of the temporary arena around the two fighters (metres). 0 = no arena.")]
        [Min(0f)] public float arenaRadius = 12f;
        [Tooltip("Leaving the arena loses the duel.")]
        public bool leavingArenaLoses = true;
        [Tooltip("Show the arena as a glowing ring on the ground.")]
        public bool showArena = true;
        [Tooltip("The loser stays at this share of max health instead of dying (duels never kill).")]
        [Range(0.01f, 1f)] public float loserHealth = 0.05f;
        [Tooltip("Heal both fighters to full when the duel ends.")]
        public bool healAfterDuel = true;
        [Tooltip("Both fighters' abilities are back off cooldown when the duel starts.")]
        public bool resetCooldownsAtStart = true;
    }

    /// <summary>
    /// Everything about the hub city, the cyclical world and the dungeons: scene names, cycle length and warnings,
    /// penalties, zones, duels, dungeon scenes. Optional: create one with Assets ▸ Create ▸ SimpleMovements ▸ Hub World ▸
    /// Hub World Settings and put it in a Resources folder named exactly "HubWorldSettings" (Tools ▸ SimpleMovements ▸
    /// Hub World ▸ Create Settings does both) - otherwise the defaults below are used.
    /// </summary>
    [CreateAssetMenu(fileName = "HubWorldSettings", menuName = "SimpleMovements/Hub World/Hub World Settings", order = 0)]
    public class HubWorldSettings : ScriptableObject
    {
        [Header("Scenes (must be in Build Settings)")]
        [Tooltip("The permanent hub city. Kept loaded the whole game; never regenerated.")]
        public string citySceneName = "City";
        [Tooltip("The open world (terrain, mobs, portals, the exterior copy of the city). Loaded with a new seed each cycle.")]
        public string worldSceneName = "World";
        [Tooltip("Load the City scene at start when it is not open (e.g. when the game starts in a bootstrap scene).")]
        public bool loadCityOnStart = true;
        [Tooltip("Unload the World scene while nobody is out in the world (single player / local play: keeps the city " +
                 "light and the two never overlap). A dedicated world server (multiplayer stages) keeps it loaded.")]
        public bool unloadWorldWhenEmpty = true;

        [Header("Dimensional Cycle")]
        [Tooltip("Length of one cycle (minutes). At its end the city jumps and the world is regenerated with a new seed.")]
        [Min(0.1f)] public float cycleMinutes = 90f;
        [Tooltip("Warnings before the jump (minutes before it).")]
        public List<float> warningMinutes = new List<float> { 10f, 5f, 1f };
        [Tooltip("Seed of the whole sequence of worlds (0 = a new one every session). Same seed = same worlds and modifiers.")]
        public int shardSeed = 0;
        [Tooltip("Count real time (unscaled), so pausing the game does not pause the cycle. Off = game time.")]
        public bool useUnscaledTime = false;
        [Tooltip("The city gate closes this many seconds before the jump (0 = open until the jump).")]
        [Min(0f)] public float closeGateSecondsBeforeJump = 30f;

        [Header("Warnings and atmosphere")]
        [Tooltip("Warning text ({0} = time left).")]
        public string warningText = "The city is about to jump dimensions! Return within {0}.";
        [Tooltip("Text when the city jumps.")]
        public string jumpText = "The city has jumped dimensions.";
        [Tooltip("Text for players lost between dimensions ({0} = what they lost).")]
        public string lostText = "You were lost between dimensions... {0}";
        public AudioClip warningSound;
        public AudioClip finalWarningSound;
        public AudioClip jumpSound;
        [Range(0f, 1f)] public float soundVolume = 0.8f;
        [Tooltip("The sky changes during this many final seconds of a cycle (0 = no change).")]
        [Min(0f)] public float atmosphereRampSeconds = 600f;
        [Tooltip("Fog colour the world drifts toward as the jump nears.")]
        public Color riftFogColor = new Color(0.45f, 0.25f, 0.6f);
        [Tooltip("Fog density at the jump (the scene's own density at the start of the ramp).")]
        [Min(0f)] public float riftFogDensity = 0.02f;
        [Tooltip("Ambient light the world drifts toward as the jump nears.")]
        public Color riftAmbientColor = new Color(0.35f, 0.25f, 0.5f);
        [Tooltip("Sun intensity multiplier at the jump.")]
        [Range(0f, 1f)] public float riftSunMultiplier = 0.4f;

        [Header("Lost between dimensions")]
        public LootLossSettings lootLoss = new LootLossSettings();
        public DisorientationSettings disorientation = new DisorientationSettings();

        [Header("Dimensional modifiers")]
        [Tooltip("Special conditions a cycle may have (Frozen Dimension, Eclipse, Sunken World, Invaded World...).")]
        public List<DimensionalModifier> modifiers = new List<DimensionalModifier>();
        [Tooltip("Weight of a cycle WITHOUT a modifier, compared to the modifiers' own weights.")]
        [Min(0f)] public float noModifierWeight = 2f;

        [Header("Zones")]
        public ZoneSettings zones = new ZoneSettings();

        [Header("City")]
        [Tooltip("Abilities used in the city are only shown (validated and put on cooldown; nothing is hit).")]
        public bool cityAbilitiesVisualOnly = true;

        [Header("Duels")]
        public DuelSettings duels = new DuelSettings();

        [Header("Dungeons")]
        [Tooltip("Build every dungeon in a scene of its own (own physics, own slot). Off = the classic mode (under the world).")]
        public bool dungeonsInOwnScenes = true;
        public DungeonSceneSettings dungeonScenes = new DungeonSceneSettings();

        [Header("Travel")]
        [Tooltip("Longest wait (seconds) for the ground under an arrival point to be generated before placing the player anyway.")]
        [Min(1f)] public float arrivalGroundTimeout = 20f;
        [Tooltip("Log cycle, travel and zone events to the Console.")]
        public bool logEvents = false;

        public float CycleSeconds => Mathf.Max(6f, cycleMinutes * 60f);

        /// <summary>Warning times in seconds before the jump.</summary>
        public List<float> WarningSeconds()
        {
            var list = new List<float>();
            if (warningMinutes != null)
                foreach (float m in warningMinutes)
                    if (m > 0f)
                        list.Add(m * 60f);
            return list;
        }

        private static HubWorldSettings instance;

        /// <summary>The settings asset from Resources/HubWorldSettings, or defaults.</summary>
        public static HubWorldSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<HubWorldSettings>("HubWorldSettings");
                    if (instance == null)
                    {
                        instance = CreateInstance<HubWorldSettings>();
                        instance.name = "HubWorldSettings (defaults)";
                        instance.hideFlags = HideFlags.DontSave;
                    }
                }
                return instance;
            }
        }

        /// <summary>Use a specific settings asset (tests, bootstrap scripts).</summary>
        public static void Override(HubWorldSettings settings) => instance = settings;

        /// <summary>The zone map these settings describe.</summary>
        public ZoneMap CreateZoneMap() => new ZoneMap(zones.cityCenter, zones.cityWallRadius, zones.rings, zones.difficultyPerKmBeyond, zones.maxDifficulty);
    }
}
