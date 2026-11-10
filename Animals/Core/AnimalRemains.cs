using System;
using UnityEngine;

/// <summary>
/// What a dead animal's remains are, for butchering through Harvesting (the runtime lays out the carcass from it): its
/// anatomy, size and age, how it was raised (a quality factor for every piece), and what illness, starving and poison did
/// to its meat, organs and hide. Server side only (it names the owner's key).
/// </summary>
[Serializable]
public class AnimalRemainsReport
{
    public string animal = "", species = "", anatomy = "", name = "", owner = "";
    [Tooltip("How it died, in words (\"Slaughtered\", \"Died of infection (Severe)\").")]
    public string cause = "";
    [Tooltip("The condition that killed it (an id; empty: none).")]
    public string condition = "";
    public AnimalDeathCause deathCause;
    [Tooltip("How it was raised: x the quality of every piece (1 = as a wild animal; 0.3-1.5).")]
    public float rearing = 1f;
    [Tooltip("How much meat is on it (1 = well fleshed; a starved animal less).")]
    public float lean = 1f;
    [Tooltip("Poison in its flesh (0-1).")]
    public float poison;
    [Tooltip("Taint from an infection or worms that showed (0-1; Harvesting marks its meat and organs Tainted above 0.12).")]
    public float taint;
    [Tooltip("Gut contamination from a digestive illness (0-1).")]
    public float contamination;
    [Tooltip("Damage to its hide or plumage from a lifetime's poor hygiene (0-1).")]
    public float coatDamage;
    [Tooltip("Parasites' severity at death (0-1): the hide gets flaws on a share of it.")]
    public float parasites;
    [Tooltip("Its body's size against an average adult (0.2-2).")]
    public float sizeScale = 1f;
    [Tooltip("0 young, 1 adult, 2 old (HarvestAge Young / Adult / Old).")]
    public int age = 1;
    [Tooltip("Game hours since it died (the carcass is laid out already that old).")]
    public double hoursDead;
    [Tooltip("A stable seed for its carcass (from its id).")]
    public int seed;
}

/// <summary>How a dead animal's remains come out of how it lived and died (the rules' Remains group).</summary>
public static class AnimalRemains
{
    /// <summary>
    /// The report of a dead animal's remains (§7 with revision 1):
    /// <list type="bullet">
    /// <item>score = (rearing welfare x life welfare + rearing nutrition x life nutrition + rearing health x life health +
    /// rearing hygiene x life hygiene) / (100 x the sum of those weights); rearing = Lerp(rearing min, rearing max, score) -
    /// illness penalty x worst severity/100 - (starved or malnourished: starvation penalty) - (poisoned: poison penalty) +
    /// 0.06 x (production quality gene - 0.5) - 0.15 x life illness/100 - min(0.1, starved hours/240), clamped 0.3-1.5.</item>
    /// <item>lean = clamp(0.25 + 0.75 x Smooth(15, 75, the mean of its life nutrition and its nutrition), 0.12, 1), x 0.7 if
    /// it starved to death; poison (poisoned, or toxin above tolerance) = clamp((toxin / tolerance - 0.5) / 2, 0.15, 0.9);
    /// taint = its worst illness that taints the carcass (<see cref="Taints"/>: an infection or worms, not hoof rot) in a
    /// stage that shows, / 100 x 0.8 (at least 0.15 when such an illness killed it; one still hidden taints nothing);
    /// contamination = its digestive illness / 100 x 0.7; coat damage = clamp01((50 - life hygiene) / 100); the hide's
    /// parasite flaws = its worst worms / 100 (hidden ones too).</item>
    /// </list>
    /// "Starved", "poisoned" and the like count the death's cause and the active illnesses of that kind.
    /// </summary>
    public static AnimalRemainsReport Report(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat, double now, AnimalRules rules)
    {
        var r = new AnimalRemainsReport();
        if (a == null) return r;
        rules ??= cat?.Rules ?? AnimalRules.Defaults();
        // An animal that never lived a Phase 2 step (it died in a Phase 1 save) is judged by the life record's fair defaults.
        AnimalLife life = a.life ?? new AnimalLife();
        r.animal = a.id ?? "";
        r.species = a.species ?? "";
        r.anatomy = sp?.harvestAnatomy ?? "";
        r.name = AnimalSim.NameOf(a, sp);
        r.owner = a.owner ?? "";
        r.deathCause = a.deathCause;
        r.condition = a.deathCondition ?? "";
        r.cause = CauseText(a, sp, cat);

        float worst = AnimalHealth.Worst(a, cat);
        bool starved = a.deathCause == AnimalDeathCause.Starvation || a.deathCause == AnimalDeathCause.Malnutrition
                       || AnimalHealth.WorstOf(a, cat, AnimalConditionKind.Malnutrition) != null;
        bool poisoned = a.deathCause == AnimalDeathCause.Poisoning || AnimalHealth.WorstOf(a, cat, AnimalConditionKind.Poisoning) != null;
        r.rearing = Rearing(a, life, rules, worst, starved, poisoned);

        float nutrition = 0.5f * life.nutrition + 0.5f * a.nutrition;
        r.lean = Mathf.Clamp(0.25f + 0.75f * AnimalMath.Smooth(15f, 75f, nutrition), 0.12f, 1f) * (a.deathCause == AnimalDeathCause.Starvation ? 0.7f : 1f);
        float tolerance = Mathf.Max(0.01f, AnimalSim.Tolerance(a, sp));
        r.poison = poisoned || a.toxin > tolerance ? Mathf.Clamp((a.toxin / tolerance - 0.5f) / 2f, 0.15f, 0.9f) : 0f;

        // Taint: an illness of the whole body (a fever, worms) that shows runs through the carcass; hoof rot stays in the
        // hoof, and one still hidden leaves the meat fit to eat. The hide's flaws count worms, hidden ones too (below).
        AnimalCondition parasites = AnimalHealth.WorstOf(a, cat, AnimalConditionKind.Parasites);
        float taint = 0f;
        if (a.conditions != null && cat != null)
            foreach (AnimalCondition k in a.conditions)
            {
                if (k == null || k.Recovering) continue;
                AnimalConditionData d = cat.Condition(k.id);
                if (Taints(d) && d.Stage(k.stage).visible) taint = Mathf.Max(taint, k.severity / 100f * 0.8f);
            }
        AnimalConditionData killer = string.IsNullOrEmpty(a.deathCondition) ? null : cat?.Condition(a.deathCondition);
        if (Taints(killer)) taint = Mathf.Max(taint, 0.15f);
        r.taint = AnimalMath.Clamp01(taint);
        AnimalCondition digestive = AnimalHealth.WorstOf(a, cat, AnimalConditionKind.Digestive);
        r.contamination = digestive != null ? AnimalMath.Clamp01(digestive.severity / 100f * 0.7f) : 0f;
        r.coatDamage = AnimalMath.Clamp01((50f - life.hygiene) / 100f);
        r.parasites = parasites != null ? AnimalMath.Clamp01(parasites.severity / 100f) : 0f;
        r.sizeScale = Mathf.Clamp(AnimalCare.SizeOf(sp, a), 0.2f, 2f);
        r.age = a.stage < GrowthStage.Adolescent ? 0 : a.stage == GrowthStage.Senior ? 2 : 1;
        r.hoursDead = a.Alive || a.diedAt < 0.0 ? 0.0 : Math.Max(0.0, now - a.diedAt);
        r.seed = AnimalMath.Hash(a.id ?? "");
        return r;
    }

