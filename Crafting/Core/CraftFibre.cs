using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An item whose copies are raw fibre of differing quality (a fine fleece from a well kept sheep, a matted one): a copy put
/// into a workshop as loose fibre makes a batch shaped by its quality (see <see cref="CraftFibre.Grade"/>).
/// </summary>
public interface ICraftFibreQuality
{
    /// <summary>The quality (0-1) of the copy with this per-copy code; below 0: unknown (the supply's typical batch).</summary>
    float FibreQuality(int code);
}

/// <summary>A batch of loose fibre's state: how dirty, greasy, tangled and aligned it is, how much is short or knotted.</summary>
[Serializable]
public class FibreState
{
    [Tooltip("The raw fibre it is (a Fibre supply id: wool_fibre, flax_fibre...).")]
    public string fibre = "";
    [Tooltip("Grams of fibre (dry).")]
    public double grams;
    [Tooltip("Dirt and vegetable matter left in it (0-1): burrs, seeds, shives.")]
    public double dirt;
    [Tooltip("Grease, wax or gum left in it (0-1): lanolin, sericin.")]
    public double grease;
    [Tooltip("How matted and tangled the locks still are (0 open - 1 compacted).")]
    public double tangle = 0.8;
    [Tooltip("How parallel the fibres lie (0 every which way - 1 combed straight).")]
    public double alignment = 0.1;
    [Tooltip("Short and broken fibres in it (0-1).")]
    public double shortFibre;
    [Tooltip("Neps: little knots of fibre from carding too hard (0-1).")]
    public double neps;
    [Tooltip("Felted (0-1): wool scoured hot and agitated, or shocked from hot to cold.")]
    public double felted;
    [Tooltip("Water in it (0 dry - 1 dripping).")]
    public double wet;
    [Tooltip("Soap left in it from too few rinses (0-1).")]
    public double soap;
    [Tooltip("What has been done to it.")]
    public bool cleaned, scoured, opened, carded, combed;
}

/// <summary>Spun yarn: singles or plied, how long, how even, how twisted, how strong, and whether its length is known.</summary>
[Serializable]
public class YarnState
{
    [Tooltip("The fibre it was spun from (a Fibre supply id).")]
    public string fibre = "";
    public double grams;
    [Tooltip("Metres of yarn.")]
    public double length;
    [Tooltip("How even its thickness is (0-1).")]
    public double evenness = 0.6;
    [Tooltip("Twist (0 under-twisted, 0.5 right, 1 over-twisted).")]
    public double twist = 0.5;
    [Tooltip("Strands plied together (1 = singles).")]
    public int plies = 1;
    [Tooltip("Plied: how balanced the ply twist is against the singles' (0-1; 1 hangs straight).")]
    public double balance = 1;
    [Tooltip("Strength (1 = ordinary linen thread).")]
    public double strength = 1;
    [Tooltip("How well prepared the fibre was (0-1): clean, scoured, carded or combed, without neps or short fibre.")]
    public double prep = 0.5;
    [Tooltip("Spun from combed fibre (a smooth worsted) rather than carded (an airy woollen).")]
    public bool worsted;
    [Tooltip("Wound into a measured skein: the length counted, and how far off the count was (0 exact).")]
    public bool measured;
    public double measuredLength, measureError;
}

/// <summary>A pattern laid out and chalked on a hide or a length of cloth, before it is cut.</summary>
[Serializable]
public class CraftLayoutPlacement
{
    public string pattern = "";
    public int x, y, rotation;
}

/// <summary>
/// Fibre into yarn: <b>cleaning</b> (picking out burrs, seeds and shives), <b>scouring</b> (hot water and soap lift the
/// grease - too hot, agitated or shocked and wool felts), <b>opening</b> (teasing the matted locks apart - torn hard,
/// the fibres break), <b>carding</b> (light strokes of the hand cards align the fibres into an airy rolag - heavy ones
/// knot neps into it), <b>combing</b> (long fibres pulled through the combs come out straight and the short ones stay
/// behind as noil: a smooth, strong worsted), <b>spinning</b> (as well as the fibre was prepared and the hands drafted),
/// <b>plying</b> (two or three singles twisted the other way: stronger, more even, and balanced when the ply twist
/// matches the singles'), <b>joining</b> (bobbins of the same yarn wound together onto one, for a warp longer than one
/// bobbin holds), <b>measuring</b> (winding a skein on the niddy-noddy and counting the wraps: a skein's length must be
/// known before it is reeled into thread).
/// </summary>
public static class CraftFibre
{
    /// <summary>Metres of thread one thread item is.</summary>
    public const double MetresPerThread = 20;

    /// <summary>
    /// The grams of a skein of thread - <see cref="MetresPerThread"/> of a middling yarn (a half-prepared fibre drafted at
    /// 0.5): what one thread item weighs, in the bag and in the seams it is sewn into, and the fibre its price is weighed by.
    /// </summary>
    public static double SkeinGrams => MetresPerThread / Math.Max(0.1, MetresSpun(1, 0.5, 0.5));

    /// <summary>Metres one wrap round the niddy-noddy is.</summary>
    public const double MetresPerWrap = 1.5;
    /// <summary>Grams of fibre one bobbin of spinning holds (the wheel spins a bobbin at a time; the guard holds a request to it).</summary>
    public const int BobbinGrams = 30;
    /// <summary>How much stronger a worsted (spun from combed fibre) is than a woollen of the same fibre (×).</summary>
    public const double WorstedStrength = 1.12;

    // ---- carding: how hard the top card presses (0 just touching the fibre - 1 down on the bottom card's teeth)
    /// <summary>Below this the card barely touches the fibre: it aligns nothing.</summary>
    public const double CardTouch = 0.05;
    /// <summary>From here a stroke aligns fully (the light band: <see cref="CardLight"/> to <see cref="CardNeps"/>).</summary>
    public const double CardLight = 0.25;
    /// <summary>Pressed harder than this, the teeth roll the fibre into neps.</summary>
    public const double CardNeps = 0.4;
    /// <summary>Pressed harder than this, the teeth break fibres too.</summary>
    public const double CardBreaks = 0.5;
    /// <summary>A stroke this hard is heavy-handed (the cards say so).</summary>
    public const double CardHard = 0.6;

    // ---- scouring
    /// <summary>A rinse this much colder (or hotter) than the bath starts to shock wool (°C)...</summary>
    public const double ShockFrom = 15;
    /// <summary>...and at this much it felts it fully.</summary>
    public const double ShockFull = 40;
    /// <summary>A rinse this far from the bath's heat felts wool enough to leave the Felted defect (°C).</summary>
    public const double ShockFelts = 25;
    /// <summary>Soap one scour with a detergent leaves in the fibre, and what each rinse takes out.</summary>
    public const double SoapPerScour = 0.4, SoapPerRinse = 0.25;

