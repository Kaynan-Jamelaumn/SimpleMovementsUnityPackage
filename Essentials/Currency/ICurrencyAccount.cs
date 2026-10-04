using System;

/// <summary>
/// Something that holds money (a player's <see cref="CurrencyWallet"/>, a shared party purse, a server-side account).
/// Every change goes through <see cref="TrySpend"/> / <see cref="TryAdd"/>, which either apply the whole amount or
/// nothing, so a caller can always tell whether money moved. A null currency means the account's default currency.
/// </summary>
public interface ICurrencyAccount
{
    /// <summary>Raised after a balance changed: (currency, old balance, new balance).</summary>
    event Action<CurrencyDefinition, int, int> BalanceChanged;

    CurrencyDefinition DefaultCurrency { get; }

    int GetBalance(CurrencyDefinition currency);

    bool CanAfford(CurrencyDefinition currency, int amount);

    /// <summary>Could <paramref name="amount"/> be received (wallet limit, room for coin items)?</summary>
    bool CanReceive(CurrencyDefinition currency, int amount);

    /// <summary>Takes the whole amount, or nothing (false).</summary>
    bool TrySpend(CurrencyDefinition currency, int amount);

    /// <summary>Gives the whole amount, or nothing (false).</summary>
    bool TryAdd(CurrencyDefinition currency, int amount);
}
