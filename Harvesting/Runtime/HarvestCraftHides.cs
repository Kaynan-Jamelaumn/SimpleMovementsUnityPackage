using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tannery hides of harvested creatures that no animal of the crafting system gives: troll hide and snakeskin. The
/// Crafting Database gets them from <i>Harvesting ▸ Add Harvested Hides to the Crafting Database</i> (and the harvesting
/// setup), so the crafting system's own defaults are left as they are.
/// </summary>
public static class HarvestCraftHides
{
    public static List<CraftHideDefinition> All() => new List<CraftHideDefinition>
    {
        Hide("snake", "Snakeskin", 18, 5, 1.2f, 0.9f, 0.7f, 0.85f, 0f, 0.15f, 0.03f, 18, "Thin and scaled: belts, wraps, sheaths.", new Color(0.45f, 0.5f, 0.3f)),
        Hide("troll", "Troll hide", 16, 12, 9f, 6f, 1.5f, 0.15f, 0.3f, 0.6f, 0.06f, 60, "Thick as a door: heavy leather armour that shrugs off blows.", new Color(0.4f, 0.45f, 0.36f)),
    };

    private static CraftHideDefinition Hide(string id, string name, int w, int h, float back, float belly, float fiber, float supple, float hair, float fat,
        float flaws, float price, string description, Color color) =>
        new CraftHideDefinition
        {
            id = id, displayName = name, width = w, height = h, thicknessBack = back, thicknessBelly = belly, fiberStrength = fiber, suppleness = supple,
            hair = hair, fat = fat, flaws = flaws, price = price, description = description, color = color,
        };
}
