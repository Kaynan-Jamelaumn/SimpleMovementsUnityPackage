using System;
using UnityEngine;

/// <summary>
/// A skinned hide becomes a raw hide of the crafting system - a worked piece for the tannery (the fleshing beam, the
/// vats, the frame) - with every cell as the fight and the skinning left it: holes from arrows, slashes and bites where
/// they were, burned patches with the hair singed off, tears and nicks from a careless knife, flesh left on where the
/// hide was pulled badly, and rot where it had started to turn. The tannery lays hides out with the spine running across
/// (its width is the hide's length), so the pelt grid is turned to match.
/// </summary>
public static class HarvestHides
{
    /// <summary>
    /// The crafting hide piece for a skinned hide (<paramref name="skinned"/>: the pelt's cells as they came off), or null
    /// when the material names no tannery hide the Crafting Database knows.
    /// </summary>
    public static CraftPiece Piece(HarvestMaterial m, HarvestCondition skinned, double freshness, CraftRules rules)
    {
        if (m == null || skinned == null || rules == null || string.IsNullOrWhiteSpace(m.craftHide))
            return null;
        CraftHideDefinition d = rules.Hide(m.craftHide.Trim());
        if (d == null)
            return null;
        CraftPiece p = CraftLeather.NewHide(rules, d.id, skinned.seed);
        if (p == null)
            return null;
        HideState h = p.hide;
        int pw = Math.Max(1, skinned.peltWidth), ph = Math.Max(1, skinned.peltHeight);
        double rot = Math.Max(0, 0.45 - freshness) / 0.45; // starting to turn: some rot
        for (int cy = 0; cy < h.height; cy++)
            for (int cx = 0; cx < h.width; cx++)
            {
                HideCell c = h.cells[cy * h.width + cx];
                // The tannery's x runs head to tail (the pelt's y), its y across (the pelt's x).
                int ax = Math.Min(pw - 1, (int)((cy + 0.5) / h.height * pw));
                int ay = Math.Min(ph - 1, (int)((cx + 0.5) / h.width * ph));
                HarvestPeltCell pc = skinned.Cell(ax, ay);
                if (pc == null || !pc.present || pc.lost)
                {
                    c.present = false;
                    continue;
                }
                c.present = true;
                double fight = Math.Min(1.0, pc.damage + Math.Max(0f, pc.burn - 0.6f));
                c.damage = Math.Max(c.damage * 0.5, Math.Min(1.0, fight + pc.harvest));
                if (pc.damage > 0.3f || pc.harvest > 0.3f) c.flaw = true;
                c.flesh = Math.Max(0.0, Math.Min(1.0, d.fat * 0.35 + pc.flesh * 0.8));
                c.hair *= 1.0 - Math.Min(1.0, pc.burn);
                c.grain = Math.Max(0.0, 1.0 - Math.Min(1.0, pc.burn * 0.6 + fight * 0.5));
                c.rot = Math.Max(c.rot, Math.Min(1.0, rot * (0.6 + 0.4 * HarvestMath.Noise(skinned.seed, cy * 97 + cx, 3))));
            }
        p.origin = $"hide:{d.id}";
        return p;
    }
}
