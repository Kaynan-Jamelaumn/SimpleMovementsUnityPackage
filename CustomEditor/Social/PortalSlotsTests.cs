#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using NUnit.Framework;

/// <summary>Edit Mode tests for the free player slots of a dungeon portal (<see cref="PortalSlots"/>).</summary>
public class PortalSlotsTests
{
    [Test]
    public void SharedRunCountsThePlayersInside()
    {
        var s = new PortalSlots { known = true, open = true, maxPlayers = 5, shared = true, running = true, inside = 3 };
        Assert.AreEqual(2, s.Remaining);
        Assert.IsFalse(s.IsFull);
        s.inside = 5;
        Assert.AreEqual(0, s.Remaining);
        Assert.IsTrue(s.IsFull);
        s.inside = 7; // over the limit (players joined before a change): never negative
        Assert.AreEqual(0, s.Remaining);
    }

    [Test]
    public void SharedRunNotGoingHasEverySlotFree()
    {
        var s = new PortalSlots { open = true, maxPlayers = 4, shared = true, running = false, inside = 0 };
        Assert.AreEqual(4, s.Remaining);
    }

    [Test]
    public void PrivateRunsOfferTheGroupSize()
    {
        var s = new PortalSlots { open = true, maxPlayers = 3, shared = false, running = false };
        Assert.AreEqual(3, s.Remaining);
    }

    [Test]
    public void NoLimitAndClosed()
    {
        var unlimited = new PortalSlots { open = true, maxPlayers = 0, shared = true, running = true, inside = 12 };
        Assert.IsTrue(unlimited.Unlimited);
        Assert.AreEqual(-1, unlimited.Remaining);
        Assert.IsFalse(unlimited.IsFull);
        var closed = new PortalSlots { open = false, maxPlayers = 5 };
        Assert.AreEqual(0, closed.Remaining);
        Assert.IsFalse(closed.IsFull);
    }
}
#endif
