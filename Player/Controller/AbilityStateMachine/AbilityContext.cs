using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using System.Threading.Tasks;
public class AbilityContext
{
    private PlayerInput playerInput;
    private PlayerAnimationModel animationModel;
    private PlayerAbilityController abilityController;
    private PlayerAbilityHolder abilityHolder;
    private InputActionReference abilityActionReference;
    public bool cachedAvailability;

    public bool triggered;
    public Transform targetTransform;
    public Transform oldTransform;
    public GameObject instantiatedParticle;
    public bool abilityStartedActivating = false;


    public bool shouldHaveDelayedLaunchTime = false;
    public bool abilityStillInProgress = false;
    public bool isWaitingForClick = false;


    public bool isPermanentTargetOnCast = false;
    public AttackCast attackCast = null;




    public AbilityContext(PlayerInput playerInput, PlayerAnimationModel animationModel, PlayerAbilityController abilityController, PlayerAbilityHolder abilityHolder) 
    {

        this.playerInput = playerInput;
        this.animationModel = animationModel;
        this.abilityController = abilityController;
        this.abilityHolder = abilityHolder;
    }

    public event Action<bool> AvailabilityChanged;

    public void SetCachedAvailability(bool value)
    {
        if (cachedAvailability != value)
        {
            cachedAvailability = value;
            AvailabilityChanged?.Invoke(value);  // Broadcasts the new availability
        }
    }


    public PlayerInput PlayerInput => playerInput;
    public PlayerAnimationModel AnimationModel => animationModel;
    public PlayerAbilityController AbilityController => abilityController;
    public PlayerAbilityHolder AbilityHolder => abilityHolder;

    public InputActionReference AbilityActionReference { get => abilityActionReference; set => abilityActionReference = value; }







    public virtual async Task SetParticleDuration(GameObject instantiatedParticle, AbilityHolder ability, AttackCast attackCast = null)
    {
        ParticleSystem particleSystem = instantiatedParticle.GetComponent<ParticleSystem>();
        if (particleSystem.isPlaying)
        {
            particleSystem.Stop(true);
        }
        ParticleSystem.MainModule mainModule = particleSystem.main;

        // Set start delay and duration before starting the particle system
        mainModule.startDelay = 0;

        float duration;
        if (ability.abilityEffect.shouldLaunch)
            duration = ability.abilityEffect.lifeSpan;

        else if (ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.shouldMarkAtCast)
            duration = ability.abilityEffect.castDuration + ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;

        else duration = ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration;
        mainModule.startDelay = ability.abilityEffect.castDuration;


        mainModule.duration = duration;
        mainModule.startLifetime = duration;
        //  Set sub-particle system durations
        float subParticleDuration = ability.abilityEffect.shouldLaunch ? ability.abilityEffect.lifeSpan :
            ability.abilityEffect.isPartialPermanentTargetWhileCasting || ability.abilityEffect.shouldMarkAtCast ?
            ability.abilityEffect.finalLaunchTime + ability.abilityEffect.duration : ability.abilityEffect.duration;

        foreach (var particle in particleSystem.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule mainModuleSubParticle = particle.main;
            mainModuleSubParticle.startDelay = ability.abilityEffect.finalLaunchTime;
            mainModuleSubParticle.duration = subParticleDuration;
            mainModuleSubParticle.startLifetime = subParticleDuration - ability.abilityEffect.finalLaunchTime;
            if (ability.abilityEffect.subParticleShouldChangeSize)
            {
                if (attackCast.castType == AttackCast.CastType.Box)
                {
                    if (mainModuleSubParticle.startSizeX.constant < attackCast.boxSize.x && mainModuleSubParticle.startSizeZ.constant < attackCast.boxSize.z && mainModuleSubParticle.startSizeY.constant < attackCast.boxSize.y)
                        ChangeParticleSize(mainModuleSubParticle, attackCast);
                }
                else if (mainModuleSubParticle.startSizeX.constant < attackCast.castSize && mainModuleSubParticle.startSizeZ.constant < attackCast.castSize && mainModuleSubParticle.startSizeY.constant < attackCast.castSize)
                    ChangeParticleSize(mainModuleSubParticle, attackCast);
            }
        }
        if (ability.abilityEffect.particleShouldChangeSize) ChangeParticleSize(mainModule, attackCast);
        particleSystem.Play();
    }
#pragma warning restore CS1998
    private void ChangeParticleSize(ParticleSystem.MainModule particle, AttackCast attackCast = null)
    {
        if (attackCast != null)
        {
            float sizeX, sizeY, sizeZ;
            if (attackCast.castType == AttackCast.CastType.Sphere)
                sizeX = sizeY = sizeZ = attackCast.castSize;

            else
            {
                sizeX = attackCast.boxSize.x;
                sizeY = attackCast.boxSize.y;
                sizeZ = attackCast.boxSize.z;
            }

            particle.startSizeX = sizeX;
            particle.startSizeY = sizeY;
            particle.startSizeZ = sizeZ;
        }
    }



}

