#if FISHNET
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameNet
{
    /// <summary>
    /// Networks a player's inventory (<see cref="InventoryManager"/>): the server's copy is the truth.
    /// <list type="bullet">
    /// <item>Server to owner: the whole inventory (<see cref="InventorySnapshot"/>) after every change - a burst of changes
    /// is one message, and a slow poll catches changes made without an inventory event (durability, rounds). Only the
    /// owner receives it. Its client shows it without running gameplay.</item>
    /// <item>Owner to server: every inventory action as a request (<see cref="InventoryRequest"/>): drag and drop, split,
    /// turn, hotbar selection, use, quickslots, armor commands, picking items up, opening and closing storages. The server
    /// checks it (ownership, the item still in that slot, alive, range to world objects, how long a pickup was held, rate
    /// limits) and repeats it through the inventory's own code, so every rule applies. Rearranging is shown on the client
    /// at once; the snapshot confirms or corrects it.</item>
    /// <item>Server to everyone watching: what the player holds and wears (<see cref="InventoryAppearance"/>), shown with the
    /// normal equipment visuals on their copies.</item>
    /// </list>
    /// Put it on the player prefab root next to the NetworkObject (the inventory UI must be part of the player prefab).
    /// Offline it does nothing and the inventory works exactly as before.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetInventory : NetworkBehaviour, IInventoryNetBridge
    {
        /// <summary>Seconds a pickup's hold may be shorter than its Pick Up Time (network jitter).</summary>
        public const float HoldTolerance = 0.25f;
        /// <summary>Longest feedback message forwarded to the owner.</summary>
        public const int MaxMessageLength = 200;

        [Tooltip("The inventory to network. Empty = the player's InventoryManager (on this object, its children or its UI).")]
        [SerializeField] private InventoryManager inventory;
        [Tooltip("Shortest time between two inventory snapshots sent to the owner (a burst of changes is one message).")]
        [SerializeField, Range(0f, 0.5f)] private float minSendInterval = 0.05f;
        [Tooltip("Seconds between checks for changes made without an inventory event (durability, rounds loaded).")]
        [SerializeField, Range(0.1f, 5f)] private float pollInterval = 0.5f;
        [Tooltip("Furthest an item may be from the player (server positions) to be picked up, in metres.")]
        [SerializeField, Range(1f, 20f)] private float maxPickupDistance = 6f;
        [Tooltip("Furthest a storage may be from the player to be opened; further away an open storage closes.")]
        [SerializeField, Range(1f, 20f)] private float maxStorageDistance = 6f;

        private readonly SyncVar<InventoryAppearance> appearance = new SyncVar<InventoryAppearance>();

        // Server
        private InventorySendSchedule schedule = new InventorySendSchedule(0.05f, 0.5f);
        private InventorySnapshot lastSent;
        private uint handledSeq;
        private bool ownerReady;
        private bool serverHooked;
        private float nextAppearanceCheck;
        private float nextStorageCheck;
        private GameObject holdTarget;
        private float holdStartedAt;
        private WeaponNetMath.RateLimiter requestLimiter = new WeaponNetMath.RateLimiter(30f, 20f);
        private WeaponNetMath.RateLimiter useLimiter = new WeaponNetMath.RateLimiter(8f, 4f);
        private WeaponNetMath.RateLimiter worldLimiter = new WeaponNetMath.RateLimiter(10f, 6f);
        private WeaponNetMath.RateLimiter messageLimiter = new WeaponNetMath.RateLimiter(4f, 3f);

        // Clients
        private uint sentSeq;
        private bool remoteDirty;
        private EquipmentVisuals ownerVisuals;

        /// <summary>The inventory this component networks.</summary>
        public InventoryManager Inventory => inventory;

        /// <summary>What the other players see of this inventory.</summary>
        public InventoryAppearance Appearance => appearance.Value;

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            if (inventory == null)
                inventory = GetComponentInChildren<InventoryManager>(true);
            if (inventory == null)
                inventory = InventoryManager.For(this);
        }

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            Resolve();
            schedule = new InventorySendSchedule(minSendInterval, pollInterval);
            HookServer(true);
            UpdateAppearance();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            HookServer(false); // nothing is replicated any more
            // The player leaves: the storage it had open is saved (while its items still exist) and freed for the others.
            if (inventory != null)
                inventory.CloseStorageIf(null);
            ownerReady = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Resolve();
            appearance.OnChange += OnAppearanceChanged;
            if (!IsServerInitialized)
            {
                SetBridge(true);
                remoteDirty = true;
                if (IsOwner)
                    AskForSnapshot();
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            appearance.OnChange -= OnAppearanceChanged;
            HookOwnerVisuals(false);
            SetBridge(false);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsServerInitialized)
                return;
            remoteDirty = true;
            if (IsOwner)
                AskForSnapshot();
            else
                HookOwnerVisuals(false);
        }

        private void SetBridge(bool on)
        {
            if (inventory == null)
                return;
            if (on)
                inventory.NetBridge = this;
            else if (ReferenceEquals(inventory.NetBridge, this))
                inventory.NetBridge = null;
        }

        private void AskForSnapshot()
        {
            // The server's first snapshot is sent when the client says it is ready (an RPC before the spawn would be lost).
            if (inventory != null)
                ((IInventoryNetBridge)this).Send(inventory, new InventoryRequest { kind = (byte)InventoryRequestKind.Resync });
        }

        private void HookServer(bool on)
        {
            if (inventory == null || on == serverHooked)
                return;
            serverHooked = on;
            if (on)
            {
                inventory.InventoryChanged += OnServerInventoryChanged;
                inventory.HotbarSelectionChanged += OnServerInventoryChanged;
                inventory.MessageShown += OnServerMessage;
            }
            else
            {
                inventory.InventoryChanged -= OnServerInventoryChanged;
                inventory.HotbarSelectionChanged -= OnServerInventoryChanged;
                inventory.MessageShown -= OnServerMessage;
            }
        }

        private void OnDestroy()
        {
            HookServer(false);
            HookOwnerVisuals(false);
            SetBridge(false);
        }

        private void Update()
        {
            if (inventory == null)
                return;
            if (IsServerInitialized)
            {
                ServerUpdate();
                return;
            }
            if (!IsClientInitialized)
                return;
            if (IsOwner)
            {
                HookOwnerVisuals(true);
            }
            else if (remoteDirty && inventory.ApplyAppearance(appearance.Value))
            {
                remoteDirty = false; // shown once the copy's inventory has built its slots
            }
        }

        // ------------------------------------------------------------------ server: sending
        private void ServerUpdate()
        {
            float now = Time.unscaledTime;
            if (now >= nextAppearanceCheck)
            {
                nextAppearanceCheck = now + 0.2f;
                UpdateAppearance(); // drawn / sheathed changes without an inventory event
            }
            if (now >= nextStorageCheck)
            {
                nextStorageCheck = now + 0.5f;
                CheckOpenStorage();
            }
            NetworkConnection owner = Owner;
            if (!ownerReady || owner == null || !owner.IsValid || owner.IsLocalClient)
                return; // the host's own player is the server's copy itself
            switch (schedule.Evaluate(now))
            {
                case InventorySendSchedule.Decision.Send:
                    SendSnapshot(owner, inventory.CaptureSnapshot(handledSeq), now);
                    break;
                case InventorySendSchedule.Decision.Poll:
                    InventorySnapshot s = inventory.CaptureSnapshot(handledSeq);
                    if (lastSent == null || !s.SameState(lastSent))
                        SendSnapshot(owner, s, now);
                    else
                        schedule.Polled(now);
                    break;
            }
        }

        private void SendSnapshot(NetworkConnection owner, InventorySnapshot s, float now)
        {
            TargetInventorySnapshot(owner, s);
            lastSent = s;
            schedule.Sent(now);
        }

        private void OnServerInventoryChanged()
        {
            schedule.MarkDirty();
            UpdateAppearance();
        }

        private void UpdateAppearance()
        {
            if (!IsServerInitialized || inventory == null)
                return;
            InventoryAppearance a = inventory.CaptureAppearance();
            if (!a.Equals(appearance.Value))
                appearance.Value = a;
        }

        private void OnServerMessage(string text, InventoryFeedback.Kind kind)
        {
            NetworkConnection owner = Owner;
            if (!IsServerInitialized || owner == null || !owner.IsValid || owner.IsLocalClient || string.IsNullOrEmpty(text))
                return;
            if (!messageLimiter.TryTake(Time.unscaledTime))
                return;
            TargetInventoryMessage(owner, text.Length > MaxMessageLength ? text.Substring(0, MaxMessageLength) : text, (byte)kind);
        }

        /// <summary>The server closes a storage the player walked away from, can no longer use (dead), or that is gone.</summary>
        private void CheckOpenStorage()
        {
            if (!inventory.IsStorageOpened)
                return;
            bool alive = IsAlive;
            Vector3 at = PlayerPosition;
            Scene scene = PlayerScene;
            float max = maxStorageDistance * maxStorageDistance;
            inventory.CloseStorageIf(s => !alive || !s.isActiveAndEnabled || s.gameObject.scene != scene || (s.transform.position - at).sqrMagnitude > max);
        }

        // ------------------------------------------------------------------ server: requests
        private void Acknowledge(uint seq)
        {
            // Every request is answered by a snapshot, refused ones too: the client then shows the server's truth.
            ownerReady = true;
            if ((int)(seq - handledSeq) > 0)
                handledSeq = seq;
            schedule.MarkDirty();
        }

        [ServerRpc]
        private void CmdInventoryRequest(InventoryRequest request)
        {
            if (inventory == null)
                return;
            Acknowledge(request.seq);
            if (!InventoryNetRules.IsWellFormed(request) || IsWorldKind(request.Kind))
                return;
            float now = Time.unscaledTime;
            InventoryRequestKind kind = request.Kind;
            if (kind != InventoryRequestKind.Resync && !requestLimiter.TryTake(now))
                return;
            bool use = kind == InventoryRequestKind.UseSelected || kind == InventoryRequestKind.UseItem;
            if (use && !useLimiter.TryTake(now))
                return;
            if (!IsAlive && !AllowedWhileDead(kind))
                return;
            if (!inventory.ApplyRequest(request, out string reason) && !string.IsNullOrEmpty(reason))
                OnServerMessage(reason, InventoryFeedback.Kind.Warning);
        }

        [ServerRpc]
        private void CmdInventoryWorldRequest(InventoryRequest request, NetworkObject target)
        {
            if (inventory == null)
                return;
            Acknowledge(request.seq);
            if (!InventoryNetRules.IsWellFormed(request) || !IsWorldKind(request.Kind))
                return;
            if (!worldLimiter.TryTake(Time.unscaledTime) || !IsAlive)
                return;
            GameObject go = ResolveTarget(target, request.path);
            if (go == null || go.scene != PlayerScene)
                return;
            switch (request.Kind)
            {
                case InventoryRequestKind.PickupHold:
                    holdTarget = go;
                    holdStartedAt = Time.time;
                    break;
                case InventoryRequestKind.Pickup:
                    ServerPickup(go, request.itemId);
                    break;
                case InventoryRequestKind.OpenStorage:
                    ServerOpenStorage(go);
                    break;
            }
        }

        private static bool IsWorldKind(InventoryRequestKind kind) =>
            kind == InventoryRequestKind.Pickup || kind == InventoryRequestKind.PickupHold || kind == InventoryRequestKind.OpenStorage;

        private static bool AllowedWhileDead(InventoryRequestKind kind) =>
            kind == InventoryRequestKind.SelectHotbar || kind == InventoryRequestKind.CloseStorage || kind == InventoryRequestKind.Resync
            || kind == InventoryRequestKind.SetDrawn || kind == InventoryRequestKind.AssignQuickSlot;

        private GameObject ResolveTarget(NetworkObject target, string path)
        {
            if (target != null)
                return NetObjects.IsSpawned(target) && target != NetworkObject ? target.gameObject : null;
            if (string.IsNullOrEmpty(path))
                return null;
            Transform t = SceneObjectPath.Find(PlayerScene, path);
            // A path names only objects without a network identity of their own (networked ones are sent by reference).
            return t != null && t.GetComponent<NetworkObject>() == null ? t.gameObject : null;
        }

        private void ServerPickup(GameObject go, string expectedItemId)
        {
            ItemPickable p = go.GetComponent<ItemPickable>();
            if (p == null)
                p = go.GetComponentInChildren<ItemPickable>();
            if (p == null || !p.isActiveAndEnabled || p.Taken || p.itemScriptableObject == null)
                return;
            if (!string.IsNullOrEmpty(expectedItemId) && ItemDatabase.Id(p.itemScriptableObject) != expectedItemId)
                return; // it changed meanwhile
            if ((p.transform.position - PlayerPosition).sqrMagnitude > maxPickupDistance * maxPickupDistance)
            {
                OnServerMessage("Too far away to pick that up.", InventoryFeedback.Kind.Info);
                return;
            }
            if (p.InteractionTime > 0f)
            {
                bool heldLongEnough = holdTarget == go && Time.time - holdStartedAt >= p.InteractionTime - HoldTolerance;
                holdTarget = null;
                if (!heldLongEnough)
                    return;
            }
            inventory.ItemPicked(p.gameObject);
        }

        private void ServerOpenStorage(GameObject go)
        {
            Storage storage = go.GetComponent<Storage>();
            if (storage == null)
                storage = go.GetComponentInParent<Storage>();
            if (storage == null)
                storage = go.GetComponentInChildren<Storage>();
            if (storage == null || !storage.isActiveAndEnabled || storage.gameObject.scene != PlayerScene)
                return;
            if ((storage.transform.position - PlayerPosition).sqrMagnitude > maxStorageDistance * maxStorageDistance)
            {
                OnServerMessage("Too far away to open that.", InventoryFeedback.Kind.Info);
                return;
            }
            if (inventory.IsStorageOpened && inventory.OpenedStorage == storage)
                return;
            inventory.TryOpenStorage(storage, out _);
        }

        private bool IsAlive
        {
            get
            {
                CombatEntity e = inventory != null ? inventory.PlayerEntity : null;
                return e == null || e.IsAlive;
            }
        }

        private Vector3 PlayerPosition
        {
            get
            {
                CombatEntity e = inventory != null ? inventory.PlayerEntity : null;
                if (e != null)
                    return e.Position;
                return inventory != null && inventory.Player != null ? inventory.Player.transform.position : transform.position;
            }
        }

        private Scene PlayerScene => inventory != null && inventory.Player != null ? inventory.Player.scene : gameObject.scene;

        // ------------------------------------------------------------------ server: scene items without a network identity
        /// <summary>
        /// A scene item without a network identity was picked up by this player on the server: every client watching the
        /// player removes (or updates) its own copy of that item.
        /// </summary>
        internal void ServerSceneItemTaken(string path, int remaining)
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(path) || path.Length > InventoryNetRules.MaxPathLength)
                return;
            RpcSceneItemTaken(path, remaining);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void RpcSceneItemTaken(string path, int remaining)
        {
            Transform t = SceneObjectPath.Find(gameObject.scene, path);
            ItemPickable p = t != null ? t.GetComponent<ItemPickable>() : null;
            if (p == null || p.GetComponent<NetworkObject>() != null)
                return;
            if (remaining <= 0)
                Destroy(p.gameObject);
            else
                p.quantity = remaining;
        }

        // ------------------------------------------------------------------ clients
        void IInventoryNetBridge.Send(InventoryManager from, InventoryRequest request)
        {
            if (!IsClientInitialized || !IsOwner || IsServerInitialized)
                return;
            request.seq = ++sentSeq;
            CmdInventoryRequest(request);
        }

        void IInventoryNetBridge.SendWorld(InventoryManager from, InventoryRequest request, GameObject target)
        {
            if (!IsClientInitialized || !IsOwner || IsServerInitialized || target == null)
                return;
            NetworkObject nob = NetObjects.Of(target);
            // A networked object is named by reference - when the target is that object (not a plain child of it).
            bool byReference = nob != null && NetObjects.IsSpawned(nob) && nob != NetworkObject
                               && (nob.gameObject == target || nob.GetComponent<ItemPickable>() != null || nob.GetComponent<Storage>() != null);
            if (byReference)
            {
                request.path = null;
            }
            else
            {
                nob = null;
                request.path = SceneObjectPath.Of(target.transform);
                if (string.IsNullOrEmpty(request.path) || request.path.Length > InventoryNetRules.MaxPathLength)
                    return;
            }
            request.seq = ++sentSeq;
            CmdInventoryWorldRequest(request, nob);
        }

        [TargetRpc]
        private void TargetInventorySnapshot(NetworkConnection conn, InventorySnapshot snapshot)
        {
            if (IsServerInitialized || inventory == null || snapshot == null)
                return;
            // Sent before the server had our latest request: a newer one is on its way (this one would undo what is shown).
            if (!InventoryNetRules.ShouldApply(snapshot.ack, sentSeq))
                return;
            inventory.ApplySnapshot(snapshot);
        }

        [TargetRpc]
        private void TargetInventoryMessage(NetworkConnection conn, string text, byte kind)
        {
            if (IsServerInitialized || inventory == null)
                return;
            inventory.ShowMessage(text, kind <= (byte)InventoryFeedback.Kind.Error ? (InventoryFeedback.Kind)kind : InventoryFeedback.Kind.Warning);
        }

        private void OnAppearanceChanged(InventoryAppearance previous, InventoryAppearance next, bool asServer)
        {
            if (asServer || IsServerInitialized)
                return;
            remoteDirty = true; // shown in Update (the copy's inventory may still be building its slots)
        }

        /// <summary>The owner's weapons were drawn or sheathed (its key, or a fight): the server and the others follow.</summary>
        private void HookOwnerVisuals(bool on)
        {
            EquipmentVisuals v = on && inventory != null ? inventory.Visuals : null;
            if (v == ownerVisuals)
                return;
            if (ownerVisuals != null)
                ownerVisuals.DrawnChanged -= OnOwnerDrawnChanged;
            ownerVisuals = v;
            if (ownerVisuals != null)
                ownerVisuals.DrawnChanged += OnOwnerDrawnChanged;
        }

        private void OnOwnerDrawnChanged(bool drawn)
        {
            if (inventory != null && IsOwner && !IsServerInitialized)
                inventory.SendDrawnState(drawn);
        }
    }
}
#endif
