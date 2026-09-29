using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>What a grid cell is. Walls are solid cells: two rooms are always separated by at least one of them.</summary>
    public enum CellType : byte
    {
        /// <summary>Rock / wall.</summary>
        Solid = 0,
        /// <summary>Walkable floor (rooms, halls, caverns, corridors, tunnels, landings).</summary>
        Floor = 1,
        /// <summary>Walkable doorway between a room and a corridor (gets a door frame).</summary>
        Door = 2,
        /// <summary>Part of a vertical link (stair well or drop shaft): built by the link builder, not walkable at floor level.</summary>
        Link = 3,
    }

    /// <summary>Extra information per cell.</summary>
    [Flags]
    public enum CellFlags : ushort
    {
        None = 0,
        /// <summary>Natural surface: irregular walls, uneven floor, organic meshing.</summary>
        Organic = 1 << 0,
        /// <summary>Must never be carved (outside the floor footprint, around shafts).</summary>
        Reserved = 1 << 1,
        /// <summary>Open to the floor above (under a drop shaft): no ceiling is built.</summary>
        NoCeiling = 1 << 2,
        /// <summary>Carved by a connection (corridor, tunnel, breach).</summary>
        Corridor = 1 << 3,
        /// <summary>Geometry comes from a room prefab: the mesher leaves it alone.</summary>
        Prefab = 1 << 4,
        /// <summary>A secret door.</summary>
        Secret = 1 << 5,
        /// <summary>A narrow point on the way between areas.</summary>
        Chokepoint = 1 << 6,
        /// <summary>On the shortest walk from this floor's arrival to its departure.</summary>
        MainPath = 1 << 7,
        /// <summary>In front of a stair well or drop shaft.</summary>
        Landing = 1 << 8,
        /// <summary>A solid pillar inside a room.</summary>
        Pillar = 1 << 9,
        /// <summary>Where built structure breaks into natural rock (rubble decoration goes here).</summary>
        Rubble = 1 << 10,
        /// <summary>Taken by a placement (portal, spawn, loot...).</summary>
        Occupied = 1 << 11,
        /// <summary>A drop shaft's opening on the upper floor: no floor, but the room's ceiling still covers it.</summary>
        Pit = 1 << 12,
    }

    public enum AreaKind : byte
    {
        Room,
        Hall,
        Cavern,
        Landing,
        Corridor,
    }

    /// <summary>
    /// What an area is for. Built-in roles cover the common cases; <see cref="Custom"/> plus
    /// <see cref="Area.Tag"/> covers game-specific ones (library, armory, prison...).
    /// </summary>
    public enum AreaRole : byte
    {
        None,
        Entrance,
        Exit,
        /// <summary>Arrival from the floor above.</summary>
        StairsUp,
        /// <summary>Departure to the floor below.</summary>
        StairsDown,
        /// <summary>Holds a pit dropping to the floor below (one way).</summary>
        DropSource,
        /// <summary>Where a drop from the floor above lands.</summary>
        DropLanding,
        Boss,
        Treasure,
        Rest,
        Arena,
        Shrine,
        Secret,
        Custom,
    }

    /// <summary>How an area was made.</summary>
    public enum ZoneStyle : byte
    {
        Built,
        Cavern,
        Ruins,
    }

    [Flags]
    public enum ZoneMask : byte
    {
        None = 0,
        Built = 1,
        Cavern = 2,
        Ruins = 4,
        All = Built | Cavern | Ruins,
    }

    public enum ConnectionKind : byte
    {
        /// <summary>Rooms one wall apart: a door straight through it.</summary>
        Door,
        /// <summary>A constructed corridor with doors at its ends.</summary>
        Corridor,
        /// <summary>A winding natural tunnel.</summary>
        Tunnel,
        /// <summary>Two caverns already open to each other (nothing to carve).</summary>
        Opening,
        /// <summary>A rough passage between built and natural areas.</summary>
        Breach,
        /// <summary>A corridor behind a secret door.</summary>
        Secret,
    }

    public enum LinkKind : byte
    {
        /// <summary>Two-way stair well.</summary>
        Stairs,
        /// <summary>One-way pit to the floor below.</summary>
        Drop,
    }

    /// <summary>The generation strategy of a floor.</summary>
    public enum FloorStyle : byte
    {
        /// <summary>Rooms of varied size and shape scattered with spacing, joined by corridors.</summary>
        Rooms,
        /// <summary>Binary space partition: orderly, fortress-like rooms.</summary>
        Bsp,
        /// <summary>Natural caves (noise + cellular automata), chambers and tunnels.</summary>
        Caverns,
        /// <summary>Zones of built rooms, caverns and ruins stitched together.</summary>
        Hybrid,
        /// <summary>The original grid maze (optionally with the old RoomBehaviour prefabs), with corridors and loops.</summary>
        GridMaze,
    }

    /// <summary>Grid directions. +y in the grid is +z (north) in the world.</summary>
    public enum Dir4 : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    public enum PlacementKind : byte
    {
        PlayerSpawn,
        EntrancePortal,
        ExitPortal,
        Mob,
        Boss,
        Loot,
        Interactable,
        PointOfInterest,
        Hazard,
        Decoration,
        Light,
    }

    public enum SizeClass : byte
    {
        Small,
        Medium,
        Large,
        Huge,
    }

    /// <summary>Where in the dungeon's structure a role goes.</summary>
    public enum RolePlacement : byte
    {
        Any,
        /// <summary>A dead end.</summary>
        Leaf,
        OnMainPath,
        OffMainPath,
        /// <summary>An area with three or more ways out.</summary>
        Hub,
        /// <summary>The last main-path area before the floor's exit / departure.</summary>
        EndOfMainPath,
    }

    public enum SizePreference : byte
    {
        Any,
        Small,
        Medium,
        Large,
    }

    /// <summary>Where inside an area a prop goes.</summary>
    public enum PropPlacement : byte
    {
        Anywhere,
        WallAdjacent,
        Center,
        Corner,
        Corridor,
        Doorway,
        DeadEnd,
        Chokepoint,
        /// <summary>Where built structure breaks into natural rock.</summary>
        Transition,
    }

    /// <summary>Built-in stand-ins used when a table entry or theme slot has no prefab.</summary>
    public enum DungeonPrimitive : byte
    {
        None,
        Chest,
        Barrel,
        Crate,
        Torch,
        Brazier,
        Crystal,
        Altar,
        Pillar,
        Rubble,
        SpikeTrap,
        Fountain,
        Bookshelf,
        Bones,
        Mushroom,
        PlaceholderMob,
        PlaceholderBoss,
        EntrancePortal,
        ExitPortal,
    }

    public enum AnchorKind : byte
    {
        Entrance,
        Exit,
        /// <summary>Landing at the bottom of a stair well (arrival on the lower floor).</summary>
        StairsUpLanding,
        /// <summary>Landing at the top of a stair well (departure on the upper floor).</summary>
        StairsDownLanding,
        /// <summary>Room holding a pit on the upper floor.</summary>
        DropSource,
        /// <summary>Room under a pit on the lower floor.</summary>
        DropLanding,
    }

    public static class Dir4Util
    {
        public static readonly int[] DX = { 0, 1, 0, -1 };
        public static readonly int[] DY = { 1, 0, -1, 0 };

        public static Dir4 Opposite(this Dir4 d) => (Dir4)(((int)d + 2) & 3);

        public static Dir4 Left(this Dir4 d) => (Dir4)(((int)d + 3) & 3);

        public static Dir4 Right(this Dir4 d) => (Dir4)(((int)d + 1) & 3);

        public static Vector2Int Delta(this Dir4 d) => new Vector2Int(DX[(int)d], DY[(int)d]);

        /// <summary>Yaw in degrees for an object facing this direction (0 = +z / north).</summary>
        public static float Yaw(this Dir4 d) => (int)d * 90f;

        public static bool IsVertical(this Dir4 d) => d == Dir4.North || d == Dir4.South;

        /// <summary>The direction of a unit step (dx, dy); North when the step is zero or diagonal-major in y.</summary>
        public static Dir4 FromDelta(int dx, int dy)
        {
            if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
                return dx > 0 ? Dir4.East : Dir4.West;
            return dy >= 0 ? Dir4.North : Dir4.South;
        }

        public static bool Contains(this ZoneMask mask, ZoneStyle style)
        {
            return (mask & (ZoneMask)(1 << (int)style)) != 0;
        }
    }
}
