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
/// Built by Tools ▸ SimpleMovements ▸ Scene UI ▸ Build Pause &amp; Settings Menu.
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

    private readonly List<GameObject> built = new List<GameObject>();
    private Canvas canvas;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        HideHover();
    }

    private void OnEnable() => Rebuild();

    private void OnDisable() => HideHover();

    private void Update()
    {
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
    public void Rebuild()
    {
        foreach (GameObject go in built)
            if (go != null) Destroy(go);
        built.Clear();
        if (content == null)
            return;
        PlayerStatusController p = player != null ? player : FindAnyObjectByType<PlayerStatusController>();
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
                Row(t.Name, tag, Hover(t.Name, t.description, StatText.Of(t)));
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
                Row(slot.ability.DisplayName, $"Active · {source}", Hover(slot.ability.DisplayName, slot.ability.description, lines));
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
                    Row(b.MenuName, $"{kind} · {t.Name}", Hover(b.MenuName, null, new List<StatText.Line> { new StatText.Line(b.Describe(), b.IsActive ? 0 : 1) }));
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
                    Row(e.MenuName, $"Passive · {a.Name}", Hover(e.MenuName, null, new List<StatText.Line> { new StatText.Line(e.Describe(), 0) }));
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

    /// <summary>One row: label on the left, value on the right; hovering shows <paramref name="hover"/>.</summary>
    private void Row(string label, string value, string hover)
    {
        Image bg = InventoryUIFactory.Panel("Row", content, new Color(1f, 1f, 1f, 0.03f));
        bg.raycastTarget = hover != null;
        HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 12f);
        h.padding = new RectOffset(10, 10, 0, 0);
        InventoryUIFactory.Size(bg, -1f, 32f);
        TextMeshProUGUI l = InventoryUIFactory.Text("Label", bg.transform, label, 16f, InventoryUIFactory.TextColor);
        InventoryUIFactory.Size(l, -1f, 32f, 1f);
        TextMeshProUGUI v = InventoryUIFactory.Text("Value", bg.transform, value, 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Right);
        InventoryUIFactory.Size(v, 220f, 32f, 0f);
        if (hover != null)
        {
            var trigger = bg.gameObject.AddComponent<CharacterSheetHover>();
            trigger.Setup(this, hover);
        }
        built.Add(bg.gameObject);
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
        if (hoverPanel == null || hoverText == null)
            return;
        hoverText.text = text;
        hoverPanel.gameObject.SetActive(true);
        hoverPanel.SetAsLastSibling();
        Update();
    }

    internal void HideHover()
    {
        if (hoverPanel != null)
            hoverPanel.gameObject.SetActive(false);
    }
}
