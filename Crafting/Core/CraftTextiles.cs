using System;
using System.Collections.Generic;

/// <summary>
/// The tailor's: spinning fibre into thread, and cloth. Cloth is a hide-like sheet of cells (<see cref="HideState"/>)
/// from a <see cref="CraftHideDefinition"/> marked <see cref="CraftHideDefinition.textile"/>: it comes woven and
/// finished (no fleshing, liming or tanning), is laid out and cut with patterns at the tailor's table - along its grain
/// (the warp runs along its length: a pattern turned a quarter lies across the grain, which hangs and wears worse) - and
/// sewn with thread. Thread spun by hand is as good as the hands that drafted it: an even draft with the right twist
/// gives fine thread, a lumpy, over- or under-twisted one coarse yarn.
/// </summary>
public static class CraftTextiles
{
    /// <summary>How good a spun hank is (0-1): even drafting, with the twist near the middle (0.5).</summary>
    public static double SpinQuality(double evenness, double twist) =>
        CraftMath.Clamp01(CraftMath.Clamp01(evenness) * (1 - Math.Min(1, Math.Abs(CraftMath.Clamp01(twist) - 0.5) * 1.4)));

    /// <summary>
    /// A twist within this of right (0.5) is well twisted: it keeps nine-tenths of the yarn's quality and more
    /// (<see cref="SpinQuality"/>) - the green band of the wheel's TWIST gauge. Further off it reels a coarser thread.
    /// </summary>
    public const double TwistTrue = 0.07;

    /// <summary>A yarn of this quality reels into the coarse (0), even (1) or fine (2) thread of its fibre (<see cref="CraftFibre.ThreadOf"/>, <see cref="Spun"/>).</summary>
    public static int ThreadTier(double quality) => quality < 0.45 ? 0 : quality < 0.72 ? 1 : 2;

    /// <summary>
    /// The thread a hank of <paramref name="fibre"/> spins into: coarse, even or fine by the quality of the spinning - and
    /// of the fibre's preparation (<paramref name="prep"/>, <see cref="CraftFibre.Prep"/>): raw fibre, greasy and
    /// unopened, never spins fine; that wants a picked, scoured, carded or combed batch. The same tiers as a skein reeled
    /// off as thread (<see cref="ThreadTier"/>).
    /// </summary>
    public static string Spun(CraftRules rules, CraftSupplyDefinition fibre, double evenness, double twist, double prep = 1)
    {
        if (fibre == null || fibre.spinsInto == null || fibre.spinsInto.Count == 0)
            return null;
        double q = SpinQuality(evenness, twist) * (0.55 + 0.45 * CraftMath.Clamp01(prep));
        int tier = Math.Min(ThreadTier(q), fibre.spinsInto.Count - 1);
        for (int t = tier; t >= 0; t--)
            if (rules.Supply(fibre.spinsInto[t]) != null)
                return fibre.spinsInto[t];
        return null;
    }

    /// <summary>How ready a fibre is to spin as it comes, unprepared (see <see cref="CraftFibre.Prep"/>).</summary>
    public static double RawPrep(CraftRules rules, string fibre)
    {
        CraftPiece p = CraftFibre.NewFibre(rules, fibre);
        return p != null ? CraftFibre.Prep(p.fibre) : 0.3;
    }

    /// <summary>The wearer's chest (mm): their own, from their name - the same wherever it is measured.</summary>
    public static double ChestOf(string wearer) => 880 + (CraftMath.Hash(string.IsNullOrEmpty(wearer) ? "wearer" : wearer) & 255);

    // ---- the fit of clothes made to measure (CraftEvaluator's fit)
    /// <summary>How well clothes made without measuring fit (×).</summary>
    public const double FitUnmeasured = 0.95;
    /// <summary>Clothes made to a measure fit ×(<see cref="FitMeasured"/> + <see cref="FitPerAccuracy"/> × its accuracy).</summary>
    public const double FitMeasured = 0.9, FitPerAccuracy = 0.12;
    /// <summary>A measure this accurate is called "made to measure" (careful).</summary>
    public const double FitCareful = 0.8;
    /// <summary>A measure less accurate than this fits worse than none (≈0.42).</summary>
    public const double FitWorthIt = (FitUnmeasured - FitMeasured) / FitPerAccuracy;

