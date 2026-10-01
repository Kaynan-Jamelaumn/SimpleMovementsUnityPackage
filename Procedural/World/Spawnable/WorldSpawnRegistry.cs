using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies one portal site across chunk unloading and reloading, and across sessions: the world region it
/// belongs to and its slot in that region (see <see cref="PortalSitePlanner"/>).
/// </summary>
[Serializable]
public struct PortalSiteId : IEquatable<PortalSiteId>
{
    public int RegionX, RegionZ, Slot;

    public PortalSiteId(int regionX, int regionZ, int slot)
    {
        RegionX = regionX;
        RegionZ = regionZ;
        Slot = slot;
    }

    public bool Equals(PortalSiteId other) => RegionX == other.RegionX && RegionZ == other.RegionZ && Slot == other.Slot;
    public override bool Equals(object obj) => obj is PortalSiteId other && Equals(other);
    public override int GetHashCode() => (RegionX * 73856093) ^ (RegionZ * 19349663) ^ (Slot * 83492791);
    public override string ToString() => $"portal {RegionX},{RegionZ}#{Slot}";
}

/// <summary>
/// World-wide spawning state that must outlive terrain chunks (main thread only):
/// <list type="bullet">
/// <item>the live chunk spawners and the total number of mobs and portals they have out (for global limits and debugging);</item>
/// <item>portal sites that are closed (used, despawned, or closed by the game) and when they reopen;</item>
/// <item>per chunk, the mobs killed there and when each comes back - so leaving and returning doesn't refill a cleared area;</item>
/// <item>the player transforms (looked up by tag once a second, not per spawn check);</item>
/// <item>the pause flag (set while the player is in a dungeon).</item>
/// </list>
/// Everything is reset when entering Play mode (also with domain reload disabled).
/// </summary>
public static class WorldSpawnRegistry
{
    // ------------------------------------------------------------------ pause

    /// <summary>
    /// True while world spawning is paused (the player is in a dungeon - see DungeonWorldPause): spawners create
    /// nothing, and mobs are switched off until it resumes.
    /// </summary>
    public static bool Paused { get; set; }

    // ------------------------------------------------------------------ counts

    private static readonly HashSet<ChunkSpawnerBase> Spawners = new HashSet<ChunkSpawnerBase>();

    /// <summary>Mobs currently alive from world spawners.</summary>
    public static int MobCount { get; internal set; }

    /// <summary>Portals currently in the world from portal spawners.</summary>
    public static int PortalCount { get; internal set; }

    /// <summary>
    /// Overrides Mob Settings' Max Mobs In World when above 0 (a <see cref="SpawnerManager"/> in the scene sets it
    /// from its Global Entity Limit).
    /// </summary>
    public static int MobLimit { get; set; }

    /// <summary>Multiplies spawn intervals (1 = normal). A SpawnerManager raises it while the frame rate is low.</summary>
    public static float IntervalMultiplier { get; set; } = 1f;

    /// <summary>The live chunk spawners (for debugging tools).</summary>
    public static IEnumerable<ChunkSpawnerBase> All => Spawners;

    internal static void Register(ChunkSpawnerBase spawner) => Spawners.Add(spawner);
    internal static void Unregister(ChunkSpawnerBase spawner) => Spawners.Remove(spawner);

    // ------------------------------------------------------------------ portal sites

    private static readonly Dictionary<PortalSiteId, float> ClosedSites = new Dictionary<PortalSiteId, float>();

    /// <summary>Raised when a portal site is closed (e.g. its dungeon was completed) - with the time it reopens (infinity = never).</summary>
    public static event Action<PortalSiteId, float> PortalSiteClosed;

    /// <summary>
    /// Closes a portal site: its portal is removed if present and doesn't come back until
    /// <paramref name="reopenAfterSeconds"/> have passed (negative or infinity = never, this session - save
    /// <see cref="ClosedPortalSites"/> to keep it closed across sessions).
    /// </summary>
    public static void ClosePortalSite(PortalSiteId site, float reopenAfterSeconds)
    {
        float until = reopenAfterSeconds < 0f || float.IsInfinity(reopenAfterSeconds) ? float.PositiveInfinity : Time.time + reopenAfterSeconds;
        if (ClosedSites.TryGetValue(site, out float current) && current >= until)
            return;
        ClosedSites[site] = until;
        PortalSiteClosed?.Invoke(site, until);
    }

    /// <summary>Opens a closed portal site again (its portal appears the next time its chunk checks).</summary>
    public static void OpenPortalSite(PortalSiteId site) => ClosedSites.Remove(site);

    /// <summary>True when the site is closed right now.</summary>
    public static bool IsPortalSiteClosed(PortalSiteId site)
    {
        if (!ClosedSites.TryGetValue(site, out float until))
            return false;
        if (Time.time < until)
            return true;
        ClosedSites.Remove(site);
        return false;
    }

    /// <summary>Sites closed for good (for saving). Timed closures are not included - they reopen by themselves.</summary>
    public static IEnumerable<PortalSiteId> ClosedPortalSites
    {
        get
        {
            foreach (KeyValuePair<PortalSiteId, float> pair in ClosedSites)
                if (float.IsPositiveInfinity(pair.Value))
                    yield return pair.Key;
        }
    }

