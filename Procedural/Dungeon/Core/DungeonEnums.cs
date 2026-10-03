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
    public enum CellFlags : uint
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
        /// <summary>A gate, locked door or shortcut door stands here (kept free of props and mobs).</summary>
        Locked = 1 << 13,
        /// <summary>
        /// Open air over a drop into darkness (islands over a chasm, the astral void). On a solid cell: the chasm itself -
        /// no floor, cliffs down to its bottom, the ceiling still above. On a walkable cell: a bridge across it.
        /// </summary>
        Chasm = 1 << 14,
        /// <summary>Part of a building in an open cavern (undercity): its walls stop at a roof, open air above.</summary>
        Roofed = 1 << 15,
        /// <summary>Under the open cavern sky (undercity streets and plazas): rock ceiling, no doors.</summary>
        Outdoor = 1 << 16,
        /// <summary>A climbing shaft between floors (vines down a giant root): like a drop's pit, but climbable both ways.</summary>
        Climb = 1 << 17,
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
        // ---- Added later (append only: roles are saved as numbers) ----
        /// <summary>A guardian room: an elite mob (bigger, higher tier) and its guards; the doors lock until they fall.</summary>
        MiniBoss,
        /// <summary>A locked treasure room: its key lies elsewhere on the same floor.</summary>
        Vault,
        /// <summary>A trap gauntlet: spikes, fire jets, blades and darts, with a reward at the far end.</summary>
        TrapRoom,
        /// <summary>Pressure plates: step on them in the order they light up to reveal the reward.</summary>
        Puzzle,
        /// <summary>Looks empty: the doors lock behind the players and mobs appear in waves.</summary>
        Ambush,
        Library,
        Armory,
        Prison,
        /// <summary>Sarcophagi and candles; sometimes the dead rise when the players come in.</summary>
        Crypt,
        Laboratory,
        Garden,
        /// <summary>A throne hall guarded by an elite, like a guardian room.</summary>
        Throne,
        /// <summary>A nest that keeps spawning mobs until it is destroyed.</summary>
        Nest,
        /// <summary>Cursed and blood altars: trade health or take a curse for a reward.</summary>
        Gambling,
        /// <summary>A kitchen / mess hall: long tables, stoves, food that heals a little.</summary>
        Kitchen,
        /// <summary>A gallery of paintings; one of them is a way into a hidden pocket room.</summary>
        Gallery,
        /// <summary>Barracks: rows of beds and sleeping soldiers you can sneak past.</summary>
        Barracks,
        /// <summary>A pit fight: spectators, gates the waves come through, and a champion.</summary>
        Colosseum,
        /// <summary>A greenhouse / herbarium: rare herbs and poisonous plants.</summary>
        Greenhouse,
        /// <summary>A wine cellar: barrels to break and a hidden lever that opens a secret room.</summary>
        WineCellar,
        /// <summary>A map room: its carved table reveals the floor's map.</summary>
        MapRoom,
        /// <summary>A room that fills with poison gas every few seconds (a valve shuts it off).</summary>
        GasChamber,
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
        // ---- Added later (append only) ----
        /// <summary>A walkway carved across a chasm (no doors at its ends).</summary>
        Bridge,
        /// <summary>A pair of teleport pads (or a magic painting): nothing is carved, the pads join the two areas.</summary>
        Portal,
        /// <summary>A moving platform carrying players across a chasm along its track (nothing is carved).</summary>
        Platform,
    }

    public enum LinkKind : byte
    {
        /// <summary>Two-way stair well.</summary>
        Stairs,
        /// <summary>One-way pit to the floor below.</summary>
        Drop,
        // ---- Added later (append only) ----
        /// <summary>A spiral staircase in a round shaft; consecutive spirals share the shaft (a tower's stair core).</summary>
        Spiral,
        /// <summary>A shaft with vines (down a giant root): climbed in both directions.</summary>
        Climb,
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
        /// <summary>A fortress around a central keep: rings of rooms, gates into the keep, corner towers.</summary>
        Citadel,
        /// <summary>A lattice of ossuary chambers joined by long galleries lined with burial niches.</summary>
        Catacombs,
        /// <summary>A small round floor around a spiral stair core: a ring hall and chambers (tower dungeons stack them).</summary>
        Tower,
        /// <summary>A city in a huge cavern: streets and plazas under an open rock sky, buildings you can enter.</summary>
        Undercity,
        /// <summary>An organic hive: round cells packed like a honeycomb, joined by fleshy tunnels.</summary>
        Hive,
        /// <summary>Islands over a chasm: rock platforms joined by bridges and moving platforms; fall and you drop.</summary>
        Islands,
        /// <summary>A dragon's den: one huge cavern with a hoard, side tunnels and side caves.</summary>
        Den,
        /// <summary>The astral void: floating platforms joined by portals, moving platforms and a few bridges; gravity flips.</summary>
        Astral,
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
        // ---- Added later (append only) ----
        /// <summary>A key that opens a Locked Door with the same Link id.</summary>
        Key,
        /// <summary>A door that opens for a player carrying the key with the same Link id (vaults).</summary>
        LockedDoor,
        /// <summary>A gate that closes while its room's encounter is fought (boss, arena, guardian, ambush rooms).</summary>
        Gate,
        /// <summary>A pressure plate of a puzzle room (Link = its place in the order).</summary>
        Switch,
        /// <summary>Runs a room's event: locking, waves, puzzles (no visible object).</summary>
        RoomController,
        /// <summary>A door that only opens from one side - a shortcut back once it is reached from the far side.</summary>
        Shortcut,
        /// <summary>A teleport pad or magic painting; pads with the same Link are a pair.</summary>
        Teleporter,
        /// <summary>A moving platform; Link = the Platform connection whose track it follows.</summary>
        MovingPlatform,
        /// <summary>A tripwire across a corridor; it fires the Arrow Launchers with the same Link.</summary>
        Tripwire,
        /// <summary>An arrow launcher in a corridor wall, fired by its tripwire (same Link).</summary>
        ArrowLauncher,
        /// <summary>A wall that rises and sinks as the floor reshuffles its routes (Link = its connection).</summary>
        ShiftingWall,
        /// <summary>A lever or valve: opens the secret door / shuts the gas (Link = the cell or area it works).</summary>
        Lever,
        /// <summary>A nest that spawns mobs until destroyed (Entry = the encounter it spawns).</summary>
        Nest,
        /// <summary>A room-wide effect run by the builder (Link = <see cref="AreaEffectKind"/>), e.g. poison gas.</summary>
        AreaEffect,
    }

    /// <summary>What an <see cref="PlacementKind.AreaEffect"/> placement does.</summary>
    public enum AreaEffectKind
    {
        PoisonGas = 0,
    }

    /// <summary>Special behaviour of a placed mob.</summary>
    public enum MobOrder : byte
    {
        None,
        /// <summary>Asleep until a player comes close, makes noise or hurts it (barracks).</summary>
        Sleep,
        /// <summary>Roams between rooms of its floor (roaming mini-bosses).</summary>
        Roam,
        /// <summary>The champion of a pit fight: the last wave, an elite.</summary>
        Champion,
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
        /// <summary>Against the wall farthest from the room's ways in, facing the room (thrones, altars).</summary>
        BackWall,
        /// <summary>Anywhere off the walking route between the floor's way in and way out (hazards that shouldn't block it).</summary>
        OffPath,
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
        // ---- Added later (append only) ----
        Sarcophagus,
        WeaponRack,
        ArmorStand,
        Cage,
        AlchemyTable,
        Cauldron,
        Throne,
        Banner,
        Statue,
        Table,
        Candles,
        Vines,
        PressurePlate,
        Key,
        Gate,
        LockedDoor,
        LavaPool,
        IceSpikes,
        SporeVent,
        DartTrap,
        FireTrap,
        BladeTrap,
        Pedestal,
        HerbPatch,
        Cobweb,
        LampPost,
        MarketStall,
        Cart,
        Well,
        Bed,
        LongTable,
        Bench,
        Stove,
        Pots,
        Painting,
        Planter,
        RareHerb,
        PoisonPlant,
        WineRack,
        WineBarrel,
        MapTable,
        BloodAltar,
        CursedAltar,
        Nest,
        Spectators,
        GoldPile,
        EggSac,
        Fungus,
        GiantRoot,
        Tripwire,
        LogTrap,
        GasVent,
        Lever,
        Teleporter,
        MovingPlatform,
        Railing,
        Stalactite,
        StarMote,
        DragonBones,
        Debris,
    }

    /// <summary>A floor-wide twist rolled per floor: changes the light, the props and the hazards of the whole floor.</summary>
    public enum FloorModifier : byte
    {
        None,
        /// <summary>Water covers the low ground (rooms and corridors wade in it, caves get pools).</summary>
        Flooded,
        /// <summary>Lava pools in the caves and ruins, a red glow.</summary>
        Molten,
        /// <summary>Roots, vines, mushrooms and poisonous spore vents.</summary>
        Overgrown,
        /// <summary>Few lights, thick fog, more mobs.</summary>
        Darkness,
        /// <summary>Ice crystals and frost, a cold blue light.</summary>
        Frozen,
    }

    /// <summary>Floors a prop may appear on, by their modifier (Any = every floor).</summary>
    [Flags]
    public enum FloorModifierMask : byte
    {
        Any = 0,
        Normal = 1,
        Flooded = 2,
        Molten = 4,
        Overgrown = 8,
        Darkness = 16,
        Frozen = 32,
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
        /// <summary>Room around the top of a climbing shaft (upper floor).</summary>
        ClimbTop,
        /// <summary>Room under a climbing shaft (lower floor).</summary>
        ClimbBottom,
    }

    /// <summary>Questions about the kinds above.</summary>
    public static class DungeonKinds
    {
        /// <summary>Connections crossed without walking a carved path: teleport pads and moving platforms.</summary>
        public static bool IsJump(this ConnectionKind kind) => kind == ConnectionKind.Portal || kind == ConnectionKind.Platform;

        /// <summary>Floor styles built over a chasm (platforms, bridges, falls).</summary>
        public static bool HasChasm(this FloorStyle style) => style == FloorStyle.Islands || style == FloorStyle.Astral;

        /// <summary>Links that can't be taken back up.</summary>
        public static bool IsOneWay(this LinkKind kind) => kind == LinkKind.Drop;
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

        /// <summary>Does a prop with this mask belong on a floor with <paramref name="modifier"/>? (Any = yes.)</summary>
        public static bool Allows(this FloorModifierMask mask, FloorModifier modifier)
        {
            if (mask == FloorModifierMask.Any)
                return true;
            FloorModifierMask bit = modifier == FloorModifier.None ? FloorModifierMask.Normal : (FloorModifierMask)(1 << (int)modifier);
            return (mask & bit) != 0;
        }
    }
}
