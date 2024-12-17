//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using static EndlessTerrain;
//using UnityEngine;

//namespace Assets.Scripts.Procedural.Terrain
//{
//    internal class FuncationBackUp
//    {
//        void UpdateVisibleChunksOld()
//        {
//            // Hide all chunks from the last update.
//            for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++)
//            {
//                terrainChunksVisibleLastUpdate[i].SetVisible(false);
//            }
//            terrainChunksVisibleLastUpdate.Clear();

//            // Calculate the viewer's current chunk coordinates in the chunk grid.
//            // Each chunk is chunkSize units wide, so we divide the viewer's position by chunkSize
//            // and round to the nearest integer to get the chunk's grid coordinates.
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            // Iterate over all potential chunks within the viewing distance.
//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    // Calculate the coordinates of the chunk being considered.
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    // Limit the number of chunks if maximum chunks per side is enabled.
//                    if (shouldHaveMaxChunkPerSide)
//                    {
//                        // This condition ensures that the number of chunks generated or rendered does not exceed the maximum allowed per side.
//                        // It checks if the absolute value of either the x or y coordinate of the chunk exceeds the limit set by 'maxChunksPerSide'.
//                        // If either coordinate is outside the allowed range (greater than 'maxChunksPerSide'), the chunk is skipped to optimize performance,
//                        // preventing the generation or rendering of chunks that are too far from the viewer's position.
//                        // For example, if 'maxChunksPerSide' is 3, the system will only generate chunks within a 7x7 grid centered on the viewer (from -3 to 3 on both axes).
//                        if (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide)
//                        {
//                            continue;
//                        }
//                    }
//                    // Update existing chunk or create a new one if it doesn't exist.
//                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
//                    {
//                        // Update the chunk and check if it's visible.
//                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
//                        if (terrainChunkDictionary[viewedChunkCoord].IsVisible())
//                        {
//                            terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
//                        }

//                    }
//                    else
//                    {
//                        count++;
//                        TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);

//                    }
//                }
//            }


//        }
//        second:
//void UpdateVisibleChunks()
//        {
//            // Calculate the viewer's current chunk coordinates in the chunk grid.
//            // Each chunk is chunkSize units wide, so we divide the viewer's position by chunkSize
//            // and round to the nearest integer to get the chunk's grid coordinates.
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            // Keep track of currently visible chunks
//            HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();
//            // Iterate over all potential chunks within the viewing distance.
//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    // Calculate the coordinates of the chunk being considered.
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    // Skip chunks outside the maximum allowed range (if enabled)
//                    if (shouldHaveMaxChunkPerSide &&

//                        // This condition ensures that the number of chunks generated or rendered does not exceed the maximum allowed per side.
//                        // It checks if the absolute value of either the x or y coordinate of the chunk exceeds the limit set by 'maxChunksPerSide'.
//                        // If either coordinate is outside the allowed range (greater than 'maxChunksPerSide'), the chunk is skipped to optimize performance,
//                        // preventing the generation or rendering of chunks that are too far from the viewer's position.
//                        // For example, if 'maxChunksPerSide' is 3, the system will only generate chunks within a 7x7 grid centered on the viewer (from -
//                        (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide))
//                    {
//                        continue;
//                    }

//                    chunksToBeVisible.Add(viewedChunkCoord);

//                    // Update or create chunk
//                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
//                    {
//                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
//                    }
//                    else
//                    {
//                        count++;
//                        TerrainChunk newChunk = new TerrainChunk(viewedChunkCoord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//                        terrainChunkDictionary.Add(viewedChunkCoord, newChunk);
//                    }
//                }
//            }

//            // Hide chunks that are no longer within view
//            for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
//            {
//                TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
//                if (!chunksToBeVisible.Contains(chunk.Position / chunkSize)) // Check if chunk is still in the visible set
//                {
//                    chunk.SetVisible(false);
//                    terrainChunksVisibleLastUpdate.RemoveAt(i);
//                }
//            }

//            // Update list of currently visible chunks
//            terrainChunksVisibleLastUpdate.Clear();
//            foreach (Vector2 coord in chunksToBeVisible)
//            {
//                if (terrainChunkDictionary[coord].IsVisible())
//                {
//                    terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[coord]);
//                }
//            }
//        }

