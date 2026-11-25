using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// Where a cave wall really is: the dungeon mesher draws a natural wall between an open cell and a rock cell at a
    /// point jittered along the line between their centres (by a position hash of the floor's build seed). This repeats
    /// exactly that rule, so things set into walls (deposits, crystals) sit on the wall that is drawn - on the server and
    /// on every client alike. Plain math: safe on worker threads.
    /// </summary>
    public static class CaveWalls
    {
        private const int MeshSalt = 0x6D657368;   // DungeonMesher.Hash01's salt

        /// <summary>
        /// The wall's line (floor-local x, z in meters) between open cell (<paramref name="ax"/>, <paramref name="ay"/>) and its
        /// rock 4-neighbour (<paramref name="bx"/>, <paramref name="by"/>), for a floor meshed with <paramref name="buildSeed"/>
        /// and <paramref name="roughness"/> (Caves > Wall Roughness).
        /// </summary>
        public static Vector2 WallPoint(TileGrid g, float cellSize, int buildSeed, float roughness, int ax, int ay, int bx, int by)
        {
            int lox = ax, loy = ay, hix = bx, hiy = by;
            if (ay > by || (ay == by && ax > bx))
            {
                lox = bx; loy = by; hix = ax; hiy = ay;
            }
            var pa = new Vector2((lox + 0.5f) * cellSize, (loy + 0.5f) * cellSize);
            var pb = new Vector2((hix + 0.5f) * cellSize, (hiy + 0.5f) * cellSize);
            float t = 0.5f;
            if (roughness > 0f && SharedEdgeOrganic(g, ax, ay, bx, by))
            {
                int ex = ax + bx, ey = ay + by;
                float r = Mathf.Clamp(roughness, 0f, 0.45f);
                t = 0.5f + (PlacementRandom.Value(buildSeed, MeshSalt, ex, ey, 3) - 0.5f) * 2f * r;
            }
            return Vector2.Lerp(pa, pb, t);
        }

        private static bool SharedEdgeOrganic(TileGrid g, int ax, int ay, int bx, int by)
        {
            if (ay == by)
            {
                int x = Mathf.Min(ax, bx);
                return SquareOrganic(g, x, ay - 1) && SquareOrganic(g, x, ay);
            }
            int y = Mathf.Min(ay, by);
            return SquareOrganic(g, ax - 1, y) && SquareOrganic(g, ax, y);
        }

        /// <summary>The mesher's rule: a square of four cell centres is natural when every open corner is a plain cave cell.</summary>
        private static bool SquareOrganic(TileGrid g, int sx, int sy)
        {
            bool anyOpen = false;
            for (int k = 0; k < 4; k++)
            {
                int x = sx + (k == 1 || k == 2 ? 1 : 0), y = sy + (k >= 2 ? 1 : 0);
                if (!g.InBounds(x, y))
                    continue;   // outside = rock
                int i = x + y * g.Width;
                CellType type = g.Type[i];
                if (type == CellType.Link)
                    return false;   // a shaft (hole or pit)
                if (type == CellType.Solid)
                {
                    if (g.Has(i, CellFlags.Chasm) || g.Has(i, CellFlags.Roofed))
                        return false;
                    continue;
                }
                if (g.Has(i, CellFlags.Prefab) || g.Has(i, CellFlags.Roofed))
                    return false;
                anyOpen = true;
                if (!g.Has(i, CellFlags.Organic) || g.Has(i, CellFlags.NoCeiling) || type == CellType.Door)
                    return false;
            }
            return anyOpen;
        }
    }

    /// <summary>
    /// Seeded rock shapes written into <see cref="MeshBuffers"/> (smooth, welded): boulders, stalagmites and stalactites,
    /// columns, crystals and the simple solids their colliders use. The same seed always gives the same rock. Plain
    /// math: safe on worker threads.
    /// </summary>
    public static class CaveRockMesh
    {
        // ------------------------------------------------------------------ boulders

        /// <summary>
        /// A lumpy boulder: a sphere of <paramref name="radii"/> (x, y, z half sizes) round <paramref name="center"/>, rotated by
        /// <paramref name="rotation"/>, its surface pushed in and out by a few seeded bumps.
        /// </summary>
        public static void Boulder(MeshBuffers m, Vector3 center, Vector3 radii, Quaternion rotation, int seed, DungeonSurface surface, int rings = 7, int sides = 11)
        {
            rings = Mathf.Max(3, rings);
            sides = Mathf.Max(4, sides);
            var rng = new DungeonRandom((ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x51UL);
            const int Bumps = 7;
            var bumpDir = new Vector3[Bumps];
            var bumpSize = new float[Bumps];
            var bumpHeight = new float[Bumps];
            for (int i = 0; i < Bumps; i++)
            {
                var d = new Vector3(rng.Range(-1f, 1f), rng.Range(-1f, 1f), rng.Range(-1f, 1f));
                bumpDir[i] = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.up;
                bumpSize[i] = rng.Range(0.6f, 1.3f);
                bumpHeight[i] = rng.Range(-0.16f, 0.22f);
            }
            float squash = rng.Range(0.85f, 1.1f);
            var grid = new Vector3[rings + 1, sides + 1];
            for (int r = 0; r <= rings; r++)
            {
                float th = Mathf.PI * r / rings;
                for (int s = 0; s <= sides; s++)
                {
                    float ph = 2f * Mathf.PI * (s == sides ? 0 : s) / sides;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    float k = 1f;
                    for (int i = 0; i < Bumps; i++)
                    {
                        float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(bumpDir[i], d), -1f, 1f));
                        float t = 1f - angle / bumpSize[i];
                        if (t > 0f)
                            k += bumpHeight[i] * t * t * (3f - 2f * t);
                    }
                    var local = new Vector3(d.x * radii.x, d.y * radii.y * squash, d.z * radii.z) * k;
                    grid[r, s] = center + rotation * local;
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    Vector3 a = grid[r, s], b = grid[r, s + 1], c = grid[r + 1, s + 1], d = grid[r + 1, s];
                    Vector3 mid = (a + b + c + d) * 0.25f;
                    Vector3 outward = mid - center;
                    m.Triangle(a, b, c, surface, true, outward);
                    m.Triangle(a, c, d, surface, true, outward);
                }
        }

        // ------------------------------------------------------------------ spikes and columns

        /// <summary>
        /// A stalagmite (<paramref name="down"/> false: rising from <paramref name="root"/>) or a stalactite (true: hanging
        /// from it): a tapering, slightly bent cone of <paramref name="length"/> and base <paramref name="radius"/>, with a
        /// flared foot that blends into the rock it grows from.
        /// </summary>
        public static void Spike(MeshBuffers m, Vector3 root, float length, float radius, bool down, int seed, DungeonSurface surface, int sides = 8)
        {
            if (length <= 0.05f || radius <= 0.01f)
                return;
            sides = Mathf.Max(5, sides);
            var rng = new DungeonRandom((ulong)(uint)seed * 0xD6E8FEB86659FD93UL + 0x77UL);
            const int Rings = 6;
            float dirY = down ? -1f : 1f;
            var lean = new Vector2(rng.Range(-0.18f, 0.18f), rng.Range(-0.18f, 0.18f)) * length;
            float phase = rng.Range(0f, 6.28f);
            var rings = new Vector3[Rings + 2][];
            // The flared foot, a little inside the rock.
            rings[0] = Ring(root + new Vector3(0f, -0.18f * dirY, 0f), radius * 1.35f, sides, phase, 0.12f, rng);
            for (int k = 0; k <= Rings; k++)
            {
                float t = k / (float)(Rings + 1);
                float r = radius * Mathf.Pow(1f - t, 1.25f) * (k == 0 ? 1.05f : 1f);
                var c = root + new Vector3(lean.x * t * t, dirY * length * t, lean.y * t * t);
                rings[k + 1] = Ring(c, Mathf.Max(0.015f, r), sides, phase + k * 0.4f, 0.14f, rng);
            }
            var tip = root + new Vector3(lean.x, dirY * length, lean.y);
            Vector3 axis = (tip - root).normalized;
            for (int k = 0; k < rings.Length - 1; k++)
                Band(m, rings[k], rings[k + 1], root, axis, surface);
            Cap(m, rings[rings.Length - 1], tip, surface);
        }

        /// <summary>A column from the floor (<paramref name="bottom"/>) to the ceiling (<paramref name="top"/>): wide at both ends, narrower in the middle.</summary>
        public static void Column(MeshBuffers m, Vector3 bottom, Vector3 top, float radius, int seed, DungeonSurface surface, int sides = 9)
        {
            float height = top.y - bottom.y;
            if (height <= 0.5f || radius <= 0.05f)
                return;
            sides = Mathf.Max(6, sides);
            var rng = new DungeonRandom((ulong)(uint)seed * 0xBF58476D1CE4E5B9UL + 0x31UL);
            const int Rings = 9;
            float phase = rng.Range(0f, 6.28f);
            float waist = rng.Range(0.42f, 0.6f), waistAt = rng.Range(0.4f, 0.6f);
            var rings = new Vector3[Rings + 3][];
            rings[0] = Ring(bottom + Vector3.down * 0.2f, radius * 1.3f, sides, phase, 0.12f, rng);
            for (int k = 0; k <= Rings; k++)
            {
                float t = k / (float)Rings;
                float bend = Mathf.Abs(t - waistAt) / Mathf.Max(waistAt, 1f - waistAt);
                float r = radius * (waist + (1f - waist) * Mathf.Pow(bend, 1.6f));
                var c = Vector3.Lerp(bottom, top, t);
                rings[k + 1] = Ring(c, r, sides, phase + k * 0.35f, 0.12f, rng);
            }
            rings[Rings + 2] = Ring(top + Vector3.up * 0.2f, radius * 1.3f, sides, phase, 0.12f, rng);
            Vector3 axis = Vector3.up;
            for (int k = 0; k < rings.Length - 1; k++)
                Band(m, rings[k], rings[k + 1], bottom, axis, surface);
        }

        /// <summary>A crystal: a six-sided prism with a pointed end, from <paramref name="root"/> along <paramref name="direction"/> (crisp facets).</summary>
        public static void Crystal(MeshBuffers m, Vector3 root, Vector3 direction, float length, float radius, int seed, DungeonSurface surface)
        {
            if (length <= 0.02f || radius <= 0.005f || direction.sqrMagnitude < 1e-6f)
                return;
            Vector3 axis = direction.normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(axis, side);
            float twist = (seed & 1023) / 1023f * Mathf.PI;
            const int Sides = 6;
            float bodyLength = length * 0.72f;
            var bottomRing = new Vector3[Sides];
            var topRing = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float a = twist + i * Mathf.PI * 2f / Sides;
                Vector3 o = (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * radius;
                bottomRing[i] = root - axis * 0.08f + o;
                topRing[i] = root + axis * bodyLength + o * 0.92f;
            }
            Vector3 tip = root + axis * length;
            for (int i = 0; i < Sides; i++)
            {
                int j = (i + 1) % Sides;
                Vector3 outward = (bottomRing[i] + bottomRing[j]) * 0.5f - (root - axis * 0.08f);
                m.Quad(bottomRing[i], bottomRing[j], topRing[j], topRing[i], surface, false, outward);
                m.Triangle(topRing[i], topRing[j], tip, surface, false, outward + axis * radius);
            }
        }

        /// <summary>A cluster of crystals growing from <paramref name="root"/> roughly along <paramref name="normal"/>.</summary>
        public static void CrystalCluster(MeshBuffers m, Vector3 root, Vector3 normal, float size, int seed, DungeonSurface surface)
        {
            var rng = new DungeonRandom((ulong)(uint)seed * 0x94D049BB133111EBUL + 0x13UL);
            int count = rng.Range(3, 7);
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var jitter = new Vector3(rng.Range(-0.7f, 0.7f), rng.Range(-0.7f, 0.7f), rng.Range(-0.7f, 0.7f));
                Vector3 dir = (n + jitter * 0.8f).normalized;
                if (Vector3.Dot(dir, n) < 0.2f)
                    dir = (dir + n).normalized;
                float len = size * rng.Range(0.35f, 1f);
                float rad = len * rng.Range(0.12f, 0.2f);
                Vector3 at = root + new Vector3(rng.Range(-0.12f, 0.12f), rng.Range(-0.08f, 0.08f), rng.Range(-0.12f, 0.12f)) * size;
                Crystal(m, at, dir, len, rad, seed * 31 + i, surface);
            }
        }

        // ------------------------------------------------------------------ colliders

        /// <summary>An upright, closed eight-sided prism (a standing formation's collider).</summary>
        public static void Prism(MeshBuffers m, Vector3 bottom, float height, float radius, DungeonSurface surface)
        {
            const int Sides = 8;
            var lo = new Vector3[Sides];
            var hi = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                var o = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                lo[i] = bottom + o;
                hi[i] = bottom + o + Vector3.up * height;
            }
            Vector3 topCenter = bottom + Vector3.up * height;
            for (int i = 0; i < Sides; i++)
            {
                int j = (i + 1) % Sides;
                Vector3 outward = (lo[i] + lo[j]) * 0.5f - bottom;
                m.Quad(lo[i], lo[j], hi[j], hi[i], surface, false, outward);
                m.Triangle(hi[i], hi[j], topCenter, surface, false, Vector3.up);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static Vector3[] Ring(Vector3 center, float radius, int sides, float phase, float roughness, DungeonRandom rng)
        {
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = phase + i * Mathf.PI * 2f / sides;
                float r = radius * (1f + rng.Range(-roughness, roughness));
                ring[i] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            return ring;
        }

        private static void Band(MeshBuffers m, Vector3[] a, Vector3[] b, Vector3 origin, Vector3 axis, DungeonSurface surface)
        {
            int n = a.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 mid = (a[i] + a[j] + b[i] + b[j]) * 0.25f;
                Vector3 along = Vector3.Project(mid - origin, axis);
                Vector3 outward = mid - (origin + along);
                m.Quad(a[i], a[j], b[j], b[i], surface, true, outward);
            }
        }

        private static void Cap(MeshBuffers m, Vector3[] ring, Vector3 tip, DungeonSurface surface)
        {
            int n = ring.Length;
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in ring)
                center += p;
            center /= n;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 outward = (ring[i] + ring[j]) * 0.5f - center + (tip - center) * 0.5f;
                m.Triangle(ring[i], ring[j], tip, surface, true, outward);
            }
        }
    }
}
