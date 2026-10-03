using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProceduralDungeon
{
    /// <summary>
    /// A mini-boss that roams the floor: it walks a patrol through the middle of several rooms it can reach (rooms
    /// without an event of their own), so the players may meet it anywhere. A mob with AI patrols through its
    /// <see cref="Mob"/> patrol points (and fights as usual); a stand-in walks the route itself with a NavMeshAgent.
    /// </summary>
    public class DungeonRoamer : MonoBehaviour
    {
        [Tooltip("Rooms on its route.")]
        [Min(2)] public int stops = 5;
        [Tooltip("Stand-ins only: walking speed (meters per second).")]
        [Min(0.2f)] public float speed = 2.2f;
        [Tooltip("Stand-ins only: seconds it lingers at each stop.")]
        [Min(0f)] public float linger = 3f;

        public IReadOnlyList<Vector3> Route => route;

        private readonly List<Vector3> route = new List<Vector3>();
        private NavMeshAgent agent;
        private int next;
        private float leaveAt;

        private void Start()
        {
            var tag = GetComponent<DungeonSpawned>();
            if (tag == null || tag.Dungeon == null)
                return;
            BuildRoute(tag.Dungeon, tag.floor, tag.area);
            if (route.Count < 2)
                return;
            var mob = GetComponent<Mob>();
            if (mob != null)
            {
                mob.PatrolRoute = null;
                mob.PatrolPointsRelativeToHome = false;
                mob.PatrolPoints = route.ToArray();
                return;
            }
            // A stand-in: walk the route itself.
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
                agent = gameObject.AddComponent<NavMeshAgent>();
            agent.speed = speed;
            agent.radius = 0.5f;
            agent.height = 2f;
            if (agent.isOnNavMesh)
                agent.SetDestination(route[next = 1]);
        }

        private void Update()
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || route.Count < 2)
                return;
            if (agent.pathPending || agent.remainingDistance > 0.6f)
                return;
            if (leaveAt <= 0f)
            {
                leaveAt = Time.time + linger;
                return;
            }
            if (Time.time < leaveAt)
                return;
            leaveAt = 0f;
            next = (next + 1) % route.Count;
            agent.SetDestination(route[next]);
        }

        /// <summary>The middle of its own room, then rooms it can walk to, nearest first along a loop.</summary>
        private void BuildRoute(DungeonInstance d, int floor, int ownArea)
        {
            route.Clear();
            route.Add(transform.position);
            FloorLayout f = d.Layout.Floors[floor];
            var candidates = new List<(Vector3 pos, float dist)>();
            var path = new NavMeshPath();
            foreach (Area a in f.Areas)
            {
                if (a.Id == ownArea || a.CenterCell < 0 || a.Kind == AreaKind.Corridor || a.Cells.Count < 12 || d.HasRoomEvent(floor, a.Id))
                    continue;
                if (a.Role == AreaRole.Vault || a.Role == AreaRole.Secret || a.Role == AreaRole.Puzzle)
                    continue;
                Vector3 p = d.CellToWorld(floor, new Vector2Int(f.Grid.X(a.CenterCell), f.Grid.Y(a.CenterCell)));
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;
                if (!NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
                candidates.Add((hit.position, (hit.position - transform.position).sqrMagnitude));
            }
            candidates.Sort((x, y) => x.dist.CompareTo(y.dist));
            // A handful spread out: alternate near and far so the route sweeps the floor.
            for (int i = 0; i < candidates.Count && route.Count < stops; i += Mathf.Max(1, candidates.Count / stops))
                route.Add(candidates[i].pos);
        }
    }
}
