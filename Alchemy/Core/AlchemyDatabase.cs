using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The physics of the simulation: time step, heat, boiling and evaporation, dissolving, solubility and crystals,
/// stirring, the manual fire and baths, layers and particles, acidity, contamination, observation and bottling. Shared
/// by every station; tune it on the Alchemy Database.
/// </summary>
[Serializable]
public class AlchemySimulationSettings
{
    [Header("Simulation")]
    [Tooltip("Seconds per simulation step. Preparations run in whole steps (and operations happen between steps), so the " +
             "same operations at the same times always give the same result.")]
    [Range(0.05f, 1f)] public float stepSeconds = 0.2f;
    [Tooltip("Room temperature (°C) when the surroundings give none.")]
    public float defaultAmbient = 20f;
    [Tooltip("Thermal mass of one unit of contents (more contents heat and cool more slowly).")]
    [Min(0.01f)] public float heatCapacityPerUnit = 1f;
    [Tooltip("Lowest and highest temperatures the simulation allows (°C).")]
    public float minTemperature = -80f;
    public float maxTemperature = 1200f;
    [Tooltip("Amounts below this that are not growing are cleaned away (units): a trickle building up stays.")]
    [Min(0f)] public float traceAmount = 0.0005f;

    [Header("Boiling")]
    [Tooltip("How fast liquids above their boiling point boil away (per °C above it, per second, × the amount).")]
    [Min(0f)] public float boilRate = 0.02f;
    [Tooltip("Heat one unit takes to boil away (°C × thermal mass): boiling holds the temperature near the boiling point.")]
    [Min(0f)] public float latentHeat = 1500f;
    [Tooltip("Sealed vessels: how fast vapour condenses back once the contents are below its boiling point (per second).")]
    [Min(0f)] public float condenseRate = 0.4f;
    [Tooltip("Sealed vessels: pressure per unit of vapour, relative to the free space of the vessel (it also rises with the temperature).")]
    [Min(0f)] public float pressureScale = 2f;
    [Tooltip("Share of the contents lost when a sealed vessel bursts.")]
    [Range(0f, 1f)] public float ruptureSpill = 0.3f;

    [Header("Dissolving and stirring")]
    [Tooltip("Ingredients dissolve twice as fast every this many °C above 20 °C.")]
    [Min(1f)] public float dissolveDoublingDegrees = 30f;
    [Tooltip("Dissolving speed in a vessel with no liquid (dry ingredients barely dissolve).")]
    [Range(0f, 1f)] public float dryDissolveFactor = 0.15f;
    [Tooltip("Dissolving speed × (1 + this × agitation).")]
    [Min(0f)] public float agitationDissolveBoost = 2f;
    [Tooltip("Seconds for the agitation of a stir to fall to about a third.")]
    [Min(0.1f)] public float agitationDecaySeconds = 4f;

    [Header("Solubility")]
    [Tooltip("Share per second of what the liquid can no longer hold that comes out of solution (precipitates).")]
    [Min(0f)] public float precipitationRate = 0.5f;
    [Tooltip("Share per second of the undissolved part that dissolves again once there is room (× 1 + 2 × agitation).")]
    [Min(0f)] public float redissolveRate = 0.15f;
    [Tooltip("Crystals dissolve again this many times slower than loose powder.")]
    [Range(0.01f, 1f)] public float crystalRedissolveFactor = 0.4f;

    [Header("Evaporation")]
    [Tooltip("Below their boiling point, liquids evaporate at this share per second × the vessel's surface × 2 ^ ((T - " +
             "boiling point) / Evaporation Doubling Degrees): a simmer slowly concentrates, volatile oils leave first.")]
    [Min(0f)] public float evaporationRate = 0.00015f;
    [Min(1f)] public float evaporationDoublingDegrees = 15f;

