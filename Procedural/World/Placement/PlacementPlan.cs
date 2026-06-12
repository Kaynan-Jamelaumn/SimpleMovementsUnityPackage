using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One placeable object type, compiled from a <see cref="BiomeObject"/> for the placement engine: resolved
/// biome sets, footprint, spacing, relationships to the types placed before it. Read-only once built.
/// </summary>
public sealed class PlacementType
{
    public int Index;
    public BiomeObject Def;
    /// <summary>The prefab (only touched on the main thread, when spawning).</summary>
    public GameObject Prefab;
    public string Tag;
    public int Seed;
    public int Salt;
    public bool Landmark;
    public bool[] AllowedBiome;
    public bool[] IncompatibleBiome;
    public bool UsesBorders;
    public PrefabShape Shape;
    /// <summary>Footprint box (what rests on the ground) in the prefab root's space, before its scale; false = a circle of <see cref="Radius"/> around the pivot.</summary>
    public bool HasBox;
    public Vector3 BoxMin, BoxMax;
    /// <summary>Footprint radius on the ground at placement scale 1 (world units).</summary>
    public float Radius;
    /// <summary>Radius of the whole object seen from above at placement scale 1 (world units): for spacing.</summary>
    public float SizeRadius;
    public float CandidateCell;
    public float HardSpacing;
    public float ChancePerSquareUnit;
    /// <summary>How much relationships can raise this type's chance (its first chance roll allows for it).</summary>
    public float MaxBoost = 1f;
    public bool Clustered;
    public float ClusterCell, ClusterRadius, InsideDensity, OutsideDensity;
    public float MinSlope, MaxSlope;
    public int MaxPerChunk;
    /// <summary>How far beyond a chunk this type's possible spots matter to the types checking against it (world units).</summary>
    public float Margin;
    public readonly List<RelationCheck> Checks = new List<RelationCheck>();
    /// <summary>Hard exclusions that later types enforce against this one are recorded on those types; this lists warnings for the editor.</summary>
    public readonly List<string> Notes = new List<string>();
}

/// <summary>A relationship a type checks when it is placed, against types placed before it.</summary>
public struct RelationCheck
{
    public RelationKind Kind;
    public int[] Others;
    public float Radius;
    public float Strength;
}

/// <summary>
/// Everything object placement needs about the world and the object types, compiled on the main thread (it
/// reads prefab names and measures prefabs) and then shared read-only by the worker threads that place each
/// chunk's objects. Types are ordered so that a type is placed after the types it has to check against.
/// </summary>
public sealed class PlacementPlan
{
    /// <summary>Largest distance (world units) a relationship or spacing can reach across a chunk border.</summary>
    public const float MaxMargin = 64f;
    // How much an "Attracted To" relationship can raise a chance, at Strength 1.
    public const float MaxAttraction = 3f;

    public int WorldSeed;
    public PlacementType[] Types;
    public List<Biome> Biomes = new List<Biome>();
    public Dictionary<Biome, int> BiomeIndex = new Dictionary<Biome, int>();
    public TerrainHeightSampler Sampler;
    public WaterSettings Water;
    public TerrainClimate Climate;
    public float ClimateNoiseScale;
    public int VoronoiSeed;
    public float SeaLevel;
    public int ChunkSpan;
    public int LodFactor;
    public float CliffAngle = 45f;
    public List<string> Warnings = new List<string>();

    public bool IsEmpty => Types == null || Types.Length == 0;

    /// <summary>
    /// Compiles the placement plan for a generator's biome object lists (main thread only).
    /// <paramref name="shapes"/> measures a prefab (normally <see cref="PrefabShapeCache.Get"/>).
    /// </summary>
    public static PlacementPlan Build(TerrainGenerator tg, Func<GameObject, PrefabShape> shapes)
    {
        var plan = new PlacementPlan
        {
            WorldSeed = tg.VoronoiSeed,
            VoronoiSeed = tg.VoronoiSeed,
            Water = tg.EnableWater ? WaterSettings.From(tg) : null,
            ClimateNoiseScale = tg.ClimateNoiseScale,
            SeaLevel = tg.SeaLevel,
            ChunkSpan = tg.ChunkSize - 1,
            LodFactor = tg.LevelOfDetail > 0 ? tg.LevelOfDetail * 2 : 1,
            CliffAngle = tg.ObjectCliffAngle,
        };
        plan.Sampler = new TerrainHeightSampler(tg, plan.Water);
        plan.Climate = tg.TerrainClimate;

        var types = new List<PlacementType>();
        var seenKeys = new Dictionary<string, int>();
        BiomeInstance[] definitions = tg.BiomeDefinitions ?? new BiomeInstance[0];
        foreach (BiomeInstance instance in definitions)
        {
            if (instance?.BiomePrefab != null && !plan.BiomeIndex.ContainsKey(instance.BiomePrefab))
            {
                plan.BiomeIndex[instance.BiomePrefab] = plan.Biomes.Count;
                plan.Biomes.Add(instance.BiomePrefab);
            }
        }

        if (!tg.ShouldSpawnObjects)
        {
            plan.Types = new PlacementType[0];
            return plan;
        }

        foreach (BiomeInstance instance in definitions)
        {
            if (instance?.BiomePrefab == null || instance.runtimeObjects == null)
                continue;
            foreach (BiomeObject def in instance.runtimeObjects)
            {
                if (def == null || def.terrainObject == null || (def.probabilityToSpawn <= 0f && !(def.limits.mode == PlacementMode.Landmark && (def.limits.guaranteed || def.limits.unique))))
                    continue;
                types.Add(Compile(plan, instance.BiomePrefab, def, shapes, seenKeys));
            }
        }

        ResolveRelationsAndOrder(plan, types);
        return plan;
    }

