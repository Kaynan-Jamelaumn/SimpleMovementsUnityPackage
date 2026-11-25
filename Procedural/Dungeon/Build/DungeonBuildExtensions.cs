using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralDungeon
{
    /// <summary>
    /// A custom stage (<see cref="DungeonStageAsset"/>) that also builds what it decided: placements of its own kinds
    /// (<see cref="PlacementKind.Resource"/>: ore seams...), extra geometry per floor (rock formations), changes to what the
    /// builder makes (placements left out, objects it reacts to). When a build - or a network client's copy - starts,
    /// the <see cref="DungeonBuilder"/> asks every custom stage of the profile that implements this for a session.
    /// <para>The stage asset is shared by every build: keep per-build state in the session, never in the asset.</para>
    /// </summary>
    public interface IDungeonBuildExtension
    {
        /// <summary>A build starts (main thread): the session that builds this stage's part of it, or null when there is nothing to do.</summary>
        IDungeonBuildSession BeginBuild(DungeonBuildContext context);
    }

    /// <summary>
    /// One build's part of an <see cref="IDungeonBuildExtension"/>. Called on the main thread, in this order: placements
    /// as each floor is built (<see cref="ShouldBuild"/>, <see cref="TryBuildPlacement"/>, <see cref="OnBuilt"/>), then
    /// <see cref="BuildFloor"/> before the floor's colliders are assigned and its NavMesh is baked, the floor's mobs
    /// after the bake, <see cref="OnFullyBuilt"/> once, and <see cref="Teardown"/> when the dungeon is cleared.
    /// </summary>
    public interface IDungeonBuildSession
    {
        /// <summary>
        /// Builds placement <paramref name="index"/> when it is one of this extension's, under <paramref name="parent"/> at
        /// <paramref name="local"/> (return false for any other). The builder tags the object afterwards (DungeonSpawned,
        /// network id). <paramref name="prefab"/> = the pooled prefab it was taken from through the context (null for an object made here).
        /// </summary>
        bool TryBuildPlacement(Placement p, int index, Transform parent, Vector3 local, Quaternion rotation,
            out GameObject built, out GameObject prefab, out string entryName);

        /// <summary>Is placement <paramref name="index"/> built at all? False leaves it out (a mob killed on an earlier visit).</summary>
        bool ShouldBuild(Placement p, int index);

        /// <summary>The object of placement <paramref name="index"/> exists and is tagged (every kind, mobs included).</summary>
        void OnBuilt(GameObject go, Placement p, int index);

        /// <summary>
        /// The floor's own extra work, after its placements and before its colliders are assigned and its NavMesh is baked
        /// (colliders added with <see cref="DungeonBuildContext.AddCollider"/> are part of the bake). Yield null whenever
        /// <see cref="DungeonBuildContext.ShouldYield"/> to stay within the frame budget.
        /// </summary>
        IEnumerator BuildFloor(int floor);

        /// <summary>Every floor is built (not called for a network client's copy, whose floors arrive one by one).</summary>
        void OnFullyBuilt();

        /// <summary>The dungeon is being cleared: give back pooled objects, free what was made (before its objects are destroyed).</summary>
        void Teardown();
    }

    /// <summary>What a build extension can use of the build it is part of (main thread only).</summary>
    public sealed class DungeonBuildContext
    {
        private readonly Func<bool> shouldYield;
        private readonly Action<MeshCollider, Mesh> addCollider;
        private readonly Func<int, Transform> floorRoot;
        private readonly List<Mesh> ownedMeshes;
        private readonly List<GameObject> pooled;

        internal DungeonBuildContext(DungeonInstance instance, DungeonLayout layout, CompiledProfile profile, DungeonMaterials materials,
            DungeonPrimitives primitives, DungeonObjectPool pool, bool replica, bool immediate, Func<bool> shouldYield,
            Action<MeshCollider, Mesh> addCollider, Func<int, Transform> floorRoot, List<Mesh> ownedMeshes, List<GameObject> pooled)
        {
            Instance = instance;
            Layout = layout;
            Profile = profile;
            Materials = materials;
            Primitives = primitives;
            Pool = pool;
            Replica = replica;
            Immediate = immediate;
            this.shouldYield = shouldYield;
            this.addCollider = addCollider;
            this.floorRoot = floorRoot;
            this.ownedMeshes = ownedMeshes;
            this.pooled = pooled;
        }

        /// <summary>The dungeon being built (its root object).</summary>
        public DungeonInstance Instance { get; }
        /// <summary>Its layout (a network client's copy holds only the floors it was sent).</summary>
        public DungeonLayout Layout { get; }
        public CompiledProfile Profile { get; }
        /// <summary>The build's materials: the theme's per surface (<see cref="DungeonMaterials.BySurface"/>) and flat colours.</summary>
        public DungeonMaterials Materials { get; }
        /// <summary>The built-in stand-in objects.</summary>
        public DungeonPrimitives Primitives { get; }
        /// <summary>The pool prefab instances come from (give them back with <see cref="TrackPooled"/>, or release them yourself).</summary>
        public DungeonObjectPool Pool { get; }
        /// <summary>A network client's presentation-only copy: show things, decide nothing (no items, no mobs, no rules).</summary>
        public bool Replica { get; }
        /// <summary>Built in one go (editor preview, tests): no frame budget.</summary>
        public bool Immediate { get; }

        public int GeometryLayer => Profile != null && Profile.Build != null ? Profile.Build.geometryLayer : 0;
        public bool CastShadows => Profile == null || Profile.Build == null || Profile.Build.castShadows;

        /// <summary>True when this frame's build budget is used up: yield null.</summary>
        public bool ShouldYield() => shouldYield != null && shouldYield();

        /// <summary>The root of floor <paramref name="floor"/> (null when it isn't built yet).</summary>
        public Transform FloorRoot(int floor) => floorRoot?.Invoke(floor);

        /// <summary>A child of a floor's root by name (made when missing), e.g. "Formations".</summary>
        public Transform FloorChild(int floor, string name)
        {
            Transform root = FloorRoot(floor);
            if (root == null)
                return null;
            Transform t = root.Find(name);
            if (t != null)
                return t;
            var go = new GameObject(name) { layer = root.gameObject.layer };
            go.transform.SetParent(root, false);
            return go.transform;
        }

        /// <summary>
        /// A mesh collider whose mesh is cooked on a job thread and assigned before the current floor's NavMesh is baked
        /// (call it from <see cref="IDungeonBuildSession.BuildFloor"/>; later it is assigned right away).
        /// </summary>
        public void AddCollider(MeshCollider collider, Mesh mesh)
        {
            if (collider == null || mesh == null)
                return;
            if (addCollider != null)
                addCollider(collider, mesh);
            else
                collider.sharedMesh = mesh;
        }

        /// <summary>
        /// A Mesh made from <paramref name="buffers"/> (32-bit indices when large), owned by the build (destroyed with the
        /// dungeon), and the materials of its submeshes (the build's materials per <see cref="DungeonSurface"/>).
        /// </summary>
        public Mesh CreateMesh(MeshBuffers buffers, string name, out Material[] materials)
        {
            materials = new Material[0];
            if (buffers == null || buffers.IsEmpty)
                return null;
            var mesh = new Mesh { name = name };
            if (buffers.Vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(buffers.Vertices);
            mesh.SetNormals(buffers.Normals);
            mesh.SetUVs(0, buffers.Uvs);
            var used = new List<int>();
            for (int s = 0; s < buffers.Triangles.Length; s++)
                if (buffers.Triangles[s].Count > 0)
                    used.Add(s);
            mesh.subMeshCount = Mathf.Max(1, used.Count);
            for (int i = 0; i < used.Count; i++)
                mesh.SetTriangles(buffers.Triangles[used[i]], i, false);
            mesh.RecalculateBounds();
            if (Profile != null && Profile.Theme != null)
                mesh.RecalculateTangents();   // the theme's materials may have normal maps
            materials = new Material[used.Count];
            for (int i = 0; i < used.Count; i++)
                materials[i] = Materials != null && used[i] < Materials.BySurface.Length ? Materials.BySurface[used[i]] : null;
            Own(mesh);
            return mesh;
        }

        /// <summary>Destroys <paramref name="mesh"/> with the dungeon.</summary>
        public void Own(Mesh mesh)
        {
            if (mesh != null && ownedMeshes != null && !ownedMeshes.Contains(mesh))
                ownedMeshes.Add(mesh);
        }

        /// <summary>A prefab instance taken from <see cref="Pool"/>: given back to it when the dungeon is cleared.</summary>
        public void TrackPooled(GameObject instance)
        {
            if (instance != null && pooled != null)
                pooled.Add(instance);
        }
    }
}
