using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// What the character creation screen offers: the races, the classes, the traits (empty = every trait of the Trait
/// Database marked Available At Creation) and the creation rules. The server checks every character a client sends
/// against the same catalog.
/// <para>Put it in a Resources folder named "CharacterCatalog" (Tools ▸ SimpleMovements ▸ Project Setup ▸ Create
/// Character Catalog (Resources) creates and fills it). Without one, a catalog is assembled at runtime from every race
/// and class found in Resources folders (in the editor: in the whole project).</para>
/// </summary>
[CreateAssetMenu(fileName = "CharacterCatalog", menuName = "SimpleMovements/Character/Character Catalog", order = 2)]
public class CharacterCatalog : ScriptableObject
{
    [Header("Choices")]
    [Tooltip("Races the player can pick (archetypes of kind Race). Empty = no race step.")]
    public List<CharacterArchetype> races = new List<CharacterArchetype>();
    [Tooltip("Classes the player can pick.")]
    public List<PlayerClass> classes = new List<PlayerClass>();
    [Tooltip("Traits offered when a class has no trait list of its own. Empty = the Trait Database's creation traits.")]
    public List<Trait> traits = new List<Trait>();

    [Header("Rules")]
    [Tooltip("A race must be picked (when there are races).")]
    public bool raceRequired = true;
    [Tooltip("Active traits (an ability on its own key) a new character may pick.")]
    [Min(0)] public int maxActiveTraits = 1;
    [Tooltip("Longest character name.")]
    [Range(3, 32)] public int maxNameLength = 20;
    [Tooltip("Two saved characters of this machine cannot share a name.")]
    public bool uniqueNames = true;

    [Header("Preview")]
    [Tooltip("Model shown on the creation screen. Empty = the player prefab of the Net Player Spawner / the scene's player.")]
    public GameObject previewModel;

    private static CharacterCatalog instance;
    private bool generated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>The catalog (the Resources asset, else one assembled from the project). Never null.</summary>
    public static CharacterCatalog Instance
    {
        get
        {
            if (instance != null)
                return instance;
            instance = SMAssets.Load<CharacterCatalog>(SMAssets.Databases, "CharacterCatalog");
            if (instance == null)
                instance = Generate();
            return instance;
        }
    }

    /// <summary>True when no catalog asset exists and the lists were assembled from the project.</summary>
    public bool IsGenerated => generated;

    private static CharacterCatalog Generate()
    {
        var c = CreateInstance<CharacterCatalog>();
        c.name = "CharacterCatalog (generated)";
        c.hideFlags = HideFlags.DontSave;
        c.generated = true;
        var races = new List<CharacterArchetype>();
        var classes = new List<PlayerClass>();
#if UNITY_EDITOR
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterArchetype"))
        {
            var a = AssetDatabase.LoadAssetAtPath<CharacterArchetype>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) races.Add(a);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerClass"))
        {
            var p = AssetDatabase.LoadAssetAtPath<PlayerClass>(AssetDatabase.GUIDToAssetPath(guid));
            if (p != null) classes.Add(p);
        }
#else
        races.AddRange(Resources.LoadAll<CharacterArchetype>(""));
        classes.AddRange(Resources.LoadAll<PlayerClass>(""));
#endif
        foreach (CharacterArchetype a in races)
            if (a != null && a.kind == ArchetypeKind.Race && a.availableAtCreation && !c.races.Contains(a))
                c.races.Add(a);
        foreach (PlayerClass p in classes)
            if (p != null && !c.classes.Contains(p))
                c.classes.Add(p);
        c.races.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
        c.classes.Sort((x, y) => string.CompareOrdinal(x.GetClassName(), y.GetClassName()));
        if (c.classes.Count == 0)
            Debug.LogWarning("[Characters] No Player Class found: the creation screen has no class to offer. Create one (Create ▸ SimpleMovements ▸ Character ▸ Player Class) " +
                             "and, for builds, list it in a Character Catalog (Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Character Catalog (Resources)).");
        return c;
    }

