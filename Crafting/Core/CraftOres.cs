using System;
using System.Collections.Generic;

/// <summary>
/// Ores: what a lump of a deposit holds (deterministic from the deposit's seed and the lump's index: two deposits of
/// one ore differ, lumps of one deposit vary a little), and the cleaning that comes before smelting. Crushing breaks a
/// fragment into smaller ones - the smaller they get, the more the mineral and the rock come apart (some fragments
/// rich, some barren) - but too fine and it is lost in the wash and chokes the furnace. Discarding takes a fragment
/// away (barren rock, or by mistake rich ore). Washing carries dirt away and, from the fines, light rock faster than
/// heavy mineral (a good hand keeps the heavy part). Sieving separates fines from lumps. None of it removes what is
/// bound inside the mineral: only the furnace separates that.
/// </summary>
public static class CraftOres
{
    /// <summary>Fragments below this size (cm) are fines.</summary>
    public const double FineSize = 1.0;
    /// <summary>The most fragments a lump is broken into.</summary>
    public const int MaxFragments = 48;

    /// <summary>A raw lump from <paramref name="deposit"/> (a seed: the same deposit and index always give the same lump).</summary>
    public static CraftPiece NewLump(CraftRules rules, string oreId, int deposit, int lump)
    {
        CraftOreDefinition o = rules.Ore(oreId);
        if (o == null)
            return null;
        var p = new CraftPiece { kind = CraftPieceKind.Ore, origin = $"ore:{o.id}" };
        p.ore.ore = o.id;
        p.ore.deposit = deposit;
        p.ore.lump = lump;
        p.ore.temperature = rules.Settings.defaultAmbient;
        for (int k = 0; k < o.yields.Count; k++)
        {
            CraftOreYield y = o.yields[k];
            double v = CraftMath.Lerp(y.min, y.max, CraftMath.Noise(deposit, k, 11));
            v *= 1 + 0.15 * (CraftMath.Noise(deposit, lump * 31 + k, 12) - 0.5);
            p.ore.bound.Add(new CraftAmount(y.element, Math.Max(0, v)));
        }
        double mineral = CraftMath.Lerp(o.mineralMin, o.mineralMax, 0.65 * CraftMath.Noise(deposit, 0, 13) + 0.35 * CraftMath.Noise(deposit, lump, 14));
        double dirt = CraftMath.Lerp(o.dirtMin, o.dirtMax, CraftMath.Noise(deposit, lump, 15));
        if (mineral + dirt > 1) dirt = 1 - mineral;
        double mass = o.lumpMass * (0.8 + 0.4 * CraftMath.Noise(deposit, lump, 16));
        p.ore.fragments.Add(new OreFragment
        {
            mass = mass, mineral = mineral, dirt = dirt, gangue = Math.Max(0, 1 - mineral - dirt), size = 10 + 6 * CraftMath.Noise(deposit, lump, 17),
            x = 0.5, y = 0.5,
        });
        p.ore.moisture = 0.05 + 0.1 * CraftMath.Noise(deposit, lump, 18);
        return p;
    }

