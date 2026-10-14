using System;
using System.Collections.Generic;

namespace GameNet
{
    /// <summary>
    /// One stack offered in a player-to-player trade: the slot it is in (the bag or the hotbar of the offering player's
    /// inventory, as an <see cref="InventorySlotRef"/>), the item expected there (its <see cref="ItemDatabase"/> id - the
    /// trade is refused if something else is there when it happens) and how many units of it.
    /// </summary>
    [Serializable]
    public struct TradeItemOffer : IEquatable<TradeItemOffer>
    {
        /// <summary>The <see cref="InventoryArea"/> (bag or hotbar only), as a byte.</summary>
        public byte area;
        /// <summary>Index of the slot in its area.</summary>
        public short index;
        /// <summary>The item's <see cref="ItemDatabase"/> id.</summary>
        public string itemId;
        /// <summary>Units offered (at most the stack's size).</summary>
        public int quantity;

        public TradeItemOffer(InventorySlotRef slot, string itemId, int quantity)
        {
            area = slot.area;
            index = slot.index;
            this.itemId = itemId;
            this.quantity = quantity;
        }

        /// <summary>The slot as an <see cref="InventorySlotRef"/>.</summary>
        public InventorySlotRef Slot => new InventorySlotRef { area = area, index = index };

        /// <summary>A bag or hotbar slot, an item id and at least one unit.</summary>
        public bool IsWellFormed =>
            (area == (byte)InventoryArea.Bag || area == (byte)InventoryArea.Hotbar) && index >= 0 && index < InventoryNetRules.MaxSlotsPerArea
            && !string.IsNullOrEmpty(itemId) && itemId.Length <= TradeRules.MaxItemIdLength && quantity > 0;

        public bool Equals(TradeItemOffer other) => area == other.area && index == other.index && itemId == other.itemId && quantity == other.quantity;
        public override bool Equals(object obj) => obj is TradeItemOffer o && Equals(o);
        public override int GetHashCode() => ((area << 16) ^ index) * 397 ^ quantity ^ (itemId != null ? itemId.GetHashCode() : 0);
    }

    /// <summary>Where a trade stands.</summary>
    public enum TradeState : byte
    {
        /// <summary>No trade.</summary>
        None = 0,
        /// <summary>Asked: the partner has not answered yet.</summary>
        Proposed = 1,
        /// <summary>Both are in the trade window, changing offers, locking and confirming.</summary>
        Open = 2,
        /// <summary>The items and money were exchanged.</summary>
        Completed = 3,
        /// <summary>Declined, cancelled, timed out, too far apart, a player left.</summary>
        Cancelled = 4,
    }

    /// <summary>Limits of player trading (the server's settings).</summary>
    [Serializable]
    public sealed class TradeRules
    {
        /// <summary>Longest item id accepted in an offer.</summary>
        public const int MaxItemIdLength = 64;

        /// <summary>Most stacks one side can offer.</summary>
        public int maxItemsPerSide = 12;
        /// <summary>Seconds a request waits for an answer.</summary>
        public float proposalSeconds = 30f;
        /// <summary>Seconds an open trade may go without any change before it is cancelled.</summary>
        public float idleSeconds = 180f;
        /// <summary>Furthest apart the two players may be (metres; further cancels the trade).</summary>
        public float range = 8f;
    }

    /// <summary>One side of a trade: the player, what they offer, and whether they locked and confirmed it.</summary>
    public sealed class TradeSide
    {
        internal readonly List<TradeItemOffer> items = new List<TradeItemOffer>();

        public TradeSide(int playerId) => PlayerId = playerId;

        /// <summary>The player (<c>HubWorld.PlayerIds</c>).</summary>
        public int PlayerId { get; }
        /// <summary>The stacks offered.</summary>
        public IReadOnlyList<TradeItemOffer> Items => items;
        /// <summary>Money offered (in the trade's currency).</summary>
        public int Currency { get; internal set; }
        /// <summary>The player locked their offer (it cannot change without unlocking both sides).</summary>
        public bool Locked { get; internal set; }
        /// <summary>The player confirmed the exchange (only possible while both sides are locked).</summary>
        public bool Confirmed { get; internal set; }
    }

    /// <summary>A trade between two players.</summary>
    public sealed class Trade
    {
        internal Trade(int id, int proposer, int partner, double now)
        {
            Id = id;
            Proposer = new TradeSide(proposer);
            Partner = new TradeSide(partner);
            State = TradeState.Proposed;
            StartedAt = now;
            LastChange = now;
        }

        public int Id { get; }
        /// <summary>The player who asked.</summary>
        public TradeSide Proposer { get; }
        /// <summary>The player who was asked.</summary>
        public TradeSide Partner { get; }
        public TradeState State { get; internal set; }
        /// <summary>When it was asked (seconds, the book's clock).</summary>
        public double StartedAt { get; }
        /// <summary>When it last changed (accepted, an offer, a lock or a confirmation).</summary>
        public double LastChange { get; internal set; }
        /// <summary>Goes up with every change of an offer; a confirmation names the revision the player saw.</summary>
        public int Revision { get; internal set; }
        /// <summary>Why it was cancelled (empty otherwise).</summary>
        public string EndReason { get; internal set; } = "";

