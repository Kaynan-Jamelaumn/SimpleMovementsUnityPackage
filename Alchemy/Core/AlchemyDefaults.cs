using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The built-in alchemy: a small, consistent chemistry that shows every rule of the system. Used when the project has
/// no Alchemy Database, and to fill a new one (Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Alchemy Database).
/// <para>The spoiler sheet is <c>Documentation/Alchemy/05-Default-Chemistry.md</c>. In short, at a simmer Solar Sap
/// (Sunpetal) becomes Vital Essence (healing); kept above 75 °C it slowly turns into Ardent Essence (vigour); above
/// 85 °C that burns out into ash and sludge. Frost Oil (Frostmint) turns Vital Essence into Serene Essence (calm) below
/// 80 °C but Ardent Essence into Tempered Essence (fortitude) above 60 °C - and above 110 °C into an Ember Spirit vapour
/// that only an alembic can catch (quickstep). Void Salt neutralises any active compound into Null Essence,
/// whatever the mixture was becoming. Quicksilver speeds the extraction, Lead Dust stops it, Rock Salt stabilises.</para>
/// </summary>
public static partial class AlchemyDefaults
{
    // Substance ids (code and tests use them).
    public const string Water = "water", Spirit = "spirit", Oil = "oil";
    public const string SolarSap = "solar_sap", FrostOil = "frost_oil", EmberDust = "ember_dust", BitterResin = "bitter_resin",
        MoonDew = "moon_dew", Brine = "brine", Quicksilver = "quicksilver", LeadDust = "lead_dust", VoidSalt = "void_salt",
        FungalPulp = "fungal_pulp", PetalOil = "petal_oil", BitterLatex = "bitter_latex";
    public const string VitalEssence = "vital_essence", ArdentEssence = "ardent_essence", SereneEssence = "serene_essence",
        TemperedEssence = "tempered_essence", RimeEssence = "rime_essence", ManaEssence = "mana_essence", EmberSpirit = "ember_spirit",
        NullEssence = "null_essence", Venom = "venom", UnstableAether = "unstable_aether";
    public const string MoonCrystal = "moon_crystal", FireAsh = "fire_ash", Sludge = "bitter_sludge", Char = "char";
    public const string Turpentine = "turpentine", AceticAcid = "acetic_acid", CitricAcid = "citric_acid", Lye = "lye", Chalk = "chalk",
        Alum = "alum", AlumCrystal = "alum_crystal", Niter = "niter", NiterCrystal = "niter_crystal", Silt = "silt", Tannin = "tannin",
        Iron = "iron", IronGallInk = "iron_gall_ink", LavenderOil = "lavender_oil", Rosin = "rosin", Sugars = "sugars", Yeast = "yeast",
        Esters = "esters", Beeswax = "beeswax", PepperOil = "pepper_oil", SalamanderEssence = "salamander_essence", Lecithin = "lecithin",
        Albumen = "albumen", EggCurds = "egg_curds", Lees = "lees", Casein = "casein", BoundToxin = "bound_toxin", Serpentine = "serpentine",
        Antitoxin = "antitoxin", Somnine = "somnine", Salicin = "salicin", WillowEssence = "willow_essence", PlantFibre = "plant_fibre",
        Sanctity = "sanctity", Charcoal = "charcoal", BlastingPowder = "blasting_powder", Soap = "soap";
    public const string SanguineSap = "sanguine_sap", SanguineEssence = "sanguine_essence", StormSalt = "storm_salt", StormEssence = "storm_essence",
        LichenAcid = "lichen_acid", StarfrostEssence = "starfrost_essence", ShadowSpores = "shadow_spores", UmbralEssence = "umbral_essence",
        DraconicIchor = "draconic_ichor", WyrmbloodEssence = "wyrmblood_essence", Gingerol = "gingerol", GingerEssence = "ginger_essence",
        RoseOil = "rose_oil", Brimstone = "brimstone", Vitriol = "vitriol", Vigorine = "vigorine", Mandragorin = "mandragorin",
        RestorativeEssence = "restorative_essence", PhoenixAsh = "phoenix_ash", PhoenixEssence = "phoenix_essence",
        AetherDown = "aether_down", ZephyrEssence = "zephyr_essence";
    public const string EmberSalt = "ember_salt", SomnineSalt = "somnine_salt", AlchemicalGold = "alchemical_gold",
        BerserkerEssence = "berserker_essence", Panacea = "panacea";
    public const string SilverSap = "silver_sap", HolyEssence = "holy_essence", KelpGel = "kelp_gel", GillEssence = "gill_essence",
        AshPollen = "ash_pollen", FirewardEssence = "fireward_essence";

    /// <summary>Replaces the database's content with the built-in alchemy.</summary>
    public static void Fill(AlchemyDatabase db)
    {
        db.settings = new AlchemySimulationSettings();
        db.properties = Properties();
        db.substances = Substances();
        db.reactions = Reactions();
        db.outcomes = Outcomes();
        db.forms = AlchemyIngredientForm.Defaults();
        db.filters = Filters();
        db.fuels = Fuels();
        db.notes = "Built-in alchemy (AlchemyDefaults). Spoilers: Documentation/Alchemy/05-Default-Chemistry.md.";
        db.MarkChanged();
    }

    /// <summary>
    /// Adds the built-in properties, substances, reactions, outcomes, filters and fuels that <paramref name="db"/> does not
    /// have yet (by id), leaving everything already there as it is. Returns how many were added.
    /// </summary>
    public static int AddMissing(AlchemyDatabase db)
    {
        if (db == null)
            return 0;
        int n = AddMissing(db.properties, Properties(), e => e.id);
        n += AddMissing(db.substances, Substances(), e => e.id);
        n += AddMissing(db.reactions, Reactions(), e => e.id);
        n += AddMissing(db.outcomes, Outcomes(), e => e.id);
        n += AddMissing(db.filters, Filters(), e => e.id);
        n += AddMissing(db.fuels, Fuels(), e => e.id);
        if (n > 0) db.MarkChanged();
        return n;
    }

    private static int AddMissing<T>(List<T> into, List<T> from, System.Func<T, string> id) where T : class
    {
        if (into == null || from == null)
            return 0;
        int n = 0;
        foreach (T x in from)
        {
            if (x == null || into.Exists(e => e != null && string.Equals(id(e), id(x), System.StringComparison.OrdinalIgnoreCase))) continue;
            into.Add(x);
            n++;
        }
        return n;
    }

    /// <summary>The built-in rules without a database asset (tests, tools).</summary>
    public static AlchemyRules Rules(AlchemySimulationSettings settings = null) =>
        AlchemyRules.Compile(settings ?? new AlchemySimulationSettings(), Properties(), Substances(), Reactions(), Outcomes(),
            AlchemyIngredientForm.Defaults(), Filters(), Fuels());

    // ------------------------------------------------------------------ properties
    public static List<AlchemyPropertyDefinition> Properties() => new List<AlchemyPropertyDefinition>
    {
        P("vitality", "Vitality", "Life and healing.", new Color(0.9f, 0.3f, 0.4f)),
        P("fire", "Fire", "Heat, vigour, burning.", new Color(1f, 0.55f, 0.15f)),
        P("frost", "Frost", "Cold, stillness.", new Color(0.55f, 0.85f, 1f)),
        P("arcane", "Arcane", "Raw magic.", new Color(0.65f, 0.45f, 1f)),
        P("mind", "Mind", "Calm and clarity.", new Color(0.4f, 0.85f, 0.8f)),
        P("toxicity", "Toxicity", "Poison.", new Color(0.45f, 0.75f, 0.2f)),
        P("volatility", "Reactivity", "How violently it reacts (how easily it evaporates is its boiling point).", new Color(1f, 0.85f, 0.2f)),
        P("stability", "Stability", "How well it keeps.", new Color(0.7f, 0.72f, 0.78f)),
        P("organic", "Organic", "Plant and animal matter: it rots, it scorches.", new Color(0.45f, 0.6f, 0.3f)),
        P("mineral", "Mineral", "Salts, metals and stones.", new Color(0.6f, 0.55f, 0.5f)),
        P("acid", "Acid", "Sourness: lowers the pH. Acids and alkalis neutralise each other.", new Color(0.85f, 0.95f, 0.3f)),
        P("alkali", "Alkali", "Bitterness and slipperiness: raises the pH.", new Color(0.5f, 0.6f, 0.95f)),
        P("astringent", "Astringent", "Draws things together: puckers, binds, tans.", new Color(0.65f, 0.45f, 0.3f)),
        P("clarifying", "Clarifying", "Makes suspended particles settle out of a liquid.", new Color(0.85f, 0.9f, 0.95f)),
        P("emulsifier", "Emulsifier", "Holds oil and water together once they are stirred into one.", new Color(0.95f, 0.85f, 0.55f)),
        P("contaminant", "Contaminant", "Dirt that gets in the way of every reaction and spoils a potion's purity.", new Color(0.45f, 0.35f, 0.25f)),
        P("oxidizable", "Oxidisable", "Browns and spoils when air is beaten into it (faster with iron).", new Color(0.7f, 0.5f, 0.3f)),
    };

    private static AlchemyPropertyDefinition P(string id, string name, string description, Color color) =>
        new AlchemyPropertyDefinition { id = id, displayName = name, description = description, color = color };

    // ------------------------------------------------------------------ substances
    public static List<AlchemySubstance> Substances()
    {
        List<AlchemySubstance> list = BaseSubstances();
        list.AddRange(HerbalSubstances());
        return list;
    }

