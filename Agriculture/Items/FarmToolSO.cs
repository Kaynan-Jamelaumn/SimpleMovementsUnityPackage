using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A farming tool: a hoe, a shovel, a trowel, a dibber, pruning shears, a sickle, a pollination brush, tweezers - or
/// garden stakes (used up, one per plant). Its quality and its wear (durability) decide how well it works; it wears
/// with use. Having it in the bag is enough (the field window offers what the player carries).
/// </summary>
[CreateAssetMenu(fileName = "Farm Tool", menuName = "SimpleMovements/Agriculture/Farm Tool", order = 13)]
public class FarmToolSO : ItemSO, IAgriItem, IRuntimeItem
{
    [Header("Agriculture")]
    [Tooltip("Its id in the agriculture rules (a built-in tool's id: hoe, shovel, trowel...). Two assets with one id count as the same tool.")]
    [SerializeField] private string toolId = "hoe";
    [SerializeField] private FarmToolKind kind = FarmToolKind.Hoe;
    [Tooltip("How good a tool it is (0-1).")]
    [SerializeField, Range(0f, 1f)] private float quality = 0.6f;
    [Tooltip("How fast it works (1 = normal).")]
    [SerializeField, Range(0.25f, 3f)] private float speed = 1f;

    [System.NonSerialized] private string runtimeId;

    public string ToolId => toolId;
    public FarmToolKind Kind => kind;
    public float Quality => quality;
    public float Speed => speed;
    public string AgriKey => AgriIds.Tool(toolId);
    public string RuntimeId => runtimeId;

    public FarmToolSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 1;
        weight = 1f;
        maxDurability = 150;
        durability = 150;
        durabilityReductionPerUse = 1;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(FarmToolPreset p, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (p == null) return;
        Apply(p);
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    /// <summary>Fills it from a built-in tool (used by the editor's Create Items).</summary>
    public void Apply(FarmToolPreset p)
    {
        toolId = p.id;
        kind = p.kind;
        quality = p.quality;
        speed = p.speed;
        name = p.name;
        description = p.description;
        price = p.value;
        weight = p.weight;
        bool stakes = p.kind == FarmToolKind.Stakes;
        stackMax = stakes ? 20 : 1;
        maxDurability = stakes ? 1 : Mathf.Max(1, p.durability);
        durability = maxDurability;
        durabilityReductionPerUse = stakes ? 0 : 1;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public override bool ShowsDurability => kind != FarmToolKind.Stakes;

    public FarmToolProfile Profile(InventoryItem copy) => new FarmToolProfile
    {
        id = toolId, kind = kind, quality = quality, speed = speed,
        edge = copy != null && MaxDurability > 1 ? Mathf.Clamp01(copy.durability / MaxDurability) : 1f,
    };

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        lines.Add($"{AgriText.Tool(kind)} · {(quality >= 0.8f ? "fine" : quality >= 0.55f ? "good" : quality >= 0.3f ? "plain" : "crude")} work");
    }
}
