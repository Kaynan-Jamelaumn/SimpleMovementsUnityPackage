using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a condition of a need feels: decides its colour on the HUD and the tone of its message.</summary>
public enum NeedMood
{
    /// <summary>Better than normal (Full, Quenched, Refreshed): green.</summary>
    Good,
    /// <summary>Normal: nothing to notice.</summary>
    Fine,
    /// <summary>The first signs (Peckish, Dry Throat, Sleepy): yellow.</summary>
    Low,
    /// <summary>It hurts (Hungry, Thirsty, Exhausted): orange.</summary>
    Bad,
    /// <summary>Dangerous (Starving, Dehydrated, Falling Asleep): red.</summary>
    Critical,
}

/// <summary>
/// One condition of a need (hunger, thirst, rest): its name, the part of the bar where it applies and what it does to
/// the character while it lasts. A need has a list of them that covers the whole bar - Full, Satisfied, Fed, Peckish,
/// Hungry, Ravenous, Starving - and exactly one applies at a time: the one with the highest From Percent that the need
/// has reached.
/// </summary>
[Serializable]
public class NeedStage
{
    [Tooltip("Name of the condition (Peckish, Thirsty, Refreshed): shown under the need's gauge, on its status icon and in messages.")]
    public string name = "";
    [Tooltip("The condition applies while the need is at or above this percent of its maximum, up to the next condition's.")]
    [Range(0f, 100f)] public float fromPercent = 0f;
    [Tooltip("How it feels: the colour of its name on the HUD and the tone of its message.")]
    public NeedMood mood = NeedMood.Fine;
    [Tooltip("What it does while it lasts: stats, health / stamina / sanity per second, faster or slower needs, fainting. " +
             "Leave it empty for a condition that only has a name (Fed). Its Label may stay empty: the icon takes the condition's name.")]
    public StatusConsequence effect = new StatusConsequence("", true);
    [Tooltip("Told to the player when the condition begins (empty = nothing).")]
    public string message = "";
    [Tooltip("How strongly it shows on the local player's screen (0 = not at all). Rest: the eyelids droop and close; " +
             "hunger and thirst: the edges of the picture darken in slow pulses (light-headed).")]
    [Range(0f, 1f)] public float screenEffect = 0f;

    public NeedStage() { }

    public NeedStage(string name, float fromPercent, NeedMood mood, string message = "")
    {
        this.name = name;
        this.fromPercent = fromPercent;
        this.mood = mood;
        this.message = message;
        effect = new StatusConsequence("", mood != NeedMood.Good);
    }

    /// <summary>Does it change anything (a condition may be a name only)?</summary>
    public bool HasEffect => effect != null && (HasStats(effect) || effect.healthPerSecond != 0f || effect.staminaPerSecond != 0f ||
        effect.sanityPerSecond != 0f || !Mathf.Approximately(effect.hungerDrain, 1f) || !Mathf.Approximately(effect.thirstDrain, 1f) ||
        effect.stunChancePerMinute > 0f);

    private static bool HasStats(StatusConsequence c) =>
        (c.combatStats != null && c.combatStats.Count > 0) || (c.characterStats != null && c.characterStats.Count > 0);

    /// <summary>"Peckish (from 30%): -5% Stamina Regen Rate".</summary>
    public string Describe() => $"{name} (from {fromPercent:0.#}%): {(HasEffect ? effect.Describe() : "no effect")}";

    public NeedStage Clone()
    {
        var c = (NeedStage)MemberwiseClone();
        c.effect = effect != null ? effect.Clone() : new StatusConsequence("", true);
        return c;
    }
}

/// <summary>
/// A stage of tiredness as it was saved before conditions (<see cref="NeedStage"/>): what happened while rest was below
/// <see cref="belowPercent"/>. Only read to bring an older Sleep Manager's own stages over.
/// </summary>
[Serializable]
public class RestStage
{
    [Tooltip("The stage starts when rest is below this percent of the max.")]
    [Range(0f, 100f)] public float belowPercent = 50f;
    [Tooltip("What it does (status icon, stats, per-second effects, fainting).")]
    public StatusConsequence effect = new StatusConsequence("Tired");
}

