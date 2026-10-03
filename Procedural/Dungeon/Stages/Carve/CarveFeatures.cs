using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Galleries' magic paintings: each gallery gets a Portal connection to a pocket room elsewhere on its floor - the
    /// floor's secret room when there is one, else a dead end off the main path (a treasure room or vault first). Walking
    /// into the painting takes you there; its twin painting brings you back.
    /// </summary>
    public static class GalleryPortals
    {
        public static void Add(DungeonContext ctx, FloorLayout floor)
        {
            foreach (Area gallery in floor.Areas)
            {
                if (gallery.Role != AreaRole.Gallery || gallery.Cells.Count == 0)
                    continue;
                Area pocket = null;
                int bestScore = int.MinValue;
                foreach (Area a in floor.Areas)
                {
                    if (a == gallery || a.Cells.Count < 6 || a.AnchorIndex >= 0 || a.Kind == AreaKind.Corridor)
                        continue;
                    int score;
                    switch (a.Role)
                    {
                        case AreaRole.Secret: score = 100; break;
                        case AreaRole.Treasure:
                        case AreaRole.Vault: score = 50; break;
                        case AreaRole.None: score = 10; break;
                        default: continue;
                    }
                    if (a.IsLeaf)
                        score += 20;
                    if (a.OnMainPath)
                        score -= 40;
                    score += Mathf.RoundToInt((a.Center - gallery.Center).magnitude * 0.1f);   // farther is better
                    if (score > bestScore)
                    {
                        bestScore = score;
                        pocket = a;
                    }
                }
                if (pocket == null || floor.FindConnection(gallery.Id, pocket.Id) != null)
                    continue;
                int ca = WallCell(floor.Grid, gallery), cb = WallCell(floor.Grid, pocket);
                if (ca < 0 || cb < 0)
                    continue;
                Connection c = floor.AddConnection(gallery.Id, pocket.Id, ConnectionKind.Portal);
                c.Forced = true;
                c.IsLoop = true;
                c.Routed = true;
                c.DoorA = ca;
                c.DoorB = cb;
            }
        }

        /// <summary>A walkable cell against a wall, away from doors and landings (where a painting hangs).</summary>
        private static int WallCell(TileGrid g, Area area)
        {
            int best = -1;
            float bestScore = float.MinValue;
            foreach (int c in area.Cells)
            {
                if (g.Type[c] != CellType.Floor || g.Has(c, CellFlags.Landing | CellFlags.Pit | CellFlags.NoCeiling))
                    continue;
                bool wall = false, door = false;
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (g.IsRock(nb))
                        wall = true;
                    if (nb >= 0 && (g.Type[nb] == CellType.Door || g.Type[nb] == CellType.Link))
                        door = true;
                }
                if (!wall || door)
                    continue;
                // Prefer the middle of a wall, away from the room's centre... and its corners.
                float score = -(g.Center(c) - area.Center).sqrMagnitude * 0.01f + g.WalkableNeighbors(c);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// How deep each chasm cell drops (stored as the cell's floor height, negative): as far as the space under it allows
    /// - above the ceiling of the floor below where that floor is open there, down to that floor's base where it is rock,
    /// and far down on the last floor. Runs after every floor's heights are known.
    /// </summary>
    public static class ChasmDepth
    {
        public const float LastFloorDepth = 26f;

        public static void Apply(DungeonContext ctx)
        {
            List<FloorLayout> floors = ctx.Layout.Floors;
            for (int f = 0; f < floors.Count; f++)
            {
                FloorLayout floor = floors[f];
                if (!floor.Spec.Style.HasChasm())
                    continue;
                TileGrid g = floor.Grid;
                FloorLayout below = f + 1 < floors.Count ? floors[f + 1] : null;
                float drop = below != null ? floor.Spec.BaseY - below.Spec.BaseY : 0f;
                for (int i = 0; i < g.Count; i++)
                {
                    if (!g.IsChasm(i))
                        continue;
                    float bottom;
                    if (below == null)
                        bottom = -LastFloorDepth;
                    else
                    {
                        TileGrid b = below.Grid;
                        float limit;
                        if (b.Type[i] == CellType.Link)
                            limit = drop - 0.6f;                                  // a stair well below: stay shallow
                        else if (b.Type[i] != CellType.Solid || b.IsChasm(i))
                            limit = b.CeilingHeight[i] + 0.4f;                    // open air below: stay above its ceiling
                        else
                            limit = 0.3f;                                         // rock below: down to that floor's level
                        bottom = Mathf.Min(-1.2f, limit - drop);
                    }
                    g.FloorHeight[i] = bottom;
                }
            }
        }

    }
}
