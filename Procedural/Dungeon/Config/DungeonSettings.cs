using System;
using UnityEngine;

namespace ProceduralDungeon
{
    // The settings groups shown on a DungeonProfile. They are plain serializable data: the profile compiler clones them
    // on the main thread, and the generation stages read the clones on worker threads.
    //
    // Units: "cells" are grid cells (Cell Size meters each, 1.5 m by default); "meters" are world units.
    // Ranges written "from low to high Openness/Complexity" are interpolated by that floor's value (0..1, rolled per
    // floor from the profile's Openness / Complexity ranges).

    /// <summary>A min..max range of floats (drawn on one line in the Inspector).</summary>
    [Serializable]
    public struct FloatRange
    {
        [Tooltip("Lowest value.")]
        public float min;
        [Tooltip("Highest value.")]
        public float max;

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float Lerp(float t) => min + (max - min) * Mathf.Clamp01(t);

        public float Random(DungeonRandom rng) => rng.Range(Mathf.Min(min, max), Mathf.Max(min, max));

        public bool Contains(float v) => v >= Mathf.Min(min, max) && v <= Mathf.Max(min, max);
    }

    /// <summary>A min..max range of integers, both inclusive (drawn on one line in the Inspector).</summary>
    [Serializable]
    public struct IntRange
    {
        [Tooltip("Lowest value (inclusive).")]
        public int min;
        [Tooltip("Highest value (inclusive).")]
        public int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        /// <summary>min..max inclusive.</summary>
        public int Random(DungeonRandom rng) => rng.Range(Mathf.Min(min, max), Mathf.Max(min, max) + 1);

        public int Lerp(float t) => Mathf.RoundToInt(min + (max - min) * Mathf.Clamp01(t));
    }

    [Serializable]
    public class SizeClassSettings
    {
        [Tooltip("Which size this entry defines. A DungeonRequest (e.g. the Portal's Dungeon Size) picks one; missing sizes fall back to Medium.")]
        public SizeClass size = SizeClass.Medium;
        [Tooltip("Grid cells per floor, width x depth (before Footprint Variation). Meters = cells x Cell Size: 64 cells at 1.5 m = 96 m. Recommended 40-120; larger floors take longer to generate and build.")]
        public Vector2Int floorCells = new Vector2Int(64, 64);
        [Tooltip("Floor count range for this size (min, max). (0, 0) = use the profile's Floor Count.")]
        public Vector2Int floorCount = Vector2Int.zero;
    }

    [Serializable]
    public class StyleWeights
    {
        [Tooltip("Relative chance of a Rooms floor: scattered rooms of varied shapes joined by corridors and doors. 0 = never.")]
        [Min(0f)] public float rooms = 1f;
        [Tooltip("Relative chance of a BSP floor: an orderly fortress/crypt of rooms from recursive splitting. 0 = never.")]
        [Min(0f)] public float bsp = 0.6f;
        [Tooltip("Relative chance of a Caverns floor: natural chambers and winding tunnels, uneven floors and domed ceilings. 0 = never.")]
        [Min(0f)] public float caverns = 0.8f;
        [Tooltip("Relative chance of a Hybrid floor: built rooms, caverns and ruins mixed in zones on one floor. 0 = never.")]
        [Min(0f)] public float hybrid = 0.9f;
        [Tooltip("Relative chance of a Grid Maze floor: equal blocks on a grid (the old generator's layout; can use RoomBehaviour room prefabs). 0 = never.")]
        [Min(0f)] public float gridMaze = 0.15f;

        public float Get(FloorStyle style)
        {
            switch (style)
            {
                case FloorStyle.Rooms: return rooms;
                case FloorStyle.Bsp: return bsp;
                case FloorStyle.Caverns: return caverns;
                case FloorStyle.Hybrid: return hybrid;
                default: return gridMaze;
            }
        }
    }

    [Serializable]
    public class RoomSizeClass
    {
        [Tooltip("Just a label for the list (not used by generation).")]
        public string name = "Medium";
        [Tooltip("Room side length range (cells). A 6-cell room at 1.5 m is 9 m wide.")]
        public IntRange size = new IntRange(6, 9);
        [Tooltip("Relative chance of this size (before Openness Bias).")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Rooms of this class become Halls: taller (Hall Ceiling), open, preferred for arenas and bosses.")]
        public bool hall;
        [Tooltip("How much Openness raises this class's weight (negative lowers it): big rooms on open floors, small ones on tight floors.")]
        public float opennessBias;
    }

