using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static EndlessTerrain;
using Unity.VisualScripting;

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
        private TerrainGenerator terrainGenerator;
        private Texture2D splatmap;
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

            meshObject.transform.position = positionV3;
            meshObject.transform.parent = parent;
            SetVisible(false);

            mapGenerator.RequestMapData(OnMapDataReceived, globalOffset);
        }

        void OnMapDataReceived(MapData mapData)
        {
            mapGenerator.RequestTerrainData(mapData, OnTerrainDataReceived, globalOffset);
        }
        void OnBiomeObjectDataReceived(BiomeObjectData biomeObjectData)
        {

        }

        void OnTerrainDataReceived(TerrainData terrainData)
        {
            terrainGenerator = terrainData.terrainGenerator;
            splatmap = terrainData.splatMap;
            TextureGenerator.AssignTexture(terrainData.splatMap, terrainGenerator, meshRenderer);
            Mesh mesh = terrainData.meshData.UpdateMesh();
            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;
            if  (terrainData.heightMap == null) Debug.Log("zzzzzzzzzz");


            mapGenerator.RequestBiomeObjectData(OnBiomeObjectDataReceived, terrainData, globalOffset, meshObject.transform);
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
