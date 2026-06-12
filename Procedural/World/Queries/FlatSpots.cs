using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finds flat, open spots on the terrain - for buildings, camps, quest sites, spawn points - by footprint size,
/// slope, unevenness, water, biome, height and distance from placed objects, optionally on the NavMesh and
/// reachable from a point.
///
/// <see cref="Find"/> and <see cref="FindAsync"/> use the loaded chunks' exact data (the same heights the
/// colliders have; see <see cref="LoadedTerrain"/>), so they only see where chunks are loaded.
/// <see cref="FindApproximate"/> works anywhere, from the world's coarse shape (before erosion, without
/// objects) - to pick a far-away site before its chunks exist; check it with Find once they are loaded.
///
/// Everything is deterministic: the same query on the same world gives the same spots.
/// </summary>
public static class FlatSpots
{
    /// <summary>What to look for.</summary>
    [Serializable]
    public class Query
    {
        [Tooltip("Centre of the search (world X, Z).")]
        public Vector2 center;
        [Tooltip("How far from the centre to look (world units).")]
        public float searchRadius = 100f;
        [Tooltip("Radius of the area that must be flat and free (world units).")]
        public float footprintRadius = 4f;
        [Tooltip("Steepest ground allowed under the footprint (degrees).")]
        public float maxSlope = 8f;
        [Tooltip("Largest height difference allowed across the footprint (world units).")]
        public float maxUnevenness = 0.5f;
        [Tooltip("Allow water under the footprint.")]
        public bool allowWater = false;
        [Tooltip("Keep at least this far from water (world units, 0 = no limit).")]
        public float minWaterDistance = 0f;
        [Tooltip("Stay within this distance of water (world units, 0 = no limit) - e.g. a fishing camp.")]
        public float maxWaterDistance = 0f;
        [Tooltip("Only in these biomes (empty = any).")]
        public List<Biome> allowedBiomes = new List<Biome>();
        [Tooltip("Limit the ground height (world Y).")]
        public bool limitHeight = false;
        public float minHeight = 0f;
        public float maxHeight = 100f;
        [Tooltip("Keep clear of placed objects (trees, rocks...): no object's footprint within the footprint plus Object Clearance.")]
        public bool avoidObjects = true;
        [Tooltip("Extra free space around the footprint (world units).")]
        public float objectClearance = 1f;
        [Tooltip("How many spots to return at most (the best first).")]
        public int maxResults = 1;
        [Tooltip("Smallest distance between two returned spots (world units, 0 = two footprints).")]
        public float minSpacing = 0f;
        [Tooltip("Distance between the spots tried (world units, 0 = the footprint radius). Smaller finds tighter fits but is slower.")]
        public float sampleSpacing = 0f;
        [Tooltip("Prefer spots near the centre (0 = only flatness counts, 1 = distance matters as much as flatness).")]
        [Range(0f, 1f)] public float preferCenter = 0.25f;
        [Tooltip("Only spots on the NavMesh (loaded chunks only).")]
        public bool requireNavMesh = false;
        [Tooltip("How far from the spot the NavMesh may be (world units).")]
        public float navMeshSampleDistance = 2f;
        [Tooltip("Only spots with a complete NavMesh path from Reachable From (needs Require NavMesh).")]
        public bool requireReachable = false;
        public Vector3 reachableFrom;
        [Tooltip("Changes which of several equally good spots are picked.")]
        public int seed = 0;
    }

    /// <summary>A spot found.</summary>
    public struct Spot
    {
        /// <summary>The footprint's centre on the ground.</summary>
        public Vector3 position;
        /// <summary>The ground's normal averaged over the footprint.</summary>
        public Vector3 normal;
        /// <summary>Slope (degrees) and height difference (world units) across the footprint.</summary>
        public float slope, unevenness;
        /// <summary>0-1, higher is better (flatter, nearer the centre).</summary>
        public float score;
        public Biome biome;
        /// <summary>True when found by <see cref="FindApproximate"/> (the world's coarse shape).</summary>
        public bool approximate;
    }

    // Footprint samples: the centre, and rings at half and full radius.
    private const int RingPoints = 8;