    [Serializable]
    public class RoomShapeWeights
    {
        [Tooltip("Relative chance of a plain rectangular room.")]
        [Min(0f)] public float rectangle = 3f;
        [Tooltip("Relative chance of an L-shaped room.")]
        [Min(0f)] public float lShape = 1f;
        [Tooltip("Relative chance of a T-shaped room.")]
        [Min(0f)] public float tShape = 0.6f;
        [Tooltip("Relative chance of a cross-shaped room.")]
        [Min(0f)] public float cross = 0.5f;
        [Tooltip("Relative chance of a round room.")]
        [Min(0f)] public float circle = 0.6f;
        [Tooltip("Relative chance of a room made of overlapping rectangles.")]
        [Min(0f)] public float composite = 1f;
        [Tooltip("Relative chance of a large room with pillars.")]
        [Min(0f)] public float pillaredHall = 0.5f;
        [Tooltip("Relative chance of a partly collapsed room (rubble, broken walls). Hybrid floors add ruins by zone anyway.")]
        [Min(0f)] public float ruined = 0f;
    }

    [Serializable]
    public class RoomSettings
    {
        [Tooltip("Share of the floor covered by rooms, from low to high Openness (the rest is rock and corridors). Recommended 0.15-0.45.")]
        public FloatRange coverage = new FloatRange(0.2f, 0.38f);
        [Tooltip("The room sizes to pick from, each with its weight. Needs at least one entry.")]
        public RoomSizeClass[] sizes =
        {
            new RoomSizeClass { name = "Closet", size = new IntRange(3, 4), weight = 0.45f, opennessBias = -0.4f },
            new RoomSizeClass { name = "Small", size = new IntRange(4, 6), weight = 1f, opennessBias = -0.3f },
            new RoomSizeClass { name = "Medium", size = new IntRange(6, 9), weight = 1.2f },
            new RoomSizeClass { name = "Large", size = new IntRange(9, 13), weight = 0.55f, opennessBias = 0.6f },
            new RoomSizeClass { name = "Hall", size = new IntRange(13, 18), weight = 0.2f, hall = true, opennessBias = 0.5f },
        };
        [Tooltip("Relative chances of the room shapes.")]
        public RoomShapeWeights shapes = new RoomShapeWeights();
        [Tooltip("How much rooms stretch into rectangles (0 = square, 0.8 = up to 80% longer one way).")]
        [Range(0f, 0.8f)] public float aspectVariation = 0.35f;
        [Tooltip("Solid cells kept between rooms (at least 1 = a wall; more leaves room for corridors).")]
        public IntRange spacing = new IntRange(2, 4);
        [Tooltip("Chance a room is packed one wall away from its neighbour (joined by a door, no corridor). Higher = denser, more 'building-like' floors.")]
        [Range(0f, 1f)] public float tightPackingChance = 0.15f;
        [Tooltip("Room placement tries per floor. More fills big floors better but costs generation time. Recommended 300-1500.")]
        [Min(10)] public int placementAttempts = 700;
        [Tooltip("Candidate positions per room: the one farthest from other rooms wins (1 = random placement; higher spreads rooms evenly).")]
        [Range(1, 8)] public int spreadCandidates = 4;
        [Header("Heights (meters)")]
        [Tooltip("Ceiling height of ordinary rooms (meters). Keep below the profile's Floor Spacing.")]
        public float roomCeiling = 4f;
        [Tooltip("Ceiling height of halls (meters). The profile warns when Floor Spacing is too small for it.")]
        public float hallCeiling = 6.5f;
        [Tooltip("Ceiling height of corridors (meters). At least the player's height plus some headroom (e.g. 2.5+).")]
        public float corridorCeiling = 3.2f;
    }

