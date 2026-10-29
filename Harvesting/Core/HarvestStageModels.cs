using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays one stage of a harvest: turns the pointer on the work surface into what the hands did, through the player's
/// <see cref="CraftHands"/> (a seeded wander that grows as they tire and shrinks with practice), and measures it -
/// precision, harm done, waste, mistakes. Nothing here decides what the harvest gives: the
/// <see cref="HarvestResolver"/> does, from the results. The same inputs at the same times always give the same results.
/// </summary>
public abstract class HarvestStageModel
{
    public readonly HarvestStageSpec Spec;
    protected readonly CraftHands hands;

    /// <summary>Seconds worked on it.</summary>
    public double Time { get; protected set; }
    public bool Done { get; protected set; }
    public bool Holding { get; protected set; }
    public int Mistakes { get; protected set; }
    /// <summary>The last thing the hands felt ("The hide rips!"), and whether it was a mistake.</summary>
    public string Feedback { get; private set; } = "";
    public bool FeedbackBad { get; private set; }
    public double FeedbackAt { get; private set; } = -9;
    /// <summary>Where the pointer is, and where the tool really is (the pointer plus the hand's wander).</summary>
    public Vector2 Pointer { get; protected set; } = new Vector2(-1f, -1f);
    public Vector2 Tip { get; protected set; } = new Vector2(-1f, -1f);
    /// <summary>How far through the stage (0-1).</summary>
    public abstract double Progress { get; }

    private Vector2 wander, wanderTarget;
    private double wanderTimer, workTimer;
    private int wanderStep;

    protected HarvestStageModel(HarvestStageSpec spec, CraftHands hands)
    {
        Spec = spec;
        this.hands = hands ?? new CraftHands(1);
    }

    /// <summary>The model that plays <paramref name="spec"/>.</summary>
    public static HarvestStageModel Create(HarvestStageSpec spec, CraftHands hands)
    {
        switch (spec.kind)
        {
            case HarvestStageKind.Trace: return new HarvestTraceModel(spec, hands);
            case HarvestStageKind.Peel: return new HarvestPeelModel(spec, hands);
            case HarvestStageKind.Steady: return new HarvestSteadyModel(spec, hands);
            case HarvestStageKind.Pull: return new HarvestPullModel(spec, hands);
            case HarvestStageKind.Saw: return new HarvestSawModel(spec, hands);
            case HarvestStageKind.Pry: return new HarvestPryModel(spec, hands);
            default: return new HarvestScrapeModel(spec, hands);
        }
    }

    public abstract void OnPointer(HarvestPointer phase, Vector2 p);

    /// <summary>One frame (unscaled seconds).</summary>
    public virtual void Tick(double dt)
    {
        if (Done || dt <= 0)
            return;
        Time += dt;
        UpdateWander(dt);
        if (Holding)
        {
            workTimer += dt;
            if (workTimer >= 0.8)
            {
                workTimer = 0;
                hands.Move(Effort);
            }
        }
    }

    /// <summary>How hard the work is on the arm (0-1): pulling and sawing tire it more than fine cuts.</summary>
    protected virtual double Effort => 0.25;

    /// <summary>
    /// The hand's wander: a slow drift toward a new offset every few tenths of a second, as far as the hands tremble
    /// (tired hands far more, practised ones less) times the stage's <see cref="HarvestStageSpec.tremble"/>.
    /// </summary>
    private void UpdateWander(double dt)
    {
        wanderTimer -= dt;
        if (wanderTimer <= 0)
        {
            wanderTimer = 0.18;
            wanderStep++;
            float scale = (float)(Spec.tremble / 0.06);
            wanderTarget = new Vector2((float)hands.Tremble(71 + wanderStep * 2) * scale, (float)hands.Tremble(72 + wanderStep * 2) * scale);
        }
        float k = (float)(1 - Math.Exp(-dt * 6));
        wander += (wanderTarget - wander) * k;
        if (Pointer.x >= 0f)
            Tip = Pointer + wander;
    }

    /// <summary>The wander right now (px).</summary>
    public Vector2 Wander => wander;

    protected void Say(string text, bool bad = false)
    {
        if (string.IsNullOrEmpty(text))
            return;
        // The same warning is not repeated every frame.
        if (text == Feedback && Time - FeedbackAt < 1.2)
            return;
        Feedback = text;
        FeedbackBad = bad;
        FeedbackAt = Time;
    }

    /// <summary>The results of the stage (one, or one per piece for stages that work several).</summary>
    public abstract List<HarvestStageResult> Finish();

    protected HarvestStageResult NewResult(int unit = -1) => new HarvestStageResult
    {
        kind = Spec.kind, unit = unit >= 0 ? unit : Spec.unit, seconds = (float)Time, mistakes = Mistakes, tool = Spec.toolId ?? "", toolFitness = Spec.toolFitness,
    };

    protected static float C01(double v) => (float)(v < 0 ? 0 : v > 1 ? 1 : v);

    /// <summary>The nearest point of <paramref name="path"/> to <paramref name="p"/> between two distances along it.</summary>
    public static void Nearest(HarvestPath path, Vector2 p, float from, float to, out float along, out float distance)
    {
        along = Math.Max(0f, from);
        distance = float.MaxValue;
        float walked = 0f;
        List<Vector2> pts = path.points;
        for (int i = 1; i < pts.Count; i++)
        {
            Vector2 a = pts[i - 1], b = pts[i];
            float seg = (b - a).magnitude;
            if (seg < 1e-5f) continue;
            float s0 = walked, s1 = walked + seg;
            walked = s1;
            if (s1 < from || s0 > to) continue;
            float d = HarvestDiagram.SegmentDistance(p, a, b, out float t);
            float s = s0 + t * seg;
            // Keep the answer inside the window.
            if (s < from) { s = from; d = (p - (a + (b - a) * ((from - s0) / seg))).magnitude; }
            else if (s > to) { s = to; d = (p - (a + (b - a) * ((to - s0) / seg))).magnitude; }
            if (d < distance)
            {
                distance = d;
                along = s;
            }
        }
    }
}

