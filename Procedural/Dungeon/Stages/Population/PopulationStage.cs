using System.Collections.Generic;
using ProceduralCommon;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Stage 8. Decides what goes where - as data only, the build step creates the objects:
    /// the entrance portal against the entrance room's back wall with the player spawn in front of it, the exit
    /// portal, the gates and locked doors of event rooms and vaults, bosses in boss rooms, elites in guardian and throne
    /// rooms, mob packs spent from a per-area budget (difficulty x progress x size x role, never near the spawn, never in
    /// doorways - ambush waves hidden until sprung), loot (treasure rooms, bosses, vaults, challenge rooms, dead ends),
    /// props from the prop table (lights, points of interest and themed furniture in special rooms, traps, the floor
    /// modifier's hazards, decorations), and finally the mechanics: vault keys, puzzle plates, room events and shortcut
    /// doors (see PopulationFeatures.cs).
    /// </summary>
    public sealed class PopulationStage : IDungeonStage
    {
        public string Name => "Population";

        public void Run(DungeonContext ctx)
        {
            DungeonLayout layout = ctx.Layout;
            var perFloor = new List<Placement>[layout.Floors.Count];
            ctx.ForEachFloor(floor => perFloor[floor.Index] = new FloorPopulator(ctx, floor).Run());
            foreach (List<Placement> list in perFloor)
                layout.Placements.AddRange(list);
            if (!layout.PlayerSpawn.Valid)
                ctx.Fail("No player spawn could be placed.");
        }
    }

    internal sealed partial class FloorPopulator
    {
        private readonly DungeonContext ctx;
        private readonly FloorLayout floor;
        private readonly TileGrid g;
        private readonly CompiledProfile p;
        private readonly PopulationSettings pop;
        private readonly DungeonRandom rng;
        private readonly List<Placement> placements = new List<Placement>();
        private readonly SpatialHash2D<int> occupied = new SpatialHash2D<int>(4f);
        private readonly SpatialHash2D<int> mobs = new SpatialHash2D<int>(4f);
        private readonly SpatialHash2D<int> loot = new SpatialHash2D<int>(4f);
        private readonly Dictionary<int, SpatialHash2D<int>> propHashes = new Dictionary<int, SpatialHash2D<int>>();
        private readonly float[] wall;
        private readonly int[] reach;
        private int nextGroup;

        public FloorPopulator(DungeonContext ctx, FloorLayout floor)
        {
            this.ctx = ctx;
            this.floor = floor;
            g = floor.Grid;
            p = ctx.Profile;
            pop = p.Population;
            rng = ctx.Random("Population", floor.Index);
            wall = floor.WallDistance;
            reach = floor.DistanceFromArrival;
            nextGroup = floor.Index * 100000;
        }

        public List<Placement> Run()
        {
            if (floor.Index == 0)
                PlaceEntrance();
            if (floor.Spec.IsLast)
                PlaceExit();
            ReserveLocks();
            ReserveCrossings();
            PlaceBosses();
            PlaceElites();
            PlaceEncounters();
            PlaceLoot();
            PlaceRoomFixtures();
            PlaceProps();
            PlaceMechanics();
            PlaceTraps();
            return placements;
        }

        private bool Full => placements.Count >= pop.maxPlacementsPerFloor;

        // ------------------------------------------------------------------ helpers

        private Vector2 CellPos(int cell) => g.Center(cell);

        private static float YawTowards(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            return d.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
        }

        private int Add(PlacementKind kind, PlacementTable table, int entry, int cell, Vector2 pos, float yaw, float heightOffset = 0f, int group = -1, float scale = 1f, int tier = 0, float radius = 0.8f)
        {
            int area = g.Area[cell];
            placements.Add(new Placement
            {
                Kind = kind,
                Table = table,
                Entry = entry,
                Floor = floor.Index,
                Area = area,
                Cell = pos,
                Height = g.FloorHeight[cell] + heightOffset,
                Yaw = yaw,
                Group = group,
                Scale = scale,
                Tier = tier,
            });
            occupied.Add(pos, placements.Count - 1, radius);
            g.Set(cell, CellFlags.Occupied);
            return placements.Count - 1;
        }

        /// <summary>Cells of an area that open onto something else (doors, corridors, other areas, stairs).</summary>
        private List<int> Openings(Area area)
        {
            var list = new List<int>();
            foreach (int c in area.Cells)
            {
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb < 0)
                        continue;
                    bool outside = (g.IsWalkable(nb) && g.Area[nb] != area.Id) || g.Type[nb] == CellType.Link;
                    if (outside)
                    {
                        list.Add(c);
                        break;
                    }
                }
            }
            return list;
        }

        /// <summary>Direction of a rock 4-neighbour (a wall to put something against - not a chasm's edge), or -1.</summary>
        private int WallSide(int cell)
        {
            for (int d = 0; d < 4; d++)
            {
                int nb = g.Neighbor(cell, d);
                if (nb >= 0 && g.Type[nb] == CellType.Solid && !g.IsChasm(nb))
                    return d;
            }
            return -1;
        }

        private bool NearDoor(int cell)
        {
            if (g.Type[cell] == CellType.Door)
                return true;
            for (int d = 0; d < 4; d++)
            {
                int nb = g.Neighbor(cell, d);
                if (nb >= 0 && (g.Type[nb] == CellType.Door || g.Type[nb] == CellType.Link))
                    return true;
            }
            return false;
        }

        private bool Blocked(int cell) => g.Has(cell, CellFlags.Occupied | CellFlags.Landing) || g.Type[cell] != CellType.Floor;

        /// <summary>Picks the back-wall cell of an area (farthest from its openings) for a portal.</summary>
        private int BackWallCell(Area area)
        {
            List<int> openings = Openings(area);
            int best = -1;
            float bestScore = float.MinValue;
            foreach (int c in area.Cells)
            {
                if (Blocked(c) || NearDoor(c) || wall[c] < 1f || wall[c] >= 1.6f || WallSide(c) < 0)
                    continue;
                float nearest = float.MaxValue;
                Vector2 pc = CellPos(c);
                foreach (int o in openings)
                    nearest = Mathf.Min(nearest, (CellPos(o) - pc).sqrMagnitude);
                // Prefer the back wall, and the middle of it (a little towards the room centre).
                float score = nearest - 0.15f * (pc - area.Center).sqrMagnitude;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best >= 0 ? best : area.CenterCell;
        }

        // ------------------------------------------------------------------ portals and spawn

        private void PlaceEntrance()
        {
            Area area = floor.Areas[floor.ArrivalArea];
            int portalCell = BackWallCell(area);
            Vector2 portalPos = CellPos(portalCell);
            int side = WallSide(portalCell);
            if (side >= 0)
                portalPos += (Vector2)((Dir4)side).Delta() * 0.25f;
            Vector2 into = (area.Center - portalPos).sqrMagnitude > 0.01f ? (area.Center - portalPos).normalized : (Vector2)floor.Anchors[area.AnchorIndex].Facing.Delta();
            float yaw = YawTowards(portalPos, portalPos + into);

            // The spawn: a clear cell a few steps in front of the portal.
            Vector2 target = portalPos + into * 2.6f;
            int spawnCell = -1;
            float best = float.MaxValue;
            foreach (int c in area.Cells)
            {
                if (c == portalCell || Blocked(c) || wall[c] < 1.4f)
                    continue;
                float d = (CellPos(c) - target).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    spawnCell = c;
                }
            }
            if (spawnCell < 0)
                spawnCell = area.CenterCell;

            Add(PlacementKind.EntrancePortal, PlacementTable.None, -1, portalCell, portalPos, yaw, 0f, -1, 1f, 0, 1.6f);
            ctx.Layout.EntrancePortal = new DungeonPose(0, portalPos, g.FloorHeight[portalCell], yaw);
            Vector2 spawnPos = CellPos(spawnCell);
            Add(PlacementKind.PlayerSpawn, PlacementTable.None, -1, spawnCell, spawnPos, yaw, 0f, -1, 1f, 0, 1.6f);
            ctx.Layout.PlayerSpawn = new DungeonPose(0, spawnPos, g.FloorHeight[spawnCell], yaw);
        }

        private void PlaceExit()
        {
            Area area = floor.Areas[floor.DepartureArea];
            int portalCell = BackWallCell(area);
            Vector2 portalPos = CellPos(portalCell);
            int side = WallSide(portalCell);
            if (side >= 0)
                portalPos += (Vector2)((Dir4)side).Delta() * 0.25f;
            float yaw = YawTowards(portalPos, area.Center);
            Add(PlacementKind.ExitPortal, PlacementTable.None, -1, portalCell, portalPos, yaw, 0f, -1, 1f, 0, 1.6f);
            ctx.Layout.ExitPortal = new DungeonPose(floor.Index, portalPos, g.FloorHeight[portalCell], yaw);
        }

        // ------------------------------------------------------------------ encounters

        private bool EncounterAllowed(EncounterInfo e, Area area, bool boss)
        {
            if (e.Boss != boss)
                return false;
            if (floor.Index < e.MinFloor || (e.MaxFloor >= 0 && floor.Index > e.MaxFloor))
                return false;
            if (area != null)
            {
                if (!e.Progress.Contains(area.Progress) || !e.Styles.Contains(area.Style))
                    return false;
                if (e.Roles.Length > 0 && System.Array.IndexOf(e.Roles, area.Role) < 0)
                    return false;
            }
            return true;
        }

        private EncounterInfo PickEncounter(Area area, bool boss, int maxCost)
        {
            var options = new List<EncounterInfo>();
            var weights = new List<float>();
            foreach (EncounterInfo e in p.Encounters)
            {
                if (!EncounterAllowed(e, area, boss) || e.Cost > maxCost)
                    continue;
                options.Add(e);
                weights.Add(e.Weight);
            }
            int pick = rng.WeightedIndex(weights);
            return pick < 0 ? null : options[pick];
        }

        private bool InSafeZone(int cell)
        {
            float radius = floor.Index == 0 ? pop.safeRadius : pop.safeRadius * 0.5f;
            return reach[cell] < 0 || reach[cell] < radius;
        }

        private void PlaceBosses()
        {
            foreach (Area area in floor.Areas)
            {
                if (area.Role != AreaRole.Boss)
                    continue;
                EncounterInfo e = PickEncounter(area, true, int.MaxValue);
                if (e == null)
                    continue;
                int best = -1;
                foreach (int c in area.Cells)
                    if (!Blocked(c) && (best < 0 || wall[c] > wall[best]))
                        best = c;
                if (best < 0)
                    continue;
                Vector2 pos = CellPos(best);
                int group = nextGroup++;
                Add(PlacementKind.Boss, PlacementTable.Encounters, e.Index, best, pos, YawTowards(pos, floor.Areas[floor.ArrivalArea].Center), 0f, group, 1f, e.Tier, 2.5f);
                mobs.Add(pos, group, 3f);
            }
        }

        private static float RoleMultiplier(Area area)
        {
            switch (area.Role)
            {
                case AreaRole.Entrance:
                case AreaRole.Exit:
                case AreaRole.Rest:
                case AreaRole.StairsUp:
                case AreaRole.DropLanding:
                case AreaRole.Boss:
                    return 0f;
                case AreaRole.StairsDown:
                case AreaRole.DropSource:
                    return 0.4f;
                case AreaRole.Secret:
                    return 0.6f;
                case AreaRole.Treasure:
                    return 1.3f;
                case AreaRole.Arena:
                    return 2.2f;
                case AreaRole.TrapRoom:
                case AreaRole.Puzzle:
                    return 0f;
                case AreaRole.Ambush:
                    return 2f;
                case AreaRole.MiniBoss:
                case AreaRole.Throne:
                    return 0.6f;
                case AreaRole.Vault:
                    return 0.4f;
                case AreaRole.Garden:
                    return 0.6f;
                case AreaRole.Library:
                    return 0.7f;
                case AreaRole.Laboratory:
                    return 0.8f;
                case AreaRole.Armory:
                    return 1.1f;
                case AreaRole.Prison:
                    return 1.2f;
                case AreaRole.Nest:
                    return 0.5f;
                case AreaRole.Gambling:
                case AreaRole.GasChamber:
                case AreaRole.MapRoom:
                    return 0f;
                case AreaRole.Kitchen:
                    return 0.9f;
                case AreaRole.Gallery:
                case AreaRole.Greenhouse:
                case AreaRole.WineCellar:
                    return 0.6f;
                case AreaRole.Barracks:
                    return 1.5f;
                case AreaRole.Colosseum:
                    return 2.4f;
                default:
                    return area.Kind == AreaKind.Hall ? 1.2f : 1f;
            }
        }

        private void PlaceEncounters()
        {
            if (p.Encounters.Count == 0)
                return;

            float floorBonus = floor.Spec.Modifier == FloorModifier.Darkness ? 1f + Mathf.Max(0f, p.FloorModifiers.darkEncounterBonus) : 1f;
            foreach (Area area in floor.Areas)
            {
                if (Full)
                    return;
                float mult = RoleMultiplier(area);
                if (mult <= 0f || !TemplateAllowsPopulation(area))
                    continue;
                int firstPlacement = placements.Count;
                float budget = pop.encounterDensity * area.Cells.Count / 100f * area.Difficulty * mult * floorBonus * (area.OnMainPath ? 1f : 0.85f);
                int whole = Mathf.FloorToInt(budget);
                if (rng.Value() < budget - whole)
                    whole++;
                if (whole <= 0)
                    continue;

                var cells = new List<int>(area.Cells);
                rng.Shuffle(cells);
                int guard = 0;
                while (whole > 0 && guard++ < 32 && !Full)
                {
                    EncounterInfo e = PickEncounter(area, false, whole);
                    if (e == null)
                        break;
                    int size = Mathf.Max(1, Mathf.Min(e.PackSize.Random(rng), whole / e.Cost));
                    int placed = PlacePack(e, cells, size);
                    if (placed == 0)
                        break;
                    whole -= e.Cost * placed;
                }
                if (area.Role == AreaRole.Colosseum)
                    PitFight(area, firstPlacement);
                else if (IsAmbush(area))
                    MakeWaves(firstPlacement);
                else if (area.Role == AreaRole.Barracks)
                    PutToSleep(firstPlacement);
            }

            // Wandering mobs in long corridors.
            foreach (Connection c in floor.Connections)
            {
                if (Full || c.Failed || c.Cells.Count < 8 || c.Kind == ConnectionKind.Bridge || !rng.Chance(pop.corridorEncounterChance))
                    continue;
                Area near = floor.Areas[c.A];
                EncounterInfo e = PickEncounter(near, false, 2);
                if (e == null)
                    continue;
                var cells = new List<int>();
                for (int k = 2; k < c.Cells.Count - 2; k++)
                    cells.Add(c.Cells[k]);
                rng.Shuffle(cells);
                PlacePack(e, cells, 1);
            }
        }

        private int PlacePack(EncounterInfo e, List<int> cells, int size)
        {
            int center = -1;
            foreach (int c in cells)
            {
                if (Blocked(c) || g.Has(c, CellFlags.Chokepoint) || NearDoor(c) || InSafeZone(c) || wall[c] < e.Clearance)
                    continue;
                Vector2 pos = CellPos(c);
                if (mobs.AnyWithin(pos, pop.packSpacing) || occupied.AnyWithin(pos, 1.5f))
                    continue;
                center = c;
                break;
            }
            if (center < 0)
                return 0;

            int group = nextGroup++;
            Vector2 centerPos = CellPos(center);
            float facing = rng.Range(0f, 360f);
            Add(PlacementKind.Mob, PlacementTable.Encounters, e.Index, center, centerPos, facing, 0f, group, 1f, e.Tier, 0.6f);
            mobs.Add(centerPos, group, 0.6f);
            int placed = 1;

            int r = Mathf.CeilToInt(e.PackRadius);
            var around = new List<int>();
            int cx = g.X(center), cy = g.Y(center);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    int x = cx + dx, y = cy + dy;
                    if (!g.InBounds(x, y))
                        continue;
                    int c = g.Index(x, y);
                    if (dx * dx + dy * dy <= e.PackRadius * e.PackRadius && reach[c] >= 0)
                        around.Add(c);
                }
            rng.Shuffle(around);
            foreach (int c in around)
            {
                if (placed >= size)
                    break;
                if (Blocked(c) || NearDoor(c) || wall[c] < e.Clearance * 0.9f || InSafeZone(c))
                    continue;
                Vector2 pos = CellPos(c) + new Vector2(rng.Range(-0.2f, 0.2f), rng.Range(-0.2f, 0.2f));
                if (occupied.AnyWithin(pos, 1.1f))
                    continue;
                Add(PlacementKind.Mob, PlacementTable.Encounters, e.Index, c, pos, facing + rng.Range(-40f, 40f), 0f, group, 1f, e.Tier, 0.6f);
                placed++;
            }
            return placed;
        }

        private bool TemplateAllowsPopulation(Area area)
        {
            return area.TemplateIndex < 0 || p.Templates[area.TemplateIndex].AllowPopulation;
        }

        // ------------------------------------------------------------------ loot

        private void PlaceLoot()
        {
            if (p.Loot.Count == 0)
                return;
            foreach (Area area in floor.Areas)
            {
                if (Full)
                    return;
                if (!TemplateAllowsPopulation(area))
                    continue;
                int count = 0, tierBonus = 0;
                bool dormant = false, farEnd = false;
                switch (area.Role)
                {
                    case AreaRole.Treasure: count = pop.treasureRoomLoot.Random(rng); tierBonus = 1; break;
                    case AreaRole.Boss: count = pop.bossLoot.Random(rng); tierBonus = 2; dormant = LocksRoom(area); break;
                    case AreaRole.Secret: count = rng.Range(1, 3); tierBonus = 1; break;
                    case AreaRole.Vault: count = pop.vaultLoot.Random(rng); tierBonus = 2; break;
                    case AreaRole.TrapRoom: count = pop.challengeLoot.Random(rng); tierBonus = 1; farEnd = true; break;
                    case AreaRole.Puzzle: count = pop.challengeLoot.Random(rng); tierBonus = 1; dormant = true; break;
                    case AreaRole.Ambush:
                    case AreaRole.MiniBoss:
                    case AreaRole.Throne:
                        count = pop.challengeLoot.Random(rng); tierBonus = 1; dormant = LocksRoom(area); break;
                    case AreaRole.Crypt: count = 1; tierBonus = 1; dormant = LocksRoom(area); break;
                    case AreaRole.Armory: count = rng.Range(1, 3); break;
                    case AreaRole.Laboratory: count = 1; break;
                    case AreaRole.Library:
                    case AreaRole.Prison:
                    case AreaRole.Kitchen:
                    case AreaRole.Gallery:
                    case AreaRole.Greenhouse:
                        count = rng.Chance(0.5f) ? 1 : 0; break;
                    case AreaRole.Barracks: count = rng.Range(1, 3); break;
                    case AreaRole.Nest: count = pop.challengeLoot.Random(rng); tierBonus = 1; dormant = true; break;
                    case AreaRole.Colosseum: count = pop.challengeLoot.Random(rng) + 1; tierBonus = 2; dormant = LocksRoom(area); break;
                    case AreaRole.GasChamber: count = pop.challengeLoot.Random(rng); tierBonus = 1; farEnd = true; break;
                    case AreaRole.None:
                        if (area.IsLeaf ? rng.Chance(pop.deadEndLootChance) : rng.Chance(pop.roomLootChance))
                            count = 1;
                        break;
                }
                List<int> spots = farEnd ? FarEnd(area) : area.Cells;
                for (int k = 0; k < count && !Full; k++)
                {
                    LootInfo entry = PickLoot(area, tierBonus);
                    if (entry == null)
                        break;
                    if (!FindSpot(spots, farEnd ? PropPlacement.Anywhere : entry.Placement, loot, 2f, 0f, out int cell, out Vector2 pos, out float yaw) &&
                        !(farEnd && FindSpot(area.Cells, entry.Placement, loot, 2f, 0f, out cell, out pos, out yaw)))
                        break;
                    int index = Add(PlacementKind.Loot, PlacementTable.Loot, entry.Index, cell, pos, yaw, 0f, -1, 1f, entry.Tier, 0.8f);
                    if (dormant)
                        SetDormant(index, 0);
                    loot.Add(pos, entry.Index, 0.8f);
                }
            }
        }

        private LootInfo PickLoot(Area area, int tierBonus)
        {
            int maxTier = Mathf.FloorToInt(area.Progress * 2f + tierBonus + (floor.Spec.Difficulty - 1f));
            var options = new List<LootInfo>();
            var weights = new List<float>();
            foreach (LootInfo e in p.Loot)
            {
                if (floor.Index < e.MinFloor || (e.MaxFloor >= 0 && floor.Index > e.MaxFloor))
                    continue;
                if (!e.Progress.Contains(area.Progress) || !e.Styles.Contains(area.Style) || e.Tier > Mathf.Max(0, maxTier))
                    continue;
                options.Add(e);
                weights.Add(e.Weight * (1f + e.Tier * tierBonus * 0.5f));
            }
            int pick = rng.WeightedIndex(weights);
            return pick < 0 ? null : options[pick];
        }

        // ------------------------------------------------------------------ props

        private void PlaceProps()
        {
            foreach (PropInfo e in p.Props)
            {
                if (Full)
                    return;
                if (floor.Index < e.MinFloor || (e.MaxFloor >= 0 && floor.Index > e.MaxFloor))
                    continue;
                if (!e.Modifiers.Allows(floor.Spec.Modifier))
                    continue;
                if (!propHashes.TryGetValue(e.Index, out SpatialHash2D<int> hash))
                    propHashes[e.Index] = hash = new SpatialHash2D<int>(Mathf.Max(2f, e.Spacing));

                bool corridorUnits = e.Placement == PropPlacement.Corridor || e.Placement == PropPlacement.Transition || e.Placement == PropPlacement.Chokepoint;
                if (corridorUnits)
                {
                    if (e.Roles.Length > 0)
                        continue;
                    foreach (Connection c in floor.Connections)
                    {
                        if (c.Failed || c.Cells.Count == 0)
                            continue;
                        ZoneStyle style = c.Kind == ConnectionKind.Tunnel ? ZoneStyle.Cavern : (c.Kind == ConnectionKind.Breach ? ZoneStyle.Ruins : ZoneStyle.Built);
                        if (e.Placement != PropPlacement.Transition && !e.Styles.Contains(style))
                            continue;
                        if (e.Placement == PropPlacement.Transition && c.Kind != ConnectionKind.Breach)
                            continue;
                        float progress = (floor.Areas[c.A].Progress + floor.Areas[c.B].Progress) * 0.5f;
                        if (!e.Progress.Contains(progress) || !MainPathOk(e.MainPath, c.OnMainPath))
                            continue;
                        PlaceUnit(e, c.Cells, hash, DarkLightFactor(e, null));
                    }
                }
                else
                {
                    foreach (Area area in floor.Areas)
                    {
                        if (!TemplateAllowsPopulation(area))
                            continue;
                        if (e.Roles.Length > 0 && System.Array.IndexOf(e.Roles, area.Role) < 0)
                            continue;
                        if (!string.IsNullOrEmpty(e.AreaTag) && e.AreaTag != area.Tag)
                            continue;
                        if (!e.Styles.Contains(area.Style) || !e.Progress.Contains(area.Progress) || !MainPathOk(e.MainPath, area.OnMainPath))
                            continue;
                        if (e.Placement == PropPlacement.DeadEnd && !area.IsLeaf)
                            continue;
                        PlaceUnit(e, area.Cells, hash, DarkLightFactor(e, area));
                    }
                }
            }
        }

        private static bool MainPathOk(MainPathFilter filter, bool onMainPath)
        {
            return filter == MainPathFilter.Any || (filter == MainPathFilter.OnlyMainPath) == onMainPath;
        }

        /// <summary>Dark floors keep only a share of their lights (special rooms keep theirs).</summary>
        private float DarkLightFactor(PropInfo e, Area area)
        {
            if (e.Kind != PlacementKind.Light || floor.Spec.Modifier != FloorModifier.Darkness)
                return 1f;
            if (area != null && (area.Role == AreaRole.Boss || area.Role == AreaRole.Rest || area.Role == AreaRole.Shrine ||
                                 area.Role == AreaRole.Entrance || area.Role == AreaRole.Exit))
                return 1f;
            return Mathf.Clamp01(p.FloorModifiers.darkLightShare);
        }

        private void PlaceUnit(PropInfo e, List<int> cells, SpatialHash2D<int> hash, float chanceFactor = 1f)
        {
            if (!rng.Chance(e.Chance * chanceFactor))
                return;
            float extra = e.PerHundredCells * cells.Count / 100f;
            int count = e.PerArea.Random(rng) + Mathf.FloorToInt(extra) + (rng.Value() < extra - Mathf.Floor(extra) ? 1 : 0);
            for (int k = 0; k < count && !Full; k++)
            {
                if (!FindSpot(cells, e.Placement, hash, e.Spacing, e.AwayFromMobs, out int cell, out Vector2 pos, out float yaw))
                    return;
                Add(e.Kind, PlacementTable.Props, e.Index, cell, pos, yaw, e.HeightOffset, -1, e.Scale.Random(rng), 0, 0.7f);
                hash.Add(pos, e.Index, 0.5f);
            }
        }

        /// <summary>Finds a free spot among <paramref name="cells"/> for a placement rule.</summary>
        private bool FindSpot(List<int> cells, PropPlacement placement, SpatialHash2D<int> sameKind, float spacing, float awayFromMobs,
            out int cell, out Vector2 pos, out float yaw)
        {
            cell = -1;
            pos = default;
            yaw = 0f;
            var candidates = new List<int>();
            foreach (int c in cells)
            {
                if (Blocked(c) && !(placement == PropPlacement.Corridor && g.Type[c] == CellType.Floor && !g.Has(c, CellFlags.Occupied)))
                    continue;
                if (g.Has(c, CellFlags.Occupied | CellFlags.Landing) || g.Type[c] == CellType.Door)
                    continue;
                switch (placement)
                {
                    case PropPlacement.WallAdjacent:
                        if (wall[c] >= 1.6f || WallSide(c) < 0 || NearDoor(c))
                            continue;
                        break;
                    case PropPlacement.Corner:
                        if (CornerYaw(c, out _) < 0 || NearDoor(c))
                            continue;
                        break;
                    case PropPlacement.Center:
                        if (wall[c] < 2f)
                            continue;
                        break;
                    case PropPlacement.Anywhere:
                    case PropPlacement.DeadEnd:
                        if (wall[c] < 1.4f || NearDoor(c))
                            continue;
                        break;
                    case PropPlacement.Corridor:
                        if (!g.Has(c, CellFlags.Corridor) || NearDoor(c))
                            continue;
                        break;
                    case PropPlacement.Doorway:
                        if (!NearDoor(c))
                            continue;
                        break;
                    case PropPlacement.Chokepoint:
                        if (!g.Has(c, CellFlags.Chokepoint))
                            continue;
                        break;
                    case PropPlacement.Transition:
                        if (!g.Has(c, CellFlags.Rubble))
                            continue;
                        break;
                    case PropPlacement.BackWall:
                        if (wall[c] >= 1.6f || WallSide(c) < 0 || NearDoor(c))
                            continue;
                        break;
                    case PropPlacement.OffPath:
                        if (g.Has(c, CellFlags.MainPath | CellFlags.Chokepoint) || wall[c] < 1.4f || NearDoor(c))
                            continue;
                        break;
                }
                candidates.Add(c);
            }
            if (candidates.Count == 0)
                return false;

            Vector2 middle = Vector2.zero;
            if (placement == PropPlacement.Center)
                candidates.Sort((a, b) => wall[b].CompareTo(wall[a]));
            else if (placement == PropPlacement.BackWall)
            {
                // The wall cell farthest from the ways in (a throne faces whoever comes in).
                List<int> ways = WaysIn(cells);
                foreach (int c in cells)
                    middle += CellPos(c);
                middle /= Mathf.Max(1, cells.Count);
                var score = new Dictionary<int, float>();
                foreach (int c in candidates)
                {
                    float nearest = float.MaxValue;
                    foreach (int o in ways)
                        nearest = Mathf.Min(nearest, (CellPos(o) - CellPos(c)).sqrMagnitude);
                    score[c] = ways.Count > 0 ? nearest : 0f;
                }
                candidates.Sort((a, b) => score[b].CompareTo(score[a]));
            }
            else
                rng.Shuffle(candidates);

            foreach (int c in candidates)
            {
                Vector2 p0 = CellPos(c);
                if (spacing > 0f && sameKind.AnyWithin(p0, spacing))
                    continue;
                if (awayFromMobs > 0f && mobs.AnyWithin(p0, awayFromMobs))
                    continue;
                if (occupied.AnyWithin(p0, 0.9f))
                    continue;

                cell = c;
                pos = p0;
                switch (placement)
                {
                    case PropPlacement.WallAdjacent:
                    {
                        int side = WallSide(c);
                        pos += (Vector2)((Dir4)side).Delta() * 0.3f;
                        yaw = ((Dir4)side).Opposite().Yaw();
                        break;
                    }
                    case PropPlacement.Corner:
                        yaw = CornerYaw(c, out Vector2 push);
                        pos += push * 0.25f;
                        break;
                    case PropPlacement.BackWall:
                    {
                        int side = WallSide(c);
                        pos += (Vector2)((Dir4)side).Delta() * 0.3f;
                        yaw = YawTowards(pos, middle);
                        break;
                    }
                    case PropPlacement.Doorway:
                        for (int d = 0; d < 4; d++)
                        {
                            int nb = g.Neighbor(c, d);
                            if (nb >= 0 && g.Type[nb] == CellType.Door)
                                yaw = ((Dir4)d).Yaw();
                        }
                        break;
                    case PropPlacement.Corridor:
                        // Along the corridor (traps swing and shoot along or across it).
                        yaw = g.IsWalkable(g.Neighbor(c, 0)) || g.IsWalkable(g.Neighbor(c, 2)) ? 0f : 90f;
                        break;
                    default:
                        yaw = rng.Range(0f, 360f);
                        pos += new Vector2(rng.Range(-0.15f, 0.15f), rng.Range(-0.15f, 0.15f));
                        break;
                }
                return true;
            }
            return false;
        }

        /// <summary>If the cell is in a corner (two perpendicular walls), the yaw facing out of it; otherwise -1.</summary>
        private float CornerYaw(int c, out Vector2 push)
        {
            push = Vector2.zero;
            for (int d = 0; d < 4; d++)
            {
                int n1 = g.Neighbor(c, d), n2 = g.Neighbor(c, (d + 1) & 3);
                if (n1 >= 0 && n2 >= 0 && g.IsRock(n1) && g.IsRock(n2))
                {
                    push = (Vector2)((Dir4)d).Delta() + ((Dir4)((d + 1) & 3)).Delta();
                    push.Normalize();
                    float yaw = Mathf.Atan2(-push.x, -push.y) * Mathf.Rad2Deg;
                    return yaw < 0f ? yaw + 360f : yaw;
                }
            }
            return -1f;
        }
    }
}
