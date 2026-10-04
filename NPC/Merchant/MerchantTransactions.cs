using System;
using System.Collections.Generic;
using UnityEngine;

public enum MerchantTransactionType
{
    /// <summary>The player buys from the merchant.</summary>
    Buy,
    /// <summary>The player sells to the merchant.</summary>
    Sell,
}

public enum MerchantTransactionStatus
{
    Success,
    /// <summary>Only part of the quantity went through (the rest was refunded / kept).</summary>
    PartialSuccess,
    InvalidRequest,
    MerchantUnavailable,
    OutOfStock,
    NotEnoughCurrency,
    NoInventorySpace,
    /// <summary>The stack is not in the player's bag or hotbar (equipped items must be taken off first).</summary>
    ItemNotOwned,
    NotEnoughItems,
    MerchantRefuses,
    /// <summary>The price changed since the player saw it (the window refreshes and the player confirms again).</summary>
    PriceChanged,
    /// <summary>The player's wallet cannot hold the money (its limit, or no room for coin items).</summary>
    WalletFull,
    /// <summary>Waiting for the processor (a server) to answer.</summary>
    Pending,
    Failed,
}

/// <summary>
/// What the player wants to do: buy <see cref="Quantity"/> of a shop line, or sell <see cref="Quantity"/> of one of their
/// stacks. It holds only identities and numbers, so it can be sent to a server as is (map the merchant, slot id and
/// stack to network ids). <see cref="ExpectedTotal"/> is the price the player saw: a request is refused when the price
/// has changed meanwhile instead of charging something else.
/// </summary>
public readonly struct MerchantTransactionRequest
{
    public readonly MerchantTransactionType Type;
    public readonly Merchant Merchant;
    public readonly GameObject Player;
    public readonly ItemSO Item;
    public readonly int Quantity;
    /// <summary>Buy: the shop line (<see cref="MerchantStockSlot.Id"/>).</summary>
    public readonly int StockSlotId;
    /// <summary>Sell: the player's stack.</summary>
    public readonly InventoryItem Stack;
    /// <summary>The total price the player was shown (0 = do not check).</summary>
    public readonly int ExpectedTotal;

    private MerchantTransactionRequest(MerchantTransactionType type, Merchant merchant, GameObject player, ItemSO item, int quantity, int slotId, InventoryItem stack, int expectedTotal)
    {
        Type = type;
        Merchant = merchant;
        Player = player;
        Item = item;
        Quantity = quantity;
        StockSlotId = slotId;
        Stack = stack;
        ExpectedTotal = expectedTotal;
    }

    public static MerchantTransactionRequest Buy(Merchant merchant, GameObject player, MerchantStockSlot slot, int quantity, int expectedTotal = 0) =>
        new MerchantTransactionRequest(MerchantTransactionType.Buy, merchant, player, slot?.Item, quantity, slot != null ? slot.Id : -1, null, expectedTotal);

    public static MerchantTransactionRequest Sell(Merchant merchant, GameObject player, InventoryItem stack, int quantity, int expectedTotal = 0) =>
        new MerchantTransactionRequest(MerchantTransactionType.Sell, merchant, player, stack != null ? stack.itemScriptableObject : null, quantity, -1, stack, expectedTotal);
}

/// <summary>The outcome of a request: what happened, how many, for how much, and a message for the player.</summary>
public readonly struct MerchantTransactionResult
{
    public readonly MerchantTransactionStatus Status;
    public readonly MerchantTransactionRequest Request;
    /// <summary>Units bought / sold (or that would be, for a quote).</summary>
    public readonly int Quantity;
    /// <summary>Money paid / received (or that would be).</summary>
    public readonly int TotalPrice;
    public readonly int UnitPrice;
    public readonly string Message;

    public MerchantTransactionResult(MerchantTransactionStatus status, MerchantTransactionRequest request, int quantity, int unitPrice, int totalPrice, string message)
    {
        Status = status;
        Request = request;
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalPrice = totalPrice;
        Message = message ?? "";
    }

    public bool Succeeded => Status == MerchantTransactionStatus.Success || Status == MerchantTransactionStatus.PartialSuccess;

    public static MerchantTransactionResult Fail(MerchantTransactionStatus status, in MerchantTransactionRequest request, string message, int unitPrice = 0) =>
        new MerchantTransactionResult(status, request, 0, unitPrice, 0, message);
}

