using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>How an ability chooses its target and aim.</summary>
[Serializable]
public class AbilityTargeting
{
    [Tooltip("Self: centred on the caster (novas, buffs, stomps).\nUnit: needs a target character.\nPoint: a spot on the ground within Range (meteors, cages, ground surges).\nDirection: fired from the caster toward the aim (projectiles, cones, beams, charges).")]
    public AbilityTargetingMode mode = AbilityTargetingMode.Direction;

    [Tooltip("Maximum distance to the target or aimed point (metres). Mobs only start the cast inside this range.")]
    [Min(0f)] public float range = 10f;

    [Tooltip("Minimum distance. Mobs will not use it closer than this (e.g. artillery that cannot hit point blank). 0 = none.")]
    [Min(0f)] public float minRange = 0f;

    [Tooltip("Who can be chosen as the target (Unit mode and mob target checks).")]
    public TargetFilter targetFilter = TargetFilter.Enemies;

    [Tooltip("The caster needs a clear line of sight to the target to start the cast.")]
    public bool requireLineOfSight = true;

    [Tooltip("Lock At Cast Start: the aim is fixed when the cast starts (dodgeable telegraphs).\nTrack Until Release: the aim follows the target while casting (limited by Aim Turn Rate).\nFollow Caster: stays attached to the caster (spins, auras, frontal cleaves).")]
    public AbilityAimLock aimLock = AbilityAimLock.TrackUntilRelease;

    [Tooltip("Degrees per second the aim can turn while tracking (0 = instant). Low values let targets dodge by moving sideways.")]
    [Min(0f)] public float aimTurnRate = 180f;

    [Tooltip("0-1: aim where the target will be when the ability lands (uses its velocity) instead of where it is now. Used by mobs for projectiles and ground targets.")]
    [Range(0f, 1f)] public float leadTarget = 0.5f;

    [Tooltip("The caster turns to face the aim while casting.")]
    public bool faceAim = true;

    [Tooltip("Players: when nothing is aimed at, pick the enemy closest to the crosshair within this angle (degrees). 0 = off.")]
    [Range(0f, 90f)] public float autoAimAngle = 20f;

    [Tooltip("Players: pressing the key shows a preview of the area; left click (or the key again) confirms, right click cancels.")]
    public bool playerConfirmsTarget = false;

    [Tooltip("Point mode: keep the aimed point on the ground.")]
    public bool snapToGround = true;

    [Tooltip("If the target gets farther than Range + this margin while casting, the cast is cancelled. 0 = never cancel.")]
    [Min(0f)] public float cancelRangeMargin = 0f;

    public void Validate(string owner, List<string> errors, List<string> warnings)
    {
        if (mode != AbilityTargetingMode.Self && range <= 0f)
            errors.Add($"{owner}: Targeting Range must be above 0 for {mode} abilities.");
        if (minRange > 0f && minRange >= range && mode != AbilityTargetingMode.Self)
            errors.Add($"{owner}: Min Range ({minRange}) must be smaller than Range ({range}).");
        if (mode == AbilityTargetingMode.Unit && targetFilter == TargetFilter.None)
            errors.Add($"{owner}: Unit targeting needs a Target Filter.");
        if (aimLock == AbilityAimLock.TrackUntilRelease && aimTurnRate > 0f && aimTurnRate < 20f)
            warnings.Add($"{owner}: Aim Turn Rate {aimTurnRate}°/s is very low; the aim will barely move.");
    }
}

/// <summary>Hints the mob AI uses to decide when this ability is a good idea.</summary>
[Serializable]
public class AbilityAIHints
{
    [Tooltip("How much mobs like this ability. 0 = never used automatically, 1 = normal, 3+ = favourite.")]
    [Range(0f, 5f)] public float priority = 1f;

    [Tooltip("Preferred distance to the target, minimum (metres). 0 = the ability's Min Range.")]
    [Min(0f)] public float preferredMinDistance = 0f;

    [Tooltip("Preferred distance to the target, maximum (metres). 0 = the ability's reach.")]
    [Min(0f)] public float preferredMaxDistance = 0f;

