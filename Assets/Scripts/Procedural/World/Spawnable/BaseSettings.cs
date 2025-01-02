[System.Serializable]
public abstract class BaseSettings
{
    public bool shouldWaitToStartSpawning;
    public float waitingTime;
    public float minWaitingTime;
    public float maxWaitingTime;
    public bool shouldHaveRandomWaitingTime;
    public float retryingSpawnTime;
}
