using System.Collections.Generic;
using HubWorld;
using UnityEngine;

/// <summary>What another player is to this machine's player, for the nameplates and highlights (strongest first).</summary>
public enum PlayerBond
{
    None,
    /// <summary>Their guild is in your guild's alliance.</summary>
    Alliance,
    /// <summary>Same guild.</summary>
    Guild,
    /// <summary>Same party (group).</summary>
    Party,
    /// <summary>In a duel (accepted: countdown or fight) - with you or with someone else.</summary>
    Duel,
}

/// <summary>
/// The bonds between players as every machine sees them (parties, guilds and alliances, duels), for presentation:
/// nameplates, health bars, silhouettes. The combat rules use <see cref="CombatRelations"/> instead.
/// </summary>
public static class PlayerBonds
{
    /// <summary>Is <paramref name="player"/> in an accepted duel (countdown or fight)?</summary>
    public static bool IsDueling(CombatEntity player)
    {
        DuelSystem duels = DuelSystem.Instance;
        if (duels == null || player == null)
            return false;
        Duel duel = duels.DuelOf(player);
        return duel != null && duel.IsActive;
    }

    /// <summary>
    /// The strongest bond of <paramref name="other"/> seen by <paramref name="me"/>: a duel (anyone's), then your party,
    /// your guild, your alliance. None for yourself and for non-players.
    /// </summary>
    public static PlayerBond Between(CombatEntity me, CombatEntity other)
    {
        if (other == null || other.Kind != CombatEntity.EntityKind.Player)
            return PlayerBond.None;
        if (IsDueling(other))
            return PlayerBond.Duel;
        if (me == null || me == other)
            return PlayerBond.None;
        if (CombatParties.SameParty(me, other))
            return PlayerBond.Party;
        if (CombatSocial.SameGuild(me, other))
            return PlayerBond.Guild;
        if (CombatSocial.SameAlliance(me, other))
            return PlayerBond.Alliance;
        return PlayerBond.None;
    }

    /// <summary>A party, guild or alliance member (the friendly bonds).</summary>
    public static bool IsFriendly(PlayerBond bond) => bond == PlayerBond.Party || bond == PlayerBond.Guild || bond == PlayerBond.Alliance;
}

/// <summary>
/// Who has been fighting recently, on every machine: a character is "in combat" for a few seconds after it took a hit
/// (blocked ones included) or one of its hits landed (its summons' hits count for it). Fed by
/// <see cref="CombatEntity.DamageFeedback"/>, which is raised where the rules run and on network clients alike, so the
/// answer is the same offline, on a host and on clients. Presentation only (nameplates show health bars in combat).
/// </summary>
public static class CombatActivity
{
    private static readonly Dictionary<CombatEntity, float> lastActive = new Dictionary<CombatEntity, float>();
    private static readonly HashSet<CombatEntity> watched = new HashSet<CombatEntity>();
    private static readonly List<CombatEntity> scratch = new List<CombatEntity>();
    private static float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lastActive.Clear();
        watched.Clear();
        nextScan = 0f;
    }

    /// <summary>Did <paramref name="e"/> fight (hit or get hit) within the last <paramref name="window"/> seconds?</summary>
    public static bool InCombat(CombatEntity e, float window = 6f) => SecondsSinceCombat(e) <= window;

    /// <summary>Seconds since <paramref name="e"/> last hit or was hit (infinity = not seen fighting).</summary>
    public static float SecondsSinceCombat(CombatEntity e)
    {
        if (e == null)
            return float.PositiveInfinity;
        Watch();
        float last = lastActive.TryGetValue(e, out float t) ? t : -1f;
        if (!e.IsReplica)
            last = Mathf.Max(last, Mathf.Max(e.LastDamagedTime, e.LastDealtDamageTime));
        return last < 0f ? float.PositiveInfinity : Time.time - last;
    }

    /// <summary>Marks a character (its controlling player for summons) as fighting now.</summary>
    public static void Mark(CombatEntity e)
    {
        if (e == null)
            return;
        CombatEntity owner = CombatRules.ControllingPlayer(e);
        lastActive[owner != null ? owner : e] = Time.time;
    }

    /// <summary>Subscribes to the hits of characters not watched yet (twice a second at most).</summary>
    private static void Watch()
    {
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + 0.5f;
        watched.RemoveWhere(e => e == null);
        IReadOnlyList<CombatEntity> all = CombatEntity.All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatEntity e = all[i];
            if (e != null && watched.Add(e))
                e.DamageFeedback += OnHit;
        }
        // Forget characters that are gone.
        scratch.Clear();
        foreach (CombatEntity e in lastActive.Keys)
            if (e == null)
                scratch.Add(e);
        for (int i = 0; i < scratch.Count; i++)
            lastActive.Remove(scratch[i]);
    }

    private static void OnHit(DamageInfo info)
    {
        Mark(info.target);
        if (info.source != null && info.source != info.target)
            Mark(info.source);
    }
}
