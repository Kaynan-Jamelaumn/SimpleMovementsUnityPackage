using System.Collections.Generic;
using UnityEngine;

/// <summary>An attack intention that a client's copy of its own player sends to the server (see <see cref="IWeaponNetBridge"/>).</summary>
public enum WeaponInputKind : byte
{
    /// <summary>An attack input was pressed (<see cref="WeaponController.BeginAttackInput"/>).</summary>
    Press = 0,
    /// <summary>An attack input was released (<see cref="WeaponController.ReleaseAttackInput"/>).</summary>
    Release = 1,
    /// <summary>A tap: pressed and released at once (<see cref="WeaponController.PerformAttack"/>).</summary>
    Tap = 2,
    /// <summary>The reload input (<see cref="WeaponController.Reload"/>).</summary>
    Reload = 3,
    /// <summary>Drop the charge without attacking (<see cref="WeaponController.CancelCharge"/>).</summary>
    CancelCharge = 4,
}

/// <summary>
/// What a weapon controller and a block controller ask the network layer to do when this machine does not decide
/// gameplay (a client's copy of its own player): send attack, reload, throw and block intentions to the server, which
/// runs the same weapon code with them. Installed by the network layer on the owning client only
/// (<see cref="WeaponController.NetBridge"/>, <see cref="BlockController.NetBridge"/>); offline nothing is installed and
/// every input runs locally exactly as before.
/// </summary>
public interface IWeaponNetBridge
{
    /// <summary>Sends an attack input of <paramref name="input"/> (the raw input pressed, before any hand mapping).</summary>
    void SendAttackInput(WeaponController controller, WeaponInputKind kind, AttackType input);

    /// <summary>Asks the server to throw one <paramref name="weapon"/> from the inventory. Returns true when a request was sent.</summary>
    bool SendQuickThrow(WeaponController controller, WeaponSO weapon);

    /// <summary>Tells the server whether the player wants its block up (sent when it changes).</summary>
    void SendBlockIntent(BlockController block, bool wantsToBlock);
}

/// <summary>
/// Optional: an ammo source that can find the inventory stack a thrown weapon is taken from. The server uses it to
/// resolve a client's quick throw (the client sends only which weapon). Without it the server looks through an
/// <see cref="InventoryManager"/> ammo source, then the hands.
/// </summary>
public interface IThrowableSource
{
    /// <summary>A stack of <paramref name="weapon"/> that can be thrown now (not on cooldown), or null.</summary>
    InventoryItem FindThrowable(WeaponSO weapon);
}

/// <summary>
/// Spawns the item a stuck projectile is recovered as (an arrow, the thrown weapon) as a networked world item, so every
/// player can see and pick it up. Installed by the network layer on the server (<see cref="WeaponProjectile.RecoveredItemSpawner"/>);
/// returns true when it spawned the item (the projectile is then removed), false to keep the local pickup.
/// </summary>
/// <param name="item">The item recovered.</param>
/// <param name="durability">Durability of each unit (may be null).</param>
/// <param name="position">Where the projectile is stuck.</param>
/// <param name="rotation">Its rotation.</param>
/// <param name="sceneOf">The projectile: the item belongs in its scene.</param>
public delegate bool RecoveredItemSpawn(ItemSO item, IList<int> durability, Vector3 position, Quaternion rotation, GameObject sceneOf);

/// <summary>An attack (or a charge) as the machine that runs the rules shows it; the network layer repeats it on clients.</summary>
public struct WeaponAttackShown
{
    /// <summary>The hand attacking.</summary>
    public WeaponHandSide hand;
    /// <summary>The input that started it (after hand mapping).</summary>
    public AttackType input;
    /// <summary>The weapon attacking (the thrown weapon for a quick throw).</summary>
    public WeaponSO weapon;
    /// <summary>The attack (action or variation).</summary>
    public AttackComponent attack;
    /// <summary>The action the attack belongs to (itself for an action).</summary>
    public AttackAction action;
    /// <summary>The combo sequence it finishes, or null (its finisher effects play).</summary>
    public ComboSequence sequence;
    /// <summary>The combo tree branch it starts, or null (its branch effects play).</summary>
    public ComboBranch branch;
    /// <summary>0-1 charge of the attack.</summary>
    public float chargeRatio;
    /// <summary>Speed multiplier of the timeline (frames / speed = seconds).</summary>
    public float speed;
    /// <summary>A quick throw: only its animation plays (no timeline).</summary>
    public bool quick;
    /// <summary>
    /// Seed of the attack's behaviour rolls (<see cref="WeaponNetMath.Roll"/>): every machine decides the same way which
    /// chance-based cosmetic behaviours play.
    /// </summary>
    public int seed;
}

