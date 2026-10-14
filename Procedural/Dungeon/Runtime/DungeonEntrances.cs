using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Arrival points of a dungeon for linked portals (several world portals leading into one dungeon). Entrance 0 is
    /// the dungeon's own spawn, in front of its entrance portal. Entrance 1, 2... are open, walkable spots of the same
    /// floor, each as far as possible (walking distance) from the spawn and from the entrances before it - so two
    /// teams that come in through two linked portals start on opposite sides and meet somewhere in the middle. Rooms
    /// with their own rules (boss, guardian, vault, trap, puzzle, ambush, secret) and the floor's way down are never
    /// used. Decided by the server from the dungeon's layout; the same dungeon always gives the same points.
    /// </summary>
    public static class DungeonEntrances
    {
        private sealed class Cache
        {
            public DungeonInstance Dungeon;
            public DungeonLayout Layout;
            public int Floor;
            public readonly List<int> Cells = new List<int>();
            public readonly List<int[]> Floods = new List<int[]>();
            public bool Exhausted;
        }

        private static readonly List<Cache> caches = new List<Cache>(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => caches.Clear();

        /// <summary>
        /// Where players of entrance <paramref name="entrance"/> arrive in <paramref name="dungeon"/> (world space). 0 (or
        /// no room for another point) = the dungeon's spawn.
        /// </summary>
        public static Pose Get(DungeonInstance dungeon, int entrance)
        {
            if (dungeon == null)
                return default;
            Pose spawn = dungeon.PlayerSpawn;
            if (entrance <= 0 || dungeon.Layout == null || !dungeon.Layout.PlayerSpawn.Valid)
                return spawn;
            Cache cache = CacheOf(dungeon);
            if (cache == null)
                return spawn;
            FloorLayout arrivalFloor = cache.Layout.Floors[cache.Floor];
            while (cache.Cells.Count <= entrance && !cache.Exhausted)
                cache.Exhausted = !AddNext(arrivalFloor, cache.Cells, cache.Floods);
            if (entrance >= cache.Cells.Count)
                return spawn;

            FloorLayout floor = dungeon.Layout.Floors[cache.Floor];
            TileGrid g = floor.Grid;
            int cell = cache.Cells[entrance];
            // Facing the dungeon's spawn (where the other team comes from).
            Vector2 here = g.Center(cell);
            Vector2 toward = g.Center(cache.Cells[0]) - here;
            float yaw = toward.sqrMagnitude > 0.01f ? Mathf.Atan2(toward.x, toward.y) * Mathf.Rad2Deg : dungeon.Layout.PlayerSpawn.Yaw;
            return dungeon.ToWorld(new DungeonPose(cache.Floor, here, g.FloorHeight[cell], yaw));
        }

        private static Cache CacheOf(DungeonInstance dungeon)
        {
            for (int i = caches.Count - 1; i >= 0; i--)
            {
                Cache c = caches[i];
                if (c.Dungeon == null)
                {
                    caches.RemoveAt(i);
                    continue;
                }
                if (c.Dungeon == dungeon && c.Layout == dungeon.Layout)
                    return c;
            }
            DungeonLayout layout = dungeon.Layout;
            int floorIndex = Mathf.Clamp(layout.PlayerSpawn.Floor, 0, layout.Floors.Count - 1);
            FloorLayout floor = layout.Floors[floorIndex];
            if (floor == null || floor.Grid == null)
                return null;
            TileGrid g = floor.Grid;
            int x = Mathf.Clamp(Mathf.FloorToInt(layout.PlayerSpawn.Cell.x), 0, g.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(layout.PlayerSpawn.Cell.y), 0, g.Height - 1);
            int start = g.Index(x, y);
            if (!g.IsWalkable(start))
                start = NearestWalkable(g, start);
            if (start < 0)
                return null;
            var cache = new Cache { Dungeon = dungeon, Layout = layout, Floor = floorIndex };
            cache.Cells.Add(start);
            cache.Floods.Add(floor.Flood(start));
            if (caches.Count >= 8)
                caches.RemoveAt(0);
            caches.Add(cache);
            return cache;
        }

        /// <summary>
        /// The arrival cells of a floor: <paramref name="startCell"/> (the dungeon's spawn) first, then up to
        /// <paramref name="count"/> - 1 more, each the usable cell farthest (walking) from all the earlier ones. Fewer
        /// when the floor has no room for more (points closer than a few cells are not worth it).
        /// </summary>
        public static List<int> ArrivalCells(FloorLayout floor, int startCell, int count)
        {
            var cells = new List<int>();
            if (floor == null || floor.Grid == null || !floor.Grid.IsWalkable(startCell) || count <= 0)
                return cells;
            var floods = new List<int[]>();
            cells.Add(startCell);
            floods.Add(floor.Flood(startCell));
            while (cells.Count < count && AddNext(floor, cells, floods))
            {
            }
            return cells;
        }

        // The next arrival point: the usable cell farthest (walking) from every point chosen so far. False when none is
        // worth it.
        private static bool AddNext(FloorLayout floor, List<int> cells, List<int[]> floods)
        {
            TileGrid g = floor.Grid;
            int best = -1;
            int bestScore = 0;
            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                float openness = pass == 0 ? 2f : 1f; // prefer open ground, settle for any usable cell
                for (int c = 0; c < g.Count; c++)
                {
                    if (!Usable(floor, c, openness))
                        continue;
                    int nearest = int.MaxValue;
                    for (int i = 0; i < floods.Count; i++)
                    {
                        int d = floods[i][c];
                        if (d < 0)
                        {
                            nearest = -1; // cut off from a chosen point: unreachable without the other team's side
                            break;
                        }
                        if (d < nearest)
                            nearest = d;
                    }
                    if (nearest > bestScore)
                    {
                        bestScore = nearest;
                        best = c;
                    }
                }
            }
            // Too close to be worth a separate entrance (a tiny floor): no more points.
            if (best < 0 || bestScore < MinSeparation)
                return false;
            cells.Add(best);
            floods.Add(floor.Flood(best));
            return true;
        }

        /// <summary>Fewest cells (walking) between two arrival points; a floor without room for that gets no more.</summary>
        public const int MinSeparation = 4;

        private static bool Usable(FloorLayout floor, int c, float openness)
        {
            TileGrid g = floor.Grid;
            if (!g.IsWalkable(c) || g.Type[c] == CellType.Door || g.IsBridge(c))
                return false;
            const CellFlags Avoid = CellFlags.Pit | CellFlags.Chasm | CellFlags.Locked | CellFlags.Occupied |
                                    CellFlags.Landing | CellFlags.Climb | CellFlags.Secret;
            if ((g.Flags[c] & Avoid) != 0)
                return false;
            if (floor.WallDistance != null && c < floor.WallDistance.Length && floor.WallDistance[c] < openness)
                return false;
            int area = g.Area[c];
            if (area >= 0 && area < floor.Areas.Count)
            {
                switch (floor.Areas[area].Role)
                {
                    case AreaRole.Boss:
                    case AreaRole.MiniBoss:
                    case AreaRole.Vault:
                    case AreaRole.Secret:
                    case AreaRole.TrapRoom:
                    case AreaRole.Puzzle:
                    case AreaRole.Ambush:
                    case AreaRole.Exit:
                    case AreaRole.StairsDown:
                    case AreaRole.DropSource:
                        return false;
                }
            }
            return true;
        }

        private static int NearestWalkable(TileGrid g, int from)
        {
            int fx = g.X(from), fy = g.Y(from);
            for (int r = 1; r < 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = fx + dx, y = fy + dy;
                        if (g.InBounds(x, y) && g.IsWalkable(g.Index(x, y)))
                            return g.Index(x, y);
                    }
            return -1;
        }
    }
}
