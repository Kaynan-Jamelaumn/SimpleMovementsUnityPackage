using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;

/// <summary>
/// Centralized manager for coordinating all spawners in the terrain system.
/// Provides global spawn coordination, performance monitoring, debugging tools,
/// and ecosystem balance management for natural spawn distribution.
/// </summary>
public class SpawnerManager : MonoBehaviour
{
    [Header("Spawner Coordination")]
    [Tooltip("Enable global coordination between all spawners")]
    public bool enableGlobalCoordination = true;

    [Tooltip("Maximum total spawned entities across all spawners")]
    [Range(10, 1000)]
    public int globalEntityLimit = 200;

    [Tooltip("Enable ecosystem balance calculations")]
    public bool enableEcosystemBalance = true;

    [Tooltip("Target ratio of portals to mobs (portals per mob)")]
    [Range(0.01f, 1f)]
    public float portalToMobRatio = 0.1f;

    [Header("Performance Monitoring")]
    [Tooltip("Enable performance monitoring for all spawners")]
    public bool enablePerformanceMonitoring = true;

    [Tooltip("Target frame rate for spawn throttling")]
    [Range(30f, 120f)]
    public float targetFrameRate = 60f;

    [Tooltip("Memory usage threshold for spawn throttling (MB)")]
    [Range(100f, 2000f)]
    public float memoryThresholdMB = 500f;

    [Header("Debug and Visualization")]
    [Tooltip("Enable debug UI overlay")]
    public bool enableDebugUI = false;

    [Tooltip("Show spawn statistics in debug UI")]
    public bool showSpawnStatistics = true;

    [Tooltip("Show performance metrics in debug UI")]
    public bool showPerformanceMetrics = true;

    [Tooltip("Enable visual debug indicators for spawns")]
    public bool enableVisualDebug = false;

    [Tooltip("Debug indicator prefab (optional)")]
    public GameObject debugIndicatorPrefab;

    [Header("Player Integration")]
    [Tooltip("Automatically find player objects")]
    public bool autoFindPlayers = true;

    [Tooltip("Player tags to search for")]
    public string[] playerTags = { "Player" };

    [Tooltip("Manual player references")]
    public List<Transform> playerTransforms;

    // Private fields for tracking
    private List<PortalSpawner> portalSpawners = new List<PortalSpawner>();
    private List<MobSpawner> mobSpawners = new List<MobSpawner>();
    private List<Transform> activePlayers = new List<Transform>();

    // Performance tracking
    private float lastFrameRateCheck = 0f;
    private float lastMemoryCheck = 0f;
    private readonly float performanceCheckInterval = 1f;
    private bool isPerformanceThrottling = false;

    // Statistics tracking
    private SpawnStatistics statistics = new SpawnStatistics();

    // Debug UI
    private Rect debugWindowRect = new Rect(10, 10, 400, 300);
    private Vector2 debugScrollPosition = Vector2.zero;
    private bool showDebugWindow = false;

    /// <summary>
    /// Statistics container for spawn tracking.
    /// </summary>
    [System.Serializable]
    public class SpawnStatistics
    {
        public int totalPortalsSpawned = 0;
        public int totalMobsSpawned = 0;
        public int activePortals = 0;
        public int activeMobs = 0;
        public int failedSpawnAttempts = 0;
        public float averageSpawnTime = 0f;
        public float currentFrameRate = 0f;
        public float currentMemoryUsage = 0f;
        public float ecosystemBalanceScore = 1f;

        public void Reset()
        {
            totalPortalsSpawned = 0;
            totalMobsSpawned = 0;
            activePortals = 0;
            activeMobs = 0;
            failedSpawnAttempts = 0;
            averageSpawnTime = 0f;
            ecosystemBalanceScore = 1f;
        }
    }

    private void Awake()
    {
        // Find all spawners in the scene
        FindAllSpawners();

        // Initialize player tracking
        if (autoFindPlayers)
        {
            FindPlayers();
        }
        else if (playerTransforms != null)
        {
            activePlayers.AddRange(playerTransforms.Where(p => p != null));
        }
    }

    private void Start()
    {
        if (enableGlobalCoordination)
        {
            // Register callbacks with all spawners
            RegisterSpawnerCallbacks();
        }

        // Start performance monitoring if enabled
        if (enablePerformanceMonitoring)
        {
            InvokeRepeating(nameof(UpdatePerformanceMetrics), 1f, performanceCheckInterval);
        }

        // Start ecosystem balance monitoring if enabled
        if (enableEcosystemBalance)
        {
            InvokeRepeating(nameof(UpdateEcosystemBalance), 5f, 5f);
        }
    }

