using System;
using UnityEngine;

/// <summary>
/// Optional rules a game layer adds to the world spawners without changing their settings - e.g. stronger mobs and
/// harder dungeons farther from a hub city, or mob types that only appear during a world event. Each hook is null
/// (no effect) until something sets it; set them before the world scene is loaded.
/// </summary>
public static class WorldSpawnHooks
{
    /// <summary>
    /// Multiplies a mob type's spawn weight at a world position (1 = unchanged, 0 = never there). Applied on top of
    /// biome, rarity and time-of-day weighting.
    /// </summary>
    public static Func<SpawnableMob, Vector3, float> MobWeight;

    /// <summary>
    /// A world mob was just created by a chunk's <see cref="MobSpawner"/> (object, type, position) - before its first
    /// frame, so its stats can still be scaled (its health manager fills up to Max Value on Start).
    /// </summary>
    public static Action<GameObject, SpawnableMob, Vector3> MobSpawned;

    /// <summary>
    /// The dungeon difficulty of a world portal at a position (position, the difficulty Portal Settings would give).
    /// Used by portals placed by a <see cref="PortalSpawner"/> and by hand-placed portals with Use Spawner Difficulty.
    /// </summary>
    public static Func<Vector3, float, float> PortalDifficulty;

    /// <summary>
    /// The kind of dungeon a world portal leads to (portal position, dungeon seed); null = the portal's own choice.
    /// </summary>
    public static Func<Vector3, int, ProceduralDungeon.DungeonProfile> DungeonType;

    /// <summary>
    /// The zone of a world position (the Hub World's rings around the city), for the zone rules of portal and mob
    /// types (<see cref="SpawnablePortal.zones"/>, <see cref="SpawnableMob.zones"/>). Null = no zones: those rules
    /// let everything through.
    /// </summary>
    public static Func<Vector3, SpawnZone> ZoneAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        MobWeight = null;
        MobSpawned = null;
        PortalDifficulty = null;
        DungeonType = null;
        ZoneAt = null;
    }

    /// <summary>The zone at <paramref name="position"/> (<see cref="SpawnZone.Unknown"/> without <see cref="ZoneAt"/>).</summary>
    public static SpawnZone ZoneOf(Vector3 position)
    {
        if (ZoneAt == null)
            return SpawnZone.Unknown;
        try
        {
            return ZoneAt(position);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return SpawnZone.Unknown;
        }
    }

    /// <summary>May a portal of <paramref name="type"/> stand at <paramref name="position"/> (its zone rule)?</summary>
    public static bool PortalAllowedAt(SpawnablePortal type, Vector3 position) =>
        type == null || type.zones == null || !type.zones.enabled || type.zones.Allows(ZoneOf(position));

    /// <summary>The dungeon profile for a portal after <see cref="DungeonType"/> (<paramref name="own"/> when it has no opinion).</summary>
    public static ProceduralDungeon.DungeonProfile ApplyDungeonType(Vector3 position, int seed, ProceduralDungeon.DungeonProfile own)
    {
        if (DungeonType == null)
            return own;
        try
        {
            ProceduralDungeon.DungeonProfile p = DungeonType(position, seed);
            return p != null ? p : own;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return own;
        }
    }

    /// <summary>The weight of <paramref name="type"/> at <paramref name="position"/> after <see cref="MobWeight"/>.</summary>
    public static float ApplyMobWeight(SpawnableMob type, Vector3 position, float weight)
    {
        if (weight <= 0f)
            return weight;
        // The type's zone rule (a mob only in the Frontier, another only in PvP zones...).
        if (type != null && type.zones != null && type.zones.enabled && !type.zones.Allows(ZoneOf(position)))
            return 0f;
        // The type's time and weather rule (only at night, only in storms...).
        if (type != null && type.conditions != null && !type.conditions.Allows(position))
            return 0f;
        if (MobWeight == null)
            return weight;
        try
        {
            return Mathf.Max(0f, weight * MobWeight(type, position));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return weight;
        }
    }

    /// <summary>The difficulty of a portal at <paramref name="position"/> after <see cref="PortalDifficulty"/>.</summary>
    public static float ApplyPortalDifficulty(Vector3 position, float difficulty)
    {
        if (PortalDifficulty == null)
            return difficulty;
        try
        {
            return Mathf.Max(0.1f, PortalDifficulty(position, difficulty));
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return difficulty;
        }
    }

    internal static void RaiseMobSpawned(GameObject mob, SpawnableMob type, Vector3 position)
    {
        if (MobSpawned == null || mob == null)
            return;
        try
        {
            MobSpawned(mob, type, position);
        }
        catch (Exception e)
        {
            Debug.LogException(e, mob);
        }
    }
}
