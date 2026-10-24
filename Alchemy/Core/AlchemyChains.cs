using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
/// Reaction networks: multi-step chains and alternative pathways.
/// <para>Reactions never run in isolation: a product is just another substance, so whatever one reaction makes any other
/// reaction can consume (Solar Sap → Vital Essence → Ardent Essence → ash). This class makes that network visible:
/// which reactions make or use a substance, every route that leads to it (<see cref="PathwaysTo"/>), and what the
/// player's journal knows of them (<see cref="KnownRoutes"/>). It also holds the extra built-in reactions that give
/// existing substances a second way of being made (<see cref="ExtraReactions"/>).</para>
/// </summary>
public static class AlchemyChains
{
    private sealed class Index
    {
        public List<int>[] Producers, Consumers;
    }

    private static readonly ConditionalWeakTable<AlchemyRules, Index> cache = new ConditionalWeakTable<AlchemyRules, Index>();

    private static Index IndexOf(AlchemyRules rules)
    {
        if (cache.TryGetValue(rules, out Index idx))
            return idx;
        int n = rules.SubstanceCount;
        idx = new Index { Producers = new List<int>[n], Consumers = new List<int>[n] };
        for (int i = 0; i < n; i++)
        {
            idx.Producers[i] = new List<int>();
            idx.Consumers[i] = new List<int>();
        }
        foreach (CompiledReaction r in rules.Reactions)
        {
            if (!r.Valid) continue;
            foreach (int p in r.Products)
                if (!idx.Producers[p].Contains(r.Index)) idx.Producers[p].Add(r.Index);
            foreach (int[] match in r.ReactantMatches)
                foreach (int s in match)
                    if (!idx.Consumers[s].Contains(r.Index)) idx.Consumers[s].Add(r.Index);
        }
        cache.Add(rules, idx);
        return idx;
    }

    /// <summary>Indices of the reactions that make <paramref name="substanceId"/> (alternative pathways when more than one).</summary>
    public static IReadOnlyList<int> Producers(AlchemyRules rules, string substanceId)
    {
        int i = rules.IndexOf(substanceId);
        return i >= 0 ? IndexOf(rules).Producers[i] : new List<int>();
    }

    /// <summary>Indices of the reactions that use <paramref name="substanceId"/> (what it can turn into next).</summary>
    public static IReadOnlyList<int> Consumers(AlchemyRules rules, string substanceId)
    {
        int i = rules.IndexOf(substanceId);
        return i >= 0 ? IndexOf(rules).Consumers[i] : new List<int>();
    }

    /// <summary>
    /// Every route (up to <paramref name="maxRoutes"/>) by which <paramref name="substanceId"/> can come about, as chains of
    /// reaction indices in the order they happen. A route stops where a substance is not made by any reaction (it comes
    /// from an ingredient), at solvents, or at <paramref name="maxDepth"/> steps.
    /// </summary>
    public static List<List<int>> PathwaysTo(AlchemyRules rules, string substanceId, int maxDepth = 4, int maxRoutes = 12)
    {
        var routes = new List<List<int>>();
        int target = rules.IndexOf(substanceId);
        if (target < 0) return routes;
        var visiting = new HashSet<int>();
        foreach (List<int> chain in Expand(rules, IndexOf(rules), target, maxDepth, visiting, maxRoutes))
            if (chain.Count > 0 && routes.Count < maxRoutes)
                routes.Add(chain);
        return routes;
    }

    private static List<List<int>> Expand(AlchemyRules rules, Index idx, int target, int depth, HashSet<int> visiting, int cap)
    {
        var result = new List<List<int>>();
        if (depth <= 0 || !visiting.Add(target))
        {
            result.Add(new List<int>());
            return result;
        }
        foreach (int ri in idx.Producers[target])
        {
            CompiledReaction r = rules.Reactions[ri];
            bool selfFed = false;
            foreach (int[] m in r.ReactantMatches) foreach (int s in m) selfFed |= s == target;
            if (selfFed) continue;
            // Chains of the reactants that are themselves made by other reactions (a third compound joining in).
            var prefixes = new List<List<int>> { new List<int>() };
            foreach (int[] match in r.ReactantMatches)
            {
                int s = -1;
                foreach (int c in match)
                    if (rules.Substances[c].role != SubstanceRole.Solvent && idx.Producers[c].Count > 0 && !visiting.Contains(c)) { s = c; break; }
                if (s < 0) continue;
                var next = new List<List<int>>();
                foreach (List<int> sub in Expand(rules, idx, s, depth - 1, visiting, cap))
                    foreach (List<int> pre in prefixes)
                    {
                        var merged = new List<int>(pre);
                        foreach (int x in sub) if (!merged.Contains(x)) merged.Add(x);
                        next.Add(merged);
                        if (next.Count >= 3) break;
                    }
                if (next.Count > 0) prefixes = next;
            }
            foreach (List<int> pre in prefixes)
            {
                var chain = new List<int>(pre) { ri };
                result.Add(chain);
                if (result.Count >= cap) break;
            }
            if (result.Count >= cap) break;
        }
        visiting.Remove(target);
        if (result.Count == 0) result.Add(new List<int>());
        return result;
    }

