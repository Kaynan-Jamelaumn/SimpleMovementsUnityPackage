
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


/// <summary>
/// Manages the generation of a multi-floor dungeon with customizable room types, rules, and constraints.
/// </summary>
public class DungeonGenerator : MonoBehaviour, IAssignmentsValidator
{
    /// <summary>
    /// Represents a cell in the dungeon grid.
    /// Each cell can have walls, a room type, and a visited status.
    /// </summary>
    public class Cell
    {
        public bool visited = false; // Tracks whether this cell has been visited during maze generation.
        public bool[] status = new bool[4]; // Represents walls: [0] = up, [1] = down, [2] = left, [3] = right.
        public GameObject roomType; // The room type (GameObject) associated with this cell.
    }

    /// <summary>
    /// Defines the rules for room placement in the dungeon.
    /// Includes constraints like position limits, floor levels, and max spawn counts.
    /// </summary>
    [System.Serializable]
    public class Rule
    {
        public GameObject room; // Prefab for the room.
        public Vector3Int minPosition; // Minimum position (x, y, z) where this room can spawn.
        public Vector3Int maxPosition; // Maximum position (x, y, z) where this room can spawn.
        public bool obligatory; // If true, this room must spawn.
        public int maxRoomsPerFloor; // Maximum rooms of this type per floor.
        public int totalMaxRooms; // Maximum total number of rooms of this type.
        public int minFloorTospawn = 1; // Minimum floor level where the room can spawn.
        public int maxFloorTospawn = 3; // Maximum floor level where the room can spawn.
        public bool hasMaxRoomsToSpawn; // Indicates if there's a hard limit on the number of rooms to spawn.
        public int currentNumberOfRooms; // Tracks the current count of rooms of this type.
        public int currentFloor; // Tracks the current floor being processed.

        /// <summary>
        /// Determines if the room can spawn based on its constraints and probabilities.
        /// </summary>
        /// <param name="x">X-coordinate of the position.</param>
        /// <param name="y">Y-coordinate of the position.</param>
        /// <param name="z">Floor level.</param>
        /// <returns>
        /// 2 = High probability (mandatory room),
        /// 1 = Low probability,
        /// 0 = Cannot spawn.
        /// </returns>
        /// 
        public int ProbabilityOfSpawning(int x, int y, int z)
        {
            // Reset room count when moving to a new floor unless limited by total max rooms.
            if (currentFloor != z && !hasMaxRoomsToSpawn)
            {
                currentFloor = z;
                currentNumberOfRooms = 0;
            }

            // Check if maximum room limits are reached.
            if ((hasMaxRoomsToSpawn && currentNumberOfRooms >= totalMaxRooms) ||
               (!hasMaxRoomsToSpawn && currentNumberOfRooms >= maxRoomsPerFloor)) return 0;

            // Ensure room is within allowed floor range.
            if (z < minFloorTospawn - 1 || z > maxFloorTospawn - 1) return 0;

            // Check if room is within allowed grid positions.
            if (!(x >= minPosition.x && x <= maxPosition.x &&
                  y >= minPosition.y && y <= maxPosition.y &&
                  z >= minPosition.z && z <= maxPosition.z)) return 0;

            // Mandatory rooms have a higher spawn priority.
            return obligatory ? 2 : 1;
        }
    }

    // Configuration for dungeon generation.
    [Header("Dungeon Settings")]
    [SerializeField, Tooltip("Seed for random number generation. Set to 0 for a random seed.")]
    public int seed;

    [SerializeField, Tooltip("Random seed generated when the seed is 0.")]
    private int randomSeed;

    [SerializeField, Tooltip("Size of the dungeon grid in terms of X and Y dimensions.")]
    public Vector2Int size;

    [SerializeField, Tooltip("Index for the starting position in the grid.")]
    private int startPos = 0;

    [SerializeField, Tooltip("Offset for placing rooms relative to the grid.")]
    public Vector3 offset;

    [Header("Stair Configuration")]
    [Tooltip("Prefab for the stair room connecting floors.")]
    public GameObject stairRoom;

    [SerializeField, Tooltip("Maximum number of stairs allowed per floor.")]
    private int maxNumberOfStairsPerFloor = 3;