/// <summary>A status with named conditions (the HUD shows the current one under its gauge).</summary>
public interface INeedStages
{
    /// <summary>The condition the need is in right now (null = none). Known on every copy, a client's included.</summary>
    NeedStage CurrentStage { get; }
    /// <summary>Every condition of the need.</summary>
    IReadOnlyList<NeedStage> Conditions { get; }
}

/// <summary>Finding the condition of a need, colours, and the default conditions of hunger, thirst and rest.</summary>
public static class NeedStages
{
    /// <summary>
    /// The condition a need at <paramref name="percent"/> is in: the one with the highest From Percent it has reached
    /// (-1 = none: the list is empty, or the need is below every condition).
    /// </summary>
    public static int Find(IList<NeedStage> stages, float percent)
    {
        int best = -1;
        float bestFrom = float.NegativeInfinity;
        if (stages == null)
            return best;
        for (int i = 0; i < stages.Count; i++)
        {
            NeedStage s = stages[i];
            if (s == null || percent + 1e-4f < s.fromPercent || s.fromPercent <= bestFrom)
                continue;
            bestFrom = s.fromPercent;
            best = i;
        }
        return best;
    }

    /// <summary>The percent below which a need stops being fine: where its first Low, Bad or Critical condition ends.</summary>
    public static float TroubleBelow(IList<NeedStage> stages)
    {
        if (stages == null)
            return 0f;
        // The lowest condition that is fine or good: everything under it is trouble.
        float fine = float.PositiveInfinity;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i] != null && stages[i].mood <= NeedMood.Fine)
                fine = Mathf.Min(fine, stages[i].fromPercent);
        return float.IsPositiveInfinity(fine) ? 100f : fine;
    }

    /// <summary>The colour of a mood on the HUD; <paramref name="normal"/> for a fine one.</summary>
    public static Color MoodColor(NeedMood mood, Color normal)
    {
        switch (mood)
        {
            case NeedMood.Good: return new Color(0.55f, 0.87f, 0.5f);
            case NeedMood.Low: return new Color(0.96f, 0.84f, 0.42f);
            case NeedMood.Bad: return new Color(0.98f, 0.62f, 0.3f);
            case NeedMood.Critical: return new Color(1f, 0.4f, 0.36f);
            default: return normal;
        }
    }

    /// <summary>Problems of a list of conditions (inspectors, tests): empty names, two at the same percent, a gap at the bottom.</summary>
    public static void Validate(IList<NeedStage> stages, string need, List<string> warnings)
    {
        if (stages == null || stages.Count == 0)
        {
            warnings.Add($"{need}: no conditions (nothing happens as it drops).");
            return;
        }
        float lowest = float.PositiveInfinity;
        for (int i = 0; i < stages.Count; i++)
        {
            NeedStage s = stages[i];
            if (s == null) { warnings.Add($"{need}: condition #{i + 1} is empty."); continue; }
            if (string.IsNullOrWhiteSpace(s.name)) warnings.Add($"{need}: condition #{i + 1} has no name.");
            lowest = Mathf.Min(lowest, s.fromPercent);
            for (int k = 0; k < i; k++)
                if (stages[k] != null && Mathf.Approximately(stages[k].fromPercent, s.fromPercent))
                    warnings.Add($"{need}: '{stages[k].name}' and '{s.name}' both start at {s.fromPercent:0.#}% (only the first applies).");
        }
        if (lowest > 0.001f)
            warnings.Add($"{need}: no condition starts at 0% (below {lowest:0.#}% nothing applies).");
    }

    /// <summary>
    /// Conditions that do what older "below percent" stages did: each from the threshold under it up to its own, the
    /// mildest one Low, the next Bad, the rest Critical, and a Rested one above them. Best first, like the defaults.
    /// </summary>
    public static List<NeedStage> FromBelowStages(IList<RestStage> legacy)
    {
        var sorted = new List<RestStage>();
        if (legacy != null)
            foreach (RestStage s in legacy)
                if (s != null) sorted.Add(s);
        sorted.Sort((a, b) => a.belowPercent.CompareTo(b.belowPercent));
        var result = new List<NeedStage>();
        float from = 0f;
        for (int i = 0; i < sorted.Count; i++)
        {
            RestStage s = sorted[i];
            StatusConsequence effect = s.effect != null ? s.effect.Clone() : new StatusConsequence("", true);
            // "-10% stamina regeneration" was written on the flat stat: keep what it meant.
            effect.characterStats = new List<TraitModifier>(TraitStats.ReadRegenPercents(effect.characterStats));
            int fromTop = sorted.Count - 1 - i; // the mildest stage is the highest one
            var stage = new NeedStage(string.IsNullOrEmpty(effect.label) ? $"Tired {fromTop + 1}" : effect.label, from,
                fromTop == 0 ? NeedMood.Low : fromTop == 1 ? NeedMood.Bad : NeedMood.Critical)
            {
                effect = effect,
                screenEffect = fromTop == 0 ? 0.15f : fromTop == 1 ? 0.4f : 0.8f,
            };
            effect.label = "";
            result.Add(stage);
            from = s.belowPercent;
        }
        result.Add(new NeedStage("Rested", Mathf.Clamp(from, 0f, 100f), NeedMood.Fine));
        result.Reverse();
        return result;
    }

    // ------------------------------------------------------------------ defaults
    private static TraitModifier Pct(TraitStat stat, float value) => TraitModifier.Percent(stat, value);
    private static CombatStatModifier Combat(CombatStatType stat, float value) => new CombatStatModifier(stat, value);

    /// <summary>
    /// Hunger, best first: Stuffed, Full, Satisfied, Fed (nothing), Peckish, Hungry, Ravenous, Starving. Being well fed
    /// helps the body mend and keep going; an empty stomach saps stamina first, then strength, then the will, and at the
    /// bottom the character grows faint. (At exactly 0 the Hunger Manager's starvation damage runs as well.)
    /// </summary>
    public static List<NeedStage> Hunger() => new List<NeedStage>
    {
        new NeedStage("Stuffed", 95f, NeedMood.Good, "You could not eat another bite.")
        {
            effect = new StatusConsequence("", false)
            {
                // A full belly mends the body, and slows the legs a little.
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.HealthRegenRate, 25f), Pct(TraitStat.InjuryHealing, 15f), Pct(TraitStat.HealingReceived, 5f),
                    Pct(TraitStat.SprintSpeed, -5f),
                },
            },
        },
        new NeedStage("Full", 80f, NeedMood.Good, "You are full.")
        {
            effect = new StatusConsequence("", false)
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.HealthRegenRate, 15f), Pct(TraitStat.StaminaRegenRate, 10f), Pct(TraitStat.InjuryHealing, 10f),
                },
            },
        },
        new NeedStage("Satisfied", 60f, NeedMood.Good)
        {
            effect = new StatusConsequence("", false)
            {
                characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, 5f) },
            },
        },
        new NeedStage("Fed", 45f, NeedMood.Fine),
        new NeedStage("Peckish", 30f, NeedMood.Low, "Your stomach growls.")
        {
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, -5f) },
            },
        },
        new NeedStage("Hungry", 15f, NeedMood.Bad, "You are hungry.")
        {
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.StaminaRegenRate, -15f), Pct(TraitStat.MaxStamina, -10f), Pct(TraitStat.HealthRegenRate, -25f),
                    Pct(TraitStat.WeaponDamage, -5f),
                },
                sanityPerSecond = -0.01f,
            },
        },
        new NeedStage("Ravenous", 5f, NeedMood.Bad, "You are weak with hunger.")
        {
            screenEffect = 0.25f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.StaminaRegenRate, -30f), Pct(TraitStat.MaxStamina, -20f), Pct(TraitStat.HealthRegenRate, -60f),
                    Pct(TraitStat.InjuryHealing, -25f), Pct(TraitStat.MoveSpeed, -6f), Pct(TraitStat.WeaponDamage, -12f),
                    Pct(TraitStat.CarryWeight, -10f),
                },
                sanityPerSecond = -0.03f,
            },
        },
        new NeedStage("Starving", 0f, NeedMood.Critical, "You are starving.")
        {
            screenEffect = 0.6f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.StaminaRegenRate, -50f), Pct(TraitStat.MaxStamina, -35f), Pct(TraitStat.HealthRegenRate, -100f),
                    Pct(TraitStat.InjuryHealing, -40f), Pct(TraitStat.MoveSpeed, -15f), Pct(TraitStat.WeaponDamage, -22f),
                    Pct(TraitStat.CarryWeight, -25f),
                },
                combatStats = new List<CombatStatModifier> { Combat(CombatStatType.ColdResistance, -25f) },
                sanityPerSecond = -0.06f,
                stunChancePerMinute = 0.15f, stunSeconds = 1.5f,
            },
        },
    };

    /// <summary>
    /// Thirst, best first: Quenched, Hydrated (nothing), Dry Throat, Thirsty, Parched, Dehydrated. Water is breath and
    /// focus: thirst slows the stamina and the mana back, then the casting and the aim, and at the bottom the head spins.
    /// (At exactly 0 the Thirst Manager's dehydration damage runs as well.)
    /// </summary>
    public static List<NeedStage> Thirst() => new List<NeedStage>
    {
        new NeedStage("Quenched", 85f, NeedMood.Good, "Your thirst is quenched.")
        {
            effect = new StatusConsequence("", false)
            {
                characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, 10f), Pct(TraitStat.ManaRegenRate, 10f) },
                combatStats = new List<CombatStatModifier> { Combat(CombatStatType.HeatResistance, 10f) },
            },
        },
        new NeedStage("Hydrated", 55f, NeedMood.Fine),
        new NeedStage("Dry Throat", 35f, NeedMood.Low, "Your throat is dry.")
        {
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, -8f) },
            },
        },
        new NeedStage("Thirsty", 18f, NeedMood.Bad, "You are thirsty.")
        {
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.StaminaRegenRate, -20f), Pct(TraitStat.ManaRegenRate, -15f), Pct(TraitStat.AbilityCastTime, 8f),
                    Pct(TraitStat.StaminaCost, 10f),
                },
                combatStats = new List<CombatStatModifier> { Combat(CombatStatType.HeatResistance, -15f) },
            },
        },
        new NeedStage("Parched", 6f, NeedMood.Bad, "Your lips are cracked and your head aches.")
        {
            screenEffect = 0.3f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.StaminaRegenRate, -35f), Pct(TraitStat.ManaRegenRate, -30f), Pct(TraitStat.AbilityCastTime, 15f),
                    Pct(TraitStat.StaminaCost, 20f), Pct(TraitStat.MoveSpeed, -8f),
                },
                combatStats = new List<CombatStatModifier>
                {
                    Combat(CombatStatType.HeatResistance, -30f), Combat(CombatStatType.CriticalChance, -5f),
                },
                sanityPerSecond = -0.03f,
                stunChancePerMinute = 0.08f, stunSeconds = 1f,
            },
        },
        new NeedStage("Dehydrated", 0f, NeedMood.Critical, "You are dehydrated.")
        {
            screenEffect = 0.7f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.StaminaRegenRate, -55f), Pct(TraitStat.ManaRegenRate, -50f), Pct(TraitStat.AbilityCastTime, 25f),
                    Pct(TraitStat.StaminaCost, 35f), Pct(TraitStat.MoveSpeed, -18f), Pct(TraitStat.WeaponDamage, -15f),
                },
                combatStats = new List<CombatStatModifier>
                {
                    Combat(CombatStatType.HeatResistance, -50f), Combat(CombatStatType.CriticalChance, -10f),
                },
                sanityPerSecond = -0.08f,
                stunChancePerMinute = 0.3f, stunSeconds = 2f,
            },
        },
    };

    /// <summary>
    /// Rest, best first: Refreshed, Rested (nothing), Sleepy, Tired, Exhausted, Falling Asleep. Tiredness dulls the
    /// reflexes and the casting, wears on the mind, and at the bottom the character nods off on its feet - until it
    /// collapses (the Sleep Manager's Collapse At Percent).
    /// </summary>
    public static List<NeedStage> Rest() => new List<NeedStage>
    {
        new NeedStage("Refreshed", 90f, NeedMood.Good)
        {
            effect = new StatusConsequence("", false)
            {
                characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, 10f), Pct(TraitStat.AbilityCastTime, -5f) },
                combatStats = new List<CombatStatModifier> { Combat(CombatStatType.CriticalChance, 2f) },
                sanityPerSecond = 0.01f,
            },
        },
        new NeedStage("Rested", 55f, NeedMood.Fine),
        new NeedStage("Sleepy", 35f, NeedMood.Low, "You yawn.")
        {
            screenEffect = 0.12f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier> { Pct(TraitStat.StaminaRegenRate, -8f) },
                combatStats = new List<CombatStatModifier> { Combat(CombatStatType.CriticalChance, -2f) },
            },
        },
        new NeedStage("Tired", 20f, NeedMood.Bad, "You are tired.")
        {
            screenEffect = 0.3f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.MoveSpeed, -6f), Pct(TraitStat.StaminaRegenRate, -18f), Pct(TraitStat.AbilityCastTime, 10f),
                },
                combatStats = new List<CombatStatModifier>
                {
                    Combat(CombatStatType.CriticalChance, -4f), Combat(CombatStatType.AttackSpeed, -5f),
                },
                sanityPerSecond = -0.02f,
            },
        },
        new NeedStage("Exhausted", 8f, NeedMood.Bad, "You can barely keep your eyes open.")
        {
            screenEffect = 0.6f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.MoveSpeed, -14f), Pct(TraitStat.StaminaRegenRate, -35f), Pct(TraitStat.AbilityCastTime, 20f),
                },
                combatStats = new List<CombatStatModifier>
                {
                    Combat(CombatStatType.WeaponDamage, -10f), Combat(CombatStatType.CriticalChance, -8f), Combat(CombatStatType.AttackSpeed, -10f),
                },
                sanityPerSecond = -0.06f,
                stunChancePerMinute = 0.12f, stunSeconds = 1.5f,
            },
        },
        new NeedStage("Falling Asleep", 0f, NeedMood.Critical, "You are falling asleep on your feet.")
        {
            screenEffect = 1f,
            effect = new StatusConsequence("")
            {
                characterStats = new List<TraitModifier>
                {
                    Pct(TraitStat.MoveSpeed, -25f), Pct(TraitStat.StaminaRegenRate, -55f), Pct(TraitStat.AbilityCastTime, 35f),
                    Pct(TraitStat.DamageTaken, 10f),
                },
                combatStats = new List<CombatStatModifier>
                {
                    Combat(CombatStatType.WeaponDamage, -20f), Combat(CombatStatType.CriticalChance, -15f), Combat(CombatStatType.AttackSpeed, -18f),
                },
                sanityPerSecond = -0.12f,
                // Nodding off: the character sleeps for a moment where it stands, several times a minute.
                stunChancePerMinute = 0.75f, stunSeconds = 2.5f,
            },
        },
    };
}

