using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The herbal pack of the built-in alchemy: more ingredients, the substances they release, the reactions that turn
/// them into essences, and the potions those make - and second and third recipes for potions that already exist
/// (healing from nettles, vigour from dragontongue, mana from the starlight lotus, calm from chamomile, antitoxin from
/// wormwood, sleep from poppies, fireward from salamander scales, venom from nightshade). Spoilers:
/// <c>Documentation/Alchemy/05-Default-Chemistry.md</c> (the "Herbal pack" section).
/// </summary>
public static partial class AlchemyDefaults
{
    public const string BlazeNectar = "blaze_nectar", NettleSap = "nettle_sap", LotusNectar = "lotus_nectar", TrollSap = "troll_sap",
        GraniteDust = "granite_dust", StoneskinEssence = "stoneskin_essence", CloverNectar = "clover_nectar", GustPollen = "gust_pollen",
        SwiftEssence = "swift_essence", SageOil = "sage_oil", ClarityEssence = "clarity_essence", BearTallow = "bear_tallow",
        EmberResin = "ember_resin", HearthwardEssence = "hearthward_essence", GiantSpores = "giant_spores", TitanEssence = "titan_essence",
        BarleyStarch = "barley_starch", NourishingBroth = "nourishing_broth", Absinthin = "absinthin", PoppyLatex = "poppy_latex",
        ChamomileOil = "chamomile_oil", SalamanderSlime = "salamander_slime", NightshadeJuice = "nightshade_juice";

