using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One object to spawn, as decided by <see cref="ObjectPlacementEngine"/>.</summary>
public struct ObjectPlacement
{
    /// <summary>Index into <see cref="PlacementPlan.Types"/>.</summary>
    public int Type;
    public Vector3 Position;
    public Quaternion Rotation;
    /// <summary>Final local scale (the prefab's own scale times the placement scale).</summary>
    public Vector3 Scale;
    /// <summary>Radius of the whole object seen from above (world units).</summary>
    public float Radius;
    /// <summary>Where the bottom of the object's renderers should end up (world Y), for the after-spawn check; NaN = don't check.</summary>
    public float ExpectedMinY;
}

/// <summary>A chunk's placement result: the objects to spawn, in placement order, plus counts for tuning.</summary>
public sealed class PlacementResult
{
    public readonly List<ObjectPlacement> Objects = new List<ObjectPlacement>();
    /// <summary>The plan the placements were made with (their <see cref="ObjectPlacement.Type"/> indexes its types).</summary>
    public PlacementPlan Plan;
    /// <summary>Per type (plan order): candidate spots tried, and accepted.</summary>
    public int[] Tried, Accepted;
    /// <summary>Per type (plan order): time spent placing it, in stopwatch ticks.</summary>
    public long[] Ticks;
    /// <summary>Per type (plan order), per stage (see <see cref="ObjectPlacementEngine.Stage"/>): spots rejected there.</summary>
    public int[][] Rejected;

    /// <summary>Spots rejected at a stage, over all types.</summary>
    public int RejectedAt(ObjectPlacementEngine.Stage stage)
    {
        int total = 0;
        if (Rejected != null)
            foreach (int[] perType in Rejected)
                total += perType[(int)stage];
        return total;
    }
}

/// <summary>
/// Decides where a chunk's objects go - trees, rocks, plants, landmarks - by each object's rules
/// (<see cref="BiomeObject"/>). It runs on a worker thread and only produces a list of placements; the
/// objects are created afterwards on the main thread, a few per frame (see <see cref="PlacementInstantiator"/>).
///
/// Each object type goes through a pipeline:
/// <code>
/// candidate spot (jittered world grid, one per cell)
///   -> density roll (noise patches, clusters, biome-border fading)  -+ world-consistent
///   -> biome (allowed / forbidden / borders / incompatible neighbours) -+
///   -> slope and surface -> water -> altitude, land shape, climate -> distances to features
///   -> relationships (spacing, exclusions, avoid/attract/require) -> region/cluster/chunk limits
///   -> orientation -> scale -> ground contact and footprint fit -> accepted
/// </code>
/// Every random choice is a hash of the seed, the object type and the candidate's world cell, so the result
/// for a chunk never depends on load order, thread timing or other chunks. Types are placed in dependency
/// order (see <see cref="PlacementPlan"/>), each checking only against types placed before it.
///
/// Across chunk borders, a chunk can't see its neighbours' exact results (it doesn't have their terrain). It
/// does know, exactly, which of their candidate spots pass the world-consistent first stages (position,
/// priority, density roll, biome) - and treats those as possibly occupied. So a spot is only kept if no
/// neighbouring spot could conflict with it: two chunks can never both keep conflicting spots, and every
/// chunk makes the same decision whenever it is generated. Requires Nearby is the other way round: only
/// objects certainly there (in the same chunk) satisfy it, so it is never broken either.
/// </summary>
public static partial class ObjectPlacementEngine
{
    /// <summary>Pipeline stages, for rejection counts.</summary>
    public enum Stage
    {
        Chance,
        Biome,
        Slope,
        Water,
        Altitude,
        Climate,
        Feature,
        Relations,
        Spacing,
        Limits,
        Orientation,
        Ground,
        Count,
    }

    /// <summary>The generated data of the chunk being populated.</summary>
    public sealed class ChunkInput
    {
        /// <summary>World cell of the chunk's corner.</summary>
        public int OriginX, OriginY;
        /// <summary>Cells the chunk owns per side (ChunkSize - 1); candidates at [origin, origin + Span) belong to it.</summary>
        public int Span;
        /// <summary>Final heights (ChunkSize + 1 per side).</summary>
        public float[,] HeightMap;
        /// <summary>Biome per cell (ChunkSize per side).</summary>
        public Biome[,] BiomeMap;
        public WaterMapData Water;
        /// <summary>Environment around the chunk (may be null: then only the chunk's own data is used).</summary>
        public PlacementFields Fields;
        /// <summary>Mesh vertex spacing at the chunk's base level of detail.</summary>
        public int LodFactor = 1;
    }

