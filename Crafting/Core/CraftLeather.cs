using System;
using System.Collections.Generic;

/// <summary>
/// Hides and leather, cell by cell.
/// <list type="bullet">
/// <item><b>Raw hides</b> vary by animal (size, thickness along the back and at the belly, fibre, hair, fat, flaws - the
/// scars a hide is born with are kept apart from the knife's work, <see cref="HideCell.scar"/>) and rot when left raw
/// and wet - in the air or in plain water; lime, a tanning liquor and a pickle keep it (<see cref="Preserved"/>).</item>
/// <item><b>Scraping</b> at the beam: on the flesh side it takes flesh and fat off where the knife goes - pressed harder
/// than a clean or thin cell can bear (<see cref="SafeFleshingPressure"/>: a steep knife bites, a flat one is gentle but
/// takes less), it scores that cell; on the grain side it takes off hair loosened by lime (and scuffs the grain when
/// pressed hard, or where hair the lime has not loosened holds). Leather tanned with its hair on (<see cref="HairLeft"/>)
/// is worth less: a hairy grain takes neither the slicker nor the dye.</item>
/// <item><b>Vats</b>: water soaks (and washes out alum, and hardens tanned leather when hot); lime loosens the hair
/// (warmth speeds it only up to about 30 °C; too long and the grain goes spongy); tanning liquors tan the outside first
/// and the core over time - slower in a thick hide, where flesh was left, in the cold; a liquor too strong on a raw hide
/// closes its outside and leaves the core raw (case hardening). In any liquor a wet hide heated past its shrink
/// temperature shrinks fast, and a raw one cooks. The liquor is used up as it tans; soaked again, dried leather is wet
/// again.</item>
/// <item><b>Drying</b> (rack, frame, room) is faster when warm, moving and dry; leather stiffens as it dries through the
/// damp stage unless it is staked then (between <see cref="StakeWet"/> and <see cref="StakeDry"/>); unstretched - on
/// the rack, or laced slack - it puckers as it shrinks; laced in the frame with each side pulled to at least
/// <see cref="FlatFrom"/> of what its edge can take (<see cref="EdgeLimit"/>) it dries flat - only as flat as its
/// slackest side holds it (<see cref="Flatness"/>); pulled unevenly it dries out of square (<see cref="Unevenness"/>);
/// overdrawn (past <see cref="OverdrawnFrom"/>) its edge thins as it dries, and past what it can take it tears.</item>
/// <item><b>Finishing</b>: staking softens; oils make it supple and shed water (and block dye: dye first); wax sheds
/// water and stiffens; dye goes where it is brushed (and gives the item made of it its colour, <see cref="Colour"/>);
/// slicking smooths and seals the grain (and burnishes a cut part's edges where it runs along them); skiving thins (and
/// holes it if it goes too far). Oil, wax and dye are used up by what the leather takes in (<see cref="Uptake"/>).</item>
/// <item><b>Cutting</b> a pattern takes those cells - flaws, thin spots and the knife's slips there - into a part; a
/// careless cut leaves ragged edges, one along a chalked line cleaner ones (<see cref="ChalkedCut"/>). A part is not cut
/// again.</item>
/// </list>
/// </summary>
public static class CraftLeather
{
    // ------------------------------------------------------------------ the rules the windows show
    /// <summary>How far each tool works round the line it is drawn along (cells): the knives, the slicker and the dye brush, the staking blade, the oil rag.</summary>
    public const double ScrapeReach = 0.7, SkiveReach = 0.7, SlickReach = 0.8, DyeReach = 0.8, StakeReach = 0.9, OilReach = 1.0;
    /// <summary>Staking: wetter than this the leather is too wet to work (it stiffens again as it dries)...</summary>
    public const double StakeWet = 0.55;
    /// <summary>...and at or below this it is too dry (staked dry, it hardly softens, and bark-tanned grain cracks).</summary>
    public const double StakeDry = 0.15;
    /// <summary>A hide is dry once every cell is at or below this moisture.</summary>
    public const double DryAt = 0.16;
    /// <summary>The frame: a side pulled to at least this share of what its edge can take dries flat (less: it puckers)...</summary>
    public const double FlatFrom = 0.5;
    /// <summary>...past this share the edge is overdrawn and thins as it dries; past the whole of it, it tears.</summary>
    public const double OverdrawnFrom = 0.85;
    /// <summary>Opposite sides pulled unevenly by more than this (N-S plus E-W, in shares of their edges' limits) dry out of square.</summary>
    public const double UnevenFrom = 0.3;
    /// <summary>What a side with no edge left can take (nothing there to tear).</summary>
    public const double NoEdge = 9;
    /// <summary>The drying rack hangs this many hides side by side (the window draws each in its place).</summary>
    public const int RackHides = 4;
    /// <summary>A raw cell rotting past this spoils: its hair slips, its grain and its skin go (the window's "rotting!").</summary>
    public const double SpoilsFrom = 0.4;
    /// <summary>A raw cell rotting past this starts to smell (the window warns before it spoils).</summary>
    public const double SmellsFrom = 0.25;
    /// <summary>Lime, a tanning liquor or a pickle at least this strong keeps a raw hide from rotting (<see cref="Preserved"/>).</summary>
    public const double PreservesFrom = 0.05;
    /// <summary>A hide's hair is off once its mean hair is under this: the tannery's path, the guide and the evaluator alike.</summary>
    public const double UnhairedBelow = 0.1;
    /// <summary>A cell with at least this much hair has hair for the hair knife to drag out (less is bare grain).</summary>
    public const double HairyFrom = 0.05;
    /// <summary>
    /// The most one straight pass of the skiving knife takes off a cell, per unit of its depth: the cells the line runs
    /// through get about this much of the stroke (going back over a place in the same stroke cuts up to twice as deep).
    /// </summary>
    public const double SkivePass = 1.1;
    /// <summary>A cell skived thinner than this (mm) is skived through: it holes.</summary>
    public const double SkivedThrough = 0.6;
    /// <summary>A cut made along the pattern chalked at the very place comes out this much cleaner (the line was laid out first).</summary>
    public const double ChalkedCut = 0.05;
    /// <summary>A part cut cleaner than this has clean edges; down to <see cref="RaggedCut"/> they are a little ragged; under it, ragged.</summary>
    public const double CleanCut = 0.85, RaggedCut = 0.6;

    /// <summary>How thick a cell is now (mm): shrinking thickens it a little as it draws it in (what calipers read and the evaluator uses).</summary>
    public static double CellThickness(HideCell c) => c.thickness * (1 - c.shrink * 0.3) * (1 + c.shrink * 0.5);

