using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a crafted weapon or piece of armour remembers of how it was made: its design, its maker, its quality and why,
/// and the work itself (the finished assembly and its parts, so it could be taken apart or repaired one day).
/// </summary>
[Serializable]
public class CraftedItemRecord
{
    public int version = 1;
    public string id = "";
    public string designId = "";
    public string name = "";
    public string maker = "";
    public long utc;
    [Tooltip("0-1 and its tier (Crude ... Masterwork).")]
    public float quality;
    public string tier = "";
    public string material = "";
    [Tooltip("Has it been tested (bent, struck, its edge tried): its numbers and quality are known.")]
    public bool inspected;
    public float weight, price;
    public Color color = new Color(0.6f, 0.6f, 0.62f);
    [Header("Numbers (from the evaluation)")]
    public float damageMin, damageMax, critChance, critMultiplier = 1f, knockback, attackSpeed = 1f, toolDamage, elementBuildup = 1f;
    public ElementType element = ElementType.None;
    public float defense, magicDefense;
    public int durability = 100;
    public List<CraftedResistance> resistances = new List<CraftedResistance>();
    public List<string> notes = new List<string>();
    public List<string> parts = new List<string>();
    public List<string> procedure = new List<string>();
    public List<string> inspection = new List<string>();
    public string assemblyJson = "";
    public List<string> componentJson = new List<string>();

    public string ToJson() => JsonUtility.ToJson(this);

    public static CraftedItemRecord FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            CraftedItemRecord r = JsonUtility.FromJson<CraftedItemRecord>(json);
            if (r == null) return null;
            r.notes ??= new List<string>();
            r.parts ??= new List<string>();
            r.procedure ??= new List<string>();
            r.inspection ??= new List<string>();
            r.componentJson ??= new List<string>();
            r.resistances ??= new List<CraftedResistance>();
            return r;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>An elemental resistance a crafted piece of armour gives (percent less damage).</summary>
[Serializable]
public class CraftedResistance
{
    public ElementType element;
    public float percent;
}

/// <summary>A weapon, tool, shield or piece of armour made at a crafting station.</summary>
public interface ICraftedItem
{
    CraftedItemRecord Record { get; }
}

/// <summary>Tooltip lines every crafted item shares.</summary>
public static class CraftedTooltip
{
    public const string IdPrefix = "crafted:";

    public static void Append(CraftedItemRecord r, List<string> lines)
    {
        if (r == null)
            return;
        string by = string.IsNullOrEmpty(r.maker) ? "" : $" · made by {r.maker}";
        if (r.inspected)
            lines.Add($"<color=#ffcc61>{r.tier} work</color> <color=#9aa0a6>({r.quality * 100:0}%){by}</color>");
        else
            lines.Add($"<color=#9aa0a6>Not yet tried: how good it is will show in use, or at a bench{by}</color>");
        if (r.parts.Count > 0)
            lines.Add($"<color=#9aa0a6>{string.Join("; ", r.parts)}</color>");
        if (r.inspected)
            foreach (string n in r.inspection)
                lines.Add($"<color=#c9ccd1>{n}</color>");
    }
}
