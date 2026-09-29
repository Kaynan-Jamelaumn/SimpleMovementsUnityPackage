using System.Collections.Generic;

/// <summary>
/// The rules between traits (required, incompatible, mutually exclusive) for a set of traits being chosen, e.g. on
/// the character creation screen. The <see cref="TraitManager"/> applies the same rules to traits added in game.
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
}
