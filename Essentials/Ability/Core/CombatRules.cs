using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the combat rules say about two players (or their summons) fighting each other.</summary>
public enum PvpVerdict
{
    /// <summary>No opinion: the next rule decides, finally <see cref="CombatSettings.playersCanHurtEachOther"/> and teams.</summary>
    Unspecified,
    /// <summary>Open PvP (a dangerous zone): enemies, except party members, who stay protected as usual.</summary>
    Allowed,
    /// <summary>
    /// No PvP (the city, a safe zone): they are allies, and nothing harmful lands on the other - damage, crowd control,
    /// knockbacks and debuffs are refused even with friendly fire on.
    /// </summary>
    Forbidden,
    /// <summary>Sworn opponents (a duel): enemies whatever their party, team or zone.</summary>
    Opponents,
}

/// <summary>
/// A source of combat rules that depend on where characters are or what they agreed to: the city, safe and dangerous
/// zones, duels. Register one with <see cref="CombatRules.Register"/>. Providers are asked from the highest
/// <see cref="Priority"/> down and the first one with an opinion decides, so a duel (high priority) can allow a fight
/// inside a city that forbids PvP (low priority).
/// </summary>
/// <remarks>
/// Rules must be pure functions of state every machine knows (positions, zone ids, duel pairs), so the server and the
/// clients reach the same answer once that state is synchronised.
/// </remarks>
public interface ICombatRuleProvider
{
    /// <summary>Higher is asked first (duels 100, zones and realms 0).</summary>
    int Priority { get; }

    /// <summary>
    /// The rule between two different players. Summons and other controlled characters are judged by their player
    /// (see <see cref="CombatRules.ControllingPlayer"/>), so this only ever receives players.
    /// </summary>
    PvpVerdict PlayerVersusPlayer(CombatEntity a, CombatEntity b);

    /// <summary>
    /// True = the abilities of <paramref name="caster"/> are only shown (the cast is validated and put on cooldown, its
    /// animation, sounds and effects play, but no action runs: no damage, healing, summons or movement). False = they
    /// work normally. Null = no opinion.
    /// </summary>
    bool? AbilitiesVisualOnly(CombatEntity caster);
}

/// <summary>
/// The pluggable combat rules on top of teams, parties and factions (see <see cref="CombatRelations"/>): who may fight
/// whom right now, and where abilities are only for show. With no provider registered nothing changes.
/// <list type="bullet">
/// <item><see cref="PlayerVersusPlayer"/>: asked by <see cref="CombatRelations.Get"/> for every pair of players.</item>
/// <item><see cref="MayHarm"/>: the last line of defence - <see cref="CombatEntity.ApplyDamage"/>,
/// <see cref="CombatEntity.ApplyControl"/>, <see cref="CombatEntity.ApplyDisplacement"/>, ability effects and weapon
/// hits refuse anything harmful between players whose PvP is <see cref="PvpVerdict.Forbidden"/>.</item>
/// <item><see cref="IsVisualOnly"/>: asked by the <see cref="AbilityCaster"/> before a cast; such casts raise
/// <see cref="VisualCast"/> so presentation code (or the network layer) can show them to nearby players.</item>
/// </list>
/// </summary>
public static class CombatRules
{
    private static readonly List<ICombatRuleProvider> providers = new List<ICombatRuleProvider>(4);

    /// <summary>A cast that was only shown (see <see cref="ICombatRuleProvider.AbilitiesVisualOnly"/>), at its release.</summary>
    public static event Action<AbilityCastInstance> VisualCast;

    /// <summary>A harmful action between two players was refused (source, target): feedback ("You can't fight here").</summary>
    public static event Action<CombatEntity, CombatEntity> HarmRefused;

