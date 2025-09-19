using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Real-time monitoring and debugging tool for the terrain generation system.
/// Shows live statistics, validates object alignment, and provides debugging tools.
/// </summary>
public class TerrainMonitor : MonoBehaviour
{
    [Header("Monitoring Settings")]
    [Tooltip("Enable real-time monitoring display")]
    public bool enableMonitoring = true;

    [Tooltip("Update frequency for statistics (seconds)")]
    [Range(0.1f, 5f)]
    public float updateInterval = 1f;

    [Tooltip("Show on-screen GUI")]
    public bool showGUI = true;

    [Tooltip("GUI position")]
    public Vector2 guiPosition = new Vector2(10, 10);

    [Header("Debug Visualization")]
    [Tooltip("Draw object alignment debug lines")]
    public bool showObjectAlignment = false;

    [Tooltip("Draw biome boundaries")]
    public bool showBiomeBoundaries = false;

    [Tooltip("Show spawn attempt positions")]
    public bool showSpawnAttempts = false;

    [Header("Validation")]
    [Tooltip("Automatically validate object alignment")]
    public bool autoValidateAlignment = true;

    [Tooltip("Fix misaligned objects automatically")]
    public bool autoFixMisalignment = false;

    [Tooltip("Maximum objects to check per frame")]
    [Range(1, 50)]
    public int maxChecksPerFrame = 10;

    [Header("Performance Monitoring")]
    [Tooltip("Track frame rate")]
    public bool monitorPerformance = true;

    [Tooltip("Alert if FPS drops below threshold")]
    [Range(15, 60)]
    public int fpsAlertThreshold = 30;

    // Private fields
    private float lastUpdateTime;
    private TerrainGenerator terrainGenerator;
    private EndlessTerrain endlessTerrain;

    // Statistics
    private TerrainStats currentStats = new TerrainStats();
    private List<float> fpsHistory = new List<float>();
    private Queue<GameObject> objectsToValidate = new Queue<GameObject>();

    // GUI Style
    private GUIStyle guiStyle;
    private bool guiStyleInitialized = false;

    [System.Serializable]
    public class TerrainStats
    {
        public int activeChunks;
        public int totalObjects;
        public int misalignedObjects;
        public float averageFPS;
        public float minFPS;
        public float maxFPS;
        public Dictionary<string, int> objectsByType = new Dictionary<string, int>();
        public Dictionary<string, int> misalignedByType = new Dictionary<string, int>();
        public float alignmentPercentage;
        public int biomesActive;
        public Vector3 playerPosition;
        public int lodLevel;
    }

    private void Start()
    {
        terrainGenerator = FindObjectOfType<TerrainGenerator>();
        endlessTerrain = FindObjectOfType<EndlessTerrain>();

        if (terrainGenerator == null)
        {
            Debug.LogWarning("TerrainMonitor: No TerrainGenerator found in scene");
        }

        if (endlessTerrain == null)
        {
            Debug.LogWarning("TerrainMonitor: No EndlessTerrain found in scene");
        }

        lastUpdateTime = Time.time;
    }

    private void Update()
    {
        if (!enableMonitoring) return;

        // Update FPS tracking
        if (monitorPerformance)
        {
            UpdateFPSTracking();
        }

        // Update statistics at specified interval
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateStatistics();
            lastUpdateTime = Time.time;
        }

        // Validate objects incrementally
        if (autoValidateAlignment)
        {
            ValidateObjectsIncremental();
        }

