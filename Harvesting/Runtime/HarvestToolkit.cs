using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tools a player has for harvesting, from their inventory: harvesting tools (<see cref="HarvestToolSO"/>), and any
/// blade or axe after a fashion - a dagger skins and butchers poorly, an axe hacks through joints - their edge from their
/// durability, their make from a crafted item's quality. The bare hands are always there (they can pull and pry a little).
/// The same player and inventory give the same toolbox on the server and on the player's machine.
/// </summary>
public static class HarvestToolkit
{
    /// <summary>The toolbox of <paramref name="player"/> (bag, hotbar and equipment).</summary>
    public static HarvestToolbox For(CombatEntity player)
    {
        var box = new HarvestToolbox();
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv == null)
            return box;
        foreach (InventoryItem it in inv.GetBagItems())
            Add(box, it);
        IReadOnlyList<InventorySlot> eq = inv.EquipmentSlots;
        if (eq != null)
            foreach (InventorySlot s in eq)
                if (s != null && s.heldItem != null)
                    Add(box, s.heldItem.GetComponent<InventoryItem>());
        return box;
    }

    private static void Add(HarvestToolbox box, InventoryItem copy)
    {
        HarvestToolProfile p = ProfileOf(copy);
        if (p == null)
            return;
        // Two of the same tool: the sharper one.
        int i = box.tools.FindIndex(t => t.id == p.id);
        if (i < 0) box.tools.Add(p);
        else if (p.sharpness > box.tools[i].sharpness) box.tools[i] = p;
    }

    /// <summary>The id a tool is known by in plans and results (its Item Database id, else its asset name).</summary>
    public static string IdOf(ItemSO item) => item == null ? "" : ItemDatabase.Id(item) ?? "item:" + ((Object)item).name;

    /// <summary>A copy of an item as a harvesting tool (null when it is no use for harvesting).</summary>
    public static HarvestToolProfile ProfileOf(InventoryItem copy)
    {
        ItemSO item = copy != null ? copy.itemScriptableObject : null;
        if (item == null)
            return null;
        if (item is HarvestToolSO tool)
            return tool.Profile(copy, IdOf(item));
        if (!(item is WeaponSO w))
            return null;
        HarvestToolFit fit = WeaponFit(w);
        if (fit == null)
            return null;
        float edge = item.MaxDurability > 1 ? Mathf.Clamp01(copy.durability / item.MaxDurability) : 1f;
        float quality = item is ICraftedItem crafted && crafted.Record != null ? Mathf.Clamp01(crafted.Record.quality) : 0.5f;
        return new HarvestToolProfile { id = IdOf(item), displayName = item.Name, fit = fit, sharpness = edge, quality = quality, speed = 1f };
    }

    /// <summary>How fit a weapon is for each kind of harvesting work, by its category (null = not at all).</summary>
    public static HarvestToolFit WeaponFit(WeaponSO w)
    {
        switch (w.Category)
        {
            case WeaponCategory.Dagger: return new HarvestToolFit(0.6f, 0.5f, 0.5f, 0f, 0.3f, 0f, 0f);
            case WeaponCategory.Thrown: return new HarvestToolFit(0.4f, 0.3f, 0.35f, 0f, 0.2f, 0f, 0f);
            case WeaponCategory.Sword: return new HarvestToolFit(0.25f, 0.45f, 0.15f, 0f, 0.15f, 0f, 0.3f);
            case WeaponCategory.Greatsword: return new HarvestToolFit(0.1f, 0.3f, 0f, 0f, 0.1f, 0f, 0.4f);
            case WeaponCategory.Axe: return new HarvestToolFit(0.1f, 0.35f, 0f, 0.15f, 0.1f, 0f, 0.9f);
            case WeaponCategory.Spear: return new HarvestToolFit(0.2f, 0.2f, 0.1f, 0f, 0.1f, 0f, 0f);
            case WeaponCategory.Fist: return new HarvestToolFit(0.1f, 0.1f, 0f, 0f, 0.15f, 0.15f, 0f);
            case WeaponCategory.Tool:
                switch (w.ToolType)
                {
                    case ToolType.Axe: return new HarvestToolFit(0.05f, 0.3f, 0f, 0.15f, 0.1f, 0f, 0.8f);
                    case ToolType.Scythe: return new HarvestToolFit(0.2f, 0.2f, 0f, 0f, 0.15f, 0f, 0.1f);
                    case ToolType.PickAxe: return new HarvestToolFit(0f, 0f, 0f, 0f, 0.1f, 0.2f, 0.3f);
                    default: return null;
                }
            default: return null;
        }
    }

    /// <summary>
    /// Wears the tool <paramref name="toolId"/> the player used (its sharpest copy) by <paramref name="uses"/> uses of its
    /// durability. Where the rules run (offline, the server).
    /// </summary>
    public static void Wear(CombatEntity player, string toolId, int uses = 1)
    {
        if (player == null || string.IsNullOrEmpty(toolId) || uses <= 0)
            return;
        InventoryManager inv = InventoryManager.For(player);
        if (inv == null)
            return;
        // The copy the toolbox would have picked: the sharpest one, in the bag, the hotbar or held (as For gathers them).
        InventoryItem best = null;
        void Consider(InventoryItem it)
        {
            ItemSO item = it != null ? it.itemScriptableObject : null;
            if (item == null || item.MaxDurability <= 1 || item.DurabilityReductionPerUse <= 0 || IdOf(item) != toolId) return;
            if (best == null || it.durability > best.durability) best = it;
        }
        foreach (InventoryItem it in inv.GetBagItems())
            Consider(it);
        IReadOnlyList<InventorySlot> eq = inv.EquipmentSlots;
        if (eq != null)
            foreach (InventorySlot s in eq)
                if (s != null && s.heldItem != null)
                    Consider(s.heldItem.GetComponent<InventoryItem>());
        if (best == null)
            return;
        best.durability = Mathf.Max(0f, best.durability - uses * best.itemScriptableObject.DurabilityReductionPerUse);
        best.RefreshUI();
    }
}