    /// <summary>How well clothes fit their wearer when made to a measure of <paramref name="accuracy"/> (0-1), or without one.</summary>
    public static double FitOf(bool measured, double accuracy) => measured ? FitMeasured + FitPerAccuracy * CraftMath.Clamp01(accuracy) : FitUnmeasured;

    /// <summary>
    /// Is an item of <paramref name="d"/> made to the wearer's measure (the tape's: a chest) - a garment, a padded coat:
    /// a design with a part cut from cloth? Its fit counts (<see cref="FitOf"/>, the evaluator's fit). A shield, a weapon,
    /// leather armour or a jewel takes no chest measure: one taken waits for the next garment started.
    /// </summary>
    public static bool MadeToMeasure(CraftDesign d)
    {
        if (d == null || d.kind == CraftDesignKind.Jewelry)
            return false;
        foreach (CraftComponentSpec c in d.components)
            if (c != null && c.kind == CraftPieceKind.Hide && c.leather != null && c.leather.fabric == CraftFabric.Cloth)
                return true;
        return false;
    }

    /// <summary>Is this piece cloth (a textile sheet) rather than a hide?</summary>
    public static bool IsCloth(CraftRules rules, CraftPiece p) => p != null && p.kind == CraftPieceKind.Hide && (rules.Hide(p.hide.hide)?.textile ?? false);

    /// <summary>A cut part of cloth lies along the grain (its pattern turned 0 or 2 quarter turns) or across it.</summary>
    public static bool OnGrain(HideState h) => h.rotation % 2 == 0;

    /// <summary>
    /// Is a joint a cloth seam - sewn with the needle - rather than leather work for the awl? Its parts (those that are
    /// not supplies, nor the fasteners sewn on: a clasp sewn onto a cloak is sewn as its cloth is) are cloth: the fitted
    /// pieces where they are in, else what the design cuts them from (<see cref="CraftLeatherSpec.fabric"/>).
    /// </summary>
    public static bool ClothSeam(CraftWorkshop ws, CraftPiece asm, CraftJointSpec joint)
    {
        CraftDesign d = asm != null && asm.kind == CraftPieceKind.Assembly ? ws.Rules.Design(asm.assembly.design) : null;
        if (d == null || joint == null)
            return false;
        bool any = false;
        foreach (string role in new[] { joint.roleA, joint.roleB })
        {
            if (string.IsNullOrEmpty(role)) continue;
            CraftComponentSpec c = d.Component(role);
            if (c == null || c.supply || c.kind == CraftPieceKind.Fasteners) continue;
            CraftPiece part = ws.Piece(asm.assembly.PieceIn(role));
            bool cloth = part != null ? IsCloth(ws.Rules, part) : c.kind == CraftPieceKind.Hide && c.leather != null && c.leather.fabric == CraftFabric.Cloth;
            if (!cloth) return false;
            any = true;
        }
        return any;
    }

    /// <summary>Is <paramref name="pattern"/> cut from cloth by any design (a part of fabric Cloth or Either)?</summary>
    public static bool ClothPattern(CraftRules rules, string pattern)
    {
        foreach (CraftDesign d in rules.Designs)
            foreach (CraftComponentSpec c in d.components)
                if (c.kind == CraftPieceKind.Hide && c.leather != null && c.leather.fabric != CraftFabric.Leather && c.leather.pattern == pattern)
                    return true;
        return false;
    }

    /// <summary>
    /// The cloth parts the unfinished items on a workshop still want, by pattern: (pattern, how many, the item's name),
    /// in the order the items lie there - what a tailor cuts next.
    /// </summary>
    public static List<(string pattern, int count, string item)> PatternsWanted(CraftWorkshop ws)
    {
        var wanted = new List<(string, int, string)>();
        if (ws == null)
            return wanted;
        foreach (CraftPiece a in ws.Pieces)
        {
            if (a.kind != CraftPieceKind.Assembly || a.assembly.finished) continue;
            CraftDesign d = ws.Rules.Design(a.assembly.design);
            if (d == null) continue;
            foreach (CraftComponentSpec c in d.components)
            {
                if (c.supply || c.kind != CraftPieceKind.Hide || c.leather == null || c.leather.fabric == CraftFabric.Leather || string.IsNullOrEmpty(c.leather.pattern)) continue;
                int missing = 0;
                for (int i = 0; i < Math.Max(1, c.count); i++)
                    if (ws.Piece(a.assembly.PieceIn(c.role, i)) == null) missing++;
                if (missing > 0) wanted.Add((c.leather.pattern, missing, d.Name));
            }
        }
        return wanted;
    }

