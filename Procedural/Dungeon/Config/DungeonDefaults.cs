using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Built-in population used when a profile has no tables: primitive chests, torches, crystals, barrels, bones,
    /// altars, fountains, traps, the furniture of the special rooms (library shelves, weapon racks, cages, sarcophagi,
    /// alchemy tables, thrones...), the floor modifiers' hazards (lava, spore vents) and placeholder mobs. Enough to see
    /// and test a dungeon without any art. <see cref="FillMissing"/> adds the special-room props a table doesn't cover.
    /// </summary>
    public static class DungeonDefaults
    {
        public static List<EncounterInfo> PlaceholderEncounters()
        {
            return new List<EncounterInfo>
            {
                new EncounterInfo
                {
                    Name = "Placeholder Mob", Placeholder = DungeonPrimitive.PlaceholderMob, Weight = 1f, Cost = 1,
                    PackSize = new IntRange(1, 3), PackRadius = 2.5f, MinFloor = 0, MaxFloor = -1, Progress = new FloatRange(0f, 1f),
                    Styles = ZoneMask.All, Roles = new AreaRole[0], Clearance = 1f,
                },
                new EncounterInfo
                {
                    Name = "Placeholder Brute", Placeholder = DungeonPrimitive.PlaceholderMob, Weight = 0.35f, Cost = 3,
                    PackSize = new IntRange(1, 1), PackRadius = 1.5f, MinFloor = 0, MaxFloor = -1, Progress = new FloatRange(0.3f, 1f),
                    Styles = ZoneMask.All, Roles = new AreaRole[0], Clearance = 1.5f, Tier = 1,
                },
                new EncounterInfo
                {
                    Index = 2, Name = "Placeholder Boss", Placeholder = DungeonPrimitive.PlaceholderBoss, Boss = true, Weight = 1f, Cost = 10,
                    PackSize = new IntRange(1, 1), PackRadius = 1f, MinFloor = 0, MaxFloor = -1, Progress = new FloatRange(0f, 1f),
                    Styles = ZoneMask.All, Roles = new AreaRole[0], Clearance = 2f, Tier = 2,
                },
            }.WithIndexes();
        }

        public static List<LootInfo> Loot()
        {
            var list = new List<LootInfo>
            {
                new LootInfo { Name = "Chest", Placeholder = DungeonPrimitive.Chest, Weight = 1f, Tier = 0, Progress = new FloatRange(0f, 1f), Styles = ZoneMask.All, Placement = PropPlacement.WallAdjacent, MaxFloor = -1 },
                new LootInfo { Name = "Ornate Chest", Placeholder = DungeonPrimitive.Chest, Weight = 0.5f, Tier = 1, Progress = new FloatRange(0.4f, 1f), Styles = ZoneMask.All, Placement = PropPlacement.WallAdjacent, MaxFloor = -1 },
                new LootInfo { Name = "Urn", Placeholder = DungeonPrimitive.Barrel, Weight = 0.7f, Tier = 0, Progress = new FloatRange(0f, 0.8f), Styles = ZoneMask.Built | ZoneMask.Ruins, Placement = PropPlacement.Corner, MaxFloor = -1 },
            };
            for (int i = 0; i < list.Count; i++)
                list[i].Index = i;
            return list;
        }

        public static List<PropInfo> Props(DungeonTheme theme)
        {
            Color torch = theme != null ? theme.torchColor : new Color(1f, 0.62f, 0.3f);
            Color crystal = theme != null ? theme.crystalColor : new Color(0.35f, 0.75f, 1f);
            float torchRange = theme != null ? theme.torchRange : 9f;
            var guarded = new[] { AreaRole.Arena, AreaRole.Boss, AreaRole.Entrance, AreaRole.MiniBoss, AreaRole.Throne };
            var list = new List<PropInfo>
            {
                Prop("Wall Torch", PlacementKind.Light, DungeonPrimitive.Torch, PropPlacement.WallAdjacent, ZoneMask.Built | ZoneMask.Ruins,
                    new IntRange(1, 1), 1.8f, 6f, 1f, height: 1.9f, color: torch, range: torchRange),
                Prop("Glow Crystal", PlacementKind.Light, DungeonPrimitive.Crystal, PropPlacement.WallAdjacent, ZoneMask.Cavern,
                    new IntRange(0, 1), 1.4f, 7f, 0.9f, color: crystal, range: torchRange * 0.8f),
                Prop("Brazier", PlacementKind.Light, DungeonPrimitive.Brazier, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(2, 4), 0f, 4f, 1f, roles: guarded, color: torch, range: torchRange),
                Prop("Barrel", PlacementKind.Decoration, DungeonPrimitive.Barrel, PropPlacement.Corner, ZoneMask.Built,
                    new IntRange(0, 2), 0f, 1.5f, 0.55f),
                Prop("Crate", PlacementKind.Decoration, DungeonPrimitive.Crate, PropPlacement.WallAdjacent, ZoneMask.Built | ZoneMask.Ruins,
                    new IntRange(0, 2), 0.3f, 2f, 0.5f),
                Prop("Bones", PlacementKind.Decoration, DungeonPrimitive.Bones, PropPlacement.Anywhere, ZoneMask.Cavern | ZoneMask.Ruins,
                    new IntRange(0, 1), 0.7f, 4f, 0.6f),
                Prop("Mushrooms", PlacementKind.Decoration, DungeonPrimitive.Mushroom, PropPlacement.WallAdjacent, ZoneMask.Cavern,
                    new IntRange(0, 2), 1.2f, 3f, 0.7f),
                Prop("Rubble", PlacementKind.Decoration, DungeonPrimitive.Rubble, PropPlacement.Transition, ZoneMask.All,
                    new IntRange(1, 3), 0f, 1.2f, 1f),
                Prop("Altar", PlacementKind.PointOfInterest, DungeonPrimitive.Altar, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 1), 0f, 2f, 1f, roles: new[] { AreaRole.Shrine }),
                Prop("Fountain", PlacementKind.PointOfInterest, DungeonPrimitive.Fountain, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 1), 0f, 2f, 1f, roles: new[] { AreaRole.Rest }),
                Prop("Bookshelf", PlacementKind.Interactable, DungeonPrimitive.Bookshelf, PropPlacement.WallAdjacent, ZoneMask.Built,
                    new IntRange(1, 3), 0f, 2f, 0.8f, roles: new[] { AreaRole.Secret, AreaRole.Treasure, AreaRole.Shrine }),
                Prop("Spike Trap", PlacementKind.Hazard, DungeonPrimitive.SpikeTrap, PropPlacement.Corridor, ZoneMask.Built | ZoneMask.Ruins,
                    new IntRange(1, 1), 0f, 6f, 0.2f, progress: new FloatRange(0.15f, 1f)),
                Prop("Pillar", PlacementKind.Decoration, DungeonPrimitive.Pillar, PropPlacement.Corner, ZoneMask.Built,
                    new IntRange(4, 4), 0f, 2f, 0.35f, roles: new[] { AreaRole.Arena, AreaRole.Boss }),

                // ---- Special rooms
                Prop("Library Shelves", PlacementKind.Decoration, DungeonPrimitive.Bookshelf, PropPlacement.WallAdjacent, ZoneMask.Built | ZoneMask.Ruins,
                    new IntRange(3, 5), 3f, 1.6f, 1f, roles: new[] { AreaRole.Library }),
                Prop("Reading Table", PlacementKind.Decoration, DungeonPrimitive.Table, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 2), 0f, 3f, 1f, roles: new[] { AreaRole.Library, AreaRole.Laboratory, AreaRole.Prison }),
                Prop("Candles", PlacementKind.Light, DungeonPrimitive.Candles, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 3), 0.5f, 2.5f, 1f, roles: new[] { AreaRole.Library, AreaRole.Crypt, AreaRole.Shrine }, color: new Color(1f, 0.78f, 0.45f), range: 4.5f),
                Prop("Weapon Rack", PlacementKind.Decoration, DungeonPrimitive.WeaponRack, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(2, 4), 1f, 2f, 1f, roles: new[] { AreaRole.Armory }),
                Prop("Armor Stand", PlacementKind.Decoration, DungeonPrimitive.ArmorStand, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 3), 0f, 2.2f, 1f, roles: new[] { AreaRole.Armory, AreaRole.Throne }),
                Prop("Supply Crates", PlacementKind.Decoration, DungeonPrimitive.Crate, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(1, 3), 0f, 1.5f, 1f, roles: new[] { AreaRole.Armory }),
                Prop("Cell Cage", PlacementKind.Decoration, DungeonPrimitive.Cage, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(2, 4), 1f, 2.6f, 1f, roles: new[] { AreaRole.Prison }),
                Prop("Remains", PlacementKind.Decoration, DungeonPrimitive.Bones, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(1, 3), 0f, 2f, 1f, roles: new[] { AreaRole.Prison, AreaRole.Crypt, AreaRole.TrapRoom }),
                Prop("Sarcophagus", PlacementKind.Decoration, DungeonPrimitive.Sarcophagus, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 2.5f, 2.6f, 1f, roles: new[] { AreaRole.Crypt }),
                Prop("Cobwebs", PlacementKind.Decoration, DungeonPrimitive.Cobweb, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(1, 3), 0f, 2f, 0.8f, roles: new[] { AreaRole.Crypt, AreaRole.Prison, AreaRole.Library }, height: 2.2f),
                Prop("Alchemy Table", PlacementKind.Decoration, DungeonPrimitive.AlchemyTable, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 3), 0.5f, 2.2f, 1f, roles: new[] { AreaRole.Laboratory }),
                Prop("Cauldron", PlacementKind.Light, DungeonPrimitive.Cauldron, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 1), 0f, 3f, 1f, roles: new[] { AreaRole.Laboratory }, color: new Color(0.45f, 1f, 0.35f), range: 6f),
                Prop("Overgrowth", PlacementKind.Decoration, DungeonPrimitive.Vines, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(2, 5), 2f, 1.5f, 1f, roles: new[] { AreaRole.Garden }),
                Prop("Garden Mushrooms", PlacementKind.Decoration, DungeonPrimitive.Mushroom, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 1f, 2f, 1f, roles: new[] { AreaRole.Garden }),
                Prop("Healing Herbs", PlacementKind.Interactable, DungeonPrimitive.HerbPatch, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(1, 2), 0f, 3f, 1f, roles: new[] { AreaRole.Garden }),
                Prop("Throne", PlacementKind.PointOfInterest, DungeonPrimitive.Throne, PropPlacement.BackWall, ZoneMask.All,
                    new IntRange(1, 1), 0f, 4f, 1f, roles: new[] { AreaRole.Throne }),
                Prop("Banners", PlacementKind.Decoration, DungeonPrimitive.Banner, PropPlacement.WallAdjacent, ZoneMask.Built | ZoneMask.Ruins,
                    new IntRange(2, 4), 0f, 2.5f, 1f, roles: new[] { AreaRole.Throne, AreaRole.Boss, AreaRole.Armory }),
                Prop("Statue", PlacementKind.Decoration, DungeonPrimitive.Statue, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(2, 4), 0f, 3f, 1f, roles: new[] { AreaRole.Throne, AreaRole.Shrine, AreaRole.Puzzle, AreaRole.Vault }),
                Prop("Treasure Pedestal", PlacementKind.Decoration, DungeonPrimitive.Pedestal, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 2), 0f, 2.5f, 1f, roles: new[] { AreaRole.Vault }),

                // ---- Trap gauntlets (and a few traps in corridors)
                Prop("Spike Floor", PlacementKind.Hazard, DungeonPrimitive.SpikeTrap, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 2), 7f, 1.6f, 1f, roles: new[] { AreaRole.TrapRoom }),
                Prop("Fire Jet", PlacementKind.Hazard, DungeonPrimitive.FireTrap, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(1, 1), 5f, 2.2f, 1f, roles: new[] { AreaRole.TrapRoom }),
                Prop("Blade Pendulum", PlacementKind.Hazard, DungeonPrimitive.BladeTrap, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 2), 0f, 4f, 1f, roles: new[] { AreaRole.TrapRoom }),
                Prop("Dart Wall", PlacementKind.Hazard, DungeonPrimitive.DartTrap, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 3), 0f, 3f, 1f, roles: new[] { AreaRole.TrapRoom }),
                Prop("Corridor Darts", PlacementKind.Hazard, DungeonPrimitive.DartTrap, PropPlacement.Corridor, ZoneMask.Built | ZoneMask.Ruins,
                    new IntRange(1, 1), 0f, 8f, 0.12f, progress: new FloatRange(0.3f, 1f)),

                // ---- Floor modifiers
                Prop("Lava Pool", PlacementKind.Hazard, DungeonPrimitive.LavaPool, PropPlacement.OffPath, ZoneMask.Cavern | ZoneMask.Ruins,
                    new IntRange(0, 1), 1.6f, 5f, 0.9f, modifiers: FloorModifierMask.Molten, color: new Color(1f, 0.45f, 0.1f), range: 7f),
                Prop("Spore Vent", PlacementKind.Hazard, DungeonPrimitive.SporeVent, PropPlacement.OffPath, ZoneMask.All,
                    new IntRange(0, 1), 0.8f, 6f, 0.8f, modifiers: FloorModifierMask.Overgrown),
                Prop("Roots", PlacementKind.Decoration, DungeonPrimitive.Vines, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(0, 2), 2.5f, 1.5f, 0.9f, modifiers: FloorModifierMask.Overgrown),
                Prop("Glowcaps", PlacementKind.Light, DungeonPrimitive.Mushroom, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(0, 2), 1.5f, 3f, 0.9f, modifiers: FloorModifierMask.Overgrown, color: new Color(0.3f, 0.9f, 0.6f), range: 4f),
                Prop("Ice Crystals", PlacementKind.Light, DungeonPrimitive.IceSpikes, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(0, 2), 1.8f, 3f, 0.9f, modifiers: FloorModifierMask.Frozen, color: new Color(0.6f, 0.85f, 1f), range: 5f),
                Prop("Dark Cobwebs", PlacementKind.Decoration, DungeonPrimitive.Cobweb, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(0, 2), 0f, 2f, 0.6f, modifiers: FloorModifierMask.Darkness, height: 2.2f),
                Prop("Giant Roots", PlacementKind.Decoration, DungeonPrimitive.GiantRoot, PropPlacement.OffPath, ZoneMask.All,
                    new IntRange(0, 1), 0.8f, 6f, 0.7f, modifiers: FloorModifierMask.Overgrown),

                // ---- Newer rooms
                Prop("Egg Sacs", PlacementKind.Decoration, DungeonPrimitive.EggSac, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 1f, 2f, 1f, roles: new[] { AreaRole.Nest }),
                Prop("Gnawed Bones", PlacementKind.Decoration, DungeonPrimitive.Bones, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(1, 3), 0f, 2f, 1f, roles: new[] { AreaRole.Nest, AreaRole.GasChamber }),
                Prop("Blood Altar", PlacementKind.Interactable, DungeonPrimitive.BloodAltar, PropPlacement.BackWall, ZoneMask.All,
                    new IntRange(1, 1), 0f, 4f, 1f, roles: new[] { AreaRole.Gambling }),
                Prop("Cursed Altar", PlacementKind.Interactable, DungeonPrimitive.CursedAltar, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 1), 0f, 4f, 0.7f, roles: new[] { AreaRole.Gambling }),
                Prop("Ritual Candles", PlacementKind.Light, DungeonPrimitive.Candles, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 0f, 2f, 1f, roles: new[] { AreaRole.Gambling }, color: new Color(1f, 0.35f, 0.3f), range: 4.5f),
                Prop("Long Table", PlacementKind.Decoration, DungeonPrimitive.LongTable, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 2), 0f, 3.5f, 1f, roles: new[] { AreaRole.Kitchen }),
                Prop("Benches", PlacementKind.Decoration, DungeonPrimitive.Bench, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 3), 0f, 2.2f, 1f, roles: new[] { AreaRole.Kitchen }),
                Prop("Stove", PlacementKind.Light, DungeonPrimitive.Stove, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 1), 0f, 3f, 1f, roles: new[] { AreaRole.Kitchen }, color: new Color(1f, 0.55f, 0.2f), range: 7f),
                Prop("Pots of Stew", PlacementKind.Interactable, DungeonPrimitive.Pots, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 2), 0f, 2f, 1f, roles: new[] { AreaRole.Kitchen }),
                Prop("Kitchen Barrels", PlacementKind.Decoration, DungeonPrimitive.Barrel, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(1, 3), 0f, 1.2f, 1f, roles: new[] { AreaRole.Kitchen }),
                Prop("Paintings", PlacementKind.Decoration, DungeonPrimitive.Painting, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(4, 8), 3f, 2.2f, 1f, roles: new[] { AreaRole.Gallery }),
                Prop("Gallery Statues", PlacementKind.Decoration, DungeonPrimitive.Statue, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(1, 3), 0f, 3f, 1f, roles: new[] { AreaRole.Gallery }),
                Prop("Viewing Bench", PlacementKind.Decoration, DungeonPrimitive.Bench, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 1), 0f, 3f, 0.8f, roles: new[] { AreaRole.Gallery }),
                Prop("Bunks", PlacementKind.Decoration, DungeonPrimitive.Bed, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(3, 6), 4f, 1.4f, 1f, roles: new[] { AreaRole.Barracks }),
                Prop("Barracks Rack", PlacementKind.Decoration, DungeonPrimitive.WeaponRack, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 2), 0f, 2f, 1f, roles: new[] { AreaRole.Barracks }),
                Prop("Spectators", PlacementKind.Decoration, DungeonPrimitive.Spectators, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(3, 6), 2f, 3.4f, 1f, roles: new[] { AreaRole.Colosseum }),
                Prop("Arena Braziers", PlacementKind.Light, DungeonPrimitive.Brazier, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(2, 4), 0f, 3f, 1f, roles: new[] { AreaRole.Colosseum }, color: new Color(1f, 0.6f, 0.25f), range: 10f),
                Prop("Arena Banners", PlacementKind.Decoration, DungeonPrimitive.Banner, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(2, 4), 0f, 3f, 1f, roles: new[] { AreaRole.Colosseum }),
                Prop("Planters", PlacementKind.Decoration, DungeonPrimitive.Planter, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(2, 4), 2f, 2.2f, 1f, roles: new[] { AreaRole.Greenhouse }),
                Prop("Rare Herbs", PlacementKind.Interactable, DungeonPrimitive.RareHerb, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(1, 2), 0f, 3f, 1f, roles: new[] { AreaRole.Greenhouse }),
                Prop("Poisonous Plants", PlacementKind.Hazard, DungeonPrimitive.PoisonPlant, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 2f, 2f, 1f, roles: new[] { AreaRole.Greenhouse }),
                Prop("Greenhouse Vines", PlacementKind.Decoration, DungeonPrimitive.Vines, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 3), 0f, 1.5f, 1f, roles: new[] { AreaRole.Greenhouse }),
                Prop("Wine Racks", PlacementKind.Decoration, DungeonPrimitive.WineRack, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(2, 4), 2f, 1.8f, 1f, roles: new[] { AreaRole.WineCellar }),
                Prop("Wine Barrels", PlacementKind.Decoration, DungeonPrimitive.WineBarrel, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(3, 6), 3f, 1.4f, 1f, roles: new[] { AreaRole.WineCellar }),
                Prop("Cellar Cobwebs", PlacementKind.Decoration, DungeonPrimitive.Cobweb, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(1, 2), 0f, 2f, 0.8f, roles: new[] { AreaRole.WineCellar }, height: 2.2f),
                Prop("Map Table", PlacementKind.Interactable, DungeonPrimitive.MapTable, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 1), 0f, 4f, 1f, roles: new[] { AreaRole.MapRoom }),
                Prop("Chart Shelves", PlacementKind.Decoration, DungeonPrimitive.Bookshelf, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 2), 0f, 2f, 1f, roles: new[] { AreaRole.MapRoom }),
                Prop("Map Room Candles", PlacementKind.Light, DungeonPrimitive.Candles, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(1, 2), 0f, 2f, 1f, roles: new[] { AreaRole.MapRoom }, color: new Color(1f, 0.8f, 0.5f), range: 4.5f),
                Prop("Gas Vents", PlacementKind.Decoration, DungeonPrimitive.GasVent, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 2f, 2.5f, 1f, roles: new[] { AreaRole.GasChamber }),

                // ---- Corridor traps and caves
                Prop("Swinging Log", PlacementKind.Hazard, DungeonPrimitive.LogTrap, PropPlacement.Corridor, ZoneMask.All,
                    new IntRange(1, 1), 0f, 10f, 0.1f, progress: new FloatRange(0.25f, 1f)),
                Prop("Stalactites", PlacementKind.Decoration, DungeonPrimitive.Stalactite, PropPlacement.Anywhere, ZoneMask.Cavern,
                    new IntRange(0, 3), 1f, 3f, 0.5f),

                // ---- Floor styles (by the areas' tags)
                Prop("Street Lamps", PlacementKind.Light, DungeonPrimitive.LampPost, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 2), 3f, 6f, 1f, tag: "Street", color: new Color(1f, 0.8f, 0.5f), range: 10f),
                Prop("Carts", PlacementKind.Decoration, DungeonPrimitive.Cart, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(0, 1), 0.5f, 8f, 0.5f, tag: "Street"),
                Prop("Plaza Well", PlacementKind.PointOfInterest, DungeonPrimitive.Well, PropPlacement.Center, ZoneMask.All,
                    new IntRange(1, 1), 0f, 5f, 0.6f, tag: "Plaza"),
                Prop("Market Stalls", PlacementKind.Decoration, DungeonPrimitive.MarketStall, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 2f, 3.5f, 0.9f, tag: "Plaza"),
                Prop("Plaza Lamps", PlacementKind.Light, DungeonPrimitive.LampPost, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(2, 4), 0f, 5f, 1f, tag: "Plaza", color: new Color(1f, 0.8f, 0.5f), range: 10f),
                Prop("Ruined Walls", PlacementKind.Decoration, DungeonPrimitive.Debris, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(2, 4), 2f, 2.5f, 1f, tag: "Ruin"),
                Prop("Hoard Gold", PlacementKind.Decoration, DungeonPrimitive.GoldPile, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(6, 12), 1f, 2.2f, 1f, tag: "Den"),
                Prop("Dragon Bones", PlacementKind.Decoration, DungeonPrimitive.DragonBones, PropPlacement.OffPath, ZoneMask.All,
                    new IntRange(1, 2), 0f, 6f, 1f, tag: "Den"),
                Prop("Den Stalactites", PlacementKind.Decoration, DungeonPrimitive.Stalactite, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(3, 6), 1f, 4f, 1f, tag: "Den"),
                Prop("Hive Egg Sacs", PlacementKind.Decoration, DungeonPrimitive.EggSac, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(0, 2), 1f, 2.5f, 0.7f, tag: "Hive"),
                Prop("Hive Fungus", PlacementKind.Light, DungeonPrimitive.Fungus, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(0, 2), 1f, 3f, 0.8f, tag: "Hive", color: new Color(0.6f, 0.4f, 0.9f), range: 5f),
                Prop("Brood Eggs", PlacementKind.Decoration, DungeonPrimitive.EggSac, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(3, 6), 2f, 2f, 1f, tag: "Brood"),
                Prop("Brood Fungus", PlacementKind.Light, DungeonPrimitive.Fungus, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(1, 3), 1f, 3f, 1f, tag: "Brood", color: new Color(0.4f, 0.9f, 0.6f), range: 6f),
                Prop("Star Motes", PlacementKind.Light, DungeonPrimitive.StarMote, PropPlacement.Anywhere, ZoneMask.All,
                    new IntRange(1, 3), 1f, 4f, 1f, tag: "Platform", color: new Color(0.7f, 0.8f, 1f), range: 7f),
                Prop("Island Crystals", PlacementKind.Light, DungeonPrimitive.Crystal, PropPlacement.WallAdjacent, ZoneMask.All,
                    new IntRange(0, 2), 1f, 4f, 0.6f, tag: "Island", color: new Color(0.5f, 0.75f, 1f), range: 7f),
            };
            for (int i = 0; i < list.Count; i++)
                list[i].Index = i;
            return list;
        }

        /// <summary>The roles whose rooms are furnished by props (special rooms).</summary>
        public static readonly AreaRole[] FurnishedRoles =
        {
            AreaRole.Library, AreaRole.Armory, AreaRole.Prison, AreaRole.Crypt, AreaRole.Laboratory, AreaRole.Garden,
            AreaRole.Throne, AreaRole.TrapRoom, AreaRole.Vault, AreaRole.Shrine, AreaRole.Rest,
            AreaRole.Nest, AreaRole.Gambling, AreaRole.Kitchen, AreaRole.Gallery, AreaRole.Barracks, AreaRole.Colosseum,
            AreaRole.Greenhouse, AreaRole.WineCellar, AreaRole.MapRoom, AreaRole.GasChamber,
        };

        /// <summary>The area tags of the floor styles that props dress (streets, plazas, ruins, the den, the hive, islands).</summary>
        public static readonly string[] FurnishedTags = { "Street", "Plaza", "Ruin", "Den", "Hive", "Brood", "Platform", "Island" };

        /// <summary>
        /// Adds the built-in props for every special room, floor style tag and floor modifier that <paramref name="props"/>
        /// (a prop table's entries) has nothing for, and the swinging logs when it has none, so new rooms are never empty
        /// with an older table.
        /// </summary>
        public static void FillMissing(List<PropInfo> props, DungeonTheme theme)
        {
            List<PropInfo> builtIn = Props(theme);
            var have = new HashSet<string>();
            foreach (PropInfo e in props)
                have.Add(e.Name);

            void AddAll(System.Predicate<PropInfo> which)
            {
                foreach (PropInfo e in builtIn)
                {
                    if (!which(e) || have.Contains(e.Name))
                        continue;
                    e.Index = props.Count;
                    props.Add(e);
                    have.Add(e.Name);
                }
            }

            foreach (AreaRole role in FurnishedRoles)
            {
                bool covered = props.Exists(e => System.Array.IndexOf(e.Roles, role) >= 0);
                if (!covered)
                    AddAll(e => System.Array.IndexOf(e.Roles, role) >= 0);
            }
            foreach (string tag in FurnishedTags)
            {
                if (!props.Exists(e => e.AreaTag == tag))
                    AddAll(e => e.AreaTag == tag);
            }
            if (!props.Exists(e => e.Placeholder == DungeonPrimitive.LogTrap))
                AddAll(e => e.Placeholder == DungeonPrimitive.LogTrap);
            foreach (FloorModifierMask m in new[] { FloorModifierMask.Molten, FloorModifierMask.Overgrown, FloorModifierMask.Frozen, FloorModifierMask.Darkness })
            {
                bool covered = props.Exists(e => e.Modifiers != FloorModifierMask.Any && (e.Modifiers & m) != 0);
                if (!covered)
                    AddAll(e => e.Modifiers != FloorModifierMask.Any && (e.Modifiers & m) != 0);
            }
        }

        private static PropInfo Prop(string name, PlacementKind kind, DungeonPrimitive primitive, PropPlacement placement, ZoneMask styles,
            IntRange perArea, float perHundred, float spacing, float chance, AreaRole[] roles = null, float height = 0f,
            Color? color = null, float range = 8f, FloatRange? progress = null, FloorModifierMask modifiers = FloorModifierMask.Any, string tag = "")
        {
            return new PropInfo
            {
                Name = name,
                Kind = kind,
                Placeholder = primitive,
                Placement = placement,
                Styles = styles,
                PerArea = perArea,
                PerHundredCells = perHundred,
                Spacing = spacing,
                Chance = chance,
                Roles = roles ?? new AreaRole[0],
                AreaTag = tag,
                MainPath = MainPathFilter.Any,
                Modifiers = modifiers,
                Progress = progress ?? new FloatRange(0f, 1f),
                MaxFloor = -1,
                HeightOffset = height,
                Scale = new FloatRange(0.9f, 1.1f),
                LightColor = color ?? Color.white,
                LightRange = range,
            };
        }

        private static List<EncounterInfo> WithIndexes(this List<EncounterInfo> list)
        {
            for (int i = 0; i < list.Count; i++)
                list[i].Index = i;
            return list;
        }
    }
}
