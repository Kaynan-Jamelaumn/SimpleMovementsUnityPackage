using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rules that keep some characters out of ability absorption entirely - for example players of a class that uses another
/// magic system (Rune Magic users never absorb abilities). Systems register rules; the absorption code asks:
/// <list type="bullet">
/// <item><see cref="Blocks"/>: may this character never receive this grant? <see cref="PlayerAbilityController.CanReceive"/>
/// refuses it, ability pickups leave such players alone (another player can take the orb), and an Instant delivery to
/// such a killer drops a pickup instead.</item>
/// <item><see cref="SuppressesRolls"/>: should this killer's kills roll no absorption at all?</item>
/// </list>
/// With no rules registered nothing changes. A rule that throws is logged and counts as "no".
/// </summary>
public static class AbsorptionGate
{
    /// <summary>True when the rule keeps <paramref name="receiver"/> from ever receiving <paramref name="grant"/> (either may be null).</summary>
    public delegate bool BlockRule(CombatEntity receiver, AbilityGrant grant);

    /// <summary>True when kills by <paramref name="killer"/> should roll no absorption.</summary>
    public delegate bool SuppressRule(CombatEntity killer);

    private static readonly List<BlockRule> blockRules = new List<BlockRule>();
    private static readonly List<SuppressRule> suppressRules = new List<SuppressRule>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Play Mode without a domain reload keeps statics: systems register again before the first scene.
        blockRules.Clear();
        suppressRules.Clear();
    }

    /// <summary>Is any block rule registered (callers can skip work when not)?</summary>
    public static bool HasBlockRules => blockRules.Count > 0;

    public static void AddBlockRule(BlockRule rule)
    {
        if (rule != null && !blockRules.Contains(rule))
            blockRules.Add(rule);
    }

    public static void RemoveBlockRule(BlockRule rule) => blockRules.Remove(rule);

    public static void AddSuppressRule(SuppressRule rule)
    {
        if (rule != null && !suppressRules.Contains(rule))
            suppressRules.Add(rule);
    }

    public static void RemoveSuppressRule(SuppressRule rule) => suppressRules.Remove(rule);

    /// <summary>Does a rule keep <paramref name="receiver"/> from ever receiving <paramref name="grant"/>?</summary>
    public static bool Blocks(CombatEntity receiver, AbilityGrant grant)
    {
        for (int i = 0; i < blockRules.Count; i++)
        {
            try
            {
                if (blockRules[i](receiver, grant))
                    return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        return false;
    }

    /// <summary>Should kills by <paramref name="killer"/> roll no absorption at all?</summary>
    public static bool SuppressesRolls(CombatEntity killer)
    {
        for (int i = 0; i < suppressRules.Count; i++)
        {
            try
            {
                if (suppressRules[i](killer))
                    return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        return false;
    }
}
