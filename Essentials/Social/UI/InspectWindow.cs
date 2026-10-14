using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows an inspected player (<see cref="PlayerInspection"/>): name and title, guild, alliance, level, race and class,
/// fame and health, their equipment (icon, slot and item) and their combat stats. Opens when the answer arrives
/// (the Inspect key on the target, or Inspect in the target menu); Close or Escape closes it. It frees the cursor while open.
/// </summary>
[DisallowMultipleComponent]
public class InspectWindow : MonoBehaviour, IGameplayPanel
{
    private CanvasGroup group;
    private RectTransform window, equipmentList, statsList;
    private TextMeshProUGUI titleText, subtitleText, infoText;
    private SocialPanelHandle handle;
    private InspectData shown;
    private bool open;

    public string PanelName => "Inspect";

    /// <summary>The player shown (0 = closed).</summary>
    public int ShownPlayer => open ? shown.playerId : 0;

    public void ClosePanel() => Close();

    private void Start()
    {
        handle = new SocialPanelHandle(this);
        Build();
        PlayerInspection.Received += OnReceived;
    }

    private void OnDestroy()
    {
        PlayerInspection.Received -= OnReceived;
        handle?.Close();
        if (window != null)
            Destroy(window.gameObject);
    }

    private void OnReceived(InspectData data)
    {
        if (!data.Ok)
        {
            PlayerActions.Say(data.error);
            return;
        }
        shown = data;
        Fill(data);
        open = true;
        SocialUI.Show(group, true);
        handle.Open();
    }

    public void Close()
    {
        open = false;
        SocialUI.Show(group, false);
        handle?.Close();
    }

    private void Update()
    {
        if (!open)
            return;
        // The inspected player left (disconnected, another realm): the window closes.
        CombatEntity e = HubWorld.PlayerIds.Find(shown.playerId);
        CombatEntity me = PlayerLocator.Local;
        if (e == null || me == null || CombatRules.AreSeparated(me, e))
            Close();
    }

    // ------------------------------------------------------------------ content

    private void Fill(InspectData d)
    {
        titleText.text = string.IsNullOrEmpty(d.title) ? d.name : $"{d.name}  <size=70%><i>{d.title}</i></size>";
        string guild = string.IsNullOrEmpty(d.guild) ? "<color=#8a8f99>No guild</color>"
            : string.IsNullOrEmpty(d.guildTag) ? d.guild : $"{d.guild} [{d.guildTag}]";
        if (!string.IsNullOrEmpty(d.alliance))
            guild += $"  ·  <color=#9aa4b5>Alliance: {d.alliance}</color>";
        subtitleText.text = guild;

        var info = new System.Text.StringBuilder();
        if (d.level > 0) info.Append($"Level <b>{d.level}</b>");
        if (!string.IsNullOrEmpty(d.race)) info.Append(info.Length > 0 ? "   " : "").Append(d.race);
        if (!string.IsNullOrEmpty(d.className)) info.Append(info.Length > 0 ? " " : "").Append(d.className);
        info.Append(info.Length > 0 ? "   " : "").Append($"Fame <b><color=#ffd34d>{d.fame:N0}</color></b>");
        if (d.maxHealth > 0f) info.Append($"   Health <b>{Mathf.CeilToInt(d.health)}/{Mathf.CeilToInt(d.maxHealth)}</b>");
        infoText.text = info.ToString();

        SocialUI.Clear(equipmentList);
        if (d.equipment == null || d.equipment.Length == 0)
            Note(equipmentList, "Nothing equipped.");
        else
            foreach (InspectItem item in d.equipment)
                ItemRow(item);

        SocialUI.Clear(statsList);
        if (d.stats == null || d.stats.Length == 0)
            Note(statsList, "No stats.");
        else
            foreach (InspectStat s in d.stats)
                StatRow(s);
    }

