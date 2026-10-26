using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What an element is to the smith. Serialized as numbers: only add new values at the END.</summary>
public enum CraftElementKind
{
    /// <summary>A metal that can be the base of an alloy (iron, copper, silver...).</summary>
    Metal,
    /// <summary>Something added to a metal in small amounts that changes it (carbon, manganese, nickel).</summary>
    Alloying,
    /// <summary>Something that spoils a metal (sulfur, phosphorus).</summary>
    Impurity,
    /// <summary>A metal of legend (mythril, star iron, orichalcum).</summary>
    Fantasy,
}

/// <summary>A property an alloy effect changes. Serialized as numbers: only add new values at the END.</summary>
public enum CraftProperty
{
    /// <summary>Annealed hardness (HV).</summary>
    Hardness,
    /// <summary>Toughness (J): resistance to cracking and chipping.</summary>
    Toughness,
    /// <summary>Ductility (% elongation): how far it bends and stretches before it breaks.</summary>
    Ductility,
    /// <summary>Resistance to corrosion (0-1).</summary>
    Corrosion,
    /// <summary>Melting point (°C).</summary>
    MeltingPoint,
    /// <summary>How deeply and easily a quench hardens it (ferrous metals: × the critical cooling rate).</summary>
    Hardenability,
    /// <summary>How well it carries magic (0-1).</summary>
    Magic,
    /// <summary>How well it fills a mould (0-1).</summary>
    Castability,
    /// <summary>Cracks when worked hot (sulfur: red shortness).</summary>
    HotShortness,
    /// <summary>Brittle when cold (phosphorus: cold shortness).</summary>
    ColdShortness,
    /// <summary>Pulls the colour toward the effect's tint.</summary>
    Tint,
    /// <summary>Resistance to the supernatural (0-1): spirits, the undead.</summary>
    Supernatural,
    /// <summary>How fast it hardens as it is worked cold (HV per unit of cold work).</summary>
    WorkHardening,
    /// <summary>How much harder than its base a quench can make it (non-ferrous precipitation, fantasy metals): HV.</summary>
    QuenchHardness,
}

