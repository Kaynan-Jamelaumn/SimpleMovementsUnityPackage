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

    [Tooltip("In which zones of the world this portal may appear (the Hub World's rings around the city): e.g. a beginner " +
             "dungeon only in the Safe Zone, a raid only in the Dangerous Zone and beyond. Off = any zone.")]
    public ZoneFilter zones = new ZoneFilter();

    [Header("Time and Weather")]
    [Tooltip("When this portal may be out: only by day or at night, between some hours, only in some weathers (a storm " +
             "dungeon that opens in thunderstorms) or never in some. Default: any time, any weather. Despawn When Unmet " +
             "closes it again when the rule ends (it comes back when it is true again).")]
    public SpawnConditions conditions = new SpawnConditions();

    [Header("After Use (this type)")]
    [Tooltip("Use this type's own After Use rule instead of the Portal Settings' one. (A Portal component whose own " +
             "After Use is not 'Use Spawner Rule' wins over both.)")]
    public bool overrideAfterUse;

    [Tooltip("With Override After Use. Stays Open: the portal stays. Close After Entering: it closes once its dungeon can " +
             "take nobody else - right after the first group for a private dungeon, when it is full (or over) for a shared " +
             "or linked one. Close After Completing: it closes when its dungeon is completed.")]
    public PortalUseRule closeAfterUse = PortalUseRule.StaysOpen;

    [Tooltip("With Override After Use: seconds before a closed portal comes back (0 = never, for this session).")]
    [Min(0f)] public float reopenAfterUse;

    [Header("Players (this type)")]
    [Tooltip("Most players in this portal's dungeon (0 = the prefab Portal's Max Players). 1 = solo only.")]
    [Min(0)] public int maxPlayers;

    [Tooltip("Who shares a dungeon. Use Portal Setting: the prefab Portal decides. Separate Runs: every group that enters " +
             "gets its own dungeon. Shared Run: everyone who enters while its dungeon is going joins it (up to Max Players).")]
    public PortalSharingOverride sharing = PortalSharingOverride.UsePortalSetting;

    [Header("Linked Portals (two or more entrances to ONE dungeon)")]
    [Tooltip("How many portals this type puts in the world for one dungeon. 1 = a normal portal. 2 or more = linked " +
             "portals some distance apart that lead into the SAME dungeon: whoever enters any of them while it is going " +
             "joins it (up to Max Players) - teams that cooperate, or fight each other where PvP is on.")]
    [Range(1, 4)] public int linkedPortals = 1;

    [Tooltip("Distance between linked portals (world units). They stay inside the chunk of their site: with Extra Large " +
             "chunks (240 units) keep it under about 120.")]
    [Min(4f)] public float linkedSpacing = 60f;

    [Tooltip("Where the players of each linked portal arrive. Same Entrance: everyone at the dungeon's entrance. Separate " +
             "Entrances: each linked portal has its own arrival point on the first floor, as far apart as the dungeon allows.")]
    public LinkedArrival linkedArrival = LinkedArrival.SeparateEntrances;

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
        reopenAfterUse = Mathf.Max(0f, reopenAfterUse);
        maxPlayers = Mathf.Max(0, maxPlayers);
        linkedPortals = Mathf.Clamp(linkedPortals, 1, 4);
        linkedSpacing = Mathf.Max(4f, linkedSpacing);
        zones?.Validate();
        conditions?.Validate();
        if (limitHeight && minPreferredHeight > maxPreferredHeight)
        {
            float t = minPreferredHeight;
            minPreferredHeight = maxPreferredHeight;
            maxPreferredHeight = t;
        }
    }
}
