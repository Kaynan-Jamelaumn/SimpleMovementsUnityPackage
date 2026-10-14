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
    /// Parties, guilds and inspection over the network. The server's <see cref="PartySystem"/>, the server's copies of
    /// every <see cref="PlayerSocial"/> and the server's <see cref="PlayerInspection"/> are the authority; this global relay
    /// sends their state to every client and carries the clients' requests to them. Put it on the "Network World" object
    /// of the Bootstrap scene, next to <see cref="NetDuels"/> (Tools ▸ SimpleMovements ▸ Network ▸ Add Missing Relays To
    /// Network World).
    /// </summary>
    /// <remarks>
    /// <para><b>Replicated (server → every client).</b> <see cref="PartyMembers"/> lists every member of every party
    /// (party id, leader, member; players by <see cref="PlayerIds"/>), <see cref="Invites"/> every waiting invitation
    /// (its expiry in network time) and <see cref="Socials"/> every player's guild, tag, alliance, fame, title, level and
    /// Allow Inspect. Only entries that changed are sent. Each client copies them into its own systems
    /// (<see cref="PartySystem.ApplyReplicated"/>, <see cref="PlayerSocial.ApplyReplicated"/>), so every machine knows each
    /// player's party id and guild and computes the same relations, colours, nameplates and highlights - while damage is
    /// still decided on the server only. A client never changes this state itself.</para>
    /// <para><b>Requests (client → server).</b> Invite, accept, decline, leave, remove from party, make leader
    /// (<see cref="PartySystem.ClientRequest"/>) and inspect (<see cref="PlayerInspection.ClientRequest"/>) are sent with
    /// the other player's id only: the asking player is always the connection's own player, never a value the client
    /// sent. They are rate-limited per client and run through the same rules as single player on the server. A refused
    /// party request comes back as a warning message; an inspection's answer is sent to the asking client only.</para>
    /// <para>Messages ("X joined the party") are sent by the server's party system through <see cref="HubWorldMessages"/>,
    /// which <see cref="NetHubWorld"/> delivers to the players concerned.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class NetSocial : NetworkBehaviour
    {
        /// <summary>One member of one party, as sent to the clients.</summary>
        public struct PartyMemberView : IEquatable<PartyMemberView>
        {
            public int party;
            public int leader;
            public int member;

            public bool Equals(PartyMemberView o) => party == o.party && leader == o.leader && member == o.member;
            public override bool Equals(object obj) => obj is PartyMemberView o && Equals(o);
            public override int GetHashCode() => unchecked((party * 397 ^ leader) * 397 ^ member);
        }

        /// <summary>One waiting party invitation, as sent to the clients.</summary>
        public struct InviteView : IEquatable<InviteView>
        {
            public int from;
            public int to;
            public int party;
            /// <summary>Network time (server tick time, seconds) when it runs out.</summary>
            public double expires;

            public bool Equals(InviteView o) => from == o.from && to == o.to && party == o.party && expires.Equals(o.expires);
            public override bool Equals(object obj) => obj is InviteView o && Equals(o);
            public override int GetHashCode() => unchecked((from * 397 ^ to) * 397 ^ party);
        }

        /// <summary>One player's social data, as sent to the clients.</summary>
        public struct SocialView : IEquatable<SocialView>
        {
            public int player;
            public string guild;
            public string tag;
            public string alliance;
            public int fame;
            public string title;
            public bool allowInspect;
            public int level;

            public bool Equals(SocialView o) =>
                player == o.player && guild == o.guild && tag == o.tag && alliance == o.alliance && fame == o.fame &&
                title == o.title && allowInspect == o.allowInspect && level == o.level;

            public override bool Equals(object obj) => obj is SocialView o && Equals(o);
            public override int GetHashCode() => unchecked(player * 397 ^ fame * 31 ^ level);
        }

        /// <summary>An inspection's answer, flattened to strings and numbers for the wire.</summary>
        public struct InspectMessage
        {
            public int playerId;
            public string error;
            public string name;
            public string title;
            public string guild;
            public string guildTag;
            public string alliance;
            public int fame;
            public int level;
            public string race;
            public string className;
            public float health;
            public float maxHealth;
            public string[] statLabels;
            public string[] statValues;
            public string[] itemSlots;
            public string[] itemIds;
            public string[] itemNames;
        }

        /// <summary>The relay of this session (null outside one).</summary>
        public static NetSocial Instance { get; private set; }

        /// <summary>Every member of every party on the server (every client observes this object).</summary>
        public readonly SyncList<PartyMemberView> PartyMembers = new SyncList<PartyMemberView>(new SyncTypeSettings(0f));
        /// <summary>Every waiting party invitation on the server.</summary>
        public readonly SyncList<InviteView> Invites = new SyncList<InviteView>(new SyncTypeSettings(0f));
        /// <summary>Every player's guild, alliance, fame, title, level and Allow Inspect.</summary>
        public readonly SyncList<SocialView> Socials = new SyncList<SocialView>(new SyncTypeSettings(0.1f));

        [Tooltip("Seconds between two party requests of the same client (extra requests are dropped).")]
        [SerializeField, Min(0.05f)] private float requestInterval = 0.25f;
        [Tooltip("Seconds between two inspect requests of the same client.")]
        [SerializeField, Min(0.1f)] private float inspectInterval = 0.5f;
        [Tooltip("Seconds between two checks of the players' social data on the server (only changes are sent).")]
        [SerializeField, Min(0.1f)] private float socialInterval = 0.5f;

        // Server.
        private int publishedRevision = -1;
        private PartySystem boundParties;
        private float nextSocialPublish;
        private readonly Dictionary<(int from, int to, double expires), double> sentExpiries = new Dictionary<(int, int, double), double>();
        private readonly Dictionary<int, float> nextRequest = new Dictionary<int, float>();
        private readonly Dictionary<int, float> nextInspect = new Dictionary<int, float>();
        private readonly List<PartySystem.PartySnapshot> partySnapshots = new List<PartySystem.PartySnapshot>();
        private readonly List<PartyInvite> inviteSnapshots = new List<PartyInvite>();
        private readonly List<PartyMemberView> memberViews = new List<PartyMemberView>();
        private readonly List<InviteView> inviteViews = new List<InviteView>();
        private readonly List<SocialView> socialViews = new List<SocialView>();

        // Clients.
        private bool partiesDirty = true, socialsDirty = true;
        private float nextClientRefresh;
        private PartySystem appliedParties;
        private readonly List<PartyInvite> clientInvites = new List<PartyInvite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private double Now => NetHubWorld.NetworkTime(TimeManager);

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[Net] Two NetSocial relays ('{Instance.name}', '{name}') - keep one on the Network World object.", this);
            Instance = this;
            PartyMembers.OnChange += OnPartyMembersChanged;
            Invites.OnChange += OnInvitesChanged;
            Socials.OnChange += OnSocialsChanged;
        }

        private void OnDestroy()
        {
            PartyMembers.OnChange -= OnPartyMembersChanged;
            Invites.OnChange -= OnInvitesChanged;
            Socials.OnChange -= OnSocialsChanged;
            UninstallHooks();
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            publishedRevision = -1;
            nextSocialPublish = 0f;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            nextRequest.Clear();
            nextInspect.Clear();
            sentExpiries.Clear();
            boundParties = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            partiesDirty = socialsDirty = true;
            appliedParties = null;
            if (!IsServerInitialized)
            {
                PartySystem.ClientRequest = SendPartyRequest;
                PlayerInspection.ClientRequest = SendInspectRequest;
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            UninstallHooks();
        }

        private void UninstallHooks()
        {
            if (PartySystem.ClientRequest == (Action<PartySystem.PartyRequest, CombatEntity, CombatEntity>)SendPartyRequest)
                PartySystem.ClientRequest = null;
            if (PlayerInspection.ClientRequest == (Action<int>)SendInspectRequest)
                PlayerInspection.ClientRequest = null;
        }

        private void OnPartyMembersChanged(SyncListOperation op, int index, PartyMemberView previous, PartyMemberView next, bool asServer)
        {
            if (!asServer)
                partiesDirty = true;
        }

        private void OnInvitesChanged(SyncListOperation op, int index, InviteView previous, InviteView next, bool asServer)
        {
            if (!asServer)
                partiesDirty = true;
        }

        private void OnSocialsChanged(SyncListOperation op, int index, SocialView previous, SocialView next, bool asServer)
        {
            if (!asServer)
                socialsDirty = true;
        }

        private void Update()
        {
            if (IsServerInitialized)
                ServerUpdate();
            else if (IsClientInitialized)
                ClientUpdate();
        }

        // ------------------------------------------------------------------ server: publishing

        private void ServerUpdate()
        {
            PartySystem parties = PartySystem.Instance;
            if (parties != null)
            {
                if (parties != boundParties)
                {
                    boundParties = parties;
                    publishedRevision = -1;
                }
                if (parties.Book.Revision != publishedRevision)
                {
                    publishedRevision = parties.Book.Revision;
                    PublishParties(parties);
                }
            }
            if (Time.unscaledTime >= nextSocialPublish)
            {
                nextSocialPublish = Time.unscaledTime + socialInterval;
                PublishSocials();
            }
        }

        /// <summary>Writes the parties and invitations into the lists (only changed entries are sent).</summary>
        private void PublishParties(PartySystem parties)
        {
            parties.Snapshot(partySnapshots, inviteSnapshots);
            memberViews.Clear();
            foreach (PartySystem.PartySnapshot p in partySnapshots)
                if (p.members != null)
                    foreach (int m in p.members)
                        memberViews.Add(new PartyMemberView { party = p.id, leader = p.leader, member = m });

            // Expiries go out in network time, each converted once, so an unchanged invitation is never sent again.
            double localNow = Time.timeAsDouble, netNow = Now;
            var live = new HashSet<(int, int, double)>();
            inviteViews.Clear();
            foreach (PartyInvite i in inviteSnapshots)
            {
                var key = (i.from, i.to, i.expires);
                live.Add(key);
                if (!sentExpiries.TryGetValue(key, out double expires))
                {
                    expires = netNow + (i.expires - localNow);
                    sentExpiries[key] = expires;
                }
                inviteViews.Add(new InviteView { from = i.from, to = i.to, party = i.party, expires = expires });
            }
            var gone = new List<(int, int, double)>();
            foreach (var key in sentExpiries.Keys)
                if (!live.Contains(key))
                    gone.Add(key);
            foreach (var key in gone)
                sentExpiries.Remove(key);

            Write(PartyMembers, memberViews);
            Write(Invites, inviteViews);
        }

        /// <summary>Writes every player's social data into the list (only changed entries are sent).</summary>
        private void PublishSocials()
        {
            socialViews.Clear();
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
            {
                CombatEntity p = players[i];
                if (p == null)
                    continue;
                int id = PlayerIds.Of(p);
                if (id <= 0 || id >= PlayerIds.LocalIdBase)
                    continue; // not a networked player: no client could name it
                PlayerSocial s = PlayerSocial.For(p);
                if (s == null)
                    continue;
                socialViews.Add(new SocialView
                {
                    player = id,
                    guild = s.GuildName,
                    tag = s.GuildTag,
                    alliance = s.AllianceName,
                    fame = s.Fame,
                    title = s.Title,
                    allowInspect = s.AllowInspect,
                    level = s.Level,
                });
            }
            socialViews.Sort((a, b) => a.player.CompareTo(b.player)); // a stable order: a player joining moves few entries
            Write(Socials, socialViews);
        }

        /// <summary>Makes <paramref name="list"/> equal to <paramref name="views"/>, writing only the entries that differ.</summary>
        private static void Write<T>(SyncList<T> list, List<T> views) where T : IEquatable<T>
        {
            for (int i = 0; i < views.Count; i++)
            {
                if (i < list.Count)
                {
                    if (!list[i].Equals(views[i]))
                        list[i] = views[i];
                }
                else
                    list.Add(views[i]);
            }
            while (list.Count > views.Count)
                list.RemoveAt(list.Count - 1);
        }

        // ------------------------------------------------------------------ server: requests

        /// <summary>A client's party request: the asking player is the caller's own player, never a value it sent.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerPartyRequest(PartySystem.PartyRequest request, int otherId, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive)
                return;
            float now = Time.unscaledTime;
            if (nextRequest.TryGetValue(conn.ClientId, out float allowedAt) && now < allowedAt)
                return;
            nextRequest[conn.ClientId] = now + requestInterval;

            PartySystem parties = PartySystem.Instance;
            CombatEntity me = NetObjects.PlayerOf(conn);
            if (parties == null || me == null)
                return;
            CombatEntity other = PlayerIds.Find(otherId);
            string reason = null;
            switch (request)
            {
                case PartySystem.PartyRequest.Invite:
                    if (other == null || other == me)
                        return;
                    parties.Invite(me, other, out reason);
                    break;
                case PartySystem.PartyRequest.Accept:
                    parties.Accept(me); // a refusal is messaged by the party system itself
                    return;
                case PartySystem.PartyRequest.Decline:
                    parties.Decline(me);
                    return;
                case PartySystem.PartyRequest.Leave:
                    parties.Leave(me);
                    return;
                case PartySystem.PartyRequest.Kick:
                    if (other == null)
                        return;
                    parties.Kick(me, other, out reason);
                    break;
                case PartySystem.PartyRequest.Promote:
                    if (other == null)
                        return;
                    parties.Promote(me, other, out reason);
                    break;
                default:
                    return;
            }
            if (!string.IsNullOrEmpty(reason))
                HubWorldMessages.Send(me, reason, HubMessageKind.Warning);
        }

        /// <summary>A client inspects a player: the server builds the answer from its own copies and sends it to that client only.</summary>
        [ServerRpc(RequireOwnership = false)]
        private void ServerInspect(int targetId, NetworkConnection conn = null)
        {
            if (conn == null || !conn.IsActive)
                return;
            float now = Time.unscaledTime;
            if (nextInspect.TryGetValue(conn.ClientId, out float allowedAt) && now < allowedAt)
            {
                TargetInspect(conn, ToMessage(InspectData.Failed(targetId, "Please wait a moment.")));
                return;
            }
            nextInspect[conn.ClientId] = now + inspectInterval;

            CombatEntity me = NetObjects.PlayerOf(conn);
            CombatEntity target = PlayerIds.Find(targetId);
            InspectData data = me == null ? InspectData.Failed(targetId, "You are not in the game.")
                : target == null ? InspectData.Failed(targetId, "That player is not here.")
                : PlayerInspection.Build(me, target);
            TargetInspect(conn, ToMessage(data));
        }

        [TargetRpc]
        private void TargetInspect(NetworkConnection conn, InspectMessage message) => PlayerInspection.Deliver(FromMessage(message));

        // ------------------------------------------------------------------ clients

        private void ClientUpdate()
        {
            // Also once a second: players who only now appeared here get their party id and guild.
            bool refresh = Time.unscaledTime >= nextClientRefresh;
            if (refresh)
                nextClientRefresh = Time.unscaledTime + 1f;

            PartySystem parties = PartySystem.Instance;
            if (parties != null && parties != appliedParties)
            {
                appliedParties = parties;
                partiesDirty = true;
            }
            if (parties != null && partiesDirty)
            {
                partiesDirty = false;
                ApplyParties(parties);
            }
            if (socialsDirty || (refresh && Socials.Count > 0))
            {
                socialsDirty = false;
                ApplySocials();
            }
        }

        private void ApplyParties(PartySystem parties)
        {
            List<PartySystem.PartySnapshot> snapshots = GroupParties(PartyMembers);
            double localNow = Time.timeAsDouble, netNow = Now;
            clientInvites.Clear();
            for (int i = 0; i < Invites.Count; i++)
            {
                InviteView v = Invites[i];
                clientInvites.Add(new PartyInvite { from = v.from, to = v.to, party = v.party, expires = localNow + (v.expires - netNow) });
            }
            parties.ApplyReplicated(snapshots, clientInvites);
        }

        private void ApplySocials()
        {
            for (int i = 0; i < Socials.Count; i++)
            {
                SocialView v = Socials[i];
                CombatEntity e = PlayerIds.Find(v.player);
                PlayerSocial s = e != null ? PlayerSocial.For(e) : null;
                if (s != null)
                    s.ApplyReplicated(v.guild, v.tag, v.alliance, v.fame, v.title, v.allowInspect, v.level);
            }
        }

        /// <summary>The parties behind a list of members (members in list order, parties in order of first appearance).</summary>
        public static List<PartySystem.PartySnapshot> GroupParties(IReadOnlyList<PartyMemberView> members)
        {
            var order = new List<int>();
            var byParty = new Dictionary<int, (int leader, List<int> members)>();
            for (int i = 0; i < members.Count; i++)
            {
                PartyMemberView m = members[i];
                if (m.party <= 0 || m.member <= 0)
                    continue;
                if (!byParty.TryGetValue(m.party, out var p))
                {
                    p = (m.leader, new List<int>());
                    byParty[m.party] = p;
                    order.Add(m.party);
                }
                if (!p.members.Contains(m.member))
                    p.members.Add(m.member);
            }
            var result = new List<PartySystem.PartySnapshot>(order.Count);
            foreach (int id in order)
            {
                var p = byParty[id];
                result.Add(new PartySystem.PartySnapshot { id = id, leader = p.leader, members = p.members.ToArray() });
            }
            return result;
        }

        /// <summary><see cref="PartySystem.ClientRequest"/>: the other player by its network-stable id.</summary>
        private void SendPartyRequest(PartySystem.PartyRequest request, CombatEntity player, CombatEntity other)
        {
            if (IsClientInitialized)
                ServerPartyRequest(request, other != null ? PlayerIds.Of(other) : 0);
        }

        /// <summary><see cref="PlayerInspection.ClientRequest"/>.</summary>
        private void SendInspectRequest(int targetId)
        {
            if (IsClientInitialized)
                ServerInspect(targetId);
            else
                PlayerInspection.Deliver(InspectData.Failed(targetId, "Not connected to the server."));
        }

        // ------------------------------------------------------------------ inspection on the wire

        public static InspectMessage ToMessage(InspectData d)
        {
            InspectStat[] stats = d.stats ?? Array.Empty<InspectStat>();
            InspectItem[] items = d.equipment ?? Array.Empty<InspectItem>();
            var m = new InspectMessage
            {
                playerId = d.playerId,
                error = d.error ?? "",
                name = d.name ?? "",
                title = d.title ?? "",
                guild = d.guild ?? "",
                guildTag = d.guildTag ?? "",
                alliance = d.alliance ?? "",
                fame = d.fame,
                level = d.level,
                race = d.race ?? "",
                className = d.className ?? "",
                health = d.health,
                maxHealth = d.maxHealth,
                statLabels = new string[stats.Length],
                statValues = new string[stats.Length],
                itemSlots = new string[items.Length],
                itemIds = new string[items.Length],
                itemNames = new string[items.Length],
            };
            for (int i = 0; i < stats.Length; i++)
            {
                m.statLabels[i] = stats[i].label ?? "";
                m.statValues[i] = stats[i].value ?? "";
            }
            for (int i = 0; i < items.Length; i++)
            {
                m.itemSlots[i] = items[i].slot ?? "";
                m.itemIds[i] = items[i].itemId ?? "";
                m.itemNames[i] = items[i].itemName ?? "";
            }
            return m;
        }

        public static InspectData FromMessage(InspectMessage m)
        {
            int statCount = Math.Min(m.statLabels != null ? m.statLabels.Length : 0, m.statValues != null ? m.statValues.Length : 0);
            int itemCount = Math.Min(m.itemSlots != null ? m.itemSlots.Length : 0,
                Math.Min(m.itemIds != null ? m.itemIds.Length : 0, m.itemNames != null ? m.itemNames.Length : 0));
            var d = new InspectData
            {
                playerId = m.playerId,
                error = m.error ?? "",
                name = m.name ?? "",
                title = m.title ?? "",
                guild = m.guild ?? "",
                guildTag = m.guildTag ?? "",
                alliance = m.alliance ?? "",
                fame = m.fame,
                level = m.level,
                race = m.race ?? "",
                className = m.className ?? "",
                health = Finite(m.health),
                maxHealth = Finite(m.maxHealth),
                stats = new InspectStat[statCount],
                equipment = new InspectItem[itemCount],
            };
            for (int i = 0; i < statCount; i++)
                d.stats[i] = new InspectStat { label = m.statLabels[i] ?? "", value = m.statValues[i] ?? "" };
            for (int i = 0; i < itemCount; i++)
                d.equipment[i] = new InspectItem { slot = m.itemSlots[i] ?? "", itemId = m.itemIds[i] ?? "", itemName = m.itemNames[i] ?? "" };
            return d;
        }

        private static float Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : Mathf.Max(0f, v);
    }
}
#endif
