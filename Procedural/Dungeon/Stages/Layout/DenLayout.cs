using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A dragon's den: one huge cavern filling a good part of the floor (a noisy ellipse, its ceiling the tallest in the
    /// dungeon, tagged "Den" for the hoard's gold and bones, suggested for the boss), with side tunnels leading out to side
    /// caves, a few of which also join each other. The anchor rooms become caves too and are joined by tunnels.
    /// </summary>
    public sealed class DenLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            DenSettings ds = ctx.Profile.Den ?? new DenSettings();
            TileGrid g = floor.Grid;
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            foreach (Area a in floor.Areas)
                AnchorAreas.SetStyle(floor, a, ZoneStyle.Cavern);

            // The great cavern: an ellipse following the footprint's shape, its outline bent by noise.
            float share = ds.caveShare.Lerp(spec.Openness);
            float areaCells = fp.width * fp.height * share;
            float aspect = fp.width / (float)Mathf.Max(1, fp.height);
            float ry = Mathf.Sqrt(areaCells / (Mathf.PI * aspect));
            float rx = ry * aspect;
            rx = Mathf.Min(rx, fp.width * 0.5f - 3f);
            ry = Mathf.Min(ry, fp.height * 0.5f - 3f);
            var center = new Vector2(fp.x + fp.width * 0.5f + rng.Range(-2f, 2f), fp.y + fp.height * 0.5f + rng.Range(-2f, 2f));
            int seed = ctx.Seed, salt = DungeonRandom.Salt("Den") + floor.Index * 131;
            var cells = new List<int>();
            for (int y = fp.yMin + 2; y < fp.yMax - 2; y++)
                for (int x = fp.xMin + 2; x < fp.xMax - 2; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - center;
                    float angle = Mathf.Atan2(p.y, p.x);
                    float bend = 0.82f + 0.3f * PlacementRandom.Noise(Mathf.Cos(angle) * 1.6f + 7f, Mathf.Sin(angle) * 1.6f + 7f, seed, salt, 2);
                    float e = (p.x * p.x) / (rx * rx) + (p.y * p.y) / (ry * ry);
                    int i = g.Index(x, y);
                    if (e <= bend * bend && !NearFixed(floor, g, i))
                        cells.Add(i);
                }
            Area den = LayoutUtil.StampCells(floor, cells, AreaKind.Cavern, ZoneStyle.Cavern);
            if (den == null)
            {
                CaveField.Run(ctx, floor, rng, LayoutUtil.FootprintMask(floor));
                return;
            }
            LayoutUtil.KeepLargestPiece(floor, den);
            den.Tag = "Den";
            den.Hint = AreaRole.Boss;
            den.CeilingHeight = ds.caveHeight;

            // Side tunnels out to side caves.
            int count = ds.sideTunnels.Random(rng);
            float start = rng.Range(0f, Mathf.PI * 2f);
            var sides = new List<Area>();
            for (int t = 0; t < count; t++)
            {
                float angle = start + t * Mathf.PI * 2f / count + rng.Range(-0.3f, 0.3f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float size = ds.sideCaveSize.Random(rng) * 0.5f;
                Area cave = null;
                for (float reach = 1.25f; reach <= 2.2f && cave == null; reach += 0.15f)
                {
                    var c = center + new Vector2(dir.x * rx, dir.y * ry) * reach;
                    if (c.x - size < fp.xMin + 2 || c.y - size < fp.yMin + 2 || c.x + size > fp.xMax - 2 || c.y + size > fp.yMax - 2)
                        continue;
                    var disc = new List<int>();
                    bool clear = true;
                    foreach (int i in LayoutUtil.Disc(g, c, size, 0.35f, seed, salt + t * 17 + 1))
                    {
                        if (g.Type[i] != CellType.Solid)
                        {
                            clear = false;
                            break;
                        }
                        if (!NearFixed(floor, g, i) && !NearOpen(g, i))
                            disc.Add(i);
                    }
                    if (!clear || disc.Count < 12)
                        continue;
                    cave = LayoutUtil.StampCells(floor, disc, AreaKind.Cavern, ZoneStyle.Cavern);
                }
                if (cave == null)
                    continue;
                sides.Add(cave);
                LayoutUtil.Preset(floor, den, cave);
            }
            // A few side caves are joined to each other too.
            for (int i = 0; i + 1 < sides.Count; i++)
                if (rng.Chance(0.35f))
                    LayoutUtil.Preset(floor, sides[i], sides[i + 1]);
        }

        /// <summary>Within two cells of an anchor room: keep rock around it (it gets a tunnel instead).</summary>
        private static bool NearFixed(FloorLayout floor, TileGrid g, int i)
        {
            int x = g.X(i), y = g.Y(i);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!g.InBounds(nx, ny))
                        continue;
                    int n = g.Index(nx, ny);
                    if (g.Type[n] == CellType.Link)
                        return true;
                    int a = g.Area[n];
                    if (a >= 0 && floor.Areas[a].Fixed)
                        return true;
                }
            return false;
        }

        private static bool NearOpen(TileGrid g, int i)
        {
            int x = g.X(i), y = g.Y(i);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (g.InBounds(nx, ny) && g.Type[g.Index(nx, ny)] != CellType.Solid)
                        return true;
                }
            return false;
        }
    }
}
