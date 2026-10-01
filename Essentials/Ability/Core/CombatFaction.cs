using System.Collections.Generic;
using UnityEngine;

/// <summary>How a faction treats factions it does not list.</summary>
public enum FactionStance
{
    Neutral,
    Ally,
    Enemy,
}

/// <summary>
/// A side in the world - Kingdom, Bandits, Wildlife, Undead, Player Guild A... Characters get one on their Combat Entity
/// (or Mob). Factions decide who is an ally, an enemy or neutral; they have nothing to do with physics Layers.
/// Relations are symmetric: listing Bandits as an enemy of Kingdom makes Kingdom an enemy of Bandits too.
/// </summary>
[CreateAssetMenu(fileName = "Faction", menuName = "Scriptable Objects/Combat/Faction")]
public class CombatFaction : ScriptableObject
{
    [Tooltip("Name shown in tooltips and debug output. Empty = the asset name.")]
    public string displayName;
    [Tooltip("Colour used by editor gizmos and optional UI (name plates, minimap).")]
    public Color color = Color.white;

    [Tooltip("Factions this one fights alongside (never hostile). Members of the same faction are always allies.")]
    public List<CombatFaction> allies = new List<CombatFaction>();
    [Tooltip("Factions this one is hostile to.")]
    public List<CombatFaction> enemies = new List<CombatFaction>();
    [Tooltip("How this faction treats factions that are in neither list (and characters without a faction).\n" +
             "Neutral: leaves them alone unless attacked (AI still decides). Enemy: hostile to everyone not allied. Ally: friendly to everyone not listed as an enemy.")]
    public FactionStance defaultStance = FactionStance.Neutral;

    public string Name => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>Relation of this faction to <paramref name="other"/> (null = a character without a faction).</summary>
    public FactionStance StanceTowards(CombatFaction other)
    {
        if (other == this)
            return FactionStance.Ally;
        if (other != null)
        {
            // Explicit lists on either side win; enemy beats ally when both are listed (a configuration mistake).
            if (Lists(enemies, other) || Lists(other.enemies, this))
                return FactionStance.Enemy;
            if (Lists(allies, other) || Lists(other.allies, this))
                return FactionStance.Ally;
            // Unlisted: the less friendly of the two default stances.
            return Min(defaultStance, other.defaultStance);
        }
        return defaultStance;
    }

    private static bool Lists(List<CombatFaction> list, CombatFaction f) => list != null && list.Contains(f);

    private static FactionStance Min(FactionStance a, FactionStance b)
    {
        if (a == FactionStance.Enemy || b == FactionStance.Enemy) return FactionStance.Enemy;
        if (a == FactionStance.Neutral || b == FactionStance.Neutral) return FactionStance.Neutral;
        return FactionStance.Ally;
    }
}