    /// <summary>The mean thickness (mm) of what is left of a hide or a piece of cloth.</summary>
    public static double MeanThickness(CraftRules rules, CraftPiece p)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in p.hide.cells)
            if (c.present)
            {
                t += c.thickness;
                n++;
            }
        return n > 0 ? t / n : 0;
    }

    /// <summary>The pink of flesh and fat left on a hide.</summary>
    public static readonly UnityEngine.Color FleshColour = new UnityEngine.Color(0.86f, 0.62f, 0.56f, 1f);

    /// <summary>
    /// The colour of one cell as the leather or cloth itself is coloured on its grain side: a hide's skin browning to its
    /// tanning agent's colour as it tans, the dye brushed on, the hair over it - cloth its own colour, dyed. With
    /// <paramref name="fleshShows"/> the flesh and fat left under it show pink through a raw hide, as the windows draw it
    /// (they draw the wet, the slicked sheen, the damage and the rot over it too); an item is the leather's own colour.
    /// </summary>
    public static UnityEngine.Color GrainColour(CraftRules rules, HideState h, HideCell cell, bool fleshShows = false)
    {
        if (cell == null || !cell.present)
            return UnityEngine.Color.clear;
        CraftHideDefinition d = rules?.Hide(h.hide);
        UnityEngine.Color skin = d != null ? d.color : new UnityEngine.Color(0.62f, 0.5f, 0.42f);
        UnityEngine.Color dyed = new UnityEngine.Color((float)cell.dyeR, (float)cell.dyeG, (float)cell.dyeB);
        if (d != null && d.textile)
        {
            UnityEngine.Color cloth = cell.dye > 0.01 ? UnityEngine.Color.Lerp(skin, dyed, (float)CraftMath.Clamp01(cell.dye)) : skin;
            cloth.a = 1f;
            return cloth;
        }
        CraftTanningAgent a = rules?.Agent(h.agent);
        UnityEngine.Color tan = a != null ? a.color : new UnityEngine.Color(0.55f, 0.36f, 0.22f);
        UnityEngine.Color c = UnityEngine.Color.Lerp(skin, tan, (float)CraftMath.Clamp01(cell.Tannage * 1.2));
        if (fleshShows)
            c = UnityEngine.Color.Lerp(c, FleshColour, (float)CraftMath.Clamp01(cell.flesh * 0.8));
        if (cell.dye > 0.01)
            c = UnityEngine.Color.Lerp(c, dyed, (float)CraftMath.Clamp01(cell.dye));
        c = UnityEngine.Color.Lerp(c, d != null ? d.hairColor : new UnityEngine.Color(0.35f, 0.25f, 0.18f), (float)CraftMath.Clamp01(cell.hair * 0.9));
        c.a = 1f;
        return c;
    }

    /// <summary>
    /// The colour of a hide, a cut part or a piece of cloth on the whole (the mean of its cells' <see cref="GrainColour"/>):
    /// tanned, it is the tan's; dyed, the dye's as far as it took; with its hair on, the hair's. An item made of it is this colour.
    /// </summary>
    public static UnityEngine.Color Colour(CraftRules rules, HideState h)
    {
        float r = 0, g = 0, b = 0;
        int n = 0;
        if (h != null)
            foreach (HideCell c in h.cells)
            {
                if (c == null || !c.present) continue;
                UnityEngine.Color k = GrainColour(rules, h, c);
                r += k.r; g += k.g; b += k.b;
                n++;
            }
        return n > 0 ? new UnityEngine.Color(r / n, g / n, b / n, 1f) : new UnityEngine.Color(0.6f, 0.45f, 0.3f, 1f);
    }

    /// <summary>A fresh hide of an animal (the same seed always gives the same hide).</summary>
    public static CraftPiece NewHide(CraftRules rules, string hideId, int seed)
    {
        CraftHideDefinition d = rules.Hide(hideId);
        if (d == null)
            return null;
        var p = new CraftPiece { kind = CraftPieceKind.Hide, origin = d.textile ? $"a bolt of {d.Name.ToLowerInvariant()}" : $"a raw {d.Name.ToLowerInvariant()}" };
        HideState h = p.hide;
        h.hide = d.id;
        h.seed = seed;
        h.width = d.width;
        h.height = d.height;
        h.shrinkTemperature = 60;
        double cx = (d.width - 1) * 0.5, cy = (d.height - 1) * 0.5;
        for (int y = 0; y < d.height; y++)
            for (int x = 0; x < d.width; x++)
            {
                int k = y * d.width + x;
                double ex = (x - cx) / Math.Max(0.5, cx + 0.5), ey = (y - cy) / Math.Max(0.5, cy + 0.5);
                double outline = ex * ex + ey * ey;
                // A bolt of cloth is a straight-edged rectangle; a hide has the animal's outline.
                bool present = d.textile || outline <= 1.05 + 0.2 * (CraftMath.Noise(seed, k, 1) - 0.5) || d.width * d.height <= 6;
                double back = 1 - Math.Abs(y - cy) / Math.Max(0.5, cy + 0.5);
                double thick = CraftMath.Lerp(d.thicknessBelly, d.thicknessBack, back) * (0.9 + 0.2 * CraftMath.Noise(seed, k, 2));
                if (outline > 0.75) thick *= 0.85; // legs, neck and flanks are thinner
                var c = new HideCell
                {
                    present = present, thickness = thick, flesh = CraftMath.Clamp01(d.fat * (0.7 + 0.4 * CraftMath.Noise(seed, k, 3))), hair = d.hair,
                    moisture = 0.75, grain = 1,
                };
                if (d.textile)
                {
                    // Woven and finished: nothing to flesh or tan, dry and supple; a slub or a pulled thread here and there.
                    c.thickness = CraftMath.Lerp(d.thicknessBelly, d.thicknessBack, 0.5) * (0.95 + 0.1 * CraftMath.Noise(seed, k, 2));
                    c.flesh = c.hair = 0;
                    c.tanS = c.tanC = 1;
                    c.moisture = 0.08;
                    c.stiff = 1 - d.suppleness;
                    c.smooth = 0.6;
                }
                if (CraftMath.Noise(seed, k, 4) < d.flaws)
                {
                    c.flaw = true;
                    c.damage = (d.textile ? 0.15 : 0.25) + 0.55 * CraftMath.Noise(seed, k, 5);
                    c.scar = c.damage; // what it was born with, not what the knife did
                }
                h.cells.Add(c);
            }
        if (d.textile)
            h.dried = true;
        return p;
    }

    private static double Hours(CraftWorkshop ws, double dt) => dt * ws.Rules.Settings.processHoursPerSecond;

    // ------------------------------------------------------------------ every step
    public static void StepHide(CraftWorkshop ws, CraftPiece p, double dt)
    {
        CraftRules rules = ws.Rules;
        HideState h = p.hide;
        double hours = Hours(ws, dt);
        if (p.location == CraftLocation.Assembled)
            return;
        double tannage = MeanTannage(h);
        bool raw = tannage < 0.3;
        if (raw)
            h.hoursRaw += hours;
        CraftVatState vat = VatOf(ws, p);
        if (vat != null)
            InVat(ws, p, vat, hours);
        else
            Drying(ws, p, hours);
        // Rot: raw, wet and warm - in the air or in plain water; lime, a tanning liquor or a pickle keeps it.
        bool preserved = Preserved(ws, p);
        double warm = RotWarmth(vat != null ? vat.temperature : ws.Ambient);
        bool spoiling = false;
        for (int i = 0; i < h.cells.Count; i++)
        {
            HideCell c = h.cells[i];
            if (!Rots(c, preserved)) continue;
            c.rot += hours / rules.Settings.rotHours * warm * (0.5 + c.flesh);
            if (c.rot > SpoilsFrom)
            {
                c.hair = Math.Max(0, c.hair - hours * 0.05);
                c.grain = Math.Max(0, c.grain - hours * 0.01);
                c.damage = Math.Min(1, c.damage + hours * 0.004);
                spoiling = true;
            }
        }
        // as rotten as it is (told again while it lasts, never stacked on itself)
        if (spoiling && ws.Step % 50 == 0)
            ws.Flaw(p, CraftDefectKind.Rot, -1, MeanRot(h), true, "left raw and wet too long", "The hide has begun to rot: it stinks, and the hair slips.", true);
    }

    /// <summary>The vat a piece soaks in (null when it is not in one).</summary>
    private static CraftVatState VatOf(CraftWorkshop ws, CraftPiece p) =>
        p != null && p.location == CraftLocation.Vat && p.vat >= 0 && p.vat < ws.Vats.Count ? ws.Vats[p.vat] : null;

    /// <summary>
    /// Is the hide soaking in a liquor that keeps it from rotting: lime, a tanning liquor or an acid pickle at least
    /// <see cref="PreservesFrom"/> strong? Plain water, a liquor spent below that and the air do not keep a raw hide.
    /// </summary>
    public static bool Preserved(CraftWorkshop ws, CraftPiece p)
    {
        CraftVatState vat = VatOf(ws, p);
        CraftTanningAgent a = vat != null && vat.volume > 0.01 ? ws.Rules.Agent(vat.agent) : null;
        return a != null && a.kind != CraftAgentKind.Water && vat.strength >= PreservesFrom;
    }

    /// <summary>How much faster a raw hide rots at <paramref name="celsius"/> than at 20 °C: twice as fast every 10 °C, up to about 40 °C (hotter, it cooks rather than rots).</summary>
    public static double RotWarmth(double celsius) => Math.Pow(2, (Math.Min(40, celsius) - 20) / 10);

    /// <summary>Does this cell rot now: there, raw (tanned no more than 0.3), wet (from 0.35) and not kept by the liquor it soaks in?</summary>
    public static bool Rots(HideCell c, bool preserved) => c != null && c.present && !preserved && c.Tannage <= 0.3 && c.moisture >= 0.35;

    /// <summary>
    /// How far the hide's rot has gone where it rots now (the most of its rotting cells; 0 when none rots - tanned, dry,
    /// or kept in lime or a liquor), and whether a cell spoils (past <see cref="SpoilsFrom"/>): what the window warns of.
    /// </summary>
    public static double Rotting(CraftWorkshop ws, CraftPiece p, out bool spoiling)
    {
        spoiling = false;
        if (ws == null || p == null || p.kind != CraftPieceKind.Hide || p.location == CraftLocation.Assembled)
            return 0;
        bool preserved = Preserved(ws, p);
        double most = 0;
        foreach (HideCell c in p.hide.cells)
        {
            if (!Rots(c, preserved)) continue;
            most = Math.Max(most, c.rot);
            spoiling |= c.rot > SpoilsFrom;
        }
        return most;
    }

    public static double MeanTannage(HideState h)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { t += c.Tannage; n++; }
        return n > 0 ? t / n : 0;
    }

    private static double MeanRot(HideState h)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { t += c.rot; n++; }
        return n > 0 ? t / n : 0;
    }

    /// <summary>
    /// How much faster lime loosens the hair at <paramref name="celsius"/> than at 20 °C: twice as fast every 10 °C, but
    /// no faster than twice (about 30 °C) and no slower than a quarter - heat past that only cooks a raw hide.
    /// </summary>
    public static double LimeWarmth(double celsius) => CraftMath.Clamp(Math.Pow(2, (celsius - 20) / 10), 0.25, 2);

    /// <summary>Hot water hardens tanned leather from this temperature (°C) up to its shrink temperature (cuir bouilli).</summary>
    public const double HardensFrom = 55;

    /// <summary>
    /// A liquor past its agent's case-hardening strength closes the outside of a cell whose core is tanned less than this
    /// (and slows it no more once the core is past it: then a strong liquor only tans it faster).
    /// </summary>
    public const double CaseHardensBelow = 0.4;

    private static void InVat(CraftWorkshop ws, CraftPiece p, CraftVatState vat, double hours)
    {
        CraftRules rules = ws.Rules;
        HideState h = p.hide;
        CraftTanningAgent agent = rules.Agent(vat.agent);
        double temp = vat.temperature;
        double absorbed = 0;
        bool closing = false, shrinking = false, hardening = false, wet = false, washing = false;
        foreach (HideCell c in h.cells)
        {
            if (!c.present) continue;
            c.moisture = Math.Min(1, c.moisture + hours * 2);
            wet |= c.moisture > DryAt;
            if (agent != null)
                switch (agent.kind)
                {
                    case CraftAgentKind.Water:
                    {
                        // Alum washes out; hot water hardens tanned leather.
                        CraftTanningAgent tan = rules.Agent(h.agent);
                        if (tan != null && tan.fixation < 0.99 && c.Tannage > 0)
                        {
                            double loss = (1 - tan.fixation) * hours * 0.05;
                            c.tanS *= 1 - loss;
                            c.tanC *= 1 - loss;
                            washing |= tan.kind == CraftAgentKind.Mineral && c.Tannage > 0.1;
                        }
                        if (temp > HardensFrom && c.Tannage > 0.5)
                        {
                            c.hard = Math.Min(1, c.hard + hours * 0.5 * (temp - HardensFrom) / 30);
                            c.stiff = Math.Max(c.stiff, c.hard);
                            c.shrink = Math.Min(0.4, c.shrink + hours * 0.08 * (temp - HardensFrom) / 30);
                            hardening = true;
                        }
                        break;
                    }
                    case CraftAgentKind.Lime:
                    {
                        double warm = LimeWarmth(temp);
                        c.hairLoose = Math.Min(1, c.hairLoose + hours / Math.Max(0.1, agent.actionHours) * warm * (0.5 + vat.strength));
                        c.rot = Math.Max(0, c.rot - hours * 0.02);
                        if (c.hairLoose >= 1)
                            c.grain = Math.Max(0, c.grain - hours * 0.003 * warm); // over-limed: the grain goes spongy
                        break;
                    }
                    case CraftAgentKind.Acid:
                        c.damage = Math.Min(1, c.damage + hours * 0.01 * vat.strength);
                        break;
                    default:
                    {
                        double conc = CraftMath.Clamp01(vat.strength);
                        double warm = CraftMath.Clamp(Math.Pow(2, (temp - agent.bestTemperature) / 10), 0.1, 2);
                        double flesh = 1 - 0.8 * CraftMath.Clamp01(c.flesh);
                        double kS = 2.3 * 6 / agent.hoursThrough, kC = 2.3 / agent.hoursThrough * Math.Pow(4 / Math.Max(0.5, c.thickness), 2);
                        double s0 = c.tanS, c0 = c.tanC;
                        c.tanS += kS * conc * warm * flesh * c.open * (1 - c.tanS) * hours;
                        c.tanC += kC * Math.Max(0, c.tanS - c.tanC) * c.open * warm * (0.6 + 0.4 * vat.stirred) * hours;
                        c.tanS = Math.Min(1, c.tanS);
                        c.tanC = Math.Min(1, c.tanC);
                        if (conc > agent.caseHardeningAbove && c.tanC < CaseHardensBelow)
                        {
                            c.open = Math.Max(0.05, c.open - (conc - agent.caseHardeningAbove) * hours * 0.15);
                            closing |= c.open < 0.5;
                        }
                        if (agent.kind == CraftAgentKind.Oil)
                            c.oil = Math.Min(1, c.oil + hours * 0.01 * conc);
                        if (temp > agent.maxTemperature && c.Tannage < 0.6)
                        {
                            c.damage = Math.Min(1, c.damage + hours * 0.05 * (temp - agent.maxTemperature) / 10);
                            c.shrink = Math.Min(0.5, c.shrink + hours * 0.05 * (temp - agent.maxTemperature) / 10);
                            shrinking = true;
                        }
                        absorbed += c.tanS - s0 + c.tanC - c0;
                        break;
                    }
                }
            // In any liquor, a wet hide heated past its shrink temperature shrinks fast - and a raw one cooks.
            if (temp > h.shrinkTemperature)
            {
                c.shrink = Math.Min(0.6, c.shrink + hours * 0.4 * (temp - h.shrinkTemperature) / 10);
                if (c.Tannage < 0.5) c.damage = Math.Min(1, c.damage + hours * 0.3 * (temp - h.shrinkTemperature) / 10);
                shrinking = true;
            }
        }
        if (agent != null && absorbed > 0)
        {
            vat.strength = Math.Max(0, vat.strength - absorbed * 0.0005 * 60 / Math.Max(10, vat.volume));
            double tannage = MeanTannage(h);
            if (string.IsNullOrEmpty(h.agent) || tannage > 0.1 && absorbed > 1e-4)
            {
                CraftTanningAgent was = rules.Agent(h.agent);
                if (was == null || was.kind == CraftAgentKind.Water || was.kind == CraftAgentKind.Lime || was.id == agent.id || absorbed > 0.01)
                    h.agent = agent.id;
            }
            h.shrinkTemperature = CraftMath.Lerp(60, agent.shrinkTemperature, tannage);
            if (agent.fireResistance > 0)
                h.fireResistance = Math.Max(h.fireResistance, agent.fireResistance * tannage);
        }
        // Soaked again, dried leather is wet again: it dries (and is recorded drying) once more.
        if (wet)
            h.dried = false;
        // As bad as the hide is now, told again while it lasts (never stacked on itself): the share of it whose outside
        // closed, how far it shrank.
        if (closing && ws.Step % 50 == 0)
            ws.Flaw(p, CraftDefectKind.CaseHardened, -1, ClosedShare(h), false, "tanned too strong too soon: the outside closed and the core stayed raw",
                "Its outside darkens fast in the strong liquor, and closes over the raw core.", true);
        if (shrinking && ws.Step % 50 == 0)
            ws.Flaw(p, CraftDefectKind.Shrunk, -1, MeanShrink(h), true, "too hot while wet: it shrank", "It shrinks and puckers in the heat.", true);
        // alum-tawed leather soaking in water gives its tawing up to it (said to the journal now and then while it does)
        if (washing && ws.Step % 200 == 0)
            ws.Raise(CraftEventKind.Note, p.id, "alum", "");
        if (hardening && ws.Step % 200 == 0)
            p.Record(ws.Elapsed, CraftOpKind.SetVatHeat, $"Hardened in hot water at {CraftText.Celsius(temp)}");
    }

    private static double MeanShrink(HideState h)
    {
        double t = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { t += c.shrink; n++; }
        return n > 0 ? t / n : 0;
    }

    /// <summary>The share of the hide whose outside a too strong liquor has closed (its cells less than half open).</summary>
    private static double ClosedShare(HideState h)
    {
        int closed = 0, n = 0;
        foreach (HideCell c in h.cells)
            if (c.present)
            {
                n++;
                if (c.open < 0.5) closed++;
            }
        return n > 0 ? (double)closed / n : 0;
    }

    /// <summary>The tag a hide carries for how much it puckered as it dried unstretched (the mean drying shrink, 0-0.3).</summary>
    public const string PuckeredTag = "puckered";
    /// <summary>The tag a hide carries for how far out of square it dried in an unevenly pulled frame (0-1).</summary>
    public const string DistortionTag = "distortion";

    private static void Drying(CraftWorkshop ws, CraftPiece p, double hours)
    {
        CraftRules rules = ws.Rules;
        HideState h = p.hide;
        // The air's humidity as wood and fibre read it (0.5 where the station has no weather); the drying hours are
        // those of still room air at that ordinary humidity - damper air dries slower, drier air faster.
        double humidity = ws.Humidity;
        double warm = 1 + Math.Max(0, ws.Ambient - 15) / 15;
        double air = p.location == CraftLocation.Rack ? 1.3 : p.location == CraftLocation.Frame ? 1.2 : 0.8;
        double rate = warm * air * (1.25 - humidity) / 0.75 / rules.Settings.dryingHours;
        bool framed = p.location == CraftLocation.Frame;
        CraftTanningAgent agent = rules.Agent(h.agent);
        double board = agent != null ? agent.stiffness + 0.35 : 0.95;
        bool wasDry = h.dried;
        bool allDry = true;
        double tension = (h.tensionN + h.tensionE + h.tensionS + h.tensionW) * 0.25;
        // In the frame: each side's pull as a share of what its edge can take. Taut all round it dries flat (no pucker) -
        // a slack side lets it pucker however hard the others pull; pulled unevenly it dries askew, overdrawn its edge thins.
        double[] pull = framed ? Pulls(rules, h) : null;
        int[] edges = framed ? EdgeMask(h) : null;
        double flat = framed ? Flatness(pull) : 0, uneven = framed ? Unevenness(pull) : 0;
        double damp = 0, puckered = 0;
        int n = 0;
        for (int i = 0; i < h.cells.Count; i++)
        {
            HideCell c = h.cells[i];
            if (!c.present) continue;
            n++;
            double before = c.moisture;
            if (c.moisture > 0.08)
                c.moisture = Math.Max(0.08, c.moisture - rate * hours * (4 / Math.Max(1, c.thickness)));
            double lost = before - c.moisture;
            if (lost > 0 && before < StakeWet)
            {
                damp += lost;
                // Drying through the damp stage stiffens it (unless it is staked), shrinks it unless stretched.
                c.stiff = Math.Min(Math.Max(board, c.hard), c.stiff + lost * 1.6 * (c.Tannage < 0.2 ? 1.5 : 1) * (1 - 0.6 * c.oil));
                if (framed)
                    c.stretch = Math.Max(c.stretch, tension * 0.15);
                double shrink = lost * 0.12 * (1 - c.oil) * (1 - flat);
                if (shrink > 0 && c.shrink < 0.3)
                {
                    double was = c.shrink;
                    c.shrink = Math.Min(0.3, c.shrink + shrink);
                    puckered += c.shrink - was;
                }
                if (framed && edges[i] != 0)
                    for (int k = 0; k < 4; k++)
                        if ((edges[i] & (1 << k)) != 0 && pull[k] > OverdrawnFrom)
                            c.thickness = Math.Max(0.3, c.thickness * (1 - lost * 0.25 * CraftMath.Clamp01((pull[k] - OverdrawnFrom) / (1 - OverdrawnFrom))));
                if (warm > 2 && c.Tannage > 0.3 && (agent?.kind ?? CraftAgentKind.Vegetable) == CraftAgentKind.Vegetable)
                    c.grain = Math.Max(0.3, c.grain - lost * 0.05); // dried too fast and hot: the grain cracks
            }
            if (c.moisture > DryAt) allDry = false;
        }
        if (n > 0 && puckered > 0)
            p.SetTag(PuckeredTag, Math.Min(0.3, p.Tag(PuckeredTag) + puckered / n));
        if (n > 0 && framed && damp > 0 && uneven > UnevenFrom)
            p.SetTag(DistortionTag, Math.Min(1, p.Tag(DistortionTag) + damp / n / (StakeWet - 0.08) * (uneven - UnevenFrom)));
        if (allDry && !wasDry && MeanTannage(h) > 0.3)
        {
            h.dried = true;
            // dried through its damp stage unworked it is stiff (not leather hardened on purpose in hot water): id "stiff"
            bool stiff = DriedStiff(h);
            ws.Raise(CraftEventKind.Dried, p.id, stiff ? "stiff" : "", stiff ? "It has dried stiff." : "It has dried.");
            p.Record(ws.Elapsed, CraftOpKind.Stretch, framed ? "Dried in the frame" : p.location == CraftLocation.Rack ? "Dried on the rack" : "Dried");
        }
    }

    /// <summary>
    /// Leather this stiff on average when it dries (and not hardened on purpose, in hot water) dried stiff: left to dry
    /// on its own through its damp stage instead of being staked (tanned leather dries about 0.75 unstaked, softer staked).
    /// </summary>
    public const double DriesStiffFrom = 0.6;

    /// <summary>Did the hide dry stiff (<see cref="DriesStiffFrom"/>) - not hard on purpose (cuir bouilli)?</summary>
    public static bool DriedStiff(HideState h)
    {
        double stiff = 0, hard = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { stiff += c.stiff; hard += c.hard; n++; }
        return n > 0 && stiff / n >= DriesStiffFrom && hard / n < 0.5;
    }

    public static void OnEnterVat(CraftWorkshop ws, CraftPiece p)
    {
        if (p.kind != CraftPieceKind.Hide || p.vat < 0 || p.vat >= ws.Vats.Count)
            return;
        CraftTanningAgent a = ws.Rules.Agent(ws.Vats[p.vat].agent);
        if (a != null)
            p.Record(ws.Elapsed, CraftOpKind.MovePiece, $"Put in {a.Name.ToLowerInvariant()} ({ws.Vats[p.vat].strength * 100:0}% strength, {CraftText.Celsius(ws.Vats[p.vat].temperature)})");
    }

    /// <summary>A hide laced into the frame starts slack: its four sides are pulled from nothing.</summary>
    public static void OnLaced(CraftWorkshop ws, CraftPiece p)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        HideState h = p.hide;
        h.tensionN = h.tensionE = h.tensionS = h.tensionW = 0;
    }

    // ------------------------------------------------------------------ strokes over the cells
    /// <summary>Calls <paramref name="touch"/>(cell, weight) for the cells under a stroke (points: x, y pairs in cells) of <paramref name="radius"/> cells.</summary>
    public static void Stroke(HideState h, List<float> points, double radius, Action<HideCell, double> touch) =>
        StrokeCells(h, points, radius, (i, c, w) => touch(c, w));

    /// <summary>
    /// Calls <paramref name="touch"/>(index, cell, weight) once for each present cell under a stroke (points: x, y pairs in
    /// cells, row 0 at the top) of <paramref name="radius"/> cells; the index is y × width + x. The windows run it on a
    /// stroke they send to show the cells it works, exactly as the workshop works them.
    /// </summary>
    public static void StrokeCells(HideState h, IList<float> points, double radius, Action<int, HideCell, double> touch)
    {
        if (h == null || points == null || points.Count < 2 || h.cells.Count < h.width * h.height)
            return;
        var weight = new double[h.cells.Count];
        void Sample(double x, double y, double w)
        {
            int x0 = (int)Math.Floor(x - radius), x1 = (int)Math.Ceiling(x + radius);
            int y0 = (int)Math.Floor(y - radius), y1 = (int)Math.Ceiling(y + radius);
            for (int cy = Math.Max(0, y0); cy <= Math.Min(h.height - 1, y1); cy++)
                for (int cx = Math.Max(0, x0); cx <= Math.Min(h.width - 1, x1); cx++)
                {
                    double dx = cx + 0.5 - x, dy = cy + 0.5 - y;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d > radius) continue;
                    weight[cy * h.width + cx] += w * (1 - d / radius * 0.5);
                }
        }
        if (points.Count == 2)
            Sample(points[0], points[1], 1);
        for (int k = 2; k + 1 < points.Count; k += 2)
        {
            double ax = points[k - 2], ay = points[k - 1], bx = points[k], by = points[k + 1];
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int n = Math.Max(1, (int)Math.Ceiling(len / 0.25));
            for (int s = 0; s < n; s++)
            {
                double t = (s + 0.5) / n;
                Sample(ax + (bx - ax) * t, ay + (by - ay) * t, len / n);
            }
        }
        for (int i = 0; i < weight.Length; i++)
            if (weight[i] > 1e-6 && h.cells[i].present)
                touch(i, h.cells[i], Math.Min(2, weight[i]));
    }

    /// <summary>
    /// The most a fleshing knife held at <paramref name="angle"/> (0 flat - 1 steep) can bear on cell <paramref name="c"/>
    /// without scoring it where it is clean or thin (<see cref="Scorable"/>): thicker skin takes more, a steep knife bites
    /// sooner, a flat one is gentler (and takes less flesh).
    /// </summary>
    public static double SafeFleshingPressure(HideCell c, double angle) =>
        0.55 * c.thickness / 3 + 0.175 - 0.3 * (CraftMath.Clamp01(angle) - 0.7);

    /// <summary>Can the fleshing knife score this cell (clean of flesh, or thin)? Thick skin still under flesh and fat takes any pressure.</summary>
    public static bool Scorable(HideCell c) => c.flesh < 0.12 || c.thickness < 2;

    /// <summary>On the grain side the knife scuffs the grain when pressed harder than this (and at any pressure where hair the lime has not loosened holds).</summary>
    public const double ScuffAbove = 0.5;
    /// <summary>Hair loosened less than this does not come away: the knife scuffs the grain instead (where there is hair: <see cref="HairyFrom"/>).</summary>
    public const double LooseFrom = 0.3;

    /// <summary>
    /// Does the hair knife scuff this cell's grain at any pressure: hair there (<see cref="HairyFrom"/>) that the lime has
    /// not loosened (<see cref="LooseFrom"/>) holds, and the knife drags over the grain instead? Bare grain - a drake's, or
    /// a cell already scraped clean - is scuffed only when pressed past <see cref="ScuffAbove"/>.
    /// </summary>
    public static bool HairHolds(HideCell c) => c != null && c.hair > HairyFrom && c.hairLoose < LooseFrom;

    /// <summary>
    /// How much of its hair a hide still has, as the work counts it (0: unhaired, its mean hair under
    /// <see cref="UnhairedBelow"/> - to 1: all it grew): the tannery's path does its Unhair step by 1 minus this, and leather
    /// tanned with its hair on is worth less by it (a hairy grain takes neither the slicker nor the dye, nor finishes).
    /// </summary>
    public static double HairLeft(CraftHideDefinition def, double meanHair) =>
        meanHair < UnhairedBelow ? 0 : CraftMath.Clamp01((meanHair - UnhairedBelow) / (Math.Max(0.15, def?.hair ?? 0.5) - UnhairedBelow));

    /// <summary>How much of its hair a hide (or a part) still has (see <see cref="HairLeft(CraftHideDefinition, double)"/>); cloth has none.</summary>
    public static double HairLeft(CraftRules rules, HideState h)
    {
        double hair = 0;
        int n = 0;
        foreach (HideCell c in h.cells)
            if (c.present) { hair += c.hair; n++; }
        return n > 0 ? HairLeft(rules?.Hide(h.hide), hair / n) : 0;
    }

    public static void Scrape(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        bool grainSide = op.flag;
        double pressure = CraftMath.Clamp01(op.a), angle = CraftMath.Clamp01(op.b);
        StrokeCells(p.hide, op.points, ScrapeReach, (i, c, w) =>
        {
            if (!grainSide)
            {
                double damage0 = c.damage;
                bool scorable = Scorable(c); // as the cell was when the knife came to it
                double excess = pressure - SafeFleshingPressure(c, angle);
                c.flesh = Math.Max(0, c.flesh - pressure * w * 0.35 * (0.6 + 0.4 * angle));
                if (excess > 0 && scorable)
                {
                    c.damage = Math.Min(1, c.damage + excess * w * 0.15);
                    double rise = c.damage - damage0;
                    // the knife's work, not an old scar: recorded on the cell it scored (a part cut there carries it)
                    if (c.damage > 0.25 && damage0 <= 0.25 || rise > 0.03)
                        p.AddDefect(CraftDefectKind.Score, i, rise, true, "the knife scored the hide where it was already clean or thin");
                }
            }
            else
            {
                // hair the lime has not loosened holds, and the knife drags over the grain (as the cell was when it came)
                bool holds = HairHolds(c);
                c.hair = Math.Max(0, c.hair - pressure * w * 0.5 * (0.2 + 0.8 * c.hairLoose));
                c.grain = Math.Max(0, c.grain - Math.Max(0, pressure - ScuffAbove) * w * 0.15 - (holds ? pressure * w * 0.05 : 0));
            }
        });
        p.Record(ws.Elapsed, CraftOpKind.Scrape, grainSide ? "Scraped the hair off" : "Fleshed", pressure);
        ws.Raise(CraftEventKind.Scraped, p.id, grainSide ? "grain" : "flesh", "");
    }

    // ------------------------------------------------------------------ the frame
    public static void Stretch(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        HideState h = p.hide;
        double heldN = h.tensionN, heldE = h.tensionE, heldS = h.tensionS, heldW = h.tensionW;
        h.tensionN = CraftMath.Clamp01(op.a);
        h.tensionE = CraftMath.Clamp01(op.b);
        h.tensionS = CraftMath.Clamp01(op.c);
        h.tensionW = CraftMath.Clamp01(op.d);
        CraftHideDefinition d = ws.Rules.Hide(h.hide);
        double fiber = d != null ? d.fiberStrength : 1;
        // Each side's pull on its edge cells: a cell tears by as much as the pull now goes past both what it can take and
        // what it already held - a side sent again unchanged never tears again. An uneven pull skews it only as it dries.
        int[] edges = EdgeMask(h);
        Edge(h, edges, 0, h.tensionN, heldN, fiber, p);
        Edge(h, edges, 1, h.tensionE, heldE, fiber, p);
        Edge(h, edges, 2, h.tensionS, heldS, fiber, p);
        Edge(h, edges, 3, h.tensionW, heldW, fiber, p);
        double mean = (h.tensionN + h.tensionE + h.tensionS + h.tensionW) * 0.25;
        foreach (HideCell c in h.cells)
            if (c.present && c.moisture > 0.3)
                c.stretch = Math.Max(c.stretch * 0.8, mean * 0.12);
        p.Record(ws.Elapsed, CraftOpKind.Stretch, "Laced and stretched in the frame", mean);
    }

    private static void Edge(HideState h, int[] edges, int side, double tension, double held, double fiber, CraftPiece p)
    {
        for (int i = 0; i < edges.Length; i++)
        {
            if ((edges[i] & (1 << side)) == 0) continue;
            HideCell c = h.cells[i];
            double over = tension - Math.Max(EdgeStrength(fiber, c), held);
            if (over <= 0) continue;
            c.damage = Math.Min(1, c.damage + over * 0.8);
            p.AddDefect(CraftDefectKind.Tear, i, over, true, "it tore where the frame pulled harder than it could take");
        }
    }

    /// <summary>What one edge cell can take from the frame before it tears (as a side's tension, 0-1+).</summary>
    public static double EdgeStrength(double fiber, HideCell c) =>
        fiber * (0.4 + 0.15 * c.thickness) * (1 - 0.8 * c.damage) * (1 - 0.5 * c.rot);

    /// <summary>
    /// The most side <paramref name="side"/> (0 north - the top row, as drawn -, 1 east, 2 south, 3 west) can be pulled
    /// before it tears: what its weakest edge cell can take (<see cref="NoEdge"/> when nothing is left on that side).
    /// A rabbit's rim takes about 0.25, a deer's about 0.45, a cow's about 0.8, a troll's well past 1.
    /// </summary>
    public static double EdgeLimit(CraftRules rules, HideState h, int side)
    {
        if (h == null || h.cells.Count < h.width * h.height)
            return NoEdge;
        double fiber = rules?.Hide(h.hide)?.fiberStrength ?? 1;
        int[] edges = EdgeMask(h);
        double least = NoEdge;
        for (int i = 0; i < edges.Length; i++)
            if ((edges[i] & (1 << side)) != 0)
                least = Math.Min(least, EdgeStrength(fiber, h.cells[i]));
        return least;
    }

    /// <summary>How hard each side (north, east, south, west) is pulled now, as a share of what its edge can take (1 = it tears).</summary>
    public static double[] Pulls(CraftRules rules, HideState h)
    {
        double[] t = { h.tensionN, h.tensionE, h.tensionS, h.tensionW };
        var pull = new double[4];
        for (int k = 0; k < 4; k++)
            pull[k] = Math.Min(2, t[k] / Math.Max(1e-3, EdgeLimit(rules, h, k)));
        return pull;
    }

    /// <summary>
    /// How unevenly a frame pulls (opposite sides' shares of their edges' limits: |N - S| + |E - W|): past
    /// <see cref="UnevenFrom"/> the hide dries out of square. The windows word the frame by the same rule.
    /// </summary>
    public static double Unevenness(double[] pull) => pull == null || pull.Length < 4 ? 0
        : Math.Abs(Math.Min(1.5, pull[0]) - Math.Min(1.5, pull[2])) + Math.Abs(Math.Min(1.5, pull[1]) - Math.Min(1.5, pull[3]));

    /// <summary>How unevenly the frame pulls <paramref name="h"/> now (see <see cref="Unevenness(double[])"/>).</summary>
    public static double Unevenness(CraftRules rules, HideState h) => Unevenness(Pulls(rules, h));

    /// <summary>
    /// How flat a frame holds a hide as it dries (0 - as slack as the rack: it puckers fully - to 1 flat): as far as its
    /// slackest side is pulled into the green (<see cref="FlatFrom"/>) - each side must hold, however hard the others pull.
    /// The windows word the frame by the same rule ("taut and even" is 1).
    /// </summary>
    public static double Flatness(double[] pull)
    {
        if (pull == null || pull.Length < 4)
            return 0;
        double least = 1;
        for (int k = 0; k < 4; k++) least = Math.Min(least, Math.Min(1, pull[k]));
        return CraftMath.Clamp01(least / FlatFrom);
    }

    /// <summary>
    /// Which sides' edges each cell is on (bit 0 north - the first present cell from the top of its column -, bit 1 east,
    /// bit 2 south, bit 3 west): the cells the frame's lacing pulls.
    /// </summary>
    public static int[] EdgeMask(HideState h)
    {
        var mask = new int[h.cells.Count];
        if (h.cells.Count < h.width * h.height)
            return mask;
        for (int x = 0; x < h.width; x++)
        {
            int top = FirstRow(h, x, true), bottom = FirstRow(h, x, false);
            if (top >= 0) mask[top * h.width + x] |= 1;
            if (bottom >= 0) mask[bottom * h.width + x] |= 4;
        }
        for (int y = 0; y < h.height; y++)
        {
            int right = FirstCol(h, y, false), left = FirstCol(h, y, true);
            if (right >= 0) mask[y * h.width + right] |= 2;
            if (left >= 0) mask[y * h.width + left] |= 8;
        }
        return mask;
    }

    private static int FirstRow(HideState h, int x, bool top)
    {
        for (int k = 0; k < h.height; k++)
        {
            int y = top ? k : h.height - 1 - k;
            if (h.Cell(x, y)?.present == true) return y;
        }
        return -1;
    }

    private static int FirstCol(HideState h, int y, bool left)
    {
        for (int k = 0; k < h.width; k++)
        {
            int x = left ? k : h.width - 1 - k;
            if (h.Cell(x, y)?.present == true) return x;
        }
        return -1;
    }

    /// <summary>What marked a cell, by the work (<see cref="WorkMarks"/>): a knife (a score, a skived-through hole), the frame (a tear), or otherwise (rot, a burning pickle, cooking, a ragged cut).</summary>
    public const int ByKnife = 1, ByFrame = 2, ByOther = 3;

    /// <summary>How much of a cell's damage the work did (the scars it was born with apart).</summary>
    public static double Worked(HideCell c) => c == null ? 0 : Math.Max(0, c.damage - Math.Min(c.damage, c.scar));

    /// <summary>
    /// What marked each of the hide's cells, by the work - the scars it was born with apart (0: nothing): a knife's score or
    /// a skived-through hole recorded on the cell (<see cref="ByKnife"/>, first), a tear (<see cref="ByFrame"/>), else
    /// damage the work did there past <paramref name="least"/> (<see cref="ByOther"/>). It all goes into a part cut there.
    /// </summary>
    public static int[] WorkMarks(CraftPiece p, double least = 0)
    {
        HideState h = p.hide;
        var by = new int[h.cells.Count];
        foreach (CraftDefect d in p.defects)
        {
            if (d.at < 0 || d.at >= by.Length) continue;
            if (d.kind == CraftDefectKind.Score || d.kind == CraftDefectKind.Hole) by[d.at] = ByKnife;
            else if (d.kind == CraftDefectKind.Tear && by[d.at] == 0) by[d.at] = ByFrame;
        }
        for (int i = 0; i < by.Length; i++)
            if (by[i] == 0 && h.cells[i].present && Worked(h.cells[i]) > least) by[i] = ByOther;
        return by;
    }

    /// <summary>Is cell <paramref name="i"/> on the part's border (a side of it open to the cut edge)?</summary>
    public static bool OnBorder(HideState h, int i)
    {
        if (h == null || i < 0 || i >= h.cells.Count || h.width <= 0)
            return false;
        int x = i % h.width, y = i / h.width;
        return h.Cell(x - 1, y)?.present != true || h.Cell(x + 1, y)?.present != true || h.Cell(x, y - 1)?.present != true || h.Cell(x, y + 1)?.present != true;
    }

    // ------------------------------------------------------------------ finishing
    /// <summary>How much a stroke of the staking blade softens leather at this moisture: best damp, little wet, little dry.</summary>
    public static double StakeStrength(double moisture) => moisture > StakeWet ? 0.05 : moisture > StakeDry ? 0.25 : 0.08;

    /// <summary>
    /// Does its grain crack under the staking blade where it is dry (at or below <see cref="StakeDry"/>)? Bark-tanned
    /// (vegetable) leather's does: dried stiff, it is oiled supple (<see cref="Condition"/>: oil softens dry leather and does
    /// it no harm); any other leather may be staked dry (it softens slowly).
    /// </summary>
    public static bool CracksStakedDry(CraftRules rules, HideState h) => rules?.Agent(h?.agent)?.kind == CraftAgentKind.Vegetable;

    public static void Stake(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        double pressure = CraftMath.Clamp01(op.a);
        bool veg = CracksStakedDry(ws.Rules, p.hide);
        StrokeCells(p.hide, op.points, StakeReach, (i, c, w) =>
        {
            c.stiff = Math.Max(c.hard * 0.9, c.stiff - pressure * w * StakeStrength(c.moisture));
            if (c.moisture <= StakeDry && veg)
                c.grain = Math.Max(0.2, c.grain - pressure * w * 0.03);
        });
        p.Record(ws.Elapsed, CraftOpKind.Stake, "Staked (worked soft)", pressure);
    }

    /// <summary>How hard an oil or a wax is worked in by a hand pressing <paramref name="a"/> (0-1): a hand presses no harder than fully.</summary>
    public static double ConditionAmount(CraftSupplyDefinition s, double a) => CraftMath.Clamp01(a) * Math.Max(0.1, s?.strength ?? 1);

    /// <summary>What one cell takes up of an oil or a wax (<paramref name="kind"/>) from a stroke of weight <paramref name="w"/>: oil only a third as well where it is wet.</summary>
    public static double ConditionTake(HideCell c, CraftSupplyKind kind, double amount, double w) =>
        kind == CraftSupplyKind.Wax ? Math.Max(0, Math.Min(1 - c.wax, amount * w * 0.5))
        : Math.Max(0, Math.Min(1 - c.oil, amount * w * 0.4 * (c.moisture < 0.6 ? 1 : 0.4)));

    /// <summary>What one cell takes up of a dye brushed at <paramref name="amount"/> with weight <paramref name="w"/>: oil and wax on it repel it, hair keeps it off the grain.</summary>
    public static double DyeTake(HideCell c, double amount, double w) =>
        Math.Max(0, Math.Min(1 - c.dye, amount * w * 0.5 * (1 - 0.8 * c.oil) * (1 - 0.6 * c.wax) * (1 - 0.7 * CraftMath.Clamp01(c.hair))));

    /// <summary>
    /// How much of an oil, a wax or a dye (<paramref name="s"/>) a stroke along <paramref name="points"/> at
    /// <paramref name="a"/> would put into the leather, in cell-loads (one cell taken up fully is 1) - by the very rules
    /// <see cref="Condition"/> and <see cref="Dye"/> apply, so the station can charge for what is taken before it is.
    /// An oil or wax with no path is worked in all over.
    /// </summary>
    public static double Uptake(HideState h, CraftSupplyDefinition s, IList<float> points, double a)
    {
        if (h == null || s == null)
            return 0;
        double total = 0;
        if (s.kind == CraftSupplyKind.Dye)
        {
            double amount = CraftMath.Clamp01(a);
            StrokeCells(h, points, DyeReach, (i, c, w) => total += DyeTake(c, amount, w));
            return total;
        }
        if (s.kind != CraftSupplyKind.Oil && s.kind != CraftSupplyKind.Wax)
            return 0;
        double am = ConditionAmount(s, a);
        if (points == null || points.Count < 2)
        {
            foreach (HideCell c in h.cells)
                if (c.present) total += ConditionTake(c, s.kind, am, 1);
        }
        else
            StrokeCells(h, points, OilReach, (i, c, w) => total += ConditionTake(c, s.kind, am, w));
        return total;
    }

    /// <summary>
    /// Paying for oil, wax or dye by what the leather takes in: a stroke taking <paramref name="loads"/> cell-loads, from a
    /// pot (flask, cake) with <paramref name="left"/> still in it, opens this many new items - each covers
    /// <paramref name="cellsPerItem"/> cells - and leaves <paramref name="leftAfter"/> in the pot. A stroke that takes
    /// nothing in costs nothing; what the open pot still holds is used first.
    /// </summary>
    public static int PotsToOpen(double loads, double left, double cellsPerItem, out double leftAfter)
    {
        double each = Math.Max(0.1, cellsPerItem);
        loads = Math.Max(0, loads);
        left = Math.Max(0, left);
        int open = (int)Math.Ceiling(Math.Max(0, loads - left) / each - 1e-9);
        leftAfter = Math.Max(0, left + open * each - loads);
        return open;
    }

    public static void Condition(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftSupplyDefinition s = ws.Rules.Supply(op.id);
        if (p == null || p.kind != CraftPieceKind.Hide || s == null)
            return;
        double amount = ConditionAmount(s, op.a);
        void Apply(HideCell c, double w)
        {
            double take = ConditionTake(c, s.kind, amount, w);
            if (s.kind == CraftSupplyKind.Wax)
            {
                c.wax += take;
                c.stiff = Math.Min(1, c.stiff + amount * w * 0.05);
            }
            else
            {
                c.oil += take;
                c.stiff = Math.Max(c.hard * 0.8, c.stiff - take * 0.5);
            }
        }
        if (op.points == null || op.points.Count < 2)
            foreach (HideCell c in p.hide.cells)
            {
                if (c.present) Apply(c, 1);
            }
        else
            StrokeCells(p.hide, op.points, OilReach, (i, c, w) => Apply(c, w));
        p.Record(ws.Elapsed, CraftOpKind.Condition, $"Worked in {s.Name.ToLowerInvariant()}");
    }

    public static void Dye(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        CraftSupplyDefinition s = ws.Rules.Supply(op.id);
        if (p == null || p.kind != CraftPieceKind.Hide || s == null)
            return;
        double amount = CraftMath.Clamp01(op.a); // a hand presses no harder than fully
        double reached = 0, repelled = 0;
        StrokeCells(p.hide, op.points, DyeReach, (i, c, w) =>
        {
            // oil or wax on the grain throws the dye off (DyeTake): it takes in blotches
            reached += w;
            if (c.oil > BlotchesFrom || c.wax > BlotchesFrom) repelled += w;
            double take = DyeTake(c, amount, w);
            if (take <= 0) return;
            double total = c.dye + take;
            c.dyeR = (c.dyeR * c.dye + s.color.r * take) / total;
            c.dyeG = (c.dyeG * c.dye + s.color.g * take) / total;
            c.dyeB = (c.dyeB * c.dye + s.color.b * take) / total;
            c.dye = total;
        });
        p.Record(ws.Elapsed, CraftOpKind.Dye, $"Dyed {s.Name.ToLowerInvariant()}");
        if (reached > 0 && repelled > 0.5 * reached)
            ws.Raise(CraftEventKind.Note, p.id, "blotch", "The dye beads on the oiled leather and takes in blotches.");
    }

    /// <summary>Oil or wax on a cell past this throws a dye brushed over it off in blotches (<see cref="DyeTake"/>): dye first, then oil.</summary>
    public const double BlotchesFrom = 0.25;

    public static void Slick(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        double pressure = CraftMath.Clamp01(op.a);
        HideState h = p.hide;
        bool part = !string.IsNullOrEmpty(h.pattern);
        double all = 0, along = 0;
        StrokeCells(h, op.points, SlickReach, (i, c, w) =>
        {
            double damp = c.moisture > 0.2 && c.moisture < 0.5 ? 1.5 : 1;
            // a damaged grain slicks less, and hair left on it keeps the slicker off it
            c.smooth = Math.Min(1, c.smooth + pressure * w * 0.3 * damp * c.grain * (1 - CraftMath.Clamp01(c.hair)));
            if (!part) return;
            all += w;
            if (OnBorder(h, i)) along += w;
        });
        // A cut part's edges are burnished by the strokes that run along them, not by slicking its middle.
        if (part && all > 0)
            h.edgeFinish = Math.Min(1, h.edgeFinish + pressure * 0.2 * along / all);
        p.Record(ws.Elapsed, CraftOpKind.Slick, "Slicked and burnished", pressure);
    }

    public static void Skive(CraftWorkshop ws, CraftPiece p, CraftOperation op)
    {
        if (p == null || p.kind != CraftPieceKind.Hide)
            return;
        double depth = CraftMath.Clamp01(op.a); // a hand presses no harder than fully
        StrokeCells(p.hide, op.points, SkiveReach, (i, c, w) =>
        {
            double damage0 = c.damage;
            c.thickness = Math.Max(0.3, c.thickness - depth * w);
            if (c.thickness >= SkivedThrough)
                return;
            c.damage = Math.Min(1, c.damage + 0.5 * w);
            if (c.damage > damage0)
                p.AddDefect(CraftDefectKind.Hole, i, c.damage - damage0, true, "skived through");
        });
        p.Record(ws.Elapsed, CraftOpKind.Skive, "Skived thinner", depth);
    }

    /// <summary>
    /// Where <paramref name="pattern"/> is chalked on the hide at exactly (<paramref name="x"/>, <paramref name="y"/>) and
    /// turned so (its index in the layout; -1: not chalked there): a cut there follows the chalk and comes out
    /// <see cref="ChalkedCut"/> cleaner.
    /// </summary>
    public static int ChalkAt(HideState h, string pattern, int x, int y, int rotation)
    {
        if (h == null)
            return -1;
        int turn = ((rotation % 4) + 4) % 4;
        return h.layout.FindIndex(l => l.pattern == pattern && l.x == x && l.y == y && l.rotation == turn);
    }

    /// <summary>Do the cells under <paramref name="pattern"/> at (x, y) all exist (are still part of the hide)?</summary>
    public static bool Fits(HideState h, CraftPattern pattern, int x, int y, int rotation)
    {
        pattern.Size(rotation, out int w, out int hh);
        for (int py = 0; py < hh; py++)
            for (int px = 0; px < w; px++)
            {
                if (!pattern.Covers(px, py, rotation)) continue;
                HideCell c = h.Cell(x + px, y + py);
                if (c == null || !c.present) return false;
            }
        return true;
    }
}