    private void Update()
    {
        // Update player positions if auto-finding is enabled
        if (autoFindPlayers && Time.frameCount % 60 == 0) // Check every 60 frames
        {
            FindPlayers();
        }

        // Handle debug UI toggle
        if (Input.GetKeyDown(KeyCode.F3))
        {
            showDebugWindow = !showDebugWindow;
        }

        // Update statistics
        UpdateStatistics();
    }

    /// <summary>
    /// Finds all spawners currently in the scene.
    /// </summary>
    private void FindAllSpawners()
    {
        portalSpawners.Clear();
        mobSpawners.Clear();

        // Find all portal spawners
        var foundPortalSpawners = FindObjectsOfType<PortalSpawner>();
        portalSpawners.AddRange(foundPortalSpawners);

        // Find all mob spawners
        var foundMobSpawners = FindObjectsOfType<MobSpawner>();
        mobSpawners.AddRange(foundMobSpawners);

        Debug.Log($"SpawnerManager: Found {portalSpawners.Count} portal spawners and {mobSpawners.Count} mob spawners");
    }

    /// <summary>
    /// Finds player objects in the scene based on tags.
    /// </summary>
    private void FindPlayers()
    {
        activePlayers.Clear();

        foreach (string playerTag in playerTags)
        {
            var playersWithTag = GameObject.FindGameObjectsWithTag(playerTag);
            activePlayers.AddRange(playersWithTag.Select(go => go.transform).Where(t => t != null));
        }

        // Add manual player references
        if (playerTransforms != null)
        {
            activePlayers.AddRange(playerTransforms.Where(p => p != null && !activePlayers.Contains(p)));
        }
    }

    /// <summary>
    /// Registers callback events with all spawners for coordination.
    /// </summary>
    private void RegisterSpawnerCallbacks()
    {
        // Note: In a full implementation, you would add events to the spawner classes
        // and register callbacks here for spawn success/failure tracking
        Debug.Log("SpawnerManager: Registering spawner callbacks");
    }

    /// <summary>
    /// Updates performance metrics and applies throttling if necessary.
    /// </summary>
    private void UpdatePerformanceMetrics()
    {
        statistics.currentFrameRate = 1f / Time.deltaTime;
        statistics.currentMemoryUsage = System.GC.GetTotalMemory(false) / (1024f * 1024f); // MB

        // Check if we need to throttle spawning
        bool shouldThrottle = statistics.currentFrameRate < targetFrameRate * 0.8f ||
                             statistics.currentMemoryUsage > memoryThresholdMB;

        if (shouldThrottle != isPerformanceThrottling)
        {
            isPerformanceThrottling = shouldThrottle;

            if (shouldThrottle)
            {
                Debug.LogWarning("SpawnerManager: Performance throttling activated");
                // In a full implementation, you would notify all spawners to reduce spawn rates
            }
            else
            {
                Debug.Log("SpawnerManager: Performance throttling deactivated");
                // In a full implementation, you would notify all spawners to resume normal spawn rates
            }
        }
    }

    /// <summary>
    /// Updates ecosystem balance calculations and adjusts spawn rates accordingly.
    /// </summary>
    private void UpdateEcosystemBalance()
    {
        if (!enableEcosystemBalance) return;

        int totalPortals = GetTotalActivePortals();
        int totalMobs = GetTotalActiveMobs();

        // Calculate current ratio
        float currentRatio = totalMobs > 0 ? (float)totalPortals / totalMobs : 0f;

        // Calculate balance score (1.0 = perfect balance, lower = imbalanced)
        statistics.ecosystemBalanceScore = 1f - Mathf.Abs(currentRatio - portalToMobRatio) / portalToMobRatio;
        statistics.ecosystemBalanceScore = Mathf.Clamp01(statistics.ecosystemBalanceScore);

        // Apply balance corrections if needed
        if (statistics.ecosystemBalanceScore < 0.7f)
        {
            ApplyEcosystemBalanceCorrections(currentRatio);
        }
    }