//        third:
//    void UpdateVisibleChunksNew()
//        {
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();

//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    if (shouldHaveMaxChunkPerSide &&
//                        (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide))
//                    {
//                        continue;
//                    }

//                    chunksToBeVisible.Add(viewedChunkCoord);

//                    if (!terrainChunkDictionary.ContainsKey(viewedChunkCoord))
//                    {
//                        StartCoroutine(LoadChunk(viewedChunkCoord));
//                    }
//                    else
//                    {
//                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
//                    }
//                }
//            }

//            // Hide chunks no longer visible
//            for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
//            {
//                TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
//                if (!chunksToBeVisible.Contains(chunk.Position / chunkSize))
//                {
//                    chunk.SetVisible(false);
//                    terrainChunksVisibleLastUpdate.RemoveAt(i);
//                }
//            }

//            terrainChunksVisibleLastUpdate.Clear();
//            foreach (Vector2 coord in chunksToBeVisible)
//            {
//                if (terrainChunkDictionary[coord].IsVisible())
//                {
//                    terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[coord]);
//                }
//            }
//        }

//        IEnumerator LoadChunk(Vector2 coord)
//        {
//            if (!terrainChunkDictionary.ContainsKey(coord))
//            {
//                count++;
//                TerrainChunk newChunk = new TerrainChunk(coord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//                terrainChunkDictionary.Add(coord, newChunk);
//                yield return null; // Pause for a frame
//            }
//        }
//        forth:

//    void UpdateVisibleChunks()
//        {
//            int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
//            int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

//            HashSet<Vector2> chunksToBeVisible = new HashSet<Vector2>();

//            // Iterate through chunks in view distance
//            for (int yOffset = -chunksVisibleInViewDst; yOffset <= chunksVisibleInViewDst; yOffset++)
//            {
//                for (int xOffset = -chunksVisibleInViewDst; xOffset <= chunksVisibleInViewDst; xOffset++)
//                {
//                    Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

//                    // Check max chunk boundary
//                    if (shouldHaveMaxChunkPerSide &&
//                        (Mathf.Abs(viewedChunkCoord.x) > maxChunksPerSide || Mathf.Abs(viewedChunkCoord.y) > maxChunksPerSide))
//                    {
//                        continue;
//                    }

//                    chunksToBeVisible.Add(viewedChunkCoord);

//                    // Check if chunk exists, otherwise create it
//                    if (!terrainChunkDictionary.TryGetValue(viewedChunkCoord, out TerrainChunk chunk))
//                    {
//                        chunk = CreateNewChunk(viewedChunkCoord);
//                    }

//                    chunk.UpdateTerrainChunk();
//                }
//            }

//            // Update visibility for chunks not in chunksToBeVisible
//            UpdateChunkVisibility(chunksToBeVisible);
//        }

//        // Helper to create a new chunk
//        TerrainChunk CreateNewChunk(Vector2 coord)
//        {
//            count++;
//            TerrainChunk newChunk = new TerrainChunk(coord, chunkSize, transform, portalManager.portalPrefabs, portalManager.globalMaxPortals, count);
//            terrainChunkDictionary.Add(coord, newChunk);
//            return newChunk;
//        }

//        // Helper to update visibility for old and new chunks
//        void UpdateChunkVisibility(HashSet<Vector2> chunksToBeVisible)
//        {
//            for (int i = terrainChunksVisibleLastUpdate.Count - 1; i >= 0; i--)
//            {
//                TerrainChunk chunk = terrainChunksVisibleLastUpdate[i];
//                Vector2 chunkCoord = chunk.Position / chunkSize;

//                if (!chunksToBeVisible.Contains(chunkCoord))
//                {
//                    chunk.SetVisible(false);
//                    terrainChunksVisibleLastUpdate.RemoveAt(i);
//                }
//            }

//            foreach (Vector2 coord in chunksToBeVisible)
//            {
//                TerrainChunk chunk = terrainChunkDictionary[coord];
//                if (chunk.IsVisible() && !terrainChunksVisibleLastUpdate.Contains(chunk))
//                {
//                    terrainChunksVisibleLastUpdate.Add(chunk);
//                }
//            }
//        }
//    }
//}