    private static PlacementType Compile(PlacementPlan plan, Biome owner, BiomeObject def, Func<GameObject, PrefabShape> shapes, Dictionary<string, int> seenKeys)
    {
        string prefabName = def.terrainObject.name;
        var type = new PlacementType
        {
            Def = def,
            Prefab = def.terrainObject,
            Tag = !string.IsNullOrEmpty(def.groupTag) ? def.groupTag : prefabName,
            Landmark = def.limits.mode == PlacementMode.Landmark,
        };

        // A stable identity for this entry's random numbers: its biome and prefab (and which copy, when the
        // same prefab is listed twice in one biome) - not its position in the lists, so reordering keeps the world.
        string key = (owner.name ?? "") + "/" + prefabName;
        seenKeys.TryGetValue(key, out int copies);
        seenKeys[key] = copies + 1;
        type.Salt = PlacementRandom.StableHash(key + "#" + copies) ^ def.seed.seedOffset * 0x3C6EF372;
        type.Seed = def.seed.useWorldSeed ? plan.WorldSeed : def.seed.fixedSeed;

        int biomeCount = plan.Biomes.Count;
        type.AllowedBiome = new bool[biomeCount];
        type.IncompatibleBiome = new bool[biomeCount];
        Mark(plan, type.AllowedBiome, owner);
        foreach (Biome b in def.biomes.alsoAllowedIn)
            Mark(plan, type.AllowedBiome, b);
        foreach (Biome b in def.biomes.forbiddenIn)
            if (b != null && plan.BiomeIndex.TryGetValue(b, out int f))
                type.AllowedBiome[f] = false;
        foreach (Biome b in def.biomes.incompatibleNeighbours)
            Mark(plan, type.IncompatibleBiome, b);
        type.UsesBorders = def.biomeCenterPreference > 0f || def.biomes.borderMode != BiomeBorderMode.Anywhere || def.biomes.incompatibleNeighbours.Count > 0;

        // Footprint (what rests on the ground) and size (the whole object, for spacing).
        type.Shape = shapes != null ? shapes(def.terrainObject) : PrefabShape.Unknown();
        GroundContactRules ground = def.ground;
        Vector3 rootScale = type.Shape.RootScale;
        if (ground.footprint != FootprintSource.CustomRadius && type.Shape.TryGetBox(ground.footprint, out Vector3 min, out Vector3 max))
        {
            // Footprint Scale shrinks the box around its centre (not its height).
            float k = Mathf.Clamp(ground.footprintScale, 0.05f, 1f);
            Vector3 mid = (min + max) * 0.5f, half = (max - min) * 0.5f;
            type.HasBox = true;
            type.BoxMin = new Vector3(mid.x - half.x * k, min.y, mid.z - half.z * k);
            type.BoxMax = new Vector3(mid.x + half.x * k, max.y, mid.z + half.z * k);
            type.Radius = BoxRadius(type.BoxMin, type.BoxMax, rootScale);
        }
        else
        {
            type.Radius = ground.footprint == FootprintSource.CustomRadius ? Mathf.Max(0.05f, ground.customRadius) : 0.5f;
        }
        type.SizeRadius = type.Shape.TryGetSizeBox(out Vector3 sizeMin, out Vector3 sizeMax) ? Mathf.Max(type.Radius, BoxRadius(sizeMin, sizeMax, rootScale)) : type.Radius;
        float maxScale = Mathf.Max(0.01f, Mathf.Max(def.scaleRange.x, def.scaleRange.y));

        // Spacing and candidate density.
        type.HardSpacing = def.minSpacing > 0f ? def.minSpacing : Mathf.Max(0.5f, 1.8f * type.SizeRadius * maxScale);
        type.CandidateCell = Mathf.Max(0.5f, type.HardSpacing * 0.7f);
        type.ChancePerSquareUnit = Mathf.Max(0f, def.probabilityToSpawn) * 0.01f;
        type.MaxPerChunk = def.hasMaxNumberOfObjects ? Mathf.Max(0, def.maxNumberOfThisObject) : int.MaxValue;

        // Slope.
        type.MaxSlope = Mathf.Clamp(def.slopeThreshold * def.slopeAvoidance, 0f, 90f);
        type.MinSlope = Mathf.Clamp(def.slope.minSlope, 0f, type.MaxSlope);

        // Clusters: about clusterCount per chunk area.
        type.Clustered = def.isClusterable && def.clusterCount > 0 && def.clusterRadius > 0f;
        if (type.Clustered)
        {
            type.ClusterCell = Mathf.Max(def.clusterRadius, plan.ChunkSpan / Mathf.Sqrt(def.clusterCount));
            type.ClusterRadius = def.clusterRadius;
            type.InsideDensity = Mathf.Max(0f, def.clustering.insideDensity);
            if (def.clustering.desiredClusterSize > 0 && type.ChancePerSquareUnit > 0f)
            {
                // Expected objects in a cluster ~ chance x inside density x (area x 1/3: the falloff's average).
                float area = Mathf.PI * def.clusterRadius * def.clusterRadius / 3f;
                type.InsideDensity = def.clustering.desiredClusterSize / Mathf.Max(1e-6f, type.ChancePerSquareUnit * area);
            }
            type.OutsideDensity = Mathf.Clamp01(def.clustering.outsideDensity);
        }

        // Relationship reach of this type's own rules (for the margin of its possible spots across borders).
        type.Margin = Mathf.Min(MaxMargin, Mathf.Max(type.HardSpacing, Mathf.Max(def.spacing.softSpacing, def.clustering.growthRadius)));
        return type;
    }

