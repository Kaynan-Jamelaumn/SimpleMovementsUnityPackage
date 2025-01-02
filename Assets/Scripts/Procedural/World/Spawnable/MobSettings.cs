using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


[System.Serializable]
public class MobSettings
{
    public List<SpawnableMob> prefabs;
    public int maxNumberOfMobs;
    public bool shouldWaitToStartSpawning;
    public float waitingTime;
    public float minWaitingTime;
    public float maxWaitingTime;
    public bool shouldHaveRandomWaitingTime;
    public float retryingSpawnTime;
}

