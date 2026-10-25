using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A material that can go into an alchemy vessel: herbs, salts, dusts, liquids, crystals. Its
/// <see cref="AlchemyIngredientProfile"/> says which substances it releases, how fast it dissolves and how warm it is;
/// what it then becomes is decided by the reactions of the station's Alchemy Database, never by recipes. Make the
/// built-in ones with Tools ▸ SimpleMovements ▸ Alchemy ▸ Create Ingredient Items, or your own with
/// Create ▸ SimpleMovements ▸ Alchemy ▸ Ingredient.
/// </summary>
[CreateAssetMenu(fileName = "Ingredient", menuName = "SimpleMovements/Alchemy/Ingredient", order = 1)]
public class AlchemyIngredientSO : MaterialSO, IAlchemyIngredient, IRuntimeItem
{
    [System.NonSerialized] private string runtimeId;

    /// <summary>The id of an ingredient made at runtime from a built-in preset ("ingredient:sunpetal"); null for an asset.</summary>
    public string RuntimeId => runtimeId;

    [Header("Alchemy")]
    [Tooltip("What it releases into a mixture, how long it takes to dissolve, its temperature.")]
    [SerializeField] private AlchemyIngredientProfile alchemy = new AlchemyIngredientProfile();

    public string AlchemyId => ((Object)this).name;
    public string AlchemyName => string.IsNullOrWhiteSpace(Name) ? ((Object)this).name : Name;
    public AlchemyIngredientProfile AlchemyProfile => alchemy ?? (alchemy = new AlchemyIngredientProfile());

    public AlchemyIngredientSO()
    {
        stackMax = 20;
        weight = 0.1f;
    }

    /// <summary>Sets every field (the Create Ingredient Items tool, runtime ingredients).</summary>
    public void Setup(string displayName, string itemDescription, MaterialKind kind, float itemPrice, float itemWeight,
        AlchemyIngredientProfile profile, Sprite itemIcon, GameObject model)
    {
        name = displayName;
        description = itemDescription;
        SetMaterial(kind);
        price = itemPrice;
        weight = itemWeight;
        stackMax = 20;
        alchemy = profile != null ? profile.Clone() : new AlchemyIngredientProfile();
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    /// <summary>Sets the icon and model (editor tools).</summary>
    public void SetArt(Sprite itemIcon, GameObject model)
    {
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("Alchemy ingredient");
        AlchemyIngredientProfile p = AlchemyProfile;
        AlchemyKnowledge k = AlchemyKnowledge.Local;
        AlchemyRules rules = AlchemyDatabase.Instance.Rules;
        if (k != null && p.releases != null && p.releases.Count > 0)
        {
            var known = new List<string>();
            int unknown = 0;
            foreach (SubstanceAmount r in p.releases)
            {
                if (r == null) continue;
                if (AlchemyJournal.IsIdentified(k.Data, rules, r.substance)) known.Add(rules.SubstanceName(r.substance));
                else unknown++;
            }
            if (known.Count > 0)
                lines.Add("Releases " + string.Join(", ", known) + (unknown > 0 ? $" and {unknown} unknown substance{(unknown > 1 ? "s" : "")}" : ""));
            else
                lines.Add("<color=#9aa0a6>What it releases is unknown: experiment.</color>");
        }
        AlchemyIngredientForm form = rules.Form(p.form);
        if (p.dissolveSeconds <= 0.01f) lines.Add("Mixes in at once");
        else lines.Add($"{form.Name}: gives up its substances over about {p.dissolveSeconds / Mathf.Max(0.01f, form.releaseSpeed):0} s (faster when hot or stirred)");
        if (rules.GroundForm(p) != null) lines.Add("<color=#9aa0a6>Can be ground at a station with a mortar.</color>");
        if (p.temperature < 5f) lines.Add("<color=#9fd8ff>Ice cold</color>");
        else if (p.temperature > 35f) lines.Add("<color=#ffb070>Warm to the touch</color>");
        base.AppendTooltip(lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        AlchemyProfile.Validate(AlchemyDatabase.Instance.Rules, $"Ingredient '{AlchemyName}'", errors, warnings);
    }

    // ------------------------------------------------------------------ runtime ingredients (no asset)
    /// <summary>An ingredient item made at runtime from a built-in preset (free supplies, offline harvests).</summary>
    internal static AlchemyIngredientSO CreateRuntime(AlchemyDefaults.IngredientPreset preset)
    {
        var s = CreateInstance<AlchemyIngredientSO>();
        ((Object)s).name = preset.name;
        s.hideFlags = HideFlags.DontSave;
        s.Setup(preset.name, preset.description, preset.kind, preset.price, preset.weight, preset.profile,
            AlchemyArt.Icon(AlchemyArt.ShapeOf(preset.kind), preset.color), AlchemyArt.Template(AlchemyArt.ShapeOf(preset.kind), preset.color));
        s.runtimeId = AlchemyItems.IngredientPrefix + preset.id;
        return s;
    }
}
