using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What every product item shares (eggs, milk and wool, food or material): its spec by id, the quality and the origin of a
/// copy, its value and its tooltip. A copy's code carries its quality and the serial of its lot in the world's product
/// ledger (see <see cref="AnimalItemCode.Product"/>): where the rules run (the server, or a game alone) the ledger tells
/// where the copy came from - its animal, its cycle, how the animal lived meanwhile; elsewhere only the quality is known.
/// Other systems (a cook, a crafter, a trader) read them through <see cref="QualityOf"/> and <see cref="OriginOf"/>.
/// </summary>
public static class AnimalProducts
{
    /// <summary>A product's spec by id, from any species of the Animal Database (null: no species gives it).</summary>
    public static AnimalProductSpec Spec(string product)
    {
        if (string.IsNullOrEmpty(product)) return null;
        foreach (AnimalSpeciesData sp in AnimalDatabase.Instance.AllSpecies)
        {
            AnimalProductSpec p = AnimalProduction.Spec(sp, product);
            if (p != null) return p;
        }
        return null;
    }

    /// <summary>The species that gives a product (null: none).</summary>
    public static AnimalSpeciesData SpeciesOf(string product)
    {
        foreach (AnimalSpeciesData sp in AnimalDatabase.Instance.AllSpecies)
            if (AnimalProduction.Spec(sp, product) != null) return sp;
        return null;
    }

    /// <summary>The quality of one copy (0-1); a copy without a code (bought, looted) is of middling quality.</summary>
    public static float QualityOf(InventoryItem stack, int fromTop = 0) =>
        AnimalItemCode.TryProduct(AnimalToolkit.CodeOf(stack, fromTop), out float q, out _) ? q : AnimalItemCode.UnknownQuality;

    /// <summary>The tier of one copy (None for a copy without a code).</summary>
    public static AnimalProductTier TierOf(InventoryItem stack, int fromTop = 0)
    {
        if (!AnimalItemCode.TryProduct(AnimalToolkit.CodeOf(stack, fromTop), out float q, out long serial, out bool rare)) return AnimalProductTier.None;
        AnimalProductRecord r = Record(serial, ProductOf(stack));
        return r != null ? r.tier : AnimalProduction.Tier(q, rare, AnimalDatabase.Instance.Rules.production);
    }

    /// <summary>
    /// Where one copy came from: its lot in the world's product ledger (its animal, cycle, means), or null - a copy without
    /// a code, a lot the ledger has let go, or a machine where the rules do not run (a network client).
    /// </summary>
    public static AnimalProductRecord OriginOf(InventoryItem stack, int fromTop = 0)
    {
        if (!AnimalItemCode.TryProduct(AnimalToolkit.CodeOf(stack, fromTop), out _, out long serial)) return null;
        return Record(serial, ProductOf(stack));
    }

    private static AnimalProductRecord Record(long serial, string product)
    {
        if (serial <= 0) return null;
        AnimalBook book = AnimalDirector.Book;
        return book?.Ledger.Find(serial, product);
    }

    private static string ProductOf(InventoryItem stack)
    {
        ItemSO item = stack != null ? stack.itemScriptableObject : null;
        return item is IAnimalProductItem p ? p.ProductId : null;
    }

    /// <summary>What a copy is worth relative to a perfect one (the curve feed, produce and harvested pieces use).</summary>
    public static float ValueFactor(InventoryItem stack, int fromTop = 0) => AnimalItemCode.ValueFactor(QualityOf(stack, fromTop));

    /// <summary>The lines of a copy's tooltip: its quality and tier, where it came from (where the ledger knows), the stack's mean.</summary>
    public static void Tooltip(InventoryItem stack, List<string> lines)
    {
        if (stack == null || lines == null) return;
        int code = AnimalToolkit.CodeOf(stack, 0);
        if (!AnimalItemCode.TryProduct(code, out float q, out long serial, out bool rare))
        {
            lines.Add("Quality: unknown");
            return;
        }
        AnimalProductRecord r = Record(serial, ProductOf(stack));
        AnimalProductTier tier = r != null ? r.tier : AnimalProduction.Tier(q, rare, AnimalDatabase.Instance.Rules.production);
        lines.Add($"Quality: {AnimalProduction.TierWord(tier)} ({Mathf.RoundToInt(q * 100f)}%)");
        if (r != null)
        {
            AnimalSpeciesData sp = AnimalDatabase.Instance.Species(r.species);
            AnimalBreedData br = sp?.Breed(r.breed);
            string kind = (br != null ? br.name + " " : "") + (sp != null ? sp.DisplayName.ToLowerInvariant() : r.species);
            string who = string.IsNullOrWhiteSpace(r.animalName) ? $"a {kind}" : $"{r.animalName}, a {kind}";
            lines.Add($"From {who} (lot {r.cycle})");
            lines.Add($"Its feeding {AnimalProduction.ConditionWord(r.nutrition)}, its welfare {AnimalProduction.ConditionWord(r.welfare)}, " +
                      $"kept {(r.hygiene >= 65f ? "clean" : r.hygiene >= 40f ? "fairly clean" : "dirty")}");
        }
        List<int> d = stack.DurabilityList;
        if (d != null && d.Count > 1)
        {
            float sum = 0f;
            int n = 0;
            foreach (int c in d)
                if (AnimalItemCode.TryProduct(c, out float cq, out _))
                {
                    sum += cq;
                    n++;
                }
            if (n > 1) lines.Add($"The stack: {AnimalProduction.TierWord(AnimalProduction.Tier(sum / n, false, AnimalDatabase.Instance.Rules.production))} on average");
        }
    }

    /// <summary>The Alchemy profile a product brews as (its spec's Alchemy Ingredient preset), or null.</summary>
    public static AlchemyIngredientProfile AlchemyProfileOf(AnimalProductSpec p)
    {
        if (p == null || string.IsNullOrWhiteSpace(p.alchemyIngredient)) return null;
        AlchemyIngredientSO ing = AlchemyItems.RuntimeIngredient(p.alchemyIngredient);
        return ing != null ? ing.AlchemyProfile : null;
    }

    /// <summary>The tooltip lines every product item shows of itself (not of a copy).</summary>
    public static void ItemTooltip(AnimalProductSpec p, List<string> lines)
    {
        if (p == null || lines == null) return;
        AnimalSpeciesData sp = SpeciesOf(p.id);
        if (sp != null) lines.Add($"From {sp.plural.ToLowerInvariant()}");
        lines.Add("Its quality shows how its animal lived while it was made");
    }
}
