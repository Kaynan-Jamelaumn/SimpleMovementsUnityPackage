using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An item asset given for an animal item id (instead of the item made at runtime).</summary>
[Serializable]
public class AnimalItemBinding
{
    [Tooltip("The id: animal:feed/<food>, animal:livestock/<species>/<breed>, animal:tool/<tool> or animal:product/<product>.")]
    public string id = "";
    public ItemSO item;
}

/// <summary>
/// The project's animal husbandry: the rules, the species (with their breeds), the foods, the illnesses, the medicines,
/// the care tools and supplies, the sounds of care, and the item assets bound to animal item ids. Create it with Tools ▸
/// SimpleMovements ▸ Animals ▸ Create Animal Database (it lives at Resources/SimpleMovements/Settings/AnimalDatabase);
/// without one the built-in rules and content are used. Adding a species, a breed, a food, an illness, a medicine or a
/// tool is adding data - nothing in the rules names one.
/// <para>A species asset made before Phase 5 (its Data Version below 5) takes what it lacks from the built-in species of its
/// id, with a warning, until it is updated: below 2 its care, its susceptibility and an anatomy it left empty (or at its
/// Phase 1 default), below 3 its products.</para>
/// </summary>
[CreateAssetMenu(fileName = "AnimalDatabase", menuName = "SimpleMovements/Animals/Animal Database", order = 0)]
public class AnimalDatabase : ScriptableObject, IAnimalCatalog
{
    public const string ResourcesName = "AnimalDatabase";
    /// <summary>The content version of the built-in species (5: Phase 5's egg trays and breed yields; 4: Phase 4's breeding, behaviour and perks; 3: Phase 3's products; 2: Phase 2's care, susceptibility and farm anatomies).</summary>
    public const int SpeciesDataVersion = 5;

    [Tooltip("The rules of animal husbandry (time, owners away, nutrition, feeding, care, habitat, comfort, welfare, health, the veterinarian, remains, limits, history, boarding, saving).")]
    public AnimalRules rules = AnimalRules.Defaults();

    [Header("Species")]
    [Tooltip("Animal species. One with the id of a built-in species replaces it.")]
    public List<AnimalSpecies> species = new List<AnimalSpecies>();
    [Tooltip("Use the built-in species (chicken, duck, rabbit, sheep, goat, pig, cow, horse) for the ids no asset gives.")]
    public bool includeBuiltInSpecies = true;

    [Header("Foods")]
    [Tooltip("Foods animals can be given. One with the id of a built-in food replaces it.")]
    public List<AnimalFood> foods = new List<AnimalFood>();
    [Tooltip("Use the built-in foods (hay, grass, grain, feeds, the farm's produce, meat...) for the ids no asset gives.")]
    public bool includeBuiltInFoods = true;

    [Header("Illnesses")]
    [Tooltip("Illnesses: what brings each on, its stages and symptoms, how it worsens and mends. One with the id of a built-in illness replaces it (in its place).")]
    public List<AnimalConditionData> conditions = new List<AnimalConditionData>();
    [Tooltip("Use the built-in illnesses (malnutrition, dehydration, infection, digestive upset, worms and lice, chronic stress, injury, hoof rot, food poisoning) for the ids no entry gives.")]
    public bool includeBuiltInConditions = true;

    [Header("Medicines")]
    [Tooltip("Medicines: the items that are each (a brewed potion of an Alchemy outcome, a plain remedy) and what it does. One with the id of a built-in medicine replaces it.")]
    public List<AnimalTreatmentData> treatments = new List<AnimalTreatmentData>();
    [Tooltip("Use the built-in medicines (Alchemy's potions that help animals, the three veterinary outcomes, charcoal, wormwood, vinegar, lavender) for the ids no entry gives.")]
    public bool includeBuiltInTreatments = true;

    [Header("Care tools and supplies")]
    [Tooltip("Care tools (brushes, a hoof pick, a pitchfork) and supplies (soap, straw), by the item keys that are them. One with the id of a built-in tool replaces it.")]
    public List<AnimalToolData> tools = new List<AnimalToolData>();
    [Tooltip("Use the built-in tools and supplies (soft brush, curry comb, hoof pick, pitchfork; the farm's shovel and straw; soap) for the ids no entry gives.")]
    public bool includeBuiltInTools = true;

