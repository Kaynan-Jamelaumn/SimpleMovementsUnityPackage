using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.AI.Navigation;
using System.Drawing;
using static DataStructure;
/// <summary>
/// Manages the creation and updating of an infinite terrain system around the viewer's position.
/// This system generates terrain chunks dynamically based on proximity, ensuring performance optimization 
/// by only displaying terrain chunks within a specified viewing distance.
/// </summary>
/// <example>
/// Attach the `EndlessTerrain` script to an empty GameObject.
/// Assign a `TerrainGenerator` script to the scene and link it to the EndlessTerrain.
/// Set a `viewer` Transform (e.g., the player's camera).
/// </example>
public partial class EndlessTerrain : MonoBehaviour
{
    [Tooltip("Enable to show debug messages in the console")]
    [SerializeField]
    public bool enableDebugging = false;

    [Tooltip("If Should use HDRP Shader if not, will use URP Shaders(Lighter)")]
    [SerializeField]
    public bool shouldUseHDRPShaders = false;

    [Tooltip("Maximum distance (in world units) from the viewer within which terrain chunks are displayed.")]
    [SerializeField]
    public float maxViewDst = 250;

    [Tooltip("The object (e.g., player or camera) whose position determines terrain visibility.")]
    public Transform viewer;
    /// <summary>
    /// The viewer's position in world space, represented as a 2D coordinate (x, z).
    /// Used for efficient distance calculations, ignoring the y-axis (height).
    /// </summary>
    [Tooltip("Viewer's position in world space (x, z), ignoring height.")]
    public static Vector2 viewerPosition;

    [Tooltip("Reference to the terrain generator responsible for creating terrain data.")]
    static TerrainGenerator mapGenerator;

    [Tooltip("Scale factor affecting terrain features (size, spacing, etc.).")]
    [SerializeField] float scaleFactor = 1.0f;

    [Tooltip("The size of each terrain chunk in world units.")]
    int chunkSize;

    [Tooltip("Number of terrain chunks visible within the maximum viewing distance.")]
    int chunksVisibleInViewDst;

    [Tooltip("Enable to limit the number of visible chunks per side.")]
    public bool shouldHaveMaxChunkPerSide = true;

    [Tooltip("Maximum number of terrain chunks generated per side.")]
    public int maxChunksPerSide = 5;

    [Tooltip("Dictionary storing terrain chunks, keyed by their 2D coordinates.")]
    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();

    [Tooltip("List of terrain chunks visible during the last frame update.")]
    List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    [Tooltip("Configuration settings for spawning portals.")]
    [SerializeField]
    PortalSettings portalSettings;

    [Tooltip("Configuration settings for spawning mobs.")]
    [SerializeField]
    MobSettings mobSettings;


    int count = -1;

    void Start()
    {
        mapGenerator = Object.FindFirstObjectByType<TerrainGenerator>();

        if (mapGenerator == null)
        {
            Debug.LogError("TerrainGenerator not found! Please add a TerrainGenerator to the scene.");
            return;
        }

        chunkSize = mapGenerator.ChunkSize - 1;
        chunksVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);

        // Get scaleFactor and ensure it's not 0
        scaleFactor = mapGenerator.ScaleFactor;
        if (scaleFactor <= 0)
        {
            if (enableDebugging)
                Debug.LogWarning($"TerrainGenerator ScaleFactor was {scaleFactor}, setting to 1.0");
            scaleFactor = 1.0f;
            mapGenerator.ScaleFactor = 1.0f;
        }

