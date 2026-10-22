using System;
using System.Collections.Generic;

/// <summary>A reaction with its selectors resolved to substance indices (built once per rules).</summary>
public sealed class CompiledReaction
{
    public readonly int Index;
    public readonly AlchemyReaction Definition;
    /// <summary>Per reactant: the substances it can consume.</summary>
    public readonly int[][] ReactantMatches;
    public readonly double[] ReactantRatios;
    public readonly double[] ReactantOrders;
    public readonly int[] Products;
    public readonly double[] ProductRatios;
    /// <summary>Per modifier: the substances that count for it.</summary>
    public readonly int[][] ModifierMatches;
    /// <summary>Does it consume a contaminant, or make residue (decay, burning)? Then contaminants do not slow it.</summary>
    public readonly bool ConsumesContaminant;

    internal CompiledReaction(int index, AlchemyReaction def, AlchemyRules rules)
    {
        Index = index;
        Definition = def;
        int n = def.reactants?.Count ?? 0;
        ReactantMatches = new int[n][];
        ReactantRatios = new double[n];
        ReactantOrders = new double[n];
        for (int i = 0; i < n; i++)
        {
            ReactionReactant r = def.reactants[i];
            ReactantMatches[i] = r?.substance != null ? rules.Match(r.substance) : Array.Empty<int>();
            ReactantRatios[i] = r != null ? Math.Max(0.0001, r.ratio) : 1;
            ReactantOrders[i] = r != null ? Math.Max(0, r.order) : 1;
        }
        var products = new List<int>();
        var ratios = new List<double>();
        if (def.products != null)
            foreach (ReactionProduct p in def.products)
            {
                int k = p != null ? rules.IndexOf(p.substance) : -1;
                if (k < 0) continue;
                products.Add(k);
                ratios.Add(Math.Max(0, p.ratio));
            }
        Products = products.ToArray();
        ProductRatios = ratios.ToArray();
        int m = def.modifiers?.Count ?? 0;
        ModifierMatches = new int[m][];
        for (int i = 0; i < m; i++)
            ModifierMatches[i] = def.modifiers[i]?.substance != null ? rules.Match(def.modifiers[i].substance) : Array.Empty<int>();
        int dirt = rules.PropertyIndex(rules.Settings.contaminantProperty);
        if (dirt >= 0)
            foreach (int[] match in ReactantMatches)
                foreach (int s in match)
                    ConsumesContaminant |= rules.PropertyMatrix[s, dirt] > 0;
        foreach (int p in Products)
            ConsumesContaminant |= rules.Substances[p].role == SubstanceRole.Residue; // decay is not slowed by its own dirt
    }

    /// <summary>Can it ever run (every reactant picks at least one substance)?</summary>
    public bool Valid
    {
        get
        {
            if (ReactantMatches.Length == 0) return false;
            foreach (int[] m in ReactantMatches)
                if (m.Length == 0) return false;
            return true;
        }
    }
}

