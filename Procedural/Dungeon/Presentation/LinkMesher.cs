using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Geometry of vertical links, in the lower floor's local space (its base at y = 0, the floor above at
    /// y = FloorSpacing). Pure data.
    /// <list type="bullet">
    /// <item><b>Stairs</b>: a solid staircase filling the well (low risers, so both characters and NavMesh agents
    /// climb it), a smooth ramp for the collider, shaft walls on the long sides, a lintel above the lower opening, a
    /// cap at the top, and the two ends for a NavMeshLink joining the two floors' NavMeshes.</item>
    /// <item><b>Drops</b>: walls lining the shaft between the lower floor's ceiling and the upper floor, and a
    /// one-way link.</item>
    /// </list>
    /// </summary>
    public static class LinkMesher
    {
        private const float Skirt = 0.25f;
        private const float StepRise = 0.2f;

        public static LinkMeshData Build(DungeonLayout layout, VerticalLink link, float textureScale)
        {
            float cs = layout.CellSize;
            float rise = layout.FloorSpacing;
            FloorLayout upper = layout.Floors[link.UpperFloor], lower = layout.Floors[link.LowerFloor];
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

            if (link.Kind == LinkKind.Drop)
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
