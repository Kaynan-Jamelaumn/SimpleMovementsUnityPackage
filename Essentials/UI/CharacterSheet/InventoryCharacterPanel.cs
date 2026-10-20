using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The character panel beside the open inventory: name, class, race, level, resources, combat stats, traits and
/// abilities, each with its description (hover a row for every source). It is a <see cref="CharacterSheetMenu"/>
/// that refreshes while open (equipment, traits and stat changes show at once). While the project's injury system is
/// on, a Health tab shows the body's condition, injuries and missing limbs, where injuries are treated
/// (<see cref="InjuryHealthPanel"/>). Created by the <see cref="InventoryManager"/> (Show Character Panel); the header
/// button folds it.
/// </summary>
public class InventoryCharacterPanel : MonoBehaviour
{
    public enum Side { Auto, Left, Right }

    [SerializeField] private InventoryManager inventory;
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform body;
    [SerializeField] private CharacterSheetMenu sheet;
    [SerializeField] private TextMeshProUGUI foldLabel;
    [SerializeField] private RectTransform healthPage;
    [SerializeField] private InjuryHealthPanel health;
    [SerializeField] private Button statsTab;
    [SerializeField] private Button healthTab;
    [SerializeField] private Side side = Side.Auto;
    [SerializeField, Min(280f)] private float width = 430f;

    private bool folded;
    private bool shown;
    private bool showingHealth;
    private bool healthAvailable;
    private bool tabsChecked;
    private RectTransform inventoryRect;

    /// <summary>Builds the panel under <paramref name="container"/> (the inventory's canvas area), hidden.</summary>
    public static InventoryCharacterPanel Create(Transform container, InventoryManager inventory, RectTransform inventoryPanel, Side side)
    {
        RectTransform host = InventoryUIFactory.Rect("Character Panel", container);
        InventoryUIFactory.Stretch(host);
        var p = host.gameObject.AddComponent<InventoryCharacterPanel>();
        p.inventory = inventory;
        p.inventoryRect = inventoryPanel;
        p.side = side;

        Image bg = UITheme.Panel("Panel", host, new Color(0.055f, 0.065f, 0.09f, 0.97f), UITheme.RadiusLarge);
        p.panel = bg.rectTransform;
        var shadow = bg.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -4f);

