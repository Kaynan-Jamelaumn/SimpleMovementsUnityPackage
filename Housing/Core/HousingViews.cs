using System;
using System.Collections.Generic;

// What the housing windows show, as plain data (built where the rules run, sent as JSON to a network client). Times are
// lease days from now ("in"), with the real seconds a lease day lasts, so a client never needs the server's clock. Money
// is whole currency; rents per day are in thousandths. Nothing here names another player by key.

/// <summary>A message for the player.</summary>
[Serializable]
public class NoticeView
{
    public double ago;
    public string text = "";
}

/// <summary>Something held for the player.</summary>
[Serializable]
public class HeldView
{
    public long id;
    public string source = "";
    public int stacks;
    public int items;
    public long fee;
    public double heldFor;
    /// <summary>Lease days it stays free to claim (-1: claims are always free, or the free days are over), and until it is
    /// thrown away (-1: it is kept until claimed).</summary>
    public double freeFor = -1.0;
    public double discardIn = -1.0;
    public List<string> names = new List<string>();
}

/// <summary>Someone the player let into a place (named by name and fingerprint, never by key).</summary>
[Serializable]
public class GrantView
{
    public string name = "";
    public string fingerprint = "";
    public int access;
    public bool byMe;
}

/// <summary>A player near the player (one they may let in): their session id, name and fingerprint.</summary>
[Serializable]
public class NearbyView
{
    public int playerId;
    public string name = "";
    public string fingerprint = "";
}

/// <summary>A place the player may use (theirs or one they were let into).</summary>
[Serializable]
public class PlaceAccessView
{
    public string placeId = "";
    public string roomId = "";
    public string name = "";
    public int access;
    public bool tenant;
    /// <summary>With the Manage right (not the tenant): those this player let in there, whom they may change or let out.</summary>
    public List<GrantView> grants = new List<GrantView>();
}

/// <summary>The player's own account and messages, in every housing window.</summary>
[Serializable]
public class AccountView
{
    public long balance;
    public long reserved;
    public long owed;
    public long cap;
    public int wallet;
    public string currency = "";
    public List<NoticeView> notices = new List<NoticeView>();
    public List<HeldView> held = new List<HeldView>();
    public List<PlaceAccessView> places = new List<PlaceAccessView>();
}

// ------------------------------------------------------------------ inns
[Serializable]
public class InnRoomView
{
    public string id = "";
    public string name = "";
    public float quality = 1f;
    public bool free;
    public long nightly;
}

[Serializable]
public class InnView
{
    /// <summary>Why the housing office cannot answer (empty when it is open): nothing else in the view is real then.</summary>
    public string closed = "";
    public string innId = "";
    public string name = "";
    public string district = "";
    public int baseNightly;
    /// <summary>Its price level now (1 = its own nightly price) and how full it has been lately (0-1).</summary>
    public double level = 1.0;
    public double occupancy;
    public int maxNights = 14;
    public double nextFreeIn = -1.0;
    public List<InnRoomView> rooms = new List<InnRoomView>();
    /// <summary>The player's room here (empty id: none).</summary>
    public string myRoomId = "";
    public string myRoomName = "";
    public double myEndsIn;
    public long extendNightly;
    /// <summary>How many more nights the player's stay may take now (0: none), and why not when it may take none.</summary>
    public int maxMoreNights;
    public string extendReason = "";
    public List<GrantView> myGrants = new List<GrantView>();
    public List<NearbyView> nearby = new List<NearbyView>();
    public double secondsPerDay = 86400.0;
    public bool gameTime;
    public AccountView account = new AccountView();
}

// ------------------------------------------------------------------ the realtor
/// <summary>A house in the realtor's list.</summary>
[Serializable]
public class ListingView
{
    public string id = "";
    public string name = "";
    public string description = "";
    public string district = "";
    public float houseArea;
    public float houseFootprint;
    public int floors = 1;
    public float landArea;
    public float gardenArea;
    public string zones = "";
    public bool weekly = true;
    public bool monthly = true;
    public long askingMilli;
    public long baseMilli;
    /// <summary>The least offer it takes (a free house, or one coming free: what it asks; a house that stays let: its base rent, or its asking when lower). 0: as it asks (an older server).</summary>
    public long minOfferMilli;
    /// <summary>Let and its renewal fixed: the least offer that takes it from its tenant at its lease end (public). 0: not known.</summary>
    public long takeAtMilli;
    public bool vacant;
    public bool mine;
    /// <summary>Let, but its tenant does not renew: it comes free when its term ends (<see cref="endsIn"/>) and is offered a day later.</summary>
    public bool comesFree;
    /// <summary>Named in the player's order but let by another realtor (not one of this realtor's houses).</summary>
    public bool elsewhere;
    public string tenant = "";
    public double endsIn;
    public double firstClearingIn;
    /// <summary>The player's offer on it (0: none), its term (<see cref="LeaseTerm"/>) and rank in their order (1 = favourite).</summary>
    public long myOfferMilli;
    public int myTerm;
    public int myRank;
    /// <summary>Metres and compass direction from the realtor ("120 m north-east").</summary>
    public float distance;
    public string direction = "";
    /// <summary>Its last prices paid (per day, thousandths; newest first) and how long ago.</summary>
    public List<long> salesMilli = new List<long>();
    public List<double> salesAgo = new List<double>();
}

