using System;
using System.Collections.Generic;

/// <summary>What a metal part of a piece of jewellery is formed into. Serialized as numbers: add at the END.</summary>
public enum CraftJewelPart
{
    /// <summary>Not formed yet.</summary>
    None,
    /// <summary>A ring's band (the shank), formed round the mandrel and its seam closed.</summary>
    Band,
    /// <summary>A bezel: a collar of thin strip that will be pushed over the stone's girdle.</summary>
    Bezel,
    /// <summary>A prong head: claws of wire that will be pushed over the stone's crown.</summary>
    Prongs,
    /// <summary>A pendant's bail: the loop it hangs from.</summary>
    Bail,
    /// <summary>A chain of links bent and closed one by one.</summary>
    Chain,
}

/// <summary>
/// The jeweller's work: <b>forming</b> the metal (a band round the ring mandrel to the wearer's size, a bezel collar or a
/// prong head, a bail, a chain), <b>preparing the setting</b> (cutting the seat the stone will sit in to the stone's own
/// girdle - too small it will not go in, too large it rattles - and level, or it sits tilted), <b>fabrication</b>
/// (soldering the parts: flux, then heat until the solder flows - short of it nothing joins, past it the metal pits and
/// the heat reaches a sensitive stone), <b>setting</b> (pushing the bezel or the prongs over the stone, opposite sides in
/// turn, firmly but not so hard a brittle stone chips) and <b>finishing</b> (buffing, cleaning, and the inspection under
/// the loupe: a loose or tilted stone is caught here).
/// </summary>
public static class CraftJewelry
{
    public const string PartTag = "jewel.part", SizeTag = "jewel.size", RoundTag = "jewel.round", SeamTag = "jewel.seam", ProngTag = "jewel.prongs",
        SeatTag = "jewel.seat", SeatLevelTag = "jewel.seatLevel", SeatCleanTag = "jewel.seatClean";

    public static CraftJewelPart PartOf(CraftPiece p) => p == null ? CraftJewelPart.None : (CraftJewelPart)(int)Math.Round(p.Tag(PartTag));

    /// <summary>The wearer's ring finger (inner diameter, mm): their own, from their name - the same wherever it is measured.</summary>
    public static double FingerOf(string wearer) => 15.5 + (CraftMath.Hash(string.IsNullOrEmpty(wearer) ? "wearer" : wearer) & 63) / 63.0 * 6.0;

    /// <summary>The length (mm) of metal a part of <paramref name="size"/> mm (inner diameter) needs.</summary>
    public static double LengthNeeded(CraftJewelPart part, double size, double thickness, int prongs) => part switch
    {
        CraftJewelPart.Band => Math.PI * (size + thickness),
        CraftJewelPart.Bezel => Math.PI * (size + thickness),
        CraftJewelPart.Prongs => Math.Max(3, prongs) * 9,
        CraftJewelPart.Bail => 14,
        CraftJewelPart.Chain => Math.Max(300, size * 3),
        _ => 0,
    };

    /// <summary>The thickest and widest the metal of a part may be (mm): jewellery is wire and strip.</summary>
    public static double MaxThickness(CraftJewelPart part) => part == CraftJewelPart.Bezel ? 1.2 : part == CraftJewelPart.Chain ? 2.5 : 4;

