using System;
using System.Collections.Generic;
using UnityEngine;

// Disabilities: the parts of a trait that take something away for good - an action the character cannot do, an eye, its
// hearing, a limb, an appetite it cannot keep up with - or now and then, at random (episodes: a migraine, a sleep
// attack, a knee that gives way). Put them on a trait with a negative cost: it then gives trait points to spend on
// other traits. They work with the injury system on or off; with it on, a missing limb is really missing.

/// <summary>Shared helpers of the disability behaviours.</summary>
internal static class DisabilityUtil
{
    /// <summary>Does this machine run the game rules for the character (offline, the server, the host)?</summary>
    public static bool Rules(TraitContext ctx)
    {
        CombatEntity e = ctx.Entity;
        return e != null && !e.IsReplica;
    }

    /// <summary>Is the character this machine's player?</summary>
    public static bool IsLocal(TraitContext ctx)
    {
        GameObject root = ctx.Root != null ? ctx.Root.gameObject : null;
        if (root == null)
            return false;
        return GameNet.NetAuthority.IsNetworkedObject(root)
            ? GameNet.NetAuthority.IsLocallyControlled(root)
            : GameNet.NetAuthority.HasLocalPlayer;
    }

    /// <summary>Tells the local player something (nothing on other machines).</summary>
    public static void Tell(TraitContext ctx, string message, bool warning = true)
    {
        if (string.IsNullOrEmpty(message) || !IsLocal(ctx))
            return;
        InventoryManager inventory = InventoryManager.For(ctx.Root);
        if (inventory != null)
            inventory.ShowMessage(message, warning ? InventoryFeedback.Kind.Warning : InventoryFeedback.Kind.Info);
    }

    /// <summary>How much daylight there is (0 night - 1 day); -1 where there is no day and night.</summary>
    public static float Daylight()
    {
        DayNightCycle cycle = DayNightCycle.Instance;
        if (cycle != null && cycle.isActiveAndEnabled)
            return cycle.Daylight;
        // Another clock (the Weather System's day, a Day Night Manager): day between sunrise and sunset.
        float hour = WorldTime.Hour;
        if (hour < 0f)
            return -1f;
        float rise = WorldTime.SunriseHour, set = WorldTime.SunsetHour;
        return Mathf.Clamp01(Mathf.Min(Mathf.InverseLerp(rise - 0.75f, rise + 0.75f, hour), 1f - Mathf.InverseLerp(set - 0.75f, set + 0.75f, hour)));
    }