    [Header("Sounds")]
    [Tooltip("What care, work and the animals themselves sound like (an empty slot is silent).")]
    public AnimalSounds sounds = new AnimalSounds();

    [Header("Items")]
    [Tooltip("Item assets given for animal item ids. Ids without one get an item made at runtime.")]
    public List<AnimalItemBinding> items = new List<AnimalItemBinding>();

    [NonSerialized] private AnimalCatalog compiled;
    [NonSerialized] private int version, compiledVersion = -1;

    // ------------------------------------------------------------------ instance
    private static AnimalDatabase instance;
    private static bool searched;
    /// <summary>Species assets already warned about at runtime for predating Phase 5 (once each per play).</summary>
    private static readonly HashSet<string> warnedOld = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static bool HasInstance => instance != null;

    /// <summary>The project's database (Resources/SimpleMovements/Settings/AnimalDatabase), or the built-in rules and content.</summary>
    public static AnimalDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                if (!searched)
                {
                    searched = true;
                    instance = SMAssets.Load<AnimalDatabase>(SMAssets.Settings, ResourcesName);
                }
                if (instance == null)
                {
                    instance = CreateInstance<AnimalDatabase>();
                    instance.name = "AnimalDatabase (built-in)";
                    instance.hideFlags = HideFlags.DontSave;
                }
            }
            return instance;
        }
    }

    /// <summary>Uses a specific database (tests, bootstrap scripts). Null = find it again.</summary>
    public static void Override(AnimalDatabase db)
    {
        instance = db;
        searched = db != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        searched = false;
        warnedOld.Clear();
    }

    public AnimalRules Rules => rules ??= AnimalRules.Defaults();

    private void OnValidate() => MarkChanged();

    /// <summary>Forget the compiled content (call after changing species, foods, illnesses, medicines or tools from code).</summary>
    public void MarkChanged() => version++;

    // ------------------------------------------------------------------ content
    private AnimalCatalog Catalog
    {
        get
        {
            if (compiled != null && compiledVersion == version) return compiled;
            var cat = new AnimalCatalog(Rules, null, null);
            if (includeBuiltInSpecies)
                foreach (AnimalSpeciesData s in AnimalDefaults.Species()) cat.AddSpecies(s);
            List<AnimalSpeciesData> builtIn = null;
            if (species != null)
                foreach (AnimalSpecies s in species)
                {
                    if (s == null || s.data == null || !AnimalIds.ValidKey(s.data.id)) continue;
                    AnimalSpeciesData data = s.data;
                    if (data.dataVersion < SpeciesDataVersion)
                    {
                        // Made before Phase 5: what it lacks comes from the built-in species of its id (on a copy: the asset is never changed here).
                        builtIn ??= AnimalDefaults.Species();
                        AnimalSpeciesData copy = Copy(data);
                        if (copy != null && UpdateFromBuiltIn(copy, builtIn))
                        {
                            data = copy;
                            if (Application.isPlaying && warnedOld.Add(s.data.id))
                                Debug.LogWarning($"Animals: the species asset '{s.name}' was made before Phase 5: its {OldDataWhat(s.data.dataVersion)} are taken from the built-in '{s.data.id}' (Animals ▸ Update Species Assets To Phase 5 writes them into it).", s);
                        }
                    }
                    cat.AddSpecies(data);
                }
            if (includeBuiltInFoods)
                foreach (AnimalFoodData f in AnimalDefaults.Foods()) cat.AddFood(f);
            if (foods != null)
                foreach (AnimalFood f in foods)
                    if (f != null && f.data != null && AnimalIds.ValidKey(f.data.id)) cat.AddFood(f.data);
            if (includeBuiltInConditions)
                foreach (AnimalConditionData c in AnimalDefaults.Conditions()) cat.AddCondition(c);
            if (conditions != null)
                foreach (AnimalConditionData c in conditions)
                    if (c != null && AnimalIds.ValidKey(c.id)) cat.AddCondition(c);
            if (includeBuiltInTreatments)
                foreach (AnimalTreatmentData t in AnimalDefaults.Treatments()) cat.AddTreatment(t);
            if (treatments != null)
                foreach (AnimalTreatmentData t in treatments)
                    if (t != null && AnimalIds.ValidKey(t.id)) cat.AddTreatment(t);
            if (includeBuiltInTools)
                foreach (AnimalToolData t in AnimalDefaults.Tools()) cat.AddTool(t);
            if (tools != null)
                foreach (AnimalToolData t in tools)
                    if (t != null && AnimalIds.ValidKey(t.id)) cat.AddTool(t);
            compiled = cat;
            compiledVersion = version;
            return cat;
        }
    }

    public IReadOnlyList<AnimalSpeciesData> AllSpecies => Catalog.AllSpecies;
    public IReadOnlyList<AnimalFoodData> AllFoods => Catalog.AllFoods;
    public AnimalSpeciesData Species(string id) => Catalog.Species(id?.Trim());
    public AnimalFoodData Food(string id) => Catalog.Food(id?.Trim());
    public AnimalFoodData FoodOfItem(string itemId) => Catalog.FoodOfItem(itemId);

    public IReadOnlyList<AnimalConditionData> AllConditions => Catalog.AllConditions;
    public AnimalConditionData Condition(string id) => Catalog.Condition(id?.Trim());
    public IReadOnlyList<AnimalTreatmentData> AllTreatments => Catalog.AllTreatments;
    public AnimalTreatmentData Treatment(string id) => Catalog.Treatment(id?.Trim());
    /// <summary>The medicine an item is (by its key: "potion:antidote", "ingredient:charcoal"; see <see cref="AnimalToolkit.KeyOf"/>), or null.</summary>
    public AnimalTreatmentData TreatmentOfItem(string itemKey) => Catalog.TreatmentOfItem(itemKey);
    public IReadOnlyList<AnimalToolData> AllTools => Catalog.AllTools;
    public AnimalToolData Tool(string id) => Catalog.Tool(id?.Trim());
    /// <summary>The care tool or supply an item is (by its key: "animal:tool/soft_brush", "agri:tool/shovel", "craftstock:Supply:soap"), or null.</summary>
    public AnimalToolData ToolOfItem(string itemKey) => Catalog.ToolOfItem(itemKey);

    /// <summary>The model a species' animals are shown with (its asset's Body Prefab), or null for a placeholder body.</summary>
    public GameObject BodyPrefab(string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId) || species == null) return null;
        foreach (AnimalSpecies s in species)
            if (s != null && s.bodyPrefab != null && s.data != null && string.Equals(s.data.id, speciesId, StringComparison.OrdinalIgnoreCase))
                return s.bodyPrefab;
        return null;
    }

    /// <summary>The item asset bound to an animal item id, or null.</summary>
    public ItemSO BoundItem(string id)
    {
        if (string.IsNullOrEmpty(id) || items == null) return null;
        foreach (AnimalItemBinding b in items)
            if (b != null && b.item != null && string.Equals(b.id, id, StringComparison.OrdinalIgnoreCase))
                return b.item;
        return null;
    }

    // ------------------------------------------------------------------ species made before Phase 5
    /// <summary>
    /// Brings a species' data up to date from the built-in species of its id, in place: before Data Version 2 its care, its
    /// susceptibility, and an anatomy left empty or at its Phase 1 default ("sheep" for the goat, "boar" for the pig); before
    /// 3 its products (unless it lists its own) and its seniors' production; before 4 its breeding, behaviour and perks
    /// (unless it lists its own perks); before 5 its egg-tray products and its breeds' yields (a breed naming its own
    /// specialty keeps it); its Data Version becomes <see cref="SpeciesDataVersion"/>. False when no built-in species has its
    /// id (nothing is changed). The editor's "Update Species Assets To Phase 5" can write a species asset's data with it.
    /// </summary>
    public static bool UpdateFromBuiltIn(AnimalSpeciesData data) => UpdateFromBuiltIn(data, AnimalDefaults.Species());

    private static bool UpdateFromBuiltIn(AnimalSpeciesData data, List<AnimalSpeciesData> builtIn)
    {
        AnimalSpeciesData twin = BuiltInTwin(data, builtIn);
        if (twin == null) return false;
        if (data.dataVersion < 2)
        {
            data.care = twin.care ?? new AnimalCareSpec();
            data.susceptibility = twin.susceptibility ?? new List<AnimalSusceptibility>();
            if (string.IsNullOrWhiteSpace(data.harvestAnatomy) || IsPhase1Anatomy(data.id, data.harvestAnatomy)) data.harvestAnatomy = twin.harvestAnatomy ?? "";
        }
        // Phase 3: what it gives (an asset that already lists its own products keeps them), and its productive stages.
        if (data.dataVersion < 3)
        {
            if (data.products == null || data.products.Count == 0) data.products = twin.products ?? new List<AnimalProductSpec>();
            if (data.stages != null && twin.stages != null)
                foreach (AnimalStageSpec st in data.stages)
                {
                    AnimalStageSpec t = twin.Stage(st.stage);
                    if (st != null && t != null && st.stage == GrowthStage.Senior && t.productionEnabled) st.productionEnabled = true;
                }
        }
        // Phase 4: how it breeds, behaves and bonds (perks an asset lists itself are kept).
        if (data.dataVersion < 4)
        {
            data.breeding = twin.breeding ?? new AnimalBreedingSpec();
            data.behaviour = twin.behaviour ?? new AnimalBehaviourSpec();
            if (data.perks == null || data.perks.Count == 0) data.perks = twin.perks ?? new List<AnimalAffinityPerk>();
        }
        // Phase 5: which of its products roll into egg trays, and what its breeds are bred for (a breed the built-in species
        // has not, or one that names its own specialty, is kept as it is).
        if (data.dataVersion < 5)
        {
            if (data.products != null && twin.products != null)
                foreach (AnimalProductSpec p in data.products)
                {
                    AnimalProductSpec t = p != null ? AnimalProduction.Spec(twin, p.id) : null;
                    if (t != null && t.trays) p.trays = true;
                }
            if (data.breeds != null && twin.breeds != null)
                foreach (AnimalBreedData b in data.breeds)
                {
                    if (b == null || !string.IsNullOrWhiteSpace(b.specialty)) continue;
                    AnimalBreedData t = twin.breeds.Find(x => x != null && x.id == b.id);
                    if (t == null) continue;
                    b.specialty = t.specialty ?? "";
                    b.productYield = t.productYield;
                }
        }
        data.dataVersion = Math.Max(data.dataVersion, SpeciesDataVersion);
        return true;
    }

    /// <summary>What a species asset of an older Data Version takes from the built-in species of its id ("care, susceptibility, anatomy and products").</summary>
    public static string OldDataWhat(int dataVersion) =>
        dataVersion < 2 ? "care, susceptibility, anatomy, products, breeding, behaviour, perks and breed yields"
        : dataVersion < 3 ? "products, breeding, behaviour, perks and breed yields"
        : dataVersion < 4 ? "breeding, behaviour, perks and breed yields" : "egg trays and breed yields";

    private static AnimalSpeciesData BuiltInTwin(AnimalSpeciesData data, List<AnimalSpeciesData> builtIn)
    {
        if (data == null || builtIn == null || string.IsNullOrWhiteSpace(data.id)) return null;
        foreach (AnimalSpeciesData b in builtIn)
            if (b != null && string.Equals(b.id, data.id.Trim(), StringComparison.OrdinalIgnoreCase)) return b;
        return null;
    }

    /// <summary>The anatomy Phase 1 gave a species when it had none of its own (the goat was butchered as a sheep, the pig as a boar).</summary>
    private static bool IsPhase1Anatomy(string speciesId, string anatomy)
    {
        string s = (speciesId ?? "").Trim(), a = (anatomy ?? "").Trim();
        return (string.Equals(s, "goat", StringComparison.OrdinalIgnoreCase) && string.Equals(a, "sheep", StringComparison.OrdinalIgnoreCase))
               || (string.Equals(s, "pig", StringComparison.OrdinalIgnoreCase) && string.Equals(a, "boar", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A deep copy of a species' data (its asset is never changed by the database).</summary>
    private static AnimalSpeciesData Copy(AnimalSpeciesData data)
    {
        if (data == null) return null;
        try
        {
            return JsonUtility.FromJson<AnimalSpeciesData>(JsonUtility.ToJson(data));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Animals: could not copy species '{data.id}' ({e.Message}).");
            return null;
        }
    }

    // ------------------------------------------------------------------ checks
    /// <summary>
    /// Setup problems: the rules, the welfare and quality formulas, each species (and those made before Phase 5), each food,
    /// the illnesses, medicines and tools of the database, the item bindings - and whether they fit together: the potions
    /// the medicines name are Alchemy outcomes, the tools the species' care and products need exist, the illnesses they are
    /// prone to exist, their remains have a Harvesting anatomy, every product has an item, and pens can be mucked out and
    /// bedded.
    /// </summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<AnimalSpeciesData> builtIn = null;
        if (species != null)
            foreach (AnimalSpecies s in species)
            {
                if (s == null || s.data == null) { warnings.Add("An empty entry in Species."); continue; }
                if (!AnimalIds.ValidKey(s.data.id)) errors.Add($"{s.name}: the id '{s.data.id}' may not be empty or hold spaces, '/', ':' or '|'.");
                if (!ids.Add(s.data.id)) errors.Add($"Two species are called '{s.data.id}'.");
                AnimalSpeciesData check = s.data;
                if (s.data.dataVersion < SpeciesDataVersion)
                {
                    builtIn ??= AnimalDefaults.Species();
                    AnimalSpeciesData copy = BuiltInTwin(s.data, builtIn) != null ? Copy(s.data) : null;
                    if (copy != null && UpdateFromBuiltIn(copy, builtIn))
                    {
                        check = copy;
                        warnings.Add($"{s.name}: made before Phase 5 (Data Version {s.data.dataVersion}): its {OldDataWhat(s.data.dataVersion)} are taken from the built-in '{s.data.id}' until it is updated (Animals ▸ Update Species Assets To Phase 5).");
                    }
                    else
                        warnings.Add($"{s.name}: made before Phase 5 (Data Version {s.data.dataVersion}) and no built-in species has its id: give it its {OldDataWhat(s.data.dataVersion)}, then set its Data Version to {SpeciesDataVersion}.");
                }
                var e = new List<string>();
                var w = new List<string>();
                check.Validate(e, w);
                foreach (string x in e) errors.Add($"{s.name}: {x}");
                foreach (string x in w) warnings.Add($"{s.name}: {x}");
            }
        var foodIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (foods != null)
            foreach (AnimalFood f in foods)
            {
                if (f == null || f.data == null) { warnings.Add("An empty entry in Foods."); continue; }
                if (!AnimalIds.ValidKey(f.data.id)) errors.Add($"{f.name}: the id '{f.data.id}' may not be empty or hold spaces, '/', ':' or '|'.");
                if (!foodIds.Add(f.data.id)) errors.Add($"Two foods are called '{f.data.id}'.");
                var e = new List<string>();
                var w = new List<string>();
                f.data.Validate(e, w);
                foreach (string x in e) errors.Add($"{f.name}: {x}");
                foreach (string x in w) warnings.Add($"{f.name}: {x}");
            }
        // Every food a diet names should exist (a typo would silently make it Inedible).
        foreach (AnimalSpeciesData s in AllSpecies)
        {
            if (s.diet == null) continue;
            for (int a = (int)FoodAffinity.Favorite; a <= (int)FoodAffinity.Toxic; a++)
                foreach (string entry in s.diet.Entries((FoodAffinity)a))
                    if (!string.IsNullOrEmpty(entry) && entry[0] != '#' && Food(entry) == null)
                        warnings.Add($"Species '{s.id}': its diet names '{entry}', which is no food.");
        }
        ValidateHealth(errors, warnings);
        if (items != null)
            foreach (AnimalItemBinding b in items)
            {
                if (b == null) continue;
                if (!AnimalIds.Parse(b.id, out string kind, out string key))
                {
                    errors.Add($"Item binding '{b.id}': not an animal item id (animal:feed/hay, animal:tool/soft_brush...).");
                    continue;
                }
                if (kind == AnimalIds.ToolKind && Tool(key) == null) warnings.Add($"Item binding {b.id}: there is no care tool '{key}'.");
                if (kind == AnimalIds.ProductKind && AnimalProducts.Spec(key) == null) warnings.Add($"Item binding {b.id}: no species gives '{key}'.");
                if (b.item == null) warnings.Add($"Item binding {b.id} has no item.");
                else
                {
                    string itemKey = AnimalToolkit.KeyOf(b.item);
                    if (!string.Equals(itemKey, b.id, StringComparison.OrdinalIgnoreCase))
                        warnings.Add($"Item binding {b.id}: its item '{b.item.name}' goes by '{itemKey}', so it is not recognised as {b.id} (use an animal item of that id).");
                }
            }
        if (AllSpecies.Count == 0) warnings.Add("No species: add species assets or turn Include Built In Species on.");
        AnimalRules r = Rules;
        if (r.tickHours < r.stepHours) warnings.Add("Tick Hours is shorter than Step Hours: pens update more often than they step.");
        if (r.hideRadius < r.showRadius) warnings.Add("Hide Radius is below Show Radius: bodies would flicker at the edge.");
        if (r.toxicBelow > r.harmfulBelow) warnings.Add("Toxic Below is above Harmful Below: hungry animals would take poison before an upset stomach.");
        AnimalWelfare.Validate(r.welfare, errors, warnings);
        r.production?.Validate(errors, warnings);
        r.breeding?.Validate(errors, warnings);
        r.social?.Validate(errors, warnings);
        r.upgrades?.Validate(errors, warnings);
    }

    /// <summary>The illnesses, medicines and tools of the database, and how the content fits together.</summary>
    private void ValidateHealth(List<string> errors, List<string> warnings)
    {
        CheckIds("Illnesses", conditions, c => c.id, errors, warnings);
        CheckIds("Medicines", treatments, t => t.id, errors, warnings);
        CheckIds("Care tools", tools, t => t.id, errors, warnings);
        // The database's own entries (the built-in ones are checked by the tests).
        if (conditions != null)
            foreach (AnimalConditionData c in conditions)
                if (c != null) c.Validate(errors, warnings);
        if (treatments != null)
            foreach (AnimalTreatmentData t in treatments)
                if (t != null) t.Validate(this, errors, warnings);
        if (tools != null)
            foreach (AnimalToolData t in tools)
                if (t != null) t.Validate(errors, warnings);
        if (AllConditions.Count == 0) warnings.Add("No illnesses: animals never fall ill (turn Include Built In Conditions on, or add some).");

        // The potions the medicines name are outcomes Alchemy can brew (the veterinary ones are installed first).
        try
        {
            AnimalAlchemy.EnsureInstalled();
            foreach (AnimalTreatmentData t in AllTreatments)
            {
                if (t?.items == null) continue;
                foreach (string key in t.items)
                    if (PotionOutcome(key, out string outcome) && !AnimalAlchemy.OutcomeExists(outcome))
                        warnings.Add($"Medicine '{t.id}': '{key}' names '{outcome}', which is no Alchemy outcome (it can never be brewed).");
            }
        }
        catch (Exception e)
        {
            warnings.Add($"Medicines: the Alchemy outcomes they name could not be checked ({e.Message}).");
        }

        // What each species' care needs, what it is prone to, and how its remains are butchered.
        foreach (AnimalSpeciesData s in AllSpecies)
        {
            if (s == null) continue;
            if (s.care?.actions != null)
                foreach (AnimalCareActionSpec a in s.care.actions)
                {
                    if (a == null) continue;
                    if (a.tools != null && a.tools.Count > 0 && !AnyTool(a.tools, false))
                        warnings.Add($"Species '{s.id}': '{a.Id}' needs a {Words(a.tools)}, which no care tool is (it can never be done).");
                    if (a.TakesSupply && !AnyTool(new[] { a.supply }, true))
                        warnings.Add($"Species '{s.id}': '{a.Id}' takes {a.supply}, which no supply is.");
                }
            if (s.susceptibility != null)
                foreach (AnimalSusceptibility x in s.susceptibility)
                    if (x != null && !string.IsNullOrWhiteSpace(x.condition) && Condition(x.condition) == null)
                        warnings.Add($"Species '{s.id}': it is said to be prone to '{x.condition}', which is no illness.");
            if (!string.IsNullOrWhiteSpace(s.harvestAnatomy))
            {
                try
                {
                    if (HarvestDatabase.Instance.Anatomy(s.harvestAnatomy) == null)
                        warnings.Add($"Species '{s.id}': its remains are butchered as '{s.harvestAnatomy}', which is no Harvesting anatomy (its carcass cannot be laid out).");
                }
                catch (Exception e)
                {
                    warnings.Add($"Species '{s.id}': its anatomy could not be checked ({e.Message}).");
                }
            }
        }
        if (!AnyTool(AnimalCommands.MuckOutTools, false)) warnings.Add("No care tool is a fork or a shovel: no pen can be mucked out.");
        if (!AnyTool(new[] { "bedding" }, true)) warnings.Add("No supply is bedding: no straw can be laid in a bedded pen.");
        ValidateProducts(warnings);
        ValidateBreeding(warnings);
    }

    /// <summary>What breeding takes is an item the game has (a typo would make every breeding refused).</summary>
    private void ValidateBreeding(List<string> warnings)
    {
        foreach (AnimalSpeciesData s in AllSpecies)
        {
            AnimalBreedingSpec b = s?.breeding;
            if (b == null || !b.enabled || b.costCount <= 0 || string.IsNullOrWhiteSpace(b.costItem)) continue;
            try
            {
                if (AnimalToolkit.ItemOfKey(b.costItem.Trim()) == null)
                    warnings.Add($"Species '{s.id}': breeding takes '{b.costItem}', which is no item (it could never be bred).");
            }
            catch (Exception e)
            {
                warnings.Add($"Species '{s.id}': its breeding cost could not be checked ({e.Message}).");
            }
        }
    }

    /// <summary>
    /// What the species give: the tools collecting needs exist, every product has an item (made at runtime, or bound), and a
    /// product given by two species is the same product (its item is made from the first species that gives it).
    /// </summary>
    private void ValidateProducts(List<string> warnings)
    {
        var first = new Dictionary<string, (string species, AnimalProductSpec spec)>(StringComparer.OrdinalIgnoreCase);
        foreach (AnimalSpeciesData s in AllSpecies)
        {
            if (s?.products == null) continue;
            foreach (AnimalProductSpec p in s.products)
            {
                if (p == null || !AnimalIds.ValidKey(p.id)) continue;
                if (p.NeedsTool && !AnyTool(p.tools, false))
                    warnings.Add($"Species '{s.id}': collecting '{p.id}' needs a {Words(p.tools)}, which no tool is (it can never be collected).");
                if (first.TryGetValue(p.id, out var had))
                {
                    if (had.spec.edible != p.edible || had.spec.drink != p.drink || !string.Equals(had.spec.DisplayName, p.DisplayName, StringComparison.Ordinal))
                        warnings.Add($"Species '{s.id}': its product '{p.id}' differs from that of '{had.species}' (name, food or drink): its item is made from '{had.species}''s, so give it its own id.");
                    continue;
                }
                first[p.id] = (s.id, p);
                ItemSO item = null;
                try { item = AnimalItems.Product(p.id); }
                catch (Exception e) { warnings.Add($"Product '{p.id}': its item could not be made ({e.Message})."); continue; }
                if (item == null) warnings.Add($"Product '{p.id}' has no item: it could never be collected.");
            }
        }
    }

    private static void CheckIds<T>(string what, List<T> list, Func<T, string> idOf, List<string> errors, List<string> warnings) where T : class
    {
        if (list == null) return;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < list.Count; i++)
        {
            T x = list[i];
            if (x == null)
            {
                warnings.Add($"An empty entry in {what}.");
                continue;
            }
            string id = idOf(x);
            if (!AnimalIds.ValidKey(id))
            {
                errors.Add($"{what} #{i + 1}: the id '{id}' may not be empty or hold spaces, '/', ':' or '|' (it is left out).");
                continue;
            }
            if (!seen.Add(id)) errors.Add($"Two entries of {what} are called '{id}' (the last one counts).");
        }
    }

    /// <summary>Is some care tool (<paramref name="consumed"/> false) or supply (true) one of these tags?</summary>
    private bool AnyTool(IReadOnlyList<string> tags, bool consumed)
    {
        if (tags == null || tags.Count == 0) return false;
        foreach (AnimalToolData t in AllTools)
            if (t != null && t.consumed == consumed && t.HasAny(tags)) return true;
        return false;
    }

    /// <summary>The outcome a "potion:&lt;outcome&gt;" key names (not a prefix key).</summary>
    private static bool PotionOutcome(string key, out string outcome)
    {
        outcome = null;
        const string prefix = "potion:";
        if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix, StringComparison.Ordinal) || key.EndsWith("*", StringComparison.Ordinal)) return false;
        outcome = key.Substring(prefix.Length);
        int hash = outcome.IndexOf('#');
        if (hash >= 0) outcome = outcome.Substring(0, hash);
        return outcome.Length > 0;
    }

    /// <summary>Tool tags in words ("brush or curry comb").</summary>
    private static string Words(List<string> tags)
    {
        var parts = new List<string>();
        foreach (string t in tags)
            if (!string.IsNullOrWhiteSpace(t)) parts.Add(t.Trim().Replace('_', ' '));
        return string.Join(" or ", parts);
    }
}