/// <summary>
/// One constituent of metals and ores: iron, carbon, copper, tin, sulfur, mythril... An element has the properties a
/// metal made mostly of it has; <see cref="CraftAlloyEffect"/>s say what it does in small amounts to another base.
/// Metals are never identified by the names of what went in: their actual composition decides what they are
/// (<see cref="CraftMaterialClass"/>) and how they behave.
/// </summary>
[Serializable]
public class CraftElement
{
    [Tooltip("Stable id used by ores, effects, classes, saves and the journal (lower case: \"iron\").")]
    public string id = "";
    public string displayName = "";
    [Tooltip("Short symbol shown in an assay (Fe, Cu...).")]
    public string symbol = "";
    [TextArea(1, 3)] public string description = "";
    public CraftElementKind kind = CraftElementKind.Metal;
    [Tooltip("Colour of the clean metal.")]
    public Color color = new Color(0.6f, 0.6f, 0.62f);
    [Tooltip("g/cm³.")]
    [Min(0.1f)] public float density = 7.8f;
    [Tooltip("°C.")]
    public float meltingPoint = 1500f;
    [Tooltip("Boils (or burns) away above this (°C): zinc at 907. 4000 = never in a forge.")]
    public float boilingPoint = 4000f;
    [Tooltip("Hardness of the pure, annealed metal (HV).")]
    [Min(1f)] public float hardness = 80f;
    [Tooltip("Toughness of the pure metal (J).")]
    [Min(0f)] public float toughness = 60f;
    [Tooltip("Ductility of the pure metal (% elongation).")]
    [Min(0f)] public float ductility = 40f;
    [Tooltip("Resistance to corrosion (0 rusts at once - 1 never tarnishes).")]
    [Range(0f, 1f)] public float corrosion = 0.3f;
    [Tooltip("How well it fills a mould (0-1).")]
    [Range(0f, 1f)] public float castability = 0.5f;
    [Tooltip("Scale starts to form above this (°C). 4000 = it never scales (gold).")]
    public float oxidationOnset = 570f;
    [Tooltip("How fast it scales once hot (1 = iron).")]
    [Min(0f)] public float oxidationRate = 1f;
    [Tooltip("Cold work goes (it anneals, recrystallises) above this (°C).")]
    public float recrystallization = 550f;
    [Tooltip("The forging window: below Hot Work Min it is worked cold (it hardens, then cracks); above Hot Work Max it starts to burn.")]
    public float hotWorkMin = 800f;
    public float hotWorkMax = 1250f;
    [Tooltip("Above this it burns (grain boundaries melt): irreversible.")]
    public float burnTemperature = 1350f;
    [Tooltip("Grain grows when held above this (°C): coarse grain is weak.")]
    public float grainGrowth = 1000f;
    [Tooltip("HV gained per unit of cold work (bronze and copper harden a lot as they are hammered cold).")]
    [Min(0f)] public float workHardening = 60f;
    [Tooltip("Pulled by a magnet (iron, nickel) - until it is above its Curie temperature.")]
    public bool magnetic;
    public float curieTemperature = 770f;
    [Tooltip("How easily it goes into the slag when oxidised (0 noble - 1 base): what refining and cupellation remove.")]
    [Range(0f, 1f)] public float slagAffinity = 0.5f;
    [Tooltip("How well it carries magic (0-1).")]
    [Range(0f, 1f)] public float magic;
    [Tooltip("An element it lends affinity to (resistances, weapon element).")]
    public ElementType affinity = ElementType.None;
    [Range(0f, 1f)] public float affinityStrength;
    [Tooltip("Resistance to the supernatural it lends (0-1).")]
    [Range(0f, 1f)] public float supernatural;
    [Tooltip("Value of a gram.")]
    [Min(0f)] public float pricePerGram = 0.01f;
    [Tooltip("What the player notices about it before identifying it.")]
    public string observation = "";
    [Tooltip("Known from the start (iron, copper): never shown as unknown.")]
    public bool alwaysKnown;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    public bool Scales => oxidationOnset < 3999f;

    /// <summary>The element's own value of a property (its alloys start from it).</summary>
    public float BaseValue(CraftProperty p)
    {
        switch (p)
        {
            case CraftProperty.Hardness: return hardness;
            case CraftProperty.Toughness: return toughness;
            case CraftProperty.Ductility: return ductility;
            case CraftProperty.Corrosion: return corrosion;
            case CraftProperty.MeltingPoint: return meltingPoint;
            case CraftProperty.Magic: return magic;
            case CraftProperty.Castability: return castability;
            case CraftProperty.Supernatural: return supernatural;
            case CraftProperty.WorkHardening: return workHardening;
            case CraftProperty.Hardenability: return 1f;
            default: return 0f;
        }
    }
}

/// <summary>
/// What an element does to a metal it is alloyed into: "carbon in iron: hardenability rises to a peak at 0.8 %",
/// "tin in copper: hardness +130 HV at 12 %, falling and brittle above 20 %". The added value follows the fraction:
/// it rises smoothly to <see cref="peakValue"/> at <see cref="peakAt"/>, then changes by <see cref="beyondSlope"/> per
/// unit of fraction beyond it. Alloys are made of these: no recipe names them.
/// </summary>
[Serializable]
public class CraftAlloyEffect
{
    [Tooltip("The base metal this applies to (\"iron\"); empty = any base.")]
    public string baseElement = "";
    [Tooltip("The element alloyed in.")]
    public string element = "";
    public CraftProperty property = CraftProperty.Hardness;
    [Tooltip("The fraction (0-1, by mass) where the effect is strongest.")]
    [Range(0.00001f, 1f)] public float peakAt = 0.1f;
    [Tooltip("What it adds at its peak (in the property's units: HV, J, %, °C...).")]
    public float peakValue = 50f;
    [Tooltip("Change per unit of fraction beyond the peak (negative: it falls off; 0: it holds).")]
    public float beyondSlope;
    [Tooltip("Tint: the colour it pulls the metal toward.")]
    public Color tint = Color.white;
    [Tooltip("Why (shown once the player understands the alloy).")]
    public string note = "";

