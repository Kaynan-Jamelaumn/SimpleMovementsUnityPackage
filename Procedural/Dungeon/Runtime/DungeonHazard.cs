using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProceduralDungeon
{
    /// <summary>
    /// A trap zone (e.g. the primitive spike trap). While something with <see cref="targetTag"/> stands in its trigger it
    /// "hits" every <see cref="interval"/> seconds: <see cref="onHit"/> and <see cref="AnyHit"/> fire with the target and
    /// the damage, so your health system decides what a hit does. Spikes (child objects) pop up on each hit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DungeonHazard : MonoBehaviour
    {
        [Tooltip("Tag of what the trap hurts (the collider that enters must have this tag).")]
        public string targetTag = "Player";
        [Tooltip("Damage per hit, passed to On Hit / AnyHit - your health system applies it.")]
        [Min(0f)] public float damage = 10f;
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

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            spikeRest = new Vector3[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                spikeRest[i] = transform.GetChild(i).localPosition;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag(targetTag))
                return;
            GameObject target = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (lastHit.TryGetValue(target, out float last) && Time.time - last < interval)
                return;
            lastHit[target] = Time.time;
            spikeTimer = 0.35f;
            onHit.Invoke(target);
            AnyHit?.Invoke(this, target, damage);
        }

        private void Update()
        {
            if (spikeRise <= 0f || spikeRest == null)
                return;
            spikeTimer = Mathf.Max(0f, spikeTimer - Time.deltaTime);
            float lift = spikeTimer > 0f ? spikeRise : 0f;
            for (int i = 1; i < transform.childCount && i < spikeRest.Length; i++)
            {
                Transform t = transform.GetChild(i);
                t.localPosition = Vector3.MoveTowards(t.localPosition, spikeRest[i] + Vector3.up * lift, Time.deltaTime * 4f);
            }
        }
    }
}