        /// <summary>Does <paramref name="playerId"/> take part?</summary>
        public bool Involves(int playerId) => Proposer.PlayerId == playerId || Partner.PlayerId == playerId;
        /// <summary>The side of <paramref name="playerId"/> (null when not in it).</summary>
        public TradeSide SideOf(int playerId) => Proposer.PlayerId == playerId ? Proposer : Partner.PlayerId == playerId ? Partner : null;
        /// <summary>The other side of <paramref name="playerId"/> (null when not in it).</summary>
        public TradeSide OtherSide(int playerId) => Proposer.PlayerId == playerId ? Partner : Partner.PlayerId == playerId ? Proposer : null;
        /// <summary>Both sides locked their offers.</summary>
        public bool BothLocked => Proposer.Locked && Partner.Locked;
        /// <summary>Both sides confirmed: the exchange can be made.</summary>
        public bool BothConfirmed => Proposer.Confirmed && Partner.Confirmed;
        /// <summary>Nothing offered by either side.</summary>
        public bool IsEmpty => Proposer.items.Count == 0 && Partner.items.Count == 0 && Proposer.Currency == 0 && Partner.Currency == 0;
    }

    /// <summary>
    /// The rules of player-to-player trading, without inventories or networking (pure, unit tested): one trade per
    /// player; a request the other player accepts or declines; offers of item stacks (by inventory slot) and money;
    /// both sides lock their offer, then both confirm; any change of an offer unlocks both sides; either side cancels;
    /// requests and idle trades time out; players too far apart are separated. The server's <c>NetTrading</c> checks
    /// offers against the real inventories and performs the exchange with <see cref="TradeExchange"/>.
    /// </summary>
    public sealed class TradeBook
    {
        private readonly List<Trade> trades = new List<Trade>();
        private int nextId = 1;

        public TradeBook(TradeRules rules = null) => Rules = rules ?? new TradeRules();

        public TradeRules Rules { get; }
        /// <summary>The trades in progress (requests and open trades).</summary>
        public IReadOnlyList<Trade> Trades => trades;

        /// <summary>The trade <paramref name="playerId"/> is in (asked, asking or open), or null.</summary>
        public Trade TradeOf(int playerId)
        {
            for (int i = 0; i < trades.Count; i++)
                if (trades[i].Involves(playerId))
                    return trades[i];
            return null;
        }

        /// <summary>
        /// <paramref name="from"/> asks <paramref name="to"/> to trade. When <paramref name="to"/> already asked
        /// <paramref name="from"/>, this accepts that request instead. Returns the trade, or null with a reason.
        /// </summary>
        public Trade Propose(int from, int to, double now, out string reason)
        {
            reason = "";
            if (from <= 0 || to <= 0)
            {
                reason = "No such player.";
                return null;
            }
            if (from == to)
            {
                reason = "You cannot trade with yourself.";
                return null;
            }
            Trade theirs = TradeOf(to);
            if (theirs != null && theirs.State == TradeState.Proposed && theirs.Proposer.PlayerId == to && theirs.Partner.PlayerId == from)
                return Accept(from, now, out reason);
            if (TradeOf(from) != null)
            {
                reason = "You are already trading.";
                return null;
            }
            if (theirs != null)
            {
                reason = "They are busy trading with someone else.";
                return null;
            }
            var trade = new Trade(nextId++, from, to, now);
            trades.Add(trade);
            return trade;
        }

        /// <summary><paramref name="playerId"/> accepts the request they received. Returns the open trade, or null with a reason.</summary>
        public Trade Accept(int playerId, double now, out string reason)
        {
            reason = "";
            Trade t = TradeOf(playerId);
            if (t == null || t.State != TradeState.Proposed || t.Partner.PlayerId != playerId)
            {
                reason = "No trade request to accept.";
                return null;
            }
            t.State = TradeState.Open;
            t.LastChange = now;
            return t;
        }

        /// <summary><paramref name="playerId"/> declines a request or cancels a trade. Returns the ended trade, or null.</summary>
        public Trade Cancel(int playerId, string reason)
        {
            Trade t = TradeOf(playerId);
            if (t != null)
                End(t, TradeState.Cancelled, reason);
            return t;
        }

        /// <summary>
        /// <paramref name="playerId"/> changes their offer to <paramref name="items"/> and <paramref name="currency"/>.
        /// A real change unlocks both sides and clears both confirmations (the other player must look again). An offer
        /// equal to the current one changes nothing. False with a reason when refused.
        /// </summary>
        public bool SetOffer(int playerId, IList<TradeItemOffer> items, int currency, double now, out string reason)
        {
            reason = "";
            Trade t = TradeOf(playerId);
            if (t == null || t.State != TradeState.Open)
            {
                reason = "You are not trading.";
                return false;
            }
            if (!IsValidOffer(items, currency, Rules.maxItemsPerSide, out reason))
                return false;
            TradeSide side = t.SideOf(playerId);
            if (side.Currency == currency && SameItems(side.items, items))
                return true;
            side.items.Clear();
            if (items != null)
                side.items.AddRange(items);
            side.Currency = currency;
            Unlock(t, now);
            t.Revision++;
            return true;
        }

