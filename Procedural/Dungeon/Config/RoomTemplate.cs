using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProceduralDungeon
{
    /// <summary>
    /// An authored room. Two modes:
    /// <list type="bullet">
    /// <item><b>Shape Mask</b>: the room's floor plan drawn as text; the builder generates its geometry like any
    /// other room. '.' floor, 'P' pillar, 'D' door socket (floor on the edge where corridors may attach), '#' or ' '
    /// outside. Rows go from north (top) to south.</item>
    /// <item><b>Prefab</b>: your own room prefab covering <see cref="footprint"/> cells, with door <see cref="sockets"/>.
    /// Tick <see cref="legacyRoomBehaviour"/> for the old RoomBehaviour rooms (4 doors, one per side).</item>
    /// </list>
    /// Templates are used by roles (fitted into the chosen area), by the Grid Maze style, and by designers who want
    /// hand-made set pieces among procedural rooms.
    /// </summary>
    [CreateAssetMenu(menuName = "SimpleMovements/Dungeon/Room Template", fileName = "RoomTemplate")]
    public class RoomTemplate : ScriptableObject
    {
        public enum TemplateMode
        {
            ShapeMask,
            Prefab,
        }

        public enum Pivot
        {
            /// <summary>The prefab's pivot is the centre of the footprint.</summary>
            Center,
            /// <summary>The pivot is the footprint's south-west corner (min x, min z).</summary>
            SouthWestCorner,
            /// <summary>The pivot is the north-west corner (min x, max z) - the old DungeonGenerator's room prefabs.</summary>
            NorthWestCorner,
        }

        [Serializable]
        public class Socket
        {
            [Tooltip("Side of the footprint.")]
            public Dir4 side = Dir4.North;
            [Tooltip("Cell along that side (from the west end for North/South, from the south end for East/West).")]
            public int offset;
            [Tooltip("Child object shown when a connection uses this socket (optional).")]
            public string openChild = "";
            [Tooltip("Child object shown when this socket is unused (optional).")]
            public string closedChild = "";
        }

        [Tooltip("Shape Mask: draw the floor plan as text below; the room's walls and floors are generated.\nPrefab: place your own room prefab (e.g. the old RoomBehaviour rooms).")]
        public TemplateMode mode = TemplateMode.ShapeMask;

        [Tooltip("Floor plan (Shape Mask mode). '.' floor, 'P' pillar, 'D' door socket, '#'/' ' outside. North row first.")]
        [TextArea(6, 24)]
        public string shapeMask =
            "###D###\n" +
            "#.....#\n" +
            "#.P.P.#\n" +
            "D.....D\n" +
            "#.P.P.#\n" +
            "#.....#\n" +
            "###D###";

        [Header("Prefab mode")]
        [Tooltip("REQUIRED in Prefab mode. The room prefab, covering Footprint cells (x Cell Size meters). It must bring its own floor, walls and colliders.")]
        public GameObject prefab;
        [Tooltip("Cells covered by the prefab (width, depth). The old RoomBehaviour rooms are 7 x 7.")]
        public Vector2Int footprint = new Vector2Int(7, 7);
        [Tooltip("Where the prefab's pivot is: the footprint's centre, its south-west corner, or its north-west corner (the old DungeonGenerator's rooms).")]
        public Pivot pivot = Pivot.Center;
        [Tooltip("Extra offset applied to the prefab (meters), e.g. to sink it to floor level.")]
        public Vector3 prefabOffset;
        [Tooltip("Door sockets: where corridors may attach, with optional child objects shown when the socket is used or not. Ignored with Legacy Room Behaviour.")]
        public List<Socket> sockets = new List<Socket>();
        [Tooltip("The prefab has the old RoomBehaviour: sockets are the middle of each side, opened with RoomBehaviour.UpdateRoom.")]
        public bool legacyRoomBehaviour;

        [Header("Use")]
        [Tooltip("Role this template gives the area it's placed in (None = keep the area's role).")]
        public AreaRole role = AreaRole.None;
        [Tooltip("Custom tag given to the area (props and roles can target it).")]
        public string tag = "";
        [Tooltip("Relative chance of this template among those that fit.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Zone styles it may be placed in.")]
        public ZoneMask styles = ZoneMask.Built | ZoneMask.Ruins;
        [Tooltip("Ceiling height (meters, 0 = the default for the room's size).")]
        [Min(0f)] public float ceilingHeight;
        [Tooltip("Allow mobs, loot and props inside.")]
        public bool allowPopulation = true;

        /// <summary>
        /// The template's plan as cells: <paramref name="floor"/> true for walkable cells, <paramref name="pillar"/> for
        /// pillars, and door sockets. Index = x + y * width with y = 0 the south row.
        /// </summary>
        public bool TryGetPlan(out int width, out int height, out bool[] floor, out bool[] pillar, out List<Vector3Int> socketList, out string error)
        {
            socketList = new List<Vector3Int>();   // (x, y, side)
            error = null;
            if (mode == TemplateMode.Prefab)
            {
                width = Mathf.Max(1, footprint.x);
                height = Mathf.Max(1, footprint.y);
                floor = new bool[width * height];
                pillar = new bool[width * height];
                for (int i = 0; i < floor.Length; i++)
                    floor[i] = true;

                if (legacyRoomBehaviour)
                {
                    socketList.Add(new Vector3Int(width / 2, height - 1, (int)Dir4.North));
                    socketList.Add(new Vector3Int(width / 2, 0, (int)Dir4.South));
                    socketList.Add(new Vector3Int(width - 1, height / 2, (int)Dir4.East));
                    socketList.Add(new Vector3Int(0, height / 2, (int)Dir4.West));
                }
                else
                {
                    foreach (Socket s in sockets)
                    {
                        if (s == null)
                            continue;
                        switch (s.side)
                        {
                            case Dir4.North: socketList.Add(new Vector3Int(Mathf.Clamp(s.offset, 0, width - 1), height - 1, (int)Dir4.North)); break;
                            case Dir4.South: socketList.Add(new Vector3Int(Mathf.Clamp(s.offset, 0, width - 1), 0, (int)Dir4.South)); break;
                            case Dir4.East: socketList.Add(new Vector3Int(width - 1, Mathf.Clamp(s.offset, 0, height - 1), (int)Dir4.East)); break;
                            default: socketList.Add(new Vector3Int(0, Mathf.Clamp(s.offset, 0, height - 1), (int)Dir4.West)); break;
                        }
                    }
                }
                if (socketList.Count == 0)
                {
                    error = $"Template '{name}' has no sockets: corridors can't reach it.";
                    return false;
                }
                return true;
            }

            string[] rows = (shapeMask ?? "").Replace("\r", "").Split('\n');
            var lines = new List<string>();
            foreach (string r in rows)
                if (r.Trim().Length > 0)
                    lines.Add(r);
            height = lines.Count;
            width = 0;
            foreach (string l in lines)
                width = Mathf.Max(width, l.Length);
            floor = new bool[Mathf.Max(1, width * height)];
            pillar = new bool[floor.Length];
            if (width == 0 || height == 0)
            {
                error = $"Template '{name}' has an empty shape mask.";
                return false;
            }

            var doors = new List<Vector2Int>();
            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                string line = lines[row];
                for (int x = 0; x < width; x++)
                {
                    char ch = x < line.Length ? line[x] : ' ';
                    int i = x + y * width;
                    switch (ch)
                    {
                        case '.': floor[i] = true; break;
                        case 'P': case 'p': pillar[i] = true; break;
                        case 'D': case 'd': floor[i] = true; doors.Add(new Vector2Int(x, y)); break;
                    }
                }
            }

            // A socket faces the side where the door cell touches the outside of the plan.
            foreach (Vector2Int d in doors)
            {
                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = d.x + Dir4Util.DX[dir], ny = d.y + Dir4Util.DY[dir];
                    bool outside = nx < 0 || ny < 0 || nx >= width || ny >= height || (!floor[nx + ny * width] && !pillar[nx + ny * width]);
                    if (outside)
                    {
                        socketList.Add(new Vector3Int(d.x, d.y, dir));
                        break;
                    }
                }
            }

            int floorCount = 0;
            foreach (bool f in floor)
                if (f)
                    floorCount++;
            if (floorCount == 0)
            {
                error = $"Template '{name}' has no floor cells ('.').";
                return false;
            }
            if (socketList.Count == 0)
            {
                error = $"Template '{name}' has no door sockets ('D' on its edge).";
                return false;
            }
            return true;
        }
    }
}