    // ------------------------------------------------------------------ lookups
    /// <summary>The id saved for an asset (its asset name).</summary>
    public static string Id(Object asset) => asset != null ? asset.name : "";

    public List<CharacterArchetype> Races => races.FindAll(r => r != null && r.availableAtCreation);

    public List<PlayerClass> Classes => classes.FindAll(c => c != null && (c.archetype == null || c.archetype.availableAtCreation));

    public bool HasRaces => Races.Count > 0;

    public bool RaceRequired => raceRequired && HasRaces;

    /// <summary>Traits offered by default (when the class has no list of its own).</summary>
    public List<Trait> Traits
    {
        get
        {
            var list = traits.FindAll(t => t != null);
            if (list.Count > 0)
                return list;
            TraitDatabase db = SMAssets.Load<TraitDatabase>(SMAssets.Databases, "TraitDatabase");
#if UNITY_EDITOR
            if (db == null)
                db = TraitDatabase.Instance;
#endif
            return db != null ? db.GetCreationTraits() : list;
        }
    }

    public CharacterArchetype FindRace(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (CharacterArchetype r in races)
            if (r != null && (r.name == id || r.Name == id)) return r;
        return null;
    }

    public PlayerClass FindClass(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (PlayerClass c in classes)
            if (c != null && (c.name == id || c.GetClassName() == id)) return c;
        return null;
    }

    /// <summary>A trait by asset name or display name: the catalog's, the classes' and races' lists, then the Trait Database.</summary>
    public Trait FindTrait(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Trait t in traits)
            if (t != null && (t.name == id || t.Name == id)) return t;
        foreach (PlayerClass c in classes)
        {
            if (c == null) continue;
            foreach (Trait t in c.GetAllAvailableTraits())
                if (t != null && (t.name == id || t.Name == id)) return t;
        }
        foreach (CharacterArchetype r in races)
        {
            if (r == null) continue;
            foreach (Trait t in r.offeredTraits)
                if (t != null && (t.name == id || t.Name == id)) return t;
        }
        foreach (Trait t in Traits)
            if (t != null && (t.name == id || t.Name == id)) return t;
        return null;
    }
}

/// <summary>
/// A character being made (or a saved one, resolved to its assets): race, class, chosen traits, height. Knows the
/// creation rules — trait points, costs, what can be combined — so the creation screen and the server agree on them.
/// </summary>
public class CharacterBuild
{
    public CharacterArchetype race;
    public PlayerClass playerClass;
    public readonly List<Trait> traits = new List<Trait>();
    public float height;

    private readonly List<CharacterArchetype> archetypes = new List<CharacterArchetype>();

    /// <summary>The race and the class's archetype.</summary>
    public IList<CharacterArchetype> Archetypes
    {
        get
        {
            archetypes.Clear();
            if (race != null) archetypes.Add(race);
            if (playerClass != null && playerClass.archetype != null && !archetypes.Contains(playerClass.archetype))
                archetypes.Add(playerClass.archetype);
            return archetypes;
        }
    }

    public int Cost(Trait t) => TraitRules.Cost(t, playerClass, Archetypes);

    /// <summary>Points before any trait: the class's plus the race / class bonuses.</summary>
    public int StartingPoints => (playerClass != null ? playerClass.traitPoints : 0) + TraitRules.BonusPoints(Archetypes);

    public int SpentPoints
    {
        get
        {
            int n = 0;
            foreach (Trait t in traits) n += Cost(t);
            return n;
        }
    }

    public int RemainingPoints => StartingPoints - SpentPoints;

    /// <summary>Traits the character gets anyway: the class's starting traits and the archetypes' innate traits.</summary>
    public List<Trait> GrantedTraits()
    {
        var list = new List<Trait>();
        if (playerClass != null) list.AddRange(playerClass.GetStartingTraits());
        foreach (CharacterArchetype a in Archetypes)
            if (a != null) list.AddRange(a.innateTraits.FindAll(t => t != null));
        return list;
    }

