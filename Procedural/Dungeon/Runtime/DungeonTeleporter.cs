using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// One end of a portal link between two areas of a floor (astral platforms, a gallery's magic painting): a pad you
    /// stand on, or a painting you step into. After a moment the player is sent to the other end (the teleporter of the
    /// same floor with the same <see cref="DungeonSpawned.link"/>), and can't be sent back for a short while.
    /// </summary>
    public class DungeonTeleporter : MonoBehaviour
    {
        public enum Style { Pad, Painting }

        [Tooltip("Pad: stand on it. Painting: stand in front of it (it hangs on a wall).")]
        public Style style = Style.Pad;
        [Tooltip("Seconds a player must stay on it before being sent.")]
        [Min(0f)] public float delay = 0.6f;
        [Tooltip("How close a player must be (meters).")]
        [Min(0.3f)] public float radius = 0.9f;
        [Tooltip("Seconds before the same player can use a teleporter again.")]
        [Min(0f)] public float cooldown = 2.5f;

        private static readonly Dictionary<CombatEntity, float> readyAt = new Dictionary<CombatEntity, float>();
        private readonly Dictionary<CombatEntity, float> held = new Dictionary<CombatEntity, float>();
        private DungeonSpawned tag;
        private DungeonTeleporter partner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => readyAt.Clear();

        /// <summary>Where a player arrives: on the pad, or a step in front of the painting.</summary>
        public Vector3 Arrival => style == Style.Pad ? transform.position + Vector3.up * 0.15f : transform.position + transform.forward * 1.3f + Vector3.up * 0.1f;

        /// <summary>The spot players stand on to use it.</summary>
        private Vector3 Spot => style == Style.Pad ? transform.position : transform.position + transform.forward * 0.8f;

        /// <summary>The other end (the same pair id on the same floor), or null.</summary>
        public DungeonTeleporter Partner
        {
            get
            {
                if (partner != null)
                    return partner;
                if (tag == null)
                    tag = GetComponent<DungeonSpawned>();
                if (tag == null || tag.Dungeon == null)
                    return null;
                foreach (DungeonSpawned s in tag.Dungeon.Spawned(PlacementKind.Teleporter))
                    if (s != tag && s.link == tag.link && s.floor == tag.floor)
                        return partner = s.GetComponent<DungeonTeleporter>();
                return null;
            }
        }

        private void Update()
        {
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive)
                    continue;
                Vector3 d = p.transform.position - Spot;
                bool on = Mathf.Abs(d.y) < 1.6f && new Vector2(d.x, d.z).sqrMagnitude <= radius * radius;
                if (!on || (readyAt.TryGetValue(p, out float t) && Time.time < t))
                {
                    held.Remove(p);
                    continue;
                }
                held.TryGetValue(p, out float h);
                h += Time.deltaTime;
                held[p] = h;
                if (h >= delay)
                {
                    held.Remove(p);
                    Send(p);
                }
            }
        }

        public bool Send(CombatEntity p)
        {
            DungeonTeleporter other = Partner;
            if (p == null || other == null)
                return false;
            readyAt[p] = Time.time + cooldown;
            Quaternion facing = other.style == Style.Painting ? other.transform.rotation : p.transform.rotation;
            DungeonSession.Teleport(p.gameObject, other.Arrival, facing);
            if (p.PlayerMovement != null)
                p.PlayerMovement.VerticalVelocity = 0f;
            if (style == Style.Painting)
                DungeonMessages.Show("You step through the painting...");
            return true;
        }
    }
}
