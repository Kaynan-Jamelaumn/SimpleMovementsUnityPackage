using System;
using UnityEngine.InputSystem;
using UnityEngine;

public class AbilityContext
{
    // Core dependencies injected through constructor
    private PlayerInput playerInput;
    private PlayerAnimationModel animationModel;
    private PlayerAbilityController abilityController;
    private PlayerAbilityHolder abilityHolder;
    private InputActionReference abilityActionReference;

    // State management fields
    public bool cachedAvailability;
    public bool triggered;
    public Transform targetTransform;
    public Transform oldTransform;
    public GameObject instantiatedParticle;
    public bool abilityStillInProgress = false;
    public bool isWaitingForClick = false;
    public AttackCast attackCast = null;

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
    }

    // Event for tracking availability changes
    public event Action<bool> AvailabilityChanged;

    public void SetCachedAvailability(bool value)
    {
        if (cachedAvailability != value)
        {
            cachedAvailability = value;
            AvailabilityChanged?.Invoke(value);
        }
    }

    // Main particle configuration entry point
    public virtual void SetParticleDuration(GameObject instantiatedParticle, AbilityHolder ability, AttackCast attackCast = null)
    {
        ParticleSystem particleSystem = instantiatedParticle.GetComponent<ParticleSystem>();

        // Configure main particle first
        StopAndConfigureParticleSystem(particleSystem, ability, attackCast);

        // Then configure all child particles
        ConfigureSubParticles(particleSystem, ability, attackCast);

        particleSystem.Play();
    }

    private void StopAndConfigureParticleSystem(ParticleSystem particleSystem, AbilityHolder ability, AttackCast attackCast)
    {
        // Ensure particle system is stopped before reconfiguration
        if (particleSystem.isPlaying)
            particleSystem.Stop(true);

        var mainModule = particleSystem.main;

        // Calculate duration based on multiple ability flags
        float duration = CalculateMainParticleDuration(ability);

        // Set core timing properties
        mainModule.startDelay = ability.abilityEffect.castDuration;
        mainModule.duration = duration;
        mainModule.startLifetime = duration;

        // Handle main particle size changes if required
        if (ability.abilityEffect.particleShouldChangeSize)
        {
            ChangeParticleSize(mainModule, attackCast);
        }
    }

    private float CalculateMainParticleDuration(AbilityHolder ability)
    {
        // Complex duration logic based on ability type:
        // - Launch abilities use lifespan
        // - Casting/marking abilities use sum of multiple timings
        // - Default to launch time + duration
        if (ability.abilityEffect.shouldLaunch)
            return ability.abilityEffect.lifeSpan;

        if (ability.abilityEffect.isPartialPermanentTargetWhileCasting ||
            ability.abilityEffect.shouldMarkAtCast)
            return ability.abilityEffect.castDuration +
                   ability.abilityEffect.finalLaunchTime +
                   ability.abilityEffect.duration;

        return ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
    }

    private void ConfigureSubParticles(ParticleSystem mainParticle, AbilityHolder ability, AttackCast attackCast)
    {
        // Process all child particle systems recursively
        foreach (var particle in mainParticle.GetComponentsInChildren<ParticleSystem>())
        {
            ConfigureSubParticle(particle, ability, attackCast);
        }
    }

    private void ConfigureSubParticle(ParticleSystem particle, AbilityHolder ability, AttackCast attackCast)
    {
        var mainModule = particle.main;

        // Sub-particles have different duration calculations
        float subDuration = CalculateSubParticleDuration(ability);

        // Sub-particle timing relative to main particle
        mainModule.startDelay = ability.abilityEffect.finalLaunchTime;
        mainModule.duration = subDuration;

        // Get the constant value from startDelay if it's using constant mode
        float startDelayValue = mainModule.startDelay.mode == ParticleSystemCurveMode.Constant
            ? mainModule.startDelay.constant
            : 0f;

        mainModule.startLifetime = subDuration - startDelayValue;

        // Conditional size changes for sub-particles
        if (ability.abilityEffect.subParticleShouldChangeSize && attackCast != null)
        {
            HandleSubParticleSizeChange(mainModule, attackCast);
        }
    }

    private float CalculateSubParticleDuration(AbilityHolder ability)
    {
        // Simplified duration for sub-particles:
        // Either lifespan for launched abilities
        // or launch time + duration for others
        return ability.abilityEffect.shouldLaunch
            ? ability.abilityEffect.lifeSpan
            : ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
    }

    private void HandleSubParticleSizeChange(ParticleSystem.MainModule mainModule, AttackCast attackCast)
    {
        // Size change conditions based on cast type:
        // - Box: Check against all dimensions
        // - Sphere: Uniform size check
        bool shouldChangeSize = attackCast.castType switch
        {
            AttackCast.CastType.Box => mainModule.startSizeX.constant < attackCast.boxSize.x &&
                                      mainModule.startSizeZ.constant < attackCast.boxSize.z &&
                                      mainModule.startSizeY.constant < attackCast.boxSize.y,
            AttackCast.CastType.Sphere => mainModule.startSizeX.constant < attackCast.castSize &&
                                          mainModule.startSizeZ.constant < attackCast.castSize &&
                                          mainModule.startSizeY.constant < attackCast.castSize,
            _ => false
        };

        if (shouldChangeSize)
        {
            ChangeParticleSize(mainModule, attackCast);
        }
    }

    private void ChangeParticleSize(ParticleSystem.MainModule particle, AttackCast attackCast = null)
    {
        if (attackCast != null)
        {
            float sizeX, sizeY, sizeZ;

            // Determine size parameters based on attack type:
            // - Sphere uses uniform size
            // - Box uses individual dimensions
            if (attackCast.castType == AttackCast.CastType.Sphere)
            {
                sizeX = sizeY = sizeZ = attackCast.castSize;
            }
            else
            {
                sizeX = attackCast.boxSize.x;
                sizeY = attackCast.boxSize.y;
                sizeZ = attackCast.boxSize.z;
            }

            // Apply new size values to all dimensions
            particle.startSizeX = sizeX;
            particle.startSizeY = sizeY;
            particle.startSizeZ = sizeZ;
        }
    }


}