        // Debug visualization
        if (showObjectAlignment)
        {
            DrawObjectAlignmentDebug();
        }
    }

    private void UpdateFPSTracking()
    {
        float currentFPS = 1f / Time.deltaTime;
        fpsHistory.Add(currentFPS);

        // Keep only last 60 frames
        if (fpsHistory.Count > 60)
        {
            fpsHistory.RemoveAt(0);
        }

        // Alert on low FPS
        if (currentFPS < fpsAlertThreshold && Time.frameCount % 60 == 0)
        {
            Debug.LogWarning($"Low FPS detected: {currentFPS:F1} (threshold: {fpsAlertThreshold})");
        }
    }

    private void UpdateStatistics()
    {
        currentStats = new TerrainStats();

        // Find all terrain chunks
        GameObject[] terrainChunks = GameObject.FindGameObjectsWithTag("Ground");
        currentStats.activeChunks = terrainChunks.Length;

        // Player position
        if (endlessTerrain?.viewer != null)
        {
            currentStats.playerPosition = endlessTerrain.viewer.position;
        }

        // LOD level
        if (terrainGenerator != null)
        {
            currentStats.lodLevel = terrainGenerator.LevelOfDetail;
        }

        // Count objects and check alignment
        int totalObjects = 0;
        int misalignedObjects = 0;
        currentStats.objectsByType.Clear();
        currentStats.misalignedByType.Clear();

        foreach (GameObject chunk in terrainChunks)
        {
            for (int i = 0; i < chunk.transform.childCount; i++)
            {
                Transform child = chunk.transform.GetChild(i);

                // Skip if it's a spawner or other system component
                if (child.GetComponent<SpawnerBase<object, object>>() != null) continue;

                totalObjects++;
                string objectType = child.name.Replace("(Clone)", "").Trim();

                if (!currentStats.objectsByType.ContainsKey(objectType))
                {
                    currentStats.objectsByType[objectType] = 0;
                    currentStats.misalignedByType[objectType] = 0;
                }

                currentStats.objectsByType[objectType]++;

                // Check alignment
                if (!IsObjectProperlyAligned(child, chunk.transform))
                {
                    misalignedObjects++;
                    currentStats.misalignedByType[objectType]++;

                    // Auto-fix if enabled
                    if (autoFixMisalignment)
                    {
                        FixObjectAlignment(child.gameObject, chunk.transform);
                    }
                }
            }
        }

        currentStats.totalObjects = totalObjects;
        currentStats.misalignedObjects = misalignedObjects;
        currentStats.alignmentPercentage = totalObjects > 0 ?
            ((totalObjects - misalignedObjects) / (float)totalObjects) * 100f : 100f;

        // FPS statistics
        if (fpsHistory.Count > 0)
        {
            currentStats.averageFPS = fpsHistory.Average();
            currentStats.minFPS = fpsHistory.Min();
            currentStats.maxFPS = fpsHistory.Max();
        }

        // Count active biomes
        if (terrainGenerator?.BiomeDefinitions != null)
        {
            currentStats.biomesActive = terrainGenerator.BiomeDefinitions.Length;
        }
    }

    private void ValidateObjectsIncremental()
    {
        // Populate queue if empty
        if (objectsToValidate.Count == 0)
        {
            GameObject[] terrainChunks = GameObject.FindGameObjectsWithTag("Ground");
            foreach (GameObject chunk in terrainChunks)
            {
                for (int i = 0; i < chunk.transform.childCount; i++)
                {
                    Transform child = chunk.transform.GetChild(i);
                    if (child.GetComponent<Collider>() != null)
                    {
                        objectsToValidate.Enqueue(child.gameObject);
                    }
                }
            }
        }

        // Validate a few objects each frame
        int checksThisFrame = 0;
        while (objectsToValidate.Count > 0 && checksThisFrame < maxChecksPerFrame)
        {
            GameObject obj = objectsToValidate.Dequeue();
            if (obj != null)
            {
                ValidateAndFixObject(obj);
            }
            checksThisFrame++;
        }
    }

    private void ValidateAndFixObject(GameObject obj)
    {
        Transform chunkParent = obj.transform.parent;
        if (chunkParent == null || !chunkParent.CompareTag("Ground")) return;

        if (!IsObjectProperlyAligned(obj.transform, chunkParent))
        {
            if (autoFixMisalignment)
            {
                FixObjectAlignment(obj, chunkParent);
            }
        }
    }

    private bool IsObjectProperlyAligned(Transform obj, Transform terrainTransform)
    {
        Vector3 rayStart = obj.position + Vector3.up * 10f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f))
        {
            if (hit.transform == terrainTransform || hit.transform.IsChildOf(terrainTransform))
            {
                float distanceToGround = obj.position.y - hit.point.y;
                Collider objCollider = obj.GetComponent<Collider>();

                if (objCollider != null)
                {
                    float expectedHeight = GetExpectedObjectHeight(objCollider);
                    float tolerance = 0.5f;

                    return Mathf.Abs(distanceToGround - expectedHeight) <= tolerance;
                }
            }
        }

        return false;
    }

    private float GetExpectedObjectHeight(Collider collider)
    {
        if (collider is CapsuleCollider capsule)
            return capsule.height * 0.5f * collider.transform.lossyScale.y;
        else if (collider is BoxCollider box)
            return box.size.y * 0.5f * collider.transform.lossyScale.y;
        else if (collider is SphereCollider sphere)
            return sphere.radius * collider.transform.lossyScale.y;
        else
            return collider.bounds.extents.y;
    }

    private void FixObjectAlignment(GameObject obj, Transform terrainTransform)
    {
        Collider objCollider = obj.GetComponent<Collider>();
        if (objCollider == null) return;

        Vector3 rayStart = obj.transform.position + Vector3.up * 50f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f))
        {
            if (hit.transform == terrainTransform || hit.transform.IsChildOf(terrainTransform))
            {
                Vector3 properPosition = hit.point;
                float heightOffset = GetExpectedObjectHeight(objCollider);
                properPosition.y += heightOffset;

                obj.transform.position = properPosition;
            }
        }
    }

    private void DrawObjectAlignmentDebug()
    {
        GameObject[] terrainChunks = GameObject.FindGameObjectsWithTag("Ground");

        foreach (GameObject chunk in terrainChunks)
        {
            for (int i = 0; i < chunk.transform.childCount; i++)
            {
                Transform child = chunk.transform.GetChild(i);
                Collider objCollider = child.GetComponent<Collider>();
                if (objCollider == null) continue;

                Vector3 rayStart = child.position + Vector3.up * 10f;

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f))
                {
                    if (hit.transform == chunk.transform || hit.transform.IsChildOf(chunk.transform))
                    {
                        bool isAligned = IsObjectProperlyAligned(child, chunk.transform);
                        Color debugColor = isAligned ? Color.green : Color.red;

                        Debug.DrawLine(child.position, hit.point, debugColor, 0.1f);
                        Debug.DrawRay(hit.point, hit.normal * 2f, Color.blue, 0.1f);
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        if (!showGUI || !enableMonitoring) return;

        InitializeGUIStyle();

        float panelWidth = 400f;
        float panelHeight = 500f;
        Rect panelRect = new Rect(guiPosition.x, guiPosition.y, panelWidth, panelHeight);

        GUI.Box(panelRect, "");

        GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 10, panelRect.width - 20, panelRect.height - 20));

        GUILayout.Label("TERRAIN SYSTEM MONITOR", guiStyle);
        GUILayout.Space(10);

        // Performance Stats
        GUILayout.Label($"<b>PERFORMANCE</b>", guiStyle);
        GUILayout.Label($"FPS: {currentStats.averageFPS:F1} (min: {currentStats.minFPS:F1}, max: {currentStats.maxFPS:F1})", guiStyle);
        GUILayout.Label($"LOD Level: {currentStats.lodLevel}", guiStyle);
        GUILayout.Space(5);

        // Terrain Stats
        GUILayout.Label($"<b>TERRAIN</b>", guiStyle);
        GUILayout.Label($"Active Chunks: {currentStats.activeChunks}", guiStyle);
        GUILayout.Label($"Total Objects: {currentStats.totalObjects}", guiStyle);
        GUILayout.Label($"Biomes: {currentStats.biomesActive}", guiStyle);
        GUILayout.Space(5);

        // Alignment Stats
        GUILayout.Label($"<b>OBJECT ALIGNMENT</b>", guiStyle);
        GUILayout.Label($"Properly Aligned: {currentStats.alignmentPercentage:F1}%", guiStyle);
        GUILayout.Label($"Misaligned Objects: {currentStats.misalignedObjects}", guiStyle);

        if (currentStats.misalignedObjects > 0)
        {
            GUI.color = Color.red;
            GUILayout.Label($"⚠️ Alignment Issues Detected!", guiStyle);
            GUI.color = Color.white;
        }

        GUILayout.Space(5);

        // Player Position
        GUILayout.Label($"<b>PLAYER</b>", guiStyle);
        GUILayout.Label($"Position: ({currentStats.playerPosition.x:F0}, {currentStats.playerPosition.y:F1}, {currentStats.playerPosition.z:F0})", guiStyle);
        GUILayout.Space(5);

        // Object Breakdown
        GUILayout.Label($"<b>OBJECTS BY TYPE</b>", guiStyle);
        foreach (var kvp in currentStats.objectsByType.Take(5))
        {
            int misaligned = currentStats.misalignedByType.ContainsKey(kvp.Key) ?
                currentStats.misalignedByType[kvp.Key] : 0;

            Color textColor = misaligned > 0 ? Color.yellow : Color.white;
            GUI.color = textColor;
            GUILayout.Label($"{kvp.Key}: {kvp.Value} ({misaligned} misaligned)", guiStyle);
            GUI.color = Color.white;
        }

        GUILayout.Space(10);

        // Control Buttons
        if (GUILayout.Button("Validate All Objects"))
        {
            StartCoroutine(ValidateAllObjectsCoroutine());
        }

        if (GUILayout.Button("Fix All Misaligned Objects"))
        {
            StartCoroutine(FixAllMisalignedObjectsCoroutine());
        }

        if (GUILayout.Button("Reset Statistics"))
        {
            fpsHistory.Clear();
            objectsToValidate.Clear();
        }

        GUILayout.EndArea();
    }

    private void InitializeGUIStyle()
    {
        if (guiStyleInitialized) return;

        guiStyle = new GUIStyle(GUI.skin.label);
        guiStyle.fontSize = 12;
        guiStyle.normal.textColor = Color.white;
        guiStyle.richText = true;

        guiStyleInitialized = true;
    }

    private System.Collections.IEnumerator ValidateAllObjectsCoroutine()
    {
        Debug.Log("Starting comprehensive object validation...");

        GameObject[] terrainChunks = GameObject.FindGameObjectsWithTag("Ground");
        int totalChecked = 0;
        int totalFixed = 0;

        foreach (GameObject chunk in terrainChunks)
        {
            for (int i = 0; i < chunk.transform.childCount; i++)
            {
                Transform child = chunk.transform.GetChild(i);
                if (child.GetComponent<Collider>() != null)
                {
                    if (!IsObjectProperlyAligned(child, chunk.transform))
                    {
                        FixObjectAlignment(child.gameObject, chunk.transform);
                        totalFixed++;
                    }
                    totalChecked++;

                    if (totalChecked % 50 == 0)
                    {
                        yield return null; // Spread work across frames
                    }
                }
            }
        }

        Debug.Log($"Validation complete: Checked {totalChecked} objects, fixed {totalFixed}");
    }

    private System.Collections.IEnumerator FixAllMisalignedObjectsCoroutine()
    {
        yield return StartCoroutine(ValidateAllObjectsCoroutine());
    }

    // Public methods for external access
    public TerrainStats GetCurrentStats() => currentStats;

    public void ForceStatsUpdate() => UpdateStatistics();

    [ContextMenu("Generate Statistics Report")]
    public void GenerateDetailedReport()
    {
        UpdateStatistics();

        StringBuilder report = new StringBuilder();
        report.AppendLine("=== DETAILED TERRAIN SYSTEM REPORT ===");
        report.AppendLine($"Generated: {System.DateTime.Now}");
        report.AppendLine();

        report.AppendLine("PERFORMANCE:");
        report.AppendLine($"  Average FPS: {currentStats.averageFPS:F2}");
        report.AppendLine($"  Min FPS: {currentStats.minFPS:F2}");
        report.AppendLine($"  Max FPS: {currentStats.maxFPS:F2}");
        report.AppendLine($"  LOD Level: {currentStats.lodLevel}");
        report.AppendLine();

        report.AppendLine("TERRAIN:");
        report.AppendLine($"  Active Chunks: {currentStats.activeChunks}");
        report.AppendLine($"  Total Objects: {currentStats.totalObjects}");
        report.AppendLine($"  Object Alignment: {currentStats.alignmentPercentage:F1}%");
        report.AppendLine($"  Misaligned Objects: {currentStats.misalignedObjects}");
        report.AppendLine();

        report.AppendLine("OBJECTS BY TYPE:");
        foreach (var kvp in currentStats.objectsByType)
        {
            int misaligned = currentStats.misalignedByType.ContainsKey(kvp.Key) ?
                currentStats.misalignedByType[kvp.Key] : 0;
            report.AppendLine($"  {kvp.Key}: {kvp.Value} total, {misaligned} misaligned");
        }

        Debug.Log(report.ToString());
    }
}