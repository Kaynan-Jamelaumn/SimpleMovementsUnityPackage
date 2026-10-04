using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The selected item in the shop window: icon, name, category, description, what it does, its value and price, stock
/// or the amount owned, durability, a quantity selector (stackable items), the total, and the Buy / Sell button with the
/// reason it is disabled ("Not enough gold", "No room in your bag"). Driven by the <see cref="MerchantWindow"/>; every
/// field is optional for a prefab of your own.
/// </summary>
[DisallowMultipleComponent]
public class MerchantDetailsPanel : MonoBehaviour
{
    [Tooltip("Shown while an item is selected.")]
    [SerializeField] private GameObject content;
    [Tooltip("Shown while nothing is selected.")]
    [SerializeField] private GameObject emptyState;
    [SerializeField] private TextMeshProUGUI emptyText;

    [Header("Item")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI statsText;
    [Tooltip("Value, price each, stock / owned, durability, weight.")]
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Quantity")]
    [SerializeField] private GameObject quantityRow;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button maxButton;
    [SerializeField] private TMP_InputField quantityField;

    [Header("Action")]
    [SerializeField] private TextMeshProUGUI totalText;
    [SerializeField] private TextMeshProUGUI reasonText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private Image actionBackground;

    private MerchantWindow owner;
    private bool wired;
    private readonly List<string> lines = new List<string>();
    private readonly StringBuilder sb = new StringBuilder();

    public void Configure(GameObject contentRoot, GameObject empty, TextMeshProUGUI emptyLabel, Image iconImage, TextMeshProUGUI itemName,
        TextMeshProUGUI category, TextMeshProUGUI description, TextMeshProUGUI stats, TextMeshProUGUI info, GameObject qtyRow, Button minus,
        Button plus, Button max, TMP_InputField field, TextMeshProUGUI total, TextMeshProUGUI reason, Button action, TextMeshProUGUI actionText, Image actionBg)
    {
        content = contentRoot;
        emptyState = empty;
        emptyText = emptyLabel;
        icon = iconImage;
        nameText = itemName;
        categoryText = category;
        descriptionText = description;
        statsText = stats;
        infoText = info;
        quantityRow = qtyRow;
        minusButton = minus;
        plusButton = plus;
        maxButton = max;
        quantityField = field;
        totalText = total;
        reasonText = reason;
        actionButton = action;
        actionLabel = actionText;
        actionBackground = actionBg;
    }

    /// <summary>Connects the buttons to the window (once).</summary>
    public void Wire(MerchantWindow window)
    {
        owner = window;
        if (wired)
            return;
        wired = true;
        if (minusButton != null) minusButton.onClick.AddListener(() => owner?.StepQuantity(-1));
        if (plusButton != null) plusButton.onClick.AddListener(() => owner?.StepQuantity(1));
        if (maxButton != null) maxButton.onClick.AddListener(() => owner?.SetMaxQuantity());
        if (actionButton != null) actionButton.onClick.AddListener(() => owner?.ConfirmSelected());
        if (quantityField != null)
        {
            quantityField.contentType = TMP_InputField.ContentType.IntegerNumber;
            quantityField.onEndEdit.AddListener(text =>
            {
                if (owner != null && int.TryParse(text, out int n))
                    owner.SetQuantity(n);
                else
                    owner?.RefreshDetails();
            });
        }
    }

    /// <summary>Nothing selected.</summary>
    public void ShowEmpty(string message)
    {
        if (content != null) content.SetActive(false);
        if (emptyState != null) emptyState.SetActive(true);
        if (emptyText != null) emptyText.text = message ?? "";
    }

    /// <summary>Shows the selected item with the quantity, the quote for it and whether the action is possible.</summary>
    public void Show(MerchantDisplayEntry e, MerchantTransactionResult quote, int quantity, int maxQuantity, bool busy,
        CurrencyDefinition currency, MerchantUISkin skin)
    {
        if (e == null || e.Item == null)
        {
            ShowEmpty("");
            return;
        }
        if (content != null) content.SetActive(true);
        if (emptyState != null) emptyState.SetActive(false);
        ItemSO item = e.Item;
        bool buying = e.Mode == MerchantTransactionType.Buy;

        if (icon != null)
        {
            icon.sprite = item.Icon;
            icon.enabled = item.Icon != null;
        }
        if (nameText != null) nameText.text = item.Name;
        if (categoryText != null)
        {
            categoryText.text = e.Category.Path;
            if (e.Category.Leaf != null) categoryText.color = e.Category.Leaf.Color;
        }
        if (descriptionText != null)
        {
            descriptionText.text = item.Description ?? "";
            descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(item.Description));
        }
        if (statsText != null)
        {
            lines.Clear();
            item.AppendTooltip(lines);
            statsText.text = string.Join("\n", lines);
            statsText.gameObject.SetActive(lines.Count > 0);
        }
        if (infoText != null)
        {
            sb.Clear();
            string Money(int v) => currency != null ? currency.Format(v) : v.ToString();
            sb.Append($"Value  {item.Price:0.##}\n");
            sb.Append(buying ? "Price  " : "You get  ").Append(Money(e.UnitPrice)).Append(buying || e.Quantity <= 1 ? "" : " each").Append('\n');
            if (buying)
                sb.Append(e.Unlimited ? "Stock  plenty" : $"Stock  {e.Quantity}").Append('\n');
            else
                sb.Append($"You have  {e.Quantity}{(e.FromHotbar ? "  (hotbar)" : "")}").Append('\n');
            if (!buying && e.Stack != null && item.MaxDurability > 1)
                sb.Append($"Durability  {Mathf.RoundToInt(e.Stack.durability)} / {item.MaxDurability}\n");
            else if (buying && item.MaxDurability > 1)
                sb.Append($"Durability  {item.MaxDurability}\n");
            sb.Append($"Weight  {item.Weight:0.##}");
            if (item.GridSize != Vector2Int.one)
                sb.Append($"   ·   Size  {item.GridSize.x}×{item.GridSize.y}");
            infoText.text = sb.ToString();
        }

        bool stackable = maxQuantity > 1;
        if (quantityRow != null) quantityRow.SetActive(stackable);
        if (quantityField != null && !quantityField.isFocused)
            quantityField.SetTextWithoutNotify(quantity.ToString());
        if (minusButton != null) minusButton.interactable = quantity > 1 && !busy;
        if (plusButton != null) plusButton.interactable = quantity < maxQuantity && !busy;
        if (maxButton != null) maxButton.interactable = quantity < maxQuantity && !busy;

        int total = quote.TotalPrice > 0 ? quote.TotalPrice : e.UnitPrice * quantity;
        if (totalText != null)
        {
            totalText.text = (buying ? "Total  " : "You get  ") + (currency != null ? currency.Format(total) : total.ToString());
            totalText.color = buying && quote.Status == MerchantTransactionStatus.NotEnoughCurrency ? skin.unaffordableColor : skin.priceColor;
        }
        string reason = quote.Succeeded ? "" : quote.Message;
        if (reasonText != null)
        {
            reasonText.text = reason;
            reasonText.color = skin.errorColor;
            reasonText.gameObject.SetActive(!string.IsNullOrEmpty(reason));
        }
        if (actionButton != null) actionButton.interactable = quote.Succeeded && !busy;
        if (actionLabel != null) actionLabel.text = busy ? "…" : buying ? (quantity > 1 ? $"Buy {quantity}" : "Buy") : (quantity > 1 ? $"Sell {quantity}" : "Sell");
        if (actionBackground != null) actionBackground.color = buying ? skin.buyButtonColor : skin.sellButtonColor;
    }
}
