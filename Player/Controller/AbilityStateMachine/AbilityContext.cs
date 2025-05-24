using System;
using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections.Generic;

public class AbilityContext : IDisposable
{
    // Core dependencies injected through constructor
    private PlayerInput playerInput;
    private PlayerAnimationModel animationModel;
    private PlayerAbilityController abilityController;
    private PlayerAbilityHolder abilityHolder;
    private InputActionReference abilityActionReference;

    // State management fields
    public bool cachedAvailability;
    public bool triggered; // Indicates if the ability has been triggered.
    public Transform targetTransform;
    public GameObject instantiatedParticle;
    public bool abilityStillInProgress = false;
    public bool isWaitingForClick = false;
    public AttackCast attackCast = null;

    // Memory optimization fields.
    private Transform cachedPlayerTransform; // Cached transform for optimization purposes.
    private readonly Dictionary<ParticleSystem, ParticleSystemConfig> particleConfigCache = new(); // Cache for particle configurations.
    private bool isDisposed = false; // Tracks whether the object has been disposed.

    private Vector3 lastPlayerPosition;
    private Quaternion lastPlayerRotation;

    // Struct to cache particle configurations and avoid repeated calculations
    private struct ParticleSystemConfig
    {
        public float duration;
        public float startDelay;
        public float startLifetime;
        public Vector3 startSize;
        public Vector3 originalSize; // Store original size for proportional scaling
        public bool isConfigured;
    }

    // Property accessors
    public PlayerInput PlayerInput => playerInput;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerAbilityController AbilityController => abilityController;
    public PlayerAbilityHolder AbilityHolder => abilityHolder;
    public InputActionReference AbilityActionReference { get => abilityActionReference; set => abilityActionReference = value; }

    public AbilityContext(PlayerInput playerInput, PlayerAnimationModel animationModel,
                          PlayerAbilityController abilityController, PlayerAbilityHolder abilityHolder)
    {
        this.playerInput = playerInput;
        this.animationModel = animationModel;
        this.abilityController = abilityController;
        this.abilityHolder = abilityHolder;

        // Initialize cached transform reference
        InitializeCachedTransform();
    }

    private void InitializeCachedTransform()
    {
        if (cachedPlayerTransform == null)
        {
            // Ensures that the transform is not destroyed when loading new scenes.
            GameObject transformCache = new GameObject("PlayerTransformCache");
            // Don't destroy on load to prevent recreation
            UnityEngine.Object.DontDestroyOnLoad(transformCache);
            cachedPlayerTransform = transformCache.transform;
        }
    }

    // Tracks changes to ability availability and raises an event when it changes.
    public event Action<bool> AvailabilityChanged;

    public void SetCachedAvailability(bool value)
    {
        if (cachedAvailability != value)
        {
            cachedAvailability = value;
            AvailabilityChanged?.Invoke(value); // Notifies subscribers of the change.
        }
    }

    // Configures and plays particle effects, leveraging cached configurations for optimization.
    public virtual void SetParticleDuration(GameObject instantiatedParticle, AbilityHolder ability, AttackCast attackCast = null)
    {
        if (instantiatedParticle == null || ability?.abilityEffect == null) return;

        ParticleSystem particleSystem = instantiatedParticle.GetComponent<ParticleSystem>();
        if (particleSystem == null) return;

        // Checks if the particle system configuration is cached, otherwise calculates it.
        if (!particleConfigCache.TryGetValue(particleSystem, out ParticleSystemConfig config) || !config.isConfigured)
        {
            config = CalculateParticleConfig(ability, attackCast, true, particleSystem);
            particleConfigCache[particleSystem] = config; // Caches the configuration for future use.
        }

        // Apply cached configuration
        ApplyParticleConfiguration(particleSystem, config, ability, attackCast, true);

        // Configure child particles with optimized loop
        ConfigureSubParticles(particleSystem, ability, attackCast);

        particleSystem.Play();
    }