    /// <summary>
    /// Is the sky open above the character: nothing solid within 60 m over its head? Under a roof, in a cave or a
    /// dungeon the sun does not reach it, whatever the hour.
    /// </summary>
    public static bool UnderOpenSky(TraitContext ctx)
    {
        Transform body = ctx.Body;
        if (body == null)
            return true;
        float head = ctx.Controller != null ? ctx.Controller.height * Mathf.Abs(body.lossyScale.y) : 2f;
        Vector3 from = body.position + Vector3.up * (head + 0.3f);
        if (!ScenePhysics.Raycast(from, Vector3.up, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return true;
        return ctx.Root != null && hit.transform.IsChildOf(ctx.Root);
    }

    /// <summary>The sunlight that reaches the character (0-1): the daylight, and none of it under a roof; -1 where there is no day and night.</summary>
    public static float Sunlight(TraitContext ctx, ref float checkedAt, ref bool openSky)
    {
        float day = Daylight();
        if (day <= 0f)
            return day;
        if (Time.unscaledTime >= checkedAt)
        {
            checkedAt = Time.unscaledTime + 0.5f;
            openSky = UnderOpenSky(ctx);
        }
        return openSky ? day : 0f;
    }
}

// ====================================================================== cannot do
[Serializable, AbilityMenu("Disability/Cannot Do", "The character can never do the chosen actions: sprint, jump, dash, roll, attack with weapons, hold two-handed weapons, block, cast abilities...", 0)]
public class BlockActionsTrait : TraitBehaviour
{
    [Tooltip("What the character cannot do while it has the trait.")]
    public BodyAction actions = BodyAction.Sprint;
    [Tooltip("Why (\"A bad leg\"): shown when an item, a held item or an attack is refused (movement, blocking and " +
             "casting are refused without a message). Empty = nothing is said.")]
    public string reason = "";

    [NonSerialized] private CombatEntity applied;

    public override string Describe() => BodyActions.DescribeAll(actions) + ".";

    public override string LiveStatus => applied != null ? "applied" : "waiting for the character";

    public override void OnAdded(TraitContext ctx) => Apply(ctx);

    public override void Tick(TraitContext ctx, float dt)
    {
        // The combat entity may appear after the trait.
        if (applied == null)
            Apply(ctx);
    }

    private void Apply(TraitContext ctx)
    {
        CombatEntity e = ctx.Entity;
        if (e == null || actions == BodyAction.None)
            return;
        e.SetActionBlock(this, actions, reason);
        applied = e;
        if ((actions & (BodyAction.MainHand | BodyAction.OffHand | BodyAction.TwoHandedWeapons)) != 0)
            InventoryManager.For(ctx.Root)?.RefreshHands();
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (applied == null)
            return;
        applied.ClearActionBlock(this);
        bool hands = (actions & (BodyAction.MainHand | BodyAction.OffHand | BodyAction.TwoHandedWeapons)) != 0;
        applied = null;
        if (hands)
            InventoryManager.For(ctx.Root)?.RefreshHands();
    }

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (actions == BodyAction.None)
            warnings.Add("Cannot Do: no action is chosen (the behaviour does nothing).");
    }
}

// ====================================================================== sight
/// <summary>When an impaired sight shows.</summary>
public enum SightWhen
{
    /// <summary>All the time.</summary>
    Always,
    /// <summary>In the dark: the darker the night, the more (night blindness). Nothing where there is no day and night.</summary>
    InTheDark,
    /// <summary>In daylight: the brighter the day, the more (eyes that cannot stand the light).</summary>
    InDaylight,
}

[Serializable, AbilityMenu("Disability/Impaired Sight", "The player sees less: a blind eye darkens its half of the screen, poor eyesight hazes it, tunnel vision closes the edges in; always, only in the dark or only in daylight.", 1)]
public class VisionImpairmentTrait : TraitBehaviour
{
    [Tooltip("Which eye the Darkness is in: Left or Right for one eye (that half of the screen), Center for both.")]
    public BodyPartSide eye = BodyPartSide.Center;
    [Tooltip("What it does to the screen: Darkness (the eye's half goes dark; 1 = that eye is blind), Haze (blurred), Tunnel " +
             "(the edges close in), Sway (dizzy).")]
    public VisionEffect sight = new VisionEffect(0f, 0.3f);
    [Tooltip("When it shows.")]
    public SightWhen when = SightWhen.Always;

    [NonSerialized] private DisabilityEffects effects;
    [NonSerialized] private float nextCheck;
    [NonSerialized] private float skyCheckedAt;
    [NonSerialized] private bool openSky = true;
    [NonSerialized] private float lastStrength;

    public override string Describe()
    {
        var parts = new List<string>();
        if (sight != null)
        {
            string side = eye == BodyPartSide.Center ? "" : eye.ToString().ToLowerInvariant() + " ";
            if (sight.darkness >= 0.95f) parts.Add(eye == BodyPartSide.Center ? "blind" : $"blind in the {side}eye");
            else if (sight.darkness > 0f) parts.Add($"{side}sight {sight.darkness * 100f:0}% darker");
            if (sight.haze > 0f) parts.Add("blurred");
            if (sight.tunnel > 0f) parts.Add("tunnel vision");
            if (sight.sway > 0f) parts.Add("dizzy");
        }
        string suffix = when == SightWhen.InTheDark ? " in the dark" : when == SightWhen.InDaylight ? " in daylight" : "";
        return $"Sight: {(parts.Count > 0 ? string.Join(", ", parts) : "unchanged")}{suffix}.";
    }

    public override string LiveStatus => effects != null ? $"sight {lastStrength * 100f:0}%" : "inactive";