    [Header("Continuous stirring")]
    [Tooltip("Seconds for the agitation to follow the stirring speed.")]
    [Min(0.05f)] public float stirResponseSeconds = 0.8f;
    [Tooltip("Reactions between two or more substances run × (1 + this × agitation): stirring brings them together.")]
    [Min(0f)] public float mixingBoost = 0.6f;
    [Tooltip("Uneven stirring: each change of speed adds this × the change to the turbulence, which fades over Turbulence Decay Seconds.")]
    [Min(0f)] public float turbulenceGain = 1f;
    [Min(0.1f)] public float turbulenceDecaySeconds = 3f;
    [Tooltip("Above this agitation an open vessel slops over: Splash Rate of the contents per second is lost.")]
    [Range(0f, 1f)] public float splashAgitation = 0.9f;
    [Min(0f)] public float splashRate = 0.003f;
    [Tooltip("Stirring hard beats air into the liquid (the aeration variable follows max(0, agitation - 0.5) × 2 over this many seconds).")]
    [Min(0.1f)] public float aerationSeconds = 5f;

    [Header("Manual fire")]
    [Tooltip("Fuel (units) that gives about two thirds of the full fire.")]
    [Min(0.01f)] public float fuelScale = 1f;
    [Tooltip("Fuel units burnt per second at full fire.")]
    [Min(0f)] public float fuelBurnRate = 0.02f;
    [Tooltip("Seconds for the flames to rise toward what the fuel and the air allow, and to die down.")]
    [Min(0.1f)] public float fireRiseSeconds = 5f;
    [Min(0.1f)] public float fireFallSeconds = 10f;
    [Tooltip("Draft with the air vent open and no bellows (0-1); each unit of bellows air adds Bellows Draft.")]
    [Range(0f, 1f)] public float draftBase = 0.55f;
    [Min(0f)] public float bellowsDraft = 0.6f;
    [Tooltip("Air added by one full pump of the bellows, and the seconds it takes to fade.")]
    [Range(0f, 1f)] public float bellowsGain = 0.35f;
    [Min(0.1f)] public float bellowsDecaySeconds = 3f;

    [Header("Baths")]
    [Tooltip("A water bath never lets the contents go above this (°C), and heats them this many times more gently.")]
    public float waterBathMax = 96f;
    [Range(0.05f, 1f)] public float waterBathTransfer = 0.7f;
    [Tooltip("An ice bath cools toward this (°C) with this heat transfer (the fire barely matters).")]
    public float iceBathTemperature = 0f;
    [Min(0f)] public float iceBathTransfer = 3f;

    [Header("Layers and particles")]
    [Tooltip("Watery and oily solvents form two layers when each is at least this share of the solvent.")]
    [Range(0.01f, 0.5f)] public float layerMinShare = 0.08f;
    [Tooltip("Stirring above this agitation emulsifies two layers (they mix while it lasts) within Emulsion Seconds; " +
             "without stirring the emulsion breaks within Emulsion Break Seconds.")]
    [Range(0f, 1f)] public float emulsifyAgitation = 0.35f;
    [Min(0.1f)] public float emulsionSeconds = 2f;
    [Min(0.1f)] public float emulsionBreakSeconds = 4f;
    [Tooltip("Substances with this property keep an emulsion from breaking (egg yolk, soap): at 1% of the liquid, " +
             "Emulsifier Stability times longer.")]
    public string emulsifierProperty = "emulsifier";
    [Min(0f)] public float emulsifierStability = 150f;
    [Tooltip("Substances with this property make suspended particles settle (alum, egg white): suspension ÷ (1 + " +
             "Clarifier Strength × the property's value).")]
    public string clarifierProperty = "clarifying";
    [Min(0f)] public float clarifierStrength = 100f;

    [Header("Acidity")]
    [Tooltip("pH = 7 - 7 × tanh((acid - alkali) / pH Scale), from these two properties of the liquid.")]
    public string acidProperty = "acid";
    public string alkaliProperty = "alkali";
    [Min(0.001f)] public float phScale = 0.08f;

    [Header("Contamination")]
    [Tooltip("Substances with this property get in the way of every reaction - dissolved, floating in the liquid, or (a " +
             "third as much) settled at the bottom: rates ÷ (1 + Contaminant Interference × their share ÷ 0.05).")]
    public string contaminantProperty = "contaminant";
    [Min(0f)] public float contaminantInterference = 0.5f;
    [Tooltip("Share of the residues and contaminants that stays in the vessel when it is emptied (a station's vessel " +
             "gets dirty: clean it, or the next preparation starts with them).")]
    [Range(0f, 1f)] public float vesselResidueShare = 0.05f;

