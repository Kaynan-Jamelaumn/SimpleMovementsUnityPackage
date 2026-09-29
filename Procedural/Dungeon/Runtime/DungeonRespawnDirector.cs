using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Optional: brings mobs back over time, the replacement for the old DungeonMobSpawner's timer. It reuses the
    /// generator's own mob placements as spawn points (so respawns keep the same rules: never near the entrance,
    /// never in doorways), only on the player's floor, never within <see cref="minDistance"/> of the player or in view
    /// of the camera, and never above the number of mobs the floor started with. Deaths are counted through
    /// <see cref="DungeonSpawned.Destroyed"/>. Put it on the DungeonManager.
    /// </summary>
    public class DungeonRespawnDirector : MonoBehaviour
    {
        [Tooltip("REQUIRED. The DungeonManager whose dungeons get respawns (usually the one on this object).")]
        public DungeonManager manager;
        [Tooltip("Seconds between respawn attempts (random between x and y).")]
        public Vector2 interval = new Vector2(20f, 45f);
        [Tooltip("At most this share of a floor's original mobs is alive at once through respawns.")]
        [Range(0f, 1f)] public float maxShareAlive = 0.75f;
        [Tooltip("Never respawn closer than this to the player (meters).")]
        [Min(0f)] public float minDistance = 25f;
        [Tooltip("Never respawn where the main camera can see.")]
        public bool avoidCameraView = true;
        [Tooltip("Tag used to find the player.")]
        public string playerTag = "Player";

        private float next;
        private Transform player;
        private readonly Dictionary<int, int> startCount = new Dictionary<int, int>();
        private DungeonInstance tracked;
        private readonly System.Random random = new System.Random();

        private void Awake()
        {
            if (manager == null)
                manager = GetComponent<DungeonManager>();
        }

        private void Update()
        {
            DungeonInstance d = manager != null ? manager.Current : null;
            if (d == null || !d.IsFullyBuilt)
                return;
            if (d != tracked)
            {
                tracked = d;
                startCount.Clear();
                foreach (Placement p in d.Layout.Placements)
                    if (p.Kind == PlacementKind.Mob)
                        startCount[p.Floor] = (startCount.TryGetValue(p.Floor, out int c) ? c : 0) + 1;
                next = Time.time + Next();
            }
            if (Time.time < next)
                return;
            next = Time.time + Next();

            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag(playerTag);
                player = p != null ? p.transform : null;
                if (player == null)
                    return;
            }

            int floor = d.CurrentFloor;
            int alive = 0;
            foreach (DungeonSpawned s in d.Spawned(PlacementKind.Mob))
                if (s.floor == floor)
                    alive++;
            if (!startCount.TryGetValue(floor, out int start) || alive >= Mathf.CeilToInt(start * maxShareAlive))
                return;

            // A spawn point away from the player and out of view.
            List<Placement> points = d.Placements(PlacementKind.Mob, floor);
            Camera cam = Camera.main;
            Plane[] frustum = avoidCameraView && cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;
            for (int attempt = 0; attempt < 8 && points.Count > 0; attempt++)
            {
                int index = random.Next(points.Count);
                Placement p = points[index];
                Vector3 world = d.CellToWorld(p.Floor, p.Cell, p.Height);
                if ((world - player.position).sqrMagnitude < minDistance * minDistance)
                    continue;
                if (frustum != null && GeometryUtility.TestPlanesAABB(frustum, new Bounds(world + Vector3.up, Vector3.one * 2f)))
                    continue;
                Respawn(d, p);
                return;
            }
        }

        private float Next() => Mathf.Lerp(interval.x, interval.y, (float)random.NextDouble());

        private void Respawn(DungeonInstance d, Placement p)
        {
            if (p.Entry < 0 || p.Entry >= d.Profile.Encounters.Count)
                return;
            EncounterInfo e = d.Profile.Encounters[p.Entry];
            if (!e.HasPrefab || e.Prefab == null)
                return;   // placeholders aren't respawned
            Transform floorRoot = d.FloorRoot(p.Floor);
            Transform parent = floorRoot != null ? floorRoot.Find("Mobs") : d.transform;
            Vector3 world = d.CellToWorld(p.Floor, p.Cell, p.Height);
            if (UnityEngine.AI.NavMesh.SamplePosition(world, out UnityEngine.AI.NavMeshHit hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas))
                world = hit.position;
            GameObject go = Instantiate(e.Prefab, world, Quaternion.Euler(0f, p.Yaw, 0f), parent);
            var tag = go.GetComponent<DungeonSpawned>();
            if (tag == null)
                tag = go.AddComponent<DungeonSpawned>();
            tag.kind = PlacementKind.Mob;
            tag.floor = p.Floor;
            tag.area = p.Area;
            tag.tier = p.Tier;
            tag.entryName = e.Name;
            tag.placementIndex = -1;
            d.Register(tag);
        }
    }
}
