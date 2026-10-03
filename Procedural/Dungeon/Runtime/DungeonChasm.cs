using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The chasm of an islands or astral floor (on the floor's root). By default a black fog fills it: a player who falls
    /// off a ledge or a bridge dies as soon as they touch the fog (<see cref="fogTop"/>). With the profile's Chasm Fall
    /// Rule set to Floor Below, a faller lands instead on the floor below (the walkable spot nearest to where they fell,
    /// taking a share of their health as damage); with nothing below they are put back where they last stood safely.
    /// </summary>
    public class DungeonChasm : MonoBehaviour
    {
        [Tooltip("What a fall does.")]
        public ChasmFall fall = ChasmFall.Death;
        [Tooltip("Height of the black fog's surface below the floor's base (meters, negative); touching it kills. NaN = no fog on this floor.")]
        public float fogTop = float.NaN;
        [Tooltip("Share of max health lost when landing on the floor below (or put back on the ledge).")]
        [Range(0f, 1f)] public float fallDamage = 0.2f;
        [Tooltip("How far below the ledges a player must fall before the fall is resolved (meters; sooner where the chasm is shallower).")]
        [Min(1f)] public float fallDepth = 4f;

        private DungeonInstance dungeon;
        private int floor;
        private readonly Dictionary<CombatEntity, Vector3> safe = new Dictionary<CombatEntity, Vector3>();
        private float nextSafeCheck;

        public void Setup(DungeonInstance owner, int floorIndex, ChasmFall mode, float damage, float fog)
        {
            dungeon = owner;
            floor = floorIndex;
            fall = mode;
            fallDamage = damage;
            fogTop = fog;
        }

        /// <summary>Does this floor's chasm kill (its fog)?</summary>
        public bool Kills => !float.IsNaN(fogTop);

        private void Update()
        {
            if (dungeon == null)
                dungeon = GetComponentInParent<DungeonInstance>();
            if (dungeon == null || dungeon.Layout == null)
                return;
            bool recordSafe = Time.time >= nextSafeCheck;
            if (recordSafe)
                nextSafeCheck = Time.time + 0.25f;
            float baseY = dungeon.Layout.Floors[floor].Spec.BaseY;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive)
                    continue;
                if (dungeon.FloorAt(p.transform.position) != floor)
                    continue;
                Vector3 local = dungeon.transform.InverseTransformPoint(p.transform.position);
                TileGrid g = dungeon.Layout.Floors[floor].Grid;
                var cell = new Vector2Int(Mathf.FloorToInt(local.x / dungeon.CellSize), Mathf.FloorToInt(local.z / dungeon.CellSize));
                if (!g.InBounds(cell))
                    continue;
                int i = g.Index(cell);
                float ground = baseY + (g.IsChasm(i) ? 0f : g.FloorHeight[i]);
                bool grounded = p.Controller == null || p.Controller.isGrounded;
                if (recordSafe && grounded && g.IsWalkable(i) && Mathf.Abs(local.y - ground) < 0.6f)
                    safe[p] = p.transform.position;
                bool chasm = g.IsChasm(i), bridge = g.IsBridge(i);
                if (!chasm && !bridge)
                    continue;
                // Under a bridge counts as soon as the faller is well below its deck.
                float limit;
                if (Kills)
                    limit = chasm ? Mathf.Max(fogTop, g.FloorHeight[i] + 0.5f) : Mathf.Min(fogTop, g.FloorHeight[i] - 1.2f);
                else
                    limit = chasm ? Mathf.Max(g.FloorHeight[i] + 0.5f, -fallDepth) : g.FloorHeight[i] - Mathf.Max(1.5f, fallDepth);
                if (local.y < baseY + limit)
                    Resolve(p);
            }
        }

        private void Resolve(CombatEntity p)
        {
            bool last = floor >= dungeon.FloorCount - 1;
            if (Kills)
            {
                p.ApplyDamage(new DamageInfo { amount = p.MaxHealth * 10f + 1000f, target = p, point = p.transform.position, direction = Vector3.down, type = DamageType.True });
                safe.Remove(p);
                DungeonMessages.Show("The black fog swallows you.", true);
                return;
            }
            Vector3 to;
            string message;
            if (!last && LandingBelow(p.transform.position, out to))
            {
                message = "You fall to the floor below!";
            }
            else
            {
                to = safe.TryGetValue(p, out Vector3 s) ? s : dungeon.PlayerSpawn.position;
                message = "You climb back out of the chasm, bruised.";
            }
            DungeonSession.Teleport(p.gameObject, to + Vector3.up * 0.3f, p.transform.rotation);
            if (p.PlayerMovement != null)
                p.PlayerMovement.VerticalVelocity = 0f;
            if (fallDamage > 0f)
                p.ApplyDamage(new DamageInfo { amount = p.MaxHealth * fallDamage, target = p, point = to, direction = Vector3.down, type = DamageType.True });
            DungeonMessages.Show(message, true);
        }

        /// <summary>The walkable cell of the floor below nearest under a position (searching outwards in rings).</summary>
        private bool LandingBelow(Vector3 world, out Vector3 landing)
        {
            landing = world;
            int below = floor + 1;
            TileGrid g = dungeon.Layout.Floors[below].Grid;
            Vector3 local = dungeon.transform.InverseTransformPoint(world);
            int cx = Mathf.FloorToInt(local.x / dungeon.CellSize), cy = Mathf.FloorToInt(local.z / dungeon.CellSize);
            for (int r = 0; r < Mathf.Max(g.Width, g.Height); r++)
            {
                int best = -1;
                float bestSq = float.MaxValue;
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r || !g.InBounds(cx + dx, cy + dy))
                            continue;
                        int i = g.Index(cx + dx, cy + dy);
                        if (!g.IsWalkable(i) || g.IsChasm(i) || g.Has(i, CellFlags.Locked | CellFlags.Secret))
                            continue;
                        float d = dx * dx + dy * dy;
                        if (d < bestSq)
                        {
                            bestSq = d;
                            best = i;
                        }
                    }
                if (best >= 0)
                {
                    landing = dungeon.CellToWorld(below, new Vector2Int(g.X(best), g.Y(best)));
                    return true;
                }
            }
            return false;
        }
    }
}
