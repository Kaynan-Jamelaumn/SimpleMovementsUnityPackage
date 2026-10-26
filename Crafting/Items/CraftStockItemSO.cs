using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a stock material is, at a crafting station. Serialized as numbers in assets: add new values at the END.</summary>
public enum CraftStockKind
{
    /// <summary>A bar, rod, sheet or ingot of a metal (its composition and size).</summary>
    Metal,
    /// <summary>A lump of ore (each lump in a stack keeps its deposit and which lump it was).</summary>
    Ore,
    /// <summary>A raw hide (each hide keeps which animal it came from).</summary>
    Hide,
    /// <summary>A wooden blank (haft, grip, board).</summary>
    Wood,
    /// <summary>Ready-made rivets, studs or buckles.</summary>
    Fasteners,
    /// <summary>Fuel for the forge or the furnace (charcoal, coal, coke, firewood).</summary>
    Fuel,
    /// <summary>A supply used up as it is worked: flux, thread, padding, wax, oil, dye, clay, a cupel.</summary>
    Supply,
    /// <summary>A tanning or soaking agent poured into a vat (lime, bark, alum, brain oil...).</summary>
    Agent,
}

/// <summary>An element of a stock metal and its share by mass.</summary>
[Serializable]
public class CraftStockElement
{
    public string element = "iron";
    [Range(0f, 1f)] public float fraction = 1f;

    public CraftStockElement() { }
    public CraftStockElement(string element, float fraction) { this.element = element; this.fraction = fraction; }
}

/// <summary>
/// A raw material for the smithy, the smeltery and the tannery: a bar of a metal (by its composition, never by a
/// name - what it is follows from what it is made of), a lump of ore, a raw hide, a wooden blank, fasteners, fuel, a
/// supply or a tanning agent. Put on a <see cref="CraftStation"/> it becomes a piece of the workshop with its own
/// state; what comes back out is a worked piece (<see cref="CraftPieceItemSO"/>) or a finished item.
/// <para>Ores and hides vary: every lump in a stack keeps its deposit and every hide its animal (in the copy's
/// durability list, like a potion's strength), so two lumps of the same ore from different seams smelt differently.</para>
/// </summary>
[CreateAssetMenu(fileName = "Craft Stock", menuName = "SimpleMovements/Crafting/Stock Material", order = 2)]
public class CraftStockItemSO : MaterialSO, IItemInstanceTooltip, IRuntimeItem
{
    [Header("Crafting")]
    [Tooltip("What it is at a crafting station.")]
    [SerializeField] private CraftStockKind stockKind = CraftStockKind.Metal;
    [Tooltip("The Crafting Database id of the ore, hide, wood, fuel, supply or agent (unused for metal and fasteners).")]
    [SerializeField] private string craftId = "";
    [Tooltip("Metal and fasteners: what the metal is made of (element ids and mass fractions; they are normalised).")]
    [SerializeField] private List<CraftStockElement> composition = new List<CraftStockElement>();
    [Tooltip("Metal: length, width and thickness (mm). Wood: the blank's size (0 = the species' usual blank).")]
    [SerializeField] private Vector3 size = new Vector3(200f, 30f, 6f);
    [Tooltip("Metal: it was cast (an ingot: coarse-grained, maybe porous) rather than forged stock.")]
    [SerializeField] private bool cast;
    [Tooltip("Fasteners: what they are, how many in one item and the grams of each.")]
    [SerializeField] private CraftFastenerKind fastenerKind = CraftFastenerKind.Rivet;
    [SerializeField, Min(1)] private int fastenerCount = 10;
    [SerializeField, Min(0.1f)] private float fastenerMass = 4f;
    [Tooltip("Ore: the deposit used for lumps that carry none (-1 = a deposit from the item's name).")]
    [SerializeField] private int defaultDeposit = -1;

    /// <summary>Prefix of the ids of stock made at runtime (every machine makes the same item from the same rules).</summary>
    public const string IdPrefix = "craftstock:";

    [NonSerialized] private string runtimeKey;

    /// <summary>The id of stock made at runtime ("craftstock:Metal:Tool steel"); null for an asset (the Item Database names it).</summary>
    public string RuntimeId => string.IsNullOrEmpty(runtimeKey) ? null : IdPrefix + runtimeKey;

    /// <summary>Marks stock made at runtime by <see cref="CraftItems.Stock"/> (its kind and content id).</summary>
    public void SetRuntimeKey(string key) => runtimeKey = key;

    public CraftStockKind StockKind => stockKind;
    public string CraftId => craftId;
    public IReadOnlyList<CraftStockElement> Composition => composition;
    public Vector3 Size => size;
    public bool IsCast => cast;
    public CraftFastenerKind FastenerKind => fastenerKind;
    public int FastenerCount => fastenerCount;
    public float FastenerMass => fastenerMass;

