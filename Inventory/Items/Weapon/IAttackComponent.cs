using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-only view of an attack's settings (implemented by <see cref="AttackComponent"/>).</summary>
public interface IAttackComponent
{
    float GetTotalDuration();
    bool IsInActiveFrames(float normalizedTime);
    bool IsInRecoveryFrames(float normalizedTime);
    float StartupFrames { get; }
    float ActiveFrames { get; }
    float RecoveryFrames { get; }
    float AnimationSpeed { get; }
    float StaminaCost { get; }
    bool LockMovement { get; }
    float MovementSpeedMultiplier { get; }
    Vector3 ForwardMovement { get; }
    AnimationClip AnimationClip { get; }
    AudioClip AttackSound { get; }
    ParticleSystem AttackParticles { get; }
    GameObject TrailEffect { get; }
    List<AttackActionEffect> Effects { get; }
}
