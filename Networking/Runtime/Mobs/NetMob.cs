#if FISHNET
using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Utility.Template;
using HubWorld;
using ProceduralDungeon;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks a mob. Put it on the root of every mob prefab, next to its NetworkObject, a NetworkTransform, the status
    /// module's <see cref="NetCombatStatus"/> and the abilities module's <see cref="NetAbilityCaster"/>.
    /// </summary>
    /// <remarks>
    /// <para>The server (or host) runs the mob exactly as in single player: senses, decisions, threat, abilities, the
    /// NavMeshAgent and death. This component adds <see cref="LagCompensated"/> there (so players' hits are checked
    /// against where they saw the mob) and polls, each tick, what clients need to show it: the AI state with a few flags
    /// and the movement speeds (<see cref="MobReplicaState"/>), who it is after (for target indicators), and its name,
    /// strength and elite flag (for nameplates). Unchanged values are not sent again.</para>
    /// <para>On a pure client the copy is a replica (<see cref="MobMovementStateMachine.IsReplica"/>): its AI and
    /// NavMeshAgent are off, the NetworkTransform moves it, and its <see cref="MobAnimationDriver"/> animates it from the
    /// replicated state and the speed it is seen moving at - no NetworkAnimator is needed. Alerts and dodges are repeated
    /// with an observers RPC; hits and deaths come from the status module (<see cref="CombatEntity.ReplicatedDamage"/>,
    /// <see cref="CombatEntity.ReplicatedLifeChanged"/>).</para>
    /// <para>Clients send nothing to a mob: there is no ServerRpc here.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetMob : TickNetworkBehaviour
    {
        /// <summary>What clients show about a mob besides its state (wire type).</summary>
        public struct Identity : IEquatable<Identity>
        {
            /// <summary>The name shown (<see cref="Mob.DisplayName"/>, or the dungeon's label).</summary>
            public string displayName;
            /// <summary>Strength multiplier (zone strength; 1 = normal).</summary>
            public float strength;
            /// <summary>Elite or champion.</summary>
            public bool elite;

            public bool Equals(Identity o) => displayName == o.displayName && strength == o.strength && elite == o.elite;
            public override bool Equals(object obj) => obj is Identity o && Equals(o);
            public override int GetHashCode() => ((displayName != null ? displayName.GetHashCode() : 0) * 397 ^ strength.GetHashCode()) * 397 ^ (elite ? 1 : 0);
        }

        /// <summary>Longest name sent (characters).</summary>
        public const int MaxNameLength = 64;

        [Tooltip("Seconds between the server's checks of the mob's name, strength and elite flag (they rarely change after it spawns).")]
        [Min(0.1f)] public float identityCheckInterval = 1f;

        private readonly SyncVar<MobReplicaState> state = new SyncVar<MobReplicaState>(new SyncTypeSettings(0f));
        private readonly SyncVar<NetworkObject> target = new SyncVar<NetworkObject>();
        private readonly SyncVar<Identity> identity = new SyncVar<Identity>(new Identity { displayName = "", strength = 1f });

        private Mob mob;
        private MobMovementStateMachine machine;
        private bool serverHooked;
        private float nextIdentityCheck;

        /// <summary>Raised on clients when the replicated name, strength or elite flag changes (nameplates).</summary>
        public event Action<NetMob> IdentityChanged;

        /// <summary>Raised on clients when the mob's replicated target changes (target indicators).</summary>
        public event Action<NetMob> TargetChanged;

        /// <summary>The mob this component networks.</summary>
        public Mob Mob => mob;

        /// <summary>The AI state as last replicated (the server's current one on the server).</summary>
        public MobReplicaState State => state.Value;

        /// <summary>Name, strength and elite flag as last replicated.</summary>
        public Identity Info => identity.Value;

        /// <summary>
        /// Who the mob is after: the brain's target where the AI runs, the replicated one on clients (null when nobody, or
        /// when that character is not visible on this client).
        /// </summary>
        public CombatEntity Target
        {
            get
            {
                if (machine != null && !machine.IsReplica)
                    return machine.VisibleTarget;
                NetworkObject nob = target.Value;
                return nob != null ? CombatEntity.Resolve(nob.gameObject) : null;
            }
        }

        /// <summary>The NetMob of a mob (null when it is not networked).</summary>
        public static NetMob Of(Mob m) => m != null ? m.GetComponent<NetMob>() : null;

        private void Awake()
        {
            Resolve();
            SetTickCallbacks(TickCallback.Tick);
            state.OnChange += OnStateChanged;
            target.OnChange += OnTargetChanged;
            identity.OnChange += OnIdentityChanged;
        }

        private void OnDestroy()
        {
            HookServer(false);
            state.OnChange -= OnStateChanged;
            target.OnChange -= OnTargetChanged;
            identity.OnChange -= OnIdentityChanged;
        }

        private void Resolve()
        {
            if (mob == null)
                mob = GetComponent<Mob>();
            if (machine == null)
                machine = GetComponent<MobMovementStateMachine>();
        }

        // ------------------------------------------------------------------ server

        public override void OnStartServer()
        {
            base.OnStartServer();
            Resolve();
            if (mob == null)
                Debug.LogWarning($"[{name}] NetMob needs a Mob component on the same object.", this);
            if (GetComponent<LagCompensated>() == null)
                gameObject.AddComponent<LagCompensated>();
            if (machine != null)
                machine.SyncReplicaMode(); // the server always runs the AI
            HookServer(true);
            nextIdentityCheck = 0f;
            PollServer();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            HookServer(false);
        }

        private void HookServer(bool on)
        {
            if (on == serverHooked || machine == null)
                return;
            serverHooked = on;
            if (on)
                machine.AnimationEventPlayed += OnServerAnimationEvent;
            else
                machine.AnimationEventPlayed -= OnServerAnimationEvent;
        }

        protected override void TimeManager_OnTick()
        {
            if (IsServerInitialized)
                PollServer();
        }

        // Copies what clients need from the running AI into the synchronized state (equal values are not sent again).
        private void PollServer()
        {
            if (machine != null)
                state.Value = machine.CaptureReplicaState();
            target.Value = TargetObject();
            float now = Time.unscaledTime;
            if (now >= nextIdentityCheck)
            {
                nextIdentityCheck = now + Mathf.Max(0.1f, identityCheckInterval);
                identity.Value = CaptureIdentity();
            }
        }

        private NetworkObject TargetObject()
        {
            CombatEntity t = machine != null && machine.Context != null ? machine.Context.Brain.Target : null;
            if (t == null)
                return null;
            NetworkObject nob = NetObjects.Of(t);
            return nob != null && nob != NetworkObject && nob.IsSpawned ? nob : null;
        }

        private Identity CaptureIdentity()
        {
            var id = new Identity
            {
                displayName = mob != null ? mob.DisplayName : name,
                strength = mob != null ? mob.Strength : 1f,
                elite = mob != null && mob.IsElite,
            };
            // Set by the hub world's zones and the dungeon builder on the server only.
            ZoneMobStrength zone = GetComponent<ZoneMobStrength>();
            if (zone != null)
                id.strength = zone.strength;
            DungeonSpawned tag = GetComponent<DungeonSpawned>();
            if (tag != null)
            {
                id.elite |= tag.elite;
                if ((mob == null || !mob.HasCustomDisplayName) && !string.IsNullOrEmpty(tag.entryName))
                    id.displayName = tag.entryName;
            }
            id.displayName = Sanitize(id.displayName);
            id.strength = float.IsNaN(id.strength) || float.IsInfinity(id.strength) ? 1f : Mathf.Clamp(id.strength, 0f, 1000f);
            return id;
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength) : s;
        }

        private void OnServerAnimationEvent(MobAnimationEvent e)
        {
            if (!IsServerInitialized || !IsSpawned)
                return;
            // Hits are not sent: clients flinch at the hits the status module reports.
            if (e == MobAnimationEvent.Alert || e == MobAnimationEvent.Dodge)
                ObserversAnimationEvent(e);
        }

        // ------------------------------------------------------------------ client

        public override void OnStartClient()
        {
            base.OnStartClient();
            Resolve();
            if (IsServerInitialized)
                return; // the host runs the AI and shows it directly
            if (machine != null)
                machine.SyncReplicaMode(); // before the next frame, so the NavMeshAgent never steers the copy
            ApplyState(state.Value);
            ApplyTarget(target.Value);
            ApplyIdentity(identity.Value);
        }

        /// <summary>One-shot reaction the server's mob played (alert, dodge); the host already played it.</summary>
        [ObserversRpc(ExcludeServer = true)]
        private void ObserversAnimationEvent(MobAnimationEvent e)
        {
            if (IsServerInitialized || machine == null || !Enum.IsDefined(typeof(MobAnimationEvent), e))
                return;
            machine.PlayReplicatedAnimationEvent(e);
        }

        private bool AppliesOnClient(bool asServer) => !asServer && !IsServerInitialized;

        private void OnStateChanged(MobReplicaState prev, MobReplicaState next, bool asServer)
        {
            if (AppliesOnClient(asServer))
                ApplyState(next);
        }

        private void OnTargetChanged(NetworkObject prev, NetworkObject next, bool asServer)
        {
            if (AppliesOnClient(asServer))
                ApplyTarget(next);
        }

        private void OnIdentityChanged(Identity prev, Identity next, bool asServer)
        {
            if (AppliesOnClient(asServer))
                ApplyIdentity(next);
        }

        private void ApplyState(MobReplicaState s)
        {
            if (machine == null)
                return;
            machine.SyncReplicaMode();
            machine.ApplyReplicaState(s);
        }

        private void ApplyTarget(NetworkObject nob)
        {
            CombatEntity e = nob != null ? CombatEntity.Resolve(nob.gameObject) : null;
            if (machine != null)
                machine.ApplyReplicaTarget(e);
            TargetChanged?.Invoke(this);
        }

        private void ApplyIdentity(Identity id)
        {
            if (mob != null)
            {
                string shown = Sanitize(id.displayName);
                mob.DisplayName = shown;
                mob.Strength = float.IsNaN(id.strength) || float.IsInfinity(id.strength) ? 1f : Mathf.Clamp(id.strength, 0f, 1000f);
                mob.IsElite = id.elite;
                if (shown.Length > 0)
                    gameObject.name = shown;
            }
            IdentityChanged?.Invoke(this);
        }
    }

    /// <summary>Installs the mob network hooks of the always-compiled code (<see cref="MobNetSpawn"/>).</summary>
    internal static class NetMobHooks
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install() => MobNetSpawn.NetworkIdentityResolver = go => NetObjects.Of(go) != null;
    }
}
#endif
