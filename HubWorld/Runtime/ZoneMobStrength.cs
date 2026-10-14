using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// The extra strength of a world mob spawned far from the city (added by the director): its max health was raised
    /// when it spawned, and this multiplies the damage it deals.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneMobStrength : MonoBehaviour, IDamageDealtModifier
    {
        [Tooltip("Multiplies the damage this mob deals and was applied to its max health (1 = as authored).")]
        [Min(0.1f)] public float strength = 1f;

        private CombatEntity entity;

        /// <summary>Makes a freshly spawned mob <paramref name="strength"/> times as strong (health now, damage from now on).</summary>
        public static ZoneMobStrength Apply(GameObject mob, float strength)
        {
            if (mob == null || Mathf.Approximately(strength, 1f))
                return null;
            var z = mob.GetComponent<ZoneMobStrength>();
            if (z == null)
                z = mob.AddComponent<ZoneMobStrength>();
            HealthManager health = mob.GetComponentInChildren<HealthManager>();
            if (health != null && health.MaxValue > 0f)
                health.MaxValue = health.MaxValue / Mathf.Max(0.1f, z.strength) * strength; // re-applying replaces the old factor
            z.strength = strength;
            Mob m = mob.GetComponent<Mob>();
            if (m != null)
                m.Strength = strength;
            return z;
        }

        private void Start()
        {
            entity = CombatEntity.Resolve(gameObject);
            if (entity != null)
                entity.AddDamageDealtModifier(this);
        }

        private void OnDestroy()
        {
            if (entity != null)
                entity.RemoveDamageDealtModifier(this);
        }

        public float ModifyDamageDealt(in DamageInfo info, float amount) => amount * strength;
    }
}
