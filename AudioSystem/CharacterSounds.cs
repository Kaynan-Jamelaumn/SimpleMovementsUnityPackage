using UnityEngine;

/// <summary>
/// The sounds a character makes by itself - a player, a mob, an NPC, an animal: anything with a
/// <see cref="CombatEntity"/>. Each slot is an <see cref="AudioCue"/>; an empty slot plays nothing, so add the
/// component and fill what the character has.
/// <list type="bullet">
/// <item><b>Hits</b>: a grunt when hurt (a louder one for a hit that takes a big share of its health), not more often
/// than Hurt Interval, and not for damage over time.</item>
/// <item><b>Life</b>: death, coming back, being healed, gaining a level.</item>
/// <item><b>Creature voice</b>: a call now and then while alive, one when it notices an enemy, one when it dodges
/// (mobs).</item>
/// <item><b>Steps</b>: one per Step Length walked on the ground - for creatures whose clips have no footstep events
/// (players have the Player Movement Audio for that).</item>
/// </list>
/// Heard on every machine that shows the character: it listens to the presentation events, which the server's hits,
/// deaths and reactions also raise on the clients. Sounds only - nothing here changes the game.
/// </summary>
[DisallowMultipleComponent]
public class CharacterSounds : MonoBehaviour
{
    [Header("Hits")]
    [SerializeField] private AudioCue hurt;
    [Tooltip("Played instead of Hurt for a hit that takes at least Bad Hit Share of the character's health (empty = Hurt).")]
    [SerializeField] private AudioCue hurtBadly;
    [SerializeField, Range(0.05f, 1f)] private float badHitShare = 0.25f;
    [Tooltip("Least seconds between two hurt sounds (a flurry of hits is one grunt).")]
    [SerializeField, Min(0f)] private float hurtInterval = 0.3f;

    [Header("Life")]
    [SerializeField] private AudioCue death;
    [SerializeField] private AudioCue revive;
    [SerializeField] private AudioCue healed;
    [Tooltip("Least seconds between two healed sounds (healing over time is one sound, not one per tick).")]
    [SerializeField, Min(0f)] private float healedInterval = 2f;
    [SerializeField] private AudioCue levelUp;

    [Header("Creature voice")]
    [Tooltip("A call now and then while alive (a growl, a bleat).")]
    [SerializeField] private AudioCue idle;
    [Tooltip("Seconds between two idle calls: one between the two values is picked each time.")]
    [SerializeField] private Vector2 idleEvery = new Vector2(8f, 20f);
    [Tooltip("When it notices an enemy (mobs).")]
    [SerializeField] private AudioCue alert;
    [Tooltip("When it sidesteps an attack (mobs).")]
    [SerializeField] private AudioCue dodge;

    [Header("Steps (creatures)")]
    [SerializeField] private AudioCue footstep;
    [Tooltip("Metres walked on the ground between two steps.")]
    [SerializeField, Min(0.2f)] private float stepLength = 1.4f;

    private CombatEntity entity;
    private MobMovementStateMachine mob;
    private ExperienceManager experience;
    private bool hooked;
    private float nextHurt, nextHealed, nextIdle;
    private Vector3 lastPosition;
    private float walked;
    private bool hasLast;

    private void OnEnable()
    {
        if (entity == null)
            entity = CombatEntity.Resolve(gameObject);
        if (mob == null)
            mob = GetComponentInParent<MobMovementStateMachine>();
        if (experience == null)
            experience = GetComponentInParent<ExperienceManager>();
        Hook(true);
        hasLast = false;
        walked = 0f;
        nextIdle = Time.time + NextIdleDelay();
    }

    private void OnDisable() => Hook(false);

