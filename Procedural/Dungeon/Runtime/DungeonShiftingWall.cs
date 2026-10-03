using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A wall that rises out of the floor to close a passage of one of the floor's loops, and sinks again - moved by
    /// the floor's <see cref="DungeonShiftingFloor"/>, which reshuffles which loops are shut every few minutes while
    /// keeping every room reachable. Like every barrier it never rises into someone standing in it.
    /// </summary>
    public class DungeonShiftingWall : DungeonBarrier
    {
        /// <summary>The connection whose passage it closes (DungeonSpawned.link).</summary>
        public int ConnectionId => Link;

        private void Start() => Initialize(false);

        /// <summary>Somebody (player or mob) is near: the floor waits before shutting this one.</summary>
        public bool Busy
        {
            get
            {
                foreach (CombatEntity e in CombatEntity.All)
                {
                    if (e == null || !e.IsAlive)
                        continue;
                    Vector3 d = e.transform.position - transform.position;
                    if (Mathf.Abs(d.y) < 3f && new Vector2(d.x, d.z).sqrMagnitude < 1.6f * 1.6f)
                        return true;
                }
                return false;
            }
        }
    }
}
