using UnityEngine;
using UnityEngine.AI;

namespace ProceduralDungeon
{
    /// <summary>
    /// A mob asleep (barracks): its AI is switched off until a player comes too close - much closer when sneaking
    /// (crouched) - it is hurt, or a sleeper near it wakes up (one waking soldier rouses the ones around him). Sneak past
    /// to leave the barracks sleeping.
    /// </summary>
    public class DungeonSleeper : MonoBehaviour
    {
        [Tooltip("A player walking within this distance wakes it (meters).")]
        [Min(0.5f)] public float wakeRadius = 5f;
        [Tooltip("A sneaking (crouched) player must come this close (meters).")]
        [Min(0.3f)] public float sneakRadius = 1.6f;
        [Tooltip("Sleepers within this distance of one that wakes wake too (meters).")]
        [Min(0f)] public float alarmRadius = 6f;

        public bool Asleep { get; private set; }

        private MobMovementStateMachine ai;
        private NavMeshAgent agent;
        private CombatEntity entity;
        private Transform marker;
        private float nextCheck;

        private void Start()
        {
            ai = GetComponent<MobMovementStateMachine>();
            agent = GetComponent<NavMeshAgent>();
            entity = GetComponentInChildren<CombatEntity>();
            if (entity != null)
                entity.Damaged += OnDamaged;
            Sleep();
        }

        private void Sleep()
        {
            Asleep = true;
            if (ai != null && ai.Context != null)
                ai.Context.Brain.EnterSleep();
            if (ai != null)
                ai.enabled = false;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;
            // A small "Zzz" glow above the head.
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "Asleep";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 2.4f;
            go.transform.localScale = Vector3.one * 0.18f;
            var r = go.GetComponent<Renderer>();
            r.material.color = new Color(0.55f, 0.7f, 1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker = go.transform;
        }

        private void Update()
        {
            if (!Asleep)
                return;
            if (marker != null)
                marker.localPosition = Vector3.up * (2.4f + Mathf.Sin(Time.time * 1.5f) * 0.12f);
            if (Time.time < nextCheck)
                return;
            nextCheck = Time.time + 0.2f;
            foreach (CombatEntity p in CombatEntity.Players)
            {
                if (p == null || !p.IsAlive)
                    continue;
                float r = p.IsSneaking ? sneakRadius : wakeRadius;
                Vector3 d = p.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 3f && d.sqrMagnitude <= r * r)
                {
                    Wake(p, true);
                    return;
                }
            }
        }

        private void OnDamaged(DamageInfo info) => Wake(info.source, true);

        /// <summary>Wakes it (and, with <paramref name="alarm"/>, the sleepers around it).</summary>
        public void Wake(CombatEntity by, bool alarm)
        {
            if (!Asleep)
                return;
            Asleep = false;
            if (marker != null)
                Destroy(marker.gameObject);
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = false;
            if (ai != null)
            {
                ai.enabled = true;
                if (by != null && ai.Context != null)
                    ai.Context.Brain.ForceTarget(by);
            }
            if (!alarm || alarmRadius <= 0f)
                return;
            foreach (DungeonSleeper other in FindObjectsByType<DungeonSleeper>(FindObjectsSortMode.None))
                if (other != this && other.Asleep && (other.transform.position - transform.position).sqrMagnitude <= alarmRadius * alarmRadius)
                    other.Wake(by, false);
        }

        private void OnDestroy()
        {
            if (entity != null)
                entity.Damaged -= OnDamaged;
        }
    }
}