    private void Hook(bool on)
    {
        if (hooked == on)
            return;
        hooked = on;
        if (entity != null)
        {
            if (on)
            {
                entity.DamageFeedback += OnHit;
                entity.HealFeedback += OnHealed;
                entity.Died += OnDied;
                entity.Revived += OnRevived;
                entity.ReplicatedLifeChanged += OnReplicatedLife;
            }
            else
            {
                entity.DamageFeedback -= OnHit;
                entity.HealFeedback -= OnHealed;
                entity.Died -= OnDied;
                entity.Revived -= OnRevived;
                entity.ReplicatedLifeChanged -= OnReplicatedLife;
            }
        }
        if (mob != null)
        {
            if (on) mob.ReactionShown += OnReaction;
            else mob.ReactionShown -= OnReaction;
        }
        if (experience != null)
        {
            if (on) experience.OnLevelUp += OnLevelUp;
            else experience.OnLevelUp -= OnLevelUp;
        }
    }

    /// <summary>Plays one of the character's cues on it (for animation events and other scripts). Null plays nothing.</summary>
    public void Play(AudioCue cue)
    {
        if (cue != null)
            cue.Play(gameObject);
    }

    // ------------------------------------------------------------------ hits and life
    private void OnHit(DamageInfo info)
    {
        // Stopped hits have their own sounds (the block), damage over time would grunt every tick.
        if (info.amount <= 0f || info.isPeriodic || Time.time < nextHurt)
            return;
        if (entity != null && entity.IsDead)
            return; // the killing blow is the death sound
        AudioCue cue = hurt;
        float max = entity != null ? entity.MaxHealth : 0f;
        if (hurtBadly != null && max > 0f && info.amount >= max * badHitShare)
            cue = hurtBadly;
        if (cue == null)
            return;
        nextHurt = Time.time + hurtInterval;
        cue.Play(gameObject);
    }

    private void OnHealed(float amount)
    {
        if (healed == null || amount <= 0f || Time.time < nextHealed)
            return;
        nextHealed = Time.time + healedInterval;
        healed.Play(gameObject);
    }

    private void OnDied(CombatEntity killer) => Play(death);

    private void OnRevived() => Play(revive);

    private void OnReplicatedLife(bool dead) => Play(dead ? death : revive);

    private void OnLevelUp(int level) => Play(levelUp);

    private void OnReaction(MobAnimationEvent e)
    {
        if (e == MobAnimationEvent.Alert)
            Play(alert);
        else if (e == MobAnimationEvent.Dodge)
            Play(dodge);
    }

    // ------------------------------------------------------------------ voice and steps
    private void Update()
    {
        if (idle == null && footstep == null)
            return;
        bool dead = entity != null && entity.IsDead;
        if (idle != null && Time.time >= nextIdle)
        {
            nextIdle = Time.time + NextIdleDelay();
            if (!dead)
                idle.Play(gameObject);
        }
        if (footstep != null)
            Steps(dead);
    }

    private float NextIdleDelay()
    {
        float low = Mathf.Max(0.5f, Mathf.Min(idleEvery.x, idleEvery.y));
        float high = Mathf.Max(low, Mathf.Max(idleEvery.x, idleEvery.y));
        return Random.Range(low, high);
    }

    private void Steps(bool dead)
    {
        Vector3 position = transform.position;
        if (!hasLast)
        {
            hasLast = true;
            lastPosition = position;
            return;
        }
        Vector3 moved = position - lastPosition;
        lastPosition = position;
        moved.y = 0f;
        float distance = moved.magnitude;
        // Dead, moved in one go (a teleport, a spawn), or not on its feet: no steps, and the stride starts again.
        if (dead || distance > 3f || !OnTheGround())
        {
            walked = 0f;
            return;
        }
        walked += distance;
        if (walked < stepLength)
            return;
        walked = 0f;
        footstep.Play(position);
    }

    /// <summary>Mobs tell whether they fly, swim or are in the air (also on a client's copy); anything else is taken to walk.</summary>
    private bool OnTheGround()
    {
        if (mob == null || mob.Context == null)
            return true;
        MobAnimationDriver driver = mob.Context.Animation;
        if (driver != null && driver.IsReplica)
            return !driver.Replicated.Has(MobReplicaFlags.Flying) && !driver.Replicated.Has(MobReplicaFlags.Swimming);
        MobMotor motor = mob.Context.Motor;
        return motor == null || (!motor.IsFlying && !motor.IsSwimming && !motor.IsAirborne);
    }
}