    // ------------------------------------------------------------------ substances
    private static List<AlchemySubstance> HerbalSubstances() => new List<AlchemySubstance>
    {
        S(BlazeNectar, "Blaze Nectar", SubstanceRole.Raw, new Color(1f, 0.5f, 0.12f), 180f, "The fiery nectar of dragontongue. Boiled, it becomes ardent essence at once; warm, with pepperfire, salamander essence.", "an orange nectar that warms the glass")
            .With("fire", 0.5f).With("vitality", 0.2f).With("organic", 0.5f).WithDensity(1.15f)
            .StableUpTo(140f, 0.03f, "the nectar scorches black", (Sludge, 0.7f)),
        S(NettleSap, "Nettle Sap", SubstanceRole.Raw, new Color(0.35f, 0.6f, 0.25f), 900f, "The green sap of stinging nettles: steeped hot, its sting goes out and a healing essence comes.", "a green, faintly stinging sap")
            .With("vitality", 0.35f).With("organic", 0.5f).With("toxicity", 0.02f).WithDensity(1.05f),
        S(LotusNectar, "Lotus Nectar", SubstanceRole.Raw, new Color(0.75f, 0.8f, 1f), 120f, "Nectar of the starlight lotus: magic like moon dew, but it wakes by day as well as by night.", "a pale blue, starry nectar")
            .With("arcane", 0.5f).With("mind", 0.2f).WithDensity(1.04f)
            .Dissolves(Water, 1f).Dissolves(Spirit, 0.5f).Dissolves(Oil, 0.02f).Dissolves(Turpentine, 0.02f),
        S(TrollSap, "Troll Sap", SubstanceRole.Raw, new Color(0.45f, 0.5f, 0.35f), 900f, "The grey-green sap of troll moss: a pinch of iron sets it into a stone-hard essence.", "a heavy grey-green sap")
            .With("vitality", 0.2f).With("mineral", 0.2f).With("organic", 0.4f).WithDensity(1.2f),
        S(GraniteDust, "Granite Dust", SubstanceRole.Raw, new Color(0.6f, 0.58f, 0.56f), 900f, "Rock flour: it does nothing - until tempered essence takes it up.", "a grey grit that sinks")
            .Solid().With("mineral", 0.9f).WithDensity(2.7f),
        S(StoneskinEssence, "Stoneskin Essence", SubstanceRole.Active, new Color(0.55f, 0.55f, 0.6f), 230f, "It hardens the skin like stone - and the body grows a little slow.", "a heavy, slate-grey liquid")
            .With("stability", 0.6f).With("mineral", 0.4f).With("vitality", 0.1f).WithDensity(1.3f)
            .StableUpTo(115f, 0.03f, "the grey goes chalky", (Sludge, 0.6f)),
        S(CloverNectar, "Clover Nectar", SubstanceRole.Raw, new Color(0.75f, 0.85f, 0.35f), 900f, "The nectar of harefoot clover: gentle heat quickens it.", "a sweet green-gold nectar")
            .With("vitality", 0.1f).With("organic", 0.4f).WithDensity(1.1f),
        S(GustPollen, "Gust Pollen", SubstanceRole.Raw, new Color(0.92f, 0.95f, 0.88f), 900f, "Pollen of windchime seeds. Water barely wets it; cool spirit draws its swiftness out.", "a pale pollen that drifts on the surface")
            .With("volatility", 0.2f).With("arcane", 0.2f).With("organic", 0.2f).WithDensity(0.8f)
            .Dissolves(Water, 0.05f).Dissolves(Spirit, 1f).Dissolves(Oil, 0.3f).Dissolves(Turpentine, 0.3f),
        S(SwiftEssence, "Swift Essence", SubstanceRole.Active, new Color(0.6f, 0.95f, 0.55f), 200f, "Light feet and a quick breath.", "a lively, pale green liquid")
            .With("vitality", 0.2f).With("volatility", 0.1f).WithDensity(1f)
            .StableUpTo(95f, 0.03f, "the green goes flat and brown", (Sludge, 0.6f)),
        S(SageOil, "Sage Oil", SubstanceRole.Raw, new Color(0.6f, 0.7f, 0.55f), 170f, "The clearing oil of sage. Water barely holds it: steep it in spirit.", "a sharp, clean herbal smell")
            .With("mind", 0.5f).With("organic", 0.3f).WithDensity(0.92f)
            .Dissolves(Water, 0.05f).Dissolves(Spirit, 0.8f).Dissolves(Oil, 1f).Dissolves(Turpentine, 1f).Volatile(1.2f),
        S(ClarityEssence, "Clarity Essence", SubstanceRole.Active, new Color(0.65f, 0.85f, 1f), 210f, "A clear head: magic comes back faster, fear slides off.", "a crystal-clear, faintly blue liquid")
            .With("mind", 0.6f).With("arcane", 0.2f).WithDensity(1f)
            .StableUpTo(90f, 0.03f, "the clear liquid clouds", (Sludge, 0.5f)),
        S(BearTallow, "Bear Tallow", SubstanceRole.Raw, new Color(0.95f, 0.9f, 0.75f), 900f, "Rendered bear fat: it melts into hot oil, and with pepperfire keeps the cold out.", "white fat melting into the oil")
            .With("organic", 0.6f).With("fire", 0.1f).WithDensity(0.92f)
            .Dissolves(Water, 0.01f).Dissolves(Spirit, 0.2f).Dissolves(Oil, 1f).Dissolves(Turpentine, 1f),
        S(EmberResin, "Ember Resin", SubstanceRole.Raw, new Color(0.7f, 0.3f, 0.15f), 900f, "The smouldering resin of emberroot: only real heat - hot oil - wakes it.", "a dark red resin that smoulders")
            .With("fire", 0.4f).With("organic", 0.4f).WithDensity(1.1f)
            .Dissolves(Water, 0.1f).Dissolves(Spirit, 0.8f).Dissolves(Oil, 1f).Dissolves(Turpentine, 1f),
        S(HearthwardEssence, "Hearthward Essence", SubstanceRole.Active, new Color(0.85f, 0.45f, 0.3f), 260f, "A hearth inside you: cold cannot get in.", "a warm, rust-red liquid")
            .With("fire", 0.4f).With("stability", 0.4f).WithDensity(1f),
        S(GiantSpores, "Giant Spores", SubstanceRole.Raw, new Color(0.7f, 0.55f, 0.35f), 900f, "Spores of the giantcap. Warm spirit draws out a strength that is not your own.", "a thick brown cloud of spores")
            .With("organic", 0.6f).With("vitality", 0.2f).WithDensity(1.05f),
        S(TitanEssence, "Titan Essence", SubstanceRole.Active, new Color(0.8f, 0.6f, 0.25f), 220f, "The strength to carry what no one should.", "a thick ochre liquid")
            .With("vitality", 0.3f).With("fire", 0.2f).With("mineral", 0.2f).WithDensity(1.15f)
            .StableUpTo(110f, 0.03f, "the ochre turns muddy", (Sludge, 0.6f)),
        S(BarleyStarch, "Barley Starch", SubstanceRole.Raw, new Color(0.9f, 0.85f, 0.65f), 900f, "The starch of wild barley: boiled long, it thickens into a broth.", "a starchy cloudiness")
            .With("organic", 0.7f).WithDensity(1.2f),
        S(NourishingBroth, "Nourishing Broth", SubstanceRole.Active, new Color(0.85f, 0.7f, 0.45f), 105f, "A thick, filling broth: food and drink in one.", "a savoury, steaming broth")
            .With("vitality", 0.2f).With("organic", 0.5f).WithDensity(1.05f),
        S(Absinthin, "Absinthin", SubstanceRole.Raw, new Color(0.55f, 0.7f, 0.45f), 900f, "The bitter principle of wormwood: drawn out in acid, gentle heat turns it to antitoxin.", "an intense bitterness")
            .With("toxicity", 0.05f).With("mind", 0.1f).With("organic", 0.2f).WithDensity(1.1f).ExtractsBestAtPh(3.5f, 0.6f),
        S(PoppyLatex, "Poppy Latex", SubstanceRole.Raw, new Color(0.9f, 0.85f, 0.8f), 900f, "The milky latex of poppy pods: gentle warmth turns it into somnine.", "a milky, heavy-smelling trace")
            .With("mind", 0.4f).With("toxicity", 0.05f).WithDensity(1.1f),
        S(ChamomileOil, "Chamomile Oil", SubstanceRole.Raw, new Color(0.95f, 0.9f, 0.5f), 900f, "The gentle oil of chamomile: hot (not boiling) water turns it into serene essence.", "an apple-sweet scent")
            .With("mind", 0.3f).With("vitality", 0.1f).WithDensity(0.95f),
        S(SalamanderSlime, "Salamander Slime", SubstanceRole.Raw, new Color(0.9f, 0.55f, 0.3f), 900f, "The fireproof slime of salamander scales: cold water draws its gift out; heat ruins it.", "an orange, slippery slime")
            .With("fire", 0.2f).With("stability", 0.3f).WithDensity(1.05f)
            .StableUpTo(60f, 0.05f, "the slime turns to grey jelly", (Sludge, 0.8f)),
        S(NightshadeJuice, "Nightshade Juice", SubstanceRole.Raw, new Color(0.3f, 0.15f, 0.35f), 900f, "The deadly juice of nightshade berries: warm spirit concentrates it into venom.", "a dark purple juice")
            .With("toxicity", 0.7f).With("mind", 0.2f).With("organic", 0.3f).WithDensity(1.08f),
    };