    [Header("Observation")]
    [Tooltip("A reaction counts as running (log, visuals) at this rate or more (units per second).")]
    [Min(0f)] public float activeRateThreshold = 0.004f;
    [Tooltip("Units of a reaction that must happen in a preparation for the player to have seen it.")]
    [Min(0f)] public float observeAmount = 0.3f;
    [Tooltip("Preparations in which the player must see a reaction before they understand it (the journal names it and its conditions).")]
    [Min(1)] public int observationsToUnderstand = 2;
    [Tooltip("A substance counts as present (log, identification) from this many units.")]
    [Min(0f)] public float noticeableAmount = 0.05f;

    [Header("Bottling")]
    [Tooltip("Units of liquid per bottle.")]
    [Min(0.1f)] public float unitsPerBottle = 5f;
    [Tooltip("The last bottle is filled when at least this share of a bottle is left.")]
    [Range(0.1f, 1f)] public float minFillForBottle = 0.6f;
    [Tooltip("How much impurities lower the quality (0 = only potency counts, 1 = purity matters as much).")]
    [Range(0f, 1f)] public float purityInfluence = 0.6f;
}

/// <summary>
/// The alchemy system's rules: properties, substances, reactions, outcomes, extra item ingredients and the
/// simulation settings. Stations compile it into <see cref="AlchemyRules"/>. Optional: without one in
/// Resources/SimpleMovements/Settings/AlchemyDatabase, the built-in rules (<see cref="AlchemyDefaults"/>) are used.
/// <para>Expansions: put further databases in <see cref="extensions"/> - their properties, substances, reactions and
/// outcomes are added (same ids replace the base ones), so content packs need no change to this asset or the code.</para>
/// </summary>
[CreateAssetMenu(fileName = "AlchemyDatabase", menuName = "SimpleMovements/Alchemy/Alchemy Database", order = 0)]
public class AlchemyDatabase : ScriptableObject
{
    public const string ResourcesName = "AlchemyDatabase";

    [Tooltip("Shown in the inspector only.")]
    [TextArea(1, 3)] public string notes = "";
    public AlchemySimulationSettings settings = new AlchemySimulationSettings();

    [Header("Rules (the chemistry - no items here)")]
    public List<AlchemyPropertyDefinition> properties = new List<AlchemyPropertyDefinition>();
    [Tooltip("The chemicals inside the vessel (Solar Sap, Vital Essence, Water...): what ingredients are made of and what " +
             "reactions make. They are not items: players carry Ingredient items (which release substances) and Potion items " +
             "(Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Ingredient Items / Create Potion Items).")]
    public List<AlchemySubstance> substances = new List<AlchemySubstance>();
    public List<AlchemyReaction> reactions = new List<AlchemyReaction>();
    public List<AlchemyOutcome> outcomes = new List<AlchemyOutcome>();
    [Tooltip("The forms ingredients come in (whole, ground, extract...): how fast and how selectively each releases. Empty = the built-in ones.")]
    public List<AlchemyIngredientForm> forms = AlchemyIngredientForm.Defaults();
    [Tooltip("The ways of filtering the liquid (strainer, paper, charcoal...). Empty = the built-in ones.")]
    public List<AlchemyFilter> filters = new List<AlchemyFilter>();
    [Tooltip("What a manual fire burns (firewood, charcoal, resin...). Empty = the built-in ones.")]
    public List<AlchemyFuel> fuels = new List<AlchemyFuel>();

    [Header("Other items usable as ingredients")]
    [Tooltip("Optional: items of other kinds (a food, a monster part) that can go into a vessel too, with what they release. " +
             "Alchemy Ingredient items do not need to be listed here, and nothing here creates item assets.")]
    public List<AlchemyItemIngredient> itemIngredients = new List<AlchemyItemIngredient>();
    [Tooltip("More databases whose content is added to this one (content packs). Same ids replace this one's entries.")]
    public List<AlchemyDatabase> extensions = new List<AlchemyDatabase>();

