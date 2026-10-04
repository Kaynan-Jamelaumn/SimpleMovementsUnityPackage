using UnityEngine;

/// <summary>
/// A kind of money: gold, silver, gems, faction tokens... Wallets (<see cref="CurrencyWallet"/>) hold amounts of it and
/// merchants price their goods in it. By default it is a number on the wallet; with a <see cref="BackingItem"/> it is
/// that item in the inventory instead (coins that take space and weigh something).
/// <para>The project's default currency is the asset named "DefaultCurrency" in a Resources folder
/// (Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Default Currency), else a built-in "Gold".</para>
/// </summary>
[CreateAssetMenu(fileName = "Currency", menuName = "SimpleMovements/Economy/Currency", order = 0)]
public class CurrencyDefinition : ScriptableObject
{
    public const string ResourcesName = "DefaultCurrency";

    [Tooltip("Name shown in the UI (\"Gold\"). Empty = the asset's name.")]
    [SerializeField] private string displayName = "Gold";
    [Tooltip("Short suffix after amounts (\"g\" → 250 g). Empty = the display name.")]
    [SerializeField] private string symbol = "g";
    [Tooltip("Icon shown next to amounts (wallet, prices). Optional.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Colour of amounts in the UI.")]
    [SerializeField] private Color color = new Color(1f, 0.84f, 0.35f, 1f);
    [Tooltip("Write amounts with thousands separators (12,500).")]
    [SerializeField] private bool thousandsSeparator = true;

    [Header("Amounts")]
    [Tooltip("What a new wallet starts with.")]
    [SerializeField, Min(0)] private int startingAmount = 100;
    [Tooltip("Most a wallet can hold (sales that would go over it are refused).")]
    [SerializeField, Min(1)] private int maxAmount = 999999999;

    [Header("Coins As Items (optional)")]
    [Tooltip("Empty: the currency is a number on the wallet. Set: the currency IS this item in the inventory - the balance is how " +
             "many the player carries, paying removes them and earning adds them (it needs room in the bag).")]
    [SerializeField] private ItemSO backingItem;

    private static CurrencyDefinition defaultCurrency;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => defaultCurrency = null;

    /// <summary>Resources/DefaultCurrency, else a built-in "Gold" made in memory.</summary>
    public static CurrencyDefinition Default
    {
        get
        {
            if (defaultCurrency == null)
            {
                defaultCurrency = Resources.Load<CurrencyDefinition>(ResourcesName);
                if (defaultCurrency == null)
                {
                    defaultCurrency = CreateInstance<CurrencyDefinition>();
                    defaultCurrency.name = "Gold";
                    defaultCurrency.hideFlags = HideFlags.DontSave;
                }
            }
            return defaultCurrency;
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Symbol => string.IsNullOrWhiteSpace(symbol) ? DisplayName : symbol;
    public Sprite Icon => icon != null ? icon : backingItem != null ? backingItem.Icon : null;
    public Color Color => color;
    public int StartingAmount => startingAmount;
    public int MaxAmount => Mathf.Max(1, maxAmount);
    public ItemSO BackingItem => backingItem;
    /// <summary>Is the currency an item in the inventory (see <see cref="BackingItem"/>)?</summary>
    public bool IsItemBacked => backingItem != null;

    /// <summary>"12,500"</summary>
    public string FormatNumber(int amount) => thousandsSeparator ? amount.ToString("N0") : amount.ToString();

    /// <summary>"12,500 g"</summary>
    public string Format(int amount) => $"{FormatNumber(amount)} {Symbol}";

    private void OnValidate()
    {
        startingAmount = Mathf.Clamp(startingAmount, 0, Mathf.Max(1, maxAmount));
    }
}
