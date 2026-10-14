#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using NUnit.Framework;

/// <summary>Edit Mode tests for the party rules (<see cref="PartyBook"/>).</summary>
public class PartyBookTests
{
    private static PartyBook Book(int max = 5) => new PartyBook { Rules = new PartyRules { maxMembers = max, inviteSeconds = 30f } };

    [Test]
    public void InviteAndAcceptFormsAPartyLedByTheInviter()
    {
        PartyBook b = Book();
        Assert.IsTrue(b.Invite(1, 2, 0d, out _));
        Party p = b.Accept(2, 1d, out string reason);
        Assert.IsNotNull(p, reason);
        Assert.AreEqual(1, p.Leader);
        Assert.IsTrue(p.Has(1) && p.Has(2));
        Assert.AreSame(p, b.Of(1));
        Assert.AreEqual(0, b.Invites.Count);
    }

    [Test]
    public void CannotInviteSelfOrSomeoneInAParty()
    {
        PartyBook b = Book();
        Assert.IsFalse(b.Invite(1, 1, 0d, out _));
        b.Invite(1, 2, 0d, out _);
        b.Accept(2, 0d, out _);
        Assert.IsFalse(b.Invite(3, 2, 0d, out string reason));
        StringAssert.Contains("already", reason);
        Assert.IsFalse(b.Invite(1, 2, 0d, out _), "already in your party");
    }

    [Test]
    public void InvitationsExpire()
    {
        PartyBook b = Book();
        b.Invite(1, 2, 0d, out _);
        Assert.IsTrue(b.Expire(31d));
        Assert.IsNull(b.Accept(2, 31d, out _));
    }

    [Test]
    public void FullPartiesRefuseNewMembers()
    {
        PartyBook b = Book(2);
        b.Invite(1, 2, 0d, out _);
        b.Accept(2, 0d, out _);
        Assert.IsFalse(b.Invite(1, 3, 0d, out string reason));
        StringAssert.Contains("full", reason);
    }

    [Test]
    public void LeaderLeavingPassesTheLeadAndLastMemberDisbands()
    {
        PartyBook b = Book();
        b.Invite(1, 2, 0d, out _);
        b.Accept(2, 0d, out _);
        b.Invite(1, 3, 0d, out _);
        b.Accept(3, 0d, out _);
        Party p = b.Of(1);
        b.Leave(1);
        Assert.AreEqual(2, p.Leader);
        b.Leave(2);
        Assert.IsNull(b.Of(3), "a party of one is disbanded");
        Assert.AreEqual(0, b.Parties.Count);
    }

    [Test]
    public void OnlyTheLeaderKicksAndPromotes()
    {
        PartyBook b = Book();
        b.Invite(1, 2, 0d, out _);
        b.Accept(2, 0d, out _);
        b.Invite(1, 3, 0d, out _);
        b.Accept(3, 0d, out _);
        Assert.IsFalse(b.Kick(2, 3, out _));
        Assert.IsTrue(b.Kick(1, 3, out _));
        Assert.IsNull(b.Of(3));
        Assert.IsTrue(b.Promote(1, 2, out _));
        Assert.AreEqual(2, b.Of(1).Leader);
    }

    [Test]
    public void ForgettingADisconnectedPlayer()
    {
        PartyBook b = Book();
        b.Invite(1, 2, 0d, out _);
        b.Accept(2, 0d, out _);
        b.Invite(1, 3, 0d, out _);
        b.Forget(1);
        Assert.IsNull(b.Of(2), "two-player party disbanded");
        Assert.AreEqual(0, b.Invites.Count);
    }

    [Test]
    public void ReplaceMirrorsTheServer()
    {
        PartyBook b = Book();
        b.Replace(new[] { (7, 5, (System.Collections.Generic.IReadOnlyList<int>)new[] { 5, 6 }) },
                  new[] { new PartyInvite { from = 5, to = 9, party = 7, expires = 10d } });
        Assert.AreEqual(7, b.Of(6).Id);
        Assert.IsTrue(b.TryGetInviteFor(9, out PartyInvite inv));
        Assert.AreEqual(5, inv.from);
    }
}
#endif
