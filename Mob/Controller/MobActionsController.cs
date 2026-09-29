using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The mob component placed on mob prefabs (a <see cref="Mob"/> with extra helpers). The old query methods are kept
/// and now answer from the AI's perception (what the mob actually sees and remembers).
/// </summary>
public class MobActionsController : Mob
{
    [Tooltip("Size of the old bite detection box (kept for compatibility).")]
    [SerializeField] Vector3 detectionDistance = new Vector3(1.5f, 2f, 2f);
    [Tooltip("Offset of the old bite detection box (kept for compatibility).")]
    [SerializeField] Vector3 offSetDetectionDistance = new Vector3(0f, 1f, 1f);
    [Tooltip("Transform used for gizmos and the bite box. Empty = this object.")]
    [SerializeField] Transform mobTransform;

    public Transform MobTransform
    {
        get => mobTransform != null ? mobTransform : transform;
        set => mobTransform = value;
    }

    public Vector3 OffSetDetectionDistance
    {
        get => offSetDetectionDistance;
        set => offSetDetectionDistance = value;
    }

    public Vector3 DetectionDistance
    {
        get => detectionDistance;
        set => detectionDistance = value;
    }

    /// <summary>The best target the mob currently knows about (its current target first), or null.</summary>
    public Transform AvailableTarget()
    {
        MobMovementStateMachine machine = AI;
        if (machine == null || machine.Context == null)
            return null;
        MobBrain brain = machine.Context.Brain;
        if (brain.Target != null)
            return brain.Target.transform;
        List<Transform> all = AvailableTargets();
        return all != null && all.Count > 0 ? all[0] : null;
    }

    /// <summary>Every hostile character the mob has noticed, nearest first (null if none).</summary>
    public List<Transform> AvailableTargets()
    {
        MobMovementStateMachine machine = AI;
        if (machine == null || machine.Context == null)
            return null;
        IReadOnlyList<MobMemoryEntry> entries = machine.Context.Memory.Entries;
        var found = new List<MobMemoryEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            MobMemoryEntry e = entries[i];
            if (e.entity != null && e.entity.IsAlive && e.detected && e.relation == MobRelationKind.Enemy)
                found.Add(e);
        }
        if (found.Count == 0)
            return null;
        found.Sort((a, b) => a.distance.CompareTo(b.distance));
        var result = new List<Transform>(found.Count);
        for (int i = 0; i < found.Count; i++)
            result.Add(found[i].entity.transform);
        return result;
    }

    /// <summary>The best reachable position away from a threat (open ground, far from it, not into hazards).</summary>
    public Vector3 CalculateBestEscapePosition(Vector3 threatPosition, float maxDistance)
    {
        MobMovementStateMachine machine = AI;
        if (machine != null && machine.Context != null &&
            machine.Context.Motor.TryFindEscapePoint(threatPosition, maxDistance, out Vector3 point, out _))
            return point;

        Vector3 away = (transform.position - threatPosition).normalized;
        Vector3 test = transform.position + away * maxDistance;
        return NavMesh.SamplePosition(test, out NavMeshHit hit, maxDistance, NavMesh.AllAreas) ? hit.position : transform.position;
    }

    protected override void Die()
    {
        StopAllCoroutines();
        base.Die();
    }

    private void OnDrawGizmos()
    {
        Transform t = MobTransform;
        Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.5f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
        Gizmos.DrawWireCube(offSetDetectionDistance, detectionDistance);
        Gizmos.matrix = oldMatrix;
    }
}
