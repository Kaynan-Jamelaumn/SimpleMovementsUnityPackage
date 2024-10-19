using UnityEngine;
using System.Collections.Generic;

using Unity.AI.Navigation;

public class EndlessTerrain : MonoBehaviour
{
    public const float maxViewDst = 450;
    public Transform viewer;

    public static Vector2 viewerPosition;
    static TerrainGenerator mapGenerator;
    float scaleFactor = 1.0f;
    int chunkSize;
    int chunksVisibleInViewDst;
    public bool shouldHaveMaxChunkPerSide = true; // Maximum number of chunks per side
    public int maxChunksPerSide = 5; // Maximum number of chunks per side

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    void Start()
    {
        mapGenerator = FindObjectOfType<TerrainGenerator>();
        chunkSize = TerrainGenerator.chunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);
        scaleFactor = mapGenerator.ScaleFactor;
    }

    void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        UpdateVisibleChunks();
    }

    void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
        {
            terrainChunksVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (shouldHaveMaxChunkPerSide)
                {
                    if (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide)
                    {
                        continue;
                    }
                }

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                    if (terrainChunkDictionary[viewedChunkCoord].IsVisible())
                    {
                        terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
                    }
                }
                else
                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, transform));
                }
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        NavMeshSurface navMeshSurface;
        private TerrainGenerator terrainGenerator;

        private Texture2D splatmap;

        private WorldMobSpawner worldMobSpawner;

        Vector2 globalOffset;


        public TerrainChunk(Vector2 coord, int size, Transform parent)
        {
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);
            globalOffset = new Vector2(position.x, position.y);
            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            // Add NavMeshSurface component to the terrain chunk
            navMeshSurface = meshObject.AddComponent<NavMeshSurface>();
            navMeshSurface.collectObjects = CollectObjects.Children;

            worldMobSpawner = meshObject.AddComponent<WorldMobSpawner>();
            worldMobSpawner.Initialize(size, globalOffset, parent, navMeshSurface); //size = chunkSize

            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);

            // Initialize the WorldMobSpawner for this chunk
 

            mapGenerator.RequestMapData(OnMapDataReceived, globalOffset);
        }

        void OnMapDataReceived(MapData mapData)
        {
            mapGenerator.RequestTerrainData(mapData, OnTerrainDataReceived, globalOffset);
        }
        void OnBiomeObjectDataReceived(BiomeObjectData biomeObjectData)
        {

            BakeNavMesh();

            worldMobSpawner.SetBiomes(biomeObjectData.biomeMap);  // Pass the biome data to the spawner

            worldMobSpawner.StartSpawningMobs(biomeObjectData.heightMap);

        }

        void OnTerrainDataReceived(TerrainData terrainData)
        {
            terrainGenerator = terrainData.terrainGenerator;
            splatmap = terrainData.splatMap;
            TextureGenerator.AssignTexture(terrainData.splatMap, terrainGenerator, meshRenderer);
            Mesh mesh = terrainData.meshData.UpdateMesh();
            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;

            mapGenerator.RequestBiomeObjectData(OnBiomeObjectDataReceived, terrainData, globalOffset, meshObject.transform);
        }
        // Function to bake NavMesh after mesh is generated
        void BakeNavMesh()
        {
            navMeshSurface.BuildNavMesh(); // Bake the NavMesh for the chunk
        }


        public void UpdateTerrainChunk()
        {
            float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
            bool visible = viewerDstFromNearestEdge <= maxViewDst;
            SetVisible(visible);
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }


    }
}
