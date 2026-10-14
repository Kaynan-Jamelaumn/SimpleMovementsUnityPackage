using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameNet
{
    /// <summary>One side of an exchange: the player's inventory and wallet, and what they give.</summary>
    public sealed class TradeParty
    {
        public InventoryManager Inventory;
        public CurrencyWallet Wallet;
        public IReadOnlyList<TradeItemOffer> Items;
        /// <summary>Money given (in the exchange's currency).</summary>
        public int Currency;
        /// <summary>Name used in messages ("Bram has no room...").</summary>
        public string Name;
    }

    /// <summary>
    /// Performs a player-to-player trade on the server's inventories and wallets, all or nothing. Everything is checked
    /// first (each offered stack still in its slot with that item and enough units, not being dragged, money enough);
    /// then the offered items leave both players, the money is paid, each player receives the other's items and money.
    /// If any step fails, every step done so far is undone in reverse order and the items go back to their owners (into
    /// the inventory, else dropped at their feet), so items and money are never duplicated or lost.
    /// </summary>
    public static class TradeExchange
    {
        private sealed class Taken
        {
            public ItemSO Item;
            public int Count;
            public List<int> Durabilities;
        }

        /// <summary>
        /// Checks that <paramref name="party"/> can give what it offers (nothing changes). <paramref name="reason"/>
        /// explains a refusal.
        /// </summary>
        public static bool Validate(TradeParty party, CurrencyDefinition currency, out string reason)
        {
            return Resolve(party, currency, null, out reason);
        }

        /// <summary>
        /// Exchanges the offers of <paramref name="a"/> and <paramref name="b"/> (all or nothing). False with a reason
        /// (and nothing changed) when it cannot be done.
        /// </summary>
        public static bool Execute(TradeParty a, TradeParty b, CurrencyDefinition currency, out string reason)
        {
            reason = "";
            if (a == null || b == null || a.Inventory == null || b.Inventory == null || a.Inventory == b.Inventory)
            {
                reason = "The trade is not possible.";
                return false;
            }
            var stacksA = new List<(InventoryItem stack, int count)>();
            var stacksB = new List<(InventoryItem stack, int count)>();
            if (!Resolve(a, currency, stacksA, out reason) || !Resolve(b, currency, stacksB, out reason))
                return false;

            var takenA = new List<Taken>();
            var takenB = new List<Taken>();
            var addedToA = new List<Taken>();
            var addedToB = new List<Taken>();
            bool paidA = false, paidB = false, gotA = false, gotB = false;

            // 1. The offered items leave both players.
            if (!TakeAll(a.Inventory, stacksA, takenA) || !TakeAll(b.Inventory, stacksB, takenB))
            {
                reason = "An offered item could not be taken.";
                goto Undo;
            }
            // 2. Both pay.
            if (a.Currency > 0 && !(paidA = a.Wallet.TrySpend(currency, a.Currency)))
            {
                reason = $"{a.Name} does not have the {currency.DisplayName} offered.";
                goto Undo;
            }
            if (b.Currency > 0 && !(paidB = b.Wallet.TrySpend(currency, b.Currency)))
            {
                reason = $"{b.Name} does not have the {currency.DisplayName} offered.";
                goto Undo;
            }
            // 3. Each receives the other's items (room is checked first so a full bag refuses cleanly).
            if (!HasRoom(a.Inventory, takenB))
            {
                reason = $"{a.Name} has no room for the items.";
                goto Undo;
            }
            if (!HasRoom(b.Inventory, takenA))
            {
                reason = $"{b.Name} has no room for the items.";
                goto Undo;
            }
            if (!GiveAll(a.Inventory, takenB, addedToA))
            {
                reason = $"{a.Name} has no room for the items.";
                goto Undo;
            }
            if (!GiveAll(b.Inventory, takenA, addedToB))
            {
                reason = $"{b.Name} has no room for the items.";
                goto Undo;
            }
            // 4. Each receives the other's money.
            if (b.Currency > 0 && !(gotA = a.Wallet.TryAdd(currency, b.Currency)))
            {
                reason = $"{a.Name} cannot carry more {currency.DisplayName}.";
                goto Undo;
            }
            if (a.Currency > 0 && !(gotB = b.Wallet.TryAdd(currency, a.Currency)))
            {
                reason = $"{b.Name} cannot carry more {currency.DisplayName}.";
                goto Undo;
            }
            return true;

        Undo:
            if (gotA) a.Wallet.TrySpend(currency, b.Currency);
            if (gotB) b.Wallet.TrySpend(currency, a.Currency);
            RemoveAll(b.Inventory, addedToB);
            RemoveAll(a.Inventory, addedToA);
            if (paidB) Refund(b, currency);
            if (paidA) Refund(a, currency);
            GiveBack(b.Inventory, takenB);
            GiveBack(a.Inventory, takenA);
            return false;
        }

        // ------------------------------------------------------------------ steps
        /// <summary>Finds the offered stacks and checks them and the money; fills <paramref name="stacks"/> when given.</summary>
        private static bool Resolve(TradeParty p, CurrencyDefinition currency, List<(InventoryItem stack, int count)> stacks, out string reason)
        {
            reason = "";
            string who = string.IsNullOrEmpty(p?.Name) ? "A player" : p.Name;
            if (p == null || p.Inventory == null || !p.Inventory.IsAuthoritative)
            {
                reason = $"{who} has no inventory.";
                return false;
            }
            var seen = new HashSet<InventoryItem>(ReferenceComparer<InventoryItem>.Instance);
            int backingOffered = 0;
            int n = p.Items != null ? p.Items.Count : 0;
            for (int i = 0; i < n; i++)
            {
                TradeItemOffer o = p.Items[i];
                if (!o.IsWellFormed)
                {
                    reason = "Invalid offer.";
                    return false;
                }
                InventorySlot slot = p.Inventory.SlotAt(o.Slot);
                InventoryItem stack = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
                if (stack == null || stack.itemScriptableObject == null || ItemDatabase.Id(stack.itemScriptableObject) != o.itemId)
                {
                    reason = $"{who}'s offer changed (an item was moved).";
                    return false;
                }
                if (!seen.Add(stack))
                {
                    reason = "The same stack is offered twice.";
                    return false;
                }
                if (stack.stackCurrent < o.quantity)
                {
                    reason = $"{who} no longer has {o.quantity} {stack.itemScriptableObject.Name}.";
                    return false;
                }
                if (p.Inventory.IsBeingDragged(stack))
                {
                    reason = $"{who} is moving an offered item.";
                    return false;
                }
                if (currency != null && currency.IsItemBacked && stack.itemScriptableObject == currency.BackingItem)
                    backingOffered += o.quantity;
                stacks?.Add((stack, o.quantity));
            }
            if (p.Currency < 0)
            {
                reason = "Invalid amount.";
                return false;
            }
            if (p.Currency > 0)
            {
                if (p.Wallet == null || currency == null)
                {
                    reason = $"{who} has no wallet.";
                    return false;
                }
                // Coin items offered as items are not also available as money.
                if ((long)p.Wallet.GetBalance(currency) - backingOffered < p.Currency)
                {
                    reason = $"{who} does not have {currency.Format(p.Currency)}.";
                    return false;
                }
            }
            return true;
        }

        private static bool TakeAll(InventoryManager inventory, List<(InventoryItem stack, int count)> stacks, List<Taken> taken)
        {
            foreach ((InventoryItem stack, int count) in stacks)
            {
                if (stack == null || stack.itemScriptableObject == null)
                    return false;
                ItemSO item = stack.itemScriptableObject;
                List<int> durabilities = UnitDurabilities(stack, count);
                int removed = inventory.ConsumeItem(stack, count);
                if (removed > 0)
                    taken.Add(new Taken { Item = item, Count = removed, Durabilities = Last(durabilities, removed) });
                if (removed != count)
                    return false;
            }
            return true;
        }

        /// <summary>Is there room for every item of <paramref name="incoming"/> (counted per kind of item)?</summary>
        private static bool HasRoom(InventoryManager inventory, List<Taken> incoming)
        {
            var totals = new Dictionary<ItemSO, int>(ReferenceComparer<ItemSO>.Instance);
            foreach (Taken t in incoming)
                totals[t.Item] = (totals.TryGetValue(t.Item, out int n) ? n : 0) + t.Count;
            foreach (KeyValuePair<ItemSO, int> kv in totals)
                if (!inventory.HasEnoughSpace(kv.Key, kv.Value))
                    return false;
            return true;
        }

        private static bool GiveAll(InventoryManager inventory, List<Taken> items, List<Taken> added)
        {
            foreach (Taken t in items)
            {
                int left = inventory.AddItem(t.Item, t.Count, t.Durabilities);
                int got = t.Count - left;
                if (got > 0)
                    added.Add(new Taken { Item = t.Item, Count = got });
                if (left > 0)
                    return false;
            }
            return true;
        }

        private static void RemoveAll(InventoryManager inventory, List<Taken> added)
        {
            for (int i = added.Count - 1; i >= 0; i--)
            {
                Taken t = added[i];
                int removed = inventory.RemoveItems(t.Item, t.Count);
                if (removed != t.Count)
                    Debug.LogError($"[Trade] Undo: only {removed} of {t.Count} {t.Item.Name} could be taken back from '{inventory.name}'.", inventory);
            }
        }

        private static void Refund(TradeParty p, CurrencyDefinition currency)
        {
            if (!p.Wallet.TryAdd(currency, p.Currency))
                Debug.LogError($"[Trade] Undo: {currency.Format(p.Currency)} could not be given back to {p.Name} (wallet full).", p.Wallet);
        }

        /// <summary>Returns taken items to their owner: into the inventory, else dropped next to the player.</summary>
        private static void GiveBack(InventoryManager inventory, List<Taken> taken)
        {
            foreach (Taken t in taken)
            {
                int left = inventory.AddItem(t.Item, t.Count, t.Durabilities);
                if (left <= 0)
                    continue;
                GameObject player = inventory.Player != null ? inventory.Player : inventory.gameObject;
                Vector3 at = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.5f;
                List<int> rest = Last(t.Durabilities, left);
                if (ItemHandler.SpawnWorldItem(t.Item, left, rest, at, player) != null)
                    inventory.ShowMessage($"No room: {t.Item.Name} ×{left} was dropped.");
                else
                    Debug.LogError($"[Trade] {left} {t.Item.Name} could not be given back (no room and no prefab to drop).", inventory);
            }
        }

        // ------------------------------------------------------------------ durabilities
        /// <summary>The durabilities of the <paramref name="count"/> units ConsumeItem takes, ordered for AddItem.</summary>
        private static List<int> UnitDurabilities(InventoryItem stack, int count)
        {
            var list = new List<int>(count);
            if (stack == null || stack.itemScriptableObject == null || stack.itemScriptableObject.MaxDurability <= 1)
                return list;
            list.Add(Mathf.RoundToInt(stack.durability));
            List<int> rest = stack.DurabilityList;
            for (int i = rest.Count - 1; i >= 0 && list.Count < count; i--)
                list.Add(rest[i]);
            list.Reverse(); // AddItem uses the last one first: the unit that was in use stays in use
            return list;
        }

        /// <summary>The last <paramref name="count"/> entries (AddItem uses a durability list from its end).</summary>
        private static List<int> Last(List<int> list, int count)
        {
            if (list == null || list.Count <= count)
                return list;
            return list.GetRange(list.Count - count, count);
        }
    }
}
