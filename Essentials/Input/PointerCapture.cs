using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets a component claim a player's mouse click so it does not also attack or use the item in hand - for example while
/// the pointer is over an NPC that a click talks to. Weapon, item and block code already skip clicks for which
/// <see cref="PlayerAbilityController.IsPointerCapturedFor"/> is true, and that includes these claims.
/// <para>A claim lasts until it is released (call <see cref="Release"/> in OnDisable); claims of destroyed owners are ignored.</para>
/// </summary>
public static class PointerCapture
{
    private static readonly Dictionary<object, Transform> claims = new Dictionary<object, Transform>(ReferenceComparer<object>.Instance);
    private static readonly List<object> stale = new List<object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => claims.Clear();

    /// <summary><paramref name="owner"/> takes the click of the player owning <paramref name="anyOnPlayer"/>.</summary>
    public static void Claim(object owner, Component anyOnPlayer)
    {
        if (owner == null || anyOnPlayer == null)
            return;
        claims[owner] = anyOnPlayer.transform.root;
    }

    /// <summary>Gives the click back.</summary>
    public static void Release(object owner)
    {
        if (owner != null)
            claims.Remove(owner);
    }

    public static bool IsClaimedBy(object owner) => owner != null && claims.ContainsKey(owner);

    /// <summary>Is the click of the player owning <paramref name="anyOnPlayer"/> claimed? Null = any player.</summary>
    public static bool IsCapturedFor(GameObject anyOnPlayer)
    {
        if (claims.Count == 0)
            return false;
        Transform root = anyOnPlayer != null ? anyOnPlayer.transform.root : null;
        bool found = false;
        stale.Clear();
        foreach (KeyValuePair<object, Transform> kv in claims)
        {
            if ((kv.Key is Object o && o == null) || kv.Value == null)
            {
                stale.Add(kv.Key); // its owner was destroyed without releasing
                continue;
            }
            if (root == null || kv.Value == root)
                found = true;
        }
        foreach (object s in stale)
            claims.Remove(s);
        return found;
    }
}
