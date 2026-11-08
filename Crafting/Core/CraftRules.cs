using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everything the crafting rules are made of, as plain lists: elements and what they do in alloys, the names of
/// metals by composition, ores, fuels, quenchants, vat agents, hides, woods, supplies, abrasive tools, moulds, leather
/// patterns and item designs. The Crafting Database holds one; content packs are more of them merged on top
/// (<see cref="Merge"/>: an entry with the same id replaces the earlier one).
/// </summary>
[Serializable]
public class CraftContent
{
    public List<CraftElement> elements = new List<CraftElement>();
    public List<CraftAlloyEffect> effects = new List<CraftAlloyEffect>();
    public List<CraftMaterialClass> classes = new List<CraftMaterialClass>();
    public List<CraftOreDefinition> ores = new List<CraftOreDefinition>();
    public List<CraftFuel> fuels = new List<CraftFuel>();
    public List<CraftQuenchant> quenchants = new List<CraftQuenchant>();
    public List<CraftTanningAgent> agents = new List<CraftTanningAgent>();
    public List<CraftHideDefinition> hides = new List<CraftHideDefinition>();
    public List<CraftWoodDefinition> woods = new List<CraftWoodDefinition>();
    public List<CraftSupplyDefinition> supplies = new List<CraftSupplyDefinition>();
    public List<CraftAbrasive> abrasives = new List<CraftAbrasive>();
    public List<CraftMold> molds = new List<CraftMold>();
    public List<CraftPattern> patterns = new List<CraftPattern>();
    public List<CraftDesign> designs = new List<CraftDesign>();
    public List<CraftGemDefinition> gems = new List<CraftGemDefinition>();
    public List<CraftGemCut> cuts = new List<CraftGemCut>();

    /// <summary>Adds <paramref name="other"/>'s entries; one with an id already here replaces it (effects are added).</summary>
    public void Merge(CraftContent other)
    {
        if (other == null)
            return;
        MergeById(elements, other.elements, e => e.id);
        if (other.effects != null) effects.AddRange(other.effects);
        MergeById(classes, other.classes, e => e.id);
        MergeById(ores, other.ores, e => e.id);
        MergeById(fuels, other.fuels, e => e.id);
        MergeById(quenchants, other.quenchants, e => e.id);
        MergeById(agents, other.agents, e => e.id);
        MergeById(hides, other.hides, e => e.id);
        MergeById(woods, other.woods, e => e.id);
        MergeById(supplies, other.supplies, e => e.id);
        MergeById(abrasives, other.abrasives, e => e.id);
        MergeById(molds, other.molds, e => e.id);
        MergeById(patterns, other.patterns, e => e.id);
        MergeById(designs, other.designs, e => e.id);
        MergeById(gems ??= new List<CraftGemDefinition>(), other.gems, e => e.id);
        MergeById(cuts ??= new List<CraftGemCut>(), other.cuts, e => e.id);
    }

    /// <summary>
    /// Adds what <paramref name="other"/> has that this content lacks (by id; an alloy effect by its base, element and
    /// property), leaving everything already here as it is: a project's database catches up with new built-in content -
    /// a new metal with what it does in alloys - without losing its own changes. Returns how many were added.
    /// </summary>
    public int AddMissing(CraftContent other)
    {
        if (other == null)
            return 0;
        int n = AddMissingById(elements, other.elements, e => e.id);
        n += AddMissingEffects(effects ??= new List<CraftAlloyEffect>(), other.effects);
        n += AddMissingById(classes, other.classes, e => e.id);
        n += AddMissingById(ores, other.ores, e => e.id);
        n += AddMissingById(fuels, other.fuels, e => e.id);
        n += AddMissingById(quenchants, other.quenchants, e => e.id);
        n += AddMissingById(agents, other.agents, e => e.id);
        n += AddMissingById(hides, other.hides, e => e.id);
        n += AddMissingById(woods, other.woods, e => e.id);
        n += AddMissingById(supplies, other.supplies, e => e.id);
        n += AddMissingById(abrasives, other.abrasives, e => e.id);
        n += AddMissingById(molds, other.molds, e => e.id);
        n += AddMissingById(patterns, other.patterns, e => e.id);
        n += AddMissingById(designs, other.designs, e => e.id);
        n += AddMissingById(gems ??= new List<CraftGemDefinition>(), other.gems, e => e.id);
        n += AddMissingById(cuts ??= new List<CraftGemCut>(), other.cuts, e => e.id);
        return n;
    }

    /// <summary>
    /// Adds the alloy effects of <paramref name="from"/> that <paramref name="into"/> has none of for the same base,
    /// element and property (an effect has no id: those three are what it is about).
    /// </summary>
    public static int AddMissingEffects(List<CraftAlloyEffect> into, List<CraftAlloyEffect> from)
    {
        if (into == null || from == null)
            return 0;
        int n = 0;
        foreach (CraftAlloyEffect x in from)
        {
            if (x == null || into.Exists(e => e != null && SameEffect(e, x))) continue;
            into.Add(x);
            n++;
        }
        return n;
    }

