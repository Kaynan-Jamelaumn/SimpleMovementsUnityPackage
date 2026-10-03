using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProceduralDungeon
{
    /// <summary>
    /// A trap zone: spikes, fire jets, spore vents, lava. While a character it affects stands in its trigger it "hits"
    /// every <see cref="interval"/> seconds: the damage goes through the combat system (defense, resistances, death, kill
    /// credit) and <see cref="onHit"/> / <see cref="AnyHit"/> fire. Characters are recognised by their Combat Entity
    /// (kind: players, mobs, others), not by tags, so every player of a multiplayer game is hurt.
    /// <para>Constant traps hurt all the time (spikes pop up on each hit). Cycling traps are only dangerous while active
    /// (Active Seconds on, Inactive Seconds off, shifted by Phase Offset): their children named "Active" show only then, and
    /// spikes rise for the active time - watch the rhythm and pass in between.</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DungeonHazard : MonoBehaviour
    {
        public enum Timing
        {
            /// <summary>Always dangerous.</summary>
            Constant,
            /// <summary>Dangerous for Active Seconds, then safe for Inactive Seconds.</summary>
            Cycle,
        }

        [Tooltip("Constant: always dangerous. Cycle: dangerous only while active (fire jets, spike floors, spore vents).")]
        public Timing timing = Timing.Constant;
        [Tooltip("Cycle: seconds it is dangerous.")]
        [Min(0.05f)] public float activeSeconds = 1.2f;
        [Tooltip("Cycle: seconds it is safe.")]
        [Min(0.05f)] public float inactiveSeconds = 1.8f;
        [Tooltip("Cycle: shifts this trap's rhythm (seconds), so neighbouring traps don't fire together.")]
        public float phaseOffset;
        [Tooltip("Damage type (Physical is reduced by Defense, Magical by Magic Resistance, True by neither).")]
        public DamageType damageType = DamageType.Physical;
        [Tooltip("Element of the damage (fire, poison, ice...): elemental resistances and reactions apply.")]
        public ElementType element = ElementType.None;
        [Tooltip("Kinds of characters the trap hurts (players, mobs, others).")]
        public EntityKinds affects = EntityKinds.Players;
        [Tooltip("Fallback for objects without a Combat Entity / status controller: they are hurt when they have this tag (empty = never).")]
        public string targetTag = "";
        [Tooltip("Damage per hit.")]
        [Min(0f)] public float damage = 10f;
        [Tooltip("Deal the damage through the combat system. Turn off if you apply it yourself in On Hit / AnyHit.")]
        public bool applyDamage = true;
        [Tooltip("Seconds between hits while the target stays in the trap.")]
        [Min(0.05f)] public float interval = 1f;
        [Tooltip("Height the spikes (children) rise by on a hit (0 = don't animate).")]
        public float spikeRise = 0.25f;
        [Tooltip("Called on every hit with the target - hook your damage code here (or subscribe to DungeonHazard.AnyHit from code).")]
        public UnityEvent<GameObject> onHit = new UnityEvent<GameObject>();

        /// <summary>Raised for every hazard hit: (hazard, target, damage).</summary>
        public static event Action<DungeonHazard, GameObject, float> AnyHit;

        private readonly Dictionary<GameObject, float> lastHit = new Dictionary<GameObject, float>();
        private float spikeTimer;
        private Vector3[] spikeRest;
        private readonly List<GameObject> activeParts = new List<GameObject>();

        /// <summary>Is it dangerous right now?</summary>
        public bool IsActive
        {
            get
            {
                if (timing == Timing.Constant)
                    return true;
                float period = activeSeconds + inactiveSeconds;
                return Mathf.Repeat(Time.time + phaseOffset, period) < activeSeconds;
            }
        }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            spikeRest = new Vector3[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                spikeRest[i] = transform.GetChild(i).localPosition;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t != transform && t.name == "Active")
                    activeParts.Add(t.gameObject);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsActive)
                return;
            CombatEntity entity = CombatEntity.Resolve(other);
            GameObject target;
            if (entity != null)
            {
                if (!entity.IsAlive || (affects & KindOf(entity)) == 0)
                    return;
                target = entity.gameObject;
            }
            else
            {
                if (string.IsNullOrEmpty(targetTag) || !SafeCompareTag(other, targetTag))
                    return;
                target = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            }
            if (lastHit.TryGetValue(target, out float last) && Time.time - last < interval)
                return;
            lastHit[target] = Time.time;
            spikeTimer = 0.35f;
            if (applyDamage && entity != null && damage > 0f)
                entity.ApplyDamage(new DamageInfo
                {
                    amount = damage,
                    target = entity,
                    point = other.ClosestPoint(transform.position),
                    direction = Vector3.up,
                    type = damageType,
                    element = element,
                });
            onHit.Invoke(target);
            AnyHit?.Invoke(this, target, damage);
        }

        private static EntityKinds KindOf(CombatEntity e) =>
            e.Kind == CombatEntity.EntityKind.Player ? EntityKinds.Players : e.Kind == CombatEntity.EntityKind.Mob ? EntityKinds.Mobs : EntityKinds.Others;

        private static bool SafeCompareTag(Component c, string tag)
        {
            try { return c.CompareTag(tag); }
            catch (UnityException) { return false; } // tag not defined in this project
        }

        private void Update()
        {
            bool active = IsActive;
            foreach (GameObject part in activeParts)
                if (part != null && part.activeSelf != active)
                    part.SetActive(active);
            if (spikeRise <= 0f || spikeRest == null)
                return;
            spikeTimer = Mathf.Max(0f, spikeTimer - Time.deltaTime);
            // Constant traps pop their spikes on each hit; cycling ones raise them for the active time.
            float lift = (timing == Timing.Cycle ? active : spikeTimer > 0f) ? spikeRise : 0f;
            for (int i = 1; i < transform.childCount && i < spikeRest.Length; i++)
            {
                Transform t = transform.GetChild(i);
                if (t.name == "Active" || t.GetComponent<Light>() != null)
                    continue;
                t.localPosition = Vector3.MoveTowards(t.localPosition, spikeRest[i] + Vector3.up * lift, Time.deltaTime * 4f);
            }
        }
    }
}
