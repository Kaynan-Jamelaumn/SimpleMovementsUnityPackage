using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The items alchemy gives and takes: the bottle item of an outcome tier (its asset, else one made at runtime), the
/// item a harvested substance becomes, and runtime ingredient items from the built-in presets (free supplies).
/// Runtime items exist only on this machine: network sessions and saves need assets (Tools ▸ SimpleMovements ▸
/// Alchemy ▸ Create Potion Items / Create Ingredient Items).
/// </summary>
public static class AlchemyItems
{
    private static readonly Dictionary<string, BrewedPotionSO> runtimePotions = new Dictionary<string, BrewedPotionSO>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AlchemyIngredientSO> runtimeIngredients = new Dictionary<string, AlchemyIngredientSO>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        runtimePotions.Clear();
        runtimeIngredients.Clear();
    }

    /// <summary>
    /// The item a bottle of <paramref name="result"/> becomes: the tier's item, else (when <paramref name="allowRuntime"/>)
    /// an item made at runtime from the outcome's effects. Null when the outcome bottles nothing.
    /// </summary>
    public static ItemSO PotionItem(AlchemyRules rules, AlchemyResult result, bool allowRuntime = true)
    {
        if (result?.Outcome == null || !result.Outcome.producesItems)
            return null;
        AlchemyOutcomeTier tier = result.TierData;
        if (tier != null && tier.item != null)
            return tier.item;
        if (!allowRuntime)
            return null;
        int t = Math.Max(0, result.Tier);
        string key = result.Outcome.id + "#" + t;
        if (runtimePotions.TryGetValue(key, out BrewedPotionSO cached) && cached != null)
            return cached;
        Color color = result.LiquidColor.a > 0f ? result.LiquidColor : new Color(0.6f, 0.6f, 0.65f);
        BrewedPotionSO item = BrewedPotionSO.CreateRuntime(result.Outcome, t, rules, Typical(rules, result.Outcome, color));
        runtimePotions[key] = item;
        return item;
    }

    /// <summary>The colour of an outcome's potency substance (so every bottle of it looks alike), else <paramref name="fallback"/>.</summary>
    public static Color Typical(AlchemyRules rules, AlchemyOutcome outcome, Color fallback)
    {
        if (rules != null && outcome != null && outcome.HasPotency && outcome.potency.mode == SubstanceSelectorMode.Substance)
        {
            AlchemySubstance s = rules.Substance(outcome.potency.id);
            if (s != null) return s.color;
        }
        return fallback;
    }

    /// <summary>
    /// The item a harvested substance becomes: its Harvest Item, else (offline) a runtime ingredient of the same name
    /// (Moon Crystal), else the built-in ingredient it mainly comes from (settled brine is Rock Salt).
    /// </summary>
    public static ItemSO HarvestItem(AlchemySubstance s, bool allowRuntime = true)
    {
        if (s == null)
            return null;
        if (s.harvestItem != null)
            return s.harvestItem;
        if (!allowRuntime)
            return null;
        AlchemyIngredientSO byName = RuntimeIngredient(s.Name);
        if (byName != null)
            return byName;
        AlchemyDefaults.IngredientPreset source = AlchemyDefaults.MainSourceOf(s.id);
        return source != null ? RuntimeIngredient(source.name) : null;
    }

    /// <summary>A built-in ingredient preset as an item made at runtime (null when there is no such preset).</summary>
    public static AlchemyIngredientSO RuntimeIngredient(string presetNameOrId)
    {
        if (string.IsNullOrWhiteSpace(presetNameOrId))
            return null;
        if (runtimeIngredients.TryGetValue(presetNameOrId, out AlchemyIngredientSO cached) && cached != null)
            return cached;
        AlchemyDefaults.IngredientPreset p = AlchemyDefaults.Ingredient(presetNameOrId);
        if (p == null)
            return null;
        if (runtimeIngredients.TryGetValue(p.name, out cached) && cached != null)
            return cached;
        AlchemyIngredientSO item = AlchemyIngredientSO.CreateRuntime(p);
        runtimeIngredients[p.name] = item;
        runtimeIngredients[p.id] = item;
        return item;
    }

    /// <summary>Every built-in ingredient as a runtime item (free supplies for testing).</summary>
    public static List<AlchemyIngredientSO> AllRuntimeIngredients()
    {
        var list = new List<AlchemyIngredientSO>();
        foreach (AlchemyDefaults.IngredientPreset p in AlchemyDefaults.Ingredients())
        {
            AlchemyIngredientSO item = RuntimeIngredient(p.name);
            if (item != null) list.Add(item);
        }
        return list;
    }

    /// <summary>Was <paramref name="item"/> made at runtime (no asset: only this machine knows it)?</summary>
    public static bool IsRuntime(ItemSO item) => item != null && (item.hideFlags & HideFlags.DontSave) != 0;
}
