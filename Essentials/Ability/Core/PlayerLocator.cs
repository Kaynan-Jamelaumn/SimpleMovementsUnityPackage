using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds players without tags, layers or "the one player in the scene": a player is a <see cref="CombatEntity"/> of kind
/// Player (a character with a <see cref="PlayerStatusController"/>). Works with any number of players, so portals,
/// traps, spawners and the AI keep working in multiplayer.
/// <list type="bullet">
/// <item><see cref="FromCollider"/>: the player a collider belongs to (a trigger was entered by…).</item>
/// <item><see cref="All"/>, <see cref="Nearest"/>, <see cref="AnyWithin"/>: every player, the closest, proximity.</item>
/// <item><see cref="Local"/>: the player of this machine (camera, UI). Your networking code sets it; with a single
/// player it is that player.</item>
/// <item><see cref="PartyMembersNear"/>: who should travel together (dungeons).</item>
/// </list>
/// </summary>
public static class PlayerLocator
{
    private static CombatEntity local;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => local = null;

    /// <summary>Every active player (alive or dead).</summary>
    public static IReadOnlyList<CombatEntity> All => CombatEntity.Players;

    /// <summary>
    /// The player controlled on this machine. Set it from your networking code when the local player spawns
    /// (<c>PlayerLocator.Local = entity</c>). When nobody set it, the first player (single player: the player).
    /// </summary>
    public static CombatEntity Local
    {
        get
        {
            if (local != null && local.isActiveAndEnabled)
                return local;
            IReadOnlyList<CombatEntity> players = CombatEntity.Players;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null)
                    return players[i];
            return null;
        }
        set => local = value;
    }

    /// <summary>The player a collider belongs to (any child collider of the player works), or null.</summary>
    public static CombatEntity FromCollider(Collider collider)
    {
        CombatEntity e = CombatEntity.Resolve(collider);
        return e != null && e.Kind == CombatEntity.EntityKind.Player ? e : null;
    }

    /// <summary>The player a GameObject belongs to, or null.</summary>
    public static CombatEntity FromObject(GameObject go)
    {
        CombatEntity e = CombatEntity.Resolve(go);
        return e != null && e.Kind == CombatEntity.EntityKind.Player ? e : null;
    }

    /// <summary>Is this collider part of a player?</summary>
    public static bool IsPlayer(Collider collider) => FromCollider(collider) != null;

    /// <summary>
    /// The object to move when teleporting a player: the one with the CharacterController (moving a child would leave
    /// the body behind), else the player's root object.
    /// </summary>
    public static GameObject MovableRoot(CombatEntity player)
    {
        if (player == null)
            return null;
        CharacterController cc = player.GetComponentInParent<CharacterController>();
        if (cc == null)
            cc = player.GetComponentInChildren<CharacterController>();
        if (cc != null)
            return cc.gameObject;
        return player.Status != null ? player.Status.gameObject : player.gameObject;
    }

    /// <summary>The nearest player to <paramref name="position"/> (living ones only unless <paramref name="includeDead"/>), or null.</summary>
    public static CombatEntity Nearest(Vector3 position, bool includeDead = false)
    {
        CombatEntity best = null;
        float bestSqr = float.MaxValue;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || (!includeDead && p.IsDead))
                continue;
            float d = (p.Position - position).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = p;
            }
        }
        return best;
    }

    /// <summary>Distance to the nearest living player (float.MaxValue when there is none).</summary>
    public static float DistanceToNearest(Vector3 position)
    {
        CombatEntity p = Nearest(position);
        return p != null ? Vector3.Distance(p.Position, position) : float.MaxValue;
    }

    /// <summary>Is any living player within <paramref name="radius"/> (horizontal distance) of <paramref name="position"/>?</summary>
    public static bool AnyWithin(Vector3 position, float radius)
    {
        float sqr = radius * radius;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || p.IsDead)
                continue;
            Vector3 d = p.Position - position;
            d.y = 0f;
            if (d.sqrMagnitude <= sqr)
                return true;
        }
        return false;
    }

    /// <summary>
    /// <paramref name="who"/> and the members of its party within <paramref name="radius"/> metres (results cleared
    /// first, <paramref name="who"/> first). Radius 0 = only <paramref name="who"/>.
    /// </summary>
    public static void PartyMembersNear(CombatEntity who, float radius, List<CombatEntity> results)
    {
        results.Clear();
        if (who == null)
            return;
        results.Add(who);
        if (radius <= 0f || who.PartyId == 0)
            return;
        float sqr = radius * radius;
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || p == who || p.IsDead || p.PartyId != who.PartyId)
                continue;
            if ((p.Position - who.Position).sqrMagnitude <= sqr)
                results.Add(p);
        }
    }

    /// <summary>Makes sure a player object is registered (adds its CombatEntity). Players call it on Awake.</summary>
    public static CombatEntity Register(GameObject playerObject) => playerObject != null ? CombatEntity.GetOrAdd(playerObject) : null;
}
