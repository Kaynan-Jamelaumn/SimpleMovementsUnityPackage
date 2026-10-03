using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Fortress floors built around a centre: a big KEEP (octagon, round, cross, pillared or cloister hall) in the middle
    /// of the floor, one or more RINGS of rooms around it (joined to their neighbours by ring corridors), a few SPOKES
    /// (gates) from the inner ring into the keep, each outer room joined to the ring inside it, and round TOWERS in the
    /// corners. Openness makes the keep bigger; Complexity adds rooms. Anchors (entrance, stairs) simply take the place
    /// of the rooms they overlap.
    /// </summary>
    public sealed class CitadelLayout : ILayoutStrategy
    {
        private sealed class RingRoom
        {
            public Area Area;
            public int Ring;
            public float Angle;
        }

        public void Generate(DungeonContext ctx, FloorLayout floor, DungeonRandom rng)
        {
            CitadelSettings cs = ctx.Profile.Citadel ?? new CitadelSettings();
            FloorSpec spec = floor.Spec;
            RectInt fp = spec.Footprint;
            bool[] allowed = LayoutUtil.FootprintMask(floor);
            int minSide = Mathf.Min(fp.width, fp.height);
            Vector2 center = new Vector2(fp.x + fp.width * 0.5f, fp.y + fp.height * 0.5f);
            // Rings are ellipses that follow the footprint's shape.
            float ax = fp.width / (float)minSide, ay = fp.height / (float)minSide;

            // 1. The keep, as close to the centre as the anchors allow.
            Area keep = null;
            int keepSide = Mathf.Clamp(Mathf.RoundToInt(minSide * cs.keepSize.Lerp(spec.Openness)), 7, Mathf.Max(7, minSide - 12));
            for (int attempt = 0; attempt < 4 && keep == null; attempt++, keepSide -= 2)
            {
                if (keepSide < 7)
                    break;
                RoomShape shape = PickKeepShape(cs, keepSide, rng);
                ShapeMask mask = RoomShapes.Generate(shape, keepSide, keepSide, rng);
                keep = PlaceNear(floor, mask, center, 6, 2, allowed, AreaKind.Hall);
            }
            float keepRadius = keep != null ? keepSide * 0.5f : 2f;

            // 2. Rings of rooms.
            var rooms = new List<RingRoom>();
            float radius = keepRadius;
            int ringCount = 0;
            for (int ring = 0; ring < cs.maxRings; ring++)
            {
                int size = Mathf.Max(3, Mathf.RoundToInt(cs.roomSize.Lerp(1f - spec.Complexity)));
                int gap = cs.ringGap.Random(rng);
                float r = radius + gap + size * 0.5f + 1f;
                float rx = r * ax, ry = r * ay;
                if (center.x - rx - size * 0.5f < fp.xMin + 2 || center.x + rx + size * 0.5f > fp.xMax - 2 ||
                    center.y - ry - size * 0.5f < fp.yMin + 2 || center.y + ry + size * 0.5f > fp.yMax - 2)
                    break;
                float circumference = 2f * Mathf.PI * Mathf.Sqrt((rx * rx + ry * ry) * 0.5f);
                int count = Mathf.Clamp(Mathf.RoundToInt(circumference / (size + cs.roomSpacing)), 4, 18);
                float start = rng.Range(0f, Mathf.PI * 2f);
                int placed = 0;
                for (int k = 0; k < count; k++)
                {
                    float angle = start + k * Mathf.PI * 2f / count;
                    var pos = new Vector2(center.x + Mathf.Cos(angle) * rx, center.y + Mathf.Sin(angle) * ry);
                    int w = Mathf.Max(3, size + rng.Range(-1, 2)), h = Mathf.Max(3, size + rng.Range(-1, 2));
                    RoomShape shape = PickRoomShape(ctx, w, h, rng);
                    ShapeMask mask = RoomShapes.Generate(shape, w, h, rng);
                    Area area = PlaceNear(floor, mask, pos, 2, 1, allowed, AreaKind.Room);
                    if (area == null)
                        continue;
                    rooms.Add(new RingRoom { Area = area, Ring = ring, Angle = Normalize(angle) });
                    placed++;
                }
                if (placed == 0)
                    break;
                ringCount++;
                radius = r + size * 0.5f;
            }

            // 3. Corner towers.
            var towers = new List<Area>();
            if (cs.cornerTowers)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    int t = cs.towerSize.Random(rng);
                    float x = (corner & 1) == 0 ? fp.xMin + 2 + t * 0.5f : fp.xMax - 2 - t * 0.5f;
                    float y = (corner & 2) == 0 ? fp.yMin + 2 + t * 0.5f : fp.yMax - 2 - t * 0.5f;
                    ShapeMask mask = RoomShapes.Generate(rng.Chance(0.5f) ? RoomShape.Circle : RoomShape.Octagon, t, t, rng);
                    Area tower = PlaceNear(floor, mask, new Vector2(x, y), 3, 1, allowed, AreaKind.Room);
                    if (tower != null)
                        towers.Add(tower);
                }
            }

            // 4. The plan of the fortress: ring corridors, gates into the keep, spokes between rings, towers.
            for (int ring = 0; ring < ringCount; ring++)
            {
                var onRing = rooms.FindAll(r => r.Ring == ring);
                onRing.Sort((a, b) => a.Angle.CompareTo(b.Angle));
                for (int k = 0; k < onRing.Count && onRing.Count > 1; k++)
                {
                    RingRoom a = onRing[k], b = onRing[(k + 1) % onRing.Count];
                    if (a == b || (onRing.Count == 2 && k == 1))
                        continue;
                    float step = Mathf.PI * 2f / Mathf.Max(3, onRing.Count);
                    float gapAngle = Normalize(b.Angle - a.Angle);
                    // Neighbours along the ring (not across a missing stretch), and only some links on outer rings.
                    if (gapAngle > step * 2.2f || (ring > 0 && !rng.Chance(cs.ringLinkChance)))
                        continue;
                    Preset(floor, a.Area, b.Area);
                }
            }
            if (keep != null)
            {
                var inner = rooms.FindAll(r => r.Ring == 0);
                int gates = Mathf.Min(inner.Count, cs.gates.Random(rng));
                if (gates > 0)
                {
                    inner.Sort((a, b) => a.Angle.CompareTo(b.Angle));
                    int offset = rng.Range(0, inner.Count);
                    for (int g = 0; g < gates; g++)
                        Preset(floor, keep, inner[(offset + g * inner.Count / gates) % inner.Count].Area);
                }
            }
            foreach (RingRoom r in rooms)
            {
                if (r.Ring == 0)
                    continue;
                RingRoom nearest = null;
                float best = float.MaxValue;
                foreach (RingRoom o in rooms)
                {
                    if (o.Ring != r.Ring - 1)
                        continue;
                    float d = (o.Area.Center - r.Area.Center).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = o;
                    }
                }
                if (nearest != null)
                    Preset(floor, r.Area, nearest.Area);
            }
            foreach (Area tower in towers)
            {
                Area nearest = null;
                float best = float.MaxValue;
                foreach (RingRoom o in rooms)
                {
                    float d = (o.Area.Center - tower.Center).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = o.Area;
                    }
                }
                if (nearest != null)
                    Preset(floor, tower, nearest);
            }
        }

        private static void Preset(FloorLayout floor, Area a, Area b)
        {
            if (a == null || b == null || a.Id == b.Id)
                return;
            foreach (Vector2Int p in floor.PresetConnections)
                if ((p.x == a.Id && p.y == b.Id) || (p.x == b.Id && p.y == a.Id))
                    return;
            floor.PresetConnections.Add(new Vector2Int(a.Id, b.Id));
        }

        private static float Normalize(float angle)
        {
            float twoPi = Mathf.PI * 2f;
            angle %= twoPi;
            return angle < 0f ? angle + twoPi : angle;
        }

        private static RoomShape PickKeepShape(CitadelSettings cs, int side, DungeonRandom rng)
        {
            var weights = new float[12];
            weights[(int)RoomShape.Octagon] = cs.keepOctagon;
            weights[(int)RoomShape.Circle] = cs.keepRound;
            weights[(int)RoomShape.Cross] = cs.keepCross;
            weights[(int)RoomShape.PillaredHall] = cs.keepPillared;
            weights[(int)RoomShape.Ring] = cs.keepCloister;
            for (int s = 0; s < weights.Length; s++)
                if (side < RoomShapes.MinSide((RoomShape)s))
                    weights[s] = 0f;
            int pick = rng.WeightedIndex(weights);
            return pick < 0 ? RoomShape.Octagon : (RoomShape)pick;
        }

        private static RoomShape PickRoomShape(DungeonContext ctx, int w, int h, DungeonRandom rng)
        {
            // Mostly plain chambers; the profile's shapes for the rest.
            return rng.Chance(0.55f) ? RoomShape.Rectangle : LayoutUtil.PickShape(ctx.Profile.Rooms.shapes, w, h, false, rng);
        }

        /// <summary>Stamps the plan centred as close to <paramref name="target"/> as it fits (spiral search).</summary>
        private static Area PlaceNear(FloorLayout floor, ShapeMask mask, Vector2 target, int reach, int spacing, bool[] allowed, AreaKind kind)
        {
            int cx = Mathf.RoundToInt(target.x - mask.W * 0.5f), cy = Mathf.RoundToInt(target.y - mask.H * 0.5f);
            for (int r = 0; r <= reach; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r)
                            continue;
                        if (!LayoutUtil.CanPlace(floor, mask, cx + dx, cy + dy, spacing, allowed))
                            continue;
                        AreaKind k = kind == AreaKind.Room && mask.FloorCount >= 110 ? AreaKind.Hall : kind;
                        return LayoutUtil.Stamp(floor, mask, cx + dx, cy + dy, k, ZoneStyle.Built);
                    }
                }
            }
            return null;
        }
    }
}