    // Random streams per candidate.
    private const int StreamJitterX = 0, StreamJitterY = 1, StreamPriority = 2, StreamChance = 3, StreamSoft = 4,
        StreamSoftSpacing = 5, StreamGrowth = 6, StreamYaw = 10, StreamTiltAxis = 11, StreamTilt = 12,
        StreamQuatA = 13, StreamQuatB = 14, StreamQuatC = 15, StreamScale = 16, StreamClusterX = 20, StreamClusterY = 21, StreamClusterR = 22;

    private struct Candidate
    {
        public int CellX, CellY;
        public float X, Z;
        public float Priority;
        public float RawChance;
        public float Gate;
        public int Biome;
        public bool InCluster;
        public int ClusterX, ClusterZ;
        // Filled by the site evaluation:
        public float Soft;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
        public float ExpectedMinY;
    }

    /// <summary>A placed or possible object, for relationship and spacing queries.</summary>
    private struct Entry
    {
        public float X, Z;
        public int Type;
        public float Priority;
    }

    /// <summary>A uniform grid of <see cref="Entry"/> for "anything of these types within R" queries.</summary>
    private sealed class SpatialHash
    {
        private const float CellSize = 8f;
        private readonly Dictionary<long, List<Entry>> cells = new Dictionary<long, List<Entry>>();
        public int Count;

        public void Add(Entry entry)
        {
            long key = Key(Mathf.FloorToInt(entry.X / CellSize), Mathf.FloorToInt(entry.Z / CellSize));
            if (!cells.TryGetValue(key, out List<Entry> list))
                cells[key] = list = new List<Entry>(4);
            list.Add(entry);
            Count++;
        }

