#if FISHNET
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// The owner's intention to cast one of its slots (client to server). It carries what the player aimed at, never
    /// any number that decides the outcome: the server validates it and runs its own copy of the cast.
    /// </summary>
    public struct NetAbilityCastRequest
    {
        /// <summary>Slot index on the caster.</summary>
        public byte slot;
        /// <summary>Request number (lets the owner match the server's refusal to its request).</summary>
        public ushort seq;
        /// <summary>Id of the ability the owner sees in that slot (refused when the server's slot holds another one).</summary>
        public string abilityId;
        /// <summary>The character aimed at (null: none, or one without a NetworkObject).</summary>
        public NetworkObject target;
        /// <summary>The aimed point (crosshair hit) when <see cref="hasPoint"/>.</summary>
        public Vector3 point;
        public bool hasPoint;
        /// <summary>The aimed direction when <see cref="hasDirection"/>.</summary>
        public Vector3 direction;
        public bool hasDirection;
        /// <summary>The client's last received server tick (what it was looking at: lag compensation).</summary>
        public uint viewTick;
        /// <summary>How far into <see cref="viewTick"/> the client was (0-100).</summary>
        public byte viewPercent;
    }

    /// <summary>The owner's current crosshair while an ability that follows it until release winds up (client to server).</summary>
    public struct NetAbilityAim
    {
        public NetworkObject target;
        public Vector3 point;
        public bool hasPoint;
        public Vector3 direction;
        public bool hasDirection;
        public uint viewTick;
        public byte viewPercent;
    }

    /// <summary>A cast the server started (server to observers): everything a client needs to replay its presentation.</summary>
    public struct NetAbilityCastStart
    {
        /// <summary>The server's cast id (release and interrupt messages refer to it).</summary>
        public int castId;
        public byte slot;
        public string abilityId;
        /// <summary>The server's resolved numbers (cast time, area, counts...): clients never recompute them.</summary>
        public AbilityStats stats;
        public NetworkObject target;
        public Vector3 aimPoint;
        public Vector3 aimDirection;
        public Vector3 aimTargetPoint;
        public bool hasAimTargetPoint;
        /// <summary>Wind-up seconds on the server.</summary>
        public float castTime;
        /// <summary>Server tick of the start (clients skip the part of the wind-up the message spent travelling).</summary>
        public uint tick;
        /// <summary>Seed of the cast's cosmetic randomness (projectile spreads, rains, summon placement).</summary>
        public int seed;
        /// <summary>City casts: shown only, no action runs anywhere.</summary>
        public bool visualOnly;
    }

    /// <summary>The server released a cast (server to observers), with its final aim.</summary>
    public struct NetAbilityCastRelease
    {
        public int castId;
        public NetworkObject target;
        public Vector3 aimPoint;
        public Vector3 aimDirection;
        public Vector3 aimTargetPoint;
        public bool hasAimTargetPoint;
    }

    /// <summary>A caster-less cast the server ran (items, weapon attacks) whose source is this caster (server to observers).</summary>
    public struct NetAbilityInstantCast
    {
        public string abilityId;
        public AbilityStats stats;
        public NetworkObject target;
        public Vector3 position;
        public Vector3 direction;
        public int seed;
    }

    /// <summary>The server's timers of one slot (server to owner), as seconds left: absolute times differ per machine.</summary>
    public struct NetAbilitySlotState
    {
        public byte index;
        public AbilityPhase phase;
        public float phaseRemaining;
        public float phaseLength;
        public byte charges;
        public float rechargeRemaining;
        public float lockedRemaining;
        /// <summary>Server tick when measured (the owner discounts the travel time).</summary>
        public uint tick;
    }

    /// <summary>What one slot holds (replicated list, server to clients). Abilities travel as Ability Database ids.</summary>
    public struct NetAbilitySlotContent
    {
        public string abilityId;
        /// <summary>The slot's modifiers (JsonUtility, like saved absorbed abilities); empty = none.</summary>
        public string modifiersJson;
        public string label;
        public AbilitySlotSource source;
        public bool enabled;
        /// <summary>The slot holds an absorbed ability (the grant fields below are valid).</summary>
        public bool hasGrant;
        public string grantName;
        public AbsorbTier grantTier;
        public string grantSourceId;

        /// <summary>Same content (field by field)?</summary>
        public static bool Same(in NetAbilitySlotContent a, in NetAbilitySlotContent b) =>
            string.Equals(a.abilityId ?? "", b.abilityId ?? "") &&
            string.Equals(a.modifiersJson ?? "", b.modifiersJson ?? "") &&
            string.Equals(a.label ?? "", b.label ?? "") &&
            a.source == b.source &&
            a.enabled == b.enabled &&
            a.hasGrant == b.hasGrant &&
            string.Equals(a.grantName ?? "", b.grantName ?? "") &&
            a.grantTier == b.grantTier &&
            string.Equals(a.grantSourceId ?? "", b.grantSourceId ?? "");
    }
}
#endif
