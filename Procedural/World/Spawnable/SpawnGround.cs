using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Why a spawn spot was rejected (see <see cref="SpawnGround.Check"/>).</summary>
public enum SpawnRejection
{
    None,
    /// <summary>No loaded chunk data there.</summary>
    NoTerrain,
    Slope,
    Water,
    Biome,
    Height,
    /// <summary>Too close to a placed object (tree, rock...).</summary>
    Objects,
    /// <summary>No NavMesh within reach (or not the spawned agent's type).</summary>
    NavMesh,
}

/// <summary>
/// Checks a spawn spot against the loaded terrain - the same data the chunk meshes and colliders were built from
/// (<see cref="LoadedTerrain"/>): ground height, slope, water, biome, height band, distance to placed objects, and
/// optionally the NavMesh. Shared by the mob and portal spawners so both judge the ground the same way the object
/// placement does. Main thread only.
/// </summary>
public static class SpawnGround
{
    /// <summary>What a spot must satisfy.</summary>
    public struct Rules
    {
        /// <summary>Radius of what is spawned (world units) - for the slope sample and the object clearance.</summary>
        public float Radius;
        /// <summary>Steepest ground allowed (degrees).</summary>
        public float MaxSlope;
        public bool AllowWater;
        /// <summary>Free space kept around placed objects (world units); negative = ignore objects.</summary>
        public float ObjectClearance;
        /// <summary>Only in these biomes (null or empty = any).</summary>
        public IList<Biome> AllowedBiomes;
        /// <summary>Never in these biomes.</summary>
        public IList<Biome> ForbiddenBiomes;
        public bool LimitHeight;
        public float MinHeight, MaxHeight;
        /// <summary>Must be on the NavMesh of <see cref="AgentTypeId"/> (within <see cref="NavMeshDistance"/>); the spot snaps onto it.</summary>
        public bool RequireNavMesh;
        public float NavMeshDistance;
        public int AgentTypeId;
        public int AreaMask;
    }

    /// <summary>
    /// Checks the spot at world (x, z). On success <paramref name="position"/> is on the ground (on the NavMesh
    /// when it is required) and <paramref name="biome"/> is the biome there.
    /// </summary>
    public static SpawnRejection Check(float x, float z, in Rules rules, out Vector3 position, out Biome biome)
    {
        position = default;
        biome = null;
        if (!LoadedTerrain.TryGetHeight(x, z, out float height))
            return SpawnRejection.NoTerrain;

        if (rules.MaxSlope < 90f && LoadedTerrain.TryGetNormal(x, z, Mathf.Max(1f, rules.Radius), out Vector3 normal))
        {
            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope > rules.MaxSlope)
                return SpawnRejection.Slope;
        }

        if (!rules.AllowWater && LoadedTerrain.WaterAt(x, z, out _) != WaterBodyType.None)
            return SpawnRejection.Water;

        biome = LoadedTerrain.BiomeAt(x, z);
        if (rules.AllowedBiomes != null && rules.AllowedBiomes.Count > 0 && (biome == null || !rules.AllowedBiomes.Contains(biome)))
            return SpawnRejection.Biome;
        if (rules.ForbiddenBiomes != null && biome != null && rules.ForbiddenBiomes.Contains(biome))
            return SpawnRejection.Biome;

        if (rules.LimitHeight && (height < rules.MinHeight || height > rules.MaxHeight))
            return SpawnRejection.Height;

        if (rules.ObjectClearance >= 0f && IsNearObject(x, z, Mathf.Max(0f, rules.Radius) + rules.ObjectClearance))
            return SpawnRejection.Objects;

        position = new Vector3(x, height, z);
        if (rules.RequireNavMesh)
        {
            float reach = Mathf.Max(0.25f, rules.NavMeshDistance);
            var filter = new NavMeshQueryFilter { agentTypeID = rules.AgentTypeId, areaMask = rules.AreaMask == 0 ? NavMesh.AllAreas : rules.AreaMask };
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, reach, filter))
                return SpawnRejection.NavMesh;
            position = hit.position;
        }
        return SpawnRejection.None;
    }

    private static bool nearFound;
    private static readonly System.Action<Vector3, float> MarkFound = (p, r) => nearFound = true;

    /// <summary>True when a placed object's footprint is within <paramref name="radius"/> of (x, z).</summary>
    public static bool IsNearObject(float x, float z, float radius)
    {
        nearFound = false;
        LoadedTerrain.ForEachObjectNear(x, z, radius, MarkFound);
        return nearFound;
    }

    /// <summary>
    /// The radius of a prefab's footprint around its pivot (world units, ignoring height), from the same
    /// measurements the object placement uses (<see cref="PrefabShapeCache"/>: renderers, else colliders).
    /// </summary>
    public static float FootprintRadius(GameObject prefab, float fallback = 1f)
    {
        if (prefab == null)
            return fallback;
        PrefabShape shape = PrefabShapeCache.Get(prefab);
        if (!shape.TryGetSizeBox(out Vector3 min, out Vector3 max))
            return fallback;
        float x = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)) * Mathf.Abs(shape.RootScale.x);
        float z = Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z)) * Mathf.Abs(shape.RootScale.z);
        float radius = Mathf.Sqrt(x * x + z * z);
        return radius > 0.01f ? radius : fallback;
    }

    /// <summary>
    /// Moves a freshly spawned instance so the bottom of its renderers (else colliders) touches
    /// <paramref name="groundY"/>, then down by <paramref name="sink"/> - right whatever the prefab's pivot.
    /// </summary>
    public static void SitOnGround(GameObject instance, float groundY, float sink)
    {
        if (instance == null)
            return;
        bool any = false;
        float bottom = float.PositiveInfinity;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;
            bottom = Mathf.Min(bottom, r.bounds.min.y);
            any = true;
        }
        if (!any)
        {
            foreach (Collider c in instance.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger)
                    continue;
                bottom = Mathf.Min(bottom, c.bounds.min.y);
                any = true;
            }
        }
        if (any)
            instance.transform.position += Vector3.up * (groundY - bottom - sink);
    }
}