public sealed partial class CraftWorkshop
{
    // ------------------------------------------------------------------ vats
    /// <summary>
    /// Fills a vat with an agent (<paramref name="doses"/> of it), or strengthens it; water into a liquor dilutes it, and
    /// an agent poured into a vat of plain water makes its liquor there.
    /// </summary>
    public bool FillVat(int vat, string agent, int doses, out string reason)
    {
        if (!CanFillVat(vat, agent, out reason))
            return Refuse(reason, out reason);
        Record(new CraftOperation { kind = CraftOpKind.FillVat, i0 = vat, id = Rules.Agent(agent).id, i1 = Math.Max(1, doses) });
        return true;
    }

    /// <summary>
    /// Can <paramref name="agent"/> go into vat <paramref name="vat"/> - an empty vat, one of plain water (it makes its
    /// liquor there), the same liquor (it strengthens it), or water to thin a liquor - and if not, why (the bucket asks
    /// before it starts to pour)? Nothing is refused or recorded by asking.
    /// </summary>
    public bool CanFillVat(int vat, string agent, out string reason)
    {
        reason = "";
        CraftTanningAgent a = Rules.Agent(agent);
        CraftVatState v = Has(CraftTools.Vats) && vat >= 0 && vat < Vats.Count ? Vats[vat] : null;
        if (v == null)
            reason = "There is no such vat here.";
        else if (a == null)
            reason = "That does not go in a vat.";
        else if (v.volume > 0.01 && !string.IsNullOrEmpty(v.agent) && v.agent != a.id && a.kind != CraftAgentKind.Water && Rules.Agent(v.agent)?.kind != CraftAgentKind.Water)
            reason = "The vat holds something else: empty it first.";
        return reason.Length == 0;
    }

