#if FISHNET
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Timing;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Server-side lag compensation (Fish-Net's collider rollback is a Pro feature; this is a small replacement): every
    /// tick it records where each <see cref="LagCompensated"/> object is; when a hit query runs for a player (inside
    /// <see cref="LagCompensation.For(CombatEntity)"/>), everything else is moved back by that player's delay - the
    /// time between what their screen showed and the moment their action reached the server - and put back after.
    /// </summary>
    /// <remarks>
    /// Combat relays report each player's view with <see cref="ReportView"/> (the client's
    /// <c>TimeManager.GetPreciseTick(TickType.LastPacketTick)</c> sent with every attack or cast). The delay is
    /// clamped to <see cref="maxRewindSeconds"/>, so a client cannot claim an arbitrarily old view.
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetLagCompensation : MonoBehaviour, LagCompensation.IProvider
    {
        [Tooltip("Longest rewind allowed (seconds). Players with more latency are compensated only this much.")]
        [Range(0f, 1f)] public float maxRewindSeconds = 0.35f;

        [Tooltip("Remote objects are drawn this many ticks in the past on clients (NetworkTransform interpolation).")]
        [Range(0, 8)] public int interpolationTicks = 2;

        [Tooltip("How quickly a player's measured delay follows new reports (0..1).")]
        [Range(0.05f, 1f)] public float smoothing = 0.35f;

        [Tooltip("Ticks of history kept per object.")]
        [Range(8, 256)] public int historyTicks = 64;

        private static readonly List<LagCompensated> tracked = new List<LagCompensated>();
        private readonly Dictionary<int, double> delayTicks = new Dictionary<int, double>();
        private readonly List<LagCompensated> moved = new List<LagCompensated>();
        private NetworkManager manager;
        private TimeManager time;
        private bool subscribed;

        public static NetLagCompensation Instance { get; private set; }

        internal static void Track(LagCompensated c)
        {
            if (c != null && !tracked.Contains(c))
                tracked.Add(c);
        }

        internal static void Untrack(LagCompensated c) => tracked.Remove(c);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => tracked.Clear();

        private void Awake()
        {
            Instance = this;
            manager = GetComponentInParent<NetworkManager>();
        }

        private void OnEnable()
        {
            NetAuthority.ModeChanged += OnModeChanged;
            OnModeChanged(NetAuthority.Mode);
        }

        private void OnDisable()
        {
            NetAuthority.ModeChanged -= OnModeChanged;
            Subscribe(false);
            if (ReferenceEquals(LagCompensation.Provider, this))
                LagCompensation.Provider = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnModeChanged(NetMode mode)
        {
            bool server = mode == NetMode.Host || mode == NetMode.Server;
            Subscribe(server);
            if (server)
                LagCompensation.Provider = this;
            else if (ReferenceEquals(LagCompensation.Provider, this))
                LagCompensation.Provider = null;
            if (!server)
                delayTicks.Clear();
        }

        private void Subscribe(bool on)
        {
            if (manager == null)
                manager = GetComponentInParent<NetworkManager>();
            if (manager == null || on == subscribed)
                return;
            time = manager.TimeManager;
            subscribed = on;
            if (on)
                time.OnPostTick += OnPostTick;
            else
                time.OnPostTick -= OnPostTick;
        }

        private void OnPostTick()
        {
            uint tick = time.Tick;
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                LagCompensated c = tracked[i];
                if (c == null)
                {
                    tracked.RemoveAt(i);
                    continue;
                }
                if (c.isActiveAndEnabled)
                    c.Record(tick, historyTicks);
            }
        }

        private double MaxRewindTicks => time != null ? maxRewindSeconds / time.TickDelta : 0d;

        /// <summary>
        /// Records what a client was looking at when it acted (server side): <paramref name="lastPacketTick"/> is the
        /// client's <c>TimeManager.GetPreciseTick(TickType.LastPacketTick)</c>, sent with the action.
        /// </summary>
        public void ReportView(NetworkConnection conn, uint lastPacketTick, double percent)
        {
            if (conn == null || !conn.IsValid || time == null || lastPacketTick == 0 || lastPacketTick == TimeManager.UNSET_TICK)
                return;
            double now = time.Tick;
            double seen = lastPacketTick + Mathf.Clamp01((float)percent) - interpolationTicks;
            double d = now - seen;
            if (d < 0d)
                d = 0d;
            d = System.Math.Min(d, MaxRewindTicks);
            if (delayTicks.TryGetValue(conn.ClientId, out double old))
                d = old + (d - old) * smoothing;
            delayTicks[conn.ClientId] = d;
        }

        /// <summary>The current delay of a client in seconds (0 when unknown).</summary>
        public float DelaySeconds(NetworkConnection conn) =>
            conn != null && time != null && delayTicks.TryGetValue(conn.ClientId, out double d) ? (float)(d * time.TickDelta) : 0f;

        public void Forget(NetworkConnection conn)
        {
            if (conn != null)
                delayTicks.Remove(conn.ClientId);
        }

        public bool Rewind(CombatEntity attacker)
        {
            if (time == null || attacker == null)
                return false;
            NetworkConnection conn = NetObjects.OwnerOf(attacker);
            if (conn == null || conn.IsLocalClient || !delayTicks.TryGetValue(conn.ClientId, out double d) || d < 0.5d)
                return false; // mobs, the host's own player (no latency) and players without a report are not rewound
            double target = time.Tick - d;
            Transform attackerRoot = attacker.transform.root;
            moved.Clear();
            for (int i = 0; i < tracked.Count; i++)
            {
                LagCompensated c = tracked[i];
                if (c == null || !c.isActiveAndEnabled || c.transform.root == attackerRoot)
                    continue;
                if (c.gameObject.scene != attacker.gameObject.scene)
                    continue;
                if (c.RewindTo(target))
                    moved.Add(c);
            }
            if (moved.Count == 0)
                return false;
            Physics.SyncTransforms();
            return true;
        }

        public void Restore()
        {
            for (int i = 0; i < moved.Count; i++)
                if (moved[i] != null)
                    moved[i].RestorePose();
            moved.Clear();
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// Marks an object whose colliders can be hit by players, so <see cref="NetLagCompensation"/> records its pose every
    /// tick and can move it back for a hit query. Added to players and mobs on the server by the network layer.
    /// </summary>
    [DisallowMultipleComponent]
    public class LagCompensated : MonoBehaviour
    {
        private PoseHistory history;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private bool saved;

        private void OnEnable() => NetLagCompensation.Track(this);

        private void OnDisable()
        {
            NetLagCompensation.Untrack(this);
            history?.Clear();
        }

        internal void Record(uint tick, int capacity)
        {
            if (history == null || history.Capacity != capacity)
                history = new PoseHistory(capacity);
            history.Record(tick, transform.position, transform.rotation);
        }

        internal bool RewindTo(double tick)
        {
            if (history == null || history.Count == 0 || !history.Sample(tick, out Vector3 p, out Quaternion r))
                return false;
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            saved = true;
            transform.SetPositionAndRotation(p, r);
            return true;
        }

        internal void RestorePose()
        {
            if (!saved)
                return;
            saved = false;
            transform.SetPositionAndRotation(savedPosition, savedRotation);
        }
    }
}
#endif