    /// <summary>Are two alloy effects about the same thing (base, element, property)?</summary>
    public static bool SameEffect(CraftAlloyEffect a, CraftAlloyEffect b) =>
        a != null && b != null && a.property == b.property && string.Equals(a.element ?? "", b.element ?? "", StringComparison.OrdinalIgnoreCase)
        && string.Equals((a.baseElement ?? "").Trim(), (b.baseElement ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds the entries of <paramref name="from"/> whose id <paramref name="into"/> does not have yet.</summary>
    public static int AddMissingById<T>(List<T> into, List<T> from, Func<T, string> id) where T : class
    {
        if (into == null || from == null)
            return 0;
        int n = 0;
        foreach (T x in from)
        {
            if (x == null || into.Exists(e => e != null && string.Equals(id(e), id(x), StringComparison.OrdinalIgnoreCase))) continue;
            into.Add(x);
            n++;
        }
        return n;
    }

    private static void MergeById<T>(List<T> into, List<T> from, Func<T, string> id) where T : class
    {
        if (from == null)
            return;
        foreach (T x in from)
        {
            if (x == null) continue;
            int at = into.FindIndex(e => e != null && string.Equals(id(e), id(x), StringComparison.OrdinalIgnoreCase));
            if (at >= 0) into[at] = x;
            else into.Add(x);
        }
    }
}

/// <summary>
/// The crafting rules, compiled for fast lookups: every definition by id, elements by index (compositions are arrays
/// over them), alloy effects by property, material classes by priority. Built by the Crafting Database (or
/// <see cref="CraftDefaults.Rules"/>); tests and tools can <see cref="Compile"/> their own.
/// </summary>
public sealed class CraftRules
{
    public CraftSimulationSettings Settings { get; private set; }
    public CraftElement[] Elements { get; private set; }
    public CraftAlloyEffect[] Effects { get; private set; }
    public CraftMaterialClass[] Classes { get; private set; }
    public CraftOreDefinition[] Ores { get; private set; }
    public CraftFuel[] Fuels { get; private set; }
    public CraftQuenchant[] Quenchants { get; private set; }
    public CraftTanningAgent[] Agents { get; private set; }
    public CraftHideDefinition[] Hides { get; private set; }
    public CraftWoodDefinition[] Woods { get; private set; }
    public CraftSupplyDefinition[] Supplies { get; private set; }
    public CraftGemDefinition[] Gems { get; private set; }
    public CraftGemCut[] Cuts { get; private set; }
    public CraftAbrasive[] Abrasives { get; private set; }
    public CraftMold[] Molds { get; private set; }
    public CraftPattern[] Patterns { get; private set; }
    public CraftDesign[] Designs { get; private set; }
    public readonly List<string> CompileErrors = new List<string>();
    public readonly List<string> CompileWarnings = new List<string>();

    /// <summary>Index of the steel base (iron) and its hardening element (carbon); -1 when the rules have none.</summary>
    public int SteelBase { get; private set; } = -1;
    public int SteelCarbon { get; private set; } = -1;

    private readonly Dictionary<string, int> elementIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ItemSO> templates = new Dictionary<string, ItemSO>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> byId = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Effects by base element index (-1 = any) and property.</summary>
    private readonly Dictionary<long, List<(int element, CraftAlloyEffect effect)>> effectsByBase = new Dictionary<long, List<(int, CraftAlloyEffect)>>();

    public int ElementCount => Elements.Length;

    private CraftRules() { }

    // ------------------------------------------------------------------ lookups
    public int IndexOf(string elementId) => !string.IsNullOrEmpty(elementId) && elementIndex.TryGetValue(elementId, out int i) ? i : -1;
    public CraftElement Element(string id) { int i = IndexOf(id); return i >= 0 ? Elements[i] : null; }
    public string ElementName(string id) => Element(id)?.Name ?? id;
    public CraftOreDefinition Ore(string id) => Find<CraftOreDefinition>("ore:", id);
    public CraftFuel Fuel(string id) => Find<CraftFuel>("fuel:", id);
    public CraftQuenchant Quenchant(string id) => Find<CraftQuenchant>("quench:", id);
    public CraftTanningAgent Agent(string id) => Find<CraftTanningAgent>("agent:", id);
    public CraftHideDefinition Hide(string id) => Find<CraftHideDefinition>("hide:", id);
    public CraftWoodDefinition Wood(string id) => Find<CraftWoodDefinition>("wood:", id);
    public CraftGemDefinition Gem(string id) => Find<CraftGemDefinition>("gem:", id);
    public CraftGemCut Cut(string id) => Find<CraftGemCut>("cut:", id);
    public CraftSupplyDefinition Supply(string id) => Find<CraftSupplyDefinition>("supply:", id);
    public CraftAbrasive Abrasive(string id) => Find<CraftAbrasive>("abrasive:", id);
    public CraftMold Mold(string id) => Find<CraftMold>("mold:", id) ?? PatternMold(id);

    /// <summary>The id prefix of a mould rammed up round a pattern of a design's part: <c>cast:design/role</c>.</summary>
    public const string PatternMoldPrefix = "cast:";
    private readonly Dictionary<string, CraftMold> patternMolds = new Dictionary<string, CraftMold>(StringComparer.OrdinalIgnoreCase);

    public static string PatternMoldId(string design, string role) => $"{PatternMoldPrefix}{design}/{role}";

    /// <summary>
    /// A mould in the shape of a design's metal part (sand rammed round a pattern of it): the part's profile at the
    /// middle of its length range, its eye; what is cast in it is meant for that part. Null when there is no such part.
    /// </summary>
    public CraftMold PatternMold(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.StartsWith(PatternMoldPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        if (patternMolds.TryGetValue(id, out CraftMold cached))
            return cached;
        string rest = id.Substring(PatternMoldPrefix.Length);
        int slash = rest.IndexOf('/');
        CraftDesign d = slash > 0 ? Design(rest.Substring(0, slash)) : null;
        CraftComponentSpec c = d?.Component(rest.Substring(slash + 1));
        CraftMold m = null;
        if (c != null && Castable(c))
        {
            CraftShapeTarget shape = c.shape;
            m = new CraftMold
            {
                id = PatternMoldId(d.id, c.role), displayName = $"{d.Name}: {c.Name}",
                length = (shape.lengthMin + Math.Max(shape.lengthMin, shape.lengthMax)) * 0.5f,
                profile = new List<CraftProfilePoint>(shape.profile),
                width = shape.profile[0].width, thickness = shape.profile[0].thickness,
                eyeAt = shape.eye ? shape.eyeAt : -1f, eyeDiameter = shape.eye ? shape.eyeDiameter : 0f,
                designId = d.id, designRole = c.role,
            };
        }
        patternMolds[id] = m;
        return m;
    }

    /// <summary>A metal part with a shape to it can be cast in a mould of its own.</summary>
    public static bool Castable(CraftComponentSpec c) =>
        c != null && c.kind == CraftPieceKind.Metal && !c.supply && c.shape != null && c.shape.profile != null && c.shape.profile.Count >= 2;

    /// <summary>Every mould: the stock ones, then one in the shape of each design's castable metal part.</summary>
    public List<CraftMold> AllMolds()
    {
        var list = new List<CraftMold>(Molds);
        foreach (CraftDesign d in Designs)
            foreach (CraftComponentSpec c in d.components)
                if (Castable(c))
                {
                    CraftMold m = PatternMold(PatternMoldId(d.id, c.role));
                    if (m != null) list.Add(m);
                }
        return list;
    }
    public CraftPattern Pattern(string id) => Find<CraftPattern>("pattern:", id);
    public CraftDesign Design(string id) => Find<CraftDesign>("design:", id);
    public CraftMaterialClass Class(string id) => Find<CraftMaterialClass>("class:", id);

    private T Find<T>(string prefix, string id) where T : class =>
        !string.IsNullOrEmpty(id) && byId.TryGetValue(prefix + id, out object o) ? o as T : null;

    /// <summary>The item a design's finished items copy their moves and model from: its own Template, else one set on the rules (the database's list).</summary>
    public ItemSO TemplateOf(CraftDesign d)
    {
        if (d == null)
            return null;
        if (d.template != null)
            return d.template;
        return templates.TryGetValue(d.id, out ItemSO t) ? t : null;
    }

    /// <summary>Sets the template of a design for these rules only (the design's own Template field still wins).</summary>
    public void SetTemplate(string designId, ItemSO item)
    {
        if (string.IsNullOrEmpty(designId))
            return;
        if (item == null) templates.Remove(designId);
        else templates[designId] = item;
    }

    /// <summary>The first supply of a kind (the thread used when none is chosen).</summary>
    public CraftSupplyDefinition FirstSupply(CraftSupplyKind kind)
    {
        foreach (CraftSupplyDefinition s in Supplies)
            if (s.kind == kind) return s;
        return null;
    }

    /// <summary>The effects on a property for a base metal (and those for any base).</summary>
    public IEnumerable<(int element, CraftAlloyEffect effect)> EffectsOn(int baseElement, CraftProperty property)
    {
        if (effectsByBase.TryGetValue(Key(baseElement, property), out var list))
            foreach (var e in list) yield return e;
        if (baseElement >= 0 && effectsByBase.TryGetValue(Key(-1, property), out var any))
            foreach (var e in any) yield return e;
    }

    private static long Key(int baseElement, CraftProperty p) => ((long)(baseElement + 1) << 16) | (long)p;

    /// <summary>The abrasives of one kind.</summary>
    public List<CraftAbrasive> AbrasivesOf(CraftAbrasiveKind kind)
    {
        var list = new List<CraftAbrasive>();
        foreach (CraftAbrasive a in Abrasives)
            if (a.kind == kind) list.Add(a);
        return list;
    }

    // ------------------------------------------------------------------ compiling
    /// <summary>Builds rules from <paramref name="content"/> (null lists are empty). Problems are in <see cref="CompileErrors"/> and <see cref="CompileWarnings"/>.</summary>
    public static CraftRules Compile(CraftSimulationSettings settings, CraftContent content)
    {
        var r = new CraftRules { Settings = settings ?? new CraftSimulationSettings() };
        content ??= new CraftContent();
        r.Elements = Unique(content.elements, e => e.id, "element", r);
        for (int i = 0; i < r.Elements.Length; i++)
            r.elementIndex[r.Elements[i].id] = i;
        r.Ores = r.Register("ore:", Unique(content.ores, e => e.id, "ore", r), e => e.id);
        r.Fuels = r.Register("fuel:", Unique(content.fuels, e => e.id, "fuel", r), e => e.id);
        r.Quenchants = r.Register("quench:", Unique(content.quenchants, e => e.id, "quenchant", r), e => e.id);
        r.Agents = r.Register("agent:", Unique(content.agents, e => e.id, "vat agent", r), e => e.id);
        r.Hides = r.Register("hide:", Unique(content.hides, e => e.id, "hide", r), e => e.id);
        r.Woods = r.Register("wood:", Unique(content.woods, e => e.id, "wood", r), e => e.id);
        r.Supplies = r.Register("supply:", Unique(content.supplies, e => e.id, "supply", r), e => e.id);
        r.Abrasives = r.Register("abrasive:", Unique(content.abrasives, e => e.id, "abrasive", r), e => e.id);
        r.Molds = r.Register("mold:", Unique(content.molds, e => e.id, "mould", r), e => e.id);
        r.Patterns = r.Register("pattern:", Unique(content.patterns, e => e.id, "pattern", r), e => e.id);
        r.Designs = r.Register("design:", Unique(content.designs, e => e.id, "design", r), e => e.id);
        r.Gems = r.Register("gem:", Unique(content.gems, e => e.id, "gem", r), e => e.id);
        r.Cuts = r.Register("cut:", Unique(content.cuts, e => e.id, "gem cut", r), e => e.id);
        CraftMaterialClass[] classes = r.Register("class:", Unique(content.classes, e => e.id, "material class", r), e => e.id);
        // Highest priority first; equal priorities keep the order they are listed in (Array.Sort alone is not stable).
        var listed = new Dictionary<CraftMaterialClass, int>();
        for (int i = 0; i < classes.Length; i++) listed[classes[i]] = i;
        Array.Sort(classes, (a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : listed[a].CompareTo(listed[b]));
        r.Classes = classes;

        var effects = new List<CraftAlloyEffect>();
        if (content.effects != null)
            foreach (CraftAlloyEffect e in content.effects)
            {
                if (e == null) continue;
                int el = r.IndexOf(e.element);
                int b = string.IsNullOrWhiteSpace(e.baseElement) ? -1 : r.IndexOf(e.baseElement);
                if (el < 0)
                {
                    r.CompileErrors.Add($"Alloy effect on '{e.property}': unknown element '{e.element}'.");
                    continue;
                }
                if (b < 0 && !string.IsNullOrWhiteSpace(e.baseElement))
                {
                    r.CompileErrors.Add($"Alloy effect of {e.element}: unknown base '{e.baseElement}'.");
                    continue;
                }
                effects.Add(e);
                long key = Key(b, e.property);
                if (!r.effectsByBase.TryGetValue(key, out var list))
                    r.effectsByBase[key] = list = new List<(int, CraftAlloyEffect)>();
                list.Add((el, e));
            }
        r.Effects = effects.ToArray();
        r.SteelBase = r.IndexOf(r.Settings.steelBase);
        r.SteelCarbon = r.IndexOf(r.Settings.steelCarbon);
        r.Check();
        return r;
    }

    private T[] Register<T>(string prefix, T[] items, Func<T, string> id)
    {
        foreach (T x in items)
            byId[prefix + id(x)] = x;
        return items;
    }

    private static T[] Unique<T>(List<T> list, Func<T, string> id, string what, CraftRules r) where T : class
    {
        var result = new List<T>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (list != null)
            foreach (T x in list)
            {
                if (x == null) continue;
                string key = id(x);
                if (string.IsNullOrWhiteSpace(key))
                {
                    r.CompileErrors.Add($"A {what} has no id.");
                    continue;
                }
                if (!seen.Add(key))
                {
                    r.CompileWarnings.Add($"Two {what}s have the id '{key}': the first is used.");
                    continue;
                }
                result.Add(x);
            }
        return result.ToArray();
    }

    /// <summary>
    /// References between definitions (ore yields, class ranges, designs' patterns...) as errors, and what the content
    /// asks that it cannot give as warnings: a part that cannot be made to its own target or from any stock, a price that
    /// loses or prints money, classes that overlap (<see cref="CheckParts"/>, <see cref="CheckContradictions"/>).
    /// </summary>
    private void Check()
    {
        foreach (CraftOreDefinition o in Ores)
        {
            if (o.yields == null || o.yields.Count == 0)
                CompileErrors.Add($"Ore '{o.id}' yields nothing.");
            else
                foreach (CraftOreYield y in o.yields)
                    if (y == null || IndexOf(y.element) < 0)
                        CompileErrors.Add($"Ore '{o.id}': unknown element '{y?.element}'.");
            if (o.mineralMax < o.mineralMin)
                CompileWarnings.Add($"Ore '{o.id}': Mineral Max is below Mineral Min.");
            if (!string.IsNullOrEmpty(o.needsFlux) && Supply(o.needsFlux) == null)
                CompileErrors.Add($"Ore '{o.id}': unknown flux '{o.needsFlux}'.");
        }
        foreach (CraftMaterialClass c in Classes)
        {
            if (IndexOf(c.baseElement) < 0)
                CompileErrors.Add($"Material class '{c.id}': unknown base '{c.baseElement}'.");
            if (c.ranges != null)
                foreach (CraftElementRange e in c.ranges)
                    if (e == null || IndexOf(e.element) < 0)
                        CompileErrors.Add($"Material class '{c.id}': unknown element '{e?.element}'.");
        }
        foreach (CraftDesign d in Designs)
        {
            if (d.components == null || d.components.Count == 0)
            {
                CompileErrors.Add($"Design '{d.id}' has no components.");
                continue;
            }
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CraftComponentSpec c in d.components)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.role))
                {
                    CompileErrors.Add($"Design '{d.id}': a component has no role.");
                    continue;
                }
                if (!roles.Add(c.role))
                    CompileErrors.Add($"Design '{d.id}': two components are '{c.role}'.");
                if (c.kind == CraftPieceKind.Hide && !c.supply && Pattern(c.leather?.pattern) == null)
                    CompileErrors.Add($"Design '{d.id}', {c.role}: unknown pattern '{c.leather?.pattern}'.");
                if ((c.kind == CraftPieceKind.Metal || c.kind == CraftPieceKind.Wood) && !c.supply && (c.shape?.profile == null || c.shape.profile.Count < 2) && c.kind == CraftPieceKind.Metal && d.kind != CraftDesignKind.Jewelry)
                    CompileErrors.Add($"Design '{d.id}', {c.role}: the shape needs at least two profile points.");
            }
            if (d.joints != null)
                foreach (CraftJointSpec j in d.joints)
                {
                    if (j == null) continue;
                    if (!roles.Contains(j.roleA) || !string.IsNullOrEmpty(j.roleB) && !roles.Contains(j.roleB))
                        CompileErrors.Add($"Design '{d.id}': joint '{j.id}' joins an unknown part ({j.roleA}, {j.roleB}).");
                }
            // A hilted blade's tang, at its shortest, must come through the longest grip and pommel it is made for (and be peened).
            CraftJointSpec peen = CraftAssembly.PeenJoint(d);
            CraftShapeTarget blade = peen != null ? d.Component(peen.roleA)?.shape : null;
            if (blade != null && blade.tangTo > 0)
            {
                double hilt = CraftAssembly.PeenAllowance;
                foreach (CraftComponentSpec c in d.components)
                    if (c != null && !c.supply && (c.function == CraftComponentFunction.Grip || c.function == CraftComponentFunction.Counterweight))
                        hilt += Math.Max(1, c.count) * (c.shape != null ? c.shape.lengthMax : c.wood?.lengthMax ?? 0);
                if (blade.lengthMin * blade.tangTo < hilt)
                    CompileWarnings.Add($"Design '{d.id}': the tang of its shortest blade ({blade.lengthMin * blade.tangTo:0} mm) is shorter than its longest grip and pommel and the peen ({hilt:0} mm): it could not be finished.");
            }
            CheckParts(d);
        }
        CheckContradictions();
        foreach (CraftPattern p in Patterns)
            if (p.Area == 0)
                CompileErrors.Add($"Pattern '{p.id}' covers no cells.");
        foreach (CraftGemCut c in Cuts)
            if (c.tiers == null || c.tiers.Count == 0)
                CompileErrors.Add($"Gem cut '{c.id}' has no tiers of facets.");
        foreach (CraftGemDefinition g in Gems)
            if (!string.IsNullOrEmpty(g.preferredCut) && Cut(g.preferredCut) == null)
                CompileWarnings.Add($"Gem '{g.id}': unknown preferred cut '{g.preferredCut}'.");
        if (SteelBase < 0)
            CompileWarnings.Add($"No '{Settings.steelBase}' element: nothing hardens by quenching.");
    }

