using UnityEngine;

/// <summary>
///  base class for defining common settings for spawnable entities (Portals, Mobs).
/// Includes advanced configurable options for timing, retries, performance monitoring,
/// and natural spawning behaviors with comprehensive validation.
/// </summary>
[System.Serializable]
public abstract class BaseSettings
{
    [Header("Basic Spawn Timing")]
    /// <summary>
    /// Determines if the system should wait before starting the spawning process.
    /// </summary>
    [Tooltip("Should the system wait before starting the spawning process?")]
    public bool shouldWaitToStartSpawning;

    /// <summary>
    /// The fixed time to wait before starting the spawning process.
    /// </summary>
    [Tooltip("The time to wait before starting the spawn process.")]
    public float waitingTime;

    /// <summary>
    /// The minimum randomized waiting time before spawning.
    /// </summary>
    [Tooltip("Minimum waiting time before spawning.")]
    public float minWaitingTime;

    /// <summary>
    /// The maximum randomized waiting time before spawning.
    /// </summary>
    [Tooltip("Maximum waiting time before spawning.")]
    public float maxWaitingTime;

    /// <summary>
    /// Determines if the waiting time between spawns should be randomized.
    /// </summary>
    [Tooltip("Should there be a random waiting time between spawns?")]
    public bool shouldHaveRandomWaitingTime;

    /// <summary>
    /// The time interval between retries for failed spawns.
    /// </summary>
    [Tooltip("Time interval between retries for spawning.")]
    public float retryingSpawnTime;

    [Header("Advanced Timing Controls")]
    // UNIMPLEMENTED: Adaptive timing methods exist but are never called by spawners
    /// <summary>
    /// Enable adaptive timing based on spawn success/failure rates.
    /// </summary>
    [Tooltip("Enable adaptive timing based on success/failure rates.")]
    public bool enableAdaptiveTiming = false;

    // UNIMPLEMENTED: Not used in spawn logic
    /// <summary>
    /// Minimum time multiplier when adaptive timing reduces spawn intervals.
    /// </summary>
    [Tooltip("Minimum timing multiplier for adaptive timing.")]
    [Range(0.1f, 1f)]
    public float minTimingMultiplier = 0.5f;

    // UNIMPLEMENTED: Not used in spawn logic
    /// <summary>
    /// Maximum time multiplier when adaptive timing increases spawn intervals.
    /// </summary>
    [Tooltip("Maximum timing multiplier for adaptive timing.")]
    [Range(1f, 5f)]
    public float maxTimingMultiplier = 2f;

    // UNIMPLEMENTED: Not used in spawn logic
    /// <summary>
    /// How quickly adaptive timing responds to success/failure patterns.
    /// </summary>
    [Tooltip("How quickly adaptive timing responds to patterns.")]
    [Range(0.1f, 2f)]
    public float adaptiveTimingSpeed = 1f;

    [Header("Spawn Wave Configuration")]
    // UNIMPLEMENTED: Wave spawning completely unimplemented
    /// <summary>
    /// Enable wave-based spawning patterns instead of continuous spawning.
    /// </summary>
    [Tooltip("Enable wave-based spawning patterns.")]
    public bool enableWaveSpawning = false;

    // UNIMPLEMENTED: Not used in spawn logic
    /// <summary>
    /// Number of entities to spawn per wave.
    /// </summary>
    [Tooltip("Number of entities to spawn per wave.")]
    [Range(1, 20)]
    public int entitiesPerWave = 3;

    // UNIMPLEMENTED: Not used in spawn logic
    /// <summary>
    /// Time between waves when wave spawning is enabled.
    /// </summary>
    [Tooltip("Time between spawning waves.")]
    [Range(10f, 300f)]
    public float timeBetweenWaves = 60f;

    // UNIMPLEMENTED: Not used in spawn logic
    /// <summary>
    /// Random variation in wave timing (±percentage).
    /// </summary>
    [Tooltip("Random variation in wave timing (±percentage).")]
    [Range(0f, 0.5f)]
    public float waveTimingVariation = 0.2f;

    [Header("Performance and Quality Controls")]
    // UNIMPLEMENTED: Performance throttling not checked by spawners
    /// <summary>
    /// Enable performance-based spawn throttling.
    /// </summary>
    [Tooltip("Enable performance-based spawn throttling.")]
    public bool enablePerformanceThrottling = true;

