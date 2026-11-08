using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An item asset given for an agricultural item id (instead of the item made at runtime).</summary>
[Serializable]
public class AgriItemBinding
{
    [Tooltip("The id: agri:seed/<species>, agri:produce/<species>, agri:amend/<material>, agri:tool/<tool>, agri:instr/<instrument>, agri:water/<container>.")]
    public string id = "";
    public ItemSO item;
}

/// <summary>
/// The project's agriculture: the rules (time, calendar, the city's climate, soil chemistry, pests, genetics, skills,
/// saving), the plant species, the soil materials, the hybrids, and the item assets bound to agricultural ids.
/// Create it with Tools ▸ SimpleMovements ▸ Agriculture ▸ Create Agriculture Database (it lives at
/// Resources/SimpleMovements/Settings/AgricultureDatabase); without one the built-in rules and content are used.
/// </summary>
[CreateAssetMenu(fileName = "AgricultureDatabase", menuName = "SimpleMovements/Agriculture/Agriculture Database", order = 0)]
public class AgricultureDatabase : ScriptableObject, IAgriCatalog
{
    public const string ResourcesName = "AgricultureDatabase";

    [Tooltip("The rules of agriculture.")]
    public AgriSettings settings = AgriSettings.Defaults();

    [Header("Plants")]
    [Tooltip("Plant species. One with the id of a built-in species replaces it.")]
    public List<PlantSpecies> species = new List<PlantSpecies>();
    [Tooltip("Use the built-in species (wheat, carrot, cabbage, bean, potato, tomato, strawberry, apple, grape, chamomile, comfrey, " +
             "lavender, marigold, mandrake, nightshade, moonpetal, emberroot, frostbloom, twilight bloom) for the ids no asset gives.")]
    public bool includeBuiltInSpecies = true;
    [Tooltip("Crosses that sometimes give seeds of another species.")]
    public List<HybridRule> hybrids = new List<HybridRule>();
    public bool includeBuiltInHybrids = true;

    [Header("Soil materials")]
    [Tooltip("Materials (fertilizers, amendments, treatments). One with the id of a built-in material replaces it.")]
    public List<SoilAmendmentSO> amendments = new List<SoilAmendmentSO>();
    public bool includeBuiltInAmendments = true;

    [Header("Items")]
    [Tooltip("Item assets given for agricultural ids. Ids without one get an item made at runtime.")]
    public List<AgriItemBinding> items = new List<AgriItemBinding>();

    [NonSerialized] private List<PlantSpeciesData> compiledSpecies;
    [NonSerialized] private Dictionary<string, PlantSpeciesData> speciesById;
    [NonSerialized] private List<SoilAmendmentData> compiledAmendments;
    [NonSerialized] private Dictionary<string, int> amendmentIndex;
    [NonSerialized] private List<HybridRule> compiledHybrids;
    [NonSerialized] private int version, compiledVersion = -1;

    // ------------------------------------------------------------------ instance
    private static AgricultureDatabase instance;
    private static bool searched;

    public static bool HasInstance => instance != null;

