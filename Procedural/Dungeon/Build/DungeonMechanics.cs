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
                d.affects = affects;
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
                Bounds bounds = LocalBoundsOf(target);
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

        /// <summary>
        /// A trigger like the placeholder's, fitted to the prefab's size (traps need one to hurt). <paramref name="bounds"/>
        /// is the prefab's box in its own space (see <see cref="LocalBoundsOf"/>), so the trigger fits however the trap is turned.
        /// </summary>
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
                Vector3 center = bounds.center;
                Vector3 size = bounds.size;
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

        /// <summary>
        /// The box around an object's meshes in its own space - the space a collider on it is measured in. (The world box
        /// of an object turned off the grid's axes is bigger than the object, and turning that back gives a wrong size.)
        /// </summary>
        private static Bounds LocalBoundsOf(GameObject go)
        {
            Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
            var box = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                    continue;   // particles and trails have no shape of their own
                Bounds own = r.localBounds;
                Matrix4x4 m = toLocal * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = own.center + Vector3.Scale(own.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = m.MultiplyPoint3x4(corner);
                    if (any)
                        box.Encapsulate(point);
                    else
                        box = new Bounds(point, Vector3.zero);
                    any = true;
                }
            }
            return box;
        }

        /// <summary>
        /// A post (and foot) under a lever stand-in that has no wall to hang on - a valve in a room over the void.
        /// <paramref name="height"/> is the lever's height above the floor.
        /// </summary>
        public void AddPost(GameObject lever, float height)
        {
            if (lever == null || height <= 0.1f)
                return;
            // Behind the plate, so the handle swings free in front of it: from the floor up to the plate's top.
            float length = height + 0.25f;
            Part(lever, PrimitiveType.Cube, new Vector3(0f, 0.25f - length * 0.5f, -0.16f), new Vector3(0.14f, length, 0.12f), Iron);
            Part(lever, PrimitiveType.Cube, new Vector3(0f, -height + 0.04f, -0.16f), new Vector3(0.42f, 0.08f, 0.42f), DarkStone);
            Collider(lever, new Vector3(0f, 0.25f - length * 0.5f, -0.16f), new Vector3(0.18f, length, 0.16f));
        }

        /// <summary>
        /// Puts a floor trap that is dangerous all the time on a cycle (see <see cref="PlacementLinks.TimedTrap"/>: it
        /// stands where nobody can walk around it), each with its own rhythm. Traps already on a cycle keep theirs.
        /// </summary>
        public static void PutOnCycle(GameObject go, int variant)
        {
            if (go == null)
                return;
            foreach (DungeonHazard h in go.GetComponentsInChildren<DungeonHazard>(true))
            {
                if (h.timing != DungeonHazard.Timing.Constant)
                    continue;
                h.timing = DungeonHazard.Timing.Cycle;
                h.phaseOffset = (variant % 7) * 0.43f;
            }
        }
    }
}
