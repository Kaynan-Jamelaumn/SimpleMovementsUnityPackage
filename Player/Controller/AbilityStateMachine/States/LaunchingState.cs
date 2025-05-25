using System.Collections;
using UnityEngine;

public class LaunchingState : AbilityState
{
    private bool _goToNextState = false;
    private Coroutine _activeRoutine;

    // Performance optimization fields - increased pool sizes for complex scenarios
    // These arrays are reused to avoid creating new arrays every frame (garbage collection optimization)
    private static readonly Collider[] _collisionResults = new Collider[64];
    private static readonly RaycastHit[] _raycastResults = new RaycastHit[32];

    //  caching system
    // Cache frequently accessed components to avoid expensive GetComponent calls
    private Camera _cachedCamera;
    private Vector3 _cachedLaunchDirection;
    private float _lastCacheTime;
    private const float CACHE_REFRESH_INTERVAL = 0.1f; // How often to refresh cached values (in seconds)

    //  collision detection
    private float _lastCollisionCheckTime;
    private float _collisionCheckInterval = 0.02f; // How often to check for collisions (in seconds)

    // Movement filtering with adaptive thresholds
    // Only update positions if movement is significant enough to matter visually
    private Vector3 _lastPosition;
    private Vector3 _lastParticlePosition;
    private float _movementThreshold = 0.01f; // Minimum distance to consider as "moved"
    private float _particleMovementThreshold = 0.005f; // Even smaller threshold for particle effects

    // Frame management - Core frame skipping system
    private int _frameSkipCounter; // Tracks which frame we're on
    private int _frameSkipInterval = 1; // How many frames to skip (1 = no skipping, 2 = skip every other frame)

    // Performance profiling
    private AbilityProfile _currentProfile; // Current optimization settings based on ability type
    private float _performanceScore = 1.0f; // Dynamic performance adjustment (1.0 = perfect, 0.0 = terrible)

    // Memory management
    // Queue to track recent positions for stuck detection
    private readonly System.Collections.Generic.Queue<Vector3> _positionHistory = new(8);
    private int _stuckFrameCount = 0; // How many frames the projectile hasn't moved
    private const int MAX_STUCK_FRAMES = 30; // Max frames before considering projectile "stuck"

    // Distance-based optimizations
    private float _totalTravelDistance = 0f; // Total distance traveled by projectile
    private float _maxOptimizationDistance = 100f; // Distance threshold for aggressive optimization

    // Ability profiles define different optimization strategies based on ability characteristics
    public enum AbilityProfileType
    {
        Standard,     // Default balanced settings
        Precision,    // High accuracy, frequent checks - for abilities requiring exact timing
        HighSpeed,    // Fast projectiles, optimized intervals - for bullets/fast spells
        LongRange,    // Distance-based optimizations - for abilities that travel far
        Tracking,     // Continuous tracking with smart skipping - for homing missiles
        Heavyweight   // Complex abilities with many effects - for expensive spells
    }

    // Profile structure containing all optimization settings for a specific ability type
    private struct AbilityProfile
    {
        public AbilityProfileType type;
        public float collisionInterval; // How often to check collisions
        public float movementThreshold; // Minimum movement to update position
        public int frameSkipInterval; // Frame skipping pattern
        public bool useAdaptiveOptimization; // Whether to dynamically adjust settings
        public float performanceBudget; // CPU time budget per frame in milliseconds

