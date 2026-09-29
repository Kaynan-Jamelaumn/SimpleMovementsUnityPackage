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

        private static void AssertTraversable(DungeonLayout layout, string label)
        {
            foreach (FloorLayout f in layout.Floors)
            {
                int[] reach = ValidateStage.Flood(f.Grid, f.ArrivalCell);
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
