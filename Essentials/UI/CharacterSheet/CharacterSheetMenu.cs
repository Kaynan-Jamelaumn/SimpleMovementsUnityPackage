using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Character page of the settings menu: the player's name, class, race, level and height, resources, combat stats,
/// traits, abilities (active and passive) and current buffs / debuffs. Hovering a row shows what it changes, green when
/// it helps and red when it hurts (for a stat: every source that adds to it). Rebuilt each time the page opens.
/// Built by Tools ▸ SimpleMovements ▸ Scene ▸ Build Pause &amp; Settings Menu.
/// </summary>
public class CharacterSheetMenu : MonoBehaviour
{
    [Tooltip("Where the sections and rows are created (a vertical layout, usually a scroll view's content).")]
    [SerializeField] private Transform content;
    [Tooltip("Panel shown next to the mouse while a row is hovered.")]
    [SerializeField] private RectTransform hoverPanel;
    [SerializeField] private TextMeshProUGUI hoverText;
    [Tooltip("The player shown. Empty = the first player in the scene.")]
    [SerializeField] private PlayerStatusController player;
    [Tooltip("Show each trait's and ability's description under its name (not only on hover).")]
    [SerializeField] private bool inlineDescriptions = true;
    [Tooltip("Rebuild while shown when the stats, traits or equipment change (and at most every this many seconds otherwise; 0 = only when opened).")]
    [SerializeField, Min(0f)] private float refreshSeconds = 0f;