    [Serializable]
    public class BspSettings
    {
        [Tooltip("Smallest partition side (cells): partitions aren't split below this. Smaller = more, smaller rooms.")]
        [Min(6)] public int minLeafSize = 11;
        [Tooltip("Where a partition is split, as a fraction of its length (0.5 = always in the middle).")]
        public FloatRange splitRatio = new FloatRange(0.38f, 0.62f);
        [Tooltip("Cells left between a room and its partition's edge (room for corridors).")]
        public IntRange roomMargin = new IntRange(1, 3);
        [Tooltip("Chance a partition stays empty (solid rock).")]
        [Range(0f, 0.6f)] public float emptyLeafChance = 0.08f;
        [Tooltip("Chance a partition stops splitting early, leaving a bigger room (lowered by Complexity).")]
        [Range(0f, 0.6f)] public float earlyStopChance = 0.12f;
        [Tooltip("Chance a room is not a plain rectangle (uses Rooms > Shapes).")]
        [Range(0f, 1f)] public float shapedRoomChance = 0.3f;
    }

    [Serializable]
    public class CaveSettings
    {
        [Tooltip("Initial share of rock, from high to low Openness (lower = bigger, more connected caverns). Around 0.45-0.56; small changes have a big effect.")]
        public FloatRange solidFill = new FloatRange(0.46f, 0.555f);
        [Tooltip("Scale of the noise that varies the rock share across the floor (bigger = smaller patches of open and tight cave).")]
        public float fillNoiseScale = 0.06f;
        [Tooltip("How much that noise changes the rock share (0 = uniform caves).")]
        [Range(0f, 0.4f)] public float fillNoiseStrength = 0.2f;
        [Tooltip("Cellular-automaton smoothing passes: more = rounder, smoother caves. Recommended 4-6.")]
        [Range(0, 10)] public int smoothingIterations = 5;
        [Tooltip("A cell becomes rock with at least this many rock neighbours (of 8).")]
        [Range(4, 8)] public int rockThreshold = 5;
        [Tooltip("A cell opens with at most this many rock neighbours (of 8). Must be below Rock Threshold.")]
        [Range(0, 4)] public int openThreshold = 3;
        [Tooltip("Open pockets smaller than this (cells) are filled with rock.")]
        [Min(1)] public int minRegionCells = 40;
        [Tooltip("Passages narrower than this (cells) are widened, so the player always fits.")]
        [Range(1, 3)] public int minPassageWidth = 2;
        [Header("Chambers")]
        [Tooltip("A chamber (an area of its own) is centred at least this far from rock (cells). Larger = fewer, bigger chambers.")]
        public float chamberMinRadius = 2.2f;
        [Tooltip("Minimum distance between chamber centres (cells).")]
        public float chamberSpacing = 8f;
        [Header("Tunnels")]
        [Tooltip("Width of the tunnels carved between chambers (cells).")]
        public IntRange tunnelWidth = new IntRange(2, 3);
        [Tooltip("How much tunnels wander (0 = straight).")]
        [Range(0f, 2f)] public float tunnelWinding = 0.9f;
        [Header("Heights (meters)")]
        [Tooltip("How far cave floors rise and fall (meters, +/-). Keep small enough to walk (slopes are limited anyway).")]
        public float floorHeightAmplitude = 1.1f;
        [Tooltip("Scale of the floor height noise (bigger = shorter bumps).")]
        public float floorHeightScale = 0.07f;
        [Tooltip("Smoothing passes on floor heights (more = gentler slopes).")]
        [Range(0, 12)] public int heightSmoothing = 5;
        [Tooltip("Ceiling height next to cave walls (meters).")]
        public float ceilingBase = 3.2f;
        [Tooltip("Extra ceiling height per cell of distance from the walls: domes in big chambers.")]
        public float ceilingPerWallDistance = 0.55f;
        [Tooltip("Lowest and highest cave ceiling (meters). The highest must stay below the profile's Floor Spacing.")]
        public FloatRange ceilingLimits = new FloatRange(2.8f, 7.5f);
        [Tooltip("Random unevenness of cave ceilings (meters, +/-).")]
        public float ceilingNoise = 0.7f;
        [Header("Surface")]
        [Tooltip("Irregularity of cave walls (fraction of a cell): 0 = smooth, blocky outlines.")]
        [Range(0f, 0.45f)] public float wallRoughness = 0.3f;
        [Tooltip("How far cave walls bulge in and out (meters).")]
        [Range(0f, 1f)] public float wallBulge = 0.3f;
    }