    /// <summary>
    /// Forms a metal part: <c>i0</c> = the part, <c>i1</c> = prongs, <c>a</c> = its inner diameter (mm), <c>b</c> = how
    /// round it came off the mandrel (0-1), <c>c</c> = the gap left at the seam (mm). A hard, unannealed metal cracks.
    /// </summary>
    public static void FormShank(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Metal)
            return;
        var part = (CraftJewelPart)op.i0;
        p.SetTag(PartTag, (int)part);
        p.SetTag(SizeTag, Math.Max(0, op.a));
        p.SetTag(RoundTag, CraftMath.Clamp01(op.b));
        p.SetTag(SeamTag, Math.Max(0, op.c));
        p.SetTag(ProngTag, Math.Max(0, op.i1));
        CraftAlloyProfile prof = p.Profile(ws.Rules);
        if (prof.Hardness > 220 && prof.Ductility < 10 && p.MeanTemperature < prof.HotWorkMin * 0.5)
            p.AddDefect(CraftDefectKind.Crack, -1, 0.3, true, "the hard metal cracked as it was bent round the mandrel - anneal it first");
        string what = part switch
        {
            CraftJewelPart.Band => $"Formed into a band, {op.a:0.#} mm inside",
            CraftJewelPart.Bezel => $"Formed into a bezel, {op.a:0.##} mm inside",
            CraftJewelPart.Prongs => $"Formed into a head of {op.i1} prongs",
            CraftJewelPart.Bail => "Formed into a bail",
            CraftJewelPart.Chain => "Bent and closed into a chain",
            _ => "Formed",
        };
        p.Record(ws.Elapsed, CraftOpKind.FormShank, what, op.b);
    }

    /// <summary>
    /// Cuts the seat of a setting for a stone: <c>a</c> = the seat's diameter (mm), <c>b</c> = how level (0-1), <c>c</c>
    /// = how clean the burr left it (0-1). A seat is only ever cut wider.
    /// </summary>
    public static void SeatSetting(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Metal)
            return;
        double was = p.Tag(SeatTag);
        p.SetTag(SeatTag, Math.Max(was, op.a));
        p.SetTag(SeatLevelTag, CraftMath.Clamp01(op.b));
        p.SetTag(SeatCleanTag, CraftMath.Clamp01(op.c));
        CraftPiece stone = ws.Piece(op.other);
        double err = stone != null && stone.kind == CraftPieceKind.Gem ? Math.Max(was, op.a) - stone.gem.girdle : 0;
        string how = stone == null ? "Seat cut" : err < -0.15 ? "Seat cut - too small for the stone" : err > 0.3 ? "Seat cut - too wide: the stone will rattle" : "Seat cut to the stone";
        p.Record(ws.Elapsed, CraftOpKind.SeatSetting, how, err);
    }

    /// <summary>How well a seat fits a stone (1 snug; less the wider it is; 0 = the stone does not go in).</summary>
    public static double SeatFit(CraftPiece setting, CraftPiece stone)
    {
        if (setting == null || stone == null || stone.kind != CraftPieceKind.Gem)
            return 0;
        double seat = setting.Tag(SeatTag);
        if (seat <= 0)
            return 0;
        double err = seat - stone.gem.girdle;
        if (err < -0.15) return 0;
        return err <= 0.08 ? 1 : Math.Exp(-(err - 0.08) / 0.25);
    }

    // ------------------------------------------------------------------ joints
    /// <summary>The setting (metal) and the stone (gem) of a setting joint.</summary>
    public static void SettingParts(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec, out CraftPiece setting, out CraftPiece stone)
    {
        CraftPiece a = ws.Piece(asm.assembly.PieceIn(spec.roleA)), b = ws.Piece(asm.assembly.PieceIn(spec.roleB));
        setting = a != null && a.kind == CraftPieceKind.Metal ? a : b != null && b.kind == CraftPieceKind.Metal ? b : null;
        stone = a != null && a.kind == CraftPieceKind.Gem ? a : b != null && b.kind == CraftPieceKind.Gem ? b : null;
    }

    private static CraftPiece StoneIn(CraftWorkshop ws, CraftPiece asm)
    {
        foreach (AssemblyPart part in asm.assembly.parts)
        {
            CraftPiece p = ws.Piece(part.piece);
            if (p != null && p.kind == CraftPieceKind.Gem) return p;
        }
        return null;
    }

    /// <summary>
    /// Solders a joint: <c>a</c> = the peak heat as a share of the solder's flow point (1 = it just flows), <c>b</c> =
    /// how well the joint was fluxed (0-1). A gapped seam does not fill; overheated, the metal pits and firescales, and a
    /// stone already set feels it.
    /// </summary>
    public static void Solder(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec, JointState j, CraftOperation op)
    {
        double heat = Math.Max(0, op.a), flux = CraftMath.Clamp01(op.b);
        double gap = 0;
        foreach (string role in new[] { spec.roleA, spec.roleB })
        {
            CraftPiece p = ws.Piece(asm.assembly.PieceIn(role));
            if (p != null && p.kind == CraftPieceKind.Metal) gap = Math.Max(gap, p.Tag(SeamTag));
        }
        double flow = CraftMath.Smooth(0.85, 1.0, heat) * (0.35 + 0.65 * flux) * Math.Exp(-gap / 0.35);
        j.solder = Math.Max(j.solder, CraftMath.Clamp01(flow));
        if (heat > 1.12)
        {
            j.overheat = CraftMath.Clamp01(j.overheat + (heat - 1.12) * 2);
            asm.AddDefect(CraftDefectKind.Porosity, -1, (heat - 1.12) * 2, true, "overheated: the solder pitted and the metal firescaled");
        }
        CraftPiece stone = StoneIn(ws, asm);
        CraftGemDefinition gd = stone != null ? ws.Rules.Gem(stone.gem.gem) : null;
        if (gd != null && heat > 0.6 && (gd.heatSensitive || gd.toughness < 0.45))
        {
            double sev = (heat - 0.6) * (gd.heatSensitive ? 1.5 : 0.6);
            stone.gem.heat = CraftMath.Clamp01(stone.gem.heat + sev);
            stone.AddDefect(CraftDefectKind.Crack, -1, sev, false, "the soldering heat reached the stone in its setting");
        }
        if (gap > 0.4)
            ws.Raise(CraftEventKind.Note, asm.id, "gap", "The seam gapes: solder will not bridge it. Close it on the mandrel first.");
    }

    /// <summary>
    /// Pushes the bezel or the prongs over the stone: <c>i1</c> = pushes, <c>a</c> = pressure (0-1), <c>b</c> = how
    /// evenly round (opposite sides in turn, 0-1). A wide seat never holds it fully; an uneven seat or uneven pushing
    /// tilts it; too hard on a brittle stone chips it.
    /// </summary>
    public static void Push(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec, JointState j, CraftOperation op)
    {
        SettingParts(ws, asm, spec, out CraftPiece setting, out CraftPiece stone);
        if (setting == null || stone == null)
            return;
        CraftGemDefinition gd = ws.Rules.Gem(stone.gem.gem);
        int n = Math.Max(1, op.i1);
        double press = CraftMath.Clamp01(op.a), even = CraftMath.Clamp01(op.b), fit = SeatFit(setting, stone), tq = ws.ToolQuality;
        if (j.pushes == 0)
            j.tilt = CraftMath.Clamp01(j.tilt + (1 - setting.Tag(SeatLevelTag)) * 0.35);
        j.pushes += n;
        bool prongs = spec.kind == CraftJointKind.ProngSet;
        j.security = Math.Min(0.35 + 0.65 * fit, j.security + n * press * 0.07 * (0.5 + 0.5 * even) * Math.Min(1.2, tq) * (prongs ? 0.8 : 1));
        j.tilt = CraftMath.Clamp01(j.tilt + n * (1 - even) * 0.02);
        if (press > 0.8 && gd != null && gd.toughness < 0.55)
        {
            double sev = (press - 0.8) * 2 * (1 - gd.toughness);
            stone.gem.chips = CraftMath.Clamp01(stone.gem.chips + sev);
            stone.AddDefect(CraftDefectKind.Chip, -1, sev, true, prongs ? "a prong was pushed so hard it chipped the crown" : "the bezel was pushed so hard it chipped the girdle");
        }
    }

    public static bool JointDone(CraftJointSpec spec, JointState j)
    {
        switch (spec.kind)
        {
            case CraftJointKind.Solder: return j.solder >= 0.5;
            case CraftJointKind.BezelSet: return j.security >= 0.6;
            case CraftJointKind.ProngSet: return j.security >= 0.6 && j.pushes >= Math.Max(1, spec.count);
            default: return true;
        }
    }

    public static double JointQuality(CraftJointSpec spec, JointState j)
    {
        switch (spec.kind)
        {
            case CraftJointKind.Solder: return CraftMath.Clamp01(j.solder * (1 - 0.6 * Math.Min(1, j.overheat)));
            case CraftJointKind.BezelSet:
            case CraftJointKind.ProngSet: return CraftMath.Clamp01(Math.Min(1, j.security / 0.85) * (1 - 0.6 * j.tilt));
            default: return 1;
        }
    }

    /// <summary>Is the joint kind one of the jeweller's?</summary>
    public static bool IsJewelJoint(CraftJointKind k) => k == CraftJointKind.Solder || k == CraftJointKind.BezelSet || k == CraftJointKind.ProngSet;

    /// <summary>What the metal of a part of a jewellery design must be formed as (a setting: by its joint with the stone).</summary>
    public static CraftJewelPart PartWanted(CraftDesign d, CraftComponentSpec c)
    {
        if (c.function == CraftComponentFunction.Setting)
        {
            foreach (CraftJointSpec j in d.joints)
                if ((j.roleA == c.role || j.roleB == c.role) && (j.kind == CraftJointKind.BezelSet || j.kind == CraftJointKind.ProngSet))
                    return j.kind == CraftJointKind.ProngSet ? CraftJewelPart.Prongs : CraftJewelPart.Bezel;
            return CraftJewelPart.Bezel;
        }
        return c.jewelPart == CraftJewelPart.None ? CraftJewelPart.Band : c.jewelPart;
    }

    /// <summary>Can <paramref name="p"/> go in the place <paramref name="c"/> of a jewellery design?</summary>
    public static bool PartFits(CraftWorkshop ws, CraftPiece asm, CraftDesign d, CraftComponentSpec c, CraftPiece p, out string reason)
    {
        reason = "";
        if (d == null || d.kind != CraftDesignKind.Jewelry)
            return true;
        if (p.kind == CraftPieceKind.Metal)
        {
            CraftJewelPart want = PartWanted(d, c), have = PartOf(p);
            if (have != want)
            {
                reason = $"Form it into {(want == CraftJewelPart.Prongs ? "a prong head" : CraftText.A(want.ToString()) + " " + want.ToString().ToLowerInvariant())} first.";
                return false;
            }
            // a setting going on after the stone: the seat must take it
            if (c.function == CraftComponentFunction.Setting)
            {
                CraftPiece stone = StoneIn(ws, asm);
                if (stone != null && SeatFit(p, stone) <= 0)
                {
                    reason = p.Tag(SeatTag) <= 0 ? "Cut its seat for the stone first." : "The seat is too small for the stone: cut it wider.";
                    return false;
                }
            }
            return true;
        }
        if (p.kind == CraftPieceKind.Gem)
        {
            CraftGemSpec g = c.gem ?? new CraftGemSpec();
            CraftGemCut cut = ws.Rules.Cut(p.gem.cut);
            if (p.gem.stage < CraftGemStage.Polished)
            {
                reason = "Cut and polish the stone first.";
                return false;
            }
            if (p.gem.carats < g.caratsMin || p.gem.carats > g.caratsMax)
            {
                reason = $"The stone must be {g.caratsMin:0.##}-{g.caratsMax:0.#} carats ({p.gem.carats:0.##} ct).";
                return false;
            }
            bool cab = cut != null && cut.cabochon;
            if (cab && !g.cabochonOk || !cab && !g.facetedOk)
            {
                reason = cab ? "This setting takes a faceted stone." : "This setting takes a cabochon.";
                return false;
            }
            foreach (CraftComponentSpec other in d.components)
            {
                if (other.function != CraftComponentFunction.Setting) continue;
                CraftPiece setting = ws.Piece(asm.assembly.PieceIn(other.role));
                if (setting != null && SeatFit(setting, p) <= 0)
                {
                    reason = setting.Tag(SeatTag) <= 0 ? "Cut the setting's seat for this stone first." : "The seat is too small for the stone: cut it wider.";
                    return false;
                }
            }
        }
        return true;
    }

    // ------------------------------------------------------------------ finishing
    /// <summary>Buffs, cleans and inspects a piece of jewellery: <c>a</c> = how well it was buffed, <c>b</c> = cleaned (0-1).</summary>
    public static void FinishJewel(CraftWorkshop ws, CraftPiece asm, CraftOperation op)
    {
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return;
        asm.assembly.finish = CraftMath.Clamp01(Math.Max(asm.assembly.finish, 0.6 * CraftMath.Clamp01(op.a) + 0.4 * CraftMath.Clamp01(op.b)));
        CraftDesign d = ws.Rules.Design(asm.assembly.design);
        var found = new List<string>();
        if (d != null)
            foreach (CraftJointSpec spec in d.joints)
            {
                JointState j = asm.assembly.Joint(spec.id);
                if (j == null) continue;
                if ((spec.kind == CraftJointKind.BezelSet || spec.kind == CraftJointKind.ProngSet) && j.security < 0.7) found.Add("the stone moves in its setting");
                if ((spec.kind == CraftJointKind.BezelSet || spec.kind == CraftJointKind.ProngSet) && j.tilt > 0.25) found.Add("the stone sits tilted");
                if (spec.kind == CraftJointKind.Solder && j.overheat > 0.2) found.Add("pitted solder");
            }
        CraftPiece stone = StoneIn(ws, asm);
        if (stone != null && stone.gem.chips > 0.1) found.Add("the stone is chipped");
        asm.Record(ws.Elapsed, CraftOpKind.FinishJewel, found.Count == 0 ? "Buffed and inspected: sound" : "Inspected: " + string.Join(", ", found), asm.assembly.finish);
        foreach (string f in found)
            ws.Raise(CraftEventKind.Note, asm.id, f.Contains("moves") ? "loose_stone" : "inspection", CraftText.Capital(f) + ".");
    }

    // ------------------------------------------------------------------ the finished piece
    /// <summary>Jewellery's numbers: what the stone carries (magic defence, a resistance of its element), how secure it is, its worth.</summary>
    public static void Evaluate(CraftRules rules, CraftDesign d, Dictionary<string, List<CraftPieceReport>> reports, AssemblyState a, CraftItemReport r)
    {
        double stoneQ = 0, magic = 0, security = 1;
        var resist = new Dictionary<ElementType, double>();
        foreach (KeyValuePair<string, List<CraftPieceReport>> kv in reports)
            foreach (CraftPieceReport pr in kv.Value)
            {
                if (pr.Kind == CraftPieceKind.Gem && pr.Gem != null)
                {
                    CraftGemDefinition gd = rules.Gem(pr.Piece.gem.gem);
                    if (gd != null && stoneQ <= 0)
                    {
                        // named for its stone: "Gold solitaire ring with a ruby"
                        string stone = gd.Name.ToLowerInvariant();
                        r.Name += $" with {CraftText.A(stone)} {stone}";
                        r.Color = gd.color;
                    }
                    stoneQ = Math.Max(stoneQ, pr.Gem.Quality);
                    double size = CraftMath.Smooth(0.2, 6, pr.Gem.Carats);
                    magic += (0.5 + 1.5 * size) * pr.Gem.Quality * 3;
                    if (gd != null && gd.affinity != ElementType.None && gd.affinityStrength > 0)
                    {
                        resist.TryGetValue(gd.affinity, out double v);
                        resist[gd.affinity] = v + gd.affinityStrength * 40 * pr.Gem.Quality * (0.6 + 0.4 * size);
                    }
                }
                else if (pr.Kind == CraftPieceKind.Metal && pr.Profile != null)
                {
                    magic += pr.Profile.Magic * 2;
                    if (pr.Profile.Affinity != ElementType.None && pr.Profile.AffinityStrength > 0.05)
                    {
                        resist.TryGetValue(pr.Profile.Affinity, out double v);
                        resist[pr.Profile.Affinity] = v + pr.Profile.AffinityStrength * 12;
                    }
                }
            }
        if (a != null)
            foreach (CraftJointSpec spec in d.joints)
            {
                if (spec.kind != CraftJointKind.BezelSet && spec.kind != CraftJointKind.ProngSet) continue;
                JointState j = a.Joint(spec.id);
                security = Math.Min(security, j != null ? CraftMath.Clamp01(j.security / 0.85) : 0);
                if (j != null && j.tilt > 0.25) r.Notes.Add("the stone sits tilted");
            }
        if (security < 0.75) r.Notes.Add("the stone is loose: it may fall out");
        r.Protection = stoneQ;
        r.MagicDefense = (float)((d.magicDefense + magic) * (0.5 + 0.5 * security));
        r.Defense = (float)(d.defense * (0.6 + 0.4 * r.Integrity));
        foreach (KeyValuePair<ElementType, double> kv in resist)
            r.Resistances.Add(new ElementalResistanceValue(kv.Key, (float)Math.Min(50, kv.Value * (0.5 + 0.5 * security))));
        double finish = a != null ? a.finish : 0;
        if (finish < 0.5) r.Notes.Add("the metal is dull and unbuffed");
        r.Durability = Math.Max(5, (int)Math.Round(d.durability * r.Integrity * (0.5 + 0.5 * security) * (0.7 + 0.3 * r.Material)));
        r.Edge = 0;
    }
}