/// <summary>
/// The alchemy rules ready to simulate: a database and its extensions merged (later entries with the same id replace
/// earlier ones), substances indexed, selectors resolved and outcomes sorted by priority. Immutable once built;
/// shared by every preparation that uses the database. Build one from code with <see cref="Compile(AlchemyDatabase)"/>
/// or <see cref="Compile(AlchemySimulationSettings, IEnumerable{AlchemyPropertyDefinition}, IEnumerable{AlchemySubstance}, IEnumerable{AlchemyReaction}, IEnumerable{AlchemyOutcome}, IEnumerable{AlchemyIngredientForm}, IEnumerable{AlchemyFilter}, IEnumerable{AlchemyFuel})"/>.
/// <para>Some reactions are made by the rules themselves: each substance's thermal and pH stability becomes a breakdown
/// reaction, each clash between incompatible substances a reaction, and each interference a modifier of the reactions
/// the substance takes part in (on copies: the database's reactions are never changed).</para>
/// </summary>
public sealed class AlchemyRules
{
    public readonly AlchemySimulationSettings Settings;
    public readonly AlchemyPropertyDefinition[] Properties;
    public readonly AlchemySubstance[] Substances;
    public readonly CompiledReaction[] Reactions;
    /// <summary>Outcomes, highest priority first (ties keep their list order).</summary>
    public readonly AlchemyOutcome[] Outcomes;
    /// <summary>[substance, property] values.</summary>
    public readonly double[,] PropertyMatrix;
    /// <summary>The forms ingredients come in (whole, ground, extract...).</summary>
    public readonly AlchemyIngredientForm[] Forms;
    /// <summary>Indices of the Solvent substances (liquid).</summary>
    public readonly int[] Solvents;
    /// <summary>[substance, solvent] units of the substance one unit of the solvent holds at 20 °C (log), for substances with a solubility limit.</summary>
    public readonly double[,] LogSolubility;
    /// <summary>Does the substance have a solubility limit?</summary>
    public readonly bool[] LimitedSolubility;
    /// <summary>The ways of filtering and what a manual fire burns.</summary>
    public readonly AlchemyFilter[] Filters;
    public readonly AlchemyFuel[] Fuels;
    /// <summary>Per substance: its density, the layer it forms or prefers, the index of the solid it crystallises as (-1 =
    /// none) and, for crystals, the index of the substance they dissolve back into (-1).</summary>
    public readonly double[] Density;
    public readonly LiquidLayer[] Layer;
    public readonly int[] CrystalOf;
    public readonly int[] DissolvesAs;
    /// <summary>Per substance: what makes it curdle out of solution, and how strongly.</summary>
    public readonly int[][][] CurdledBy;
    public readonly double[][] CurdleStrength;
    public readonly List<string> CompileErrors = new List<string>();
    public readonly List<string> CompileWarnings = new List<string>();

    private readonly Dictionary<string, int> substanceIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> propertyIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> reactionIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AlchemyOutcome> outcomeById = new Dictionary<string, AlchemyOutcome>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AlchemyIngredientForm> formById = new Dictionary<string, AlchemyIngredientForm>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AlchemyFilter> filterById = new Dictionary<string, AlchemyFilter>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AlchemyFuel> fuelById = new Dictionary<string, AlchemyFuel>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int[]> matchCache = new Dictionary<string, int[]>(StringComparer.Ordinal);

    /// <summary>The source database (null when built from lists).</summary>
    public AlchemyDatabase Database { get; private set; }

    public IEnumerable<AlchemyReaction> ReactionDefinitions
    {
        get
        {
            foreach (CompiledReaction r in Reactions)
                yield return r.Definition;
        }
    }

    public int SubstanceCount => Substances.Length;

