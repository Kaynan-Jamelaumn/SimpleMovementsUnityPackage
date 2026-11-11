using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// A Soul Sanctuary (design spec 15.1): a place where rune users' souls recover faster. Inside it rest and meditation
    /// recover Multiplier times faster, standing in it out of combat recovers a little by itself, and a sanctuary can also
    /// heal deep damage slowly. Where several sanctuaries overlap, the strongest counts.
    /// <para>The area is a sphere of <see cref="Radius"/> around the object, or the <see cref="Area"/> collider when one is
    /// set (a box, sphere, capsule or convex mesh; it does not need to be a trigger). Runs where the rules run.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways] // known in edit mode too (tools, Edit Mode tests); it has no per-frame work
    [AddComponentMenu("SimpleMovements/Rune Magic/Soul Sanctuary")]
    public class SoulSanctuary : MonoBehaviour
    {
        [Tooltip("Shown to the player.")]
        [SerializeField] private string displayName = "Soul Sanctuary";
        [Tooltip("Metres around the object (when no Area is set).")]
        [SerializeField, Min(0.5f)] private float radius = 8f;
        [Tooltip("Optional: the sanctuary is this collider's volume instead of the radius (box, sphere, capsule or convex mesh).")]
        [SerializeField] private Collider area;

        [Header("Recovery")]
        [Tooltip("Use the database's Sanctuary Multiplier and Sanctuary Passive Per Minute (off: the values below).")]
        [SerializeField] private bool useDatabaseValues = true;
        [Tooltip("Rest and meditation recover this many times faster here.")]
        [SerializeField, Min(0f)] private float multiplier = 2f;
        [Tooltip("Soul Integrity recovered per minute just by being here out of combat.")]
        [SerializeField, Min(0f)] private float passivePerMinute = 1f;
        [Tooltip("Deep damage healed per minute here, out of combat (0 = none: most sanctuaries cannot mend deep damage).")]
        [SerializeField, Min(0f)] private float deepHealPerMinute = 0f;

        private static readonly List<SoulSanctuary> active = new List<SoulSanctuary>();

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public float Radius => radius;
        public Collider Area => area;
        public float DeepHealPerMinute => deepHealPerMinute;

        /// <summary>Every enabled sanctuary.</summary>
        public static IReadOnlyList<SoulSanctuary> Active => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
        }

        private void OnDisable() => active.Remove(this);

        /// <summary>The recovery multiplier here.</summary>
        public float MultiplierFor(SoulRecoveryBalance r) => useDatabaseValues && r != null ? r.sanctuaryMultiplier : multiplier;

        /// <summary>Recovery per minute just for being here.</summary>
        public float PassivePerMinuteFor(SoulRecoveryBalance r) => useDatabaseValues && r != null ? r.sanctuaryPassivePerMinute : passivePerMinute;

        /// <summary>Is <paramref name="point"/> inside the sanctuary?</summary>
        public bool Contains(Vector3 point)
        {
            if (area != null && area.enabled && area.gameObject.activeInHierarchy)
            {
                Vector3 closest = area.ClosestPoint(point);
                return (closest - point).sqrMagnitude < 0.0001f;
            }
            return (point - transform.position).sqrMagnitude <= radius * radius;
        }

        /// <summary>The strongest sanctuary containing <paramref name="point"/> (null when none).</summary>
        public static SoulSanctuary Find(Vector3 point)
        {
            SoulRecoveryBalance r = active.Count > 0 ? RuneMagicDatabase.Instance.Catalog.Balance.soulRecovery : null;
            SoulSanctuary best = null;
            float bestValue = float.NegativeInfinity;
            for (int i = 0; i < active.Count; i++)
            {
                SoulSanctuary s = active[i];
                if (s == null || !s.Contains(point)) continue;
                float value = s.MultiplierFor(r) * 1000f + s.PassivePerMinuteFor(r) + s.deepHealPerMinute;
                if (value > bestValue)
                {
                    bestValue = value;
                    best = s;
                }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.45f, 0.86f, 0.82f, 0.5f);
            if (area == null)
                Gizmos.DrawWireSphere(transform.position, radius);
            else
                Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
        }
    }
}
