using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Everything that shapes a kind of dungeon: grid scale, floor count and sizes, which floor styles appear, how
    /// open and complex floors are, room/cave/hybrid/maze settings, connections, stairs and drops, room roles,
    /// population tables, the visual theme, validation and build settings. A <see cref="DungeonRequest"/> (seed,
    /// size, difficulty) picks one dungeon out of the space a profile describes.
    ///
    /// One profile = one kind of dungeon (e.g. "Crypt", "Deep Caves"). Every value has a working default: a new
    /// profile with nothing assigned builds complete dungeons with placeholder art. Assign a Theme for your materials
    /// or modular prefabs, and an Encounter table for your mobs.
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Dungeon/Dungeon Profile", fileName = "DungeonProfile")]
    public class DungeonProfile : ScriptableObject
    {
        [Header("Scale")]
        [Tooltip("World size of one grid cell (meters). Walls are one cell thick, doors one cell wide, corridors 1-2 cells. 1.5 suits a human-sized player; with a tile kit, set it to the kit's module size. Recommended 1-3.")]
        [Min(0.5f)] public float cellSize = 1.5f;
        [Tooltip("Vertical distance between floors (meters). Must exceed the tallest ceiling (Hall Ceiling, cave Ceiling Limits max) plus cave floor variation - the profile warns otherwise. Also sets how long stairs are.")]
        [Min(5f)] public float floorSpacing = 10f;
        [Tooltip("Steepest stair slope (degrees): with Floor Spacing it sets how long stair wells are. Your character must be able to climb it (CharacterController Slope Limit). Recommended 30-35.")]
        [Range(20f, 45f)] public float maxStairSlope = 33f;

        [Header("Floors")]
        [Tooltip("Number of floors (min, max), used by sizes whose Floor Count is (0, 0).")]
        public Vector2Int floorCount = new Vector2Int(2, 4);
        [Tooltip("What Small / Medium / Large / Huge mean: floor size in cells and floor count. The Portal's Dungeon Size (or the DungeonRequest) picks one.")]
        public SizeClassSettings[] sizeClasses =
        {
            new SizeClassSettings { size = SizeClass.Small, floorCells = new Vector2Int(46, 46), floorCount = new Vector2Int(1, 2) },
            new SizeClassSettings { size = SizeClass.Medium, floorCells = new Vector2Int(64, 64) },
            new SizeClassSettings { size = SizeClass.Large, floorCells = new Vector2Int(86, 86), floorCount = new Vector2Int(3, 5) },
            new SizeClassSettings { size = SizeClass.Huge, floorCells = new Vector2Int(112, 112), floorCount = new Vector2Int(4, 7) },
        };
        [Tooltip("How much each floor's size and position vary (share of the size), so floors don't stack as identical squares.")]
        [Range(0f, 0.4f)] public float footprintVariation = 0.15f;
        [Tooltip("Openness range per floor (each floor rolls a value in it): small rooms and tight passages (0) to big rooms, open caverns and many loops (1).")]
        public FloatRange openness = new FloatRange(0.3f, 0.75f);
        [Tooltip("Complexity range per floor: few rooms and branches (0) to many rooms, side paths and winding routes (1).")]
        public FloatRange complexity = new FloatRange(0.3f, 0.75f);
        [Tooltip("Relative chance of each floor style. At least one must be above 0. Set one alone to get a single-style dungeon.")]
        public StyleWeights styles = new StyleWeights();
        [Tooltip("Added to the Caverns and Hybrid weights per floor of depth (deeper = more natural). 0 = the same mix on every floor.")]
        public float naturalWeightPerFloor = 0.25f;
        [Tooltip("Multiplies the weight of the previous floor's style (below 1 = more variety between floors, 1 = no effect).")]
        [Range(0f, 1f)] public float repeatStylePenalty = 0.45f;
        [Tooltip("Difficulty added per floor below the first (more and tougher mobs, better loot deeper down).")]
        public float difficultyPerFloor = 0.2f;

        [Header("Styles")]
        [Tooltip("Rooms floors: coverage, room sizes and shapes, spacing, ceiling heights (also used by the built rooms of Hybrid and BSP floors).")]
        public RoomSettings rooms = new RoomSettings();
        [Tooltip("BSP floors: how the floor is split into rooms.")]
        public BspSettings bsp = new BspSettings();
        [Tooltip("Caverns (and the cave zones of Hybrid floors): rock share, smoothing, chambers, tunnels, heights, wall roughness.")]
        public CaveSettings caves = new CaveSettings();
        [Tooltip("Hybrid floors: zone size and the share of built, cavern and ruin zones.")]
        public HybridSettings hybrid = new HybridSettings();
        [Tooltip("Grid Maze floors: block size, gaps, pruning, and optional block templates (old RoomBehaviour rooms).")]
        public MazeSettings maze = new MazeSettings();

        [Header("Connections and floors")]
        [Tooltip("How areas are joined: loops, dead ends, corridor width and shape, secret doors.")]
        public ConnectionSettings connections = new ConnectionSettings();
        [Tooltip("Stairs, drops, landings, and the entrance and exit rooms.")]
        public LinkSettings links = new LinkSettings();

        [Header("Roles")]
        [Tooltip("Special rooms: which areas become Boss, Treasure, Rest, Arena, Shrine, Secret (or your Custom tags), where, and how many. The Default Roles button restores a sensible set (boss required on the last floor).")]
        public List<RoleRule> roles = DefaultRoles();

        [Header("Population")]
        [Tooltip("Mobs, loot and props: the tables (optional - built-in placeholders are used when empty) and how densely they are placed.")]
        public PopulationSettings population = new PopulationSettings();

        [Header("Presentation")]
        [Tooltip("OPTIONAL. Materials, colours, the modular tile kit (floor/wall/ceiling/door-frame/pillar prefabs), portal prefabs and atmosphere. Empty = flat-coloured placeholder materials and built-in portals.")]
        public DungeonTheme theme;
        [Tooltip("How the dungeon's scene objects are built: frame budget, floor streaming, NavMesh, layers, ceilings, tile kit.")]
        public BuildSettings build = new BuildSettings();

        [Header("Validation")]
        [Tooltip("Retries, repairs of unreachable areas, and the generation report.")]
        public ValidationSettings validation = new ValidationSettings();

        [Header("Extensions")]
        [Tooltip("OPTIONAL. Project-specific generation stages (see DungeonStageAsset), run on the worker thread in their slot.")]
        public List<DungeonStageAsset> customStages = new List<DungeonStageAsset>();

        /// <summary>The settings of a size class (falls back to Medium).</summary>
        public SizeClassSettings GetSize(SizeClass size)
        {
            if (sizeClasses != null)
            {
                foreach (SizeClassSettings s in sizeClasses)
                    if (s != null && s.size == size)
                        return s;
                foreach (SizeClassSettings s in sizeClasses)
                    if (s != null && s.size == SizeClass.Medium)
                        return s;
            }
            return new SizeClassSettings();
        }

        /// <summary>Length of a stair well (cells) for this profile's floor spacing and slope.</summary>
        public int StairLengthCells()
        {
            float run = floorSpacing / Mathf.Tan(maxStairSlope * Mathf.Deg2Rad);
            return Mathf.Max(3, Mathf.CeilToInt(run / cellSize));
        }

        private void Reset()
        {
            roles = DefaultRoles();
        }

        private void OnValidate()
        {
            floorCount.x = Mathf.Max(1, floorCount.x);
            floorCount.y = Mathf.Max(floorCount.x, floorCount.y);
            // Same rule as CompiledProfile.MaxCeiling: the tallest ceiling plus cave floor variation plus some rock.
            float tallest = Mathf.Max(rooms.hallCeiling, caves.ceilingLimits.max) + caves.floorHeightAmplitude + 0.8f;
            if (floorSpacing < tallest)
                Debug.LogWarning($"{name}: Floor Spacing ({floorSpacing}) is less than the tallest ceiling plus cave floor variation ({tallest:0.0}); ceilings will be clamped.", this);
        }

        /// <summary>A sensible default set of roles.</summary>
        public static List<RoleRule> DefaultRoles()
        {
            return new List<RoleRule>
            {
                new RoleRule
                {
                    name = "Boss", role = AreaRole.Boss, required = true, perFloor = new IntRange(1, 1), maxTotal = 1,
                    lastFloorOnly = true, placement = RolePlacement.EndOfMainPath, size = SizePreference.Large, minCells = 30,
                },
                new RoleRule
                {
                    name = "Treasure", role = AreaRole.Treasure, perFloor = new IntRange(0, 2), progress = new FloatRange(0.15f, 1f),
                    placement = RolePlacement.Leaf, chance = 0.85f,
                },
                new RoleRule
                {
                    name = "Rest", role = AreaRole.Rest, perFloor = new IntRange(0, 1), progress = new FloatRange(0.3f, 0.85f),
                    placement = RolePlacement.OnMainPath, size = SizePreference.Small, chance = 0.5f,
                },
                new RoleRule
                {
                    name = "Arena", role = AreaRole.Arena, perFloor = new IntRange(0, 1), progress = new FloatRange(0.2f, 0.9f),
                    placement = RolePlacement.OnMainPath, size = SizePreference.Large, minCells = 50, chance = 0.45f,
                },
                new RoleRule
                {
                    name = "Shrine", role = AreaRole.Shrine, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.4f,
                },
                new RoleRule
                {
                    name = "Secret", role = AreaRole.Secret, perFloor = new IntRange(0, 1), placement = RolePlacement.Leaf,
                    size = SizePreference.Small, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f, secretEntrance = true,
                },
            };
        }
    }
}
