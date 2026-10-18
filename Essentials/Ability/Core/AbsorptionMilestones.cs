using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ability absorption milestones: levels that each allow one more absorbed ability (e.g. 10, 20, 35, 40, 45 = one ability
/// at level 10, two at 20... five at 45). With the Milestone Regression death penalty, dying sends the character back to
/// its last milestone level (57 → 45) and it loses the ability absorbed most recently.
/// <para>Where they come from (first one that is on): the player's <see cref="DeathPenalty"/> (its own milestones), the
/// class archetype, the race archetype, then <see cref="AbsorptionSettings"/> (the project-wide rule).</para>
/// </summary>
[Serializable]
public class AbsorptionMilestones
{
    [Tooltip("Limit absorbed abilities by level (one more at each milestone). Off = no limit from levels.")]
    public bool enabled = false;
    [Tooltip("Levels that each allow one more absorbed ability.")]
    public List<int> levels = new List<int> { 10, 20, 35, 40, 45 };
    [Tooltip("Abilities that can be absorbed before the first milestone.")]
    [Min(0)] public int startingSlots = 0;

    /// <summary>How many absorbed abilities a character of <paramref name="level"/> may hold.</summary>
    public int CapacityAt(int level)
    {
        int n = startingSlots;
        if (levels != null)
            foreach (int l in levels)
                if (l <= level) n++;
        return n;
    }

    /// <summary>The highest milestone at or below <paramref name="level"/> (0 = none).</summary>
    public int MilestoneAtOrBelow(int level)
    {
        int best = 0;
        if (levels != null)
            foreach (int l in levels)
                if (l <= level && l > best) best = l;
        return best;
    }

    /// <summary>The highest milestone strictly below <paramref name="level"/> (0 = none).</summary>
    public int MilestoneBelow(int level) => MilestoneAtOrBelow(level - 1);

    /// <summary>The next milestone above <paramref name="level"/> (0 = none left).</summary>
    public int NextMilestone(int level)
    {
        int best = int.MaxValue;
        if (levels != null)
            foreach (int l in levels)
                if (l > level && l < best) best = l;
        return best == int.MaxValue ? 0 : best;
    }

    public string Describe()
    {
        if (!enabled) return "No level limit";
        var sorted = new List<int>(levels ?? new List<int>());
        sorted.Sort();
        return (startingSlots > 0 ? $"{startingSlots} at start, " : "") + "one more at level " + string.Join(", ", sorted);
    }

    public AbsorptionMilestones Clone()
    {
        var c = (AbsorptionMilestones)MemberwiseClone();
        c.levels = new List<int>(levels ?? new List<int>());
        return c;
    }

    /// <summary>The milestones that apply to the character of <paramref name="anyPart"/>.</summary>
    public static AbsorptionMilestones For(Component anyPart)
    {
        if (anyPart != null)
        {
            DeathPenalty penalty = anyPart.GetComponentInParent<DeathPenalty>() ?? anyPart.GetComponentInChildren<DeathPenalty>();
            if (penalty != null && penalty.OwnMilestones != null && penalty.OwnMilestones.enabled)
                return penalty.OwnMilestones;
            CharacterIdentity id = CharacterIdentity.For(anyPart, false);
            if (id != null && id.Archetypes != null)
            {
                foreach (ArchetypeKind kind in new[] { ArchetypeKind.Class, ArchetypeKind.Race, ArchetypeKind.Background, ArchetypeKind.Other })
                    foreach (CharacterArchetype a in id.Archetypes)
                        if (a != null && a.kind == kind && a.absorptionMilestones != null && a.absorptionMilestones.enabled)
                            return a.absorptionMilestones;
            }
        }
        AbsorptionSettings settings = AbsorptionSettings.Instance;
        return settings != null && settings.milestones != null ? settings.milestones : new AbsorptionMilestones();
    }

    /// <summary>The level of the character of <paramref name="anyPart"/> (1 without an Experience Manager).</summary>
    public static int LevelOf(Component anyPart)
    {
        if (anyPart == null) return 1;
        ExperienceManager xp = anyPart.GetComponentInParent<ExperienceManager>() ?? anyPart.GetComponentInChildren<ExperienceManager>();
        return xp != null ? Mathf.Max(1, xp.CurrentLevel) : 1;
    }
}
