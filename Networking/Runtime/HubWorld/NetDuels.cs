#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Duels over the network: the server's <see cref="DuelSystem"/> stays the authority, this global relay sends its open
    /// duels to every client and carries the clients' requests (challenge, accept, decline, forfeit) to it. Put it on the
    /// "Network World" object of the Bootstrap scene, next to <see cref="NetHubWorld"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Replicated.</b> <see cref="Duels"/> lists every open duel (players by <see cref="PlayerIds"/>, state,
    /// deadline in network time, arena centre). Each client's duel system copies it
    /// (<see cref="DuelSystem.ApplyReplicated"/>), so <c>CombatRules.PlayerVersusPlayer</c> answers Opponents for two
    /// fighters there too, the HUD shows the prompt and the countdown, and the arena ring appears around the fighters on
    /// every client that sees them.</para>
    /// <para><b>Requests.</b> A client's duel system checks a request locally for a quick answer and sends it here
    /// (<see cref="DuelSystem.ClientRequest"/>); the server finds the asking player from the connection (a client can only
    /// act for its own player), rate-limits it and runs the same rules as single player - range and realm measured on the
    /// server, both players alive, nobody already in a duel. The "Challenge to a duel" interaction relayed by the economy
    /// module's interactor reaches <see cref="DuelChallengeTarget.Interact"/> on the server directly; a second, identical
    /// challenge arriving through this relay is ignored silently.</para>
    /// <para>Messages ("X challenges you to a duel!", "Fight!") are sent by the server's duel system through
    /// <see cref="HubWorldMessages"/>, which <see cref="NetHubWorld"/> delivers to the players concerned.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetDuels : NetworkBehaviour
    {
        /// <summary>One open duel as sent to the clients.</summary>
        public struct DuelView : IEquatable<DuelView>
        {
            public int id;
            public int challenger;
            public int challenged;
            public DuelState state;
            /// <summary>Network time (server tick time, seconds) of the duel's deadline.</summary>
            public double deadline;
            public int winner;
            public DuelEndReason reason;
            public bool arena;
            public Vector3 center;

            public bool Equals(DuelView o) =>
                id == o.id && challenger == o.challenger && challenged == o.challenged && state == o.state &&
                deadline.Equals(o.deadline) && winner == o.winner && reason == o.reason && arena == o.arena && center == o.center;

            public override bool Equals(object obj) => obj is DuelView o && Equals(o);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = id;
                    h = h * 397 ^ challenger;
                    h = h * 397 ^ challenged;
                    return h * 397 ^ (int)state;
                }
            }
        }

        /// <summary>The relay of this session (null outside one).</summary>
        public static NetDuels Instance { get; private set; }

        /// <summary>The server's open duels (every client observes this object).</summary>
        public readonly SyncList<DuelView> Duels = new SyncList<DuelView>(new SyncTypeSettings(0f));

        [Tooltip("Seconds between two duel requests of the same client (extra requests are dropped).")]
        [SerializeField, Min(0.05f)] private float requestInterval = 0.25f;

        private readonly List<DuelSnapshot> snapshots = new List<DuelSnapshot>();
        private readonly List<DuelView> views = new List<DuelView>();
        private readonly Dictionary<int, (DuelState state, double deadline)> sentDeadlines = new Dictionary<int, (DuelState, double)>();
        private readonly Dictionary<int, float> nextRequest = new Dictionary<int, float>();
        private DuelSystem boundSystem;   // server: the duel system whose changes are sent
        private DuelSystem appliedSystem; // client: the duel system the list was last written to
        private bool serverDirty = true;
        private bool clientDirty = true;
        private float nextClientRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private double Now => NetHubWorld.NetworkTime(TimeManager);

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[Net] Two NetDuels relays ('{Instance.name}', '{name}') - keep one on the Network World object.", this);
            Instance = this;
            Duels.OnChange += OnDuelsChanged;
        }

        private void OnDestroy()
        {
            Duels.OnChange -= OnDuelsChanged;
            Unbind();
            UninstallHooks();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            DuelSystem.NameResolver = ResolveName;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            UninstallHooks();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            serverDirty = true;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Unbind();
            nextRequest.Clear();
            sentDeadlines.Clear();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            clientDirty = true;
            appliedSystem = null;
            if (!IsServerInitialized)
                DuelSystem.ClientRequest = SendRequest;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (DuelSystem.ClientRequest == (Action<DuelSystem.DuelRequest, CombatEntity, CombatEntity>)SendRequest)
                DuelSystem.ClientRequest = null;
        }

        private void UninstallHooks()
        {
            if (DuelSystem.NameResolver == (Func<CombatEntity, string>)ResolveName)
                DuelSystem.NameResolver = null;
            if (DuelSystem.ClientRequest == (Action<DuelSystem.DuelRequest, CombatEntity, CombatEntity>)SendRequest)
                DuelSystem.ClientRequest = null;
        }

        private void OnDuelsChanged(SyncListOperation op, int index, DuelView previous, DuelView next, bool asServer)
        {
            if (!asServer)
                clientDirty = true;
        }

        private void Update()
        {
            if (IsServerInitialized)
                ServerUpdate();
            else if (IsClientInitialized)
                ClientUpdate();
        }

        /// <summary>The name every machine shows for a player: their network display name.</summary>
        private static string ResolveName(CombatEntity e)
        {
            NetworkObject nob = NetObjects.Of(e);
            NetPlayer player = nob != null ? nob.GetComponent<NetPlayer>() : null;
            return player != null ? player.DisplayName.Value : null;
        }

        // ------------------------------------------------------------------ server

        private void ServerUpdate()
        {
            DuelSystem duels = DuelSystem.Instance;
            if (duels != boundSystem)
            {
                Unbind();
                boundSystem = duels;
                if (duels != null)
                    duels.DuelChanged += OnServerDuelChanged;
                serverDirty = true;
            }
            if (!serverDirty || duels == null)
                return;
            serverDirty = false;
            Publish(duels);
        }

        private void Unbind()
        {
            if (boundSystem != null)
                boundSystem.DuelChanged -= OnServerDuelChanged;
            boundSystem = null;
        }

        private void OnServerDuelChanged(Duel d) => serverDirty = true;

        /// <summary>Writes the open duels into the list (only the entries that changed are sent).</summary>
        private void Publish(DuelSystem duels)
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            double localNow = director != null ? director.Now : Time.timeAsDouble;
            double netNow = Now;
            duels.Snapshot(snapshots);
            views.Clear();
            var live = new HashSet<int>();
            foreach (DuelSnapshot s in snapshots)
            {
                live.Add(s.id);
                // A deadline is converted once per state, so an unchanged duel is never sent again for clock jitter.
                double deadline;
                if (sentDeadlines.TryGetValue(s.id, out var sent) && sent.state == s.state)
                    deadline = sent.deadline;
                else
                {
                    deadline = s.deadline >= double.MaxValue * 0.5 ? double.MaxValue : netNow + (s.deadline - localNow);
                    sentDeadlines[s.id] = (s.state, deadline);
                }
                views.Add(new DuelView
                {
                    id = s.id,
                    challenger = s.challenger,
                    challenged = s.challenged,
                    state = s.state,
                    deadline = deadline,
                    winner = s.winner,
                    reason = s.reason,
                    arena = s.arena,
                    center = s.center,
                });
            }
            var gone = new List<int>();
            foreach (int id in sentDeadlines.Keys)
                if (!live.Contains(id))
                    gone.Add(id);
            foreach (int id in gone)
                sentDeadlines.Remove(id);

            for (int i = 0; i < views.Count; i++)
            {
                if (i < Duels.Count)
                {
                    if (!Duels[i].Equals(views[i]))
                        Duels[i] = views[i];
                }
                else
                    Duels.Add(views[i]);
            }
            while (Duels.Count > views.Count)
                Duels.RemoveAt(Duels.Count - 1);
        }

        /// <summary>A client's duel request: the asking player is the caller's own player, never a value it sent.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerDuelRequest(DuelSystem.DuelRequest request, int targetId, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive)
                return;
            float now = Time.unscaledTime;
            if (nextRequest.TryGetValue(conn.ClientId, out float allowedAt) && now < allowedAt)
                return;
            nextRequest[conn.ClientId] = now + requestInterval;

            DuelSystem duels = DuelSystem.Instance;
            CombatEntity me = NetObjects.PlayerOf(conn);
            if (duels == null || me == null || me.IsDead)
                return;
            string reason = null;
            switch (request)
            {
                case DuelSystem.DuelRequest.Challenge:
                {
                    CombatEntity target = PlayerIds.Find(targetId);
                    if (target == null || target == me)
                        return;
                    // The same challenge may arrive twice (the relayed interaction and this request): keep the first.
                    Duel open = duels.DuelOf(me);
                    if (open != null && open.State == DuelState.Challenged && open.Challenger == PlayerIds.Of(me) && open.Challenged == PlayerIds.Of(target))
                        return;
                    if (!duels.Challenge(me, target, out reason))
                        break;
                    return;
                }
                case DuelSystem.DuelRequest.Accept:
                    duels.Accept(me);
                    return;
                case DuelSystem.DuelRequest.Decline:
                    duels.Decline(me);
                    return;
                case DuelSystem.DuelRequest.Forfeit:
                    duels.Forfeit(me);
                    return;
                default:
                    return;
            }
            if (!string.IsNullOrEmpty(reason))
                HubWorldMessages.Send(me, reason, HubMessageKind.Warning);
        }

        // ------------------------------------------------------------------ clients

        private void ClientUpdate()
        {
            DuelSystem duels = DuelSystem.Instance;
            if (duels == null)
                return; // applied once the City (and the director's duel system) is loaded here
            if (appliedSystem != duels)
            {
                appliedSystem = duels;
                clientDirty = true;
            }
            // Also once a second: a fighter who only now came into view gets the arena ring (unchanged duels raise nothing).
            if (!clientDirty && (Duels.Count == 0 || Time.unscaledTime < nextClientRefresh))
                return;
            clientDirty = false;
            nextClientRefresh = Time.unscaledTime + 1f;
            HubWorldDirector director = HubWorldDirector.Instance;
            double localNow = director != null ? director.Now : Time.timeAsDouble;
            double netNow = Now;
            snapshots.Clear();
            for (int i = 0; i < Duels.Count; i++)
            {
                DuelView v = Duels[i];
                snapshots.Add(new DuelSnapshot
                {
                    id = v.id,
                    challenger = v.challenger,
                    challenged = v.challenged,
                    state = v.state,
                    deadline = v.deadline >= double.MaxValue * 0.5 ? double.MaxValue : localNow + (v.deadline - netNow),
                    winner = v.winner,
                    reason = v.reason,
                    arena = v.arena,
                    center = v.center,
                });
            }
            duels.ApplyReplicated(snapshots);
        }

        /// <summary><see cref="DuelSystem.ClientRequest"/>: sends a request (the target by its network-stable id).</summary>
        private void SendRequest(DuelSystem.DuelRequest request, CombatEntity player, CombatEntity target)
        {
            if (!IsClientInitialized)
                return;
            ServerDuelRequest(request, target != null ? PlayerIds.Of(target) : 0);
        }
    }
}
#endif
