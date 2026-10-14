using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The realm rules (<see cref="CombatRules.Separation"/>) as the mob AI and the mob spawners use them. A mob only
/// perceives, targets, hears and stays awake for characters it is not separated from - a player in the City is never
/// chased by a World mob standing at the same coordinates, and dungeon mobs only deal with their own run's players -
/// and a world chunk only counts the players standing in its own realm.
/// </summary>
/// <remarks>
/// In a game without realms (no separation rule installed) nothing is ever filtered, so plain single-scene games
/// behave exactly as before. On a server every player is in <see cref="CombatEntity.Players"/>, so these checks cover
/// all of them, not only the player of this machine.
/// </remarks>
public static class MobRealms
{
    /// <summary>
    /// Optional scene-level form of <see cref="CombatRules.Separation"/> for places that have no
    /// <see cref="CombatEntity"/> of their own (a chunk spawner): are two scenes separate realms? Installed by the hub
    /// world next to <see cref="CombatRules.Separation"/>. When a separation rule exists but this is not installed, two
    /// different scenes count as separate realms, except the DontDestroyOnLoad scene (where travelling players wait),
    /// which is never separated from anything.
    /// </summary>
    public static Func<Scene, Scene, bool> SceneSeparation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => SceneSeparation = null;

    /// <summary>True when any realm rule is installed (otherwise nothing is ever separated).</summary>
    public static bool Active => CombatRules.Separation != null || SceneSeparation != null;

    /// <summary>Can <paramref name="self"/> perceive, target or affect <paramref name="other"/>? False for null.</summary>
    public static bool CanInteract(CombatEntity self, CombatEntity other) => other != null && !CombatRules.AreSeparated(self, other);

    /// <summary>Are two scenes separate realms (see <see cref="SceneSeparation"/>)? Never for the same or invalid scenes.</summary>
    public static bool AreScenesSeparated(Scene a, Scene b)
    {
        if (!Active || a == b || !a.IsValid() || !b.IsValid())
            return false;
        Func<Scene, Scene, bool> rule = SceneSeparation;
        if (rule == null)
            return !IsDontDestroyOnLoad(a) && !IsDontDestroyOnLoad(b);
        try
        {
            return rule(a, b);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    /// <summary>Unity's DontDestroyOnLoad scene (it has no build index and this fixed name).</summary>
    public static bool IsDontDestroyOnLoad(Scene s) => s.IsValid() && s.buildIndex < 0 && s.name == "DontDestroyOnLoad";

    /// <summary>Is <paramref name="go"/> in the realm whose scene is <paramref name="realm"/> (not separated from it)?</summary>
    public static bool IsInRealm(Scene realm, GameObject go) => go != null && !AreScenesSeparated(realm, go.scene);

    /// <summary>
    /// The nearest living player that <paramref name="self"/> is not separated from (null when there is none; then
    /// <paramref name="distance"/> is <see cref="float.MaxValue"/>). With a null <paramref name="self"/> every player
    /// counts, like <see cref="CombatEntity.NearestPlayer"/>.
    /// </summary>
    public static CombatEntity NearestPlayer(CombatEntity self, Vector3 position, out float distance)
    {
        IReadOnlyList<CombatEntity> players = CombatEntity.Players;
        bool filter = self != null && CombatRules.Separation != null;
        CombatEntity best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < players.Count; i++)
        {
            CombatEntity p = players[i];
            if (p == null || p.IsDead)
                continue;
            if (filter && CombatRules.AreSeparated(self, p))
                continue;
            float d = (p.Position - position).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = p;
            }
        }
        distance = best != null ? Mathf.Sqrt(bestSqr) : float.MaxValue;
        return best;
    }
}