    // ------------------------------------------------------------------ reactions
    private static List<AlchemyReaction> HerbalReactions() => new List<AlchemyReaction>
    {
        // Dragontongue: a second road to vigour (boiling) and a direct one to salamander essence (warm, with pepperfire).
        R("nectar_kindling", "Nectar Kindling", "Simmered hot, blaze nectar turns straight into ardent essence - vigour without cooking healing essence for hours.")
            .Consumes(BlazeNectar).Makes(ArdentEssence, 0.8f).Makes(Water, 0.2f)
            .Speed(0.05f, 90f, 20f).Between(76f, 135f)
            .Heat(2f).Looks(ReactionVisual.Bubbles, "the orange nectar darkens to amber and the vessel grows warm"),
        R("blaze_binding", "Blaze Binding", "Warm, blaze nectar takes pepperfire oil straight into salamander essence - no healing essence needed.")
            .Consumes(BlazeNectar).Consumes(PepperOil, 0.6f).Makes(SalamanderEssence, 1.4f)
            .Speed(0.12f, 60f, 0f).Between(40f, 76f)
            .Heat(1f).Looks(ReactionVisual.Glow, "orange and red swirl into one scarlet glow"),

        // More healing: nettles.
        R("nettle_steeping", "Nettle Steeping", "Steeped hot, nettle sap loses its sting and gives a healing essence - weaker than sunpetal's, and far cheaper.")
            .Consumes(NettleSap).Makes(VitalEssence, 0.7f).Makes(Water, 0.2f)
            .Speed(0.02f, 75f, 20f).Between(55f, 100f)
            .Looks(ReactionVisual.Glow, "the green blushes pink"),

        // More mana: the starlight lotus wakes by day.
        R("lotus_awakening", "Lotus Awakening", "Warmth wakes the magic in lotus nectar - by day as well as by night.")
            .Consumes(LotusNectar).Makes(ManaEssence, 0.85f)
            .Speed(0.03f, 45f, 20f).Between(20f, 62f).ModifiedBy(LeadDust, 0f, 0.02f)
            .Looks(ReactionVisual.Glow, "tiny lights rise through the liquid and it glows violet"),

        // Stoneskin: troll moss with iron, or granite taken up by tempered essence.
        R("stone_setting", "Stone Setting", "A pinch of iron sets troll sap into stoneskin essence.")
            .Consumes(TrollSap).Consumes(Iron, 0.15f).Makes(StoneskinEssence, 1f)
            .Speed(0.25f, 65f, 20f).Between(40f, 95f)
            .Looks(ReactionVisual.None, "the sap thickens and turns slate grey"),
        R("granite_tempering", "Granite Tempering", "Tempered essence takes up rock flour into stoneskin essence: sap → vital → ardent → tempered → stoneskin.")
            .Consumes(GraniteDust).Consumes(TemperedEssence).Makes(StoneskinEssence, 1.6f)
            .Speed(0.2f, 75f, 0f).Between(55f, 105f)
            .Looks(ReactionVisual.Hiss, "the grit hisses and dissolves into the steel-blue, which greys"),

        // Swiftness: harefoot clover warm, or windchime pollen in cool spirit.
        R("clover_quickening", "Clover Quickening", "Gentle heat quickens clover nectar into swift essence.")
            .Consumes(CloverNectar).Makes(SwiftEssence, 0.9f)
            .Speed(0.025f, 65f, 20f).Between(45f, 85f)
            .Looks(ReactionVisual.Bubbles, "quick little bubbles race to the surface"),
        R("gust_drawing", "Gust Drawing", "Cool spirit draws swiftness out of gust pollen (heat drives it off).")
            .Consumes(GustPollen).Consumes(Spirit, 0.3f, 0.5f).Makes(SwiftEssence, 0.8f).Makes(Spirit, 0.3f)
            .Speed(0.05f, 25f, 0f).Between(5f, 42f)
            .Looks(ReactionVisual.None, "the pollen vanishes and the spirit seems to stir by itself"),

        // Clarity: sage in spirit, or rose oil joining mana essence.
        R("sage_clearing", "Sage Clearing", "Warm, sage oil clears into clarity essence (it wants spirit to dissolve in).")
            .Consumes(SageOil).Makes(ClarityEssence, 0.85f)
            .Speed(0.03f, 55f, 20f).Between(35f, 82f)
            .Looks(ReactionVisual.None, "the liquid turns perfectly clear"),
        R("rose_clarity", "Rose Clarity", "Rose oil gentles mana essence into clarity essence: moon dew → mana → clarity.")
            .Consumes(RoseOil).Consumes(ManaEssence).Makes(ClarityEssence, 2.6f)
            .Speed(0.2f, 45f, 0f).Between(20f, 70f)
            .Looks(ReactionVisual.Glow, "the violet hum clears to a pale, steady blue"),

        // Hearthward: tallow and pepperfire in hot oil, or emberroot resin in hotter oil.
        R("hearth_rendering", "Hearth Rendering", "In hot oil, bear tallow takes pepperfire into hearthward essence.")
            .Consumes(BearTallow).Consumes(PepperOil, 0.5f).Makes(HearthwardEssence, 1.3f)
            .Speed(0.1f, 110f, 20f).Between(85f, 170f)
            .Heat(1f).Looks(ReactionVisual.Bubbles, "the fat sizzles and turns rust-red"),
        R("resin_warming", "Resin Warming", "Far above boiling - in oil - ember resin wakes into hearthward essence.")
            .Consumes(EmberResin).Makes(HearthwardEssence, 0.8f)
            .Speed(0.02f, 125f, 20f).Between(105f, 190f)
            .Looks(ReactionVisual.Smoke, "a resinous smoke and a deep red colour"),

        // Titan: giantcap in warm spirit, or stoneskin essence joined to ardent essence.
        R("giant_steeping", "Giant Steeping", "Warm spirit draws a giant's strength out of the spores.")
            .Consumes(GiantSpores).Consumes(Spirit, 0.5f, 0.5f).Makes(TitanEssence, 0.8f).Makes(Spirit, 0.5f)
            .Speed(0.03f, 50f, 20f).Between(30f, 76f)
            .Looks(ReactionVisual.None, "the spores swell and the spirit turns ochre"),
        R("titan_union", "Titan Union", "Hot, stoneskin essence and ardent essence join into titan essence.")
            .Consumes(StoneskinEssence).Consumes(ArdentEssence).Makes(TitanEssence, 1.6f)
            .Speed(0.15f, 85f, 0f).Between(76f, 105f)
            .Heat(2f).Looks(ReactionVisual.Glow, "the grey and the amber fuse into gold-brown"),

        // Broth: barley boiled long, or milk and honey kept hot.
        R("broth_boiling", "Broth Boiling", "Boiled long, barley starch thickens into a nourishing broth.")
            .Consumes(BarleyStarch).Makes(NourishingBroth, 0.9f)
            .Speed(0.02f, 98f, 15f).Between(85f, 130f)
            .Looks(ReactionVisual.Bubbles, "it thickens and smells of bread"),
        R("milk_broth", "Milk Broth", "Milk curd and sugars kept hot become a sweet, filling broth.")
            .Consumes(Casein).Consumes(Sugars, 1f).Makes(NourishingBroth, 2f)
            .Speed(0.06f, 80f, 20f).Between(65f, 98f)
            .Looks(ReactionVisual.None, "a skin forms and it smells of warm milk and honey"),

        // Second roads to existing potions.
        R("wormwood_tempering", "Wormwood Tempering", "In acid and gentle heat, absinthin turns into antitoxin - an antidote without snakeweed.")
            .Consumes(Absinthin).Makes(Antitoxin, 0.7f)
            .Speed(0.02f, 65f, 20f).Between(50f, 85f)
            .Needs(new VariableCondition(AlchemyVariables.Ph, -1f, 5.5f))
            .Looks(ReactionVisual.Glow, "a faint green glow, and the bitterness fades"),
        R("poppy_steeping", "Poppy Steeping", "Gently warm, poppy latex becomes somnine - a sleep draught without dreamshade (keep it under 75 °C).")
            .Consumes(PoppyLatex).Makes(Somnine, 0.7f)
            .Speed(0.025f, 55f, 20f).Between(35f, 72f)
            .Looks(ReactionVisual.None, "a dusky violet tint creeps in"),
        R("chamomile_steeping", "Chamomile Steeping", "Steeped hot - not boiling - chamomile oil becomes serene essence: calm without frost oil or mana.")
            .Consumes(ChamomileOil).Makes(SereneEssence, 0.7f)
            .Speed(0.025f, 70f, 20f).Between(55f, 80f)
            .Looks(ReactionVisual.Glow, "the liquid turns a calm teal"),
        R("slime_cooling", "Slime Cooling", "Cold water draws fireward essence out of salamander slime - the fire ward without boiling ash for hours.")
            .Consumes(SalamanderSlime).Makes(FirewardEssence, 0.8f)
            .Speed(0.03f, 20f, 0f).Between(0f, 40f)
            .Looks(ReactionVisual.None, "a smoky copper colour spreads through the cold water"),
        R("nightshade_drawing", "Nightshade Drawing", "Warm spirit concentrates nightshade juice into venom - a third road to it.")
            .Consumes(NightshadeJuice).Consumes(Spirit, 0.5f, 0.5f).Makes(Venom, 0.8f).Makes(Spirit, 0.5f)
            .Speed(0.05f, 50f, 25f).Between(25f, 80f)
            .Looks(ReactionVisual.Bubbles, "a green film gathers on the purple"),
    };

