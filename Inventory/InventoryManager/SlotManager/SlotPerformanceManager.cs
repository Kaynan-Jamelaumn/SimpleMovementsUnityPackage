using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SlotPerformanceManager
{
    [Header("Performance Monitoring")]
    [SerializeField, Tooltip("Enable performance monitoring")]
    private bool enableMonitoring = true;

    [SerializeField, Tooltip("Log performance warnings when operations exceed threshold")]
    private bool logPerformanceWarnings = true;

    [SerializeField, Tooltip("Maximum time (ms) for slot creation before warning")]
    private float slotCreationWarningThreshold = 5f;

    [SerializeField, Tooltip("Maximum time (ms) for layout calculation before warning")]
    private float layoutCalculationWarningThreshold = 10f;

    [SerializeField, Tooltip("Maximum time (ms) for layout refresh before warning")]
    private float layoutRefreshWarningThreshold = 15f;

    [SerializeField, Tooltip("Keep performance history for analysis")]
    private bool keepPerformanceHistory = false;

    [SerializeField, Tooltip("Maximum number of performance records to keep")]
    private int maxPerformanceRecords = 100;

    // Performance tracking
    private Dictionary<string, PerformanceMetric> performanceMetrics;
    private List<PerformanceRecord> performanceHistory;
    private System.Diagnostics.Stopwatch stopwatch;

    // Performance statistics
    private int totalOperations;
    private float totalTime;
    private float peakTime;
    private string peakOperation;

    [System.Serializable]
    public class PerformanceMetric
    {
        public string operationName;
        public int executionCount;
        public float totalTime;
        public float averageTime;
        public float minTime;
        public float maxTime;
        public float lastExecutionTime;

        public PerformanceMetric(string name)
        {
            operationName = name;
            executionCount = 0;
            totalTime = 0f;
            averageTime = 0f;
            minTime = float.MaxValue;
            maxTime = 0f;
            lastExecutionTime = 0f;
        }

        public void RecordExecution(float executionTime)
        {
            executionCount++;
            totalTime += executionTime;
            averageTime = totalTime / executionCount;
            minTime = Mathf.Min(minTime, executionTime);
            maxTime = Mathf.Max(maxTime, executionTime);
            lastExecutionTime = executionTime;
        }
    }

    [System.Serializable]
    public class PerformanceRecord
    {
        public string operationName;
        public float executionTime;
        public System.DateTime timestamp;
        public string additionalInfo;

        public PerformanceRecord(string operation, float time, string info = "")
        {
            operationName = operation;
            executionTime = time;
            timestamp = System.DateTime.Now;
            additionalInfo = info;
        }
    }

    // Properties
    public bool EnableMonitoring => enableMonitoring;
    public int TotalOperations => totalOperations;
    public float TotalTime => totalTime;
    public float AverageTime => totalOperations > 0 ? totalTime / totalOperations : 0f;
    public float PeakTime => peakTime;
    public string PeakOperation => peakOperation;

    public void Initialize()
    {
        if (!enableMonitoring) return;

        performanceMetrics = new Dictionary<string, PerformanceMetric>();
        performanceHistory = new List<PerformanceRecord>();
        stopwatch = new System.Diagnostics.Stopwatch();

        ResetPerformanceStats();
    }

    private void ResetPerformanceStats()
    {
        totalOperations = 0;
        totalTime = 0f;
        peakTime = 0f;
        peakOperation = "";
    }

    // Performance Measurement
    public void StartOperation(string operationName)
    {
        if (!enableMonitoring) return;

        stopwatch.Restart();
    }

    public void EndOperation(string operationName, string additionalInfo = "")
    {
        if (!enableMonitoring) return;

        stopwatch.Stop();
        float executionTime = (float)stopwatch.Elapsed.TotalMilliseconds;

        RecordPerformance(operationName, executionTime, additionalInfo);
    }

    private void RecordPerformance(string operationName, float executionTime, string additionalInfo = "")
    {
        // Update global statistics
        totalOperations++;
        totalTime += executionTime;

        if (executionTime > peakTime)
        {
            peakTime = executionTime;
            peakOperation = operationName;
        }

        // Update or create metric for this operation
        if (!performanceMetrics.ContainsKey(operationName))
        {
            performanceMetrics[operationName] = new PerformanceMetric(operationName);
        }

        performanceMetrics[operationName].RecordExecution(executionTime);

        // Add to history if enabled
        if (keepPerformanceHistory)
        {
            AddToHistory(operationName, executionTime, additionalInfo);
        }

        // Check for performance warnings
        CheckPerformanceWarnings(operationName, executionTime);
    }

    private void AddToHistory(string operationName, float executionTime, string additionalInfo)
    {
        var record = new PerformanceRecord(operationName, executionTime, additionalInfo);
        performanceHistory.Add(record);

        // Limit history size
        if (performanceHistory.Count > maxPerformanceRecords)
        {
            performanceHistory.RemoveAt(0);
        }
    }

    private void CheckPerformanceWarnings(string operationName, float executionTime)
    {
        if (!logPerformanceWarnings) return;

        float threshold = GetWarningThreshold(operationName);
        if (executionTime > threshold)
        {
            Debug.LogWarning($"[SlotPerformance] {operationName} took {executionTime:F2}ms (threshold: {threshold:F2}ms)");
        }
    }

    private float GetWarningThreshold(string operationName)
    {
        return operationName.ToLower() switch
        {
            var name when name.Contains("slot") && name.Contains("creation") => slotCreationWarningThreshold,
            var name when name.Contains("layout") && name.Contains("calculation") => layoutCalculationWarningThreshold,
            var name when name.Contains("layout") && name.Contains("refresh") => layoutRefreshWarningThreshold,
            _ => 20f // Default threshold
        };
    }

    // Convenient measurement methods
    public System.IDisposable MeasureOperation(string operationName, string additionalInfo = "")
    {
        if (!enableMonitoring) return new NullDisposable();

        return new PerformanceMeasurement(this, operationName, additionalInfo);
    }

    private class PerformanceMeasurement : System.IDisposable
    {
        private SlotPerformanceManager manager;
        private string operationName;
        private string additionalInfo;

        public PerformanceMeasurement(SlotPerformanceManager manager, string operationName, string additionalInfo)
        {
            this.manager = manager;
            this.operationName = operationName;
            this.additionalInfo = additionalInfo;
            manager.StartOperation(operationName);
        }

        public void Dispose()
        {
            manager.EndOperation(operationName, additionalInfo);
        }
    }

    private class NullDisposable : System.IDisposable
    {
        public void Dispose() { }
    }

    // Performance Analysis
    public PerformanceMetric GetMetric(string operationName)
    {
        return performanceMetrics.ContainsKey(operationName) ? performanceMetrics[operationName] : null;
    }

    public List<PerformanceMetric> GetAllMetrics()
    {
        return new List<PerformanceMetric>(performanceMetrics.Values);
    }

    public List<PerformanceMetric> GetSlowestOperations(int count = 5)
    {
        var metrics = GetAllMetrics();
        metrics.Sort((a, b) => b.averageTime.CompareTo(a.averageTime));
        return metrics.GetRange(0, Mathf.Min(count, metrics.Count));
    }

    public List<PerformanceMetric> GetMostFrequentOperations(int count = 5)
    {
        var metrics = GetAllMetrics();
        metrics.Sort((a, b) => b.executionCount.CompareTo(a.executionCount));
        return metrics.GetRange(0, Mathf.Min(count, metrics.Count));
    }

    public string GetPerformanceReport()
    {
        if (!enableMonitoring)
            return "Performance monitoring is disabled.";

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== Slot Performance Report ===");
        report.AppendLine($"Total Operations: {totalOperations}");
        report.AppendLine($"Total Time: {totalTime:F2}ms");
        report.AppendLine($"Average Time: {AverageTime:F2}ms");
        report.AppendLine($"Peak Time: {peakTime:F2}ms ({peakOperation})");
        report.AppendLine();

        // Top 5 slowest operations
        var slowest = GetSlowestOperations(5);
        if (slowest.Count > 0)
        {
            report.AppendLine("=== Slowest Operations (Average) ===");
            foreach (var metric in slowest)
            {
                report.AppendLine($"{metric.operationName}: {metric.averageTime:F2}ms avg, {metric.maxTime:F2}ms max ({metric.executionCount} executions)");
            }
            report.AppendLine();
        }

        // Most frequent operations
        var frequent = GetMostFrequentOperations(5);
        if (frequent.Count > 0)
        {
            report.AppendLine("=== Most Frequent Operations ===");
            foreach (var metric in frequent)
            {
                report.AppendLine($"{metric.operationName}: {metric.executionCount} executions, {metric.averageTime:F2}ms avg");
            }
            report.AppendLine();
        }

        return report.ToString();
    }

    public string GetDetailedMetricReport(string operationName)
    {
        var metric = GetMetric(operationName);
        if (metric == null)
            return $"No performance data found for operation: {operationName}";

        return $"=== {operationName} Performance Details ===\n" +
               $"Execution Count: {metric.executionCount}\n" +
               $"Total Time: {metric.totalTime:F2}ms\n" +
               $"Average Time: {metric.averageTime:F2}ms\n" +
               $"Min Time: {metric.minTime:F2}ms\n" +
               $"Max Time: {metric.maxTime:F2}ms\n" +
               $"Last Execution: {metric.lastExecutionTime:F2}ms";
    }

    // Performance Optimization Recommendations
    public List<string> GetOptimizationRecommendations()
    {
        var recommendations = new List<string>();

        if (!enableMonitoring)
        {
            recommendations.Add("Enable performance monitoring to get optimization recommendations.");
            return recommendations;
        }

        var slowest = GetSlowestOperations(3);
        foreach (var metric in slowest)
        {
            if (metric.averageTime > 10f)
            {
                recommendations.Add($"Consider optimizing '{metric.operationName}' - averaging {metric.averageTime:F2}ms per execution");
            }
        }

        var frequent = GetMostFrequentOperations(3);
        foreach (var metric in frequent)
        {
            if (metric.executionCount > 100 && metric.averageTime > 2f)
            {
                recommendations.Add($"'{metric.operationName}' is called frequently ({metric.executionCount} times) - consider caching or batching");
            }
        }

        if (peakTime > 50f)
        {
            recommendations.Add($"Peak operation time is very high ({peakTime:F2}ms for '{peakOperation}') - investigate for potential blocking operations");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Performance looks good! No specific recommendations at this time.");
        }

        return recommendations;
    }

    // Configuration
    public void SetMonitoringEnabled(bool enabled)
    {
        enableMonitoring = enabled;
        if (enabled && performanceMetrics == null)
        {
            Initialize();
        }
    }

    public void SetWarningThresholds(float slotCreation, float layoutCalculation, float layoutRefresh)
    {
        slotCreationWarningThreshold = slotCreation;
        layoutCalculationWarningThreshold = layoutCalculation;
        layoutRefreshWarningThreshold = layoutRefresh;
    }

    public void SetHistoryEnabled(bool enabled, int maxRecords = 100)
    {
        keepPerformanceHistory = enabled;
        maxPerformanceRecords = maxRecords;

        if (!enabled && performanceHistory != null)
        {
            performanceHistory.Clear();
        }
    }

    // Clear and Reset
    public void ClearPerformanceData()
    {
        if (performanceMetrics != null)
            performanceMetrics.Clear();

        if (performanceHistory != null)
            performanceHistory.Clear();

        ResetPerformanceStats();
    }

    public void ClearHistory()
    {
        if (performanceHistory != null)
            performanceHistory.Clear();
    }

    // Debug Methods
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogPerformanceReport()
    {
        Debug.Log(GetPerformanceReport());
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogOptimizationRecommendations()
    {
        var recommendations = GetOptimizationRecommendations();
        foreach (var recommendation in recommendations)
        {
            Debug.Log($"[SlotPerformance Recommendation] {recommendation}");
        }
    }
}