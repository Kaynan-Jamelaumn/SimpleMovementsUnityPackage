using UnityEngine;
using static HarvestBuild;

/// <summary>
/// The farm animals of Animal Husbandry, butchered with every mechanic the game's creatures use: a cow, a horse, a goat
/// and a pig (four-legged) and a chicken and a duck (birds). Their hides go to the tannery as hides the Crafting Database
/// has (a cow's and a horse's as a cow hide, a goat's as a deer hide, a pig's as a boar hide), so each pelt is laid out at
/// that hide's size; a bird's feathers are its hide. They name no mob types: no mob is ever taken for one (a wild pig stays
/// a boar) - only the species that name them by id use them.
/// <para>Each is built like the game animal whose body it shares (the cow like the bear, the horse like the deer, the
/// goat like the sheep, the pig like the boar, the birds like the eagle), its materials in the same order.</para>
/// </summary>
public static partial class HarvestDefaults
{
    private static readonly Color Down = new Color(0.94f, 0.92f, 0.86f);

    // ================================================================== four-legged farm animals
    /// <summary>A cow: a great hide for the tannery, a lot of beef, tallow, horn (polled breeds have none) and hooves.</summary>
    public static HarvestAnatomyData Cow()
    {
        HarvestAnatomyData a = New("cow", "Cow", HarvestBodyPlan.Quadruped, 600f, 1.4f, 13, 18,
            "A big, placid grazer: a great hide, a lot of beef, tallow and horn.");
        a.hideColor = new Color(0.55f, 0.4f, 0.3f);
        a.hairColor = new Color(0.4f, 0.28f, 0.2f);
        a.toughness = 1.1f;
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("hide", "Cow hide", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "hide").Hard(0.45f).Spoils(120f)
            .Worth(18f, 8f, 12f).Tannery("cow").Tint(new Color(0.5f, 0.36f, 0.26f)).Describe("Big and thick along the back: armour, soles, belts, saddles."));
        QuadrupedMeat(a, "Beef", 40f, 1.3f);
        a.materials.Add(Material("fat", "Beef tallow", HarvestMaterialKind.Fat, HarvestMethod.Butchering, "fat").From("belly", 1, 1f).From("back", 1, 0.5f)
            .Yield(3f, 6f, "slab").Hard(0.3f).Needs(HarvestMethods.Hide).Spoils(80f).Worth(2.5f, 0.8f, 4f).Food(12f).Tint(Fat)
            .Alchemy("oil", 3f, 0.6f).Describe("Renders into tallow: candles, soap, waterproofing."));
        QuadrupedTendons(a, "Cow", 3f);
        QuadrupedBones(a, "Cow", 2f);
        Organs(a, "Cow", 1.2f);
        a.materials.Add(Material("horns", "Cow horn", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "horn").From("head", 2).Count().Rare(0.7f, 0)
            .Hard(0.45f).Worth(6f, 0.6f, 6f).Tint(new Color(0.75f, 0.68f, 0.55f)).Describe("Drinking horns, combs, buttons. Polled breeds grow none."));
        a.materials.Add(Hooves());
        return a;
    }

    /// <summary>A horse: a big hide (the tannery works it as a cow hide), lean meat, long sinews and hooves.</summary>
    public static HarvestAnatomyData Horse()
    {
        HarvestAnatomyData a = New("horse", "Horse", HarvestBodyPlan.Quadruped, 500f, 1.6f, 13, 18,
            "A strong, swift grazer: a big hide, lean meat, long sinews and hooves.");
        a.hideColor = new Color(0.5f, 0.36f, 0.24f);
        a.hairColor = new Color(0.33f, 0.22f, 0.14f);
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("hide", "Horse hide", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "hide").Hard(0.45f).Spoils(120f)
            .Worth(16f, 7f, 12f).Tannery("cow").Tint(new Color(0.46f, 0.33f, 0.22f))
            .Describe("Close-grained and strong (the tannery works it like a cow hide): harness, belts, shoes."));
        QuadrupedMeat(a, "Horse", 34f, 1f);
        QuadrupedTendons(a, "Horse", 4f);
        QuadrupedBones(a, "Horse", 2f);
        Organs(a, "Horse", 1f);
        a.materials.Add(Hooves());
        return a;
    }

    /// <summary>A goat: a supple goatskin (the tannery works it as a deer hide), lean meat and horns - billies and nannies both carry them.</summary>
    public static HarvestAnatomyData Goat()
    {
        HarvestAnatomyData a = New("goat", "Goat", HarvestBodyPlan.Quadruped, 50f, 0.8f, 8, 10,
            "A hardy, nimble browser: a supple goatskin, lean meat, horns.");
        a.hideColor = new Color(0.72f, 0.64f, 0.54f);
        a.hairColor = new Color(0.82f, 0.78f, 0.7f);
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("goatskin", "Goatskin", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "skin").Hard(0.4f).Spoils(120f)
            .Worth(10f, 2.5f, 10f).Tannery("deer").Tint(new Color(0.78f, 0.72f, 0.62f))
            .Describe("Thin, supple and strong (the tannery works it like a deer hide): gloves, bags, drumheads, parchment."));
        QuadrupedMeat(a, "Goat", 6f, 1f);
        QuadrupedTendons(a, "Goat", 2f);
        QuadrupedBones(a, "Goat", 1.5f);
        Organs(a, "Goat", 0.8f);
        a.materials.Add(Material("horns", "Goat horn", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "horn").From("head", 2).Count()
            .Hard(0.45f).Worth(6f, 0.3f, 6f).Tint(new Color(0.55f, 0.48f, 0.38f)).Describe("Billies and nannies both carry them: handles, buttons, horns to blow."));
        a.materials.Add(Hooves());
        return a;
    }

    /// <summary>A farm pig: a tough pigskin (the tannery works it as a boar hide), pork and plenty of lard - no tusks.</summary>
    public static HarvestAnatomyData Pig()
    {
        HarvestAnatomyData a = New("pig", "Pig", HarvestBodyPlan.Quadruped, 110f, 0.9f, 9, 12,
            "A stout, clever farm hog: a tough pigskin, pork and plenty of lard.");
        a.hideColor = new Color(0.86f, 0.66f, 0.6f);
        a.hairColor = new Color(0.8f, 0.62f, 0.56f);
        a.toughness = 1.1f;
        a.regions = QuadrupedRegions();
        a.materials.Add(Material("hide", "Pigskin", HarvestMaterialKind.Hide, HarvestMethod.Skinning).From("back").Yield(1f, 1f, "hide").Hard(0.45f).Spoils(120f)
            .Worth(10f, 4f, 12f).Tannery("boar").Tint(new Color(0.84f, 0.64f, 0.58f))
            .Describe("Tough and porous (the tannery works it like a boar hide): gloves, saddles, book covers."));
        QuadrupedMeat(a, "Pork", 13f, 1.2f);
        a.materials.Add(Material("fat", "Lard", HarvestMaterialKind.Fat, HarvestMethod.Butchering, "fat").From("belly", 1, 1f).From("back", 1, 0.6f)
            .Yield(3f, 5f, "slab").Hard(0.3f).Needs(HarvestMethods.Hide).Spoils(80f).Worth(2.5f, 0.6f, 4f).Food(12f).Tint(Fat).Alchemy("oil", 2.5f, 0.6f));
        QuadrupedTendons(a, "Pig", 2f);
        QuadrupedBones(a, "Pig", 1.5f);
        Organs(a, "Pig", 1f);
        a.materials.Add(Hooves());
        return a;
    }

    /// <summary>A hoof from each leg (glue), as the deer's and the boar's.</summary>
    private static HarvestMaterial Hooves() =>
        Material("hooves", "Hoof", HarvestMaterialKind.Horn, HarvestMethod.Trophies, "claw").From("foreleg_l").From("foreleg_r").From("hindleg_l").From("hindleg_r")
            .Count().Hard(0.35f).Worth(1.5f, 0.2f, 3f).Tint(Horn).Describe("Boiled down for glue.");

    // ================================================================== farm birds
    /// <summary>A hen: feathers (its hide: plucked before the meat), breast and legs, a wishbone and the giblets. No gut to spill.</summary>
    public static HarvestAnatomyData Chicken()
    {
        HarvestAnatomyData a = New("chicken", "Chicken", HarvestBodyPlan.Avian, 2.5f, 0.45f, 8, 8,
            "A farmyard hen: feathers, breast and legs, and the giblets.");
        a.hideColor = new Color(0.96f, 0.86f, 0.76f);
        a.hairColor = new Color(0.82f, 0.56f, 0.32f);
        a.regions = AvianRegions();
        a.materials.Add(Material("feathers", "Chicken feathers", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers")
            .From("breast").From("belly", 1, 0.6f).From("wing_l", 1, 0.8f).From("wing_r", 1, 0.8f).From("tail", 1, 0.6f)
            .Yield(10f, 20f, "feather").Hard(0.3f).Worth(0.2f, 0.005f, 2f).Tint(new Color(0.85f, 0.6f, 0.36f))
            .Describe("Fletching, quills, and stuffing for pillows."));
        BirdMeat(a, "Chicken", 1.5f, 16f, 12f);
        a.materials.Add(Wishbone());
        Giblets(a, "Chicken", 1f);
        return a;
    }

    /// <summary>A duck: feathers and down (its hide), a rich breast and legs, duck fat, a wishbone and the giblets. No gut to spill.</summary>
    public static HarvestAnatomyData Duck()
    {
        HarvestAnatomyData a = New("duck", "Duck", HarvestBodyPlan.Avian, 3f, 0.4f, 8, 8,
            "A farmyard duck: feathers and down, a rich breast and legs, fat for the pan.");
        a.hideColor = new Color(0.94f, 0.84f, 0.74f);
        a.hairColor = new Color(0.9f, 0.9f, 0.86f);
        a.regions = AvianRegions();
        a.materials.Add(Material("feathers", "Duck feathers and down", HarvestMaterialKind.Feathers, HarvestMethod.Skinning, "feathers")
            .From("breast").From("belly").From("wing_l", 1, 0.8f).From("wing_r", 1, 0.8f).From("tail", 1, 0.5f)
            .Yield(10f, 18f, "feather").Hard(0.35f).Worth(0.35f, 0.005f, 2f).Tint(Down)
            .Describe("Soft down for quilts and pillows, and feathers for fletching."));
        BirdMeat(a, "Duck", 2f, 18f, 14f);
        a.materials.Add(Material("fat", "Duck fat", HarvestMaterialKind.Fat, HarvestMethod.Butchering, "fat").From("belly", 1, 1f).From("breast", 1, 0.4f)
            .Yield(1f, 2f, "portion").Hard(0.3f).Needs(HarvestMethods.Hide).Spoils(90f).Worth(1.5f, 0.2f, 3f).Food(10f).Tint(Fat)
            .Alchemy("oil", 1f, 0.6f).Describe("Rendered from under the skin: the best fat for roasting."));
        a.materials.Add(Wishbone());
        Giblets(a, "Duck", 1.2f);
        return a;
    }

    /// <summary>A farm bird's meat: its breast and its legs (freed once the feathers are off), worth <paramref name="value"/> for the breast.</summary>
    private static void BirdMeat(HarvestAnatomyData a, string prefix, float value, float breastHunger, float legHunger)
    {
        a.materials.Add(Material("breast", $"{prefix} breast", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("breast").Yield(1f, 2f).Hard(0.3f)
            .Needs(HarvestMethods.Hide).Spoils(40f).Worth(value, 0.3f, 4f).Food(breastHunger).Tint(Meat));
        a.materials.Add(Material("legs", $"{prefix} legs", HarvestMaterialKind.Meat, HarvestMethod.Butchering, "cut").From("leg_l").From("leg_r").Yield(1f, 2f).Hard(0.3f)
            .Needs(HarvestMethods.Hide).Spoils(40f).Worth(value * 0.8f, 0.25f, 3f).Food(legHunger).Tint(Meat));
    }

    private static HarvestMaterial Wishbone() =>
        Material("wishbone", "Wishbone", HarvestMaterialKind.Bone, HarvestMethod.Bones, "jaw").From("breast").Count().Hard(0.6f)
            .Needs(HarvestMethods.Hide).Worth(0.5f, 0.01f, 3f).Tint(Ivory).Describe("Fragile. Make a wish.");

    /// <summary>A farm bird's giblets: the heart, the liver and the gizzard - food, and none of them soils what is around it.</summary>
    private static void Giblets(HarvestAnatomyData a, string prefix, float value)
    {
        a.materials.Add(Material("heart", $"{prefix} heart", HarvestMaterialKind.Organ, HarvestMethod.Organs, "heart").From("breast").Count().Hard(0.45f)
            .Spoils(18f).Worth(value * 0.5f, 0.02f, 3f).Food(4f).Tint(Red));
        a.materials.Add(Material("liver", $"{prefix} liver", HarvestMaterialKind.Organ, HarvestMethod.Organs, "liver").From("belly").Count().Hard(0.45f)
            .Spoils(15f).Worth(value * 0.8f, 0.04f, 3f).Food(6f).Tint(Liver).Describe("A morsel for the pan."));
        a.materials.Add(Material("gizzard", $"{prefix} gizzard", HarvestMaterialKind.Organ, HarvestMethod.Organs, "stomach").From("belly").Count().Hard(0.45f)
            .Spoils(15f).Worth(value * 0.6f, 0.05f, 3f).Food(6f).Tint(Gut).Describe("The muscular stomach, full of grit: cleaned, it is good eating."));
    }
}