    /// <summary>The project's database (Resources/SimpleMovements/Settings/AgricultureDatabase), or the built-in rules and content.</summary>
    public static AgricultureDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                if (!searched)
                {
                    searched = true;
                    instance = SMAssets.Load<AgricultureDatabase>(SMAssets.Settings, ResourcesName);
                }
                if (instance == null)
                {
                    instance = CreateInstance<AgricultureDatabase>();
                    instance.name = "AgricultureDatabase (built-in)";
                    instance.hideFlags = HideFlags.DontSave;
                }
            }
            return instance;
        }
    }

    /// <summary>Uses a specific database (tests, bootstrap scripts). Null = find it again.</summary>
    public static void Override(AgricultureDatabase db)
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

    public AgriSettings Settings => settings ??= AgriSettings.Defaults();

    private void OnValidate() => MarkChanged();

    /// <summary>Forget the compiled content (call after changing species or materials from code).</summary>
    public void MarkChanged() => version++;

    // ------------------------------------------------------------------ content
    private void Compile()
    {
        if (compiledSpecies != null && compiledVersion == version)
            return;
        compiledSpecies = new List<PlantSpeciesData>();
        speciesById = new Dictionary<string, PlantSpeciesData>(StringComparer.OrdinalIgnoreCase);
        if (species != null)
            foreach (PlantSpecies s in species)
                if (s != null && s.data != null && !string.IsNullOrWhiteSpace(s.data.id) && !speciesById.ContainsKey(s.data.id))
                {
                    compiledSpecies.Add(s.data);
                    speciesById[s.data.id] = s.data;
                }
        if (includeBuiltInSpecies)
            foreach (PlantSpeciesData s in AgriDefaults.Species())
                if (!speciesById.ContainsKey(s.id))
                {
                    compiledSpecies.Add(s);
                    speciesById[s.id] = s;
                }

        compiledAmendments = new List<SoilAmendmentData>();
        amendmentIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (amendments != null)
            foreach (SoilAmendmentSO a in amendments)
                if (a != null && a.data != null && !string.IsNullOrWhiteSpace(a.data.id) && !amendmentIndex.ContainsKey(a.data.id))
                {
                    amendmentIndex[a.data.id] = compiledAmendments.Count;
                    compiledAmendments.Add(a.data);
                }
        if (includeBuiltInAmendments)
            foreach (SoilAmendmentData a in AgriDefaults.Amendments())
                if (!amendmentIndex.ContainsKey(a.id))
                {
                    amendmentIndex[a.id] = compiledAmendments.Count;
                    compiledAmendments.Add(a);
                }

        compiledHybrids = new List<HybridRule>();
        if (hybrids != null)
            foreach (HybridRule h in hybrids)
                if (h != null) compiledHybrids.Add(h);
        if (includeBuiltInHybrids)
            compiledHybrids.AddRange(AgriDefaults.Hybrids());
        compiledVersion = version;
    }

    public IReadOnlyList<PlantSpeciesData> AllSpecies
    {
        get
        {
            Compile();
            return compiledSpecies;
        }
    }

    public IReadOnlyList<SoilAmendmentData> AllAmendments
    {
        get
        {
            Compile();
            return compiledAmendments;
        }
    }

    public IReadOnlyList<HybridRule> Hybrids
    {
        get
        {
            Compile();
            return compiledHybrids;
        }
    }

    public PlantSpeciesData Species(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        Compile();
        return speciesById.TryGetValue(id.Trim(), out PlantSpeciesData s) ? s : null;
    }

    public int AmendmentIndex(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return -1;
        Compile();
        return amendmentIndex.TryGetValue(id.Trim(), out int i) ? i : -1;
    }

    public SoilAmendmentData Amendment(int index)
    {
        Compile();
        return index >= 0 && index < compiledAmendments.Count ? compiledAmendments[index] : null;
    }

    public SoilAmendmentData Amendment(string id) => Amendment(AmendmentIndex(id));

    /// <summary>The item asset bound to an agricultural id (or an amendment asset of the database), or null.</summary>
    public ItemSO BoundItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (items != null)
            foreach (AgriItemBinding b in items)
                if (b != null && b.item != null && string.Equals(b.id, id, StringComparison.OrdinalIgnoreCase))
                    return b.item;
        if (amendments != null && AgriIds.Parse(id, out string kind, out string key) && kind == "amend")
            foreach (SoilAmendmentSO a in amendments)
                if (a != null && a.data != null && string.Equals(a.data.id, key, StringComparison.OrdinalIgnoreCase))
                    return a;
        return null;
    }

    /// <summary>Setup problems: the rules, each species, each material, the hybrids and the item bindings.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (species != null)
            foreach (PlantSpecies s in species)
            {
                if (s == null) { warnings.Add("An empty entry in Species."); continue; }
                if (!ids.Add(s.Id)) errors.Add($"Two species are called '{s.Id}'.");
                var e = new List<string>();
                var w = new List<string>();
                s.Validate(e, w);
                foreach (string x in e) errors.Add($"{s.name}: {x}");
                foreach (string x in w) warnings.Add($"{s.name}: {x}");
            }
        var amendmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (amendments != null)
            foreach (SoilAmendmentSO a in amendments)
            {
                if (a == null || a.data == null) { warnings.Add("An empty entry in Amendments."); continue; }
                if (!amendmentIds.Add(a.data.id)) errors.Add($"Two materials are called '{a.data.id}'.");
                var e = new List<string>();
                var w = new List<string>();
                a.data.Validate(e, w);
                foreach (string x in e) errors.Add($"{a.name}: {x}");
                foreach (string x in w) warnings.Add($"{a.name}: {x}");
            }
        foreach (HybridRule h in Hybrids)
        {
            if (Species(h.parentA) == null || Species(h.parentB) == null) errors.Add($"Hybrid {h.parentA} × {h.parentB}: a parent is not a species.");
            if (Species(h.child) == null) errors.Add($"Hybrid {h.parentA} × {h.parentB}: no species '{h.child}'.");
            else if (Species(h.parentA) != null && Species(h.parentB) != null &&
                     (string.IsNullOrEmpty(Species(h.parentA).crossGroup) || Species(h.parentA).crossGroup != Species(h.parentB).crossGroup))
                warnings.Add($"Hybrid {h.parentA} × {h.parentB}: the parents are not of one Cross Group, so they never pollinate each other.");
        }
        if (items != null)
            foreach (AgriItemBinding b in items)
            {
                if (b == null) continue;
                if (!AgriIds.Parse(b.id, out _, out _)) errors.Add($"Item binding '{b.id}': not an agricultural id (agri:seed/tomato...).");
                if (b.item == null) warnings.Add($"Item binding {b.id} has no item.");
            }
        if (AllSpecies.Count == 0) warnings.Add("No species: add species assets or turn Include Built In Species on.");
        AgriSettings st = Settings;
        if (st.tickHours > st.maxStepHours) warnings.Add("Tick Hours is longer than Max Step Hours: plants step in larger jumps than they update.");
    }
}
