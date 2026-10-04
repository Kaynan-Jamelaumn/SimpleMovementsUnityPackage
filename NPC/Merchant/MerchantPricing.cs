using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How fractional prices become whole amounts.</summary>
public enum PriceRounding
{
    Nearest,
    Down,
    Up,
}

/// <summary>Everything a price can depend on.</summary>
public readonly struct MerchantPriceContext
{
    public readonly Merchant Merchant;
    public readonly ItemSO Item;
    /// <summary>True: the player buys from the merchant. False: the player sells to the merchant.</summary>
    public readonly bool PlayerBuys;
    /// <summary>The shop line being bought (null when selling or when not known).</summary>
    public readonly MerchantStockSlot Slot;
    /// <summary>The player's stack being sold (null when buying).</summary>
    public readonly InventoryItem Stack;
    /// <summary>The buyer / seller (may be null in editor previews).</summary>
    public readonly GameObject Player;
    public readonly ItemCategoryMatch Category;

    public MerchantPriceContext(Merchant merchant, ItemSO item, bool playerBuys, MerchantStockSlot slot, InventoryItem stack, GameObject player, ItemCategoryMatch category)
    {
        Merchant = merchant;
        Item = item;
        PlayerBuys = playerBuys;
        Slot = slot;
        Stack = stack;
        Player = player;
        Category = category;
    }
}

/// <summary>
/// A pricing rule added to a merchant's Pricing ▸ Modifiers (pick the type from the dropdown). Write your own by
/// deriving from it: reputation discounts, faction prices, haggling traits, time-of-day markets...
/// Modifiers run in list order on the price before rounding.
/// </summary>
[Serializable]
public abstract class MerchantPriceModifier
{
    [Tooltip("Off = the modifier is skipped.")]
    public bool enabled = true;

    /// <summary>Returns the changed price (before rounding).</summary>
    public abstract float Modify(float price, in MerchantPriceContext context);

    /// <summary>One line for the merchant inspector.</summary>
    public virtual string Describe() => GetType().Name;
}

/// <summary>Different prices for a category (and its subcategories): a smith who pays well for ores, a jeweller who charges more for rings.</summary>
[Serializable]
public class CategoryPriceModifier : MerchantPriceModifier
{
    [Tooltip("The category (its subcategories are included).")]
    public ItemCategory category;
    [Tooltip("Multiplies what the player PAYS for items of the category.")]
    [Min(0f)] public float buyMultiplier = 1f;
    [Tooltip("Multiplies what the player GETS for items of the category.")]
    [Min(0f)] public float sellMultiplier = 1f;

    public override float Modify(float price, in MerchantPriceContext c)
    {
        if (category == null || !c.Category.IsIn(category))
            return price;
        return price * (c.PlayerBuys ? buyMultiplier : sellMultiplier);
    }

    public override string Describe() => $"{(category != null ? category.Path : "(no category)")}: buy ×{buyMultiplier:0.##}, sell ×{sellMultiplier:0.##}";
}

/// <summary>Different prices for one item.</summary>
[Serializable]
public class ItemPriceModifier : MerchantPriceModifier
{
    public ItemSO item;
    [Min(0f)] public float buyMultiplier = 1f;
    [Min(0f)] public float sellMultiplier = 1f;

    public override float Modify(float price, in MerchantPriceContext c)
    {
        if (item == null || c.Item != item)
            return price;
        return price * (c.PlayerBuys ? buyMultiplier : sellMultiplier);
    }

    public override string Describe() => $"{(item != null ? item.Name : "(no item)")}: buy ×{buyMultiplier:0.##}, sell ×{sellMultiplier:0.##}";
}

/// <summary>Different prices for a kind of item (all weapons, all materials...).</summary>
[Serializable]
public class ItemKindPriceModifier : MerchantPriceModifier
{
    public ItemKind kind = ItemKind.Material;
    [Min(0f)] public float buyMultiplier = 1f;
    [Min(0f)] public float sellMultiplier = 1f;

    public override float Modify(float price, in MerchantPriceContext c)
    {
        if (c.Item == null || !new ItemCategoryRule(kind).Matches(c.Item))
            return price;
        return price * (c.PlayerBuys ? buyMultiplier : sellMultiplier);
    }

    public override string Describe() => $"{kind}: buy ×{buyMultiplier:0.##}, sell ×{sellMultiplier:0.##}";
}

/// <summary>Limited goods get dearer as they run out (up to +Max Increase when one is left).</summary>
[Serializable]
public class ScarcityPriceModifier : MerchantPriceModifier
{
    [Tooltip("Price increase when the stock is almost gone (0.5 = +50%).")]
    [Min(0f)] public float maxIncrease = 0.5f;

