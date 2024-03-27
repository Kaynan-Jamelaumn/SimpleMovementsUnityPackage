
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


public class DungeonGenerator : MonoBehaviour
{

    public class Cell
    {
        public bool visited = false;
        public bool[] status = new bool[4];
        public GameObject roomType;
    }

    [System.Serializable]
    public class Rule
    {
        public GameObject room;
        public Vector3Int minPosition;
        public Vector3Int maxPosition;

        public bool obligatory;

        public int maxRoomsPerFloor;
        public int totalMaxRooms;

        public int minFloorTospawn = 1; // Minimum 
        public int maxFloorTospawn = 3; // Maximum 

        public bool hasMaxRoomsToSpawn;

        public int currentNumberOfRooms;
        public int currentFloor;
        public int ProbabilityOfSpawning(int x, int y, int z)
        {
            if (currentFloor != z && !hasMaxRoomsToSpawn)
            {
                currentFloor = z;
                currentNumberOfRooms = 0;
            }

            // Check if the number of rooms spawned on this floor exceeds the limit
            if (hasMaxRoomsToSpawn && currentNumberOfRooms >= totalMaxRooms)
                return 0;
            else if (hasMaxRoomsToSpawn == false && currentNumberOfRooms >= maxRoomsPerFloor)
                return 0;

            // Check if the floor is within the valid range
            if (z < minFloorTospawn - 1 || z > maxFloorTospawn - 1)
                return 0;

            // Check if the position is within the valid range
            if (!(x >= minPosition.x && x <= maxPosition.x && y >= minPosition.y && y <= maxPosition.y && z >= minPosition.z && z <= maxPosition.z))
                return 0;
            return obligatory ? 2 : 1;
        }
    }
    [SerializeField] private int seed;
    [SerializeField] private int randomSeed;
    [SerializeField] private Vector2Int size;
    [SerializeField] private int startPos = 0;
    [SerializeField] private Vector3 offset;
    [Header("Stair Config")]
    [Tooltip("The StairRoomPrefab( a room that will connect differents floors trough a stair)")] public GameObject stairRoom;
    [SerializeField] private int maxNumberOfStairsPerFloor = 3; // Maximum number of floors
    [SerializeField] private int minNumberOfStairsPerFloor = 3; // Maximum number of floors
    [SerializeField] private bool randomMaxStairsPerFloor = false;
    [SerializeField] private bool canPlaceStairInEmptyCells = true;
    [Tooltip("What is the chance for the stair to be spawned in every cell")][Range(0f, 1f)] public float stairSpawnChance;

    [Header("Rooms Config")]
    [SerializeField] private int minFloors = 1; // Minimum number of floors
    [SerializeField] private int maxFloors = 3; // Maximum number of floors
    [SerializeField] private Rule[] rooms;
    private List<Cell> board;
    private Dictionary<int, List<Cell>> bigBoard = new Dictionary<int, List<Cell>>();
    [SerializeField] private DungeonMobSpawner mobSpawner;
    [SerializeField] private Vector3 spawnPosition;
    bool firstVisited = false;
    public Vector3 SpawnPosition { get => spawnPosition; set => spawnPosition = value; }
    // Start is called before the first frame update
    void Start()
    {

        if (!mobSpawner) mobSpawner = GetComponent<DungeonMobSpawner>();
        if (seed != 0) Random.InitState(seed);
        else
        {
            randomSeed = System.DateTime.Now.Millisecond + Random.Range(0, 100000);
            Random.InitState(randomSeed);

        }
        GenerateDungeon();
    }
    private void ValidateAssignments()
    {
       // Assert.IsNotNull(playerStatusController, "PlayerStatusController is not assigned in playerStatusController.");
    }
    public RoomBehaviour[] GetAllRooms()
    {
        return GetComponentsInChildren<RoomBehaviour>();
    }

    void GenerateDungeon()
    {
        int numFloors = Random.Range(minFloors, maxFloors + 1); // Randomize number of floors
        int maxStairs = Random.Range(minNumberOfStairsPerFloor, maxNumberOfStairsPerFloor + 1);
        for (int floor = 0; floor < numFloors; floor++)
        {
            if (randomMaxStairsPerFloor) maxStairs = Random.Range(minNumberOfStairsPerFloor, maxNumberOfStairsPerFloor + 1);
            MazeGenerator(floor, maxStairs);
        }
        mobSpawner.StartSpawningMobs();

    }

