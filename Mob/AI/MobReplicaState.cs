using System;
using UnityEngine;
using EState = MobMovementStateMachine.EMobMovementState;

/// <summary>Flags carried by <see cref="MobReplicaState"/>.</summary>
[Flags]
public enum MobReplicaFlags : byte
{
    None = 0,
    /// <summary>The AI is fighting (combat idle / strafing animations).</summary>
    InCombat = 1 << 0,
    /// <summary>Stunned or knocked down.</summary>
    Stunned = 1 << 1,
    /// <summary>Dead (death animation, colliders off).</summary>
    Dead = 1 << 2,
    /// <summary>Running away.</summary>
    Fleeing = 1 << 3,
}

/// <summary>
/// What a machine that does not run a mob's AI needs in order to show it: the AI state, a few flags and the mob's
/// walking and running speed (to scale its movement animations). The server captures it from the running AI
/// (<see cref="MobMovementStateMachine.CaptureReplicaState"/>); the network layer sends it and applies it to the
/// client's copy (<see cref="MobMovementStateMachine.ApplyReplicaState"/>). Position and rotation travel separately.
/// </summary>
/// <remarks>
/// Only public fields, so the network layer can send it as it is. Speeds are rounded to <see cref="SpeedStep"/> so
/// tiny speed changes do not cause a resend.
/// </remarks>
[Serializable]
public struct MobReplicaState : IEquatable<MobReplicaState>
{
    /// <summary>Speeds are rounded to this (metres per second).</summary>
    public const float SpeedStep = 0.05f;

    /// <summary>The AI state (<see cref="MobMovementStateMachine.EMobMovementState"/>).</summary>
    public byte state;
    /// <summary><see cref="MobReplicaFlags"/>.</summary>
    public byte flags;
    /// <summary>Walking speed (metres per second).</summary>
    public float walkSpeed;
    /// <summary>Running speed (metres per second).</summary>
    public float runSpeed;

    /// <summary>A state with speeds rounded to <see cref="SpeedStep"/> (negative and non-finite speeds become 0).</summary>
    public static MobReplicaState Create(EState aiState, MobReplicaFlags replicaFlags, float walk, float run)
    {
        return new MobReplicaState
        {
            state = (byte)aiState,
            flags = (byte)replicaFlags,
            walkSpeed = Quantize(walk),
            runSpeed = Quantize(run),
        };
    }

    /// <summary>The AI state as the enum.</summary>
    public EState State => (EState)state;

    /// <summary>The flags as the enum.</summary>
    public MobReplicaFlags Flags => (MobReplicaFlags)flags;

    /// <summary>Is <paramref name="flag"/> set?</summary>
    public bool Has(MobReplicaFlags flag) => (flags & (byte)flag) != 0;

    /// <summary>Rounds a speed to <see cref="SpeedStep"/>; negative, NaN and infinite values become 0, huge ones are capped.</summary>
    public static float Quantize(float speed)
    {
        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
            return 0f;
        return Mathf.Round(Mathf.Min(speed, 1000f) / SpeedStep) * SpeedStep;
    }

    public bool Equals(MobReplicaState other) =>
        state == other.state && flags == other.flags && walkSpeed == other.walkSpeed && runSpeed == other.runSpeed;

    public override bool Equals(object obj) => obj is MobReplicaState other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int h = state | (flags << 8);
            h = h * 397 ^ walkSpeed.GetHashCode();
            h = h * 397 ^ runSpeed.GetHashCode();
            return h;
        }
    }

    public static bool operator ==(MobReplicaState a, MobReplicaState b) => a.Equals(b);
    public static bool operator !=(MobReplicaState a, MobReplicaState b) => !a.Equals(b);

    public override string ToString() => $"{State} [{Flags}] walk {walkSpeed:0.##} run {runSpeed:0.##}";
}
