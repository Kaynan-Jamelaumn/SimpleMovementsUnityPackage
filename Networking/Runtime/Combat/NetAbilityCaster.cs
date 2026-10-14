#if FISHNET
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks an <see cref="AbilityCaster"/> (players, mobs, summons): the server runs the unchanged casting pipeline
    /// and every client replays it.
    /// <list type="bullet">
    /// <item>Owner to server: the player's own machine sends cast, aim and cancel intentions (<see cref="IAbilityNetBridge"/>);
    /// the server validates them (ownership, slot, ability id, finite and clamped aim, rate limits, then the caster's own
    /// checks: cooldown, resources, range and line of sight judged with lag compensation) and casts, or tells the owner why
    /// not.</item>
    /// <item>Server to observers: every cast start, release, aim change and interrupt. Clients play a cosmetic copy of
    /// the cast (animation, effects, telegraphs, projectiles, hit feedback) with the server's numbers and seed; only the
    /// server applies damage, costs, cooldowns, summons and displacement.</item>
    /// <item>Server to owner: the slots' cooldowns and charges (seconds left), so the owner's UI and input gating match.</item>
    /// <item>Server to clients: player slot contents (ability ids, modifiers, labels, absorbed grants).</item>
    /// </list>
    /// Put it on the root of every networked caster prefab, next to the NetworkObject. Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetAbilityCaster : NetworkBehaviour, IAbilityNetBridge
    {
        /// <summary>When the slot contents are replicated.</summary>
        public enum SlotContentMode
        {
            /// <summary>Players only (their slots change at runtime: traits, absorbed abilities); mobs build theirs from the prefab.</summary>
            Auto,
            Always,
            Never,
        }

        [Tooltip("The caster to network. Empty = the AbilityCaster on this object or its children.")]
        [SerializeField] private AbilityCaster caster;
        [Tooltip("Replicate what each slot holds. Auto = players only (mob slots come from their prefab on every machine).")]
        [SerializeField] private SlotContentMode slotContents = SlotContentMode.Auto;
        [Tooltip("Times per second the aim of an ability that follows the crosshair until release is sent while it winds up.")]
        [SerializeField, Range(5f, 30f)] private float aimSendRate = 15f;
        [Tooltip("Times per second the server checks the slots for cooldown / charge changes to report to the owner.")]
        [SerializeField, Range(2f, 30f)] private float slotPollRate = 10f;

        /// <summary>Seconds a sent cast request blocks another request of the same slot (until the server answers).</summary>
        public const float PendingSeconds = 0.35f;
        /// <summary>Seconds the server keeps retrying a request refused for timing (cooldown / busy just ending).</summary>
        public const float InputBufferSeconds = 0.15f;
        /// <summary>Farthest aimed point accepted from a client (metres from the caster).</summary>
        public const float MaxAimDistance = 160f;

        private readonly SyncList<NetAbilitySlotContent> contents = new SyncList<NetAbilitySlotContent>();
        private readonly SyncVar<NetworkObject> summoner = new SyncVar<NetworkObject>();

        private static readonly Dictionary<CombatEntity, NetAbilityCaster> serverByEntity =
            new Dictionary<CombatEntity, NetAbilityCaster>(ReferenceComparer<CombatEntity>.Instance);
        private static readonly HashSet<string> reportedUnknown = new HashSet<string>();
        private static readonly List<int> pruneBuffer = new List<int>(8);

        private PlayerAbilityController player;
        private bool replicaSet;
        private readonly List<Behaviour> disabledInput = new List<Behaviour>();

        // Owner (client-only copy).
        private readonly Dictionary<int, float> pendingUntil = new Dictionary<int, float>();
        private ushort nextSeq;
        private float nextAimSend;
        private Vector3 lastSentAimPoint, lastSentAimDirection;

        // Clients: copies of the server's casts by server cast id.
        private readonly Dictionary<int, AbilityCastInstance> mirrors = new Dictionary<int, AbilityCastInstance>();
        private readonly List<NetAbilitySlotContent> applied = new List<NetAbilitySlotContent>();
        private bool contentsAppliedOnce;

        // Server.
        private AbilityNetMath.RateLimiter castLimiter = new AbilityNetMath.RateLimiter(12f, 6f);
        private AbilityNetMath.RateLimiter aimLimiter = new AbilityNetMath.RateLimiter(40f, 20f);
        private AbilityNetMath.RateLimiter cancelLimiter = new AbilityNetMath.RateLimiter(8f, 4f);
        private bool hasRemoteAim;
        private CastRequest remoteAim;
        private float remoteAimTime;
        private BufferedCast buffered;
        private float nextSlotPoll;
        private float nextContentPoll;
        private float nextAimBroadcast;
        private readonly List<SentSlotState> sentStates = new List<SentSlotState>();
        private readonly List<SlotContentSource> contentSources = new List<SlotContentSource>();
        private readonly Dictionary<int, Vector3> broadcastAim = new Dictionary<int, Vector3>();
        private bool serverHooked;

        private struct BufferedCast
        {
            public bool active;
            public int slot;
            public ushort seq;
            public CastRequest request;
            public float until;
            public NetworkConnection conn;
        }

        private struct SentSlotState
        {
            public bool valid;
            public AbilityPhase phase;
            public int charges;
            public float locked;
            public float recharge;
            public float at;
        }

        private struct SlotContentSource
        {
            public bool valid;
            public AbilityDefinition ability;
            public AbilityModifierSet modifiers;
            public string label;
            public AbilitySlotSource source;
            public bool enabled;
            public AbilityGrant grant;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            serverByEntity.Clear();
            reportedUnknown.Clear();
            AbilityCaster.InstantCastExecuted -= OnInstantCastExecuted;
        }

        /// <summary>The caster this component networks.</summary>
        public AbilityCaster Caster => caster;

        /// <summary>The networked caster of <paramref name="entity"/> on the server (null if it has none).</summary>
        public static NetAbilityCaster ServerOf(CombatEntity entity) =>
            entity != null && serverByEntity.TryGetValue(entity, out NetAbilityCaster n) && n != null ? n : null;

        private void Awake()
        {
            ResolveCaster();
        }

        private void ResolveCaster()
        {
            if (caster == null)
                caster = GetComponentInChildren<AbilityCaster>(true);
            player = caster as PlayerAbilityController;
        }

        private bool ReplicatesContents =>
            slotContents == SlotContentMode.Always || (slotContents == SlotContentMode.Auto && player != null);

        /// <summary>Does this machine control the character (the host's own player or the owning client)?</summary>
        private bool LocallyControlled => Owner != null && Owner.IsValid && Owner.IsLocalClient;

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            ResolveCaster();
            if (caster == null)
            {
                Debug.LogWarning($"[{name}] NetAbilityCaster found no AbilityCaster to network.", this);
                return;
            }
            caster.Initialize();
            HookServer(true);
            if (caster.Entity != null)
            {
                bool first = serverByEntity.Count == 0;
                serverByEntity[caster.Entity] = this;
                if (first)
                {
                    AbilityCaster.InstantCastExecuted -= OnInstantCastExecuted;
                    AbilityCaster.InstantCastExecuted += OnInstantCastExecuted;
                }
                NetworkObject s = caster.Entity.Summoner != null ? NetObjects.Of(caster.Entity.Summoner) : null;
                summoner.Value = NetObjects.IsSpawned(s) ? s : null;
            }
            if (ReplicatesContents)
                WriteAllContents();
            ApplyRole();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            HookServer(false);
            if (caster != null && caster.Entity != null && serverByEntity.TryGetValue(caster.Entity, out NetAbilityCaster n) && n == this)
                serverByEntity.Remove(caster.Entity);
            if (serverByEntity.Count == 0)
                AbilityCaster.InstantCastExecuted -= OnInstantCastExecuted;
            buffered = default;
            hasRemoteAim = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ResolveCaster();
            if (caster == null)
                return;
            caster.Initialize();
            if (!IsServerInitialized)
            {
                // A client-only copy: the server decides everything this caster does.
                caster.SetReplica(true);
                replicaSet = true;
                contents.OnChange += OnContentsChanged;
                summoner.OnChange += OnSummonerChanged;
                ApplyAllContents(false);
                contentsAppliedOnce = true; // later changes are news (an absorbed ability), the first state is not
                ApplySummoner(summoner.Value);
            }
            ApplyRole();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            contents.OnChange -= OnContentsChanged;
            summoner.OnChange -= OnSummonerChanged;
            EndMirrors();
            pendingUntil.Clear();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            // The caster decides again if this object outlives the session (scene objects). Input stays off on copies
            // of other players: they are about to be destroyed and must not read this machine's keyboard meanwhile.
            if (replicaSet && caster != null)
                caster.SetReplica(false);
            replicaSet = false;
            if (player != null)
                player.RemoteAim = null;
            if (LocallyControlled)
                SetInputMachines(true);
        }

        public override void OnOwnershipServer(NetworkConnection prevOwner)
        {
            base.OnOwnershipServer(prevOwner);
            ApplyRole();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyRole();
        }

        /// <summary>
        /// Who reads input and aims for this copy: only the machine controlling the character reads its devices; the
        /// server follows the aim a remote owner streams; a client-only owner sends its casts to the server.
        /// </summary>
        private void ApplyRole()
        {
            if (caster == null)
                return;
            bool local = LocallyControlled;
            if (player != null)
            {
                player.LocalInputEnabled = local;
                player.RemoteAim = IsServerInitialized && !local ? (PlayerAbilityController.AimProvider)ProvideRemoteAim : null;
                SetInputMachines(local);
            }
            if (replicaSet)
                caster.NetBridge = local ? this : null;
        }

        /// <summary>Switches the player's ability input machines (keyboard reading) on for the local player only.</summary>
        private void SetInputMachines(bool on)
        {
            if (on)
            {
                for (int i = 0; i < disabledInput.Count; i++)
                {
                    if (disabledInput[i] != null)
                        disabledInput[i].enabled = true;
                }
                disabledInput.Clear();
                return;
            }
            AbilitiesStateMachine[] machines = GetComponentsInChildren<AbilitiesStateMachine>(true);
            for (int i = 0; i < machines.Length; i++)
            {
                AbilitiesStateMachine m = machines[i];
                if (m != null && m.enabled)
                {
                    m.enabled = false; // its OnDisable releases the shared input and clears buffered presses
                    disabledInput.Add(m);
                }
            }
        }

        private void HookServer(bool on)
        {
            if (caster == null || on == serverHooked)
                return;
            serverHooked = on;
            if (on)
            {
                caster.CastStarted += OnServerCastStarted;
                caster.CastReleased += OnServerCastReleased;
                caster.CastInterrupted += OnServerCastInterrupted;
                caster.SlotChanged += OnServerSlotChanged;
                caster.SlotPhaseChanged += OnServerSlotPhaseChanged;
            }
            else
            {
                caster.CastStarted -= OnServerCastStarted;
                caster.CastReleased -= OnServerCastReleased;
                caster.CastInterrupted -= OnServerCastInterrupted;
                caster.SlotChanged -= OnServerSlotChanged;
                caster.SlotPhaseChanged -= OnServerSlotPhaseChanged;
            }
        }

        private void OnDestroy()
        {
            HookServer(false);
            if (caster != null && caster.Entity != null && serverByEntity.TryGetValue(caster.Entity, out NetAbilityCaster n) && n == this)
                serverByEntity.Remove(caster.Entity);
        }

        private void Update()
        {
            if (caster == null)
                return;
            if (IsServerInitialized)
                ServerUpdate();
            else if (IsClientInitialized && replicaSet && caster.NetBridge != null)
                OwnerStreamAim();
        }

        // ------------------------------------------------------------------ owner: intentions (IAbilityNetBridge)
        bool IAbilityNetBridge.RequestCast(AbilityCaster from, int slotIndex, in CastRequest request, out CastFailReason reason)
        {
            reason = CastFailReason.None;
            if (from != caster || !IsClientInitialized || !LocallyControlled || slotIndex < 0 || slotIndex > byte.MaxValue)
            {
                reason = CastFailReason.Disabled;
                return false;
            }
            float now = Time.unscaledTime;
            if (pendingUntil.TryGetValue(slotIndex, out float until) && now < until)
            {
                reason = CastFailReason.Busy; // the previous request of this slot is still on its way
                return false;
            }
            AbilitySlot s = caster.GetSlot(slotIndex);
            PreciseTick view = TimeManager.GetPreciseTick(TickType.LastPacketTick);
            var msg = new NetAbilityCastRequest
            {
                slot = (byte)slotIndex,
                seq = ++nextSeq,
                abilityId = s != null && s.ability != null ? s.ability.Id : "",
                target = NetTarget(request.target),
                point = request.point,
                hasPoint = request.hasPoint,
                direction = request.direction,
                hasDirection = request.hasDirection,
                viewTick = view.Tick,
                viewPercent = view.PercentAsByte,
            };
            pendingUntil[slotIndex] = now + PendingSeconds;
            lastSentAimPoint = request.point;
            lastSentAimDirection = request.direction;
            CmdCast(msg);
            return true;
        }

        void IAbilityNetBridge.RequestCancel(AbilityCaster from)
        {
            if (from == caster && IsClientInitialized && LocallyControlled)
                CmdCancelCasting();
        }

        /// <summary>Owner: streams the crosshair while one of its casts that follows it until release winds up.</summary>
        private void OwnerStreamAim()
        {
            if (player == null || !player.LocalInputEnabled)
                return;
            AbilityCastInstance tracking = null;
            IReadOnlyList<AbilitySlot> slots = caster.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityCastInstance c = slots[i] != null ? slots[i].CurrentCast : null;
                if (c != null && c.Phase == AbilityPhase.Casting && !c.Released && !c.Interrupted &&
                    c.Definition.targeting.aimLock == AbilityAimLock.TrackUntilRelease)
                {
                    tracking = c;
                    break;
                }
            }
            if (tracking == null)
                return;
            float now = Time.unscaledTime;
            if (now < nextAimSend)
                return;
            nextAimSend = now + 1f / Mathf.Max(1f, aimSendRate);
            CastRequest r = player.BuildAimRequest(tracking.Definition, tracking.Stats);
            if (!AbilityNetMath.AimChanged(lastSentAimPoint, lastSentAimDirection, r.point, r.direction, 0.05f, 0.5f))
                return;
            lastSentAimPoint = r.point;
            lastSentAimDirection = r.direction;
            PreciseTick view = TimeManager.GetPreciseTick(TickType.LastPacketTick);
            CmdAim(new NetAbilityAim
            {
                target = NetTarget(r.target),
                point = r.point,
                hasPoint = r.hasPoint,
                direction = r.direction,
                hasDirection = r.hasDirection,
                viewTick = view.Tick,
                viewPercent = view.PercentAsByte,
            });
        }

        // ------------------------------------------------------------------ server: validation
        [ServerRpc]
        private void CmdCast(NetAbilityCastRequest req, NetworkConnection conn = null)
        {
            if (caster == null || conn == null)
                return;
            if (!castLimiter.TryTake(Time.unscaledTime))
                return; // spam: dropped without an answer (the owner's pending request simply times out)
            ReportView(conn, req.viewTick, req.viewPercent);

            int slot = req.slot;
            AbilitySlot s = caster.GetSlot(slot);
            if (s == null || s.ability == null || (!string.IsNullOrEmpty(req.abilityId) && s.ability.Id != req.abilityId))
            {
                // Nothing there, or the owner's view of the slot is out of date (its content is on its way).
                RpcCastRejected(conn, req.slot, req.seq, CastFailReason.NoAbility);
                return;
            }
            if (!ReadRequest(req.target, req.point, req.hasPoint, req.direction, req.hasDirection, out CastRequest request))
            {
                RpcCastRejected(conn, req.slot, req.seq, CastFailReason.NoTarget);
                return;
            }
            remoteAim = request;
            hasRemoteAim = true;
            remoteAimTime = Time.unscaledTime;

            if (buffered.active)
            {
                // A newer request replaces the one waiting for its slot to be ready.
                RpcCastRejected(buffered.conn, (byte)buffered.slot, buffered.seq, CastFailReason.Busy);
                buffered = default;
            }
            CastFailReason reason = caster.CanCast(slot, request);
            if (reason == CastFailReason.None && caster.TryCast(slot, request, out reason))
                return;
            if (AbilityCaster.IsTimingReason(reason))
            {
                // Its cooldown or the previous cast ends a moment later here than on the owner's screen: retry briefly.
                buffered = new BufferedCast { active = true, slot = slot, seq = req.seq, request = request, until = Time.unscaledTime + InputBufferSeconds, conn = conn };
                return;
            }
            RpcCastRejected(conn, req.slot, req.seq, reason);
        }

        [ServerRpc]
        private void CmdAim(NetAbilityAim aim, Channel channel = Channel.Unreliable, NetworkConnection conn = null)
        {
            if (caster == null || conn == null || !aimLimiter.TryTake(Time.unscaledTime))
                return;
            ReportView(conn, aim.viewTick, aim.viewPercent);
            if (!ReadRequest(aim.target, aim.point, aim.hasPoint, aim.direction, aim.hasDirection, out CastRequest request))
                return;
            remoteAim = request;
            hasRemoteAim = true;
            remoteAimTime = Time.unscaledTime;
        }

        [ServerRpc]
        private void CmdCancelCasting()
        {
            if (caster == null || !cancelLimiter.TryTake(Time.unscaledTime))
                return;
            if (buffered.active)
            {
                RpcCastRejected(buffered.conn, (byte)buffered.slot, buffered.seq, CastFailReason.Disabled);
                buffered = default;
            }
            caster.CancelCasting();
        }

        /// <summary>
        /// Turns a client's aim into a cast request the server can use: the target must be a spawned character, every
        /// vector finite, the direction normalised and the point within <see cref="MaxAimDistance"/> of the caster.
        /// </summary>
        private bool ReadRequest(NetworkObject target, Vector3 point, bool hasPoint, Vector3 direction, bool hasDirection, out CastRequest request)
        {
            request = new CastRequest
            {
                target = target != null && target.IsSpawned ? CombatEntity.Resolve(target.gameObject) : null,
                point = point,
                hasPoint = hasPoint,
                direction = direction,
                hasDirection = hasDirection,
            };
            if (request.target != null && (request.target == caster.Entity || !request.target.IsAlive))
                request.target = null;
            Vector3 origin = caster.Entity != null ? caster.Entity.Position : caster.transform.position;
            return AbilityNetMath.SanitizeRequest(ref request, origin, MaxAimDistance);
        }

        private static void ReportView(NetworkConnection conn, uint tick, byte percent)
        {
            NetLagCompensation lag = NetLagCompensation.Instance;
            if (lag != null)
                lag.ReportView(conn, tick, percent / 100d);
        }

        /// <summary>The aim of a remote player's cast that follows its crosshair (server; see PlayerAbilityController.RemoteAim).</summary>
        private bool ProvideRemoteAim(AbilityCastInstance cast, out CastRequest request)
        {
            request = remoteAim;
            if (request.target != null && !request.target.IsAlive)
                request.target = null;
            return hasRemoteAim && Time.unscaledTime - remoteAimTime < 3f;
        }

        // ------------------------------------------------------------------ server: periodic work
        private void ServerUpdate()
        {
            float now = Time.unscaledTime;
            if (buffered.active)
                RetryBufferedCast(now);

            bool remoteOwner = player != null && Owner != null && Owner.IsValid && !Owner.IsLocalClient;
            if (remoteOwner && now >= nextSlotPoll)
            {
                nextSlotPoll = now + 1f / Mathf.Max(1f, slotPollRate);
                PollSlotStates();
            }
            if (ReplicatesContents && now >= nextContentPoll)
            {
                nextContentPoll = now + 0.5f;
                WriteAllContents();
            }
            if (now >= nextAimBroadcast)
            {
                nextAimBroadcast = now + 1f / Mathf.Max(1f, aimSendRate);
                BroadcastTrackingAims();
            }
        }

        private void RetryBufferedCast(float now)
        {
            if (buffered.conn == null || !buffered.conn.IsActive)
            {
                buffered = default;
                return;
            }
            CastFailReason reason = caster.CanCast(buffered.slot, buffered.request);
            if (reason == CastFailReason.None)
            {
                BufferedCast b = buffered;
                buffered = default;
                if (!caster.TryCast(b.slot, b.request, out reason))
                    RpcCastRejected(b.conn, (byte)b.slot, b.seq, reason);
                return;
            }
            if (now >= buffered.until || !AbilityCaster.IsTimingReason(reason))
            {
                BufferedCast b = buffered;
                buffered = default;
                RpcCastRejected(b.conn, (byte)b.slot, b.seq, reason);
            }
        }

        /// <summary>Server: reports to a remote owner every slot whose timers changed other than by ticking down.</summary>
        private void PollSlotStates()
        {
            IReadOnlyList<AbilitySlot> slots = caster.Slots;
            int count = Mathf.Min(slots.Count, byte.MaxValue + 1);
            while (sentStates.Count < count)
                sentStates.Add(default);
            float now = Time.time;
            for (int i = 0; i < count; i++)
            {
                AbilitySlot s = slots[i];
                if (s == null)
                    continue;
                SentSlotState last = sentStates[i];
                float locked = AbilityNetMath.Remaining(s.LockedUntil, now);
                float recharge = AbilityNetMath.Remaining(s.RechargeEnd, now);
                bool changed = !last.valid || last.phase != s.Phase || last.charges != s.Charges ||
                               AbilityNetMath.TimerJumped(last.locked, last.at, locked, now) ||
                               AbilityNetMath.TimerJumped(last.recharge, last.at, recharge, now);
                if (changed)
                    SendSlotState(i);
            }
        }

        private void SendSlotState(int index)
        {
            if (Owner == null || !Owner.IsValid || Owner.IsLocalClient || index < 0 || index > byte.MaxValue)
                return;
            AbilitySlot s = caster.GetSlot(index);
            if (s == null)
                return;
            s.GetNetState(out AbilityPhase phase, out float phaseRemaining, out float phaseLength, out int charges, out float recharge, out float locked);
            while (sentStates.Count <= index)
                sentStates.Add(default);
            sentStates[index] = new SentSlotState { valid = true, phase = phase, charges = charges, locked = locked, recharge = recharge, at = Time.time };
            RpcSlotState(Owner, new NetAbilitySlotState
            {
                index = (byte)index,
                phase = phase,
                phaseRemaining = phaseRemaining,
                phaseLength = phaseLength,
                charges = (byte)Mathf.Clamp(charges, 0, byte.MaxValue),
                rechargeRemaining = recharge,
                lockedRemaining = locked,
                tick = TimeManager.Tick,
            });
        }

        /// <summary>Server: tells the other clients where a player's crosshair-following casts aim while they wind up.</summary>
        private void BroadcastTrackingAims()
        {
            IReadOnlyList<AbilitySlot> slots = caster.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityCastInstance c = slots[i] != null ? slots[i].CurrentCast : null;
                if (c == null || c.Cosmetic || c.Released || c.Interrupted || c.Phase != AbilityPhase.Casting ||
                    c.Definition.targeting.aimLock != AbilityAimLock.TrackUntilRelease)
                    continue;
                if (broadcastAim.TryGetValue(c.Id, out Vector3 last) && (last - c.AimPoint).sqrMagnitude < 0.0025f)
                    continue;
                broadcastAim[c.Id] = c.AimPoint;
                RpcCastAim(c.Id, c.AimPoint, c.AimDirection);
            }
        }

        // ------------------------------------------------------------------ server: slot contents
        private void OnServerSlotChanged(int index)
        {
            if (index >= 0 && index < contentSources.Count)
                contentSources[index] = default; // rebuild (its modifiers may have changed in place)
            if (ReplicatesContents)
                WriteAllContents();
            if (sentStates.Count > index && index >= 0)
                sentStates[index] = default; // report its timers again
        }

        /// <summary>Server: brings the replicated slot list up to date (only slots whose content changed are rewritten).</summary>
        private void WriteAllContents()
        {
            if (caster == null || !IsServerInitialized)
                return;
            IReadOnlyList<AbilitySlot> slots = caster.Slots;
            int count = Mathf.Min(slots.Count, byte.MaxValue + 1);
            while (contentSources.Count < count)
                contentSources.Add(default);
            for (int i = 0; i < count; i++)
            {
                AbilitySlot s = slots[i];
                SlotContentSource src = contentSources[i];
                if (src.valid && s != null && ReferenceEquals(src.ability, s.ability) && ReferenceEquals(src.modifiers, s.modifiers) &&
                    src.label == s.label && src.source == s.Source && src.enabled == s.enabled && ReferenceEquals(src.grant, s.Grant) &&
                    i < contents.Count)
                    continue;
                contentSources[i] = s == null
                    ? new SlotContentSource { valid = true }
                    : new SlotContentSource { valid = true, ability = s.ability, modifiers = s.modifiers, label = s.label, source = s.Source, enabled = s.enabled, grant = s.Grant };
                NetAbilitySlotContent c = BuildContent(s);
                if (i < contents.Count)
                {
                    if (!NetAbilitySlotContent.Same(contents[i], c))
                        contents[i] = c;
                }
                else
                {
                    contents.Add(c);
                }
            }
            while (contents.Count > count)
                contents.RemoveAt(contents.Count - 1);
        }

        private static NetAbilitySlotContent BuildContent(AbilitySlot s)
        {
            if (s == null)
                return new NetAbilitySlotContent { abilityId = "", modifiersJson = "", label = "", enabled = true, grantName = "", grantSourceId = "" };
            AbilityGrant g = s.Grant;
            return new NetAbilitySlotContent
            {
                abilityId = s.ability != null ? s.ability.Id : "",
                modifiersJson = s.ability != null && s.modifiers != null ? JsonUtility.ToJson(s.modifiers) : "",
                label = s.label ?? "",
                source = s.Source,
                enabled = s.enabled,
                hasGrant = g != null && g.ability != null,
                grantName = g != null ? g.displayName ?? "" : "",
                grantTier = g != null ? g.tier : AbsorbTier.Same,
                grantSourceId = g != null && g.sourceAbility != null ? g.sourceAbility.Id : "",
            };
        }

        // ------------------------------------------------------------------ server: cast events
        private void OnServerCastStarted(AbilityCastInstance c)
        {
            if (c == null || c.Cosmetic || !IsServerInitialized || c.Slot == null || c.Slot.Index < 0 || c.Slot.Index > byte.MaxValue)
                return;
            if (buffered.active && c.Slot.Index == buffered.slot)
                buffered = default; // the slot cast after all (another path started it)
            RpcCastStart(new NetAbilityCastStart
            {
                castId = c.Id,
                slot = (byte)c.Slot.Index,
                abilityId = c.Definition != null ? c.Definition.Id : "",
                stats = c.Stats,
                target = NetTarget(c.Target),
                aimPoint = c.AimPoint,
                aimDirection = c.AimDirection,
                aimTargetPoint = c.AimTargetPoint,
                hasAimTargetPoint = c.HasAimTargetPoint,
                castTime = Mathf.Max(0f, c.ReleaseTime - c.StartTime),
                tick = TimeManager.Tick,
                seed = c.Seed,
                visualOnly = c.VisualOnly,
            });
        }

        private void OnServerCastReleased(AbilityCastInstance c)
        {
            if (c == null || c.Cosmetic || !IsServerInitialized)
                return;
            broadcastAim.Remove(c.Id);
            RpcCastRelease(new NetAbilityCastRelease
            {
                castId = c.Id,
                target = NetTarget(c.Target),
                aimPoint = c.AimPoint,
                aimDirection = c.AimDirection,
                aimTargetPoint = c.AimTargetPoint,
                hasAimTargetPoint = c.HasAimTargetPoint,
            });
        }

        private void OnServerCastInterrupted(AbilityCastInstance c, CastInterruptReason reason)
        {
            if (c == null || c.Cosmetic || !IsServerInitialized)
                return;
            broadcastAim.Remove(c.Id);
            RpcCastInterrupted(c.Id, reason);
        }

        private void OnServerSlotPhaseChanged(AbilitySlot slot, AbilityPhase from, AbilityPhase to)
        {
            if (slot != null && player != null && IsServerInitialized)
                SendSlotState(slot.Index);
        }

        /// <summary>Server: shows a caster-less cast (items, weapon attacks) to the observers of its source.</summary>
        private static void OnInstantCastExecuted(AbilityCastInstance c)
        {
            if (c == null || c.Cosmetic || c.Definition == null)
                return;
            NetAbilityCaster n = ServerOf(c.CasterEntity);
            if (n == null || !n.IsServerInitialized)
                return;
            n.RpcInstantCast(new NetAbilityInstantCast
            {
                abilityId = c.Definition.Id,
                stats = c.Stats,
                target = NetTarget(c.Target),
                position = c.AimPoint,
                direction = c.AimDirection,
                seed = c.Seed,
            });
        }

        // ------------------------------------------------------------------ clients: replay
        [ObserversRpc(ExcludeServer = true)]
        private void RpcCastStart(NetAbilityCastStart m)
        {
            if (IsServerInitialized || caster == null)
                return;
            pendingUntil.Remove(m.slot);
            AbilityDefinition def = ResolveAbility(m.slot, m.abilityId);
            if (def == null)
                return;
            float castTime = Mathf.Max(0f, m.castTime);
            float elapsed = Mathf.Clamp((float)TimeManager.TimePassed(m.tick), 0f, castTime);
            AbilityCastInstance cast = caster.BeginMirrorCast(m.slot, def, m.stats, EntityOf(m.target), m.aimPoint, m.aimDirection,
                m.aimTargetPoint, m.hasAimTargetPoint, castTime, elapsed, m.visualOnly, m.seed);
            PruneMirrors();
            if (cast != null)
                mirrors[m.castId] = cast;
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcCastRelease(NetAbilityCastRelease m)
        {
            if (IsServerInitialized || caster == null || !mirrors.TryGetValue(m.castId, out AbilityCastInstance cast))
                return;
            caster.ReleaseMirrorCast(cast, EntityOf(m.target), m.aimPoint, m.aimDirection, m.aimTargetPoint, m.hasAimTargetPoint);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcCastInterrupted(int castId, CastInterruptReason reason)
        {
            if (IsServerInitialized || caster == null || !mirrors.TryGetValue(castId, out AbilityCastInstance cast))
                return;
            mirrors.Remove(castId);
            caster.InterruptMirrorCast(cast, reason);
        }

        [ObserversRpc(ExcludeServer = true, ExcludeOwner = true)]
        private void RpcCastAim(int castId, Vector3 aimPoint, Vector3 aimDirection, Channel channel = Channel.Unreliable)
        {
            if (IsServerInitialized || caster == null || !mirrors.TryGetValue(castId, out AbilityCastInstance cast))
                return;
            caster.SetMirrorAim(cast, aimPoint, aimDirection);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcInstantCast(NetAbilityInstantCast m)
        {
            if (IsServerInitialized || caster == null)
                return;
            AbilityDefinition def = ResolveAbility(-1, m.abilityId);
            if (def == null)
                return;
            AbilityCaster.ExecuteMirrorInstant(def, caster.Entity, m.position, m.direction, EntityOf(m.target), m.stats, m.seed);
        }

        [TargetRpc]
        private void RpcCastRejected(NetworkConnection conn, byte slot, ushort seq, CastFailReason reason)
        {
            if (caster == null)
                return;
            pendingUntil.Remove(slot);
            if (reason != CastFailReason.None)
                caster.RaiseCastFailed(slot, reason);
        }

        [TargetRpc]
        private void RpcSlotState(NetworkConnection conn, NetAbilitySlotState state)
        {
            if (IsServerInitialized || caster == null)
                return;
            AbilitySlot s = caster.GetSlot(state.index);
            if (s == null)
                return;
            float age = Mathf.Clamp((float)TimeManager.TimePassed(state.tick), 0f, 0.5f);
            s.ApplyNetState(state.phase, state.phaseRemaining, state.phaseLength, state.charges, state.rechargeRemaining, state.lockedRemaining, age);
        }

        /// <summary>Forgets copies that ended (their server events can no longer arrive).</summary>
        private void PruneMirrors()
        {
            if (mirrors.Count == 0)
                return;
            pruneBuffer.Clear();
            foreach (KeyValuePair<int, AbilityCastInstance> kv in mirrors)
            {
                AbilityCastInstance c = kv.Value;
                if (c == null || c.Interrupted || c.Phase == AbilityPhase.InCooldown)
                    pruneBuffer.Add(kv.Key);
            }
            for (int i = 0; i < pruneBuffer.Count; i++)
                mirrors.Remove(pruneBuffer[i]);
            pruneBuffer.Clear();
        }

        private void EndMirrors()
        {
            if (caster != null)
            {
                foreach (KeyValuePair<int, AbilityCastInstance> kv in mirrors)
                {
                    if (kv.Value != null && !kv.Value.Interrupted)
                        caster.InterruptMirrorCast(kv.Value, CastInterruptReason.Disabled);
                }
            }
            mirrors.Clear();
        }

        // ------------------------------------------------------------------ clients: slot contents
        private void OnContentsChanged(SyncListOperation op, int index, NetAbilitySlotContent oldItem, NetAbilitySlotContent newItem, bool asServer)
        {
            if (asServer || IsServerInitialized || caster == null)
                return;
            if ((op == SyncListOperation.Add || op == SyncListOperation.Set) && index >= 0 && index < contents.Count)
                ApplyContent(index, contents[index], contentsAppliedOnce);
            else
                ApplyAllContents(contentsAppliedOnce);
        }

        private void ApplyAllContents(bool notify)
        {
            if (caster == null || IsServerInitialized || !ReplicatesContents)
                return;
            for (int i = 0; i < contents.Count; i++)
                ApplyContent(i, contents[i], notify);
            if (contents.Count == 0)
                return; // nothing received yet: keep the prefab's slots
            // Slots this copy has beyond the server's (created locally before the list arrived) are emptied.
            for (int i = contents.Count; i < caster.SlotCount; i++)
            {
                AbilitySlot s = caster.GetSlot(i);
                if (s != null && !s.IsEmpty)
                    caster.ApplyReplicatedSlot(i, null, null, "", AbilitySlotSource.Skill, true, null);
            }
        }

        /// <summary>Client: puts the server's content of slot <paramref name="index"/> into this copy.</summary>
        private void ApplyContent(int index, NetAbilitySlotContent c, bool notify)
        {
            while (applied.Count <= index)
                applied.Add(new NetAbilitySlotContent { abilityId = null });
            NetAbilitySlotContent before = applied[index];
            bool known = before.abilityId != null;
            if (known && NetAbilitySlotContent.Same(before, c))
                return;
            applied[index] = c;

            AbilityDefinition def = string.IsNullOrEmpty(c.abilityId) ? null : ResolveAbility(index, c.abilityId);
            var mods = new AbilityModifierSet();
            if (!string.IsNullOrEmpty(c.modifiersJson))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(c.modifiersJson, mods);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
            AbilityGrant grant = null;
            if (c.hasGrant && def != null)
            {
                AbilityDefinition src = string.IsNullOrEmpty(c.grantSourceId) ? null : AbilityDatabase.Resolve(c.grantSourceId);
                grant = new AbilityGrant(def, mods, c.grantName, c.grantTier, src);
            }
            caster.ApplyReplicatedSlot(index, def, mods, c.label, c.source, c.enabled, grant);

            // The owner's "ability absorbed" feedback (the server's copy raised it there).
            bool newGrant = grant != null && (!known || !before.hasGrant || before.abilityId != c.abilityId || before.modifiersJson != c.modifiersJson);
            if (notify && newGrant && LocallyControlled)
                caster.NotifyAbilityReceived(grant, index);
        }

        private void OnSummonerChanged(NetworkObject prev, NetworkObject next, bool asServer)
        {
            if (!asServer && !IsServerInitialized)
                ApplySummoner(next);
        }

        private void ApplySummoner(NetworkObject nob)
        {
            if (caster == null || caster.Entity == null)
                return;
            CombatEntity s = EntityOf(nob);
            if (s != null || nob == null)
                caster.Entity.Summoner = s; // its side (team, party) follows the summoner's here too
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>
        /// The ability with <paramref name="id"/>: the one already in slot <paramref name="slot"/> when it matches (mob
        /// slots are built identically on every machine), otherwise from the Ability Database and runtime definitions.
        /// </summary>
        private AbilityDefinition ResolveAbility(int slot, string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            AbilitySlot s = slot >= 0 ? caster.GetSlot(slot) : null;
            if (s != null && s.ability != null && s.ability.Id == id)
                return s.ability;
            AbilityDefinition def = AbilityDatabase.Resolve(id);
            if (def == null && reportedUnknown.Add(id))
                Debug.LogWarning($"[{name}] The server used ability '{id}', which this machine does not know. Add it to the Ability Database " +
                                 "(Resources/AbilityDatabase) so every machine can find it by id.", this);
            return def;
        }

        private static NetworkObject NetTarget(CombatEntity e)
        {
            if (e == null)
                return null;
            NetworkObject nob = NetObjects.Of(e);
            return NetObjects.IsSpawned(nob) ? nob : null;
        }

        private static CombatEntity EntityOf(NetworkObject nob) => nob != null ? CombatEntity.Resolve(nob.gameObject) : null;
    }
}
#endif
