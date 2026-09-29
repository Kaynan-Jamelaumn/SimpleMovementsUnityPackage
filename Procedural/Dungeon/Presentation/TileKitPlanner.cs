using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    public enum TileKitPiece : byte
    {
        Floor,
        Wall,
        Ceiling,
        DoorFrame,
        Pillar,
    }

    /// <summary>One modular piece to place (floor-local position, yaw, scale).</summary>
    public struct TileKitItem
    {
        public TileKitPiece Piece;
        public Vector3 Position;
        public float Yaw;
        public Vector3 Scale;
        public int Cell;
    }

    /// <summary>
    /// Plans where a theme's modular pieces go in built areas - pure data. Every wall edge is owned by exactly one
    /// open cell (the one it faces), so walls are never doubled - which is what the old WallCollision script tried to
    /// clean up after the fact with physics queries.
    /// </summary>
    public static class TileKitPlanner
    {
        public sealed class Options
        {
            public float CellSize = 1.5f;
            public float ModuleSize = 2f;
            public float WallPrefabHeight = 4f;
            public bool ScaleWallsToCeiling = true;
            public bool Ceilings = true;
            public bool DoorFrames = true;
        }

        public static List<TileKitItem> Plan(FloorLayout floor, Options o)
        {
            var items = new List<TileKitItem>();
            TileGrid g = floor.Grid;
            float cs = o.CellSize;
            float s = cs / Mathf.Max(0.01f, o.ModuleSize);

            for (int i = 0; i < g.Count; i++)
            {
                bool open = g.IsWalkable(i);
                int x = i % g.Width, y = i / g.Width;
                var center = new Vector3((x + 0.5f) * cs, 0f, (y + 0.5f) * cs);

                if (!open)
                {
                    if (g.Has(i, CellFlags.Pillar))
                    {
                        float ceiling = 0f;
                        int n = 0;
                        for (int d = 0; d < 4; d++)
                        {
                            int nb = g.Neighbor(i, d);
                            if (nb >= 0 && g.IsWalkable(nb) && !g.Has(nb, CellFlags.Organic))
                            {
                                ceiling += g.CeilingHeight[nb];
                                n++;
                            }
                        }
                        if (n > 0)
                            items.Add(new TileKitItem { Piece = TileKitPiece.Pillar, Position = center, Scale = new Vector3(s, (ceiling / n) / Mathf.Max(0.01f, o.WallPrefabHeight), s), Cell = i });
                    }
                    continue;
                }
                if (g.Has(i, CellFlags.Organic | CellFlags.Prefab))
                    continue;

                float floorH = g.FloorHeight[i], ceilH = g.CeilingHeight[i];
                items.Add(new TileKitItem { Piece = TileKitPiece.Floor, Position = center + Vector3.up * floorH, Scale = new Vector3(s, 1f, s), Cell = i });
                if (o.Ceilings && !g.Has(i, CellFlags.NoCeiling))
                    items.Add(new TileKitItem { Piece = TileKitPiece.Ceiling, Position = center + Vector3.up * ceilH, Scale = new Vector3(s, 1f, s), Cell = i });

                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(i, d);
                    if (nb >= 0 && g.Type[nb] != CellType.Solid)
                        continue;   // open, shaft or prefab: no wall
                    var dir = (Dir4)d;
                    Vector2Int delta = dir.Delta();
                    float height = o.ScaleWallsToCeiling ? (ceilH - floorH) / Mathf.Max(0.01f, o.WallPrefabHeight) : 1f;
                    items.Add(new TileKitItem
                    {
                        Piece = TileKitPiece.Wall,
                        Position = center + new Vector3(delta.x * cs * 0.5f, floorH, delta.y * cs * 0.5f),
                        Yaw = dir.Opposite().Yaw(),
                        Scale = new Vector3(s, height, 1f),
                        Cell = i,
                    });
                }

                if (o.DoorFrames && g.Type[i] == CellType.Door && !g.Has(i, CellFlags.Secret))
                {
                    bool passageNS = g.IsWalkable(g.Neighbor(i, 0)) && g.IsWalkable(g.Neighbor(i, 2));
                    items.Add(new TileKitItem { Piece = TileKitPiece.DoorFrame, Position = center + Vector3.up * floorH, Yaw = passageNS ? 0f : 90f, Scale = new Vector3(s, 1f, s), Cell = i });
                }
            }
            return items;
        }
    }
}
