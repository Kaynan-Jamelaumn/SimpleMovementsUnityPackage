#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using ProceduralDungeon;
using UnityEngine;

namespace HubWorld.Tests
{
    /// <summary>
    /// Edit Mode tests for the world's spawn rules: the zone filter of portal and mob types (and the zone map feeding
    /// it), the zone-aware portal type pick, and the arrival points that linked portals use inside a dungeon.
    /// </summary>
    public class SpawnRuleTests
    {
        private static SpawnZone Zone(int index, string name, bool pvp) =>
            new SpawnZone { Known = true, Index = index, Name = name, Pvp = pvp };

        [TearDown]
        public void ClearHooks() => WorldSpawnHooks.ZoneAt = null;

        // ------------------------------------------------------------------ the zone filter

        [Test]
        public void ZoneFilterOffOrWithoutZonesAllowsEverything()
        {
            var off = new ZoneFilter { enabled = false, zoneNames = new[] { "Safe Zone" } };
            Assert.IsTrue(off.Allows(Zone(3, "The Abyss", true)));
            var on = new ZoneFilter { enabled = true, zoneNames = new[] { "Safe Zone" } };
            Assert.IsTrue(on.Allows(SpawnZone.Unknown), "a world without zones never filters anything out");
        }

        [Test]
        public void ZoneFilterMatchesNamesIgnoringCaseAndSpaces()
        {
            var f = new ZoneFilter { enabled = true, zoneNames = new[] { "  safe zone ", "", "FRONTIER" } };
            Assert.IsTrue(f.Allows(Zone(0, "Safe Zone", false)));
            Assert.IsTrue(f.Allows(Zone(1, "Frontier", true)));
            Assert.IsFalse(f.Allows(Zone(2, "Dangerous Zone", true)));
            var blanks = new ZoneFilter { enabled = true, zoneNames = new[] { "", " " } };
            Assert.IsTrue(blanks.Allows(Zone(2, "Dangerous Zone", true)), "blank names are no names");
        }

        [Test]
        public void ZoneFilterRingRangeAndPvpRule()
        {
            var far = new ZoneFilter { enabled = true, minRing = 2 };
            Assert.IsFalse(far.Allows(Zone(1, "Frontier", true)));
            Assert.IsTrue(far.Allows(Zone(2, "Dangerous Zone", true)));
            Assert.IsTrue(far.Allows(Zone(7, "The Abyss", true)), "no farthest ring = no limit");
            Assert.IsFalse(far.Allows(Zone(-1, "City", false)), "the city is ring -1");

            var band = new ZoneFilter { enabled = true, minRing = 0, maxRing = 1 };
            Assert.IsTrue(band.Allows(Zone(1, "Frontier", true)));
            Assert.IsFalse(band.Allows(Zone(2, "Dangerous Zone", true)));

            var peaceful = new ZoneFilter { enabled = true, pvp = ZonePvpRule.OnlyNonPvp };
            Assert.IsTrue(peaceful.Allows(Zone(0, "Safe Zone", false)));
            Assert.IsFalse(peaceful.Allows(Zone(1, "Frontier", true)));
            var arena = new ZoneFilter { enabled = true, pvp = ZonePvpRule.OnlyPvp };
            Assert.IsFalse(arena.Allows(Zone(0, "Safe Zone", false)));
            Assert.IsTrue(arena.Allows(Zone(1, "Frontier", true)));
        }

        [Test]
        public void ZoneFilterValidateKeepsTheRangeSane()
        {
            var f = new ZoneFilter { enabled = true, minRing = 3, maxRing = 1 };
            f.Validate();
            Assert.AreEqual(3, f.maxRing, "the farthest ring is never nearer than the nearest one");
            f.maxRing = -7;
            f.Validate();
            Assert.AreEqual(-1, f.maxRing);
            StringAssert.Contains("ring 3", f.Describe());
        }

        [Test]
        public void ZoneMapRingsFeedTheFilter()
        {
            var map = new ZoneMap(Vector2.zero, 100f, new[]
            {
                new ZoneBand("Safe Zone", 600f, false, 1f, 1f, 1f, 1f, 1f, Color.green),
                new ZoneBand("Frontier", 1500f, true, 1f, 2f, 1f, 1.4f, 1f, Color.yellow),
            });
            WorldSpawnHooks.ZoneAt = p =>
            {
                ZoneInfo z = map.At(p);
                return new SpawnZone { Known = true, Index = z.index, Name = z.name, Pvp = z.pvp };
            };
            var safeOnly = new ZoneFilter { enabled = true, zoneNames = new[] { "Safe Zone" } };
            Assert.IsTrue(safeOnly.Allows(WorldSpawnHooks.ZoneOf(new Vector3(300f, 0f, 0f))));
            Assert.IsFalse(safeOnly.Allows(WorldSpawnHooks.ZoneOf(new Vector3(1000f, 0f, 0f))));
            Assert.IsFalse(safeOnly.Allows(WorldSpawnHooks.ZoneOf(new Vector3(50f, 0f, 0f))), "inside the city walls");
        }

        // ------------------------------------------------------------------ portal and mob types by zone

