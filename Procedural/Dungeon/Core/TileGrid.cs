using System;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// One floor's cells: what each cell is, which area or connection owns it, and its floor and ceiling heights
    /// (2.5D: caves have uneven floors and domed ceilings, but nothing overhangs within a floor). Index = x + y * Width;
    /// +y is north (+z in the world). Plain data: generated on worker threads, read by every later stage.
    /// </summary>
    public sealed class TileGrid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly CellType[] Type;
        public readonly CellFlags[] Flags;
        /// <summary>Owning area id, -1 for none (walls, corridors).</summary>
        public readonly int[] Area;
        /// <summary>Connection id of corridor / tunnel cells, -1 for none.</summary>
        public readonly int[] Connection;
        /// <summary>Floor height relative to the floor's base (world units).</summary>
        public readonly float[] FloorHeight;
        /// <summary>Ceiling height relative to the floor's base (world units). Chasm cells: the ceiling above the drop.</summary>
        public readonly float[] CeilingHeight;
        /// <summary>
        /// Roofed cells (undercity buildings): the top of the roof, relative to the floor's base. Null until a layout uses
        /// roofs (see <see cref="EnsureRoofs"/>).
        /// </summary>
        public float[] RoofHeight;
        /// <summary>Roofed cells: the open cavern's ceiling above the roof. Null until a layout uses roofs.</summary>
        public float[] SkyHeight;

        public TileGrid(int width, int height)
        {
            Width = width;
            Height = height;
            int n = width * height;
            Type = new CellType[n];
            Flags = new CellFlags[n];
            Area = new int[n];
            Connection = new int[n];
            FloorHeight = new float[n];
            CeilingHeight = new float[n];
            for (int i = 0; i < n; i++)
            {
                Area[i] = -1;
                Connection[i] = -1;
            }
        }

        public int Count => Width * Height;

        public int Index(int x, int y) => x + y * Width;

        public int Index(Vector2Int c) => c.x + c.y * Width;

        public int X(int index) => index % Width;

        public int Y(int index) => index / Width;

        public Vector2Int Coords(int index) => new Vector2Int(index % Width, index / Width);

        /// <summary>Cell centre in cell units.</summary>
        public Vector2 Center(int index) => new Vector2(index % Width + 0.5f, index / Width + 0.5f);

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

        /// <summary>The neighbour in a direction (0 N, 1 E, 2 S, 3 W), or -1 outside the grid.</summary>
        public int Neighbor(int index, int dir)
        {
            int x = index % Width + Dir4Util.DX[dir], y = index / Width + Dir4Util.DY[dir];
            return InBounds(x, y) ? x + y * Width : -1;
        }

        public bool IsWalkable(int index) => index >= 0 && (Type[index] == CellType.Floor || Type[index] == CellType.Door);

        public bool IsSolid(int index) => index < 0 || Type[index] == CellType.Solid;

        /// <summary>Open air over a drop (no floor): a solid cell with the Chasm flag.</summary>
        public bool IsChasm(int index) => index >= 0 && Type[index] == CellType.Solid && (Flags[index] & CellFlags.Chasm) != 0;

        /// <summary>Real rock (solid and not a chasm).</summary>
        public bool IsRock(int index) => index < 0 || (Type[index] == CellType.Solid && (Flags[index] & CellFlags.Chasm) == 0);

        /// <summary>A walkway over a chasm.</summary>
        public bool IsBridge(int index) => IsWalkable(index) && (Flags[index] & CellFlags.Chasm) != 0;

        /// <summary>Allocates the roof and sky heights (undercity buildings).</summary>
        public void EnsureRoofs()
        {
            if (RoofHeight == null)
                RoofHeight = new float[Count];
            if (SkyHeight == null)
                SkyHeight = new float[Count];
        }

        public bool Has(int index, CellFlags flag) => (Flags[index] & flag) != 0;

        public void Set(int index, CellFlags flag) => Flags[index] |= flag;

        public void Clear(int index, CellFlags flag) => Flags[index] &= ~flag;

        /// <summary>True if the cell may be carved into (not reserved, not a shaft, inside the grid border).</summary>
        public bool IsCarvable(int index)
        {
            if (index < 0 || Has(index, CellFlags.Reserved) || Type[index] == CellType.Link)
                return false;
            int x = index % Width, y = index / Width;
            return x > 0 && y > 0 && x < Width - 1 && y < Height - 1;
        }

        /// <summary>Makes a cell walkable floor owned by an area (or none).</summary>
        public void SetFloor(int index, int area, bool organic)
        {
            Type[index] = CellType.Floor;
            Area[index] = area;
            if (organic)
                Flags[index] |= CellFlags.Organic;
            else
                Flags[index] &= ~CellFlags.Organic;
        }

        /// <summary>Turns a cell back into rock (a chasm stays a chasm, a building wall keeps its roof).</summary>
        public void SetSolid(int index)
        {
            Type[index] = CellType.Solid;
            Area[index] = -1;
            Connection[index] = -1;
            Flags[index] &= CellFlags.Reserved | CellFlags.Chasm | CellFlags.Roofed;
        }

        /// <summary>Counts walkable 4-neighbours.</summary>
        public int WalkableNeighbors(int index)
        {
            int count = 0;
            for (int d = 0; d < 4; d++)
                if (IsWalkable(Neighbor(index, d)))
                    count++;
            return count;
        }

        /// <summary>Solid cells among the 8 neighbours (outside the grid counts as solid).</summary>
        public int SolidNeighbors8(int index)
        {
            int x = index % Width, y = index / Width, count = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    int nx = x + dx, ny = y + dy;
                    if (!InBounds(nx, ny) || Type[nx + ny * Width] == CellType.Solid)
                        count++;
                }
            }
            return count;
        }

        public TileGrid Clone()
        {
            var copy = new TileGrid(Width, Height);
            Array.Copy(Type, copy.Type, Type.Length);
            Array.Copy(Flags, copy.Flags, Flags.Length);
            Array.Copy(Area, copy.Area, Area.Length);
            Array.Copy(Connection, copy.Connection, Connection.Length);
            Array.Copy(FloorHeight, copy.FloorHeight, FloorHeight.Length);
            Array.Copy(CeilingHeight, copy.CeilingHeight, CeilingHeight.Length);
            if (RoofHeight != null)
                copy.RoofHeight = (float[])RoofHeight.Clone();
            if (SkyHeight != null)
                copy.SkyHeight = (float[])SkyHeight.Clone();
            return copy;
        }
    }
}
