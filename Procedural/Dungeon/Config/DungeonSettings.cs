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
        [Tooltip("Relative chance of a Citadel floor: a fortress around a central keep - rings of rooms, gates into the keep, corner towers. 0 = never.")]
        [Min(0f)] public float citadel = 0.35f;
        [Tooltip("Relative chance of a Catacombs floor: ossuary chambers joined by long galleries lined with burial niches. 0 = never.")]
        [Min(0f)] public float catacombs = 0.3f;
        [Tooltip("Relative chance of a Tower floor: a small round floor around a spiral stair core. When every floor is a Tower floor the dungeon is one tall tower: the floors stack exactly and the spiral stairs join them. 0 = never.")]
        [Min(0f)] public float tower = 0.15f;
        [Tooltip("Relative chance of an Undercity floor: streets and plazas under a high cavern sky, with buildings you can enter. 0 = never.")]
        [Min(0f)] public float undercity = 0.25f;
        [Tooltip("Relative chance of a Hive floor: round organic cells packed like a honeycomb, joined by fleshy tunnels. 0 = never.")]
        [Min(0f)] public float hive = 0.25f;
        [Tooltip("Relative chance of an Islands floor: rock platforms over a chasm, joined by bridges and moving platforms. Falling drops you to the floor below (or kills on the last floor). 0 = never.")]
        [Min(0f)] public float islands = 0.2f;
        [Tooltip("Relative chance of a Den floor: one huge cavern with a hoard, side tunnels and side caves (best as the boss floor). 0 = never.")]
        [Min(0f)] public float den = 0.12f;
        [Tooltip("Relative chance of an Astral floor: floating platforms in a void joined by portals, moving platforms and a few bridges, where gravity flips every so often. 0 = never.")]
        [Min(0f)] public float astral = 0.1f;

        public float Get(FloorStyle style)
        {
            switch (style)
            {
                case FloorStyle.Rooms: return rooms;
                case FloorStyle.Bsp: return bsp;
                case FloorStyle.Caverns: return caverns;
                case FloorStyle.Hybrid: return hybrid;
                case FloorStyle.Citadel: return citadel;
                case FloorStyle.Catacombs: return catacombs;
                case FloorStyle.Tower: return tower;
                case FloorStyle.Undercity: return undercity;
                case FloorStyle.Hive: return hive;
                case FloorStyle.Islands: return islands;
                case FloorStyle.Den: return den;
                case FloorStyle.Astral: return astral;
                default: return gridMaze;
            }
        }

        /// <summary>Sets one style's weight.</summary>
        public void Set(FloorStyle style, float weight)
        {
            weight = Mathf.Max(0f, weight);
            switch (style)
            {
                case FloorStyle.Rooms: rooms = weight; break;
                case FloorStyle.Bsp: bsp = weight; break;
                case FloorStyle.Caverns: caverns = weight; break;
                case FloorStyle.Hybrid: hybrid = weight; break;
                case FloorStyle.Citadel: citadel = weight; break;
                case FloorStyle.Catacombs: catacombs = weight; break;
                case FloorStyle.Tower: tower = weight; break;
                case FloorStyle.Undercity: undercity = weight; break;
                case FloorStyle.Hive: hive = weight; break;
                case FloorStyle.Islands: islands = weight; break;
                case FloorStyle.Den: den = weight; break;
                case FloorStyle.Astral: astral = weight; break;
                default: gridMaze = weight; break;
            }
        }

        /// <summary>Sum of every weight (0 = no style can be picked).</summary>
        public float Total => rooms + bsp + caverns + hybrid + gridMaze + citadel + catacombs + tower + undercity + hive + islands + den + astral;

        /// <summary>True when only Tower floors can be picked (a tower dungeon: stacked floors joined by spiral stairs).</summary>
        public bool OnlyTowers => tower > 0f && Total - tower <= 0f;
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
        [Tooltip("Relative chance of an octagonal room (a rectangle with its corners cut).")]
        [Min(0f)] public float octagon = 0.7f;
        [Tooltip("Relative chance of a cloister: a ring of floor around a solid core (needs 9+ cells).")]
        [Min(0f)] public float ring = 0.25f;
        [Tooltip("Relative chance of an apse: a long room ending in a half circle, like a chapel.")]
        [Min(0f)] public float apse = 0.4f;
        [Tooltip("Relative chance of a diamond (a square turned 45 degrees).")]
        [Min(0f)] public float diamond = 0.25f;
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
        [Tooltip("Ceiling height of ordinary rooms (meters). Bigger rooms get more (Ceilings > Extra Per Room Cell), and every height is multiplied by Ceilings > Height Scale.")]
        public float roomCeiling = 5f;
        [Tooltip("Ceiling height of halls (meters), before the size bonus and vaulting (see the profile's Ceilings group).")]
        public float hallCeiling = 7.5f;
        [Tooltip("Ceiling height of corridors (meters). At least the player's height plus room for the camera (3.5+ for a third-person camera).")]
        public float corridorCeiling = 3.6f;
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
        public float ceilingBase = 4f;
        [Tooltip("Extra ceiling height per cell of distance from the walls: domes in big chambers.")]
        public float ceilingPerWallDistance = 0.6f;
        [Tooltip("Lowest and highest cave ceiling (meters, before Ceilings > Height Scale). With Auto Floor Spacing the floors make room for the highest.")]
        public FloatRange ceilingLimits = new FloatRange(3.4f, 10f);
        [Tooltip("Random unevenness of cave ceilings (meters, +/-).")]
        public float ceilingNoise = 0.8f;
        [Header("Surface")]
        [Tooltip("Irregularity of cave walls (fraction of a cell): 0 = smooth, blocky outlines.")]
        [Range(0f, 0.45f)] public float wallRoughness = 0.3f;
        [Tooltip("How far cave walls bulge in and out (meters).")]
        [Range(0f, 1f)] public float wallBulge = 0.3f;
    }

    /// <summary>Ready-made sets of ceiling heights (see <see cref="CeilingSettings.ApplyPreset"/>).</summary>
    public enum CeilingPreset
    {
        /// <summary>The original low ceilings: rooms 4 m, halls 6.5 m, corridors 3.2 m, no vaults.</summary>
        Classic,
        /// <summary>Rooms 5 m, halls 7.5 m, corridors 3.6 m, bigger rooms taller, half the halls vaulted.</summary>
        Standard,
        /// <summary>Rooms 6.5 m, halls 10 m, corridors 4.5 m, tall caves, most halls vaulted.</summary>
        Tall,
        /// <summary>Rooms 8 m, halls 14 m, corridors 5.5 m, huge vaults and caverns.</summary>
        Cathedral,
    }

    [Serializable]
    public class CeilingSettings
    {
        [Tooltip("Multiplies EVERY ceiling height - rooms, halls, corridors, caves, room templates and special rooms (1 = as set). The quickest way to make the whole dungeon taller (1.5) or lower (0.8).")]
        [Range(0.5f, 3f)] public float heightScale = 1f;
        [Tooltip("Bigger rooms get taller ceilings: extra meters per cell of the room's shorter side beyond Size Scaling From. 0 = every room gets the plain Room / Hall Ceiling.")]
        [Min(0f)] public float extraPerRoomCell = 0.2f;
        [Tooltip("Rooms whose shorter side is at most this many cells keep the plain Room / Hall Ceiling.")]
        [Min(1)] public int sizeScalingFrom = 6;
        [Tooltip("Most extra height a room gets from its size (meters).")]
        [Min(0f)] public float maxSizeBonus = 2.5f;
        [Tooltip("Chance a hall or a large room gets a VAULTED ceiling: it rises towards the middle of the room like an arch (pillars make cross vaults). Special rooms set their own (Roles > Vaulted).")]
        [Range(0f, 1f)] public float vaultChance = 0.5f;
        [Tooltip("Rooms with at least this many cells may be vaulted (halls always may).")]
        [Min(0)] public int vaultMinCells = 70;
        [Tooltip("How much higher a vaulted ceiling is in the middle of the room than at its walls (meters).")]
        [Min(0f)] public float vaultHeight = 2.5f;
        [Tooltip("Lowest clearance anywhere, floor to ceiling (meters).")]
        [Min(2f)] public float minHeadroom = 2.6f;
        [Tooltip("Height of door openings (meters); the door frame's header fills the rest up to the ceiling.")]
        [Min(2f)] public float doorHeight = 3f;
        [Tooltip("ON: floors move further apart when the ceilings need it (stairs get longer, nothing is cut). OFF: Floor Spacing is kept and ceilings that don't fit are lowered.")]
        public bool autoFloorSpacing = true;
        [Tooltip("Solid rock kept between a ceiling and the floor above (meters).")]
        [Min(0.3f)] public float rockBetweenFloors = 0.8f;

        /// <summary>Extra height of a built room from its size (meters, before Height Scale).</summary>
        public float SizeBonus(int shorterSide) => Mathf.Min(maxSizeBonus, extraPerRoomCell * Mathf.Max(0, shorterSide - sizeScalingFrom));

        /// <summary>Sets the room, hall, corridor and cave heights and these settings to one of the presets.</summary>
        public static void ApplyPreset(CeilingPreset preset, RoomSettings rooms, CaveSettings caves, CeilingSettings ceilings)
        {
            // room, hall, corridor, cave base, cave per wall, cave max, cave min, extra per cell, max bonus, vault chance, vault height, door
            float[] v;
            switch (preset)
            {
                case CeilingPreset.Classic: v = new[] { 4f, 6.5f, 3.2f, 3.2f, 0.55f, 7.5f, 2.8f, 0f, 0f, 0f, 0f, 2.6f }; break;
                case CeilingPreset.Tall: v = new[] { 6.5f, 10f, 4.5f, 5f, 0.7f, 13f, 4f, 0.3f, 3.5f, 0.65f, 3.5f, 3.4f }; break;
                case CeilingPreset.Cathedral: v = new[] { 8f, 14f, 5.5f, 6f, 0.9f, 17f, 5f, 0.4f, 5f, 0.8f, 5f, 4f }; break;
                default: v = new[] { 5f, 7.5f, 3.6f, 4f, 0.6f, 10f, 3.4f, 0.2f, 2.5f, 0.5f, 2.5f, 3f }; break;
            }
            if (rooms != null)
            {
                rooms.roomCeiling = v[0];
                rooms.hallCeiling = v[1];
                rooms.corridorCeiling = v[2];
            }
            if (caves != null)
            {
                caves.ceilingBase = v[3];
                caves.ceilingPerWallDistance = v[4];
                caves.ceilingLimits = new FloatRange(v[6], v[5]);
            }
            if (ceilings != null)
            {
                ceilings.heightScale = 1f;
                ceilings.extraPerRoomCell = v[7];
                ceilings.maxSizeBonus = v[8];
                ceilings.vaultChance = v[9];
                ceilings.vaultHeight = v[10];
                ceilings.doorHeight = v[11];
                ceilings.minHeadroom = Mathf.Min(ceilings.minHeadroom, v[2]);
                ceilings.autoFloorSpacing = true;
            }
        }
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
    public class CitadelSettings
    {
        [Tooltip("Size of the central keep as a share of the floor's shorter side, from low to high Openness.")]
        public FloatRange keepSize = new FloatRange(0.2f, 0.3f);
        [Tooltip("Relative chance of an octagonal keep.")]
        [Min(0f)] public float keepOctagon = 1f;
        [Tooltip("Relative chance of a round keep.")]
        [Min(0f)] public float keepRound = 0.7f;
        [Tooltip("Relative chance of a cross-shaped keep.")]
        [Min(0f)] public float keepCross = 0.5f;
        [Tooltip("Relative chance of a pillared keep.")]
        [Min(0f)] public float keepPillared = 0.8f;
        [Tooltip("Relative chance of a cloister keep (a ring around a solid core).")]
        [Min(0f)] public float keepCloister = 0.5f;
        [Tooltip("Side of the rooms on the rings (cells), from high to low Complexity (complex floors: smaller, more rooms).")]
        public IntRange roomSize = new IntRange(4, 7);
        [Tooltip("Cells between the keep and the first ring, and between rings (room for the ring corridors).")]
        public IntRange ringGap = new IntRange(3, 5);
        [Tooltip("Cells between rooms along a ring.")]
        [Min(1f)] public float roomSpacing = 3f;
        [Tooltip("Most rings of rooms (the floor's size limits it too).")]
        [Range(1, 4)] public int maxRings = 3;
        [Tooltip("Gates from the inner ring into the keep.")]
        public IntRange gates = new IntRange(2, 4);
        [Tooltip("Chance that two neighbouring rooms of an outer ring are joined (the inner ring is always a full circle).")]
        [Range(0f, 1f)] public float ringLinkChance = 0.7f;
        [Tooltip("Round towers in the floor's corners.")]
        public bool cornerTowers = true;
        [Tooltip("Tower size (cells).")]
        public IntRange towerSize = new IntRange(6, 8);
    }

    [Serializable]
    public class CatacombSettings
    {
        [Tooltip("Cells between neighbouring chambers, from high to low Complexity (complex floors: a tighter lattice, more chambers).")]
        public IntRange pitch = new IntRange(10, 14);
        [Tooltip("Side of the ordinary ossuary chambers (cells).")]
        public IntRange chamberSize = new IntRange(3, 5);
        [Tooltip("Chance a chamber is a bigger crypt hall.")]
        [Range(0f, 1f)] public float hallChance = 0.15f;
        [Tooltip("Side of the crypt halls (cells).")]
        public IntRange hallSize = new IntRange(6, 8);
        [Tooltip("Width of the galleries (cells), from low to high Openness.")]
        public IntRange galleryWidth = new IntRange(2, 3);
        [Tooltip("Chance of each extra gallery that makes a loop, from low to high Openness (0 = a perfect maze).")]
        public FloatRange loopChance = new FloatRange(0.1f, 0.35f);
        [Tooltip("Chance of a burial niche at each spot along a gallery, from low to high Complexity.")]
        public FloatRange nicheChance = new FloatRange(0.25f, 0.6f);
        [Tooltip("Cells between niche spots along a gallery.")]
        public IntRange nicheEvery = new IntRange(3, 5);
        [Tooltip("Niche width along the gallery (cells).")]
        public IntRange nicheSize = new IntRange(2, 3);
        [Tooltip("Niche depth away from the gallery (cells).")]
        public IntRange nicheDepth = new IntRange(2, 3);
    }


    [Serializable]
    public class TowerSettings
    {
        [Tooltip("Diameter of a tower floor (cells). In a tower dungeon (only Tower floors) every floor has the same footprint, so the floors stack exactly. Smaller = tighter floors.")]
        public IntRange diameter = new IntRange(28, 36);
        [Tooltip("Side of the square stair core holding the spiral staircase (cells). 5 = 7.5 m at the default Cell Size.")]
        [Range(4, 8)] public int coreSize = 5;
        [Tooltip("Width of the ring hall around the stair core (cells).")]
        public IntRange ringWidth = new IntRange(2, 3);
        [Tooltip("Chambers around the ring hall, from low to high Complexity.")]
        public IntRange chambers = new IntRange(4, 8);
        [Tooltip("Chance a tower floor is one great round hall (with columns) instead of a ring hall and chambers.")]
        [Range(0f, 1f)] public float greatHallChance = 0.2f;
        [Tooltip("Steepest slope of the spiral stairs (degrees), measured where the steps are steepest - next to the central column. More turns are added until it is met.")]
        [Range(25f, 50f)] public float spiralSlope = 40f;
        [Tooltip("Radius of the spiral stair's central column (meters). A thicker column keeps the inside of the steps walkable.")]
        [Range(0.4f, 2.5f)] public float newelRadius = 1.1f;
        [Tooltip("Chance of an outside balcony room hanging off the tower wall on each floor.")]
        [Range(0f, 1f)] public float balconyChance = 0.3f;
    }

    [Serializable]
    public class UndercitySettings
    {
        [Tooltip("Width of the main avenues (cells).")]
        public IntRange avenueWidth = new IntRange(3, 4);
        [Tooltip("Width of the side streets (cells).")]
        public IntRange streetWidth = new IntRange(2, 3);
        [Tooltip("Side of a city block between streets (cells), from high to low Complexity (complex floors: smaller blocks, more streets).")]
        public IntRange blockSize = new IntRange(10, 16);
        [Tooltip("Chance a street crossing opens into a plaza.")]
        [Range(0f, 1f)] public float plazaChance = 0.35f;
        [Tooltip("Plaza size (cells).")]
        public IntRange plazaSize = new IntRange(7, 11);
        [Tooltip("Rooms per building (each with its own door or an inner doorway).")]
        public IntRange roomsPerBuilding = new IntRange(1, 3);
        [Tooltip("Chance a lot is left as rubble (a collapsed building).")]
        [Range(0f, 1f)] public float ruinChance = 0.12f;
        [Header("Heights (meters)")]
        [Tooltip("Height of the cavern ceiling over the streets - the city's sky.")]
        public FloatRange skyHeight = new FloatRange(13f, 17f);
        [Tooltip("Ceiling height inside the buildings.")]
        public float interiorCeiling = 4.5f;
        [Tooltip("How far a building's roof rises above its rooms' ceiling (some buildings look a storey taller).")]
        public FloatRange roofAbove = new FloatRange(0.8f, 4f);
    }

    [Serializable]
    public class HiveSettings
    {
        [Tooltip("Radius of the round cells (cells), from low to high Openness.")]
        public FloatRange cellRadius = new FloatRange(2.8f, 4.5f);
        [Tooltip("Rock left between neighbouring cells (cells).")]
        public IntRange wall = new IntRange(1, 2);
        [Tooltip("Chance a cell of the honeycomb is missing (solid rock), from high to low Complexity.")]
        [Range(0f, 0.6f)] public float missingChance = 0.18f;
        [Tooltip("Chance each pair of neighbouring cells is joined by a tunnel, from low to high Openness (the rest only where needed).")]
        public FloatRange joinChance = new FloatRange(0.25f, 0.6f);
        [Tooltip("Chance a cell is merged with a neighbour into a big brood chamber.")]
        [Range(0f, 1f)] public float broodChance = 0.18f;
        [Tooltip("How far the cells' outlines wobble away from a circle (0 = round).")]
        [Range(0f, 1f)] public float wobble = 0.4f;
        [Tooltip("Ceiling height of the cells (meters, lowest at the walls, highest in the middle).")]
        public FloatRange ceiling = new FloatRange(3.6f, 6.5f);
    }

    [Serializable]
    public class IslandSettings
    {
        [Tooltip("Radius of the islands (cells), from low to high Openness.")]
        public FloatRange islandRadius = new FloatRange(3f, 5.5f);
        [Tooltip("Open air between neighbouring islands (cells).")]
        public IntRange gap = new IntRange(3, 6);
        [Tooltip("Share of the joins between islands that are moving platforms instead of bridges.")]
        [Range(0f, 1f)] public float platformShare = 0.3f;
        [Tooltip("Bridge width (cells).")]
        public IntRange bridgeWidth = new IntRange(1, 2);
        [Tooltip("Rock ledge along the floor's edge (cells). 0 = islands only.")]
        [Range(0, 6)] public int rimWidth = 2;
        [Tooltip("Height of the cavern ceiling above the islands (meters).")]
        public FloatRange ceiling = new FloatRange(10f, 14f);
        [Tooltip("Extra depth of the chasm (meters): the floor below sits this much deeper, so the drop looks bottomless.")]
        [Min(0f)] public float chasmDepth = 6f;
    }

    [Serializable]
    public class DenSettings
    {
        [Tooltip("Share of the floor taken by the great cavern.")]
        public FloatRange caveShare = new FloatRange(0.3f, 0.45f);
        [Tooltip("Side tunnels leading out of the great cavern to side caves.")]
        public IntRange sideTunnels = new IntRange(3, 6);
        [Tooltip("Side cave size (cells).")]
        public IntRange sideCaveSize = new IntRange(5, 9);
        [Tooltip("Ceiling of the great cavern at its middle (meters): the den is the tallest space in the dungeon.")]
        public float caveHeight = 16f;
        [Tooltip("Gold piles of the hoard.")]
        public IntRange hoardPiles = new IntRange(6, 12);
    }

    [Serializable]
    public class AstralSettings
    {
        [Tooltip("Radius of the floating platforms (cells), from low to high Openness.")]
        public FloatRange platformRadius = new FloatRange(3f, 5f);
        [Tooltip("Void between neighbouring platforms (cells).")]
        public IntRange gap = new IntRange(4, 8);
        [Tooltip("Share of the joins between platforms that are portals (a pair of teleport pads).")]
        [Range(0f, 1f)] public float portalShare = 0.45f;
        [Tooltip("Share of the joins that are moving platforms (the rest are narrow bridges).")]
        [Range(0f, 1f)] public float platformShare = 0.25f;
        [Tooltip("Height of the void's ceiling (meters).")]
        public FloatRange ceiling = new FloatRange(12f, 16f);
        [Tooltip("Extra depth of the void below the platforms (meters): the floor below sits this much deeper.")]
        [Min(0f)] public float voidDepth = 8f;
        [Header("Gravity")]
        [Tooltip("Seconds between gravity flips on an astral floor (random in the range). 0 = gravity never flips.")]
        public FloatRange flipInterval = new FloatRange(35f, 70f);
        [Tooltip("How long gravity stays flipped (seconds): players float up, can drift across gaps, and fall when it returns.")]
        [Min(1f)] public float flipDuration = 6f;
        [Tooltip("How high players float during a flip (meters).")]
        [Min(0.5f)] public float floatHeight = 3f;
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
        [Tooltip("Chance of a climbing shaft between two floors: vines (down a giant root) that players climb up and down. Needs a player with a PlayerMovementModel (others just drop down).")]
        [Range(0f, 1f)] public float climbChance = 0.12f;
        [Tooltip("Climbing shaft size (cells).")]
        [Range(1, 2)] public int climbSize = 1;
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
        [Tooltip("Ceiling height of this room (meters, before Ceilings > Height Scale). 0 = the normal room / hall height. Use it to make boss halls, arenas and shrines tower over the rest.")]
        [Min(0f)] public float ceilingHeight;
        [Tooltip("Vaulted ceiling: rises towards the middle of the room (by Ceilings > Vault Height).")]
        public bool vaulted;
        [Tooltip("A neighbouring dead end off the main path becomes a hidden room behind a secret door, opened by a hidden lever in this room (wine cellars).")]
        public bool hiddenRoom;
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
        [Tooltip("Loot items in each vault (behind its locked door).")]
        public IntRange vaultLoot = new IntRange(3, 5);
        [Tooltip("Loot items at the far end of a trap gauntlet, behind a solved puzzle, and after a cleared ambush or guardian room.")]
        public IntRange challengeLoot = new IntRange(1, 2);
        [Header("Special rooms")]
        [Tooltip("Special rooms (library, armory, prison, crypt, laboratory, garden, throne, traps, puzzles...) and floor modifiers that the Prop table has no props for get the built-in ones, so they are never empty. Off = only the table's props.")]
        public bool fillMissingRoleProps = true;
        [Header("Limits")]
        [Tooltip("Safety cap on placements (mobs, loot, props) per floor.")]
        [Min(10)] public int maxPlacementsPerFloor = 500;
    }

    [Serializable]
    public class FloorModifierSettings
    {
        [Tooltip("Chance a floor gets a modifier (flooded, molten, overgrown, darkness, frozen). 0 = never.")]
        [Range(0f, 1f)] public float chance = 0.3f;
        [Tooltip("First floor that may have one (0 = the top floor; 1 keeps the first floor plain).")]
        [Min(0)] public int firstFloor = 1;
        [Tooltip("Relative chance of a FLOODED floor: water over the low ground (rooms and corridors wade in it, caves get pools).")]
        [Min(0f)] public float flooded = 1f;
        [Tooltip("Relative chance of a MOLTEN floor: lava pools in caves and ruins (burning), a red glow.")]
        [Min(0f)] public float molten = 0.6f;
        [Tooltip("Relative chance of an OVERGROWN floor: roots, vines, glowing mushrooms and poisonous spore vents.")]
        [Min(0f)] public float overgrown = 0.8f;
        [Tooltip("Relative chance of a DARK floor: few lights, thick fog, more mobs.")]
        [Min(0f)] public float darkness = 0.7f;
        [Tooltip("Relative chance of a FROZEN floor: ice crystals, frost and a cold blue light.")]
        [Min(0f)] public float frozen = 0.5f;
        [Tooltip("Flooded floors: height of the water above the floor (meters).")]
        [Range(0.05f, 1.2f)] public float waterLevel = 0.35f;
        [Tooltip("Dark floors: share of the usual lights kept (boss, rest and shrine rooms keep theirs).")]
        [Range(0f, 1f)] public float darkLightShare = 0.2f;
        [Tooltip("Dark floors: extra mobs (0.25 = a quarter more).")]
        [Min(0f)] public float darkEncounterBonus = 0.25f;
        [Tooltip("Change the ambient light and fog while players are on a floor with a modifier (needs the theme's Apply Atmosphere).")]
        public bool atmosphere = true;

        public float Weight(FloorModifier m)
        {
            switch (m)
            {
                case FloorModifier.Flooded: return flooded;
                case FloorModifier.Molten: return molten;
                case FloorModifier.Overgrown: return overgrown;
                case FloorModifier.Darkness: return darkness;
                case FloorModifier.Frozen: return frozen;
                default: return 0f;
            }
        }
    }

    [Serializable]
    public class RoomEventSettings
    {
        [Tooltip("The boss room's ways out close behind the players until the boss is dead.")]
        public bool lockBossRoom = true;
        [Tooltip("Arenas lock until every mob in them is dead.")]
        public bool lockArenas = true;
        [Tooltip("Guardian (Mini Boss) and Throne rooms lock until their elite and its guards are dead.")]
        public bool lockGuardianRooms = true;
        [Tooltip("Ambush rooms: how many waves of mobs appear (each after the previous one is dead).")]
        public IntRange ambushWaves = new IntRange(2, 3);
        [Tooltip("Chance the dead rise in a crypt: its mobs stay hidden until the players come in, then the doors lock.")]
        [Range(0f, 1f)] public float cryptAmbushChance = 0.5f;
        [Tooltip("Elite mobs (guardian and throne rooms): size multiplier.")]
        [Range(1f, 3f)] public float eliteScale = 1.3f;
        [Tooltip("Elite mobs: tier added (read it from DungeonSpawned.tier to scale health and damage).")]
        [Range(0, 5)] public int eliteTierBonus = 1;
        [Tooltip("Vaults get a locked door and a key somewhere else on the floor. Off = vaults are open treasure rooms.")]
        public bool vaultKeys = true;
        [Tooltip("Puzzle rooms: how many pressure plates.")]
        public IntRange puzzlePlates = new IntRange(3, 4);
        [Tooltip("Chance per floor of a SHORTCUT: a door on a loop that only opens from its far side, so players who reach it get a quick way back.")]
        [Range(0f, 1f)] public float shortcutChance = 0.4f;
        [Tooltip("Rooms with more ways in than this are not locked (wide cave openings).")]
        [Range(1, 16)] public int maxGatesPerRoom = 8;

        [Header("Pit fights, barracks, nests")]
        [Tooltip("Pit fight (colosseum) waves; the champion (an elite) comes with the last one.")]
        public IntRange pitFightWaves = new IntRange(3, 4);
        [Tooltip("Pit fight champion: size multiplier.")]
        [Range(1f, 3f)] public float championScale = 1.45f;
        [Tooltip("Share of a barracks' soldiers that are asleep (sneak past them crouched; they wake when a player comes close, hurts one, or a sleeper beside them wakes).")]
        [Range(0f, 1f)] public float sleeperShare = 0.8f;
        [Tooltip("Nests per nest room.")]
        public IntRange nestsPerRoom = new IntRange(1, 2);

        [Header("Roaming, shifting floors, traps")]
        [Tooltip("Chance per floor of a roaming mini-boss: an elite that walks from room to room.")]
        [Range(0f, 1f)] public float roamerChance = 0.35f;
        [Tooltip("Roaming mini-boss: size multiplier.")]
        [Range(1f, 3f)] public float roamerScale = 1.3f;
        [Tooltip("Chance per floor that its routes reshuffle: some loop passages are walled off and others open every few minutes (never cutting anything off).")]
        [Range(0f, 1f)] public float shiftingChance = 0.25f;
        [Tooltip("Share of a shifting floor's loop passages that get a shifting wall.")]
        [Range(0f, 1f)] public float shiftingShare = 0.6f;
        [Tooltip("Seconds between reshuffles (random in the range).")]
        public FloatRange shiftInterval = new FloatRange(90f, 180f);
        [Tooltip("Chance per long corridor (8+ cells) of a tripwire that fires arrows from the walls.")]
        [Range(0f, 1f)] public float tripwireChance = 0.15f;

        [Header("Chasms")]
        [Tooltip("What falling into a chasm does. Death (default): a black fog fills the chasm and touching it kills. Floor Below: land on the floor below, hurt (Floor Below Else Death: die on the last floor, where the fog is).")]
        public ChasmFall chasmFallRule = ChasmFall.Death;
        [Tooltip("Damage of a fall to the floor below (share of max health).")]
        [Range(0f, 1f)] public float fallDamage = 0.2f;
        [Tooltip("How far below the ledges the black fog's surface lies (meters): falling players die when they touch it.")]
        [Range(0.5f, 8f)] public float voidFogDepth = 1.6f;
    }

    /// <summary>What happens to a player who falls into a chasm.</summary>
    public enum ChasmFall
    {
        /// <summary>Lands on the floor below; on the last floor, dies.</summary>
        FloorBelowElseDeath,
        /// <summary>Always lands on the floor below (the last floor puts the player back at its arrival).</summary>
        FloorBelow,
        /// <summary>Dies in the black fog that fills the chasm.</summary>
        Death,
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