/// <summary>A ranged shot as the machine that runs the rules fired it.</summary>
public struct WeaponShotShown
{
    /// <summary>The hand that fired.</summary>
    public WeaponHandSide hand;
    /// <summary>The weapon that fired (the thrown weapon for a throw).</summary>
    public WeaponSO weapon;
    /// <summary>The attack that fired (its hit filter decides what a cosmetic projectile stops on), or null.</summary>
    public AttackComponent attack;
    /// <summary>The ammo fired (null = none / the weapon itself).</summary>
    public AmmoSO ammo;
    /// <summary>Where the projectiles left (muzzle).</summary>
    public Vector3 from;
    /// <summary>Direction of the shot before spread (muzzle flash orientation).</summary>
    public Vector3 direction;
    /// <summary>Initial velocity of each projectile (spread included).</summary>
    public List<Vector3> velocities;
}

/// <summary>A hit's feedback (particles, hit effect, sound) as the machine that runs the rules showed it.</summary>
public struct WeaponHitShown
{
    /// <summary>The weapon that hit.</summary>
    public WeaponSO weapon;
    /// <summary>The attack that hit.</summary>
    public AttackComponent attack;
    /// <summary>Where it hit.</summary>
    public Vector3 point;
    /// <summary>The way the hit travelled.</summary>
    public Vector3 direction;
    /// <summary>Element of the hit (its particles play), None = none.</summary>
    public ElementType element;
    /// <summary>The character hit (null for objects).</summary>
    public CombatEntity target;
    /// <summary>A prop or tool target: only the attack's particles play.</summary>
    public bool objectOnly;
    /// <summary>A block or a parry stopped the hit completely: no hit effects (the block shows its own).</summary>
    public bool stopped;
    /// <summary>Seed of the attack's behaviour rolls (see <see cref="WeaponAttackShown.seed"/>).</summary>
    public int seed;
    /// <summary>The attack's hit count with this hit (its Each Hit behaviours roll with it).</summary>
    public int hitIndex;
}

/// <summary>An attack's Impact (a slam reaching the ground) as the machine that runs the rules showed it.</summary>
public struct WeaponImpactShown
{
    /// <summary>The weapon that struck the ground.</summary>
    public WeaponSO weapon;
    /// <summary>The attack whose Impact it is.</summary>
    public AttackComponent attack;
    /// <summary>Where the ground was struck.</summary>
    public Vector3 point;
    /// <summary>The ground's normal there.</summary>
    public Vector3 normal;
    /// <summary>The attacker's facing (the effect's orientation).</summary>
    public Vector3 forward;
    /// <summary>Area multiplier of the attack (the effect's scale).</summary>
    public float scale;
    /// <summary>Seed of the attack's behaviour rolls (see <see cref="WeaponAttackShown.seed"/>).</summary>
    public int seed;
}

/// <summary>
/// Receives what a weapon controller shows where the rules run (offline nothing listens; the server's network layer
/// repeats it on every client watching the player). Calls happen after the gameplay they describe, never instead of it.
/// </summary>
public interface IWeaponPresentationSink
{
    /// <summary>A charge (or a bow draw) began.</summary>
    void ChargeShown(WeaponController controller, in WeaponAttackShown attack);
    /// <summary>An attack started (after its charge, if any), or a quick throw was made.</summary>
    void AttackShown(WeaponController controller, in WeaponAttackShown attack);
    /// <summary>The attack or charge running was cut short (interrupted, charge dropped or released too early).</summary>
    void AttackStopped(WeaponController controller, bool interrupted);
    /// <summary>A ranged weapon fired.</summary>
    void ShotShown(WeaponController controller, in WeaponShotShown shot);
    /// <summary>An attack hit something.</summary>
    void HitShown(WeaponController controller, in WeaponHitShown hit);
    /// <summary>An attack's Impact happened.</summary>
    void ImpactShown(WeaponController controller, in WeaponImpactShown impact);
    /// <summary>A hand started (true) or stopped (false) reloading.</summary>
    void ReloadShown(WeaponController controller, WeaponHandSide side, bool started);
    /// <summary>An attack could not start (the reason as in <see cref="WeaponController.AttackFailed"/>).</summary>
    void AttackFailedShown(WeaponController controller, AttackType input, string reason);
}

/// <summary>
/// Stable numbering of a weapon's attacks, so machines can name an attack with a small number instead of an object:
/// the five actions (Normal, Light, Heavy, Special, Alternate) each followed by its variations, then the special action of
/// every combo sequence, then the action of every combo tree branch. Missing actions keep their slot (empty), so the
/// numbers depend only on the weapon asset, which every machine has.
/// </summary>
public static class WeaponAttackCatalog
{
    /// <summary>One attack of a weapon and where it comes from.</summary>
    public struct Entry
    {
        /// <summary>The attack (action or variation); null for an empty action slot.</summary>
        public AttackComponent attack;
        /// <summary>The action it belongs to (itself for an action).</summary>
        public AttackAction action;
        /// <summary>The combo sequence whose special action it is, or null.</summary>
        public ComboSequence sequence;
        /// <summary>The combo tree branch whose action it is, or null.</summary>
        public ComboBranch branch;
    }