    // Calculates particle configurations based on ability properties and context.
    private ParticleSystemConfig CalculateParticleConfig(AbilityHolder ability, AttackCast attackCast, bool isMainParticle, ParticleSystem particleSystem)
    {
        // Determines duration and delay for the main or sub-particles.
        float duration = isMainParticle ? CalculateMainParticleDuration(ability) : CalculateSubParticleDuration(ability);
        float startDelay = isMainParticle ? ability.abilityEffect.castDuration : ability.abilityEffect.finalLaunchTime;

        // Store original size for proportional scaling
        var mainModule = particleSystem.main;
        Vector3 originalSize = new Vector3(
            mainModule.startSizeX.constant,
            mainModule.startSizeY.constant,
            mainModule.startSizeZ.constant
        );

        return new ParticleSystemConfig
        {
            duration = duration,
            startDelay = startDelay,
            startLifetime = isMainParticle ? duration : duration - startDelay,
            startSize = CalculateParticleSize(ability, attackCast),
            originalSize = originalSize,
            isConfigured = true
        };
    }

    private Vector3 CalculateParticleSize(AbilityHolder ability, AttackCast attackCast)
    {
        if (attackCast == null) return Vector3.one;

        return attackCast.castType == AttackCast.CastType.Sphere
            ? Vector3.one * attackCast.castSize
            : attackCast.boxSize;
    }

    private void ApplyParticleConfiguration(ParticleSystem particleSystem, ParticleSystemConfig config, AbilityHolder ability, AttackCast attackCast, bool isMainParticle)
    {
        // Stop only if playing to avoid unnecessary operations
        if (particleSystem.isPlaying)
            particleSystem.Stop(true);

        var mainModule = particleSystem.main;
        mainModule.startDelay = config.startDelay;
        mainModule.duration = config.duration;
        mainModule.startLifetime = config.startLifetime;

        // Apply size changes based on ability settings
        bool shouldChangeSize = false;

        if (isMainParticle)
        {
            // Main particle follows particleShouldChangeSize setting
            shouldChangeSize = ability.abilityEffect.particleShouldChangeSize;
        }
        else
        {
            // Sub-particles: check OnlyPrincipal setting and subParticleShouldChangeSize
            if (ability.abilityEffect.onlyPrincipalParticle)
            {
                shouldChangeSize = false; // Only main particle should change size
            }
            else
            {
                shouldChangeSize = ability.abilityEffect.subParticleShouldChangeSize &&
                                 ShouldChangeSubParticleSize(mainModule, attackCast);
            }
        }

        if (shouldChangeSize && attackCast != null)
        {
            Vector3 proportionalSize = CalculateProportionalSize(config.originalSize, config.startSize);
            ApplyParticleSize(mainModule, proportionalSize);
        }
    }

    private Vector3 CalculateProportionalSize(Vector3 originalSize, Vector3 targetSize)
    {
        // Calculate scaling factors for each axis to maintain proportions
        float scaleX = originalSize.x > 0 ? targetSize.x / originalSize.x : 1f;
        float scaleY = originalSize.y > 0 ? targetSize.y / originalSize.y : 1f;
        float scaleZ = originalSize.z > 0 ? targetSize.z / originalSize.z : 1f;

        // Use the largest scale factor to maintain proportions
        float maxScale = Mathf.Max(scaleX, scaleY, scaleZ);

        return new Vector3(
            originalSize.x * maxScale,
            originalSize.y * maxScale,
            originalSize.z * maxScale
        );
    }

    private void ApplyParticleSize(ParticleSystem.MainModule mainModule, Vector3 size)
    {
        mainModule.startSizeX = size.x;
        mainModule.startSizeY = size.y;
        mainModule.startSizeZ = size.z;
    }

    private void ConfigureSubParticles(ParticleSystem mainParticle, AbilityHolder ability, AttackCast attackCast)
    {
        // Skip sub-particles if OnlyPrincipal is true
        if (ability.abilityEffect.onlyPrincipalParticle) return;

        // Get all child particles once to avoid repeated GetComponent calls
        ParticleSystem[] childParticles = mainParticle.GetComponentsInChildren<ParticleSystem>();

        for (int i = 0; i < childParticles.Length; i++)
        {
            if (childParticles[i] == mainParticle) continue; // Skip main particle

            ConfigureSubParticle(childParticles[i], ability, attackCast);
        }
    }