    /// <summary>
    /// Flat spots among the loaded chunks, best first (main thread). Returns how many were added to
    /// <paramref name="results"/>. Only places where chunks are loaded (and, with the NavMesh options, have
    /// their NavMesh) can be found.
    /// </summary>
    public static int Find(Query query, List<Spot> results)
    {
        if (query == null || results == null)
            return 0;
        Area area = Area.FromLoaded(query);
        List<Spot> ranked = Rank(query, area, SampleLoaded);
        return Select(query, ranked, results, true);
    }

    /// <summary>
    /// As <see cref="Find"/>, with the search on a worker thread of <paramref name="generator"/> (only the NavMesh
    /// checks and the callback run on the main thread). <paramref name="callback"/> gets the spots, best first.
    /// </summary>
    public static void FindAsync(TerrainGenerator generator, Query query, Action<List<Spot>> callback)
    {
        if (generator == null || query == null)
        {
            callback?.Invoke(new List<Spot>());
            return;
        }
        Area area = Area.FromLoaded(query);
        generator.WorkerPool.Enqueue(() =>
        {
            List<Spot> ranked = Rank(query, area, SampleLoaded);
            generator.RunOnMainThread(() =>
            {
                var results = new List<Spot>();
                Select(query, ranked, results, true);
                callback?.Invoke(results);
            });
        });
    }

    /// <summary>
    /// Flat spots anywhere, from the world's coarse shape (heights before erosion, the sea and lakes, biomes) - no
    /// chunk needs to be loaded, and no objects are known yet. Thread-safe. Check a spot with <see cref="Find"/>
    /// (small search radius around it) once its chunks are loaded, before building on it.
    /// </summary>
    public static int FindApproximate(TerrainGenerator generator, Query query, List<Spot> results)
    {
        if (generator == null || query == null || results == null)
            return 0;
        WaterSettings water = generator.EnableWater ? WaterSettings.From(generator) : null;
        var sampler = new TerrainHeightSampler(generator, water);
        var area = new Area { Sampler = sampler, WaterSettings = water };
        List<Spot> ranked = Rank(query, area, SampleCoarse);
        return Select(query, ranked, results, false);
    }

    // ------------------------------------------------------------------ search

    /// <summary>What a search reads: loaded chunks (copied references, so a worker thread can read them) or the coarse sampler.</summary>
    private sealed class Area
    {
        public Dictionary<Vector2Int, LoadedTerrain.Chunk> Chunks;
        public int Span;
        public List<Vector4> Objects;   // x, y, z, radius
        public TerrainHeightSampler Sampler;
        public WaterSettings WaterSettings;
        // Distance to water over the search area (loaded mode), built when the query needs it.
        public float[] WaterDistance;
        public int FieldX, FieldZ, FieldSize;

        public static Area FromLoaded(Query query)
        {
            var area = new Area { Chunks = new Dictionary<Vector2Int, LoadedTerrain.Chunk>(), Span = LoadedTerrain.ChunkSpan, Objects = new List<Vector4>() };
            if (area.Span <= 0)
                return area;

            float reach = query.searchRadius + query.footprintRadius + Mathf.Max(query.minWaterDistance, query.maxWaterDistance) + 2f;
            int cx0 = Mathf.FloorToInt((query.center.x - reach) / area.Span), cx1 = Mathf.FloorToInt((query.center.x + reach) / area.Span);
            int cz0 = Mathf.FloorToInt((query.center.y - reach) / area.Span), cz1 = Mathf.FloorToInt((query.center.y + reach) / area.Span);
            foreach (LoadedTerrain.Chunk chunk in LoadedTerrain.All)
            {
                if (chunk.Coord.x >= cx0 && chunk.Coord.x <= cx1 && chunk.Coord.y >= cz0 && chunk.Coord.y <= cz1)
                    area.Chunks[chunk.Coord] = chunk;
            }

            if (query.avoidObjects)
            {
                float objectReach = query.searchRadius + query.footprintRadius + query.objectClearance;
                LoadedTerrain.ForEachObjectNear(query.center.x, query.center.y, objectReach,
                    (position, radius) => area.Objects.Add(new Vector4(position.x, position.y, position.z, radius)));
            }
            return area;
        }

        public bool TryGetChunk(float x, float z, out LoadedTerrain.Chunk chunk)
        {
            chunk = null;
            return Span > 0 && Chunks.TryGetValue(new Vector2Int(Mathf.FloorToInt(x / Span), Mathf.FloorToInt(z / Span)), out chunk);
        }
    }