    /// <summary>Every trait the character will have (picked + granted).</summary>
    public List<Trait> AllTraits()
    {
        var list = new List<Trait>(traits);
        foreach (Trait t in GrantedTraits())
            if (!list.Contains(t)) list.Add(t);
        return list;
    }

    /// <summary>The traits that can be picked for the current race and class.</summary>
    public List<Trait> Selectable(CharacterCatalog catalog) =>
        playerClass != null ? TraitRules.Selectable(playerClass, Archetypes, catalog != null ? catalog.Traits : null) : new List<Trait>();

    /// <summary>Why the trait cannot be picked now (null = it can).</summary>
    public string WhyCannotAdd(Trait t, CharacterCatalog catalog)
    {
        if (t == null) return "No trait.";
        if (traits.Contains(t)) return "Already chosen.";
        if (playerClass == null) return "Choose a class first.";
        int max = catalog != null ? catalog.maxActiveTraits : 1;
        string why = TraitRules.WhyNot(t, Archetypes) ?? TraitRules.WhyTooManyActive(t, traits, max);
        if (why != null) return why;
        var chosen = new List<Trait>(traits);
        chosen.AddRange(GrantedTraits());
        why = TraitRules.WhyCannotCombine(t, chosen);
        if (why != null) return why;
        int cost = Cost(t);
        if (cost > RemainingPoints) return $"Needs {cost} points ({RemainingPoints} left).";
        return null;
    }

    /// <summary>Why the trait cannot be removed (another chosen trait requires it), or null.</summary>
    public string WhyCannotRemove(Trait t)
    {
        Trait needs = TraitRules.RequiredBy(t, traits);
        return needs != null ? $"{needs.Name} requires it." : null;
    }

    /// <summary>Changing the race or class can make picked traits illegal: drops them (last first) until all are fine.</summary>
    public List<Trait> DropInvalidTraits(CharacterCatalog catalog)
    {
        var dropped = new List<Trait>();
        var keep = new List<Trait>(traits);
        traits.Clear();
        List<Trait> selectable = Selectable(catalog);
        foreach (Trait t in keep)
        {
            if (t != null && selectable.Contains(t) && WhyCannotAdd(t, catalog) == null)
                traits.Add(t);
            else if (t != null)
                dropped.Add(t);
        }
        return dropped;
    }

    /// <summary>What still stops this character from being created (null = it is ready).</summary>
    public string Problem(CharacterCatalog catalog, string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return "Enter a name.";
        if (catalog != null && catalog.RaceRequired && race == null) return "Choose a race.";
        if (playerClass == null) return "Choose a class.";
        if (race != null && playerClass.archetype != null && !race.IsCompatibleWith(playerClass.archetype))
            return $"{race.Name} cannot be a {playerClass.GetClassName()}.";
        if (RemainingPoints < 0) return "Too many trait points spent.";
        return null;
    }

    public float ClampedHeight => race != null && race.setsHeight ? race.ClampHeight(height > 0f ? height : race.defaultHeight) : Mathf.Max(0f, height);

    // ------------------------------------------------------------------ saves
    /// <summary>Resolves a save to its assets (unknown races, classes or traits are left out).</summary>
    public static CharacterBuild From(CharacterSave save, CharacterCatalog catalog)
    {
        var b = new CharacterBuild();
        if (save == null || catalog == null)
            return b;
        b.race = catalog.FindRace(save.race);
        b.playerClass = catalog.FindClass(save.playerClass);
        b.height = save.height;
        if (save.traits != null)
            foreach (string id in save.traits)
            {
                Trait t = catalog.FindTrait(id);
                if (t != null && !b.traits.Contains(t)) b.traits.Add(t);
            }
        return b;
    }

