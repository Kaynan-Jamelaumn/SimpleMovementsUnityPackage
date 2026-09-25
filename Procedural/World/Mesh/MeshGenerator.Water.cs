using System.Collections.Generic;
using UnityEngine;

// MeshGenerator, part 2 of 2: the water mesh (see MeshGenerator.cs).
public static partial class MeshGenerator
{
    /// <summary>
    /// Generates a chunk's water mesh from its water map (see <see cref="WaterGenerator"/>), using the same
    /// grid/LOD sampling as <see cref="GenerateTerrainMesh"/> so water vertices line up with the terrain
    /// mesh underneath.
    ///
    /// A quad is drawn as soon as ANY of its corners is wet. Its dry corners sit at the lower of the
    /// nearby water body's level (<see cref="WaterMapData.ShoreLevel"/>) and the terrain height. Rims,
    /// banks and coasts are kept above the water, so normally the flat water just slides under the land
    /// and the visible shoreline is exactly where terrain crosses the water surface - no gap between water
    /// and shore. If terrain were ever lower, the water meets it instead of floating. Quads past this
    /// chunk's own extent (the neighbor draws those too) are skipped, so transparent water is never
    /// blended twice along a chunk border.
    /// </summary>
    /// <param name="terrainGenerator">Terrain generator, for ScaleFactor.</param>
    /// <param name="heightMap">The chunk's terrain height map.</param>
    /// <param name="water">The chunk's water map. Null returns null.</param>
    /// <param name="lod">Must match the LOD the chunk's terrain mesh was built with, so shorelines line up.</param>
    /// <param name="globalOffset">The chunk's world offset, for world-space UVs that tile seamlessly across chunks.</param>
    /// <returns>Water mesh data, or null if this chunk has no water at all.</returns>
    public static WaterMeshData GenerateWaterMesh(TerrainGenerator terrainGenerator, float[,] heightMap, WaterMapData water, int lod = 0, Vector2 globalOffset = default)
    {
        if (water == null)
            return null;

        int mapWidth = heightMap.GetLength(0);
        int mapHeight = heightMap.GetLength(1);

        int lodFactor = lod > 0 ? lod * 2 : 1;
        int meshWidth = (mapWidth - 1) / lodFactor;
        int meshHeight = (mapHeight - 1) / lodFactor;

        // Chunks are spaced (map size - 2) cells apart, so the last height map column/row is the next
        // chunk's first: quads reaching past it belong to the neighbor.
        int lastOwnedX = mapWidth - 2;
        int lastOwnedY = mapHeight - 2;

        WaterMeshData meshData = new WaterMeshData(meshWidth, meshHeight);
        // A quad whose wet corners differ in water height by more than this is a waterfall sheet (steeper
        // than ~50 degrees at this vertex spacing); gentler rapids stay river.
        float waterfallRise = 1.2f * lodFactor;
        float scaleFactor = terrainGenerator.ScaleFactor;
        const float uvScale = 1f / 20f;
        bool anyQuad = false;

        int vertexIndex = 0;
        for (int y = 0; y <= meshHeight; y++)
        {
            for (int x = 0; x <= meshWidth; x++)
            {
                int heightMapX = Mathf.Min(x * lodFactor, mapWidth - 1);
                int heightMapY = Mathf.Min(y * lodFactor, mapHeight - 1);
                float terrain = heightMap[heightMapX, heightMapY];

                float waterHeight;
                if (water.IsWet(heightMapX, heightMapY))
                {
                    waterHeight = water.Surface[heightMapX, heightMapY];
                }
                else
                {
                    float shore = water.ShoreLevel[heightMapX, heightMapY];
                    waterHeight = float.IsNaN(shore) ? terrain : Mathf.Min(shore, terrain);
                }

                meshData.vertices[vertexIndex] = new Vector3(heightMapX * scaleFactor, waterHeight, heightMapY * scaleFactor);
                meshData.uvs[vertexIndex] = new Vector2((globalOffset.x + heightMapX) * uvScale, (globalOffset.y + heightMapY) * uvScale);
                // For custom water shaders: r = water depth (0-1 over 10 units, 0 at the shoreline, e.g. for
                // foam/fade), g = water type (0.25 ocean, 0.5 lake, 0.75 pond, 1 river, 0 dry).
                float depth = Mathf.Clamp01((waterHeight - terrain) / 10f);
                meshData.colors[vertexIndex] = new Color(depth, (int)water.Type[heightMapX, heightMapY] / 4f, 0f, 1f);
                // Second UV channel: flow direction x speed, for shaders that scroll ripples downstream.
                meshData.flows[vertexIndex] = new Vector2(water.FlowX[heightMapX, heightMapY], water.FlowY[heightMapX, heightMapY]);

                if (x < meshWidth && y < meshHeight)
                {
                    int nextHeightMapX = Mathf.Min((x + 1) * lodFactor, mapWidth - 1);
                    int nextHeightMapY = Mathf.Min((y + 1) * lodFactor, mapHeight - 1);

                    if (nextHeightMapX <= lastOwnedX && nextHeightMapY <= lastOwnedY)
                    {
                        WaterBodyType type = QuadWaterType(
                            water.Type[heightMapX, heightMapY],
                            water.Type[nextHeightMapX, heightMapY],
                            water.Type[heightMapX, nextHeightMapY],
                            water.Type[nextHeightMapX, nextHeightMapY]);

                        if (type != WaterBodyType.None)
                        {
                            if (IsWaterfall(water, heightMapX, heightMapY, nextHeightMapX, nextHeightMapY, waterfallRise))
                                type = WaterBodyType.Waterfall;

                            int topLeft = y * (meshWidth + 1) + x;
                            int topRight = topLeft + 1;
                            int bottomLeft = (y + 1) * (meshWidth + 1) + x;
                            int bottomRight = bottomLeft + 1;

                            // Same triangulation as the terrain mesh, so the water and terrain surfaces are
                            // compared triangle-for-triangle and cross along clean straight shorelines.
                            List<int> triangles = meshData.submeshTriangles[WaterMeshData.SubmeshIndex(type)];
                            triangles.Add(topLeft);
                            triangles.Add(bottomLeft);
                            triangles.Add(topRight);
                            triangles.Add(topRight);
                            triangles.Add(bottomLeft);
                            triangles.Add(bottomRight);
                            anyQuad = true;
                        }
                    }
                }

                vertexIndex++;
            }
        }

        return anyQuad ? meshData : null;
    }

