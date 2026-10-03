using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>Ready-made kinds of dungeon (see <see cref="DungeonTypes.Apply"/>).</summary>
    public enum DungeonType
    {
        /// <summary>A bit of everything: rooms, fortresses, caves, mazes, citadels and catacombs.</summary>
        Classic,
        /// <summary>Crypts and catacombs: ossuaries, burial niches, sarcophagi, dark floors, the dead rising.</summary>
        Crypt,
        /// <summary>Natural caves: huge domed caverns, flooded, overgrown, molten and frozen depths.</summary>
        DeepCaves,
        /// <summary>A military fortress: citadels and orderly halls, armories, prisons, guardians, a throne.</summary>
        Fortress,
        /// <summary>Mazes: winding corridors, dead ends full of loot, trap gauntlets and puzzles.</summary>
        Labyrinth,
        /// <summary>A sunken temple: flooded halls, shrines, libraries, puzzles, vaults.</summary>
        SunkenTemple,
        /// <summary>A volcanic forge: molten caverns, armories, laboratories, trap gauntlets.</summary>
        VolcanicForge,
        /// <summary>A cathedral: soaring vaulted halls, chapels with apses, shrines, crypts.</summary>
        Cathedral,
        /// <summary>A prison: cell blocks, dark corridors, ambushes, a warden.</summary>
        Prison,
        /// <summary>Frozen depths: icy caverns and ruins.</summary>
        FrozenDepths,
        /// <summary>Overgrown ruins: collapsed rooms taken over by roots and gardens, vines to climb between floors.</summary>
        OvergrownRuins,
        /// <summary>A tower: small round floors stacked tightly, one spiral staircase through them all.</summary>
        Tower,
        /// <summary>A ruined city underground: streets, plazas and buildings you can enter.</summary>
        Undercity,
        /// <summary>A fungal hive: honeycombed cells, fleshy tunnels, brood chambers and nests.</summary>
        FungalHive,
        /// <summary>Islands over a chasm: ledges and islands joined by bridges and floating platforms; falls drop a floor.</summary>
        ChasmIslands,
        /// <summary>A dragon's den: caves leading down to one huge cavern with the hoard and its keeper.</summary>
        DragonsDen,
        /// <summary>An astral void: floating platforms, portals between them and gravity that flips.</summary>
        AstralVoid,
    }

    /// <summary>
    /// Turns a <see cref="DungeonProfile"/> into one kind of dungeon by setting its floor styles, openness and
    /// complexity, room sizes and shapes, ceilings, connections, stairs and drops, special rooms, mechanics and floor
    /// modifiers - and, for a theme, its colours and atmosphere. Tables (mobs, loot, props), the theme reference and the
    /// build settings are kept. Use it from code or with the profile inspector's Dungeon Type menu.
    /// </summary>
    public static class DungeonTypes
    {
        public static string Describe(DungeonType type)
        {
            switch (type)
            {
                case DungeonType.Crypt: return "Catacombs and crypts: ossuary chambers, galleries lined with burial niches, sarcophagi, dark floors, the dead rising.";
                case DungeonType.DeepCaves: return "Natural caves: huge domed caverns and tunnels, flooded, overgrown, molten and frozen depths.";
                case DungeonType.Fortress: return "A fortress: citadel keeps and orderly halls, armories, prisons, guardians and a throne room.";
                case DungeonType.Labyrinth: return "Mazes and catacombs: winding passages, dead ends full of loot, trap gauntlets, puzzles, vaults.";
                case DungeonType.SunkenTemple: return "A sunken temple: flooded halls and caverns, shrines, libraries, puzzles and vaults.";
                case DungeonType.VolcanicForge: return "A volcanic forge: molten caverns and works, armories, laboratories, trap gauntlets, guardians.";
                case DungeonType.Cathedral: return "A cathedral: soaring vaulted halls, octagons and apses, shrines, crypts and libraries.";
                case DungeonType.Prison: return "A prison: cell blocks and tight corridors, dark floors, ambushes, a warden and a vault.";
                case DungeonType.FrozenDepths: return "Frozen depths: icy caverns and ruins under a cold blue light.";
                case DungeonType.OvergrownRuins: return "Overgrown ruins: collapsed rooms taken over by giant roots, gardens and spore vents; vines to climb between floors.";
                case DungeonType.Tower: return "A tower: small round floors stacked tightly around one spiral staircase - barracks, kitchens, a map room, a throne at the top.";
                case DungeonType.Undercity: return "A ruined city underground: streets and plazas under a cavern sky, buildings to enter, cellars, galleries and gambling dens.";
                case DungeonType.FungalHive: return "A fungal hive: honeycombed cells and fleshy tunnels, brood chambers, nests that keep spawning, gas chambers.";
                case DungeonType.ChasmIslands: return "Islands over a chasm: ledges and islands joined by bridges and floating platforms; a fall drops you to the floor below.";
                case DungeonType.DragonsDen: return "A dragon's den: caves leading down to one huge cavern with the hoard, bones and its keeper.";
                case DungeonType.AstralVoid: return "An astral void: floating platforms over nothing, teleport pads between them and gravity that flips.";
                default: return "A bit of everything: rooms, fortresses, caverns, mazes, citadels and catacombs, with every special room.";
            }
        }

        /// <summary>Reshapes <paramref name="p"/> into a kind of dungeon (keeps its tables, theme and build settings).</summary>
        public static void Apply(DungeonProfile p, DungeonType type)
        {
            // Start from the defaults of everything this sets.
            var fresh = ScriptableObject.CreateInstance<DungeonProfile>();
            p.styles = fresh.styles;
            p.openness = fresh.openness;
            p.complexity = fresh.complexity;
            p.naturalWeightPerFloor = fresh.naturalWeightPerFloor;
            p.rooms = fresh.rooms;
            p.caves = fresh.caves;
            p.hybrid = fresh.hybrid;
            p.citadel = fresh.citadel;
            p.catacombs = fresh.catacombs;
            p.connections = fresh.connections;
            p.links = fresh.links;
            p.ceilings = fresh.ceilings;
            p.mechanics = fresh.mechanics;
            p.floorModifiers = fresh.floorModifiers;
            p.tower = fresh.tower;
            p.undercity = fresh.undercity;
            p.hive = fresh.hive;
            p.islands = fresh.islands;
            p.den = fresh.den;
            p.astral = fresh.astral;
            p.overrideLastFloorStyle = false;
            p.lastFloorStyle = fresh.lastFloorStyle;
            p.roles = DungeonProfile.DefaultRoles();
            DestroyTemp(fresh);

            StyleWeights s = p.styles;
            switch (type)
            {
                case DungeonType.Crypt:
                    Styles(s, rooms: 0.4f, bsp: 0.8f, caverns: 0f, hybrid: 0.2f, maze: 0.3f, citadel: 0.2f, catacombs: 1.4f);
                    p.openness = new FloatRange(0.2f, 0.5f);
                    p.complexity = new FloatRange(0.5f, 0.85f);
                    p.naturalWeightPerFloor = 0f;
                    Ceilings(p, CeilingPreset.Standard);
                    Modifiers(p, 0.45f, flooded: 0.4f, molten: 0f, overgrown: 0.2f, darkness: 1.4f, frozen: 0.2f);
                    p.mechanics.cryptAmbushChance = 0.7f;
                    Boost(p, AreaRole.Crypt, 0.8f, 1, 2);
                    Boost(p, AreaRole.Library, 0.4f);
                    Boost(p, AreaRole.Puzzle, 0.4f);
                    Boost(p, AreaRole.Secret, 0.5f);
                    Boost(p, AreaRole.Gambling, 0.4f);
                    Boost(p, AreaRole.GasChamber, 0.3f);
                    Remove(p, AreaRole.Garden, AreaRole.Armory, AreaRole.Laboratory, AreaRole.Kitchen, AreaRole.Greenhouse, AreaRole.Barracks);
                    break;

                case DungeonType.DeepCaves:
                    Styles(s, rooms: 0f, bsp: 0f, caverns: 1.5f, hybrid: 0.5f, maze: 0f, citadel: 0f, catacombs: 0f);
                    p.openness = new FloatRange(0.45f, 0.9f);
                    p.complexity = new FloatRange(0.3f, 0.7f);
                    Ceilings(p, CeilingPreset.Tall);
                    p.caves.ceilingLimits = new FloatRange(4f, 15f);
                    p.caves.ceilingPerWallDistance = 0.9f;
                    p.links.dropChance = 0.55f;
                    Modifiers(p, 0.6f, flooded: 1f, molten: 0.7f, overgrown: 1f, darkness: 0.6f, frozen: 0.6f);
                    p.floorModifiers.firstFloor = 0;
                    Boost(p, AreaRole.Garden, 0.5f);
                    Boost(p, AreaRole.Ambush, 0.45f);
                    Boost(p, AreaRole.Nest, 0.5f);
                    Remove(p, AreaRole.Library, AreaRole.Armory, AreaRole.Prison, AreaRole.Laboratory, AreaRole.Throne, AreaRole.Puzzle,
                        AreaRole.Kitchen, AreaRole.Gallery, AreaRole.Barracks, AreaRole.WineCellar, AreaRole.Greenhouse);
                    break;

                case DungeonType.Fortress:
                    Styles(s, rooms: 0.6f, bsp: 1.2f, caverns: 0f, hybrid: 0.2f, maze: 0f, citadel: 1.2f, catacombs: 0f);
                    p.openness = new FloatRange(0.35f, 0.7f);
                    p.complexity = new FloatRange(0.4f, 0.75f);
                    p.naturalWeightPerFloor = 0.05f;
                    Ceilings(p, CeilingPreset.Tall);
                    p.rooms.shapes.pillaredHall = 1.2f;
                    p.rooms.shapes.octagon = 1f;
                    Modifiers(p, 0.15f, flooded: 0.5f, molten: 0.2f, overgrown: 0.2f, darkness: 0.6f, frozen: 0.3f);
                    Boost(p, AreaRole.Armory, 0.7f);
                    Boost(p, AreaRole.Prison, 0.5f);
                    Boost(p, AreaRole.MiniBoss, 0.6f);
                    Boost(p, AreaRole.Throne, 0.7f);
                    Boost(p, AreaRole.Arena, 0.6f);
                    Boost(p, AreaRole.Barracks, 0.6f);
                    Boost(p, AreaRole.Kitchen, 0.45f);
                    Boost(p, AreaRole.MapRoom, 0.4f);
                    Boost(p, AreaRole.Colosseum, 0.35f);
                    Remove(p, AreaRole.Garden, AreaRole.Greenhouse, AreaRole.Nest);
                    break;

                case DungeonType.Labyrinth:
                    Styles(s, rooms: 0.2f, bsp: 0f, caverns: 0f, hybrid: 0f, maze: 1.4f, citadel: 0f, catacombs: 1f);
                    p.openness = new FloatRange(0.15f, 0.45f);
                    p.complexity = new FloatRange(0.7f, 1f);
                    p.naturalWeightPerFloor = 0f;
                    p.maze.blockSize = 5;
                    p.maze.gap = 3;
                    p.maze.pruneFraction = new FloatRange(0f, 0.15f);
                    p.connections.loopChance = new FloatRange(0.03f, 0.15f);
                    p.connections.deadEndShare = new FloatRange(0.35f, 0.6f);
                    p.population.deadEndLootChance = 0.75f;
                    Ceilings(p, CeilingPreset.Standard);
                    Modifiers(p, 0.25f, flooded: 0.6f, molten: 0.2f, overgrown: 0.4f, darkness: 1f, frozen: 0.3f);
                    Boost(p, AreaRole.TrapRoom, 0.6f);
                    Boost(p, AreaRole.Puzzle, 0.5f);
                    Boost(p, AreaRole.Vault, 0.5f);
                    Boost(p, AreaRole.Treasure, 1f, 1, 3);
                    Boost(p, AreaRole.MapRoom, 0.6f);
                    Boost(p, AreaRole.GasChamber, 0.4f);
                    p.mechanics.tripwireChance = 0.3f;
                    p.mechanics.shiftingChance = 0.6f;
                    break;

                case DungeonType.SunkenTemple:
                    Styles(s, rooms: 0.7f, bsp: 0.3f, caverns: 0.5f, hybrid: 1.2f, maze: 0f, citadel: 0.4f, catacombs: 0f);
                    p.openness = new FloatRange(0.45f, 0.8f);
                    Ceilings(p, CeilingPreset.Tall);
                    p.rooms.shapes.apse = 1f;
                    p.rooms.shapes.ring = 0.6f;
                    Modifiers(p, 0.75f, flooded: 2.5f, molten: 0f, overgrown: 0.8f, darkness: 0.3f, frozen: 0f);
                    p.floorModifiers.firstFloor = 0;
                    p.floorModifiers.waterLevel = 0.45f;
                    Boost(p, AreaRole.Shrine, 0.8f, 1, 2);
                    Boost(p, AreaRole.Library, 0.5f);
                    Boost(p, AreaRole.Puzzle, 0.55f);
                    Boost(p, AreaRole.Vault, 0.5f);
                    Boost(p, AreaRole.Gambling, 0.4f);
                    Remove(p, AreaRole.Armory, AreaRole.Prison, AreaRole.Barracks, AreaRole.Kitchen);
                    break;

                case DungeonType.VolcanicForge:
                    Styles(s, rooms: 0.3f, bsp: 0.5f, caverns: 1f, hybrid: 1f, maze: 0f, citadel: 0.3f, catacombs: 0f);
                    p.openness = new FloatRange(0.4f, 0.8f);
                    Ceilings(p, CeilingPreset.Tall);
                    Modifiers(p, 0.7f, flooded: 0f, molten: 3f, overgrown: 0f, darkness: 0.3f, frozen: 0f);
                    p.floorModifiers.firstFloor = 0;
                    Boost(p, AreaRole.Armory, 0.6f);
                    Boost(p, AreaRole.Laboratory, 0.5f);
                    Boost(p, AreaRole.TrapRoom, 0.55f);
                    Boost(p, AreaRole.MiniBoss, 0.55f);
                    Boost(p, AreaRole.Colosseum, 0.4f);
                    Remove(p, AreaRole.Garden, AreaRole.Library, AreaRole.Greenhouse, AreaRole.WineCellar);
                    break;

                case DungeonType.Cathedral:
                    Styles(s, rooms: 1.2f, bsp: 0.3f, caverns: 0f, hybrid: 0.2f, maze: 0f, citadel: 0.8f, catacombs: 0.3f);
                    p.openness = new FloatRange(0.6f, 0.95f);
                    p.complexity = new FloatRange(0.25f, 0.6f);
                    p.naturalWeightPerFloor = 0f;
                    Ceilings(p, CeilingPreset.Cathedral);
                    p.rooms.shapes.pillaredHall = 1.5f;
                    p.rooms.shapes.apse = 1.2f;
                    p.rooms.shapes.octagon = 1.2f;
                    p.rooms.shapes.cross = 1f;
                    p.ceilings.vaultChance = 0.9f;
                    Modifiers(p, 0.15f, flooded: 0.3f, molten: 0f, overgrown: 0.3f, darkness: 0.7f, frozen: 0.2f);
                    Boost(p, AreaRole.Shrine, 0.8f, 1, 2);
                    Boost(p, AreaRole.Crypt, 0.5f);
                    Boost(p, AreaRole.Library, 0.5f);
                    Boost(p, AreaRole.Throne, 0.5f);
                    Boost(p, AreaRole.Gallery, 0.6f);
                    Remove(p, AreaRole.Prison, AreaRole.Laboratory, AreaRole.Garden, AreaRole.Barracks, AreaRole.Nest, AreaRole.GasChamber);
                    break;

                case DungeonType.Prison:
                    Styles(s, rooms: 0.3f, bsp: 1.3f, caverns: 0f, hybrid: 0.2f, maze: 0.6f, citadel: 0.3f, catacombs: 0.2f);
                    p.openness = new FloatRange(0.15f, 0.45f);
                    p.complexity = new FloatRange(0.6f, 0.95f);
                    p.naturalWeightPerFloor = 0f;
                    Ceilings(p, CeilingPreset.Standard);
                    Modifiers(p, 0.35f, flooded: 0.6f, molten: 0f, overgrown: 0.2f, darkness: 1.5f, frozen: 0.2f);
                    Boost(p, AreaRole.Prison, 1f, 1, 3);
                    Boost(p, AreaRole.Ambush, 0.55f);
                    Boost(p, AreaRole.MiniBoss, 0.5f);
                    Boost(p, AreaRole.Armory, 0.4f);
                    Boost(p, AreaRole.Vault, 0.45f);
                    Boost(p, AreaRole.Barracks, 0.5f);
                    Boost(p, AreaRole.Kitchen, 0.4f);
                    Boost(p, AreaRole.GasChamber, 0.45f);
                    Remove(p, AreaRole.Garden, AreaRole.Shrine, AreaRole.Greenhouse, AreaRole.Gallery);
                    break;

                case DungeonType.FrozenDepths:
                    Styles(s, rooms: 0.2f, bsp: 0.2f, caverns: 1.3f, hybrid: 0.8f, maze: 0f, citadel: 0.2f, catacombs: 0f);
                    p.openness = new FloatRange(0.4f, 0.85f);
                    Ceilings(p, CeilingPreset.Tall);
                    Modifiers(p, 0.8f, flooded: 0f, molten: 0f, overgrown: 0f, darkness: 0.4f, frozen: 3f);
                    p.floorModifiers.firstFloor = 0;
                    Boost(p, AreaRole.Ambush, 0.45f);
                    Boost(p, AreaRole.Shrine, 0.5f);
                    Remove(p, AreaRole.Garden, AreaRole.Laboratory, AreaRole.Library, AreaRole.Greenhouse, AreaRole.Kitchen, AreaRole.WineCellar);
                    break;

                case DungeonType.OvergrownRuins:
                    Styles(s, rooms: 0.5f, bsp: 0.2f, caverns: 0.4f, hybrid: 1.5f, maze: 0f, citadel: 0.2f, catacombs: 0f);
                    p.hybrid.ruinsWeight = 1.5f;
                    p.hybrid.ruinsErosion = 0.5f;
                    p.rooms.shapes.ruined = 1.5f;
                    p.openness = new FloatRange(0.45f, 0.85f);
                    Ceilings(p, CeilingPreset.Standard);
                    Modifiers(p, 0.7f, flooded: 0.4f, molten: 0f, overgrown: 3f, darkness: 0.3f, frozen: 0f);
                    p.floorModifiers.firstFloor = 0;
                    Boost(p, AreaRole.Garden, 0.8f, 1, 2);
                    Boost(p, AreaRole.Shrine, 0.5f);
                    Boost(p, AreaRole.Library, 0.4f);
                    Boost(p, AreaRole.Greenhouse, 0.6f);
                    // The roots of a giant tree run through the floors: vines to climb up and down.
                    p.links.climbChance = 0.45f;
                    Remove(p, AreaRole.Prison, AreaRole.Armory, AreaRole.Barracks);
                    break;

                case DungeonType.Tower:
                    Styles(s, rooms: 0f, bsp: 0f, caverns: 0f, hybrid: 0f, maze: 0f, citadel: 0f, catacombs: 0f, tower: 1f);
                    p.openness = new FloatRange(0.4f, 0.75f);
                    p.complexity = new FloatRange(0.3f, 0.6f);
                    p.naturalWeightPerFloor = 0f;
                    Ceilings(p, CeilingPreset.Standard);
                    Modifiers(p, 0.15f, flooded: 0f, molten: 0f, overgrown: 0.4f, darkness: 0.8f, frozen: 0.3f);
                    Boost(p, AreaRole.Barracks, 0.6f);
                    Boost(p, AreaRole.Kitchen, 0.5f);
                    Boost(p, AreaRole.Library, 0.5f);
                    Boost(p, AreaRole.MapRoom, 0.5f);
                    Boost(p, AreaRole.Armory, 0.5f);
                    Boost(p, AreaRole.Throne, 0.5f);
                    Remove(p, AreaRole.Garden, AreaRole.Nest, AreaRole.Colosseum, AreaRole.GasChamber);
                    break;

                case DungeonType.Undercity:
                    Styles(s, rooms: 0.1f, bsp: 0f, caverns: 0.15f, hybrid: 0f, maze: 0f, citadel: 0f, catacombs: 0.15f, undercity: 1.5f);
                    p.openness = new FloatRange(0.45f, 0.85f);
                    p.complexity = new FloatRange(0.35f, 0.7f);
                    Ceilings(p, CeilingPreset.Tall);
                    Modifiers(p, 0.3f, flooded: 0.5f, molten: 0f, overgrown: 0.6f, darkness: 1f, frozen: 0.2f);
                    Boost(p, AreaRole.Kitchen, 0.5f);
                    Boost(p, AreaRole.WineCellar, 0.55f);
                    Boost(p, AreaRole.Gallery, 0.45f);
                    Boost(p, AreaRole.Gambling, 0.45f);
                    Boost(p, AreaRole.Barracks, 0.4f);
                    Boost(p, AreaRole.MapRoom, 0.5f);
                    Boost(p, AreaRole.Colosseum, 0.35f);
                    p.mechanics.roamerChance = 0.55f;
                    break;

                case DungeonType.FungalHive:
                    Styles(s, rooms: 0f, bsp: 0f, caverns: 0.4f, hybrid: 0f, maze: 0f, citadel: 0f, catacombs: 0f, hive: 1.5f);
                    p.openness = new FloatRange(0.35f, 0.75f);
                    p.complexity = new FloatRange(0.45f, 0.85f);
                    p.naturalWeightPerFloor = 0f;
                    Ceilings(p, CeilingPreset.Standard);
                    Modifiers(p, 0.7f, flooded: 0.6f, molten: 0f, overgrown: 3f, darkness: 0.8f, frozen: 0f);
                    p.floorModifiers.firstFloor = 0;
                    p.links.dropChance = 0.4f;
                    Boost(p, AreaRole.Nest, 0.85f, 1, 2);
                    Boost(p, AreaRole.GasChamber, 0.5f);
                    Boost(p, AreaRole.Greenhouse, 0.4f);
                    Boost(p, AreaRole.Ambush, 0.5f);
                    Remove(p, AreaRole.Library, AreaRole.Armory, AreaRole.Prison, AreaRole.Throne, AreaRole.Kitchen, AreaRole.Gallery,
                        AreaRole.Barracks, AreaRole.WineCellar, AreaRole.MapRoom, AreaRole.Colosseum, AreaRole.Laboratory);
                    break;

                case DungeonType.ChasmIslands:
                    Styles(s, rooms: 0f, bsp: 0f, caverns: 0.35f, hybrid: 0f, maze: 0f, citadel: 0f, catacombs: 0f, islands: 1.5f);
                    p.openness = new FloatRange(0.5f, 0.9f);
                    Ceilings(p, CeilingPreset.Tall);
                    Modifiers(p, 0.35f, flooded: 0f, molten: 0.8f, overgrown: 0.6f, darkness: 0.6f, frozen: 0.6f);
                    p.mechanics.chasmFallRule = ChasmFall.Death;
                    Boost(p, AreaRole.Shrine, 0.5f);
                    Boost(p, AreaRole.Gambling, 0.4f);
                    Boost(p, AreaRole.Nest, 0.4f);
                    Remove(p, AreaRole.Prison, AreaRole.Library, AreaRole.Kitchen, AreaRole.WineCellar, AreaRole.Barracks, AreaRole.Gallery);
                    break;

                case DungeonType.DragonsDen:
                    Styles(s, rooms: 0.2f, bsp: 0f, caverns: 1.2f, hybrid: 0.6f, maze: 0f, citadel: 0f, catacombs: 0f, islands: 0.2f);
                    p.overrideLastFloorStyle = true;
                    p.lastFloorStyle = FloorStyle.Den;
                    p.openness = new FloatRange(0.5f, 0.9f);
                    Ceilings(p, CeilingPreset.Tall);
                    p.caves.ceilingLimits = new FloatRange(4f, 15f);
                    Modifiers(p, 0.45f, flooded: 0.2f, molten: 2f, overgrown: 0.3f, darkness: 0.5f, frozen: 0f);
                    Boost(p, AreaRole.Treasure, 0.8f, 1, 2);
                    Boost(p, AreaRole.MiniBoss, 0.55f);
                    Boost(p, AreaRole.Nest, 0.45f);
                    Remove(p, AreaRole.Library, AreaRole.Prison, AreaRole.Kitchen, AreaRole.Gallery, AreaRole.Barracks, AreaRole.WineCellar,
                        AreaRole.Greenhouse, AreaRole.MapRoom, AreaRole.Laboratory);
                    break;

                case DungeonType.AstralVoid:
                    Styles(s, rooms: 0f, bsp: 0f, caverns: 0f, hybrid: 0f, maze: 0f, citadel: 0f, catacombs: 0f, islands: 0.3f, astral: 1.5f);
                    p.openness = new FloatRange(0.5f, 0.9f);
                    p.naturalWeightPerFloor = 0f;
                    Ceilings(p, CeilingPreset.Tall);
                    Modifiers(p, 0.2f, flooded: 0f, molten: 0f, overgrown: 0f, darkness: 1f, frozen: 0.5f);
                    Boost(p, AreaRole.Shrine, 0.6f);
                    Boost(p, AreaRole.Gambling, 0.55f);
                    Boost(p, AreaRole.Puzzle, 0.5f);
                    Boost(p, AreaRole.MapRoom, 0.4f);
                    Remove(p, AreaRole.Kitchen, AreaRole.Barracks, AreaRole.WineCellar, AreaRole.Prison, AreaRole.Garden, AreaRole.Greenhouse,
                        AreaRole.Armory, AreaRole.Library);
                    break;

                default:
                    Ceilings(p, CeilingPreset.Standard);
                    break;
            }
        }

        /// <summary>Sets a theme's colours and atmosphere for a kind of dungeon (materials and prefabs are kept).</summary>
        public static void ApplyTheme(DungeonTheme t, DungeonType type)
        {
            Color built = new Color(0.36f, 0.34f, 0.31f), wall = new Color(0.52f, 0.49f, 0.44f), cave = new Color(0.30f, 0.26f, 0.22f), caveWall = new Color(0.38f, 0.33f, 0.28f);
            Color ambient = new Color(0.12f, 0.11f, 0.13f), fog = new Color(0.05f, 0.045f, 0.06f), torch = new Color(1f, 0.62f, 0.3f);
            float density = 0.035f;
            Color voidColor = new Color(0.01f, 0.01f, 0.025f);
            switch (type)
            {
                case DungeonType.Crypt:
                case DungeonType.Prison:
                    built = new Color(0.3f, 0.3f, 0.31f); wall = new Color(0.42f, 0.42f, 0.44f); ambient = new Color(0.08f, 0.09f, 0.12f);
                    fog = new Color(0.03f, 0.04f, 0.06f); density = 0.045f; torch = new Color(0.95f, 0.7f, 0.45f);
                    break;
                case DungeonType.DeepCaves:
                    cave = new Color(0.27f, 0.24f, 0.21f); caveWall = new Color(0.34f, 0.3f, 0.26f); ambient = new Color(0.09f, 0.1f, 0.12f);
                    fog = new Color(0.03f, 0.04f, 0.05f); density = 0.03f;
                    break;
                case DungeonType.Fortress:
                    built = new Color(0.4f, 0.37f, 0.33f); wall = new Color(0.56f, 0.52f, 0.46f); ambient = new Color(0.14f, 0.12f, 0.12f);
                    density = 0.025f;
                    break;
                case DungeonType.SunkenTemple:
                    built = new Color(0.33f, 0.4f, 0.38f); wall = new Color(0.45f, 0.55f, 0.52f); ambient = new Color(0.08f, 0.14f, 0.15f);
                    fog = new Color(0.03f, 0.08f, 0.09f); density = 0.04f; torch = new Color(0.6f, 0.9f, 1f);
                    break;
                case DungeonType.VolcanicForge:
                    built = new Color(0.25f, 0.22f, 0.2f); wall = new Color(0.33f, 0.28f, 0.25f); cave = new Color(0.22f, 0.17f, 0.14f); caveWall = new Color(0.28f, 0.2f, 0.16f);
                    ambient = new Color(0.2f, 0.09f, 0.05f); fog = new Color(0.14f, 0.05f, 0.02f); density = 0.03f; torch = new Color(1f, 0.5f, 0.2f);
                    break;
                case DungeonType.Cathedral:
                    built = new Color(0.45f, 0.43f, 0.4f); wall = new Color(0.66f, 0.63f, 0.58f); ambient = new Color(0.16f, 0.15f, 0.17f);
                    fog = new Color(0.08f, 0.07f, 0.09f); density = 0.018f; torch = new Color(1f, 0.82f, 0.55f);
                    break;
                case DungeonType.FrozenDepths:
                    built = new Color(0.45f, 0.5f, 0.55f); wall = new Color(0.6f, 0.66f, 0.72f); cave = new Color(0.5f, 0.56f, 0.62f); caveWall = new Color(0.62f, 0.7f, 0.78f);
                    ambient = new Color(0.16f, 0.2f, 0.27f); fog = new Color(0.14f, 0.18f, 0.24f); density = 0.03f; torch = new Color(0.7f, 0.85f, 1f);
                    break;
                case DungeonType.OvergrownRuins:
                    built = new Color(0.32f, 0.34f, 0.27f); wall = new Color(0.45f, 0.47f, 0.38f); cave = new Color(0.26f, 0.28f, 0.2f); caveWall = new Color(0.33f, 0.36f, 0.26f);
                    ambient = new Color(0.1f, 0.15f, 0.09f); fog = new Color(0.04f, 0.08f, 0.04f); density = 0.035f;
                    break;
                case DungeonType.Tower:
                    built = new Color(0.4f, 0.38f, 0.35f); wall = new Color(0.55f, 0.52f, 0.47f); ambient = new Color(0.13f, 0.12f, 0.13f);
                    density = 0.022f; torch = new Color(1f, 0.68f, 0.35f);
                    break;
                case DungeonType.Undercity:
                    built = new Color(0.33f, 0.32f, 0.31f); wall = new Color(0.48f, 0.45f, 0.41f); cave = new Color(0.25f, 0.24f, 0.24f); caveWall = new Color(0.3f, 0.29f, 0.3f);
                    ambient = new Color(0.08f, 0.09f, 0.13f); fog = new Color(0.04f, 0.05f, 0.08f); density = 0.028f; torch = new Color(1f, 0.78f, 0.45f);
                    break;
                case DungeonType.FungalHive:
                    cave = new Color(0.3f, 0.22f, 0.26f); caveWall = new Color(0.4f, 0.27f, 0.33f); built = cave; wall = caveWall;
                    ambient = new Color(0.12f, 0.08f, 0.14f); fog = new Color(0.07f, 0.04f, 0.08f); density = 0.045f; torch = new Color(0.6f, 1f, 0.6f);
                    break;
                case DungeonType.ChasmIslands:
                    cave = new Color(0.28f, 0.28f, 0.3f); caveWall = new Color(0.35f, 0.35f, 0.38f);
                    ambient = new Color(0.09f, 0.1f, 0.14f); fog = new Color(0.03f, 0.04f, 0.07f); density = 0.03f;
                    break;
                case DungeonType.DragonsDen:
                    cave = new Color(0.3f, 0.22f, 0.17f); caveWall = new Color(0.38f, 0.27f, 0.2f);
                    ambient = new Color(0.16f, 0.09f, 0.06f); fog = new Color(0.1f, 0.05f, 0.03f); density = 0.025f; torch = new Color(1f, 0.55f, 0.25f);
                    break;
                case DungeonType.AstralVoid:
                    cave = new Color(0.22f, 0.2f, 0.3f); caveWall = new Color(0.3f, 0.27f, 0.4f); built = cave; wall = caveWall;
                    ambient = new Color(0.12f, 0.1f, 0.2f); fog = new Color(0.05f, 0.03f, 0.1f); density = 0.02f; torch = new Color(0.7f, 0.75f, 1f);
                    voidColor = new Color(0.03f, 0.01f, 0.07f);
                    break;
            }
            t.builtFloorColor = built;
            t.builtWallColor = wall;
            t.builtCeilingColor = built * 0.8f;
            t.caveFloorColor = cave;
            t.caveWallColor = caveWall;
            t.caveCeilingColor = cave * 0.8f;
            t.ambientLight = ambient;
            t.fogColor = fog;
            t.fogDensity = density;
            t.torchColor = torch;
            t.voidColor = voidColor;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Sets every style's weight (the newer styles are off unless given).</summary>
        private static void Styles(StyleWeights s, float rooms, float bsp, float caverns, float hybrid, float maze, float citadel, float catacombs,
            float tower = 0f, float undercity = 0f, float hive = 0f, float islands = 0f, float den = 0f, float astral = 0f)
        {
            s.tower = tower;
            s.undercity = undercity;
            s.hive = hive;
            s.islands = islands;
            s.den = den;
            s.astral = astral;
            s.rooms = rooms;
            s.bsp = bsp;
            s.caverns = caverns;
            s.hybrid = hybrid;
            s.gridMaze = maze;
            s.citadel = citadel;
            s.catacombs = catacombs;
        }

        private static void Ceilings(DungeonProfile p, CeilingPreset preset) => CeilingSettings.ApplyPreset(preset, p.rooms, p.caves, p.ceilings);

        private static void Modifiers(DungeonProfile p, float chance, float flooded, float molten, float overgrown, float darkness, float frozen)
        {
            FloorModifierSettings m = p.floorModifiers;
            m.chance = chance;
            m.flooded = flooded;
            m.molten = molten;
            m.overgrown = overgrown;
            m.darkness = darkness;
            m.frozen = frozen;
        }

        /// <summary>Makes a role more likely (chance) and optionally more numerous per floor.</summary>
        private static void Boost(DungeonProfile p, AreaRole role, float chance, int min = -1, int max = -1)
        {
            foreach (RoleRule r in p.roles)
            {
                if (r == null || r.role != role)
                    continue;
                r.chance = Mathf.Clamp01(chance);
                if (min >= 0 && max >= min)
                    r.perFloor = new IntRange(min, max);
            }
        }

        private static void Remove(DungeonProfile p, params AreaRole[] roles)
        {
            var set = new HashSet<AreaRole>(roles);
            p.roles.RemoveAll(r => r != null && set.Contains(r.role) && !r.required);
        }

        private static void DestroyTemp(Object o)
        {
            if (Application.isPlaying)
                Object.Destroy(o);
            else
                Object.DestroyImmediate(o);
        }
    }
}
