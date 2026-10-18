using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Optional changes to a trap's mechanics, set on a prop entry (works for prefabs and placeholders): damage, element,
    /// who it hurts, and its rhythm. Off = the trap's own values (the prefab's component, or the placeholder defaults).
    /// </summary>
    [Serializable]
    public class TrapSettings
    {
        [Tooltip("Use the values below for the traps of this entry.")]
        public bool overrideTrap;
        [Tooltip("Damage per hit.")]
        [Min(0f)] public float damage = 12f;
        [Tooltip("How the damage is resisted (Physical by Defense, Magical by Magic Resistance, True by neither).")]
        public DamageType damageType = DamageType.Physical;
        [Tooltip("Element of the damage (Fire jets, Poison darts...).")]
        public ElementType element = ElementType.None;
        [Tooltip("Who it hurts.")]
        public EntityKinds affects = EntityKinds.Players;
        [Tooltip("Floor traps (spikes, fire jets, vents, lava): always dangerous, or on a cycle.")]
        public DungeonHazard.Timing timing = DungeonHazard.Timing.Cycle;
        [Tooltip("Cycle: seconds dangerous / seconds safe.")]
        public Vector2 activeAndSafeSeconds = new Vector2(1.2f, 1.8f);
        [Tooltip("Floor traps: seconds between two hits on the same character while standing in it.")]
        [Min(0.05f)] public float hitInterval = 0.6f;
        [Tooltip("Blades and logs: seconds for a full swing (0 = keep). Dart walls: seconds between volleys (0 = keep).")]
        [Min(0f)] public float period;
        [Tooltip("Blades and logs: push given to whoever is hit (m/s, 0 = keep).")]
        [Min(0f)] public float knockback;

        /// <summary>Applies the overrides to every trap component under <paramref name="go"/>.</summary>
        public void ApplyTo(GameObject go)
        {
            if (!overrideTrap || go == null)
                return;
            foreach (DungeonHazard h in go.GetComponentsInChildren<DungeonHazard>(true))
            {
                h.damage = damage;
                h.damageType = damageType;
                h.element = element;
                h.affects = affects;
                h.timing = timing;
                h.activeSeconds = Mathf.Max(0.05f, activeAndSafeSeconds.x);
                h.inactiveSeconds = Mathf.Max(0.05f, activeAndSafeSeconds.y);
                h.interval = Mathf.Max(0.05f, hitInterval);
            }
            foreach (DungeonBladeTrap b in go.GetComponentsInChildren<DungeonBladeTrap>(true))
            {
                b.damage = damage;
                b.affects = affects;
                if (period > 0f) b.period = Mathf.Max(0.3f, period);
                if (knockback > 0f) b.knockback = knockback;
            }
            foreach (DungeonDartTrap d in go.GetComponentsInChildren<DungeonDartTrap>(true))
            {
                d.damage = damage;
                d.element = element;
                if (period > 0f) d.reload = Mathf.Max(0.2f, period);
            }
        }
    }

    public sealed partial class DungeonPrimitives
    {
        /// <summary>
        /// Gives a prefab placed for an entry the mechanics of its placeholder kind when it has none of its own: a Spike Trap
        /// prefab gets a DungeonHazard (with a trigger fitted to the prefab), a Blade Trap prefab a DungeonBladeTrap, a Barrel
        /// / Crate / Urn prefab a DungeonBreakable, a Chest prefab a DungeonChest, a Fountain a rest point... The values are
        /// the placeholder's (change them on your prefab's component, or with the entry's Trap Settings). Mechanics already
        /// on the prefab are kept.
        /// </summary>
        public void GiveMechanics(GameObject target, DungeonPrimitive kind, float lightRange, int variant)
        {
            if (target == null || kind == DungeonPrimitive.None)
                return;
            GameObject stand = Create(kind, "Mechanics", Color.white, lightRange, variant);
            try
            {
                Bounds bounds = BoundsOf(target);
                bool copied = false;
                foreach (MonoBehaviour source in stand.GetComponents<MonoBehaviour>())
                {
                    if (source == null || !IsMechanic(source))
                        continue;
                    Type type = source.GetType();
                    if (target.GetComponentInChildren(type, true) != null)
                        continue;   // the prefab brings its own
                    if (source is DungeonHazard)
                        AddTrigger(target, stand, bounds);
                    Component added = target.AddComponent(type);
                    if (added == null)
                        continue;
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), added);
                    copied = true;
                }
                if (copied && kind == DungeonPrimitive.Chest && target.transform.Find("Lid") == null)
                    Debug.Log($"[Dungeon] '{target.name}': a chest prefab can have a child named \"Lid\" (it opens when the chest is used).", target);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stand);
            }
        }

        private static bool IsMechanic(MonoBehaviour m) =>
            m is DungeonHazard || m is DungeonBladeTrap || m is DungeonDartTrap || m is DungeonInteractable ||
            m is DungeonPressurePlate || m is DungeonKey;

        /// <summary>A trigger like the placeholder's, fitted to the prefab's size (traps need one to hurt).</summary>
        private static void AddTrigger(GameObject target, GameObject stand, Bounds bounds)
        {
            foreach (Collider c in target.GetComponents<Collider>())
                if (c.isTrigger)
                    return;   // it has one already
            BoxCollider proto = null;
            foreach (BoxCollider b in stand.GetComponents<BoxCollider>())
                if (b.isTrigger) { proto = b; break; }
            var box = target.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (bounds.size.sqrMagnitude > 0.0001f)
            {
                Vector3 center = target.transform.InverseTransformPoint(bounds.center);
                Vector3 size = target.transform.InverseTransformVector(bounds.size);
                size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                // At least as tall as the placeholder's zone (a flat spike plate still hurts the legs).
                float minHeight = proto != null ? proto.size.y : 1f;
                if (size.y < minHeight)
                {
                    center.y += (minHeight - size.y) * 0.5f;
                    size.y = minHeight;
                }
                box.center = center;
                box.size = size;
            }
            else if (proto != null)
            {
                box.center = proto.center;
                box.size = proto.size;
            }
        }

        private static Bounds BoundsOf(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++)
                b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