    [Tooltip("Only use it when the target is predicted to be inside the hit area (recommended for melee and area abilities; ignored by projectiles).")]
    public bool requireTargetInArea = true;

    [Tooltip("Area abilities: only worth it if at least this many enemies would be hit.")]
    [Min(1)] public int minTargets = 1;

    [Tooltip("Use only when the caster's health is at or below this fraction (heals, escapes, enrage). 1 = any time.")]
    [Range(0f, 1f)] public float useBelowOwnHealth = 1f;

    [Tooltip("Use only when the target's health is at or below this fraction (executes). 1 = any time.")]
    [Range(0f, 1f)] public float useBelowTargetHealth = 1f;

    [Tooltip("Defensive: preferred when the mob is in danger (low health, surrounded, standing in a hazard).")]
    public bool defensive = false;

    [Tooltip("Opener: preferred as the first action when a fight starts (buffs, summons, gap closers).")]
    public bool opener = false;

    [Tooltip("Extra seconds mobs wait before using it again, on top of the cooldown (makes big attacks rarer). Players ignore this.")]
    [Min(0f)] public float extraAICooldown = 0f;

    [Tooltip("Do not use it while an ally is casting the same ability (avoids stacking walls or cages).")]
    public bool avoidAllyOverlap = false;
}

/// <summary>Animation, visual and sound settings of an ability.</summary>
[Serializable]
public class AbilityPresentation
{
    [Tooltip("Animator trigger set when the cast (wind-up) starts. Missing parameters are ignored safely.")]
    public string castTrigger = "";

    [Tooltip("Animator trigger set when the ability is released.")]
    public string releaseTrigger = "";

    [Tooltip("Animator bool kept true while casting/launching (channels). Empty = none.")]
    public string castingBool = "";

    [Tooltip("Effect attached to the caster's cast point while casting.")]
    public GameObject castVfx;

    [Tooltip("Effect played at the cast point on release (muzzle flash, swing).")]
    public GameObject releaseVfx;

    public AudioClip castSound;
    public AudioClip releaseSound;
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Draw the area on the ground while casting. Mob telegraphs warn the player; the player's own is shown as an aim preview.")]
    public bool showTelegraph = true;

    [Tooltip("Telegraph colour. Alpha 0 = use the colours in Combat Settings.")]
    public Color telegraphColor = new Color(0f, 0f, 0f, 0f);

    [Tooltip("How far the sound of this ability carries for AI hearing (metres). 0 = silent.")]
    [Min(0f)] public float noiseRadius = 15f;
}

