using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Limits on what a character can do that do not come from injuries: a disability trait ("cannot sprint", "cannot hold
/// two-handed weapons"), a curse, a quest state. Each source owns one key; what is blocked is everything any source
/// blocks. The <see cref="CombatEntity"/> keeps one and asks it together with the injury system, so movement, weapons,
/// hands, blocking, items and abilities follow it with the injury system on or off.
/// </summary>
public sealed class ActionBlocks
{
    private struct Entry
    {
        public BodyAction actions;
        public string reason;
    }

    private readonly Dictionary<object, Entry> entries = new Dictionary<object, Entry>();

    /// <summary>Everything blocked right now.</summary>
    public BodyAction Blocked { get; private set; }

    public bool Any => Blocked != BodyAction.None;

    public bool Allows(BodyAction action) => (Blocked & action) == 0;

    /// <summary>Blocks <paramref name="actions"/> for <paramref name="key"/> (replacing what it blocked before); None removes it.</summary>
    public void Set(object key, BodyAction actions, string reason)
    {
        if (key == null)
            return;
        if (actions == BodyAction.None)
        {
            Clear(key);
            return;
        }
        entries[key] = new Entry { actions = actions, reason = reason ?? "" };
        Recompute();
    }

    public void Clear(object key)
    {
        if (key != null && entries.Remove(key))
            Recompute();
    }

    private void Recompute()
    {
        BodyAction all = BodyAction.None;
        foreach (Entry e in entries.Values)
            all |= e.actions;
        Blocked = all;
    }

    /// <summary>Why <paramref name="action"/> is blocked: the reason of a source that blocks it ("" when none does).</summary>
    public string ReasonFor(BodyAction action)
    {
        foreach (Entry e in entries.Values)
            if ((e.actions & action) != 0 && !string.IsNullOrEmpty(e.reason))
                return e.reason;
        return "";
    }

    /// <summary>What the hands can still hold with these blocks on top of <paramref name="hands"/>.</summary>
    public HandCapability Apply(HandCapability hands)
    {
        if (Blocked == BodyAction.None)
            return hands;
        if ((Blocked & BodyAction.MainHand) != 0 && hands.mainHand)
        {
            hands.mainHand = false;
            hands.mainReason = ReasonFor(BodyAction.MainHand);
        }
        if ((Blocked & BodyAction.OffHand) != 0 && hands.offHand)
        {
            hands.offHand = false;
            hands.offReason = ReasonFor(BodyAction.OffHand);
        }
        bool two = hands.mainHand && hands.offHand && (Blocked & BodyAction.TwoHandedWeapons) == 0;
        if (hands.twoHanded && !two)
        {
            hands.twoHanded = false;
            hands.twoHandedReason = !hands.mainHand ? hands.mainReason : !hands.offHand ? hands.offReason : ReasonFor(BodyAction.TwoHandedWeapons);
        }
        return hands;
    }
}

/// <summary>When and how long the episodes of a disability come (a migraine, a sleep attack, a flare-up of pain).</summary>
[Serializable]
public class EpisodeTiming
{
    [Tooltip("Minutes between two episodes on average, while what brings them on holds (they come at random: sometimes two close " +
             "together, sometimes none for a long while). 0 = one right after the other: a condition that is always there and only " +
             "changes strength.")]
    [Min(0f)] public float averageMinutesBetween = 6f;
    [Tooltip("At least this many seconds of peace after an episode ends.")]
    [Min(0f)] public float minimumGapSeconds = 30f;
    [Tooltip("An episode lasts between these many seconds (picked at random each time).")]
    public Vector2 durationSeconds = new Vector2(20f, 60f);
    [Tooltip("How strong an episode is, between these (picked at random each time): its effects are scaled by it. 0.3 - 1 = some " +
             "are mild, some as bad as it gets; a minimum of 0 means some pass unnoticed.")]
    public Vector2 severity = new Vector2(0.4f, 1f);

    public EpisodeTiming() { }

    public EpisodeTiming(float averageMinutesBetween, float minDuration, float maxDuration, float minSeverity = 0.4f, float maxSeverity = 1f, float gap = 30f)
    {
        this.averageMinutesBetween = averageMinutesBetween;
        durationSeconds = new Vector2(minDuration, maxDuration);
        severity = new Vector2(minSeverity, maxSeverity);
        minimumGapSeconds = gap;
    }

    /// <summary>One right after the other: always there, only its strength changes.</summary>
    public bool Continuous => averageMinutesBetween <= 0f;

    public string Describe()
    {
        string length = Mathf.Approximately(durationSeconds.x, durationSeconds.y) ? $"{durationSeconds.x:0.#} s" : $"{durationSeconds.x:0.#}-{durationSeconds.y:0.#} s";
        string strength = Mathf.Approximately(severity.x, severity.y) ? "" : $", {severity.x * 100f:0}-{severity.y * 100f:0}% strength";
        return Continuous ? $"always, changing every {length}{strength}" : $"about every {averageMinutesBetween:0.#} min for {length}{strength}";
    }
}

/// <summary>
/// The clock of an episodic disability: decides, where the game rules run, when an episode starts, how strong it is and
/// when it ends (<see cref="EpisodeTiming"/>). Episodes come as a random process, so they cannot be timed by the player.
/// </summary>
public sealed class EpisodeSchedule
{
    public const int Started = 1, Ended = -1, Nothing = 0;

    /// <summary>An episode is running.</summary>
    public bool Active { get; private set; }
    /// <summary>Its strength (0-1 of the trait's full effect).</summary>
    public float Severity { get; private set; }
    /// <summary>Seconds it lasts in all, and seconds left.</summary>
    public float Duration { get; private set; }
    public float Remaining { get; private set; }
    /// <summary>Seconds of peace left before the next one may start.</summary>
    public float GapLeft { get; private set; }

    /// <summary>
    /// Advances by <paramref name="dt"/>. <paramref name="canStart"/>: what brings episodes on holds right now.
    /// <paramref name="random"/> gives numbers from 0 to 1. Returns <see cref="Started"/>, <see cref="Ended"/> or
    /// <see cref="Nothing"/>.
    /// </summary>
    public int Tick(EpisodeTiming timing, float dt, bool canStart, Func<float> random)
    {
        if (timing == null || dt <= 0f)
            return Nothing;
        if (Active)
        {
            Remaining -= dt;
            if (Remaining > 0f)
                return Nothing;
            End(timing.Continuous ? 0f : timing.minimumGapSeconds);
            return Ended;
        }
        if (GapLeft > 0f)
        {
            GapLeft -= dt;
            if (GapLeft > 0f)
                return Nothing;
        }
        if (!canStart)
            return Nothing;
        if (!timing.Continuous)
        {
            // A random process: the chance in this step of an event that comes every so often on average.
            float chance = 1f - Mathf.Exp(-dt / (timing.averageMinutesBetween * 60f));
            if (random() >= chance)
                return Nothing;
        }
        Severity = Mathf.Clamp01(Mathf.Lerp(timing.severity.x, timing.severity.y, random()));
        Duration = Remaining = Mathf.Max(0.1f, Mathf.Lerp(timing.durationSeconds.x, timing.durationSeconds.y, random()));
        Active = true;
        return Started;
    }

    /// <summary>Ends the running episode now (the character died, fell asleep, left what brought it on).</summary>
    public void End(float gap = 0f)
    {
        Active = false;
        Remaining = 0f;
        GapLeft = Mathf.Max(0f, gap);
    }

    // ------------------------------------------------------------------ how bad, in a word
    /// <summary>
    /// The name of an episode by its strength within the trait's range: "Mild Migraine", "Migraine", "Severe Migraine".
    /// It is the name of the status the server shows, so a client learns how bad the episode is from it.
    /// </summary>
    public static string TierName(string label, float severity, Vector2 range)
    {
        if (range.y - range.x < 0.05f)
            return label;
        float f = Mathf.InverseLerp(range.x, range.y, severity);
        return f < 0.34f ? "Mild " + label : f < 0.67f ? label : "Severe " + label;
    }

    /// <summary>
    /// Reads <see cref="TierName"/> back: is <paramref name="statusName"/> an episode of <paramref name="label"/>, and
    /// roughly how strong (the middle of its third of the range)?
    /// </summary>
    public static bool TryReadTier(string statusName, string label, Vector2 range, out float severity)
    {
        severity = 0f;
        if (string.IsNullOrEmpty(statusName) || string.IsNullOrEmpty(label))
            return false;
        float f;
        if (statusName == label) f = range.y - range.x < 0.05f ? 1f : 0.5f;
        else if (statusName == "Mild " + label) f = 0.17f;
        else if (statusName == "Severe " + label) f = 0.85f;
        else return false;
        severity = Mathf.Lerp(range.x, range.y, f);
        return true;
    }
}
