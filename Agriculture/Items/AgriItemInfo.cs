using System.Collections.Generic;
using UnityEngine;

/// <summary>An agricultural item: its key in the agriculture rules ("agri:seed/tomato", "agri:tool/hoe"...).</summary>
public interface IAgriItem
{
    string AgriKey { get; }
}

/// <summary>Produce of a plant (the species it comes from).</summary>
public interface IAgriProduce : IAgriItem
{
    string SpeciesId { get; }
}

/// <summary>Per-copy helpers of agricultural items (the codes live in each copy's durability list, the last one first).</summary>
public static class AgriItemInfo
{
    public static int CodeOf(InventoryItem stack, int fromTop = 0)
    {
        List<int> d = stack != null ? stack.DurabilityList : null;
        return d != null && fromTop < d.Count ? d[d.Count - 1 - fromTop] : 0;
    }

    public static string KeyOf(ItemSO item) => item is IAgriItem a ? a.AgriKey : null;

    public static float QualityOf(InventoryItem stack, int fromTop = 0) => AgriItemCode.QualityOf(CodeOf(stack, fromTop));

    public static float ValueFactor(InventoryItem stack, int fromTop = 0)
    {
        int code = CodeOf(stack, fromTop);
        return AgriItemCode.TryProduce(code, out float q, out float potency, out _, out bool rare)
            ? AgriItemCode.ValueFactor(q) * (1f + potency * 0.5f) * (rare ? 1.5f : 1f)
            : AgriItemCode.ValueFactor(AgriItemCode.UnknownQuality);
    }

    /// <summary>The tooltip lines of one copy of produce: its quality, and its potency when it is used for more than food.</summary>
    public static void ProduceTooltip(InventoryItem stack, PlantSpeciesData species, List<string> lines)
    {
        if (stack == null || lines == null) return;
        if (!AgriItemCode.TryProduce(CodeOf(stack), out float q, out float potency, out _, out bool rare))
        {
            lines.Add("Quality: unknown");
            return;
        }
        lines.Add($"Quality: {AgriItemCode.Grade(q)} ({Mathf.RoundToInt(q * 100f)}%)");
        PlantUses uses = species != null ? species.Produce.uses : PlantUses.None;
        if ((uses & (PlantUses.Medicinal | PlantUses.Alchemical | PlantUses.Magical)) != 0)
            lines.Add($"Potency: {(potency < 0.15f ? "faint" : potency < 0.35f ? "mild" : potency < 0.6f ? "strong" : potency < 0.85f ? "very strong" : "extraordinary")}");
        if (rare) lines.Add("<color=#d8c8ff>It has a strange lustre.</color>");
        if (stack.stackCurrent > 1)
        {
            List<int> codes = stack.DurabilityList;
            if (codes != null && codes.Count > 1)
            {
                float sum = 0f;
                int n = 0;
                foreach (int c in codes)
                    if (AgriItemCode.TryProduce(c, out float qq, out _, out _, out _)) { sum += qq; n++; }
                if (n > 1) lines.Add($"The stack: {AgriItemCode.Grade(sum / n).ToLowerInvariant()} on average");
            }
        }
    }

    /// <summary>The species of produce or seeds, from the Agriculture Database.</summary>
    public static PlantSpeciesData Species(string id) => string.IsNullOrEmpty(id) ? null : AgricultureDatabase.Instance.Species(id);

    /// <summary>The litres and the kind of water in one copy of a container.</summary>
    public static bool Water(InventoryItem stack, out float litres, out WaterSourceKind kind) => AgriItemCode.TryWater(CodeOf(stack), out litres, out kind);
}