    /// <summary>True when the quad's wet corners span more than <paramref name="rise"/> in water height.</summary>
    private static bool IsWaterfall(WaterMapData water, int x0, int y0, int x1, int y1, float rise)
    {
        float min = float.MaxValue, max = float.MinValue;
        Consider(water, x0, y0, ref min, ref max);
        Consider(water, x1, y0, ref min, ref max);
        Consider(water, x0, y1, ref min, ref max);
        Consider(water, x1, y1, ref min, ref max);
        return max - min > rise;
    }

    private static void Consider(WaterMapData water, int x, int y, ref float min, ref float max)
    {
        if (!water.IsWet(x, y))
            return;
        float surface = water.Surface[x, y];
        if (surface < min) min = surface;
        if (surface > max) max = surface;
    }

    /// <summary>
    /// Which type a quad renders as when its corners touch different water bodies: the lowest enum value
    /// wins (ocean, lake, pond, river), so e.g. a river mouth quad is drawn as the body it flows into.
    /// </summary>
    private static WaterBodyType QuadWaterType(WaterBodyType a, WaterBodyType b, WaterBodyType c, WaterBodyType d)
    {
        WaterBodyType best = WaterBodyType.None;
        best = PreferWaterType(best, a);
        best = PreferWaterType(best, b);
        best = PreferWaterType(best, c);
        best = PreferWaterType(best, d);
        return best;
    }

    private static WaterBodyType PreferWaterType(WaterBodyType current, WaterBodyType candidate)
    {
        if (candidate == WaterBodyType.None)
            return current;
        if (current == WaterBodyType.None || candidate < current)
            return candidate;
        return current;
    }
}
