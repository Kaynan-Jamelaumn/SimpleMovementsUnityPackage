using System.Collections.Generic;
using UnityEngine;

/// <summary>An item that is a material of a creature (a claw of a bear, the liver of a deer).</summary>
public interface IHarvestedItem
{
    /// <summary>The creature's anatomy id ("bear").</summary>
    string AnatomyId { get; }
    /// <summary>The material's id in that anatomy ("claws").</summary>
    string MaterialId { get; }
}

/// <summary>
/// What every harvested item shares: the value each copy keeps (<see cref="HarvestItemCode"/>: its quality and marks),
/// its tooltip lines, its price by quality and what it gives an alchemy vessel.
/// </summary>
public static class HarvestedItemInfo
{
    /// <summary>
    /// The value of a copy of <paramref name="instance"/> (0 = none kept): <paramref name="fromTop"/> 0 is the copy used,
    /// sold or shown next (the last of its values), 1 the one after it.
    /// </summary>
    public static int CodeOf(InventoryItem instance, int fromTop = 0)
    {
        if (instance == null || fromTop < 0)
            return 0;
        List<int> d = instance.DurabilityList;
        return d != null && fromTop < d.Count ? d[d.Count - 1 - fromTop] : 0;
    }

    /// <summary>The quality of a copy of <paramref name="instance"/> (the next one by default; copies without a value count as <see cref="HarvestItemCode.UnknownQuality"/>).</summary>
    public static double QualityOf(InventoryItem instance, int fromTop = 0) => HarvestItemCode.QualityOf(CodeOf(instance, fromTop));

    /// <summary>A copy's price factor: a ruined piece fetches a tenth, a pristine one the full price.</summary>
    public static float ValueFactor(InventoryItem instance, int fromTop = 0) => (float)HarvestItemCode.ValueFactor(QualityOf(instance, fromTop));

    /// <summary>The material it is (from the Harvest Database), or null.</summary>
    public static HarvestMaterial Material(IHarvestedItem item, out HarvestAnatomyData anatomy)
    {
        anatomy = item != null ? HarvestDatabase.Instance.Anatomy(item.AnatomyId) : null;
        return anatomy?.Material(item.MaterialId);
    }

    /// <summary>Lines about the material: what it is, where from, how it keeps.</summary>
    public static void ItemTooltip(IHarvestedItem item, List<string> lines)
    {
        HarvestMaterial m = Material(item, out HarvestAnatomyData a);
        if (m == null)
            return;
        lines.Add($"{HarvestText.KindName(m.kind)} · from {(a != null ? HarvestText.Capital(a.Name.ToLowerInvariant()) : "a creature")}");
        if (m.spoilMinutes > 0f)
            lines.Add($"<color=#9aa0a6>Perishable: on the carcass it starts to turn after about {HarvestText.Minutes(m.spoilMinutes * 0.55)} at 20 °C.</color>");
        if (m.alchemy != null && m.alchemy.Count > 0)
            lines.Add("<color=#9aa0a6>An alchemy ingredient.</color>");
    }

    /// <summary>
    /// "This piece · Fine (82%)", what marked it (the fight told apart from the harvest), and for a stack how its pieces
    /// grade ("3 Fine, 1 Poor").
    /// </summary>
    public static void InstanceTooltip(InventoryItem instance, List<string> lines)
    {
        if (instance == null)
            return;
        int code = CodeOf(instance);
        if (!HarvestItemCode.TryDecode(code, out double quality, out HarvestMarks marks))
            return;
        lines.Add($"<b>This piece</b> · {HarvestText.Quality(quality)}");
        string story = HarvestMarksInfo.Story(marks);
        if (story.Length > 0)
            lines.Add($"<color=#c9b38a>{story}.</color>");
        List<int> codes = instance.DurabilityList;
        if (instance.stackCurrent > 1 && codes != null && codes.Count > 1)
        {
            lines.Add($"<color=#9aa0a6>This stack: {HarvestItemCode.Summary(codes)}</color>");
            lines.Add("<color=#9aa0a6>Pieces of a stack keep their own quality: the next one is shown.</color>");
        }
    }

    /// <summary>What a piece of <paramref name="m"/> gives an alchemy vessel (null when it gives nothing).</summary>
    public static AlchemyIngredientProfile AlchemyProfile(HarvestMaterial m)
    {
        if (m?.alchemy == null || m.alchemy.Count == 0)
            return null;
        var p = new AlchemyIngredientProfile
        {
            dissolveSeconds = m.kind == HarvestMaterialKind.Fat ? 30f : 18f, form = "whole", canBeGround = m.kind != HarvestMaterialKind.Fat,
            hardness = m.kind == HarvestMaterialKind.Bone || m.kind == HarvestMaterialKind.Claw || m.kind == HarvestMaterialKind.Tooth ? 0.8f : 0.2f,
            temperature = 20f, observation = $"the {m.Name.ToLowerInvariant()} sinks and clouds the liquid",
        };
        foreach (HarvestAlchemyRelease r in m.alchemy)
            if (r != null && !string.IsNullOrWhiteSpace(r.substance) && r.amount > 0f)
                p.releases.Add(new IngredientRelease(r.substance.Trim(), r.amount, r.extractability));
        return p.releases.Count > 0 ? p : null;
    }

    /// <summary>A kind of crafting material for a harvested one (shops sort materials by it).</summary>
    public static MaterialKind MaterialKindOf(HarvestMaterialKind k)
    {
        switch (k)
        {
            case HarvestMaterialKind.Hide: return MaterialKind.Leather;
            case HarvestMaterialKind.Essence: return MaterialKind.Essence;
            default: return MaterialKind.MonsterPart;
        }
    }
}
