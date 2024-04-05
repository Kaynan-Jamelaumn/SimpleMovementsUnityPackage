using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class WorldManager : MonoBehaviour
{

    private Dictionary<Vector2Int, TerrainChunk> loadedChunks;
    private Transform playerTransform;

    private void Start()
    {
        loadedChunks = new Dictionary<Vector2Int, TerrainChunk>();
        playerTransform = GameObject.FindWithTag("Player").transform; // Ensure the player has the "Player" tag
    }

    public void Update()
    {
        Vector2Int playerChunkPosition = CalculateChunkPositionFromPlayer();
        LoadSurroundingChunks(playerChunkPosition);
        UnloadDistantChunks(playerChunkPosition);
    }

    Vector2Int CalculateChunkPositionFromPlayer()
    {
		// calculate the distance of the player near the edge of the chunk
		// Calculate the chunk position based on the player's position.
		// Example: return new Vector2Int(playerPosition.x / chunkSize, playerPosition.z / chunkSize);
        return new Vector2Int(0, 0);

	}

	void LoadSurroundingChunks(Vector2Int centerChunk)
    {
		// Loop through surrounding chunk positions and load them if they're not already loaded.
	}
    void UnloadDistantChunks(Vector2Int playerChunkPosition)
    {

    }

}

public class TerrainChunk
{
	// This class would handle the generation and management of a single chunk of terrain.
}