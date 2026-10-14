using System;
using UnityEngine;

/// <summary>What the player does in water when no up/down control is held.</summary>
public enum SwimBuoyancy
{
    /// <summary>Rises back to the surface and floats there, head out of the water.</summary>
    Floats,
    /// <summary>Stays at the depth it is at (Subnautica): it only goes up or down when you swim there.</summary>
    Neutral,
    /// <summary>Slowly sinks to the bottom.</summary>
    Sinks,
}

/// <summary>
/// Swimming settings of the player (<see cref="PlayerMovementModel.Swimming"/>). The defaults play like Subnautica:
/// <list type="bullet">
/// <item>underwater you swim <b>where the camera looks</b> (look down and press forward to dive, look up to rise);</item>
/// <item>Jump held swims straight up, Crouch held straight down, the side keys strafe;</item>
/// <item>with no key held you <b>stay at your depth</b> and glide to a stop (water drag), you never walk on the floor;</item>
/// <item>at the surface you float with your head out, and looking up does not lift you out of the water;</item>
/// <item>you breathe at the surface and use oxygen with your head under (the OxygenManager).</item>
/// </list>
/// In water deeper than <see cref="minWaterDepth"/>, once the feet are <see cref="enterDepth"/> under the surface, the
/// player swims; in shallower water they wade. Jump pressed at the surface while moving hops out (onto a bank).
/// </summary>
[Serializable]
public class SwimSettings
{
    [Tooltip("The player can swim. Off = deep water is walked through like air (old behaviour).")]
    public bool enabled = true;

    [Header("When to swim")]
    [Tooltip("Water shallower than this (surface to bottom, metres) is waded through, never swum.")]
    [Min(0.5f)] public float minWaterDepth = 1.4f;
    [Tooltip("The player starts swimming once its feet are this deep under the surface (metres).")]
    [Min(0.1f)] public float enterDepth = 1.1f;
    [Tooltip("The player stops swimming (stands, walks out) once its feet are less deep than this (metres).")]
    [Min(0f)] public float exitDepth = 0.75f;
    [Tooltip("At the surface the feet hang this far under it (metres): about the height of the chest, so the head is out.")]
    [Min(0.2f)] public float floatDepth = 1.35f;
    [Tooltip("Eyes above the feet (metres), for 'head underwater'. 0 = from the CharacterController's height.")]
    [Min(0f)] public float eyeHeight;

    [Header("Direction (Subnautica-style)")]
    [Tooltip("Forward swims where the camera looks, up and down included (Subnautica). Off = forward stays level; only " +
             "Jump and Crouch change the depth.")]
    public bool swimWhereCameraLooks = true;
    [Tooltip("Camera pitch (degrees) treated as level: a third-person camera looking slightly down does not make the " +
             "player sink while swimming forward. 0 = exact look direction (first person).")]
    [Range(0f, 30f)] public float lookPitchDeadZone = 5f;

    [Header("Speeds")]
    [Tooltip("Swimming speed as a fraction of the walking speed.")]
    [Min(0f)] public float speedMultiplier = 0.85f;
    [Tooltip("Sprint held: swimming speed multiplied by this (costs more stamina). 1 = no fast swimming (Subnautica has none without fins).")]
    [Min(1f)] public float fastSwimMultiplier = 1.4f;
    [Tooltip("Up (Jump held) and down (Crouch held) swimming speed (m/s).")]
    [Min(0.1f)] public float verticalSpeed = 3f;
    [Tooltip("How quickly the swimmer reaches its speed (m/s²).")]
    [Min(0.1f)] public float acceleration = 7f;
    [Tooltip("How quickly the swimmer slows down when nothing is held (m/s²): lower = glides further.")]
    [Min(0.1f)] public float waterDrag = 3.5f;

    [Header("Buoyancy (no up/down key held)")]
    [Tooltip("Neutral (Subnautica): stay at your depth. Floats: rise back to the surface. Sinks: slowly go to the bottom.")]
    public SwimBuoyancy buoyancy = SwimBuoyancy.Neutral;
    [Tooltip("Speed of rising back to the surface (m/s), with Floats.")]
    [Min(0f)] public float riseSpeed = 1.2f;
    [Tooltip("Speed of sinking (m/s), with Sinks (and when exhausted with Sink When Exhausted).")]
    [Min(0f)] public float sinkSpeed = 1f;

    [Header("Stamina (only when the Movement Model's Should Consume Stamina is on)")]
    [Tooltip("Stamina spent every 0.8 s while swimming (moving or going up/down).")]
    [Min(0f)] public float staminaCost = 1f;
    [Tooltip("Stamina spent every 0.8 s while swimming fast (Sprint held).")]
    [Min(0f)] public float fastSwimStaminaCost = 3f;
    [Tooltip("Stamina spent every 0.8 s while treading water (no control held). 0 = treading water rests.")]
    [Min(0f)] public float treadingStaminaCost;
    [Tooltip("Out of stamina: swimming speed multiplied by this, no fast swimming, no swimming up.")]
    [Range(0f, 1f)] public float exhaustedSpeedMultiplier = 0.5f;
    [Tooltip("Out of stamina the player sinks (whatever the buoyancy) - he must reach the shore before he tires.")]
    public bool sinkWhenExhausted;

    [Header("Leaving the water")]
    [Tooltip("Jump pressed at the surface while moving hops out of the water (to climb onto a bank).")]
    public bool jumpOutAtSurface = true;
}