    // UNIMPLEMENTED: Target frame rate not used in spawner logic
    /// <summary>
    /// Target frame rate for performance throttling (0 = disabled).
    /// </summary>
    [Tooltip("Target frame rate for throttling (0 = disabled).")]
    [Range(0f, 120f)]
    public float targetFrameRate = 60f;

    // UNIMPLEMENTED: Throttle amount not applied
    /// <summary>
    /// Spawn rate reduction when frame rate drops below target.
    /// </summary>
    [Tooltip("Spawn rate reduction when frame rate drops.")]
    [Range(0.1f, 1f)]
    public float performanceThrottleAmount = 0.5f;

    // UNIMPLEMENTED: Quality adjustments completely unimplemented
    /// <summary>
    /// Enable quality-based spawn adjustments (reduce spawning on lower quality settings).
    /// </summary>
    [Tooltip("Enable quality-based spawn adjustments.")]
    public bool enableQualityAdjustments = true;

    [Header("Spawn Burst and Cooldown")]
    // UNIMPLEMENTED: Burst spawning not implemented in spawners
    /// <summary>
    /// Enable burst spawning where multiple entities spawn rapidly then cooldown.
    /// </summary>
    [Tooltip("Enable burst spawning with rapid spawns followed by cooldown.")]
    public bool enableBurstSpawning = false;

    // UNIMPLEMENTED: Burst size not used
    /// <summary>
    /// Number of entities in a burst.
    /// </summary>
    [Tooltip("Number of entities in a burst.")]
    [Range(2, 10)]
    public int burstSize = 3;

    // UNIMPLEMENTED: Burst interval not used
    /// <summary>
    /// Time between entities within a burst.
    /// </summary>
    [Tooltip("Time between entities within a burst.")]
    [Range(0.1f, 5f)]
    public float burstInterval = 1f;

    // UNIMPLEMENTED: Burst cooldown not used
    /// <summary>
    /// Cooldown time after a burst before next spawn can occur.
    /// </summary>
    [Tooltip("Cooldown time after burst before next spawn.")]
    [Range(10f, 120f)]
    public float burstCooldown = 30f;

    // UNIMPLEMENTED: Burst chance not used
    /// <summary>
    /// Chance for burst spawning when conditions are met.
    /// </summary>
    [Tooltip("Chance for burst spawning when conditions are met.")]
    [Range(0f, 1f)]
    public float burstChance = 0.3f;

    [Header("Environmental Response")]
    // UNIMPLEMENTED: Environmental response not used by spawners
    /// <summary>
    /// Enable spawning rate changes based on environmental factors.
    /// </summary>
    [Tooltip("Enable environmental factor-based spawn rate changes.")]
    public bool enableEnvironmentalResponse = true;

    // UNIMPLEMENTED: Influence strength not applied
    /// <summary>
    /// How strongly environmental factors affect spawn rates.
    /// </summary>
    [Tooltip("Strength of environmental influence on spawn rates.")]
    [Range(0.1f, 3f)]
    public float environmentalInfluenceStrength = 1f;

    // UNIMPLEMENTED: Weather response not implemented
    /// <summary>
    /// Enable weather-based spawn rate modifications (future expansion).
    /// </summary>
    [Tooltip("Enable weather-based spawn modifications.")]
    public bool enableWeatherResponse = false;

    // UNIMPLEMENTED: Population pressure not implemented
    /// <summary>
    /// Enable population pressure response (spawn less when crowded).
    /// </summary>
    [Tooltip("Enable population pressure response.")]
    public bool enablePopulationPressureResponse = true;

    [Header("Debugging and Monitoring")]
    /// <summary>
    /// Enable detailed debug logging for spawn operations.
    /// </summary>
    [Tooltip("Enable detailed debug logging for spawn operations.")]
    public bool enableDetailedLogging = false;

    /// <summary>
    /// Enable visual debug indicators for spawn attempts and successes.
    /// </summary>
    [Tooltip("Enable visual debug indicators for spawn attempts.")]
    public bool enableVisualDebug = false;