    /// <summary>Distance from the pivot to the farthest corner of a box seen from above (world units at scale 1).</summary>
    private static float BoxRadius(Vector3 min, Vector3 max, Vector3 rootScale)
    {
        float halfX = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)) * Mathf.Abs(rootScale.x);
        float halfZ = Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z)) * Mathf.Abs(rootScale.z);
        return Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
    }

    private static void Mark(PlacementPlan plan, bool[] set, Biome biome)
    {
        if (biome != null && plan.BiomeIndex.TryGetValue(biome, out int index))
            set[index] = true;
    }

    /// <summary>
    /// Orders the types so each is placed after the types its relationships check (and before the types it
    /// prevents), then records each relationship on the type that enforces it.
    /// </summary>
    private static void ResolveRelationsAndOrder(PlacementPlan plan, List<PlacementType> types)
    {
        var byTag = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < types.Count; i++)
        {
            if (!byTag.TryGetValue(types[i].Tag, out var list))
                byTag[types[i].Tag] = list = new List<int>();
            list.Add(i);
        }

        // Edges: "before -> after".
        var after = new List<int>[types.Count];
        var incoming = new int[types.Count];
        for (int i = 0; i < types.Count; i++)
            after[i] = new List<int>();
        for (int i = 0; i < types.Count; i++)
        {
            foreach (ObjectRelation rule in types[i].Def.relations)
            {
                if (rule == null || string.IsNullOrEmpty(rule.otherTag) || rule.radius <= 0f)
                    continue;
                if (!byTag.TryGetValue(rule.otherTag, out var others))
                {
                    plan.Warnings.Add($"{types[i].Tag}: no object has the tag '{rule.otherTag}' - that relationship does nothing.");
                    continue;
                }
                foreach (int o in others)
                {
                    if (o == i)
                        continue;
                    int before = rule.kind == RelationKind.PreventsNearby ? i : o;
                    int later = rule.kind == RelationKind.PreventsNearby ? o : i;
                    if (!after[before].Contains(later))
                    {
                        after[before].Add(later);
                        incoming[later]++;
                    }
                }
            }
        }

        // Kahn's topological order; among the ready types (and to break cycles): landmarks first, then higher
        // Placement Priority, then larger footprints, then list order.
        var order = new List<int>(types.Count);
        var done = new bool[types.Count];
        Comparison<int> better = (a, b) =>
        {
            if (types[a].Landmark != types[b].Landmark) return types[a].Landmark ? -1 : 1;
            int p = types[b].Def.placementPriority.CompareTo(types[a].Def.placementPriority);
            if (p != 0) return p;
            int r = types[b].SizeRadius.CompareTo(types[a].SizeRadius);
            return r != 0 ? r : a.CompareTo(b);
        };
        while (order.Count < types.Count)
        {
            int pick = -1;
            for (int i = 0; i < types.Count; i++)
                if (!done[i] && incoming[i] == 0 && (pick < 0 || better(i, pick) < 0))
                    pick = i;
            if (pick < 0)
            {
                // A cycle: take the best remaining type anyway.
                for (int i = 0; i < types.Count; i++)
                    if (!done[i] && (pick < 0 || better(i, pick) < 0))
                        pick = i;
                plan.Warnings.Add($"Relationships around '{types[pick].Tag}' form a loop; some of them can only be enforced one way.");
            }
            done[pick] = true;
            order.Add(pick);
            foreach (int next in after[pick])
                incoming[next]--;
        }

        var position = new int[types.Count];
        for (int k = 0; k < order.Count; k++)
            position[order[k]] = k;

        // Record each rule on the type placed later (which checks against the earlier one).
        for (int i = 0; i < types.Count; i++)
        {
            foreach (ObjectRelation rule in types[i].Def.relations)
            {
                if (rule == null || string.IsNullOrEmpty(rule.otherTag) || rule.radius <= 0f || !byTag.TryGetValue(rule.otherTag, out var others))
                    continue;
                float radius = Mathf.Min(rule.radius, MaxMargin);
                foreach (int o in others)
                {
                    if (o == i)
                    {
                        // A rule against its own tag is spacing between copies.
                        if (rule.kind == RelationKind.CannotSpawnNear || rule.kind == RelationKind.PreventsNearby)
                            types[i].HardSpacing = Mathf.Max(types[i].HardSpacing, radius);
                        continue;
                    }

                    bool subjectLater = position[i] > position[o];
                    switch (rule.kind)
                    {
                        case RelationKind.CannotSpawnNear:
                        case RelationKind.PreventsNearby:
                            // Symmetric in effect: whichever is placed later stays out of the other's radius.
                            AddCheck(types, subjectLater ? i : o, subjectLater ? o : i, RelationKind.CannotSpawnNear, radius, 1f);
                            break;
                        case RelationKind.Avoids:
                            if (subjectLater) AddCheck(types, i, o, RelationKind.Avoids, radius, rule.strength);
                            else AddCheck(types, o, i, RelationKind.Avoids, radius, rule.strength);
                            break;
                        case RelationKind.AttractedTo:
                        case RelationKind.RequiresNearby:
                            if (subjectLater)
                                AddCheck(types, i, o, rule.kind, radius, rule.strength);
                            else
                                types[i].Notes.Add($"'{rule.otherTag}' is placed after '{types[i].Tag}' (their relationships form a loop), so '{rule.kind}' can't be applied.");
                            break;
                    }
                }
            }
        }

        foreach (PlacementType t in types)
        {
            foreach (RelationCheck check in t.Checks)
            {
                if (check.Kind == RelationKind.AttractedTo)
                    t.MaxBoost += MaxAttraction * check.Strength;
                foreach (int o in check.Others)
                    types[o].Margin = Mathf.Min(MaxMargin, Mathf.Max(types[o].Margin, check.Radius));
            }
            t.Margin = Mathf.Min(MaxMargin, Mathf.Max(t.Margin, t.HardSpacing));
        }

        plan.Types = new PlacementType[types.Count];
        for (int k = 0; k < order.Count; k++)
        {
            plan.Types[k] = types[order[k]];
            plan.Types[k].Index = k;
        }
        // Checks refer to list indices; remap them to plan order.
        foreach (PlacementType t in plan.Types)
        {
            for (int c = 0; c < t.Checks.Count; c++)
            {
                RelationCheck check = t.Checks[c];
                var remapped = new int[check.Others.Length];
                for (int k = 0; k < remapped.Length; k++)
                    remapped[k] = position[check.Others[k]];
                check.Others = remapped;
                t.Checks[c] = check;
            }
        }
    }

    private static void AddCheck(List<PlacementType> types, int checker, int other, RelationKind kind, float radius, float strength)
    {
        List<RelationCheck> checks = types[checker].Checks;
        for (int c = 0; c < checks.Count; c++)
        {
            if (checks[c].Kind == kind && Mathf.Approximately(checks[c].Radius, radius) && Mathf.Approximately(checks[c].Strength, strength))
            {
                var merged = new int[checks[c].Others.Length + 1];
                Array.Copy(checks[c].Others, merged, checks[c].Others.Length);
                merged[merged.Length - 1] = other;
                RelationCheck updated = checks[c];
                updated.Others = merged;
                checks[c] = updated;
                return;
            }
        }
        checks.Add(new RelationCheck { Kind = kind, Others = new[] { other }, Radius = radius, Strength = strength });
    }
}