    public CraftAlloyEffect() { }

    public CraftAlloyEffect(string baseElement, string element, CraftProperty property, float peakAt, float peakValue, float beyondSlope = 0f, string note = "")
    {
        this.baseElement = baseElement ?? "";
        this.element = element;
        this.property = property;
        this.peakAt = peakAt;
        this.peakValue = peakValue;
        this.beyondSlope = beyondSlope;
        this.note = note ?? "";
    }

    /// <summary>The value it adds at <paramref name="fraction"/>.</summary>
    public double At(double fraction)
    {
        if (fraction <= 0)
            return 0;
        double p = Math.Max(1e-6, peakAt);
        if (fraction <= p)
        {
            double t = fraction / p;
            return peakValue * t * (2 - t); // rises fast, flattens toward the peak
        }
        return peakValue + beyondSlope * (fraction - p);
    }
}

/// <summary>A fraction range of one element (classes, ore yields).</summary>
[Serializable]
public class CraftElementRange
{
    public string element = "";
    [Range(0f, 1f)] public float min;
    [Range(0f, 1f)] public float max = 1f;

    public CraftElementRange() { }

    public CraftElementRange(string element, float min, float max)
    {
        this.element = element;
        this.min = min;
        this.max = max;
    }

    public bool Contains(double fraction) => fraction >= min - 1e-9 && fraction <= max + 1e-9;
}

/// <summary>
/// What a metal is called by its composition: wrought iron, mild steel, high-carbon steel, cast iron, bronze, brass,
/// sterling silver, electrum, mythril steel... The first class (by priority) whose ranges the composition falls into
/// names it. Players learn the names by assaying or by working a metal enough.
/// </summary>
[Serializable]
public class CraftMaterialClass
{
    public string id = "";
    public string displayName = "";
    [TextArea(1, 3)] public string description = "";
    [Tooltip("What it looks like before the player knows its name (\"a grey metal that sparks in long yellow streaks\").")]
    public string observation = "";
    [Tooltip("The base element (the most abundant).")]
    public string baseElement = "";
    [Tooltip("Ranges the other elements must be in (by mass fraction).")]
    public List<CraftElementRange> ranges = new List<CraftElementRange>();
    [Tooltip("Classes are tried from the highest priority down.")]
    public int priority;
    [Tooltip("Value of the metal × this.")]
    [Min(0f)] public float priceMultiplier = 1f;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    public CraftMaterialClass With(string element, float min, float max)
    {
        ranges.Add(new CraftElementRange(element, min, max));
        return this;
    }
}

/// <summary>The rock an ore comes in. Serialized as numbers: add at the END.</summary>
public enum CraftGangue
{
    /// <summary>Quartz: hard, light; in the furnace it takes iron into the slag unless fluxed with limestone.</summary>
    Quartz,
    /// <summary>Clay: soft and sticky: it washes out.</summary>
    Clay,
    /// <summary>Limestone: it fluxes the slag by itself.</summary>
    Limestone,
}

/// <summary>What one gram of an ore's mineral gives (a range: every deposit is different).</summary>
[Serializable]
public class CraftOreYield
{
    public string element = "";
    [Tooltip("Grams of the element per gram of mineral (the rest is oxygen, sulfur bound as gas...).")]
    [Range(0f, 1f)] public float min;
    [Range(0f, 1f)] public float max;

    public CraftOreYield() { }

    public CraftOreYield(string element, float min, float max)
    {
        this.element = element;
        this.min = min;
        this.max = max;
    }
}

