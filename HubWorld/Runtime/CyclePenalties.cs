using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HubWorld
{
    /// <summary>
    /// The price of being lost between dimensions, applied by the <see cref="HubWorldDirector"/> (the authority): part of
    /// the loot carried (see <see cref="LootLossRule"/>) and the Dimensional Disorientation debuff.
    /// </summary>
    public static class CyclePenalties
    {
        /// <summary>Identifies the debuff (applying it again refreshes it instead of stacking).</summary>
        public static readonly object DisorientationKey = new object();

        /// <summary>What a player lost.</summary>
        public struct LootLossResult
        {
            public int itemsLost;
            public int stacksHit;
            public int currencyLost;
            /// <summary>Item names with the amounts lost ("2 x Iron Ore").</summary>
            public List<string> lines;

            public bool Any => itemsLost > 0 || currencyLost > 0;

            /// <summary>"Lost 5 items and 30 coins." / "Your belongings made it through."</summary>
            public string Describe()
            {
                if (!Any)
                    return "Your belongings made it through.";
                var sb = new StringBuilder("Lost ");
                if (itemsLost > 0)
                    sb.Append(itemsLost == 1 ? "1 item" : $"{itemsLost} items");
                if (itemsLost > 0 && currencyLost > 0)
                    sb.Append(" and ");
                if (currencyLost > 0)
                    sb.Append(currencyLost).Append(" coins");
                return sb.Append('.').ToString();
            }
        }

        /// <summary>Takes <see cref="LootLossSettings.itemFraction"/> of what the player carries (bag, and hotbar if set) and the money share.</summary>
        public static LootLossResult ApplyLootLoss(CombatEntity player, LootLossSettings settings, System.Random random)
        {
            var result = new LootLossResult { lines = new List<string>() };
            if (player == null || settings == null)
                return result;
            random = random ?? new System.Random();

            InventoryManager inventory = InventoryManager.For(player);
            if (inventory != null && settings.itemFraction > 0f)
            {
                var items = new List<InventoryItem>();
                var seen = new HashSet<InventoryItem>();
                Collect(inventory.Slots, items, seen);
                if (settings.includeHotbar)
                    Collect(inventory.HotbarSlots, items, seen);

                var stacks = new List<LootStack>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    InventoryItem it = items[i];
                    bool keep = it.isEquipped || inventory.IsBeingDragged(it) ||
                                (settings.protectedItems != null && settings.protectedItems.Contains(it.itemScriptableObject));
                    stacks.Add(new LootStack(i, it.stackCurrent, keep));
                }
                int[] plan = LootLossRule.Plan(stacks, settings.itemFraction, random);
                for (int i = 0; i < plan.Length; i++)
                {
                    if (plan[i] <= 0 || items[i] == null)
                        continue;
                    string itemName = items[i].itemScriptableObject != null ? items[i].itemScriptableObject.Name : "item";
                    int removed = inventory.ConsumeItem(items[i], plan[i]);
                    if (removed <= 0)
                        continue;
                    result.itemsLost += removed;
                    result.stacksHit++;
                    result.lines.Add(removed > 1 ? $"{removed} x {itemName}" : itemName);
                }
            }

            if (settings.currencyFraction > 0f)
            {
                CurrencyWallet wallet = CurrencyWallet.For(player, false);
                if (wallet != null)
                {
                    int loss = (int)LootLossRule.CurrencyLoss(wallet.Balance, settings.currencyFraction);
                    if (loss > 0 && wallet.TrySpend(null, loss))
                        result.currencyLost = loss;
                }
            }
            return result;
        }

        private static void Collect(GameObject[] slots, List<InventoryItem> into, HashSet<InventoryItem> seen)
        {
            if (slots == null)
                return;
            foreach (GameObject go in slots)
            {
                InventorySlot slot = go != null ? go.GetComponent<InventorySlot>() : null;
                InventoryItem item = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
                if (item != null && item.itemScriptableObject != null && item.stackCurrent > 0 && seen.Add(item))
                    into.Add(item);
            }
        }

        /// <summary>Gives the player the Dimensional Disorientation debuff (refreshed if they already have it).</summary>
        public static bool ApplyDisorientation(CombatEntity player, DisorientationSettings settings)
        {
            if (player == null || settings == null || !settings.enabled || settings.duration <= 0f)
                return false;
            TimedStatModifiers timed = TimedStatModifiers.For(player);
            if (timed == null)
                return false;
            return timed.Apply(DisorientationKey, settings.label, null, true, ElementType.None, settings.duration, 1f,
                settings.combatStats, null, settings.characterStats, EffectStacking.Refresh, 1, settings.attachedEffect) != null;
        }

        /// <summary>Does the player have the debuff now?</summary>
        public static bool IsDisoriented(CombatEntity player)
        {
            TimedStatModifiers timed = player != null ? player.GetComponent<TimedStatModifiers>() : null;
            if (timed == null)
                return false;
            foreach (TimedStatModifiers.Active a in timed.All)
                if (a.key == DisorientationKey)
                    return true;
            return false;
        }
    }
}