    private static List<AlchemySubstance> BaseSubstances() => new List<AlchemySubstance>
    {
        // Solvents
        S(Water, "Water", SubstanceRole.Solvent, new Color(0.55f, 0.72f, 0.9f), 100f, "Clear water.", "clear water").Faint(0.15f).Known()
            .WithDensity(1f).InLayer(LiquidLayer.Aqueous),
        S(Spirit, "Grain Spirit", SubstanceRole.Solvent, new Color(0.9f, 0.88f, 0.75f), 78f, "Strong alcohol: dissolves resins, preserves, burns.", "a sharp, boozy liquid")
            .Faint(0.2f).With("volatility", 0.15f).Known().WithDensity(0.79f).PerItem(6f),
        S(Oil, "Lamp Oil", SubstanceRole.Solvent, new Color(0.85f, 0.7f, 0.3f), 260f, "A heavy oil that lets a mixture get far hotter than water allows.", "a thick yellow oil")
            .Faint(0.3f).With("organic", 0.2f).Known().WithDensity(0.9f).InLayer(LiquidLayer.Oily),
        S(Turpentine, "Turpentine", SubstanceRole.Solvent, new Color(0.95f, 0.9f, 0.7f), 155f, "Distilled pine resin: a sharp, oily solvent that floats on water and dissolves resins and waxes.", "a sharp piney smell")
            .Faint(0.2f).With("organic", 0.3f).With("fire", 0.2f).With("volatility", 0.1f).WithDensity(0.86f).InLayer(LiquidLayer.Oily),

        // Raw matter released by ingredients
        S(SolarSap, "Solar Sap", SubstanceRole.Raw, new Color(1f, 0.82f, 0.25f), 160f, "The sweet sap of sunpetals.", "a golden, sweet-smelling sap")
            .With("vitality", 0.4f).With("fire", 0.2f).With("organic", 0.6f).With("oxidizable", 0.3f).WithDensity(1.2f)
            .StableUpTo(135f, 0.03f, "the golden sap caramelises and turns bitter", (Sludge, 0.6f)),
        S(PetalOil, "Sunpetal Oil", SubstanceRole.Byproduct, new Color(1f, 0.92f, 0.55f), 95f, "The fragrant, volatile oil of sunpetals. Water barely holds it.", "a fragrant oily sheen")
            .Faint(0.5f).With("fire", 0.3f).With("volatility", 0.4f).With("organic", 0.3f)
            .Dissolves(Water, 0.02f).Dissolves(Spirit, 0.6f).Dissolves(Oil, 1f).Dissolves(Turpentine, 1f).WithDensity(0.9f).Volatile(2f),
        S(BitterLatex, "Bitter Latex", SubstanceRole.Raw, new Color(0.92f, 0.92f, 0.85f), 200f, "A toxic milky latex locked in sunpetal stems. It does not survive real heat.", "a milky, bitter-smelling trace")
            .With("toxicity", 1f).With("organic", 0.3f)
            .StableUpTo(85f, 0.15f, "the milky trace curdles and vanishes"),
        S(FrostOil, "Frost Oil", SubstanceRole.Raw, new Color(0.7f, 0.95f, 1f), 140f, "A cold, minty oil.", "a pale oil that frosts the rim")
            .With("frost", 0.6f).With("mind", 0.2f).With("organic", 0.4f).WithDensity(0.92f),
        S(EmberDust, "Ember Dust", SubstanceRole.Raw, new Color(0.9f, 0.25f, 0.1f), 900f, "Glowing spores of the embercap.", "red specks that glow when stirred")
            .Solid().With("fire", 0.8f).With("volatility", 0.4f).With("mineral", 0.2f).WithDensity(2.2f),
        S(BitterResin, "Bitter Resin", SubstanceRole.Raw, new Color(0.25f, 0.4f, 0.15f), 190f, "A poisonous resin.", "a dark green, bitter resin")
            .With("toxicity", 0.6f).With("organic", 0.5f)
            .Dissolves(Water, 0.3f).Dissolves(Spirit, 1f).Dissolves(Oil, 0.5f).Dissolves(Turpentine, 1f).WithDensity(1.1f),
        S(MoonDew, "Moon Dew", SubstanceRole.Raw, new Color(0.8f, 0.78f, 1f), 90f, "Dew gathered from moonblooms.", "a silvery, shimmering dew")
            .With("arcane", 0.6f).With("mind", 0.3f)
            .Dissolves(Water, 1f).Dissolves(Spirit, 0.4f).Dissolves(Oil, 0.03f).Dissolves(Turpentine, 0.02f).WithDensity(1.05f),
        S(Brine, "Brine", SubstanceRole.Reagent, new Color(0.85f, 0.85f, 0.8f), 900f, "Dissolved rock salt. Keeps things from changing.", "a cloudy, salty liquid")
            .Faint(0.3f).With("mineral", 0.6f).With("stability", 0.6f)
            .Dissolves(Water, 0.36f).Dissolves(Spirit, 0.01f).Dissolves(Oil, 0.001f).Dissolves(Turpentine, 0.001f).SolubilityDoubling(400f).PerItem(3f)
            .WithDensity(1.2f),
        S(Quicksilver, "Quicksilver", SubstanceRole.Reagent, new Color(0.8f, 0.82f, 0.85f), 357f, "Liquid metal that hurries reactions along.", "beads of liquid metal")
            .With("mineral", 0.7f).With("arcane", 0.2f).With("toxicity", 0.3f).With("volatility", 0.2f).WithDensity(13.5f),
        S(LeadDust, "Lead Dust", SubstanceRole.Reagent, new Color(0.4f, 0.42f, 0.45f), 900f, "Heavy grey dust that deadens reactions.", "a heavy grey haze")
            .Solid().With("mineral", 0.9f).With("toxicity", 0.2f).WithDensity(11f),
        S(VoidSalt, "Void Salt", SubstanceRole.Reagent, new Color(0.15f, 0.12f, 0.2f), 900f, "A salt that swallows magic.", "black grains that drink the light")
            .With("mineral", 0.5f).With("stability", 0.8f).WithDensity(2.5f),
        S(FungalPulp, "Fungal Pulp", SubstanceRole.Raw, new Color(0.55f, 0.45f, 0.35f), 120f, "Mushroom flesh.", "a brown, earthy pulp")
            .With("organic", 0.8f).With("toxicity", 0.05f).WithDensity(1.05f),

        // Active compounds (potions)
        S(VitalEssence, "Vital Essence", SubstanceRole.Active, new Color(0.9f, 0.2f, 0.35f), 180f, "The healing principle of sunpetals.", "a deep rose liquid that smells of summer")
            .With("vitality", 1f).With("organic", 0.1f).With("oxidizable", 0.35f).WithDensity(1.05f).InLayer(LiquidLayer.Aqueous)
            .StableUpTo(115f, 0.03f, "the rose turns a muddy brown", (Sludge, 0.8f))
            .StableBetweenPh(3f, 11.5f, 0.03f, "the rose curdles to a dull, sour pink"),
        S(ArdentEssence, "Ardent Essence", SubstanceRole.Active, new Color(1f, 0.5f, 0.1f), 200f, "Vital essence cooked into pure vigour.", "a warm amber liquid")
            .With("fire", 0.8f).With("vitality", 0.3f)
            .StableUpTo(170f, 0.03f, "the amber blackens", (FireAsh, 0.6f)),
        S(SereneEssence, "Serene Essence", SubstanceRole.Active, new Color(0.25f, 0.75f, 0.7f), 150f, "Vitality calmed by frost.", "a calm teal liquid")
            .With("mind", 0.9f).With("frost", 0.3f).With("arcane", 0.2f)
            .StableUpTo(75f, 0.02f, "the teal fades and clouds", (Water, 0.5f), (Sludge, 0.3f)),
        S(TemperedEssence, "Tempered Essence", SubstanceRole.Active, new Color(0.45f, 0.55f, 0.75f), 210f, "Fire quenched by frost: hard as steel.", "a steel-blue liquid")
            .With("fire", 0.4f).With("frost", 0.4f).With("stability", 0.8f),
        S(RimeEssence, "Rime Essence", SubstanceRole.Active, new Color(0.8f, 0.95f, 1f), 140f, "Frost oil settled in the cold.", "an icy, almost white liquid")
            .With("frost", 0.9f).With("stability", 0.3f)
            .StableUpTo(25f, 0.01f, "the frost flowers melt away", (Water, 1f)),
        S(ManaEssence, "Mana Essence", SubstanceRole.Active, new Color(0.5f, 0.3f, 0.95f), 170f, "Moon dew woken by gentle heat.", "a violet liquid that hums faintly")
            .With("arcane", 1f)
            .Dissolves(Water, 1f).Dissolves(Spirit, 0.5f).Dissolves(Oil, 0.02f).Dissolves(Turpentine, 0.02f).WithDensity(1.02f)
            .StableUpTo(85f, 0.02f, "the hum fades as the violet pales", (Water, 0.8f)),
        S(EmberSpirit, "Ember Spirit", SubstanceRole.Active, new Color(1f, 0.65f, 0.3f), 60f, "A fiery vapour born when fire and frost clash in the heat.", "shimmering orange fumes")
            .With("fire", 0.6f).With("volatility", 0.7f),
        S(NullEssence, "Null Essence", SubstanceRole.Active, new Color(0.75f, 0.75f, 0.75f), 220f, "What is left when void salt swallows a compound's power: perfectly still.", "a grey liquid that does not ripple")
            .With("stability", 1f),
        S(Venom, "Venom", SubstanceRole.Active, new Color(0.35f, 0.6f, 0.1f), 230f, "Bitter resin drawn out by spirit.", "a sickly green, oily liquid")
            .With("toxicity", 1f)
            .Dissolves(Water, 0.01f).Dissolves(Spirit, 1f).Dissolves(Oil, 0.5f).Dissolves(Turpentine, 0.5f).WithDensity(1.1f)
            .StableUpTo(95f, 0.03f, "the green film breaks up", (Sludge, 1f)),
        S(UnstableAether, "Unstable Aether", SubstanceRole.Intermediate, new Color(1f, 0.95f, 0.55f), 170f, "Overcharged magic. Do not heat it.", "a crackling, fizzing light")
            .With("arcane", 0.6f).With("volatility", 0.9f),

        // Byproducts and residues
        S(MoonCrystal, "Moon Crystal", SubstanceRole.Byproduct, new Color(0.85f, 0.85f, 1f), 900f, "Crystals that grow from moon dew and brine in the cold, still night.", "pale crystals forming at the bottom")
            .Solid().With("arcane", 0.5f).With("mineral", 0.5f).WithDensity(2.3f),
        S(FireAsh, "Fire Ash", SubstanceRole.Residue, new Color(0.35f, 0.3f, 0.28f), 900f, "Burnt-out ardent essence.", "grey flakes swirling down")
            .Solid().With("fire", 0.2f).With("mineral", 0.3f).WithDensity(0.6f),
        S(Sludge, "Bitter Sludge", SubstanceRole.Residue, new Color(0.3f, 0.27f, 0.15f), 190f, "Spoiled matter.", "a murky brown film")
            .With("toxicity", 0.3f).With("organic", 0.3f).With("contaminant", 0.3f).WithDensity(1.15f),
        S(Char, "Char", SubstanceRole.Residue, new Color(0.08f, 0.07f, 0.07f), 900f, "Scorched organic matter.", "black crust at the bottom")
            .Solid().With("mineral", 0.2f).WithDensity(0.5f),

        // ---- Acids and alkalis: the pH decides what can happen and what comes out of an ingredient.
        S(AceticAcid, "Acetic Acid", SubstanceRole.Reagent, new Color(0.95f, 0.92f, 0.7f), 118f, "The sourness of vinegar.", "a sharp, sour smell")
            .Faint(0.1f).With("acid", 1f).With("organic", 0.1f).WithDensity(1.05f),
        S(CitricAcid, "Citric Acid", SubstanceRole.Reagent, new Color(1f, 0.95f, 0.55f), 900f, "The tartness of sourfruit.", "a mouth-puckering tartness")
            .Faint(0.2f).With("acid", 1.3f).WithDensity(1.5f)
            .Dissolves(Water, 0.6f).Dissolves(Spirit, 0.3f).Dissolves(Oil, 0.005f).Dissolves(Turpentine, 0.005f),
        S(Lye, "Potash Lye", SubstanceRole.Reagent, new Color(0.85f, 0.85f, 0.78f), 900f, "Leached wood ash: a strong alkali that eats fats and skin.", "a slippery, soapy feel")
            .Faint(0.2f).With("alkali", 1.4f).With("toxicity", 0.15f).With("mineral", 0.4f).WithDensity(1.4f)
            .Dissolves(Water, 1.1f).Dissolves(Spirit, 0.05f).Dissolves(Oil, 0.001f).Dissolves(Turpentine, 0.001f),
        S(Chalk, "Chalk", SubstanceRole.Reagent, new Color(0.95f, 0.95f, 0.92f), 900f, "Powdered chalk: it barely dissolves, so it neutralises acid slowly - and fizzes.", "a white, chalky haze")
            .With("alkali", 0.8f).With("mineral", 0.8f).WithDensity(2.7f)
            .Dissolves(Water, 0.004f).Dissolves(Spirit, 0.0005f).Dissolves(Oil, 0.0005f).Dissolves(Turpentine, 0.0005f).SolubilityDoubling(0f),

        // ---- Salts that crystallise: dissolve hot, filter, cool slowly and still, collect the crystals.
        S(Alum, "Alum", SubstanceRole.Reagent, new Color(0.92f, 0.92f, 0.96f), 900f, "A sour mineral salt that makes suspended dirt settle. It dissolves well only when hot.", "a faint, astringent sourness")
            .Faint(0.2f).With("astringent", 0.8f).With("clarifying", 0.6f).With("acid", 0.2f).With("mineral", 0.6f).WithDensity(1.75f)
            .Dissolves(Water, 0.06f).Dissolves(Spirit, 0.002f).Dissolves(Oil, 0.0005f).Dissolves(Turpentine, 0.0005f).SolubilityDoubling(16f)
            .CrystallisesAs(AlumCrystal, 1.4f, 0.06f, 0.8f).PerItem(2f),
        S(AlumCrystal, "Alum Crystals", SubstanceRole.Byproduct, new Color(0.95f, 0.95f, 1f), 900f, "Clear, many-sided crystals of alum.", "clear glassy crystals on the walls")
            .Solid().With("mineral", 0.7f).With("astringent", 0.4f).WithDensity(1.75f).PerItem(2f),
        S(Niter, "Niter", SubstanceRole.Reagent, new Color(0.95f, 0.95f, 0.9f), 900f, "Dissolved saltpetre: it feeds fire, and dissolves far better hot than cold.", "a cool, salty bitterness")
            .Faint(0.2f).With("fire", 0.3f).With("mineral", 0.6f).WithDensity(2.1f)
            .Dissolves(Water, 0.3f).Dissolves(Spirit, 0.01f).Dissolves(Oil, 0.001f).Dissolves(Turpentine, 0.001f).SolubilityDoubling(22f)
            .CrystallisesAs(NiterCrystal, 1.6f, 0.06f, 0.8f).PerItem(2f),
        S(NiterCrystal, "Saltpetre Crystals", SubstanceRole.Byproduct, new Color(0.97f, 0.97f, 0.95f), 900f, "Long white needles of saltpetre.", "white needles bristling at the bottom")
            .Solid().With("fire", 0.3f).With("mineral", 0.7f).WithDensity(2.1f).PerItem(2f),
        S(Silt, "Silt", SubstanceRole.Residue, new Color(0.45f, 0.38f, 0.28f), 900f, "Fine mud. It clouds whatever it is in and gets in the way of every reaction. Filter it, or let alum settle it.", "a brown, gritty cloud")
            .With("contaminant", 1f).With("mineral", 0.6f).WithDensity(1.08f)
            .Dissolves(Water, 0.0005f).Dissolves(Spirit, 0.0002f).Dissolves(Oil, 0.0002f).Dissolves(Turpentine, 0.0002f).SolubilityDoubling(0f),

        // ---- Tannins, iron and ink.
        S(Tannin, "Tannin", SubstanceRole.Raw, new Color(0.55f, 0.35f, 0.2f), 900f, "The bitter, puckering stuff of bark and galls. Iron turns it into ink; egg white binds it.", "a brown, puckering bitterness")
            .With("astringent", 1f).With("organic", 0.4f).With("oxidizable", 0.5f).WithDensity(1.3f)
            .Dissolves(Water, 0.5f).Dissolves(Spirit, 0.8f).Dissolves(Oil, 0.01f).Dissolves(Turpentine, 0.01f)
            .ExtractsBestAtPh(9f, 0.3f),
        S(Iron, "Iron Filings", SubstanceRole.Reagent, new Color(0.35f, 0.35f, 0.38f), 900f, "Fine iron: it blackens tannins into ink and hurries the browning of anything that spoils in air.", "dark metal grit at the bottom")
            .Solid().With("mineral", 0.9f).WithDensity(7.8f),
        S(IronGallInk, "Iron Gall Ink", SubstanceRole.Active, new Color(0.12f, 0.1f, 0.18f), 300f, "A deep black ink that bites into parchment and never fades.", "an inky black liquid")
            .With("astringent", 0.3f).With("mineral", 0.2f).WithDensity(1.1f),

        // ---- Volatile oils and resins: distil them, separate them, or lose them to the air.
        S(LavenderOil, "Lavender Oil", SubstanceRole.Active, new Color(0.75f, 0.6f, 0.95f), 92f, "The calming oil of lavender. Water barely holds it, spirit and oil do; it distils over before water boils.", "a sweet, floral scent")
            .With("mind", 0.6f).With("organic", 0.3f).With("oxidizable", 0.35f).WithDensity(0.88f).Volatile(2.5f)
            .Dissolves(Water, 0.01f).Dissolves(Spirit, 1f).Dissolves(Oil, 1f).Dissolves(Turpentine, 1f)
            .StableUpTo(160f, 0.03f, "the floral scent turns acrid", (Sludge, 0.6f)),
        S(Rosin, "Rosin", SubstanceRole.Byproduct, new Color(0.8f, 0.55f, 0.2f), 900f, "The amber solid left of pine resin: turpentine and oil dissolve it, water does not.", "sticky amber lumps")
            .With("organic", 0.5f).WithDensity(1.08f)
            .Dissolves(Water, 0.0005f).Dissolves(Spirit, 0.6f).Dissolves(Oil, 0.5f).Dissolves(Turpentine, 1.5f).SolubilityDoubling(20f),

        // ---- Sugars and fermentation.
        S(Sugars, "Sugars", SubstanceRole.Raw, new Color(1f, 0.85f, 0.45f), 900f, "Sweetness from honey and fruit. Yeast turns it into spirit; it keeps things from rotting; it burns above 160 °C.", "a sticky sweetness")
            .With("organic", 0.4f).With("vitality", 0.1f).WithDensity(1.45f)
            .Dissolves(Water, 2f).Dissolves(Spirit, 0.15f).Dissolves(Oil, 0.005f).Dissolves(Turpentine, 0.005f)
            .StableUpTo(160f, 0.04f, "the sweetness darkens into bitter caramel", (Char, 0.5f)),
        S(Yeast, "Live Yeast", SubstanceRole.Reagent, new Color(0.9f, 0.85f, 0.7f), 900f, "Brewer's yeast: alive, it turns sugars into spirit. Above 42 °C it dies, and strong spirit kills it too.", "a bready, frothy smell")
            .With("organic", 0.4f).WithDensity(1.1f)
            .StableUpTo(42f, 0.2f, "the froth collapses"),
        S(Esters, "Esters", SubstanceRole.Byproduct, new Color(1f, 0.95f, 0.8f), 77f, "The fruity scent of fermentation. Volatile: it distils over with the spirit.", "a fruity, winey aroma")
            .Faint(0.1f).With("organic", 0.1f).WithDensity(0.9f).Volatile(1.5f),

        // ---- Wax, oils and salves; pepperfire; egg; milk.
        S(Beeswax, "Beeswax", SubstanceRole.Byproduct, new Color(0.95f, 0.8f, 0.35f), 900f, "Wax from the comb: it melts into hot oil and sets as it cools - the body of every salve.", "golden flecks of wax")
            .With("organic", 0.3f).With("stability", 0.3f).WithDensity(0.96f)
            .Dissolves(Water, 0.0002f).Dissolves(Spirit, 0.01f).Dissolves(Oil, 0.15f).Dissolves(Turpentine, 0.4f).SolubilityDoubling(10f),
        S(PepperOil, "Pepperfire Oil", SubstanceRole.Raw, new Color(0.95f, 0.25f, 0.1f), 300f, "The burning oil of fire peppers. It barely touches water; oil and spirit take it up.", "a red, eye-watering oil")
            .With("fire", 0.9f).With("organic", 0.3f).With("toxicity", 0.02f).WithDensity(0.94f)
            .Dissolves(Water, 0.01f).Dissolves(Spirit, 0.5f).Dissolves(Oil, 1f).Dissolves(Turpentine, 0.8f),
        S(SalamanderEssence, "Salamander Essence", SubstanceRole.Active, new Color(1f, 0.35f, 0.2f), 220f, "Vital essence bound to pepperfire: it warms and mends from within.", "a glowing scarlet liquid")
            .With("fire", 0.6f).With("vitality", 0.5f).WithDensity(1.02f)
            .StableUpTo(120f, 0.03f, "the scarlet glow dims to brown", (Sludge, 0.8f)),
        S(Lecithin, "Lecithin", SubstanceRole.Reagent, new Color(1f, 0.9f, 0.5f), 900f, "From egg yolk: it holds oil and water together (an emulsifier).", "a creamy smoothness")
            .Faint(0.3f).With("emulsifier", 1f).With("organic", 0.3f).WithDensity(1f),
        S(Albumen, "Egg Albumen", SubstanceRole.Raw, new Color(0.98f, 0.97f, 0.9f), 900f, "Egg white: it binds tannins and clears a liquid - but sets into curds above 62 °C.", "a clear, slippery strand")
            .Faint(0.2f).With("organic", 0.4f).With("vitality", 0.1f).With("clarifying", 0.3f).WithDensity(1.04f)
            .StableUpTo(62f, 0.1f, "white curds set in the liquid", (EggCurds, 1f))
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(Tannin), 0.6f, "the cloudiness gathers into soft flocks", (Lees, 1.5f)),
        S(EggCurds, "Egg Curds", SubstanceRole.Residue, new Color(0.98f, 0.95f, 0.8f), 900f, "Cooked egg. It has no business in a potion.", "rubbery white curds")
            .Solid().With("contaminant", 0.5f).WithDensity(1.05f),
        S(Lees, "Lees", SubstanceRole.Residue, new Color(0.5f, 0.38f, 0.28f), 900f, "Tannins bound by egg white: a soft sediment. Filter or decant it off.", "soft brown flocks settling")
            .Solid().WithDensity(1.3f),
        S(Casein, "Milk Curd", SubstanceRole.Raw, new Color(0.97f, 0.96f, 0.9f), 900f, "The protein of milk: it binds venoms - and curdles in acid.", "a milky whiteness")
            .With("organic", 0.5f).With("vitality", 0.2f).WithDensity(1.3f)
            .Dissolves(Water, 0.2f).Dissolves(Spirit, 0.01f).Dissolves(Oil, 0.01f).Dissolves(Turpentine, 0.005f)
            .Interacts(InteractionKind.Curdles, SubstanceSelector.WithProperty("acid", 0.5f), 4f)
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(Venom), 0.4f, "the milk curdles green around the venom", (BoundToxin, 1.5f)),
        S(BoundToxin, "Bound Toxins", SubstanceRole.Residue, new Color(0.5f, 0.55f, 0.4f), 900f, "Poison locked into curds or charcoal: harmless while it stays bound. Filter it out.", "grey-green flecks")
            .Solid().WithDensity(1.2f),

        // ---- Alkaloids and bark: the pH draws them out, gentle heat transforms them.
        S(Serpentine, "Serpentine", SubstanceRole.Raw, new Color(0.6f, 0.75f, 0.4f), 900f, "The bitter alkaloid of snakeweed: it comes out of the root far better in acid.", "a bitter, numbing taste")
            .With("toxicity", 0.05f).With("mind", 0.2f).With("organic", 0.2f).WithDensity(1.15f).ExtractsBestAtPh(3.5f, 0.8f),
        S(Antitoxin, "Antitoxin", SubstanceRole.Active, new Color(0.55f, 0.85f, 0.55f), 210f, "Serpentine tempered by gentle heat: it fights poison in the blood.", "a pale green, faintly glowing liquid")
            .With("vitality", 0.4f).With("stability", 0.3f).WithDensity(1.05f)
            .StableUpTo(85f, 0.03f, "the pale glow fades", (Sludge, 0.6f)).StableBetweenPh(2.5f, 14f, 0.02f, "the green turns a dull grey"),
        S(Somnine, "Somnine", SubstanceRole.Active, new Color(0.35f, 0.25f, 0.6f), 200f, "The sleep-bringing alkaloid of dreamshade berries. Acid draws it out; alkali and heat above 75 °C destroy it.", "a dusky violet tint")
            .With("mind", 0.6f).With("toxicity", 0.03f).WithDensity(1.05f).ExtractsBestAtPh(4f, 0.7f)
            .StableUpTo(75f, 0.04f, "the violet fades to grey", (Sludge, 0.5f)).StableBetweenPh(0f, 9f, 0.05f, "the violet turns a murky green"),
        S(Salicin, "Salicin", SubstanceRole.Raw, new Color(0.85f, 0.75f, 0.6f), 900f, "The bitter principle of willow bark: warm acid turns it into willow essence.", "a dry bitterness")
            .With("organic", 0.3f).With("vitality", 0.1f).WithDensity(1.2f).ExtractsBestAtPh(5f, 0.3f),
        S(WillowEssence, "Willow Essence", SubstanceRole.Active, new Color(0.95f, 0.85f, 0.7f), 220f, "It eases pain and the body's hurts.", "a pale amber, bitter liquid")
            .With("vitality", 0.3f).With("mind", 0.3f).WithDensity(1.05f).StableUpTo(110f, 0.03f, "the amber clouds", (Sludge, 0.6f)),
        S(PlantFibre, "Plant Fibre", SubstanceRole.Raw, new Color(0.55f, 0.5f, 0.35f), 900f, "Woody fibre: it never dissolves. Strain it out.", "stringy shreds of fibre")
            .With("organic", 0.6f).WithDensity(1.1f)
            .Dissolves(Water, 0.001f).Dissolves(Spirit, 0.001f).Dissolves(Oil, 0.001f).Dissolves(Turpentine, 0.001f).SolubilityDoubling(0f),

        // ---- Purifiers.
        S(Sanctity, "Sanctity", SubstanceRole.Reagent, new Color(0.95f, 0.95f, 1f), 900f, "The virtue of blessed water: it destroys venom, sludge and unstable magic, and does nothing else.", "a cool, clean shimmer")
            .Faint(0.3f).With("stability", 0.4f).With("arcane", 0.2f).WithDensity(1f)
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(Venom), 0.5f, "the green film hisses away", (Water, 1f))
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(Sludge), 0.3f, "the murk clears", (Water, 1f))
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(UnstableAether), 0.8f, "the crackling dies with a sigh", (NullEssence, 0.5f)),
        S(Charcoal, "Charcoal", SubstanceRole.Reagent, new Color(0.12f, 0.12f, 0.12f), 900f, "Porous charcoal: it soaks up poisons and residues. It floats - skim it off - and it burns.", "black flakes floating on top")
            .Solid().With("mineral", 0.3f).WithDensity(0.45f).PerItem(2f),
        S(BlastingPowder, "Blasting Powder", SubstanceRole.Byproduct, new Color(0.25f, 0.22f, 0.2f), 900f, "Saltpetre, ember dust and charcoal mixed dry. Keep it far from fire.", "a dark, gritty powder")
            .Solid().With("fire", 1f).With("volatility", 0.9f).WithDensity(1.7f).PerItem(2f),
        S(Soap, "Soft Soap", SubstanceRole.Byproduct, new Color(0.9f, 0.88f, 0.75f), 900f, "Oil cooked with lye: it holds oil and water together and washes anything clean.", "a slippery lather")
            .With("emulsifier", 0.8f).With("alkali", 0.2f).WithDensity(1.02f),

        // ---- Bloodmoss: iron quickens its sap.
        S(SanguineSap, "Sanguine Sap", SubstanceRole.Raw, new Color(0.6f, 0.1f, 0.15f), 900f, "The dark red sap of bloodmoss. Warmth quickens it - and iron far more.", "a dark red, iron-smelling sap")
            .With("vitality", 0.4f).With("organic", 0.4f).With("oxidizable", 0.3f).WithDensity(1.1f),
        S(SanguineEssence, "Sanguine Essence", SubstanceRole.Active, new Color(0.8f, 0.05f, 0.1f), 210f, "Quickened blood-sap: it makes the body mend itself.", "a vivid crimson liquid")
            .With("vitality", 0.7f).With("fire", 0.1f).WithDensity(1.06f).StableUpTo(95f, 0.03f, "the crimson browns", (Sludge, 0.6f)),

        // ---- Thunder agate: stirred hard (and kept cool), its salt charges mana.
        S(StormSalt, "Storm Salt", SubstanceRole.Reagent, new Color(0.55f, 0.6f, 0.85f), 900f, "Ground thunder agate: it crackles when stirred against magic.", "blue grit that crackles")
            .With("mineral", 0.6f).With("arcane", 0.4f).With("volatility", 0.1f).WithDensity(2.4f)
            .Dissolves(Water, 0.2f).Dissolves(Spirit, 0.02f).SolubilityDoubling(30f),
        S(StormEssence, "Storm Essence", SubstanceRole.Active, new Color(0.6f, 0.75f, 1f), 120f, "Mana charged by friction: quick and restless. Heat discharges it.", "a flickering, sparking blue")
            .With("arcane", 0.5f).With("fire", 0.3f).With("volatility", 0.25f).WithDensity(1f)
            .StableUpTo(60f, 0.05f, "the sparks die away", (Water, 1f)),

        // ---- Glacier lichen and starfrost: mana and rime bound in an ice bath.
        S(LichenAcid, "Lichen Acid", SubstanceRole.Reagent, new Color(0.75f, 0.85f, 0.75f), 900f, "The sharp acid of glacier lichen.", "a cold, sour tang")
            .With("acid", 0.6f).With("organic", 0.1f).WithDensity(1.1f).Dissolves(Water, 0.4f).Dissolves(Spirit, 0.5f),
        S(StarfrostEssence, "Starfrost Essence", SubstanceRole.Active, new Color(0.75f, 0.85f, 1f), 130f, "Mana and rime bound together in deep cold: it glitters like a winter sky - and only keeps cold.", "a glittering, star-flecked liquid")
            .With("frost", 0.6f).With("arcane", 0.6f).WithDensity(1f)
            .StableUpTo(12f, 0.02f, "the starlight in it fades", (ManaEssence, 0.5f), (Water, 0.5f)),

        // ---- Nightcap: its spores only wake in the dark.
        S(ShadowSpores, "Shadow Spores", SubstanceRole.Raw, new Color(0.2f, 0.18f, 0.28f), 900f, "Spores of the nightcap mushroom: they only stir at night.", "a dusky, drifting cloud")
            .With("organic", 0.3f).With("arcane", 0.3f).WithDensity(1.05f).Dissolves(Water, 0.3f).Dissolves(Spirit, 1f).Dissolves(Oil, 0.2f),
        S(UmbralEssence, "Umbral Essence", SubstanceRole.Active, new Color(0.15f, 0.1f, 0.25f), 190f, "The calm of the dark: whoever drinks it no longer fears it. Light and sanctity undo it.", "an inky liquid that swallows the light")
            .With("mind", 0.5f).With("arcane", 0.4f).WithDensity(1.02f)
            .StableUpTo(50f, 0.04f, "the darkness thins", (Sludge, 0.5f))
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(Sanctity), 0.6f, "light and shadow cancel with a soft hiss"),

        // ---- Wyrmscale: its ichor wakes only in fire far hotter than water allows.
        S(DraconicIchor, "Draconic Ichor", SubstanceRole.Raw, new Color(0.55f, 0.2f, 0.05f), 900f, "The ichor locked in a wyrm's scales. Only oil draws it out, and only great heat wakes it.", "a smoky, ember-flecked ichor")
            .With("fire", 0.6f).With("vitality", 0.3f).WithDensity(1.3f)
            .Dissolves(Water, 0.05f).Dissolves(Spirit, 0.3f).Dissolves(Oil, 1f).Dissolves(Turpentine, 0.6f),
        S(WyrmbloodEssence, "Wyrmblood Essence", SubstanceRole.Active, new Color(0.75f, 0.25f, 0.05f), 320f, "Ichor woken by ardent essence: the hide of a dragon in a bottle.", "a molten, glowing orange")
            .With("fire", 0.7f).With("stability", 0.6f).WithDensity(1.2f)
            .StableUpTo(230f, 0.03f, "the ichor cracks into black flakes", (FireAsh, 1f)),

        // ---- Ginger: warmth, and a spice for mead.
        S(Gingerol, "Gingerol", SubstanceRole.Raw, new Color(0.9f, 0.75f, 0.35f), 900f, "The heat of ginger root. Hot water turns it into a warming essence.", "a sharp, spicy warmth")
            .With("fire", 0.4f).With("organic", 0.3f).WithDensity(1.05f).Dissolves(Water, 0.3f).Dissolves(Spirit, 1f).Dissolves(Oil, 0.8f),
        S(GingerEssence, "Ginger Essence", SubstanceRole.Active, new Color(0.95f, 0.7f, 0.25f), 230f, "Warming ginger: it drives off the cold and tiredness.", "a golden, spicy liquid")
            .With("fire", 0.5f).With("vitality", 0.2f).WithDensity(1.03f).StableUpTo(130f, 0.03f, "the spice turns harsh", (Sludge, 0.5f)),

        // ---- Rose: an oil to distil, like lavender's.
        S(RoseOil, "Rose Oil", SubstanceRole.Active, new Color(1f, 0.6f, 0.7f), 88f, "Attar of roses: it gladdens the heart. Water barely holds it; it distils over with steam.", "a sweet, heady scent of roses")
            .With("mind", 0.5f).With("vitality", 0.2f).With("organic", 0.2f).With("oxidizable", 0.3f).WithDensity(0.87f)
            .Dissolves(Water, 0.01f).Dissolves(Spirit, 1f).Dissolves(Oil, 1f).Dissolves(Turpentine, 1f).Volatile(2.5f)
            .StableUpTo(150f, 0.03f, "the sweet scent sours", (Sludge, 0.5f)),

        // ---- Sulphur and vitriol: the strongest acid, and it eats everything organic.
        S(Brimstone, "Brimstone", SubstanceRole.Reagent, new Color(0.95f, 0.85f, 0.2f), 900f, "Yellow sulphur. Boiled in water with air beaten in, it slowly becomes oil of vitriol.", "yellow grains that smell of rotten eggs")
            .Solid().With("fire", 0.5f).With("mineral", 0.6f).With("toxicity", 0.1f).WithDensity(2f).PerItem(2f),
        S(Vitriol, "Oil of Vitriol", SubstanceRole.Reagent, new Color(0.9f, 0.9f, 0.7f), 330f, "A fuming, oily acid that chars anything organic it touches.", "an oily liquid that fumes")
            .With("acid", 2f).With("toxicity", 0.45f).With("mineral", 0.3f).WithDensity(1.8f),

        // ---- Wakeberry: an alkaloid that comes out in alkali (the mirror of dreamshade).
        S(Vigorine, "Vigorine", SubstanceRole.Active, new Color(0.55f, 0.3f, 0.15f), 220f, "The waking alkaloid of wakeberries. Alkali draws it out; acid flattens it.", "a bitter, roasted smell")
            .With("fire", 0.2f).With("mind", 0.3f).WithDensity(1.05f).ExtractsBestAtPh(11f, 0.5f)
            .StableUpTo(100f, 0.03f, "the bitterness turns flat", (Sludge, 0.5f)).StableBetweenPh(5f, 14f, 0.04f, "the bitterness turns sour and flat"),

        // ---- Mandrake: it binds to healing essence into something greater.
        S(Mandragorin, "Mandragorin", SubstanceRole.Raw, new Color(0.7f, 0.6f, 0.4f), 900f, "The heavy alkaloid of mandrake root: a little poisonous alone, restorative bound to healing essence. Acid draws it out.", "an earthy, numbing bitterness")
            .With("mind", 0.4f).With("vitality", 0.2f).With("toxicity", 0.15f).WithDensity(1.15f).ExtractsBestAtPh(4f, 0.6f),
        S(RestorativeEssence, "Restorative Essence", SubstanceRole.Active, new Color(1f, 0.85f, 0.45f), 230f, "Healing essence bound to mandragorin: it mends body and mind together.", "a soft, golden, glowing liquid")
            .With("vitality", 0.9f).With("mind", 0.4f).With("stability", 0.3f).WithDensity(1.05f)
            .StableUpTo(80f, 0.03f, "the gold dulls", (Sludge, 0.6f)),

        // ---- Phoenix: born in fire - and it only lives while it is hot.
        S(PhoenixAsh, "Phoenix Ash", SubstanceRole.Reagent, new Color(1f, 0.55f, 0.2f), 900f, "Glowing ash from a phoenix feather.", "glowing orange ash that floats")
            .Solid().With("fire", 1f).With("vitality", 0.5f).WithDensity(0.5f).PerItem(1f),
        S(PhoenixEssence, "Phoenix Essence", SubstanceRole.Active, new Color(1f, 0.45f, 0.1f), 400f, "Ardent essence reborn in phoenix fire. It lives only while hot: bottle it hot, or it gutters out.", "a liquid flame")
            .With("vitality", 1f).With("fire", 0.6f).WithDensity(1f)
            .StableDownTo(90f, 0.03f, "the flame inside it gutters out", (FireAsh, 0.5f), (VitalEssence, 0.5f))
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(RimeEssence), 0.5f, "fire and frost cancel in a burst of steam", (Water, 1f)),

        // ---- Griffin down: it floats on water; spirit draws it out.
        S(AetherDown, "Aether Down", SubstanceRole.Raw, new Color(0.95f, 0.95f, 1f), 900f, "The down of a griffin, lighter than air. Water will not wet it; spirit takes it in.", "fine white down floating on top")
            .With("arcane", 0.4f).With("organic", 0.2f).WithDensity(0.15f).Dissolves(Water, 0.02f).Dissolves(Spirit, 0.6f).Dissolves(Oil, 0.1f),
        S(ZephyrEssence, "Zephyr Essence", SubstanceRole.Active, new Color(0.85f, 0.95f, 1f), 140f, "Lightness itself: the load seems to lift from your shoulders.", "an almost weightless, shimmering liquid")
            .With("arcane", 0.4f).With("mind", 0.2f).WithDensity(0.8f).StableUpTo(70f, 0.04f, "the shimmer settles and is gone", (Water, 1f)),


        // ---- Chains, branches and reversible reactions.
        S(EmberSalt, "Ember Salt", SubstanceRole.Reagent, new Color(0.9f, 0.45f, 0.3f), 900f, "Ember dust bound by brine in the cold: safe to carry. Heat it past 65 °C and the dust comes free again.", "warm orange grains")
            .Solid().With("fire", 0.3f).With("mineral", 0.6f).With("stability", 0.5f).WithDensity(1.6f).PerItem(2f),
        S(SomnineSalt, "Somnine Salt", SubstanceRole.Intermediate, new Color(0.45f, 0.4f, 0.65f), 900f, "Somnine bound by acid: heat no longer destroys it. Neutralise the acid and the somnine comes back.", "a pale lilac tint")
            .With("mind", 0.3f).WithDensity(1.1f),
        S(AlchemicalGold, "Alchemical Gold", SubstanceRole.Byproduct, new Color(1f, 0.82f, 0.3f), 900f, "Lead transmuted in the presence of unstable aether. It settles at the bottom of the vessel.", "glittering golden flecks sinking")
            .Solid().With("mineral", 1f).WithDensity(19f).PerItem(1f),
        S(BerserkerEssence, "Berserker Essence", SubstanceRole.Active, new Color(0.7f, 0.1f, 0.05f), 230f, "Quickened blood fired by ardent essence: fury in a bottle.", "a dark, seething red")
            .With("fire", 0.6f).With("vitality", 0.4f).With("toxicity", 0.03f).WithDensity(1.05f)
            .StableUpTo(110f, 0.03f, "the seething stops and it clots", (Sludge, 0.6f)),
        S(Panacea, "Panacea", SubstanceRole.Active, new Color(1f, 0.95f, 0.8f), 260f, "Restorative essence joined with mana: the cure for nearly everything. Too much heat and it falls apart.", "a pearly, softly shining liquid")
            .With("vitality", 1f).With("mind", 0.6f).With("arcane", 0.4f).With("stability", 0.4f).WithDensity(1.03f)
            .StableUpTo(75f, 0.03f, "the pearl-light dims", (RestorativeEssence, 0.5f), (Water, 0.5f)),

        S(SilverSap, "Silver Sap", SubstanceRole.Raw, new Color(0.85f, 0.88f, 0.92f), 900f, "The bright sap of silverleaf. Alone it does little; blessed water wakes it.", "a bright, silvery sap")
            .With("vitality", 0.3f).With("arcane", 0.3f).With("organic", 0.3f).WithDensity(1.04f),
        S(HolyEssence, "Holy Essence", SubstanceRole.Active, new Color(1f, 0.97f, 0.85f), 220f, "Silver sap blessed by sanctity: it steadies the mind and mends the body. Shadow cannot abide it.", "a warm, golden-white light")
            .With("vitality", 0.5f).With("mind", 0.5f).With("stability", 0.4f).WithDensity(1.02f)
            .StableUpTo(90f, 0.03f, "the light flickers out", (Water, 1f))
            .Interacts(InteractionKind.Clashes, SubstanceSelector.Of(UmbralEssence), 0.8f, "light and shadow annihilate each other"),
        S(KelpGel, "Kelp Gel", SubstanceRole.Raw, new Color(0.35f, 0.5f, 0.3f), 900f, "The slippery gel of deepsea kelp. It only gives up its gift in strong salt water.", "a slippery green gel")
            .With("organic", 0.5f).With("vitality", 0.1f).WithDensity(1.03f),
        S(GillEssence, "Gill Essence", SubstanceRole.Active, new Color(0.3f, 0.65f, 0.6f), 210f, "Kelp gel bound in brine: whoever drinks it breathes long under water.", "a cool sea-green liquid")
            .With("vitality", 0.2f).With("stability", 0.4f).WithDensity(1.04f)
            .StableUpTo(95f, 0.03f, "the sea-green clouds and smells of rot", (Sludge, 0.6f)),
        S(AshPollen, "Ash Pollen", SubstanceRole.Raw, new Color(0.55f, 0.5f, 0.45f), 900f, "Grey pollen of the ashbloom, which flowers on burnt ground. Only near-boiling heat wakes it; old ash hurries it.", "a fine grey pollen")
            .With("fire", 0.3f).With("organic", 0.3f).WithDensity(1.1f),
        S(FirewardEssence, "Fireward Essence", SubstanceRole.Active, new Color(0.8f, 0.5f, 0.35f), 240f, "Ash pollen woken by heat: the body shrugs off heat and flame.", "a smoky, copper-coloured liquid")
            .With("fire", 0.2f).With("stability", 0.6f).WithDensity(1.05f),
    };

    private static AlchemySubstance S(string id, string name, SubstanceRole role, Color color, float boilingPoint, string description, string observation) =>
        new AlchemySubstance
        {
            id = id, displayName = name, role = role, color = color, boilingPoint = boilingPoint,
            description = description, observation = observation,
        };

    private static AlchemySubstance With(this AlchemySubstance s, string property, float value) => s.WithProperty(property, value);

    private static AlchemySubstance Solid(this AlchemySubstance s)
    {
        s.phase = SubstancePhase.Solid;
        return s;
    }

    private static AlchemySubstance Faint(this AlchemySubstance s, float strength)
    {
        s.colorStrength = strength;
        return s;
    }

    private static AlchemySubstance PerItem(this AlchemySubstance s, float units)
    {
        s.unitsPerItem = units;
        return s;
    }

    private static AlchemySubstance SolubilityDoubling(this AlchemySubstance s, float degrees)
    {
        s.solubilityDoublingDegrees = degrees;
        return s;
    }

    private static AlchemySubstance Volatile(this AlchemySubstance s, float multiplier)
    {
        s.evaporationMultiplier = multiplier;
        return s;
    }

    private static AlchemySubstance Known(this AlchemySubstance s)
    {
        s.alwaysKnown = true;
        return s;
    }

    // ------------------------------------------------------------------ reactions
    /// <summary>The built-in reactions, with the chains' second routes (<see cref="AlchemyChains.ExtraReactions"/>).</summary>
    public static List<AlchemyReaction> Reactions()
    {
        List<AlchemyReaction> list = BaseReactions();
        list.AddRange(HerbalReactions());
        return AlchemyChains.WithChains(list);
    }

    private static List<AlchemyReaction> BaseReactions() => new List<AlchemyReaction>
    {
        // Sunpetal line: A (healing) → B (vigour) → degraded.
        R("vital_extraction", "Vital Extraction", "Heat draws the healing essence out of solar sap.")
            .Consumes(SolarSap).Makes(VitalEssence, 0.85f).Makes(Water, 0.15f)
            .Speed(0.03f, 70f, 25f).Between(55f, 130f)
            .ModifiedBy(Quicksilver, 3f, 0.01f).ModifiedBy(LeadDust, 0f, 0.02f)
            .Looks(ReactionVisual.Glow, "the liquid blushes a deep rose and smells of summer"),
        R("vital_kindling", "Kindling", "Kept hot, vital essence slowly cooks into ardent essence.")
            .Consumes(VitalEssence).Makes(ArdentEssence)
            .Speed(0.012f, 90f, 15f).Between(75f, 400f)
            .ModifiedBy(EmberDust, 5f, 0.02f).ModifiedBy(Brine, 0.25f, 0.06f).ModifiedBy(LeadDust, 0f, 0.02f)
            .Heat(4f).Looks(ReactionVisual.Bubbles, "the rose darkens to amber and warmth rises from the vessel"),
        R("ardent_burnout", "Burn-out", "Ardent essence left on a strong fire burns out into ash and sludge.")
            .Consumes(ArdentEssence).Makes(FireAsh, 0.4f).Makes(Sludge, 0.6f)
            .Speed(0.0025f, 90f, 15f).Between(85f, 2000f)
            .ModifiedBy(Brine, 0.2f, 0.06f)
            .Looks(ReactionVisual.Smoke, "grey flakes swirl and the smell turns acrid"),

        // Frost oil redirects what is already there.
        R("serene_binding", "Serene Binding", "Frost oil calms vital essence into serene essence - only below 80 °C.")
            .Consumes(VitalEssence).Consumes(FrostOil).Makes(SereneEssence, 1.6f)
            .Speed(0.5f, 40f, 0f).Between(15f, 80f)
            .Heat(-3f).Looks(ReactionVisual.Glow, "the mixture cools and turns a calm teal"),
        R("tempering", "Tempering", "Frost oil quenches ardent essence into tempered essence - only above 60 °C.")
            .Consumes(ArdentEssence).Consumes(FrostOil).Makes(TemperedEssence, 1.6f)
            .Speed(0.5f, 80f, 0f).Between(60f, 110f)
            .Looks(ReactionVisual.Hiss, "a sharp hiss; the liquid turns steel-blue"),
        R("flash_quench", "Flash Quench", "Frost oil thrown into very hot ardent essence bursts into a fiery vapour.")
            .Consumes(ArdentEssence).Consumes(FrostOil).Makes(EmberSpirit, 1.4f)
            .Speed(1.2f, 120f, 0f).Between(110f, 2000f)
            .Heat(-6f).Looks(ReactionVisual.Hiss, "a violent hiss and shimmering orange fumes"),
        R("frost_settling", "Frost Settling", "In real cold, frost oil settles into rime essence.")
            .Consumes(FrostOil).Makes(RimeEssence)
            .Speed(0.03f, 0f, 0f).Between(-100f, 8f)
            .Looks(ReactionVisual.Glow, "frost flowers spread across the surface"),

        // Moon dew: mana, crystals, and what quicksilver does to magic.
        R("moon_infusion", "Moon Infusion", "Gentle heat wakes the magic in moon dew.")
            .Consumes(MoonDew).Makes(ManaEssence, 0.9f)
            .Speed(0.025f, 50f, 20f).Between(30f, 75f)
            .FasterWhen(new EquipmentTagCondition("copper"), 2f).FasterWhen(new VariableCondition(AlchemyVariables.Night, 0.5f, 1f), 1.5f)
            .ModifiedBy(LeadDust, 0f, 0.02f)
            .Looks(ReactionVisual.Glow, "the liquid starts to hum and glow violet"),
        R("moon_crystallization", "Moon Crystallisation", "Moon dew and brine crystallise in the cold - if left perfectly still.")
            .Consumes(MoonDew).Consumes(Brine, 0.5f).Makes(MoonCrystal, 1f)
            .Speed(0.1f, 5f, 0f).Between(-100f, 12f)
            .Needs(new VariableCondition(AlchemyVariables.Agitation, 0f, 0.12f))
            .FasterWhen(new VariableCondition(AlchemyVariables.Night, 0.5f, 1f), 3f)
            .Looks(ReactionVisual.Glow, "pale crystals grow at the bottom"),
        R("mana_overcharge", "Overcharge", "Quicksilver overcharges hot mana essence into unstable aether.")
            .Consumes(ManaEssence).Makes(UnstableAether)
            .Speed(0.04f, 80f, 15f).Between(70f, 2000f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Quicksilver), 0.004f))
            .Looks(ReactionVisual.Sparks, "the liquid crackles and fizzes with light"),
        R("aether_eruption", "Aether Eruption", "Unstable aether above 95 °C reacts violently.")
            .Consumes(UnstableAether).Makes(FireAsh, 0.3f).Makes(Water, 0.2f)
            .Speed(0.25f, 100f, 10f).Between(95f, 2000f)
            .Heat(40f).Erupts(0.2f, 0.25f).Looks(ReactionVisual.Flash, "a flash! the mixture erupts out of the vessel"),

        // Void salt neutralises any active compound, whatever the preparation was becoming.
        R("nullification", "Nullification", "Void salt swallows the power of any active compound, leaving null essence.")
            .Consumes(SubstanceSelector.OfRole(SubstanceRole.Active, NullEssence)).Consumes(VoidSalt, 0.4f).Makes(NullEssence, 1f)
            .Speed(1.5f, 20f, 0f)
            .Heat(-2f).Looks(ReactionVisual.Smoke, "every colour drains out of the liquid and it goes perfectly still"),

        // Bitterroot: venom with spirit, poison for healing.
        R("venom_drawing", "Venom Drawing", "Warm spirit draws the venom out of bitter resin.")
            .Consumes(BitterResin).Consumes(Spirit, 0.5f, 0.5f).Makes(Venom, 0.8f).Makes(Spirit, 0.5f)
            .Speed(0.08f, 50f, 25f).Between(35f, 78f)
            .Looks(ReactionVisual.Bubbles, "an oily green film gathers"),
        R("tainting", "Tainting", "Bitter resin spoils vital essence.")
            .Consumes(VitalEssence).Consumes(BitterResin, 0.5f).Makes(Sludge, 1.2f)
            .Speed(0.03f, 50f, 30f).Between(20f, 2000f)
            .Looks(ReactionVisual.Bubbles, "the rose turns muddy"),

        // Embercap: a catalyst of kindling, and a flash fire with spirit.
        R("ember_flash", "Ember Flash", "Ember dust ignites alcohol fumes.")
            .Consumes(EmberDust).Consumes(Spirit, 1.5f).Makes(FireAsh, 0.5f)
            .Speed(0.6f, 40f, 0f).Between(30f, 2000f)
            .Heat(300f).Erupts(0.1f, 0.15f).Looks(ReactionVisual.Flash, "the fumes catch fire with a whoomp!"),
        R("ember_infusion", "Ember Infusion", "Ember dust slowly warms water into a weak ardent essence. Saltpetre feeds it.")
            .Consumes(EmberDust).Makes(ArdentEssence, 0.4f).Makes(FireAsh, 0.4f)
            .Speed(0.01f, 80f, 20f).Between(60f, 2000f).ModifiedBy(Niter, 3f, 0.02f)
            .Heat(6f).Looks(ReactionVisual.Sparks, "sparks rise through the liquid"),

        // General decay: rot, scorching, and what spirit and salt prevent.
        R("rot", "Rot", "Raw organic matter left lukewarm rots into sludge.")
            .Consumes(SubstanceSelector.WithProperty("organic", 0.45f, $"{Oil},{Spirit}")).Makes(Sludge, 0.9f)
            .Speed(0.0025f, 35f, 0f).Between(25f, 50f)
            .ModifiedBy(Spirit, 0.1f, 0.15f).ModifiedBy(Brine, 0.2f, 0.06f).ModifiedBy(Sugars, 0.3f, 0.15f)
            .Looks(ReactionVisual.None, "a faint smell of rot"),
        R("scorching", "Scorching", "Organic matter scorches into char above 170 °C.")
            .Consumes(SubstanceSelector.WithProperty("organic", 0.1f, Oil)).Makes(Char, 0.6f)
            .Speed(0.05f, 180f, 20f).Between(170f, 2000f)
            .Heat(5f).Looks(ReactionVisual.Smoke, "black smoke - something is burning at the bottom"),

        // Poor fire control: a roaring fire under an unstirred vessel burns what sits on the bottom, and sets spirit alight.
        R("scorched_bottom", "Scorched Bottom", "On a roaring fire, what sits on the bottom of a vessel nobody stirs catches and burns - whatever the liquid's temperature. A water bath prevents it.")
            .Consumes(SubstanceSelector.WithProperty("organic", 0.3f, $"{Oil},{Spirit},{Turpentine}")).Makes(Char, 0.6f).Makes(Sludge, 0.3f)
            .Speed(0.008f, 80f, 0f).Between(60f, 2000f)
            .Needs(new VariableCondition(AlchemyVariables.Heat, 0.85f, 2f)).Needs(new VariableCondition(AlchemyVariables.Agitation, 0f, 0.15f))
            .Needs(new VariableCondition(AlchemyVariables.Bath, -1f, 0.5f))
            .Looks(ReactionVisual.Smoke, "a burnt smell - something has caught on the bottom"),
        R("spirit_flare", "Spirit Flare", "Spirit fumes over a roaring fire in an open vessel catch light.")
            .Consumes(Spirit).Makes(Water, 0.2f)
            .Speed(0.15f, 70f, 0f).Between(65f, 2000f)
            .Needs(new VariableCondition(AlchemyVariables.Heat, 0.8f, 2f)).Needs(new EquipmentTagCondition("open"))
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Spirit), 0.06f))
            .Heat(60f).Erupts(0.6f, 0.1f).Looks(ReactionVisual.Flash, "blue flames leap from the vessel!"),

        // Stirring too hard beats air in: what oxidises browns and spoils (iron hurries it).
        R("oxidation", "Oxidation", "Air beaten into the liquid browns what oxidises - faster with iron filings, and in an iron vessel.")
            .Consumes(SubstanceSelector.WithProperty("oxidizable", 0.3f)).Makes(Sludge, 0.8f)
            .Speed(0.0015f, 40f, 25f).Between(0f, 2000f)
            .Needs(new VariableCondition(AlchemyVariables.Aeration, 0.3f, 2f))
            .ModifiedBy(Iron, 4f, 0.02f).FasterWhen(new EquipmentTagCondition("iron"), 1.5f)
            .Looks(ReactionVisual.None, "the colour dulls and browns at the surface"),

        // Acids and alkalis cancel out.
        R("neutralisation", "Neutralisation", "An acid and an alkali cancel each other out into salt and water, fizzing.")
            .Consumes(SubstanceSelector.WithProperty("acid", 0.5f)).Consumes(SubstanceSelector.WithProperty("alkali", 0.5f)).Makes(Brine, 0.7f).Makes(Water, 0.9f)
            .Speed(0.8f, 20f, 0f).Heat(3f).Looks(ReactionVisual.Bubbles, "it fizzes and foams"),

        // Fermentation: yeast turns sugars into spirit in the warm, until the spirit kills it.
        R("fermentation", "Fermentation", "Live yeast turns sugars into spirit (and a fruity aroma) in the warm - slowly, over minutes.")
            .Consumes(Sugars).Makes(Spirit, 0.5f).Makes(Water, 0.42f).Makes(Esters, 0.04f)
            .Speed(0.0016f, 30f, 12f).Between(12f, 40f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Yeast), 0.002f)).ModifiedBy(Yeast, 4f, 0.01f).ModifiedBy(Spirit, 0.3f, 0.12f)
            .Looks(ReactionVisual.Bubbles, "it froths and smells of bread"),
        R("yeast_poisoning", "Yeast Poisoning", "Strong spirit kills the yeast that made it.")
            .Consumes(Yeast).Speed(0.05f, 20f, 0f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Spirit), 0.12f))
            .Looks(ReactionVisual.None, "the froth dies away"),

        // Iron and tannin: ink.
        R("inking", "Inking", "Tannin blackens into ink on iron - faster when air is stirred in.")
            .Consumes(Tannin).Makes(IronGallInk, 0.9f)
            .Speed(0.02f, 20f, 25f).Between(5f, 100f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Iron), 0.3f, 1000f, false))
            .FasterWhen(new VariableCondition(AlchemyVariables.Aeration, 0.15f, 2f), 3f)
            .Looks(ReactionVisual.Glow, "the liquid darkens to an inky black"),

        // Pepperfire and vital essence: they live in different layers - stir them into one, or use spirit.
        R("salamander_binding", "Salamander Binding", "Pepperfire oil binds to vital essence - where they meet: an oil and a watery essence only meet stirred together, emulsified, or in spirit.")
            .Consumes(PepperOil).Consumes(VitalEssence).Makes(SalamanderEssence, 1.7f)
            .Speed(0.25f, 60f, 0f).Between(40f, 95f)
            .Heat(2f).Looks(ReactionVisual.Glow, "a scarlet glow spreads through the liquid"),

        // Oil and lye: soap - slow until the first soap holds them together.
        R("saponification", "Saponification", "Hot lye cooks oil into soap. Lye and oil sit in different layers until stirred - and the soap, once made, holds them together.")
            .Consumes(Lye).Consumes(Oil, 2f).Makes(Soap, 2.6f).Makes(Water, 0.4f)
            .Speed(0.05f, 80f, 20f).Between(50f, 130f)
            .Heat(2f).Looks(ReactionVisual.Bubbles, "a thick lather rises"),

        // Alkaloids and bark: gentle heat, the right pH.
        R("antitoxin_tempering", "Antitoxin Tempering", "Gentle heat tempers serpentine into antitoxin; a trace of venom teaches it what to fight.")
            .Consumes(Serpentine).Makes(Antitoxin, 0.9f)
            .Speed(0.02f, 60f, 20f).Between(45f, 85f).ModifiedBy(Venom, 3f, 0.005f)
            .Looks(ReactionVisual.Glow, "a faint green glow"),
        R("willow_hydrolysis", "Willow Hydrolysis", "Warm acid turns salicin into willow essence (faster the more acid).")
            .Consumes(Salicin).Makes(WillowEssence, 0.9f)
            .Speed(0.02f, 70f, 20f).Between(50f, 100f)
            .Needs(new VariableCondition(AlchemyVariables.Ph, -1f, 5f)).FasterWhen(new VariableCondition(AlchemyVariables.Ph, -1f, 3.5f), 2f)
            .Looks(ReactionVisual.Bubbles, "the bitterness mellows"),

        // Charcoal soaks up poisons and sludge.
        R("adsorption", "Adsorption", "Charcoal soaks up poisons, binding them into its pores (faster stirred).")
            .Consumes(SubstanceSelector.WithProperty("toxicity", 0.4f, $"{Lye},{Quicksilver}")).Makes(BoundToxin, 1f)
            .Speed(0.03f, 40f, 20f).Between(-10f, 200f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Charcoal), 0.5f, 1000f, false)).ModifiedBy(Charcoal, 3f, 0.05f)
            .FasterWhen(new VariableCondition(AlchemyVariables.Agitation, 0.3f, 2f), 2f)
            .Looks(ReactionVisual.None, "the liquid clears around the black flakes"),
        R("charcoal_clearing", "Charcoal Clearing", "Charcoal soaks up sludge too.")
            .Consumes(Sludge).Makes(BoundToxin, 0.8f)
            .Speed(0.02f, 40f, 20f).Between(-10f, 200f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Charcoal), 0.5f, 1000f, false)).ModifiedBy(Charcoal, 3f, 0.05f)
            .Looks(ReactionVisual.None, "the murk clears"),

        // Blasting powder: mixed dry, and it goes off when hot.
        R("powder_mixing", "Powder Mixing", "Saltpetre crystals, ember dust and charcoal stirred together dry become blasting powder.")
            .Consumes(NiterCrystal).Consumes(EmberDust, 0.5f).Consumes(Charcoal, 0.5f).Makes(BlastingPowder, 2f)
            .Speed(0.6f, 20f, 0f).Between(10f, 150f)
            .Needs(new VariableCondition(AlchemyVariables.Liquid, -1f, 0.5f)).Needs(new VariableCondition(AlchemyVariables.Agitation, 0.2f, 2f))
            .Looks(ReactionVisual.None, "the powders darken as they mix"),
        R("detonation", "Detonation", "Blasting powder goes off above 190 °C.")
            .Consumes(BlastingPowder).Makes(FireAsh, 0.5f)
            .Speed(0.5f, 200f, 0f).Between(190f, 2000f)
            .Heat(400f).Erupts(0.05f, 0.6f).Looks(ReactionVisual.Flash, "BOOM! the powder goes off"),

        // Bloodmoss: warmth quickens the sap, a pinch of iron far more.
        R("blood_quickening", "Blood Quickening", "Warmth quickens sanguine sap into an essence that makes the body mend itself; iron hurries it.")
            .Consumes(SanguineSap).Makes(SanguineEssence, 0.9f)
            .Speed(0.012f, 60f, 20f).Between(35f, 90f).ModifiedBy(Iron, 4f, 0.005f)
            .Looks(ReactionVisual.Glow, "the red deepens to a vivid crimson"),

        // Thunder agate: friction charges mana - stir hard, and keep it cool.
        R("storm_charging", "Storm Charging", "Storm salt stirred hard against mana essence charges it with lightning. Only friction does it; heat discharges it.")
            .Consumes(StormSalt).Consumes(ManaEssence).Makes(StormEssence, 1.8f)
            .Speed(0.25f, 30f, 0f).Between(-10f, 60f)
            .Needs(new VariableCondition(AlchemyVariables.Agitation, 0.5f, 2f))
            .Looks(ReactionVisual.Sparks, "sparks crackle round the spoon"),

        // Starfrost: mana and rime bound in deep cold.
        R("starfrost_binding", "Starfrost Binding", "In the deep cold of an ice bath, rime essence and mana essence bind into starfrost.")
            .Consumes(RimeEssence).Consumes(ManaEssence).Makes(StarfrostEssence, 1.8f)
            .Speed(0.25f, 0f, 0f).Between(-30f, 6f)
            .Needs(new VariableCondition(AlchemyVariables.Bath, 1.5f, 3f))
            .Looks(ReactionVisual.Glow, "tiny stars glitter in the liquid"),

        // Nightcap: the spores only wake at night.
        R("shadow_steeping", "Shadow Steeping", "Shadow spores steeped at night, gently warm, give up the calm of the dark.")
            .Consumes(ShadowSpores).Makes(UmbralEssence, 0.9f)
            .Speed(0.02f, 30f, 20f).Between(10f, 50f)
            .Needs(new VariableCondition(AlchemyVariables.Night, 0.5f, 2f))
            .Looks(ReactionVisual.None, "the liquid darkens until it swallows the light"),

        // Wyrmscale: ichor woken by ardent essence, far hotter than water allows.
        R("ichor_awakening", "Ichor Awakening", "Above 120 °C - in oil - draconic ichor drinks ardent essence and wakes.")
            .Consumes(DraconicIchor).Consumes(ArdentEssence).Makes(WyrmbloodEssence, 1.6f)
            .Speed(0.6f, 140f, 25f).Between(120f, 240f)
            .Heat(3f).Looks(ReactionVisual.Glow, "the oil glows like molten metal"),

        // Ginger: hot water warms the root's heat into an essence.
        R("ginger_warming", "Ginger Warming", "Hot, gingerol mellows into a warming essence.")
            .Consumes(Gingerol).Makes(GingerEssence, 0.9f)
            .Speed(0.03f, 80f, 20f).Between(60f, 110f)
            .Looks(ReactionVisual.Bubbles, "a spicy steam rises"),

        // Sulphur and vitriol.
        R("vitriol_making", "Vitriol Making", "Brimstone boiled in water slowly becomes oil of vitriol - far faster with air beaten in.")
            .Consumes(Brimstone).Consumes(Water, 0.5f, 0f).Makes(Vitriol, 1.2f)
            .Speed(0.01f, 100f, 20f).Between(90f, 300f)
            .FasterWhen(new VariableCondition(AlchemyVariables.Aeration, 0.2f, 2f), 4f)
            .Looks(ReactionVisual.Smoke, "choking yellow fumes rise"),
        R("vitriol_charring", "Vitriol Charring", "Oil of vitriol chars anything organic it touches.")
            .Consumes(SubstanceSelector.WithProperty("organic", 0.3f, $"{Oil},{Spirit},{Turpentine}")).Consumes(Vitriol, 0.3f)
            .Makes(Char, 0.7f).Makes(Water, 0.3f)
            .Speed(0.4f, 20f, 25f).Between(0f, 400f)
            .Heat(4f).Looks(ReactionVisual.Smoke, "it blackens and smokes where the acid touches"),

        // Mandrake and healing essence: restoration.
        R("restoration", "Restoration", "Mandragorin binds to vital essence into a restorative essence, gently warm; moon dew guides it.")
            .Consumes(Mandragorin).Consumes(VitalEssence).Makes(RestorativeEssence, 2.2f)
            .Speed(0.15f, 60f, 20f).Between(40f, 78f).ModifiedBy(MoonDew, 3f, 0.01f)
            .Looks(ReactionVisual.Glow, "a soft golden light fills the vessel"),

        // Phoenix: reborn in fire.
        R("rebirth", "Rebirth", "In fire above 140 °C, phoenix ash rekindles ardent essence into phoenix essence.")
            .Consumes(PhoenixAsh, 1f, 0.5f).Consumes(ArdentEssence, 1.5f).Makes(PhoenixEssence, 3f)
            .Speed(1.5f, 160f, 30f).Between(140f, 260f)
            .Heat(6f).Erupts(0.4f, 0.2f).Looks(ReactionVisual.Flash, "a bird of flame rises from the vessel and sinks back in"),

        // Griffin down: spirit draws its lightness out.
        R("lightening", "Lightening", "Aether down taken in by spirit, gently warm, gives up its lightness.")
            .Consumes(AetherDown).Makes(ZephyrEssence, 0.9f)
            .Speed(0.03f, 40f, 20f).Between(20f, 65f)
            .Looks(ReactionVisual.Glow, "the liquid seems to float in the vessel"),

        // ---- Reversible: temperature decides the way. Cold brine binds ember dust; past 65 °C it lets it go.
        R("ember_binding", "Ember Binding", "In the cold, brine binds ember dust into a stable salt - and lets it go again when heated past 65 °C.")
            .Consumes(EmberDust).Consumes(Brine).Makes(EmberSalt, 2f)
            .Speed(0.15f, 20f, 0f).Between(-20f, 55f)
            .Looks(ReactionVisual.None, "the red specks dull and gather into warm orange grains")
            .Reversible(0.06f, 80f, 15f, 65f, 400f, "orange grains crackle and release glowing specks"),

        // ---- Reversible: the pH decides the way. Acid binds somnine into a salt heat cannot hurt; neutralising frees it.
        R("somnine_salting", "Somnine Salting", "Acid binds somnine into a salt that heat cannot destroy; neutralise the acid and the somnine comes back.")
            .Consumes(Somnine).Makes(SomnineSalt, 1f)
            .Speed(0.08f, 20f, 0f).Between(-20f, 200f)
            .Needs(new VariableCondition(AlchemyVariables.Ph, -1f, 5f))
            .Looks(ReactionVisual.None, "the violet pales to lilac")
            .Reversible(0.1f, 20f, 0f, -20f, 200f, "the lilac deepens back to violet")
            .ReverseNeeds(new VariableCondition(AlchemyVariables.Ph, 6f, 15f)),

        // ---- Reversible: concentration decides the way. Much water pushes the esters back; another way to the esters.
        R("esterification", "Esterification", "Warm spirit and acetic acid slowly make fruity esters (and water); much water turns them back.")
            .Consumes(Spirit, 1f, 1f).Consumes(AceticAcid).Makes(Esters, 1f).Makes(Water, 0.3f)
            .Speed(0.03f, 60f, 25f).Between(20f, 120f)
            .Looks(ReactionVisual.None, "a fruity smell rises")
            .Reversible(0.003f, 60f, 25f, 20f, 120f, "the fruity smell fades"),

        // ---- An alternative pathway (moonlit blood is a second way to healing essence) - competing with moon infusion for the dew.
        R("moonlit_blood", "Moonlit Blood", "Moon dew calms sanguine essence into healing essence - a second way to it. The dew is also what moon infusion wants.")
            .Consumes(SanguineEssence).Consumes(MoonDew).Makes(VitalEssence, 1.5f)
            .Speed(0.2f, 50f, 20f).Between(30f, 70f)
            .FasterWhen(new VariableCondition(AlchemyVariables.Night, 0.5f, 2f), 2f)
            .Looks(ReactionVisual.Glow, "the crimson softens to rose"),

        // ---- Branching: the same sanguine essence turns to fury in the heat with ardent essence (with sludge as by-product).
        R("fury_binding", "Fury Binding", "Hot, sanguine essence takes ardent essence into a seething fury - and leaves a little sludge.")
            .Consumes(SanguineEssence).Consumes(ArdentEssence).Makes(BerserkerEssence, 1.6f).Makes(Sludge, 0.2f)
            .Speed(0.15f, 85f, 20f).Between(70f, 110f)
            .Heat(2f).Looks(ReactionVisual.Bubbles, "the red darkens and seethes"),

        // ---- A dependent reaction: possible only while unstable aether - which only overcharging mana makes - is in the vessel.
        R("transmutation", "Transmutation", "While unstable aether crackles in the vessel, quicksilver turns lead into gold. Without the aether nothing happens.")
            .Consumes(LeadDust).Consumes(Quicksilver, 0.5f).Makes(AlchemicalGold, 1f)
            .Speed(0.4f, 80f, 0f).Between(60f, 200f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(UnstableAether), 0.002f))
            .Looks(ReactionVisual.Sparks, "golden sparks drift down through the liquid"),

        // ---- The end of a chain: sap → healing essence → restorative essence (mandrake), and moon dew → mana; joined, the panacea.
        R("panacea_union", "Panacea", "Restorative essence and mana essence joined gently warm: the panacea. Copper helps.")
            .Consumes(RestorativeEssence).Consumes(ManaEssence).Makes(Panacea, 1.8f)
            .Speed(0.15f, 55f, 20f).Between(40f, 72f)
            .FasterWhen(new EquipmentTagCondition("copper"), 2f)
            .Looks(ReactionVisual.Glow, "a pearly light spreads from the centre"),

        // Silverleaf: a dependent reaction - blessed water must be in the vessel.
        R("blessing", "Blessing", "Sanctity wakes silver sap into holy essence (gently warm).")
            .Consumes(SilverSap).Consumes(Sanctity, 0.4f).Makes(HolyEssence, 1.2f)
            .Speed(0.2f, 40f, 20f).Between(15f, 70f)
            .Looks(ReactionVisual.Glow, "a warm golden-white light rises"),
        // Deepsea kelp: only in strong salt water.
        R("gill_binding", "Gill Binding", "Kelp gel binds into gill essence - but only in strong brine (seawater, or salted water).")
            .Consumes(KelpGel).Makes(GillEssence, 0.9f)
            .Speed(0.03f, 60f, 20f).Between(30f, 90f)
            .Needs(new SubstanceCondition(SubstanceSelector.Of(Brine), 0.08f))
            .Looks(ReactionVisual.None, "the gel dissolves into a cool sea-green"),
        // Ashbloom: near-boiling heat, old ash as catalyst.
        R("fireward_waking", "Fireward Waking", "Near-boiling heat wakes ash pollen into fireward essence; fire ash hurries it.")
            .Consumes(AshPollen).Makes(FirewardEssence, 0.9f)
            .Speed(0.0025f, 95f, 15f).Between(85f, 130f).ModifiedBy(FireAsh, 6f, 0.01f)
            .Looks(ReactionVisual.Smoke, "a smoky copper colour spreads"),
    };

    private static AlchemyReaction R(string id, string name, string description) =>
        new AlchemyReaction { id = id, displayName = name, description = description };

    // ------------------------------------------------------------------ outcomes
    public static List<AlchemyOutcome> Outcomes()
    {
        var list = new List<AlchemyOutcome>();

        // Variants first: same main compound, changed by what else is in it.
        list.Add(Potion("tainted_healing", "Tainted Healing Draught", "Heals, but the bitterness lingers in the mind.", 70, VitalEssence, 0.3f, 15f)
            .Requires(new DominantCondition(VitalEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(VitalEssence), 0.03f))
            .Requires(new PropertyCondition("toxicity", 0.04f, 1000f))
            .Fx(ConsumableEffectType.Hp, 25f).Fx(ConsumableEffectType.Sanity, -10f));
        list.Add(Potion("healing_draught", "Healing Draught", "Restores health at once and keeps healing for a while.", 60, VitalEssence, 0.35f, 25f)
            .Requires(new DominantCondition(VitalEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(VitalEssence), 0.03f))
            .Fx(ConsumableEffectType.Hp, 35f).Fx(ConsumableEffectType.HpRegeneration, 2f, 12f, 1f));
        list.Add(Potion("vigor_draught", "Draught of Vigour", "Restores stamina and warms the body.", 60, ArdentEssence, 0.3f, 25f)
            .Requires(new DominantCondition(ArdentEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(ArdentEssence), 0.03f))
            .Fx(ConsumableEffectType.Stamina, 60f).Fx(ConsumableEffectType.StaminaRegeneration, 3f, 20f, 1f).Fx(ConsumableEffectType.BodyHeat, 8f));
        list.Add(Potion("calming_tonic", "Calming Tonic", "Steadies the mind and slowly restores mana.", 60, SereneEssence, 0.3f, 30f)
            .Requires(new DominantCondition(SereneEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(SereneEssence), 0.03f))
            .Fx(ConsumableEffectType.Sanity, 25f).Fx(ConsumableEffectType.ManaRegeneration, 2f, 15f, 1f));
        list.Add(Potion("fortitude_elixir", "Elixir of Fortitude", "Hardens the body: less damage taken for a minute.", 60, TemperedEssence, 0.3f, 45f)
            .Requires(new DominantCondition(TemperedEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(TemperedEssence), 0.03f))
            .Fx(ConsumableEffectType.HpDamageFactor, -25f, 60f, 1f));
        list.Add(Potion("rime_draught", "Rime Draught", "Cools the body: a blessing in a heat wave.", 60, RimeEssence, 0.3f, 25f)
            .Requires(new DominantCondition(RimeEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(RimeEssence), 0.03f))
            .Fx(ConsumableEffectType.BodyHeat, -20f).Fx(ConsumableEffectType.BodyHeat, -10f, 20f, 1f));
        list.Add(Potion("mana_potion", "Mana Potion", "Restores mana.", 60, ManaEssence, 0.3f, 25f)
            .Requires(new DominantCondition(ManaEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(ManaEssence), 0.03f))
            .Fx(ConsumableEffectType.Mana, 50f));
        list.Add(Potion("quickstep_elixir", "Quickstep Elixir", "Light feet: faster movement for half a minute.", 60, EmberSpirit, 0.25f, 40f)
            .Requires(new DominantCondition(EmberSpirit)).Requires(new SubstanceCondition(SubstanceSelector.Of(EmberSpirit), 0.03f))
            .Fx(ConsumableEffectType.SpeedFactor, 20f, 30f, 1f));
        list.Add(Potion("stillwater_draught", "Stillwater Draught", "The still mind needs little air: restores breath, slows its loss.", 60, NullEssence, 0.3f, 30f)
            .Requires(new DominantCondition(NullEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(NullEssence), 0.03f))
            .Fx(ConsumableEffectType.Oxygen, 40f).Fx(ConsumableEffectType.OxygenDamageFactor, -30f, 45f, 1f));

        list.Add(Potion("murky_healing", "Murky Healing Draught", "Heals, but the dirt in it turns the stomach: filter your brews.", 68, VitalEssence, 0.35f, 12f)
            .Requires(new DominantCondition(VitalEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(VitalEssence), 0.03f))
            .Requires(new PropertyCondition("contaminant", 0.015f, 1000f))
            .Fx(ConsumableEffectType.Hp, 25f).Fx(ConsumableEffectType.Stamina, -15f));
        list.Add(Potion("healing_salve", "Healing Salve", "Vital essence set in wax and oil: rubbed in, it mends slowly and surely.", 66, VitalEssence, 0.3f, 35f)
            .Requires(new DominantCondition(VitalEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(VitalEssence), 0.03f))
            .Requires(new SubstanceCondition(SubstanceSelector.Of(Oil), 0.3f)).Requires(new SubstanceCondition(SubstanceSelector.Of(Beeswax), 0.02f))
            .Fx(ConsumableEffectType.Hp, 20f).Fx(ConsumableEffectType.HpRegeneration, 3f, 40f, 1f));
        list.Add(Potion("salamander_draught", "Salamander Draught", "Warms you to the bone and mends you from within.", 60, SalamanderEssence, 0.25f, 40f)
            .Requires(new DominantCondition(SalamanderEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(SalamanderEssence), 0.03f))
            .Fx(ConsumableEffectType.BodyHeat, 15f).Fx(ConsumableEffectType.HpRegeneration, 1.5f, 30f, 1f).Fx(ConsumableEffectType.StaminaRegeneration, 2f, 20f, 1f));
        list.Add(Potion("antidote", "Antidote", "Fights poison in the blood: heals a little and hardens you against poison for a minute.", 60, Antitoxin, 0.08f, 45f)
            .Requires(new DominantCondition(Antitoxin)).Requires(new SubstanceCondition(SubstanceSelector.Of(Antitoxin), 0.03f))
            .Fx(ConsumableEffectType.Hp, 15f).Fx(ConsumableEffectType.HpDamageFactor, -20f, 60f, 1f).Fx(ConsumableEffectType.HpRegeneration, 1f, 20f, 1f));
        list.Add(Potion("sleep_draught", "Dreamless Sleep Draught", "Deep, restful sleep - and a heavy head for a while.", 60, Somnine, 0.08f, 35f)
            .Requires(new DominantCondition(Somnine)).Requires(new SubstanceCondition(SubstanceSelector.Of(Somnine), 0.02f))
            .Fx(ConsumableEffectType.Sleep, 35f).Fx(ConsumableEffectType.Sanity, 15f).Fx(ConsumableEffectType.SpeedFactor, -15f, 30f, 1f));
        list.Add(Potion("willowbark_draught", "Willowbark Draught", "Eases pain: less damage taken, and a calmer mind, for a while.", 60, WillowEssence, 0.08f, 30f)
            .Requires(new DominantCondition(WillowEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(WillowEssence), 0.03f))
            .Fx(ConsumableEffectType.Hp, 10f).Fx(ConsumableEffectType.HpDamageFactor, -15f, 90f, 1f).Fx(ConsumableEffectType.SanityRegeneration, 1f, 30f, 1f));
        list.Add(Potion("lavender_tincture", "Lavender Tincture", "Calms the mind and brings easy sleep.", 60, LavenderOil, 0.15f, 30f)
            .Requires(new DominantCondition(LavenderOil)).Requires(new SubstanceCondition(SubstanceSelector.Of(LavenderOil), 0.015f))
            .Fx(ConsumableEffectType.Sanity, 20f).Fx(ConsumableEffectType.Sleep, 10f).Fx(ConsumableEffectType.SanityRegeneration, 1f, 30f, 1f));
        AlchemyOutcome mead = Potion("honey_mead", "Honey Mead", "Fermented honey: refreshing, warming, a little heady.", 52, Spirit, 0.12f, 12f)
            .Requires(new SubstanceCondition(SubstanceSelector.Of(Esters), 0.001f)).Requires(new SubstanceCondition(SubstanceSelector.Of(Spirit), 0.03f, 0.3f))
            .Requires(new RoleShareCondition(SubstanceRole.Active, 0f, 0.1f))
            .Fx(ConsumableEffectType.Drink, 15f).Fx(ConsumableEffectType.Stamina, 15f).Fx(ConsumableEffectType.Sanity, 8f).Fx(ConsumableEffectType.SpeedFactor, -5f, 30f, 1f);
        list.Add(mead);
        list.Add(Potion("honey_brandy", "Honey Brandy", "Distilled mead: fiery and fragrant. Warms, restores, goes to the head.", 53, Spirit, 0.6f, 30f)
            .Requires(new SubstanceCondition(SubstanceSelector.Of(Esters), 0.004f)).Requires(new SubstanceCondition(SubstanceSelector.Of(Spirit), 0.3f))
            .Requires(new RoleShareCondition(SubstanceRole.Active, 0f, 0.1f))
            .Fx(ConsumableEffectType.Stamina, 25f).Fx(ConsumableEffectType.BodyHeat, 10f).Fx(ConsumableEffectType.Sanity, 5f).Fx(ConsumableEffectType.SpeedFactor, -10f, 30f, 1f));

        // ---- Pack 4.
        list.Add(Potion("bloodmoss_tonic", "Bloodmoss Tonic", "The body mends itself for a full minute.", 60, SanguineEssence, 0.25f, 35f)
            .Requires(new DominantCondition(SanguineEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(SanguineEssence), 0.03f))
            .Fx(ConsumableEffectType.Hp, 10f).Fx(ConsumableEffectType.HpRegeneration, 3f, 60f, 1f).Fx(ConsumableEffectType.Stamina, 10f));
        list.Add(Potion("stormcall_elixir", "Stormcall Elixir", "Lightning in the legs: much faster for twenty seconds, and breathless after.", 61, StormEssence, 0.2f, 55f)
            .Requires(new DominantCondition(StormEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(StormEssence), 0.03f))
            .Fx(ConsumableEffectType.SpeedFactor, 30f, 20f, 1f).Fx(ConsumableEffectType.Stamina, 40f).Fx(ConsumableEffectType.Oxygen, -10f));
        list.Add(Potion("starfrost_draught", "Starfrost Draught", "Mana like a winter night: restores it, keeps restoring it, and cools the body.", 60, StarfrostEssence, 0.25f, 60f)
            .Requires(new DominantCondition(StarfrostEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(StarfrostEssence), 0.03f))
            .Fx(ConsumableEffectType.Mana, 40f).Fx(ConsumableEffectType.ManaRegeneration, 3f, 30f, 1f).Fx(ConsumableEffectType.BodyHeat, -15f));
        list.Add(Potion("shadow_draught", "Draught of Shadows", "The dark no longer frightens you: the mind holds steady for a while.", 60, UmbralEssence, 0.15f, 40f)
            .Requires(new DominantCondition(UmbralEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(UmbralEssence), 0.03f))
            .Fx(ConsumableEffectType.Sanity, 20f).Fx(ConsumableEffectType.SanityDamageFactor, -40f, 90f, 1f));
        list.Add(Potion("dragonheart_elixir", "Dragonheart Elixir", "A dragon's hide and heart: far less damage taken, and warm in any cold.", 62, WyrmbloodEssence, 0.25f, 90f)
            .Requires(new DominantCondition(WyrmbloodEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(WyrmbloodEssence), 0.03f))
            .Fx(ConsumableEffectType.HpDamageFactor, -40f, 90f, 1f).Fx(ConsumableEffectType.BodyHeat, 20f).Fx(ConsumableEffectType.MaxHp, 20f, 90f, 1f));
        list.Add(Potion("spiced_mead", "Spiced Mead", "Mead with ginger: it warms you through and chases off tiredness.", 62, Spirit, 0.12f, 20f)
            .Requires(new SubstanceCondition(SubstanceSelector.Of(Esters), 0.001f)).Requires(new SubstanceCondition(SubstanceSelector.Of(Spirit), 0.03f, 0.3f))
            .Requires(new SubstanceCondition(SubstanceSelector.Of(GingerEssence), 0.008f))
            .Fx(ConsumableEffectType.Drink, 15f).Fx(ConsumableEffectType.Stamina, 30f).Fx(ConsumableEffectType.BodyHeat, 12f).Fx(ConsumableEffectType.Sanity, 8f));
        list.Add(Potion("ginger_tonic", "Ginger Tonic", "Warms the body and restores stamina.", 60, GingerEssence, 0.2f, 15f)
            .Requires(new DominantCondition(GingerEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(GingerEssence), 0.03f))
            .Fx(ConsumableEffectType.Stamina, 30f).Fx(ConsumableEffectType.BodyHeat, 12f).Fx(ConsumableEffectType.StaminaRegeneration, 1.5f, 30f, 1f));
        list.Add(Potion("rosewater", "Rosewater", "Gladdens the heart and soothes the mind.", 60, RoseOil, 0.15f, 30f)
            .Requires(new DominantCondition(RoseOil)).Requires(new SubstanceCondition(SubstanceSelector.Of(RoseOil), 0.015f))
            .Fx(ConsumableEffectType.Sanity, 30f).Fx(ConsumableEffectType.SanityRegeneration, 1.5f, 30f, 1f));
        list.Add(Potion("wakeful_brew", "Wakeful Brew", "Bitter and bracing: tiredness falls away, and the hands shake a little.", 60, Vigorine, 0.08f, 20f)
            .Requires(new DominantCondition(Vigorine)).Requires(new SubstanceCondition(SubstanceSelector.Of(Vigorine), 0.02f))
            .Fx(ConsumableEffectType.Sleep, 25f).Fx(ConsumableEffectType.StaminaRegeneration, 2f, 60f, 1f).Fx(ConsumableEffectType.Sanity, -5f));
        list.Add(Potion("restoration_elixir", "Restoration Elixir", "Mends body and mind together: a great healing, and a clear head.", 64, RestorativeEssence, 0.3f, 80f)
            .Requires(new DominantCondition(RestorativeEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(RestorativeEssence), 0.03f))
            .Fx(ConsumableEffectType.Hp, 60f).Fx(ConsumableEffectType.Sanity, 20f).Fx(ConsumableEffectType.HpRegeneration, 2f, 30f, 1f));
        list.Add(Potion("phoenix_draught", "Phoenix Draught", "Fire that heals: a huge healing that keeps burning on. Bottle it hot.", 65, PhoenixEssence, 0.25f, 150f)
            .Requires(new DominantCondition(PhoenixEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(PhoenixEssence), 0.03f))
            .Fx(ConsumableEffectType.Hp, 80f).Fx(ConsumableEffectType.HpRegeneration, 4f, 30f, 1f).Fx(ConsumableEffectType.BodyHeat, 15f));
        list.Add(Potion("featherfall_draught", "Featherfall Draught", "Your load feels light: carry more and move lighter for two minutes.", 60, ZephyrEssence, 0.2f, 45f)
            .Requires(new DominantCondition(ZephyrEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(ZephyrEssence), 0.03f))
            .Fx(ConsumableEffectType.MaxWeight, 20f, 120f, 1f).Fx(ConsumableEffectType.SpeedFactor, 8f, 120f, 1f));
        list.Add(Potion("berserker_draught", "Berserker Draught", "Fury: faster and tireless for a while - but every blow lands harder on you.", 60, BerserkerEssence, 0.25f, 45f)
            .Requires(new DominantCondition(BerserkerEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(BerserkerEssence), 0.03f))
            .Fx(ConsumableEffectType.SpeedFactor, 15f, 45f, 1f).Fx(ConsumableEffectType.StaminaRegeneration, 4f, 45f, 1f)
            .Fx(ConsumableEffectType.HpDamageFactor, 20f, 45f, 1f).Fx(ConsumableEffectType.Sanity, -10f));
        list.Add(Potion("panacea", "Panacea", "The cure for nearly everything: body and mind restored, and stronger for a while.", 67, Panacea, 0.25f, 250f)
            .Requires(new DominantCondition(Panacea)).Requires(new SubstanceCondition(SubstanceSelector.Of(Panacea), 0.03f))
            .Fx(ConsumableEffectType.Hp, 100f).Fx(ConsumableEffectType.Sanity, 30f).Fx(ConsumableEffectType.HpRegeneration, 3f, 60f, 1f)
            .Fx(ConsumableEffectType.MaxHp, 20f, 120f, 1f));
        list.Add(Potion("blessed_elixir", "Blessed Elixir", "Mends the body and shields the mind from dread for two minutes.", 60, HolyEssence, 0.2f, 60f)
            .Requires(new DominantCondition(HolyEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(HolyEssence), 0.03f))
            .Fx(ConsumableEffectType.Hp, 30f).Fx(ConsumableEffectType.Sanity, 25f).Fx(ConsumableEffectType.SanityDamageFactor, -25f, 120f, 1f));
        list.Add(Potion("gillweed_draught", "Gillweed Draught", "Breathe long under water: breath restored, and lost far slower for two minutes.", 60, GillEssence, 0.15f, 45f)
            .Requires(new DominantCondition(GillEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(GillEssence), 0.03f))
            .Fx(ConsumableEffectType.Oxygen, 60f).Fx(ConsumableEffectType.OxygenFactor, -40f, 120f, 1f));
        list.Add(Potion("fireward_tonic", "Fireward Tonic", "Heat and flame trouble you far less for two minutes.", 60, FirewardEssence, 0.15f, 40f)
            .Requires(new DominantCondition(FirewardEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(FirewardEssence), 0.03f))
            .Fx(ConsumableEffectType.BodyHeatFactor, -50f, 120f, 1f).Fx(ConsumableEffectType.HpDamageFactor, -10f, 120f, 1f));
        AlchemyOutcome vitriol = Potion("oil_of_vitriol", "Oil of Vitriol", "A fuming acid for etching, dissolving and darker work. Never drink it.", 58, Vitriol, 0.3f, 40f)
            .Requires(new SubstanceCondition(SubstanceSelector.Of(Vitriol), 0.06f))
            .Fx(ConsumableEffectType.Hp, -40f);
        vitriol.kind = AlchemyOutcomeKind.Material;
        list.Add(vitriol);

        AddHerbalOutcomes(list);

        // Materials and dangerous compounds.
        AlchemyOutcome ink = Potion("iron_gall_ink", "Iron Gall Ink", "A deep black ink for scribes and mapmakers. Not for drinking.", 60, IronGallInk, 0.3f, 30f)
            .Requires(new DominantCondition(IronGallInk)).Requires(new SubstanceCondition(SubstanceSelector.Of(IronGallInk), 0.03f))
            .Fx(ConsumableEffectType.Sanity, -3f);
        ink.kind = AlchemyOutcomeKind.Material;
        list.Add(ink);
        AlchemyOutcome soap = Potion("soft_soap", "Soft Soap", "Cleans anything (do not drink it).", 50, Soap, 0.5f, 8f)
            .Requires(new SubstanceCondition(SubstanceSelector.Of(Soap), 0.2f))
            .Fx(ConsumableEffectType.Hp, -5f);
        soap.kind = AlchemyOutcomeKind.Material;
        list.Add(soap);
        AlchemyOutcome venom = Potion("venom_extract", "Venom Extract", "A potent poison: a material for coatings and darker brews. Do not drink.", 60, Venom, 0.3f, 30f)
            .Requires(new DominantCondition(Venom)).Requires(new SubstanceCondition(SubstanceSelector.Of(Venom), 0.03f))
            .Fx(ConsumableEffectType.Hp, -30f).Fx(ConsumableEffectType.Hp, -20f, 10f, 1f);
        venom.kind = AlchemyOutcomeKind.Material;
        list.Add(venom);
        list.Add(Other("unstable_concoction", "Unstable Concoction", "Crackles in the bottle. Something went very wrong - or very interesting.", AlchemyOutcomeKind.Unstable, 80, 8f)
            .Requires(new AnyOfCondition(new PropertyCondition("volatility", 0.35f, 1000f), new SubstanceCondition(SubstanceSelector.Of(UnstableAether), 0.05f)))
            .Fx(ConsumableEffectType.Hp, -15f).Fx(ConsumableEffectType.SpeedFactor, 10f, 10f, 1f));

        // Failures: every mixture becomes something.
        list.Add(Other("spoiled_mixture", "Spoiled Mixture", "It went too far: sludge and ash.", AlchemyOutcomeKind.Degraded, 40, 1f)
            .Requires(new RoleShareCondition(SubstanceRole.Residue, 0.4f))
            .Fx(ConsumableEffectType.Hp, -5f).Fx(ConsumableEffectType.Sanity, -5f));
        list.Add(Other("incomplete_preparation", "Cloudy Infusion", "Taken off too soon: mostly unreacted ingredients.", AlchemyOutcomeKind.Incomplete, 30, 2f)
            .Requires(new RoleShareCondition(SubstanceRole.Raw, 0.5f))
            .Fx(ConsumableEffectType.Drink, 10f));
        AlchemyOutcome water = Other("plain_water", "Plain Liquid", "Nothing but the base liquid (and perhaps some salts).", AlchemyOutcomeKind.Inert, 20, 0f)
            .Requires(new AnyOfCondition(
                new RoleShareCondition(SubstanceRole.Solvent, 0.97f, 1f, false),
                new AllOfCondition(
                    new RoleShareCondition(SubstanceRole.Active, 0f, 0.02f), new RoleShareCondition(SubstanceRole.Raw, 0f, 0.05f),
                    new RoleShareCondition(SubstanceRole.Residue, 0f, 0.05f), new RoleShareCondition(SubstanceRole.Intermediate, 0f, 0.05f))));
        water.producesItems = false;
        list.Add(water);
        list.Add(Other("murky_mixture", "Murky Mixture", "A cloudy liquid that is nothing in particular.", AlchemyOutcomeKind.Failed, 0, 1f)
            .Fx(ConsumableEffectType.Drink, 5f).Fx(ConsumableEffectType.Sanity, -2f));
        return list;
    }

    private static AlchemyOutcome Potion(string id, string name, string description, int priority, string potency, float fullAt, float price) =>
        new AlchemyOutcome
        {
            id = id, displayName = name, description = description, kind = AlchemyOutcomeKind.Potion, priority = priority,
            potency = SubstanceSelector.Of(potency), fullPotencyAt = fullAt, basePrice = price,
        }.Tier("Weak", 0f, 0.5f).Tier("", 0.35f, 1f).Tier("Potent", 0.65f, 1.5f).Tier("Superior", 0.9f, 2f);

    private static AlchemyOutcome Other(string id, string name, string description, AlchemyOutcomeKind kind, int priority, float price) =>
        new AlchemyOutcome
        {
            id = id, displayName = name, description = description, kind = kind, priority = priority, basePrice = price,
            potency = new SubstanceSelector { id = "" }, tiers = new List<AlchemyOutcomeTier> { new AlchemyOutcomeTier("", 0f, 1f) },
        };

    private static AlchemyOutcome Fx(this AlchemyOutcome o, ConsumableEffectType type, float amount, float duration = 0f, float tick = 0f)
    {
        o.effects.Add(new ConsumableEffect
        {
            effectType = type,
            itemType = ConsumableType.Potion,
            effectName = $"{o.Name} {type}",
            amount = amount,
            timeBuffEffect = duration,
            tickCooldown = tick,
            isProcedural = duration > 0f,
            applicationChance = 1f,
            minimumLevel = 1,
        });
        return o;
    }

    // ------------------------------------------------------------------ ingredients
    /// <summary>A default ingredient (the Create Ingredient Items tool makes items of them; free supplies use them).</summary>
    public sealed class IngredientPreset
    {
        public string id, name, description;
        public MaterialKind kind;
        public float price, weight;
        public Color color;
        public AlchemyIngredientProfile profile;
    }

    /// <summary>
    /// The built-in ingredients. Each lists what it holds and how easily each part comes out (extractability): Sunpetal
    /// holds a healing sap that comes out readily, a fragrant oil that water barely holds, and a bitter latex locked in
    /// its stems that a whole petal gives up slowly - grind it and the latex comes out too (it is destroyed above 85 °C).
    /// </summary>
    public static List<IngredientPreset> Ingredients()
    {
        List<IngredientPreset> list = BaseIngredients();
        list.AddRange(HerbalIngredients());
        return list;
    }

    private static List<IngredientPreset> BaseIngredients() => new List<IngredientPreset>
    {
        I("Spring Water", "A flask of clean water: the base of most brews.", MaterialKind.Other, 1f, 0.5f, new Color(0.55f, 0.75f, 0.95f), 0f, 20f,
            "the water splashes in", Liquid, (Water, 10f, 1f)),
        I("Grain Spirit", "Strong alcohol. Draws out resins and oils, keeps things from rotting - and burns.", MaterialKind.Other, 4f, 0.5f, new Color(0.9f, 0.88f, 0.7f), 0f, 20f,
            "a sharp boozy smell rises", Liquid, (Spirit, 6f, 1f)),
        I("Lamp Oil", "A heavy oil that lets a brew get far hotter than water does.", MaterialKind.Other, 3f, 0.5f, new Color(0.85f, 0.7f, 0.3f), 0f, 20f,
            "the oil pools and spreads", Liquid, (Oil, 6f, 1f)),
        I("Sunpetal", "Golden petals full of sweet sap, with a fragrant oil and a bitter latex in the stems.", MaterialKind.Herb, 3f, 0.05f, new Color(1f, 0.8f, 0.2f), 20f, 22f,
            "the petals float, then sink", Whole, (SolarSap, 3.6f, 0.9f), (PetalOil, 0.5f, 0.6f), (BitterLatex, 0.6f, 0.06f), (Water, 0.5f, 1f)),
        I("Sunpetal Extract", "A vial of sunpetal sap, already drawn out and cleaned of oil and latex.", MaterialKind.Essence, 9f, 0.1f, new Color(1f, 0.85f, 0.3f), 4f, 20f,
            "the golden extract streams in", Extract, (SolarSap, 4.5f, 1f), (Water, 1f, 1f)),
        I("Frostmint", "Leaves so cold they frost your fingers.", MaterialKind.Herb, 4f, 0.05f, new Color(0.6f, 0.95f, 1f), 15f, -5f,
            "frost creeps up the rim", Whole, (FrostOil, 4f, 0.85f), (FungalPulp, 0.3f, 0.3f)),
        I("Frostmint Extract", "Frost oil pressed from frostmint: pure and ice cold.", MaterialKind.Essence, 10f, 0.1f, new Color(0.7f, 0.97f, 1f), 3f, -10f,
            "an icy thread of oil sinks in", Extract, (FrostOil, 5f, 1f)),
        I("Embercap", "A mushroom that glows like a coal.", MaterialKind.Herb, 5f, 0.1f, new Color(0.95f, 0.3f, 0.1f), 25f, 45f,
            "the cap hisses as it touches the liquid", Whole, (EmberDust, 3f, 0.8f), (FungalPulp, 1f, 0.5f)),
        I("Bitterroot", "A gnarled, poisonous root.", MaterialKind.Herb, 3f, 0.1f, new Color(0.3f, 0.45f, 0.2f), 30f, 20f,
            "a bitter smell", Whole, (BitterResin, 3f, 0.6f), (FungalPulp, 1f, 0.4f)),
        I("Moonbloom", "A flower that only opens at night.", MaterialKind.Herb, 6f, 0.05f, new Color(0.8f, 0.78f, 1f), 10f, 15f,
            "the petals shimmer as they dissolve", Whole, (MoonDew, 4f, 0.95f)),
        I("Rock Salt", "Coarse salt crystals. Water holds about a third of its own amount; spirit almost none.", MaterialKind.Stone, 1f, 0.1f, new Color(0.9f, 0.88f, 0.85f), 8f, 20f,
            "the crystals crackle and vanish", Whole, (Brine, 3f, 1f)),
        I("Quicksilver Drop", "A bead of liquid metal in a tiny vial.", MaterialKind.Ingot, 12f, 0.1f, new Color(0.82f, 0.84f, 0.88f), 0f, 20f,
            "a bead of metal rolls to the bottom", Liquid, (Quicksilver, 0.6f, 1f)),
        I("Lead Dust", "A pouch of heavy grey dust.", MaterialKind.Ingot, 2f, 0.2f, new Color(0.42f, 0.44f, 0.47f), 4f, 20f,
            "a grey haze sinks through the liquid", Ground, (LeadDust, 1f, 1f)),
        I("Void Salt", "Black salt that seems to drink the light.", MaterialKind.Essence, 15f, 0.1f, new Color(0.15f, 0.12f, 0.2f), 5f, 20f,
            "the black grains vanish without a ripple", Ground, (VoidSalt, 2f, 1f)),
        I("Glowcap Mushroom", "A common pale mushroom.", MaterialKind.Herb, 1f, 0.05f, new Color(0.7f, 0.6f, 0.45f), 20f, 20f,
            "the mushroom softens", Whole, (FungalPulp, 3f, 0.7f)),
        I("Moon Crystal", "A pale crystal grown from moon dew. Dissolves into pure mana essence.", MaterialKind.Gem, 30f, 0.1f, new Color(0.85f, 0.85f, 1f), 40f, 10f,
            "the crystal slowly melts away", Whole, (ManaEssence, 3f, 0.9f)),
        I("Fire Ash", "Grey ash of burnt-out ardent essence. Still faintly warm.", MaterialKind.Essence, 1f, 0.05f, new Color(0.4f, 0.35f, 0.32f), 10f, 30f,
            "the ash clouds the liquid", Ground, (EmberDust, 0.5f, 1f), (FireAsh, 1f, 1f)),

        // ---- Liquids and waters.
        I("Vinegar", "Soured wine: an acid. It draws alkaloids out of berries and roots, curdles milk - and destroys healing essence.", MaterialKind.Other, 2f, 0.5f, new Color(0.9f, 0.85f, 0.6f), 0f, 20f,
            "a sour smell rises", Liquid, (Water, 8f, 1f), (AceticAcid, 1f, 1f)),
        I("Muddy Water", "River water full of silt. It clouds every brew made with it: filter it, or settle it with alum.", MaterialKind.Other, 0.2f, 0.5f, new Color(0.55f, 0.48f, 0.35f), 0f, 15f,
            "brown water swirls in", Liquid, (Water, 9f, 1f), (Silt, 0.6f, 1f)),
        I("Seawater", "Salt water. Boil it down in a wide dish and the salt is left behind.", MaterialKind.Other, 0.5f, 0.5f, new Color(0.4f, 0.6f, 0.7f), 0f, 15f,
            "a smell of the sea", Liquid, (Water, 9f, 1f), (Brine, 1.5f, 1f), (Silt, 0.1f, 1f)),
        I("Holy Water", "Water from a shrine. It destroys venom, sludge and unstable magic.", MaterialKind.Other, 12f, 0.5f, new Color(0.9f, 0.95f, 1f), 0f, 15f,
            "the water seems to glow for a moment", Liquid, (Water, 9f, 1f), (Sanctity, 1.5f, 1f)),
        I("Turpentine", "Distilled pine resin: an oily solvent that floats on water and dissolves resins and waxes.", MaterialKind.Other, 6f, 0.5f, new Color(0.95f, 0.9f, 0.7f), 0f, 20f,
            "a sharp piney smell", Liquid, (Turpentine, 6f, 1f)),
        I("Goat Milk", "Fresh milk. Its curd binds venoms - and curdles in acid.", MaterialKind.Other, 2f, 0.5f, new Color(0.97f, 0.96f, 0.92f), 0f, 12f,
            "the milk swirls white", Liquid, (Water, 8f, 1f), (Casein, 1f, 1f), (Sugars, 0.4f, 1f)),
        I("Egg Yolk", "A yolk with a little white: it holds oil and water together. The white binds tannins, and sets above 62 °C.", MaterialKind.Other, 1f, 0.1f, new Color(1f, 0.8f, 0.3f), 3f, 15f,
            "the yolk slides in", Liquid, (Lecithin, 0.4f, 1f), (Albumen, 1.5f, 1f), (Water, 1f, 1f)),
        I("Honey", "Thick and sweet: it keeps brews from rotting, and yeast turns it into mead.", MaterialKind.Other, 4f, 0.2f, new Color(1f, 0.75f, 0.2f), 15f, 20f,
            "the honey sinks in a slow golden ribbon", Liquid, (Sugars, 3f, 1f), (Water, 1f, 1f), (Beeswax, 0.3f, 0.3f)),
        I("Ice Chips", "Chipped ice: it chills a brew in an instant (melting takes a lot of heat).", MaterialKind.Gem, 0.5f, 0.3f, new Color(0.85f, 0.95f, 1f), 0f, -60f,
            "the ice cracks and melts, chilling everything", Liquid, (Water, 5f, 1f)),

        // ---- Acids, alkalis and salts.
        I("Sourfruit", "A small, fiercely sour fruit: an acid with a little sugar.", MaterialKind.Herb, 2f, 0.1f, new Color(0.95f, 0.9f, 0.3f), 15f, 18f,
            "the fruit splits and bleeds its juice", Whole, (CitricAcid, 1.2f, 0.8f), (Water, 1.5f, 1f), (Sugars, 0.4f, 0.9f)).Hard(0.2f),
        I("Potash", "Lye leached from wood ash: a strong alkali. It cooks oil into soap and neutralises acids.", MaterialKind.Stone, 3f, 0.2f, new Color(0.8f, 0.8f, 0.75f), 5f, 20f,
            "the grey powder hisses as it dissolves", Ground, (Lye, 2.5f, 1f)),
        I("Chalk", "Powdered chalk: it hardly dissolves, but slowly neutralises acid.", MaterialKind.Stone, 1f, 0.2f, new Color(0.95f, 0.95f, 0.92f), 3f, 20f,
            "a white haze sinks", Ground, (Chalk, 3f, 1f)),
        I("Alum Crystals", "Clear, pure crystals of alum, prized by dyers and healers. They dissolve slowly, and well only when hot.", MaterialKind.Gem, 14f, 0.1f, new Color(0.92f, 0.92f, 1f), 2f, 20f,
            "the crystals tumble to the bottom", Whole, (AlumCrystal, 2f, 1f)).NoGrind(),
        I("Alum Powder", "Powdered alum: a clarifier that dissolves well only when hot.", MaterialKind.Stone, 4f, 0.1f, new Color(0.9f, 0.9f, 0.95f), 10f, 20f,
            "a white powder clouds the liquid", Ground, (Alum, 2f, 1f)),
        I("Crude Alum", "Alum dug from the ground, mixed with silt. Dissolve it hot, filter it, cool it slowly and still to grow pure crystals.", MaterialKind.Stone, 3f, 0.2f, new Color(0.75f, 0.72f, 0.7f), 40f, 20f,
            "grey lumps crumble in", Whole, (Alum, 1.6f, 0.9f), (Silt, 0.5f, 1f)).Hard(0.7f),
        I("Saltpetre Crystals", "Pure saltpetre needles: they feed fire, and make blasting powder. Far more dissolves hot than cold.", MaterialKind.Gem, 10f, 0.1f, new Color(0.97f, 0.97f, 0.95f), 2f, 20f,
            "white needles fall in", Whole, (NiterCrystal, 2f, 1f)).NoGrind(),
        I("Saltpetre Powder", "Powdered saltpetre: it feeds fire and dissolves quickly.", MaterialKind.Stone, 4f, 0.1f, new Color(0.95f, 0.95f, 0.92f), 8f, 20f,
            "a white powder dissolves", Ground, (Niter, 2f, 1f)),
        I("Cave Saltpetre", "Saltpetre scraped from cave walls, full of dirt and salt. Purify it: dissolve hot, filter, cool slowly - or seed it.", MaterialKind.Stone, 3f, 0.2f, new Color(0.8f, 0.78f, 0.72f), 30f, 18f,
            "a gritty crust crumbles in", Whole, (Niter, 1.6f, 0.9f), (Silt, 0.4f, 1f), (Brine, 0.3f, 1f)).Hard(0.5f),
        I("Iron Filings", "Fine iron: it turns tannin into ink, and hurries the browning of brews stirred too hard.", MaterialKind.Ingot, 2f, 0.3f, new Color(0.4f, 0.4f, 0.43f), 2f, 20f,
            "dark grit sinks to the bottom", Ground, (Iron, 1f, 1f)),
        I("Charcoal", "Porous charcoal: it soaks up poisons, burns long and steady, and makes a filter bed.", MaterialKind.Wood, 2f, 0.2f, new Color(0.15f, 0.15f, 0.15f), 3f, 20f,
            "black flakes float on the surface", Whole, (Charcoal, 2f, 1f)).Hard(0.3f),

        // ---- Herbs, barks and roots.
        I("Lavender", "Fragrant flowers. Their oil calms - but water barely holds it: use spirit or oil, or distil it.", MaterialKind.Herb, 4f, 0.05f, new Color(0.7f, 0.55f, 0.9f), 20f, 20f,
            "a sweet floral scent spreads", Whole, (LavenderOil, 0.8f, 0.55f), (Tannin, 0.3f, 0.3f), (PlantFibre, 0.4f, 0.3f), (Water, 0.3f, 1f)).Hard(0.2f),
        I("Lavender Oil", "Lavender's oil, distilled pure.", MaterialKind.Essence, 15f, 0.1f, new Color(0.75f, 0.6f, 0.95f), 2f, 20f,
            "the oil beads on the surface", Extract, (LavenderOil, 3f, 1f)),
        I("Fire Pepper", "A small red pepper that burns. Its oil wants oil or spirit, not water.", MaterialKind.Herb, 3f, 0.05f, new Color(0.9f, 0.2f, 0.1f), 20f, 20f,
            "eye-watering fumes rise", Whole, (PepperOil, 1.2f, 0.5f), (PlantFibre, 0.8f, 0.3f), (Water, 0.4f, 1f)).Hard(0.3f),
        I("Oak Gall", "A hard nut-like growth on oak leaves, full of tannin. With iron it makes ink.", MaterialKind.Herb, 2f, 0.05f, new Color(0.6f, 0.45f, 0.3f), 40f, 20f,
            "the gall bobs, then sinks", Whole, (Tannin, 2f, 0.6f), (PlantFibre, 0.5f, 0.3f)).Hard(0.6f),
        I("Willow Bark", "Bitter bark: in warm acid it gives an essence that eases pain. Its tannin spoils the purity: bind it with egg white.", MaterialKind.Wood, 3f, 0.1f, new Color(0.55f, 0.45f, 0.35f), 40f, 20f,
            "strips of bark soak and darken", Whole, (Salicin, 1f, 0.4f), (Tannin, 1f, 0.6f), (PlantFibre, 1f, 0.3f)).Hard(0.7f),
        I("Snakeweed Root", "A bitter root. In acid it gives up an alkaloid that gentle heat turns into antitoxin.", MaterialKind.Herb, 6f, 0.1f, new Color(0.55f, 0.65f, 0.35f), 90f, 20f,
            "the root sinks, trailing bitterness", Whole, (Serpentine, 1f, 0.2f), (Tannin, 0.5f, 0.5f), (PlantFibre, 1f, 0.3f)).Hard(0.6f),
        I("Dreamshade Berries", "Dusky berries that bring deep sleep - their alkaloid comes out in acid; heat and alkali destroy it.", MaterialKind.Herb, 7f, 0.05f, new Color(0.35f, 0.25f, 0.55f), 40f, 18f,
            "the berries burst, staining the liquid violet", Whole, (Somnine, 0.6f, 0.2f), (Sugars, 1.2f, 0.9f), (Water, 1f, 1f)).Hard(0.15f),
        I("Pine Resin", "Sticky resin: distil it for turpentine. It also flares on a fire.", MaterialKind.Wood, 2f, 0.1f, new Color(0.85f, 0.6f, 0.2f), 30f, 20f,
            "the resin softens into amber blobs", Whole, (Turpentine, 1.2f, 0.7f), (Rosin, 1.8f, 0.6f)).Hard(0.5f),
        I("Beeswax", "Wax from the comb: it melts into hot oil and sets as it cools.", MaterialKind.Other, 5f, 0.1f, new Color(0.95f, 0.8f, 0.35f), 30f, 20f,
            "the wax softens", Whole, (Beeswax, 3f, 1f)).Hard(0.3f),
        I("Brewer's Yeast", "Live yeast: with sugar and warmth it brews spirit. Heat above 42 °C kills it.", MaterialKind.Essence, 3f, 0.05f, new Color(0.9f, 0.85f, 0.7f), 5f, 18f,
            "a bready smell", Ground, (Yeast, 0.5f, 1f)),
        I("Blasting Powder", "Saltpetre, ember dust and charcoal. It goes off above 190 °C.", MaterialKind.Essence, 25f, 0.1f, new Color(0.25f, 0.22f, 0.2f), 1f, 20f,
            "the powder sinks - careful", Ground, (BlastingPowder, 2f, 1f)),

        // ---- Pack 4.
        I("Bloodmoss", "A dark red moss that grows on old battlefields. Warm it gently - a pinch of iron quickens it.", MaterialKind.Herb, 4f, 0.05f, new Color(0.55f, 0.1f, 0.12f), 20f, 18f,
            "the moss bleeds red into the liquid", Whole, (SanguineSap, 3f, 0.85f), (PlantFibre, 0.4f, 0.3f), (Tannin, 0.2f, 0.3f)).Hard(0.2f),
        I("Thunder Agate", "A banded blue stone that crackles in a storm. Grind it fine and stir it hard into mana - but keep it cool.", MaterialKind.Gem, 18f, 0.2f, new Color(0.45f, 0.55f, 0.85f), 30f, 20f,
            "blue grit sinks and crackles", Whole, (StormSalt, 2f, 0.5f)).Hard(0.9f),
        I("Glacier Lichen", "A pale lichen from the ice fields: frost oil and a sharp acid.", MaterialKind.Herb, 5f, 0.05f, new Color(0.75f, 0.85f, 0.8f), 20f, -2f,
            "a chill spreads from the lichen", Whole, (FrostOil, 1.5f, 0.6f), (LichenAcid, 0.4f, 0.7f), (PlantFibre, 0.5f, 0.3f)).Hard(0.25f),
        I("Nightcap", "A black mushroom whose spores stir only at night.", MaterialKind.Herb, 6f, 0.05f, new Color(0.18f, 0.15f, 0.25f), 25f, 15f,
            "a dark cloud of spores drifts down", Whole, (ShadowSpores, 2f, 0.6f), (FungalPulp, 1f, 0.5f)),
        I("Wyrmscale", "A scale shed by a young wyrm. Its ichor wants hot oil and ardent essence.", MaterialKind.MonsterPart, 40f, 0.2f, new Color(0.6f, 0.25f, 0.1f), 45f, 25f,
            "the scale smokes as it sinks", Whole, (DraconicIchor, 1.5f, 0.4f)).Hard(0.85f),
        I("Ginger Root", "A knobbly root full of heat. In hot water it makes a warming tonic; in mead, spiced mead.", MaterialKind.Herb, 2f, 0.05f, new Color(0.85f, 0.7f, 0.4f), 25f, 20f,
            "a spicy smell rises", Whole, (Gingerol, 1.4f, 0.5f), (PlantFibre, 0.8f, 0.3f), (Water, 0.4f, 1f)).Hard(0.5f),
        I("Rose Petals", "Red petals with a sweet, heady oil. Distil it, or draw it out in spirit.", MaterialKind.Herb, 4f, 0.02f, new Color(0.85f, 0.2f, 0.3f), 15f, 20f,
            "the petals scent the air", Whole, (RoseOil, 0.5f, 0.55f), (Sugars, 0.3f, 0.9f), (PlantFibre, 0.3f, 0.3f), (Water, 0.3f, 1f)).Hard(0.1f),
        I("Cave Sulphur", "Yellow brimstone. Boil it in water with air beaten in for oil of vitriol - which chars every herb it touches.", MaterialKind.Stone, 3f, 0.2f, new Color(0.95f, 0.85f, 0.25f), 10f, 20f,
            "yellow grains sink with a reek of rotten eggs", Ground, (Brimstone, 2f, 1f)),
        I("Wakeberries", "Bitter brown berries that chase off sleep. Their alkaloid comes out in alkali - a little potash.", MaterialKind.Herb, 3f, 0.05f, new Color(0.45f, 0.25f, 0.12f), 40f, 20f,
            "a bitter, roasted smell", Whole, (Vigorine, 0.6f, 0.2f), (Sugars, 0.6f, 0.9f), (Water, 0.8f, 1f)).Hard(0.3f),
        I("Mandrake Root", "A forked root that screams when pulled. Its alkaloid comes out in acid - and turns a healing brew into something greater.", MaterialKind.Herb, 15f, 0.1f, new Color(0.65f, 0.55f, 0.35f), 40f, 18f,
            "a faint shriek as it goes under", Whole, (Mandragorin, 1.2f, 0.35f), (Tannin, 0.3f, 0.5f), (PlantFibre, 1f, 0.3f)).Hard(0.6f),
        I("Phoenix Feather", "A feather that never stops glowing. Its ash reborn in fire, with ardent essence, makes the rarest healing of all.", MaterialKind.MonsterPart, 80f, 0.02f, new Color(1f, 0.5f, 0.15f), 20f, 60f,
            "the feather flares and crumbles into glowing ash", Whole, (PhoenixAsh, 1f, 1f)).Hard(0.2f),
        I("Griffin Down", "Down lighter than air: it floats on water. Spirit draws its lightness out.", MaterialKind.MonsterPart, 20f, 0.01f, new Color(0.96f, 0.95f, 0.92f), 15f, 20f,
            "the down floats on the surface", Whole, (AetherDown, 1.5f, 0.7f)).Hard(0.1f),

        I("Silverleaf", "A pale leaf that shines in moonlight. Steep it with holy water.", MaterialKind.Herb, 8f, 0.02f, new Color(0.8f, 0.85f, 0.9f), 20f, 18f,
            "the leaf glints as it sinks", Whole, (SilverSap, 2.5f, 0.8f), (PlantFibre, 0.4f, 0.3f)).Hard(0.2f),
        I("Deepsea Kelp", "A dark, slippery kelp from the deep. Its gift comes out only in strong salt water.", MaterialKind.Herb, 4f, 0.1f, new Color(0.25f, 0.4f, 0.25f), 30f, 10f,
            "the kelp slithers in", Whole, (KelpGel, 2.5f, 0.6f), (Brine, 0.4f, 1f), (PlantFibre, 0.5f, 0.3f)).Hard(0.3f),
        I("Ashbloom", "A grey flower that grows on burnt ground. Boil it; a pinch of fire ash hurries it.", MaterialKind.Herb, 5f, 0.02f, new Color(0.55f, 0.5f, 0.45f), 25f, 20f,
            "grey pollen drifts down", Whole, (AshPollen, 2f, 0.7f), (PlantFibre, 0.4f, 0.3f)).Hard(0.2f),
        I("Ember Salt", "Ember dust bound in brine: safe to carry and store. Heat it past 65 °C and the fire comes free.", MaterialKind.Essence, 6f, 0.1f, new Color(0.9f, 0.45f, 0.3f), 5f, 20f,
            "warm orange grains sink", Ground, (EmberSalt, 2f, 1f)),
        I("Alchemical Gold", "Gold made from lead. Worth a fortune - if anyone believes where it came from.", MaterialKind.Gem, 150f, 0.1f, new Color(1f, 0.82f, 0.3f), 5f, 20f,
            "golden flecks sink to the bottom", Ground, (AlchemicalGold, 1f, 1f)),

        // ---- Supplies (they go into no vessel): filter media and fuel.
        I("Filter Paper", "A sheet of fine paper: filters out every particle. Used up each time.", MaterialKind.Cloth, 1f, 0.01f, new Color(0.95f, 0.95f, 0.9f), 0f, 20f,
            "", Liquid),
        I("Firewood", "A split log for the fire under the vessel.", MaterialKind.Wood, 0.5f, 1f, new Color(0.55f, 0.4f, 0.25f), 0f, 20f,
            "", Liquid),
    };

    private static IngredientPreset Hard(this IngredientPreset p, float hardness)
    {
        p.profile.hardness = hardness;
        return p;
    }

    private static IngredientPreset NoGrind(this IngredientPreset p)
    {
        p.profile.canBeGround = false;
        return p;
    }

    // Forms of the presets: whole (can be ground), already ground or powdered, liquid (cannot be ground), extract.
    private const string Whole = "whole", Ground = "ground", Extract = "extract", Liquid = "liquid";

    private static IngredientPreset I(string name, string description, MaterialKind kind, float price, float weight, Color color,
        float dissolve, float temperature, string observation, string form, params (string substance, float amount, float extractability)[] releases)
    {
        var profile = new AlchemyIngredientProfile
        {
            dissolveSeconds = dissolve, temperature = temperature, observation = observation,
            form = form == Liquid ? Whole : form, canBeGround = form == Whole,
        };
        foreach (var r in releases)
            profile.releases.Add(new IngredientRelease(r.substance, r.amount, r.extractability));
        return new IngredientPreset
        {
            id = name.ToLowerInvariant().Replace(' ', '_'), name = name, description = description, kind = kind,
            price = price, weight = weight, color = color, profile = profile,
        };
    }

    /// <summary>A default ingredient by name or id (tests, free supplies).</summary>
    public static IngredientPreset Ingredient(string nameOrId)
    {
        foreach (IngredientPreset p in Ingredients())
            if (string.Equals(p.name, nameOrId, System.StringComparison.OrdinalIgnoreCase) || string.Equals(p.id, nameOrId, System.StringComparison.OrdinalIgnoreCase))
                return p;
        return null;
    }

    /// <summary>
    /// The built-in ingredient made (almost) only of <paramref name="substanceId"/> - what that substance is collected as
    /// when it settles out (brine → Rock Salt). Null when there is none.
    /// </summary>
    public static IngredientPreset MainSourceOf(string substanceId)
    {
        foreach (IngredientPreset p in Ingredients())
        {
            List<IngredientRelease> r = p.profile.releases;
            if (r.Count > 0 && string.Equals(r[0].substance, substanceId, System.StringComparison.OrdinalIgnoreCase) && r[0].amount >= 0.8f * p.profile.UnitsPerItem)
                return p;
        }
        return null;
    }

    /// <summary>A default ingredient as something a preparation can take.</summary>
    public static IAlchemyIngredient Spec(string nameOrId)
    {
        IngredientPreset p = Ingredient(nameOrId);
        return p != null ? new AlchemyIngredientSpec(p.id, p.name, p.profile) : null;
    }

    // ------------------------------------------------------------------ filters and fuels
    /// <summary>The built-in filters: a reusable cloth strainer, filter paper, a charcoal bed (it adsorbs).</summary>
    public static List<AlchemyFilter> Filters() => new List<AlchemyFilter>
    {
        new AlchemyFilter
        {
            id = "strainer", displayName = "Cloth Strainer", description = "Strains out spent herbs, fibres and coarse particles. Reusable.",
            strainsIngredients = true, catchesUndissolved = 0.6f, catchesSolids = 0.8f, liquidLoss = 0.02f, observation = "the liquid runs through the cloth",
        },
        new AlchemyFilter
        {
            id = "paper", displayName = "Filter Paper", description = "Catches every particle, however fine. Uses up a sheet.",
            strainsIngredients = true, catchesUndissolved = 1f, catchesSolids = 1f, liquidLoss = 0.05f, medium = "Filter Paper", observation = "the liquid drips through clear",
        },
        new AlchemyFilter
        {
            id = "charcoal", displayName = "Charcoal Bed",
            description = "Catches every particle and soaks up poisons, residues, tannins and dirt (and a little of everything else). Uses up a charcoal.",
            strainsIngredients = true, catchesUndissolved = 1f, catchesSolids = 1f, liquidLoss = 0.08f, medium = "Charcoal", observation = "the liquid seeps through the black bed",
        }.Adsorbs(SubstanceSelector.WithProperty("toxicity", 0.4f), 0.75f).Adsorbs(SubstanceSelector.OfRole(SubstanceRole.Residue), 0.8f)
         .Adsorbs(SubstanceSelector.WithProperty("contaminant", 0.3f), 0.9f).Adsorbs(SubstanceSelector.WithProperty("astringent", 0.5f), 0.5f)
         .Adsorbs(SubstanceSelector.OfRole(SubstanceRole.Active), 0.1f),
    };

    /// <summary>The built-in fuels of a manual fire (firewood is free at a station with a woodpile).</summary>
    public static List<AlchemyFuel> Fuels() => new List<AlchemyFuel>
    {
        new AlchemyFuel { id = "firewood", displayName = "Firewood", energy = 1f, observation = "the log catches" },
        new AlchemyFuel { id = "charcoal", displayName = "Charcoal", energy = 2.2f, item = "Charcoal", observation = "the charcoal glows red" },
        new AlchemyFuel { id = "pine_resin", displayName = "Pine Resin", energy = 0.5f, flare = 0.8f, item = "Pine Resin", observation = "the resin flares up" },
        new AlchemyFuel { id = "lamp_oil", displayName = "Lamp Oil", energy = 1.2f, flare = 0.4f, item = "Lamp Oil", observation = "the oil burns bright" },
    };

    // ------------------------------------------------------------------ equipment
    public static List<AlchemyEquipmentProfile> Equipment() => new List<AlchemyEquipmentProfile>
    {
        AlchemyEquipmentProfile.DefaultCauldron(),
        new AlchemyEquipmentProfile
        {
            displayName = "Copper Alembic",
            description = "A copper still: what boils off is cooled and collected in a flask. Copper hums with moon magic.",
            capacity = 30f, vesselMass = 15f, maxHeatTemperature = 220f, heatTransfer = 2f, ambientTransfer = 0.6f,
            vapor = VaporHandling.Condense, stirEfficiency = 0.4f, tags = new List<string> { "copper", "distills" },
        },
        new AlchemyEquipmentProfile
        {
            displayName = "Glass Retort",
            description = "A sealed glass vessel: nothing escapes, pressure builds, and you can see everything inside.",
            capacity = 20f, vesselMass = 6f, maxHeatTemperature = 300f, heatTransfer = 1.6f, ambientTransfer = 0.5f,
            vapor = VaporHandling.Sealed, maxPressure = 2f, stirEfficiency = 0.3f, revealsSubstances = true,
            tags = new List<string> { "glass", "sealed" },
        },
        new AlchemyEquipmentProfile
        {
            displayName = "Frost Basin",
            description = "A rune-carved basin that chills its contents to just above freezing - but never gets very hot.",
            capacity = 40f, vesselMass = 20f, maxHeatTemperature = 70f, canCool = true, coolingTemperature = 2f,
            heatTransfer = 2.5f, ambientTransfer = 0.6f, vapor = VaporHandling.Vent, stirEfficiency = 0.6f,
            tags = new List<string> { "stone", "frost", "open" },
        },
        new AlchemyEquipmentProfile
        {
            displayName = "Fractionating Still",
            description = "An alembic with a tall copper column: what boils at higher temperatures drips back, so the flask collects the most volatile part almost alone.",
            capacity = 30f, vesselMass = 18f, maxHeatTemperature = 220f, heatTransfer = 2f, ambientTransfer = 0.6f,
            vapor = VaporHandling.Condense, fractionation = 2.5f, surface = 0.8f, stirEfficiency = 0.4f, tags = new List<string> { "copper", "distills", "column" },
        },
        new AlchemyEquipmentProfile
        {
            displayName = "Evaporating Dish",
            description = "A wide, shallow clay dish: liquids evaporate from it six times faster - for boiling things down and drying salts.",
            capacity = 25f, vesselMass = 5f, maxHeatTemperature = 220f, heatTransfer = 2.4f, ambientTransfer = 1.2f,
            vapor = VaporHandling.Vent, surface = 6f, stirEfficiency = 0.7f, tags = new List<string> { "clay", "open", "shallow" },
        },
    };
}