    // ------------------------------------------------------------------ contradictions in the content
    /// <summary>Density (g/cm³) of a composition (element id, mass fraction) by these rules; 0 when an element is unknown.</summary>
    private double DensityOf(params (string element, double fraction)[] composition)
    {
        var f = new double[ElementCount];
        foreach (var (element, fraction) in composition)
        {
            int i = IndexOf(element);
            if (i < 0) return 0;
            f[i] += fraction;
        }
        return CraftMetallurgy.Analyze(this, f).Density;
    }

    /// <summary>The metal a pattern's mass is checked in: a hardening steel and a tin bronze (the two a part is most often made of).</summary>
    private double steelDensity = -1, bronzeDensity = -1;

    /// <summary>
    /// What a design asks of its parts that the content cannot give: a part whose own profile weighs outside its mass range
    /// (in steel or in bronze), an eye as wide as the metal round it, a weapon rated for a mass its main part cannot have,
    /// a wooden part longer or thicker than any blank or log of a wood tough enough, leather too thick for every thread.
    /// </summary>
    private void CheckParts(CraftDesign d)
    {
        if (steelDensity < 0)
        {
            steelDensity = DensityOf((Settings.steelBase, 0.995), (Settings.steelCarbon, 0.005));
            bronzeDensity = DensityOf(("copper", 0.89), ("tin", 0.11));
        }
        foreach (CraftComponentSpec c in d.components)
        {
            if (c == null || c.supply) continue;
            CraftShapeTarget s = c.shape;
            if (s != null && (s.lengthMin > s.lengthMax || s.massMin > s.massMax))
                CompileWarnings.Add($"Design '{d.id}', {c.role}: a length or mass range runs backwards (its min is above its max).");
            if (Castable(c))
            {
                // A part made exactly to its own profile - what its pattern mould casts, what the anvil's target draws -
                // must be inside its own mass range, or matching the drawn target is scored too light or too heavy.
                CraftMold mold = PatternMold(PatternMoldId(d.id, c.role));
                foreach (var (metal, density) in new[] { ("steel", steelDensity), ("bronze", bronzeDensity) })
                {
                    if (mold == null || density <= 0) continue;
                    double grams = CraftSmelting.MoldMass(this, mold, density);
                    if (grams < s.massMin - 0.5 || grams > s.massMax + 0.5)
                        CompileWarnings.Add($"Design '{d.id}', {c.role}: its own profile weighs {grams:0} g in {metal}, outside its mass range ({s.massMin:0}-{s.massMax:0} g): made exactly to it, it is scored too {(grams < s.massMin ? "light" : "heavy")}.");
                }
                if (s.eye && s.eyeDiameter > 0)
                {
                    s.At(s.eyeAt, out float width, out _);
                    if (width - s.eyeDiameter < 2)
                        CompileWarnings.Add($"Design '{d.id}', {c.role}: its {s.eyeDiameter:0.#} mm eye leaves less than a millimetre of metal each side in a part {width:0.#} mm wide there.");
                }
                CheckEdge(d, c, s);
            }
            if (c.kind == CraftPieceKind.Wood && c.wood != null)
            {
                CraftWoodSpec w = c.wood;
                if (w.lengthMin > w.lengthMax || w.diameterMin > w.diameterMax)
                    CompileWarnings.Add($"Design '{d.id}', {c.role}: a length or thickness range runs backwards (its min is above its max).");
                // as wide and thick as its profile's widest point (a board), at least its least diameter (a haft)
                double across = w.diameterMin, through = w.diameterMin;
                if (s?.profile != null)
                    foreach (CraftProfilePoint p in s.profile)
                    {
                        across = Math.Max(across, p.width);
                        through = Math.Max(through, Math.Min(p.width, p.thickness));
                    }
                bool any = false;
                foreach (CraftWoodDefinition wood in Woods)
                {
                    if (wood.toughness < w.toughnessMin) continue;
                    bool blank = wood.blankLength >= w.lengthMin && (wood.blankWidth >= across && wood.blankThickness >= through || wood.blankThickness >= across && wood.blankWidth >= through);
                    bool log = CraftDefaults.LogLength(wood) >= w.lengthMin && CraftDefaults.LogDiameter >= across;
                    if (blank || log) { any = true; break; }
                }
                if (!any)
                    CompileWarnings.Add($"Design '{d.id}', {c.role}: no blank or log of a wood tough enough ({w.toughnessMin:0.##}) is {w.lengthMin:0} mm long and {across:0} mm across: it cannot be made.");
            }
            if (c.kind == CraftPieceKind.Hide && c.leather != null && c.leather.thicknessMin > c.leather.thicknessMax)
                CompileWarnings.Add($"Design '{d.id}', {c.role}: its thickness range runs backwards (its min is above its max).");
            if (c.kind == CraftPieceKind.Gem && c.gem != null && c.gem.caratsMin > c.gem.caratsMax)
                CompileWarnings.Add($"Design '{d.id}', {c.role}: its carat range runs backwards (its min is above its max).");
        }
        // A weapon's damage and speed are rated for its reference mass: one main part (of several, each) must be able to weigh it.
        CraftComponentSpec main = d.MainComponent;
        if (d.IsWeaponLike && main != null && main.kind == CraftPieceKind.Metal && main.shape != null && main.shape.massMax > main.shape.massMin
            && (d.referenceMass < main.shape.massMin || d.referenceMass > main.shape.massMax))
            CompileWarnings.Add($"Design '{d.id}': its reference mass ({d.referenceMass:0} g) is outside its {main.Name.ToLowerInvariant()}'s mass range ({main.shape.massMin:0}-{main.shape.massMax:0} g): made well, it never reaches its rated damage.");
        // Leather sewn with the strongest thread there is must hold: the thickest leather a seam joins wants this much.
        double strongest = 0;
        foreach (CraftSupplyDefinition t in Supplies)
            if (t.kind == CraftSupplyKind.Thread) strongest = Math.Max(strongest, t.strength);
        if (d.joints != null && strongest > 0)
            foreach (CraftJointSpec j in d.joints)
            {
                if (j == null || j.kind != CraftJointKind.Stitch) continue;
                double thick = 0;
                foreach (string role in new[] { j.roleA, j.roleB })
                {
                    CraftComponentSpec part = d.Component(role);
                    if (part != null && part.kind == CraftPieceKind.Hide && part.leather != null && part.leather.fabric != CraftFabric.Cloth)
                        thick = Math.Max(thick, part.leather.thicknessMax);
                }
                if (thick > 0 && CraftTextiles.ThreadNeed(thick) > strongest + 1e-6)
                    CompileWarnings.Add($"Design '{d.id}': its seam '{j.Name}' through leather up to {thick:0.#} mm wants a thread of {CraftTextiles.ThreadNeed(thick):0.##}, stronger than any ({strongest:0.##}).");
            }
    }

