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
    public CraftSupplyDefinition Supply(string id) => Find<CraftSupplyDefinition>("supply:", id);
    public CraftAbrasive Abrasive(string id) => Find<CraftAbrasive>("abrasive:", id);
    public CraftMold Mold(string id) => Find<CraftMold>("mold:", id);
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
        CraftMaterialClass[] classes = r.Register("class:", Unique(content.classes, e => e.id, "material class", r), e => e.id);
        Array.Sort(classes, (a, b) => b.priority.CompareTo(a.priority));
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

    /// <summary>References between definitions (ore yields, class ranges, designs' patterns...).</summary>
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
                if ((c.kind == CraftPieceKind.Metal || c.kind == CraftPieceKind.Wood) && !c.supply && (c.shape?.profile == null || c.shape.profile.Count < 2) && c.kind == CraftPieceKind.Metal)
                    CompileErrors.Add($"Design '{d.id}', {c.role}: the shape needs at least two profile points.");
            }
            if (d.joints != null)
                foreach (CraftJointSpec j in d.joints)
                {
                    if (j == null) continue;
                    if (!roles.Contains(j.roleA) || !string.IsNullOrEmpty(j.roleB) && !roles.Contains(j.roleB))
                        CompileErrors.Add($"Design '{d.id}': joint '{j.id}' joins an unknown part ({j.roleA}, {j.roleB}).");
                }
        }
        foreach (CraftPattern p in Patterns)
            if (p.Area == 0)
                CompileErrors.Add($"Pattern '{p.id}' covers no cells.");
        if (SteelBase < 0)
            CompileWarnings.Add($"No '{Settings.steelBase}' element: nothing hardens by quenching.");
    }
}
