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

        internal void Initialize(DungeonLayout layout, CompiledProfile profile, Transform[] floors)
        {
            Layout = layout;
            Profile = profile;
            floorRoots = floors;
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

        /// <summary>The floor whose height band contains a world position.</summary>
        public int FloorAt(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            int floor = Mathf.FloorToInt((-local.y + Layout.FloorSpacing * 0.5f) / Layout.FloorSpacing);
            return Mathf.Clamp(floor, 0, FloorCount - 1);
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

        /// <summary>Every vertical link (stairs and drops) with its world-space landing positions.</summary>
        public IReadOnlyList<VerticalLink> Links => Layout.Links;

        // ------------------------------------------------------------------ internal

        internal void Register(DungeonSpawned s)
        {
            s.Dungeon = this;
            if (!spawned.TryGetValue(s.kind, out List<DungeonSpawned> list))
                spawned[s.kind] = list = new List<DungeonSpawned>();
            list.Add(s);
        }

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