    private float Strength(TraitContext ctx)
    {
        if (when == SightWhen.Always)
            return multiplier;
        if (when == SightWhen.InDaylight)
            return Mathf.Max(0f, DisabilityUtil.Sunlight(ctx, ref skyCheckedAt, ref openSky)) * multiplier;
        float day = DisabilityUtil.Daylight();
        return day < 0f ? 0f : (1f - day) * multiplier;
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + 0.25f;
        if (effects == null)
            effects = DisabilityEffects.For(ctx.Root);
        lastStrength = Strength(ctx);
        if (effects != null)
            effects.SetSight(this, eye, sight, lastStrength);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (effects != null)
            effects.ClearSight(this);
        effects = null;
    }

    protected override bool ReadsStrengthLive => true;

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (sight == null || !sight.HasAny)
            warnings.Add("Impaired Sight: every amount is 0 (the behaviour does nothing).");
    }
}

// ====================================================================== hearing
[Serializable, AbilityMenu("Disability/Impaired Hearing", "The player hears less: the game's volume is lowered for them, down to silence (deaf).", 2)]
public class HearingImpairmentTrait : TraitBehaviour
{
    [Tooltip("How much of the sound is left: 0.35 = hard of hearing, 0 = deaf.")]
    [Range(0f, 1f)] public float volume = 0.35f;

    [NonSerialized] private DisabilityEffects effects;

    public override string Describe() => volume <= 0.01f ? "Deaf: hears nothing." : $"Hears {volume * 100f:0}% of the sound.";

    public override void Tick(TraitContext ctx, float dt)
    {
        if (effects == null)
            effects = DisabilityEffects.For(ctx.Root);
        if (effects != null)
            effects.SetHearing(this, volume);
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (effects != null)
            effects.ClearHearing(this);
        effects = null;
    }
}

// ====================================================================== needs
[Serializable, AbilityMenu("Disability/Needs Drain Faster", "Hunger, thirst or rest run out faster (or slower): a hearty appetite, a constant thirst, a sleepyhead.", 3)]
public class NeedRatesTrait : TraitBehaviour
{
    [Tooltip("Hunger drains this many times as fast (1 = normal, 1.5 = half again as fast, 0.75 = slower).")]
    [Min(0f)] public float hunger = 1f;
    [Tooltip("Thirst drains this many times as fast.")]
    [Min(0f)] public float thirst = 1f;
    [Tooltip("Rest is lost this many times as fast while awake.")]
    [Min(0f)] public float rest = 1f;

    [NonSerialized] private PlayerStatusController applied;

    public override string Describe()
    {
        var parts = new List<string>();
        void Add(string need, float m)
        {
            float k = Mathf.Lerp(1f, m, multiplier);
            if (!Mathf.Approximately(k, 1f))
                parts.Add($"{need} x{k:0.##}");
        }
        Add("hunger", hunger);
        Add("thirst", thirst);
        Add("tiredness", rest);
        return parts.Count > 0 ? "Needs: " + string.Join(", ", parts) + "." : "Needs: unchanged.";
    }

    public override void OnAdded(TraitContext ctx) => Apply(ctx);

    public override void Tick(TraitContext ctx, float dt)
    {
        if (applied == null)
            Apply(ctx);
    }

    private void Apply(TraitContext ctx)
    {
        PlayerStatusController s = ctx.Status;
        if (s == null)
            return;
        if (s.HungerManager != null) s.HungerManager.SetRateMultiplier(this, Mathf.Lerp(1f, hunger, multiplier));
        if (s.ThirstManager != null) s.ThirstManager.SetRateMultiplier(this, Mathf.Lerp(1f, thirst, multiplier));
        if (s.SleepManager != null) s.SleepManager.SetRateMultiplier(this, Mathf.Lerp(1f, rest, multiplier));
        applied = s;
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (applied == null)
            return;
        applied.HungerManager?.ClearRateMultiplier(this);
        applied.ThirstManager?.ClearRateMultiplier(this);
        applied.SleepManager?.ClearRateMultiplier(this);
        applied = null;
    }
}

