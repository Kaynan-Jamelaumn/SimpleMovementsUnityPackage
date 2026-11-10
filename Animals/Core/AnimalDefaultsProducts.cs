using System.Collections.Generic;

/// <summary>
/// The built-in products (Phase 3): eggs from hens and ducks, milk from cows and goats, wool from sheep. Time is game hours
/// (a hen lays about an egg a day; a fleece takes two weeks: the game's animals live a few months).
/// </summary>
public static partial class AnimalDefaults
{
    /// <summary>What a built-in species gives while it lives (empty for the rest: a pig, a horse, a rabbit).</summary>
    public static List<AnimalProductSpec> ProductsOf(string species)
    {
        var list = new List<AnimalProductSpec>();
        switch (species)
        {
            case "chicken":
                list.Add(Product("egg", "Egg", "Eggs", "", "A hen's egg, laid in the nest box: how she lived the day it formed shows in it.", femaleOnly: true,
                    cycleHours: 26f, perCycle: 1, capacity: 6, freshHours: 48f, hygieneWeight: 0.25f, value: 3, verb: "Collect", tags: new[] { "egg", "animal_product", "food" }).Food(10f, false, "Egg Yolk").Trays());
                break;
            case "duck":
                list.Add(Product("duck_egg", "Duck egg", "Duck eggs", "", "A large, rich duck egg.", femaleOnly: true,
                    cycleHours: 30f, perCycle: 1, capacity: 6, freshHours: 48f, hygieneWeight: 0.25f, value: 4, verb: "Collect", tags: new[] { "egg", "animal_product", "food" }).Food(12f, false, "Egg Yolk").Trays());
                break;
            case "cow":
                list.Add(Product("milk", "Milk", "Milk", "jug", "Fresh cow's milk: a well fed, contented cow gives more, and better.", femaleOnly: true,
                    cycleHours: 8f, perCycle: 2, capacity: 6, freshHours: 0f, hygieneWeight: 0.25f, value: 6, verb: "Milk", yieldScales: true, handlingStress: 2f,
                    tags: new[] { "milk", "dairy", "animal_product", "food" }, tools: new[] { "milking" }).Food(10f, true, "Goat Milk"));
                break;
            case "goat":
                list.Add(Product("goat_milk", "Goat milk", "Goat milk", "jug", "Rich goat's milk.", femaleOnly: true,
                    cycleHours: 10f, perCycle: 1, capacity: 4, freshHours: 0f, hygieneWeight: 0.25f, value: 7, verb: "Milk", yieldScales: true, handlingStress: 2f,
                    tags: new[] { "milk", "dairy", "animal_product", "food" }, tools: new[] { "milking" }).Food(10f, true, "Goat Milk"));
                break;
            case "sheep":
                list.Add(Product("wool", "Wool", "Wool", "bundle", "Raw wool shorn from a sheep: a clean, well kept sheep grows a fine fleece, a dirty one a matted, cheap one.",
                    femaleOnly: false, cycleHours: 336f, perCycle: 3, capacity: 3, freshHours: 0f, hygieneWeight: 0.5f, value: 8, verb: "Shear", yieldScales: true,
                    handlingStress: 4f, tags: new[] { "wool", "fiber", "fleece", "animal_product" }, tools: new[] { "shears" }));
                break;
        }
        return list;
    }

    /// <summary>The tools collecting products needs: sheep shears and a milking pail (animal husbandry's own items).</summary>
    public static List<AnimalToolData> ProductTools() => new List<AnimalToolData>
    {
        OwnTool("shears", "Sheep shears", "Spring-handled shears to take a fleece off a sheep in one piece.", 0.7f, 120, 20, 0.4f, "shears"),
        OwnTool("milking_pail", "Milking pail", "A clean pail and stool for milking cows and goats.", 0.7f, 400, 12, 1.2f, "milking"),
    };

    /// <summary>It rolls into a pen's egg trays as it is laid (Phase 5: eggs).</summary>
    private static AnimalProductSpec Trays(this AnimalProductSpec p)
    {
        p.trays = true;
        return p;
    }

    /// <summary>
    /// What the built-in breeds are bred for (Phase 5): a laying hen lays more, a dairy cow or goat milks more, a wool sheep
    /// grows more wool; the rest give what their species gives (yield 1).
    /// </summary>
    public static void SpecialiseBreeds(AnimalSpeciesData s)
    {
        if (s?.breeds == null) return;
        foreach (AnimalBreedData b in s.breeds)
        {
            if (b == null) continue;
            switch (b.id)
            {
                case "leghorn": Yield(b, "egg", 1.2f); break;
                case "rhode_island_red": Yield(b, "egg", 1.05f); break;
                case "khaki_campbell": Yield(b, "duck_egg", 1.25f); break;
                case "holstein": Yield(b, "milk", 1.2f); break;
                case "jersey": Yield(b, "milk", 1.05f); break;
                case "saanen": Yield(b, "goat_milk", 1.2f); break;
                case "nubian": Yield(b, "goat_milk", 1.05f); break;
                case "merino": Yield(b, "wool", 1.25f); break;
                case "jacob": Yield(b, "wool", 1.05f); break;
                // Bred for meat or work: they give their kind's products a little more slowly.
                case "boer": Yield(b, "goat_milk", 0.85f); break;
                case "angus": Yield(b, "milk", 0.8f); break;
                case "suffolk": Yield(b, "wool", 0.85f); break;
            }
        }
    }

    private static void Yield(AnimalBreedData b, string product, float yield)
    {
        b.specialty = product;
        b.productYield = yield;
    }

    private static AnimalProductSpec Food(this AnimalProductSpec p, float nutrition, bool drink, string alchemyIngredient)
    {
        p.edible = true;
        p.nutrition = nutrition;
        p.drink = drink;
        p.alchemyIngredient = alchemyIngredient ?? "";
        return p;
    }

    private static AnimalProductSpec Product(string id, string name, string plural, string unit, string description, bool femaleOnly, float cycleHours, int perCycle,
        int capacity, float freshHours, float hygieneWeight, int value, string verb, string[] tags, bool yieldScales = false, float handlingStress = 0f,
        string[] tools = null)
    {
        var p = new AnimalProductSpec
        {
            id = id, name = name, plural = plural, unit = unit, description = description, femaleOnly = femaleOnly, cycleHours = cycleHours,
            perCycle = perCycle, capacity = capacity, freshHours = freshHours, hygieneWeight = hygieneWeight, value = value, verb = verb,
            yieldScales = yieldScales, handlingStress = handlingStress,
        };
        if (tags != null) p.tags.AddRange(tags);
        if (tools != null) p.tools.AddRange(tools);
        return p;
    }
}