    [NonSerialized] private AlchemyRules compiled;
    [NonSerialized] private int compiledVersion = -1;
    [NonSerialized] private int version;

    // ------------------------------------------------------------------ instance
    private static AlchemyDatabase instance;
    private static bool searched;

    /// <summary>The project's database (Resources/SimpleMovements/Settings/AlchemyDatabase), or the built-in rules.</summary>
    public static AlchemyDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                if (!searched)
                {
                    searched = true;
                    instance = SMAssets.Load<AlchemyDatabase>(SMAssets.Settings, ResourcesName);
                }
                if (instance == null)
                {
                    instance = CreateInstance<AlchemyDatabase>();
                    instance.name = "AlchemyDatabase (built-in)";
                    instance.hideFlags = HideFlags.DontSave;
                    AlchemyDefaults.Fill(instance);
                }
            }
            return instance;
        }
    }

    /// <summary>Uses a specific database (tests, bootstrap scripts). Null = find it again.</summary>
    public static void Override(AlchemyDatabase db)
    {
        instance = db;
        searched = db != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        searched = false;
    }

    private void Reset() => AlchemyDefaults.Fill(this);

    private void OnValidate()
    {
        version++;
        compiled = null;
    }

    /// <summary>Forget the compiled rules (call after changing the lists from code).</summary>
    public void MarkChanged()
    {
        version++;
        compiled = null;
    }

    /// <summary>The compiled rules of this database and its extensions (cached until it changes).</summary>
    public AlchemyRules Rules
    {
        get
        {
            if (compiled == null || compiledVersion != version)
            {
                compiled = AlchemyRules.Compile(this);
                compiledVersion = version;
            }
            return compiled;
        }
    }

    /// <summary>
    /// The ingredient profile of <paramref name="item"/>: its own (an <see cref="IAlchemyIngredient"/>), else this
    /// database's (or an extension's) Item Ingredients entry. Null = it cannot go into a vessel.
    /// </summary>
    public IAlchemyIngredient IngredientOf(ItemSO item)
    {
        if (item == null)
            return null;
        if (item is IAlchemyIngredient own && own.AlchemyProfile != null && own.AlchemyProfile.releases != null && own.AlchemyProfile.releases.Count > 0)
            return own;
        AlchemyItemIngredient entry = FindItemIngredient(item, new HashSet<AlchemyDatabase>());
        return entry != null ? new AlchemyIngredientSpec(item.name, item.Name, entry.profile) : null;
    }

    private AlchemyItemIngredient FindItemIngredient(ItemSO item, HashSet<AlchemyDatabase> seen)
    {
        if (!seen.Add(this))
            return null;
        if (extensions != null)
            for (int i = extensions.Count - 1; i >= 0; i--)
            {
                AlchemyItemIngredient e = extensions[i] != null ? extensions[i].FindItemIngredient(item, seen) : null;
                if (e != null) return e;
            }
        if (itemIngredients != null)
            foreach (AlchemyItemIngredient e in itemIngredients)
                if (e != null && e.item == item && e.profile != null)
                    return e;
        return null;
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        AlchemyRules rules = Rules;
        foreach (string e in rules.CompileErrors) errors.Add(e);
        foreach (string w in rules.CompileWarnings) warnings.Add(w);
        foreach (AlchemyReaction r in rules.ReactionDefinitions)
            r.Validate(rules, errors, warnings);
        foreach (AlchemyOutcome o in rules.Outcomes)
            o.Validate(rules, errors, warnings);
        if (itemIngredients != null)
            foreach (AlchemyItemIngredient e in itemIngredients)
            {
                if (e == null || e.item == null)
                    warnings.Add("Item Ingredients: an entry has no item.");
                else
                    e.profile?.Validate(rules, $"Item ingredient '{e.item.name}'", errors, warnings);
            }
        bool fallback = false;
        foreach (AlchemyOutcome o in rules.Outcomes)
            if (o.requirements == null || o.requirements.Count == 0)
                fallback = true;
        if (!fallback)
            warnings.Add("No outcome without requirements: a mixture that matches nothing gives a built-in 'Murky Mixture'.");
    }
}
