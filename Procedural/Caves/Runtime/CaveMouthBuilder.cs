using System;
using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralCaves
{
    /// <summary>
    /// The rock of a cave's passages, generated: an arch of boulders round an opening (local +z faces out of it, the
    /// opening at z = 0), a short tunnel behind it that darkens ring by ring, and a curtain at its end - black where the
    /// way goes into the dark (a cave mouth in the world, the way deeper), bright daylight where it climbs out. Boulders
    /// lie along the tunnel's sides, over its roof and across its back (outside it: nothing covers the way in). In the
    /// world the rocks' feet and the tunnel's floor follow the ground (<c>ground(x, z)</c>) and a hill rises behind, so
    /// the mouth reads as an outcrop or a hillside opening. The mesh data is plain math; only <see cref="Create"/>
    /// touches Unity objects (main thread).
    /// </summary>
    public static class CaveMouthBuilder
    {
        public enum Style
        {
            /// <summary>A cave mouth in the open world: into the dark, a rock mound behind.</summary>
            Overworld,
            /// <summary>Inside a cave: the passage up to the surface, daylight at its end.</summary>
            WayOut,
            /// <summary>Inside a cave: a passage down into the dark, to a deeper cave.</summary>
            WayDeeper,
        }

        /// <summary>The size and kind of a passage.</summary>
        public struct Shape
        {
            public Style Style;
            public float Width, Height, Depth;
            public int Seed;
        }

        /// <summary>A passage as mesh data (rock: the boulders; tunnel: its walls, roof, floor and curtain) and where its trigger and light go.</summary>
        public sealed class Meshes
        {
            public readonly MeshBuffers Rock = new MeshBuffers { TextureScale = 2.5f };
            public readonly MeshBuffers Tunnel = new MeshBuffers { TextureScale = 2.5f };
            public Vector3 TriggerCenter, TriggerSize;
            public Vector3 LightPosition;
        }

        /// <summary>Length of tunnel at the opening that stays level inside a cave (meters).</summary>
        public const float FlatEntry = 1f;

        // The four materials of a passage, by surface: rock, dim rock, dark rock, the curtain.
        public const DungeonSurface RockSurface = DungeonSurface.CaveWall;
        public const DungeonSurface DimSurface = DungeonSurface.CaveCeiling;
        public const DungeonSurface DarkSurface = DungeonSurface.BuiltWall;
        public const DungeonSurface CurtainSurface = DungeonSurface.Void;

        /// <summary>
        /// The mesh data of a passage. <paramref name="ground"/> gives the local height of the ground at local (x, z)
        /// (null = flat at 0): the tunnel's floor and the boulders' feet follow it.
        /// </summary>
        public static Meshes Build(Shape s, Func<float, float, float> ground = null)
        {
            var m = new Meshes();
            float w = Mathf.Max(1.6f, s.Width), h = Mathf.Max(2.2f, s.Height), depth = Mathf.Max(0.8f, s.Depth);
            Func<float, float, float> G = ground ?? ((x, z) => 0f);
            var rng = new DungeonRandom((ulong)(uint)s.Seed * 0x9E3779B97F4A7C15UL + 0x2BUL);
            bool world = s.Style == Style.Overworld;

            // ---- the tunnel: rings of an arched profile, narrowing and darkening away from the opening
            const int Rings = 4;
            const int ArchSteps = 7;
            float wallTop = h * 0.5f;
            // Inside a cave the player walks on the level's own (flat) floor: the way climbs out, or goes down, only past
            // the first metre of the tunnel, where the screen is already going dark.
            float slope = s.Style == Style.WayOut ? 0.32f : s.Style == Style.WayDeeper ? -0.45f : 0f;
            float Climb(float z) => slope * Mathf.Max(0f, -z - FlatEntry);
            var rings = new List<Vector3[]>();
            var floors = new float[Rings + 1];
            for (int k = 0; k <= Rings; k++)
            {
                float z = -depth * k / Rings;
                float scale = 1f - 0.14f * k / Rings;
                float half = w * 0.5f * scale, top = h * scale;
                // The floor follows the ground in the world (never below it); inside it climbs out or goes down.
                float floorY = world ? Mathf.Max(G(0f, z), G(-half * 0.6f, z), G(half * 0.6f, z)) + 0.05f : Climb(z);
                floors[k] = floorY;
                var ring = new List<Vector3>();
                ring.Add(new Vector3(-half, floorY - 0.15f, z));
                ring.Add(new Vector3(-half, floorY + wallTop * scale, z));
                for (int i = 1; i < ArchSteps; i++)
                {
                    float a = Mathf.PI * (1f - i / (float)ArchSteps);
                    ring.Add(new Vector3(Mathf.Cos(a) * half, floorY + wallTop * scale + Mathf.Sin(a) * (top - wallTop * scale), z));
                }
                ring.Add(new Vector3(half, floorY + wallTop * scale, z));
                ring.Add(new Vector3(half, floorY - 0.15f, z));
                // Rough rock: every point a little in or out (not at the opening's rim, where the boulders meet it).
                for (int i = 0; i < ring.Count; i++)
                {
                    float j = k == 0 ? 0.03f : 0.12f;
                    ring[i] += new Vector3(rng.Range(-j, j), rng.Range(-j, j) * 0.6f, rng.Range(-j, j) * 0.5f);
                }
                rings.Add(ring.ToArray());
            }
            for (int k = 0; k < Rings; k++)
            {
                DungeonSurface surface = k == 0 ? RockSurface : k == 1 ? DimSurface : DarkSurface;
                Vector3[] a = rings[k], b = rings[k + 1];
                float axisY = (floors[k] + h * 0.5f);
                for (int i = 0; i < a.Length - 1; i++)
                {
                    Vector3 mid = (a[i] + a[i + 1] + b[i] + b[i + 1]) * 0.25f;
                    Vector3 inward = new Vector3(0f, axisY, mid.z) - mid;
                    m.Tunnel.Quad(a[i], a[i + 1], b[i + 1], b[i], surface, true, inward);
                }
                // The floor between the walls' feet.
                Vector3 fl0 = new Vector3(a[0].x, floors[k], a[0].z), fr0 = new Vector3(a[a.Length - 1].x, floors[k], a[0].z);
                Vector3 fl1 = new Vector3(b[0].x, floors[k + 1], b[0].z), fr1 = new Vector3(b[b.Length - 1].x, floors[k + 1], b[0].z);
                m.Tunnel.Quad(fl0, fr0, fr1, fl1, surface, false, Vector3.up);
            }
            // The curtain closing the far end, facing the opening.
            Vector3[] last = rings[Rings];
            var centre = new Vector3(0f, floors[Rings] + h * 0.45f, last[0].z - 0.02f);
            for (int i = 0; i < last.Length - 1; i++)
                m.Tunnel.Triangle(centre, last[i], last[i + 1], CurtainSurface, false, Vector3.forward);
            m.Tunnel.Triangle(centre, last[last.Length - 1], last[0], CurtainSurface, false, Vector3.forward);

            // ---- the rock round it: pillars and a lintel at the opening, boulders along the tunnel's sides and over its
            // roof, one closing its back - all outside the tunnel, so nothing hides its way in - and in the world a hill
            // behind it and loose boulders about.
            float Foot(float x, float z) => G(x, z) - 0.35f;
            void Rock(Vector3 c, Vector3 radii, int seed, int ringCount = 7, int sideCount = 11) =>
                CaveRockMesh.Boulder(m.Rock, c, radii, Quaternion.Euler(rng.Range(-8f, 8f), rng.Range(0f, 360f), rng.Range(-8f, 8f)), seed, RockSurface, ringCount, sideCount);
            // Rocks along the tunnel keep their long side along it (turned freely they would reach into the way).
            void Aligned(Vector3 c, Vector3 radii, int seed, int ringCount, int sideCount) =>
                CaveRockMesh.Boulder(m.Rock, c, radii, Quaternion.Euler(rng.Range(-6f, 6f), rng.Range(-14f, 14f), rng.Range(-6f, 6f)), seed, RockSurface, ringCount, sideCount);
            float pillarR = Mathf.Max(0.55f, w * 0.2f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (w * 0.5f + pillarR * 0.55f);
                float foot = Foot(x, 0.1f);
                Rock(new Vector3(x, foot + h * 0.5f, 0.15f), new Vector3(pillarR, h * 0.62f, pillarR * 1.15f), s.Seed * 7 + side);
                Rock(new Vector3(side * (w * 0.5f + pillarR * 0.4f), floors[0] + h * 0.98f, -0.25f), new Vector3(pillarR * 1.1f, pillarR * 0.95f, pillarR * 1.25f), s.Seed * 11 + side);
            }
            Rock(new Vector3(rng.Range(-0.2f, 0.2f), floors[0] + h + 0.3f, 0.05f), new Vector3(w * 0.62f, Mathf.Max(0.55f, h * 0.2f), 0.95f), s.Seed * 13);
            // Along the tunnel: its walls and roof seen from outside are rock, never the inside of a tube.
            float sideR = Mathf.Max(0.6f, w * 0.24f);
            const int Along = 3;
            for (int k = 0; k < Along; k++)
            {
                float t = (k + 0.6f) / Along;
                float z = -depth * t;
                float scale = 1f - 0.14f * t;
                // Never lower than the opening's floor: a way down keeps its full height over the player's (flat) floor.
                float floorY = world ? Mathf.Lerp(floors[0], floors[Rings], t) : Mathf.Max(floors[0], Mathf.Lerp(floors[0], floors[Rings], t));
                float half = w * 0.5f * scale;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (half + sideR * 0.95f);
                    float bottom = world ? Mathf.Min(Foot(x, z), floorY - 0.3f) : floorY - 0.3f;
                    float top = floorY + h * scale * 0.85f;
                    float ry = Mathf.Max(sideR, (top - bottom) * 0.5f);
                    Aligned(new Vector3(x, (top + bottom) * 0.5f, z), new Vector3(sideR, ry, depth / Along * 0.75f + 0.25f), s.Seed * 23 + k * 2 + side, 6, 9);
                }
                float roofR = Mathf.Max(0.5f, h * 0.18f);
                Aligned(new Vector3(rng.Range(-0.15f, 0.15f), floorY + h * scale + roofR * 0.55f, z), new Vector3(half + sideR * 0.8f, roofR, depth / Along * 0.75f + 0.25f), s.Seed * 29 + k, 6, 10);
            }
            float backZ = -depth - Mathf.Max(0.5f, w * 0.18f) - 0.1f;
            Aligned(new Vector3(0f, floors[Rings] + h * 0.45f, backZ), new Vector3(w * 0.62f, h * 0.62f, Mathf.Max(0.5f, w * 0.18f)), s.Seed * 31, 6, 10);
            if (world)
            {
                // A hill rising behind the opening (it starts behind the tunnel's end, so the way in stays dark and open).
                var hill = new Vector3(w * 1.05f + 1.2f, h * 1.15f, 2.6f);
                float hz = -depth - hill.z - 0.2f;
                Aligned(new Vector3(0f, Foot(0f, hz) + h * 0.55f, hz), hill, s.Seed * 17, 8, 12);
                int loose = rng.Range(4, 8);
                for (int i = 0; i < loose; i++)
                {
                    float side = rng.Chance(0.5f) ? -1f : 1f;
                    float x = side * rng.Range(w * 0.55f + sideR * 2f, w * 0.5f + sideR * 2f + 2.4f);
                    float z = rng.Range(-depth, 1.4f);
                    float r = rng.Range(0.3f, 0.75f);
                    Rock(new Vector3(x, G(x, z) + r * 0.3f, z), new Vector3(r, r * 0.75f, r * rng.Range(0.8f, 1.2f)), s.Seed * 19 + i, 5, 8);
                }
            }
            m.Rock.FinishNormals();
            m.Tunnel.FinishNormals();

            // ---- where the trigger and the light go. The trigger fills the whole tunnel, from just inside the opening to
            // past the rock closing its end: however far in a player walks, they stay in it (walking out of its front is the
            // only way to leave it, and that is what cancels the way in).
            float triggerFloor = Mathf.Lerp(floors[0], floors[Rings], 0.5f);
            m.TriggerCenter = new Vector3(0f, triggerFloor + h * 0.45f, (TriggerFront + TriggerBack(depth)) * 0.5f);
            m.TriggerSize = new Vector3(w * 0.72f, h * 0.9f, TriggerFront - TriggerBack(depth));
            m.LightPosition = new Vector3(0f, floors[Rings] + h * 0.6f, -depth * 0.75f);
            return m;
        }

        /// <summary>Local z of the trigger's front face (just inside the opening).</summary>
        public const float TriggerFront = -0.35f;

        /// <summary>Local z of the trigger's back face for a tunnel <paramref name="depth"/> long (behind the rock closing it).</summary>
        public static float TriggerBack(float depth) => -(Mathf.Max(0.8f, depth) + 0.3f);

        /// <summary>
        /// Where players come back out of a mouth <paramref name="width"/> wide (local z on its centre line): in front of the
        /// opening, clear of its trigger and its pillars, with room for the ring a party stands in round the point.
        /// </summary>
        public static float ReturnZ(float width)
        {
            float pillarFront = 0.15f + Mathf.Max(0.55f, Mathf.Max(1.6f, width) * 0.2f) * 1.15f;
            return Mathf.Max(3f, Mathf.Max(TriggerFront + 3.35f, pillarFront + 2f));
        }

        /// <summary>
        /// Makes the passage's objects under <paramref name="parent"/>: "Passage Rock" (the boulders, with a collider) and
        /// "Passage Tunnel" (no collider: the floor under it carries the player). <paramref name="materials"/> by surface:
        /// rock, dim, dark, curtain. Colliders are cooked through <paramref name="addCollider"/> when given (a dungeon's
        /// build) or set at once. The meshes go to <paramref name="owned"/> (destroyed with what owns them).
        /// <paramref name="tangents"/>: give the meshes tangents (normal-mapped materials) - done before any collider is
        /// cooked, since a mesh must not change while its collider bakes.
        /// </summary>
        public static GameObject Create(Transform parent, Meshes m, Material rock, Material dim, Material dark, Material curtain,
            bool rockCollider, Action<MeshCollider, Mesh> addCollider, List<Mesh> owned, int layer, bool tangents = false)
        {
            var root = new GameObject("Passage");
            root.layer = layer;
            root.transform.SetParent(parent, false);
            Mesh rockMesh = Upload(m.Rock, "Passage Rock", tangents, out List<DungeonSurface> rockSurfaces);
            Mesh tunnelMesh = Upload(m.Tunnel, "Passage Tunnel", tangents, out List<DungeonSurface> tunnelSurfaces);
            Material Pick(DungeonSurface s) => s == CurtainSurface ? curtain : s == DarkSurface ? dark : s == DimSurface ? dim : rock;
            if (rockMesh != null)
            {
                owned?.Add(rockMesh);
                GameObject go = Part(root.transform, "Passage Rock", rockMesh, rockSurfaces.ConvertAll(Pick).ToArray(), true, layer);
                if (rockCollider)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    if (addCollider != null)
                        addCollider(mc, rockMesh);
                    else
                        mc.sharedMesh = rockMesh;
                }
            }
            if (tunnelMesh != null)
            {
                owned?.Add(tunnelMesh);
                Part(root.transform, "Passage Tunnel", tunnelMesh, tunnelSurfaces.ConvertAll(Pick).ToArray(), false, layer);
            }
            return root;
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material[] materials, bool shadows, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A mesh with one submesh per surface used (their surfaces in order), final before anything cooks it.</summary>
        private static Mesh Upload(MeshBuffers b, string name, bool tangents, out List<DungeonSurface> surfaces)
        {
            surfaces = new List<DungeonSurface>();
            if (b == null || b.IsEmpty)
                return null;
            var mesh = new Mesh { name = name };
            if (b.Vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.Vertices);
            mesh.SetNormals(b.Normals);
            mesh.SetUVs(0, b.Uvs);
            var used = new List<int>();
            for (int i = 0; i < b.Triangles.Length; i++)
                if (b.Triangles[i].Count > 0)
                    used.Add(i);
            mesh.subMeshCount = Mathf.Max(1, used.Count);
            for (int i = 0; i < used.Count; i++)
            {
                mesh.SetTriangles(b.Triangles[used[i]], i, false);
                surfaces.Add((DungeonSurface)used[i]);
            }
            mesh.RecalculateBounds();
            if (tangents)
                mesh.RecalculateTangents();
            return mesh;
        }
    }
}
