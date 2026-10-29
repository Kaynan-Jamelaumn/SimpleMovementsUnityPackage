using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Meat, fat or an organ harvested from a creature: food, and an alchemy ingredient when it holds substances (a liver, a
/// heart). Every copy keeps its own quality and marks like any harvested piece: a fine cut sates more than a poor one,
/// and a piece that had spoiled, was poisoned or tainted in the fight, or was soiled by a torn gut makes whoever eats it
/// sick (it hurts over time and sates far less).
/// </summary>
[CreateAssetMenu(fileName = "Harvested Food", menuName = "SimpleMovements/Harvesting/Harvested Food", order = 3)]
public class HarvestedFoodSO : ConsumableSO, IHarvestedItem, IItemInstanceTooltip, IItemInstanceValue, IRuntimeItem, IAlchemyIngredient
{
    [Header("Harvesting")]
    [Tooltip("The creature it comes from (an anatomy id of the Harvest Database: bear, deer...).")]
    [SerializeField] private string anatomyId = "";
    [Tooltip("The material of that creature it is (haunch, liver...).")]
    [SerializeField] private string materialId = "";

    [Header("Sickness")]
    [Tooltip("Health lost over Sickness Seconds by eating a spoiled, poisoned, tainted or soiled piece (scaled by how bad it is).")]
    [SerializeField, Min(0f)] private float sicknessDamage = 18f;
    [SerializeField, Min(1f)] private float sicknessSeconds = 20f;

    /// <summary>Marks that make a piece unfit to eat.</summary>
    public const HarvestMarks Sickening = HarvestMarks.Spoiled | HarvestMarks.Poisoned | HarvestMarks.Tainted | HarvestMarks.Contaminated | HarvestMarks.GutShot;

    [System.NonSerialized] private string runtimeId;
    [System.NonSerialized] private AlchemyIngredientProfile alchemy;

    public string AnatomyId => anatomyId;
    public string MaterialId => materialId;
    public string RuntimeId => runtimeId;

    public string AlchemyId => ((Object)this).name;
    public string AlchemyName => Name;
    public AlchemyIngredientProfile AlchemyProfile =>
        alchemy ??= HarvestedItemInfo.AlchemyProfile(HarvestedItemInfo.Material(this, out _)) ?? new AlchemyIngredientProfile();

    public override bool ShowsDurability => false;

    public HarvestedFoodSO()
    {
        itemType = ItemType.Food;
        stackMax = 20;
        weight = 0.5f;
        UsesOneCopyPerBite();
    }

    private void OnEnable() => UsesOneCopyPerBite();

    // Each bite eats one piece: its value goes with it (the values are not uses, so a bite takes more than any value holds).
    private void UsesOneCopyPerBite()
    {
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = int.MaxValue;
        shouldBeDestroyedOn0UsesLeft = true;
    }

    /// <summary>Fills the item from a creature's material: name, description, price, weight, what it sates, icon and model.</summary>
    public void Setup(HarvestAnatomyData a, HarvestMaterial m, Sprite itemIcon, GameObject model, string id = null)
    {
        anatomyId = a != null ? a.id : "";
        materialId = m != null ? m.id : "";
        runtimeId = id;
        alchemy = null;
        if (m == null)
            return;
        name = m.Name;
        description = string.IsNullOrWhiteSpace(m.description) ? $"From {(a != null ? HarvestText.Capital(a.Name.ToLowerInvariant()) : "a creature")}." : m.description;
        itemType = ItemType.Food;
        price = m.value;
        weight = m.weight;
        stackMax = 20;
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
        SetEffects(new List<ConsumableEffect>
        {
            new ConsumableEffect
            {
                effectType = ConsumableEffectType.Food, itemType = ConsumableType.Food, effectName = m.Name, amount = Mathf.Max(0f, m.foodValue),
                applicationChance = 1f, minimumLevel = 1,
            },
        });
        UsesOneCopyPerBite();
    }

    /// <summary>A fine piece sates fully, a poor one less; a sickening one hardly at all.</summary>
    protected override float EffectScale(InventoryItem instance)
    {
        if (!HarvestItemCode.TryDecode(HarvestedItemInfo.CodeOf(instance), out double q, out HarvestMarks marks))
            return 1f;
        float scale = 0.55f + 0.45f * (float)q;
        return (marks & Sickening) != 0 ? scale * 0.3f : scale;
    }

    public override void UseItem(GameObject player, PlayerStatusController statusController, InventoryItem instance)
    {
        int code = HarvestedItemInfo.CodeOf(instance);
        base.UseItem(player, statusController, instance);
        if (statusController == null || statusController.HpManager == null || !HarvestItemCode.TryDecode(code, out double q, out HarvestMarks marks))
            return;
        if ((marks & Sickening) == 0)
            return;
        // Worse the more of it is wrong, and the poorer it is.
        int bad = 0;
        foreach (HarvestMarks m in new[] { HarvestMarks.Spoiled, HarvestMarks.Poisoned, HarvestMarks.Tainted, HarvestMarks.Contaminated, HarvestMarks.GutShot })
            if ((marks & m) != 0) bad++;
        float harm = sicknessDamage * (0.6f + 0.25f * bad) * (1.3f - 0.6f * (float)q);
        statusController.HpManager.AddHpEffect($"Sick from {Name.ToLowerInvariant()}", -harm, sicknessSeconds, 2f, true, false);
        InventoryManager.For(statusController)?.ShowMessage($"The {Name.ToLowerInvariant()} was bad: you feel sick.", InventoryFeedback.Kind.Warning);
    }

    public float InstanceValueFactor(InventoryItem stack, int fromTop = 0) => HarvestedItemInfo.ValueFactor(stack, fromTop);

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines)
    {
        HarvestedItemInfo.InstanceTooltip(item, lines);
        if (HarvestItemCode.TryDecode(HarvestedItemInfo.CodeOf(item), out _, out HarvestMarks marks) && (marks & Sickening) != 0)
            lines.Add("<color=#f0a090>Not fit to eat: it will make you sick.</color>");
    }

    public override void AppendTooltip(List<string> lines)
    {
        lines.Add("Food");
        if (Effects != null)
            foreach (ConsumableEffect e in Effects)
                if (e != null) lines.Add(BrewedPotionSO.DescribeEffect(e));
        HarvestedItemInfo.ItemTooltip(this, lines);
    }
}