        /// <summary>Calls <paramref name="visit"/> for entries within <paramref name="radius"/> of (x, z); stops when it returns false.</summary>
        public void Query(float x, float z, float radius, Func<Entry, float, bool> visit)
        {
            if (Count == 0)
                return;
            int x0 = Mathf.FloorToInt((x - radius) / CellSize), x1 = Mathf.FloorToInt((x + radius) / CellSize);
            int z0 = Mathf.FloorToInt((z - radius) / CellSize), z1 = Mathf.FloorToInt((z + radius) / CellSize);
            float r2 = radius * radius;
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    if (!cells.TryGetValue(Key(cx, cz), out List<Entry> list))
                        continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        float dx = list[i].X - x, dz = list[i].Z - z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 <= r2 && !visit(list[i], Mathf.Sqrt(d2)))
                            return;
                    }
                }
            }
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }

    private struct RankKey : IEquatable<RankKey>
    {
        public int Type, X, Z;
        public bool Cluster;

        public bool Equals(RankKey other) => Type == other.Type && X == other.X && Z == other.Z && Cluster == other.Cluster;
        public override bool Equals(object obj) => obj is RankKey other && Equals(other);
        public override int GetHashCode() => ((Type * 397 ^ X) * 397 ^ Z) * 2 + (Cluster ? 1 : 0);
    }

    /// <summary>Per-chunk working state shared by all types.</summary>
    private sealed class ChunkState
    {
        public PlacementPlan Plan;
        public ChunkInput Input;
        public PlacementEnvironment Env;
        public PlacementResult Result;
        /// <summary>Objects accepted in this chunk so far (all types).</summary>
        public readonly SpatialHash Accepted = new SpatialHash();
        /// <summary>Per type: its possible spots just outside this chunk (in neighbouring chunks).</summary>
        public SpatialHash[] Potential;
        public int[] AcceptedCount;
        public readonly Dictionary<RankKey, float> RankThresholds = new Dictionary<RankKey, float>();
        /// <summary>The type being placed.</summary>
        public int TypeIndex;

        public void Reject(Stage stage)
        {
            Result.Rejected[TypeIndex][(int)stage]++;
        }

        public bool Owns(float x, float z)
        {
            return x >= Input.OriginX && x < Input.OriginX + Input.Span && z >= Input.OriginY && z < Input.OriginY + Input.Span;
        }

        /// <summary>Distance from a point inside the chunk to the edge of the area it owns.</summary>
        public float EdgeDistance(float x, float z)
        {
            return Mathf.Min(Mathf.Min(x - Input.OriginX, Input.OriginX + Input.Span - x), Mathf.Min(z - Input.OriginY, Input.OriginY + Input.Span - z));
        }
    }

    /// <summary>
    /// Places every object type of the plan in one chunk. Returns null if <paramref name="token"/> was cancelled
    /// on the way. Safe to call for several chunks at once on different threads.
    /// </summary>
    public static PlacementResult Place(PlacementPlan plan, ChunkInput input, WorkToken token = null)
    {
        var result = new PlacementResult { Plan = plan };
        if (plan == null || plan.IsEmpty || input == null || input.HeightMap == null)
        {
            result.Tried = result.Accepted = new int[0];
            result.Ticks = new long[0];
            result.Rejected = new int[0][];
            return result;
        }

        var state = new ChunkState
        {
            Plan = plan,
            Input = input,
            Env = new PlacementEnvironment(plan, input),
            Result = result,
            Potential = new SpatialHash[plan.Types.Length],
            AcceptedCount = new int[plan.Types.Length],
        };
        result.Tried = new int[plan.Types.Length];
        result.Accepted = new int[plan.Types.Length];
        result.Ticks = new long[plan.Types.Length];
        result.Rejected = new int[plan.Types.Length][];
        for (int t = 0; t < plan.Types.Length; t++)
            result.Rejected[t] = new int[(int)Stage.Count];

        var watch = System.Diagnostics.Stopwatch.StartNew();
        foreach (PlacementType type in plan.Types)
        {
            if (token != null && token.IsCancelled)
                return null;
            long start = watch.ElapsedTicks;
            state.TypeIndex = type.Index;
            state.Potential[type.Index] = new SpatialHash();
            if (type.Landmark)
                PlaceLandmarks(state, type);
            else
                PlaceScatter(state, type);
            result.Ticks[type.Index] = watch.ElapsedTicks - start;
        }
        return result;
    }

    // ------------------------------------------------------------------ scatter

    private static void PlaceScatter(ChunkState state, PlacementType type)
    {
        ChunkInput input = state.Input;
        float cell = type.CandidateCell;
        int cx0 = Mathf.FloorToInt(input.OriginX / cell), cx1 = Mathf.FloorToInt((input.OriginX + input.Span) / cell);
        int cy0 = Mathf.FloorToInt(input.OriginY / cell), cy1 = Mathf.FloorToInt((input.OriginY + input.Span) / cell);

        // 1. This chunk's candidates through the non-relational stages. One hash per cell decides most of them:
        //    a roll above the highest chance the cell could have is final.
        var valid = new List<Candidate>();
        float bound = ChanceBound(type);
        for (int cy = cy0; cy <= cy1; cy++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                if (PlacementRandom.Value(type.Seed, type.Salt, cx, cy, StreamChance) >= bound)
                    continue;
                Candidate c = MakeCandidate(type, cx, cy);
                if (!state.Owns(c.X, c.Z))
                    continue;
                state.Result.Tried[type.Index]++;

                if (!WorldTest(state, type, ref c, false, out _))
                    continue;
                // Hard relationships only look at types placed before this one, so they can be checked before the
                // (more expensive) site - an object that must be near a tree skips every spot without one.
                if (type.Checks.Count > 0 && !RelationsAllow(state, type, c.X, c.Z))
                {
                    state.Reject(Stage.Relations);
                    continue;
                }
                if (!EvaluateSite(state, type, ref c))
                    continue;
                c.Soft *= RelationFactor(state, type, c.X, c.Z);

                float second = c.Gate > 0f ? Mathf.Min(1f, c.RawChance * c.Soft / c.Gate) : 0f;
                if (Rand(type, c, StreamSoft) < second)
                    valid.Add(c);
                else
                    state.Reject(Stage.Chance);
            }
        }

        // 2. Possible spots of this type just across the chunk's borders.
        CollectPotential(state, type);

        // 3. Relationships, spacing and limits, in priority order.
        valid.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        var validHash = new SpatialHash();
        for (int i = 0; i < valid.Count; i++)
            validHash.Add(new Entry { X = valid[i].X, Z = valid[i].Z, Type = type.Index, Priority = valid[i].Priority });

        var acceptedHere = new SpatialHash();
        var acceptedList = new List<Candidate>();
        foreach (Candidate c in valid)
        {
            if (state.AcceptedCount[type.Index] >= type.MaxPerChunk)
            {
                state.Reject(Stage.Limits);
                continue;
            }

            // Hard spacing: a higher-priority valid spot of the same type (here) or possible spot (next door) nearby wins.
            if (BlockedBySameType(state, type, c, validHash))
            {
                state.Reject(Stage.Spacing);
                continue;
            }

            if (!SoftSpacingAllows(state, type, c, acceptedHere, StreamSoftSpacing))
            {
                state.Reject(Stage.Spacing);
                continue;
            }

            if (!WithinRankLimits(state, type, c))
            {
                state.Reject(Stage.Limits);
                continue;
            }

            Accept(state, type, c, acceptedHere);
            acceptedList.Add(c);
        }

        // 4. Growth: spots next to accepted copies that lost their chance roll get another try (not near the chunk
        //    edge, where the neighbour's growth can't be known).
        if (type.Def.clustering.growthRadius > 0f && acceptedList.Count > 0)
            Grow(state, type, acceptedList, acceptedHere);
    }

    /// <summary>The highest first-roll chance any cell of this type can have (noise and border fading only lower it).</summary>
    private static float ChanceBound(PlacementType type)
    {
        float cellArea = type.CandidateCell * type.CandidateCell;
        float cluster = type.Clustered ? Mathf.Max(type.InsideDensity, type.OutsideDensity) : 1f;
        return Mathf.Min(1f, type.ChancePerSquareUnit * cellArea * cluster * type.MaxBoost);
    }

    private static Candidate MakeCandidate(PlacementType type, int cellX, int cellY)
    {
        var c = new Candidate { CellX = cellX, CellY = cellY, Biome = -1 };
        float cell = type.CandidateCell;
        c.X = (cellX + PlacementRandom.Value(type.Seed, type.Salt, cellX, cellY, StreamJitterX)) * cell;
        c.Z = (cellY + PlacementRandom.Value(type.Seed, type.Salt, cellX, cellY, StreamJitterY)) * cell;
        c.Priority = PlacementRandom.Value(type.Seed, type.Salt, cellX, cellY, StreamPriority);
        return c;
    }

    private static float Rand(PlacementType type, Candidate c, int stream)
    {
        return PlacementRandom.Value(type.Seed, type.Salt, c.CellX, c.CellY, stream);
    }

    /// <summary>
    /// The world-consistent first stages: density (noise, clusters, border fading) and biome. The same answer in
    /// every chunk that asks, because it only uses world positions and the biome lookup the biome map is built from.
    /// <paramref name="eligible"/> = the biome and border rules allow it (whatever the chance roll said); the
    /// return value = it also passed the chance roll (including headroom for attraction).
    /// </summary>
    private static bool WorldTest(ChunkState state, PlacementType type, ref Candidate c, bool needEligibility, out bool eligible)
    {
        eligible = false;
        float cellArea = type.CandidateCell * type.CandidateCell;
        float p = type.ChancePerSquareUnit * cellArea * NoiseFactor(type, c.X, c.Z) * ClusterFactor(type, c.X, c.Z, out c.InCluster, out c.ClusterX, out c.ClusterZ);
        float roll = Rand(type, c, StreamChance);
        // Nothing below raises the chance, so a failed roll here is final (unless the caller needs to know whether
        // the spot was otherwise eligible, for growth).
        if (!needEligibility && roll >= Mathf.Min(1f, p * type.MaxBoost))
            return false;

        Biome biome = state.Env.BiomeAtCell(Mathf.FloorToInt(c.X), Mathf.FloorToInt(c.Z));
        if (biome == null || !state.Plan.BiomeIndex.TryGetValue(biome, out int biomeIndex) || !type.AllowedBiome[biomeIndex])
        {
            state.Reject(Stage.Biome);
            return false;
        }
        c.Biome = biomeIndex;
        // (Biome-border rules are judged with the site, see EvaluateSite: they only reject or lower the chance,
        // so they don't need to be world-consistent, and there they only run for spots that passed cheaper tests.)

        eligible = true;
        c.RawChance = p;
        c.Gate = Mathf.Min(1f, p * type.MaxBoost);
        return roll < c.Gate;
    }

    /// <summary>The world-consistent test alone (for spots in neighbouring chunks): would this spot be considered at all?</summary>
    private static bool WorldTestOnly(ChunkState state, PlacementType type, ref Candidate c)
    {
        return WorldTest(state, type, ref c, false, out _);
    }

    private static float NoiseFactor(PlacementType type, float x, float z)
    {
        DensityNoiseRules noise = type.Def.densityNoise;
        if (noise == null || !noise.enabled || noise.strength <= 0f)
            return 1f;
        float scale = Mathf.Max(1f, noise.scale);
        float value = PlacementRandom.Noise(x / scale, z / scale, type.Seed, type.Salt + 101, noise.octaves);
        float threshold = 1f - Mathf.Clamp01(noise.coverage);
        float patch = WaterGenerator.SmoothStep01((value - (threshold - 0.12f)) / 0.24f);
        return Mathf.Lerp(1f, patch, Mathf.Clamp01(noise.strength));
    }

    /// <summary>Density multiplier from world-space cluster centres (one per cluster cell), and the cell of the strongest cluster here.</summary>
    private static float ClusterFactor(PlacementType type, float x, float z, out bool inCluster, out int clusterX, out int clusterZ)
    {
        inCluster = false;
        clusterX = clusterZ = 0;
        if (!type.Clustered)
            return 1f;

        float cellSize = type.ClusterCell;
        int gx = Mathf.FloorToInt(x / cellSize), gz = Mathf.FloorToInt(z / cellSize);
        float best = 0f;
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int cx = gx + dx, cz = gz + dz;
                ClusterCentre(type, cx, cz, out float centreX, out float centreZ, out float radius);
                float ddx = x - centreX, ddz = z - centreZ;
                float t2 = (ddx * ddx + ddz * ddz) / (radius * radius);
                if (t2 >= 1f)
                    continue;
                float f = (1f - t2) * (1f - t2);
                if (f > best)
                {
                    best = f;
                    inCluster = true;
                    clusterX = cx;
                    clusterZ = cz;
                }
            }
        }
        return Mathf.Lerp(type.OutsideDensity, type.InsideDensity, best);
    }

    private static void ClusterCentre(PlacementType type, int cx, int cz, out float x, out float z, out float radius)
    {
        x = (cx + PlacementRandom.Value(type.Seed, type.Salt, cx, cz, StreamClusterX)) * type.ClusterCell;
        z = (cz + PlacementRandom.Value(type.Seed, type.Salt, cx, cz, StreamClusterY)) * type.ClusterCell;
        radius = type.ClusterRadius * (0.8f + 0.4f * PlacementRandom.Value(type.Seed, type.Salt, cx, cz, StreamClusterR));
    }

    /// <summary>Biome-border rules: 0 = not allowed here, otherwise the density fade toward borders.</summary>
    private static float BorderFactor(ChunkState state, PlacementType type, float x, float z, int biomeIndex)
    {
        return BorderFactor(state.Plan, type, state.Env.BiomeGaps(x, z), biomeIndex);
    }

    private static float BorderFactor(PlacementPlan plan, PlacementType type, List<KeyValuePair<Biome, float>> gaps, int biomeIndex)
    {
        BiomeRules rules = type.Def.biomes;
        Biome own = plan.Biomes[biomeIndex];
        float nearestForeign = float.PositiveInfinity, nearestOther = float.PositiveInfinity, nearestIncompatible = float.PositiveInfinity;
        for (int i = 0; i < gaps.Count; i++)
        {
            Biome other = gaps[i].Key;
            if (other == own || other == null)
                continue;
            float distance = 0.5f * gaps[i].Value;
            nearestOther = Mathf.Min(nearestOther, distance);
            if (plan.BiomeIndex.TryGetValue(other, out int index))
            {
                if (!type.AllowedBiome[index])
                    nearestForeign = Mathf.Min(nearestForeign, distance);
                if (type.IncompatibleBiome[index])
                    nearestIncompatible = Mathf.Min(nearestIncompatible, distance);
            }
            else
            {
                nearestForeign = Mathf.Min(nearestForeign, distance);
            }
        }

        if (nearestIncompatible < rules.incompatibleDistance)
            return 0f;
        float borderDistance = Mathf.Max(0.01f, rules.borderDistance);
        if (rules.borderMode == BiomeBorderMode.AwayFromBorders && nearestOther < borderDistance)
            return 0f;
        if (rules.borderMode == BiomeBorderMode.OnlyNearBorders && nearestOther > borderDistance)
            return 0f;

        float preference = Mathf.Clamp01(type.Def.biomeCenterPreference);
        if (preference <= 0f || float.IsInfinity(nearestForeign))
            return 1f;
        return Mathf.Lerp(1f, WaterGenerator.SmoothStep01(nearestForeign / borderDistance), preference);
    }

    /// <summary>
    /// This type's possible spots in the band around the chunk that later types (and its own spacing) have to
    /// respect: everything that passes the world-consistent stages there.
    /// </summary>
    private static void CollectPotential(ChunkState state, PlacementType type)
    {
        float margin = type.Margin;
        if (margin <= 0f)
            return;
        ChunkInput input = state.Input;
        float cell = type.CandidateCell;
        int cx0 = Mathf.FloorToInt((input.OriginX - margin) / cell), cx1 = Mathf.FloorToInt((input.OriginX + input.Span + margin) / cell);
        int cy0 = Mathf.FloorToInt((input.OriginY - margin) / cell), cy1 = Mathf.FloorToInt((input.OriginY + input.Span + margin) / cell);
        SpatialHash potential = state.Potential[type.Index];
        float bound = ChanceBound(type);
        for (int cy = cy0; cy <= cy1; cy++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                // Skip the chunk's interior quickly: only the band outside it matters.
                float cellMinX = cx * cell, cellMinZ = cy * cell;
                if (cellMinX >= input.OriginX && cellMinX + cell <= input.OriginX + input.Span &&
                    cellMinZ >= input.OriginY && cellMinZ + cell <= input.OriginY + input.Span)
                    continue;

                if (PlacementRandom.Value(type.Seed, type.Salt, cx, cy, StreamChance) >= bound)
                    continue;
                Candidate c = MakeCandidate(type, cx, cy);
                if (state.Owns(c.X, c.Z))
                    continue;
                if (c.X < input.OriginX - margin || c.X >= input.OriginX + input.Span + margin || c.Z < input.OriginY - margin || c.Z >= input.OriginY + input.Span + margin)
                    continue;
                if (WorldTestOnly(state, type, ref c))
                    potential.Add(new Entry { X = c.X, Z = c.Z, Type = type.Index, Priority = c.Priority });
            }
        }
    }

    private static bool BlockedBySameType(ChunkState state, PlacementType type, Candidate c, SpatialHash validHash)
    {
        bool blocked = false;
        float priority = c.Priority;
        float x = c.X, z = c.Z;
        Func<Entry, float, bool> visit = (e, d) =>
        {
            if (e.Priority > priority || (e.Priority == priority && (e.X < x || (e.X == x && e.Z < z))))
            {
                blocked = true;
                return false;
            }
            return true;
        };
        validHash.Query(c.X, c.Z, type.HardSpacing, visit);
        if (!blocked)
            state.Potential[type.Index].Query(c.X, c.Z, type.HardSpacing, visit);
        return blocked;
    }

    /// <summary>Hard relationships against the types placed before this one (accepted here, or possible next door).</summary>
    private static bool RelationsAllow(ChunkState state, PlacementType type, float x, float z)
    {
        foreach (RelationCheck check in type.Checks)
        {
            if (check.Kind == RelationKind.CannotSpawnNear)
            {
                if (AnyNear(state, check.Others, x, z, check.Radius, true))
                    return false;
            }
            else if (check.Kind == RelationKind.RequiresNearby)
            {
                // Only what is certainly there counts: objects accepted in this chunk (a possible spot next door may
                // turn out empty). Near a chunk edge the required object must be on the same side.
                if (!AnyNear(state, check.Others, x, z, check.Radius, false))
                    return false;
            }
        }
        return true;
    }

    private static bool AnyNear(ChunkState state, int[] types, float x, float z, float radius, bool includePotential)
    {
        bool found = false;
        Func<Entry, float, bool> visit = (e, d) =>
        {
            if (Array.IndexOf(types, e.Type) >= 0)
            {
                found = true;
                return false;
            }
            return true;
        };
        state.Accepted.Query(x, z, radius, visit);
        if (!found && includePotential)
        {
            foreach (int t in types)
            {
                state.Potential[t]?.Query(x, z, radius, visit);
                if (found)
                    break;
            }
        }
        return found;
    }

    /// <summary>
    /// Soft relationships (Avoids / Attracted To): a chance multiplier from the types placed before this one
    /// (0..<see cref="PlacementType.MaxBoost"/>), by proximity.
    /// </summary>
    private static float RelationFactor(ChunkState state, PlacementType type, float x, float z)
    {
        float factor = 1f;
        foreach (RelationCheck check in type.Checks)
        {
            if (check.Kind != RelationKind.Avoids && check.Kind != RelationKind.AttractedTo)
                continue;
            float closeness = 0f;
            int[] others = check.Others;
            float radius = check.Radius;
            Func<Entry, float, bool> visit = (e, d) =>
            {
                if (Array.IndexOf(others, e.Type) >= 0)
                    closeness = Mathf.Max(closeness, 1f - d / radius);
                return true;
            };
            state.Accepted.Query(x, z, radius, visit);
            foreach (int t in others)
                state.Potential[t]?.Query(x, z, radius, visit);

            if (check.Kind == RelationKind.Avoids)
                factor *= 1f - Mathf.Clamp01(check.Strength) * closeness;
            else
                factor *= 1f + PlacementPlan.MaxAttraction * Mathf.Clamp01(check.Strength) * closeness;
        }
        return factor;
    }

    private static bool SoftSpacingAllows(ChunkState state, PlacementType type, Candidate c, SpatialHash acceptedHere, int stream)
    {
        SpacingRules spacing = type.Def.spacing;
        if (spacing.softSpacing <= type.HardSpacing || spacing.softSpacingStrength <= 0f)
            return true;
        float keep = 1f;
        float range = spacing.softSpacing;
        Func<Entry, float, bool> visit = (e, d) =>
        {
            float t = Mathf.Clamp01((range - d) / Mathf.Max(0.01f, range - type.HardSpacing));
            keep *= 1f - spacing.softSpacingStrength * t;
            return true;
        };
        acceptedHere.Query(c.X, c.Z, range, visit);
        state.Potential[type.Index].Query(c.X, c.Z, range, visit);
        return Rand(type, c, stream) < keep;
    }

    /// <summary>Max Cluster Size and Max Per Region (scatter): only the highest-priority possible spots of a cluster/region are eligible.</summary>
    private static bool WithinRankLimits(ChunkState state, PlacementType type, Candidate c)
    {
        int maxCluster = type.Def.clustering.maxClusterSize;
        if (type.Clustered && maxCluster > 0 && c.InCluster)
        {
            if (c.Priority < RankThreshold(state, type, true, c.ClusterX, c.ClusterZ, maxCluster))
                return false;
        }

        int maxRegion = type.Def.limits.maxPerRegion;
        if (maxRegion > 0)
        {
            float size = Mathf.Max(type.CandidateCell, type.Def.limits.regionSize);
            if (c.Priority < RankThreshold(state, type, false, Mathf.FloorToInt(c.X / size), Mathf.FloorToInt(c.Z / size), maxRegion))
                return false;
        }
        return true;
    }

    /// <summary>
    /// The lowest priority still among the <paramref name="limit"/> highest of all possible spots (world-consistent
    /// stages only) in a cluster or region - so every chunk ranks a cluster or region the same way.
    /// </summary>
    private static float RankThreshold(ChunkState state, PlacementType type, bool cluster, int keyX, int keyZ, int limit)
    {
        var key = new RankKey { Type = type.Index, X = keyX, Z = keyZ, Cluster = cluster };
        if (state.RankThresholds.TryGetValue(key, out float cached))
            return cached;

        float minX, minZ, maxX, maxZ;
        if (cluster)
        {
            ClusterCentre(type, keyX, keyZ, out float centreX, out float centreZ, out float radius);
            minX = centreX - radius; maxX = centreX + radius;
            minZ = centreZ - radius; maxZ = centreZ + radius;
        }
        else
        {
            float size = Mathf.Max(type.CandidateCell, type.Def.limits.regionSize);
            minX = keyX * size; maxX = minX + size;
            minZ = keyZ * size; maxZ = minZ + size;
        }

        var priorities = new List<float>();
        float cell = type.CandidateCell;
        float bound = ChanceBound(type);
        int x0 = Mathf.FloorToInt(minX / cell), x1 = Mathf.FloorToInt(maxX / cell);
        int z0 = Mathf.FloorToInt(minZ / cell), z1 = Mathf.FloorToInt(maxZ / cell);
        for (int cz = z0; cz <= z1; cz++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                if (PlacementRandom.Value(type.Seed, type.Salt, cx, cz, StreamChance) >= bound)
                    continue;
                Candidate c = MakeCandidate(type, cx, cz);
                if (c.X < minX || c.X >= maxX || c.Z < minZ || c.Z >= maxZ)
                    continue;
                if (!WorldTestOnly(state, type, ref c))
                    continue;
                if (cluster && (!c.InCluster || c.ClusterX != keyX || c.ClusterZ != keyZ))
                    continue;
                priorities.Add(c.Priority);
            }
        }

        float threshold = 0f;
        if (priorities.Count > limit)
        {
            priorities.Sort((a, b) => b.CompareTo(a));
            threshold = priorities[limit - 1];
        }
        state.RankThresholds[key] = threshold;
        return threshold;
    }

    private static void Accept(ChunkState state, PlacementType type, Candidate c, SpatialHash acceptedHere)
    {
        var entry = new Entry { X = c.X, Z = c.Z, Type = type.Index, Priority = c.Priority };
        state.Accepted.Add(entry);
        acceptedHere?.Add(entry);
        state.AcceptedCount[type.Index]++;
        state.Result.Accepted[type.Index]++;

        Vector3 rootScale = type.Shape.RootScale;
        state.Result.Objects.Add(new ObjectPlacement
        {
            Type = type.Index,
            Position = c.Position,
            Rotation = c.Rotation,
            Scale = rootScale * c.Scale,
            Radius = type.SizeRadius * c.Scale,
            ExpectedMinY = c.ExpectedMinY,
        });
    }

    /// <summary>
    /// Growth: around each accepted copy (in priority order), the spots that failed their chance rolls get another
    /// chance, raised by Growth Per Neighbour for each accepted copy within Growth Radius - forests fill in and
    /// rock fields spread. Still subject to every rule, spacing and limit; skipped near the chunk edge.
    /// </summary>
    private static void Grow(ChunkState state, PlacementType type, List<Candidate> seeds, SpatialHash acceptedHere)
    {
        ClusterRules rules = type.Def.clustering;
        float radius = Mathf.Min(PlacementPlan.MaxMargin, rules.growthRadius);
        float cell = type.CandidateCell;
        float edgeBand = Mathf.Max(type.HardSpacing, type.Def.spacing.softSpacing);
        var visited = new HashSet<long>();
        foreach (Candidate seed in seeds)
            visited.Add(((long)seed.CellX << 32) ^ (uint)seed.CellY);

        for (int s = 0; s < seeds.Count; s++)
        {
            Candidate seed = seeds[s];
            int cx0 = Mathf.FloorToInt((seed.X - radius) / cell), cx1 = Mathf.FloorToInt((seed.X + radius) / cell);
            int cz0 = Mathf.FloorToInt((seed.Z - radius) / cell), cz1 = Mathf.FloorToInt((seed.Z + radius) / cell);
            for (int cz = cz0; cz <= cz1; cz++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    if (state.AcceptedCount[type.Index] >= type.MaxPerChunk)
                        return;
                    if (!visited.Add(((long)cx << 32) ^ (uint)cz))
                        continue;
                    Candidate c = MakeCandidate(type, cx, cz);
                    if (!state.Owns(c.X, c.Z) || state.EdgeDistance(c.X, c.Z) < edgeBand)
                        continue;

                    int neighbours = 0;
                    acceptedHere.Query(c.X, c.Z, radius, (e, d) => { neighbours++; return true; });
                    if (neighbours == 0)
                        continue;
                    WorldTest(state, type, ref c, true, out bool eligible);
                    if (!eligible)
                        continue;
                    float upper = Mathf.Min(1f, c.RawChance * rules.growthPerNeighbour * neighbours);
                    if (Rand(type, c, StreamGrowth) >= upper)
                        continue;
                    if (!EvaluateSite(state, type, ref c))
                        continue;
                    // The roll above assumed ideal ground; scale it by the spot's actual preferences.
                    if (Rand(type, c, StreamSoft) >= c.Soft * RelationFactor(state, type, c.X, c.Z))
                        continue;

                    bool tooClose = false;
                    acceptedHere.Query(c.X, c.Z, type.HardSpacing, (e, d) => { tooClose = true; return false; });
                    if (tooClose || !RelationsAllow(state, type, c.X, c.Z) || !WithinRankLimits(state, type, c))
                        continue;
                    Accept(state, type, c, acceptedHere);
                }
            }
        }
    }
}