        // Factory method to create predefined profiles for different ability types
        public static AbilityProfile GetProfile(AbilityProfileType type)
        {
            return type switch
            {
                // High precision abilities need frequent updates and small thresholds
                AbilityProfileType.Precision => new AbilityProfile
                {
                    type = type,
                    collisionInterval = 0.008f, // Check collisions every 8ms (very frequent)
                    movementThreshold = 0.002f, // Very small movement threshold
                    frameSkipInterval = 1, // Never skip frames
                    useAdaptiveOptimization = false, // Don't change settings dynamically
                    performanceBudget = 2.0f // Allow more CPU time
                },
                // Fast projectiles can afford less frequent checks due to speed
                AbilityProfileType.HighSpeed => new AbilityProfile
                {
                    type = type,
                    collisionInterval = 0.012f, // Check collisions every 12ms
                    movementThreshold = 0.05f, // Larger movement threshold (speed makes small movements less noticeable)
                    frameSkipInterval = 1, // Don't skip frames (fast movement needs updates)
                    useAdaptiveOptimization = true, // Adjust based on performance
                    performanceBudget = 1.5f
                },
                // Long range abilities can use aggressive optimization when far from target
                AbilityProfileType.LongRange => new AbilityProfile
                {
                    type = type,
                    collisionInterval = 0.025f, // Less frequent collision checks
                    movementThreshold = 0.1f, // Large movement threshold
                    frameSkipInterval = 2, // Skip every other frame
                    useAdaptiveOptimization = true, // Heavily optimize based on distance
                    performanceBudget = 1.0f
                },
                // Tracking abilities need balance between accuracy and performance
                AbilityProfileType.Tracking => new AbilityProfile
                {
                    type = type,
                    collisionInterval = 0.016f, // Moderate collision frequency
                    movementThreshold = 0.02f, // Small movement threshold for smooth tracking
                    frameSkipInterval = 1, // Don't skip frames for smooth tracking
                    useAdaptiveOptimization = true, // Optimize when appropriate
                    performanceBudget = 1.8f
                },
                // Heavy abilities with lots of effects need aggressive optimization
                AbilityProfileType.Heavyweight => new AbilityProfile
                {
                    type = type,
                    collisionInterval = 0.03f, // Least frequent collision checks
                    movementThreshold = 0.08f, // Large movement threshold
                    frameSkipInterval = 2, // Skip every other frame
                    useAdaptiveOptimization = true, // Heavily optimize
                    performanceBudget = 0.8f // Strict CPU budget
                },
                // Default standard profile - balanced for most abilities
                _ => new AbilityProfile
                {
                    type = AbilityProfileType.Standard,
                    collisionInterval = 0.02f, // Check collisions every 20ms
                    movementThreshold = 0.01f, // Standard movement threshold
                    frameSkipInterval = 1, // No frame skipping by default
                    useAdaptiveOptimization = false, // Fixed settings
                    performanceBudget = 1.2f // Moderate CPU budget
                }
            };
        }
    }

    public LaunchingState(AbilityContext context, AbilityStateMachine.EAbilityState estate)
        : base(context, estate) { }

    public override void EnterState()
    {
        RecalculateAvailability(AbilityStateMachine.EAbilityState.Launching);
        InitializeOptimizations();
        StartLaunchSequence();
    }