/// <summary>
/// Runs the conditions of one need on a character: follows which one applies as the need changes (on every copy, so
/// the HUD and the messages work on a client too) and, where the game rules run, applies its effects through a
/// <see cref="ConsequenceRunner"/> - exactly one condition at a time, removed the moment it ends.
/// </summary>
public sealed class NeedStageRunner
{
    private const float MessageGap = 4f;

    private readonly Component owner;
    private readonly ConsequenceRunner runner;
    private readonly Dictionary<NeedStage, StatusConsequence> named = new Dictionary<NeedStage, StatusConsequence>();
    private bool started;
    private float nextMessage;
    private CombatEntity entity;

    /// <param name="owner">The status manager it belongs to (finds the character).</param>
    /// <param name="key">Names its status and drain multipliers ("Hunger", "Thirst", "Rest"): one per need.</param>
    public NeedStageRunner(Component owner, string key)
    {
        this.owner = owner;
        runner = new ConsequenceRunner(owner, key);
    }

    /// <summary>The condition that applies (null = none).</summary>
    public NeedStage Current { get; private set; }
    /// <summary>Its place in the list (-1 = none).</summary>
    public int Index { get; private set; } = -1;

    /// <summary>The condition changed: (the one before, the new one); either may be null. Raised on every copy.</summary>
    public event Action<NeedStage, NeedStage> Changed;

