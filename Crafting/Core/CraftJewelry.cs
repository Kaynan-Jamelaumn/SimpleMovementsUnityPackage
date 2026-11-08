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

    /// <summary>A push's pressure (0-1) above which a brittle stone (toughness under <see cref="ChipToughness"/>) chips.</summary>
    public const double ChipPress = 0.8, ChipToughness = 0.55;

    /// <summary>A seat this much (mm) smaller than the girdle still takes the stone; up to <see cref="SeatSnug"/> wider it is snug.</summary>
    public const double SeatTooSmall = 0.15, SeatSnug = 0.08;

    /// <summary>
    /// A setting's inside (its collar, mm) against the stone's girdle: down to <see cref="CollarTooSmall"/> smaller the burr
    /// still opens a seat in its wall; up to <see cref="CollarLoose"/> wider it closes snugly over the stone; past that it
    /// holds less and less, and past <see cref="CollarTooWide"/> it cannot hold the stone at all.
    /// </summary>
    public const double CollarTooSmall = 0.6, CollarLoose = 0.3, CollarTooWide = 1.5;

    /// <summary>How fast a piece comes up on the buffing wheel (of a full buff, a second, with ordinary tools): only the time on the wheel counts.</summary>
    public const double BuffPerSecond = 0.12;

    /// <summary>
    /// The security (0-1) from which a stone is set: one line for the Set place's "it holds" (the joint done), the
    /// inspection (under it, the stone moves in its setting) and the finished item (under it, it is loose).
    /// </summary>
    public const double SetSecure = 0.7;

    /// <summary>
    /// The seat's fit under which a stone can never be set in it (the most it could hold, <see cref="MostSecure"/>, falls
    /// short of <see cref="SetSecure"/>): too wide - a new setting is wanted.
    /// </summary>
    public static double SeatHolds => (SetSecure - 0.35) / 0.65;

    /// <summary>The solder's flow into a joint (0-1, the joint takes from 0.5): the heat (from 0.85 of its flow point, full at 1), the flux and the seam's gap (mm).</summary>
    public static double SolderFlow(double heat, double flux, double gap) => CraftMath.Smooth(0.85, 1.0, heat) * FluxReach(flux, gap);

    /// <summary>The most a joint so fluxed and so gapped can ever take, however it is heated (under 0.5 it never takes).</summary>
    public static double FluxReach(double flux, double gap) => (0.35 + 0.65 * CraftMath.Clamp01(flux)) * Math.Exp(-Math.Max(0, gap) / 0.35);

    /// <summary>The widest seam gap (mm) of the metal parts a joint joins.</summary>
    public static double JointGap(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec)
    {
        double gap = 0;
        foreach (string role in new[] { spec.roleA, spec.roleB })
        {
            CraftPiece p = ws.Piece(asm.assembly.PieceIn(role));
            if (p != null && p.kind == CraftPieceKind.Metal) gap = Math.Max(gap, p.Tag(SeamTag));
        }
        return gap;
    }

    /// <summary>How well a seat <paramref name="err"/> mm wider than the girdle fits (see <see cref="SeatFit"/>).</summary>
    public static double SeatFitOf(double err) => err < -SeatTooSmall ? 0 : err <= SeatSnug ? 1 : Math.Exp(-(err - SeatSnug) / 0.25);

    /// <summary>A seat <paramref name="err"/> mm wider than the stone's girdle, in words (the record's and the window's).</summary>
    public static string SeatWords(double err)
    {
        double fit = SeatFitOf(err);
        return err < -SeatTooSmall ? "still too small for the stone" : fit >= 0.95 ? "snug to the stone" : fit >= SeatHolds ? "a little wide: the stone will not sit tight"
            : "too wide: the stone will rattle - start a new setting";
    }

    /// <summary>How well a setting's collar of <paramref name="slack"/> mm more than the girdle closes over the stone (1 snug - 0 it cannot hold it).</summary>
    public static double CollarFit(double slack) => slack < -CollarTooSmall || slack > CollarTooWide ? 0 : slack <= CollarLoose ? 1 : Math.Exp(-(slack - CollarLoose) / 0.5);

    /// <summary>The collar's slack (mm) round a stone: the setting's inside less the girdle.</summary>
    public static double CollarSlack(CraftPiece setting, CraftPiece stone) =>
        setting == null || stone == null || stone.kind != CraftPieceKind.Gem ? 0 : setting.Tag(SizeTag) - stone.gem.girdle;

    /// <summary>Why a setting cannot take a stone at all (formed far too small or too wide for it), or null.</summary>
    public static string CollarProblem(CraftPiece setting, CraftPiece stone)
    {
        if (setting == null || stone == null || stone.kind != CraftPieceKind.Gem || stone.gem.girdle <= 0 || setting.Tag(SizeTag) <= 0)
            return null;
        double slack = CollarSlack(setting, stone);
        string what = PartOf(setting) == CraftJewelPart.Prongs ? "head" : "bezel";
        if (slack < -CollarTooSmall)
            return $"The {what} is too small for this stone: the seat would cut through its wall - form one to the girdle ({stone.gem.girdle:0.00} mm).";
        if (slack > CollarTooWide)
            return $"The {what} is too wide for this stone ({setting.Tag(SizeTag):0.0} mm round a {stone.gem.girdle:0.00} mm girdle): it could never hold it - form one to the girdle.";
        return null;
    }

    public static CraftJewelPart PartOf(CraftPiece p) => p == null ? CraftJewelPart.None : (CraftJewelPart)(int)Math.Round(p.Tag(PartTag));

    /// <summary>A setting a design holds its stone in by claws: how many claws (0 when it is not a prong setting).</summary>
    public static int ClawsWanted(CraftDesign d, CraftComponentSpec c)
    {
        if (d == null || c == null)
            return 0;
        foreach (CraftJointSpec j in d.joints)
            if (j.kind == CraftJointKind.ProngSet && (j.roleA == c.role || j.roleB == c.role))
                return Math.Max(0, j.count);
        return 0;
    }

    /// <summary>The wearer's ring finger (inner diameter, mm): their own, from their name - the same wherever it is measured.</summary>
    public static double FingerOf(string wearer) => 15.5 + (CraftMath.Hash(string.IsNullOrEmpty(wearer) ? "wearer" : wearer) & 63) / 63.0 * 6.0;

    /// <summary>Where a finger's measurement waits for the next ring (the tape's chest waits in "fit" / "fitSize").</summary>
    public const string FingerFitVar = "fingerFit", FingerSizeVar = "fingerSize";

    /// <summary>Is a measurement of this size (mm) a finger's - the ring sizer's (under 60 mm) - rather than a chest's (the tape's)?</summary>
    public static bool IsFinger(double size) => size > 0 && size < 60;

    /// <summary>
    /// Does an item of <paramref name="d"/> take a finger's measurement (<paramref name="finger"/>: a ring) or a chest's
    /// (a garment: <see cref="CraftTextiles.MadeToMeasure"/>, where the fit counts)? Other jewellery, weapons, shields and
    /// leather armour take none.
    /// </summary>
    public static bool Takes(CraftDesign d, bool finger) =>
        d != null && (finger ? d.kind == CraftDesignKind.Jewelry && d.armorSlot == ArmorSlotType.Ring : CraftTextiles.MadeToMeasure(d));

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
    /// round it came off the mandrel (0-1), <c>c</c> = the gap left at the seam (mm). Only the length the part needs is
    /// used: the wire or strip is cut there first and the rest goes back on the bench as it was (unformed). A hard,
    /// unannealed metal cracks.
    /// </summary>
    public static void FormShank(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Metal)
            return;
        var part = (CraftJewelPart)op.i0;
        double thick = 0;
        foreach (MetalSegment s in p.segments) thick = Math.Max(thick, Math.Min(s.width, s.thickness));
        double need = LengthNeeded(part, Math.Max(0, op.a), thick, op.i1);
        CraftPiece rest = CutToLength(ws, p, need, op); // before the tags, so the offcut is plain wire
        if (rest != null)
            rest.tags.RemoveAll(t => t.name != null && t.name.StartsWith("jewel.", StringComparison.Ordinal)); // even if it was formed before
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
            CraftJewelPart.Prongs => $"Formed into a head of {op.i1} prongs, {op.a:0.##} mm inside",
            CraftJewelPart.Bail => "Formed into a bail",
            CraftJewelPart.Chain => "Bent and closed into a chain",
            _ => "Formed",
        };
        if (rest != null)
            what += $" - {p.Length:0} mm used, {rest.Length:0} mm put back";
        p.Record(ws.Elapsed, CraftOpKind.FormShank, what, op.b);
    }

    /// <summary>
    /// Cuts a length of wire or strip to <paramref name="need"/> mm (to the millimetre: a segment is parted where the
    /// length falls): the far part becomes a piece of its own, created by <paramref name="op"/> (so a replay makes the same
    /// one). Nothing is cut when the piece is no more than a millimetre or two longer. Returns the offcut, or null.
    /// </summary>
    public static CraftPiece CutToLength(CraftWorkshop ws, CraftPiece p, double need, CraftOperation op)
    {
        if (p == null || p.segments.Count == 0 || need <= 0 || p.Length < need + 2)
            return null;
        double pos = 0;
        for (int k = 0; k < p.segments.Count; k++)
        {
            MetalSegment s = p.segments[k];
            if (pos + s.length < need - 1e-6)
            {
                pos += s.length;
                continue;
            }
            double into = need - pos;
            if (into > 1e-3 && s.length - into > 1e-3)
            {
                // part this segment where the length falls: two of the same metal, the lengths split
                MetalSegment far = s.Clone();
                far.length = s.length - into;
                s.length = into;
                p.segments.Insert(k + 1, far);
            }
            else if (into <= 1e-3)
                k--; // the length falls on the boundary before this segment
            return k >= 0 && k < p.segments.Count - 1 ? CraftForging.Split(ws, p, k, op, "cut to length") : null;
        }
        return null;
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
        bool gem = stone != null && stone.kind == CraftPieceKind.Gem;
        double err = gem ? Math.Max(was, op.a) - stone.gem.girdle : 0;
        p.Record(ws.Elapsed, CraftOpKind.SeatSetting, gem ? $"Seat cut - {SeatWords(err)}" : "Seat cut", err);
    }

    /// <summary>
    /// How well a seat fits a stone (1 snug, from <see cref="SeatTooSmall"/> under the girdle to <see cref="SeatSnug"/>
    /// over it; less the wider it is; 0 = the stone does not go in).
    /// </summary>
    public static double SeatFit(CraftPiece setting, CraftPiece stone)
    {
        if (setting == null || stone == null || stone.kind != CraftPieceKind.Gem)
            return 0;
        double seat = setting.Tag(SeatTag);
        if (seat <= 0)
            return 0;
        return SeatFitOf(seat - stone.gem.girdle);
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
        double gap = JointGap(ws, asm, spec);
        double flow = SolderFlow(heat, flux, gap);
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

    /// <summary>The most a setting can hold its stone (0-1): a wide seat never holds it fully, nor a collar formed wider than the girdle.</summary>
    public static double MostSecure(CraftPiece setting, CraftPiece stone) =>
        (0.35 + 0.65 * SeatFit(setting, stone)) * CollarFit(CollarSlack(setting, stone));

    /// <summary>
    /// Pushes the bezel or the prongs over the stone: <c>i1</c> = pushes, <c>a</c> = pressure (0-1), <c>b</c> = how
    /// evenly round (opposite sides in turn, 0-1). A wide seat or a collar formed too wide never holds it fully
    /// (<see cref="MostSecure"/>); an uneven seat or uneven pushing tilts it; harder than <see cref="ChipPress"/> on a
    /// brittle stone chips it.
    /// </summary>
    public static void Push(CraftWorkshop ws, CraftPiece asm, CraftJointSpec spec, JointState j, CraftOperation op)
    {
        SettingParts(ws, asm, spec, out CraftPiece setting, out CraftPiece stone);
        if (setting == null || stone == null)
            return;
        CraftGemDefinition gd = ws.Rules.Gem(stone.gem.gem);
        int n = Math.Max(1, op.i1);
        double press = CraftMath.Clamp01(op.a), even = CraftMath.Clamp01(op.b), tq = ws.ToolQuality;
        if (j.pushes == 0)
            j.tilt = CraftMath.Clamp01(j.tilt + (1 - setting.Tag(SeatLevelTag)) * 0.35);
        j.pushes += n;
        bool prongs = spec.kind == CraftJointKind.ProngSet;
        j.security = Math.Min(MostSecure(setting, stone), j.security + n * press * 0.07 * (0.5 + 0.5 * even) * Math.Min(1.2, tq) * (prongs ? 0.8 : 1));
        j.tilt = CraftMath.Clamp01(j.tilt + n * (1 - even) * 0.02);
        if (press > ChipPress && gd != null && gd.toughness < ChipToughness)
        {
            double sev = (press - ChipPress) * 2 * (1 - gd.toughness);
            stone.gem.chips = CraftMath.Clamp01(stone.gem.chips + sev);
            stone.AddDefect(CraftDefectKind.Chip, -1, sev, true, prongs ? "a prong was pushed so hard it chipped the crown" : "the bezel was pushed so hard it chipped the girdle");
        }
    }

    /// <summary>Is a jeweller's joint done: soldered (the solder taken), or its stone set (<see cref="SetSecure"/>; a claw setting with every claw pushed)?</summary>
    public static bool JointDone(CraftJointSpec spec, JointState j)
    {
        switch (spec.kind)
        {
            case CraftJointKind.Solder: return j.solder >= 0.5;
            case CraftJointKind.BezelSet: return j.security >= SetSecure;
            case CraftJointKind.ProngSet: return j.security >= SetSecure && j.pushes >= Math.Max(1, spec.count);
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
            // a claw setting has as many prongs as the design holds its stone in
            int claws = ClawsWanted(d, c), prongs = (int)Math.Round(p.Tag(ProngTag));
            if (want == CraftJewelPart.Prongs && claws >= 3 && prongs != claws)
            {
                reason = $"This piece holds its stone in {claws} claws: form a head of {claws} prongs (this one has {prongs}).";
                return false;
            }
            // a setting going on after the stone: the seat must take it
            if (c.function == CraftComponentFunction.Setting)
            {
                CraftPiece stone = StoneIn(ws, asm);
                string collar = CollarProblem(p, stone);
                if (collar != null)
                {
                    reason = collar;
                    return false;
                }
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
                string collar = CollarProblem(setting, p);
                if (collar != null)
                {
                    reason = collar;
                    return false;
                }
                if (setting != null && SeatFit(setting, p) <= 0)
                {
                    reason = setting.Tag(SeatTag) <= 0 ? "Cut the setting's seat for this stone first." : "The seat is too small for the stone: cut it wider.";
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// How well the jeweller made a metal part of a piece of jewellery (0-1, its workmanship), from the hands' own work:
    /// how round it came off the mandrel, how closed its seam is, and - a setting - how its collar suits the stone and how
    /// level and clean its seat was cut. What is off goes into <paramref name="notes"/>. (Jewellery parts are not judged
    /// against a smith's target shape: a band is as long as its finger needs.)
    /// </summary>
    public static double PartWorkmanship(CraftPiece p, CraftPiece stone, List<string> notes = null)
    {
        CraftJewelPart part = PartOf(p);
        if (p == null || part == CraftJewelPart.None)
        {
            notes?.Add("never formed");
            return 0.3;
        }
        double round = CraftMath.Clamp01(p.Tag(RoundTag)), gap = Math.Max(0, p.Tag(SeamTag));
        double score = (0.55 + 0.45 * round) * (0.6 + 0.4 * Math.Exp(-gap / 0.35));
        if (round < 0.6) notes?.Add("out of round");
        if (gap > 0.3) notes?.Add($"the seam gapes ({gap:0.0} mm)");
        if (part == CraftJewelPart.Bezel || part == CraftJewelPart.Prongs)
        {
            if (stone != null && stone.kind == CraftPieceKind.Gem && stone.gem.girdle > 0 && p.Tag(SizeTag) > 0)
            {
                double slack = CollarSlack(p, stone), collar = CollarFit(slack);
                score *= 0.7 + 0.3 * collar;
                if (collar < 0.8) notes?.Add(slack > 0 ? "the setting is too wide for its stone" : "the setting is too tight round its stone");
            }
            if (p.Tag(SeatTag) > 0)
            {
                double level = CraftMath.Clamp01(p.Tag(SeatLevelTag)), clean = CraftMath.Clamp01(p.Tag(SeatCleanTag));
                score *= (0.85 + 0.15 * level) * (0.85 + 0.15 * clean);
                if (clean < 0.5) notes?.Add("a ragged seat");
                if (level < 0.6) notes?.Add("the seat was cut unlevel");
            }
        }
        return CraftMath.Clamp01(score);
    }

    // ------------------------------------------------------------------ finishing
    /// <summary>
    /// Buffs, cleans and inspects a piece of jewellery: <c>a</c> = how well it was buffed, <c>b</c> = cleaned (0-1). The
    /// buffing takes the stock's scratches off its metal parts.
    /// </summary>
    public static void FinishJewel(CraftWorkshop ws, CraftPiece asm, CraftOperation op)
    {
        if (asm == null || asm.kind != CraftPieceKind.Assembly)
            return;
        double buff = CraftMath.Clamp01(op.a);
        asm.assembly.finish = CraftMath.Clamp01(Math.Max(asm.assembly.finish, 0.6 * buff + 0.4 * CraftMath.Clamp01(op.b)));
        foreach (AssemblyPart part in asm.assembly.parts)
        {
            CraftPiece m = ws.Piece(part.piece);
            if (m == null || m.kind != CraftPieceKind.Metal) continue;
            foreach (MetalSegment s in m.segments)
            {
                s.rough = Math.Min(s.rough, s.rough * (1 - 0.9 * buff));
                s.polish = Math.Max(s.polish, buff);
            }
        }
        CraftDesign d = ws.Rules.Design(asm.assembly.design);
        var found = new List<string>();
        if (d != null)
            foreach (CraftJointSpec spec in d.joints)
            {
                JointState j = asm.assembly.Joint(spec.id);
                if (j == null) continue;
                if ((spec.kind == CraftJointKind.BezelSet || spec.kind == CraftJointKind.ProngSet) && j.security < SetSecure) found.Add("the stone moves in its setting");
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
        if (security < SetSecure / 0.85 - 1e-9) r.Notes.Add("the stone is loose: it may fall out"); // not set (SetSecure)
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
        if (!CanFormShank(id, part, size, prongs, out string problem))
            return Refuse(problem, out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.FormShank, piece = id, i0 = (int)part, i1 = Math.Max(0, prongs), a = (float)Math.Max(0, size), b = (float)CraftMath.Clamp01(roundness), c = (float)CraftMath.Clamp(seamGap, 0, 5) });
        return true;
    }

    /// <summary>
    /// Can <paramref name="id"/> be formed into <paramref name="part"/> of <paramref name="size"/> mm (the bench here, cool
    /// thin metal, long enough)? Changes nothing and raises nothing: the window asks it at the first tap, so no tapping is
    /// wasted on a piece the mandrel would refuse. <see cref="FormShank"/> refuses with the same words.
    /// </summary>
    public bool CanFormShank(string id, CraftJewelPart part, double size, int prongs, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.JewelersBench))
            reason = "There is no jeweller's bench here.";
        else if (p == null || p.kind != CraftPieceKind.Metal)
            reason = "That is not metal.";
        else if (p.location != CraftLocation.Air)
            reason = "Take it out first.";
        else if (part == CraftJewelPart.None)
            reason = "Choose what to form it into.";
        else if (p.MaxTemperature > 300)
            reason = "It is too hot to handle on the mandrel.";
        if (reason.Length > 0)
            return false;
        double t = 0, w = 0;
        foreach (MetalSegment s in p.segments)
        {
            t = Math.Max(t, Math.Min(s.width, s.thickness));
            w = Math.Max(w, Math.Max(s.width, s.thickness));
        }
        double need = CraftJewelry.LengthNeeded(part, size, t, prongs);
        if (t > CraftJewelry.MaxThickness(part) || w > 14)
            reason = $"It is too heavy for that: draw it into wire or roll it into strip (at most {CraftJewelry.MaxThickness(part):0.#} mm thick, 14 mm wide).";
        else if (part == CraftJewelPart.Prongs && (prongs < 3 || prongs > 8))
            reason = "A head has three to eight prongs.";
        else if ((part == CraftJewelPart.Band || part == CraftJewelPart.Bezel || part == CraftJewelPart.Prongs) && (size < 2 || size > 200))
            reason = "That size cannot be formed on the mandrel.";
        else if (p.Length < need)
            reason = $"It is too short for that: it needs {need:0} mm (it is {p.Length:0} mm).";
        return reason.Length == 0;
    }

    /// <summary>
    /// Cuts a setting's seat for <paramref name="stone"/>: its diameter (mm), how level and how clean (0-1). The setting
    /// may already be fitted to the piece (its seat is cut in place); one formed far too small or too wide for the stone
    /// is refused (<see cref="CraftJewelry.CollarProblem"/>).
    /// </summary>
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
        string collar = CraftJewelry.CollarProblem(p, g);
        if (collar != null)
            return Refuse(collar, out reason, setting);
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

    /// <summary>
    /// Takes a measurement: into the item it was taken for when that item takes this kind of measurement (a ring the
    /// finger's, a garment the chest's: <see cref="CraftJewelry.Takes"/>), otherwise it waits - each kind on its own - for
    /// the next item started that takes it (a shield started meanwhile does not use up a chest's measure).
    /// </summary>
    private void ApplyMeasureFit(CraftOperation op)
    {
        bool finger = CraftJewelry.IsFinger(op.b);
        CraftPiece asm = Piece(op.piece);
        if (asm != null && asm.kind == CraftPieceKind.Assembly && CraftJewelry.Takes(Rules.Design(asm.assembly.design), finger))
        {
            asm.assembly.measured = true;
            asm.assembly.fit = op.a;
            asm.assembly.fitSize = op.b;
            asm.Record(Elapsed, op.kind, op.a > 0.8 ? "The wearer measured carefully" : "The wearer measured", op.a);
            return;
        }
        Variables[finger ? CraftJewelry.FingerFitVar : "fit"] = op.a;
        Variables[finger ? CraftJewelry.FingerSizeVar : "fitSize"] = op.b;
        // flag: given back by an item put aside (CraftWorkshop.PutAside), not measured anew
        Raise(CraftEventKind.Note, "", "measured", op.flag ? "Its measurements wait for the next item started." : $"Measured ({op.a * 100:0}% accurate).");
    }

    /// <summary>
    /// The measurement waiting for the next item: the finger's (<paramref name="finger"/>) or the chest's. A finger
    /// measured before the two were kept apart (a size under 60 mm in the chest's place) counts as the finger's.
    /// </summary>
    public bool WaitingMeasurement(bool finger, out double fit, out double size) => WaitingMeasurement(finger, out fit, out size, out _, out _);

    private bool WaitingMeasurement(bool finger, out double fit, out double size, out string fitVar, out string sizeVar)
    {
        fitVar = finger ? CraftJewelry.FingerFitVar : "fit";
        sizeVar = finger ? CraftJewelry.FingerSizeVar : "fitSize";
        if (Variables.TryGetValue(fitVar, out fit))
        {
            size = Variables.TryGetValue(sizeVar, out double s) ? s : 0;
            if (finger || !CraftJewelry.IsFinger(size)) // a finger in the chest's place is no chest
                return true;
        }
        if (finger && Variables.TryGetValue("fit", out fit) && Variables.TryGetValue("fitSize", out size) && CraftJewelry.IsFinger(size))
        {
            fitVar = "fit";
            sizeVar = "fitSize";
            return true;
        }
        fit = size = 0;
        return false;
    }

    /// <summary>Takes the waiting measurement of the kind the item takes (a ring the finger's, clothes the chest's) into an item just started.</summary>
    internal void TakeMeasurements(CraftPiece asm)
    {
        CraftDesign d = Rules.Design(asm.assembly.design);
        bool finger = CraftJewelry.Takes(d, true);
        if (!finger && !CraftJewelry.Takes(d, false))
            return;
        if (!WaitingMeasurement(finger, out double fit, out double size, out string fitVar, out string sizeVar))
            return;
        asm.assembly.measured = true;
        asm.assembly.fit = fit;
        asm.assembly.fitSize = size;
        Variables.Remove(fitVar);
        Variables.Remove(sizeVar);
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
        if (!string.IsNullOrEmpty(p.hide.pattern))
            return Refuse(AlreadyCut, out reason, id); // a part is fitted into its item, not laid out again
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
