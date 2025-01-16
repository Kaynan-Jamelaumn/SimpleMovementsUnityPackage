using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomBehaviour : MonoBehaviour
{
    [SerializeField] private List<GameObject> walls; // 0 - North, 1 - South, 2 - East, 3 - West
    [SerializeField] private List<GameObject> doors;
    [SerializeField] private List<GameObject> upperWalls;
    [SerializeField] private List<GameObject> upperDoors;

    /// <summary>
    /// Represents a cell in the room grid.
    /// </summary>
    [System.Serializable]
    public class GridCell
    {
        /// <summary>
        /// Gets or sets a value indicating whether this cell is occupied.
        /// </summary>
        public bool IsOccupied { get; set; }

        /// <summary>
        /// Gets or sets the occupant GameObject in this cell.
        /// </summary>
        public GameObject Occupant { get; set; }

        /// <summary>
        /// Gets or sets the position of the grid cell.
        /// </summary>
        public Vector3 Position { get; set; }
    }

    [SerializeField] private GridCell[,] roomGrid;
    [SerializeField] private int gridRows = 5;
    [SerializeField] private int gridColumns = 5;

    [SerializeField] private Vector3 gridOrigin = new Vector3(0, 0.25f, 0); // Origin point of the grid
    [SerializeField] private Vector3 calcBasedOffSet = new Vector3(0.1f, 0, 0.1f); // Offset values for grid calculation
    [SerializeField] private float cellWidth; // Width of each cell in the grid
    [SerializeField] private float cellDepth; // Depth of each cell in the grid

    /// <summary>
    /// Unity's Awake method. Initializes the room dimensions and grid.
    /// </summary>
    private void Awake()
    {
        CalculateRoomDimensionsAndInitializeGrid();
    }

    /// <summary>
    /// Calculates the room dimensions and initializes the grid.
    /// </summary>
    private void CalculateRoomDimensionsAndInitializeGrid()
    {
        if (walls == null || walls.Count < 4)
        {
            Debug.LogError("Walls list is not properly configured.");
            return;
        }

        // Calculate room width and depth based on wall positions and offset values
        float roomWidth = Mathf.Abs(walls[2].transform.position.x - walls[3].transform.position.x) - calcBasedOffSet.x;
        float roomDepth = Mathf.Abs(walls[0].transform.position.z - walls[1].transform.position.z) - calcBasedOffSet.z;

        // Calculate cell dimensions
        cellWidth = roomWidth / gridColumns;
        cellDepth = roomDepth / gridRows;

        // Set the grid origin including the y position
        float gridOriginY = transform.position.y + gridOrigin.y + calcBasedOffSet.y;
        gridOrigin = new Vector3(walls[3].transform.position.x, gridOriginY, walls[1].transform.position.z);

        // Initialize the grid
        InitializeGrid();
    }

    /// <summary>
    /// Initializes the grid cells with their positions.
    /// </summary>
    private void InitializeGrid()
    {
        roomGrid = new GridCell[gridRows, gridColumns];
        for (int i = 0; i < gridRows; i++)
        {
            for (int j = 0; j < gridColumns; j++)
            {
                roomGrid[i, j] = new GridCell
                {
                    Position = new Vector3(
                        gridOrigin.x + (j * cellWidth) + (cellWidth * 0.5f),
                        gridOrigin.y,
                        gridOrigin.z + (i * cellDepth) + (cellDepth * 0.5f)
                    )
                };
            }
        }
    }

    /// <summary>
    /// Unity's OnDrawGizmos method. Draws gizmos in the editor for grid visualization.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (roomGrid != null)
        {
            foreach (GridCell cell in roomGrid)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(new Vector3(cell.Position.x, gridOrigin.y, cell.Position.z), new Vector3(cellWidth, 0.1f, cellDepth));
            }
        }
    }

    /// <summary>
    /// Finds an available cell for spawning and returns its position.
    /// </summary>
    /// <returns>Position of the available cell or null if no cell is available.</returns>
    public Vector3? GetAvailableCellForSpawning()
    {
        if (roomGrid == null)
        {
            Debug.LogError("Room grid is not initialized!");
            return null;
        }

        // Find the first available cell that is not occupied
        GridCell availableCell = roomGrid.Cast<GridCell>().FirstOrDefault(cell => !cell.IsOccupied);

        if (availableCell == null)
        {
            return null; // No available cells
        }

        availableCell.IsOccupied = true;
        return availableCell.Position;
    }

    /// <summary>
    /// Updates the room's doors and walls based on the status array.
    /// </summary>
    /// <param name="status">Array indicating the status of each door (true for open, false for closed).</param>
    /// <param name="isUpperRoom">If true, updates the upper room; otherwise, updates the main room.</param>
    public void UpdateRoom(bool[] status, bool isUpperRoom = false)
    {
        List<GameObject> targetDoors = isUpperRoom ? upperDoors : doors;
        List<GameObject> targetWalls = isUpperRoom ? upperWalls : walls;

        if (status == null || targetDoors == null || targetWalls == null)
        {
            Debug.LogError("Invalid status or room configuration.");
            return;
        }

        // Update the visibility of doors and walls based on the status array
        for (int i = 0; i < status.Length; i++)
        {
            if (i < targetDoors.Count && i < targetWalls.Count)
            {
                targetDoors[i].SetActive(status[i]);
                targetWalls[i].SetActive(!status[i]);
            }
        }
    }
}