// ====================================================================== a missing limb
[Serializable, AbilityMenu("Disability/Missing Limb", "The character has no hand, arm, foot or leg. With the injury system on the limb is really missing (hidden on the model, the hand unusable, hopping or crawling); without it, the chosen actions are blocked instead.", 4)]
public class MissingLimbTrait : TraitBehaviour
{
    [Tooltip("The body part that is missing, by its id in the Injury Settings (LeftHand, LeftForearm, LeftArm, RightFoot, RightShin, " +
             "RightLeg...). Everything attached to it is missing too.")]
    public string part = "LeftForearm";
    [Tooltip("A part named Left... or Right... is taken on the side of the OFF hand instead (Injury Settings ▸ Main Hand Side), so " +
             "a missing hand never takes the hand that holds the weapon, in a right-handed game or a left-handed one. Off = exactly " +
             "the part named.")]
    public bool onTheOffHandSide = false;
    [Tooltip("Without the injury system (or when it does not know the part): what the character cannot do instead.")]
    public BodyAction withoutInjurySystem = BodyAction.TwoHandedWeapons | BodyAction.OffHand;
    [Tooltip("Why (\"You have one arm\"), used only without the injury system: shown when an item, a held item or " +
             "an attack is refused (movement and blocking are refused without a message).")]
    public string reason = "";

    [NonSerialized] private CombatEntity blocked;
    [NonSerialized] private float nextCheck;
    [NonSerialized] private bool real;

    public override string Describe() => $"Missing {Nice(part)}{(withoutInjurySystem != BodyAction.None ? " (" + BodyActions.DescribeAll(withoutInjurySystem).ToLowerInvariant() + ")" : "")}.";

    public override string LiveStatus => real ? "missing (injury system)" : blocked != null ? "actions blocked" : "waiting";

    private static string Nice(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "limb";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < id.Length; i++)
        {
            if (i > 0 && char.IsUpper(id[i]))
                sb.Append(' ');
            sb.Append(char.ToLowerInvariant(id[i]));
        }
        return sb.ToString();
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        if (Time.time < nextCheck)
            return;
        nextCheck = Time.time + 1f;
        CombatEntity e = ctx.Entity;
        if (e == null)
            return;

        // The injury system knows the part: it is really missing (and stays so after a full heal or a respawn).
        InjuryController injuries = InjurySettings.IsEnabled ? InjuryController.For(ctx.Root) : null;
        InjuryPartState state = injuries != null && injuries.Settings.enabled ? injuries.GetPart(PartFor(injuries.Settings)) : null;
        real = state != null;
        if (real)
        {
            if (!state.Lost && injuries.RulesHere && !e.IsDead)
                injuries.RemovePartForGood(state);
            Unblock(ctx);
            return;
        }
        if (blocked == null && withoutInjurySystem != BodyAction.None)
        {
            e.SetActionBlock(this, withoutInjurySystem, reason);
            blocked = e;
            InventoryManager.For(ctx.Root)?.RefreshHands();
        }
    }

    /// <summary>The part id that is missing: the one named, or the same part on the off-hand side.</summary>
    public string PartFor(InjurySettings settings)
    {
        if (!onTheOffHandSide || settings == null || string.IsNullOrEmpty(part))
            return part;
        string off = settings.mainHandSide == BodyPartSide.Left ? "Right" : "Left";
        if (part.StartsWith("Left", StringComparison.Ordinal)) return off + part.Substring(4);
        if (part.StartsWith("Right", StringComparison.Ordinal)) return off + part.Substring(5);
        return part;
    }

    private void Unblock(TraitContext ctx)
    {
        if (blocked == null)
            return;
        blocked.ClearActionBlock(this);
        blocked = null;
        InventoryManager.For(ctx.Root)?.RefreshHands();
    }

    public override void OnRemoved(TraitContext ctx)
    {
        Unblock(ctx);
        // The limb comes back with the trait gone (a blessing took the disability away).
        InjuryController injuries = InjurySettings.IsEnabled ? InjuryController.For(ctx.Root) : null;
        InjuryPartState state = injuries != null ? injuries.GetPart(PartFor(injuries.Settings)) : null;
        if (state != null && state.Lost && injuries.RulesHere)
            injuries.GiveBackPart(state);
        real = false;
    }

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(part))
            errors.Add("Missing Limb: no body part id.");
    }
}