    /// <summary>The number used for "no attack".</summary>
    public const int None = -1;

    private static readonly List<Entry> buffer = new List<Entry>(32);

    /// <summary>Lists every attack of <paramref name="weapon"/> in catalog order into <paramref name="into"/> (cleared first).</summary>
    public static void Collect(WeaponSO weapon, List<Entry> into)
    {
        into.Clear();
        if (weapon == null)
            return;
        AddAction(into, weapon.NormalAction, true);
        AddAction(into, weapon.LightAction, true);
        AddAction(into, weapon.HeavyAction, true);
        AddAction(into, weapon.SpecialAction, true);
        AddAction(into, weapon.AlternateAction, true);
        List<ComboSequence> sequences = weapon.ComboSequences;
        if (sequences != null)
        {
            for (int i = 0; i < sequences.Count; i++)
            {
                ComboSequence s = sequences[i];
                into.Add(new Entry { attack = s != null ? s.specialAction : null, action = s != null ? s.specialAction : null, sequence = s });
            }
        }
        ComboTree tree = weapon.ComboTree;
        if (tree != null && tree.branches != null)
        {
            for (int i = 0; i < tree.branches.Count; i++)
            {
                ComboBranch b = tree.branches[i];
                into.Add(new Entry { attack = b != null ? b.branchAction : null, action = b != null ? b.branchAction : null, branch = b });
            }
        }
    }

    private static void AddAction(List<Entry> into, AttackAction action, bool withVariations)
    {
        into.Add(new Entry { attack = action, action = action });
        if (!withVariations || action == null || action.variations == null)
            return;
        for (int i = 0; i < action.variations.Count; i++)
            into.Add(new Entry { attack = action.variations[i], action = action });
    }

    /// <summary>The number of <paramref name="attack"/> in <paramref name="weapon"/>'s catalog, or <see cref="None"/>.</summary>
    public static int IndexOf(WeaponSO weapon, AttackComponent attack)
    {
        if (weapon == null || attack == null)
            return None;
        Collect(weapon, buffer);
        for (int i = 0; i < buffer.Count; i++)
        {
            if (ReferenceEquals(buffer[i].attack, attack))
            {
                buffer.Clear();
                return i;
            }
        }
        buffer.Clear();
        return None;
    }

    /// <summary>The attack numbered <paramref name="index"/> in <paramref name="weapon"/>'s catalog. False when there is none.</summary>
    public static bool TryGet(WeaponSO weapon, int index, out Entry entry)
    {
        entry = default;
        if (weapon == null || index < 0)
            return false;
        Collect(weapon, buffer);
        bool found = index < buffer.Count && buffer[index].attack != null;
        if (found)
            entry = buffer[index];
        buffer.Clear();
        return found;
    }
}

/// <summary>
/// Small pure helpers of the weapon network layer: validation of what clients send, rate limiting, packing. No
/// Fish-Net types: always compiled and covered by Edit Mode tests.
/// </summary>
public static class WeaponNetMath
{
    /// <summary>Largest coordinate accepted from a client (metres): anything beyond is treated as garbage.</summary>
    public const float MaxCoordinate = 100000f;

    /// <summary>Farthest an aim ray may start from the player (a third-person camera behind it), metres.</summary>
    public const float MaxAimOriginDistance = 15f;

    /// <summary>Is <paramref name="f"/> a usable number?</summary>
    public static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