    private AlchemyRules(AlchemySimulationSettings settings, List<AlchemyPropertyDefinition> props, List<AlchemySubstance> subs,
        List<AlchemyReaction> reactions, List<AlchemyOutcome> outcomes, List<AlchemyIngredientForm> forms,
        List<AlchemyFilter> filters, List<AlchemyFuel> fuels)
    {
        if (forms == null || forms.Count == 0)
            forms = AlchemyIngredientForm.Defaults();
        Forms = forms.ToArray();
        foreach (AlchemyIngredientForm f in Forms)
            formById[f.id] = f;
        Filters = (filters != null && filters.Count > 0 ? filters : AlchemyDefaults.Filters()).ToArray();
        foreach (AlchemyFilter f in Filters)
            filterById[f.id] = f;
        Fuels = (fuels != null && fuels.Count > 0 ? fuels : AlchemyDefaults.Fuels()).ToArray();
        foreach (AlchemyFuel f in Fuels)
            fuelById[f.id] = f;
        Settings = settings ?? new AlchemySimulationSettings();
        Properties = props.ToArray();
        for (int i = 0; i < Properties.Length; i++)
            propertyIndex[Properties[i].id] = i;
        Substances = subs.ToArray();
        for (int i = 0; i < Substances.Length; i++)
            substanceIndex[Substances[i].id] = i;
        PropertyMatrix = new double[Substances.Length, Properties.Length];
        for (int s = 0; s < Substances.Length; s++)
        {
            List<PropertyValue> values = Substances[s].properties;
            if (values == null) continue;
            foreach (PropertyValue v in values)
            {
                if (v == null) continue;
                if (propertyIndex.TryGetValue(v.property ?? "", out int p))
                    PropertyMatrix[s, p] += v.value;
                else
                    CompileWarnings.Add($"Substance '{Substances[s].Name}': unknown property '{v.property}'.");
            }
        }
        // Solubility: the solvents, and each limited substance's solubility in each of them (as a logarithm, for blends).
        var solvents = new List<int>();
        for (int i = 0; i < Substances.Length; i++)
            if (Substances[i].role == SubstanceRole.Solvent && Substances[i].phase == SubstancePhase.Liquid)
                solvents.Add(i);
        Solvents = solvents.ToArray();
        LimitedSolubility = new bool[Substances.Length];
        LogSolubility = new double[Substances.Length, Solvents.Length];
        for (int i = 0; i < Substances.Length; i++)
        {
            AlchemySubstance sub = Substances[i];
            if (!sub.HasSolubilityLimit) continue;
            LimitedSolubility[i] = true;
            foreach (SolventSolubility ss in sub.solubility)
                if (ss != null && (substanceIndex.TryGetValue(ss.solvent ?? "", out int k) ? Substances[k].role != SubstanceRole.Solvent : true))
                    CompileWarnings.Add($"Substance '{sub.Name}': '{ss.solvent}' is not a solvent.");
            for (int k = 0; k < Solvents.Length; k++)
                LogSolubility[i, k] = Math.Log(Math.Max(1e-4, sub.SolubilityIn(Substances[Solvents[k]].id)));
        }

        // Density, layers and crystals.
        int count = Substances.Length;
        Density = new double[count];
        Layer = new LiquidLayer[count];
        CrystalOf = new int[count];
        DissolvesAs = new int[count];
        CurdledBy = new int[count][][];
        CurdleStrength = new double[count][];
        for (int i = 0; i < count; i++)
        {
            Density[i] = Math.Max(0.05, Substances[i].density);
            Layer[i] = Substances[i].layer;
            CrystalOf[i] = DissolvesAs[i] = -1;
        }
        for (int i = 0; i < count; i++)
        {
            AlchemySubstance sub = Substances[i];
            if (!sub.Crystallises) continue;
            int c = IndexOf(sub.crystal);
            if (c < 0 || Substances[c].phase != SubstancePhase.Solid)
                CompileWarnings.Add($"Substance '{sub.Name}': its crystal '{sub.crystal}' must be a Solid substance.");
            else if (!sub.HasSolubilityLimit)
                CompileWarnings.Add($"Substance '{sub.Name}' crystallises but has no solubility limit: it never comes out of solution.");
            else
            {
                CrystalOf[i] = c;
                DissolvesAs[c] = i;
            }
        }

        // Breakdown outside the stable temperature and pH ranges, and compatibility, become rules of their own.
        reactions = WithInterferences(reactions);
        for (int i = 0; i < count; i++)
        {
            AlchemySubstance sub = Substances[i];
            if (sub.BreaksDownWhenHot)
                reactions.Add(Breakdown(sub, true));
            if (sub.BreaksDownWhenCold)
                reactions.Add(Breakdown(sub, false));
            if (sub.BreaksDownInAcid)
                reactions.Add(PhBreakdown(sub, true));
            if (sub.BreaksDownInAlkali)
                reactions.Add(PhBreakdown(sub, false));
            var curdles = new List<int[]>();
            var strengths = new List<double>();
            if (sub.interactions != null)
                for (int k = 0; k < sub.interactions.Count; k++)
                {
                    SubstanceInteraction x = sub.interactions[k];
                    if (x?.with == null) continue;
                    if (x.kind == InteractionKind.Clashes)
                        reactions.Add(Clash(sub, x, k));
                    else if (x.kind == InteractionKind.Curdles)
                    {
                        if (!sub.HasSolubilityLimit)
                            CompileWarnings.Add($"Substance '{sub.Name}' curdles but has no solubility limit: give it a Solubility.");
                        curdles.Add(Match(x.with));
                        strengths.Add(Math.Max(0, x.strength));
                    }
                }
            CurdledBy[i] = curdles.ToArray();
            CurdleStrength[i] = strengths.ToArray();
        }

        var compiled = new List<CompiledReaction>();
        foreach (AlchemyReaction r in reactions)
        {
            var c = new CompiledReaction(compiled.Count, r, this);
            if (!c.Valid)
                CompileWarnings.Add($"Reaction '{r.Name}' can never happen: a reactant picks no substance.");
            reactionIndex[r.id] = compiled.Count;
            compiled.Add(c);
        }
        Reactions = compiled.ToArray();
        var sorted = new List<(AlchemyOutcome o, int order)>();
        for (int i = 0; i < outcomes.Count; i++)
            sorted.Add((outcomes[i], i));
        sorted.Sort((a, b) => a.o.priority != b.o.priority ? b.o.priority.CompareTo(a.o.priority) : a.order.CompareTo(b.order));
        Outcomes = new AlchemyOutcome[sorted.Count];
        for (int i = 0; i < sorted.Count; i++)
        {
            Outcomes[i] = sorted[i].o;
            outcomeById[sorted[i].o.id] = sorted[i].o;
        }
    }

