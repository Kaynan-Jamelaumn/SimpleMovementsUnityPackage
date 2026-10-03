using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// Anchors of a tower dungeon (every floor a Tower floor): the floors share one footprint, and a square stair core
    /// in its middle holds one continuous spiral staircase. Each floor has a single doorway into the core - the way up
    /// and the way down at once - wherever the spiral passes that floor's level; the spiral turns a whole number of
    /// quarter turns per floor (enough to keep it walkable), so every doorway sits in the middle of one side of the
    /// core. The entrance is a gatehouse room at the edge of the top floor; the exit is a room at the edge of the bottom
    /// floor, on the side away from its doorway.
    /// </summary>
    internal sealed class TowerPlanner
    {
        private readonly DungeonContext ctx;
        private readonly CompiledProfile p;
        private readonly DungeonLayout layout;
        private readonly DungeonRandom rng;

        public TowerPlanner(DungeonContext ctx, DungeonRandom rng)
        {
            this.ctx = ctx;
            p = ctx.Profile;
            layout = ctx.Layout;
            this.rng = rng;
        }

        /// <summary>Side of the stair core (cells): odd, so its doorways line up with the spiral's middle.</summary>
        public static int CoreSize(CompiledProfile p)
        {
            int k = Mathf.Clamp(p.Tower != null ? p.Tower.coreSize : 5, 5, 7);
            return k % 2 == 0 ? k + 1 : k;
        }

        /// <summary>Smallest tower floor: the core, its wall, a ring hall and a chamber on each side.</summary>
        public static int MinSide(CompiledProfile p) => CoreSize(p) + 2 + 2 * 10;

        /// <summary>
        /// Quarter turns the spiral needs to climb <paramref name="rise"/> meters without being steeper than the tower's
        /// Spiral Slope next to its central column (at least three quarters of a turn).
        /// </summary>
        public static int QuarterTurns(CompiledProfile p, float rise)
        {
            TowerSettings t = p.Tower ?? new TowerSettings();
            float wall = LinkMesher.SpiralRadius(CoreSize(p), p.CellSize);
            float inner = Mathf.Clamp(t.newelRadius, 0.3f, wall - 1.2f);
            // The arc the steps need next to the column, plus the landing at the doorway.
            float run = rise / Mathf.Tan(Mathf.Clamp(t.spiralSlope, 20f, 60f) * Mathf.Deg2Rad);
            float sweep = run / inner + LinkMesher.SpiralLanding(wall, p.CellSize);
            return Mathf.Max(3, Mathf.CeilToInt(sweep / (Mathf.PI * 0.5f)));
        }

        /// <summary>The cell just outside the middle of one side of the core (side 0 east, 1 north, 2 west, 3 south).</summary>
        public static Vector2Int DoorCell(RectInt core, int side)
        {
            int mx = core.x + core.width / 2, my = core.y + core.height / 2;
            switch (side & 3)
            {
                case 0: return new Vector2Int(core.xMax, my);
                case 1: return new Vector2Int(mx, core.yMax);
                case 2: return new Vector2Int(core.x - 1, my);
                default: return new Vector2Int(mx, core.y - 1);
            }
        }

        /// <summary>The direction pointing from the core out through a side.</summary>
        public static Dir4 SideDir(int side)
        {
            switch (side & 3)
            {
                case 0: return Dir4.East;
                case 1: return Dir4.North;
                case 2: return Dir4.West;
                default: return Dir4.South;
            }
        }

        public void Plan()
        {
            int n = layout.Floors.Count;
            RectInt fp = layout.Floors[0].Spec.Footprint;
            int k = CoreSize(p);
            var core = new RectInt(fp.x + (fp.width - k) / 2, fp.y + (fp.height - k) / 2, k, k);

            // The spiral: where it passes each floor (a side of the core), turning clockwise going down.
            var sides = new int[n];
            sides[0] = rng.Range(0, 4);
            var quarters = new int[Mathf.Max(0, n - 1)];
            for (int f = 0; f < n - 1; f++)
            {
                float rise = layout.Floors[f].Spec.BaseY - layout.Floors[f + 1].Spec.BaseY;
                quarters[f] = QuarterTurns(p, rise);
                sides[f + 1] = ((sides[f] - quarters[f]) % 4 + 4) % 4;
            }

            var links = new List<VerticalLink>();
            for (int f = 0; f < n - 1; f++)
            {
                var link = new VerticalLink
                {
                    Id = layout.Links.Count,
                    Kind = LinkKind.Spiral,
                    UpperFloor = f,
                    LowerFloor = f + 1,
                    Footprint = core,
                    Descend = SideDir(sides[f]),
                    UpperLanding = DoorCell(core, sides[f]),
                    LowerLanding = DoorCell(core, sides[f + 1]),
                    OnMainPath = true,
                    Turns = quarters[f] * 0.25f,
                    StartAngle = sides[f] * Mathf.PI * 0.5f,
                    Above = f > 0 ? layout.Links.Count - 1 : -1,
                };
                if (f > 0)
                    layout.Links[layout.Links.Count - 1].Below = link.Id;
                layout.Links.Add(link);
                links.Add(link);
            }

            // One doorway per floor: the landing serves the way up and the way down.
            for (int f = 0; f < n; f++)
            {
                FloorLayout floor = layout.Floors[f];
                int up = f > 0 ? links[f - 1].Id : -1, down = f < n - 1 ? links[f].Id : -1;
                if (up < 0 && down < 0)
                    continue;
                Vector2Int door = DoorCell(core, sides[f]);
                Dir4 d = SideDir(sides[f]);
                floor.Anchors.Add(new Anchor
                {
                    Kind = up >= 0 ? AnchorKind.StairsUpLanding : AnchorKind.StairsDownLanding,
                    Floor = f,
                    Room = LandingRoom(door, d),
                    Cell = door,
                    LinkId = up >= 0 ? up : down,
                    ExtraLinkId = up >= 0 ? down : -1,
                    Facing = d,
                    Shaft = core,
                });
                int cell = floor.Grid.Index(door);
                if (up >= 0)
                    floor.ArrivalCell = cell;
                if (down >= 0)
                    floor.DepartureCell = cell;
            }

            // The gatehouse on the top floor and the exit at the bottom, at the tower's edge.
            int entranceSide = rng.Range(0, 4);
            Anchor entrance = EdgeRoom(0, AnchorKind.Entrance, entranceSide, p.Links.entranceSize);
            layout.Floors[0].ArrivalCell = layout.Floors[0].Grid.Index(entrance.Cell);
            int last = n - 1;
            // Away from the bottom floor's doorway (and from the entrance on a one-floor tower).
            int exitSide = (sides[last] + 2) & 3;
            if (n == 1 && exitSide == entranceSide)
                exitSide = (entranceSide + 2) & 3;
            Anchor exit = EdgeRoom(last, AnchorKind.Exit, exitSide, p.Links.exitSize);
            layout.Floors[last].DepartureCell = layout.Floors[last].Grid.Index(exit.Cell);
        }

        /// <summary>
        /// The doorway passage through the core's wall: the door cell and the cell beyond it (one cell wide, so the only
        /// opening into the core is the doorway itself).
        /// </summary>
        private static RectInt LandingRoom(Vector2Int door, Dir4 d)
        {
            switch (d)
            {
                case Dir4.East: return new RectInt(door.x, door.y, 2, 1);
                case Dir4.West: return new RectInt(door.x - 1, door.y, 2, 1);
                case Dir4.North: return new RectInt(door.x, door.y, 1, 2);
                default: return new RectInt(door.x, door.y - 1, 1, 2);
            }
        }

        /// <summary>A room centred on one side of the footprint, two cells in from its edge, facing the core.</summary>
        private Anchor EdgeRoom(int floorIndex, AnchorKind kind, int side, IntRange size)
        {
            FloorLayout floor = layout.Floors[floorIndex];
            RectInt fp = floor.Spec.Footprint;
            int s = Mathf.Clamp(size.Random(rng), 4, Mathf.Max(4, fp.width / 4));
            int w = s, h = Mathf.Max(4, s - 1);
            RectInt room;
            Dir4 facing;
            switch (side & 3)
            {
                case 0: room = new RectInt(fp.xMax - 2 - h, fp.y + (fp.height - w) / 2, h, w); facing = Dir4.West; break;
                case 1: room = new RectInt(fp.x + (fp.width - w) / 2, fp.yMax - 2 - h, w, h); facing = Dir4.South; break;
                case 2: room = new RectInt(fp.x + 2, fp.y + (fp.height - w) / 2, h, w); facing = Dir4.East; break;
                default: room = new RectInt(fp.x + (fp.width - w) / 2, fp.y + 2, w, h); facing = Dir4.North; break;
            }
            var anchor = new Anchor
            {
                Kind = kind,
                Floor = floorIndex,
                Room = room,
                Cell = new Vector2Int(room.x + room.width / 2, room.y + room.height / 2),
                Facing = facing,
            };
            floor.Anchors.Add(anchor);
            return anchor;
        }
    }
}
