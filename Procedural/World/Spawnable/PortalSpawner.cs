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

    private sealed class Site
    {
        public PortalSitePlanner.Site Plan;
        public Vector2 SearchCenter;
        public bool Resolved, HasSpot;
        public Vector3 Position;
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
        if (settings == null || sites.Count == 0 || IsPaused || Time.time < startAt)
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
            if (site.Instance != null)
                continue;
            if (site.Live)
                OnPortalGone(site);   // destroyed without telling us (no Portal component)
            if (!site.HasSpot || now < site.RetryAt || WorldSpawnRegistry.IsPortalSiteClosed(site.Plan.Id))
                continue;

            if (settings.enablePlayerProximityInfluence)
            {
                float d = WorldSpawnRegistry.DistanceToNearestPlayer(site.Position, settings.playerTag, Viewer);
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

        var query = new FlatSpots.Query
        {
            center = center,
            searchRadius = settings.searchRadius,
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
            seed = site.Plan.Id.GetHashCode(),
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
            return;
        }
        site.Problem = spotScratch.Count == 0 ? "no flat, dry, open spot within Search Radius" : "only spots in forbidden biomes or too close to another portal";
        Log($"{site.Plan.Id}: {site.Problem}");
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

        site.Portal = instance.GetComponentInChildren<Portal>(true);
        if (site.Portal != null)
        {
            site.Portal.AssignSite(site.Plan.Id, settings.DifficultyAt(new Vector2(site.Position.x, site.Position.z)), settings.closeAfterUse, settings.reopenAfterUse);
            site.Handler = () => OnPortalGone(site);
            site.Portal.OnPortalDestroyed += site.Handler;
        }
        else
        {
            Debug.LogWarning($"PortalSpawner: '{type.prefab.name}' has no Portal component - it stands there but can't be entered. Add a Portal (and a trigger Collider) to the prefab.", type.prefab);
        }
        Log($"{site.Plan.Id}: spawned {type.prefab.name} at {instance.transform.position}");
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
        if (site.Portal != null && site.Handler != null)
            site.Portal.OnPortalDestroyed -= site.Handler;
        if (site.Live)
        {
            site.Live = false;
            spawned = Mathf.Max(0, spawned - 1);
            WorldSpawnRegistry.PortalCount = Mathf.Max(0, WorldSpawnRegistry.PortalCount - 1);
        }
        if (site.Instance != null)
            Destroy(site.Instance);
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
