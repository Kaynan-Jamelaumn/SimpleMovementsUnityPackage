using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The original dungeon's idea, fixed and extended: rooms on a regular grid of blocks, joined as a maze
    /// (randomised depth-first search over every block - no iteration cap, no early stop), with corridors between
    /// blocks, random dead-end pruning for a varied room count and extra edges for loops. Blocks can use room
    /// templates - including the old RoomBehaviour prefabs through a legacy template.
    /// </summary>
    public sealed class GridMazeLayout : ILayoutStrategy
    {
        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            MazeSettings ms = ctx.Profile.Maze;
            CompiledProfile p = ctx.Profile;
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            bool[] allowed = LayoutUtil.FootprintMask(floor);

            int block = Mathf.Max(3, ms.blockSize);
            foreach (int t in p.MazeTemplates)
                block = Mathf.Max(block, Mathf.Max(p.Templates[t].Width, p.Templates[t].Height));
            int gap = Mathf.Max(1, ms.gap);
            int pitch = block + gap;
            int cols = Mathf.Max(1, (fp.width - 2 + gap) / pitch);
            int rows = Mathf.Max(1, (fp.height - 2 + gap) / pitch);
            int ox = fp.x + 1 + ((fp.width - 2) - (cols * pitch - gap)) / 2;
            int oy = fp.y + 1 + ((fp.height - 2) - (rows * pitch - gap)) / 2;

            // Which blocks are free (anchors may take some).
            var valid = new bool[cols * rows];
            var plain = RoomShapes.Generate(RoomShape.Rectangle, block, block, rng);
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols; i++)
                    valid[i + j * cols] = LayoutUtil.CanPlace(floor, plain, ox + i * pitch, oy + j * pitch, 1, allowed);

            // Maze over the valid blocks (one tree per connected group of blocks).
            var visited = new bool[cols * rows];
            var edges = new List<Vector2Int>();
            var stack = new Stack<int>();
            var order = new List<int>();
            for (int b = 0; b < valid.Length; b++)
                order.Add(b);
            rng.Shuffle(order);
            foreach (int start in order)
            {
                if (!valid[start] || visited[start])
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
                        if (valid[nb] && !visited[nb])
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

            // Prune some dead ends for a varied room count.
            var degree = new int[valid.Length];
            foreach (Vector2Int e in edges)
            {
                degree[e.x]++;
                degree[e.y]++;
            }
            int validCount = 0;
            foreach (bool v in valid)
                if (v)
                    validCount++;
            int prune = Mathf.RoundToInt(validCount * ms.pruneFraction.Lerp(1f - spec.Complexity));
            var removed = new bool[valid.Length];
            var leaves = new List<int>();
            for (int b = 0; b < valid.Length; b++)
                if (valid[b] && degree[b] == 1)
                    leaves.Add(b);
            rng.Shuffle(leaves);
            foreach (int b in leaves)
            {
                if (prune <= 0)
                    break;
                removed[b] = true;
                prune--;
            }
            edges.RemoveAll(e => removed[e.x] || removed[e.y]);

            // Loops between neighbouring blocks.
            float loopChance = p.Connections.loopChance.Lerp(spec.Openness);
            var existing = new HashSet<long>();
            foreach (Vector2Int e in edges)
                existing.Add(Key(e.x, e.y));
            for (int b = 0; b < valid.Length; b++)
            {
                if (!valid[b] || removed[b])
                    continue;
                int bx = b % cols, by = b / cols;
                for (int d = 0; d < 2; d++)
                {
                    int nx = bx + (d == 0 ? 1 : 0), ny = by + (d == 1 ? 1 : 0);
                    if (nx >= cols || ny >= rows)
                        continue;
                    int nb = nx + ny * cols;
                    if (!valid[nb] || removed[nb] || existing.Contains(Key(b, nb)) || !rng.Chance(loopChance))
                        continue;
                    edges.Add(new Vector2Int(b, nb));
                    existing.Add(Key(b, nb));
                }
            }

            // Rooms.
            var areaOf = new int[valid.Length];
            for (int b = 0; b < valid.Length; b++)
            {
                areaOf[b] = -1;
                if (!valid[b] || removed[b])
                    continue;
                int x = ox + (b % cols) * pitch, y = oy + (b / cols) * pitch;
                Area area = null;
                if (p.MazeTemplates.Length > 0)
                {
                    var weights = new float[p.MazeTemplates.Length];
                    for (int t = 0; t < weights.Length; t++)
                        weights[t] = p.Templates[p.MazeTemplates[t]].Weight;
                    int pick = rng.WeightedIndex(weights);
                    if (pick >= 0)
                    {
                        TemplateInfo tpl = p.Templates[p.MazeTemplates[pick]];
                        area = TemplateStamp.Stamp(floor, tpl, x + (block - tpl.Width) / 2, y + (block - tpl.Height) / 2, ZoneStyle.Built);
                    }
                }
                if (area == null)
                    area = LayoutUtil.Stamp(floor, plain, x, y, AreaKind.Room, ZoneStyle.Built);
                areaOf[b] = area.Id;
            }

            foreach (Vector2Int e in edges)
                if (areaOf[e.x] >= 0 && areaOf[e.y] >= 0)
                    floor.PresetConnections.Add(new Vector2Int(areaOf[e.x], areaOf[e.y]));
        }

        private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
    }

    /// <summary>Stamps room templates into a floor.</summary>
    public static class TemplateStamp
    {
        /// <summary>Stamps <paramref name="t"/> with its south-west corner at (x, y) as a new area.</summary>
        public static Area Stamp(FloorLayout floor, TemplateInfo t, int x, int y, ZoneStyle style)
        {
            Area area = floor.AddArea(t.FloorCells >= 110 ? AreaKind.Hall : AreaKind.Room, style);
            Apply(floor, area, t, x, y);
            return area;
        }

        /// <summary>Writes a template into an existing area (its old cells must already be cleared).</summary>
        public static void Apply(FloorLayout floor, Area area, TemplateInfo t, int x, int y)
        {
            TileGrid g = floor.Grid;
            bool organic = area.Style == ZoneStyle.Cavern;
            for (int ty = 0; ty < t.Height; ty++)
            {
                for (int tx = 0; tx < t.Width; tx++)
                {
                    int ti = tx + ty * t.Width;
                    int gi = g.Index(x + tx, y + ty);
                    if (t.Floor[ti])
                    {
                        g.SetFloor(gi, area.Id, organic);
                        if (t.IsPrefab)
                            g.Set(gi, CellFlags.Prefab);
                    }
                    else if (t.Pillar[ti])
                    {
                        g.Set(gi, CellFlags.Pillar | CellFlags.Reserved);
                    }
                }
            }
            area.TemplateIndex = t.Index;
            area.TemplateApplied = true;
            area.TemplateOrigin = new Vector2Int(x, y);
            if (t.Role != AreaRole.None && area.Role == AreaRole.None)
            {
                area.Role = t.Role;
                area.Tag = t.Tag;
            }
            LayoutUtil.RebuildCells(floor, area);
        }

        /// <summary>Grid indices of a stamped template's door sockets (the template cells), with their side.</summary>
        public static List<Vector2Int> SocketCells(FloorLayout floor, Area area, TemplateInfo t)
        {
            var list = new List<Vector2Int>();
            TileGrid g = floor.Grid;
            foreach (Vector3Int s in t.Sockets)
            {
                int gx = area.TemplateOrigin.x + s.x, gy = area.TemplateOrigin.y + s.y;
                if (g.InBounds(gx, gy))
                    list.Add(new Vector2Int(g.Index(gx, gy), s.z));
            }
            return list;
        }
    }
}
