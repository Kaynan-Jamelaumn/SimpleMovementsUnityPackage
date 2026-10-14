using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Places the world portals of one terrain chunk (added to every chunk by <see cref="EndlessTerrain"/>).
///
/// Where: the planned sites whose point lies in this chunk (<see cref="PortalSitePlanner"/> - a pure function of the
/// world seed, so no duplicates, no dependence on loading order). When the chunk is ready (its objects and NavMesh
/// exist) each site gets the flattest open spot within Search Radius of its point (<see cref="FlatSpots"/> on the
/// chunk's exact data: dry, gentle slope, clear of trees and rocks, allowed biome and height, on the NavMesh), kept
/// inside the chunk so only this chunk's data decides it. No spot = the site stays empty.
///
/// Lifetime: the portal is a child of the chunk - removed when the chunk unloads, recreated identically when it
/// comes back. A portal that disappears by itself (its Portal's Despawn Time) reappears after its type's reappear
/// delay; one closed by use (<see cref="PortalSettings.closeAfterUse"/>) or by the game
/// (<see cref="WorldSpawnRegistry.ClosePortalSite"/>) stays away until it reopens - both remembered across unloading.
///
/// Linked portals: a type with <see cref="SpawnablePortal.linkedPortals"/> above 1 puts that many portals around the
/// site, <see cref="SpawnablePortal.linkedSpacing"/> apart (on flat, open spots of the same chunk), all leading into
/// ONE dungeon: they share the site (they appear, close and reopen together) and a link key, and each one can have
/// its own arrival point in the dungeon (<see cref="SpawnablePortal.linkedArrival"/>).
///
/// Network sessions: only the server places portals (<see cref="GameNet.NetSpawn.ShouldSpawnHere"/>). A portal whose
/// prefab has a NetworkObject leaves the chunk's hierarchy, goes into the World scene (a server's active scene is the
/// City) and is network-spawned, so every client near it sees it; it is despawned for everyone when its chunk unloads
/// on the server (no player near) or it closes. Clients place nothing: their players walk into the server's copy, and
/// the Portal acts on the server's copy of the player. The closed sites and difficulty hooks
/// (<see cref="WorldSpawnHooks"/>) are the server's.
/// </summary>
public class PortalSpawner : ChunkSpawnerBase
{
    private PortalSettings settings;
    private readonly List<Site> sites = new List<Site>();
    private readonly List<FlatSpots.Spot> spotScratch = new List<FlatSpots.Spot>();
    private Transform container;
    private float startAt;
    private bool resolved;
    private int spawned;

    /// <summary>A linked portal of a site (the second, third... entrance to its dungeon).</summary>
    private sealed class Linked
    {
        public Vector3 Position;
        public float Yaw;
        public GameObject Instance;
    }

    private sealed class Site
    {
        public PortalSitePlanner.Site Plan;
        public Vector2 SearchCenter;
        public bool Resolved, HasSpot;
        public Vector3 Position;
        public readonly List<Linked> Links = new List<Linked>();
        public GameObject Instance;
        /// <summary>A portal of this site is out (Instance may already be destroyed - Unity's null).</summary>
        public bool Live;
        public Portal Portal;
        public Portal.PortalDestroyedHandler Handler;
        public float RetryAt;
        public int Appearances;
        public string Problem;
    }

    public override int ActiveCount => spawned;

    public override string Describe()
    {
        int empty = 0;
        foreach (Site s in sites)
            if (s.Resolved && !s.HasSpot)
                empty++;
        return $"Portals {spawned}/{sites.Count} sites ({empty} without a spot)";
    }

    /// <summary>The settings this chunk's portals follow (set by EndlessTerrain before <see cref="ChunkSpawnerBase.Begin"/>).</summary>
    public void SetSettings(PortalSettings portalSettings)
    {
        settings = portalSettings;
        detailedLogging = settings != null && settings.enableDetailedLogging;
    }

    protected override float TickInterval => 1f;

    protected override void OnBegin()
    {
        sites.Clear();
        resolved = false;
        if (settings == null || settings.prefabs == null || settings.prefabs.Count == 0)
            return;
        container = GetContainer("Portals");
        startAt = Time.time + settings.StartDelay(PlacementRandom.Value(WorldSeed, 0x9047, Coord.x, Coord.y, 1));

        var planned = new List<PortalSitePlanner.Site>();
        PortalSitePlanner.SitesInArea(settings, WorldSeed, Origin, Origin + Vector2.one * Span, planned);
        foreach (PortalSitePlanner.Site p in planned)
            sites.Add(new Site { Plan = p });
        if (sites.Count > 0)
            WorldSpawnRegistry.PortalSiteClosed += OnSiteClosed;
        Log($"{sites.Count} planned portal site(s)");
    }