    // ------------------------------------------------------------------ potions
    private static void AddHerbalOutcomes(List<AlchemyOutcome> list)
    {
        list.Add(Potion("stoneskin_elixir", "Stoneskin Elixir", "Skin like stone: far less damage taken for a minute, and a little slower.", 60, StoneskinEssence, 0.25f, 50f)
            .Requires(new DominantCondition(StoneskinEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(StoneskinEssence), 0.03f))
            .Fx(ConsumableEffectType.HpDamageFactor, -30f, 60f, 1f).Fx(ConsumableEffectType.SpeedFactor, -10f, 60f, 1f));
        list.Add(Potion("swiftness_draught", "Swiftness Draught", "Light feet for forty seconds, and the breath keeps up.", 60, SwiftEssence, 0.25f, 35f)
            .Requires(new DominantCondition(SwiftEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(SwiftEssence), 0.03f))
            .Fx(ConsumableEffectType.SpeedFactor, 25f, 40f, 1f).Fx(ConsumableEffectType.StaminaRegeneration, 2f, 40f, 1f));
        list.Add(Potion("clarity_elixir", "Elixir of Clarity", "A clear head: mana restored and flowing back, and fear slides off.", 60, ClarityEssence, 0.25f, 45f)
            .Requires(new DominantCondition(ClarityEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(ClarityEssence), 0.03f))
            .Fx(ConsumableEffectType.Mana, 20f).Fx(ConsumableEffectType.ManaRegeneration, 3f, 60f, 1f).Fx(ConsumableEffectType.Sanity, 15f));
        list.Add(Potion("hearthward_tonic", "Hearthward Tonic", "Warm to the bone for two minutes: the cold cannot get in.", 60, HearthwardEssence, 0.2f, 35f)
            .Requires(new DominantCondition(HearthwardEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(HearthwardEssence), 0.03f))
            .Fx(ConsumableEffectType.BodyHeat, 15f).Fx(ConsumableEffectType.BodyHeat, 1f, 120f, 1f).Fx(ConsumableEffectType.Stamina, 10f));
        list.Add(Potion("titan_draught", "Titan's Draught", "Carry far more, and tire later, for two minutes.", 60, TitanEssence, 0.25f, 55f)
            .Requires(new DominantCondition(TitanEssence)).Requires(new SubstanceCondition(SubstanceSelector.Of(TitanEssence), 0.03f))
            .Fx(ConsumableEffectType.MaxWeight, 30f, 120f, 1f).Fx(ConsumableEffectType.MaxStamina, 25f, 120f, 1f).Fx(ConsumableEffectType.Stamina, 30f));
        list.Add(Potion("travellers_broth", "Traveller's Broth", "A hot, thick broth: a meal and a drink, and the legs keep going.", 58, NourishingBroth, 0.3f, 10f)
            .Requires(new DominantCondition(NourishingBroth)).Requires(new SubstanceCondition(SubstanceSelector.Of(NourishingBroth), 0.03f))
            .Fx(ConsumableEffectType.Food, 35f).Fx(ConsumableEffectType.Drink, 20f).Fx(ConsumableEffectType.StaminaRegeneration, 1f, 60f, 1f));
    }

