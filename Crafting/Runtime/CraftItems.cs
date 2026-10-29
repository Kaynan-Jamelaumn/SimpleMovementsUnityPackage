using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// The items crafting gives and takes:
/// <list type="bullet">
/// <item><b>Stock</b> made at runtime from the Crafting Database (test stations' free supplies, projects without stock
/// assets): bars of the stock metals, ore lumps, raw hides, wooden blanks, fasteners, fuel, supplies, agents.</item>
/// <item><b>Worked pieces</b> (<see cref="CraftPieceItemSO"/>): one item per piece carried off a station.</item>
/// <item><b>Finished items</b> (<see cref="ForgedWeaponSO"/>, <see cref="ForgedArmorSO"/>): a copy of the design's
/// template with the numbers of what was made.</item>
/// </list>
/// Pieces and finished items are written to <c>persistentDataPath/Crafting/Items</c> where they are made (offline, or
/// on the server) and found again by their ids (<see cref="ItemDatabase.Item"/> asks this class, an
/// <see cref="IRuntimeItemSource"/>), so a save that names them still works after a restart. In a network session a
/// client that meets an id it does not know asks the server, which <see cref="Export"/>s the definition; the client
/// <see cref="Import"/>s it (never saved): the same sword with the same numbers and the same model on every machine.
/// Runtime stock (<c>craftstock:</c> ids) is made from the rules on each machine.
/// </summary>
public static class CraftItems
{
    private static readonly Dictionary<string, CraftStockItemSO> stock = new Dictionary<string, CraftStockItemSO>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ItemSO> made = new Dictionary<string, ItemSO>(StringComparer.Ordinal);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        stock.Clear();
        made.Clear();
    }

    private static readonly Source source = new Source();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSource() => ItemDatabase.AddSource(source);

    /// <summary>Crafting's items for the item database and the network (see <see cref="IRuntimeItemSource"/>).</summary>
    private sealed class Source : IRuntimeItemSource
    {
        public bool Owns(string id) => IsCraftId(id);
        public ItemSO Find(string id) => Resolve(id);
        public string Export(string id) => CraftItems.Export(id);
        public ItemSO Import(string id, string definition) => CraftItems.Import(id, definition);
    }

    /// <summary>Was <paramref name="item"/> made at runtime (no asset)?</summary>
    public static bool IsRuntime(ItemSO item) => item != null && (item.hideFlags & HideFlags.DontSave) != 0;

    /// <summary>Is <paramref name="id"/> a crafting runtime id (a worked piece, a finished item, runtime stock)?</summary>
    public static bool IsCraftId(string id) =>
        !string.IsNullOrEmpty(id) && (id.StartsWith(CraftPieceItemSO.IdPrefix, StringComparison.Ordinal) || id.StartsWith(CraftedTooltip.IdPrefix, StringComparison.Ordinal)
                                      || id.StartsWith(CraftStockItemSO.IdPrefix, StringComparison.Ordinal));

    /// <summary>A client of a network session: its runtime items come from the server, never from its own disk.</summary>
    private static bool ClientOfSession => GameNet.NetAuthority.IsNetworked && !GameNet.NetAuthority.IsServer;

    // ------------------------------------------------------------------ stock
    /// <summary>
    /// A stock item made at runtime: <paramref name="id"/> is the stock metal's name for Metal ("Tool steel"), the content
    /// id for the rest ("hematite", "deer", "ash", "charcoal", "limestone", "bark"), the fastener kind for Fasteners.
    /// </summary>
    public static CraftStockItemSO Stock(CraftRules rules, CraftStockKind kind, string id)
    {
        if (rules == null || string.IsNullOrEmpty(id))
            return null;
        string key = kind + ":" + id;
        if (stock.TryGetValue(key, out CraftStockItemSO cached) && cached != null)
            return cached;
        var item = ScriptableObject.CreateInstance<CraftStockItemSO>();
        item.hideFlags = HideFlags.DontSave;
        string name = id, description = "";
        float price = 1f, weight = 1f;
        Color color = Color.gray;
        List<CraftStockElement> metal = null;
        Vector3? size = null;
        switch (kind)
        {
            case CraftStockKind.Metal:
            {
                // A bar, or the jeweller's wire and strip ("Gold wire", "Sterling silver bezel strip").
                string metalName = id;
                (string suffix, float length, float width, float thickness) form = ("bar", 200, 30, 6);
                foreach (var f in CraftDefaults.JewelleryForms)
                    if (id.EndsWith(" " + f.suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        metalName = id.Substring(0, id.Length - f.suffix.Length - 1);
                        form = f;
                    }
                var m = CraftDefaults.StockMetals().Find(x => string.Equals(x.name, metalName, StringComparison.OrdinalIgnoreCase));
                if (m.composition == null)
                    return null;
                metal = new List<CraftStockElement>();
                foreach (var e in m.composition) metal.Add(new CraftStockElement(e.element, (float)e.fraction));
                CraftPiece bar = CraftDefaults.Bar(rules, form.length, form.width, form.thickness, m.composition);
                CraftAlloyProfile prof = bar.Profile(rules);
                name = $"{m.name} {form.suffix}";
                description = form.suffix == "bar"
                    ? $"A bar of {m.name.ToLowerInvariant()} from the stores, 20 cm long, 3 cm wide, 6 mm thick."
                    : $"{CraftText.Capital(form.suffix)} of {m.name.ToLowerInvariant()} for the jeweller: {form.length / 10f:0} cm long, {form.width:0.#} mm by {form.thickness:0.#} mm.";
                weight = (float)bar.Mass(rules) / 1000f;
                price = (float)(bar.Mass(rules) * prof.PricePerGram);
                color = prof.Color;
                size = new Vector3(form.length, form.width, form.thickness);
                break;
            }
            case CraftStockKind.Log:
            {
                CraftWoodDefinition w = rules.Wood(id);
                if (w == null) return null;
                name = $"{w.Name} log";
                description = $"A green {w.Name.ToLowerInvariant()} log in its bark, 90 cm long. {w.description}";
                price = w.price * 1.5f;
                weight = (float)(Math.PI * 0.12 * 0.12 * 0.9 * w.density * 1000 * (1 + CraftWoodwork.Green));
                color = Color.Lerp(w.color, new Color(0.35f, 0.27f, 0.2f), 0.6f);
                size = new Vector3(900, 240, 240);
                break;
            }
            case CraftStockKind.Gem:
            {
                CraftGemDefinition g = rules.Gem(id);
                if (g == null) return null;
                // Named for what it looks like: what it is, the gem tests tell.
                name = CraftText.Capital(Unnamed(g.observation));
                description = $"A rough stone: {g.observation}. A loupe, a balance and a refractometer tell what it is.";
                price = g.valuePerCarat * g.typicalCarats * 0.12f;
                weight = 0.01f;
                color = g.color;
                break;
            }
            case CraftStockKind.Ore:
            {
                CraftOreDefinition o = rules.Ore(id);
                if (o == null) return null;
                name = CraftText.Capital(Unnamed(o.observation));
                description = o.observation + ".";
                price = o.price;
                weight = o.lumpMass / 1000f;
                color = o.color;
                break;
            }
            case CraftStockKind.Hide:
            {
                CraftHideDefinition h = rules.Hide(id);
                if (h == null) return null;
                name = $"Raw {h.Name.ToLowerInvariant()}";
                description = h.description;
                price = h.price;
                weight = 3f;
                color = h.color;
                break;
            }
            case CraftStockKind.Wood:
            {
                CraftWoodDefinition w = rules.Wood(id);
                if (w == null) return null;
                name = $"{w.Name} blank";
                description = w.description;
                price = w.price;
                weight = w.blankLength * w.blankWidth * w.blankThickness * w.density / 1e6f;
                color = w.color;
                break;
            }
            case CraftStockKind.Fasteners:
            {
                if (!Enum.TryParse(id, true, out CraftFastenerKind fk)) return null;
                name = fk == CraftFastenerKind.Buckle ? "Iron buckles" : fk == CraftFastenerKind.Stud ? "Iron studs" : "Iron rivets";
                description = "Ready-made, from the stores.";
                price = fk == CraftFastenerKind.Buckle ? 2f : 1f;
                weight = 0.1f;
                color = new Color(0.55f, 0.55f, 0.58f);
                metal = new List<CraftStockElement> { new CraftStockElement("iron", 0.998f), new CraftStockElement("carbon", 0.002f) };
                break;
            }
            case CraftStockKind.Fuel:
            {
                CraftFuel f = rules.Fuel(id);
                if (f == null) return null;
                name = !string.IsNullOrEmpty(f.item) ? f.item : f.Name;
                description = "Fuel for the forge or the furnace.";
                price = 0.5f;
                weight = 1f;
                color = new Color(0.12f, 0.11f, 0.1f);
                break;
            }
            case CraftStockKind.Supply:
            {
                CraftSupplyDefinition s = rules.Supply(id);
                if (s == null) return null;
                name = !string.IsNullOrEmpty(s.item) ? s.item : s.Name;
                description = s.description;
                price = s.price;
                weight = 0.3f;
                color = s.color;
                break;
            }
            case CraftStockKind.Agent:
            {
                CraftTanningAgent a = rules.Agent(id);
                if (a == null) return null;
                name = !string.IsNullOrEmpty(a.item) ? a.item : a.Name;
                description = CraftText.Capital(a.observation) + ".";
                price = 1f;
                weight = 1f;
                color = a.color;
                break;
            }
        }
        item.Setup(name, description, kind, kind == CraftStockKind.Metal || kind == CraftStockKind.Fasteners ? "" : id, price, weight, CraftArt.StockIcon(kind, color), metal, size);
        if (kind == CraftStockKind.Fasteners && Enum.TryParse(id, true, out CraftFastenerKind kindOf))
            item.SetFasteners(kindOf, kindOf == CraftFastenerKind.Buckle ? 2 : 10, kindOf == CraftFastenerKind.Buckle ? 30f : 4f);
        ((UnityEngine.Object)item).name = name;
        item.SetRuntimeKey(key);
        item.SetArt(null, CraftModels.ForStock(item, rules));
        stock[key] = item;
        return item;
    }

    /// <summary>Every stock item a station with <paramref name="tools"/> can use (a test station's free supplies).</summary>
    public static List<CraftStockItemSO> AllStock(CraftRules rules, CraftTools tools)
    {
        var list = new List<CraftStockItemSO>();
        void Add(CraftStockItemSO i) { if (i != null && !list.Contains(i)) list.Add(i); }
        bool metalWork = (tools & (CraftTools.Forge | CraftTools.Anvil | CraftTools.Crucible | CraftTools.Grinder | CraftTools.AssemblyBench)) != 0;
        if (metalWork)
            foreach (var m in CraftDefaults.StockMetals()) Add(Stock(rules, CraftStockKind.Metal, m.name));
        if ((tools & (CraftTools.OreBench | CraftTools.Furnace | CraftTools.Crucible)) != 0)
            foreach (CraftOreDefinition o in rules.Ores) Add(Stock(rules, CraftStockKind.Ore, o.id));
        if ((tools & (CraftTools.FleshingBeam | CraftTools.Vats | CraftTools.Frame | CraftTools.LeatherBench)) != 0)
        {
            foreach (CraftHideDefinition h in rules.Hides) Add(Stock(rules, CraftStockKind.Hide, h.id));
            foreach (CraftTanningAgent a in rules.Agents) Add(Stock(rules, CraftStockKind.Agent, a.id));
        }
        if ((tools & CraftTools.TailorTable) != 0)
            foreach (CraftHideDefinition h in rules.Hides)
                if (h.textile) Add(Stock(rules, CraftStockKind.Hide, h.id));
        if ((tools & (CraftTools.ChoppingBlock | CraftTools.Sawhorse | CraftTools.SteamBox | CraftTools.DryingStack | CraftTools.FinishingBench)) != 0)
            foreach (CraftWoodDefinition w in rules.Woods) Add(Stock(rules, CraftStockKind.Wood, w.id));
        if ((tools & (CraftTools.ShavingHorse | CraftTools.ChoppingBlock)) != 0)
            foreach (CraftWoodDefinition w in rules.Woods) Add(Stock(rules, CraftStockKind.Log, w.id));
        if ((tools & (CraftTools.GemBench | CraftTools.FacetingMachine)) != 0)
            foreach (CraftGemDefinition g in rules.Gems) Add(Stock(rules, CraftStockKind.Gem, g.id));
        if ((tools & CraftTools.JewelersBench) != 0)
            foreach (string m in CraftDefaults.JewelleryMetals)
                foreach (var f in CraftDefaults.JewelleryForms)
                    Add(Stock(rules, CraftStockKind.Metal, $"{m} {f.suffix}"));
        if ((tools & CraftTools.AssemblyBench) != 0)
        {
            foreach (CraftWoodDefinition w in rules.Woods) Add(Stock(rules, CraftStockKind.Wood, w.id));
            Add(Stock(rules, CraftStockKind.Fasteners, nameof(CraftFastenerKind.Rivet)));
            Add(Stock(rules, CraftStockKind.Fasteners, nameof(CraftFastenerKind.Stud)));
            Add(Stock(rules, CraftStockKind.Fasteners, nameof(CraftFastenerKind.Buckle)));
        }
        if ((tools & (CraftTools.Forge | CraftTools.Furnace)) != 0)
            foreach (CraftFuel f in rules.Fuels) Add(Stock(rules, CraftStockKind.Fuel, f.id));
        foreach (CraftSupplyDefinition s in rules.Supplies) Add(Stock(rules, CraftStockKind.Supply, s.id));
        return list;
    }

    private static string Unnamed(string observation)
    {
        if (string.IsNullOrEmpty(observation)) return "unknown ore";
        string s = observation.StartsWith("a ") ? observation.Substring(2) : observation.StartsWith("an ") ? observation.Substring(3) : observation;
        int that = s.IndexOf(" that ", StringComparison.Ordinal);
        return that > 0 ? s.Substring(0, that) : s;
    }

    // ------------------------------------------------------------------ worked pieces
    /// <summary>
    /// The item a piece carried off a station becomes (an assembly carries <paramref name="parts"/>). It is written to
    /// disk when <paramref name="persist"/> so its id resolves after a restart.
    /// </summary>
    public static CraftPieceItemSO PieceItem(CraftRules rules, CraftPiece piece, List<CraftPiece> parts, CraftJournalData known, bool persist = true)
    {
        if (rules == null || piece == null)
            return null;
        var item = ScriptableObject.CreateInstance<CraftPieceItemSO>();
        item.hideFlags = HideFlags.DontSave;
        string key = Guid.NewGuid().ToString("N");
        string name = CraftLook.Name(rules, piece, known);
        double mass = piece.Mass(rules);
        if (parts != null)
            foreach (CraftPiece p in parts) mass += p.Mass(rules);
        item.Setup(key, piece, parts, name, Description(rules, piece), (float)(mass / 1000.0), PieceValue(rules, piece, parts),
            CraftArt.PieceIcon(rules, piece, parts), GridOf(piece));
        item.SetModel(CraftModels.ForPiece(rules, item.RuntimeId, piece, parts));
        made[item.RuntimeId] = item;
        if (persist)
            SavePiece(item);
        return item;
    }

    private static string Description(CraftRules rules, CraftPiece p)
    {
        if (!string.IsNullOrEmpty(p.intentDesign))
        {
            CraftDesign d = rules.Design(p.intentDesign);
            CraftComponentSpec c = d?.Component(p.intentRole);
            if (d != null && c != null) return $"Worked toward the {c.Name.ToLowerInvariant()} of {CraftText.A(d.Name)} {d.Name.ToLowerInvariant()}.";
        }
        return string.IsNullOrEmpty(p.origin) ? "A piece of work." : CraftText.Capital(p.origin) + ".";
    }

    private static Vector2Int GridOf(CraftPiece p)
    {
        switch (p.kind)
        {
            case CraftPieceKind.Hide: return p.hide.width * p.hide.height > 80 ? new Vector2Int(2, 2) : Vector2Int.one;
            case CraftPieceKind.Fasteners:
            case CraftPieceKind.Fibre:
            case CraftPieceKind.Yarn:
            case CraftPieceKind.Gem: return Vector2Int.one;
            default:
                double l = p.Length;
                return new Vector2Int(1, Mathf.Clamp(Mathf.CeilToInt((float)(l / 350.0)), 1, 5));
        }
    }

    /// <summary>What a piece (and its parts) is worth: its material, by weight.</summary>
    public static float PieceValue(CraftRules rules, CraftPiece p, List<CraftPiece> parts = null)
    {
        double v = 0;
        switch (p.kind)
        {
            case CraftPieceKind.Metal:
            case CraftPieceKind.Fasteners:
                v = p.Mass(rules) * (p.Profile(rules)?.PricePerGram ?? 0.01);
                break;
            case CraftPieceKind.Ore:
                v = (rules.Ore(p.ore.ore)?.price ?? 1) * p.ore.MineralMass / Math.Max(1, rules.Ore(p.ore.ore)?.lumpMass ?? 1000);
                break;
            case CraftPieceKind.Hide:
            {
                CraftHideDefinition h = rules.Hide(p.hide.hide);
                int cells = 0;
                foreach (HideCell c in p.hide.cells) if (c.present) cells++;
                double share = h != null && h.width * h.height > 0 ? cells / (double)(h.width * h.height) : 0.5;
                v = (h?.price ?? 5) * share * (1 + CraftLeather.MeanTannage(p.hide));
                break;
            }
            case CraftPieceKind.Wood:
                v = rules.Wood(p.wood.wood)?.price ?? 1;
                break;
            case CraftPieceKind.Fibre:
            {
                CraftSupplyDefinition s = rules.Supply(p.fibre.fibre);
                v = (s?.price ?? 0.5) * p.fibre.grams / Math.Max(1, s?.gramsPerItem ?? 100) * (1 + CraftFibre.Prep(p.fibre));
                break;
            }
            case CraftPieceKind.Yarn:
            {
                string thread = CraftFibre.ThreadOf(rules, p);
                v = (rules.Supply(thread)?.price ?? 0.5) * p.yarn.length / CraftFibre.MetresPerThread;
                break;
            }
            case CraftPieceKind.Gem:
                v = CraftLapidary.Grade(rules, p).Value;
                break;
        }
        if (parts != null)
            foreach (CraftPiece q in parts) v += PieceValue(rules, q);
        return (float)Math.Round(v, 2);
    }

    /// <summary>What an assembly still needs (part names, unmade joints).</summary>
    public static List<string> MissingParts(CraftRules rules, CraftPiece asm, List<CraftPiece> parts)
    {
        var list = new List<string>();
        CraftDesign d = rules.Design(asm?.assembly.design);
        if (d == null)
            return list;
        foreach (CraftComponentSpec c in d.components)
        {
            if (c.optional) continue;
            if (c.supply)
            {
                if (asm.assembly.Supply(c.role) < c.supplyCount) list.Add(c.Name.ToLowerInvariant());
                continue;
            }
            int have = 0;
            foreach (AssemblyPart a in asm.assembly.parts) if (a.role == c.role) have++;
            if (have < c.count) list.Add(c.Name.ToLowerInvariant());
        }
        foreach (CraftJointSpec j in d.joints)
        {
            JointState s = asm.assembly.Joint(j.id);
            if (s == null || !s.done) list.Add(j.Name.ToLowerInvariant());
        }
        return list;
    }

    // ------------------------------------------------------------------ finished items
    /// <summary>
    /// The weapon, tool, shield or armour a finished assembly is. <paramref name="inspected"/>: its numbers are known
    /// (a test bench, or the final inspection was made); else they show once it is tried.
    /// </summary>
    public static ItemSO Finished(CraftRules rules, CraftItemReport report, CraftPiece assembly, List<CraftPiece> parts, string maker, List<string> procedure,
        bool inspected, CraftJournalData known)
    {
        if (rules == null || report?.Design == null)
            return null;
        CraftDesign d = report.Design;
        CraftComponentSpec main = d.MainComponent;
        CraftPiece mainPiece = null;
        if (parts != null && main != null)
            foreach (CraftPiece p in parts)
                if (p.intentRole == main.role && (mainPiece == null || p.Mass(rules) > mainPiece.Mass(rules))) mainPiece = p;
        bool knownMaterial = mainPiece == null || mainPiece.kind != CraftPieceKind.Metal || mainPiece.Profile(rules).Class == null || CraftJournal.KnowsClass(known, rules, mainPiece.Profile(rules).Class);
        var r = new CraftedItemRecord
        {
            id = Guid.NewGuid().ToString("N"), designId = d.id, name = knownMaterial ? report.Name : d.Name, maker = maker ?? "", utc = DateTime.UtcNow.Ticks,
            quality = (float)report.Quality, tier = report.Tier, material = report.MaterialWord, inspected = inspected, weight = report.Weight, price = report.Price,
            color = report.Color, damageMin = report.DamageMin, damageMax = report.DamageMax, critChance = report.CritChance, critMultiplier = report.CritMultiplier,
            knockback = report.Knockback, attackSpeed = report.AttackSpeed, toolDamage = report.ToolDamage, elementBuildup = report.ElementBuildup, element = report.Element,
            defense = report.Defense, magicDefense = report.MagicDefense, durability = Math.Max(1, report.Durability),
        };
        foreach (ElementalResistanceValue v in report.Resistances)
            r.resistances.Add(new CraftedResistance { element = v.element, percent = v.percent });
        r.notes.AddRange(report.Notes);
        r.parts.AddRange(report.Parts);
        if (procedure != null) r.procedure.AddRange(procedure);
        if (inspected) r.inspection.AddRange(InspectionLines(report));
        r.assemblyJson = assembly != null ? JsonUtility.ToJson(assembly) : "";
        if (parts != null)
            foreach (CraftPiece p in parts) r.componentJson.Add(JsonUtility.ToJson(p));
        ItemSO item = Build(rules, r);
        if (item != null)
            SaveCrafted(r);
        return item;
    }

    /// <summary>What trying a finished item tells (the final inspection): its qualities in words.</summary>
    public static List<string> InspectionLines(CraftItemReport r)
    {
        var lines = new List<string>();
        string Word(double v) => v > 0.88 ? "excellent" : v > 0.75 ? "very good" : v > 0.6 ? "good" : v > 0.4 ? "fair" : v > 0.2 ? "poor" : "bad";
        lines.Add($"Material: {Word(r.Material)}; workmanship: {Word(r.Workmanship)}; soundness: {Word(r.Integrity)}.");
        if (r.Design != null && r.Design.IsWeaponLike)
            lines.Add($"Edge: {Word(r.Edge)}; heat treatment: {Word(r.HeatTreatment)}; assembly: {Word(r.Assembly)}.");
        else
            lines.Add($"Protection: {Word(r.Protection)}; assembly: {Word(r.Assembly)}.");
        foreach (string n in r.Notes)
            if (!n.Contains(":") || n.StartsWith(r.Design?.MainComponent?.Name ?? "\u0000", StringComparison.Ordinal))
                lines.Add(CraftText.Capital(n) + ".");
        return lines;
    }

    /// <summary>Marks a finished item as inspected (its numbers are now known) and saves it.</summary>
    public static void Inspect(CraftRules rules, ICraftedItem item, CraftItemReport report)
    {
        if (item?.Record == null)
            return;
        item.Record.inspected = true;
        item.Record.inspection.Clear();
        item.Record.inspection.AddRange(InspectionLines(report));
        SaveCrafted(item.Record);
        ItemDatabase.NotifyRuntimeItemChanged(CraftedTooltip.IdPrefix + item.Record.id);
    }

    /// <summary>The item of a record (a copy of the design's template with the record's numbers).</summary>
    public static ItemSO Build(CraftRules rules, CraftedItemRecord r)
    {
        CraftDesign d = rules?.Design(r?.designId);
        if (d == null)
            return null;
        ItemSO template = rules.TemplateOf(d);
        CraftPiece main = MainPieceOf(rules, d, r);
        Sprite icon = CraftArt.ItemIcon(rules, d, main, r.color);
        string description = string.IsNullOrEmpty(d.description) ? "" : d.description;
        ItemSO result;
        if (d.IsWeaponLike)
        {
            var w = ScriptableObject.CreateInstance<ForgedWeaponSO>();
            if (template is WeaponSO tw)
                CopySerialized(tw, w, typeof(WeaponSO));
            w.SetStats(d.weaponCategory, r.damageMin, r.damageMax, r.critChance, r.critMultiplier, r.knockback, d.scaling);
            w.SetMadeStats(d.damageForm, r.attackSpeed, r.toolDamage, r.element, r.elementBuildup);
            w.SetIdentity(r, r.name, description, r.weight, r.price, r.durability, icon, d.toolType, d.gridSize);
            w.SetModelIfNone(CraftModels.ForCrafted(rules, r));
            result = w;
        }
        else
        {
            var a = ScriptableObject.CreateInstance<ForgedArmorSO>();
            if (template is ArmorSO ta)
                CopySerialized(ta, a, typeof(ArmorSO));
            var res = new List<ElementalResistance>();
            foreach (CraftedResistance x in r.resistances)
                res.Add(new ElementalResistance { element = x.element, percent = x.percent });
            a.SetArmorStats(d.armorSlot, r.defense, r.magicDefense, res);
            a.SetIdentity(r, r.name, description, r.weight, r.price, r.durability, icon, d.gridSize);
            a.SetModelIfNone(CraftModels.ForCrafted(rules, r));
            result = a;
        }
        result.hideFlags = HideFlags.DontSave;
        made[CraftedTooltip.IdPrefix + r.id] = result;
        return result;
    }

    private static CraftPiece MainPieceOf(CraftRules rules, CraftDesign d, CraftedItemRecord r)
    {
        CraftComponentSpec main = d.MainComponent;
        CraftPiece best = null;
        foreach (string j in r.componentJson)
        {
            if (string.IsNullOrEmpty(j)) continue;
            CraftPiece p = JsonUtility.FromJson<CraftPiece>(j);
            if (p != null && (main == null || p.intentRole == main.role) && (best == null || p.Mass(rules) > best.Mass(rules))) best = p;
        }
        return best;
    }

    /// <summary>
    /// Copies the serialized data of <paramref name="from"/> into <paramref name="into"/> (a subclass instance), field by
    /// field down to <paramref name="upTo"/>: a deep copy first (Instantiate), so lists and references are its own.
    /// </summary>
    private static void CopySerialized(ItemSO from, ItemSO into, Type upTo)
    {
        ItemSO copy = UnityEngine.Object.Instantiate(from);
        for (Type t = upTo; t != null && t != typeof(ScriptableObject); t = t.BaseType)
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                f.SetValue(into, f.GetValue(copy));
        if (Application.isPlaying) UnityEngine.Object.Destroy(copy);
        else UnityEngine.Object.DestroyImmediate(copy);
    }

    // ------------------------------------------------------------------ the store
    [Serializable]
    private class PieceSave
    {
        public int version = 1;
        public string key = "", name = "", description = "";
        public float weight, price;
        public int gridX = 1, gridY = 1;
        public string piece = "";
        public List<string> parts = new List<string>();
    }

    private static string Folder => Path.Combine(Application.persistentDataPath, "Crafting", "Items");

    private static string PathOf(string id)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            id = id.Replace(c, '_');
        return Path.Combine(Folder, id + ".json");
    }

    private static void SavePiece(CraftPieceItemSO item)
    {
        if (!Application.isPlaying || ClientOfSession)
            return;
        try
        {
            var s = new PieceSave
            {
                key = item.Key, name = item.Name, description = item.Description, weight = item.Weight, price = item.Price, gridX = item.GridSize.x, gridY = item.GridSize.y,
                piece = item.PieceJson, parts = new List<string>(item.ComponentJson),
            };
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathOf(item.RuntimeId), JsonUtility.ToJson(s));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Crafting] Could not save {item.Name}: {e.Message}");
        }
    }

    private static void SaveCrafted(CraftedItemRecord r)
    {
        if (!Application.isPlaying || r == null || ClientOfSession)
            return;
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathOf(CraftedTooltip.IdPrefix + r.id), r.ToJson());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Crafting] Could not save {r.name}: {e.Message}");
        }
    }

    /// <summary>A piece went back onto a station (or was destroyed): its item and file are no longer needed.</summary>
    public static void Forget(ItemSO item)
    {
        if (!(item is IRuntimeItem r) || !(item is CraftPieceItemSO))
            return;
        made.Remove(r.RuntimeId);
        CraftModels.Forget(r.RuntimeId);
        if (ClientOfSession)
            return;
        try
        {
            string path = PathOf(r.RuntimeId);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception) { }
    }

    /// <summary>
    /// The item of a runtime id: runtime stock is made from the rules; a piece or a finished item comes from this
    /// session or (offline, on the server) from disk. Null when unknown here (a client then asks the server).
    /// </summary>
    public static ItemSO Resolve(string id)
    {
        if (!IsCraftId(id))
            return null;
        if (id.StartsWith(CraftStockItemSO.IdPrefix, StringComparison.Ordinal))
            return StockOf(id);
        if (made.TryGetValue(id, out ItemSO cached) && cached != null)
            return cached;
        if (ClientOfSession)
            return null;
        try
        {
            string path = PathOf(id);
            if (!File.Exists(path))
                return null;
            string json = File.ReadAllText(path);
            return id.StartsWith(CraftedTooltip.IdPrefix, StringComparison.Ordinal) ? Build(CraftDatabase.Instance.Rules, CraftedItemRecord.FromJson(json)) : FromPieceSave(id, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Crafting] Could not load the item {id}: {e.Message}");
            return null;
        }
    }

    private static CraftStockItemSO StockOf(string id)
    {
        string rest = id.Substring(CraftStockItemSO.IdPrefix.Length);
        int colon = rest.IndexOf(':');
        if (colon <= 0 || !Enum.TryParse(rest.Substring(0, colon), true, out CraftStockKind kind))
            return null;
        return Stock(CraftDatabase.Instance.Rules, kind, rest.Substring(colon + 1));
    }

    /// <summary>A worked piece's item from its saved text (from disk, or from the server).</summary>
    private static ItemSO FromPieceSave(string id, string json)
    {
        PieceSave s = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PieceSave>(json);
        if (s == null || CraftPieceItemSO.IdPrefix + s.key != id)
            return null;
        CraftRules rules = CraftDatabase.Instance.Rules;
        var item = ScriptableObject.CreateInstance<CraftPieceItemSO>();
        item.hideFlags = HideFlags.DontSave;
        CraftPiece p = JsonUtility.FromJson<CraftPiece>(s.piece);
        var parts = new List<CraftPiece>();
        foreach (string j in s.parts) if (!string.IsNullOrEmpty(j)) parts.Add(JsonUtility.FromJson<CraftPiece>(j));
        item.SetupFromJson(s.key, s.piece, s.parts, s.name, s.description, s.weight, s.price, p != null ? CraftArt.PieceIcon(rules, p, parts) : null, new Vector2Int(s.gridX, s.gridY));
        if (p != null)
            item.SetModel(CraftModels.ForPiece(rules, id, p, parts));
        made[id] = item;
        return item;
    }

    // ------------------------------------------------------------------ the network
    /// <summary>
    /// The definition the server sends a client for <paramref name="id"/>: a worked piece's saved text, or a finished
    /// item's record. Null for runtime stock (every machine makes it) and for items this machine does not know.
    /// </summary>
    public static string Export(string id)
    {
        if (!IsCraftId(id) || id.StartsWith(CraftStockItemSO.IdPrefix, StringComparison.Ordinal))
            return null;
        ItemSO item = Resolve(id);
        if (item is ICraftedItem crafted && crafted.Record != null)
            return crafted.Record.ToJson();
        if (item is CraftPieceItemSO piece)
            return JsonUtility.ToJson(new PieceSave
            {
                key = piece.Key, name = piece.Name, description = piece.Description, weight = piece.Weight, price = piece.Price,
                gridX = piece.GridSize.x, gridY = piece.GridSize.y, piece = piece.PieceJson, parts = new List<string>(piece.ComponentJson),
            });
        return null;
    }

    /// <summary>
    /// An item from the server's definition (never saved here). A finished item this machine already shows takes the
    /// new record in place (it was tried), so the slots holding it update.
    /// </summary>
    public static ItemSO Import(string id, string definition)
    {
        if (!IsCraftId(id) || string.IsNullOrEmpty(definition))
            return null;
        try
        {
            if (id.StartsWith(CraftedTooltip.IdPrefix, StringComparison.Ordinal))
            {
                CraftedItemRecord r = CraftedItemRecord.FromJson(definition);
                if (r == null || CraftedTooltip.IdPrefix + r.id != id)
                    return null;
                if (made.TryGetValue(id, out ItemSO existing) && existing != null)
                {
                    if (existing is ForgedWeaponSO w) w.ReplaceRecord(r);
                    else if (existing is ForgedArmorSO a) a.ReplaceRecord(r);
                    return existing;
                }
                return Build(CraftDatabase.Instance.Rules, r);
            }
            if (id.StartsWith(CraftPieceItemSO.IdPrefix, StringComparison.Ordinal))
                return made.TryGetValue(id, out ItemSO known) && known != null ? known : FromPieceSave(id, definition);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Crafting] The server's definition of {id} could not be read: {e.Message}");
        }
        return null;
    }
}
