using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// More of the population stage (part of <see cref="FloorPopulator"/>): teleport pads, magic paintings and moving
    /// platforms on their connections; pit fights (waves at the gates, a champion last); sleeping barracks; nests; the
    /// rewards of blood and cursed altars; the wine cellar's hidden lever and barrel stash; gas chambers and their valves;
    /// shifting walls that reshuffle a floor's loops; a roaming mini-boss; tripwires that fire arrows down corridors.
    /// Random choices use the "Features" stream (the rest of the population is unchanged by them).
    /// </summary>
    internal sealed partial class FloorPopulator
    {
        /// <summary>A placement id unique in the dungeon (pairs altars with their rewards).</summary>
        private int UniqueId(int localIndex) => floor.Index * 100000 + localIndex + 1;

        // ------------------------------------------------------------------ crossings (before anything else)

        /// <summary>Pads and paintings on portal connections, platforms on moving-platform connections.</summary>
        private void ReserveCrossings()
        {
            foreach (Connection c in floor.Connections)
            {
                if (c.Failed || !c.Kind.IsJump() || c.DoorA < 0 || c.DoorB < 0)
                    continue;
                if (c.Kind == ConnectionKind.Portal)
                {
                    bool painting = floor.Areas[c.A].Role == AreaRole.Gallery || floor.Areas[c.B].Role == AreaRole.Gallery;
                    int pair = floor.Index * 10000 + c.Id + 1;
                    AddPad(c.DoorA, pair, painting);
                    AddPad(c.DoorB, pair, painting);
                }
                else if (c.Track.Count > 0)
                {
                    int first = c.Track[0];
                    int index = Add(PlacementKind.MovingPlatform, PlacementTable.None, -1, c.DoorA, CellPos(first),
                        YawTowards(CellPos(c.DoorA), CellPos(c.DoorB)), 0f, -1, 1f, 0, 0.6f);
                    Placement pl = placements[index];
                    pl.Link = c.Id;
                    pl.Area = g.Area[c.DoorA];
                    placements[index] = pl;
                    g.Set(c.DoorB, CellFlags.Occupied);
                }
            }
        }

        private void AddPad(int cell, int pair, bool painting)
        {
            Vector2 pos = CellPos(cell);
            float yaw = 0f;
            if (painting)
            {
                // On the wall, facing into the room.
                int side = WallSide(cell);
                if (side >= 0)
                {
                    pos += (Vector2)((Dir4)side).Delta() * 0.45f;
                    yaw = ((Dir4)side).Opposite().Yaw();
                }
            }
            int index = Add(PlacementKind.Teleporter, PlacementTable.None, painting ? 1 : 0, cell, pos, yaw, 0f, -1, 1f, 0, 1f);
            Placement pl = placements[index];
            pl.Link = pair;
            placements[index] = pl;
        }

        // ------------------------------------------------------------------ pit fights and barracks

        /// <summary>
        /// A pit fight: the packs become waves that come out at the arena's gates, and its champion - the toughest mob
        /// allowed, bigger and stronger - comes with the last wave.
        /// </summary>
        private void PitFight(Area area, int first)
        {
            int waves = Mathf.Max(1, MakeWaves(first, M.pitFightWaves));
            List<int> gates = NearWaysIn(area, 3);

            // The champion.
            EncounterInfo champion = null;
            foreach (EncounterInfo e in p.Encounters)
                if (EncounterAllowed(e, area, false) && (champion == null || e.Cost > champion.Cost))
                    champion = e;
            if (champion != null && !Full)
            {
                int best = -1;
                foreach (int c in area.Cells)
                    if (!Blocked(c) && !NearDoor(c) && (best < 0 || wall[c] > wall[best]))
                        best = c;
                if (best >= 0)
                {
                    Vector2 pos = CellPos(best);
                    int group = nextGroup++;
                    int index = Add(PlacementKind.Mob, PlacementTable.Encounters, champion.Index, best, pos,
                        Features.Range(0f, 360f), 0f, group, M.championScale, champion.Tier + M.eliteTierBonus + 1, 1.8f);
                    Placement pl = placements[index];
                    pl.Elite = true;
                    pl.Order = MobOrder.Champion;
                    placements[index] = pl;
                    SetDormant(index, waves);
                }
            }

            // The waves step out at the gates.
            if (gates.Count == 0)
                return;
            for (int i = first; i < placements.Count; i++)
            {
                Placement pl = placements[i];
                if (pl.Kind != PlacementKind.Mob || pl.Area != area.Id || !pl.Dormant || pl.Order == MobOrder.Champion)
                    continue;
                int cell = gates[Features.Range(0, gates.Count)];
                pl.Cell = CellPos(cell) + new Vector2(Features.Range(-0.3f, 0.3f), Features.Range(-0.3f, 0.3f));
                pl.Height = g.FloorHeight[cell];
                pl.Yaw = YawTowards(pl.Cell, area.Center);
                placements[i] = pl;
            }
        }

        /// <summary>Free cells of an area within <paramref name="range"/> cells of its ways in.</summary>
        private List<int> NearWaysIn(Area area, int range)
        {
            var ways = WaysIn(area.Cells);
            var list = new List<int>();
            foreach (int c in area.Cells)
            {
                if (Blocked(c) || NearDoor(c) || wall[c] < 1f)
                    continue;
                Vector2 pc = CellPos(c);
                foreach (int w in ways)
                    if (Mathf.Max(Mathf.Abs(pc.x - CellPos(w).x), Mathf.Abs(pc.y - CellPos(w).y)) <= range)
                    {
                        list.Add(c);
                        break;
                    }
            }
            return list;
        }

        /// <summary>Most of a barracks' soldiers are asleep: sneak past them.</summary>
        private void PutToSleep(int first)
        {
            for (int i = first; i < placements.Count; i++)
            {
                Placement pl = placements[i];
                if (pl.Kind != PlacementKind.Mob || !Features.Chance(M.sleeperShare))
                    continue;
                pl.Order = MobOrder.Sleep;
                placements[i] = pl;
            }
        }

        // ------------------------------------------------------------------ special rooms

        /// <summary>Nests, the wine cellar's lever and stash, gas chambers: before the props, so the furniture fits round them.</summary>
        private void PlaceRoomFixtures()
        {
            foreach (Area area in floor.Areas)
            {
                if (area.AnchorIndex >= 0 || Full)
                    continue;
                switch (area.Role)
                {
                    case AreaRole.Nest: PlaceNests(area); break;
                    case AreaRole.WineCellar: CellarLever(area); break;
                    case AreaRole.GasChamber: GasChamber(area); break;
                }
            }
        }

        /// <summary>The rewards of blood and cursed altars (after the props: the altars are props).</summary>
        private void PlaceRoomMechanics()
        {
            foreach (Area area in floor.Areas)
                if (area.AnchorIndex < 0 && !Full && area.Role == AreaRole.Gambling)
                    AltarRewards(area);
        }

        /// <summary>Nests that keep spawning the cheapest mob allowed here until destroyed.</summary>
        private void PlaceNests(Area area)
        {
            EncounterInfo spawn = null;
            foreach (EncounterInfo e in p.Encounters)
                if (EncounterAllowed(e, area, false) && (spawn == null || e.Cost < spawn.Cost))
                    spawn = e;
            if (spawn == null)
                return;
            int count = Mathf.Max(1, M.nestsPerRoom.Random(Features));
            var cells = new List<int>();
            foreach (int c in area.Cells)
                if (!Blocked(c) && !NearDoor(c) && wall[c] >= 1.5f)
                    cells.Add(c);
            Features.Shuffle(cells);
            var chosen = new List<Vector2>();
            foreach (int c in cells)
            {
                if (chosen.Count >= count || Full)
                    break;
                Vector2 pos = CellPos(c);
                bool clear = !occupied.AnyWithin(pos, 1.5f);
                foreach (Vector2 o in chosen)
                    clear &= (o - pos).sqrMagnitude >= 16f;
                if (!clear)
                    continue;
                Add(PlacementKind.Nest, PlacementTable.Encounters, spawn.Index, c, pos, Features.Range(0f, 360f), 0f, -1, 1f, spawn.Tier, 1.4f);
                chosen.Add(pos);
            }
        }

        /// <summary>Each blood / cursed altar gets a hidden reward next to it, revealed when the altar's price is paid.</summary>
        private void AltarRewards(Area area)
        {
            int count = placements.Count;
            for (int i = 0; i < count && !Full; i++)
            {
                Placement altar = placements[i];
                if (altar.Area != area.Id || altar.Table != PlacementTable.Props)
                    continue;
                DungeonPrimitive kind = p.Props[altar.Entry].Placeholder;
                if (kind != DungeonPrimitive.BloodAltar && kind != DungeonPrimitive.CursedAltar)
                    continue;
                altar.Link = UniqueId(i);
                placements[i] = altar;
                LootInfo entry = PickLoot(area, kind == DungeonPrimitive.CursedAltar ? 2 : 1);
                if (entry == null)
                    continue;
                // Close in front of the altar.
                int best = -1;
                float bestDist = float.MaxValue;
                Vector2 front = altar.Cell + new Vector2(Mathf.Sin(altar.Yaw * Mathf.Deg2Rad), Mathf.Cos(altar.Yaw * Mathf.Deg2Rad)) * 1.6f;
                foreach (int c in area.Cells)
                {
                    if (Blocked(c) || NearDoor(c))
                        continue;
                    float d = (CellPos(c) - front).sqrMagnitude;
                    if (d < bestDist && !occupied.AnyWithin(CellPos(c), 0.9f))
                    {
                        bestDist = d;
                        best = c;
                    }
                }
                if (best < 0)
                    continue;
                int index = Add(PlacementKind.Loot, PlacementTable.Loot, entry.Index, best, CellPos(best), YawTowards(CellPos(best), altar.Cell), 0f, -1, 1f, entry.Tier, 0.8f);
                SetDormant(index, 0);
                Placement reward = placements[index];
                reward.Link = altar.Link;
                placements[index] = reward;
            }
        }

        /// <summary>The wine cellar: a hidden lever that opens the secret door to its hidden room, and a stash in a barrel.</summary>
        private void CellarLever(Area area)
        {
            foreach (int id in area.Connections)
            {
                Connection c = floor.Connections[id];
                if (c.Failed || c.Kind != ConnectionKind.Secret)
                    continue;
                int door = c.DoorA >= 0 && g.Has(c.DoorA, CellFlags.Secret) ? c.DoorA : (c.DoorB >= 0 && g.Has(c.DoorB, CellFlags.Secret) ? c.DoorB : -1);
                if (door < 0)
                    continue;
                var hash = new SpatialHash2D<int>(2f);
                hash.Add(CellPos(door), 0, 3f);   // not right next to the door it opens
                if (!FindSpot(area.Cells, PropPlacement.WallAdjacent, hash, 3f, 0f, out int cell, out Vector2 pos, out float yaw))
                    continue;
                int index = Add(PlacementKind.Lever, PlacementTable.None, 0, cell, pos, yaw, 1.1f, -1, 1f, 0, 0.6f);
                Placement pl = placements[index];
                pl.Link = door;
                placements[index] = pl;
                break;
            }
            // One barrel hides a stash (the barrels that are broken may find it).
            LootInfo entry = PickLoot(area, 1);
            if (entry != null && FindSpot(area.Cells, PropPlacement.Corner, loot, 2f, 0f, out int stash, out Vector2 spos, out float syaw))
            {
                int index = Add(PlacementKind.Loot, PlacementTable.Loot, entry.Index, stash, spos, syaw, 0f, -1, 1f, entry.Tier, 0.8f);
                SetDormant(index, 0);
                Placement pl = placements[index];
                pl.Link = PlacementLinks.BarrelStash;
                placements[index] = pl;
            }
        }

        /// <summary>A gas chamber: the room-wide poison cycle, and the valve that shuts it (at the far end).</summary>
        private void GasChamber(Area area)
        {
            placements.Add(new Placement
            {
                Kind = PlacementKind.AreaEffect,
                Table = PlacementTable.None,
                Entry = -1,
                Floor = floor.Index,
                Area = area.Id,
                Cell = area.Center,
                Height = area.CenterCell >= 0 ? g.FloorHeight[area.CenterCell] : 0f,
                Group = -1,
                Scale = 1f,
                Link = (int)AreaEffectKind.PoisonGas,
            });
            var none = new SpatialHash2D<int>(2f);
            if (FindSpot(FarEnd(area), PropPlacement.WallAdjacent, none, 0f, 0f, out int cell, out Vector2 pos, out float yaw) ||
                FindSpot(area.Cells, PropPlacement.WallAdjacent, none, 0f, 0f, out cell, out pos, out yaw))
            {
                int index = Add(PlacementKind.Lever, PlacementTable.None, 1, cell, pos, yaw, 1.1f, -1, 1f, 0, 0.6f);
                Placement pl = placements[index];
                pl.Link = area.Id;
                placements[index] = pl;
            }
        }

        // ------------------------------------------------------------------ shifting walls

        /// <summary>
        /// Some of a floor's loops get a wall that rises and sinks: every few minutes the floor reshuffles which of them
        /// are shut (at run time, always keeping every area reachable). Only clean passages (touching no third area).
        /// </summary>
        private void PlaceShiftingWalls()
        {
            if (!Features.Chance(M.shiftingChance))
                return;
            var options = new List<(Connection c, int cell, Dir4 along)>();
            foreach (Connection c in floor.Connections)
            {
                if (!c.IsLoop || c.OnMainPath || c.Failed || c.IsRepair || c.Cells.Count == 0)
                    continue;
                if (c.Kind != ConnectionKind.Corridor && c.Kind != ConnectionKind.Door && c.Kind != ConnectionKind.Tunnel && c.Kind != ConnectionKind.Breach)
                    continue;
                if (!CleanPassage(c))
                    continue;
                foreach (int cell in c.Cells)
                {
                    if (g.Has(cell, CellFlags.Occupied | CellFlags.Locked | CellFlags.Landing | CellFlags.Secret))
                        continue;
                    // A one-cell-wide spot: rock on both sides across the passage.
                    Dir4 along;
                    if (g.IsRock(g.Neighbor(cell, 1)) && g.IsRock(g.Neighbor(cell, 3)) && g.IsWalkable(g.Neighbor(cell, 0)) && g.IsWalkable(g.Neighbor(cell, 2)))
                        along = Dir4.North;
                    else if (g.IsRock(g.Neighbor(cell, 0)) && g.IsRock(g.Neighbor(cell, 2)) && g.IsWalkable(g.Neighbor(cell, 1)) && g.IsWalkable(g.Neighbor(cell, 3)))
                        along = Dir4.East;
                    else
                        continue;
                    if (!StillReachable(new[] { cell }, null))
                        continue;
                    options.Add((c, cell, along));
                    break;
                }
            }
            if (options.Count == 0)
                return;
            Features.Shuffle(options);
            int count = Mathf.Max(1, Mathf.RoundToInt(options.Count * M.shiftingShare));
            for (int k = 0; k < count && k < options.Count; k++)
            {
                var o = options[k];
                AddLockPiece(PlacementKind.ShiftingWall, o.cell, o.along, g.Area[o.cell], o.c.Id);
            }
        }

        /// <summary>A passage whose cells touch only its two areas (closing it can't silently cut a third one off).</summary>
        private bool CleanPassage(Connection c)
        {
            foreach (int cell in c.Cells)
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(cell, d);
                    if (nb < 0 || !g.IsWalkable(nb))
                        continue;
                    int a = g.Area[nb];
                    if (a >= 0 && a != c.A && a != c.B)
                        return false;
                    if (a < 0 && g.Connection[nb] >= 0 && g.Connection[nb] != c.Id)
                        return false;
                }
            return true;
        }

        // ------------------------------------------------------------------ roaming mini-boss

        /// <summary>An elite that walks from room to room: it starts in a hub of the floor.</summary>
        private void PlaceRoamer()
        {
            if (p.Encounters.Count == 0 || Full || !Features.Chance(M.roamerChance))
                return;
            Area start = null;
            foreach (Area a in floor.Areas)
            {
                if (a.AnchorIndex >= 0 || a.Role != AreaRole.None || a.Kind == AreaKind.Corridor || HasEvent(a))
                    continue;
                if (start == null || (a.IsHub && !start.IsHub) || (a.IsHub == start.IsHub && a.Cells.Count > start.Cells.Count))
                    start = a;
            }
            if (start == null)
                return;
            EncounterInfo roamer = null;
            foreach (EncounterInfo e in p.Encounters)
                if (EncounterAllowed(e, start, false) && (roamer == null || e.Cost > roamer.Cost))
                    roamer = e;
            if (roamer == null)
                return;
            int best = -1;
            foreach (int c in start.Cells)
                if (!Blocked(c) && !NearDoor(c) && !InSafeZone(c) && (best < 0 || wall[c] > wall[best]))
                    best = c;
            if (best < 0)
                return;
            Vector2 pos = CellPos(best);
            int index = Add(PlacementKind.Mob, PlacementTable.Encounters, roamer.Index, best, pos, Features.Range(0f, 360f), 0f,
                nextGroup++, M.roamerScale, roamer.Tier + M.eliteTierBonus, 1.8f);
            Placement pl = placements[index];
            pl.Elite = true;
            pl.Order = MobOrder.Roam;
            placements[index] = pl;
            mobs.Add(pos, pl.Group, 2f);
        }

        private bool HasEvent(Area a) => gatesOf.ContainsKey(a.Id) || IsAmbush(a) || a.Role == AreaRole.Puzzle;

        // ------------------------------------------------------------------ tripwires

        /// <summary>Tripwires across long corridors, each firing arrows from the walls of the next few cells.</summary>
        private void PlaceTraps()
        {
            foreach (Connection c in floor.Connections)
            {
                if (Full || c.Failed || c.Kind != ConnectionKind.Corridor || c.Cells.Count < 8)
                    continue;
                if (!Features.Chance(M.tripwireChance))
                    continue;
                List<int> path = RoutePart(c.Cells);
                if (path.Count < 8)
                    continue;
                int t = Features.Range(path.Count / 3, Mathf.Max(path.Count / 3 + 1, path.Count * 2 / 3));
                int wire = path[t];
                if (Blocked(wire) || NearDoor(wire) || g.Has(wire, CellFlags.Locked))
                    continue;
                Dir4 along = Dir4Util.FromDelta(g.X(path[t + 1]) - g.X(path[t - 1]), g.Y(path[t + 1]) - g.Y(path[t - 1]));
                int group = nextGroup++;
                int index = Add(PlacementKind.Tripwire, PlacementTable.None, -1, wire, CellPos(wire), along.Yaw(), 0f, group, 1f, 0, 0.4f);
                Placement pl = placements[index];
                pl.Link = group;
                placements[index] = pl;

                int launchers = 0;
                for (int k = 1; k <= 5 && launchers < 3; k++)
                {
                    foreach (int idx in new[] { t + k, t - k })
                    {
                        if (idx < 0 || idx >= path.Count || launchers >= 3)
                            continue;
                        int cell = path[idx];
                        if (Blocked(cell) || NearDoor(cell))
                            continue;
                        // Alternate the walls they shoot from.
                        Dir4[] sides = (launchers & 1) == 0 ? new[] { along.Right(), along.Left() } : new[] { along.Left(), along.Right() };
                        foreach (Dir4 side in sides)
                        {
                            if (!g.IsRock(g.Neighbor(cell, (int)side)))
                                continue;
                            Vector2 pos = CellPos(cell) + (Vector2)side.Delta() * 0.45f;
                            int li = Add(PlacementKind.ArrowLauncher, PlacementTable.None, -1, cell, pos, side.Opposite().Yaw(), 0f, group, 1f, 0, 0.3f);
                            Placement lp = placements[li];
                            lp.Link = group;
                            placements[li] = lp;
                            launchers++;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>The routed part of a connection's cells: the leading run of consecutive neighbours (widening comes after).</summary>
        private List<int> RoutePart(List<int> cells)
        {
            var list = new List<int>();
            foreach (int c in cells)
            {
                if (list.Count > 0)
                {
                    int prev = list[list.Count - 1];
                    if (Mathf.Abs(g.X(prev) - g.X(c)) + Mathf.Abs(g.Y(prev) - g.Y(c)) != 1)
                        break;
                }
                list.Add(c);
            }
            return list;
        }
    }
}
