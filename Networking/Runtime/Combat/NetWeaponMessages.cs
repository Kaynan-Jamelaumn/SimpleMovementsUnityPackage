#if FISHNET
using FishNet.Object;
using UnityEngine;

namespace GameNet
{
    /// <summary>
    /// An attack intention of the owning client: an input pressed, released or tapped, a reload, or a dropped charge
    /// (<see cref="WeaponInputKind"/>), with its aim ray and the tick it was looking at (lag compensation).
    /// </summary>
    public struct NetWeaponInput
    {
        /// <summary>The <see cref="WeaponInputKind"/>.</summary>
        public byte kind;
        /// <summary>The raw <see cref="AttackType"/> pressed (before the server's hand mapping).</summary>
        public byte input;
        /// <summary>Is <see cref="aimOrigin"/> / <see cref="aimDirection"/> set?</summary>
        public bool hasAim;
        /// <summary>Where the owner's aim ray starts (its camera).</summary>
        public Vector3 aimOrigin;
        /// <summary>The owner's aim direction.</summary>
        public Vector3 aimDirection;
        /// <summary>The client's <c>TimeManager.GetPreciseTick(TickType.LastPacketTick)</c> tick.</summary>
        public uint viewTick;
        /// <summary>That tick's fraction as a percentage (0-100).</summary>
        public byte viewPercent;
    }

    /// <summary>An attack (or a charge) the server started, as clients need it to show it.</summary>
    public struct NetWeaponAttack
    {
        /// <summary>Item Database id of the weapon ("" = the weapon in the hand).</summary>
        public string weaponId;
        /// <summary>The attack's number in the weapon's <see cref="WeaponAttackCatalog"/>.</summary>
        public short attack;
        /// <summary>Hand and input (<see cref="WeaponNetMath.PackHandInput"/>).</summary>
        public byte handInput;
        /// <summary>0-1 charge of the attack.</summary>
        public float chargeRatio;
        /// <summary>Speed multiplier of the timeline.</summary>
        public float speed;
        /// <summary>Seed of the attack's behaviour rolls.</summary>
        public int seed;
        /// <summary>A quick throw (animation only).</summary>
        public bool quick;
    }

    /// <summary>A ranged shot the server fired: clients fly cosmetic projectiles along the same velocities.</summary>
    public struct NetWeaponShot
    {
        /// <summary>Item Database id of the weapon ("" = the weapon in the hand).</summary>
        public string weaponId;
        /// <summary>Item Database id of the ammo fired ("" = none).</summary>
        public string ammoId;
        /// <summary>The attack's number in the weapon's catalog (-1 = unknown).</summary>
        public short attack;
        /// <summary>The <see cref="WeaponHandSide"/> that fired.</summary>
        public byte hand;
        /// <summary>The muzzle.</summary>
        public Vector3 from;
        /// <summary>The shot's direction before spread.</summary>
        public Vector3 direction;
        /// <summary>Initial velocity of each projectile.</summary>
        public Vector3[] velocities;
    }

    /// <summary>A hit of the server's attack (its feedback is shown on clients; damage is replicated by the status layer).</summary>
    public struct NetWeaponHit
    {
        /// <summary>Item Database id of the weapon ("" = the weapon in the main hand).</summary>
        public string weaponId;
        /// <summary>The attack's number in the weapon's catalog.</summary>
        public short attack;
        /// <summary>Where it hit.</summary>
        public Vector3 point;
        /// <summary>The way the hit travelled.</summary>
        public Vector3 direction;
        /// <summary>The <see cref="ElementType"/> of the hit.</summary>
        public byte element;
        /// <summary><see cref="NetWeaponCombat.HitObjectOnly"/> | <see cref="NetWeaponCombat.HitStopped"/>.</summary>
        public byte flags;
        /// <summary>The character hit (null for props, or when it is not networked).</summary>
        public NetworkObject target;
        /// <summary>Seed of the attack's behaviour rolls.</summary>
        public int seed;
        /// <summary>The attack's hit count with this hit.</summary>
        public short hitIndex;
    }

    /// <summary>An attack's Impact (a slam reaching the ground) made on the server.</summary>
    public struct NetWeaponImpact
    {
        /// <summary>Item Database id of the weapon ("" = the weapon in the main hand).</summary>
        public string weaponId;
        /// <summary>The attack's number in the weapon's catalog.</summary>
        public short attack;
        /// <summary>Where the ground was struck.</summary>
        public Vector3 point;
        /// <summary>The ground's normal.</summary>
        public Vector3 normal;
        /// <summary>The attacker's facing.</summary>
        public Vector3 forward;
        /// <summary>The attack's area multiplier.</summary>
        public float scale;
        /// <summary>Seed of the attack's behaviour rolls.</summary>
        public int seed;
    }
}
#endif
