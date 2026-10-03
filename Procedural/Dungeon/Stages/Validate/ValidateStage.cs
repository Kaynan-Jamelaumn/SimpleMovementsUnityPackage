using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Stage 6. Checks that the carved dungeon is really traversable and repairs what it can: flood fill from each
    /// floor's arrival; any unreachable area gets a passage carved to the reachable part (up to a limit); doors that
    /// ended up without walls on both sides become open archways; every stair and drop must have walkable, reachable
    /// landings; the entrance, exit and main path must exist. Anything it can't fix fails the attempt (the pipeline
    /// retries with another seed).
    /// </summary>
    public sealed class ValidateStage : IDungeonStage
    {
        public string Name => "Validate and repair";

        public void Run(DungeonContext ctx)
        {
            DungeonLayout layout = ctx.Layout;
            ctx.ForEachFloor(floor => ValidateFloor(ctx, floor));

            foreach (VerticalLink link in layout.Links)
            {
                FloorLayout up = layout.Floors[link.UpperFloor], down = layout.Floors[link.LowerFloor];
                int upCell = up.Grid.Index(link.UpperLanding), downCell = down.Grid.Index(link.LowerLanding);
                if (!up.Grid.IsWalkable(upCell) || !down.Grid.IsWalkable(downCell))
                    ctx.Fail($"{link} has a blocked landing.");
            }

            if (layout.MainPath.Count < 2)
                ctx.Fail("No main path.");
            Area entrance = layout.EntranceArea, exit = layout.ExitArea;
            if (entrance == null || entrance.Cells.Count < 9)
                ctx.Fail("The entrance room is too small.");
            if (exit == null || exit.Cells.Count < 9)
                ctx.Fail("The exit room is too small.");
        }

        private static void ValidateFloor(DungeonContext ctx, FloorLayout floor)
        {
            TileGrid g = floor.Grid;
            ValidationSettings vs = ctx.Profile.Validation;
            if (floor.ArrivalCell < 0 || !g.IsWalkable(floor.ArrivalCell))
                ctx.Fail($"Floor {floor.Index}: the arrival cell isn't walkable.");

            FixDoors(g);

            CorridorRouter router = null;
            int repairs = 0;
            while (true)
            {
                int[] reach = floor.Flood(floor.ArrivalCell);
                Area unreachable = null;
                foreach (Area a in floor.Areas)
                {
                    if (a.Cells.Count == 0)
                        continue;
                    bool reached = false;
                    foreach (int c in a.Cells)
                        if (reach[c] >= 0)
                        {
                            reached = true;
                            break;
                        }
                    if (!reached)
                    {
                        unreachable = a;
                        break;
                    }
                }
                if (unreachable == null)
                    break;

                if (!vs.repairUnreachable || repairs >= vs.maxRepairsPerFloor)
                    ctx.Fail($"Floor {floor.Index}: {unreachable} can't be reached.");

                router = router ?? new CorridorRouter(ctx, floor);
                Connection fix = router.RouteToReached(unreachable, cell => reach[cell] >= 0);
                if (fix == null)
                    ctx.Fail($"Floor {floor.Index}: {unreachable} can't be reached and no passage could be carved.");
                repairs++;
                ctx.Warn($"Floor {floor.Index}: repaired access to {unreachable} ({fix.Cells.Count} cells carved).");
            }

            if (repairs > 0)
            {
                CarveStage.RecordIncidentalOpenings(floor);
                FixDoors(g);
                LayoutUtil.RebuildAreaCells(floor);
                HeightPass.Apply(ctx, floor, ctx.Random("Carve", floor.Index));
                if (floor.Spec.Style.HasChasm())
                    ChasmDepth.Apply(ctx);
            }

            if (floor.DepartureCell >= 0 && !g.IsWalkable(floor.DepartureCell))
                ctx.Fail($"Floor {floor.Index}: the departure cell isn't walkable.");
        }

        /// <summary>
        /// Doors need a wall on each side and a way through; otherwise they become open floor. Outdoors (undercity streets)
        /// and over a chasm there are no doors.
        /// </summary>
        public static void FixDoors(TileGrid g)
        {
            for (int i = 0; i < g.Count; i++)
            {
                if (g.Type[i] != CellType.Door)
                    continue;
                bool n = g.IsWalkable(g.Neighbor(i, 0)), e = g.IsWalkable(g.Neighbor(i, 1));
                bool s = g.IsWalkable(g.Neighbor(i, 2)), w = g.IsWalkable(g.Neighbor(i, 3));
                bool framedNS = n && s && g.IsRock(g.Neighbor(i, 1)) && g.IsRock(g.Neighbor(i, 3));
                bool framedEW = e && w && g.IsRock(g.Neighbor(i, 0)) && g.IsRock(g.Neighbor(i, 2));
                if (g.Has(i, CellFlags.Outdoor | CellFlags.Chasm))
                    framedNS = framedEW = false;
                if (!framedNS && !framedEW)
                {
                    g.Type[i] = CellType.Floor;
                    g.Clear(i, CellFlags.Secret);
                }
            }
        }

        /// <summary>
        /// Walking distance (cells, 4-connected) from a cell over the grid alone; -1 where unreachable. Floors with teleport
        /// pads or moving platforms: use <see cref="FloorLayout.Flood"/>.
        /// </summary>
        public static int[] Flood(TileGrid g, int start)
        {
            var dist = new int[g.Count];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = -1;
            if (start < 0 || !g.IsWalkable(start))
                return dist;
            var queue = new Queue<int>();
            dist[start] = 0;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb < 0 || dist[nb] >= 0 || !g.IsWalkable(nb))
                        continue;
                    dist[nb] = dist[c] + 1;
                    queue.Enqueue(nb);
                }
            }
            return dist;
        }
    }
}
