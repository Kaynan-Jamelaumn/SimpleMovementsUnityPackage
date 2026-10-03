using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>A room template as plain data (see <see cref="RoomTemplate.TryGetPlan"/>).</summary>
    public sealed class TemplateInfo
    {
        public int Index;
        public string Name;
        public bool IsPrefab;
        public int Width, Height;
        public bool[] Floor, Pillar;
        /// <summary>(x, y, side) in template cells.</summary>
        public List<Vector3Int> Sockets;
        public bool Legacy;
        public AreaRole Role;
        public string Tag;
        public float Weight;
        public ZoneMask Styles;
        public float CeilingHeight;
        public bool AllowPopulation;
        /// <summary>Main thread only.</summary>
        public RoomTemplate Source;

        public int FloorCells
        {
            get
            {
                int n = 0;
                foreach (bool f in Floor)
                    if (f)
                        n++;
                return n;
            }
        }
    }

    public sealed class RoleRuleInfo
    {
        public string Name;
        public AreaRole Role;
        public string Tag;
        public bool Required;
        public IntRange PerFloor;
        public int MaxTotal;
        public int MinFloor, MaxFloor;
        public bool LastFloorOnly;
        public FloatRange Progress;
        public RolePlacement Placement;
        public SizePreference Size;
        public int MinCells;
        public ZoneMask Styles;
        public float Chance;
        public float Weight;
        public int[] Templates;
        public bool SecretEntrance;
        /// <summary>Ceiling of the room (meters, before Height Scale); 0 = the normal height.</summary>
        public float CeilingHeight;
        public bool Vaulted;
        /// <summary>A neighbouring dead end becomes a hidden room behind a secret door with a lever in this room.</summary>
        public bool HiddenRoom;
    }

    public sealed class EncounterInfo
    {
        public int Index;
        public string Name;
        public bool HasPrefab;
        public DungeonPrimitive Placeholder;
        public bool Boss;
        public float Weight;
        public int Cost;
        public IntRange PackSize;
        public float PackRadius;
        public int MinFloor, MaxFloor;
        public FloatRange Progress;
        public ZoneMask Styles;
        public AreaRole[] Roles;
        public float Clearance;
        public int Tier;
        /// <summary>Main thread only.</summary>
        public GameObject Prefab;
    }

    public sealed class LootInfo
    {
        public int Index;
        public string Name;
        public bool HasPrefab;
        public DungeonPrimitive Placeholder;
        public float Weight;
        public int Tier;
        public FloatRange Progress;
        public ZoneMask Styles;
        public PropPlacement Placement;
        public int MinFloor, MaxFloor;
        public GameObject Prefab;
    }

    public sealed class PropInfo
    {
        public int Index;
        public string Name;
        public PlacementKind Kind;
        public bool HasPrefab;
        public DungeonPrimitive Placeholder;
        public PropPlacement Placement;
        public AreaRole[] Roles;
        public string AreaTag;
        public ZoneMask Styles;
        public MainPathFilter MainPath;
        public FloorModifierMask Modifiers;
        public float Chance;
        public IntRange PerArea;
        public float PerHundredCells;
        public float Spacing;
        public float AwayFromMobs;
        public FloatRange Progress;
        public int MinFloor, MaxFloor;
        public float HeightOffset;
        public FloatRange Scale;
        public Color LightColor;
        public float LightRange;
        public GameObject Prefab;
    }

    /// <summary>Which tile-kit pieces a theme has (plain data for the mesher).</summary>
    public sealed class TileKitInfo
    {
        public bool HasFloor, HasWall, HasCeiling, HasDoorFrame, HasPillar;
        public float ModuleSize = 2f;
        public float WallPrefabHeight = 4f;
        public bool ScaleWallsToCeiling = true;
    }

    /// <summary>
    /// A <see cref="DungeonProfile"/> turned into plain data on the main thread (settings cloned, templates parsed,
    /// tables flattened, prefab presence resolved), so generation can run on worker threads without touching any
    /// UnityEngine.Object - the same idea as the world's PlacementPlan. Fields marked "main thread only" hold asset
    /// references for the build step.
    /// </summary>
    public sealed class CompiledProfile
    {
        public float CellSize;
        /// <summary>The largest distance between two floors (most floors are closer: see <see cref="SpacingFor"/>).</summary>
        public float FloorSpacing;
        /// <summary>Stair length (cells) for <see cref="FloorSpacing"/>.</summary>
        public int StairLength;
        /// <summary>Distance between a floor of each style and the floor above it (index = (int)FloorStyle).</summary>
        public float[] StyleSpacing;
        public float MaxStairSlope = 33f;
        public bool OverrideLastFloorStyle;
        public FloorStyle LastFloorStyle;
        public Vector2Int FloorCount;
        public SizeClassSettings[] Sizes;
        public float FootprintVariation;
        public FloatRange Openness;
        public FloatRange Complexity;
        public StyleWeights Styles;
        public float NaturalWeightPerFloor;
        public float RepeatStylePenalty;
        public float DifficultyPerFloor;

        public RoomSettings Rooms;
        public BspSettings Bsp;
        public CaveSettings Caves;
        public HybridSettings Hybrid;
        public MazeSettings Maze;
        public CitadelSettings Citadel;
        public CatacombSettings Catacombs;
        public TowerSettings Tower;
        public UndercitySettings Undercity;
        public HiveSettings Hive;
        public IslandSettings Islands;
        public DenSettings Den;
        public AstralSettings Astral;
        public int[] MazeTemplates;
        public ConnectionSettings Connections;
        public LinkSettings Links;
        public PopulationSettings Population;
        public ValidationSettings Validation;
        public BuildSettings Build;
        public CeilingSettings Ceilings;
        public RoomEventSettings Mechanics;
        public FloorModifierSettings FloorModifiers;

        public readonly List<RoleRuleInfo> Roles = new List<RoleRuleInfo>();
        public readonly List<TemplateInfo> Templates = new List<TemplateInfo>();
        public readonly List<EncounterInfo> Encounters = new List<EncounterInfo>();
        public readonly List<LootInfo> Loot = new List<LootInfo>();
        public readonly List<PropInfo> Props = new List<PropInfo>();
        public readonly List<string> Warnings = new List<string>();

        public bool HasTileKit;
        public readonly TileKitInfo TileKit = new TileKitInfo();
        /// <summary>Meters per texture repeat on generated meshes.</summary>
        public float TextureScale = 3f;
        /// <summary>Custom stages from the profile (their Run is called on the worker thread).</summary>
        public readonly List<DungeonStageAsset> CustomStages = new List<DungeonStageAsset>();
        /// <summary>Main thread only.</summary>
        public DungeonProfile Source;
        /// <summary>Main thread only.</summary>
        public DungeonTheme Theme;

        /// <summary>Highest ceiling (above a floor's base) that still leaves rock under the floor above, at the largest spacing.</summary>
        public float MaxCeiling => MaxCeilingFor(FloorSpacing);

        /// <summary>Highest ceiling (above a floor's base) that leaves rock under a floor <paramref name="spacing"/> meters above.</summary>
        public float MaxCeilingFor(float spacing) => Mathf.Max(2.5f, spacing - Caves.floorHeightAmplitude - (Ceilings != null ? Ceilings.rockBetweenFloors : 0.8f));

        /// <summary>Distance between a floor of <paramref name="style"/> and the floor above it.</summary>
        public float SpacingFor(FloorStyle style)
        {
            int i = (int)style;
            return StyleSpacing != null && i >= 0 && i < StyleSpacing.Length && StyleSpacing[i] > 0f ? StyleSpacing[i] : FloorSpacing;
        }

        /// <summary>Length of a stair well (cells) climbing <paramref name="rise"/> meters at the profile's steepest slope.</summary>
        public int StairLengthFor(float rise)
        {
            float run = rise / Mathf.Tan(Mathf.Clamp(MaxStairSlope, 15f, 60f) * Mathf.Deg2Rad);
            return Mathf.Max(3, Mathf.CeilToInt(run / Mathf.Max(0.1f, CellSize)));
        }

        public SizeClassSettings GetSize(SizeClass size)
        {
            foreach (SizeClassSettings s in Sizes)
                if (s != null && s.size == size)
                    return s;
            foreach (SizeClassSettings s in Sizes)
                if (s != null && s.size == SizeClass.Medium)
                    return s;
            return new SizeClassSettings();
        }

        /// <summary>Compiles a profile. Main thread only. A null profile compiles the defaults.</summary>
        public static CompiledProfile Compile(DungeonProfile profile)
        {
            bool temporary = false;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<DungeonProfile>();
                temporary = true;
            }

            var c = new CompiledProfile
            {
                CellSize = Mathf.Max(0.5f, profile.cellSize),
                FloorSpacing = profile.EffectiveFloorSpacing(),
                StairLength = profile.StairLengthCells(profile.EffectiveFloorSpacing()),
                FloorCount = new Vector2Int(Mathf.Max(1, profile.floorCount.x), Mathf.Max(Mathf.Max(1, profile.floorCount.x), profile.floorCount.y)),
                Sizes = DeepCopy.Copy(profile.sizeClasses) ?? new SizeClassSettings[0],
                FootprintVariation = profile.footprintVariation,
                Openness = profile.openness,
                Complexity = profile.complexity,
                Styles = DeepCopy.Copy(profile.styles) ?? new StyleWeights(),
                NaturalWeightPerFloor = profile.naturalWeightPerFloor,
                RepeatStylePenalty = profile.repeatStylePenalty,
                DifficultyPerFloor = profile.difficultyPerFloor,
                Rooms = DeepCopy.Copy(profile.rooms) ?? new RoomSettings(),
                Bsp = DeepCopy.Copy(profile.bsp) ?? new BspSettings(),
                Caves = DeepCopy.Copy(profile.caves) ?? new CaveSettings(),
                Hybrid = DeepCopy.Copy(profile.hybrid) ?? new HybridSettings(),
                Maze = DeepCopy.Copy(profile.maze) ?? new MazeSettings(),
                Citadel = DeepCopy.Copy(profile.citadel) ?? new CitadelSettings(),
                Catacombs = DeepCopy.Copy(profile.catacombs) ?? new CatacombSettings(),
                Tower = DeepCopy.Copy(profile.tower) ?? new TowerSettings(),
                Undercity = DeepCopy.Copy(profile.undercity) ?? new UndercitySettings(),
                Hive = DeepCopy.Copy(profile.hive) ?? new HiveSettings(),
                Islands = DeepCopy.Copy(profile.islands) ?? new IslandSettings(),
                Den = DeepCopy.Copy(profile.den) ?? new DenSettings(),
                Astral = DeepCopy.Copy(profile.astral) ?? new AstralSettings(),
                MaxStairSlope = profile.maxStairSlope,
                OverrideLastFloorStyle = profile.overrideLastFloorStyle,
                LastFloorStyle = profile.lastFloorStyle,
                Connections = DeepCopy.Copy(profile.connections) ?? new ConnectionSettings(),
                Links = DeepCopy.Copy(profile.links) ?? new LinkSettings(),
                Population = DeepCopy.Copy(profile.population) ?? new PopulationSettings(),
                Validation = DeepCopy.Copy(profile.validation) ?? new ValidationSettings(),
                Build = DeepCopy.Copy(profile.build) ?? new BuildSettings(),
                Ceilings = DeepCopy.Copy(profile.ceilings) ?? new CeilingSettings(),
                Mechanics = DeepCopy.Copy(profile.mechanics) ?? new RoomEventSettings(),
                FloorModifiers = DeepCopy.Copy(profile.floorModifiers) ?? new FloorModifierSettings(),
                Theme = profile.theme,
                Source = temporary ? null : profile,
            };
            var styleValues = (FloorStyle[])Enum.GetValues(typeof(FloorStyle));
            c.StyleSpacing = new float[styleValues.Length];
            foreach (FloorStyle style in styleValues)
                c.StyleSpacing[(int)style] = profile.EffectiveFloorSpacing(style);
            c.HasTileKit = profile.theme != null && profile.theme.HasTileKit;
            if (profile.theme != null)
            {
                DungeonTheme t = profile.theme;
                c.TextureScale = Mathf.Max(0.1f, t.textureScale);
                c.TileKit.HasFloor = t.floorTile != null;
                c.TileKit.HasWall = t.wallSegment != null;
                c.TileKit.HasCeiling = t.ceilingTile != null;
                c.TileKit.HasDoorFrame = t.doorFrame != null;
                c.TileKit.HasPillar = t.pillar != null;
                c.TileKit.ModuleSize = Mathf.Max(0.1f, t.moduleSize);
                c.TileKit.WallPrefabHeight = Mathf.Max(0.1f, t.wallPrefabHeight);
                c.TileKit.ScaleWallsToCeiling = t.scaleWallsToCeiling;
            }
            if (profile.customStages != null)
                foreach (DungeonStageAsset stage in profile.customStages)
                    if (stage != null)
                        c.CustomStages.Add(stage);

            if (c.Rooms.sizes == null || c.Rooms.sizes.Length == 0)
                c.Rooms.sizes = new RoomSettings().sizes;

            // Templates: gathered from the maze settings and the role rules.
            var templateIndex = new Dictionary<RoomTemplate, int>();
            int AddTemplate(RoomTemplate t)
            {
                if (t == null)
                    return -1;
                if (templateIndex.TryGetValue(t, out int existing))
                    return existing;
                if (!t.TryGetPlan(out int w, out int h, out bool[] floor, out bool[] pillar, out List<Vector3Int> sockets, out string error))
                {
                    c.Warnings.Add(error);
                    templateIndex[t] = -1;
                    return -1;
                }
                if (t.mode == RoomTemplate.TemplateMode.Prefab && t.prefab == null)
                {
                    c.Warnings.Add($"Template '{t.name}' is in Prefab mode without a prefab; skipped.");
                    templateIndex[t] = -1;
                    return -1;
                }
                var info = new TemplateInfo
                {
                    Index = c.Templates.Count,
                    Name = t.name,
                    IsPrefab = t.mode == RoomTemplate.TemplateMode.Prefab,
                    Width = w,
                    Height = h,
                    Floor = floor,
                    Pillar = pillar,
                    Sockets = sockets,
                    Legacy = t.legacyRoomBehaviour,
                    Role = t.role,
                    Tag = t.tag ?? "",
                    Weight = Mathf.Max(0f, t.weight),
                    Styles = t.styles,
                    CeilingHeight = t.ceilingHeight,
                    AllowPopulation = t.allowPopulation,
                    Source = t,
                };
                c.Templates.Add(info);
                templateIndex[t] = info.Index;
                return info.Index;
            }

            var maze = new List<int>();
            if (profile.maze != null && profile.maze.templates != null)
                foreach (RoomTemplate t in profile.maze.templates)
                {
                    int i = AddTemplate(t);
                    if (i >= 0)
                        maze.Add(i);
                }
            c.MazeTemplates = maze.ToArray();

            if (profile.roles != null)
            {
                foreach (RoleRule r in profile.roles)
                {
                    if (r == null)
                        continue;
                    var templates = new List<int>();
                    if (r.templates != null)
                        foreach (RoomTemplate t in r.templates)
                        {
                            int i = AddTemplate(t);
                            if (i >= 0)
                                templates.Add(i);
                        }
                    c.Roles.Add(new RoleRuleInfo
                    {
                        Name = string.IsNullOrEmpty(r.name) ? r.role.ToString() : r.name,
                        Role = r.role,
                        Tag = r.tag ?? "",
                        Required = r.required,
                        PerFloor = r.perFloor,
                        MaxTotal = r.maxTotal,
                        MinFloor = r.minFloor,
                        MaxFloor = r.maxFloor,
                        LastFloorOnly = r.lastFloorOnly,
                        Progress = r.progress,
                        Placement = r.placement,
                        Size = r.size,
                        MinCells = r.minCells,
                        Styles = r.styles == ZoneMask.None ? ZoneMask.All : r.styles,
                        Chance = r.chance,
                        Weight = r.weight,
                        Templates = templates.ToArray(),
                        SecretEntrance = r.secretEntrance,
                        CeilingHeight = Mathf.Max(0f, r.ceilingHeight),
                        Vaulted = r.vaulted,
                        HiddenRoom = r.hiddenRoom,
                    });
                }
            }

            // Tables.
            PopulationSettings pop = profile.population ?? new PopulationSettings();
            if (pop.encounters != null)
            {
                foreach (EncounterEntry e in pop.encounters.entries)
                {
                    if (e == null || e.weight <= 0f)
                        continue;
                    c.Encounters.Add(new EncounterInfo
                    {
                        Index = c.Encounters.Count,
                        Name = e.name,
                        HasPrefab = e.prefab != null,
                        Prefab = e.prefab,
                        Placeholder = e.placeholder,
                        Boss = e.boss,
                        Weight = e.weight,
                        Cost = Mathf.Max(1, e.cost),
                        PackSize = new IntRange(Mathf.Max(1, e.packSize.min), Mathf.Max(Mathf.Max(1, e.packSize.min), e.packSize.max)),
                        PackRadius = Mathf.Max(0.5f, e.packRadius),
                        MinFloor = e.minFloor,
                        MaxFloor = e.maxFloor,
                        Progress = e.progress,
                        Styles = e.styles == ZoneMask.None ? ZoneMask.All : e.styles,
                        Roles = e.roles != null ? e.roles.ToArray() : new AreaRole[0],
                        Clearance = Mathf.Max(0.5f, e.clearance),
                        Tier = e.tier,
                    });
                }
            }
            else if (pop.placeholderMobs)
            {
                c.Encounters.AddRange(DungeonDefaults.PlaceholderEncounters());
            }

            if (pop.loot != null)
            {
                foreach (LootEntry e in pop.loot.entries)
                {
                    if (e == null || e.weight <= 0f)
                        continue;
                    c.Loot.Add(new LootInfo
                    {
                        Index = c.Loot.Count,
                        Name = e.name,
                        HasPrefab = e.prefab != null,
                        Prefab = e.prefab,
                        Placeholder = e.placeholder,
                        Weight = e.weight,
                        Tier = e.tier,
                        Progress = e.progress,
                        Styles = e.styles == ZoneMask.None ? ZoneMask.All : e.styles,
                        Placement = e.placement,
                        MinFloor = e.minFloor,
                        MaxFloor = e.maxFloor,
                    });
                }
            }
            else if (pop.defaultLoot)
            {
                c.Loot.AddRange(DungeonDefaults.Loot());
            }

            if (pop.props != null)
            {
                foreach (PropEntry e in pop.props.entries)
                {
                    if (e == null)
                        continue;
                    c.Props.Add(new PropInfo
                    {
                        Index = c.Props.Count,
                        Name = e.name,
                        Kind = e.kind,
                        HasPrefab = e.prefab != null,
                        Prefab = e.prefab,
                        Placeholder = e.placeholder,
                        Placement = e.placement,
                        Roles = e.roles != null ? e.roles.ToArray() : new AreaRole[0],
                        AreaTag = e.areaTag ?? "",
                        Styles = e.styles == ZoneMask.None ? ZoneMask.All : e.styles,
                        MainPath = e.mainPath,
                        Modifiers = e.modifiers,
                        Chance = e.chance,
                        PerArea = e.perArea,
                        PerHundredCells = e.perHundredCells,
                        Spacing = e.spacing,
                        AwayFromMobs = e.awayFromMobs,
                        Progress = e.progress,
                        MinFloor = e.minFloor,
                        MaxFloor = e.maxFloor,
                        HeightOffset = e.heightOffset,
                        Scale = e.scale,
                        LightColor = e.lightColor,
                        LightRange = e.lightRange,
                    });
                }
            }
            else if (pop.defaultProps)
            {
                c.Props.AddRange(DungeonDefaults.Props(profile.theme));
            }
            // Special rooms and floor modifiers the table has nothing for get the built-in props.
            if (pop.props != null && pop.fillMissingRoleProps)
                DungeonDefaults.FillMissing(c.Props, profile.theme);

            foreach (FloorStyle style in styleValues)
            {
                if (!profile.CanAppear(style))
                    continue;
                float needed = profile.TallestCeiling(style) + c.Caves.floorHeightAmplitude + c.Ceilings.rockBetweenFloors;
                float spacing = c.SpacingFor(style);
                if (spacing + 0.01f < needed)
                {
                    c.Warnings.Add($"Floor spacing {spacing} is tight for the ceilings of {style} floors; they are lowered to {c.MaxCeilingFor(spacing):0.0} m (turn on Heights > Auto Floor Spacing).");
                    break;
                }
            }

            if (temporary)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(profile);
                else
                    UnityEngine.Object.DestroyImmediate(profile);
            }
            return c;
        }
    }

    /// <summary>
    /// Deep copy of plain serializable settings (arrays, lists and [Serializable] classes are copied; strings, enums,
    /// primitives and UnityEngine.Object references are shared).
    /// </summary>
    public static class DeepCopy
    {
        private static readonly MethodInfo Memberwise = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        public static T Copy<T>(T source) where T : class => (T)CopyObject(source);

        private static object CopyObject(object source)
        {
            if (source == null)
                return null;
            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || typeof(UnityEngine.Object).IsAssignableFrom(type))
                return source;

            if (type.IsArray)
            {
                var array = (Array)((Array)source).Clone();
                Type element = type.GetElementType();
                if (element != null && !element.IsPrimitive && !element.IsEnum && element != typeof(string))
                    for (int i = 0; i < array.Length; i++)
                        array.SetValue(CopyObject(array.GetValue(i)), i);
                return array;
            }

            if (source is IList list && type.IsGenericType)
            {
                var copy = (IList)Activator.CreateInstance(type);
                foreach (object item in list)
                    copy.Add(CopyObject(item));
                return copy;
            }

            if (type.IsValueType)
                return source;

            object clone = Memberwise.Invoke(source, null);
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type ft = field.FieldType;
                if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || (ft.IsValueType && !ft.IsGenericType))
                    continue;
                field.SetValue(clone, CopyObject(field.GetValue(source)));
            }
            return clone;
        }
    }
}
