using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>Surfaces of generated dungeon meshes (one submesh / material each).</summary>
    public enum DungeonSurface
    {
        BuiltFloor = 0,
        BuiltWall = 1,
        BuiltCeiling = 2,
        CaveFloor = 3,
        CaveWall = 4,
        CaveCeiling = 5,
        Trim = 6,
        Stairs = 7,
        /// <summary>Water of flooded floors (no collider: players wade through it).</summary>
        Liquid = 8,
        /// <summary>The dark bottom of a chasm or the void (unlit).</summary>
        Void = 9,
        Count = 10,
    }

    /// <summary>
    /// Mesh data being built: vertices, normals, UVs and one triangle list per <see cref="DungeonSurface"/>. Plain data -
    /// filled on a worker thread, uploaded to a UnityEngine.Mesh on the main thread. Organic surfaces weld shared
    /// vertices (smooth shading); built surfaces don't (crisp edges).
    /// </summary>
    public sealed class MeshBuffers
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<int>[] Triangles = new List<int>[(int)DungeonSurface.Count];
        /// <summary>Which chunk of the floor this is (in chunks).</summary>
        public Vector2Int Chunk;
        public float TextureScale = 3f;

        private readonly Dictionary<long, int> weld = new Dictionary<long, int>();

        public MeshBuffers()
        {
            for (int i = 0; i < Triangles.Length; i++)
                Triangles[i] = new List<int>();
        }

        public bool IsEmpty => Vertices.Count == 0;

        public int TriangleCount
        {
            get
            {
                int n = 0;
                foreach (List<int> t in Triangles)
                    n += t.Count / 3;
                return n;
            }
        }

        private Vector2 Uv(Vector3 p, Vector3 normal)
        {
            float s = 1f / Mathf.Max(0.01f, TextureScale);
            if (Mathf.Abs(normal.y) > 0.6f)
                return new Vector2(p.x * s, p.z * s);
            // Walls: along the wall horizontally, height vertically.
            float u = Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? p.z : p.x;
            return new Vector2(u * s, p.y * s);
        }

        private int Vertex(Vector3 p, Vector3 normal, DungeonSurface surface, bool smooth)
        {
            if (smooth)
            {
                long key = Key(p, (int)surface);
                if (weld.TryGetValue(key, out int existing))
                {
                    Normals[existing] += normal;
                    return existing;
                }
                weld[key] = Vertices.Count;
            }
            Vertices.Add(p);
            Normals.Add(normal);
            Uvs.Add(Uv(p, normal));
            return Vertices.Count - 1;
        }

        private static long Key(Vector3 p, int surface)
        {
            unchecked
            {
                long x = Mathf.RoundToInt(p.x * 256f), y = Mathf.RoundToInt(p.y * 256f), z = Mathf.RoundToInt(p.z * 256f);
                return (x & 0xFFFFF) | ((y & 0xFFFF) << 20) | ((z & 0xFFFFF) << 36) | ((long)surface << 56);
            }
        }

        /// <summary>
        /// Adds a triangle. Unity treats clockwise triangles (seen from the front) as front faces; the face normal is
        /// Cross(b - a, c - a). If <paramref name="facing"/> is non-zero, the winding is flipped when needed so the face
        /// looks that way.
        /// </summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, DungeonSurface surface, bool smooth, Vector3 facing = default)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f)
                return;
            if (facing.sqrMagnitude > 0f && Vector3.Dot(n, facing) < 0f)
            {
                Vector3 t = b;
                b = c;
                c = t;
                n = -n;
            }
            n = n.normalized;
            List<int> list = Triangles[(int)surface];
            list.Add(Vertex(a, n, surface, smooth));
            list.Add(Vertex(b, n, surface, smooth));
            list.Add(Vertex(c, n, surface, smooth));
        }

        /// <summary>Adds a quad a-b-c-d (in order around its edge), facing <paramref name="facing"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, DungeonSurface surface, bool smooth, Vector3 facing)
        {
            if (!smooth && FlatQuad(a, b, c, d, surface, facing))
                return;
            Triangle(a, b, c, surface, smooth, facing);
            Triangle(a, c, d, surface, smooth, facing);
        }

        /// <summary>
        /// A crisp (unwelded) quad whose two triangles (a, b, c) and (a, c, d) face exactly the same way - the usual
        /// built floor, wall or ceiling - as four vertices instead of six: the corners both triangles use get the same
        /// normal and texture coordinate from either, so one vertex serves both. The same two triangles, in the same
        /// order and with the same corners, as two <see cref="Triangle"/> calls. Returns false, adding nothing, for
        /// any other quad (bent, or with a degenerate half), which is then built triangle by triangle.
        /// </summary>
        private bool FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, DungeonSurface surface, Vector3 facing)
        {
            // Each half's normal exactly as Triangle works it out: flipped to look along facing, then normalized.
            Vector3 n1 = Vector3.Cross(b - a, c - a);
            Vector3 n2 = Vector3.Cross(c - a, d - a);
            if (n1.sqrMagnitude < 1e-10f || n2.sqrMagnitude < 1e-10f)
                return false;
            bool faced = facing.sqrMagnitude > 0f;
            bool flip1 = faced && Vector3.Dot(n1, facing) < 0f;
            bool flip2 = faced && Vector3.Dot(n2, facing) < 0f;
            if (flip1)
                n1 = -n1;
            if (flip2)
                n2 = -n2;
            n1 = n1.normalized;
            n2 = n2.normalized;
            // Component by component: Vector3's == is only approximate.
            if (n1.x != n2.x || n1.y != n2.y || n1.z != n2.z)
                return false;

            int ia = Vertex(a, n1, surface, false);
            int ib = Vertex(b, n1, surface, false);
            int ic = Vertex(c, n1, surface, false);
            int id = Vertex(d, n1, surface, false);
            List<int> list = Triangles[(int)surface];
            list.Add(ia);
            list.Add(flip1 ? ic : ib);
            list.Add(flip1 ? ib : ic);
            list.Add(ia);
            list.Add(flip2 ? id : ic);
            list.Add(flip2 ? ic : id);
            return true;
        }

        /// <summary>An axis-aligned box (all six faces, outward).</summary>
        public void Box(Vector3 min, Vector3 max, DungeonSurface surface, bool bottom = false)
        {
            // The eight corners: bit 0 of the number picks x (min / max), bit 1 picks y, bit 2 picks z.
            var p0 = new Vector3(min.x, min.y, min.z);
            var p1 = new Vector3(max.x, min.y, min.z);
            var p2 = new Vector3(min.x, max.y, min.z);
            var p3 = new Vector3(max.x, max.y, min.z);
            var p4 = new Vector3(min.x, min.y, max.z);
            var p5 = new Vector3(max.x, min.y, max.z);
            var p6 = new Vector3(min.x, max.y, max.z);
            var p7 = new Vector3(max.x, max.y, max.z);
            Quad(p2, p3, p7, p6, surface, false, Vector3.up);
            if (bottom)
                Quad(p0, p1, p5, p4, surface, false, Vector3.down);
            Quad(p0, p1, p3, p2, surface, false, Vector3.back);
            Quad(p4, p5, p7, p6, surface, false, Vector3.forward);
            Quad(p0, p4, p6, p2, surface, false, Vector3.left);
            Quad(p1, p5, p7, p3, surface, false, Vector3.right);
        }

        /// <summary>Normalizes the accumulated normals of welded vertices.</summary>
        public void FinishNormals()
        {
            for (int i = 0; i < Normals.Count; i++)
            {
                Vector3 n = Normals[i];
                Normals[i] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            }
            weld.Clear();
        }
    }

    /// <summary>A floor's generated geometry, in chunks, plus its link geometry.</summary>
    public sealed class FloorMeshData
    {
        public int Floor;
        public readonly List<MeshBuffers> Chunks = new List<MeshBuffers>();
        /// <summary>Water surface of a flooded floor (built without a collider), or null.</summary>
        public MeshBuffers Liquid;
        /// <summary>The bottom of the floor's chasm (with a collider that catches fallers, kept out of the NavMesh), or null.</summary>
        public MeshBuffers Void;
        /// <summary>Link visuals built into this floor (the lower floor; a spiral's top landing goes to the upper floor), keyed by link id.</summary>
        public readonly List<LinkMeshData> Links = new List<LinkMeshData>();
    }

    public sealed class LinkMeshData
    {
        public int LinkId;
        public MeshBuffers Visual;
        /// <summary>Walkable / blocking surfaces for the MeshCollider (a smooth ramp instead of steps).</summary>
        public MeshBuffers Collider;
        /// <summary>Stairs: points for a NavMeshLink across the top edge (local to the lower floor).</summary>
        public Vector3 LinkStart, LinkEnd;
        public float LinkWidth;
        /// <summary>False for pieces without a NavMeshLink (a spiral's top landing).</summary>
        public bool NavLink = true;
        /// <summary>The NavMeshLink works both ways (stairs, spirals, climbs; drops are one way).</summary>
        public bool Bidirectional = true;
        /// <summary>Climbs: the shaft's climbable volume (local to the floor it is built into).</summary>
        public Bounds ClimbVolume;
    }
}
