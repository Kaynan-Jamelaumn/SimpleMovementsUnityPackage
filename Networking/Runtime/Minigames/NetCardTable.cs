#if FISHNET
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// Makes a <see cref="CardTable"/> part of a network session. Put it on the table's root next to a NetworkObject
    /// (Tools ▸ SimpleMovements ▸ Minigames ▸ Make Selected Card Table Networked).
    /// <list type="bullet">
    /// <item>The server runs the table and the game. Sitting down is an interaction (relayed by the players'
    /// <see cref="NetInteractor"/>); everything else a seated player does (play a card, call Truco, Ready, Leave) is
    /// sent here and checked by the server like a local player's.</item>
    /// <item>Each seated player receives their own view (their hand, their buttons); everyone else the onlookers' view
    /// (no hands), late joiners included. Cards in other players' hands never leave the server.</item>
    /// </list>
    /// Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CardTable))]
    public class NetCardTable : NetworkBehaviour, ICardTableNet
    {
        [Tooltip("Least seconds between two view updates (several changes in that time are sent once).")]
        [SerializeField, Min(0.02f)] private float sendInterval = 0.1f;
        [Tooltip("Least seconds between two requests of the same client.")]
        [SerializeField, Min(0f)] private float requestInterval = 0.12f;

        private CardTable table;
        private int sentVersion = int.MinValue;
        private float nextSend;
        private readonly Dictionary<int, float> nextRequest = new Dictionary<int, float>();
        private readonly List<(int seat, CombatEntity player)> seated = new List<(int, CombatEntity)>();
        private readonly HashSet<NetworkConnection> viewers = new HashSet<NetworkConnection>();
        private readonly HashSet<NetworkConnection> stillSeated = new HashSet<NetworkConnection>();

        private CardTable Table => table != null ? table : (table = GetComponent<CardTable>());

        public bool RunsTableRules => IsServerInitialized; // not "IsServer": Fish-Net's NetworkBehaviour already has one

        public override void OnStartServer()
        {
            base.OnStartServer();
            Table.Net = this;
            sentVersion = int.MinValue;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (table != null && ReferenceEquals(table.Net, this))
                table.Net = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized)
                Table.Net = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsServerInitialized && table != null && ReferenceEquals(table.Net, this))
                table.Net = null;
        }

        // ------------------------------------------------------------------ server: views
        private void Update()
        {
            if (!IsServerInitialized)
                return;
            CardTable t = Table;
            int version = t.ViewVersion;
            if (version == sentVersion || Time.unscaledTime < nextSend)
                return;
            sentVersion = version;
            nextSend = Time.unscaledTime + sendInterval;

            byte[] onlookers = t.BuildView(-1).ToBytes();
            ObserversView(onlookers);
            t.SeatedPlayers(seated);
            stillSeated.Clear();
            foreach ((int seat, CombatEntity player) in seated)
            {
                NetworkConnection conn = NetObjects.OwnerOf(player);
                if (conn == null || !conn.IsActive || conn.IsLocalClient)
                    continue; // the host's own player sees the table directly
                stillSeated.Add(conn);
                viewers.Add(conn);
                TargetView(conn, t.BuildView(seat).ToBytes());
            }
            // Players who stood up get the onlookers' view once (their window closes).
            viewers.RemoveWhere(c => c == null || !c.IsActive);
            var left = new List<NetworkConnection>();
            foreach (NetworkConnection c in viewers)
                if (!stillSeated.Contains(c)) left.Add(c);
            foreach (NetworkConnection c in left)
            {
                viewers.Remove(c);
                TargetView(c, onlookers);
            }
        }

        [ObserversRpc(ExcludeServer = true, BufferLast = true)]
        private void ObserversView(byte[] view)
        {
            if (view != null && view.Length <= 65536)
                Table.ReceivePublicView(view);
        }

        [TargetRpc]
        private void TargetView(NetworkConnection conn, byte[] view)
        {
            if (view != null && view.Length <= 65536)
                Table.ReceivePrivateView(view);
        }

        [TargetRpc]
        private void TargetMessage(NetworkConnection conn, string text) => Table.ShowLocalMessage(text);

        void ICardTableNet.Tell(CombatEntity player, string text)
        {
            if (!IsServerInitialized || string.IsNullOrEmpty(text))
                return;
            NetworkConnection conn = NetObjects.OwnerOf(player);
            if (conn != null && conn.IsActive)
                TargetMessage(conn, text.Length > 200 ? text.Substring(0, 200) : text);
        }

        // ------------------------------------------------------------------ client: actions
        void ICardTableNet.SendAction(CardAction action)
        {
            if (IsClientInitialized && !IsServerInitialized)
                ServerAct(action.kind, action.arg);
        }

        /// <summary>A seated client does something; the server checks it like a local player's action.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerAct(int kind, int arg, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive)
                return;
            float now = Time.unscaledTime;
            if (nextRequest.TryGetValue(conn.ClientId, out float allowed) && now < allowed)
                return;
            nextRequest[conn.ClientId] = now + requestInterval;
            CombatEntity player = NetObjects.PlayerOf(conn);
            if (player == null)
                return;
            if (!Table.Act(player, new CardAction(kind, arg), out string reason) && !string.IsNullOrEmpty(reason))
                TargetMessage(conn, reason.Length > 200 ? reason.Substring(0, 200) : reason);
        }
    }
}
#endif