// ====================================================================== Trace
/// <summary>
/// Drawing the knife along lines (the opening cuts of a hide, the seams between muscles, the belly to reach the organs):
/// start at the green dot, follow the line to its end. Off the line (beyond the tolerance) the cut is ragged and wastes;
/// too fast it goes astray - and where it is fiddly, at a joint, or over the gut, the safe speed is much lower: rushing a
/// joint hacks bone, rushing over the gut opens it.
/// </summary>
public sealed class HarvestTraceModel : HarvestStageModel
{
    private int path;
    private float reached, doneBefore, total, speed, frameAdvance, lastDeviation;
    private double devSum, offLen, rushLen, rushDamage, spill, gutTimer, jointTimer;
    private int punctures, hacks;
    private readonly List<List<Vector2>> cuts = new List<List<Vector2>>();

    public HarvestTraceModel(HarvestStageSpec spec, CraftHands hands) : base(spec, hands)
    {
        foreach (HarvestPath p in spec.paths) total += p.Length;
        cuts.Add(new List<Vector2>());
        if (spec.paths.Count == 0) Done = true;
    }

    public int PathIndex => path;
    public HarvestPath CurrentPath => path < Spec.paths.Count ? Spec.paths[path] : null;
    /// <summary>How far along the current path the cut has come (px).</summary>
    public float Reached => reached;
    /// <summary>The knife's speed (px/s) and the safe speed where it is.</summary>
    public float Speed => speed;
    public float Limit => LimitAt(Zone);
    /// <summary>How far off the line the knife was last (px).</summary>
    public float Deviation => lastDeviation;
    /// <summary>The cuts made so far (where the knife really went), one list per path.</summary>
    public IReadOnlyList<List<Vector2>> Cuts => cuts;
    public HarvestZone Zone
    {
        get
        {
            HarvestPath p = CurrentPath;
            return p == null ? null : p.ZoneAt(p.Length > 0 ? reached / p.Length : 0);
        }
    }

    public override double Progress => total > 0 ? Math.Min(1, (doneBefore + reached) / total) : 1;

    private float LimitAt(HarvestZone z)
    {
        float l = Spec.speedLimit;
        if (z == null) return l;
        switch (z.kind)
        {
            case HarvestZoneKind.Fiddly: return l * 0.55f;
            case HarvestZoneKind.Joint: return l * (Spec.guides ? 0.6f : 0.45f);
            default: return l * 0.5f;
        }
    }

    public override void OnPointer(HarvestPointer phase, Vector2 p)
    {
        if (Done)
            return;
        switch (phase)
        {
            case HarvestPointer.Down:
            {
                Vector2 start = CurrentPath.At(reached);
                if ((p - start).magnitude <= Math.Max(Spec.tolerance * 2.5f, 20f))
                {
                    Holding = true;
                    Pointer = p;
                    Tip = p + Wander;
                }
                else
                {
                    Say(reached > 0f || path > 0 ? "Carry on from where the cut stopped (the green dot)." : "Start the cut at the green dot.");
                }
                break;
            }
            case HarvestPointer.Drag:
                if (!Holding) return;
                Pointer = p;
                Tip = p + Wander;
                Advance(Tip);
                break;
            case HarvestPointer.Up:
                Holding = false;
                break;
            case HarvestPointer.Move:
                if (!Holding) Pointer = p;
                break;
        }
    }

    private void Advance(Vector2 tip)
    {
        HarvestPath ph = CurrentPath;
        if (ph == null)
            return;
        float window = Math.Max(Spec.tolerance * 3f, 24f);
        Nearest(ph, tip, reached - 6f, reached + window, out float s, out float d);
        lastDeviation = d;
        if (d > Math.Max(Spec.tolerance * 3f, 20f))
        {
            Say("Back onto the line: the knife is cutting the wrong place.", true);
            return;
        }
        if (s > reached)
        {
            float adv = s - reached;
            devSum += Math.Min(d, Spec.tolerance * 2f) * adv;
            if (d > Spec.tolerance)
            {
                offLen += adv;
                Say("Off the line: the cut goes ragged.", true);
            }
            frameAdvance += adv;
            reached = s;
            cuts[cuts.Count - 1].Add(tip);
        }
        if (reached >= ph.Length - 1.5f)
            NextPath(ph.Length);
    }

    private void NextPath(float length)
    {
        doneBefore += length;
        path++;
        reached = 0f;
        Holding = false;
        if (path >= Spec.paths.Count)
        {
            Done = true;
            Say("The cuts are made.");
        }
        else
        {
            cuts.Add(new List<Vector2>());
            Say($"Next: {Spec.paths[path].name} - start at the green dot.");
        }
    }

    public override void Tick(double dt)
    {
        base.Tick(dt);
        if (Done || dt <= 0)
            return;
        float inst = (float)(frameAdvance / dt);
        frameAdvance = 0f;
        speed += (inst - speed) * (float)(1 - Math.Exp(-dt * 12));
        if (!Holding)
        {
            speed *= (float)Math.Exp(-dt * 8);
            return;
        }
        HarvestZone z = Zone;
        float over = speed / Math.Max(1f, LimitAt(z)) - 1f;
        if (over > 0f && inst > 0f)
        {
            double adv = inst * dt;
            rushLen += adv;
            rushDamage += over * adv / Math.Max(1f, total) * (z != null && z.kind == HarvestZoneKind.Fiddly ? 3.5 : 2.2) * Spec.strain;
            if (z == null || z.kind == HarvestZoneKind.Fiddly)
                Say("Too fast: the knife wanders.", true);
        }
        if (z != null && z.kind == HarvestZoneKind.Gut)
        {
            gutTimer = over > 0.15f ? gutTimer + dt : Math.Max(0, gutTimer - dt);
            if (gutTimer > 0.15)
            {
                gutTimer = -0.6;
                punctures++;
                Mistakes++;
                spill += 0.25 * Spec.strain;
                Say("The knife goes too deep: the gut is opened!", true);
            }
        }
        if (z != null && z.kind == HarvestZoneKind.Joint)
        {
            jointTimer = over > 0.2f ? jointTimer + dt : Math.Max(0, jointTimer - dt);
            if (jointTimer > 0.15)
            {
                jointTimer = -0.6;
                hacks++;
                Mistakes++;
                Say("Too fast through the joint: the blade hacks the bone.", true);
            }
        }
    }