    [Serializable]
    public class HybridSettings
    {
        [Tooltip("Average zone size (cells). Each zone is built rooms, cavern or ruins.")]
        [Min(8f)] public float zoneSize = 22f;
        [Tooltip("How irregular zone borders are (cells, 0 = straight).")]
        public float borderNoise = 6f;
        [Tooltip("Relative share of built (rooms and corridors) zones.")]
        [Min(0f)] public float builtWeight = 1f;
        [Tooltip("Relative share of cavern zones.")]
        [Min(0f)] public float cavernWeight = 1f;
        [Tooltip("Relative share of ruin zones (collapsed rooms with rubble).")]
        [Min(0f)] public float ruinsWeight = 0.5f;
        [Tooltip("How broken ruined rooms are (0 = intact, 0.8 = mostly collapsed).")]
        [Range(0f, 0.8f)] public float ruinsErosion = 0.35f;
    }

    [Serializable]
    public class MazeSettings
    {
        [Tooltip("Room size of each maze block (cells). With RoomBehaviour templates, match their footprint (7 for the old prefabs).")]
        [Min(3)] public int blockSize = 7;
        [Tooltip("Corridor length between blocks (cells, at least 1).")]
        [Min(1)] public int gap = 2;
        [Tooltip("Share of dead-end blocks removed after the maze is built (varies the room count).")]
        public FloatRange pruneFraction = new FloatRange(0f, 0.35f);
        [Tooltip("Optional room templates for the blocks (e.g. the old RoomBehaviour room prefabs). Empty = plain generated rooms.")]
        public RoomTemplate[] templates = new RoomTemplate[0];
    }

    [Serializable]
    public class ConnectionSettings
    {
        [Tooltip("Chance to add each extra (loop-making) connection, from low to high Openness. Loops give alternative routes; 0 = a pure tree (every route is the only one).")]
        public FloatRange loopChance = new FloatRange(0.08f, 0.4f);
        [Tooltip("Largest share of areas that may be dead ends, from low to high Complexity. Extra dead ends get another connection.")]
        public FloatRange deadEndShare = new FloatRange(0.15f, 0.4f);
        [Tooltip("Corridor width (cells): min for normal corridors, max for wide ones.")]
        public IntRange corridorWidth = new IntRange(1, 2);
        [Tooltip("Chance a corridor is wide (Corridor Width max), scaled by Openness.")]
        [Range(0f, 1f)] public float wideCorridorChance = 0.4f;
        [Tooltip("Extra cost per corridor turn (higher = straighter corridors, lower = more winding).")]
        public float turnPenalty = 2.5f;
        [Tooltip("Cost of walking an existing corridor (below 1 = corridors merge into junctions instead of running side by side).")]
        [Range(0.05f, 1f)] public float reuseCost = 0.35f;
        [Tooltip("Candidate connections longer than this (cells) are only used when nothing else connects.")]
        public float maxConnectionLength = 60f;
        [Tooltip("Chance a loop connection between built rooms is hidden behind a secret door.")]
        [Range(0f, 1f)] public float secretChance = 0.12f;
    }

