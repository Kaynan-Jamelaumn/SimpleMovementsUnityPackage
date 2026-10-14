using System;
using System.Collections.Generic;
using HubWorld;
using UnityEngine;

/// <summary>
/// Parties of players: invite (the target frame's menu, or <see cref="Invite"/>), accept or decline, leave, remove a
/// member, hand over the lead. Party members are allies: they cannot hurt each other (Combat Settings ▸ Party
/// Friendly Fire, off by default) but their heals and buffs reach each other, and a duel still lets two of them fight.
/// <para>Rules are in <see cref="PartyBook"/>; this is the scene side. It is created by itself when first needed
/// (or put one in a scene that stays loaded to set its Rules) and keeps itself across scene changes.</para>
/// <para>Network sessions: the server's party system is the authority. On a client the book is a copy of the server's
/// (<see cref="ApplyReplicated"/>, fed by the social relay), every player's party id is set from it, so every machine
/// computes the same combat relations, and requests go to the server through <see cref="ClientRequest"/>.</para>
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("SimpleMovements/Party System")]
public class PartySystem : MonoBehaviour
{
    /// <summary>What a network client asks the server to do.</summary>
    public enum PartyRequest : byte
    {
        Invite,
        Accept,
        Decline,
        Leave,
        Kick,
        Promote,
    }

    [Tooltip("Party size, invitation time, who may invite.")]
    [SerializeField] private PartyRules rules = new PartyRules();
    [Tooltip("Testing in single player: invitations sent to player objects that are not this machine's player are accepted at once.")]
    public bool autoAcceptForOtherPlayers = false;
    [Tooltip("Keep this object (and the parties) when scenes change.")]
    [SerializeField] private bool persistent = true;

    private static PartySystem instance;
    private static bool quitting;

    /// <summary>
    /// Network clients: sends a request to the server (request, the asking player, the other player for Invite / Kick /
    /// Promote). Installed by the networking layer; null offline and on the server.
    /// </summary>
    public static Action<PartyRequest, CombatEntity, CombatEntity> ClientRequest;

    /// <summary>The party system (created when missing, except while the application quits).</summary>
    public static PartySystem Instance
    {
        get
        {
            if (instance == null && !quitting && Application.isPlaying)
            {
#if UNITY_6000_4_OR_NEWER
                instance = FindAnyObjectByType<PartySystem>();
#else
                instance = FindAnyObjectByType<PartySystem>(FindObjectsInactive.Exclude);
#endif
                if (instance == null)
                    instance = new GameObject("Party System").AddComponent<PartySystem>();
            }
            return instance;
        }
    }

    /// <summary>The party system if one exists (never creates one).</summary>
    public static PartySystem Current => instance;

    /// <summary>Anything about the parties or invitations changed (on every machine).</summary>
    public event Action Changed;

    /// <summary>A player received an invitation (invited player, inviter). On clients: only for this machine's player.</summary>
    public event Action<CombatEntity, CombatEntity> InviteReceived;