    public override float Modify(float price, in MerchantPriceContext c)
    {
        if (!c.PlayerBuys || c.Slot == null || c.Slot.Unlimited || c.Slot.Source == null)
            return price;
        int max = c.Slot.Source.MaxQuantity;
        float missing = max > 1 ? Mathf.Clamp01(1f - (c.Slot.Quantity - 1) / (float)(max - 1)) : 0f;
        return price * (1f + maxIncrease * missing);
    }

    public override string Describe() => $"Scarcity: up to +{maxIncrease:0%}";
}

/// <summary>
/// A merchant's prices. The player pays an item's value (its Price) × Buy Multiplier and gets its value × Sell
/// Multiplier (× its durability for worn items); stock entries, category / item modifiers and your own modifiers change
/// that. Prices are whole amounts, calculated the same way everywhere (the shop window, the transaction, a server).
/// </summary>
[Serializable]
public class MerchantPricing
{
    [Tooltip("What the player pays: item value × this (1.2 = 20% above value).")]
    [Min(0f)] public float buyMultiplier = 1f;
    [Tooltip("What the player gets when selling: item value × this (0.4 = 40% of value).")]
    [Min(0f)] public float sellMultiplier = 0.4f;
    [Tooltip("Nothing costs less than this.")]
    [Min(0)] public int minimumBuyPrice = 1;
    [Tooltip("Worn items sell for less: the price follows the durability of the unit in use.")]
    public bool sellPriceFollowsDurability = true;
    [Tooltip("What a completely worn item still fetches (fraction of its full selling price).")]
    [Range(0f, 1f)] public float brokenItemValue = 0.1f;
    [Tooltip("The merchant never pays more for an item than it would sell it for (stops buy-low-sell-high loops).")]
    public bool sellNeverAboveBuy = true;
    [Tooltip("How fractional prices are rounded.")]
    public PriceRounding rounding = PriceRounding.Nearest;
    [Tooltip("Extra rules applied in order: category / item / item kind prices, scarcity, or your own (derive from MerchantPriceModifier).")]
    [SerializeReference, SubclassSelector] public List<MerchantPriceModifier> modifiers = new List<MerchantPriceModifier>();

    /// <summary>Price the player pays for one item.</summary>
    public int BuyPrice(in MerchantPriceContext c)
    {
        if (c.Item == null)
            return 0;
        MerchantStockEntry entry = c.Slot?.Source;
        if (entry != null && entry.fixedPrice > 0)
            return entry.fixedPrice;
        float price = Mathf.Max(0f, c.Item.Price) * buyMultiplier * (entry != null ? entry.priceMultiplier : 1f);
        price = ApplyModifiers(price, c);
        return Mathf.Max(minimumBuyPrice, Round(price));
    }

    /// <summary>Price the player gets for one item of <see cref="MerchantPriceContext.Stack"/> (0 = worthless).</summary>
    public int SellPrice(in MerchantPriceContext c)
    {
        if (c.Item == null)
            return 0;
        float price = Mathf.Max(0f, c.Item.Price) * sellMultiplier;
        if (sellPriceFollowsDurability && c.Stack != null && c.Item.MaxDurability > 1)
        {
            float d = Mathf.Clamp01(c.Stack.durability / c.Item.MaxDurability);
            price *= Mathf.Lerp(brokenItemValue, 1f, d);
        }
        price = ApplyModifiers(price, c);
        int sell = Mathf.Max(0, Round(price));
        if (sellNeverAboveBuy)
        {
            var buyContext = new MerchantPriceContext(c.Merchant, c.Item, true, null, null, c.Player, c.Category);
            sell = Mathf.Min(sell, BuyPrice(buyContext));
        }
        return sell;
    }

    private float ApplyModifiers(float price, in MerchantPriceContext c)
    {
        if (modifiers == null)
            return price;
        for (int i = 0; i < modifiers.Count; i++)
        {
            MerchantPriceModifier m = modifiers[i];
            if (m == null || !m.enabled)
                continue;
            try { price = Mathf.Max(0f, m.Modify(price, c)); }
            catch (Exception e) { Debug.LogException(e); }
        }
        return price;
    }

    private int Round(float price)
    {
        switch (rounding)
        {
            case PriceRounding.Down: return Mathf.FloorToInt(price + 0.0001f);
            case PriceRounding.Up: return Mathf.CeilToInt(price - 0.0001f);
            default: return Mathf.RoundToInt(price);
        }
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (sellMultiplier > buyMultiplier && !sellNeverAboveBuy)
            warnings.Add("Sell Multiplier is above Buy Multiplier: the player can make money buying and selling the same item. Turn on Sell Never Above Buy.");
        if (buyMultiplier <= 0f)
            warnings.Add("Buy Multiplier is 0: everything costs the Minimum Buy Price.");
        if (modifiers != null)
            for (int i = 0; i < modifiers.Count; i++)
                if (modifiers[i] == null)
                    warnings.Add($"Pricing modifier #{i + 1} is empty (pick a type or remove it).");
    }
}