/// <summary>
/// Carries out merchant requests. The default runs them at once on this machine
/// (<see cref="LocalMerchantTransactionProcessor"/>). For a server-authoritative game, assign your own to
/// <see cref="Merchant.GlobalProcessor"/> (or one merchant's <see cref="Merchant.Processor"/>): send the request to the
/// server, run <see cref="MerchantTransactions.Execute"/> there, and call <paramref name="onComplete"/> with its answer -
/// the shop window waits (no double purchases) and refreshes from the replicated inventory, wallet and stock.
/// </summary>
public interface IMerchantTransactionProcessor
{
    void Submit(MerchantTransactionRequest request, Action<MerchantTransactionResult> onComplete);
}

/// <summary>Runs requests immediately on this machine (single player, listen-server host).</summary>
public sealed class LocalMerchantTransactionProcessor : IMerchantTransactionProcessor
{
    public static readonly LocalMerchantTransactionProcessor Instance = new LocalMerchantTransactionProcessor();

    public void Submit(MerchantTransactionRequest request, Action<MerchantTransactionResult> onComplete)
    {
        MerchantTransactionResult result;
        try { result = MerchantTransactions.Execute(request); }
        catch (Exception e)
        {
            Debug.LogException(e);
            result = MerchantTransactionResult.Fail(MerchantTransactionStatus.Failed, request, "Something went wrong.");
        }
        onComplete?.Invoke(result);
    }
}

/// <summary>
/// The rules of buying and selling, in one place: <see cref="Quote"/> checks a request without changing anything (the
/// window uses it to enable buttons and explain why not), <see cref="Execute"/> checks it again and applies it.
/// <para>Safety: everything is checked before anything changes; money and items move in an order that can be undone
/// (pay, then receive the items - refunding what did not fit; remove the sold items, then get paid - giving the items
/// back if the payment cannot be received). Items that cannot be given back are dropped next to the player, never lost.</para>
/// </summary>
public static class MerchantTransactions
{
    /// <summary>Would the request succeed? Nothing changes. The result has the quantity and price it would use.</summary>
    public static MerchantTransactionResult Quote(in MerchantTransactionRequest request)
    {
        return request.Type == MerchantTransactionType.Buy ? CheckBuy(request, out _, out _, out _, out _) : CheckSell(request, out _, out _, out _);
    }

    /// <summary>Checks and applies the request.</summary>
    public static MerchantTransactionResult Execute(in MerchantTransactionRequest request)
    {
        MerchantTransactionResult result = request.Type == MerchantTransactionType.Buy ? ExecuteBuy(request) : ExecuteSell(request);
        request.Merchant?.OnTransaction(result);
        return result;
    }