    private void ApplyFillVat(CraftOperation op)
    {
        CraftTanningAgent a = Rules.Agent(op.id);
        if (a == null || op.i0 < 0 || op.i0 >= Vats.Count)
            return;
        CraftVatState v = Vats[op.i0];
        if (v.volume <= 0.01 || string.IsNullOrEmpty(v.agent))
        {
            v.agent = a.id;
            v.volume = 60;
            v.strength = a.kind == CraftAgentKind.Water ? 0 : Math.Min(1, a.dose * op.i1);
            v.temperature = Ambient;
        }
        else if (a.kind == CraftAgentKind.Water && v.agent != a.id)
            v.strength *= Math.Pow(0.6, op.i1);
        else if (a.kind != CraftAgentKind.Water && Rules.Agent(v.agent)?.kind == CraftAgentKind.Water)
        {
            // the agent dissolves in the water already there: the liquor is made in the vat
            v.agent = a.id;
            v.strength = Math.Min(1, a.dose * op.i1);
        }
        else
            v.strength = Math.Min(1, v.strength + a.dose * op.i1);
    }

    public bool DrainVat(int vat, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count)
            return Refuse("There is no such vat here.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.DrainVat, i0 = vat });
        return true;
    }

    private void ApplyDrainVat(CraftOperation op)
    {
        if (op.i0 < 0 || op.i0 >= Vats.Count)
            return;
        CraftVatState v = Vats[op.i0];
        v.agent = "";
        v.volume = 0;
        v.strength = 0;
        v.heat = 0;
        foreach (CraftPiece p in pieces)
            if (p.location == CraftLocation.Vat && p.vat == op.i0)
                p.location = CraftLocation.Air;
    }

