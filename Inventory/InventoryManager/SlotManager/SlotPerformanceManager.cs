using UnityEngine;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class SlotPerformanceManager
{
    [Header("Performance Tracking Settings")]
    [SerializeField, Tooltip("Enable performance monitoring")]
    private bool enablePerformanceTracking = true;

    [SerializeField, Tooltip("Maximum number of operation records to keep")]
    private int maxOperationRecords = 1000;

    [SerializeField, Tooltip("Log performance warnings when operations exceed this threshold (ms)")]
    private float performanceWarningThreshold = 50f;

    [SerializeField, Tooltip("Log detailed performance info")]
    private bool logDetailedInfo = false;

    // Performance data
    [SerializeField] private int totalOperations;
    [SerializeField] private float totalTime;
    [SerializeField] private float peakTime;
    [SerializeField] private string peakOperation = "";
    [SerializeField] private int operationsThisFrame;
    [SerializeField] private float timeThisFrame;

    // Operation tracking (runtime only)
    [System.NonSerialized] private List<OperationRecord> operationHistory;
    [System.NonSerialized] private Dictionary<string, OperationStats> operationStats;
    [System.NonSerialized] private float lastFrameTime;

    // Properties
    public bool EnablePerformanceTracking => enablePerformanceTracking;
    public int TotalOperations => totalOperations;
    public float TotalTime => totalTime;
    public float AverageTime => totalOperations > 0 ? totalTime / totalOperations : 0f;
    public float PeakTime => peakTime;
    public string PeakOperation => peakOperation;
    public int OperationsThisFrame => operationsThisFrame;
    public float TimeThisFrame => timeThisFrame;

    [System.Serializable]
    private struct OperationRecord
    {
        public string operationName;
        public float duration;
        public float timestamp;
        public string details;
    }

    [System.Serializable]
    private struct OperationStats
    {
        public int count;
        public float totalTime;
        public float minTime;
        public float maxTime;
        public float lastTime;
    }

    public void Initialize()
    {
        operationHistory = new List<OperationRecord>();
        operationStats = new Dictionary<string, OperationStats>();
        lastFrameTime = Time.realtimeSinceStartup;
        ClearFrameStats();

        if (enablePerformanceTracking && logDetailedInfo)
        {
            Debug.Log("SlotPerformanceManager initialized with tracking enabled");
        }
    }

    public void Update()
    {
        // Reset frame stats if we're on a new frame
        float currentTime = Time.realtimeSinceStartup;
        if (currentTime - lastFrameTime > 0.016f) // ~60fps threshold
        {
            ClearFrameStats();
            lastFrameTime = currentTime;
        }
    }

    private void ClearFrameStats()
    {
        operationsThisFrame = 0;
        timeThisFrame = 0f;
    }

    public System.IDisposable MeasureOperation(string operationName, string details = "")
    {
        if (!enablePerformanceTracking)
            return new NullMeasurer();

        return new PerformanceMeasurer(this, operationName, details);
    }

    internal void RecordOperation(string operationName, float duration, string details = "")
    {
        if (!enablePerformanceTracking) return;

        // Update totals
        totalOperations++;
        totalTime += duration;
        operationsThisFrame++;
        timeThisFrame += duration;

        // Update peak
        if (duration > peakTime)
        {
            peakTime = duration;
            peakOperation = operationName;
        }

        // Add to history
        if (operationHistory.Count >= maxOperationRecords)
        {
            operationHistory.RemoveAt(0);
        }

        operationHistory.Add(new OperationRecord
        {
            operationName = operationName,
            duration = duration,
            timestamp = Time.realtimeSinceStartup,
            details = details
        });

        // Update operation-specific stats
        UpdateOperationStats(operationName, duration);

        // Log warning if operation is slow
        if (duration > performanceWarningThreshold)
        {
            Debug.LogWarning($"Slow operation detected: {operationName} took {duration:F2}ms (threshold: {performanceWarningThreshold}ms)");
        }

        // Log detailed info if enabled
        if (logDetailedInfo)
        {
            Debug.Log($"Performance: {operationName} completed in {duration:F2}ms{(!string.IsNullOrEmpty(details) ? $" ({details})" : "")}");
        }
    }

    private void UpdateOperationStats(string operationName, float duration)
    {
        if (operationStats.TryGetValue(operationName, out OperationStats stats))
        {
            stats.count++;
            stats.totalTime += duration;
            stats.minTime = Mathf.Min(stats.minTime, duration);
            stats.maxTime = Mathf.Max(stats.maxTime, duration);
            stats.lastTime = duration;
            operationStats[operationName] = stats;
        }
        else
        {
            operationStats[operationName] = new OperationStats
            {
                count = 1,
                totalTime = duration,
                minTime = duration,
                maxTime = duration,
                lastTime = duration
            };
        }
    }

    public void ClearPerformanceData()
    {
        totalOperations = 0;
        totalTime = 0f;
        peakTime = 0f;
        peakOperation = "";
        operationsThisFrame = 0;
        timeThisFrame = 0f;

        operationHistory?.Clear();
        operationStats?.Clear();

        if (logDetailedInfo)
        {
            Debug.Log("Performance data cleared");
        }
    }

    public void LogOptimizationRecommendations()
    {
        var recommendations = new StringBuilder();
        recommendations.AppendLine("🔧 Performance Optimization Recommendations:");

        if (peakTime > 100f)
        {
            recommendations.AppendLine($"• Critical: {peakOperation} is very slow ({peakTime:F2}ms). Consider optimization.");
        }
        else if (peakTime > 50f)
        {
            recommendations.AppendLine($"• Warning: {peakOperation} is slow ({peakTime:F2}ms). May need optimization.");
        }

        if (totalOperations > 10000)
        {
            recommendations.AppendLine("• Consider implementing operation pooling to reduce memory allocations.");
        }

        if (AverageTime > 10f)
        {
            recommendations.AppendLine("• Average operation time is high. Review frequently called operations.");
        }

        // Analyze operation-specific recommendations
        if (operationStats != null)
        {
            foreach (var kvp in operationStats)
            {
                var stats = kvp.Value;
                if (stats.count > 100 && stats.totalTime / stats.count > 20f)
                {
                    recommendations.AppendLine($"• Optimize '{kvp.Key}': called {stats.count} times, avg {stats.totalTime / stats.count:F2}ms");
                }
            }
        }

        if (recommendations.Length > 50) // More than just the header
        {
            Debug.Log(recommendations.ToString());
        }
        else
        {
            Debug.Log("✅ No specific optimization recommendations at this time.");
        }
    }

    public void LogPerformanceReport()
    {
        var report = new StringBuilder();
        report.AppendLine("📊 Slot Performance Report");
        report.AppendLine("========================");
        report.AppendLine($"Total Operations: {totalOperations}");
        report.AppendLine($"Total Time: {totalTime:F2}ms");
        report.AppendLine($"Average Time: {AverageTime:F2}ms");
        report.AppendLine($"Peak Time: {peakTime:F2}ms ({peakOperation})");
        report.AppendLine($"Current Frame: {operationsThisFrame} ops, {timeThisFrame:F2}ms");

        if (operationStats != null && operationStats.Count > 0)
        {
            report.AppendLine("\n🔍 Operation Breakdown:");
            foreach (var kvp in operationStats)
            {
                var stats = kvp.Value;
                float avgTime = stats.totalTime / stats.count;
                report.AppendLine($"• {kvp.Key}: {stats.count}x, avg {avgTime:F2}ms (min: {stats.minTime:F2}ms, max: {stats.maxTime:F2}ms)");
            }
        }

        if (operationHistory != null && operationHistory.Count > 0)
        {
            report.AppendLine($"\n📈 Recent Operations (last {Mathf.Min(5, operationHistory.Count)}):");
            for (int i = Mathf.Max(0, operationHistory.Count - 5); i < operationHistory.Count; i++)
            {
                var record = operationHistory[i];
                report.AppendLine($"• {record.operationName}: {record.duration:F2}ms{(!string.IsNullOrEmpty(record.details) ? $" ({record.details})" : "")}");
            }
        }

        Debug.Log(report.ToString());
    }

    public string GetPerformanceReport()
    {
        return $"Performance Report:\n" +
               $"- Total Operations: {totalOperations}\n" +
               $"- Total Time: {totalTime:F2}ms\n" +
               $"- Average Time: {AverageTime:F2}ms\n" +
               $"- Peak Time: {peakTime:F2}ms ({peakOperation})\n" +
               $"- Current Frame: {operationsThisFrame} ops, {timeThisFrame:F2}ms";
    }

    public void SetPerformanceTracking(bool enabled)
    {
        enablePerformanceTracking = enabled;
        if (!enabled)
        {
            ClearPerformanceData();
        }
    }

    public void SetWarningThreshold(float thresholdMs)
    {
        performanceWarningThreshold = Mathf.Max(0f, thresholdMs);
    }

    public void SetDetailedLogging(bool enabled)
    {
        logDetailedInfo = enabled;
    }

    // Get specific operation stats
    public bool TryGetOperationStats(string operationName, out float averageTime, out int callCount)
    {
        if (operationStats != null && operationStats.TryGetValue(operationName, out OperationStats stats))
        {
            averageTime = stats.totalTime / stats.count;
            callCount = stats.count;
            return true;
        }

        averageTime = 0f;
        callCount = 0;
        return false;
    }

    // Performance measurer implementation
    private class PerformanceMeasurer : System.IDisposable
    {
        private SlotPerformanceManager manager;
        private string operationName;
        private string details;
        private float startTime;

        public PerformanceMeasurer(SlotPerformanceManager manager, string operationName, string details)
        {
            this.manager = manager;
            this.operationName = operationName;
            this.details = details;
            this.startTime = Time.realtimeSinceStartup * 1000f; // Convert to milliseconds
        }

        public void Dispose()
        {
            float duration = (Time.realtimeSinceStartup * 1000f) - startTime;
            manager.RecordOperation(operationName, duration, details);
        }
    }

    // Null measurer for when performance tracking is disabled
    private class NullMeasurer : System.IDisposable
    {
        public void Dispose() { }
    }
}