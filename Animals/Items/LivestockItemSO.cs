using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An animal in a crate, bought from a merchant or traded: let out into a pen its species can live in (the pen's window,
/// <b>Let out</b>), it becomes a living animal of the world, owned by whoever let it out - and the crate is gone. A crate
/// from a merchant carries no animal yet (the world gives a young one its sex and genes when it is let out); a crate an
/// owner filled (Phase 5: <b>Crate</b> in the pen's window, to sell or trade the animal) carries its code: the very animal
/// it holds, its sex, its stage and its grade. A merchant pays for each copy by what it holds (<see cref="IItemInstanceValue"/>:
/// its stage's and its grade's worth, the same on every machine), and a player trade keeps the code: the buyer lets out the
/// very animal.
/// </summary>
[CreateAssetMenu(fileName = "Livestock", menuName = "SimpleMovements/Animals/Livestock (Item)", order = 11)]
public class LivestockItemSO : ItemSO, IAnimalItem, IRuntimeItem, IItemInstanceTooltip, IItemInstanceValue
{
    [Header("Animals")]
    [Tooltip("The species (an id of the Animal Database's species).")]
    [SerializeField] private string speciesId = "";
    [Tooltip("The breed (empty: the species' first).")]
    [SerializeField] private string breedId = "";

    [System.NonSerialized] private string runtimeId;

    public string SpeciesId => speciesId;
    public string BreedId => breedId;
    public string AnimalKey => AnimalIds.Livestock(speciesId, breedId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public LivestockItemSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 1;
        weight = 4f;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(AnimalSpeciesData s, AnimalBreedData b, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (s == null) return;
        speciesId = s.id;
        breedId = b != null ? b.id : "";
        string young = YoungName(s);
        name = b != null ? $"{b.name} {young} (crate)" : $"{young} (crate)";
        description = $"A young {s.DisplayName.ToLowerInvariant()} in a crate. Let it out into a pen it can live in: it becomes yours.";
        price = b != null ? b.value : 50;
        weight = Mathf.Clamp(s.look.length * s.look.height * 6f, 1f, 60f);
        stackMax = 1;
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    private static string YoungName(AnimalSpeciesData s) =>
        !string.IsNullOrWhiteSpace(s.youngName) ? s.youngName : "Young " + s.DisplayName.ToLowerInvariant();

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines)
    {
        if (item == null || lines == null) return;
        int code = AnimalToolkit.CodeOf(item, 0);
        if (AnimalItemCode.TryCrated(code, out AnimalSex sex, out GrowthStage stage, out int grade, out _))
        {
            AnimalRules rules = AnimalDatabase.Instance.Rules;
            lines.Add($"{Capital(AnimalCommands.Sex(sex))}, {AnimalSim.Stage(stage).ToLowerInvariant()}: {AnimalTrade.GradeWord(grade)} stock");
            float days = AnimalTrade.RulesOf(rules).crateKeepDays;
            if (days > 0f) lines.Add($"Let it out within {days:0.#} days of crating");
            return;
        }
        if (AnimalItemCode.TryLivestock(code, out sex, out stage, out _))
            lines.Add($"{AnimalCommands.Sex(sex)}, {AnimalSim.Stage(stage)}");
    }

    /// <summary>
    /// What a copy is worth against the item's price (a young one of its breed): a crated animal its stage's worth x its
    /// grade's (see <see cref="AnimalTrade.ValueFactor"/>); any other crate its price.
    /// </summary>
    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0)
    {
        int code = AnimalToolkit.CodeOf(stack, fromTop);
        if (!AnimalItemCode.TryCrated(code, out _, out GrowthStage stage, out int grade, out _)) return 1f;
        return AnimalTrade.ValueFactor(stage, grade, AnimalDatabase.Instance.Rules);
    }

    private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("Livestock");
        AnimalSpeciesData s = AnimalDatabase.Instance.Species(speciesId);
        if (s == null) return;
        lines.Add($"Lives in: {Kinds(s.habitat.kinds)}");
        lines.Add($"Needs {s.habitat.areaPerAnimal:0.#} m² of pen as an adult");
    }

    public static string Kinds(AnimalHabitatKind kinds)
    {
        var parts = new List<string>();
        foreach (AnimalHabitatKind k in System.Enum.GetValues(typeof(AnimalHabitatKind)))
            if (k != AnimalHabitatKind.None && (kinds & k) != 0) parts.Add(k.ToString().ToLowerInvariant());
        return string.Join(", ", parts);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (string.IsNullOrWhiteSpace(speciesId)) errors.Add("No species id.");
        if (stackMax > 1) warnings.Add("Stack Max above 1: a crate holds one animal, and a crated animal is chosen by its own copy (keep it at 1).");
        else if (AnimalDatabase.Instance.Species(speciesId) == null) warnings.Add($"No species '{speciesId}' in the Animal Database.");
    }
}
