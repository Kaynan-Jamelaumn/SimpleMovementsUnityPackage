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
                    if (c.Kind != ConnectionKind.Opening && !c.Kind.IsJump())
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
                GalleryPortals.Add(ctx, floor);
                HeightPass.Apply(ctx, floor, rng);
            });
            ChasmDepth.Apply(ctx);
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

    /// <summary>
    /// Floor and ceiling heights (see <see cref="CarveStage"/>). Built rooms: the role's ceiling, else the template's,
    /// else Room / Hall Ceiling plus a bonus for big rooms; vaulted rooms rise towards their middle. Corridors: Corridor
    /// Ceiling. Caves: domes from the distance to the walls. Every height is multiplied by Ceilings > Height Scale.
    /// </summary>
    public static class HeightPass
    {
        public static void Apply(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            CompiledProfile p = ctx.Profile;
            CaveSettings cs = p.Caves;
            RoomSettings rs = p.Rooms;
            CeilingSettings cl = p.Ceilings ?? new CeilingSettings();
            float scale = Mathf.Max(0.1f, cl.heightScale);
            TileGrid g = floor.Grid;
            int n = g.Count, w = g.Width;
            int seed = ctx.Seed;
            int salt = DungeonRandom.Salt("Heights") + floor.Index * 977;
            float maxCeiling = floor.Spec.MaxCeiling;
            FloorStyle style = floor.Spec.Style;
            bool chasmFloor = style.HasChasm();
            // Islands and the void: one high cavern ceiling over islands, bridges and chasm alike.
            FloatRange skyRange = style == FloorStyle.Astral ? (p.Astral ?? new AstralSettings()).ceiling : (p.Islands ?? new IslandSettings()).ceiling;
            float ChasmSky(int i) => Mathf.Min(skyRange.Lerp(PlacementRandom.Noise((i % w) * 0.06f, (i / w) * 0.06f, seed, salt + 41, 2)) * scale, maxCeiling);

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

            // Per built area: its ceiling (role, template, or room / hall plus size bonus), vault and widest point.
            int areaCount = floor.Areas.Count;
            var areaCeiling = new float[areaCount];
            var areaVault = new bool[areaCount];
            var areaMaxWall = new float[areaCount];
            for (int a = 0; a < areaCount; a++)
            {
                Area area = floor.Areas[a];
                float templateCeiling = area.TemplateIndex >= 0 ? p.Templates[area.TemplateIndex].CeilingHeight : 0f;
                float baseHeight;
                if (area.CeilingHeight > 0f)
                    baseHeight = area.CeilingHeight;
                else if (templateCeiling > 0f)
                    baseHeight = templateCeiling;
                else if (area.Kind == AreaKind.Corridor)
                    baseHeight = Mathf.Max(rs.corridorCeiling, Mathf.Lerp(rs.corridorCeiling, rs.roomCeiling, 0.4f));
                else
                    baseHeight = (area.Kind == AreaKind.Hall ? rs.hallCeiling : rs.roomCeiling) +
                                 cl.SizeBonus(Mathf.Min(area.Bounds.width, area.Bounds.height));
                areaCeiling[a] = baseHeight * scale;

                bool big = area.Kind == AreaKind.Hall || area.Cells.Count >= cl.vaultMinCells;
                bool rolled = area.Style != ZoneStyle.Cavern && area.Kind != AreaKind.Corridor && area.Kind != AreaKind.Landing && big &&
                              PlacementRandom.Value(seed, salt + 9, floor.Index, area.Id, 0) < cl.vaultChance;
                areaVault[a] = cl.vaultHeight > 0f && (area.Vaulted || rolled);
                area.Vaulted = areaVault[a];
                float widest = 1f;
                foreach (int c in area.Cells)
                    widest = Mathf.Max(widest, wallDist[c]);
                areaMaxWall[a] = widest;
            }

            HiveSettings hive = p.Hive ?? new HiveSettings();
            UndercitySettings city = p.Undercity ?? new UndercitySettings();
            var ceil = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!walkable[i])
                    continue;
                int x = i % w, y = i / w;
                int a = g.Area[i];
                Area area = a >= 0 ? floor.Areas[a] : null;
                float height;
                if (chasmFloor)
                {
                    // Everything shares the cavern's (or the void's) ceiling; a special room may only make it taller.
                    height = Mathf.Max(ChasmSky(i) - h[i], area != null ? area.CeilingHeight * scale : 0f);
                }
                else if (g.Has(i, CellFlags.Outdoor))
                {
                    // Under the undercity's sky.
                    height = UndercityLayout.SkyAt(p, seed, floor.Index, x, y);
                }
                else if (style == FloorStyle.Undercity && g.Has(i, CellFlags.Roofed) && area != null && area.CeilingHeight <= 0f && area.TemplateIndex < 0)
                {
                    height = city.interiorCeiling * scale;
                }
                else if (style == FloorStyle.Hive && g.Has(i, CellFlags.Organic) && area != null && area.CeilingHeight <= 0f)
                {
                    // Hive cells: low at the walls, a dome in the middle.
                    float t = Mathf.Clamp01((wallDist[i] - 1f) / Mathf.Max(1f, areaMaxWall[a] - 1f));
                    float noise = (PlacementRandom.Noise(x * 0.15f, y * 0.15f, seed, salt + 5, 2) - 0.5f) * 0.6f;
                    height = (hive.ceiling.Lerp(1f - (1f - t) * (1f - t)) + noise) * scale;
                }
                else if (g.Has(i, CellFlags.Organic))
                {
                    float noise = (PlacementRandom.Noise(x * 0.11f, y * 0.11f, seed, salt + 5, 2) - 0.5f) * 2f * cs.ceilingNoise;
                    height = Mathf.Clamp(cs.ceilingBase + cs.ceilingPerWallDistance * (wallDist[i] - 1f) + noise, cs.ceilingLimits.min, cs.ceilingLimits.max) * scale;
                    // A cave chamber given a role ceiling (a boss cavern) is at least that tall in its middle.
                    if (area != null && area.CeilingHeight > 0f)
                        height = Mathf.Max(height, area.CeilingHeight * scale * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(wallDist[i] / areaMaxWall[a])));
                }
                else if (area != null)
                {
                    height = areaCeiling[a];
                    if (areaVault[a])
                    {
                        // An arch: low at the walls, highest where the room is widest (pillars make cross vaults).
                        float t = Mathf.Clamp01((wallDist[i] - 1f) / Mathf.Max(1f, areaMaxWall[a] - 1f));
                        height += cl.vaultHeight * scale * (1f - (1f - t) * (1f - t));
                    }
                }
                else
                {
                    height = rs.corridorCeiling * scale;
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
                    g.CeilingHeight[i] = Mathf.Min(Mathf.Max(ceil[i], h[i] + cl.minHeadroom), maxCeiling);
                }
                else
                {
                    g.FloorHeight[i] = 0f;
                    g.CeilingHeight[i] = chasmFloor && g.IsChasm(i) ? ChasmSky(i) : maxCeiling;
                }
            }

            // Undercity roofs: above every room's ceiling (special rooms are taller), below the cavern's sky.
            if (g.RoofHeight != null)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!g.Has(i, CellFlags.Roofed))
                        continue;
                    float roof = g.RoofHeight[i];
                    int x = i % w, y = i / w;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= g.Height)
                                continue;
                            int nb = nx + ny * w;
                            if (walkable[nb] && g.Has(nb, CellFlags.Roofed))
                                roof = Mathf.Max(roof, g.CeilingHeight[nb] + 0.4f);
                        }
                    float sky = Mathf.Min(Mathf.Max(g.SkyHeight[i], roof + 1f), maxCeiling);
                    g.SkyHeight[i] = sky;
                    g.RoofHeight[i] = Mathf.Min(roof, sky - 0.5f);
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
                g.CeilingHeight[i] = count > 0 ? sum / count : rs.roomCeiling * scale;
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