    [Serializable]
    public class LinkSettings
    {
        [Tooltip("Stair well width (cells). 2 = 3 m at the default Cell Size.")]
        [Range(1, 4)] public int stairWidth = 2;
        [Tooltip("Size of the landing rooms at the top and bottom of stairs (cells).")]
        public IntRange landingSize = new IntRange(4, 6);
        [Tooltip("How far a floor's way down is from its way in (share of the largest possible distance): 1 = opposite ends, so players cross the floor.")]
        public FloatRange departureDistance = new FloatRange(0.6f, 1f);
        [Tooltip("Chance of each extra staircase between two floors (a second way up/down).")]
        [Range(0f, 1f)] public float extraStairChance = 0.35f;
        [Tooltip("Most extra staircases between two floors.")]
        [Range(0, 3)] public int maxExtraStairs = 1;
        [Tooltip("Chance of a one-way drop (a pit into the floor below) between two floors.")]
        [Range(0f, 1f)] public float dropChance = 0.3f;
        [Tooltip("Drop shaft size (cells).")]
        [Range(1, 3)] public int dropSize = 2;
        [Tooltip("Cells kept between shafts / anchors and the floor's edge.")]
        [Min(2)] public int edgeMargin = 4;
        [Tooltip("Cells kept between two anchors (stairs, drops, entrance, exit) on the same floor.")]
        [Min(1)] public int anchorSpacing = 3;
        [Header("Entrance and exit rooms (cells)")]
        [Tooltip("Size of the entrance room on the first floor (where the player arrives through the entrance portal).")]
        public IntRange entranceSize = new IntRange(6, 8);
        [Tooltip("Size of the exit room on the last floor (with the exit portal; usually next to the boss).")]
        public IntRange exitSize = new IntRange(7, 9);
    }

    [Serializable]
    public class RoleRule
    {
        [Tooltip("Label for this rule (shown in reports and the preview).")]
        public string name = "Treasure";
        [Tooltip("The role given to the chosen areas (Boss, Treasure, Rest, Arena, Shrine, Secret, or Custom with a Tag). Roles decide loot, props, encounters and templates.")]
        public AreaRole role = AreaRole.Treasure;
        [Tooltip("Custom tag (with role Custom, or to tell same-role variants apart). Props and templates can target it.")]
        public string tag = "";
        [Tooltip("Must appear: a generation that can't place it is retried with another seed (use for the boss). Optional rules are skipped when they don't fit.")]
        public bool required;
        [Tooltip("How many per floor (min, max).")]
        public IntRange perFloor = new IntRange(0, 1);
        [Tooltip("At most this many in the whole dungeon (-1 = no limit).")]
        public int maxTotal = -1;
        [Tooltip("First floor this role may appear on (0 = the top floor).")]
        public int minFloor;
        [Tooltip("Last floor this role may appear on (-1 = no limit).")]
        public int maxFloor = -1;
        [Tooltip("Only on the deepest floor (e.g. the boss).")]
        public bool lastFloorOnly;
        [Tooltip("Where along the entrance-to-exit progression (0 = near the entrance, 1 = near the exit).")]
        public FloatRange progress = new FloatRange(0f, 1f);
        [Tooltip("Which areas qualify.\n\nAny: any area.\nLeaf: dead ends (rewards for exploring).\nOn Main Path: on the entrance-to-exit route.\nOff Main Path: side areas.\nHub: areas with many connections.\nEnd Of Main Path: the area at the end of the route (the boss).")]
        public RolePlacement placement = RolePlacement.Any;
        [Tooltip("Preferred area size (Large for arenas and bosses, Small for secrets).")]
        public SizePreference size = SizePreference.Any;
        [Tooltip("Smallest area (cells) that qualifies (relaxed when nothing fits).")]
        [Min(0)] public int minCells;
        [Tooltip("Zone styles it may appear in (Built, Cavern, Ruins).")]
        public ZoneMask styles = ZoneMask.All;
        [Tooltip("Chance to place this role on a floor where it's optional.")]
        [Range(0f, 1f)] public float chance = 1f;
        [Tooltip("Relative preference when several optional rules compete for the same area.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Optional room templates fitted into the chosen area when one fits (e.g. a boss hall).")]
        public RoomTemplate[] templates = new RoomTemplate[0];
        [Tooltip("Connections into this area are hidden behind secret doors.")]
        public bool secretEntrance;
    }