public sealed partial class CraftWorkshop
{
    /// <summary>The operations of the later crafts (fibre, gems, jewellery, layout and fitting), kept out of the main switch.</summary>
    private void ApplyMore(CraftOperation op)
    {
        switch (op.kind)
        {
            case CraftOpKind.CleanFibre: CraftFibre.Clean(this, Piece(op.piece), op); break;
            case CraftOpKind.ScourFibre: CraftFibre.Scour(this, Piece(op.piece), op); break;
            case CraftOpKind.OpenFibre: CraftFibre.Open(this, Piece(op.piece), op); break;
            case CraftOpKind.CardFibre: CraftFibre.Card(this, Piece(op.piece), op); break;
            case CraftOpKind.CombFibre: CraftFibre.Comb(this, Piece(op.piece), op); break;
            case CraftOpKind.SpinFibre: CraftFibre.Spin(this, Piece(op.piece), op); break;
            case CraftOpKind.PlyYarn: CraftFibre.Ply(this, Piece(op.piece), Piece(op.other), string.IsNullOrEmpty(op.id) ? null : Piece(op.id), op); break;
            case CraftOpKind.MeasureYarn: CraftFibre.Measure(this, Piece(op.piece), op); break;
            case CraftOpKind.LayoutPattern: ApplyLayout(op); break;
            case CraftOpKind.MeasureFit: ApplyMeasureFit(op); break;
            case CraftOpKind.TestGem: CraftLapidary.Test(this, Piece(op.piece), op); break;
            case CraftOpKind.IdentifyGem: CraftLapidary.Identify(this, Piece(op.piece), op); break;
            case CraftOpKind.EvaluateGem: CraftLapidary.Evaluate(this, Piece(op.piece), op); break;
            case CraftOpKind.OrientGem: CraftLapidary.Orient(this, Piece(op.piece), op); break;
            case CraftOpKind.CleaveGem: CraftLapidary.Cleave(this, Piece(op.piece), op); break;
            case CraftOpKind.SawGem: CraftLapidary.Saw(this, Piece(op.piece), op); break;
            case CraftOpKind.FacetGem: CraftLapidary.Facet(this, Piece(op.piece), op); break;
            case CraftOpKind.PolishGem: CraftLapidary.Polish(this, Piece(op.piece), op); break;
            case CraftOpKind.FormShank: CraftJewelry.FormShank(this, Piece(op.piece), op); break;
            case CraftOpKind.SeatSetting: CraftJewelry.SeatSetting(this, Piece(op.piece), op); break;
            case CraftOpKind.FinishJewel: CraftJewelry.FinishJewel(this, Piece(op.piece), op); break;
            case CraftOpKind.CleanGem: CraftLapidary.Clean(this, Piece(op.piece), op); break;
            case CraftOpKind.GrindGem: CraftLapidary.Grind(this, Piece(op.piece), op); break;
            case CraftOpKind.WeaveCloth: CraftFibre.Weave(this, Piece(op.piece), op); break;
        }
    }

