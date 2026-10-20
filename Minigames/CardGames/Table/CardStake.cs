using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What players put on the line at a card table.</summary>
public enum CardStakeKind
{
    /// <summary>Just for fun.</summary>
    None,
    /// <summary>Every player pays an amount of a currency; the winners share the pot.</summary>
    Currency,
    /// <summary>Every player puts in items; the winners share them.</summary>
    Items,
    /// <summary>The losers lose a limb (needs the injury system): a hand, an arm...</summary>
    Limb,
}

/// <summary>
/// A stake a card table offers (the table's designer lists them; the table's host picks one in the lobby): money, items
/// or a limb. Stakes are taken from every player when the game starts (held by the table) and paid out when it ends:
/// the winning team shares the pot; a limb stake cuts a limb off each loser. A player who leaves a game forfeits it.
/// </summary>
[Serializable]
public class CardStake
{
    [Tooltip("Name in the lobby (\"Friendly game\", \"10 gold\", \"Your hand\").")]
    public string label = "Just for fun";
    public CardStakeKind kind = CardStakeKind.None;

    [Header("Currency")]
    [Tooltip("Empty = the project's default currency.")]
    public CurrencyDefinition currency;
    [Tooltip("What each player puts in.")]
    [Min(1)] public int amount = 10;

    [Header("Items")]
    public ItemSO item;
    [Tooltip("How many each player puts in.")]
    [Min(1)] public int quantity = 1;

    [Header("Limb")]
    [Tooltip("Body parts (Injury Settings ids) a loser can lose: one of them is cut off (the first they still have, or a random one).")]
    public List<string> limbParts = new List<string> { "LeftHand", "RightHand" };
    [Tooltip("Pick the lost part at random among those the loser still has (off = the first in the list).")]
    public bool randomLimb = true;

    /// <summary>"10 gold each", "1 Healing Potion each", "Losers lose a hand".</summary>
    public string Describe()
    {
        switch (kind)
        {
            case CardStakeKind.Currency:
            {
                CurrencyDefinition c = Currency;
                return c != null ? $"{c.Format(amount)} each" : $"{amount} coins each";
            }
            case CardStakeKind.Items:
                return item != null ? $"{quantity} × {item.Name} each" : "Items (none set)";
            case CardStakeKind.Limb:
                return limbParts != null && limbParts.Count > 0 ? $"Losers lose a limb ({string.Join(" / ", limbParts)})" : "Losers lose a limb";
            default:
                return "Just for fun";
        }
    }

    /// <summary>The lobby's name for it (the label, else the description).</summary>
    public string Title => string.IsNullOrWhiteSpace(label) ? Describe() : $"{label} ({Describe()})";

    public CurrencyDefinition Currency => currency != null ? currency : CurrencyDefinition.Default;

    /// <summary>Can <paramref name="player"/> put this stake in (money, items, a limb left to lose)?</summary>
    public bool CanPay(CombatEntity player, out string reason)
    {
        reason = "";
        if (player == null)
            return true;
        switch (kind)
        {
            case CardStakeKind.Currency:
            {
                CurrencyWallet w = CurrencyWallet.For(player, false);
                if (w == null) { reason = "You have no money"; return false; }
                if (!w.CanAfford(Currency, amount)) { reason = $"You need {Currency.Format(amount)}"; return false; }
                return true;
            }
            case CardStakeKind.Items:
            {
                if (item == null) { reason = "The stake has no item"; return false; }
                InventoryManager inv = InventoryManager.For(player);
                if (inv == null || inv.CountItem(item) < quantity) { reason = $"You need {quantity} × {item.Name}"; return false; }
                return true;
            }
            case CardStakeKind.Limb:
            {
                if (!InjurySettings.IsEnabled) { reason = "Limb stakes need the injury system"; return false; }
                if (PickLimb(player, null) == null) { reason = "You have no limb left to bet"; return false; }
                return true;
            }
            default:
                return true;
        }
    }

