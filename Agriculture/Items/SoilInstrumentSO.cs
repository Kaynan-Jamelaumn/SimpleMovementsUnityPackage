using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A measuring instrument: a pH probe, an alchemical soil analysis kit, a moisture meter, a thermometer, a light meter,
/// a hygrometer, a compaction tester, the Botanical Handbook, the Advanced Alchemical Analyzer, a depth gauge, a lens.
/// Its quality decides how much it tells and how exactly (a crude probe only says "acidic", a fine one "pH 5.8").
/// </summary>
[CreateAssetMenu(fileName = "Soil Instrument", menuName = "SimpleMovements/Agriculture/Instrument", order = 15)]
public class SoilInstrumentSO : ItemSO, IAgriItem, IRuntimeItem
{
    [Header("Agriculture")]
    [Tooltip("Its id in the agriculture rules (a built-in instrument's id: ph_probe, soil_kit...). Its own small lean comes from it.")]
    [SerializeField] private string instrumentId = "ph_probe";
    [SerializeField] private InstrumentKind kind = InstrumentKind.PhProbe;
    [Tooltip("0 a crude instrument (rough classes, can be wrong) - 1 a precise one.")]
    [SerializeField, Range(0f, 1f)] private float quality = 0.6f;

    [System.NonSerialized] private string runtimeId;

    public string InstrumentId => instrumentId;
    public InstrumentKind Kind => kind;
    public float Quality => quality;
    public string AgriKey => AgriIds.Instrument(instrumentId);
    public string RuntimeId => runtimeId;
    public override bool ShowsDurability => false;

    public SoilInstrumentSO()
    {
        itemType = ItemType.Miscellaneous;
        stackMax = 1;
        weight = 0.4f;
        maxDurability = 1;
        durability = 1;
        durabilityReductionPerUse = 0;
        shouldBeDestroyedOn0UsesLeft = false;
    }

    public void Setup(InstrumentPreset p, Sprite itemIcon, GameObject model, string id = null)
    {
        runtimeId = id;
        if (p == null) return;
        Apply(p);
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public void Apply(InstrumentPreset p)
    {
        instrumentId = p.id;
        kind = p.kind;
        quality = p.quality;
        name = p.name;
        description = p.description;
        price = p.value;
        weight = p.weight;
    }

    public InstrumentProfile Profile => new InstrumentProfile { id = instrumentId, kind = kind, quality = quality };

    public override void AppendTooltip(List<string> lines)
    {
        base.AppendTooltip(lines);
        lines.Add(AgriText.Instrument(kind));
    }
}