// ====================================================================== episodes
/// <summary>What brings the episodes of a disability on.</summary>
public enum EpisodeTrigger
{
    /// <summary>Nothing in particular: they come at any time.</summary>
    Anytime,
    /// <summary>While fighting (took or dealt damage in the last seconds).</summary>
    InCombat,
    /// <summary>While not fighting.</summary>
    OutOfCombat,
    Sprinting,
    Moving,
    StandingStill,
    /// <summary>Jumping or falling.</summary>
    InTheAir,
    /// <summary>Health below 35%.</summary>
    LowHealth,
    /// <summary>Stamina below 25%.</summary>
    LowStamina,
    /// <summary>While casting an ability.</summary>
    Casting,
    Swimming,
    /// <summary>At night (where there is a day and night).</summary>
    AtNight,
    /// <summary>In daylight (where there is a day and night).</summary>
    InDaylight,
    /// <summary>While tired (the Sleep Manager's conditions: Tired or worse).</summary>
    WhileTired,
    /// <summary>While hungry or thirsty (their conditions: Hungry / Thirsty or worse).</summary>
    WhileHungryOrThirsty,
    // Added later (kept at the end so existing assets keep their values)
    /// <summary>While bleeding (injury system).</summary>
    Bleeding,
    /// <summary>While the body is cold (Body Heat: Cold or colder).</summary>
    WhileCold,
}

/// <summary>
/// A disability that comes and goes: now and then, at random, an episode starts - a migraine, a sleep attack, a knee that
/// gives way, a fit of coughing - with a strength picked each time, runs for a while and passes. Or (Average Minutes
/// Between 0) a condition that is always there and only changes strength: a chronic pain with good hours and bad ones.
/// While it lasts: a status with its stats and per-second effects scaled by the strength, the sight and the hearing it
/// takes, and optionally the character is out for a moment or for all of it.
/// <para>The game rules decide (offline, the server); clients show the episode from the status the server sends, whose
/// name says how bad it is ("Mild Migraine", "Severe Migraine").</para>
/// </summary>
[Serializable, AbilityMenu("Disability/Episodes (comes and goes)", "Now and then, at random, an episode starts (a migraine, a sleep attack, a flare-up of pain) with a strength picked each time; or a condition that is always there and only changes strength.", 5)]
public class EpisodicDisabilityTrait : TraitBehaviour
{
    [Tooltip("Name of the episode: its status icon (\"Mild Migraine\", \"Migraine\", \"Severe Migraine\" by its strength).")]
    public string label = "Migraine";
    [Tooltip("What brings episodes on: they only start while it holds.")]
    public EpisodeTrigger broughtOnBy = EpisodeTrigger.Anytime;
    [Tooltip("The episode ends as soon as what brought it on stops (sun sickness ends in the shade). Off = it runs its course.")]
    public bool endsWhenItStops = false;
    [Tooltip("How often, how long and how strong.")]
    public EpisodeTiming timing = new EpisodeTiming();

    [Header("While it lasts (scaled by the episode's strength)")]
    [Tooltip("Stats and per-second effects of an episode at full strength. Its Label is ignored (the episode's name is used).")]
    public StatusConsequence effect = new StatusConsequence("", true);
    [Tooltip("What it does to the player's sight at full strength.")]
    public VisionEffect sight = new VisionEffect();
    [Tooltip("How much of the sound is left at full strength (1 = hearing is not touched).")]
    [Range(0f, 1f)] public float hearing = 1f;

