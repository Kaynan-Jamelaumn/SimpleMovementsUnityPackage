using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// A city in a huge cavern. Streets run in a grid (one wider avenue each way, a street along the cavern wall), some
    /// crossings open into plazas, and the blocks between the streets are split into lots: buildings of one to three
    /// rooms behind one-cell walls, each with a door onto a street - or, now and then, a collapsed ruin open to the
    /// street. Streets and plazas are outdoor (the cavern's rock ceiling high above, no doors); buildings are roofed: their
    /// walls stop at a roof and the open cavern continues above them. Complexity makes the blocks smaller.
    /// </summary>
    public sealed class UndercityLayout : ILayoutStrategy
    {
        /// <summary>The cavern's sky (meters above the floor's base) over a cell, the same for streets and roofs.</summary>
        public static float SkyAt(CompiledProfile p, int seed, int floor, int x, int y)
        {
            UndercitySettings us = p.Undercity ?? new UndercitySettings();
            float scale = Mathf.Max(0.1f, (p.Ceilings ?? new CeilingSettings()).heightScale);
            float n = PlacementRandom.Noise(x * 0.045f, y * 0.045f, seed, DungeonRandom.Salt("UndercitySky") + floor * 53, 2);
            return us.skyHeight.Lerp(n) * scale;
        }

        private struct Line
        {
            public int Start, Width;
            public int End => Start + Width;
        }

        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            CompiledProfile p = ctx.Profile;
            UndercitySettings us = p.Undercity ?? new UndercitySettings();
            TileGrid g = floor.Grid;
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            g.EnsureRoofs();
            float scale = Mathf.Max(0.1f, (p.Ceilings ?? new CeilingSettings()).heightScale);
            int seed = ctx.Seed;
            float maxCeiling = spec.MaxCeiling;
            float Sky(int i) => Mathf.Min(SkyAt(p, seed, floor.Index, g.X(i), g.Y(i)), maxCeiling);

            var city = new RectInt(fp.x + 2, fp.y + 2, fp.width - 4, fp.height - 4);
            int block = Mathf.Max(7, us.blockSize.Lerp(1f - spec.Complexity));
            int street = Mathf.Max(2, us.streetWidth.Random(rng));
            int avenue = Mathf.Max(street, us.avenueWidth.Random(rng));
            List<Line> cols = Lines(city.xMin, city.xMax, block, street, avenue, rng);
            List<Line> rows = Lines(city.yMin, city.yMax, block, street, avenue, rng);

            int ColAt(int x)
            {
                for (int k = 0; k < cols.Count; k++)
                    if (x >= cols[k].Start && x < cols[k].End)
                        return k;
                return -1;
            }
            int RowAt(int y)
            {
                for (int k = 0; k < rows.Count; k++)
                    if (y >= rows[k].Start && y < rows[k].End)
                        return k;
                return -1;
            }
            // Which block interval (between lines) a coordinate falls in.
            int Interval(List<Line> lines, int v)
            {
                int k = 0;
                while (k < lines.Count && v >= lines[k].End)
                    k++;
                return k;
            }

            // Plazas at some crossings.
            var plazas = new List<RectInt>();
            foreach (Line c in cols)
                foreach (Line r in rows)
                {
                    if (!rng.Chance(us.plazaChance))
                        continue;
                    int size = us.plazaSize.Random(rng);
                    var rect = new RectInt(c.Start + c.Width / 2 - size / 2, r.Start + r.Width / 2 - size / 2, size, size);
                    rect = Clip(rect, city);
                    if (rect.width >= 5 && rect.height >= 5)
                        plazas.Add(rect);
                }
            int PlazaAt(int x, int y)
            {
                for (int k = 0; k < plazas.Count; k++)
                    if (plazas[k].Contains(new Vector2Int(x, y)))
                        return k;
                return -1;
            }

            // 1. Streets, crossings and plazas (outdoor).
            var groups = new Dictionary<long, List<int>>();
            var plazaCells = new List<int>[plazas.Count];
            for (int k = 0; k < plazas.Count; k++)
                plazaCells[k] = new List<int>();
            for (int y = city.yMin; y < city.yMax; y++)
                for (int x = city.xMin; x < city.xMax; x++)
                {
                    int col = ColAt(x), row = RowAt(y);
                    int plaza = PlazaAt(x, y);
                    if (col < 0 && row < 0 && plaza < 0)
                        continue;
                    int i = g.Index(x, y);
                    if (plaza >= 0)
                    {
                        plazaCells[plaza].Add(i);
                        continue;
                    }
                    long key;
                    if (col >= 0 && row >= 0)
                        key = (1L << 40) | ((long)col << 20) | (uint)row;                 // a crossing
                    else if (col >= 0)
                        key = (2L << 40) | ((long)col << 20) | (uint)Interval(rows, y);    // a street running north-south
                    else
                        key = (3L << 40) | ((long)row << 20) | (uint)Interval(cols, x);    // a street running east-west
                    if (!groups.TryGetValue(key, out List<int> list))
                        groups[key] = list = new List<int>();
                    list.Add(i);
                }
            var keys = new List<long>(groups.Keys);
            keys.Sort();
            foreach (long key in keys)
            {
                Area s = LayoutUtil.StampCells(floor, groups[key], AreaKind.Corridor, ZoneStyle.Built, CellFlags.Outdoor);
                if (s == null)
                    continue;
                LayoutUtil.KeepLargestPiece(floor, s);
                s.Tag = "Street";
            }
            Area biggestPlaza = null;
            foreach (List<int> cells in plazaCells)
            {
                Area pz = LayoutUtil.StampCells(floor, cells, AreaKind.Hall, ZoneStyle.Built, CellFlags.Outdoor);
                if (pz == null)
                    continue;
                LayoutUtil.KeepLargestPiece(floor, pz);
                pz.Tag = "Plaza";
                pz.Hint = AreaRole.Arena;
                if (biggestPlaza == null || pz.Cells.Count > biggestPlaza.Cells.Count)
                    biggestPlaza = pz;
            }
            if (biggestPlaza != null && spec.IsLast)
                biggestPlaza.Hint = AreaRole.Boss;

            // 2. Blocks between the streets, split into lots: buildings and ruins.
            var xs = Intervals(cols, city.xMin, city.xMax);
            var ys = Intervals(rows, city.yMin, city.yMax);
            int building = 0;
            foreach (Vector2Int bx in xs)
                foreach (Vector2Int by in ys)
                {
                    var b = new RectInt(bx.x, by.x, bx.y - bx.x, by.y - by.x);
                    if (b.width < 4 || b.height < 4)
                        continue;
                    bool alongX = b.width >= b.height;
                    int length = alongX ? b.width : b.height;
                    int lots = Mathf.Clamp(length / rng.Range(6, 9), 1, 3);
                    // Cut positions: shared one-cell walls between lots.
                    var cuts = new List<int> { 0 };
                    for (int k = 1; k < lots; k++)
                        cuts.Add(Mathf.Clamp(k * length / lots + rng.Range(-1, 2), cuts[cuts.Count - 1] + 5, length - 5));
                    cuts.Add(length - 1);
                    for (int k = 0; k + 1 < cuts.Count; k++)
                    {
                        int a0 = cuts[k], a1 = cuts[k + 1];
                        if (a1 - a0 < 4)
                            continue;
                        RectInt lot = alongX ? new RectInt(b.x + a0, b.y, a1 - a0 + 1, b.height) : new RectInt(b.x, b.y + a0, b.width, a1 - a0 + 1);
                        // A plaza or a stair well may bite into the lot: shrink it until it is all rock.
                        if (!Fit(g, ref lot))
                            continue;
                        if (rng.Chance(us.ruinChance))
                            Ruin(floor, lot, us, rng, Sky, scale);
                        else
                            Building(floor, lot, us, rng, Sky, scale, building++);
                    }
                }

            // 3. The anchor rooms become small roofed buildings too (their stair wells stand in them as towers).
            foreach (Area a in floor.Areas)
            {
                if (!a.Fixed)
                    continue;
                float roof = (us.interiorCeiling + 1.5f) * scale;
                foreach (int c in a.Cells)
                {
                    g.Set(c, CellFlags.Roofed);
                    g.RoofHeight[c] = Mathf.Min(roof, Sky(c) - 1f);
                    g.SkyHeight[c] = Sky(c);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = g.X(c) + dx, ny = g.Y(c) + dy;
                            if ((dx == 0 && dy == 0) || !g.InBounds(nx, ny))
                                continue;
                            int n = g.Index(nx, ny);
                            if (g.Type[n] != CellType.Solid || g.Has(n, CellFlags.Reserved | CellFlags.Roofed))
                                continue;
                            g.Set(n, CellFlags.Roofed);
                            g.RoofHeight[n] = Mathf.Min(roof, Sky(n) - 1f);
                            g.SkyHeight[n] = Sky(n);
                        }
                }
            }
        }

        // ------------------------------------------------------------------ streets

        /// <summary>Street lines across [from, to): one every block plus street, one of them an avenue.</summary>
        private static List<Line> Lines(int from, int to, int block, int street, int avenue, DungeonRandom rng)
        {
            var lines = new List<Line>();
            int count = Mathf.Max(2, (to - from) / (block + street) + 1);
            int avenueIndex = count > 2 ? rng.Range(1, count - 1) : 0;
            int pos = from;
            for (int k = 0; pos < to - 1; k++)
            {
                int w = k == avenueIndex ? avenue : street;
                w = Mathf.Min(w, to - pos);
                lines.Add(new Line { Start = pos, Width = w });
                pos += w + block + rng.Range(-2, 3);
            }
            return lines;
        }

        /// <summary>The intervals between the lines inside [from, to) as (start, end).</summary>
        private static List<Vector2Int> Intervals(List<Line> lines, int from, int to)
        {
            var list = new List<Vector2Int>();
            int pos = from;
            foreach (Line l in lines)
            {
                if (l.Start > pos)
                    list.Add(new Vector2Int(pos, l.Start));
                pos = Mathf.Max(pos, l.End);
            }
            if (pos < to)
                list.Add(new Vector2Int(pos, to));
            return list;
        }

        private static RectInt Clip(RectInt r, RectInt to)
        {
            int x0 = Mathf.Max(r.xMin, to.xMin), y0 = Mathf.Max(r.yMin, to.yMin);
            int x1 = Mathf.Min(r.xMax, to.xMax), y1 = Mathf.Min(r.yMax, to.yMax);
            return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }

        // ------------------------------------------------------------------ lots

        /// <summary>A building: walls on the lot's edge, 1-3 rooms inside separated by walls, a door onto a street.</summary>
        private static void Building(FloorLayout floor, RectInt lot, UndercitySettings us, DungeonRandom rng, System.Func<int, float> sky, float scale, int id)
        {
            TileGrid g = floor.Grid;
            var inner = new RectInt(lot.x + 1, lot.y + 1, lot.width - 2, lot.height - 2);
            if (inner.width < 3 || inner.height < 3 || !AllCarvable(g, lot))
                return;
            float roof = (us.interiorCeiling + us.roofAbove.Random(rng)) * scale;

            // Rooms: cut the inside along its longer side.
            bool alongX = inner.width >= inner.height;
            int length = alongX ? inner.width : inner.height;
            int rooms = Mathf.Clamp(us.roomsPerBuilding.Random(rng), 1, Mathf.Max(1, (length + 1) / 4));
            var cuts = new List<int>();
            for (int k = 1; k < rooms; k++)
                cuts.Add(Mathf.Clamp(k * length / rooms + rng.Range(-1, 2), (cuts.Count > 0 ? cuts[cuts.Count - 1] : -1) + 4, length - 4));
            var made = new List<Area>();
            int from = 0;
            for (int k = 0; k <= cuts.Count; k++)
            {
                int to = k < cuts.Count ? cuts[k] : length;
                var cells = new List<int>();
                for (int a = from; a < to; a++)
                {
                    int span = alongX ? inner.height : inner.width;
                    for (int s = 0; s < span; s++)
                    {
                        int x = alongX ? inner.x + a : inner.x + s, y = alongX ? inner.y + s : inner.y + a;
                        cells.Add(g.Index(x, y));
                    }
                }
                from = to + 1;
                Area room = LayoutUtil.StampCells(floor, cells, cells.Count >= 110 ? AreaKind.Hall : AreaKind.Room, ZoneStyle.Built, CellFlags.Roofed);
                if (room == null)
                    continue;
                room.Tag = "Building";
                made.Add(room);
            }
            for (int k = 0; k + 1 < made.Count; k++)
                LayoutUtil.Preset(floor, made[k], made[k + 1]);

            // Roof and sky over the whole lot (walls and rooms).
            foreach (Vector2Int c in lot.allPositionsWithin)
            {
                int i = g.Index(c);
                g.Set(i, CellFlags.Roofed);
                g.RoofHeight[i] = Mathf.Min(roof, sky(i) - 1f);
                g.SkyHeight[i] = sky(i);
            }

            // A door onto a street (or plaza) for the rooms next to one, at least one per building.
            bool any = false;
            foreach (Area room in made)
            {
                Area street = StreetBeyondWall(floor, room);
                if (street != null && (!any || rng.Chance(0.4f)))
                {
                    LayoutUtil.Preset(floor, room, street);
                    any = true;
                }
            }
        }

        /// <summary>A collapsed building: an open lot (outdoor) whose walls are broken towards the streets.</summary>
        private static void Ruin(FloorLayout floor, RectInt lot, UndercitySettings us, DungeonRandom rng, System.Func<int, float> sky, float scale)
        {
            TileGrid g = floor.Grid;
            if (!AllCarvable(g, lot))
                return;
            var cells = new List<int>();
            var walls = new List<int>();
            foreach (Vector2Int c in lot.allPositionsWithin)
            {
                bool edge = c.x == lot.xMin || c.y == lot.yMin || c.x == lot.xMax - 1 || c.y == lot.yMax - 1;
                int i = g.Index(c);
                if (!edge || rng.Chance(0.45f))
                    cells.Add(i);
                else
                    walls.Add(i);
            }
            // Never open the wall into a neighbouring lot: only edges facing a street stay broken.
            for (int k = cells.Count - 1; k >= 0; k--)
            {
                int i = cells[k];
                Vector2Int c = g.Coords(i);
                bool edge = c.x == lot.xMin || c.y == lot.yMin || c.x == lot.xMax - 1 || c.y == lot.yMax - 1;
                if (edge && !FacesStreet(g, i, lot))
                {
                    cells.RemoveAt(k);
                    walls.Add(i);
                }
            }
            Area ruin = LayoutUtil.StampCells(floor, cells, AreaKind.Room, ZoneStyle.Ruins, CellFlags.Outdoor);
            if (ruin == null)
                return;
            LayoutUtil.KeepLargestPiece(floor, ruin);
            ruin.Tag = "Ruin";
            // The broken walls are low.
            foreach (int i in walls)
            {
                if (g.Type[i] != CellType.Solid)
                    continue;
                g.Set(i, CellFlags.Roofed);
                g.RoofHeight[i] = rng.Range(1.2f, 3f) * scale;
                g.SkyHeight[i] = sky(i);
            }
        }

        /// <summary>Shrinks a lot away from cells it may not use (open cells, reserved rock); false when too little is left.</summary>
        private static bool Fit(TileGrid g, ref RectInt lot)
        {
            for (int guard = 0; guard < 16; guard++)
            {
                if (lot.width < 5 || lot.height < 5)
                    return false;
                Vector2Int bad = new Vector2Int(int.MinValue, 0);
                foreach (Vector2Int c in lot.allPositionsWithin)
                {
                    if (!g.InBounds(c))
                    {
                        bad = c;
                        break;
                    }
                    int i = g.Index(c);
                    // Rock of its own, next to nothing but streets, plazas and plain rock.
                    bool clear = g.Type[i] == CellType.Solid && g.IsCarvable(i) && !g.Has(i, CellFlags.Roofed);
                    for (int d = 0; d < 4 && clear; d++)
                    {
                        int nb = g.Neighbor(i, d);
                        if (nb < 0 || lot.Contains(g.Coords(nb)))
                            continue;
                        if (g.Type[nb] == CellType.Link || g.Has(nb, CellFlags.Reserved) ||
                            (g.Type[nb] != CellType.Solid && !g.Has(nb, CellFlags.Outdoor)))
                            clear = false;
                    }
                    if (!clear)
                    {
                        bad = c;
                        break;
                    }
                }
                if (bad.x == int.MinValue)
                    return true;
                // Cut off the side nearest to the bad cell.
                int left = bad.x - lot.xMin, right = lot.xMax - 1 - bad.x, bottom = bad.y - lot.yMin, top = lot.yMax - 1 - bad.y;
                int least = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
                if (least == left)
                    lot = new RectInt(bad.x + 1, lot.y, lot.xMax - bad.x - 1, lot.height);
                else if (least == right)
                    lot = new RectInt(lot.x, lot.y, bad.x - lot.x, lot.height);
                else if (least == bottom)
                    lot = new RectInt(lot.x, bad.y + 1, lot.width, lot.yMax - bad.y - 1);
                else
                    lot = new RectInt(lot.x, lot.y, lot.width, bad.y - lot.y);
            }
            return false;
        }

        private static bool FacesStreet(TileGrid g, int i, RectInt lot)
        {
            for (int d = 0; d < 4; d++)
            {
                int nb = g.Neighbor(i, d);
                if (nb >= 0 && !lot.Contains(g.Coords(nb)) && g.Has(nb, CellFlags.Outdoor))
                    return true;
            }
            return false;
        }

        private static bool AllCarvable(TileGrid g, RectInt r)
        {
            foreach (Vector2Int c in r.allPositionsWithin)
            {
                if (!g.InBounds(c))
                    return false;
                int i = g.Index(c);
                if (g.Type[i] != CellType.Solid || !g.IsCarvable(i))
                    return false;
            }
            return true;
        }

        /// <summary>A street or plaza area one wall away from the room (the area across its wall), or null.</summary>
        private static Area StreetBeyondWall(FloorLayout floor, Area room)
        {
            TileGrid g = floor.Grid;
            var counts = new Dictionary<int, int>();
            foreach (int c in room.Cells)
                for (int d = 0; d < 4; d++)
                {
                    int wall = g.Neighbor(c, d);
                    if (wall < 0 || g.Type[wall] != CellType.Solid)
                        continue;
                    int beyond = g.Neighbor(wall, d);
                    if (beyond < 0 || !g.Has(beyond, CellFlags.Outdoor) || g.Area[beyond] < 0)
                        continue;
                    counts.TryGetValue(g.Area[beyond], out int n);
                    counts[g.Area[beyond]] = n + 1;
                }
            int best = -1, most = 0;
            foreach (var kv in counts)
                if (kv.Value > most || (kv.Value == most && kv.Key < best))
                {
                    most = kv.Value;
                    best = kv.Key;
                }
            return best >= 0 ? floor.Areas[best] : null;
        }
    }
}