    /// <summary>One footprint sample: ground height, water there, biome. False where there is no data.</summary>
    private delegate bool Sampler(Area area, float x, float z, out float height, out bool wet, out Biome biome);

    private static bool SampleLoaded(Area area, float x, float z, out float height, out bool wet, out Biome biome)
    {
        height = 0f;
        wet = false;
        biome = null;
        if (!area.TryGetChunk(x, z, out LoadedTerrain.Chunk chunk) || chunk.Heights == null)
            return false;
        height = LoadedTerrain.SurfaceHeight(chunk, x, z);
        if (chunk.Water != null)
        {
            int wx = Mathf.Clamp(Mathf.RoundToInt(x) - chunk.OriginX, 0, chunk.Water.Size - 1);
            int wz = Mathf.Clamp(Mathf.RoundToInt(z) - chunk.OriginZ, 0, chunk.Water.Size - 1);
            wet = chunk.Water.Type[wx, wz] != WaterBodyType.None;
        }
        if (chunk.Biomes != null)
        {
            int size = chunk.Biomes.GetLength(0);
            biome = chunk.Biomes[Mathf.Clamp(Mathf.FloorToInt(x) - chunk.OriginX, 0, size - 1), Mathf.Clamp(Mathf.FloorToInt(z) - chunk.OriginZ, 0, size - 1)];
        }
        return true;
    }

    private static bool SampleCoarse(Area area, float x, float z, out float height, out bool wet, out Biome biome)
    {
        TerrainHeightSampler sampler = area.Sampler;
        height = sampler.SampleBaseHeight(x, z);
        biome = sampler.SampleBiome(Mathf.FloorToInt(x), Mathf.FloorToInt(z));
        wet = false;
        WaterSettings water = area.WaterSettings;
        if (water != null)
        {
            if (water.OceansEnabled && height < water.SeaLevel && OceanGenerator.LandSide(water, x, z) < 0f)
                wet = true;
            else if (LakeGenerator.FindLakeContaining(new Vector2(x, z), null, water, sampler, LakeFeature.InnerFraction) != null)
                wet = true;
        }
        return true;
    }

    /// <summary>Every candidate spot that passes the query, best first.</summary>
    private static List<Spot> Rank(Query query, Area area, Sampler sample)
    {
        var spots = new List<Spot>();
        float radius = Mathf.Max(0.5f, query.footprintRadius);
        float step = query.sampleSpacing > 0f ? query.sampleSpacing : Mathf.Max(1f, radius);
        float search = Mathf.Max(0f, query.searchRadius);
        int seedSalt = PlacementRandom.StableHash("FlatSpots") ^ query.seed * 0x2C1B3C6D;
        bool needsWaterDistance = query.minWaterDistance > 0f || query.maxWaterDistance > 0f;
        if (needsWaterDistance && area.Chunks != null)
            BuildWaterDistance(query, area);

        var offsets = new Vector2[1 + 2 * RingPoints];
        offsets[0] = Vector2.zero;
        for (int i = 0; i < RingPoints; i++)
        {
            float angle = i * Mathf.PI * 2f / RingPoints;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            offsets[1 + i] = direction * radius;
            offsets[1 + RingPoints + i] = direction * (radius * 0.5f);
        }

        int gx0 = Mathf.FloorToInt((query.center.x - search) / step), gx1 = Mathf.FloorToInt((query.center.x + search) / step);
        int gz0 = Mathf.FloorToInt((query.center.y - search) / step), gz1 = Mathf.FloorToInt((query.center.y + search) / step);
        var heights = new float[offsets.Length];
        for (int gz = gz0; gz <= gz1; gz++)
        {
            for (int gx = gx0; gx <= gx1; gx++)
            {
                // A world-aligned, jittered grid: the same spots are tried whatever the search centre.
                float x = (gx + 0.2f + 0.6f * PlacementRandom.Value(seedSalt, 1, gx, gz, 0)) * step;
                float z = (gz + 0.2f + 0.6f * PlacementRandom.Value(seedSalt, 1, gx, gz, 1)) * step;
                float dx = x - query.center.x, dz = z - query.center.y;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance > search)
                    continue;

                if (TryEvaluate(query, area, sample, x, z, offsets, heights, radius, out Spot spot))
                {
                    float flatness = 0.5f * (spot.slope / Mathf.Max(0.1f, query.maxSlope)) + 0.5f * (spot.unevenness / Mathf.Max(0.01f, query.maxUnevenness));
                    float nearness = search > 0f ? distance / search : 0f;
                    float jitter = 0.001f * PlacementRandom.Value(seedSalt, 2, gx, gz, 0);
                    spot.score = Mathf.Clamp01(1f - Mathf.Lerp(flatness, nearness, Mathf.Clamp01(query.preferCenter) * 0.5f)) - jitter;
                    spots.Add(spot);
                }
            }
        }