/// <summary>A lease the player holds.</summary>
[Serializable]
public class LeaseView
{
    public string propertyId = "";
    public string name = "";
    public double endsIn;
    public int term;
    public long rateMilli;
    public bool autoRenew;
    public float protection;
    public bool noticed;
    public long renewRateMilli;
    public long renewTotal;
    public long reserved;
    public bool movingOut;
    public List<GrantView> grants = new List<GrantView>();
}

[Serializable]
public class RealtorView
{
    /// <summary>The player's own housing, seen from anywhere (no realtor): its listings are only their houses and their order's.</summary>
    public bool own;
    /// <summary>Why the housing office cannot answer (empty when it is open): nothing else in the view is real then.</summary>
    public string closed = "";
    public string realtorName = "";
    public List<ListingView> listings = new List<ListingView>();
    public List<LeaseView> leases = new List<LeaseView>();
    /// <summary>The player's order: its items in order (property, term, offer per day), its deposit.</summary>
    public List<string> orderHouses = new List<string>();
    public List<int> orderTerms = new List<int>();
    public List<long> orderOffersMilli = new List<long>();
    public long orderEscrow;
    /// <summary>A change of the order that takes something back, waiting for the order notice: in how many lease days it takes
    /// effect (-1: none) and the order it will be (until then the order above stands, and may still win).</summary>
    public double orderPendingIn = -1.0;
    public List<string> orderPendingHouses = new List<string>();
    public List<int> orderPendingTerms = new List<int>();
    public List<long> orderPendingOffersMilli = new List<long>();
    /// <summary>The order is being withdrawn: in how many lease days (-1: it is not). It stands, and may still win, until then.</summary>
    public double orderCancelIn = -1.0;
    /// <summary>Lease days a change that takes something back (or withdrawing the order) waits: the order notice.</summary>
    public double orderNotice;
    /// <summary>
    /// The house the order is held for (its id; empty: none). A tenant's rent was fixed while the order's offer stood behind
    /// it, so until that house is offered the order wins no other house, and at that clearing it is tried for it first.
    /// </summary>
    public string orderHeldFor = "";
    /// <summary>Lease days until that house is offered (its lease end; its first clearing if it comes free); -1: not held.</summary>
    public double orderHeldIn = -1.0;
    public double nextClearingIn;
    /// <summary>Lease days between two clearings of the market.</summary>
    public double clearingEvery = 0.25;
    public int weekDays = 7;
    public int monthDays = 28;
    public int maxOrderItems = 8;
    public float defaultProtection = 2f;
    public float maxProtection = 5f;
    public float protectionMargin = 0.1f;
    public float protectionPremium = 0.05f;
    public float confirmAboveAsking = 5f;
    public string mayOrder = "";
    /// <summary>Each district's usual price level: the median of what its houses ask against their base rents (1 = as their sizes say).</summary>
    public List<string> districts = new List<string>();
    public List<double> districtLevels = new List<double>();
    public List<NearbyView> nearby = new List<NearbyView>();
    public double secondsPerDay = 86400.0;
    public bool gameTime;
    public AccountView account = new AccountView();
}

/// <summary>What a player asks the housing office (offline: done at once; on a client: sent to the server).</summary>
public enum HousingRequestKind : byte
{
    View = 0,
    RentRoom = 1,
    ExtendStay = 2,
    LeaveRoom = 3,
    PlaceOrder = 10,
    CancelOrder = 11,
    SetRenewal = 12,
    Deposit = 13,
    Withdraw = 14,
    Claim = 20,
    Grant = 21,
    Revoke = 22,
    ReadNotices = 23,
}

/// <summary>A request (sent as JSON on the network; checked again by the server).</summary>
[Serializable]
public class HousingRequest
{
    public byte kind;
    /// <summary>The window's request number (its answer carries it back).</summary>
    public int seq;
    /// <summary>A unique id per request: a request sent twice (a retry) is done once.</summary>
    public string requestId = "";
    public string placeId = "";
    public string roomId = "";
    public int nights;
    public long amount;
    /// <summary>The price the player saw (refused if it changed).</summary>
    public long expected;
    public bool flag;
    public float protection;
    public int access;
    public int playerId;
    public string fingerprint = "";
    public long bundleId;
    public List<string> houses = new List<string>();
    public List<int> terms = new List<int>();
    public List<long> offersMilli = new List<long>();
    /// <summary>The window shows the player's own housing (wherever they stand): the views that come back are those, not the desk's.</summary>
    public bool own;
}

/// <summary>The answer to a request.</summary>
[Serializable]
public class HousingReply
{
    public int seq;
    public bool ok;
    public string message = "";

    public static HousingReply Fail(string why) => new HousingReply { ok = false, message = why ?? "That cannot be done." };
    public static HousingReply Ok(string what) => new HousingReply { ok = true, message = what ?? "" };
}
