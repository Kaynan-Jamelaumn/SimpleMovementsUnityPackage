using System.Collections.Generic;
using ProceduralDungeon;
using UnityEngine;

namespace ProceduralCaves
{
    /// <summary>One floor's rock formations as mesh data, per mesh chunk (see <see cref="CaveFormationMesher"/>).</summary>
    public sealed class CaveFloorFormations
    {
        public sealed class Chunk
        {
            public Vector2Int Key;
            /// <summary>Stalagmites, stalactites, columns, boulders (the theme's cave wall surface) and rubble (cave floor).</summary>
            public readonly MeshBuffers Rock = new MeshBuffers();
            /// <summary>Crystals (drawn with a glowing material).</summary>
            public readonly MeshBuffers Crystal = new MeshBuffers();
            /// <summary>Simple solids round every standing formation of the chunk (its collider; empty = none).</summary>
            public readonly MeshBuffers Collider = new MeshBuffers();
        }

        /// <summary>A glowing light in a crystal grotto (floor-local position).</summary>
        public struct GlowLight
        {
            public Vector3 Position;
            public Color Color;
        }

        public int Floor;
        /// <summary>The colour of the floor's crystals and of their light (one per floor, from its build seed).</summary>
        public Color CrystalColor = new Color(0.45f, 0.8f, 1f);
        public readonly List<Chunk> Chunks = new List<Chunk>();
        public readonly List<GlowLight> Lights = new List<GlowLight>();
        public int Standing, Hanging, Crystals, Rubble;

        public bool IsEmpty => Chunks.Count == 0;
    }

    /// <summary>
    /// Builds a floor's rock formations from its grid and its build seed - the same on the server and on every client,
    /// which get the grid (with the <see cref="CellFlags.Formation"/> cells the cave stage chose) and the build seed with
    /// each floor:
    /// <list type="bullet">
    /// <item>on every formation cell a standing formation: a column where the ceiling is low enough (floor to ceiling), a
    /// boulder pile now and then, a crystal cluster in a crystal grotto, otherwise a stalagmite (often with a stalactite
    /// above it) - each with a simple collider, so players and creatures walk round it;</item>
    /// <item>stalactites hanging from high ceilings (never below the head room), loose rubble along walls;</item>
    /// <item>crystal clusters set into the walls of crystal grottos, a few of them with a light.</item>
    /// </list>
    /// The shapes are batched per mesh chunk (one renderer per chunk instead of one object per rock). Plain data: runs
    /// on a worker thread.
    /// </summary>
    public static class CaveFormationMesher
    {
        private static readonly int SaltKind = PlacementRandom.StableHash("CaveFormationKind");
        private static readonly int SaltHang = PlacementRandom.StableHash("CaveStalactite");
        private static readonly int SaltRubble = PlacementRandom.StableHash("CaveRubble");
        private static readonly int SaltWallCrystal = PlacementRandom.StableHash("CaveWallCrystal");

        /// <summary>The crystal colours a floor picks from.</summary>
        public static readonly Color[] CrystalHues =
        {
            new Color(0.45f, 0.8f, 1f), new Color(0.7f, 0.45f, 1f), new Color(0.4f, 1f, 0.75f), new Color(1f, 0.55f, 0.8f), new Color(0.95f, 0.85f, 0.5f),
        };

