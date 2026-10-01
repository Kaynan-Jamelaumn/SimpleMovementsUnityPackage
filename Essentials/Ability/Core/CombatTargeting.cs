using System;
using System.Collections.Generic;
using UnityEngine;

// ======================================================================================================
// Who can be hit, and by what.
//
//  1. LAYERS (physics)  - what a query can physically touch or see: character colliders, ground, walls.
//                         Combat Settings ▸ Character / Obstacle / Ground Layers. Never decides friend or foe.
//  2. RELATION          - who the target is to the attacker: Self, Party, Ally, Enemy, Neutral
//                         (CombatRelations.Get: party → faction → team → PvP rule → AI hostility).
//  3. FILTER            - which relations an attack / ability hit selects (TargetFilter on the hit),
//                         plus optional Target Rules: entity kinds, only / ignore factions.
//  4. HARM RULE         - whether HARMFUL parts (damage, control, knockback, debuffs) may land on a friend
//                         (Party / Ally). Friendly fire (Combat Settings, or per attack) decides; helpful parts
//                         (heal, buff, cleanse) always follow the filters.
//
// A hit therefore reaches a character when: layers let the query find it → the filter (or friendly fire, for
// harmful hits) selects its relation → the target rules allow it; then each effect applies if its own
// 'Only Affects' matches and, when harmful, the harm rule allows it.
// ======================================================================================================

/// <summary>Kinds of characters (Target Rules ▸ Kinds).</summary>
[Flags]
public enum EntityKinds
{
    None = 0,
    Players = 1 << 0,
    Mobs = 1 << 1,
    Others = 1 << 2,
    All = Players | Mobs | Others,
}

/// <summary>Friendly fire of one attack / hit compared to the game's rule (Combat Settings).</summary>
public enum FriendlyFireOverride
{
    /// <summary>Follow Combat Settings ▸ Party / Ally Friendly Fire.</summary>
    GameRule,
    /// <summary>Never harms party members or allies, whatever the game rule (a precise ability, a heal nova's damage part).</summary>
    Never,
    /// <summary>Always harms party members and allies caught in it (an explosion, a trap).</summary>
    Always,
}

/// <summary>
/// Optional extra restrictions of a hit, on top of its Hit Filter: which kinds of characters, which factions, and how
/// friendly fire applies. The defaults change nothing.
/// </summary>
[Serializable]
public class TargetRules
{
    [Tooltip("Friendly fire of this hit.\n• Game Rule: Combat Settings decide whether it harms party members / allies.\n" +
             "• Never: never harms them (only enemies / neutral take damage), even with friendly fire on.\n" +
             "• Always: harms party members and allies caught in it, even with friendly fire off.")]
    public FriendlyFireOverride friendlyFire = FriendlyFireOverride.GameRule;

    [Tooltip("Kinds of characters this hit can reach (players, mobs, others such as training dummies).")]
    public EntityKinds kinds = EntityKinds.All;

    [Tooltip("Only characters of these factions (empty = any faction, or none).")]
    public List<CombatFaction> onlyFactions = new List<CombatFaction>();

    [Tooltip("Characters of these factions are never hit.")]
    public List<CombatFaction> ignoreFactions = new List<CombatFaction>();

    /// <summary>True when the rules change nothing (fast path).</summary>
    public bool IsDefault => friendlyFire == FriendlyFireOverride.GameRule && kinds == EntityKinds.All &&
                             (onlyFactions == null || onlyFactions.Count == 0) && (ignoreFactions == null || ignoreFactions.Count == 0);

    /// <summary>Kind and faction restrictions (relations aside).</summary>
    public bool AllowsEntity(CombatEntity target)
    {
        if (target == null)
            return false;
        EntityKinds kind = target.Kind == CombatEntity.EntityKind.Player ? EntityKinds.Players
            : target.Kind == CombatEntity.EntityKind.Mob ? EntityKinds.Mobs : EntityKinds.Others;
        if ((kinds & kind) == 0)
            return false;
        CombatFaction f = target.Faction;
        if (onlyFactions != null && onlyFactions.Count > 0 && (f == null || !onlyFactions.Contains(f)))
            return false;
        if (ignoreFactions != null && f != null && ignoreFactions.Contains(f))
            return false;
        return true;
    }

