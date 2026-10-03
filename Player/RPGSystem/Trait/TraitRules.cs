using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rules between traits (required, incompatible, mutually exclusive) for a set of traits being chosen, e.g. on
/// the character creation screen, and the race / class rules (only-for, forbidden, cost affinities) given the
/// character's <see cref="PlayerClass"/> and archetypes. The <see cref="TraitManager"/> applies the same rules to traits
/// added in game.
/// </summary>
public static class TraitRules
{
    /// <summary>
    /// Why <paramref name="trait"/> cannot be taken together with <paramref name="chosen"/> (the traits already
    /// picked plus any the character gets anyway, like the class's starting traits). Null = it can.
    /// </summary>
    public static string WhyCannotCombine(Trait trait, IEnumerable<Trait> chosen)
    {
        if (trait == null)
            return "No trait.";
        if (chosen == null)
            return trait.requiredTraits.Exists(r => r != null) ? $"Requires {trait.requiredTraits.Find(r => r != null).Name}." : null;

        var set = new HashSet<Trait>();
        foreach (Trait t in chosen)
            if (t != null)
                set.Add(t);

        foreach (Trait r in trait.requiredTraits)
            if (r != null && !set.Contains(r))
                return $"Requires {r.Name}.";
        foreach (Trait other in set)
        {
            if (other == trait)
                continue;
            if (trait.incompatibleTraits.Contains(other) || other.incompatibleTraits.Contains(trait))
                return $"Incompatible with {other.Name}.";
            if (trait.mutuallyExclusiveTraits.Contains(other) || other.mutuallyExclusiveTraits.Contains(trait))
                return $"Cannot be taken with {other.Name}.";
        }
        return null;
    }

    /// <summary>
    /// Why <paramref name="trait"/> would exceed the active-trait limit among the traits the player picked (null = fine).
    /// Only traits with an active skill on their own key count (<see cref="Trait.HasActiveSkill"/>).
    /// </summary>
    public static string WhyTooManyActive(Trait trait, IEnumerable<Trait> picked, int maxActive = 1)
    {
        if (trait == null || !trait.HasActiveSkill)
            return null;
        int n = 0;
        Trait other = null;
        if (picked != null)
            foreach (Trait t in picked)
                if (t != null && t != trait && t.HasActiveSkill) { n++; other = t; }
        if (n < maxActive)
            return null;
        return maxActive <= 1 ? $"Only one active trait per character ({other.Name} is already chosen)." : $"Only {maxActive} active traits per character.";
    }

    /// <summary>The chosen trait that needs <paramref name="trait"/> (so it cannot be removed first), or null.</summary>
    public static Trait RequiredBy(Trait trait, IEnumerable<Trait> chosen)
    {
        if (trait == null || chosen == null)
            return null;
        foreach (Trait t in chosen)
            if (t != null && t != trait && t.requiredTraits.Contains(trait))
                return t;
        return null;
    }

    // ------------------------------------------------------------------ race / class rules
    /// <summary>Why <paramref name="trait"/> is not allowed for these archetypes (null = allowed).</summary>
    public static string WhyNot(Trait trait, IList<CharacterArchetype> archetypes)
    {
        if (trait == null)
            return "No trait.";
        if (!trait.IsAllowedFor(archetypes))
        {
            var names = new List<string>();
            foreach (CharacterArchetype a in trait.onlyFor)
                if (a != null) names.Add(a.Name);
            return $"Only for {string.Join(" / ", names)}.";
        }
        if (archetypes != null)
            foreach (CharacterArchetype a in archetypes)
                if (a != null && a.forbiddenTraits.Contains(trait))
                    return $"{a.Name} cannot take {trait.Name}.";
        return null;
    }

    public static bool Allowed(Trait trait, IList<CharacterArchetype> archetypes) => WhyNot(trait, archetypes) == null;

    /// <summary>
    /// Points the trait costs: the class's cost (preferred / difficult types) × every archetype's affinity. Drawbacks
    /// (negative costs) are not changed, so cheap-drawback exploits are impossible.
    /// </summary>
    public static int Cost(Trait trait, PlayerClass playerClass, IList<CharacterArchetype> archetypes)
    {
        if (trait == null)
            return 0;
        int baseCost = playerClass != null ? playerClass.GetTraitCost(trait) : trait.cost;
        if (baseCost <= 0 || archetypes == null)
            return baseCost;
        float m = 1f;
        foreach (CharacterArchetype a in archetypes)
            if (a != null) m *= a.TraitCostMultiplier(trait);
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * m));
    }

    /// <summary>Trait points the archetypes add to the budget.</summary>
    public static int BonusPoints(IList<CharacterArchetype> archetypes)
    {
        int n = 0;
        if (archetypes != null)
            foreach (CharacterArchetype a in archetypes)
                if (a != null) n += a.bonusTraitPoints;
        return n;
    }

    /// <summary>
    /// Traits offered at creation: the class's selectable traits (or <paramref name="everyTrait"/> when there is no class
    /// list) plus the archetypes' offered traits, minus what the rules forbid and what the archetypes already give.
    /// </summary>
    public static List<Trait> Selectable(PlayerClass playerClass, IList<CharacterArchetype> archetypes, IEnumerable<Trait> everyTrait = null)
    {
        var result = new List<Trait>();
        var seen = new HashSet<Trait>();
        void Offer(Trait t)
        {
            if (t == null || !t.availableAtCreation || !seen.Add(t))
                return;
            if (playerClass != null && playerClass.startingTraits.Contains(t))
                return;
            if (archetypes != null)
                foreach (CharacterArchetype a in archetypes)
                    if (a != null && a.innateTraits.Contains(t)) return;
            if (Allowed(t, archetypes))
                result.Add(t);
        }

        List<Trait> classList = playerClass != null ? playerClass.GetSelectableTraits() : null;
        if (classList != null && classList.Count > 0)
            foreach (Trait t in classList) Offer(t);
        else if (everyTrait != null)
            foreach (Trait t in everyTrait) Offer(t);
        if (archetypes != null)
            foreach (CharacterArchetype a in archetypes)
                if (a != null)
                    foreach (Trait t in a.offeredTraits) Offer(t);
        return result;
    }

    /// <summary>Archetypes that cannot be combined (null = fine).</summary>
    public static string WhyIncompatible(IList<CharacterArchetype> archetypes)
    {
        if (archetypes == null)
            return null;
        for (int i = 0; i < archetypes.Count; i++)
            for (int j = i + 1; j < archetypes.Count; j++)
                if (archetypes[i] != null && archetypes[j] != null && !archetypes[i].IsCompatibleWith(archetypes[j]))
                    return $"{archetypes[i].Name} cannot be combined with {archetypes[j].Name}.";
        return null;
    }
}