        public static CaveFloorFormations Build(DungeonLayout layout, int floorIndex, CaveRules rules, float wallRoughness, int chunkCells, float textureScale = 3f)
        {
            var result = new CaveFloorFormations { Floor = floorIndex };
            if (layout == null || rules == null || floorIndex < 0 || floorIndex >= layout.Floors.Count || layout.Floors[floorIndex] == null)
                return result;
            CaveFormationSettings fs = rules.Formations;
            if (!fs.enabled)
                return result;
            FloorLayout floor = layout.Floors[floorIndex];
            TileGrid g = floor.Grid;
            if (g == null)
                return result;
            float cs = layout.CellSize;
            int seed = layout.FloorBuildSeed(floorIndex);
            Color hue = CrystalHues[(int)(PlacementRandom.Hash(seed, SaltWallCrystal, 0, 0, 99) % (uint)CrystalHues.Length)];
            result.CrystalColor = hue;
            chunkCells = Mathf.Max(4, chunkCells);
            var chunks = new Dictionary<Vector2Int, CaveFloorFormations.Chunk>();
            CaveFloorFormations.Chunk ChunkAt(int cell)
            {
                var key = new Vector2Int(g.X(cell) / chunkCells, g.Y(cell) / chunkCells);
                if (!chunks.TryGetValue(key, out CaveFloorFormations.Chunk c))
                {
                    c = new CaveFloorFormations.Chunk { Key = key };
                    c.Rock.TextureScale = c.Crystal.TextureScale = Mathf.Max(0.1f, textureScale);
                    chunks[key] = c;
                }
                return c;
            }
            float R(int x, int y, int salt, int stream) => PlacementRandom.Value(seed, salt, x, y, stream);
            Vector3 Center(int cell, float height) => new Vector3((g.X(cell) + 0.5f) * cs, height, (g.Y(cell) + 0.5f) * cs);
            bool IsGrotto(int cell)
            {
                int a = g.Area[cell];
                return a >= 0 && a < floor.Areas.Count && floor.Areas[a].Tag == CavePlacements.GrottoTag;
            }

            float headRoom = Mathf.Max(1.8f, fs.headRoom);
            float maxRadius = 0.42f * cs;
            var grottoLights = new Dictionary<int, int>();

            for (int c = 0; c < g.Count; c++)
            {
                if (!g.IsWalkable(c))
                    continue;
                int x = g.X(c), y = g.Y(c);
                float floorH = g.FloorHeight[c], ceilH = g.CeilingHeight[c];
                float room = ceilH - floorH;
                bool cave = g.Has(c, CellFlags.Organic);
                bool openAbove = !g.Has(c, CellFlags.NoCeiling | CellFlags.Pit | CellFlags.Chasm | CellFlags.Roofed | CellFlags.Outdoor | CellFlags.Climb);

                // ---- standing formations (decided by the cave stage)
                if (g.Has(c, CellFlags.Formation))
                {
                    CaveFloorFormations.Chunk chunk = ChunkAt(c);
                    float kind = R(x, y, SaltKind, 0);
                    float radius = Mathf.Min(maxRadius, cs * (0.26f + 0.12f * R(x, y, SaltKind, 1)));
                    var jitter = new Vector3((R(x, y, SaltKind, 2) - 0.5f) * 0.16f * cs, 0f, (R(x, y, SaltKind, 3) - 0.5f) * 0.16f * cs);
                    Vector3 basePos = Center(c, floorH) + jitter;
                    int shapeSeed = (int)PlacementRandom.Hash(seed, SaltKind, x, y, 9);
                    float colliderHeight;
                    if (IsGrotto(c) && kind < 0.75f)
                    {
                        float size = cs * (0.8f + 0.7f * R(x, y, SaltKind, 4));
                        CaveRockMesh.Boulder(chunk.Rock, basePos + Vector3.up * 0.05f, new Vector3(radius, radius * 0.45f, radius), Quaternion.identity, shapeSeed, DungeonSurface.CaveWall, 5, 8);
                        CaveRockMesh.CrystalCluster(chunk.Crystal, basePos + Vector3.up * 0.1f, Vector3.up, Mathf.Min(size, room - 0.6f), shapeSeed, DungeonSurface.CaveWall);
                        colliderHeight = Mathf.Min(size, room - 0.6f);
                        result.Crystals++;
                        AddGrottoLight(result, grottoLights, g.Area[c], basePos + Vector3.up * (colliderHeight * 0.6f), hue, fs.grottoLights);
                    }
                    else if (room <= fs.columnMaxCeiling && room > 2.2f && kind < 0.35f)
                    {
                        CaveRockMesh.Column(chunk.Rock, basePos, Center(c, ceilH) + jitter * 0.6f, radius * 1.1f, shapeSeed, DungeonSurface.CaveWall);
                        colliderHeight = room;
                    }
                    else if (kind < 0.35f + fs.boulderShare)
                    {
                        float r = radius * 1.15f;
                        CaveRockMesh.Boulder(chunk.Rock, basePos + Vector3.up * (r * 0.55f), new Vector3(r, r * 0.8f, r * 0.95f),
                            Quaternion.Euler(0f, R(x, y, SaltKind, 5) * 360f, 0f), shapeSeed, DungeonSurface.CaveWall);
                        if (R(x, y, SaltKind, 6) < 0.6f)
                        {
                            float r2 = r * 0.55f;
                            Vector3 side = new Vector3(Mathf.Cos(kind * 40f), 0f, Mathf.Sin(kind * 40f)) * (r * 0.85f);
                            CaveRockMesh.Boulder(chunk.Rock, basePos + side + Vector3.up * (r2 * 0.5f), new Vector3(r2, r2 * 0.75f, r2), Quaternion.identity, shapeSeed + 17, DungeonSurface.CaveWall, 5, 8);
                        }
                        colliderHeight = r * 1.4f;
                    }
                    else
                    {
                        float height = Mathf.Min(room - 0.4f, cs * (0.9f + 1.4f * R(x, y, SaltKind, 7)));
                        CaveRockMesh.Spike(chunk.Rock, basePos, Mathf.Max(0.6f, height), radius, false, shapeSeed, DungeonSurface.CaveWall);
                        colliderHeight = Mathf.Max(0.6f, height);
                        // Often a stalactite above it, dripping onto it.
                        float hang = room - height - headRoom * 0.35f;
                        if (openAbove && hang > 0.5f && R(x, y, SaltKind, 8) < 0.65f)
                        {
                            CaveRockMesh.Spike(chunk.Rock, Center(c, ceilH) + jitter, Mathf.Min(hang, room * 0.4f), radius * 0.8f, true, shapeSeed + 5, DungeonSurface.CaveWall);
                            result.Hanging++;
                        }
                    }
                    if (fs.colliders && colliderHeight > 0.2f)
                        CaveRockMesh.Prism(chunk.Collider, basePos + Vector3.down * 0.1f, colliderHeight + 0.1f, radius * 0.92f, DungeonSurface.CaveWall);
                    result.Standing++;
                    continue;
                }

                if (!cave)
                    continue;

                // ---- stalactites from high ceilings (never lower than the head room)
                if (openAbove && room >= fs.stalactiteMinCeiling && R(x, y, SaltHang, 0) < fs.stalactitesPerHundredCells / 100f)
                {
                    float length = Mathf.Min(room - headRoom, cs * (0.5f + 1.6f * R(x, y, SaltHang, 1)));
                    if (length > 0.35f)
                    {
                        var at = Center(c, ceilH) + new Vector3((R(x, y, SaltHang, 2) - 0.5f) * 0.7f * cs, 0f, (R(x, y, SaltHang, 3) - 0.5f) * 0.7f * cs);
                        CaveRockMesh.Spike(ChunkAt(c).Rock, at, length, Mathf.Min(maxRadius, cs * (0.1f + 0.12f * R(x, y, SaltHang, 4))), true,
                            (int)PlacementRandom.Hash(seed, SaltHang, x, y, 5), DungeonSurface.CaveWall);
                        result.Hanging++;
                    }
                }

                // ---- by the walls: rubble, and crystals in grottos
                int wallSide = -1;
                for (int d = 0; d < 4 && wallSide < 0; d++)
                {
                    int nb = g.Neighbor(c, d);
                    if (nb >= 0 && g.IsRock(nb) && !g.Has(nb, CellFlags.Chasm))
                        wallSide = d;
                }
                if (wallSide < 0 || g.Has(c, CellFlags.Occupied | CellFlags.Landing | CellFlags.Locked) || g.Type[c] == CellType.Door)
                    continue;
                var delta = ((Dir4)wallSide).Delta();
                Vector2 wallAt = CaveWalls.WallPoint(g, cs, seed, wallRoughness, x, y, x + delta.x, y + delta.y);
                var inward = new Vector3(-delta.x, 0f, -delta.y);
                if (IsGrotto(c) && R(x, y, SaltWallCrystal, 0) < fs.grottoCrystalsPerHundredCells / 100f * 3f)
                {
                    float up = Mathf.Min(room - 1f, 0.3f + 1.8f * R(x, y, SaltWallCrystal, 1));
                    var root = new Vector3(wallAt.x, floorH + Mathf.Max(0.1f, up), wallAt.y) - inward * 0.1f;
                    Vector3 normal = (inward + Vector3.up * (0.2f + R(x, y, SaltWallCrystal, 2))).normalized;
                    int shapeSeed = (int)PlacementRandom.Hash(seed, SaltWallCrystal, x, y, 3);
                    CaveRockMesh.CrystalCluster(ChunkAt(c).Crystal, root, normal, cs * (0.35f + 0.35f * R(x, y, SaltWallCrystal, 4)), shapeSeed, DungeonSurface.CaveWall);
                    result.Crystals++;
                    AddGrottoLight(result, grottoLights, g.Area[c], root + normal * 0.4f, hue, fs.grottoLights);
                }
                else if (R(x, y, SaltRubble, 0) < fs.rubblePerHundredCells / 100f * 2.5f)
                {
                    CaveFloorFormations.Chunk chunk = ChunkAt(c);
                    int pieces = 1 + (int)(R(x, y, SaltRubble, 1) * 3f);
                    for (int k = 0; k < pieces; k++)
                    {
                        float r = 0.08f + 0.14f * R(x, y, SaltRubble, 2 + k);
                        var along = new Vector3(delta.y, 0f, delta.x) * ((R(x, y, SaltRubble, 6 + k) - 0.5f) * cs * 0.8f);
                        var p = new Vector3(wallAt.x, floorH + r * 0.35f, wallAt.y) + inward * (0.15f + r) + along;
                        CaveRockMesh.Boulder(chunk.Rock, p, new Vector3(r, r * 0.7f, r), Quaternion.identity,
                            (int)PlacementRandom.Hash(seed, SaltRubble, x, y, 10 + k), DungeonSurface.CaveFloor, 4, 6);
                    }
                    result.Rubble++;
                }
            }

            var keys = new List<Vector2Int>(chunks.Keys);
            keys.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            foreach (Vector2Int k in keys)
            {
                CaveFloorFormations.Chunk chunk = chunks[k];
                chunk.Rock.FinishNormals();
                chunk.Crystal.FinishNormals();
                if (!chunk.Rock.IsEmpty || !chunk.Crystal.IsEmpty)
                    result.Chunks.Add(chunk);
            }
            return result;
        }

        private static void AddGrottoLight(CaveFloorFormations result, Dictionary<int, int> perArea, int area, Vector3 at, Color color, int max)
        {
            if (max <= 0)
                return;
            perArea.TryGetValue(area, out int n);
            if (n >= max)
                return;
            // Spread out: not right next to another light.
            foreach (CaveFloorFormations.GlowLight l in result.Lights)
                if ((l.Position - at).sqrMagnitude < 9f)
                    return;
            perArea[area] = n + 1;
            result.Lights.Add(new CaveFloorFormations.GlowLight { Position = at, Color = color });
        }
    }
}
