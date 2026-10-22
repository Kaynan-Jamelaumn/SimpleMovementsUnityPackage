using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A potion (or failure) the player has made, with the best result and how it was made.</summary>
[Serializable]
public class AlchemyDiscovery
{
    public string outcomeId = "";
    public string name = "";
    public int timesMade;
    public int bottlesMade;
    public float bestQuality;
    public string bestTier = "";
    public string bestEquipment = "";
    public long firstUtc;
    public long lastUtc;
    [Tooltip("The steps of the best preparation (replayable by hand: same steps, same result).")]
    public List<string> bestProcedure = new List<string>();
}

/// <summary>A reaction the player has seen happen (understood after enough preparations).</summary>
[Serializable]
public class AlchemyReactionNote
{
    public string reactionId = "";
    public int timesSeen;
    public string lastBrewId = "";
    public float lowestTemperature = 9999f;
    public float highestTemperature = -9999f;
}

/// <summary>An ingredient the player has used.</summary>
[Serializable]
public class AlchemyIngredientNote
{
    public string ingredientId = "";
    public string name = "";
    public int timesUsed;
}

/// <summary>One finished preparation: what it became and what was done.</summary>
[Serializable]
public class AlchemyExperiment
{
    public long utc;
    public string outcomeId = "";
    public string name = "";
    public float quality;
    public int bottles;
    public string equipment = "";
    public List<string> procedure = new List<string>();
    public List<string> notes = new List<string>();
}

/// <summary>
/// Everything a player knows about alchemy (JsonUtility-friendly): the outcomes they have made, the reactions they
/// have seen and understood, the substances they can name, the ingredients they have used and their last experiments.
/// Nothing is revealed up front: the station window and the journal only show what is in here.
/// </summary>
[Serializable]
public class AlchemyJournalData
{
    public int version = 1;
    public List<AlchemyDiscovery> discoveries = new List<AlchemyDiscovery>();
    public List<AlchemyReactionNote> reactions = new List<AlchemyReactionNote>();
    public List<string> identified = new List<string>();
    [Tooltip("Substances the player has seen stay out of solution: the journal tells how well they dissolve.")]
    public List<string> undissolvedSeen = new List<string>();
    public List<AlchemyIngredientNote> ingredients = new List<AlchemyIngredientNote>();
    public List<AlchemyExperiment> experiments = new List<AlchemyExperiment>();

    public const int MaxExperiments = 40;

    public string ToJson(bool pretty = true) => JsonUtility.ToJson(this, pretty);

    public static AlchemyJournalData FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new AlchemyJournalData();
        try
        {
            AlchemyJournalData d = JsonUtility.FromJson<AlchemyJournalData>(json) ?? new AlchemyJournalData();
            d.discoveries ??= new List<AlchemyDiscovery>();
            d.reactions ??= new List<AlchemyReactionNote>();
            d.identified ??= new List<string>();
            d.undissolvedSeen ??= new List<string>();
            d.ingredients ??= new List<AlchemyIngredientNote>();
            d.experiments ??= new List<AlchemyExperiment>();
            return d;
        }
        catch (Exception)
        {
            return new AlchemyJournalData();
        }
    }
}

/// <summary>
/// How players learn: the rules of the journal. While a player tends a preparation, every reaction that happens in it
/// is seen once per preparation; seen in enough preparations (Observations To Understand) it is understood - named,
/// with its equation and conditions - and the substances it names are identified. Bottling identifies the outcome's
/// compound and records the discovery with the steps that made it, so a discovery can be made again.
/// A transparent vessel identifies whatever is visible in it.
/// </summary>
public static class AlchemyJournal
{
    // ------------------------------------------------------------------ questions
    public static bool IsDiscovered(AlchemyJournalData d, string outcomeId) => Discovery(d, outcomeId) != null;

    public static AlchemyDiscovery Discovery(AlchemyJournalData d, string outcomeId)
    {
        if (d == null || string.IsNullOrEmpty(outcomeId))
            return null;
        foreach (AlchemyDiscovery x in d.discoveries)
            if (string.Equals(x.outcomeId, outcomeId, StringComparison.OrdinalIgnoreCase))
                return x;
        return null;
    }

