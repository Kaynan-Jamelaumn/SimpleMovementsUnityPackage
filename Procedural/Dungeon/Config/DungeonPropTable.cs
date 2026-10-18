using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum MainPathFilter : byte
    {
        Any,
        OnlyMainPath,
        OnlyOffMainPath,
    }

    /// <summary>One kind of interactable, point of interest, hazard, decoration or light.</summary>
    [Serializable]
    public class PropEntry
    {
        [Tooltip("Label (reports, spawned object names).")]
        public string name = "Prop";
        [Tooltip("What it is for other systems (Interactable, Point Of Interest, Hazard, Decoration, Light) - reported through DungeonInstance and DungeonSpawned.")]
        public PlacementKind kind = PlacementKind.Decoration;
        [Tooltip("The prop prefab. Empty = the Placeholder primitive.")]
        public GameObject prefab;
        [Tooltip("Built-in stand-in used when Prefab is empty (torch, brazier, crystal, barrel, crate, altar, trap...).")]
        public DungeonPrimitive placeholder = DungeonPrimitive.Barrel;
        [Tooltip("With a Prefab: give it the mechanics of its Placeholder when it has none of its own (a Spike Trap prefab gets a " +
                 "DungeonHazard with a trigger fitted to it, a Blade Trap prefab a DungeonBladeTrap, a Dart Trap a DungeonDartTrap, a " +
                 "Barrel / Crate a DungeonBreakable, a Chest a DungeonChest, a Fountain a rest point...). Components already on the prefab are kept.")]
        public bool addMechanics = true;
        [Tooltip("Traps: damage, element, rhythm and who they hurt, for prefabs and placeholders alike (off = the trap's own values).")]
        public TrapSettings trap = new TrapSettings();
        [Tooltip("Breakables and chests (barrels, crates, urns): what they drop (an Item Drop Table). Empty = the Item Drop Manager's defaults.")]
        public ItemDropTable dropTable;
        [Tooltip("Where in an area it goes: Anywhere, Wall Adjacent (against a wall), Center, Corner, Corridor, Doorway, Dead End, Chokepoint, Transition (where zone styles meet), Back Wall (the wall farthest from the ways in, facing the room) or Off Path (away from the route through the floor).")]
        public PropPlacement placement = PropPlacement.WallAdjacent;
        [Tooltip("Only in areas with these roles (empty = any).")]
        public List<AreaRole> roles = new List<AreaRole>();
        [Tooltip("Only in areas with this tag (empty = any).")]
        public string areaTag = "";
        [Tooltip("Zone styles it may appear in.")]
        public ZoneMask styles = ZoneMask.All;
        [Tooltip("Only on the entrance-to-exit route, only off it, or anywhere.")]
        public MainPathFilter mainPath = MainPathFilter.Any;
        [Tooltip("Only on floors with these modifiers (Normal = floors without one). Nothing ticked = every floor.")]
        public FloorModifierMask modifiers = FloorModifierMask.Any;
        [Tooltip("Chance per area to place any.")]
        [Range(0f, 1f)] public float chance = 1f;
        [Tooltip("How many per area (before the density term).")]
        public IntRange perArea = new IntRange(0, 1);
        [Tooltip("Extra count per 100 walkable cells of the area.")]
        [Min(0f)] public float perHundredCells;
        [Tooltip("Minimum distance to others of the same entry (cells).")]
        [Min(0f)] public float spacing = 3f;
        [Tooltip("Minimum distance to mobs (cells), 0 = no rule.")]
        [Min(0f)] public float awayFromMobs;
        [Tooltip("Where along the entrance-to-exit progression (0..1) it may appear.")]
        public FloatRange progress = new FloatRange(0f, 1f);
        [Tooltip("First floor it may appear on (0 = top floor).")]
        public int minFloor;
        [Tooltip("Last floor it may appear on (-1 = no limit).")]
        public int maxFloor = -1;
        [Tooltip("Lift above the floor (meters) - e.g. torches on walls.")]
        public float heightOffset;
        [Tooltip("Random uniform scale range.")]
        public FloatRange scale = new FloatRange(1f, 1f);
        [Tooltip("Primitive lights: colour and range (prefabs bring their own).")]
        public Color lightColor = new Color(1f, 0.62f, 0.3f);
        [Tooltip("Primitive lights: range (meters, 0 = no light).")]
        [Min(0f)] public float lightRange = 9f;
    }

    /// <summary>Interactables, points of interest, hazards, decorations and lights (see <see cref="PopulationSettings.props"/>).</summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Dungeon/Prop Table", fileName = "DungeonProps")]
    public class DungeonPropTable : ScriptableObject
    {
        [Tooltip("The props. Create Default Setup fills a table with the built-in set to start from.")]
        public List<PropEntry> entries = new List<PropEntry>();
    }
}