    /// <summary>Does each copy carry a value of its own (ore lumps and hides)?</summary>
    public bool HasUnitCodes => stockKind == CraftStockKind.Ore || stockKind == CraftStockKind.Hide;

    public override bool ShowsDurability => false;

    public CraftStockItemSO()
    {
        stackMax = 20;
        weight = 1f;
    }

    // ------------------------------------------------------------------ the value each lump or hide keeps
    /// <summary>The value a lump of ore keeps: its deposit and which lump it was.</summary>
    public static int OreCode(int deposit, int lump) => 1 + (deposit & 0xFFFFF) * 1024 + (lump & 1023);

    public static bool TryDecodeOre(int code, out int deposit, out int lump)
    {
        deposit = lump = 0;
        if (code <= 0)
            return false;
        int v = code - 1;
        deposit = v / 1024;
        lump = v % 1024;
        return true;
    }

    /// <summary>The value a hide keeps: which animal it came from.</summary>
    public static int HideCode(int seed) => 1 + (seed & 0x3FFFFFFF);

    public static int HideSeed(int code) => code > 0 ? code - 1 : 0;

    /// <summary>The deposit of lumps that carry none.</summary>
    public int DefaultDeposit => defaultDeposit >= 0 ? defaultDeposit : (int)(CraftMath.Hash(((UnityEngine.Object)this).name) & 0xFFFFF);

    /// <summary>The value of the copy used next from <paramref name="instance"/> (0 = none kept).</summary>
    public static int CodeOf(InventoryItem instance)
    {
        if (instance == null)
            return 0;
        List<int> d = instance.DurabilityList;
        return d != null && d.Count > 0 ? d[d.Count - 1] : 0;
    }

    // ------------------------------------------------------------------ into the workshop
    /// <summary>The composition as (element, fraction) pairs, normalised.</summary>
    public (string element, double fraction)[] Fractions()
    {
        double sum = 0;
        foreach (CraftStockElement e in composition)
            if (e != null && e.fraction > 0) sum += e.fraction;
        var list = new List<(string, double)>();
        foreach (CraftStockElement e in composition)
            if (e != null && e.fraction > 0 && !string.IsNullOrEmpty(e.element))
                list.Add((e.element, sum > 0 ? e.fraction / sum : 0));
        return list.ToArray();
    }

    /// <summary>
    /// The workshop piece one copy of this item becomes (<paramref name="code"/> = its kept value: the ore's deposit and
    /// lump, the hide's animal). Null for fuel, supplies and agents, which are used up rather than worked.
    /// </summary>
    public CraftPiece MakePiece(CraftRules rules, int code = 0)
    {
        if (rules == null)
            return null;
        CraftPiece p;
        switch (stockKind)
        {
            case CraftStockKind.Metal:
            {
                (string element, double fraction)[] f = Fractions();
                if (f.Length == 0)
                    return null;
                p = CraftDefaults.Bar(rules, Mathf.Max(10f, size.x), Mathf.Max(2f, size.y), Mathf.Max(0.5f, size.z), f);
                p.cast = cast;
                if (cast)
                    foreach (MetalSegment s in p.segments)
                    {
                        s.rough = 70;
                        s.edge.grain = s.body.grain = 2.5;
                        s.porosity = 0.02;
                    }
                break;
            }
            case CraftStockKind.Ore:
            {
                if (!TryDecodeOre(code, out int deposit, out int lump))
                {
                    deposit = DefaultDeposit;
                    lump = 0;
                }
                p = CraftOres.NewLump(rules, craftId, deposit, lump);
                break;
            }
            case CraftStockKind.Hide:
                p = CraftLeather.NewHide(rules, craftId, code > 0 ? HideSeed(code) : (int)(CraftMath.Hash(((UnityEngine.Object)this).name) & 0xFFFF));
                break;
            case CraftStockKind.Wood:
                p = CraftDefaults.WoodBlank(rules, craftId, size.x, size.y, size.z);
                break;
            case CraftStockKind.Fasteners:
            {
                (string element, double fraction)[] f = Fractions();
                if (f.Length == 0)
                    f = new[] { ("iron", 1.0) };
                p = CraftDefaults.FastenerSet(rules, fastenerKind, fastenerCount, fastenerMass, f);
                break;
            }
            default:
                return null;
        }
        if (p != null)
            p.origin = Name;
        return p;
    }