    [Header("When it starts")]
    [Tooltip("The character is stunned for this many seconds when an episode starts (a stumble, a fit). 0 = not.")]
    [Min(0f)] public float stunSeconds = 0f;
    [Tooltip("The character is out for the whole episode (a faint, a sleep attack) instead.")]
    public bool outForAllOfIt = false;
    [Tooltip("An ability being cast is interrupted when an episode starts.")]
    public bool interruptsCasting = false;
    [Tooltip("Told to the player when an episode starts / ends (empty = nothing).")]
    public string startMessage = "";
    public string endMessage = "";
    [Tooltip("Heard when an episode starts, by everyone near the character: a cough, a sneeze, a groan (empty = nothing).")]
    public AudioCue startSound;

    [NonSerialized] private EpisodeSchedule schedule;
    [NonSerialized] private ConsequenceRunner runner;
    [NonSerialized] private DisabilityEffects effects;
    [NonSerialized] private float shown;
    [NonSerialized] private float nextViewCheck;
    [NonSerialized] private float viewSeverity;
    [NonSerialized] private float skyCheckedAt;
    [NonSerialized] private bool openSky = true;
    [NonSerialized] private InjuryController injuries;
    [NonSerialized] private float nextInjurySearch;

    public override string Describe()
    {
        var parts = new List<string>();
        if (effect != null)
        {
            string e = effect.Describe(multiplier);
            if (e != "no effect") parts.Add(e);
        }
        if (sight != null && sight.HasAny) parts.Add(sight.Describe());
        if (hearing < 0.999f) parts.Add(hearing <= 0.01f ? "deaf" : $"hears {hearing * 100f:0}%");
        if (outForAllOfIt) parts.Add("out cold for all of it");
        else if (stunSeconds > 0f) parts.Add($"stunned {stunSeconds:0.#} s when it starts");
        if (interruptsCasting) parts.Add("interrupts casting");
        string when = broughtOnBy == EpisodeTrigger.Anytime ? "" : $" while {TriggerText()}";
        return $"{label}{when}, {timing.Describe()}: {(parts.Count > 0 ? string.Join(", ", parts) : "no effect")}.";
    }

    public override string LiveStatus
    {
        get
        {
            if (schedule == null) return shown > 0f ? $"episode ({shown * 100f:0}%)" : "quiet";
            return schedule.Active ? $"EPISODE {schedule.Severity * 100f:0}%, {schedule.Remaining:0} s left" : schedule.GapLeft > 0f ? $"quiet ({schedule.GapLeft:0} s of peace)" : "quiet";
        }
    }

    private string TriggerText()
    {
        switch (broughtOnBy)
        {
            case EpisodeTrigger.InCombat: return "fighting";
            case EpisodeTrigger.OutOfCombat: return "not fighting";
            case EpisodeTrigger.Sprinting: return "sprinting";
            case EpisodeTrigger.Moving: return "moving";
            case EpisodeTrigger.StandingStill: return "standing still";
            case EpisodeTrigger.InTheAir: return "in the air";
            case EpisodeTrigger.LowHealth: return "badly hurt";
            case EpisodeTrigger.LowStamina: return "out of breath";
            case EpisodeTrigger.Casting: return "casting";
            case EpisodeTrigger.Swimming: return "swimming";
            case EpisodeTrigger.AtNight: return "it is night";
            case EpisodeTrigger.InDaylight: return "in daylight";
            case EpisodeTrigger.WhileTired: return "tired";
            case EpisodeTrigger.WhileHungryOrThirsty: return "hungry or thirsty";
            case EpisodeTrigger.Bleeding: return "bleeding";
            case EpisodeTrigger.WhileCold: return "cold";
            default: return "anything";
        }
    }