    private readonly PartyBook book = new PartyBook();
    private readonly Dictionary<int, float> missingSince = new Dictionary<int, float>();
    private readonly List<int> scratch = new List<int>();
    private readonly HashSet<CombatEntity> assigned = new HashSet<CombatEntity>();
    private int appliedRevision = -1;
    private float nextApply, nextCleanup;
    private int lastLocalInviteFrom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
        ClientRequest = null;
    }

    /// <summary>A network client mirrors the server's parties.</summary>
    private static bool IsMirror => GameNet.NetAuthority.IsClientOnly;

    private double Now => Time.timeAsDouble;

    public PartyRules Rules => rules;
    public PartyBook Book => book;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
        book.Rules = rules;
        if (persistent && transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnValidate() => book.Rules = rules;

    // ------------------------------------------------------------------ queries

    /// <summary>The party of a player, or null.</summary>
    public Party PartyOf(CombatEntity player) => player != null ? book.Of(Id(player)) : null;

    /// <summary>Is <paramref name="player"/> the leader of their party?</summary>
    public bool IsLeader(CombatEntity player)
    {
        Party p = PartyOf(player);
        return p != null && p.Leader == Id(player);
    }

    /// <summary>The living members of <paramref name="player"/>'s party on this machine (results cleared first; the player included).</summary>
    public void MembersOf(CombatEntity player, List<CombatEntity> results)
    {
        results.Clear();
        Party p = PartyOf(player);
        if (p == null)
            return;
        foreach (int id in p.Members)
        {
            CombatEntity e = PlayerIds.Find(id);
            if (e != null)
                results.Add(e);
        }
    }

    /// <summary>The invitation waiting for <paramref name="player"/> and who sent it.</summary>
    public bool TryGetInvite(CombatEntity player, out CombatEntity from, out float secondsLeft)
    {
        from = null;
        secondsLeft = 0f;
        if (player == null || !book.TryGetInviteFor(Id(player), out PartyInvite invite))
            return false;
        from = PlayerIds.Find(invite.from);
        secondsLeft = Mathf.Max(0f, (float)(invite.expires - Now));
        return true;
    }

    /// <summary>A player's name as every machine shows it.</summary>
    public static string NameOf(CombatEntity e) => DuelSystem.DisplayName(e);

    public static string NameOf(int playerId)
    {
        CombatEntity e = PlayerIds.Find(playerId);
        return e != null ? NameOf(e) : "Someone";
    }

    // ------------------------------------------------------------------ requests

    /// <summary><paramref name="from"/> invites <paramref name="to"/>. False with a reason when they cannot.</summary>
    public bool Invite(CombatEntity from, CombatEntity to, out string reason)
    {
        reason = null;
        if (from == null || to == null || from == to || to.Kind != CombatEntity.EntityKind.Player)
        {
            reason = "You can only invite another player.";
            return false;
        }
        if (!book.CanInvite(Id(from), Id(to), out reason))
            return false;
        if (IsMirror)
            return Send(PartyRequest.Invite, from, to, out reason);
        if (!book.Invite(Id(from), Id(to), Now, out reason))
            return false;
        Message(from, $"You invited {NameOf(to)} to your party.");
        Message(to, $"{NameOf(from)} invites you to a party.");
        InviteReceived?.Invoke(to, from);
        if (autoAcceptForOtherPlayers && !GameNet.NetAuthority.IsNetworked && PlayerLocator.Local != null && to != PlayerLocator.Local)
            Accept(to);
        Changed?.Invoke();
        return true;
    }

    /// <summary><paramref name="player"/> accepts their invitation.</summary>
    public bool Accept(CombatEntity player)
    {
        if (player == null)
            return false;
        if (IsMirror)
            return Send(PartyRequest.Accept, player, null, out _);
        book.TryGetInviteFor(Id(player), out PartyInvite invite);
        Party party = book.Accept(Id(player), Now, out string reason);
        if (party == null)
        {
            Message(player, reason, HubMessageKind.Warning);
            Changed?.Invoke();
            return false;
        }
        Announce(party, $"{NameOf(player)} joined the party.", Id(player));
        Message(player, invite.from > 0 ? $"You joined {NameOf(invite.from)}'s party." : "You joined the party.");
        ApplyPartyIds();
        Changed?.Invoke();
        return true;
    }

    /// <summary><paramref name="player"/> declines their invitation.</summary>
    public bool Decline(CombatEntity player)
    {
        if (player == null)
            return false;
        if (IsMirror)
            return Send(PartyRequest.Decline, player, null, out _);
        int from = book.Decline(Id(player));
        if (from == 0)
            return false;
        Message(PlayerIds.Find(from), $"{NameOf(player)} declined your party invitation.");
        Changed?.Invoke();
        return true;
    }

    /// <summary><paramref name="player"/> leaves their party.</summary>
    public bool Leave(CombatEntity player)
    {
        if (player == null || PartyOf(player) == null)
            return false;
        if (IsMirror)
            return Send(PartyRequest.Leave, player, null, out _);
        Party party = book.Leave(Id(player));
        if (party == null)
            return false;
        Message(player, "You left the party.");
        Announce(party, $"{NameOf(player)} left the party.", 0);
        ApplyPartyIds();
        Changed?.Invoke();
        return true;
    }

    /// <summary>The leader <paramref name="leader"/> removes <paramref name="member"/>.</summary>
    public bool Kick(CombatEntity leader, CombatEntity member, out string reason)
    {
        reason = null;
        if (leader == null || member == null)
            return false;
        if (IsMirror)
            return Send(PartyRequest.Kick, leader, member, out reason);
        Party party = PartyOf(leader);
        if (!book.Kick(Id(leader), Id(member), out reason))
            return false;
        Message(member, "You were removed from the party.", HubMessageKind.Warning);
        Announce(party, $"{NameOf(member)} was removed from the party.", 0);
        ApplyPartyIds();
        Changed?.Invoke();
        return true;
    }

    /// <summary>The leader hands the lead to <paramref name="member"/>.</summary>
    public bool Promote(CombatEntity leader, CombatEntity member, out string reason)
    {
        reason = null;
        if (leader == null || member == null)
            return false;
        if (IsMirror)
            return Send(PartyRequest.Promote, leader, member, out reason);
        if (!book.Promote(Id(leader), Id(member), out reason))
            return false;
        Announce(PartyOf(member), $"{NameOf(member)} now leads the party.", 0);
        Changed?.Invoke();
        return true;
    }

    private bool Send(PartyRequest request, CombatEntity player, CombatEntity other, out string reason)
    {
        reason = null;
        if (ClientRequest == null)
        {
            reason = "Not connected to the server.";
            return false;
        }
        ClientRequest(request, player, other);
        return true;
    }

    // ------------------------------------------------------------------ network mirror

    /// <summary>One party as the server sends it.</summary>
    public struct PartySnapshot
    {
        public int id;
        public int leader;
        public int[] members;
    }

    /// <summary>The server's parties and invitations (each invitation's <c>expires</c> already in this machine's clock).</summary>
    public void Snapshot(List<PartySnapshot> partiesInto, List<PartyInvite> invitesInto)
    {
        partiesInto.Clear();
        invitesInto.Clear();
        foreach (Party p in book.Parties)
        {
            var m = new int[p.Count];
            for (int i = 0; i < m.Length; i++)
                m[i] = p.Members[i];
            partiesInto.Add(new PartySnapshot { id = p.Id, leader = p.Leader, members = m });
        }
        invitesInto.AddRange(book.Invites);
    }

    /// <summary>Network clients: replaces the parties and invitations with the server's (invitation times in this machine's clock).</summary>
    public void ApplyReplicated(IReadOnlyList<PartySnapshot> parties, IReadOnlyList<PartyInvite> invites)
    {
        var list = new List<(int, int, IReadOnlyList<int>)>(parties != null ? parties.Count : 0);
        if (parties != null)
            foreach (PartySnapshot p in parties)
                list.Add((p.id, p.leader, p.members ?? Array.Empty<int>()));
        book.Replace(list, invites);
        // A new invitation for this machine's player: raise the event (the prompt shows it).
        CombatEntity local = PlayerLocator.Local;
        if (local != null && book.TryGetInviteFor(Id(local), out PartyInvite invite))
        {
            if (invite.from != lastLocalInviteFrom)
            {
                lastLocalInviteFrom = invite.from;
                InviteReceived?.Invoke(local, PlayerIds.Find(invite.from));
            }
        }
        else
            lastLocalInviteFrom = 0;
        ApplyPartyIds();
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ every frame

    private void Update()
    {
        if (!IsMirror)
        {
            if (book.Expire(Now))
                Changed?.Invoke();
            if (Time.unscaledTime >= nextCleanup)
            {
                nextCleanup = Time.unscaledTime + 2f;
                ForgetMissingPlayers();
            }
        }
        // Players appear (join, travel back into view): their party ids follow the book.
        if (book.Revision != appliedRevision || Time.unscaledTime >= nextApply)
            ApplyPartyIds();
    }

    /// <summary>Sets every player's party id from the book (0 for players in no party).</summary>
    public void ApplyPartyIds()
    {
        appliedRevision = book.Revision;
        nextApply = Time.unscaledTime + 1f;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null)
                continue;
            Party party = book.Of(Id(p));
            int id = party != null ? party.Id : 0;
            // Offline, a party id this system never gave (CombatParties.Form from game code) is left alone.
            if (id == 0 && !GameNet.NetAuthority.IsNetworked && !assigned.Contains(p))
                continue;
            if (id != 0)
                assigned.Add(p);
            else
                assigned.Remove(p);
            if (p.PartyId != id)
                p.SetParty(id);
        }
    }

    /// <summary>Players that are gone (disconnected) for a few seconds leave their party and lose their invitations.</summary>
    private void ForgetMissingPlayers()
    {
        assigned.RemoveWhere(e => e == null);
        scratch.Clear();
        foreach (Party p in book.Parties)
            scratch.AddRange(p.Members);
        foreach (PartyInvite i in book.Invites)
        {
            scratch.Add(i.from);
            scratch.Add(i.to);
        }
        float now = Time.unscaledTime;
        bool changed = false;
        foreach (int id in scratch)
        {
            if (PlayerIds.Find(id) != null)
            {
                missingSince.Remove(id);
                continue;
            }
            if (!missingSince.TryGetValue(id, out float since))
            {
                missingSince[id] = now;
                continue;
            }
            if (now - since < 10f)
                continue;
            missingSince.Remove(id);
            Party party = book.Of(id);
            book.Forget(id);
            if (party != null)
                Announce(party, $"{NameOf(id)} left the party.", 0);
            changed = true;
        }
        if (changed)
        {
            ApplyPartyIds();
            Changed?.Invoke();
        }
    }

    // ------------------------------------------------------------------ helpers

    private static int Id(CombatEntity e) => PlayerIds.Of(e);

    private static void Message(CombatEntity to, string text, HubMessageKind kind = HubMessageKind.Info)
    {
        if (to != null && !string.IsNullOrEmpty(text))
            HubWorldMessages.Send(to, text, kind);
    }

    private void Announce(Party party, string text, int except)
    {
        if (party == null)
            return;
        foreach (int id in party.Members)
            if (id != except)
                Message(PlayerIds.Find(id), text);
    }
}
