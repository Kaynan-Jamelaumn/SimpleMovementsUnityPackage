#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// Networks a player's interactions (put it on the player prefab root, next to the NetworkObject).
    /// <list type="bullet">
    /// <item><b>Relay</b>: on a client, the player's <see cref="PlayerInteractor"/> (and <c>Player.OnInteract</c> for doors and
    /// other older Interactables) does not interact itself - it sends the target (<see cref="InteractTargetRef"/>: its
    /// NetworkObject plus a path, or its scene path, and which component) to the server. The server checks the player is
    /// alive, the target is in the player's scene, the hold time (from the hold start it was told about), then range
    /// (its own positions, with <see cref="RangeTolerance"/>), line of sight and <see cref="IInteractable.CanInteract"/>,
    /// and calls <see cref="IInteractable.Interact"/> with ITS copy of the player's interactor. So any IInteractable on a
    /// networked object or in a scene works in a network session with no extra code.</item>
    /// <item><b>Results shown on the player's screen</b> (NPC menus, dialogue, shops): the server runs the NPC session and
    /// tells the owner what to show (target RPCs); the owner's choices come back as requests
    /// (<see cref="INPCSessionRelay"/>). The same pattern serves any interaction whose result is a window: the server
    /// decides, a target RPC opens the window with the server's data, the window sends requests.</item>
    /// <item><b>Merchants</b>: the server sends the stock with its prices to the owner while the shop is open (again after
    /// every change - shared stock is the same for everyone); purchases and sales are requests the server repeats with
    /// <see cref="MerchantTransactions.Execute"/> after checking the session, distance, item and quantity.</item>
    /// </list>
    /// Offline it does nothing: the interactor, NPCs and merchants work exactly as before.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetInteractor : NetworkBehaviour, IInteractionRelay, IMerchantTransactionProcessor
    {
        /// <summary>Metres a target may be beyond its range on the server (the client saw it in range a moment ago).</summary>
        public const float RangeTolerance = 1f;
        /// <summary>Seconds a hold may be shorter than the target's Hold Duration (network jitter).</summary>
        public const float HoldTolerance = 0.25f;
        /// <summary>Most units bought or sold in one request.</summary>
        public const int MaxTradeQuantity = 9999;
        /// <summary>Longest text sent to a client (greetings, messages).</summary>
        public const int MaxTextLength = 300;

        [Tooltip("The player's PlayerInteractor (finds doors, chests, NPCs and merchants and uses them with the Interact key). " +
                 "Filled automatically from this player; when the player has none, one is added when the game starts. " +
                 "Make Selected Prefab Networked ▸ Player adds it to the prefab and fills this field.")]
        [SerializeField] private PlayerInteractor interactor;
        [Tooltip("Shortest time between two stock updates sent while a shop is open (a burst of changes is one message).")]
        [SerializeField, Range(0.02f, 1f)] private float stockSendInterval = 0.1f;
        [Tooltip("Seconds a client waits for the server to answer a purchase before giving up.")]
        [SerializeField, Range(1f, 30f)] private float merchantAnswerTimeout = 6f;

        /// <summary>The interactor of the player controlled by this client (null on the server and offline).</summary>
        public static NetInteractor Local { get; private set; }

        private static int started;
        private static readonly HashSet<Component> warnedUnreplicated = new HashSet<Component>(ReferenceComparer<Component>.Instance);

        // Server
        private WeaponNetMath.RateLimiter interactLimiter = new WeaponNetMath.RateLimiter(8f, 5f);
        private WeaponNetMath.RateLimiter holdLimiter = new WeaponNetMath.RateLimiter(10f, 6f);
        private WeaponNetMath.RateLimiter npcLimiter = new WeaponNetMath.RateLimiter(10f, 6f);
        private WeaponNetMath.RateLimiter tradeLimiter = new WeaponNetMath.RateLimiter(8f, 5f);
        private Component holdAnchor;
        private float holdStartedAt;
        private Component deferredAnchor;
        private float deferredAt;
        private Merchant watchedMerchant;
        private MerchantInventory watchedStock;
        private NPCInteractionSession watchedSession;
        private bool stockDirty;
        private float nextStockSend;
        private readonly List<MerchantSlotState> stockBuffer = new List<MerchantSlotState>();

        // Owner client
        private uint merchantSeq;
        private bool ownerHooked;
        private Player playerComponent;
        private readonly Dictionary<uint, PendingTrade> pending = new Dictionary<uint, PendingTrade>();
        private readonly List<uint> expired = new List<uint>();

        private struct PendingTrade
        {
            public MerchantTransactionRequest Request;
            public Action<MerchantTransactionResult> Done;
            public float SentAt;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Local = null;
            started = 0;
            warnedUnreplicated.Clear();
        }

        /// <summary>The interactor this component networks.</summary>
        public PlayerInteractor Interactor => interactor;

        private GameObject PlayerObject => interactor != null ? interactor.PlayerObject : gameObject;
        private Scene PlayerScene => PlayerObject.scene;
        private InventoryManager PlayerInventory => interactor != null ? interactor.Inventory : InventoryManager.For(this);

        private Vector3 PlayerPosition
        {
            get
            {
                CombatEntity e = CombatEntity.Resolve(PlayerObject);
                return e != null ? e.Position : PlayerObject.transform.position;
            }
        }

        private bool IsAlive
        {
            get
            {
                CombatEntity e = CombatEntity.Resolve(PlayerObject);
                return e == null || e.IsAlive;
            }
        }

        private void Awake()
        {
            if (interactor == null)
                interactor = GetComponentInChildren<PlayerInteractor>(true);
            if (interactor == null)
                interactor = PlayerInteractor.For(this);
            if (interactor == null)
            {
                // Without one nothing could be interacted with in a session (this component only relays it).
                interactor = gameObject.AddComponent<PlayerInteractor>();
                Debug.LogWarning($"[NetInteractor] '{name}' had no PlayerInteractor: one was added. Add it to the player prefab " +
                                 "(Tools ▸ SimpleMovements ▸ Network ▸ Make Selected Prefab Networked ▸ Player does it) to keep its settings.", this);
            }
        }

#if UNITY_EDITOR
        // Fills the field in the inspector as soon as the player has a PlayerInteractor.
        protected override void Reset()
        {
            base.Reset();
            FillInteractor();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (!Application.isPlaying)
                FillInteractor();
        }

        private void FillInteractor()
        {
            if (interactor == null)
                interactor = GetComponentInChildren<PlayerInteractor>(true);
        }
#endif

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            started++;
            NPC.NetRelay = NpcRelay.Instance;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            UnhookOwner();
            started = Mathf.Max(0, started - 1);
            if (started == 0 && ReferenceEquals(NPC.NetRelay, NpcRelay.Instance))
                NPC.NetRelay = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
                HookOwner();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            UnhookOwner();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner)
                HookOwner();
            else
                UnhookOwner();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            StopWatching();
            holdAnchor = deferredAnchor = null;
        }

        private void OnDestroy()
        {
            UnhookOwner();
            StopWatching();
        }

        /// <summary>A client's own player: interactions, NPC choices and purchases go to the server from now on.</summary>
        private void HookOwner()
        {
            // The host's own player is the server's copy: it interacts directly.
            if (ownerHooked || IsServerInitialized || !IsClientInitialized)
                return;
            ownerHooked = true;
            Local = this;
            if (interactor != null)
                interactor.Relay = this;
            Merchant.GlobalProcessor = this;
            Player.NetInteractHandler = RelayOlderInteractable;
        }

        private void UnhookOwner()
        {
            if (!ownerHooked)
                return;
            ownerHooked = false;
            if (Local == this)
                Local = null;
            if (interactor != null && ReferenceEquals(interactor.Relay, this))
                interactor.Relay = null;
            if (ReferenceEquals(Merchant.GlobalProcessor, this))
                Merchant.GlobalProcessor = LocalMerchantTransactionProcessor.Instance;
            if (Player.NetInteractHandler == (Func<Player, Interactable, bool>)RelayOlderInteractable)
                Player.NetInteractHandler = null;
            FailAllPending("The connection to the server was lost.");
        }

        private void Update()
        {
            if (IsServerInitialized)
                ServerUpdate();
            if (ownerHooked && pending.Count > 0)
                ExpirePending();
        }

        // ------------------------------------------------------------------ client: the relay
        bool IInteractionRelay.TryRelay(PlayerInteractor from, IInteractable target)
        {
            if (!CanSend || target == null)
                return false;
            // Items on the ground and storages go through the inventory, which asks the server itself.
            if (target is LegacyInteractableTarget legacy && (legacy.IsPickup || legacy.IsStorage))
                return false;
            // Older Interactables take the same route as Player.OnInteract: other modules put their own relay in front of
            // Player.NetInteractHandler (dungeon elements are sent by element id, not by scene path) and the chain ends
            // with this component's relay.
            if (target is LegacyInteractableTarget older && older.Owner is Interactable olderInteractable)
            {
                Func<Player, Interactable, bool> handler = Player.NetInteractHandler;
                Player olderInput = PlayerComponent;
                if (handler != null && olderInput != null && handler != (Func<Player, Interactable, bool>)RelayOlderInteractable
                    && handler(olderInput, olderInteractable))
                    return true;
            }
            Component anchor = InteractTargets.AnchorOf(target);
            if (!InteractTargets.TryMake(anchor, out InteractTargetRef r))
                return false;
            WarnIfStateNotReplicated(anchor, r);
            ServerInteract(r);
            return true;
        }

        void IInteractionRelay.HoldChanged(PlayerInteractor from, IInteractable target, bool holding)
        {
            if (!CanSend || target == null)
                return;
            if (target is LegacyInteractableTarget legacy && (legacy.IsPickup || legacy.IsStorage))
                return;
            if (InteractTargets.TryMake(InteractTargets.AnchorOf(target), out InteractTargetRef r))
                ServerHold(r, holding);
        }

        /// <summary><c>Player.OnInteract</c> on a client used a door, lever or shrine: ask the server.</summary>
        private static bool RelayOlderInteractable(Player player, Interactable interactable)
        {
            NetInteractor local = Local;
            if (local == null || !local.CanSend || interactable == null || !InteractTargets.TryMake(interactable, out InteractTargetRef r))
                return false;
            WarnIfStateNotReplicated(interactable, r);
            local.ServerInteract(r);
            return true;
        }

        /// <summary>
        /// An object with a state every player must see (a door) but no NetworkObject + NetInteractable changes on the
        /// server only: the clients keep showing the old state. Tells the developer once per object.
        /// </summary>
        private static void WarnIfStateNotReplicated(Component anchor, in InteractTargetRef r)
        {
            // NPCs only replicate how many players they talk to ("Busy"), which the server checks anyway.
            if (anchor == null || anchor is NPC || anchor.GetComponent<IReplicatedInteractable>() == null)
                return;
            if (!r.inScene && r.root != null && r.root.GetComponentInChildren<NetInteractable>(true) != null)
                return;
            if (!warnedUnreplicated.Add(anchor))
                return;
            Debug.LogWarning($"[Net] '{anchor.name}' has no NetworkObject + NetInteractable: the server uses it, but its state " +
                             "(open, closed) is not sent to the clients. Use Tools > SimpleMovements > Network > Make Selected Prefab " +
                             "Networked > Interactable (merchant, chest, door, gate), or put a NetworkObject and a NetInteractable " +
                             "on it in its scene.", anchor);
        }

        private bool CanSend => ownerHooked && IsClientInitialized && IsOwner && !IsServerInitialized;

        /// <summary>The player's <see cref="Player"/> component (older interaction input), or null.</summary>
        private Player PlayerComponent
        {
            get
            {
                if (playerComponent == null)
                {
                    playerComponent = PlayerObject.GetComponentInChildren<Player>(true);
                    if (playerComponent == null)
                        playerComponent = GetComponentInChildren<Player>(true);
                }
                return playerComponent;
            }
        }

        // ------------------------------------------------------------------ server: interactions
        [ServerRpc]
        private void ServerInteract(InteractTargetRef target)
        {
            if (!interactLimiter.TryTake(Time.unscaledTime) || interactor == null || !IsAlive)
                return;
            Component anchor = ResolveUsable(target);
            IInteractable t = interactor.TargetFor(anchor);
            if (t == null)
                return;
            deferredAnchor = null;
            float hold = HoldOf(t);
            if (hold > 0f)
            {
                bool sawHold = holdAnchor == anchor;
                bool heldLongEnough = sawHold && Time.time - holdStartedAt >= hold - HoldTolerance;
                holdAnchor = null;
                if (!heldLongEnough)
                {
                    // No hold was reported (Player.OnInteract times holds itself): the server times it from now.
                    if (!sawHold)
                    {
                        deferredAnchor = anchor;
                        deferredAt = Time.time + hold;
                    }
                    return;
                }
            }
            Use(t);
        }

        [ServerRpc]
        private void ServerHold(InteractTargetRef target, bool holding)
        {
            if (!holdLimiter.TryTake(Time.unscaledTime))
                return;
            Component anchor = ResolveUsable(target);
            if (!holding)
            {
                if (anchor != null && holdAnchor == anchor)
                    holdAnchor = null;
                return;
            }
            if (anchor == null || !IsAlive)
                return;
            holdAnchor = anchor;
            holdStartedAt = Time.time;
            deferredAnchor = null;
        }

        /// <summary>The target on this server, when it exists, is in the player's scene and is not the player itself.</summary>
        private Component ResolveUsable(in InteractTargetRef target)
        {
            Component anchor = InteractTargets.Resolve(target, PlayerScene);
            if (anchor == null || anchor.gameObject.scene != PlayerScene)
                return null;
            if (anchor.transform.IsChildOf(PlayerObject.transform))
                return null;
            if (anchor is Behaviour b && !b.isActiveAndEnabled)
                return null;
            return anchor;
        }

        private static float HoldOf(IInteractable t)
        {
            try { return Mathf.Max(0f, t.HoldDuration); }
            catch (Exception e)
            {
                Debug.LogException(e);
                return 0f;
            }
        }

        private void Use(IInteractable t)
        {
            if (!interactor.InteractFromRequest(t, RangeTolerance, out string reason) && !string.IsNullOrEmpty(reason))
                PlayerInventory?.ShowMessage($"{t.InteractionName}: {reason}", InventoryFeedback.Kind.Info);
        }

        private void ServerUpdate()
        {
            if (deferredAnchor != null && Time.time >= deferredAt)
            {
                Component anchor = deferredAnchor;
                deferredAnchor = null;
                IInteractable t = anchor != null && IsAlive ? interactor.TargetFor(anchor) : null;
                if (t != null)
                    Use(t);
            }
            if (watchedStock != null)
            {
                if (watchedSession == null || !watchedSession.IsOpen || watchedMerchant == null || !ReferenceEquals(watchedSession.ActiveBehaviour, watchedMerchant))
                    StopWatching();
                else if (stockDirty && Time.unscaledTime >= nextStockSend)
                    SendStock();
            }
        }

        // ------------------------------------------------------------------ server: NPC sessions shown on the owner's machine
        private bool OwnerReachable => IsServerInitialized && Owner != null && Owner.IsActive;

        internal void ServerShowMenu(NPCInteractionSession session, string greeting)
        {
            if (!OwnerReachable)
                return;
            if (ReferenceEquals(session, watchedSession))
                StopWatching();
            if (!InteractTargets.TryMake(session.Npc, out InteractTargetRef r))
            {
                session.End("unreachable");
                return;
            }
            TargetNpcMenu(Owner, r, Clip(greeting));
        }

        internal void ServerOpenBehaviour(NPCInteractionSession session, NPCBehaviour behaviour)
        {
            if (!OwnerReachable)
                return;
            int index = session.Npc.BehaviourIndex(behaviour);
            if (index < 0 || !InteractTargets.TryMake(session.Npc, out InteractTargetRef r))
            {
                session.End("unreachable");
                return;
            }
            StopWatching();
            if (behaviour is Merchant merchant)
                Watch(session, merchant, r);
            TargetNpcBehaviour(Owner, r, index, session.OpenedFromMenu);
        }

        internal void ServerSessionEnded(NPCInteractionSession session, string reason)
        {
            if (ReferenceEquals(session, watchedSession))
                StopWatching();
            if (!OwnerReachable || !InteractTargets.TryMake(session.Npc, out InteractTargetRef r))
                return;
            TargetNpcEnded(Owner, r, Clip(reason));
        }

        [ServerRpc]
        private void ServerNpcPick(InteractTargetRef npcRef, int behaviourIndex)
        {
            if (!ServerSession(npcRef, out NPC npc, out NPCInteractionSession session))
                return;
            NPCBehaviour b = npc.BehaviourAt(behaviourIndex);
            if (b != null)
                npc.OpenBehaviour(session, b, true); // checks the option is available
        }

        [ServerRpc]
        private void ServerNpcFinished(InteractTargetRef npcRef, int behaviourIndex)
        {
            if (!ServerSession(npcRef, out NPC npc, out NPCInteractionSession session))
                return;
            NPCBehaviour b = npc.BehaviourAt(behaviourIndex);
            if (b != null && ReferenceEquals(session.ActiveBehaviour, b))
                session.BehaviourFinished(b);
        }

        [ServerRpc]
        private void ServerNpcClose(InteractTargetRef npcRef)
        {
            if (!npcLimiter.TryTake(Time.unscaledTime))
                return;
            NPC npc = ResolveNpc(npcRef, PlayerScene);
            NPCInteractionSession session = npc != null ? npc.SessionFor(PlayerObject) : null;
            if (session != null && session.IsRemote)
                session.End("closed");
        }

        /// <summary>
        /// The open (remote) session of this player with the NPC <paramref name="npcRef"/> names. When there is none (it
        /// ended meanwhile), the owner is told so its copy closes.
        /// </summary>
        private bool ServerSession(in InteractTargetRef npcRef, out NPC npc, out NPCInteractionSession session)
        {
            session = null;
            npc = null;
            if (!npcLimiter.TryTake(Time.unscaledTime))
                return false;
            npc = ResolveNpc(npcRef, PlayerScene);
            if (npc == null)
                return false;
            session = npc.SessionFor(PlayerObject);
            if (session != null && session.IsRemote && !npc.IsWithinSessionRange(PlayerPosition, RangeTolerance))
            {
                session.End("walked away");
                session = null;
            }
            if (session == null || !session.IsRemote || !IsAlive)
            {
                if (OwnerReachable && session == null)
                    TargetNpcEnded(Owner, npcRef, "closed");
                return false;
            }
            return true;
        }

        private static NPC ResolveNpc(in InteractTargetRef r, Scene scene) => InteractTargets.Resolve(r, scene) as NPC;

        // ------------------------------------------------------------------ server: shop stock
        private void Watch(NPCInteractionSession session, Merchant merchant, InteractTargetRef npcRef)
        {
            watchedSession = session;
            watchedMerchant = merchant;
            watchedStock = merchant.Inventory;
            watchedStock.Changed += OnStockChanged;
            SendStock(npcRef);
        }

        private void StopWatching()
        {
            if (watchedStock != null)
                watchedStock.Changed -= OnStockChanged;
            watchedStock = null;
            watchedMerchant = null;
            watchedSession = null;
            stockDirty = false;
        }

        private void OnStockChanged() => stockDirty = true;

        private void SendStock()
        {
            if (watchedMerchant != null && InteractTargets.TryMake(watchedMerchant.Npc, out InteractTargetRef r))
                SendStock(r);
            else
                stockDirty = false;
        }

        private void SendStock(InteractTargetRef npcRef)
        {
            stockDirty = false;
            nextStockSend = Time.unscaledTime + stockSendInterval;
            if (!OwnerReachable || watchedMerchant == null)
                return;
            watchedMerchant.CaptureStock(stockBuffer, PlayerObject);
            TargetMerchantStock(Owner, npcRef, watchedMerchant.Npc.BehaviourIndex(watchedMerchant), stockBuffer, watchedMerchant.SecondsUntilRestock);
        }

        // ------------------------------------------------------------------ server: purchases and sales
        [ServerRpc]
        private void ServerMerchantTrade(MerchantTradeRequest request)
        {
            MerchantTransactionResult result;
            try { result = ServerTrade(request); }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                result = MerchantTransactionResult.Fail(MerchantTransactionStatus.Failed, default, "Something went wrong.");
            }
            if (OwnerReachable)
                TargetMerchantResult(Owner, request.seq, (byte)result.Status, result.Quantity, result.UnitPrice, result.TotalPrice, Clip(result.Message));
        }

        private MerchantTransactionResult ServerTrade(in MerchantTradeRequest m)
        {
            if (!tradeLimiter.TryTake(Time.unscaledTime))
                return Refuse(MerchantTransactionStatus.InvalidRequest, "Slow down.");
            if (!IsAlive)
                return Refuse(MerchantTransactionStatus.InvalidRequest, "You cannot trade now.");
            if (m.quantity <= 0 || m.quantity > MaxTradeQuantity || m.expectedTotal < 0)
                return Refuse(MerchantTransactionStatus.InvalidRequest, "Invalid quantity.");
            NPC npc = ResolveNpc(m.npc, PlayerScene);
            Merchant merchant = npc != null ? npc.BehaviourAt(m.behaviour) as Merchant : null;
            NPCInteractionSession session = npc != null ? npc.SessionFor(PlayerObject) : null;
            // Only the shop this player has open (the session also ends when the player walks away or dies).
            if (merchant == null || session == null || !ReferenceEquals(session.ActiveBehaviour, merchant))
                return Refuse(MerchantTransactionStatus.MerchantUnavailable, "You are not trading with that merchant.");
            if (!npc.IsWithinSessionRange(PlayerPosition, RangeTolerance))
                return Refuse(MerchantTransactionStatus.MerchantUnavailable, "You are too far from the merchant.");
            ItemSO item = string.IsNullOrEmpty(m.itemId) ? null : ItemDatabase.Item(m.itemId);
            if (item == null)
                return Refuse(MerchantTransactionStatus.InvalidRequest, "Unknown item.");

            MerchantTransactionRequest request;
            switch ((MerchantTransactionType)m.type)
            {
                case MerchantTransactionType.Buy:
                {
                    MerchantStockSlot slot = merchant.Inventory.Find(m.stockSlotId);
                    if (slot == null || slot.Item != item)
                        return Refuse(MerchantTransactionStatus.InvalidRequest, "That item is no longer sold.");
                    request = MerchantTransactionRequest.Buy(merchant, PlayerObject, slot, m.quantity, m.expectedTotal);
                    break;
                }
                case MerchantTransactionType.Sell:
                {
                    InventoryArea area = m.slot.Area;
                    if (area != InventoryArea.Bag && area != InventoryArea.Hotbar)
                        return Refuse(MerchantTransactionStatus.ItemNotOwned, "Take the item off before selling it.");
                    InventoryManager inventory = PlayerInventory;
                    InventorySlot invSlot = inventory != null ? inventory.SlotAt(m.slot) : null;
                    InventoryItem stack = invSlot != null && invSlot.heldItem != null ? invSlot.heldItem.GetComponent<InventoryItem>() : null;
                    if (stack == null || stack.itemScriptableObject != item)
                        return Refuse(MerchantTransactionStatus.ItemNotOwned, "You do not have that item any more.");
                    request = MerchantTransactionRequest.Sell(merchant, PlayerObject, stack, m.quantity, m.expectedTotal);
                    break;
                }
                default:
                    return Refuse(MerchantTransactionStatus.InvalidRequest, "Invalid request.");
            }
            // The server's own stock, prices, wallet and inventory decide; the client's total only guards against a changed price.
            return MerchantTransactions.Execute(request);
        }

        private static MerchantTransactionResult Refuse(MerchantTransactionStatus status, string message) =>
            MerchantTransactionResult.Fail(status, default, message);

        // ------------------------------------------------------------------ client: purchases and sales
        /// <summary>The shop window of this client's player submits a purchase or sale: the server performs it.</summary>
        public void Submit(MerchantTransactionRequest request, Action<MerchantTransactionResult> onComplete)
        {
            if (!CanSend)
            {
                LocalMerchantTransactionProcessor.Instance.Submit(request, onComplete);
                return;
            }
            Merchant merchant = request.Merchant;
            NPC npc = merchant != null ? merchant.Npc : null;
            if (npc == null || !InteractTargets.TryMake(npc, out InteractTargetRef npcRef))
            {
                Answer(onComplete, MerchantTransactionResult.Fail(MerchantTransactionStatus.MerchantUnavailable, request, "The merchant cannot be reached."));
                return;
            }
            string itemId = request.Item != null ? ItemDatabase.Id(request.Item) : null;
            if (string.IsNullOrEmpty(itemId))
            {
                Answer(onComplete, MerchantTransactionResult.Fail(MerchantTransactionStatus.InvalidRequest, request,
                    "That item cannot be traded in a network session (it is missing from the Item Database)."));
                return;
            }
            InventorySlotRef slotRef = InventorySlotRef.None;
            if (request.Type == MerchantTransactionType.Sell)
            {
                InventoryManager inventory = request.Player != null ? InventoryManager.For(request.Player.transform) : null;
                InventorySlot slot = inventory != null && request.Stack != null ? inventory.FindSlotHolding(request.Stack.gameObject) : null;
                slotRef = slot != null ? inventory.SlotRefOf(slot) : InventorySlotRef.None;
                if (!slotRef.IsValid || (slotRef.Area != InventoryArea.Bag && slotRef.Area != InventoryArea.Hotbar))
                {
                    Answer(onComplete, MerchantTransactionResult.Fail(MerchantTransactionStatus.ItemNotOwned, request, "You do not have that item any more."));
                    return;
                }
            }
            uint seq = ++merchantSeq;
            pending[seq] = new PendingTrade { Request = request, Done = onComplete, SentAt = Time.unscaledTime };
            ServerMerchantTrade(new MerchantTradeRequest
            {
                npc = npcRef,
                behaviour = npc.BehaviourIndex(merchant),
                type = (byte)request.Type,
                stockSlotId = request.StockSlotId,
                slot = slotRef,
                itemId = itemId,
                quantity = request.Quantity,
                expectedTotal = Mathf.Max(0, request.ExpectedTotal),
                seq = seq,
            });
        }

        [TargetRpc]
        private void TargetMerchantResult(NetworkConnection conn, uint seq, byte status, int quantity, int unitPrice, int totalPrice, string message)
        {
            if (!pending.TryGetValue(seq, out PendingTrade p))
                return;
            pending.Remove(seq);
            MerchantTransactionStatus s = status <= (byte)MerchantTransactionStatus.Failed ? (MerchantTransactionStatus)status : MerchantTransactionStatus.Failed;
            Answer(p.Done, new MerchantTransactionResult(s, p.Request, quantity, unitPrice, totalPrice, message));
        }

        private void ExpirePending()
        {
            float now = Time.unscaledTime;
            expired.Clear();
            foreach (KeyValuePair<uint, PendingTrade> kv in pending)
                if (now - kv.Value.SentAt > merchantAnswerTimeout)
                    expired.Add(kv.Key);
            foreach (uint seq in expired)
            {
                PendingTrade p = pending[seq];
                pending.Remove(seq);
                Answer(p.Done, MerchantTransactionResult.Fail(MerchantTransactionStatus.Failed, p.Request, "The server did not answer."));
            }
        }

        private void FailAllPending(string message)
        {
            if (pending.Count == 0)
                return;
            var all = new List<PendingTrade>(pending.Values);
            pending.Clear();
            foreach (PendingTrade p in all)
                Answer(p.Done, MerchantTransactionResult.Fail(MerchantTransactionStatus.Failed, p.Request, message));
        }

        private static void Answer(Action<MerchantTransactionResult> done, MerchantTransactionResult result)
        {
            try { done?.Invoke(result); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ client: NPC sessions
        [TargetRpc]
        private void TargetNpcMenu(NetworkConnection conn, InteractTargetRef npcRef, string greeting)
        {
            if (IsServerInitialized)
                return;
            NPC npc = ResolveNpc(npcRef, PlayerScene);
            if (npc == null)
            {
                ServerNpcClose(npcRef); // it does not exist here: end the server's session
                return;
            }
            npc.ShowServerSession(PlayerObject, interactor, null, greeting ?? "", false);
        }

        [TargetRpc]
        private void TargetNpcBehaviour(NetworkConnection conn, InteractTargetRef npcRef, int behaviourIndex, bool fromMenu)
        {
            if (IsServerInitialized)
                return;
            NPC npc = ResolveNpc(npcRef, PlayerScene);
            NPCBehaviour behaviour = npc != null ? npc.BehaviourAt(behaviourIndex) : null;
            if (behaviour == null)
            {
                ServerNpcClose(npcRef);
                return;
            }
            npc.ShowServerSession(PlayerObject, interactor, behaviour, "", fromMenu);
        }

        [TargetRpc]
        private void TargetNpcEnded(NetworkConnection conn, InteractTargetRef npcRef, string reason)
        {
            if (IsServerInitialized)
                return;
            NPC npc = ResolveNpc(npcRef, PlayerScene);
            if (npc != null)
                npc.EndServerSession(PlayerObject, reason);
        }

        [TargetRpc]
        private void TargetMerchantStock(NetworkConnection conn, InteractTargetRef npcRef, int behaviourIndex, List<MerchantSlotState> slots, float secondsUntilRestock)
        {
            if (IsServerInitialized)
                return;
            NPC npc = ResolveNpc(npcRef, PlayerScene);
            if (npc != null && npc.BehaviourAt(behaviourIndex) is Merchant merchant)
                merchant.ApplyServerStock(slots, secondsUntilRestock);
        }

        private void ClientPick(NPCInteractionSession session, NPCBehaviour behaviour)
        {
            int index = session.Npc.BehaviourIndex(behaviour);
            if (CanSend && index >= 0 && InteractTargets.TryMake(session.Npc, out InteractTargetRef r))
                ServerNpcPick(r, index);
        }

        private void ClientFinished(NPCInteractionSession session, NPCBehaviour behaviour)
        {
            int index = session.Npc.BehaviourIndex(behaviour);
            if (CanSend && index >= 0 && InteractTargets.TryMake(session.Npc, out InteractTargetRef r))
                ServerNpcFinished(r, index);
        }

        private void ClientClose(NPCInteractionSession session)
        {
            if (CanSend && InteractTargets.TryMake(session.Npc, out InteractTargetRef r))
                ServerNpcClose(r);
        }

        private static string Clip(string text) =>
            string.IsNullOrEmpty(text) ? "" : text.Length <= MaxTextLength ? text : text.Substring(0, MaxTextLength);

        // ------------------------------------------------------------------ the NPC relay
        /// <summary>Routes NPC sessions between the server and the players' machines (installed while a session runs).</summary>
        private sealed class NpcRelay : INPCSessionRelay
        {
            public static readonly NpcRelay Instance = new NpcRelay();

            /// <summary>Server: the interactor of the session's player (null when it has none).</summary>
            private static NetInteractor Of(NPCInteractionSession s)
            {
                if (s == null || s.Player == null || s.Npc == null)
                    return null;
                // The session names the player's root (the object with its status), which may sit below the prefab root.
                NetInteractor n = s.Player.GetComponentInParent<NetInteractor>();
                if (n == null)
                    n = s.Player.GetComponentInChildren<NetInteractor>(true);
                return n != null ? n : null;
            }

            /// <summary>Client: this machine's player's interactor, when the session is theirs.</summary>
            private static NetInteractor Mine(NPCInteractionSession s)
            {
                NetInteractor local = Local;
                return local != null && s != null && s.Npc != null && s.Player == local.PlayerObject ? local : null;
            }

            public void ShowMenu(NPCInteractionSession session, string greeting)
            {
                NetInteractor n = Of(session);
                if (n != null) n.ServerShowMenu(session, greeting);
            }

            public void OpenBehaviour(NPCInteractionSession session, NPCBehaviour behaviour)
            {
                NetInteractor n = Of(session);
                if (n != null) n.ServerOpenBehaviour(session, behaviour);
            }

            public void SessionEnded(NPCInteractionSession session, string reason)
            {
                NetInteractor n = Of(session);
                if (n != null) n.ServerSessionEnded(session, reason);
            }

            public void PickOption(NPCInteractionSession session, NPCBehaviour behaviour)
            {
                NetInteractor n = Mine(session);
                if (n != null) n.ClientPick(session, behaviour);
            }

            public void BehaviourFinished(NPCInteractionSession session, NPCBehaviour behaviour)
            {
                NetInteractor n = Mine(session);
                if (n != null) n.ClientFinished(session, behaviour);
            }

            public void Close(NPCInteractionSession session)
            {
                NetInteractor n = Mine(session);
                if (n != null) n.ClientClose(session);
            }
        }
    }
}
#endif
