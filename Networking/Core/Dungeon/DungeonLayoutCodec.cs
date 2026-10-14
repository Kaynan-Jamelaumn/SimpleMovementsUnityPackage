using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using ProceduralDungeon;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// What a client needs to know about a run's dungeon before its first floor arrives. It never contains the dungeon
    /// seed (nor anything derived from it that could regenerate the dungeon): the profile by name, where the dungeon
    /// stands, the grid size and the number of floors.
    /// </summary>
    public sealed class DungeonNetHeader
    {
        /// <summary>Counts the dungeons built in the run's scene (Next Dungeon builds another one in the same scene).</summary>
        public int Generation;
        /// <summary>Name of the <see cref="DungeonProfile"/> asset (empty = the built-in defaults).</summary>
        public string Profile = "";
        /// <summary>Name of the dungeon's root object (never the seed).</summary>
        public string Label = "";
        /// <summary>World position of the dungeon's origin (its manager).</summary>
        public Vector3 Origin;
        /// <summary>World rotation of the dungeon's origin.</summary>
        public Quaternion Rotation = Quaternion.identity;
        /// <summary>Size of one grid cell (metres).</summary>
        public float CellSize = 1f;
        /// <summary>Distance between floors used by the layout.</summary>
        public float FloorSpacing;
        /// <summary>Grid size shared by every floor.</summary>
        public int Width, Height;
        /// <summary>How many floors the dungeon has (their contents are only sent when the party reaches them).</summary>
        public int FloorCount;
        /// <summary>What the exit portal does (<see cref="DungeonPortal.PortalAction"/>).</summary>
        public byte ExitAction;
        /// <summary>The dungeon's slot (-1 = none) and its size, so clients route physics queries like the server.</summary>
        public int Slot = -1;
        /// <summary>Size of the slot cube (metres).</summary>
        public float SlotSize;
        /// <summary>The dungeon scene simulates its own physics.</summary>
        public bool OwnPhysics;
    }

    /// <summary>One floor as a client receives it (see <see cref="DungeonLayoutCodec.DecodeFloor"/>).</summary>
    public sealed class DungeonNetFloor
    {
        /// <summary>The floor's grid, areas and connections (no analysis fields, no anchors).</summary>
        public FloorLayout Floor;
        /// <summary>The stairs, drops, climbs and spirals that touch this floor.</summary>
        public readonly List<VerticalLink> Links = new List<VerticalLink>();
        /// <summary>The placements the client builds on this floor (never mobs or item pickups).</summary>
        public readonly List<Placement> Placements = new List<Placement>();
        /// <summary>Index of each entry of <see cref="Placements"/> in the server's layout (the element id of what it builds).</summary>
        public readonly List<int> PlacementIds = new List<int>();
        /// <summary>The floor's build seed (purely visual detail; see <see cref="DungeonBuildSeed"/>).</summary>
        public int BuildSeed;
    }

    /// <summary>
    /// Turns a dungeon's run header and single floors into compact byte arrays (deflated) that Fish-Net can send, and
    /// back. Floors are sent one at a time as the party reaches them; the dungeon seed is never part of the data.
    /// Always compiled and free of Fish-Net types, so it is unit-tested and usable offline.
    /// </summary>
    public static class DungeonLayoutCodec
    {
        /// <summary>Format version written first in every packet (packets of another version are refused).</summary>
        public const byte Version = 1;
        /// <summary>Largest grid accepted when decoding (cells).</summary>
        public const int MaxCells = 1 << 22;
        /// <summary>Largest number of entries accepted for any list when decoding.</summary>
        public const int MaxEntries = 1 << 20;
        /// <summary>Most floors a header may announce.</summary>
        public const int MaxFloors = 256;
        /// <summary>Longest string accepted when decoding.</summary>
        public const int MaxStringLength = 256;
        /// <summary>Largest decompressed packet accepted (bytes).</summary>
        public const int MaxInflatedBytes = 64 * 1024 * 1024;

        private const byte HeaderTag = 0x48;   // 'H'
        private const byte FloorTag = 0x46;    // 'F'

        // ------------------------------------------------------------------ header

        /// <summary>Encodes a run header.</summary>
        public static byte[] EncodeHeader(DungeonNetHeader h)
        {
            if (h == null)
                throw new ArgumentNullException(nameof(h));
            return Pack(HeaderTag, w =>
            {
                w.Write(h.Generation);
                WriteString(w, h.Profile);
                WriteString(w, h.Label);
                WriteVector3(w, h.Origin);
                w.Write(h.Rotation.x);
                w.Write(h.Rotation.y);
                w.Write(h.Rotation.z);
                w.Write(h.Rotation.w);
                w.Write(h.CellSize);
                w.Write(h.FloorSpacing);
                w.Write(h.Width);
                w.Write(h.Height);
                w.Write(h.FloorCount);
                w.Write(h.ExitAction);
                w.Write(h.Slot);
                w.Write(h.SlotSize);
                w.Write(h.OwnPhysics);
            });
        }

        /// <summary>Decodes a run header; null when the data is malformed or out of range.</summary>
        public static DungeonNetHeader DecodeHeader(byte[] data)
        {
            return Unpack(data, HeaderTag, r =>
            {
                var h = new DungeonNetHeader
                {
                    Generation = r.ReadInt32(),
                    Profile = ReadString(r),
                    Label = ReadString(r),
                    Origin = ReadVector3(r),
                    Rotation = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                    CellSize = r.ReadSingle(),
                    FloorSpacing = r.ReadSingle(),
                    Width = r.ReadInt32(),
                    Height = r.ReadInt32(),
                    FloorCount = r.ReadInt32(),
                    ExitAction = r.ReadByte(),
                    Slot = r.ReadInt32(),
                    SlotSize = r.ReadSingle(),
                    OwnPhysics = r.ReadBoolean(),
                };
                if (h.Width <= 0 || h.Height <= 0 || (long)h.Width * h.Height > MaxCells)
                    return null;
                if (h.FloorCount <= 0 || h.FloorCount > MaxFloors)
                    return null;
                if (!IsFinite(h.Origin) || !IsFinite(h.CellSize) || h.CellSize <= 0f || !IsFinite(h.FloorSpacing) || !IsFinite(h.SlotSize))
                    return null;
                if (!IsFinite(h.Rotation.x) || !IsFinite(h.Rotation.y) || !IsFinite(h.Rotation.z) || !IsFinite(h.Rotation.w))
                    return null;
                float norm = h.Rotation.x * h.Rotation.x + h.Rotation.y * h.Rotation.y + h.Rotation.z * h.Rotation.z + h.Rotation.w * h.Rotation.w;
                h.Rotation = norm > 0.0001f ? Quaternion.Normalize(h.Rotation) : Quaternion.identity;
                return h;
            });
        }

        // ------------------------------------------------------------------ floors

        /// <summary>
        /// Encodes floor <paramref name="floor"/> of <paramref name="layout"/>: its spec, grid, areas, connections, the
        /// vertical links that touch it and the placements <paramref name="include"/> accepts (null = all of the floor's).
        /// </summary>
        public static byte[] EncodeFloor(DungeonLayout layout, int floor, int buildSeed, Func<Placement, bool> include = null)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));
            if (floor < 0 || floor >= layout.Floors.Count)
                throw new ArgumentOutOfRangeException(nameof(floor));
            FloorLayout f = layout.Floors[floor];
            return Pack(FloorTag, w =>
            {
                w.Write(f.Index);
                w.Write(buildSeed);
                WriteSpec(w, f.Spec ?? new FloorSpec { Index = f.Index });
                WriteGrid(w, f.Grid);

                w.Write(f.Areas.Count);
                foreach (Area a in f.Areas)
                    WriteArea(w, a);
                w.Write(f.Connections.Count);
                foreach (Connection c in f.Connections)
                    WriteConnection(w, c);

                w.Write(f.ArrivalArea);
                w.Write(f.DepartureArea);
                w.Write(f.ArrivalCell);
                w.Write(f.DepartureCell);
                w.Write(f.GlobalDistanceOffset);

                var links = new List<VerticalLink>();
                foreach (VerticalLink l in layout.Links)
                    if (l != null && (l.LowerFloor == floor || l.UpperFloor == floor))
                        links.Add(l);
                w.Write(links.Count);
                foreach (VerticalLink l in links)
                    WriteLink(w, l);

                var chosen = new List<int>();
                for (int i = 0; i < layout.Placements.Count; i++)
                {
                    Placement p = layout.Placements[i];
                    if (p.Floor == floor && (include == null || include(p)))
                        chosen.Add(i);
                }
                w.Write(chosen.Count);
                foreach (int i in chosen)
                {
                    w.Write(layout.PlacementId(i));
                    WritePlacement(w, layout.Placements[i]);
                }
            });
        }

        /// <summary>
        /// Decodes one floor. <paramref name="width"/> and <paramref name="height"/> are the header's grid size (the floor
        /// must match it). Null when the data is malformed or out of range.
        /// </summary>
        public static DungeonNetFloor DecodeFloor(byte[] data, int width, int height)
        {
            return Unpack(data, FloorTag, r =>
            {
                var result = new DungeonNetFloor();
                int index = r.ReadInt32();
                if (index < 0 || index >= MaxFloors)
                    return null;
                result.BuildSeed = r.ReadInt32();
                FloorSpec spec = ReadSpec(r);
                TileGrid grid = ReadGrid(r);
                if (grid == null || grid.Width != width || grid.Height != height)
                    return null;
                var f = new FloorLayout { Index = index, Spec = spec, Grid = grid };
                spec.Index = index;

                int areas = ReadCount(r);
                for (int i = 0; i < areas; i++)
                {
                    Area a = ReadArea(r, grid.Count);
                    a.Id = i;
                    a.Floor = index;
                    f.Areas.Add(a);
                }
                int connections = ReadCount(r);
                for (int i = 0; i < connections; i++)
                {
                    Connection c = ReadConnection(r, grid.Count);
                    c.Id = i;
                    if (c.A < 0 || c.A >= areas || c.B < 0 || c.B >= areas)
                        return null;
                    f.Connections.Add(c);
                    f.Areas[c.A].Connections.Add(i);
                    if (c.B != c.A)
                        f.Areas[c.B].Connections.Add(i);
                }

                f.ArrivalArea = ReadIndex(r, areas);
                f.DepartureArea = ReadIndex(r, areas);
                f.ArrivalCell = ReadIndex(r, grid.Count);
                f.DepartureCell = ReadIndex(r, grid.Count);
                f.GlobalDistanceOffset = r.ReadInt32();

                for (int c = 0; c < grid.Count; c++)
                    if (grid.Area[c] >= areas || grid.Connection[c] >= connections)
                        return null;

                int links = ReadCount(r);
                for (int i = 0; i < links; i++)
                {
                    VerticalLink l = ReadLink(r);
                    if (l.Id < 0 || l.Id >= MaxEntries || (l.UpperFloor != index && l.LowerFloor != index) ||
                        l.UpperFloor < 0 || l.LowerFloor < 0 || l.UpperFloor >= MaxFloors || l.LowerFloor >= MaxFloors)
                        return null;
                    result.Links.Add(l);
                    if (l.UpperFloor == index && l.UpperArea >= 0 && l.UpperArea < areas)
                        f.Areas[l.UpperArea].Links.Add(l.Id);
                    if (l.LowerFloor == index && l.LowerArea >= 0 && l.LowerArea < areas)
                        f.Areas[l.LowerArea].Links.Add(l.Id);
                }

                int placements = ReadCount(r);
                for (int i = 0; i < placements; i++)
                {
                    int id = r.ReadInt32();
                    Placement p = ReadPlacement(r);
                    if (id < 0)
                        return null;
                    p.Floor = index;
                    if (p.Area >= areas)
                        p.Area = -1;
                    result.PlacementIds.Add(id);
                    result.Placements.Add(p);
                }
                result.Floor = f;
                return result;
            });
        }

        /// <summary>A client's layout for a run: grid size and cell size from the header, no floors yet, no seed.</summary>
        public static DungeonLayout CreateReplicaLayout(DungeonNetHeader h)
        {
            if (h == null)
                throw new ArgumentNullException(nameof(h));
            return new DungeonLayout
            {
                Seed = 0,
                AttemptSeed = 0,
                Request = new DungeonRequest { label = h.Label ?? "" },
                CellSize = h.CellSize,
                FloorSpacing = h.FloorSpacing,
                Width = h.Width,
                Height = h.Height,
                PlacementIds = new List<int>(),
                FloorBuildSeeds = new int[Mathf.Clamp(h.FloorCount, 1, MaxFloors)],
            };
        }

        /// <summary>
        /// Adds a decoded floor to a client's layout. Floors must arrive in order (the next one is
        /// <c>layout.Floors.Count</c>); returns false for any other floor, which the caller keeps until its turn.
        /// </summary>
        public static bool Merge(DungeonLayout layout, DungeonNetFloor floor)
        {
            if (layout == null || floor == null || floor.Floor == null || floor.Floor.Index != layout.Floors.Count)
                return false;
            int index = floor.Floor.Index;
            if (layout.FloorBuildSeeds != null && index < layout.FloorBuildSeeds.Length)
                layout.FloorBuildSeeds[index] = floor.BuildSeed;
            layout.Floors.Add(floor.Floor);
            foreach (VerticalLink l in floor.Links)
            {
                while (layout.Links.Count <= l.Id)
                    layout.Links.Add(null);
                layout.Links[l.Id] = l;
            }
            if (layout.PlacementIds == null)
                layout.PlacementIds = new List<int>();
            for (int i = 0; i < floor.Placements.Count; i++)
            {
                layout.Placements.Add(floor.Placements[i]);
                layout.PlacementIds.Add(floor.PlacementIds[i]);
            }
            return true;
        }

        // ------------------------------------------------------------------ parts

        private static void WriteSpec(BinaryWriter w, FloorSpec s)
        {
            w.Write((byte)s.Style);
            WriteRect(w, s.Footprint);
            w.Write(s.Openness);
            w.Write(s.Complexity);
            w.Write(s.Difficulty);
            w.Write(s.IsFirst);
            w.Write(s.IsLast);
            w.Write(s.BaseY);
            w.Write(s.SpacingAbove);
            w.Write(s.MaxCeiling);
            w.Write((byte)s.Modifier);
            w.Write(s.BuiltWeight);
            w.Write(s.CavernWeight);
            w.Write(s.RuinsWeight);
        }

        private static FloorSpec ReadSpec(BinaryReader r)
        {
            var s = new FloorSpec
            {
                Style = (FloorStyle)r.ReadByte(),
                Footprint = ReadRect(r),
                Openness = r.ReadSingle(),
                Complexity = r.ReadSingle(),
                Difficulty = r.ReadSingle(),
                IsFirst = r.ReadBoolean(),
                IsLast = r.ReadBoolean(),
                BaseY = r.ReadSingle(),
                SpacingAbove = r.ReadSingle(),
                MaxCeiling = r.ReadSingle(),
                Modifier = (FloorModifier)r.ReadByte(),
                BuiltWeight = r.ReadSingle(),
                CavernWeight = r.ReadSingle(),
                RuinsWeight = r.ReadSingle(),
            };
            if (!IsFinite(s.BaseY) || !IsFinite(s.SpacingAbove) || !IsFinite(s.MaxCeiling))
                throw new InvalidDataException("floor spec");
            return s;
        }

        private static void WriteGrid(BinaryWriter w, TileGrid g)
        {
            w.Write(g.Width);
            w.Write(g.Height);
            int n = g.Count;
            for (int i = 0; i < n; i++) w.Write((byte)g.Type[i]);
            for (int i = 0; i < n; i++) w.Write((uint)g.Flags[i]);
            for (int i = 0; i < n; i++) w.Write(g.Area[i]);
            for (int i = 0; i < n; i++) w.Write(g.Connection[i]);
            for (int i = 0; i < n; i++) w.Write(g.FloorHeight[i]);
            for (int i = 0; i < n; i++) w.Write(g.CeilingHeight[i]);
            bool roofs = g.RoofHeight != null && g.SkyHeight != null;
            w.Write(roofs);
            if (roofs)
            {
                for (int i = 0; i < n; i++) w.Write(g.RoofHeight[i]);
                for (int i = 0; i < n; i++) w.Write(g.SkyHeight[i]);
            }
        }

        private static TileGrid ReadGrid(BinaryReader r)
        {
            int width = r.ReadInt32(), height = r.ReadInt32();
            if (width <= 0 || height <= 0 || (long)width * height > MaxCells)
                return null;
            var g = new TileGrid(width, height);
            int n = g.Count;
            for (int i = 0; i < n; i++) g.Type[i] = (CellType)r.ReadByte();
            for (int i = 0; i < n; i++) g.Flags[i] = (CellFlags)r.ReadUInt32();
            for (int i = 0; i < n; i++) g.Area[i] = Mathf.Max(-1, r.ReadInt32());
            for (int i = 0; i < n; i++) g.Connection[i] = Mathf.Max(-1, r.ReadInt32());
            for (int i = 0; i < n; i++) g.FloorHeight[i] = FiniteOrZero(r.ReadSingle());
            for (int i = 0; i < n; i++) g.CeilingHeight[i] = FiniteOrZero(r.ReadSingle());
            if (r.ReadBoolean())
            {
                g.EnsureRoofs();
                for (int i = 0; i < n; i++) g.RoofHeight[i] = FiniteOrZero(r.ReadSingle());
                for (int i = 0; i < n; i++) g.SkyHeight[i] = FiniteOrZero(r.ReadSingle());
            }
            return g;
        }

        private static void WriteArea(BinaryWriter w, Area a)
        {
            w.Write((byte)a.Kind);
            w.Write((byte)a.Role);
            WriteString(w, a.Tag);
            w.Write((byte)a.Style);
            WriteRect(w, a.Bounds);
            w.Write(a.Center.x);
            w.Write(a.Center.y);
            w.Write(a.CenterCell);
            w.Write(a.TemplateIndex);
            w.Write(a.AnchorIndex);
            w.Write(a.TemplateApplied);
            w.Write(a.TemplateOrigin.x);
            w.Write(a.TemplateOrigin.y);
            w.Write(a.Fixed);
            w.Write(a.Region);
            w.Write(a.CeilingHeight);
            w.Write(a.Vaulted);
            w.Write((byte)a.Hint);
            w.Write(a.Depth);
            w.Write(a.Progress);
            w.Write(a.OnMainPath);
            w.Write(a.BranchDepth);
            w.Write(a.IsLeaf);
            w.Write(a.IsHub);
            w.Write(a.MeanWallDistance);
            w.Write(a.MaxWallDistance);
            w.Write(a.Openness);
            w.Write(a.Difficulty);
            WriteInts(w, a.Cells);
        }

        private static Area ReadArea(BinaryReader r, int cells)
        {
            var a = new Area
            {
                Kind = (AreaKind)r.ReadByte(),
                Role = (AreaRole)r.ReadByte(),
                Tag = ReadString(r),
                Style = (ZoneStyle)r.ReadByte(),
                Bounds = ReadRect(r),
                Center = new Vector2(FiniteOrZero(r.ReadSingle()), FiniteOrZero(r.ReadSingle())),
                CenterCell = r.ReadInt32(),
                TemplateIndex = r.ReadInt32(),
                AnchorIndex = r.ReadInt32(),
                TemplateApplied = r.ReadBoolean(),
                TemplateOrigin = new Vector2Int(r.ReadInt32(), r.ReadInt32()),
                Fixed = r.ReadBoolean(),
                Region = r.ReadInt32(),
                CeilingHeight = FiniteOrZero(r.ReadSingle()),
                Vaulted = r.ReadBoolean(),
                Hint = (AreaRole)r.ReadByte(),
                Depth = r.ReadInt32(),
                Progress = FiniteOrZero(r.ReadSingle()),
                OnMainPath = r.ReadBoolean(),
                BranchDepth = r.ReadInt32(),
                IsLeaf = r.ReadBoolean(),
                IsHub = r.ReadBoolean(),
                MeanWallDistance = FiniteOrZero(r.ReadSingle()),
                MaxWallDistance = FiniteOrZero(r.ReadSingle()),
                Openness = FiniteOrZero(r.ReadSingle()),
                Difficulty = FiniteOrZero(r.ReadSingle()),
            };
            ReadInts(r, a.Cells, cells);
            return a;
        }

        private static void WriteConnection(BinaryWriter w, Connection c)
        {
            w.Write(c.A);
            w.Write(c.B);
            w.Write((byte)c.Kind);
            w.Write(c.Forced);
            w.Write(c.InTree);
            w.Write(c.IsLoop);
            w.Write(c.IsRepair);
            w.Write(c.OnMainPath);
            w.Write(c.EstimatedCost);
            w.Write(c.Width);
            w.Write(c.Routed);
            w.Write(c.Failed);
            w.Write(c.DoorA);
            w.Write(c.DoorB);
            WriteInts(w, c.Cells);
            WriteInts(w, c.Track);
        }

        private static Connection ReadConnection(BinaryReader r, int cells)
        {
            var c = new Connection
            {
                A = r.ReadInt32(),
                B = r.ReadInt32(),
                Kind = (ConnectionKind)r.ReadByte(),
                Forced = r.ReadBoolean(),
                InTree = r.ReadBoolean(),
                IsLoop = r.ReadBoolean(),
                IsRepair = r.ReadBoolean(),
                OnMainPath = r.ReadBoolean(),
                EstimatedCost = FiniteOrZero(r.ReadSingle()),
                Width = r.ReadInt32(),
                Routed = r.ReadBoolean(),
                Failed = r.ReadBoolean(),
                DoorA = ReadIndex(r, cells),
                DoorB = ReadIndex(r, cells),
            };
            ReadInts(r, c.Cells, cells);
            ReadInts(r, c.Track, cells);
            return c;
        }

        private static void WriteLink(BinaryWriter w, VerticalLink l)
        {
            w.Write(l.Id);
            w.Write((byte)l.Kind);
            w.Write(l.UpperFloor);
            w.Write(l.LowerFloor);
            WriteRect(w, l.Footprint);
            w.Write((byte)l.Descend);
            w.Write(l.UpperLanding.x);
            w.Write(l.UpperLanding.y);
            w.Write(l.LowerLanding.x);
            w.Write(l.LowerLanding.y);
            w.Write(l.UpperArea);
            w.Write(l.LowerArea);
            w.Write(l.OnMainPath);
            w.Write(l.Turns);
            w.Write(l.StartAngle);
            w.Write(l.Above);
            w.Write(l.Below);
        }

        private static VerticalLink ReadLink(BinaryReader r)
        {
            return new VerticalLink
            {
                Id = r.ReadInt32(),
                Kind = (LinkKind)r.ReadByte(),
                UpperFloor = r.ReadInt32(),
                LowerFloor = r.ReadInt32(),
                Footprint = ReadRect(r),
                Descend = (Dir4)(r.ReadByte() & 3),
                UpperLanding = new Vector2Int(r.ReadInt32(), r.ReadInt32()),
                LowerLanding = new Vector2Int(r.ReadInt32(), r.ReadInt32()),
                UpperArea = r.ReadInt32(),
                LowerArea = r.ReadInt32(),
                OnMainPath = r.ReadBoolean(),
                Turns = FiniteOrZero(r.ReadSingle()),
                StartAngle = FiniteOrZero(r.ReadSingle()),
                Above = r.ReadInt32(),
                Below = r.ReadInt32(),
            };
        }

        private static void WritePlacement(BinaryWriter w, Placement p)
        {
            w.Write((byte)p.Kind);
            w.Write((byte)p.Table);
            w.Write(p.Entry);
            w.Write(p.Area);
            w.Write(p.Cell.x);
            w.Write(p.Cell.y);
            w.Write(p.Height);
            w.Write(p.Yaw);
            w.Write(p.Group);
            w.Write(p.Scale);
            w.Write(p.Tier);
            w.Write(p.Dormant);
            w.Write(p.Wave);
            w.Write(p.Elite);
            w.Write(p.Link);
            w.Write((byte)p.Order);
        }

        private static Placement ReadPlacement(BinaryReader r)
        {
            var p = new Placement
            {
                Kind = (PlacementKind)r.ReadByte(),
                Table = (PlacementTable)r.ReadByte(),
                Entry = r.ReadInt32(),
                Area = r.ReadInt32(),
                Cell = new Vector2(r.ReadSingle(), r.ReadSingle()),
                Height = r.ReadSingle(),
                Yaw = r.ReadSingle(),
                Group = r.ReadInt32(),
                Scale = r.ReadSingle(),
                Tier = r.ReadInt32(),
                Dormant = r.ReadBoolean(),
                Wave = r.ReadByte(),
                Elite = r.ReadBoolean(),
                Link = r.ReadInt32(),
                Order = (MobOrder)r.ReadByte(),
            };
            if (!IsFinite(p.Cell.x) || !IsFinite(p.Cell.y) || !IsFinite(p.Height) || !IsFinite(p.Yaw) || !IsFinite(p.Scale))
                throw new InvalidDataException("placement");
            return p;
        }

        // ------------------------------------------------------------------ primitives

        private static byte[] Pack(byte tag, Action<BinaryWriter> body)
        {
            using (var raw = new MemoryStream())
            {
                using (var w = new BinaryWriter(raw, Encoding.UTF8, true))
                {
                    w.Write(tag);
                    w.Write(Version);
                    body(w);
                }
                using (var packed = new MemoryStream())
                {
                    using (var deflate = new DeflateStream(packed, System.IO.Compression.CompressionLevel.Optimal, true))
                        raw.WriteTo(deflate);
                    return packed.ToArray();
                }
            }
        }

        private static T Unpack<T>(byte[] data, byte tag, Func<BinaryReader, T> body) where T : class
        {
            if (data == null || data.Length == 0)
                return null;
            try
            {
                using (var packed = new MemoryStream(data, false))
                using (var deflate = new DeflateStream(packed, CompressionMode.Decompress))
                using (var raw = new MemoryStream())
                {
                    var buffer = new byte[16384];
                    int read;
                    while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (raw.Length + read > MaxInflatedBytes)
                            return null;
                        raw.Write(buffer, 0, read);
                    }
                    raw.Position = 0;
                    using (var r = new BinaryReader(raw, Encoding.UTF8, true))
                    {
                        if (r.ReadByte() != tag || r.ReadByte() != Version)
                            return null;
                        return body(r);
                    }
                }
            }
            catch (Exception)
            {
                return null;   // malformed data of any kind: refused, never half-applied
            }
        }

        private static void WriteString(BinaryWriter w, string s)
        {
            s = s ?? "";
            if (s.Length > MaxStringLength)
                s = s.Substring(0, MaxStringLength);
            w.Write(s);
        }

        private static string ReadString(BinaryReader r)
        {
            string s = r.ReadString();
            if (s.Length > MaxStringLength)
                throw new InvalidDataException("string");
            return s;
        }

        private static void WriteInts(BinaryWriter w, List<int> list)
        {
            w.Write(list.Count);
            foreach (int v in list)
                w.Write(v);
        }

        private static void ReadInts(BinaryReader r, List<int> into, int cells)
        {
            int n = ReadCount(r);
            for (int i = 0; i < n; i++)
            {
                int v = r.ReadInt32();
                if (v < 0 || v >= cells)
                    throw new InvalidDataException("cell index");
                into.Add(v);
            }
        }

        private static int ReadCount(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > MaxEntries)
                throw new InvalidDataException("count");
            return n;
        }

        /// <summary>An index into a list of <paramref name="count"/> entries, or -1 (anything else is clamped to -1).</summary>
        private static int ReadIndex(BinaryReader r, int count)
        {
            int v = r.ReadInt32();
            return v >= 0 && v < count ? v : -1;
        }

        private static void WriteRect(BinaryWriter w, RectInt rect)
        {
            w.Write(rect.x);
            w.Write(rect.y);
            w.Write(rect.width);
            w.Write(rect.height);
        }

        private static RectInt ReadRect(BinaryReader r) => new RectInt(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());

        private static void WriteVector3(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x);
            w.Write(v.y);
            w.Write(v.z);
        }

        private static Vector3 ReadVector3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        private static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

        private static float FiniteOrZero(float f) => IsFinite(f) ? f : 0f;
    }
}
