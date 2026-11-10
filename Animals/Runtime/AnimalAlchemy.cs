using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animal medicine through the Alchemy system: the three veterinary outcomes, what a bottle or an ingredient is to the
/// animal rules (its key and the quality of each copy), and making medicine from code (care kits, merchants).
/// <para>The veterinary outcomes are an extension pack of the Alchemy Database: <c>vet_vermifuge</c> (wormwood steeped in
/// plain water), <c>vet_antiseptic</c> (tannin drawn into strong spirit) and <c>vet_electrolytes</c> (salt and honey in
/// clean water). They add no substance and no reaction: each is a name for a liquid the existing ingredients already make
/// (today a Cloudy Infusion or a Murky Mixture), tried after every potion and material (priorities 43-47, below Soft
/// Soap's 50) and before the failures, so no existing brew changes what it is.</para>
/// <para>The pack is installed on every machine before the first scene loads (the same rules everywhere, or the stations'
/// mirrors would disagree), and by the Animals validators and tests through <see cref="EnsureInstalled"/>. The built-in
/// database (made in memory) takes the pack among its extensions. A project's own database asset is never changed: while
/// playing, a wrapper made in memory holds the asset and the pack and takes its place.</para>
/// </summary>
public static class AnimalAlchemy
{
    /// <summary>Outcome ids of the veterinary medicines.</summary>
    public const string VermifugeId = "vet_vermifuge", AntisepticId = "vet_antiseptic", ElectrolytesId = "vet_electrolytes";

    /// <summary>The name of the extension pack (how an installed one is recognised).</summary>
    public const string PackName = "Animal Husbandry Veterinary Pack";

    private const string PotionPrefix = "potion:", IngredientPrefix = "ingredient:";

    /// <summary>The purity a bottle made from code is given (its strength makes up the asked quality).</summary>
    private const double MadePurity = 0.9;

    private static readonly string[] outcomeIds = { VermifugeId, AntisepticId, ElectrolytesId };

    /// <summary>The veterinary outcome ids (some may be missing when a project's database lacks their substances).</summary>
    public static IReadOnlyList<string> OutcomeIds => outcomeIds;

    // The wrapper that takes the place of a project's database asset while playing, the asset it wraps, and its pack.
    private static AlchemyDatabase wrapper, wrapped, wrapperPack;

    // Items made by hand and set as an outcome's tier item (not brewed bottles): which outcome and tier each is.
    private static AlchemyRules handMadeRules;
    private static readonly List<ItemSO> handMadeItems = new List<ItemSO>();
    private static readonly List<string> handMadeOutcomes = new List<string>();
    private static readonly List<int> handMadeTiers = new List<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // The wrapper itself is kept: a play session without a domain reload wraps the same asset again with it.
        handMadeRules = null;
        handMadeItems.Clear();
        handMadeOutcomes.Clear();
        handMadeTiers.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallAtStart() => EnsureInstalled();

    // ---------------------------------------------------------------- installing
    /// <summary>
    /// Makes sure the veterinary outcomes are in <see cref="AlchemyDatabase.Instance"/> (idempotent: once there, nothing is
    /// done). Runs on every machine before the first scene loads; the Animals validators and tests call it too. The
    /// built-in database takes the pack among its extensions; a project's database asset is never changed - while playing a
    /// wrapper takes its place (<see cref="AlchemyDatabase.Override"/>), and in the editor outside play mode it is left as it
    /// is (its tools keep working on the asset itself; the outcomes still count as existing, see <see cref="OutcomeExists"/>).
    /// An outcome whose id the project's database already has is not added (the project's own wins).
    /// </summary>
    public static void EnsureInstalled()
    {
        AlchemyDatabase db = AlchemyDatabase.Instance;
        if (db == null) return;
        AlchemyDatabase use = Install(db, Application.isPlaying);
        if (use != null && !ReferenceEquals(use, db))
            AlchemyDatabase.Override(use);
    }

