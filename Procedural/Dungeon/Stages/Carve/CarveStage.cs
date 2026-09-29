using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Stage 5. Makes the plan physical on every floor (in parallel): fits role templates into their areas, routes
    /// and carves every connection (spanning-tree ones first, so loops can reuse them), then sets floor and ceiling
    /// heights - flat built rooms, uneven slope-limited cave floors, domed cave ceilings that rise with the distance
    /// from the walls.
    /// </summary>
    public sealed class CarveStage : IDungeonStage
    {
        public string Name => "Carve";

        public void Run(DungeonContext ctx)
        {
            ctx.ForEachFloor(floor =>
            {
                DungeonRandom rng = ctx.Random("Carve", floor.Index);
                FitTemplates(ctx, floor);

                var router = new CorridorRouter(ctx, floor);
                var order = new List<Connection>();
                foreach (Connection c in floor.Connections)
                    if (c.Kind != ConnectionKind.Opening)
                        order.Add(c);
                order.Sort((a, b) =>
                {
                    int x = (b.InTree ? 1 : 0).CompareTo(a.InTree ? 1 : 0);
                    if (x != 0) return x;
                    x = a.EstimatedCost.CompareTo(b.EstimatedCost);
                    return x != 0 ? x : a.Id.CompareTo(b.Id);
                });
                foreach (Connection c in order)
                {
                    ctx.ThrowIfCancelled();
                    if (!router.Route(c))
                        ctx.Warn($"Floor {floor.Index}: could not route {c}.");
                }

                RecordIncidentalOpenings(floor);
                LayoutUtil.RebuildAreaCells(floor);
                HeightPass.Apply(ctx, floor, rng);
            });
        }

        /// <summary>
        /// Tunnels may brush past chambers they don't join; each such contact is a real way through, so it becomes an
        /// Opening connection (keeps the area graph - dead ends, hubs, paths - true to the carved floor).
        /// </summary>
        public static void RecordIncidentalOpenings(FloorLayout floor)
        {
            TileGrid g = floor.Grid;
            int count = floor.Connections.Count;
            for (int i = 0; i < g.Count; i++)
            {
                int conn = g.Connection[i];
                if (conn < 0 || conn >= count || g.Area[i] >= 0 || !g.IsWalkable(i))
                    continue;
                Connection c = floor.Connections[conn];
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(i, d);
                    if (nb < 0 || !g.IsWalkable(nb))
                        continue;
                    int other = g.Area[nb];
                    if (other < 0 || other == c.A || other == c.B)
                        continue;
                    if (floor.FindConnection(other, c.A) != null)
                        continue;
                    Connection opening = floor.AddConnection(other, c.A, ConnectionKind.Opening);
                    opening.Forced = true;
                    opening.IsLoop = true;
                    opening.Routed = true;
                }
            }
        }

        /// <summary>Stamps templates chosen by the Roles stage into their areas (when they fit inside).</summary>
        private static void FitTemplates(DungeonContext ctx, FloorLayout floor)
        {
            TileGrid g = floor.Grid;
            foreach (Area area in floor.Areas)
            {
                if (area.TemplateIndex < 0 || area.TemplateApplied || area.Fixed)
                    continue;
                TemplateInfo t = ctx.Profile.Templates[area.TemplateIndex];
                var cells = new HashSet<int>(area.Cells);
                RectInt b = area.Bounds;

                bool Fits(int ox, int oy)
                {
                    for (int ty = 0; ty < t.Height; ty++)
                        for (int tx = 0; tx < t.Width; tx++)
                        {
                            int ti = tx + ty * t.Width;
                            if ((t.Floor[ti] || t.Pillar[ti]) && !cells.Contains(g.Index(ox + tx, oy + ty)))
                                return false;
                        }
                    return true;
                }

                int bestX = int.MinValue, bestY = 0;
                int cx = b.x + (b.width - t.Width) / 2, cy = b.y + (b.height - t.Height) / 2;
                if (Fits(cx, cy))
                {
                    bestX = cx;
                    bestY = cy;
                }
                else
                {
                    for (int y = b.y; y <= b.yMax - t.Height && bestX == int.MinValue; y++)
                        for (int x = b.x; x <= b.xMax - t.Width; x++)
                            if (Fits(x, y))
                            {
                                bestX = x;
                                bestY = y;
                                break;
                            }
                }

                if (bestX == int.MinValue)
                {
                    ctx.Warn($"Floor {floor.Index}: template '{t.Name}' doesn't fit {area}.");
                    area.TemplateIndex = -1;
                    continue;
                }
                foreach (int c in area.Cells)
                    g.SetSolid(c);
                TemplateStamp.Apply(floor, area, t, bestX, bestY);
            }
        }
    }

    /// <summary>Floor and ceiling heights (see <see cref="CarveStage"/>).</summary>
    public static class HeightPass
    {
        public static void Apply(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            CompiledProfile p = ctx.Profile;
            CaveSettings cs = p.Caves;
            RoomSettings rs = p.Rooms;
            TileGrid g = floor.Grid;
            int n = g.Count, w = g.Width;
            int seed = ctx.Seed;
            int salt = DungeonRandom.Salt("Heights") + floor.Index * 977;
            float maxCeiling = p.MaxCeiling;

            var walkable = new bool[n];
            var pinned = new bool[n];
            var h = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!g.IsWalkable(i))
                    continue;
                walkable[i] = true;
                int a = g.Area[i];
                bool organic = g.Has(i, CellFlags.Organic);
                pinned[i] = !organic || g.Type[i] == CellType.Door || g.Has(i, CellFlags.Landing | CellFlags.Prefab) ||
                            (a >= 0 && floor.Areas[a].Fixed);
                if (!pinned[i])
                {
                    int x = i % w, y = i / w;
                    h[i] = (PlacementRandom.Noise(x * cs.floorHeightScale, y * cs.floorHeightScale, seed, salt, 3) - 0.5f) * 2f * cs.floorHeightAmplitude;
                }
            }

            // Smooth the natural floor.
            var tmp = new float[n];
            for (int it = 0; it < cs.heightSmoothing; it++)
            {
                for (int i = 0; i < n; i++)
                {
                    tmp[i] = h[i];
                    if (!walkable[i] || pinned[i])
                        continue;
                    float sum = h[i];
                    int count = 1;
                    for (int d = 0; d < 4; d++)
                    {
                        int nb = g.Neighbor(i, d);
                        if (nb >= 0 && walkable[nb])
                        {
                            sum += h[nb];
                            count++;
                        }
                    }
                    tmp[i] = 0.5f * h[i] + 0.5f * sum / count;
                }
                System.Array.Copy(tmp, h, n);
            }

            // Limit slopes so everything stays walkable.
            float maxStep = Mathf.Tan(28f * Mathf.Deg2Rad) * p.CellSize;
            for (int pass = 0; pass < 60; pass++)
            {
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (!walkable[i])
                        continue;
                    for (int d = 0; d < 2; d++)
                    {
                        int j = g.Neighbor(i, d);
                        if (j < 0 || !walkable[j])
                            continue;
                        float diff = h[j] - h[i];
                        if (Mathf.Abs(diff) <= maxStep + 1e-4f || (pinned[i] && pinned[j]))
                            continue;
                        float sign = Mathf.Sign(diff);
                        if (pinned[i])
                            h[j] = h[i] + sign * maxStep;
                        else if (pinned[j])
                            h[i] = h[j] - sign * maxStep;
                        else
                        {
                            float mid = (h[i] + h[j]) * 0.5f;
                            h[i] = mid - sign * maxStep * 0.5f;
                            h[j] = mid + sign * maxStep * 0.5f;
                        }
                        changed = true;
                    }
                }
                if (!changed)
                    break;
            }

            // Ceilings.
            var solid = new bool[n];
            for (int i = 0; i < n; i++)
                solid[i] = !walkable[i];
            float[] wallDist = DistanceField.Compute(solid, g.Width, g.Height);
            var ceil = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!walkable[i])
                    continue;
                int x = i % w, y = i / w;
                int a = g.Area[i];
                Area area = a >= 0 ? floor.Areas[a] : null;
                float height;
                if (g.Has(i, CellFlags.Organic))
                {
                    float noise = (PlacementRandom.Noise(x * 0.11f, y * 0.11f, seed, salt + 5, 2) - 0.5f) * 2f * cs.ceilingNoise;
                    height = Mathf.Clamp(cs.ceilingBase + cs.ceilingPerWallDistance * (wallDist[i] - 1f) + noise, cs.ceilingLimits.min, cs.ceilingLimits.max);
                }
                else if (area != null)
                {
                    float templateCeiling = area.TemplateIndex >= 0 ? p.Templates[area.TemplateIndex].CeilingHeight : 0f;
                    if (templateCeiling > 0f)
                        height = templateCeiling;
                    else if (area.Kind == AreaKind.Hall)
                        height = rs.hallCeiling;
                    else
                        height = rs.roomCeiling;
                }
                else
                {
                    height = rs.corridorCeiling;
                }
                ceil[i] = h[i] + height;
            }

            // Smooth natural ceilings a little.
            for (int it = 0; it < 2; it++)
            {
                for (int i = 0; i < n; i++)
                {
                    tmp[i] = ceil[i];
                    if (!walkable[i] || !g.Has(i, CellFlags.Organic))
                        continue;
                    float sum = ceil[i];
                    int count = 1;
                    for (int d = 0; d < 4; d++)
                    {
                        int nb = g.Neighbor(i, d);
                        if (nb >= 0 && walkable[nb] && g.Has(nb, CellFlags.Organic))
                        {
                            sum += ceil[nb];
                            count++;
                        }
                    }
                    tmp[i] = sum / count;
                }
                System.Array.Copy(tmp, ceil, n);
            }

            for (int i = 0; i < n; i++)
            {
                if (walkable[i])
                {
                    g.FloorHeight[i] = h[i];
                    g.CeilingHeight[i] = Mathf.Clamp(ceil[i], h[i] + 2.4f, maxCeiling);
                }
                else
                {
                    g.FloorHeight[i] = 0f;
                    g.CeilingHeight[i] = maxCeiling;
                }
            }

            // Drop pits keep the ceiling of the room around them.
            for (int i = 0; i < n; i++)
            {
                if (!g.Has(i, CellFlags.Pit))
                    continue;
                float sum = 0f;
                int count = 0;
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(i, d);
                    if (nb >= 0 && walkable[nb])
                    {
                        sum += g.CeilingHeight[nb];
                        count++;
                    }
                }
                g.CeilingHeight[i] = count > 0 ? sum / count : rs.roomCeiling;
            }
            // Pits in the middle of a pit (2x2 or 3x3) take their neighbouring pits' value.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    if (!g.Has(i, CellFlags.Pit))
                        continue;
                    float best = g.CeilingHeight[i];
                    for (int d = 0; d < 4; d++)
                    {
                        int nb = g.Neighbor(i, d);
                        if (nb >= 0 && g.Has(nb, CellFlags.Pit) && g.CeilingHeight[nb] < best)
                            best = g.CeilingHeight[nb];
                    }
                    g.CeilingHeight[i] = Mathf.Min(best, maxCeiling);
                }
        }
    }
}
