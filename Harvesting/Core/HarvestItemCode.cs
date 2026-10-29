using System;
using System.Collections.Generic;

/// <summary>
/// The value each harvested piece keeps in its copy of the item (the inventory item's durability list, one value per
/// piece, like a potion's strength): its quality and its marks. It survives stacking, splitting, dropping, storage,
/// saves and the network, so a stack of claws remembers which are pristine and which cracked.
/// <para>Layout: 2 + quality (0-1000, 10 bits) + marks (<see cref="HarvestMarks"/>, 20 bits) shifted by 10, at most
/// 1,073,741,802. 0 and 1 (what the inventory gives a copy with no value of its own: bought, looted from a table,
/// dropped by an Item Spawner) are pieces without a value: they count as <see cref="UnknownQuality"/>.</para>
/// </summary>
public static class HarvestItemCode
{
    /// <summary>The quality of a piece that carries no value (bought or dropped by a loot table).</summary>
    public const double UnknownQuality = 0.6;

    public static int Encode(double quality, HarvestMarks marks)
    {
        int q = (int)Math.Round(HarvestMath.Clamp01(quality) * 1000.0);
        int m = (int)marks & HarvestMarksInfo.Mask;
        return 2 + (q | (m << 10));
    }

    /// <summary>Reads a piece's value. False for 0 and 1 (no value) and numbers that are not piece values.</summary>
    public static bool TryDecode(int code, out double quality, out HarvestMarks marks)
    {
        quality = UnknownQuality;
        marks = HarvestMarks.None;
        if (code <= 1)
            return false;
        int v = code - 2;
        int q = v & 1023;
        if (q > 1000 || (v >> 30) != 0)
            return false;
        quality = q / 1000.0;
        marks = (HarvestMarks)((v >> 10) & HarvestMarksInfo.Mask);
        return true;
    }

    /// <summary>The quality of a value (<see cref="UnknownQuality"/> for 0).</summary>
    public static double QualityOf(int code) => TryDecode(code, out double q, out _) ? q : UnknownQuality;

    /// <summary>
    /// How the pieces of a stack's values grade: "2 Pristine, 1 Poor" (best first). Values without a quality count as
    /// unknown.
    /// </summary>
    public static string Summary(IList<int> codes)
    {
        if (codes == null || codes.Count == 0)
            return "";
        var counts = new int[Enum.GetValues(typeof(HarvestGrade)).Length];
        int unknown = 0;
        foreach (int c in codes)
        {
            if (!TryDecode(c, out double q, out _)) { unknown++; continue; }
            counts[(int)HarvestText.Grade(q)]++;
        }
        var parts = new List<string>();
        for (int g = counts.Length - 1; g >= 0; g--)
            if (counts[g] > 0)
                parts.Add($"<color={HarvestText.GradeHex((HarvestGrade)g)}>{counts[g]} {(HarvestGrade)g}</color>");
        if (unknown > 0)
            parts.Add($"{unknown} unknown");
        return string.Join(", ", parts);
    }

    /// <summary>The mean quality of a stack's values (unknown ones as <see cref="UnknownQuality"/>).</summary>
    public static double MeanQuality(IList<int> codes)
    {
        if (codes == null || codes.Count == 0)
            return UnknownQuality;
        double sum = 0;
        foreach (int c in codes) sum += QualityOf(c);
        return sum / codes.Count;
    }

    /// <summary>
    /// What a piece of <paramref name="quality"/> is worth against a perfect one: a ruined piece fetches a tenth, a
    /// pristine one the full price.
    /// </summary>
    public static double ValueFactor(double quality) => 0.1 + 0.9 * Math.Pow(HarvestMath.Clamp01(quality), 1.4);
}
