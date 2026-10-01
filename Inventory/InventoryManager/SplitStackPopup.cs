using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Right-click on a stack: choose how many to take out - half, or any amount with the slider, the -/+ buttons or by
/// typing it - and split them into a free slot. Built from code by the <see cref="InventoryManager"/> when none is
/// assigned. Enter confirms, Escape (or a click outside) cancels.
/// </summary>
[DisallowMultipleComponent]
public class SplitStackPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_InputField amountField;
    [SerializeField] private TextMeshProUGUI keepText;
    [SerializeField] private TextMeshProUGUI messageText;

    private InventoryItem item;
    private Func<InventoryItem, int, bool> onSplit;
    private bool updating;
    private int openedFrame;

    public bool IsOpen => gameObject.activeSelf && item != null;

    /// <summary>Builds the popup under <paramref name="parent"/> (inactive until opened).</summary>
    public static SplitStackPopup Create(Transform parent)
    {
        Image bg = InventoryUIFactory.Panel("SplitStackPopup", parent, InventoryUIFactory.PanelColor);
        var rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(300f, 0f);
        InventoryUIFactory.Vertical(bg.gameObject, 12, 8f, true);
        var popup = bg.gameObject.AddComponent<SplitStackPopup>();

        popup.title = InventoryUIFactory.Text("Title", rt, "Split", 18f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);

        RectTransform row = InventoryUIFactory.Rect("Amount", rt);
        InventoryUIFactory.Horizontal(row.gameObject, 6f);
        InventoryUIFactory.Size(row, -1f, 32f);
        InventoryUIFactory.Button("Minus", row, "−", () => popup.Step(-1), 32f);
        popup.amountField = InventoryUIFactory.NumberField("Field", row, 70f);
        InventoryUIFactory.Button("Plus", row, "+", () => popup.Step(1), 32f);
        InventoryUIFactory.Button("Half", row, "Half", popup.SetHalf, 70f);

        popup.slider = InventoryUIFactory.Slider("Slider", rt);
        popup.keepText = InventoryUIFactory.Text("Keep", rt, "", 14f, InventoryUIFactory.MutedColor);
        popup.messageText = InventoryUIFactory.Text("Message", rt, "", 14f, new Color(1f, 0.55f, 0.45f));

        RectTransform buttons = InventoryUIFactory.Rect("Buttons", rt);
        InventoryUIFactory.Horizontal(buttons.gameObject, 8f);
        InventoryUIFactory.Size(buttons, -1f, 32f);
        InventoryUIFactory.Button("Split", buttons, "Split", popup.Confirm);
        InventoryUIFactory.Button("Cancel", buttons, "Cancel", popup.Close);

        popup.slider.onValueChanged.AddListener(v => popup.SetAmount(Mathf.RoundToInt(v), fromSlider: true));
        popup.amountField.onValueChanged.AddListener(s => { if (int.TryParse(s, out int n)) popup.SetAmount(n, fromField: true); });
        popup.amountField.onEndEdit.AddListener(_ => popup.SetAmount(popup.Amount));
        bg.gameObject.SetActive(false);
        return popup;
    }

    public int Amount { get; private set; }

    /// <summary>
    /// Opens the popup for <paramref name="stack"/> near <paramref name="screenPosition"/>. <paramref name="split"/>
    /// performs the split (returns false when it could not, e.g. no free slot).
    /// </summary>
    public void Open(InventoryItem stack, Vector2 screenPosition, Func<InventoryItem, int, bool> split)
    {
        if (stack == null || stack.stackCurrent < 2)
            return;
        item = stack;
        onSplit = split;
        openedFrame = Time.frameCount;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        title.text = $"Split {(stack.itemScriptableObject != null ? stack.itemScriptableObject.Name : "stack")} ({stack.stackCurrent})";
        messageText.text = "";
        slider.minValue = 1;
        slider.maxValue = stack.stackCurrent - 1;
        SetAmount(Mathf.Max(1, stack.stackCurrent / 2));

        var rt = (RectTransform)transform;
        rt.position = screenPosition + new Vector2(12f, -12f);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        InventoryUIFactory.ClampToCanvas(rt, GetComponentInParent<Canvas>());
        amountField.Select();
        amountField.ActivateInputField();
    }

    public void Close()
    {
        item = null;
        onSplit = null;
        gameObject.SetActive(false);
    }

    private void SetHalf()
    {
        if (item != null) SetAmount(Mathf.Max(1, item.stackCurrent / 2));
    }

    private void Step(int delta) => SetAmount(Amount + delta);

    private void SetAmount(int amount, bool fromSlider = false, bool fromField = false)
    {
        if (updating || item == null)
            return;
        updating = true;
        Amount = Mathf.Clamp(amount, 1, Mathf.Max(1, item.stackCurrent - 1));
        if (!fromSlider) slider.SetValueWithoutNotify(Amount);
        if (!fromField) amountField.SetTextWithoutNotify(Amount.ToString());
        keepText.text = $"Take {Amount} · keep {item.stackCurrent - Amount}";
        updating = false;
    }

    private void Confirm()
    {
        if (item == null)
        {
            Close();
            return;
        }
        if (int.TryParse(amountField.text, out int typed))
            SetAmount(typed);
        if (onSplit != null && onSplit(item, Amount))
            Close();
        else
            messageText.text = "No free slot for the new stack.";
    }

    private void Update()
    {
        if (item == null || item.stackCurrent < 2)
        {
            Close(); // the stack was used, moved away or destroyed
            return;
        }
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Confirm(); return; }
        }
        // A click outside closes it (not the right click that opened it).
        Mouse mouse = Mouse.current;
        if (mouse != null && Time.frameCount != openedFrame &&
            (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) &&
            !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, mouse.position.ReadValue(), null))
        {
            Close();
        }
    }
}