    [SerializeField, Tooltip("Minimum number of stairs allowed per floor.")]
    private int minNumberOfStairsPerFloor = 3;

    [SerializeField, Tooltip("Randomize the maximum number of stairs per floor.")]
    private bool randomMaxStairsPerFloor = false;

    [SerializeField, Tooltip("Allow stairs to spawn in empty cells.")]
    private bool canPlaceStairInEmptyCells = true;

    [Tooltip("Probability of stairs spawning in each cell.")]
    [Range(0f, 1f)]
    public float stairSpawnChance;

    [Header("Room Configuration")]
    [SerializeField, Tooltip("Minimum number of floors in the dungeon.")]
    public int minFloors = 1;

    [SerializeField, Tooltip("Maximum number of floors in the dungeon.")]
    public int maxFloors = 3;

    [SerializeField, Tooltip("Rules defining room placement constraints.")]
    private Rule[] rooms;

    private List<Cell> board; // Tracks cells on the current floor.
    private Dictionary<int, List<Cell>> bigBoard = new Dictionary<int, List<Cell>>(); // Tracks cells across all floors.

    [SerializeField, Tooltip("Mob spawner for dungeon enemies.")]
    private DungeonMobSpawner mobSpawner;

    [SerializeField, Tooltip("Player's spawn position in the dungeon.")]
    private Vector3 spawnPosition;

    private bool firstVisited = false; // Tracks if the first room has been visited.

    // Property for accessing and updating spawn position.
    public Vector3 SpawnPosition { get => spawnPosition; set => spawnPosition = value; }

    // Event triggered when dungeon generation is complete.
    public event Action OnDungeonGenerated;


    /// <summary>
    /// Initializes the dungeon generation process and sets up the random seed.
    /// </summary>
    void Start()
{
    // Ensure the mob spawner is assigned
    mobSpawner ??= GetComponent<DungeonMobSpawner>();

    // Initialize the random seed
    randomSeed = (seed != 0) ? seed : System.DateTime.Now.Millisecond + UnityEngine.Random.Range(0, 100000);
    UnityEngine.Random.InitState(randomSeed);

    // Begin the dungeon generation process
    GenerateDungeon();
}

    /// <summary>
    /// Ensures necessary components are assigned.
    /// </summary>
    public void ValidateAssignments()
    {
        // Example of a validation check (currently commented out)
        // Ensures that all critical components are properly assigned
        // Assert.IsNotNull(playerStatusController, "PlayerStatusController is not assigned in playerStatusController.");
    }

    /// <summary>
    /// Retrieves all rooms in the dungeon as an array.
    /// </summary>
    /// <returns>An array of RoomBehaviour components.</returns>
    public RoomBehaviour[] GetAllRooms()
    {
        // Collects all RoomBehaviour components in the dungeon's hierarchy
        return GetComponentsInChildren<RoomBehaviour>();
    }

    /// <summary>
    /// Generates the dungeon structure, including multiple floors, stairs, and rooms.
    /// </summary>
    private void GenerateDungeon()
    {
        // Determine the number of floors for the dungeon, within the specified range.
        int numFloors = UnityEngine.Random.Range(minFloors, maxFloors + 1);
        firstVisited = false; // Tracks if the first room has been visited for spawn positioning.

        // Generate each floor in the dungeon.
        for (int floor = 0; floor < numFloors; floor++)
        {
            // Determine the maximum number of stairs for this floor.
            int maxStairs = randomMaxStairsPerFloor
                ? UnityEngine.Random.Range(minNumberOfStairsPerFloor, maxNumberOfStairsPerFloor + 1)
                : maxNumberOfStairsPerFloor;

            // Generate the maze layout for the floor.
            MazeGenerator(floor, maxStairs);
        }

        // Start spawning mobs after dungeon generation.
        mobSpawner.StartSpawningMobs();

        // Trigger any event handlers for when the dungeon is fully generated.
        OnDungeonGenerated?.Invoke();
    }

