using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>The measuring tools a station has (and the test station's full readout). Serialized as numbers: add at the END.</summary>
[Flags]
public enum CraftInstruments
{
    None = 0,
    /// <summary>A pyrometer / heat gauge: the temperature in degrees.</summary>
    Thermometer = 1 << 0,
    /// <summary>Calipers and a rule: dimensions in mm.</summary>
    Calipers = 1 << 1,
    /// <summary>A balance: mass in grams.</summary>
    Scale = 1 << 2,
    /// <summary>A loupe: hairline cracks, inclusions, the grain on a fracture.</summary>
    Loupe = 1 << 3,
    /// <summary>A magnet on a rod.</summary>
    Magnet = 1 << 4,
    /// <summary>A test file: roughly how hard it is.</summary>
    TestFile = 1 << 5,
    /// <summary>An assayer's kit (touchstone, acids, a balance): what a metal is made of.</summary>
    Assay = 1 << 6,
    /// <summary>An unglazed streak plate (ores).</summary>
    StreakPlate = 1 << 7,
    /// <summary>A refractometer: a gem's refractive index (it tells one stone from another).</summary>
    Refractometer = 1 << 8,
    /// <summary>Hardness points (Mohs 5 to 9) and a hydrostatic balance (specific gravity): gem tests.</summary>
    HardnessPoints = 1 << 9,
    /// <summary>A pin moisture meter for wood.</summary>
    MoistureMeter = 1 << 10,
    /// <summary>A dichroscope: does a gem's colour change with direction (and which way its axis lies)?</summary>
    Dichroscope = 1 << 11,
    All = Thermometer | Calipers | Scale | Loupe | Magnet | TestFile | Assay | StreakPlate | Refractometer | HardnessPoints | MoistureMeter | Dichroscope,
    /// <summary>The gem tester's kit: loupe, balance, refractometer, hardness points, dichroscope.</summary>
    GemKit = Loupe | Scale | Refractometer | HardnessPoints | Dichroscope,
}

/// <summary>
/// What a piece, a fire, a furnace, a crucible or a vat looks like, sounds like and feels like - read from its real
/// state, in words that describe and never advise. Metals are named only when the player knows them; temperatures are
/// colours unless there is a thermometer; dimensions and mass are "about" unless measured; a hairline crack only shows
/// under a loupe; a file says roughly how hard; a magnet stops pulling at the hardening heat. The test station (reveal)
/// adds the numbers.
/// </summary>
public static class CraftLook
{
    // ------------------------------------------------------------------ names
    /// <summary>What the player calls a metal: its name when known, else what it looks like.</summary>
    public static string MetalName(CraftRules rules, CraftAlloyProfile p, CraftJournalData d)
    {
        if (p == null)
            return "metal";
        if (p.Class != null && CraftJournal.KnowsClass(d, rules, p.Class))
            return p.Class.Name.ToLowerInvariant();
        if (p.Class != null && !string.IsNullOrEmpty(p.Class.observation))
            return p.Class.observation;
        return p.BaseElement != null && !string.IsNullOrEmpty(p.BaseElement.observation) ? p.BaseElement.observation : "an unknown metal";
    }

    /// <summary>A short label for a piece ("Steel bar", "Bloom", "Blade of grey metal", "Tanned deer hide").</summary>
    public static string Name(CraftRules rules, CraftPiece p, CraftJournalData d)
    {
        if (p == null)
            return "";
        switch (p.kind)
        {
            case CraftPieceKind.Ore:
            {
                CraftOreDefinition o = rules.Ore(p.ore.ore);
                string n = o == null ? "Ore" : CraftJournal.KnowsOre(d, o.id) ? o.Name : CraftText.Capital(Unnamed(o.observation));
                return p.ore.fragments.Count > 1 ? $"{n} (broken)" : n;
            }
            case CraftPieceKind.Hide:
            {
                CraftHideDefinition h = rules.Hide(p.hide.hide);
                CraftPattern pat = rules.Pattern(p.hide.pattern);
                if (h != null && h.textile)
                    return pat != null ? $"{pat.Name} ({h.Name.ToLowerInvariant()}{(CraftTextiles.OnGrain(p.hide) ? "" : ", across the grain")})" : $"A length of {h.Name.ToLowerInvariant()}";
                string animal = h != null ? h.Name.Replace(" hide", "").Replace(" pelt", "").ToLowerInvariant() : "";
                double tan = CraftLeather.MeanTannage(p.hide);
                string what = tan > 0.6 ? $"{animal} leather" : tan > 0.2 ? $"half-tanned {animal} hide" : $"raw {animal} hide";
                if (pat != null) return $"{pat.Name} ({what})";
                return CraftText.Capital(what.Replace("  ", " ").Trim());
            }
            case CraftPieceKind.Wood:
            {
                CraftWoodDefinition w = rules.Wood(p.wood.wood);
                CraftComponentSpec spec = rules.Design(p.intentDesign)?.Component(p.intentRole);
                // a plank wood's name says plank ("Linden plank"): its log is a "Linden log", its blank a "Linden plank"
                string species = w?.Name ?? "Wood";
                bool plank = species.EndsWith(" plank", StringComparison.OrdinalIgnoreCase);
                if (plank) species = species.Substring(0, species.Length - " plank".Length);
                if (spec == null && p.wood.log) return $"{species} log{(p.wood.bark > 0.3 ? "" : ", debarked")}";
                return $"{species} {(spec != null ? spec.Name.ToLowerInvariant() : plank ? "plank" : "blank")}";
            }
            case CraftPieceKind.Fibre:
            {
                string f = rules.Supply(p.fibre.fibre)?.Name ?? "fibre";
                FibreState s = p.fibre;
                string state = s.felted > 0.4 ? "felted" : s.combed ? "combed top of" : s.carded ? "carded" : s.scoured ? "scoured" : s.cleaned ? "picked" : "raw";
                return CraftText.Capital($"{state} {f.ToLowerInvariant()}{(s.wet > 0.4 ? ", wet" : "")}");
            }
            case CraftPieceKind.Yarn:
            {
                string f = (rules.Supply(p.yarn.fibre)?.Name ?? "fibre").ToLowerInvariant().Replace("raw ", "");
                YarnState y = p.yarn;
                string ply = y.plies > 1 ? $"{y.plies}-ply " : "";
                return CraftText.Capital($"{ply}{(y.worsted ? "worsted" : "yarn")} of {f}{(y.measured ? ", in a skein" : ", on a bobbin")}");
            }
            case CraftPieceKind.Gem:
            {
                GemState g = p.gem;
                CraftGemDefinition def = rules.Gem(g.gem);
                CraftGemDefinition named = rules.Gem(g.identifiedAs);
                // What the player calls it: what they named it (right or wrong), what they know by sight, or what it looks like.
                string n = named != null ? named.Name : def != null && CraftJournal.KnowsGem(d, def.id) ? def.Name : def != null ? CraftText.Capital(Unnamed(def.observation)) : "Stone";
                CraftGemCut cut = rules.Cut(g.cut);
                switch (g.stage)
                {
                    case CraftGemStage.Rough: return $"{n} (rough)";
                    case CraftGemStage.Preformed: return $"{n} (preform)";
                    case CraftGemStage.Faceted: return $"{n}, {cut?.Name.ToLowerInvariant() ?? "cut"} (unpolished)";
                    default: return $"{n}, {cut?.Name.ToLowerInvariant() ?? "cut"}";
                }
            }
            case CraftPieceKind.Fasteners:
            {
                string kind = p.fasteners.kind == CraftFastenerKind.Buckle ? "buckle" : p.fasteners.kind == CraftFastenerKind.Stud ? "stud" : "rivet";
                return $"{p.fasteners.count} {kind}{(p.fasteners.count == 1 ? "" : "s")} of {Short(MetalName(rules, p.Profile(rules), d))}";
            }
            case CraftPieceKind.Assembly:
            {
                CraftDesign des = rules.Design(p.assembly.design);
                return p.assembly.finished ? des?.Name ?? "Item" : $"{des?.Name ?? "Item"} (unfinished)";
            }
            default:
            {
                CraftAlloyProfile prof = p.Profile(rules);
                string metal = MetalName(rules, prof, d);
                bool known = prof.Class != null && CraftJournal.KnowsClass(d, rules, prof.Class);
                string shape = ShapeWord(rules, p);
                return known ? CraftText.Capital($"{metal} {shape}") : CraftText.Capital($"{shape} of {Short(metal)}");
            }
        }
    }

    /// <summary>What the player calls a stone (a finished jewel's): the name they gave it, its name if they know it by sight, else what it looks like.</summary>
    public static string StoneName(CraftRules rules, CraftPiece p, CraftJournalData d)
    {
        if (p == null || p.kind != CraftPieceKind.Gem)
            return "stone";
        CraftGemDefinition def = rules.Gem(p.gem.gem), named = rules.Gem(p.gem.identifiedAs);
        return named != null ? named.Name : def != null && CraftJournal.KnowsGem(d, def.id) ? def.Name : def != null ? Unnamed(def.observation) : "stone";
    }

    /// <summary>An observation without its article and clauses ("a heavy, rust-red rock that ..." → "heavy, rust-red rock").</summary>
    private static string Unnamed(string observation)
    {
        if (string.IsNullOrEmpty(observation)) return "unknown stone";
        string s = observation.StartsWith("a ") ? observation.Substring(2) : observation.StartsWith("an ") ? observation.Substring(3) : observation;
        int that = s.IndexOf(" that ", StringComparison.Ordinal);
        if (that > 0) s = s.Substring(0, that);
        int with = s.IndexOf(" with ", StringComparison.Ordinal);
        if (with > 0) s = s.Substring(0, with);
        int semi = s.IndexOf(';');
        return semi > 0 ? s.Substring(0, semi) : s;
    }

    /// <summary>
    /// A metal's observation as a name ("a soft, bright metal that crackles when bent" → "soft, bright metal"): without its
    /// article, cut at a clause ("that ...") or at the first comma past its noun - the comma between the adjectives before
    /// the noun stays, so an unknown tin bar is a "Bar of soft, bright metal", never a "Bar of soft".
    /// </summary>
    private static string Short(string observation)
    {
        if (string.IsNullOrEmpty(observation)) return "metal";
        string s = observation.StartsWith("a ") ? observation.Substring(2) : observation.StartsWith("an ") ? observation.Substring(3) : observation;
        int that = s.IndexOf(" that ", StringComparison.Ordinal);
        if (that > 0) s = s.Substring(0, that);
        int noun = NounEnd(s);
        int cut = noun < s.Length ? s.IndexOfAny(new[] { ',', ';' }, noun) : -1;
        if (cut > 0) s = s.Substring(0, cut);
        return s.Trim();
    }

    /// <summary>The words that end the noun phrase of a metal's observation ("metal", "iron", "lump"...).</summary>
    private static readonly string[] MetalNouns = { "metal", "iron", "steel", "bronze", "brass", "gold", "silver", "copper", "tin", "lead", "alloy", "lump", "rock" };

    /// <summary>Where the first noun of an observation ends (its length when it names none: then no comma is cut at).</summary>
    private static int NounEnd(string s)
    {
        int start = 0;
        foreach (string word in s.Split(' '))
        {
            string w = word.TrimEnd(',', ';', '.');
            if (Array.IndexOf(MetalNouns, w) >= 0)
                return start + w.Length;
            start += word.Length + 1; // the word and the space after it
        }
        return s.Length;
    }

    /// <summary>
    /// "bar", "bloom", "ingot", "plate", "blade", "rod" - by its intent and its shape: what it is meant for first (a bloom
    /// forged into a dagger's blade is the blade), then a bloom while it is one (not yet hammered sound:
    /// <see cref="CraftForging.Consolidated"/>).
    /// </summary>
    public static string ShapeWord(CraftRules rules, CraftPiece p)
    {
        CraftComponentSpec spec = rules.Design(p.intentDesign)?.Component(p.intentRole);
        if (spec != null) return spec.Name.ToLowerInvariant();
        if (Bloom(rules, p)) return "bloom";
        if (p.segments.Count == 0) return "piece";
        double l = p.Length, w = 0, t = 0;
        foreach (MetalSegment s in p.segments) { w += s.width; t += s.thickness; }
        w /= p.segments.Count;
        t /= p.segments.Count;
        bool beveled = false;
        foreach (MetalSegment s in p.segments) beveled |= s.Beveled;
        if (beveled) return "blade";
        if (w > 80 && t < 5) return "plate";
        if (p.cast && l < 160 && w > 25) return "ingot";
        if (Math.Max(w, t) < 9) return "rod";
        return "bar";
    }

    /// <summary>Is it a bloom still - drawn from the furnace and not yet hammered sound (<see cref="CraftForging.Consolidated"/>)?</summary>
    public static bool Bloom(CraftRules rules, CraftPiece p) => p != null && p.bloom && !CraftForging.Consolidated(rules, p);

    // ------------------------------------------------------------------ pieces
    /// <summary>What can be seen and felt of a piece (no numbers unless an instrument or <paramref name="reveal"/>).</summary>
    public static List<string> Describe(CraftRules rules, CraftPiece p, CraftJournalData d, CraftInstruments tools, bool dark, bool reveal, CraftWorkshop ws = null)
    {
        var lines = new List<string>();
        if (p == null)
            return lines;
        if (reveal) tools = CraftInstruments.All;
        switch (p.kind)
        {
            case CraftPieceKind.Ore: Ore(rules, p, d, tools, lines); break;
            case CraftPieceKind.Hide: Hide(rules, p, d, lines); break;
            case CraftPieceKind.Wood: Wood(rules, p, lines); break;
            case CraftPieceKind.Assembly: break;
            case CraftPieceKind.Fibre: Fibre(rules, p, lines); break;
            case CraftPieceKind.Yarn: Yarn(rules, p, tools, lines); break;
            case CraftPieceKind.Gem: Gem(rules, p, d, tools, lines); break;
            default: Metal(rules, p, d, tools, dark, lines); break;
        }
        Measured(rules, p, tools, lines, ws);
        // Flaws, one line per kind: the worst of them, and where.
        var seen = new List<CraftDefectKind>();
        foreach (CraftDefect def in p.defects)
        {
            if (seen.Contains(def.kind)) continue;
            CraftDefect worst = null;
            int count = 0, first = int.MaxValue, last = -1;
            foreach (CraftDefect o in p.defects)
            {
                if (o.kind != def.kind) continue;
                bool shows = o.surface && o.severity > 0.25 || (tools & CraftInstruments.Loupe) != 0 && o.severity > 0.05 && Visible(o.kind);
                if (!shows && !reveal) continue;
                count++;
                if (worst == null || o.severity > worst.severity) worst = o;
                if (o.at >= 0) { first = Math.Min(first, o.at); last = Math.Max(last, o.at); }
            }
            if (worst == null) continue;
            seen.Add(def.kind);
            string where = count > 1 && last >= 0 && p.segments.Count > 0 ? Spread(p, first, last) : worst.at >= 0 && p.segments.Count > 0 ? Where(p, Math.Min(worst.at, p.segments.Count - 1)) : "";
            lines.Add(DefectWords(worst, where) + (reveal ? $" <color=#9aa0a6>({worst.cause}; worst {worst.severity:0.00}{(count > 1 ? $", {count} places" : "")})</color>" : ""));
        }
        if (reveal)
            lines.AddRange(Readout(rules, p));
        return lines;
    }

    /// <summary>
    /// The share of the piece's length that is bare from an abrasive (no scale, ground or polished:
    /// <see cref="CraftSurface.GroundPolish"/> and up), and those stretches' scratches (µm) and polish, by length.
    /// </summary>
    private static double BareSurface(CraftPiece p, out double rough, out double polish)
    {
        double bare = 0, all = 0, sum = 0, shine = 0;
        foreach (MetalSegment s in p.segments)
        {
            all += s.length;
            if (s.scale > 0.02 || s.polish < CraftSurface.GroundPolish - 1e-6) continue;
            bare += s.length;
            sum += s.rough * s.length;
            shine += s.polish * s.length;
        }
        rough = bare > 0 ? sum / bare : 0;
        polish = bare > 0 ? shine / bare : 0;
        return all > 0 ? bare / all : 0;
    }

    /// <summary>Hidden flaws a loupe finds on the surface (a raw core only shows on a cut edge; pores under the skin never).</summary>
    private static bool Visible(CraftDefectKind k) => k != CraftDefectKind.CaseHardened && k != CraftDefectKind.Porosity;

    private static void Metal(CraftRules rules, CraftPiece p, CraftJournalData d, CraftInstruments tools, bool dark, List<string> lines)
    {
        CraftAlloyProfile prof = p.Profile(rules);
        CraftPieceReport r = CraftEvaluator.Piece(rules, p);
        string metal = MetalName(rules, prof, d);
        if (Bloom(rules, p))
            lines.Add(r.Porosity > 0.1 ? "A spongy, cindery lump, full of holes and dark slag." : "A rough, lumpy bar with streaks of slag in it.");
        else if (p.cast && r.Roughness > 40)
            lines.Add("A rough casting, still marked by the mould.");
        // known by name, the metal's name; unknown, what it looks like ("It is a soft, bright metal." - what its sparks or its
        // sound tell, the observation's clause, waits for the wheel or the hammer)
        lines.Add($"It is {(prof.Class != null && CraftJournal.KnowsClass(d, rules, prof.Class) ? metal : CraftText.A(Short(metal)) + " " + Short(metal))}.");
        // Heat: the glow, by region.
        Glow(rules, p, d, (tools & CraftInstruments.Thermometer) != 0, dark, lines);
        // The surface: scale; else, where most of it is bare from an abrasive, its scratches in the Grind area's own words
        // (CraftSurface.FinishOf - brighter where a buff brightened it); else forged or cast grey.
        double bare = BareSurface(p, out double bareRough, out double barePolish);
        CraftSurface.Finish finish = CraftSurface.FinishOf(bareRough, barePolish);
        bool ground = r.Scale <= 0.02 && bare >= 0.5;
        if (r.Scale > 0.08) lines.Add("A thick black scale covers it, flaking in places.");
        else if (r.Scale > 0.02) lines.Add("It is dark with scale.");
        else if (ground && finish == CraftSurface.Finish.Mirror) lines.Add("It is polished bright as a mirror.");
        else if (ground && finish == CraftSurface.Finish.Bright) lines.Add("It is polished bright.");
        else if (ground && finish == CraftSurface.Finish.SmoothGrey) lines.Add("It has a smooth grey finish.");
        else if (ground && finish == CraftSurface.Finish.FineScratches) lines.Add("It is ground clean and grey.");
        else if (r.Temperature < 300) lines.Add(ground ? "It is ground grey and dull." : "It is grey and dull.");
        if (ground && finish == CraftSurface.Finish.CoarseScratches) lines.Add("Coarse scratches run across it.");
        else if (ground && finish == CraftSurface.Finish.FineScratches) lines.Add("Fine scratches run across it.");
        else if (r.Scale < 0.05 && CraftSurface.FinishOf(ground ? bareRough : r.Roughness) == CraftSurface.Finish.Rough)
            lines.Add(p.cast && !Did(p, CraftOpKind.Strike) ? "Its surface is rough and sandy from the mould." : "Its surface is rough with hammer marks.");
        if (r.Streaks > CraftSmelting.Streaky)
            lines.Add("Streaks of a different colour run through the metal: it was cast from a melt not stirred even, and it is weak where they meet.");
        double film = 0;
        foreach (MetalSegment s in p.segments) if (s.polish >= CraftSurface.FilmPolish - 1e-9) film = Math.Max(film, s.temperFilm); // bare steel, as it is drawn
        string tc = CraftMetallurgy.TemperName(film);
        if (!string.IsNullOrEmpty(tc) && prof.Steel) lines.Add($"The bright steel has taken {tc} colours.");
        if (p.Tag("etched") > 0.2 && p.contrast > 0.15) lines.Add("Etched, it shows a pattern of light and dark layers.");
        if (p.Tag("etched") > 0.2 && HamonLike(p)) lines.Add("Etched, a misty line runs along it where the hard edge meets the softer spine.");
        if (r.Coating > 0.3) lines.Add("It has a thin protective coat.");
        Smithing(p, tools, lines);
        // Shape.
        double bend = Math.Abs(r.Bend);
        CraftShapeTarget target = rules.Design(p.intentDesign)?.Component(p.intentRole)?.shape;
        double want = target?.curvature ?? 0;
        if (Math.Abs(r.Bend - want) > 3) lines.Add(Math.Abs(want) > 1 ? "Its curve is not what it should be." : $"It is {(bend > 12 ? "badly bent" : "slightly bent")}.");
        CraftComponentSpec part = rules.Design(p.intentDesign)?.Component(p.intentRole);
        if (target != null && target.profile != null && target.profile.Count >= 2)
        {
            var notes = new List<string>();
            double shape = CraftEvaluator.ShapeScore(rules, p, target, notes);
            string role = part.Name.ToLowerInvariant();
            lines.Add(shape > 0.85 ? $"It has the shape of {CraftText.A(role)} {role}." : shape > 0.55 ? $"It is roughly the shape of {CraftText.A(role)} {role}." : $"It is still far from the shape of {CraftText.A(role)} {role}.");
            if ((tools & CraftInstruments.Calipers) != 0)
                foreach (string n in notes) lines.Add($"Against the pattern: {n}.");
        }
        else if (Uneven(p) > 0.25 && !p.cast)
            lines.Add("It is uneven: thick in places, thin in others.");
        if (r.Dish > 3) lines.Add(r.Dish > 30 ? "It is deeply dished." : "It is dished.");
        if (r.Eye > 0) lines.Add(p.cast ? "A hole runs through it." : "It has a hole punched through it.");
        // The edge.
        if (r.HasEdge)
        {
            // By the stone's own ladder (CraftSurface.KeenStep) and its burr (BurrShows: where the stone draws one).
            switch (CraftSurface.KeenStep(r.Sharpness))
            {
                case 0: lines.Add("The edge is blunt: light glints along it."); break;
                case 1: lines.Add("The edge is sharp enough to cut, not keen."); break;
                case 2: lines.Add("The edge is keen."); break;
                default: lines.Add("The edge is razor-keen: it catches on a fingernail and shows no line of light."); break;
            }
            double burr = 0;
            foreach (MetalSegment s in p.segments) if (CraftSurface.HasEdge(s)) burr = Math.Max(burr, s.burr);
            if (burr > CraftSurface.BurrShows) lines.Add("A wire burr runs along one side of the edge.");
            if (r.Chip > 0.15) lines.Add("The edge is nicked and chipped.");
            if (r.Symmetry < 0.7) lines.Add("The bevels do not match from side to side.");
        }
        // In the hand.
        if (prof.Density > 10) lines.Add("It is very heavy for its size.");
        else if (prof.Density < 4) lines.Add("It is strangely light for its size.");
        if (prof.Magic > 0.5) lines.Add("It hums faintly in the hand.");
        if (prof.Supernatural > 0.5) lines.Add("Its shimmer seems to deepen in shadow.");
        if (prof.Affinity == ElementType.Fire && prof.AffinityStrength > 0.3) lines.Add("It is warm to the touch, even cold.");
        foreach (CraftVariable v in p.tags)
            if (v.name.StartsWith("affinity.", StringComparison.Ordinal) && v.value > 0.05)
                lines.Add(v.name.EndsWith("Fire") ? "Embers seem to drift deep in the steel." : v.name.EndsWith("Ice") ? "A faint frost clings to it." : "Something of what it was quenched in lingers in it.");
        // Tests.
        if ((tools & CraftInstruments.Magnet) != 0)
            lines.Add(Magnet(rules, p));
        if ((tools & CraftInstruments.TestFile) != 0 && r.Temperature < 300)
            lines.Add(FileTest(r.EdgeHardness > 0 ? Math.Max(r.EdgeHardness, r.BodyHardness) : r.BodyHardness));
    }

    /// <summary>What the smith put on it and the fire left: clay on the spine, flux, a weld's seam not yet set, one face hotter.</summary>
    private static void Smithing(CraftPiece p, CraftInstruments tools, List<string> lines)
    {
        int n = p.segments.Count, clayed = 0, fluxed = 0;
        double clay = 0;
        foreach (MetalSegment s in p.segments)
        {
            if (s.clay > 0.05) { clayed++; clay += s.clay; }
            if (s.flux > 0.05) fluxed++;
        }
        if (clayed > 0)
        {
            clay /= clayed;
            string coat = (tools & CraftInstruments.Calipers) != 0 ? $"a coat of clay {clay:0.#} mm thick" : clay < 1 ? "a thin wash of clay" : clay < 3 ? "a coat of clay" : "a thick coat of clay";
            lines.Add($"{CraftText.Capital(coat)} is caked {(clayed * 3 >= n * 2 ? "along its spine" : "on part of its spine")}: a wire brush or the wheel takes it off.");
        }
        if (fluxed > 0)
            lines.Add(fluxed * 4 >= n * 3 ? "A glassy skin of molten flux covers it." : "A glassy skin of flux covers part of it; the rest is bare.");
        else if (p.MaxTemperature > 600 && Did(p, CraftOpKind.AddFlux))
            lines.Add("The flux on it has burnt off.");
        double open = CraftForging.SeamOpen(p);
        if (open > 0)
            lines.Add(open > 0.6 ? "A seam runs along it, not yet welded: strike it shut at welding heat." : "Its seam is not welded shut in places yet: strike them at welding heat.");
        // faceBias is face 1's lead over face 0's: the face down in the coals is face 1 while face 0 is up.
        if (p.location == CraftLocation.Fire && Math.Abs(p.faceBias) > 0.15 && (p.faceBias > 0) == (p.faceUp == 0))
            lines.Add("The face down in the coals glows brighter than the face up: turn it.");
    }

    private static bool HamonLike(CraftPiece p)
    {
        foreach (MetalSegment s in p.segments)
            if (s.edge.mart > 0.5 && s.body.mart < 0.3) return true;
        return false;
    }

    /// <summary>How uneven the thickness is from one segment to the next (0 even).</summary>
    private static double Uneven(CraftPiece p)
    {
        double sum = 0;
        int n = 0;
        for (int i = 1; i < p.segments.Count; i++)
        {
            double a = p.segments[i - 1].thickness, b = p.segments[i].thickness;
            sum += Math.Abs(a - b) / Math.Max(0.5, (a + b) * 0.5);
            n++;
        }
        return n > 0 ? sum / n * 4 : 0;
    }

    private static void Glow(CraftRules rules, CraftPiece p, CraftJournalData d, bool thermometer, bool dark, List<string> lines)
    {
        if (p.segments.Count == 0)
            return;
        double hot = p.MaxTemperature, cold = double.MaxValue;
        int hottest = 0;
        for (int i = 0; i < p.segments.Count; i++)
        {
            // by the same measure as the hottest heat: the hotter of a stretch's zones (in the fire the thin edge runs ahead)
            double t = Math.Max(p.segments[i].edge.temp, p.segments[i].body.temp);
            if (t >= hot - 1e-9) hottest = i;
            cold = Math.Min(cold, t);
        }
        string colour = CraftMetallurgy.HeatName(hot, dark);
        if (string.IsNullOrEmpty(colour))
        {
            if (hot > 60) lines.Add(thermometer ? $"It is hot to hold: {CraftText.Celsius(hot)}." : hot > 150 ? "It is too hot to touch." : "It is warm.");
            return;
        }
        string where = p.segments.Count > 2 && hot - cold > 150 ? Where(p, hottest) : "";
        string gauge = thermometer ? $" - the gauge reads {CraftText.Celsius(hot)}" : "";
        if (colour == "black heat")
        {
            lines.Add($"It is at a black heat{where}: it does not glow, but spit sizzles off it{gauge}.");
            return;
        }
        string words = CraftJournal.HeatWords(d, rules, colour);
        lines.Add($"It glows {words}{where}{gauge}.");
        if (hot - cold > 250 && p.segments.Count > 2)
        {
            string rest = CraftMetallurgy.HeatName(cold, dark);
            lines.Add(string.IsNullOrEmpty(rest) || rest == "black heat" ? "The rest of it is dark." : $"The rest of it glows {rest}.");
        }
        if (hot > p.Profile(rules).BurnTemperature - 30)
            lines.Add("Bright sparks crackle off it.");
    }

    private static string Where(CraftPiece p, int i)
    {
        double t = p.PositionOf(i);
        return t < 0.2 ? " at one end" : t > 0.8 ? " at the far end" : " in the middle";
    }

    public static string Magnet(CraftRules rules, CraftPiece p)
    {
        CraftAlloyProfile prof = p.Profile(rules);
        if (!prof.Magnetic && rules.IndexOf("nickel") >= 0 && prof.F(rules.IndexOf("nickel")) < 0.3)
            return "The magnet does not pull at it at all.";
        return p.MaxTemperature > prof.Curie ? "The magnet no longer sticks to it." : "The magnet snaps onto it.";
    }

    public static string FileTest(double hv) =>
        hv > 620 ? "The test file skates over it without biting." :
        hv > 450 ? "The test file barely bites." :
        hv > 250 ? "The test file bites with effort." :
        hv > 120 ? "The test file bites easily." : "The test file cuts into it like into lead.";

    /// <summary>How a blow sounds (from the Struck event's id).</summary>
    public static string StrikeSound(string id) => id switch
    {
        "ring" => "It rings clearly under the hammer.",
        "thud" => "The hot metal takes the blow with a dull thud.",
        "dead" => "It gives a flat, dead sound.",
        "crack" => "Something tears with the blow.",
        _ => "",
    };

    /// <summary>
    /// What the instruments read (calipers, the balance, the moisture meter); an item being assembled is weighed with the
    /// parts fitted to it (<paramref name="ws"/>: the workshop it is in - without it, it is not weighed).
    /// </summary>
    private static void Measured(CraftRules rules, CraftPiece p, CraftInstruments tools, List<string> lines, CraftWorkshop ws = null)
    {
        var sb = new StringBuilder();
        if ((tools & CraftInstruments.Calipers) != 0 && p.segments.Count > 0)
        {
            double w = 0, t = 0, l = 0;
            foreach (MetalSegment s in p.segments) { w += s.width * s.length; t += s.thickness * s.length; l += s.length; }
            sb.Append($"Calipers: {CraftText.Length(p.Length)} long, {CraftText.Length(w / l)} wide, {CraftText.Length(t / l)} thick. ");
        }
        if ((tools & CraftInstruments.Scale) != 0 && p.kind == CraftPieceKind.Assembly)
        {
            if (ws != null)
                sb.Append($"Balance: {CraftText.Mass(AssembledMass(rules, ws, p))} with its parts. ");
        }
        else if ((tools & CraftInstruments.Scale) != 0)
            sb.Append(p.kind == CraftPieceKind.Gem ? $"Balance: {p.gem.carats:0.00} ct. " : $"Balance: {CraftText.Mass(p.Mass(rules))}. ");
        if ((tools & CraftInstruments.MoistureMeter) != 0 && p.kind == CraftPieceKind.Wood)
            sb.Append($"Moisture meter: {p.wood.moisture * 100:0} %. ");
        if ((tools & CraftInstruments.Calipers) != 0 && p.kind == CraftPieceKind.Gem && p.gem.girdle > 0)
            sb.Append($"Girdle: {p.gem.girdle:0.00} mm. ");
        if ((tools & CraftInstruments.Calipers) != 0 && p.kind == CraftPieceKind.Hide)
        {
            // a hide or a cut part: how thick it is, and how much that varies over it
            double lo = double.MaxValue, hi = 0, sum = 0;
            int n = 0;
            foreach (HideCell c in p.hide.cells)
                if (c.present)
                {
                    double t = CraftLeather.CellThickness(c);
                    lo = Math.Min(lo, t);
                    hi = Math.Max(hi, t);
                    sum += t;
                    n++;
                }
            if (n > 0)
                sb.Append(hi - lo < 0.15 ? $"Calipers: {sum / n:0.0} mm thick. " : $"Calipers: {sum / n:0.0} mm thick ({lo:0.0}-{hi:0.0} mm). ");
        }
        if (sb.Length > 0)
            lines.Add(sb.ToString().Trim());
        else if (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Wood)
            lines.Add($"About {Rough(p.Length)} long; it weighs about {RoughMass(p.Mass(rules))}.");
    }

    /// <summary>
    /// What an item being assembled weighs: its parts (and theirs, an assembly fitted to it) and the supplies that went into
    /// it (<see cref="CraftAssembly.SuppliesMass"/>), as the balance reads them - what the finished item will weigh.
    /// </summary>
    public static double AssembledMass(CraftRules rules, CraftWorkshop ws, CraftPiece asm, int depth = 0)
    {
        double m = 0;
        if (ws == null || asm == null || depth > 8)
            return m;
        foreach (CraftPiece c in ws.Components(asm))
            m += c.kind == CraftPieceKind.Assembly ? AssembledMass(rules, ws, c, depth + 1) : c.Mass(rules);
        // and what went into it besides its parts: the batting quilted in, the thread in its seams
        return m + CraftAssembly.SuppliesMass(rules, asm);
    }

    private static string Rough(double mm) => mm >= 1000 ? $"{Math.Round(mm / 100) / 10:0.#} m" : mm >= 100 ? $"{Math.Round(mm / 50) * 5:0} cm" : $"{Math.Round(mm / 10):0} cm";
    private static string RoughMass(double g) => g >= 1000 ? $"{Math.Round(g / 250) / 4:0.##} kg" : g >= 100 ? $"{Math.Round(g / 50) * 50:0} g" : "a handful of grams";

    private static string Spread(CraftPiece p, int first, int last)
    {
        double a = p.PositionOf(first), b = p.PositionOf(Math.Min(last, p.segments.Count - 1));
        if (b - a > 0.6) return " all along it";
        if (b - a > 0.25) return " in several places";
        return Where(p, (first + last) / 2);
    }

    private static string DefectWords(CraftDefect d, string where)
    {
        switch (d.kind)
        {
            case CraftDefectKind.Crack: return d.severity > 0.5 ? $"A crack runs into it{where}." : $"A fine hairline crack shows{where}.";
            case CraftDefectKind.ColdShut: return $"A fold of metal lies in the surface{where}.";
            case CraftDefectKind.Inclusion: return "Dark specks and streaks of slag show in it.";
            case CraftDefectKind.Porosity: return "It is pocked with small holes.";
            case CraftDefectKind.Delamination: return $"Layers gape apart at the edge{where}.";
            case CraftDefectKind.Burnt: return $"The metal looks coarse and crumbly{where}, glittering where it broke.";
            case CraftDefectKind.ScalePit: return $"Pits from hammered-in scale mar the surface{where}.";
            case CraftDefectKind.Overground: return $"It is thin as paper{where}.";
            case CraftDefectKind.EdgeChip: return "The edge is chipped.";
            case CraftDefectKind.Hole: return "There are holes in it.";
            case CraftDefectKind.Score: return "Knife scores cross the flesh side.";
            case CraftDefectKind.Shrunk: return "It has shrunk and puckered.";
            case CraftDefectKind.CaseHardened: return "The cut edge shows a pale, raw core between dark tanned skins.";
            case CraftDefectKind.Rot: return "It smells of rot; the hair pulls out in tufts.";
            case CraftDefectKind.Tear: return "It has torn at the edge.";
            case CraftDefectKind.Misrun: return "The far end of the casting never filled.";
            case CraftDefectKind.Blotch: return "The dye is blotchy.";
            case CraftDefectKind.RoughEdge: return "Its edges are sharp and unfinished.";
            case CraftDefectKind.LooseJoint: return "Something on it is loose.";
            case CraftDefectKind.CrackedRivet: return "A rivet head is split.";
            case CraftDefectKind.Misaligned: return "The holes do not line up.";
            case CraftDefectKind.Gap: return "There is a gap where the parts meet.";
            case CraftDefectKind.Bark: return "Bark is still on it.";
            case CraftDefectKind.Chip: return d.severity > 0.4 ? "It is badly chipped." : "A small chip is missing from it.";
            case CraftDefectKind.Felted: return "The fibres have matted into felt.";
            default: return d.kind.ToString();
        }
    }

    private static void Ore(CraftRules rules, CraftPiece p, CraftJournalData d, CraftInstruments tools, List<string> lines)
    {
        CraftOreDefinition o = rules.Ore(p.ore.ore);
        if (o == null)
            return;
        lines.Add(CraftText.Capital(o.observation) + ".");
        lines.Add($"Its lustre is {o.luster}.");
        double rich = 0, barren = 0, dirt = 0, fines = 0, mass = p.ore.Mass;
        foreach (OreFragment f in p.ore.fragments)
        {
            if (f.mineral > 0.6) rich += f.mass;
            if (f.mineral < 0.2) barren += f.mass;
            dirt += f.dirt * f.mass;
            if (f.size < CraftOres.FineSize) fines += f.mass;
        }
        if (p.ore.fragments.Count > 1)
        {
            if (rich > mass * 0.3) lines.Add("Some fragments are rich with the ore's colour.");
            if (barren > mass * 0.1) lines.Add($"Some fragments look like plain {(o.gangue == CraftGangue.Limestone ? "limestone" : o.gangue == CraftGangue.Clay ? "clay" : "grey rock")}.");
            if (fines > mass * 0.3) lines.Add("Much of it has been crushed to grit and dust.");
        }
        if (dirt > mass * 0.05) lines.Add("It is caked in dirt and clay.");
        else if (p.ore.moisture > 0.3) lines.Add("It is wet.");
        if (p.ore.roasted >= CraftSmelting.RoastedEnough) lines.Add("It has been roasted: it is crumbly and has lost its smell.");
        else if (p.ore.roasted > 0.3) lines.Add("It is partly roasted: crumbly at the surface, but it still smells of sulfur - unroasted, it gives a matte.");
        if (p.ore.temperature > 120) lines.Add(p.ore.temperature > 500 ? "It glows: it is far too hot to touch." : "Heat shimmers off it: it is too hot to touch.");
        if (o.magnetic && (tools & CraftInstruments.Magnet) != 0) lines.Add("The magnet pulls at it.");
        if ((tools & CraftInstruments.StreakPlate) != 0) lines.Add($"On the streak plate it leaves a {o.streak} streak.");
        // its heft is the lump's own (its mineral, rock and dirt together), against ordinary rock's 2.7: gold-bearing quartz
        // weighs like quartz, a rich galena or magnetite lump a quarter more, a fallen star twice as much
        double bulk = BulkDensity(o, p.ore);
        if (bulk > 5) lines.Add("It is very heavy in the hand.");
        else if (bulk > 3.4) lines.Add("It is heavy for a stone.");
        else if (bulk < 2.5) lines.Add("It is light for a stone.");
    }

    /// <summary>
    /// How dense a lump is (g/cm³), as it weighs in the hand: its fragments' mineral at the ore's density, its rock at its
    /// gangue's (quartz 2.65, clay 2.6, limestone 2.7) and its dirt at 1.8. Ordinary rock is about 2.7.
    /// </summary>
    public static double BulkDensity(CraftOreDefinition o, OreState ore)
    {
        double rock = o == null ? 2.65 : o.gangue == CraftGangue.Clay ? 2.6 : o.gangue == CraftGangue.Limestone ? 2.7 : 2.65;
        double mineral = Math.Max(0.5, o?.density ?? 2.65), mass = 0, volume = 0;
        if (ore?.fragments != null)
            foreach (OreFragment f in ore.fragments)
            {
                mass += f.mass;
                volume += f.mass * (f.mineral / mineral + f.gangue / rock + f.dirt / 1.8);
            }
        return volume > 1e-9 ? mass / volume : mineral;
    }

    /// <summary>A cloth whose weave is less sound than this on average (its cells' grain: <see cref="CraftFibre.Weave"/>) is called loose and weak.</summary>
    public const double LooseWeave = 0.65;

    private static void Hide(CraftRules rules, CraftPiece p, CraftJournalData d, List<string> lines)
    {
        HideState h = p.hide;
        CraftHideDefinition def = rules.Hide(h.hide);
        if (def != null && def.textile)
        {
            if (!string.IsNullOrEmpty(def.description)) lines.Add(CraftText.Capital(def.description));
            // the weave as it was woven (CraftFibre.Weave: the yarn's soundness and each row's beat), and its flaws
            double sound = 0, flawed = 0, cells = 0;
            foreach (HideCell c in h.cells)
                if (c.present) { sound += c.grain; flawed += c.damage; cells++; }
            if (cells > 0 && sound / cells < LooseWeave) lines.Add("The weave is loose and weak: a poor yarn, or rows beaten unevenly.");
            if (cells > 0 && flawed / cells > 0.05) lines.Add("Slubs and loose picks flaw it here and there.");
            if (!string.IsNullOrEmpty(h.pattern))
            {
                lines.Add(CraftTextiles.OnGrain(h) ? "It was cut along the grain: it hangs straight." : "It was cut across the grain: it will stretch and pucker.");
                lines.Add(h.cutQuality > CraftLeather.CleanCut ? "Its edges are cut clean." : h.cutQuality > CraftLeather.RaggedCut ? "Its edges are a little ragged." : "Its edges are ragged and fraying.");
            }
            return;
        }
        CraftPieceReport r = CraftEvaluator.Piece(rules, p);
        double flesh = 0, hair = 0, n = 0, hairLoose = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { flesh += c.flesh; hair += c.hair; hairLoose += c.hairLoose; n++; }
        if (n <= 0)
            return;
        flesh /= n;
        hair /= n;
        hairLoose /= n;
        if (r.Tannage < 0.2)
        {
            if (flesh > 0.3) lines.Add("Fat and flesh still cling to the flesh side.");
            else if (flesh > 0.08) lines.Add("Patches of flesh remain here and there.");
            else lines.Add("The flesh side is scraped clean.");
        }
        // the hair by the work's own measure (CraftLeather.HairLeft: what the path's Unhair step and the evaluator read)
        double hairLeft = CraftLeather.HairLeft(def, hair);
        if (hairLeft > 0.5) lines.Add(hairLoose > 0.7 ? "The hair comes away at a touch." : "It still has its hair.");
        else if (hairLeft > 0) lines.Add(hairLoose > 0.7 ? "Loose tufts of hair remain." : "Tufts of hair remain.");
        if (r.Tannage > 0.6) lines.Add(r.CoreTannage < 0.4 ? "It is tanned on the outside." : "It is tanned through.");
        else if (r.Tannage > 0.2) lines.Add("It has taken some colour from the tanning.");
        if (r.Moisture > 0.6 && r.Tannage < 0.05 && h.hoursRaw < 12) lines.Add("It is fresh and wet.");
        else if (r.Moisture > 0.7) lines.Add("It is sodden.");
        else if (r.Moisture > 0.35) lines.Add("It is damp.");
        else if (r.Moisture < 0.15 && r.Tannage > 0.3) lines.Add("It is dry.");
        if (r.Stiffness > 0.8) lines.Add(r.Hard > 0.5 ? "It is hard as wood." : "It is stiff as a board.");
        else if (r.Suppleness > 0.75) lines.Add("It is soft and supple.");
        else if (r.Stiffness > 0.4) lines.Add("It is firm.");
        if (r.WaterResistance > 0.6) lines.Add("Water beads on it.");
        double dye = 0;
        foreach (HideCell c in h.cells) if (c.present) dye += c.dye;
        if (dye / n > 0.1) lines.Add(r.DyeEvenness < CraftEvaluator.PatchyDye ? "The dye is patchy." : "It is evenly dyed.");
        if (r.Shrink > 0.1) lines.Add("It has shrunk and puckered.");
        else if (r.Puckered > 0.02) lines.Add("It dried puckered: it was not stretched as it dried.");
        if (r.Distortion > 0.05) lines.Add("It dried out of square: the frame pulled it unevenly.");
        if (r.Damage > 0.15) lines.Add("It is scarred and holed in places.");
        if (r.Slicked > 0.5) lines.Add("Its grain is slicked to a gloss.");
        if (!string.IsNullOrEmpty(h.pattern))
        {
            // a cut part: how cleanly it was cut, and whether its edges were burnished
            lines.Add(h.cutQuality > CraftLeather.CleanCut ? "Its edges are cut clean." : h.cutQuality > CraftLeather.RaggedCut ? "Its edges are cut a little ragged." : "Its edges are cut ragged.");
            lines.Add(h.edgeFinish > 0.6 ? "Its edges are burnished smooth." : h.edgeFinish > 0.2 ? "Its edges are partly burnished." : "Its edges are raw and unburnished.");
        }
        if (p.location == CraftLocation.Frame)
        {
            // by each side's pull on what its edge can take, as the frame's gauges show it
            double[] pull = CraftLeather.Pulls(rules, h);
            double most = Math.Max(Math.Max(pull[0], pull[1]), Math.Max(pull[2], pull[3]));
            lines.Add(most > CraftLeather.OverdrawnFrom ? "It is laced overtight in the frame: an edge will thin as it dries."
                : CraftLeather.Unevenness(pull) > CraftLeather.UnevenFrom ? "It is laced unevenly in the frame."
                : CraftLeather.Flatness(pull) >= 1 ? "It is laced taut in the frame." : "It is laced slack in the frame.");
        }
    }

    private static void Wood(CraftRules rules, CraftPiece p, List<string> lines)
    {
        CraftWoodDefinition w = rules.Wood(p.wood.wood);
        if (w != null) lines.Add(CraftText.Capital(w.description));
        WoodState ws = p.wood;
        lines.Add(ws.runout < 0.12 ? "The grain runs straight along it, end to end." : ws.runout < 0.35 ? "The grain runs a little off its length." : "The grain runs out of its sides: it will break along it.");
        if (ws.moisture > 0.25) lines.Add("It is green and heavy with water.");
        else if (ws.moisture > CraftWoodwork.DryEnough) lines.Add("It is still damp: season it before it goes into a fit.");
        double t = CraftWoodwork.MeanTemperature(p);
        if (p.location == CraftLocation.Steam) lines.Add(ws.steamed >= 1 ? "It has steamed through: it should bend now." : ws.steamed > 0.4 ? "It is softening in the steam." : "It is heating in the steam.");
        else if (CraftWoodwork.Plasticity(p) > 0.3) lines.Add(t > 80 ? "It is hot and limp from the steam: bend it now." : "It is cooling and stiffening.");
        double bend = 0;
        foreach (MetalSegment s in p.segments) bend += s.bend;
        if (Math.Abs(bend) > 8) lines.Add($"It is bent through about {Math.Abs(bend):0}°.");
        if (ws.checks > 0.3) lines.Add("Deep checks have opened in it.");
        else if (ws.checks > 0.08) lines.Add("Fine checks run along its surface and ends.");
        if (ws.finish > 0.5) lines.Add(ws.finishEven > 0.7 ? "A deep, even finish glows on it." : "The finish on it is blotchy.");
        else if (ws.finish > 0.1) lines.Add(ws.finishWet > 0.3 ? "The coat is still tacky." : "It has a thin coat of finish.");
        if (p.wood.split > 0.1) lines.Add("It has split along the grain.");
        if (p.wood.charred > 0.1) lines.Add("It is scorched black.");
        if (ws.bark > 0.6) lines.Add("It is still in its bark.");
        else if (ws.bark > 0.05) lines.Add("Patches of bark are left on it.");
        if (ws.gouged > 0.1) lines.Add("The drawknife has gouged into it.");
        if (ws.rot > 0.1) lines.Add("Under the bark the sapwood is soft with rot and beetle.");
        if (ws.lastGrit >= 0 && ws.finish <= 0.1) lines.Add(ws.lastGrit >= CraftWoodwork.FineGrit ? "It is sanded smooth as silk." : ws.lastGrit > 0.45 ? "It is sanded smooth." : "It is sanded with a coarse paper.");
        if (ws.resin > 0.3) lines.Add("A hard amber film of resin seals it.");
        if (ws.wax > 0.3) lines.Add("It has a soft wax sheen.");
        if (ws.oil > 0.2 && ws.finish <= 0.1) lines.Add(ws.finishWet > 0.3 ? "The oil in its grain is still tacky." : "Oil has soaked into its grain and hardened.");
        double hole = 0;
        foreach (MetalSegment s in p.segments) hole = Math.Max(hole, s.hole);
        if (hole > 0) lines.Add("A slot has been cut into it.");
    }

    private static void Fibre(CraftRules rules, CraftPiece p, List<string> lines)
    {
        FibreState f = p.fibre;
        CraftSupplyDefinition s = rules.Supply(f.fibre);
        if (s != null && !string.IsNullOrEmpty(s.description)) lines.Add(s.description);
        lines.Add(f.dirt > 0.25 ? "Burrs, seeds and bits of straw are caught all through it." : f.dirt > 0.08 ? "A little dirt and chaff is left in it." : "It is clean.");
        if (f.grease > 0.3) lines.Add("It is greasy and sticky to the touch.");
        else if (f.grease > 0.12) lines.Add("It still feels a little waxy.");
        if (f.felted > 0.4) lines.Add("It has matted into a felt: it will hardly tease apart.");
        else if (f.felted > 0.15) lines.Add("Parts of it have matted.");
        lines.Add(f.alignment > 0.85 ? "The fibres lie straight and parallel, like a combed top." : f.alignment > 0.5 ? "The fibres lie mostly one way, in an airy roll." : f.tangle > 0.5 ? "The locks are matted and tangled." : "The fibres lie every which way.");
        if (f.neps > 0.15) lines.Add("Little knots (neps) of fibre are scattered through it.");
        if (f.shortFibre > 0.3) lines.Add("Much of it is short and broken.");
        if (f.wet > 0.4) lines.Add("It is soaking wet.");
        else if (f.wet > 0.1) lines.Add("It is damp.");
        if (f.soap > 0.2) lines.Add("It feels slippery with soap.");
        lines.Add($"About {Math.Round(f.grams / 5) * 5:0} g of it.");
    }

    private static void Yarn(CraftRules rules, CraftPiece p, CraftInstruments tools, List<string> lines)
    {
        YarnState y = p.yarn;
        lines.Add(y.evenness > 0.8 ? "It is fine and even all along." : y.evenness > 0.55 ? "It thickens and thins here and there." : "It is lumpy: thick slubs and thin places.");
        lines.Add(y.twist < 0.3 ? "It is soft and under-twisted: it drifts apart when pulled." : y.twist > 0.7 ? "It is hard and over-twisted: it kinks on itself." : "It holds its twist well.");
        if (y.plies > 1) lines.Add(y.balance > 0.75 ? $"{y.plies} strands plied: the skein hangs straight." : $"{y.plies} strands plied: the skein twists on itself.");
        if (y.worsted) lines.Add("Spun from combed fibre: smooth and lustrous.");
        lines.Add(y.measured ? $"A counted skein of {y.measuredLength:0} m." : $"Roughly {Math.Round(y.length / 10) * 10:0} m on the bobbin: wind it on the niddy-noddy to count it.");
    }

    private static void Gem(CraftRules rules, CraftPiece p, CraftJournalData d, CraftInstruments tools, List<string> lines)
    {
        GemState g = p.gem;
        CraftGemDefinition def = rules.Gem(g.gem);
        if (def == null)
            return;
        bool known = g.identified || CraftJournal.KnowsGem(d, def.id);
        lines.Add(known && !string.IsNullOrEmpty(def.description) ? def.description : CraftText.Capital(def.observation) + ".");
        if (def.opaque) lines.Add("It lets no light through.");
        else lines.Add(g.tone > 0.8 ? "Its colour is rich and deep." : g.tone > 0.6 ? "Its colour is good." : "Its colour is pale.");
        if (g.looked && def.cleavage >= CraftCleavage.Good && g.stage == CraftGemStage.Rough)
            lines.Add("Under the loupe, flat, mirror-like cracks catch the light along one direction: it cleaves.");
        if (g.hardLo > 1 || g.hardHi < 10)
            lines.Add(g.hardHi < 10 ? $"A {g.hardHi:0.#} point scratches it{(g.hardLo > 1 ? $", a {g.hardLo:0.#} does not" : "")}." : $"A {g.hardLo:0.#} point does not scratch it.");
        if (g.readRI >= 0) lines.Add($"Refractometer: {g.readRI:0.00}{(g.stage < CraftGemStage.Faceted ? " (a spot reading on the rough)" : "")}.");
        if (g.readSG >= 0) lines.Add($"Hydrostatic balance: specific gravity {g.readSG:0.00}.");
        if (g.readPleo >= 0) lines.Add(g.readPleo > 0.4 ? $"Dichroscope: its colour changes strongly with direction; the axis lies about {g.readAxis:0}° from the top." : g.readPleo > 0.12 ? $"Dichroscope: its colour shifts a little with direction (axis near {g.readAxis:0}°)." : "Dichroscope: its colour does not change with direction.");
        if (!string.IsNullOrEmpty(g.identifiedAs)) lines.Add($"You named it {rules.Gem(g.identifiedAs)?.Name.ToLowerInvariant() ?? g.identifiedAs}.");
        if (g.evaluated)
        {
            int found = 0, feathers = 0;
            foreach (CraftInclusion i in g.inclusions)
                if (i.found) { found++; if (i.kind == CraftInclusionKind.Feather) feathers++; }
            lines.Add(found == 0 ? "Under the loupe you found it clean." : $"Under the loupe: {found} inclusion{(found == 1 ? "" : "s")} found{(feathers > 0 ? $", {feathers} of them cracks" : "")}.");
        }
        if (g.oriented)
        {
            CraftGemCut cut = rules.Cut(g.cut);
            lines.Add($"Marked out for {CraftText.A(cut?.Name ?? "cut")} {cut?.Name.ToLowerInvariant() ?? "cut"}, its table {g.tableAngle:0}° to the crystal.");
            if (g.stage == CraftGemStage.Rough && CraftLapidary.Excess(g) > CraftLapidary.PreformExcess) lines.Add("Much rough lies outside the marked stone: saw it off.");
            if (cut != null && g.stage < CraftGemStage.Faceted) lines.Add($"{g.facets.Count} of {cut.tiers.Count} tiers cut.");
        }
        if (g.stage >= CraftGemStage.Faceted)
            lines.Add(g.polish > 0.85 ? "Its facets are polished bright." : g.polish > 0.5 ? "Its facets shine, a little hazy." : "Its facets are frosted from the lap: polish them.");
        if (g.scratches > 0.1) lines.Add("Fine scratches show on it.");
        if (g.heat > 0.2) lines.Add("It has crazed with heat.");
    }

    private static bool Did(CraftPiece p, CraftOpKind kind)
    {
        foreach (CraftRecordEntry e in p.record)
            if (e.kind == kind) return true;
        return false;
    }

    // ------------------------------------------------------------------ the test station's readout
    /// <summary>The numbers behind a piece (the test station): composition, phases and hardness by region, stresses, geometry.</summary>
    public static List<string> Readout(CraftRules rules, CraftPiece p)
    {
        const string grey = "<color=#9aa0a6>";
        var lines = new List<string>();
        if (p == null)
            return lines;
        CraftPieceReport r = CraftEvaluator.Piece(rules, p);
        var comp = new StringBuilder();
        double total = 0;
        foreach (CraftAmount a in p.composition) total += a.grams;
        foreach (CraftAmount a in p.composition)
            if (total > 0 && a.grams / total > 0.00005)
                comp.Append($"{rules.Element(a.element)?.Name ?? a.element} {a.grams / total * 100:0.###}%  ");
        if (comp.Length > 0) lines.Add($"{grey}Composition: {comp.ToString().Trim()}</color>");
        if (p.kind == CraftPieceKind.Metal || p.kind == CraftPieceKind.Fasteners)
        {
            CraftAlloyProfile prof = p.Profile(rules);
            lines.Add($"{grey}Class: {prof.ClassName}; solidus {prof.Solidus:0} °C, liquidus {prof.Liquidus:0} °C, burns above {prof.BurnTemperature:0} °C, hot work {prof.HotWorkMin:0}-{prof.HotWorkMax:0} °C" +
                      (prof.HardensByQuench ? $", A1 {prof.CriticalTemperature:0} °C, Ms {prof.MartensiteStart:0} °C, hardenability {prof.Hardenability:0.00}" : "") + "</color>");
            lines.Add($"{grey}Mass {r.Mass:0.0} g; T {p.MaxTemperature:0} °C max / {p.MeanTemperature:0} °C mean; edge {r.EdgeHardness:0} HV, body {r.BodyHardness:0} HV, toughness {r.Toughness:0.0} J; integrity {r.Integrity:0.00}</color>");
            int n = p.segments.Count;
            foreach (int i in new[] { 0, n / 4, n / 2, 3 * n / 4, n - 1 })
            {
                if (i < 0 || i >= n) continue;
                MetalSegment g = p.segments[i];
                CraftZone e = g.edge, b = g.body;
                lines.Add($"{grey}@{p.PositionOf(i) * 100:0}%: {g.width:0.0}×{g.thickness:0.00} mm, edge {g.EdgeThickness:0.00} mm/{g.EdgeAngle:0}°, apex {g.apex:0} µm, bend {g.bend:0.0}° | " +
                          $"edge {e.temp:0}°C A{e.aust:0.00} M{e.mart:0.00} P{e.pearl:0.00} grain {e.grain:0.00} temper {CraftMetallurgy.TemperFraction(e.temperP):0.00} σ{e.stress:0.00} | " +
                          $"body {b.temp:0}°C A{b.aust:0.00} M{b.mart:0.00} P{b.pearl:0.00} cold {b.cold:0.00} σ{b.stress:0.00}; scale {g.scale:0.000}, slag {g.slag:0.0} g, pores {g.porosity:0.00}</color>");
            }
        }
        else if (p.kind == CraftPieceKind.Gem)
        {
            CraftGemGrade gg = CraftLapidary.Grade(rules, p);
            lines.Add($"{grey}{rules.Gem(p.gem.gem)?.Name ?? p.gem.gem}: {p.gem.carats:0.00} ct ({p.gem.stage}); clarity {gg.Clarity:0.00}, colour {gg.Color:0.00}, cut {gg.Cut:0.00}, polish {gg.Polish:0.00}, integrity {gg.Integrity:0.00}, brilliance {gg.Brilliance:0.00}; quality {gg.Quality:0.00}, value {gg.Value:0}{(gg.Yield > 0 ? $", expected yield {gg.Yield:0.00} ct" : "")}</color>");
        }
        else if (p.kind == CraftPieceKind.Fibre)
            lines.Add($"{grey}{p.fibre.grams:0} g; dirt {p.fibre.dirt:0.00}, grease {p.fibre.grease:0.00}, tangle {p.fibre.tangle:0.00}, alignment {p.fibre.alignment:0.00}, short {p.fibre.shortFibre:0.00}, neps {p.fibre.neps:0.00}, felted {p.fibre.felted:0.00}, wet {p.fibre.wet:0.00}; ready to spin {CraftFibre.Prep(p.fibre):0.00}</color>");
        else if (p.kind == CraftPieceKind.Yarn)
            lines.Add($"{grey}{p.yarn.length:0} m, {p.yarn.grams:0} g, {p.yarn.plies} ply; evenness {p.yarn.evenness:0.00}, twist {p.yarn.twist:0.00}, balance {p.yarn.balance:0.00}, strength {p.yarn.strength:0.00}, prep {p.yarn.prep:0.00}; quality {CraftFibre.Quality(p.yarn):0.00}</color>");
        else if (p.kind == CraftPieceKind.Wood)
            lines.Add($"{grey}Moisture {p.wood.moisture:0.000}, bark {p.wood.bark:0.00}, gouged {p.wood.gouged:0.00}, rot {p.wood.rot:0.00}, runout {p.wood.runout:0.00}, checks {p.wood.checks:0.00}, last grit {p.wood.lastGrit:0.00}, finish {p.wood.finish:0.00} (even {p.wood.finishEven:0.00}), oil {p.wood.oil:0.00}, wax {p.wood.wax:0.00}, resin {p.wood.resin:0.00}; water resistance {CraftWoodwork.FinishProtection(p.wood):0.00}, wear {CraftWoodwork.FinishWear(p.wood):0.00}</color>");
        else if (p.kind == CraftPieceKind.Hide)
            lines.Add($"{grey}Tannage {r.Tannage:0.00} (core {r.CoreTannage:0.00}), moisture {r.Moisture:0.00}, stiffness {r.Stiffness:0.00}, suppleness {r.Suppleness:0.00}, water resistance {r.WaterResistance:0.00}, shrink {r.Shrink:0.00}, damage {r.Damage:0.00}, shrink temperature {p.hide.shrinkTemperature:0} °C</color>");
        else if (p.kind == CraftPieceKind.Ore)
        {
            OreState o = p.ore;
            var metals = new StringBuilder();
            CraftOreDefinition def = rules.Ore(o.ore);
            if (def != null)
                foreach (CraftOreYield y in def.yields)
                    metals.Append($", {rules.Element(y.element)?.Name ?? y.element} {CraftOres.MetalContent(rules, p, y.element) * o.Mass:0.#} g");
            lines.Add($"{grey}{o.fragments.Count} fragments, {o.Mass:0} g ({o.MineralMass:0} g mineral{metals}), roasted {o.roasted:0.00}, moisture {o.moisture:0.00}, deposit {o.deposit}</color>");
        }
        return lines;
    }

    // ------------------------------------------------------------------ the workshop
    /// <summary>
    /// A fire (the forge's or the furnace's) in words: how hot it glows (the colour words of
    /// <see cref="CraftMetallurgy.HeatName"/>, so firewood's orange is not coke's white), what its flame does
    /// (<see cref="Flame"/>), its fuel and its air.
    /// </summary>
    public static string Fire(CraftFireState f, bool thermometer, bool furnace)
    {
        if (f.fuel <= 0 && f.level < 0.03)
            return f.lit ? "Only cold ashes are left." : furnace ? "The furnace is cold." : "The forge is cold.";
        string heat = CraftMetallurgy.HeatName(f.temperature, false);
        string glow = heat.Length == 0 || heat == "black heat" ? (f.level > 0.1 ? "burns without a glow yet" : "smoulders") : $"glows {heat}";
        string fuel = f.fuel > 6 ? "plenty of fuel" : f.fuel > 2 ? "the fuel is burning down" : "it is nearly out of fuel";
        string air = f.damper < 0.2 ? " The air vent is choked." : f.air > 0.4 ? " The bellows make it roar." : "";
        return $"The {(furnace ? "furnace" : "fire")} {glow}{(thermometer ? $" ({CraftText.Celsius(f.temperature)})" : "")}, with {Flame(f)}; {fuel}.{air}";
    }

    /// <summary>
    /// The flame by how oxidising the fire is (<see cref="CraftFireState.Oxidizing"/>), at the furnace's own thresholds:
    /// smoky and reducing (ore gives up its metal, steel does not scale), clear (sulfides roast), an oxidising blast (lead
    /// goes into a cupel, a matte's sulfur burns - and little metal comes).
    /// </summary>
    public static string Flame(CraftFireState f)
    {
        double ox = f.Oxidizing;
        return ox <= CraftSmelting.RoastAir ? "a smoky, reducing flame" : ox <= CraftSmelting.CupelAir ? "a clear flame" : "an oxidising blast";
    }

    /// <summary>
    /// The quench's warning for a blade of hardening steel about to go into a harsh quenchant (water, brine:
    /// <see cref="CraftThermal.HarshFor"/>): it may crack - fill the tank with a gentler one the station keeps
    /// (<paramref name="offered"/>: oil), or clay the spine and leave the edge thick. "" when none is due.
    /// </summary>
    public static string HarshQuench(CraftRules rules, CraftPiece p, CraftQuenchant tank, IEnumerable<string> offered)
    {
        if (!CraftThermal.HarshFor(rules, p, tank))
            return "";
        string gentle = null;
        if (offered != null)
            foreach (string id in offered)
            {
                CraftQuenchant q = rules.Quenchant(id);
                if (q != null && CraftThermal.CrackHarshness(q) < CraftThermal.HarshQuench) { gentle = q.Name.ToLowerInvariant(); break; }
            }
        bool clayed = false;
        foreach (MetalSegment s in p.segments) clayed |= s.clay > 0.5;
        string spine = clayed ? "leave its edge thick" : "clay its spine and leave its edge thick";
        return $"{CraftText.Capital((tank?.Name ?? "water").ToLowerInvariant())} is a harsh quench for this steel: a blade may crack in it - "
               + (gentle != null ? $"fill the tank with {gentle}, which hardens it whole, or {spine}." : $"{spine}.");
    }

    /// <summary>
    /// The crucible in words: the melt's state (solid, slushy, sluggish, glaring and taking up gas, or bright and fluid),
    /// what boils off, the gas it holds, dross, slag, its cover and streaks. With the workshop (<paramref name="ws"/>), a
    /// solid charge still lying in the melt is said: it chills the melt, which takes up no gas until it has all run.
    /// </summary>
    public static string Crucible(CraftRules rules, CraftCrucibleState c, CraftJournalData d, bool thermometer, CraftWorkshop ws = null)
    {
        if (c.Mass <= CraftSmelting.EmptyCrucible)
            return c.slag + c.dross > 5 ? $"Only a crust of {(c.slag >= c.dross ? "slag" : "dross")} is left in the crucible: scrape it out with the skimmer before the next melt." : "The crucible is empty.";
        CraftAlloyProfile p = CraftSmelting.MeltProfile(rules, c);
        double superheat = c.temperature - p.Liquidus;
        var sb = new StringBuilder();
        if (c.temperature < p.Solidus) sb.Append("The metal in the crucible is solid.");
        else if (superheat < 0) sb.Append("It is slushy: melting, not yet liquid.");
        else if (CraftSmelting.Fill(rules, p, superheat) < CraftSmelting.FullFill) sb.Append("The melt is sluggish: poured now, it would freeze before it filled a mould.");
        else if (CraftSmelting.GasChange(superheat, c.cover) > 0)
            sb.Append(CraftSmelting.ChargeUnmelted(ws) ? "The melt is glaring, but the solid metal still in it chills it: it takes up no gas until that has run too."
                : "The melt is glaring and runs like water: held this hot, it takes up gas.");
        else sb.Append("The melt is bright and fluid.");
        if (thermometer) sb.Append($" ({CraftText.Celsius(c.temperature)})");
        if (superheat >= 0)
            foreach (CraftAmount a in c.melt)
            {
                CraftElement e = rules.Element(a.element);
                if (e != null && a.grams > 0.5 && c.temperature > e.boilingPoint)
                {
                    sb.Append($" White fumes and green-white flames: the {e.Name.ToLowerInvariant()} is burning off{(c.cover > 0.3 ? "" : " - a cover keeps most of it")}.");
                    break;
                }
            }
        if (c.gas > 0.3) sb.Append(" It spits and bubbles: it has taken up gas (held just above melting, under a cover, it settles).");
        else if (c.gas > CraftSmelting.PoreGas && superheat >= 0) sb.Append(" Fine bubbles rise through it: it holds gas, and would cast with pores (held just above melting, under a cover, it settles).");
        if (c.dross > 5) sb.Append(" A skin of dross floats on it.");
        if (c.slag > 5) sb.Append(" The ore's rock floats on it as slag: skim it off.");
        if (c.cover > 0.3) sb.Append(" A glassy cover of flux lies over it.");
        if (c.mixed < 1 - CraftSmelting.Streaky) sb.Append(" Streaks of different colour swirl in it: stir it even, or it casts streaky and weak.");
        return sb.ToString();
    }

    /// <summary>
    /// The furnace in words: its fire and flame, what the flame does to the charge, the slag and the fine ore choking the
    /// draft (<paramref name="fines"/>: the share of the charge that is fines, <see cref="CraftSmelting.FinesInCharge"/>),
    /// the metal at the bottom, the matte, the bloom, a cupel.
    /// </summary>
    public static string Furnace(CraftRules rules, CraftFurnaceState f, bool thermometer, double fines = 0)
    {
        var sb = new StringBuilder(Fire(f.fire, thermometer, true));
        if (f.fire.fuel > 0 || f.fire.level >= 0.03)
        {
            double ox = f.fire.Oxidizing;
            sb.Append(ox <= CraftSmelting.RoastAir ? " Ore gives up its metal in it." : ox <= CraftSmelting.CupelAir ? " Sulfide ore roasts in it; metal comes only slowly."
                : " It roasts sulfides and drives lead into a cupel, but gives little metal.");
        }
        if (f.slag > rules.Settings.slagCapacity * 0.6) sb.Append(" Slag is building up: the draft labours.");
        else if (f.slag > 200) sb.Append(" Slag drips down through the charge.");
        if (fines > FinesChoke) sb.Append(" Fine ore packs the charge: the draft labours through it (sieve the fines off, and charge lumps).");
        double metal = f.PoolMass;
        if (metal >= CraftSmelting.PourMinimum)
            sb.Append(CraftSmelting.PoolLiquid(rules, f) ? " Liquid metal glows at the bottom." : " Metal has frozen at the bottom: the furnace is too cold to run it.");
        else if (metal >= CraftSmelting.BeadMinimum)
            sb.Append(" A bead of metal lies at the bottom: too little to pour - lift it out with the tongs.");
        if (f.matte > 1) sb.Append(" A dull matte lies under the slag: an oxidising blast roasts its sulfur away.");
        if (f.BloomMass >= CraftSmelting.BloomMinimum) sb.Append(" A spongy bloom has grown at the bottom.");
        else if (f.BloomMass >= 1) sb.Append(" A little iron has gathered at the bottom.");
        if (f.cupel > 0)
            sb.Append(metal > 0 && CraftSmelting.Grams(f.pool, "lead") > 0.01 && f.fire.Oxidizing > CraftSmelting.CupelAir && f.fire.temperature > 900
                ? " The cupel darkens as it soaks up the oxide." : " A bone-ash cupel lies in the hearth.");
        return sb.ToString();
    }

    /// <summary>A charge this much fines (by mass) is said to choke the draft.</summary>
    public const double FinesChoke = 0.15;

    public static string Quench(CraftRules rules, CraftQuenchState q, bool thermometer)
    {
        CraftQuenchant m = rules.Quenchant(q.medium);
        string warm = q.temperature > 80 ? "steaming hot" : q.temperature > 45 ? "warm" : "cool";
        return $"The tank holds {m?.Name.ToLowerInvariant() ?? q.medium}, {warm}{(thermometer ? $" ({CraftText.Celsius(q.temperature)})" : "")}.";
    }

    public static string Vat(CraftRules rules, CraftVatState v, bool thermometer, bool reveal)
    {
        if (v.volume <= 0.01)
            return "Empty.";
        CraftTanningAgent a = rules.Agent(v.agent);
        string strength = VatStrength(v.strength);
        string temp = VatHeat(v.temperature);
        string kind = a?.observation ?? v.agent;
        return a != null && a.kind == CraftAgentKind.Water
            ? $"Water, {temp}{(thermometer ? $" ({CraftText.Celsius(v.temperature)})" : "")}."
            : $"{CraftText.Capital(kind)}: {strength}{(reveal ? $" ({v.strength * 100:0}%)" : "")}, {temp}{(thermometer ? $" ({CraftText.Celsius(v.temperature)})" : "")}.";
    }

    /// <summary>How warm a vat's liquor feels to the hand, without a thermometer (the same words wherever a vat is shown).</summary>
    public static string VatHeat(double celsius) => celsius > 85 ? "simmering" : celsius > 50 ? "hot" : celsius > 30 ? "warm" : "cool";

    /// <summary>How strong a vat's liquor is to the tanner's taste and eye (0-1 strength: strong, medium, weak, spent), wherever a vat is worded.</summary>
    public static string VatStrength(double strength) => strength > 0.6 ? "strong" : strength > 0.3 ? "medium" : strength > 0.05 ? "weak" : "spent";
}