    void MazeGenerator(int floor, int maxStairs)
    {
        int currentCell = startPos;
        board = new List<Cell>();
        List<Cell> floorCells = new List<Cell>();

        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                Cell newCell = new Cell();
                board.Add(newCell);
                floorCells.Add(newCell);
            }
        }
        bigBoard.Add(floor, floorCells);

        Stack<int> path = new Stack<int>();

        int k = 0;

        while (k < 1000)
        {
            k++;

            board[currentCell].visited = true;

            if (currentCell == board.Count - 1)
            {
                break;
            }

            //Check the cell's neighbors
            List<int> neighbors = CheckNeighbors(currentCell);

            if (neighbors.Count == 0)
            {
                if (path.Count == 0)
                {
                    break;
                }
                else
                {
                    currentCell = path.Pop();
                }
            }
            else
            {
                path.Push(currentCell);

                int newCell = neighbors[Random.Range(0, neighbors.Count)];

                if (newCell > currentCell)
                {
                    //down or right
                    if (newCell - 1 == currentCell)
                    {
                        board[currentCell].status[2] = true;
                        currentCell = newCell;
                        board[currentCell].status[3] = true;
                    }
                    else
                    {
                        board[currentCell].status[1] = true;
                        currentCell = newCell;
                        board[currentCell].status[0] = true;
                    }
                }
                else
                {
                    //up or left
                    if (newCell + 1 == currentCell)
                    {
                        board[currentCell].status[3] = true;
                        currentCell = newCell;
                        board[currentCell].status[2] = true;
                    }
                    else
                    {
                        board[currentCell].status[0] = true;
                        currentCell = newCell;
                        board[currentCell].status[1] = true;
                    }
                }

            }

        }
        GenerateRooms(floor, maxStairs);
    }

    void GenerateRooms(int floor, int maxStairs)
    {
        int currentStairs = 0;
        for (int i = 0; i < size.x; i++)
        {
            for (int j = 0; j < size.y; j++)
            {
                Cell currentCell = board[i + j * size.x];

                if (currentCell.visited)
                {
                    int randomRoom = -1;
                    List<int> availableRooms = new List<int>();

                    for (int k = 0; k < rooms.Length; k++)
                    {
                        int p = rooms[k].ProbabilityOfSpawning(i, j, floor);

                        if (p == 2)
                        {
                            randomRoom = k;
                            rooms[k].currentNumberOfRooms++;
                            break;
                        }
                        else if (p == 1)
                        {
                            availableRooms.Add(k);
                        }
                    }

                    if (randomRoom == -1 && availableRooms.Count > 0)
                    {
                        randomRoom = availableRooms[Random.Range(0, availableRooms.Count)];
                    }
                    // else randomRoom = 0;
                    //Cell cellBelow = board[(i + j * size.x) + (floor - 1) * (size.x * size.y)];
                    if (randomRoom != -1)
                    {
                        if (floor > 0 && currentStairs != maxStairs)
                        {
                            if (bigBoard.ContainsKey(floor - 1))
                            {

                                List<Cell> cellsOnFloorBelow = bigBoard[floor - 1];
                                Cell cellBelow = cellsOnFloorBelow[i + j * size.x];
                                if (Random.value <= stairSpawnChance && (cellBelow.roomType && cellBelow.roomType != stairRoom || canPlaceStairInEmptyCells))
                                {
                                    RoomBehaviour belowNewRoom = Instantiate(stairRoom, new Vector3(i * offset.x, (floor - 1) * offset.y, -j * offset.z), Quaternion.identity, transform).GetComponent<RoomBehaviour>();
                                    belowNewRoom.UpdateRoom(cellBelow.status);
                                    belowNewRoom.UpdateUpperRoom(currentCell.status);

                                    if (cellBelow.roomType) Destroy(cellBelow.roomType.gameObject);

                                    cellBelow.roomType = belowNewRoom.gameObject;
                                    belowNewRoom.name += " " + i + "-" + j + "-" + floor + "-";
                                    currentStairs++;

                                    continue;

                                }
                            }
                        }
                        InstantiateNewRandomRoom(i, j, floor, randomRoom, currentCell);
                    }
                }
            }
        }
    }

    void InstantiateNewRandomRoom(int i, int j, int floor, int randomRoom, Cell currentCell)
    {
        RoomBehaviour newRoom = Instantiate(rooms[randomRoom].room, new Vector3(i * offset.x, floor * offset.y, -j * offset.z), Quaternion.identity, transform).GetComponent<RoomBehaviour>();
        newRoom.UpdateRoom(currentCell.status);
        //Debug.Log(newRoom.transform.position);
        currentCell.roomType = newRoom.gameObject;
        newRoom.name += " " + i + "-" + j + "-" + floor; if (floor == 0 && currentCell == board[startPos])
        if (firstVisited == false)// currentCell == board[startPos])
        {
                spawnPosition = newRoom.transform.position;

                 firstVisited = true;
                Debug.Log(newRoom.transform.position);
            }

    }

    List<int> CheckNeighbors(int cell)
    {
        List<int> neighbors = new List<int>();

        //check up neighbor
        if (cell - size.x >= 0 && !board[(cell - size.x)].visited)
        {
            neighbors.Add((cell - size.x));
        }

        //check down neighbor
        if (cell + size.x < board.Count && !board[(cell + size.x)].visited)
        {
            neighbors.Add((cell + size.x));
        }


        //check right neighbor
        if ((cell + 1) % size.x != 0 && !board[(cell + 1)].visited)
        {
            neighbors.Add((cell + 1));
        }

        //check left neighbor
        if (cell % size.x != 0 && !board[(cell - 1)].visited)
        {
            neighbors.Add((cell - 1));
        }

        return neighbors;
    }
}
