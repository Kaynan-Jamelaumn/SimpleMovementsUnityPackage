using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// Places the cave entrances of one terrain chunk (added to every chunk by <see cref="EndlessTerrain"/>, started with
    /// its other spawners once the chunk is ready). Where: the planned sites whose point lies in this chunk
    /// (<see cref="CaveSitePlanner"/>, a pure function of the world seed). Each gets the best spot within Search Radius on
    /// this chunk's exact data (<see cref="FlatSpots"/>: dry, not too steep, clear of trees and rocks, an allowed biome
    /// and height; never in a world area without portals or objects - towns, the hub city), kept inside the chunk so only
    /// its data decides it, facing downhill. No spot = no cave there.
    /// <para>Cave mouths are scenery: every machine builds them the same way from the same seed (a network client too -
    /// no network objects are needed); going in is the server's decision (its copy of the player walks into its copy of
    /// the mouth). The mouth is a child of the chunk: it goes with the chunk and comes back identically with it. The cave
    /// behind it keeps its state in the world's save (<see cref="CaveStore"/>), not here.</para>
    /// </summary>
    public sealed class CaveEntranceSpawner : ChunkSpawnerBase
    {
        private sealed class Site
        {
            public CaveSitePlanner.Site Plan;
            public Vector2 SearchCenter;
            public bool Resolved, HasSpot;
            public Vector3 Position;
            public float Yaw;
            public CaveEntrance Instance;
            public string Problem;
        }

        private CaveEntranceSettings settings;
        private readonly List<Site> sites = new List<Site>();
        private readonly List<FlatSpots.Spot> spots = new List<FlatSpots.Spot>();
        private Transform container;
        private bool done;

        public override int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (Site s in sites)
                    if (s.Instance != null)
                        n++;
                return n;
            }
        }

        public override string Describe()
        {
            int empty = 0;
            foreach (Site s in sites)
                if (s.Resolved && !s.HasSpot)
                    empty++;
            return $"Caves {ActiveCount}/{sites.Count} sites ({empty} without a spot)";
        }

        /// <summary>The settings this chunk's caves follow (set by EndlessTerrain before Begin).</summary>
        public void SetSettings(CaveEntranceSettings caveSettings)
        {
            settings = caveSettings;
            detailedLogging = settings != null && settings.enableDetailedLogging;
        }

        protected override float TickInterval => 0.5f;

        protected override void OnBegin()
        {
            sites.Clear();
            done = false;
            if (settings == null || !settings.IsUsable)
                return;
            var planned = new List<CaveSitePlanner.Site>();
            CaveSitePlanner.SitesInArea(settings, WorldSeed, Origin, Origin + Vector2.one * Span, planned);
            foreach (CaveSitePlanner.Site p in planned)
                sites.Add(new Site { Plan = p });
            Log($"{sites.Count} planned cave site(s)");
        }

        protected override void OnEnd()
        {
            foreach (Site s in sites)
                if (s.Instance != null)
                    Destroy(s.Instance.gameObject);
            sites.Clear();
            done = false;
        }

        protected override void Tick()
        {
            // Scenery: placed once per chunk load, on every machine (the same everywhere), paused or not.
            if (done || settings == null)
                return;
            done = true;
            if (sites.Count == 0)
                return;
            container = GetContainer("Cave Entrances");
            for (int i = 0; i < sites.Count; i++)
            {
                Resolve(sites[i], i);
                if (sites[i].HasSpot)
                    Spawn(sites[i]);
            }
        }

        private void Resolve(Site site, int index)
        {
            site.Resolved = true;
            site.HasSpot = false;
            float footprint = Mathf.Max(1f, settings.footprintRadius);
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
                minWaterDistance = footprint + 2f,
                avoidObjects = true,
                // What the seed placed, chopped or not: the spot is the same on every machine (each builds its own mouth)
                // and on every load - a tree cut down later never moves a cave.
                includeRemovedObjects = true,
                objectClearance = settings.objectClearance,
                maxResults = 8,
                preferCenter = 0.35f,
                requireNavMesh = false,
                seed = site.Plan.Seed,
                limitHeight = settings.limitHeight,
                minHeight = settings.minHeight,
                maxHeight = settings.maxHeight,
            };
            spots.Clear();
            FlatSpots.Find(query, spots);
            float spacing = settings.EffectiveMinDistance;
            foreach (FlatSpots.Spot spot in spots)
            {
                if (!settings.IsBiomeAllowed(spot.biome))
                    continue;
                if (WorldAreas.Has(WorldAreaRules.NoPortals, spot.position.x, spot.position.z) || WorldAreas.Has(WorldAreaRules.NoObjects, spot.position.x, spot.position.z))
                    continue;
                bool crowded = false;
                for (int j = 0; j < index && !crowded; j++)
                {
                    Site other = sites[j];
                    crowded = other.HasSpot && (new Vector2(other.Position.x, other.Position.z) - new Vector2(spot.position.x, spot.position.z)).sqrMagnitude < spacing * spacing * 0.64f;
                }
                if (crowded)
                    continue;
                site.HasSpot = true;
                site.Position = spot.position;
                site.Yaw = Facing(spot, site.Plan.Yaw);
                Log($"{site.Plan.Id}: spot at {spot.position} (slope {spot.slope:0.0})");
                return;
            }
            site.Problem = spots.Count == 0 ? "no dry, open spot within Search Radius" : "only spots in forbidden biomes, kept areas or too close to another cave";
            Log($"{site.Plan.Id}: {site.Problem}");
        }

        /// <summary>Downhill on a slope (the mouth opens out of it), the planned facing on flat ground.</summary>
        private static float Facing(FlatSpots.Spot spot, float planned)
        {
            Vector3 n = spot.normal;
            var downhill = new Vector2(n.x, n.z);
            if (downhill.sqrMagnitude < 0.0025f)   // flatter than about 3 degrees
                return planned;
            return Mathf.Atan2(downhill.x, downhill.y) * Mathf.Rad2Deg;
        }

        private void Spawn(Site site)
        {
            DungeonProfile[] profiles = settings.Profiles();
            if (profiles.Length == 0)
                return;
            var go = new GameObject($"Cave Entrance ({site.Plan.Id})");
            go.transform.SetParent(container, true);
            go.transform.SetPositionAndRotation(site.Position - Vector3.up * 0.05f, Quaternion.Euler(0f, site.Yaw, 0f));
            go.layer = gameObject.layer;
            var entrance = go.AddComponent<CaveEntrance>();
            // The cave behind it - its size, difficulty and seed - comes from the planned site, not the spot found for it.
            Vector2 point = site.Plan.Point;
            float difficulty = WorldSpawnHooks.ApplyPortalDifficulty(new Vector3(point.x, site.Position.y, point.y), settings.DifficultyAt(point));
            SizeClass size = settings.SizeAt(point, PlacementRandom.Value(WorldSeed, 0x5A1E, site.Plan.Seed, 0, 0));
            entrance.Setup(settings, site.Plan.Id, site.Plan.Seed, point, profiles, size, difficulty);
            entrance.Build();
            site.Instance = entrance;
            Log($"{site.Plan.Id}: '{entrance.EffectiveName}' ({size}, difficulty {difficulty:0.00}) at {site.Position}");
        }

        private float ClampInside(float value, float origin, float margin)
        {
            float lo = origin + margin, hi = origin + Span - margin;
            return lo <= hi ? Mathf.Clamp(value, lo, hi) : origin + Span * 0.5f;
        }

        private void OnDrawGizmos()
        {
            if (settings == null || !settings.enableVisualDebug)
                return;
            foreach (Site site in sites)
            {
                var planned = new Vector3(site.Plan.Point.x, transform.position.y, site.Plan.Point.y);
                if (LoadedTerrain.TryGetHeight(planned.x, planned.z, out float h))
                    planned.y = h;
                Gizmos.color = new Color(0.8f, 0.55f, 0.25f);
                Gizmos.DrawWireSphere(planned + Vector3.up, 1.2f);
                if (site.HasSpot)
                {
                    Gizmos.DrawLine(site.Position, site.Position + Vector3.up * 14f);
                    Gizmos.DrawLine(site.Position + Vector3.up, site.Position + Vector3.up + Quaternion.Euler(0f, site.Yaw, 0f) * Vector3.forward * 4f);
                }
            }
        }
    }
}