    /// <summary>A fire under a vat (0-1): hot water for hardening leather.</summary>
    public bool SetVatHeat(int vat, double heat, out string reason)
    {
        reason = "";
        if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count)
            return Refuse("There is no such vat here.", out reason);
        heat = CraftMath.Clamp01(heat);
        if (Math.Abs(heat - Vats[vat].heat) < 0.005)
            return true; // no real change (a fire turned by a hair): the history keeps only what changed, as for the air vent
        Record(new CraftOperation { kind = CraftOpKind.SetVatHeat, i0 = vat, a = (float)heat });
        return true;
    }

    /// <summary>
    /// Stirs a vat with the paddle, as steadily as <paramref name="steadiness"/> (0-1: circles of an even radius; 0 = not
    /// known, a full stir): an unsteady paddle mixes the liquor only half as well (<see cref="StirredBy"/>) - and never
    /// leaves it less mixed than it was.
    /// </summary>
    public bool StirVat(int vat, out string reason, double steadiness = 0)
    {
        reason = "";
        if (!Has(CraftTools.Vats) || vat < 0 || vat >= Vats.Count || Vats[vat].volume <= 0)
            return Refuse("There is nothing to stir.", out reason);
        Record(new CraftOperation { kind = CraftOpKind.StirVat, i0 = vat, a = (float)CraftMath.Clamp01(steadiness) });
        return true;
    }

    /// <summary>How well stirred a vat is after a stir of <paramref name="steadiness"/> (0 = not known: fully).</summary>
    public static double StirredBy(double steadiness) => steadiness > 0 ? CraftMath.Clamp01(0.5 + 0.5 * steadiness) : 1;

    // ------------------------------------------------------------------ hide work
    private bool HideCheck(string id, CraftTools tool, string missing, out CraftPiece p, out string reason, bool allowFrame = false, bool clothToo = false)
    {
        reason = "";
        p = Piece(id);
        if ((Tools & tool) == 0) // any of the tools named will do
            return Refuse(missing, out reason, id);
        if (p == null || p.kind != CraftPieceKind.Hide)
            return Refuse("That is not a hide.", out reason, id);
        if (!clothToo && CraftTextiles.IsCloth(Rules, p))
            return Refuse("It is cloth: it needs no fleshing, staking or tanning.", out reason, id);
        if (p.location != CraftLocation.Air && !(allowFrame && (p.location == CraftLocation.Frame || p.location == CraftLocation.Rack)))
            return Refuse("Take it out first.", out reason, id);
        return true;
    }

    /// <summary>A stroke of the knife at the beam along <paramref name="path"/> (cells, x/y pairs): the flesh or the grain side.</summary>
    public bool Scrape(string id, IList<float> path, bool grainSide, double pressure, double angle, out string reason)
    {
        if (!HideCheck(id, CraftTools.FleshingBeam, "There is no fleshing beam here.", out CraftPiece p, out reason))
            return false;
        if (path == null || path.Count < 2)
            return Refuse("The knife did not touch it.", out reason, id);
        var op = new CraftOperation { kind = CraftOpKind.Scrape, piece = id, flag = grainSide, a = (float)pressure, b = (float)angle };
        op.points.AddRange(path);
        Record(op);
        return true;
    }

    /// <summary>The tension on each side of the frame (0-1): north, east, south, west.</summary>
    public bool Stretch(string id, double north, double east, double south, double west, out string reason)
    {
        reason = "";
        CraftPiece p = Piece(id);
        if (p == null || p.kind != CraftPieceKind.Hide || p.location != CraftLocation.Frame)
            return Refuse("Lace it in the frame first.", out reason, id);
        Record(new CraftOperation { kind = CraftOpKind.Stretch, piece = id, a = (float)north, b = (float)east, c = (float)south, d = (float)west });
        return true;
    }

    private bool PathOp(CraftOpKind kind, string id, IList<float> path, float a, string supply, CraftTools tool, string missing, out string reason, bool allowFrame = true, bool clothToo = false)
    {
        if (!HideCheck(id, tool, missing, out CraftPiece p, out reason, allowFrame, clothToo))
            return false;
        var op = new CraftOperation { kind = kind, piece = id, a = a, id = supply ?? "" };
        if (path != null) op.points.AddRange(path);
        Record(op);
        return true;
    }

    public bool Stake(string id, IList<float> path, double pressure, out string reason) =>
        PathOp(CraftOpKind.Stake, id, path, (float)pressure, null, FinishingTools, NoFinishing, out reason);

    /// <summary>Works oil or wax in (along a path, or all over when <paramref name="path"/> is null).</summary>
    public bool Condition(string id, string supply, IList<float> path, double amount, out string reason)
    {
        CraftSupplyDefinition s = Rules.Supply(supply);
        if (s == null || s.kind != CraftSupplyKind.Oil && s.kind != CraftSupplyKind.Wax)
            return Refuse("That does not condition leather.", out reason, id);
        return PathOp(CraftOpKind.Condition, id, path, (float)amount, s.id, FinishingTools, NoFinishing, out reason, true, true);
    }

    public bool Dye(string id, string dye, IList<float> path, double amount, out string reason)
    {
        CraftSupplyDefinition s = Rules.Supply(dye);
        if (s == null || s.kind != CraftSupplyKind.Dye)
            return Refuse("That is not a dye.", out reason, id);
        return PathOp(CraftOpKind.Dye, id, path, (float)amount, s.id, FinishingTools, NoFinishing, out reason, true, true);
    }

    /// <summary>
    /// The finishing of leather - staking, slicking, skiving, oiling, dyeing - is done at a currier's beam in a tannery
    /// or at a leather bench: either will do.
    /// </summary>
    public const CraftTools FinishingTools = CraftTools.LeatherBench | CraftTools.FleshingBeam;
    private const string NoFinishing = "There is no leather bench or fleshing beam here to finish leather on.";

    public bool Slick(string id, IList<float> path, double pressure, out string reason) =>
        PathOp(CraftOpKind.Slick, id, path, (float)pressure, null, FinishingTools, NoFinishing, out reason);

    public bool Skive(string id, IList<float> path, double depth, out string reason) =>
        PathOp(CraftOpKind.Skive, id, path, (float)depth, null, FinishingTools, NoFinishing, out reason, false);

    /// <summary>The refusal for a piece already cut to a pattern (a part is fitted, not cut again).</summary>
    public const string AlreadyCut = "It is already cut to a pattern: cut parts from a whole hide or a bolt of cloth.";

    /// <summary>
    /// Cuts a pattern from a hide at cell (<paramref name="x"/>, <paramref name="y"/>), rotated by quarter turns, as
    /// cleanly as <paramref name="accuracy"/> (0-1, the cutting minigame). Returns the part's piece id. A part already
    /// cut is not cut again.
    /// </summary>
    public string CutPattern(string id, string pattern, int x, int y, int rotation, double accuracy, out string reason)
    {
        reason = "";
        // Leather is cut at the leather bench; cloth there or at the tailor's table.
        CraftPiece src = Piece(id);
        CraftTools table = CraftTextiles.IsCloth(Rules, src) && Has(CraftTools.TailorTable) ? CraftTools.TailorTable : CraftTools.LeatherBench;
        if (!HideCheck(id, table, "There is no cutting table here.", out CraftPiece p, out reason, false, true))
            return null;
        if (!string.IsNullOrEmpty(p.hide.pattern))
        {
            Refuse(AlreadyCut, out reason, id);
            return null;
        }
        CraftPattern pat = Rules.Pattern(pattern);
        if (pat == null)
        {
            Refuse("There is no such pattern.", out reason, id);
            return null;
        }
        if (!CraftLeather.Fits(p.hide, pat, x, y, rotation))
        {
            Refuse("The pattern does not fit there: it runs off the hide.", out reason, id);
            return null;
        }
        var op = new CraftOperation { kind = CraftOpKind.CutPattern, piece = id, id = pat.id, i0 = x, i1 = y, i2 = rotation, a = (float)CraftMath.Clamp01(accuracy) };
        Record(op);
        return op.other;
    }

    private void ApplyCutPattern(CraftOperation op)
    {
        CraftPiece src = Piece(op.piece);
        CraftPattern pat = Rules.Pattern(op.id);
        if (src == null || pat == null)
            return;
        pat.Size(op.i2, out int w, out int h);
        CraftHideDefinition stuff = Rules.Hide(src.hide.hide);
        string material = stuff?.Name.ToLowerInvariant() ?? "hide";
        // "A belt strap cut from a cow hide", "a tunic body cut from wool cloth"
        CraftPiece part = Create(op, CraftPieceKind.Hide, $"{CraftText.A(pat.Name)} {pat.Name.ToLowerInvariant()} cut from {(stuff != null && stuff.textile ? material : $"{CraftText.A(material)} {material}")}");
        HideState from = src.hide;
        // Cut along a chalked line: cleaner. Any chalked pattern it cuts through is lost.
        double accuracy = op.a;
        int chalked = CraftLeather.ChalkAt(from, pat.id, op.i0, op.i1, op.i2);
        if (chalked >= 0)
        {
            from.layout.RemoveAt(chalked);
            accuracy = Math.Min(1, accuracy + CraftLeather.ChalkedCut);
        }
        int hit = CraftTextiles.Overlaps(Rules, from, pat, op.i0, op.i1, op.i2);
        while (hit >= 0)
        {
            from.layout.RemoveAt(hit);
            hit = CraftTextiles.Overlaps(Rules, from, pat, op.i0, op.i1, op.i2);
        }
        HideState to = part.hide;
        to.hide = from.hide;
        to.seed = from.seed;
        to.agent = from.agent;
        to.shrinkTemperature = from.shrinkTemperature;
        to.hoursRaw = from.hoursRaw;
        to.fireResistance = from.fireResistance;
        to.dried = from.dried;
        to.pattern = pat.id;
        to.rotation = op.i2;
        to.cutQuality = accuracy;
        to.width = w;
        to.height = h;
        var taken = new HashSet<int>(); // the hide's cells that go into the part
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                HideCell c = from.Cell(op.i0 + px, op.i1 + py);
                bool covered = pat.Covers(px, py, op.i2) && c != null && c.present;
                HideCell copy = covered ? c.Clone() : new HideCell { present = false };
                if (covered)
                {
                    c.present = false;
                    taken.Add((op.i1 + py) * from.width + op.i0 + px);
                    // A careless cut leaves ragged edges.
                    bool border = !pat.Covers(px - 1, py, op.i2) || !pat.Covers(px + 1, py, op.i2) || !pat.Covers(px, py - 1, op.i2) || !pat.Covers(px, py + 1, op.i2);
                    if (border) copy.damage = Math.Min(1, copy.damage + (1 - accuracy) * 0.25);
                }
                to.cells.Add(copy);
            }
        foreach (CraftRecordEntry r in src.record)
            part.record.Add(new CraftRecordEntry { time = r.time, kind = r.kind, text = r.text, count = r.count, min = r.min, max = r.max });
        // Flaws: those of the cells under the pattern go with the part (where they are in it) and leave the hide; of the
        // whole hide's, only what it all went through (rot, a closed tan, shrinking) - a knife's slip elsewhere does not.
        foreach (CraftDefect d in src.defects)
        {
            if (d.at < 0)
            {
                if (d.kind == CraftDefectKind.Rot || d.kind == CraftDefectKind.CaseHardened || d.kind == CraftDefectKind.Shrunk)
                    part.defects.Add(d.Clone());
                continue;
            }
            if (!taken.Contains(d.at))
                continue;
            CraftDefect moved = d.Clone();
            moved.at = (d.at / from.width - op.i1) * w + d.at % from.width - op.i0;
            part.defects.Add(moved);
        }
        src.defects.RemoveAll(d => d.at >= 0 && taken.Contains(d.at));
        // How it dried goes with every part: puckered, out of square.
        foreach (string tag in new[] { CraftLeather.PuckeredTag, CraftLeather.DistortionTag })
            if (src.Tag(tag) > 0)
                part.SetTag(tag, src.Tag(tag));
        part.Record(Elapsed, op.kind, chalked >= 0 ? $"Cut out along the chalk as {CraftText.A(pat.Name)} {pat.Name.ToLowerInvariant()}" : $"Cut out as {CraftText.A(pat.Name)} {pat.Name.ToLowerInvariant()}", accuracy);
        bool any = false;
        foreach (HideCell c in from.cells) any |= c.present;
        if (!any)
            Destroy(src, "Nothing is left of the hide.");
        Raise(CraftEventKind.Cut, part.id, pat.id, "");
    }
}
