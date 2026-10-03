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
        [Tooltip("Vertical distance between floors (meters) - the minimum: with Ceilings > Auto Floor Spacing (default) each floor moves further below the one above when its own ceilings need it (a den or an undercity sky needs more than a crypt). Also sets how long stairs are.")]
        [Min(5f)] public float floorSpacing = 12f;
        [Tooltip("Steepest stair slope (degrees): with Floor Spacing it sets how long stair wells are. Your character must be able to climb it (CharacterController Slope Limit). Recommended 30-35.")]
        [Range(20f, 45f)] public float maxStairSlope = 33f;

        [Header("Heights")]
        [Tooltip("How tall the dungeon is: one Height Scale for every ceiling, bigger rooms taller, vaulted halls, door height, and whether floors move apart to fit the ceilings. The base heights are Rooms > Heights and Caverns > Heights; special rooms set their own in Roles. The inspector's Ceiling Heights buttons apply ready-made sets.")]
        public CeilingSettings ceilings = new CeilingSettings();

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
        [Tooltip("Give the last (boss) floor a set style, e.g. Den for a dragon's lair or Citadel for a fortress keep.")]
        public bool overrideLastFloorStyle;
        [Tooltip("The last floor's style when Override Last Floor Style is on.")]
        public FloorStyle lastFloorStyle = FloorStyle.Den;

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
        [Tooltip("Citadel floors: the central keep, rings of rooms, gates and corner towers.")]
        public CitadelSettings citadel = new CitadelSettings();
        [Tooltip("Catacombs floors: chamber lattice, galleries, loops and burial niches.")]
        public CatacombSettings catacombs = new CatacombSettings();
        [Tooltip("Tower floors: diameter, the spiral stair core, ring hall, chambers. A dungeon with only Tower floors is one tower whose floors stack exactly.")]
        public TowerSettings tower = new TowerSettings();
        [Tooltip("Undercity floors: streets, plazas, building blocks, the cavern sky and the buildings' roofs.")]
        public UndercitySettings undercity = new UndercitySettings();
        [Tooltip("Hive floors: the honeycomb of round cells, their tunnels and brood chambers.")]
        public HiveSettings hive = new HiveSettings();
        [Tooltip("Islands floors: island size, the chasm between them, bridges and moving platforms.")]
        public IslandSettings islands = new IslandSettings();
        [Tooltip("Den floors: the great cavern, its side tunnels and caves, the hoard.")]
        public DenSettings den = new DenSettings();
        [Tooltip("Astral floors: floating platforms, portals, moving platforms, and the gravity flips.")]
        public AstralSettings astral = new AstralSettings();

        [Header("Connections and floors")]
        [Tooltip("How areas are joined: loops, dead ends, corridor width and shape, secret doors.")]
        public ConnectionSettings connections = new ConnectionSettings();
        [Tooltip("Stairs, drops, landings, and the entrance and exit rooms.")]
        public LinkSettings links = new LinkSettings();

        [Header("Roles")]
        [Tooltip("Special rooms: which areas become Boss, Treasure, Rest, Arena, Shrine, Secret (or your Custom tags), where, and how many. The Default Roles button restores a sensible set (boss required on the last floor).")]
        public List<RoleRule> roles = DefaultRoles();

        [Header("Special rooms, mechanics and floor modifiers")]
        [Tooltip("Room events: locked boss / arena / guardian rooms, ambush waves, crypts where the dead rise, elite mobs, vault keys, puzzle plates and shortcut doors.")]
        public RoomEventSettings mechanics = new RoomEventSettings();
        [Tooltip("Floor-wide twists rolled per floor: flooded, molten, overgrown, dark and frozen floors (light, props, hazards).")]
        public FloorModifierSettings floorModifiers = new FloorModifierSettings();

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

        /// <summary>Length of a stair well (cells) for this profile's (effective) floor spacing and slope.</summary>
        public int StairLengthCells() => StairLengthCells(EffectiveFloorSpacing());

        /// <summary>Length of a stair well (cells) for a floor spacing.</summary>
        public int StairLengthCells(float spacing)
        {
            float run = spacing / Mathf.Tan(maxStairSlope * Mathf.Deg2Rad);
            return Mathf.Max(3, Mathf.CeilToInt(run / Mathf.Max(0.1f, cellSize)));
        }

        /// <summary>
        /// The tallest ceiling this profile can produce above a floor (meters, Height Scale included): rooms and halls with
        /// their size bonus and vault, corridors, caves, special rooms and room templates.
        /// </summary>
        public float TallestCeiling()
        {
            CeilingSettings c = ceilings ?? new CeilingSettings();
            float vault = c.vaultChance > 0f ? c.vaultHeight : 0f;
            float bonus = c.extraPerRoomCell > 0f ? c.maxSizeBonus : 0f;
            float tallest = 0f;
            if (rooms != null)
                tallest = Mathf.Max(Mathf.Max(rooms.roomCeiling, rooms.hallCeiling) + bonus + vault, rooms.corridorCeiling);
            if (caves != null)
                tallest = Mathf.Max(tallest, caves.ceilingLimits.max);
            if (roles != null)
            {
                foreach (RoleRule r in roles)
                {
                    if (r == null)
                        continue;
                    if (r.ceilingHeight > 0f)
                        tallest = Mathf.Max(tallest, r.ceilingHeight + (r.vaulted ? c.vaultHeight : 0f));
                    else if (r.vaulted)
                        tallest = Mathf.Max(tallest, (rooms != null ? Mathf.Max(rooms.roomCeiling, rooms.hallCeiling) + bonus : 0f) + c.vaultHeight);
                    if (r.templates != null)
                        foreach (RoomTemplate t in r.templates)
                            if (t != null)
                                tallest = Mathf.Max(tallest, t.ceilingHeight + (r.vaulted ? c.vaultHeight : 0f));
                }
            }
            if (maze != null && maze.templates != null)
                foreach (RoomTemplate t in maze.templates)
                    if (t != null)
                        tallest = Mathf.Max(tallest, t.ceilingHeight);
            return tallest * Mathf.Max(0.1f, c.heightScale);
        }

        /// <summary>
        /// The tallest ceiling a floor of <paramref name="style"/> can produce (meters, Height Scale included): everything
        /// <see cref="TallestCeiling()"/> counts, plus the style's own spaces (the undercity's sky, the den's great cavern,
        /// the islands' and the void's ceilings, the hive's cells).
        /// </summary>
        public float TallestCeiling(FloorStyle style)
        {
            float scale = Mathf.Max(0.1f, (ceilings ?? new CeilingSettings()).heightScale);
            float own = 0f;
            switch (style)
            {
                case FloorStyle.Undercity: own = undercity != null ? undercity.skyHeight.max : 0f; break;
                case FloorStyle.Hive: own = hive != null ? hive.ceiling.max : 0f; break;
                case FloorStyle.Islands: own = islands != null ? islands.ceiling.max : 0f; break;
                case FloorStyle.Den: own = den != null ? den.caveHeight : 0f; break;
                case FloorStyle.Astral: own = astral != null ? astral.ceiling.max : 0f; break;
            }
            return Mathf.Max(TallestCeiling(), own * scale);
        }

        /// <summary>
        /// The distance between a floor of <paramref name="style"/> and the floor above it: Floor Spacing, raised (with
        /// Auto Floor Spacing) so that floor's tallest ceiling plus the cave floor variation plus Rock Between Floors fits.
        /// </summary>
        public float EffectiveFloorSpacing(FloorStyle style)
        {
            CeilingSettings c = ceilings ?? new CeilingSettings();
            float spacing = Mathf.Max(5f, floorSpacing);
            if (!c.autoFloorSpacing)
                return spacing;
            float needed = TallestCeiling(style) + (caves != null ? caves.floorHeightAmplitude : 0f) + c.rockBetweenFloors;
            return Mathf.Max(spacing, Mathf.Ceil(needed * 2f) * 0.5f);
        }

        /// <summary>
        /// The largest distance between two floors this profile can produce: the spacing of the tallest style that can
        /// appear (most floors are closer). With Auto Floor Spacing off, Floor Spacing.
        /// </summary>
        public float EffectiveFloorSpacing()
        {
            float most = 0f;
            foreach (FloorStyle style in (FloorStyle[])System.Enum.GetValues(typeof(FloorStyle)))
                if (CanAppear(style))
                    most = Mathf.Max(most, EffectiveFloorSpacing(style));
            return most > 0f ? most : EffectiveFloorSpacing(FloorStyle.Rooms);
        }

        /// <summary>The distance between two ordinary floors (styles without their own tall spaces).</summary>
        public float BaseFloorSpacing() => EffectiveFloorSpacing(FloorStyle.Rooms);

        /// <summary>Can a floor of this style appear (its weight, or the last floor's override)?</summary>
        public bool CanAppear(FloorStyle style) =>
            (styles != null && styles.Get(style) > 0f) || (overrideLastFloorStyle && lastFloorStyle == style);

        private void Reset()
        {
            roles = DefaultRoles();
        }

        private void OnValidate()
        {
            floorCount.x = Mathf.Max(1, floorCount.x);
            floorCount.y = Mathf.Max(floorCount.x, floorCount.y);
            if (ceilings == null)
                ceilings = new CeilingSettings();
            // Same rule as FloorSpec.MaxCeiling: the tallest ceiling plus cave floor variation plus some rock.
            float tallest = 0f;
            foreach (FloorStyle style in (FloorStyle[])System.Enum.GetValues(typeof(FloorStyle)))
                if (CanAppear(style))
                    tallest = Mathf.Max(tallest, TallestCeiling(style));
            tallest += (caves != null ? caves.floorHeightAmplitude : 0f) + ceilings.rockBetweenFloors;
            if (!ceilings.autoFloorSpacing && floorSpacing < tallest)
                Debug.LogWarning($"{name}: Floor Spacing ({floorSpacing}) is less than the tallest ceiling plus cave floor variation ({tallest:0.0}); ceilings will be lowered. Turn on Heights > Auto Floor Spacing to make room instead.", this);
        }

        /// <summary>A sensible default set of roles: the classic ones plus the special rooms.</summary>
        public static List<RoleRule> DefaultRoles()
        {
            var list = new List<RoleRule>
            {
                new RoleRule
                {
                    name = "Boss", role = AreaRole.Boss, required = true, perFloor = new IntRange(1, 1), maxTotal = 1,
                    lastFloorOnly = true, placement = RolePlacement.EndOfMainPath, size = SizePreference.Large, minCells = 30,
                    ceilingHeight = 10f, vaulted = true,
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
                    ceilingHeight = 8f,
                },
                new RoleRule
                {
                    name = "Shrine", role = AreaRole.Shrine, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.4f, ceilingHeight = 7f, vaulted = true,
                },
                new RoleRule
                {
                    name = "Secret", role = AreaRole.Secret, perFloor = new IntRange(0, 1), placement = RolePlacement.Leaf,
                    size = SizePreference.Small, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f, secretEntrance = true,
                },
            };
            list.AddRange(SpecialRoomRules());
            return list;
        }

        /// <summary>
        /// The special rooms: guardian, vault, trap gauntlet, puzzle, ambush, library, armory, prison, crypt, laboratory,
        /// garden, throne, nest, cursed altars, kitchen, gallery, barracks, pit fight, greenhouse, wine cellar, map room and
        /// gas chamber. Each is optional (a chance per floor), so floors get a few of them, never all.
        /// </summary>
        public static List<RoleRule> SpecialRoomRules()
        {
            return new List<RoleRule>
            {
                new RoleRule
                {
                    name = "Guardian", role = AreaRole.MiniBoss, perFloor = new IntRange(0, 1), progress = new FloatRange(0.35f, 0.9f),
                    placement = RolePlacement.OnMainPath, size = SizePreference.Large, minCells = 36, chance = 0.4f, ceilingHeight = 8f,
                },
                new RoleRule
                {
                    name = "Vault", role = AreaRole.Vault, perFloor = new IntRange(0, 1), progress = new FloatRange(0.2f, 1f),
                    placement = RolePlacement.Leaf, size = SizePreference.Medium, minCells = 12, styles = ZoneMask.Built | ZoneMask.Ruins,
                    chance = 0.35f, weight = 1.2f,
                },
                new RoleRule
                {
                    name = "Trap Gauntlet", role = AreaRole.TrapRoom, perFloor = new IntRange(0, 1), progress = new FloatRange(0.15f, 1f),
                    placement = RolePlacement.OffMainPath, size = SizePreference.Medium, minCells = 24, chance = 0.35f,
                },
                new RoleRule
                {
                    name = "Puzzle", role = AreaRole.Puzzle, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    size = SizePreference.Medium, minCells = 30, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f, ceilingHeight = 6f,
                },
                new RoleRule
                {
                    name = "Ambush", role = AreaRole.Ambush, perFloor = new IntRange(0, 1), progress = new FloatRange(0.25f, 0.85f),
                    placement = RolePlacement.OnMainPath, size = SizePreference.Medium, minCells = 30, chance = 0.3f,
                },
                new RoleRule
                {
                    name = "Library", role = AreaRole.Library, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    size = SizePreference.Medium, minCells = 24, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f, ceilingHeight = 7f,
                },
                new RoleRule
                {
                    name = "Armory", role = AreaRole.Armory, perFloor = new IntRange(0, 1), size = SizePreference.Medium, minCells = 20,
                    styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f,
                },
                new RoleRule
                {
                    name = "Prison", role = AreaRole.Prison, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    minCells = 20, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.25f,
                },
                new RoleRule
                {
                    name = "Crypt", role = AreaRole.Crypt, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    minCells = 24, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f, ceilingHeight = 6f, vaulted = true,
                },
                new RoleRule
                {
                    name = "Laboratory", role = AreaRole.Laboratory, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    minCells = 18, styles = ZoneMask.Built, chance = 0.2f,
                },
                new RoleRule
                {
                    name = "Garden", role = AreaRole.Garden, perFloor = new IntRange(0, 1), minCells = 24,
                    styles = ZoneMask.Cavern | ZoneMask.Ruins, chance = 0.25f,
                },
                new RoleRule
                {
                    name = "Throne", role = AreaRole.Throne, perFloor = new IntRange(0, 1), maxTotal = 1, minFloor = 1,
                    progress = new FloatRange(0.5f, 1f), placement = RolePlacement.OnMainPath, size = SizePreference.Large, minCells = 45,
                    styles = ZoneMask.Built, chance = 0.3f, ceilingHeight = 10f, vaulted = true,
                },
                new RoleRule
                {
                    name = "Nest", role = AreaRole.Nest, perFloor = new IntRange(0, 1), progress = new FloatRange(0.3f, 1f),
                    size = SizePreference.Medium, minCells = 30, chance = 0.25f, ceilingHeight = 6f,
                },
                new RoleRule
                {
                    name = "Cursed Altars", role = AreaRole.Gambling, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    size = SizePreference.Small, minCells = 14, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.25f, ceilingHeight = 6.5f, vaulted = true,
                },
                new RoleRule
                {
                    name = "Kitchen", role = AreaRole.Kitchen, perFloor = new IntRange(0, 1), size = SizePreference.Medium, minCells = 24,
                    styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.25f,
                },
                new RoleRule
                {
                    name = "Gallery", role = AreaRole.Gallery, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    size = SizePreference.Medium, minCells = 24, styles = ZoneMask.Built, chance = 0.2f, ceilingHeight = 6.5f,
                },
                new RoleRule
                {
                    name = "Barracks", role = AreaRole.Barracks, perFloor = new IntRange(0, 1), size = SizePreference.Medium, minCells = 30,
                    styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f,
                },
                new RoleRule
                {
                    name = "Pit Fight", role = AreaRole.Colosseum, perFloor = new IntRange(0, 1), maxTotal = 1, progress = new FloatRange(0.3f, 0.95f),
                    placement = RolePlacement.OnMainPath, size = SizePreference.Large, minCells = 60, chance = 0.25f, ceilingHeight = 10f,
                },
                new RoleRule
                {
                    name = "Greenhouse", role = AreaRole.Greenhouse, perFloor = new IntRange(0, 1), placement = RolePlacement.OffMainPath,
                    size = SizePreference.Medium, minCells = 24, chance = 0.2f, ceilingHeight = 7f, vaulted = true,
                },
                new RoleRule
                {
                    name = "Wine Cellar", role = AreaRole.WineCellar, perFloor = new IntRange(0, 1), size = SizePreference.Medium, minCells = 18,
                    styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.25f, hiddenRoom = true,
                },
                new RoleRule
                {
                    name = "Map Room", role = AreaRole.MapRoom, perFloor = new IntRange(0, 1), progress = new FloatRange(0f, 0.6f),
                    size = SizePreference.Small, minCells = 12, styles = ZoneMask.Built | ZoneMask.Ruins, chance = 0.3f,
                },
                new RoleRule
                {
                    name = "Gas Chamber", role = AreaRole.GasChamber, perFloor = new IntRange(0, 1), progress = new FloatRange(0.2f, 1f),
                    placement = RolePlacement.OffMainPath, size = SizePreference.Medium, minCells = 24, chance = 0.25f,
                },
            };
        }
    }
}