    public override List<HarvestStageResult> Finish()
    {
        double l = Math.Max(1f, total);
        HarvestStageResult r = NewResult();
        r.precision = C01(1 - devSum / l / Math.Max(1f, Spec.tolerance) * 0.8 - rushLen / l * 0.3);
        r.damage = C01(offLen / l * 0.5 + rushDamage + hacks * 0.05 + punctures * 0.04);
        r.completeness = C01(1 - offLen / l * 0.6 - hacks * 0.03);
        r.spill = C01(spill);
        if (r.damage > 0.35 || offLen / l > 0.3) r.marks |= HarvestMarks.Rough;
        if (punctures > 0) r.marks |= HarvestMarks.Contaminated;
        if (hacks > 0) r.marks |= HarvestMarks.Rough;
        return new List<HarvestStageResult> { r };
    }
}

// ====================================================================== Peel
/// <summary>
/// Separating a hide from the flesh, from the opening cut (its two edges) toward the middle of the back: press on the
/// edge of the part still on and pull it outward - the further, the harder. In the green band the knife frees it, cell
/// by cell; slack does nothing; past the band the hide tears. Faces, paws and tails have a narrow band; a dull knife
/// needs more pull; holes from the fight tear easily. Pulling in the middle of the band leaves the least flesh on it.
/// </summary>
public sealed class HarvestPeelModel : HarvestStageModel
{
    private readonly int w, h;
    private readonly List<int>[] rows;
    private readonly int[] left, right;
    private readonly float[] progressL, progressR, tearL, tearR;
    private readonly float[] harm, flesh;
    private readonly bool[] peeled;
    private int hideCells, peeledCells, tears, nicks;
    private double bandTime, slackTime, overTime, quality, qualityTime;
    private int row = -1, side;
    private float tension;

