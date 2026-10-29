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
    /// The thread a hank of <paramref name="fibre"/> spins into: coarse, even or fine by the quality of the spinning - and
    /// of the fibre's preparation (<paramref name="prep"/>, <see cref="CraftFibre.Prep"/>): raw fibre, greasy and
    /// unopened, never spins fine; that wants a picked, scoured, carded or combed batch.
    /// </summary>
    public static string Spun(CraftRules rules, CraftSupplyDefinition fibre, double evenness, double twist, double prep = 1)
    {
        if (fibre == null || fibre.spinsInto == null || fibre.spinsInto.Count == 0)
            return null;
        double q = SpinQuality(evenness, twist) * (0.55 + 0.45 * CraftMath.Clamp01(prep));
        int tier = q < 0.45 ? 0 : q < 0.75 ? 1 : 2;
        tier = Math.Min(tier, fibre.spinsInto.Count - 1);
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

    /// <summary>Is this piece cloth (a textile sheet) rather than a hide?</summary>
    /// <summary>The wearer's chest (mm): their own, from their name - the same wherever it is measured.</summary>
    public static double ChestOf(string wearer) => 880 + (CraftMath.Hash(string.IsNullOrEmpty(wearer) ? "wearer" : wearer) & 255);

    public static bool IsCloth(CraftRules rules, CraftPiece p) => p != null && p.kind == CraftPieceKind.Hide && (rules.Hide(p.hide.hide)?.textile ?? false);

    /// <summary>A cut part of cloth lies along the grain (its pattern turned 0 or 2 quarter turns) or across it.</summary>
    public static bool OnGrain(HideState h) => h.rotation % 2 == 0;

    /// <summary>The index of a chalked placement the pattern at (x, y) would overlap (-1 = none).</summary>
    public static int Overlaps(CraftRules rules, HideState h, CraftPattern pat, int x, int y, int rotation)
    {
        pat.Size(rotation, out int w, out int hh);
        for (int i = 0; i < h.layout.Count; i++)
        {
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
    /// How well a thread suits what it sews (0-1): heavy leather wants a strong thread; fine cloth puckers under a
    /// coarse one. <paramref name="thickness"/> = the material (mm), <paramref name="strength"/> = the thread's.
    /// </summary>
    public static double ThreadSuits(double thickness, double strength)
    {
        if (strength <= 0) return 1;
        double need = 0.6 + 0.2 * Math.Max(0, thickness);
        if (strength < need) return Math.Exp(-(need - strength) / 0.45);
        return 1 - Math.Min(0.25, Math.Max(0, strength - need * 2.2) * 0.15);
    }
}
