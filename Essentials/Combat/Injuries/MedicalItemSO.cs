using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A medical item (bandage, splint, suture kit, disinfectant, surgical kit...): one or more <see cref="InjuryTreatment"/>s
/// that say which injuries it treats (by type, tag, severity and body part) and what it does. The player applies it from
/// the Health page of the inventory's character panel (click an injury, pick the item), or uses it from the hotbar or a
/// quickslot to treat the most urgent injury it can. Each treatment uses <see cref="UnitsPerUse"/> of the stack.
/// Create presets with Tools ▸ SimpleMovements ▸ Player ▸ Injuries ▸ Create Medical Items.
/// </summary>
[CreateAssetMenu(fileName = "Medical Item", menuName = "SimpleMovements/Items/Medical Item", order = 6)]
public class MedicalItemSO : ItemSO
{
    [Header("Treatments")]
    [Tooltip("What the item treats and how. The first treatment that fits an injury is used.")]
    [SerializeField] private List<InjuryTreatment> treatments = new List<InjuryTreatment>();
    [Tooltip("Units of the stack used by one treatment.")]
    [SerializeField, Min(1)] private int unitsPerUse = 1;
    [Tooltip("Using it from the hotbar or a quickslot treats the most urgent injury it can (a bleeding wound first). Off = only " +
             "from the Health page.")]
    [SerializeField] private bool quickUse = true;

    public IReadOnlyList<InjuryTreatment> Treatments => treatments ?? (treatments = new List<InjuryTreatment>());
    public int UnitsPerUse => Mathf.Max(1, unitsPerUse);

    public override bool CanUseFromQuickSlot => quickUse;

    /// <summary>The stack's durability list is not used: no durability bar.</summary>
    public override bool ShowsDurability => false;

    public MedicalItemSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 10;
        // Treatments use units themselves (only when they succeed): the generic "use" must not spend any.
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    /// <summary>Sets the treatments (editor presets, scripts).</summary>
    public void SetTreatments(List<InjuryTreatment> list, int units = 1)
    {
        treatments = list ?? new List<InjuryTreatment>();
        unitsPerUse = Mathf.Max(1, units);
    }

    /// <summary>Sets the basic item fields of a preset (editor).</summary>
    public void SetPresetInfo(string displayName, string text, int maxStack, float itemWeight, float itemPrice)
    {
        name = displayName;
        description = text;
        stackMax = Mathf.Max(1, maxStack);
        weight = itemWeight;
        price = itemPrice;
    }

    /// <summary>Sets the icon and world model (editor presets).</summary>
    public void SetLook(Sprite itemIcon, GameObject model)
    {
        icon = itemIcon;
        prefab = model;
    }

    /// <summary>The first treatment that fits (null = none).</summary>
    public InjuryTreatment TreatmentFor(in TreatmentContext ctx)
    {
        if (treatments == null)
            return null;
        for (int i = 0; i < treatments.Count; i++)
        {
            InjuryTreatment t = treatments[i];
            if (t == null)
                continue;
            TreatmentContext c = ctx;
            c.treatment = t;
            if (t.Matches(c))
                return t;
        }
        return null;
    }

    /// <summary>Hotbar / quickslot use: treats the most urgent injury this item can treat.</summary>
    public override void UseItem(GameObject player, PlayerStatusController statusController)
    {
        Component anchor = statusController != null ? statusController : player != null ? player.transform : null;
        InjuryController injuries = InjuryController.For(anchor);
        if (injuries == null || !injuries.Settings.enabled)
        {
            InventoryManager.For(anchor)?.ShowMessage($"{Name} has no use here.", InventoryFeedback.Kind.Info);
            return;
        }
        injuries.QuickTreat(this);
    }

    public override void AppendTooltip(List<string> lines)
    {
        InjurySettings settings = InjurySettings.Instance;
        if (treatments != null)
            foreach (InjuryTreatment t in treatments)
                if (t != null) lines.Add(t.Describe(settings));
        if (UnitsPerUse > 1)
            lines.Add($"Uses {UnitsPerUse} per treatment");
        if (!settings.enabled)
            lines.Add("<color=#9aa0a6>(the injury system is off)</color>");
        base.AppendTooltip(lines);
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        if (treatments == null || treatments.Count == 0)
            errors.Add("A medical item needs at least one treatment.");
        else
            for (int i = 0; i < treatments.Count; i++)
                treatments[i]?.Validate($"Treatment #{i + 1}", errors, warnings);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
        if (treatments == null)
            treatments = new List<InjuryTreatment>();
    }
}
