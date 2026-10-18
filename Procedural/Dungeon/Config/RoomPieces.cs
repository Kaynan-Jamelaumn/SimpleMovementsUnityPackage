using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// The building pieces of one kind of room: its own floor tiles, wall segments, ceiling tiles, door frames and
    /// pillars, instead of the theme's tile kit (or of the generated geometry). Set them on a Room Template (Shape Mask
    /// mode) or on a Role rule (every room of that role: a kitchen with tiled walls, a crypt with carved stone). Each piece
    /// left empty uses the theme's piece, else the generated geometry. Pieces follow the same rules as the theme's tile kit:
    /// one module square, pivots at their base centre (walls facing +z into the room), with colliders.
    /// </summary>
    [Serializable]
    public class RoomPieces
    {
        [Tooltip("Floor tile one module square, pivot at its centre, top surface at y = 0, with a collider.")]
        public GameObject floorTile;
        [Tooltip("Wall segment one module wide, pivot at the bottom centre of its face, facing +z (into the room), with a collider.")]
        public GameObject wallSegment;
        [Tooltip("Ceiling tile one module square, pivot at its centre, bottom surface at y = 0.")]
        public GameObject ceilingTile;
        [Tooltip("Door frame at the room's door cells, pivot at the bottom centre, opening along z.")]
        public GameObject doorFrame;
        [Tooltip("Pillar for pillar cells, pivot at the bottom centre.")]
        public GameObject pillar;
        [Tooltip("Size of one module of these pieces (meters). 0 = the theme's Module Size.")]
        [Min(0f)] public float moduleSize;
        [Tooltip("Height of the wall segment prefab (meters). 0 = the theme's Wall Prefab Height.")]
        [Min(0f)] public float wallPrefabHeight;

        public bool IsUsed => floorTile != null || wallSegment != null || ceilingTile != null || doorFrame != null || pillar != null;

        public GameObject For(TileKitPiece piece)
        {
            switch (piece)
            {
                case TileKitPiece.Floor: return floorTile;
                case TileKitPiece.Wall: return wallSegment;
                case TileKitPiece.Ceiling: return ceilingTile;
                case TileKitPiece.DoorFrame: return doorFrame;
                default: return pillar;
            }
        }
    }

    /// <summary>Which pieces a room has (plain data, safe on the meshing threads).</summary>
    public sealed class RoomPiecesInfo
    {
        public bool Floor, Wall, Ceiling, DoorFrame, Pillar;
        public float ModuleSize, WallPrefabHeight;
        /// <summary>Main thread only.</summary>
        public RoomPieces Source;

        public static RoomPiecesInfo From(RoomPieces p)
        {
            if (p == null || !p.IsUsed)
                return null;
            return new RoomPiecesInfo
            {
                Floor = p.floorTile != null,
                Wall = p.wallSegment != null,
                Ceiling = p.ceilingTile != null,
                DoorFrame = p.doorFrame != null,
                Pillar = p.pillar != null,
                ModuleSize = p.moduleSize,
                WallPrefabHeight = p.wallPrefabHeight,
                Source = p,
            };
        }

        public bool Has(TileKitPiece piece)
        {
            switch (piece)
            {
                case TileKitPiece.Floor: return Floor;
                case TileKitPiece.Wall: return Wall;
                case TileKitPiece.Ceiling: return Ceiling;
                case TileKitPiece.DoorFrame: return DoorFrame;
                default: return Pillar;
            }
        }
    }

    /// <summary>Which pieces each cell of a floor gets (the area's own pieces, else the theme's tile kit).</summary>
    public static class RoomPiecesMap
    {
        /// <summary>The room pieces of an area: its template's, else the first role rule for its role (matching tag first).</summary>
        public static RoomPiecesInfo For(CompiledProfile profile, Area area)
        {
            if (profile == null || area == null)
                return null;
            if (area.TemplateIndex >= 0 && area.TemplateApplied && area.TemplateIndex < profile.Templates.Count)
            {
                RoomPiecesInfo t = profile.Templates[area.TemplateIndex].Pieces;
                if (t != null) return t;
            }
            RoomPiecesInfo any = null;
            foreach (RoleRuleInfo r in profile.Roles)
            {
                if (r.Pieces == null || r.Role != area.Role) continue;
                if (!string.IsNullOrEmpty(r.Tag) && r.Tag == area.Tag) return r.Pieces;
                if (any == null && string.IsNullOrEmpty(r.Tag)) any = r.Pieces;
            }
            return any;
        }

        /// <summary>Per cell: the room pieces of its area (null = the theme's kit / generated geometry).</summary>
        public static RoomPiecesInfo[] Cells(CompiledProfile profile, FloorLayout floor)
        {
            if (profile == null || !profile.HasRoomPieces || floor == null)
                return null;
            TileGrid g = floor.Grid;
            var byArea = new RoomPiecesInfo[floor.Areas.Count];
            for (int a = 0; a < floor.Areas.Count; a++)
                byArea[a] = For(profile, floor.Areas[a]);
            var cells = new RoomPiecesInfo[g.Count];
            bool any = false;
            for (int i = 0; i < g.Count; i++)
            {
                int a = g.Area[i];
                if (a < 0 || a >= byArea.Length)
                {
                    // Walls, pillars and doorways belong to the open neighbour's room.
                    for (int d = 0; d < 4 && cells[i] == null; d++)
                    {
                        int nb = g.Neighbor(i, d);
                        int na = nb >= 0 ? g.Area[nb] : -1;
                        if (na >= 0 && na < byArea.Length && g.Type[i] != CellType.Solid)
                            cells[i] = byArea[na];
                        else if (na >= 0 && na < byArea.Length && g.Has(i, CellFlags.Pillar))
                            cells[i] = byArea[na];
                    }
                }
                else
                {
                    cells[i] = byArea[a];
                }
                any |= cells[i] != null;
            }
            return any ? cells : null;
        }
    }
}
