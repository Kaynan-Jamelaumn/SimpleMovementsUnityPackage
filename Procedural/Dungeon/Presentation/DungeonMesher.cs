using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Turns a floor's grid into floor, wall and ceiling meshes - pure data, safe on a worker thread.
    ///
    /// The mesh is built over "squares" whose corners are four neighbouring cell centres. Walls always sit between an
    /// open and a solid cell, on the cell edge:
    /// <list type="bullet">
    /// <item><b>Built</b> squares are split into four quadrants (one per cell): crisp, straight walls along cell
    /// edges and square rooms.</item>
    /// <item><b>Natural</b> squares (all open corners are cave cells) use marching squares: walls cut corners
    /// diagonally, their contour points are shifted by noise (rough rock) and their middle bulges in and out, so caves
    /// look organic while staying exactly as walkable as the grid says (diagonal-only contacts never connect).</item>
    /// </list>
    /// Vertex heights are shared across squares (edge midpoints average the cells on both sides), so floors and
    /// ceilings are continuous: slopes in caves and ramps in corridors need no extra geometry. Stair wells, drop shafts
    /// and prefab rooms are left empty for their own builders.
    /// </summary>
    public static class DungeonMesher
    {
        public sealed class Options
        {
            public float CellSize = 1.5f;
            public int ChunkCells = 24;
            public bool Ceilings = true;
            /// <summary>Leave built floors / walls / ceilings / door frames to the tile-kit builder.</summary>
            public bool SkipBuiltFloors;
            public bool SkipBuiltWalls;
            public bool SkipBuiltCeilings;
            public bool SkipBuiltDoorFrames;
            public bool DoorFrames = true;
            public float TextureScale = 3f;
            /// <summary>Cave wall contour jitter, fraction of a cell (0..0.45).</summary>
            public float WallRoughness = 0.3f;
            /// <summary>Cave wall bulge (meters).</summary>
            public float WallBulge = 0.3f;
            public int Seed;
            public float DoorHeight = 2.6f;
        }

        private const float Skirt = 0.25f;

        private enum Sample : byte
        {
            Solid,
            Open,
            /// <summary>Stair well or prefab room: no geometry, no wall.</summary>
            Hole,
            /// <summary>Drop opening on the upper floor: no floor, no wall, but ceiling.</summary>
            Pit,
        }

        private sealed class Ctx
        {
            public FloorLayout Floor;
            public TileGrid G;
            public Options O;
            public float Cs;
            public Dictionary<Vector2Int, MeshBuffers> Chunks;
        }

        public static FloorMeshData Build(FloorLayout floor, Options options)
        {
            var ctx = new Ctx
            {
                Floor = floor,
                G = floor.Grid,
                O = options,
                Cs = options.CellSize,
                Chunks = new Dictionary<Vector2Int, MeshBuffers>(),
            };
            TileGrid g = ctx.G;
            for (int sy = -1; sy < g.Height; sy++)
                for (int sx = -1; sx < g.Width; sx++)
                    BuildSquare(ctx, sx, sy);

            if (options.DoorFrames)
                BuildDoorFrames(ctx);

            var data = new FloorMeshData { Floor = floor.Index };
            var keys = new List<Vector2Int>(ctx.Chunks.Keys);
            keys.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            foreach (Vector2Int k in keys)
            {
                MeshBuffers m = ctx.Chunks[k];
                if (m.IsEmpty)
                    continue;
                m.FinishNormals();
                data.Chunks.Add(m);
            }
            return data;
        }

        // ------------------------------------------------------------------ samples

        private static Sample Classify(Ctx c, int x, int y)
        {
            TileGrid g = c.G;
            if (!g.InBounds(x, y))
                return Sample.Solid;
            int i = x + y * g.Width;
            switch (g.Type[i])
            {
                case CellType.Floor:
                case CellType.Door:
                    return g.Has(i, CellFlags.Prefab) ? Sample.Hole : Sample.Open;
                case CellType.Link:
                    return g.Has(i, CellFlags.Pit) ? Sample.Pit : Sample.Hole;
                default:
                    return Sample.Solid;
            }
        }

        private static bool Organic(Ctx c, int x, int y)
        {
            return c.G.InBounds(x, y) && c.G.Has(x + y * c.G.Width, CellFlags.Organic);
        }

        /// <summary>A square is natural when every open corner is a cave cell and nothing in it is a hole or roofless.</summary>
        private static bool SquareOrganic(Ctx c, int sx, int sy)
        {
            bool anyOpen = false;
            for (int k = 0; k < 4; k++)
            {
                int x = sx + (k == 1 || k == 2 ? 1 : 0), y = sy + (k >= 2 ? 1 : 0);
                Sample s = Classify(c, x, y);
                if (s == Sample.Hole || s == Sample.Pit)
                    return false;
                if (s == Sample.Open)
                {
                    anyOpen = true;
                    int i = x + y * c.G.Width;
                    if (!c.G.Has(i, CellFlags.Organic) || c.G.Has(i, CellFlags.NoCeiling) || c.G.Type[i] == CellType.Door)
                        return false;
                }
            }
            return anyOpen;
        }

        private static Vector3 CornerPos(Ctx c, int x, int y, float height) => new Vector3((x + 0.5f) * c.Cs, height, (y + 0.5f) * c.Cs);

        private static float FloorH(Ctx c, int x, int y) => c.G.FloorHeight[x + y * c.G.Width];

        private static float CeilH(Ctx c, int x, int y) => c.G.CeilingHeight[x + y * c.G.Width];

        /// <summary>Hash noise in [0,1) for a lattice point (stable across chunks and threads).</summary>
        private static float Hash01(int seed, int a, int b, int stream) => PlacementRandom.Value(seed, 0x6D657368, a, b, stream);

        // ------------------------------------------------------------------ squares

        private static MeshBuffers ChunkFor(Ctx c, int sx, int sy)
        {
            int cs = Mathf.Max(4, c.O.ChunkCells);
            var key = new Vector2Int(Mathf.FloorToInt((sx + 1) / (float)cs), Mathf.FloorToInt((sy + 1) / (float)cs));
            if (!c.Chunks.TryGetValue(key, out MeshBuffers m))
            {
                m = new MeshBuffers { Chunk = key, TextureScale = c.O.TextureScale };
                c.Chunks[key] = m;
            }
            return m;
        }

        private static void BuildSquare(Ctx c, int sx, int sy)
        {
            // Corners in counter-clockwise order (seen from above): 0 (sx,sy), 1 (sx+1,sy), 2 (sx+1,sy+1), 3 (sx,sy+1).
            var cx = new[] { sx, sx + 1, sx + 1, sx };
            var cy = new[] { sy, sy, sy + 1, sy + 1 };
            var s = new Sample[4];
            bool anyOpen = false;
            for (int k = 0; k < 4; k++)
            {
                s[k] = Classify(c, cx[k], cy[k]);
                anyOpen |= s[k] == Sample.Open || s[k] == Sample.Pit;
            }
            if (!anyOpen)
                return;

            MeshBuffers m = ChunkFor(c, sx, sy);
            if (SquareOrganic(c, sx, sy))
                MarchingSquare(c, m, sx, sy, cx, cy, s);
            else
                BlockySquare(c, m, cx, cy, s);
        }

        /// <summary>Height at the midpoint of the sample edge between corners a and b (average of the open ones).</summary>
        private static float EdgeHeight(Ctx c, int[] cx, int[] cy, Sample[] s, int a, int b, bool ceiling)
        {
            float sum = 0f;
            int n = 0;
            if (Counts(s[a], ceiling)) { sum += ceiling ? CeilH(c, cx[a], cy[a]) : FloorH(c, cx[a], cy[a]); n++; }
            if (Counts(s[b], ceiling)) { sum += ceiling ? CeilH(c, cx[b], cy[b]) : FloorH(c, cx[b], cy[b]); n++; }
            return n > 0 ? sum / n : 0f;
        }

        private static float CenterHeight(Ctx c, int[] cx, int[] cy, Sample[] s, bool ceiling)
        {
            float sum = 0f;
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                if (!Counts(s[k], ceiling))
                    continue;
                sum += ceiling ? CeilH(c, cx[k], cy[k]) : FloorH(c, cx[k], cy[k]);
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        /// <summary>Samples that contribute to a vertex height: open cells, and pits for ceilings.</summary>
        private static bool Counts(Sample s, bool ceiling) => s == Sample.Open || (ceiling && s == Sample.Pit);

        // ------------------------------------------------------------------ built squares

        private static void BlockySquare(Ctx c, MeshBuffers m, int[] cx, int[] cy, Sample[] s)
        {
            float cs = c.Cs;
            // Square-local points (x, z): corners, edge midpoints e0..e3, centre.
            Vector2 P(int k) => new Vector2((cx[k] + 0.5f) * cs, (cy[k] + 0.5f) * cs);
            Vector2 E(int k) => (P(k) + P((k + 1) & 3)) * 0.5f;
            Vector2 M = (P(0) + P(2)) * 0.5f;

            bool[] skip = new bool[4];
            bool[] skipCeiling = new bool[4];
            for (int k = 0; k < 4; k++)
            {
                bool built = s[k] == Sample.Open && !Organic(c, cx[k], cy[k]);
                skip[k] = built && c.O.SkipBuiltFloors;
                skipCeiling[k] = built && c.O.SkipBuiltCeilings;
            }

            float mF = CenterHeight(c, cx, cy, s, false), mC = CenterHeight(c, cx, cy, s, true);
            var eF = new float[4];
            var eC = new float[4];
            for (int k = 0; k < 4; k++)
            {
                eF[k] = EdgeHeight(c, cx, cy, s, k, (k + 1) & 3, false);
                eC[k] = EdgeHeight(c, cx, cy, s, k, (k + 1) & 3, true);
            }

            // Whole square open and alike: one quad per surface.
            bool allOpen = s[0] == Sample.Open && s[1] == Sample.Open && s[2] == Sample.Open && s[3] == Sample.Open;
            bool sameStyle = allOpen && !skip[0] && !skip[1] && !skip[2] && !skip[3] &&
                             !skipCeiling[0] && !skipCeiling[1] && !skipCeiling[2] && !skipCeiling[3];
            bool organic0 = Organic(c, cx[0], cy[0]);
            for (int k = 1; k < 4 && sameStyle; k++)
                sameStyle = Organic(c, cx[k], cy[k]) == organic0;
            bool anyNoCeiling = false;
            for (int k = 0; k < 4; k++)
                anyNoCeiling |= s[k] == Sample.Open && c.G.Has(cx[k] + cy[k] * c.G.Width, CellFlags.NoCeiling);

            if (sameStyle)
            {
                DungeonSurface fs = organic0 ? DungeonSurface.CaveFloor : DungeonSurface.BuiltFloor;
                DungeonSurface ce = organic0 ? DungeonSurface.CaveCeiling : DungeonSurface.BuiltCeiling;
                var f = new Vector3[4];
                var cl = new Vector3[4];
                for (int k = 0; k < 4; k++)
                {
                    f[k] = new Vector3(P(k).x, FloorH(c, cx[k], cy[k]), P(k).y);
                    cl[k] = new Vector3(P(k).x, CeilH(c, cx[k], cy[k]), P(k).y);
                }
                m.Quad(f[0], f[1], f[2], f[3], fs, organic0, Vector3.up);
                if (c.O.Ceilings && !anyNoCeiling)
                {
                    m.Quad(cl[0], cl[1], cl[2], cl[3], ce, organic0, Vector3.down);
                    return;
                }
            }

            // Quadrants: k owns (P(k), E(k), M, E(k-1)).
            for (int k = 0; k < 4; k++)
            {
                bool pit = s[k] == Sample.Pit;
                if (s[k] != Sample.Open && !pit)
                    continue;
                bool organic = Organic(c, cx[k], cy[k]);
                int prev = (k + 3) & 3;
                Vector2 a = P(k), b = E(k), d = E(prev);
                if (!pit && !sameStyle && !skip[k])
                {
                    var fa = new Vector3(a.x, FloorH(c, cx[k], cy[k]), a.y);
                    var fb = new Vector3(b.x, eF[k], b.y);
                    var fm = new Vector3(M.x, mF, M.y);
                    var fd = new Vector3(d.x, eF[prev], d.y);
                    m.Quad(fa, fb, fm, fd, organic ? DungeonSurface.CaveFloor : DungeonSurface.BuiltFloor, organic, Vector3.up);
                }

                if (c.O.Ceilings && !skipCeiling[k] && !c.G.Has(cx[k] + cy[k] * c.G.Width, CellFlags.NoCeiling))
                {
                    var ca = new Vector3(a.x, CeilH(c, cx[k], cy[k]), a.y);
                    var cb = new Vector3(b.x, eC[k], b.y);
                    var cmm = new Vector3(M.x, mC, M.y);
                    var cd = new Vector3(d.x, eC[prev], d.y);
                    m.Quad(ca, cb, cmm, cd, organic ? DungeonSurface.CaveCeiling : DungeonSurface.BuiltCeiling, organic, Vector3.down);
                }
            }

            // Walls on the half-midlines between quadrants: segment E(k)..M separates corners k and k+1.
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) & 3;
                int open = -1;
                if (s[k] == Sample.Open && s[n] == Sample.Solid)
                    open = k;
                else if (s[n] == Sample.Open && s[k] == Sample.Solid)
                    open = n;
                if (open < 0)
                    continue;
                bool organic = Organic(c, cx[open], cy[open]);
                if (c.O.SkipBuiltWalls && !organic)
                    continue;
                Vector2 e = E(k);
                Vector2 toOpen = P(open) - (P(k) + P(n)) * 0.5f;
                var facing = new Vector3(toOpen.x, 0f, toOpen.y);
                float f0 = eF[k], f1 = mF, c0 = eC[k], c1 = mC;
                var b0 = new Vector3(e.x, f0 - Skirt, e.y);
                var b1 = new Vector3(M.x, f1 - Skirt, M.y);
                var t1 = new Vector3(M.x, c1 + Skirt, M.y);
                var t0 = new Vector3(e.x, c0 + Skirt, e.y);
                m.Quad(b0, b1, t1, t0, organic ? DungeonSurface.CaveWall : DungeonSurface.BuiltWall, false, facing);
            }
        }

        // ------------------------------------------------------------------ natural squares

        /// <summary>Contour point on the sample edge from corner a to corner b (one open, one solid), jittered for rough rock.</summary>
        private static Vector2 ContourPoint(Ctx c, int[] cx, int[] cy, Sample[] s, int a, int b)
        {
            // Canonical direction (from the lower-left sample), so both squares sharing this edge compute the same point.
            int lo = a, hi = b;
            if (cy[a] > cy[b] || (cy[a] == cy[b] && cx[a] > cx[b]))
            {
                lo = b;
                hi = a;
            }
            Vector2 pa = new Vector2((cx[lo] + 0.5f) * c.Cs, (cy[lo] + 0.5f) * c.Cs);
            Vector2 pb = new Vector2((cx[hi] + 0.5f) * c.Cs, (cy[hi] + 0.5f) * c.Cs);
            float t = 0.5f;
            if (c.O.WallRoughness > 0f && SharedEdgeOrganic(c, cx[a], cy[a], cx[b], cy[b]))
            {
                int ex = cx[a] + cx[b], ey = cy[a] + cy[b];
                float r = Mathf.Clamp(c.O.WallRoughness, 0f, 0.45f);
                t = 0.5f + (Hash01(c.O.Seed, ex, ey, 3) - 0.5f) * 2f * r;
            }
            return Vector2.Lerp(pa, pb, t);
        }

        /// <summary>Both squares sharing the sample edge (a)-(b) are natural (so the edge point may move).</summary>
        private static bool SharedEdgeOrganic(Ctx c, int ax, int ay, int bx, int by)
        {
            if (ay == by)
            {
                int x = Mathf.Min(ax, bx);
                return SquareOrganic(c, x, ay - 1) && SquareOrganic(c, x, ay);
            }
            int y = Mathf.Min(ay, by);
            return SquareOrganic(c, ax - 1, y) && SquareOrganic(c, ax, y);
        }

        private static void MarchingSquare(Ctx c, MeshBuffers m, int sx, int sy, int[] cx, int[] cy, Sample[] s)
        {
            bool o0 = s[0] == Sample.Open, o1 = s[1] == Sample.Open, o2 = s[2] == Sample.Open, o3 = s[3] == Sample.Open;
            bool saddle = (o0 && o2 && !o1 && !o3) || (o1 && o3 && !o0 && !o2);

            // Polygons of open ground (counter-clockwise), and the cuts where walls go.
            // Points carry "free": whether both squares sharing the point are natural (so the wall may bulge there -
            // a point shared with a straight built wall must stay put, or a sliver would open between them).
            var polygons = new List<List<(Vector2 p, float f, float ce, bool free)>>();
            var cuts = new List<((Vector2 p, float f, float ce, bool free) from, (Vector2 p, float f, float ce, bool free) to)>();

            (Vector2, float, float, bool) Corner(int k) => (new Vector2((cx[k] + 0.5f) * c.Cs, (cy[k] + 0.5f) * c.Cs), FloorH(c, cx[k], cy[k]), CeilH(c, cx[k], cy[k]), false);
            (Vector2, float, float, bool) Edge(int k)
            {
                int n = (k + 1) & 3;
                int open = s[k] == Sample.Open ? k : n;
                return (ContourPoint(c, cx, cy, s, k, n), FloorH(c, cx[open], cy[open]), CeilH(c, cx[open], cy[open]),
                    SharedEdgeOrganic(c, cx[k], cy[k], cx[n], cy[n]));
            }

            if (saddle)
            {
                // Diagonal contact: two separate corners (the grid doesn't connect them either).
                for (int k = 0; k < 4; k++)
                {
                    if (s[k] != Sample.Open)
                        continue;
                    int prev = (k + 3) & 3;
                    var poly = new List<(Vector2, float, float, bool)> { Corner(k), Edge(k), Edge(prev) };
                    polygons.Add(poly);
                    cuts.Add((Edge(k), Edge(prev)));
                }
            }
            else
            {
                var poly = new List<(Vector2, float, float, bool)>();
                int firstEdge = -1;
                for (int k = 0; k < 4; k++)
                {
                    int n = (k + 1) & 3;
                    if (s[k] == Sample.Open)
                        poly.Add(Corner(k));
                    if ((s[k] == Sample.Open) != (s[n] == Sample.Open))
                        poly.Add(Edge(k));
                }
                polygons.Add(poly);
                // The cut runs between the two edge points, from the one entered after an open corner.
                for (int k = 0; k < 4; k++)
                {
                    int n = (k + 1) & 3;
                    if (s[k] == Sample.Open && s[n] != Sample.Open)
                        firstEdge = k;
                }
                if (firstEdge >= 0)
                {
                    int second = -1;
                    for (int k = 0; k < 4; k++)
                    {
                        int n = (k + 1) & 3;
                        if (s[k] != Sample.Open && s[n] == Sample.Open)
                            second = k;
                    }
                    if (second >= 0)
                        cuts.Add((Edge(firstEdge), Edge(second)));
                }
            }

            foreach (var poly in polygons)
            {
                if (poly.Count < 3)
                    continue;
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    var a = poly[0];
                    var b = poly[i];
                    var d = poly[i + 1];
                    m.Triangle(new Vector3(a.p.x, a.f, a.p.y), new Vector3(d.p.x, d.f, d.p.y), new Vector3(b.p.x, b.f, b.p.y), DungeonSurface.CaveFloor, true, Vector3.up);
                    if (c.O.Ceilings)
                        m.Triangle(new Vector3(a.p.x, a.ce, a.p.y), new Vector3(b.p.x, b.ce, b.p.y), new Vector3(d.p.x, d.ce, d.p.y), DungeonSurface.CaveCeiling, true, Vector3.down);
                }
            }

            foreach (var cut in cuts)
                CaveWall(c, m, cut.from, cut.to);
        }

        /// <summary>
        /// A cave wall along a cut (open ground on the left of from->to), in two bands with a bulging middle row.
        /// </summary>
        private static void CaveWall(Ctx c, MeshBuffers m, (Vector2 p, float f, float ce, bool free) from, (Vector2 p, float f, float ce, bool free) to)
        {
            Vector2 dir = to.p - from.p;
            if (dir.sqrMagnitude < 1e-6f)
                return;
            var inward = new Vector3(-dir.y, 0f, dir.x);   // left of travel = towards the open ground

            Vector3 Bulge(Vector2 p, float y, bool free)
            {
                if (c.O.WallBulge <= 0f || !free)
                    return new Vector3(p.x, y, p.y);
                int kx = Mathf.RoundToInt(p.x * 8f), kz = Mathf.RoundToInt(p.y * 8f);
                float dx = (Hash01(c.O.Seed, kx, kz, 7) - 0.5f) * 2f * c.O.WallBulge;
                float dz = (Hash01(c.O.Seed, kx, kz, 8) - 0.5f) * 2f * c.O.WallBulge;
                return new Vector3(p.x + dx, y, p.y + dz);
            }

            float midA = Mathf.Lerp(from.f, from.ce, 0.45f), midB = Mathf.Lerp(to.f, to.ce, 0.45f);
            var b0 = new Vector3(from.p.x, from.f - Skirt, from.p.y);
            var b1 = new Vector3(to.p.x, to.f - Skirt, to.p.y);
            Vector3 m0 = Bulge(from.p, midA, from.free), m1 = Bulge(to.p, midB, to.free);
            var t0 = new Vector3(from.p.x, from.ce + Skirt, from.p.y);
            var t1 = new Vector3(to.p.x, to.ce + Skirt, to.p.y);
            m.Quad(b0, b1, m1, m0, DungeonSurface.CaveWall, true, inward);
            m.Quad(m0, m1, t1, t0, DungeonSurface.CaveWall, true, inward);
        }

        // ------------------------------------------------------------------ door frames

        private static void BuildDoorFrames(Ctx c)
        {
            TileGrid g = c.G;
            float cs = c.Cs;
            for (int i = 0; i < g.Count; i++)
            {
                if (g.Type[i] != CellType.Door || g.Has(i, CellFlags.Secret | CellFlags.Prefab))
                    continue;
                if (c.O.SkipBuiltDoorFrames && !g.Has(i, CellFlags.Organic))
                    continue;   // the tile kit's door frame prefab takes over
                int x = i % g.Width, y = i / g.Width;
                bool passageNS = g.IsWalkable(g.Neighbor(i, 0)) && g.IsWalkable(g.Neighbor(i, 2));
                float floor = g.FloorHeight[i];
                float ceiling = g.CeilingHeight[i];
                float doorTop = Mathf.Min(floor + c.O.DoorHeight, ceiling - 0.15f);
                float post = Mathf.Min(0.22f, cs * 0.15f);
                float depth = Mathf.Min(0.35f, cs * 0.3f);
                Vector3 center = new Vector3((x + 0.5f) * cs, floor, (y + 0.5f) * cs);
                MeshBuffers m = ChunkFor(c, x, y);

                // Posts against the side walls, a lintel, and a header up to the ceiling.
                if (passageNS)
                {
                    float x0 = x * cs, x1 = (x + 1) * cs, z0 = center.z - depth * 0.5f, z1 = center.z + depth * 0.5f;
                    m.Box(new Vector3(x0, floor - 0.05f, z0), new Vector3(x0 + post, doorTop, z1), DungeonSurface.Trim);
                    m.Box(new Vector3(x1 - post, floor - 0.05f, z0), new Vector3(x1, doorTop, z1), DungeonSurface.Trim);
                    m.Box(new Vector3(x0, doorTop, z0), new Vector3(x1, ceiling + Skirt, z1), DungeonSurface.Trim, true);
                }
                else
                {
                    float z0 = y * cs, z1 = (y + 1) * cs, x0 = center.x - depth * 0.5f, x1 = center.x + depth * 0.5f;
                    m.Box(new Vector3(x0, floor - 0.05f, z0), new Vector3(x1, doorTop, z0 + post), DungeonSurface.Trim);
                    m.Box(new Vector3(x0, floor - 0.05f, z1 - post), new Vector3(x1, doorTop, z1), DungeonSurface.Trim);
                    m.Box(new Vector3(x0, doorTop, z0), new Vector3(x1, ceiling + Skirt, z1), DungeonSurface.Trim, true);
                }
            }
        }
    }
}
