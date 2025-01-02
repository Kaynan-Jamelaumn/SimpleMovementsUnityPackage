
using System.Collections.Generic;

[System.Serializable]
public class PortalSettings
{
    public List<SpawnablePortal> prefabs;
    public int maxNumberOfPortals;
    public bool shouldWaitToStartSpawning;
    public float waitingTime;
    public float minWaitingTime;
    public float maxWaitingTime;
    public bool shouldHaveRandomWaitingTime;
    public float retryingSpawnTime;
}