    /// <summary>The reaction a substance's thermal stability stands for ("Vital Essence breaks down above 115 °C").</summary>
    private static AlchemyReaction Breakdown(AlchemySubstance sub, bool hot)
    {
        float limit = hot ? sub.stableMax : sub.stableMin;
        var r = new AlchemyReaction
        {
            id = (hot ? "breakdown_hot." : "breakdown_cold.") + sub.id,
            displayName = $"{sub.Name} breaks down",
            description = hot ? $"Above {limit:0} °C {sub.Name} is not stable: it breaks down, faster the hotter it gets."
                              : $"Below {limit:0} °C {sub.Name} is not stable: it breaks down, faster the colder it gets.",
            observation = string.IsNullOrWhiteSpace(sub.degradeObservation) ? (hot ? "something spoils in the heat" : "something spoils in the cold") : sub.degradeObservation,
            rate = sub.degradeRate,
            minTemperature = hot ? limit : -273f,
            maxTemperature = hot ? 2000f : limit,
            referenceTemperature = hot ? limit + 10f : limit - 10f,
            doublingDegrees = hot ? sub.degradeDoublingDegrees : -sub.degradeDoublingDegrees,
            visual = hot ? ReactionVisual.Smoke : ReactionVisual.None,
            generatedFrom = sub.id,
        };
        r.reactants.Add(new ReactionReactant(SubstanceSelector.Of(sub.id)));
        if (sub.degradesInto != null)
            foreach (ReactionProduct p in sub.degradesInto)
                if (p != null && !string.IsNullOrWhiteSpace(p.substance))
                    r.products.Add(new ReactionProduct(p.substance, p.ratio));
        return r;
    }

    /// <summary>The reaction a substance's pH stability stands for ("Acid destroys Vital Essence").</summary>
    private static AlchemyReaction PhBreakdown(AlchemySubstance sub, bool acid)
    {
        string seen = !string.IsNullOrWhiteSpace(sub.phDegradeObservation) ? sub.phDegradeObservation : sub.degradeObservation;
        var r = new AlchemyReaction
        {
            id = (acid ? "breakdown_acid." : "breakdown_alkali.") + sub.id,
            displayName = acid ? $"Acid destroys {sub.Name}" : $"Alkali destroys {sub.Name}",
            description = acid ? $"Below pH {sub.stablePhMin:0.#} {sub.Name} is not stable: the acid breaks it down (faster when hot)."
                               : $"Above pH {sub.stablePhMax:0.#} {sub.Name} is not stable: the alkali breaks it down (faster when hot).",
            observation = string.IsNullOrWhiteSpace(seen) ? (acid ? "something sours and spoils" : "something turns soapy and spoils") : seen,
            rate = sub.phDegradeRate,
            minTemperature = -100f,
            maxTemperature = 2000f,
            referenceTemperature = 20f,
            doublingDegrees = 20f,
            visual = ReactionVisual.Bubbles,
            generatedFrom = sub.id,
        };
        r.reactants.Add(new ReactionReactant(SubstanceSelector.Of(sub.id)));
        r.conditions.Add(acid ? new VariableCondition(AlchemyVariables.Ph, -1f, sub.stablePhMin) : new VariableCondition(AlchemyVariables.Ph, sub.stablePhMax, 15f));
        if (sub.degradesInto != null)
            foreach (ReactionProduct p in sub.degradesInto)
                if (p != null && !string.IsNullOrWhiteSpace(p.substance))
                    r.products.Add(new ReactionProduct(p.substance, p.ratio));
        return r;
    }