    /// <summary>Takes the stake from <paramref name="player"/> (held by the table). False = nothing was taken.</summary>
    public bool Take(CombatEntity player)
    {
        switch (kind)
        {
            case CardStakeKind.Currency:
            {
                CurrencyWallet w = CurrencyWallet.For(player, false);
                return w != null && w.TrySpend(Currency, amount);
            }
            case CardStakeKind.Items:
            {
                InventoryManager inv = InventoryManager.For(player);
                if (inv == null || item == null || inv.CountItem(item) < quantity)
                    return false;
                int removed = inv.RemoveItems(item, quantity);
                if (removed < quantity)
                {
                    if (removed > 0) inv.AddItem(item, removed);
                    return false;
                }
                return true;
            }
            default:
                return true; // a limb is only taken from the losers, at the end
        }
    }

    /// <summary>
    /// Gives <paramref name="units"/> stakes' worth to <paramref name="player"/> (2 = twice what one player put in, plus
    /// <paramref name="extra"/> single coins / items). Items that do not fit fall at <paramref name="dropAt"/>.
    /// </summary>
    public void Give(CombatEntity player, int units, int extra, Vector3 dropAt)
    {
        if (player == null || (units <= 0 && extra <= 0))
            return;
        switch (kind)
        {
            case CardStakeKind.Currency:
            {
                int total = amount * Mathf.Max(0, units) + Mathf.Max(0, extra);
                CurrencyWallet w = CurrencyWallet.For(player, true);
                if (w != null && total > 0 && !w.TryAdd(Currency, total) && Currency.IsItemBacked)
                    ItemHandler.SpawnWorldItem(Currency.BackingItem, total, null, dropAt, player.gameObject);
                return;
            }
            case CardStakeKind.Items:
            {
                if (item == null)
                    return;
                int total = quantity * Mathf.Max(0, units) + Mathf.Max(0, extra);
                InventoryManager inv = InventoryManager.For(player);
                int left = inv != null ? inv.AddItem(item, total) : total;
                if (left > 0)
                    ItemHandler.SpawnWorldItem(item, left, null, dropAt, player.gameObject);
                return;
            }
        }
    }

    /// <summary>Cuts a limb off a loser. Returns the part's name (null = nothing to cut).</summary>
    public string TakeLimb(CombatEntity loser, System.Random rng)
    {
        InjuryPartState part = PickLimb(loser, rng);
        if (part == null)
            return null;
        InjuryController c = InjuryController.For(loser);
        return c != null && c.LoseLimb(part.Id) != null ? part.Name : null;
    }

    private InjuryPartState PickLimb(CombatEntity player, System.Random rng)
    {
        InjuryController c = InjuryController.For(player);
        if (c == null || limbParts == null)
            return null;
        var options = new List<InjuryPartState>();
        foreach (string id in limbParts)
        {
            InjuryPartState p = c.GetPart(id);
            if (p != null && !p.Lost && p.Def.limbLoss != null && p.Def.limbLoss.canBeLost && !options.Contains(p))
                options.Add(p);
        }
        if (options.Count == 0)
            return null;
        return randomLimb && rng != null ? options[rng.Next(options.Count)] : options[0];
    }

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        switch (kind)
        {
            case CardStakeKind.Items:
                if (item == null) errors.Add($"{owner}: stake '{label}' bets items but has no item.");
                break;
            case CardStakeKind.Limb:
                if (limbParts == null || limbParts.Count == 0) errors.Add($"{owner}: stake '{label}' bets a limb but lists no body part.");
                else if (!InjurySettings.IsEnabled) warnings.Add($"{owner}: stake '{label}' bets a limb, but the injury system is off (nobody can pick it).");
                else
                    foreach (string id in limbParts)
                    {
                        InjuryBodyPart p = InjurySettings.Instance.FindPart(id);
                        if (p == null) errors.Add($"{owner}: stake '{label}': '{id}' is not a body part of the Injury Settings.");
                        else if (p.limbLoss == null || !p.limbLoss.canBeLost) warnings.Add($"{owner}: stake '{label}': {p.Name} cannot be lost.");
                    }
                break;
            case CardStakeKind.Currency:
                if (currency == null && CurrencyDefinition.Default == null) warnings.Add($"{owner}: stake '{label}' has no currency and the project has no default one.");
                break;
        }
    }
}