    /// <summary>
    /// Installs the pack for <paramref name="db"/> and returns the database to use: <paramref name="db"/> itself (it has the
    /// outcomes already, or it is made in memory and took the pack), a wrapper of it (an asset, when
    /// <paramref name="allowWrapper"/>), or <paramref name="db"/> unchanged (an asset, no wrapper allowed).
    /// </summary>
    internal static AlchemyDatabase Install(AlchemyDatabase db, bool allowWrapper)
    {
        if (db == null) return null;
        AlchemyRules rules = db.Rules;
        if (MissingCount(rules) == 0) return db;
        if (IsInMemory(db))
        {
            // The built-in database (or one made by code): it is nobody's asset, so it takes the pack itself.
            if (db.extensions == null) db.extensions = new List<AlchemyDatabase>();
            db.extensions.RemoveAll(IsOurPack);
            AlchemyDatabase pack = MakePack(rules);
            if (pack == null) return db;
            db.extensions.Add(pack);
            db.MarkChanged();
            return db;
        }
        if (!allowWrapper) return db;
        if (wrapper == null || !ReferenceEquals(wrapped, db))
        {
            wrapper = MakeWrapper(db, rules, out wrapperPack);
            wrapped = db;
        }
        else
            wrapper.MarkChanged(); // the asset may have changed since the wrapper was made
        return wrapper;
    }

    /// <summary>Is the database made in memory (the built-in one, a wrapper, a pack) rather than a project asset?</summary>
    private static bool IsInMemory(AlchemyDatabase db) => db != null && (db.hideFlags & HideFlags.DontSave) != 0;

    private static bool IsOurPack(AlchemyDatabase db) => db != null && db.name == PackName;

    /// <summary>The veterinary outcomes these rules could have (their substances exist) but do not.</summary>
    private static int MissingCount(AlchemyRules rules)
    {
        if (rules == null) return 0;
        int n = 0;
        foreach (string id in outcomeIds)
            if (rules.Outcome(id) == null && CanMake(rules, id)) n++;
        return n;
    }

    /// <summary>A new, empty database made in memory (never saved).</summary>
    private static AlchemyDatabase NewDatabase(string name)
    {
        var db = ScriptableObject.CreateInstance<AlchemyDatabase>();
        db.name = name;
        db.hideFlags = HideFlags.DontSave;
        // Empty every list (a new database starts with the built-in forms: merged in, they would replace a project's own).
        db.properties = new List<AlchemyPropertyDefinition>();
        db.substances = new List<AlchemySubstance>();
        db.reactions = new List<AlchemyReaction>();
        db.outcomes = new List<AlchemyOutcome>();
        db.forms = new List<AlchemyIngredientForm>();
        db.filters = new List<AlchemyFilter>();
        db.fuels = new List<AlchemyFuel>();
        db.itemIngredients = new List<AlchemyItemIngredient>();
        db.extensions = new List<AlchemyDatabase>();
        return db;
    }

    /// <summary>The pack: the veterinary outcomes <paramref name="baseRules"/> are missing (null when there are none).</summary>
    private static AlchemyDatabase MakePack(AlchemyRules baseRules)
    {
        var missing = new List<AlchemyOutcome>();
        foreach (AlchemyOutcome o in VetOutcomes(baseRules))
            if (baseRules.Outcome(o.id) == null)
                missing.Add(o);
        if (missing.Count == 0) return null;
        AlchemyDatabase pack = NewDatabase(PackName);
        pack.notes = "Animal husbandry's veterinary medicines (AnimalAlchemy), made in memory at start. " +
                     "Recipes: Documentation/Alchemy/05-Default-Chemistry.md, Veterinary medicines.";
        pack.outcomes.AddRange(missing);
        pack.MarkChanged();
        return pack;
    }