    /// <summary>
    /// How long to display visual debug indicators (in seconds).
    /// </summary>
    [Tooltip("Duration for visual debug indicators.")]
    [Range(1f, 30f)]
    public float debugIndicatorDuration = 5f;

    /// <summary>
    /// Enable spawn statistics tracking and reporting.
    /// </summary>
    [Tooltip("Enable spawn statistics tracking and reporting.")]
    public bool enableStatisticsTracking = true;

    [Header("Safety and Fallback")]
    /// <summary>
    /// Enable automatic parameter validation and correction.
    /// </summary>
    [Tooltip("Enable automatic parameter validation and correction.")]
    public bool enableAutoValidation = true;

    /// <summary>
    /// Enable fallback to safe defaults if settings are invalid.
    /// </summary>
    [Tooltip("Enable fallback to safe defaults for invalid settings.")]
    public bool enableSafeFallbacks = true;

    // UNIMPLEMENTED: Max total spawn attempts not enforced in spawners
    /// <summary>
    /// Maximum total spawn attempts before giving up entirely (safety limit).
    /// </summary>
    [Tooltip("Maximum total spawn attempts before giving up (safety limit).")]
    [Range(100, 10000)]
    public int maxTotalSpawnAttempts = 1000;

    // UNIMPLEMENTED: Emergency rate limit not enforced in spawners
    /// <summary>
    /// Emergency spawn rate limit (spawns per second) to prevent system overload.
    /// </summary>
    [Tooltip("Emergency spawn rate limit (spawns per second).")]
    [Range(0.1f, 10f)]
    public float emergencyRateLimit = 2f;

    // Runtime tracking variables (not exposed in inspector)
    [HideInInspector] public float currentTimingMultiplier = 1f;
    [HideInInspector] public int consecutiveFailures = 0;
    [HideInInspector] public int consecutiveSuccesses = 0;
    [HideInInspector] public float lastSpawnTime = 0f;
    [HideInInspector] public int totalSpawnAttempts = 0;
    [HideInInspector] public bool isInBurstMode = false;
    [HideInInspector] public float burstStartTime = 0f;
    [HideInInspector] public int currentBurstCount = 0;

    /// <summary>
    /// Validates base settings and corrects invalid values.
    /// </summary>
    protected virtual void ValidateBaseSettings()
    {
        if (!enableAutoValidation) return;

        // Validate basic timing
        waitingTime = Mathf.Max(0f, waitingTime);
        minWaitingTime = Mathf.Max(0f, minWaitingTime);
        maxWaitingTime = Mathf.Max(minWaitingTime, maxWaitingTime);
        retryingSpawnTime = Mathf.Max(0.1f, retryingSpawnTime);

        if (shouldHaveRandomWaitingTime && minWaitingTime > maxWaitingTime)
        {
            if (enableDetailedLogging)
                Debug.LogWarning("BaseSettings: minWaitingTime > maxWaitingTime, correcting");
            maxWaitingTime = minWaitingTime + 1f;
        }

        // Validate adaptive timing
        minTimingMultiplier = Mathf.Clamp(minTimingMultiplier, 0.1f, 1f);
        maxTimingMultiplier = Mathf.Max(1f, maxTimingMultiplier);
        adaptiveTimingSpeed = Mathf.Max(0.1f, adaptiveTimingSpeed);

        // Validate wave spawning
        entitiesPerWave = Mathf.Max(1, entitiesPerWave);
        timeBetweenWaves = Mathf.Max(1f, timeBetweenWaves);
        waveTimingVariation = Mathf.Clamp01(waveTimingVariation);

        // Validate burst spawning
        burstSize = Mathf.Max(2, burstSize);
        burstInterval = Mathf.Max(0.1f, burstInterval);
        burstCooldown = Mathf.Max(1f, burstCooldown);
        burstChance = Mathf.Clamp01(burstChance);

        // Validate performance settings
        targetFrameRate = Mathf.Max(0f, targetFrameRate);
        performanceThrottleAmount = Mathf.Clamp(performanceThrottleAmount, 0.1f, 1f);

        // Validate safety limits
        maxTotalSpawnAttempts = Mathf.Max(100, maxTotalSpawnAttempts);
        emergencyRateLimit = Mathf.Max(0.1f, emergencyRateLimit);

        // Validate environmental response
        environmentalInfluenceStrength = Mathf.Max(0.1f, environmentalInfluenceStrength);

        // Validate debug settings
        debugIndicatorDuration = Mathf.Max(1f, debugIndicatorDuration);

        // Reset runtime tracking if values are invalid
        currentTimingMultiplier = Mathf.Clamp(currentTimingMultiplier, minTimingMultiplier, maxTimingMultiplier);
        consecutiveFailures = Mathf.Max(0, consecutiveFailures);
        consecutiveSuccesses = Mathf.Max(0, consecutiveSuccesses);
        totalSpawnAttempts = Mathf.Max(0, totalSpawnAttempts);
    }

