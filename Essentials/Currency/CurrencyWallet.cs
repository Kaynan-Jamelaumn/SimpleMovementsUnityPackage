using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An amount of one currency held by a wallet.</summary>
[Serializable]
public class CurrencyBalance
{
    public CurrencyDefinition currency;
    [Min(0)] public int amount;

    public CurrencyBalance() { }

    public CurrencyBalance(CurrencyDefinition currency, int amount)
    {
        this.currency = currency;
        this.amount = amount;
    }
}

/// <summary>
/// The player's money: one balance per currency (gold by default). Merchants, rewards and your scripts change it only
/// through <see cref="TrySpend"/> / <see cref="TryAdd"/>, which move the whole amount or nothing, and every change raises
/// <see cref="BalanceChanged"/> (the shop window and any HUD follow it). A currency with a Backing Item is that item in the
/// player's inventory instead of a number here.
/// <para>Added to the player automatically the first time something needs it (<see cref="For"/>); add it yourself to
/// set starting balances. For a server-authoritative game, keep these calls on the server and replicate the balances.</para>
/// </summary>
[DisallowMultipleComponent]
public class CurrencyWallet : MonoBehaviour, ICurrencyAccount
{
    [Tooltip("The currency used when none is named (merchants without a currency of their own). Empty = the project's default " +
             "currency (Resources/DefaultCurrency, else a built-in Gold).")]
    [SerializeField] private CurrencyDefinition defaultCurrency;
    [Tooltip("Balances. A currency missing from the list starts at its Starting Amount the first time it is used. Currencies " +
             "with a Backing Item are counted from the inventory instead (their amount here is ignored).")]
    [SerializeField] private List<CurrencyBalance> balances = new List<CurrencyBalance>();
    [Tooltip("The inventory used by coin-item currencies. Empty = the player's inventory, found automatically.")]
    [SerializeField] private InventoryManager inventory;
    [Tooltip("Log every change of balance to the Console.")]
    [SerializeField] private bool logChanges;

    /// <summary>(currency, old balance, new balance) after any change.</summary>
    public event Action<CurrencyDefinition, int, int> BalanceChanged;
    /// <summary>Any wallet's balance changed: (wallet, currency, old, new).</summary>
    public static event Action<CurrencyWallet, CurrencyDefinition, int, int> AnyBalanceChanged;

    private readonly Dictionary<CurrencyDefinition, int> itemBalances = new Dictionary<CurrencyDefinition, int>();
    private InventoryManager subscribedInventory;

    public CurrencyDefinition DefaultCurrency => defaultCurrency != null ? defaultCurrency : CurrencyDefinition.Default;
    public IReadOnlyList<CurrencyBalance> Balances => balances;

