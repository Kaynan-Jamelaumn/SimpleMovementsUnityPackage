using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Circles drawn by the hand round a centre (a stirring rod, a paddle, a swirl in the pan): how many turns, and how
/// steady - an even radius and a smooth pace stir best.
/// </summary>
public sealed class CraftSwirl
{
    private Vector2 centre;
    private float lastAngle, travelled, sumR, sumR2;
    private int n;
    private bool started;

    /// <summary>Whole turns since <see cref="Begin"/> (either way round).</summary>
    public float Turns => Mathf.Abs(travelled) / (2f * Mathf.PI);
    /// <summary>0-1: the radius held even.</summary>
    public float Steadiness
    {
        get
        {
            if (n < 4) return 0f;
            float mean = sumR / n, var = Mathf.Max(0f, sumR2 / n - mean * mean);
            return Mathf.Clamp01(1f - Mathf.Sqrt(var) / Mathf.Max(1f, mean) * 2.5f);
        }
    }
    public bool Active => started;

    public void Begin(Vector2 c)
    {
        centre = c;
        travelled = sumR = sumR2 = 0f;
        n = 0;
        started = true;
        lastAngle = float.NaN;
    }

    public void Add(Vector2 p)
    {
        if (!started) return;
        Vector2 d = p - centre;
        float r = d.magnitude;
        if (r < 3f) return;
        float a = Mathf.Atan2(d.y, d.x);
        if (!float.IsNaN(lastAngle))
            travelled += Mathf.DeltaAngle(lastAngle * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        lastAngle = a;
        sumR += r;
        sumR2 += r * r;
        n++;
    }

    /// <summary>Takes <paramref name="turns"/> off the count (they were sent).</summary>
    public void Spend(float turns) => travelled -= Mathf.Sign(travelled) * turns * 2f * Mathf.PI;

    public void Clear() => started = false;
}

/// <summary>A sieve, a rake, a box shaken back and forth: how many shakes, and how even their rhythm.</summary>
public sealed class CraftShake
{
    private float lastX, lastTurn = -1f;
    private int dir, reversals;
    private readonly List<float> gaps = new List<float>();

    public int Shakes => reversals / 2;
    /// <summary>0-1: shakes of an even length of time.</summary>
    public float Rhythm
    {
        get
        {
            if (gaps.Count < 3) return 0.5f;
            float mean = 0f, var = 0f;
            foreach (float g in gaps) mean += g;
            mean /= gaps.Count;
            foreach (float g in gaps) var += (g - mean) * (g - mean);
            var /= gaps.Count;
            return Mathf.Clamp01(1f - Mathf.Sqrt(var) / Mathf.Max(0.05f, mean) * 1.5f);
        }
    }

    public void Add(float x, float time)
    {
        float dx = x - lastX;
        if (Mathf.Abs(dx) < 4f) return;
        int d = dx > 0 ? 1 : -1;
        if (dir != 0 && d != dir)
        {
            reversals++;
            if (lastTurn >= 0f) gaps.Add(time - lastTurn);
            lastTurn = time;
        }
        dir = d;
        lastX = x;
    }

    public void Clear()
    {
        reversals = dir = 0;
        lastTurn = -1f;
        gaps.Clear();
    }
}

/// <summary>How much of something has been covered - flux sprinkled along a bar, clay painted on, ash raked over.</summary>
public sealed class CraftCoverage
{
    private float[] cells = new float[0];

    public int Count => cells.Length;
    public float this[int i] => i >= 0 && i < cells.Length ? cells[i] : 0f;

    public void Reset(int count)
    {
        if (cells.Length != count) cells = new float[Mathf.Max(0, count)];
        else System.Array.Clear(cells, 0, cells.Length);
    }

    /// <summary>Adds <paramref name="amount"/> around cell <paramref name="at"/> (its neighbours get half), up to <paramref name="max"/>.</summary>
    public void Paint(int at, float amount, float max = 1f)
    {
        for (int k = -1; k <= 1; k++)
        {
            int i = at + k;
            if (i < 0 || i >= cells.Length) continue;
            cells[i] = Mathf.Min(max, cells[i] + amount * (k == 0 ? 1f : 0.5f));
        }
    }

    public void Set(int at, float value)
    {
        if (at >= 0 && at < cells.Length) cells[at] = Mathf.Max(0f, value);
    }

    public void Wipe(int at, float amount)
    {
        if (at >= 0 && at < cells.Length) cells[at] = Mathf.Max(0f, cells[at] - amount);
    }

    /// <summary>The mean cover (0-1 of <paramref name="full"/>).</summary>
    public float Mean(float full = 1f)
    {
        if (cells.Length == 0) return 0f;
        float s = 0f;
        foreach (float c in cells) s += Mathf.Min(1f, c / Mathf.Max(1e-4f, full));
        return s / cells.Length;
    }

    public List<float> ToList()
    {
        var l = new List<float>(cells.Length);
        foreach (float c in cells) l.Add(c);
        return l;
    }
}
