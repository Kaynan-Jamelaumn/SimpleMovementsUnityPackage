using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A built dungeon in the scene, and the one place gameplay code asks about it: entrance, exit and player spawn,
    /// floors, areas and their roles, walkability, depth along the dungeon, the placements the generator decided and
    /// the objects it spawned. Gameplay (doors, encounters, bosses, quests) reads this - it never calls the generator.
    /// </summary>
    public class DungeonInstance : MonoBehaviour
    {
        public DungeonLayout Layout { get; private set; }
        public CompiledProfile Profile { get; private set; }
        public DungeonManager Manager { get; internal set; }

        public float CellSize => Layout.CellSize;
        public int FloorCount => Layout.Floors.Count;
        public int Seed => Layout.Seed;

        /// <summary>Where the player starts (world space).</summary>
        public Pose PlayerSpawn => ToWorld(Layout.PlayerSpawn);
        public Pose EntrancePortal => ToWorld(Layout.EntrancePortal);
        public Pose ExitPortal => ToWorld(Layout.ExitPortal);

        /// <summary>The floor the player is on (kept up to date by <see cref="DungeonFloorStreamer"/>).</summary>
        public int CurrentFloor { get; private set; }

        /// <summary>All floors built (Ready may fire earlier, as soon as the start floors are playable).</summary>
        public bool IsFullyBuilt { get; private set; }

        public event Action<int> FloorEntered;
        public event Action<DungeonInstance> FullyBuilt;

        private Transform[] floorRoots;
        private readonly Dictionary<PlacementKind, List<DungeonSpawned>> spawned = new Dictionary<PlacementKind, List<DungeonSpawned>>();
        private readonly Dictionary<long, List<DungeonSpawned>> byArea = new Dictionary<long, List<DungeonSpawned>>();
        private readonly HashSet<int> populatedFloors = new HashSet<int>();
        private readonly HashSet<long> eventAreas = new HashSet<long>();
        private readonly Dictionary<long, DungeonSecretDoor> secretDoors = new Dictionary<long, DungeonSecretDoor>();
        private readonly HashSet<int> mappedFloors = new HashSet<int>();

        /// <summary>Makes a stand-in mob for an encounter without a prefab (set by the builder).</summary>
        internal Func<EncounterInfo, int, GameObject> PlaceholderMob;

        /// <summary>A floor's map was revealed (a map table): floor.</summary>
        public event Action<int> FloorMapped;

        /// <summary>What happened in this run: kills, chests, keys, secrets, puzzles, cleared rooms...</summary>
        public DungeonRunStats Stats { get; } = new DungeonRunStats();

        /// <summary>A room's event finished: (floor, area) - a locked room cleared, an ambush beaten, a puzzle solved.</summary>
        public event Action<int, int> RoomCompleted;

        private static long AreaKey(int floor, int area) => ((long)floor << 32) | (uint)area;

        internal void Initialize(DungeonLayout layout, CompiledProfile profile, Transform[] floors)
        {
            Layout = layout;
            Profile = profile;
            floorRoots = floors;
            Stats.startedAt = Time.time;
            foreach (Placement p in layout.Placements)
                if (p.Kind == PlacementKind.RoomController)
                    eventAreas.Add(AreaKey(p.Floor, p.Area));
        }

        public Transform FloorRoot(int floor) => floorRoots != null && floor >= 0 && floor < floorRoots.Length ? floorRoots[floor] : null;

        // ------------------------------------------------------------------ coordinates

        /// <summary>World position of a point on a floor (cell units, cell centre = x + 0.5) at a height above the floor's base.</summary>
        public Vector3 CellToWorld(int floor, Vector2 cell, float height)
        {
            return transform.TransformPoint(Layout.ToLocal(floor, cell, height));
        }

        /// <summary>World position of a cell's centre, on its floor surface.</summary>
        public Vector3 CellToWorld(int floor, Vector2Int cell)
        {
            TileGrid g = Layout.Floors[floor].Grid;
            float h = g.InBounds(cell) ? g.FloorHeight[g.Index(cell)] : 0f;
            return CellToWorld(floor, new Vector2(cell.x + 0.5f, cell.y + 0.5f), h);
        }

        public Pose ToWorld(DungeonPose pose)
        {
            if (!pose.Valid)
                return new Pose(transform.position, transform.rotation);
            return new Pose(CellToWorld(pose.Floor, pose.Cell, pose.Height), transform.rotation * Quaternion.Euler(0f, pose.Yaw, 0f));
        }

        /// <summary>
        /// The floor whose height band contains a world position: a floor's band reaches down to the top of the floor
        /// below (its tallest ceiling), so a chasm's depths still belong to the floor they cut through. Floors may be
        /// spaced differently.
        /// </summary>
        public int FloorAt(Vector3 world)
        {
            float y = transform.InverseTransformPoint(world).y;
            for (int f = 0; f < FloorCount - 1; f++)
            {
                FloorSpec below = Layout.Floors[f + 1].Spec;
                float top = Mathf.Min(below.BaseY + below.MaxCeiling, Layout.Floors[f].Spec.BaseY - 0.5f);
                if (y >= top)
                    return f;
            }
            return FloorCount - 1;
        }

        /// <summary>The cell under a world position (on the floor whose band it's in). False outside the grid.</summary>
        public bool WorldToCell(Vector3 world, out int floor, out Vector2Int cell)
        {
            floor = FloorAt(world);
            Vector3 local = transform.InverseTransformPoint(world);
            cell = new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.z / CellSize));
            return Layout.Floors[floor].Grid.InBounds(cell);
        }

        // ------------------------------------------------------------------ queries

        public bool IsWalkable(Vector3 world)
        {
            if (!WorldToCell(world, out int floor, out Vector2Int cell))
                return false;
            TileGrid g = Layout.Floors[floor].Grid;
            return g.IsWalkable(g.Index(cell));
        }

        public bool TryGetArea(Vector3 world, out Area area)
        {
            area = null;
            if (!WorldToCell(world, out int floor, out Vector2Int cell))
                return false;
            FloorLayout f = Layout.Floors[floor];
            area = f.AreaAt(f.Grid.Index(cell));
            return area != null;
        }

        /// <summary>Areas with a role (on one floor, or all floors with -1).</summary>
        public List<Area> AreasWithRole(AreaRole role, int floor = -1)
        {
            var list = new List<Area>();
            foreach (FloorLayout f in Layout.Floors)
            {
                if (floor >= 0 && f.Index != floor)
                    continue;
                foreach (Area a in f.Areas)
                    if (a.Role == role)
                        list.Add(a);
            }
            return list;
        }

        /// <summary>Areas with a custom tag (e.g. "Library").</summary>
        public List<Area> AreasWithTag(string tag, int floor = -1)
        {
            var list = new List<Area>();
            foreach (FloorLayout f in Layout.Floors)
            {
                if (floor >= 0 && f.Index != floor)
                    continue;
                foreach (Area a in f.Areas)
                    if (a.Tag == tag)
                        list.Add(a);
            }
            return list;
        }

        /// <summary>Walking distance (cells) from the dungeon entrance to a world position along the main path; -1 if unreachable.</summary>
        public int PathDepth(Vector3 world)
        {
            if (!WorldToCell(world, out int floor, out Vector2Int cell))
                return -1;
            FloorLayout f = Layout.Floors[floor];
            int d = f.DistanceFromArrival[f.Grid.Index(cell)];
            return d < 0 ? -1 : f.GlobalDistanceOffset + d;
        }

        /// <summary>How far along the dungeon (0 entrance .. 1 exit) a world position is, by its area.</summary>
        public float Progress(Vector3 world) => TryGetArea(world, out Area a) ? a.Progress : 0f;

        /// <summary>Placements the generator decided (of a kind, on a floor or all with -1).</summary>
        public List<Placement> Placements(PlacementKind kind, int floor = -1)
        {
            var list = new List<Placement>();
            foreach (Placement p in Layout.Placements)
                if (p.Kind == kind && (floor < 0 || p.Floor == floor))
                    list.Add(p);
            return list;
        }

        /// <summary>World position and rotation of a placement.</summary>
        public Pose PlacementPose(Placement p) => new Pose(CellToWorld(p.Floor, p.Cell, p.Height), transform.rotation * Quaternion.Euler(0f, p.Yaw, 0f));

        /// <summary>Spawned objects of a kind that still exist.</summary>
        public List<DungeonSpawned> Spawned(PlacementKind kind)
        {
            var list = new List<DungeonSpawned>();
            if (spawned.TryGetValue(kind, out List<DungeonSpawned> all))
                foreach (DungeonSpawned s in all)
                    if (s != null)
                        list.Add(s);
            return list;
        }

        /// <summary>Spawned objects of one area (mobs, loot, props, gates, plates...) that still exist, hidden ones included.</summary>
        public List<DungeonSpawned> SpawnedIn(int floor, int area)
        {
            var list = new List<DungeonSpawned>();
            if (byArea.TryGetValue(AreaKey(floor, area), out List<DungeonSpawned> all))
                foreach (DungeonSpawned s in all)
                    if (s != null)
                        list.Add(s);
            return list;
        }

        /// <summary>The floor's mobs have been placed (its NavMesh is baked).</summary>
        public bool IsFloorPopulated(int floor) => populatedFloors.Contains(floor);

        /// <summary>The area runs an event (locked room, ambush, puzzle): respawns stay out of it.</summary>
        public bool HasRoomEvent(int floor, int area) => eventAreas.Contains(AreaKey(floor, area));

        /// <summary>The modifier of a floor (flooded, molten...).</summary>
        public FloorModifier ModifierOf(int floor) => floor >= 0 && floor < FloorCount ? Layout.Floors[floor].Spec.Modifier : FloorModifier.None;

        /// <summary>The secret door in a cell of a floor (cell index), or null.</summary>
        public DungeonSecretDoor SecretDoorAt(int floor, int cell) => secretDoors.TryGetValue(AreaKey(floor, cell), out DungeonSecretDoor d) ? d : null;

        internal void RegisterSecretDoor(int floor, int cell, DungeonSecretDoor door) => secretDoors[AreaKey(floor, cell)] = door;

        /// <summary>Has a floor's map been revealed (by its map table)?</summary>
        public bool IsMapped(int floor) => mappedFloors.Contains(floor);

        /// <summary>Reveals a floor's map (the map overlay shows it from now on).</summary>
        public void RevealMap(int floor)
        {
            if (floor < 0 || floor >= FloorCount || !mappedFloors.Add(floor))
                return;
            FloorMapped?.Invoke(floor);
        }

        /// <summary>
        /// Spawns a mob of an encounter at a world position, tagged and registered like the dungeon's own (nests, respawns):
        /// its prefab, or a stand-in when it has none. Null when it has neither.
        /// </summary>
        public GameObject SpawnMob(EncounterInfo e, int floor, int area, int tier, Vector3 world, float yaw)
        {
            if (e == null)
                return null;
            Transform floorRoot = FloorRoot(floor);
            Transform parent = floorRoot != null ? floorRoot.Find("Mobs") : null;
            if (parent == null)
                parent = floorRoot != null ? floorRoot : transform;
            if (UnityEngine.AI.NavMesh.SamplePosition(world, out UnityEngine.AI.NavMeshHit hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                world = hit.position;
            Quaternion rot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            GameObject go;
            if (e.HasPrefab && e.Prefab != null)
            {
                go = Instantiate(e.Prefab, world, rot, parent);
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isActiveAndEnabled)
                    agent.Warp(world);
            }
            else if (PlaceholderMob != null)
            {
                go = PlaceholderMob(e, UnityEngine.Random.Range(0, 1000));
                go.transform.SetParent(parent, true);
                go.transform.SetPositionAndRotation(world, rot);
            }
            else
            {
                return null;
            }
            var tag = go.GetComponent<DungeonSpawned>();
            if (tag == null)
                tag = go.AddComponent<DungeonSpawned>();
            tag.kind = PlacementKind.Mob;
            tag.floor = floor;
            tag.area = area;
            tag.tier = tier;
            tag.entryName = e.Name;
            tag.placementIndex = -1;
            Register(tag);
            return go;
        }

        /// <summary>Every vertical link (stairs and drops) with its world-space landing positions.</summary>
        public IReadOnlyList<VerticalLink> Links => Layout.Links;

        // ------------------------------------------------------------------ internal

        internal void Register(DungeonSpawned s)
        {
            s.Dungeon = this;
            if (!spawned.TryGetValue(s.kind, out List<DungeonSpawned> list))
                spawned[s.kind] = list = new List<DungeonSpawned>();
            list.Add(s);
            if (s.area >= 0)
            {
                long key = AreaKey(s.floor, s.area);
                if (!byArea.TryGetValue(key, out List<DungeonSpawned> inArea))
                    byArea[key] = inArea = new List<DungeonSpawned>();
                inArea.Add(s);
            }
            if (s.kind == PlacementKind.Mob || s.kind == PlacementKind.Boss)
            {
                CombatEntity e = s.GetComponentInChildren<CombatEntity>(true);
                if (e != null)
                {
                    bool elite = s.elite, boss = s.kind == PlacementKind.Boss;
                    e.Died += _ =>
                    {
                        Stats.Bump(ref Stats.mobsKilled);
                        if (elite) Stats.Bump(ref Stats.elitesKilled);
                        if (boss) Stats.Bump(ref Stats.bossesKilled);
                    };
                }
            }
        }

        internal void MarkFloorPopulated(int floor) => populatedFloors.Add(floor);

        internal void RaiseRoomCompleted(int floor, int area) => RoomCompleted?.Invoke(floor, area);

        private void OnDestroy() => DungeonKeyRing.Clear(this);

        internal void SetCurrentFloor(int floor)
        {
            if (floor == CurrentFloor)
                return;
            CurrentFloor = floor;
            FloorEntered?.Invoke(floor);
        }

        internal void MarkFullyBuilt()
        {
            IsFullyBuilt = true;
            FullyBuilt?.Invoke(this);
        }

        private void OnDrawGizmosSelected()
        {
            if (Layout == null)
                return;
            Gizmos.color = Color.green;
            Pose spawn = PlayerSpawn;
            Gizmos.DrawWireSphere(spawn.position + Vector3.up, 0.5f);
            Gizmos.DrawRay(spawn.position + Vector3.up, spawn.forward * 1.5f);
            Gizmos.color = Color.cyan;
            foreach (VerticalLink link in Layout.Links)
            {
                Vector3 a = CellToWorld(link.UpperFloor, link.UpperLanding);
                Vector3 b = CellToWorld(link.LowerFloor, link.LowerLanding);
                Gizmos.DrawLine(a + Vector3.up, b + Vector3.up);
            }
        }
    }
}
