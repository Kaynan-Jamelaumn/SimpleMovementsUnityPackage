using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Moves a character over time without its own input: knockbacks, pulls, dashes and leaps. Works with a
/// CharacterController (player), a NavMeshAgent (mobs - stays on the NavMesh unless leaping), a Rigidbody or a
/// plain Transform. Added automatically by <see cref="CombatEntity.ApplyDisplacement"/> and movement abilities.
/// </summary>
[DisallowMultipleComponent]
public class ForcedMovement : MonoBehaviour
{
    private CombatEntity entity;
    private Vector3 displacement;
    private Vector3 applied;
    private float duration;
    private float elapsed;
    private float arcHeight;
    private bool linear;
    private bool active;
    private bool agentWasStopped;
    private bool agentPositionDetached;
    private Action onComplete;

    private static readonly Dictionary<CombatEntity, ForcedMovement> byEntity = new Dictionary<CombatEntity, ForcedMovement>();
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[8];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => byEntity.Clear();

    /// <summary>True while moving.</summary>
    public bool IsActive => active;

    /// <summary>Remaining seconds of the current movement.</summary>
    public float Remaining => active ? Mathf.Max(0f, duration - elapsed) : 0f;

    public static ForcedMovement Find(CombatEntity e)
    {
        if (e == null)
            return null;
        byEntity.TryGetValue(e, out ForcedMovement fm);
        return fm;
    }

    public static ForcedMovement GetOrAdd(CombatEntity e)
    {
        ForcedMovement fm = Find(e);
        if (fm != null)
            return fm;
        fm = e.GetComponent<ForcedMovement>();
        if (fm == null)
            fm = e.gameObject.AddComponent<ForcedMovement>();
        fm.entity = e;
        byEntity[e] = fm;
        return fm;
    }

    private void Awake()
    {
        if (entity == null)
            entity = GetComponent<CombatEntity>();
        if (entity != null)
            byEntity[entity] = this;
    }

    private void OnDestroy()
    {
        if (entity != null && byEntity.TryGetValue(entity, out ForcedMovement fm) && fm == this)
            byEntity.Remove(entity);
    }

    private void OnDisable()
    {
        if (active)
            Finish(false);
    }

    /// <summary>
    /// Starts moving by <paramref name="offset"/> (world) over <paramref name="seconds"/>, rising <paramref name="arc"/>
    /// metres at the middle. <paramref name="linearMotion"/> keeps constant speed (dashes); otherwise it eases out
    /// (knockbacks). A new movement replaces the current one.
    /// </summary>
    public void Begin(Vector3 offset, float seconds, float arc, bool linearMotion, Action completed = null)
    {
        if (active)
            Finish(false);

        if (entity == null)
            entity = GetComponent<CombatEntity>();

        displacement = offset;
        duration = Mathf.Max(0.02f, seconds);
        arcHeight = Mathf.Max(0f, arc);
        linear = linearMotion;
        elapsed = 0f;
        applied = Vector3.zero;
        onComplete = completed;
        active = true;

        NavMeshAgent agent = entity != null ? entity.Agent : GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agentWasStopped = agent.isStopped;
            agent.isStopped = true;
            if (arcHeight > 0.01f)
            {
                agent.updatePosition = false;
                agentPositionDetached = true;
            }
        }
    }

    /// <summary>Stops the current movement where it is.</summary>
    public void Cancel()
    {
        if (active)
            Finish(false);
    }

    private void Update()
    {
        if (!active)
            return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = linear ? t : 1f - (1f - t) * (1f - t);
        Vector3 target = displacement * eased;
        if (arcHeight > 0.01f)
            target += Vector3.up * (arcHeight * 4f * t * (1f - t));

        Vector3 delta = target - applied;
        applied = target;
        Move(delta);

        if (t >= 1f)
            Finish(true);
    }

    private void Move(Vector3 delta)
    {
        if (delta.sqrMagnitude < 1e-8f)
            return;

        CharacterController cc = entity != null ? entity.Controller : null;
        if (cc != null && cc.enabled)
        {
            cc.Move(delta);
            return;
        }

        NavMeshAgent agent = entity != null ? entity.Agent : null;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (agentPositionDetached)
            {
                transform.position += ClipAgainstObstacles(transform.position, delta);
            }
            else
            {
                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                agent.Move(flat);
            }
            return;
        }

        Rigidbody rb = entity != null ? entity.Body : GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.MovePosition(rb.position + ClipAgainstObstacles(rb.position, delta));
            return;
        }

        transform.position += ClipAgainstObstacles(transform.position, delta);
    }

    /// <summary>Shortens a move so it stops at the first obstacle (not other characters).</summary>
    private Vector3 ClipAgainstObstacles(Vector3 from, Vector3 delta)
    {
        float distance = delta.magnitude;
        if (distance < 1e-4f)
            return delta;
        Vector3 dir = delta / distance;
        float r = entity != null ? Mathf.Max(0.05f, entity.Radius * 0.8f) : 0.3f;
        Vector3 origin = from + Vector3.up * (entity != null ? Mathf.Max(r, entity.Height * 0.5f) : 1f);
        int n = Physics.SphereCastNonAlloc(origin, r, dir, hitBuffer, distance, CombatSettings.Instance.obstacleLayers, QueryTriggerInteraction.Ignore);
        float allowed = distance;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null || h.distance <= 0f)
                continue;
            if (entity != null && h.collider.transform.IsChildOf(entity.transform))
                continue;
            if (CombatEntity.Resolve(h.collider) != null)
                continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, h.distance - 0.05f));
        }
        return dir * allowed;
    }

    private void Finish(bool completed)
    {
        active = false;
        NavMeshAgent agent = entity != null ? entity.Agent : GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            if (agentPositionDetached)
            {
                agent.updatePosition = true;
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, agent.areaMask))
                    agent.Warp(hit.position);
                else
                    agent.Warp(transform.position);
            }
            if (agent.isOnNavMesh)
                agent.isStopped = agentWasStopped;
        }
        agentPositionDetached = false;

        Action cb = onComplete;
        onComplete = null;
        if (completed)
            cb?.Invoke();
    }
}
