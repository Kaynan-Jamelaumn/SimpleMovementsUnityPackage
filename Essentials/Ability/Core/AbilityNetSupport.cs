using UnityEngine;

/// <summary>
/// What an <see cref="AbilityCaster"/> asks the network layer to do when this machine does not decide gameplay (a
/// client's copy of its own player): send the cast or cancel intention to the server. Installed by the network layer
/// on the owning client only (<see cref="AbilityCaster.NetBridge"/>); offline nothing is installed and casts run
/// locally exactly as before.
/// </summary>
public interface IAbilityNetBridge
{
    /// <summary>
    /// Sends a cast of slot <paramref name="slotIndex"/> to the authority. Returns true when a request was sent;
    /// false (with the reason) when it was not, e.g. <see cref="CastFailReason.Busy"/> while an earlier request of the
    /// same slot still waits for the server's answer.
    /// </summary>
    bool RequestCast(AbilityCaster caster, int slotIndex, in CastRequest request, out CastFailReason reason);

    /// <summary>Asks the authority to cancel the abilities of <paramref name="caster"/> still winding up.</summary>
    void RequestCancel(AbilityCaster caster);
}

/// <summary>
/// Small pure helpers used by the ability network layer (validation of client requests, time conversion, seeds,
/// rate limiting). No Fish-Net types: always compiled and covered by Edit Mode tests.
/// </summary>
public static class AbilityNetMath
{
    private static readonly System.Random seedSource = new System.Random();

    /// <summary>Largest coordinate accepted from a client (metres): anything beyond is treated as garbage.</summary>
    public const float MaxCoordinate = 100000f;

