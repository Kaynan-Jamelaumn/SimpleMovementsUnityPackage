
//using System.Collections.Generic;
//using UnityEngine;

//public class OldOldDungeonGenerator : MonoBehaviour
//{
//    public class Cell
//    {
//        public bool visited = false;
//        public bool[] status = new bool[4];
//    }

//    [System.Serializable]
//    public class Rule
//    {
//        public GameObject room;
//        public Vector3Int minPosition;
//        public Vector3Int maxPosition;

//        public bool obligatory;
//        public int maxRoomsPerFloor;
//        public int minFloorTospawn = 1; // Minimum 
//        public int maxFloorTospawn = 3; // Maximum 
//        public int weightToSpawn; // to help choose the rooms to spawn
//        public bool hasMaxRoomsToSpawn;
//        public int totalMaxRooms;
//        public int currentNumberOfRooms;
//        public int currentFloor;



//        public int ProbabilityOfSpawning(int x, int y, int z)
//        {
//            if (currentFloor != z && !hasMaxRoomsToSpawn)
//            {
//                currentFloor = z;
//                currentNumberOfRooms = 0;
//            }

//            // Check if the number of rooms spawned on this floor exceeds the limit
//            if (hasMaxRoomsToSpawn && currentNumberOfRooms >= totalMaxRooms)
//                return 0;
//            else if (hasMaxRoomsToSpawn == false && currentNumberOfRooms >= maxRoomsPerFloor)
//                return 0;


//            // Check if the floor is within the valid range
//            if (z < minFloorTospawn - 1 || z > maxFloorTospawn - 1)
//                return 0;

//            // Check if the position is within the valid range
//            if (!(x >= minPosition.x && x <= maxPosition.x && y >= minPosition.y && y <= maxPosition.y && z >= minPosition.z && z <= maxPosition.z))
//                return 0;
//            return obligatory ? 2 : 1;
//        }

//    }
//    public GameObject stairRoom;

//    public Vector3Int size; // Change to Vector3Int
//    public int minFloors = 1; // Minimum number of floors
//    public int maxFloors = 3; // Maximum number of floors
//    int numFloors = 0;
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
//                                rooms[k].currentNumberOfRooms++;
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
//                                rooms[randomRoom].currentNumberOfRooms++;
//                            }

//                        }

//                        if (randomRoom != -1)
//                        {                           
//                            //if (counts[randomRoom].currentNumber < counts[randomRoom].maxRoomsPerFloor && floor+1 >= counts[randomRoom].minFloorTospawn && floor+1 <= counts[randomRoom].maxFloorTospawn)
//                                RoomBehaviour newRoom = Instantiate(rooms[randomRoom].room, new Vector3(i * offset.x, floor * offset.y, -j * offset.z), Quaternion.identity, transform).GetComponent<RoomBehaviour>();
//                                newRoom.UpdateRoom(currentCell.status);
//                                newRoom.name += " " + i + "-" + j + "-" + floor;
//                        }
//                    }
//                }
//            }
//        }
//    }
  
//    void MazeGenerator()
//    {
//        board = new List<Cell>();
//        numFloors = Random.Range(minFloors, maxFloors + 1); // Randomize number of floors
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

//        while (k < 400)
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














////void GenerateDungeon()
////{
////    int numFloors = Random.Range(minFloors, maxFloors + 1); // Randomize number of floors
////    for (int floor = 0; floor < numFloors; floor++)
////    {
////        for (int i = 0; i < size.x; i++)
////        {
////            for (int j = 0; j < size.y; j++)
////            {
////                Cell currentCell = board[(i + j * size.x) + floor * (size.x * size.y)]; // Calculate the index based on floor
////                if (currentCell.visited)
////                {
////                    int randomRoom = -1;
////                    List<int> availableRooms = new List<int>();

////                    for (int k = 0; k < rooms.Length; k++)
////                    {
////                        int p = rooms[k].ProbabilityOfSpawning(i, j, floor);
////                        if (p == 2)
////                        {
////                            randomRoom = k;
////                            break;
////                        }
////                        else if (p == 1)
////                        {
////                            availableRooms.Add(k);
////                        }
////                    }

////                    if (randomRoom == -1)
////                    {
////                        if (availableRooms.Count > 0)
////                        {
////                            randomRoom = availableRooms[Random.Range(0, availableRooms.Count)];
////                        }
////                        else
////                        {
////                            randomRoom = 0;
////                        }
////                    }

