using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A wall plate that shoots darts straight ahead (its forward) when a player passes in front of it - or, when
    /// <see cref="triggeredOnly"/>, only when something calls <see cref="Trigger"/> (the arrow launchers of a corridor's
    /// tripwire). Darts fly until they hit something: a character takes damage, a wall stops them.
    /// </summary>
    public class DungeonDartTrap : MonoBehaviour
    {
        [Tooltip("How far ahead it notices players and darts fly (meters).")]
        [Min(1f)] public float range = 9f;
        [Tooltip("How wide the watched lane is (meters).")]
        [Min(0.2f)] public float laneWidth = 1.4f;
        [Tooltip("Seconds between shots.")]
        [Min(0.2f)] public float reload = 1.6f;
        [Tooltip("Dart speed (meters per second).")]
        [Min(1f)] public float speed = 16f;
        [Tooltip("Damage per dart.")]
        [Min(0f)] public float damage = 9f;
        [Tooltip("Element of the darts (Poison for poisoned darts).")]
        public ElementType element = ElementType.Poison;
        [Tooltip("Height of the muzzle above the trap's base (meters).")]
        public float muzzleHeight = 1.2f;
        [Tooltip("Only shoots when triggered (a tripwire), never by watching its lane.")]
        public bool triggeredOnly;
        [Tooltip("Darts per trigger.")]
        [Min(1)] public int volley = 1;

        private struct Dart
        {
            public Transform visual;
            public Vector3 position;
            public float travelled;
        }

        private readonly List<Dart> darts = new List<Dart>();
        private float readyAt;
        private Material material;

        private void Start()
        {
            Renderer r = GetComponentInChildren<Renderer>();
            material = r != null ? r.sharedMaterial : null;
        }

        private Vector3 Muzzle => transform.position + Vector3.up * muzzleHeight + transform.forward * 0.25f;

        private void Update()
        {
            if (!triggeredOnly && Time.time >= readyAt && PlayerInLane())
            {
                readyAt = Time.time + reload;
                Fire();
            }
            for (int i = darts.Count - 1; i >= 0; i--)
            {
                Dart d = darts[i];
                float step = speed * Time.deltaTime;
                bool done = d.travelled + step > range;
                if (Physics.Raycast(d.position, transform.forward, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore))
                {
                    CombatEntity e = CombatEntity.Resolve(hit.collider);
                    if (e != null && e.IsAlive)
                        e.ApplyDamage(new DamageInfo { amount = damage, target = e, point = hit.point, direction = transform.forward, type = DamageType.Physical, element = element });
                    done = true;
                }
                d.position += transform.forward * step;
                d.travelled += step;
                if (d.visual != null)
                    d.visual.position = d.position;
                if (done)
                {
                    if (d.visual != null)
                        Destroy(d.visual.gameObject);
                    darts.RemoveAt(i);
                }
                else
                {
                    darts[i] = d;
                }
            }
        }

        private bool PlayerInLane()
        {
            Vector3 origin = transform.position;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive)
                    continue;
                Vector3 local = transform.InverseTransformPoint(p.transform.position);
                if (local.z > 0.3f && local.z < range && Mathf.Abs(local.x) < laneWidth * 0.5f && Mathf.Abs(p.transform.position.y - origin.y) < 2.5f)
                    return true;
            }
            return false;
        }

        /// <summary>Shoots a volley now (ignoring the reload), staggered a little in height.</summary>
        public void Trigger()
        {
            readyAt = Time.time + reload;
            for (int i = 0; i < volley; i++)
                Fire(i * 0.35f - (volley - 1) * 0.175f);
        }

        private void Fire(float lift = 0f)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(visual.GetComponent<Collider>());
            visual.name = "Dart";
            visual.transform.localScale = new Vector3(0.04f, 0.04f, 0.35f);
            visual.transform.rotation = transform.rotation;
            if (material != null)
                visual.GetComponent<Renderer>().sharedMaterial = material;
            Vector3 muzzle = Muzzle + Vector3.up * lift;
            darts.Add(new Dart { visual = visual.transform, position = muzzle, travelled = 0f });
            visual.transform.position = muzzle;
        }

        private void OnDestroy()
        {
            foreach (Dart d in darts)
                if (d.visual != null)
                    Destroy(d.visual.gameObject);
        }
    }
}
