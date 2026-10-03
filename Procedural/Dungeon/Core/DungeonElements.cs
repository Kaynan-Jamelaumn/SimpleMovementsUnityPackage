using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A room, hall, cavern chamber or landing on one floor. Whatever made it (room scatter, BSP, caves, a template),
    /// every later stage - paths, roles, population - treats it the same way.
    /// </summary>
    public sealed class Area
    {
        public int Id;
        public int Floor;
        public AreaKind Kind;
        public AreaRole Role;
        /// <summary>Free-form tag for custom roles and gameplay lookups (e.g. "Library").</summary>
        public string Tag = "";
        public ZoneStyle Style;

        /// <summary>Grid indices of the area's walkable cells.</summary>
        public readonly List<int> Cells = new List<int>();
        public RectInt Bounds;
        /// <summary>A representative point (cell units) used for graph distances: inside the area.</summary>
        public Vector2 Center;
        public int CenterCell = -1;

        /// <summary>Room template stamped here (index into the compiled profile's templates), -1 for none.</summary>
        public int TemplateIndex = -1;
        /// <summary>Anchor that created this area (entrance, exit, landings), -1 for none.</summary>
        public int AnchorIndex = -1;
        /// <summary>The template has been stamped into the grid.</summary>
        public bool TemplateApplied;
        /// <summary>Grid cell of the stamped template's south-west corner.</summary>
        public Vector2Int TemplateOrigin;
        /// <summary>Anchor areas keep their shape (no template fitting, no removal).</summary>
        public bool Fixed;
        /// <summary>Connected region at layout time (caves can start as several separate regions).</summary>
        public int Region;
        /// <summary>Ceiling of the room set by its role (meters, before Height Scale); 0 = the normal height.</summary>
        public float CeilingHeight;
        /// <summary>The ceiling rises towards the middle of the room (set by its role, or rolled for halls by the height pass).</summary>
        public bool Vaulted;
        /// <summary>
        /// A role the layout suggests for this area (a den's great cavern for the boss, a plaza for an arena...). The Roles
        /// stage prefers it for rules of that role; None = no suggestion.
        /// </summary>
        public AreaRole Hint;

        // Graph information (Connectivity and Roles stages).
        public readonly List<int> Connections = new List<int>();
        public readonly List<int> Links = new List<int>();
        /// <summary>Area hops from the dungeon entrance (across floors), -1 if unknown.</summary>
        public int Depth = -1;
        /// <summary>0 at the entrance, 1 at the exit: how far along the main path this area is (or where its branch leaves it).</summary>
        public float Progress;
        public bool OnMainPath;
        /// <summary>Hops from the nearest main-path area.</summary>
        public int BranchDepth;
        public bool IsLeaf;
        public bool IsHub;

        // Metrics (Analysis stage).
        public float MeanWallDistance;
        public float MaxWallDistance;
        /// <summary>0 = narrow, 1 = wide open.</summary>
        public float Openness;
        public float Difficulty;

        public int CellCount => Cells.Count;

        public void RecomputeBounds(TileGrid grid)
        {
            if (Cells.Count == 0)
            {
                Bounds = new RectInt(0, 0, 0, 0);
                return;
            }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            float sx = 0f, sy = 0f;
            foreach (int c in Cells)
            {
                int x = grid.X(c), y = grid.Y(c);
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                sx += x + 0.5f;
                sy += y + 0.5f;
            }
            Bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

            // Centre: the area cell nearest the centroid (so it is inside even for L shapes and caverns).
            Vector2 centroid = new Vector2(sx / Cells.Count, sy / Cells.Count);
            float best = float.MaxValue;
            foreach (int c in Cells)
            {
                float d = (grid.Center(c) - centroid).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    CenterCell = c;
                }
            }
            Center = grid.Center(CenterCell);
        }

        public override string ToString() => $"Area {Id} ({Kind}, {Role}{(string.IsNullOrEmpty(Tag) ? "" : " " + Tag)}, {Cells.Count} cells, floor {Floor})";
    }

    /// <summary>A way between two areas of the same floor.</summary>
    public sealed class Connection
    {
        public int Id;
        public int A, B;
        public ConnectionKind Kind;
        /// <summary>Decided by the layout (maze edges) or already open (cave openings): always kept.</summary>
        public bool Forced;
        /// <summary>Part of the spanning tree (removing it would disconnect the floor).</summary>
        public bool InTree;
        /// <summary>An extra edge that creates an alternative route.</summary>
        public bool IsLoop;
        /// <summary>Added by validation to reconnect an unreachable area.</summary>
        public bool IsRepair;
        public bool OnMainPath;
        /// <summary>Estimated corridor length (cells) from the candidate graph.</summary>
        public float EstimatedCost;
        public int Width = 1;
        public bool Routed;
        public bool Failed;
        /// <summary>Cells carved for this connection (not counting the areas' own cells).</summary>
        public readonly List<int> Cells = new List<int>();
        /// <summary>
        /// Door cells at the A and B ends (grid index), -1 when that end has no door. Portals and moving platforms: the
        /// walkable cell at each end (where the pad is / where the platform docks).
        /// </summary>
        public int DoorA = -1, DoorB = -1;
        /// <summary>Moving platforms: the chasm cells of the track from the A end to the B end (not carved).</summary>
        public readonly List<int> Track = new List<int>();

        public int Other(int area) => area == A ? B : A;

        public bool Joins(int a, int b) => (A == a && B == b) || (A == b && B == a);

        public override string ToString() => $"Connection {Id} {A}<->{B} ({Kind}{(IsLoop ? ", loop" : "")}{(Failed ? ", FAILED" : "")})";
    }

    /// <summary>A stair well or drop shaft between two consecutive floors.</summary>
    public sealed class VerticalLink
    {
        public int Id;
        public LinkKind Kind;
        public int UpperFloor, LowerFloor;
        /// <summary>The shaft's cells (same on both floors).</summary>
        public RectInt Footprint;
        /// <summary>Stairs: the direction you walk while going down.</summary>
        public Dir4 Descend;
        /// <summary>First walkable cell outside the shaft on the upper floor (where you step onto the stairs / into the pit).</summary>
        public Vector2Int UpperLanding;
        /// <summary>First walkable cell outside the shaft on the lower floor (where the stairs arrive / below the pit).</summary>
        public Vector2Int LowerLanding;
        public int UpperArea = -1, LowerArea = -1;
        public bool OnMainPath;
        public bool OneWay => Kind == LinkKind.Drop;
        /// <summary>Spirals: turns of the stair between the two floors (a multiple of a quarter turn).</summary>
        public float Turns;
        /// <summary>Spirals: angle (radians, 0 = east, counter-clockwise) of the doorway on the upper floor; the stair turns clockwise going down.</summary>
        public float StartAngle;
        /// <summary>Spirals: the spiral above / below sharing the shaft (-1 = none).</summary>
        public int Above = -1, Below = -1;

        public override string ToString() => $"{Kind} {Id}: floor {UpperFloor} -> {LowerFloor} at {Footprint}";
    }

    /// <summary>
    /// A fixed point a floor's layout must honour: entrance and exit rooms, stair landings and drop rooms. Anchors are
    /// decided before any floor is laid out, which is what makes every floor reachable by construction.
    /// </summary>
    public sealed class Anchor
    {
        public AnchorKind Kind;
        public int Floor;
        /// <summary>Cells that become the anchor's area.</summary>
        public RectInt Room;
        /// <summary>Key cell: the landing cell for stairs, the pit centre side for drops, the room centre otherwise.</summary>
        public Vector2Int Cell;
        public int LinkId = -1;
        /// <summary>A second link served by the same landing (a tower floor's doorway is both the way down and the way up).</summary>
        public int ExtraLinkId = -1;
        /// <summary>Direction pointing away from the shaft into the room (landings) or into the room (entrance/exit).</summary>
        public Dir4 Facing;
        public int AreaId = -1;
        /// <summary>Shaft cells on this floor (stair well, or the pit on a drop's upper floor). Empty = none.</summary>
        public RectInt Shaft;
        /// <summary>Cells inside the room that are open to the floor above (under a drop).</summary>
        public RectInt NoCeiling;

        public override string ToString() => $"{Kind} anchor on floor {Floor}: room {Room}";
    }

    /// <summary>What the macro plan decided for one floor.</summary>
    public sealed class FloorSpec
    {
        public int Index;
        public FloorStyle Style;
        /// <summary>The usable part of the floor's grid (floors differ in size and position).</summary>
        public RectInt Footprint;
        /// <summary>0..1: small rooms, narrow passages - large rooms, open caverns, more loops.</summary>
        public float Openness;
        /// <summary>0..1: simple, few branches - many rooms, side paths, winding routes.</summary>
        public float Complexity;
        public float Difficulty;
        public bool IsFirst, IsLast;
        /// <summary>World-space offset of this floor below the dungeon origin (negative, floor 0 = 0).</summary>
        public float BaseY;
        /// <summary>Distance up to the floor above's base (floor 0: the distance its own ceilings need).</summary>
        public float SpacingAbove;
        /// <summary>Highest ceiling on this floor (above its base) that still leaves rock under the floor above.</summary>
        public float MaxCeiling = 8f;
        /// <summary>The floor's twist (flooded, molten, overgrown, dark, frozen), None for most floors.</summary>
        public FloorModifier Modifier;
        /// <summary>Hybrid floors: zone style weights.</summary>
        public float BuiltWeight = 1f, CavernWeight = 1f, RuinsWeight = 0.5f;
    }

    /// <summary>A reference to an area on some floor.</summary>
    public struct AreaRef
    {
        public int Floor;
        public int Area;

        public AreaRef(int floor, int area)
        {
            Floor = floor;
            Area = area;
        }

        public bool IsValid => Floor >= 0 && Area >= 0;

        public override string ToString() => $"floor {Floor} area {Area}";
    }

    /// <summary>A position and facing in dungeon space: floor, cell coordinates (cell centre = x + 0.5), height above the floor's base, yaw.</summary>
    public struct DungeonPose
    {
        public bool Valid;
        public int Floor;
        public Vector2 Cell;
        public float Height;
        public float Yaw;

        public DungeonPose(int floor, Vector2 cell, float height, float yaw)
        {
            Valid = true;
            Floor = floor;
            Cell = cell;
            Height = height;
            Yaw = yaw;
        }
    }

    /// <summary>Something the population stage decided to put in the dungeon.</summary>
    public struct Placement
    {
        public PlacementKind Kind;
        /// <summary>Which compiled table the entry comes from (see <see cref="PlacementTable"/>).</summary>
        public PlacementTable Table;
        public int Entry;
        public int Floor;
        public int Area;
        public Vector2 Cell;
        public float Height;
        public float Yaw;
        /// <summary>Members of the same pack / group share this id (-1 for none).</summary>
        public int Group;
        public float Scale;
        /// <summary>Tier / level for loot and mobs (0 = lowest).</summary>
        public int Tier;
        /// <summary>Spawned hidden (inactive) and revealed by its room's event: ambush waves, puzzle and cleared-room rewards.</summary>
        public bool Dormant;
        /// <summary>Ambush wave the mob belongs to (1 = the first; 0 = not part of a wave).</summary>
        public byte Wave;
        /// <summary>An elite mob (guardian rooms): bigger and a tier higher.</summary>
        public bool Elite;
        /// <summary>Pairs things up: the key id of a Key / Locked Door, a pressure plate's place in the order.</summary>
        public int Link;
        /// <summary>Mobs: asleep, roaming, a pit fight's champion.</summary>
        public MobOrder Order;
    }

    /// <summary>Special values of <see cref="Placement.Link"/>.</summary>
    public static class PlacementLinks
    {
        /// <summary>A hidden reward found by breaking the barrels of its room (wine cellars).</summary>
        public const int BarrelStash = -3;
    }

    public enum PlacementTable : byte
    {
        None,
        Encounters,
        Loot,
        Props,
    }
}