    /// <summary>A database made in memory that holds the project's asset and the pack (the asset itself is never changed).</summary>
    private static AlchemyDatabase MakeWrapper(AlchemyDatabase asset, AlchemyRules assetRules, out AlchemyDatabase pack)
    {
        AlchemyDatabase w = NewDatabase(asset.name + " + " + PackName);
        w.notes = "Made in memory while playing: the project's Alchemy Database and the veterinary pack. Edit the asset, not this.";
        w.settings = asset.settings; // the root's settings are the ones the rules use
        w.extensions.Add(asset);
        pack = MakePack(assetRules);
        if (pack != null) w.extensions.Add(pack);
        w.MarkChanged();
        return w;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void WatchPlayMode()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>Back in the editor: the project's own database is again the one its tools (the Alchemy Lab, the inspector) use.</summary>
    private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
    {
        if (change != UnityEditor.PlayModeStateChange.EnteredEditMode || wrapper == null) return;
        if (ReferenceEquals(AlchemyDatabase.Instance, wrapper))
            AlchemyDatabase.Override(null);
        if (wrapperPack != null) UnityEngine.Object.DestroyImmediate(wrapperPack);
        UnityEngine.Object.DestroyImmediate(wrapper);
        wrapper = wrapped = wrapperPack = null;
    }
#endif

    // ---------------------------------------------------------------- the veterinary outcomes
    /// <summary>
    /// The veterinary outcomes for <paramref name="rules"/> (fresh copies; each only when every substance it reads exists
    /// there). Classifications of liquids the existing ingredients make - no new substance, no new reaction. Each needs its
    /// substance to be what the liquid is mostly about (the main one of its role: a little wormwood in a mushroom broth is
    /// no vermifuge), no active compound (a potion on its way stays what it becomes), little residue (a spoiled brew stays
    /// spoiled), nothing poisonous and little dirt (muddy water must be filtered first):
    /// <list type="bullet">
    /// <item><b>Vermifuge</b> (47): absinthin (wormwood's bitter principle) at least 2 % of the liquid, pH 6-10 (in acid it
    /// becomes antitoxin, an antidote; in lye it is no drench), no antitoxin. Full strength at 12 %.</item>
    /// <item><b>Antiseptic Wash</b> (45): tannin at least 3 % of the liquid, in strong spirit (at least 35 % of the liquid),
    /// pH 3-9. Full strength at 30 %.</item>
    /// <item><b>Electrolyte Drench</b> (43): brine 2-14 % and sugars 2-16 % of the liquid (the salt the main reagent, the honey
    /// the main raw matter), in water (spirit at most 10 %), pH 5.5-8.5. Full strength at 8 % brine.</item>
    /// </list>
    /// </summary>
    public static List<AlchemyOutcome> VetOutcomes(AlchemyRules rules)
    {
        var list = new List<AlchemyOutcome>();
        if (rules == null) return list;
        if (CanMake(rules, VermifugeId))
        {
            AlchemyOutcome o = Outcome(VermifugeId, "Vermifuge",
                    "A bitter wormwood drench that drives worms out of livestock. For animals: it turns a person's stomach.",
                    47, AlchemyDefaults.Absinthin, 0.12f, 20f)
                .Requires(MainOfItsRole(rules, AlchemyDefaults.Absinthin))
                .Requires(new SubstanceCondition(SubstanceSelector.Of(AlchemyDefaults.Absinthin), 0.02f))
                .Requires(new VariableCondition(AlchemyVariables.Ph, 6f, 10f));
            if (rules.IndexOf(AlchemyDefaults.Antitoxin) >= 0)
                o.Requires(new NotCondition(new SubstanceCondition(SubstanceSelector.Of(AlchemyDefaults.Antitoxin), 0.005f)));
            list.Add(Fx(Fx(Clean(rules, o), ConsumableEffectType.Hp, -5f), ConsumableEffectType.Sanity, -3f));
        }
        if (CanMake(rules, AntisepticId))
        {
            AlchemyOutcome o = Outcome(AntisepticId, "Antiseptic Wash",
                    "Tannin drawn out in strong spirit: it cleans wounds, sore hooves and broken skin. A wash for animals, not a drink.",
                    45, AlchemyDefaults.Tannin, 0.3f, 18f)
                .Requires(MainOfItsRole(rules, AlchemyDefaults.Tannin))
                .Requires(new SubstanceCondition(SubstanceSelector.Of(AlchemyDefaults.Tannin), 0.03f))
                .Requires(new SubstanceCondition(SubstanceSelector.Of(AlchemyDefaults.Spirit), 0.35f))
                .Requires(new VariableCondition(AlchemyVariables.Ph, 3f, 9f));
            list.Add(Fx(Clean(rules, o), ConsumableEffectType.Hp, -8f));
        }
        if (CanMake(rules, ElectrolytesId))
        {
            AlchemyOutcome o = Outcome(ElectrolytesId, "Electrolyte Drench",
                    "Salt and honey in clean water: it gives back what a parched or scouring animal has lost. Far too salty for a person.",
                    43, AlchemyDefaults.Brine, 0.08f, 8f)
                .Requires(MainOfItsRole(rules, AlchemyDefaults.Brine))
                .Requires(MainOfItsRole(rules, AlchemyDefaults.Sugars))
                .Requires(new SubstanceCondition(SubstanceSelector.Of(AlchemyDefaults.Brine), 0.02f, 0.14f))
                .Requires(new SubstanceCondition(SubstanceSelector.Of(AlchemyDefaults.Sugars), 0.02f, 0.16f))
                .Requires(new VariableCondition(AlchemyVariables.Ph, 5.5f, 8.5f)); // no sour fruit or lye in a drench
            if (rules.IndexOf(AlchemyDefaults.Spirit) >= 0)
                o.Requires(new SubstanceCondition(SubstanceSelector.Of(AlchemyDefaults.Spirit), 0f, 0.1f));
            list.Add(Fx(Fx(Clean(rules, o), ConsumableEffectType.Hp, -2f), ConsumableEffectType.Sanity, -2f));
        }
        return list;
    }

    /// <summary>"It is the main substance of its role in the liquid" (the role it has in these rules).</summary>
    private static AlchemyCondition MainOfItsRole(AlchemyRules rules, string substanceId) =>
        new DominantCondition(substanceId, rules.Substance(substanceId).role);

    /// <summary>
    /// The requirements every veterinary outcome shares: no active compound, little residue, nothing poisonous, and clean
    /// (made with muddy water and not filtered, it stays what it was: a medicine for wounds and bellies must be clean).
    /// </summary>
    private static AlchemyOutcome Clean(AlchemyRules rules, AlchemyOutcome o)
    {
        o.Requires(new RoleShareCondition(SubstanceRole.Active, 0f, 0.02f))
         .Requires(new RoleShareCondition(SubstanceRole.Residue, 0f, 0.2f));
        if (rules.PropertyIndex("toxicity") >= 0)
            o.Requires(new PropertyCondition("toxicity", -1000f, 0.04f)); // below what taints a healing draught
        string dirt = rules.Settings.contaminantProperty;
        if (rules.PropertyIndex(dirt) >= 0)
            o.Requires(new PropertyCondition(dirt, -1000f, 0.015f)); // below what makes a healing draught murky
        return o;
    }

    /// <summary>Do <paramref name="rules"/> have every substance the veterinary outcome <paramref name="outcomeId"/> reads?</summary>
    private static bool CanMake(AlchemyRules rules, string outcomeId)
    {
        if (rules == null) return false;
        if (string.Equals(outcomeId, VermifugeId, StringComparison.OrdinalIgnoreCase))
            return rules.IndexOf(AlchemyDefaults.Absinthin) >= 0;
        if (string.Equals(outcomeId, AntisepticId, StringComparison.OrdinalIgnoreCase))
            return rules.IndexOf(AlchemyDefaults.Tannin) >= 0 && rules.IndexOf(AlchemyDefaults.Spirit) >= 0;
        if (string.Equals(outcomeId, ElectrolytesId, StringComparison.OrdinalIgnoreCase))
            return rules.IndexOf(AlchemyDefaults.Brine) >= 0 && rules.IndexOf(AlchemyDefaults.Sugars) >= 0;
        return false;
    }

    /// <summary>A potion outcome with the built-in quality bands (Weak, Standard, Potent, Superior) and no requirements yet.</summary>
    private static AlchemyOutcome Outcome(string id, string name, string description, int priority, string potency, float fullAt, float price)
    {
        var o = new AlchemyOutcome
        {
            id = id, displayName = name, description = description, kind = AlchemyOutcomeKind.Potion, priority = priority,
            potency = SubstanceSelector.Of(potency), fullPotencyAt = fullAt, basePrice = price,
        };
        return o.Tier("Weak", 0f, 0.5f).Tier("", 0.35f, 1f).Tier("Potent", 0.65f, 1.5f).Tier("Superior", 0.9f, 2f);
    }

    /// <summary>What drinking it does to a person (an instant effect, at the Standard tier).</summary>
    private static AlchemyOutcome Fx(AlchemyOutcome o, ConsumableEffectType type, float amount)
    {
        o.effects.Add(new ConsumableEffect
        {
            effectType = type,
            itemType = ConsumableType.Potion,
            effectName = $"{o.Name} {type}",
            amount = amount,
            applicationChance = 1f,
            minimumLevel = 1,
        });
        return o;
    }

    // ---------------------------------------------------------------- what an item is
    /// <summary>
    /// The key an Alchemy item goes by in the animal rules: "potion:&lt;outcome&gt;" for a brewed bottle (whatever its tier,
    /// colour, or asset) or an item made by hand and set as a tier of an outcome; "ingredient:&lt;id&gt;" for an alchemy
    /// ingredient (its preset id: the name in lower case, spaces as '_'; an asset and its runtime twin alike). Null for
    /// anything else.
    /// </summary>
    public static string KeyOf(ItemSO item)
    {
        if (item == null) return null;
        if (item is BrewedPotionSO potion)
            return string.IsNullOrWhiteSpace(potion.OutcomeId) ? null : PotionPrefix + potion.OutcomeId.Trim();
        if (item is AlchemyIngredientSO ingredient)
        {
            string runtime = ingredient.RuntimeId;
            if (!string.IsNullOrEmpty(runtime)) return runtime;
            string id = NormalisedId(ingredient.AlchemyId);
            return string.IsNullOrEmpty(id) ? null : IngredientPrefix + id;
        }
        string outcome = HandMadeOutcome(item, out _);
        return outcome != null ? PotionPrefix + outcome : null;
    }

    /// <summary>An ingredient's id as its presets have it ("Brewer's Yeast" → "brewer's_yeast").</summary>
    private static string NormalisedId(string name) =>
        string.IsNullOrWhiteSpace(name) ? "" : name.Trim().ToLowerInvariant().Replace(' ', '_');

    /// <summary>The outcome (and tier) an item made by hand is a tier item of; null when it is none (brewed bottles know their own).</summary>
    private static string HandMadeOutcome(ItemSO item, out int tier)
    {
        tier = 0;
        if (item == null || item is BrewedPotionSO) return null;
        AlchemyDatabase db = AlchemyDatabase.Instance;
        AlchemyRules rules = db != null ? db.Rules : null;
        if (rules == null) return null;
        if (!ReferenceEquals(rules, handMadeRules))
        {
            handMadeRules = rules;
            handMadeItems.Clear();
            handMadeOutcomes.Clear();
            handMadeTiers.Clear();
            foreach (AlchemyOutcome o in rules.Outcomes)
            {
                if (o == null || o.tiers == null) continue;
                for (int t = 0; t < o.tiers.Count; t++)
                {
                    ItemSO own = o.tiers[t] != null ? o.tiers[t].item : null;
                    if (own == null || own is BrewedPotionSO) continue;
                    handMadeItems.Add(own);
                    handMadeOutcomes.Add(o.id);
                    handMadeTiers.Add(t);
                }
            }
        }
        for (int i = 0; i < handMadeItems.Count; i++)
            if (ReferenceEquals(handMadeItems[i], item))
            {
                tier = handMadeTiers[i];
                return handMadeOutcomes[i];
            }
        return null;
    }

    /// <summary>
    /// The quality (0-1) of one copy of a potion stack (<paramref name="fromTop"/> 0 = the top copy, the next one used): a
    /// brewed bottle by the strength and purity it was bottled at (as the evaluator grades them); a bottle without a code (a
    /// merchant's, an older one), or an item made by hand, by its tier's typical quality (Weak 0.2, Standard 0.5, Potent
    /// 0.75, Superior 0.95); an outcome without potency 1. Anything else: 0.5.
    /// </summary>
    public static float QualityOfCopy(InventoryItem stack, int fromTop)
    {
        ItemSO item = stack != null ? stack.itemScriptableObject : null;
        if (item == null) return 0.5f;
        string outcomeId;
        int tier;
        var potion = item as BrewedPotionSO;
        if (potion != null)
        {
            outcomeId = potion.OutcomeId;
            tier = potion.Tier;
        }
        else
        {
            outcomeId = HandMadeOutcome(item, out tier);
            if (outcomeId == null) return 0.5f;
        }
        AlchemyDatabase db = AlchemyDatabase.Instance;
        AlchemyOutcome o = db != null ? db.Rules.Outcome(outcomeId) : null;
        if (o != null && !o.HasPotency) return 1f;
        // The codes are read copy by copy (never the stack's own float, which keeps the last bottle taken).
        if (potion != null && BrewedPotionSO.TryDecode(AnimalToolkit.CodeOf(stack, fromTop), out double potency, out double purity))
            return Mathf.Clamp01((float)BrewedPotionSO.QualityOf(potency, purity));
        return TypicalQuality(o, tier);
    }

    private static readonly float[] typicalByTier = { 0.2f, 0.5f, 0.75f, 0.95f };

    /// <summary>
    /// The quality a copy of a tier is taken to have when it carries no code: Weak 0.2, Standard 0.5, Potent 0.75,
    /// Superior 0.95 with the built-in bands, else the middle of the tier's band.
    /// </summary>
    internal static float TypicalQuality(AlchemyOutcome o, int tier)
    {
        int count = o != null && o.tiers != null ? o.tiers.Count : 0;
        if (count == 0)
            return tier >= 0 && tier < typicalByTier.Length ? typicalByTier[tier] : 0.5f;
        int t = Mathf.Clamp(tier, 0, count - 1);
        float low = o.tiers[t] != null ? o.tiers[t].minQuality : 0f;
        float high = t + 1 < count && o.tiers[t + 1] != null ? o.tiers[t + 1].minQuality : 1f;
        if (t < typicalByTier.Length && typicalByTier[t] >= low - 1e-4f && typicalByTier[t] <= high + 1e-4f)
            return typicalByTier[t];
        return Mathf.Clamp01((low + high) * 0.5f);
    }

    // ---------------------------------------------------------------- outcomes and their items
    /// <summary>
    /// Does the Alchemy Database have this outcome (after <see cref="EnsureInstalled"/>)? The veterinary outcomes count as
    /// existing whenever their substances do: in the editor outside play mode a project's database asset is not wrapped,
    /// yet they are there as soon as the game runs.
    /// </summary>
    public static bool OutcomeExists(string outcomeId)
    {
        if (string.IsNullOrWhiteSpace(outcomeId)) return false;
        EnsureInstalled();
        AlchemyDatabase db = AlchemyDatabase.Instance;
        AlchemyRules rules = db != null ? db.Rules : null;
        if (rules == null) return false;
        return rules.Outcome(outcomeId.Trim()) != null || CanMake(rules, outcomeId.Trim());
    }

    /// <summary>
    /// The item a bottle of one tier of an outcome is (the tier's own item when it has one, else the runtime bottle every
    /// machine makes from its id); <paramref name="tier"/> is clamped to the outcome's tiers (1 = Standard with the built-in
    /// bands). Null when the outcome is unknown or bottles nothing.
    /// </summary>
    public static ItemSO PotionItem(string outcomeId, int tier = 1)
    {
        if (string.IsNullOrWhiteSpace(outcomeId)) return null;
        EnsureInstalled();
        AlchemyRules rules = AlchemyDatabase.Instance.Rules;
        AlchemyOutcome o = rules.Outcome(outcomeId.Trim());
        if (o == null || !o.producesItems) return null;
        int count = o.tiers != null ? o.tiers.Count : 0;
        return TierItem(rules, o, Mathf.Clamp(tier, 0, Mathf.Max(0, count - 1)));
    }

    /// <summary>The item of one tier, through Alchemy's own runtime bottles (the same item a station bottles: they stack).</summary>
    private static ItemSO TierItem(AlchemyRules rules, AlchemyOutcome o, int tier)
    {
        var result = new AlchemyResult
        {
            Outcome = o,
            Tier = tier,
            LiquidColor = AlchemyItems.Typical(rules, o, new Color(0.6f, 0.6f, 0.65f)),
        };
        return AlchemyItems.PotionItem(rules, result, true);
    }

    /// <summary>
    /// Gives a player <paramref name="count"/> bottles of an outcome at <paramref name="quality"/> (0-1: the tier follows
    /// it), each coded with a strength and purity of that quality - as if brewed. Where the rules run only (the player's
    /// inventory is authoritative); what does not fit in the bag goes on the ground by them. Returns the bottles given (0:
    /// unknown outcome, nothing bottled, no player or not here).
    /// </summary>
    public static int MakePotions(CombatEntity player, string outcomeId, int count, float quality)
    {
        if (player == null || count <= 0 || string.IsNullOrWhiteSpace(outcomeId)) return 0;
        InventoryManager inv = InventoryManager.For(player);
        if (inv == null || !inv.IsAuthoritative) return 0;
        EnsureInstalled();
        AlchemyRules rules = AlchemyDatabase.Instance.Rules;
        AlchemyOutcome o = rules.Outcome(outcomeId.Trim());
        if (o == null || !o.producesItems) return 0;
        double q = Mathf.Clamp01(quality);
        int code = 0;
        if (o.HasPotency)
        {
            code = CodeFor(q);
            if (BrewedPotionSO.TryDecode(code, out double potency, out double purity))
                q = BrewedPotionSO.QualityOf(potency, purity); // the tier follows what the code says
        }
        ItemSO item = TierItem(rules, o, Math.Max(0, o.TierFor(q)));
        if (item == null) return 0;
        List<int> codes = null;
        if (code > 0 && item is BrewedPotionSO)
        {
            codes = new List<int>(count);
            for (int i = 0; i < count; i++) codes.Add(code);
        }
        AnimalToolkit.Give(player, item, count, codes);
        return count;
    }

    /// <summary>
    /// The code of a bottle of <paramref name="quality"/>: purity 0.9 and the strength that gives that quality (pure and at
    /// full strength above what purity 0.9 allows).
    /// </summary>
    internal static int CodeFor(double quality)
    {
        double q = Math.Max(0.0, Math.Min(1.0, quality));
        AlchemyDatabase db = AlchemyDatabase.Instance;
        double influence = db != null ? Math.Max(0.0, Math.Min(1.0, db.Rules.Settings.purityInfluence)) : 0.6;
        double purity = MadePurity;
        double potency = q / Math.Max(1e-6, 1.0 - influence * (1.0 - purity));
        if (potency > 1.0)
        {
            potency = 1.0;
            purity = influence > 1e-6 ? Math.Max(0.0, Math.Min(1.0, 1.0 - (1.0 - q) / influence)) : 1.0;
        }
        return BrewedPotionSO.Encode(potency, purity);
    }

    /// <summary>A built-in ingredient preset as an item ("charcoal", "Wormwood": by id or name; null when there is no such preset).</summary>
    public static ItemSO IngredientItem(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId)) return null;
        string id = presetId.Trim();
        if (id.StartsWith(IngredientPrefix, StringComparison.Ordinal)) id = id.Substring(IngredientPrefix.Length);
        return AlchemyItems.RuntimeIngredient(id);
    }
}
