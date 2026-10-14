#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the economy's network support: the trading rules (<see cref="TradeBook"/>) and the view each
    /// player receives, the paths that name interactables inside networked objects, and the merchant stock a server sends
    /// to a client.
    /// </summary>
    public class EconomyNetTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                if (o != null)
                    Object.DestroyImmediate(o);
            created.Clear();
        }

        private static TradeItemOffer Offer(InventoryArea area, int index, string id = "sword", int quantity = 1) =>
            new TradeItemOffer(new InventorySlotRef(area, index), id, quantity);

        private static TradeBook OpenTrade(out Trade trade, TradeRules rules = null)
        {
            var book = new TradeBook(rules);
            trade = book.Propose(1, 2, 0d, out _);
            book.Accept(2, 1d, out _);
            return book;
        }

        // ------------------------------------------------------------------ requests
        [Test]
        public void Propose_ThenAccept_OpensTheTrade()
        {
            var book = new TradeBook();
            Trade t = book.Propose(1, 2, 0d, out string reason);
            Assert.IsNotNull(t, reason);
            Assert.AreEqual(TradeState.Proposed, t.State);
            Assert.AreSame(t, book.TradeOf(2));

            Assert.IsNull(book.Accept(1, 1d, out _), "only the asked player accepts");
            Assert.AreSame(t, book.Accept(2, 1d, out _));
            Assert.AreEqual(TradeState.Open, t.State);
        }

        [Test]
        public void Propose_RefusesSelfBusyAndInvalidPlayers()
        {
            var book = new TradeBook();
            Assert.IsNull(book.Propose(1, 1, 0d, out _));
            Assert.IsNull(book.Propose(0, 2, 0d, out _));
            Assert.IsNotNull(book.Propose(1, 2, 0d, out _));
            Assert.IsNull(book.Propose(1, 3, 0d, out _), "already trading");
            Assert.IsNull(book.Propose(3, 2, 0d, out string reason), "the other player is busy");
            StringAssert.Contains("busy", reason);
        }

        [Test]
        public void Propose_BackToTheAsker_AcceptsTheirRequest()
        {
            var book = new TradeBook();
            Trade t = book.Propose(1, 2, 0d, out _);
            Assert.AreSame(t, book.Propose(2, 1, 1d, out _));
            Assert.AreEqual(TradeState.Open, t.State);
            Assert.AreEqual(1, book.Trades.Count);
        }

        [Test]
        public void Cancel_EndsTheTradeForBoth()
        {
            TradeBook book = OpenTrade(out Trade t);
            Assert.AreSame(t, book.Cancel(2, "cancelled"));
            Assert.AreEqual(TradeState.Cancelled, t.State);
            Assert.IsNull(book.TradeOf(1));
            Assert.IsNull(book.TradeOf(2));
            Assert.IsNull(book.Cancel(2, "again"));
        }

        // ------------------------------------------------------------------ offers, locks, confirmations
        [Test]
        public void SetOffer_ChangesUnlockBothSidesAndBumpTheRevision()
        {
            TradeBook book = OpenTrade(out Trade t);
            Assert.IsTrue(book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 3) }, 10, 2d, out _));
            int revision = t.Revision;
            Assert.IsTrue(book.SetLocked(1, true, 3d, out _));
            Assert.IsTrue(book.SetLocked(2, true, 3d, out _));
            Assert.IsTrue(t.BothLocked);

            // The same offer again changes nothing.
            Assert.IsTrue(book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 3) }, 10, 4d, out _));
            Assert.IsTrue(t.BothLocked);
            Assert.AreEqual(revision, t.Revision);

            // A different one (even by the other side) unlocks both.
            Assert.IsTrue(book.SetOffer(2, new[] { Offer(InventoryArea.Hotbar, 0, "potion", 4) }, 0, 5d, out _));
            Assert.IsFalse(t.Proposer.Locked);
            Assert.IsFalse(t.Partner.Locked);
            Assert.AreEqual(revision + 1, t.Revision);
            Assert.AreEqual(1, t.Partner.Items.Count);
        }

        [Test]
        public void SetOffer_RefusedWhenNotOpenOrInvalid()
        {
            var book = new TradeBook(new TradeRules { maxItemsPerSide = 2 });
            book.Propose(1, 2, 0d, out _);
            Assert.IsFalse(book.SetOffer(1, null, 5, 0d, out _), "not accepted yet");
            book.Accept(2, 0d, out _);
            Assert.IsFalse(book.SetOffer(1, null, -1, 0d, out _), "negative money");
            Assert.IsFalse(book.SetOffer(1, new[] { Offer(InventoryArea.Equipment, 0) }, 0, 0d, out _), "worn items cannot be offered");
            Assert.IsFalse(book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 1), Offer(InventoryArea.Bag, 1, "other") }, 0, 0d, out _), "same slot twice");
            Assert.IsFalse(book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 1), Offer(InventoryArea.Bag, 2), Offer(InventoryArea.Bag, 3) }, 0, 0d, out _), "too many");
            Assert.IsFalse(book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 1, "sword", 0) }, 0, 0d, out _), "zero units");
            Assert.IsFalse(book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 1, "") }, 0, 0d, out _), "no item id");
            Assert.IsTrue(book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 1), Offer(InventoryArea.Hotbar, 1) }, 0, 0d, out _));
        }

        [Test]
        public void Confirm_NeedsBothLocksTheCurrentRevisionAndSomethingOffered()
        {
            TradeBook book = OpenTrade(out Trade t);
            Assert.IsFalse(book.Confirm(1, t.Revision, 2d, out _, out _), "nothing locked");
            book.SetLocked(1, true, 2d, out _);
            book.SetLocked(2, true, 2d, out _);
            Assert.IsFalse(book.Confirm(1, t.Revision, 2d, out _, out _), "nothing offered");

            book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 0) }, 0, 3d, out _);
            book.SetLocked(1, true, 3d, out _);
            book.SetLocked(2, true, 3d, out _);
            Assert.IsFalse(book.Confirm(1, t.Revision - 1, 3d, out _, out string reason), "stale revision");
            StringAssert.Contains("changed", reason);

            Assert.IsTrue(book.Confirm(1, t.Revision, 4d, out bool ready, out _));
            Assert.IsFalse(ready);
            Assert.IsTrue(book.Confirm(2, t.Revision, 4d, out ready, out _));
            Assert.IsTrue(ready);

            book.Complete(t);
            Assert.AreEqual(TradeState.Completed, t.State);
            Assert.IsNull(book.TradeOf(1));
        }

        [Test]
        public void Unlocking_ClearsBothConfirmations()
        {
            TradeBook book = OpenTrade(out Trade t);
            book.SetOffer(2, new[] { Offer(InventoryArea.Bag, 5) }, 3, 1d, out _);
            book.SetLocked(1, true, 1d, out _);
            book.SetLocked(2, true, 1d, out _);
            book.Confirm(1, t.Revision, 1d, out _, out _);
            Assert.IsTrue(t.Proposer.Confirmed);
            book.SetLocked(2, false, 2d, out _);
            Assert.IsFalse(t.Proposer.Confirmed);
            Assert.IsFalse(t.Partner.Confirmed);
            Assert.IsTrue(t.Proposer.Locked, "only the unlocking side unlocks");

            book.SetLocked(2, true, 3d, out _);
            book.Confirm(1, t.Revision, 3d, out _, out _);
            book.Unlock(t, 4d); // the server saw an offered stack move
            Assert.IsFalse(t.BothLocked);
            Assert.IsFalse(t.Proposer.Confirmed);
        }

        // ------------------------------------------------------------------ timeouts and range
        [Test]
        public void Expire_EndsOldRequestsIdleTradesAndSeparatedPlayers()
        {
            var rules = new TradeRules { proposalSeconds = 10f, idleSeconds = 60f };
            var book = new TradeBook(rules);
            Trade request = book.Propose(1, 2, 0d, out _);
            Trade open = book.Propose(3, 4, 0d, out _);
            book.Accept(4, 0d, out _);
            Trade far = book.Propose(5, 6, 0d, out _);
            var ended = new List<Trade>();

            book.Expire(5d, (a, b) => a == 5 ? "too far" : null, ended);
            CollectionAssert.AreEquivalent(new[] { far }, ended);
            Assert.AreEqual("too far", far.EndReason);

            ended.Clear();
            book.Expire(11d, null, ended);
            CollectionAssert.AreEquivalent(new[] { request }, ended);

            ended.Clear();
            book.SetOffer(3, new[] { Offer(InventoryArea.Bag, 0) }, 0, 30d, out _); // activity keeps it alive
            book.Expire(80d, null, ended);
            Assert.AreEqual(0, ended.Count);
            book.Expire(91d, null, ended);
            CollectionAssert.AreEquivalent(new[] { open }, ended);
            Assert.AreEqual(0, book.Trades.Count);
        }

        // ------------------------------------------------------------------ views
        [Test]
        public void View_ShowsTheTradeFromEachSide()
        {
            var rules = new TradeRules { proposalSeconds = 30f };
            var book = new TradeBook(rules);
            Trade t = book.Propose(1, 2, 0d, out _);
            TradeView asked = TradeView.For(t, 2, id => "P" + id, 10d, rules);
            Assert.IsTrue(asked.incoming);
            Assert.AreEqual("P1", asked.partnerName);
            Assert.AreEqual(20f, asked.secondsLeft, 0.001f);
            Assert.IsFalse(TradeView.For(t, 1, id => "P" + id, 10d, rules).incoming);

            book.Accept(2, 1d, out _);
            book.SetOffer(1, new[] { Offer(InventoryArea.Bag, 2, "gem", 3) }, 7, 2d, out _);
            TradeView mine = TradeView.For(t, 1, null, 2d, rules);
            TradeView theirs = TradeView.For(t, 2, null, 2d, rules);
            Assert.AreEqual(TradeState.Open, mine.State);
            Assert.AreEqual(1, mine.myItems.Count);
            Assert.AreEqual(0, mine.theirItems.Count);
            Assert.AreEqual(7, theirs.theirCurrency);
            Assert.AreEqual("gem", theirs.theirItems[0].itemId);
            Assert.AreEqual(t.Revision, theirs.revision);

            TradeView none = TradeView.For(null, 1, null, 0d, rules, "gone");
            Assert.AreEqual(TradeState.None, none.State);
            Assert.AreEqual("gone", none.message);
            Assert.IsNotNull(none.myItems);
        }

        // ------------------------------------------------------------------ interaction paths
        [Test]
        public void InteractionPath_FindsChildrenWithRepeatedNames()
        {
            var root = new GameObject("NPC");
            created.Add(root);
            Transform a = new GameObject("Volume").transform;
            a.SetParent(root.transform);
            Transform b = new GameObject("Volume").transform;
            b.SetParent(root.transform);
            Transform deep = new GameObject("Lever/Handle").transform;
            deep.SetParent(b);

            Assert.AreEqual("", InteractionPath.Of(root.transform, root.transform));
            Assert.AreEqual("Volume", InteractionPath.Of(root.transform, a));
            Assert.AreEqual("Volume[1]", InteractionPath.Of(root.transform, b));
            string path = InteractionPath.Of(root.transform, deep);
            Assert.AreEqual("Volume[1]/Lever_Handle", path);
            Assert.AreSame(deep, InteractionPath.Find(root.transform, path));
            Assert.AreSame(a, InteractionPath.Find(root.transform, "Volume"));
            Assert.AreSame(root.transform, InteractionPath.Find(root.transform, ""));
            Assert.IsNull(InteractionPath.Find(root.transform, "Volume[2]"));

            var other = new GameObject("Other");
            created.Add(other);
            Assert.IsNull(InteractionPath.Of(root.transform, other.transform), "not under the root");
        }

        // ------------------------------------------------------------------ merchant stock
        [Test]
        public void MerchantStock_ServerStateRoundTrips()
        {
            var sword = ScriptableObject.CreateInstance<MiscItemSO>();
            var gem = ScriptableObject.CreateInstance<MiscItemSO>();
            created.Add(sword);
            created.Add(gem);
            var ids = new Dictionary<ItemSO, string> { [sword] = "sword", [gem] = "gem" };
            var items = new Dictionary<string, ItemSO> { ["sword"] = sword, ["gem"] = gem };
            var entries = new List<MerchantStockEntry> { new MerchantStockEntry(sword, 3), new MerchantStockEntry(gem, 1, true) };

            var server = new MerchantInventory();
            server.Initialize(entries);
            server.Take(server.Slots[0], 1);
            server.AddBought(gem, 2, 4); // the unlimited gem line absorbs it: no buy-back line
            var unnamed = ScriptableObject.CreateInstance<MiscItemSO>();
            created.Add(unnamed);
            server.AddBought(unnamed, 1, 4); // no id: not sent
            var states = new List<MerchantSlotState>();
            server.CaptureState(states, entries, i => ids.TryGetValue(i, out string id) ? id : null, s => s.Id * 10);
            Assert.AreEqual(2, states.Count);
            Assert.AreEqual(2, states[0].quantity);
            Assert.AreEqual(0, states[0].entryIndex);
            Assert.IsTrue(states[1].unlimited);

            var client = new MerchantInventory();
            client.Initialize(new List<MerchantStockEntry>()); // a client's own (stale) start is replaced
            int changed = 0;
            client.Changed += () => changed++;
            Assert.AreEqual(2, client.ApplyServerState(states, entries, id => items.TryGetValue(id, out ItemSO i) ? i : null));
            Assert.AreEqual(1, changed);
            MerchantStockSlot first = client.Find(server.Slots[0].Id);
            Assert.IsNotNull(first);
            Assert.AreSame(sword, first.Item);
            Assert.AreSame(entries[0], first.Source);
            Assert.AreEqual(2, first.Quantity);
            Assert.AreEqual(server.Slots[0].Id * 10, first.ServerUnitPrice);

            // The next update keeps the line object (a window's selection stays on it) and takes the new numbers.
            server.Take(server.Slots[0], 1);
            server.CaptureState(states, entries, i => ids.TryGetValue(i, out string id) ? id : null, s => 99);
            client.ApplyServerState(states, entries, id => items.TryGetValue(id, out ItemSO i) ? i : null);
            Assert.AreSame(first, client.Find(first.Id));
            Assert.AreEqual(1, first.Quantity);
            Assert.AreEqual(99, first.ServerUnitPrice);
        }

        [Test]
        public void MerchantProcessor_OfflineKeepsTheMerchantsOwn()
        {
            Assert.AreEqual(NetMode.Offline, NetAuthority.Mode, "Edit Mode tests run without a session");
            var go = new GameObject("Merchant");
            created.Add(go);
            Merchant merchant = go.AddComponent<Merchant>();
            IMerchantTransactionProcessor global = Merchant.GlobalProcessor;
            try
            {
                var network = new RecordingProcessor();
                Merchant.GlobalProcessor = network;
                Assert.AreSame(network, merchant.Processor, "without a processor of its own the merchant uses the global one");
                var own = new RecordingProcessor();
                merchant.Processor = own;
                Assert.AreSame(own, merchant.Processor, "offline (and on a server) the merchant's own processor wins as before");
            }
            finally
            {
                Merchant.GlobalProcessor = global;
            }
        }

        private sealed class RecordingProcessor : IMerchantTransactionProcessor
        {
            public void Submit(MerchantTransactionRequest request, System.Action<MerchantTransactionResult> onComplete) { }
        }

        [Test]
        public void MerchantStock_UnknownItemsAndDuplicateIdsAreSkipped()
        {
            var sword = ScriptableObject.CreateInstance<MiscItemSO>();
            created.Add(sword);
            var states = new List<MerchantSlotState>
            {
                new MerchantSlotState { id = 4, itemId = "sword", quantity = 1, entryIndex = 9 },
                new MerchantSlotState { id = 4, itemId = "sword", quantity = 5 },
                new MerchantSlotState { id = 5, itemId = "missing", quantity = 1 },
            };
            var client = new MerchantInventory();
            Assert.AreEqual(1, client.ApplyServerState(states, null, id => id == "sword" ? sword : null));
            Assert.AreEqual(1, client.Slots.Count);
            Assert.IsNull(client.Slots[0].Source, "an entry index out of range is ignored");
        }
    }
}
#endif