/// <summary>
/// A kind of ore and its deposits: what its mineral holds (each deposit within the ranges), how much loose rock and
/// dirt comes with it, what it looks like, and how it smelts. Two deposits of the same ore give different metal.
/// Cleaning removes the loose rock and dirt, never what is bound in the mineral (only smelting separates that).
/// </summary>
[Serializable]
public class CraftOreDefinition
{
    public string id = "";
    public string displayName = "";
    [TextArea(1, 3)] public string description = "";
    [Tooltip("What it looks like before it is identified (\"a heavy, rust-red rock\").")]
    public string observation = "";
    [Tooltip("Colour of the mineral.")]
    public Color color = new Color(0.55f, 0.25f, 0.2f);
    [Tooltip("Lustre: \"earthy\", \"metallic\", \"glassy\"...")]
    public string luster = "earthy";
    [Tooltip("The colour of its streak on an unglazed tile.")]
    public string streak = "red-brown";
    public Color streakColor = new Color(0.55f, 0.25f, 0.2f);
    public bool magnetic;
    [Tooltip("Density of the mineral (g/cm³): heavy minerals stay in the pan, light rock washes away.")]
    [Min(0.5f)] public float density = 5f;
    [Tooltip("Mohs hardness of the mineral (scratch test).")]
    [Range(1f, 10f)] public float hardness = 5.5f;
    [Tooltip("What a gram of the mineral gives (by element).")]
    public List<CraftOreYield> yields = new List<CraftOreYield>();
    [Tooltip("Share of a raw lump that is mineral (the rest is rock and dirt).")]
    [Range(0f, 1f)] public float mineralMin = 0.35f;
    [Range(0f, 1f)] public float mineralMax = 0.7f;
    [Tooltip("Share of a raw lump that is dirt and clay (washes out).")]
    [Range(0f, 1f)] public float dirtMin = 0.03f;
    [Range(0f, 1f)] public float dirtMax = 0.12f;
    public CraftGangue gangue = CraftGangue.Quartz;
    public Color gangueColor = new Color(0.72f, 0.72f, 0.7f);
    [Tooltip("A native metal (gold, native silver, a meteorite): already metallic - it only needs melting or forging.")]
    public bool native;
    [Tooltip("A sulfide: smelted unroasted it gives a brittle matte full of sulfur. Roast it first.")]
    public bool sulfide;
    [Tooltip("Roasting temperature (°C, an oxidising fire): drives off the sulfur.")]
    public float roastTemperature = 700f;
    [Tooltip("The metal starts to come out of the mineral above this (°C, a reducing fire).")]
    public float reductionTemperature = 1100f;
    [Tooltip("How fast it reduces once hot enough (1 = hematite).")]
    [Min(0f)] public float reductionRate = 1f;
    [Tooltip("Mass of a raw lump (g).")]
    [Min(1f)] public float lumpMass = 1000f;
    [Tooltip("Value of a lump.")]
    [Min(0f)] public float price = 2f;
    [Tooltip("Mythril-like ores: smelting needs this flux id (\"arcane_flux\"); empty = none.")]
    public string needsFlux = "";

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

    public CraftOreDefinition Yields(string element, float min, float max)
    {
        yields.Add(new CraftOreYield(element, min, max));
        return this;
    }
}

