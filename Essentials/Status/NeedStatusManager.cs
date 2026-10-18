using UnityEngine;

/// <summary>
/// A need that drains by itself and hurts when empty: hunger and thirst.
/// <para><b>How the numbers work</b> (the same for both):</para>
/// <list type="bullet">
/// <item>Every <b>Consuming Tick Rate</b> seconds the value loses <b>Decrement Value</b>, made bigger or smaller by
/// <b>Decrement Factor</b> (a PERCENT: +50 = 50% more, -50 = half, 0 = unchanged) and by the drain multipliers other
/// systems set (cold makes you hungry faster, sleeping slows it - see <see cref="StatusManager.RateMultiplier"/>).
/// Example: Consuming Tick Rate 6, Decrement Value 1, Decrement Factor 0 = 1 point every 6 s = 10 per minute, a full
/// bar of 100 lasts 10 minutes.</item>
/// <item>Hunger and thirst do NOT regenerate by themselves: food, drinks, potions and abilities fill them (each gain is
/// changed by <b>Increment Factor</b>, a percent).</item>
/// <item>When empty, the character loses the penalty damage every penalty interval until it eats / drinks.</item>
/// </list>
/// One timer drives it (no coroutine is started per frame), so the drain is exactly the configured rate.
/// </summary>
public abstract class NeedStatusManager : StatusManager
{
    [Header("Need")]
    [Tooltip("Start full when the game starts (off: keep the value set in the inspector).")]
    [SerializeField] protected bool startFull = true;
    [Tooltip("No drain and no penalty while the character is dead or unconscious.")]
    [SerializeField] protected bool pauseWhileDown = true;

    protected HealthManager healthManager;
    private float drainTimer;
    private float penaltyTimer;

    /// <summary>Health lost per penalty tick while empty.</summary>
    protected abstract float PenaltyDamage { get; }
    /// <summary>Seconds between two penalty ticks while empty.</summary>
    protected abstract float PenaltyInterval { get; }

    /// <summary>Is the need empty (the character is starving / dehydrated)?</summary>
    public bool IsEmpty => currentValue <= 0f;

    /// <summary>Points lost per minute at the current rate (inspectors, tooltips).</summary>
    public float DrainPerMinute => shouldConsume && consumingTickRate > 0f
        ? RemoveFactor(decrementValue) * RateMultiplier * 60f / Mathf.Max(0.05f, consumingTickRate) : 0f;

    protected virtual void Awake()
    {
        healthManager = GetComponent<HealthManager>();
        if (healthManager == null)
            healthManager = GetComponentInParent<HealthManager>();
    }

    protected virtual void Start()
    {
        // A client's copy keeps the value the server sent (it may have arrived before Start).
        if (!IsReplica && startFull)
            currentValue = maxValue;
    }

    protected virtual void Update()
    {
        if (IsReplica)
            return; // the drain and the penalty run on the server
        Simulate(Time.deltaTime);
    }

    private void Simulate(float dt)
    {
        if (pauseWhileDown && StatusDamage.IsDown(this))
        {
            isConsuming = false;
            penaltyTimer = 0f;
            return;
        }

        // Drain: one tick every Consuming Tick Rate seconds.
        if (shouldConsume && currentValue > 0f)
        {
            isConsuming = true;
            float interval = Mathf.Max(0.05f, consumingTickRate);
            drainTimer += dt;
            int guard = 0;
            while (drainTimer >= interval && guard++ < 16)
            {
                drainTimer -= interval;
                currentValue = Mathf.Max(0f, currentValue - RemoveFactor(decrementValue) * RateMultiplier);
            }
        }
        else
        {
            isConsuming = false;
            drainTimer = 0f;
        }
        isRegenerating = false; // needs never regenerate by themselves

        // Empty: the penalty every interval.
        if (currentValue <= 0f && PenaltyDamage > 0f)
        {
            penaltyTimer += dt;
            float interval = Mathf.Max(0.1f, PenaltyInterval);
            if (penaltyTimer >= interval)
            {
                penaltyTimer -= interval;
                StatusDamage.Apply(this, PenaltyDamage);
            }
        }
        else
        {
            penaltyTimer = 0f;
        }
    }

    /// <summary>Removes <paramref name="amount"/> now (changed by Decrement Factor).</summary>
    protected void Consume(float amount)
    {
        if (IsReplica || amount <= 0f || currentValue <= 0f)
            return;
        currentValue = Mathf.Max(0f, currentValue - RemoveFactor(amount));
    }

    protected override void OnReplicaChanged(bool on)
    {
        drainTimer = 0f;
        penaltyTimer = 0f;
    }

    protected override void UpdateStatus() => UpdateBar();
    protected override float AddFactor(float amount) => ApplyFactor(amount, incrementFactor);
    protected override float RemoveFactor(float amount) => ApplyFactor(amount, decrementFactor);

    public override void ResetForRespawn(float fraction)
    {
        base.ResetForRespawn(fraction);
        drainTimer = 0f;
        penaltyTimer = 0f;
    }

    protected virtual void OnValidate()
    {
        consumingTickRate = Mathf.Max(0.05f, consumingTickRate);
        decrementValue = Mathf.Max(0f, decrementValue);
    }
}
