using System;
using System.Collections.Generic;

/// <summary>The brushes of the ore bench (serialized as numbers: add at the END).</summary>
public enum CraftOreBrush { Soft, Stiff, Wire }

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

    /// <summary>The most pieces a blow can split one of the lump's stones into now: no more than the bench has room for (<see cref="MaxFragments"/>).</summary>
    public static int MostPieces(CraftPiece p) => p?.ore == null ? 0 : Math.Min(4, MaxFragments - p.ore.fragments.Count + 1);

    /// <summary>A raw lump from <paramref name="deposit"/> (a seed: the same deposit and index always give the same lump).</summary>
    public static CraftPiece NewLump(CraftRules rules, string oreId, int deposit, int lump)
    {
        CraftOreDefinition o = rules.Ore(oreId);
        if (o == null)
            return null;
        var p = new CraftPiece { kind = CraftPieceKind.Ore, origin = "a lump of ore" }; // what it is, the player finds out
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
        double size = 10 + 6 * CraftMath.Noise(deposit, lump, 17);
        p.ore.fragments.Add(new OreFragment
        {
            mass = mass, mineral = mineral, dirt = dirt, gangue = Math.Max(0, 1 - mineral - dirt), size = size,
            rind = Rind(size), // the weathered surface of the lump as it came out of the seam
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
        int children = Math.Min(MostPieces(p), 2 + (k > 1.2 ? 2 : k > 0.7 ? 1 : 0));
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
        // The weathered rind was the stone's outside: the pieces share what was left of it with their rock - the faces the
        // blow opened are fresh rock, with no rind of their own.
        double rind = f.RindShare;
        for (int i = 0; i < children; i++)
        {
            double m = f.mass * w[i] / sum;
            double mineral = Math.Min(1 - f.dirt, shares[i] * scale);
            placed += m * mineral;
            double a = CraftMath.Noise(seed, i, 4) * Math.PI * 2, r = 0.04 + 0.08 * CraftMath.Noise(seed, i, 5);
            made.Add(new OreFragment
            {
                mass = m, mineral = mineral, dirt = f.dirt, gangue = 0, size = Math.Max(0.15, size * (0.7 + 0.6 * CraftMath.Noise(seed, i, 6))), rind = rind,
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
    /// <paramref name="technique"/> (0 careless - 1 a practised swirl that keeps the heavy part). A strong flow washes
    /// fast but carries heavy grains off with the light (<see cref="Separation"/>); a gentle one is slow and selective.
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
                mineral *= Math.Exp(-loss * Math.Pow(gangueDensity / mineralDensity, (1 + 2 * technique) * Separation(flow)));
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

    /// <summary>
    /// How sharply a flow of water (0-1) parts the heavy mineral from the light rock among the fines: a gentle trickle
    /// leaves the heavy grains behind (1.5), a full flow carries them off with the rock (0.4); half a flow is 1.
    /// </summary>
    public static double Separation(double flow) => 1.6 - 1.2 * CraftMath.Clamp01(flow);

    /// <summary>
    /// How the lump lies on the bench (its crushings and how many stones): every blow, stone thrown out and sieving
    /// changes it. A request aimed at a stone by its place carries it, so a stone that moved is not struck by mistake.
    /// </summary>
    public static int Layout(CraftPiece p) => p?.ore == null ? 0 : p.ore.crushes * 64 + p.ore.fragments.Count;

    /// <summary>
    /// Where the stones of a lump lie on a tray <paramref name="width"/> × <paramref name="height"/> (in the tray's own
    /// units): each where it lies on the bench (<see cref="OreFragment.x"/>, <see cref="OreFragment.y"/>: the pieces of a
    /// blow lie round the stone they came from), as big as it is (<paramref name="perCm"/> a centimetre, never smaller than
    /// <paramref name="least"/> - to be picked up - nor bigger than <paramref name="most"/>), pushed apart from the stones
    /// it would lie on (the smaller gives way). <paramref name="lay"/>: where each stone lay last time, by its place - a
    /// stone starts from there, so a stone thrown out or broken leaves the others where they lie, and a stone moves only
    /// when another is pushed against it; it is updated to where they lie now. Returns (x, y, radius) of each, in order.
    /// </summary>
    public static List<(double x, double y, double r)> Tray(IList<OreFragment> stones, double width, double height, double perCm, double least, double most,
        Dictionary<(double, double), (double, double)> lay = null)
    {
        int n = stones?.Count ?? 0;
        var x = new double[n];
        var y = new double[n];
        var r = new double[n];
        most = Math.Max(least, Math.Min(most, Math.Min(width, height) * 0.5));
        for (int i = 0; i < n; i++)
        {
            OreFragment f = stones[i];
            r[i] = CraftMath.Clamp(f.size * perCm, least, most);
            if (lay != null && lay.TryGetValue((f.x, f.y), out (double, double) was))
            {
                x[i] = was.Item1;
                y[i] = was.Item2;
            }
            else
            {
                x[i] = f.x * width;
                y[i] = f.y * height;
            }
        }
        const double gap = 1.5;
        for (int pass = 0; pass < 60; pass++)
        {
            bool moved = false;
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    double dx = x[j] - x[i], dy = y[j] - y[i], d = Math.Sqrt(dx * dx + dy * dy), need = r[i] + r[j] + gap;
                    if (d >= need - 1e-6)
                        continue;
                    if (d < 1e-6)
                    {
                        double a = (i * 37 + j * 71) % 360 * Math.PI / 180; // stones on the same spot part the same way every time
                        dx = Math.Cos(a);
                        dy = Math.Sin(a);
                    }
                    else
                    {
                        dx /= d;
                        dy /= d;
                    }
                    double push = need - d, gives = r[j] * r[j] / (r[i] * r[i] + r[j] * r[j]); // the smaller stone gives way
                    x[i] -= dx * push * gives;
                    y[i] -= dy * push * gives;
                    x[j] += dx * push * (1 - gives);
                    y[j] += dy * push * (1 - gives);
                    moved = true;
                }
            for (int i = 0; i < n; i++)
            {
                x[i] = CraftMath.Clamp(x[i], r[i], width - r[i]);
                y[i] = CraftMath.Clamp(y[i], r[i], height - r[i]);
            }
            if (!moved)
                break;
        }
        var placed = new List<(double x, double y, double r)>(n);
        lay?.Clear();
        for (int i = 0; i < n; i++)
        {
            placed.Add((x[i], y[i], r[i]));
            if (lay != null) lay[(stones[i].x, stones[i].y)] = (x[i], y[i]);
        }
        return placed;
    }

    /// <summary>What a brush that has been all over a stone says when it is put to it again (the workshop's refusal, and the window's before it starts).</summary>
    public static string BrushedAll(CraftOreBrush brush) =>
        $"The {BrushName(brush)} has been all over that stone: it takes no more off it{(brush == CraftOreBrush.Soft ? " (the stiff brush takes more)" : "")}.";

    /// <summary>
    /// Brushes one fragment dry, by hand: <c>a</c> is how much more of its surface the brush went over (0-1, scrubbed
    /// well). A soft brush takes loose dirt (caked clay only slowly); a stiff one takes dirt and clay; a wire brush takes
    /// the dirt and knocks the weathered rind of rock off the surface too (<see cref="OreFragment.RindShare"/>: the lump's
    /// outside, which its broken stones share - never the fresh rock a blow opened) - but scratches away a little of the
    /// mineral with it (more of a soft mineral). Nothing is lost with water, as washing loses fines: slower than washing,
    /// one stone at a time, but clean.
    /// <para>Each brush goes over each part of the stone once: the fragment keeps how much of its surface each one has
    /// gone over (<see cref="OreFragment.Brushed"/>), so going over it in several strokes does what one sweep over the
    /// same surface does, and a brush takes no more than its share (<see cref="BrushOnDirt"/>, <see cref="BrushOnRind"/>)
    /// however long it scrubs - the soft brush never shifts more of the caked clay, the wire brush knocks the rind off once.
    /// A fresh break (<see cref="Crush"/>) starts unbrushed: what the brushes left of the dirt and the rind on its old
    /// faces can be gone over again, but there is never more rind than the lump came with.</para>
    /// </summary>
    public static void Brush(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Ore || op.i0 < 0 || op.i0 >= p.ore.fragments.Count)
            return;
        CraftOreDefinition o = ws.Rules.Ore(p.ore.ore);
        var brush = (CraftOreBrush)op.i1;
        double cover = CraftMath.Clamp01(op.a);
        OreFragment f = p.ore.fragments[op.i0];
        if (!BrushTo(f, o, brush, Math.Min(1, f.Brushed(brush) + cover)))
            return;
        p.Record(ws.Elapsed, CraftOpKind.BrushOre, $"Brushed with a {BrushName(brush)}", cover);
    }

    /// <summary>
    /// <see cref="Brush"/>'s rule on one stone: <paramref name="brush"/> has now gone over <paramref name="to"/> of its
    /// surface (0-1; from as far as it had, <see cref="OreFragment.Brushed"/>) - the dirt and the weathered rind it takes
    /// there gone (the wire brush's little ore too), and the stone's shares counted anew on what is left of it, as the
    /// station counts them. Each brush's work is its own, so it does the same in whatever order the brushes went over it:
    /// the 3D ore bench reads from it what the station will have once its going-overs are in (its Clean meter). False when
    /// nothing would be left of the stone (it is then only marked as gone over).
    /// </summary>
    public static bool BrushTo(OreFragment f, CraftOreDefinition o, CraftOreBrush brush, double to)
    {
        double c0 = f.Brushed(brush), c1 = Math.Max(c0, Math.Min(1, to));
        double dirt = f.mass * f.dirt, mineral = f.mass * f.mineral, gangue = f.mass * f.gangue;
        dirt *= Still(BrushOnDirt(brush, o), c0, c1);
        // Only the weathered rind comes off the rock: the sound rock under it stays.
        double rind = gangue * f.RindShare, rindLeft = rind * Still(BrushOnRind(brush), c0, c1);
        gangue = Math.Max(0, gangue - rind + rindLeft);
        if (brush == CraftOreBrush.Wire)
            mineral *= Still(o != null && o.hardness < 4 ? 0.06 : 0.02, c0, c1);
        f.SetBrushed(brush, c1);
        double m = dirt + mineral + gangue;
        if (m < 1e-6)
            return false;
        f.mass = m;
        f.dirt = dirt / m;
        f.mineral = mineral / m;
        f.gangue = gangue / m;
        f.rind = gangue > 1e-12 ? CraftMath.Clamp01(rindLeft / gangue) : 0;
        return true;
    }

    /// <summary>
    /// What is left of something a brush takes <paramref name="takes"/> of where it goes, as the share of the surface it
    /// has gone over grows from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    private static double Still(double takes, double from, double to) => (1 - takes * to) / Math.Max(1e-9, 1 - takes * from);

    /// <summary>How much of the dirt a full going-over takes off with this brush.</summary>
    public static double BrushOnDirt(CraftOreBrush brush, CraftOreDefinition o)
    {
        bool clay = o != null && o.gangue == CraftGangue.Clay;
        return brush == CraftOreBrush.Soft ? (clay ? 0.3 : 0.7) : brush == CraftOreBrush.Stiff ? 0.9 : 0.85;
    }

    /// <summary>How much of the weathered rind a full going-over knocks off with this brush.</summary>
    public static double BrushOnRind(CraftOreBrush brush) => brush == CraftOreBrush.Wire ? 0.8 : brush == CraftOreBrush.Stiff ? 0.12 : 0;

    /// <summary>
    /// The share of a lump's loose rock that is the weathered rind on its surface, by its size (cm; a small lump is mostly
    /// surface): what a lump comes out of the seam with. Its broken stones share it (<see cref="OreFragment.RindShare"/>).
    /// </summary>
    public static double Rind(double sizeCm) => CraftMath.Clamp(2.0 / (sizeCm + 2.0), 0.08, 0.6);

    public static string BrushName(CraftOreBrush brush) => brush == CraftOreBrush.Soft ? "soft brush" : brush == CraftOreBrush.Stiff ? "stiff brush" : "wire brush";

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
    /// <summary>
    /// Is this lump on the bench, where it can be worked (crushed, washed, sieved, brushed, picked over)? Not while it is
    /// smelting in the furnace or giving its metal to the crucible, nor while it is too hot to handle.
    /// </summary>
    private bool OnTheBench(CraftPiece p, string id, out string reason)
    {
        reason = "";
        switch (p.location)
        {
            case CraftLocation.Air:
                break;
            case CraftLocation.Furnace:
                return Refuse("It is part of the furnace's charge now: it smelts there.", out reason, id);
            case CraftLocation.Melt:
                return Refuse("It is in the crucible: lift it out with the tongs first.", out reason, id);
            default:
                return Refuse("Put it on the bench first.", out reason, id);
        }
        if (p.ore.temperature > 120)
            return Refuse("It is too hot to handle: let it cool on the bench.", out reason, id);
        return true;
    }

    /// <summary>
    /// How the lump lies on the bench now: its crushings and its stones (<see cref="CraftOres.Layout"/>). A request that
    /// names a stone by its place carries the layout it was aimed at; a different one (the stones moved meanwhile: a
    /// blow, a stone thrown out, a sieving) is refused rather than striking the wrong stone. 0 = not checked.
    /// </summary>
    private bool SameLayout(CraftPiece p, int layout, string id, out string reason)
    {
        reason = "";
        if (layout == 0 || layout == CraftOres.Layout(p))
            return true;
        return Refuse("The stones have moved since: look again and pick the stone anew.", out reason, id);
    }

    /// <summary>A blow of the hammer on a fragment of an ore lump (the breaking stone). <paramref name="layout"/>: see <see cref="CraftOres.Layout"/>.</summary>
    public bool CrushOre(string id, int fragment, double force, out string reason, int layout = 0)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.OreBench))
            return Refuse("There is no breaking stone here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Ore)
            return Refuse("That is not ore.", out reason, id);
        if (!OnTheBench(p, id, out reason) || !SameLayout(p, layout, id, out reason))
            return false;
        if (fragment < 0 || fragment >= p.ore.fragments.Count)
            return Refuse("There is no such fragment.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.CrushOre, piece = id, i0 = fragment, a = (float)Math.Max(0, force) });
        return true;
    }

    /// <summary>
    /// Throws a fragment of an ore lump onto the waste heap - the last one too (nothing is left of the lump then).
    /// <paramref name="layout"/>: see <see cref="CraftOres.Layout"/>.
    /// </summary>
    public bool DiscardFragment(string id, int fragment, out string reason, int layout = 0)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.OreBench))
            return Refuse("There is no waste heap here: sort ore at an ore bench.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Ore)
            return Refuse("That is not ore.", out reason, id);
        if (!OnTheBench(p, id, out reason) || !SameLayout(p, layout, id, out reason))
            return false;
        if (fragment < 0 || fragment >= p.ore.fragments.Count)
            return Refuse("There is no such fragment.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.DiscardFragment, piece = id, i0 = fragment });
        return true;
    }

    /// <summary>
    /// Brushes fragment <paramref name="fragment"/> of an ore lump dry: <paramref name="coverage"/> is how much of its
    /// surface the brush went over (0-1). <paramref name="layout"/>: see <see cref="CraftOres.Layout"/>.
    /// </summary>
    public bool BrushOre(string id, int fragment, CraftOreBrush brush, double coverage, out string reason, int layout = 0)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.OreBench))
            return Refuse("There are no brushes here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Ore)
            return Refuse("That is not ore.", out reason, id);
        if (!OnTheBench(p, id, out reason) || !SameLayout(p, layout, id, out reason))
            return false;
        if (fragment < 0 || fragment >= p.ore.fragments.Count)
            return Refuse("There is no such stone.", out reason, id);
        if (!(coverage > 0))
            return Refuse("The brush did not touch it.", out reason, id);
        if (p.ore.fragments[fragment].Brushed(brush) >= 0.999)
            return Refuse(CraftOres.BrushedAll(brush), out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.BrushOre, piece = id, i0 = fragment, i1 = (int)brush, a = (float)CraftMath.Clamp01(coverage) });
        return true;
    }

    /// <summary>Washes an ore lump in the pan (or the trough).</summary>
    public bool WashOre(string id, double flow, double seconds, double technique, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.OreBench))
            return Refuse("There is no washing trough here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Ore)
            return Refuse("That is not ore.", out reason, id);
        if (!OnTheBench(p, id, out reason))
            return false;
        if (seconds <= 0 || flow <= 0)
            return Refuse("No water ran over it.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.WashOre, piece = id, a = (float)flow, b = (float)seconds, c = (float)technique });
        return true;
    }

    /// <summary>
    /// Sieves an ore lump's fragments: those smaller than <paramref name="mesh"/> (cm) fall through into a new piece -
    /// all of them when it was shaken thoroughly (<paramref name="thoroughness"/> 1), only the smallest when not.
    /// Returns its id (null when nothing, or everything, falls through).
    /// </summary>
    public string SieveOre(string id, double mesh, out string reason, double thoroughness = 1)
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
        if (!OnTheBench(p, id, out reason))
            return null;
        double through = mesh * SieveReach(CraftMath.Clamp01(thoroughness));
        int small = 0;
        foreach (OreFragment f in p.ore.fragments)
            if (f.size < through) small++;
        if (small == 0 || small == p.ore.fragments.Count)
        {
            Refuse(small == 0 ? "Nothing falls through the sieve: every stone is bigger than its holes." : "It all falls through the sieve: take a finer mesh to sort the fines from the lumps.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.SieveOre, piece = id, a = (float)mesh, b = (float)Math.Max(0.001, CraftMath.Clamp01(thoroughness)) };
        Record(op);
        return op.other;
    }

    /// <summary>The share of the mesh that really lets fragments through after shaking this well (operations recorded before shaking counted: 1).</summary>
    public static double SieveReach(double thoroughness) => thoroughness > 0 ? 0.55 + 0.45 * thoroughness : 1;

    private void ApplySieve(CraftOperation op)
    {
        CraftPiece p = Piece(op.piece);
        if (p == null)
            return;
        CraftPiece fines = Create(op, CraftPieceKind.Ore, p.Derived("sieved"));
        fines.ore = Copy(p.ore);
        fines.ore.fragments.Clear();
        double through = op.a * SieveReach(op.b);
        for (int i = p.ore.fragments.Count - 1; i >= 0; i--)
            if (p.ore.fragments[i].size < through)
            {
                fines.ore.fragments.Insert(0, p.ore.fragments[i]);
                p.ore.fragments.RemoveAt(i);
            }
        p.Record(Elapsed, op.kind, $"Sieved at {op.a:0.#} cm");
        fines.Record(Elapsed, op.kind, $"Fell through a {op.a:0.#} cm sieve");
    }

}