    // NOTE: The following methods exist but are NEVER CALLED by MobSpawner or PortalSpawner
    // They would need to be integrated into SpawnRoutine() to be functional

    /// <summary>
    /// Gets the effective waiting time considering all modifiers and current state.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    /// <param name="baseTime">Base waiting time to modify.</param>
    /// <returns>Effective waiting time with all modifiers applied.</returns>
    public virtual float GetEffectiveWaitingTime(float baseTime = -1f)
    {
        if (baseTime < 0) baseTime = waitingTime;

        float effectiveTime = shouldHaveRandomWaitingTime ?
            Random.Range(minWaitingTime, maxWaitingTime) :
            baseTime;

        // Apply adaptive timing
        if (enableAdaptiveTiming)
        {
            effectiveTime *= currentTimingMultiplier;
        }

        // Apply performance throttling
        if (enablePerformanceThrottling && targetFrameRate > 0)
        {
            float currentFPS = 1f / Time.deltaTime;
            if (currentFPS < targetFrameRate)
            {
                float throttleMultiplier = 1f + (1f - performanceThrottleAmount);
                effectiveTime *= throttleMultiplier;
            }
        }

        // Apply environmental response
        if (enableEnvironmentalResponse)
        {
            // Placeholder for environmental factors
            // In a full implementation, this would consider weather, population density, etc.
            effectiveTime *= environmentalInfluenceStrength;
        }

        // Apply emergency rate limiting
        float timeSinceLastSpawn = Time.time - lastSpawnTime;
        float minTimeForRateLimit = 1f / emergencyRateLimit;
        effectiveTime = Mathf.Max(effectiveTime, minTimeForRateLimit - timeSinceLastSpawn);

        return Mathf.Max(0.1f, effectiveTime);
    }

    /// <summary>
    /// Updates adaptive timing based on spawn success/failure.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    /// <param name="wasSuccessful">Whether the spawn attempt was successful.</param>
    public virtual void UpdateAdaptiveTiming(bool wasSuccessful)
    {
        if (!enableAdaptiveTiming) return;

        if (wasSuccessful)
        {
            consecutiveSuccesses++;
            consecutiveFailures = 0;

            // Reduce timing slightly on consecutive successes (spawn faster)
            if (consecutiveSuccesses > 3)
            {
                currentTimingMultiplier = Mathf.Lerp(
                    currentTimingMultiplier,
                    minTimingMultiplier,
                    Time.deltaTime * adaptiveTimingSpeed
                );
            }
        }
        else
        {
            consecutiveFailures++;
            consecutiveSuccesses = 0;

            // Increase timing on failures (spawn slower)
            if (consecutiveFailures > 2)
            {
                currentTimingMultiplier = Mathf.Lerp(
                    currentTimingMultiplier,
                    maxTimingMultiplier,
                    Time.deltaTime * adaptiveTimingSpeed * 2f
                );
            }
        }

        // Clamp the multiplier to valid range
        currentTimingMultiplier = Mathf.Clamp(currentTimingMultiplier, minTimingMultiplier, maxTimingMultiplier);
    }