    /// <summary>Are all components of <paramref name="v"/> usable numbers within <see cref="MaxCoordinate"/>?</summary>
    public static bool IsFinite(Vector3 v) =>
        IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z) &&
        Mathf.Abs(v.x) <= MaxCoordinate && Mathf.Abs(v.y) <= MaxCoordinate && Mathf.Abs(v.z) <= MaxCoordinate;

    /// <summary>Is <paramref name="kind"/> a known <see cref="WeaponInputKind"/>?</summary>
    public static bool IsKnownKind(byte kind) => kind <= (byte)WeaponInputKind.CancelCharge;

    /// <summary>Is <paramref name="input"/> a known <see cref="AttackType"/>?</summary>
    public static bool IsKnownInput(byte input) => input <= (byte)AttackType.OffHand;

    /// <summary>
    /// Cleans an aim ray received from a client: rejects non-finite or absurd values and a zero direction, normalises the
    /// direction and pulls the origin back within <paramref name="maxOriginDistance"/> of <paramref name="anchor"/> (the
    /// player's position on the server). Returns false when the ray is unusable.
    /// </summary>
    public static bool SanitizeAim(ref Vector3 origin, ref Vector3 direction, Vector3 anchor, float maxOriginDistance)
    {
        if (!IsFinite(origin) || !IsFinite(direction))
            return false;
        float sqr = direction.sqrMagnitude;
        if (sqr < 1e-8f)
            return false;
        direction /= Mathf.Sqrt(sqr);
        Vector3 d = origin - anchor;
        float dist = d.magnitude;
        if (maxOriginDistance >= 0f && dist > maxOriginDistance)
            origin = anchor + d / dist * maxOriginDistance;
        return true;
    }

    /// <summary>
    /// Has an aim ray moved enough to be sent again: its origin by more than <paramref name="minMove"/> metres or its
    /// direction by more than <paramref name="minAngle"/> degrees?
    /// </summary>
    public static bool AimChanged(Vector3 oldOrigin, Vector3 oldDirection, Vector3 origin, Vector3 direction, float minMove, float minAngle)
    {
        if ((origin - oldOrigin).sqrMagnitude > minMove * minMove)
            return true;
        if (oldDirection.sqrMagnitude < 1e-8f || direction.sqrMagnitude < 1e-8f)
            return oldDirection.sqrMagnitude >= 1e-8f || direction.sqrMagnitude >= 1e-8f;
        return Vector3.Angle(oldDirection, direction) > minAngle;
    }

    /// <summary>A view tick's fraction (0-1) as a byte percentage (0-100).</summary>
    public static byte PercentToByte(double fraction) => (byte)Mathf.Clamp(Mathf.RoundToInt((float)(fraction * 100d)), 0, 100);

    /// <summary>
    /// A deterministic roll in [0, 1) from an attack's <paramref name="seed"/> and what is rolled for (behaviour index,
    /// moment, hit number). Offline it is as random as <see cref="Random.value"/> (the seed is random); in a session the
    /// server sends the seed, so clients play exactly the chance-based cosmetic behaviours the server ran.
    /// </summary>
    public static float Roll(int seed, int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)a * 0x85EBCA77u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)b * 0xC2B2AE3Du;
            h = (h << 11) | (h >> 21);
            h ^= (uint)c * 0x27D4EB2Fu;
            // Final avalanche (murmur3 fmix32)
            h ^= h >> 16;
            h *= 0x85EBCA6Bu;
            h ^= h >> 13;
            h *= 0xC2B2AE35u;
            h ^= h >> 16;
            return (h >> 8) * (1f / 16777216f); // 24 bits: exactly representable, always < 1
        }
    }

    /// <summary>A random non-zero seed for an attack (<see cref="Roll"/>).</summary>
    public static int NewSeed()
    {
        int s = Random.Range(int.MinValue, int.MaxValue);
        return s != 0 ? s : 1;
    }

    /// <summary>Packs a hand and an input into one byte (hand in bit 7, input in the low bits).</summary>
    public static byte PackHandInput(WeaponHandSide hand, AttackType input) =>
        (byte)(((int)input & 0x7F) | (hand == WeaponHandSide.Off ? 0x80 : 0));

    /// <summary>Unpacks <see cref="PackHandInput"/>. False when the input is unknown.</summary>
    public static bool UnpackHandInput(byte packed, out WeaponHandSide hand, out AttackType input)
    {
        hand = (packed & 0x80) != 0 ? WeaponHandSide.Off : WeaponHandSide.Main;
        int i = packed & 0x7F;
        input = (AttackType)i;
        return IsKnownInput((byte)i);
    }

    /// <summary>
    /// A token bucket: <see cref="PerSecond"/> requests per second on average, bursts up to <see cref="Burst"/>.
    /// Used by the server to drop spammed requests.
    /// </summary>
    public struct RateLimiter
    {
        /// <summary>Tokens added per second.</summary>
        public float PerSecond;
        /// <summary>Most tokens kept (largest burst).</summary>
        public float Burst;
        private float tokens;
        private float last;
        private bool started;

        public RateLimiter(float perSecond, float burst)
        {
            PerSecond = Mathf.Max(0f, perSecond);
            Burst = Mathf.Max(1f, burst);
            tokens = Burst;
            last = 0f;
            started = false;
        }

        /// <summary>Takes one token at time <paramref name="now"/> (seconds). False = over the limit.</summary>
        public bool TryTake(float now)
        {
            if (!started)
            {
                started = true;
                last = now;
                tokens = Burst;
            }
            float dt = Mathf.Max(0f, now - last);
            last = now;
            tokens = Mathf.Min(Burst, tokens + dt * PerSecond);
            if (tokens < 1f)
                return false;
            tokens -= 1f;
            return true;
        }
    }
}