    public HarvestPeelModel(HarvestStageSpec spec, CraftHands hands) : base(spec, hands)
    {
        w = Math.Max(1, spec.gridW);
        h = Math.Max(1, spec.gridH);
        rows = new List<int>[h];
        left = new int[h];
        right = new int[h];
        progressL = new float[h];
        progressR = new float[h];
        tearL = new float[h];
        tearR = new float[h];
        harm = new float[w * h];
        flesh = new float[w * h];
        peeled = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            rows[y] = new List<int>();
            for (int x = 0; x < w; x++)
                if (Kind(x, y) > 0) { rows[y].Add(x); hideCells++; }
        }
        if (hideCells == 0) Done = true;
    }

    private byte Kind(int x, int y)
    {
        int k = y * w + x;
        return k < Spec.cells.Count ? Spec.cells[k] : (byte)0;
    }

    public float Tension => tension;
    /// <summary>The row and side being worked (-1 none; side -1 left, +1 right).</summary>
    public int Row => row;
    public int Side => side;
    public bool IsPeeled(int x, int y) => peeled[y * w + x];
    public float HarmAt(int x, int y) => harm[y * w + x];
    public float FleshAt(int x, int y) => flesh[y * w + x];
    /// <summary>The band where the pull works now (the worked cell's: narrower on fiddly cells).</summary>
    public void Band(out float low, out float high)
    {
        int cell = FrontierCell(row, side);
        bool fiddly = cell >= 0 && Kind(cell % w, cell / w) == 2;
        low = fiddly ? Spec.fiddlyLow : Spec.bandLow;
        high = fiddly ? Spec.fiddlyHigh : Spec.bandHigh;
    }

    public override double Progress => hideCells > 0 ? peeledCells / (double)hideCells : 1;
    protected override double Effort => 0.35;

    /// <summary>The next cell still on, from the left (-1) or right (+1) edge of a row; -1 when the row is done.</summary>
    public int FrontierCell(int y, int s)
    {
        if (y < 0 || y >= h || s == 0) return -1;
        List<int> r = rows[y];
        if (left[y] + right[y] >= r.Count) return -1;
        int x = s < 0 ? r[left[y]] : r[r.Count - 1 - right[y]];
        return y * w + x;
    }

    private Rect CellRect(int x, int y)
    {
        float cw = Spec.area.width / w, ch = Spec.area.height / h;
        return new Rect(Spec.area.x + x * cw, Spec.area.y + Spec.area.height - (y + 1) * ch, cw, ch);
    }

    public override void OnPointer(HarvestPointer phase, Vector2 p)
    {
        if (Done) return;
        switch (phase)
        {
            case HarvestPointer.Down:
                if (!Spec.area.Contains(p) && !Near(p))
                {
                    Say("Press on the edge of the hide, then pull it outward.");
                    return;
                }
                Holding = true;
                Pointer = p;
                Aim(p);
                break;
            case HarvestPointer.Drag:
                if (!Holding) return;
                Pointer = p;
                Aim(p);
                break;
            case HarvestPointer.Up:
                Holding = false;
                tension = 0f;
                row = -1;
                break;
            case HarvestPointer.Move:
                if (!Holding) Pointer = p;
                break;
        }
    }

    private bool Near(Vector2 p) => p.x > Spec.area.x - 160f && p.x < Spec.area.xMax + 160f && p.y > Spec.area.y - 20f && p.y < Spec.area.yMax + 20f;

    /// <summary>Which row and side the hand works, and how hard it pulls (the distance outward from the edge still on).</summary>
    private void Aim(Vector2 p)
    {
        float ch = Spec.area.height / h;
        int y = (int)Math.Floor((Spec.area.yMax - p.y) / ch);
        y = Math.Max(0, Math.Min(h - 1, y));
        // The nearest row with work left on the side the pointer is on.
        float mid = Spec.area.x + Spec.area.width * 0.5f;
        int s = p.x < mid ? -1 : 1;
        int best = -1;
        for (int d = 0; d < h && best < 0; d++)
        {
            if (y - d >= 0 && FrontierCell(y - d, s) >= 0) best = y - d;
            else if (y + d < h && FrontierCell(y + d, s) >= 0) best = y + d;
        }
        if (best < 0)
        {
            s = -s;
            for (int d = 0; d < h && best < 0; d++)
            {
                if (y - d >= 0 && FrontierCell(y - d, s) >= 0) best = y - d;
                else if (y + d < h && FrontierCell(y + d, s) >= 0) best = y + d;
            }
        }
        row = best;
        side = s;
        if (best < 0) { tension = 0f; return; }
        int cell = FrontierCell(best, s);
        Rect r = CellRect(cell % w, cell / w);
        float pull = s < 0 ? r.x - p.x : p.x - r.xMax;
        tension = Math.Max(0f, pull / PullPixels);
    }

    /// <summary>How far out from the edge still on a pull of 1 is (px), whatever the size of the hide.</summary>
    public const float PullPixels = 90f;

    public override void Tick(double dt)
    {
        base.Tick(dt);
        if (Done || !Holding || row < 0 || dt <= 0)
            return;
        // The hand's tremble shakes the pull a little.
        float t = Math.Max(0f, tension + (float)hands.Tremble(91 + (int)(Time * 5)) * 0.8f);
        Work(row, side, t, 1f, dt);
        if (row - 1 >= 0) Work(row - 1, side, t, 0.5f, dt);
        if (row + 1 < h) Work(row + 1, side, t, 0.5f, dt);
        if (peeledCells >= hideCells)
        {
            Done = true;
            Holding = false;
            Say("The hide is off.");
        }
    }

    private void Work(int y, int s, float t, float weight, double dt)
    {
        int cell = FrontierCell(y, s);
        if (cell < 0) return;
        int x = cell % w;
        byte kind = Kind(x, y);
        bool fiddly = kind == 2;
        float low = fiddly ? Spec.fiddlyLow : Spec.bandLow, high = fiddly ? Spec.fiddlyHigh : Spec.bandHigh;
        float mid = (low + high) * 0.5f, half = Math.Max(0.01f, (high - low) * 0.5f);
        ref float progress = ref (s < 0 ? ref progressL[y] : ref progressR[y]);
        ref float tear = ref (s < 0 ? ref tearL[y] : ref tearR[y]);
        if (t < low)
        {
            if (weight >= 1f)
            {
                slackTime += dt;
                Say("Slack: pull it further out.");
            }
            tear = Math.Max(0f, tear - (float)dt);
            return;
        }
        if (t <= high)
        {
            progress += (float)(Spec.rate * dt * weight * (0.6 + 0.4 * (t - low) / (high - low)) * Spec.toolSpeed);
            if (weight >= 1f)
            {
                bandTime += dt;
                quality += (1 - Math.Min(1, Math.Abs(t - mid) / half)) * dt;
                qualityTime += dt;
            }
            tear = Math.Max(0f, tear - (float)dt * 0.5f);
        }
        else
        {
            tear += (float)((t - high) * Spec.strain * dt * 4 * weight * (kind == 3 ? 1.6 : 1));
            if (weight >= 1f)
            {
                overTime += dt;
                qualityTime += dt;
                Say(fiddly ? "Too hard for this spot: ease off." : "Too hard: the hide is straining.", true);
            }
            if (tear >= 1f)
            {
                harm[cell] = Math.Min(1f, harm[cell] + 0.45f + 0.4f * Math.Min(1f, (t - high) / 0.3f));
                tears++;
                Mistakes++;
                Say("The hide rips!", true);
                Peel(cell, y, s, 0.35f);
                tear = 0.3f;
                return;
            }
        }
        if (progress >= 1f)
        {
            progress -= 1f;
            float off = Math.Min(1f, Math.Abs(t - mid) / half);
            float leftOn = 0.1f + 0.5f * off + 0.2f * (1f - Spec.toolFitness);
            // A nick now and then: more on fiddly cells, with a tired hand and a poor knife.
            double chance = Spec.nick * (fiddly ? 2 : 1) * (0.5 + hands.fatigue);
            if (HarvestMath.Noise(hands.seed, cell * 7 + nicks, 83) < chance)
            {
                harm[cell] = Math.Min(1f, harm[cell] + 0.08f + 0.12f * (float)HarvestMath.Noise(hands.seed, cell, 84));
                nicks++;
                Say("A nick in the hide.", true);
            }
            Peel(cell, y, s, leftOn);
        }
    }

    private void Peel(int cell, int y, int s, float leftOn)
    {
        if (peeled[cell]) return;
        peeled[cell] = true;
        flesh[cell] = Math.Max(flesh[cell], leftOn);
        peeledCells++;
        if (s < 0) left[y]++;
        else right[y]++;
    }

    public override List<HarvestStageResult> Finish()
    {
        HarvestStageResult r = NewResult();
        r.cellDamage = new List<float>(harm);
        r.cellFlesh = new List<float>(flesh);
        r.lostCells = new List<int>();
        double sum = 0;
        for (int i = 0; i < harm.Length; i++) sum += harm[i];
        r.damage = C01(hideCells > 0 ? sum / hideCells : 0);
        r.precision = C01(qualityTime > 0 ? quality / qualityTime : 0.5);
        r.completeness = C01(Progress);
        if (tears > 0) r.marks |= HarvestMarks.Ripped;
        if (nicks > 0) r.marks |= HarvestMarks.Nicked;
        return new List<HarvestStageResult> { r };
    }
}