    protected override void OnEnd()
    {
        WorldSpawnRegistry.PortalSiteClosed -= OnSiteClosed;
        foreach (Site site in sites)
            Despawn(site);
        sites.Clear();
    }

    protected override void Tick()
    {
        if (settings == null || sites.Count == 0)
            return;
        if (!GameNet.NetSpawn.ShouldSpawnHere)
        {
            // A network client: the server places the world's portals and they arrive through the network. A portal this
            // machine placed before it joined a session as a client is removed.
            for (int i = 0; i < sites.Count; i++)
                if (sites[i].Live)
                    Despawn(sites[i]);
            return;
        }
        if (IsPaused || Time.time < startAt)
            return;

        // Every site's spot first, in the same order every time (a site checks its distance to the earlier ones), so
        // the result never depends on which sites happen to be closed right now.
        if (!resolved)
        {
            resolved = true;
            for (int i = 0; i < sites.Count; i++)
                Resolve(sites[i], i);
        }

        float now = Time.time;
        for (int i = 0; i < sites.Count; i++)
        {
            Site site = sites[i];
            SpawnablePortal type = site.Plan.Type >= 0 && site.Plan.Type < settings.prefabs.Count ? settings.prefabs[site.Plan.Type] : null;
            SpawnConditions conditions = type != null ? type.conditions : null;
            if (site.Instance != null)
            {
                // Its time or weather rule ended (a night portal at sunrise, a storm portal after the storm): it closes
                // and comes back when the rule is true again (not counted as used, no reappear delay).
                if (conditions != null && conditions.despawnWhenUnmet && conditions.IsUsed && !conditions.Allows(site.Position))
                {
                    Despawn(site);
                    site.RetryAt = now + 5f;
                    Log($"{site.Plan.Id}: closed ({conditions.Describe()} no longer true)");
                }
                continue;
            }
            if (site.Live)
                OnPortalGone(site);   // destroyed without telling us (no Portal component)
            if (!site.HasSpot || now < site.RetryAt || WorldSpawnRegistry.IsPortalSiteClosed(site.Plan.Id))
                continue;
            if (WorldAreas.Has(WorldAreaRules.NoPortals, site.Position.x, site.Position.z))
                continue;
            // Only at its time of day and in its weather (checked again every few seconds).
            if (conditions != null && !conditions.Allows(site.Position))
            {
                site.RetryAt = now + 5f;
                continue;
            }

            if (settings.enablePlayerProximityInfluence)
            {
                // Players in this chunk's scene only: in a network session the City overlaps the World in space.
                float d = WorldSpawnRegistry.DistanceToNearestPlayer(site.Position, settings.playerTag, Viewer, gameObject.scene);
                if (d < settings.minDistanceFromPlayer || (settings.maxDistanceFromPlayer > 0f && d > settings.maxDistanceFromPlayer && !float.IsPositiveInfinity(d)))
                {
                    site.RetryAt = now + settings.retryingSpawnTime;
                    continue;
                }
            }
            Spawn(site);
        }
    }

