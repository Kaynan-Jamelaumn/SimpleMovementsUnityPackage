using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A care tool of animal husbandry - a soft brush, a curry comb, a hoof pick, a pitchfork - or a supply of its own that
/// is used up. What it is to the animals is its tool's data (<see cref="AnimalToolData"/>: its tags say which care it does,
/// its quality how well); a tool wears with use (its durability is its uses: a worn-out tool still works, poorly), a
/// supply is used up one per use. Having it in the bag is enough (the pen's window offers what the player carries).
/// An asset bound in the Animal Database (animal:tool/&lt;tool&gt;) replaces the item made at runtime.
/// </summary>
[CreateAssetMenu(fileName = "Animal Care Tool", menuName = "SimpleMovements/Animals/Care Tool (Item)", order = 12)]
public class AnimalToolSO : ItemSO, IAnimalItem, IRuntimeItem, IItemInstanceTooltip
{
    [Header("Animals")]
    [Tooltip("The care tool it is (an id of the Animal Database's tools: soft_brush, curry_comb, hoof_pick, pitchfork).")]
    [SerializeField] private string toolId = "";

    [System.NonSerialized] private string runtimeId;

    public string ToolId => toolId;
    public string AnimalKey => AnimalIds.Tool(toolId);
    public string RuntimeId => runtimeId;
    /// <summary>A tool shows its wear; a supply (used up whole) does not.</summary>
    public override bool ShowsDurability => MaxDurability > 1;

    public AnimalToolSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 1;
        weight = 0.5f;
        maxDurability = 150;
        durability = 150;
        durabilityReductionPerUse = 1;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(AnimalToolData t, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (t == null) return;
        Apply(t);
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    /// <summary>Fills it from a tool's data (the item made at runtime, and the editor's assets).</summary>
    public void Apply(AnimalToolData t)
    {
        if (t == null) return;
        toolId = t.id;
        name = t.DisplayName;
        description = string.IsNullOrWhiteSpace(t.description) ? "A tool for caring for animals." : t.description;
        itemType = ItemType.Miscellaneous;
        price = Mathf.Max(1, t.value);
        weight = Mathf.Max(0.05f, t.weight);
        shouldBeDestroyedOn0UsesLeft = false;
        if (t.consumed)
        {
            // A supply: stacked, used up one per use (the command takes it).
            stackMax = 20;
            maxDurability = 1;
            durability = 1;
            durabilityReductionPerUse = 0;
        }
        else
        {
            // A tool: one to a slot, its uses its durability (0 uses: it never wears).
            stackMax = 1;
            maxDurability = Mathf.Max(1, t.uses);
            durability = maxDurability;
            durabilityReductionPerUse = t.uses > 1 ? 1 : 0;
        }
    }

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines)
    {
        if (item == null || lines == null || MaxDurability <= 1) return;
        float edge = AnimalToolkit.EdgeOf(item);
        if (edge <= 0f) lines.Add("Worn out: it does a poor job");
        else lines.Add($"Wear: {Mathf.RoundToInt((1f - edge) * 100f)}% worn");
    }

    public override void AppendTooltip(List<string> lines)
    {
        AnimalToolData t = AnimalDatabase.Instance.Tool(toolId);
        if (t == null)
        {
            lines.Add("Care tool");
            return;
        }
        string tags = Tags(t);
        lines.Add(t.consumed ? (tags.Length > 0 ? $"Care supply · {tags}" : "Care supply") : (tags.Length > 0 ? $"Care tool · {tags}" : "Care tool"));
        if (!t.consumed && t.uses > 1) lines.Add($"About {t.uses} uses before it is worn out");
    }

    /// <summary>A tool's tags in words ("brush, curry comb").</summary>
    public static string Tags(AnimalToolData t)
    {
        if (t?.tags == null || t.tags.Count == 0) return "";
        var parts = new List<string>();
        foreach (string tag in t.tags)
            if (!string.IsNullOrWhiteSpace(tag)) parts.Add(tag.Trim().Replace('_', ' '));
        return string.Join(", ", parts);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(toolId)) errors.Add("No tool id.");
        else
        {
            AnimalToolData t = AnimalDatabase.Instance.Tool(toolId);
            if (t == null) warnings.Add($"No care tool '{toolId}' in the Animal Database.");
            else if (!t.IsItem(AnimalKey)) warnings.Add($"The care tool '{toolId}' does not list '{AnimalKey}' among its items: this item is not taken for it.");
        }
    }
}
