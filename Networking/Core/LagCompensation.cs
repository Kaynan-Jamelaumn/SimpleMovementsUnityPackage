using System;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Lag compensation for server-side hit detection: while a hit query runs on behalf of an attacker, everything that
    /// can be hit is moved back to where that attacker saw it when they acted, then put back. Always compiled; offline
    /// (and on clients) the scope does nothing. The Fish-Net layer installs the <see cref="Provider"/>, which records
    /// positions every tick and knows each player's latency.
    /// </summary>
    /// <example><code>
    /// using (LagCompensation.For(attackerEntity))
    ///     count = ScenePhysics.OverlapSphereNonAlloc(center, radius, hits, mask);
    /// </code></example>
    /// <remarks>
    /// Scopes nest: only the outermost one rewinds. Keep the scope around the query only (not around damage code that
    /// may move or destroy things). Fish-Net's own collider rollback is part of Fish-Net Pro; this is a small
    /// self-contained replacement that works with the free version.
    /// </remarks>
    public static class LagCompensation
    {
        /// <summary>The network layer's rewind implementation.</summary>
        public interface IProvider
        {
            /// <summary>Moves hittable objects back to where <paramref name="attacker"/> saw them. False = nothing done.</summary>
            bool Rewind(CombatEntity attacker);

            /// <summary>Puts everything moved by the last <see cref="Rewind"/> back.</summary>
            void Restore();
        }

        /// <summary>Installed by the network layer while a server runs.</summary>
        public static IProvider Provider;

        private static int depth;
        private static bool rewound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Provider = null;
            depth = 0;
            rewound = false;
        }

        /// <summary>Is a rewind in effect right now?</summary>
        public static bool IsRewound => rewound;

        /// <summary>Rewinds the world for <paramref name="attacker"/> until the returned scope is disposed.</summary>
        public static Scope For(CombatEntity attacker)
        {
            depth++;
            if (depth == 1 && attacker != null && Provider != null && NetAuthority.IsNetworked && NetAuthority.IsServer)
            {
                try
                {
                    rewound = Provider.Rewind(attacker);
                }
                catch (Exception e)
                {
                    rewound = false;
                    Debug.LogException(e);
                }
            }
            return new Scope(true);
        }

        /// <summary>Rewinds for the entity that <paramref name="go"/> belongs to (see <see cref="CombatEntity.Resolve(GameObject)"/>).</summary>
        public static Scope For(GameObject go) => For(go != null ? CombatEntity.Resolve(go) : null);

        private static void Exit()
        {
            if (depth <= 0)
                return;
            depth--;
            if (depth == 0 && rewound)
            {
                rewound = false;
                try
                {
                    Provider?.Restore();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>Ends the rewind when disposed (use with <c>using</c>).</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly bool active;

            internal Scope(bool active) => this.active = active;

            public void Dispose()
            {
                if (active)
                    Exit();
            }
        }
    }

    /// <summary>
    /// A ring buffer of poses by tick, sampled with interpolation between ticks. Used by the lag-compensation provider
    /// (one per hittable object); pure data, unit-tested.
    /// </summary>
    public sealed class PoseHistory
    {
        private readonly uint[] ticks;
        private readonly Vector3[] positions;
        private readonly Quaternion[] rotations;
        private int head = -1;
        private int count;

        public PoseHistory(int capacity = 64)
        {
            capacity = Mathf.Max(2, capacity);
            ticks = new uint[capacity];
            positions = new Vector3[capacity];
            rotations = new Quaternion[capacity];
        }

        public int Count => count;
        public int Capacity => ticks.Length;

        /// <summary>The newest recorded tick (0 when empty).</summary>
        public uint NewestTick => count > 0 ? ticks[head] : 0u;

        /// <summary>The oldest recorded tick (0 when empty).</summary>
        public uint OldestTick => count > 0 ? ticks[(head - count + 1 + ticks.Length) % ticks.Length] : 0u;

        public void Clear()
        {
            head = -1;
            count = 0;
        }

        /// <summary>Records the pose at <paramref name="tick"/> (ticks must increase; a repeated tick replaces the last entry).</summary>
        public void Record(uint tick, Vector3 position, Quaternion rotation)
        {
            if (count > 0 && tick <= ticks[head])
            {
                if (tick < ticks[head])
                    Clear(); // time went backwards (server restarted): start over
                else
                {
                    positions[head] = position;
                    rotations[head] = rotation;
                    return;
                }
            }
            head = (head + 1) % ticks.Length;
            ticks[head] = tick;
            positions[head] = position;
            rotations[head] = rotation;
            if (count < ticks.Length)
                count++;
        }

        /// <summary>
        /// The pose at a (fractional) tick: interpolated between the two recorded ticks around it, clamped to the oldest
        /// and newest entries. False when nothing is recorded.
        /// </summary>
        public bool Sample(double tick, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (count == 0)
                return false;
            int newest = head;
            if (tick >= ticks[newest])
            {
                position = positions[newest];
                rotation = rotations[newest];
                return true;
            }
            int cap = ticks.Length;
            int later = newest;
            for (int i = 1; i < count; i++)
            {
                int idx = (newest - i + cap) % cap;
                if (ticks[idx] <= tick)
                {
                    double span = (double)ticks[later] - ticks[idx];
                    float t = span > 0d ? (float)((tick - ticks[idx]) / span) : 0f;
                    position = Vector3.Lerp(positions[idx], positions[later], t);
                    rotation = Quaternion.Slerp(rotations[idx], rotations[later], t);
                    return true;
                }
                later = idx;
            }
            position = positions[later]; // older than everything recorded: the oldest pose
            rotation = rotations[later];
            return true;
        }
    }
}