    private void ItemRow(InspectItem entry)
    {
        ItemSO item = string.IsNullOrEmpty(entry.itemId) ? null : ItemDatabase.Item(entry.itemId);
        Image bg = InventoryUIFactory.Panel("Item", equipmentList, new Color(1f, 1f, 1f, 0.05f));
        InventoryUIFactory.Size(bg, -1f, 40f, 1f);
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 8f);
        h.padding = new RectOffset(6, 6, 3, 3);
        Image icon = InventoryUIFactory.Panel("Icon", bg.transform, item != null && item.Icon != null ? Color.white : new Color(1f, 1f, 1f, 0.1f));
        if (item != null && item.Icon != null)
        {
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
        }
        icon.raycastTarget = false;
        InventoryUIFactory.Size(icon, 34f, 34f, 0f);
        TextMeshProUGUI slot = InventoryUIFactory.Text("Slot", bg.transform, entry.slot, 14f, InventoryUIFactory.MutedColor);
        slot.textWrappingMode = TextWrappingModes.NoWrap;
        InventoryUIFactory.Size(slot, 110f, 34f, 0f);
        string name = item != null ? item.Name : (string.IsNullOrEmpty(entry.itemName) ? "Unknown item" : entry.itemName);
        TextMeshProUGUI label = InventoryUIFactory.Text("Name", bg.transform, name, 16f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        InventoryUIFactory.Size(label, -1f, 34f, 1f);
    }

    private void StatRow(InspectStat s)
    {
        RectTransform row = InventoryUIFactory.Rect("Stat", statsList);
        InventoryUIFactory.Horizontal(row.gameObject, 8f);
        InventoryUIFactory.Size(row, -1f, 24f, 1f);
        TextMeshProUGUI label = InventoryUIFactory.Text("Label", row, s.label, 15f, InventoryUIFactory.MutedColor);
        InventoryUIFactory.Size(label, -1f, 24f, 1f);
        TextMeshProUGUI value = InventoryUIFactory.Text("Value", row, s.value, 15f, Color.white, TextAlignmentOptions.Right, FontStyles.Bold);
        InventoryUIFactory.Size(value, 80f, 24f, 0f);
    }

    private static void Note(RectTransform list, string text)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text("Note", list, text, 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Italic);
        InventoryUIFactory.Size(t, -1f, 28f, 1f);
    }

    // ------------------------------------------------------------------ building

    private void Build()
    {
        Transform root = SocialUI.Canvas.transform;
        Image panel = InventoryUIFactory.Panel("Inspect", root, InventoryUIFactory.PanelColor);
        window = panel.rectTransform;
        SocialUI.Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 600f));
        group = panel.gameObject.AddComponent<CanvasGroup>();
        InventoryUIFactory.Vertical(panel.gameObject, 18, 8f, false);

        titleText = InventoryUIFactory.Text("Name", panel.transform, "", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        titleText.richText = true;
        InventoryUIFactory.Size(titleText, -1f, 38f, 1f);
        subtitleText = InventoryUIFactory.Text("Guild", panel.transform, "", 18f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
        subtitleText.richText = true;
        InventoryUIFactory.Size(subtitleText, -1f, 26f, 1f);
        infoText = InventoryUIFactory.Text("Info", panel.transform, "", 17f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center);
        infoText.richText = true;
        InventoryUIFactory.Size(infoText, -1f, 26f, 1f);

        RectTransform columns = InventoryUIFactory.Rect("Columns", panel.transform);
        InventoryUIFactory.Horizontal(columns.gameObject, 14f);
        LayoutElement le = InventoryUIFactory.Size(columns, -1f, 400f, 1f);
        le.flexibleHeight = 1f;
        equipmentList = Column(columns, "Equipment", 1.3f);
        statsList = Column(columns, "Stats", 1f);

        RectTransform buttons = InventoryUIFactory.Rect("Buttons", panel.transform);
        InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
        InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
        InventoryUIFactory.Button("Close", buttons, "Close", Close);
        SocialUI.Show(group, false);
    }

    /// <summary>A titled, scrolling column; returns its content.</summary>
    private static RectTransform Column(Transform parent, string title, float weight)
    {
        Image bg = InventoryUIFactory.Panel(title, parent, new Color(1f, 1f, 1f, 0.04f));
        InventoryUIFactory.Vertical(bg.gameObject, 10, 6f, false);
        LayoutElement le = InventoryUIFactory.Size(bg, -1f, -1f, weight);
        le.flexibleHeight = 1f;
        TextMeshProUGUI header = InventoryUIFactory.Text("Header", bg.transform, title, 19f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        InventoryUIFactory.Size(header, -1f, 28f, 1f);

        RectTransform viewport = InventoryUIFactory.Rect("List", bg.transform);
        LayoutElement vle = InventoryUIFactory.Size(viewport, -1f, 120f, 1f);
        vle.flexibleHeight = 1f;
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0.12f);
        RectTransform content = InventoryUIFactory.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 4f;
        v.padding = new RectOffset(4, 4, 4, 4);
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return content;
    }
}