    // ---- plying
    /// <summary>A ply balances the singles when its twist is this share of theirs (the other way).</summary>
    public const double PlyBalanceAt = 0.8;
    /// <summary>A ply hangs straight when its balance is above this...</summary>
    public const double PlyStraight = 0.75;
    /// <summary>...which is a ply twist within this of <see cref="PlyBalanceAt"/> × the singles' twist.</summary>
    public const double PlyBand = (1 - PlyStraight) / 2.2;

    public static CraftSupplyDefinition Def(CraftRules rules, CraftPiece p) =>
        rules.Supply(p.kind == CraftPieceKind.Yarn ? p.yarn.fibre : p.fibre.fibre);

    /// <summary>A batch of raw fibre as it comes off the sheep or the field.</summary>
    public static CraftPiece NewFibre(CraftRules rules, string fibreId, double grams = 0)
    {
        CraftSupplyDefinition d = rules.Supply(fibreId);
        if (d == null || d.kind != CraftSupplyKind.Fibre)
            return null;
        var p = new CraftPiece { kind = CraftPieceKind.Fibre, origin = $"a batch of {d.Name.ToLowerInvariant()}" };
        p.fibre.fibre = d.id;
        p.fibre.grams = grams > 0 ? grams : d.gramsPerItem;
        p.fibre.dirt = d.dirt;
        p.fibre.grease = d.grease;
        p.fibre.shortFibre = d.shortFibre;
        p.fibre.tangle = 0.8;
        p.fibre.alignment = 0.1;
        return p;
    }

    /// <summary>
    /// A batch made from a copy of known quality (0-1; below 0: unknown, the supply's typical batch): a fine fleece comes
    /// cleaner and sounder, a poor one dirtier and with more short, broken fibre. Quality 0.6 is the supply's typical batch;
    /// 1 has half its dirt and short fibre, 0 three quarters more.
    /// </summary>
    public static void Grade(CraftPiece p, float quality)
    {
        if (p == null || p.kind != CraftPieceKind.Fibre || quality < 0f || float.IsNaN(quality))
            return;
        double q = Math.Min(1.0, quality);
        double f = 1.0 + (0.6 - q) * 1.25;
        p.fibre.dirt = Math.Max(0, Math.Min(1, p.fibre.dirt * f));
        p.fibre.shortFibre = Math.Max(0, Math.Min(1, p.fibre.shortFibre * f));
    }

    /// <summary>Wet fibre dries in the air (slower in damp air).</summary>
    public static void Step(CraftWorkshop ws, CraftPiece p, double dt)
    {
        if (p.fibre.wet <= 0 || p.location != CraftLocation.Air)
            return;
        double hours = dt * ws.Rules.Settings.processHoursPerSecond;
        p.fibre.wet = Math.Max(0, p.fibre.wet - hours * 0.35 * (1.3 - ws.Humidity) * (1 + Math.Max(0, ws.Ambient - 18) / 25));
    }

    /// <summary>
    /// How ready it is to spin (0-1): aligned, open, clean, without short fibre, neps, felting or soap. Grease left in
    /// it is spun "in the grease" (it spins, but the yarn stays oily and heavy).
    /// </summary>
    public static double Prep(FibreState f) =>
        CraftMath.Clamp01((0.35 + 0.65 * f.alignment) * (1 - 0.7 * f.tangle) * (1 - 0.8 * f.dirt) * (1 - 0.6 * f.shortFibre)
                          * (1 - 0.7 * f.neps) * (1 - 0.9 * f.felted) * (1 - 0.3 * f.grease) * (1 - 0.4 * f.soap));

    /// <summary>
    /// How good a yarn is (0-1): even, rightly twisted, balanced if plied. How well the fibre was prepared is already in
    /// its evenness (<see cref="Spin"/>), so it is counted once: a raw batch spins as well as raw fibre from the bag.
    /// </summary>
    public static double Quality(YarnState y) =>
        CraftMath.Clamp01(CraftTextiles.SpinQuality(y.evenness, y.twist) * (y.plies > 1 ? 0.85 + 0.15 * y.balance : 1) + (y.plies > 1 ? 0.08 : 0));

    /// <summary>
    /// Metres of yarn <paramref name="grams"/> of fibre spin into, drafted <paramref name="fine"/> (0 thick - 1 fine) from
    /// fibre prepared <paramref name="prep"/> (0-1): a fine draft draws the same fibre out much further.
    /// </summary>
    public static double MetresSpun(double grams, double fine, double prep) =>
        Math.Max(0, grams) * (1.2 + 6.5 * CraftMath.Clamp01(fine)) * (0.85 + 0.15 * CraftMath.Clamp01(prep));

    /// <summary>
    /// What a draft keeps of the hands' evenness (0-1): the fibre's preparation always shows, and a fine draft of poorly
    /// prepared fibre - tangled, greasy, full of short fibre - draws out lumpy (fine yarn wants well-prepared fibre).
    /// </summary>
    public static double DraftEvenness(double prep, double fine) =>
        (0.55 + 0.45 * CraftMath.Clamp01(prep)) * (1 - 0.3 * CraftMath.Clamp01(fine) * (1 - CraftMath.Clamp01(prep)));

    /// <summary>
    /// The twist of yarn made of stretches of different twist (bobbins wound together, strands plied): the twist that
    /// is as far from right (0.5) as the stretches are on average, on the side most of it lies - an under-twisted and an
    /// over-twisted stretch never make a rightly twisted yarn.
    /// </summary>
    public static double MixedTwist(IList<(double twist, double weight)> parts)
    {
        double w = 0, off = 0, side = 0;
        foreach ((double twist, double weight) in parts)
        {
            double d = CraftMath.Clamp01(twist) - 0.5, k = Math.Max(0, weight);
            w += k;
            off += Math.Abs(d) * k;
            side += d * k;
        }
        if (w <= 0) return 0.5;
        return CraftMath.Clamp01(0.5 + (side >= 0 ? 1 : -1) * off / w);
    }

    // ------------------------------------------------------------------ preparing fibre
    /// <summary>
    /// The share of a batch's dirt picking takes out when <paramref name="share"/> (0-1) of the burrs and seeds that can
    /// be seen were picked with tools of <paramref name="toolQuality"/>: fine tweezers pick more, but never every last
    /// speck (some always hide deeper in the fibre).
    /// </summary>
    public static double PickedOut(double share, double toolQuality) => Math.Min(0.95, CraftMath.Clamp01(share) * 0.85 * Math.Min(1.2, toolQuality));