    public override void ExitState()
    {
        // Clean shutdown of active coroutine
        if (_activeRoutine != null)
        {
            Context.AbilityController.StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        // Reset all optimization state
        ResetOptimizationState();
        _goToNextState = false;
    }

    public override AbilityStateMachine.EAbilityState GetNextState() =>
        _goToNextState ? AbilityStateMachine.EAbilityState.Active : StateKey;

    public override void UpdateState() { }
    public override void OnTriggerEnter(Collider other) { }
    public override void OnTriggerStay(Collider other) { }
    public override void OnTriggerExit(Collider other) { }
    public override void LateUpdateState() { }

    // Initialize all optimization systems when entering the state
    private void InitializeOptimizations()
    {
        // Enhanced ability profiling - determine what kind of ability this is
        _currentProfile = DetermineAbilityProfile();
        ApplyProfileSettings();

        // Initialize caching systems
        InitializeCacheReferences();
        InitializePerformanceTracking();

        // Setup position tracking
        InitializePositionTracking();
    }

    // Apply the settings from the selected profile to our optimization variables
    private void ApplyProfileSettings()
    {
        _collisionCheckInterval = _currentProfile.collisionInterval;
        _movementThreshold = _currentProfile.movementThreshold;
        _frameSkipInterval = _currentProfile.frameSkipInterval;

        // Adaptive particle threshold based on profile
        // Particles can have even smaller thresholds since they're just visual
        _particleMovementThreshold = _movementThreshold * 0.5f;
    }

    // Cache frequently accessed components to avoid expensive lookups
    private void InitializeCacheReferences()
    {
        _cachedCamera = Camera.main;
        _lastCacheTime = Time.time;
        _lastCollisionCheckTime = Time.time;
    }

    // Initialize performance tracking variables
    private void InitializePerformanceTracking()
    {
        _performanceScore = 1.0f; // Start with perfect performance
        _totalTravelDistance = 0f;
        _stuckFrameCount = 0;
        _positionHistory.Clear();
    }

    // Initialize position tracking for movement detection
    private void InitializePositionTracking()
    {
        if (Context.AbilityController != null)
        {
            _lastPosition = Context.AbilityController.transform.position;
            _lastParticlePosition = _lastPosition;
        }
    }

    // Reset all optimization state when exiting
    private void ResetOptimizationState()
    {
        _frameSkipCounter = 0;
        _positionHistory.Clear();
        _stuckFrameCount = 0;
        _totalTravelDistance = 0f;
    }

    // Analyze the ability and determine which optimization profile to use
    private AbilityProfile DetermineAbilityProfile()
    {
        AbilityHolder ability = Context.AbilityHolder;
        if (ability?.abilityEffect == null)
            return AbilityProfile.GetProfile(AbilityProfileType.Standard);

        // Enhanced profile selection with multiple criteria
        // Calculate how complex/expensive this ability is
        int complexityScore = CalculateAbilityComplexity(ability);

        // Choose profile based on ability characteristics
        if (complexityScore > 6)
            return AbilityProfile.GetProfile(AbilityProfileType.Heavyweight); // Very complex ability

        if (ability.abilityEffect.speed > 6)
            return AbilityProfile.GetProfile(AbilityProfileType.HighSpeed); // Fast moving

        if (ability.abilityEffect.lifeSpan > 12f || ability.abilityEffect.finalLaunchTime > 6f)
            return AbilityProfile.GetProfile(AbilityProfileType.LongRange); // Long duration/range

        if (IsTrackingAbility(ability))
            return AbilityProfile.GetProfile(AbilityProfileType.Tracking); // Needs to track target

        if (RequiresPrecision(ability))
            return AbilityProfile.GetProfile(AbilityProfileType.Precision); // Needs high accuracy

        return AbilityProfile.GetProfile(AbilityProfileType.Standard); // Default case
    }

    // Calculate complexity score based on ability features
    private int CalculateAbilityComplexity(AbilityHolder ability)
    {
        int complexity = 0;

        // Each feature adds to complexity score
        if (ability.abilityEffect.multiAreaEffect) complexity += 2; // Area effects are expensive
        if (ability.abilityEffect.shouldLaunch) complexity += 1; // Projectiles need tracking
        if (ability.abilityEffect.isFixedPosition) complexity += 1; // Position tracking
        if (ability.abilityEffect.isPartialPermanentTargetWhileCasting) complexity += 2; // Continuous targeting
        if (ability.abilityEffect.effects != null && ability.abilityEffect.effects.Count > 3) complexity += 2; // Many effects

        return complexity;
    }

    // Check if this ability needs to track/follow something
    private bool IsTrackingAbility(AbilityHolder ability)
    {
        return ability.abilityEffect.isFixedPosition ||
               ability.abilityEffect.isPartialPermanentTargetWhileCasting ||
               ability.abilityEffect.singleTargetSelfTarget;
    }

    // Check if this ability requires high precision (small area effects)
    private bool RequiresPrecision(AbilityHolder ability)
    {
        if (Context.attackCast == null) return false;

        // Small area effects need precise positioning
        return (Context.attackCast.castType == AttackCast.CastType.Sphere && Context.attackCast.castSize < 1.5f) ||
               (Context.attackCast.castType == AttackCast.CastType.Box && Context.attackCast.boxSize.magnitude < 3f);
    }

    // Start the appropriate launch sequence based on ability type
    private void StartLaunchSequence()
    {
        AbilityHolder ability = Context.AbilityHolder;

        if (ability?.abilityEffect == null)
        {
            Debug.LogError("AbilityHolder or AbilityEffect is null in LaunchingState");
            TriggerStateTransition();
            return;
        }

        // Route to appropriate launch routine based on ability type
        if (ability.abilityEffect.shouldLaunch)
            _activeRoutine = Context.AbilityController.StartCoroutine(ProjectileLaunchRoutine());
        else if (ability.abilityEffect.isFixedPosition)
            _activeRoutine = Context.AbilityController.StartCoroutine(FixedPositionRoutine());
        else
            _activeRoutine = Context.AbilityController.StartCoroutine(DelayedActivationRoutine());
    }

    // Handle abilities that stay in a fixed position (like ground effects)
    private IEnumerator FixedPositionRoutine()
    {
        float startTime = Time.time;
        float duration = Context.AbilityHolder.abilityEffect.finalLaunchTime;
        int frameCount = 0;

        while (Time.time <= startTime + duration)
        {
            frameCount++;

            // Enhanced frame skipping with performance awareness
            // Skip frames when appropriate to save CPU time
            if (ShouldSkipFrame(frameCount, startTime + duration - Time.time))
            {
                yield return null; // Skip this frame but still yield control
                continue;
            }

            UpdateFixedPositionTarget();
            yield return null;
        }

        FinalizeAbility();
    }

    // Update position for fixed position abilities (player might move)
    private void UpdateFixedPositionTarget()
    {
        Vector3 currentPosition = Context.AbilityController.transform.position;

        // Only update if movement is significant enough to matter
        if (HasSignificantMovement(currentPosition, _lastPosition))
        {
            SetGizmosAndColliderAndParticlePosition(true);
            _lastPosition = currentPosition;

            // Track position for stuck detection
            TrackPositionHistory(currentPosition);
        }
    }

    // Handle abilities with a delay before activation (like timed explosives)
    private IEnumerator DelayedActivationRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;

        // Critical: Only set initial position if not marked at cast
        if (!ability.abilityEffect.shouldMarkAtCast)
            SetGizmosAndColliderAndParticlePosition();

        // Optimized wait using cached time
        float waitTime = ability.abilityEffect.finalLaunchTime;
        float startTime = Time.time;

        // Simple wait loop - no complex optimization needed for delayed abilities
        while (Time.time < startTime + waitTime)
        {
            yield return null;
        }

        FinalizeAbility();
    }

