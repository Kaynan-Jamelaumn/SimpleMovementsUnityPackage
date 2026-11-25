using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using ProceduralDungeon;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralCaves
{
    /// <summary>
    /// One build of a cave (made by <see cref="CaveMiningStage.BeginBuild"/> for every build of a profile with that stage,
    /// and for every network client's copy). On the main thread, as the <see cref="DungeonBuilder"/> goes:
    /// <list type="bullet">
    /// <item>deposits: each Resource placement becomes a pooled deposit object (the entry's prefab, or the generated rock
    /// with its mineral) with an <see cref="OreDeposit"/> set up from its resource entry - mined with a pick, a few loose
    /// stones by hand - set into the drawn wall (the mesher's contour rule) or standing on the floor; mined deposits come
    /// back as they were left (the cave's memory), growing back meanwhile;</item>
    /// <item>rock formations per level: the mesh data is made on a dungeon worker thread from the level's grid and build
    /// seed (the same everywhere), then batched per mesh chunk with one collider each, cooked before the level's NavMesh is
    /// baked so creatures walk round them; crystal grottos glow;</item>
    /// <item>the way out and the way deeper as natural passages round the dungeon's own portals (a fade, then the portal's
    /// rules);</item>
    /// <item>where the rules run: creatures made tougher with depth (<see cref="CaveDeepDweller"/>), slain creatures left
    /// out on the next visit until they come back, opened chests and broken containers kept as they were.</item>
    /// </list>
    /// Everything it made is given back or destroyed with the dungeon.
    /// </summary>
    public sealed class CaveBuildSession : IDungeonBuildSession
    {
        private readonly CaveRules rules;
        private readonly DungeonBuildContext ctx;
        private readonly CaveInstance cave;
        private readonly float roughness;
        private readonly int chunkCells;
        private readonly float textureScale;
        private readonly WorkToken token = new WorkToken();

        // Deposit objects from the generated-art pool (given back on teardown); every deposit's adapter by placement.
        private readonly List<GameObject> artDeposits = new List<GameObject>();
        private readonly Dictionary<int, CaveDeposit> deposits = new Dictionary<int, CaveDeposit>();
        private readonly List<(CombatEntity entity, Action<CombatEntity> handler)> deaths = new List<(CombatEntity, Action<CombatEntity>)>();
        private readonly List<(DungeonChest chest, Action<DungeonChest, int> handler)> chests = new List<(DungeonChest, Action<DungeonChest, int>)>();
        private bool replicaOpened;

        /// <summary>Statistics of this build (tests, the inspector): formations and lights made.</summary>
        public int StandingFormations { get; private set; }
        public int HangingFormations { get; private set; }
        public int CrystalClusters { get; private set; }
        public int FormationChunks { get; private set; }
        public int GrottoLights { get; private set; }

        public CaveBuildSession(CaveMiningStage stage, CaveRules rules, DungeonBuildContext context)
        {
            ctx = context ?? throw new ArgumentNullException(nameof(context));
            this.rules = rules ?? (stage != null ? stage.Rules : CaveRules.Create(null));
            CompiledProfile profile = ctx.Profile;
            roughness = profile != null && profile.Caves != null ? profile.Caves.wallRoughness : 0.3f;
            chunkCells = profile != null && profile.Build != null ? profile.Build.meshChunkCells : 24;
            textureScale = profile != null ? profile.TextureScale : 3f;
            if (ctx.Instance != null)
            {
                cave = ctx.Instance.GetComponent<CaveInstance>();
                if (cave == null)
                    cave = ctx.Instance.gameObject.AddComponent<CaveInstance>();
                cave.Setup(ctx.Instance, this.rules, ctx.Replica);
            }
        }

        /// <summary>The cave being built.</summary>
        public CaveInstance Cave => cave;

        private bool RulesSide => !ctx.Replica;

        /// <summary>
        /// The id of placement <paramref name="index"/>: its index in the server's layout (a client's layout holds only what
        /// it builds, in its own order). What the cave remembers, and what a deposit says it is, use it - the same everywhere.
        /// </summary>
        private int IdOf(int index) => ctx.Layout != null ? ctx.Layout.PlacementId(index) : index;

        // ------------------------------------------------------------------ placements

        public bool ShouldBuild(Placement p, int index)
        {
            if (!RulesSide || cave == null)
                return true;
            switch (p.Kind)
            {
                case PlacementKind.Mob:
                case PlacementKind.Boss:
                    // Slain creatures stay away until they come back (Between Visits). A client builds no creatures anyway.
                    return !cave.IsSlain(IdOf(index));
                case PlacementKind.Loot:
                    // An item picked up on an earlier visit stays gone - a room's reward too, even when its guards are still
                    // away. Chests and containers are built, and shown emptied.
                    return !(IsPickup(p) && cave.IsTaken(IdOf(index)));
                default:
                    return true;
            }
        }

        /// <summary>Is Loot placement <paramref name="p"/> an item lying on the ground (not a container)?</summary>
        private bool IsPickup(in Placement p)
        {
            CompiledProfile profile = ctx.Profile;
            return profile != null && p.Entry >= 0 && p.Entry < profile.Loot.Count && DungeonBuilder.IsItemPickup(profile.Loot[p.Entry].Prefab);
        }

        public bool TryBuildPlacement(Placement p, int index, Transform parent, Vector3 local, Quaternion rotation,
            out GameObject built, out GameObject prefab, out string entryName)
        {
            built = null;
            prefab = null;
            entryName = null;
            if (p.Kind != PlacementKind.Resource)
                return false;
            CaveResourceEntry e = rules.Resource(p.Entry);
            if (e == null)
                return true;   // one of ours, but its entry is gone (the table was edited): nothing to build
            entryName = string.IsNullOrEmpty(e.name) ? e.craftId : e.name;
            bool onWall = p.Wave == CavePlacements.WallMount;
            Vector3 at = onWall ? WallPosition(p, local) : local;
            float size = p.Scale > 0f ? p.Scale : 1f;

            GameObject go;
            GameObject mineral = null;
            if (e.prefab != null)
            {
                go = ctx.Pool.Get(e.prefab, parent, at, rotation);
                if (go == null)
                    return true;
                prefab = e.prefab;
                go.transform.localScale = e.prefab.transform.localScale * size;
            }
            else
            {
                go = CaveDepositArt.Take(parent);
                go.transform.localPosition = at;
                go.transform.localRotation = rotation;
                artDeposits.Add(go);
                Color rock = RockColor(e), ore = MineralColor(e);
                Material rockMat = ctx.Materials != null ? ctx.Materials.Flat(rock) : CaveMaterials.Flat(rock);
                Material oreMat = ctx.Materials != null ? ctx.Materials.Flat(ore, e.glow) : CaveMaterials.Flat(ore, e.glow);
                mineral = CaveDepositArt.Dress(go, e, p.Link, onWall, size, rockMat, oreMat, ctx.CastShadows);
                go.name = entryName;
            }
            int layer = ctx.GeometryLayer;
            if (layer > 0)
                SetLayer(go, layer);

            OreDeposit deposit = go.GetComponent<OreDeposit>();
            if (deposit == null)
                deposit = go.AddComponent<OreDeposit>();
            if (go.GetComponent<CaveDeposit>() == null)
                go.AddComponent<CaveDeposit>();
            float regrow = e.regrowMinutes > 0f && rules.Persistence.regrowScale > 0f ? e.regrowMinutes * 60f * rules.Persistence.regrowScale : 0f;
            deposit.Configure(e.craftId, e.kind, p.Link, LumpsFor(e, p), e.workPerLump, regrow, e.gem, e.gemChance, e.gatherByHand,
                e.looseStones, e.minToolDamage, entryName, e.itemOverride);
            if (mineral != null)
                deposit.SetVisuals(mineral, null);
            built = go;
            return true;
        }

        public void OnBuilt(GameObject go, Placement p, int index)
        {
            if (go == null)
                return;
            switch (p.Kind)
            {
                case PlacementKind.Resource:
                    SetupDeposit(go, p, index);
                    break;
                case PlacementKind.Mob:
                case PlacementKind.Boss:
                    if (RulesSide)
                        SetupCreature(go, p, index);
                    break;
                case PlacementKind.Loot:
                    if (RulesSide)
                        SetupContainer(go, index);
                    break;
                case PlacementKind.EntrancePortal:
                case PlacementKind.ExitPortal:
                    if (rules.Access.naturalPassages)
                        DressPassage(go, p);
                    break;
                default:
                    // Breakable props (urns, barrels, crates) stay broken between visits too.
                    if (RulesSide && go.GetComponent<DungeonBreakable>() != null)
                        SetupContainer(go, index);
                    break;
            }
        }

        // ------------------------------------------------------------------ deposits

        /// <summary>Where a wall deposit goes: on the wall the mesher draws (its contour point), a little out of it.</summary>
        private Vector3 WallPosition(in Placement p, Vector3 local)
        {
            DungeonLayout layout = ctx.Layout;
            if (layout == null || p.Floor < 0 || p.Floor >= layout.Floors.Count || layout.Floors[p.Floor] == null)
                return local;
            TileGrid g = layout.Floors[p.Floor].Grid;
            if (g == null)
                return local;
            int x = Mathf.FloorToInt(p.Cell.x), y = Mathf.FloorToInt(p.Cell.y);
            Vector3 facing = Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward;
            // The wall is behind the deposit (it faces out of it, into the chamber).
            int dx = 0, dy = 0;
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.z))
                dx = facing.x > 0f ? -1 : 1;
            else
                dy = facing.z > 0f ? -1 : 1;
            if (!g.InBounds(x, y) || !g.InBounds(x + dx, y + dy) || !g.IsRock(g.Index(x + dx, y + dy)))
                return local;
            float cs = layout.CellSize;
            Vector2 w = CaveWalls.WallPoint(g, cs, layout.FloorBuildSeed(p.Floor), roughness, x, y, x + dx, y + dy);
            // A little out of the drawn line: the wall bulges in and out between its contour points.
            return new Vector3(w.x, local.y, w.y) + facing * 0.12f;
        }

        /// <summary>Lumps of a deposit: its entry's range, more for a bigger rock (the same on every machine).</summary>
        private static int LumpsFor(CaveResourceEntry e, in Placement p)
        {
            int lo = Mathf.Min(e.lumps.min, e.lumps.max), hi = Mathf.Max(e.lumps.min, e.lumps.max);
            float roll = PlacementRandom.Value(p.Link, PlacementRandom.StableHash("CaveLumps"), p.Entry, Mathf.FloorToInt(p.Cell.x), Mathf.FloorToInt(p.Cell.y));
            int n = lo + Mathf.Min(hi - lo, Mathf.FloorToInt(roll * (hi - lo + 1)));
            float mid = Mathf.Max(0.1f, (e.size.min + e.size.max) * 0.5f);
            float bigger = Mathf.Clamp((p.Scale > 0f ? p.Scale : mid) / mid, 0.7f, 1.5f);
            return Mathf.Max(1, Mathf.RoundToInt(n * bigger));
        }

        private void SetupDeposit(GameObject go, Placement p, int index)
        {
            OreDeposit deposit = go.GetComponent<OreDeposit>();
            CaveDeposit adapter = go.GetComponent<CaveDeposit>();
            if (deposit == null || adapter == null || cave == null)
                return;
            CaveResourceEntry e = rules.Resource(p.Entry);
            float depth = cave.DepthOf(p.Floor, p.Area);
            int id = IdOf(index);
            // The cave's memory first (before anything listens, so nothing is written back for it).
            if (RulesSide && cave.TryGetDeposit(id, out OreDeposit.SavedState state, out float secondsAgo))
                deposit.RestoreState(state, secondsAgo);
            adapter.Setup(cave, deposit, id, e, depth, ctx.Replica);
            deposits[id] = adapter;
            cave.DepositCount = deposits.Count;
            // What players arriving later (and the run's clients) see of it.
            if (RulesSide && (deposit.LumpsIssued > 0 || deposit.WorkedOut || deposit.ReplicatedState != 0))
                adapter.PublishState();
        }

        private static Color RockColor(CaveResourceEntry e) => e.rockColor;

        /// <summary>The colour of what shows in the rock: the Crafting Database's (ores, gems, supplies) or the entry's own.</summary>
        public static Color MineralColor(CaveResourceEntry e)
        {
            if (e == null)
                return Color.gray;
            if (!e.databaseColors || string.IsNullOrEmpty(e.craftId))
                return e.mineralColor;
            try
            {
                CraftDatabase db = CraftDatabase.Instance;
                CraftRules r = db != null ? db.Rules : null;
                if (r == null)
                    return e.mineralColor;
                switch (e.kind)
                {
                    case CraftStockKind.Ore:
                    {
                        CraftOreDefinition o = r.Ore(e.craftId);
                        if (o != null)
                            return Opaque(o.color);
                        break;
                    }
                    case CraftStockKind.Gem:
                    {
                        CraftGemDefinition g = r.Gem(e.craftId);
                        if (g != null)
                            return Opaque(g.color);
                        break;
                    }
                    case CraftStockKind.Supply:
                    {
                        CraftSupplyDefinition s = r.Supply(e.craftId);
                        if (s != null)
                            return Opaque(s.color);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            return e.mineralColor;
        }

        private static Color Opaque(Color c) => new Color(c.r, c.g, c.b, 1f);

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform)
                SetLayer(t.gameObject, layer);
        }

        // ------------------------------------------------------------------ creatures and containers

        private void SetupCreature(GameObject go, Placement p, int index)
        {
            if (cave == null)
                return;
            int bands = (int)rules.Depth.Band(cave.DepthOf(p.Floor, p.Area));
            CaveDeepDweller.Apply(go, bands, rules.Enemies);
            if (cave.Record == null)
                return;
            CombatEntity entity = go.GetComponentInChildren<CombatEntity>(true);
            if (entity == null)
                return;
            int id = IdOf(index);
            CaveInstance c = cave;
            Action<CombatEntity> handler = _ => c.KeepSlain(id);
            entity.Died += handler;
            deaths.Add((entity, handler));
        }

        private void SetupContainer(GameObject go, int index)
        {
            if (cave == null || cave.Record == null)
                return;
            index = IdOf(index);
            foreach (DungeonChest chest in go.GetComponentsInChildren<DungeonChest>(true))
            {
                if (cave.IsTaken(index))
                {
                    // Emptied on an earlier visit: it stands open (and every player sees it open).
                    chest.ApplyNetState(1, null);
                    DungeonNet.Publish(chest, 1);
                    continue;
                }
                int id = index;
                CaveInstance c = cave;
                Action<DungeonChest, int> handler = (_, __) => c.KeepTaken(id);
                chest.Opened += handler;
                chests.Add((chest, handler));
            }
            foreach (DungeonBreakable b in go.GetComponentsInChildren<DungeonBreakable>(true))
            {
                if (cave.IsTaken(index))
                {
                    // Broken on an earlier visit: broken at once, quietly (no pieces, drops or stash; clients are told).
                    b.RestoreBroken();
                    continue;
                }
                cave.WatchBreakable(b, index);
            }
            // An item lying in the cave: picked up, it stays gone (see ShouldBuild).
            foreach (ItemPickable pickup in go.GetComponentsInChildren<ItemPickable>(true))
                cave.WatchPickup(pickup, index);
        }

        // ------------------------------------------------------------------ the way out and the way deeper

        private void DressPassage(GameObject go, Placement p)
        {
            var portal = go.GetComponent<DungeonPortal>();
            if (portal == null)
                return;
            bool entrance = p.Kind == PlacementKind.EntrancePortal;
            DungeonTheme theme = ctx.Profile != null ? ctx.Profile.Theme : null;
            GameObject own = theme != null ? (entrance ? theme.entrancePortal : theme.exitPortal) : null;
            if (own != null)
                return;   // the theme's own portal stays as it is
            DungeonLayout layout = ctx.Layout;
            if (layout == null || p.Floor < 0 || p.Floor >= layout.Floors.Count || layout.Floors[p.Floor] == null)
                return;
            TileGrid g = layout.Floors[p.Floor].Grid;
            float cs = layout.CellSize;

            // The stand-in portal goes: the passage's rock is its look, the passage's trigger its way through.
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            foreach (Light l in go.GetComponentsInChildren<Light>(true))
                l.enabled = false;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                c.enabled = false;

            bool deeper = portal.action == DungeonPortal.PortalAction.NextDungeon;
            int cx = Mathf.FloorToInt(p.Cell.x), cy = Mathf.FloorToInt(p.Cell.y);
            float headroom = 4f;
            if (g != null && g.InBounds(cx, cy))
            {
                int cell = g.Index(cx, cy);
                headroom = g.CeilingHeight[cell] - g.FloorHeight[cell];
            }
            float height = Mathf.Clamp(headroom - 0.6f, 2.4f, 3.4f);
            float width = Mathf.Clamp(cs * 1.7f, 2.4f, 3.2f);
            const float depth = 2.8f;
            // Stand against the wall behind: the tunnel's end meets it.
            float behind = g != null ? DistanceToRock(g, p.Cell, -(Quaternion.Euler(0f, p.Yaw, 0f) * Vector3.forward), 4f) * cs : depth;
            float forward = Mathf.Clamp(depth - behind - 0.25f, -1.5f, depth * 0.6f);

            var shape = new CaveMouthBuilder.Shape
            {
                Style = deeper ? CaveMouthBuilder.Style.WayDeeper : CaveMouthBuilder.Style.WayOut,
                Width = width,
                Height = height,
                Depth = depth,
                Seed = layout.FloorBuildSeed(p.Floor) ^ (entrance ? 0x5157 : 0x7A31),
            };
            CaveMouthBuilder.Meshes meshes = CaveMouthBuilder.Build(shape);
            var holder = new GameObject(deeper ? "Way Deeper" : "Way Out");
            holder.layer = ctx.GeometryLayer;
            holder.transform.SetParent(go.transform, false);
            holder.transform.localPosition = new Vector3(0f, 0f, forward);
            holder.transform.localRotation = Quaternion.identity;

            Color wallColor = theme != null ? theme.caveWallColor : new Color(0.38f, 0.33f, 0.28f);
            DungeonMaterials mats = ctx.Materials;
            Material rock = mats != null && mats.BySurface[(int)DungeonSurface.CaveWall] != null ? mats.BySurface[(int)DungeonSurface.CaveWall] : CaveMaterials.Flat(wallColor);
            Material dim = mats != null ? mats.Flat(CaveMaterials.Shade(wallColor, 0.45f)) : CaveMaterials.Flat(CaveMaterials.Shade(wallColor, 0.45f));
            Material dark = mats != null ? mats.Flat(CaveMaterials.Shade(wallColor, 0.14f)) : CaveMaterials.Flat(CaveMaterials.Shade(wallColor, 0.14f));
            Material curtain;
            if (deeper)
                curtain = mats != null && mats.BySurface[(int)DungeonSurface.Void] != null ? mats.BySurface[(int)DungeonSurface.Void] : CaveMaterials.Flat(Color.black);
            else
                curtain = mats != null ? mats.Flat(rules.Access.daylight, Mathf.Max(0.5f, rules.Access.daylightIntensity)) : CaveMaterials.Flat(rules.Access.daylight, rules.Access.daylightIntensity);

            // Tangents (a themed, normal-mapped material) are made in the upload: the rock's collider is cooked on a job
            // thread from the moment it is added, and its mesh must not change meanwhile.
            var made = new List<Mesh>(2);
            bool tangents = ctx.Profile != null && ctx.Profile.Theme != null;
            CaveMouthBuilder.Create(holder.transform, meshes, rock, dim, dark, curtain, true, ctx.AddCollider, made, ctx.GeometryLayer, tangents);
            foreach (Mesh mesh in made)
                ctx.Own(mesh);

            if (!deeper && rules.Access.daylightIntensity > 0f)
            {
                // Daylight coming down the way out.
                var lightGo = new GameObject("Daylight");
                lightGo.transform.SetParent(holder.transform, false);
                lightGo.transform.localPosition = meshes.LightPosition;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = rules.Access.daylight;
                light.intensity = rules.Access.daylightIntensity;
                light.range = Mathf.Max(6f, height * 2.6f);
                light.shadows = LightShadows.None;
            }

            // The trigger: walking in darkens the screen, then the portal's rules send the group.
            var triggerGo = new GameObject("Passage Trigger");
            triggerGo.transform.SetParent(holder.transform, false);
            triggerGo.transform.localPosition = meshes.TriggerCenter;
            var box = triggerGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = meshes.TriggerSize;
            var passage = triggerGo.AddComponent<CavePassage>();
            passage.LeadTo(portal);
            passage.delay = rules.Access.fadeSeconds;
            passage.armDelay = Mathf.Max(portal.armDelay, 1.5f);
            passage.sound = rules.Access.passageSound;
            string name = cave != null ? cave.BaseName : "the cave";
            if (deeper)
            {
                passage.title = "Deeper down";
                // (A client's copy knows the run's depth too: it comes with the run's header.)
                passage.subtitle = cave != null ? $"{name} - Depth {cave.ChainDepth + 2}" : $"Further into {name}";
            }
            else
            {
                passage.title = portal.action == DungeonPortal.PortalAction.CompleteDungeon ? "Back to the surface" : "Leaving the cave";
                passage.subtitle = $"{name}, behind you";
            }
        }

        /// <summary>Cells from <paramref name="from"/> (cell units) along <paramref name="dir"/> to the first rock (at most <paramref name="max"/>).</summary>
        private static float DistanceToRock(TileGrid g, Vector2 from, Vector3 dir, float max)
        {
            var d = new Vector2(dir.x, dir.z);
            if (d.sqrMagnitude < 1e-6f)
                return max;
            d.Normalize();
            const float step = 0.2f;
            for (float t = step; t <= max; t += step)
            {
                Vector2 q = from + d * t;
                int x = Mathf.FloorToInt(q.x), y = Mathf.FloorToInt(q.y);
                if (!g.InBounds(x, y) || g.IsRock(g.Index(x, y)))
                    return Mathf.Max(0f, t - step * 0.5f);
            }
            return max;
        }

        // ------------------------------------------------------------------ rock formations

        private sealed class FormationJob
        {
            public CaveFloorFormations Result;
            public Exception Error;
            private int done;
            public bool Done => Volatile.Read(ref done) == 1;
            public void Finish() => Volatile.Write(ref done, 1);
        }

        public IEnumerator BuildFloor(int floor)
        {
            if (rules.Formations.enabled && ctx.Layout != null && floor >= 0 && floor < ctx.Layout.Floors.Count)
            {
                var job = new FormationJob();
                DungeonLayout layout = ctx.Layout;
                CaveRules r = rules;
                float rough = roughness, tex = textureScale;
                int chunk = chunkCells;
                void Work()
                {
                    try
                    {
                        if (!token.IsCancelled)
                            job.Result = CaveFormationMesher.Build(layout, floor, r, rough, chunk, tex);
                    }
                    catch (Exception e)
                    {
                        job.Error = e;
                    }
                    finally
                    {
                        job.Finish();
                    }
                }
                if (ctx.Immediate || !Application.isPlaying)
                {
                    Work();
                }
                else
                {
                    DungeonWorkers.Pool.Enqueue(Work, null, token);
                    // A cancelled job is skipped by the pool without running: stop waiting then.
                    while (!job.Done && !token.IsCancelled)
                        yield return null;
                }
                if (job.Error != null)
                    Debug.LogException(job.Error);
                else if (job.Result != null && !token.IsCancelled)
                {
                    IEnumerator making = MakeFormations(floor, job.Result);
                    while (making.MoveNext())
                        yield return making.Current;
                }
            }
            if (ctx.Replica && !replicaOpened && cave != null)
            {
                // A client's copy has no "fully built" moment (its levels arrive one by one): it opens with its first level.
                replicaOpened = true;
                cave.MarkBuilt();
            }
        }

        private IEnumerator MakeFormations(int floor, CaveFloorFormations data)
        {
            if (data == null || data.IsEmpty)
                yield break;
            Transform root = ctx.FloorChild(floor, "Formations");
            if (root == null)
                yield break;
            int layer = ctx.GeometryLayer;
            bool shadows = ctx.CastShadows;
            DungeonMaterials mats = ctx.Materials;
            Material crystal = mats != null ? mats.Flat(data.CrystalColor, 1.6f) : CaveMaterials.Flat(data.CrystalColor, 1.6f);
            foreach (CaveFloorFormations.Chunk chunk in data.Chunks)
            {
                var chunkGo = new GameObject($"Formations {chunk.Key.x},{chunk.Key.y}") { layer = layer };
                chunkGo.transform.SetParent(root, false);
                if (!chunk.Rock.IsEmpty)
                {
                    Mesh mesh = ctx.CreateMesh(chunk.Rock, chunkGo.name, out Material[] materials);
                    if (mesh != null)
                        AddRenderer(chunkGo, mesh, materials, shadows);
                }
                if (!chunk.Crystal.IsEmpty)
                {
                    Mesh mesh = ctx.CreateMesh(chunk.Crystal, chunkGo.name + " Crystals", out Material[] materials);
                    if (mesh != null)
                    {
                        var crystals = new GameObject("Crystals") { layer = layer };
                        crystals.transform.SetParent(chunkGo.transform, false);
                        var all = new Material[materials.Length];
                        for (int i = 0; i < all.Length; i++)
                            all[i] = crystal;
                        AddRenderer(crystals, mesh, all, false);
                    }
                }
                if (!chunk.Collider.IsEmpty)
                {
                    Mesh mesh = ctx.CreateMesh(chunk.Collider, chunkGo.name + " Collider", out _);
                    if (mesh != null)
                    {
                        var collider = chunkGo.AddComponent<MeshCollider>();
                        ctx.AddCollider(collider, mesh);   // cooked off the main thread, before the level's NavMesh is baked
                    }
                }
                FormationChunks++;
                if (ctx.ShouldYield())
                    yield return null;
            }
            foreach (CaveFloorFormations.GlowLight l in data.Lights)
            {
                var lightGo = new GameObject("Crystal Glow") { layer = layer };
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = l.Position;
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = l.Color;
                light.intensity = 1.6f;
                light.range = rules.Formations.grottoLightRange;
                light.shadows = LightShadows.None;
                GrottoLights++;
            }
            StandingFormations += data.Standing;
            HangingFormations += data.Hanging;
            CrystalClusters += data.Crystals;
        }

        private static void AddRenderer(GameObject go, Mesh mesh, Material[] materials, bool shadows)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ end

        public void OnFullyBuilt()
        {
            if (cave != null)
                cave.MarkBuilt();
        }

        public void Teardown()
        {
            token.Cancel();
            // What disappears from here on goes with the dungeon: nobody took it.
            if (cave != null)
                cave.StopWatching();
            // What was mined last (the work on a half-broken lump too) goes into the cave's memory.
            if (RulesSide && cave != null)
            {
                foreach (KeyValuePair<int, CaveDeposit> kv in deposits)
                {
                    OreDeposit d = kv.Value != null ? kv.Value.Deposit : null;
                    if (d == null)
                        continue;
                    OreDeposit.SavedState s = d.CaptureState();
                    if (s.taken > 0 || s.gathered > 0 || s.issued > 0 || s.work > 0f)
                        cave.KeepDeposit(kv.Key, s);
                }
            }
            // The visit is over: the cave's record may be forgotten again (once it is the one left longest ago).
            if (cave != null)
                cave.ReleaseRecord();
            foreach ((CombatEntity entity, Action<CombatEntity> handler) in deaths)
                if (entity != null)
                    entity.Died -= handler;
            deaths.Clear();
            foreach ((DungeonChest chest, Action<DungeonChest, int> handler) in chests)
                if (chest != null)
                    chest.Opened -= handler;
            chests.Clear();
            foreach (CaveDeposit adapter in deposits.Values)
                if (adapter != null)
                    adapter.Release();
            deposits.Clear();
            // Generated deposit objects leave the dungeon for their pool (prefab ones are the builder's: they keep state, so
            // its pool destroys them rather than reusing them).
            foreach (GameObject go in artDeposits)
                CaveDepositArt.Give(go);
            artDeposits.Clear();
        }
    }
}