    private void ConfigureSubParticle(ParticleSystem particle, AbilityHolder ability, AttackCast attackCast)
    {
        // Use cached configuration for sub-particles too
        if (!particleConfigCache.TryGetValue(particle, out ParticleSystemConfig config) || !config.isConfigured)
        {
            config = CalculateParticleConfig(ability, attackCast, false, particle);
            particleConfigCache[particle] = config;
        }

        ApplyParticleConfiguration(particle, config, ability, attackCast, false);
    }

    private bool ShouldChangeSubParticleSize(ParticleSystem.MainModule mainModule, AttackCast attackCast)
    {
        if (attackCast == null) return false;

        return attackCast.castType switch
        {
            AttackCast.CastType.Box => mainModule.startSizeX.constant < attackCast.boxSize.x &&
                                      mainModule.startSizeZ.constant < attackCast.boxSize.z &&
                                      mainModule.startSizeY.constant < attackCast.boxSize.y,
            AttackCast.CastType.Sphere => mainModule.startSizeX.constant < attackCast.castSize &&
                                          mainModule.startSizeZ.constant < attackCast.castSize &&
                                          mainModule.startSizeY.constant < attackCast.castSize,
            _ => false
        };
    }

    private float CalculateMainParticleDuration(AbilityHolder ability)
    {
        if (ability.abilityEffect.shouldLaunch)
            return ability.abilityEffect.lifeSpan;

        if (ability.abilityEffect.isPartialPermanentTargetWhileCasting ||
            ability.abilityEffect.shouldMarkAtCast)
            return ability.abilityEffect.castDuration +
                   ability.abilityEffect.finalLaunchTime +
                   ability.abilityEffect.duration;

        return ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
    }

    private float CalculateSubParticleDuration(AbilityHolder ability)
    {
        return ability.abilityEffect.shouldLaunch
            ? ability.abilityEffect.lifeSpan
            : ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
    }

    // Optimized transform handling - reuse cached transform instead of creating GameObjects
    public Transform GetCachedPlayerTransform(Transform sourceTransform)
    {
        if (cachedPlayerTransform == null)
        {
            InitializeCachedTransform();
        }

        // Null check for sourceTransform
        if (sourceTransform == null)
        {
            Debug.LogWarning("GetCachedPlayerTransform called with null sourceTransform");
            return cachedPlayerTransform;
        }

        // Only update if position or rotation actually changed
        if (lastPlayerPosition != sourceTransform.position || lastPlayerRotation != sourceTransform.rotation)
        {
            cachedPlayerTransform.position = sourceTransform.position;
            cachedPlayerTransform.rotation = sourceTransform.rotation;
            lastPlayerPosition = sourceTransform.position;
            lastPlayerRotation = sourceTransform.rotation;
        }

        return cachedPlayerTransform;
    }

    // Cleans up allocated resources, including cached transforms and particles.
    public void Dispose()
    {
        if (isDisposed) return;

        // Clear particle cache
        particleConfigCache.Clear(); // Clears cached particle configurations.

        // Cleanup cached transform
        if (cachedPlayerTransform != null)
        {
            UnityEngine.Object.Destroy(cachedPlayerTransform.gameObject); // Destroys the cached transform object.
            cachedPlayerTransform = null;
        }

        // Cleanup instantiated particle
        if (instantiatedParticle != null)
        {
            UnityEngine.Object.Destroy(instantiatedParticle); // Ensures particle objects are destroyed to prevent memory leaks.
            instantiatedParticle = null;
        }

        // Clear event subscriptions to prevent memory leaks
        AvailabilityChanged = null; // Removes event subscribers to avoid memory leaks.
        isDisposed = true; // Marks the object as disposed to prevent further use.
    }

    // Finalizer ensures Dispose is called if it wasn't explicitly invoked.
    ~AbilityContext()
    {
        Dispose();
    }
}