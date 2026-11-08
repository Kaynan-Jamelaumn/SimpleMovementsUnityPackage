using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An empty container some harvested materials must be taken into (an empty vial for blood or venom, a jar for bile):
/// the bag must hold one per piece, and each piece taken uses one up. Made by Tools ▸ SimpleMovements ▸ Harvesting ▸
/// Create Tools for every container the Harvest Database's materials name; sell them at a merchant, or give them as
/// starting items. Any item of the right name works (the alchemy station's bottle, for one).
/// </summary>
[CreateAssetMenu(fileName = "Harvest Container", menuName = "SimpleMovements/Harvesting/Container", order = 5)]
public class HarvestContainerSO : MaterialSO
{
    public HarvestContainerSO()
    {
        stackMax = 20;
        weight = 0.05f;
        price = 1f;
    }

    /// <summary>Fills it as the container called <paramref name="containerName"/>.</summary>
    public void Setup(string containerName, Sprite itemIcon)
    {
        name = containerName;
        description = $"Empty. Blood, venom and bile are taken into one ({HarvestText.Capital(containerName.ToLowerInvariant())}).";
        SetMaterial(MaterialKind.Other);
        if (itemIcon != null)
            icon = itemIcon;
    }

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("A container for harvesting: one is used up for each piece of blood, venom or bile taken.");
        base.AppendTooltip(lines);
    }
}
