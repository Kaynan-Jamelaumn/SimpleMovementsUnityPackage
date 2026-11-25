using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralCaves
{
    /// <summary>
    /// The generated look of a deposit without a prefab of its own: a lump of rock with the mineral showing in it -
    /// nuggets for ores, coal and stone, crystals for gemstone veins - in a few shapes per kind of deposit, shared by every
    /// deposit (one mesh each, never one per deposit). Worked out, the mineral disappears and the bare rock stays. Deposit
    /// objects are pooled: a cave entered again reuses them instead of creating new ones. Main thread.
    /// </summary>
    public static class CaveDepositArt
    {
        /// <summary>Shapes per kind of deposit.</summary>
        public const int Variants = 3;

        private sealed class Shape
        {
            public Mesh Rock, Mineral;
            public float Radius;
        }

        private static readonly Dictionary<long, Shape> shapes = new Dictionary<long, Shape>();
        private static readonly Stack<GameObject> pool = new Stack<GameObject>();
        private static Transform poolRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            shapes.Clear();
            pool.Clear();
            poolRoot = null;
        }

        /// <summary>Deposit objects waiting in the pool.</summary>
        public static int Pooled => pool.Count;

        // ------------------------------------------------------------------ objects

        /// <summary>A deposit object (from the pool when one waits there): "Rock" and "Mineral" children, a collider, an Ore Deposit.</summary>
        public static GameObject Take(Transform parent)
        {
            GameObject go = null;
            while (pool.Count > 0 && go == null)
                go = pool.Pop();
            if (go == null)
            {
                go = new GameObject("Deposit");
                Part(go.transform, "Rock");
                Part(go.transform, "Mineral");
                go.AddComponent<SphereCollider>();
                go.AddComponent<OreDeposit>();
                go.AddComponent<CaveDeposit>();
            }
            go.transform.SetParent(parent, false);
            go.SetActive(true);
            return go;
        }

        /// <summary>Gives a deposit object back (it leaves the dungeon, which is about to be destroyed).</summary>
        public static void Give(GameObject go)
        {
            if (go == null)
                return;
            var adapter = go.GetComponent<CaveDeposit>();
            if (adapter != null)
                adapter.Release();
            // Outside Play mode (an editor preview being cleared) nothing is pooled: the scene would keep the pool.
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(go);
                return;
            }
            if (pool.Count >= 512)
            {
                Object.Destroy(go);
                return;
            }
            if (poolRoot == null)
            {
                var root = new GameObject("[Cave Deposit Pool]") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(root);
                poolRoot = root.transform;
            }
            // It leaves its dungeon for good: its placement tag no longer belongs to it (the dungeon's teardown removes only
            // the objects still its own that it finds outside itself - network-spawned mobs and pickups), and the next cave
            // tags it anew. Detached, not destroyed: a teardown may run inside a physics callback (a portal's trigger), where
            // nothing can be destroyed at once. No network identity: it may come back as another dungeon's element (as the
            // builder's own pool does).
            DungeonSpawned tag = go.GetComponent<DungeonSpawned>();
            if (tag != null)
                tag.Detach();
            DungeonElement element = go.GetComponent<DungeonElement>();
            if (element != null)
            {
                element.Id = -1;
                element.Dungeon = null;
                element.ResetStates();
            }
            go.SetActive(false);
            go.transform.SetParent(poolRoot, false);
            pool.Push(go);
        }

        private static void Part(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }

        /// <summary>
        /// Shows <paramref name="e"/> on deposit object <paramref name="go"/>: its shape (variant from the seam), materials and
        /// collider. <paramref name="onWall"/>: the rock is half sunk in the wall, facing out (+z). Returns the mineral child
        /// (hidden while the deposit is worked out).
        /// </summary>
        public static GameObject Dress(GameObject go, CaveResourceEntry e, int seam, bool onWall, float size, Material rock, Material mineral, bool shadows)
        {
            Shape shape = ShapeFor(e, Mathf.Abs(seam) % Variants, onWall);
            Transform rockT = go.transform.Find("Rock"), mineralT = go.transform.Find("Mineral");
            Set(rockT, shape.Rock, rock, shadows);
            Set(mineralT, shape.Mineral, mineral, false);
            go.transform.localScale = Vector3.one * Mathf.Max(0.3f, size);
            var sphere = go.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.radius = shape.Radius;
                sphere.center = onWall ? new Vector3(0f, 0f, shape.Radius * 0.15f) : new Vector3(0f, shape.Radius * 0.55f, 0f);
                sphere.isTrigger = false;
            }
            return mineralT != null ? mineralT.gameObject : null;
        }

        private static void Set(Transform part, Mesh mesh, Material material, bool shadows)
        {
            if (part == null)
                return;
            part.gameObject.SetActive(mesh != null);
            var filter = part.GetComponent<MeshFilter>();
            var renderer = part.GetComponent<MeshRenderer>();
            if (filter != null)
                filter.sharedMesh = mesh;
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ shapes

        private static Shape ShapeFor(CaveResourceEntry e, int variant, bool onWall)
        {
            bool gem = e.kind == CraftStockKind.Gem;
            bool soft = e.kind == CraftStockKind.Supply || e.kind == CraftStockKind.Fuel || e.kind == CraftStockKind.Quenchant;
            int style = gem ? 2 : soft ? 1 : 0;
            long key = ((long)style << 8) | ((long)variant << 1) | (onWall ? 1L : 0L);
            if (shapes.TryGetValue(key, out Shape cached) && cached.Rock != null && (cached.Mineral != null || style == 1 && cached.Mineral == null))
                return cached;
            Shape shape = Make(style, variant, onWall);
            shapes[key] = shape;
            return shape;
        }

        private static Shape Make(int style, int variant, bool onWall)
        {
            int seed = 7919 * (variant + 1) + style * 104729 + (onWall ? 13 : 0);
            var rng = new DungeonRandom((ulong)seed * 0x9E3779B97F4A7C15UL);
            var rock = new MeshBuffers { TextureScale = 1.2f };
            var mineral = new MeshBuffers { TextureScale = 0.6f };
            // A wall deposit is flattened against the wall (its back deep in the rock), a floor deposit is a mound.
            Vector3 radii = onWall ? new Vector3(0.62f, 0.5f, 0.55f) : new Vector3(0.6f, 0.45f, 0.58f);
            if (style == 1)
                radii = onWall ? new Vector3(0.7f, 0.55f, 0.5f) : new Vector3(0.72f, 0.38f, 0.68f);
            Vector3 center = onWall ? new Vector3(0f, 0f, -0.18f) : new Vector3(0f, radii.y * 0.55f, 0f);
            CaveRockMesh.Boulder(rock, center, radii, Quaternion.Euler(0f, rng.Range(-20f, 20f), rng.Range(-12f, 12f)), seed, DungeonSurface.CaveWall);
            // Where the mineral shows: the side facing the cave (a wall deposit's front, a floor deposit's top).
            Vector3 facing = onWall ? Vector3.forward : Vector3.up;
            int count = style == 2 ? rng.Range(4, 7) : rng.Range(7, 13);
            for (int i = 0; i < count; i++)
            {
                var d = new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f));
                if (d.sqrMagnitude < 1e-3f)
                    d = facing;
                d = d.normalized;
                if (Vector3.Dot(d, facing) < 0.1f)
                    d = (d + facing * 1.2f).normalized;
                Vector3 surface = center + Vector3.Scale(d, radii) * 0.92f;
                if (style == 2)
                {
                    float len = rng.Range(0.28f, 0.6f);
                    CaveRockMesh.Crystal(mineral, surface - d * 0.08f, (d + facing * 0.6f).normalized, len, len * rng.Range(0.14f, 0.22f), seed + i, DungeonSurface.CaveWall);
                }
                else
                {
                    float r = style == 1 ? rng.Range(0.1f, 0.2f) : rng.Range(0.06f, 0.14f);
                    CaveRockMesh.Boulder(mineral, surface, new Vector3(r, r * rng.Range(0.6f, 1f), r), Quaternion.identity, seed * 3 + i, DungeonSurface.CaveWall, 4, 6);
                }
            }
            rock.FinishNormals();
            mineral.FinishNormals();
            return new Shape
            {
                Rock = ToMesh(rock, $"Deposit Rock {style}.{variant}{(onWall ? " wall" : "")}"),
                Mineral = ToMesh(mineral, $"Deposit Mineral {style}.{variant}{(onWall ? " wall" : "")}"),
                Radius = Mathf.Max(radii.x, Mathf.Max(radii.y, radii.z)),
            };
        }

        /// <summary>
        /// One single-material mesh from <paramref name="b"/> (every surface into one submesh). Kept for the session by the
        /// shape cache. Not DontSave: an editor preview saved with its scene keeps the meshes of its deposits, and once the
        /// cache lets go of them (a new session) unused ones are unloaded like any other.
        /// </summary>
        public static Mesh ToMesh(MeshBuffers b, string name)
        {
            if (b == null || b.IsEmpty)
                return null;
            var all = new List<int>();
            foreach (List<int> t in b.Triangles)
                all.AddRange(t);
            var mesh = new Mesh { name = name };
            if (b.Vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.Vertices);
            mesh.SetNormals(b.Normals);
            mesh.SetUVs(0, b.Uvs);
            mesh.SetTriangles(all, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
