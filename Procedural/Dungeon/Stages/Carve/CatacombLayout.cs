using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Catacomb floors: a lattice of small OSSUARY CHAMBERS (now and then a bigger crypt hall) joined by long, narrow
    /// GALLERIES that form a maze with a few loops, and lined with BURIAL NICHES - tiny rooms one wall off the gallery,
    /// joined to it by a doorway (dead ends for loot, secrets and ambushes). Galleries are areas of their own (kind
    /// Corridor): they get mobs and props, but never a special-room role. Complexity tightens the lattice and adds
    /// niches; Openness widens the galleries and adds loops.
    /// </summary>
    public sealed class CatacombLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            CatacombSettings cs = ctx.Profile.Catacombs ?? new CatacombSettings();
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            bool[] allowed = LayoutUtil.FootprintMask(floor);

            int galleryWidth = Mathf.Clamp(cs.galleryWidth.Lerp(spec.Openness), 1, 4);
            int maxChamber = Mathf.Max(cs.chamberSize.max, cs.hallChance > 0f ? cs.hallSize.max : 0);
            int pitch = Mathf.Max(maxChamber + 6, cs.pitch.Lerp(1f - spec.Complexity));
            int margin = 2 + maxChamber / 2;
            int cols = Mathf.Max(1, (fp.width - margin * 2) / pitch + 1);
            int rows = Mathf.Max(1, (fp.height - margin * 2) / pitch + 1);
            int ox = fp.x + (fp.width - (cols - 1) * pitch) / 2;
            int oy = fp.y + (fp.height - (rows - 1) * pitch) / 2;

            // 1. Chambers on the lattice (anchors take some nodes).
            var areaOf = new Area[cols * rows];
            var sideOf = new int[cols * rows];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    bool hall = rng.Chance(cs.hallChance);
                    int side = hall ? cs.hallSize.Random(rng) : cs.chamberSize.Random(rng);
                    RoomShape shape = hall
                        ? (rng.Chance(0.5f) ? RoomShape.Octagon : RoomShape.Cross)
                        : (rng.Chance(0.3f) && side >= 5 ? RoomShape.Octagon : RoomShape.Rectangle);
                    ShapeMask mask = RoomShapes.Generate(shape, side, side, rng);
                    int x = ox + i * pitch - side / 2, y = oy + j * pitch - side / 2;
                    if (!LayoutUtil.CanPlace(floor, mask, x, y, 1, allowed))
                        continue;
                    areaOf[i + j * cols] = LayoutUtil.Stamp(floor, mask, x, y, hall ? AreaKind.Hall : AreaKind.Room, ZoneStyle.Built);
                    sideOf[i + j * cols] = side;
                }
            }

            // 2. Which neighbours are joined: a maze over the chambers, plus loops.
            var edges = new List<Vector2Int>();
            var visited = new bool[cols * rows];
            var stack = new Stack<int>();
            var order = new List<int>();
            for (int b = 0; b < areaOf.Length; b++)
                if (areaOf[b] != null)
                    order.Add(b);
            rng.Shuffle(order);
            foreach (int start in order)
            {
                if (visited[start])
                    continue;
                visited[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int cur = stack.Peek();
                    var options = new List<int>(4);
                    int cx = cur % cols, cy = cur / cols;
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = cx + Dir4Util.DX[d], ny = cy + Dir4Util.DY[d];
                        if (nx < 0 || ny < 0 || nx >= cols || ny >= rows)
                            continue;
                        int nb = nx + ny * cols;
                        if (areaOf[nb] != null && !visited[nb])
                            options.Add(nb);
                    }
                    if (options.Count == 0)
                    {
                        stack.Pop();
                        continue;
                    }
                    int pick = options[rng.Range(0, options.Count)];
                    visited[pick] = true;
                    edges.Add(new Vector2Int(cur, pick));
                    stack.Push(pick);
                }
            }
            var joined = new HashSet<long>();
            foreach (Vector2Int e in edges)
                joined.Add(Key(e.x, e.y));
            float loopChance = cs.loopChance.Lerp(spec.Openness);
            for (int b = 0; b < areaOf.Length; b++)
            {
                if (areaOf[b] == null)
                    continue;
                int bx = b % cols, by = b / cols;
                for (int d = 0; d < 2; d++)
                {
                    int nx = bx + (d == 0 ? 1 : 0), ny = by + (d == 1 ? 1 : 0);
                    if (nx >= cols || ny >= rows)
                        continue;
                    int nb = nx + ny * cols;
                    if (areaOf[nb] == null || joined.Contains(Key(b, nb)) || !rng.Chance(loopChance))
                        continue;
                    edges.Add(new Vector2Int(b, nb));
                    joined.Add(Key(b, nb));
                }
            }

            // 3. A gallery along every edge (one wall away from both chambers), lined with niches.
            float nicheChance = cs.nicheChance.Lerp(spec.Complexity);
            foreach (Vector2Int e in edges)
            {
                Area a = areaOf[e.x], b = areaOf[e.y];
                bool horizontal = e.x / cols == e.y / cols;
                int lo = Mathf.Min(e.x, e.y), hi = Mathf.Max(e.x, e.y);
                Area from = areaOf[lo], to = areaOf[hi];
                int fx = ox + (lo % cols) * pitch, fy = oy + (lo / cols) * pitch;
                int hiCenter = horizontal ? ox + (hi % cols) * pitch : oy + (hi / cols) * pitch;
                // One wall cell between each chamber and the gallery: from two past the low chamber's last cell to two
                // before the high chamber's first cell.
                int start = (horizontal ? fx : fy) - sideOf[lo] / 2 + sideOf[lo] - 1 + 2;
                int end = hiCenter - sideOf[hi] / 2 - 1;
                int length = end - start;
                Area gallery = null;
                if (length >= 2)
                {
                    int w = horizontal ? length : galleryWidth, h = horizontal ? galleryWidth : length;
                    int gx = horizontal ? start : fx - galleryWidth / 2;
                    int gy = horizontal ? fy - galleryWidth / 2 : start;
                    ShapeMask mask = RoomShapes.Generate(RoomShape.Rectangle, w, h, rng);
                    if (LayoutUtil.CanPlace(floor, mask, gx, gy, 1, allowed))
                    {
                        gallery = LayoutUtil.Stamp(floor, mask, gx, gy, AreaKind.Corridor, ZoneStyle.Built);
                        gallery.Tag = "Gallery";
                        floor.PresetConnections.Add(new Vector2Int(from.Id, gallery.Id));
                        floor.PresetConnections.Add(new Vector2Int(gallery.Id, to.Id));
                        AddNiches(cs, floor, gallery, horizontal, gx, gy, w, h, nicheChance, allowed, rng);
                    }
                }
                if (gallery == null)
                    floor.PresetConnections.Add(new Vector2Int(a.Id, b.Id));   // the router digs a way round whatever is in the way
            }
        }

        /// <summary>Burial niches on both sides of a gallery, one wall away (joined by a doorway).</summary>
        private static void AddNiches(CatacombSettings cs, FloorLayout floor, Area gallery, bool horizontal, int gx, int gy, int w, int h,
            float chance, bool[] allowed, DungeonRandom rng)
        {
            int length = horizontal ? w : h;
            int along = rng.Range(0, 2);
            while (along < length - 1)
            {
                for (int side = 0; side < 2; side++)
                {
                    if (!rng.Chance(chance))
                        continue;
                    int width = cs.nicheSize.Random(rng), depth = cs.nicheDepth.Random(rng);
                    int nw = horizontal ? width : depth, nh = horizontal ? depth : width;
                    int nx, ny;
                    if (horizontal)
                    {
                        nx = gx + along;
                        ny = side == 0 ? gy - 1 - nh : gy + h + 1;
                    }
                    else
                    {
                        ny = gy + along;
                        nx = side == 0 ? gx - 1 - nw : gx + w + 1;
                    }
                    if (horizontal ? nx + nw > gx + w : ny + nh > gy + h)
                        continue;
                    ShapeMask mask = RoomShapes.Generate(RoomShape.Rectangle, nw, nh, rng);
                    if (!CanPlaceBeside(floor, mask, nx, ny, gallery.Id, allowed))
                        continue;
                    Area niche = LayoutUtil.Stamp(floor, mask, nx, ny, AreaKind.Room, ZoneStyle.Built);
                    niche.Tag = "Niche";
                    floor.PresetConnections.Add(new Vector2Int(gallery.Id, niche.Id));
                }
                along += cs.nicheEvery.Random(rng);
            }
        }

        /// <summary>
        /// Like <see cref="LayoutUtil.CanPlace"/> with a spacing of 1, except that the gallery it belongs to may be two
        /// cells away (a single wall between them).
        /// </summary>
        private static bool CanPlaceBeside(FloorLayout f, ShapeMask m, int ox, int oy, int galleryId, bool[] allowed)
        {
            TileGrid g = f.Grid;
            RectInt fp = f.Spec.Footprint;
            if (ox <= fp.xMin || oy <= fp.yMin || ox + m.W >= fp.xMax || oy + m.H >= fp.yMax)
                return false;
            for (int y = 0; y < m.H; y++)
            {
                for (int x = 0; x < m.W; x++)
                {
                    int cx = ox + x, cy = oy + y;
                    int i = g.Index(cx, cy);
                    if (g.Type[i] != CellType.Solid || !g.IsCarvable(i) || (allowed != null && !allowed[i]))
                        return false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (!g.InBounds(nx, ny))
                                return false;
                            if (g.Type[g.Index(nx, ny)] != CellType.Solid)
                                return false;
                        }
                    // Two cells out: only the gallery may be there (the wall between them stays).
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != 2)
                                continue;
                            int nx = cx + dx, ny = cy + dy;
                            if (!g.InBounds(nx, ny))
                                continue;
                            int ni = g.Index(nx, ny);
                            if (g.Type[ni] != CellType.Solid && g.Area[ni] != galleryId)
                                return false;
                        }
                }
            }
            return true;
        }

        private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    }
}