    [Serializable]
    public class PopulationSettings
    {
        [Tooltip("Which mobs appear (prefabs, weights, costs, packs). Empty = placeholder capsules (see Placeholder Mobs). The Encounter table inspector can import EndlessTerrain's mob list.")]
        public DungeonEncounterTable encounters;
        [Tooltip("Which loot appears (prefabs or built-in chests, tiers, placements). Empty = built-in chests (with Default Loot).")]
        public DungeonLootTable loot;
        [Tooltip("Props, lights, traps and decorations. Empty = the built-in set (with Default Props).")]
        public DungeonPropTable props;
        [Tooltip("Use built-in loot (primitive chests) when no loot table is set.")]
        public bool defaultLoot = true;
        [Tooltip("Use built-in props (torches, crystals, barrels, altars, traps...) when no prop table is set.")]
        public bool defaultProps = true;
        [Tooltip("Put placeholder capsule mobs where mobs would go when no encounter table is set (handy while testing).")]
        public bool placeholderMobs = true;
        [Header("Encounters")]
        [Tooltip("Mob budget (cost units) per 100 walkable cells at difficulty 1. Recommended 1-4.")]
        public float encounterDensity = 2.2f;
        [Tooltip("No mobs within this walking distance (cells) of the player's spawn.")]
        public float safeRadius = 16f;
        [Tooltip("Minimum distance between packs (cells).")]
        public float packSpacing = 5f;
        [Tooltip("Chance a corridor gets an encounter.")]
        [Range(0f, 1f)] public float corridorEncounterChance = 0.12f;
        [Header("Loot")]
        [Tooltip("Loot items in each treasure room.")]
        public IntRange treasureRoomLoot = new IntRange(2, 4);
        [Tooltip("Loot items in the boss room.")]
        public IntRange bossLoot = new IntRange(1, 2);
        [Tooltip("Chance a dead end has loot (rewards exploring).")]
        [Range(0f, 1f)] public float deadEndLootChance = 0.55f;
        [Tooltip("Chance an ordinary room has loot.")]
        [Range(0f, 1f)] public float roomLootChance = 0.15f;
        [Header("Limits")]
        [Tooltip("Safety cap on placements (mobs, loot, props) per floor.")]
        [Min(10)] public int maxPlacementsPerFloor = 500;
    }

    [Serializable]
    public class ValidationSettings
    {
        [Tooltip("Whole-dungeon retries (with derived seeds) when a check fails. If every attempt fails, the Portal puts the player back and logs why.")]
        [Range(1, 20)] public int maxAttempts = 6;
        [Tooltip("Carve a passage to areas that turned out unreachable instead of retrying.")]
        public bool repairUnreachable = true;
        [Tooltip("Most repair passages per floor before the attempt is retried.")]
        [Range(0, 64)] public int maxRepairsPerFloor = 16;
        [Tooltip("Log the generation report (timings, retries, warnings) to the Console after each dungeon.")]
        public bool logReport;
    }

    [Serializable]
    public class BuildSettings
    {
        [Tooltip("Main-thread time per frame spent creating the dungeon's objects (ms). Higher = built sooner but bigger hitches. Recommended 4-8.")]
        [Min(0.5f)] public float frameBudgetMs = 6f;
        [Tooltip("Only keep floors near the player active (saves rendering and physics on deep dungeons).")]
        public bool streamFloors = true;
        [Tooltip("Floors above and below the player's that stay active (with Stream Floors).")]
        [Range(0, 4)] public int floorsAround = 1;
        [Tooltip("Bake a NavMesh per floor (joined across floors by NavMeshLinks on the stairs). Needed for mobs to move. Requires the AI Navigation package.")]
        public bool bakeNavMesh = true;
        [Tooltip("NavMesh agent type the floors are baked for (0 = Humanoid, the default). Must match your mobs' NavMeshAgent Agent Type.")]
        public int navMeshAgentTypeId;
        [Tooltip("Layer of the generated geometry (walls, floors, stairs). Default layer = 0.")]
        public int geometryLayer;
        [Tooltip("Cells per mesh chunk side: smaller chunks cull better, larger means fewer objects.")]
        [Range(8, 64)] public int meshChunkCells = 24;
        [Tooltip("Build ceilings (turn off for a top-down camera).")]
        public bool buildCeilings = true;
        [Tooltip("Generated geometry casts shadows.")]
        public bool castShadows = true;
        [Tooltip("Use the theme's tile-kit prefabs (floor, wall, ceiling, door frame, pillar) for built areas when the theme has them. Caves always use generated meshes.")]
        public bool useTileKit = true;
        [Tooltip("Door frames at door cells.")]
        public bool doorFrames = true;
        [Tooltip("Mark generated geometry static (for batching) when built in the editor (Preview). Ignored in Play mode.")]
        public bool markStatic = true;
    }
}