    // ------------------------------------------------------------------ buy
    private static MerchantTransactionResult CheckBuy(in MerchantTransactionRequest r, out MerchantStockSlot slot, out InventoryManager inventory,
        out CurrencyWallet wallet, out CurrencyDefinition currency)
    {
        slot = null;
        inventory = null;
        wallet = null;
        currency = null;
        if (!CheckCommon(r, out MerchantTransactionResult fail))
            return fail;
        Merchant m = r.Merchant;
        slot = m.Inventory.Find(r.StockSlotId);
        if (slot == null || slot.Item == null || slot.Item != r.Item)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.InvalidRequest, r, "That item is no longer sold.");
        string itemName = slot.Item.Name;
        if (!slot.InStock)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.OutOfStock, r, $"{itemName} is sold out.");
        if (slot.Available < r.Quantity)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.OutOfStock, r, $"Only {slot.Quantity} {itemName} left.");

        int unit = m.GetBuyPrice(slot, r.Player);
        long total = (long)unit * r.Quantity;
        if (total > int.MaxValue)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.InvalidRequest, r, "That is too many at once.", unit);
        if (r.ExpectedTotal > 0 && r.ExpectedTotal != total)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.PriceChanged, r, "The price has changed.", unit);

        inventory = InventoryManager.For(r.Player.transform);
        if (inventory == null)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.Failed, r, "You have no inventory.", unit);
        wallet = CurrencyWallet.For(r.Player.transform);
        currency = m.CurrencyFor(wallet);
        if (!wallet.CanAfford(currency, (int)total))
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.NotEnoughCurrency, r,
                $"Not enough {currency.DisplayName} ({currency.Format((int)total)} needed, you have {currency.Format(wallet.GetBalance(currency))}).", unit);
        if (!inventory.HasEnoughSpace(slot.Item, r.Quantity))
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.NoInventorySpace, r,
                inventory.UseGridInventory && slot.Item.GridSize != Vector2Int.one
                    ? $"No room in your bag for {itemName} ({slot.Item.GridSize.x}×{slot.Item.GridSize.y})."
                    : $"No room in your bag for {(r.Quantity > 1 ? $"{r.Quantity} {itemName}" : itemName)}.", unit);
        return new MerchantTransactionResult(MerchantTransactionStatus.Success, r, r.Quantity, unit, (int)total, "");
    }

    private static MerchantTransactionResult ExecuteBuy(in MerchantTransactionRequest r)
    {
        MerchantTransactionResult check = CheckBuy(r, out MerchantStockSlot slot, out InventoryManager inventory, out CurrencyWallet wallet, out CurrencyDefinition currency);
        if (!check.Succeeded)
            return check;
        int unit = check.UnitPrice;
        int total = check.TotalPrice;

        // 1. Pay.
        if (!wallet.TrySpend(currency, total))
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.NotEnoughCurrency, r, $"Not enough {currency.DisplayName}.", unit);

        // 2. Receive the items; refund what did not fit (the space check makes this rare: it guards against surprises).
        int left = inventory.AddItem(slot.Item, r.Quantity);
        int added = r.Quantity - left;
        if (left > 0 && !wallet.TryAdd(currency, unit * left))
            Debug.LogError($"[Merchant] Could not refund {currency.Format(unit * left)} for {left} {slot.Item.Name} that did not fit (wallet full).", r.Merchant);
        if (added <= 0)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.NoInventorySpace, r, $"No room in your bag for {slot.Item.Name}.", unit);

        // 3. The merchant's stock goes down.
        r.Merchant.Inventory.Take(slot, added);
        int paid = unit * added;
        string text = added > 1 ? $"Bought {added} {slot.Item.Name} for {currency.Format(paid)}." : $"Bought {slot.Item.Name} for {currency.Format(paid)}.";
        return new MerchantTransactionResult(left > 0 ? MerchantTransactionStatus.PartialSuccess : MerchantTransactionStatus.Success, r, added, unit, paid,
            left > 0 ? text + $" {left} did not fit and were refunded." : text);
    }

    // ------------------------------------------------------------------ sell
    private static MerchantTransactionResult CheckSell(in MerchantTransactionRequest r, out InventoryManager inventory, out CurrencyWallet wallet, out CurrencyDefinition currency)
    {
        inventory = null;
        wallet = null;
        currency = null;
        if (!CheckCommon(r, out MerchantTransactionResult fail))
            return fail;
        Merchant m = r.Merchant;
        InventoryItem stack = r.Stack;
        if (stack == null || stack.itemScriptableObject == null || stack.itemScriptableObject != r.Item)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.ItemNotOwned, r, "You do not have that item any more.");
        inventory = InventoryManager.For(r.Player.transform);
        if (inventory == null)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.Failed, r, "You have no inventory.");
        if (!IsInBag(inventory, stack, out bool equipped))
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.ItemNotOwned, r,
                equipped ? $"Take off {r.Item.Name} before selling it." : "You do not have that item any more.");
        if (inventory.IsBeingDragged(stack))
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.InvalidRequest, r, "Drop the item first.");
        if (r.Quantity > stack.stackCurrent)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.NotEnoughItems, r, $"You only have {stack.stackCurrent} {r.Item.Name} in that stack.");
        if (!m.WillBuy(r.Item, out string refusal))
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.MerchantRefuses, r, refusal);

        int unit = m.GetSellPrice(stack, r.Player);
        if (unit <= 0 && m.RefusesWorthlessItems)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.MerchantRefuses, r, $"{r.Item.Name} is worthless here.");
        long total = (long)unit * r.Quantity;
        if (total > int.MaxValue)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.InvalidRequest, r, "That is too many at once.", unit);
        if (r.ExpectedTotal > 0 && r.ExpectedTotal != total)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.PriceChanged, r, "The price has changed.", unit);
        wallet = CurrencyWallet.For(r.Player.transform);
        currency = m.CurrencyFor(wallet);
        // Coin items: room is checked after the sold items are taken out (they free space); numbers: checked now.
        if (!currency.IsItemBacked && !wallet.CanReceive(currency, (int)total))
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.WalletFull, r, $"You cannot carry more {currency.DisplayName}.", unit);
        return new MerchantTransactionResult(MerchantTransactionStatus.Success, r, r.Quantity, unit, (int)total, "");
    }

    private static MerchantTransactionResult ExecuteSell(in MerchantTransactionRequest r)
    {
        MerchantTransactionResult check = CheckSell(r, out InventoryManager inventory, out CurrencyWallet wallet, out CurrencyDefinition currency);
        if (!check.Succeeded)
            return check;
        InventoryItem stack = r.Stack;
        ItemSO item = r.Item;
        int unit = check.UnitPrice;

        // Remember the durabilities, to give the exact items back if the payment cannot be received.
        List<int> durabilities = UnitDurabilities(stack, r.Quantity);

        // 1. The items leave the player's inventory (weight, the hand and an emptied stack follow).
        int removed = inventory.ConsumeItem(stack, r.Quantity);
        if (removed <= 0)
            return MerchantTransactionResult.Fail(MerchantTransactionStatus.Failed, r, $"Could not sell {item.Name}.", unit);

        // 2. Get paid; if the money cannot be received, the items come back.
        int pay = unit * removed;
        if (!wallet.TryAdd(currency, pay))
        {
            GiveBack(inventory, r.Player, item, removed, durabilities);
            return MerchantTransactionResult.Fail(currency.IsItemBacked ? MerchantTransactionStatus.NoInventorySpace : MerchantTransactionStatus.WalletFull, r,
                currency.IsItemBacked ? $"No room in your bag for the {currency.DisplayName}." : $"You cannot carry more {currency.DisplayName}.", unit);
        }

        // 3. The merchant may sell it on.
        if (r.Merchant.ResellsBoughtItems)
            r.Merchant.Inventory.AddBought(item, removed, r.Merchant.MaxBuybackLines);
        string text = removed > 1 ? $"Sold {removed} {item.Name} for {currency.Format(pay)}." : $"Sold {item.Name} for {currency.Format(pay)}.";
        return new MerchantTransactionResult(removed < r.Quantity ? MerchantTransactionStatus.PartialSuccess : MerchantTransactionStatus.Success, r, removed, unit, pay, text);
    }

    // ------------------------------------------------------------------ helpers
    private static bool CheckCommon(in MerchantTransactionRequest r, out MerchantTransactionResult fail)
    {
        fail = default;
        if (r.Merchant == null || !r.Merchant.isActiveAndEnabled)
        {
            fail = MerchantTransactionResult.Fail(MerchantTransactionStatus.MerchantUnavailable, r, "The merchant is not available.");
            return false;
        }
        if (r.Player == null || r.Item == null || r.Quantity <= 0)
        {
            fail = MerchantTransactionResult.Fail(MerchantTransactionStatus.InvalidRequest, r, "Nothing to trade.");
            return false;
        }
        if (r.Type == MerchantTransactionType.Sell && !r.Merchant.BuysItems)
        {
            fail = MerchantTransactionResult.Fail(MerchantTransactionStatus.MerchantRefuses, r, $"{r.Merchant.ShopName} does not buy anything.");
            return false;
        }
        return true;
    }

    /// <summary>Is the stack in the player's bag or hotbar (not worn in an equipment slot)?</summary>
    public static bool IsInBag(InventoryManager inventory, InventoryItem stack, out bool equipped)
    {
        equipped = false;
        if (inventory == null || stack == null)
            return false;
        InventorySlot slot = inventory.FindSlotHolding(stack.gameObject);
        if (slot == null)
            return false;
        foreach (GameObject go in inventory.StorageSlots)
            if (go == slot.gameObject)
                return true;
        equipped = true;
        return false;
    }

    /// <summary>The durabilities of the <paramref name="count"/> units ConsumeItem takes (the unit in use first).</summary>
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

    /// <summary>Returns sold items whose payment failed: into the inventory, else dropped next to the player.</summary>
    private static void GiveBack(InventoryManager inventory, GameObject player, ItemSO item, int count, List<int> durabilities)
    {
        int left = inventory.AddItem(item, count, durabilities);
        if (left <= 0)
            return;
        Vector3 at = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.5f;
        if (ItemHandler.SpawnWorldItem(item, left, null, at, player) != null)
            inventory.ShowMessage($"No room: {item.Name} ×{left} was dropped.");
        else
            Debug.LogError($"[Merchant] {left} {item.Name} could not be given back (no room and no prefab to drop).", inventory);
    }
}
