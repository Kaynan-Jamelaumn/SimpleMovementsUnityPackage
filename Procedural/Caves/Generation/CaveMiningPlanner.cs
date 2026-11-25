using System.Collections.Generic;
using ProceduralCommon;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>
    /// Decides a cave's mining and its rock formations, as data only (runs on a dungeon worker thread, after the Analysis
    /// stage and before Population, every level in parallel):
    /// <list type="number">
    /// <item>the depth of every chamber (<see cref="CaveDepth"/>);</item>
    /// <item>the special chambers: rich veins (a chamber full of ore, tagged "Rich Vein"; below the first level sometimes
    /// guarded - it becomes a guardian chamber), crystal grottos (tagged "Crystal Grotto", from the Medium band down) and
    /// hoards (boss, guardian and treasure rooms);</item>
    /// <item>deposits, vein by vein: each chamber and tunnel gets deposits by its size and depth (many more in special
    /// chambers); each vein is one resource, picked by how well its depth range suits the place, its weight and the
    /// chamber; its deposits sit together in the walls (at a height a pick swings at) or on open floor;</item>
    /// <item>standing formations (stalagmites, columns, boulders, crystal clusters) in open ground.</item>
    /// </list>
    /// Nothing that stands in the way (a floor deposit, a formation) can ever close a passage: each stands on a cell
    /// whose eight neighbours are open floor and no two are closer than two cells, so a way round each one always stays
    /// open. Nothing is placed near the level's way in, on a chamber's boundary (where gates and openings are), on
    /// landings, in doorways, on the main path's narrow points or in rooms that need their floor (entrances, rests,
    /// shrines, puzzles, traps). Every cell taken is flagged Occupied, so the creatures, chests and props placed after
    /// keep clear of it; formation cells are also flagged <see cref="CellFlags.Formation"/> (their shape is built later
    /// from the floor's build seed, the same on the server and on every client).
    /// </summary>
    public static class CaveMiningPlanner
    {
        /// <summary>Plans every level of the dungeon in <paramref name="ctx"/> and adds the deposits to its placements.</summary>
        public static void Plan(DungeonContext ctx, CaveRules rules)
        {
            if (ctx == null || rules == null)
                return;
            DungeonLayout layout = ctx.Layout;
            var perFloor = new List<Placement>[layout.Floors.Count];
            ctx.ForEachFloor(floor => perFloor[floor.Index] = new FloorPlanner(ctx, rules, floor).Run());
            foreach (List<Placement> list in perFloor)
                if (list != null)
                    layout.Placements.AddRange(list);
        }

        private enum ChamberKind : byte
        {
            Ordinary,
            RichVein,
            Grotto,
            Hoard,
        }

        /// <summary>A chamber or tunnel to spread deposits over.</summary>
        private sealed class Unit
        {
            public int Area = -1;
            public float Depth;
            public ChamberKind Kind;
            public readonly List<int> Cells = new List<int>();
        }

        private sealed class FloorPlanner
        {
            private readonly DungeonContext ctx;
            private readonly CaveRules rules;
            private readonly FloorLayout floor;
            private readonly TileGrid g;
            private readonly float[] wall;
            private readonly int[] reach;
            private readonly List<Placement> placements = new List<Placement>();
            private readonly SpatialHash2D<int> deposits = new SpatialHash2D<int>(4f);
            private readonly SpatialHash2D<int> standing = new SpatialHash2D<int>(4f);
            private readonly List<int> scratch = new List<int>();
            private readonly List<float> weights = new List<float>();
            private float[] areaDepth;
            private ChamberKind[] kinds;
            private int nextVein;

            public FloorPlanner(DungeonContext ctx, CaveRules rules, FloorLayout floor)
            {
                this.ctx = ctx;
                this.rules = rules;
                this.floor = floor;
                g = floor.Grid;
                wall = floor.WallDistance;
                reach = floor.DistanceFromArrival;
                nextVein = floor.Index * 100000 + 50000;
            }

            public List<Placement> Run()
            {
                // Needs the Analysis stage's fields (walking distance, wall distance): without them nothing is placed.
                if (g == null || wall == null || reach == null || floor.Areas.Count == 0)
                    return placements;
                areaDepth = CaveDepth.AreaDepths(ctx, rules.Depth, floor);
                kinds = new ChamberKind[floor.Areas.Count];
                ChooseChambers(ctx.Random("CaveChambers", floor.Index));
                PlaceDeposits(ctx.Random("CaveDeposits", floor.Index));
                if (rules.Formations.enabled)
                    PlaceFormations(ctx.Random("CaveFormations", floor.Index));
                return placements;
            }

            // ------------------------------------------------------------------ where things may go

            private static bool RoleTakesDeposits(AreaRole role)
            {
                switch (role)
                {
                    case AreaRole.None:
                    case AreaRole.Treasure:
                    case AreaRole.Secret:
                    case AreaRole.Garden:
                    case AreaRole.Nest:
                    case AreaRole.MiniBoss:
                    case AreaRole.Arena:
                    case AreaRole.Ambush:
                    case AreaRole.Boss:
                    case AreaRole.Custom:
                        return true;
                    default:
                        return false;
                }
            }

            private bool AreaAllowed(Area a)
            {
                if (a == null || a.Cells.Count == 0 || a.AnchorIndex >= 0 || a.Id == floor.ArrivalArea || a.Id == floor.DepartureArea)
                    return false;
                if (!RoleTakesDeposits(a.Role))
                    return false;
                return !rules.Deposits.cavesOnly || a.Style == ZoneStyle.Cavern;
            }

            /// <summary>A cell something may stand on: plain floor, not taken, not a landing, lock, shaft, chasm or doorway.</summary>
            private bool Free(int c)
            {
                if (c < 0 || g.Type[c] != CellType.Floor)
                    return false;
                const CellFlags taken = CellFlags.Occupied | CellFlags.Landing | CellFlags.Locked | CellFlags.Pit | CellFlags.Chasm | CellFlags.Climb |
                                        CellFlags.Formation | CellFlags.Secret | CellFlags.Prefab | CellFlags.NoCeiling | CellFlags.Roofed;
                if ((g.Flags[c] & taken) != 0)
                    return false;
                if (rules.Deposits.cavesOnly && !g.Has(c, CellFlags.Organic))
                    return false;
                return reach[c] >= Mathf.Max(0, rules.Deposits.clearOfArrival);
            }

            /// <summary>Next to a doorway, a shaft or another area (a chamber's boundary, where its openings and gates are)?</summary>
            private bool OnBoundary(int c)
            {
                int own = g.Area[c];
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb < 0)
                        continue;
                    if (g.Type[nb] == CellType.Door || g.Type[nb] == CellType.Link)
                        return true;
                    if (g.IsWalkable(nb) && g.Area[nb] != own)
                        return true;
                }
                return false;
            }

            /// <summary>Can a blocker (floor deposit, formation) stand here without ever closing a way: all eight neighbours open, none blocked?</summary>
            private bool RoomAllRound(int c)
            {
                int x = g.X(c), y = g.Y(c);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        int nx = x + dx, ny = y + dy;
                        if (!g.InBounds(nx, ny))
                            return false;
                        int n = g.Index(nx, ny);
                        if (!g.IsWalkable(n) || g.Has(n, CellFlags.Formation | CellFlags.Chasm | CellFlags.Pit | CellFlags.Locked))
                            return false;
                    }
                return true;
            }

            /// <summary>Is the 4-neighbour of <paramref name="c"/> in direction <paramref name="d"/> a wall a deposit can sit in (rock, not a chasm's edge or a shaft's casing)?</summary>
            private bool IsWall(int c, int d)
            {
                int nb = g.Neighbor(c, d);
                return nb >= 0 && g.IsRock(nb) && !g.Has(nb, CellFlags.Reserved | CellFlags.Chasm);
            }

            private bool HasWall(int c)
            {
                for (int d = 0; d < 4; d++)
                    if (IsWall(c, d))
                        return true;
                return false;
            }

            /// <summary>A wall 4-neighbour to put a deposit into, as a direction 0-3 (any of several the same chance); -1 when none.</summary>
            private int WallSide(int c, DungeonRandom rng)
            {
                int found = -1, count = 0;
                for (int d = 0; d < 4; d++)
                {
                    if (IsWall(c, d))
                    {
                        count++;
                        // Among several walls (a corner), any one; each the same chance.
                        if (rng.Range(0, count) == 0)
                            found = d;
                    }
                }
                return found;
            }

            private float DepthOfCell(int c, Unit u) => u != null ? u.Depth : 0f;

            // ------------------------------------------------------------------ special chambers

            private void ChooseChambers(DungeonRandom rng)
            {
                CaveChamberSettings ch = rules.Chambers;
                // Hoards: the rooms that guard something.
                foreach (Area a in floor.Areas)
                    if (AreaAllowed(a) && (a.Role == AreaRole.Boss || a.Role == AreaRole.MiniBoss || a.Role == AreaRole.Treasure))
                        kinds[a.Id] = ChamberKind.Hoard;

                var candidates = new List<Area>();
                void Candidates(int minCells)
                {
                    candidates.Clear();
                    foreach (Area a in floor.Areas)
                        if (AreaAllowed(a) && a.Role == AreaRole.None && string.IsNullOrEmpty(a.Tag) && a.Kind == AreaKind.Cavern &&
                            kinds[a.Id] == ChamberKind.Ordinary && a.Cells.Count >= minCells)
                            candidates.Add(a);
                }
                Area Pick()
                {
                    if (candidates.Count == 0)
                        return null;
                    weights.Clear();
                    foreach (Area a in candidates)
                        weights.Add(a.Cells.Count * (a.OnMainPath ? 0.5f : 1.5f) * (a.IsLeaf ? 1.3f : 1f));
                    int i = rng.WeightedIndex(weights);
                    if (i < 0)
                        return null;
                    Area chosen = candidates[i];
                    candidates.RemoveAt(i);
                    return chosen;
                }

                // Rich veins (mining areas), now and then guarded below the first level.
                if (rng.Chance(ch.richVeinChance))
                {
                    Candidates(ch.richVeinMinCells);
                    int count = ch.richVeinsPerLevel.Random(rng);
                    bool guarded = false;
                    for (int k = 0; k < count; k++)
                    {
                        Area a = Pick();
                        if (a == null)
                            break;
                        a.Tag = CavePlacements.RichVeinTag;
                        kinds[a.Id] = ChamberKind.RichVein;
                        if (!guarded && floor.Index >= 1 && !HasRole(AreaRole.MiniBoss) && !HasRole(AreaRole.Boss) && rng.Chance(ch.guardedChance))
                        {
                            a.Role = AreaRole.MiniBoss;   // an elite and its guards (Population), the ways in close while they fight
                            guarded = true;
                        }
                    }
                }

                // A crystal grotto, from the Medium band down (twice as likely in the Deep and Abyssal bands).
                CaveDepthBand band = rules.Depth.Band(CaveDepth.Of(rules.Depth, floor.Index, 0.5f, ctx.Request != null ? ctx.Request.depth : 0,
                    ctx.Request != null ? ctx.Request.difficulty : 1f));
                if (band >= CaveDepthBand.Medium && rng.Chance(Mathf.Clamp01(ch.grottoChance * (band >= CaveDepthBand.Deep ? 2f : 1f))))
                {
                    Candidates(ch.grottoMinCells);
                    Area a = Pick();
                    if (a != null)
                    {
                        a.Tag = CavePlacements.GrottoTag;
                        kinds[a.Id] = ChamberKind.Grotto;
                    }
                }
            }

            private bool HasRole(AreaRole role)
            {
                foreach (Area a in floor.Areas)
                    if (a.Role == role)
                        return true;
                return false;
            }

            // ------------------------------------------------------------------ deposits

            private List<Unit> Units()
            {
                var units = new List<Unit>();
                var byArea = new Dictionary<int, Unit>();
                foreach (Area a in floor.Areas)
                {
                    if (!AreaAllowed(a))
                        continue;
                    var u = new Unit { Area = a.Id, Depth = areaDepth[a.Id], Kind = kinds[a.Id] };
                    byArea[a.Id] = u;
                    units.Add(u);
                }
                // Natural tunnels between chambers hold deposits too (as deep as the chambers they join).
                foreach (Connection con in floor.Connections)
                {
                    if (con.Failed || con.Kind != ConnectionKind.Tunnel || con.Cells.Count < 4)
                        continue;
                    var u = new Unit { Area = -1, Kind = ChamberKind.Ordinary };
                    float da = con.A >= 0 && con.A < areaDepth.Length ? areaDepth[con.A] : 0f;
                    float db = con.B >= 0 && con.B < areaDepth.Length ? areaDepth[con.B] : da;
                    u.Depth = (da + db) * 0.5f;
                    foreach (int c in con.Cells)
                        if (g.Area[c] < 0)
                            u.Cells.Add(c);
                    if (u.Cells.Count > 0)
                        units.Add(u);
                }
                foreach (Area a in floor.Areas)
                    if (byArea.TryGetValue(a.Id, out Unit u))
                        u.Cells.AddRange(a.Cells);
                return units;
            }

            private void PlaceDeposits(DungeonRandom rng)
            {
                CaveDepositSettings ds = rules.Deposits;
                CaveChamberSettings ch = rules.Chambers;
                if (rules.Resources.Count == 0 || ds.perHundredCells <= 0f)
                    return;
                foreach (Unit u in Units())
                {
                    if (placements.Count >= ds.maxPerLevel)
                        return;
                    var wallCells = new List<int>();
                    var floorCells = new List<int>();
                    foreach (int c in u.Cells)
                    {
                        if (!Free(c) || OnBoundary(c))
                            continue;
                        if (wall[c] < 1.6f)
                        {
                            if (HasWall(c))
                                wallCells.Add(c);
                        }
                        else if (wall[c] >= 1.5f && !g.Has(c, CellFlags.MainPath | CellFlags.Chokepoint) && RoomAllRound(c))
                        {
                            floorCells.Add(c);
                        }
                    }
                    if (wallCells.Count + floorCells.Count == 0)
                        continue;
                    rng.Shuffle(wallCells);
                    rng.Shuffle(floorCells);

                    float density = ds.perHundredCells * (1f + ds.morePerDepth * Mathf.Max(0f, u.Depth));
                    float selectionDepth = u.Depth;
                    switch (u.Kind)
                    {
                        case ChamberKind.RichVein:
                            density *= ch.richVeinDensity;
                            selectionDepth += ch.richVeinDepthBonus;
                            break;
                        case ChamberKind.Grotto:
                            density *= ch.grottoDensity;
                            selectionDepth += ch.grottoDepthBonus;
                            break;
                        case ChamberKind.Hoard:
                            density *= ch.hoardDensity;
                            selectionDepth += ch.hoardDepthBonus;
                            break;
                    }
                    float expected = density * u.Cells.Count / 100f;
                    int target = Mathf.FloorToInt(expected) + (rng.Chance(expected - Mathf.Floor(expected)) ? 1 : 0);
                    int placed = 0, guard = 0;
                    while (placed < target && guard++ < target * 6 + 6 && placements.Count < ds.maxPerLevel)
                    {
                        int entry = PickResource(rng, selectionDepth, u.Kind);
                        if (entry < 0)
                            break;
                        CaveResourceEntry e = rules.Resources[entry];
                        int mount = ChooseMount(rng, e, wallCells.Count > 0, floorCells.Count > 0);
                        if (mount < 0)
                            continue;   // this resource has no place here (a floor-only one in a chamber without open floor)
                        bool onWall = mount == 1;
                        List<int> pool = onWall ? wallCells : floorCells;
                        if (pool.Count == 0)
                            continue;
                        int vein = nextVein++;
                        int count = Mathf.Min(e.veinSize.Random(rng), target - placed);
                        int first = TakeSpot(pool, -1, 0f, onWall);
                        if (first < 0)
                            continue;
                        AddDeposit(rng, entry, e, first, onWall, vein, u);
                        placed++;
                        for (int k = 1; k < count; k++)
                        {
                            int next = TakeSpot(pool, first, Mathf.Max(1.5f, e.veinSpread), onWall);
                            if (next < 0)
                                break;
                            AddDeposit(rng, entry, e, next, onWall, vein, u);
                            placed++;
                        }
                    }
                }
            }

            private int PickResource(DungeonRandom rng, float depth, ChamberKind kind)
            {
                weights.Clear();
                bool special = kind != ChamberKind.Ordinary;
                for (int i = 0; i < rules.Resources.Count; i++)
                {
                    CaveResourceEntry e = rules.Resources[i];
                    float w = e != null && e.enabled ? e.weight * CaveDepth.Suitability(e, depth) : 0f;
                    if (w > 0f && e.specialChambersOnly && !special)
                        w = 0f;
                    if (w > 0f)
                    {
                        if (kind == ChamberKind.RichVein)
                            w *= e.richVeinAffinity;
                        else if (kind == ChamberKind.Grotto)
                            w *= e.grottoAffinity;
                        else if (kind == ChamberKind.Hoard)
                            w *= 1f + (int)e.rarity * 0.5f;   // what is guarded leans to the rarer
                    }
                    weights.Add(w);
                }
                return rng.WeightedIndex(weights);
            }

            /// <summary>Where a deposit of <paramref name="e"/> goes here: 1 in a wall, 0 on the floor, -1 nowhere (neither it allows is free).</summary>
            private int ChooseMount(DungeonRandom rng, CaveResourceEntry e, bool walls, bool floors)
            {
                bool wallOk = (e.mount & CaveMount.Wall) != 0 && walls;
                bool floorOk = (e.mount & CaveMount.Floor) != 0 && floors;
                if (wallOk && floorOk)
                    return rng.Chance(rules.Deposits.wallShare) ? 1 : 0;
                return wallOk ? 1 : floorOk ? 0 : -1;
            }

            /// <summary>
            /// The next free spot of <paramref name="pool"/> (shuffled), at least the deposit spacing from every deposit -
            /// and, for a vein's later deposits, within <paramref name="spread"/> cells of its first (<paramref name="near"/>).
            /// The spot is removed from the pool. -1 when none fits.
            /// </summary>
            private int TakeSpot(List<int> pool, int near, float spread, bool onWall)
            {
                float spacing = Mathf.Max(1f, rules.Deposits.spacing);
                Vector2 center = near >= 0 ? g.Center(near) : default;
                for (int i = 0; i < pool.Count; i++)
                {
                    int c = pool[i];
                    Vector2 pos = g.Center(c);
                    if (near >= 0 && (pos - center).sqrMagnitude > spread * spread)
                        continue;
                    if (!Free(c) || deposits.AnyWithin(pos, spacing))
                        continue;
                    if (!onWall && (standing.AnyWithin(pos, Mathf.Max(2.3f, rules.Formations.spacing)) || !RoomAllRound(c)))
                        continue;
                    pool.RemoveAt(i);
                    return c;
                }
                return -1;
            }

            private void AddDeposit(DungeonRandom rng, int entry, CaveResourceEntry e, int cell, bool onWall, int vein, Unit u)
            {
                Vector2 pos = g.Center(cell);
                float yaw;
                float height = g.FloorHeight[cell];
                if (onWall)
                {
                    // Facing out of the wall, into the chamber; at a height a pick swings at (never above the ceiling).
                    int side = Mathf.Max(0, WallSide(cell, rng));   // the cell was chosen for having a wall
                    yaw = ((Dir4)side).Opposite().Yaw();
                    float headroom = g.CeilingHeight[cell] - g.FloorHeight[cell];
                    FloatRange wh = rules.Deposits.wallHeight;
                    height += Mathf.Min(wh.Random(rng), Mathf.Max(0.2f, headroom - 1.2f));
                }
                else
                {
                    pos += new Vector2(rng.Range(-0.12f, 0.12f), rng.Range(-0.12f, 0.12f));
                    yaw = rng.Range(0f, 360f);
                    standing.Add(pos, placements.Count, 0.6f);
                }
                placements.Add(new Placement
                {
                    Kind = PlacementKind.Resource,
                    Table = PlacementTable.None,
                    Entry = entry,
                    Floor = floor.Index,
                    Area = g.Area[cell],
                    Cell = pos,
                    Height = height,
                    Yaw = yaw,
                    Group = vein,
                    Scale = Mathf.Max(0.3f, e.size.Random(rng)),
                    Tier = e.Tier,
                    // The seam: every deposit its own (20 bits, what the crafting system keeps in each lump).
                    Link = (int)(PlacementRandom.Hash(ctx.Seed, DungeonRandom.Salt("CaveSeam"), floor.Index, cell, entry) & 0xFFFFF),
                    Wave = onWall ? CavePlacements.WallMount : CavePlacements.FloorMount,
                    Order = MobOrder.None,
                });
                deposits.Add(pos, placements.Count - 1, 0.8f);
                g.Set(cell, CellFlags.Occupied);
            }

            // ------------------------------------------------------------------ standing formations

            private void PlaceFormations(DungeonRandom rng)
            {
                CaveFormationSettings fs = rules.Formations;
                if (fs.standingPerHundredCells <= 0f)
                    return;
                float spacing = Mathf.Max(2.3f, fs.spacing);
                foreach (Area a in floor.Areas)
                {
                    if (!AreaAllowed(a))
                        continue;
                    // Fighting rooms keep most of their floor clear.
                    float density = fs.standingPerHundredCells * (a.Role == AreaRole.Boss || a.Role == AreaRole.Arena || a.Role == AreaRole.MiniBoss ? 0.4f : 1f);
                    if (kinds[a.Id] == ChamberKind.Grotto)
                        density *= 1.6f;
                    float expected = density * a.Cells.Count / 100f;
                    int target = Mathf.FloorToInt(expected) + (rng.Chance(expected - Mathf.Floor(expected)) ? 1 : 0);
                    if (target <= 0)
                        continue;
                    scratch.Clear();
                    foreach (int c in a.Cells)
                        if (Free(c) && wall[c] >= fs.minWallDistance && !OnBoundary(c) && !g.Has(c, CellFlags.MainPath | CellFlags.Chokepoint) && RoomAllRound(c))
                            scratch.Add(c);
                    rng.Shuffle(scratch);
                    int placed = 0;
                    foreach (int c in scratch)
                    {
                        if (placed >= target)
                            break;
                        Vector2 pos = g.Center(c);
                        if (standing.AnyWithin(pos, spacing) || deposits.AnyWithin(pos, 1.6f) || !RoomAllRound(c))
                            continue;
                        g.Set(c, CellFlags.Formation | CellFlags.Occupied);
                        standing.Add(pos, -1, 0.6f);
                        placed++;
                    }
                }
            }
        }
    }
}
