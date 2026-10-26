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

    /// <summary>The thread a hank of <paramref name="fibre"/> spins into: coarse, even or fine by the quality of the spinning.</summary>
    public static string Spun(CraftRules rules, CraftSupplyDefinition fibre, double evenness, double twist)
    {
        if (fibre == null || fibre.spinsInto == null || fibre.spinsInto.Count == 0)
            return null;
        double q = SpinQuality(evenness, twist);
        int tier = q < 0.45 ? 0 : q < 0.75 ? 1 : 2;
        tier = Math.Min(tier, fibre.spinsInto.Count - 1);
        for (int t = tier; t >= 0; t--)
            if (rules.Supply(fibre.spinsInto[t]) != null)
                return fibre.spinsInto[t];
        return null;
    }

    /// <summary>Is this piece cloth (a textile sheet) rather than a hide?</summary>
    public static bool IsCloth(CraftRules rules, CraftPiece p) => p != null && p.kind == CraftPieceKind.Hide && (rules.Hide(p.hide.hide)?.textile ?? false);

    /// <summary>A cut part of cloth lies along the grain (its pattern turned 0 or 2 quarter turns) or across it.</summary>
    public static bool OnGrain(HideState h) => h.rotation % 2 == 0;
}