    private bool Holds(TraitContext ctx)
    {
        switch (broughtOnBy)
        {
            case EpisodeTrigger.InCombat: return ctx.InCombat(6f);
            case EpisodeTrigger.OutOfCombat: return !ctx.InCombat(6f);
            case EpisodeTrigger.Sprinting: return ctx.MovementState == MovementStateMachine.EMovementState.Running;
            case EpisodeTrigger.Moving: return ctx.MoveInput.sqrMagnitude >= 0.01f;
            case EpisodeTrigger.StandingStill: return ctx.MoveInput.sqrMagnitude < 0.01f;
            case EpisodeTrigger.InTheAir: return !ctx.IsGrounded;
            case EpisodeTrigger.LowHealth: return ctx.HealthRatio < 0.35f;
            case EpisodeTrigger.LowStamina:
                StaminaManager st = ctx.Status != null ? ctx.Status.StaminaManager : null;
                return st != null && st.MaxValue > 0f && st.CurrentValue / st.MaxValue < 0.25f;
            case EpisodeTrigger.Casting: return ctx.IsCasting;
            case EpisodeTrigger.Swimming: return ctx.MovementState == MovementStateMachine.EMovementState.Swimming;
            case EpisodeTrigger.AtNight:
            {
                float day = DisabilityUtil.Daylight();
                return day >= 0f && day < 0.25f;
            }
            case EpisodeTrigger.InDaylight: return DisabilityUtil.Sunlight(ctx, ref skyCheckedAt, ref openSky) > 0.6f;
            case EpisodeTrigger.Bleeding:
            {
                if (injuries == null && Time.time >= nextInjurySearch)
                {
                    nextInjurySearch = Time.time + 2f;
                    injuries = InjurySettings.IsEnabled ? InjuryController.For(ctx.Root) : null;
                }
                return injuries != null && injuries.IsBleeding;
            }
            case EpisodeTrigger.WhileCold:
            {
                BodyHeatManager heat = ctx.Status != null ? ctx.Status.BodyHeatManager : null;
                if (heat == null || !heat.isActiveAndEnabled)
                    return false;
                BodyHeatManager.TemperatureLevel level = heat.CurrentTemperatureLevel;
                return level == BodyHeatManager.TemperatureLevel.Cold || level == BodyHeatManager.TemperatureLevel.ExtremelyCold;
            }
            case EpisodeTrigger.WhileTired:
            {
                SleepManager sleep = ctx.Status != null ? ctx.Status.SleepManager : null;
                return sleep != null && sleep.isActiveAndEnabled && sleep.CurrentSleepinessLevel >= SleepManager.SleepinessLevel.ModeratelySleepy;
            }
            case EpisodeTrigger.WhileHungryOrThirsty:
            {
                PlayerStatusController s = ctx.Status;
                return s != null && (Bad(s.HungerManager) || Bad(s.ThirstManager));
            }
            default: return true;
        }
    }

    private static bool Bad(NeedStatusManager need)
    {
        if (need == null || !need.isActiveAndEnabled)
            return false;
        NeedStage stage = need.CurrentStage;
        return stage != null ? stage.mood >= NeedMood.Bad : need.IsEmpty;
    }

    public override void Tick(TraitContext ctx, float dt)
    {
        CombatEntity e = ctx.Entity;
        if (e == null)
            return;
        float severity;
        if (!e.IsReplica)
        {
            RulesTick(ctx, e, dt);
            severity = schedule.Active ? schedule.Severity : 0f;
        }
        else
        {
            // A client's copy: the server's status says an episode runs, and its name how bad it is.
            if (Time.unscaledTime >= nextViewCheck)
            {
                nextViewCheck = Time.unscaledTime + 0.25f;
                viewSeverity = ReadView(e);
            }
            severity = viewSeverity;
        }
        Show(ctx, severity);
    }

    private void RulesTick(TraitContext ctx, CombatEntity e, float dt)
    {
        if (schedule == null)
        {
            schedule = new EpisodeSchedule();
            runner = new ConsequenceRunner(ctx.Manager, this);
        }
        SleepManager sleep = ctx.Status != null ? ctx.Status.SleepManager : null;
        bool down = e.IsDead || e.IsUnconscious || (sleep != null && sleep.IsSleeping);
        if (down)
        {
            if (schedule.Active)
                EndNow(0f);
            return;
        }
        bool holds = Holds(ctx);
        if (schedule.Active && endsWhenItStops && !holds)
            EndNow(timing.Continuous ? 0f : timing.minimumGapSeconds);

        int what = schedule.Tick(timing, dt, holds, () => UnityEngine.Random.value);
        // A condition that is always there: the next stretch begins as the last one ends, with a new strength.
        if (what == EpisodeSchedule.Ended && timing.Continuous && holds)
            what = schedule.Tick(timing, 0.001f, true, () => UnityEngine.Random.value) == EpisodeSchedule.Started ? EpisodeSchedule.Started : EpisodeSchedule.Ended;
        if (what == EpisodeSchedule.Started)
            Begin(e);
        else if (what == EpisodeSchedule.Ended)
            runner.Clear();
        runner.Tick(dt);
    }