        [Test]
        public void PortalTypesOnlyAppearInTheirZones()
        {
            var safePrefab = new GameObject("SafePortal");
            var raidPrefab = new GameObject("RaidPortal");
            try
            {
                var settings = new PortalSettings
                {
                    regionSize = 600f,
                    maxNumberOfPortals = 1,
                    spawnChance = 1f,
                    useWeightedSelection = false,
                    prefabs = new List<SpawnablePortal>
                    {
                        new SpawnablePortal { prefab = safePrefab, zones = new ZoneFilter { enabled = true, maxRing = 0 } },
                        new SpawnablePortal { prefab = raidPrefab, zones = new ZoneFilter { enabled = true, minRing = 2 } },
                    },
                };
                // West of x = 3000 is the Safe Zone (ring 0), east of it the Dangerous Zone (ring 2).
                WorldSpawnHooks.ZoneAt = p => p.x < 3000f ? Zone(0, "Safe Zone", false) : Zone(2, "Dangerous Zone", true);
                var sites = new List<PortalSitePlanner.Site>();
                PortalSitePlanner.SitesInArea(settings, 1234, new Vector2(0f, 0f), new Vector2(6000f, 6000f), sites);
                Assert.Greater(sites.Count, 20);
                foreach (PortalSitePlanner.Site site in sites)
                    Assert.AreEqual(site.Point.x < 3000f ? 0 : 1, site.Type, $"site at {site.Point}");

                // Without zones both types appear on both sides again.
                WorldSpawnHooks.ZoneAt = null;
                sites.Clear();
                PortalSitePlanner.SitesInArea(settings, 1234, new Vector2(0f, 0f), new Vector2(6000f, 6000f), sites);
                bool raidInWest = sites.Exists(s => s.Point.x < 3000f && s.Type == 1);
                Assert.IsTrue(raidInWest);
            }
            finally
            {
                Object.DestroyImmediate(safePrefab);
                Object.DestroyImmediate(raidPrefab);
            }
        }

        [Test]
        public void MobWeightIsZeroOutsideTheMobsZones()
        {
            var frontierMob = new SpawnableMob { zones = new ZoneFilter { enabled = true, zoneNames = new[] { "Frontier" } } };
            WorldSpawnHooks.ZoneAt = p => p.x < 600f ? Zone(0, "Safe Zone", false) : Zone(1, "Frontier", true);
            Assert.AreEqual(0f, WorldSpawnHooks.ApplyMobWeight(frontierMob, new Vector3(100f, 0f, 0f), 2f));
            Assert.AreEqual(2f, WorldSpawnHooks.ApplyMobWeight(frontierMob, new Vector3(900f, 0f, 0f), 2f));
            var anywhere = new SpawnableMob();
            Assert.AreEqual(2f, WorldSpawnHooks.ApplyMobWeight(anywhere, new Vector3(100f, 0f, 0f), 2f));
        }

        // ------------------------------------------------------------------ arrival points of linked portals

        /// <summary>A walkable strip of <paramref name="width"/> x <paramref name="height"/> cells, one room.</summary>
        private static FloorLayout Strip(int width, int height)
        {
            var floor = new FloorLayout { Index = 0, Grid = new TileGrid(width, height) };
            Area room = floor.AddArea(AreaKind.Room, default(ZoneStyle));
            for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    int c = floor.Grid.Index(x, y);
                    floor.Grid.SetFloor(c, room.Id, false);
                    room.Cells.Add(c);
                }
            return floor;
        }

        [Test]
        public void SecondArrivalIsAtTheFarEndOfTheFloor()
        {
            FloorLayout floor = Strip(40, 9);
            int start = floor.Grid.Index(1, 4);
            List<int> cells = DungeonEntrances.ArrivalCells(floor, start, 3);
            Assert.AreEqual(3, cells.Count);
            Assert.AreEqual(start, cells[0], "entrance 0 is the dungeon's own spawn");
            Assert.GreaterOrEqual(floor.Grid.X(cells[1]), 35, "entrance 1 is as far as the floor allows");
            int[] fromStart = floor.Flood(start);
            int[] fromSecond = floor.Flood(cells[1]);
            Assert.GreaterOrEqual(fromStart[cells[2]], DungeonEntrances.MinSeparation);
            Assert.GreaterOrEqual(fromSecond[cells[2]], DungeonEntrances.MinSeparation);
        }

        [Test]
        public void ArrivalsAvoidRoomsWithTheirOwnRules()
        {
            FloorLayout floor = Strip(40, 9);
            // The far end becomes a boss room.
            Area boss = floor.AddArea(AreaKind.Room, default(ZoneStyle));
            boss.Role = AreaRole.Boss;
            for (int y = 1; y < 8; y++)
                for (int x = 30; x < 39; x++)
                    floor.Grid.Area[floor.Grid.Index(x, y)] = boss.Id;
            List<int> cells = DungeonEntrances.ArrivalCells(floor, floor.Grid.Index(1, 4), 2);
            Assert.AreEqual(2, cells.Count);
            Assert.Less(floor.Grid.X(cells[1]), 30, "never inside the boss room");
        }

        [Test]
        public void TinyFloorHasOnlyTheSpawn()
        {
            FloorLayout floor = Strip(4, 4); // 2 x 2 walkable cells
            List<int> cells = DungeonEntrances.ArrivalCells(floor, floor.Grid.Index(1, 1), 3);
            Assert.AreEqual(1, cells.Count, "points closer than the minimum separation are not worth a second entrance");
        }
    }
}
#endif
