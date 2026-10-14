using UnityEngine;

/// <summary>
/// Works out how fast something moves from where it is each frame: used on a client's copy of a networked mob, which
/// is moved by the network (interpolated server positions) rather than by its NavMeshAgent, to drive its movement
/// animations. Frame-to-frame jitter is smoothed out and jumps (teleports, warps) are ignored.
/// </summary>
public sealed class MobMotionEstimator
{
    /// <summary>How quickly the estimate follows the measured speed (per second; higher = snappier).</summary>
    public float Responsiveness = 10f;

    /// <summary>A move longer than this in one step is treated as a teleport and ignored (metres).</summary>
    public float TeleportDistance = 5f;

    private Vector3 last;
    private bool hasLast;

    /// <summary>The smoothed velocity (world space).</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>Starts again from <paramref name="position"/> (velocity zero).</summary>
    public void Reset(Vector3 position)
    {
        last = position;
        hasLast = true;
        Velocity = Vector3.zero;
    }

    /// <summary>Feeds the position after <paramref name="dt"/> seconds and returns the smoothed velocity.</summary>
    public Vector3 Step(Vector3 position, float dt)
    {
        if (!IsFinite(position))
            return Velocity;
        if (!hasLast)
        {
            Reset(position);
            return Velocity;
        }
        Vector3 delta = position - last;
        last = position;
        if (dt <= 1e-5f || float.IsNaN(dt) || float.IsInfinity(dt))
            return Velocity;
        if (delta.sqrMagnitude > TeleportDistance * TeleportDistance)
        {
            Velocity = Vector3.zero;
            return Velocity;
        }
        Vector3 measured = delta / dt;
        float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, Responsiveness) * Mathf.Min(dt, 0.5f));
        Velocity = Vector3.Lerp(Velocity, measured, k);
        if (Velocity.sqrMagnitude < 1e-6f)
            Velocity = Vector3.zero;
        return Velocity;
    }

    private static bool IsFinite(Vector3 v) =>
        !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
        !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
}