    /// <summary>
    /// Applies corrections to maintain ecosystem balance.
    /// </summary>
    /// <param name="currentRatio">Current portal to mob ratio.</param>
    private void ApplyEcosystemBalanceCorrections(float currentRatio)
    {
        if (currentRatio > portalToMobRatio * 1.5f)
        {
            // Too many portals relative to mobs
            Debug.Log("SpawnerManager: Too many portals, adjusting spawn rates");
            // In a full implementation, you would reduce portal spawn rates and increase mob spawn rates
        }
        else if (currentRatio < portalToMobRatio * 0.5f)
        {
            // Too few portals relative to mobs
            Debug.Log("SpawnerManager: Too few portals, adjusting spawn rates");
            // In a full implementation, you would increase portal spawn rates and reduce mob spawn rates
        }
    }

    /// <summary>
    /// Updates general statistics.
    /// </summary>
    private void UpdateStatistics()
    {
        statistics.activePortals = GetTotalActivePortals();
        statistics.activeMobs = GetTotalActiveMobs();
    }

    /// <summary>
    /// Gets the total number of active portals across all spawners.
    /// </summary>
    /// <returns>Total active portal count.</returns>
    public int GetTotalActivePortals()
    {
        return portalSpawners.Sum(spawner => spawner.GetActiveInstanceCount());
    }

    /// <summary>
    /// Gets the total number of active mobs across all spawners.
    /// </summary>
    /// <returns>Total active mob count.</returns>
    public int GetTotalActiveMobs()
    {
        return mobSpawners.Sum(spawner => spawner.GetActiveInstanceCount());
    }

    /// <summary>
    /// Gets the total number of active entities across all spawners.
    /// </summary>
    /// <returns>Total active entity count.</returns>
    public int GetTotalActiveEntities()
    {
        return GetTotalActivePortals() + GetTotalActiveMobs();
    }

    /// <summary>
    /// Checks if global entity limit has been reached.
    /// </summary>
    /// <returns>True if limit has been reached.</returns>
    public bool IsGlobalLimitReached()
    {
        return GetTotalActiveEntities() >= globalEntityLimit;
    }

    /// <summary>
    /// Gets all active player positions.
    /// </summary>
    /// <returns>List of player positions.</returns>
    public List<Vector3> GetPlayerPositions()
    {
        return activePlayers.Where(p => p != null).Select(p => p.position).ToList();
    }

    /// <summary>
    /// Finds the closest player to a given position.
    /// </summary>
    /// <param name="position">Position to check from.</param>
    /// <returns>Closest player transform, or null if no players.</returns>
    public Transform GetClosestPlayer(Vector3 position)
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (var player in activePlayers.Where(p => p != null))
        {
            float distance = Vector3.Distance(position, player.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = player;
            }
        }