    /// <summary>Two incompatible substances destroying each other, as a reaction.</summary>
    private AlchemyReaction Clash(AlchemySubstance sub, SubstanceInteraction x, int k)
    {
        string other = x.with.Describe(this);
        var r = new AlchemyReaction
        {
            id = $"clash.{sub.id}.{k}",
            displayName = $"{sub.Name} clashes with {other}",
            description = $"{sub.Name} and {other} cannot coexist: they destroy each other as soon as they meet.",
            observation = string.IsNullOrWhiteSpace(x.observation) ? "it hisses as two things cancel each other out" : x.observation,
            rate = Math.Max(0.0001f, x.strength),
            minTemperature = -100f,
            maxTemperature = 2000f,
            referenceTemperature = 20f,
            doublingDegrees = 0f,
            visual = ReactionVisual.Hiss,
            generatedFrom = sub.id,
        };
        r.reactants.Add(new ReactionReactant(SubstanceSelector.Of(sub.id)));
        r.reactants.Add(new ReactionReactant(x.with));
        foreach (ReactionProduct p in x.products)
            if (p != null && !string.IsNullOrWhiteSpace(p.substance))
                r.products.Add(new ReactionProduct(p.substance, p.ratio));
        return r;
    }

    /// <summary>
    /// The reactions, with every substance's interferences added as modifiers of the reactions that consume it: "Tannin
    /// slows every reaction that consumes Vital Essence". Changed reactions are copies; the others are kept as they are.
    /// </summary>
    private List<AlchemyReaction> WithInterferences(List<AlchemyReaction> reactions)
    {
        var result = new List<AlchemyReaction>(reactions.Count + 16);
        foreach (AlchemyReaction r in reactions)
        {
            AlchemyReaction copy = null;
            foreach (AlchemySubstance sub in Substances)
            {
                if (sub.interactions == null) continue;
                bool consumes = false;
                foreach (ReactionReactant re in r.reactants)
                    consumes |= re?.substance != null && re.substance.Matches(sub);
                if (!consumes) continue;
                foreach (SubstanceInteraction x in sub.interactions)
                {
                    if (x?.with == null || x.kind != InteractionKind.Interferes) continue;
                    copy ??= r.CopyForRules();
                    copy.modifiers.Add(new ReactionModifier(x.with, 1f / (1f + Math.Max(0f, x.strength)), 0.02f));
                }
            }
            result.Add(copy ?? r);
        }
        return result;
    }

    // ------------------------------------------------------------------ building
    /// <summary>The rules of <paramref name="db"/> and its extensions.</summary>
    public static AlchemyRules Compile(AlchemyDatabase db)
    {
        var props = new List<AlchemyPropertyDefinition>();
        var subs = new List<AlchemySubstance>();
        var reactions = new List<AlchemyReaction>();
        var outcomes = new List<AlchemyOutcome>();
        var forms = new List<AlchemyIngredientForm>();
        var filters = new List<AlchemyFilter>();
        var fuels = new List<AlchemyFuel>();
        var errors = new List<string>();
        Gather(db, props, subs, reactions, outcomes, forms, filters, fuels, errors, new HashSet<AlchemyDatabase>());
        AlchemyRules rules = Compile(db != null ? db.settings : null, props, subs, reactions, outcomes, forms, filters, fuels);
        rules.Database = db;
        rules.CompileErrors.InsertRange(0, errors);
        return rules;
    }

