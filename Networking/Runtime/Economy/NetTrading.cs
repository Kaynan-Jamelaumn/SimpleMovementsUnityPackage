#if FISHNET
using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using HubWorld;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Player-to-player trading in a network session (a global relay: put it on the "Network World" object of the
    /// Bootstrap scene, which every client observes). The rules are <see cref="TradeBook"/>'s; this component is their
    /// server authority and the clients' link to it.
    /// <list type="bullet">
    /// <item><b>Starting</b>: a player presses the trade key (T) looking at another player (or near one): the server checks
    /// both are alive, in the same realm scene and within range, and the other player gets a request to accept or decline.</item>
    /// <item><b>Offers</b>: each player offers stacks of their bag or hotbar (by slot, with the item expected there) and
    /// money; the server checks every offer against its own inventory and wallet. Any change of an offer unlocks both sides.</item>
    /// <item><b>Exchange</b>: both lock, then both confirm the same revision; the server checks everything again and swaps
    /// atomically on its inventories (<see cref="TradeExchange"/>: all or nothing - room and ownership first, then the move,
    /// undone completely if anything fails). The inventory module then sends each owner their new inventory.</item>
    /// <item><b>Replication</b>: each player receives only their own trade (<see cref="TradeView"/>), shown by
    /// <see cref="TradeWindow"/>.</item>
    /// </list>
    /// Offline single player there is nobody to trade with: the component does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetTrading : NetworkBehaviour
    {
        /// <summary>The relay of this session (null outside one).</summary>
        public static NetTrading Instance { get; private set; }

        [Tooltip("Limits: stacks per side, request and idle timeouts, how far apart the players may be.")]
        [SerializeField] private TradeRules rules = new TradeRules();
        [Tooltip("Currency players can offer. Empty = the default currency.")]
        [SerializeField] private CurrencyDefinition currency;
        [Tooltip("Seconds between two trade requests of the same client (extra requests are dropped).")]
        [SerializeField, Min(0.02f)] private float requestInterval = 0.12f;
        [Tooltip("The default key of the Trade input (asks the player you look at, or the nearest one, to trade; accepts a request). " +
                 "The input actions' \"Trade\" action is used when there is one (with your rebinds); otherwise a Trade action is added on this key, rebindable in Key Bindings.")]
        [SerializeField] private KeyCode tradeKey = KeyCode.L;
        [Tooltip("Show the built-in trade window on clients (turn off to use your own UI with View / ViewChanged).")]
        [SerializeField] private bool createWindow = true;

        private TradeBook book;
        private readonly Dictionary<int, float> nextRequest = new Dictionary<int, float>();
        private readonly Dictionary<int, string> dirty = new Dictionary<int, string>();
        private readonly List<Trade> ended = new List<Trade>();
        private readonly List<int> sendBuffer = new List<int>();
        private float nextCheck;

        private TradeView view = TradeView.Closed("");
        private TradeWindow window;

        /// <summary>This client's trade as the server last sent it.</summary>
        public TradeView View => view;
        /// <summary>The currency of trades.</summary>
        public CurrencyDefinition Currency => currency != null ? currency : CurrencyDefinition.Default;
        /// <summary>The trading limits.</summary>
        public TradeRules Rules => rules;
        /// <summary>The trade key's default (the Trade input action is what is read).</summary>
        public KeyCode TradeKey => tradeKey;

        private GlobalInputBinding tradeInput;

        /// <summary>The Trade input (the input actions' "Trade" action, with the player's rebinds).</summary>
        public GlobalInputBinding TradeInput => tradeInput ??= GlobalInputBinding.Create(new[] { "Trade", "TradeRequest", "RequestTrade", "InviteTrade" },
            new[] { "<Keyboard>/" + tradeKey.ToString().ToLowerInvariant() }, "Trade", true, true, this);

        /// <summary>What the trade key is now, for prompts ("L").</summary>
        public string TradeKeyLabel
        {
            get
            {
                string k = TradeInput.KeyLabel;
                return string.IsNullOrEmpty(k) ? tradeKey.ToString() : k;
            }
        }
        /// <summary>The server's trades (server only; null on clients).</summary>
        public TradeBook Book => book;

        /// <summary>Raised on a client when its trade changed (opened, an offer, a lock, closed).</summary>
        public event Action<TradeView> ViewChanged;
        /// <summary>Raised on a client with a message about its trade ("Bram declined the trade.").</summary>
        public event Action<string> Message;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[Net] Two NetTrading relays ('{Instance.name}', '{name}') - keep one on the Network World object.", this);
            Instance = this;
            if (rules == null)
                rules = new TradeRules();
        }

        private void OnDestroy()
        {
            tradeInput?.Dispose();
            tradeInput = null;
            if (Instance == this)
                Instance = null;
            if (window != null)
                Destroy(window.gameObject);
        }

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            book = new TradeBook(rules);
            nextRequest.Clear();
            dirty.Clear();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            book = null;
            nextRequest.Clear();
            dirty.Clear();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            view = TradeView.Closed("");
            if (createWindow && NetAuthority.HasLocalPlayer && window == null)
            {
                var go = new GameObject("Trade Window");
                go.transform.SetParent(transform, false);
                window = go.AddComponent<TradeWindow>();
                window.Bind(this);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            SetView(TradeView.Closed(""));
        }

        private void Update()
        {
            if (IsServerInitialized && book != null)
                ServerUpdate();
            if (IsClientInitialized)
                ClientUpdate();
        }

        // ------------------------------------------------------------------ server
        private double Now => Time.unscaledTimeAsDouble;

        private void ServerUpdate()
        {
            float now = Time.unscaledTime;
            if (now >= nextCheck)
            {
                nextCheck = now + 0.25f;
                ended.Clear();
                book.Expire(Now, WhyApart, ended);
                foreach (Trade t in ended)
                {
                    MarkDirty(t.Proposer.PlayerId, t.EndReason);
                    MarkDirty(t.Partner.PlayerId, t.EndReason);
                }
                RecheckLockedOffers();
            }
            if (dirty.Count > 0)
                SendDirty();
        }

        /// <summary>Locked offers must stay valid (an offered stack moved or used up unlocks both sides).</summary>
        private void RecheckLockedOffers()
        {
            IReadOnlyList<Trade> trades = book.Trades;
            for (int i = 0; i < trades.Count; i++)
            {
                Trade t = trades[i];
                if (t.State != TradeState.Open || (!t.Proposer.Locked && !t.Partner.Locked))
                    continue;
                if (OfferValid(t.Proposer, out string reason) && OfferValid(t.Partner, out reason))
                    continue;
                book.Unlock(t, Now);
                MarkDirty(t.Proposer.PlayerId, reason);
                MarkDirty(t.Partner.PlayerId, reason);
            }
        }

        private bool OfferValid(TradeSide side, out string reason)
        {
            reason = "";
            TradeParty party = PartyOf(side.PlayerId, side.Items, side.Currency);
            if (party == null)
            {
                reason = "A player left.";
                return false;
            }
            return TradeExchange.Validate(party, Currency, out reason);
        }

        /// <summary>Why two players cannot (or can no longer) trade; null when they can.</summary>
        private string WhyApart(int a, int b)
        {
            CombatEntity ea = PlayerIds.Find(a);
            CombatEntity eb = PlayerIds.Find(b);
            if (ea == null || eb == null)
                return "The trade was cancelled: the other player left.";
            if (!ea.IsAlive || !eb.IsAlive)
                return "The trade was cancelled.";
            if (CombatRules.AreSeparated(ea, eb) || ea.gameObject.scene != eb.gameObject.scene)
                return "The trade was cancelled: you are no longer in the same place.";
            float range = Mathf.Max(1f, rules.range);
            if ((ea.Position - eb.Position).sqrMagnitude > range * range)
                return "The trade was cancelled: you are too far apart.";
            return null;
        }

        private TradeParty PartyOf(int playerId, IReadOnlyList<TradeItemOffer> items, int money)
        {
            CombatEntity e = PlayerIds.Find(playerId);
            if (e == null)
                return null;
            return new TradeParty
            {
                Inventory = InventoryManager.For(e),
                Wallet = CurrencyWallet.For(e, true),
                Items = items,
                Currency = money,
                Name = NameOf(playerId),
            };
        }

        private static string NameOf(int playerId)
        {
            CombatEntity e = PlayerIds.Find(playerId);
            if (e == null)
                return "Someone";
            NetworkObject nob = NetObjects.Of(e);
            NetPlayer p = nob != null ? nob.GetComponent<NetPlayer>() : null;
            return p != null && !string.IsNullOrEmpty(p.DisplayName.Value) ? p.DisplayName.Value : e.gameObject.name;
        }

        /// <summary>
        /// The caller's player, when the request is allowed now (one request per interval; <paramref name="rateLimited"/>
        /// false for a cancel, which must never be lost - the replies are merged per frame anyway).
        /// </summary>
        private bool Caller(NetworkConnection conn, out CombatEntity me, out int myId, bool rateLimited = true)
        {
            me = null;
            myId = 0;
            if (book == null || conn == null || !conn.IsActive)
                return false;
            if (rateLimited)
            {
                float now = Time.unscaledTime;
                if (nextRequest.TryGetValue(conn.ClientId, out float allowedAt) && now < allowedAt)
                    return false;
                nextRequest[conn.ClientId] = now + requestInterval;
            }
            me = NetObjects.PlayerOf(conn);
            if (me == null)
                return false;
            myId = PlayerIds.Of(me);
            return myId > 0;
        }

        private void MarkDirty(int playerId, string message = null)
        {
            if (playerId <= 0)
                return;
            if (!dirty.TryGetValue(playerId, out string existing) || string.IsNullOrEmpty(existing))
                dirty[playerId] = message ?? "";
        }

        private void SendDirty()
        {
            sendBuffer.Clear();
            sendBuffer.AddRange(dirty.Keys);
            foreach (int id in sendBuffer)
            {
                string message = dirty[id];
                dirty.Remove(id);
                CombatEntity e = PlayerIds.Find(id);
                NetworkConnection conn = e != null ? NetObjects.OwnerOf(e) : null;
                if (conn == null || !conn.IsActive)
                    continue;
                Trade t = book.TradeOf(id);
                TradeView v = t != null ? TradeView.For(t, id, NameOf, Now, rules, message) : TradeView.Closed(message);
                TargetTradeView(conn, v);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTradePropose(int targetId, NetworkConnection conn = null)
        {
            if (!Caller(conn, out CombatEntity me, out int myId))
                return;
            CombatEntity target = PlayerIds.Find(targetId);
            if (target == null || target == me || NetObjects.OwnerOf(target) == null)
                return;
            if (!me.IsAlive)
                return;
            string apart = WhyApart(myId, targetId);
            if (apart != null)
            {
                Tell(conn, $"{NameOf(targetId)} is too far away to trade.");
                return;
            }
            Trade t = book.Propose(myId, targetId, Now, out string reason);
            if (t == null)
            {
                Tell(conn, reason);
                return;
            }
            MarkDirty(myId, t.State == TradeState.Open ? "" : $"You asked {NameOf(targetId)} to trade.");
            MarkDirty(targetId, t.State == TradeState.Open ? "" : $"{NameOf(myId)} wants to trade with you.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTradeRespond(bool accept, NetworkConnection conn = null)
        {
            if (!Caller(conn, out _, out int myId))
                return;
            Trade t = book.TradeOf(myId);
            if (t == null)
            {
                MarkDirty(myId);
                return;
            }
            int other = t.OtherSide(myId).PlayerId;
            if (accept)
            {
                if (book.Accept(myId, Now, out string reason) == null)
                {
                    MarkDirty(myId, reason);
                    return;
                }
                MarkDirty(myId);
                MarkDirty(other, $"{NameOf(myId)} accepted the trade.");
                return;
            }
            bool wasRequest = t.State == TradeState.Proposed && t.Partner.PlayerId == myId;
            book.Cancel(myId, "declined");
            MarkDirty(myId, "");
            MarkDirty(other, wasRequest ? $"{NameOf(myId)} declined the trade." : $"{NameOf(myId)} cancelled the trade.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTradeOffer(List<TradeItemOffer> items, int money, NetworkConnection conn = null)
        {
            if (!Caller(conn, out _, out int myId))
                return;
            Trade t = book.TradeOf(myId);
            if (t == null || t.State != TradeState.Open)
            {
                MarkDirty(myId);
                return;
            }
            // Shape first (cheap), then against the server's own inventory and wallet.
            if (!TradeBook.IsValidOffer(items, money, rules.maxItemsPerSide, out string reason))
            {
                MarkDirty(myId, reason);
                return;
            }
            TradeParty party = PartyOf(myId, items, money);
            if (party == null || !TradeExchange.Validate(party, Currency, out reason))
            {
                MarkDirty(myId, string.IsNullOrEmpty(reason) ? "That offer is not possible." : reason);
                return;
            }
            int before = t.Revision;
            if (!book.SetOffer(myId, items, money, Now, out reason))
            {
                MarkDirty(myId, reason);
                return;
            }
            MarkDirty(myId);
            MarkDirty(t.OtherSide(myId).PlayerId, t.Revision != before ? $"{NameOf(myId)} changed their offer." : "");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTradeLock(bool locked, NetworkConnection conn = null)
        {
            if (!Caller(conn, out _, out int myId))
                return;
            Trade t = book.TradeOf(myId);
            if (t == null || t.State != TradeState.Open)
            {
                MarkDirty(myId);
                return;
            }
            if (locked && !OfferValid(t.SideOf(myId), out string invalid))
            {
                MarkDirty(myId, invalid);
                return;
            }
            if (!book.SetLocked(myId, locked, Now, out string reason))
            {
                MarkDirty(myId, reason);
                return;
            }
            MarkDirty(myId);
            MarkDirty(t.OtherSide(myId).PlayerId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTradeConfirm(int revision, NetworkConnection conn = null)
        {
            if (!Caller(conn, out _, out int myId))
                return;
            Trade t = book.TradeOf(myId);
            if (t == null)
            {
                MarkDirty(myId);
                return;
            }
            if (!book.Confirm(myId, revision, Now, out bool ready, out string reason))
            {
                MarkDirty(myId, reason);
                return;
            }
            int other = t.OtherSide(myId).PlayerId;
            if (!ready)
            {
                MarkDirty(myId);
                MarkDirty(other, $"{NameOf(myId)} confirmed the trade.");
                return;
            }
            Exchange(t);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerTradeCancel(NetworkConnection conn = null)
        {
            if (!Caller(conn, out _, out int myId, false))
                return;
            Trade t = book.Cancel(myId, "cancelled");
            if (t == null)
            {
                MarkDirty(myId);
                return;
            }
            MarkDirty(myId, "You cancelled the trade.");
            MarkDirty(t.OtherSide(myId).PlayerId, $"{NameOf(myId)} cancelled the trade.");
        }

        /// <summary>Both confirmed: the server swaps the offers (all or nothing).</summary>
        private void Exchange(Trade t)
        {
            int a = t.Proposer.PlayerId, b = t.Partner.PlayerId;
            string apart = WhyApart(a, b);
            TradeParty pa = PartyOf(a, t.Proposer.Items, t.Proposer.Currency);
            TradeParty pb = PartyOf(b, t.Partner.Items, t.Partner.Currency);
            string reason = "";
            if (apart != null)
            {
                book.Cancel(a, apart);
                MarkDirty(a, apart);
                MarkDirty(b, apart);
                return;
            }
            bool done;
            try { done = pa != null && pb != null && TradeExchange.Execute(pa, pb, Currency, out reason); }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                done = false;
                reason = "Something went wrong.";
            }
            if (pa == null || pb == null)
                reason = "A player left.";
            if (!done)
            {
                book.Unlock(t, Now);
                string text = string.IsNullOrEmpty(reason) ? "The trade could not be made." : $"The trade could not be made: {reason}";
                MarkDirty(a, text);
                MarkDirty(b, text);
                return;
            }
            book.Complete(t);
            MarkDirty(a, $"Trade with {NameOf(b)} completed.");
            MarkDirty(b, $"Trade with {NameOf(a)} completed.");
        }

        private void Tell(NetworkConnection conn, string text)
        {
            if (conn != null && conn.IsActive && !string.IsNullOrEmpty(text))
                TargetTradeMessage(conn, text.Length > 200 ? text.Substring(0, 200) : text);
        }

        // ------------------------------------------------------------------ client
        [TargetRpc]
        private void TargetTradeView(NetworkConnection conn, TradeView v)
        {
            if (v.myItems == null) v.myItems = new List<TradeItemOffer>();
            if (v.theirItems == null) v.theirItems = new List<TradeItemOffer>();
            SetView(v);
            if (!string.IsNullOrEmpty(v.message))
                ShowMessage(v.message);
        }

        [TargetRpc]
        private void TargetTradeMessage(NetworkConnection conn, string text) => ShowMessage(text);

        private void SetView(TradeView v)
        {
            view = v;
            try { ViewChanged?.Invoke(v); }
            catch (Exception e) { Debug.LogException(e, this); }
        }

        private void ShowMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            try { Message?.Invoke(text); }
            catch (Exception e) { Debug.LogException(e, this); }
            InventoryManager inv = LocalInventory;
            if (inv != null)
                inv.ShowMessage(text, InventoryFeedback.Kind.Info);
        }

        /// <summary>This machine's player (null on a dedicated server).</summary>
        public static CombatEntity LocalPlayer => NetPlayer.Local != null ? NetPlayer.Local.Entity : null;

        private static InventoryManager LocalInventory
        {
            get
            {
                CombatEntity me = LocalPlayer;
                return me != null ? InventoryManager.For(me) : null;
            }
        }

        private void ClientUpdate()
        {
            if (!NetAuthority.HasLocalPlayer || !TradeInput.Pressed || InputBindingStore.GameplayInputPaused)
                return;
            // The trade key answers a request received; otherwise it asks the player in view.
            if (view.State == TradeState.Proposed && view.incoming)
            {
                Respond(true);
                return;
            }
            if (view.State != TradeState.None)
                return;
            InventoryManager inv = LocalInventory;
            if (inv != null && (inv.IsInventoryOpened || inv.IsExternalPanelOpen))
                return;
            CombatEntity target = PickTarget();
            if (target == null)
            {
                ShowMessage("Look at a player to trade with them.");
                return;
            }
            RequestTrade(target);
        }

        /// <summary>The player the local player looks at (the interaction focus), else the nearest one in front within range.</summary>
        private CombatEntity PickTarget()
        {
            CombatEntity me = LocalPlayer;
            if (me == null)
                return null;
            PlayerInteractor pi = PlayerInteractor.For(me);
            if (pi != null && pi.Focused is Component c && c != null)
            {
                CombatEntity focused = PlayerLocator.FromObject(c.gameObject);
                if (focused != null && focused != me && focused.IsAlive)
                    return focused;
            }
            float range = Mathf.Max(1f, rules.range);
            Vector3 forward = me.transform.forward;
            CombatEntity best = null;
            float bestScore = float.MaxValue;
            foreach (NetPlayer p in NetPlayer.All)
            {
                CombatEntity e = p != null ? p.Entity : null;
                if (e == null || e == me || !e.IsAlive || e.gameObject.scene != me.gameObject.scene || CombatRules.AreSeparated(me, e))
                    continue;
                Vector3 to = e.Position - me.Position;
                float d = to.magnitude;
                if (d > range)
                    continue;
                float score = d + (d > 0.1f ? Vector3.Angle(forward, to) / 45f : 0f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }
            return best;
        }

        /// <summary>Asks <paramref name="other"/> to trade (client).</summary>
        public void RequestTrade(CombatEntity other)
        {
            int id = PlayerIds.Of(other);
            if (IsClientInitialized && id > 0)
                ServerTradePropose(id);
        }

        /// <summary>Accepts (true) or declines (false) the request this client received.</summary>
        public void Respond(bool accept)
        {
            if (IsClientInitialized)
                ServerTradeRespond(accept);
        }

        /// <summary>Replaces this client's offer (stacks by slot and money).</summary>
        public void SendOffer(List<TradeItemOffer> items, int money)
        {
            if (IsClientInitialized)
                ServerTradeOffer(items ?? new List<TradeItemOffer>(), Mathf.Max(0, money));
        }

        /// <summary>Locks (or unlocks) this client's offer.</summary>
        public void SetLocked(bool locked)
        {
            if (IsClientInitialized)
                ServerTradeLock(locked);
        }

        /// <summary>Confirms the trade as currently shown.</summary>
        public void Confirm()
        {
            if (IsClientInitialized)
                ServerTradeConfirm(view.revision);
        }

        /// <summary>Cancels the trade (or the request).</summary>
        public void Cancel()
        {
            if (IsClientInitialized)
                ServerTradeCancel();
        }
    }
}
#endif
