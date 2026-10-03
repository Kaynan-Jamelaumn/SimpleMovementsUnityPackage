#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ProceduralDungeon.Tests
{
    /// <summary>
    /// Edit Mode tests (Window > General > Test Runner) checking the generator's guarantees over many seeds and every
    /// style. Only compiled when the Unity Test Framework is present.
    /// </summary>
    public class DungeonGenerationTests
    {
        private static CompiledProfile Profile() => CompiledProfile.Compile(null);

        [Test]
        public void EveryStyleProducesTraversableDungeons()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            foreach (FloorStyle style in System.Enum.GetValues(typeof(FloorStyle)))
            {
                for (int s = 1; s <= 8; s++)
                {
                    var request = new DungeonRequest { seed = s * 7919 + (int)style, overrideStyle = true, style = style, size = (SizeClass)(s % 3) };
                    DungeonLayout layout = pipeline.Generate(request, p);
                    AssertTraversable(layout, $"{style} seed {request.seed}");
                }
            }
        }

        [Test]
        public void MixedDungeonsHaveEntranceExitBossAndSpawn()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            for (int s = 1; s <= 15; s++)
            {
                DungeonLayout layout = pipeline.Generate(new DungeonRequest { seed = 100 + s }, p);
                Assert.IsTrue(layout.PlayerSpawn.Valid, "player spawn");
                Assert.IsTrue(layout.EntrancePortal.Valid, "entrance portal");
                Assert.IsTrue(layout.ExitPortal.Valid, "exit portal");
                Assert.AreEqual(0, layout.MainPath[0].Floor);
                Assert.AreEqual(layout.Floors.Count - 1, layout.MainPath[layout.MainPath.Count - 1].Floor);
                int bosses = 0;
                foreach (FloorLayout f in layout.Floors)
                    foreach (Area a in f.Areas)
                        if (a.Role == AreaRole.Boss)
                            bosses++;
                Assert.AreEqual(1, bosses, "one boss room");
            }
        }

        [Test]
        public void SameSeedSameDungeon()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            var request = new DungeonRequest { seed = 424242, size = SizeClass.Large };
            Assert.AreEqual(Fingerprint(pipeline.Generate(request, p)), Fingerprint(pipeline.Generate(request, p)));
        }

        [Test]
        public void MeshesAreBuilt()
        {
            CompiledProfile p = Profile();
            DungeonLayout layout = DungeonPipeline.CreateDefault().Generate(new DungeonRequest { seed = 5, floorCount = 2 }, p);
            List<FloorMeshData> meshes = DungeonMeshing.BuildAll(layout, p);
            Assert.AreEqual(layout.Floors.Count, meshes.Count);
            foreach (FloorMeshData m in meshes)
                Assert.Greater(m.Chunks.Count, 0);
            Assert.AreEqual(layout.Links.Count, meshes[1].Links.Count + meshes[0].Links.Count);
        }

        [Test]
        public void EveryDungeonTypeGenerates()
        {
            foreach (DungeonType type in System.Enum.GetValues(typeof(DungeonType)))
            {
                var profile = ScriptableObject.CreateInstance<DungeonProfile>();
                DungeonTypes.Apply(profile, type);
                CompiledProfile p = CompiledProfile.Compile(profile);
                DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
                for (int s = 1; s <= 4; s++)
                {
                    var request = new DungeonRequest { seed = s * 104729 + (int)type, size = (SizeClass)(s % 4) };
                    DungeonLayout layout = pipeline.Generate(request, p);
                    AssertTraversable(layout, $"{type} seed {request.seed}");
                    AssertLocksAreSafe(layout, $"{type} seed {request.seed}");
                }
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LocksNeverCutAnythingOffAndKeysAreReachable()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            for (int s = 1; s <= 30; s++)
                AssertLocksAreSafe(pipeline.Generate(new DungeonRequest { seed = 900 + s * 31, size = (SizeClass)(s % 4) }, p), $"seed {900 + s * 31}");
        }

        [Test]
        public void CeilingsAreTallAndFitBetweenFloors()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            float tallest = 0f;
            for (int s = 1; s <= 10; s++)
            {
                DungeonLayout layout = pipeline.Generate(new DungeonRequest { seed = 300 + s }, p);
                foreach (FloorLayout f in layout.Floors)
                    for (int i = 0; i < f.Grid.Count; i++)
                    {
                        if (!f.Grid.IsWalkable(i))
                            continue;
                        float head = f.Grid.CeilingHeight[i] - f.Grid.FloorHeight[i];
                        Assert.GreaterOrEqual(head, p.Ceilings.minHeadroom - 0.01f, "headroom");
                        Assert.LessOrEqual(f.Grid.CeilingHeight[i], p.MaxCeiling + 0.01f, "ceiling under the floor above");
                        tallest = Mathf.Max(tallest, head);
                    }
            }
            Assert.Greater(tallest, 8f, "the tallest rooms (boss halls, vaults) reach well above the old 4 m");
            Assert.GreaterOrEqual(p.FloorSpacing, p.MaxCeiling + p.Caves.floorHeightAmplitude, "floors make room for the ceilings");
        }

        [Test]
        public void AmbushMobsWaitInWaves()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            int checkedRooms = 0;
            for (int s = 1; s <= 40 && checkedRooms < 3; s++)
            {
                DungeonLayout layout = pipeline.Generate(new DungeonRequest { seed = 700 + s, size = SizeClass.Large }, p);
                foreach (FloorLayout f in layout.Floors)
                    foreach (Area a in f.Areas)
                    {
                        if (a.Role != AreaRole.Ambush)
                            continue;
                        bool controller = false;
                        foreach (Placement pl in layout.Placements)
                        {
                            if (pl.Floor != f.Index || pl.Area != a.Id)
                                continue;
                            if (pl.Kind == PlacementKind.Mob)
                                Assert.IsTrue(pl.Dormant && pl.Wave >= 1, "ambush mobs are hidden in waves");
                            controller |= pl.Kind == PlacementKind.RoomController && pl.Link == (int)RoomEventMode.Ambush;
                        }
                        Assert.IsTrue(controller, "an ambush room has its event");
                        checkedRooms++;
                    }
            }
            Assert.Greater(checkedRooms, 0, "some ambush rooms were generated");
        }

        [Test]
        public void FloorsNeverOverlap()
        {
            // Every type, so every style and chasm depth: wherever the floor below has open space, the floor above (its
            // ground or its chasm's bottom) stays above that space's ceiling. (Over solid rock a chasm may go deeper.)
            foreach (DungeonType type in System.Enum.GetValues(typeof(DungeonType)))
            {
                var profile = ScriptableObject.CreateInstance<DungeonProfile>();
                DungeonTypes.Apply(profile, type);
                CompiledProfile p = CompiledProfile.Compile(profile);
                DungeonLayout layout = DungeonPipeline.CreateDefault().Generate(new DungeonRequest { seed = 4242 + (int)type, floorCount = 3 }, p);
                for (int f = 0; f + 1 < layout.Floors.Count; f++)
                {
                    FloorLayout upper = layout.Floors[f], lower = layout.Floors[f + 1];
                    for (int i = 0; i < upper.Grid.Count; i++)
                    {
                        if (!(upper.Grid.IsWalkable(i) || upper.Grid.IsChasm(i)) || !lower.Grid.IsWalkable(i) || upper.Grid.Type[i] == CellType.Link)
                            continue;
                        float bottom = upper.Spec.BaseY + upper.Grid.FloorHeight[i], top = lower.Spec.BaseY + lower.Grid.CeilingHeight[i];
                        Assert.Greater(bottom, top, $"{type}: floor {f} reaches into floor {f + 1} at {upper.Grid.Coords(i)}");
                    }
                }
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TowerIsOneContinuousSpiral()
        {
            var profile = ScriptableObject.CreateInstance<DungeonProfile>();
            DungeonTypes.Apply(profile, DungeonType.Tower);
            CompiledProfile p = CompiledProfile.Compile(profile);
            for (int s = 1; s <= 4; s++)
            {
                DungeonLayout layout = DungeonPipeline.CreateDefault().Generate(new DungeonRequest { seed = 61 + s * 13, floorCount = 4 }, p);
                Assert.AreEqual(layout.Floors.Count - 1, layout.Links.Count, "one spiral flight per pair of floors");
                for (int k = 0; k < layout.Links.Count; k++)
                {
                    VerticalLink link = layout.Links[k];
                    Assert.AreEqual(LinkKind.Spiral, link.Kind);
                    Assert.AreEqual(k > 0 ? layout.Links[k - 1].Id : -1, link.Above, "each flight continues the one above");
                    if (k > 0)
                        Assert.AreEqual(layout.Links[k - 1].LowerLanding, link.UpperLanding, "one doorway per floor: the way down starts where the way up arrives");
                }
                AssertTraversable(layout, $"tower seed {61 + s * 13}");
            }
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void JumpsAndChasmsHoldTogether()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            foreach (FloorStyle style in new[] { FloorStyle.Islands, FloorStyle.Astral })
                for (int s = 1; s <= 5; s++)
                {
                    DungeonLayout layout = pipeline.Generate(new DungeonRequest { seed = 515 + s * 37, overrideStyle = true, style = style, floorCount = 2 }, p);
                    foreach (FloorLayout f in layout.Floors)
                    {
                        TileGrid g = f.Grid;
                        int chasm = 0;
                        for (int i = 0; i < g.Count; i++)
                            if (g.IsChasm(i) && !g.IsBridge(i))
                            {
                                chasm++;
                                Assert.Less(g.FloorHeight[i], -1f, $"{style}: the chasm has depth");
                            }
                        Assert.Greater(chasm, 0, $"{style}: floor {f.Index} has a chasm");
                    }
                    var pads = new Dictionary<int, int>();
                    foreach (Placement pl in layout.Placements)
                    {
                        if (pl.Kind == PlacementKind.Teleporter)
                            pads[pl.Link] = pads.TryGetValue(pl.Link, out int n) ? n + 1 : 1;
                        if (pl.Kind == PlacementKind.MovingPlatform)
                        {
                            Connection c = layout.Floors[pl.Floor].Connections[pl.Link];
                            Assert.AreEqual(ConnectionKind.Platform, c.Kind);
                            Assert.Greater(c.Track.Count, 0, "a platform has a track");
                            Assert.IsTrue(layout.Floors[pl.Floor].Grid.IsWalkable(c.DoorA) && layout.Floors[pl.Floor].Grid.IsWalkable(c.DoorB), "a platform's track ends on ledges");
                        }
                    }
                    foreach (var kv in pads)
                        Assert.AreEqual(2, kv.Value, $"{style}: teleporter pair {kv.Key} has two ends");
                    AssertTraversable(layout, $"{style} seed {515 + s * 37}");
                }
        }

        [Test]
        public void SpecialRoomsGetTheirMechanics()
        {
            CompiledProfile p = Profile();
            DungeonPipeline pipeline = DungeonPipeline.CreateDefault();
            int cellars = 0, gas = 0, altars = 0, pits = 0, sleepers = 0;
            for (int s = 1; s <= 60; s++)
            {
                DungeonLayout layout = pipeline.Generate(new DungeonRequest { seed = 2000 + s * 17, size = SizeClass.Large }, p);
                foreach (Placement pl in layout.Placements)
                {
                    FloorLayout f = layout.Floors[pl.Floor];
                    switch (pl.Kind)
                    {
                        case PlacementKind.Lever when pl.Entry == 0:
                            Assert.IsTrue(f.Grid.Has(pl.Link, CellFlags.Secret), "a cellar lever opens a secret door");
                            cellars++;
                            break;
                        case PlacementKind.Lever when pl.Entry == 1:
                            Assert.AreEqual(AreaRole.GasChamber, f.Areas[pl.Link].Role, "a valve shuts a gas chamber");
                            gas++;
                            break;
                        case PlacementKind.Loot when pl.Dormant && pl.Link > 0 && pl.Link >= f.Index * 100000:
                            Assert.IsTrue(layout.Placements.Exists(a => a.Kind != PlacementKind.Loot && a.Link == pl.Link && a.Floor == pl.Floor), "an altar's reward has its altar");
                            altars++;
                            break;
                        case PlacementKind.RoomController when pl.Link == (int)RoomEventMode.PitFight:
                            Assert.IsTrue(layout.Placements.Exists(m => m.Floor == pl.Floor && m.Area == pl.Area && m.Order == MobOrder.Champion && m.Dormant), "a pit fight has its champion");
                            pits++;
                            break;
                        case PlacementKind.Mob when pl.Order == MobOrder.Sleep:
                            Assert.AreEqual(AreaRole.Barracks, f.Areas[pl.Area].Role, "sleepers are in barracks");
                            sleepers++;
                            break;
                    }
                }
            }
            Assert.Greater(cellars, 0, "wine cellars with a hidden lever");
            Assert.Greater(gas, 0, "gas chambers");
            Assert.Greater(altars, 0, "altar rewards");
            Assert.Greater(pits, 0, "pit fights");
            Assert.Greater(sleepers, 0, "sleeping barracks");
        }

        /// <summary>With every vault door and shortcut closed, everything else stays reachable and every key can be reached.</summary>
        private static void AssertLocksAreSafe(DungeonLayout layout, string label)
        {
            foreach (FloorLayout f in layout.Floors)
            {
                TileGrid g = f.Grid;
                var blocked = new HashSet<int>();
                var vaults = new HashSet<int>();
                foreach (Placement pl in layout.Placements)
                {
                    if (pl.Floor != f.Index)
                        continue;
                    if (pl.Kind == PlacementKind.LockedDoor || pl.Kind == PlacementKind.Shortcut)
                        blocked.Add(g.Index(Vector2Int.FloorToInt(pl.Cell)));
                    if (pl.Kind == PlacementKind.LockedDoor)
                        vaults.Add(pl.Area);
                }
                // (The floor's own flood also crosses its teleport pads and moving platforms.)
                int[] open = f.Flood(f.ArrivalCell), closed = f.Flood(f.ArrivalCell, blocked);
                for (int i = 0; i < g.Count; i++)
                    if (open[i] >= 0 && closed[i] < 0 && !blocked.Contains(i))
                        Assert.IsTrue(vaults.Contains(g.Area[i]), $"{label}: floor {f.Index} cell {g.Coords(i)} is cut off by a lock");
                foreach (Placement pl in layout.Placements)
                    if (pl.Floor == f.Index && pl.Kind == PlacementKind.Key)
                        Assert.GreaterOrEqual(closed[g.Index(Vector2Int.FloorToInt(pl.Cell))], 0, $"{label}: key {pl.Link} can't be reached");
            }
        }

        private static void AssertTraversable(DungeonLayout layout, string label)
        {
            foreach (FloorLayout f in layout.Floors)
            {
                int[] reach = f.Flood(f.ArrivalCell);
                foreach (Area a in f.Areas)
                {
                    bool reached = false;
                    foreach (int c in a.Cells)
                        reached |= reach[c] >= 0;
                    Assert.IsTrue(reached, $"{label}: {a} unreachable");
                }
                Assert.GreaterOrEqual(reach[f.DepartureCell], 0, $"{label}: floor {f.Index} departure unreachable");
            }
            foreach (VerticalLink link in layout.Links)
            {
                TileGrid up = layout.Floors[link.UpperFloor].Grid, down = layout.Floors[link.LowerFloor].Grid;
                Assert.IsTrue(up.IsWalkable(up.Index(link.UpperLanding)), $"{label}: {link} upper landing");
                Assert.IsTrue(down.IsWalkable(down.Index(link.LowerLanding)), $"{label}: {link} lower landing");
            }
        }

        private static string Fingerprint(DungeonLayout layout)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (FloorLayout f in layout.Floors)
                    for (int i = 0; i < f.Grid.Count; i++)
                    {
                        h = (h ^ (ulong)f.Grid.Type[i]) * 1099511628211UL;
                        h = (h ^ (ulong)(f.Grid.Area[i] + 1)) * 1099511628211UL;
                    }
                foreach (Placement pl in layout.Placements)
                    h = (h ^ (ulong)Mathf.RoundToInt(pl.Cell.x * 100 + pl.Cell.y * 10000)) * 1099511628211UL;
                return h.ToString("x16");
            }
        }
    }
}
#endif