    // ------------------------------------------------------------------ the jeweller's bench
    /// <summary>
    /// Forms a metal part for jewellery round the mandrel (see <see cref="CraftJewelry.FormShank"/>): its inner diameter
    /// (<paramref name="size"/> mm), how round, the seam's gap.
    /// </summary>
    public bool FormShank(string id, CraftJewelPart part, double size, double roundness, double seamGap, int prongs, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.JewelersBench))
            return Refuse("There is no jeweller's bench here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Metal)
            return Refuse("That is not metal.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, id);
        if (part == CraftJewelPart.None)
            return Refuse("Choose what to form it into.", out reason, id);
        if (p.MaxTemperature > 300)
            return Refuse("It is too hot to handle on the mandrel.", out reason, id);
        double t = 0, w = 0;
        foreach (MetalSegment s in p.segments)
        {
            t = Math.Max(t, Math.Min(s.width, s.thickness));
            w = Math.Max(w, Math.Max(s.width, s.thickness));
        }
        if (t > CraftJewelry.MaxThickness(part) || w > 14)
            return Refuse($"It is too heavy for that: draw it into wire or roll it into strip (at most {CraftJewelry.MaxThickness(part):0.#} mm thick, 14 mm wide).", out reason, id);
        if (part == CraftJewelPart.Prongs && (prongs < 3 || prongs > 8))
            return Refuse("A head has three to eight prongs.", out reason, id);
        double need = CraftJewelry.LengthNeeded(part, size, t, prongs);
        if (p.Length < need)
            return Refuse($"It is too short for that: it needs {need:0} mm.", out reason, id);
        if ((part == CraftJewelPart.Band || part == CraftJewelPart.Bezel) && (size < 2 || size > 200))
            return Refuse("That size cannot be formed on the mandrel.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.FormShank, piece = id, i0 = (int)part, i1 = Math.Max(0, prongs), a = (float)Math.Max(0, size), b = (float)CraftMath.Clamp01(roundness), c = (float)CraftMath.Clamp(seamGap, 0, 5) });
        return true;
    }

    /// <summary>Cuts a setting's seat for <paramref name="stone"/>: its diameter (mm), how level and how clean (0-1).</summary>
    public bool SeatSetting(string setting, string stone, double diameter, double level, double clean, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(setting), g = Piece(stone);
        if (!Has(CraftTools.JewelersBench))
            return Refuse("There is no jeweller's bench here.", out reason, setting);
        if (p == null || p.kind != CraftPieceKind.Metal || CraftJewelry.PartOf(p) != CraftJewelPart.Bezel && CraftJewelry.PartOf(p) != CraftJewelPart.Prongs)
            return Refuse("Form a bezel or a prong head first.", out reason, setting);
        if (p.location != CraftLocation.Air && p.location != CraftLocation.Assembled)
            return Refuse("Take it out first.", out reason, setting);
        if (g == null || g.kind != CraftPieceKind.Gem || g.gem.girdle <= 0)
            return Refuse("The seat is cut to a cut stone: it needs one to measure.", out reason, setting);
        if (diameter <= 0.5 || diameter > 60)
            return Refuse("That is no size for a seat.", out reason, setting);
        Record(new CraftOperation { kind = CraftOpKind.SeatSetting, piece = setting, other = stone, a = (float)diameter, b = (float)CraftMath.Clamp01(level), c = (float)CraftMath.Clamp01(clean) });
        return true;
    }

    /// <summary>Buffs, cleans and inspects a piece of jewellery before it is finished.</summary>
    public bool FinishJewel(string assembly, double buff, double clean, out string reason)
    {
        reason = "";
        CraftPiece asm = Piece(assembly);
        if (!Has(CraftTools.JewelersBench) && !Has(CraftTools.Polishing))
            return Refuse("There is no buffing wheel here.", out reason, assembly);
        if (asm == null || asm.kind != CraftPieceKind.Assembly || Rules.Design(asm.assembly.design)?.kind != CraftDesignKind.Jewelry)
            return Refuse("There is no piece of jewellery being made.", out reason, assembly);
        CraftDesign d = Rules.Design(asm.assembly.design);
        foreach (CraftJointSpec js in d.joints)
        {
            JointState j = asm.assembly.Joint(js.id);
            if (j == null || !CraftAssembly.JointDone(js, j))
                return Refuse($"{js.Name}: not finished.", out reason, assembly);
        }
        Record(new CraftOperation { kind = CraftOpKind.FinishJewel, piece = assembly, a = (float)CraftMath.Clamp01(buff), b = (float)CraftMath.Clamp01(clean) });
        return true;
    }

    // ------------------------------------------------------------------ measuring and laying out
    /// <summary>
    /// Measures the wearer (the tape for clothes, the ring sizer for a finger): <paramref name="accuracy"/> (0-1) and the
    /// size read (<paramref name="size"/>: mm). The next item started takes the measurements; an item already started
    /// (<paramref name="assembly"/>) takes them now.
    /// </summary>
    public bool MeasureFit(string assembly, double accuracy, double size, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.TailorTable) && !Has(CraftTools.JewelersBench) && !Has(CraftTools.LeatherBench))
            return Refuse("There is nothing to measure with here.", out reason);
        CraftPiece asm = string.IsNullOrEmpty(assembly) ? null : Piece(assembly);
        if (asm != null && (asm.kind != CraftPieceKind.Assembly || asm.assembly.finished))
            return Refuse("It is finished.", out reason, assembly);
        Record(new CraftOperation { kind = CraftOpKind.MeasureFit, piece = asm != null ? asm.id : "", a = (float)CraftMath.Clamp01(accuracy), b = (float)Math.Max(0, size) });
        return true;
    }

    private void ApplyMeasureFit(CraftOperation op)
    {
        CraftPiece asm = Piece(op.piece);
        if (asm != null && asm.kind == CraftPieceKind.Assembly)
        {
            asm.assembly.measured = true;
            asm.assembly.fit = op.a;
            asm.assembly.fitSize = op.b;
            asm.Record(Elapsed, op.kind, op.a > 0.8 ? "The wearer measured carefully" : "The wearer measured", op.a);
            return;
        }
        Variables["fit"] = op.a;
        Variables["fitSize"] = op.b;
        Raise(CraftEventKind.Note, "", "measured", $"Measured ({op.a * 100:0}% accurate).");
    }

    /// <summary>Takes the waiting measurements into an item just started.</summary>
    internal void TakeMeasurements(CraftPiece asm)
    {
        if (!Variables.TryGetValue("fit", out double fit))
            return;
        asm.assembly.measured = true;
        asm.assembly.fit = fit;
        asm.assembly.fitSize = Variables.TryGetValue("fitSize", out double s) ? s : 0;
        Variables.Remove("fit");
        Variables.Remove("fitSize");
    }

    /// <summary>
    /// Lays a pattern out on a hide or a length of cloth and chalks round it (<paramref name="clear"/> = rub every
    /// chalked pattern off). Patterns may not overlap; cutting along a chalked line is cleaner.
    /// </summary>
    public bool LayoutPattern(string id, string pattern, int x, int y, int rotation, bool clear, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.TailorTable) && !Has(CraftTools.LeatherBench))
            return Refuse("There is no cutting table here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Hide)
            return Refuse("That is not cloth or leather.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, id);
        if (clear)
        {
            Record(new CraftOperation { kind = CraftOpKind.LayoutPattern, piece = id, flag = true });
            return true;
        }
        CraftPattern pat = Rules.Pattern(pattern);
        if (pat == null)
            return Refuse("There is no such pattern.", out reason, id);
        if (!CraftLeather.Fits(p.hide, pat, x, y, rotation))
            return Refuse("The pattern does not fit there: it runs off the edge.", out reason, id);
        if (CraftTextiles.Overlaps(Rules, p.hide, pat, x, y, rotation) >= 0)
            return Refuse("It overlaps a pattern already chalked there.", out reason, id);
        if (p.hide.layout.Count >= 24)
            return Refuse("There is no room for more chalk lines.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.LayoutPattern, piece = id, id = pat.id, i0 = x, i1 = y, i2 = ((rotation % 4) + 4) % 4 });
        return true;
    }

    private void ApplyLayout(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        if (op.flag)
        {
            p.hide.layout.Clear();
            return;
        }
        p.hide.layout.Add(new CraftLayoutPlacement { pattern = op.id, x = op.i0, y = op.i1, rotation = op.i2 });
        p.Record(Elapsed, op.kind, "Laid out and chalked");
    }
}