    /// <summary>Restores permanently closed sites (from a save). Affects portals spawned from now on.</summary>
    public static void SetClosedPortalSites(IEnumerable<PortalSiteId> sites)
    {
        ClosedSites.Clear();
        if (sites != null)
            foreach (PortalSiteId site in sites)
                ClosedSites[site] = float.PositiveInfinity;
    }

    // ------------------------------------------------------------------ mob memory per chunk

    /// <summary>How many chunks remember their killed mobs after unloading (oldest forgotten first).</summary>
    public static int ChunkMemoryCapacity = 512;

    private static readonly Dictionary<Vector2Int, List<float>> Kills = new Dictionary<Vector2Int, List<float>>();
    private static readonly LinkedList<Vector2Int> KillOrder = new LinkedList<Vector2Int>();

    /// <summary>Remembers a mob killed in a chunk: one less mob there until <paramref name="respawnAt"/> (Time.time).</summary>
    internal static void RecordKill(Vector2Int chunk, float respawnAt)
    {
        if (!Kills.TryGetValue(chunk, out List<float> list))
        {
            list = new List<float>();
            Kills[chunk] = list;
            KillOrder.AddLast(chunk);
            while (KillOrder.Count > Mathf.Max(16, ChunkMemoryCapacity))
            {
                Kills.Remove(KillOrder.First.Value);
                KillOrder.RemoveFirst();
            }
        }
        list.Add(respawnAt);
    }

    /// <summary>Mobs of a chunk that were killed and haven't come back yet (expired entries are dropped).</summary>
    internal static int PendingKills(Vector2Int chunk)
    {
        if (!Kills.TryGetValue(chunk, out List<float> list))
            return 0;
        float now = Time.time;
        list.RemoveAll(t => t <= now);
        return list.Count;
    }

    /// <summary>Forgets every chunk's killed mobs (everything refills).</summary>
    public static void ClearMobMemory()
    {
        Kills.Clear();
        KillOrder.Clear();
    }

    // ------------------------------------------------------------------ players

    private sealed class PlayerCache
    {
        public readonly List<Transform> Found = new List<Transform>();
        public float NextSearch = -1f;
    }

    private static readonly Dictionary<string, PlayerCache> Players = new Dictionary<string, PlayerCache>();
    private static readonly List<Transform> FallbackOnly = new List<Transform>();

    /// <summary>
    /// Every player: the registered player Combat Entities (multiplayer-safe, no tags), plus objects with
    /// <paramref name="playerTag"/> that are not registered players (searched at most once a second per tag). Falls back
    /// to <paramref name="fallback"/> (EndlessTerrain's viewer) when there is none.
    /// </summary>
    public static List<Transform> GetPlayers(string playerTag, Transform fallback)
    {
        string key = playerTag ?? "";
        if (!Players.TryGetValue(key, out PlayerCache cache))
        {
            cache = new PlayerCache();
            Players[key] = cache;
        }
        if (Time.unscaledTime >= cache.NextSearch || cache.Found.Exists(p => p == null))
        {
            cache.NextSearch = Time.unscaledTime + 1f;
            cache.Found.Clear();
            IReadOnlyList<CombatEntity> registered = CombatEntity.Players;
            for (int i = 0; i < registered.Count; i++)
                if (registered[i] != null && !cache.Found.Contains(registered[i].transform))
                    cache.Found.Add(registered[i].transform);
            if (!string.IsNullOrEmpty(playerTag))
            {
                try
                {
                    foreach (GameObject go in GameObject.FindGameObjectsWithTag(playerTag))
                        if (go != null && PlayerLocator.FromObject(go) == null && !cache.Found.Contains(go.transform))
                            cache.Found.Add(go.transform); // tagged objects that are not registered players
                }
                catch (UnityException)
                {
                    // The tag isn't defined in this project: use the fallback.
                }
            }
        }
        if (cache.Found.Count > 0 || fallback == null)
            return cache.Found;
        FallbackOnly.Clear();
        FallbackOnly.Add(fallback);
        return FallbackOnly;
    }

    /// <summary>Horizontal distance (x, z) from <paramref name="position"/> to the nearest player; infinity when there is none.</summary>
    public static float DistanceToNearestPlayer(Vector3 position, string playerTag, Transform fallback)
    {
        float best = float.PositiveInfinity;
        foreach (Transform p in GetPlayers(playerTag, fallback))
        {
            if (p == null)
                continue;
            float dx = p.position.x - position.x, dz = p.position.z - position.z;
            best = Mathf.Min(best, dx * dx + dz * dz);
        }
        return float.IsPositiveInfinity(best) ? best : Mathf.Sqrt(best);
    }

    // ------------------------------------------------------------------ reset

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Paused = false;
        Spawners.Clear();
        MobCount = 0;
        PortalCount = 0;
        MobLimit = 0;
        IntervalMultiplier = 1f;
        ClosedSites.Clear();
        PortalSiteClosed = null;
        Kills.Clear();
        KillOrder.Clear();
        Players.Clear();
        FallbackOnly.Clear();
    }
}