    private readonly List<GameObject> built = new List<GameObject>();
    private Canvas canvas;
    private bool dirty;
    private float nextRefresh;
    private bool hovering;
    private PlayerStatusController watched;
    private CombatStats watchedStats;
    private TraitManager watchedTraits;
    private EquipmentManager watchedEquipment;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        HideHover();
    }

    /// <summary>Sets the sheet's parts (sheets built from code: the settings menu, the inventory's character panel).</summary>
    public void Configure(Transform rows, RectTransform hover, TextMeshProUGUI hoverLabel, bool descriptionsInline, float autoRefreshSeconds)
    {
        content = rows;
        hoverPanel = hover;
        hoverText = hoverLabel;
        inlineDescriptions = descriptionsInline;
        refreshSeconds = Mathf.Max(0f, autoRefreshSeconds);
        HideHover();
    }

    private void OnEnable() => Rebuild();

    private void OnDisable()
    {
        HideHover();
        Watch(null);
    }

    private void OnDestroy() => Watch(null);

    // Rebuilt when what it shows changes (while open), never while a row is hovered (its details would vanish).
    private void Watch(PlayerStatusController p)
    {
        if (watched == p)
            return;
        if (watchedStats != null) watchedStats.Changed -= MarkDirty;
        if (watchedTraits != null) watchedTraits.OnTraitsChanged -= MarkDirty;
        if (watchedEquipment != null) watchedEquipment.Changed -= MarkDirty;
        watched = p;
        watchedStats = p != null ? p.GetComponentInChildren<CombatStats>(true) : null;
        watchedTraits = p != null ? (p.TraitManager != null ? p.TraitManager : p.GetComponentInChildren<TraitManager>(true)) : null;
        watchedEquipment = p != null ? p.GetComponentInChildren<EquipmentManager>(true) : null;
        if (watchedStats == null && p != null) watchedStats = p.GetComponentInParent<CombatStats>();
        if (watchedEquipment == null && p != null) watchedEquipment = p.GetComponentInParent<EquipmentManager>();
        if (refreshSeconds <= 0f)
            return;
        if (watchedStats != null) watchedStats.Changed += MarkDirty;
        if (watchedTraits != null) watchedTraits.OnTraitsChanged += MarkDirty;
        if (watchedEquipment != null) watchedEquipment.Changed += MarkDirty;
    }

    private void MarkDirty() => dirty = true;

    private void Update()
    {
        if (refreshSeconds > 0f && !hovering && Time.unscaledTime >= nextRefresh)
        {
            PlayerStatusController current = player != null ? player : LocalPlayerStatus();
            if (dirty || current != watched || Time.unscaledTime >= nextRefresh + refreshSeconds * 4f)
                Rebuild();
        }
        if (hoverPanel == null || !hoverPanel.gameObject.activeSelf)
            return;
        Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
        hoverPanel.position = mouse + new Vector2(18f, -18f);
        InventoryUIFactory.ClampToCanvas(hoverPanel, canvas);
    }

    /// <summary>Shows another player (split screen, spectating).</summary>
    public void SetPlayer(PlayerStatusController p)
    {
        player = p;
        if (isActiveAndEnabled) Rebuild();
    }

    /// <summary>Rebuilds every section from the player's current state.</summary>
    /// <summary>The status of this machine's player (several players exist in network sessions).</summary>
    private static PlayerStatusController LocalPlayerStatus()
    {
        CombatEntity local = PlayerLocator.Local;
        if (local != null)
        {
            PlayerStatusController s = local.GetComponentInParent<PlayerStatusController>();
            if (s == null)
                s = local.GetComponentInChildren<PlayerStatusController>();
            if (s != null)
                return s;
        }
        return FindAnyObjectByType<PlayerStatusController>();
    }

    public void Rebuild()
    {
        dirty = false;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.25f, refreshSeconds);
        foreach (GameObject go in built)
            if (go != null) Destroy(go);
        built.Clear();
        if (content == null)
            return;
        PlayerStatusController p = player != null ? player : LocalPlayerStatus();
        if (isActiveAndEnabled)
            Watch(p);
        if (p == null)
        {
            Text("No player in the game.", 18f, InventoryUIFactory.MutedColor);
            return;
        }

        CharacterIdentity identity = p.GetComponentInChildren<CharacterIdentity>(true);
        TraitManager traits = p.TraitManager != null ? p.TraitManager : p.GetComponentInChildren<TraitManager>(true);
        CombatStats stats = p.GetComponentInChildren<CombatStats>(true);
        PlayerAbilityController abilities = p.GetComponentInChildren<PlayerAbilityController>(true);
        PlayerNameComponent nameComp = p.GetComponentInChildren<PlayerNameComponent>(true);
        PlayerClass cls = p.CurrentPlayerClass;

        // ---------------------------------------------------------------- identity
        string charName = nameComp != null && !string.IsNullOrEmpty(nameComp.PlayerName) ? nameComp.PlayerName : p.name;
        Text(charName, 30f, InventoryUIFactory.TextColor, FontStyles.Bold);
        var sub = new List<string>();
        if (identity != null && identity.Race != null) sub.Add(identity.Race.Name);
        if (cls != null) sub.Add(cls.GetClassName());
        int level = identity != null ? identity.Level : p.XPManager != null ? Mathf.Max(1, p.XPManager.CurrentLevel) : 1;
        sub.Add($"Level {level}");
        if (identity != null && identity.Height > 0f) sub.Add($"{identity.Height:0.00} m");
        Text(string.Join("  ·  ", sub), 17f, InventoryUIFactory.MutedColor);

        // ---------------------------------------------------------------- class and race
        Section("Class & Race");
        if (cls != null)
        {
            var lines = new List<StatText.Line>();
            StatText.Add(lines, cls.GetCombatStatModifiers());
            if (cls.archetype != null) lines.AddRange(StatText.Of(cls.archetype));
            Row(cls.GetClassName(), "Class", Hover(cls.GetClassName(), cls.classDescription, lines));
        }
        if (identity != null)
        {
            foreach (CharacterArchetype a in identity.Archetypes)
                if (a != null && (cls == null || a != cls.archetype))
                    Row(a.Name, a.kind.ToString(), Hover(a.Name, a.description, StatText.Of(a)));
        }

        // ---------------------------------------------------------------- resources
        Section("Resources");
        Resource("Health", p.HpManager);
        Resource("Stamina", p.StaminaManager);
        Resource("Mana", p.ManaManager);
        if (p.WeightManager != null) Resource("Carry Weight", p.WeightManager);
        if (p.SpeedManager != null)
            Row("Move Speed", $"{p.SpeedManager.BaseSpeed:0.##} m/s", null);

        // ---------------------------------------------------------------- combat stats
        if (stats != null)
        {
            Section("Combat Stats");
            bool any = false;
            foreach (CombatStatType stat in (CombatStatType[])Enum.GetValues(typeof(CombatStatType)))
            {
                List<StatText.Line> sources = stats.SourcesOf(stat);
                float v = stats.Get(stat);
                if (Mathf.Approximately(v, 0f) && sources.Count == 0)
                    continue;
                any = true;
                CombatStatInfo info = CombatStatInfo.Get(stat);
                int sign = Mathf.Approximately(v, 0f) ? 0 : (v > 0f) != CombatStatInfo.LowerIsBetter(stat) ? 1 : -1;
                string value = StatText.Paint($"{v:+0.##;-0.##;0}{(info.isPercent ? "%" : "")}", sign);
                Row(info.name, value, Hover(info.name, info.description, sources));
            }
            if (!any)
                Text("No combat stats yet.", 15f, InventoryUIFactory.MutedColor);
        }

        // ---------------------------------------------------------------- traits
        if (traits != null)
        {
            Section("Traits");
            List<Trait> owned = traits.ActiveTraits;
            if (owned.Count == 0)
                Text("No traits.", 15f, InventoryUIFactory.MutedColor);
            foreach (Trait t in owned)
            {
                if (t == null) continue;
                string tag = t.HasActiveSkill ? StatText.Paint("Active", 0) : t.Kind == TraitKind.Passive ? "Passive" : t.Kind.ToString();
                Row(t.Name, tag, Hover(t.Name, t.description, StatText.Of(t)), Detail(t.description, StatText.Of(t)));
            }
        }

        // ---------------------------------------------------------------- abilities
        Section("Abilities");
        bool anyAbility = false;
        if (abilities != null)
        {
            foreach (AbilitySlot slot in abilities.Slots)
            {
                if (slot == null || slot.ability == null) continue;
                anyAbility = true;
                string source = slot.Source == AbilitySlotSource.Skill ? "Skill" : slot.Source == AbilitySlotSource.Innate ? "Innate" : "Item";
                var lines = new List<StatText.Line> { new StatText.Line(slot.ability.Describe(slot.modifiers), 0) };
                Row(slot.ability.DisplayName, $"Active · {source}", Hover(slot.ability.DisplayName, slot.ability.description, lines), Detail(slot.ability.description, lines));
            }
        }
        if (traits != null)
        {
            foreach (Trait t in traits.ActiveTraits)
            {
                if (t == null) continue;
                foreach (TraitBehaviour b in t.behaviours)
                {
                    if (b == null || b.CountsAsActiveTrait) continue; // trait abilities are listed with the slots above
                    anyAbility = true;
                    string kind = b.IsActive ? "Movement" : "Passive";
                    var bl = new List<StatText.Line> { new StatText.Line(b.Describe(), b.IsActive ? 0 : 1) };
                    Row(b.MenuName, $"{kind} · {t.Name}", Hover(b.MenuName, null, bl), Detail(null, bl));
                }
            }
        }
        if (identity != null)
        {
            foreach (CharacterArchetype a in identity.Archetypes)
            {
                if (a == null) continue;
                foreach (EquipmentEffect e in a.effects)
                {
                    if (e == null) continue;
                    anyAbility = true;
                    var el = new List<StatText.Line> { new StatText.Line(e.Describe(), 0) };
                    Row(e.MenuName, $"Passive · {a.Name}", Hover(e.MenuName, null, el), Detail(null, el));
                }
            }
        }
        if (!anyAbility)
            Text("No abilities.", 15f, InventoryUIFactory.MutedColor);

        // ---------------------------------------------------------------- buffs and debuffs
        TimedStatModifiers timed = p.GetComponentInChildren<TimedStatModifiers>(true);
        if (timed != null && timed.All.Count > 0)
        {
            Section("Effects");
            foreach (TimedStatModifiers.Active a in timed.All)
                Row(StatText.Paint(a.name, a.debuff ? -1 : 1), $"{a.Remaining:0}s", null);
        }
    }

    // ------------------------------------------------------------------ building blocks
    private void Section(string title)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text(title, content, title.ToUpperInvariant(), 15f, InventoryUIFactory.AccentColor, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
        InventoryUIFactory.Size(t, -1f, 34f);
        built.Add(t.gameObject);
    }

    private void Text(string text, float size, Color color, FontStyles style = FontStyles.Normal)
    {
        TextMeshProUGUI t = InventoryUIFactory.Text("Text", content, text, size, color, TextAlignmentOptions.TopLeft, style);
        built.Add(t.gameObject);
    }

    private void Resource(string label, StatusManager m)
    {
        if (m == null) return;
        Row(label, $"{m.CurrentValue:0} / {m.MaxValue:0}", null);
    }

    /// <summary>
    /// One row: label on the left, value on the right; hovering shows <paramref name="hover"/>. With inline
    /// descriptions on, <paramref name="detail"/> is written under the label.
    /// </summary>
    private void Row(string label, string value, string hover, string detail = null)
    {
        Image bg = InventoryUIFactory.Panel("Row", content, new Color(1f, 1f, 1f, 0.03f));
        UITheme.Round(bg, UITheme.RadiusSmall);
        bg.raycastTarget = hover != null;
        bool twoLines = inlineDescriptions && !string.IsNullOrEmpty(detail);
        if (twoLines)
        {
            VerticalLayoutGroup v = InventoryUIFactory.Vertical(bg.gameObject, 0, 0f, false);
            v.padding = new RectOffset(10, 10, 6, 8);
            RectTransform top = InventoryUIFactory.Rect("Top", bg.transform);
            InventoryUIFactory.Horizontal(top.gameObject, 12f);
            InventoryUIFactory.Size(top, -1f, 26f);
            TextMeshProUGUI l2 = InventoryUIFactory.Text("Label", top, label, 16f, InventoryUIFactory.TextColor, TextAlignmentOptions.Left, FontStyles.Bold);
            InventoryUIFactory.Size(l2, -1f, 26f, 1f);
            TextMeshProUGUI v2 = InventoryUIFactory.Text("Value", top, value, 14f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Right);
            InventoryUIFactory.Size(v2, 200f, 26f, 0f);
            TextMeshProUGUI d = InventoryUIFactory.Text("Detail", bg.transform, detail, 14f, InventoryUIFactory.MutedColor, TextAlignmentOptions.TopLeft);
            d.richText = true;
        }
        else
        {
            HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 12f);
            h.padding = new RectOffset(10, 10, 0, 0);
            InventoryUIFactory.Size(bg, -1f, 32f);
            TextMeshProUGUI l = InventoryUIFactory.Text("Label", bg.transform, label, 16f, InventoryUIFactory.TextColor);
            InventoryUIFactory.Size(l, -1f, 32f, 1f);
            TextMeshProUGUI v = InventoryUIFactory.Text("Value", bg.transform, value, 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Right);
            InventoryUIFactory.Size(v, 220f, 32f, 0f);
        }
        if (hover != null)
        {
            var trigger = bg.gameObject.AddComponent<CharacterSheetHover>();
            trigger.Setup(this, hover);
        }
        built.Add(bg.gameObject);
    }

    /// <summary>A short description and the coloured changes, for the line under a row's label.</summary>
    private static string Detail(string description, List<StatText.Line> lines)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(description))
        {
            string d = description.Trim();
            if (d.Length > 220) d = d.Substring(0, 217) + "...";
            sb.Append(d);
        }
        if (lines != null)
        {
            int n = 0;
            foreach (StatText.Line line in lines)
            {
                if (n++ >= 4) { sb.Append("\n…"); break; }
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("• ").Append(line.Rich);
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static string Hover(string title, string description, List<StatText.Line> lines)
    {
        var sb = new StringBuilder();
        sb.Append("<b>").Append(title).Append("</b>");
        if (!string.IsNullOrEmpty(description))
            sb.Append('\n').Append(StatText.Paint(description, 0));
        if (lines != null && lines.Count > 0)
        {
            sb.Append('\n');
            foreach (StatText.Line line in lines)
                sb.Append("\n• ").Append(line.Rich);
        }
        else
            sb.Append('\n').Append(StatText.Paint("No changes.", 0));
        return sb.ToString();
    }

    // ------------------------------------------------------------------ hover
    internal void ShowHover(string text)
    {
        hovering = true;
        if (hoverPanel == null || hoverText == null)
            return;
        hoverText.text = text;
        hoverPanel.gameObject.SetActive(true);
        hoverPanel.SetAsLastSibling();
        Update();
    }

    internal void HideHover()
    {
        hovering = false;
        if (hoverPanel != null)
            hoverPanel.gameObject.SetActive(false);
    }
}