    /// <summary>Raised when a provider is added or removed.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        providers.Clear();
        Separation = null;
        VisualCast = null;
        HarmRefused = null;
        Changed = null;
    }

    /// <summary>True when at least one provider is registered (fast path for everything else).</summary>
    public static bool HasProviders => providers.Count > 0;

    public static IReadOnlyList<ICombatRuleProvider> Providers => providers;

    /// <summary>Adds a provider (once). Providers are kept sorted by descending priority.</summary>
    public static void Register(ICombatRuleProvider provider)
    {
        if (provider == null || providers.Contains(provider))
            return;
        int i = 0;
        while (i < providers.Count && providers[i].Priority >= provider.Priority)
            i++;
        providers.Insert(i, provider);
        Changed?.Invoke();
    }

    public static void Unregister(ICombatRuleProvider provider)
    {
        if (provider != null && providers.Remove(provider))
            Changed?.Invoke();
    }

    /// <summary>
    /// The player behind a character: itself for a player, the summoner's player for a summon (followed up a few
    /// levels), null for mobs and scenery.
    /// </summary>
    public static CombatEntity ControllingPlayer(CombatEntity e)
    {
        for (int depth = 0; e != null && depth < 8; depth++)
        {
            if (e.Kind == CombatEntity.EntityKind.Player)
                return e;
            e = e.Summoner;
        }
        return null;
    }

    /// <summary>The rule between two players (Unspecified when no provider has an opinion, or a and b are the same).</summary>
    public static PvpVerdict PlayerVersusPlayer(CombatEntity a, CombatEntity b)
    {
        if (a == null || b == null || a == b || providers.Count == 0)
            return PvpVerdict.Unspecified;
        for (int i = 0; i < providers.Count; i++)
        {
            PvpVerdict v;
            try
            {
                v = providers[i].PlayerVersusPlayer(a, b);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                continue;
            }
            if (v != PvpVerdict.Unspecified)
                return v;
        }
        return PvpVerdict.Unspecified;
    }

    /// <summary>
    /// The PvP rule between the players behind two characters (summons count as their player). Unspecified when either
    /// is not controlled by a player or both are controlled by the same one.
    /// </summary>
    public static PvpVerdict Between(CombatEntity a, CombatEntity b)
    {
        if (providers.Count == 0 || a == null || b == null)
            return PvpVerdict.Unspecified;
        CombatEntity pa = ControllingPlayer(a), pb = ControllingPlayer(b);
        if (pa == null || pb == null || pa == pb)
            return PvpVerdict.Unspecified;
        return PlayerVersusPlayer(pa, pb);
    }

    /// <summary>
    /// May something harmful from <paramref name="source"/> land on <paramref name="target"/>? False only between two
    /// players (or their summons) whose PvP is <see cref="PvpVerdict.Forbidden"/>. Everything else is left to the usual
    /// relation and friendly-fire rules.
    /// </summary>
    public static bool MayHarm(CombatEntity source, CombatEntity target)
    {
        if (source == null || target == null || source == target)
            return true;
        if (AreSeparated(source, target))
            return false; // different realms (the City and the World overlap in space; dungeon instances)
        if (providers.Count == 0)
            return true;
        if (Between(source, target) != PvpVerdict.Forbidden)
            return true;
        HarmRefused?.Invoke(source, target);
        return false;
    }

    /// <summary>
    /// Set by the hub world (or the network layer): are two entities in separate places that cannot affect each other -
    /// a player in the City and a mob in the World stand at overlapping coordinates, and every dungeon run has its own
    /// scene. Null (the default) = nothing is separated, so plain single-scene or multi-scene games are unaffected.
    /// </summary>
    public static Func<CombatEntity, CombatEntity, bool> Separation;

    /// <summary>Are <paramref name="a"/> and <paramref name="b"/> separated (see <see cref="Separation"/>)?</summary>
    public static bool AreSeparated(CombatEntity a, CombatEntity b)
    {
        if (Separation == null || a == null || b == null || a == b)
            return false;
        try
        {
            return Separation(a, b);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    /// <summary>Can <paramref name="a"/> see, target or affect <paramref name="b"/> at all (not <see cref="AreSeparated"/>)?</summary>
    public static bool CanInteract(CombatEntity a, CombatEntity b) => !AreSeparated(a, b);

    /// <summary>Are the abilities of <paramref name="caster"/> only shown right now (see <see cref="ICombatRuleProvider.AbilitiesVisualOnly"/>)?</summary>
    public static bool IsVisualOnly(CombatEntity caster)
    {
        if (providers.Count == 0 || caster == null)
            return false;
        for (int i = 0; i < providers.Count; i++)
        {
            bool? v;
            try
            {
                v = providers[i].AbilitiesVisualOnly(caster);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                continue;
            }
            if (v.HasValue)
                return v.Value;
        }
        return false;
    }

    /// <summary>Called by the <see cref="AbilityCaster"/> when a visual-only cast is released.</summary>
    public static void RaiseVisualCast(AbilityCastInstance cast)
    {
        if (cast != null)
            VisualCast?.Invoke(cast);
    }
}
