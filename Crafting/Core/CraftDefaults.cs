using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The built-in crafting content: elements (iron, carbon, copper, tin, zinc, lead, silver, gold, nickel, manganese,
/// the impurities sulfur and phosphorus, silica, and mythril, starmetal and orichalcum), what they do in alloys, the
/// names of metals by composition, ores and their deposits, fuels, quenchants, vat agents, hides, woods, supplies,
/// abrasive tools, moulds, leather patterns and item designs. <see cref="Fill"/> puts it in a database (which can
/// then be edited); <see cref="Rules"/> compiles it (tests, tools, a project without a database).
/// </summary>
public static partial class CraftDefaults
{
    public const string Iron = "iron", Carbon = "carbon", Copper = "copper", Tin = "tin", Zinc = "zinc", Lead = "lead", Silver = "silver", Gold = "gold";
    public const string Nickel = "nickel", Manganese = "manganese", Sulfur = "sulfur", Phosphorus = "phosphorus", Silica = "silica";
    public const string Mythril = "mythril", Starmetal = "starmetal", Orichalcum = "orichalcum";

    /// <summary>Compiles the built-in content (a new copy each call: tests can change it freely).</summary>
    public static CraftRules Rules() => CraftRules.Compile(new CraftSimulationSettings(), Content());

    /// <summary>Puts the built-in content and settings into <paramref name="db"/>.</summary>
    public static void Fill(CraftDatabase db)
    {
        if (db == null)
            return;
        db.settings = new CraftSimulationSettings();
        db.content = Content();
    }

    public static CraftContent Content()
    {
        var c = new CraftContent();
        c.elements.AddRange(Elements());
        c.effects.AddRange(Effects());
        c.classes.AddRange(Classes());
        c.ores.AddRange(Ores());
        c.fuels.AddRange(Fuels());
        c.quenchants.AddRange(Quenchants());
        c.agents.AddRange(Agents());
        c.hides.AddRange(Hides());
        c.hides.AddRange(ArmouryCloths());
        c.woods.AddRange(Woods());
        c.woods.AddRange(ArmouryWoods());
        c.supplies.AddRange(Supplies());
        c.supplies.AddRange(ArmourySupplies());
        c.abrasives.AddRange(Abrasives());
        c.molds.AddRange(Molds());
        c.patterns.AddRange(Patterns());
        c.patterns.AddRange(ArmouryPatterns());
        c.designs.AddRange(Designs());
        return c;
    }

    // ------------------------------------------------------------------ elements
    private static CraftElement E(string id, string name, string symbol, CraftElementKind kind, Color color, float density, float melt, float hardness, float toughness,
        float ductility, float corrosion, float castability, float price, string observation)
    {
        return new CraftElement
        {
            id = id, displayName = name, symbol = symbol, kind = kind, color = color, density = density, meltingPoint = melt, hardness = hardness,
            toughness = toughness, ductility = ductility, corrosion = corrosion, castability = castability, pricePerGram = price, observation = observation,
        };
    }

    public static List<CraftElement> Elements()
    {
        var list = new List<CraftElement>();
        CraftElement e;
        e = E(Iron, "Iron", "Fe", CraftElementKind.Metal, new Color(0.55f, 0.56f, 0.58f), 7.87f, 1538, 90, 80, 40, 0.25f, 0.35f, 0.004f, "a grey metal that rusts");
        e.boilingPoint = 2860; e.oxidationOnset = 570; e.recrystallization = 550; e.hotWorkMin = 800; e.hotWorkMax = 1250; e.burnTemperature = 1450;
        e.grainGrowth = 1050; e.workHardening = 80; e.magnetic = true; e.curieTemperature = 770; e.slagAffinity = 0.6f; e.alwaysKnown = true;
        e.description = "The smith's metal. With a little carbon it is steel, which hardens in a quench.";
        list.Add(e);
        e = E(Carbon, "Carbon", "C", CraftElementKind.Alloying, new Color(0.1f, 0.1f, 0.1f), 2.2f, 3550, 0, 0, 0, 0, 0, 0, "it cannot be seen in the metal");
        e.boilingPoint = 4000; e.oxidationOnset = 4000; e.slagAffinity = 0; e.alwaysKnown = true;
        e.description = "From the charcoal. In iron: steel (hardenable) from about 0.3 %, cast iron above 2 %.";
        list.Add(e);
        e = E(Manganese, "Manganese", "Mn", CraftElementKind.Alloying, new Color(0.6f, 0.6f, 0.62f), 7.2f, 1246, 0, 0, 0, 0, 0, 0.01f, "");
        e.slagAffinity = 0.7f; e.description = "Comes in with some iron ores. Steel hardens deeper; it takes the sting out of sulfur.";
        list.Add(e);
        e = E(Nickel, "Nickel", "Ni", CraftElementKind.Metal, new Color(0.66f, 0.64f, 0.6f), 8.9f, 1455, 100, 90, 45, 0.6f, 0.4f, 0.02f, "a pale, slightly golden grey metal");
        e.magnetic = true; e.curieTemperature = 358; e.oxidationOnset = 800; e.oxidationRate = 0.3f; e.recrystallization = 600; e.hotWorkMin = 850; e.hotWorkMax = 1200;
        e.burnTemperature = 1380; e.slagAffinity = 0.35f; e.description = "Found with fallen stars. Tough, it brightens steel in a pattern.";
        list.Add(e);
        e = E(Silica, "Silica", "SiO₂", CraftElementKind.Impurity, new Color(0.85f, 0.85f, 0.82f), 2.65f, 1710, 0, 0, 0, 0, 0, 0, "");
        e.slagAffinity = 1; e.description = "Rock. In the furnace it becomes slag - and takes iron with it unless there is lime.";
        list.Add(e);
        e = E(Phosphorus, "Phosphorus", "P", CraftElementKind.Impurity, new Color(0.7f, 0.65f, 0.6f), 1.8f, 44, 0, 0, 0, 0, 0, 0, "");
        e.slagAffinity = 0.5f; e.description = "From bog ore. Hardens iron - and makes it brittle in the cold.";
        list.Add(e);
        e = E(Sulfur, "Sulfur", "S", CraftElementKind.Impurity, new Color(0.9f, 0.85f, 0.3f), 2.07f, 115, 0, 0, 0, 0, 0, 0, "");
        e.slagAffinity = 0.4f; e.description = "From sulfide ores and coal. Iron with it tears at forging heat; copper with it is a brittle matte.";
        list.Add(e);
        e = E(Copper, "Copper", "Cu", CraftElementKind.Metal, new Color(0.85f, 0.48f, 0.32f), 8.96f, 1085, 45, 60, 50, 0.55f, 0.55f, 0.01f, "a soft, rosy metal");
        e.boilingPoint = 2560; e.oxidationOnset = 300; e.oxidationRate = 0.7f; e.recrystallization = 300; e.hotWorkMin = 600; e.hotWorkMax = 950; e.burnTemperature = 1040;
        e.grainGrowth = 700; e.workHardening = 70; e.slagAffinity = 0.35f; e.alwaysKnown = true;
        e.description = "Soft and easy to work cold (it hardens as it is hammered; heat softens it again). The base of bronze and brass.";
        list.Add(e);
        e = E(Tin, "Tin", "Sn", CraftElementKind.Metal, new Color(0.8f, 0.8f, 0.82f), 7.3f, 232, 6, 15, 50, 0.7f, 0.7f, 0.03f, "a soft, bright metal that crackles when bent");
        e.boilingPoint = 2600; e.oxidationOnset = 200; e.oxidationRate = 0.4f; e.recrystallization = 20; e.hotWorkMin = 100; e.hotWorkMax = 200; e.burnTemperature = 225;
        e.grainGrowth = 200; e.workHardening = 2; e.slagAffinity = 0.4f;
        e.description = "Melts in a candle flame. A tenth of it in copper makes bronze.";
        list.Add(e);
        e = E(Zinc, "Zinc", "Zn", CraftElementKind.Metal, new Color(0.75f, 0.77f, 0.8f), 7.14f, 420, 35, 10, 20, 0.5f, 0.6f, 0.01f, "a bluish-grey metal");
        e.boilingPoint = 907; e.oxidationOnset = 300; e.recrystallization = 100; e.hotWorkMin = 100; e.hotWorkMax = 250; e.burnTemperature = 410; e.slagAffinity = 0.5f;
        e.description = "Boils away before copper melts: it is taken into copper from its ore under a cover (cementation) to make brass.";
        list.Add(e);
        e = E(Lead, "Lead", "Pb", CraftElementKind.Metal, new Color(0.45f, 0.46f, 0.5f), 11.34f, 327, 5, 10, 50, 0.75f, 0.8f, 0.005f, "a dull, very heavy, very soft metal");
        e.boilingPoint = 1749; e.oxidationOnset = 400; e.oxidationRate = 0.3f; e.recrystallization = 0; e.hotWorkMin = 50; e.hotWorkMax = 250; e.burnTemperature = 320;
        e.slagAffinity = 0.55f; e.description = "Carries silver out of its ore; a cupel takes the lead away and leaves the silver.";
        list.Add(e);
        e = E(Silver, "Silver", "Ag", CraftElementKind.Metal, new Color(0.92f, 0.92f, 0.93f), 10.49f, 962, 25, 50, 55, 0.8f, 0.7f, 0.5f, "a bright white metal");
        e.boilingPoint = 2162; e.oxidationOnset = 4000; e.recrystallization = 200; e.hotWorkMin = 500; e.hotWorkMax = 800; e.burnTemperature = 930; e.grainGrowth = 600;
        e.workHardening = 55; e.slagAffinity = 0.05f; e.supernatural = 0.25f; e.description = "Soft and noble; the dead do not like it.";
        list.Add(e);
        e = E(Gold, "Gold", "Au", CraftElementKind.Metal, new Color(1f, 0.82f, 0.35f), 19.3f, 1064, 25, 45, 60, 1f, 0.8f, 4f, "a heavy yellow metal that never tarnishes");
        e.boilingPoint = 2970; e.oxidationOnset = 4000; e.recrystallization = 200; e.hotWorkMin = 500; e.hotWorkMax = 900; e.burnTemperature = 1030; e.workHardening = 45;
        e.slagAffinity = 0; e.magic = 0.15f; e.description = "Never tarnishes, never scales. Natural gold carries silver (electrum).";
        list.Add(e);
        e = E(Mythril, "Mythril", "Mi", CraftElementKind.Fantasy, new Color(0.72f, 0.85f, 0.98f), 3.2f, 1820, 320, 120, 30, 0.95f, 0.4f, 2f, "a pale blue, feather-light metal with a pearly sheen");
        e.boilingPoint = 4000; e.oxidationOnset = 4000; e.recrystallization = 900; e.hotWorkMin = 1100; e.hotWorkMax = 1500; e.burnTemperature = 1700; e.grainGrowth = 1400;
        e.workHardening = 120; e.slagAffinity = 0.1f; e.magic = 0.9f; e.affinity = ElementType.Lightning; e.affinityStrength = 0.5f;
        e.description = "Light as aluminium, hard as steel, carries magic. Only a very hot furnace with an arcane flux reduces its ore.";
        list.Add(e);
        e = E(Starmetal, "Starmetal", "St", CraftElementKind.Fantasy, new Color(0.42f, 0.44f, 0.52f), 8.5f, 1600, 140, 110, 30, 0.8f, 0.35f, 1.5f, "a dark metal with a faint inner shimmer, like a night sky");
        e.oxidationOnset = 700; e.oxidationRate = 0.4f; e.recrystallization = 650; e.hotWorkMin = 850; e.hotWorkMax = 1250; e.burnTemperature = 1450; e.supernatural = 0.9f;
        e.affinity = ElementType.Holy; e.affinityStrength = 0.6f; e.slagAffinity = 0.2f; e.magnetic = true;
        e.description = "From fallen stars. A little of it in steel makes it harden deep and bite what is not of this world.";
        list.Add(e);
        e = E(Orichalcum, "Orichalcum", "Or", CraftElementKind.Fantasy, new Color(0.95f, 0.62f, 0.3f), 8.2f, 1050, 90, 70, 35, 0.9f, 0.75f, 1.2f, "a fiery red-gold metal, warm to the touch");
        e.oxidationOnset = 900; e.oxidationRate = 0.3f; e.recrystallization = 450; e.hotWorkMin = 650; e.hotWorkMax = 950; e.burnTemperature = 1000; e.workHardening = 60;
        e.magic = 0.4f; e.affinity = ElementType.Fire; e.affinityStrength = 0.8f; e.slagAffinity = 0.2f;
        e.description = "The fire-metal of legend. Alloyed into bronze it lends its fire.";
        list.Add(e);
        return list;
    }

    // ------------------------------------------------------------------ alloy effects
    private static CraftAlloyEffect Fx(string b, string el, CraftProperty p, float peak, float value, float slope = 0f, string note = "") =>
        new CraftAlloyEffect(b, el, p, peak, value, slope, note);

    private static CraftAlloyEffect Tint(string b, string el, float peak, float strength, Color tint)
    {
        var e = new CraftAlloyEffect(b, el, CraftProperty.Tint, peak, strength, 0f);
        e.tint = tint;
        return e;
    }

    public static List<CraftAlloyEffect> Effects()
    {
        return new List<CraftAlloyEffect>
        {
            // Iron and steel (carbon's hardening is the steel model's).
            Fx(Iron, Carbon, CraftProperty.Toughness, 0.002f, 5, -4500, "carbon makes iron stronger but less tough"),
            Fx(Iron, Carbon, CraftProperty.Ductility, 0.001f, 0, -3000),
            Fx(Iron, Carbon, CraftProperty.MeltingPoint, 0.043f, -390, 0, "carbon melts iron lower: cast iron runs at 1150 °C"),
            Fx(Iron, Carbon, CraftProperty.Castability, 0.04f, 0.55f),
            Fx(Iron, Manganese, CraftProperty.Hardenability, 0.015f, 1.0f, 0, "manganese: deeper hardening"),
            Fx(Iron, Manganese, CraftProperty.HotShortness, 0.01f, -0.6f, 0, "manganese ties up sulfur"),
            Fx(Iron, Manganese, CraftProperty.Toughness, 0.01f, 5),
            Fx(Iron, Nickel, CraftProperty.Hardenability, 0.03f, 0.8f),
            Fx(Iron, Nickel, CraftProperty.Toughness, 0.05f, 30, 0, "nickel toughens steel"),
            Fx(Iron, Nickel, CraftProperty.Corrosion, 0.1f, 0.2f),
            Fx(Iron, Nickel, CraftProperty.Hardness, 0.05f, 30),
            Fx(Iron, Phosphorus, CraftProperty.Hardness, 0.003f, 60, 4000, "phosphoric iron is hard"),
            Fx(Iron, Phosphorus, CraftProperty.ColdShortness, 0.004f, 0.25f, 120, "phosphorus: brittle when cold"),
            Fx(Iron, Phosphorus, CraftProperty.Toughness, 0.003f, -15, -4000),
            Fx(Iron, Sulfur, CraftProperty.HotShortness, 0.002f, 0.5f, 300, "sulfur: it tears at forging heat"),
            Fx(Iron, Sulfur, CraftProperty.Toughness, 0.003f, -10, -1000),
            Fx(Iron, Copper, CraftProperty.HotShortness, 0.005f, 0.3f, 20),
            Fx(Iron, Starmetal, CraftProperty.Hardenability, 0.08f, 2f, 0, "starmetal: steel hardens through"),
            Fx(Iron, Starmetal, CraftProperty.Toughness, 0.1f, 25),
            Fx(Iron, Starmetal, CraftProperty.Supernatural, 0.05f, 0.4f),
            Fx(Iron, Mythril, CraftProperty.Hardness, 0.15f, 80),
            Fx(Iron, Mythril, CraftProperty.Toughness, 0.15f, 30),
            Fx(Iron, Mythril, CraftProperty.Magic, 0.2f, 0.5f),
            Fx(Iron, Mythril, CraftProperty.Corrosion, 0.2f, 0.4f),
            Fx(Iron, Orichalcum, CraftProperty.HotShortness, 0.05f, 0.3f, 2, "orichalcum and iron do not agree"),
            Tint(Iron, Nickel, 0.08f, 0.2f, new Color(0.7f, 0.7f, 0.66f)),
            // Bronze, brass and other copper alloys.
            Fx(Copper, Tin, CraftProperty.Hardness, 0.25f, 120, -250, "tin hardens copper into bronze"),
            Fx(Copper, Tin, CraftProperty.Toughness, 0.06f, 15, -380, "too much tin and bronze is brittle (bell metal)"),
            Fx(Copper, Tin, CraftProperty.Ductility, 0.02f, 0, -220),
            Fx(Copper, Tin, CraftProperty.MeltingPoint, 0.15f, -80),
            Fx(Copper, Tin, CraftProperty.Castability, 0.1f, 0.25f),
            Fx(Copper, Tin, CraftProperty.Corrosion, 0.1f, 0.2f),
            Fx(Copper, Tin, CraftProperty.WorkHardening, 0.1f, 60, 0, "bronze hardens a lot when hammered cold"),
            Tint(Copper, Tin, 0.1f, 0.6f, new Color(0.85f, 0.65f, 0.35f)),
            Fx(Copper, Zinc, CraftProperty.Hardness, 0.3f, 40),
            Fx(Copper, Zinc, CraftProperty.Ductility, 0.3f, 10, -100),
            Fx(Copper, Zinc, CraftProperty.Toughness, 0.3f, 10),
            Fx(Copper, Zinc, CraftProperty.Castability, 0.3f, 0.2f),
            Fx(Copper, Zinc, CraftProperty.MeltingPoint, 0.35f, 60),
            Tint(Copper, Zinc, 0.3f, 0.8f, new Color(0.92f, 0.78f, 0.38f)),
            Fx(Copper, Lead, CraftProperty.Castability, 0.05f, 0.2f),
            Fx(Copper, Lead, CraftProperty.Toughness, 0.05f, -20, -200),
            Fx(Copper, Nickel, CraftProperty.Hardness, 0.2f, 40),
            Fx(Copper, Nickel, CraftProperty.Corrosion, 0.2f, 0.3f),
            Tint(Copper, Nickel, 0.25f, 0.8f, new Color(0.85f, 0.84f, 0.8f)),
            Fx(Copper, Orichalcum, CraftProperty.Hardness, 0.2f, 60),
            Fx(Copper, Orichalcum, CraftProperty.Corrosion, 0.2f, 0.3f),
            Fx(Copper, Orichalcum, CraftProperty.Magic, 0.2f, 0.25f),
            Tint(Copper, Orichalcum, 0.2f, 0.5f, new Color(1f, 0.6f, 0.3f)),
            Fx(Copper, Sulfur, CraftProperty.ColdShortness, 0.02f, 0.7f, 0, "copper with sulfur is a brittle matte"),
            Fx(Copper, Sulfur, CraftProperty.Toughness, 0.02f, -40, -100),
            Fx(Copper, Iron, CraftProperty.Hardness, 0.03f, 20),
            // Silver, gold, tin, mythril.
            Fx(Silver, Copper, CraftProperty.Hardness, 0.075f, 55, 200, "a little copper hardens silver (sterling)"),
            Fx(Silver, Copper, CraftProperty.Toughness, 0.075f, 10),
            Fx(Silver, Copper, CraftProperty.Corrosion, 0.1f, -0.15f),
            Fx(Gold, Silver, CraftProperty.Hardness, 0.3f, 30),
            Tint(Gold, Silver, 0.35f, 0.6f, new Color(0.95f, 0.9f, 0.6f)),
            Fx(Gold, Copper, CraftProperty.Hardness, 0.25f, 90),
            Fx(Gold, Copper, CraftProperty.Toughness, 0.25f, 10),
            Tint(Gold, Copper, 0.25f, 0.7f, new Color(0.95f, 0.6f, 0.5f)),
            Fx(Tin, Copper, CraftProperty.Hardness, 0.03f, 20),
            Fx(Tin, Lead, CraftProperty.Hardness, 0.2f, 4),
            Fx(Mythril, Iron, CraftProperty.Hardness, 0.2f, 40),
            Fx(Mythril, Silver, CraftProperty.QuenchHardness, 0.1f, 220, -800, "a little silver lets a quench make mythril very hard"),
            Fx(Mythril, Silver, CraftProperty.Magic, 0.1f, 0.05f),
            Fx(Mythril, Starmetal, CraftProperty.Supernatural, 0.1f, 0.4f),
            Fx(Orichalcum, Copper, CraftProperty.Toughness, 0.2f, 15),
        };
    }

    // ------------------------------------------------------------------ names by composition
    private static CraftMaterialClass Class(string id, string name, string baseId, int priority, string observation, float price = 1f, string description = "") =>
        new CraftMaterialClass { id = id, displayName = name, baseElement = baseId, priority = priority, observation = observation, priceMultiplier = price, description = description };

    public static List<CraftMaterialClass> Classes()
    {
        return new List<CraftMaterialClass>
        {
            Class("cast_iron", "Cast iron", Iron, 100, "a coarse grey metal that sparks in short dull streaks", 0.6f, "Too much carbon: it casts well and cannot be forged.").With(Carbon, 0.021f, 1),
            Class("starmetal_steel", "Starmetal steel", Iron, 99, "a steel with a faint shimmer, as if lit from inside", 6f, "Steel with fallen-star metal in it.").With(Starmetal, 0.03f, 1).With(Carbon, 0.0025f, 0.021f),
            Class("meteoric_iron", "Meteoric iron", Iron, 98, "a dark iron with a shimmer and bright nickel streaks", 5f, "Fallen-star iron: nickel and starmetal.").With(Nickel, 0.04f, 0.2f).With(Starmetal, 0.01f, 1).With(Carbon, 0, 0.0025f),
            Class("mythril_steel", "Mythril steel", Iron, 97, "a pale steel, lighter in the hand than it looks", 8f).With(Mythril, 0.05f, 0.5f),
            Class("phosphoric_iron", "Phosphoric iron", Iron, 95, "a bright, hard iron that rings sharply", 1f, "Iron from bog ore: hard, and brittle in the cold.").With(Phosphorus, 0.002f, 1).With(Carbon, 0, 0.003f),
            Class("nickel_steel", "Nickel steel", Iron, 92, "a steel that etches bright", 2.5f).With(Nickel, 0.01f, 0.1f).With(Carbon, 0.0025f, 0.021f),
            Class("wrought_iron", "Wrought iron", Iron, 90, "a soft, fibrous grey iron that sparks in long straw streaks", 1f, "Iron with hardly any carbon: tough, soft, it does not harden.").With(Carbon, 0, 0.0008f),
            Class("mild_steel", "Mild steel", Iron, 80, "a grey metal that sparks in long streaks with a few forks", 1.3f, "Low-carbon steel: tough; it hardly hardens.").With(Carbon, 0.0008f, 0.003f),
            Class("medium_steel", "Medium-carbon steel", Iron, 80, "a grey metal that sparks with small bursting stars", 1.8f, "Hardens well, stays tough: axes, hammers, springy blades.").With(Carbon, 0.003f, 0.006f),
            Class("high_carbon_steel", "High-carbon steel", Iron, 80, "a grey metal that throws a bushy shower of stars", 2.4f, "Hardens very hard: keen edges, if it is tempered.").With(Carbon, 0.006f, 0.012f),
            Class("ultra_high_carbon_steel", "Ultra-high-carbon steel", Iron, 80, "a steel that throws a dense shower of bursting stars", 2.4f, "The hardest steel - and the most brittle.").With(Carbon, 0.012f, 0.021f),
            Class("copper_matte", "Copper matte", Copper, 99, "a dark, brittle, metallic lump that smells of sulfur", 0.3f, "Copper still full of sulfur: roast it and smelt it again.").With(Sulfur, 0.02f, 1),
            Class("orichalcum_bronze", "Orichalcum bronze", Copper, 95, "a red-gold bronze, warm to the touch", 8f).With(Orichalcum, 0.05f, 0.5f),
            Class("nickel_silver", "Nickel silver", Copper, 92, "a silvery metal that is not silver", 1.5f).With(Nickel, 0.1f, 0.3f).With(Zinc, 0.1f, 0.35f),
            Class("bell_bronze", "Bell bronze", Copper, 90, "a pale golden metal that rings like a bell - and snaps", 1.4f, "Too much tin: very hard, very brittle.").With(Tin, 0.16f, 0.35f),
            Class("leaded_bronze", "Leaded bronze", Copper, 88, "a dull golden metal", 1.2f, "Casts well; softer.").With(Tin, 0.04f, 0.16f).With(Lead, 0.03f, 0.3f),
            Class("bronze", "Bronze", Copper, 85, "a golden-brown metal", 1.6f, "Copper with about a tenth of tin: hard, tough, it casts well and hardens when hammered cold.").With(Tin, 0.04f, 0.16f),
            Class("brass", "Brass", Copper, 85, "a bright yellow metal", 1.5f, "Copper and zinc.").With(Zinc, 0.1f, 0.45f),
            Class("copper_metal", "Copper", Copper, 50, "a soft, rosy metal", 1f).With(Tin, 0, 0.04f).With(Zinc, 0, 0.1f),
            Class("pewter", "Pewter", Tin, 80, "a soft, dull grey metal", 1.2f).With(Copper, 0.005f, 0.1f),
            Class("tin_metal", "Tin", Tin, 50, "a soft, bright metal that crackles when bent"),
            Class("silver_lead", "Silver-bearing lead", Lead, 80, "a soft, heavy grey metal", 2f, "Lead carrying silver: cupel it.").With(Silver, 0.0005f, 0.3f),
            Class("lead_metal", "Lead", Lead, 50, "a dull, heavy, soft metal"),
            Class("sterling_silver", "Sterling silver", Silver, 80, "a bright white metal, harder than silver", 1f).With(Copper, 0.05f, 0.1f),
            Class("coin_silver", "Coin silver", Silver, 75, "a white metal with a warm cast", 0.9f).With(Copper, 0.1f, 0.25f),
            Class("fine_silver", "Fine silver", Silver, 60, "a soft, bright white metal", 1.1f).With(Copper, 0, 0.05f),
            Class("electrum", "Electrum", Gold, 85, "a pale yellow-white metal", 0.7f, "Gold and silver together, as rivers give it.").With(Silver, 0.15f, 0.5f),
            Class("rose_gold", "Rose gold", Gold, 85, "a pinkish gold", 0.8f).With(Copper, 0.15f, 0.35f),
            Class("fine_gold", "Fine gold", Gold, 60, "a deep yellow metal", 1.1f).With(Silver, 0, 0.02f).With(Copper, 0, 0.02f),
            Class("gold_metal", "Gold", Gold, 40, "a yellow metal"),
            Class("mythril_silver", "Mythril silver", Mythril, 80, "a pale blue metal with a silver sheen", 1.3f).With(Silver, 0.03f, 0.2f),
            Class("mythril_metal", "Mythril", Mythril, 50, "a pale blue, feather-light metal"),
            Class("zinc_metal", "Zinc", Zinc, 50, "a bluish-grey metal"),
            Class("nickel_metal", "Nickel", Nickel, 50, "a pale grey metal"),
            Class("starmetal_metal", "Starmetal", Starmetal, 50, "a dark metal with a faint inner shimmer"),
            Class("orichalcum_metal", "Orichalcum", Orichalcum, 50, "a fiery red-gold metal"),
        };
    }

    // ------------------------------------------------------------------ ores
    private static CraftOreDefinition Ore(string id, string name, string observation, Color color, string luster, string streak, Color streakColor, float density,
        float hardness, float mineralMin, float mineralMax, CraftGangue gangue, float reduction, float rate, float price, string description)
    {
        return new CraftOreDefinition
        {
            id = id, displayName = name, observation = observation, color = color, luster = luster, streak = streak, streakColor = streakColor, density = density,
            hardness = hardness, mineralMin = mineralMin, mineralMax = mineralMax, gangue = gangue, reductionTemperature = reduction, reductionRate = rate, price = price,
            description = description, gangueColor = gangue == CraftGangue.Clay ? new Color(0.6f, 0.52f, 0.42f) : gangue == CraftGangue.Limestone ? new Color(0.82f, 0.8f, 0.72f) : new Color(0.75f, 0.74f, 0.72f),
        };
    }

    public static List<CraftOreDefinition> Ores()
    {
        var list = new List<CraftOreDefinition>();
        CraftOreDefinition o;
        o = Ore("hematite", "Red iron ore", "a heavy, rust-red rock that leaves a red-brown streak", new Color(0.55f, 0.22f, 0.18f), "earthy to metallic", "red-brown",
            new Color(0.55f, 0.25f, 0.2f), 5.2f, 6f, 0.35f, 0.7f, CraftGangue.Quartz, 1050, 1f, 3f, "Hematite. The common iron ore: rich, with quartz around it.");
        o.Yields(Iron, 0.6f, 0.68f).Yields(Silica, 0.02f, 0.08f).Yields(Phosphorus, 0.0005f, 0.003f).Yields(Sulfur, 0.0002f, 0.002f).Yields(Manganese, 0.001f, 0.01f);
        list.Add(o);
        o = Ore("magnetite", "Black iron ore", "a heavy black rock that a lodestone pulls", new Color(0.15f, 0.15f, 0.17f), "metallic", "black", new Color(0.12f, 0.12f, 0.12f),
            5.15f, 6f, 0.45f, 0.8f, CraftGangue.Quartz, 1100, 0.8f, 4f, "Magnetite. Rich and clean, slow to reduce.");
        o.magnetic = true;
        o.Yields(Iron, 0.65f, 0.71f).Yields(Silica, 0.01f, 0.05f).Yields(Phosphorus, 0.0002f, 0.001f).Yields(Sulfur, 0.0001f, 0.001f);
        list.Add(o);
        o = Ore("bog_iron", "Bog iron", "a light, yellow-brown, crumbly lump from a marsh", new Color(0.55f, 0.4f, 0.2f), "earthy", "yellow-brown", new Color(0.65f, 0.5f, 0.25f),
            3.8f, 4f, 0.4f, 0.75f, CraftGangue.Clay, 950, 1.3f, 1.5f, "Limonite from the bogs: easy to smelt, but full of phosphorus.");
        o.dirtMin = 0.08f; o.dirtMax = 0.2f;
        o.Yields(Iron, 0.45f, 0.58f).Yields(Silica, 0.04f, 0.12f).Yields(Phosphorus, 0.004f, 0.012f).Yields(Sulfur, 0.0002f, 0.001f).Yields(Manganese, 0.005f, 0.02f);
        list.Add(o);
        o = Ore("malachite", "Green copper ore", "a banded, bright green stone", new Color(0.18f, 0.55f, 0.35f), "silky", "pale green", new Color(0.55f, 0.8f, 0.6f),
            3.9f, 3.8f, 0.25f, 0.6f, CraftGangue.Limestone, 900, 1.4f, 5f, "Malachite. Smelts easily into clean copper; it often sits in limestone.");
        o.Yields(Copper, 0.5f, 0.57f).Yields(Silica, 0.01f, 0.05f).Yields(Iron, 0.005f, 0.02f);
        list.Add(o);
        o = Ore("chalcopyrite", "Brassy copper ore", "a brassy yellow, glittering rock that smells of sulfur when struck", new Color(0.78f, 0.68f, 0.3f), "metallic", "greenish black",
            new Color(0.2f, 0.25f, 0.18f), 4.2f, 3.7f, 0.3f, 0.6f, CraftGangue.Quartz, 1100, 1f, 4f, "Chalcopyrite: copper and iron bound with sulfur. Roast it, or it gives matte.");
        o.sulfide = true; o.roastTemperature = 650;
        o.Yields(Copper, 0.3f, 0.35f).Yields(Iron, 0.28f, 0.31f).Yields(Sulfur, 0.33f, 0.35f).Yields(Silica, 0.01f, 0.04f);
        list.Add(o);
        o = Ore("cassiterite", "Tin stone", "a dark brown stone, very heavy for its size, with a greasy shine", new Color(0.25f, 0.17f, 0.12f), "adamantine", "pale brown",
            new Color(0.8f, 0.7f, 0.6f), 6.9f, 6.5f, 0.15f, 0.45f, CraftGangue.Quartz, 1000, 0.9f, 8f, "Cassiterite. Heavy: it stays in the pan when the rock washes away.");
        o.Yields(Tin, 0.7f, 0.78f).Yields(Iron, 0.005f, 0.03f).Yields(Silica, 0.01f, 0.04f);
        list.Add(o);
        o = Ore("galena", "Lead ore", "heavy grey cubes with a bright metallic shine", new Color(0.5f, 0.52f, 0.56f), "metallic", "lead-grey", new Color(0.4f, 0.42f, 0.45f),
            7.5f, 2.6f, 0.3f, 0.7f, CraftGangue.Quartz, 800, 1.2f, 3f, "Galena: lead and sulfur, and a little silver. Roast it, smelt it, cupel the lead.");
        o.sulfide = true; o.roastTemperature = 600;
        o.Yields(Lead, 0.84f, 0.86f).Yields(Sulfur, 0.13f, 0.135f).Yields(Silver, 0.0005f, 0.006f);
        list.Add(o);
        o = Ore("native_silver", "Native silver", "black-tarnished silvery wires in white rock", new Color(0.6f, 0.6f, 0.62f), "metallic", "silver-white", new Color(0.85f, 0.85f, 0.85f),
            10.5f, 2.7f, 0.1f, 0.35f, CraftGangue.Quartz, 960, 1f, 20f, "Silver as it grows in the rock: melt it out.");
        o.native = true;
        o.Yields(Silver, 0.88f, 0.98f).Yields(Copper, 0.005f, 0.05f).Yields(Gold, 0f, 0.01f);
        list.Add(o);
        o = Ore("gold_quartz", "Gold-bearing quartz", "white quartz with specks of yellow metal", new Color(1f, 0.82f, 0.35f), "metallic in glassy rock", "gold", new Color(1f, 0.82f, 0.35f),
            19f, 2.8f, 0.01f, 0.06f, CraftGangue.Quartz, 1065, 1f, 15f, "Gold in quartz: crush it, wash it, melt the gold out. Natural gold carries silver.");
        o.native = true; o.lumpMass = 1500;
        o.Yields(Gold, 0.75f, 0.92f).Yields(Silver, 0.06f, 0.22f).Yields(Copper, 0f, 0.02f);
        list.Add(o);
        o = Ore("calamine", "Calamine", "a pale, porous, honey-coloured stone", new Color(0.75f, 0.72f, 0.6f), "earthy", "white", new Color(0.92f, 0.92f, 0.9f),
            4.4f, 4.5f, 0.3f, 0.65f, CraftGangue.Limestone, 950, 1f, 4f, "Zinc ore. Its zinc boils away in a furnace: heat it in a covered crucible with molten copper and the copper drinks it (brass).");
        o.Yields(Zinc, 0.45f, 0.52f).Yields(Iron, 0.01f, 0.04f).Yields(Silica, 0.01f, 0.05f);
        list.Add(o);
        o = Ore("meteorite", "Fallen star", "a heavy, black, pitted lump with a rusty crust; a lodestone pulls it", new Color(0.18f, 0.17f, 0.17f), "metallic under the crust", "grey",
            new Color(0.4f, 0.4f, 0.42f), 7.5f, 5f, 0.75f, 0.95f, CraftGangue.Quartz, 1000, 1f, 40f, "Iron from the sky, with nickel and starmetal. Heat it and it welds into a bloom.");
        o.native = true; o.magnetic = true; o.dirtMin = 0.01f; o.dirtMax = 0.04f; o.lumpMass = 2500;
        o.Yields(Iron, 0.85f, 0.9f).Yields(Nickel, 0.06f, 0.1f).Yields(Starmetal, 0.02f, 0.05f).Yields(Phosphorus, 0.001f, 0.003f).Yields(Carbon, 0.0003f, 0.001f);
        list.Add(o);
        o = Ore("mythril_ore", "Mythril vein", "grey rock shot with pale blue, glimmering veins", new Color(0.55f, 0.72f, 0.9f), "pearly metallic", "silver-blue",
            new Color(0.7f, 0.8f, 0.95f), 4.5f, 7f, 0.2f, 0.5f, CraftGangue.Quartz, 1350, 0.6f, 60f, "It will not give up its metal without a very hot furnace and an arcane flux.");
        o.needsFlux = "arcane_flux";
        o.Yields(Mythril, 0.4f, 0.6f).Yields(Silver, 0.01f, 0.05f).Yields(Silica, 0.05f, 0.15f);
        list.Add(o);
        o = Ore("orichalcum_ore", "Orichalcum ore", "fiery orange crystals that feel warm", new Color(0.92f, 0.45f, 0.18f), "glassy to metallic", "orange", new Color(0.95f, 0.55f, 0.25f),
            5f, 5.5f, 0.25f, 0.55f, CraftGangue.Quartz, 1000, 1f, 45f, "A sulfide like copper's: roast it first.");
        o.sulfide = true; o.roastTemperature = 650;
        o.Yields(Orichalcum, 0.5f, 0.65f).Yields(Copper, 0.05f, 0.15f).Yields(Sulfur, 0.02f, 0.05f).Yields(Silica, 0.01f, 0.03f);
        list.Add(o);
        return list;
    }

    // ------------------------------------------------------------------ fuels, quenchants, vats
    public static List<CraftFuel> Fuels()
    {
        return new List<CraftFuel>
        {
            new CraftFuel { id = "charcoal", displayName = "Charcoal", item = "Charcoal", energy = 3, maxTemperature = 1350, burnRate = 1, reducing = 0.65f, carburizing = 0.6f, smoke = 0.15f },
            new CraftFuel { id = "coal", displayName = "Coal", item = "Coal", energy = 4, maxTemperature = 1450, burnRate = 0.8f, reducing = 0.5f, carburizing = 0.3f, sulfur = 0.4f, smoke = 0.6f },
            new CraftFuel { id = "coke", displayName = "Coke", item = "Coke", energy = 4.5f, maxTemperature = 1550, burnRate = 0.7f, reducing = 0.55f, carburizing = 0.35f, sulfur = 0.05f, smoke = 0.1f },
            new CraftFuel { id = "firewood", displayName = "Firewood", item = "Firewood", energy = 2, maxTemperature = 950, burnRate = 1.8f, reducing = 0.3f, carburizing = 0.1f, smoke = 0.5f, burnsAnyWood = true },
        };
    }

    public static List<CraftQuenchant> Quenchants()
    {
        return new List<CraftQuenchant>
        {
            new CraftQuenchant { id = "water", displayName = "Water", severity = 1, vaporBlanket = 0.55f, lowTemperatureFactor = 1, boilingPoint = 100, observation = "It hisses and steam bursts up." },
            new CraftQuenchant { id = "brine", displayName = "Brine", item = "Rock Salt", severity = 1.6f, vaporBlanket = 0.15f, lowTemperatureFactor = 1, boilingPoint = 105, observation = "It hisses sharply." },
            new CraftQuenchant { id = "oil", displayName = "Quenching oil", item = "Quenching Oil", severity = 0.38f, vaporBlanket = 0.25f, lowTemperatureFactor = 0.45f, boilingPoint = 300, flashPoint = 210, heatCapacity = 0.5f, observation = "It sizzles and smokes." },
            new CraftQuenchant { id = "frostwater", displayName = "Frostwater", item = "Frostwater", severity = 1.25f, vaporBlanket = 0.05f, lowTemperatureFactor = 0.4f, boilingPoint = 100, affinity = ElementType.Ice, affinityStrength = 0.25f, observation = "A rime of frost creeps up the blade." },
            new CraftQuenchant { id = "dragon_blood", displayName = "Dragon's blood", item = "Dragon's Blood", severity = 0.75f, vaporBlanket = 0.2f, lowTemperatureFactor = 0.6f, boilingPoint = 300, flashPoint = 500, heatCapacity = 0.7f, affinity = ElementType.Fire, affinityStrength = 0.3f, observation = "The blood drinks the heat with a dark hiss; embers dance on it." },
        };
    }

    public static List<CraftTanningAgent> Agents()
    {
        return new List<CraftTanningAgent>
        {
            new CraftTanningAgent { id = "water", displayName = "Water", kind = CraftAgentKind.Water, dose = 0, hoursThrough = 1, observation = "clear water" },
            new CraftTanningAgent { id = "lime", displayName = "Lime water", kind = CraftAgentKind.Lime, item = "Quicklime", dose = 0.25f, actionHours = 24, observation = "milky, caustic lime water" },
            new CraftTanningAgent { id = "bark", displayName = "Oak bark liquor", kind = CraftAgentKind.Vegetable, item = "Oak Bark", dose = 0.15f, hoursThrough = 72, caseHardeningAbove = 0.55f, fixation = 1, stiffness = 0.55f, waterResistance = 0.45f, shrinkTemperature = 78, bestTemperature = 22, maxTemperature = 40, color = new Color(0.55f, 0.36f, 0.22f), durability = 1.2f, observation = "a brown, bitter-smelling liquor" },
            new CraftTanningAgent { id = "alum", displayName = "Alum and salt", kind = CraftAgentKind.Mineral, item = "Alum Powder", dose = 0.3f, hoursThrough = 18, caseHardeningAbove = 0.9f, fixation = 0.35f, stiffness = 0.25f, waterResistance = 0.1f, shrinkTemperature = 70, bestTemperature = 25, maxTemperature = 40, color = new Color(0.92f, 0.9f, 0.85f), durability = 0.85f, observation = "a clear, salty, puckering liquor" },
            new CraftTanningAgent { id = "brain_oil", displayName = "Oil emulsion", kind = CraftAgentKind.Oil, item = "Tanning Oil", dose = 0.25f, hoursThrough = 30, caseHardeningAbove = 0.95f, fixation = 0.85f, stiffness = 0.1f, waterResistance = 0.5f, shrinkTemperature = 65, bestTemperature = 30, maxTemperature = 45, color = new Color(0.85f, 0.78f, 0.6f), durability = 0.95f, observation = "a creamy, greasy emulsion" },
            new CraftTanningAgent { id = "tanning_essence", displayName = "Alchemical tanning essence", kind = CraftAgentKind.Alchemical, item = "Tanning Essence", dose = 0.4f, hoursThrough = 8, caseHardeningAbove = 0.8f, fixation = 1, stiffness = 0.45f, waterResistance = 0.7f, shrinkTemperature = 110, bestTemperature = 30, maxTemperature = 60, color = new Color(0.45f, 0.3f, 0.4f), durability = 1.4f, fireResistance = 0.4f, observation = "a violet liquor that smells of smoke" },
            new CraftTanningAgent { id = "vinegar", displayName = "Vinegar pickle", kind = CraftAgentKind.Acid, item = "Vinegar", dose = 0.4f, actionHours = 6, observation = "sharp-smelling vinegar" },
        };
    }

    // ------------------------------------------------------------------ hides and woods
    private static CraftHideDefinition Hide(string id, string name, int w, int h, float back, float belly, float fiber, float supple, float hair, float fat, float flaws, float price, string description) =>
        new CraftHideDefinition { id = id, displayName = name, width = w, height = h, thicknessBack = back, thicknessBelly = belly, fiberStrength = fiber, suppleness = supple, hair = hair, fat = fat, flaws = flaws, price = price, description = description };

    public static List<CraftHideDefinition> Hides()
    {
        var list = new List<CraftHideDefinition>
        {
            Hide("cow", "Cow hide", 18, 13, 5.5f, 3.2f, 1f, 0.35f, 0.5f, 0.5f, 0.04f, 8, "Big and thick along the back: armour, soles, belts."),
            Hide("deer", "Deer hide", 10, 8, 2.4f, 1.4f, 0.8f, 0.7f, 0.6f, 0.3f, 0.06f, 6, "Thin and supple: gloves, wraps, garments."),
            Hide("boar", "Boar hide", 12, 9, 5f, 3.5f, 1.15f, 0.25f, 0.85f, 0.75f, 0.08f, 5, "Tough and bristly, scarred."),
            Hide("wolf", "Wolf pelt", 11, 8, 3f, 2f, 0.9f, 0.5f, 0.95f, 0.35f, 0.05f, 9, "A thick fur."),
            Hide("bear", "Bear hide", 16, 12, 6f, 4f, 1.2f, 0.3f, 1f, 0.9f, 0.05f, 15, "Very thick, very fatty."),
            Hide("rabbit", "Rabbit pelt", 4, 3, 1f, 0.7f, 0.5f, 0.9f, 0.9f, 0.2f, 0.03f, 1, "Small and soft: linings."),
            Hide("drake", "Drake hide", 14, 10, 7f, 5f, 1.6f, 0.2f, 0f, 0.4f, 0.03f, 60, "Scaled and fire-proof by nature."),
            // Cloth, by the bolt (cut and sewn at the tailor's table).
            Hide("linen_cloth", "Linen cloth", 14, 20, 0.7f, 0.7f, 0.55f, 0.8f, 0f, 0f, 0.01f, 6, "Smooth, cool linen: shirts, linings, the layers of a gambeson."),
            Hide("wool_cloth", "Wool cloth", 14, 20, 1.6f, 1.6f, 0.45f, 0.85f, 0f, 0f, 0.015f, 8, "Warm, springy wool: cloaks, hoods, padding."),
            Hide("canvas", "Canvas", 12, 18, 1.2f, 1.2f, 0.8f, 0.55f, 0f, 0f, 0.01f, 7, "Heavy hemp canvas: the outer shell of a gambeson, bags, sails."),
        };
        foreach (CraftHideDefinition c in list)
            if (c.id == "linen_cloth" || c.id == "wool_cloth" || c.id == "canvas") c.textile = true;
        list[0].color = new Color(0.62f, 0.5f, 0.42f);
        list[6].color = new Color(0.35f, 0.42f, 0.3f);
        list[7].color = new Color(0.86f, 0.83f, 0.74f);
        list[8].color = new Color(0.5f, 0.42f, 0.36f);
        list[9].color = new Color(0.68f, 0.62f, 0.48f);
        list[6].affinity = ElementType.Fire;
        list[6].affinityStrength = 0.5f;
        return list;
    }

    private static CraftWoodDefinition Wood(string id, string name, float density, float hardness, float toughness, float flex, Color color, float price, string description,
        float l = 1000, float w = 40, float t = 40) =>
        new CraftWoodDefinition { id = id, displayName = name, density = density, hardness = hardness, toughness = toughness, flexibility = flex, color = color, price = price, description = description, blankLength = l, blankWidth = w, blankThickness = t };

    public static List<CraftWoodDefinition> Woods()
    {
        var list = new List<CraftWoodDefinition>
        {
            Wood("ash", "Ash", 0.68f, 0.5f, 0.85f, 0.55f, new Color(0.8f, 0.72f, 0.55f), 1, "Springy and tough: the wood for hafts and shafts.", 2200),
            Wood("hickory", "Hickory", 0.8f, 0.65f, 0.95f, 0.5f, new Color(0.75f, 0.6f, 0.45f), 1.5f, "The toughest haft wood."),
            Wood("oak", "Oak", 0.75f, 0.6f, 0.6f, 0.35f, new Color(0.65f, 0.5f, 0.32f), 1.2f, "Hard and heavy; it splits under shock."),
            Wood("maple", "Maple", 0.7f, 0.62f, 0.6f, 0.4f, new Color(0.85f, 0.78f, 0.62f), 1.2f, "Fine and hard: grips."),
            Wood("linden", "Linden plank", 0.5f, 0.3f, 0.45f, 0.5f, new Color(0.88f, 0.82f, 0.66f), 1, "Light boards for shields.", 650, 650, 16),
            Wood("ironwood", "Ironwood", 1.1f, 0.9f, 0.9f, 0.3f, new Color(0.3f, 0.22f, 0.18f), 8, "So dense it sinks; a legend among haft-makers."),
        };
        list[5].affinity = ElementType.Earth;
        list[5].affinityStrength = 0.3f;
        return list;
    }

    // ------------------------------------------------------------------ supplies and tools
    private static CraftSupplyDefinition Supply(string id, string name, CraftSupplyKind kind, string item, float strength, float price, string description, Color? color = null) =>
        new CraftSupplyDefinition { id = id, displayName = name, kind = kind, item = item, strength = strength, price = price, description = description, color = color ?? Color.white };

    public static List<CraftSupplyDefinition> Supplies()
    {
        return new List<CraftSupplyDefinition>
        {
            Supply("limestone", "Limestone", CraftSupplyKind.Flux, "Limestone", 1, 0.5f, "Furnace flux: its lime takes the silica's place in the slag, so the iron stays."),
            Supply("borax", "Borax", CraftSupplyKind.Flux, "Borax", 1, 2, "Flux for welding and for covering a melt."),
            Supply("sand", "Sand", CraftSupplyKind.Flux, "Sand", 0.6f, 0.1f, "A cheap welding flux."),
            Supply("arcane_flux", "Arcane flux", CraftSupplyKind.Flux, "Arcane Flux", 1, 25, "Without it, mythril ore gives nothing."),
            Supply("bone_ash", "Bone-ash cupel", CraftSupplyKind.Cupel, "Bone Ash Cupel", 1, 3, "Soaks up lead as it oxidises; silver stays on it."),
            Supply("linen_thread", "Linen thread", CraftSupplyKind.Thread, "Linen Thread", 1, 1, "Ordinary sewing thread."),
            Supply("waxed_thread", "Waxed thread", CraftSupplyKind.Thread, "Waxed Thread", 1.3f, 2, "Strong and it sheds water."),
            Supply("sinew", "Sinew", CraftSupplyKind.Thread, "Sinew", 1.2f, 1, "Shrinks tight as it dries."),
            Supply("silk_thread", "Silk thread", CraftSupplyKind.Thread, "Silk Thread", 1.5f, 6, "The strongest thread."),
            Supply("wool_padding", "Wool batting", CraftSupplyKind.Padding, "Wool Batting", 1, 3, "Padding under armour: it softens blows."),
            Supply("linen_padding", "Linen padding", CraftSupplyKind.Padding, "Linen Padding", 0.8f, 2, "Quilted linen padding."),
            Supply("beeswax", "Beeswax", CraftSupplyKind.Wax, "Beeswax", 1, 2, "Sheds water; stiffens leather a little."),
            Supply("neatsfoot_oil", "Neatsfoot oil", CraftSupplyKind.Oil, "Neatsfoot Oil", 1, 2, "Makes leather supple."),
            Supply("linseed_oil", "Linseed oil", CraftSupplyKind.Oil, "Linseed Oil", 0.8f, 1, "Protects metal and wood; softens leather a little."),
            Supply("dye_black", "Black dye", CraftSupplyKind.Dye, "Black Dye", 1, 2, "Iron and vinegar.", new Color(0.08f, 0.07f, 0.07f)),
            Supply("dye_brown", "Walnut dye", CraftSupplyKind.Dye, "Walnut Dye", 1, 1, "A warm brown.", new Color(0.35f, 0.22f, 0.12f)),
            Supply("dye_red", "Madder dye", CraftSupplyKind.Dye, "Madder Dye", 1, 3, "A deep red.", new Color(0.6f, 0.12f, 0.1f)),
            Supply("dye_blue", "Woad dye", CraftSupplyKind.Dye, "Woad Dye", 1, 3, "A dark blue.", new Color(0.12f, 0.2f, 0.45f)),
            Supply("refractory_clay", "Refractory clay", CraftSupplyKind.Clay, "Refractory Clay", 1, 1, "Coat the spine before the quench: it stays soft and tough, the edge hardens."),
            // The tailor's: fibre to spin, and what it spins into (coarse, even, fine).
            Spinnable(Supply("wool_fibre", "Raw wool", CraftSupplyKind.Fibre, "Raw Wool", 1, 0.5f, "Washed, carded wool, ready to spin."), "coarse_wool_yarn", "wool_yarn", "fine_wool_yarn"),
            Spinnable(Supply("flax_fibre", "Flax tow", CraftSupplyKind.Fibre, "Flax Tow", 1, 0.5f, "Hackled flax, ready to spin into linen."), "coarse_linen_yarn", "linen_thread", "fine_linen_thread"),
            Supply("coarse_wool_yarn", "Coarse wool yarn", CraftSupplyKind.Thread, "Coarse Wool Yarn", 0.6f, 0.4f, "Lumpy, hand-spun yarn: it holds, roughly."),
            Supply("wool_yarn", "Wool yarn", CraftSupplyKind.Thread, "Wool Yarn", 0.85f, 0.8f, "Even wool yarn."),
            Supply("fine_wool_yarn", "Fine worsted", CraftSupplyKind.Thread, "Fine Worsted", 1.05f, 1.5f, "Fine, smooth, strong wool thread."),
            Supply("coarse_linen_yarn", "Tow yarn", CraftSupplyKind.Thread, "Tow Yarn", 0.7f, 0.4f, "Coarse linen from the short fibres."),
            Supply("fine_linen_thread", "Fine linen thread", CraftSupplyKind.Thread, "Fine Linen Thread", 1.25f, 2, "Fine, even and very strong."),
            // The woodshop's finishes.
            Supply("spirit_varnish", "Spirit varnish", CraftSupplyKind.Varnish, "Spirit Varnish", 1, 2, "Dries fast to a bright, thin film."),
            Supply("oil_varnish", "Oil varnish", CraftSupplyKind.Varnish, "Oil Varnish", 1.25f, 3, "Slow to dry, tough and weatherproof."),
        };
    }

    private static CraftSupplyDefinition Spinnable(CraftSupplyDefinition s, params string[] into)
    {
        s.spinsInto = new List<string>(into);
        return s;
    }

    private static CraftAbrasive Tool(string id, string name, CraftAbrasiveKind kind, float cut, float scale, float finish, float heat = 0, float maxHardness = 2000, float apex = 10, bool wood = false, bool leather = false) =>
        new CraftAbrasive { id = id, displayName = name, kind = kind, cut = cut, scale = scale, finish = finish, heat = heat, maxHardness = maxHardness, apex = apex, wood = wood, leather = leather };

    public static List<CraftAbrasive> Abrasives()
    {
        return new List<CraftAbrasive>
        {
            Tool("wire_brush", "Wire brush", CraftAbrasiveKind.Brush, 0.002f, 0.04f, 30),
            Tool("scraper", "Scraper", CraftAbrasiveKind.Brush, 0.01f, 0.06f, 20),
            Tool("coarse_wheel", "Coarse wheel", CraftAbrasiveKind.Wheel, 0.08f, 0.2f, 25, 1.2f),
            Tool("fine_wheel", "Fine wheel", CraftAbrasiveKind.Wheel, 0.03f, 0.1f, 6, 0.8f),
            Tool("bastard_file", "Bastard file", CraftAbrasiveKind.File, 0.02f, 0.05f, 12, 0, 600),
            Tool("smooth_file", "Smooth file", CraftAbrasiveKind.File, 0.008f, 0.02f, 4, 0, 620),
            Tool("needle_file", "Needle file", CraftAbrasiveKind.File, 0.003f, 0.01f, 2, 0, 620),
            Tool("rasp", "Rasp", CraftAbrasiveKind.File, 0.04f, 0, 40, 0, 150, 10, true),
            Tool("carving_knife", "Carving knife", CraftAbrasiveKind.File, 0.03f, 0, 15, 0, 60, 10, true, true),
            Tool("coarse_emery", "Coarse emery", CraftAbrasiveKind.Polish, 0.002f, 0.02f, 3),
            Tool("fine_emery", "Fine emery", CraftAbrasiveKind.Polish, 0.001f, 0.01f, 1),
            Tool("rouge", "Polishing rouge", CraftAbrasiveKind.Polish, 0.0003f, 0.005f, 0.2f),
            Tool("coarse_stone", "Coarse whetstone", CraftAbrasiveKind.Whetstone, 0, 0, 8, 0, 2000, 18),
            Tool("medium_stone", "Medium whetstone", CraftAbrasiveKind.Whetstone, 0, 0, 3, 0, 2000, 7),
            Tool("fine_stone", "Fine whetstone", CraftAbrasiveKind.Whetstone, 0, 0, 1, 0, 2000, 2.5f),
            Tool("strop", "Leather strop", CraftAbrasiveKind.Whetstone, 0, 0, 0.5f, 0, 2000, 1),
        };
    }

    private static CraftMold Mold(string id, string name, float l, float w, float t) => new CraftMold { id = id, displayName = name, length = l, width = w, thickness = t };

    private static CraftMold Profiled(string id, string name, float l, params (float t, float w, float th)[] points)
    {
        var m = new CraftMold { id = id, displayName = name, length = l };
        foreach (var p in points) m.profile.Add(new CraftProfilePoint(p.t, p.w, p.th));
        if (points.Length > 0) { m.width = points[0].w; m.thickness = points[0].th; }
        return m;
    }

    public static List<CraftMold> Molds()
    {
        var list = new List<CraftMold>
        {
            Mold("ingot", "Ingot", 120, 40, 20),
            Mold("bar", "Bar", 250, 30, 12),
            Mold("plate", "Plate", 250, 200, 3),
            Profiled("blade_blank", "Blade blank", 600, (0, 14, 7), (0.15f, 45, 7), (0.95f, 35, 6), (1, 10, 5)),
            Profiled("dagger_blank", "Dagger blank", 300, (0, 12, 6), (0.3f, 32, 6), (1, 8, 4)),
            Profiled("axe_head", "Axe head", 170, (0, 40, 30), (0.25f, 40, 30), (0.5f, 30, 12), (1, 100, 6)),
            Mold("mace_head", "Mace head", 90, 60, 60),
            Profiled("spear_head", "Spear head", 300, (0, 26, 26), (0.3f, 26, 18), (0.4f, 40, 8), (1, 6, 3)),
            Mold("guard", "Guard", 200, 22, 12),
            Mold("pommel", "Pommel", 55, 40, 35),
            Mold("boss", "Shield boss", 160, 160, 4),
            Profiled("pick_head", "Pick head", 450, (0, 10, 10), (0.45f, 35, 30), (0.55f, 35, 30), (1, 10, 10)),
            new CraftMold { id = "rivets", displayName = "Rivets", length = 14, width = 5, thickness = 5, fasteners = true, fastenerKind = CraftFastenerKind.Rivet, count = 12, fastenerMass = 5 },
            new CraftMold { id = "studs", displayName = "Studs", length = 10, width = 8, thickness = 4, fasteners = true, fastenerKind = CraftFastenerKind.Stud, count = 24, fastenerMass = 4 },
            new CraftMold { id = "buckles", displayName = "Buckles", length = 50, width = 6, thickness = 6, fasteners = true, fastenerKind = CraftFastenerKind.Buckle, count = 2, fastenerMass = 30 },
        };
        foreach (CraftMold m in list)
            switch (m.id)
            {
                case "axe_head": m.eyeAt = 0.12f; m.eyeDiameter = 28; break;
                case "mace_head": m.eyeAt = 0.5f; m.eyeDiameter = 28; break;
                case "spear_head": m.eyeAt = 0.1f; m.eyeDiameter = 26; break;
                case "guard": m.eyeAt = 0.5f; m.eyeDiameter = 13; break;
                case "pommel": m.eyeAt = 0.5f; m.eyeDiameter = 11; break;
                case "pick_head": m.eyeAt = 0.5f; m.eyeDiameter = 30; break;
            }
        return list;
    }

    private static CraftPattern Pattern(string id, string name, int seamHoles, params string[] rows) =>
        new CraftPattern { id = id, displayName = name, seamHoles = seamHoles, rows = new List<string>(rows) };

    public static List<CraftPattern> Patterns()
    {
        return new List<CraftPattern>
        {
            Pattern("jerkin_body", "Jerkin body", 40, ".XXXXXX.", "XXXXXXXX", "XXXXXXXX", "XXXXXXXX", "XXXXXXXX", ".XXXXXX."),
            Pattern("strap", "Strap", 8, "XXXX"),
            Pattern("long_strap", "Belt strap", 10, "XXXXXXXXXX"),
            Pattern("cap", "Cap crown", 20, ".X.", "XXX", "XXX"),
            Pattern("bracer", "Bracer", 12, "XXX", "XXX"),
            Pattern("glove", "Glove", 30, "XX", "XX", "XX"),
            Pattern("boot_upper", "Boot upper", 24, "XXX", "XXX", "XXX", "XX."),
            Pattern("sole", "Sole", 24, "X", "X", "X"),
            Pattern("liner", "Helm liner", 12, "XXX", "XXX", "XXX"),
            Pattern("grip_wrap", "Grip wrap", 0, "XXX"),
            // Cloth (the bolt's length - its grain - runs down the rows).
            Pattern("gambeson_panel", "Gambeson panel", 36, ".XXXX.", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", "XXXXXX", ".XXXX."),
            Pattern("cloak_half", "Cloak half", 30, "..XXX", ".XXXX", "XXXXX", "XXXXX", "XXXXXX", "XXXXXX", "XXXXXXX", "XXXXXXX"),
            Pattern("hood", "Hood side", 16, ".XX.", "XXXX", "XXXX", "XXXX", "XXX."),
            Pattern("trouser_leg", "Trouser leg", 26, "XXXX", "XXXX", "XXXX", "XXX.", "XXX.", "XXX.", "XXX.", "XXX.", "XX..", "XX.."),
        };
    }

    // ------------------------------------------------------------------ designs
    private static CraftShapeTarget Shape(float lmin, float lmax, float mmin, float mmax, params (float t, float w, float th)[] points)
    {
        var s = new CraftShapeTarget { lengthMin = lmin, lengthMax = lmax, massMin = mmin, massMax = mmax };
        foreach (var p in points) s.Point(p.t, p.w, p.th);
        return s;
    }

    private static CraftComponentSpec Metal(string role, string name, CraftComponentFunction f, CraftShapeTarget shape, float importance = 1f, int count = 1) =>
        new CraftComponentSpec { role = role, displayName = name, kind = CraftPieceKind.Metal, function = f, shape = shape, importance = importance, count = count };

    private static CraftComponentSpec WoodPart(string role, string name, float lmin, float lmax, float dmin, float dmax, float tough, float importance = 0.6f)
    {
        var c = new CraftComponentSpec
        {
            role = role, displayName = name, kind = CraftPieceKind.Wood, function = CraftComponentFunction.Grip, importance = importance,
            wood = new CraftWoodSpec { lengthMin = lmin, lengthMax = lmax, diameterMin = dmin, diameterMax = dmax, toughnessMin = tough },
        };
        float d = (dmin + dmax) * 0.5f;
        c.shape = Shape(lmin, lmax, 0, 99999, (0, d, d), (1, d, d));
        return c;
    }

    private static CraftComponentSpec Leather(string role, string name, CraftComponentFunction f, string pattern, float tmin, float tmax, float smin = 0, float smax = 1, int count = 1, float importance = 1f, bool optional = false) =>
        new CraftComponentSpec
        {
            role = role, displayName = name, kind = CraftPieceKind.Hide, function = f, count = count, importance = importance, optional = optional,
            leather = new CraftLeatherSpec { pattern = pattern, thicknessMin = tmin, thicknessMax = tmax, suppleMin = smin, suppleMax = smax },
        };

    private static CraftComponentSpec Fasteners(string role, string name, CraftFastenerKind kind, int count, bool optional = false) =>
        new CraftComponentSpec { role = role, displayName = name, kind = CraftPieceKind.Fasteners, function = CraftComponentFunction.Fastener, fastener = kind, fastenerCount = count, importance = 0.5f, optional = optional };

    private static CraftComponentSpec SupplyPart(string role, string name, CraftSupplyKind kind, int count, CraftComponentFunction f) =>
        new CraftComponentSpec { role = role, displayName = name, kind = CraftPieceKind.Metal, supply = true, supplyKind = kind, supplyCount = count, function = f, importance = 0.3f };

    private static CraftJointSpec Joint(string id, string name, CraftJointKind kind, string a, string b, int count, float importance = 1f) =>
        new CraftJointSpec { id = id, displayName = name, kind = kind, roleA = a, roleB = b, count = count, importance = importance };

    private static CraftDesign Hilted(string id, string name, WeaponCategory cat, DamageForm form, WeaponScaling scaling, CraftShapeTarget blade, CraftShapeTarget guard,
        float gripMin, float gripMax, float gripD0, float gripD1, CraftShapeTarget pommel, string description)
    {
        blade.edge = CraftEdgeKind.Double;
        blade.hardened = true;
        guard.eye = true;
        guard.eyeAt = 0.5f;
        pommel.eye = true;
        pommel.eyeAt = 0.5f;
        var d = new CraftDesign
        {
            id = id, displayName = name, kind = CraftDesignKind.Weapon, weaponCategory = cat, damageForm = form, scaling = scaling, description = description,
            components =
            {
                Metal("blade", "Blade", CraftComponentFunction.Edge, blade, 3),
                Metal("guard", "Guard", CraftComponentFunction.Guard, guard, 0.6f),
                WoodPart("grip", "Grip", gripMin, gripMax, gripD0, gripD1, 0.3f),
                Metal("pommel", "Pommel", CraftComponentFunction.Counterweight, pommel, 0.6f),
                Leather("wrap", "Grip wrap", CraftComponentFunction.Wrap, "grip_wrap", 1f, 2.5f, 0.4f, 1, 1, 0.3f, true),
            },
            joints =
            {
                Joint("guard_fit", "Guard on the tang", CraftJointKind.Fit, "blade", "guard", 1, 0.8f),
                Joint("grip_fit", "Grip on the tang", CraftJointKind.Fit, "blade", "grip", 1, 0.8f),
                Joint("pommel_peen", "Pommel peened on", CraftJointKind.Peen, "blade", "pommel", 1, 1.2f),
                Joint("wrap", "Grip wrap", CraftJointKind.Wrap, "grip", "wrap", 10, 0.3f),
            },
        };
        return d;
    }

    private static CraftDesign Hafted(string id, string name, CraftDesignKind kind, WeaponCategory cat, DamageForm form, CraftComponentSpec head, CraftComponentSpec haft,
        CraftJointKind joint, string description)
    {
        head.importance = 3;
        haft.role = "haft";
        var d = new CraftDesign
        {
            id = id, displayName = name, kind = kind, weaponCategory = cat, damageForm = form, scaling = WeaponScaling.Strength, description = description,
            components = { head, haft },
            joints = { Joint("head_haft", "Head on the haft", joint, "haft", "head", 1, 1.4f) },
        };
        return d;
    }

    public static List<CraftDesign> Designs()
    {
        var list = new List<CraftDesign>();
        CraftDesign d;

        // Hilted blades.
        CraftShapeTarget blade = Shape(300, 380, 150, 350, (0, 12, 6), (0.3f, 12, 6), (0.31f, 32, 5.5f), (0.7f, 26, 4.5f), (1, 4, 3));
        blade.edgeFrom = 0.33f; blade.edgeAngle = 28; blade.tangTo = 0.3f;
        d = Hilted("dagger", "Dagger", WeaponCategory.Dagger, DamageForm.Pierce, WeaponScaling.Agility, blade,
            Shape(70, 110, 30, 90, (0, 14, 8), (1, 14, 8)), 85, 110, 22, 30, Shape(25, 45, 40, 120, (0, 30, 25), (1, 30, 25)),
            "A short double-edged blade: quick stabs.");
        d.damageMin = 7; d.damageMax = 10; d.critChance = 0.12f; d.critMultiplier = 1.8f; d.knockback = 0.2f; d.referenceMass = 260; d.idealBalance = 30;
        d.durability = 220; d.price = 40; d.gridSize = new Vector2Int(1, 2);
        d.joints[3].count = 8;
        list.Add(d);

        blade = Shape(820, 940, 900, 1350, (0, 14, 6.5f), (0.12f, 14, 6.5f), (0.13f, 46, 6), (0.5f, 42, 5), (0.9f, 32, 4), (1, 8, 3));
        blade.edgeFrom = 0.14f; blade.edgeAngle = 30; blade.tangTo = 0.13f;
        d = Hilted("arming_sword", "Arming sword", WeaponCategory.Sword, DamageForm.Slash, WeaponScaling.Strength, blade,
            Shape(180, 240, 150, 320, (0, 20, 12), (1, 20, 12)), 95, 120, 26, 34, Shape(40, 60, 180, 340, (0, 45, 38), (1, 45, 38)),
            "A one-handed double-edged sword.");
        d.damageMin = 10; d.damageMax = 14; d.critChance = 0.07f; d.critMultiplier = 1.6f; d.knockback = 0.5f; d.referenceMass = 1150; d.idealBalance = 120;
        d.durability = 300; d.price = 120; d.gridSize = new Vector2Int(1, 4);
        list.Add(d);

        blade = Shape(1250, 1450, 1800, 2600, (0, 16, 7), (0.2f, 16, 7), (0.21f, 54, 7), (0.6f, 46, 6), (0.95f, 34, 4.5f), (1, 10, 3));
        blade.edgeFrom = 0.22f; blade.edgeAngle = 32; blade.tangTo = 0.2f;
        d = Hilted("greatsword", "Greatsword", WeaponCategory.Greatsword, DamageForm.Slash, WeaponScaling.Strength, blade,
            Shape(260, 320, 250, 450, (0, 24, 14), (1, 24, 14)), 220, 280, 28, 36, Shape(50, 70, 300, 520, (0, 52, 44), (1, 52, 44)),
            "A two-handed sword.");
        d.damageMin = 18; d.damageMax = 26; d.critChance = 0.05f; d.critMultiplier = 1.8f; d.knockback = 1.2f; d.referenceMass = 2200; d.idealBalance = 160;
        d.durability = 350; d.price = 220; d.gridSize = new Vector2Int(1, 5);
        d.joints[3].count = 16;
        list.Add(d);

        // Hafted weapons and tools.
        CraftShapeTarget head = Shape(160, 210, 900, 1500, (0, 38, 30), (0.28f, 38, 30), (0.45f, 26, 12), (0.7f, 60, 7), (1, 110, 5));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.75f; head.edgeAngle = 34; head.eye = true; head.eyeAt = 0.14f; head.eyeDiameter = 30; head.hardened = true;
        d = Hafted("battle_axe", "Battle axe", CraftDesignKind.Weapon, WeaponCategory.Axe, DamageForm.Slash, Metal("head", "Axe head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Haft", 650, 850, 30, 40, 0.6f), CraftJointKind.Wedge, "A bearded war axe on a long haft.");
        d.damageMin = 13; d.damageMax = 19; d.critChance = 0.05f; d.critMultiplier = 1.7f; d.knockback = 0.9f; d.referenceMass = 1300; d.idealBalance = 450;
        d.durability = 280; d.price = 100; d.gridSize = new Vector2Int(2, 4);
        list.Add(d);

        head = Shape(220, 320, 250, 500, (0, 26, 26), (0.3f, 26, 20), (0.35f, 40, 8), (0.7f, 30, 6), (1, 4, 3));
        head.edge = CraftEdgeKind.Double; head.edgeFrom = 0.35f; head.edgeAngle = 28; head.eye = true; head.eyeAt = 0.12f; head.eyeDiameter = 26; head.hardened = true;
        d = Hafted("spear", "Spear", CraftDesignKind.Weapon, WeaponCategory.Spear, DamageForm.Pierce, Metal("head", "Spear head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Shaft", 1700, 2100, 26, 34, 0.6f), CraftJointKind.Socket, "A socketed spear head on an ash shaft.");
        d.components.Add(Fasteners("rivets", "Socket pin", CraftFastenerKind.Rivet, 1, true));
        d.damageMin = 11; d.damageMax = 15; d.critChance = 0.1f; d.critMultiplier = 1.7f; d.knockback = 0.4f; d.referenceMass = 350; d.idealBalance = 900;
        d.durability = 260; d.price = 80; d.gridSize = new Vector2Int(1, 6);
        list.Add(d);

        head = Shape(80, 120, 700, 1200, (0, 60, 60), (1, 60, 60));
        head.eye = true; head.eyeAt = 0.5f; head.eyeDiameter = 28;
        d = Hafted("mace", "Mace", CraftDesignKind.Weapon, WeaponCategory.Mace, DamageForm.Blunt, Metal("head", "Mace head", CraftComponentFunction.Striking, head),
            WoodPart("haft", "Haft", 500, 700, 30, 38, 0.6f), CraftJointKind.Wedge, "A heavy iron head on a haft.");
        d.damageMin = 12; d.damageMax = 17; d.critChance = 0.04f; d.critMultiplier = 1.5f; d.knockback = 1.4f; d.referenceMass = 950; d.idealBalance = 400;
        d.durability = 350; d.price = 90; d.gridSize = new Vector2Int(1, 3);
        list.Add(d);

        head = Shape(400, 500, 1000, 1700, (0, 10, 10), (0.42f, 34, 28), (0.58f, 34, 28), (1, 10, 10));
        head.eye = true; head.eyeAt = 0.5f; head.eyeDiameter = 30; head.hardened = true;
        d = Hafted("pickaxe", "Pickaxe", CraftDesignKind.Tool, WeaponCategory.Tool, DamageForm.Pierce, Metal("head", "Pick head", CraftComponentFunction.Striking, head),
            WoodPart("haft", "Haft", 750, 900, 32, 40, 0.6f), CraftJointKind.Wedge, "For ore and stone.");
        d.toolType = ToolType.PickAxe;
        d.damageMin = 8; d.damageMax = 11; d.toolDamage = 15; d.critChance = 0.03f; d.knockback = 0.6f; d.referenceMass = 1300; d.idealBalance = 650;
        d.durability = 400; d.price = 60; d.gridSize = new Vector2Int(2, 4);
        list.Add(d);

        head = Shape(130, 170, 600, 1000, (0, 32, 24), (0.3f, 32, 24), (0.5f, 24, 10), (1, 80, 5));
        head.edge = CraftEdgeKind.Single; head.edgeFrom = 0.75f; head.edgeAngle = 30; head.eye = true; head.eyeAt = 0.15f; head.eyeDiameter = 26; head.hardened = true;
        d = Hafted("hatchet", "Hatchet", CraftDesignKind.Tool, WeaponCategory.Tool, DamageForm.Slash, Metal("head", "Hatchet head", CraftComponentFunction.Edge, head),
            WoodPart("haft", "Haft", 380, 480, 28, 36, 0.6f), CraftJointKind.Wedge, "For felling trees.");
        d.toolType = ToolType.Axe;
        d.damageMin = 7; d.damageMax = 10; d.toolDamage = 14; d.referenceMass = 800; d.idealBalance = 300; d.durability = 320; d.price = 45; d.gridSize = new Vector2Int(2, 3);
        list.Add(d);

        // Leather and mixed armour.
        d = new CraftDesign
        {
            id = "leather_cap", displayName = "Leather cap", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Helmet, description = "A sewn leather cap.",
            components = { Leather("crown", "Crown", CraftComponentFunction.Protection, "cap", 2.5f, 5.5f, 0, 0.6f, 1, 2), SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread) },
            joints = { Joint("seam", "Crown seam", CraftJointKind.Stitch, "crown", "thread", 20) },
            defense = 4, durability = 150, price = 15, gridSize = new Vector2Int(2, 2),
        };
        list.Add(d);

        d = new CraftDesign
        {
            id = "leather_jerkin", displayName = "Leather jerkin", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Chestplate, description = "A sleeveless leather jerkin with buckled straps.",
            components =
            {
                Leather("body", "Body", CraftComponentFunction.Protection, "jerkin_body", 2.5f, 4.5f, 0.25f, 0.9f, 1, 2),
                Leather("straps", "Shoulder strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 2, 0.5f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 2),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
            },
            joints = { Joint("straps_sewn", "Straps sewn on", CraftJointKind.Stitch, "body", "straps", 24), Joint("buckles", "Buckles", CraftJointKind.Buckle, "straps", "buckles", 2) },
            defense = 9, durability = 220, price = 45, gridSize = new Vector2Int(2, 3),
        };
        list.Add(d);

        d = new CraftDesign
        {
            id = "studded_armor", displayName = "Studded leather armour", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Chestplate, description = "Thick leather set with metal studs over padding.",
            components =
            {
                Leather("body", "Body", CraftComponentFunction.Protection, "jerkin_body", 3, 5.5f, 0.1f, 0.8f, 1, 2),
                Fasteners("studs", "Studs", CraftFastenerKind.Stud, 24),
                SupplyPart("padding", "Padding", CraftSupplyKind.Padding, 2, CraftComponentFunction.Padding),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
            },
            joints = { Joint("studs", "Studs set", CraftJointKind.Rivet, "body", "studs", 24), Joint("padding_sewn", "Padding sewn in", CraftJointKind.Stitch, "body", "padding", 20, 0.5f) },
            defense = 14, durability = 280, price = 90, gridSize = new Vector2Int(2, 3),
        };
        list.Add(d);

        CraftShapeTarget plate = Shape(170, 220, 120, 280, (0, 65, 1.8f), (1, 65, 1.8f));
        plate.dish = 6;
        d = new CraftDesign
        {
            id = "plated_bracers", displayName = "Plated bracers", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Bracers, description = "Leather bracers riveted with steel plates.",
            components =
            {
                Leather("leather", "Bracer leather", CraftComponentFunction.Backing, "bracer", 2.5f, 5, 0, 1, 2),
                Metal("plates", "Plate", CraftComponentFunction.Protection, plate, 1.5f, 2),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 8),
            },
            joints = { Joint("plates_riveted", "Plates riveted on", CraftJointKind.Rivet, "leather", "plates", 8, 1.2f) },
            defense = 6, durability = 250, price = 50, gridSize = new Vector2Int(2, 2),
        };
        list.Add(d);

        plate = Shape(70, 100, 40, 110, (0, 60, 1.5f), (1, 60, 1.5f));
        plate.dish = 4;
        d = new CraftDesign
        {
            id = "gauntlets", displayName = "Plated gloves", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Gloves, description = "Supple gloves with plates over the back of the hand.",
            components =
            {
                Leather("gloves", "Glove", CraftComponentFunction.Backing, "glove", 1, 2.2f, 0.5f, 1, 2),
                Metal("plates", "Hand plate", CraftComponentFunction.Protection, plate, 1, 2),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 6),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
            },
            joints = { Joint("seams", "Seams", CraftJointKind.Stitch, "gloves", "thread", 30), Joint("plates", "Plates riveted on", CraftJointKind.Rivet, "gloves", "plates", 6) },
            defense = 5, durability = 200, price = 45, gridSize = new Vector2Int(2, 2),
        };
        list.Add(d);

        d = new CraftDesign
        {
            id = "leather_boots", displayName = "Leather boots", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Boots, description = "Boots with thick sewn soles.",
            components =
            {
                Leather("uppers", "Upper", CraftComponentFunction.Protection, "boot_upper", 2, 4, 0.3f, 0.9f, 2),
                Leather("soles", "Sole", CraftComponentFunction.Backing, "sole", 5, 9, 0, 0.4f, 2),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
            },
            joints = { Joint("soles_sewn", "Soles sewn on", CraftJointKind.Stitch, "uppers", "soles", 30) },
            defense = 5, durability = 260, price = 35, gridSize = new Vector2Int(2, 2),
        };
        list.Add(d);

        plate = Shape(220, 280, 900, 1500, (0, 190, 2), (1, 190, 2));
        plate.dish = 60;
        d = new CraftDesign
        {
            id = "plate_helm", displayName = "Plate helm", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Helmet, description = "A dished steel skull with a riveted leather liner and padding.",
            components =
            {
                Metal("shell", "Helm shell", CraftComponentFunction.Protection, plate, 2),
                Leather("liner", "Liner", CraftComponentFunction.Backing, "liner", 1.5f, 3.5f, 0.3f),
                SupplyPart("padding", "Padding", CraftSupplyKind.Padding, 1, CraftComponentFunction.Padding),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 6),
            },
            joints = { Joint("liner_riveted", "Liner riveted in", CraftJointKind.Rivet, "shell", "liner", 6) },
            defense = 12, durability = 400, price = 110, gridSize = new Vector2Int(2, 2),
        };
        list.Add(d);

        plate = Shape(380, 450, 3500, 6000, (0, 340, 2.5f), (0.5f, 360, 2.5f), (1, 320, 2.2f));
        plate.dish = 50;
        d = new CraftDesign
        {
            id = "breastplate", displayName = "Breastplate", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Chestplate, description = "A dished steel plate on buckled leather straps, worn over padding.",
            components =
            {
                Metal("plate", "Breastplate", CraftComponentFunction.Protection, plate, 3),
                Leather("straps", "Strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 2, 0.5f),
                Fasteners("buckles", "Buckles", CraftFastenerKind.Buckle, 2),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 8),
                SupplyPart("padding", "Padding", CraftSupplyKind.Padding, 2, CraftComponentFunction.Padding),
            },
            joints = { Joint("straps_riveted", "Straps riveted on", CraftJointKind.Rivet, "plate", "straps", 8, 1.2f), Joint("buckles", "Buckles", CraftJointKind.Buckle, "straps", "buckles", 2) },
            defense = 24, durability = 500, price = 260, gridSize = new Vector2Int(3, 3),
        };
        list.Add(d);

        d = new CraftDesign
        {
            id = "belt", displayName = "Belt", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Belt, description = "A buckled leather belt.",
            components = { Leather("strap", "Strap", CraftComponentFunction.Strap, "long_strap", 3, 5, 0, 1, 1, 2), Fasteners("buckle", "Buckle", CraftFastenerKind.Buckle, 1) },
            joints = { Joint("buckle", "Buckle", CraftJointKind.Buckle, "strap", "buckle", 1) },
            defense = 1, durability = 300, price = 12, gridSize = new Vector2Int(3, 1),
        };
        list.Add(d);

        CraftComponentSpec board = WoodPart("board", "Board", 550, 700, 10, 18, 0.3f, 2);
        board.function = CraftComponentFunction.Protection;
        board.shape = Shape(550, 700, 0, 99999, (0, 600, 14), (1, 600, 14));
        CraftShapeTarget boss = Shape(140, 180, 400, 800, (0, 150, 2.5f), (1, 150, 2.5f));
        boss.dish = 45;
        d = new CraftDesign
        {
            id = "round_shield", displayName = "Round shield", kind = CraftDesignKind.Shield, armorSlot = ArmorSlotType.Shield, description = "Linden boards with a dished iron boss and a leather grip.",
            components =
            {
                board,
                Metal("boss", "Boss", CraftComponentFunction.Protection, boss, 1),
                Leather("grip_strap", "Grip strap", CraftComponentFunction.Strap, "strap", 3, 5, 0, 1, 1, 0.5f),
                Fasteners("rivets", "Rivets", CraftFastenerKind.Rivet, 8),
            },
            joints = { Joint("boss_riveted", "Boss riveted on", CraftJointKind.Rivet, "board", "boss", 4), Joint("strap_riveted", "Grip riveted on", CraftJointKind.Rivet, "board", "grip_strap", 4) },
            defense = 10, durability = 350, price = 70, gridSize = new Vector2Int(3, 3),
        };
        list.Add(d);

        // The tailor's: sewn cloth.
        d = new CraftDesign
        {
            id = "padded_gambeson", displayName = "Padded gambeson", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Chestplate,
            description = "Layers of cloth quilted over wool batting: worn alone, or under mail and plate.",
            components =
            {
                Cloth("shell", "Outer shell", CraftComponentFunction.Protection, "gambeson_panel", 0.8f, 2, 2, 1.5f),
                Cloth("lining", "Lining", CraftComponentFunction.Backing, "gambeson_panel", 0.5f, 1.2f, 2, 0.8f),
                SupplyPart("padding", "Batting", CraftSupplyKind.Padding, 3, CraftComponentFunction.Padding),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 2, CraftComponentFunction.Thread),
            },
            joints = { Joint("quilting", "Quilting through the batting", CraftJointKind.Stitch, "shell", "padding", 40, 1.2f), Joint("seams", "Side and shoulder seams", CraftJointKind.Stitch, "shell", "lining", 30) },
            defense = 11, durability = 260, price = 55, gridSize = new Vector2Int(2, 3),
        };
        list.Add(d);

        d = new CraftDesign
        {
            id = "wool_cloak", displayName = "Wool cloak", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Cloak,
            description = "Two halves of heavy wool sewn down the back, closed with a clasp.",
            components =
            {
                Cloth("halves", "Cloak half", CraftComponentFunction.Protection, "cloak_half", 1.2f, 2.5f, 2, 1f),
                Fasteners("clasp", "Clasp", CraftFastenerKind.Buckle, 1),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
            },
            joints = { Joint("back_seam", "Back seam", CraftJointKind.Stitch, "halves", "thread", 30), Joint("clasp", "Clasp sewn on", CraftJointKind.Buckle, "halves", "clasp", 1) },
            defense = 2, durability = 200, price = 25, gridSize = new Vector2Int(2, 3),
        };
        list.Add(d);

        d = new CraftDesign
        {
            id = "cloth_hood", displayName = "Hood", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Helmet,
            description = "A simple hood of two sides sewn together.",
            components =
            {
                Cloth("sides", "Hood side", CraftComponentFunction.Protection, "hood", 0.6f, 2.5f, 2, 1f),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
            },
            joints = { Joint("crown_seam", "Crown seam", CraftJointKind.Stitch, "sides", "thread", 18) },
            defense = 1, durability = 120, price = 8, gridSize = new Vector2Int(2, 2),
        };
        list.Add(d);

        d = new CraftDesign
        {
            id = "quilted_trousers", displayName = "Quilted trousers", kind = CraftDesignKind.Armor, armorSlot = ArmorSlotType.Leggings,
            description = "Canvas legs quilted over a layer of batting.",
            components =
            {
                Cloth("legs", "Trouser leg", CraftComponentFunction.Protection, "trouser_leg", 0.8f, 2, 2, 1.2f),
                SupplyPart("padding", "Batting", CraftSupplyKind.Padding, 1, CraftComponentFunction.Padding),
                SupplyPart("thread", "Thread", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread),
            },
            joints = { Joint("inseam", "Inseams and quilting", CraftJointKind.Stitch, "legs", "padding", 36) },
            defense = 5, durability = 200, price = 30, gridSize = new Vector2Int(2, 3),
        };
        list.Add(d);
        AddArmoury(list);
        return list;
    }

    private static CraftComponentSpec Cloth(string role, string name, CraftComponentFunction f, string pattern, float tmin, float tmax, int count, float importance)
    {
        CraftComponentSpec c = Leather(role, name, f, pattern, tmin, tmax, 0, 1, count, importance);
        c.leather.fabric = CraftFabric.Cloth;
        return c;
    }

    // ------------------------------------------------------------------ starting pieces (test stations, scripts)
    /// <summary>A bar of metal of a composition (fractions by element id), at room temperature: test stations, scripts and tests.</summary>
    public static CraftPiece Bar(CraftRules rules, double length, double width, double thickness, params (string element, double fraction)[] composition)
    {
        var p = new CraftPiece { kind = CraftPieceKind.Metal, origin = "a bar from the stores" };
        foreach (var c in composition)
            p.composition.Add(new CraftAmount(c.element, c.fraction));
        p.CompositionChanged();
        double grams = length * width * thickness * p.Profile(rules).Density / 1000;
        foreach (CraftAmount a in p.composition)
            a.grams *= grams;
        p.CompositionChanged();
        int n = rules.Settings.segments;
        double ambient = rules.Settings.defaultAmbient;
        for (int i = 0; i < n; i++)
        {
            var s = new MetalSegment { length = length / n, width = width, thickness = thickness, rough = 30 };
            s.edge.temp = s.edge.prevTemp = s.body.temp = s.body.prevTemp = ambient;
            p.segments.Add(s);
        }
        return p;
    }

    /// <summary>A wooden blank of a species (its item's size unless given).</summary>
    public static CraftPiece WoodBlank(CraftRules rules, string wood, double length = 0, double width = 0, double thickness = 0)
    {
        CraftWoodDefinition w = rules.Wood(wood);
        if (w == null)
            return null;
        var p = new CraftPiece { kind = CraftPieceKind.Wood, origin = $"wood:{w.id}" };
        p.wood.wood = w.id;
        length = length > 0 ? length : w.blankLength;
        width = width > 0 ? width : w.blankWidth;
        thickness = thickness > 0 ? thickness : w.blankThickness;
        int n = Math.Max(4, rules.Settings.segments / 2);
        for (int i = 0; i < n; i++)
        {
            var s = new MetalSegment { length = length / n, width = width, thickness = thickness, rough = 60 };
            s.edge.temp = s.edge.prevTemp = s.body.temp = s.body.prevTemp = rules.Settings.defaultAmbient;
            p.segments.Add(s);
        }
        return p;
    }

    /// <summary>A set of fasteners of a metal (count, grams each).</summary>
    public static CraftPiece FastenerSet(CraftRules rules, CraftFastenerKind kind, int count, double massEach, params (string element, double fraction)[] composition)
    {
        var p = new CraftPiece { kind = CraftPieceKind.Fasteners, origin = "fasteners from the stores" };
        foreach (var c in composition)
            p.composition.Add(new CraftAmount(c.element, c.fraction * count * massEach));
        p.CompositionChanged();
        p.fasteners.kind = kind;
        p.fasteners.count = count;
        p.fasteners.massEach = massEach;
        p.fasteners.quality = 1;
        var s = new MetalSegment { length = kind == CraftFastenerKind.Buckle ? 50 : 14, width = 5, thickness = 5 };
        s.edge.temp = s.edge.prevTemp = s.body.temp = s.body.prevTemp = rules.Settings.defaultAmbient;
        p.segments.Add(s);
        return p;
    }

    /// <summary>Stock metals for test stations and scripts: name, composition.</summary>
    public static List<(string name, (string element, double fraction)[] composition)> StockMetals()
    {
        return new List<(string, (string, double)[])>
        {
            ("Wrought iron", new[] { (Iron, 0.9994), (Carbon, 0.0004), (Phosphorus, 0.0002) }),
            ("Mild steel", new[] { (Iron, 0.9975), (Carbon, 0.0018), (Manganese, 0.0007) }),
            ("Spring steel", new[] { (Iron, 0.9935), (Carbon, 0.0055), (Manganese, 0.001) }),
            ("Tool steel", new[] { (Iron, 0.9895), (Carbon, 0.0095), (Manganese, 0.001) }),
            ("Nickel steel", new[] { (Iron, 0.9655), (Carbon, 0.0075), (Nickel, 0.025), (Manganese, 0.002) }),
            ("Copper", new[] { (Copper, 0.998), (Iron, 0.002) }),
            ("Bronze", new[] { (Copper, 0.89), (Tin, 0.11) }),
            ("Brass", new[] { (Copper, 0.7), (Zinc, 0.3) }),
            ("Tin", new[] { (Tin, 1.0) }),
            ("Sterling silver", new[] { (Silver, 0.925), (Copper, 0.075) }),
            ("Gold", new[] { (Gold, 0.99), (Silver, 0.01) }),
            ("Mythril", new[] { (Mythril, 0.97), (Silver, 0.03) }),
            ("Starmetal steel", new[] { (Iron, 0.9325), (Carbon, 0.0075), (Starmetal, 0.05), (Nickel, 0.01) }),
            ("Orichalcum bronze", new[] { (Copper, 0.72), (Tin, 0.08), (Orichalcum, 0.2) }),
        };
    }
}