    public static AlchemyReactionNote ReactionNote(AlchemyJournalData d, string reactionId)
    {
        if (d == null || string.IsNullOrEmpty(reactionId))
            return null;
        foreach (AlchemyReactionNote x in d.reactions)
            if (string.Equals(x.reactionId, reactionId, StringComparison.OrdinalIgnoreCase))
                return x;
        return null;
    }

    public static bool IsUnderstood(AlchemyJournalData d, AlchemyRules rules, string reactionId)
    {
        AlchemyReactionNote n = ReactionNote(d, reactionId);
        return n != null && n.timesSeen >= Math.Max(1, rules.Settings.observationsToUnderstand);
    }

    public static bool IsIdentified(AlchemyJournalData d, AlchemyRules rules, string substanceId)
    {
        AlchemySubstance s = rules.Substance(substanceId);
        if (s != null && s.alwaysKnown)
            return true;
        return d != null && d.identified.Exists(x => string.Equals(x, substanceId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The substance's name if identified, else what it looks like ("an unknown golden, sweet-smelling sap").</summary>
    public static string SubstanceLabel(AlchemyJournalData d, AlchemyRules rules, string substanceId)
    {
        AlchemySubstance s = rules.Substance(substanceId);
        if (s == null)
            return substanceId;
        if (IsIdentified(d, rules, substanceId))
            return s.Name;
        return string.IsNullOrWhiteSpace(s.observation) ? "an unknown substance" : "unknown: " + s.observation;
    }

    // ------------------------------------------------------------------ learning
    /// <summary>
    /// Credits the player with what they can see in <paramref name="brew"/> now: reactions that happened in it
    /// (once per preparation), and with a transparent vessel the substances in it. Returns what they just learnt
    /// ("You understand Vital Extraction", "You identified Vital Essence").
    /// </summary>
    public static List<string> Observe(AlchemyJournalData d, AlchemyBrew brew)
    {
        var learnt = new List<string>();
        if (d == null || brew == null)
            return learnt;
        AlchemyRules rules = brew.Rules;
        AlchemySimulationSettings s = rules.Settings;
        float temp = (float)brew.Mixture.Temperature;
        for (int j = 0; j < rules.Reactions.Length; j++)
        {
            AlchemyReaction def = rules.Reactions[j].Definition;
            AlchemyReactionNote note = ReactionNote(d, def.id);
            if (brew.IsReacting(j) && note != null)
            {
                note.lowestTemperature = Math.Min(note.lowestTemperature, temp);
                note.highestTemperature = Math.Max(note.highestTemperature, temp);
            }
            if (brew.TotalOf(j) < s.observeAmount)
                continue;
            if (note == null)
            {
                note = new AlchemyReactionNote { reactionId = def.id };
                d.reactions.Add(note);
            }
            if (note.lastBrewId == brew.Id)
                continue;
            note.lastBrewId = brew.Id;
            note.timesSeen++;
            note.lowestTemperature = Math.Min(note.lowestTemperature, temp);
            note.highestTemperature = Math.Max(note.highestTemperature, temp);
            if (note.timesSeen == Math.Max(1, s.observationsToUnderstand))
            {
                learnt.Add($"You understand {def.Name}: {def.Equation(rules)}.");
                foreach (ReactionReactant r in def.reactants)
                    if (r?.substance != null && r.substance.mode == SubstanceSelectorMode.Substance)
                        Identify(d, rules, r.substance.id, learnt);
                foreach (ReactionProduct p in def.products)
                    if (p != null)
                        Identify(d, rules, p.substance, learnt);
            }
        }
        AlchemyMixture mix = brew.Mixture;
        for (int i = 0; i < rules.SubstanceCount; i++)
            if (mix.UndissolvedAmount(i) >= s.noticeableAmount && !d.undissolvedSeen.Contains(rules.Substances[i].id))
            {
                d.undissolvedSeen.Add(rules.Substances[i].id);
                if (IsIdentified(d, rules, rules.Substances[i].id))
                    learnt.Add($"{rules.Substances[i].Name} does not dissolve well in this liquid.");
            }
        if (brew.Equipment.revealsSubstances)
        {
            AlchemyMixture m = brew.Mixture;
            for (int i = 0; i < rules.SubstanceCount; i++)
                if (m.Amount(i) >= s.noticeableAmount)
                    Identify(d, rules, rules.Substances[i].id, learnt);
        }
        return learnt;
    }

    /// <summary>Adds a substance to what the player can name (with a message in <paramref name="learnt"/>).</summary>
    public static bool Identify(AlchemyJournalData d, AlchemyRules rules, string substanceId, List<string> learnt = null)
    {
        AlchemySubstance s = rules.Substance(substanceId);
        if (d == null || s == null || IsIdentified(d, rules, substanceId))
            return false;
        d.identified.Add(s.id);
        learnt?.Add($"You identified {s.Name}.");
        return true;
    }

    public static void RecordIngredient(AlchemyJournalData d, string id, string name, int count = 1)
    {
        if (d == null || string.IsNullOrEmpty(id))
            return;
        AlchemyIngredientNote n = d.ingredients.Find(x => string.Equals(x.ingredientId, id, StringComparison.OrdinalIgnoreCase));
        if (n == null)
        {
            n = new AlchemyIngredientNote { ingredientId = id, name = name };
            d.ingredients.Add(n);
        }
        n.timesUsed += count;
        if (!string.IsNullOrEmpty(name)) n.name = name;
    }

    /// <summary>
    /// Records a finished result (bottled, collected or poured away): the discovery (new, or a better one with the
    /// steps that made it), the experiment log, and the outcome's compound becomes identified. Returns messages.
    /// </summary>
    public static List<string> RecordResult(AlchemyJournalData d, AlchemyBrew brew, AlchemyResult result, int bottles, long utcTicks)
    {
        var learnt = new List<string>();
        if (d == null || brew == null || result?.Outcome == null)
            return learnt;
        AlchemyRules rules = brew.Rules;
        learnt.AddRange(Observe(d, brew));
        List<string> procedure = Procedure(brew);
        procedure.Add($"{AlchemyText.Clock(brew.Elapsed)}  → {result.DisplayName}{(bottles > 0 ? $" × {bottles}" : "")}");

        AlchemyDiscovery found = Discovery(d, result.OutcomeId);
        if (found == null)
        {
            found = new AlchemyDiscovery { outcomeId = result.OutcomeId, name = result.Outcome.Name, firstUtc = utcTicks, bestQuality = -1f };
            d.discoveries.Add(found);
            learnt.Add(result.IsFailure ? $"New result: {result.Outcome.Name}." : $"Discovery: {result.Outcome.Name}!");
        }
        found.timesMade++;
        found.bottlesMade += bottles;
        found.lastUtc = utcTicks;
        if (result.Quality > found.bestQuality + 1e-4)
        {
            if (found.bestQuality >= 0f && !result.IsFailure)
                learnt.Add($"Your best {result.Outcome.Name} yet ({result.Quality * 100:0}%).");
            found.bestQuality = (float)result.Quality;
            found.bestTier = result.TierData?.label ?? "";
            found.bestEquipment = brew.Equipment.displayName;
            found.bestProcedure = new List<string>(procedure);
        }
        if (result.Outcome.HasPotency && result.Outcome.potency.mode == SubstanceSelectorMode.Substance)
            Identify(d, rules, result.Outcome.potency.id, learnt);

        d.experiments.Add(new AlchemyExperiment
        {
            utc = utcTicks, outcomeId = result.OutcomeId, name = result.DisplayName, quality = (float)result.Quality, bottles = bottles,
            equipment = brew.Equipment.displayName, procedure = procedure, notes = new List<string>(result.Notes),
        });
        if (d.experiments.Count > AlchemyJournalData.MaxExperiments)
            d.experiments.RemoveRange(0, d.experiments.Count - AlchemyJournalData.MaxExperiments);
        return learnt;
    }

    // ------------------------------------------------------------------ procedures
    /// <summary>
    /// The preparation's history as readable steps ("0:00  Added 3 × Sunpetal", "0:05  Fire 35% (→ 97 °C)",
    /// "1:10  Bottled 4"). Close fire changes and stirs are merged.
    /// </summary>
    public static List<string> Procedure(AlchemyBrew brew)
    {
        var lines = new List<string>();
        if (brew == null)
            return lines;
        double step = brew.Rules.Settings.stepSeconds;
        IReadOnlyList<AlchemyOperation> ops = brew.History;
        int stirs = 0, pumps = 0, stirChanges = 0;
        double stirAt = -100, heatAt = -100, pumpAt = -100, stirringFrom = -1, stirringSum = 0, lastSpeed = 0, lastSpeedAt = 0, ventAt = -100;
        int heatLine = -1, ventLine = -1;
        // Continuous stirring and the bellows make many small operations: they are told as runs ("Stirred at about 45%
        // for 0:40, unevenly", "Worked the bellows × 6").
        void EndStirring(double at)
        {
            if (stirringFrom < 0) return;
            stirringSum += lastSpeed * (at - lastSpeedAt);
            double length = at - stirringFrom;
            double mean = length > 0 ? stirringSum / length : lastSpeed;
            string how = stirChanges > Math.Max(2, length / 4) ? ", unevenly" : "";
            lines.Add($"{AlchemyText.Clock(stirringFrom)}  Stirred at about {mean * 100:0}% for {AlchemyText.Clock(length)}{how}");
            stirringFrom = -1;
        }
        for (int i = 0; i < ops.Count; i++)
        {
            AlchemyOperation op = ops[i];
            double t = op.step * step;
            if (op.kind != AlchemyOpKind.Stir && stirs > 0)
            {
                lines.Add($"{AlchemyText.Clock(stirAt)}  Stirred{(stirs > 1 ? $" × {stirs}" : "")}");
                stirs = 0;
            }
            if (op.kind != AlchemyOpKind.Bellows && pumps > 0 && t - pumpAt > 8.0)
            {
                lines.Add($"{AlchemyText.Clock(pumpAt)}  Worked the bellows{(pumps > 1 ? $" × {pumps}" : "")}");
                pumps = 0;
            }
            switch (op.kind)
            {
                case AlchemyOpKind.AddIngredient:
                    lines.Add($"{AlchemyText.Clock(t)}  Added {(op.count > 1 ? op.count + " × " : "")}{op.label}" +
                              (string.IsNullOrEmpty(op.form) ? "" : $" ({brew.Rules.Form(op.form).Name.ToLowerInvariant()}" +
                                                                    $"{(op.evenness < 0.45f ? ", very unevenly" : op.evenness < 0.7f ? ", unevenly" : "")})"));
                    break;
                case AlchemyOpKind.AddSubstance:
                    lines.Add(string.IsNullOrEmpty(op.form) ? $"{AlchemyText.Clock(t)}  Poured {AlchemyText.Units(op.value)} of {op.label}"
                                                            : $"{AlchemyText.Clock(t)}  ({AlchemyText.Units(op.value)} of {op.label} {op.form})");
                    break;
                case AlchemyOpKind.AddFuel:
                    lines.Add($"{AlchemyText.Clock(t)}  Put {(op.count > 1 ? op.count + " × " : "")}{op.label} on the fire");
                    break;
                case AlchemyOpKind.Bellows:
                    if (pumps > 0 && t - pumpAt > 8.0)
                    {
                        lines.Add($"{AlchemyText.Clock(pumpAt)}  Worked the bellows{(pumps > 1 ? $" × {pumps}" : "")}");
                        pumps = 0;
                    }
                    if (pumps == 0) pumpAt = t;
                    pumps++;
                    break;
                case AlchemyOpKind.SetDamper:
                {
                    string vent = $"{AlchemyText.Clock(t)}  Air vent {(op.value <= 0.01f ? "closed (fire choked)" : op.value >= 0.99f ? "open" : $"{op.value * 100:0}% open")}";
                    if (ventLine >= 0 && ventLine == lines.Count - 1 && t - ventAt < 3.0)
                        lines[ventLine] = vent;
                    else
                    {
                        lines.Add(vent);
                        ventLine = lines.Count - 1;
                    }
                    ventAt = t;
                    break;
                }
                case AlchemyOpKind.SetStirring:
                    if (op.value > 0f && stirringFrom < 0)
                    {
                        stirringFrom = lastSpeedAt = t;
                        stirringSum = 0;
                        stirChanges = 0;
                    }
                    else if (stirringFrom >= 0)
                    {
                        stirringSum += lastSpeed * (t - lastSpeedAt);
                        lastSpeedAt = t;
                        stirChanges++;
                    }
                    lastSpeed = op.value;
                    if (op.value <= 0f)
                        EndStirring(t);
                    break;
                case AlchemyOpKind.SetBath:
                    lines.Add($"{AlchemyText.Clock(t)}  {((AlchemyBath)(int)op.value) switch { AlchemyBath.Water => "Stood it in a water bath", AlchemyBath.Ice => "Stood it in an ice bath", _ => "Took it out of the bath" }}");
                    break;
                case AlchemyOpKind.Filter:
                    lines.Add($"{AlchemyText.Clock(t)}  Filtered through the {op.label.ToLowerInvariant()}");
                    break;
                case AlchemyOpKind.Decant:
                    lines.Add($"{AlchemyText.Clock(t)}  Poured off the top layer");
                    break;
                case AlchemyOpKind.Skim:
                    lines.Add($"{AlchemyText.Clock(t)}  Skimmed the surface");
                    break;
                case AlchemyOpKind.SetHeat:
                    string text = $"{AlchemyText.Clock(t)}  {HeatText(op.value)}{(string.IsNullOrEmpty(op.label) ? "" : $" (→ {op.label})")}";
                    if (heatLine >= 0 && heatLine == lines.Count - 1 && t - heatAt < 3.0)
                        lines[heatLine] = text;
                    else
                    {
                        lines.Add(text);
                        heatLine = lines.Count - 1;
                    }
                    heatAt = t;
                    break;
                case AlchemyOpKind.Stir:
                    if (stirs == 0 || t - stirAt > 5.0)
                    {
                        if (stirs > 0)
                            lines.Add($"{AlchemyText.Clock(stirAt)}  Stirred{(stirs > 1 ? $" × {stirs}" : "")}");
                        stirs = 0;
                        stirAt = t;
                    }
                    stirs++;
                    break;
                case AlchemyOpKind.SetVariable:
                    if (string.Equals(op.id, AlchemyVariables.Night, StringComparison.OrdinalIgnoreCase))
                        lines.Add($"{AlchemyText.Clock(t)}  ({(op.value >= 0.5f ? "night fell" : "day broke")})");
                    break;
                case AlchemyOpKind.Suspend:
                    lines.Add($"{AlchemyText.Clock(t)}  Paused");
                    break;
                case AlchemyOpKind.Resume:
                    lines.Add($"{AlchemyText.Clock(t)}  Resumed");
                    break;
                case AlchemyOpKind.Bottle:
                    lines.Add($"{AlchemyText.Clock(t)}  Bottled {op.count}{(string.IsNullOrEmpty(op.label) ? "" : " × " + op.label)}");
                    break;
                case AlchemyOpKind.CollectDistillate:
                    lines.Add($"{AlchemyText.Clock(t)}  Collected the distillate{(string.IsNullOrEmpty(op.label) ? "" : " (" + op.label + ")")}");
                    break;
            }
        }
        if (stirs > 0)
            lines.Add($"{AlchemyText.Clock(stirAt)}  Stirred{(stirs > 1 ? $" × {stirs}" : "")}");
        if (pumps > 0)
            lines.Add($"{AlchemyText.Clock(pumpAt)}  Worked the bellows{(pumps > 1 ? $" × {pumps}" : "")}");
        EndStirring(brew.Elapsed);
        return lines;
    }

    /// <summary>"Fire 35%", "Fire off", "Cooling 50%".</summary>
    public static string HeatText(double level)
    {
        if (Math.Abs(level) < 0.005) return "Fire off";
        return level > 0 ? $"Fire {level * 100:0}%" : $"Cooling {-level * 100:0}%";
    }

    // ------------------------------------------------------------------ what the player may see
    /// <summary>
    /// What the player has learnt about how an identified substance behaves: how well it dissolves (once they have seen it
    /// stay undissolved) and where it breaks down (once they understand the breakdown). Empty lines are left out.
    /// </summary>
    public static List<string> SubstanceBehaviour(AlchemyJournalData d, AlchemyRules rules, AlchemySubstance s)
    {
        var lines = new List<string>();
        if (s == null)
            return lines;
        if (s.HasSolubilityLimit && d != null && d.undissolvedSeen.Contains(s.id))
        {
            var parts = new List<string>();
            foreach (int k in rules.Solvents)
            {
                float v = s.SolubilityIn(rules.Substances[k].id);
                string how = v >= 0.5f ? "well" : v >= 0.1f ? "somewhat" : v >= 0.02f ? "poorly" : "hardly";
                parts.Add($"{how} in {rules.Substances[k].Name}");
            }
            lines.Add("Dissolves " + string.Join(", ", parts) + (s.solubilityDoublingDegrees > 0f && s.solubilityDoublingDegrees < 200f ? " — more when hot" : ""));
        }
        if (s.BreaksDownWhenHot && IsUnderstood(d, rules, "breakdown_hot." + s.id))
            lines.Add($"Breaks down above {s.stableMax:0} °C");
        if (s.BreaksDownWhenCold && IsUnderstood(d, rules, "breakdown_cold." + s.id))
            lines.Add($"Breaks down below {s.stableMin:0} °C");
        if (s.BreaksDownInAcid && IsUnderstood(d, rules, "breakdown_acid." + s.id))
            lines.Add($"Acid destroys it (below pH {s.stablePhMin:0.#})");
        if (s.BreaksDownInAlkali && IsUnderstood(d, rules, "breakdown_alkali." + s.id))
            lines.Add($"Alkali destroys it (above pH {s.stablePhMax:0.#})");
        if (s.Crystallises && IsIdentified(d, rules, s.crystal))
            lines.Add($"Crystallises as {rules.SubstanceName(s.crystal)} when a saturated liquid is left still and cooled slowly");
        if (s.interactions != null)
            for (int k = 0; k < s.interactions.Count; k++)
                if (s.interactions[k]?.kind == InteractionKind.Clashes && IsUnderstood(d, rules, $"clash.{s.id}.{k}"))
                    lines.Add($"Cannot coexist with {s.interactions[k].with.Describe(rules)}");
        return lines;
    }

    /// <summary>What the journal says of a reaction: the full description once understood, else what was seen.</summary>
    public static string DescribeReaction(AlchemyJournalData d, AlchemyRules rules, CompiledReaction r)
    {
        AlchemyReaction def = r.Definition;
        if (IsUnderstood(d, rules, def.id))
            return $"<b>{def.Name}</b>: {def.Describe(rules)}";
        AlchemyReactionNote n = ReactionNote(d, def.id);
        string seen = string.IsNullOrWhiteSpace(def.observation) ? "something reacted" : def.observation;
        if (n == null)
            return "Something is happening: " + seen;
        string where = n.highestTemperature > -9000f ? $", seen between {n.lowestTemperature:0} and {n.highestTemperature:0} °C" : "";
        return $"Unknown reaction: {seen} (seen in {n.timesSeen} of {rules.Settings.observationsToUnderstand} preparations{where})";
    }
}
