using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something to carry water in: a watering can, a bucket, a flask. Each copy remembers how much it holds and where the
/// water came from (well, river, rain...): the water carries that into the soil. Fill it at a Water Source.
/// </summary>
[CreateAssetMenu(fileName = "Water Container", menuName = "SimpleMovements/Agriculture/Water Container", order = 16)]
public class WaterContainerSO : ItemSO, IAgriItem, IRuntimeItem, IItemInstanceTooltip
{
    [Header("Agriculture")]
    [SerializeField] private string containerId = "watering_can";
    [Tooltip("Litres it holds.")]
    [SerializeField, Min(0.5f)] private float capacity = 6f;
    [Tooltip("How wide the water spreads when poured, metres.")]
    [SerializeField, Min(0.05f)] private float spread = 0.4f;
    [Tooltip("Litres poured per second.")]
    [SerializeField, Min(0.1f)] private float flow = 0.8f;
    [Tooltip("It wets the leaves of the plants it is poured over.")]
    [SerializeField] private bool wetsLeaves = true;

    [System.NonSerialized] private string runtimeId;

    public string ContainerId => containerId;
    public float Capacity => capacity;
    public float Spread => spread;
    public float Flow => flow;
    public bool WetsLeaves => wetsLeaves;
    public string AgriKey => AgriIds.Container(containerId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public WaterContainerSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 1;
        weight = 1f;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(ContainerPreset p, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (p == null) return;
        Apply(p);
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public void Apply(ContainerPreset p)
    {
        containerId = p.id;
        capacity = p.capacity;
        spread = p.spread;
        flow = p.flow;
        wetsLeaves = p.wetsLeaves;
        name = p.name;
        description = p.description;
        price = p.value;
        weight = p.weight;
    }

    public WaterProfile Profile(InventoryItem copy)
    {
        AgriItemInfo.Water(copy, out float litres, out WaterSourceKind kind);
        return new WaterProfile { id = containerId, litres = Mathf.Min(litres, capacity), kind = kind, spread = spread, flow = flow, wetsLeaves = wetsLeaves };
    }

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines)
    {
        if (AgriItemInfo.Water(item, out float litres, out WaterSourceKind kind) && litres > 0.05f)
            lines.Add($"Holds {litres:0.#} of {capacity:0.#} L of {AgriText.Water(kind).ToLowerInvariant()}");
        else
            lines.Add($"Empty (holds {capacity:0.#} L)");
    }
}
