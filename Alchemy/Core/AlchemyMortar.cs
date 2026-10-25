using System;
using System.Collections.Generic;

/// <summary>
/// What is in a mortar while it is worked: the ingredient as pieces lying in the bowl, each its own size (1 = whole,
/// toward 0 = dust) and share of the ingredient. <b>Pounding</b> breaks the pieces under the pestle in two - best on
/// hard roots and crystals; <b>grinding</b> wears down the pieces the pestle's head passes over and shoves them aside -
/// best on soft petals and leaves. Pieces slide back to the bottom of the bowl, so the whole bowl must be worked:
/// what was missed stays coarse.
/// <para>The result (<see cref="Result"/>) is what the station adds: the grind level it reached (its fineness by share) and
/// how evenly - how alike the pieces are, as the simulation's evenness (uneven: part of it goes in coarser and part finer,
/// see <see cref="AlchemyBrew.AddIngredient(IAlchemyIngredient, int, out string, string, float)"/>).</para>
/// Pure and deterministic: the 3D mortar (<see cref="AlchemyHandsOn"/>) shows it and feeds it the pestle's moves.
/// Positions are in the bowl's own units: its inner radius at the bottom is 1.
/// </summary>
public sealed class AlchemyMortar
{
    public struct Piece
    {
        public double X, Y;
        /// <summary>1 = whole, toward 0 = dust.</summary>
        public double Size;
        /// <summary>Its share of the ingredient (all pieces add up to 1).</summary>
        public double Mass;
    }

    /// <summary>The most pieces (more breaking makes the pieces smaller instead of more).</summary>
    public const int MaxPieces = 48;
    /// <summary>The smallest a piece gets (dust).</summary>
    public const double Dust = 0.02;
    /// <summary>Pounding breaks pieces no smaller than this: dust takes grinding.</summary>
    public const double PoundFloor = 0.35;

    private readonly List<Piece> pieces = new List<Piece>();
    private readonly Random random;

    /// <summary>0 soft petals - 1 rock.</summary>
    public double Hardness { get; }
    /// <summary>The grind levels the ingredient can reach (coarse, ground, powder...).</summary>
    public int Levels { get; }
    /// <summary>Reach of a blow of the pestle, and of its head while grinding (bowl units).</summary>
    public double PoundRadius = 0.38, HeadRadius = 0.24;
    /// <summary>Has anything been done to it yet?</summary>
    public bool Worked { get; private set; }
    public int Pounds { get; private set; }
    public double Ground { get; private set; }

    public IReadOnlyList<Piece> Pieces => pieces;

    /// <summary><paramref name="count"/> items of an ingredient of <paramref name="hardness"/> that can reach <paramref name="levels"/> grind levels.</summary>
    public AlchemyMortar(int count, double hardness, int levels, int seed = 1)
    {
        Hardness = Math.Max(0, Math.Min(1, hardness));
        Levels = Math.Max(0, levels);
        random = new Random(seed);
        int n = Math.Max(4, Math.Min(20, 4 * Math.Max(1, count)));
        for (int i = 0; i < n; i++)
        {
            double a = random.NextDouble() * Math.PI * 2, r = Math.Sqrt(random.NextDouble()) * 0.55;
            pieces.Add(new Piece { X = Math.Cos(a) * r, Y = Math.Sin(a) * r, Size = 0.9 + 0.1 * random.NextDouble(), Mass = 1.0 / n });
        }
    }

    /// <summary>
    /// A blow of the pestle at (<paramref name="x"/>, <paramref name="y"/>) with <paramref name="force"/> (about 1): the
    /// pieces under it break in two (large ones) or crack smaller, and scatter. Returns how many it hit.
    /// </summary>
    public int Pound(double x, double y, double force)
    {
        Worked = true;
        Pounds++;
        double k = Math.Max(0, force);
        double brittle = 0.2 + 0.8 * Hardness; // roots and crystals shatter; petals and leaves mostly bruise
        int hit = 0;
        int count = pieces.Count;
        for (int i = 0; i < count; i++)
        {
            Piece p = pieces[i];
            double dx = p.X - x, dy = p.Y - y;
            double reach = PoundRadius + p.Size * 0.12;
            if (dx * dx + dy * dy > reach * reach) continue;
            hit++;
            if (p.Size <= PoundFloor)
                continue; // too small to break with a blow
            if (pieces.Count < MaxPieces && random.NextDouble() < brittle * Math.Min(1.3, k))
            {
                // Breaks in two halves, flying apart.
                double s = Math.Max(PoundFloor, p.Size * 0.8);
                double a = random.NextDouble() * Math.PI * 2, d = 0.12 + 0.1 * random.NextDouble();
                p.Size = s;
                p.Mass *= 0.5;
                Piece q = p;
                p.X += Math.Cos(a) * d; p.Y += Math.Sin(a) * d;
                q.X -= Math.Cos(a) * d; q.Y -= Math.Sin(a) * d;
                pieces[i] = Clamp(p);
                pieces.Add(Clamp(q));
            }
            else
            {
                p.Size = Math.Max(PoundFloor, p.Size * (1 - (0.05 + 0.12 * brittle) * Math.Min(1.5, k)));
                pieces[i] = p;
            }
        }
        return hit;
    }

