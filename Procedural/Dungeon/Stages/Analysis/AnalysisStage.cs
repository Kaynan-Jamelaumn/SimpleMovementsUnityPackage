using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Stage 7. Measures the finished floors for the systems that come after (population, AI, gameplay): walking
    /// distance from each floor's arrival, distance to the nearest wall, the cells of the main path, chokepoints
    /// (one-cell doors and passages), each area's size, width and openness, dead ends and hubs, and each floor's
    /// distance from the dungeon entrance along the main path.
    /// </summary>
    public sealed class AnalysisStage : IDungeonStage
    {
        public string Name => "Analysis";

        public void Run(DungeonContext ctx)
        {
            ctx.ForEachFloor(floor => Analyse(ctx, floor));

            DungeonLayout layout = ctx.Layout;
            int offset = 0;
            for (int f = 0; f < layout.Floors.Count; f++)
            {
                FloorLayout floor = layout.Floors[f];
                floor.GlobalDistanceOffset = offset;
                int toDeparture = floor.DepartureCell >= 0 && floor.DistanceFromArrival[floor.DepartureCell] >= 0
                    ? floor.DistanceFromArrival[floor.DepartureCell]
                    : 0;
                float rise = f + 1 < layout.Floors.Count ? floor.Spec.BaseY - layout.Floors[f + 1].Spec.BaseY : 0f;
                offset += toDeparture + (rise > 0f ? ctx.Profile.StairLengthFor(rise) : 0);
            }
        }

        private static void Analyse(DungeonContext ctx, FloorLayout floor)
        {
            TileGrid g = floor.Grid;
            int n = g.Count;

            floor.DistanceFromArrival = floor.Flood(floor.ArrivalCell);
            Dictionary<int, List<int>> jumps = floor.Jumps();

            var solid = new bool[n];
            for (int i = 0; i < n; i++)
                solid[i] = !g.IsWalkable(i);
            floor.WallDistance = DistanceField.Compute(solid, g.Width, g.Height);

            // Main path cells: walk back from the departure along decreasing distance.
            floor.MainPathCells.Clear();
            int cell = floor.DepartureCell;
            if (cell >= 0 && floor.DistanceFromArrival[cell] >= 0)
            {
                while (cell >= 0)
                {
                    floor.MainPathCells.Add(cell);
                    g.Set(cell, CellFlags.MainPath);
                    int d = floor.DistanceFromArrival[cell];
                    if (d == 0)
                        break;
                    int next = -1;
                    for (int dir = 0; dir < 4 && next < 0; dir++)
                    {
                        int nb = g.Neighbor(cell, dir);
                        if (nb >= 0 && floor.DistanceFromArrival[nb] == d - 1)
                            next = nb;
                    }
                    // Across a teleport pad or a moving platform.
                    if (next < 0 && jumps != null && jumps.TryGetValue(cell, out List<int> from))
                        foreach (int nb in from)
                            if (next < 0 && floor.DistanceFromArrival[nb] == d - 1)
                                next = nb;
                    cell = next;
                }
                floor.MainPathCells.Reverse();
            }

            // Chokepoints: one-cell-wide passages.
            for (int i = 0; i < n; i++)
            {
                if (!g.IsWalkable(i))
                    continue;
                bool nS = g.IsWalkable(g.Neighbor(i, 0)) && g.IsWalkable(g.Neighbor(i, 2));
                bool eW = g.IsWalkable(g.Neighbor(i, 1)) && g.IsWalkable(g.Neighbor(i, 3));
                bool wallsEW = !g.IsWalkable(g.Neighbor(i, 1)) && !g.IsWalkable(g.Neighbor(i, 3));
                bool wallsNS = !g.IsWalkable(g.Neighbor(i, 0)) && !g.IsWalkable(g.Neighbor(i, 2));
                if ((nS && wallsEW) || (eW && wallsNS) || g.Type[i] == CellType.Door || g.IsBridge(i))
                    g.Set(i, CellFlags.Chokepoint);
            }

            // Area metrics and structure.
            foreach (Area a in floor.Areas)
            {
                float sum = 0f, max = 0f;
                foreach (int c in a.Cells)
                {
                    float w = floor.WallDistance[c];
                    sum += w;
                    if (w > max)
                        max = w;
                }
                a.MeanWallDistance = a.Cells.Count > 0 ? sum / a.Cells.Count : 0f;
                a.MaxWallDistance = max;
                a.Openness = Mathf.Clamp01((a.MeanWallDistance - 1f) / 3f);
                int degree = floor.Degree(a.Id);
                a.IsLeaf = degree <= 1;
                a.IsHub = degree >= 3;
            }
        }
    }
}
