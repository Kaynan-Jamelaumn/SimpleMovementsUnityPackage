using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomBehaviour : MonoBehaviour
{
    public GameObject[] walls; // Assuming 0 - North, 1 - South, 2 - East, 3 - West
    public GameObject[] doors;
    public GameObject[] upperWalls;
    public GameObject[] upperDoors;

    [System.Serializable]
    public class GridCell
    {
        public bool isOccupied;
        public GameObject occupant; // The item or mob in this cell
        public Vector3 position; // Store the position of the grid cell
    }

    [SerializeField] private GridCell[,] roomGrid;
    [SerializeField] private int gridRows = 5;
    [SerializeField] private int gridColumns = 5;

    [SerializeField] private Vector3 gridOrigin = new Vector3(0,0.25f,0); // Origin point of the grid
    [SerializeField] private Vector3 calcBasedOffSet = new Vector3(0.1f, 0, 0.1f); // Origin point of the grid
    [SerializeField] private float cellWidth; // Width of each cell in the grid
    [SerializeField] private float cellDepth; // Depth of each cell in the grid

    private void Awake()
    {
        CalculateRoomDimensionsAndInitializeGrid();
    }

    private void CalculateRoomDimensionsAndInitializeGrid()
    {
        // Calculate room dimensions based on the walls' positions
        float roomWidth = Mathf.Abs(walls[2].transform.position.x - walls[3].transform.position.x) - calcBasedOffSet.x;
        float roomDepth = Mathf.Abs(walls[0].transform.position.z - walls[1].transform.position.z) - calcBasedOffSet.z;

        // Calculate cell dimensions
        cellWidth = roomWidth / gridColumns;
        cellDepth = roomDepth / gridRows;

        // Set the grid origin (include the y position)
        float gridOriginY = transform.position.y + gridOrigin.y + calcBasedOffSet.y; // Assuming the room's y position is what we want for the grid
        gridOrigin = new Vector3(walls[3].transform.position.x, gridOriginY, walls[1].transform.position.z);

        // Initialize grid with the calculated dimensions
        InitializeGrid();
    }
    void InitializeGrid()
    {
        roomGrid = new GridCell[gridRows, gridColumns];
        for (int i = 0; i < gridRows; i++)
        {
            for (int j = 0; j < gridColumns; j++)
            {
                roomGrid[i, j] = new GridCell();

                // Calculate the world position of each grid cell
                float posX = gridOrigin.x + (j * cellWidth) + (cellWidth * 0.5f);
                float posY = gridOrigin.y; // Use the y value from gridOrigin
                float posZ = gridOrigin.z + (i * cellDepth) + (cellDepth * 0.5f);
                roomGrid[i, j].position = new Vector3(posX, posY, posZ); // grid cell position includes gridOrigin's y value

               // Debug.Log($"Grid Cell [{i},{j}]: isOccupied = {roomGrid[i, j].isOccupied}");
            }
        }
    }
    private void OnDrawGizmos()
    {
        if (roomGrid != null)
        {
            for (int i = 0; i < gridRows; i++)
            {
                for (int j = 0; j < gridColumns; j++)
                {
                    Vector3 position = new Vector3(roomGrid[i, j].position.x, gridOrigin.y, roomGrid[i, j].position.z);
                    Gizmos.color = Color.yellow;
                    // Use the correct y position for the gizmo, not just '1'
                    Gizmos.DrawWireCube(position, new Vector3(cellWidth, 0.1f, cellDepth));
                }
            }
        }
    }
    // Method to find an available cell and spawn a mob
    public Vector3? GetAvailableCellForSpawning()
    {
        if (roomGrid == null)
        {
            Debug.LogError("roomGrid is not initialized!");
            return null;
        }

        List<GridCell> availableCells = new List<GridCell>();
        foreach (GridCell cell in roomGrid)
        {
            if (!cell.isOccupied)
            {
                availableCells.Add(cell);
            }
        }

        if (availableCells.Count == 0)
        {
            return null; // No available cells
        }

        // Select a random available cell
        GridCell selectedCell = availableCells[UnityEngine.Random.Range(0, availableCells.Count)];
        selectedCell.isOccupied = true; // Mark the cell as occupied

        return selectedCell.position;
    }

    public void UpdateRoom(bool[] status)
    {
        for (int i = 0; i < status.Length; i++)
        {
            doors[i].SetActive(status[i]);
            walls[i].SetActive(!status[i]);
        }
    }

    public void UpdateUpperRoom(bool[] status)
    {
        for (int i = 0; i < status.Length; i++)
        {
            upperDoors[i].SetActive(status[i]);
            upperWalls[i].SetActive(!status[i]);
        }
    }
}