    /// <summary>"Solar Sap → Vital Essence  ›  Vital Essence → Ardent Essence".</summary>
    public static string DescribeRoute(AlchemyRules rules, List<int> route)
    {
        var parts = new List<string>();
        foreach (int ri in route) parts.Add(rules.Reactions[ri].Definition.Equation(rules));
        return string.Join("  ›  ", parts);
    }

    /// <summary>
    /// What the player's journal says of how a substance comes about and what becomes of it - only through reactions
    /// they understand: "Made by Vital Extraction (…)", more than one meaning several known routes, and "Goes on to…".
    /// </summary>
    public static List<string> KnownRoutes(AlchemyJournalData d, AlchemyRules rules, AlchemySubstance s)
    {
        var lines = new List<string>();
        if (s == null || s.role == SubstanceRole.Solvent) return lines;
        var made = new List<string>();
        foreach (int ri in Producers(rules, s.id))
        {
            AlchemyReaction def = rules.Reactions[ri].Definition;
            if (!def.IsGenerated && AlchemyJournal.IsUnderstood(d, rules, def.id))
                made.Add($"{def.Name} ({def.Equation(rules)})");
        }
        if (made.Count == 1) lines.Add("Made by " + made[0]);
        else if (made.Count > 1) lines.Add($"{made.Count} known routes: " + string.Join("; or ", made));
        var onward = new List<string>();
        foreach (int ri in Consumers(rules, s.id))
        {
            AlchemyReaction def = rules.Reactions[ri].Definition;
            if (!def.IsGenerated && AlchemyJournal.IsUnderstood(d, rules, def.id))
                onward.Add(def.Name);
        }
        if (onward.Count > 0) lines.Add("Goes on to react in: " + string.Join(", ", onward));
        return lines;
    }

    /// <summary>Adds <see cref="ExtraReactions"/> to a built-in reaction list (ids already present are kept).</summary>
    public static List<AlchemyReaction> WithChains(List<AlchemyReaction> list)
    {
        foreach (AlchemyReaction r in ExtraReactions())
            if (!list.Exists(x => x.id == r.id)) list.Add(r);
        return list;
    }

    /// <summary>
    /// Second ways of making existing substances, and links that extend chains:
    /// Venom also from sunpetal's bitter latex (not only bitter resin); Vital Essence also from sugars and fungal pulp;
    /// Serene Essence also from lavender oil joining mana essence (moon dew → mana → serene: a three-compound chain);
    /// Salamander Essence also from hot ardent essence meeting pepperfire oil (sunpetal → vital → ardent → salamander).
    /// </summary>
    public static List<AlchemyReaction> ExtraReactions() => new List<AlchemyReaction>
    {
        new AlchemyReaction { id = "latex_drawing", displayName = "Latex Drawing",
            description = "Warm spirit draws venom out of bitter latex too - a second route to venom, via sunpetal stems instead of bitterroot." }
            .Consumes(AlchemyDefaults.BitterLatex).Consumes(AlchemyDefaults.Spirit, 0.5f, 0.5f).Makes(AlchemyDefaults.Venom, 0.7f).Makes(AlchemyDefaults.Spirit, 0.5f)
            .Speed(0.05f, 50f, 25f).Between(25f, 80f)
            .Looks(ReactionVisual.Bubbles, "a faint green film gathers around the milky trace"),
        new AlchemyReaction { id = "mycelial_rendering", displayName = "Mycelial Rendering",
            description = "Sugars feed mushroom flesh into a weak vital essence: healing from honey and fungi, without any sunpetal." }
            .Consumes(AlchemyDefaults.Sugars).Consumes(AlchemyDefaults.FungalPulp).Makes(AlchemyDefaults.VitalEssence, 0.6f).Makes(AlchemyDefaults.Water, 0.3f)
            .Speed(0.01f, 60f, 20f).Between(40f, 100f)
            .Looks(ReactionVisual.Glow, "a pale rose tint seeps out of the pulp"),
        new AlchemyReaction { id = "lavender_calming", displayName = "Lavender Calming",
            description = "Lavender oil settles mana essence into serene essence: moon dew → mana → serene, a route that never touches frost oil." }
            .Consumes(AlchemyDefaults.LavenderOil).Consumes(AlchemyDefaults.ManaEssence).Makes(AlchemyDefaults.SereneEssence, 1.3f)
            .Speed(0.2f, 40f, 0f).Between(15f, 75f)
            .Heat(-2f).Looks(ReactionVisual.Glow, "the violet hum softens into a calm teal"),
        new AlchemyReaction { id = "ember_binding", displayName = "Ember Binding",
            description = "Hot ardent essence takes up pepperfire oil: sunpetal → vital → ardent → salamander, a longer road to salamander essence." }
            .Consumes(AlchemyDefaults.ArdentEssence).Consumes(AlchemyDefaults.PepperOil).Makes(AlchemyDefaults.SalamanderEssence, 1.6f)
            .Speed(0.2f, 90f, 0f).Between(76f, 110f)
            .Heat(2f).Looks(ReactionVisual.Glow, "the amber flares scarlet where the oil meets it"),
    };
}