    /// <summary>
    /// Call every frame. <paramref name="percent"/>: the need, 0-100. <paramref name="rules"/>: this copy runs the game
    /// rules (applies the effects); a client's copy only follows the condition. <paramref name="active"/> false: no
    /// condition applies now (dead, unconscious, asleep).
    /// </summary>
    public void Tick(IList<NeedStage> stages, float percent, float dt, bool rules, bool active = true)
    {
        int index = active ? NeedStages.Find(stages, percent) : -1;
        NeedStage next = index >= 0 ? stages[index] : null;
        Index = index;
        if (!ReferenceEquals(next, Current))
        {
            NeedStage before = Current;
            Current = next;
            if (started && active)
                Announce(before, next);
            Changed?.Invoke(before, next);
        }
        started = true;

        if (!rules)
        {
            if (runner.Current != null)
                runner.Clear();
            return;
        }
        StatusConsequence wanted = EffectOf(Current);
        if (!ReferenceEquals(wanted, runner.Current))
            runner.Set(wanted);
        runner.Tick(dt);
    }

    /// <summary>Ends the running condition's effects (the component is switched off, the character respawns).</summary>
    public void Clear()
    {
        runner.Clear();
        Current = null;
        Index = -1;
        started = false;
    }

    /// <summary>The effect of a condition, named after it when its own label is empty (the status icon's name).</summary>
    private StatusConsequence EffectOf(NeedStage stage)
    {
        if (stage == null || !stage.HasEffect)
            return null;
        if (!string.IsNullOrEmpty(stage.effect.label))
            return stage.effect;
        // One copy per condition that carries its name (the same object every time: the runner keeps it running).
        if (!named.TryGetValue(stage, out StatusConsequence c))
        {
            c = new StatusConsequence(stage.name, true);
            named[stage] = c;
        }
        Copy(stage.effect, c);
        c.label = stage.name;
        c.debuff = stage.mood > NeedMood.Fine;
        return c;
    }