    /// <summary>
    /// A blow on fragment <paramref name="index"/> with <paramref name="force"/> (about 1): it breaks into two to four
    /// smaller fragments; the smaller they are, the more unevenly mineral and rock part between them.
    /// </summary>
    public static void Crush(CraftWorkshop ws, CraftPiece p, int index, double force)
    {
        if (p == null || p.kind != CraftPieceKind.Ore || index < 0 || index >= p.ore.fragments.Count)
            return;
        OreFragment f = p.ore.fragments[index];
        CraftOreDefinition o = ws.Rules.Ore(p.ore.ore);
        double hardness = o != null ? o.hardness / 10.0 : 0.5;
        int seed = CraftMath.Hash(p.id) ^ (p.ore.crushes * 7919);
        p.ore.crushes++;
        double k = force / (0.4 + hardness);
        if (k < 0.25 || p.ore.fragments.Count >= MaxFragments)
        {
            f.size = Math.Max(0.2, f.size * (1 - 0.05 * k)); // it only chips
            return;
        }
        int children = Math.Min(MaxFragments - p.ore.fragments.Count + 1, 2 + (k > 1.2 ? 2 : k > 0.7 ? 1 : 0));
        if (children < 2)
            return;
        // Masses: uneven halves; sizes: smaller; mineral: more uneven the smaller they are (liberation).
        var w = new double[children];
        double sum = 0;
        for (int i = 0; i < children; i++)
        {
            w[i] = 0.4 + CraftMath.Noise(seed, i, 1);
            sum += w[i];
        }
        double size = f.size * (0.4 + 0.2 * CraftMath.Noise(seed, 0, 2)) * (1 - 0.25 * Math.Min(1, k - 0.25));
        double spread = 1.4 * (1 - CraftMath.Clamp01(size / 14));
        var shares = new double[children];
        double mineralMass = f.mass * f.mineral, rawMineral = 0;
        for (int i = 0; i < children; i++)
        {
            shares[i] = Math.Max(0, f.mineral * (1 + spread * (CraftMath.Noise(seed, i, 3) * 2 - 1)));
            rawMineral += f.mass * w[i] / sum * shares[i];
        }
        double scale = rawMineral > 1e-12 ? mineralMass / rawMineral : 0;
        var made = new List<OreFragment>(children);
        double placed = 0;
        for (int i = 0; i < children; i++)
        {
            double m = f.mass * w[i] / sum;
            double mineral = Math.Min(1 - f.dirt, shares[i] * scale);
            placed += m * mineral;
            double a = CraftMath.Noise(seed, i, 4) * Math.PI * 2, r = 0.04 + 0.08 * CraftMath.Noise(seed, i, 5);
            made.Add(new OreFragment
            {
                mass = m, mineral = mineral, dirt = f.dirt, gangue = 0, size = Math.Max(0.15, size * (0.7 + 0.6 * CraftMath.Noise(seed, i, 6))),
                x = CraftMath.Clamp(f.x + Math.Cos(a) * r, 0.05, 0.95), y = CraftMath.Clamp(f.y + Math.Sin(a) * r, 0.05, 0.95),
            });
        }
        // What the clamp could not place goes to the fragments with room for it: the mineral is all still there.
        double left = mineralMass - placed;
        for (int i = 0; i < made.Count && left > 1e-12; i++)
        {
            double room = made[i].mass * (1 - made[i].dirt - made[i].mineral);
            double put = Math.Min(room, left);
            made[i].mineral += put / made[i].mass;
            left -= put;
        }
        foreach (OreFragment c in made)
            c.gangue = Math.Max(0, 1 - c.mineral - c.dirt);
        p.ore.fragments.RemoveAt(index);
        p.ore.fragments.InsertRange(index, made);
        p.Record(ws.Elapsed, CraftOpKind.CrushOre, "Crushed", force);
    }

    /// <summary>Throws fragment <paramref name="index"/> away (onto the waste heap).</summary>
    public static void Discard(CraftWorkshop ws, CraftPiece p, int index)
    {
        if (p == null || p.kind != CraftPieceKind.Ore || index < 0 || index >= p.ore.fragments.Count)
            return;
        OreFragment f = p.ore.fragments[index];
        p.ore.fragments.RemoveAt(index);
        p.Record(ws.Elapsed, CraftOpKind.DiscardFragment, "Picked out a fragment", f.mineral);
        if (p.ore.fragments.Count == 0)
            ws.Destroy(p, "Nothing is left of the lump.");
    }

    /// <summary>
    /// Washes the ore for <paramref name="seconds"/> with a <paramref name="flow"/> of water (0-1) and a
    /// <paramref name="technique"/> (0 careless - 1 a practised swirl that keeps the heavy part).
    /// </summary>
    public static void Wash(CraftWorkshop ws, CraftPiece p, double flow, double seconds, double technique)
    {
        if (p == null || p.kind != CraftPieceKind.Ore)
            return;
        CraftOreDefinition o = ws.Rules.Ore(p.ore.ore);
        double mineralDensity = o != null ? o.density : 4;
        double gangueDensity = o != null && o.gangue == CraftGangue.Clay ? 2.2 : 2.65;
        flow = CraftMath.Clamp01(flow);
        technique = CraftMath.Clamp01(technique);
        double work = flow * Math.Max(0, seconds);
        for (int i = p.ore.fragments.Count - 1; i >= 0; i--)
        {
            OreFragment f = p.ore.fragments[i];
            double dirt = f.mass * f.dirt, mineral = f.mass * f.mineral, gangue = f.mass * f.gangue;
            dirt *= Math.Exp(-work * 0.15 * (1 + 2 / Math.Max(0.2, f.size)) * (o != null && o.gangue == CraftGangue.Clay ? 0.6 : 1));
            if (f.size < FineSize)
            {
                // Fines: light rock goes first; heavy mineral stays with a good hand, and with a heavy mineral.
                double loss = work * 0.02 * (1.5 - technique) * (1.2 - f.size);
                gangue *= Math.Exp(-loss);
                mineral *= Math.Exp(-loss * Math.Pow(gangueDensity / mineralDensity, 1 + 2 * technique));
            }
            double m = dirt + mineral + gangue;
            if (m < 1e-6)
            {
                p.ore.fragments.RemoveAt(i);
                continue;
            }
            f.mass = m;
            f.dirt = dirt / m;
            f.mineral = mineral / m;
            f.gangue = gangue / m;
        }
        p.ore.moisture = Math.Min(1, p.ore.moisture + work * 0.05);
        p.Record(ws.Elapsed, CraftOpKind.WashOre, "Washed", flow);
        if (p.ore.fragments.Count == 0)
            ws.Destroy(p, "It was all washed away.");
    }