    /// <summary>The index of a chalked placement the pattern at (x, y) would overlap (-1 = none).</summary>
    public static int Overlaps(CraftRules rules, HideState h, CraftPattern pat, int x, int y, int rotation) => Overlaps(rules, h, pat, x, y, rotation, -1);

    /// <summary>
    /// The index of a chalked placement other than <paramref name="except"/> (the chalk the pattern is laid on) that the
    /// pattern at (x, y) would overlap (-1 = none): a cut there rubs that chalk out.
    /// </summary>
    public static int Overlaps(CraftRules rules, HideState h, CraftPattern pat, int x, int y, int rotation, int except)
    {
        pat.Size(rotation, out int w, out int hh);
        for (int i = 0; i < h.layout.Count; i++)
        {
            if (i == except) continue;
            CraftLayoutPlacement l = h.layout[i];
            CraftPattern other = rules.Pattern(l.pattern);
            if (other == null) continue;
            other.Size(l.rotation, out int ow, out int oh);
            if (x >= l.x + ow || l.x >= x + w || y >= l.y + oh || l.y >= y + hh) continue;
            for (int py = 0; py < hh; py++)
                for (int px = 0; px < w; px++)
                    if (pat.Covers(px, py, rotation) && other.Covers(x + px - l.x, y + py - l.y, l.rotation))
                        return i;
        }
        return -1;
    }

    /// <summary>How much of the sheet the chalked layout uses (0-1): the share of its cells under a pattern.</summary>
    public static double Utilization(CraftRules rules, HideState h)
    {
        int present = 0, used = 0;
        for (int y = 0; y < h.height; y++)
            for (int x = 0; x < h.width; x++)
            {
                HideCell c = h.Cell(x, y);
                if (c == null || !c.present) continue;
                present++;
                foreach (CraftLayoutPlacement l in h.layout)
                {
                    CraftPattern p = rules.Pattern(l.pattern);
                    if (p != null && p.Covers(x - l.x, y - l.y, l.rotation)) { used++; break; }
                }
            }
        return present > 0 ? (double)used / present : 0;
    }

    /// <summary>How far off the grain the chalked patterns lie (share of placements turned a quarter).</summary>
    public static double OffGrain(HideState h)
    {
        if (h.layout.Count == 0) return 0;
        int off = 0;
        foreach (CraftLayoutPlacement l in h.layout) if (l.rotation % 2 != 0) off++;
        return (double)off / h.layout.Count;
    }

    /// <summary>
    /// How well a thread suits what it sews (0-1): heavy leather wants a strong thread (<see cref="ThreadNeed"/>); fine
    /// cloth puckers under a coarse one. <paramref name="thickness"/> = the material (mm), <paramref name="strength"/> =
    /// the thread's.
    /// </summary>
    public static double ThreadSuits(double thickness, double strength)
    {
        if (strength <= 0) return 1;
        double need = ThreadNeed(thickness);
        if (strength < need) return Math.Exp(-(need - strength) / 0.45);
        return 1 - Math.Min(0.25, Math.Max(0, strength - need * 2.2) * 0.15);
    }

    /// <summary>
    /// The thread strength a material of this thickness wants (1 = linen thread): it rises quickly through cloth (up to
    /// 3 mm), then slowly through leather - so the strongest thread (silk, 1.5) suits the thickest sole (9 mm), hemp twine
    /// leather to about 6 mm, waxed thread to 5 mm, linen only up to 2 mm.
    /// </summary>
    public static double ThreadNeed(double thickness)
    {
        double t = Math.Max(0, thickness);
        return t <= 3 ? 0.6 + 0.2 * t : 1.2 + 0.05 * (t - 3);
    }
}