    /// <summary>Finds the site's spot on this chunk's exact terrain (once per chunk load; the result is always the same).</summary>
    private void Resolve(Site site, int index)
    {
        site.Resolved = true;
        site.HasSpot = false;
        SpawnablePortal type = site.Plan.Type >= 0 && site.Plan.Type < settings.prefabs.Count ? settings.prefabs[site.Plan.Type] : null;
        if (type == null || type.prefab == null)
        {
            site.Problem = "type has no prefab";
            return;
        }

        float footprint = settings.footprintRadius > 0f ? settings.footprintRadius : SpawnGround.FootprintRadius(type.prefab, 1.5f);
        // Keep the whole search (footprint and object clearance included) inside this chunk.
        float margin = settings.searchRadius + footprint + settings.objectClearance + settings.edgeAvoidanceDistance + 4f;
        Vector2 center = site.Plan.Point;
        center.x = ClampInside(center.x, Origin.x, margin);
        center.y = ClampInside(center.y, Origin.y, margin);
        site.SearchCenter = center;

        FlatSpots.Query query = SpotQuery(type, center, settings.searchRadius, footprint, site.Plan.Id.GetHashCode());
        spotScratch.Clear();
        FlatSpots.Find(query, spotScratch);
        float spacing = settings.EffectiveMinDistance;
        foreach (FlatSpots.Spot spot in spotScratch)
        {
            if (!settings.IsBiomeAllowed(spot.biome))
                continue;
            // Sites of this chunk resolved earlier (always in the same order) keep their distance.
            bool crowded = false;
            for (int j = 0; j < index; j++)
            {
                Site other = sites[j];
                if (other.HasSpot && (new Vector2(other.Position.x, other.Position.z) - new Vector2(spot.position.x, spot.position.z)).sqrMagnitude < spacing * spacing * 0.64f)
                {
                    crowded = true;
                    break;
                }
            }
            if (crowded)
                continue;
            site.HasSpot = true;
            site.Position = spot.position;
            Log($"{site.Plan.Id}: spot at {spot.position} (slope {spot.slope:0.0}, biome {(spot.biome != null ? spot.biome.name : "-")})");
            if (type.linkedPortals > 1)
                ResolveLinks(site, type, footprint, index);
            return;
        }
        site.Problem = spotScratch.Count == 0 ? "no flat, dry, open spot within Search Radius" : "only spots in forbidden biomes or too close to another portal";
        Log($"{site.Plan.Id}: {site.Problem}");
    }

    /// <summary>A search for a flat, open spot for a portal of <paramref name="type"/> around <paramref name="center"/>.</summary>
    private FlatSpots.Query SpotQuery(SpawnablePortal type, Vector2 center, float radius, float footprint, int seed)
    {
        var query = new FlatSpots.Query
        {
            center = center,
            searchRadius = radius,
            footprintRadius = footprint,
            maxSlope = settings.maxSlope,
            maxUnevenness = settings.maxUnevenness,
            allowWater = false,
            avoidObjects = true,
            objectClearance = settings.objectClearance,
            maxResults = 8,
            preferCenter = 0.5f,
            requireNavMesh = settings.requireNavMesh && Terrain != null && Terrain.bakeNavMesh,
            navMeshSampleDistance = 2f,
            seed = seed,
        };
        if (settings.useBiomeRestrictions && type.preferredBiomes != null)
            foreach (Biome b in type.preferredBiomes)
                if (b != null)
                    query.allowedBiomes.Add(b);
        if (settings.useHeightRestrictions || type.limitHeight)
        {
            query.limitHeight = true;
            query.minHeight = Mathf.Max(settings.useHeightRestrictions ? settings.minSpawnHeight : float.NegativeInfinity, type.limitHeight ? type.minPreferredHeight : float.NegativeInfinity);
            query.maxHeight = Mathf.Min(settings.useHeightRestrictions ? settings.maxSpawnHeight : float.PositiveInfinity, type.limitHeight ? type.maxPreferredHeight : float.PositiveInfinity);
        }
        return query;
    }

    /// <summary>
    /// Spots for a linked type's other portals: Linked Spacing away from the site's spot in evenly spread directions
    /// (turned by the seed), each the flattest open spot near its point, inside this chunk, clear of the other portals.
    /// A direction without a spot tries the next angle; a portal without any spot is left out (logged).
    /// </summary>
    private void ResolveLinks(Site site, SpawnablePortal type, float footprint, int index)
    {
        site.Links.Clear();
        int extra = Mathf.Clamp(type.linkedPortals, 1, 4) - 1;
        float spacing = Mathf.Max(4f, type.linkedSpacing);
        float radius = Mathf.Min(settings.searchRadius, spacing * 0.4f);
        float margin = radius + footprint + settings.objectClearance + settings.edgeAvoidanceDistance + 4f;
        int seed = site.Plan.Id.GetHashCode();
        float baseAngle = PlacementRandom.Value(WorldSeed, 0x4C1A, seed, 0, 0) * 360f;
        float minApart = spacing * 0.5f;
        for (int k = 1; k <= extra; k++)
        {
            bool found = false;
            for (int attempt = 0; attempt < 6 && !found; attempt++)
            {
                float angle = (baseAngle + (k - 1) * 360f / extra + attempt * 60f) * Mathf.Deg2Rad;
                Vector2 point = new Vector2(site.Position.x + Mathf.Sin(angle) * spacing, site.Position.z + Mathf.Cos(angle) * spacing);
                point.x = ClampInside(point.x, Origin.x, margin);
                point.y = ClampInside(point.y, Origin.y, margin);
                spotScratch.Clear();
                FlatSpots.Find(SpotQuery(type, point, radius, footprint, seed * 31 + k * 7 + attempt), spotScratch);
                foreach (FlatSpots.Spot spot in spotScratch)
                {
                    if (!settings.IsBiomeAllowed(spot.biome) || !FarFrom(spot.position, site.Position, minApart))
                        continue;
                    bool clear = true;
                    foreach (Linked other in site.Links)
                        clear &= FarFrom(spot.position, other.Position, minApart);
                    for (int j = 0; j < index && clear; j++)
                        if (sites[j].HasSpot)
                            clear &= FarFrom(spot.position, sites[j].Position, settings.EffectiveMinDistance * 0.5f);
                    if (!clear)
                        continue;
                    Vector3 toMain = site.Position - spot.position;
                    site.Links.Add(new Linked
                    {
                        Position = spot.position,
                        // Facing the site's main portal.
                        Yaw = toMain.sqrMagnitude > 0.01f ? Mathf.Atan2(toMain.x, toMain.z) * Mathf.Rad2Deg : site.Plan.Yaw,
                    });
                    found = true;
                    break;
                }
            }
            if (!found)
                Log($"{site.Plan.Id}: no spot for linked portal {k + 1} of {extra + 1} (spacing {spacing:0} - lower Linked Spacing or Search Radius)");
        }
    }