        RectTransform header = UITheme.Area("Header", bg.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -58f), new Vector2(-12f, -10f));
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(header.gameObject, 8f);
        h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        TextMeshProUGUI title = UITheme.Label(header, "CHARACTER", 15f, UITheme.Accent, TextAlignmentOptions.Left, FontStyles.Bold);
        title.characterSpacing = 5f;
        InventoryUIFactory.Size(title, -1f, 40f, 1f);
        p.statsTab = UITheme.Button("Stats Tab", header, "Stats", () => p.ShowPage(false), UITheme.ButtonStyle.Tab, 78f, 36f);
        p.healthTab = UITheme.Button("Health Tab", header, "Health", () => p.ShowPage(true), UITheme.ButtonStyle.Tab, 86f, 36f);
        p.statsTab.gameObject.SetActive(false);
        p.healthTab.gameObject.SetActive(false);
        Button fold = UITheme.Button("Fold", header, "Hide", null, UITheme.ButtonStyle.Ghost, 80f, 36f);
        p.foldLabel = fold.GetComponentInChildren<TextMeshProUGUI>(true);
        if (p.foldLabel != null) p.foldLabel.fontSize = 14f;
        fold.onClick.AddListener(p.ToggleFold);

        RectTransform content = UITheme.Area("Body", bg.transform, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -60f));
        p.body = content;
        VerticalLayoutGroup v = InventoryUIFactory.Vertical(content.gameObject, 0, 0f, false);
        v.childForceExpandHeight = true;
        content.gameObject.SetActive(false); // the sheet wakes up once configured
        p.sheet = content.gameObject.AddComponent<CharacterSheetMenu>();
        RectTransform list = UITheme.ScrollList("Sheet", content, 0f, out _, new Color(0f, 0f, 0f, 0f), 6, 4f);
        GameMenuFactory.HoverPanel(container, out RectTransform hoverRt, out TextMeshProUGUI hoverText);
        p.sheet.Configure(list, hoverRt, hoverText, true, 1f);
        if (inventory != null && inventory.PlayerStatus != null)
            p.sheet.SetPlayer(inventory.PlayerStatus);
        content.gameObject.SetActive(true);

        // The Health page (injuries), next to the sheet; shown by the Health tab while the injury system is on.
        p.healthPage = UITheme.Area("Health", bg.transform, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -62f));
        p.health = InjuryHealthPanel.Create(p.healthPage, inventory, container);
        p.healthPage.gameObject.SetActive(false);

        bg.gameObject.SetActive(false);
        return p;
    }

    private void LateUpdate()
    {
        bool open = inventory != null && inventory.IsInventoryOpened && inventory.isActiveAndEnabled;
        if (open)
            UpdateTabs();
        if (open == shown)
            return;
        shown = open;
        if (panel == null)
            return;
        if (open)
        {
            Place();
            panel.SetAsLastSibling();
        }
        panel.gameObject.SetActive(open);
    }

    /// <summary>Puts the panel on the side away from the inventory window, full height (above the hotbar).</summary>
    private void Place()
    {
        bool left = side == Side.Left;
        if (side == Side.Auto)
        {
            left = true;
            var area = transform as RectTransform;
            if (inventoryRect != null && area != null)
            {
                Vector3 c = area.InverseTransformPoint(inventoryRect.TransformPoint(inventoryRect.rect.center));
                left = c.x >= area.rect.center.x - area.rect.width * 0.1f; // the inventory is in the middle or on the right
            }
        }
        float w = folded ? 220f : width;
        float bottom = folded ? -1f : 170f;
        if (left)
        {
            panel.anchorMin = new Vector2(0f, folded ? 1f : 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.offsetMin = new Vector2(24f, folded ? -92f : bottom);
            panel.offsetMax = new Vector2(24f + w, -24f);
        }
        else
        {
            panel.anchorMin = new Vector2(1f, folded ? 1f : 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.offsetMin = new Vector2(-24f - w, folded ? -92f : bottom);
            panel.offsetMax = new Vector2(-24f, -24f);
        }
        ApplyPage();
    }

    /// <summary>Shows the Health tab only while the injury system is on (and this player has injuries tracked).</summary>
    private void UpdateTabs()
    {
        bool available = InjuryHealthPanel.Available(inventory);
        if (available == healthAvailable && tabsChecked)
            return;
        tabsChecked = true;
        healthAvailable = available;
        if (statsTab != null) statsTab.gameObject.SetActive(available && !folded);
        if (healthTab != null) healthTab.gameObject.SetActive(available && !folded);
        if (!available && showingHealth)
            showingHealth = false;
        ApplyPage();
    }

    /// <summary>Shows the character sheet (false) or the Health page (true).</summary>
    public void ShowPage(bool showHealth)
    {
        showingHealth = showHealth && healthAvailable;
        ApplyPage();
    }

    /// <summary>Opens the Health page (when the injury system is on).</summary>
    public void ShowHealth()
    {
        healthAvailable = InjuryHealthPanel.Available(inventory);
        ShowPage(true);
    }

    private void ApplyPage()
    {
        if (body != null) body.gameObject.SetActive(!folded && !showingHealth);
        if (healthPage != null) healthPage.gameObject.SetActive(!folded && showingHealth);
        if (statsTab != null) UITheme.SetSelected(statsTab, !showingHealth);
        if (healthTab != null) UITheme.SetSelected(healthTab, showingHealth);
    }

    private void ToggleFold()
    {
        folded = !folded;
        if (foldLabel != null) foldLabel.text = folded ? "Show" : "Hide";
        if (statsTab != null) statsTab.gameObject.SetActive(healthAvailable && !folded);
        if (healthTab != null) healthTab.gameObject.SetActive(healthAvailable && !folded);
        Place();
    }

    /// <summary>Rebuilds the sheet now (after a change made from code).</summary>
    public void Refresh()
    {
        if (sheet != null && sheet.isActiveAndEnabled)
            sheet.Rebuild();
        if (health != null && health.isActiveAndEnabled)
            health.Refresh();
    }
}