    // Handle projectile abilities (bullets, fireballs, etc.)
    private IEnumerator ProjectileLaunchRoutine()
    {
        AbilityHolder ability = Context.AbilityHolder;
        if (!ValidateProjectileSetup(ability)) yield break;

        Transform playerTransform = Context.AbilityController.transform;
        ProjectileData projectileData = InitializeProjectile(ability, playerTransform);

        yield return ProjectileMovementLoop(ability, projectileData);
    }

    // Data structure to track projectile state and optimization info
    private struct ProjectileData
    {
        public Vector3 direction; // Which way the projectile is moving
        public Vector3 lastPosition; // Last known position (for stuck detection)
        public float startTime; // When projectile was launched
        public float endTime; // When projectile should expire
        public GameObject hitTarget; // What the projectile hit (if anything)
        public bool useAdaptiveOptimization; // Whether to adjust optimization dynamically
        public float totalDistance; // Total distance this projectile will travel
        public int frameCount; // How many frames this projectile has existed
    }

    // Validate that projectile setup is correct
    private bool ValidateProjectileSetup(AbilityHolder ability)
    {
        if (ability?.abilityEffect == null)
        {
            Debug.LogError("Invalid ability setup in ProjectileLaunchRoutine");
            TriggerStateTransition();
            return false;
        }

        Transform playerTransform = Context.AbilityController?.transform;
        if (playerTransform == null)
        {
            Debug.LogError("Player transform is null in ProjectileLaunchRoutine");
            TriggerStateTransition();
            return false;
        }

        return true;
    }