    /// <summary>The inventory holding coin items (null when the player has none).</summary>
    public InventoryManager Inventory
    {
        get
        {
            if (inventory == null)
                inventory = InventoryManager.For(this);
            return inventory;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyBalanceChanged = null;

    /// <summary>
    /// The wallet of the player owning <paramref name="anyOnPlayer"/>: on the player, else next to the player's inventory,
    /// else (with <paramref name="create"/>) a new one added to the player.
    /// </summary>
    public static CurrencyWallet For(Component anyOnPlayer, bool create = true)
    {
        if (anyOnPlayer == null)
            return null;
        Transform root = AbilitiesStateMachine.PlayerRoot(anyOnPlayer);
        CurrencyWallet w = root.GetComponentInChildren<CurrencyWallet>(true);
        if (w == null)
            w = anyOnPlayer.GetComponentInParent<CurrencyWallet>();
        if (w == null)
        {
            InventoryManager inv = InventoryManager.For(anyOnPlayer);
            if (inv != null)
            {
                w = inv.GetComponentInChildren<CurrencyWallet>(true);
                if (w == null)
                    w = inv.GetComponentInParent<CurrencyWallet>();
            }
        }
        if (w == null && create)
            w = root.gameObject.AddComponent<CurrencyWallet>();
        return w;
    }

    private void Awake()
    {
        if (balances == null)
            balances = new List<CurrencyBalance>();
        Entry(DefaultCurrency); // the default currency starts at its starting amount
    }

    private void OnEnable() => SubscribeInventory();

    private void Start() => SubscribeInventory();

    private void OnDisable()
    {
        if (subscribedInventory != null)
            subscribedInventory.InventoryChanged -= OnInventoryChanged;
        subscribedInventory = null;
    }

    private void SubscribeInventory()
    {
        InventoryManager inv = Inventory;
        if (inv == null || inv == subscribedInventory)
            return;
        if (subscribedInventory != null)
            subscribedInventory.InventoryChanged -= OnInventoryChanged;
        subscribedInventory = inv;
        inv.InventoryChanged += OnInventoryChanged;
        OnInventoryChanged();
    }

    // ------------------------------------------------------------------ queries
    public int GetBalance(CurrencyDefinition currency)
    {
        currency = Resolve(currency);
        if (currency.IsItemBacked)
            return Inventory != null ? Inventory.GetItemCount(currency.BackingItem) : 0;
        return Entry(currency).amount;
    }

    /// <summary>The balance of the default currency.</summary>
    public int Balance => GetBalance(null);

    public bool CanAfford(CurrencyDefinition currency, int amount) => amount <= 0 || GetBalance(currency) >= amount;

    public bool CanReceive(CurrencyDefinition currency, int amount)
    {
        if (amount <= 0)
            return true;
        currency = Resolve(currency);
        if (currency.IsItemBacked)
            return Inventory != null && Inventory.HasEnoughSpace(currency.BackingItem, amount);
        return (long)Entry(currency).amount + amount <= currency.MaxAmount;
    }

    // ------------------------------------------------------------------ changes
    public bool TrySpend(CurrencyDefinition currency, int amount)
    {
        if (amount < 0)
            return false;
        if (amount == 0)
            return true;
        currency = Resolve(currency);
        if (!CanAfford(currency, amount))
            return false;
        if (currency.IsItemBacked)
        {
            int removed = Inventory.RemoveItems(currency.BackingItem, amount);
            if (removed != amount)
            {
                if (removed > 0)
                    Inventory.AddItem(currency.BackingItem, removed); // all or nothing
                return false;
            }
            OnInventoryChanged();
            return true;
        }
        CurrencyBalance e = Entry(currency);
        int old = e.amount;
        e.amount -= amount;
        Raise(currency, old, e.amount);
        return true;
    }

    public bool TryAdd(CurrencyDefinition currency, int amount)
    {
        if (amount < 0)
            return false;
        if (amount == 0)
            return true;
        currency = Resolve(currency);
        if (!CanReceive(currency, amount))
            return false;
        if (currency.IsItemBacked)
        {
            int left = Inventory.AddItem(currency.BackingItem, amount);
            if (left > 0)
            {
                Inventory.RemoveItems(currency.BackingItem, amount - left); // all or nothing
                return false;
            }
            OnInventoryChanged();
            return true;
        }
        CurrencyBalance e = Entry(currency);
        int old = e.amount;
        e.amount += amount;
        Raise(currency, old, e.amount);
        return true;
    }

    /// <summary>Sets a balance (loading a save, debug tools). Coin-item currencies cannot be set this way.</summary>
    public void SetBalance(CurrencyDefinition currency, int amount)
    {
        currency = Resolve(currency);
        if (currency.IsItemBacked)
        {
            Debug.LogWarning($"[Currency] {currency.DisplayName} is an item in the inventory: add or remove the item instead of setting the balance.", this);
            return;
        }
        CurrencyBalance e = Entry(currency);
        int old = e.amount;
        e.amount = Mathf.Clamp(amount, 0, currency.MaxAmount);
        if (old != e.amount)
            Raise(currency, old, e.amount);
    }

    // ------------------------------------------------------------------ internals
    private CurrencyDefinition Resolve(CurrencyDefinition currency) => currency != null ? currency : DefaultCurrency;

    /// <summary>The balance entry of a number currency (created at its starting amount).</summary>
    private CurrencyBalance Entry(CurrencyDefinition currency)
    {
        for (int i = 0; i < balances.Count; i++)
            if (balances[i] != null && balances[i].currency == currency)
                return balances[i];
        var e = new CurrencyBalance(currency, currency.IsItemBacked ? 0 : currency.StartingAmount);
        balances.Add(e);
        return e;
    }

    private void Raise(CurrencyDefinition currency, int oldAmount, int newAmount)
    {
        if (logChanges)
            Debug.Log($"[Currency] {name}: {currency.DisplayName} {oldAmount} → {newAmount} ({newAmount - oldAmount:+#;-#;0}).", this);
        BalanceChanged?.Invoke(currency, oldAmount, newAmount);
        AnyBalanceChanged?.Invoke(this, currency, oldAmount, newAmount);
    }

    /// <summary>Coin-item currencies change whenever the inventory does: report the difference.</summary>
    private void OnInventoryChanged()
    {
        if (Inventory == null)
            return;
        CheckItemCurrency(DefaultCurrency);
        for (int i = 0; i < balances.Count; i++)
            if (balances[i] != null && balances[i].currency != null)
                CheckItemCurrency(balances[i].currency);
    }

    private void CheckItemCurrency(CurrencyDefinition currency)
    {
        if (currency == null || !currency.IsItemBacked)
            return;
        int now = Inventory.GetItemCount(currency.BackingItem);
        if (!itemBalances.TryGetValue(currency, out int before))
        {
            itemBalances[currency] = now;
            return;
        }
        if (before == now)
            return;
        itemBalances[currency] = now;
        Raise(currency, before, now);
    }

    private void OnValidate()
    {
        if (balances == null)
            return;
        foreach (CurrencyBalance b in balances)
            if (b != null && b.currency != null)
                b.amount = Mathf.Clamp(b.amount, 0, b.currency.MaxAmount);
    }
}
