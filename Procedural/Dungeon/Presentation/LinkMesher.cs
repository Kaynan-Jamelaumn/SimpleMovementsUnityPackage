using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Geometry of vertical links, in the lower floor's local space (its base at y = 0, the floor above at the distance
    /// between the two floors). Pure data.
    /// <list type="bullet">
    /// <item><b>Stairs</b>: a solid staircase filling the well (low risers, so both characters and NavMesh agents
    /// climb it), a smooth ramp for the collider, shaft walls on the long sides, a lintel above the lower opening, a
    /// cap at the top, and the two ends for a NavMeshLink joining the two floors' NavMeshes.</item>
    /// <item><b>Drops</b> and <b>climbs</b>: walls lining the shaft between the lower floor's ceiling and the upper floor,
    /// and a link (one way for drops, both ways for climbs - the builder adds the vines and the climbing volume).</item>
    /// <item><b>Spirals</b>: a spiral staircase in a round shaft - steps winding clockwise down around a central column,
    /// a smooth helical ramp for the collider, the round wall with the lower floor's doorway, a landing at the bottom,
    /// and a NavMeshLink at the top to the landing above (built into the upper floor by the spiral above, or by
    /// <see cref="BuildSpiralTop"/> for the first spiral of a stack).</item>
    /// </list>
    /// </summary>
    public static class LinkMesher
    {
        private const float Skirt = 0.25f;
        private const float StepRise = 0.2f;

        public static LinkMeshData Build(DungeonLayout layout, VerticalLink link, float textureScale)
        {
            float cs = layout.CellSize;
            FloorLayout upper = layout.Floors[link.UpperFloor], lower = layout.Floors[link.LowerFloor];
            float rise = upper.Spec.BaseY - lower.Spec.BaseY;
            RectInt f = link.Footprint;
            float x0 = f.xMin * cs, x1 = f.xMax * cs, z0 = f.yMin * cs, z1 = f.yMax * cs;

            var data = new LinkMeshData
            {
                LinkId = link.Id,
                Visual = new MeshBuffers { TextureScale = textureScale },
                Collider = new MeshBuffers { TextureScale = textureScale },
            };

            float upperCeiling = rise + upper.Grid.CeilingHeight[upper.Grid.Index(link.UpperLanding)];
            float lowerCeiling = lower.Grid.CeilingHeight[lower.Grid.Index(link.LowerLanding)];

            if (link.Kind == LinkKind.Drop || link.Kind == LinkKind.Climb)
            {
                float bottom = lowerCeiling - 0.01f;
                foreach (Vector2Int c in f.allPositionsWithin)
                    bottom = Mathf.Min(bottom, lower.Grid.CeilingHeight[lower.Grid.Index(c)] - 0.01f);
                float shaftTop = rise + 0.05f;
                WallsAround(data, x0, x1, z0, z1, bottom, shaftTop, DungeonSurface.BuiltWall, true, true, true, true);

                float upperFloorH = rise + upper.Grid.FloorHeight[upper.Grid.Index(link.UpperLanding)];
                data.LinkStart = new Vector3((link.UpperLanding.x + 0.5f) * cs, upperFloorH, (link.UpperLanding.y + 0.5f) * cs);
                data.LinkEnd = new Vector3((link.LowerLanding.x + 0.5f) * cs, lower.Grid.FloorHeight[lower.Grid.Index(link.LowerLanding)], (link.LowerLanding.y + 0.5f) * cs);
                data.LinkWidth = cs;
                data.Bidirectional = link.Kind == LinkKind.Climb;
                if (link.Kind == LinkKind.Climb)
                    data.ClimbVolume = new Bounds(new Vector3((x0 + x1) * 0.5f, (rise + 1.2f) * 0.5f, (z0 + z1) * 0.5f),
                        new Vector3(x1 - x0, rise + 1.2f, z1 - z0));
                data.Visual.FinishNormals();
                data.Collider.FinishNormals();
                return data;
            }

            Dir4 d = link.Descend;
            bool alongZ = d.IsVertical();
            float run = alongZ ? z1 - z0 : x1 - x0;
            int steps = Mathf.Max(4, Mathf.CeilToInt(rise / StepRise));
            float stepRise = rise / steps;
            float depth = run / steps;
            var down = new Vector3(d.Delta().x, 0f, d.Delta().y);   // towards the lower end

            // u = distance from the lower end towards the upper end.
            void AlongRect(float u0, float u1, out float ax0, out float ax1, out float az0, out float az1)
            {
                ax0 = x0; ax1 = x1; az0 = z0; az1 = z1;
                switch (d)
                {
                    case Dir4.North: az0 = z1 - u1; az1 = z1 - u0; break;
                    case Dir4.South: az0 = z0 + u0; az1 = z0 + u1; break;
                    case Dir4.East: ax0 = x1 - u1; ax1 = x1 - u0; break;
                    default: ax0 = x0 + u0; ax1 = x0 + u1; break;
                }
            }

            // Visual steps: tread and riser per step (the rest of each block is hidden).
            for (int j = 0; j < steps; j++)
            {
                AlongRect(j * depth, (j + 1) * depth, out float ax0, out float ax1, out float az0, out float az1);
                float y = (j + 1) * stepRise;
                data.Visual.Quad(new Vector3(ax0, y, az0), new Vector3(ax1, y, az0), new Vector3(ax1, y, az1), new Vector3(ax0, y, az1), DungeonSurface.Stairs, false, Vector3.up);

                // Riser on the lower-end side of the step.
                float yb = j * stepRise;
                Vector3 r0, r1;
                switch (d)
                {
                    case Dir4.North: r0 = new Vector3(x0, 0, az1); r1 = new Vector3(x1, 0, az1); break;
                    case Dir4.South: r0 = new Vector3(x0, 0, az0); r1 = new Vector3(x1, 0, az0); break;
                    case Dir4.East: r0 = new Vector3(ax1, 0, z0); r1 = new Vector3(ax1, 0, z1); break;
                    default: r0 = new Vector3(ax0, 0, z0); r1 = new Vector3(ax0, 0, z1); break;
                }
                data.Visual.Quad(r0 + Vector3.up * yb, r1 + Vector3.up * yb, r1 + Vector3.up * y, r0 + Vector3.up * y, DungeonSurface.Stairs, false, down);
            }

            // Collider ramp: lower end at 0, upper end at the floor above.
            AlongRect(0f, 0f, out float lx0, out float lx1, out float lz0, out float lz1);
            AlongRect(run, run, out float ux0, out float ux1, out float uz0, out float uz1);
            Vector3 lowA, lowB, upA, upB;
            if (alongZ)
            {
                lowA = new Vector3(x0, 0f, lz0); lowB = new Vector3(x1, 0f, lz0);
                upA = new Vector3(x0, rise, uz0); upB = new Vector3(x1, rise, uz0);
            }
            else
            {
                lowA = new Vector3(lx0, 0f, z0); lowB = new Vector3(lx0, 0f, z1);
                upA = new Vector3(ux0, rise, z0); upB = new Vector3(ux0, rise, z1);
            }
            data.Collider.Quad(lowA, lowB, upB, upA, DungeonSurface.Stairs, false, Vector3.up);

            // Shaft: long sides full height, a lintel over the lower opening, the cap on top.
            float top = Mathf.Max(upperCeiling, rise + 2.5f);
            bool north = d == Dir4.North, south = d == Dir4.South, east = d == Dir4.East, west = d == Dir4.West;
            // Sides.
            WallsAround(data, x0, x1, z0, z1, -Skirt, top + Skirt, DungeonSurface.BuiltWall,
                wallNorth: east || west, wallSouth: east || west, wallEast: north || south, wallWest: north || south);
            // Lintel above the lower opening, and the rock face under the upper landing.
            float lintelBottom = lowerCeiling;
            WallsAround(data, x0, x1, z0, z1, lintelBottom, top + Skirt, DungeonSurface.BuiltWall,
                wallNorth: north, wallSouth: south, wallEast: east, wallWest: west);
            WallsAround(data, x0, x1, z0, z1, -Skirt, rise, DungeonSurface.BuiltWall,
                wallNorth: south, wallSouth: north, wallEast: west, wallWest: east);
            // Cap.
            data.Visual.Quad(new Vector3(x0, top, z0), new Vector3(x1, top, z0), new Vector3(x1, top, z1), new Vector3(x0, top, z1), DungeonSurface.BuiltCeiling, false, Vector3.down);

            // NavMeshLink across the top edge: from the ramp just below it to the upper landing just beyond it.
            float inset = cs * 0.5f;
            float slope = rise / Mathf.Max(0.01f, run);
            Vector3 topMid = alongZ ? new Vector3((x0 + x1) * 0.5f, rise, uz0) : new Vector3(ux0, rise, (z0 + z1) * 0.5f);
            data.LinkStart = topMid + down * inset - Vector3.up * slope * inset;
            data.LinkEnd = topMid - down * inset;
            data.LinkWidth = (alongZ ? x1 - x0 : z1 - z0) * 0.8f;

            data.Visual.FinishNormals();
            data.Collider.FinishNormals();
            return data;
        }

        // ------------------------------------------------------------------ spirals

        /// <summary>Angle (radians) a spiral's landing takes at a doorway: the doorway's width plus a margin.</summary>
        public static float SpiralLanding(float wallRadius, float cellSize) =>
            2f * Mathf.Asin(Mathf.Clamp01(cellSize * 0.55f / Mathf.Max(0.5f, wallRadius))) + 0.3f;

        /// <summary>Radius of a spiral shaft's round wall for a core of <paramref name="cells"/> cells.</summary>
        public static float SpiralRadius(int cells, float cellSize) => cells * cellSize * 0.5f - 0.02f;

        private static Vector3 Polar(Vector3 center, float radius, float angle, float y) =>
            new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius);

        /// <summary>
        /// One spiral of a stack, in the lower floor's space: the steps from the upper floor's level down to the lower
        /// floor, the collider ramp, the bottom landing (a whole floor at the bottom of the stack), the round wall with the
        /// lower doorway, the central column, the lintel over the doorway and a NavMeshLink at the top.
        /// </summary>
        public static LinkMeshData BuildSpiral(DungeonLayout layout, VerticalLink link, float textureScale, float newelRadius, float doorHeight)
        {
            float cs = layout.CellSize;
            FloorLayout upper = layout.Floors[link.UpperFloor], lower = layout.Floors[link.LowerFloor];
            float rise = upper.Spec.BaseY - lower.Spec.BaseY;
            RectInt f = link.Footprint;
            var center = new Vector3((f.x + f.width * 0.5f) * cs, 0f, (f.y + f.height * 0.5f) * cs);
            float R = SpiralRadius(f.width, cs);
            float rIn = Mathf.Clamp(newelRadius, 0.3f, R - 1.2f);
            float landing = SpiralLanding(R, cs);
            float top = link.StartAngle, bottom = top - link.Turns * Mathf.PI * 2f;
            float slopeStart = top - landing * 0.5f, slopeEnd = bottom + landing * 0.5f;
            float span = Mathf.Max(0.1f, slopeStart - slopeEnd);

            var data = new LinkMeshData
            {
                LinkId = link.Id,
                Visual = new MeshBuffers { TextureScale = textureScale },
                Collider = new MeshBuffers { TextureScale = textureScale },
            };

            // Steps: treads and risers winding down.
            int steps = Mathf.Max(8, Mathf.CeilToInt(rise / StepRise));
            float da = span / steps;
            for (int j = 0; j < steps; j++)
            {
                float a1 = slopeStart - j * da, a0 = a1 - da;
                float h = rise * (steps - j) / (steps + 1f);
                float below = j + 1 < steps ? rise * (steps - j - 1) / (steps + 1f) : 0f;
                data.Visual.Quad(Polar(center, rIn, a0, h), Polar(center, R, a0, h), Polar(center, R, a1, h), Polar(center, rIn, a1, h),
                    DungeonSurface.Stairs, false, Vector3.up);
                var down = new Vector3(Mathf.Sin(a0), 0f, -Mathf.Cos(a0));
                data.Visual.Quad(Polar(center, rIn, a0, below), Polar(center, R, a0, below), Polar(center, R, a0, h), Polar(center, rIn, a0, h),
                    DungeonSurface.Stairs, false, down);
            }

            // The smooth ramp players and agents walk on, and the stair's underside seen from below.
            int segments = Mathf.Max(8, Mathf.CeilToInt(span / 0.12f));
            for (int i = 0; i < segments; i++)
            {
                float b0 = slopeStart - span * i / segments, b1 = slopeStart - span * (i + 1) / segments;
                float y0 = rise * (b0 - slopeEnd) / span, y1 = rise * (b1 - slopeEnd) / span;
                data.Collider.Quad(Polar(center, rIn, b1, y1), Polar(center, R, b1, y1), Polar(center, R, b0, y0), Polar(center, rIn, b0, y0),
                    DungeonSurface.Stairs, false, Vector3.up);
                float t = 0.45f;
                data.Visual.Quad(Polar(center, rIn, b1, y1 - t), Polar(center, R, b1, y1 - t), Polar(center, R, b0, y0 - t), Polar(center, rIn, b0, y0 - t),
                    DungeonSurface.Stairs, false, Vector3.down);
            }

            // The bottom landing - the whole floor of the shaft at the bottom of the stack.
            bool lowest = link.Below < 0;
            float from = lowest ? bottom : bottom - landing * 0.5f, to = lowest ? bottom + Mathf.PI * 2f : bottom + landing * 0.5f;
            Sector(data, center, rIn, R, from, to, 0f);

            // Walls: the round wall (with the lower doorway) and the column, over this spiral's height.
            float lowerCeiling = lower.Grid.CeilingHeight[lower.Grid.Index(link.LowerLanding)];
            RoundWall(data, center, R, lowest ? -Skirt : 0f, rise, bottom, cs, doorHeight);
            Column(data, center, rIn - 0.05f, lowest ? -Skirt : 0f, rise);
            Lintel(data, center, R, bottom, cs, doorHeight, lowerCeiling + Skirt);

            // NavMeshLink: from just below the top of the ramp to the landing above (the upper floor's NavMesh).
            float rMid = (rIn + R) * 0.5f;
            float back = Mathf.Min(0.35f, span * 0.2f);
            data.LinkStart = Polar(center, rMid, slopeStart - back, rise * (slopeStart - back - slopeEnd) / span);
            data.LinkEnd = Polar(center, rMid, top, rise);
            data.LinkWidth = (R - rIn) * 0.8f;

            data.Visual.FinishNormals();
            data.Collider.FinishNormals();
            return data;
        }

        /// <summary>
        /// The top of a spiral stack, in the upper floor's space: the landing at its doorway, the round wall and the column up
        /// to a cap over the shaft, and the lintel over the doorway.
        /// </summary>
        public static LinkMeshData BuildSpiralTop(DungeonLayout layout, VerticalLink link, float textureScale, float newelRadius, float doorHeight)
        {
            float cs = layout.CellSize;
            FloorLayout upper = layout.Floors[link.UpperFloor];
            RectInt f = link.Footprint;
            var center = new Vector3((f.x + f.width * 0.5f) * cs, 0f, (f.y + f.height * 0.5f) * cs);
            float R = SpiralRadius(f.width, cs);
            float rIn = Mathf.Clamp(newelRadius, 0.3f, R - 1.2f);
            float landing = SpiralLanding(R, cs);
            float top = link.StartAngle;
            float ceiling = upper.Grid.CeilingHeight[upper.Grid.Index(link.UpperLanding)];
            float cap = Mathf.Max(ceiling, doorHeight + 0.6f);

            var data = new LinkMeshData
            {
                LinkId = link.Id,
                Visual = new MeshBuffers { TextureScale = textureScale },
                Collider = new MeshBuffers { TextureScale = textureScale },
                NavLink = false,
            };
            Sector(data, center, rIn, R, top - landing * 0.5f, top + landing * 0.5f, 0f);
            RoundWall(data, center, R, 0f, cap + Skirt, top, cs, doorHeight);
            Column(data, center, rIn - 0.05f, 0f, cap + Skirt);
            Lintel(data, center, R, top, cs, doorHeight, ceiling + Skirt);
            // The cap over the shaft.
            int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                data.Visual.Triangle(new Vector3(center.x, cap, center.z), Polar(center, R + 0.05f, a0, cap), Polar(center, R + 0.05f, a1, cap),
                    DungeonSurface.BuiltCeiling, false, Vector3.down);
            }
            data.Visual.FinishNormals();
            data.Collider.FinishNormals();
            return data;
        }

        /// <summary>A flat annular sector (visual and collider) from angle a0 to a1 at height y.</summary>
        private static void Sector(LinkMeshData data, Vector3 center, float r0, float r1, float a0, float a1, float y)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 0.15f));
            for (int i = 0; i < n; i++)
            {
                float b0 = a0 + (a1 - a0) * i / n, b1 = a0 + (a1 - a0) * (i + 1) / n;
                Vector3 p0 = Polar(center, r0, b0, y), p1 = Polar(center, r1, b0, y), p2 = Polar(center, r1, b1, y), p3 = Polar(center, r0, b1, y);
                data.Visual.Quad(p0, p1, p2, p3, DungeonSurface.Stairs, false, Vector3.up);
                data.Collider.Quad(p0, p1, p2, p3, DungeonSurface.Stairs, false, Vector3.up);
            }
        }

        /// <summary>
        /// The shaft's round wall (inward, visual and collider) from <paramref name="y0"/> to <paramref name="y1"/>, open
        /// below the door height at the doorway's angle (one cell wide).
        /// </summary>
        private static void RoundWall(LinkMeshData data, Vector3 center, float radius, float y0, float y1, float doorAngle, float cs, float doorHeight)
        {
            float half = Mathf.Asin(Mathf.Clamp01(cs * 0.5f / radius));
            float open0 = doorAngle - half, open1 = doorAngle + half;
            void Band(float a0, float a1, float lo, float hi)
            {
                if (hi <= lo)
                    return;
                int n = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 0.15f));
                for (int i = 0; i < n; i++)
                {
                    float b0 = a0 + (a1 - a0) * i / n, b1 = a0 + (a1 - a0) * (i + 1) / n;
                    Vector3 p0 = Polar(center, radius, b0, lo), p1 = Polar(center, radius, b1, lo), p2 = Polar(center, radius, b1, hi), p3 = Polar(center, radius, b0, hi);
                    Vector3 mid = (p0 + p2) * 0.5f;
                    var inward = new Vector3(center.x - mid.x, 0f, center.z - mid.z);
                    data.Visual.Quad(p0, p1, p2, p3, DungeonSurface.BuiltWall, false, inward);
                    data.Collider.Quad(p0, p1, p2, p3, DungeonSurface.BuiltWall, false, inward);
                }
            }
            Band(open1, open0 + Mathf.PI * 2f, y0, y1);                    // all round, except the doorway
            Band(open0, open1, Mathf.Max(y0, doorHeight), y1);             // above the doorway
            if (y0 < 0f)
                Band(open0, open1, y0, 0f);                                // under the threshold
        }

        /// <summary>The central column (outward, visual and collider).</summary>
        private static void Column(LinkMeshData data, Vector3 center, float radius, float y0, float y1)
        {
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 p0 = Polar(center, radius, a0, y0), p1 = Polar(center, radius, a1, y0), p2 = Polar(center, radius, a1, y1), p3 = Polar(center, radius, a0, y1);
                Vector3 mid = (p0 + p2) * 0.5f;
                var outward = new Vector3(mid.x - center.x, 0f, mid.z - center.z);
                data.Visual.Quad(p0, p1, p2, p3, DungeonSurface.Trim, false, outward);
                data.Collider.Quad(p0, p1, p2, p3, DungeonSurface.Trim, false, outward);
            }
        }

        /// <summary>The wall face above a doorway, seen from the room outside (the round wall only faces in).</summary>
        private static void Lintel(LinkMeshData data, Vector3 center, float radius, float doorAngle, float cs, float doorHeight, float topY)
        {
            if (topY <= doorHeight)
                return;
            var dir = new Vector3(Mathf.Cos(doorAngle), 0f, Mathf.Sin(doorAngle));
            var across = new Vector3(-dir.z, 0f, dir.x) * (cs * 0.5f + 0.02f);
            Vector3 edge = center + dir * (radius + 0.03f);
            data.Visual.Quad(edge - across + Vector3.up * doorHeight, edge + across + Vector3.up * doorHeight,
                edge + across + Vector3.up * topY, edge - across + Vector3.up * topY, DungeonSurface.BuiltWall, false, dir);
        }

        /// <summary>Inward-facing walls on the chosen sides of a rectangle (visual and collider).</summary>
        private static void WallsAround(LinkMeshData data, float x0, float x1, float z0, float z1, float yb, float yt, DungeonSurface surface,
            bool wallNorth, bool wallSouth, bool wallEast, bool wallWest)
        {
            if (yt <= yb)
                return;
            void Wall(Vector3 a, Vector3 b, Vector3 facing)
            {
                var a0 = new Vector3(a.x, yb, a.z);
                var b0 = new Vector3(b.x, yb, b.z);
                var b1 = new Vector3(b.x, yt, b.z);
                var a1 = new Vector3(a.x, yt, a.z);
                data.Visual.Quad(a0, b0, b1, a1, surface, false, facing);
                data.Collider.Quad(a0, b0, b1, a1, surface, false, facing);
            }
            if (wallSouth) Wall(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.forward);
            if (wallNorth) Wall(new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), Vector3.back);
            if (wallWest) Wall(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), Vector3.right);
            if (wallEast) Wall(new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.left);
        }
    }
}