// ====================================================================== Steady
/// <summary>
/// Cutting small attachments free (an organ's ligaments, a tendon's ends, the skin around a claw): press on a mark and
/// hold the knife tip on it while it cuts. The hand wanders - more the longer it holds and the more tired it is - so
/// the pointer must keep correcting. Wandering into a gut spills it; into the piece itself, damages it.
/// </summary>
public sealed class HarvestSteadyModel : HarvestStageModel
{
    private readonly float[] cut;
    private int active = -1, doneCount;
    private double inTime, outTime, holdTime, damage, spill;
    private readonly bool[] touching;
    private readonly Vector2[][] shapes;

    public HarvestSteadyModel(HarvestStageSpec spec, CraftHands hands) : base(spec, hands)
    {
        cut = new float[spec.targets.Count];
        touching = new bool[spec.hazards.Count];
        shapes = new Vector2[spec.hazards.Count][];
        for (int i = 0; i < shapes.Length; i++) shapes[i] = spec.hazards[i].shape.ToArray();
        if (spec.targets.Count == 0) Done = true;
    }

    /// <summary>How far each mark is cut (0-1).</summary>
    public float CutOf(int target) => target >= 0 && target < cut.Length ? cut[target] : 0f;
    public int Active => active;
    /// <summary>Is the tip in a hazard right now?</summary>
    public bool InHazard { get; private set; }
    public override double Progress
    {
        get
        {
            if (cut.Length == 0) return 1;
            double s = 0;
            foreach (float c in cut) s += Math.Min(1f, c);
            return s / cut.Length;
        }
    }

    public override void OnPointer(HarvestPointer phase, Vector2 p)
    {
        if (Done) return;
        switch (phase)
        {
            case HarvestPointer.Down:
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < Spec.targets.Count; i++)
                {
                    if (cut[i] >= 1f) continue;
                    float d = (Spec.targets[i].at - p).magnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best < 0 || bestD > Math.Max(Spec.targets[best].radius * 2.5f, 22f))
                {
                    Say("Press on one of the yellow marks and hold the knife there.");
                    return;
                }
                active = best;
                Holding = true;
                holdTime = 0;
                Pointer = p;
                Tip = p + Wander;
                break;
            }
            case HarvestPointer.Drag:
            case HarvestPointer.Move:
                Pointer = p;
                break;
            case HarvestPointer.Up:
                Holding = false;
                active = -1;
                break;
        }
    }

    public override void Tick(double dt)
    {
        base.Tick(dt);
        if (Done || dt <= 0)
            return;
        InHazard = false;
        if (!Holding || active < 0)
            return;
        holdTime += dt;
        // Holding still is tiring: the wander grows the longer the knife is held up.
        Vector2 extra = Wander * (float)Math.Min(1.5, holdTime * 0.12);
        Tip = Pointer + Wander + extra;
        HarvestTarget t = Spec.targets[active];
        if ((Tip - t.at).magnitude <= t.radius)
        {
            cut[active] += (float)(dt * Spec.toolSpeed / Math.Max(0.1f, t.need));
            inTime += dt;
        }
        else
        {
            outTime += dt;
            Say("Steady - back onto the mark.");
        }
        for (int i = 0; i < Spec.hazards.Count; i++)
        {
            HarvestHazard z = Spec.hazards[i];
            bool inside = HarvestDiagram.Contains(shapes[i], Tip);
            if (inside)
            {
                InHazard = true;
                if (!touching[i])
                {
                    Mistakes++;
                    Say(z.gut ? $"You're cutting into the {z.name}!" : $"Careful - you're cutting the {z.name}!", true);
                }
                if (z.gut) spill += dt * z.severity * 0.9 * Spec.strain;
                else damage += dt * z.severity * 0.8 * Spec.strain;
            }
            touching[i] = inside;
        }
        if (cut[active] >= 1f)
        {
            cut[active] = 1f;
            doneCount++;
            active = -1;
            Holding = false;
            Say(doneCount >= cut.Length ? "It is free." : "Cut free. Next mark.");
            if (doneCount >= cut.Length) Done = true;
        }
    }

    public override List<HarvestStageResult> Finish()
    {
        HarvestStageResult r = NewResult();
        r.precision = C01(inTime + outTime > 0 ? inTime / (inTime + outTime) : 0.5);
        r.damage = C01(damage);
        r.spill = C01(spill);
        r.completeness = C01(Progress);
        if (r.damage > 0.05f) r.marks |= HarvestMarks.Nicked;
        if (r.spill > 0.05f) r.marks |= HarvestMarks.Contaminated;
        return new List<HarvestStageResult> { r };
    }
}

// ====================================================================== Pull
/// <summary>
/// Drawing something out (a tendon, an organ, a tooth after it is loose): grip it and pull along the arrow, keeping the
/// pull in the green band. Now and then it catches - the band jumps up and the pull must follow - then gives, and a pull
/// left high after it gives strains it; strain past the limit snaps it.
/// </summary>
public sealed class HarvestPullModel : HarvestStageModel
{
    private readonly float[] catches;
    private double progress, strain, peakStrain, damage, inBand, over, stall;
    private int snaps;
    private float tension;
    private Vector2 grip;

    public HarvestPullModel(HarvestStageSpec spec, CraftHands hands) : base(spec, hands)
    {
        int n = Math.Max(0, spec.count);
        catches = new float[n];
        for (int i = 0; i < n; i++)
            catches[i] = (float)((i + 1) / (double)(n + 1) + (HarvestMath.Noise(hands?.seed ?? 1, i, 87) - 0.5) * 0.08);
    }

    public float Tension => tension;
    public float Strain => (float)strain;
    public override double Progress => Math.Min(1, progress);
    /// <summary>Where the gripped end is now (it comes out as it is drawn).</summary>
    public Vector2 End => Spec.pullFrom + (Spec.pullTo - Spec.pullFrom) * (float)Math.Min(1, progress);
    private Vector2 Dir => (Spec.pullTo - Spec.pullFrom).normalized;
    protected override double Effort => 0.4;

