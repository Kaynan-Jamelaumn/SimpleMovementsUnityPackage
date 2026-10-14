using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Physics queries that work when some scenes have their own physics simulation (a dungeon scene created with
/// <see cref="LocalPhysicsMode.Physics3D"/>): Unity's static <see cref="Physics"/> queries only see the default physics
/// scene, so a raycast inside such a dungeon would find nothing.
/// <para>
/// Scenes with their own physics register the world-space region they occupy (<see cref="RegisterRegion"/>). Every
/// query here is routed by its start position to the physics scene of the region containing it, or to the default one.
/// The methods take the same arguments as their <see cref="Physics"/> counterparts, so a call site only changes its
/// class name. With no region registered they call <see cref="Physics"/> directly: nothing changes for projects that
/// do not use separate physics scenes.
/// </para>
/// </summary>
/// <remarks>
/// Regions must not overlap each other or any place used by the default physics scene - dungeon scenes get their own
/// slots far from the world for that reason (see ProceduralDungeon.DungeonScenes).
/// </remarks>
public static class ScenePhysics
{
    private struct Region
    {
        public Scene scene;
        public PhysicsScene physics;
        public Bounds bounds;
    }

    private static readonly List<Region> regions = new List<Region>(4);
    private static Collider[] colliderBuffer = new Collider[64];
    private static RaycastHit[] hitBuffer = new RaycastHit[32];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => regions.Clear();

    /// <summary>Number of registered regions (0 = every query uses the default physics scene).</summary>
    public static int RegionCount => regions.Count;

    /// <summary>
    /// Routes queries starting inside <paramref name="bounds"/> to the physics scene of <paramref name="scene"/>.
    /// Registering the same scene again replaces its region.
    /// </summary>
    public static void RegisterRegion(Scene scene, Bounds bounds)
    {
        if (!scene.IsValid())
            return;
        UnregisterRegion(scene);
        regions.Add(new Region { scene = scene, physics = scene.GetPhysicsScene(), bounds = bounds });
    }

    public static void UnregisterRegion(Scene scene)
    {
        for (int i = regions.Count - 1; i >= 0; i--)
            if (regions[i].scene == scene)
                regions.RemoveAt(i);
    }

    /// <summary>The scene whose region contains <paramref name="position"/>, if any.</summary>
    public static bool TryGetRegionScene(Vector3 position, out Scene scene)
    {
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].bounds.Contains(position))
            {
                scene = regions[i].scene;
                return true;
            }
        }
        scene = default;
        return false;
    }

    /// <summary>The physics scene that owns <paramref name="position"/> (the default one outside every region).</summary>
    public static PhysicsScene At(Vector3 position)
    {
        for (int i = 0; i < regions.Count; i++)
            if (regions[i].bounds.Contains(position))
                return regions[i].physics;
        return Physics.defaultPhysicsScene;
    }

    /// <summary>The physics scene <paramref name="go"/> simulates in.</summary>
    public static PhysicsScene Of(GameObject go) => go != null ? go.scene.GetPhysicsScene() : Physics.defaultPhysicsScene;

    /// <summary>True when queries at <paramref name="position"/> go to the default physics scene.</summary>
    private static bool UsesDefault(Vector3 position, out PhysicsScene scene)
    {
        if (regions.Count == 0)
        {
            scene = default;
            return true;
        }
        scene = At(position);
        return scene == Physics.defaultPhysicsScene;
    }

    // ------------------------------------------------------------------ rays

    public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(origin, out PhysicsScene s))
            return Physics.Raycast(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
        return s.Raycast(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity,
        int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(origin, out PhysicsScene s))
            return Physics.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        return s.Raycast(origin, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) =>
        Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);

    public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance = Mathf.Infinity,
        int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(origin, out PhysicsScene s))
            return Physics.RaycastNonAlloc(origin, direction, results, maxDistance, layerMask, queryTriggerInteraction);
        return s.Raycast(origin, direction, results, maxDistance, layerMask, queryTriggerInteraction);
    }

    public static int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal) =>
        RaycastNonAlloc(ray.origin, ray.direction, results, maxDistance, layerMask, queryTriggerInteraction);

    /// <summary>Every hit along the ray (allocates, like <see cref="Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)"/>).</summary>
    public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(origin, out PhysicsScene s))
            return Physics.RaycastAll(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
        int n;
        while ((n = s.Raycast(origin, direction, hitBuffer, maxDistance, layerMask, queryTriggerInteraction)) == hitBuffer.Length)
            hitBuffer = new RaycastHit[hitBuffer.Length * 2];
        var result = new RaycastHit[n];
        System.Array.Copy(hitBuffer, result, n);
        return result;
    }

    public static int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, RaycastHit[] results, float maxDistance = Mathf.Infinity,
        int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(origin, out PhysicsScene s))
            return Physics.SphereCastNonAlloc(origin, radius, direction, results, maxDistance, layerMask, queryTriggerInteraction);
        return s.SphereCast(origin, radius, direction, results, maxDistance, layerMask, queryTriggerInteraction);
    }

    // ------------------------------------------------------------------ overlaps

    public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask = Physics.AllLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(position, out PhysicsScene s))
            return Physics.OverlapSphereNonAlloc(position, radius, results, layerMask, queryTriggerInteraction);
        return s.OverlapSphere(position, radius, results, layerMask, queryTriggerInteraction);
    }

    public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation,
        int layerMask = Physics.AllLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(center, out PhysicsScene s))
            return Physics.OverlapBoxNonAlloc(center, halfExtents, results, orientation, layerMask, queryTriggerInteraction);
        return s.OverlapBox(center, halfExtents, results, orientation, layerMask, queryTriggerInteraction);
    }

    public static int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, Collider[] results, int layerMask = Physics.AllLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(point0, out PhysicsScene s))
            return Physics.OverlapCapsuleNonAlloc(point0, point1, radius, results, layerMask, queryTriggerInteraction);
        return s.OverlapCapsule(point0, point1, radius, results, layerMask, queryTriggerInteraction);
    }

    /// <summary>Every collider in the sphere (allocates, like <see cref="Physics.OverlapSphere(Vector3, float, int, QueryTriggerInteraction)"/>).</summary>
    public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask = Physics.AllLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(position, out PhysicsScene s))
            return Physics.OverlapSphere(position, radius, layerMask, queryTriggerInteraction);
        int n;
        while ((n = s.OverlapSphere(position, radius, colliderBuffer, layerMask, queryTriggerInteraction)) == colliderBuffer.Length)
            colliderBuffer = new Collider[colliderBuffer.Length * 2];
        return Copy(n);
    }

    /// <summary>Every collider in the box (allocates, like <see cref="Physics.OverlapBox(Vector3, Vector3, Quaternion, int, QueryTriggerInteraction)"/>).</summary>
    public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, int layerMask = Physics.AllLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(center, out PhysicsScene s))
            return Physics.OverlapBox(center, halfExtents, orientation, layerMask, queryTriggerInteraction);
        int n;
        while ((n = s.OverlapBox(center, halfExtents, colliderBuffer, orientation, layerMask, queryTriggerInteraction)) == colliderBuffer.Length)
            colliderBuffer = new Collider[colliderBuffer.Length * 2];
        return Copy(n);
    }

    /// <summary>Every collider in the capsule (allocates, like <see cref="Physics.OverlapCapsule(Vector3, Vector3, float, int, QueryTriggerInteraction)"/>).</summary>
    public static Collider[] OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask = Physics.AllLayers,
        QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        if (UsesDefault(point0, out PhysicsScene s))
            return Physics.OverlapCapsule(point0, point1, radius, layerMask, queryTriggerInteraction);
        int n;
        while ((n = s.OverlapCapsule(point0, point1, radius, colliderBuffer, layerMask, queryTriggerInteraction)) == colliderBuffer.Length)
            colliderBuffer = new Collider[colliderBuffer.Length * 2];
        return Copy(n);
    }

    private static Collider[] Copy(int n)
    {
        var result = new Collider[n];
        System.Array.Copy(colliderBuffer, result, n);
        return result;
    }
}
