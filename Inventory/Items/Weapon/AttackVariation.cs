using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
[System.Serializable]
public class AttackVariation : IAttackComponent
{
    [Header("Variation Configuration")]
    public string variationName;


    [Header("Animation & Timing")]
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private float animationSpeed = 1.0f;
    [SerializeField] private float startupFrames = 0.1f;
    [SerializeField] private float activeFrames = 0.2f;
    [SerializeField] private float recoveryFrames = 0.3f;

    [Header("Combat Properties")]
    public float StaminaCost => staminaCost;
    private float staminaCost;

    [SerializeField] private bool lockMovement = false;
    [SerializeField] private float movementSpeedMultiplier = 1.0f;
    [SerializeField] private Vector3 forwardMovement = Vector3.zero;

    [Header("Effects")]
    [SerializeField] private List<AttackActionEffect> effects = new List<AttackActionEffect>();

    [Header("Audio & Visual")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField] private ParticleSystem attackParticles;
    [SerializeField] private GameObject trailEffect;

    // IAttackComponent implementation
    public float StartupFrames => startupFrames;
    public float ActiveFrames => activeFrames;
    public float RecoveryFrames => recoveryFrames;
    public float AnimationSpeed => animationSpeed;
    public bool LockMovement => lockMovement;
    public float MovementSpeedMultiplier => movementSpeedMultiplier;
    public Vector3 ForwardMovement => forwardMovement;
    public AnimationClip AnimationClip => animationClip;
    public AudioClip AttackSound => attackSound;
    public ParticleSystem AttackParticles => attackParticles;
    public GameObject TrailEffect => trailEffect;
    public List<AttackActionEffect> Effects => effects;

    public float GetTotalDuration()
    {
        return (startupFrames + activeFrames + recoveryFrames) / animationSpeed;
    }

    public bool IsInActiveFrames(float normalizedTime)
    {
        float startTime = startupFrames / GetTotalDuration();
        float endTime = (startupFrames + activeFrames) / GetTotalDuration();
        return normalizedTime >= startTime && normalizedTime <= endTime;
    }

    public bool IsInRecoveryFrames(float normalizedTime)
    {
        float recoveryStart = (startupFrames + activeFrames) / GetTotalDuration();
        return normalizedTime >= recoveryStart;
    }
}
namespace Assets.Scripts.Inventory.Items.Weapon
{
    internal class AttackVariation
    {
    }
}
