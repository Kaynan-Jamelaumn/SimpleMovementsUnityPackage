using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>The rules of unconsciousness (global ones in Combat Settings, or a character's own).</summary>
[Serializable]
public class DownedRules
{
    [Tooltip("Characters with an Unconsciousness component fall unconscious at 0 health instead of dying.")]
    public bool enabled = true;

    [Header("Death threshold (how much damage below 0 health is survived)")]
    [Tooltip("Points of damage below 0 health the character survives, unconscious. A hit that takes health further than this " +
             "below 0 kills instantly (e.g. 15: at 10 health, a 20-damage hit = -10 = unconscious; a 30-damage hit = -20 = dead). " +
             "The Death Threshold stat (traits, race, class, equipment, buffs) is added.")]
    [Min(0f)] public float deathThreshold = 15f;
    [Tooltip("Plus this percent of the max health (0 = only the flat points). 25 = a 200-health character survives 50 more.")]
    [Range(0f, 200f)] public float deathThresholdPercentOfMaxHealth = 0f;

    [Header("Unconscious time")]
    [Tooltip("Seconds the character stays unconscious, waiting for a revive, before dying. The Unconscious Time stat " +
             "(traits, race, class...) adds seconds.")]
    [Min(1f)] public float unconsciousSeconds = 30f;
    [Tooltip("Off: the timer never runs out (the character waits until revived, killed or it gives up).")]
    public bool bleedOut = true;
    [Tooltip("Damage taken while unconscious is multiplied by this before it counts toward the death threshold " +
             "(1 = full, 0 = cannot be finished off).")]
    [Min(0f)] public float damageWhileDownMultiplier = 1f;

    [Header("Revive")]
    [Tooltip("Health a revived character gets back (fraction of the max).")]
    [Range(0.01f, 1f)] public float reviveHealthFraction = 0.25f;
    [Tooltip("Seconds of invulnerability after being revived (0 = none).")]
    [Min(0f)] public float reviveInvulnerability = 2f;
    [Tooltip("Other players can revive by holding Interact on the unconscious character.")]
    public bool reviveByInteracting = true;
    [Tooltip("Seconds Interact must be held to revive.")]
    [Min(0f)] public float reviveHoldSeconds = 3f;
    [Tooltip("Healing (potions, heals) first pays back the damage below 0, then wakes the character up. Off: only a revive " +
             "(Revive ability effect, interacting) wakes it.")]
    public bool healingRevives = false;

    [Header("Giving up")]
    [Tooltip("The unconscious player may give up and die at once (button on the screen or the Give Up input).")]
    public bool allowGiveUp = true;
    [Tooltip("Seconds the Give Up input must be held (avoids accidents).")]
    [Min(0f)] public float giveUpHoldSeconds = 1.5f;

    public DownedRules Clone() => (DownedRules)MemberwiseClone();
}

/// <summary>
/// Unconsciousness, the instant-death threshold, revives and giving up.
/// <list type="bullet">
/// <item><b>0 health</b> knocks the character out instead of killing it: it cannot move or act, its health stays at 0
/// and does not regenerate, and a countdown runs (<see cref="DownedRules.unconsciousSeconds"/> + the Unconscious Time
/// stat). When it runs out, the character dies.</item>
/// <item><b>Death threshold</b>: the damage below 0 is counted. When a hit (or hits while down) takes the character more
/// than <see cref="Threshold"/> below 0 - <see cref="DownedRules.deathThreshold"/> + percent of max health + the Death
/// Threshold stat - it dies instantly.</item>
/// <item><b>Revive</b>: a Revive ability effect (Abilities ▸ Healing ▸ Revive), another player holding Interact on it,
/// healing when Healing Revives is on, or <see cref="Revive"/> from your own scripts.</item>
/// <item><b>Give up</b>: the player can die at once (screen button or the Give Up input held).</item>
/// </list>
/// Traits, races, classes, equipment and buffs change it through the <b>Death Threshold</b> and <b>Unconscious Time</b>
/// combat stats. The rules are global (Combat Settings ▸ Downed Rules) unless <see cref="useGlobalRules"/> is off.
/// Put it on the player (Player Setup Validator adds it). Network sessions: the server decides; the owner's give-up is
/// sent as a request.
/// </summary>
[DisallowMultipleComponent]
public class Unconsciousness : MonoBehaviour, IInteractable
{
    [Tooltip("Use the project's rules (Combat Settings ▸ Downed Rules). Off = the rules below.")]
    [SerializeField] private bool useGlobalRules = true;
    [Tooltip("This character's own rules (used when Use Global Rules is off).")]
    [SerializeField] private DownedRules rules = new DownedRules();

    [Header("Player input")]
    [Tooltip("Show the unconscious screen (countdown, Give Up button) to the local player.")]
    [SerializeField] private bool showScreen = true;
    [Tooltip("Give Up action (optional). Empty = an action named GiveUp, else one created on the default controls.")]
    [SerializeField] private InputActionReference giveUpAction;
    [SerializeField] private string[] giveUpActionNames = { "GiveUp", "Give Up" };
    [SerializeField] private string[] giveUpDefaultControls = { "<Keyboard>/x", "<Gamepad>/buttonEast" };

    [Header("Debug")]
    [SerializeField] private bool logChanges;

    private CombatEntity entity;
    private HealthManager health;
    private AvailabilityStateMachine availability;
    private float downedAt;
    private float until;
    private float deficit;
    private CombatEntity knockedOutBy;
    private CombatInputBinding giveUpInput;
    private float giveUpHeld;
    private UnconsciousScreen screen;
    private bool replicatedDown;

    /// <summary>The character was knocked out (attacker, may be null).</summary>
    public event Action<CombatEntity> KnockedOut;
    /// <summary>The character woke up: revived (true) or it died / gave up (false).</summary>
    public event Action<bool> WokeUp;
    /// <summary>Any character was knocked out (victim, attacker).</summary>
    public static event Action<CombatEntity, CombatEntity> AnyKnockedOut;
    /// <summary>Any character was revived (revived, reviver).</summary>
    public static event Action<CombatEntity, CombatEntity> AnyRevived;

    /// <summary>The rules in use.</summary>
    public DownedRules Rules => useGlobalRules || rules == null ? CombatSettings.Instance.downedRules : rules;

    /// <summary>Unconscious right now.</summary>
    public bool IsDown { get; private set; }

    /// <summary>Network layer (owning client): asks the server to give up. Null offline.</summary>
    public Action GiveUpRequest { get; set; }

    public CombatEntity Entity => entity != null ? entity : (entity = CombatEntity.Resolve(gameObject) ?? GetComponentInParent<CombatEntity>());

    /// <summary>Damage below 0 health taken so far (the death threshold counts it).</summary>
    public float Deficit => deficit;

    /// <summary>How far below 0 the character can go before dying (rules + the Death Threshold stat).</summary>
    public float Threshold
    {
        get
        {
            DownedRules r = Rules;
            float t = r.deathThreshold;
            CombatEntity e = Entity;
            if (e != null)
            {
                t += e.MaxHealth * r.deathThresholdPercentOfMaxHealth / 100f;
                if (e.Stats != null) t += e.Stats.Get(CombatStatType.DeathThreshold);
            }
            return Mathf.Max(0f, t);
        }
    }

    /// <summary>Seconds of unconsciousness before death (rules + the Unconscious Time stat).</summary>
    public float Duration
    {
        get
        {
            float d = Rules.unconsciousSeconds;
            CombatEntity e = Entity;
            if (e != null && e.Stats != null) d += e.Stats.Get(CombatStatType.UnconsciousDuration);
            return Mathf.Max(1f, d);
        }
    }

    /// <summary>Seconds left before death (infinite when Bleed Out is off).</summary>
    public float Remaining => !IsDown ? 0f : Rules.bleedOut ? Mathf.Max(0f, until - Time.time) : float.PositiveInfinity;

    /// <summary>Seconds since the knock-out.</summary>
    public float TimeDown => IsDown ? Time.time - downedAt : 0f;

    /// <summary>Who knocked the character out (may be null).</summary>
    public CombatEntity KnockedOutBy => knockedOutBy;

    private void Awake()
    {
        Resolve();
    }

    private void Resolve()
    {
        CombatEntity e = Entity;
        if (e != null)
        {
            e.Downed = this;
            health = e.Health;
            availability = e.Availability;
        }
        if (health == null) health = GetComponentInChildren<HealthManager>();
        if (availability == null) availability = GetComponentInChildren<AvailabilityStateMachine>();
    }

    private void OnEnable()
    {
        Resolve();
    }

    private void OnDisable()
    {
        if (IsDown && Entity != null && !Entity.IsReplica)
            Die("disabled");
        if (entity != null && entity.Downed == this)
            entity.Downed = null;
        giveUpInput?.Dispose();
        giveUpInput = null;
        if (screen != null) screen.Hide();
    }

    private void OnDestroy()
    {
        if (screen != null) Destroy(screen.gameObject);
    }

    // ------------------------------------------------------------------ knock-out (called by CombatEntity)

    /// <summary>
    /// Health reached 0 with <paramref name="overkill"/> damage below 0: knocks the character out, or returns false when it
    /// must die (unconsciousness off, already down, or the overkill passes the death threshold).
    /// </summary>
    public bool TryKnockOut(DamageInfo lethal, float overkill)
    {
        if (!isActiveAndEnabled || IsDown || !Rules.enabled)
            return false;
        CombatEntity e = Entity;
        if (e == null || e.IsReplica)
            return false;
        float threshold = Threshold;
        if (overkill >= threshold)
        {
            if (logChanges) Debug.Log($"[Unconsciousness] {name}: {overkill:0.#} below 0 >= threshold {threshold:0.#}: dies.", this);
            return false;
        }
        GoDown(lethal.source, overkill);
        return true;
    }

    private void GoDown(CombatEntity attacker, float overkill)
    {
        CombatEntity e = Entity;
        IsDown = true;
        deficit = Mathf.Max(0f, overkill);
        downedAt = Time.time;
        until = Time.time + Duration;
        knockedOutBy = attacker;
        giveUpHeld = 0f;
        if (health != null)
        {
            health.SetCurrentValue(0f);
            health.RegenerationBlocked = true;
        }
        SetIncapacitated(true);
        if (e != null && e.Caster != null) e.Caster.Interrupt(CastInterruptReason.Stun, true);
        // Asleep characters wake up (they are now unconscious).
        SleepManager sleep = GetComponentInChildren<SleepManager>();
        if (sleep != null && sleep.IsSleeping) sleep.StopSleeping();
        if (logChanges) Debug.Log($"[Unconsciousness] {name} is unconscious ({deficit:0.#}/{Threshold:0.#} below 0, {Duration:0} s).", this);
        KnockedOut?.Invoke(attacker);
        AnyKnockedOut?.Invoke(e, attacker);
    }

    /// <summary>Damage while unconscious (called by CombatEntity). Returns the damage counted; <paramref name="killed"/> when it passed the threshold.</summary>
    public float TakeDamageWhileDown(DamageInfo info, out bool killed)
    {
        killed = false;
        if (!IsDown)
            return 0f;
        float amount = Mathf.Max(0f, info.amount * Rules.damageWhileDownMultiplier);
        deficit += amount;
        if (deficit >= Threshold)
        {
            killed = true;
            if (logChanges) Debug.Log($"[Unconsciousness] {name} was finished off ({deficit:0.#} below 0).", this);
        }
        return amount;
    }

    /// <summary>A heal while unconscious (called by CombatEntity): true when it may go to health (the character woke up).</summary>
    public bool AcceptHeal(ref float amount, CombatEntity source)
    {
        if (!IsDown)
            return true;
        if (!Rules.healingRevives)
            return false;
        if (amount <= deficit)
        {
            deficit -= amount;
            return false;
        }
        amount -= deficit;
        deficit = 0f;
        WakeUp(true, source);
        return true;
    }

    /// <summary>The character died (called by CombatEntity): unconsciousness ends.</summary>
    internal void OnDeath()
    {
        if (!IsDown)
            return;
        EndDown();
        WokeUp?.Invoke(false);
    }

    /// <summary>The character was revived from death (called by CombatEntity): nothing of the knock-out is left.</summary>
    internal void OnRevived()
    {
        if (IsDown)
            EndDown();
    }

    private void EndDown()
    {
        IsDown = false;
        deficit = 0f;
        giveUpHeld = 0f;
        knockedOutBy = null;
        if (health != null) health.RegenerationBlocked = false;
        SetIncapacitated(false);
    }

    private void SetIncapacitated(bool on)
    {
        if (availability != null) availability.SetIncapacitated(this, on);
    }

    // ------------------------------------------------------------------ revive / give up / die

    /// <summary>
    /// Wakes the character up with <paramref name="healthFraction"/> of its max health (NaN = the rules' Revive Health).
    /// <paramref name="reviver"/> is who did it (may be null). False when it is not unconscious or this is a client's copy.
    /// </summary>
    public bool Revive(float healthFraction = float.NaN, CombatEntity reviver = null)
    {
        if (!IsDown || Entity == null || Entity.IsReplica)
            return false;
        float fraction = float.IsNaN(healthFraction) ? Rules.reviveHealthFraction : Mathf.Clamp(healthFraction, 0.01f, 1f);
        WakeUp(false, reviver);
        if (health != null)
            health.SetCurrentValue(Mathf.Max(1f, health.MaxValue * fraction));
        if (Rules.reviveInvulnerability > 0f)
            Entity.SetInvulnerable(Rules.reviveInvulnerability);
        return true;
    }

    private void WakeUp(bool byHealing, CombatEntity reviver)
    {
        EndDown();
        if (logChanges) Debug.Log($"[Unconsciousness] {name} was revived{(reviver != null ? " by " + reviver.name : "")}{(byHealing ? " (healing)" : "")}.", this);
        WokeUp?.Invoke(true);
        AnyRevived?.Invoke(Entity, reviver);
    }

    /// <summary>The player gives up: dies at once (a client's copy asks the server).</summary>
    public void GiveUp()
    {
        if (!IsDown && !replicatedDown)
            return;
        if (!Rules.allowGiveUp)
            return;
        if (Entity != null && Entity.IsReplica)
        {
            GiveUpRequest?.Invoke();
            return;
        }
        Die("gave up");
    }

    /// <summary>Server / offline: ends the unconsciousness with death.</summary>
    public void Die(string reason = null)
    {
        if (!IsDown)
            return;
        CombatEntity e = Entity;
        CombatEntity killer = knockedOutBy;
        if (logChanges) Debug.Log($"[Unconsciousness] {name} dies ({reason ?? "time ran out"}).", this);
        EndDown();
        WokeUp?.Invoke(false);
        if (e != null)
        {
            if (health != null) health.SetCurrentValue(0f);
            e.HandleDeath(killer);
        }
    }

    // ------------------------------------------------------------------ network (client copies)

    /// <summary>Client copies: shows the server's state (screen, animation, movement lock).</summary>
    public void ApplyReplicated(bool down, float remainingSeconds, float deficitNow, CombatEntity attacker)
    {
        if (Entity == null || !Entity.IsReplica)
            return;
        replicatedDown = down;
        if (down && !IsDown)
        {
            IsDown = true;
            downedAt = Time.time;
            knockedOutBy = attacker;
            SetIncapacitated(true);
            KnockedOut?.Invoke(attacker);
        }
        else if (!down && IsDown)
        {
            IsDown = false;
            SetIncapacitated(false);
            WokeUp?.Invoke(false);
        }
        deficit = deficitNow;
        until = Time.time + (float.IsInfinity(remainingSeconds) || float.IsNaN(remainingSeconds) ? 99999f : remainingSeconds);
    }

    // ------------------------------------------------------------------ update

    private void Update()
    {
        CombatEntity e = Entity;
        if (e == null)
            return;
        bool rulesHere = !e.IsReplica;
        if (IsDown && rulesHere)
        {
            if (Rules.bleedOut && Time.time >= until)
            {
                Die();
                return;
            }
        }

        bool local = IsLocalPlayer(e);
        if (local)
        {
            if (IsDown && Rules.allowGiveUp)
            {
                if (giveUpInput == null)
                    giveUpInput = CombatInputBinding.Create(this, giveUpAction, giveUpActionNames, giveUpDefaultControls, "Give Up");
                if (giveUpInput != null && giveUpInput.Held)
                {
                    giveUpHeld += Time.deltaTime;
                    if (giveUpHeld >= Rules.giveUpHoldSeconds)
                    {
                        giveUpHeld = 0f;
                        GiveUp();
                    }
                }
                else giveUpHeld = 0f;
            }
            if (showScreen)
            {
                if (IsDown && screen == null)
                    screen = UnconsciousScreen.Create(this);
                if (screen != null) screen.Tick();
            }
        }
    }

    /// <summary>Fraction (0-1) of the Give Up hold done (the screen shows it).</summary>
    public float GiveUpProgress => Rules.giveUpHoldSeconds > 0f ? Mathf.Clamp01(giveUpHeld / Rules.giveUpHoldSeconds) : 0f;

    /// <summary>The key of the Give Up input ("X").</summary>
    /// <summary>
    /// The Give Up key for the device in use, read from the action each time (so a key changed in the action map or the
    /// Key Bindings menu shows at once).
    /// </summary>
    public string GiveUpKey
    {
        get
        {
            if (giveUpInput == null || giveUpInput.ReadAction == null)
                return "X";
            string s = InputDeviceTracker.DisplayString(giveUpInput.ReadAction);
            if (string.IsNullOrEmpty(s))
                s = giveUpInput.ReadAction.GetBindingDisplayString(0);
            return HudStyle.ShortKey(s);
        }
    }

    private static bool IsLocalPlayer(CombatEntity e)
    {
        if (e == null || e.Kind != CombatEntity.EntityKind.Player)
            return false;
        if (!GameNet.NetAuthority.IsNetworkedObject(e.gameObject))
            return GameNet.NetAuthority.HasLocalPlayer;
        return GameNet.NetAuthority.IsLocallyControlled(e.gameObject);
    }

    // ------------------------------------------------------------------ revive by interacting (other players)

    public string InteractionName => Entity != null ? Entity.name : name;
    public string InteractionVerb => "Revive";
    public Transform InteractionPoint => transform;
    public float InteractionRange => 2.5f;
    public int InteractionPriority => 20;
    public float HoldDuration => Rules.reviveHoldSeconds;
    public bool AllowsClick => false;

    public bool CanInteract(PlayerInteractor interactor, out string reason)
    {
        reason = "";
        if (!IsDown || !Rules.reviveByInteracting || interactor == null)
            return false;
        CombatEntity me = CombatEntity.Resolve(interactor.PlayerObject);
        if (me == null || me == Entity || me.IsDead || me.IsUnconscious)
            return false;
        if (CombatRelations.IsEnemy(me, Entity))
        {
            reason = "Enemy";
            return false;
        }
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor, out _))
            return;
        Revive(float.NaN, CombatEntity.Resolve(interactor.PlayerObject));
    }

    public void OnFocusChanged(PlayerInteractor interactor, bool focused) { }

    private void OnValidate()
    {
        if (rules == null) rules = new DownedRules();
    }
}