    private void Begin(CombatEntity e)
    {
        float severity = schedule.Severity * multiplier;
        if (severity <= 0.01f)
        {
            // A quiet spell of a condition that comes and goes: nothing to show.
            runner.Clear();
            return;
        }
        StatusConsequence c = effect != null ? effect.Clone() : new StatusConsequence("", true);
        c.label = EpisodeSchedule.TierName(label, schedule.Severity, timing.severity);
        runner.Set(c, severity);
        if (outForAllOfIt)
            e.ApplyControl(ControlType.Stun, schedule.Duration, null);
        else if (stunSeconds > 0f)
            e.ApplyControl(ControlType.Stun, stunSeconds * Mathf.Lerp(0.5f, 1f, schedule.Severity), null);
        if (interruptsCasting && e.Caster != null)
            e.Caster.Interrupt(CastInterruptReason.Stun, true);
    }

    private void EndNow(float gap)
    {
        schedule.End(gap);
        runner?.Clear();
    }

    /// <summary>The strength of the episode the server's statuses show (0 = none).</summary>
    private float ReadView(CombatEntity e)
    {
        TimedStatModifiers mods = e.GetComponent<TimedStatModifiers>();
        if (mods == null)
            return 0f;
        IReadOnlyList<TimedStatModifiers.Active> all = mods.All;
        for (int i = 0; i < all.Count; i++)
            if (EpisodeSchedule.TryReadTier(all[i].name, label, timing.severity, out float s))
                return s * multiplier;
        return 0f;
    }

    /// <summary>What the player perceives of it (any copy): the sight, the hearing, and a word when it starts and ends.</summary>
    private void Show(TraitContext ctx, float severity)
    {
        bool was = shown > 0.01f, now = severity > 0.01f;
        shown = severity;
        if (now != was)
        {
            DisabilityUtil.Tell(ctx, now ? startMessage : endMessage, now);
            if (now && startSound != null && ctx.Root != null)
                startSound.Play(ctx.Root.gameObject);
        }
        bool perceived = (sight != null && sight.HasAny) || hearing < 0.999f;
        if (!perceived)
            return;
        if (effects == null)
            effects = DisabilityEffects.For(ctx.Root);
        if (effects == null)
            return;
        effects.SetSight(this, BodyPartSide.Center, sight, severity);
        effects.SetHearing(this, Mathf.Lerp(1f, hearing, Mathf.Clamp01(severity)));
    }

    public override void OnRemoved(TraitContext ctx)
    {
        if (schedule != null && schedule.Active)
            EndNow(0f);
        runner?.Clear();
        if (effects != null)
        {
            effects.ClearSight(this);
            effects.ClearHearing(this);
        }
        effects = null;
        shown = 0f;
    }

    protected override bool ReadsStrengthLive => true;

    public override void Validate(List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(label))
            errors.Add("Episodes: no label (the episode's status needs a name; clients know an episode runs by it).");
        if (timing == null)
        {
            errors.Add("Episodes: no timing.");
            return;
        }
        if (timing.durationSeconds.y < timing.durationSeconds.x)
            warnings.Add($"Episodes ({label}): the longest duration is shorter than the shortest.");
        if (timing.severity.y < timing.severity.x)
            warnings.Add($"Episodes ({label}): the highest severity is lower than the lowest.");
        bool does = (effect != null && effect.Describe() != "no effect") || (sight != null && sight.HasAny) || hearing < 0.999f || outForAllOfIt || stunSeconds > 0f || interruptsCasting;
        if (!does)
            warnings.Add($"Episodes ({label}): an episode does nothing.");
    }
}
