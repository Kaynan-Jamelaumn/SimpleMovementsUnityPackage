using System;
using System.Collections.Generic;

/// <summary>Limits of parties.</summary>
[Serializable]
public class PartyRules
{
    [UnityEngine.Tooltip("Most players in one party (2-40).")]
    [UnityEngine.Range(2, 40)] public int maxMembers = 5;
    [UnityEngine.Tooltip("Seconds an invitation waits for an answer.")]
    [UnityEngine.Min(5f)] public float inviteSeconds = 30f;
    [UnityEngine.Tooltip("Only the leader may invite (off = any member may).")]
    public bool onlyLeaderInvites = false;
    [UnityEngine.Tooltip("A party of one (everyone else left) is disbanded.")]
    public bool disbandWhenAlone = true;
}

/// <summary>One party: its id, leader and members (player ids, see HubWorld.PlayerIds).</summary>
public sealed class Party
{
    public int Id { get; internal set; }
    public int Leader { get; internal set; }
    internal readonly List<int> members = new List<int>();
    public IReadOnlyList<int> Members => members;
    public int Count => members.Count;
    public bool Has(int player) => members.Contains(player);
}

/// <summary>An invitation to join a party (or to start one with the inviter).</summary>
public struct PartyInvite : IEquatable<PartyInvite>
{
    public int from;
    public int to;
    /// <summary>The inviter's party (0 = they have none yet: one is formed on accept).</summary>
    public int party;
    /// <summary>When it runs out (the book's clock).</summary>
    public double expires;

    public bool Equals(PartyInvite o) => from == o.from && to == o.to && party == o.party && expires.Equals(o.expires);
    public override bool Equals(object obj) => obj is PartyInvite o && Equals(o);
    public override int GetHashCode() => unchecked((from * 397) ^ to);
}

/// <summary>
/// The party rules, without Unity: invitations, accepting, leaving, kicking, the leader. Every change bumps
/// <see cref="Revision"/>. Players are plain ids; the <see cref="PartySystem"/> maps them to characters and applies
/// the party id to each member's Combat Entity (which is what the combat relations read).
/// </summary>
public sealed class PartyBook
{
    private readonly List<Party> parties = new List<Party>();
    private readonly List<PartyInvite> invites = new List<PartyInvite>();
    private int nextId = 1;

    public PartyRules Rules { get; set; } = new PartyRules();
    public IReadOnlyList<Party> Parties => parties;
    public IReadOnlyList<PartyInvite> Invites => invites;
    /// <summary>Raised by every change.</summary>
    public int Revision { get; private set; }

    /// <summary>The party of a player, or null.</summary>
    public Party Of(int player)
    {
        if (player <= 0)
            return null;
        foreach (Party p in parties)
            if (p.members.Contains(player))
                return p;
        return null;
    }

    public Party Find(int partyId)
    {
        foreach (Party p in parties)
            if (p.Id == partyId)
                return p;
        return null;
    }

    /// <summary>The invitation waiting for <paramref name="player"/> (the newest), if any.</summary>
    public bool TryGetInviteFor(int player, out PartyInvite invite)
    {
        for (int i = invites.Count - 1; i >= 0; i--)
        {
            if (invites[i].to == player)
            {
                invite = invites[i];
                return true;
            }
        }
        invite = default;
        return false;
    }

    /// <summary>Can <paramref name="from"/> invite <paramref name="to"/>? (Reason when not.)</summary>
    public bool CanInvite(int from, int to, out string reason)
    {
        reason = null;
        if (from <= 0 || to <= 0 || from == to)
        {
            reason = "You can only invite another player.";
            return false;
        }
        Party mine = Of(from);
        if (mine != null && Rules.onlyLeaderInvites && mine.Leader != from)
        {
            reason = "Only the party leader can invite.";
            return false;
        }
        if (mine != null && mine.Count >= Rules.maxMembers)
        {
            reason = "Your party is full.";
            return false;
        }
        Party theirs = Of(to);
        if (theirs != null)
        {
            reason = theirs == mine ? "They are already in your party." : "They are already in a party.";
            return false;
        }
        foreach (PartyInvite i in invites)
        {
            if (i.from == from && i.to == to)
            {
                reason = "You already invited them.";
                return false;
            }
        }
        return true;
    }

    /// <summary><paramref name="from"/> invites <paramref name="to"/>. False with a reason when they cannot.</summary>
    public bool Invite(int from, int to, double now, out string reason)
    {
        if (!CanInvite(from, to, out reason))
            return false;
        Party mine = Of(from);
        invites.RemoveAll(i => i.to == to); // one waiting invitation per player: the newest
        invites.Add(new PartyInvite { from = from, to = to, party = mine != null ? mine.Id : 0, expires = now + Math.Max(5f, Rules.inviteSeconds) });
        Revision++;
        return true;
    }