    /// <summary>What the ore piece would give per gram if it were all reduced (by element), with its mineral share.</summary>
    public static double MetalContent(CraftRules rules, CraftPiece p, string element)
    {
        double mineral = p.ore.MineralMass, mass = p.ore.Mass;
        return mass > 1e-9 ? mineral * p.ore.Bound(element) / mass : 0;
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ ore cleaning
    /// <summary>A blow of the hammer on a fragment of an ore lump (the breaking stone).</summary>
    public bool CrushOre(string id, int fragment, double force, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.OreBench))
            return Refuse("There is no breaking stone here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Ore)
            return Refuse("That is not ore.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, id);
        if (fragment < 0 || fragment >= p.ore.fragments.Count)
            return Refuse("There is no such fragment.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.CrushOre, piece = id, i0 = fragment, a = (float)Math.Max(0, force) });
        return true;
    }

    /// <summary>Throws a fragment of an ore lump away.</summary>
    public bool DiscardFragment(string id, int fragment, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null || p.kind != CraftPieceKind.Ore)
            return Refuse("That is not ore.", out reason, id);
        if (fragment < 0 || fragment >= p.ore.fragments.Count)
            return Refuse("There is no such fragment.", out reason, id);
        if (p.ore.fragments.Count == 1)
            return Refuse("That is all there is of it.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.DiscardFragment, piece = id, i0 = fragment });
        return true;
    }

    /// <summary>Washes an ore lump in the trough.</summary>
    public bool WashOre(string id, double flow, double seconds, double technique, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.OreBench))
            return Refuse("There is no washing trough here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Ore)
            return Refuse("That is not ore.", out reason, id);
        if (seconds <= 0 || flow <= 0)
            return Refuse("No water ran over it.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.WashOre, piece = id, a = (float)flow, b = (float)seconds, c = (float)technique });
        return true;
    }

    /// <summary>
    /// Sieves an ore lump's fragments: those smaller than <paramref name="mesh"/> (cm) fall through into a new piece.
    /// Returns its id (null when nothing, or everything, falls through).
    /// </summary>
    public string SieveOre(string id, double mesh, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.OreBench))
        {
            Refuse("There is no sieve here.", out reason, id);
            return null;
        }
        if (p == null || p.kind != CraftPieceKind.Ore)
        {
            Refuse("That is not ore.", out reason, id);
            return null;
        }
        int small = 0;
        foreach (OreFragment f in p.ore.fragments)
            if (f.size < mesh) small++;
        if (small == 0 || small == p.ore.fragments.Count)
        {
            Refuse(small == 0 ? "Nothing falls through the sieve." : "It all falls through the sieve.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.SieveOre, piece = id, a = (float)mesh };
        Record(op);
        return op.other;
    }

    private void ApplySieve(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        CraftPiece fines = Create(op, CraftPieceKind.Ore, $"sieved from {p.origin}");
        fines.ore = Copy(p.ore);
        fines.ore.fragments.Clear();
        for (int i = p.ore.fragments.Count - 1; i >= 0; i--)
            if (p.ore.fragments[i].size < op.a)
            {
                fines.ore.fragments.Insert(0, p.ore.fragments[i]);
                p.ore.fragments.RemoveAt(i);
            }
        p.Record(Elapsed, op.kind, $"Sieved at {op.a:0.#} cm");
        fines.Record(Elapsed, op.kind, $"Fell through a {op.a:0.#} cm sieve");
    }

}
