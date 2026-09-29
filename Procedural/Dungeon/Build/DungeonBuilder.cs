using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProceduralDungeon
{
    /// <summary>
    /// Creates a dungeon's scene objects from its layout and mesh data, on the main thread, a few milliseconds per
    /// frame: per floor the generated geometry (or the theme's tile kit), stairs and drops, prefab rooms, secret
    /// doors, portals, props and loot; then each floor's NavMesh (built in the background, joined across floors by
    /// NavMeshLinks on the stairs) and finally the mobs, placed on the NavMesh. The dungeon is reported playable as
    /// soon as the first floors are done; deeper floors keep building behind the player.
    /// </summary>
    public sealed class DungeonBuilder
    {
        public sealed class Settings
        {
            /// <summary>No frame budget, synchronous NavMesh (editor preview, tests).</summary>
            public bool Immediate;
            public DungeonPortal.PortalAction ExitAction = DungeonPortal.PortalAction.CompleteDungeon;
            /// <summary>Floors that must be complete (with NavMesh and mobs) before the dungeon counts as playable.</summary>
            public int PlayableFloors = 2;
            public Action<float, string> Progress;
            public Action<DungeonInstance> Playable;
        }

        private readonly CompiledProfile profile;
        private readonly DungeonLayout layout;
        private readonly List<FloorMeshData> meshes;
        private readonly DungeonObjectPool pool;
        private readonly DungeonMaterials materials;
        private readonly DungeonPrimitives primitives;
        private readonly List<Mesh> createdMeshes = new List<Mesh>();
        private readonly List<NavMeshData> createdNavMeshes = new List<NavMeshData>();
        private readonly List<GameObject> pooledInstances = new List<GameObject>();
        // The current floor's collider meshes, cooked on job threads while the rest of the floor is built.
        private readonly MeshColliderBaker.Batch colliderBakes = new MeshColliderBaker.Batch();
        private readonly Stopwatch watch = new Stopwatch();
        private Settings settings;
        private GameObject root;
        private Transform[] floorRoots;
        private NavMeshSurface[] surfaces;
        private AsyncOperation[] bakes;
        private int stepsDone, stepsTotal;

        public DungeonInstance Instance { get; private set; }

        public DungeonBuilder(CompiledProfile profile, DungeonLayout layout, List<FloorMeshData> meshes, DungeonObjectPool pool)
        {
            this.profile = profile;
            this.layout = layout;
            this.meshes = meshes;
            this.pool = pool ?? new DungeonObjectPool();
            materials = new DungeonMaterials(profile.Theme);
            primitives = new DungeonPrimitives(materials, profile.Theme);
        }

        private BuildSettings B => profile.Build;

        private bool Tick()
        {
            if (settings.Immediate)
                return false;
            if (watch.Elapsed.TotalMilliseconds < Mathf.Max(0.5f, B.frameBudgetMs))
                return false;
            watch.Restart();
            return true;
        }

        private void Report(string what)
        {
            stepsDone++;
            settings.Progress?.Invoke(stepsTotal > 0 ? Mathf.Clamp01(stepsDone / (float)stepsTotal) : 1f, what);
        }

        // ------------------------------------------------------------------ build

        public IEnumerator Build(Transform parent, Settings s)
        {
            settings = s ?? new Settings();
            watch.Restart();
            int floors = layout.Floors.Count;
            stepsTotal = floors * 3 + 1;
            string label = string.IsNullOrEmpty(layout.Request?.label) ? $"Dungeon {layout.Seed}" : layout.Request.label;

            root = new GameObject(label);
            root.transform.SetParent(parent, false);
            floorRoots = new Transform[floors];
            surfaces = new NavMeshSurface[floors];
            bakes = new AsyncOperation[floors];
            for (int f = 0; f < floors; f++)
            {
                var go = new GameObject($"Floor {f} ({layout.Floors[f].Spec.Style})");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, layout.Floors[f].Spec.BaseY, 0f);
                go.layer = B.geometryLayer;
                floorRoots[f] = go.transform;
            }
            Instance = root.AddComponent<DungeonInstance>();
            Instance.Initialize(layout, profile, floorRoots);

            int playable = Mathf.Clamp(settings.PlayableFloors, 1, floors);
            bool reported = false;
            var mobsDone = new bool[floors];
            for (int f = 0; f < floors; f++)
            {
                IEnumerator floorBuild = BuildFloor(f);
                while (floorBuild.MoveNext())
                    yield return floorBuild.Current;
                if (B.bakeNavMesh)
                    StartBake(f);
                Report($"Floor {f} built");

                if (!reported && f == playable - 1)
                {
                    IEnumerator ready = FinishFloors(0, f, mobsDone);
                    while (ready.MoveNext())
                        yield return ready.Current;
                    reported = true;
                    AddStreamer();
                    settings.Playable?.Invoke(Instance);
                }
            }

            IEnumerator rest = FinishFloors(0, floors - 1, mobsDone);
            while (rest.MoveNext())
                yield return rest.Current;
            if (!reported)
            {
                AddStreamer();
                settings.Playable?.Invoke(Instance);
            }
            Report("Done");
            Instance.MarkFullyBuilt();
        }

        /// <summary>Waits for the NavMesh of floors first..last and places their mobs.</summary>
        private IEnumerator FinishFloors(int first, int last, bool[] mobsDone)
        {
            for (int f = first; f <= last; f++)
            {
                if (mobsDone[f])
                    continue;
                while (bakes[f] != null && !bakes[f].isDone)
                    yield return null;
                IEnumerator mobs = SpawnMobs(f);
                while (mobs.MoveNext())
                    yield return mobs.Current;
                mobsDone[f] = true;
                Report($"Floor {f} populated");
            }
        }

        private void AddStreamer()
        {
            var streamer = root.GetComponent<DungeonFloorStreamer>();
            if (streamer == null)
                streamer = root.AddComponent<DungeonFloorStreamer>();
            streamer.streaming = B.streamFloors;
            streamer.floorsAround = Mathf.Max(1, B.floorsAround);
        }

        private IEnumerator BuildFloor(int f)
        {
            FloorLayout floor = layout.Floors[f];
            Transform fr = floorRoots[f];
            Transform geometry = Child(fr, "Geometry");

            // Generated geometry.
            FloorMeshData data = meshes[f];
            foreach (MeshBuffers chunk in data.Chunks)
            {
                CreateMeshObject($"Chunk {chunk.Chunk.x},{chunk.Chunk.y}", geometry, chunk, null);
                if (Tick())
                    yield return null;
            }

            // Stairs and drops that end on this floor.
            foreach (LinkMeshData link in data.Links)
            {
                VerticalLink vl = layout.Links[link.LinkId];
                GameObject go = CreateMeshObject($"{vl.Kind} {vl.Id} (from floor {vl.UpperFloor})", Child(fr, "Links"), link.Visual, link.Collider);
                if (B.bakeNavMesh)
                {
                    var nl = go.AddComponent<NavMeshLink>();
                    nl.agentTypeID = B.navMeshAgentTypeId;
                    nl.startPoint = link.LinkStart;
                    nl.endPoint = link.LinkEnd;
                    nl.width = link.LinkWidth;
                    nl.bidirectional = vl.Kind == LinkKind.Stairs;
                    nl.UpdateLink();
                }
                if (Tick())
                    yield return null;
            }

            // Tile kit.
            if (profile.HasTileKit && B.useTileKit && profile.Theme != null)
            {
                IEnumerator kit = BuildTileKit(floor, Child(fr, "Tiles"));
                while (kit.MoveNext())
                    yield return kit.Current;
            }

            BuildPrefabRooms(floor, Child(fr, "Rooms"));
            BuildDoors(floor, Child(fr, "Doors"));
            if (Tick())
                yield return null;

            // Portals, props and loot (mobs come after the NavMesh).
            Transform props = Child(fr, "Props");
            for (int i = 0; i < layout.Placements.Count; i++)
            {
                Placement p = layout.Placements[i];
                if (p.Floor != f || p.Kind == PlacementKind.Mob || p.Kind == PlacementKind.Boss)
                    continue;
                SpawnPlacement(p, i, props);
                if (Tick())
                    yield return null;
            }

            // The floor's colliders (their NavMesh is baked from them next): the cooking has been running on job
            // threads meanwhile, so this usually doesn't wait at all.
            while (!settings.Immediate && !colliderBakes.IsCompleted)
                yield return null;
            colliderBakes.Finish();
            Report($"Floor {f} objects");
        }

        // ------------------------------------------------------------------ meshes

        private static Transform Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null)
                return t;
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private Mesh ToMesh(MeshBuffers b, string name, out Material[] mats)
        {
            var mesh = new Mesh { name = name };
            if (b.Vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.Vertices);
            mesh.SetNormals(b.Normals);
            mesh.SetUVs(0, b.Uvs);
            var used = new List<int>();
            for (int s = 0; s < b.Triangles.Length; s++)
                if (b.Triangles[s].Count > 0)
                    used.Add(s);
            mesh.subMeshCount = Mathf.Max(1, used.Count);
            mats = new Material[used.Count];
            for (int i = 0; i < used.Count; i++)
            {
                mesh.SetTriangles(b.Triangles[used[i]], i, false);
                mats[i] = materials.BySurface[used[i]];
            }
            mesh.RecalculateBounds();
            if (profile.Theme != null)
                mesh.RecalculateTangents();
            createdMeshes.Add(mesh);
            return mesh;
        }

        private GameObject CreateMeshObject(string name, Transform parent, MeshBuffers visual, MeshBuffers collider)
        {
            var go = new GameObject(name);
            go.layer = B.geometryLayer;
            go.transform.SetParent(parent, false);
            Mesh mesh = ToMesh(visual, name, out Material[] mats);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = mats;
            renderer.shadowCastingMode = B.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            // Cooked on a job thread with the collider's cooking options and assigned at the end of the floor
            // (see BuildFloor): nothing is cooked on the main thread, and no "missing pre-baked collision" warning.
            var mc = go.AddComponent<MeshCollider>();
            colliderBakes.Add(mc, collider != null && !collider.IsEmpty ? ToMesh(collider, name + " Collider", out _) : mesh);
#if UNITY_EDITOR
            if (!Application.isPlaying && B.markStatic)
                go.isStatic = true;
#endif
            return go;
        }

        // ------------------------------------------------------------------ tile kit, prefab rooms, doors

        private IEnumerator BuildTileKit(FloorLayout floor, Transform parent)
        {
            DungeonTheme t = profile.Theme;
            List<TileKitItem> items = TileKitPlanner.Plan(floor, new TileKitPlanner.Options
            {
                CellSize = layout.CellSize,
                ModuleSize = profile.TileKit.ModuleSize,
                WallPrefabHeight = profile.TileKit.WallPrefabHeight,
                ScaleWallsToCeiling = profile.TileKit.ScaleWallsToCeiling,
                Ceilings = B.buildCeilings,
                DoorFrames = B.doorFrames,
            });
            foreach (TileKitItem item in items)
            {
                GameObject prefab;
                switch (item.Piece)
                {
                    case TileKitPiece.Floor: prefab = t.floorTile; break;
                    case TileKitPiece.Wall: prefab = t.wallSegment; break;
                    case TileKitPiece.Ceiling: prefab = t.ceilingTile; break;
                    case TileKitPiece.DoorFrame: prefab = t.doorFrame; break;
                    default: prefab = t.pillar; break;
                }
                if (prefab == null)
                    continue;
                GameObject go = pool.Get(prefab, parent, item.Position, Quaternion.Euler(0f, item.Yaw, 0f));
                go.transform.localScale = Vector3.Scale(prefab.transform.localScale, item.Scale);
                pooledInstances.Add(go);
                if (Tick())
                    yield return null;
            }
        }

        private void BuildPrefabRooms(FloorLayout floor, Transform parent)
        {
            float cs = layout.CellSize;
            TileGrid g = floor.Grid;
            foreach (Area area in floor.Areas)
            {
                if (area.TemplateIndex < 0 || !area.TemplateApplied)
                    continue;
                TemplateInfo t = profile.Templates[area.TemplateIndex];
                if (!t.IsPrefab || t.Source == null || t.Source.prefab == null)
                    continue;
                RoomTemplate src = t.Source;
                Vector2Int o = area.TemplateOrigin;
                Vector3 pos;
                switch (src.pivot)
                {
                    case RoomTemplate.Pivot.SouthWestCorner: pos = new Vector3(o.x * cs, 0f, o.y * cs); break;
                    case RoomTemplate.Pivot.NorthWestCorner: pos = new Vector3(o.x * cs, 0f, (o.y + t.Height) * cs); break;
                    default: pos = new Vector3((o.x + t.Width * 0.5f) * cs, 0f, (o.y + t.Height * 0.5f) * cs); break;
                }
                GameObject go = pool.Get(src.prefab, parent, pos + src.prefabOffset, Quaternion.identity);
                go.name = $"{src.name} (area {area.Id})";
                pooledInstances.Add(go);

                var used = new bool[t.Sockets.Count];
                for (int i = 0; i < t.Sockets.Count; i++)
                {
                    Vector3Int sck = t.Sockets[i];
                    var front = new Vector2Int(o.x + sck.x, o.y + sck.y) + ((Dir4)sck.z).Delta();
                    used[i] = g.InBounds(front) && g.IsWalkable(g.Index(front));
                }

                if (t.Legacy)
                {
                    // Sockets are North, South, East, West - RoomBehaviour's wall order.
                    var legacy = go.GetComponent<RoomBehaviour>();
                    if (legacy != null)
                        legacy.UpdateRoom(used);
                }
                else if (src.sockets != null)
                {
                    for (int i = 0; i < src.sockets.Count && i < used.Length; i++)
                    {
                        RoomTemplate.Socket sck = src.sockets[i];
                        SetChildActive(go.transform, sck.openChild, used[i]);
                        SetChildActive(go.transform, sck.closedChild, !used[i]);
                    }
                }
            }
        }

        private static void SetChildActive(Transform root, string path, bool active)
        {
            if (string.IsNullOrEmpty(path))
                return;
            Transform t = root.Find(path);
            if (t != null)
                t.gameObject.SetActive(active);
        }

        private void BuildDoors(FloorLayout floor, Transform parent)
        {
            TileGrid g = floor.Grid;
            float cs = layout.CellSize;
            DungeonTheme theme = profile.Theme;
            for (int i = 0; i < g.Count; i++)
            {
                if (g.Type[i] != CellType.Door || g.Has(i, CellFlags.Prefab))
                    continue;
                int x = i % g.Width, y = i / g.Width;
                bool passageNS = g.IsWalkable(g.Neighbor(i, 0)) && g.IsWalkable(g.Neighbor(i, 2));
                var center = new Vector3((x + 0.5f) * cs, g.FloorHeight[i], (y + 0.5f) * cs);
                Quaternion rot = Quaternion.Euler(0f, passageNS ? 0f : 90f, 0f);

                if (g.Has(i, CellFlags.Secret))
                {
                    GameObject door;
                    float height = g.CeilingHeight[i] - g.FloorHeight[i] + 0.2f;
                    if (theme != null && theme.secretDoor != null)
                    {
                        door = pool.Get(theme.secretDoor, parent, center, rot);
                        pooledInstances.Add(door);
                    }
                    else
                    {
                        door = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        door.name = "Secret Door";
                        door.transform.SetParent(parent, false);
                        door.transform.localPosition = center + Vector3.up * (height * 0.5f - 0.05f);
                        door.transform.localRotation = rot;
                        door.transform.localScale = new Vector3(cs * 1.02f, height, cs * 0.9f);
                        door.GetComponent<Renderer>().sharedMaterial = materials.BySurface[(int)DungeonSurface.BuiltWall];
                    }
                    if (door.GetComponent<DungeonSecretDoor>() == null)
                        door.AddComponent<DungeonSecretDoor>();
                    // (Explicit null checks: in the editor GetComponent returns a "fake null" that ?? doesn't see.)
                    var modifier = door.GetComponent<NavMeshModifier>();
                    if (modifier == null)
                        modifier = door.AddComponent<NavMeshModifier>();
                    modifier.ignoreFromBuild = true;
                    var obstacle = door.GetComponent<NavMeshObstacle>();
                    if (obstacle == null)
                        obstacle = door.AddComponent<NavMeshObstacle>();
                    obstacle.carving = true;
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.size = Vector3.one;
                    continue;
                }

                if (theme != null && theme.door != null)
                {
                    GameObject leaf = pool.Get(theme.door, parent, center, rot);
                    pooledInstances.Add(leaf);
                }
            }
        }

        // ------------------------------------------------------------------ placements

        private GameObject SpawnPlacement(Placement p, int index, Transform parent)
        {
            float cs = layout.CellSize;
            var local = new Vector3(p.Cell.x * cs, p.Height, p.Cell.y * cs);
            Quaternion rot = Quaternion.Euler(0f, p.Yaw, 0f);
            GameObject prefab = null;
            GameObject go = null;
            string entryName = p.Kind.ToString();
            DungeonTheme theme = profile.Theme;

            switch (p.Kind)
            {
                case PlacementKind.PlayerSpawn:
                    go = new GameObject("Player Spawn");
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = local;
                    go.transform.localRotation = rot;
                    return go;
                case PlacementKind.EntrancePortal:
                case PlacementKind.ExitPortal:
                {
                    bool entrance = p.Kind == PlacementKind.EntrancePortal;
                    prefab = theme != null ? (entrance ? theme.entrancePortal : theme.exitPortal) : null;
                    go = prefab != null
                        ? pool.Get(prefab, parent, local, rot)
                        : Primitive(entrance ? DungeonPrimitive.EntrancePortal : DungeonPrimitive.ExitPortal, entrance ? "Entrance Portal" : "Exit Portal", Color.white, 0f, 0, parent, local, rot);
                    var portal = go.GetComponent<DungeonPortal>();
                    if (portal == null)
                        portal = go.AddComponent<DungeonPortal>();
                    portal.action = entrance ? DungeonPortal.PortalAction.ReturnToWorld : settings.ExitAction;
                    break;
                }
                case PlacementKind.Loot:
                {
                    LootInfo e = profile.Loot[p.Entry];
                    entryName = e.Name;
                    prefab = e.Prefab;
                    go = prefab != null ? pool.Get(prefab, parent, local, rot) : Primitive(e.Placeholder, e.Name, Color.white, 0f, e.Tier, parent, local, rot);
                    break;
                }
                default:
                {
                    if (p.Table != PlacementTable.Props)
                        return null;
                    PropInfo e = profile.Props[p.Entry];
                    entryName = e.Name;
                    prefab = e.Prefab;
                    go = prefab != null ? pool.Get(prefab, parent, local, rot) : Primitive(e.Placeholder, e.Name, e.LightColor, e.LightRange, index, parent, local, rot);
                    if (p.Scale > 0f && Mathf.Abs(p.Scale - 1f) > 0.001f)
                        go.transform.localScale = go.transform.localScale * p.Scale;
                    break;
                }
            }

            if (go == null)
                return null;
            if (prefab != null)
                pooledInstances.Add(go);
            Tag(go, p, index, entryName, prefab);
            return go;
        }

        private GameObject Primitive(DungeonPrimitive kind, string name, Color light, float range, int variant, Transform parent, Vector3 local, Quaternion rot)
        {
            GameObject go = primitives.Create(kind, name, light, range, variant);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            return go;
        }

        private void Tag(GameObject go, Placement p, int index, string entryName, GameObject prefab)
        {
            var tag = go.GetComponent<DungeonSpawned>();
            if (tag == null)
                tag = go.AddComponent<DungeonSpawned>();
            tag.kind = p.Kind;
            tag.floor = p.Floor;
            tag.area = p.Area;
            tag.tier = p.Tier;
            tag.group = p.Group;
            tag.placementIndex = index;
            tag.entryName = entryName;
            tag.SourcePrefab = prefab;
            Instance.Register(tag);
        }

        private IEnumerator SpawnMobs(int f)
        {
            Transform parent = Child(floorRoots[f], "Mobs");
            float cs = layout.CellSize;
            for (int i = 0; i < layout.Placements.Count; i++)
            {
                Placement p = layout.Placements[i];
                if (p.Floor != f || (p.Kind != PlacementKind.Mob && p.Kind != PlacementKind.Boss) || p.Table != PlacementTable.Encounters)
                    continue;
                EncounterInfo e = profile.Encounters[p.Entry];
                if (!e.HasPrefab && !profile.Population.placeholderMobs)
                    continue;
                SpawnMob(e, p, i, parent, cs);
                if (Tick())
                    yield return null;
            }
        }

        /// <summary>Creates one mob for a placement (also used by the respawn director).</summary>
        internal GameObject SpawnMob(EncounterInfo e, Placement p, int index, Transform parent, float cs)
        {
            Vector3 world = parent.parent.TransformPoint(new Vector3(p.Cell.x * cs, p.Height, p.Cell.y * cs));
            if (B.bakeNavMesh && NavMesh.SamplePosition(world, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                world = hit.position;
            Quaternion rot = parent.rotation * Quaternion.Euler(0f, p.Yaw, 0f);
            GameObject go;
            if (e.HasPrefab && e.Prefab != null)
            {
                go = Object.Instantiate(e.Prefab, world, rot, parent);
                var agent = go.GetComponent<NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                    agent.Warp(world);
            }
            else
            {
                go = primitives.Create(e.Placeholder, e.Name, Color.white, 0f, index);
                go.transform.SetParent(parent, true);
                go.transform.SetPositionAndRotation(world, rot);
            }
            Tag(go, p, index, e.Name, null);
            return go;
        }

        // ------------------------------------------------------------------ navmesh

        private void StartBake(int f)
        {
            Transform fr = floorRoots[f];
            var s = fr.gameObject.AddComponent<NavMeshSurface>();
            s.agentTypeID = B.navMeshAgentTypeId;
            s.collectObjects = CollectObjects.Children;
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = ~0;
            surfaces[f] = s;
            if (settings.Immediate)
            {
                s.BuildNavMesh();
                if (s.navMeshData != null)
                    createdNavMeshes.Add(s.navMeshData);
                return;
            }
            Physics.SyncTransforms();
            var data = new NavMeshData(s.agentTypeID)
            {
                name = fr.name + " NavMesh",
                position = fr.position,
                rotation = fr.rotation,
            };
            createdNavMeshes.Add(data);
            s.navMeshData = data;
            s.AddData();
            bakes[f] = s.UpdateNavMesh(data);
        }

        // ------------------------------------------------------------------ teardown

        /// <summary>Removes everything this builder created (pooled prefab instances go back to the pool).</summary>
        public void Teardown()
        {
            // A cancelled build may still be cooking meshes that are destroyed below.
            colliderBakes.Cancel();
            foreach (GameObject go in pooledInstances)
                if (go != null)
                    pool.Release(go);
            pooledInstances.Clear();
            if (root != null)
                DungeonMaterials.Destroy(root);
            root = null;
            foreach (Mesh m in createdMeshes)
                DungeonMaterials.Destroy(m);
            createdMeshes.Clear();
            foreach (NavMeshData d in createdNavMeshes)
                DungeonMaterials.Destroy(d);
            createdNavMeshes.Clear();
            materials.Dispose();
        }
    }
}
