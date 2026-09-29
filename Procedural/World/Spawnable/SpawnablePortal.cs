using UnityEngine;

/// <summary>
/// One kind of world portal the <see cref="PortalSpawner"/> can place (an entry of <see cref="PortalSettings.prefabs"/>):
/// its prefab, how likely it is, where it may stand, and how soon it comes back after disappearing by itself.
/// </summary>
[System.Serializable]
public class SpawnablePortal
{
    [Header("Portal Prefab")]
    [Tooltip("REQUIRED. The portal prefab. It needs a Portal component (which sends the player into a dungeon) and a trigger Collider the player walks into. Tools > SimpleMovements > Dungeon > Create World Portal Prefab makes a ready one.")]
    public GameObject prefab;

    [Header("Selection")]
    [Tooltip("Relative chance of this type when a site gets a portal (with Use Weighted Selection). 2 = twice as likely as a type with 1.")]
    [Min(0f)] public float baseSpawnWeight = 1f;

    [Tooltip("Rarity level: higher = rarer (divides the weight; see Rarity Favor Bias). 5 = average.")]
    [Range(1, 10)] public int rarityLevel = 5;

    [Tooltip("At most this many portals of this type per region (0 = no limit).")]
    [Min(0)] public int maxInstances = 0;

    [Header("Where It May Stand")]
    [Tooltip("Only in these biomes (empty = any biome not in the Global Forbidden Biomes).")]
    public Biome[] preferredBiomes = new Biome[0];

    [Tooltip("Only between Min and Max Preferred Height (world Y).")]
    public bool limitHeight = false;

    [Tooltip("Lowest ground height for this type (world Y), with Limit Height.")]
    public float minPreferredHeight = 0f;

    [Tooltip("Highest ground height for this type (world Y), with Limit Height.")]
    public float maxPreferredHeight = 100f;

    [Header("Reappearing")]
    [Tooltip("When the portal disappears by itself (its Portal's Despawn Time runs out, or the game destroys it), seconds before it appears again at the same site. 0 = as soon as the player is far enough away.")]
    [Min(0f)] public float spawnTime = 60f;

    [Tooltip("Shortest random reappear delay (seconds), with Should Have Random Spawn Time.")]
    [Min(0f)] public float minSpawnTime = 30f;

    [Tooltip("Longest random reappear delay (seconds), with Should Have Random Spawn Time.")]
    [Min(0f)] public float maxSpawnTime = 120f;

    [Tooltip("Pick the reappear delay between Min and Max Spawn Time instead of using Spawn Time.")]
    public bool shouldHaveRandomSpawnTime;

    /// <summary>This type's relative chance (see <see cref="PortalSettings.useWeightedSelection"/>).</summary>
    public float Weight(bool weighted, float rarityBias)
    {
        if (prefab == null)
            return 0f;
        if (!weighted)
            return 1f;
        float rarity = Mathf.Pow((11f - Mathf.Clamp(rarityLevel, 1, 10)) / 10f, Mathf.Max(0.1f, rarityBias));
        return Mathf.Max(0f, baseSpawnWeight) * rarity;
    }

    /// <summary>True when this type may stand in <paramref name="biome"/>.</summary>
    public bool CanSpawnInBiome(Biome biome)
    {
        if (preferredBiomes == null || preferredBiomes.Length == 0)
            return true;
        return biome != null && System.Array.IndexOf(preferredBiomes, biome) >= 0;
    }

    /// <summary>True when this type may stand at ground height <paramref name="height"/>.</summary>
    public bool IsHeightAcceptable(float height) => !limitHeight || (height >= minPreferredHeight && height <= maxPreferredHeight);

    /// <summary>Seconds before a site reopens after this portal disappeared by itself (<paramref name="random01"/> picks the random delay).</summary>
    public float ReappearDelay(float random01)
    {
        return shouldHaveRandomSpawnTime ? Mathf.Lerp(minSpawnTime, Mathf.Max(minSpawnTime, maxSpawnTime), random01) : spawnTime;
    }

    /// <summary>Clamps values and reports configuration problems.</summary>
    public void ValidateConfiguration()
    {
        baseSpawnWeight = Mathf.Max(0f, baseSpawnWeight);
        maxInstances = Mathf.Max(0, maxInstances);
        spawnTime = Mathf.Max(0f, spawnTime);
        minSpawnTime = Mathf.Max(0f, minSpawnTime);
        maxSpawnTime = Mathf.Max(minSpawnTime, maxSpawnTime);
        if (limitHeight && minPreferredHeight > maxPreferredHeight)
        {
            float t = minPreferredHeight;
            minPreferredHeight = maxPreferredHeight;
            maxPreferredHeight = t;
        }
    }
}