    public string Describe()
    {
        var parts = new List<string>();
        if (friendlyFire == FriendlyFireOverride.Never) parts.Add("never harms friends");
        if (friendlyFire == FriendlyFireOverride.Always) parts.Add("harms friends too");
        if (kinds != EntityKinds.All) parts.Add("only " + kinds.ToString().ToLowerInvariant());
        if (onlyFactions != null && onlyFactions.Count > 0) parts.Add("only " + Names(onlyFactions));
        if (ignoreFactions != null && ignoreFactions.Count > 0) parts.Add("not " + Names(ignoreFactions));
        return string.Join(", ", parts);
    }

    private static string Names(List<CombatFaction> list)
    {
        var n = new List<string>();
        foreach (CombatFaction f in list) if (f != null) n.Add(f.Name);
        return string.Join(" / ", n);
    }
}

/// <summary>The filter + friendly-fire + target-rule decisions shared by weapons and abilities.</summary>
public static class CombatTargeting
{
    /// <summary>
    /// Can a hit with <paramref name="filter"/> / <paramref name="rules"/> reach <paramref name="target"/>
    /// (whose relation to the attacker is <paramref name="relation"/>)? A <paramref name="harmful"/> hit also reaches
    /// party members / allies when friendly fire lets it harm them, even if the filter does not list them.
    /// </summary>
    public static bool CanHit(TargetFilter filter, TargetRules rules, CombatRelation relation, CombatEntity target, bool harmful)
    {
        if (rules != null && !rules.IsDefault && target != null && !rules.AllowsEntity(target))
            return false;
        if (CombatRelations.Passes(filter, relation))
            return true;
        return harmful && (relation == CombatRelation.Party || relation == CombatRelation.Ally) && FriendlyFire(relation, rules);
    }

    /// <summary>May harmful effects (damage, control, knockback, debuffs) land on a character with this relation?</summary>
    public static bool CanHarm(CombatRelation relation, TargetRules rules = null)
    {
        switch (relation)
        {
            case CombatRelation.Party:
            case CombatRelation.Ally:
                return FriendlyFire(relation, rules);
            default:
                return true; // enemies, neutral, and yourself (a self-damaging ability hits you on purpose)
        }
    }

    /// <summary>Is friendly fire on for this relation (the hit's override, else the game rule)?</summary>
    public static bool FriendlyFire(CombatRelation relation, TargetRules rules)
    {
        FriendlyFireOverride o = rules != null ? rules.friendlyFire : FriendlyFireOverride.GameRule;
        if (o == FriendlyFireOverride.Always) return true;
        if (o == FriendlyFireOverride.Never) return false;
        CombatSettings s = CombatSettings.Instance;
        return relation == CombatRelation.Party ? s.partyFriendlyFire : relation == CombatRelation.Ally && s.allyFriendlyFire;
    }
}

/// <summary>
/// Parties: groups of players (or companions) that fight together. A party is only an id on each member's Combat
/// Entity, so it is easy to synchronise in multiplayer (the server assigns ids, every client computes the same
/// relations). 0 = no party.
/// </summary>
public static class CombatParties
{
    private static int nextId = 1;

    /// <summary>A new, unused party id (single player / host). In multiplayer, use the ids your server hands out.</summary>
    public static int NewPartyId() => nextId++;

    /// <summary>Puts every character in one new party and returns its id.</summary>
    public static int Form(params CombatEntity[] members)
    {
        int id = NewPartyId();
        foreach (CombatEntity e in members)
            if (e != null) e.SetParty(id);
        return id;
    }

    /// <summary>Living members of a party (results cleared first).</summary>
    public static void Members(int partyId, List<CombatEntity> results)
    {
        results.Clear();
        if (partyId == 0)
            return;
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].PartyId == partyId)
                results.Add(all[i]);
    }

    public static bool SameParty(CombatEntity a, CombatEntity b) => a != null && b != null && a.PartyId != 0 && a.PartyId == b.PartyId;
}
