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
    /// Makes a <see cref="HarvestableCreature"/> part of a network session, with the server deciding everything. Put it on
    /// the mob prefab next to the creature (Tools ▸ SimpleMovements ▸ Harvesting ▸ Make Selected Mob Harvestable, and
    /// Make Selected Prefab Networked ▸ Mob for a harvestable mob, add it).
    /// <list type="bullet">
    /// <item><b>The server keeps the body</b>: where the hits landed, the carcass, every harvest. Its carcass goes to every
    /// client watching the mob after each change (and now and then, to set their clocks right); the copies spoil on their
    /// own in between.</item>
    /// <item><b>Harvesting</b> is an interaction (relayed by the player's <see cref="NetInteractor"/>): the server opens the
    /// carcass window on the player's screen and sends their skills. Starting a material asks the server, which plans the
    /// work from its own carcass, the player's tools and skills, and sends the plan back: the player's hands play exactly
    /// that plan. What the minigames measured comes back and is held to the plan (<see cref="HarvestRequestGuard"/>)
    /// before the server works out the pieces, gives them and sends the report.</item>
    /// <item>A player who walks away, dies or leaves frees what they were working on.</item>
    /// </list>
    /// Offline it does nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HarvestableCreature))]
    public class NetCarcass : NetworkBehaviour, IHarvestNet
    {
        /// <summary>Longest message sent to a client.</summary>
        public const int MaxMessageLength = 200;
        /// <summary>Largest harvest result a client may send (bytes, unpacked).</summary>
        public const int MaxResultSize = 256 * 1024;
        private const int MaxResultParts = 8;
        private const int MaxIdLength = 64;
        private const int MaxUnits = 256;
        private const byte CarcassData = 0, PlanData = 1, ReportData = 2, SkillsData = 3;

        [Tooltip("Least seconds between two carcass updates sent to the clients (changes in between go together).")]
        [SerializeField, Range(0.05f, 2f)] private float sendInterval = 0.25f;
        [Tooltip("Metres a player may be beyond the carcass's reach (server positions) and still work on it.")]
        [SerializeField, Range(0f, 5f)] private float rangeTolerance = 1.5f;

        private HarvestableCreature creature;

        // Server
        private bool dirty;
        private float nextSend, nextWatch;
        private int transfer;
        private readonly Dictionary<int, float> nextBegin = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextStrip = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextAsk = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextRelease = new Dictionary<int, float>();
        private readonly Dictionary<int, NetPayloadAssembler> resultParts = new Dictionary<int, NetPayloadAssembler>();
        private readonly List<int> lockers = new List<int>();

        // Clients
        private readonly NetPayloadAssembler observerParts = new NetPayloadAssembler();
        private readonly NetPayloadAssembler targetParts = new NetPayloadAssembler();
        private bool openWhenReady;
        private int sentResults;
        private float deadSince = -1f, askAgainAt;

        private HarvestableCreature Creature => creature != null ? creature : (creature = GetComponent<HarvestableCreature>());

        public bool RunsRules => IsServerInitialized; // not "IsServer": Fish-Net's NetworkBehaviour already has one

        // ------------------------------------------------------------------ lifecycle
        public override void OnStartServer()
        {
            base.OnStartServer();
            Creature.Net = this;
            dirty = creature.IsCarcass;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (creature != null && ReferenceEquals(creature.Net, this))
                creature.Net = null;
            nextBegin.Clear();
            nextStrip.Clear();
            nextAsk.Clear();
            nextRelease.Clear();
            resultParts.Clear();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsServerInitialized)
                return; // the host runs the rules itself
            Creature.Net = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (IsServerInitialized || creature == null)
                return;
            HarvestWindow.Close(creature);
            if (ReferenceEquals(creature.Net, this))
                creature.Net = null;
            openWhenReady = false;
        }

        /// <summary>A client that starts watching the mob after it died gets its carcass.</summary>
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (connection != null && !connection.IsLocalClient && Creature.IsCarcass)
                SendText(CarcassData, creature.ExportCarcass(), connection);
        }

        // ------------------------------------------------------------------ server: the carcass for everyone
        void IHarvestNet.CarcassChanged()
        {
            if (IsServerInitialized)
                dirty = true;
        }

        private void LateUpdate()
        {
            if (!IsServerInitialized)
                return;
            float now = Time.unscaledTime;
            if (dirty && now >= nextSend)
            {
                dirty = false;
                nextSend = now + sendInterval;
                SendText(CarcassData, Creature.ExportCarcass(), null);
            }
            if (now >= nextWatch)
            {
                nextWatch = now + 1f;
                WatchWorkers();
            }
        }

        /// <summary>Sends text in parts: to everyone watching (<paramref name="to"/> null) or to one client.</summary>
        private void SendText(byte kind, string json, NetworkConnection to)
        {
            List<byte[]> parts = NetPayload.Split(NetPayload.Pack(json ?? ""));
            int id = ++transfer;
            for (int i = 0; i < parts.Count; i++)
            {
                if (to == null)
                    ObserversData(kind, id, (short)i, (short)parts.Count, parts[i]);
                else
                    TargetData(to, kind, id, (short)i, (short)parts.Count, parts[i]);
            }
        }

        /// <summary>Remote players working on the carcass who walked away, died or left: what they held is free again.</summary>
        private void WatchWorkers()
        {
            HarvestCarcass k = Creature.Carcass;
            if (k == null || k.locks.Count == 0)
                return;
            lockers.Clear();
            foreach (HarvestLock l in k.locks)
                if (!lockers.Contains(l.player))
                    lockers.Add(l.player);
            foreach (int key in lockers)
            {
                CombatEntity p = PlayerIds.Find(key);
                if (p != null && CardTable.IsLocalPlayer(p))
                    continue; // the host's own player: its window watches itself
                NetworkConnection conn = p != null ? NetObjects.OwnerOf(p) : null;
                if (p != null && conn != null && conn.IsActive && p.isActiveAndEnabled && p.IsAlive && InReach(p))
                    continue;
                creature.Release(key);
                if (conn != null && conn.IsActive && !conn.IsLocalClient)
                    TargetClose(conn);
            }
        }

        private bool InReach(CombatEntity player)
        {
            HarvestableCreature c = Creature;
            if (player == null || player.gameObject.scene != c.gameObject.scene)
                return false;
            Vector3 a = c.InteractionPoint.position, b = player.Position;
            a.y = b.y = 0f;
            float reach = c.InteractionRange * 1.6f + 0.5f + rangeTolerance;
            return (a - b).sqrMagnitude <= reach * reach;
        }

        // ------------------------------------------------------------------ server: to one player
        private NetworkConnection RemoteOf(CombatEntity player)
        {
            if (!IsServerInitialized || player == null)
                return null;
            NetworkConnection conn = NetObjects.OwnerOf(player);
            return conn != null && conn.IsActive && !conn.IsLocalClient ? conn : null;
        }

        bool IHarvestNet.Open(CombatEntity player)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn == null || !Creature.IsCarcass)
                return false;
            SendText(CarcassData, creature.ExportCarcass(), conn);
            TargetOpen(conn);
            return true;
        }

        void IHarvestNet.SendPlan(CombatEntity player, HarvestPlan plan)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn != null && plan != null)
                SendText(PlanData, JsonUtility.ToJson(plan), conn);
        }

        void IHarvestNet.SendReport(CombatEntity player, HarvestReport report)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn != null && report != null)
                SendText(ReportData, report.ToJson(), conn);
        }

        void IHarvestNet.SendSkills(CombatEntity player, string json)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn != null && !string.IsNullOrEmpty(json))
                SendText(SkillsData, json, conn);
        }

        void IHarvestNet.Tell(CombatEntity player, string text)
        {
            NetworkConnection conn = RemoteOf(player);
            if (conn != null && !string.IsNullOrEmpty(text))
                TargetMessage(conn, Clip(text));
        }

        private static string Clip(string text) => text.Length > MaxMessageLength ? text.Substring(0, MaxMessageLength) : text;

        // ------------------------------------------------------------------ server: requests
        /// <summary>
        /// The player behind a request, alive and by the carcass; null otherwise (<paramref name="player"/> is still the
        /// connection's player when there is one, and <paramref name="why"/> says what is wrong).
        /// </summary>
        private CombatEntity Worker(NetworkConnection conn, out CombatEntity player, out string why)
        {
            why = "";
            player = conn != null && conn.IsActive ? NetObjects.PlayerOf(conn) : null;
            if (player == null)
                return null;
            if (!player.IsAlive)
                why = "You cannot work on it now.";
            else if (!Creature.IsCarcass)
                why = "There is nothing to harvest.";
            else if (!InReach(player))
                why = "Too far away to work on it.";
            return why.Length == 0 ? player : null;
        }

        /// <summary>Tells a remote player their request was refused (their window is waiting for an answer).</summary>
        private void Refuse(CombatEntity player, string material, string why, string what = "the work")
        {
            HarvestMaterial m = Creature.Anatomy?.Material(material);
            ((IHarvestNet)this).SendReport(player, HarvestReport.Refused(material ?? "", m != null ? m.Name : what, why));
        }

        private static bool Allowed(Dictionary<int, float> next, NetworkConnection conn, float interval)
        {
            float now = Time.unscaledTime;
            if (next.TryGetValue(conn.ClientId, out float at) && now < at)
                return false;
            if (next.Count > 64)
                next.Clear(); // players come and go
            next[conn.ClientId] = now + interval;
            return true;
        }

        private static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= MaxIdLength;

        [ServerRpc(RequireOwnership = false)]
        private void ServerCarcass(NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || !Allowed(nextAsk, conn, 1f))
                return;
            SendText(CarcassData, Creature.ExportCarcass(), conn);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerBegin(string material, int[] units, NetworkConnection conn = null)
        {
            if (!ValidId(material) || units != null && units.Length > MaxUnits || conn == null || !Allowed(nextBegin, conn, 0.2f))
                return;
            if (Worker(conn, out CombatEntity player, out string why) == null)
            {
                if (player != null)
                    Refuse(player, material, why);
                return;
            }
            creature.BeginFromRemote(player, material, units != null ? new List<int>(units) : new List<int>());
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerCancel(string material, NetworkConnection conn = null)
        {
            if (!ValidId(material) || conn == null || !conn.IsActive)
                return;
            CombatEntity player = NetObjects.PlayerOf(conn);
            HarvestAnatomyData a = Creature.Anatomy;
            if (player != null && a != null)
                creature.Cancel(player, a.MaterialIndex(material));
        }

        [ServerRpc(RequireOwnership = false)]
        private void ServerStrip(NetworkConnection conn = null)
        {
            if (conn == null || !Allowed(nextStrip, conn, 1f))
                return;
            if (Worker(conn, out CombatEntity player, out string why) != null)
                creature.StripHide(player);
            else if (player != null)
                Refuse(player, "", why, "the hide");
        }

        /// <summary>The client's window closed: whatever its player was working on here is free again.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerRelease(NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || !Allowed(nextRelease, conn, 0.2f))
                return;
            CombatEntity player = NetObjects.PlayerOf(conn);
            if (player != null)
                Creature.CancelAll(player);
        }

        /// <summary>A part of a client's harvest result; the whole is checked against the work the server planned for them.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerResult(int id, short index, short count, byte[] part, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive || count < 1 || count > MaxResultParts)
                return;
            if (!resultParts.TryGetValue(conn.ClientId, out NetPayloadAssembler parts))
            {
                if (resultParts.Count > 64)
                    resultParts.Clear();
                resultParts[conn.ClientId] = parts = new NetPayloadAssembler();
            }
            if (!parts.Add(id, index, count, part, out byte[] whole))
                return;
            HarvestTaskResult result = null;
            if (NetPayload.TryUnpack(whole, out string json, MaxResultSize))
            {
                try { result = JsonUtility.FromJson<HarvestTaskResult>(json); }
                catch (Exception) { result = null; }
            }
            if (result == null || !ValidId(result.material) || result.stages == null || result.stages.Count > 512)
            {
                // Unreadable: the work is lost, and what the player held is free again.
                CombatEntity sender = NetObjects.PlayerOf(conn);
                if (sender != null)
                {
                    Creature.CancelAll(sender);
                    Refuse(sender, result != null && ValidId(result.material) ? result.material : "", "The work could not be read: it was lost.");
                }
                return;
            }
            if (Worker(conn, out CombatEntity player, out string why) == null)
            {
                // Gone too far (or dead): the work is lost, and what it held is free again.
                if (player != null)
                {
                    creature.Cancel(player, Creature.Anatomy != null ? Creature.Anatomy.MaterialIndex(result.material) : -1);
                    Refuse(player, result.material, why.Length > 0 ? why + " The work was lost." : "The work was lost.");
                }
                return;
            }
            creature.Submit(player, result);
        }

        // ------------------------------------------------------------------ clients: requests
        private bool IsRemoteClient => IsClientInitialized && !IsServerInitialized;

        void IHarvestNet.SendBegin(string material, List<int> units)
        {
            if (IsRemoteClient)
                ServerBegin(material, units != null ? units.ToArray() : Array.Empty<int>());
        }

        void IHarvestNet.SendCancel(string material)
        {
            if (IsRemoteClient)
                ServerCancel(material);
        }

        void IHarvestNet.SendStrip()
        {
            if (IsRemoteClient)
                ServerStrip();
        }

        void IHarvestNet.SendRelease()
        {
            if (IsRemoteClient)
                ServerRelease();
        }

        void IHarvestNet.SendSubmit(HarvestTaskResult result)
        {
            if (!IsRemoteClient || result == null)
                return;
            List<byte[]> parts = NetPayload.Split(NetPayload.Pack(JsonUtility.ToJson(result)));
            if (parts.Count > MaxResultParts)
            {
                ServerCancel(result.material);
                Creature.ReceiveReport(LocalPlayer(), HarvestReport.Refused(result.material, result.material, "The work was too large to send."));
                return;
            }
            int id = ++sentResults;
            for (int i = 0; i < parts.Count; i++)
                ServerResult(id, (short)i, (short)parts.Count, parts[i]);
        }

        // ------------------------------------------------------------------ clients: receiving
        private void Update()
        {
            // The mob died here but its carcass has not come (it normally follows the death at once): ask for it.
            if (!IsRemoteClient || creature == null || creature.IsCarcass || creature.Anatomy == null || creature.Entity == null || !creature.Entity.IsDead)
            {
                deadSince = -1f;
                return;
            }
            float now = Time.unscaledTime;
            if (deadSince < 0f)
                deadSince = now;
            else if (now - deadSince > 2f && now >= askAgainAt)
            {
                askAgainAt = now + 5f;
                ServerCarcass();
            }
        }

        [ObserversRpc(ExcludeServer = true)]
        private void ObserversData(byte kind, int id, short index, short count, byte[] part) => ReceivePart(observerParts, kind, id, index, count, part);

        [TargetRpc]
        private void TargetData(NetworkConnection conn, byte kind, int id, short index, short count, byte[] part) => ReceivePart(targetParts, kind, id, index, count, part);

        private void ReceivePart(NetPayloadAssembler parts, byte kind, int id, short index, short count, byte[] part)
        {
            if (IsServerInitialized || Creature == null)
                return;
            if (!parts.Add(id, index, count, part, out byte[] whole) || !NetPayload.TryUnpack(whole, out string json))
                return;
            switch (kind)
            {
                case CarcassData:
                    creature.ApplyReplicated(json);
                    if (openWhenReady && creature.IsCarcass)
                        OpenWindow();
                    break;
                case PlanData:
                {
                    HarvestPlan plan;
                    try { plan = JsonUtility.FromJson<HarvestPlan>(json); }
                    catch (Exception) { return; }
                    creature.ReceivePlan(LocalPlayer(), plan);
                    break;
                }
                case ReportData:
                    creature.ReceiveReport(LocalPlayer(), HarvestReport.FromJson(json));
                    break;
                case SkillsData:
                {
                    CombatEntity me = LocalPlayer();
                    if (me != null)
                        HarvestKnowledge.For(me)?.ReceiveFromServer(json);
                    break;
                }
            }
        }

        [TargetRpc]
        private void TargetOpen(NetworkConnection conn)
        {
            openWhenReady = true;
            if (creature != null && creature.IsCarcass)
                OpenWindow();
        }

        private void OpenWindow()
        {
            openWhenReady = false;
            CombatEntity me = LocalPlayer();
            if (me != null && creature != null && creature.IsCarcass)
                HarvestWindow.Show(creature, me);
        }

        [TargetRpc]
        private void TargetClose(NetworkConnection conn)
        {
            openWhenReady = false;
            if (creature != null)
                HarvestWindow.Close(creature);
        }

        [TargetRpc]
        private void TargetMessage(NetworkConnection conn, string text)
        {
            if (IsServerInitialized || string.IsNullOrEmpty(text) || text.Length > MaxMessageLength)
                return;
            CombatEntity me = LocalPlayer();
            if (me != null)
                InventoryManager.For(me)?.ShowMessage(text, InventoryFeedback.Kind.Info);
        }

        private static CombatEntity LocalPlayer()
        {
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && CardTable.IsLocalPlayer(players[i]))
                    return players[i];
            return null;
        }
    }
}
#endif