    /// <summary>
    /// An edge its own section can carry (<see cref="CraftSurface.EdgeHalfAngle"/>): an angle no steeper than bevels are
    /// ground; a section not too narrow for the design's angle along most of the edge (ground to it, the bevels would
    /// meet steeper, and the edge is judged at that); and nowhere so thick for its width that they meet steeper than a
    /// whetstone is held (that stretch could not be honed).
    /// </summary>
    private void CheckEdge(CraftDesign d, CraftComponentSpec c, CraftShapeTarget s)
    {
        if (s == null || s.edge == CraftEdgeKind.None || c.kind != CraftPieceKind.Metal || s.profile == null || s.profile.Count < 2)
            return;
        if (s.edgeAngle > 2 * CraftSurface.MaxBevelAngle + 1e-3)
            CompileWarnings.Add($"Design '{d.id}', {c.role}: its edge angle ({s.edgeAngle:0}°) is steeper than bevels are ground ({2 * CraftSurface.MaxBevelAngle:0}° at the most, {CraftSurface.MaxBevelAngle:0}° a side): it can never be reached.");
        int edges = s.edge == CraftEdgeKind.Double ? 2 : 1, n = 0, stretches = Math.Max(8, Settings.segments);
        double sum = 0, steepest = 0, steepestAt = 0;
        for (int k = 0; k < stretches; k++)
        {
            float t = (k + 0.5f) / stretches;
            if (t < s.edgeFrom || t > s.edgeTo) continue;
            s.At(t, out float width, out float thickness);
            double a = CraftSurface.EdgeHalfAngle(width, thickness, s.edgeAngle, edges);
            sum += a;
            n++;
            if (a > steepest) { steepest = a; steepestAt = t; }
        }
        if (n == 0)
            return;
        if (sum / n > Math.Min(s.edgeAngle * 0.5, CraftSurface.MaxBevelAngle) + 2)
            CompileWarnings.Add($"Design '{d.id}', {c.role}: its section is too narrow for its {s.edgeAngle:0}° edge along most of it - ground to the section, the bevels meet at about {2 * sum / n:0}°, and the edge is judged at that: give it that angle, or a wider, thinner section.");
        if (steepest > CraftSurface.StoneAngleMax + 1e-3)
            CompileWarnings.Add($"Design '{d.id}', {c.role}: at {steepestAt * 100:0}% of its length its section is too thick for its width - the bevels meet at {steepest:0}° a side there, steeper than a whetstone is held ({CraftSurface.StoneAngleMax:0}°): that stretch could not be honed.");
    }