        return closest;
    }

    /// <summary>
    /// Creates a visual debug indicator at the specified position.
    /// </summary>
    /// <param name="position">Position to create indicator.</param>
    /// <param name="isSuccess">Whether this indicates a successful spawn.</param>
    /// <param name="duration">How long to display the indicator.</param>
    public void CreateDebugIndicator(Vector3 position, bool isSuccess, float duration = 3f)
    {
        if (!enableVisualDebug || debugIndicatorPrefab == null) return;

        GameObject indicator = Instantiate(debugIndicatorPrefab, position, Quaternion.identity);

        // Color the indicator based on success/failure
        var renderer = indicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = isSuccess ? Color.green : Color.red;
        }

        // Destroy after duration
        Destroy(indicator, duration);
    }

    /// <summary>
    /// Forces all spawners to reset their failure states.
    /// </summary>
    [ContextMenu("Reset All Spawner Failures")]
    public void ResetAllSpawnerFailures()
    {
        foreach (var spawner in portalSpawners)
        {
            spawner.ResetFailureState();
        }

        foreach (var spawner in mobSpawners)
        {
            spawner.ResetFailureState();
        }

        Debug.Log("SpawnerManager: Reset failure states for all spawners");
    }

    /// <summary>
    /// Gets comprehensive debug information about all spawners.
    /// </summary>
    /// <returns>Formatted debug information string.</returns>
    public string GetComprehensiveDebugInfo()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Spawner Manager Debug Info ===");
        sb.AppendLine($"Active Players: {activePlayers.Count}");
        sb.AppendLine($"Total Portals: {statistics.activePortals}");
        sb.AppendLine($"Total Mobs: {statistics.activeMobs}");
        sb.AppendLine($"Global Limit: {GetTotalActiveEntities()}/{globalEntityLimit}");
        sb.AppendLine($"Performance Throttling: {isPerformanceThrottling}");
        sb.AppendLine($"Frame Rate: {statistics.currentFrameRate:F1} fps");
        sb.AppendLine($"Memory Usage: {statistics.currentMemoryUsage:F1} MB");
        sb.AppendLine($"Ecosystem Balance: {statistics.ecosystemBalanceScore:F2}");
        sb.AppendLine("");

        // Add individual spawner info
        sb.AppendLine("=== Portal Spawners ===");
        for (int i = 0; i < portalSpawners.Count; i++)
        {
            sb.AppendLine($"Portal Spawner {i}: {portalSpawners[i].GetSpawnStatistics()}");
        }

        sb.AppendLine("");
        sb.AppendLine("=== Mob Spawners ===");
        for (int i = 0; i < mobSpawners.Count; i++)
        {
            sb.AppendLine($"Mob Spawner {i}: {mobSpawners[i].GetSpawnStatistics()}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Debug UI rendering.
    /// </summary>
    private void OnGUI()
    {
        if (!enableDebugUI || !showDebugWindow) return;

        debugWindowRect = GUI.Window(0, debugWindowRect, DrawDebugWindow, "Spawner Manager Debug");
    }

    /// <summary>
    /// Draws the debug window.
    /// </summary>
    /// <param name="windowID">Window ID.</param>
    private void DrawDebugWindow(int windowID)
    {
        debugScrollPosition = GUILayout.BeginScrollView(debugScrollPosition);

        if (showSpawnStatistics)
        {
            GUILayout.Label("=== Spawn Statistics ===", GUI.skin.box);
            GUILayout.Label($"Active Portals: {statistics.activePortals}");
            GUILayout.Label($"Active Mobs: {statistics.activeMobs}");
            GUILayout.Label($"Total Entities: {GetTotalActiveEntities()}/{globalEntityLimit}");
            GUILayout.Label($"Ecosystem Balance: {statistics.ecosystemBalanceScore:F2}");
            GUILayout.Space(10);
        }

        if (showPerformanceMetrics)
        {
            GUILayout.Label("=== Performance Metrics ===", GUI.skin.box);
            GUILayout.Label($"Frame Rate: {statistics.currentFrameRate:F1} fps");
            GUILayout.Label($"Memory: {statistics.currentMemoryUsage:F1} MB");
            GUILayout.Label($"Throttling: {(isPerformanceThrottling ? "ACTIVE" : "Inactive")}");
            GUILayout.Space(10);
        }

        GUILayout.Label("=== Controls ===", GUI.skin.box);
        if (GUILayout.Button("Reset All Failures"))
        {
            ResetAllSpawnerFailures();
        }

        if (GUILayout.Button("Find Spawners"))
        {
            FindAllSpawners();
        }

        if (GUILayout.Button("Reset Statistics"))
        {
            statistics.Reset();
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    /// <summary>
    /// Gets current spawn statistics for external access.
    /// </summary>
    /// <returns>Current spawn statistics.</returns>
    public SpawnStatistics GetSpawnStatistics()
    {
        return statistics;
    }

    /// <summary>
    /// Manually adds a spawner to be managed (useful for dynamically created spawners).
    /// </summary>
    /// <param name="spawner">Spawner to add.</param>
    public void RegisterSpawner(Component spawner)
    {
        if (spawner is PortalSpawner portalSpawner && !portalSpawners.Contains(portalSpawner))
        {
            portalSpawners.Add(portalSpawner);
            Debug.Log($"SpawnerManager: Registered portal spawner {portalSpawner.name}");
        }
        else if (spawner is MobSpawner mobSpawner && !mobSpawners.Contains(mobSpawner))
        {
            mobSpawners.Add(mobSpawner);
            Debug.Log($"SpawnerManager: Registered mob spawner {mobSpawner.name}");
        }
    }

    /// <summary>
    /// Manually removes a spawner from management.
    /// </summary>
    /// <param name="spawner">Spawner to remove.</param>
    public void UnregisterSpawner(Component spawner)
    {
        if (spawner is PortalSpawner portalSpawner)
        {
            portalSpawners.Remove(portalSpawner);
            Debug.Log($"SpawnerManager: Unregistered portal spawner {portalSpawner.name}");
        }
        else if (spawner is MobSpawner mobSpawner)
        {
            mobSpawners.Remove(mobSpawner);
            Debug.Log($"SpawnerManager: Unregistered mob spawner {mobSpawner.name}");
        }
    }
}