    private static void Gather(AlchemyDatabase db, List<AlchemyPropertyDefinition> props, List<AlchemySubstance> subs,
        List<AlchemyReaction> reactions, List<AlchemyOutcome> outcomes, List<AlchemyIngredientForm> forms, List<AlchemyFilter> filters,
        List<AlchemyFuel> fuels, List<string> errors, HashSet<AlchemyDatabase> seen)
    {
        if (db == null)
            return;
        if (!seen.Add(db))
        {
            errors.Add($"Alchemy database '{db.name}' includes itself through its extensions.");
            return;
        }
        Merge(props, db.properties, p => p.id);
        Merge(subs, db.substances, s => s.id);
        Merge(reactions, db.reactions, r => r.id);
        Merge(outcomes, db.outcomes, o => o.id);
        Merge(forms, db.forms, f => f.id);
        Merge(filters, db.filters, f => f.id);
        Merge(fuels, db.fuels, f => f.id);
        if (db.extensions != null)
            foreach (AlchemyDatabase ext in db.extensions)
                Gather(ext, props, subs, reactions, outcomes, forms, filters, fuels, errors, seen);
    }

    /// <summary>Adds <paramref name="from"/> to <paramref name="into"/>: same ids replace (in place), new ones are appended.</summary>
    private static void Merge<T>(List<T> into, List<T> from, Func<T, string> idOf) where T : class
    {
        if (from == null)
            return;
        foreach (T item in from)
        {
            if (item == null || string.IsNullOrWhiteSpace(idOf(item)))
                continue;
            int at = into.FindIndex(x => string.Equals(idOf(x), idOf(item), StringComparison.OrdinalIgnoreCase));
            if (at >= 0) into[at] = item;
            else into.Add(item);
        }
    }

    /// <summary>Rules from lists (tests, tools). Entries without an id are skipped; duplicate ids keep the last one.</summary>
    public static AlchemyRules Compile(AlchemySimulationSettings settings, IEnumerable<AlchemyPropertyDefinition> properties,
        IEnumerable<AlchemySubstance> substances, IEnumerable<AlchemyReaction> reactions, IEnumerable<AlchemyOutcome> outcomes,
        IEnumerable<AlchemyIngredientForm> forms = null, IEnumerable<AlchemyFilter> filters = null, IEnumerable<AlchemyFuel> fuels = null)
    {
        var props = new List<AlchemyPropertyDefinition>();
        var subs = new List<AlchemySubstance>();
        var reacts = new List<AlchemyReaction>();
        var outs = new List<AlchemyOutcome>();
        Merge(props, properties != null ? new List<AlchemyPropertyDefinition>(properties) : null, p => p.id);
        Merge(subs, substances != null ? new List<AlchemySubstance>(substances) : null, s => s.id);
        var enabled = new List<AlchemyReaction>();
        if (reactions != null)
            foreach (AlchemyReaction r in reactions)
                if (r != null && r.enabled) enabled.Add(r);
        Merge(reacts, enabled, r => r.id);
        Merge(outs, outcomes != null ? new List<AlchemyOutcome>(outcomes) : null, o => o.id);
        var fs = new List<AlchemyIngredientForm>();
        Merge(fs, forms != null ? new List<AlchemyIngredientForm>(forms) : null, f => f.id);
        var fl = new List<AlchemyFilter>();
        Merge(fl, filters != null ? new List<AlchemyFilter>(filters) : null, f => f.id);
        var fu = new List<AlchemyFuel>();
        Merge(fu, fuels != null ? new List<AlchemyFuel>(fuels) : null, f => f.id);
        return new AlchemyRules(settings, props, subs, reacts, outs, fs, fl, fu);
    }

    // ------------------------------------------------------------------ lookups
    public int IndexOf(string substanceId) =>
        !string.IsNullOrEmpty(substanceId) && substanceIndex.TryGetValue(substanceId, out int i) ? i : -1;

    public AlchemySubstance Substance(string substanceId)
    {
        int i = IndexOf(substanceId);
        return i >= 0 ? Substances[i] : null;
    }

    public int PropertyIndex(string propertyId) =>
        !string.IsNullOrEmpty(propertyId) && propertyIndex.TryGetValue(propertyId, out int i) ? i : -1;

    public int ReactionIndex(string reactionId) =>
        !string.IsNullOrEmpty(reactionId) && reactionIndex.TryGetValue(reactionId, out int i) ? i : -1;

    public CompiledReaction Reaction(string reactionId)
    {
        int i = ReactionIndex(reactionId);
        return i >= 0 ? Reactions[i] : null;
    }