    /// <summary>
    /// Contradictions between definitions: an ore that sells for more than its poorest lump's metal is worth (or for a
    /// fraction of a usual lump's), classes of one base and priority whose ranges overlap, ranges that run backwards,
    /// fibres that spin into what is not a thread or weave into what is not cloth, an ore's flux that is not a flux.
    /// </summary>
    private void CheckContradictions()
    {
        foreach (CraftOreDefinition o in Ores)
        {
            if (o.yields != null)
                foreach (CraftOreYield y in o.yields)
                    if (y != null && y.min > y.max)
                        CompileWarnings.Add($"Ore '{o.id}': its {y.element} yield runs backwards (its min is above its max).");
            if (o.dirtMax < o.dirtMin)
                CompileWarnings.Add($"Ore '{o.id}': Dirt Max is below Dirt Min.");
            if (!string.IsNullOrEmpty(o.needsFlux) && Supply(o.needsFlux) is CraftSupplyDefinition flux && flux.kind != CraftSupplyKind.Flux)
                CompileWarnings.Add($"Ore '{o.id}': it needs '{o.needsFlux}', which is not a flux.");
            if (o.price <= 0 || o.yields == null || o.yields.Count == 0) continue;
            // Smelted, the poorest lump must give metal worth its price; a usual lump not many times it (a money machine).
            double poorest = CraftValue.OreMetal(this, o, 0), usual = CraftValue.OreMetal(this, o, 0.5);
            if (poorest > 0 && o.price > poorest + 0.005)
                CompileWarnings.Add($"Ore '{o.id}': a lump costs {o.price:0.##}, but the metal of the poorest lump is worth {poorest:0.##}: smelting it loses money.");
            if (usual > 5 * o.price)
                CompileWarnings.Add($"Ore '{o.id}': a lump costs {o.price:0.##}, but a usual lump's metal is worth {usual:0.##}: buying and smelting it prints money.");
        }
        foreach (CraftMaterialClass c in Classes)
            if (c.ranges != null)
                foreach (CraftElementRange e in c.ranges)
                    if (e != null && e.min > e.max)
                        CompileWarnings.Add($"Material class '{c.id}': its {e.element} range runs backwards (its min is above its max).");
        // Equal-priority classes of one base are tried in the order they are listed; if their ranges overlap, which one
        // names a metal depends on that order alone.
        for (int a = 0; a < Classes.Length; a++)
            for (int b = a + 1; b < Classes.Length; b++)
            {
                CraftMaterialClass x = Classes[a], y = Classes[b];
                if (x.priority != y.priority || !string.Equals(x.baseElement, y.baseElement, StringComparison.OrdinalIgnoreCase)) continue;
                if (Overlap(x, y))
                    CompileWarnings.Add($"Material classes '{x.id}' and '{y.id}': the same base and priority ({x.priority}), and a metal can be both: give one a higher priority or narrow a range.");
            }
        foreach (CraftSupplyDefinition s in Supplies)
        {
            if (s.spinsInto != null)
                foreach (string t in s.spinsInto)
                    if (!string.IsNullOrEmpty(t) && Supply(t)?.kind != CraftSupplyKind.Thread)
                        CompileWarnings.Add($"Supply '{s.id}': it spins into '{t}', which is not a thread.");
            if (!string.IsNullOrEmpty(s.weavesInto) && !(Hide(s.weavesInto)?.textile ?? false))
                CompileWarnings.Add($"Supply '{s.id}': it weaves into '{s.weavesInto}', which is not a cloth.");
            CheckSpinning(s);
        }
        foreach (CraftWoodDefinition w in Woods)
            if (w.blankLength <= 0 || w.blankWidth <= 0 || w.blankThickness <= 0)
                CompileWarnings.Add($"Wood '{w.id}': its blank has no size.");
    }

