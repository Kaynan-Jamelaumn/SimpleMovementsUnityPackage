using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The mobs of one terrain chunk (added to every chunk by <see cref="EndlessTerrain"/>, started once the chunk's
/// objects and NavMesh exist).
///
/// <list type="bullet">
/// <item>Population: each chunk holds up to Max Number Of Mobs - fewer where little of it suits any mob type (a pure
/// function of the world seed and the chunk, so it's the same every visit).</item>
/// <item>Activation: mobs only exist while the player is within Activation Distance of the chunk. They are spawned one
/// (or one pack) per Spawn Interval on valid ground - on the NavMesh of the mob's agent type, dry, not too steep, clear
/// of trees and rocks, in an allowed biome and height band, away from the player, the camera's view, safe zones and
/// other mobs - and removed when the player is beyond Deactivation Distance or the chunk unloads.</item>
/// <item>Respawning: removed mobs come back when the player returns. Killed ones (their Mob was destroyed) leave a gap
/// for Respawn Delay, remembered in <see cref="WorldSpawnRegistry"/> even while the chunk is unloaded - so clearing
/// an area and coming back doesn't instantly refill it.</item>
/// <item>Limits: per type per chunk (Max Instances), per chunk (the population) and world-wide (Max Mobs In World) -
/// all counted from what actually exists, so nothing can be duplicated or over-counted.</item>
/// </list>
/// While the player is in a dungeon (<see cref="WorldSpawnRegistry.Paused"/>) the chunk's mobs are switched off.
/// </summary>
public class MobSpawner : ChunkSpawnerBase
{
    /// <summary>Extra safe zones added by the game at runtime (no mob spawns within Mob Settings' Safe Zone Radius of them).</summary>
    public static readonly List<Vector3> RuntimeSafeZones = new List<Vector3>();

    /// <summary>Adds a safe zone (e.g. a camp the player built). Uses Mob Settings' Safe Zone Radius.</summary>
    public static void AddSafeZone(Vector3 center) => RuntimeSafeZones.Add(center);

    private MobSettings settings;
    private readonly List<LiveMob> live = new List<LiveMob>();
    private readonly List<GameObject> mobObjects = new List<GameObject>();
    private Transform container;
    private int population;
    private bool active;
    private float startAt, nextSpawnAt;
    private System.Random random;
    private int kills;
    private SpawnRejection lastRejection;
    private Camera viewCamera;

    private sealed class LiveMob
    {
        public GameObject Go;
        public SpawnableMob Type;
        public Mob Mob;
        public Mob.MobDestroyedHandler Handler;
    }

    /// <summary>What a mob prefab needs from the NavMesh: its agent type and areas, and its radius.</summary>
    private struct AgentInfo
    {
        public int AgentTypeId, AreaMask;
        public float Radius;
        public bool HasAgent;
    }

    private static readonly Dictionary<GameObject, AgentInfo> AgentInfos = new Dictionary<GameObject, AgentInfo>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RuntimeSafeZones.Clear();
        AgentInfos.Clear();
    }

    /// <summary>This chunk's population target (before killed mobs are subtracted).</summary>
    public int Population => population;

    /// <summary>True while the player is near enough for this chunk to have its mobs.</summary>
    public bool IsActive => active;

    /// <summary>The chunk's living mobs.</summary>
    public IReadOnlyList<GameObject> Mobs
    {
        get
        {
            mobObjects.Clear();
            foreach (LiveMob m in live)
                if (m.Go != null)
                    mobObjects.Add(m.Go);
            return mobObjects;
        }
    }

    public override int ActiveCount => live.Count;

    public override string Describe()
    {
        int pending = Started ? WorldSpawnRegistry.PendingKills(Coord) : 0;
        return $"Mobs {live.Count}/{Mathf.Max(0, population - pending)} (population {population}, {pending} awaiting respawn) {(active ? "active" : "idle")}" +
               (lastRejection != SpawnRejection.None ? $", last rejection: {lastRejection}" : "");
    }

    /// <summary>The settings this chunk's mobs follow (set by EndlessTerrain before <see cref="ChunkSpawnerBase.Begin"/>).</summary>
    public void SetSettings(MobSettings mobSettings)
    {
        settings = mobSettings;
        detailedLogging = settings != null && settings.enableDetailedLogging;
    }

    protected override float TickInterval => 0.5f;

    protected override void OnBegin()
    {
        if (settings == null || settings.prefabs == null || settings.prefabs.Count == 0)
            return;
        container = GetContainer("Mobs");
        random = new System.Random((int)PlacementRandom.Hash(WorldSeed, 0x30B5, Coord.x, Coord.y, 0) ^ Time.frameCount);
        population = ComputePopulation();
        startAt = Time.time + settings.StartDelay(PlacementRandom.Value(WorldSeed, 0x30B5, Coord.x, Coord.y, 1));
        Log($"population {population}");
    }

    protected override void OnEnd()
    {
        DespawnAll();
        active = false;
    }

    /// <summary>
    /// How many mobs this chunk holds: Max Number Of Mobs times the share of the chunk (a 5x5 sample) that is dry, in
    /// an allowed biome and suits at least one mob type, varied per chunk by Population Variation.
    /// </summary>
    private int ComputePopulation()
    {
        if (settings.maxNumberOfMobs <= 0)
            return 0;
        int suitable = 0;
        const int samples = 5;
        for (int j = 0; j < samples; j++)
        {
            for (int i = 0; i < samples; i++)
            {
                float x = Origin.x + (i + 0.5f) * Span / samples, z = Origin.y + (j + 0.5f) * Span / samples;
                if (LoadedTerrain.WaterAt(x, z, out _) != WaterBodyType.None)
                    continue;
                Biome biome = LoadedTerrain.BiomeAt(x, z);
                if (!settings.IsBiomeAllowed(biome))
                    continue;
                foreach (SpawnableMob type in settings.prefabs)
                {
                    if (type != null && type.mobPrefab != null && type.CanSpawnInBiome(biome))
                    {
                        suitable++;
                        break;
                    }
                }
            }
        }
        float share = suitable / (float)(samples * samples);
        float variation = 1f - settings.populationVariation * PlacementRandom.Value(WorldSeed, 0x30B5, Coord.x, Coord.y, 2);
        return Mathf.RoundToInt(settings.maxNumberOfMobs * share * variation);
    }

    protected override void Tick()
    {
        if (settings == null || container == null)
            return;
        RemoveDestroyed();

        // In a dungeon: the chunk's mobs are switched off (they keep their state) and nothing spawns.
        bool paused = IsPaused;
        if (container.gameObject.activeSelf == paused)
            container.gameObject.SetActive(!paused);
        if (paused || population <= 0)
            return;

        float distance = DistanceToPlayers();
        if (!active)
        {
            if (distance > settings.activationDistance)
                return;
            active = true;
            nextSpawnAt = Mathf.Max(Time.time, startAt);
            Log("activated");
        }
        else if (distance > settings.deactivationDistance)
        {
            active = false;
            DespawnAll();
            Log("deactivated (player far away)");
            return;
        }

        if (Time.time < nextSpawnAt)
            return;
        int target = population - WorldSpawnRegistry.PendingKills(Coord);
        int capacity = target - live.Count;
        int worldLimit = WorldSpawnRegistry.MobLimit > 0 ? WorldSpawnRegistry.MobLimit : settings.maxMobsInWorld;
        if (worldLimit > 0)
            capacity = Mathf.Min(capacity, worldLimit - WorldSpawnRegistry.MobCount);
        if (capacity <= 0)
        {
            nextSpawnAt = Time.time + settings.spawnInterval;
            return;
        }
        nextSpawnAt = Time.time + TrySpawn(capacity);
    }

    private float DistanceToPlayers()
    {
        float best = float.PositiveInfinity;
        foreach (Transform p in WorldSpawnRegistry.GetPlayers(settings.playerTag, Viewer))
            if (p != null)
                best = Mathf.Min(best, DistanceTo(p.position));
        return best;
    }

    /// <summary>Tries to spawn one mob or pack (at most <paramref name="capacity"/> mobs). Returns the seconds to wait before the next try.</summary>
    private float TrySpawn(int capacity)
    {
        viewCamera = settings.avoidCameraView ? Camera.main : null;
        bool? isDay = DayNightManager.instance != null ? DayNightManager.instance.isDayTime : (bool?)null;

        for (int attempt = 0; attempt < settings.maxSpawnAttempts; attempt++)
        {
            float x = Origin.x + (float)random.NextDouble() * Span;
            float z = Origin.y + (float)random.NextDouble() * Span;
            Biome biome = LoadedTerrain.BiomeAt(x, z);
            if (!settings.IsBiomeAllowed(biome))
            {
                lastRejection = SpawnRejection.Biome;
                continue;
            }
            SpawnableMob type = PickType(biome, isDay);
            if (type == null)
            {
                lastRejection = SpawnRejection.Biome;
                continue;
            }
            if (!TryGetSpot(type, x, z, biome, settings.minDistanceBetweenMobs, out Vector3 spot))
                continue;

            Spawn(type, spot);
            int spawned = 1;

            // A pack: more of the same type around the first one.
            if (type.isPackAnimal && spawned < capacity && random.NextDouble() < settings.GetEffectivePackSpawnChance(type.packSpawnChance))
            {
                int largest = type.preferredPackSize > 0 ? type.preferredPackSize : settings.maxPackSize;
                int size = Mathf.Min(capacity, settings.minPackSize + random.Next(Mathf.Max(1, largest - settings.minPackSize + 1)));
                float spread = type.packSpreadRadius > 0f ? type.packSpreadRadius : settings.packSpawnRadius;
                for (int member = 1; member < size && CanSpawnType(type); member++)
                {
                    for (int tries = 0; tries < settings.maxSpawnAttempts; tries++)
                    {
                        Vector2 offset = RandomInsideCircle() * spread;
                        float mx = spot.x + offset.x, mz = spot.z + offset.y;
                        if (!Contains(mx, mz))
                            continue;
                        Biome memberBiome = LoadedTerrain.BiomeAt(mx, mz);
                        if (!settings.IsBiomeAllowed(memberBiome) || !type.CanSpawnInBiome(memberBiome))
                            continue;
                        if (TryGetSpot(type, mx, mz, memberBiome, settings.minDistanceBetweenMobs * 0.5f, out Vector3 memberSpot))
                        {
                            Spawn(type, memberSpot);
                            spawned++;
                            break;
                        }
                    }
                }
            }
            Log($"spawned {spawned} x {type.mobPrefab.name} at {spot}");
            float wait = type.GetEffectiveSpawnTime();
            return wait > 0f ? wait : settings.spawnInterval;
        }
        Log($"no valid spot after {settings.maxSpawnAttempts} tries (last: {lastRejection})");
        return settings.retryingSpawnTime;
    }

    /// <summary>A weighted pick among the types that may live in <paramref name="biome"/> and still fit their limit.</summary>
    private SpawnableMob PickType(Biome biome, bool? isDay)
    {
        float total = 0f;
        foreach (SpawnableMob type in settings.prefabs)
            if (type != null && CanSpawnType(type))
                total += type.WeightAt(biome, settings, isDay);
        if (total <= 0f)
            return null;
        float pick = (float)random.NextDouble() * total;
        SpawnableMob last = null;
        foreach (SpawnableMob type in settings.prefabs)
        {
            if (type == null || !CanSpawnType(type))
                continue;
            float w = type.WeightAt(biome, settings, isDay);
            if (w <= 0f)
                continue;
            last = type;
            if (pick < w)
                return type;
            pick -= w;
        }
        return last;
    }

    private bool CanSpawnType(SpawnableMob type)
    {
        if (type.maxInstances <= 0)
            return true;
        int count = 0;
        foreach (LiveMob m in live)
            if (m.Type == type)
                count++;
        return count < type.maxInstances;
    }

    /// <summary>Checks a spot for a type: ground (see <see cref="SpawnGround"/>), player distance, camera, safe zones, other mobs.</summary>
    private bool TryGetSpot(SpawnableMob type, float x, float z, Biome biome, float spacing, out Vector3 spot)
    {
        AgentInfo agent = GetAgentInfo(type.mobPrefab);
        var rules = new SpawnGround.Rules
        {
            Radius = agent.Radius,
            MaxSlope = Mathf.Min(settings.maxSlope, type.maxSpawnSlope),
            AllowWater = false,
            ObjectClearance = settings.objectClearance,
            ForbiddenBiomes = settings.globalForbiddenBiomes,
            RequireNavMesh = Terrain != null && Terrain.bakeNavMesh,
            NavMeshDistance = settings.navMeshSampleDistance,
            AgentTypeId = agent.AgentTypeId,
            AreaMask = agent.AreaMask,
        };
        if (settings.useHeightBasedSpawning)
        {
            float tolerance = settings.globalHeightTolerance;
            if (type.limitHeight)
            {
                rules.LimitHeight = true;
                rules.MinHeight = type.minPreferredHeight - tolerance;
                rules.MaxHeight = type.maxPreferredHeight + tolerance;
            }
            else if (biome != null && biome.maxHeight > biome.minHeight)
            {
                rules.LimitHeight = true;
                rules.MinHeight = biome.minHeight - tolerance;
                rules.MaxHeight = biome.maxHeight + tolerance;
            }
        }

        SpawnRejection rejection = SpawnGround.Check(x, z, rules, out spot, out _);
        if (rejection != SpawnRejection.None)
        {
            lastRejection = rejection;
            return false;
        }
        if (settings.IsInSafeZone(spot) || InRuntimeSafeZone(spot))
            return false;
        if (settings.enablePlayerProximityInfluence)
        {
            float d = WorldSpawnRegistry.DistanceToNearestPlayer(spot, settings.playerTag, Viewer);
            float min = Mathf.Max(settings.minDistanceFromPlayer, type.minDistanceFromPlayer);
            if (d < min || (settings.maxDistanceFromPlayer > 0f && d > settings.maxDistanceFromPlayer && !float.IsPositiveInfinity(d)))
                return false;
        }
        if (viewCamera != null && IsInView(spot))
            return false;
        if (spacing > 0f && AnyMobWithin(spot, spacing))
            return false;
        return true;
    }

    private bool InRuntimeSafeZone(Vector3 p)
    {
        if (RuntimeSafeZones.Count == 0)
            return false;
        float r2 = settings.safeZoneRadius * settings.safeZoneRadius;
        foreach (Vector3 c in RuntimeSafeZones)
        {
            float dx = c.x - p.x, dz = c.z - p.z;
            if (dx * dx + dz * dz < r2)
                return true;
        }
        return false;
    }

    private bool IsInView(Vector3 p)
    {
        Vector3 v = viewCamera.WorldToViewportPoint(p + Vector3.up);
        if (v.z <= 0f || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f)
            return false;
        return v.z < settings.hiddenSpawnDistance;
    }

    /// <summary>True when a mob of this or a neighbouring chunk is within <paramref name="radius"/> of <paramref name="p"/>.</summary>
    private bool AnyMobWithin(Vector3 p, float radius)
    {
        float r2 = radius * radius;
        foreach (ChunkSpawnerBase s in WorldSpawnRegistry.All)
        {
            if (!(s is MobSpawner mobs) || mobs.live.Count == 0 || mobs.DistanceTo(p) > radius + 64f)
                continue;
            foreach (LiveMob m in mobs.live)
            {
                if (m.Go == null)
                    continue;
                Vector3 q = m.Go.transform.position;
                float dx = q.x - p.x, dz = q.z - p.z;
                if (dx * dx + dz * dz < r2)
                    return true;
            }
        }
        return false;
    }

    private void Spawn(SpawnableMob type, Vector3 position)
    {
        Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
        GameObject go = Instantiate(type.mobPrefab, position, rotation, container);
        var entry = new LiveMob { Go = go, Type = type, Mob = go.GetComponent<Mob>() };
        if (entry.Mob != null)
        {
            entry.Handler = () => OnMobGone(entry);
            entry.Mob.OnMobDestroyed += entry.Handler;
        }
        live.Add(entry);
        WorldSpawnRegistry.MobCount++;
    }

    /// <summary>A mob died (or the game destroyed it): one gap in this chunk until its respawn time.</summary>
    private void OnMobGone(LiveMob entry)
    {
        if (!live.Remove(entry))
            return;
        WorldSpawnRegistry.MobCount = Mathf.Max(0, WorldSpawnRegistry.MobCount - 1);
        if (!Started || settings == null)
            return;
        kills++;
        float variation = 1f + settings.respawnDelayVariation * ((float)random.NextDouble() * 2f - 1f);
        WorldSpawnRegistry.RecordKill(Coord, Time.time + settings.respawnDelay * variation);
        Log($"{(entry.Type != null && entry.Type.mobPrefab != null ? entry.Type.mobPrefab.name : "mob")} killed");
    }

    /// <summary>Mobs destroyed without a Mob component's event count as killed.</summary>
    private void RemoveDestroyed()
    {
        for (int i = live.Count - 1; i >= 0; i--)
            if (live[i].Go == null)
                OnMobGone(live[i]);
    }

    /// <summary>Removes every mob of the chunk without counting them as killed (they come back when the chunk activates again).</summary>
    private void DespawnAll()
    {
        foreach (LiveMob m in live)
        {
            if (m.Mob != null && m.Handler != null)
                m.Mob.OnMobDestroyed -= m.Handler;
            if (m.Go != null)
                Destroy(m.Go);
        }
        WorldSpawnRegistry.MobCount = Mathf.Max(0, WorldSpawnRegistry.MobCount - live.Count);
        live.Clear();
    }

    private Vector2 RandomInsideCircle()
    {
        float angle = (float)random.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt((float)random.NextDouble());
        return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
    }

    private static AgentInfo GetAgentInfo(GameObject prefab)
    {
        if (prefab == null)
            return new AgentInfo { Radius = 0.5f, AreaMask = NavMesh.AllAreas };
        if (AgentInfos.TryGetValue(prefab, out AgentInfo info))
            return info;
        NavMeshAgent agent = prefab.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null)
        {
            float scale = Mathf.Max(Mathf.Abs(agent.transform.lossyScale.x), Mathf.Abs(agent.transform.lossyScale.z));
            info = new AgentInfo { HasAgent = true, AgentTypeId = agent.agentTypeID, AreaMask = agent.areaMask, Radius = Mathf.Max(0.1f, agent.radius * scale) };
        }
        else
        {
            info = new AgentInfo { AgentTypeId = 0, AreaMask = NavMesh.AllAreas, Radius = SpawnGround.FootprintRadius(prefab, 0.5f) };
        }
        AgentInfos[prefab] = info;
        return info;
    }

    private void OnDrawGizmos()
    {
        if (settings == null || !settings.enableVisualDebug || !Started)
            return;
        Gizmos.color = active ? new Color(1f, 0.4f, 0.2f, 0.6f) : new Color(0.5f, 0.5f, 0.5f, 0.3f);
        Vector3 center = new Vector3(Origin.x + Span * 0.5f, transform.position.y, Origin.y + Span * 0.5f);
        Gizmos.DrawWireCube(center, new Vector3(Span, 1f, Span));
        foreach (LiveMob m in live)
            if (m.Go != null)
                Gizmos.DrawLine(m.Go.transform.position, m.Go.transform.position + Vector3.up * 4f);
    }
}