    /// <summary>Writes the race, class, traits and height into a save.</summary>
    public void WriteTo(CharacterSave save)
    {
        if (save == null) return;
        save.race = CharacterCatalog.Id(race);
        save.playerClass = CharacterCatalog.Id(playerClass);
        save.height = ClampedHeight;
        save.traits = new List<string>();
        foreach (Trait t in traits)
            if (t != null) save.traits.Add(CharacterCatalog.Id(t));
    }

    /// <summary>
    /// Server side: a character exactly as the rules allow it. An unknown or forbidden race / class is replaced by the
    /// first one offered, traits that are not allowed or not affordable are dropped, the name is cleaned and the height
    /// clamped. <paramref name="problems"/> lists what was changed.
    /// </summary>
    public static CharacterSave Sanitize(CharacterSave save, CharacterCatalog catalog, List<string> problems = null)
    {
        CharacterSave s = save != null ? save.Clone() : new CharacterSave();
        catalog = catalog != null ? catalog : CharacterCatalog.Instance;
        CharacterBuild b = From(s, catalog);
        if (b.race != null && !b.race.availableAtCreation) { problems?.Add($"Race {b.race.Name} cannot be picked."); b.race = null; }
        if (b.race == null && catalog.RaceRequired)
        {
            List<CharacterArchetype> rs = catalog.Races;
            b.race = rs.Count > 0 ? rs[0] : null;
            problems?.Add($"Unknown race '{s.race}'.");
        }
        if (b.playerClass == null)
        {
            List<PlayerClass> cs = catalog.Classes;
            b.playerClass = cs.Count > 0 ? cs[0] : null;
            if (!string.IsNullOrEmpty(s.playerClass)) problems?.Add($"Unknown class '{s.playerClass}'.");
        }
        if (b.race != null && b.playerClass != null && b.playerClass.archetype != null && !b.race.IsCompatibleWith(b.playerClass.archetype))
        {
            problems?.Add($"{b.race.Name} cannot be a {b.playerClass.GetClassName()}.");
            PlayerClass fallback = catalog.Classes.Find(c => c.archetype == null || b.race.IsCompatibleWith(c.archetype));
            if (fallback != null) b.playerClass = fallback;
        }
        // Re-add the traits one by one with every rule (allowed, combinable, affordable).
        var wanted = new List<Trait>(b.traits);
        b.traits.Clear();
        List<Trait> selectable = b.Selectable(catalog);
        foreach (Trait t in wanted)
        {
            string why = !selectable.Contains(t) ? "not offered" : b.WhyCannotAdd(t, catalog);
            if (why == null) b.traits.Add(t);
            else problems?.Add($"Trait {t.Name} removed ({why}).");
        }
        // Requirements that ended up missing (a required trait was dropped).
        for (int i = b.traits.Count - 1; i >= 0; i--)
        {
            var others = new List<Trait>(b.traits);
            others.RemoveAt(i);
            others.AddRange(b.GrantedTraits());
            if (TraitRules.WhyCannotCombine(b.traits[i], others) != null)
            {
                problems?.Add($"Trait {b.traits[i].Name} removed (its requirement is missing).");
                b.traits.RemoveAt(i);
            }
        }
        b.WriteTo(s);
        s.characterName = CleanName(s.characterName, catalog.maxNameLength);
        if (s.appearance == null) s.appearance = new CharacterAppearanceData();
        return s;
    }

    /// <summary>Trims a name, strips markup and control characters, shortens it.</summary>
    public static string CleanName(string wanted, int maxLength = 20)
    {
        if (string.IsNullOrWhiteSpace(wanted))
            return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in wanted.Trim())
        {
            if (c == '<' || c == '>' || c == '|' || char.IsControl(c))
                continue;
            sb.Append(c);
            if (sb.Length >= maxLength)
                break;
        }
        return sb.ToString().Trim();
    }
}
