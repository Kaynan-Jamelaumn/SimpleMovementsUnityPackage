using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An item asset given for one material of one creature (instead of the item made at runtime).</summary>
[Serializable]
public class HarvestItemBinding
{
    [Tooltip("The creature (anatomy id).")]
    public string anatomy = "";
    [Tooltip("Its material (id).")]
    public string material = "";
    [Tooltip("The item given. A Harvested Material / Harvested Food keeps each piece's quality; any other item is given plain.")]
    public ItemSO item;
}

/// <summary>
/// The project's harvesting: the rules (how hits mark bodies, spoiling, quality and yield, skills, the minigames'
/// margins), the creatures (Creature Anatomy assets, and the built-in ones), and the items given for their materials.
/// Create it with Tools ▸ SimpleMovements ▸ Harvesting ▸ Create Harvest Database (it lives at
/// Resources/SimpleMovements/Settings/HarvestDatabase); without one the built-in rules and creatures are used.
/// </summary>
[CreateAssetMenu(fileName = "HarvestDatabase", menuName = "SimpleMovements/Harvesting/Harvest Database", order = 0)]
public class HarvestDatabase : ScriptableObject
{
    public const string ResourcesName = "HarvestDatabase";

    [Tooltip("The rules of harvesting.")]
    public HarvestSettings settings = HarvestSettings.Defaults();

    [Header("Creatures")]
    [Tooltip("Creature anatomies. One with the id of a built-in creature replaces it.")]
    public List<CreatureAnatomy> anatomies = new List<CreatureAnatomy>();
    [Tooltip("Use the built-in creatures (bear, wolf, deer, boar, rabbit, sheep, drake, troll, eagle, salmon, giant spider, giant serpent) " +
             "for the ids no asset gives.")]
    public bool includeBuiltIn = true;

    [Header("Items")]
    [Tooltip("Item assets given for materials (by creature and material). Materials without one get an item made at runtime.")]
    public List<HarvestItemBinding> items = new List<HarvestItemBinding>();
    [Tooltip("Harvesting tools merchants and your scripts can hand out (the editor's Create Tools fills it). Not needed to harvest.")]
    public List<HarvestToolSO> tools = new List<HarvestToolSO>();

    [NonSerialized] private List<HarvestAnatomyData> compiled;
    [NonSerialized] private Dictionary<string, HarvestAnatomyData> byId;
    [NonSerialized] private int version, compiledVersion = -1;

    // ------------------------------------------------------------------ instance
    private static HarvestDatabase instance;
    private static bool searched;

    /// <summary>The project's database (Resources/SimpleMovements/Settings/HarvestDatabase), or the built-in rules and creatures.</summary>
    public static HarvestDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                if (!searched)
                {
                    searched = true;
                    instance = SMAssets.Load<HarvestDatabase>(SMAssets.Settings, ResourcesName);
                }
                if (instance == null)
                {
                    instance = CreateInstance<HarvestDatabase>();
                    instance.name = "HarvestDatabase (built-in)";
                    instance.hideFlags = HideFlags.DontSave;
                }
            }
            return instance;
        }
    }

    /// <summary>Uses a specific database (tests, bootstrap scripts). Null = find it again.</summary>
    public static void Override(HarvestDatabase db)
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

    public HarvestSettings Settings => settings ??= HarvestSettings.Defaults();

    private void OnValidate() => MarkChanged();

    /// <summary>Forget the compiled creatures (call after changing anatomies from code).</summary>
    public void MarkChanged() => version++;

    // ------------------------------------------------------------------ creatures
    /// <summary>Every creature: the assets, then the built-in ones no asset replaces.</summary>
    public IReadOnlyList<HarvestAnatomyData> Anatomies
    {
        get
        {
            Compile();
            return compiled;
        }
    }

    private void Compile()
    {
        if (compiled != null && compiledVersion == version)
            return;
        compiled = new List<HarvestAnatomyData>();
        byId = new Dictionary<string, HarvestAnatomyData>(StringComparer.OrdinalIgnoreCase);
        if (anatomies != null)
            foreach (CreatureAnatomy c in anatomies)
                if (c != null && c.data != null && !string.IsNullOrWhiteSpace(c.data.id) && !byId.ContainsKey(c.data.id))
                {
                    compiled.Add(c.data);
                    byId[c.data.id] = c.data;
                }
        if (includeBuiltIn)
            foreach (HarvestAnatomyData a in HarvestDefaults.Anatomies())
                if (!byId.ContainsKey(a.id))
                {
                    compiled.Add(a);
                    byId[a.id] = a;
                }
        compiledVersion = version;
    }

    /// <summary>The creature with <paramref name="id"/>, or null.</summary>
    public HarvestAnatomyData Anatomy(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        Compile();
        return byId.TryGetValue(id.Trim(), out HarvestAnatomyData a) ? a : null;
    }

    /// <summary>
    /// The creature a mob is: an anatomy asset listing its Mob Profile, else the first whose Mob Types match its type or its
    /// name (whole words: "Black Bear" is a bear, "Bearded Vulture" is not). Null when none.
    /// </summary>
    public HarvestAnatomyData AnatomyForMob(MobProfile profile, string mobType, string mobName)
    {
        if (profile != null && anatomies != null)
            foreach (CreatureAnatomy c in anatomies)
                if (c != null && c.data != null && c.mobProfiles != null && c.mobProfiles.Contains(profile))
                    return c.data;
        foreach (HarvestAnatomyData a in Anatomies)
            if (a.MatchesMob(mobType))
                return a;
        foreach (HarvestAnatomyData a in Anatomies)
            if (a.MatchesMob(null, mobName))
                return a;
        return null;
    }

    /// <summary>The item asset bound to a material, or null (an item is then made at runtime).</summary>
    public ItemSO BoundItem(string anatomy, string material)
    {
        if (items == null)
            return null;
        foreach (HarvestItemBinding b in items)
            if (b != null && b.item != null && string.Equals(b.anatomy, anatomy, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(b.material, material, StringComparison.OrdinalIgnoreCase))
                return b.item;
        return null;
    }

    /// <summary>Setup problems: the rules, each creature, the item bindings.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (anatomies != null)
            foreach (CreatureAnatomy c in anatomies)
            {
                if (c == null) { warnings.Add("An empty entry in Anatomies."); continue; }
                if (!ids.Add(c.Id)) errors.Add($"Two anatomies are called '{c.Id}'.");
                var e = new List<string>();
                var w = new List<string>();
                c.Validate(e, w);
                foreach (string x in e) errors.Add($"{c.name}: {x}");
                foreach (string x in w) warnings.Add($"{c.name}: {x}");
            }
        if (items != null)
            foreach (HarvestItemBinding b in items)
            {
                if (b == null) continue;
                HarvestAnatomyData a = Anatomy(b.anatomy);
                if (a == null) errors.Add($"Item binding: no creature '{b.anatomy}'.");
                else if (a.Material(b.material) == null) errors.Add($"Item binding: {a.Name} has no material '{b.material}'.");
                if (b.item == null) warnings.Add($"Item binding {b.anatomy}/{b.material} has no item.");
            }
        if (!includeBuiltIn && (anatomies == null || anatomies.Count == 0))
            warnings.Add("No creatures: add anatomies or turn Include Built In on.");
    }
}