    /// <summary>
    /// A fibre, the thread it spins into and the cloth it weaves into are worth more at each step, never many times more:
    /// a skein of its thread (<see cref="CraftFibre.MetresPerThread"/> of a middling yarn, a half-prepared fibre drafted
    /// at 0.5) at most <see cref="SpinningAddsAtMost"/> times the fibre in it, and the yarn a bolt is woven from (12 m a
    /// cell, <see cref="CraftFibre.MetresPerCell"/>) at its dearest thread less than the bolt.
    /// </summary>
    private void CheckSpinning(CraftSupplyDefinition s)
    {
        if (s.kind != CraftSupplyKind.Fibre || s.spinsInto == null || s.gramsPerItem <= 0)
            return;
        double fibre = s.price / s.gramsPerItem * CraftFibre.SkeinGrams, dearest = 0;
        foreach (string t in s.spinsInto)
        {
            CraftSupplyDefinition thread = Supply(t);
            if (thread == null) continue;
            dearest = Math.Max(dearest, thread.price);
            if (thread.price > fibre * SpinningAddsAtMost)
                CompileWarnings.Add($"Supply '{t}': a {CraftFibre.MetresPerThread:0} m skein costs {thread.price:0.###}, but the {s.Name.ToLowerInvariant()} in it is worth {fibre:0.###}: spinning prints money.");
        }
        CraftHideDefinition cloth = Hide(s.weavesInto);
        if (cloth == null || !cloth.textile)
            return;
        double yarn = dearest * cloth.width * cloth.height * CraftFibre.MetresPerCell / CraftFibre.MetresPerThread;
        if (yarn > cloth.price)
            CompileWarnings.Add($"Hide '{cloth.id}': a bolt costs {cloth.price:0.##}, but the yarn woven into it is worth {yarn:0.##}: weaving throws value away.");
    }

    /// <summary>The most a skein of thread may be worth against the fibre in it (<see cref="CheckSpinning"/>).</summary>
    public const double SpinningAddsAtMost = 4;

    /// <summary>Can one composition fall in both classes' ranges (more than on a shared boundary)? An element a class does not name may be anything.</summary>
    private static bool Overlap(CraftMaterialClass x, CraftMaterialClass y)
    {
        var lo = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var hi = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (CraftMaterialClass c in new[] { x, y })
            if (c.ranges != null)
                foreach (CraftElementRange r in c.ranges)
                {
                    if (r == null) continue;
                    lo[r.element] = Math.Max(lo.TryGetValue(r.element, out double l) ? l : 0, r.min);
                    hi[r.element] = Math.Min(hi.TryGetValue(r.element, out double h) ? h : 1, r.max);
                }
        double least = 0;
        foreach (KeyValuePair<string, double> e in lo)
        {
            if (hi[e.Key] - e.Value <= 1e-6) return false;
            least += e.Value;
        }
        return least < 0.5; // what the base leaves room for: it is the most abundant metal
    }
}
