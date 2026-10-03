using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A floor whose connections reshuffle (on the floor's root, when it has <see cref="DungeonShiftingWall"/>s): every
    /// few minutes it decides again which walls stand and which sink. A wall that is the last way between two parts of
    /// the floor always sinks - every room stays reachable (secret doors don't count as ways) - and a wall with someone
    /// next to it is left as it is.
    /// </summary>
    public class DungeonShiftingFloor : MonoBehaviour
    {
        [Tooltip("Seconds between reshuffles (random in this range).")]
        public Vector2 interval = new Vector2(90f, 180f);
        [Tooltip("Chance that a wall that may close does close.")]
        [Range(0f, 1f)] public float closeChance = 0.6f;

        public event System.Action<DungeonShiftingFloor> Shifted;

        private DungeonInstance dungeon;
        private int floor;
        private float nextShift;
        private readonly List<DungeonShiftingWall> walls = new List<DungeonShiftingWall>();

        public void Setup(DungeonInstance owner, int floorIndex, Vector2 shiftInterval)
        {
            dungeon = owner;
            floor = floorIndex;
            interval = shiftInterval;
        }

        private void Start() => nextShift = Time.time + 2f;

        private void Update()
        {
            if (dungeon == null)
                dungeon = GetComponentInParent<DungeonInstance>();
            if (dungeon == null || Time.time < nextShift || !dungeon.IsFloorPopulated(floor))
                return;
            bool first = walls.Count == 0;
            if (first)
                foreach (DungeonSpawned s in dungeon.Spawned(PlacementKind.ShiftingWall))
                {
                    var w = s.floor == floor ? s.GetComponent<DungeonShiftingWall>() : null;
                    if (w != null)
                        walls.Add(w);
                }
            nextShift = Time.time + Random.Range(interval.x, Mathf.Max(interval.x, interval.y));
            Shift();
            if (!first)
                foreach (CombatEntity p in CombatEntity.Players)
                    if (p != null && p.IsAlive && dungeon.FloorAt(p.transform.position) == floor)
                    {
                        DungeonMessages.Show("Stone grinds in the distance: the walls are shifting.");
                        break;
                    }
        }

        /// <summary>Decides again which walls stand (a random spanning choice that keeps the floor connected).</summary>
        public void Shift()
        {
            walls.RemoveAll(w => w == null);
            if (walls.Count == 0 || dungeon == null)
                return;
            FloorLayout f = dungeon.Layout.Floors[floor];
            var shifting = new HashSet<int>();
            foreach (DungeonShiftingWall w in walls)
                shifting.Add(w.ConnectionId);

            var parent = new int[f.Areas.Count];
            for (int i = 0; i < parent.Length; i++)
                parent[i] = i;
            foreach (Connection c in f.Connections)
                if (!c.Failed && c.Kind != ConnectionKind.Secret && !shifting.Contains(c.Id) && Valid(c, parent.Length))
                    Union(parent, c.A, c.B);

            var order = new List<DungeonShiftingWall>(walls);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            foreach (DungeonShiftingWall w in order)
            {
                int id = w.ConnectionId;
                Connection c = id >= 0 && id < f.Connections.Count ? f.Connections[id] : null;
                if (c == null || !Valid(c, parent.Length))
                {
                    w.SetClosed(false);
                    continue;
                }
                bool needed = Find(parent, c.A) != Find(parent, c.B);
                bool close = !needed && (w.Busy ? w.IsClosed : Random.value < closeChance);
                w.SetClosed(close);
                if (!close)
                    Union(parent, c.A, c.B);
            }
            Shifted?.Invoke(this);
        }

        private static bool Valid(Connection c, int areas) => c.A >= 0 && c.B >= 0 && c.A < areas && c.B < areas;

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
                x = parent[x] = parent[parent[x]];
            return x;
        }

        private static void Union(int[] parent, int a, int b) => parent[Find(parent, a)] = Find(parent, b);
    }
}