    // Initialize projectile data and settings
    private ProjectileData InitializeProjectile(AbilityHolder ability, Transform playerTransform)
    {
        // Initialize target transform with cached version
        if (Context.targetTransform == null)
            Context.targetTransform = GetTargetTransform(playerTransform);

        ability.targetTransform = Context.targetTransform;

        return new ProjectileData
        {
            direction = GetCachedLaunchDirection(ability),
            lastPosition = ability.targetTransform.position,
            startTime = Time.time,
            endTime = Time.time + ability.abilityEffect.lifeSpan,
            hitTarget = null,
            useAdaptiveOptimization = _currentProfile.useAdaptiveOptimization,
            totalDistance = ability.abilityEffect.speed * ability.abilityEffect.lifeSpan,
            frameCount = 0
        };
    }

    // Main projectile movement and collision loop
    private IEnumerator ProjectileMovementLoop(AbilityHolder ability, ProjectileData data)
    {
        while (Time.time < data.endTime)
        {
            data.frameCount++; // Track how many frames this projectile has lived
            float deltaTime = Time.deltaTime;

            // Early exit checks
            if (!ValidateProjectileState(ability)) break;

            // FRAME SKIPPING LOGIC - This is the core optimization
            // Skip processing this frame if conditions are met
            if (ShouldSkipFrame(data.frameCount, data.endTime - Time.time))
            {
                yield return null; // Still yield control to Unity but skip all processing
                continue;
            }

            // Dynamic optimization updates - adjust settings based on current conditions
            if (data.useAdaptiveOptimization)
                UpdateDynamicOptimizations(data.endTime - Time.time, ability);

            // Movement and collision processing
            ProcessProjectileMovement(ability, data, deltaTime);

            // Collision checking - only check if enough time has passed
            if (ShouldCheckCollisions())
            {
                data.hitTarget = PerformCollisionCheck(ability, data.hitTarget);
                _lastCollisionCheckTime = Time.time;

                // If we hit something, handle the impact and exit
                if (data.hitTarget != null)
                {
                    yield return HandleProjectileImpact(ability, data.hitTarget);
                    yield break;
                }
            }

            // Stuck detection and handling - prevent infinite loops
            if (DetectStuckProjectile(ability.targetTransform.position, data.lastPosition))
            {
                yield return HandleStuckProjectile(ability);
                yield break;
            }

            data.lastPosition = ability.targetTransform.position;
            yield return null; // Yield control back to Unity for next frame
        }

        // Projectile expired naturally (ran out of time)
        yield return HandleProjectileExpiration(ability);
    }

    // Validate that projectile is still in a valid state
    private bool ValidateProjectileState(AbilityHolder ability)
    {
        return ability.targetTransform != null && Context.instantiatedParticle != null;
    }

    // Process projectile movement and update positions
    private void ProcessProjectileMovement(AbilityHolder ability, ProjectileData data, float deltaTime)
    {
        MoveProjectile(ability, data.direction, deltaTime);
        UpdateParticlePosition(ability.targetTransform.position);

        // Update travel distance for optimization decisions
        _totalTravelDistance += Vector3.Distance(ability.targetTransform.position, data.lastPosition);
    }

    // Get cached launch direction to avoid expensive calculations every frame
    private Vector3 GetCachedLaunchDirection(AbilityHolder ability)
    {
        // Enhanced caching with invalidation checks
        // Only recalculate if cache is old or invalid
        bool shouldRefreshCache = Time.time - _lastCacheTime > CACHE_REFRESH_INTERVAL ||
                                  _cachedLaunchDirection == Vector3.zero ||
                                  _cachedCamera == null;

        if (shouldRefreshCache)
        {
            _cachedLaunchDirection = CalculateOptimizedLaunchDirection(ability);
            _lastCacheTime = Time.time;
        }

        return _cachedLaunchDirection;
    }

    // Calculate the direction the projectile should travel
    private Vector3 CalculateOptimizedLaunchDirection(AbilityHolder ability)
    {
        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
            if (_cachedCamera == null) return Vector3.forward;
        }

