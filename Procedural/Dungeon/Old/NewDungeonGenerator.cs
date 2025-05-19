//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class NewdddDungeonGenerator : MonoBehaviour
//{
//    public class Cell
//    {
//        public bool visited = false;
//        public bool[] status = new bool[4];
//        public bool hasStair = false; // Indicates if the cell has a staircase
//    }

//    [System.Serializable]
//    public class Rule
//    {
//        public GameObject room;
//        public Vector3Int minPosition;
//        public Vector3Int maxPosition;

//        public bool obligatory;

//        public int ProbabilityOfSpawning(int x, int y, int z)
//        {
//            // 0 - cannot spawn 1 - can spawn 2 - HAS to spawn

//            if (x >= minPosition.x && x <= maxPosition.x && y >= minPosition.y && y <= maxPosition.y && z >= minPosition.z && z <= maxPosition.z)
//            {
//                return obligatory ? 2 : 1;
//            }

//            return 0;
//        }

//    }
//    public GameObject stairRoom;

//    public Vector3Int size; // Change to Vector3Int
//    public int minFloors = 1; // Minimum number of floors
//    public int maxFloors = 3; // Maximum number of floors
//    public Rule[] rooms;
//    public Vector3 offset; // Change to Vector3

//    List<Cell> board;

//    // Start is called before the first frame update
//    void Start()
//    {
//        MazeGenerator();
//    }

//    void GenerateDungeon()
//    {
//        int numFloors = Random.Range(minFloors, maxFloors + 1); // Randomize number of floors

//        for (int floor = 0; floor < numFloors; floor++)
//        {
//            for (int i = 0; i < size.x; i++)
//            {
//                for (int j = 0; j < size.y; j++)
//                {
//                    Cell currentCell = board[(i + j * size.x) + floor * (size.x * size.y)]; // Calculate the index based on floor
//                    if (currentCell.visited)
//                    {
//                        int randomRoom = -1;
//                        List<int> availableRooms = new List<int>();

//                        for (int k = 0; k < rooms.Length; k++)
//                        {
//                            int p = rooms[k].ProbabilityOfSpawning(i, j, floor);

//                            if (p == 2)
//                            {
//                                randomRoom = k;
//                                break;
//                            }
//                            else if (p == 1)
//                            {
//                                availableRooms.Add(k);
//                            }
//                        }

//                        if (randomRoom == -1)
//                        {
//                            if (availableRooms.Count > 0)
//                            {
//                                randomRoom = availableRooms[Random.Range(0, availableRooms.Count)];
//                            }
//                            else
//                            {
//                                randomRoom = 0;
//                            }
//                        }

//                        var newRoom = Instantiate(rooms[randomRoom].room, new Vector3(i * offset.x, floor * offset.y, -j * offset.z), Quaternion.identity, transform).GetComponent<RoomBehaviour>();
//                        newRoom.UpdateRoom(currentCell.status);
//                        newRoom.name += " " + i + "-" + j + "-" + floor;

//                        // Check if the cell has a staircase
//                        if (currentCell.hasStair)
//                        {
//                            // Instantiate stairRoom prefab
//                            var stair = Instantiate(stairRoom, new Vector3(i * offset.x, (floor + 1) * offset.y, -j * offset.z), Quaternion.identity, transform);
//                            // Set the staircase name
//                            stair.name = "Stair " + i + "-" + j + "-" + floor;
//                        }
//                    }
//                }
//            }
//        }
//    }

//    void MazeGenerator()
//    {
//        board = new List<Cell>();

//        for (int floor = 0; floor < maxFloors; floor++)
//        {
//            for (int i = 0; i < size.x; i++)
//            {
//                for (int j = 0; j < size.y; j++)
//                {
//                    board.Add(new Cell());
//                }
//            }
//        }

//        int currentCell = 0; // Change to start from the first cell

//        Stack<int> path = new Stack<int>();

//        int k = 0;

//        while (k < 1000)
//        {
//            k++;

//            board[currentCell].visited = true;

//            if (currentCell == board.Count - 1)
//            {
//                break;
//            }

//            //Check the cell's neighbors
//            List<int> neighbors = CheckNeighbors(currentCell);

//            if (neighbors.Count == 0)
//            {
//                if (path.Count == 0)
//                {
//                    break;
//                }
//                else
//                {
//                    currentCell = path.Pop();
//                }
//            }
//            else
//            {
//                path.Push(currentCell);

//                int newCell = neighbors[Random.Range(0, neighbors.Count)];

//                if (newCell > currentCell)
//                {
//                    //down or right
//                    if (newCell - 1 == currentCell)
//                    {
//                        board[currentCell].status[2] = true;
//                        currentCell = newCell;
//                        board[currentCell].status[3] = true;
//                    }
//                    else
//                    {
//                        board[currentCell].status[1] = true;
//                        currentCell = newCell;
//                        board[currentCell].status[0] = true;
//                    }
//                }
//                else
//                {
//                    //up or left
//                    if (newCell + 1 == currentCell)
//                    {
//                        board[currentCell].status[3] = true;
//                        currentCell = newCell;
//                        board[currentCell].status[2] = true;
//                    }
//                    else
//                    {
//                        board[currentCell].status[0] = true;
//                        currentCell = newCell;
//                        board[currentCell].status[1] = true;
//                    }
//                }

//                // Check if we need to place a staircase
//                if (Random.Range(0f, 1f) < 0.1f) // Adjust this probability as needed
//                {
//                    board[newCell].hasStair = true;
//                }
//            }

//        }
//        GenerateDungeon();
//    }

//    List<int> CheckNeighbors(int cell)
//    {
//        List<int> neighbors = new List<int>();

//        //check up neighbor
//        if (cell - size.x >= 0 && !board[(cell - size.x)].visited)
//        {
//            neighbors.Add((cell - size.x));
//        }

//        //check down neighbor
//        if (cell + size.x < board.Count && !board[(cell + size.x)].visited)
//        {
//            neighbors.Add((cell + size.x));
//        }

//        //check right neighbor
//        if ((cell + 1) % size.x != 0 && !board[(cell + 1)].visited)
//        {
//            neighbors.Add((cell + 1));
//        }

//        //check left neighbor
//        if (cell % size.x != 0 && !board[(cell - 1)].visited)
//        {
//            neighbors.Add((cell - 1));
//        }

//        return neighbors;
//    }
//}
