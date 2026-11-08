#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Whole journeys from the default stock to a finished, tried leather, cloth, wooden or jewelled item (a bow from a
    /// blank and from a green log, a leather jerkin from a raw hide, a kite shield, a padded gambeson, a wool tunic from the
    /// fleece, a bezel ring, a pendant and a charm from rough stones), at the default stations (their presets' tools and
    /// instruments), every step sent as the station's window sends it and bounded by the player's guard as a real
    /// station bounds it (not a test station): the harness below does what <c>CraftStation.Request</c> does where the
    /// rules run - the shape checks, the guard, practice, the station's own wrappers (supplies from the bag, named as
    /// they go in, the open pots, a spin from the bag, reeling thread off) - and the real time between the requests. Where
    /// the guide says what to do next, the journey does that. Then every leather and cloth design made well. (The rules
    /// these journeys showed wrong, each on its own: <see cref="CraftReviewJourneysCraftTests"/>.)
    /// </summary>
    public class CraftCraftJourneyTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        // ------------------------------------------------------------------ the player and the stations
        /// <summary>The player's bag: supply (and stock) ids and how many.</summary>
        private sealed class Bag
        {
            public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
            public int Count(string id) => id != null && Items.TryGetValue(id, out int n) ? n : 0;
            public void Add(string id, int n) { Items[id] = Count(id) + n; }
            public bool Use(string id, int n)
            {
                if (Count(id) < n) return false;
                Items[id] = Count(id) - n;
                return true;
            }
        }

        /// <summary>A pot opened: what is left of the item (a flask of oil, a cake of wax, a pot of varnish).</summary>
        private sealed class Pots
        {
            private readonly Dictionary<string, double> left = new Dictionary<string, double>();
            public double Left(string id) => left.TryGetValue(id, out double l) ? l : 0;
            public int ToOpen(string id, double use) => Math.Max(0, (int)Math.Ceiling(use - Left(id) - 1e-9));
            public void Use(string id, double use) { int open = ToOpen(id, use); left[id] = Left(id) + open - use; }
        }

        /// <summary>
        /// A station of the given tools and instruments where the rules run, worked by one player (<see cref="Practice"/> in
        /// each craft, 0 = a novice). <see cref="Send"/> is <c>CraftStation.Request</c> at a real station.
        /// </summary>
        private sealed class Station
        {
            public readonly string Name;
            public readonly CraftWorkshop W;
            public readonly CraftRequestGuard Guard;
            public readonly CraftInstruments Instruments;
            public readonly Bag Bag;
            public readonly Pots Pots = new Pots();
            public readonly Dictionary<CraftDiscipline, double> Practice = new Dictionary<CraftDiscipline, double>();
            public double Now = 1;
            public string Last = "";
            public readonly List<string> Log = new List<string>();
            public int Refused;

            public Station(string name, CraftTools tools, Bag bag, int seed, CraftInstruments? instruments = null)
            {
                Name = name;
                W = new CraftWorkshop(Rules(), tools, 18, seed, tools.HasFlag(CraftTools.Vats) ? 3 : 0, "water", name);
                Guard = new CraftRequestGuard(seed * 7919 + 17, 0);
                Bag = bag;
                // the editor's presets: a magnet and a test file everywhere, the gem kit and calipers at a lapidary or jeweller,
                // a moisture meter at a woodshop, a thermometer and a balance at a fibre bench
                CraftInstruments kit = CraftInstruments.Magnet | CraftInstruments.TestFile;
                if ((tools & (CraftTools.GemBench | CraftTools.FacetingMachine | CraftTools.JewelersBench)) != 0) kit |= CraftInstruments.GemKit | CraftInstruments.Calipers;
                if ((tools & (CraftTools.DryingStack | CraftTools.ShavingHorse)) != 0) kit |= CraftInstruments.MoistureMeter;
                if ((tools & CraftTools.FibreBench) != 0) kit |= CraftInstruments.Thermometer | CraftInstruments.Scale;
                Instruments = instruments ?? kit;
                Guard.Touch(Now);
            }

            public CraftRules R => W.Rules;
            public CraftPiece P(string id) => W.Piece(id);
            public double Skill(CraftDiscipline d) => Practice.TryGetValue(d, out double m) ? m : 0;

            /// <summary>Real time passes at the station (its workshop runs: the station's Update).</summary>
            public void Wait(double seconds)
            {
                if (seconds <= 0) return;
                Now += seconds;
                int steps = (int)Math.Round(seconds / R.Settings.stepSeconds);
                W.RunSteps(steps);
            }

            /// <summary>Hours of process time (the tannery's, the drying stack's): the player walks off and comes back.</summary>
            public void Hours(double hours) => Wait(hours / R.Settings.processHoursPerSecond);

            private double Pace(in CraftRequest r)
            {
                CraftSimulationSettings s = R.Settings;
                CraftDiscipline? craft = CraftMastery.OfRequest(R, W, r);
                double skill = craft.HasValue ? Skill(craft.Value) : 0;
                return s.handWorkPace * (s.windowPaceNovice + (s.windowPaceMaster - s.windowPaceNovice) * CraftMath.Clamp01(skill));
            }

            /// <summary>Puts a stock piece on the station (the item's own piece: what <c>CraftStockItemSO.MakePiece</c> makes).</summary>
            public string Put(CraftPiece piece, IEnumerable<CraftPiece> parts = null)
            {
                Assert.IsNotNull(piece, "the stock makes a piece");
                return W.AddPiece(piece, parts);
            }

            /// <summary>Takes a piece off this station (Take) and puts it on <paramref name="to"/> (Put): a worked piece comes back as it was carried off.</summary>
            public string CarryTo(string id, Station to)
            {
                Assert.IsTrue(CraftWorkshop.CanTake(P(id), out string why), $"{Name}: take {id}: {why}");
                List<CraftPiece> taken = W.RemovePiece(id, out why);
                Assert.IsNotNull(taken, why);
                return to.Put(taken[0].Clone(), taken.Skip(1).Select(c => c.Clone()).ToList());
            }

            /// <summary>The request as a real station takes it (the guard, practice, the station's wrappers). False = refused (<see cref="Last"/> says why).</summary>
            public bool Send(CraftRequest r)
            {
                string reason;
                CraftRequestGuard.Fit(ref r, out string trimmed);
                if (!CraftRequestGuard.WellFormed(r, out reason)) return No(r, reason);
                string only = CraftRequestGuard.TestStationOnly(r, W);
                if (only != null) return No(r, only);
                CraftDiscipline? craft = CraftMastery.OfRequest(R, W, r);
                double practice = craft.HasValue ? Skill(craft.Value) : 0;
                Guard.Skill = practice;
                if (!Guard.Check(ref r, Now, out string note, W)) return No(r, note);
                if (note.Length == 0) note = trimmed;
                CraftMastery.ByPractice(ref r, practice);
                bool ok = Dispatch(r, out reason);
                if (!ok)
                {
                    Guard.Refund();
                    return No(r, reason);
                }
                Last = note;
                Log.Add($"{Now,8:0.0} {r.kind} {r.piece} {r.id}: ok {note}");
                return true;
            }

            private bool No(in CraftRequest r, string reason)
            {
                Last = reason;
                Refused++;
                Log.Add($"{Now,8:0.0} {r.kind} {r.piece} {r.id}: REFUSED {reason}");
                return false;
            }

            /// <summary>Sends and insists it was done.</summary>
            public void Must(CraftRequest r, string what = null)
            {
                Assert.IsTrue(Send(r), $"{Name}: {what ?? r.kind.ToString()} refused: {Last}");
            }

            private bool Dispatch(CraftRequest r, out string reason)
            {
                reason = "";
                CraftWorkshop w = W;
                switch (r.kind)
                {
                    case CraftRequestKind.Move:
                    {
                        int to = r.flag ? r.i1 : r.i1 > 0 ? r.i1 : 9999;
                        return w.MovePiece(r.piece, r.location, out reason, r.a > 0 ? r.a : 1, r.i0, to, r.i2, r.b);
                    }
                    case CraftRequestKind.SetIntent: return w.SetIntent(r.piece, r.id, r.other, out reason);
                    case CraftRequestKind.Carve: return w.Carve(r.piece, r.i0, r.i1, (CraftSurface.CarveCut)r.i2, r.a * Pace(r) / Math.Max(0.1, R.Settings.handWorkPace), r.b, out reason);
                    case CraftRequestKind.SawCut: return w.SawCut(r.piece, r.a, r.b, r.c, out reason) != null;
                    case CraftRequestKind.RipCut: return w.RipCut(r.piece, r.a, r.b, r.c, r.d, r.i0 / 1000.0, out reason);
                    case CraftRequestKind.Rive: return w.Rive(r.piece, r.a, r.b, out reason, r.c > 0 ? r.c : 0.5) != null;
                    case CraftRequestKind.DebarkWood: return w.DebarkWood(r.piece, r.i0, r.i1, r.a, r.b, r.c, out reason);
                    case CraftRequestKind.SplitLog: return w.SplitLog(r.piece, r.a, r.b, r.c, Math.Max(1, r.i0), out reason) != null;
                    case CraftRequestKind.StackWood: return w.StackWood(r.piece, r.a, r.b, r.c, out reason);
                    case CraftRequestKind.StackCover: return w.SetStackCover(r.a, out reason);
                    case CraftRequestKind.BendWood: return w.BendWood(r.piece, r.i0, r.i1, r.a, r.b, r.flag, out reason, r.c);
                    case CraftRequestKind.SandWood: return w.SandWood(r.piece, r.i0, r.i1, r.a, r.b * Pace(r) / Math.Max(0.1, R.Settings.handWorkPace), r.c, out reason);
                    case CraftRequestKind.Varnish: return Brush(r, out reason);
                    case CraftRequestKind.File: return w.File(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, r.c * Pace(r), out reason);
                    case CraftRequestKind.Grind: return w.Grind(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, r.c * Pace(r), out reason);
                    case CraftRequestKind.Polish: return w.Polish(r.piece, r.i0, r.i1, r.id, r.a, r.b * Pace(r), out reason);
                    case CraftRequestKind.FillVat:
                        if (Bag.Count(r.id) < Math.Max(1, r.count)) { reason = $"You have no {r.id}."; return false; }
                        if (!w.FillVat(r.i0, r.id, Math.Max(1, r.count), out reason)) return false;
                        Bag.Use(r.id, Math.Max(1, r.count));
                        return true;
                    case CraftRequestKind.DrainVat: return w.DrainVat(r.i0, out reason);
                    case CraftRequestKind.VatHeat: return w.SetVatHeat(r.i0, r.a, out reason);
                    case CraftRequestKind.StirVat: return w.StirVat(r.i0, out reason, r.a);
                    case CraftRequestKind.Scrape: return w.Scrape(r.piece, r.points, r.flag, r.a, r.b, out reason);
                    case CraftRequestKind.Stretch: return w.Stretch(r.piece, r.a, r.b, r.c, r.d, out reason);
                    case CraftRequestKind.Stake: return w.Stake(r.piece, r.points, r.a, out reason);
                    case CraftRequestKind.Slick: return w.Slick(r.piece, r.points, r.a, out reason);
                    case CraftRequestKind.Skive: return w.Skive(r.piece, r.points, r.a, out reason);
                    case CraftRequestKind.Condition:
                    case CraftRequestKind.Dye: return FinishLeather(r, out reason);
                    case CraftRequestKind.Cut: return w.CutPattern(r.piece, r.id, r.i0, r.i1, r.i2, r.a, out reason) != null;
                    case CraftRequestKind.LayoutPattern: return w.LayoutPattern(r.piece, r.id, r.i0, r.i1, r.i2, r.flag, out reason);
                    case CraftRequestKind.StartAssembly:
                    {
                        CraftDesign d = R.Design(r.id);
                        if (d == null || !CraftAssembly.MadeAt(R, d, w.Tools, out string missing)) { reason = "That is not made here."; return false; }
                        return w.StartAssembly(r.id, out reason) != null;
                    }
                    case CraftRequestKind.Attach: return Attach(r, out reason);
                    case CraftRequestKind.JointWork: return w.JointWork(r.piece, r.id, (CraftJointWork)r.i0, Math.Max(0, r.count), r.a, r.b, out reason);
                    case CraftRequestKind.Finish:
                    {
                        CraftPiece asm = w.Piece(r.piece);
                        CraftDesign d = asm != null && asm.kind == CraftPieceKind.Assembly ? R.Design(asm.assembly.design) : null;
                        return w.Finish(r.piece, out reason, Skill(CraftMastery.OfDesign(d)));
                    }
                    // the fibre bench, the wheel and the loom
                    case CraftRequestKind.CleanFibre: return w.CleanFibre(r.piece, r.a, r.b, out reason);
                    case CraftRequestKind.ScourFibre:
                        if (!string.IsNullOrEmpty(r.id) && Bag.Count(r.id) < 1) { reason = $"You have no {r.id}."; return false; }
                        if (!w.ScourFibre(r.piece, r.a, r.b, r.c, r.d, r.id, r.i0, out reason)) return false;
                        if (!string.IsNullOrEmpty(r.id)) Bag.Use(r.id, 1);
                        return true;
                    case CraftRequestKind.OpenFibre: return w.OpenFibre(r.piece, r.a, r.b, out reason);
                    case CraftRequestKind.CardFibre: return w.CardFibre(r.piece, Math.Max(1, r.count), r.a, r.b, out reason);
                    case CraftRequestKind.CombFibre: return w.CombFibre(r.piece, Math.Max(1, r.count), r.a, out reason);
                    case CraftRequestKind.SpinFibre: return w.SpinFibre(r.piece, r.a, r.b, r.c, Math.Max(1, r.i0), out reason) != null;
                    case CraftRequestKind.Spin: return SpinFromBag(r.id, r.a, r.b, r.c, out reason);
                    case CraftRequestKind.PlyYarn: return w.PlyYarn(r.piece, r.other, r.id, r.a, r.b, out reason);
                    case CraftRequestKind.JoinYarn: return w.JoinYarn(r.piece, r.other, out reason);
                    case CraftRequestKind.MeasureYarn: return w.MeasureYarn(r.piece, r.a, out reason);
                    case CraftRequestKind.ReelYarn: return Reel(r.piece, out reason);
                    case CraftRequestKind.WeaveCloth:
                        return w.WeaveCloth(r.piece, r.i0, r.a, r.b, r.points != null ? r.points.GetRange(0, Math.Min(r.points.Count, Math.Max(0, r.i1))) : null, out reason) != null;
                    case CraftRequestKind.MeasureFit: return w.MeasureFit(r.piece, r.a, r.b, out reason);
                    // the lapidary
                    case CraftRequestKind.TestGem:
                    {
                        var test = (CraftGemTest)r.i0;
                        CraftInstruments need = test switch
                        {
                            CraftGemTest.Scratch => CraftInstruments.HardnessPoints,
                            CraftGemTest.Refractometer => CraftInstruments.Refractometer,
                            CraftGemTest.Gravity => CraftInstruments.Scale,
                            CraftGemTest.Dichroscope => CraftInstruments.Dichroscope,
                            _ => CraftInstruments.Loupe,
                        };
                        if ((Instruments & need) != need) { reason = $"There is no {need} here."; return false; }
                        return w.TestGem(r.piece, test, r.a, r.b, out reason);
                    }
                    case CraftRequestKind.IdentifyGem: return w.IdentifyGem(r.piece, r.id, out reason);
                    case CraftRequestKind.EvaluateGem:
                        if ((Instruments & CraftInstruments.Loupe) == 0) { reason = "There is no loupe here."; return false; }
                        return w.EvaluateGem(r.piece, r.points, r.a, out reason);
                    case CraftRequestKind.OrientGem: return w.OrientGem(r.piece, r.id, r.a, r.b, r.c, r.d, out reason);
                    case CraftRequestKind.CleanGem: return w.CleanGem(r.piece, r.i0 == 1, r.points, r.a, out reason);
                    case CraftRequestKind.CleaveGem: return w.CleaveGem(r.piece, r.a, r.b, r.c, out reason, r.i0 == 1);
                    case CraftRequestKind.SawGem: return w.SawGem(r.piece, r.a, r.b, r.c, r.d, r.i0 / 1000.0, out reason, r.i1);
                    case CraftRequestKind.GrindGem: return w.GrindGem(r.piece, r.points, r.a, r.b, out reason);
                    case CraftRequestKind.FacetGem: return w.FacetGem(r.piece, r.i0, Math.Max(1, r.count), r.a, r.b, r.c, r.d, out reason, r.i2 / 1000.0, r.i1);
                    case CraftRequestKind.PolishGem:
                    {
                        CraftPiece p = w.Piece(r.piece);
                        bool first = p != null && p.kind == CraftPieceKind.Gem && p.gem.polish <= 0;
                        if (first && Bag.Count(r.id) < 1) { reason = $"You have no {r.id}."; return false; }
                        if (!w.PolishGem(r.piece, r.id, Math.Max(1, r.count), r.a, r.b, out reason, r.points)) return false;
                        if (first) Bag.Use(r.id, 1);
                        return true;
                    }
                    // the jeweller
                    case CraftRequestKind.FormShank: return w.FormShank(r.piece, (CraftJewelPart)r.i0, r.a, r.b, r.c, r.i1, out reason);
                    case CraftRequestKind.SeatSetting: return w.SeatSetting(r.piece, r.other, r.a, r.b, r.c, out reason);
                    case CraftRequestKind.FinishJewel: return w.FinishJewel(r.piece, r.a, r.b, out reason);
                    case CraftRequestKind.Routine:
                    {
                        var routine = (CraftRoutine)r.i0;
                        double m = Skill(CraftMastery.RoutineDiscipline(routine));
                        if (!CraftMastery.CanRoutine(routine, m, null)) { reason = "Only a practised hand does that by routine."; return false; }
                        return CraftMastery.DoRoutine(w, routine, m, r.piece, "wearer", null, out reason);
                    }
                    default:
                        reason = $"(the harness does not send {r.kind})";
                        return false;
                }
            }

            /// <summary>The station's Attach: a supply part's thread, padding, solder or cord comes from the bag.</summary>
            private bool Attach(CraftRequest r, out string reason)
            {
                CraftPiece asm = W.Piece(r.piece);
                CraftDesign design = asm != null && asm.kind == CraftPieceKind.Assembly ? R.Design(asm.assembly.design) : null;
                CraftComponentSpec spec = design?.Component(r.id);
                if (spec != null && spec.supply)
                {
                    CraftSupplyDefinition s = R.Supply(r.other);
                    if (s != null && s.kind != spec.supplyKind || s == null && !string.IsNullOrEmpty(r.other))
                    {
                        reason = $"That is not {spec.Name.ToLowerInvariant()}.";
                        return false;
                    }
                    s ??= R.Supplies.FirstOrDefault(x => x.kind == spec.supplyKind && Bag.Count(x.id) > 0) ?? R.FirstSupply(spec.supplyKind);
                    int room = CraftAssembly.SupplyRoom(design, asm, spec.role);
                    if (room <= 0)
                        return W.Attach(r.piece, r.id, 0, null, out reason, 1, s?.strength ?? 0, s?.id);
                    int n = Math.Min(Math.Max(1, r.count), room);
                    if (s != null && Bag.Count(s.id) < n) { reason = $"You have {Bag.Count(s.id)} {s.id}: {n} are needed."; return false; }
                    if (!W.Attach(r.piece, r.id, 0, null, out reason, n, s?.strength ?? 0, s?.id)) return false;
                    if (s != null) Bag.Use(s.id, n);
                    return true;
                }
                return W.Attach(r.piece, r.id, r.i0, r.other, out reason);
            }

            /// <summary>The station's Brush: a stroke of a finish paid from the open pot.</summary>
            private bool Brush(CraftRequest r, out string reason)
            {
                CraftPiece p = W.Piece(r.piece);
                int n = p != null ? p.segments.Count : 0;
                double share = n > 0 ? (Math.Min(n - 1, Math.Max(r.i0, r.i1)) - Math.Max(0, Math.Min(r.i0, r.i1)) + 1) / (double)n : 1;
                double use = CraftWoodwork.BrushUse(share, r.b);
                int open = Pots.ToOpen(r.id, use);
                if (open > Bag.Count(r.id)) { reason = $"You have no {r.id}."; return false; }
                if (!W.Varnish(r.piece, r.id, r.i0, r.i1, r.a, r.b, out reason)) return false;
                Bag.Use(r.id, open);
                Pots.Use(r.id, use);
                return true;
            }

            /// <summary>The station's FinishLeather: oil, wax or dye paid by what the leather takes in.</summary>
            private bool FinishLeather(CraftRequest r, out string reason)
            {
                bool dye = r.kind == CraftRequestKind.Dye;
                CraftSupplyDefinition s = R.Supply(r.id);
                CraftPiece p = W.Piece(r.piece);
                if (s == null || p == null || p.kind != CraftPieceKind.Hide)
                    return dye ? W.Dye(r.piece, r.id, r.points, r.a, out reason) : W.Condition(r.piece, r.id, r.points, r.a, out reason);
                double use = CraftLeather.Uptake(p.hide, s, r.points, r.a) / Math.Max(0.1, s.cellsPerItem);
                int open = Pots.ToOpen(s.id, use);
                if (open > Bag.Count(s.id)) { reason = $"That stroke would take {open} {s.id}: you have {Bag.Count(s.id)}."; return false; }
                if (!(dye ? W.Dye(r.piece, r.id, r.points, r.a, out reason) : W.Condition(r.piece, r.id, r.points, r.a, out reason))) return false;
                Bag.Use(s.id, open);
                Pots.Use(s.id, use);
                return true;
            }

            /// <summary>The station's Spin: one fibre item from the bag laid on the bench, a bobbin spun from it.</summary>
            private bool SpinFromBag(string fibre, double evenness, double twist, double fineness, out string reason)
            {
                reason = "";
                if (!W.Has(CraftTools.SpinningWheel)) { reason = "There is no spinning wheel here."; return false; }
                CraftSupplyDefinition f = R.Supply(fibre);
                if (f == null || f.kind != CraftSupplyKind.Fibre) { reason = "That cannot be spun."; return false; }
                if (!Bag.Use(f.id, 1)) { reason = $"You have no {f.id}."; return false; }
                string id = W.AddPiece(CraftFibre.NewFibre(R, f.id));
                return W.SpinFibre(id, evenness, twist, fineness, CraftFibre.BobbinGrams, out reason) != null;
            }

            /// <summary>The station's ReelYarn: one thread item per 20 m counted, into the bag.</summary>
            public int Reeled;
            private bool Reel(string id, out string reason)
            {
                reason = "";
                CraftPiece p = W.Piece(id);
                if (p == null || p.kind != CraftPieceKind.Yarn) { reason = "That is not yarn."; return false; }
                if (!p.yarn.measured) { reason = "Wind it into a skein and count it first."; return false; }
                int n = CraftFibre.ThreadCount(p);
                string thread = CraftFibre.ThreadOf(R, p);
                if (n < 1 || string.IsNullOrEmpty(thread)) { reason = "There is not enough of it to reel."; return false; }
                if (W.RemovePiece(id, out reason) == null) return false;
                Bag.Add(thread, n);
                Reeled += n;
                LastThread = thread;
                return true;
            }
            public string LastThread;

            public void Dump(int last = 40)
            {
                foreach (string l in Log.Skip(Math.Max(0, Log.Count - last))) Console.WriteLine($"   [{Name}] {l}");
            }
        }

        private static CraftItemReport Tried(Station s, string asm) => CraftEvaluator.Item(s.R, s.W, s.P(asm));

        private static void Print(string what, CraftItemReport r)
        {
            Console.WriteLine($"== {what}: {r.Name} q={r.Quality:0.000} tier={r.Tier} mat={r.Material:0.00} work={r.Workmanship:0.00} integ={r.Integrity:0.00} asm={r.Assembly:0.00} prot={r.Protection:0.00} edge={r.Edge:0.00} wood={r.Wood:0.00}");
            Console.WriteLine($"   def={r.Defense:0.0} dmg={r.DamageMin:0.0}-{r.DamageMax:0.0} dur={r.Durability} weight={r.Weight:0.000} kg price={r.Price:0.00} speed={r.AttackSpeed:0.00}");
            foreach (string n in r.Notes) Console.WriteLine("   note: " + n);
        }

        // ------------------------------------------------------------------ helpers for honest strokes
        /// <summary>A knife stroke as the Carve window sends it (from end to end; a half-second stroke at the knife's default 50 %).</summary>
        private static CraftRequest Knife(CraftPiece p, CraftSurface.CarveCut cut, float press = 0.5f, float seconds = 0.5f, float steady = 0.9f, int from = 0, int to = -1)
        {
            float depth = press * 1.8f * Math.Max(0.4f, Math.Min(1.2f, seconds / 0.5f)) * 1f; // a fresh arm (strength 1)
            return new CraftRequest(CraftRequestKind.Carve, p.id) { i0 = from, i1 = to < 0 ? p.segments.Count - 1 : to, i2 = (int)cut, a = depth, b = steady };
        }

        // ================================================================== the bow (the woodshop)
        [Test]
        public void Journey_HuntingBow_FromAYewBlank_AtTheWoodshop()
        {
            var bag = new Bag();
            bag.Add("hemp_cord", 2);
            bag.Add("linseed_oil", 1);
            var shop = new Station("woodshop", CraftTools.Woodshop, bag, 11);
            CraftRules R = shop.R;
            string stave = shop.Put(CraftDefaults.WoodBlank(R, "yew"));
            CraftPiece p = shop.P(stave);
            Console.WriteLine($"yew blank {p.Length:0} x {p.segments[0].width:0} x {p.segments[0].thickness:0} mm, moisture {p.wood.moisture:0.00}, {p.segments.Count} segments");
            // Carve: what it is for
            shop.Must(new CraftRequest(CraftRequestKind.SetIntent, stave) { id = "hunting_bow", other = "stave" });
            // Saw to length at 1450 mm (the saw strokes are the window's own; the cut goes at the end)
            shop.Wait(8);
            string offcut = null;
            {
                var r = new CraftRequest(CraftRequestKind.SawCut, stave) { a = (float)(1450 / p.Length), b = 0.05f, c = 0.95f };
                shop.Must(r, "saw to length");
            }
            p = shop.P(stave);
            Console.WriteLine($"sawn: {p.Length:0} mm");
            // Shave the thickness and the width to the dashed 30 mm, end to end, two strokes a second
            int strokes = 0;
            while (p.segments.Average(s => s.thickness) > 30.6 && strokes < 400)
            {
                shop.Wait(0.55);
                shop.Must(Knife(p, CraftSurface.CarveCut.Thickness), "shave the thickness");
                strokes++;
            }
            while (p.segments.Average(s => s.width) > 30.6 && strokes < 800)
            {
                shop.Wait(0.55);
                shop.Must(Knife(p, CraftSurface.CarveCut.Width), "shave the width");
                strokes++;
            }
            Console.WriteLine($"carved in {strokes} strokes: {p.segments.Average(s => s.width):0.0} x {p.segments.Average(s => s.thickness):0.0} mm, split {p.wood.split:0.000}, rough {p.segments.Average(s => s.rough):0.0}");
            // Finish: coarse, medium, fine paper - passes along the whole board
            foreach (float grit in new[] { 0.25f, 0.55f, 0.85f })
                for (int k = 0; k < 12; k++)
                {
                    shop.Wait(0.4);
                    shop.Must(new CraftRequest(CraftRequestKind.SandWood, stave) { i0 = 0, i1 = p.segments.Count - 1, a = grit, b = 1.5f, c = 0.95f }, "sand");
                }
            Console.WriteLine($"sanded: rough {p.segments.Average(s => s.rough):0.0}");
            // two long, even coats of linseed oil, the second once the first dried
            shop.Wait(1);
            shop.Must(new CraftRequest(CraftRequestKind.Varnish, stave) { id = "linseed_oil", i0 = 0, i1 = p.segments.Count - 1, a = 0.85f, b = 0.4f }, "oil");
            shop.Hours(11);
            shop.Must(new CraftRequest(CraftRequestKind.Varnish, stave) { id = "linseed_oil", i0 = 0, i1 = p.segments.Count - 1, a = 0.85f, b = 0.4f }, "oil again");
            shop.Hours(11);
            CraftPieceReport part = CraftEvaluator.Piece(R, p);
            Console.WriteLine($"stave: shape {part.Shape:0.00} integrity {part.Integrity:0.00} defects {string.Join("; ", part.Defects)}");
            // Assemble
            shop.Wait(2);
            shop.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "hunting_bow" }, "start");
            string asm = shop.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            shop.Wait(2);
            shop.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "stave", i0 = 0, other = stave }, "fit the stave");
            shop.Wait(2);
            shop.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "string", other = "hemp_cord", count = 1 }, "the bowstring");
            for (int k = 0; k < 10 && !CraftAssembly.JointComplete(R.Design("hunting_bow").Joint("strung"), shop.P(asm).assembly.Joint("strung")); k++)
            {
                shop.Wait(0.8);
                shop.Must(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "strung", i0 = (int)CraftJointWork.Wrap, count = 1, i1 = 0, a = 0.65f, b = 0.05f }, "string it");
            }
            shop.Wait(1);
            shop.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            CraftItemReport item = Tried(shop, asm);
            Print("hunting bow", item);
            shop.Dump(6);
            Assert.Greater(item.Quality, 0.5, "honest, careful work at the woodshop makes a decent bow");
        }

        // ================================================================== hides: the tannery's path, then the leather bench
        /// <summary>Strokes of a tool over every row of a hide, as the beam's window sends them (a point every 0.2 cells, a stroke at most 200 points).</summary>
        private static void Rows(Station s, string hide, Func<List<float>, CraftRequest> tool, double secondsPerStroke = 0.9, int passes = 1)
        {
            HideState h = s.P(hide).hide;
            for (int pass = 0; pass < passes; pass++)
                for (int y = 0; y < h.height; y++)
                {
                    // only across the hide where it is (a stroke off the hide ends there)
                    int x0 = -1, x1 = -1;
                    for (int x = 0; x < h.width; x++)
                        if (h.Cell(x, y)?.present ?? false) { if (x0 < 0) x0 = x; x1 = x; }
                    if (x0 < 0) continue;
                    var pts = new List<float>();
                    for (double x = x0 + 0.1; x <= x1 + 0.9 + 1e-6; x += 0.2) { pts.Add((float)x); pts.Add(y + 0.5f); }
                    if (pts.Count < 4) { pts.Add(x1 + 0.9f); pts.Add(y + 0.5f); }
                    s.Wait(secondsPerStroke);
                    s.Must(tool(pts), "a stroke over the hide");
                }
        }

        private static double Mean(HideState h, Func<HideCell, double> of) => h.cells.Where(c => c.present).Select(of).DefaultIfEmpty(0).Average();

        /// <summary>The best place to lay a pattern (fits, its cells in the thickness wanted, few scars), or null.</summary>
        private static (int x, int y, int rot)? Place(CraftRules R, HideState h, string pattern, double tmin, double tmax)
        {
            CraftPattern pat = R.Pattern(pattern);
            (int, int, int)? best = null;
            double bestScore = double.MaxValue;
            for (int rot = 0; rot < 4; rot++)
            {
                pat.Size(rot, out int w, out int hh);
                for (int y = 0; y + hh <= h.height; y++)
                    for (int x = 0; x + w <= h.width; x++)
                    {
                        if (!CraftLeather.Fits(h, pat, x, y, rot)) continue;
                        double score = 0;
                        for (int py = 0; py < hh; py++)
                            for (int px = 0; px < w; px++)
                            {
                                if (!pat.Covers(px, py, rot)) continue;
                                HideCell c = h.Cell(x + px, y + py);
                                double t = CraftLeather.CellThickness(c);
                                score += Math.Max(0, tmin - t) * 4 + Math.Max(0, t - tmax) * 4 + c.damage * 3;
                            }
                        if (score < bestScore) { bestScore = score; best = (x, y, rot); }
                    }
            }
            return best;
        }

        /// <summary>
        /// A raw hide along the tannery's path at a Tannery (beam, vats, frame, rack): fleshed, limed, unhaired, tanned in oak bark
        /// started weak and strengthened, laced taut in the frame and staked as it dries. Returns the dried leather's id.
        /// </summary>
        private static string Tan(Station tan, string hide, List<string> guide)
        {
            CraftRules R = tan.R;
            void Note() { CraftGuide.Step? g = CraftGuide.Next(tan.W, tan.W.Tools, tan.P(hide), null); guide.Add(g.HasValue ? $"{g.Value.text} [{g.Value.area}/{g.Value.place}]" : "(none)"); }
            Note();
            // Flesh: the knife at its default 35 % pressure and steep angle, row by row, until the guide says it is fleshed
            for (int k = 0; k < 6 && Mean(tan.P(hide).hide, c => c.flesh) >= 0.08; k++)
                Rows(tan, hide, pts => new CraftRequest(CraftRequestKind.Scrape, hide) { points = pts, flag = false, a = 0.35f, b = 0.7f });
            Console.WriteLine($"fleshed: flesh {Mean(tan.P(hide).hide, c => c.flesh):0.000}, damage {Mean(tan.P(hide).hide, c => c.damage):0.000}");
            Note();
            // Lime: four doses of quicklime, the hide in, until LOOSE passes 70 %
            tan.Wait(2);
            for (int k = 0; k < 4; k++) { tan.Wait(1); tan.Must(new CraftRequest(CraftRequestKind.FillVat) { i0 = 0, id = "lime", count = 1 }, "lime"); }
            tan.Wait(2);
            tan.Must(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Vat, i2 = 0 }, "into the lime");
            Note();
            int hours = 0;
            while (Mean(tan.P(hide).hide, c => c.hairLoose) < 0.72 && hours < 80) { tan.Hours(1); hours++; }
            Console.WriteLine($"limed {hours} h: loose {Mean(tan.P(hide).hide, c => c.hairLoose):0.00}");
            tan.Must(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Air }, "out of the lime");
            Note();
            for (int k = 0; k < 6 && Mean(tan.P(hide).hide, c => c.hair) >= 0.1; k++)
                Rows(tan, hide, pts => new CraftRequest(CraftRequestKind.Scrape, hide) { points = pts, flag = true, a = 0.35f, b = 0.5f });
            Console.WriteLine($"unhaired: hair {Mean(tan.P(hide).hide, c => c.hair):0.000}, grain {Mean(tan.P(hide).hide, c => c.grain):0.00}");
            Note();
            // Tan: oak bark started weak (two doses), strengthened as the core comes, stirred now and then
            tan.Must(new CraftRequest(CraftRequestKind.FillVat) { i0 = 1, id = "bark", count = 1 }, "bark");
            tan.Wait(1);
            tan.Must(new CraftRequest(CraftRequestKind.FillVat) { i0 = 1, id = "bark", count = 1 }, "bark");
            tan.Wait(2);
            tan.Must(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Vat, i2 = 1 }, "into the bark");
            Note();
            hours = 0;
            while (Mean(tan.P(hide).hide, c => c.tanC) <= 0.76 && hours < 400)
            {
                tan.Hours(2);
                hours += 2;
                tan.Send(new CraftRequest(CraftRequestKind.StirVat) { i0 = 1, a = 0.8f });
                CraftVatState v = tan.W.Vats[1];
                // the CORE bar's mark: the least tanned place (the window warns past the case-hardening line while it is under 40 %)
                double core = tan.P(hide).hide.cells.Where(c => c.present).Min(c => c.tanC);
                // strengthen it as the core takes the tan (never past the case-hardening line while the core is raw)
                CraftTanningAgent bark = R.Agent("bark");
                if (v.strength + bark.dose <= (core < 0.4 ? bark.caseHardeningAbove : 1) && tan.Bag.Count("bark") > 0)
                    tan.Send(new CraftRequest(CraftRequestKind.FillVat) { i0 = 1, id = "bark", count = 1 });
            }
            HideState h = tan.P(hide).hide;
            Console.WriteLine($"tanned {hours} h: surface {Mean(h, c => c.tanS):0.00} core {Mean(h, c => c.tanC):0.00} (least {h.cells.Where(c => c.present).Min(c => c.tanC):0.00}) open {Mean(h, c => c.open):0.00}, bark left {tan.Bag.Count("bark")}, vat strength {tan.W.Vats[1].strength:0.00}");
            Note();
            tan.Must(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Air }, "out of the bark");
            Note();
            // The frame: laced in, each side pulled to 70 % of what its edge takes
            tan.Wait(3);
            tan.Must(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Frame }, "into the frame");
            h = tan.P(hide).hide;
            float Pull(int side) => (float)(0.7 * CraftLeather.EdgeLimit(R, h, side));
            tan.Wait(4);
            tan.Must(new CraftRequest(CraftRequestKind.Stretch, hide) { a = Pull(0), b = Pull(1), c = Pull(2), d = Pull(3) }, "stretch");
            Note();
            // Stake it as it dries (the DRY bar's gold band), several times, then let it dry through
            hours = 0;
            int staked = 0;
            while (!tan.P(hide).hide.dried && hours < 200)
            {
                tan.Hours(1);
                hours++;
                double wet = Mean(tan.P(hide).hide, c => c.moisture);
                if (wet <= CraftLeather.StakeWet && wet > CraftLeather.StakeDry + 0.03 && staked < 6)
                {
                    Rows(tan, hide, pts => new CraftRequest(CraftRequestKind.Stake, hide) { points = pts, a = 0.4f });
                    staked++;
                }
                if (hours % 6 == 0) Note();
            }
            h = tan.P(hide).hide;
            Console.WriteLine($"dried {hours} h, staked {staked}x: dried={h.dried} stiff {Mean(h, c => c.stiff):0.00} wet {Mean(h, c => c.moisture):0.00} thickness {Mean(h, c => CraftLeather.CellThickness(c)):0.00} damage {Mean(h, c => c.damage):0.000} pucker {tan.P(hide).Tag(CraftLeather.PuckeredTag):0.000}");
            Note();
            tan.Must(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Air }, "take it down");
            Note();
            return hide;
        }

        [Test]
        public void Journey_LeatherJerkin_FromARawCowHide_TanneryThenLeatherworks()
        {
            var bag = new Bag();
            bag.Add("lime", 4);
            bag.Add("bark", 12);
            bag.Add("neatsfoot_oil", 10);
            bag.Add("waxed_thread", 1);
            var tannery = new Station("tannery", CraftTools.Tannery, bag, 21);
            CraftRules R = tannery.R;
            var guide = new List<string>();
            string hide = tannery.Put(CraftLeather.NewHide(R, "cow", 4242));
            Tan(tannery, hide, guide);
            HideState h = tannery.P(hide).hide;
            // dried stiff, as the guide says what to do: bark-tanned, it is oiled supple at the beam (staked dry, its grain
            // would crack), the neatsfoot oil paid from the open flask
            CraftGuide.Step? stiffStep = CraftGuide.Next(tannery.W, tannery.W.Tools, tannery.P(hide), null);
            double grain0 = Mean(h, c => c.grain), stiff0 = Mean(h, c => c.stiff);
            int oiled = 0;
            if (stiff0 >= 0.35)
            {
                Assert.IsTrue(stiffStep.HasValue && stiffStep.Value.text.Contains("oil it supple"), $"the guide oils a bark hide that dried stiff: {stiffStep?.text}");
                while (Mean(tannery.P(hide).hide, c => c.stiff) >= 0.35 && oiled < 6)
                {
                    Rows(tannery, hide, pts => new CraftRequest(CraftRequestKind.Condition, hide) { points = pts, id = "neatsfoot_oil", a = 0.35f });
                    oiled++;
                }
                Assert.Less(Mean(h, c => c.stiff), 0.35, "oiled as the guide says, it is supple");
            }
            Console.WriteLine($"guide: '{stiffStep?.text}'; oiled {oiled}x: stiff {stiff0:0.00} -> {Mean(h, c => c.stiff):0.00}, grain {grain0:0.000} -> {Mean(h, c => c.grain):0.000}; neatsfoot left {bag.Count("neatsfoot_oil")}");
            Assert.AreEqual(grain0, Mean(h, c => c.grain), 1e-9, "what the guide asks for never cracks the grain");
            foreach (string g in guide.Distinct()) Console.WriteLine("   guide: " + g);
            // to the leatherworks
            var works = new Station("leatherworks", CraftTools.Leatherworks, bag, 22);
            string leather = tannery.CarryTo(hide, works);
            CraftGuide.Step? next = CraftGuide.Next(works.W, works.W.Tools, works.P(leather), null);
            Console.WriteLine($"at the leatherworks: {next?.text} [{next?.area}/{next?.place}]");
            CraftDesign d = R.Design("leather_jerkin");
            string Cut(string pattern, double tmin, double tmax)
            {
                var at = Place(R, works.P(leather).hide, pattern, tmin, tmax);
                Assert.IsTrue(at.HasValue, $"{pattern} fits somewhere on the hide");
                works.Wait(20); // laid, marked, cut round
                works.Must(new CraftRequest(CraftRequestKind.Cut, leather) { id = pattern, i0 = at.Value.x, i1 = at.Value.y, i2 = at.Value.rot, a = 0.95f }, "cut " + pattern);
                return works.W.Pieces.Last(p => p.kind == CraftPieceKind.Hide && p.hide.pattern == pattern).id;
            }
            string body = Cut("jerkin_body", 2.5, 4.5), strap1 = Cut("strap", 3, 5), strap2 = Cut("strap", 3, 5);
            foreach (string part in new[] { body, strap1 })
            {
                CraftPieceReport pr = CraftEvaluator.Piece(R, works.P(part));
                Console.WriteLine($"part {works.P(part).hide.pattern}: thick {pr.Thickness:0.00} tan {pr.Tannage:0.00}/{pr.CoreTannage:0.00} supple {pr.Suppleness:0.00} strength {pr.Strength:0.00} integ {pr.Integrity:0.00} defects {string.Join("; ", pr.Defects)}");
            }
            // a little neatsfoot oil on the parts, at the leather bench
            foreach (string part in new[] { body, strap1, strap2 })
                Rows(works, part, pts => new CraftRequest(CraftRequestKind.Condition, part) { points = pts, id = "neatsfoot_oil", a = 0.35f });
            Console.WriteLine($"oiled the parts: body oil {Mean(works.P(body).hide, c => c.oil):0.00} stiff {Mean(works.P(body).hide, c => c.stiff):0.00}; neatsfoot left {bag.Count("neatsfoot_oil")}");
            string buckles = works.Put(CraftDefaults.FastenerSet(R, CraftFastenerKind.Buckle, 2, 30, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002)));
            works.Wait(3);
            works.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "leather_jerkin" }, "start");
            string asm = works.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            foreach ((string role, int i, string piece) in new[] { ("body", 0, body), ("straps", 0, strap1), ("straps", 1, strap2), ("buckles", 0, buckles) })
            {
                works.Wait(2);
                works.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = i, other = piece }, "fit " + role);
            }
            works.Wait(2);
            works.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "thread", other = "waxed_thread", count = 1 }, "thread");
            // the seams: the pricking iron on the beat, then the awl, a beat every 0.7 s, pulled at 60 %
            foreach (string joint in new[] { "straps_sewn", "buckles" })
            {
                CraftJointSpec js = d.Joint(joint);
                for (int beat = 0; beat < 400 && !CraftAssembly.JointComplete(js, works.P(asm).assembly.Joint(joint)); beat++)
                {
                    works.Wait(0.7);
                    JointState j = works.P(asm).assembly.Joint(joint);
                    bool punch = j.holes <= j.stitches;
                    var r = punch
                        ? new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)CraftJointWork.Punch, count = 1, i1 = 2, a = 0.1f, b = 0.07f }
                        : new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)CraftJointWork.Stitch, count = 1, i1 = 2, a = 0.6f, b = 0.1f };
                    if (!works.Send(r)) { Console.WriteLine($"   {joint}: {works.Last}"); break; }
                }
                JointState done = works.P(asm).assembly.Joint(joint);
                Console.WriteLine($"{joint}: holes {done.holes} stitches {done.stitches} tension {done.tension:0.00} damage {done.damage:0.00} complete {CraftAssembly.JointComplete(js, done)}");
            }
            works.Wait(1);
            works.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            CraftItemReport item = Tried(works, asm);
            Print("leather jerkin", item);
            Assert.Greater(item.Quality, 0.5, "honest work along the whole path makes a decent jerkin");
            Assert.LessOrEqual(item.Defense, d.defense * 1.5, $"a well-made leather jerkin guards about as its design says ({d.defense}), not {item.Defense:0.0} (a breastplate's design: {R.Design("breastplate").defense})");
        }

        /// <summary>Cuts a pattern from a hide where it fits best (laid, marked, cut round: some 20 s of the hands), at the novice's cleanest.</summary>
        private static string CutPart(Station s, string hide, string pattern, double tmin, double tmax)
        {
            var at = Place(s.R, s.P(hide).hide, pattern, tmin, tmax);
            Assert.IsTrue(at.HasValue, $"{pattern} fits somewhere on the hide");
            s.Wait(20);
            s.Must(new CraftRequest(CraftRequestKind.Cut, hide) { id = pattern, i0 = at.Value.x, i1 = at.Value.y, i2 = at.Value.rot, a = 0.95f }, "cut " + pattern);
            return s.W.Pieces.Last(p => p.kind == CraftPieceKind.Hide && p.hide.pattern == pattern).id;
        }

        /// <summary>A rivet joint as the assembly bench works it: a hole punched on the beat, then a good blow (60 % of a full one into leather).</summary>
        private static void Rivet(Station s, string asm, string joint, float force = 0.6f)
        {
            CraftDesign d = s.R.Design(s.P(asm).assembly.design);
            CraftJointSpec js = d.Joint(joint);
            for (int k = 0; k < 200 && !CraftAssembly.JointComplete(js, s.P(asm).assembly.Joint(joint)); k++)
            {
                JointState j = s.P(asm).assembly.Joint(joint);
                s.Wait(1.0);
                CraftRequest r = CraftAssembly.NeedsHoles(s.W, s.P(asm), d, js) && j.holes < j.rivets + 1
                    ? new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)CraftJointWork.Punch, count = 1, i1 = 0, a = 0.1f, b = 0.07f }
                    : new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)CraftJointWork.Rivet, count = 1, a = force * 4 };
                if (!s.Send(r)) { Console.WriteLine($"   {joint}: {s.Last}"); break; }
            }
            JointState done = s.P(asm).assembly.Joint(joint);
            Console.WriteLine($"{joint}: holes {done.holes} rivets {done.rivets} quality {done.rivetQuality:0.00} damage {done.damage:0.00} complete {CraftAssembly.JointComplete(js, done)}");
        }

        // ================================================================== the kite shield: woodshop, tannery, armourer
        [Test]
        public void Journey_KiteShield_LindenBoardLeatherFacingRivets_AtTheArmourer()
        {
            var bag = new Bag();
            bag.Add("lime", 4);
            bag.Add("bark", 12);
            var shop = new Station("woodshop", CraftTools.Woodshop, bag, 31);
            CraftRules R = shop.R;
            CraftDesign d = R.Design("kite_shield");
            // the board: a linden plank, ripped to the kite's taper, thinned to 12 mm
            string board = shop.Put(CraftDefaults.WoodBlank(R, "linden"));
            CraftPiece b = shop.P(board);
            Console.WriteLine($"linden plank {b.Length:0} x {b.segments[0].width:0} x {b.segments[0].thickness:0} mm, {b.segments.Count} segments");
            shop.Must(new CraftRequest(CraftRequestKind.SetIntent, board) { id = "kite_shield", other = "board" });
            shop.Wait(10);
            shop.Must(new CraftRequest(CraftRequestKind.SawCut, board) { a = 1000f / 1100f, b = 0.05f, c = 0.95f }, "saw to 1000 mm");
            shop.Wait(25);
            shop.Must(new CraftRequest(CraftRequestKind.RipCut, board) { a = 0, b = 0.6f, c = 500, d = 450, i0 = 60 }, "rip the top");
            shop.Wait(25);
            shop.Must(new CraftRequest(CraftRequestKind.RipCut, board) { a = 0.6f, b = 1, c = 450, d = 60, i0 = 60 }, "rip the point");
            for (int k = 0; k < 40 && b.segments.Average(x => x.thickness) > 12.4; k++)
            {
                shop.Wait(0.55);
                shop.Must(Knife(b, CraftSurface.CarveCut.Thickness), "shave the thickness");
            }
            CraftPieceReport br = CraftEvaluator.Piece(R, b);
            Console.WriteLine($"board: {b.Length:0} mm, widths {string.Join(",", b.segments.Select(x => x.width.ToString("0")))}, thick {b.segments.Average(x => x.thickness):0.0}; shape {br.Shape:0.00} {string.Join("; ", br.ShapeNotes)} integ {br.Integrity:0.00}");
            // the leather: a cow hide along the tannery's path
            var tannery = new Station("tannery", CraftTools.Tannery, bag, 32);
            string hide = tannery.Put(CraftLeather.NewHide(R, "cow", 777));
            Tan(tannery, hide, new List<string>());
            // the armourer: the leather cut, the rivets from the stores, assembled
            var arm = new Station("armourer", CraftTools.Armourer, bag, 33);
            string leather = tannery.CarryTo(hide, arm);
            string boardHere = shop.CarryTo(board, arm);
            string facing = CutPart(arm, leather, "facing", 2, 4), e1 = CutPart(arm, leather, "strap", 3, 5), e2 = CutPart(arm, leather, "strap", 3, 5);
            CraftPieceReport fr = CraftEvaluator.Piece(R, arm.P(facing));
            Console.WriteLine($"facing: thick {fr.Thickness:0.00} supple {fr.Suppleness:0.00} integ {fr.Integrity:0.00} {string.Join("; ", fr.Defects)}");
            CraftPiece Rivets() => CraftDefaults.FastenerSet(R, CraftFastenerKind.Rivet, 10, 4, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002));
            string r1 = arm.Put(Rivets()), r2 = arm.Put(Rivets());
            arm.Wait(3);
            arm.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "kite_shield" }, "start");
            string asm = arm.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            foreach ((string role, int i, string piece) in new[] { ("board", 0, boardHere), ("facing", 0, facing), ("grip_strap", 0, e1), ("grip_strap", 1, e2), ("rivets", 0, r1), ("rivets", 0, r2) })
            {
                arm.Wait(2);
                arm.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = i, other = piece }, "fit " + role);
            }
            Rivet(arm, asm, "facing_riveted");
            Rivet(arm, asm, "straps_riveted");
            CraftGuide.Step? g = CraftGuide.Next(arm.W, arm.W.Tools, arm.P(asm), null);
            Console.WriteLine($"guide: {g?.text} [{g?.area}/{g?.place}]");
            arm.Wait(1);
            arm.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            CraftItemReport item = Tried(arm, asm);
            Print("kite shield", item);
            Assert.Greater(item.Quality, 0.5, "honest work makes a decent shield");
            Assert.LessOrEqual(item.Defense, d.defense * 1.5, $"a well-made kite shield guards about as well as the design says ({d.defense}), not {item.Defense:0.0}");
        }

        /// <summary>
        /// A cloth seam as the tailor's Sew place sends it: a click a beat along the chalk line, each pulled ~0.6, the
        /// stitches going six at a time - the item's thread with the first of them, then the holes the needle pierced, then the pulls.
        /// </summary>
        private static void Sew(Station s, string asm, string joint, string thread, float pull = 0.6f)
        {
            CraftDesign d = s.R.Design(s.P(asm).assembly.design);
            CraftJointSpec js = d.Joint(joint);
            for (int k = 0; k < 100 && s.P(asm).assembly.Joint(joint).stitches < js.count; k++)
            {
                s.Wait(6 * 0.8); // six clicks along the line
                CraftPiece a = s.P(asm);
                foreach (CraftComponentSpec c in d.components)
                    if (c.supply && c.supplyKind == CraftSupplyKind.Thread && a.assembly.Supply(c.role) < c.supplyCount)
                        s.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = c.role, count = Math.Max(1, c.supplyCount - (int)a.assembly.Supply(c.role)), other = thread }, "the thread");
                if (!s.Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)CraftJointWork.Punch, count = 6, i1 = 1, a = 0.15f, b = 0.1f })) { Console.WriteLine($"   {joint} pierce: {s.Last}"); break; }
                if (!s.Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)CraftJointWork.Stitch, count = 6, i1 = 1, a = pull, b = 0.05f })) { Console.WriteLine($"   {joint} pull: {s.Last}"); break; }
            }
            JointState done = s.P(asm).assembly.Joint(joint);
            Console.WriteLine($"{joint}: holes {done.holes} stitches {done.stitches}/{js.count} tension {done.tension:0.00} complete {CraftAssembly.JointComplete(js, done)}");
        }

        // ================================================================== the gambeson (the tailor's, from cloth off the bolt)
        [Test]
        public void Journey_PaddedGambeson_FromBoltsOfCanvasAndLinen_AtTheTailors()
        {
            var bag = new Bag();
            bag.Add("wool_padding", 3);
            bag.Add("linen_thread", 2);
            var tailor = new Station("tailor", CraftTools.Tailor, bag, 41);
            CraftRules R = tailor.R;
            CraftDesign d = R.Design("padded_gambeson");
            string canvas = tailor.Put(CraftLeather.NewHide(R, "canvas", 4101)), linen = tailor.Put(CraftLeather.NewHide(R, "linen_cloth", 4102));
            CraftGuide.Step? g = CraftGuide.Next(tailor.W, tailor.W.Tools, tailor.P(canvas), null);
            Console.WriteLine($"a bolt of canvas: {g?.text} [{g?.area}/{g?.place}]; thickness {Mean(tailor.P(canvas).hide, c => c.thickness):0.00}, linen {Mean(tailor.P(linen).hide, c => c.thickness):0.00}");
            // measure the wearer first (the next item started takes it): a careful tape
            tailor.Wait(15);
            tailor.Must(new CraftRequest(CraftRequestKind.MeasureFit, null) { a = 0.9f, b = (float)(CraftTextiles.ChestOf("wearer") + 0.1 * 60) }, "measure");
            tailor.Wait(2);
            tailor.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "padded_gambeson" }, "start");
            string asm = tailor.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            // cut: two shell panels from the canvas, two lining panels from the linen, along the grain
            string Panel(string bolt)
            {
                HideState h = tailor.P(bolt).hide;
                CraftPattern pat = R.Pattern("gambeson_panel");
                for (int y = 0; y < h.height; y++)
                    for (int x = 0; x < h.width; x++)
                        if (CraftLeather.Fits(h, pat, x, y, 0))
                        {
                            tailor.Wait(25); // pinned, snipped round
                            tailor.Must(new CraftRequest(CraftRequestKind.Cut, bolt) { id = "gambeson_panel", i0 = x, i1 = y, i2 = 0, a = 0.95f }, "cut a panel");
                            return tailor.W.Pieces.Last(p => p.kind == CraftPieceKind.Hide && p.hide.pattern == "gambeson_panel").id;
                        }
                Assert.Fail($"no room for another panel on the {h.hide}");
                return null;
            }
            string s1 = Panel(canvas), s2 = Panel(canvas), l1 = Panel(linen), l2 = Panel(linen);
            foreach ((string role, int i, string piece) in new[] { ("shell", 0, s1), ("shell", 1, s2), ("lining", 0, l1), ("lining", 1, l2) })
            {
                tailor.Wait(2);
                tailor.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = i, other = piece }, "fit " + role);
            }
            tailor.Wait(2);
            tailor.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "padding", other = "wool_padding", count = 3 }, "the batting");
            Sew(tailor, asm, "quilting", "linen_thread");
            Sew(tailor, asm, "seams", "linen_thread");
            g = CraftGuide.Next(tailor.W, tailor.W.Tools, tailor.P(asm), null);
            Console.WriteLine($"guide: {g?.text} [{g?.area}/{g?.place}]");
            tailor.Wait(1);
            tailor.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            CraftItemReport item = Tried(tailor, asm);
            Print("padded gambeson", item);
            Assert.Greater(item.Quality, 0.5, "honest work makes a decent gambeson");
            Assert.LessOrEqual(item.Defense, d.defense * 1.5, $"about the design's defence ({d.defense}), not {item.Defense:0.0}");
            double panels = new[] { s1, s2, l1, l2 }.Sum(id => tailor.P(id).Mass(R));
            Assert.Greater(item.Weight, (panels + 3 * R.Supply("wool_padding").ItemGrams) / 1000 - 0.01, "the three battings quilted into it weigh in it");
        }

        /// <summary>A batch of raw fibre made ready at the fibre bench, as its places send it: picked, scoured hot with soap, dried, teased, carded.</summary>
        private static void Prepare(Station s, string batch)
        {
            s.Wait(40);
            s.Must(new CraftRequest(CraftRequestKind.CleanFibre, batch) { a = 0.9f, b = 0.05f }, "pick");
            s.Wait(60);
            s.Must(new CraftRequest(CraftRequestKind.ScourFibre, batch) { a = 62f, b = 30f, c = 0.1f, d = 3f, id = "soap", i0 = 3 }, "scour");
            for (int k = 0; k < 60 && s.P(batch).fibre.wet > 0.3; k++) s.Wait(30); // hung to dry
            s.Wait(30);
            s.Must(new CraftRequest(CraftRequestKind.OpenFibre, batch) { a = 0.9f, b = 0.2f }, "tease");
            for (int k = 0; k < 3; k++)
            {
                s.Wait(4);
                s.Must(new CraftRequest(CraftRequestKind.CardFibre, batch) { count = 4, a = 0.3f, b = 0.9f }, "card");
            }
        }

        // ================================================================== a garment from the fleece: spinnery, then the tailor's
        [Test]
        public void Journey_WoolTunic_FromRawFleece_SpunWovenAndMadeToMeasure()
        {
            var bag = new Bag();
            bag.Add("wool_fibre", 5);
            bag.Add("soap", 5);
            var spin = new Station("spinnery", CraftTools.Spinnery, bag, 51);
            CraftRules R = spin.R;
            CraftDesign d = R.Design("wool_tunic");
            var guide = new List<string>();
            void Note(Station st, string id) { CraftGuide.Step? g = CraftGuide.Next(st.W, st.W.Tools, st.P(id), null); guide.Add(g.HasValue ? $"{st.Name}: {g.Value.text} [{g.Value.area}/{g.Value.place}]" : $"{st.Name}: (none)"); }
            // five batches laid on the bench from the bag, each made ready
            var batches = new List<string>();
            for (int k = 0; k < 5; k++)
            {
                Assert.IsTrue(bag.Use("wool_fibre", 1));
                batches.Add(spin.Put(CraftFibre.NewFibre(R, "wool_fibre")));
            }
            Note(spin, batches[0]);
            foreach (string b in batches) Prepare(spin, b);
            FibreState f0 = spin.P(batches[0]).fibre;
            Console.WriteLine($"a batch made ready: {f0.grams:0} g, grease {f0.grease:0.00} dirt {f0.dirt:0.00} wet {f0.wet:0.00} felted {f0.felted:0.00} prep {CraftFibre.Prep(f0):0.00}");
            Note(spin, batches[0]);
            // spin the batches into bobbins: an even draft at a middling fineness, the twist in its band
            var bobbins = new List<string>();
            foreach (string b in batches)
                while (spin.P(b) != null && spin.P(b).fibre.grams >= CraftFibre.BobbinGrams)
                {
                    spin.Wait(60);
                    spin.Must(new CraftRequest(CraftRequestKind.SpinFibre, b) { a = 0.85f, b = 0.5f, c = 0.5f, i0 = CraftFibre.BobbinGrams }, "spin a bobbin");
                    bobbins.Add(spin.W.Pieces.Last(p => p.kind == CraftPieceKind.Yarn).id);
                }
            YarnState y0 = spin.P(bobbins[0]).yarn;
            Console.WriteLine($"{bobbins.Count} bobbins: {y0.length:0} m each, evenness {y0.evenness:0.00} strength {y0.strength:0.00} quality {CraftFibre.Quality(y0):0.00} -> thread {CraftFibre.ThreadOf(R, spin.P(bobbins[0]))}");
            Note(spin, bobbins[0]);
            // the thread for the seams: one bobbin wound into a skein, counted, reeled off
            string skein = bobbins[bobbins.Count - 1];
            bobbins.RemoveAt(bobbins.Count - 1);
            double len = spin.P(skein).yarn.length;
            spin.Wait(90);
            spin.Must(new CraftRequest(CraftRequestKind.MeasureYarn, skein) { a = (float)(Math.Floor(len / CraftFibre.MetresPerWrap) * CraftFibre.MetresPerWrap) }, "wind and count");
            spin.Wait(3);
            spin.Must(new CraftRequest(CraftRequestKind.ReelYarn, skein), "reel off");
            Console.WriteLine($"reeled {spin.Reeled} x {spin.LastThread}");
            // the warp: every other bobbin wound onto the first, then a length 6 cells wide woven, a beat a row
            string warp = bobbins[0];
            for (int k = 1; k < bobbins.Count; k++)
            {
                spin.Wait(10);
                spin.Must(new CraftRequest(CraftRequestKind.JoinYarn, warp) { other = bobbins[k] }, "wind on");
            }
            Console.WriteLine($"warp: {spin.P(warp).yarn.length:0} m = {CraftFibre.RowsFor(spin.P(warp).yarn.length, 6)} rows at 6 wide");
            int rows = Math.Min(CraftFibre.RowsFor(spin.P(warp).yarn.length, 6), 17);
            var beats = new List<float>();
            for (int k = 0; k < rows; k++) beats.Add(0.82f + 0.06f * (k % 3) / 2f);
            spin.Wait(rows * 1.2 + 8); // tensioned, then a throw of the shuttle and a beat a row
            spin.Must(new CraftRequest(CraftRequestKind.WeaveCloth, warp) { i0 = 6, i1 = rows, a = 0.95f, b = 0.5f, points = beats }, "weave");
            string cloth = spin.W.Pieces.Last(p => p.kind == CraftPieceKind.Hide).id;
            HideState ch = spin.P(cloth).hide;
            Console.WriteLine($"woven: {ch.width} x {ch.height}, thick {Mean(ch, c => c.thickness):0.00} mm, flaws {ch.cells.Count(c => c.flaw)}, stiff {Mean(ch, c => c.stiff):0.00}");
            Note(spin, cloth);
            // the tailor's: measure, start, cut on the grain, fit, sew
            var tailor = new Station("tailor", CraftTools.Tailor, bag, 52);
            string bolt = spin.CarryTo(cloth, tailor);
            Note(tailor, bolt);
            tailor.Wait(15);
            tailor.Must(new CraftRequest(CraftRequestKind.MeasureFit, null) { a = 0.9f, b = (float)(CraftTextiles.ChestOf("wearer") + 0.1 * 60) }, "measure");
            tailor.Wait(2);
            tailor.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "wool_tunic" }, "start");
            string asm = tailor.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            string CutAt(string pattern, int x, int y)
            {
                tailor.Wait(25);
                tailor.Must(new CraftRequest(CraftRequestKind.Cut, bolt) { id = pattern, i0 = x, i1 = y, i2 = 0, a = 0.95f }, $"cut {pattern} at {x},{y}");
                return tailor.W.Pieces.Last(p => p.kind == CraftPieceKind.Hide && p.hide.pattern == pattern).id;
            }
            string b1 = CutAt("tunic_body", 0, 0), b2 = CutAt("tunic_body", 0, 6), s1 = CutAt("sleeve", 0, 12), s2 = CutAt("sleeve", 3, 12);
            foreach ((string role, int i, string piece) in new[] { ("body", 0, b1), ("body", 1, b2), ("sleeves", 0, s1), ("sleeves", 1, s2) })
            {
                tailor.Wait(2);
                tailor.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = i, other = piece }, "fit " + role);
            }
            Sew(tailor, asm, "sleeves", spin.LastThread);
            Sew(tailor, asm, "side_seams", spin.LastThread);
            Note(tailor, asm);
            tailor.Wait(1);
            tailor.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            foreach (string g in guide) Console.WriteLine("   guide: " + g);
            CraftItemReport item = Tried(tailor, asm);
            Print("wool tunic", item);
            Assert.Greater(item.Quality, 0.5, "honest work from the fleece makes a decent tunic");
            Assert.IsTrue(item.Notes.Any(n => n.Contains("made to measure")), "measured carefully first, it is made to measure");
            Assert.Greater(item.Protection, 0.6, "a tunic of sound hand-woven wool guards about as its design says (weighed against a cloth, not a leather)");
        }

        // ================================================================== gems: the lapidary, then the jeweller
        /// <summary>What the Orient place said the last stone cut by <see cref="CutGem"/> would come to (its expected yield, ct).</summary>
        private static double Promised;

        /// <summary>
        /// A rough stone cut at a Lapidary (gem bench, faceting machine; the gem kit), every place as its window sends it:
        /// brushed clean sector by sector, tested, named, searched under the loupe, marked out clear of what was found, sawn,
        /// ground to the outline with the drip on, each tier cut with a novice's hits, polished pass by pass. Returns the stone.
        /// </summary>
        private static string CutGem(Station lap, string stone, string cutId, string compound, List<string> guide, double minR = 0)
        {
            CraftRules R = lap.R;
            void Note() { CraftGuide.Step? g = CraftGuide.Next(lap.W, lap.W.Tools, lap.P(stone), null); guide.Add(g.HasValue ? $"{g.Value.text} [{g.Value.area}/{g.Value.place}]" : "(none)"); }
            GemState G() => lap.P(stone).gem;
            CraftGemDefinition d = R.Gem(G().gem);
            Note();
            // Clean: the brush, a steady even stroke, over each sector until it is in the green
            for (int pass = 0; pass < 30 && CraftLapidary.Dirt(G()) > 0.05; pass++)
            {
                var work = new List<float>();
                for (int k = 0; k < CraftLapidary.CrustSectors; k++) work.Add(G().crust[k] > 0.03f ? 0.6f : 0f);
                lap.Wait(2);
                lap.Must(new CraftRequest(CraftRequestKind.CleanGem, stone) { points = work, a = 0.85f, i0 = 0 }, "brush the crust");
            }
            Console.WriteLine($"cleaned: dirt {CraftLapidary.Dirt(G()):0.000}, chips {G().chips:0.00}");
            Note();
            // Test: the loupe, the points (5 to 9.5), the refractometer, the balance, the dichroscope
            lap.Wait(4); lap.Must(new CraftRequest(CraftRequestKind.TestGem, stone) { i0 = (int)CraftGemTest.Loupe, a = 0.9f }, "look");
            foreach (float pt in new[] { 5f, 6f, 6.5f, 7f, 7.5f, 8f, 9f, 9.5f })
            { lap.Wait(3); lap.Must(new CraftRequest(CraftRequestKind.TestGem, stone) { i0 = (int)CraftGemTest.Scratch, a = 0.8f, b = pt }, "scratch"); }
            lap.Wait(6); lap.Must(new CraftRequest(CraftRequestKind.TestGem, stone) { i0 = (int)CraftGemTest.Refractometer, a = 0.9f }, "refractometer");
            lap.Wait(8); lap.Must(new CraftRequest(CraftRequestKind.TestGem, stone) { i0 = (int)CraftGemTest.Gravity, a = 0.9f }, "balance");
            lap.Wait(6); lap.Must(new CraftRequest(CraftRequestKind.TestGem, stone) { i0 = (int)CraftGemTest.Dichroscope, a = 0.9f }, "dichroscope");
            Console.WriteLine($"readings: hardness {G().hardLo}-{G().hardHi}, RI {G().readRI}, SG {G().readSG}, pleo {G().readPleo:0.00} axis {G().readAxis:0}");
            lap.Wait(5);
            lap.Must(new CraftRequest(CraftRequestKind.IdentifyGem, stone) { id = G().gem }, "name it");
            Note();
            // Evaluate: the loupe moved over the stone at every depth, a hundred looks a second
            var looks = new List<float>();
            for (float z = 0.05f; z < 1f; z += 0.15f)
                for (float y = -0.9f; y <= 0.9f; y += 0.15f)
                    for (float x = -0.9f; x <= 0.9f; x += 0.15f) { looks.Add(x); looks.Add(y); looks.Add(z); }
            for (int i = 0; i < looks.Count; i += 300)
            {
                lap.Wait(3);
                lap.Must(new CraftRequest(CraftRequestKind.EvaluateGem, stone) { points = looks.GetRange(i, Math.Min(300, looks.Count - i)), a = 0.85f }, "search");
            }
            int found = G().inclusions.Count(i => i.found);
            Console.WriteLine($"searched: {found} of {G().inclusions.Count} inclusions found");
            Note();
            // Orient: the outline as big as it goes clear of what was found, the table across the axis (read)
            double bestR = 0; float bx = 0, by = 0;
            for (float wx = -0.3f; wx <= 0.3f; wx += 0.05f)
                for (float wy = -0.3f; wy <= 0.3f; wy += 0.05f)
                    for (float r = 0.8f; r >= 0.3f; r -= 0.02f)
                    {
                        if (r + Math.Sqrt(wx * wx + wy * wy) > 0.98) continue;
                        GemState g = G();
                        double len = Math.Max(1, R.Cut(cutId).lengthRatio);
                        bool clear = g.inclusions.Where(i => i.found).All(i =>
                        {
                            double a = r, b = r / len, sz = i.size * 0.5, dx = i.x - wx, dy = i.y - wy;
                            return dx * dx / ((a + sz) * (a + sz)) + dy * dy / ((b + sz) * (b + sz)) > 1;
                        });
                        if (clear && r > bestR) { bestR = r; bx = wx; by = wy; }
                        if (clear) break;
                    }
            // a stone too small for what it is meant for: a bigger outline, a flaw or two kept in it
            if (bestR < minR) { bestR = minR; bx = by = 0; }
            float table = (float)(d.pleochroism > 0.05 ? G().readAxis : G().axis);
            if (d.cleavage >= CraftCleavage.Good && Math.Abs(table - G().cleaveTilt) < 10) table = (float)Math.Min(90, G().cleaveTilt + 15);
            lap.Wait(10);
            lap.Must(new CraftRequest(CraftRequestKind.OrientGem, stone) { id = cutId, a = table, b = bx, c = by, d = (float)bestR }, "mark it out");
            Promised = CraftLapidary.Grade(R, lap.P(stone)).Yield; // the Orient place's "Expected about ... ct"
            Console.WriteLine($"oriented: window ({bx:0.00},{by:0.00}) r {bestR:0.00}, excess {CraftLapidary.Excess(G()):0.00}, planned {CraftLapidary.PlannedCarats(G(), R.Cut(cutId)):0.00} ct of {G().carats:0.00} (the window: expected about {Promised:0.00} ct)");
            Note();
            // Saw (or cleave) the bulk off: four cuts round the outline, the drip on, a light feed
            for (int k = 0; k < 8 && CraftLapidary.Excess(G()) > CraftLapidary.PreformExcess - 0.1; k++)
            {
                // a line is a line: 0-180 degrees, the offset on either side of the outline (the window draws it through two points)
                double ang = (k % 4) * 45, rad = ang * Math.PI / 180, nx = Math.Sin(rad), ny = -Math.Cos(rad);
                float off = (float)(nx * bx + ny * by + (k < 4 ? 1 : -1) * (CraftLapidary.WindowRadius(G()) + 0.08));
                if (Math.Abs(off) >= 0.95f) continue;
                lap.Wait(20);
                if (!lap.Send(new CraftRequest(CraftRequestKind.SawGem, stone) { a = (float)ang, b = off, c = 0.85f, d = 1f, i0 = 300, i1 = 0 })) Console.WriteLine("   saw: " + lap.Last);
                Console.WriteLine($"   saw {ang:0} at {off:0.00}: excess {CraftLapidary.Excess(G()):0.00} area {CraftLapidary.Area(G()):0.00} window r {CraftLapidary.WindowRadius(G()):0.00} carats {G().carats:0.00}");
            }
            Console.WriteLine($"sawn: excess {CraftLapidary.Excess(G()):0.00}, carats {G().carats:0.00}, chips {G().chips:0.00}");
            Note();
            // Grind to the outline: where it stands out, a touch at a time, the drip on, steady
            for (int pass = 0; pass < 80 && CraftLapidary.Excess(G()) > 0.08; pass++)
            {
                GemState g = G();
                List<float> prof = g.profile.Count == CraftLapidary.ProfileRadii ? g.profile : CraftLapidary.Outline(g);
                var t = new List<float>();
                for (int k = 0; k < CraftLapidary.ProfileRadii; k++)
                    if (prof[k] > CraftLapidary.TargetRadius(g, k) + 0.02) { t.Add(k * 360f / CraftLapidary.ProfileRadii); t.Add(0.15f); }
                if (t.Count == 0) break;
                lap.Wait(1.5);
                if (!lap.Send(new CraftRequest(CraftRequestKind.GrindGem, stone) { points = t, a = 1f, b = 0.85f })) { Console.WriteLine("   grind: " + lap.Last); break; }
            }
            Console.WriteLine($"ground: excess {CraftLapidary.Excess(G()):0.00} (ground {CraftLapidary.GroundExcess(G()):0.00}), heat {G().heat:0.00}, carats {G().carats:0.00}");
            Note();
            // Facet: each tier at its angle from the chart for the RI read, a novice's hits on the bar
            CraftGemCut cut = R.Cut(cutId);
            for (int t = 0; t < cut.tiers.Count; t++)
            {
                int hits = CraftMastery.FacetHits(cut.tiers[t].facets, 0, null);
                lap.Wait(hits * 0.9 + 1);
                double ri = G().readRI > 0 ? G().readRI : d.refractiveIndex;
                lap.Must(new CraftRequest(CraftRequestKind.FacetGem, stone) { i0 = t, count = cut.tiers[t].facets, a = (float)CraftLapidary.IdealAngle(cut.tiers[t], ri) + 0.1f, b = 0.08f, c = 0.05f, d = 1f, i1 = 0, i2 = 170 }, $"cut the {cut.tiers[t].name}");
            }
            Console.WriteLine($"faceted: stage {G().stage}, carats {G().carats:0.00}, girdle {G().girdle:0.00} mm, symmetry ...");
            Note();
            // Polish: passes of the lap, every region in turn, a light hand at a middling speed
            for (int pass = 0; pass < 60; pass++)
            {
                var where = new List<float>();
                for (int k = 0; k < CraftLapidary.PolishRegions; k++) where.Add(k == pass % CraftLapidary.PolishRegions ? 1.2f : 0.1f);
                lap.Wait(1.6);
                if (!lap.Send(new CraftRequest(CraftRequestKind.PolishGem, stone) { id = compound, count = 1, a = 0.3f, b = 0.5f, points = where })) { Console.WriteLine("   polish: " + lap.Last); break; }
                if (G().polishRegions.Count == CraftLapidary.PolishRegions && G().polishRegions.All(v => v >= 1f)) break;
            }
            CraftGemGrade grade = CraftLapidary.Grade(R, lap.P(stone));
            Console.WriteLine($"polished: stage {G().stage}, polish {G().polish:0.00}, regions {string.Join(",", G().polishRegions.Select(v => v.ToString("0.0")))}, carats {G().carats:0.00}, girdle {G().girdle:0.00}");
            Console.WriteLine($"grade: quality {grade.Quality:0.00} value {grade.Value:0.0} cut {grade.Cut:0.00} polish {grade.Polish:0.00} clarity {grade.Clarity:0.00}");
            Note();
            return stone;
        }

        [Test]
        public void Journey_BezelRing_WithACutGarnet_LapidaryThenJeweller()
        {
            var bag = new Bag();
            bag.Add("cerium_oxide", 1);
            bag.Add("gold_solder", 1);
            var lap = new Station("lapidary", CraftTools.Lapidary, bag, 61);
            CraftRules R = lap.R;
            var guide = new List<string>();
            string rough = lap.Put(CraftLapidary.NewRough(R, "garnet", 6161));
            Console.WriteLine($"rough garnet {lap.P(rough).gem.carats:0.00} ct, {lap.P(rough).gem.inclusions.Count} inclusions, dirt {CraftLapidary.Dirt(lap.P(rough).gem):0.00}");
            string stone = CutGem(lap, rough, "round_brilliant", "cerium_oxide", guide);
            foreach (string g in guide.Distinct()) Console.WriteLine("   guide: " + g);
            var jw = new Station("jeweller", CraftTools.Jeweller, bag, 62);
            string gem = lap.CarryTo(stone, jw);
            CraftPiece Gold((string suffix, float length, float width, float thickness) form) => CraftDefaults.Bar(R, form.length, form.width, form.thickness, CraftDefaults.StockMetals().First(m => m.name == "Gold").composition);
            string wire = jw.Put(Gold(CraftDefaults.JewelleryForms[0])), strip = jw.Put(Gold(CraftDefaults.JewelleryForms[1]));
            // Size the finger: the sizer's rings tried until one fits (a few tries)
            double finger = CraftJewelry.FingerOf("wearer");
            jw.Wait(12);
            jw.Must(new CraftRequest(CraftRequestKind.MeasureFit, null) { a = 0.85f, b = (float)(Math.Round(finger * 4) / 4) }, "size the finger");
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "bezel_ring" }, "start");
            string asm = jw.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            // Form the band to the finger and the bezel to the stone: tapped round, the seam closed
            jw.Wait(20);
            jw.Must(new CraftRequest(CraftRequestKind.FormShank, wire) { i0 = (int)CraftJewelPart.Band, a = (float)jw.P(asm).assembly.fitSize, b = 0.85f, c = 0.05f, i1 = 0 }, "form the band");
            string band = jw.W.Pieces.Last(x => x.kind == CraftPieceKind.Metal && CraftJewelry.PartOf(x) == CraftJewelPart.Band).id;
            jw.Wait(20);
            jw.Must(new CraftRequest(CraftRequestKind.FormShank, strip) { i0 = (int)CraftJewelPart.Bezel, a = (float)jw.P(gem).gem.girdle, b = 0.85f, c = 0.05f, i1 = 0 }, "form the bezel");
            string bezel = jw.W.Pieces.Last(x => x.kind == CraftPieceKind.Metal && CraftJewelry.PartOf(x) == CraftJewelPart.Bezel).id;
            Console.WriteLine($"band {jw.P(band).Length:0.0} mm (size {jw.P(band).Tag(CraftJewelry.SizeTag):0.00} for a {finger:0.00} finger), bezel {jw.P(bezel).Length:0.0} mm; wire left {jw.P(wire)?.Length ?? 0:0} mm");
            // Seat: the burr held still, let go in the green ring
            jw.Wait(6);
            jw.Must(new CraftRequest(CraftRequestKind.SeatSetting, bezel) { other = gem, a = (float)jw.P(gem).gem.girdle + 0.03f, b = 0.85f, c = 0.85f }, "seat");
            foreach ((string role, string piece) in new[] { ("band", band), ("setting", bezel) })
            { jw.Wait(2); jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = 0, other = piece }, "fit " + role); }
            // Solder: flux brushed along the seam, the torch held until the heat is in the green
            jw.Wait(3);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "solder", count = 1, other = "gold_solder" }, "solder in");
            jw.Wait(8);
            jw.Must(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "seat", i0 = (int)CraftJointWork.Solder, count = 1, a = 1.02f, b = 0.9f }, "solder the bezel on");
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "stone", i0 = 0, other = gem }, "the stone in its bezel");
            CraftJointSpec set = R.Design("bezel_ring").Joint("set");
            for (int k = 0; k < 40 && !CraftAssembly.JointDone(set, jw.P(asm).assembly.Joint("set")); k++)
            {
                jw.Wait(1.2);
                if (!jw.Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "set", i0 = (int)CraftJointWork.Push, count = 1, a = 0.55f, b = 0.85f })) { Console.WriteLine("   set: " + jw.Last); break; }
            }
            JointState sj = jw.P(asm).assembly.Joint("set");
            Console.WriteLine($"set: pushes {sj.pushes} security {sj.security:0.00} tilt {sj.tilt:0.00}; seat solder {jw.P(asm).assembly.Joint("seat").solder:0.00}");
            // Finish: on the buffing wheel, the steam jet, Inspect & finish
            jw.Wait(9);
            jw.Must(new CraftRequest(CraftRequestKind.FinishJewel, asm) { a = 0.9f, b = 0.9f }, "buff and clean");
            jw.Wait(1);
            jw.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            CraftItemReport item = Tried(jw, asm);
            Print("bezel ring", item);
            Assert.Greater(item.Quality, 0.5, "honest work makes a decent ring");
            Assert.IsTrue(CraftAssembly.JointDone(set, sj), "the Set place said: the stone is set, it holds");
            Assert.IsFalse(item.Notes.Any(n => n.Contains("loose")), $"set until the window said it holds (security {sj.security:0.00}), the ring must not call the stone loose: {string.Join("; ", item.Notes)}");
        }

        [Test]
        public void Journey_Pendant_WithATurquoiseCabochon_OnItsCord()
        {
            var bag = new Bag();
            bag.Add("tripoli", 1);
            bag.Add("silver_solder", 1);
            bag.Add("hemp_cord", 1);
            var lap = new Station("lapidary", CraftTools.Lapidary, bag, 71);
            CraftRules R = lap.R;
            var guide = new List<string>();
            string rough = lap.Put(CraftLapidary.NewRough(R, "turquoise", 7171));
            Console.WriteLine($"rough turquoise {lap.P(rough).gem.carats:0.00} ct, {lap.P(rough).gem.inclusions.Count} inclusions");
            string stone = CutGem(lap, rough, "cabochon_oval", "tripoli", guide, 0.72);
            double promise = Promised;
            foreach (string g in guide.Distinct()) Console.WriteLine("   guide: " + g);
            var jw = new Station("jeweller", CraftTools.Jeweller, bag, 72);
            string gem = lap.CarryTo(stone, jw);
            CraftPiece Silver((string suffix, float length, float width, float thickness) form) => CraftDefaults.Bar(R, form.length, form.width, form.thickness, CraftDefaults.StockMetals().First(m => m.name == "Sterling silver").composition);
            string wire = jw.Put(Silver(CraftDefaults.JewelleryForms[0])), strip = jw.Put(Silver(CraftDefaults.JewelleryForms[1]));
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "pendant" }, "start");
            string asm = jw.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            jw.Wait(15);
            jw.Must(new CraftRequest(CraftRequestKind.FormShank, wire) { i0 = (int)CraftJewelPart.Bail, a = 6f, b = 0.85f, c = 0.05f, i1 = 0 }, "form the bail");
            string bail = jw.W.Pieces.Last(x => x.kind == CraftPieceKind.Metal && CraftJewelry.PartOf(x) == CraftJewelPart.Bail).id;
            jw.Wait(20);
            jw.Must(new CraftRequest(CraftRequestKind.FormShank, strip) { i0 = (int)CraftJewelPart.Bezel, a = (float)jw.P(gem).gem.girdle, b = 0.85f, c = 0.05f, i1 = 0 }, "form the bezel");
            string bezel = jw.W.Pieces.Last(x => x.kind == CraftPieceKind.Metal && CraftJewelry.PartOf(x) == CraftJewelPart.Bezel).id;
            jw.Wait(6);
            jw.Must(new CraftRequest(CraftRequestKind.SeatSetting, bezel) { other = gem, a = (float)jw.P(gem).gem.girdle + 0.03f, b = 0.85f, c = 0.85f }, "seat");
            foreach ((string role, string piece) in new[] { ("bail", bail), ("setting", bezel) })
            { jw.Wait(2); jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = 0, other = piece }, "fit " + role); }
            jw.Wait(3);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "solder", count = 1, other = "silver_solder" }, "solder in");
            jw.Wait(8);
            jw.Must(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "bail", i0 = (int)CraftJointWork.Solder, count = 1, a = 1.02f, b = 0.9f }, "solder the bail on");
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "stone", i0 = 0, other = gem }, "the stone in its bezel");
            CraftJointSpec set = R.Design("pendant").Joint("set");
            for (int k = 0; k < 40 && !CraftAssembly.JointDone(set, jw.P(asm).assembly.Joint("set")); k++)
            {
                jw.Wait(1.2);
                if (!jw.Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "set", i0 = (int)CraftJointWork.Push, count = 1, a = 0.55f, b = 0.85f })) { Console.WriteLine("   set: " + jw.Last); break; }
            }
            CraftGuide.Step? gs = CraftGuide.Next(jw.W, jw.W.Tools, jw.P(asm), null);
            Console.WriteLine($"set: security {jw.P(asm).assembly.Joint("set").security:0.00}; guide: {gs?.text} [{gs?.area}/{gs?.place}]");
            // the Finish place: the cord threaded on (the first thread in the bag), buffed, steamed, Inspect & finish
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "cord", count = 1, other = "hemp_cord" }, "thread the cord on");
            jw.Wait(9);
            jw.Must(new CraftRequest(CraftRequestKind.FinishJewel, asm) { a = 0.9f, b = 0.9f }, "buff and clean");
            jw.Wait(1);
            jw.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            CraftItemReport item = Tried(jw, asm);
            Print("pendant", item);
            Assert.Greater(item.Quality, 0.5, "honest work makes a decent pendant");
            Assert.IsFalse(item.Notes.Any(n => n.Contains("loose")), $"set until the window said it holds: {string.Join("; ", item.Notes)}");
            Assert.Greater(jw.P(gem).gem.carats, promise * 0.85, $"the Orient place said about {promise:0.00} ct; ground to its outline as the guide says, it came to {jw.P(gem).gem.carats:0.00} ct");
        }

        [Test]
        public void Journey_GemCharm_ACabochonCagedInClaws_OnACord()
        {
            var bag = new Bag();
            bag.Add("tripoli", 1);
            bag.Add("silver_solder", 1);
            bag.Add("linen_thread", 1);
            var lap = new Station("lapidary", CraftTools.Lapidary, bag, 81);
            CraftRules R = lap.R;
            var guide = new List<string>();
            string rough = lap.Put(CraftLapidary.NewRough(R, "turquoise", 8181));
            Console.WriteLine($"rough turquoise {lap.P(rough).gem.carats:0.00} ct");
            string stone = CutGem(lap, rough, "cabochon_round", "tripoli", guide, 0.75);
            var jw = new Station("jeweller", CraftTools.Jeweller, bag, 82);
            string gem = lap.CarryTo(stone, jw);
            CraftPiece Silver((string suffix, float length, float width, float thickness) form) => CraftDefaults.Bar(R, form.length, form.width, form.thickness, CraftDefaults.StockMetals().First(m => m.name == "Sterling silver").composition);
            string wire = jw.Put(Silver(CraftDefaults.JewelleryForms[0])), wire2 = jw.Put(Silver(CraftDefaults.JewelleryForms[0]));
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "charm" }, "start");
            string asm = jw.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            CraftDesign d = R.Design("charm");
            int claws = CraftJewelry.ClawsWanted(d, d.Component("setting"));
            jw.Wait(15);
            jw.Must(new CraftRequest(CraftRequestKind.FormShank, wire) { i0 = (int)CraftJewelPart.Bail, a = 6f, b = 0.85f, c = 0.05f, i1 = 0 }, "form the bail");
            string bail = jw.W.Pieces.Last(x => x.kind == CraftPieceKind.Metal && CraftJewelry.PartOf(x) == CraftJewelPart.Bail).id;
            jw.Wait(20);
            jw.Must(new CraftRequest(CraftRequestKind.FormShank, wire2) { i0 = (int)CraftJewelPart.Prongs, a = (float)jw.P(gem).gem.girdle, b = 0.85f, c = 0.05f, i1 = claws }, "form the claw cage");
            string cage = jw.W.Pieces.Last(x => x.kind == CraftPieceKind.Metal && CraftJewelry.PartOf(x) == CraftJewelPart.Prongs).id;
            jw.Wait(6);
            jw.Must(new CraftRequest(CraftRequestKind.SeatSetting, cage) { other = gem, a = (float)jw.P(gem).gem.girdle + 0.03f, b = 0.85f, c = 0.85f }, "seat");
            foreach ((string role, string piece) in new[] { ("bail", bail), ("setting", cage) })
            { jw.Wait(2); jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = 0, other = piece }, "fit " + role); }
            jw.Wait(3);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "solder", count = 1, other = "silver_solder" }, "solder in");
            jw.Wait(8);
            jw.Must(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "bail", i0 = (int)CraftJointWork.Solder, count = 1, a = 1.02f, b = 0.9f }, "solder the bail on");
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "stone", i0 = 0, other = gem }, "the stone in its cage");
            CraftJointSpec set = d.Joint("set");
            for (int k = 0; k < 60 && !CraftAssembly.JointDone(set, jw.P(asm).assembly.Joint("set")); k++)
            {
                jw.Wait(1.2);
                if (!jw.Send(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "set", i0 = (int)CraftJointWork.Push, count = 1, a = 0.55f, b = 0.85f })) { Console.WriteLine("   set: " + jw.Last); break; }
            }
            JointState sj = jw.P(asm).assembly.Joint("set");
            Console.WriteLine($"set: pushes {sj.pushes} security {sj.security:0.00} (most {CraftJewelry.MostSecure(jw.P(cage), jw.P(gem)):0.00})");
            jw.Wait(2);
            jw.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "cord", count = 1, other = "linen_thread" }, "thread the cord on");
            jw.Wait(9);
            jw.Must(new CraftRequest(CraftRequestKind.FinishJewel, asm) { a = 0.9f, b = 0.9f }, "buff and clean");
            jw.Wait(1);
            jw.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            CraftItemReport item = Tried(jw, asm);
            Print("gem charm", item);
            Assert.Greater(item.Quality, 0.5, "honest work makes a decent charm");
            Assert.IsFalse(item.Notes.Any(n => n.Contains("loose")), $"set until the window said it holds (security {sj.security:0.00}): {string.Join("; ", item.Notes)}");
        }

        // ================================================================== a bow from the green log
        [Test]
        public void Journey_HuntingBow_FromAGreenYewLog_DebarkedSplitSeasonedCarved()
        {
            var bag = new Bag();
            bag.Add("hemp_cord", 1);
            bag.Add("beeswax", 1);
            var shop = new Station("woodshop", CraftTools.Woodshop, bag, 91);
            CraftRules R = shop.R;
            var guide = new List<string>();
            string log = shop.Put(CraftDefaults.Log(R, "yew"));
            void Note(string id) { CraftGuide.Step? g = CraftGuide.Next(shop.W, shop.W.Tools, shop.P(id), null); guide.Add(g.HasValue ? $"{g.Value.text} [{g.Value.area}/{g.Value.place}]" : "(none)"); }
            CraftPiece L = shop.P(log);
            Console.WriteLine($"yew log {L.Length:0} x {L.segments[0].width:0} mm, moisture {L.wood.moisture:0.00}, bark {L.wood.bark:0.00}");
            Note(log);
            // Debark at the shaving horse: two pulls a face along the whole log, four faces (turned between)
            for (int face = 0; face < 4 && CraftWoodwork.BarkLeft(shop.P(log)) > 0.001; face++)
            {
                for (int pull = 0; pull < 2; pull++)
                {
                    shop.Wait(2.5);
                    if (!shop.Send(new CraftRequest(CraftRequestKind.DebarkWood, log) { i0 = 0, i1 = L.segments.Count - 1, a = 0.45f, b = 0.85f, c = (float)CraftWoodwork.DebarkPerStroke })) Console.WriteLine("   debark: " + shop.Last);
                }
                shop.Wait(1); // the log turned a face
            }
            Console.WriteLine($"debarked: bark {CraftWoodwork.BarkLeft(shop.P(log)):0.000}, gouged {shop.P(log).wood.gouged:0.00}");
            Note(log);
            // Split at the chopping block: the wedge through the heart along a ray, driven home in a few full blows - until a billet is left
            string piece = log;
            for (int k = 0; k < 8 && CraftWoodwork.SplitsWithWedges(shop.P(piece), out _); k++)
            {
                shop.Wait(8);
                shop.Must(new CraftRequest(CraftRequestKind.SplitLog, piece) { a = 0.04f, b = 0.9f, c = 0.9f, i0 = 4 }, "split");
                CraftPiece q = shop.P(piece);
                Console.WriteLine($"   split: {q.segments.Average(x => x.width):0} x {q.segments.Average(x => x.thickness):0} mm, runout {q.wood.runout:0.00}");
            }
            Note(piece);
            // Rive it to a stave with the froe if still too wide, else season it
            CraftPiece st = shop.P(piece);
            if (Math.Max(st.segments.Average(x => x.width), st.segments.Average(x => x.thickness)) > 40)
            {
                shop.Wait(12);
                shop.Must(new CraftRequest(CraftRequestKind.Rive, piece) { a = 0.08f, b = 0.85f, c = 0.5f }, "rive");
                Console.WriteLine($"   rived: {st.segments.Average(x => x.width):0} x {st.segments.Average(x => x.thickness):0} mm, runout {st.wood.runout:0.00}");
            }
            // Season in the stack: stickers even, ends sealed, the cover over it
            shop.Wait(10);
            shop.Must(new CraftRequest(CraftRequestKind.StackCover) { a = 1f }, "the cover");
            shop.Must(new CraftRequest(CraftRequestKind.StackWood, piece) { a = 0.9f, b = 0.9f, c = 1f }, "stack it");
            Note(piece);
            int hours = 0;
            while (shop.P(piece).wood.moisture > CraftWoodwork.DryEnough - 0.02 && hours < 400) { shop.Hours(4); hours += 4; }
            Console.WriteLine($"seasoned {hours} h: moisture {st.wood.moisture:0.00}, checks {st.wood.checks:0.00}, rot {st.wood.rot:0.00}");
            shop.Must(new CraftRequest(CraftRequestKind.Move, piece) { location = CraftLocation.Air }, "off the stack");
            Note(piece);
            // Carve: for the bow's stave, sawn to 1450 mm, shaved to the dashed 30 mm
            shop.Must(new CraftRequest(CraftRequestKind.SetIntent, piece) { id = "hunting_bow", other = "stave" });
            shop.Wait(8);
            shop.Must(new CraftRequest(CraftRequestKind.SawCut, piece) { a = (float)(1450 / st.Length), b = 0.05f, c = 0.95f }, "saw to length");
            int strokes = 0;
            while (st.segments.Average(x => x.thickness) > 30.6 && strokes < 400) { shop.Wait(0.55); shop.Must(Knife(st, CraftSurface.CarveCut.Thickness), "shave"); strokes++; }
            while (st.segments.Average(x => x.width) > 30.6 && strokes < 800) { shop.Wait(0.55); shop.Must(Knife(st, CraftSurface.CarveCut.Width), "shave"); strokes++; }
            foreach (float grit in new[] { 0.25f, 0.55f, 0.85f })
                for (int k = 0; k < 12; k++) { shop.Wait(0.4); shop.Must(new CraftRequest(CraftRequestKind.SandWood, piece) { i0 = 0, i1 = st.segments.Count - 1, a = grit, b = 1.5f, c = 0.95f }, "sand"); }
            shop.Wait(1);
            shop.Must(new CraftRequest(CraftRequestKind.Varnish, piece) { id = "beeswax", i0 = 0, i1 = st.segments.Count - 1, a = 0.85f, b = 0.4f }, "wax");
            CraftPieceReport pr = CraftEvaluator.Piece(R, st);
            Console.WriteLine($"stave: {st.Length:0} x {st.segments.Average(x => x.width):0.0} x {st.segments.Average(x => x.thickness):0.0}, shape {pr.Shape:0.00}, runout {st.wood.runout:0.00}, split {st.wood.split:0.00}, gouged {st.wood.gouged:0.00}, defects {string.Join("; ", pr.Defects)}");
            shop.Wait(2);
            shop.Must(new CraftRequest(CraftRequestKind.StartAssembly) { id = "hunting_bow" }, "start");
            string asm = shop.W.Pieces.First(x => x.kind == CraftPieceKind.Assembly).id;
            shop.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "stave", i0 = 0, other = piece }, "fit the stave");
            shop.Wait(2);
            shop.Must(new CraftRequest(CraftRequestKind.Attach, asm) { id = "string", other = "hemp_cord", count = 1 }, "the bowstring");
            for (int k = 0; k < 10 && !CraftAssembly.JointComplete(R.Design("hunting_bow").Joint("strung"), shop.P(asm).assembly.Joint("strung")); k++)
            { shop.Wait(0.8); shop.Must(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "strung", i0 = (int)CraftJointWork.Wrap, count = 1, i1 = 0, a = 0.65f, b = 0.05f }, "string it"); }
            shop.Wait(1);
            shop.Must(new CraftRequest(CraftRequestKind.Finish, asm), "finish");
            foreach (string g in guide) Console.WriteLine("   guide: " + g);
            CraftItemReport item = Tried(shop, asm);
            Print("hunting bow from the log", item);
            Assert.Greater(item.Quality, 0.5);
        }

        // ================================================================== focused proofs of what the journeys met
        /// <summary>
        /// A stone pushed over one push at a time, as the Set place sends them, until the window says "The stone is set: it
        /// holds" is not called loose by the finished piece: the Set place, its green and the evaluator read one line
        /// (<see cref="CraftJewelry.SetSecure"/>).
        /// </summary>
        [Test]
        public void Setting_AStoneTheSetPlaceSaysHolds_IsNotCalledLooseByTheFinishedPiece()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Lapidary | CraftTools.Jeweller, 18, 5, 0, "water", "set");
            string id = w.AddPiece(CraftLapidary.NewRough(w.Rules, "garnet", 9, 4));
            w.Piece(id).gem.inclusions.Clear();
            Assert.IsTrue(w.OrientGem(id, "round_brilliant", w.Piece(id).gem.axis, 0, 0, 0.75, out string why), why);
            CraftGemCut cut = w.Rules.Cut("round_brilliant");
            for (int t = 0; t < cut.tiers.Count; t++)
                Assert.IsTrue(w.FacetGem(id, t, cut.tiers[t].facets, CraftLapidary.IdealAngle(cut.tiers[t], 1.79), 0.05, 0, 1, out why), why);
            Assert.IsTrue(w.PolishGem(id, "cerium_oxide", 14, 0.25, 0.5, out why), why);
            CraftPiece stone = w.Piece(id);
            (string, float, float, float) wireForm = CraftDefaults.JewelleryForms[0], stripForm = CraftDefaults.JewelleryForms[1];
            var gold = CraftDefaults.StockMetals().First(m => m.name == "Gold").composition;
            string band = w.AddPiece(CraftDefaults.Bar(w.Rules, wireForm.Item2, wireForm.Item3, wireForm.Item4, gold));
            string bezel = w.AddPiece(CraftDefaults.Bar(w.Rules, stripForm.Item2, stripForm.Item3, stripForm.Item4, gold));
            Assert.IsTrue(w.FormShank(band, CraftJewelPart.Band, 18, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.FormShank(bezel, CraftJewelPart.Bezel, stone.gem.girdle, 0.9, 0.05, 0, out why), why);
            Assert.IsTrue(w.SeatSetting(bezel, stone.id, stone.gem.girdle + 0.02, 0.9, 0.9, out why), why);
            string asm = w.StartAssembly("bezel_ring", out why);
            Assert.IsTrue(w.Attach(asm, "band", 0, band, out why), why);
            Assert.IsTrue(w.Attach(asm, "setting", 0, bezel, out why), why);
            Assert.IsTrue(w.Attach(asm, "solder", 0, null, out why, 1, 1), why);
            Assert.IsTrue(w.JointWork(asm, "seat", CraftJointWork.Solder, 1, 1.0, 0.9, out why), why);
            Assert.IsTrue(w.Attach(asm, "stone", 0, stone.id, out why), why);
            CraftJointSpec set = w.Rules.Design("bezel_ring").Joint("set");
            // one push at a time, as the Set place sends them, until it says the stone holds
            for (int k = 0; k < 60 && !CraftAssembly.JointDone(set, w.Piece(asm).assembly.Joint("set")); k++)
                Assert.IsTrue(w.JointWork(asm, "set", CraftJointWork.Push, 1, 0.55, 0.85, out why), why);
            double security = w.Piece(asm).assembly.Joint("set").security;
            Assert.IsTrue(w.FinishJewel(asm, 0.9, 0.9, out why), why);
            Assert.IsTrue(w.Finish(asm, out why), why);
            CraftItemReport r = CraftEvaluator.Item(w.Rules, w, w.Piece(asm));
            Console.WriteLine($"set to security {security:0.000}: {string.Join("; ", r.Notes)}");
            Assert.IsFalse(r.Notes.Any(n => n.Contains("loose")), $"the Set place said it holds (security {security:0.000}); the ring says: {string.Join("; ", r.Notes)}");
        }

        /// <summary>
        /// Green wood seasoned in the best stack the Dry place lays (stickers even, both ends sealed, the cover over it all:
        /// "Stacked well ... Now it seasons slowly") dries slower than it checks: a green yew, oak or ash billet as green as
        /// the stock log comes out sound.
        /// </summary>
        [Test]
        public void Seasoning_TheBestStackTheWindowLays_DoesNotCheckAGreenBillet()
        {
            var bad = new List<string>();
            foreach (string wood in new[] { "yew", "ash", "oak" })
            {
                var w = new CraftWorkshop(Rules(), CraftTools.Woodshop, 18, 5, 0, "water", "season");
                string billet = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, wood, 1450, 32, 32));
                w.Piece(billet).wood.moisture = 0.6; // as green as the stock log
                Assert.IsTrue(w.SetStackCover(1, out string why), why);
                Assert.IsTrue(w.StackWood(billet, 1, 1, 1, out why), why);
                int hours = 0;
                while (w.Piece(billet).wood.moisture > CraftWoodwork.DryEnough && hours < 600) { w.RunSteps((int)(4 / w.Rules.Settings.processHoursPerSecond / w.Rules.Settings.stepSeconds)); hours += 4; }
                double checks = w.Piece(billet).wood.checks;
                Console.WriteLine($"{wood}: seasoned to {w.Piece(billet).wood.moisture:0.00} in {hours} h, checks {checks:0.00}");
                if (checks > 0.05) bad.Add($"{wood}: checks {checks:0.00} in the best stack");
            }
            Assert.IsEmpty(bad, string.Join("; ", bad));
        }

        // ================================================================== sweeps: every leather and cloth design, made well
        /// <summary>An ideal part of a leather or cloth spec: the stock hide or bolt that suits it best, tanned through and dried, cut clean.</summary>
        private static string IdealHidePart(CraftWorkshop w, CraftComponentSpec c, int seed, out string from)
        {
            CraftRules R = w.Rules;
            CraftLeatherSpec l = c.leather;
            bool cloth = l.fabric == CraftFabric.Cloth;
            double mid = (l.thicknessMin + l.thicknessMax) * 0.5;
            string best = null;
            double bestScore = double.MaxValue;
            (int x, int y, int rot) at = (0, 0, 0);
            foreach (CraftHideDefinition hd in R.Hides)
            {
                if (hd.textile != cloth) continue;
                CraftPiece trial = CraftLeather.NewHide(R, hd.id, seed);
                foreach (HideCell cell in trial.hide.cells) { cell.flaw = false; cell.damage = 0; cell.scar = 0; }
                var place = Place(R, trial.hide, l.pattern, l.thicknessMin, l.thicknessMax);
                if (!place.HasValue) continue;
                if (cloth && place.Value.rot != 0)
                {
                    // cloth is cut on the grain: the pattern unturned
                    CraftPattern pat = R.Pattern(l.pattern);
                    pat.Size(0, out int pw, out int ph);
                    if (pw > trial.hide.width || ph > trial.hide.height) continue;
                    place = (0, 0, 0);
                }
                double t = cloth ? (hd.thicknessBack + hd.thicknessBelly) * 0.5 : mid;
                double score = cloth ? Math.Abs(t - mid) + (t < l.thicknessMin || t > l.thicknessMax ? 10 : 0) : 0;
                if (!cloth)
                {
                    // the placement's mean thickness against the spec
                    CraftPattern pat = R.Pattern(l.pattern);
                    pat.Size(place.Value.rot, out int pw, out int ph);
                    double sum = 0; int n = 0;
                    for (int py = 0; py < ph; py++)
                        for (int px = 0; px < pw; px++)
                            if (pat.Covers(px, py, place.Value.rot)) { sum += CraftLeather.CellThickness(trial.hide.Cell(place.Value.x + px, place.Value.y + py)); n++; }
                    double mean = n > 0 ? sum / n : 0;
                    score = (mean < l.thicknessMin ? l.thicknessMin - mean : mean > l.thicknessMax ? mean - l.thicknessMax : 0) * 10 + Math.Abs(mean - mid) * 0.1;
                }
                if (score < bestScore) { bestScore = score; best = hd.id; at = place.Value; }
            }
            Assert.IsNotNull(best, $"{c.role}: no stock hide or cloth takes the {l.pattern} pattern");
            from = best;
            CraftPiece hide = CraftLeather.NewHide(R, best, seed);
            CraftHideDefinition def = R.Hide(best);
            foreach (HideCell cell in hide.hide.cells)
            {
                cell.flaw = false; cell.damage = 0; cell.scar = 0;
                if (cloth) continue;
                cell.flesh = 0; cell.hair = 0; cell.tanS = cell.tanC = 1; cell.moisture = 0.1;
                // as supple as the part wants (the middle of its range)
                double want = (l.suppleMin + l.suppleMax) * 0.5, t = CraftLeather.CellThickness(cell);
                cell.stiff = CraftMath.Clamp01(1 - (want - def.suppleness * 0.4 + Math.Max(0, t - 3) * 0.05) / 0.6);
            }
            if (!cloth) { hide.hide.dried = true; hide.hide.agent = "bark"; }
            string id = w.AddPiece(hide);
            string part = w.CutPattern(id, l.pattern, at.x, at.y, at.rot, 1, out string why);
            Assert.IsNotNull(part, $"{c.role}: {why}");
            return part;
        }

        /// <summary>
        /// Every design made only of leather, cloth, fasteners and supplies, made as well as the hands make it (ideal parts from
        /// the stock hides and bolts, every joint complete and true, measured where it is made to measure) is what its design
        /// says: it guards about as its Defense (never more than half again it), its protection is no worse than good, and a
        /// Superior or Masterwork piece lasts most of its design's durability.
        /// </summary>
        [Test]
        public void Sweep_EveryLeatherAndClothDesign_MadeWell_IsWhatItsDesignSays()
        {
            CraftRules R = Rules();
            var bad = new List<string>();
            int seed = 500;
            foreach (CraftDesign d in R.Designs)
            {
                if (d.kind == CraftDesignKind.Jewelry || d.IsWeaponLike) continue;
                bool ok = d.components.Count > 0;
                foreach (CraftComponentSpec c in d.components)
                    if (!c.supply && c.kind != CraftPieceKind.Hide && c.kind != CraftPieceKind.Fasteners && !c.optional) ok = false;
                if (!ok) continue;
                var w = new CraftWorkshop(R, CraftTools.Everything, 18, seed, 3, "water", "sweep");
                bool cloth = d.components.Exists(c => c.kind == CraftPieceKind.Hide && c.leather != null && c.leather.fabric == CraftFabric.Cloth);
                Assert.IsTrue(w.MeasureFit(null, 1, CraftTextiles.ChestOf("wearer"), out string why), why);
                string asm = w.StartAssembly(d.id, out why);
                Assert.IsNotNull(asm, $"{d.id}: {why}");
                var froms = new List<string>();
                foreach (CraftComponentSpec c in d.components)
                {
                    if (c.optional) continue;
                    if (c.supply)
                    {
                        // the strongest thread for leather (silk: it holds the thickest sole), the plain linen for cloth; wool batting
                        double strength = c.supplyKind == CraftSupplyKind.Thread ? (cloth ? 1 : 1.5) : 1;
                        Assert.IsTrue(w.Attach(asm, c.role, 0, null, out why, Math.Max(1, c.supplyCount), strength), $"{d.id} {c.role}: {why}");
                        continue;
                    }
                    for (int i = 0; i < Math.Max(1, c.count); i++)
                    {
                        string part;
                        if (c.kind == CraftPieceKind.Hide) { part = IdealHidePart(w, c, ++seed, out string from); froms.Add($"{c.role}<{from}"); }
                        else
                        {
                            int n = Math.Max(1, c.fastenerCount);
                            part = w.AddPiece(CraftDefaults.FastenerSet(R, c.fastener, n, c.fastener == CraftFastenerKind.Buckle ? 30 : 4, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002)));
                        }
                        Assert.IsTrue(w.Attach(asm, c.role, i, part, out why), $"{d.id} {c.role}[{i}]: {why}");
                    }
                }
                foreach (CraftJointSpec js in d.joints)
                    for (int k = 0; k < 400 && !CraftAssembly.JointComplete(js, w.Piece(asm).assembly.Joint(js.id)); k++)
                    {
                        JointState j = w.Piece(asm).assembly.Joint(js.id);
                        bool riveted = js.kind == CraftJointKind.Rivet || js.kind == CraftJointKind.Buckle && CraftAssembly.RivetPart(d, js) != null;
                        bool done;
                        if (riveted)
                            done = CraftAssembly.NeedsHoles(w, w.Piece(asm), d, js) && j.holes < j.rivets + 1
                                ? w.JointWork(asm, js.id, CraftJointWork.Punch, 1, 0.05, 0.05, out why)
                                : w.JointWork(asm, js.id, CraftJointWork.Rivet, 1, CraftAssembly.IdealCharge(w, w.Piece(asm), js, CraftJointWork.Rivet) * 4, 0, out why);
                        else if (js.kind == CraftJointKind.Wrap)
                            done = w.JointWork(asm, js.id, CraftJointWork.Wrap, 1, 0.65, 0.05, out why);
                        else
                            done = j.holes <= j.stitches
                                ? w.JointWork(asm, js.id, CraftJointWork.Punch, 4, 0.05, 0.05, out why)
                                : w.JointWork(asm, js.id, CraftJointWork.Stitch, 4, 0.6, 0.05, out why);
                        if (!done) { bad.Add($"{d.id}: {js.id}: {why}"); break; }
                    }
                if (!w.Finish(asm, out why)) { bad.Add($"{d.id}: cannot finish: {why}"); continue; }
                CraftItemReport r = CraftEvaluator.Item(R, w, w.Piece(asm));
                Console.WriteLine($"{d.id,-20} q={r.Quality:0.00} {r.Tier,-9} prot={r.Protection:0.00} def={r.Defense,5:0.0}/{d.defense,-4} dur={r.Durability,4}/{d.durability,-4} {r.Weight:0.000} kg price {r.Price:0.0}/{d.price} [{string.Join(" ", froms.Distinct())}] {string.Join("; ", r.Notes)}");
                if (d.defense > 0 && r.Defense > d.defense * 1.5) bad.Add($"{d.id}: defence {r.Defense:0.0}, its design {d.defense}");
                if (d.defense > 0 && r.Protection < 0.6) bad.Add($"{d.id}: protection {r.Protection:0.00} made well");
                if (r.Quality >= 0.9 && r.Durability < d.durability * 0.75) bad.Add($"{d.id}: {r.Tier} (q {r.Quality:0.00}) yet durability {r.Durability} of its design's {d.durability}");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        /// <summary>
        /// A seasoned yew blank meant for the hunting bow's stave (its intent set at Carve) is 1900 x 40 x 40 mm; the stave wants
        /// 1300-1600 mm and 26-34 mm across: the guide names the next step for it (saw it to length, then carve it to its dashed
        /// shape), so the window's guide row is not empty at the step the bow hangs on.
        /// </summary>
        [Test]
        public void Guide_AWoodenPartMeantForAnItem_IsToldToSawAndCarveItToItsShape()
        {
            var w = new CraftWorkshop(Rules(), CraftTools.Woodshop, 18, 5, 0, "water", "guide-wood");
            string stave = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "yew"));
            Assert.IsTrue(w.SetIntent(stave, "hunting_bow", "stave", out string why), why);
            CraftPiece p = w.Piece(stave);
            CraftComponentSpec spec = w.Rules.Design("hunting_bow").Component("stave");
            CraftGuide.Step? g = CraftGuide.Next(w, w.Tools, p, null);
            Console.WriteLine($"yew blank {p.Length:0} x {p.segments[0].width:0} x {p.segments[0].thickness:0} mm for the stave ({spec.wood.lengthMin:0}-{spec.wood.lengthMax:0} mm, {spec.wood.diameterMin:0}-{spec.wood.diameterMax:0} across): guide '{g?.text}' [{g?.area}/{g?.place}]");
            Assert.IsTrue(p.Length > spec.wood.lengthMax, "the blank is longer than the stave");
            Assert.IsTrue(g.HasValue, "a piece meant for a part and far from its shape is told what comes next (saw it to length, carve it to the dashed shape)");
        }
    }
}
#endif
