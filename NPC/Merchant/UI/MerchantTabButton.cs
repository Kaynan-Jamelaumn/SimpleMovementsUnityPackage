using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// A tab of the shop window: Buy / Sell, a category, a subcategory, or a filter toggle. Generated when the skin has no
/// tab prefab; a prefab of your own needs this component (Button and Label at least).
/// </summary>
[DisallowMultipleComponent]
public class MerchantTabButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("Optional icon (category icons).")]
    [SerializeField] private Image icon;
    [SerializeField] private Image background;
    [Tooltip("Optional object shown while the tab is selected (an underline...).")]
    [SerializeField] private GameObject selectedIndicator;
    [Tooltip("Tint the background with the skin's tab colours.")]
    [SerializeField] private bool tintBackground = true;

    public Button Button => button;
    public bool IsSelected { get; private set; }
    /// <summary>What the tab stands for (a category, null for All).</summary>
    public object Tag { get; set; }

    public void Configure(Button b, TextMeshProUGUI text, Image iconImage, Image bg)
    {
        button = b;
        label = text;
        icon = iconImage;
        background = bg;
    }

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    /// <summary>Text, icon and click action.</summary>
    public void Setup(string text, Sprite sprite, UnityAction onClick)
    {
        if (button == null) Awake();
        SetText(text);
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(onClick);
        }
    }

    public void SetText(string text)
    {
        if (label != null && label.text != text)
            label.text = text ?? "";
    }

    public void SetSelected(bool selected, MerchantUISkin skin)
    {
        IsSelected = selected;
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
        if (background != null && tintBackground && skin != null)
            background.color = selected ? skin.tabSelectedColor : skin.tabColor;
        if (label != null && skin != null)
            label.color = selected ? skin.textColor : skin.mutedTextColor;
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }
}
