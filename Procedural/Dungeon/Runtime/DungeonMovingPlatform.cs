using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A platform that floats back and forth across a chasm along its track (waypoints set by the builder, in its
    /// parent's space), waiting a moment at each end. Players standing on it are carried with it. Mobs don't ride it:
    /// the NavMesh doesn't cross the chasm.
    /// </summary>
    public class DungeonMovingPlatform : MonoBehaviour
    {
        [Tooltip("Travel speed (meters per second).")]
        [Min(0.2f)] public float speed = 2.2f;
        [Tooltip("Seconds it waits at each end.")]
        [Min(0f)] public float wait = 1.8f;
        [Tooltip("Half the size of the deck (meters, x and z).")]
        public Vector2 halfSize = new Vector2(0.9f, 0.9f);
        [Tooltip("Height of the deck's top above the platform's pivot (meters).")]
        public float deckHeight = 0f;
        [Tooltip("The track (local to the parent). Set by the builder; the platform starts at the first point.")]
        public List<Vector3> waypoints = new List<Vector3>();

        private int target = 1, step = 1;
        private float waitUntil;

        private void Start()
        {
            if (waypoints.Count > 0)
                transform.localPosition = waypoints[0];
            waitUntil = Time.time + wait;
        }

        private void LateUpdate()
        {
            if (waypoints.Count < 2 || Time.time < waitUntil)
                return;
            Vector3 before = transform.position;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, waypoints[target], speed * Time.deltaTime);
            Vector3 delta = transform.position - before;
            if (delta.sqrMagnitude > 0f)
                Carry(before, delta);
            if ((transform.localPosition - waypoints[target]).sqrMagnitude < 1e-4f)
            {
                if (target + step < 0 || target + step >= waypoints.Count)
                {
                    step = -step;
                    waitUntil = Time.time + wait;
                }
                target += step;
            }
        }

        /// <summary>Moves every player standing on the deck (before the move) along with it.</summary>
        private void Carry(Vector3 before, Vector3 delta)
        {
            float top = before.y + deckHeight;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive || p.Controller == null || !p.Controller.enabled)
                    continue;
                Vector3 feet = p.transform.position;
                Vector3 local = Quaternion.Inverse(transform.rotation) * (feet - before);
                if (Mathf.Abs(local.x) > halfSize.x + 0.3f || Mathf.Abs(local.z) > halfSize.y + 0.3f || feet.y < top - 0.35f || feet.y > top + 0.6f)
                    continue;
                p.Controller.Move(delta);
            }
        }
    }
}
