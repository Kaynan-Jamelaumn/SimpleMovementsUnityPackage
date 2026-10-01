using System;
using UnityEngine;
using UnityEngine.AI;

namespace ProceduralDungeon
{
    /// <summary>
    /// A wall section that hides a passage. It slides into the floor when the player stays close to it for
    /// <see cref="holdTime"/> seconds (searching the wall), or when <see cref="Open"/> is called (a lever, a key, a
    /// spell). Mobs path through once it's open (it carves the NavMesh only while closed).
    /// </summary>
    public class DungeonSecretDoor : MonoBehaviour
    {
        [Tooltip("How close a player must stand to the wall to search it (meters). Any player can find it (multiplayer-safe).")]
        [Min(0f)] public float searchDistance = 1.8f;
        [Tooltip("Seconds the player must stay that close before the wall opens.")]
        [Min(0f)] public float holdTime = 1.5f;
        [Tooltip("How fast the wall sinks into the floor (meters per second).")]
        [Min(0.1f)] public float openSpeed = 1.2f;

        public bool IsOpen { get; private set; }

        public event Action<DungeonSecretDoor> Opened;

        private float held;
        private Vector3 closed;
        private float height = 3f;
        private NavMeshObstacle obstacle;

        private void Start()
        {
            closed = transform.localPosition;
            var col = GetComponent<Collider>();
            if (col != null)
                height = col.bounds.size.y + 0.1f;
            obstacle = GetComponent<NavMeshObstacle>();
        }

        private void Update()
        {
            if (IsOpen)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, closed + Vector3.down * height, openSpeed * Time.deltaTime);
                return;
            }
            // Any living player searching the wall (not "the object tagged Player": several players may be inside).
            bool searching = PlayerLocator.AnyWithin(transform.position, searchDistance);
            held = searching ? held + Time.deltaTime : Mathf.Max(0f, held - Time.deltaTime);
            if (held >= holdTime)
                Open();
        }

        public void Open()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            if (obstacle != null)
                obstacle.enabled = false;
            Opened?.Invoke(this);
        }
    }
}