    /// <summary>The band now (higher while it catches).</summary>
    public void Band(out float low, out float high) => BandAt(progress, out low, out high);

    /// <summary>The band at a point of the draw (the guide shows the next catch coming).</summary>
    public void BandAt(double at, out float low, out float high)
    {
        float lift = 0f;
        foreach (float c in catches)
            if (Math.Abs(at - c) < 0.045) lift = 0.28f;
        low = Spec.bandLow + lift;
        high = Spec.bandHigh + lift;
    }

    /// <summary>The next catch ahead (fraction of the draw), or -1.</summary>
    public float NextCatch
    {
        get
        {
            foreach (float c in catches)
                if (c > progress + 0.045) return c;
            return -1f;
        }
    }

    public override void OnPointer(HarvestPointer phase, Vector2 p)
    {
        if (Done) return;
        switch (phase)
        {
            case HarvestPointer.Down:
                if ((p - End).magnitude > 40f)
                {
                    Say("Grip the end (the ring) and pull along the arrow.");
                    return;
                }
                Holding = true;
                grip = p;
                Pointer = p;
                tension = 0f;
                break;
            case HarvestPointer.Drag:
                if (!Holding) return;
                Pointer = p;
                tension = Math.Max(0f, Vector2.Dot(p - grip, Dir)) / 120f;
                break;
            case HarvestPointer.Up:
                Holding = false;
                tension = 0f;
                break;
            case HarvestPointer.Move:
                if (!Holding) Pointer = p;
                break;
        }
    }

    public override void Tick(double dt)
    {
        base.Tick(dt);
        if (Done || dt <= 0)
            return;
        if (!Holding)
        {
            strain = Math.Max(0, strain - dt * 0.6);
            return;
        }
        float t = Math.Max(0f, tension + (float)hands.Tremble(93 + (int)(Time * 5)) * 0.6f);
        Band(out float low, out float high);
        if (t < low)
        {
            stall += dt;
            strain = Math.Max(0, strain - dt * 0.5);
            Say("It holds: pull a little harder.");
        }
        else if (t <= high)
        {
            progress += Spec.rate * dt * Spec.toolSpeed;
            inBand += dt;
            strain = Math.Max(0, strain - dt * 0.5);
        }
        else
        {
            over += dt;
            strain += (t - high) * Spec.strain * dt * 3;
            peakStrain = Math.Max(peakStrain, strain);
            Say("Too hard: it is straining.", true);
            if (strain >= 1)
            {
                snaps++;
                Mistakes++;
                damage += 0.35;
                strain = 0;
                progress += 0.1;
                Say("It snaps!", true);
            }
        }
        if (progress >= 1)
        {
            progress = 1;
            Done = true;
            Holding = false;
            Say("Out.");
        }
    }

    public override List<HarvestStageResult> Finish()
    {
        HarvestStageResult r = NewResult();
        double total = inBand + over + stall * 0.3;
        r.precision = C01(total > 0 ? inBand / total : 0.5);
        r.damage = C01(damage + peakStrain * 0.1);
        r.completeness = C01(1 - snaps * 0.2);
        if (snaps > 0) r.marks |= HarvestMarks.Ripped;
        return new List<HarvestStageResult> { r };
    }
}

// ====================================================================== Saw
/// <summary>
/// Sawing through bone or horn: strokes back and forth along the cut line, in time with the beat. An even rhythm cuts
/// cleanly; a snatched stroke makes the saw jump and splinter the bone; a crooked one (off the line) binds and splinters.
/// </summary>
public sealed class HarvestSawModel : HarvestStageModel
{
    private double progress, splinter, lastU, strokeStart, strokeLen, strokeLateral, errSum;
    private int dir, strokes, samples, crooked;

    public HarvestSawModel(HarvestStageSpec spec, CraftHands hands) : base(spec, hands)
    {
        if (spec.paths.Count == 0 || spec.paths[0].points.Count < 2) Done = true;
    }

    private Vector2 A => Spec.paths[0].points[0];
    private Vector2 B => Spec.paths[0].points[Spec.paths[0].points.Count - 1];
    public override double Progress => Math.Min(1, progress);
    public int Strokes => strokes;
    /// <summary>Where the beat is (0-1 over one stroke there and back).</summary>
    public double Beat => Time % (2 * Spec.seconds) / (2 * Spec.seconds);
    protected override double Effort => 0.45;

    public override void OnPointer(HarvestPointer phase, Vector2 p)
    {
        if (Done) return;
        Vector2 a = A, b = B, d = (b - a).normalized;
        switch (phase)
        {
            case HarvestPointer.Down:
                if (HarvestDiagram.SegmentDistance(p, a - d * 60f, b + d * 60f, out _) > 30f)
                {
                    Say("Put the saw on the line, then stroke along it in time with the beat.");
                    return;
                }
                Holding = true;
                Pointer = p;
                lastU = Vector2.Dot(p - a, d);
                strokeStart = Time;
                strokeLen = 0;
                dir = 0;
                break;
            case HarvestPointer.Drag:
            {
                if (!Holding) return;
                Pointer = p;
                double u = Vector2.Dot(p - a, d);
                double lateral = HarvestDiagram.SegmentDistance(p, a - d * 400f, b + d * 400f, out _);
                double du = u - lastU;
                lastU = u;
                if (Math.Abs(du) < 0.5) return;
                int s = du > 0 ? 1 : -1;
                if (dir != 0 && s != dir && strokeLen >= Math.Min(60f, (b - a).magnitude * 0.4f))
                    Stroke();
                if (s != dir)
                {
                    dir = s;
                    strokeStart = Time;
                    strokeLen = 0;
                    strokeLateral = 0;
                    samples = 0;
                }
                strokeLen += Math.Abs(du);
                strokeLateral += lateral;
                samples++;
                break;
            }
            case HarvestPointer.Up:
                Holding = false;
                break;
            case HarvestPointer.Move:
                if (!Holding) Pointer = p;
                break;
        }
    }