////                    var newRoom = Instantiate(rooms[randomRoom].room, new Vector3(i * offset.x, floor * offset.y, -j * offset.z), Quaternion.identity, transform).GetComponent<RoomBehaviour>();
////                    newRoom.UpdateRoom(currentCell.status);
////                    newRoom.name += " " + i + "-" + j + "-" + floor;

////                }
////            }
////        }
////    }
////}





////void GenerateDungeon()
////{
////    int numFloors = Random.Range(minFloors, maxFloors + 1); // Randomize number of floors
////    for (int floor = 0; floor < numFloors; floor++)
////    {
////        foreach (var room in rooms) counts.Add(new CountHolder(room.maxRoomsPerFloor, room.minFloorTospawn, room.maxFloorTospawn));
////        int currentNumberOfRoomsOnFloor = 0; // Track the number of rooms generated on this floor
////        for (int i = 0; i < size.x; i++)
////        {
////            for (int j = 0; j < size.y; j++)
////            {
////                Cell currentCell = board[(i + j * size.x) + floor * (size.x * size.y)]; // Calculate the index based on floor
////                if (currentCell.visited)
////                {
////                    int randomRoom = -1;
////                    List<int> availableRooms = new List<int>();

////                    for (int k = 0; k < rooms.Length; k++)
////                    {
////                        int p = rooms[k].ProbabilityOfSpawning(i, j, floor);
////                        if (p == 2)
////                        {
////                            randomRoom = k;
////                            rooms[k].currentNumberOfRooms++;
////                            break;
////                        }
////                        else if (p == 1)
////                        {
////                            rooms[k].currentNumberOfRooms++;
////                            availableRooms.Add(k);
////                        }
////                    }

////                    if (randomRoom == -1)
////                    {
////                        if (availableRooms.Count > 0)
////                        {
////                            randomRoom = availableRooms[Random.Range(0, availableRooms.Count)];
////                        }
////                        else
////                        {
////                            randomRoom = 0;
////                        }
////                    }

////                    if (randomRoom != -1)
////                    {
////                        Debug.Log(randomRoom);
////                        // Check if the current number of rooms on this floor exceeds the limit
////                        if (rooms[randomRoom].currentNumberOfRooms < rooms[randomRoom].maxRoomsPerFloor && floor + 1 >= rooms[randomRoom].minFloorTospawn && floor + 1 <= rooms[randomRoom].maxFloorTospawn)
////                        // if (currentNumberOfRoomsOnFloor < rooms[randomRoom].maxRoomsPerFloor)
////                        {
////                            Debug.Log(rooms[randomRoom].currentNumberOfRooms + " " + randomRoom);
////                            var newRoom = Instantiate(rooms[randomRoom].room, new Vector3(i * offset.x, floor * offset.y, -j * offset.z), Quaternion.identity, transform).GetComponent<RoomBehaviour>();
////                            newRoom.UpdateRoom(currentCell.status);
////                            newRoom.name += " " + i + "-" + j + "-" + floor;
////                            currentNumberOfRoomsOnFloor++; // Increment the count of rooms generated on this floor
////                        }
////                    }
////                }
////            }
////        }
////    }
////}









////void GxenerateDungeon()
////{
////    int numFloors = Random.Range(minFloors, maxFloors + 1); // Randomize number of floors

////    for (int floor = 0; floor < numFloors; floor++)
////    {
////        for (int i = 0; i < size.x; i++)
////        {
////            for (int j = 0; j < size.y; j++)
////            {
////                // Itera sobre as regras para verificar se a sala deve ser gerada nesta posição

////                foreach (Rule rule in rooms)
////                {
////                    int p = rule.ProbabilityOfSpawning(i, j, floor);

////                    // Se a probabilidade de spawn for maior que 0, gera a sala
////                    if (p > 0)
////                    {
////                        Cell currentCell = board[(i + j * size.x) + floor * (size.x * size.y)];
////                        var newRoom = Instantiate(rule.room, new Vector3(i * offset.x, floor * offset.y, -j * offset.z), Quaternion.identity, transform).GetComponent<RoomBehaviour>();
////                        newRoom.UpdateRoom(currentCell.status);
////                        newRoom.name += " " + i + "-" + j + "-" + floor;
////                        break; // Sai do loop de regras depois de gerar uma sala
////                    }
////                }
////            }
////        }
////    }
////}