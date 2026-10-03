using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

namespace ProceduralDungeon
{
    /// <summary>
    /// Something that blocks a doorway and sinks into the floor to open it: room gates, vault doors, shortcut doors. While
    /// closed its collider blocks and its NavMeshObstacle carves the NavMesh (mobs path round it); open, neither does. It
    /// never rises while someone stands in it. The NavMesh is baked without it (NavMeshModifier: ignore from build).
    /// </summary>
    public abstract class DungeonBarrier : MonoBehaviour
    {
        [Tooltip("How far it sinks to open (meters). 0 = its collider's height.")]
        [Min(0f)] public float travel;
        [Tooltip("Opening / closing speed (meters per second).")]
        [Min(0.1f)] public float speed = 3f;

        public bool IsClosed { get; private set; }

        private Vector3 raised;
        private bool initialized;
        private Collider[] colliders;
        private NavMeshObstacle obstacle;
        private DungeonSpawned tag;

        /// <summary>The dungeon this barrier belongs to (from its DungeonSpawned).</summary>
        protected DungeonInstance Dungeon => tag != null ? tag.Dungeon : (tag = GetComponent<DungeonSpawned>()) != null ? tag.Dungeon : null;

        /// <summary>The key id / area it belongs to (DungeonSpawned.link).</summary>
        protected int Link => tag != null || (tag = GetComponent<DungeonSpawned>()) != null ? tag.link : 0;

        /// <summary>Sets the starting state at once (no animation).</summary>
        protected void Initialize(bool closed)
        {
            if (initialized)
                return;
            initialized = true;
            raised = transform.localPosition;
            colliders = GetComponentsInChildren<Collider>(true);
            if (travel <= 0f)
            {
                // Sink by the height of what blocks (its solid colliders), so it disappears under the floor.
                float tallest = 0f;
                foreach (Collider c in colliders)
                    if (!c.isTrigger)
                        tallest = Mathf.Max(tallest, c.bounds.size.y);
                travel = tallest > 0f ? tallest + 0.1f : 3f;
            }
            PrepareForNavMesh(gameObject);
            obstacle = GetComponent<NavMeshObstacle>();
            IsClosed = closed;
            transform.localPosition = closed ? raised : raised + Vector3.down * travel;
            Apply(closed);
        }

        /// <summary>
        /// Keeps a barrier out of the NavMesh bake and gives it an obstacle the size of its collider (carving while closed).
        /// The builder calls it as soon as the barrier exists, before the floor's NavMesh is baked.
        /// </summary>
        public static void PrepareForNavMesh(GameObject go)
        {
            // (Explicit null checks: in the editor GetComponent returns a "fake null" that ?? doesn't see.)
            var modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                return;
            obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            var box = go.GetComponent<BoxCollider>();
            if (box != null)
            {
                obstacle.center = box.center;
                obstacle.size = box.size;
            }
        }

        /// <summary>Closes or opens it (animated).</summary>
        public void SetClosed(bool closed)
        {
            Initialize(closed);
            IsClosed = closed;
        }

        protected virtual void Update()
        {
            if (!initialized)
                return;
            Vector3 target = raised + (IsClosed ? Vector3.zero : Vector3.down * travel);
            // Never rise into someone standing in the doorway: wait until it's clear.
            if (IsClosed && transform.localPosition != target && Occupied())
                return;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
            bool blocking = IsClosed && (transform.localPosition - target).sqrMagnitude < 0.5f * 0.5f + (travel * 0.5f) * (travel * 0.5f) &&
                            transform.localPosition.y > raised.y - travel * 0.5f;
            Apply(blocking);
        }

        private void Apply(bool blocking)
        {
            if (colliders != null)
                foreach (Collider c in colliders)
                    if (c != null && !c.isTrigger)
                        c.enabled = blocking;
            if (obstacle != null)
                obstacle.enabled = blocking;
        }

        /// <summary>A character (player or mob) stands in the doorway.</summary>
        private bool Occupied()
        {
            foreach (CombatEntity e in CombatEntity.All)
            {
                if (e == null || !e.IsAlive)
                    continue;
                Vector3 d = e.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 3f && new Vector2(d.x, d.z).sqrMagnitude < 0.8f * 0.8f)
                    return true;
            }
            return false;
        }
    }
}