    /// <summary><paramref name="player"/> accepts the invitation waiting for them. Returns the party joined, or null.</summary>
    public Party Accept(int player, double now, out string reason)
    {
        reason = null;
        Expire(now);
        if (!TryGetInviteFor(player, out PartyInvite invite))
        {
            reason = "No party invitation.";
            return null;
        }
        invites.Remove(invite);
        Revision++;
        if (Of(player) != null)
        {
            reason = "You are already in a party.";
            return null;
        }
        Party party = Of(invite.from);
        if (party == null)
        {
            if (invite.party != 0)
            {
                reason = "That party no longer exists.";
                return null;
            }
            // The inviter had no party: one is formed, led by them.
            party = new Party { Id = nextId++, Leader = invite.from };
            party.members.Add(invite.from);
            parties.Add(party);
            // Their other invitations now invite into this party.
            for (int i = 0; i < invites.Count; i++)
                if (invites[i].from == invite.from)
                {
                    PartyInvite x = invites[i];
                    x.party = party.Id;
                    invites[i] = x;
                }
        }
        else if (invite.party != 0 && party.Id != invite.party)
        {
            reason = "They are in another party now.";
            return null;
        }
        if (party.Count >= Rules.maxMembers)
        {
            reason = "That party is full.";
            return null;
        }
        party.members.Add(player);
        invites.RemoveAll(i => i.to == player);
        return party;
    }

    /// <summary><paramref name="player"/> declines the invitation waiting for them. Returns who invited them (0 = none).</summary>
    public int Decline(int player)
    {
        if (!TryGetInviteFor(player, out PartyInvite invite))
            return 0;
        invites.Remove(invite);
        Revision++;
        return invite.from;
    }

    /// <summary><paramref name="player"/> leaves their party (a new leader is chosen). Returns the party left, or null.</summary>
    public Party Leave(int player)
    {
        Party party = Of(player);
        if (party == null)
            return null;
        Remove(party, player);
        return party;
    }

    /// <summary>The leader removes <paramref name="member"/>. False with a reason when they cannot.</summary>
    public bool Kick(int leader, int member, out string reason)
    {
        reason = null;
        Party party = Of(leader);
        if (party == null || party.Leader != leader)
        {
            reason = "Only the party leader can remove members.";
            return false;
        }
        if (member == leader || !party.Has(member))
        {
            reason = "They are not in your party.";
            return false;
        }
        Remove(party, member);
        return true;
    }

    /// <summary>The leader hands the lead to <paramref name="member"/>.</summary>
    public bool Promote(int leader, int member, out string reason)
    {
        reason = null;
        Party party = Of(leader);
        if (party == null || party.Leader != leader)
        {
            reason = "Only the party leader can do that.";
            return false;
        }
        if (!party.Has(member) || member == leader)
        {
            reason = "They are not in your party.";
            return false;
        }
        party.Leader = member;
        Revision++;
        return true;
    }

    /// <summary>Removes a player from everything (they disconnected): their party and every invitation from or to them.</summary>
    public void Forget(int player)
    {
        int removed = invites.RemoveAll(i => i.from == player || i.to == player);
        Party party = Of(player);
        if (party != null)
            Remove(party, player);
        else if (removed > 0)
            Revision++;
    }

    /// <summary>Removes invitations that ran out. True when any did.</summary>
    public bool Expire(double now)
    {
        int n = invites.RemoveAll(i => i.expires <= now);
        if (n > 0)
            Revision++;
        return n > 0;
    }

    /// <summary>Replaces everything with a copy (network clients mirror the server's book).</summary>
    public void Replace(IEnumerable<(int id, int leader, IReadOnlyList<int> members)> newParties, IEnumerable<PartyInvite> newInvites)
    {
        parties.Clear();
        invites.Clear();
        if (newParties != null)
        {
            foreach (var p in newParties)
            {
                var party = new Party { Id = p.id, Leader = p.leader };
                if (p.members != null)
                    party.members.AddRange(p.members);
                parties.Add(party);
                nextId = Math.Max(nextId, p.id + 1);
            }
        }
        if (newInvites != null)
            invites.AddRange(newInvites);
        Revision++;
    }

    private void Remove(Party party, int player)
    {
        party.members.Remove(player);
        invites.RemoveAll(i => i.from == player && i.party == party.Id);
        if (party.Count == 0 || (party.Count == 1 && Rules.disbandWhenAlone))
        {
            parties.Remove(party);
            invites.RemoveAll(i => i.party == party.Id);
        }
        else if (party.Leader == player)
        {
            party.Leader = party.members[0];
        }
        Revision++;
    }
}
