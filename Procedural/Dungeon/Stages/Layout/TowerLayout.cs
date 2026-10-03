using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A round tower floor. In the middle stands the stair core: the spiral staircase's shaft (in a tower dungeon) or a
    /// solid core (a Tower floor among other styles, whose stairs are elsewhere). Around it either one great round hall
    /// with a ring of columns, or a ring hall hugging the core with chambers between it and the tower's round outer wall
    /// - wedges separated by radial walls, each with a door onto the ring, some also joined to their neighbours. Turrets
    /// may stand in the footprint's corners outside the round wall. Complexity adds chambers.
    /// </summary>
    public sealed class TowerLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            TowerSettings ts = ctx.Profile.Tower ?? new TowerSettings();
            TileGrid g = floor.Grid;
            RectInt fp = floor.Spec.Footprint;
            float radius = Mathf.Min(fp.width, fp.height) * 0.5f - 1.5f;

            // The stair core: a spiral shaft running through the floor, or a solid core in the middle.
            RectInt core = default;
            bool spiral = false;
            foreach (Anchor a in floor.Anchors)
            {
                if (a.Shaft.width <= 0 || a.LinkId < 0 || ctx.Layout.Links[a.LinkId].Kind != LinkKind.Spiral)
                    continue;
                core = a.Shaft;
                spiral = true;
            }
            int k = TowerPlanner.CoreSize(ctx.Profile);
            if (!spiral)
                core = new RectInt(fp.x + (fp.width - k) / 2, fp.y + (fp.height - k) / 2, k, k);
            var center = new Vector2(core.x + core.width * 0.5f, core.y + core.height * 0.5f);
            var coreWall = new RectInt(core.x - 1, core.y - 1, core.width + 2, core.height + 2);
            if (!spiral)
                foreach (Vector2Int c in coreWall.allPositionsWithin)
                    if (g.InBounds(c) && g.Type[g.Index(c)] == CellType.Solid)
                        g.Set(g.Index(c), CellFlags.Reserved);
            float coreReach = coreWall.width * 0.5f * 1.4142f;

            float Dist(int i) => (g.Center(i) - center).magnitude;
            bool Free(int i) => g.Type[i] == CellType.Solid && g.IsCarvable(i) && !coreWall.Contains(g.Coords(i));

            var halls = new List<Area>();
            bool greatHall = rng.Chance(ts.greatHallChance) || radius < coreReach + 6f;
            if (greatHall)
            {
                // One great round hall with a ring of columns.
                float columns = (coreReach + radius) * 0.5f;
                int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * columns / 4f));
                var pillars = new HashSet<int>();
                for (int c = 0; c < count; c++)
                {
                    float angle = c * Mathf.PI * 2f / count;
                    var p = new Vector2Int(Mathf.FloorToInt(center.x + Mathf.Cos(angle) * columns), Mathf.FloorToInt(center.y + Mathf.Sin(angle) * columns));
                    if (g.InBounds(p))
                        pillars.Add(g.Index(p));
                }
                var cells = new List<int>();
                for (int y = fp.yMin; y < fp.yMax; y++)
                    for (int x = fp.xMin; x < fp.xMax; x++)
                    {
                        int i = g.Index(x, y);
                        if (Free(i) && Dist(i) <= radius && !pillars.Contains(i))
                            cells.Add(i);
                    }
                foreach (int pi in pillars)
                    if (g.Type[pi] == CellType.Solid)
                        g.Set(pi, CellFlags.Pillar | CellFlags.Reserved);
                Area hall = LayoutUtil.StampCells(floor, cells, AreaKind.Hall, ZoneStyle.Built);
                if (hall != null)
                {
                    halls.Add(hall);
                    if (floor.Spec.IsLast)
                        hall.Hint = AreaRole.Boss;
                }
            }
            else
            {
                // A ring hall hugging the core...
                float ringOut = coreReach + ts.ringWidth.Random(rng);
                var ringCells = new List<int>();
                for (int y = fp.yMin; y < fp.yMax; y++)
                    for (int x = fp.xMin; x < fp.xMax; x++)
                    {
                        int i = g.Index(x, y);
                        if (Free(i) && Dist(i) <= ringOut)
                            ringCells.Add(i);
                    }
                Area ring = LayoutUtil.StampCells(floor, ringCells, AreaKind.Corridor, ZoneStyle.Built);

                // ...and chambers between it and the outer wall, separated by radial walls.
                float inner = ringOut + 1.05f;
                float midRadius = (inner + radius) * 0.5f;
                int most = Mathf.Max(3, Mathf.FloorToInt(2f * Mathf.PI * midRadius / 5f));
                int count = Mathf.Clamp(ts.chambers.Lerp(floor.Spec.Complexity) + rng.Range(-1, 2), 3, most);
                float step = Mathf.PI * 2f / count;
                float start = rng.Range(0f, Mathf.PI * 2f);
                var sectors = new List<int>[count];
                for (int c = 0; c < count; c++)
                    sectors[c] = new List<int>();
                for (int y = fp.yMin; y < fp.yMax; y++)
                    for (int x = fp.xMin; x < fp.xMax; x++)
                    {
                        int i = g.Index(x, y);
                        float d = Dist(i);
                        if (!Free(i) || d < inner || d > radius)
                            continue;
                        Vector2 v = g.Center(i) - center;
                        float a = Mathf.Repeat(Mathf.Atan2(v.y, v.x) - start, Mathf.PI * 2f);
                        int sector = Mathf.Min(count - 1, Mathf.FloorToInt(a / step));
                        float toWall = Mathf.Min(a - sector * step, (sector + 1) * step - a) * d;
                        if (toWall < 0.6f)
                            continue;   // the radial wall between two chambers
                        sectors[sector].Add(i);
                    }
                var chambers = new List<Area>();
                foreach (List<int> cells in sectors)
                {
                    if (cells.Count < 6)
                    {
                        chambers.Add(null);
                        continue;
                    }
                    Area chamber = LayoutUtil.StampCells(floor, cells, cells.Count >= 110 ? AreaKind.Hall : AreaKind.Room, ZoneStyle.Built);
                    chambers.Add(chamber);
                    if (chamber != null)
                        halls.Add(chamber);
                    if (ring != null)
                        LayoutUtil.Preset(floor, ring, chamber);
                }
                // Some neighbouring chambers open into each other (loops round the tower).
                for (int c = 0; c < chambers.Count; c++)
                {
                    Area a = chambers[c], b = chambers[(c + 1) % chambers.Count];
                    if (a != null && b != null && a != b && rng.Chance(0.25f + 0.35f * floor.Spec.Openness))
                        LayoutUtil.Preset(floor, a, b);
                }
                if (ring == null && chambers.Count > 1)
                    for (int c = 0; c + 1 < chambers.Count; c++)
                        LayoutUtil.Preset(floor, chambers[c], chambers[c + 1]);
            }

            // Turrets in the footprint's corners, outside the round wall.
            for (int corner = 0; corner < 4; corner++)
            {
                if (!rng.Chance(ts.balconyChance))
                    continue;
                float tr = rng.Range(2f, 3.2f);
                var tc = new Vector2((corner & 1) == 0 ? fp.xMin + 1.5f + tr : fp.xMax - 1.5f - tr,
                                     (corner & 2) == 0 ? fp.yMin + 1.5f + tr : fp.yMax - 1.5f - tr);
                if ((tc - center).magnitude - tr < radius + 1.5f)
                    continue;
                var cells = new List<int>();
                foreach (int i in LayoutUtil.Disc(g, tc, tr, 0f, 0, 0))
                {
                    if (!Free(i) || !fp.Contains(g.Coords(i)) || Dist(i) < radius + 1.5f || TouchesOpen(g, i))
                        continue;
                    cells.Add(i);
                }
                if (cells.Count < 8)
                    continue;
                Area turret = LayoutUtil.StampCells(floor, cells, AreaKind.Room, ZoneStyle.Built);
                Area nearest = null;
                float best = float.MaxValue;
                foreach (Area h in halls)
                {
                    float d = (h.Center - tc).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = h;
                    }
                }
                LayoutUtil.Preset(floor, turret, nearest);
            }
        }

        /// <summary>An open cell (or one of another area) next to this one: keeps a wall between turrets and the tower.</summary>
        private static bool TouchesOpen(TileGrid g, int i)
        {
            for (int d = 0; d < 4; d++)
            {
                int nb = g.Neighbor(i, d);
                if (nb >= 0 && g.Type[nb] != CellType.Solid)
                    return true;
            }
            return false;
        }
    }
}