    private void Stroke()
    {
        double period = Time - strokeStart;
        double err = Math.Min(1, Math.Abs(period - Spec.seconds) / Math.Max(0.05, Spec.window));
        bool onLine = samples == 0 || strokeLateral / samples <= Spec.tolerance;
        if (period < Spec.seconds * 0.45)
        {
            splinter += 0.04 * Spec.strain;
            Mistakes++;
            Say("Too fast: the saw jumps and splinters the bone.", true);
        }
        else if (!onLine)
        {
            splinter += 0.03 * Spec.strain;
            crooked++;
            Say("Crooked: keep the blade on the line.", true);
        }
        else if (err < 0.35)
        {
            Say("Even strokes.");
        }
        strokes++;
        errSum += err;
        progress += 1.0 / Math.Max(1, Spec.count) * Math.Max(0.25, 1 - 0.6 * err) * Spec.toolSpeed * (onLine ? 1 : 0.5);
        if (progress >= 1)
        {
            progress = 1;
            Done = true;
            Holding = false;
            Say("Sawn through.");
        }
    }

    public override List<HarvestStageResult> Finish()
    {
        HarvestStageResult r = NewResult();
        double meanErr = strokes > 0 ? errSum / strokes : 0.5;
        r.precision = C01(1 - meanErr * 0.7 - (strokes > 0 ? crooked / (double)strokes : 0) * 0.3);
        r.damage = C01(splinter);
        r.completeness = C01(Progress);
        if (r.damage > 0.08f) r.marks |= HarvestMarks.Splintered;
        return new List<HarvestStageResult> { r };
    }
}

// ====================================================================== Pry
/// <summary>
/// Working pieces loose one after another (claws, teeth, scales): grip one and rock it side to side - each swing that
/// reaches past the inner marks loosens it. Swinging past the outer marks, or snatching, strains it; strain past the
/// limit cracks it (a piece already cracked in the fight gives way sooner). Fully loose, it comes free.
/// </summary>
public sealed class HarvestPryModel : HarvestStageModel
{
    private int current;
    private float looseness, stress, peakStress, crack;
    private int lastSide;
    private double lastSwing = -9, swingTime;
    private readonly List<HarvestStageResult> results = new List<HarvestStageResult>();
    private int startMistakes;

    public HarvestPryModel(HarvestStageSpec spec, CraftHands hands) : base(spec, hands)
    {
        if (spec.pieces.Count == 0) Done = true;
    }

    public int Current => current;
    public HarvestPieceSpot Piece => current < Spec.pieces.Count ? Spec.pieces[current] : null;
    public float Looseness => looseness;
    public float Stress => stress;
    /// <summary>The sideways offset of the grip (px).</summary>
    public float Offset { get; private set; }
    public override double Progress => Spec.pieces.Count > 0 ? (current + Math.Min(1f, looseness)) / Spec.pieces.Count : 1;
    protected override double Effort => 0.3;

    private float Fragility => Piece != null ? 2f - Math.Max(0.2f, Math.Min(1f, Piece.integrity)) : 1f;

    public override void OnPointer(HarvestPointer phase, Vector2 p)
    {
        if (Done) return;
        switch (phase)
        {
            case HarvestPointer.Down:
                if (Piece == null || (p - Piece.at).magnitude > 60f)
                {
                    Say("Grip the highlighted piece, then rock it side to side.");
                    return;
                }
                Holding = true;
                Pointer = p;
                lastSide = 0;
                break;
            case HarvestPointer.Drag:
            {
                if (!Holding || Piece == null) return;
                Pointer = p;
                Offset = p.x - Piece.at.x;
                float a = Math.Abs(Offset);
                int s = Offset > Spec.tolerance ? 1 : Offset < -Spec.tolerance ? -1 : 0;
                if (s != 0 && s != lastSide)
                {
                    if (lastSide != 0)
                    {
                        double interval = Time - lastSwing;
                        looseness += 1f / Math.Max(1, Spec.count) * (float)Spec.toolSpeed;
                        if (interval < 0.18)
                        {
                            stress += 0.15f * Spec.strain * Fragility;
                            Say("Rushed: it strains.", true);
                        }
                        lastSwing = Time;
                    }
                    lastSide = s;
                }
                if (a > Spec.speedLimit)
                    Say("Too far: it strains.", true);
                break;
            }
            case HarvestPointer.Up:
                Holding = false;
                Offset = 0f;
                break;
            case HarvestPointer.Move:
                if (!Holding) Pointer = p;
                break;
        }
    }

    public override void Tick(double dt)
    {
        base.Tick(dt);
        if (Done || dt <= 0)
            return;
        swingTime += dt;
        if (Holding && Math.Abs(Offset) > Spec.speedLimit)
            stress += (float)(dt * Spec.strain * 0.8 * Fragility * (Math.Abs(Offset) / Spec.speedLimit));
        stress = Math.Max(0f, stress - (float)dt * 0.35f);
        peakStress = Math.Max(peakStress, stress);
        if (stress >= 1f)
        {
            crack += 0.45f;
            Mistakes++;
            stress = 0.2f;
            Say("It cracks!", true);
        }
        if (looseness >= 1f)
        {
            HarvestStageResult r = NewResult(Piece.unit);
            r.seconds = (float)swingTime;
            r.mistakes = Mistakes - startMistakes;
            r.precision = C01(1 - peakStress * 0.6);
            r.damage = C01(crack + Math.Max(0f, peakStress - 0.7f) * 0.5f);
            if (crack > 0f) r.marks |= HarvestMarks.Cracked;
            results.Add(r);
            current++;
            looseness = 0f;
            stress = 0f;
            peakStress = 0f;
            crack = 0f;
            swingTime = 0;
            startMistakes = Mistakes;
            Holding = false;
            Offset = 0f;
            if (current >= Spec.pieces.Count)
            {
                Done = true;
                Say("All free.");
            }
            else
            {
                Say("It comes free. Next one.");
            }
        }
    }

