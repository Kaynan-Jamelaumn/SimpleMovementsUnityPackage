using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using MeshRef = UnityEngine.EntityId;
#else
using MeshRef = System.Int32;
#endif

/// <summary>
/// Pre-bakes ("cooks") the PhysX collision data of meshes created at runtime - terrain chunks, dungeon floors -
/// on job threads, with exactly the cooking options their <see cref="MeshCollider"/> uses.
///
/// Why: a MeshCollider can only use pre-baked data that was cooked with the same options (and convex flag) it has.
/// When they differ - or nothing was baked - Unity cooks the mesh again on the main thread the moment the mesh is
/// assigned (a hitch of several milliseconds for a terrain chunk) and, in recent versions, logs
/// "Mesh ... used by the Mesh Collider is missing pre-baked triangle collision". <see cref="Physics.BakeMesh(int, bool)"/>
/// without options cooks with its own defaults, which is what caused that warning for the terrain.
///
/// Use: <see cref="Schedule(Mesh, bool)"/> right after creating the mesh (don't modify it afterwards), keep building,
/// and once the handle <see cref="JobHandle.IsCompleted"/> call <see cref="JobHandle.Complete"/> and
/// <see cref="Assign"/>. Never destroy the mesh while its bake is running (Complete the handle first).
/// </summary>
public static class MeshColliderBaker
{
    /// <summary>
    /// The cooking options every runtime-generated MeshCollider in the package uses - Unity's defaults (faster
    /// simulation, mesh cleaning, welding co-located vertices, fast midphase). Cooking cost doesn't matter much
    /// here since it runs on a job thread.
    /// </summary>
    public const MeshColliderCookingOptions CookingOptions =
        MeshColliderCookingOptions.CookForFasterSimulation |
        MeshColliderCookingOptions.EnableMeshCleaning |
        MeshColliderCookingOptions.WeldColocatedVertices |
        MeshColliderCookingOptions.UseFastMidphase;

    /// <summary>Starts cooking <paramref name="mesh"/> on a job thread. The mesh must stay alive and unchanged until the job completes.</summary>
    public static JobHandle Schedule(Mesh mesh, bool convex = false, JobHandle dependsOn = default)
    {
        if (mesh == null)
            return dependsOn;
        JobHandle handle = new BakeJob { Mesh = IdOf(mesh), Convex = convex }.Schedule(dependsOn);
        JobHandle.ScheduleBatchedJobs();
        return handle;
    }

    /// <summary>
    /// Gives <paramref name="collider"/> the cooking options the mesh was baked with, then the mesh - which, once its
    /// bake has completed, uses the pre-baked data instead of cooking on the main thread.
    /// </summary>
    public static void Assign(MeshCollider collider, Mesh mesh, bool convex = false)
    {
        if (collider == null)
            return;
#if UNITY_2022_1_OR_NEWER
        collider.cookingOptions = CookingOptions;
#endif
        collider.convex = convex;
        collider.sharedMesh = mesh;
    }

    /// <summary>Cooks and assigns right away on the main thread (editor tools and tests, where a hitch doesn't matter).</summary>
    public static void BakeAndAssign(MeshCollider collider, Mesh mesh, bool convex = false)
    {
        if (mesh != null)
            Bake(IdOf(mesh), convex);
        Assign(collider, mesh, convex);
    }

    private static MeshRef IdOf(Mesh mesh)
    {
#if UNITY_6000_3_OR_NEWER
        return mesh.GetEntityId();
#else
        return mesh.GetInstanceID();
#endif
    }

    private static void Bake(MeshRef mesh, bool convex)
    {
#if UNITY_2022_1_OR_NEWER
        Physics.BakeMesh(mesh, convex, CookingOptions);
#else
        // Older versions have no overload with options: the collider keeps its default options, which this matches.
        Physics.BakeMesh(mesh, convex);
#endif
    }

    private struct BakeJob : IJob
    {
        public MeshRef Mesh;
        public bool Convex;

        public void Execute() => Bake(Mesh, Convex);
    }

    /// <summary>
    /// Bakes a set of meshes in parallel and assigns them to their colliders when every bake is done - e.g. all the
    /// colliders of one dungeon floor. Add while building, then poll <see cref="IsCompleted"/> and call
    /// <see cref="Finish"/> (or call Finish directly to wait).
    /// </summary>
    public sealed class Batch
    {
        private readonly List<MeshCollider> colliders = new List<MeshCollider>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<bool> convex = new List<bool>();
        private readonly List<JobHandle> handles = new List<JobHandle>();

        public int Count => colliders.Count;

        /// <summary>Starts baking <paramref name="mesh"/> for <paramref name="collider"/> (assigned by <see cref="Finish"/>).</summary>
        public void Add(MeshCollider collider, Mesh mesh, bool isConvex = false)
        {
            if (collider == null || mesh == null)
                return;
            colliders.Add(collider);
            meshes.Add(mesh);
            convex.Add(isConvex);
            handles.Add(Schedule(mesh, isConvex));
        }

        /// <summary>True when every bake has finished (Finish then costs nothing).</summary>
        public bool IsCompleted
        {
            get
            {
                foreach (JobHandle h in handles)
                    if (!h.IsCompleted)
                        return false;
                return true;
            }
        }

        /// <summary>Waits for the bakes (if still running) and assigns every mesh to its collider.</summary>
        public void Finish()
        {
            CompleteJobs();
            for (int i = 0; i < colliders.Count; i++)
                Assign(colliders[i], meshes[i], convex[i]);
            colliders.Clear();
            meshes.Clear();
            convex.Clear();
        }

        /// <summary>Waits for the bakes without assigning (before destroying the meshes, e.g. when a build is cancelled).</summary>
        public void Cancel()
        {
            CompleteJobs();
            colliders.Clear();
            meshes.Clear();
            convex.Clear();
        }

        private void CompleteJobs()
        {
            if (handles.Count == 0)
                return;
            var all = new NativeArray<JobHandle>(handles.Count, Allocator.Temp);
            for (int i = 0; i < handles.Count; i++)
                all[i] = handles[i];
            JobHandle.CombineDependencies(all).Complete();
            all.Dispose();
            handles.Clear();
        }
    }
}
