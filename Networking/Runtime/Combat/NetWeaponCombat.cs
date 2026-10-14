#if FISHNET
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Networks a character's weapons and block (<see cref="WeaponController"/>, <see cref="BlockController"/>): the
    /// server runs the unchanged weapon code and every client shows what it did.
    /// <list type="bullet">
    /// <item>Owner to server: the player's own machine sends attack intentions (input pressed / released / tapped,
    /// reload, drop the charge, quick throw), its aim ray and whether it wants its block up (<see cref="IWeaponNetBridge"/>).
    /// The server validates them (ownership, known values, finite aim pulled back near the player, rate limits, the
    /// weapon really in the inventory), records the client's view tick for lag compensation and feeds them to its copy
    /// of the player, whose own checks then decide (cooldowns, stamina, ammo, reloads, durability, combos).</item>
    /// <item>Server to everyone watching: charges, attacks, interruptions, shots (cosmetic projectiles), hit and impact
    /// feedback, reloads and shield bashes (<see cref="IWeaponPresentationSink"/>); the block state as a SyncVar.</item>
    /// <item>Server to owner: the rounds loaded in each hand (ammo counters) and why an attack could not start.</item>
    /// </list>
    /// Damage, crits, ammo, durability and block outcomes exist only on the server; the status layer replicates their
    /// results. Put it on the player prefab root next to the NetworkObject (mobs with a Block Controller may have one
    /// too, to show their guard). Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetWeaponCombat : NetworkBehaviour, IWeaponNetBridge, IWeaponPresentationSink
    {
        /// <summary><see cref="NetWeaponHit.flags"/>: a prop or tool was hit.</summary>
        public const byte HitObjectOnly = 1;
        /// <summary><see cref="NetWeaponHit.flags"/>: a block or parry stopped the hit.</summary>
        public const byte HitStopped = 2;
        /// <summary>Most projectiles of one shot sent to clients.</summary>
        public const int MaxShotProjectiles = 32;
        /// <summary>Longest Item Database id accepted from a client.</summary>
        public const int MaxItemIdLength = 64;

        [Tooltip("The weapons to network. Empty = the WeaponController on this object or its children.")]
        [SerializeField] private WeaponController weapons;
        [Tooltip("The block to network. Empty = the BlockController on this object or its children.")]
        [SerializeField] private BlockController block;
        [Tooltip("Times per second the owner streams its aim while it holds an attack input of a ranged weapon.")]
        [SerializeField, Range(5f, 40f)] private float aimSendRate = 20f;
        [Tooltip("Times per second the server checks the rounds loaded in each hand for the owner's ammo counters.")]
        [SerializeField, Range(2f, 30f)] private float roundsPollRate = 10f;

        private readonly SyncVar<byte> blockState = new SyncVar<byte>();
        private readonly SyncVar<int> mainRounds = new SyncVar<int>(-1, new SyncTypeSettings(ReadPermission.OwnerOnly));
        private readonly SyncVar<int> offRounds = new SyncVar<int>(-1, new SyncTypeSettings(ReadPermission.OwnerOnly));

        // Server
        private WeaponNetMath.RateLimiter pressLimiter = new WeaponNetMath.RateLimiter(15f, 8f);
        private WeaponNetMath.RateLimiter releaseLimiter = new WeaponNetMath.RateLimiter(30f, 16f);
        private WeaponNetMath.RateLimiter aimLimiter = new WeaponNetMath.RateLimiter(60f, 30f);
        private WeaponNetMath.RateLimiter throwLimiter = new WeaponNetMath.RateLimiter(6f, 3f);
        private WeaponNetMath.RateLimiter blockLimiter = new WeaponNetMath.RateLimiter(20f, 10f);
        private WeaponNetMath.RateLimiter failLimiter = new WeaponNetMath.RateLimiter(8f, 4f);
        private float nextRoundsPoll;
        private bool serverHooked;

        // Owner
        private float nextAimSend;
        private Vector3 lastAimOrigin, lastAimDirection;

        /// <summary>The weapon controller this component networks (null on characters with a block only).</summary>
        public WeaponController Weapons => weapons;

        /// <summary>The block controller this component networks, or null.</summary>
        public BlockController Block => block;

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            if (weapons == null)
                weapons = GetComponentInChildren<WeaponController>(true);
            if (block == null)
                block = GetComponentInChildren<BlockController>(true);
        }

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            Resolve();
            HookServer(true);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            HookServer(false);
            ForgetRemoteOwner();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Resolve();
            blockState.OnChange += OnBlockStateChanged;
            if (!IsServerInitialized && block != null)
                ApplyBlockState(blockState.Value);
            ApplyRole();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            blockState.OnChange -= OnBlockStateChanged;
            SetBridges(false);
            if (!IsServerInitialized)
            {
                // What was being shown is over: no charge or block pose stays on a copy that outlives the session.
                if (weapons != null)
                    weapons.PresentAttackStopped(true);
                if (block != null)
                    block.ApplyReplicatedState(BlockController.BlockState.Idle);
            }
        }

        public override void OnOwnershipServer(NetworkConnection prevOwner)
        {
            base.OnOwnershipServer(prevOwner);
            ForgetRemoteOwner(); // inputs, aim and block intent of the previous owner do not carry over
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyRole();
        }

        /// <summary>Only a client-only owner sends intentions; the host's own player runs its weapons directly.</summary>
        private void ApplyRole() => SetBridges(IsClientInitialized && IsOwner && !IsServerInitialized);

        private void SetBridges(bool on)
        {
            if (weapons != null)
            {
                if (on)
                    weapons.NetBridge = this;
                else if (ReferenceEquals(weapons.NetBridge, this))
                    weapons.NetBridge = null;
            }
            if (block != null)
            {
                if (on)
                    block.NetBridge = this;
                else if (ReferenceEquals(block.NetBridge, this))
                    block.NetBridge = null;
            }
        }

        private void HookServer(bool on)
        {
            if (on == serverHooked)
                return;
            serverHooked = on;
            if (weapons != null)
            {
                if (on)
                    weapons.PresentationSink = this;
                else if (ReferenceEquals(weapons.PresentationSink, this))
                    weapons.PresentationSink = null;
            }
            if (block != null)
            {
                if (on)
                    block.Bashed += OnServerBash;
                else
                    block.Bashed -= OnServerBash;
            }
        }

        /// <summary>Server: lets go of everything a remote owner was holding (it left or the object changed hands).</summary>
        private void ForgetRemoteOwner()
        {
            if (weapons != null)
            {
                weapons.DropRemoteInputs();
                weapons.ClearRemoteAim();
            }
            if (block != null)
                block.ClearRemoteIntent();
        }

        private void Update()
        {
            if (IsServerInitialized)
                ServerUpdate();
            else if (IsClientInitialized && IsOwner)
                OwnerUpdate();
        }

        // ------------------------------------------------------------------ owner: intentions (IWeaponNetBridge)
        void IWeaponNetBridge.SendAttackInput(WeaponController from, WeaponInputKind kind, AttackType input)
        {
            if (from != weapons || !IsClientInitialized || !IsOwner || IsServerInitialized)
                return;
            CmdWeaponInput(BuildInput(kind, input));
        }

        bool IWeaponNetBridge.SendQuickThrow(WeaponController from, WeaponSO weapon)
        {
            if (from != weapons || weapon == null || !IsClientInitialized || !IsOwner || IsServerInitialized)
                return false;
            string id = ItemDatabase.Id(weapon);
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[NetWeaponCombat] '{weapon.Name}' is not in the Item Database: it cannot be thrown in a network session " +
                                 "(Tools > SimpleMovements > Network > Build Item Database).", this);
                return false;
            }
            CmdQuickThrow(id, BuildInput(WeaponInputKind.Tap, AttackType.Normal));
            return true;
        }

        void IWeaponNetBridge.SendBlockIntent(BlockController from, bool wantsToBlock)
        {
            if (from != block || !IsClientInitialized || !IsOwner || IsServerInitialized)
                return;
            CmdBlockIntent(wantsToBlock);
        }

        private NetWeaponInput BuildInput(WeaponInputKind kind, AttackType input)
        {
            PreciseTick view = TimeManager.GetPreciseTick(TickType.LastPacketTick);
            var m = new NetWeaponInput
            {
                kind = (byte)kind,
                input = (byte)input,
                viewTick = view.Tick,
                viewPercent = view.PercentAsByte,
            };
            if (weapons != null && weapons.TryGetAimRay(out Ray ray))
            {
                m.hasAim = true;
                m.aimOrigin = ray.origin;
                m.aimDirection = ray.direction;
                lastAimOrigin = ray.origin;
                lastAimDirection = ray.direction;
            }
            return m;
        }

        /// <summary>Owner: ammo counters follow the server; the aim is streamed while a ranged attack input is held.</summary>
        private void OwnerUpdate()
        {
            if (weapons == null)
                return;
            ApplyRounds(WeaponHandSide.Main, mainRounds.Value);
            ApplyRounds(WeaponHandSide.Off, offRounds.Value);

            if (!weapons.HasForwardedHeldInputs && !weapons.IsCharging)
                return;
            if (weapons.MainHand.Ranged == null && weapons.OffHand.Ranged == null)
                return; // melee attacks do not aim
            float now = Time.unscaledTime;
            if (now < nextAimSend || !weapons.TryGetAimRay(out Ray ray))
                return;
            if (!WeaponNetMath.AimChanged(lastAimOrigin, lastAimDirection, ray.origin, ray.direction, 0.02f, 0.25f))
                return;
            nextAimSend = now + 1f / Mathf.Max(1f, aimSendRate);
            lastAimOrigin = ray.origin;
            lastAimDirection = ray.direction;
            PreciseTick view = TimeManager.GetPreciseTick(TickType.LastPacketTick);
            CmdWeaponAim(ray.origin, ray.direction, view.Tick, view.PercentAsByte);
        }

        private void ApplyRounds(WeaponHandSide side, int value)
        {
            if (value >= 0 && weapons.RawLoadedRounds(side) != value)
                weapons.ApplyReplicatedRounds(side, value);
        }

        // ------------------------------------------------------------------ server: validation
        [ServerRpc]
        private void CmdWeaponInput(NetWeaponInput m, NetworkConnection conn = null)
        {
            if (weapons == null || conn == null)
                return;
            if (!WeaponNetMath.IsKnownKind(m.kind) || !WeaponNetMath.IsKnownInput(m.input))
                return;
            var kind = (WeaponInputKind)m.kind;
            float now = Time.unscaledTime;
            // Letting go is limited separately (and generously): a dropped release would leave a charge held.
            bool letGo = kind == WeaponInputKind.Release || kind == WeaponInputKind.CancelCharge;
            if (letGo)
            {
                if (!releaseLimiter.TryTake(now))
                    return;
            }
            else if (!pressLimiter.TryTake(now))
            {
                return;
            }
            ReportView(conn, m.viewTick, m.viewPercent);
            if (m.hasAim)
                ApplyAim(m.aimOrigin, m.aimDirection);

            var input = (AttackType)m.input;
            switch (kind)
            {
                case WeaponInputKind.Press:
                    if (IsAlive)
                        weapons.ServerBeginAttackInput(input);
                    break;
                case WeaponInputKind.Release:
                    weapons.ReleaseAttackInput(input);
                    break;
                case WeaponInputKind.Tap:
                    if (IsAlive)
                        weapons.ServerTapAttack(input);
                    break;
                case WeaponInputKind.Reload:
                    if (IsAlive)
                        weapons.Reload();
                    break;
                case WeaponInputKind.CancelCharge:
                    weapons.CancelCharge();
                    break;
            }
        }

        [ServerRpc]
        private void CmdWeaponAim(Vector3 origin, Vector3 direction, uint viewTick, byte viewPercent, Channel channel = Channel.Unreliable, NetworkConnection conn = null)
        {
            if (weapons == null || conn == null || !aimLimiter.TryTake(Time.unscaledTime))
                return;
            ReportView(conn, viewTick, viewPercent);
            ApplyAim(origin, direction);
        }

        [ServerRpc]
        private void CmdQuickThrow(string weaponId, NetWeaponInput m, NetworkConnection conn = null)
        {
            if (weapons == null || conn == null || !throwLimiter.TryTake(Time.unscaledTime))
                return;
            if (string.IsNullOrEmpty(weaponId) || weaponId.Length > MaxItemIdLength || !IsAlive)
                return;
            // Only a throwable the player really carries (the server's inventory decides which stack).
            if (!(ItemDatabase.Item(weaponId) is WeaponSO weapon))
                return;
            ReportView(conn, m.viewTick, m.viewPercent);
            if (m.hasAim)
                ApplyAim(m.aimOrigin, m.aimDirection);
            weapons.ServerQuickThrow(weapon);
        }

        [ServerRpc]
        private void CmdBlockIntent(bool wantsToBlock)
        {
            if (block == null)
                return;
            // Lowering always goes through (a dropped "down" would keep the block up); raising is rate limited.
            if (wantsToBlock && !blockLimiter.TryTake(Time.unscaledTime))
                return;
            block.SetRemoteIntent(wantsToBlock);
        }

        private bool IsAlive
        {
            get
            {
                CombatEntity e = weapons != null ? weapons.Entity : null;
                return e == null || e.IsAlive;
            }
        }

        /// <summary>Server: the owner's aim ray, if usable, pulled back near the player (see <see cref="WeaponNetMath.SanitizeAim"/>).</summary>
        private void ApplyAim(Vector3 origin, Vector3 direction)
        {
            CombatEntity e = weapons.Entity;
            Vector3 anchor = e != null ? e.Center : weapons.PlayerObject.transform.position;
            if (WeaponNetMath.SanitizeAim(ref origin, ref direction, anchor, WeaponNetMath.MaxAimOriginDistance))
                weapons.SetRemoteAim(origin, direction);
        }

        private static void ReportView(NetworkConnection conn, uint tick, byte percent)
        {
            NetLagCompensation lag = NetLagCompensation.Instance;
            if (lag != null)
                lag.ReportView(conn, tick, Mathf.Min(percent, (byte)100) / 100d);
        }

        // ------------------------------------------------------------------ server: state for clients
        private void ServerUpdate()
        {
            if (block != null)
            {
                byte b = (byte)block.State;
                if (blockState.Value != b)
                    blockState.Value = b;
            }
            if (weapons == null)
                return;
            float now = Time.unscaledTime;
            if (now < nextRoundsPoll)
                return;
            nextRoundsPoll = now + 1f / Mathf.Max(1f, roundsPollRate);
            int main = weapons.RawLoadedRounds(WeaponHandSide.Main);
            if (mainRounds.Value != main)
                mainRounds.Value = main;
            int off = weapons.RawLoadedRounds(WeaponHandSide.Off);
            if (offRounds.Value != off)
                offRounds.Value = off;
        }

        private void OnServerBash()
        {
            if (IsServerInitialized)
                RpcShieldBash();
        }

        // ------------------------------------------------------------------ server: presentation (IWeaponPresentationSink)
        private bool Broadcasts(WeaponController from) => from == weapons && IsServerInitialized;

        void IWeaponPresentationSink.ChargeShown(WeaponController from, in WeaponAttackShown attack)
        {
            if (Broadcasts(from) && TryEncode(attack, out NetWeaponAttack m))
                RpcWeaponCharge(m);
        }

        void IWeaponPresentationSink.AttackShown(WeaponController from, in WeaponAttackShown attack)
        {
            if (Broadcasts(from) && TryEncode(attack, out NetWeaponAttack m))
                RpcWeaponAttack(m);
        }

        void IWeaponPresentationSink.AttackStopped(WeaponController from, bool interrupted)
        {
            if (Broadcasts(from))
                RpcWeaponStopped(interrupted);
        }

        void IWeaponPresentationSink.ShotShown(WeaponController from, in WeaponShotShown shot)
        {
            if (!Broadcasts(from) || shot.weapon == null || shot.velocities == null || shot.velocities.Count == 0)
                return;
            int count = Mathf.Min(shot.velocities.Count, MaxShotProjectiles);
            var velocities = new Vector3[count];
            for (int i = 0; i < count; i++)
                velocities[i] = shot.velocities[i];
            RpcWeaponShot(new NetWeaponShot
            {
                weaponId = IdOf(shot.weapon),
                ammoId = shot.ammo != null ? IdOf(shot.ammo) : "",
                attack = (short)WeaponAttackCatalog.IndexOf(shot.weapon, shot.attack),
                hand = (byte)shot.hand,
                from = shot.from,
                direction = shot.direction,
                velocities = velocities,
            });
        }

        void IWeaponPresentationSink.HitShown(WeaponController from, in WeaponHitShown hit)
        {
            if (!Broadcasts(from))
                return;
            int index = WeaponAttackCatalog.IndexOf(hit.weapon, hit.attack);
            if (index < 0)
                return;
            NetworkObject target = hit.target != null ? NetObjects.Of(hit.target) : null;
            RpcWeaponHit(new NetWeaponHit
            {
                weaponId = IdOf(hit.weapon),
                attack = (short)index,
                point = hit.point,
                direction = hit.direction,
                element = (byte)hit.element,
                flags = (byte)((hit.objectOnly ? HitObjectOnly : 0) | (hit.stopped ? HitStopped : 0)),
                target = NetObjects.IsSpawned(target) ? target : null,
                seed = hit.seed,
                hitIndex = (short)Mathf.Clamp(hit.hitIndex, 0, short.MaxValue),
            });
        }

        void IWeaponPresentationSink.ImpactShown(WeaponController from, in WeaponImpactShown impact)
        {
            if (!Broadcasts(from))
                return;
            int index = WeaponAttackCatalog.IndexOf(impact.weapon, impact.attack);
            if (index < 0)
                return;
            RpcWeaponImpact(new NetWeaponImpact
            {
                weaponId = IdOf(impact.weapon),
                attack = (short)index,
                point = impact.point,
                normal = impact.normal,
                forward = impact.forward,
                scale = impact.scale,
                seed = impact.seed,
            });
        }

        void IWeaponPresentationSink.ReloadShown(WeaponController from, WeaponHandSide side, bool started)
        {
            if (Broadcasts(from))
                RpcWeaponReload((byte)side, started);
        }

        void IWeaponPresentationSink.AttackFailedShown(WeaponController from, AttackType input, string reason)
        {
            // The owner's UI and its empty clicks; the host's own player already saw it.
            if (!Broadcasts(from) || Owner == null || !Owner.IsValid || Owner.IsLocalClient)
                return;
            if (failLimiter.TryTake(Time.unscaledTime))
                RpcWeaponFailed(Owner, (byte)input, reason ?? "");
        }

        private static string IdOf(ItemSO item)
        {
            string id = item != null ? ItemDatabase.Id(item) : null;
            return id ?? "";
        }

        private static bool TryEncode(in WeaponAttackShown a, out NetWeaponAttack m)
        {
            m = default;
            int index = WeaponAttackCatalog.IndexOf(a.weapon, a.attack);
            if (index < 0)
                return false;
            m = new NetWeaponAttack
            {
                weaponId = IdOf(a.weapon),
                attack = (short)index,
                handInput = WeaponNetMath.PackHandInput(a.hand, a.input),
                chargeRatio = a.chargeRatio,
                speed = a.speed,
                seed = a.seed,
                quick = a.quick,
            };
            return true;
        }

        // ------------------------------------------------------------------ clients: presentation
        /// <summary>Client: the weapon an attack message names (by id, else the one in that hand), or null.</summary>
        private WeaponSO ResolveWeapon(string id, WeaponHandSide hand)
        {
            WeaponSO w = !string.IsNullOrEmpty(id) ? ItemDatabase.Item(id) as WeaponSO : null;
            if (w == null && weapons != null && string.IsNullOrEmpty(id))
                w = weapons.GetHand(hand).Weapon;
            return w;
        }

        private bool Decode(in NetWeaponAttack m, out WeaponAttackShown shown)
        {
            shown = default;
            if (weapons == null || !WeaponNetMath.UnpackHandInput(m.handInput, out WeaponHandSide hand, out AttackType input))
                return false;
            WeaponSO weapon = ResolveWeapon(m.weaponId, hand);
            if (!WeaponAttackCatalog.TryGet(weapon, m.attack, out WeaponAttackCatalog.Entry e))
                return false;
            shown = new WeaponAttackShown
            {
                hand = hand,
                input = input,
                weapon = weapon,
                attack = e.attack,
                action = e.action,
                sequence = e.sequence,
                branch = e.branch,
                chargeRatio = WeaponNetMath.IsFinite(m.chargeRatio) ? Mathf.Clamp01(m.chargeRatio) : 0f,
                speed = WeaponNetMath.IsFinite(m.speed) ? Mathf.Clamp(m.speed, 0.05f, 20f) : 1f,
                quick = m.quick,
                seed = m.seed,
            };
            return true;
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWeaponCharge(NetWeaponAttack m)
        {
            if (!IsServerInitialized && Decode(m, out WeaponAttackShown shown))
                weapons.PresentCharge(shown);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWeaponAttack(NetWeaponAttack m)
        {
            if (!IsServerInitialized && Decode(m, out WeaponAttackShown shown))
                weapons.PresentAttack(shown);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWeaponStopped(bool interrupted)
        {
            if (!IsServerInitialized && weapons != null)
                weapons.PresentAttackStopped(interrupted);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWeaponShot(NetWeaponShot m)
        {
            if (IsServerInitialized || weapons == null || m.velocities == null || m.hand > (byte)WeaponHandSide.Off)
                return;
            var hand = (WeaponHandSide)m.hand;
            WeaponSO weapon = ResolveWeapon(m.weaponId, hand);
            if (weapon == null)
                return;
            AttackComponent attack = WeaponAttackCatalog.TryGet(weapon, m.attack, out WeaponAttackCatalog.Entry e) ? e.attack : null;
            int count = Mathf.Min(m.velocities.Length, MaxShotProjectiles);
            var velocities = new System.Collections.Generic.List<Vector3>(count);
            for (int i = 0; i < count; i++)
                if (WeaponNetMath.IsFinite(m.velocities[i]))
                    velocities.Add(m.velocities[i]);
            if (!WeaponNetMath.IsFinite(m.from) || velocities.Count == 0)
                return;
            weapons.PresentShot(new WeaponShotShown
            {
                hand = hand,
                weapon = weapon,
                attack = attack,
                ammo = !string.IsNullOrEmpty(m.ammoId) ? ItemDatabase.Item(m.ammoId) as AmmoSO : null,
                from = m.from,
                direction = WeaponNetMath.IsFinite(m.direction) ? m.direction : Vector3.zero,
                velocities = velocities,
            });
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWeaponHit(NetWeaponHit m)
        {
            if (IsServerInitialized || weapons == null)
                return;
            WeaponSO weapon = ResolveWeapon(m.weaponId, WeaponHandSide.Main);
            if (!WeaponAttackCatalog.TryGet(weapon, m.attack, out WeaponAttackCatalog.Entry e) || !WeaponNetMath.IsFinite(m.point))
                return;
            weapons.PresentHit(new WeaponHitShown
            {
                weapon = weapon,
                attack = e.attack,
                point = m.point,
                direction = WeaponNetMath.IsFinite(m.direction) ? m.direction : Vector3.zero,
                element = System.Enum.IsDefined(typeof(ElementType), (int)m.element) ? (ElementType)m.element : ElementType.None,
                target = m.target != null ? CombatEntity.Resolve(m.target.gameObject) : null,
                objectOnly = (m.flags & HitObjectOnly) != 0,
                stopped = (m.flags & HitStopped) != 0,
                seed = m.seed,
                hitIndex = m.hitIndex,
            });
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWeaponImpact(NetWeaponImpact m)
        {
            if (IsServerInitialized || weapons == null)
                return;
            WeaponSO weapon = ResolveWeapon(m.weaponId, WeaponHandSide.Main);
            if (!WeaponAttackCatalog.TryGet(weapon, m.attack, out WeaponAttackCatalog.Entry e) || !WeaponNetMath.IsFinite(m.point))
                return;
            weapons.PresentImpact(new WeaponImpactShown
            {
                weapon = weapon,
                attack = e.attack,
                point = m.point,
                normal = WeaponNetMath.IsFinite(m.normal) ? m.normal : Vector3.up,
                forward = WeaponNetMath.IsFinite(m.forward) ? m.forward : Vector3.forward,
                scale = WeaponNetMath.IsFinite(m.scale) ? m.scale : 1f,
                seed = m.seed,
            });
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcWeaponReload(byte hand, bool started)
        {
            if (!IsServerInitialized && weapons != null && hand <= (byte)WeaponHandSide.Off)
                weapons.PresentReload((WeaponHandSide)hand, started);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcShieldBash()
        {
            if (!IsServerInitialized && block != null)
                block.PresentBash();
        }

        [TargetRpc]
        private void RpcWeaponFailed(NetworkConnection conn, byte input, string reason)
        {
            if (!IsServerInitialized && weapons != null && WeaponNetMath.IsKnownInput(input))
                weapons.PresentAttackFailed((AttackType)input, reason);
        }

        private void OnBlockStateChanged(byte previous, byte next, bool asServer)
        {
            if (!asServer)
                ApplyBlockState(next);
        }

        private void ApplyBlockState(byte value)
        {
            if (block != null && value <= (byte)BlockController.BlockState.GuardBroken)
                block.ApplyReplicatedState((BlockController.BlockState)value);
        }
    }
}
#endif
