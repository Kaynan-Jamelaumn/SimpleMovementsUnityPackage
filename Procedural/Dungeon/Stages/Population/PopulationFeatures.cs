using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>What a room event controller does (stored in the RoomController placement's Link).</summary>
    public enum RoomEventMode
    {
        /// <summary>The gates close when a player comes in and open when every mob of the room is dead.</summary>
        LockUntilCleared = 0,
        /// <summary>Like Lock Until Cleared, but the mobs appear in waves (the placement's Tier = number of waves).</summary>
        Ambush = 1,
        /// <summary>Pressure plates in the order they light up reveal the room's reward.</summary>
        Puzzle = 2,
        /// <summary>A pit fight: the gates close, waves come out at the gates, the champion comes last.</summary>
        PitFight = 3,
    }

    /// <summary>
    /// The mechanics of the population stage (part of <see cref="FloorPopulator"/>): gates at the ways into event rooms
    /// (boss, arena, guardian, throne, ambush, rising crypt), locked doors on vaults with their keys elsewhere on the
    /// floor, elite mobs, ambush waves, rewards that appear when a room is cleared or solved, puzzle plates, room event
    /// controllers and one-way shortcut doors. Uses its own random stream ("Features"), so the rest of the population is
    /// unchanged by it. Every permanent lock is checked: closing it never cuts any other part of the floor off.
    /// </summary>
    internal sealed partial class FloorPopulator
    {
        private DungeonRandom featureRng;
        private readonly Dictionary<int, List<int>> gatesOf = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, int> vaultKeyOf = new Dictionary<int, int>();
        private readonly HashSet<int> permanentlyBlocked = new HashSet<int>();

        private DungeonRandom Features => featureRng ?? (featureRng = ctx.Random("Features", floor.Index));

        private RoomEventSettings M => p.Mechanics ?? new RoomEventSettings();

        // ------------------------------------------------------------------ which rooms

        /// <summary>A crypt whose dead rise when the players come in (decided per area, without using a random stream).</summary>
        private bool CryptRises(Area area)
        {
            return area.Role == AreaRole.Crypt &&
                   PlacementRandom.Value(ctx.Seed, DungeonRandom.Salt("CryptRises"), floor.Index, area.Id, 0) < M.cryptAmbushChance;
        }

        private bool IsAmbush(Area area) => area.Role == AreaRole.Ambush || CryptRises(area);

        /// <summary>Does the room close its gates while its fight lasts?</summary>
        private bool LocksRoom(Area area)
        {
            switch (area.Role)
            {
                case AreaRole.Boss: return M.lockBossRoom;
                case AreaRole.Arena: return M.lockArenas;
                case AreaRole.MiniBoss:
                case AreaRole.Throne:
                    return M.lockGuardianRooms;
                case AreaRole.Ambush:
                case AreaRole.Colosseum:
                    return true;
                case AreaRole.Crypt: return CryptRises(area);
                default: return false;
            }
        }

        // ------------------------------------------------------------------ helpers

        private void SetDormant(int index, int wave)
        {
            Placement pl = placements[index];
            pl.Dormant = true;
            pl.Wave = (byte)Mathf.Clamp(wave, 0, 255);
            placements[index] = pl;
        }

        /// <summary>The packs placed since <paramref name="first"/> become hidden waves (1, 2, 3...). Returns the wave count.</summary>
        private int MakeWaves(int first) => MakeWaves(first, M.ambushWaves);

        private int MakeWaves(int first, IntRange count)
        {
            var groups = new List<int>();
            for (int i = first; i < placements.Count; i++)
                if (placements[i].Kind == PlacementKind.Mob && !groups.Contains(placements[i].Group))
                    groups.Add(placements[i].Group);
            if (groups.Count == 0)
                return 0;
            int waves = Mathf.Clamp(count.Random(Features), 1, groups.Count);
            for (int i = first; i < placements.Count; i++)
            {
                if (placements[i].Kind != PlacementKind.Mob)
                    continue;
                int wave = 1 + groups.IndexOf(placements[i].Group) % waves;
                SetDormant(i, wave);
            }
            return waves;
        }

        /// <summary>Cells of a set that touch walkable ground outside it (the ways in).</summary>
        private List<int> WaysIn(List<int> cells)
        {
            var inside = new HashSet<int>(cells);
            var list = new List<int>();
            foreach (int c in cells)
            {
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb >= 0 && !inside.Contains(nb) && (g.IsWalkable(nb) || g.Type[nb] == CellType.Link))
                    {
                        list.Add(c);
                        break;
                    }
                }
            }
            return list;
        }

        /// <summary>The third of the area's cells farthest (walking) from its ways in: the far end of a gauntlet.</summary>
        private List<int> FarEnd(Area area)
        {
            var inside = new HashSet<int>(area.Cells);
            var dist = new Dictionary<int, int>();
            var queue = new Queue<int>();
            foreach (int c in WaysIn(area.Cells))
            {
                dist[c] = 0;
                queue.Enqueue(c);
            }
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb < 0 || !inside.Contains(nb) || dist.ContainsKey(nb) || !g.IsWalkable(nb))
                        continue;
                    dist[nb] = dist[c] + 1;
                    queue.Enqueue(nb);
                }
            }
            var cells = new List<int>(dist.Keys);
            cells.Sort((a, b) => dist[b].CompareTo(dist[a]));
            int keep = Mathf.Max(1, cells.Count / 3);
            return cells.GetRange(0, Mathf.Min(keep, cells.Count));
        }

        /// <summary>
        /// Walkable cells reachable from the floor's arrival without crossing <paramref name="blocked"/> (teleport pads
        /// and moving platforms included).
        /// </summary>
        private bool[] Reachable(HashSet<int> blocked)
        {
            int[] dist = floor.Flood(floor.ArrivalCell, blocked);
            var seen = new bool[g.Count];
            for (int i = 0; i < dist.Length; i++)
                seen[i] = dist[i] >= 0;
            return seen;
        }

        /// <summary>
        /// True if closing <paramref name="extra"/> (on top of the locks already made) still lets players reach every cell
        /// they reached before, except the cells of <paramref name="behind"/> (the vault being locked).
        /// </summary>
        private bool StillReachable(IEnumerable<int> extra, Area behind)
        {
            bool[] before = Reachable(permanentlyBlocked);
            var blocked = new HashSet<int>(permanentlyBlocked);
            foreach (int c in extra)
                blocked.Add(c);
            bool[] after = Reachable(blocked);
            var allowed = behind != null ? new HashSet<int>(behind.Cells) : null;
            for (int i = 0; i < g.Count; i++)
            {
                if (!before[i] || after[i] || blocked.Contains(i))
                    continue;
                if (allowed != null && allowed.Contains(i))
                    continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// One gate per way into the area: in the doorway when the way in is a door, else on the room's own edge cell.
        /// Null when a way in can't take a gate (stairs, an occupied cell) or there are too many.
        /// </summary>
        private List<(int cell, Dir4 along)> GateCells(Area area, bool doorways)
        {
            var result = new List<(int, Dir4)>();
            var taken = new HashSet<int>();
            foreach (int c in area.Cells)
            {
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb < 0)
                        continue;
                    if (g.Type[nb] == CellType.Link)
                        return null;
                    if (!g.IsWalkable(nb) || g.Area[nb] == area.Id)
                        continue;
                    int cell = doorways && g.Type[nb] == CellType.Door && !g.Has(nb, CellFlags.Secret) ? nb : c;
                    if (taken.Contains(cell))
                        continue;
                    if (g.Has(cell, CellFlags.Occupied | CellFlags.Landing | CellFlags.Locked))
                        return null;
                    taken.Add(cell);
                    result.Add((cell, (Dir4)d));
                    if (result.Count > M.maxGatesPerRoom)
                        return null;
                }
            }
            return result.Count > 0 ? result : null;
        }

        private int AddLockPiece(PlacementKind kind, int cell, Dir4 along, int areaId, int link)
        {
            int index = Add(kind, PlacementTable.None, -1, cell, CellPos(cell), along.Yaw(), 0f, -1, 1f, 0, 0.5f);
            Placement pl = placements[index];
            pl.Area = areaId;
            pl.Link = link;
            placements[index] = pl;
            g.Set(cell, CellFlags.Locked);
            return index;
        }

        // ------------------------------------------------------------------ before the mobs: gates and locks

        /// <summary>Gates for event rooms and locked doors for vaults, decided before anything else is placed.</summary>
        private void ReserveLocks()
        {
            foreach (Area area in floor.Areas)
            {
                if (area.AnchorIndex >= 0)
                    continue;
                if (area.Role == AreaRole.Vault && M.vaultKeys)
                {
                    // Prefer the doorways; fall back to the vault's own edge when a doorway is shared with a passage.
                    List<(int cell, Dir4 along)> cells = GateCells(area, true);
                    if (cells == null || !StillReachable(Cells(cells), area))
                        cells = GateCells(area, false);
                    if (cells == null || !StillReachable(Cells(cells), area))
                        continue;   // can't be locked safely: an open treasure room
                    int key = floor.Index * 1000 + area.Id;
                    foreach (var c in cells)
                    {
                        AddLockPiece(PlacementKind.LockedDoor, c.cell, c.along, area.Id, key);
                        permanentlyBlocked.Add(c.cell);
                    }
                    vaultKeyOf[area.Id] = key;
                    continue;
                }
                if (!LocksRoom(area))
                    continue;
                List<(int cell, Dir4 along)> gates = GateCells(area, true) ?? GateCells(area, false);
                if (gates == null)
                    continue;
                var list = new List<int>();
                foreach (var c in gates)
                    list.Add(AddLockPiece(PlacementKind.Gate, c.cell, c.along, area.Id, area.Id));
                gatesOf[area.Id] = list;
            }
        }

        private static IEnumerable<int> Cells(List<(int cell, Dir4 along)> list)
        {
            foreach (var c in list)
                yield return c.cell;
        }

        // ------------------------------------------------------------------ elites

        /// <summary>Guardian and throne rooms: the toughest ordinary mob allowed there, bigger and a tier higher.</summary>
        private void PlaceElites()
        {
            if (p.Encounters.Count == 0)
                return;
            foreach (Area area in floor.Areas)
            {
                if ((area.Role != AreaRole.MiniBoss && area.Role != AreaRole.Throne) || Full)
                    continue;
                EncounterInfo elite = null;
                foreach (EncounterInfo e in p.Encounters)
                {
                    if (!EncounterAllowed(e, area, false))
                        continue;
                    if (elite == null || e.Cost > elite.Cost || (e.Cost == elite.Cost && e.Weight > elite.Weight))
                        elite = e;
                }
                if (elite == null)
                    continue;
                int best = -1;
                foreach (int c in area.Cells)
                    if (!Blocked(c) && !NearDoor(c) && (best < 0 || wall[c] > wall[best]))
                        best = c;
                if (best < 0)
                    continue;
                Vector2 pos = CellPos(best);
                int group = nextGroup++;
                int index = Add(PlacementKind.Mob, PlacementTable.Encounters, elite.Index, best, pos,
                    YawTowards(pos, floor.Areas[floor.ArrivalArea].Center), 0f, group, M.eliteScale, elite.Tier + M.eliteTierBonus, 1.8f);
                Placement pl = placements[index];
                pl.Elite = true;
                placements[index] = pl;
                mobs.Add(pos, group, 2f);
            }
        }

        // ------------------------------------------------------------------ after everything: keys, plates, events, shortcut

        private void PlaceMechanics()
        {
            PlaceKeys();
            foreach (Area area in floor.Areas)
            {
                if (area.AnchorIndex >= 0)
                    continue;
                if (area.Role == AreaRole.Puzzle)
                    PlacePuzzle(area);
                else if (area.Role == AreaRole.Colosseum)
                    AddController(area, RoomEventMode.PitFight, WaveCount(area));
                else if (gatesOf.ContainsKey(area.Id) || IsAmbush(area))
                    AddController(area, IsAmbush(area) ? RoomEventMode.Ambush : RoomEventMode.LockUntilCleared, WaveCount(area));
            }
            if (Features.Chance(M.shortcutChance))
                PlaceShortcut();
            PlaceRoomMechanics();
            PlaceShiftingWalls();
            PlaceRoamer();
        }

        private int WaveCount(Area area)
        {
            int waves = 0;
            foreach (Placement pl in placements)
                if (pl.Area == area.Id && pl.Kind == PlacementKind.Mob && pl.Wave > waves)
                    waves = pl.Wave;
            return waves;
        }

        private void AddController(Area area, RoomEventMode mode, int waves)
        {
            // Not a physical object: it doesn't occupy its cell.
            placements.Add(new Placement
            {
                Kind = PlacementKind.RoomController,
                Table = PlacementTable.None,
                Entry = -1,
                Floor = floor.Index,
                Area = area.Id,
                Cell = area.Center,
                Height = area.CenterCell >= 0 ? g.FloorHeight[area.CenterCell] : 0f,
                Group = -1,
                Scale = 1f,
                Tier = waves,
                Link = (int)mode,
            });
        }

        /// <summary>Each locked vault's key, somewhere reachable on the same floor - preferably guarded.</summary>
        private void PlaceKeys()
        {
            if (vaultKeyOf.Count == 0)
                return;
            bool[] reach = Reachable(permanentlyBlocked);
            foreach (var pair in vaultKeyOf)
            {
                Area vault = floor.Areas[pair.Key];
                Area best = null;
                float bestScore = float.MinValue;
                foreach (Area a in floor.Areas)
                {
                    if (a.Id == vault.Id || a.Role == AreaRole.Vault || a.Role == AreaRole.Secret || a.Role == AreaRole.Puzzle || a.Cells.Count == 0)
                        continue;
                    float score = Features.Range(0f, 2f);
                    switch (a.Role)
                    {
                        case AreaRole.MiniBoss:
                        case AreaRole.Throne:
                        case AreaRole.Arena:
                        case AreaRole.Ambush:
                            score += 10f;
                            break;
                        case AreaRole.TrapRoom:
                        case AreaRole.Prison:
                        case AreaRole.Crypt:
                            score += 6f;
                            break;
                        case AreaRole.Entrance:
                        case AreaRole.Exit:
                        case AreaRole.StairsUp:
                        case AreaRole.StairsDown:
                        case AreaRole.DropLanding:
                        case AreaRole.DropSource:
                            score -= 8f;
                            break;
                    }
                    foreach (Placement pl in placements)
                        if (pl.Area == a.Id && pl.Kind == PlacementKind.Mob)
                            score += 0.6f;
                    // Somewhere the players have to go out of their way for.
                    score += a.IsLeaf ? 2f : 0f;
                    if (score > bestScore && HasKeySpot(a, reach))
                    {
                        bestScore = score;
                        best = a;
                    }
                }
                if (best == null)
                    continue;
                int cell = -1;
                foreach (int c in best.Cells)
                    if (reach[c] && !Blocked(c) && !NearDoor(c) && wall[c] >= 1f && (cell < 0 || wall[c] > wall[cell]))
                        cell = c;
                if (cell < 0)
                    continue;
                int index = Add(PlacementKind.Key, PlacementTable.None, -1, cell, CellPos(cell), Features.Range(0f, 360f), 0f, -1, 1f, 0, 0.6f);
                Placement pl2 = placements[index];
                pl2.Link = pair.Value;
                placements[index] = pl2;
            }
        }

        private bool HasKeySpot(Area a, bool[] reach)
        {
            foreach (int c in a.Cells)
                if (reach[c] && !Blocked(c) && !NearDoor(c) && wall[c] >= 1f)
                    return true;
            return false;
        }

        /// <summary>Pressure plates (Link = their place in the order) and the room's puzzle controller.</summary>
        private void PlacePuzzle(Area area)
        {
            int wanted = Mathf.Max(2, M.puzzlePlates.Random(Features));
            var candidates = new List<int>();
            foreach (int c in area.Cells)
                if (!Blocked(c) && !NearDoor(c) && wall[c] >= 1.4f)
                    candidates.Add(c);
            Features.Shuffle(candidates);
            var chosen = new List<Vector2>();
            int order = 0;
            foreach (int c in candidates)
            {
                if (order >= wanted)
                    break;
                Vector2 pos = CellPos(c);
                bool clear = true;
                foreach (Vector2 o in chosen)
                    if ((o - pos).sqrMagnitude < 2.2f * 2.2f)
                        clear = false;
                if (!clear || occupied.AnyWithin(pos, 1f))
                    continue;
                int index = Add(PlacementKind.Switch, PlacementTable.None, -1, c, pos, 0f, 0f, -1, 1f, 0, 0.7f);
                Placement pl = placements[index];
                pl.Link = order++;
                placements[index] = pl;
                chosen.Add(pos);
            }
            AddController(area, RoomEventMode.Puzzle, 0);
        }

        /// <summary>
        /// A door on a loop that opens only from its far side (the side farther from the floor's way in): reach it the long
        /// way round and it becomes a quick way back. Never on the main route, never cutting anything off.
        /// </summary>
        private void PlaceShortcut()
        {
            var options = new List<Connection>();
            foreach (Connection c in floor.Connections)
                if (c.IsLoop && !c.OnMainPath && !c.Failed && !c.IsRepair && c.Kind != ConnectionKind.Secret && (c.DoorA >= 0 || c.DoorB >= 0))
                    options.Add(c);
            Features.Shuffle(options);
            foreach (Connection c in options)
            {
                int door = c.DoorA >= 0 ? c.DoorA : c.DoorB;
                if (g.Type[door] != CellType.Door || g.Has(door, CellFlags.Occupied | CellFlags.Locked | CellFlags.Secret))
                    continue;
                // The two sides of the doorway along its passage.
                int sideA = -1, sideB = -1;
                Dir4 along = Dir4.North;
                for (int d = 0; d < 2; d++)
                {
                    int n1 = g.Neighbor(door, d), n2 = g.Neighbor(door, d + 2);
                    if (n1 >= 0 && n2 >= 0 && g.IsWalkable(n1) && g.IsWalkable(n2))
                    {
                        sideA = n1;
                        sideB = n2;
                        along = (Dir4)d;
                    }
                }
                if (sideA < 0 || reach[sideA] < 0 || reach[sideB] < 0 || !StillReachable(new[] { door }, null))
                    continue;
                // It opens from the side that is farther from the way in.
                Dir4 openFrom = reach[sideA] >= reach[sideB] ? along : along.Opposite();
                AddLockPiece(PlacementKind.Shortcut, door, openFrom, g.Area[door], -1);
                permanentlyBlocked.Add(door);
                return;
            }
        }
    }
}