        spots.Sort((a, b) =>
        {
            int c = b.score.CompareTo(a.score);
            if (c != 0) return c;
            c = a.position.x.CompareTo(b.position.x);
            return c != 0 ? c : a.position.z.CompareTo(b.position.z);
        });
        return spots;
    }

    private static bool TryEvaluate(Query query, Area area, Sampler sample, float x, float z, Vector2[] offsets, float[] heights, float radius, out Spot spot)
    {
        spot = default;
        Biome centreBiome = null;
        float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
        for (int i = 0; i < offsets.Length; i++)
        {
            float px = x + offsets[i].x, pz = z + offsets[i].y;
            if (!sample(area, px, pz, out float h, out bool wet, out Biome biome))
                return false;
            if (wet && !query.allowWater)
                return false;
            if (query.allowedBiomes != null && query.allowedBiomes.Count > 0 && !query.allowedBiomes.Contains(biome))
                return false;
            if (i == 0)
                centreBiome = biome;
            heights[i] = h;
            lowest = Mathf.Min(lowest, h);
            highest = Mathf.Max(highest, h);
            // Early out on the first ring: most spots fail here.
            if (highest - lowest > query.maxUnevenness)
                return false;
        }

        float centre = heights[0];
        if (query.limitHeight && (centre < query.minHeight || centre > query.maxHeight))
            return false;

        // Slope of the plane through the ring (least squares): the footprint's overall tilt.
        float sx = 0f, sz = 0f;
        for (int i = 1; i < offsets.Length; i++)
        {
            float d = heights[i] - centre;
            sx += offsets[i].x * d;
            sz += offsets[i].y * d;
        }
        float norm = 0f;
        for (int i = 1; i < offsets.Length; i++)
            norm += offsets[i].x * offsets[i].x;
        float gradX = norm > 0f ? sx / norm : 0f, gradZ = norm > 0f ? sz / norm : 0f;
        Vector3 normal = new Vector3(-gradX, 1f, -gradZ).normalized;
        float slope = Mathf.Acos(Mathf.Clamp(normal.y, -1f, 1f)) * Mathf.Rad2Deg;
        if (slope > query.maxSlope)
            return false;

        if ((query.minWaterDistance > 0f || query.maxWaterDistance > 0f) && area.WaterDistance != null)
        {
            float waterDistance = WaterDistanceAt(area, x, z) - radius;
            if (query.minWaterDistance > 0f && waterDistance < query.minWaterDistance)
                return false;
            if (query.maxWaterDistance > 0f && waterDistance > query.maxWaterDistance)
                return false;
        }

        if (query.avoidObjects && area.Objects != null)
        {
            foreach (Vector4 o in area.Objects)
            {
                float reach = radius + o.w + query.objectClearance;
                float ox = o.x - x, oz = o.z - z;
                if (ox * ox + oz * oz < reach * reach)
                    return false;
            }
        }

        // The rings can miss a bump or a puddle between them: confirm the whole footprint. The loaded terrain is
        // flat between its mesh vertices, so its highest and lowest points are at vertices inside the footprint or
        // on its edge - check exactly those; the coarse shape is checked on a one-unit grid.
        int lod = 1;
        if (area.Chunks != null && area.TryGetChunk(x, z, out LoadedTerrain.Chunk centreChunk))
            lod = Mathf.Max(1, centreChunk.LodFactor);
        float step = area.Chunks != null ? lod : 1f;
        float startX = area.Chunks != null ? Mathf.Ceil((x - radius) / lod) * lod : x - radius;
        float startZ = area.Chunks != null ? Mathf.Ceil((z - radius) / lod) * lod : z - radius;
        for (float pz = startZ; pz <= z + radius; pz += step)
        {
            for (float px = startX; px <= x + radius; px += step)
            {
                float dx = px - x, dz = pz - z;
                if (dx * dx + dz * dz > radius * radius)
                    continue;
                if (!FootprintPointValid(query, area, sample, px, pz, ref lowest, ref highest))
                    return false;
            }
        }
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI / 16f;
            if (!FootprintPointValid(query, area, sample, x + Mathf.Cos(angle) * radius, z + Mathf.Sin(angle) * radius, ref lowest, ref highest))
                return false;
        }
        if (highest - lowest > query.maxUnevenness)
            return false;

        spot = new Spot
        {
            position = new Vector3(x, centre, z),
            normal = normal,
            slope = slope,
            unevenness = highest - lowest,
            biome = centreBiome,
            approximate = area.Sampler != null,
        };
        return true;
    }

    private static bool FootprintPointValid(Query query, Area area, Sampler sample, float x, float z, ref float lowest, ref float highest)
    {
        if (!sample(area, x, z, out float h, out bool wet, out Biome _))
            return false;
        if (wet && !query.allowWater)
            return false;
        lowest = Mathf.Min(lowest, h);
        highest = Mathf.Max(highest, h);
        return highest - lowest <= query.maxUnevenness;
    }

    /// <summary>The best spots, at least Min Spacing apart, checked against the NavMesh when asked (main thread).</summary>
    private static int Select(Query query, List<Spot> ranked, List<Spot> results, bool navMeshChecks)
    {
        int added = 0;
        float spacing = query.minSpacing > 0f ? query.minSpacing : 2f * Mathf.Max(0.5f, query.footprintRadius);
        var chosen = new List<Spot>();
        NavMeshPath path = null;
        foreach (Spot candidate in ranked)
        {
            if (added >= Mathf.Max(1, query.maxResults))
                break;

            bool tooClose = false;
            foreach (Spot other in chosen)
            {
                float dx = other.position.x - candidate.position.x, dz = other.position.z - candidate.position.z;
                if (dx * dx + dz * dz < spacing * spacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
                continue;

            Spot spot = candidate;
            if (navMeshChecks && query.requireNavMesh)
            {
                if (!NavMesh.SamplePosition(spot.position, out NavMeshHit hit, Mathf.Max(0.1f, query.navMeshSampleDistance), NavMesh.AllAreas))
                    continue;
                if (query.requireReachable)
                {
                    if (path == null)
                        path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(query.reachableFrom, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                        continue;
                }
            }

            chosen.Add(spot);
            results.Add(spot);
            added++;
        }
        return added;
    }

    /// <summary>Exact distances to water over the search area, from the loaded chunks' water maps.</summary>
    private static void BuildWaterDistance(Query query, Area area)
    {
        float reach = query.searchRadius + query.footprintRadius + Mathf.Max(query.minWaterDistance, query.maxWaterDistance) + 2f;
        int x0 = Mathf.FloorToInt(query.center.x - reach), z0 = Mathf.FloorToInt(query.center.y - reach);
        int size = Mathf.CeilToInt(2f * reach) + 1;
        if (size > 4096)
            return;   // too big to be worth it: water distance rules are then skipped
        var seeds = new bool[size * size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int wx = x0 + x, wz = z0 + z;
                if (!area.TryGetChunk(wx, wz, out LoadedTerrain.Chunk chunk) || chunk.Water == null)
                    continue;
                int cx = wx - chunk.OriginX, cz = wz - chunk.OriginZ;
                if (cx >= 0 && cz >= 0 && cx < chunk.Water.Size && cz < chunk.Water.Size)
                    seeds[z * size + x] = chunk.Water.Type[cx, cz] != WaterBodyType.None;
            }
        }
        area.WaterDistance = PlacementFields.DistanceTransform(seeds, size);
        area.FieldX = x0;
        area.FieldZ = z0;
        area.FieldSize = size;
    }

    private static float WaterDistanceAt(Area area, float x, float z)
    {
        int cx = Mathf.Clamp(Mathf.RoundToInt(x) - area.FieldX, 0, area.FieldSize - 1);
        int cz = Mathf.Clamp(Mathf.RoundToInt(z) - area.FieldZ, 0, area.FieldSize - 1);
        return area.WaterDistance[cz * area.FieldSize + cx];
    }
}
