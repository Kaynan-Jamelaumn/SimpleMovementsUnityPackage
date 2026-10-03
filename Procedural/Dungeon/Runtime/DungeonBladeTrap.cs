using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A pendulum blade (or a swinging log) hanging from the ceiling. Its child "Pivot" swings around its local Z axis
    /// (sideways) or X axis (back and forth, logs swinging down a corridor); anything the blade (the pivot's child
    /// "Blade") sweeps through is hit - players by default - once per swing, and pushed along the swing.
    /// </summary>
    public class DungeonBladeTrap : MonoBehaviour
    {
        public enum SwingAxis { Sideways, ForwardBack }

        [Tooltip("Sideways: swings across its forward (blades). Forward Back: swings along it (logs down a corridor).")]
        public SwingAxis axis = SwingAxis.Sideways;
        [Tooltip("Push given to whoever is hit (meters per second, players only).")]
        [Min(0f)] public float knockback;
        [Tooltip("Kinds of characters the blade hurts.")]
        public EntityKinds affects = EntityKinds.Players;
        [Tooltip("Damage per hit.")]
        [Min(0f)] public float damage = 18f;
        [Tooltip("Swing amplitude (degrees either side).")]
        [Range(5f, 85f)] public float amplitude = 55f;
        [Tooltip("Seconds for a full swing there and back.")]
        [Min(0.3f)] public float period = 2.6f;
        [Tooltip("Shifts this blade's rhythm (seconds).")]
        public float phaseOffset;
        [Tooltip("Size of the blade's hit box (meters).")]
        public Vector3 bladeSize = new Vector3(1.6f, 0.35f, 0.15f);

        private Transform pivot, blade;
        private readonly Dictionary<CombatEntity, float> lastHit = new Dictionary<CombatEntity, float>();
        private readonly Collider[] hits = new Collider[16];

        private void Start()
        {
            pivot = transform.Find("Pivot");
            blade = pivot != null ? pivot.Find("Blade") : null;
        }

        private void Update()
        {
            if (pivot == null)
                return;
            float angle = amplitude * Mathf.Sin((Time.time + phaseOffset) * Mathf.PI * 2f / period);
            pivot.localRotation = axis == SwingAxis.Sideways ? Quaternion.Euler(0f, 0f, angle) : Quaternion.Euler(angle, 0f, 0f);
        }

        private void FixedUpdate()
        {
            if (blade == null)
                return;
            int n = Physics.OverlapBoxNonAlloc(blade.position, bladeSize * 0.5f, hits, blade.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                CombatEntity e = CombatEntity.Resolve(hits[i]);
                if (e == null || !e.IsAlive || (affects & KindOf(e)) == 0)
                    continue;
                if (lastHit.TryGetValue(e, out float t) && Time.time - t < period * 0.45f)
                    continue;
                lastHit[e] = Time.time;
                // The way the blade is moving: along its swing, toward where it is heading.
                // (The blade hangs below the pivot: a growing angle moves it along +right sideways, along -forward forward-back.)
                float rate = Mathf.Cos((Time.time + phaseOffset) * Mathf.PI * 2f / period);
                Vector3 swing = (axis == SwingAxis.Sideways ? transform.right : -transform.forward) * (rate >= 0f ? 1f : -1f);
                e.ApplyDamage(new DamageInfo
                {
                    amount = damage,
                    target = e,
                    point = hits[i].ClosestPoint(blade.position),
                    direction = swing,
                    type = DamageType.Physical,
                });
                if (knockback > 0f && e.Controller != null && e.Controller.enabled)
                {
                    e.Controller.Move(swing * knockback * 0.12f);
                    if (e.PlayerMovement != null)
                        e.PlayerMovement.VerticalVelocity = Mathf.Max(e.PlayerMovement.VerticalVelocity, knockback * 0.3f);
                }
            }
        }

        private static EntityKinds KindOf(CombatEntity e) =>
            e.Kind == CombatEntity.EntityKind.Player ? EntityKinds.Players : e.Kind == CombatEntity.EntityKind.Mob ? EntityKinds.Mobs : EntityKinds.Others;
    }
}
