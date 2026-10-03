using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// An organic hive: round cells on a honeycomb (hexagonal) lattice, their outlines wobbling, a few missing (solid) and
    /// a few merged with a neighbour into big brood chambers. Neighbouring cells are joined by fleshy tunnels (each pair
    /// with a chance; the Connectivity stage adds the rest needed). Everything is natural rock (organic meshing); the
    /// anchor rooms become part of the hive. Openness makes the cells bigger and more joined; Complexity leaves more gaps.
    /// </summary>
    public sealed class HiveLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            HiveSettings hs = ctx.Profile.Hive ?? new HiveSettings();
            TileGrid g = floor.Grid;
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            foreach (Area a in floor.Areas)
                AnchorAreas.SetStyle(floor, a, ZoneStyle.Cavern);

            float r = hs.cellRadius.Lerp(spec.Openness);
            int wall = Mathf.Max(1, hs.wall.Random(rng));
            float pitch = 2f * r + wall + 1f;
            float rowStep = pitch * 0.8660254f;
            float missing = Mathf.Lerp(hs.missingChance * 0.5f, hs.missingChance, spec.Complexity);

            // The honeycomb's centres.
            var centers = new List<Vector2>();
            var keys = new List<Vector2Int>();
            int row = 0;
            for (float y = fp.yMin + r + 2f; y <= fp.yMax - r - 2f; y += rowStep, row++)
            {
                int col = 0;
                for (float x = fp.xMin + r + 2f + ((row & 1) == 1 ? pitch * 0.5f : 0f); x <= fp.xMax - r - 2f; x += pitch, col++)
                {
                    if (rng.Chance(missing))
                        continue;
                    centers.Add(new Vector2(x, y));
                    keys.Add(new Vector2Int(col, row));
                }
            }

            // Cells (wobbly discs), stamped as cavern chambers.
            var cellsOf = new Area[centers.Count];
            for (int c = 0; c < centers.Count; c++)
            {
                float radius = r * rng.Range(0.85f, 1.1f);
                var cells = new List<int>();
                foreach (int i in LayoutUtil.Disc(g, centers[c], radius, hs.wobble * 0.35f, ctx.Seed, DungeonRandom.Salt("HiveCell") + floor.Index * 977 + c))
                    if (fp.Contains(g.Coords(i)) && !NearOtherOpen(g, i, -1))
                        cells.Add(i);
                if (cells.Count < 8)
                    continue;
                cellsOf[c] = LayoutUtil.StampCells(floor, cells, AreaKind.Cavern, ZoneStyle.Cavern);
                if (cellsOf[c] != null)
                    cellsOf[c].Tag = "Hive";
            }

            // Brood chambers: merge a cell into a neighbour through the wall between them.
            for (int c = 0; c < centers.Count; c++)
            {
                if (cellsOf[c] == null || !rng.Chance(hs.broodChance))
                    continue;
                int other = Nearest(centers, cellsOf, c, pitch * 1.2f, rng);
                if (other < 0)
                    continue;
                Area keep = cellsOf[c], gone = cellsOf[other];
                if (keep == gone || gone.Fixed)
                    continue;
                foreach (int i in gone.Cells)
                    g.Area[i] = keep.Id;
                // Open the wall between the two centres.
                foreach (int i in Line(g, centers[c], centers[other], 1.4f))
                {
                    if (g.Type[i] == CellType.Solid && g.IsCarvable(i))
                    {
                        g.SetFloor(i, keep.Id, true);
                    }
                }
                gone.Cells.Clear();
                for (int k = 0; k < cellsOf.Length; k++)
                    if (cellsOf[k] == gone)
                        cellsOf[k] = keep;
                keep.Kind = AreaKind.Hall;
                keep.Tag = "Brood";
                LayoutUtil.RebuildCells(floor, keep);
            }

            // Tunnels between neighbours (the honeycomb's edges), each with a chance.
            float join = hs.joinChance.Lerp(spec.Openness);
            for (int a = 0; a < centers.Count; a++)
                for (int b = a + 1; b < centers.Count; b++)
                {
                    if (cellsOf[a] == null || cellsOf[b] == null || cellsOf[a] == cellsOf[b])
                        continue;
                    if ((centers[a] - centers[b]).magnitude > pitch * 1.15f || !rng.Chance(join))
                        continue;
                    LayoutUtil.Preset(floor, cellsOf[a], cellsOf[b]);
                }
        }

        /// <summary>Another area's open cell within one cell (keeps a wall between hive cells and the anchors).</summary>
        private static bool NearOtherOpen(TileGrid g, int i, int self)
        {
            int x = g.X(i), y = g.Y(i);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!g.InBounds(nx, ny))
                        continue;
                    int n = g.Index(nx, ny);
                    if (g.Type[n] != CellType.Solid && g.Area[n] != self)
                        return true;
                }
            return false;
        }

        private static int Nearest(List<Vector2> centers, Area[] cellsOf, int c, float within, DungeonRandom rng)
        {
            var options = new List<int>();
            for (int o = 0; o < centers.Count; o++)
                if (o != c && cellsOf[o] != null && cellsOf[o] != cellsOf[c] && (centers[o] - centers[c]).magnitude <= within)
                    options.Add(o);
            return options.Count > 0 ? options[rng.Range(0, options.Count)] : -1;
        }

        /// <summary>Cells within <paramref name="halfWidth"/> of the segment a-b.</summary>
        public static List<int> Line(TileGrid g, Vector2 a, Vector2 b, float halfWidth)
        {
            var list = new List<int>();
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - halfWidth - 1), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + halfWidth + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - halfWidth - 1), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + halfWidth + 1);
            Vector2 ab = b - a;
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!g.InBounds(x, y))
                        continue;
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    if ((a + ab * t - p).magnitude <= halfWidth)
                        list.Add(g.Index(x, y));
                }
            return list;
        }
    }
}