    private static bool FarFrom(Vector3 a, Vector3 b, float distance)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return dx * dx + dz * dz >= distance * distance;
    }

    private float ClampInside(float value, float origin, float margin)
    {
        float lo = origin + margin, hi = origin + Span - margin;
        return lo <= hi ? Mathf.Clamp(value, lo, hi) : origin + Span * 0.5f;
    }

    private void Spawn(Site site)
    {
        SpawnablePortal type = settings.prefabs[site.Plan.Type];
        GameObject instance = Instantiate(type.prefab, site.Position, Quaternion.Euler(0f, site.Plan.Yaw, 0f), container);
        instance.name = $"{type.prefab.name} ({site.Plan.Id})";
        SpawnGround.SitOnGround(instance, site.Position.y, settings.sinkDepth);
        site.Instance = instance;
        site.Live = true;
        site.Appearances++;
        spawned++;
        WorldSpawnRegistry.PortalCount++;

        // The type's own After Use rule, or the settings'.
        PortalUseRule rule = type.overrideAfterUse ? type.closeAfterUse : settings.closeAfterUse;
        float reopen = type.overrideAfterUse ? type.reopenAfterUse : settings.reopenAfterUse;
        float difficulty = WorldSpawnHooks.ApplyPortalDifficulty(site.Position, settings.DifficultyAt(new Vector2(site.Position.x, site.Position.z)));
        // Linked portals share a key (one dungeon) and take its seed from the main portal's spot.
        bool linked = site.Links.Count > 0;
        string link = linked ? "link:" + site.Plan.Id : null;

        site.Portal = instance.GetComponentInChildren<Portal>(true);
        if (site.Portal != null)
        {
            site.Portal.AssignSite(site.Plan.Id, difficulty, rule, reopen);
            site.Portal.AssignSpawnOptions(type.maxPlayers, type.sharing, link, 0, linked ? site.Position : (Vector3?)null);
            site.Handler = () => OnPortalGone(site);
            site.Portal.OnPortalDestroyed += site.Handler;
        }
        else
        {
            Debug.LogWarning($"PortalSpawner: '{type.prefab.name}' has no Portal component - it stands there but can't be entered. Add a Portal (and a trigger Collider) to the prefab.", type.prefab);
        }
        // Set up completely first, so the first state clients receive is final. Offline (and for prefabs without a
        // network identity) the portal stays under the chunk, as before.
        GameNet.WorldTerrainNet.SpawnWorldObject(instance, this, "world portal");
        Log($"{site.Plan.Id}: spawned {type.prefab.name} at {instance.transform.position}");

        for (int k = 0; k < site.Links.Count; k++)
        {
            Linked l = site.Links[k];
            GameObject twin = Instantiate(type.prefab, l.Position, Quaternion.Euler(0f, l.Yaw, 0f), container);
            twin.name = $"{type.prefab.name} ({site.Plan.Id} link {k + 2})";
            SpawnGround.SitOnGround(twin, l.Position.y, settings.sinkDepth);
            l.Instance = twin;
            spawned++;
            WorldSpawnRegistry.PortalCount++;
            Portal p = twin.GetComponentInChildren<Portal>(true);
            if (p != null)
            {
                int arrival = type.linkedArrival == LinkedArrival.SeparateEntrances ? k + 1 : 0;
                p.AssignSite(site.Plan.Id, difficulty, rule, reopen);
                p.AssignSpawnOptions(type.maxPlayers, type.sharing, link, arrival, site.Position);
            }
            GameNet.WorldTerrainNet.SpawnWorldObject(twin, this, "world portal");
            Log($"{site.Plan.Id}: spawned linked portal {k + 2} at {twin.transform.position}");
        }
    }

    /// <summary>Removes a site's linked portals (with the main one: they appear and disappear together).</summary>
    private void DespawnLinks(Site site)
    {
        foreach (Linked l in site.Links)
        {
            if (ReferenceEquals(l.Instance, null))
                continue; // not out
            spawned = Mathf.Max(0, spawned - 1);
            WorldSpawnRegistry.PortalCount = Mathf.Max(0, WorldSpawnRegistry.PortalCount - 1);
            if (l.Instance != null) // it may have gone by itself (its own despawn time)
                GameNet.NetSpawn.Destroy(l.Instance);
            l.Instance = null;
        }
    }

    /// <summary>The portal disappeared by itself (its despawn time, or the game destroyed it): reopen the site after the type's delay.</summary>
    private void OnPortalGone(Site site)
    {
        if (!site.Live)
            return;
        site.Live = false;
        if (site.Portal != null && site.Handler != null)
            site.Portal.OnPortalDestroyed -= site.Handler;
        site.Portal = null;
        site.Handler = null;
        site.Instance = null;
        spawned = Mathf.Max(0, spawned - 1);
        WorldSpawnRegistry.PortalCount = Mathf.Max(0, WorldSpawnRegistry.PortalCount - 1);
        DespawnLinks(site);
        if (!Started || settings == null)
            return;
        SpawnablePortal type = settings.prefabs[site.Plan.Type];
        float delay = type.ReappearDelay(PlacementRandom.Value(WorldSeed, 0x9047, site.Plan.Id.GetHashCode(), site.Appearances, 2));
        if (delay > 0f)
            WorldSpawnRegistry.ClosePortalSite(site.Plan.Id, delay);
        Log($"{site.Plan.Id}: portal gone, back in {delay:0} s");
    }

    /// <summary>Removes the site's portal without scheduling it to reappear (chunk unloading, or the site was closed).</summary>
    private void Despawn(Site site)
    {
        DespawnLinks(site);
        if (site.Portal != null && site.Handler != null)
            site.Portal.OnPortalDestroyed -= site.Handler;
        if (site.Live)
        {
            site.Live = false;
            spawned = Mathf.Max(0, spawned - 1);
            WorldSpawnRegistry.PortalCount = Mathf.Max(0, WorldSpawnRegistry.PortalCount - 1);
        }
        // Network-despawned for everyone when it was network-spawned (it is no longer under the chunk then); destroyed
        // otherwise. A client never removes a networked portal (the server does).
        if (site.Instance != null)
            GameNet.NetSpawn.Destroy(site.Instance);
        site.Portal = null;
        site.Handler = null;
        site.Instance = null;
    }

    private void OnSiteClosed(PortalSiteId id, float until)
    {
        foreach (Site site in sites)
        {
            if (site.Plan.Id.Equals(id) && site.Live)
            {
                Despawn(site);
                Log($"{id}: closed");
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (settings == null || !settings.enableVisualDebug)
            return;
        foreach (Site site in sites)
        {
            Vector3 planned = new Vector3(site.Plan.Point.x, transform.position.y, site.Plan.Point.y);
            if (LoadedTerrain.TryGetHeight(planned.x, planned.z, out float h))
                planned.y = h;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(planned + Vector3.up, 1f);
            if (site.Resolved)
            {
                Vector3 c = new Vector3(site.SearchCenter.x, planned.y, site.SearchCenter.y);
                Gizmos.color = site.HasSpot ? new Color(0.3f, 1f, 0.4f) : Color.red;
                DrawCircle(c, settings.searchRadius);
                if (site.HasSpot)
                {
                    Gizmos.DrawLine(site.Position, site.Position + Vector3.up * 12f);
                    Gizmos.DrawWireSphere(site.Position, 1.5f);
                    // Linked portals: a line from the main one to each.
                    foreach (Linked l in site.Links)
                    {
                        Gizmos.DrawLine(site.Position + Vector3.up * 2f, l.Position + Vector3.up * 2f);
                        Gizmos.DrawWireSphere(l.Position, 1.5f);
                    }
                }
            }
        }
    }

    private static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 32;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