        if (enableDebugging)
            Debug.Log($"EndlessTerrain initialized - ChunkSize: {chunkSize}, ScaleFactor: {scaleFactor}, ChunksVisible: {chunksVisibleInViewDst}, MaxViewDistance: {maxViewDst}");
    }

    void Update()
    {
        if (mapGenerator == null) return;

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        UpdateVisibleChunks();
    }
    /// <summary>
    /// Updates the visibility of terrain chunks based on the viewer's current position.
    /// This method ensures only chunks within the viewing distance are active,
    /// dynamically hiding or creating chunks as necessary.
    /// </summary>
    void UpdateVisibleChunks()
    {
        // Calculate the viewer's current chunk coordinates in the chunk grid.
        // Each chunk is `chunkSize` units wide, so we divide the viewer's position by `chunkSize`
        // and round to the nearest integer to get the chunk's grid coordinates.
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        // Calculate the effective chunk range based on both view distance and max chunks per side setting
        int effectiveChunkRange = shouldHaveMaxChunkPerSide ?
            Mathf.Min(chunksVisibleInViewDst, maxChunksPerSide) :
            chunksVisibleInViewDst;

        if (enableDebugging && Time.frameCount % 60 == 0) // Log every 60 frames to avoid spam
        {
            Debug.Log($"Effective chunk range: {effectiveChunkRange} (chunksVisibleInViewDst: {chunksVisibleInViewDst}, maxChunksPerSide: {maxChunksPerSide}, shouldHaveMaxChunkPerSide: {shouldHaveMaxChunkPerSide})");
        }

        HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();
        // Iterate through chunks in effective range

        for (int yOffset = -effectiveChunkRange; yOffset <= effectiveChunkRange; yOffset++)
        {
            for (int xOffset = -effectiveChunkRange; xOffset <= effectiveChunkRange; xOffset++)
            {
                // Calculate the coordinates of the chunk being considered.
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                chunksToBeVisible.Add(viewedChunkCoord);

                if (!terrainChunkDictionary.TryGetValue(viewedChunkCoord, out TerrainChunk chunk))
                {
                    chunk = CreateNewChunk(viewedChunkCoord);
                }

                chunk.UpdateTerrainChunk();
            }
        }

        UpdateChunkVisibility(chunksToBeVisible);
    }

    TerrainChunk CreateNewChunk(Vector2 coord)
    {
        count++;
        if (enableDebugging)
            Debug.Log($"Creating new chunk {count} at coord {coord}, chunkSize: {chunkSize}, scaleFactor: {scaleFactor}");

        TerrainChunk newChunk = new TerrainChunk(coord, chunkSize, scaleFactor, transform, portalSettings, mobSettings, count, shouldUseHDRPShaders, enableDebugging, maxViewDst);
        terrainChunkDictionary.Add(coord, newChunk);
        return newChunk;
    }

    void UpdateChunkVisibility(HashSet<Vector2> chunksToBeVisible)
    {
        for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
        {
            TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
            Vector2 chunkCoord = chunk.Position / chunkSize;

            if (!chunksToBeVisible.Contains(chunkCoord))
            {
                chunk.SetVisible(false);
                terrainChunksVisibleLastUpdate.RemoveAt(i);
            }
        }

        foreach (Vector2 coord in chunksToBeVisible)
        {
            TerrainChunk chunk = terrainChunkDictionary[coord];
            if (chunk.IsVisible() && !terrainChunksVisibleLastUpdate.Contains(chunk))
            {
                terrainChunksVisibleLastUpdate.Add(chunk);
            }
        }
    }

    public TerrainChunk GetRandomActiveChunk()
    {
        if (terrainChunksVisibleLastUpdate.Count == 0) return null;
        return terrainChunksVisibleLastUpdate[Random.Range(0, terrainChunksVisibleLastUpdate.Count)];
    }

    /// <summary>
    /// Draws the erosion debug overlay (see <see cref="TerrainGenerator.VisualizeErosionDebug"/>) for
    /// every currently visible chunk. Terrain chunks only exist once generated at runtime, so this only
    /// has anything to draw while in Play mode with the viewer having moved terrain into view.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (mapGenerator == null || !mapGenerator.VisualizeErosionDebug || terrainChunksVisibleLastUpdate == null)
            return;

        foreach (TerrainChunk chunk in terrainChunksVisibleLastUpdate)
        {
            chunk?.DrawErosionGizmos(mapGenerator);
        }
    }
}