/// <summary>A fuel for the forge or the furnace.</summary>
[Serializable]
public class CraftFuel
{
    public string id = "";
    public string displayName = "";
    [Tooltip("The item it uses up (its name, asset name or id). Empty = a free woodpile/coal heap at stations that have one.")]
    public string item = "";
    [Tooltip("Burn units one item adds.")]
    [Min(0f)] public float energy = 1f;
    [Tooltip("The hottest the fire gets on it, bellows working (°C).")]
    public float maxTemperature = 1350f;
    [Tooltip("How fast it burns (1 = charcoal).")]
    [Min(0.01f)] public float burnRate = 1f;
    [Tooltip("How much it keeps the fire reducing (it eats the oxygen that scales the metal), 0-1.")]
    [Range(0f, 1f)] public float reducing = 0.6f;
    [Tooltip("Carbon it gives hot iron left long in it (case hardening), 0-1.")]
    [Range(0f, 1f)] public float carburizing = 0.5f;
    [Tooltip("Sulfur it gives hot iron (coal): makes it red-short.")]
    [Range(0f, 1f)] public float sulfur;
    [Tooltip("Smoke and soot (visual).")]
    [Range(0f, 1f)] public float smoke = 0.2f;
    [Tooltip("Any wood burns as this fuel too: wooden blanks, and plain materials named after a wood or as wood (Oak Plank, Ash Log, Timber, Branch...). Never a finished item.")]
    public bool burnsAnyWood;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>
/// A quenching medium: how hard it pulls heat out, how it behaves while the piece is hot (a vapour jacket that slows
/// the start unless the piece is moved), how gentle it is at the low end (oil: less cracking), whether it burns.
/// </summary>
[Serializable]
public class CraftQuenchant
{
    public string id = "";
    public string displayName = "";
    [Tooltip("The item a tank of it uses up when filled (empty = free: water from the well).")]
    public string item = "";
    [Tooltip("Cooling severity (still water = 1, brine 1.6, oil 0.35, air 0.02).")]
    [Min(0f)] public float severity = 1f;
    [Tooltip("How much a vapour jacket slows the cooling above 400 °C while the piece is held still (0-1).")]
    [Range(0f, 1f)] public float vaporBlanket = 0.5f;
    [Tooltip("Severity × this below 300 °C (oil and frostwater are gentle where cracks start).")]
    [Range(0f, 2f)] public float lowTemperatureFactor = 1f;
    [Tooltip("°C: the medium boils (its own temperature cannot rise above it).")]
    public float boilingPoint = 100f;
    [Tooltip("Oils flare above this (°C of the oil, or a hot piece held half in): 0 = it never burns.")]
    public float flashPoint;
    [Tooltip("How much heat the tank absorbs per degree (more = it warms less).")]
    [Min(0.01f)] public float heatCapacity = 1f;
    public ElementType affinity = ElementType.None;
    [Tooltip("Affinity it gives a hardened blade (dragon's blood: fire).")]
    [Range(0f, 1f)] public float affinityStrength;
    [Tooltip("What the player sees.")]
    public string observation = "";

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>What a vat can hold: water, lime, tanning liquors, alum, oil, an alchemical essence, a pickle.</summary>
[Serializable]
public class CraftTanningAgent
{
    public string id = "";
    public string displayName = "";
    public CraftAgentKind kind = CraftAgentKind.Vegetable;
    [Tooltip("The item one dose uses up (empty = free: water).")]
    public string item = "";
    [Tooltip("Strength one dose adds to a vat (concentration 0-1).")]
    [Range(0f, 1f)] public float dose = 0.2f;
    [Tooltip("How fast it penetrates (tans a 4 mm hide through in this many hours at the best temperature and full strength).")]
    [Min(0.1f)] public float hoursThrough = 36f;
    [Tooltip("Too strong too early tans the outside first and closes it: the core stays raw (case hardening). Strength above this risks it.")]
    [Range(0f, 1f)] public float caseHardeningAbove = 0.6f;
    [Tooltip("How much of the tannage stays when wetted (alum washes out: 0.3).")]
    [Range(0f, 1f)] public float fixation = 1f;
    [Tooltip("What the finished leather is like: stiffness 0 limp - 1 rigid; water resistance 0-1.")]
    [Range(0f, 1f)] public float stiffness = 0.5f;
    [Range(0f, 1f)] public float waterResistance = 0.4f;
    [Tooltip("Shrink temperature of the leather once tanned (°C, wet): raw hide 60.")]
    public float shrinkTemperature = 75f;
    [Tooltip("Best working temperature (°C) and the most the hide stands in it.")]
    public float bestTemperature = 20f;
    public float maxTemperature = 35f;
    [Tooltip("Colour it gives.")]
    public Color color = new Color(0.55f, 0.36f, 0.22f);
    [Tooltip("Durability of the leather × this.")]
    [Min(0f)] public float durability = 1f;
    [Tooltip("Fire resistance it gives (alchemical essences).")]
    [Range(0f, 1f)] public float fireResistance;
    [Tooltip("Lime: hours to loosen the hair (at 20 °C). Acid: scale it eats per hour.")]
    [Min(0f)] public float actionHours = 24f;
    public string observation = "";

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>An animal's hide: its size, how thick it is where, its fibre, hair and fat, its flaws.</summary>
[Serializable]
public class CraftHideDefinition
{
    public string id = "";
    public string displayName = "";
    [TextArea(1, 3)] public string description = "";
    public string observation = "";
    [Tooltip("Size in cells (each cell is the settings' Cell Size, 10 cm by default).")]
    [Min(1)] public int width = 12;
    [Min(1)] public int height = 9;
    [Tooltip("Thickness along the back and at the belly (mm).")]
    [Min(0.2f)] public float thicknessBack = 5f;
    [Min(0.2f)] public float thicknessBelly = 3f;
    [Tooltip("Fibre strength (1 = cattle): tear resistance, durability.")]
    [Min(0.1f)] public float fiberStrength = 1f;
    [Tooltip("Natural suppleness (0-1).")]
    [Range(0f, 1f)] public float suppleness = 0.4f;
    [Tooltip("Hair (0 bare - 1 thick fur).")]
    [Range(0f, 1f)] public float hair = 0.5f;
    [Tooltip("Fat and flesh left on it (0-1).")]
    [Range(0f, 1f)] public float fat = 0.5f;
    [Tooltip("Chance a cell has a scar or a hole.")]
    [Range(0f, 0.5f)] public float flaws = 0.05f;
    public Color color = new Color(0.62f, 0.5f, 0.42f);
    public Color hairColor = new Color(0.35f, 0.25f, 0.18f);
    public ElementType affinity = ElementType.None;
    [Range(0f, 1f)] public float affinityStrength;
    [Min(0f)] public float price = 6f;
    [Tooltip("Woven cloth (linen, wool, canvas) rather than a hide: it comes finished - no fleshing or tanning - and is cut and sewn at the tailor's table.")]
    public bool textile;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>A wood for grips, hafts, shafts and boards.</summary>
[Serializable]
public class CraftWoodDefinition
{
    public string id = "";
    public string displayName = "";
    [TextArea(1, 3)] public string description = "";
    public string observation = "";
    [Tooltip("g/cm³.")]
    [Min(0.1f)] public float density = 0.7f;
    [Tooltip("Hardness (0-1).")]
    [Range(0f, 1f)] public float hardness = 0.5f;
    [Tooltip("Shock resistance (0-1): hafts need it.")]
    [Range(0f, 1f)] public float toughness = 0.6f;
    [Tooltip("How much it flexes (0-1).")]
    [Range(0f, 1f)] public float flexibility = 0.4f;
    public Color color = new Color(0.68f, 0.52f, 0.34f);
    [Tooltip("The blank one item gives (mm).")]
    public float blankLength = 1000f;
    public float blankWidth = 40f;
    public float blankThickness = 40f;
    [Min(0f)] public float price = 1f;
    public ElementType affinity = ElementType.None;
    [Range(0f, 1f)] public float affinityStrength;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>A consumable supply: fluxes, thread, padding, wax, oils, dyes, clay, cupels.</summary>
[Serializable]
public class CraftSupplyDefinition
{
    public string id = "";
    public string displayName = "";
    public CraftSupplyKind kind = CraftSupplyKind.Other;
    [Tooltip("The item it uses up (its name, asset name or id).")]
    public string item = "";
    [TextArea(1, 2)] public string description = "";
    [Tooltip("Thread: strength (1 = linen). Padding: protection. Wax/oil: how much it gives. Flux: how well it works.")]
    [Min(0f)] public float strength = 1f;
    [Tooltip("Dye colour; colour of the supply.")]
    public Color color = Color.white;
    [Min(0f)] public float price = 1f;