/// <summary>
/// One ability, shared by players and mobs. Everything about it lives in this asset: timing, targeting, what it does
/// (a list of actions such as area hits, projectiles, ground surges, walls, summons and dashes, each with its own
/// effects), animation, AI hints and absorption rules. Per-character differences (an absorbed weaker copy, an elite
/// buff) are expressed with <see cref="AbilityModifierSet"/>s so the asset itself is never modified at runtime.
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "Scriptable Objects/Ability/Ability Definition", order = -10)]
public class AbilityDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique id used by saves, the Ability Database and absorption. Generated automatically; only change it if you know why.")]
    [SerializeField] private string abilityId = "";
    [Tooltip("Name shown to the player. Empty = the asset name.")]
    [SerializeField] private string displayName = "";
    [TextArea(2, 5)] public string description = "";
    public Sprite icon;
    [Tooltip("What kind of ability this is. Mobs use tags to decide when to use it (Gap Closer, Escape, Defensive...).")]
    public AbilityTag tags = AbilityTag.None;

    [Header("Timing (seconds)")]
    [Tooltip("Wind-up before the ability goes off (Casting phase). The telegraph shows during this time. 0 = instant.")]
    [Min(0f)] public float castTime = 0.5f;
    [Tooltip("Minimum length of the Launching phase after release. Actions that take longer (volleys, dashes, beams) extend it automatically.")]
    [Min(0f)] public float launchTime = 0f;
    [Tooltip("Active phase after launching: follow-through / recovery. The caster is still busy (a punish window).")]
    [Min(0f)] public float activeTime = 0.25f;
    [Tooltip("Seconds before it can be used again, counted from the end of the Active phase.")]
    [Min(0f)] public float cooldown = 4f;
    [Tooltip("Uses stored. Each charge recharges separately over Cooldown seconds.")]
    [Min(1)] public int charges = 1;
    [Tooltip("Cooldown put on the caster's OTHER abilities when this one is used (global cooldown). 0 = none.")]
    [Min(0f)] public float sharedCooldown = 0f;

    [Header("Casting Rules")]
    [Tooltip("What the caster may do while casting and launching.")]
    public CasterMovementRule movementWhileCasting = CasterMovementRule.Stop;
    [Tooltip("Speed multiplier while casting when Movement While Casting = Slowed.")]
    [Range(0f, 1f)] public float castMoveSpeedMultiplier = 0.4f;
    [Tooltip("Phases during which the caster's other abilities cannot be used.")]
    public AbilityPhaseMask blocksOtherAbilities = AbilityPhaseMask.Casting | AbilityPhaseMask.Launching;
    [Tooltip("Taking damage while casting cancels the cast.")]
    public bool interruptedByDamage = false;
    [Tooltip("Smallest hit (fraction of max health) that cancels the cast. 0 = any damage.")]
    [Range(0f, 1f)] public float interruptDamageThreshold = 0.05f;
    [Tooltip("Stuns and silences cancel the cast.")]
    public bool interruptedByControl = true;
    [Tooltip("Fraction of the cooldown applied when the cast is cancelled (0 = can retry at once).")]
    [Range(0f, 1f)] public float cooldownOnInterrupt = 0.5f;
    [Tooltip("Give the cost back when the cast is cancelled before release.")]
    public bool refundCostOnInterrupt = true;

    [Header("Cost")]
    [Min(0f)] public float manaCost = 0f;
    [Min(0f)] public float staminaCost = 0f;
    [Tooltip("Health paid to cast. It can never kill the caster.")]
    [Min(0f)] public float healthCost = 0f;

    [Header("Targeting")]
    public AbilityTargeting targeting = new AbilityTargeting();

    [Header("Actions (run in order on release; each can have its own delay)")]
    [SerializeReference, SubclassSelector] public List<CastAction> actions = new List<CastAction>();

    [Header("Presentation")]
    public AbilityPresentation presentation = new AbilityPresentation();

    [Header("Mob AI")]
    public AbilityAIHints ai = new AbilityAIHints();

    [Header("Absorption")]
    public AbsorptionRules absorption = new AbsorptionRules();

    [SerializeField, HideInInspector] private AbilityEffectSO legacySource;
    [NonSerialized] private bool runtimeCreated;

    // ------------------------------------------------------------------ identity
    /// <summary>Stable unique id.</summary>
    public string Id
    {
        get
        {
            if (string.IsNullOrEmpty(abilityId))
                abilityId = runtimeCreated ? "runtime:" + name + ":" + Guid.NewGuid().ToString("N").Substring(0, 8) : Guid.NewGuid().ToString("N");
            return abilityId;
        }
    }

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// <summary>The legacy AbilityEffectSO this definition was converted from (if any).</summary>
    public AbilityEffectSO LegacySource => legacySource;

    /// <summary>True for definitions created in code at runtime (legacy conversion, presets) rather than assets.</summary>
    public bool IsRuntimeCreated => runtimeCreated;

    public bool HasTag(AbilityTag tag) => (tags & tag) != 0;

    /// <summary>Marks a definition created in code and sets its id/name.</summary>
    public void InitializeRuntime(string id, string niceName, AbilityEffectSO legacy = null)
    {
        runtimeCreated = true;
        abilityId = id;
        displayName = niceName;
        name = niceName;
        legacySource = legacy;
        hideFlags = HideFlags.DontSave;
    }

    /// <summary>Sets the display name (editor tools and presets).</summary>
    public void SetDisplayName(string value) => displayName = value ?? "";

    /// <summary>Sets the id (editor tools; keep ids unique).</summary>
    public void SetId(string value) => abilityId = value ?? "";

    /// <summary>Sets the legacy asset this definition mirrors (editor converter).</summary>
    public void SetLegacySource(AbilityEffectSO source) => legacySource = source;

    // ------------------------------------------------------------------ resolved numbers
    public float Range(in AbilityStats s) => targeting.range * s.range;
    public float MinRange(in AbilityStats s) => targeting.minRange * s.range;
    public float CastTime(in AbilityStats s) => castTime * s.castTime;
    public float Cooldown(in AbilityStats s) => cooldown * s.cooldown;
    public int MaxCharges(in AbilityStats s) => Mathf.Max(1, charges + s.extraCharges);

    /// <summary>Length of the Launching phase with these stats (at least Launch Time; long actions extend it).</summary>
    public float LaunchDuration(in AbilityStats s)
    {
        float d = launchTime;
        for (int i = 0; i < actions.Count; i++)
        {
            CastAction a = actions[i];
            if (a == null)
                continue;
            d = Mathf.Max(d, a.delay + a.LaunchDuration(this, s));
        }
        return d;
    }

    /// <summary>Longest distance from the caster at which this ability can hurt something (AI range checks).</summary>
    public float MaxReach(in AbilityStats s)
    {
        float reach = targeting.mode == AbilityTargetingMode.Self ? 0f : Range(s);
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] != null)
                reach = Mathf.Max(reach, actions[i].Reach(this, s));
        }
        return reach;
    }

    /// <summary>Total damage one cast can deal to one target (rough, for AI and tooltips).</summary>
    public float EstimateDamage(in AbilityStats s)
    {
        float total = 0f;
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] != null)
                total += actions[i].EstimateDamage(s);
        }
        return total;
    }

    /// <summary>Seconds of hard crowd control one cast applies (AI).</summary>
    public float EstimateControl(in AbilityStats s)
    {
        float total = 0f;
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] != null)
                total = Mathf.Max(total, actions[i].EstimateControl(s));
        }
        return total;
    }

    public bool BlocksOthersDuring(AbilityPhase phase)
    {
        switch (phase)
        {
            case AbilityPhase.Casting: return (blocksOtherAbilities & AbilityPhaseMask.Casting) != 0;
            case AbilityPhase.Launching: return (blocksOtherAbilities & AbilityPhaseMask.Launching) != 0;
            case AbilityPhase.Active: return (blocksOtherAbilities & AbilityPhaseMask.Active) != 0;
            default: return false;
        }
    }

    /// <summary>Finds the first action of a type (e.g. the ProjectileAction), or null.</summary>
    public T FindAction<T>() where T : CastAction
    {
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] is T t)
                return t;
        }
        return null;
    }

    /// <summary>Loads VFX into the pools ahead of time.</summary>
    public void Prewarm()
    {
        for (int i = 0; i < actions.Count; i++)
            actions[i]?.Prewarm();
    }

    // ------------------------------------------------------------------ text
    /// <summary>Multi-line tooltip text for this ability with optional modifiers.</summary>
    public string Describe(AbilityModifierSet modifiers = null)
    {
        AbilityStats s = AbilityStats.From(modifiers);
        var sb = new StringBuilder();
        sb.Append(DisplayName);
        if (modifiers != null && !string.IsNullOrEmpty(modifiers.label))
            sb.Append(" (").Append(modifiers.label).Append(')');
        sb.AppendLine();
        if (!string.IsNullOrEmpty(description))
            sb.AppendLine(description);
        sb.Append("Cast ").Append(CastTime(s).ToString("0.##")).Append("s, cooldown ").Append(Cooldown(s).ToString("0.#")).Append('s');
        int ch = MaxCharges(s);
        if (ch > 1)
            sb.Append(", ").Append(ch).Append(" charges");
        if (targeting.mode != AbilityTargetingMode.Self)
            sb.Append(", range ").Append(Range(s).ToString("0.#")).Append('m');
        sb.AppendLine();
        float cost = manaCost * s.cost;
        if (cost > 0f) sb.Append("Mana ").Append(cost.ToString("0.#")).Append("  ");
        cost = staminaCost * s.cost;
        if (cost > 0f) sb.Append("Stamina ").Append(cost.ToString("0.#")).Append("  ");
        cost = healthCost * s.cost;
        if (cost > 0f) sb.Append("Health ").Append(cost.ToString("0.#"));
        if (manaCost + staminaCost + healthCost > 0f)
            sb.AppendLine();
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i] == null)
                continue;
            string line = actions[i].Describe(this, s);
            if (!string.IsNullOrEmpty(line))
                sb.Append("• ").AppendLine(line);
        }
        if (modifiers != null && !modifiers.IsIdentity)
            sb.Append("Modified: ").AppendLine(modifiers.Describe());
        return sb.ToString().TrimEnd();
    }

    public override string ToString() => DisplayName;

    // ------------------------------------------------------------------ validation
    /// <summary>Checks the setup. Errors break the ability; warnings are likely mistakes.</summary>
    public void Validate(List<string> errors, List<string> warnings)
    {
        string owner = DisplayName;
        if (actions == null || actions.Count == 0)
            errors.Add($"{owner}: has no actions, it will do nothing. Add at least one action (Area Hit, Projectile...).");
        else
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] == null)
                {
                    errors.Add($"{owner}: action #{i + 1} is empty. Pick a type from its dropdown or remove it.");
                    continue;
                }
                actions[i].Validate(this, $"{owner} › {actions[i].MenuName} #{i + 1}", errors, warnings);
            }
        }

        targeting.Validate(owner, errors, warnings);

        if (targeting.mode == AbilityTargetingMode.Unit || targeting.mode == AbilityTargetingMode.Point)
        {
            bool anyRanged = false;
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null && (actions[i].anchor != ActionAnchor.Caster || actions[i] is ProjectileAction || actions[i] is MovementAction || actions[i] is DirectEffectAction))
                    anyRanged = true;
            }
            if (!anyRanged && actions.Count > 0)
                warnings.Add($"{owner}: targeting is {targeting.mode} but every action is anchored at the Caster; set an action's Anchor to Aim Point or Target Unit.");
        }

        if (cooldown <= 0f && castTime <= 0f && activeTime <= 0f)
            warnings.Add($"{owner}: no cast time, active time or cooldown: it can be used every frame.");
        if (ai.preferredMaxDistance > 0f && ai.preferredMaxDistance < ai.preferredMinDistance)
            errors.Add($"{owner}: AI Preferred Max Distance is smaller than Preferred Min Distance.");
        if (absorption.canBeAbsorbed && absorption.absorbedAs == this)
            warnings.Add($"{owner}: 'Absorbed As' points to itself; leave it empty to grant the same ability.");
        if (castTime > 0f && movementWhileCasting == CasterMovementRule.Free && presentation.showTelegraph && targeting.aimLock == AbilityAimLock.LockAtCastStart)
            warnings.Add($"{owner}: the caster moves freely while a locked telegraph stays behind - make sure that is intended.");
    }

    private void OnEnable()
    {
        if (actions == null) actions = new List<CastAction>();
        if (targeting == null) targeting = new AbilityTargeting();
        if (presentation == null) presentation = new AbilityPresentation();
        if (ai == null) ai = new AbilityAIHints();
        if (absorption == null) absorption = new AbsorptionRules();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        OnEnable();
        if (string.IsNullOrEmpty(abilityId) && !runtimeCreated)
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            string guid = string.IsNullOrEmpty(path) ? "" : UnityEditor.AssetDatabase.AssetPathToGUID(path);
            abilityId = string.IsNullOrEmpty(guid) ? Guid.NewGuid().ToString("N") : guid;
        }
        if (targeting.minRange > targeting.range)
            targeting.minRange = targeting.range;
    }
#endif
}