        Vector3 cameraForward = _cachedCamera.transform.forward;

        // Enhanced direction calculation based on ability type
        if (ability.abilityEffect.isGroundFixedPosition)
        {
            cameraForward.y = 0f; // Remove vertical component for ground abilities
        }
        else
        {
            // Dynamic vertical influence based on ability properties
            float verticalInfluence = CalculateVerticalInfluence(ability);
            cameraForward.y *= verticalInfluence;
        }

        return cameraForward.normalized;
    }

    // Calculate how much vertical angle to apply based on ability characteristics
    private float CalculateVerticalInfluence(AbilityHolder ability)
    {
        // Base influence
        float influence = 0.33f;

        // Adjust based on ability properties
        if (ability.abilityEffect.shouldLaunch && ability.abilityEffect.speed > 30f)
            influence *= 0.7f; // Reduce for fast projectiles (they don't need much arc)

        if (ability.abilityEffect.lifeSpan > 5f)
            influence *= 1.2f; // Increase for long-range abilities (more arc helps reach far targets)

        return Mathf.Clamp(influence, 0.1f, 0.6f);
    }

    // CORE FRAME SKIPPING LOGIC
    // This determines whether to skip processing the current frame to save CPU time
    private bool ShouldSkipFrame(int frameCount, float remainingTime)
    {
        // No skipping for precision abilities - they need every frame processed
        if (_currentProfile.type == AbilityProfileType.Precision)
            return false;

        // Performance-based skipping - if performance is bad, skip more aggressively
        if (_performanceScore < 0.7f) // Performance is below 70%
        {
            // Skip more frames when performance is poor
            return frameCount % (_frameSkipInterval * 2) != 0;
        }

        // Standard frame skipping logic based on profile settings
        // frameCount % _frameSkipInterval gives us a pattern:
        // If _frameSkipInterval = 1: 0, 1, 2, 3, 4... % 1 = 0, 0, 0, 0, 0... (never skip)
        // If _frameSkipInterval = 2: 0, 1, 2, 3, 4... % 2 = 0, 1, 0, 1, 0... (skip every other)
        // If _frameSkipInterval = 3: 0, 1, 2, 3, 4... % 3 = 0, 1, 2, 0, 1... (process every 3rd)
        if (frameCount % _frameSkipInterval != 0)
            return true; // Skip this frame

        // Distance-based optimization for long-range abilities
        if (_currentProfile.type == AbilityProfileType.LongRange)
        {
            return ShouldSkipFrameForLongRange(frameCount, remainingTime);
        }

        return false; // Don't skip this frame
    }

    // Additional frame skipping logic specifically for long-range abilities
    private bool ShouldSkipFrameForLongRange(int frameCount, float remainingTime)
    {
        // More aggressive skipping when far from target and lots of time remaining
        if (_totalTravelDistance > _maxOptimizationDistance * 0.3f && remainingTime > 3f)
        {
            // Skip even more frames when we're far away and have time
            return frameCount % (_frameSkipInterval * 3) != 0;
        }

        // Moderate skipping when we have time remaining
        if (remainingTime > 3f)
        {
            return frameCount % (_frameSkipInterval * 2) != 0;
        }

        return false; // Don't skip when close to expiration
    }

    // Check if enough time has passed to perform collision detection
    private bool ShouldCheckCollisions()
    {
        return Time.time - _lastCollisionCheckTime >= _collisionCheckInterval;
    }

    // DYNAMIC OPTIMIZATION SYSTEM
    // Adjusts optimization settings based on current conditions
    private void UpdateDynamicOptimizations(float remainingTime, AbilityHolder ability)
    {
        float remainingTimeRatio = remainingTime / ability.abilityEffect.lifeSpan;
        float speedFactor = Mathf.Clamp01(ability.abilityEffect.speed / 100f);

        // Dynamic collision interval adjustment - check less often when far from target
        AdjustCollisionInterval(remainingTimeRatio, speedFactor);

        // Dynamic movement threshold adjustment - allow bigger movements when far away
        AdjustMovementThresholds(remainingTimeRatio);

        // Performance score adjustment - track how well we're performing
        UpdatePerformanceScore();
    }

    // Adjust how often we check for collisions based on projectile state
    private void AdjustCollisionInterval(float remainingTimeRatio, float speedFactor)
    {
        switch (_currentProfile.type)
        {
            case AbilityProfileType.LongRange:
                // Check more frequently as we approach the target (less remaining time)
                _collisionCheckInterval = Mathf.Lerp(0.005f, 0.05f, 1f - remainingTimeRatio);
                break;
            case AbilityProfileType.HighSpeed:
                // Fast projectiles need frequent checks when they have time and speed
                _collisionCheckInterval = Mathf.Lerp(0.005f, 0.02f, speedFactor * remainingTimeRatio);
                break;
            case AbilityProfileType.Heavyweight:
                // Heavy abilities can afford less frequent checks, more so over time
                _collisionCheckInterval = Mathf.Lerp(0.02f, 0.04f, 1f - remainingTimeRatio);
                break;
        }
    }

    // Adjust movement thresholds - how much movement is considered "significant"
    private void AdjustMovementThresholds(float remainingTimeRatio)
    {
        // Allow larger movements when projectile has more time remaining
        // Small movements matter more as we approach the target
        _movementThreshold = _currentProfile.movementThreshold * (1f + remainingTimeRatio * 0.5f);
        _particleMovementThreshold = _movementThreshold * 0.4f;
    }

    // Track performance and adjust optimization aggressiveness
    private void UpdatePerformanceScore()
    {
        // Simple performance scoring based on frame time
        float currentFrameTime = Time.deltaTime;
        float targetFrameTime = 1f / 60f; // 60 FPS target

        // Score is ratio of target time vs actual time
        // 1.0 = perfect 60fps, 0.5 = 30fps, 2.0 = 120fps
        float frameScore = Mathf.Clamp01(targetFrameTime / currentFrameTime);

        // Smoothly blend the new score with the old one (moving average)
        _performanceScore = Mathf.Lerp(_performanceScore, frameScore, 0.1f);
    }

    // Move the projectile forward based on its direction and speed
    private void MoveProjectile(AbilityHolder ability, Vector3 direction, float deltaTime)
    {
        if (ability?.targetTransform == null) return;

        Vector3 movement = direction * ability.abilityEffect.speed * deltaTime;
        ability.targetTransform.position += movement;
    }

    // Update particle system position (visual effect) only when movement is significant
    private void UpdateParticlePosition(Vector3 newPosition)
    {
        if (Context.instantiatedParticle == null) return;

        // Only update particle position if movement is significant enough to see
        if (HasSignificantMovement(newPosition, _lastParticlePosition, _particleMovementThreshold))
        {
            Context.instantiatedParticle.transform.position = newPosition;
            _lastParticlePosition = newPosition;
        }
    }

    // Check if movement between two positions is significant enough to warrant an update
    private bool HasSignificantMovement(Vector3 newPos, Vector3 oldPos, float threshold = -1f)
    {
        float thresholdToUse = threshold < 0 ? _movementThreshold : threshold;
        return Vector3.Distance(newPos, oldPos) > thresholdToUse;
    }

    // Perform collision detection for projectile
    private GameObject PerformCollisionCheck(AbilityHolder ability, GameObject previousHit)
    {
        if (previousHit != null) return previousHit; // Already hit something

        try
        {
            return CheckCollisionsOptimized(ability);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error in collision detection: {ex.Message}");
            return null;
        }
    }

    // Optimized collision checking using Unity's NonAlloc methods
    private GameObject CheckCollisionsOptimized(AbilityHolder ability)
    {
        if (ability?.targetTransform == null || Context.attackCast == null)
            return null;

        Vector3 position = ability.targetTransform.position;
        int hitCount = GetCollisionHits(position, ability.targetTransform.rotation);

        return ProcessCollisionResults(hitCount);
    }

    private int GetCollisionHits(Vector3 position, Quaternion rotation)
    {
        return Context.attackCast.castType switch
        {
            AttackCast.CastType.Sphere => Physics.OverlapSphereNonAlloc(
                position,
                Context.attackCast.castSize,
                _collisionResults,
                Context.attackCast.targetLayers
            ),
            AttackCast.CastType.Box => Physics.OverlapBoxNonAlloc(
                position,
                Context.attackCast.boxSize * 0.5f,
                _collisionResults,
                rotation,
                Context.attackCast.targetLayers
            ),
            _ => 0
        };
    }

    private GameObject ProcessCollisionResults(int hitCount)
    {
        GameObject abilityControllerObject = Context.AbilityController.gameObject;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _collisionResults[i];
            if (hit != null && hit.gameObject != abilityControllerObject)
            {
                return hit.gameObject;
            }
        }

        return null;
    }

    private void TrackPositionHistory(Vector3 position)
    {
        _positionHistory.Enqueue(position);

        if (_positionHistory.Count > 8)
            _positionHistory.Dequeue();
    }

    private bool DetectStuckProjectile(Vector3 currentPos, Vector3 lastPos)
    {
        if (Vector3.Distance(currentPos, lastPos) < _movementThreshold * 0.1f)
        {
            _stuckFrameCount++;
            return _stuckFrameCount > MAX_STUCK_FRAMES;
        }

        _stuckFrameCount = 0;
        return false;
    }

    private IEnumerator HandleStuckProjectile(AbilityHolder ability)
    {
        Debug.LogWarning("Projectile appears to be stuck, forcing completion");

        // Small delay to allow for natural resolution
        yield return new WaitForSeconds(0.1f);

        HandleProjectileExpiration(ability);
    }

    private IEnumerator HandleProjectileImpact(AbilityHolder ability, GameObject hitTarget)
    {
        CleanupParticle();

        if (ability?.targetTransform != null)
            Context.targetTransform = ability.targetTransform;

        // Determine effect application based on ability type
        if (ability?.abilityEffect?.multiAreaEffect == true)
            ApplyAbilityUse();
        else
            ApplyAbilityUse(hitTarget);

        TriggerStateTransition();
        yield break;
    }

    private IEnumerator HandleProjectileExpiration(AbilityHolder ability)
    {
        CleanupParticle();

        if (ability?.targetTransform != null)
            Context.targetTransform = ability.targetTransform;

        ApplyAbilityUse();
        TriggerStateTransition();
        yield break;
    }

    private void CleanupParticle()
    {
        if (Context.instantiatedParticle != null)
        {
            Object.Destroy(Context.instantiatedParticle);
            Context.instantiatedParticle = null;
        }
    }

    private void FinalizeAbility()
    {
        ApplyAbilityUse();
        TriggerStateTransition();
    }

    private void TriggerStateTransition() => _goToNextState = true;

    // Legacy method - marked obsolete but maintained for compatibility
    [System.Obsolete("Use optimized launch routines instead - this method has performance issues")]
    public virtual IEnumerator UntilReachesPosition()
    {
        AbilityHolder ability = Context.AbilityHolder;
        float startTime = Time.time;

        if (ability.abilityEffect.shouldLaunch)
        {
            Vector3 startPosition = Context.AbilityController.transform.position;
            Vector3 targetPosition = Context.targetTransform.transform.position;
            float journeyLength = Vector3.Distance(startPosition, targetPosition);
            Transform newPlayerTransform = GetTargetTransform(Context.AbilityController.transform);

            while (Time.time < startTime + ability.abilityEffect.finalLaunchTime)
            {
                float distCovered = (Time.time - startTime) * ability.abilityEffect.speed;
                float fracJourney = distCovered / journeyLength;
                newPlayerTransform.transform.position = Vector3.Lerp(startPosition, targetPosition, fracJourney);
                Context.targetTransform = newPlayerTransform.transform;

                if (Context.instantiatedParticle != null)
                    Context.instantiatedParticle.transform.position = newPlayerTransform.transform.position;

                yield return null;
            }
            Context.targetTransform = newPlayerTransform.transform;
        }
    }
}