    // ------------------------------------------------------------------ ingredients
    private static List<IngredientPreset> HerbalIngredients() => new List<IngredientPreset>
    {
        I("Dragontongue", "A flame-shaped orange flower. Simmer it hot (about 80 °C) for vigour; warm it with fire pepper for salamander essence.", MaterialKind.Herb, 5f, 0.05f, new Color(1f, 0.45f, 0.1f), 20f, 25f,
            "the petals curl as if burning", Whole, (BlazeNectar, 3f, 0.8f), (PlantFibre, 0.4f, 0.3f), (Water, 0.3f, 1f)).Hard(0.2f),
        I("Stinging Nettle", "A common weed that stings. Steeped hot, it heals - a poor man's sunpetal.", MaterialKind.Herb, 1f, 0.05f, new Color(0.3f, 0.55f, 0.2f), 25f, 18f,
            "the leaves wilt and darken", Whole, (NettleSap, 3f, 0.7f), (PlantFibre, 0.6f, 0.3f), (Tannin, 0.2f, 0.3f)).Hard(0.15f),
        I("Starlight Lotus", "A water lily with star-flecked petals. Its magic wakes by day as well as by night.", MaterialKind.Herb, 8f, 0.05f, new Color(0.7f, 0.75f, 1f), 12f, 15f,
            "the petals sparkle as they dissolve", Whole, (LotusNectar, 3.5f, 0.85f), (Water, 0.5f, 1f)),
        I("Troll Moss", "A grey moss from under bridges. With a pinch of iron filings it makes a stoneskin elixir.", MaterialKind.Herb, 4f, 0.05f, new Color(0.45f, 0.5f, 0.38f), 25f, 15f,
            "the moss sinks heavily", Whole, (TrollSap, 3f, 0.6f), (PlantFibre, 0.4f, 0.3f)).Hard(0.25f),
        I("Granite Dust", "Rock flour from a quarry. Tempered essence takes it up into stoneskin.", MaterialKind.Stone, 1f, 0.2f, new Color(0.6f, 0.58f, 0.56f), 3f, 20f,
            "grey grit sinks to the bottom", Ground, (GraniteDust, 2f, 1f)),
        I("Harefoot Clover", "A four-leafed clover the hares love. Warm it gently for a swiftness draught.", MaterialKind.Herb, 3f, 0.02f, new Color(0.55f, 0.8f, 0.3f), 15f, 20f,
            "a sweet green smell", Whole, (CloverNectar, 2.5f, 0.8f), (PlantFibre, 0.3f, 0.3f), (Water, 0.3f, 1f)).Hard(0.1f),
        I("Windchime Seeds", "Papery seeds that rattle in the wind. Their pollen wants cool spirit - not heat.", MaterialKind.Herb, 4f, 0.02f, new Color(0.9f, 0.92f, 0.85f), 20f, 18f,
            "the seeds rattle and a pale pollen floats", Whole, (GustPollen, 2f, 0.5f), (PlantFibre, 0.3f, 0.3f)).Hard(0.4f),
        I("Sage Leaves", "Grey-green leaves with a clean, sharp oil that clears the head. Steep them in spirit.", MaterialKind.Herb, 3f, 0.02f, new Color(0.6f, 0.68f, 0.55f), 20f, 20f,
            "a clean herbal scent", Whole, (SageOil, 0.8f, 0.5f), (Tannin, 0.3f, 0.3f), (PlantFibre, 0.4f, 0.3f)).Hard(0.15f),
        I("Bear Tallow", "A slab of rendered bear fat. In hot oil with fire pepper it keeps the cold out.", MaterialKind.MonsterPart, 6f, 0.2f, new Color(0.95f, 0.9f, 0.75f), 30f, 15f,
            "the fat slides in and softens", Whole, (BearTallow, 3f, 1f)).Hard(0.2f),
        I("Emberroot", "A dark red root that smoulders when cut. Its resin wakes only in hot oil.", MaterialKind.Herb, 6f, 0.1f, new Color(0.6f, 0.25f, 0.12f), 40f, 30f,
            "a thin smoke curls from the root", Whole, (EmberResin, 2f, 0.5f), (PlantFibre, 0.6f, 0.3f)).Hard(0.6f),
        I("Giantcap", "A mushroom as big as a shield. Warm spirit draws a giant's strength out of its spores.", MaterialKind.Herb, 7f, 0.2f, new Color(0.7f, 0.55f, 0.35f), 30f, 18f,
            "a cloud of brown spores", Whole, (GiantSpores, 2.5f, 0.6f), (FungalPulp, 1f, 0.5f)).Hard(0.2f),
        I("Wild Barley", "A handful of wild grain. Boiled long it makes a filling broth.", MaterialKind.Herb, 1f, 0.1f, new Color(0.85f, 0.78f, 0.5f), 40f, 20f,
            "the grains swell", Whole, (BarleyStarch, 3f, 0.7f), (PlantFibre, 0.6f, 0.3f)).Hard(0.3f),
        I("Wormwood", "A silvery, bitter herb. In acid, gently warm, it gives an antitoxin.", MaterialKind.Herb, 4f, 0.03f, new Color(0.65f, 0.72f, 0.6f), 30f, 20f,
            "an intense bitterness rises", Whole, (Absinthin, 1f, 0.25f), (Tannin, 0.4f, 0.5f), (PlantFibre, 0.8f, 0.3f)).Hard(0.4f),
        I("Poppy Pod", "A ripe seed head. Its milky latex, gently warmed, brings sleep.", MaterialKind.Herb, 6f, 0.02f, new Color(0.7f, 0.65f, 0.5f), 25f, 20f,
            "a milky trace seeps out", Whole, (PoppyLatex, 0.8f, 0.4f), (PlantFibre, 0.4f, 0.3f)).Hard(0.3f),
        I("Chamomile", "Small daisy-like flowers that smell of apples. Steeped hot, they calm.", MaterialKind.Herb, 2f, 0.02f, new Color(0.95f, 0.92f, 0.6f), 15f, 20f,
            "an apple-sweet scent", Whole, (ChamomileOil, 1.2f, 0.7f), (PlantFibre, 0.3f, 0.3f), (Water, 0.3f, 1f)).Hard(0.1f),
        I("Salamander Scale", "A scale from a fire salamander. Steep it cold: heat ruins its slime.", MaterialKind.MonsterPart, 15f, 0.05f, new Color(0.9f, 0.5f, 0.25f), 30f, 25f,
            "the scale slicks the water orange", Whole, (SalamanderSlime, 2f, 0.7f)).Hard(0.5f),
        I("Nightshade", "Glossy black berries, deadly. Warm spirit makes venom of them.", MaterialKind.Herb, 4f, 0.02f, new Color(0.2f, 0.1f, 0.25f), 15f, 18f,
            "the berries burst purple", Whole, (NightshadeJuice, 1.5f, 0.7f), (Sugars, 0.4f, 0.9f), (Water, 0.8f, 1f)).Hard(0.15f),
    };
}