    /// <summary>
    /// How far a batch gives when its locks are pulled apart (0-1): felted fibre hardly opens, and wet fibre clings
    /// (teased wet, it opens little more than half as far: let it dry first).
    /// </summary>
    public static double OpenGive(FibreState f) => (1 - 0.8 * CraftMath.Clamp01(f.felted)) * (f.wet > 0.4 ? 0.6 : 1);

    /// <summary>Picks out dirt: <c>a</c> = share of the debris picked out (0-1), <c>b</c> = fibre pulled out with it (0-1).</summary>
    public static void Clean(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Fibre)
            return;
        // fine tweezers pick more, but never every last speck
        double got = PickedOut(op.a, ws.ToolQuality), lost = CraftMath.Clamp01(op.b);
        p.fibre.dirt *= 1 - got;
        p.fibre.grams *= 1 - lost * 0.08;
        p.fibre.cleaned |= p.fibre.dirt < 0.1;
        p.Record(ws.Elapsed, CraftOpKind.CleanFibre, p.fibre.dirt < 0.1 ? "Picked clean" : "Picked over", p.fibre.dirt);
    }

    /// <summary>
    /// The heat a fibre's scouring bath wants (°C): grease (or silk's gum) starts to lift at <paramref name="from"/> and
    /// lifts fully from <paramref name="full"/> - wool, flax, hemp and cotton from about 35 to 62 °C, silk near boiling.
    /// </summary>
    public static void ScourHeat(CraftSupplyDefinition fibre, out double from, out double full)
    {
        bool silk = fibre != null && fibre.id.IndexOf("silk", StringComparison.OrdinalIgnoreCase) >= 0;
        from = silk ? 70 : 35;
        full = silk ? 95 : 62;
    }

    /// <summary>
    /// Minutes of soaking a second at the bath counts (the fibre bench's soak gauge), and the longest soak the bath takes:
    /// the station bounds a scour's soak to the real time it had (<see cref="CraftRequestGuard"/>).
    /// </summary>
    public const double SoakMinutesPerSecond = 1, MaxSoakMinutes = 240;

    /// <summary>Minutes of soaking that lift about two-thirds of what the bath's heat can lift (three times this: nearly all).</summary>
    public static double SoakMinutes(CraftSupplyDefinition fibre) =>
        fibre != null && fibre.id.IndexOf("silk", StringComparison.OrdinalIgnoreCase) >= 0 ? 40 : 15;

    /// <summary>The soap left in fibre that held <paramref name="soap"/>, scoured with a detergent (or water alone) and rinsed <paramref name="rinses"/> times.</summary>
    public static double SoapAfter(double soap, bool detergent, int rinses) =>
        detergent ? CraftMath.Clamp01(soap + SoapPerScour - SoapPerRinse * Math.Max(0, rinses)) : Math.Max(0, soap - SoapPerRinse * Math.Max(0, rinses));

    /// <summary>
    /// Scours it: <c>a</c> = the bath's temperature (°C), <c>b</c> = minutes soaking, <c>c</c> = how much it was stirred
    /// and squeezed (0 left still - 1 churned), <c>d</c> = how far the rinse's temperature differed from the bath (°C),
    /// <c>id</c> = the detergent (empty = water alone), <c>i0</c> = rinses. Grease melts out from about 35 °C and fully
    /// from about 62 °C (<see cref="ScourHeat"/>), best with soap; wool felts when it is hot and agitated or shocked hot to
    /// cold (<see cref="ShockFrom"/>); silk's gum needs near-boiling water.
    /// </summary>
    public static void Scour(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Fibre)
            return;
        FibreState f = p.fibre;
        CraftSupplyDefinition d = Def(ws.Rules, p);
        CraftSupplyDefinition soap = ws.Rules.Supply(op.id);
        double t = op.a, minutes = Math.Max(0, op.b), stir = CraftMath.Clamp01(op.c), shock = Math.Abs(op.d);
        ScourHeat(d, out double from, out double full);
        double melt = CraftMath.Smooth(from, full, t);
        double eff = melt * (soap != null ? Math.Min(1.3, 0.75 + 0.25 * soap.strength) : 0.4) * (1 - Math.Exp(-minutes / SoakMinutes(d)));
        f.grease *= 1 - CraftMath.Clamp01(eff) * 0.95;
        f.dirt *= 1 - CraftMath.Clamp01(eff) * 0.45;
        if (d != null && d.felts)
        {
            double felt = stir * CraftMath.Smooth(30, 70, t) * 0.55 + CraftMath.Smooth(ShockFrom, ShockFull, shock) * 0.45;
            f.felted = CraftMath.Clamp01(f.felted + felt);
            if (felt > 0.15)
            {
                p.AddDefect(CraftDefectKind.Felted, -1, felt, true, stir > 0.4 ? "churned in hot water, the wool felted" : "shocked from hot to cold, the wool felted");
                ws.Raise(CraftEventKind.Note, p.id, "felted", "The wool has matted into felt.");
            }
        }
        int rinses = Math.Max(0, op.i0);
        f.soap = SoapAfter(f.soap, soap != null, rinses);
        f.wet = 1;
        f.scoured |= f.grease < 0.12;
        string how = f.felted > 0.3 ? "Scoured - and it has felted into a mat" : f.grease < 0.12 ? "Scoured clean of grease" : melt < 0.3 ? "Washed in water too cool to lift the grease" : "Scoured, some grease still in it";
        p.Record(ws.Elapsed, CraftOpKind.ScourFibre, how, f.grease);
    }

    /// <summary>
    /// Teases the matted locks open: <c>a</c> = how much was opened (0-1), <c>b</c> = how hard it was torn (0 gently
    /// teased - 1 ripped). Felted fibre hardly opens; torn hard, the fibres break.
    /// </summary>
    public static void Open(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Fibre)
            return;
        FibreState f = p.fibre;
        double done = CraftMath.Clamp01(op.a), harsh = CraftMath.Clamp01(op.b);
        f.tangle *= 1 - done * 0.7 * OpenGive(f);
        f.shortFibre = CraftMath.Clamp01(f.shortFibre + done * harsh * 0.08);
        f.dirt *= 1 - done * 0.2;
        f.opened |= f.tangle < 0.4;
        p.Record(ws.Elapsed, CraftOpKind.OpenFibre, harsh > 0.6 ? "Torn open: fibres broke" : f.tangle < 0.4 ? "Teased open" : "Teased, still lumpy", f.tangle);
    }

    /// <summary>How much of a stroke's aligning a card pressed this hard does (0 hovering above the fibre - 1 from <see cref="CardLight"/>).</summary>
    public static double CardGrip(double press) => CraftMath.Smooth(CardTouch, CardLight, press);

    /// <summary>
    /// Cards it: <c>i0</c> = strokes, <c>a</c> = pressure (0 just touching the fibre - 1 down on the teeth; light is
    /// right: from <see cref="CardLight"/> to <see cref="CardNeps"/>), <c>b</c> = how even and in one direction the
    /// strokes were (0-1). The fibres align (to about two-thirds - carding does not straighten like combing) as far as
    /// the teeth reach into them (a card that hardly touches the fibre aligns nothing), the last tangles open; hard strokes
    /// and unopened locks knot neps and break fibres.
    /// </summary>
    public static void Card(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Fibre)
            return;
        FibreState f = p.fibre;
        int n = Math.Max(1, op.i0);
        double press = CraftMath.Clamp01(op.a), even = CraftMath.Clamp01(op.b), tq = ws.ToolQuality;
        double clog = Math.Max(0, f.tangle - 0.5) + (f.wet > 0.25 ? f.wet * 0.6 : 0) + f.felted * 0.6;
        double gain = 0.1 * n * (0.5 + 0.5 * even) * Math.Min(1.3, tq) * (1 - Math.Min(0.8, clog)) * CardGrip(press);
        f.alignment += (0.65 - f.alignment) * Math.Min(1, gain);
        f.tangle *= Math.Exp(-0.15 * n * CardGrip(press));
        f.neps = CraftMath.Clamp01(f.neps + n * (Math.Max(0, press - CardNeps) * 0.02 + clog * 0.01 + (f.alignment > 0.6 ? 0.001 : 0)));
        f.shortFibre = CraftMath.Clamp01(f.shortFibre + n * Math.Max(0, press - CardBreaks) * 0.01 / Math.Max(0.6, tq));
        f.dirt *= Math.Exp(-0.03 * n * CardGrip(press));
        f.carded |= f.alignment > 0.5;
        if (press > CardHard && n >= 4) ws.Raise(CraftEventKind.Note, p.id, "neps", "Little knots of fibre roll up between the cards.");
        p.Record(ws.Elapsed, CraftOpKind.CardFibre, clog > 0.3 ? "Carded: the cards clogged and knotted it" : press > CardHard ? "Carded hard: neps and broken fibre in it"
            : press > CardNeps ? "Carded a little hard: a few neps in it" : press < CardLight ? "Carded too lightly: the teeth hardly reached it" : "Carded into a light rolag", f.alignment);
    }

    /// <summary>
    /// Combs it: <c>i0</c> = passes, <c>a</c> = how true each pass was (0-1). Long fibres come out straight (to near 1)
    /// and the short ones, neps and dirt stay in the comb as noil (weight lost). Cotton and short tow are too short to comb.
    /// </summary>
    public static void Comb(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Fibre)
            return;
        FibreState f = p.fibre;
        int n = Math.Max(1, op.i0);
        double tr = CraftMath.Clamp01(op.a), tq = ws.ToolQuality;
        f.alignment += (0.95 - f.alignment) * Math.Min(1, 0.25 * n * tr * Math.Min(1.2, tq));
        double removed = f.shortFibre * (1 - Math.Exp(-0.4 * n)) * tr;
        f.grams *= 1 - removed * 0.8 - 0.01 * n; // the noil, and a little long fibre with it
        f.shortFibre = Math.Max(0, f.shortFibre - removed);
        f.neps *= Math.Exp(-0.3 * n * tr);
        f.dirt *= Math.Exp(-0.4 * n * tr);
        f.tangle *= Math.Exp(-0.3 * n);
        f.combed |= f.alignment > 0.85;
        p.Record(ws.Elapsed, CraftOpKind.CombFibre, f.combed ? "Combed into a smooth top" : "Combed", f.alignment);
    }

    // ------------------------------------------------------------------ spinning, plying, measuring
    /// <summary>
    /// Spins <c>i0</c> grams of the batch into a new yarn: <c>a</c> = the draft's evenness, <c>b</c> = the twist (0-1),
    /// <c>c</c> = how fine it was drafted (0 thick - 1 fine: more metres to the gram, <see cref="MetresSpun"/>). The yarn
    /// is as good as the hands and the preparation (<see cref="DraftEvenness"/>: a fine draft wants well-prepared fibre):
    /// combed fibre spins a smooth, strong worsted, carded a softer woollen; unprepared fibre lumpy.
    /// </summary>
    public static void Spin(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Fibre)
            return;
        FibreState f = p.fibre;
        CraftSupplyDefinition d = Def(ws.Rules, p);
        double grams = Math.Min(f.grams, Math.Max(1, op.i0));
        double prep = Prep(f), fine = CraftMath.Clamp01(op.c);
        CraftPiece y = ws.Create(op, CraftPieceKind.Yarn, $"spun from {d?.Name.ToLowerInvariant() ?? "fibre"}");
        y.yarn.fibre = f.fibre;
        y.yarn.grams = grams * (1 + 0.3 * f.grease);
        y.yarn.length = MetresSpun(grams, fine, prep);
        y.yarn.evenness = CraftMath.Clamp01(CraftMath.Clamp01(op.a) * DraftEvenness(prep, fine));
        y.yarn.twist = CraftMath.Clamp01(op.b);
        y.yarn.prep = prep;
        y.yarn.worsted = f.combed;
        double twistF = 1 - Math.Min(1, Math.Abs(y.yarn.twist - 0.5) * 1.6);
        y.yarn.strength = (d?.strength ?? 1) * (0.5 + 0.5 * twistF) * (0.6 + 0.4 * y.yarn.evenness) * (f.combed ? WorstedStrength : 1) * (1 - 0.5 * f.shortFibre) * (1 - 0.5 * f.neps);
        f.grams -= grams;
        y.Record(ws.Elapsed, CraftOpKind.SpinFibre, f.combed ? "Spun worsted from the combed top" : f.carded ? "Spun woollen from rolags" : "Spun from unprepared fibre: lumpy", y.yarn.evenness);
        if (f.grams < 0.5)
            ws.Destroy(p, "The batch is all spun.");
    }

    /// <summary>How balanced a ply twist of <paramref name="ply"/> is against singles of <paramref name="singlesTwist"/> (0-1; above <see cref="PlyStraight"/> it hangs straight).</summary>
    public static double PlyBalance(double ply, double singlesTwist) =>
        CraftMath.Clamp01(1 - Math.Abs(CraftMath.Clamp01(ply) - PlyBalanceAt * CraftMath.Clamp01(singlesTwist)) * 2.2);

    /// <summary>The singles' twist a ply of these strands balances (<see cref="MixedTwist"/> of them, by length).</summary>
    public static double SinglesTwist(IList<CraftPiece> strands)
    {
        var parts = new List<(double, double)>();
        foreach (CraftPiece s in strands)
            if (s != null && s.kind == CraftPieceKind.Yarn) parts.Add((s.yarn.twist, 1));
        return MixedTwist(parts);
    }

    /// <summary>
    /// Plies <c>piece</c> with <c>other</c> (and a third strand, <c>id</c>): <c>a</c> = the ply twist (0-1; balanced near
    /// <see cref="PlyBalanceAt"/> of the singles' twist, the other way), <c>b</c> = how evenly the strands were tensioned
    /// (0-1). The strands even each other out and add their strength; the shortest sets the length. The plied yarn is
    /// wound on the bobbin that ran out (the shortest strand's; on a tie the first's): every other strand keeps what is
    /// left on its bobbin (a bobbin with less than half a metre left is spent) - and none is the skein it was counted as
    /// any more (its length must be counted again before it is reeled).
    /// </summary>
    public static void Ply(CraftWorkshop ws, CraftPiece a, CraftPiece b, CraftPiece c, CraftOperation op)
    {
        if (a == null || b == null || a.kind != CraftPieceKind.Yarn || b.kind != CraftPieceKind.Yarn)
            return;
        var strands = new List<CraftPiece> { a, b };
        if (c != null && c.kind == CraftPieceKind.Yarn && c != a && c != b) strands.Add(c);
        double len = double.MaxValue, even = 0, strength = 0, prep = 0, grams = 0;
        int plies = 0;
        bool worsted = true;
        CraftPiece onto = a;
        foreach (CraftPiece s in strands)
        {
            len = Math.Min(len, s.yarn.length);
            if (s.yarn.length < onto.yarn.length) onto = s; // strictly shorter: a tie keeps the first
            even += s.yarn.evenness;
            prep += s.yarn.prep;
            plies += s.yarn.plies;
            worsted &= s.yarn.worsted;
        }
        int k = strands.Count;
        even /= k; prep /= k;
        double twist = SinglesTwist(strands);
        double ply = CraftMath.Clamp01(op.a), tension = CraftMath.Clamp01(op.b);
        double balance = PlyBalance(ply, twist);
        string fibre = a.yarn.fibre;
        foreach (CraftPiece s in strands)
        {
            double share = s.yarn.length > 0 ? Math.Min(1, len / s.yarn.length) : 0;
            grams += s.yarn.grams * share;
            strength += s.yarn.strength;
            s.yarn.grams *= 1 - share;
            s.yarn.length = Math.Max(0, s.yarn.length - len);
            // what is left on a bobbin is not the skein that was counted
            s.yarn.measured = false;
            s.yarn.measuredLength = s.yarn.measureError = 0;
        }
        YarnState y = onto.yarn;
        y.fibre = fibre;
        y.length = len * 0.95;
        y.grams = grams;
        y.plies = plies;
        y.twist = twist;
        y.prep = prep;
        y.worsted = worsted;
        y.balance = balance;
        y.evenness = CraftMath.Clamp01(even + (1 - even) * 0.35 - (1 - tension) * 0.2);
        y.strength = strength * (0.85 + 0.15 * balance) * (0.85 + 0.15 * tension);
        y.measured = false;
        y.measuredLength = y.measureError = 0;
        foreach (CraftPiece s in strands)
            if (s != onto && s.yarn.length < 0.5)
                ws.Destroy(s, "A bobbin is all plied in.");
        onto.Record(ws.Elapsed, CraftOpKind.PlyYarn, balance > PlyStraight ? $"Plied {plies}-ply: it hangs straight" : $"Plied {plies}-ply: it twists on itself", balance);
    }

    /// <summary>
    /// Winds the yarn on bobbin <paramref name="from"/> onto bobbin <paramref name="onto"/>, end to end (the caller checks
    /// they are the same fibre and the same plies): one bobbin long enough for a warp no single bobbin holds. Lengths and
    /// grams add; evenness, prep, strength and balance are the stretches' own, by length (<see cref="MixedTwist"/> for the
    /// twist: joining never puts a twist right); the length is no longer counted; the emptied bobbin is gone.
    /// </summary>
    public static void Join(CraftWorkshop ws, CraftPiece onto, CraftPiece from, CraftOperation op)
    {
        if (onto == null || from == null || onto == from || onto.kind != CraftPieceKind.Yarn || from.kind != CraftPieceKind.Yarn)
            return;
        YarnState y = onto.yarn, z = from.yarn;
        double la = Math.Max(0, y.length), lb = Math.Max(0, z.length), total = la + lb;
        double Mean(double u, double v) => total > 0 ? (u * la + v * lb) / total : (u + v) / 2;
        y.twist = MixedTwist(new List<(double, double)> { (y.twist, la), (z.twist, lb) });
        y.evenness = CraftMath.Clamp01(Mean(y.evenness, z.evenness));
        y.prep = CraftMath.Clamp01(Mean(y.prep, z.prep));
        y.strength = Math.Max(0, Mean(y.strength, z.strength));
        y.balance = CraftMath.Clamp01(Mean(y.balance, z.balance));
        y.worsted &= z.worsted;
        y.length = total;
        y.grams = Math.Max(0, y.grams) + Math.Max(0, z.grams);
        y.measured = false;
        y.measuredLength = y.measureError = 0;
        ws.Destroy(from, "A bobbin is wound onto another.");
        onto.Record(ws.Elapsed, CraftOpKind.JoinYarn, $"Another bobbin wound on: {total:0} m of yarn", total);
    }

    /// <summary>Winds it into a skein and counts it: <c>a</c> = the length counted (m). The count may be off.</summary>
    public static void Measure(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Yarn)
            return;
        p.yarn.measured = true;
        p.yarn.measuredLength = Math.Max(0, op.a);
        p.yarn.measureError = p.yarn.length > 0 ? Math.Abs(op.a - p.yarn.length) / p.yarn.length : 1;
        p.Record(ws.Elapsed, CraftOpKind.MeasureYarn, p.yarn.measureError < 0.05 ? "Wound and counted into a skein" : "Wound into a skein - the count is off", p.yarn.measuredLength);
    }

    /// <summary>The thread a yarn reels into (a Thread supply of its fibre: coarse, even or fine by its quality).</summary>
    public static string ThreadOf(CraftRules rules, CraftPiece p)
    {
        CraftSupplyDefinition d = rules.Supply(p.yarn.fibre);
        if (d == null || d.spinsInto == null || d.spinsInto.Count == 0)
            return null;
        int tier = CraftTextiles.ThreadTier(Quality(p.yarn));
        for (int t = Math.Min(tier, d.spinsInto.Count - 1); t >= 0; t--)
            if (rules.Supply(d.spinsInto[t]) != null) return d.spinsInto[t];
        return null;
    }

    // ------------------------------------------------------------------ weaving
    /// <summary>Metres of yarn a cell of cloth (a hand's breadth square) takes, warp and weft together.</summary>
    public const double MetresPerCell = 12;
    /// <summary>The narrowest and widest warp a loom takes (cells), and the most rows one length is woven to.</summary>
    public const int MinWidth = 2, MaxWidth = 16, MaxRows = 30;

    /// <summary>Rows of cloth <paramref name="width"/> cells wide that <paramref name="metres"/> of yarn weave (at most <see cref="MaxRows"/>).</summary>
    public static int RowsFor(double metres, int width) =>
        Math.Min(MaxRows, (int)Math.Floor(Math.Max(0, metres) / (MetresPerCell * Math.Max(MinWidth, Math.Min(MaxWidth, width)))));

    /// <summary>
    /// How sound a yarn weaves (0-1): its strength against the best its fibre spins (a combed worsted, even and rightly
    /// twisted: <see cref="WorstedStrength"/> × the fibre's), strand by strand. A kinked or loosely twisted, lumpy yarn,
    /// or one spun from short and knotted fibre, weaves a weaker cloth.
    /// </summary>
    public static double WeaveSoundness(CraftRules rules, YarnState y)
    {
        double best = (rules?.Supply(y.fibre)?.strength ?? 1) * WorstedStrength * Math.Max(1, y.plies);
        return CraftMath.Clamp01(y.strength / Math.Max(1e-6, best));
    }

    /// <summary>A yarn less sound than this (<see cref="WeaveSoundness"/>) is called weak where it is woven.</summary>
    public const double WeakYarn = 0.45;

    /// <summary>How sound a row beaten <paramref name="beat"/> (0-1) true is: packed close and even (1), or its picks lie loose (an uneven beat).</summary>
    public static double RowSoundness(double beat) => 0.8 + 0.2 * CraftMath.Clamp01(beat);

    /// <summary>The cloth a yarn weaves into (its fibre's <c>weavesInto</c>).</summary>
    public static CraftHideDefinition ClothOf(CraftRules rules, CraftPiece p)
    {
        CraftSupplyDefinition d = p != null && p.kind == CraftPieceKind.Yarn ? rules.Supply(p.yarn.fibre) : null;
        return d == null || string.IsNullOrEmpty(d.weavesInto) ? null : rules.Hide(d.weavesInto);
    }

    /// <summary>
    /// Weaves a yarn into a length of cloth on the loom: <c>i0</c> = the width (cells), <c>a</c> = how evenly the warp
    /// was tensioned (0-1), <c>b</c> = how close it was beaten (0 open, 0.5 balanced, 1 packed), <c>points</c> = how
    /// true each row's beat was (the loom's timing bar, 0-1). One row is woven for every beat, never more than the yarn
    /// allows (<see cref="RowsFor"/>: <see cref="MetresPerCell"/> a cell, at most <see cref="MaxRows"/>; wind more bobbins
    /// on for a longer length); the rest of the yarn stays on the bobbin. The cloth is as sound as its yarn
    /// (<see cref="WeaveSoundness"/>) and each row as its beat (<see cref="RowSoundness"/>: a row beaten off true lies
    /// looser, and more of its patches are flawed); a row beaten badly (below the damage threshold) leaves a skipped pick
    /// (a flaw), an uneven warp puckers the cloth, a lumpy yarn shows slubs, an over-twisted one weaves a harsh, wiry
    /// cloth. The cloth is a new piece the tailor lays out, cuts and sews.
    /// </summary>
    public static void Weave(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Yarn)
            return;
        CraftHideDefinition cloth = ClothOf(ws.Rules, p);
        if (cloth == null)
            return;
        YarnState y = p.yarn;
        int width = Math.Max(MinWidth, Math.Min(MaxWidth, op.i0));
        int rows = RowsFor(y.length, width);
        if (rows < 1)
            return;
        // as many rows as were beaten in (the rest of the yarn stays on the bobbin)
        if (op.points.Count > 0) rows = Math.Min(rows, op.points.Count);
        CraftPiece c = ws.Create(op, CraftPieceKind.Hide, p.Derived("woven"));
        CraftPiece made = CraftLeather.NewHide(ws.Rules, cloth.id, unchecked(ws.Seed + (int)ws.Step * 31 + p.record.Count));
        c.hide = made.hide;
        HideState h = c.hide;
        HideCell model = h.cells.Count > 0 ? h.cells[0] : new HideCell();
        h.width = width;
        h.height = rows;
        h.cells.Clear();
        double tension = CraftMath.Clamp01(op.a), density = CraftMath.Clamp01(op.b), q = Quality(y);
        double sett = 1 - Math.Min(1, Math.Abs(density - 0.5) * 1.6);
        // the yarn's own strength (a weak yarn weaves a weak cloth) and its twist (over-twisted, it snarls into a harsh, wiry weave)
        double sound = WeaveSoundness(ws.Rules, y), kink = CraftMath.Clamp01((y.twist - 0.5) * 2);
        // a fine yarn weaves a thinner cloth; a packed beat a thicker, stiffer one
        double thick = CraftMath.Lerp(cloth.thicknessBelly, cloth.thicknessBack, 0.5) * (0.75 + 0.5 * (1 - Math.Min(1, y.length / Math.Max(1, y.grams) / 8))) * (0.85 + 0.3 * density);
        float damageBelow = ws.Rules.Settings.timing.damageBelow;
        int flaws = 0;
        double scoreSum = 0;
        for (int r = 0; r < rows; r++)
        {
            double beat = op.points.Count > 0 ? CraftMath.Clamp01(op.points[r % op.points.Count]) : 0.5;
            scoreSum += beat;
            for (int x = 0; x < width; x++)
            {
                int k = r * width + x;
                var cell = new HideCell
                {
                    present = true, thickness = thick * (0.95 + 0.1 * CraftMath.Noise(h.seed, k, 2)), flesh = 0, hair = 0,
                    // the weave's soundness (the evaluator reads a cloth's strength from it): the yarn's, and the row's beat
                    grain = model.grain * (0.4 + 0.6 * sound) * RowSoundness(beat),
                    tanS = 1, tanC = 1, moisture = 0.08, stiff = CraftMath.Clamp01(model.stiff + (density - 0.5) * 0.5 + 0.3 * kink),
                    smooth = CraftMath.Clamp01(0.3 + 0.35 * q + 0.25 * beat + 0.1 * sett),
                };
                double slub = (1 - y.evenness) * 0.35, pucker = (1 - tension) * 0.3, loose = (1 - beat) * 0.2;
                if (beat < damageBelow)
                {
                    // a skipped pick runs across the row
                    cell.flaw = true;
                    cell.damage = 0.2 + 0.5 * (damageBelow - beat) / Math.Max(0.01, damageBelow);
                }
                else if (CraftMath.Noise(h.seed, k, 4) < slub * 0.3 + pucker * 0.25 + (1 - sett) * 0.1 + loose)
                {
                    cell.flaw = true;
                    cell.damage = 0.1 + 0.3 * CraftMath.Noise(h.seed, k, 5);
                }
                if (cell.flaw) flaws++;
                h.cells.Add(cell);
            }
        }
        h.dried = true;
        double used = rows * width * MetresPerCell;
        double share = y.length > 0 ? Math.Min(1, used / y.length) : 1;
        y.grams *= 1 - share;
        y.length -= used;
        y.measured = false;
        double mean = scoreSum / rows;
        for (int r = 0; r < rows; r++)
            if (op.points.Count > 0 && op.points[r % op.points.Count] < damageBelow)
            {
                ws.Raise(CraftEventKind.Note, c.id, "skipped_pick", "A bad beat: the shuttle skipped a pick.");
                break;
            }
        string weak = sound >= WeakYarn ? "" : y.twist > 0.7 ? " of a weak, kinked yarn" : y.twist < 0.3 ? " of a weak, loosely twisted yarn" : " of a weak yarn";
        c.Record(ws.Elapsed, CraftOpKind.WeaveCloth, $"Woven {width}×{rows}{weak}: beaten {mean * 100:0}% true" + (flaws > 0 ? $", {flaws} flawed patch{(flaws == 1 ? "" : "es")}" : ""), mean);
        if (y.length < 0.5)
            ws.Destroy(p, "The yarn is all woven.");
    }

    /// <summary>Thread items a measured skein gives: its counted length, never more than is really there.</summary>
    public static int ThreadCount(CraftPiece p) =>
        p == null || p.kind != CraftPieceKind.Yarn || !p.yarn.measured ? 0 : (int)Math.Floor(Math.Min(p.yarn.measuredLength, p.yarn.length) / MetresPerThread);
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ the fibre bench and the wheel
    private bool FibreCheck(string id, CraftTools tool, string missing, out CraftPiece p, out string reason)
    {
        reason = "";
        p = Piece(id);
        if (!Has(tool))
            return Refuse(missing, out reason, id);
        if (p == null || p.kind != CraftPieceKind.Fibre)
            return Refuse("That is not loose fibre.", out reason, id);
        if (p.location != CraftLocation.Air)
            return Refuse("Take it out first.", out reason, id);
        return true;
    }

    public bool CleanFibre(string id, double picked, double lost, out string reason)
    {
        if (!FibreCheck(id, CraftTools.FibreBench, "There is no picking table here.", out _, out reason))
            return false;
        Record(new CraftOperation { kind = CraftOpKind.CleanFibre, piece = id, a = (float)CraftMath.Clamp01(picked), b = (float)CraftMath.Clamp01(lost) });
        return true;
    }

    public bool ScourFibre(string id, double celsius, double minutes, double agitation, double shock, string detergent, int rinses, out string reason)
    {
        if (!FibreCheck(id, CraftTools.FibreBench, "There are no scouring tubs here.", out _, out reason))
            return false;
        if (!string.IsNullOrEmpty(detergent) && Rules.Supply(detergent)?.kind != CraftSupplyKind.Detergent)
            return Refuse("That does not scour.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.ScourFibre, piece = id, id = detergent ?? "", a = (float)CraftMath.Clamp(celsius, 0, 100), b = (float)CraftMath.Clamp(minutes, 0, CraftFibre.MaxSoakMinutes), c = (float)CraftMath.Clamp01(agitation), d = (float)CraftMath.Clamp(shock, 0, 100), i0 = Math.Max(0, Math.Min(9, rinses)) });
        return true;
    }

    public bool OpenFibre(string id, double done, double harsh, out string reason)
    {
        if (!FibreCheck(id, CraftTools.FibreBench, "There is no picking table here.", out _, out reason))
            return false;
        Record(new CraftOperation { kind = CraftOpKind.OpenFibre, piece = id, a = (float)CraftMath.Clamp01(done), b = (float)CraftMath.Clamp01(harsh) });
        return true;
    }

    public bool CardFibre(string id, int strokes, double pressure, double evenness, out string reason)
    {
        if (!FibreCheck(id, CraftTools.FibreBench, "There are no hand cards here.", out CraftPiece p, out reason))
            return false;
        if (p.fibre.wet > 0.5)
            return Refuse("It is still wet: let it dry before it is carded.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.CardFibre, piece = id, i0 = Math.Max(1, Math.Min(40, strokes)), a = (float)CraftMath.Clamp01(pressure), b = (float)CraftMath.Clamp01(evenness) });
        return true;
    }

    public bool CombFibre(string id, int passes, double trueness, out string reason)
    {
        if (!FibreCheck(id, CraftTools.FibreBench, "There are no combs here.", out CraftPiece p, out reason))
            return false;
        CraftSupplyDefinition d = CraftFibre.Def(Rules, p);
        if (d != null && d.staple < 40)
            return Refuse($"{d.Name} is too short in the staple to comb: card it.", out reason, id);
        if (p.fibre.wet > 0.5)
            return Refuse("It is still wet: let it dry before it is combed.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.CombFibre, piece = id, i0 = Math.Max(1, Math.Min(20, passes)), a = (float)CraftMath.Clamp01(trueness) });
        return true;
    }

    /// <summary>Spins grams of a prepared batch into yarn (see <see cref="CraftFibre.Spin"/>). Returns the yarn's id.</summary>
    public string SpinFibre(string id, double evenness, double twist, double fineness, int grams, out string reason)
    {
        if (!FibreCheck(id, CraftTools.SpinningWheel, "There is no spinning wheel here.", out CraftPiece p, out reason))
            return null;
        if (p.fibre.wet > 0.3)
        {
            Refuse("It is still wet: let it dry before it is spun.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.SpinFibre, piece = id, a = (float)CraftMath.Clamp01(evenness), b = (float)CraftMath.Clamp01(twist), c = (float)CraftMath.Clamp01(fineness), i0 = Math.Max(1, Math.Min(500, grams)) };
        Record(op);
        return op.other;
    }

    /// <summary>Plies two (or three) yarns together on the wheel (see <see cref="CraftFibre.Ply"/>).</summary>
    public bool PlyYarn(string a, string b, string c, double plyTwist, double tension, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.SpinningWheel))
            return Refuse("There is no spinning wheel here.", out reason, a);
        CraftPiece pa = Piece(a), pb = Piece(b), pc = string.IsNullOrEmpty(c) ? null : Piece(c);
        if (pa == null || pb == null || pa == pb || pa.kind != CraftPieceKind.Yarn || pb.kind != CraftPieceKind.Yarn || pc != null && pc.kind != CraftPieceKind.Yarn)
            return Refuse("Plying needs two (or three) bobbins of yarn.", out reason, a);
        if (pa.location != CraftLocation.Air || pb.location != CraftLocation.Air || pc != null && pc.location != CraftLocation.Air)
            return Refuse("Take them out first.", out reason, a);
        if (pa.yarn.length < 1 || pb.yarn.length < 1 || pc != null && pc.yarn.length < 1)
            return Refuse("There is too little yarn on one bobbin to ply.", out reason, a);
        if (!SameFibre(pa, pb) || pc != null && !SameFibre(pa, pc))
            return Refuse("Ply bobbins spun from the same fibre.", out reason, a);
        Record(new CraftOperation { kind = CraftOpKind.PlyYarn, piece = a, other = b, id = pc != null ? c : "", a = (float)CraftMath.Clamp01(plyTwist), b = (float)CraftMath.Clamp01(tension) });
        return true;
    }

    private static bool SameFibre(CraftPiece x, CraftPiece y) => string.Equals(x.yarn.fibre, y.yarn.fibre, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Could bobbin <paramref name="from"/> be wound onto bobbin <paramref name="onto"/> (<see cref="JoinYarn"/>)? Both
    /// yarn lying on the station, spun from the same fibre and plied alike - a warp of mixed yarn weaves unevenly. Only
    /// asks (nothing is raised): a window checks with it before it offers the bobbin.
    /// </summary>
    public bool CanJoinYarn(string onto, string from, out string reason)
    {
        reason = JoinProblem(onto, from) ?? "";
        return reason.Length == 0;
    }

    private string JoinProblem(string onto, string from)
    {
        CraftPiece a = Piece(onto), b = Piece(from);
        if (!Has(CraftTools.Loom) && !Has(CraftTools.SpinningWheel))
            return "There is no loom or wheel here to wind bobbins together on.";
        if (a == null || b == null || a.kind != CraftPieceKind.Yarn || b.kind != CraftPieceKind.Yarn)
            return "Winding together needs two bobbins of yarn.";
        if (a == b)
            return "Pick another bobbin to wind on.";
        if (a.location != CraftLocation.Air || b.location != CraftLocation.Air)
            return "Take them out first.";
        if (!SameFibre(a, b))
            return "Only yarn of the same fibre is wound together.";
        if (a.yarn.plies != b.yarn.plies)
            return a.yarn.plies > b.yarn.plies ? "That bobbin is plied less: ply it alike first." : "That bobbin is plied more: wind singles with singles.";
        return null;
    }

    /// <summary>
    /// Winds bobbin <paramref name="from"/> onto bobbin <paramref name="onto"/> end to end, at the loom or the wheel (see
    /// <see cref="CraftFibre.Join"/>): for a length of cloth longer than one bobbin's yarn weaves.
    /// </summary>
    public bool JoinYarn(string onto, string from, out string reason)
    {
        string problem = JoinProblem(onto, from);
        if (problem != null)
            return Refuse(problem, out reason, onto);
        reason = "";
        Record(new CraftOperation { kind = CraftOpKind.JoinYarn, piece = onto, other = from });
        return true;
    }

    /// <summary>
    /// Weaves a yarn into cloth on the loom (see <see cref="CraftFibre.Weave"/>): <paramref name="beats"/> = how true
    /// each row's beat was (0-1). Returns the cloth's id.
    /// </summary>
    public string WeaveCloth(string id, int width, double tension, double density, IList<float> beats, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.Loom))
        {
            Refuse("There is no loom here.", out reason, id);
            return null;
        }
        if (p == null || p.kind != CraftPieceKind.Yarn)
        {
            Refuse("Weaving needs a bobbin of yarn.", out reason, id);
            return null;
        }
        if (p.location != CraftLocation.Air)
        {
            Refuse("Take it out first.", out reason, id);
            return null;
        }
        if (CraftFibre.ClothOf(Rules, p) == null)
        {
            Refuse("Nothing is woven from that yarn.", out reason, id);
            return null;
        }
        if (beats == null || beats.Count == 0)
        {
            // a row is woven with a beat of the reed: as many rows as were beaten, never more
            Refuse("Beat the rows in first.", out reason, id);
            return null;
        }
        width = Math.Max(CraftFibre.MinWidth, Math.Min(CraftFibre.MaxWidth, width));
        if (CraftFibre.RowsFor(p.yarn.length, width) < 1)
        {
            Refuse($"There is too little yarn for a row {width} wide ({CraftFibre.MetresPerCell * width:0} m a row; the bobbin has {p.yarn.length:0} m): narrow the warp or wind another bobbin on.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.WeaveCloth, piece = id, i0 = width, a = (float)CraftMath.Clamp01(tension), b = (float)CraftMath.Clamp01(density) };
        for (int i = 0; i < beats.Count && i < CraftFibre.MaxRows; i++) op.points.Add((float)CraftMath.Clamp01(beats[i]));
        Record(op);
        return op.other;
    }

    /// <summary>Winds a yarn into a skein on the niddy-noddy and counts it (<paramref name="countedMetres"/>).</summary>
    public bool MeasureYarn(string id, double countedMetres, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (!Has(CraftTools.SpinningWheel) && !Has(CraftTools.TailorTable))
            return Refuse("There is no niddy-noddy here.", out reason, id);
        if (p == null || p.kind != CraftPieceKind.Yarn)
            return Refuse("That is not yarn.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.MeasureYarn, piece = id, a = (float)Math.Max(0, Math.Min(100000, countedMetres)) });
        return true;
    }
}