    /// <summary>Is <paramref name="f"/> a usable number (not NaN, not infinite)?</summary>
    public static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    /// <summary>Are all components of <paramref name="v"/> usable numbers within <see cref="MaxCoordinate"/>?</summary>
    public static bool IsFinite(Vector3 v) =>
        IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z) &&
        Mathf.Abs(v.x) <= MaxCoordinate && Mathf.Abs(v.y) <= MaxCoordinate && Mathf.Abs(v.z) <= MaxCoordinate;

    /// <summary>
    /// Cleans a cast request received from a client before the server uses it: rejects NaN / infinite / absurd
    /// vectors, normalises the direction (a zero direction counts as none) and pulls the aimed point back within
    /// <paramref name="maxPointDistance"/> of <paramref name="origin"/>. Returns false when the request is unusable.
    /// The target is not touched (the caster's own checks validate it).
    /// </summary>
    public static bool SanitizeRequest(ref CastRequest request, Vector3 origin, float maxPointDistance)
    {
        if (request.hasPoint && !IsFinite(request.point))
            return false;
        if (request.hasDirection && !IsFinite(request.direction))
            return false;
        if (request.hasDirection)
        {
            if (request.direction.sqrMagnitude < 1e-8f)
            {
                request.hasDirection = false;
                request.direction = Vector3.zero;
            }
            else
            {
                request.direction.Normalize();
            }
        }
        if (request.hasPoint && maxPointDistance > 0f)
        {
            Vector3 d = request.point - origin;
            float sqr = d.sqrMagnitude;
            if (sqr > maxPointDistance * maxPointDistance)
                request.point = origin + d / Mathf.Sqrt(sqr) * maxPointDistance;
        }
        if (!request.hasPoint)
            request.point = Vector3.zero;
        if (!request.hasDirection)
            request.direction = Vector3.zero;
        return true;
    }

    /// <summary>Seconds left until an absolute time (0 when it is unset (0) or already passed).</summary>
    public static float Remaining(float absoluteTime, float now) => absoluteTime > 0f ? Mathf.Max(0f, absoluteTime - now) : 0f;

    /// <summary>
    /// Turns "seconds left" measured on another machine into this machine's absolute time, discounting
    /// <paramref name="age"/> seconds the message spent travelling. 0 (unset) when nothing is left.
    /// </summary>
    public static float ToLocalTime(float remaining, float now, float age)
    {
        float left = remaining - Mathf.Max(0f, age);
        return left > 0f ? now + left : 0f;
    }

    /// <summary>
    /// Did a countdown change other than by ticking down? <paramref name="sentRemaining"/> seconds were reported at
    /// <paramref name="sentAt"/>; it should now be that minus the time since. True when <paramref name="remainingNow"/>
    /// differs by more than <paramref name="tolerance"/> (a cooldown reset or reduced, a charge spent or regained early):
    /// the server then tells the owner again.
    /// </summary>
    public static bool TimerJumped(float sentRemaining, float sentAt, float remainingNow, float now, float tolerance = 0.1f)
    {
        float expected = Mathf.Max(0f, sentRemaining - Mathf.Max(0f, now - sentAt));
        return Mathf.Abs(expected - Mathf.Max(0f, remainingNow)) > tolerance;
    }

    /// <summary>A fresh random seed for the cosmetic randomness of one cast (does not touch UnityEngine.Random).</summary>
    public static int NextSeed()
    {
        lock (seedSource)
            return seedSource.Next(1, int.MaxValue);
    }

    /// <summary>The seed of one volley of a cast: the server and every client derive the same value.</summary>
    public static int VolleySeed(int castSeed, int volleyIndex) => unchecked(castSeed * 31 + volleyIndex * 7919 + 17);

    /// <summary>Did the aim move enough (distance or angle) to be worth sending again?</summary>
    public static bool AimChanged(Vector3 lastPoint, Vector3 lastDirection, Vector3 point, Vector3 direction, float minDistance, float minAngle)
    {
        if ((point - lastPoint).sqrMagnitude > minDistance * minDistance)
            return true;
        if (lastDirection.sqrMagnitude < 1e-8f || direction.sqrMagnitude < 1e-8f)
            return (lastDirection.sqrMagnitude >= 1e-8f) != (direction.sqrMagnitude >= 1e-8f);
        return Vector3.Angle(lastDirection, direction) > minAngle;
    }

    /// <summary>
    /// A token bucket: allows <see cref="Burst"/> requests at once and <see cref="PerSecond"/> on average. Used by the
    /// server to ignore clients that spam requests.
    /// </summary>
    public struct RateLimiter
    {
        /// <summary>Requests refilled per second.</summary>
        public float PerSecond;
        /// <summary>Requests allowed back to back.</summary>
        public float Burst;
        private float tokens;
        private float last;
        private bool started;

        /// <summary>A limiter that starts full (<paramref name="burst"/> requests available).</summary>
        public RateLimiter(float perSecond, float burst)
        {
            PerSecond = Mathf.Max(0.01f, perSecond);
            Burst = Mathf.Max(1f, burst);
            tokens = Burst;
            last = 0f;
            started = false;
        }

        /// <summary>Takes one request at time <paramref name="now"/>. False = over the limit (drop the request).</summary>
        public bool TryTake(float now)
        {
            if (!started)
            {
                started = true;
                tokens = Burst;
                last = now;
            }
            tokens = Mathf.Min(Burst, tokens + Mathf.Max(0f, now - last) * PerSecond);
            last = now;
            if (tokens < 1f)
                return false;
            tokens -= 1f;
            return true;
        }
    }
}

/// <summary>
/// A small deterministic random sequence (xorshift) for the cosmetic randomness of a cast: projectile spreads and rains,
/// summon placement. The server and every client build it from the same seed (<see cref="AbilityCastInstance.Seed"/>),
/// so they produce the same numbers, and it never touches the global <see cref="UnityEngine.Random"/> state.
/// </summary>
public struct CastRandom
{
    private uint state;

    /// <summary>Starts the sequence of <paramref name="seed"/> (any value, 0 included).</summary>
    public CastRandom(int seed)
    {
        state = unchecked((uint)seed * 2654435761u) ^ 0x9E3779B9u;
        if (state == 0u)
            state = 0x6D2B79F5u;
        NextUInt(); // mixes the first value so close seeds do not start alike
    }

    /// <summary>The next raw 32-bit value.</summary>
    public uint NextUInt()
    {
        if (state == 0u)
            state = 0x6D2B79F5u; // default(CastRandom) still works
        uint x = state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        state = x;
        return x;
    }

    /// <summary>A value in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    /// <summary>A value in [<paramref name="min"/>, <paramref name="max"/>) (like UnityEngine.Random.Range for floats).</summary>
    public float Range(float min, float max) => min + (max - min) * NextFloat();

    /// <summary>A uniformly distributed point inside the unit circle (like UnityEngine.Random.insideUnitCircle).</summary>
    public Vector2 InsideUnitCircle()
    {
        float angle = NextFloat() * Mathf.PI * 2f;
        float r = Mathf.Sqrt(NextFloat());
        return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
    }
}