    /// <summary>Does an illness taint its carcass at death: an infection or worms of the whole body (Taints Carcass; not hoof rot)?</summary>
    public static bool Taints(AnimalConditionData c) =>
        c != null && c.taintsCarcass && (c.kind == AnimalConditionKind.Infection || c.kind == AnimalConditionKind.Parasites);

    /// <summary>How well it was raised: the quality factor of its remains (and, Phase 3, its products), 0.3-1.5 (see <see cref="Report"/>).</summary>
    public static float Rearing(AnimalRecord a, AnimalLife life, AnimalRules rules, float worst, bool starved, bool poisoned)
    {
        if (a == null) return 1f;
        rules ??= AnimalRules.Defaults();
        life ??= a.life ?? new AnimalLife();
        float ww = Mathf.Max(0f, rules.rearingWelfare), wn = Mathf.Max(0f, rules.rearingNutrition), wh = Mathf.Max(0f, rules.rearingHealth), wy = Mathf.Max(0f, rules.rearingHygiene);
        float sum = ww + wn + wh + wy;
        float score = sum > 0f ? (ww * life.welfare + wn * life.nutrition + wh * life.health + wy * life.hygiene) / (100f * sum) : 0.5f;
        float rearing = AnimalMath.Lerp(rules.rearingMin, rules.rearingMax, score);
        rearing -= rules.illnessPenalty * AnimalMath.Clamp100(worst) / 100f;
        if (starved) rearing -= rules.starvationPenalty;
        if (poisoned) rearing -= rules.poisonPenalty;
        rearing += 0.06f * ((a.genes != null ? a.genes.productionQuality : 0.5f) - 0.5f);
        rearing -= 0.15f * AnimalMath.Clamp100(life.illness) / 100f;
        rearing -= Mathf.Min(0.1f, Mathf.Max(0f, life.starvedHours) / 240f);
        return Mathf.Clamp(rearing, 0.3f, 1.5f);
    }

    /// <summary>How it died, in words: "Slaughtered", "Died of infection (Severe)", "Starved to death"...</summary>
    public static string CauseText(AnimalRecord a, AnimalSpeciesData sp, IAnimalCatalog cat)
    {
        if (a == null) return "";
        if (a.Alive) return "Alive";
        if (a.deathCause == AnimalDeathCause.Slaughtered) return "Slaughtered";
        AnimalConditionData c = string.IsNullOrEmpty(a.deathCondition) ? null : cat?.Condition(a.deathCondition);
        if (c != null)
        {
            AnimalCondition k = a.Condition(c.id);
            return k != null ? $"Died of {c.DisplayName.ToLowerInvariant()} ({c.StageName(k)})" : $"Died of {c.DisplayName.ToLowerInvariant()}";
        }
        switch (a.deathCause)
        {
            case AnimalDeathCause.Starvation: return "Starved to death";
            case AnimalDeathCause.Dehydration: return "Died of thirst";
            case AnimalDeathCause.Poisoning: return "Poisoned";
            case AnimalDeathCause.Malnutrition: return "Wasted away from poor feeding";
            case AnimalDeathCause.OldAge: return "Died of old age";
            case AnimalDeathCause.Illness: return "Died of illness";
            case AnimalDeathCause.Injury: return "Died of its injuries";
            default: return "Died";
        }
    }
}