    [Tooltip("Fibre: the thread it spins into - coarse, even, fine (supply ids).")]
    public List<string> spinsInto = new List<string>();

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>What an abrasive tool is. Serialized as numbers: add at the END.</summary>
public enum CraftAbrasiveKind
{
    /// <summary>Removes loose scale (wire brush, scraper).</summary>
    Brush,
    /// <summary>A grinding wheel.</summary>
    Wheel,
    /// <summary>A file or a rasp.</summary>
    File,
    /// <summary>Sanding cloth, stones and a buffing wheel (polishing).</summary>
    Polish,
    /// <summary>A whetstone or a strop.</summary>
    Whetstone,
}

/// <summary>
/// A tool that takes material off: wire brush, scraper, grinding wheels, files, polishing grits, whetstones and the
/// strop. How fast it cuts, the finish it leaves, how much it heats, what it can cut.
/// </summary>
[Serializable]
public class CraftAbrasive
{
    public string id = "";
    public string displayName = "";
    public CraftAbrasiveKind kind = CraftAbrasiveKind.File;
    [Tooltip("Metal (mm of thickness per second at full pressure, on soft iron) it removes.")]
    [Min(0f)] public float cut = 0.05f;
    [Tooltip("Scale it removes per second.")]
    [Min(0f)] public float scale = 0.05f;
    [Tooltip("Roughness it leaves (µm): the finest finish it can reach.")]
    [Min(0.05f)] public float finish = 3f;
    [Tooltip("Heat it puts into the edge (°C per second of hard work on a thin edge).")]
    [Min(0f)] public float heat;
    [Tooltip("It cannot cut metal harder than this (HV): a file skates on hardened steel.")]
    [Min(1f)] public float maxHardness = 2000f;
    [Tooltip("Works wood too (rasps, knives).")]
    public bool wood;
    [Tooltip("Works leather (edge bevellers, slickers).")]
    public bool leather;
    [Tooltip("Whetstones: the edge apex it reaches (µm).")]
    [Min(0.1f)] public float apex = 10f;
    [Tooltip("The item a station needs for it (empty = part of the station's tools).")]
    public string item = "";

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}

/// <summary>A mould the crucible pours into: the shape (and count) of what comes out.</summary>
[Serializable]
public class CraftMold
{
    public string id = "";
    public string displayName = "";
    [Tooltip("The shape poured (mm).")]
    public float length = 150f;
    public float width = 40f;
    public float thickness = 20f;
    [Tooltip("Optional profile along its length (t, width, thickness): a blade blank, a guard...")]
    public List<CraftProfilePoint> profile = new List<CraftProfilePoint>();
    [Tooltip("Fasteners: a fastener mould makes this kind (rivets, buckles).")]
    public bool fasteners;
    public CraftFastenerKind fastenerKind = CraftFastenerKind.Rivet;
    [Tooltip("Fasteners per pour.")]
    [Min(1)] public int count = 1;
    [Tooltip("Grams one fastener weighs.")]
    [Min(0.1f)] public float fastenerMass = 6f;
    [Tooltip("A hole (eye) is cast in at this position (0-1; -1 = none) of this diameter (mm).")]
    public float eyeAt = -1f;
    public float eyeDiameter;
    /// <summary>A mould made from a design's part (see <see cref="CraftRules.PatternMold"/>): what is cast in it is meant for that part.</summary>
    [NonSerialized] public string designId, designRole;

    public string Name => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
}