    public AlchemyOutcome Outcome(string outcomeId) =>
        !string.IsNullOrEmpty(outcomeId) && outcomeById.TryGetValue(outcomeId, out AlchemyOutcome o) ? o : null;

    public string SubstanceName(string substanceId) => Substance(substanceId)?.Name ?? substanceId;

    public string PropertyName(string propertyId)
    {
        int i = PropertyIndex(propertyId);
        return i >= 0 ? Properties[i].Name : propertyId;
    }

    /// <summary>The substances <paramref name="selector"/> picks (indices, cached).</summary>
    public int[] Match(SubstanceSelector selector)
    {
        if (selector == null)
            return Array.Empty<int>();
        string key = selector.Key;
        if (matchCache.TryGetValue(key, out int[] cached))
            return cached;
        var list = new List<int>();
        if (selector.mode == SubstanceSelectorMode.Substance)
        {
            int i = IndexOf(selector.id);
            if (i >= 0 && selector.Matches(Substances[i])) list.Add(i);
        }
        else
        {
            for (int i = 0; i < Substances.Length; i++)
                if (selector.Matches(Substances[i]))
                    list.Add(i);
        }
        int[] result = list.ToArray();
        lock (matchCache)
            matchCache[key] = result;
        return result;
    }

    /// <summary>The form with <paramref name="formId"/>, else the first one (whole).</summary>
    public AlchemyIngredientForm Form(string formId)
    {
        if (!string.IsNullOrEmpty(formId) && formById.TryGetValue(formId, out AlchemyIngredientForm f))
            return f;
        return Forms.Length > 0 ? Forms[0] : new AlchemyIngredientForm();
    }

    public bool HasForm(string formId) => !string.IsNullOrEmpty(formId) && formById.ContainsKey(formId);

    /// <summary>The form <paramref name="profile"/> becomes when ground once (null = it cannot be ground).</summary>
    public AlchemyIngredientForm GroundForm(AlchemyIngredientProfile profile)
    {
        List<AlchemyIngredientForm> levels = GrindLevels(profile);
        return levels.Count > 0 ? levels[0] : null;
    }

    /// <summary>
    /// The forms grinding can bring <paramref name="profile"/> to, coarsest first (whole → partially ground → finely ground
    /// → pulverised with the built-in forms). Empty when it cannot be ground.
    /// </summary>
    public List<AlchemyIngredientForm> GrindLevels(AlchemyIngredientProfile profile)
    {
        var list = new List<AlchemyIngredientForm>();
        if (profile == null || !profile.canBeGround)
            return list;
        AlchemyIngredientForm f = Form(profile.form);
        while (!string.IsNullOrEmpty(f.groundInto) && HasForm(f.groundInto) && list.Count < 8)
        {
            f = Form(f.groundInto);
            if (list.Contains(f)) break;
            list.Add(f);
        }
        return list;
    }

    /// <summary>The next coarser form (the one that grinds into <paramref name="formId"/>; null = none).</summary>
    public AlchemyIngredientForm CoarserForm(string formId)
    {
        foreach (AlchemyIngredientForm f in Forms)
            if (string.Equals(f.groundInto, formId, StringComparison.OrdinalIgnoreCase))
                return f;
        return null;
    }

    /// <summary>The next finer form (what <paramref name="formId"/> grinds into; null = none).</summary>
    public AlchemyIngredientForm FinerForm(string formId)
    {
        AlchemyIngredientForm f = HasForm(formId) ? Form(formId) : null;
        return f != null && HasForm(f.groundInto) ? Form(f.groundInto) : null;
    }

    public AlchemyFilter Filter(string filterId) =>
        !string.IsNullOrEmpty(filterId) && filterById.TryGetValue(filterId, out AlchemyFilter f) ? f : null;

    public AlchemyFuel Fuel(string fuelId) =>
        !string.IsNullOrEmpty(fuelId) && fuelById.TryGetValue(fuelId, out AlchemyFuel f) ? f : null;

    /// <summary>Units per bottle of <paramref name="outcome"/>.</summary>
    public double UnitsPerBottle(AlchemyOutcome outcome) =>
        outcome != null && outcome.unitsPerBottle > 0f ? outcome.unitsPerBottle : Math.Max(0.1, Settings.unitsPerBottle);
}