        /// <summary>
        /// <paramref name="playerId"/> locks (or unlocks) their offer. Unlocking clears both confirmations. False with a
        /// reason when they are not in an open trade.
        /// </summary>
        public bool SetLocked(int playerId, bool locked, double now, out string reason)
        {
            reason = "";
            Trade t = TradeOf(playerId);
            if (t == null || t.State != TradeState.Open)
            {
                reason = "You are not trading.";
                return false;
            }
            TradeSide side = t.SideOf(playerId);
            if (side.Locked == locked)
                return true;
            side.Locked = locked;
            if (!locked)
            {
                t.Proposer.Confirmed = false;
                t.Partner.Confirmed = false;
            }
            t.LastChange = now;
            return true;
        }

        /// <summary>
        /// <paramref name="playerId"/> confirms the exchange as of <paramref name="revision"/> (the offers they saw).
        /// Needs both sides locked and the current revision. <paramref name="ready"/> = both confirmed: perform the
        /// exchange, then call <see cref="Complete"/> (or <see cref="Unlock"/> when it failed).
        /// </summary>
        public bool Confirm(int playerId, int revision, double now, out bool ready, out string reason)
        {
            ready = false;
            reason = "";
            Trade t = TradeOf(playerId);
            if (t == null || t.State != TradeState.Open)
            {
                reason = "You are not trading.";
                return false;
            }
            if (!t.BothLocked)
            {
                reason = "Both offers must be locked first.";
                return false;
            }
            if (revision != t.Revision)
            {
                reason = "The offers changed - look again.";
                return false;
            }
            if (t.IsEmpty)
            {
                reason = "Nothing is offered.";
                return false;
            }
            t.SideOf(playerId).Confirmed = true;
            t.LastChange = now;
            ready = t.BothConfirmed;
            return true;
        }

        /// <summary>Something changed (an offer, a player's inventory): both sides unlock and must confirm again.</summary>
        public void Unlock(Trade t, double now)
        {
            if (t == null)
                return;
            t.Proposer.Locked = t.Partner.Locked = false;
            t.Proposer.Confirmed = t.Partner.Confirmed = false;
            t.LastChange = now;
        }

        /// <summary>The exchange was made: the trade ends.</summary>
        public void Complete(Trade t) => End(t, TradeState.Completed, "");

        /// <summary>
        /// Ends requests nobody answered, idle trades and trades whose players cannot stay in them:
        /// <paramref name="whyApart"/>(a, b) returns why (too far apart, a player left or died) or null when they can.
        /// The ended trades are added to <paramref name="ended"/>.
        /// </summary>
        public void Expire(double now, Func<int, int, string> whyApart, List<Trade> ended)
        {
            for (int i = trades.Count - 1; i >= 0; i--)
            {
                if (i >= trades.Count)
                    continue;
                Trade t = trades[i];
                string why = null;
                if (t.State == TradeState.Proposed && now - t.StartedAt > Rules.proposalSeconds)
                    why = "The trade request expired.";
                else if (t.State == TradeState.Open && now - t.LastChange > Rules.idleSeconds)
                    why = "The trade timed out.";
                else if (whyApart != null)
                    why = whyApart(t.Proposer.PlayerId, t.Partner.PlayerId);
                if (why == null)
                    continue;
                End(t, TradeState.Cancelled, why);
                ended?.Add(t);
            }
        }

        /// <summary>Is an offer acceptable (count, well-formed stacks, no slot twice, money not negative)?</summary>
        public static bool IsValidOffer(IList<TradeItemOffer> items, int currency, int maxItems, out string reason)
        {
            reason = "";
            if (currency < 0)
            {
                reason = "Invalid amount.";
                return false;
            }
            int n = items != null ? items.Count : 0;
            if (n > maxItems)
            {
                reason = $"At most {maxItems} stacks can be offered.";
                return false;
            }
            for (int i = 0; i < n; i++)
            {
                if (!items[i].IsWellFormed)
                {
                    reason = "Invalid offer.";
                    return false;
                }
                for (int k = 0; k < i; k++)
                    if (items[k].area == items[i].area && items[k].index == items[i].index)
                    {
                        reason = "The same stack is offered twice.";
                        return false;
                    }
            }
            return true;
        }

        private static bool SameItems(List<TradeItemOffer> a, IList<TradeItemOffer> b)
        {
            int nb = b != null ? b.Count : 0;
            if (a.Count != nb)
                return false;
            for (int i = 0; i < nb; i++)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }

        private void End(Trade t, TradeState state, string reason)
        {
            t.State = state;
            t.EndReason = reason ?? "";
            trades.Remove(t);
        }
    }
}