    public override List<HarvestStageResult> Finish() => new List<HarvestStageResult>(results);
}

// ====================================================================== Scrape
/// <summary>
/// Scraping a bone clean (or plucking a bird, scaling a fish, trimming fat): drag the scraper over it - an even, brisk
/// stroke takes the most; dawdling on what is already bare gouges it, frantic strokes skim (and break feathers). Done
/// when it is clean, or accepted as it is.
/// </summary>
public sealed class HarvestScrapeModel : HarvestStageModel
{
    private readonly int w, h;
    private readonly float[] tissue;
    private readonly float total;
    private float removed, speed;
    private double gouge, breakage;
    private Vector2 last;
    private bool hasLast;
    private const float Radius = 16f;
    /// <summary>Strokes this fast (px/s) work at full rate; slower ones less.</summary>
    public const float FullSpeed = 120f;
    /// <summary>Strokes faster than this skim: they take less.</summary>
    public const float SkimSpeed = 420f;
    /// <summary>Strokes faster than this break what is worked.</summary>
    public const float BreakSpeed = 450f;
    /// <summary>Slower than this is pressing, not stroking (it gouges bare spots).</summary>
    public const float PressSpeed = 30f;
    /// <summary>Tissue this thin is as good as gone: a stroke takes the rest.</summary>
    private const float Bare = 0.05f;

    public HarvestScrapeModel(HarvestStageSpec spec, CraftHands hands) : base(spec, hands)
    {
        w = Math.Max(1, spec.gridW);
        h = Math.Max(1, spec.gridH);
        tissue = new float[w * h];
        for (int i = 0; i < tissue.Length; i++)
        {
            byte b = i < spec.cells.Count ? spec.cells[i] : (byte)255;
            tissue[i] = b == 255 ? -1f : b / 10f;
            if (tissue[i] > 0f) total += tissue[i];
        }
        if (total <= 0f) Done = true;
    }

    /// <summary>Tissue left on a cell (−1 = not part of the piece).</summary>
    public float TissueAt(int x, int y) => tissue[y * w + x];
    public float Coverage => total > 0 ? Math.Min(1f, removed / total) : 1f;
    public float Gouge => (float)gouge;
    public float Speed => speed;
    public override double Progress => Coverage;

    public override void OnPointer(HarvestPointer phase, Vector2 p)
    {
        if (Done) return;
        switch (phase)
        {
            case HarvestPointer.Down:
                Holding = true;
                Pointer = p;
                last = p;
                hasLast = true;
                break;
            case HarvestPointer.Drag:
                if (!Holding) return;
                Pointer = p;
                break;
            case HarvestPointer.Up:
                Holding = false;
                hasLast = false;
                break;
            case HarvestPointer.Move:
                if (!Holding) Pointer = p;
                break;
        }
    }

    public override void Tick(double dt)
    {
        base.Tick(dt);
        if (Done || dt <= 0)
            return;
        if (!Holding)
        {
            speed *= (float)Math.Exp(-dt * 8);
            return;
        }
        Vector2 at = Tip.x >= 0f ? Tip : Pointer;
        float moved = hasLast ? (at - last).magnitude : 0f;
        last = at;
        hasLast = true;
        speed += ((float)(moved / dt) - speed) * (float)(1 - Math.Exp(-dt * 10));
        float take = (float)(Spec.rate * dt * Spec.toolSpeed * Math.Min(1.0, speed / FullSpeed) * (speed > SkimSpeed ? 0.6 : 1.0));
        bool pressing = speed < PressSpeed;
        bool overBare = false;
        float cw = Spec.area.width / w, ch = Spec.area.height / h;
        int x0 = (int)Math.Floor((at.x - Radius - Spec.area.x) / cw), x1 = (int)Math.Floor((at.x + Radius - Spec.area.x) / cw);
        int y0 = (int)Math.Floor((Spec.area.yMax - at.y - Radius) / ch), y1 = (int)Math.Floor((Spec.area.yMax - at.y + Radius) / ch);
        for (int y = Math.Max(0, y0); y <= Math.Min(h - 1, y1); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(w - 1, x1); x++)
            {
                int k = y * w + x;
                if (tissue[k] < 0f) continue;
                Vector2 c = new Vector2(Spec.area.x + (x + 0.5f) * cw, Spec.area.yMax - (y + 0.5f) * ch);
                float d = (c - at).magnitude;
                if (d > Radius) continue;
                float fall = 1f - d / (Radius + 1f);
                if (tissue[k] <= Bare)
                {
                    overBare = true;
                    continue;
                }
                float r = Math.Min(tissue[k], take * fall);
                if (tissue[k] - r <= Bare) r = tissue[k]; // the last of it comes away with the stroke (at any frame rate)
                tissue[k] -= r;
                removed += r;
            }
        if (pressing && overBare)
        {
            gouge += dt * Spec.strain * 0.6;
            Say("You're gouging bare bone: keep it moving to where there is still tissue.", true);
        }
        if (speed > BreakSpeed)
        {
            breakage += dt * (speed - BreakSpeed) / BreakSpeed * Spec.strain * 0.5;
            Say("Too frantic: it skims and breaks.", true);
        }
        if (Coverage >= 0.96f)
        {
            Done = true;
            Holding = false;
            Say("Clean.");
        }
    }

    /// <summary>Accepts it as it is (partly clean).</summary>
    public void Accept()
    {
        if (!Done)
        {
            Done = true;
            Holding = false;
            Say(Coverage >= 0.8f ? "Clean enough." : "Left half done.");
        }
    }

    public override List<HarvestStageResult> Finish()
    {
        HarvestStageResult r = NewResult();
        r.precision = C01(Coverage);
        r.completeness = C01(Coverage);
        r.damage = C01(gouge * 0.5 + breakage);
        if (gouge > 0.1) r.marks |= HarvestMarks.Nicked;
        if (breakage > 0.1) r.marks |= HarvestMarks.Ripped;
        return new List<HarvestStageResult> { r };
    }
}