    /// <summary>
    /// Generates the maze structure for a specific floor.
    /// </summary>
    /// <param name="floor">The current floor index.</param>
    /// <param name="maxStairs">Maximum number of stairs to generate on the floor.</param>
    private void MazeGenerator(int floor, int maxStairs)
    {
        int currentCell = startPos; // Start cell for maze generation.
        board = new List<Cell>(); // Represents the cells of the current floor.
        List<Cell> floorCells = new List<Cell>(); // Temporary storage for floor cells.

        // Initialize the cells for the current floor.
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                Cell newCell = new Cell();
                board.Add(newCell);
                floorCells.Add(newCell);
            }
        }
        bigBoard[floor] = floorCells; // Store the floor cells in the dungeon structure.

        Stack<int> path = new Stack<int>(); // Tracks the path for backtracking during maze generation.
        int k = 0; // Safety counter to prevent infinite loops.

        while (k++ < 1000)
        {
            board[currentCell].visited = true; // Mark the current cell as visited.

            // If the current cell is the last one, exit the loop.
            if (currentCell == board.Count - 1) break;

            // Get a list of unvisited neighboring cells.
            List<int> neighbors = CheckNeighbors(currentCell);

            if (neighbors.Count == 0)
            {
                // If no neighbors, backtrack using the path stack.
                if (path.Count == 0) break;
                currentCell = path.Pop();
            }
            else
            {
                // Move to a random neighboring cell and connect it to the current cell.
                path.Push(currentCell);
                int newCell = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
                UpdateConnectivity(currentCell, newCell);
                currentCell = newCell;
            }
        }

        // Generate rooms and stairs on the current floor.
        GenerateRooms(floor, maxStairs);
    }

    /// <summary>
    /// Updates the connectivity between two cells by modifying their status arrays.
    /// </summary>
    /// <param name="current">The current cell index.</param>
    /// <param name="next">The next cell index to connect to.</param>
    private void UpdateConnectivity(int current, int next)
    {
        // Determine the direction of connection and update the status arrays of both cells.
        if (next > current)
        {
            if (next - 1 == current) // Right neighbor
            {
                board[current].status[2] = true;
                board[next].status[3] = true;
            }
            else // Down neighbor
            {
                board[current].status[1] = true;
                board[next].status[0] = true;
            }
        }
        else
        {
            if (next + 1 == current) // Left neighbor
            {
                board[current].status[3] = true;
                board[next].status[2] = true;
            }
            else // Up neighbor
            {
                board[current].status[0] = true;
                board[next].status[1] = true;
            }
        }
    }

    /// <summary>
    /// Generates rooms and places stairs on a specific floor.
    /// </summary>
    /// <param name="floor">The current floor index.</param>
    /// <param name="maxStairs">Maximum number of stairs to place on the floor.</param>
    private void GenerateRooms(int floor, int maxStairs)
    {
        int currentStairs = 0; // Track the number of stairs placed.

        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                Cell currentCell = board[i + j * size.x];

                if (currentCell.visited)
                {
                    // Determine the appropriate room type for the current cell.
                    int roomIndex = DetermineRoomIndex(i, j, floor);

                    if (roomIndex != -1)
                    {
                        // Attempt to place stairs if conditions are met.
                        if (HandleStairs(floor, currentStairs, maxStairs, i, j, currentCell))
                        {
                            currentStairs++;
                            continue;
                        }

                        // Instantiate a room at the current cell.
                        InstantiateRoom(i, j, floor, roomIndex, currentCell);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines the appropriate room index based on probabilities.
    /// </summary>
    /// <param name="x">The x-coordinate of the cell.</param>
    /// <param name="y">The y-coordinate of the cell.</param>
    /// <param name="floor">The current floor index.</param>
    /// <returns>The index of the room to place, or -1 if no room is selected.</returns>
    private int DetermineRoomIndex(int x, int y, int floor)
    {
        int chosenRoom = -1;
        List<int> availableRooms = new List<int>();

        // Evaluate each room's probability of spawning at the given position.
        for (int k = 0; k < rooms.Length; k++)
        {
            int probability = rooms[k].ProbabilityOfSpawning(x, y, floor);

            if (probability == 2)
            {
                // Guaranteed spawn room.
                chosenRoom = k;
                rooms[k].currentNumberOfRooms++;
                break;
            }
            else if (probability == 1)
            {
                // Possible spawn room.
                availableRooms.Add(k);
            }
        }

        // Select a random room from available options if no guaranteed room was chosen.
        if (chosenRoom == -1 && availableRooms.Count > 0)
        {
            chosenRoom = availableRooms[UnityEngine.Random.Range(0, availableRooms.Count)];
        }

        return chosenRoom;
    }

    /// <summary>
    /// Handles the placement of stairs between floors if conditions are met.
    /// </summary>
    /// <param name="floor">The current floor index.</param>
    /// <param name="currentStairs">The current count of placed stairs.</param>
    /// <param name="maxStairs">The maximum number of stairs allowed on this floor.</param>
    /// <param name="x">The x-coordinate of the cell.</param>
    /// <param name="y">The y-coordinate of the cell.</param>
    /// <param name="currentCell">The current cell object.</param>
    /// <returns>True if stairs were placed, false otherwise.</returns>
    private bool HandleStairs(int floor, int currentStairs, int maxStairs, int x, int y, Cell currentCell)
    {
        if (floor > 0 && currentStairs < maxStairs && bigBoard.TryGetValue(floor - 1, out List<Cell> cellsOnFloorBelow))
        {
            Cell cellBelow = cellsOnFloorBelow[x + y * size.x];

            if (UnityEngine.Random.value <= stairSpawnChance &&
                (cellBelow.roomType == null || canPlaceStairInEmptyCells))
            {
                // Instantiate a stair room connecting the current cell to the cell below.
                Vector3 roomPosition = new Vector3(x * offset.x, (floor - 1) * offset.y, -y * offset.z) + transform.position;
                RoomBehaviour stairRoomInstance = Instantiate(stairRoom, roomPosition, Quaternion.identity, transform).GetComponent<RoomBehaviour>();
                stairRoomInstance.UpdateRoom(cellBelow.status);
                stairRoomInstance.UpdateUpperRoom(currentCell.status);

                // Replace any existing room type in the cell below with the stair room.
                if (cellBelow.roomType)
                    Destroy(cellBelow.roomType);

                cellBelow.roomType = stairRoomInstance.gameObject;
                stairRoomInstance.name += $"Stair ({x}, {y}, {floor})";
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Instantiates a room at the specified cell.
    /// </summary>
    /// <param name="x">The x-coordinate of the cell.</param>
    /// <param name="y">The y-coordinate of the cell.</param>
    /// <param name="floor">The current floor index.</param>
    /// <param name="roomIndex">The index of the room to instantiate.</param>
    /// <param name="currentCell">The current cell object.</param>
    private void InstantiateRoom(int x, int y, int floor, int roomIndex, Cell currentCell)
    {
        // Calculate the room's position in the world space.
        Vector3 roomPosition = new Vector3(x * offset.x, floor * offset.y, -y * offset.z) + transform.position;
        RoomBehaviour newRoom = Instantiate(rooms[roomIndex].room, roomPosition, Quaternion.identity, transform).GetComponent<RoomBehaviour>();

        // Set the spawn position for the first visited room.
        if (!firstVisited)
        {
            firstVisited = true;
            spawnPosition = roomPosition + new Vector3(offset.x / 2, offset.y / 2, -offset.z / 2);
        }

        // Update the room's connectivity status and associate it with the current cell.
        newRoom.UpdateRoom(currentCell.status);
        currentCell.roomType = newRoom.gameObject;
        newRoom.name += $"Room ({x}, {y}, {floor})";
    }

    /// <summary>
    /// Checks for unvisited neighboring cells around the specified cell.
    /// </summary>
    /// <param name="cell">The index of the current cell.</param>
    /// <returns>A list of indices of unvisited neighboring cells.</returns>
    private List<int> CheckNeighbors(int cell)
    {
        List<int> neighbors = new List<int>();

        // Check the cell above.
        if (cell - size.x >= 0 && !board[cell - size.x].visited)
            neighbors.Add(cell - size.x);

        // Check the cell below.
        if (cell + size.x < board.Count && !board[cell + size.x].visited)
            neighbors.Add(cell + size.x);

        // Check the cell to the right.
        if ((cell + 1) % size.x != 0 && !board[cell + 1].visited)
            neighbors.Add(cell + 1);

        // Check the cell to the left.
        if (cell % size.x != 0 && !board[cell - 1].visited)
            neighbors.Add(cell - 1);

        return neighbors;
    }

}
