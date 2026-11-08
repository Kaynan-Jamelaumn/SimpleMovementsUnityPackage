#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Whole journeys from raw default stock to a finished, tried metal item, at real stations (the guard on, a novice's
    /// hands, the window's pace), with requests shaped as the stations' windows send them - and the rules they showed
    /// (the review of the metal journeys; its fixes' own tests: <see cref="CraftReviewJourneysMetalTests"/>). The smith
    /// here does what the windows say: gives a bar its purpose at the anvil (For), fills the tank with oil when the quench
    /// says water is harsh on the steel, lets a glaring melt come down before it is poured.
    /// </summary>
    public class CraftMetalJourneyTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>A stock bar from the stores, as CraftItems.Stock makes it: 200 x 30 x 6 mm.</summary>
        internal static CraftPiece StockBar(CraftRules rules, string metal)
        {
            CraftPiece p = CraftDefaults.Bar(rules, 200, 30, 6, Metal(metal));
            p.origin = metal + " bar";
            return p;
        }

        // ================================================================== a real station, as CraftStation.Request runs it
        /// <summary>
        /// One player's work at a real (not test) station: CraftStation.Request's order (shape, test-station-only, the forced
        /// open fold, the guard, practice, the request, the refund) with the player's bag of supplies; real time passes with
        /// <see cref="Wait"/> (the station advances its workshop by the real time).
        /// </summary>
        internal sealed class Bench
        {
            public readonly CraftWorkshop W;
            public readonly CraftRequestGuard Guard;
            public readonly CraftTools Tools;
            public readonly Dictionary<string, int> Bag;
            public readonly CraftHands Hands;
            public double Now = 100;
            public double Skill;
            public readonly List<string> Refused = new List<string>();
            public readonly List<string> Partial = new List<string>();
            public string Name;
            /// <summary>The station's Free Fuel (the field's default: a woodpile and a charcoal heap).</summary>
            public bool FreeFuel = true;
            public double Pace => W.Rules.Settings.handWorkPace * (W.Rules.Settings.windowPaceNovice + (W.Rules.Settings.windowPaceMaster - W.Rules.Settings.windowPaceNovice) * Skill);

            public Bench(string name, CraftTools tools, Dictionary<string, int> bag, int seed = 1, string quench = "water")
            {
                Name = name;
                Tools = tools;
                W = new CraftWorkshop(Rules(), tools, 18, seed, (tools & CraftTools.Vats) != 0 ? 3 : 0, quench, name);
                Guard = new CraftRequestGuard(seed * 7919 + 17, 0);
                Guard.Touch(Now);
                Bag = bag ?? new Dictionary<string, int>();
                Hands = new CraftHands(seed * 31 + 5, 0);
            }

            public int Have(string id) => Bag.TryGetValue(id, out int n) ? n : 0;

            private bool HaveSupply(string id, int count, out string reason)
            {
                reason = "";
                int have = Have(id);
                if (have >= count) return true;
                reason = have > 0 ? $"You have {have} {id}: {count} are needed." : $"You have no {id}.";
                return false;
            }

            private bool UseSupply(string id, int count, out string reason)
            {
                reason = "";
                if (string.IsNullOrEmpty(id)) return true;
                if (Have(id) < count) { reason = $"You have no {id}."; return false; }
                Bag[id] = Have(id) - count;
                return true;
            }

            /// <summary>Real time passes: the arm rests, the guard's allowances refill, the workshop runs.</summary>
            public void Wait(double seconds)
            {
                if (seconds <= 0) return;
                Now += seconds;
                Hands.Rest(seconds);
                double left = seconds;
                while (left > 1e-9)
                {
                    double d = Math.Min(left, 100);
                    W.Advance(d, 5000);
                    left -= d;
                }
            }

            public bool Do(CraftRequest r) => Do(r, out _);

            public bool Do(CraftRequest r, out string reason)
            {
                CraftRequestGuard.Fit(ref r, out string trimmed);
                if (!CraftRequestGuard.WellFormed(r, out reason)) { Refused.Add($"{r.kind}: {reason}"); return false; }
                string only = CraftRequestGuard.TestStationOnly(r, W);
                if (only != null) { reason = only; Refused.Add($"{r.kind}: {reason}"); return false; }
                if (r.kind == CraftRequestKind.Fold || r.kind == CraftRequestKind.Weld) r.flag = true;
                Guard.Settings = W.Rules.Settings.mastery;
                Guard.Skill = Skill;
                if (!Guard.Check(ref r, Now, out string note, W)) { reason = note; Refused.Add($"{r.kind}: {reason}"); return false; }
                if (note.Length > 0) Partial.Add($"{r.kind}: {note}");
                CraftMastery.ByPractice(ref r, Skill, W.Rules.Settings.mastery);
                bool ok;
                CraftWorkshop w = W;
                switch (r.kind)
                {
                    case CraftRequestKind.Move:
                    {
                        int to = r.flag ? r.i1 : r.i1 > 0 ? r.i1 : 9999;
                        ok = w.MovePiece(r.piece, r.location, out reason, r.a > 0 ? r.a : 1, r.i0, to, r.i2, r.b);
                        break;
                    }
                    case CraftRequestKind.Turn: ok = w.TurnPiece(r.piece, out reason); break;
                    case CraftRequestKind.SetIntent: ok = w.SetIntent(r.piece, r.id, r.other, out reason); break;
                    case CraftRequestKind.AddFuel:
                    {
                        CraftFuel fuel = w.Rules.Fuel(r.id);
                        int have = FreeFuel ? -1 : Have(r.id);
                        if (fuel == null) { ok = false; reason = "That does not burn here."; break; }
                        if (have == 0) { ok = false; reason = $"You have no {r.id}."; break; }
                        int count = have < 0 ? Math.Max(1, r.count) : Math.Min(Math.Max(1, r.count), have);
                        ok = w.AddFuel(fuel.id, count, out reason, r.flag);
                        if (ok && have > 0) UseSupply(r.id, count, out _);
                        break;
                    }
                    case CraftRequestKind.Bellows:
                        ok = r.flag ? w.Has(CraftTools.Furnace) : w.Has(CraftTools.Forge);
                        if (ok) w.Bellows(r.a > 0 ? r.a : 1, r.flag);
                        else reason = "no fire";
                        break;
                    case CraftRequestKind.Damper: w.SetDamper(r.a, r.flag); ok = true; break;
                    case CraftRequestKind.Oven: ok = w.SetOven(r.a, out reason); break;
                    case CraftRequestKind.Crush: ok = w.CrushOre(r.piece, r.i0, r.a, out reason, r.i2); break;
                    case CraftRequestKind.DiscardFragment: ok = w.DiscardFragment(r.piece, r.i0, out reason, r.i2); break;
                    case CraftRequestKind.Wash: ok = w.WashOre(r.piece, r.a, r.b, r.c, out reason); break;
                    case CraftRequestKind.Sieve:
                    {
                        string fines = w.SieveOre(r.piece, r.a, out reason, r.b > 0 ? r.b : 1);
                        ok = reason == "" || fines != null;
                        break;
                    }
                    case CraftRequestKind.BrushOre:
                        ok = w.BrushOre(r.piece, r.i0, (CraftOreBrush)r.i1, Math.Min(1, r.a * Pace / Math.Max(0.1, w.Rules.Settings.handWorkPace)), out reason, r.i2);
                        break;
                    case CraftRequestKind.Flux:
                        ok = HaveSupply(r.id, Math.Max(1, r.count), out reason)
                             && (r.i0 >= 2 ? w.FluxPiece(r.id, Math.Max(1, r.count), r.piece, out reason, r.a > 0 ? r.a : 1, r.points) : w.AddFlux(r.id, Math.Max(1, r.count), r.i0, out reason, r.piece, r.a > 0 ? r.a : 1))
                             && UseSupply(r.id, Math.Max(1, r.count), out reason);
                        break;
                    case CraftRequestKind.TapSlag: ok = w.TapSlag(out reason, r.a > 0 ? r.a : 1); break;
                    case CraftRequestKind.ExtractBloom: ok = w.ExtractBloom(out reason) != null; break;
                    case CraftRequestKind.StirMelt: ok = w.StirMelt(r.a > 0 ? r.a : 1, out reason); break;
                    case CraftRequestKind.Skim: ok = w.SkimDross(r.a > 0 ? r.a : 1, out reason); break;
                    case CraftRequestKind.Pour:
                        ok = (r.flag ? w.KnockOut(out reason) : w.Pour(r.id, r.a, r.b, r.i0, out reason, r.c, r.d, r.i1 > 0 ? r.i1 / 1000.0 : 1, r.i2 / 1000.0)) != null;
                        break;
                    case CraftRequestKind.TakeMelt:
                    {
                        string cast = r.flag ? w.LiftBead(out reason) : w.TakeMelt(r.id, r.a, r.b, out reason, r.c, r.d, r.i1 > 0 ? r.i1 / 1000.0 : 1, r.i0, r.i2 / 1000.0);
                        ok = cast != null;
                        break;
                    }
                    case CraftRequestKind.Strike: ok = w.Strike(r.piece, r.a, (CraftStrikeTool)r.i0, (CraftStrikeFace)r.i1, r.b, r.c, out reason); break;
                    case CraftRequestKind.Fold: ok = w.Fold(r.piece, out reason, r.b > 0 ? r.b : 1, r.flag); break;
                    case CraftRequestKind.Weld: ok = w.Weld(r.piece, r.other, out reason, r.b > 0 ? r.b : 1, r.flag); break;
                    case CraftRequestKind.Split:
                        ok = w.Piece(r.piece)?.kind != CraftPieceKind.Wood;
                        if (ok) ok = w.SplitPiece(r.piece, r.a, out reason) != null;
                        else reason = "Wood is sawn through stroke by stroke.";
                        break;
                    case CraftRequestKind.FormFastener: ok = w.FormFastener(r.piece, (CraftFastenerKind)r.i0, out reason) != null; break;
                    case CraftRequestKind.Agitate: ok = w.Agitate(r.piece, (CraftQuenchMotion)r.i0, out reason, r.a > 0 ? r.a : -1); break;
                    case CraftRequestKind.FillTank:
                    {
                        // the station's own quenchants (a preset station's: CraftThermal.PresetQuenchants), water free from the well
                        CraftQuenchant q = w.Rules.Quenchant(r.id);
                        if (q == null || !CraftThermal.PresetQuenchants(w.Rules, Tools, false).Contains(q.id)) { reason = "The tank is not filled with that here."; ok = false; break; }
                        bool free = string.IsNullOrEmpty(q.item);
                        ok = (free || HaveSupply(r.id, 1, out reason)) && w.FillTank(r.id, out reason) && (free || UseSupply(r.id, 1, out reason));
                        break;
                    }
                    case CraftRequestKind.Clay:
                    {
                        string clay = w.Rules.FirstSupply(CraftSupplyKind.Clay)?.id;
                        ok = HaveSupply(clay, 1, out reason) && w.ApplyClay(r.piece, r.i0, r.i1, r.a, out reason, r.points) && UseSupply(clay, 1, out reason);
                        break;
                    }
                    case CraftRequestKind.Descale: ok = w.Descale(r.piece, r.i0, r.i1, r.id, r.a, r.b * Pace, out reason); break;
                    case CraftRequestKind.Grind: ok = w.Grind(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, r.c * Pace, out reason); break;
                    case CraftRequestKind.File: ok = w.File(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, r.c * Pace, out reason); break;
                    case CraftRequestKind.Polish: ok = w.Polish(r.piece, r.i0, r.i1, r.id, r.a, r.b * Pace, out reason); break;
                    case CraftRequestKind.Coat: ok = HaveSupply(r.id, 1, out reason) && w.Coat(r.piece, r.id, out reason) && UseSupply(r.id, 1, out reason); break;
                    case CraftRequestKind.Rive: ok = w.Rive(r.piece, r.a, r.b, out reason, r.c > 0 ? r.c : 0.5) != null; break;
                    case CraftRequestKind.SawCut: ok = w.SawCut(r.piece, r.a, r.b, r.c, out reason) != null; break;
                    case CraftRequestKind.BendWood: ok = w.BendWood(r.piece, r.i0, r.i1, r.a, r.b, r.flag, out reason, r.c); break;
                    case CraftRequestKind.StackWood: ok = w.StackWood(r.piece, r.a, r.b, r.c, out reason); break;
                    case CraftRequestKind.StackCover: ok = w.SetStackCover(r.a, out reason); break;
                    case CraftRequestKind.SandWood: ok = w.SandWood(r.piece, r.i0, r.i1, r.a, r.b * Pace / Math.Max(0.1, w.Rules.Settings.handWorkPace), r.c, out reason); break;
                    case CraftRequestKind.Varnish: ok = w.Varnish(r.piece, r.id, r.i0, r.i1, r.a, r.b, out reason); break;
                    case CraftRequestKind.DebarkWood: ok = w.DebarkWood(r.piece, r.i0, r.i1, r.a, r.b, r.c, out reason); break;
                    case CraftRequestKind.SplitLog: ok = w.SplitLog(r.piece, r.a, r.b, r.c, Math.Max(1, r.i0), out reason) != null; break;
                    case CraftRequestKind.Carve:
                        ok = w.Carve(r.piece, r.i0, r.i1, (CraftSurface.CarveCut)r.i2, r.a * Pace / Math.Max(0.1, w.Rules.Settings.handWorkPace), r.b, out reason);
                        break;
                    case CraftRequestKind.Sharpen: ok = w.Sharpen(r.piece, r.i0, r.i1, (CraftSurfaceSide)r.i2, r.id, r.a, r.b, Math.Max(1, r.count), r.c, r.flag, out reason); break;
                    case CraftRequestKind.Scrape: ok = w.Scrape(r.piece, r.points, r.flag, r.a, r.b, out reason); break;
                    case CraftRequestKind.Stake: ok = w.Stake(r.piece, r.points, r.a, out reason); break;
                    case CraftRequestKind.Stretch: ok = w.Stretch(r.piece, r.a, r.b, r.c, r.d, out reason); break;
                    case CraftRequestKind.FillVat:
                    {
                        CraftTanningAgent agent = w.Rules.Agent(r.id);
                        bool free = agent != null && (agent.kind == CraftAgentKind.Water || string.IsNullOrEmpty(agent.item));
                        ok = (free || HaveSupply(r.id, Math.Max(1, r.count), out reason)) && w.FillVat(r.i0, r.id, Math.Max(1, r.count), out reason) && (free || UseSupply(r.id, Math.Max(1, r.count), out reason));
                        break;
                    }
                    case CraftRequestKind.DrainVat: ok = w.DrainVat(r.i0, out reason); break;
                    case CraftRequestKind.Condition: ok = HaveSupply(r.id, 1, out reason) && w.Condition(r.piece, r.id, r.points, r.a, out reason) && UseSupply(r.id, 1, out reason); break;
                    case CraftRequestKind.Cut: ok = w.CutPattern(r.piece, r.id, r.i0, r.i1, r.i2, r.a, out reason) != null; break;
                    case CraftRequestKind.StartAssembly:
                        ok = CraftAssembly.MadeAt(w.Rules, w.Rules.Design(r.id), Tools, out string missing);
                        if (ok) ok = w.StartAssembly(r.id, out reason) != null;
                        else reason = "That is not made here (" + missing + ").";
                        break;
                    case CraftRequestKind.Attach: ok = Attach(r, out reason); break;
                    case CraftRequestKind.Detach: ok = w.Detach(r.piece, r.id, r.i0, out reason); break;
                    case CraftRequestKind.JointWork: ok = w.JointWork(r.piece, r.id, (CraftJointWork)r.i0, Math.Max(0, r.count), r.a, r.b, out reason); break;
                    case CraftRequestKind.Finish: ok = w.Finish(r.piece, out reason, Skill); break;
                    default: ok = false; reason = "not emulated: " + r.kind; break;
                }
                if (!ok)
                {
                    Guard.Refund();
                    Refused.Add($"{r.kind}: {reason}");
                }
                else reason = note;
                return ok;
            }

            private bool Attach(CraftRequest r, out string reason)
            {
                CraftPiece asm = W.Piece(r.piece);
                CraftDesign design = asm != null && asm.kind == CraftPieceKind.Assembly ? W.Rules.Design(asm.assembly.design) : null;
                CraftComponentSpec spec = design?.Component(r.id);
                if (spec != null && spec.supply)
                {
                    CraftSupplyDefinition s = W.Rules.Supply(r.other);
                    if (s != null && s.kind != spec.supplyKind || s == null && !string.IsNullOrEmpty(r.other)) { reason = "That is not it."; return false; }
                    s ??= W.Rules.Supplies.FirstOrDefault(x => x.kind == spec.supplyKind && Have(x.id) > 0) ?? W.Rules.FirstSupply(spec.supplyKind);
                    int room = CraftAssembly.SupplyRoom(design, asm, spec.role);
                    if (room <= 0) return W.Attach(r.piece, r.id, 0, null, out reason, 1, s?.strength ?? 0);
                    int n = Math.Min(Math.Max(1, r.count), room);
                    if (s != null && !HaveSupply(s.id, n, out reason)) return false;
                    if (!W.Attach(r.piece, r.id, 0, null, out reason, n, s?.strength ?? 0)) return false;
                    return s == null || UseSupply(s.id, n, out reason);
                }
                return W.Attach(r.piece, r.id, r.i0, r.other, out reason);
            }

            /// <summary>Takes a piece off this station (into the bag) and puts it on <paramref name="to"/> (the piece item's clone).</summary>
            public string CarryTo(Bench to, string id)
            {
                CraftPiece p = W.Piece(id);
                Assert.IsTrue(CraftWorkshop.CanTake(p, out string why), $"{Name}: take {id}: {why}");
                List<CraftPiece> taken = W.RemovePiece(id, out why);
                Assert.IsNotNull(taken, why);
                CraftPiece piece = taken[0].Clone();
                var parts = taken.Skip(1).Select(c => c.Clone()).ToList();
                return to.W.AddPiece(piece, parts);
            }

            public string Put(CraftPiece p) => W.AddPiece(p);
        }

        // ================================================================== the hands at the forge and the anvil
        /// <summary>
        /// A smith's hands at the forge and the anvil, as the windows send them: the bellows pumped at a steady rhythm (one
        /// pump each 0.8 s), the fire fed a shovel at a time, the piece put in at a depth and taken out, and blows of the
        /// hammer held up about as long as a full charge takes (0.95 s), with the hands' own tremble.
        /// </summary>
        internal sealed class Smith
        {
            public readonly Bench B;
            public readonly CraftBellowsPump Pump = new CraftBellowsPump();
            public readonly CraftHammer Hammer = new CraftHammer();
            public int Blows, Heats, Refusals;
            public double Depth = 0.7;
            public Smith(Bench b) { B = b; }

            private void Feed()
            {
                if (B.W.Forge.fuel < 4)
                    Assert.IsTrue(B.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 1 }, out string why), why);
            }

            public void PumpOnce()
            {
                Pump.Tick(0.8);
                B.Wait(0.8);
                double s = Pump.Pump(B.Hands);
                Assert.IsTrue(B.Do(new CraftRequest(CraftRequestKind.Bellows) { a = (float)s }, out string why), why);
            }

            public void Blow(double seconds)
            {
                for (double t = 0; t < seconds; t += 0.8) { Feed(); PumpOnce(); }
            }

            public void Light()
            {
                Assert.IsTrue(B.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 3 }, out string why), why);
                Blow(20);
            }

            /// <summary>Into the fire (the whole piece, or segments from..to) until its coolest worked stretch is at <paramref name="until"/> °C; then out onto the anvil.</summary>
            public void Heat(string id, double until, double most = 120, int from = -1, int to = -1)
            {
                RestIfTired();
                var r = new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Fire, a = (float)Depth };
                if (from >= 0) { r.i0 = from; r.i1 = to; r.flag = true; }
                Assert.IsTrue(B.Do(r, out string why), why);
                Heats++;
                double t = 0;
                while (t < most)
                {
                    Feed();
                    PumpOnce();
                    t += 0.8;
                    CraftPiece p = B.W.Piece(id);
                    double cool = double.MaxValue;
                    for (int i = 0; i < p.segments.Count; i++)
                        if (from < 0 || i >= from && i <= to) cool = Math.Min(cool, p.segments[i].Temperature);
                    if (cool >= until) break;
                }
                Assert.IsTrue(B.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, out why), why);
            }

            /// <summary>One blow: the hammer held up 0.95 s (a full charge), released at <paramref name="aim"/> (0-1 along the piece).</summary>
            /// <summary>The fatigue at which the window tells the arm is tiring: a careful player puts the hammer down and rests then.</summary>
            public double RestAt = 0.35;
            public double MostFatigue;
            public double Rested;

            /// <summary>Rests the arm (hands still: no pumping, no blows) until it is fresh again.</summary>
            public void RestIfTired()
            {
                MostFatigue = Math.Max(MostFatigue, B.Hands.fatigue);
                if (B.Hands.fatigue < RestAt) return;
                double t = 0;
                while (B.Hands.fatigue > 0.12 && t < 120) { B.Wait(1); t += 1; }
                Rested += t;
            }

            public bool Strike(string id, double aim, CraftStrikeTool tool, CraftStrikeFace face, double tilt = 0, double hold = 0.95)
            {
                MostFatigue = Math.Max(MostFatigue, B.Hands.fatigue);
                Hammer.Raise();
                Hammer.Tick(hold);
                B.Wait(hold);
                CraftBlow blow = Hammer.Release(aim, tool == CraftStrikeTool.Horn ? 0 : tilt, B.Hands);
                double sent = tool == CraftStrikeTool.Horn ? tilt : blow.tilt;
                bool ok = B.Do(new CraftRequest(CraftRequestKind.Strike, id) { a = (float)blow.position, i0 = (int)tool, i1 = (int)face, b = (float)blow.force, c = (float)sent }, out string why);
                Blows++;
                if (!ok) Refusals++;
                return ok;
            }
        }

        /// <summary>The design's target width and thickness at x mm along a part forged to L mm.</summary>
        private static void TargetAt(CraftShapeTarget t, double L, double x, out double w, out double th)
        {
            t.At((float)CraftMath.Clamp01(x / L), out float tw, out float tt);
            w = tw;
            th = tt;
        }

        /// <summary>
        /// Where each segment's metal belongs on the finished part (mm along it, L long): matched by volume - the metal of
        /// the first n % of the bar's volume makes the first n % of the part's - so a smith draws the tang from the end of
        /// the bar that becomes it.
        /// </summary>
        private static double[] Destinations(CraftPiece p, CraftShapeTarget t, double L)
        {
            int n = p.segments.Count;
            double total = 0;
            var mid = new double[n];
            for (int i = 0; i < n; i++)
            {
                double v = p.segments[i].Volume(p.edges);
                mid[i] = total + v * 0.5;
                total += v;
            }
            const int steps = 400;
            var cum = new double[steps + 1];
            for (int k = 1; k <= steps; k++)
            {
                TargetAt(t, L, (k - 0.5) * L / steps, out double w, out double th);
                cum[k] = cum[k - 1] + w * th * L / steps;
            }
            var x = new double[n];
            for (int i = 0; i < n; i++)
            {
                double want = mid[i] / total * cum[steps];
                int k = 1;
                while (k < steps && cum[k] < want) k++;
                double f = cum[k] - cum[k - 1] > 1e-9 ? (want - cum[k - 1]) / (cum[k] - cum[k - 1]) : 0;
                x[i] = (k - 1 + f) * L / steps;
            }
            return x;
        }

        /// <summary>The length a part of this target takes the piece's volume to (within its range).</summary>
        private static double LengthFor(CraftPiece p, CraftShapeTarget t, double keep = 1)
        {
            double area = 0;
            for (int k = 0; k < 200; k++)
            {
                t.At((k + 0.5f) / 200f, out float w, out float th);
                area += w * th / 200.0;
            }
            double v = 0;
            foreach (MetalSegment s in p.segments) v += s.Volume(p.edges);
            return CraftMath.Clamp(v * keep / area, t.lengthMin, t.lengthMax);
        }

        /// <summary>
        /// Forges a piece toward a design's target, heat after heat, as a careful smith at the anvil would: the stretch whose
        /// width or thickness is furthest from where its metal belongs gets the blow that moves it there - on its edge to
        /// narrow it, the cross peen across to draw it out, the flat to thin and spread it - while it is at forging heat.
        /// </summary>
        private static double ForgeTo(Smith s, string id, CraftShapeTarget t, double L, int maxHeats = 60, double good = 0.92, bool log = false)
        {
            CraftRules rules = s.B.W.Rules;
            int last = -1;
            for (int heat = 0; heat < maxHeats; heat++)
            {
                CraftPiece p = s.B.W.Piece(id);
                CraftAlloyProfile prof = p.Profile(rules);
                double score = CraftEvaluator.ShapeScore(rules, p, t);
                if (log) Console.WriteLine($"  heat {heat}: length {p.Length:0} shape {score:0.000} mass {p.Mass(rules):0} blows {s.Blows} defects {p.defects.Count}");
                if (score >= good) return score;
                s.RestIfTired();
                s.Heat(id, prof.HotWorkMin + 250, 90);
                bool any = false;
                for (int blow = 0; blow < 40; blow++)
                {
                    p = s.B.W.Piece(id);
                    double[] x = Destinations(p, t, L);
                    int n = p.segments.Count;
                    var ew = new double[n];
                    var et = new double[n];
                    for (int i = 0; i < n; i++)
                    {
                        TargetAt(t, L, x[i], out double tw, out double tt);
                        ew[i] = (p.segments[i].width - tw) / Math.Max(5, tw);
                        et[i] = (p.segments[i].thickness - tt) / Math.Max(1, tt);
                    }
                    bool EdgeSafe(int i)
                    {
                        for (int j = Math.Max(0, i - 3); j <= Math.Min(n - 1, i + 3); j++)
                            if (p.segments[j].width / Math.Max(0.3, p.segments[j].thickness) > 5.5) return false;
                        return true;
                    }
                    int best = -1;
                    double need = 0;
                    CraftStrikeTool tool = CraftStrikeTool.Flat;
                    CraftStrikeFace face = CraftStrikeFace.Flat;
                    for (int i = 0; i < n; i++)
                    {
                        if (p.segments[i].Temperature < prof.HotWorkMin + 30) continue;
                        double here = 0;
                        CraftStrikeTool k = CraftStrikeTool.Flat;
                        CraftStrikeFace f = CraftStrikeFace.Flat;
                        double ratio = p.segments[i].width / Math.Max(0.3, p.segments[i].thickness);
                        if (ew[i] > 0.05 && EdgeSafe(i)) { here = ew[i] + 0.05; f = CraftStrikeFace.Edge; }
                        else if (et[i] > 0.04 && (ew[i] <= 0.05 || ratio / (1 - Math.Min(0.3, et[i])) < 4.5)) { here = et[i]; k = ew[i] < -0.15 ? CraftStrikeTool.PeenAlong : ew[i] < -0.05 ? CraftStrikeTool.Flat : CraftStrikeTool.PeenAcross; }
                        if (here <= 0) continue;
                        if (last >= 0 && Math.Abs(i - last) <= 2) here *= 1.5; // keep working the stretch in hand
                        if (here > need) { need = here; best = i; tool = k; face = f; }
                    }
                    if (best < 0) break;
                    any = true;
                    last = best;
                    s.Strike(id, CraftMath.Clamp01(p.PositionOf(best)), tool, face);
                }
                if (!any && CraftEvaluator.ShapeScore(rules, s.B.W.Piece(id), t) < good && heat > 3)
                {
                    // nothing left the hammer can do toward the target
                    if (log) Console.WriteLine("  nothing more the hammer can do");
                    break;
                }
            }
            return CraftEvaluator.ShapeScore(rules, s.B.W.Piece(id), t);
        }

        private static string Profile(CraftRules rules, CraftPiece p, CraftShapeTarget t, double L)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < p.segments.Count; i++)
            {
                MetalSegment g = p.segments[i];
                t.At((float)p.PositionOf(i), out float tw, out float tt);
                sb.Append($"[{i} @{p.PositionOf(i):0.00} w {g.width:0.0}/{tw:0.0} t {g.thickness:0.0}/{tt:0.0} l {g.length:0.0} {g.Temperature:0}C]");
                if (i % 4 == 3) sb.Append('\n');
            }
            return sb.ToString();
        }

        // ================================================================== the wheel, the stone, the tank, the oven
        /// <summary>
        /// The grinding wheel as the window sends it: every 0.2 s the piece is held to the wheel, the stretch under it with
        /// the time it dwelt (here one segment at a time, moving along), at the window's pace; dipped in the tank to cool
        /// when it runs hot (a hardened edge long before its temper runs).
        /// </summary>
        private static int Grind(Bench b, string id, int from, int to, CraftSurfaceSide side, string wheel, double angle, double press, double secondsEach,
            double coolAt = 150, bool file = false)
        {
            int sent = 0;
            for (int i = from; i <= to; i++)
            {
                for (double t = 0; t < secondsEach - 1e-6; t += 0.2)
                {
                    CraftPiece p = b.W.Piece(id);
                    if (p.MaxTemperature > coolAt) Dip(b, id);
                    b.Wait(0.2);
                    var r = new CraftRequest(file ? CraftRequestKind.File : CraftRequestKind.Grind, id) { i0 = i, i1 = i, i2 = (int)side, id = wheel, a = (float)angle, b = (float)press, c = 0.2f };
                    if (b.Do(r, out string why)) sent++;
                    else if (why.Contains("skates") || why.Contains("glazes")) return sent;
                }
            }
            return sent;
        }

        /// <summary>The grind window's Dip: into the tank, out once cooler than 100 °C (4 s at the most).</summary>
        private static void Dip(Bench b, string id)
        {
            CraftPiece p = b.W.Piece(id);
            if (p.location == CraftLocation.Assembled || !b.W.Has(CraftTools.QuenchTank))
            {
                // a fitted part is not dipped: it is let cool in the air
                while (b.W.Piece(id).MaxTemperature > 60) b.Wait(2);
                return;
            }
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Quench, a = 1f }, out string why), why);
            for (double t = 0; t < 4 && b.W.Piece(id).MaxTemperature > 100; t += 0.2) b.Wait(0.2);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, out why), why);
        }

        /// <summary>Lets a piece cool in the air to <paramref name="below"/> °C.</summary>
        private static void Cool(Bench b, string id, double below = 40, double most = 1800)
        {
            for (double t = 0; t < most && b.W.Piece(id).MaxTemperature > below; t += 5) b.Wait(5);
        }

        /// <summary>
        /// Passes on the whetstone as the window sends them: one request a pass (a run of a fifth of the stone or more that
        /// turns), three passes a second at most - here one each 0.45 s - the angle held, its wobble, the pressure, and
        /// whether the edge led.
        /// </summary>
        private static int Hone(Bench b, string id, int from, int to, bool left, string stone, double angle, double press, int passes, bool leading, double wobble = 1.0)
        {
            int ok = 0;
            for (int k = 0; k < passes; k++)
            {
                b.Wait(0.45);
                b.Hands.Move(0.1);
                var r = new CraftRequest(CraftRequestKind.Sharpen, id)
                {
                    i0 = from, i1 = to, i2 = (int)(left ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel), id = stone,
                    a = (float)angle, b = (float)press, count = 1, c = (float)wobble, flag = leading,
                };
                if (b.Do(r, out _)) ok++;
            }
            return ok;
        }

        /// <summary>The edge's stretch of a design's part on the piece (segments whose position lies in edgeFrom..edgeTo).</summary>
        private static void EdgeSpan(CraftPiece p, CraftShapeTarget t, out int from, out int to)
        {
            from = -1;
            to = -1;
            for (int i = 0; i < p.segments.Count; i++)
            {
                double x = p.PositionOf(i);
                if (x < t.edgeFrom || x > t.edgeTo) continue;
                if (from < 0) from = i;
                to = i;
            }
        }

        private static string Edge(CraftRules rules, CraftPiece p, int from, int to)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = from; i <= to; i++)
            {
                MetalSegment g = p.segments[i];
                sb.Append($"[{i} w{g.width:0.0} t{g.thickness:0.0} et{g.EdgeThickness:0.00} L{g.AngleL:0}/{g.bevelDepthL:0.0} R{g.AngleR:0}/{g.bevelDepthR:0.0} hone {g.honeL:0.0}/{g.honeR:0.0} apex {g.apex:0} burr {g.burr:0.00}] ");
            }
            return sb.ToString();
        }

        private static string Metallurgy(CraftRules rules, CraftPiece p)
        {
            CraftPieceReport r = CraftEvaluator.Piece(rules, p);
            return $"edge {r.EdgeHardness:0} HV body {r.BodyHardness:0} HV tough {r.Toughness:0.0} (min {r.MinToughness:0.0}) hardened {r.Hardened:0.00} tempered {r.Tempered:0.00} stress {r.Stress:0.00} grain {r.Grain:0.00} sharp {r.Sharpness:0.00} angle {r.EdgeAngle:0} sym {r.Symmetry:0.00} burr {r.Burr:0.00} rough {r.Roughness:0} scale {r.Scale:0.000} integrity {r.Integrity:0.00}";
        }

        private static string Defects(CraftPiece p) => string.Join("; ", p.defects.GroupBy(d => d.kind + ": " + d.cause).Select(g => $"{g.Key} x{g.Count()} ({g.Sum(d => d.severity):0.00})"));

        private static string Report(CraftItemReport r) =>
            $"{r.Name}: {r.Tier} q={r.Quality:0.000} material {r.Material:0.00} work {r.Workmanship:0.00} integrity {r.Integrity:0.00} edge {r.Edge:0.00} heat {r.HeatTreatment:0.00} assembly {r.Assembly:0.00} protection {r.Protection:0.00} " +
            $"dmg {r.DamageMin:0.0}-{r.DamageMax:0.0} speed {r.AttackSpeed:0.00} weight {r.Weight:0.00} kg price {r.Price:0.0} durability {r.Durability} defence {r.Defense:0.0}\n  notes: {string.Join(" | ", r.Notes)}\n  parts: {string.Join(" | ", r.Parts)}";

        /// <summary>Heats a piece to a hardening heat (every stretch past its critical temperature, held a moment) and quenches it upright, point first, moved up and down.</summary>
        private static void Harden(Smith s, string id, double soak = 20, CraftQuenchMotion motion = CraftQuenchMotion.UpDown)
        {
            Bench b = s.B;
            CraftPiece p = b.W.Piece(id);
            CraftAlloyProfile prof = p.Profile(b.W.Rules);
            double crit = prof.CriticalTemperature;
            s.Heat(id, crit + 40, 150);
            // back in to soak a little (the smith watches the colour even out): a few more pumps
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Fire, a = (float)s.Depth }, out string why), why);
            for (double t = 0; t < soak; t += 0.8) s.PumpOnce();
            // dragged straight from the fire down into the tank, upright and point first, all of it
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Quench, a = 1f, b = 0f }, out why), why);
            b.Wait(0.3);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Agitate, id) { i0 = (int)motion, a = -1f }, out why), why);
            for (double t = 0; t < 30 && b.W.Piece(id).MaxTemperature > 60; t += 0.5) b.Wait(0.5);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Agitate, id) { i0 = (int)CraftQuenchMotion.Still, a = -1f }, out why), why);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, out why), why);
        }

        /// <summary>Tempers in the oven: kindled at <paramref name="celsius"/>, the piece in once it is at its heat, out after <paramref name="seconds"/>.</summary>
        private static void Temper(Bench b, string id, double celsius, double seconds)
        {
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Oven) { a = (float)celsius }, out string why), why);
            for (double t = 0; t < 600 && b.W.Oven.temperature < celsius - 10; t += 5) b.Wait(5);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Oven }, out why), why);
            b.Wait(seconds);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Air }, out why), why);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Oven) { a = 0 }, out why), why);
        }

        /// <summary>Straightens a bent piece at forging heat with the straightening blows, where it is bent.</summary>
        private static void Straighten(Smith s, string id, double within = 2)
        {
            for (int heat = 0; heat < 6; heat++)
            {
                CraftPiece p = s.B.W.Piece(id);
                double bend = p.segments.Sum(g => g.bend);
                if (Math.Abs(bend) <= within) return;
                s.Heat(id, p.Profile(s.B.W.Rules).HotWorkMin + 200, 90);
                for (int k = 0; k < 16; k++)
                {
                    p = s.B.W.Piece(id);
                    int worst = 0;
                    for (int i = 1; i < p.segments.Count; i++) if (Math.Abs(p.segments[i].bend) > Math.Abs(p.segments[worst].bend)) worst = i;
                    if (Math.Abs(p.segments[worst].bend) < 0.2) break;
                    s.Strike(id, p.PositionOf(worst), CraftStrikeTool.Straighten, CraftStrikeFace.Flat);
                }
            }
        }

        // ================================================================== more of the smith's work
        private static bool Log = true;
        private static HashSet<string> Ids(Bench b) => new HashSet<string>(b.W.Pieces.Select(p => p.id));

        /// <summary>Cuts a piece in two with the hot chisel at <paramref name="at"/> (0-1), blow by blow at forging heat; returns the far part's id.</summary>
        private static string Chisel(Smith s, string id, double at)
        {
            HashSet<string> before = Ids(s.B);
            for (int heat = 0; heat < 6; heat++)
            {
                CraftPiece p = s.B.W.Piece(id);
                s.Heat(id, p.Profile(s.B.W.Rules).HotWorkMin + 200, 90);
                for (int k = 0; k < 20; k++)
                {
                    // the chisel stays where it was set: every blow drives it there
                    s.Hammer.Raise();
                    s.Hammer.Tick(0.95);
                    s.B.Wait(0.95);
                    CraftBlow blow = s.Hammer.Release(at, 0, s.B.Hands);
                    s.B.Do(new CraftRequest(CraftRequestKind.Strike, id) { a = (float)at, i0 = (int)CraftStrikeTool.Chisel, i1 = (int)CraftStrikeFace.Flat, b = (float)blow.force, c = (float)blow.tilt });
                    string made = s.B.W.Pieces.Select(x => x.id).FirstOrDefault(x => !before.Contains(x));
                    if (made != null) return made;
                    if (s.B.W.Piece(id).segments[s.B.W.Piece(id).SegmentAt(at)].Temperature < s.B.W.Piece(id).Profile(s.B.W.Rules).HotWorkMin - 100) break;
                }
            }
            Assert.Fail("the chisel did not cut it through");
            return null;
        }

        /// <summary>Drifts an eye at <paramref name="at"/> until it is <paramref name="diameter"/> mm across, at forging heat.</summary>
        private static void Drift(Smith s, string id, double at, double diameter)
        {
            for (int heat = 0; heat < 6; heat++)
            {
                CraftPiece p = s.B.W.Piece(id);
                if (CraftAssembly.Eye(p) >= diameter) return;
                s.Heat(id, p.Profile(s.B.W.Rules).HotWorkMin + 200, 90);
                for (int k = 0; k < 12; k++)
                {
                    p = s.B.W.Piece(id);
                    if (CraftAssembly.Eye(p) >= diameter) return;
                    int seg = p.segments.FindIndex(g => g.hole > 0);
                    double where = seg >= 0 ? p.PositionOf(seg) : at;
                    if (p.segments[p.SegmentAt(where)].Temperature < p.Profile(s.B.W.Rules).HotWorkMin - 40) break;
                    s.Hammer.Raise();
                    s.Hammer.Tick(0.95);
                    s.B.Wait(0.95);
                    CraftBlow blow = s.Hammer.Release(where, 0, s.B.Hands);
                    // the drift is set in the eye it opened (aimed at it: the hands' tremble moves the hammer, not the drift)
                    s.B.Do(new CraftRequest(CraftRequestKind.Strike, id) { a = (float)where, i0 = (int)CraftStrikeTool.Drift, i1 = (int)CraftStrikeFace.Flat, b = (float)(blow.force * 0.7), c = (float)blow.tilt });
                }
            }
        }

        /// <summary>
        /// Folds a bar on itself and forge-welds it, as the forge window does by hand: brought to welding heat, fluxed all
        /// over, folded (the seam left open), then struck shut with the flat at welding heat, back to the fire between heats
        /// (fluxed again) until the seam is set.
        /// </summary>
        private static void FoldWeld(Smith s, string id, string flux = "borax")
        {
            CraftRules rules = s.B.W.Rules;
            CraftAlloyProfile prof = s.B.W.Piece(id).Profile(rules);
            double weld = CraftForging.WeldingHeat(rules, prof);
            double keep = s.Depth;
            s.Depth = 0.9;
            s.Heat(id, weld - 60, 120);
            Assert.IsTrue(s.B.Do(new CraftRequest(CraftRequestKind.Flux, id) { id = flux, i0 = 2, count = 1, a = 1f }, out string why), why);
            Assert.IsTrue(s.B.Do(new CraftRequest(CraftRequestKind.Fold, id) { flag = true, b = 1f, count = 1 }, out why), "fold: " + why);
            if (Log) Console.WriteLine($"   folded: {s.B.W.Piece(id).segments.Count} segs {s.B.W.Piece(id).Length:0} mm seam {CraftForging.SeamOpen(s.B.W.Piece(id)):0.00} flux {CraftForging.MeanFlux(s.B.W.Piece(id)):0.00} minT {s.B.W.Piece(id).segments.Min(g => g.Temperature):0}");
            for (int heat = 0; heat < 8 && CraftForging.SeamOpen(s.B.W.Piece(id)) > 0; heat++)
            {
                if (Log) Console.WriteLine($"   heat {heat}: seam {CraftForging.SeamOpen(s.B.W.Piece(id)):0.00} flux {CraftForging.MeanFlux(s.B.W.Piece(id)):0.00} delam {s.B.W.Piece(id).defects.Count(d => d.kind == CraftDefectKind.Delamination)} refused {string.Join(" | ", s.B.Refused.Skip(Math.Max(0, s.B.Refused.Count - 2)))}");
                if (heat > 0)
                {
                    s.Heat(id, weld - 20, 120);
                    s.B.Do(new CraftRequest(CraftRequestKind.Flux, id) { id = flux, i0 = 2, count = 1, a = 1f });
                }
                else s.Heat(id, weld - 20, 120);
                for (int k = 0; k < 20; k++)
                {
                    CraftPiece p = s.B.W.Piece(id);
                    int open = -1;
                    for (int i = 0; i < p.segments.Count; i++)
                        if (p.segments[i].seamOpen > 0 && (open < 0 || p.segments[i].seamOpen > p.segments[open].seamOpen)) open = i;
                    if (open < 0) break;
                    if (p.segments[open].Temperature < weld - 150) break;
                    s.Strike(id, p.PositionOf(open), CraftStrikeTool.Flat, CraftStrikeFace.Flat);
                }
            }
            s.Depth = keep;
        }

        // ================================================================== the carving bench (a smith's files and knives)
        /// <summary>Saws a wooden piece across at <paramref name="mark"/> (0-1) with the bench's saw: strokes up and down on the line; returns the far part's id.</summary>
        private static string Saw(Bench b, string id, double mark, double wander = 0.12, double square = 0.9)
        {
            CraftPiece p = b.W.Piece(id);
            MetalSegment g = p.segments[p.SegmentAt(mark)];
            double strokes = CraftSawStroke.StrokesFor(Math.Min(g.width, g.thickness));
            b.Wait(strokes * 0.6); // a full stroke each 0.6 s
            b.Hands.Move(0.3);
            HashSet<string> before = Ids(b);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.SawCut, id) { a = (float)mark, b = (float)wander, c = (float)square }, out string why), "saw: " + why);
            return b.W.Pieces.Select(x => x.id).First(x => !before.Contains(x));
        }

        /// <summary>Knife strokes along the whole piece (0.5 s each, a medium pressure), until its width and thickness are down to <paramref name="w"/> and <paramref name="t"/> mm.</summary>
        private static int Shave(Bench b, string id, double w, double t, int from = 0, int to = -1, float press = 0.5f)
        {
            int n = 0;
            for (int k = 0; k < 400; k++)
            {
                CraftPiece p = b.W.Piece(id);
                int last = to < 0 ? p.segments.Count - 1 : to;
                double wide = 0, thick = 0;
                for (int i = from; i <= last; i++) { wide = Math.Max(wide, p.segments[i].width); thick = Math.Max(thick, p.segments[i].thickness); }
                CraftSurface.CarveCut cut;
                if (wide > w + 0.2) cut = CraftSurface.CarveCut.Width;
                else if (thick > t + 0.2) cut = CraftSurface.CarveCut.Thickness;
                else break;
                b.Wait(0.5);
                // the window's stroke: depth by the pressure and its half second, steadiness by how straight it ran
                float depth = press * 1.8f * 1f * (0.6f + 0.4f * (float)b.Hands.Strength);
                float steady = 0.9f * Mathf.Clamp01(1f - (float)b.Hands.Unsteadiness * 5f);
                b.Hands.Move(press * 0.4f);
                b.Do(new CraftRequest(CraftRequestKind.Carve, id) { i0 = from, i1 = last, i2 = (int)cut, a = depth, b = steady });
                n++;
            }
            return n;
        }

        /// <summary>Bores the slot for the tang from end 0, a turn at a time, until it takes the tang (the window's own test: <see cref="CraftWoodwork.TangSlot"/>).</summary>
        private static int Bore(Bench b, string id)
        {
            CraftPiece p = b.W.Piece(id);
            Assert.IsTrue(CraftWoodwork.TangSlot(b.W, p, out double need, out _), "the grip knows the tang it goes on");
            int turns = 0, reach = 1, n = p.segments.Count;
            while (CraftAssembly.Eye(b.W.Piece(id)) < need && turns < 200) // until the slot takes the tang (the fit refuses one even a little tight)
            {
                b.Wait(0.4);
                b.Hands.Move(0.2);
                if (b.Do(new CraftRequest(CraftRequestKind.Carve, id) { i0 = 0, i1 = reach - 1, i2 = (int)CraftSurface.CarveCut.Bore, a = 1.2f * 0.5f + 0.3f, b = 0.85f }))
                    reach = Math.Min(reach + 1, Math.Max(1, n * 3 / 4));
                turns++;
            }
            return turns;
        }

        /// <summary>The assembly bench's hammer: a blow held up to <paramref name="charge"/> of a full one (the bench sends 4 x the charge).</summary>
        private static bool JointBlow(Bench b, string asm, string joint, CraftJointWork work, double charge)
        {
            b.Wait(Math.Max(0.45, 0.9 * charge + 0.1));
            b.Hands.Move(charge);
            return b.Do(new CraftRequest(CraftRequestKind.JointWork, asm) { id = joint, i0 = (int)work, count = 1, a = (float)(4 * charge) }, out _);
        }

        // ================================================================== the journeys
        [Test]
        public void Journey_Dagger_FromTwoStockBars_AtASmithy()
        {
            var bag = new Dictionary<string, int> { ["borax"] = 6, ["refractory_clay"] = 2, ["oil"] = 1 };
            var b = new Bench("smithy", CraftTools.Smithy, bag);
            var s = new Smith(b);
            CraftRules rules = b.W.Rules;
            CraftDesign dagger = rules.Design("dagger");
            CraftShapeTarget bt = dagger.Component("blade").shape;
            string blade = b.Put(StockBar(rules, "Spring steel"));
            // what the bar is for, said at the anvil (its For menu): the blade's shape is dashed over it from now on
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.SetIntent, blade) { id = "dagger", other = "blade" }, out string said), said);
            Assert.AreEqual(2, b.W.Piece(blade).edges, "the dagger's blade is double-edged");
            s.Light();

            // ---- the blade: drawn out to the design's shape, straightened
            double L = LengthFor(b.W.Piece(blade), bt, 0.98);
            double shape = ForgeTo(s, blade, bt, L, 40, 0.95, true);
            Straighten(s, blade);
            CraftPiece bp = b.W.Piece(blade);
            Console.WriteLine($"FORGED: shape {CraftEvaluator.ShapeScore(rules, bp, bt):0.000} length {bp.Length:0} mass {bp.Mass(rules):0} blows {s.Blows} heats {s.Heats} t={b.Now - 100:0}s rested {s.Rested:0}s most fatigue {s.MostFatigue:0.00} bend {bp.segments.Sum(g => g.bend):0.0} defects: {Defects(bp)}");
            Console.WriteLine(Profile(rules, bp, bt, L));
            Cool(b, blade, 60);

            // ---- the profile ground where the hammer left it wide (the point)
            bp = b.W.Piece(blade);
            for (int i = 0; i < bp.segments.Count; i++)
            {
                bt.At((float)bp.PositionOf(i), out float tw, out _);
                double over = bp.segments[i].width - tw;
                if (over > 0.8 && bp.PositionOf(i) > bt.tangTo) Grind(b, blade, i, i, CraftSurfaceSide.Spine, "coarse_wheel", 15, 0.8, Math.Min(30, over * 2.5), 300);
            }
            Cool(b, blade, 60);
            // ---- hardened at full thickness (an edge ground first cracks the more easily), tempered in the oven: the quench
            // window says water is harsh on spring steel, so the tank is filled with the quenching oil the smithy keeps
            string warned = CraftLook.HarshQuench(rules, b.W.Piece(blade), rules.Quenchant(b.W.Quench.medium), CraftThermal.PresetQuenchants(rules, b.Tools, false));
            Console.WriteLine("QUENCH WINDOW: " + warned);
            StringAssert.Contains("quenching oil", warned);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.FillTank) { id = "oil" }, out string filled), "the tank filled with oil: " + filled);
            Harden(s, blade);
            bp = b.W.Piece(blade);
            Console.WriteLine($"QUENCHED: {Metallurgy(rules, bp)} bend {bp.segments.Sum(g => g.bend):0.0} defects: {Defects(bp)}");
            Assert.IsFalse(bp.HasDefect(CraftDefectKind.Crack), "quenched in oil, the blade is whole");
            Temper(b, blade, 220, 300);
            bp = b.W.Piece(blade);
            Console.WriteLine($"TEMPERED: {Metallurgy(rules, bp)}");

            // ---- the bevels ground until the window's strip is green (the edge met, centred, at the design's angle), cooled in the tank
            EdgeSpan(b.W.Piece(blade), bt, out int e0, out int e1);
            for (int pass = 0; pass < 60; pass++)
            {
                bp = b.W.Piece(blade);
                bool done = true;
                for (int i = e0; i <= e1; i++)
                {
                    bt.At((float)bp.PositionOf(i), out float tw, out float tt);
                    done &= CraftSurface.Judge(bp.segments[i], tw, tt, bt.edgeAngle, true).progress != CraftSurface.Progress.ToGrind;
                }
                if (done) break;
                for (int i = e0; i <= e1; i++)
                {
                    bp = b.W.Piece(blade);
                    bt.At((float)bp.PositionOf(i), out float tw, out float tt);
                    CraftSurface.Judgement j = CraftSurface.Judge(bp.segments[i], tw, tt, bt.edgeAngle, true);
                    if (j.progress != CraftSurface.Progress.ToGrind) continue;
                    string wheel = pass < 30 ? "coarse_wheel" : "fine_wheel";
                    // the shallower face first, as the strip's two bars show
                    if (j.doneL <= j.doneR) Grind(b, blade, i, i, CraftSurfaceSide.LeftBevel, wheel, bt.edgeAngle / 2, 0.6, 0.4, 100);
                    else Grind(b, blade, i, i, CraftSurfaceSide.RightBevel, wheel, bt.edgeAngle / 2, 0.6, 0.4, 100);
                }
            }
            bp = b.W.Piece(blade);
            Console.WriteLine($"GROUND: edges {bp.edges} {Metallurgy(rules, bp)} shape {CraftEvaluator.ShapeScore(rules, bp, bt):0.000} t={b.Now - 100:0}s");
            Console.WriteLine(Edge(rules, bp, e0, e1));
            Cool(b, blade, 60);
            int passes = 0;
            foreach (string stone in new[] { "medium_stone", "fine_stone" })
            {
                for (int round = 0; round < 30; round++)
                {
                    passes += Hone(b, blade, e0, e1, true, stone, bt.edgeAngle / 2 + 1, 0.45, 6, true);
                    passes += Hone(b, blade, e0, e1, false, stone, bt.edgeAngle / 2 + 1, 0.45, 6, true);
                    bp = b.W.Piece(blade);
                    bool honed = true;
                    for (int i = e0; i <= e1; i++) honed &= bp.segments[i].honeL >= 1 && bp.segments[i].honeR >= 1;
                    if (honed) break;
                }
                passes += Hone(b, blade, e0, e1, true, stone, bt.edgeAngle / 2 + 1, 0.15, 6, false);
                passes += Hone(b, blade, e0, e1, false, stone, bt.edgeAngle / 2 + 1, 0.15, 6, false);
                Console.WriteLine($"  after the {stone}: {passes} passes, sharp {CraftEvaluator.Piece(rules, b.W.Piece(blade)).Sharpness:0.00}");
            }
            passes += Hone(b, blade, e0, e1, true, "strop", bt.edgeAngle / 2 + 1, 0.3, 6, false);
            passes += Hone(b, blade, e0, e1, false, "strop", bt.edgeAngle / 2 + 1, 0.3, 6, false);
            bp = b.W.Piece(blade);
            Console.WriteLine($"SHARPENED ({passes} passes): {Metallurgy(rules, bp)} t={b.Now - 100:0}s");
            Console.WriteLine(Edge(rules, bp, e0, e1));

            // ---- the guard and the pommel, from a bar of mild steel: cut on the anvil, forged, their eyes drifted
            CraftShapeTarget gt = dagger.Component("guard").shape, pt = dagger.Component("pommel").shape;
            string fitting = b.Put(StockBar(rules, "Mild steel"));
            string rest = Chisel(s, fitting, 0.3);           // 60 mm for the guard
            string guard = fitting;
            string pommel = rest;
            string spare = Chisel(s, pommel, 0.5);           // 70 mm for the pommel, 70 mm spare
            ForgeTo(s, guard, gt, LengthFor(b.W.Piece(guard), gt), 20, 0.9);
            Drift(s, guard, 0.5, gt.eyeDiameter);
            FoldWeld(s, pommel);
            ForgeTo(s, pommel, pt, LengthFor(b.W.Piece(pommel), pt), 20, 0.9);
            Drift(s, pommel, 0.5, pt.eyeDiameter);
            Cool(b, guard, 60);
            Cool(b, pommel, 60);
            CraftPiece gp = b.W.Piece(guard), pp = b.W.Piece(pommel);
            Console.WriteLine($"GUARD: shape {CraftEvaluator.ShapeScore(rules, gp, gt):0.00} {gp.Length:0} mm {gp.Mass(rules):0} g eye {CraftAssembly.Eye(gp):0.0} defects {Defects(gp)}");
            Console.WriteLine($"POMMEL: shape {CraftEvaluator.ShapeScore(rules, pp, pt):0.00} {pp.Length:0} mm {pp.Mass(rules):0} g eye {CraftAssembly.Eye(pp):0.0} seam {CraftForging.SeamOpen(pp):0.00} defects {Defects(pp)}");

            // ---- the grip: a maple blank, its purpose chosen, sawn to length, shaved, the slot bored
            string grip = b.Put(CraftDefaults.WoodBlank(rules, "maple"));
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.SetIntent, grip) { id = "dagger", other = "grip" }, out string why), why);
            CraftWoodSpec gs = dagger.Component("grip").wood;
            string offcut = Saw(b, grip, 100.0 / b.W.Piece(grip).Length);
            int strokes = Shave(b, grip, (gs.diameterMin + gs.diameterMax) / 2, (gs.diameterMin + gs.diameterMax) / 2);
            // the slot is the size of the tang it goes on - the real blade's where it lies in the workshop (its tang measured)
            int turns = Bore(b, grip);
            CraftPiece wp = b.W.Piece(grip);
            Console.WriteLine($"GRIP: {wp.Length:0} mm, {wp.segments.Max(g => g.width):0.0} x {wp.segments.Max(g => g.thickness):0.0}, slot {CraftAssembly.Eye(wp):0.0} ({strokes} strokes, {turns} turns) split {wp.wood.split:0.00}");

            // ---- the assembly bench
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "dagger" }, out why), why);
            string asm = b.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            foreach ((string role, string piece) in new[] { ("blade", blade), ("guard", guard), ("grip", grip), ("pommel", pommel) })
                Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = role, i0 = 0, other = piece }, out why), $"{role}: {why}");
            Console.WriteLine($"FITTED: edges of the blade {b.W.Piece(blade).edges}; missing: {string.Join("; ", CraftAssembly.Missing(b.W, b.W.Piece(asm)))}");
            for (int k = 0; k < 6 && !CraftAssembly.JointComplete(dagger.Joint("pommel_peen"), b.W.Piece(asm).assembly.Joint("pommel_peen")); k++)
                JointBlow(b, asm, "pommel_peen", CraftJointWork.Peen, 0.8);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Finish, asm), out why), "finish: " + why);
            CraftItemReport item = CraftEvaluator.Item(rules, b.W, b.W.Piece(asm));
            Console.WriteLine("ITEM: " + Report(item));
            Console.WriteLine($"time at the smithy {(b.Now - 100) / 60:0} min");
            // the dead ends the journey met are gone: the blade whole (oil), its edge ground all along (the strip asks for it
            // where the hammer left it thin), and the dagger rated by what it is
            CollectionAssert.DoesNotContain(item.Notes, "Blade: cracked - it will break in use");
            Assert.IsFalse(item.Notes.Any(n => n.Contains("not ground along")), string.Join(" | ", item.Notes));
            Assert.GreaterOrEqual(item.Quality, 0.6, $"{item.Tier} ({item.Quality:0.00})");
            Console.WriteLine("refused: " + string.Join(" | ", b.Refused.Distinct()));
            Console.WriteLine("partial: " + string.Join(" | ", b.Partial.Distinct().Take(8)));
        }

        /// <summary>A piece shaped to a design's part at <paramref name="length"/> (each segment as wide and thick as the target there).</summary>
        internal static CraftPiece ToTarget(CraftRules rules, CraftShapeTarget t, double length, string metal, double edgeLeft = -1)
        {
            CraftPiece p = CraftDefaults.Bar(rules, length, 10, 5, Metal(metal));
            double pos = 0;
            foreach (MetalSegment s in p.segments)
            {
                t.At((float)((pos + s.length * 0.5) / length), out float w, out float th);
                s.width = w;
                s.thickness = th;
                pos += s.length;
            }
            if (edgeLeft >= 0)
                for (int i = 0; i < p.segments.Count; i++)
                {
                    double x = p.PositionOf(i);
                    if (x < t.edgeFrom || x > t.edgeTo) continue;
                    MetalSegment s = p.segments[i];
                    double d = Math.Max(0, (s.thickness - edgeLeft) * 0.5), wd = d / Math.Tan(t.edgeAngle * 0.5 * Math.PI / 180);
                    s.bevelDepthL = s.bevelDepthR = d;
                    s.bevelWidthL = s.bevelWidthR = Math.Min(wd, s.width);
                }
            // as the mass it has (the volume of the shaped segments)
            double grams = 0;
            foreach (MetalSegment s in p.segments) grams += s.Volume(p.edges);
            return p;
        }

        /// <summary>Grinds a blade's edge until the grind window's strip is green (or nothing more is to grind): the shallower face first.</summary>
        private static int GrindToGreen(Bench b, string id, CraftShapeTarget t, string wheel, double press = 0.6, double coolAt = 100, int most = 80)
        {
            EdgeSpan(b.W.Piece(id), t, out int e0, out int e1);
            int sent = 0;
            for (int pass = 0; pass < most; pass++)
            {
                CraftPiece p = b.W.Piece(id);
                bool done = true;
                for (int i = e0; i <= e1; i++)
                {
                    t.At((float)p.PositionOf(i), out float tw, out float tt);
                    done &= CraftSurface.Judge(p.segments[i], tw, tt, t.edgeAngle, true).progress != CraftSurface.Progress.ToGrind;
                }
                if (done) break;
                for (int i = e0; i <= e1; i++)
                {
                    p = b.W.Piece(id);
                    t.At((float)p.PositionOf(i), out float tw, out float tt);
                    CraftSurface.Judgement j = CraftSurface.Judge(p.segments[i], tw, tt, t.edgeAngle, true);
                    if (j.progress != CraftSurface.Progress.ToGrind) continue;
                    sent += Grind(b, id, i, i, j.doneL <= j.doneR ? CraftSurfaceSide.LeftBevel : CraftSurfaceSide.RightBevel, wheel, t.edgeAngle / 2, press, 0.4, coolAt);
                }
            }
            return sent;
        }

        /// <summary>Hones each face with edge-leading strokes until both meet the apex, takes the burr off with light trailing ones; returns the passes made.</summary>
        private static int HoneToKeen(Bench b, string id, int e0, int e1, string stone, double angle, double press = 0.45, int most = 400)
        {
            int passes = 0;
            for (int round = 0; round < most / 12; round++)
            {
                CraftPiece p = b.W.Piece(id);
                bool honed = true;
                for (int i = e0; i <= e1; i++) honed &= p.segments[i].honeL >= 1 && p.segments[i].honeR >= 1;
                if (honed) break;
                passes += Hone(b, id, e0, e1, true, stone, angle, press, 6, true);
                passes += Hone(b, id, e0, e1, false, stone, angle, press, 6, true);
            }
            passes += Hone(b, id, e0, e1, true, stone, angle, 0.15, 6, false);
            passes += Hone(b, id, e0, e1, false, stone, angle, 0.15, 6, false);
            return passes;
        }

        // ================================================================== what the journeys showed, one rule at a time
        /// <summary>
        /// The smithy the editor's menu makes has a tank of water and keeps brine and quenching oil too
        /// (<see cref="CraftThermal.PresetQuenchants"/>). Over the water, a spring-steel dagger blade is warned of (the
        /// quench window: water cracks a blade of hardening steel); with the tank filled with oil as the window says, heated
        /// just past its critical heat and quenched as the window teaches (upright, point first, moved up and down), it comes
        /// out hardened and whole - at full thickness, and with its edge roughed in to 1.5 mm as smiths leave it.
        /// </summary>
        [Test]
        public void Rule_ADaggerBlade_AtTheDefaultSmithy_HardensWhole()
        {
            CraftRules rules = Rules();
            CraftShapeTarget bt = rules.Design("dagger").Component("blade").shape;
            var seen = new List<(string line, double integrity, double hardened)>();
            foreach (double edge in new[] { -1.0, 1.5 })
            {
                var b = new Bench("smithy", CraftTools.Smithy, new Dictionary<string, int> { ["oil"] = 1 }, 3, "water");
                var sm = new Smith(b);
                string id = b.Put(ToTarget(rules, bt, 430, "Spring steel", edge));
                string warned = CraftLook.HarshQuench(rules, b.W.Piece(id), rules.Quenchant(b.W.Quench.medium), CraftThermal.PresetQuenchants(rules, b.Tools, false));
                StringAssert.Contains("quenching oil", warned, "over the water the quench window says it is harsh, and what to fill the tank with");
                Assert.IsFalse(b.Do(new CraftRequest(CraftRequestKind.FillTank) { id = "frostwater" }, out string no), "a quenchant the station does not keep");
                Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.FillTank) { id = "oil" }, out string why), "the default smithy keeps oil: " + why);
                sm.Light();
                Harden(sm, id, 10);
                CraftPiece p = b.W.Piece(id);
                CraftPieceReport r = CraftEvaluator.Piece(rules, p);
                seen.Add(($"edge {(edge < 0 ? "full" : edge + " mm")}: {p.defects.Count(d => d.kind == CraftDefectKind.Crack)} cracks, integrity {r.Integrity:0.00}, hardened {r.Hardened:0.00} ({r.EdgeHardness:0} HV)", r.Integrity, r.Hardened));
            }
            Console.WriteLine(string.Join("\n", seen.Select(x => x.line)));
            foreach (var x in seen)
            {
                Assert.GreaterOrEqual(x.integrity, 0.7, "quenched in oil as the window teaches, the blade comes out whole: " + x.line);
                Assert.GreaterOrEqual(x.hardened, 0.3, "and hardened: " + x.line);
            }
        }

        /// <summary>A dagger whose blade is cracked through rates no better than poor: its sound guard, grip and pommel do not make up for it.</summary>
        [Test]
        public void Rule_ADaggerWithABladeCrackedThrough_IsNotAGoodDagger()
        {
            CraftRules rules = Rules();
            CraftDesign d = rules.Design("dagger");
            CraftShapeTarget bt = d.Component("blade").shape;
            CraftItemReport Make(bool cracked)
            {
                CraftPiece blade = ToTarget(rules, bt, 430, "Spring steel", 0);
                blade.intentDesign = "dagger";
                blade.intentRole = "blade";
                blade.edges = 2;
                foreach (MetalSegment g in blade.segments)
                {
                    g.edge.mart = g.body.mart = 0.9; g.edge.temperP = g.body.temperP = CraftMetallurgy.Temper(0, 220, 300, rules.Settings.temperTimeScale);
                    g.apex = 3; g.honeL = g.honeR = 1; g.rough = 6; g.scale = 0;
                }
                if (cracked)
                    for (int i = 11; i < 24; i++) blade.AddDefect(CraftDefectKind.Crack, i, 0.27, true, "cracked in the quench");
                CraftPiece guard = ToTarget(rules, d.Component("guard").shape, 90, "Mild steel");
                guard.segments[12].hole = 11;
                CraftPiece pommel = ToTarget(rules, d.Component("pommel").shape, 35, "Mild steel");
                pommel.segments[12].hole = 11;
                CraftPiece grip = CraftDefaults.WoodBlank(rules, "maple", 100, 26, 26);
                grip.segments[0].hole = 11;
                var parts = new Dictionary<string, List<CraftPiece>> { ["blade"] = new List<CraftPiece> { blade }, ["guard"] = new List<CraftPiece> { guard }, ["grip"] = new List<CraftPiece> { grip }, ["pommel"] = new List<CraftPiece> { pommel } };
                var joints = new AssemblyState { design = "dagger" };
                foreach (CraftJointSpec j in d.joints) joints.joints.Add(new JointState { joint = j.id, gap = 0.4, peen = 1.2, done = true });
                return CraftEvaluator.Item(rules, d, parts, joints);
            }
            CraftItemReport sound = Make(false), broken = Make(true);
            Console.WriteLine("sound:   " + Report(sound));
            Console.WriteLine("cracked: " + Report(broken));
            Assert.Less(CraftEvaluator.Piece(rules, broken.PartPieces.First(x => x.role == "blade").piece).Integrity, 0.05, "the blade itself is cracked through");
            Assert.Less(broken.Quality, 0.4, $"a dagger whose blade is cracked through is rated {broken.Tier} ({broken.Quality:0.00}): its integrity {broken.Integrity:0.00} is the mean of its parts', the sound fittings outweighing the broken blade");
        }

        /// <summary>A bar fitted as a double-edged blade is a double-edged blade from then on, as a bar given that purpose is (the fit gives it its purpose's edges, as SetIntent does: CraftPiece.IntendedEdges).</summary>
        [Test]
        public void Rule_ABarFittedAsADaggersBlade_TakesItsTwoEdges()
        {
            CraftRules rules = Rules();
            var b = new Bench("smithy", CraftTools.Smithy, null);
            string bar = b.Put(StockBar(rules, "Spring steel"));
            // the grind window knows no design for it: its first bevel makes it single-edged
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Grind, bar) { i0 = 20, i1 = 20, i2 = (int)CraftSurfaceSide.LeftBevel, id = "coarse_wheel", a = 14, b = 0.6f, c = 0.2f }, out string why), why);
            Assert.AreEqual(1, b.W.Piece(bar).edges, "a bevel ground on a bar with no purpose gives it one edge");
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "dagger" }, out why), why);
            string asm = b.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "blade", other = bar }, out why), why);
            CraftPiece fitted = b.W.Piece(bar);
            Assert.AreEqual("dagger", fitted.intentDesign, "fitted, it is the dagger's blade");
            Assert.AreEqual(2, fitted.edges, "the dagger's blade is double-edged, but the bar fitted as it keeps the one edge it had (ApplyAttach sets the intent, not the edges)");
        }

        /// <summary>The grind window says "once the strip is green along the edge, hone it on the whetstone": the stone must then hone that edge, not send the player back to the wheel.</summary>
        [Test]
        public void Rule_AnEdgeTheGrindStripCallsDone_IsOneTheStoneHones()
        {
            CraftRules rules = Rules();
            CraftShapeTarget bt = rules.Design("dagger").Component("blade").shape;
            var b = new Bench("smithy", CraftTools.Smithy, null, 3, "oil");
            CraftPiece blade = ToTarget(rules, bt, 430, "Spring steel");
            blade.intentDesign = "dagger";
            blade.intentRole = "blade";
            blade.edges = 2;
            string id = b.Put(blade);
            GrindToGreen(b, id, bt, "coarse_wheel");
            Cool(b, id, 50);
            CraftPiece p = b.W.Piece(id);
            EdgeSpan(p, bt, out int e0, out int e1);
            var wears = new List<string>();
            for (int i = e0; i <= e1; i++)
            {
                MetalSegment g = p.segments[i];
                bt.At((float)p.PositionOf(i), out float tw, out float tt);
                if (CraftSurface.Judge(g, tw, tt, bt.edgeAngle, true).progress != CraftSurface.Progress.Done) continue;
                CraftSurface.HoneStep step = CraftSurface.NextHone(g.honeL, g.honeR, g.burr, g.EdgeThickness, CraftSurface.HasEdge(g), g.apex, 3);
                if (step == CraftSurface.HoneStep.WearFlat) wears.Add($"{i}: flat {g.EdgeThickness:0.00} mm");
            }
            // how long the medium stone (the walkthrough's) takes to wear that flat away before it hones anything
            int passes = 0;
            while (passes < 2000)
            {
                p = b.W.Piece(id);
                bool flats = false;
                for (int i = e0; i <= e1; i++) flats |= p.segments[i].EdgeThickness > CraftSurface.FlatGone;
                if (!flats) break;
                passes += Hone(b, id, e0, e1, passes % 12 < 6, "medium_stone", bt.edgeAngle / 2 + 1, 0.45, 6, true);
            }
            Console.WriteLine($"green but 'a flat still shows' on {wears.Count} stretches ({string.Join(", ", wears)}); the medium stone wore them away in {passes} passes ({passes * 0.45 / 60:0.0} min)");
            Assert.IsEmpty(wears, "the grind strip turns green at an edge 0.3 mm thick (EdgeMet) and tells the player to hone it; the stone hones nothing above 0.15 mm (FlatGone) and sends them back to the wheel");
        }

        /// <summary>
        /// The forge asks for a steady rhythm on the bellows (a pump each half second to a second and a half is the steadiest),
        /// and a stock bar takes about a minute of it to reach forging heat. The arm should still be fresh enough to forge then.
        /// </summary>
        [Test]
        public void Rule_PumpingTheBellowsSteadily_DoesNotWearOutTheArm()
        {
            CraftRules rules = Rules();
            var b = new Bench("smithy", CraftTools.Smithy, null);
            var sm = new Smith(b) { RestAt = 2 };
            string bar = b.Put(StockBar(rules, "Spring steel"));
            sm.Light();
            double lit = b.Hands.fatigue;
            sm.Heat(bar, b.W.Piece(bar).Profile(rules).HotWorkMin + 250, 120);
            double heated = b.Hands.fatigue, took = b.Now;
            sm.Blow(120);
            Console.WriteLine($"lighting the forge (20 s of pumping): fatigue {lit:0.00}; one heat to forging heat: {heated:0.00}; two more minutes of steady pumping: {b.Hands.fatigue:0.00} ({b.Hands.Feeling})");
            Assert.Less(heated, 0.35, "after lighting the forge and one heat the arm is already tiring (the window's words) before the first blow");
            Assert.Less(b.Hands.fatigue, 0.6, "a few minutes of steady pumping - what every forge session is - exhausts the arm: the window's arm never rests between pumps (idle < 2 s), so steady work drives fatigue to 1");
        }

        // ================================================================== the smeltery's furnace and crucible
        /// <summary>The furnace's bellows (the furnace window's pump, flag = the furnace), a pump each 0.8 s; the furnace fed two shovels when it runs low.</summary>
        private static void FurnaceBlow(Smith s, double seconds)
        {
            for (double t = 0; t < seconds; t += 0.8)
            {
                if (s.B.W.Furnace.fire.fuel < 4)
                    Assert.IsTrue(s.B.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 2, flag = true }, out string why), why);
                s.Pump.Tick(0.8);
                s.B.Wait(0.8);
                double k = s.Pump.Pump(s.B.Hands);
                Assert.IsTrue(s.B.Do(new CraftRequest(CraftRequestKind.Bellows) { a = (float)k, flag = true }, out string w), w);
            }
        }

        /// <summary>
        /// Melts what is in the crucible (the furnace's fire under it, blown steadily, the hands resting when they tire),
        /// covers it with borax, stirs and skims it once it runs, and pours it into <paramref name="mould"/> as the window's
        /// pour sends it: a steady stream in the FLOW bar's green band, unbroken, the cup filled and the crucible tipped back.
        /// Returns the casting's id.
        /// </summary>
        private static string MeltAndPour(Smith s, string mould, double rate = 0.55, double steadiness = 0.85, double over = 60)
        {
            Bench b = s.B;
            for (double t = 0; t < 1800; t += 8)
            {
                if (b.Hands.fatigue > s.RestAt) { b.Wait(3); FurnaceBlow(s, 1); continue; }
                FurnaceBlow(s, 8);
                bool unmelted = b.W.Pieces.Any(p => p.location == CraftLocation.Melt);
                CraftAlloyProfile prof = b.W.Crucible.Mass > 1 ? CraftSmelting.MeltProfile(b.W.Rules, b.W.Crucible) : null;
                if (!unmelted && prof != null && b.W.Crucible.temperature >= prof.Liquidus + over) break;
            }
            // As the crucible's words ask, a melt "glaring ... held this hot, it takes up gas" is let come down before it is
            // poured (poured far too hot it casts with pores): the blast eased off - the hands rest, the air vent half shut -
            // and blown again only if it would turn sluggish.
            for (double t = 0; t < 900; t += 2)
            {
                CraftAlloyProfile m = CraftSmelting.MeltProfile(b.W.Rules, b.W.Crucible);
                double superheat = b.W.Crucible.temperature - m.Liquidus;
                if (Log && t % 20 < 2) Console.WriteLine($"   settling: t {t:0} superheat {superheat:0} furnace {b.W.Furnace.fire.temperature:0} gas {b.W.Crucible.gas:0.00}");
                if (superheat < Math.Max(over, 60)) FurnaceBlow(s, 2);
                else if (CraftSmelting.GasChange(superheat, b.W.Crucible.cover) > 0) b.Wait(2);
                else break;
            }
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Flux) { id = "borax", count = 1, i0 = 1, a = 1f }, out string why), "cover: " + why);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.StirMelt) { a = 0.8f }, out why), "stir: " + why);
            FurnaceBlow(s, 4);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Skim) { a = 0.8f }, out why), "skim: " + why);
            FurnaceBlow(s, 2);
            CraftAlloyProfile melt = CraftSmelting.MeltProfile(b.W.Rules, b.W.Crucible);
            Console.WriteLine($"   melt {b.W.Crucible.Mass:0} g at {b.W.Crucible.temperature:0} (liquidus {melt.Liquidus:0}) dross {b.W.Crucible.dross:0.0} gas {b.W.Crucible.gas:0.00} {melt.ClassName}; mould {CraftSmelting.MoldMass(b.W.Rules, b.W.Rules.Mold(mould), melt.Density):0} g");
            b.Wait(6); // the pour itself
            b.Hands.Move(0.3);
            HashSet<string> before = Ids(b);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Pour) { id = mould, a = (float)rate, b = (float)steadiness, i0 = 0, c = 0.01f, d = 0f, i1 = 1000, i2 = 8 }, out why), "pour: " + why);
            return b.W.Pieces.Select(x => x.id).First(x => !before.Contains(x));
        }

        /// <summary>A piece let cool in the air until it can be carried (no tank at a smeltery).</summary>
        private static void CoolToCarry(Bench b, string id) => Cool(b, id, 110, 3600);

        // ================================================================== the woodshop
        /// <summary>
        /// A haft at the woodshop: a stock blank, its purpose chosen (the carving bench's For menu), sawn to <paramref name="length"/>
        /// mm, shaved to <paramref name="size"/> mm square, and its eye end rasped (Grind ▸ Shape: Rasp ▸ Tang, held on the last
        /// stretch) until it is no more than 3 mm thicker than the eye: a tight drive.
        /// </summary>
        private static string Haft(Bench b, string wood, string design, string role, double length, double size, double eye, bool fromEnd = true)
        {
            CraftRules rules = b.W.Rules;
            string blank = b.Put(CraftDefaults.WoodBlank(rules, wood));
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.SetIntent, blank) { id = design, other = role }, out string why), why);
            CraftPiece p = b.W.Piece(blank);
            if (p.Length > length + 3) Saw(b, blank, length / p.Length);
            Shave(b, blank, size, size);
            // the eye end: the last segment(s)
            p = b.W.Piece(blank);
            int last = p.segments.Count - 1;
            double Over() { MetalSegment e = b.W.Piece(blank).segments[last]; return Math.Max(e.width, e.thickness) - eye; }
            int k = 0;
            while (Over() > 1.0 && k++ < 400)
            {
                b.Wait(0.2);
                b.Do(new CraftRequest(CraftRequestKind.File, blank) { i0 = last, i1 = last, i2 = (int)CraftSurfaceSide.Tang, id = "rasp", a = 15, b = 0.7f, c = 0.2f });
            }
            return blank;
        }

        [Test]
        public void Journey_Hatchet_BronzeHeadCast_AshHaft_AtRealStations()
        {
            CraftRules rules = Rules();
            CraftDesign hatchet = rules.Design("hatchet");
            CraftShapeTarget ht = hatchet.Component("head").shape;
            var bag = new Dictionary<string, int> { ["borax"] = 4 };

            // ---- the smeltery: two stock bronze bars melted under borax and poured into the hatchet's pattern mould
            var smeltery = new Bench("smeltery", CraftTools.Smeltery, bag);
            var sm = new Smith(smeltery);
            string a = smeltery.Put(StockBar(rules, "Bronze")), c = smeltery.Put(StockBar(rules, "Bronze"));
            foreach (string id in new[] { a, c })
                Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Melt }, out string w), w);
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 2, flag = true }, out string why), why);
            string head = MeltAndPour(sm, CraftRules.PatternMoldId("hatchet", "head"));
            CraftPiece hp = smeltery.W.Piece(head);
            Console.WriteLine($"CAST: {hp.Mass(rules):0} g, {hp.Length:0} mm, eye {CraftAssembly.Eye(hp):0.0}, shape {CraftEvaluator.ShapeScore(rules, hp, ht):0.00}, defects {Defects(hp)}; t={smeltery.Now - 100:0}s fatigue {smeltery.Hands.fatigue:0.00}");
            CoolToCarry(smeltery, head);
            Console.WriteLine($"   cooled to carry after {smeltery.Now - 100:0}s");

            // ---- the woodshop: the haft
            var woodshop = new Bench("woodshop", CraftTools.Woodshop, bag);
            CraftWoodSpec hs = hatchet.Component("haft").wood;
            string haft = Haft(woodshop, "ash", "hatchet", "haft", 430, 32, ht.eyeDiameter);
            CraftPiece wp = woodshop.W.Piece(haft);
            Console.WriteLine($"HAFT: {wp.Length:0} mm, {wp.segments[0].width:0.0} x {wp.segments[0].thickness:0.0}, eye end {wp.segments.Last().width:0.0} x {wp.segments.Last().thickness:0.0}, split {wp.wood.split:0.00}, moisture {wp.wood.moisture:0.00}");

            // ---- the smithy: fitted, wedged, the edge ground in place and honed
            var smithy = new Bench("smithy", CraftTools.Smithy, bag);
            string h = smeltery.CarryTo(smithy, head), hf = woodshop.CarryTo(smithy, haft);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "hatchet" }, out why), why);
            string asm = smithy.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "head", other = h }, out why), "head: " + why);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "haft", other = hf }, out why), "haft: " + why);
            CraftJointSpec wedge = hatchet.Joint("head_haft");
            for (int k = 0; k < 12 && !CraftAssembly.JointComplete(wedge, smithy.W.Piece(asm).assembly.Joint("head_haft")); k++)
                JointBlow(smithy, asm, "head_haft", CraftJointWork.Wedge, 0.7);
            Console.WriteLine($"WEDGED: {smithy.W.Piece(asm).assembly.Joint("head_haft").wedge:0.00} gap {smithy.W.Piece(asm).assembly.Joint("head_haft").gap:0.0}");
            int g = GrindToGreen(smithy, h, ht, "coarse_wheel", 0.5, 100);
            EdgeSpan(smithy.W.Piece(h), ht, out int e0, out int e1);
            Console.WriteLine($"GROUND in place: {g} wheel requests; {Edge(rules, smithy.W.Piece(h), e0, e1)}");
            Cool(smithy, h, 60);
            int n = HoneToKeen(smithy, h, e0, e1, "medium_stone", ht.edgeAngle / 2 + 1, 0.45, 1200);
            Console.WriteLine($"HONED: {n} passes ({n * 0.45 / 60:0.0} min) {Metallurgy(rules, smithy.W.Piece(h))}");
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Finish, asm), out why), "finish: " + why);
            CraftItemReport item = CraftEvaluator.Item(rules, smithy.W, smithy.W.Piece(asm));
            Console.WriteLine("ITEM: " + Report(item));
            Console.WriteLine("refused: " + string.Join(" | ", smeltery.Refused.Concat(woodshop.Refused).Concat(smithy.Refused).Distinct()));
            Console.WriteLine("partial: " + string.Join(" | ", smeltery.Partial.Concat(woodshop.Partial).Concat(smithy.Partial).Distinct().Take(6)));
            Assert.GreaterOrEqual(item.Quality, 0.4, "a hatchet made carefully at real stations");
        }

        // ================================================================== the ore bench
        /// <summary>
        /// Cleans a lump at the ore bench as the window's tools do: the hammer on each stone bigger than a few centimetres
        /// (held up 0.75 s: the ore hammer's full charge; the bench sends the blow x 1.5), the barren grey stones picked out
        /// onto the waste heap (the rich ones show their colour), then a slow, even swirl in the pan.
        /// </summary>
        private static void CleanOre(Bench b, string lump, double keepAbove = 0.25, double crushTo = 3.5)
        {
            var oreHammer = new CraftHammer { fullCharge = 0.7f };
            for (int k = 0; k < 200; k++)
            {
                CraftPiece p = b.W.Piece(lump);
                int big = -1;
                for (int i = 0; i < p.ore.fragments.Count; i++)
                    if (p.ore.fragments[i].size > crushTo && (big < 0 || p.ore.fragments[i].size > p.ore.fragments[big].size)) big = i;
                if (big < 0 || p.ore.fragments.Count >= CraftOres.MaxFragments) break;
                oreHammer.Raise();
                oreHammer.Tick(0.75);
                b.Wait(0.75);
                CraftBlow blow = oreHammer.Release(0.5, 0, b.Hands);
                b.Do(new CraftRequest(CraftRequestKind.Crush, lump) { i0 = big, a = (float)(blow.force * 1.5), i2 = CraftOres.Layout(p) });
            }
            for (int k = 0; k < 60; k++)
            {
                CraftPiece p = b.W.Piece(lump);
                if (p.ore.fragments.Count <= 1) break;
                int barren = p.ore.fragments.FindIndex(f => f.mineral < keepAbove);
                if (barren < 0) break;
                b.Wait(0.8);
                b.Do(new CraftRequest(CraftRequestKind.DiscardFragment, lump) { i0 = barren, i2 = CraftOres.Layout(p) });
            }
            b.Wait(6);
            b.Do(new CraftRequest(CraftRequestKind.Wash, lump) { a = 0.4f, b = 6f, c = 0.75f });
        }

        private static string Ore(CraftRules rules, CraftPiece p)
        {
            double m = 0, mineral = 0, dirt = 0;
            foreach (OreFragment f in p.ore.fragments) { m += f.mass; mineral += f.mass * f.mineral; dirt += f.mass * f.dirt; }
            return $"{p.ore.fragments.Count} stones, {m:0} g, mineral {mineral / Math.Max(1, m):0.00}, dirt {dirt / Math.Max(1, m):0.000}";
        }

        [Test]
        public void Journey_IronBarFromOre_OreBench_Furnace_Bloom_Smithy()
        {
            CraftRules rules = Rules();
            var bag = new Dictionary<string, int> { ["limestone"] = 6 };
            var smeltery = new Bench("smeltery", CraftTools.Smeltery, bag);
            var sm = new Smith(smeltery);
            var lumps = new List<string>();
            for (int i = 0; i < 3; i++) lumps.Add(smeltery.Put(CraftOres.NewLump(rules, "hematite", 4242, i)));
            foreach (string l in lumps)
            {
                Console.WriteLine($"lump: {Ore(rules, smeltery.W.Piece(l))}");
                CleanOre(smeltery, l);
                Console.WriteLine($"  cleaned: {Ore(rules, smeltery.W.Piece(l))}");
            }
            // the furnace: charcoal, a scoop of limestone, the ore, a steady blast
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 2, flag = true }, out string why), why);
            FurnaceBlow(sm, 30);
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Flux) { id = "limestone", count = 1, i0 = 0 }, out why), "flux: " + why);
            foreach (string l in lumps)
                if (smeltery.W.Piece(l) != null)
                    Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Move, l) { location = CraftLocation.Furnace }, out why), "charge: " + why);
            double t0 = smeltery.Now;
            for (int k = 0; k < 400; k++)
            {
                if (smeltery.Hands.fatigue > sm.RestAt) { smeltery.Wait(3); FurnaceBlow(sm, 1); }
                FurnaceBlow(sm, 8);
                CraftFurnaceState f = smeltery.W.Furnace;
                if (f.slag > 0.6 * rules.Settings.slagCapacity && f.fire.temperature > 1120) smeltery.Do(new CraftRequest(CraftRequestKind.TapSlag) { a = 1f });
                bool charged = smeltery.W.Pieces.Any(x => x.location == CraftLocation.Furnace && x.kind == CraftPieceKind.Ore);
                if (!charged && f.BloomMass >= CraftSmelting.BloomMinimum) break;
                if (k % 25 == 0) Console.WriteLine($"  t+{smeltery.Now - t0:0}s furnace {f.fire.temperature:0} bloom {f.BloomMass:0} g slag {f.slag:0} pool {f.PoolMass:0} charge {smeltery.W.Pieces.Count(x => x.location == CraftLocation.Furnace)}");
            }
            CraftFurnaceState fs = smeltery.W.Furnace;
            Console.WriteLine($"SMELTED after {smeltery.Now - t0:0}s: bloom {fs.BloomMass:0} g, slag {fs.slag:0} g, bloom slag {fs.bloomSlag:0}, guide: {CraftGuide.Next(smeltery.W, smeltery.Tools, null, null)?.text}");
            HashSet<string> before = Ids(smeltery);
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.ExtractBloom), out why), "pull: " + why);
            string bloom = smeltery.W.Pieces.Select(x => x.id).First(x => !before.Contains(x));
            CraftPiece bp = smeltery.W.Piece(bloom);
            CraftAlloyProfile prof = bp.Profile(rules);
            double bloomMass = bp.Mass(rules);
            Console.WriteLine($"BLOOM: {bp.Mass(rules):0} g {prof.ClassName} C {bp.Grams("carbon") / bp.Mass(rules) * 100:0.00}% P {bp.Grams("phosphorus") / bp.Mass(rules) * 100:0.000}% porosity {bp.segments.Average(g => g.porosity):0.00} slag {bp.segments.Sum(g => g.slag):0} g {bp.Length:0} x {bp.segments.Average(g => g.width):0} x {bp.segments.Average(g => g.thickness):0} mm; defects {Defects(bp)}");
            Console.WriteLine($"  guide: {CraftGuide.Next(smeltery.W, smeltery.Tools, bp, null)?.text ?? "(nothing)"}");
            CoolToCarry(smeltery, bloom);

            // ---- the smithy: consolidated at welding heat, drawn into a bar
            var smithy = new Bench("smithy", CraftTools.Smithy, bag);
            var s2 = new Smith(smithy);
            s2.Light();
            string bar = smeltery.CarryTo(smithy, bloom);
            double weld = CraftForging.WeldingHeat(rules, smithy.W.Piece(bar).Profile(rules));
            s2.Depth = 0.85;
            for (int heat = 0; heat < 12; heat++)
            {
                CraftPiece p = smithy.W.Piece(bar);
                double poro = p.segments.Max(g => g.porosity), slag = p.segments.Sum(g => g.slag);
                if (poro < 0.02 && slag < 0.01 * p.Mass(rules)) break;
                s2.Heat(bar, weld - 40, 150);
                for (int k = 0; k < 16; k++)
                {
                    p = smithy.W.Piece(bar);
                    int worst = 0;
                    for (int i = 1; i < p.segments.Count; i++) if (p.segments[i].porosity + p.segments[i].slag > p.segments[worst].porosity + p.segments[worst].slag) worst = i;
                    if (p.segments[worst].Temperature < weld - 200) break;
                    s2.Strike(bar, p.PositionOf(worst), CraftStrikeTool.Flat, CraftStrikeFace.Flat);
                }
                p = smithy.W.Piece(bar);
                Console.WriteLine($"  heat {heat}: porosity {p.segments.Max(g => g.porosity):0.000} slag {p.segments.Sum(g => g.slag):0.0} g {p.Length:0} x {p.segments.Average(g => g.width):0} x {p.segments.Average(g => g.thickness):0.0} cracks {p.defects.Count(d => d.kind == CraftDefectKind.Crack)} fatigue {smithy.Hands.fatigue:0.00}");
            }
            // drawn out toward a stock bar's section (30 x 6)
            CraftShapeTarget stock = new CraftShapeTarget { lengthMin = 100, lengthMax = 400, massMin = 0, massMax = 99999 };
            stock.Point(0, 30, 6); stock.Point(1, 30, 6);
            ForgeTo(s2, bar, stock, LengthFor(smithy.W.Piece(bar), stock), 20, 0.9);
            Cool(smithy, bar, 60);
            CraftPiece made = smithy.W.Piece(bar);
            CraftPieceReport r = CraftEvaluator.Piece(rules, made);
            Assert.Less(made.Mass(rules), bloomMass, "hammered sound and drawn out, the bloom ends lighter than it came from the furnace: its slag out, its scale burnt off");
            Assert.IsFalse(made.bloom, "consolidated, it is a bloom no longer");
            Assert.AreNotEqual("bloom", CraftLook.ShapeWord(rules, made), "nor is it called one");
            CraftPiece bought = StockBar(rules, "Wrought iron");
            CraftPieceReport rb = CraftEvaluator.Piece(rules, bought);
            Console.WriteLine($"BAR: {r.MaterialName} {made.Mass(rules):0} g {made.Length:0} x {made.segments.Average(g => g.width):0.0} x {made.segments.Average(g => g.thickness):0.0} integrity {r.Integrity:0.00} slag share {r.SlagShare:0.000} porosity {r.Porosity:0.000} hardness {r.BodyHardness:0} value {CraftValue.Of(rules, made):0.00}; defects {Defects(made)}");
            Console.WriteLine($"  a stock wrought-iron bar: {rb.MaterialName} {bought.Mass(rules):0} g integrity {rb.Integrity:0.00} value {CraftValue.Of(rules, bought):0.00}");
            Console.WriteLine($"  look: {string.Join(" | ", CraftLook.Describe(rules, made, null, CraftInstruments.None, false, false).Take(10))}");
            Console.WriteLine("refused: " + string.Join(" | ", smeltery.Refused.Concat(smithy.Refused).Distinct().Take(12)));
        }

        /// <summary>
        /// Scale hammered into the surface: the pit is as deep as the scale the blows drove in (<c>MetalSegment.pit</c>, which
        /// adds up). The defect that reports it should say that depth - not grow by a third of itself with every blow that
        /// re-reports the same pit (AddDefect's merge rule is for a second, separate defect).
        /// </summary>
        [Test]
        public void Rule_AScalePit_IsAsBadAsThePitIsDeep_NotAsTheBlowsAreMany()
        {
            CraftRules rules = Rules();
            var b = new Bench("smithy", CraftTools.Smithy, null);
            var sm = new Smith(b);
            sm.Light();
            string bar = b.Put(StockBar(rules, "Mild steel"));
            int seg = 12;
            sm.Depth = 0.9;
            double weld = CraftForging.WeldingHeat(rules, b.W.Piece(bar).Profile(rules));
            for (int heat = 0; heat < 6; heat++)
            {
                // a welding heat, as a bloom or a weld takes it (an oxidising fire scales it)
                sm.Heat(bar, weld - 40, 150);
                CraftPiece p = b.W.Piece(bar);
                double scale = p.segments[seg].scale;
                for (int k = 0; k < 4; k++) sm.Strike(bar, p.PositionOf(seg), CraftStrikeTool.Flat, CraftStrikeFace.Flat);
                p = b.W.Piece(bar);
                CraftDefect d = p.defects.FirstOrDefault(x => x.kind == CraftDefectKind.ScalePit && x.at == p.SegmentAt(p.PositionOf(seg)));
                Console.WriteLine($"heat {heat}: scale before {scale:0.000} mm, pits now: " + string.Join(", ", p.defects.Where(x => x.kind == CraftDefectKind.ScalePit).Select(x => $"[{x.at}] defect {x.severity:0.00} vs pit {p.segments[x.at].pit:0.00}")));
            }
            CraftPiece q = b.W.Piece(bar);
            foreach (CraftDefect d in q.defects.Where(x => x.kind == CraftDefectKind.ScalePit))
                Assert.LessOrEqual(d.severity, q.segments[d.at].pit * 1.05 + 1e-6, $"segment {d.at}: the pit is {q.segments[d.at].pit:0.00} deep, its defect says {d.severity:0.00} - each blow that re-reports the pit adds 30% of it again");
        }

        /// <summary>A bloom hammered sound at welding heat, and forged on, is a bar (or a blade) - not a "bloom" with "streaks of slag" for ever.</summary>
        [Test]
        public void Rule_AConsolidatedBloom_IsNoLongerABloom()
        {
            CraftRules rules = Rules();
            var b = new Bench("smithy", CraftTools.Smithy, null);
            CraftPiece bloomBar = StockBar(rules, "Wrought iron");
            bloomBar.bloom = true; // as ExtractBloom marks it
            foreach (MetalSegment g in bloomBar.segments) { g.porosity = 0; g.slag = 0; }
            string id = b.Put(bloomBar);
            CraftPiece p = b.W.Piece(id);
            Console.WriteLine($"a sound bar from a bloom (no pores, no slag): called '{CraftLook.ShapeWord(rules, p)}'; it looks: {string.Join(" | ", CraftLook.Describe(rules, p, null, CraftInstruments.None, false, false).Take(2))}");
            p.intentDesign = "dagger";
            p.intentRole = "blade";
            Console.WriteLine($"  given the dagger's blade as its purpose: called '{CraftLook.ShapeWord(rules, p)}'");
            Assert.AreNotEqual("bloom", CraftLook.ShapeWord(rules, p), "the bloom flag is never cleared and is read before the piece's purpose: a dagger blade forged from bloom iron is listed as a bloom");
        }

        [Test]
        public void Journey_Spear_BronzeSocketCast_AshShaft_AtRealStations()
        {
            CraftRules rules = Rules();
            CraftDesign spear = rules.Design("spear");
            CraftShapeTarget st = spear.Component("head").shape;
            var bag = new Dictionary<string, int> { ["borax"] = 4 };
            var smeltery = new Bench("smeltery", CraftTools.Smeltery, bag);
            var sm = new Smith(smeltery);
            for (int i = 0; i < 3; i++)
            {
                string bar = smeltery.Put(StockBar(rules, "Bronze"));
                Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Move, bar) { location = CraftLocation.Melt }, out string w), w);
            }
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 2, flag = true }, out string why), why);
            string head = MeltAndPour(sm, CraftRules.PatternMoldId("spear", "head"));
            CraftPiece hp = smeltery.W.Piece(head);
            Console.WriteLine($"CAST: {hp.Mass(rules):0} g {hp.Length:0} mm socket {CraftAssembly.Eye(hp):0.0} edges {hp.edges} (the design's head is {st.edge}) shape {CraftEvaluator.ShapeScore(rules, hp, st):0.00} defects {Defects(hp)}");
            CoolToCarry(smeltery, head);

            var woodshop = new Bench("woodshop", CraftTools.Woodshop, bag);
            string shaft = Haft(woodshop, "ash", "spear", "haft", 1900, 30, st.eyeDiameter);
            CraftPiece wp = woodshop.W.Piece(shaft);
            Console.WriteLine($"SHAFT: {wp.Length:0} mm {wp.segments[0].width:0.0} x {wp.segments[0].thickness:0.0}, socket end {wp.segments.Last().width:0.0} x {wp.segments.Last().thickness:0.0}");

            var smithy = new Bench("smithy", CraftTools.Smithy, bag);
            string h = smeltery.CarryTo(smithy, head), sh = woodshop.CarryTo(smithy, shaft);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "spear" }, out why), why);
            string asm = smithy.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "head", other = h }, out why), "head: " + why);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "haft", other = sh }, out why), "shaft: " + why);
            CraftJointSpec socket = spear.Joint("head_haft");
            for (int k = 0; k < 12 && !CraftAssembly.JointComplete(socket, smithy.W.Piece(asm).assembly.Joint("head_haft")); k++)
                JointBlow(smithy, asm, "head_haft", CraftJointWork.Wedge, 0.7);
            Console.WriteLine($"DRIVEN ON: {smithy.W.Piece(asm).assembly.Joint("head_haft").wedge:0.00}; guide: {CraftGuide.Next(smithy.W, smithy.Tools, smithy.W.Piece(asm), null)?.text}");
            int g = GrindToGreen(smithy, h, st, "coarse_wheel", 0.5, 100);
            EdgeSpan(smithy.W.Piece(h), st, out int e0, out int e1);
            Cool(smithy, h, 60);
            int n = HoneToKeen(smithy, h, e0, e1, "medium_stone", st.edgeAngle / 2 + 1, 0.45, 1200);
            Console.WriteLine($"EDGE: {g} wheel requests, {n} stone passes, edges {smithy.W.Piece(h).edges}; {Metallurgy(rules, smithy.W.Piece(h))}");
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Finish, asm), out why), "finish: " + why);
            CraftItemReport item = CraftEvaluator.Item(rules, smithy.W, smithy.W.Piece(asm));
            Console.WriteLine("ITEM: " + Report(item));
            Console.WriteLine("refused: " + string.Join(" | ", smeltery.Refused.Concat(woodshop.Refused).Concat(smithy.Refused).Distinct()));
        }

        /// <summary>
        /// A spear's head is driven onto its shaft until the window's gauge reads home: green from
        /// <see cref="CraftAssembly.WedgeGoodLow"/> to <see cref="CraftAssembly.WedgeGoodHigh"/> ("DRIVEN 1.0 (HOME 0.6-1.3)"),
        /// red past it, where the shaft splits. Driven within the green, the shaft holds; driven on into the red, it splits.
        /// </summary>
        [Test]
        public void Rule_ASocketDrivenWithinTheWindowsGreenBand_DoesNotSplitTheShaft()
        {
            CraftRules rules = Rules();
            CraftDesign spear = rules.Design("spear");
            var b = new Bench("smithy", CraftTools.Smithy, null);
            CraftPiece head = ToTarget(rules, spear.Component("head").shape, 270, "Bronze");
            head.segments[1].hole = 26;
            CraftPiece shaft = CraftDefaults.WoodBlank(rules, "ash", 1900, 30, 30);
            shaft.segments[shaft.segments.Count - 1].width = shaft.segments[shaft.segments.Count - 1].thickness = 27;
            string h = b.Put(head), sh = b.Put(shaft);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "spear" }, out string why), why);
            string asm = b.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "head", other = h }, out why), why);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "haft", other = sh }, out why), why);
            // blows in the hammer's band until the gauge's needle is well into the green (the smith stops short of the red)
            while (b.W.Piece(asm).assembly.Joint("head_haft").wedge < 1.0) JointBlow(b, asm, "head_haft", CraftJointWork.Wedge, 0.7);
            double driven = b.W.Piece(asm).assembly.Joint("head_haft").wedge;
            Console.WriteLine($"socket driven {driven:0.00} (the window: green {CraftAssembly.WedgeGoodLow:0.0}-{CraftAssembly.WedgeGoodHigh:0.0}): shaft split {b.W.Piece(sh).wood.split:0.00}");
            Assert.LessOrEqual(driven, CraftAssembly.WedgeGoodHigh, "still in the green");
            Assert.AreEqual(0, b.W.Piece(sh).wood.split, 1e-9, "driven within the window's green band, the shaft holds");
            Assert.IsTrue(CraftAssembly.JointComplete(rules.Design("spear").Joint("head_haft"), b.W.Piece(asm).assembly.Joint("head_haft")), "and the head is home");
            // driven on into the red, as the gauge warns, the shaft splits
            while (b.W.Piece(asm).assembly.Joint("head_haft").wedge < 1.6) JointBlow(b, asm, "head_haft", CraftJointWork.Wedge, 0.7);
            Console.WriteLine($"driven on to {b.W.Piece(asm).assembly.Joint("head_haft").wedge:0.00}: shaft split {b.W.Piece(sh).wood.split:0.00}");
            Assert.Greater(b.W.Piece(sh).wood.split, 0, "past the green the gauge is red, and the shaft splits");
        }

        /// <summary>Smelts the lumps charged in the furnace (a steady blast, the slag tapped when it chokes the draft) until none is left in it.</summary>
        private static void Smelt(Smith s, int most = 600)
        {
            Bench b = s.B;
            for (int k = 0; k < most; k++)
            {
                if (b.Hands.fatigue > s.RestAt) { b.Wait(3); FurnaceBlow(s, 1); }
                FurnaceBlow(s, 8);
                CraftFurnaceState f = b.W.Furnace;
                if (f.slag > 0.6 * b.W.Rules.Settings.slagCapacity && f.fire.temperature > 1120) b.Do(new CraftRequest(CraftRequestKind.TapSlag) { a = 1f });
                if (!b.W.Pieces.Any(x => x.location == CraftLocation.Furnace)) break;
            }
        }

        /// <summary>Runs the furnace's liquid metal into a mould (the crucible area's pour, "from the furnace"): returns the casting's id.</summary>
        private static string RunOff(Smith s, string mould)
        {
            Bench b = s.B;
            FurnaceBlow(s, 6);
            b.Wait(6);
            HashSet<string> before = Ids(b);
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.TakeMelt) { id = mould, a = 0.55f, b = 0.85f, i0 = 0, c = 0.01f, d = 0f, i1 = 1000, i2 = 8 }, out string why), "run off: " + why);
            return b.W.Pieces.Select(x => x.id).FirstOrDefault(x => !before.Contains(x));
        }

        [Test]
        public void Journey_MaceHead_FromOre_Furnace_Crucible_Mould_AtRealStations()
        {
            CraftRules rules = Rules();
            CraftDesign mace = rules.Design("mace");
            var bag = new Dictionary<string, int> { ["limestone"] = 6, ["borax"] = 4 };
            var smeltery = new Bench("smeltery", CraftTools.Smeltery, bag);
            var sm = new Smith(smeltery);
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 2, flag = true }, out string why), why);
            FurnaceBlow(sm, 30);

            // ---- copper from green copper ore
            var copper = new List<string>();
            for (int i = 0; i < 6; i++) copper.Add(smeltery.Put(CraftOres.NewLump(rules, "malachite", 7001, i)));
            foreach (string l in copper) CleanOre(smeltery, l);
            foreach (string l in copper)
                if (smeltery.W.Piece(l) != null) Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Move, l) { location = CraftLocation.Furnace }, out why), "charge: " + why);
            Smelt(sm);
            CraftFurnaceState f = smeltery.W.Furnace;
            Console.WriteLine($"COPPER SMELTED: pool {f.PoolMass:0} g bloom {f.BloomMass:0} g slag {f.slag:0} g, furnace {f.fire.temperature:0}; guide: {CraftGuide.Next(smeltery.W, smeltery.Tools, null, null)?.text}");
            string ingot = RunOff(sm, "ingot");
            CraftPiece ip = smeltery.W.Piece(ingot);
            Console.WriteLine($"  ingot: {ip?.Mass(rules):0} g {ip?.Profile(rules).ClassName}, pool left {smeltery.W.Furnace.PoolMass:0} g; defects {(ip != null ? Defects(ip) : "-")}");
            string more = smeltery.W.Furnace.PoolMass >= CraftSmelting.PourMinimum ? RunOff(sm, "ingot") : null;
            if (more != null) Console.WriteLine($"  a second ingot: {smeltery.W.Piece(more).Mass(rules):0} g");

            // ---- tin from tin stone
            string tinLump = smeltery.Put(CraftOres.NewLump(rules, "cassiterite", 7002, 0));
            CleanOre(smeltery, tinLump);
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Move, tinLump) { location = CraftLocation.Furnace }, out why), "charge tin: " + why);
            Smelt(sm);
            f = smeltery.W.Furnace;
            Console.WriteLine($"TIN SMELTED: pool {f.PoolMass:0} g ({string.Join(", ", f.pool.Where(a => a.grams > 1).Select(a => $"{a.element} {a.grams:0}"))}); guide: {CraftGuide.Next(smeltery.W, smeltery.Tools, null, null)?.text}");
            HashSet<string> before = Ids(smeltery);
            string tin = f.PoolMass >= CraftSmelting.PourMinimum ? RunOff(sm, "ingot") : null;
            if (tin == null && smeltery.Do(new CraftRequest(CraftRequestKind.TakeMelt) { flag = true }, out why)) tin = smeltery.W.Pieces.Select(x => x.id).First(x => !before.Contains(x));
            Console.WriteLine($"  tin: {(tin != null ? $"{smeltery.W.Piece(tin).Mass(rules):0} g {smeltery.W.Piece(tin).Profile(rules).ClassName}" : "none: " + why)}");

            // ---- the crucible: copper and tin into bronze, poured into the mace's head
            foreach (string id in new[] { ingot, more, tin })
            {
                if (id == null) continue;
                Cool(smeltery, id, 600, 600); // lifted off the floor once it has set
                Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Melt }, out why), "into the crucible: " + why);
            }
            string head = MeltAndPour(sm, CraftRules.PatternMoldId("mace", "head"));
            CraftPiece hp = smeltery.W.Piece(head);
            CraftShapeTarget mt = mace.Component("head").shape;
            Console.WriteLine($"MACE HEAD: {hp.Mass(rules):0} g {hp.Profile(rules).ClassName} Sn {hp.Grams("tin") / hp.Mass(rules) * 100:0.0}% eye {CraftAssembly.Eye(hp):0} shape {CraftEvaluator.ShapeScore(rules, hp, mt):0.00} defects {Defects(hp)}; crucible left {smeltery.W.Crucible.Mass:0} g");
            Assert.IsFalse(hp.HasDefect(CraftDefectKind.Porosity), "copper and tin melted together, the tin long run before the copper, cast sound");
            Assert.GreaterOrEqual(hp.Mass(rules), mt.massMin, "and as heavy as a mace head should be");
            CoolToCarry(smeltery, head);

            var woodshop = new Bench("woodshop", CraftTools.Woodshop, bag);
            string haft = Haft(woodshop, "ash", "mace", "haft", 600, 34, mt.eyeDiameter);
            var smithy = new Bench("smithy", CraftTools.Smithy, bag);
            string h = smeltery.CarryTo(smithy, head), hf = woodshop.CarryTo(smithy, haft);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "mace" }, out why), why);
            string asm = smithy.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "head", other = h }, out why), "head: " + why);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "haft", other = hf }, out why), "haft: " + why);
            for (int k = 0; k < 12 && !CraftAssembly.JointComplete(mace.Joint("head_haft"), smithy.W.Piece(asm).assembly.Joint("head_haft")); k++)
                JointBlow(smithy, asm, "head_haft", CraftJointWork.Wedge, 0.7);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Finish, asm), out why), "finish: " + why);
            CraftItemReport item = CraftEvaluator.Item(rules, smithy.W, smithy.W.Piece(asm));
            Console.WriteLine("ITEM: " + Report(item));
            Console.WriteLine($"time: smeltery {(smeltery.Now - 100) / 60:0} min");
            Console.WriteLine("refused: " + string.Join(" | ", smeltery.Refused.Concat(woodshop.Refused).Concat(smithy.Refused).Distinct().Take(12)));
        }

        /// <summary>
        /// Bronze made in the crucible from its metals - a copper ingot and tin - put in together, melted under the furnace's
        /// blast, covered once it runs, poured as soon as it is all molten: the casting should be as sound as bronze poured
        /// from bronze bars, not half pores because the tin, melted long before the copper, "took up gas" all the while.
        /// </summary>
        [Test]
        public void Rule_BronzeAlloyedInTheCrucible_IsNotGassyBecauseTheTinMeltedFirst()
        {
            CraftRules rules = Rules();
            (double gas, double poro, double grams) Pour(bool tinAfter, bool bars)
            {
                var b = new Bench("smeltery", CraftTools.Smeltery, new Dictionary<string, int> { ["borax"] = 4 });
                var sm = new Smith(b);
                var pieces = new List<string>();
                if (bars) { pieces.Add(b.Put(StockBar(rules, "Bronze"))); pieces.Add(b.Put(StockBar(rules, "Bronze"))); pieces.Add(b.Put(StockBar(rules, "Bronze"))); }
                else
                {
                    CraftPiece cu = CraftDefaults.Bar(rules, 120, 40, 20, Metal("Copper")); cu.cast = true; // an ingot run off the furnace
                    pieces.Add(b.Put(cu));
                }
                string tin = bars ? null : b.Put(CraftDefaults.Bar(rules, 60, 20, 12, Metal("Tin")));
                foreach (string id in pieces) Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, id) { location = CraftLocation.Melt }, out string w), w);
                if (tin != null && !tinAfter) Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, tin) { location = CraftLocation.Melt }, out string w2), w2);
                Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 2, flag = true }, out string why), why);
                for (int k = 0; k < 300 && b.W.Pieces.Any(x => x.location == CraftLocation.Melt); k++) FurnaceBlow(sm, 2);
                if (tin != null && tinAfter)
                {
                    Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, tin) { location = CraftLocation.Melt }, out why), why);
                    for (int k = 0; k < 300 && b.W.Pieces.Any(x => x.location == CraftLocation.Melt); k++) FurnaceBlow(sm, 2);
                }
                double gas = b.W.Crucible.gas;
                string cast = MeltAndPour(sm, CraftRules.PatternMoldId("mace", "head"), 0.55, 0.85, 0);
                CraftPiece c = b.W.Piece(cast);
                return (gas, c.segments.Average(g => g.porosity), c.Mass(rules));
            }
            var fromBars = Pour(false, true);
            var together = Pour(false, false);
            var tinLast = Pour(true, false);
            Console.WriteLine($"bronze bars: gas {fromBars.gas:0.00} porosity {fromBars.poro:0.00} {fromBars.grams:0} g; copper and tin together: gas {together.gas:0.00} porosity {together.poro:0.00} {together.grams:0} g; tin added to molten copper: gas {tinLast.gas:0.00} porosity {tinLast.poro:0.00} {tinLast.grams:0} g");
            Assert.Less(together.poro, 0.15, "copper and tin melted together: the tin's melt, a thousand degrees over its own liquidus while the copper is still solid, takes up gas, and the bronze casting comes out a sponge");
        }

        /// <summary>
        /// Stacks a second bar on a first and forge-welds them as the forge window does by hand: both brought to welding
        /// heat in the fire together, both fluxed, the second laid on the first (its seam open), struck shut with the flat
        /// at welding heat, fluxed again between heats.
        /// </summary>
        private static void StackWeld(Smith s, string id, string other, string flux = "borax")
        {
            Bench b = s.B;
            CraftRules rules = b.W.Rules;
            double weld = CraftForging.WeldingHeat(rules, b.W.Piece(id).Profile(rules));
            double keep = s.Depth;
            s.Depth = 0.9;
            s.RestIfTired();
            foreach (string x in new[] { id, other })
                Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, x) { location = CraftLocation.Fire, a = (float)s.Depth }, out string w), w);
            for (double t = 0; t < 150; t += 0.8)
            {
                s.PumpOnce();
                if (b.W.Piece(id).segments.Min(g => g.Temperature) >= weld - 60 && b.W.Piece(other).segments.Min(g => g.Temperature) >= weld - 60) break;
            }
            foreach (string x in new[] { id, other })
            {
                Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Move, x) { location = CraftLocation.Air }, out string w), w);
                Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Flux, x) { id = flux, i0 = 2, count = 1, a = 1f }, out w), "flux: " + w);
            }
            Assert.IsTrue(b.Do(new CraftRequest(CraftRequestKind.Weld, id) { other = other, flag = true, b = 1f, count = 1 }, out string why), "weld: " + why);
            for (int heat = 0; heat < 8 && CraftForging.SeamOpen(b.W.Piece(id)) > 0; heat++)
            {
                s.Heat(id, weld - 20, 120);
                if (heat > 0) b.Do(new CraftRequest(CraftRequestKind.Flux, id) { id = flux, i0 = 2, count = 1, a = 1f });
                for (int k = 0; k < 24; k++)
                {
                    CraftPiece p = b.W.Piece(id);
                    int open = -1;
                    for (int i = 0; i < p.segments.Count; i++)
                        if (p.segments[i].seamOpen > 0 && p.segments[i].Temperature > weld - 150 && (open < 0 || p.segments[i].seamOpen > p.segments[open].seamOpen)) open = i;
                    if (open < 0) break;
                    s.Strike(id, p.PositionOf(open), CraftStrikeTool.Flat, CraftStrikeFace.Flat);
                }
            }
            s.Depth = keep;
        }

        [Test]
        public void Journey_Scythe_WeldedBlade_BentSnath_AtRealStations()
        {
            CraftRules rules = Rules();
            CraftDesign scythe = rules.Design("scythe");
            CraftShapeTarget st = scythe.Component("head").shape;
            var bag = new Dictionary<string, int> { ["borax"] = 12, ["oil"] = 1 };

            // ---- the woodshop: an ash snath, sawn, shaved, steamed and bent into its sweep, seasoned again
            var woodshop = new Bench("woodshop", CraftTools.Woodshop, bag);
            CraftComponentSpec sn = scythe.Component("haft");
            string snath = Haft(woodshop, "ash", "scythe", "haft", 1550, 33, st.eyeDiameter);
            Assert.IsTrue(woodshop.Do(new CraftRequest(CraftRequestKind.Move, snath) { location = CraftLocation.Steam }, out string why), "steam: " + why);
            double t0 = woodshop.Now;
            for (double t = 0; t < 4 * 3600 && woodshop.W.Piece(snath).wood.steamed < 0.99; t += 30) woodshop.Wait(30);
            Console.WriteLine($"STEAMED through after {(woodshop.Now - t0) / 60:0} min: steamed {woodshop.W.Piece(snath).wood.steamed:0.00} moisture {woodshop.W.Piece(snath).wood.moisture:0.00}; guide: {CraftGuide.Next(woodshop.W, woodshop.Tools, woodshop.W.Piece(snath), null)?.text}");
            Assert.IsTrue(woodshop.Do(new CraftRequest(CraftRequestKind.Move, snath) { location = CraftLocation.Air }, out why), why);
            CraftPiece wp = woodshop.W.Piece(snath);
            // over the snath form (600 mm), from the middle of the snath, a little past its 25 degrees (it springs back), at an even pace, with the strap
            for (int k = 0; k < 4; k++)
            {
                double has = woodshop.W.Piece(snath).segments.Sum(g => g.bend);
                if (has >= sn.shape.curvature - 1) break;
                double angle = (sn.shape.curvature - has) * 1.6;
                double arc = angle * Math.PI / 180 * 600;
                int i0 = wp.SegmentAt(0.3), i1 = wp.SegmentAt(Math.Min(1, 0.3 + arc / wp.Length));
                woodshop.Wait(3);
                Assert.IsTrue(woodshop.Do(new CraftRequest(CraftRequestKind.BendWood, snath) { i0 = i0, i1 = i1, a = (float)angle, b = 0.4f, c = 600f, flag = true }, out why), "bend: " + why);
                Console.WriteLine($"  bent {angle:0}°: it has {woodshop.W.Piece(snath).segments.Sum(g => g.bend):0.0}° split {woodshop.W.Piece(snath).wood.split:0.00} soft {CraftWoodwork.Plasticity(woodshop.W.Piece(snath)):0.00}");
            }
            Console.WriteLine($"  guide: {CraftGuide.Next(woodshop.W, woodshop.Tools, woodshop.W.Piece(snath), null)?.text}");
            Assert.IsTrue(woodshop.Do(new CraftRequest(CraftRequestKind.StackWood, snath) { a = 0.9f, b = 0.9f, c = 0.8f }, out why), "stack: " + why);
            t0 = woodshop.Now;
            for (double t = 0; t < 48 * 3600 && woodshop.W.Piece(snath).wood.moisture > CraftWoodwork.DryEnough - 0.01; t += 60) woodshop.Wait(60);
            Console.WriteLine($"SEASONED after {(woodshop.Now - t0) / 60:0} min: moisture {woodshop.W.Piece(snath).wood.moisture:0.00} bend {woodshop.W.Piece(snath).segments.Sum(g => g.bend):0.0} checks {woodshop.W.Piece(snath).wood.checks:0.00}");
            Assert.IsTrue(woodshop.Do(new CraftRequest(CraftRequestKind.Move, snath) { location = CraftLocation.Air }, out why), why);

            // ---- the smithy: three bars of spring steel stacked and welded, drawn into the blade, its eye drifted
            var smithy = new Bench("smithy", CraftTools.Smithy, bag);
            var s = new Smith(smithy);
            s.Light();
            string blade = smithy.Put(StockBar(rules, "Spring steel"));
            StackWeld(s, blade, smithy.Put(StockBar(rules, "Spring steel")));
            StackWeld(s, blade, smithy.Put(StockBar(rules, "Spring steel")));
            CraftPiece bp = smithy.W.Piece(blade);
            Console.WriteLine($"WELDED: {bp.Length:0} x {bp.segments.Average(g => g.width):0} x {bp.segments.Average(g => g.thickness):0.0} mm {bp.Mass(rules):0} g seam {CraftForging.SeamOpen(bp):0.00} defects {Defects(bp)} t={smithy.Now - 100:0}s");
            double L = LengthFor(bp, st, 0.97);
            ForgeTo(s, blade, st, L, 60, 0.9, true);
            Console.WriteLine(Profile(rules, smithy.W.Piece(blade), st, L));
            Drift(s, blade, st.eyeAt, st.eyeDiameter);
            Straighten(s, blade);
            bp = smithy.W.Piece(blade);
            Console.WriteLine($"FORGED: shape {CraftEvaluator.ShapeScore(rules, bp, st):0.00} {bp.Length:0} mm {bp.Mass(rules):0} g eye {CraftAssembly.Eye(bp):0} blows {s.Blows} heats {s.Heats} t={(smithy.Now - 100) / 60:0} min defects {Defects(bp)}");
            // the quench window says water is harsh on spring steel: the tank is filled with oil first
            StringAssert.Contains("quenching oil", CraftLook.HarshQuench(rules, bp, rules.Quenchant(smithy.W.Quench.medium), CraftThermal.PresetQuenchants(rules, smithy.Tools, false)));
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.FillTank) { id = "oil" }, out why), "oil: " + why);
            Harden(s, blade);
            Temper(smithy, blade, 220, 300);
            Cool(smithy, blade, 60);
            bp = smithy.W.Piece(blade);
            Console.WriteLine($"HARDENED: {Metallurgy(rules, bp)} defects {Defects(bp)}");
            Assert.IsFalse(bp.HasDefect(CraftDefectKind.Crack), "hardened in oil, the blade is whole");
            string sn2 = woodshop.CarryTo(smithy, snath);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "scythe" }, out why), why);
            string asm = smithy.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "head", other = blade }, out why), "blade: " + why);
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "haft", other = sn2 }, out why), "snath: " + why);
            for (int k = 0; k < 12 && !CraftAssembly.JointComplete(scythe.Joint("head_haft"), smithy.W.Piece(asm).assembly.Joint("head_haft")); k++)
                JointBlow(smithy, asm, "head_haft", CraftJointWork.Wedge, 0.7);
            int g2 = GrindToGreen(smithy, blade, st, "coarse_wheel", 0.5, 100);
            EdgeSpan(smithy.W.Piece(blade), st, out int e0, out int e1);
            Cool(smithy, blade, 60);
            int n = HoneToKeen(smithy, blade, e0, e1, "medium_stone", st.edgeAngle / 2 + 1, 0.45, 1500);
            Console.WriteLine($"EDGE: {g2} wheel requests, {n} passes; {Metallurgy(rules, smithy.W.Piece(blade))}");
            Assert.IsTrue(smithy.Do(new CraftRequest(CraftRequestKind.Finish, asm), out why), "finish: " + why);
            CraftItemReport item = CraftEvaluator.Item(rules, smithy.W, smithy.W.Piece(asm));
            Console.WriteLine("ITEM: " + Report(item));
            Console.WriteLine("refused: " + string.Join(" | ", woodshop.Refused.Concat(smithy.Refused).Distinct().Take(12)));
        }

        /// <summary>A stroke across every row of a hide (the knife or the staking blade drawn from one side to the other): x,y pairs.</summary>
        private static void Rows(Bench b, string hide, Func<List<float>, bool> stroke)
        {
            CraftPiece h = b.W.Piece(hide);
            for (int y = 0; y < h.hide.height; y++)
            {
                b.Wait(1.0);
                var path = new List<float>();
                for (int x = 0; x <= h.hide.width; x += 2) { path.Add(x); path.Add(y + 0.5f); }
                stroke(path);
            }
        }

        /// <summary>A process hour of the tannery's vats is <c>1 / processHoursPerSecond</c> real seconds.</summary>
        private static void Hours(Bench b, double hours) => b.Wait(hours / b.W.Rules.Settings.processHoursPerSecond);

        [Test]
        public void Journey_PlatedBracers_RawHideAndStockBars_AtRealStations()
        {
            CraftRules rules = Rules();
            CraftDesign d = rules.Design("plated_bracers");
            var bag = new Dictionary<string, int> { ["lime"] = 2, ["bark"] = 4, ["neatsfoot_oil"] = 4 };

            // ---- the tannery: a raw cow hide fleshed, limed, unhaired, bark-tanned, dried on the rack with staking, oiled
            var tannery = new Bench("tannery", CraftTools.Tannery, bag);
            string hide = tannery.Put(CraftLeather.NewHide(rules, "cow", 11));
            Rows(tannery, hide, path => tannery.Do(new CraftRequest(CraftRequestKind.Scrape, hide) { points = path, flag = false, a = 0.55f, b = 0.8f }));
            Rows(tannery, hide, path => tannery.Do(new CraftRequest(CraftRequestKind.Scrape, hide) { points = path, flag = false, a = 0.55f, b = 0.8f }));
            Assert.IsTrue(tannery.Do(new CraftRequest(CraftRequestKind.FillVat) { i0 = 0, id = "lime", count = 1 }, out string why), "lime: " + why);
            Assert.IsTrue(tannery.Do(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Vat, i2 = 0 }, out why), "into the lime: " + why);
            Hours(tannery, 26);
            Assert.IsTrue(tannery.Do(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Air }, out why), why);
            Rows(tannery, hide, path => tannery.Do(new CraftRequest(CraftRequestKind.Scrape, hide) { points = path, flag = true, a = 0.4f, b = 0.8f }));
            Rows(tannery, hide, path => tannery.Do(new CraftRequest(CraftRequestKind.Scrape, hide) { points = path, flag = true, a = 0.4f, b = 0.8f }));
            Assert.IsTrue(tannery.Do(new CraftRequest(CraftRequestKind.FillVat) { i0 = 1, id = "bark", count = 2 }, out why), "bark: " + why);
            Assert.IsTrue(tannery.Do(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Vat, i2 = 1 }, out why), "into the bark: " + why);
            Hours(tannery, 80);
            CraftPieceReport tanned = CraftEvaluator.Piece(rules, tannery.W.Piece(hide));
            Console.WriteLine($"TANNED: tannage {tanned.Tannage:0.00} core {tanned.CoreTannage:0.00} thickness {tanned.Thickness:0.0} hair {tannery.W.Piece(hide).hide.cells.Where(c => c.present).Average(c => c.hair):0.00} rot {tannery.W.Piece(hide).hide.cells.Where(c => c.present).Average(c => c.rot):0.00} defects {Defects(tannery.W.Piece(hide))}; guide: {CraftGuide.Next(tannery.W, tannery.Tools, tannery.W.Piece(hide), null)?.text}");
            Assert.IsTrue(tannery.Do(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Rack }, out why), "rack: " + why);
            for (int k = 0; k < 14; k++)
            {
                Hours(tannery, 2);
                Rows(tannery, hide, path => tannery.Do(new CraftRequest(CraftRequestKind.Stake, hide) { points = path, a = 0.55f }));
            }
            Hours(tannery, 8);
            Assert.IsTrue(tannery.Do(new CraftRequest(CraftRequestKind.Move, hide) { location = CraftLocation.Air }, out why), why);
            Rows(tannery, hide, path => tannery.Do(new CraftRequest(CraftRequestKind.Condition, hide) { id = "neatsfoot_oil", points = path, a = 0.5f }));
            CraftPieceReport leather = CraftEvaluator.Piece(rules, tannery.W.Piece(hide));
            Console.WriteLine($"LEATHER: {CraftLook.Name(rules, tannery.W.Piece(hide), null)} tannage {leather.Tannage:0.00} supple {leather.Suppleness:0.00} stiff {leather.Stiffness:0.00} thickness {leather.Thickness:0.0} dried {tannery.W.Piece(hide).hide.dried} strength {leather.Strength:0.00}; guide: {CraftGuide.Next(tannery.W, tannery.Tools, tannery.W.Piece(hide), null)?.text}; t={(tannery.Now - 100) / 60:0} min");

            // ---- the armourer: the leather cut, two plates forged from a bar, riveted on
            var armourer = new Bench("armourer", CraftTools.Armourer, bag);
            var sm = new Smith(armourer);
            string hideHere = tannery.CarryTo(armourer, hide);
            var leathers = new List<string>();
            foreach ((int x, int y) in new[] { (2, 2), (6, 2) })
            {
                HashSet<string> before = Ids(armourer);
                armourer.Wait(4);
                if (armourer.Do(new CraftRequest(CraftRequestKind.Cut, hideHere) { id = "bracer", i0 = x, i1 = y, i2 = 0, a = 0.9f }, out why))
                    leathers.Add(armourer.W.Pieces.Select(q => q.id).First(q => !before.Contains(q)));
                else Console.WriteLine("cut: " + why);
            }
            CraftShapeTarget pt = d.Component("plates").shape;
            var plates = new List<string>();
            sm.Light();
            foreach (int k in new[] { 0, 1 })
            {
                string bar = armourer.Put(StockBar(rules, "Mild steel"));
                double L = 195;
                double keep = 195 * 65 * 1.8 / (200 * 30 * 6.0);
                string off = Chisel(sm, bar, keep);   // what a plate takes of the bar
                ForgeTo(sm, bar, pt, LengthFor(armourer.W.Piece(bar), pt), 30, 0.9);
                // dished with the dishing hammer, all along it
                for (int heat = 0; heat < 4; heat++)
                {
                    CraftPiece p = armourer.W.Piece(bar);
                    double dish = p.segments.Average(g => g.dish);
                    if (dish >= pt.dish - 0.5) break;
                    sm.Heat(bar, 900, 60);
                    for (int i = 0; i < p.segments.Count; i += 2) sm.Strike(bar, p.PositionOf(i), CraftStrikeTool.Dishing, CraftStrikeFace.Flat);
                }
                Cool(armourer, bar, 60);
                plates.Add(bar);
                CraftPiece pp = armourer.W.Piece(bar);
                Console.WriteLine($"PLATE {k}: shape {CraftEvaluator.ShapeScore(rules, pp, pt):0.00} {pp.Length:0} x {pp.segments.Average(g => g.width):0} x {pp.segments.Average(g => g.thickness):0.0} dish {pp.segments.Average(g => g.dish):0.0} {pp.Mass(rules):0} g defects {Defects(pp)}");
            }
            string rivets = armourer.Put(CraftDefaults.FastenerSet(rules, CraftFastenerKind.Rivet, 10, 4, (CraftDefaults.Iron, 0.998), (CraftDefaults.Carbon, 0.002)));
            Assert.IsTrue(armourer.Do(new CraftRequest(CraftRequestKind.StartAssembly) { id = "plated_bracers" }, out why), why);
            string asm = armourer.W.Pieces.Last(x => x.kind == CraftPieceKind.Assembly).id;
            for (int i = 0; i < leathers.Count; i++) Assert.IsTrue(armourer.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "leather", i0 = i, other = leathers[i] }, out why), "leather: " + why);
            for (int i = 0; i < plates.Count; i++) Assert.IsTrue(armourer.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "plates", i0 = i, other = plates[i] }, out why), "plate: " + why);
            Assert.IsTrue(armourer.Do(new CraftRequest(CraftRequestKind.Attach, asm) { id = "rivets", other = rivets }, out why), "rivets: " + why);
            for (int k = 0; k < 40 && armourer.W.Piece(asm).assembly.Joint("plates_riveted").holes < 8; k++)
            {
                armourer.Wait(0.6);
                armourer.Do(new CraftRequest(CraftRequestKind.JointWork, asm) { id = "plates_riveted", i0 = (int)CraftJointWork.Punch, count = 1, a = 0.05f, b = 0.05f });
            }
            for (int k = 0; k < 30 && armourer.W.Piece(asm).assembly.Joint("plates_riveted").rivets < 8; k++)
                JointBlow(armourer, asm, "plates_riveted", CraftJointWork.Rivet, 0.6);
            JointState js = armourer.W.Piece(asm).assembly.Joint("plates_riveted");
            Console.WriteLine($"RIVETED: holes {js.holes} rivets {js.rivets} quality {js.rivetQuality:0.00}; missing: {string.Join("; ", CraftAssembly.Missing(armourer.W, armourer.W.Piece(asm)))}");
            Assert.IsTrue(armourer.Do(new CraftRequest(CraftRequestKind.Finish, asm), out why), "finish: " + why);
            CraftItemReport item = CraftEvaluator.Item(rules, armourer.W, armourer.W.Piece(asm));
            Console.WriteLine("ITEM: " + Report(item));
            Console.WriteLine("refused: " + string.Join(" | ", tannery.Refused.Concat(armourer.Refused).Distinct().Take(12)));
        }

        // ================================================================== run 2: more rules the journeys showed
        /// <summary>
        /// A bloom hammered sound at welding heat closes its pores and gives up its slag: it gets denser and smaller, never
        /// heavier. (The ore journey's bloom of 988 g came out a bar of 1196 g.)
        /// </summary>
        [Test]
        public void Rule_ABloomHammeredSound_WeighsNoMoreThanItDid()
        {
            CraftRules rules = Rules();
            var b = new Bench("smithy", CraftTools.Smithy, null);
            var sm = new Smith(b);
            sm.Light();
            CraftPiece bloom = CraftDefaults.Bar(rules, 80, 60, 35, Metal("Wrought iron"));
            bloom.bloom = true; // as ExtractBloom makes it: a sponge of iron and slag
            foreach (MetalSegment g in bloom.segments) { g.porosity = 0.3; g.slag = 3; }
            string id = b.Put(bloom);
            CraftPiece p0 = b.W.Piece(id);
            double before = p0.Mass(rules), slag0 = p0.segments.Sum(g => g.slag);
            double vol0 = p0.segments.Sum(g => g.Volume(p0.edges));
            double weld = CraftForging.WeldingHeat(rules, p0.Profile(rules));
            sm.Depth = 0.85;
            for (int heat = 0; heat < 4; heat++)
            {
                sm.Heat(id, weld - 40, 150);
                for (int k = 0; k < 14; k++)
                {
                    CraftPiece p = b.W.Piece(id);
                    sm.Strike(id, p.PositionOf((k * 5) % p.segments.Count), CraftStrikeTool.Flat, CraftStrikeFace.Flat);
                }
            }
            CraftPiece q = b.W.Piece(id);
            double after = q.Mass(rules), slag1 = q.segments.Sum(g => g.slag), vol1 = q.segments.Sum(g => g.Volume(q.edges));
            Console.WriteLine($"bloom: {before:0} g (porosity 0.30, slag {slag0:0} g, {vol0 / 1000:0} cm3) -> {after:0} g (porosity {q.segments.Average(g => g.porosity):0.00}, slag {slag1:0} g, {vol1 / 1000:0} cm3)");
            Assert.LessOrEqual(after, before + 0.5, $"closing the pores at welding heat made metal: {before:0} g -> {after:0} g (the volume was kept, {vol0 / 1000:0} -> {vol1 / 1000:0} cm3, while the porosity fell, so Volume x density x (1 - porosity) grew)");
        }

        /// <summary>
        /// A spear's head cast in its own pattern mould is meant for the spear's head - a double-edged leaf - and should be
        /// ground as one, as a bar given that purpose is (the casting gives it its purpose's edges, as SetIntent does: CraftPiece.IntendedEdges).
        /// </summary>
        [Test]
        public void Rule_ASpearHeadCastInItsPatternMould_IsDoubleEdged()
        {
            CraftRules rules = Rules();
            CraftDesign spear = rules.Design("spear");
            var smeltery = new Bench("smeltery", CraftTools.Smeltery, new Dictionary<string, int> { ["borax"] = 4 });
            var sm = new Smith(smeltery);
            for (int i = 0; i < 3; i++)
            {
                string bar = smeltery.Put(StockBar(rules, "Bronze"));
                Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.Move, bar) { location = CraftLocation.Melt }, out string w), w);
            }
            Assert.IsTrue(smeltery.Do(new CraftRequest(CraftRequestKind.AddFuel) { id = "charcoal", count = 2, flag = true }, out string why), why);
            string head = MeltAndPour(sm, CraftRules.PatternMoldId("spear", "head"));
            CraftPiece hp = smeltery.W.Piece(head);
            Console.WriteLine($"cast spear head: meant for {hp.intentDesign}/{hp.intentRole}, edges {hp.edges}; the design's head is {spear.Component("head").shape.edge}");
            Assert.AreEqual("spear", hp.intentDesign);
            Assert.AreEqual(2, hp.edges, "a spear head cast in its pattern mould takes the purpose but not the edges: CraftSmelting.Cast sets edges = 0, and the wheel's first bevel then makes it single-edged");
        }

        /// <summary>
        /// The grind strip judges each stretch of an edge "as the item will be judged". A stretch the hammer left a little
        /// thin or narrow, with no edge ground on it at all, still needs its edge: the strip must ask for it (yellow, to
        /// grind), not call it "off" (orange: "met at another angle, or a little narrow") or "ground away past the design"
        /// (red) - the window then says "Coat it last" and the item is judged "the edge is not ground along all of it".
        /// </summary>
        [Test]
        public void Rule_AStretchForgedALittleThin_StillAsksForItsEdge()
        {
            CraftRules rules = Rules();
            CraftShapeTarget bt = rules.Design("dagger").Component("blade").shape;
            bt.At(0.7f, out float tw, out float tt);
            var seen = new List<string>();
            foreach (double thin in new[] { 0.0, 0.06, 0.15 })
            {
                var g = new MetalSegment { length = 18, width = tw, thickness = tt * (1 - thin) };
                CraftSurface.Judgement j = CraftSurface.Judge(g, tw, tt, bt.edgeAngle, true);
                seen.Add($"{thin * 100:0}% thin, no bevel (edge {g.EdgeThickness:0.0} mm thick): {j.progress}");
            }
            Console.WriteLine(string.Join("\n", seen));
            foreach (string line in seen)
                StringAssert.EndsWith("ToGrind", line, "a stretch with no edge on it at all is not asked to be ground: " + line);
        }
    }
}
#endif
