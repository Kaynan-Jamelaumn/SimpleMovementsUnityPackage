using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A piece of work carried out of a crafting station: a bar half drawn out, a quenched blade, a bloom, a casting, a
/// fleshed hide, a cut strap, an item not yet finished. It is that one piece - its shape, its heat, its hardness, its
/// flaws, its history - so it is made at runtime (one item per piece, never stacked) and goes back onto any station
/// exactly as it came off. An unfinished assembly carries its parts with it.
/// <para>Items made at runtime are kept on disk by <see cref="CraftItems"/> where they are made (offline, the server),
/// so the ids in a save still resolve; in a network session the clients receive the definition from the server
/// (<see cref="IRuntimeItemSource"/>), and its model is built from the piece itself (<see cref="CraftModels"/>).</para>
/// </summary>
public class CraftPieceItemSO : ItemSO, IRuntimeItem, IItemInstanceTooltip
{
    public const string IdPrefix = "craftpiece:";

    [SerializeField] private string key = "";
    [SerializeField] private string pieceJson = "";
    [SerializeField] private List<string> componentJson = new List<string>();

    [NonSerialized] private CraftPiece piece;
    [NonSerialized] private List<CraftPiece> components;

    /// <summary>The key of this piece (its file and its id).</summary>
    public string Key => key;
    public string RuntimeId => IdPrefix + key;

    /// <summary>The piece (a copy is put on a station: this one stays as it was carried).</summary>
    public CraftPiece Piece => piece ??= string.IsNullOrEmpty(pieceJson) ? null : JsonUtility.FromJson<CraftPiece>(pieceJson);

    /// <summary>An assembly's parts (empty for a single piece).</summary>
    public List<CraftPiece> Components
    {
        get
        {
            if (components == null)
            {
                components = new List<CraftPiece>();
                if (componentJson != null)
                    foreach (string j in componentJson)
                        if (!string.IsNullOrEmpty(j)) components.Add(JsonUtility.FromJson<CraftPiece>(j));
            }
            return components;
        }
    }

    public override bool ShowsDurability => false;

    public CraftPieceItemSO()
    {
        itemType = ItemType.Material;
        stackMax = 1;
        canRotateInGrid = true;
    }

    /// <summary>The piece and its parts as text (saved by <see cref="CraftItems"/>).</summary>
    public string PieceJson => pieceJson;
    public IReadOnlyList<string> ComponentJson => componentJson;

    /// <summary>Fills the item from a piece taken off a station.</summary>
    public void Setup(string itemKey, CraftPiece p, IEnumerable<CraftPiece> parts, string displayName, string itemDescription, float kilograms, float value,
        Sprite itemIcon, Vector2Int grid)
    {
        key = itemKey ?? Guid.NewGuid().ToString("N");
        ((UnityEngine.Object)this).name = displayName;
        name = displayName;
        description = itemDescription;
        piece = p?.Clone();
        pieceJson = p != null ? JsonUtility.ToJson(p) : "";
        components = new List<CraftPiece>();
        componentJson = new List<string>();
        if (parts != null)
            foreach (CraftPiece c in parts)
            {
                if (c == null) continue;
                components.Add(c.Clone());
                componentJson.Add(JsonUtility.ToJson(c));
            }
        weight = Mathf.Max(0.01f, kilograms);
        price = Mathf.Max(0f, value);
        stackMax = 1;
        if (itemIcon != null) icon = itemIcon;
        SetGridSize(grid);
    }

    /// <summary>The model shown on the ground and in the hand (built from the piece).</summary>
    public void SetModel(GameObject model)
    {
        if (model != null) prefab = model;
    }

    /// <summary>Restores the item from its saved text.</summary>
    public void SetupFromJson(string itemKey, string json, IEnumerable<string> parts, string displayName, string itemDescription, float kilograms, float value,
        Sprite itemIcon, Vector2Int grid)
    {
        key = itemKey;
        ((UnityEngine.Object)this).name = displayName;
        name = displayName;
        description = itemDescription;
        pieceJson = json ?? "";
        componentJson = parts != null ? new List<string>(parts) : new List<string>();
        piece = null;
        components = null;
        weight = Mathf.Max(0.01f, kilograms);
        price = Mathf.Max(0f, value);
        stackMax = 1;
        if (itemIcon != null) icon = itemIcon;
        SetGridSize(grid);
    }

    public override void AppendTooltip(List<string> lines)
    {
        CraftPiece p = Piece;
        if (p == null)
            return;
        CraftRules rules = CraftDatabase.Instance.Rules;
        CraftJournalData known = CraftKnowledge.Local != null ? CraftKnowledge.Local.Data : null;
        int shown = 0;
        foreach (string line in CraftLook.Describe(rules, p, known, CraftInstruments.None, false, false))
        {
            lines.Add($"<color=#c9ccd1>{line}</color>");
            if (++shown >= 6) break;
        }
        if (p.kind == CraftPieceKind.Assembly)
        {
            List<string> missing = CraftItems.MissingParts(rules, p, Components);
            lines.Add(missing.Count > 0 ? "Unfinished: " + string.Join(", ", missing) : "All its parts are fitted: finish it at a bench.");
        }
        if (p.MaxTemperature > 60)
            lines.Add("<color=#ffb070>Still hot.</color>");
    }

    public void AppendInstanceTooltip(InventoryItem item, List<string> lines) { }
}
