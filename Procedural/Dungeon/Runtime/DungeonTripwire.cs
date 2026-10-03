using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A thin wire across a corridor (along its local X). A player crossing it - unless sneaking carefully over it
    /// (crouched) - makes the arrow launchers of its group (the <see cref="DungeonDartTrap"/>s whose
    /// <see cref="DungeonSpawned.link"/> is the wire's) shoot across the corridor. It re-arms after a while.
    /// </summary>
    public class DungeonTripwire : MonoBehaviour
    {
        [Tooltip("Half the wire's length (meters).")]
        [Min(0.2f)] public float halfLength = 1f;
        [Tooltip("Seconds before it can fire again.")]
        [Min(0.5f)] public float rearm = 5f;
        [Tooltip("Crouched players step over it without setting it off.")]
        public bool sneakOver = true;

        public event System.Action<DungeonTripwire> Tripped;

        private float readyAt;
        private DungeonSpawned tag;

        private void Update()
        {
            if (Time.time < readyAt)
                return;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive || (sneakOver && p.IsSneaking))
                    continue;
                Vector3 local = transform.InverseTransformPoint(p.transform.position);
                if (Mathf.Abs(local.x) <= halfLength + 0.3f && Mathf.Abs(local.z) < 0.35f && local.y > -0.5f && local.y < 1.2f)
                {
                    Trip();
                    return;
                }
            }
        }

        public void Trip()
        {
            readyAt = Time.time + rearm;
            if (tag == null)
                tag = GetComponent<DungeonSpawned>();
            Tripped?.Invoke(this);
            if (tag == null || tag.Dungeon == null)
                return;
            int fired = 0;
            foreach (DungeonSpawned s in tag.Dungeon.Spawned(PlacementKind.ArrowLauncher))
            {
                if (s.floor != tag.floor || s.link != tag.link)
                    continue;
                var trap = s.GetComponent<DungeonDartTrap>();
                if (trap != null)
                {
                    trap.Trigger();
                    fired++;
                }
            }
            if (fired > 0)
                DungeonMessages.Show("*click*");
        }
    }
}
