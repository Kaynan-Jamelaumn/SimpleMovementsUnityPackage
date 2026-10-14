using System;
using System.Collections.Generic;

namespace GameNet
{
    /// <summary>
    /// A trade as one of its players sees it (sent by the server to that player only): who the partner is, both offers,
    /// locks and confirmations, the revision to confirm, and time left on a request.
    /// </summary>
    [Serializable]
    public struct TradeView
    {
        public int tradeId;
        /// <summary>The <see cref="TradeState"/> as a byte (<see cref="TradeState.None"/> = no trade: close the window).</summary>
        public byte state;
        /// <summary>A request this player received (and may accept), not one they made.</summary>
        public bool incoming;
        public int partnerId;
        public string partnerName;
        public List<TradeItemOffer> myItems;
        public List<TradeItemOffer> theirItems;
        public int myCurrency;
        public int theirCurrency;
        public bool myLocked;
        public bool theirLocked;
        public bool myConfirmed;
        public bool theirConfirmed;
        public int revision;
        /// <summary>Seconds before a request expires (0 for an open trade).</summary>
        public float secondsLeft;
        /// <summary>Why the trade ended or what just happened ("The trade was completed.").</summary>
        public string message;

        /// <summary>The state as an enum.</summary>
        public TradeState State => (TradeState)state;

        /// <summary>No trade (the window closes), with an optional <paramref name="message"/>.</summary>
        public static TradeView Closed(string message, TradeState state = TradeState.None) => new TradeView
        {
            state = (byte)state,
            myItems = new List<TradeItemOffer>(),
            theirItems = new List<TradeItemOffer>(),
            partnerName = "",
            message = message ?? "",
        };

        /// <summary>
        /// <paramref name="trade"/> as <paramref name="viewerId"/> sees it; <paramref name="nameOf"/> names the partner.
        /// </summary>
        public static TradeView For(Trade trade, int viewerId, Func<int, string> nameOf, double now, TradeRules rules, string message = "")
        {
            TradeSide mine = trade?.SideOf(viewerId);
            TradeSide theirs = trade?.OtherSide(viewerId);
            if (mine == null || theirs == null)
                return Closed(message);
            return new TradeView
            {
                tradeId = trade.Id,
                state = (byte)trade.State,
                incoming = trade.State == TradeState.Proposed && trade.Partner.PlayerId == viewerId,
                partnerId = theirs.PlayerId,
                partnerName = nameOf != null ? nameOf(theirs.PlayerId) ?? "" : "",
                myItems = new List<TradeItemOffer>(mine.Items),
                theirItems = new List<TradeItemOffer>(theirs.Items),
                myCurrency = mine.Currency,
                theirCurrency = theirs.Currency,
                myLocked = mine.Locked,
                theirLocked = theirs.Locked,
                myConfirmed = mine.Confirmed,
                theirConfirmed = theirs.Confirmed,
                revision = trade.Revision,
                secondsLeft = trade.State == TradeState.Proposed && rules != null
                    ? (float)Math.Max(0.0, rules.proposalSeconds - (now - trade.StartedAt))
                    : 0f,
                message = message ?? "",
            };
        }
    }
}
