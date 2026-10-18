using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The list of every ability in the game, looked up by id. Saves store ability ids, so absorbed abilities can be
/// restored after loading. Optional: create one (Assets > Create > SimpleMovements > Abilities > Ability Database),
/// press "Collect All Abilities" in its inspector, and put it in a Resources folder named exactly "AbilityDatabase".
/// </summary>
[CreateAssetMenu(fileName = "AbilityDatabase", menuName = "SimpleMovements/Abilities/Ability Database", order = 10)]
public class AbilityDatabase : ScriptableObject
{
    [Tooltip("Every ability that can be saved, absorbed or looked up by id.")]
    public List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    private Dictionary<string, AbilityDefinition> byId;
    private static AbilityDatabase instance;
    private static bool searched;

    /// <summary>
    /// Definitions created in code at runtime (legacy conversions, mob basic attacks, presets) by id. Every machine
    /// creates the same ones with the same ids, so a client finds what the server names (see <see cref="Resolve"/>).
    /// </summary>
    private static readonly Dictionary<string, AbilityDefinition> runtimeById = new Dictionary<string, AbilityDefinition>();

    /// <summary>The database in Resources/SimpleMovements/Databases/AbilityDatabase (null if there is none).</summary>
    public static AbilityDatabase Instance
    {
        get
        {
            if (instance == null && !searched)
            {
                searched = true;
                instance = SMAssets.Load<AbilityDatabase>(SMAssets.Databases, "AbilityDatabase");
            }
            return instance;
        }
    }

    public static void Override(AbilityDatabase db)
    {
        instance = db;
        searched = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        searched = false;
        runtimeById.Clear();
        FallbackResolver = null;
    }

    /// <summary>
    /// Last resort of <see cref="Resolve"/> for ids nothing else knows: a system that creates definitions in code on
    /// demand (e.g. trait presets) can install a function that builds the definition for its id, so a client finds an
    /// ability the server created even when this machine has not created it yet. Null = none.
    /// </summary>
    public static Func<string, AbilityDefinition> FallbackResolver;

    /// <summary>
    /// The ability with this id from anywhere this machine knows: definitions created at runtime (legacy conversions,
    /// mob basic attacks, presets), the Ability Database (if there is one) and converted legacy abilities. Null when
    /// unknown. The network layer uses it to turn the ids the server sends back into abilities.
    /// </summary>
    public static AbilityDefinition Resolve(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (runtimeById.TryGetValue(id, out AbilityDefinition runtime))
        {
            if (runtime != null)
                return runtime;
            runtimeById.Remove(id); // destroyed
        }
        AbilityDatabase db = Instance;
        AbilityDefinition found = db != null ? db.Find(id) : null;
        if (found == null)
            found = LegacyAbilityConverter.FindConverted(id);
        if (found == null && FallbackResolver != null)
        {
            try
            {
                found = FallbackResolver(id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                found = null;
            }
            if (found != null && found.Id == id)
                RegisterRuntime(found);
        }
        return found;
    }

    /// <summary>
    /// Makes a definition created at runtime findable by <see cref="Resolve"/> (the first one registered under an id
    /// wins). Called automatically for definitions marked with <see cref="AbilityDefinition.InitializeRuntime"/>.
    /// </summary>
    public static void RegisterRuntime(AbilityDefinition ability)
    {
        if (ability == null || !Application.isPlaying)
            return;
        string id = ability.Id;
        if (string.IsNullOrEmpty(id))
            return;
        if (runtimeById.TryGetValue(id, out AbilityDefinition existing) && existing != null)
            return;
        runtimeById[id] = ability;
    }

    private void OnEnable() => byId = null;

    private void BuildIndex()
    {
        byId = new Dictionary<string, AbilityDefinition>();
        for (int i = 0; i < abilities.Count; i++)
        {
            AbilityDefinition a = abilities[i];
            if (a == null)
                continue;
            if (!byId.ContainsKey(a.Id))
                byId.Add(a.Id, a);
        }
    }

    /// <summary>The ability with this id (also finds converted legacy abilities), or null.</summary>
    public AbilityDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (byId == null)
            BuildIndex();
        if (byId.TryGetValue(id, out AbilityDefinition a) && a != null)
            return a;
        return LegacyAbilityConverter.FindConverted(id);
    }

    public bool Contains(AbilityDefinition ability) => ability != null && Find(ability.Id) == ability;

    /// <summary>Adds an ability at runtime (e.g. a converted legacy ability) so it can be found by id.</summary>
    public void Register(AbilityDefinition ability)
    {
        if (ability == null)
            return;
        if (byId == null)
            BuildIndex();
        if (byId.ContainsKey(ability.Id))
            return;
        byId.Add(ability.Id, ability);
        if (!ability.IsRuntimeCreated && !abilities.Contains(ability))
            abilities.Add(ability);
    }

    /// <summary>Every ability with <paramref name="tag"/>.</summary>
    public void WithTag(AbilityTag tag, List<AbilityDefinition> results)
    {
        results.Clear();
        for (int i = 0; i < abilities.Count; i++)
        {
            if (abilities[i] != null && abilities[i].HasTag(tag))
                results.Add(abilities[i]);
        }
    }

    /// <summary>Checks for empty entries, duplicate ids and invalid abilities.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        var seen = new Dictionary<string, AbilityDefinition>();
        for (int i = 0; i < abilities.Count; i++)
        {
            AbilityDefinition a = abilities[i];
            if (a == null)
            {
                warnings.Add($"Ability Database: entry #{i + 1} is empty.");
                continue;
            }
            if (seen.TryGetValue(a.Id, out AbilityDefinition other))
            {
                if (other == a)
                    warnings.Add($"Ability Database: '{a.DisplayName}' is listed twice.");
                else
                    errors.Add($"Ability Database: '{a.DisplayName}' and '{other.DisplayName}' share the id '{a.Id}'. Change the Ability Id of one of them.");
                continue;
            }
            seen.Add(a.Id, a);
            a.Validate(errors, warnings);
        }
        byId = null;
    }
}
