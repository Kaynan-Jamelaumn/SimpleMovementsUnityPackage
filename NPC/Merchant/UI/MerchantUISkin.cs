using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The look of a shop window, as one asset shared by any number of merchants: your own prefabs (the whole window, or
/// just its item slots, grid items, tabs and buttons), sprites, font, colours, sizes and sounds. Every field is optional:
/// what is empty is generated with the default look, so a skin can change only the font, or only the slot prefab.
/// </summary>
[CreateAssetMenu(fileName = "Merchant UI Skin", menuName = "SimpleMovements/NPC/Merchant UI Skin", order = 2)]
public class MerchantUISkin : ScriptableObject
{
    [Header("Prefabs (optional)")]
    [Tooltip("Your complete shop window (a prefab with a MerchantWindow whose fields you wired). Empty = the window is built from the parts below.")]
    public MerchantWindow windowPrefab;
    [Tooltip("An item in the slot layout (needs a MerchantItemEntryUI). Empty = generated.")]
    public MerchantItemEntryUI slotEntryPrefab;
    [Tooltip("An item in the grid layout, stretched over the cells it takes (needs a MerchantItemEntryUI). Empty = generated.")]
    public MerchantItemEntryUI gridEntryPrefab;
    [Tooltip("Category, subcategory, Buy / Sell and filter tabs (needs a MerchantTabButton). Empty = generated.")]
    public MerchantTabButton tabPrefab;
    [Tooltip("Buttons of the generated window (Buy, Sell, −, +, Max, Close...). Needs a Button with a TextMeshPro label child. Empty = generated.")]
    public Button buttonPrefab;

    [Header("Sprites (optional)")]
    [Tooltip("Background of the window.")]
    public Sprite windowBackground;
    [Tooltip("Background of the inner panels (item area, details).")]
    public Sprite panelBackground;
    [Tooltip("Background of an item slot / grid cell.")]
    public Sprite slotBackground;
    [Tooltip("Frame drawn over the selected item.")]
    public Sprite selectedFrame;
    [Tooltip("Background of buttons.")]
    public Sprite buttonBackground;
    [Tooltip("Background of tabs.")]
    public Sprite tabBackground;
    [Tooltip("Icon next to amounts (empty = the currency's icon).")]
    public Sprite currencyIcon;
    [Tooltip("How the background sprites are drawn (Sliced for 9-sliced frames).")]
    public Image.Type backgroundImageType = Image.Type.Sliced;

    [Header("Font")]
    [Tooltip("Font of every text of the generated window (empty = TextMeshPro's default).")]
    public TMP_FontAsset font;
    [Tooltip("Scales every font size of the generated window.")]
    [Min(0.5f)] public float fontScale = 1f;

    [Header("Colours")]
    public Color windowColor = new Color(0.06f, 0.07f, 0.09f, 0.97f);
    public Color panelColor = new Color(1f, 1f, 1f, 0.04f);
    public Color slotColor = new Color(1f, 1f, 1f, 0.07f);
    [Tooltip("Behind items in the grid layout (shows the cells they take).")]
    public Color gridItemColor = new Color(0.35f, 0.55f, 0.85f, 0.22f);
    public Color accentColor = new Color(0.35f, 0.6f, 1f, 1f);
    public Color textColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    public Color mutedTextColor = new Color(0.68f, 0.7f, 0.74f, 1f);
    [Tooltip("Prices the player can pay.")]
    public Color priceColor = new Color(1f, 0.84f, 0.35f, 1f);
    [Tooltip("Prices the player cannot pay.")]
    public Color unaffordableColor = new Color(1f, 0.42f, 0.38f, 1f);
    public Color selectedColor = new Color(1f, 0.85f, 0.35f, 1f);
    public Color buttonColor = new Color(1f, 1f, 1f, 0.14f);
    public Color buyButtonColor = new Color(0.25f, 0.55f, 0.3f, 1f);
    public Color sellButtonColor = new Color(0.6f, 0.42f, 0.18f, 1f);
    public Color tabColor = new Color(1f, 1f, 1f, 0.08f);
    public Color tabSelectedColor = new Color(0.35f, 0.6f, 1f, 0.55f);
    public Color successColor = new Color(0.55f, 0.9f, 0.55f, 1f);
    public Color errorColor = new Color(1f, 0.5f, 0.45f, 1f);
    [Tooltip("Items that cannot be bought / sold now, and items filtered out of a mirrored inventory.")]
    [Range(0f, 1f)] public float dimmedAlpha = 0.35f;

    [Header("Sizes")]
    [Tooltip("Size of the window (it shrinks to fit small screens).")]
    public Vector2 windowSize = new Vector2(1180f, 720f);
    [Tooltip("Size of an item slot in the slot layout.")]
    [Min(32f)] public float slotSize = 72f;
    [Tooltip("Size of a grid cell (0 = the player's grid cell size, made smaller when the grid does not fit).")]
    [Min(0f)] public float gridCellSize;
    [Tooltip("Gap between slots / cells.")]
    [Min(0f)] public float spacing = 4f;
    [Tooltip("Width of the item details panel.")]
    [Min(200f)] public float detailsWidth = 340f;

    [Header("Selling")]
    [Tooltip("The Sell tab shows the player's bag laid out like their inventory (the same slots, or the same grid with items where they are), " +
             "with what cannot be sold dimmed. Off = a list of the sellable items only.")]
    public bool mirrorInventoryWhenSelling = true;

    [Header("Sounds")]
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioClip buySound;
    public AudioClip sellSound;
    public AudioClip errorSound;
    public AudioClip clickSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    private static MerchantUISkin defaultSkin;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => defaultSkin = null;

    /// <summary>The default look (used by merchants without a skin).</summary>
    public static MerchantUISkin Default
    {
        get
        {
            if (defaultSkin == null)
            {
                defaultSkin = CreateInstance<MerchantUISkin>();
                defaultSkin.name = "Default Merchant Skin";
                defaultSkin.hideFlags = HideFlags.DontSave;
            }
            return defaultSkin;
        }
    }

    public float FontSize(float size) => size * Mathf.Max(0.5f, fontScale);

    public void Validate(List<string> errors, List<string> warnings)
    {
        if (windowPrefab != null && windowPrefab.GetComponentInChildren<RectTransform>(true) == null)
            errors.Add($"Skin '{name}': the Window Prefab has no RectTransform (it must be a UI object).");
        if (slotEntryPrefab != null && slotEntryPrefab.GetComponent<RectTransform>() == null)
            errors.Add($"Skin '{name}': the Slot Entry Prefab is not a UI object.");
        if (gridEntryPrefab != null && gridEntryPrefab.GetComponent<RectTransform>() == null)
            errors.Add($"Skin '{name}': the Grid Entry Prefab is not a UI object.");
        if (buttonPrefab != null && buttonPrefab.GetComponentInChildren<TextMeshProUGUI>(true) == null)
            warnings.Add($"Skin '{name}': the Button Prefab has no TextMeshPro label child: its buttons will show no text.");
        if (windowSize.x < 700f || windowSize.y < 420f)
            warnings.Add($"Skin '{name}': the window is very small ({windowSize.x:0}×{windowSize.y:0}); 1000×640 or more is recommended.");
    }
}
