#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// The hub world's global relay: carries the server's dimensional cycle to every client and the hub world's messages
    /// to the players they are for. Put it on the "Network World" object of the Bootstrap scene (a scene NetworkObject
    /// observed by every client); it finds <see cref="HubWorldDirector.Instance"/> by itself, whenever the City (where the
    /// director lives) is loaded.
    /// </summary>
    /// <remarks>
    /// <para><b>Server.</b> The director decides everything. This component reads its state every frame into one SyncVar
    /// (<see cref="State"/>: cycle index, network time of the next jump, cycle length, world seed, modifier indexes,
    /// jumping, gate open), sends the cycle's events as RPCs (warnings, the jump, the loot lost between dimensions), applies
    /// the shard seed given on the command line (<see cref="NetShardSeed"/>) and installs
    /// <see cref="HubWorldMessages.Remote"/>, so a message for one player reaches that player's client and a message for
    /// everyone reaches every client.</para>
    /// <para><b>Clients.</b> The director is a mirror (<see cref="HubWorldDirector.IsMirror"/>): this writes the server's
    /// state into it, so the HUD countdown, the sky and sounds, the gate and the terrain seed match the server. The jump
    /// time travels as network (tick) time, so each client counts down locally without a message per second.</para>
    /// <para>Only the WORLD seed is sent - never the shard seed (it would reveal future worlds and the loot-loss rolls)
    /// and never a dungeon seed.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetHubWorld : NetworkBehaviour
    {
        /// <summary>The server's cycle as sent to the clients.</summary>
        public struct CycleState : IEquatable<CycleState>
        {
            /// <summary>The current cycle.</summary>
            public int cycleIndex;
            /// <summary>Network time (server tick time, seconds) of the next jump.</summary>
            public double jumpAt;
            /// <summary>Length of a cycle in seconds (0 = nothing sent yet).</summary>
            public float duration;
            /// <summary>The current world's seed (never the shard seed).</summary>
            public int worldSeed;
            /// <summary>Index of the current modifier in the settings' list (-1 = none).</summary>
            public int currentModifier;
            /// <summary>Index of the next cycle's modifier (-1 = none).</summary>
            public int nextModifier;
            public bool jumping;
            public bool gateOpen;
            /// <summary>The World scene's state on the server (clients track their own copy).</summary>
            public WorldLoadState serverWorld;

            public bool Equals(CycleState o) =>
                cycleIndex == o.cycleIndex && jumpAt.Equals(o.jumpAt) && duration.Equals(o.duration) && worldSeed == o.worldSeed &&
                currentModifier == o.currentModifier && nextModifier == o.nextModifier && jumping == o.jumping &&
                gateOpen == o.gateOpen && serverWorld == o.serverWorld;

            public override bool Equals(object obj) => obj is CycleState o && Equals(o);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = cycleIndex;
                    h = h * 397 ^ jumpAt.GetHashCode();
                    h = h * 397 ^ worldSeed;
                    h = h * 397 ^ currentModifier;
                    h = h * 397 ^ nextModifier;
                    return h * 397 ^ (jumping ? 1 : 0) ^ (gateOpen ? 2 : 0) ^ (int)serverWorld << 2;
                }
            }
        }

        /// <summary>Longest message text sent over the network (longer texts are cut).</summary>
        public const int MaxMessageLength = 512;

        /// <summary>The relay of this session (null outside one).</summary>
        public static NetHubWorld Instance { get; private set; }

        /// <summary>The server's cycle (sent to every observer - every client - each tick it changes).</summary>
        public readonly SyncVar<CycleState> State = new SyncVar<CycleState>(new SyncTypeSettings(0f));

        [Tooltip("Seconds the network time of the next jump may drift from the server's cycle clock before it is sent again.")]
        [SerializeField, Min(0.05f)] private float resendDrift = 0.25f;

        private HubWorldDirector boundDirector;   // server: the director whose events are relayed
        private HubWorldDirector appliedDirector; // client: the director the state was last written to
        private bool stateDirty = true;
        private bool shardSeedApplied;

        /// <summary>The World scene's state on the server (clients load their own copy only when their player goes there).</summary>
        public WorldLoadState ServerWorldState => State.Value.serverWorld;

        /// <summary>The network clock in seconds: the server's tick time (a client's estimate of it).</summary>
        public static double NetworkTime(TimeManager timeManager) =>
            timeManager != null ? timeManager.TicksToTime(timeManager.GetPreciseTick(TickType.Tick)) : Time.unscaledTimeAsDouble;

        private double Now => NetworkTime(TimeManager);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[Net] Two NetHubWorld relays ('{Instance.name}', '{name}') - keep one on the Network World object.", this);
            Instance = this;
            State.OnChange += OnStateChanged;
        }

        private void OnDestroy()
        {
            State.OnChange -= OnStateChanged;
            Unbind();
            UninstallMessages();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            shardSeedApplied = false;
            HubWorldMessages.Remote = DeliverRemote;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            UninstallMessages();
            Unbind();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            stateDirty = true;
            appliedDirector = null;
        }

        private void UninstallMessages()
        {
            if (HubWorldMessages.Remote == (Action<CombatEntity, string, HubMessageKind>)DeliverRemote)
                HubWorldMessages.Remote = null;
        }

        private void OnStateChanged(CycleState previous, CycleState next, bool asServer)
        {
            if (!asServer)
                stateDirty = true;
        }

        private void Update()
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            if (IsServerInitialized)
                ServerUpdate(director);
            else if (IsClientInitialized)
                ClientUpdate(director);
        }

        // ------------------------------------------------------------------ server

        private void ServerUpdate(HubWorldDirector director)
        {
            if (director == null)
                return;
            if (boundDirector != director)
                Bind(director);
            if (!shardSeedApplied)
            {
                shardSeedApplied = true;
                if (NetShardSeed.Value.HasValue && NetShardSeed.Value.Value != 0)
                    director.UseShardSeed(NetShardSeed.Value.Value);
            }
            CycleState next = BuildState(director);
            CycleState current = State.Value;
            // The clients count down locally: the jump time is only sent again when the cycle changed or it drifted.
            if (current.cycleIndex == next.cycleIndex && current.duration > 0f && Math.Abs(current.jumpAt - next.jumpAt) <= resendDrift)
                next.jumpAt = current.jumpAt;
            if (!next.Equals(current))
                State.Value = next;
        }

        private CycleState BuildState(HubWorldDirector director) => new CycleState
        {
            cycleIndex = director.CycleIndex,
            jumpAt = Now + director.SecondsUntilJump,
            duration = (float)director.CycleDuration,
            worldSeed = director.WorldSeed,
            currentModifier = director.ModifierIndex(director.CurrentModifier),
            nextModifier = director.ModifierIndex(director.NextModifier),
            jumping = director.IsJumping,
            gateOpen = director.GateOpen,
            serverWorld = director.WorldState,
        };

        private void Bind(HubWorldDirector director)
        {
            Unbind();
            boundDirector = director;
            director.CycleWarning += OnServerWarning;
            director.CycleJumped += OnServerJumped;
            director.PlayerLostBetweenDimensions += OnServerLost;
        }

        private void Unbind()
        {
            if (boundDirector != null)
            {
                boundDirector.CycleWarning -= OnServerWarning;
                boundDirector.CycleJumped -= OnServerJumped;
                boundDirector.PlayerLostBetweenDimensions -= OnServerLost;
            }
            boundDirector = null;
        }

        private void OnServerWarning(float secondsLeft)
        {
            if (IsServerInitialized)
                RpcWarning(secondsLeft);
        }

        private void OnServerJumped(int cycleIndex)
        {
            if (!IsServerInitialized || boundDirector == null)
                return;
            // The new world goes with the event, so no client hears the jump before it knows the new seed.
            CycleState state = BuildState(boundDirector);
            State.Value = state;
            RpcJump(cycleIndex, state);
        }

        private void OnServerLost(CombatEntity player, CyclePenalties.LootLossResult loss)
        {
            if (!IsServerInitialized)
                return;
            NetworkConnection conn = NetObjects.OwnerOf(player);
            if (conn == null || !conn.IsActive || conn.IsLocalClient)
                return; // the host's own player saw the event here
            string[] lines = loss.lines != null ? loss.lines.ToArray() : Array.Empty<string>();
            TargetLost(conn, loss.itemsLost, loss.stacksHit, loss.currencyLost, lines);
        }

        /// <summary><see cref="HubWorldMessages.Remote"/>: a message for one player goes to their client, a message for everyone to every client.</summary>
        private void DeliverRemote(CombatEntity to, string text, HubMessageKind kind)
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(text))
                return;
            if (text.Length > MaxMessageLength)
                text = text.Substring(0, MaxMessageLength);
            if (to == null)
            {
                RpcMessage(text, kind);
                return;
            }
            NetworkConnection conn = NetObjects.OwnerOf(to);
            if (conn == null || !conn.IsActive || conn.IsLocalClient)
                return; // not a remote client's player (the host's own player reads it here)
            TargetMessage(conn, text, kind);
        }

        // ------------------------------------------------------------------ client

        private void ClientUpdate(HubWorldDirector director)
        {
            if (director == null)
                return;
            CycleState state = State.Value;
            if (state.duration <= 0f)
                return; // nothing received yet
            if (appliedDirector != director)
            {
                appliedDirector = director; // a new City load brought the director: give it the whole state
                stateDirty = true;
            }
            if (stateDirty)
            {
                stateDirty = false;
                director.ApplyServerWorld(state.worldSeed, state.currentModifier, state.nextModifier);
                director.ApplyServerFlags(state.jumping, state.gateOpen);
            }
            director.ApplyServerCycle(state.cycleIndex, state.jumpAt - Now, state.duration);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWarning(float secondsLeft)
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            if (director != null)
                director.ShowServerWarning(secondsLeft);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcJump(int cycleIndex, CycleState state)
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            if (director == null)
                return;
            director.ApplyServerWorld(state.worldSeed, state.currentModifier, state.nextModifier);
            director.ApplyServerCycle(state.cycleIndex, state.jumpAt - Now, state.duration);
            director.ShowServerJump(cycleIndex);
            director.ApplyServerFlags(state.jumping, state.gateOpen);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcMessage(string text, HubMessageKind kind) => HubWorldMessages.DeliverLocal(null, text, kind);

        [TargetRpc]
        private void TargetMessage(NetworkConnection conn, string text, HubMessageKind kind) =>
            HubWorldMessages.DeliverLocal(HubWorldDirector.LocalPlayer, text, kind);

        [TargetRpc]
        private void TargetLost(NetworkConnection conn, int itemsLost, int stacksHit, int currencyLost, string[] lines)
        {
            HubWorldDirector director = HubWorldDirector.Instance;
            CombatEntity local = HubWorldDirector.LocalPlayer;
            if (director != null && local != null)
                director.ShowLostBetweenDimensions(local, itemsLost, stacksHit, currencyLost, lines);
        }
    }
}
#endif
