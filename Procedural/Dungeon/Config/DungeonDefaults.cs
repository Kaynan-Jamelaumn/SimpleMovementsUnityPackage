using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Built-in population used when a profile has no tables: primitive chests, torches, crystals, barrels, bones,
    /// altars, fountains, traps and placeholder mobs. Enough to see and test a dungeon without any art.
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
            var list = new List<PropInfo>
            {
                Prop("Wall Torch", PlacementKind.Light, DungeonPrimitive.Torch, PropPlacement.WallAdjacent, ZoneMask.Built | ZoneMask.Ruins,
                    new IntRange(1, 1), 1.8f, 6f, 1f, height: 1.9f, color: torch, range: torchRange),
                Prop("Glow Crystal", PlacementKind.Light, DungeonPrimitive.Crystal, PropPlacement.WallAdjacent, ZoneMask.Cavern,
                    new IntRange(0, 1), 1.4f, 7f, 0.9f, color: crystal, range: torchRange * 0.8f),
                Prop("Brazier", PlacementKind.Light, DungeonPrimitive.Brazier, PropPlacement.Corner, ZoneMask.All,
                    new IntRange(2, 4), 0f, 4f, 1f, roles: new[] { AreaRole.Arena, AreaRole.Boss, AreaRole.Entrance }, color: torch, range: torchRange),
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
            };
            for (int i = 0; i < list.Count; i++)
                list[i].Index = i;
            return list;
        }

        private static PropInfo Prop(string name, PlacementKind kind, DungeonPrimitive primitive, PropPlacement placement, ZoneMask styles,
            IntRange perArea, float perHundred, float spacing, float chance, AreaRole[] roles = null, float height = 0f,
            Color? color = null, float range = 8f, FloatRange? progress = null)
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
                AreaTag = "",
                MainPath = MainPathFilter.Any,
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
