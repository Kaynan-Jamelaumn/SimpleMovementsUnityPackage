using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Mixed floors: the footprint is split into zones (a Voronoi diagram of jittered points, with noise-warped
    /// borders - the same trick the world uses for biomes), each zone built, natural cavern or ruins. Caves are grown in
    /// the cavern zones, rooms scattered in the built zones (ruined, eroded rooms in the ruins zones); the
    /// Connectivity stage then stitches them together with corridors, tunnels and rough breaches.
    /// </summary>
    public sealed class HybridLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            HybridSettings hs = ctx.Profile.Hybrid;
            FloorSpec spec = floor.Spec;
            TileGrid g = floor.Grid;
            RectInt fp = spec.Footprint;
            int seed = ctx.Seed;
            int salt = DungeonRandom.Salt("HybridZones") + floor.Index * 131;

            // Zone points on a jittered grid.
            float step = Mathf.Max(8f, hs.zoneSize);
            int nx = Mathf.Max(1, Mathf.RoundToInt(fp.width / step));
            int ny = Mathf.Max(1, Mathf.RoundToInt(fp.height / step));
            float sx = fp.width / (float)nx, sy = fp.height / (float)ny;
            var points = new List<Vector2>();
            var styles = new List<ZoneStyle>();
            float cavernWeight = spec.CavernWeight * (0.7f + 0.6f * spec.Openness);
            var weights = new[] { spec.BuiltWeight, cavernWeight, spec.RuinsWeight };
            for (int gy = 0; gy < ny; gy++)
            {
                for (int gx = 0; gx < nx; gx++)
                {
                    points.Add(new Vector2(fp.x + (gx + 0.5f + rng.Range(-0.35f, 0.35f)) * sx, fp.y + (gy + 0.5f + rng.Range(-0.35f, 0.35f)) * sy));
                    int pick = rng.WeightedIndex(weights);
                    styles.Add(pick < 0 ? ZoneStyle.Built : (ZoneStyle)pick);
                }
            }

            // A hybrid floor should actually mix: make sure there's at least one built and one natural zone.
            if (points.Count >= 2)
            {
                if (!styles.Contains(ZoneStyle.Cavern))
                    styles[rng.Range(0, styles.Count)] = ZoneStyle.Cavern;
                if (!styles.Contains(ZoneStyle.Built) && !styles.Contains(ZoneStyle.Ruins))
                {
                    int i = rng.Range(0, styles.Count);
                    styles[i] = ZoneStyle.Built;
                    if (!styles.Contains(ZoneStyle.Cavern))
                        styles[(i + 1) % styles.Count] = ZoneStyle.Cavern;
                }
            }

            // Zone per cell: nearest point, with warped coordinates for irregular borders.
            var zone = new int[g.Count];
            for (int y = 0; y < g.Height; y++)
            {
                for (int x = 0; x < g.Width; x++)
                {
                    float wx = x + (PlacementRandom.Noise(x * 0.05f, y * 0.05f, seed, salt, 2) - 0.5f) * 2f * hs.borderNoise;
                    float wy = y + (PlacementRandom.Noise(x * 0.05f, y * 0.05f, seed, salt + 1, 2) - 0.5f) * 2f * hs.borderNoise;
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int p = 0; p < points.Count; p++)
                    {
                        float dx = points[p].x - wx, dy = points[p].y - wy;
                        float d = dx * dx + dy * dy;
                        if (d < bestD)
                        {
                            bestD = d;
                            best = p;
                        }
                    }
                    zone[x + y * g.Width] = best;
                }
            }
            floor.Zone = zone;
            floor.ZoneStyles = styles;

            ZoneStyle StyleAt(Vector2Int c) => g.InBounds(c) ? styles[zone[g.Index(c)]] : ZoneStyle.Built;

            foreach (Area a in floor.Areas)
                AnchorAreas.SetStyle(floor, a, StyleAt(Vector2Int.FloorToInt(a.Center)));

            bool[] footprint = LayoutUtil.FootprintMask(floor);
            var caveMask = new bool[g.Count];
            var roomMask = new bool[g.Count];
            for (int i = 0; i < g.Count; i++)
            {
                if (!footprint[i])
                    continue;
                if (styles[zone[i]] == ZoneStyle.Cavern)
                    caveMask[i] = true;
                else
                    roomMask[i] = true;
            }

            CaveField.Run(ctx, floor, rng, caveMask);
            RoomScatterLayout.Scatter(ctx, floor, rng, roomMask, StyleAt, 1.1f);
        }
    }
}