    private static void Copy(StatusConsequence from, StatusConsequence to)
    {
        to.element = from.element;
        to.combatStats = from.combatStats;
        to.characterStats = from.characterStats;
        to.healthPerSecond = from.healthPerSecond;
        to.staminaPerSecond = from.staminaPerSecond;
        to.sanityPerSecond = from.sanityPerSecond;
        to.hungerDrain = from.hungerDrain;
        to.thirstDrain = from.thirstDrain;
        to.stunChancePerMinute = from.stunChancePerMinute;
        to.stunSeconds = from.stunSeconds;
    }

    /// <summary>Tells the local player the condition that began (its Message), not more often than every few seconds.</summary>
    private void Announce(NeedStage before, NeedStage next)
    {
        if (next == null || string.IsNullOrEmpty(next.message) || owner == null || Time.unscaledTime < nextMessage)
            return;
        if (entity == null)
            entity = owner.GetComponentInParent<CombatEntity>() ?? owner.GetComponentInChildren<CombatEntity>();
        if (entity == null || entity.Kind != CombatEntity.EntityKind.Player)
            return;
        bool local = GameNet.NetAuthority.IsNetworkedObject(entity.gameObject)
            ? GameNet.NetAuthority.IsLocallyControlled(entity.gameObject)
            : GameNet.NetAuthority.HasLocalPlayer;
        if (!local)
            return;
        nextMessage = Time.unscaledTime + MessageGap;
        InventoryManager inventory = InventoryManager.For(owner);
        if (inventory != null)
            inventory.ShowMessage(next.message, next.mood >= NeedMood.Bad ? InventoryFeedback.Kind.Warning : InventoryFeedback.Kind.Info);
    }
}
