using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>One floor of the dungeon: its grid, areas, connections and analysis fields.</summary>
    public sealed class FloorLayout
    {
        public int Index;
        public FloorSpec Spec;
        public TileGrid Grid;
        public readonly List<Area> Areas = new List<Area>();
        public readonly List<Connection> Connections = new List<Connection>();
        public readonly List<Anchor> Anchors = new List<Anchor>();
        /// <summary>Connections decided by the layout itself (maze edges): always kept, as corridors.</summary>
        public readonly List<Vector2Int> PresetConnections = new List<Vector2Int>();
        /// <summary>Connections of a set kind decided by the layout: bridges, portal pads, moving platforms (always kept).</summary>
        public readonly List<PresetLink> PresetLinks = new List<PresetLink>();

        /// <summary>Where the player arrives on this floor (entrance on floor 0, the stairs' lower landing below).</summary>
        public int ArrivalArea = -1;
        /// <summary>Where the main path leaves this floor (the stairs' upper landing, or the exit on the last floor).</summary>
        public int DepartureArea = -1;
        public int ArrivalCell = -1;
        public int DepartureCell = -1;

        /// <summary>Walking distance in cells from the arrival cell (-1 = unreachable). Filled by the Analysis stage.</summary>
        public int[] DistanceFromArrival;
        /// <summary>Distance in cells from every cell to the nearest non-walkable cell. Filled by the Analysis stage.</summary>
        public float[] WallDistance;
        /// <summary>Hybrid floors: zone per cell (index into <see cref="ZoneStyles"/>), null otherwise.</summary>
        public int[] Zone;
        public List<ZoneStyle> ZoneStyles;
        /// <summary>Cells on the shortest walk from arrival to departure.</summary>
        public readonly List<int> MainPathCells = new List<int>();
        /// <summary>Distance from the dungeon entrance to this floor's arrival, along the main path (cells).</summary>
        public int GlobalDistanceOffset;

        public Area AddArea(AreaKind kind, ZoneStyle style)
        {
            var area = new Area { Id = Areas.Count, Floor = Index, Kind = kind, Style = style };
            Areas.Add(area);
            return area;
        }

        public Connection AddConnection(int a, int b, ConnectionKind kind)
        {
            var c = new Connection { Id = Connections.Count, A = a, B = b, Kind = kind };
            Connections.Add(c);
            Areas[a].Connections.Add(c.Id);
            Areas[b].Connections.Add(c.Id);
            return c;
        }

        public Connection FindConnection(int a, int b)
        {
            foreach (int id in Areas[a].Connections)
            {
                Connection c = Connections[id];
                if (c.Joins(a, b) && !c.Failed)
                    return c;
            }
            return null;
        }

        /// <summary>Degree of an area counting working connections and vertical links.</summary>
        public int Degree(int area)
        {
            int degree = Areas[area].Links.Count;
            foreach (int id in Areas[area].Connections)
                if (!Connections[id].Failed)
                    degree++;
            return degree;
        }

        public bool IsReachable(int cell) => DistanceFromArrival != null && cell >= 0 && DistanceFromArrival[cell] >= 0;

        /// <summary>
        /// Drops areas without cells and renumbers the rest (grid, anchors and presets follow). Only valid before
        /// connections exist - layout strategies call it after merging or pruning areas.
        /// </summary>
        public void RemoveEmptyAreas()
        {
            if (Connections.Count > 0)
                throw new System.InvalidOperationException("RemoveEmptyAreas must run before connections are made.");
            var remap = new int[Areas.Count];
            var kept = new List<Area>();
            for (int i = 0; i < Areas.Count; i++)
            {
                if (Areas[i].Cells.Count == 0)
                {
                    remap[i] = -1;
                    continue;
                }
                remap[i] = kept.Count;
                Areas[i].Id = kept.Count;
                kept.Add(Areas[i]);
            }
            if (kept.Count == Areas.Count)
                return;
            Areas.Clear();
            Areas.AddRange(kept);
            for (int c = 0; c < Grid.Count; c++)
                if (Grid.Area[c] >= 0)
                    Grid.Area[c] = remap[Grid.Area[c]];
            foreach (Anchor a in Anchors)
                if (a.AreaId >= 0)
                    a.AreaId = remap[a.AreaId];
            for (int i = PresetConnections.Count - 1; i >= 0; i--)
            {
                Vector2Int p = PresetConnections[i];
                int a = remap[p.x], b = remap[p.y];
                if (a < 0 || b < 0)
                    PresetConnections.RemoveAt(i);
                else
                    PresetConnections[i] = new Vector2Int(a, b);
            }
            for (int i = PresetLinks.Count - 1; i >= 0; i--)
            {
                PresetLink p = PresetLinks[i];
                int a = remap[p.A], b = remap[p.B];
                if (a < 0 || b < 0)
                {
                    PresetLinks.RemoveAt(i);
                    continue;
                }
                p.A = a;
                p.B = b;
                PresetLinks[i] = p;
            }
            if (ArrivalArea >= 0) ArrivalArea = remap[ArrivalArea];
            if (DepartureArea >= 0) DepartureArea = remap[DepartureArea];
        }

        /// <summary>
        /// Walking distance (cells) from <paramref name="start"/> to every cell, -1 where unreachable - stepping across
        /// teleport pads and moving platforms (a jump counts as one step), never through <paramref name="blocked"/>.
        /// </summary>
        public int[] Flood(int start, HashSet<int> blocked = null)
        {
            TileGrid g = Grid;
            var dist = new int[g.Count];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = -1;
            if (start < 0 || !g.IsWalkable(start) || (blocked != null && blocked.Contains(start)))
                return dist;
            Dictionary<int, List<int>> jumps = Jumps();
            var queue = new Queue<int>();
            dist[start] = 0;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb < 0 || dist[nb] >= 0 || !g.IsWalkable(nb) || (blocked != null && blocked.Contains(nb)))
                        continue;
                    dist[nb] = dist[c] + 1;
                    queue.Enqueue(nb);
                }
                if (jumps != null && jumps.TryGetValue(c, out List<int> to))
                {
                    foreach (int nb in to)
                    {
                        if (dist[nb] >= 0 || !g.IsWalkable(nb) || (blocked != null && blocked.Contains(nb)))
                            continue;
                        dist[nb] = dist[c] + 1;
                        queue.Enqueue(nb);
                    }
                }
            }
            return dist;
        }

        /// <summary>The cells joined by teleport pads and moving platforms (both ways), or null when there are none.</summary>
        public Dictionary<int, List<int>> Jumps()
        {
            Dictionary<int, List<int>> map = null;
            void Add(int a, int b)
            {
                if (!map.TryGetValue(a, out List<int> list))
                    map[a] = list = new List<int>();
                list.Add(b);
            }
            foreach (Connection c in Connections)
            {
                if (c.Failed || !c.Kind.IsJump() || c.DoorA < 0 || c.DoorB < 0)
                    continue;
                map = map ?? new Dictionary<int, List<int>>();
                Add(c.DoorA, c.DoorB);
                Add(c.DoorB, c.DoorA);
            }
            return map;
        }

        /// <summary>Area at a cell, or null.</summary>
        public Area AreaAt(int cell)
        {
            int id = cell >= 0 ? Grid.Area[cell] : -1;
            return id >= 0 ? Areas[id] : null;
        }
    }

    /// <summary>A connection of a set kind decided by a layout (see <see cref="FloorLayout.PresetLinks"/>).</summary>
    public struct PresetLink
    {
        public int A, B;
        public ConnectionKind Kind;
        /// <summary>Portals and platforms: the walkable end cells (grid index).</summary>
        public int CellA, CellB;
        /// <summary>Platforms: the chasm cells between the ends, in order from A to B.</summary>
        public List<int> Track;
        /// <summary>Bridges: walkway width (cells).</summary>
        public int Width;
    }

    /// <summary>
    /// Everything the generator decided, as plain data - no GameObjects. Presentation builds the scene from it, gameplay
    /// systems query it through <see cref="DungeonInstance"/>, and the editor preview draws it.
    /// </summary>
    public sealed class DungeonLayout
    {
        /// <summary>The seed that was asked for.</summary>
        public int Seed;
        /// <summary>The seed of the attempt that succeeded (differs from <see cref="Seed"/> after a retry).</summary>
        public int AttemptSeed;
        public int Attempt;
        public DungeonRequest Request;

        public float CellSize;
        public float FloorSpacing;
        /// <summary>Grid size shared by every floor.</summary>
        public int Width, Height;

        public readonly List<FloorLayout> Floors = new List<FloorLayout>();
        public readonly List<VerticalLink> Links = new List<VerticalLink>();

        public AreaRef Entrance = new AreaRef(-1, -1);
        public AreaRef Exit = new AreaRef(-1, -1);
        /// <summary>Areas from the entrance to the exit, in order, across floors.</summary>
        public readonly List<AreaRef> MainPath = new List<AreaRef>();

        public DungeonPose PlayerSpawn;
        public DungeonPose EntrancePortal;
        public DungeonPose ExitPortal;
        public readonly List<Placement> Placements = new List<Placement>();

        public readonly GenerationReport Report = new GenerationReport();

        public Area GetArea(AreaRef r) => r.IsValid ? Floors[r.Floor].Areas[r.Area] : null;

        public Area EntranceArea => GetArea(Entrance);
        public Area ExitArea => GetArea(Exit);

        /// <summary>World position of a point in dungeon space, relative to the dungeon origin.</summary>
        public Vector3 ToLocal(int floor, Vector2 cell, float height)
        {
            return new Vector3(cell.x * CellSize, Floors[floor].Spec.BaseY + height, cell.y * CellSize);
        }

        public Vector3 ToLocal(DungeonPose pose) => ToLocal(pose.Floor, pose.Cell, pose.Height);

        /// <summary>Floor height at a cell centre, relative to the floor's base.</summary>
        public float FloorHeightAt(int floor, int cell) => Floors[floor].Grid.FloorHeight[cell];

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Dungeon seed {Seed} (attempt {Attempt}, seed {AttemptSeed}), {Floors.Count} floors, grid {Width}x{Height}, cell {CellSize}m");
            foreach (FloorLayout f in Floors)
            {
                int loops = 0, walkable = 0;
                foreach (Connection c in f.Connections)
                    if (c.IsLoop && !c.Failed)
                        loops++;
                for (int i = 0; i < f.Grid.Count; i++)
                    if (f.Grid.IsWalkable(i))
                        walkable++;
                sb.AppendLine($"  Floor {f.Index}: {f.Spec.Style}, {f.Areas.Count} areas, {f.Connections.Count} connections ({loops} loops), {walkable} walkable cells, openness {f.Spec.Openness:0.00}, complexity {f.Spec.Complexity:0.00}");
            }
            sb.AppendLine($"  Links: {Links.Count}, main path: {MainPath.Count} areas, placements: {Placements.Count}");
            return sb.ToString();
        }
    }

    /// <summary>Timings, retries and notes from one generation.</summary>
    public sealed class GenerationReport
    {
        public struct StageTime
        {
            public string Stage;
            public double Milliseconds;
        }

        public readonly List<StageTime> Stages = new List<StageTime>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> FailedAttempts = new List<string>();
        public double TotalMilliseconds;
        public int Attempts;

        public void AddTime(string stage, double ms)
        {
            lock (Stages)
                Stages.Add(new StageTime { Stage = stage, Milliseconds = ms });
        }

        public void Warn(string message)
        {
            lock (Warnings)
                Warnings.Add(message);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Generated in {TotalMilliseconds:0.0} ms, {Attempts} attempt(s)");
            foreach (StageTime s in Stages)
                sb.AppendLine($"  {s.Stage}: {s.Milliseconds:0.00} ms");
            foreach (string f in FailedAttempts)
                sb.AppendLine("  retry: " + f);
            foreach (string w in Warnings)
                sb.AppendLine("  warning: " + w);
            return sb.ToString();
        }
    }
}
