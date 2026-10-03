using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Random Spawning Items", menuName = "SimpleMovements/Items/Random Item Spawns (Storage)", order = 22)]
public class RandomSpawningItems : ScriptableObject
{
    public List<ItemSO> itemsToSpawn = new List<ItemSO>();
    public int minCount;
    public int maxCount;
}