    /// <summary>
    /// The pestle's head ground from (<paramref name="x0"/>, <paramref name="y0"/>) to (<paramref name="x1"/>,
    /// <paramref name="y1"/>) with <paramref name="force"/> (about 1): the pieces it passes over wear down (by how far it
    /// went) and are pushed along.
    /// </summary>
    public void Grind(double x0, double y0, double x1, double y1, double force)
    {
        double sx = x1 - x0, sy = y1 - y0;
        double len = Math.Sqrt(sx * sx + sy * sy);
        if (len < 1e-6)
            return;
        Worked = true;
        Ground += len;
        double rate = 0.65 * Math.Max(0, force) * (1.15 - 0.75 * Hardness);
        for (int i = 0; i < pieces.Count; i++)
        {
            Piece p = pieces[i];
            // Distance from the piece to the stroke.
            double t = Math.Max(0, Math.Min(1, ((p.X - x0) * sx + (p.Y - y0) * sy) / (len * len)));
            double cx = x0 + sx * t - p.X, cy = y0 + sy * t - p.Y;
            double reach = HeadRadius + p.Size * 0.1;
            if (cx * cx + cy * cy > reach * reach) continue;
            // Bigger pieces wear slower; the head rides over dust.
            p.Size = Math.Max(Dust, p.Size * Math.Exp(-rate * len * (0.6 + 0.4 * p.Size)));
            // Shoved along (and out to the side) by the head.
            p.X += sx * 0.35 - cx * 0.3;
            p.Y += sy * 0.35 - cy * 0.3;
            pieces[i] = Clamp(p);
        }
    }

    /// <summary>The pieces slide back down to the bottom of the bowl (<paramref name="seconds"/>).</summary>
    public void Settle(double seconds)
    {
        double k = Math.Exp(-seconds * 0.35);
        for (int i = 0; i < pieces.Count; i++)
        {
            Piece p = pieces[i];
            p.X *= k;
            p.Y *= k;
            pieces[i] = p;
        }
    }

    /// <summary>How fine it is overall (0 whole - 1 dust), by share.</summary>
    public double Fineness
    {
        get
        {
            double f = 0;
            foreach (Piece p in pieces) f += p.Mass * (1 - p.Size);
            return f;
        }
    }

    /// <summary>The grind level of a piece of <paramref name="size"/> (0 = still whole).</summary>
    public int LevelOf(double size) => Math.Max(0, Math.Min(Levels, (int)Math.Floor((1 - size) * (Levels + 1) + 1e-9)));

    /// <summary>The share of the ingredient at each level (0 = whole).</summary>
    public double[] Shares()
    {
        var shares = new double[Levels + 1];
        foreach (Piece p in pieces) shares[LevelOf(p.Size)] += p.Mass;
        return shares;
    }

    /// <summary>
    /// How alike the pieces are (0-1): 1 when they are all one size, falling as lumps lie among dust (the share-weighted
    /// spread of their sizes; a spread of a level's width or more is 0).
    /// </summary>
    public double Evenness
    {
        get
        {
            double mean = 0;
            foreach (Piece p in pieces) mean += p.Mass * p.Size;
            double spread = 0;
            foreach (Piece p in pieces) spread += p.Mass * Math.Abs(p.Size - mean);
            double width = 1.0 / (Levels + 1);
            return Math.Max(0, Math.Min(1, 1 - spread / (width * 0.6)));
        }
    }

    /// <summary>
    /// What it has become: the level it reached by its overall fineness (0 = still whole) and how evenly, as the
    /// simulation's evenness (<see cref="Evenness"/>; whole is even).
    /// </summary>
    public void Result(out int level, out double evenness)
    {
        level = LevelOf(1 - Fineness);
        evenness = level == 0 ? 1 : Evenness;
    }

    private static Piece Clamp(Piece p)
    {
        double r = Math.Sqrt(p.X * p.X + p.Y * p.Y), max = 0.9;
        if (r > max)
        {
            p.X *= max / r;
            p.Y *= max / r;
        }
        return p;
    }
}