    // ------------------------------------------------------------------ tooltip
    public override void AppendTooltip(List<string> lines)
    {
        switch (stockKind)
        {
            case CraftStockKind.Metal: lines.Add(cast ? "Metal stock · cast" : "Metal stock"); break;
            case CraftStockKind.Ore: lines.Add("Ore: clean it, smelt it"); break;
            case CraftStockKind.Hide: lines.Add(CraftDatabase.Instance != null && (CraftDatabase.Instance.Rules?.Hide(craftId)?.textile ?? false) ? "Cloth: cut it and sew it at the tailor's" : "Raw hide: flesh it, tan it"); break;
            case CraftStockKind.Wood: lines.Add("Wooden blank"); break;
            case CraftStockKind.Fasteners: lines.Add($"{fastenerCount} {fastenerKind.ToString().ToLowerInvariant()}{(fastenerCount == 1 ? "" : "s")}"); break;
            case CraftStockKind.Fuel: lines.Add("Fuel for the forge or the furnace"); break;
            case CraftStockKind.Supply: lines.Add("Crafting supply"); break;
            case CraftStockKind.Agent: lines.Add("Goes in a vat"); break;
        }
        if (stockKind == CraftStockKind.Metal && size.x > 0)
            lines.Add($"<color=#9aa0a6>About {size.x / 10f:0} cm long, {size.y / 10f:0.#} cm wide, {size.z:0.#} mm thick</color>");
        base.AppendTooltip(lines);
    }

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines)
    {
        int code = CodeOf(item);
        if (stockKind == CraftStockKind.Ore && TryDecodeOre(code, out int deposit, out _))
            lines.Add($"<color=#9aa0a6>From seam {deposit & 0xffff:X4}</color>");
    }

    // ------------------------------------------------------------------ made from code
    /// <summary>Fills the item (runtime items and the editor's Create Stock Items).</summary>
    public void Setup(string displayName, string itemDescription, CraftStockKind kind, string id, float itemPrice, float itemWeight, Sprite itemIcon,
        IEnumerable<CraftStockElement> metal = null, Vector3? dimensions = null, bool isCast = false)
    {
        name = displayName;
        description = itemDescription;
        stockKind = kind;
        craftId = id ?? "";
        price = itemPrice;
        weight = itemWeight;
        stackMax = kind == CraftStockKind.Hide || kind == CraftStockKind.Wood ? 5 : kind == CraftStockKind.Metal ? 10 : 20;
        composition = metal != null ? new List<CraftStockElement>(metal) : new List<CraftStockElement>();
        if (dimensions.HasValue) size = dimensions.Value;
        cast = isCast;
        SetMaterial(kind == CraftStockKind.Ore ? MaterialKind.Ore : kind == CraftStockKind.Metal ? MaterialKind.Ingot : kind == CraftStockKind.Hide ? MaterialKind.Leather
            : kind == CraftStockKind.Wood ? MaterialKind.Wood : kind == CraftStockKind.Agent ? MaterialKind.Herb : MaterialKind.Other);
        if (itemIcon != null) icon = itemIcon;
        SetGridSize(kind == CraftStockKind.Hide ? new Vector2Int(2, 2) : kind == CraftStockKind.Wood ? new Vector2Int(1, 3) : kind == CraftStockKind.Metal && size.x > 300 ? new Vector2Int(1, 2) : Vector2Int.one);
    }

    /// <summary>Sets the fasteners a Fasteners item holds.</summary>
    public void SetFasteners(CraftFastenerKind kind, int count, float gramsEach)
    {
        fastenerKind = kind;
        fastenerCount = Mathf.Max(1, count);
        fastenerMass = Mathf.Max(0.1f, gramsEach);
    }

    public void SetArt(Sprite itemIcon, GameObject model)
    {
        if (itemIcon != null) icon = itemIcon;
        if (model != null) prefab = model;
    }

    public override void ValidateItem(List<string> errors, List<string> warnings)
    {
        base.ValidateItem(errors, warnings);
        CraftRules rules = CraftDatabase.Instance.Rules;
        switch (stockKind)
        {
            case CraftStockKind.Metal:
            case CraftStockKind.Fasteners:
                if (Fractions().Length == 0) errors.Add("A metal needs a composition (element ids and fractions).");
                foreach (CraftStockElement e in composition)
                    if (e != null && rules.IndexOf(e.element) < 0) errors.Add($"Unknown element '{e.element}'.");
                break;
            case CraftStockKind.Ore: if (rules.Ore(craftId) == null) errors.Add($"Unknown ore '{craftId}'."); break;
            case CraftStockKind.Hide: if (rules.Hide(craftId) == null) errors.Add($"Unknown hide '{craftId}'."); break;
            case CraftStockKind.Wood: if (rules.Wood(craftId) == null) errors.Add($"Unknown wood '{craftId}'."); break;
            case CraftStockKind.Fuel: if (rules.Fuel(craftId) == null) errors.Add($"Unknown fuel '{craftId}'."); break;
            case CraftStockKind.Supply: if (rules.Supply(craftId) == null) errors.Add($"Unknown supply '{craftId}'."); break;
            case CraftStockKind.Agent: if (rules.Agent(craftId) == null) errors.Add($"Unknown agent '{craftId}'."); break;
        }
    }
}
