using System;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>How deep a cave is (see <see cref="CaveDepth"/>).</summary>
    public enum CaveDepthBand
    {
        /// <summary>The first level of an ordinary cave: fuel, stone, common ores, weak creatures.</summary>
        Shallow,
        /// <summary>A level or two down: better ores, tougher creatures.</summary>
        Medium,
        /// <summary>Far down (or a far cave): silver, gold, gemstones, dangerous creatures.</summary>
        Deep,
        /// <summary>The depths below the depths: orichalcum, mythril, the finest gemstones, the worst of what lives there.</summary>
        Abyssal,
    }

    /// <summary>
    /// How deep a place in a cave counts (its depth score decides which resources and how tough the creatures are):
    /// one per level down, a little more far from the level's way in, more in caves far from the world's start (their
    /// difficulty) and in the deeper caves reached through a cave's lowest passage.
    /// </summary>
    [Serializable]
    public class CaveDepthSettings
    {
        [Tooltip("Depth added by each level down (floor 0 = the top level).")]
        [Min(0f)] public float perLevel = 1f;
        [Tooltip("Depth added from a level's way in to its farthest point (0 = the whole level is as deep).")]
        [Min(0f)] public float acrossLevel = 0.6f;
        [Tooltip("Depth added by each deeper cave reached through the lowest level's passage (the exit's Next Dungeon).")]
        [Min(0f)] public float perDeeperCave = 2f;
        [Tooltip("Depth added per point of difficulty above 1 (caves far from the world's start are harder - Portal / Cave Entrance difficulty).")]
        [Min(0f)] public float perDifficulty = 0.6f;
        [Tooltip("Where the bands begin: Medium, Deep and Abyssal (Shallow is below Medium).")]
        public Vector3 bandStarts = new Vector3(1f, 2.5f, 4f);
        [Tooltip("The bottom of the world: depth never counts more than this, however far down a chain of deeper caves goes (keep it inside the deepest resources' ranges - the built-in ones reach 9-12). 0 = no limit.")]
        [Min(0f)] public float maxDepth = 8f;

        public CaveDepthBand Band(float depth) =>
            depth < bandStarts.x ? CaveDepthBand.Shallow : depth < bandStarts.y ? CaveDepthBand.Medium : depth < bandStarts.z ? CaveDepthBand.Deep : CaveDepthBand.Abyssal;
    }

    /// <summary>How many deposits a cave holds and where they may go.</summary>
    [Serializable]
    public class CaveDepositSettings
    {
        [Tooltip("Deposits per 100 walkable cells of an ordinary chamber (a 64 x 64 level has some 1 200-1 800 walkable cells). Recommended 0.8-2.5.")]
        [Min(0f)] public float perHundredCells = 1.4f;
        [Tooltip("More deposits deeper down: +this share per point of depth (0.15 = 15% more a level down).")]
        [Min(0f)] public float morePerDepth = 0.12f;
        [Tooltip("Share of deposits in the walls (the rest on the floor, where an entry allows both).")]
        [Range(0f, 1f)] public float wallShare = 0.75f;
        [Tooltip("Smallest distance between two deposits (cells). The deposits of one vein sit about this far apart.")]
        [Min(1f)] public float spacing = 1.7f;
        [Tooltip("No deposit within this walking distance of a level's way in (cells): the arrival stays clear.")]
        [Min(0)] public int clearOfArrival = 6;
        [Tooltip("Most deposits on one level.")]
        [Min(0)] public int maxPerLevel = 140;
        [Tooltip("Only in natural cave chambers and tunnels (off = also in built rooms of mixed profiles).")]
        public bool cavesOnly = true;
        [Tooltip("Height of wall deposits above the floor (meters): where a pick swings.")]
        public FloatRange wallHeight = new FloatRange(0.25f, 1.35f);
    }

    /// <summary>The special chambers: rich veins (mining areas), crystal grottos and guarded hoards.</summary>
    [Serializable]
    public class CaveChamberSettings
    {
        [Header("Rich Veins (mining areas)")]
        [Tooltip("Chance a level has a rich vein: a chamber whose walls are full of ore (tagged \"Rich Vein\").")]
        [Range(0f, 1f)] public float richVeinChance = 0.6f;
        [Tooltip("Rich veins on a level when it has them.")]
        public IntRange richVeinsPerLevel = new IntRange(1, 2);
        [Tooltip("Smallest chamber that can be a rich vein (cells).")]
        [Min(4)] public int richVeinMinCells = 40;
        [Tooltip("Deposits in a rich vein, against an ordinary chamber of its size.")]
        [Min(1f)] public float richVeinDensity = 4f;
        [Tooltip("A rich vein counts as this much deeper when its resources are chosen (better ores).")]
        [Min(0f)] public float richVeinDepthBonus = 0.8f;

        [Header("Crystal Grottos")]
        [Tooltip("Chance a level has a crystal grotto (tagged \"Crystal Grotto\": glowing crystals, gemstone veins), from the Medium band down. Doubles in the Deep and Abyssal bands.")]
        [Range(0f, 1f)] public float grottoChance = 0.35f;
        [Tooltip("Smallest chamber that can be a crystal grotto (cells).")]
        [Min(4)] public int grottoMinCells = 28;
        [Tooltip("Deposits in a grotto, against an ordinary chamber of its size.")]
        [Min(1f)] public float grottoDensity = 3f;
        [Tooltip("A grotto counts as this much deeper when its gems are chosen.")]
        [Min(0f)] public float grottoDepthBonus = 1f;

        [Header("Guarded Hoards")]
        [Tooltip("Chance a rich vein below the first level is guarded: it becomes a guardian chamber (an elite creature and its guards; the ways in close while they fight).")]
        [Range(0f, 1f)] public float guardedChance = 0.4f;
        [Tooltip("Rooms of these roles hold a hoard: their deposits count as this much deeper (bosses, guardians, treasure rooms).")]
        [Min(0f)] public float hoardDepthBonus = 1.5f;
        [Tooltip("Deposits in a hoard room, against an ordinary chamber of its size.")]
        [Min(1f)] public float hoardDensity = 1.6f;
    }

    /// <summary>The rock formations: stalagmites, columns, boulders, crystal clusters, stalactites and loose rubble.</summary>
    [Serializable]
    public class CaveFormationSettings
    {
        [Tooltip("Build rock formations.")]
        public bool enabled = true;
        [Tooltip("Formations that stand in the way (stalagmites, columns, boulders, crystal clusters) per 100 open cells. They never close a passage: each keeps a free cell all round.")]
        [Min(0f)] public float standingPerHundredCells = 2.4f;
        [Tooltip("Smallest distance from a wall for a standing formation (cells): it stands in open ground, with room to walk round it.")]
        [Min(1.5f)] public float minWallDistance = 1.6f;
        [Tooltip("Smallest distance between two standing formations (cells).")]
        [Min(2.3f)] public float spacing = 2.8f;
        [Tooltip("Ceilings lower than this (meters above the floor) may hold columns (floor-to-ceiling); higher ones get stalagmites.")]
        [Min(2f)] public float columnMaxCeiling = 9f;
        [Tooltip("Share of standing formations that are boulders.")]
        [Range(0f, 1f)] public float boulderShare = 0.25f;
        [Tooltip("Stalactites hanging from the ceiling per 100 cells (they never hang lower than Head Room).")]
        [Min(0f)] public float stalactitesPerHundredCells = 7f;
        [Tooltip("Stalactites only where the ceiling is at least this high (meters above the floor).")]
        [Min(2.5f)] public float stalactiteMinCeiling = 4.2f;
        [Tooltip("Free height under hanging rock (meters).")]
        [Min(1.8f)] public float headRoom = 2.6f;
        [Tooltip("Loose rubble along the walls per 100 cells (small, walk-over stones).")]
        [Min(0f)] public float rubblePerHundredCells = 5f;
        [Tooltip("Crystal clusters on the walls of crystal grottos per 100 cells.")]
        [Min(0f)] public float grottoCrystalsPerHundredCells = 9f;
        [Tooltip("Glowing lights in a crystal grotto (each is a real light: keep it low).")]
        [Range(0, 6)] public int grottoLights = 3;
        [Tooltip("Range of a grotto's lights (meters).")]
        [Min(1f)] public float grottoLightRange = 7f;
        [Tooltip("Standing formations get colliders (players and creatures walk round them; the NavMesh knows them).")]
        public bool colliders = true;
    }

    /// <summary>The cave's creatures, on top of the profile's encounter table (which creatures, how many).</summary>
    [Serializable]
    public class CaveEnemySettings
    {
        [Tooltip("Creatures grow tougher with depth: from the Medium band down each gets these per band below Shallow (Medium 1, Deep 2, Abyssal 3), through its Combat Stats (a lasting modifier named Deep-Dweller).")]
        public bool depthToughness = true;
        [Tooltip("Endurance points per band (more health).")]
        [Min(0f)] public float endurancePerBand = 4f;
        [Tooltip("Defense points per band.")]
        [Min(0f)] public float defensePerBand = 5f;
        [Tooltip("More damage dealt per band (percent, every kind of damage the creature deals).")]
        [Min(0f)] public float damagePercentPerBand = 8f;
    }

    /// <summary>What a cave keeps when the players leave and come back (see <see cref="CaveStore"/>).</summary>
    [Serializable]
    public class CavePersistenceSettings
    {
        [Tooltip("Keep each cave's state - mined deposits, slain creatures, opened chests and broken barrels - in the world's save, so leaving and coming back finds it as it was. The cave itself (its tunnels, chambers, deposits) is always the same: it comes from the entrance's seed.")]
        public bool keepState = true;
        [Tooltip("Minutes of play until slain creatures come back (0 = never in this world).")]
        [Min(0f)] public float creaturesReturnMinutes = 45f;
        [Tooltip("Minutes of play until opened chests and broken containers are filled again (0 = never).")]
        [Min(0f)] public float lootReturnMinutes = 0f;
        [Tooltip("Scales every deposit's regrow time (1 = as its resource entry says; 0.5 = twice as fast; 0 = worked-out deposits never grow back).")]
        [Min(0f)] public float regrowScale = 1f;
    }

    /// <summary>How the way out (the entrance portal inside) and the way deeper (the exit portal) look and feel.</summary>
    [Serializable]
    public class CaveAccessSettings
    {
        [Tooltip("Build the way out as a rock passage up towards daylight and the way deeper as a dark passage down (instead of the dungeon's portals), when the theme has no portal prefab of its own.")]
        public bool naturalPassages = true;
        [Tooltip("Colour of the daylight coming down the way out.")]
        public Color daylight = new Color(1f, 0.93f, 0.78f);
        [Tooltip("Intensity of that light.")]
        [Min(0f)] public float daylightIntensity = 2.4f;
        [Tooltip("Seconds the screen takes to fade out when a player walks into a passage.")]
        [Range(0.1f, 2f)] public float fadeSeconds = 0.6f;
        [Tooltip("OPTIONAL. Played when a player goes in or out (a gust of cave air, an echo).")]
        public AudioClip passageSound;
    }
}