    /// <summary>
    /// Checks if spawning should be throttled based on performance.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    /// <returns>True if spawning should be throttled.</returns>
    public virtual bool ShouldThrottleSpawning()
    {
        if (!enablePerformanceThrottling) return false;

        // Check frame rate
        if (targetFrameRate > 0)
        {
            float currentFPS = 1f / Time.deltaTime;
            if (currentFPS < targetFrameRate * 0.8f) // 80% of target
            {
                return true;
            }
        }

        // Check emergency rate limit
        float timeSinceLastSpawn = Time.time - lastSpawnTime;
        if (timeSinceLastSpawn < 1f / emergencyRateLimit)
        {
            return true;
        }

        // Check total attempts safety limit
        if (totalSpawnAttempts > maxTotalSpawnAttempts)
        {
            if (enableDetailedLogging)
                Debug.LogWarning($"BaseSettings: Hit max total spawn attempts ({maxTotalSpawnAttempts})");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Records a spawn attempt for statistics and safety tracking.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    /// <param name="wasSuccessful">Whether the spawn was successful.</param>
    public virtual void RecordSpawnAttempt(bool wasSuccessful)
    {
        totalSpawnAttempts++;
        lastSpawnTime = Time.time;

        UpdateAdaptiveTiming(wasSuccessful);

        if (enableStatisticsTracking && enableDetailedLogging)
        {
            Debug.Log($"BaseSettings: Spawn attempt #{totalSpawnAttempts}, Success: {wasSuccessful}, " +
                     $"Consecutive Successes: {consecutiveSuccesses}, Consecutive Failures: {consecutiveFailures}, " +
                     $"Timing Multiplier: {currentTimingMultiplier:F2}");
        }
    }

    /// <summary>
    /// Determines if burst spawning should be triggered.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    /// <returns>True if burst spawning should occur.</returns>
    public virtual bool ShouldTriggerBurst()
    {
        if (!enableBurstSpawning) return false;
        if (isInBurstMode) return false;

        // Check burst conditions
        bool hasGoodConditions = consecutiveSuccesses >= 2 && consecutiveFailures == 0;
        bool passesChanceCheck = Random.value < burstChance;
        bool hasPassedCooldown = Time.time - burstStartTime > burstCooldown;

        return hasGoodConditions && passesChanceCheck && hasPassedCooldown;
    }

    /// <summary>
    /// Starts burst spawning mode.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    public virtual void StartBurstMode()
    {
        if (!enableBurstSpawning) return;

        isInBurstMode = true;
        burstStartTime = Time.time;
        currentBurstCount = 0;

        if (enableDetailedLogging)
            Debug.Log("BaseSettings: Started burst spawning mode");
    }

    /// <summary>
    /// Checks if burst mode is complete and should end.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    /// <returns>True if burst mode should end.</returns>
    public virtual bool ShouldEndBurstMode()
    {
        if (!isInBurstMode) return false;

        return currentBurstCount >= burstSize;
    }

    /// <summary>
    /// Ends burst spawning mode.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    public virtual void EndBurstMode()
    {
        isInBurstMode = false;
        currentBurstCount = 0;

        if (enableDetailedLogging)
            Debug.Log("BaseSettings: Ended burst spawning mode, entering cooldown");
    }

    /// <summary>
    /// Gets burst interval time for spawning within burst.
    /// NOTE: This method exists but is NEVER CALLED by spawners
    /// </summary>
    /// <returns>Time to wait between burst spawns.</returns>
    public virtual float GetBurstInterval()
    {
        return burstInterval;
    }

    /// <summary>
    /// Resets all runtime tracking to initial state.
    /// </summary>
    [ContextMenu("Reset Runtime Tracking")]
    public virtual void ResetRuntimeTracking()
    {
        currentTimingMultiplier = 1f;
        consecutiveFailures = 0;
        consecutiveSuccesses = 0;
        lastSpawnTime = 0f;
        totalSpawnAttempts = 0;
        isInBurstMode = false;
        burstStartTime = 0f;
        currentBurstCount = 0;

        if (enableDetailedLogging)
            Debug.Log("BaseSettings: Runtime tracking reset");
    }

    /// <summary>
    /// Gets a summary of current spawn statistics.
    /// </summary>
    /// <returns>Formatted statistics string.</returns>
    public virtual string GetSpawnStatistics()
    {
        return $"Total Attempts: {totalSpawnAttempts}, " +
               $"Success Streak: {consecutiveSuccesses}, " +
               $"Failure Streak: {consecutiveFailures}, " +
               $"Timing Mult: {currentTimingMultiplier:F2}, " +
               $"Burst Mode: {isInBurstMode}";
    }
}