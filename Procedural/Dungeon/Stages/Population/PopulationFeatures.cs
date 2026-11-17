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
            EnsureRevealers();
        }

        /// <summary>
        /// Nothing stays hidden for good. Whatever was placed hidden - a boss room's chest, an ambush's waves, a cellar's
        /// stash - needs something in its room that shows it: a room event, the nests of a nest room, the altar it belongs
        /// to, barrels to break. A room that has none (one too open for gates, an entrance or stairs room, a nest room
        /// with no place for a nest, a puzzle room with no place for its plates) gets a room event without gates: its
        /// fight still runs and its reward appears when the room is clear - at once when nothing guards it. What is hidden
        /// outside every room is simply shown.
        /// </summary>
        private void EnsureRevealers()
        {
            var controlled = new HashSet<int>();
            var nested = new HashSet<int>();
            var breakable = new HashSet<int>();
            var altars = new HashSet<(int area, int link)>();
            foreach (Placement pl in placements)
            {
                if (pl.Area < 0)
                    continue;
                if (pl.Kind == PlacementKind.RoomController)
                    controlled.Add(pl.Area);
                else if (pl.Kind == PlacementKind.Nest)
                    nested.Add(pl.Area);
                DungeonPrimitive kind = PlaceholderOf(pl);
                if (!pl.Dormant && (kind == DungeonPrimitive.Barrel || kind == DungeonPrimitive.Crate || kind == DungeonPrimitive.WineBarrel))
                    breakable.Add(pl.Area);
                else if ((kind == DungeonPrimitive.BloodAltar || kind == DungeonPrimitive.CursedAltar) && pl.Link != 0)
                    altars.Add((pl.Area, pl.Link));
            }

            var rooms = new List<int>();
            for (int i = 0; i < placements.Count; i++)
            {
                Placement pl = placements[i];
                if (!pl.Dormant)
                    continue;
                bool mob = pl.Kind == PlacementKind.Mob || pl.Kind == PlacementKind.Boss;
                bool stash = pl.Kind == PlacementKind.Loot && pl.Link == PlacementLinks.BarrelStash;
                if (pl.Area < 0 || (stash && !breakable.Contains(pl.Area) && !controlled.Contains(pl.Area)))
                {
                    // Nothing could ever show it: it is simply there.
                    pl.Dormant = false;
                    pl.Wave = 0;
                    if (stash)
                        pl.Link = 0;
                    placements[i] = pl;
                    continue;
                }
                if (controlled.Contains(pl.Area) || stash)
                    continue;
                if (!mob && (nested.Contains(pl.Area) || (pl.Kind == PlacementKind.Loot && pl.Link != 0 && altars.Contains((pl.Area, pl.Link)))))
                    continue;
                if (!rooms.Contains(pl.Area))
                    rooms.Add(pl.Area);
            }
            foreach (int id in rooms)
            {
                Area area = floor.Areas[id];
                RoomEventMode mode = area.Role == AreaRole.Colosseum ? RoomEventMode.PitFight
                    : IsAmbush(area) ? RoomEventMode.Ambush : RoomEventMode.LockUntilCleared;
                AddController(area, mode, WaveCount(area));
            }
        }

        /// <summary>The stand-in kind of a prop or loot placement (what mechanics it has), None for everything else.</summary>
        private DungeonPrimitive PlaceholderOf(Placement pl)
        {
            if (pl.Table == PlacementTable.Props && pl.Entry >= 0 && pl.Entry < p.Props.Count)
                return p.Props[pl.Entry].Placeholder;
            if (pl.Table == PlacementTable.Loot && pl.Entry >= 0 && pl.Entry < p.Loot.Count)
                return p.Loot[pl.Entry].Placeholder;
            return DungeonPrimitive.None;
        }

        /// <summary>
        /// Spike traps standing where the way squeezes through one cell are marked to run on a cycle (the builder gives
        /// them their rhythm): a trap nobody can walk around must leave a moment to pass.
        /// </summary>
        private void TimeUnavoidableTraps()
        {
            if (!M.timeUnavoidableTraps)
                return;
            for (int i = 0; i < placements.Count; i++)
            {
                Placement pl = placements[i];
                if (pl.Table != PlacementTable.Props || pl.Link != 0 || PlaceholderOf(pl) != DungeonPrimitive.SpikeTrap)
                    continue;
                Vector2Int at = Vector2Int.FloorToInt(pl.Cell);
                if (!g.InBounds(at) || !Pinch(g.Index(at)))
                    continue;
                pl.Link = PlacementLinks.TimedTrap;
                placements[i] = pl;
            }
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

        /// <summary>
        /// Pressure plates (Link = their place in the order) and the room's puzzle controller. A puzzle needs at least two
        /// plates and something to win: a room with no place for them stays an ordinary room (whatever it hid is shown
        /// when the players come in, see <see cref="EnsureRevealers"/>).
        /// </summary>
        private void PlacePuzzle(Area area)
        {
            int wanted = Mathf.Max(2, M.puzzlePlates.Random(Features));
            // Plates with room around them; a crowded or narrow room takes them nearer its walls and each other.
            List<int> plates = PlateCells(area, wanted, 1.4f, 2.2f, 1f);
            if (plates.Count < 2)
                plates = PlateCells(area, wanted, 1f, 1.6f, 0.2f);
            if (plates.Count < 2 || !PuzzleReward(area, plates))
                return;
            for (int order = 0; order < plates.Count; order++)
            {
                int index = Add(PlacementKind.Switch, PlacementTable.None, -1, plates[order], CellPos(plates[order]), 0f, 0f, -1, 1f, 0, 0.7f);
                Placement pl = placements[index];
                pl.Link = order;
                placements[index] = pl;
            }
            AddController(area, RoomEventMode.Puzzle, 0);
        }

        /// <summary>
        /// Up to <paramref name="wanted"/> free cells for pressure plates: <paramref name="wallDistance"/> from the walls,
        /// <paramref name="spacing"/> apart, nothing within <paramref name="clearance"/> of them.
        /// </summary>
        private List<int> PlateCells(Area area, int wanted, float wallDistance, float spacing, float clearance)
        {
            var candidates = new List<int>();
            foreach (int c in area.Cells)
                if (!Blocked(c) && !NearDoor(c) && wall[c] >= wallDistance)
                    candidates.Add(c);
            Features.Shuffle(candidates);
            var chosen = new List<int>();
            foreach (int c in candidates)
            {
                if (chosen.Count >= wanted)
                    break;
                Vector2 pos = CellPos(c);
                bool clear = true;
                foreach (int o in chosen)
                    if ((CellPos(o) - pos).sqrMagnitude < spacing * spacing)
                        clear = false;
                if (clear && !occupied.AnyWithin(pos, clearance))
                    chosen.Add(c);
            }
            return chosen;
        }

        /// <summary>
        /// The puzzle's prize: the room's hidden loot - or, when the room got none, one put there now (away from the
        /// plates). False when there is none and no place for one.
        /// </summary>
        private bool PuzzleReward(Area area, List<int> plates)
        {
            foreach (Placement pl in placements)
                if (pl.Area == area.Id && pl.Dormant && pl.Kind != PlacementKind.Mob && pl.Kind != PlacementKind.Boss)
                    return true;
            LootInfo entry = PickLoot(area, 1);
            if (entry == null)
                return false;
            int best = -1;
            for (int pass = 0; pass < 2 && best < 0; pass++)
                foreach (int c in area.Cells)
                {
                    if (Blocked(c) || NearDoor(c) || (pass == 0 && occupied.AnyWithin(CellPos(c), 0.2f)))
                        continue;
                    bool onPlates = false;
                    foreach (int plate in plates)
                        onPlates |= (CellPos(plate) - CellPos(c)).sqrMagnitude < 1.5f * 1.5f;
                    if (!onPlates && (best < 0 || wall[c] < wall[best]))
                        best = c;   // by a wall, out of the way of the plates
                }
            if (best < 0)
                return false;
            Vector2 pos = CellPos(best);
            int index = Add(PlacementKind.Loot, PlacementTable.Loot, entry.Index, best, pos, YawTowards(pos, area.Center), 0f, -1, 1f, entry.Tier, 0.8f);
            SetDormant(index, 0);
            loot.Add(pos, entry.Index, 0.8f);
            return true;
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
