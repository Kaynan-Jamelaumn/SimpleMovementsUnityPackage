using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The lapidary, the jeweller and the fibre bench: gemstones (by their real properties - hardness, toughness,
/// cleavage, refractive index, specific gravity, pleochroism - so the tests tell them apart), the cuts they are given,
/// polishing compounds, solders, scouring agents, wood sealers, how each raw fibre comes, and the jewellery designs.
/// </summary>
public static partial class CraftDefaults
{
    // ------------------------------------------------------------------ gems
    private static CraftGemDefinition Gem(string id, string name, string species, Color color, float hardness, float toughness, CraftCleavage cleavage, int planes,
        float ri, float sg, float dispersion, float pleo, bool heat, bool opaque, string cut, float carats, float clarity, float value, ElementType affinity, float strength,
        string observation, string description) =>
        new CraftGemDefinition
        {
            id = id, displayName = name, species = species, color = color, hardness = hardness, toughness = toughness, cleavage = cleavage, cleavagePlanes = planes,
            refractiveIndex = ri, specificGravity = sg, dispersion = dispersion, pleochroism = pleo, heatSensitive = heat, opaque = opaque, preferredCut = cut,
            typicalCarats = carats, clarity = clarity, valuePerCarat = value, affinity = affinity, affinityStrength = strength, observation = observation, description = description,
        };

    public static List<CraftGemDefinition> Gems() => new List<CraftGemDefinition>
    {
        Gem("rock_crystal", "Rock crystal", "quartz", new Color(0.92f, 0.94f, 0.96f), 7, 0.55f, CraftCleavage.None, 0, 1.544f, 2.65f, 0.013f, 0, false, false, "round_brilliant", 6, 0.85f, 3, ElementType.None, 0,
            "a clear, glassy six-sided crystal", "Clear quartz: common, hard enough, easy to cut. A good stone to learn on."),
        Gem("amethyst", "Amethyst", "quartz", new Color(0.55f, 0.3f, 0.7f), 7, 0.55f, CraftCleavage.None, 0, 1.544f, 2.65f, 0.013f, 0.25f, true, false, "oval", 4, 0.7f, 10, ElementType.Poison, 0.35f,
            "a violet glassy crystal, paler at its base", "Purple quartz. Its colour fades with heat: keep it cool on the lap. Said to keep the wearer sober - and from poison."),
        Gem("citrine", "Citrine", "quartz", new Color(0.92f, 0.68f, 0.2f), 7, 0.55f, CraftCleavage.None, 0, 1.544f, 2.65f, 0.013f, 0.1f, false, false, "cushion", 4, 0.8f, 8, ElementType.Fire, 0.3f,
            "a honey-yellow glassy crystal", "Golden quartz: warm, bright and forgiving to cut."),
        Gem("garnet", "Almandine garnet", "garnet", new Color(0.55f, 0.08f, 0.1f), 7.5f, 0.6f, CraftCleavage.None, 0, 1.79f, 4.05f, 0.024f, 0, false, false, "round_brilliant", 2.5f, 0.6f, 18, ElementType.Fire, 0.4f,
            "a dark red, heavy, rounded crystal", "Deep red and heavy for its size; no colour change with direction. Cut it shallow: it is dark."),
        Gem("peridot", "Peridot", "olivine", new Color(0.55f, 0.75f, 0.2f), 6.75f, 0.45f, CraftCleavage.Poor, 1, 1.67f, 3.34f, 0.02f, 0.15f, true, false, "oval", 2.5f, 0.65f, 25, ElementType.Earth, 0.4f,
            "an oily green pebble", "Olive-green and soft for a gem: it scratches and cracks with sudden heat. Polish it light and cool."),
        Gem("topaz", "Blue topaz", "topaz", new Color(0.45f, 0.72f, 0.92f), 8, 0.4f, CraftCleavage.Perfect, 1, 1.62f, 3.53f, 0.014f, 0.15f, false, false, "emerald_cut", 5, 0.8f, 20, ElementType.Wind, 0.4f,
            "a pale blue prism with flat, glassy faces", "Hard, but it cleaves perfectly across its length: one blow along the plane parts it - and a table cut along that plane flakes when it is polished."),
        Gem("aquamarine", "Aquamarine", "beryl", new Color(0.55f, 0.82f, 0.88f), 7.75f, 0.55f, CraftCleavage.Poor, 1, 1.58f, 2.7f, 0.014f, 0.35f, false, false, "emerald_cut", 4, 0.8f, 60, ElementType.Water, 0.6f,
            "a sea-blue six-sided prism", "Sea-blue beryl: clean, and bluest seen down its length - orient the table across the axis."),
        Gem("emerald", "Emerald", "beryl", new Color(0.12f, 0.62f, 0.32f), 7.75f, 0.35f, CraftCleavage.Poor, 1, 1.58f, 2.72f, 0.014f, 0.4f, true, false, "emerald_cut", 1.5f, 0.35f, 180, ElementType.Earth, 0.7f,
            "a deep green prism, full of cracks and mist", "Green beryl: always included, brittle, and its oil-filled cracks hate heat. Precious when sound."),
        Gem("ruby", "Ruby", "corundum", new Color(0.78f, 0.06f, 0.2f), 9, 0.8f, CraftCleavage.None, 0, 1.77f, 4f, 0.018f, 0.5f, false, false, "oval", 1.5f, 0.4f, 300, ElementType.Fire, 0.8f,
            "a red, heavy, flat-topped crystal", "Red corundum: tough and very hard - it wants alumina or diamond to polish. Its colour is richest seen down its axis."),
        Gem("sapphire", "Sapphire", "corundum", new Color(0.12f, 0.22f, 0.7f), 9, 0.8f, CraftCleavage.None, 0, 1.77f, 4f, 0.018f, 0.55f, false, false, "cushion", 2, 0.55f, 250, ElementType.Ice, 0.7f,
            "a blue, heavy, barrel-shaped crystal", "Blue corundum: as hard and tough as ruby; silk in it softens the colour."),
        Gem("diamond", "Diamond", "diamond", new Color(0.97f, 0.97f, 1f), 10, 0.7f, CraftCleavage.Perfect, 4, 2.417f, 3.52f, 0.044f, 0, false, false, "round_brilliant", 1, 0.6f, 900, ElementType.Holy, 0.8f,
            "a greasy-looking, eight-sided crystal", "The hardest stone - only diamond polishes it - with four perfect cleavages: the cutter cleaves it with a blow, or ruins it."),
        Gem("tourmaline", "Tourmaline", "tourmaline", new Color(0.85f, 0.25f, 0.5f), 7.25f, 0.5f, CraftCleavage.None, 0, 1.63f, 3.06f, 0.017f, 0.7f, false, false, "emerald_cut", 3, 0.55f, 40, ElementType.Lightning, 0.6f,
            "a striated pink and green prism", "Strongly pleochroic: dark down its length, bright across it. It holds a charge when warmed."),
        Gem("opal", "Opal", "opal", new Color(0.85f, 0.88f, 0.92f), 5.75f, 0.3f, CraftCleavage.None, 0, 1.45f, 2.1f, 0, 0, true, true, "cabochon_oval", 3, 0.7f, 70, ElementType.Water, 0.5f,
            "a milky stone with flashes of colour", "Soft, brittle and full of water: heat crazes it. Cut en cabochon, with no facets, to show its play of colour."),
        Gem("moonstone", "Moonstone", "feldspar", new Color(0.88f, 0.9f, 0.95f), 6.25f, 0.4f, CraftCleavage.Perfect, 2, 1.52f, 2.57f, 0.012f, 0, false, true, "cabochon_round", 4, 0.65f, 15, ElementType.Dark, 0.45f,
            "a pale stone with a blue sheen that moves", "Its sheen floats across a cabochon. Two perfect cleavages: handle it gently."),
        Gem("turquoise", "Turquoise", "turquoise", new Color(0.25f, 0.75f, 0.72f), 5.5f, 0.5f, CraftCleavage.None, 0, 1.62f, 2.7f, 0, 0, true, true, "cabochon_oval", 8, 0.5f, 12, ElementType.Water, 0.4f,
            "an opaque sky-blue nugget veined with brown", "Porous and soft: polish it cool, keep it from oil. Always a cabochon."),
        Gem("lapis_lazuli", "Lapis lazuli", "lazurite", new Color(0.12f, 0.2f, 0.62f), 5.5f, 0.45f, CraftCleavage.None, 0, 1.5f, 2.8f, 0, 0, false, true, "cabochon_round", 6, 0.6f, 10, ElementType.Holy, 0.35f,
            "a deep blue rock flecked with gold", "Deep blue rock with golden pyrite: a cabochon or a bead."),
    };

    // ------------------------------------------------------------------ cuts
    private static CraftCutTier Tier(string name, CraftCutTierKind kind, int facets, float offset = 0) => new CraftCutTier { name = name, kind = kind, facets = facets, angleOffset = offset };

    public static List<CraftGemCut> Cuts() => new List<CraftGemCut>
    {
        new CraftGemCut
        {
            id = "round_brilliant", displayName = "Round brilliant", lengthRatio = 1, yield = 0.4f,
            tiers = { Tier("Pavilion mains", CraftCutTierKind.Pavilion, 8), Tier("Lower girdle facets", CraftCutTierKind.Pavilion, 16, 1.2f), Tier("Girdle", CraftCutTierKind.Girdle, 16),
                Tier("Crown mains", CraftCutTierKind.Crown, 8), Tier("Stars", CraftCutTierKind.Crown, 8, -13), Tier("Upper girdle facets", CraftCutTierKind.Crown, 16, 5), Tier("Table", CraftCutTierKind.Table, 1) },
        },
        new CraftGemCut
        {
            id = "oval", displayName = "Oval brilliant", lengthRatio = 1.35f, yield = 0.45f,
            tiers = { Tier("Pavilion mains", CraftCutTierKind.Pavilion, 8), Tier("Lower girdle facets", CraftCutTierKind.Pavilion, 16, 1.2f), Tier("Girdle", CraftCutTierKind.Girdle, 16),
                Tier("Crown mains", CraftCutTierKind.Crown, 8), Tier("Stars", CraftCutTierKind.Crown, 8, -13), Tier("Table", CraftCutTierKind.Table, 1) },
        },
        new CraftGemCut
        {
            id = "cushion", displayName = "Cushion", lengthRatio = 1, yield = 0.48f,
            tiers = { Tier("Pavilion", CraftCutTierKind.Pavilion, 8), Tier("Girdle", CraftCutTierKind.Girdle, 8), Tier("Crown", CraftCutTierKind.Crown, 8), Tier("Table", CraftCutTierKind.Table, 1) },
        },
        new CraftGemCut
        {
            id = "emerald_cut", displayName = "Emerald (step) cut", lengthRatio = 1.4f, yield = 0.55f,
            tiers = { Tier("Pavilion steps", CraftCutTierKind.Pavilion, 12, 3), Tier("Pavilion keel", CraftCutTierKind.Pavilion, 8), Tier("Girdle", CraftCutTierKind.Girdle, 8),
                Tier("Crown steps", CraftCutTierKind.Crown, 8, -6), Tier("Table", CraftCutTierKind.Table, 1) },
        },
        new CraftGemCut
        {
            id = "cabochon_round", displayName = "Round cabochon", cabochon = true, lengthRatio = 1, yield = 0.65f,
            tiers = { Tier("Base", CraftCutTierKind.Base, 1), Tier("Dome", CraftCutTierKind.Dome, 1) },
        },
        new CraftGemCut
        {
            id = "cabochon_oval", displayName = "Oval cabochon", cabochon = true, lengthRatio = 1.3f, yield = 0.65f,
            tiers = { Tier("Base", CraftCutTierKind.Base, 1), Tier("Dome", CraftCutTierKind.Dome, 1) },
        },
    };

    // ------------------------------------------------------------------ supplies
    private static CraftSupplyDefinition Compound(string id, string name, string item, float hardness, float price, string description)
    {
        CraftSupplyDefinition s = Supply(id, name, CraftSupplyKind.Compound, item, 1, price, description);
        s.polishHardness = hardness;
        return s;
    }

    private static List<CraftSupplyDefinition> WorkshopSupplies() => new List<CraftSupplyDefinition>
    {
        // The lapidary's laps.
        Compound("tripoli", "Tripoli", "Tripoli", 6, 1, "A soft, chalky polish: opal, turquoise, lapis and the softer stones."),
        Compound("cerium_oxide", "Cerium oxide", "Cerium Oxide", 7.5f, 3, "The quartz polish: amethyst, citrine, beryl, peridot."),
        Compound("alumina", "Alumina", "Alumina Polish", 9, 5, "Fine aluminium oxide: topaz, corundum."),
        Compound("diamond_paste", "Diamond paste", "Diamond Paste", 10, 20, "Diamond dust in oil: it polishes anything - diamond itself."),
        // The jeweller's.
        Supply("silver_solder", "Silver solder", CraftSupplyKind.Solder, "Silver Solder", 1, 3, "Hard silver solder: flows a little below the silver itself."),
        Supply("gold_solder", "Gold solder", CraftSupplyKind.Solder, "Gold Solder", 1.2f, 12, "Gold solder that matches the colour of the gold."),
        // The fibre bench.
        Supply("soap", "Soft soap", CraftSupplyKind.Detergent, "Soft Soap", 1, 0.5f, "Lifts grease from fleece in warm water."),
        Supply("washing_soda", "Washing soda", CraftSupplyKind.Detergent, "Washing Soda", 1.3f, 0.6f, "Strong: it scours fast, hot water and all."),
        Supply("fullers_earth", "Fuller's earth", CraftSupplyKind.Detergent, "Fuller's Earth", 0.8f, 0.4f, "A clay that soaks up grease; gentle on wool."),
        // The woodshop's sealers.
        Supply("pine_resin", "Pine resin sealer", CraftSupplyKind.Varnish, "Pine Resin Sealer", 1.1f, 2, "Resin cooked in oil: a hard, amber film that stands wear and wet."),
        Supply("tung_oil", "Tung oil", CraftSupplyKind.Oil, "Tung Oil", 1.15f, 3, "Soaks in and hardens: it keeps water out of the grain without a film."),
    };

    /// <summary>How each raw fibre comes: its staple, grease, dirt and short fibre, whether it felts, and what one item weighs.</summary>
    private static void FibreTraits(List<CraftSupplyDefinition> supplies)
    {
        void Set(string id, float staple, float grease, float dirt, float shortFibre, bool felts, float grams, string description, string cloth)
        {
            CraftSupplyDefinition s = supplies.Find(x => x.id == id);
            if (s == null) return;
            s.staple = staple; s.grease = grease; s.dirt = dirt; s.shortFibre = shortFibre; s.felts = felts; s.gramsPerItem = grams; s.weavesInto = cloth;
            if (!string.IsNullOrEmpty(description)) s.description = description;
        }
        Set("wool_fibre", 90, 0.5f, 0.3f, 0.12f, true, 100, "Raw fleece in its grease: scour it, card or comb it, then spin it - or spin it as it comes, coarsely.", "wool_cloth");
        Set("flax_fibre", 120, 0.1f, 0.3f, 0.25f, false, 100, "Hackled flax tow with shives in it: pick it, comb it, spin it into linen.", "linen_cloth");
        Set("hemp_fibre", 150, 0.1f, 0.35f, 0.25f, false, 100, "", "canvas");
        Set("cotton_fibre", 28, 0.05f, 0.3f, 0.2f, false, 80, "Ginned cotton with seeds still in it: too short to comb - pick it and card it.", "cotton_cloth");
        Set("silk_floss", 400, 0.12f, 0.05f, 0.05f, false, 50, "", "silk_cloth");
    }

    // ------------------------------------------------------------------ jewellery designs
    private static CraftComponentSpec JewelMetal(string role, string name, CraftComponentFunction f, CraftJewelPart part, float importance) =>
        new CraftComponentSpec { role = role, displayName = name, kind = CraftPieceKind.Metal, function = f, jewelPart = part, importance = importance };

    private static CraftComponentSpec Stone(float min, float max, bool faceted = true, bool cabochon = true) =>
        new CraftComponentSpec
        {
            role = "stone", displayName = "Stone", kind = CraftPieceKind.Gem, function = CraftComponentFunction.Stone, importance = 1.4f,
            gem = new CraftGemSpec { caratsMin = min, caratsMax = max, facetedOk = faceted, cabochonOk = cabochon },
        };

    private static CraftDesign Jewel(string id, string name, ArmorSlotType slot, string group, string description, float magicDefense, float defense, int durability, float price,
        CraftComponentSpec[] parts, params CraftJointSpec[] joints)
    {
        var d = new CraftDesign
        {
            id = id, displayName = name, kind = CraftDesignKind.Jewelry, armorSlot = slot, description = description, magicDefense = magicDefense, defense = defense,
            durability = durability, price = price, gridSize = new Vector2Int(1, 1), menuGroup = $"{CraftDesignMenu.Jewellery}/{group}",
        };
        d.components.AddRange(parts);
        d.joints.AddRange(joints);
        return d;
    }

    private static void AddJewellery(List<CraftDesign> list)
    {
        CraftComponentSpec Solder() => SupplyPart("solder", "Solder", CraftSupplyKind.Solder, 1, CraftComponentFunction.Fastener);

        list.Add(Jewel("bezel_ring", "Bezel ring", ArmorSlotType.Ring, "Rings", "A band with the stone held all round in a collar of metal: secure, plain and strong.", 4, 0.5f, 400, 30,
            new[] { JewelMetal("band", "Band", CraftComponentFunction.Band, CraftJewelPart.Band, 0.8f), JewelMetal("setting", "Bezel", CraftComponentFunction.Setting, CraftJewelPart.Bezel, 0.6f), Stone(0.2f, 6), Solder() },
            Joint("seat", "Bezel soldered on", CraftJointKind.Solder, "band", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.BezelSet, "setting", "stone", 1, 1.2f)));
        list.Add(Jewel("solitaire_ring", "Solitaire ring", ArmorSlotType.Ring, "Rings", "One faceted stone held up in four claws to catch the light.", 5, 0.3f, 300, 40,
            new[] { JewelMetal("band", "Band", CraftComponentFunction.Band, CraftJewelPart.Band, 0.8f), JewelMetal("setting", "Prong head", CraftComponentFunction.Setting, CraftJewelPart.Prongs, 0.6f), Stone(0.2f, 5, true, false), Solder() },
            Joint("head", "Head soldered on", CraftJointKind.Solder, "band", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.ProngSet, "setting", "stone", 4, 1.2f)));
        list.Add(Jewel("signet_ring", "Signet ring", ArmorSlotType.Ring, "Rings", "A heavy band with a flat cabochon sunk flush in its face.", 3, 1, 600, 25,
            new[] { JewelMetal("band", "Band", CraftComponentFunction.Band, CraftJewelPart.Band, 1f), JewelMetal("setting", "Bezel", CraftComponentFunction.Setting, CraftJewelPart.Bezel, 0.6f), Stone(0.5f, 8, false, true), Solder() },
            Joint("seat", "Bezel soldered on", CraftJointKind.Solder, "band", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.BezelSet, "setting", "stone", 1, 1.2f)));
        list.Add(Jewel("pendant", "Pendant", ArmorSlotType.Amulet, "Pendants and necklaces", "A stone in a bezel, hung from a bail on a cord.", 5, 0, 300, 25,
            new[] { JewelMetal("bail", "Bail", CraftComponentFunction.Band, CraftJewelPart.Bail, 0.4f), JewelMetal("setting", "Bezel", CraftComponentFunction.Setting, CraftJewelPart.Bezel, 0.7f), Stone(0.5f, 20), Solder(),
                SupplyPart("cord", "Cord", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread) },
            Joint("bail", "Bail soldered on", CraftJointKind.Solder, "bail", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.BezelSet, "setting", "stone", 1, 1.2f)));
        list.Add(Jewel("gem_necklace", "Gem necklace", ArmorSlotType.Amulet, "Pendants and necklaces", "A faceted stone in six claws on a chain of hand-made links.", 7, 0, 350, 60,
            new[] { JewelMetal("chain", "Chain", CraftComponentFunction.Band, CraftJewelPart.Chain, 0.8f), JewelMetal("setting", "Prong head", CraftComponentFunction.Setting, CraftJewelPart.Prongs, 0.6f), Stone(0.5f, 15, true, false), Solder() },
            Joint("head", "Head soldered to the chain", CraftJointKind.Solder, "chain", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.ProngSet, "setting", "stone", 6, 1.2f)));
        list.Add(Jewel("amulet", "Amulet", ArmorSlotType.Amulet, "Pendants and necklaces", "A large cabochon in a heavy bezel on a chain: worn for what the stone holds.", 9, 0.5f, 450, 50,
            new[] { JewelMetal("chain", "Chain", CraftComponentFunction.Band, CraftJewelPart.Chain, 0.6f), JewelMetal("setting", "Bezel", CraftComponentFunction.Setting, CraftJewelPart.Bezel, 0.8f), Stone(2, 30, false, true), Solder() },
            Joint("bail", "Bezel soldered to the chain", CraftJointKind.Solder, "chain", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.BezelSet, "setting", "stone", 1, 1.2f)));
        list.Add(Jewel("circlet", "Circlet", ArmorSlotType.Helmet, "Circlets", "A band of metal round the brow with a stone at the front.", 8, 1.5f, 500, 70,
            new[] { JewelMetal("band", "Band", CraftComponentFunction.Band, CraftJewelPart.Band, 1f), JewelMetal("setting", "Bezel", CraftComponentFunction.Setting, CraftJewelPart.Bezel, 0.6f), Stone(0.5f, 20), Solder() },
            Joint("seat", "Bezel soldered on", CraftJointKind.Solder, "band", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.BezelSet, "setting", "stone", 1, 1.2f)));
        list.Add(Jewel("gem_bracelet", "Gem bracelet", ArmorSlotType.Bracers, "Bracelets", "A wide cuff with a stone in claws.", 6, 1, 450, 45,
            new[] { JewelMetal("band", "Cuff", CraftComponentFunction.Band, CraftJewelPart.Band, 1f), JewelMetal("setting", "Prong head", CraftComponentFunction.Setting, CraftJewelPart.Prongs, 0.6f), Stone(0.5f, 12, true, false), Solder() },
            Joint("head", "Head soldered on", CraftJointKind.Solder, "band", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.ProngSet, "setting", "stone", 4, 1.2f)));
        list.Add(Jewel("brooch", "Brooch", ArmorSlotType.Trinket, "Brooches and charms", "A stone in a bezel on a pin, worn at the shoulder or the throat.", 5, 0.5f, 400, 35,
            new[] { JewelMetal("pin", "Pin", CraftComponentFunction.Band, CraftJewelPart.Band, 0.5f), JewelMetal("setting", "Bezel", CraftComponentFunction.Setting, CraftJewelPart.Bezel, 0.7f), Stone(0.5f, 15), Solder() },
            Joint("seat", "Bezel soldered to the pin", CraftJointKind.Solder, "pin", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.BezelSet, "setting", "stone", 1, 1.2f)));
        list.Add(Jewel("charm", "Gem charm", ArmorSlotType.Trinket, "Brooches and charms", "A rough-tumbled cabochon caged in claws on a cord: a charm carried for what the stone holds.", 7, 0, 300, 30,
            new[] { JewelMetal("bail", "Bail", CraftComponentFunction.Band, CraftJewelPart.Bail, 0.4f), JewelMetal("setting", "Claw cage", CraftComponentFunction.Setting, CraftJewelPart.Prongs, 0.7f), Stone(1, 25, false, true), Solder(),
                SupplyPart("cord", "Cord", CraftSupplyKind.Thread, 1, CraftComponentFunction.Thread) },
            Joint("bail", "Bail soldered on", CraftJointKind.Solder, "bail", "setting", 1, 0.8f),
            Joint("set", "Stone set", CraftJointKind.ProngSet, "setting", "stone", 4, 1.2f)));
    }

    /// <summary>Wire and strip for the jeweller: (suffix, length, width, thickness mm).</summary>
    public static readonly (string suffix, float length, float width, float thickness)[] JewelleryForms =
    {
        ("wire", 600, 2.5f, 1.8f),
        ("bezel strip", 150, 4, 0.5f),
    };

    /// <summary>The stock metals a jeweller works (by name in <see cref="StockMetals"/>).</summary>
    public static readonly string[] JewelleryMetals = { "Copper", "Brass", "Bronze", "Sterling silver", "Gold", "Mythril", "Orichalcum bronze" };
}
