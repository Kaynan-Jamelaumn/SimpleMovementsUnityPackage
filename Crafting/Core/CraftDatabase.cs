using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The project's crafting rules as an asset: the simulation settings and the content (elements and alloys, metal names,
/// ores, fuels, quenchants, vat agents, hides, woods, supplies, abrasive tools, moulds, leather patterns, item designs).
/// Create it with Tools ▸ SimpleMovements ▸ Project Setup ▸ Create Crafting Database (Resources) - a copy of the
/// built-in content to edit. Without one, the built-in content is used. Content packs go in <see cref="extensions"/>.
/// </summary>
[CreateAssetMenu(fileName = "CraftingDatabase", menuName = "SimpleMovements/Crafting/Crafting Database", order = 0)]
public class CraftDatabase : ScriptableObject
{
    public const string ResourcesName = "CraftingDatabase";

    [Tooltip("The physics of the simulation (time step, heat, the steel model, forging, abrasion, smelting, leather).")]
    public CraftSimulationSettings settings = new CraftSimulationSettings();
    [Tooltip("Everything the rules are made of.")]
    public CraftContent content = new CraftContent();
    [Tooltip("Template items for designs, by design id (the design's own Template wins): weapons to copy moves and models from.")]
    public List<CraftDesignTemplate> templates = new List<CraftDesignTemplate>();
    [Tooltip("More databases whose content is added to this one (content packs). Same ids replace this one's entries.")]
    public List<CraftDatabase> extensions = new List<CraftDatabase>();

    [NonSerialized] private CraftRules compiled;
    [NonSerialized] private int compiledVersion = -1;
    [NonSerialized] private int version;

    // ------------------------------------------------------------------ instance
    private static CraftDatabase instance;
    private static bool searched;

    /// <summary>The project's database (Resources/SimpleMovements/Settings/CraftingDatabase), or the built-in content.</summary>
    public static CraftDatabase Instance
    {
        get
        {
            if (instance == null)
            {
                if (!searched)
                {
                    searched = true;
                    instance = SMAssets.Load<CraftDatabase>(SMAssets.Settings, ResourcesName);
                }
                if (instance == null)
                {
                    instance = CreateInstance<CraftDatabase>();
                    instance.name = "CraftingDatabase (built-in)";
                    instance.hideFlags = HideFlags.DontSave;
                    CraftDefaults.Fill(instance);
                }
            }
            return instance;
        }
    }

    /// <summary>Uses a specific database (tests, bootstrap scripts). Null = find it again.</summary>
    public static void Override(CraftDatabase db)
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

    private void Reset() => CraftDefaults.Fill(this);

    private void OnValidate() => MarkChanged();

    /// <summary>Forget the compiled rules (call after changing the lists from code).</summary>
    public void MarkChanged()
    {
        version++;
        compiled = null;
    }

    /// <summary>The compiled rules of this database and its extensions (cached until it changes).</summary>
    public CraftRules Rules
    {
        get
        {
            if (compiled == null || compiledVersion != version)
            {
                var merged = new CraftContent();
                Collect(merged, new HashSet<CraftDatabase>());
                compiled = CraftRules.Compile(settings, merged);
                ApplyTemplates(compiled);
                compiledVersion = version;
            }
            return compiled;
        }
    }

    private void Collect(CraftContent into, HashSet<CraftDatabase> seen)
    {
        if (!seen.Add(this))
            return;
        into.Merge(content);
        if (extensions != null)
            foreach (CraftDatabase e in extensions)
                if (e != null) e.Collect(into, seen);
    }

    private void ApplyTemplates(CraftRules rules)
    {
        foreach (CraftDesign d in rules.Designs)
        {
            if (d.template != null) continue;
            ItemSO t = Template(d.id, new HashSet<CraftDatabase>());
            if (t != null) rules.SetTemplate(d.id, t);
        }
    }

    private ItemSO Template(string design, HashSet<CraftDatabase> seen)
    {
        if (!seen.Add(this))
            return null;
        if (extensions != null)
            for (int i = extensions.Count - 1; i >= 0; i--)
            {
                ItemSO t = extensions[i] != null ? extensions[i].Template(design, seen) : null;
                if (t != null) return t;
            }
        if (templates != null)
            foreach (CraftDesignTemplate t in templates)
                if (t != null && t.item != null && string.Equals(t.design, design, StringComparison.OrdinalIgnoreCase))
                    return t.item;
        return null;
    }

    /// <summary>Sets the template item of a design (the editor tools).</summary>
    public void SetTemplate(string design, ItemSO item)
    {
        templates ??= new List<CraftDesignTemplate>();
        templates.RemoveAll(t => t == null || string.Equals(t.design, design, StringComparison.OrdinalIgnoreCase));
        if (item != null)
            templates.Add(new CraftDesignTemplate { design = design, item = item });
        MarkChanged();
    }

    public void Validate(List<string> errors, List<string> warnings)
    {
        CraftRules rules = Rules;
        errors.AddRange(rules.CompileErrors);
        warnings.AddRange(rules.CompileWarnings);
        foreach (CraftDesign d in rules.Designs)
            if (d.IsWeaponLike && rules.TemplateOf(d) == null)
                warnings.Add($"Design '{d.Name}' has no template weapon: crafted ones will have no attacks (Tools ▸ SimpleMovements ▸ Crafting ▸ Create Design Templates).");
    }
}

/// <summary>A template item for a design (a weapon with its attacks and model, an armour piece with its model).</summary>
[Serializable]
public class CraftDesignTemplate
{
    public string design = "";
    public ItemSO item;
}
