#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves.Tests
{
    /// <summary>
    /// Edit Mode tests (Window > General > Test Runner) of the cave system's guarantees over many seeds: caves generate and
    /// stay traversable, deposits sit in walls or on open floor (never in rock, never blocking), rock formations never close
    /// a way, the same seed gives the same cave, deeper is richer, every built-in resource exists in the Crafting Database,
    /// formations mesh identically everywhere, a cave's memory survives a save (and is never forgotten while a visit is
    /// going on), a restored seam goes on growing back, and a mouth's trigger holds a player walking in.
    /// </summary>
    public class CaveGenerationTests
    {
        private static (CompiledProfile compiled, DungeonPipeline pipeline, CaveMiningStage stage) CaveSetup()
        {
            var profile = ScriptableObject.CreateInstance<DungeonProfile>();
            CaveDefaults.ApplyProfile(profile);
            var stage = ScriptableObject.CreateInstance<CaveMiningStage>();
            if (profile.customStages == null)
                profile.customStages = new List<DungeonStageAsset>();
            profile.customStages.Add(stage);
            CompiledProfile compiled = CompiledProfile.Compile(profile);
            return (compiled, DungeonPipeline.CreateDefault(compiled.CustomStages), stage);
        }

        private static IEnumerable<DungeonLayout> Caves(int count, SizeClass size = SizeClass.Medium, float difficulty = 1f, int salt = 0)
        {
            (CompiledProfile compiled, DungeonPipeline pipeline, _) = CaveSetup();
            for (int s = 1; s <= count; s++)
                yield return pipeline.Generate(new DungeonRequest { seed = s * 92821 + salt, size = size, difficulty = difficulty }, compiled);
        }

        [Test]
        public void CaveStageRunsBetweenAnalysisAndPopulation()
        {
            (CompiledProfile compiled, _, CaveMiningStage stage) = CaveSetup();
            Assert.AreEqual(StageSlot.AfterAnalysis, stage.slot);
            Assert.Contains(stage, new List<DungeonStageAsset>(compiled.CustomStages));
        }

        [Test]
        public void CavesHaveDepositsInValidPlaces()
        {
            int total = 0;
            foreach (DungeonLayout layout in Caves(8))
            {
                foreach (Placement p in layout.Placements)
                {
                    if (p.Kind != PlacementKind.Resource)
                        continue;
                    total++;
                    TileGrid g = layout.Floors[p.Floor].Grid;
                    int x = Mathf.FloorToInt(p.Cell.x), y = Mathf.FloorToInt(p.Cell.y);
                    Assert.IsTrue(g.InBounds(x, y), "deposit inside the level");
                    int c = g.Index(x, y);
                    Assert.IsTrue(g.IsWalkable(c), $"deposit on open ground (floor {p.Floor}, cell {x},{y})");
                    Assert.IsTrue(g.Has(c, CellFlags.Occupied), "deposit cell taken (creatures and props keep clear)");
                    Assert.IsFalse(g.Has(c, CellFlags.Landing | CellFlags.Locked), "never on a landing or a lock");
                    if (p.Wave == CavePlacements.WallMount)
                    {
                        // Its back is against rock.
                        Vector3 f = Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward;
                        int dx = 0, dy = 0;
                        if (Mathf.Abs(f.x) > Mathf.Abs(f.z)) dx = f.x > 0f ? -1 : 1;
                        else dy = f.z > 0f ? -1 : 1;
                        Assert.IsTrue(g.InBounds(x + dx, y + dy) && g.IsRock(g.Index(x + dx, y + dy)), "a wall deposit's back is in a wall");
                        float above = p.Height - g.FloorHeight[c];
                        Assert.Greater(above, 0.1f, "a wall deposit is above the floor");
                        Assert.Less(p.Height, g.CeilingHeight[c], "a wall deposit is below the ceiling");
                    }
                    else
                    {
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                if (ox != 0 || oy != 0)
                                    Assert.IsTrue(g.IsWalkable(g.Index(x + ox, y + oy)), "a floor deposit has open ground all round");
                    }
                    Assert.GreaterOrEqual(p.Entry, 0);
                }
            }
            Assert.Greater(total, 0, "caves hold deposits");
        }

        [Test]
        public void BlockersNeverCloseAWay()
        {
            foreach (DungeonLayout layout in Caves(8, SizeClass.Large))
            {
                var blocked = new HashSet<(int floor, int cell)>();
                foreach (Placement p in layout.Placements)
                    if (p.Kind == PlacementKind.Resource && p.Wave == CavePlacements.FloorMount)
                    {
                        TileGrid g = layout.Floors[p.Floor].Grid;
                        blocked.Add((p.Floor, g.Index(Mathf.FloorToInt(p.Cell.x), Mathf.FloorToInt(p.Cell.y))));
                    }
                foreach (FloorLayout f in layout.Floors)
                {
                    TileGrid g = f.Grid;
                    for (int c = 0; c < g.Count; c++)
                        if (g.Has(c, CellFlags.Formation))
                            blocked.Add((f.Index, c));
                    bool[] free = Reach(g, f.ArrivalCell, _ => false);
                    bool[] around = Reach(g, f.ArrivalCell, c => blocked.Contains((f.Index, c)));
                    for (int c = 0; c < g.Count; c++)
                        if (free[c] && !blocked.Contains((f.Index, c)))
                            Assert.IsTrue(around[c], $"floor {f.Index} cell {g.X(c)},{g.Y(c)} cut off by a formation or deposit");
                }
            }
        }

        private static bool[] Reach(TileGrid g, int from, System.Func<int, bool> isBlocked)
        {
            var seen = new bool[g.Count];
            if (from < 0 || !g.IsWalkable(from))
                return seen;
            var queue = new Queue<int>();
            queue.Enqueue(from);
            seen[from] = true;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int n = g.Neighbor(c, d);
                    if (n < 0 || seen[n] || !g.IsWalkable(n) || isBlocked(n))
                        continue;
                    seen[n] = true;
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        [Test]
        public void SameSeedSameCave()
        {
            (CompiledProfile compiled, DungeonPipeline pipeline, _) = CaveSetup();
            var request = new DungeonRequest { seed = 777001, size = SizeClass.Large };
            Assert.AreEqual(Fingerprint(pipeline.Generate(request, compiled)), Fingerprint(pipeline.Generate(request.Clone(), compiled)));
        }

        private static string Fingerprint(DungeonLayout layout)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (FloorLayout f in layout.Floors)
                    for (int i = 0; i < f.Grid.Count; i++)
                        h = (h ^ (ulong)(f.Grid.Has(i, CellFlags.Formation) ? 1 : 0) ^ ((ulong)f.Grid.Type[i] << 2)) * 1099511628211UL;
                foreach (Placement p in layout.Placements)
                    if (p.Kind == PlacementKind.Resource)
                        h = (h ^ (ulong)(p.Entry * 7919 + p.Link) ^ (ulong)Mathf.RoundToInt(p.Cell.x * 100 + p.Cell.y * 10000)) * 1099511628211UL;
                return h.ToString("x16");
            }
        }

        [Test]
        public void DeeperIsRicher()
        {
            // Mean tier and the share of rare deposits by level, over many large caves.
            var tierSum = new float[3];
            var count = new int[3];
            var rare = new int[3];
            foreach (DungeonLayout layout in Caves(16, SizeClass.Large, 1f, 31))
                foreach (Placement p in layout.Placements)
                {
                    if (p.Kind != PlacementKind.Resource || p.Floor > 2)
                        continue;
                    tierSum[p.Floor] += p.Tier;
                    count[p.Floor]++;
                    if (p.Tier >= (int)CaveRarity.Rare)
                        rare[p.Floor]++;
                }
            Assert.Greater(count[0], 0);
            Assert.Greater(count[1], 0);
            float mean0 = tierSum[0] / count[0], mean1 = tierSum[1] / count[1];
            Assert.Greater(mean1, mean0, "deposits a level down are rarer on average");
            Assert.Greater(rare[1] / (float)count[1], rare[0] / (float)count[0], "more rare deposits a level down");
        }

        [Test]
        public void HarderCavesAreDeeper()
        {
            var settings = new CaveDepthSettings();
            float easy = CaveDepth.Of(settings, 0, 0.5f, 0, 1f), hard = CaveDepth.Of(settings, 0, 0.5f, 0, 3f), chained = CaveDepth.Of(settings, 0, 0.5f, 2, 1f);
            Assert.Greater(hard, easy);
            Assert.Greater(chained, hard);
            Assert.AreEqual(CaveDepthBand.Shallow, settings.Band(easy));
            Assert.GreaterOrEqual((int)settings.Band(chained), (int)CaveDepthBand.Deep);
        }

        [Test]
        public void EveryBuiltInResourceExistsInTheCraftingDatabase()
        {
            CraftRules rules = CraftDatabase.Instance != null ? CraftDatabase.Instance.Rules : null;
            Assert.IsNotNull(rules, "Crafting Database");
            foreach (CaveResourceEntry e in CaveDefaults.Resources())
            {
                CraftStockItemSO item = CraftItems.Stock(rules, e.kind, e.craftId);
                Assert.IsNotNull(item, $"{e.name}: {e.kind} '{e.craftId}'");
                if (!string.IsNullOrEmpty(e.gem))
                    Assert.IsNotNull(CraftItems.Stock(rules, CraftStockKind.Gem, e.gem), $"{e.name}: gem '{e.gem}'");
            }
        }

        [Test]
        public void EveryDepthBandHasResources()
        {
            CaveRules rules = CaveRules.Create(null);
            Assert.IsEmpty(EditorTools.CaveSetupMenu.CheckRules("built-in", rules).FindAll(s => s.Contains("band")));
        }

        [Test]
        public void FormationsMeshTheSameEverywhere()
        {
            CaveRules rules = CaveRules.Create(null);
            foreach (DungeonLayout layout in Caves(3, SizeClass.Medium, 2f, 5))
            {
                int standing = 0;
                for (int f = 0; f < layout.Floors.Count; f++)
                {
                    CaveFloorFormations a = CaveFormationMesher.Build(layout, f, rules, 0.3f, 24);
                    CaveFloorFormations b = CaveFormationMesher.Build(layout, f, rules, 0.3f, 24);
                    Assert.AreEqual(a.Chunks.Count, b.Chunks.Count);
                    for (int i = 0; i < a.Chunks.Count; i++)
                    {
                        Assert.AreEqual(a.Chunks[i].Rock.Vertices.Count, b.Chunks[i].Rock.Vertices.Count);
                        Assert.AreEqual(a.Chunks[i].Crystal.Vertices.Count, b.Chunks[i].Crystal.Vertices.Count);
                        if (a.Chunks[i].Rock.Vertices.Count > 0)
                            Assert.AreEqual(a.Chunks[i].Rock.Vertices[0], b.Chunks[i].Rock.Vertices[0]);
                    }
                    standing += a.Standing;
                }
                Assert.Greater(standing, 0, "caves have standing formations");
            }
        }

        [Test]
        public void CaveMemorySurvivesASave()
        {
            bool enabled = CaveStore.Enabled;
            CaveStore.Enabled = true;
            try
            {
                CaveStore.FromJson("");
                CaveStore.Record r = CaveStore.Open("test:1:0", 1234, "Test Hollow");
                Assert.IsNotNull(r);
                r.SetDeposit(5, new OreDeposit.SavedState { taken = 3, gathered = 1, issued = 3, work = 4f, depletedFor = -1f });
                r.Slay(9);
                r.Take(11);
                string json = CaveStore.ToJson();
                CaveStore.FromJson(json);
                CaveStore.Record back = CaveStore.Find("test:1:0");
                Assert.IsNotNull(back);
                Assert.IsTrue(back.TryGetDeposit(5, out OreDeposit.SavedState s, out _));
                Assert.AreEqual(3, s.taken);
                Assert.AreEqual(1, s.gathered);
                Assert.IsTrue(back.IsSlain(9, 0.0));
                Assert.IsTrue(back.IsTaken(11, 0.0));
                // Another layout behind the same key: the old record is not applied to it.
                CaveStore.Record other = CaveStore.Open("test:1:0", 999, "Test Hollow");
                Assert.IsFalse(other.TryGetDeposit(5, out _, out _));
            }
            finally
            {
                CaveStore.Forget("test:1:0");
                CaveStore.Enabled = enabled;
            }
        }

        [Test]
        public void ACaveBeingVisitedIsNeverForgotten()
        {
            bool enabled = CaveStore.Enabled;
            int max = CaveStore.MaxCaves;
            CaveStore.Enabled = true;
            try
            {
                CaveStore.FromJson("");
                CaveStore.MaxCaves = 3;
                CaveStore.Record visiting = CaveStore.Open("held:1:0", 1, "Held");   // a long visit
                for (int i = 0; i < 8; i++)
                    CaveStore.Release(CaveStore.Open("short:" + i, 1, "Short " + i));   // short visits, over
                Assert.LessOrEqual(CaveStore.Count, 3, "the caves left longest ago are forgotten");
                Assert.AreSame(visiting, CaveStore.Find("held:1:0"), "the cave being visited keeps its record");
                Assert.IsNotNull(CaveStore.Find("short:7"), "the cave left last is kept");
                CaveStore.Release(visiting);
                CaveStore.Release(CaveStore.Open("last:1:0", 1, "Last"));
                CaveStore.MaxCaves = 1;
                CaveStore.Release(CaveStore.Open("last:1:0", 1, "Last"));
                Assert.AreEqual(1, CaveStore.Count, "once its visit is over it can be forgotten");
            }
            finally
            {
                CaveStore.MaxCaves = max;
                CaveStore.FromJson("");
                CaveStore.Enabled = enabled;
            }
        }

        [Test]
        public void ARestoredSeamGoesOnGrowingBack()
        {
            var go = new GameObject("Test Seam");
            try
            {
                OreDeposit seam = go.AddComponent<OreDeposit>();
                float regrow = Time.time + 10000f;
                seam.Configure("hematite", CraftStockKind.Ore, 77, lumpCount: 4, regrow: regrow);
                // Worked out for longer than this session has lasted (kept in an earlier session): its time must still count.
                float elapsed = Time.time + 300f;
                seam.RestoreState(new OreDeposit.SavedState { taken = 4, issued = 4, depletedFor = elapsed - 100f }, 100f);
                Assert.IsTrue(seam.WorkedOut);
                Assert.AreEqual(regrow - elapsed, seam.SecondsUntilRegrown, 0.05f, "it goes on growing back where it was");
                Assert.AreEqual(elapsed, seam.CaptureState().depletedFor, 0.05f, "kept again, its whole time worked out counts");
                // Grown back while away: whole again, its lumps numbered on.
                seam.RestoreState(new OreDeposit.SavedState { taken = 4, issued = 4, depletedFor = 600f }, regrow);
                Assert.IsFalse(seam.WorkedOut);
                Assert.AreEqual(4, seam.LumpsIssued);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayersStayInAMouthsTriggerAndComeBackOutsideIt()
        {
            foreach (CaveMouthBuilder.Style style in System.Enum.GetValues(typeof(CaveMouthBuilder.Style)))
                for (int seed = 1; seed <= 12; seed++)
                {
                    var shape = new CaveMouthBuilder.Shape { Style = style, Width = 3.4f, Height = 3.6f, Depth = 3.4f, Seed = seed * 31 };
                    CaveMouthBuilder.Meshes m = CaveMouthBuilder.Build(shape);
                    float front = m.TriggerCenter.z + m.TriggerSize.z * 0.5f, back = m.TriggerCenter.z - m.TriggerSize.z * 0.5f;
                    // From just inside the opening to past the end of the tunnel: walking in never leaves it.
                    Assert.Less(back, -shape.Depth, $"{style} {seed}: the trigger reaches the end of the tunnel");
                    Assert.Less(front, 0f, $"{style} {seed}: the trigger starts inside the opening");
                    if (style == CaveMouthBuilder.Style.Overworld)
                        Assert.Greater(CaveMouthBuilder.ReturnZ(shape.Width) - 1.4f - 0.5f, front, "players (and their party round them) come back outside the trigger");
                }
        }

        [Test]
        public void SitesArePlannedTheSameWay()
        {
            var s = new CaveEntranceSettings { caveProfiles = new List<DungeonProfile> { ScriptableObject.CreateInstance<DungeonProfile>() }, spawnChance = 1f };
            var a = new List<CaveSitePlanner.Site>();
            var b = new List<CaveSitePlanner.Site>();
            CaveSitePlanner.SitesInArea(s, 4242, new Vector2(-2000f, -2000f), new Vector2(2000f, 2000f), a);
            CaveSitePlanner.SitesInArea(s, 4242, new Vector2(-2000f, -2000f), new Vector2(2000f, 2000f), b);
            Assert.Greater(a.Count, 0);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Id, b[i].Id);
                Assert.AreEqual(a[i].Point, b[i].Point);
                for (int j = i + 1; j < a.Count; j++)
                    Assert.GreaterOrEqual((a[i].Point - a[j].Point).magnitude, s.EffectiveMinDistance * 0.999f, "caves keep their distance");
            }
        }
    }
}
#endif
