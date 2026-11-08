#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CraftingSystem.Tests
{
    /// <summary>
    /// Edit Mode tests of the station window's shared foundation that runs without Unity: the pointer on the work surface
    /// (one gesture per button, samples by distance, the wheel in notches), the timing of messages, confirmations and
    /// settings, and the workshop's rules for the window (what can be taken, how the pieces are listed); the shared
    /// models every area relies on - the stroke (tiring by time, passes that are real strokes), the practice a request
    /// gives, a pattern's cutting line, the glow of heat, where a flux works, and the joints an optional part left off;
    /// and the requests - their answers (waiting for the copy of the workshop, given up on after a while), the settings a
    /// hand turns (only the latest, ten times a second) and the guard that holds every player's requests to real hands
    /// (all or nothing, sized for honest work, a reason for every refusal, only clamping, work the station refused given
    /// back).
    /// </summary>
    public class CraftFoundationTests
    {
        private static CraftRules cached;
        private static CraftRules Rules() => cached ??= CraftDefaults.Rules();

        private static CraftWorkshop Workshop(CraftTools tools = CraftTools.Everything) =>
            new CraftWorkshop(Rules(), tools, 18, 1, 3, "water", "foundation");

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        /// <summary>A sampler and what it reported.</summary>
        private static CraftPointerSampler Sampler(List<(CraftPointer phase, Vector2 at, float time, float scroll)> into)
        {
            var s = new CraftPointerSampler();
            s.Sampled += (phase, at, time, scroll) => into.Add((phase, at, time, scroll));
            return s;
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        // ------------------------------------------------------------------ the pointer
        [Test]
        public void Pointer_TheLeftButtonOwnsTheGesture_RightIsOnlyCancel_MiddleNothing()
        {
            var got = new List<(CraftPointer phase, Vector2 at, float time, float scroll)>();
            CraftPointerSampler s = Sampler(got);
            s.Press(CraftPointerButton.Right, 1, V(50, 50), 0f);
            Assert.IsFalse(s.Held, "the right button never begins a gesture");
            s.Press(CraftPointerButton.Middle, 1, V(50, 50), 0.1f);
            s.Release(CraftPointerButton.Middle, 1, V(50, 50), 0.2f, 0.016f);
            s.Press(CraftPointerButton.Left, 1, V(100, 100), 1f);
            Assert.IsTrue(s.Held);
            s.Press(CraftPointerButton.Right, 1, V(100, 100), 1.1f);
            s.Release(CraftPointerButton.Right, 1, V(100, 100), 1.2f, 0.016f);
            Assert.IsTrue(s.Held, "a right click in the middle of a gesture does not end it");
            s.Press(CraftPointerButton.Left, 2, V(300, 300), 1.3f);
            s.Release(CraftPointerButton.Left, 2, V(300, 300), 1.4f, 0.016f);
            Assert.IsTrue(s.Held, "another finger neither begins nor ends the gesture");
            s.Release(CraftPointerButton.Left, 1, V(100, 100), 1.5f, 0.016f);
            Assert.IsFalse(s.Held);
            s.Release(CraftPointerButton.Left, 1, V(100, 100), 1.6f, 0.016f);
            CollectionAssert.AreEqual(new[] { CraftPointer.Cancel, CraftPointer.Down, CraftPointer.Cancel, CraftPointer.Up }, got.Select(g => g.phase).ToArray(),
                "right: Cancel once; middle: nothing; the left press and release: Down and Up");
            got.Clear();
            s.Press(CraftPointerButton.Left, 1, V(10, 10), 2f);
            s.Press(CraftPointerButton.Left, 1, V(40, 40), 3f);
            CollectionAssert.AreEqual(new[] { CraftPointer.Down, CraftPointer.Up, CraftPointer.Down }, got.Select(g => g.phase).ToArray(),
                "a release that never came (the window lost the focus) ends that gesture when the same button presses again");
            Assert.IsTrue(s.Held);
            s.Reset();
            Assert.IsFalse(s.Held, "the window closing ends it too");
        }

        [Test]
        public void Pointer_DragsAreSampledByDistance_AStillHandSendsNothing()
        {
            var got = new List<(CraftPointer phase, Vector2 at, float time, float scroll)>();
            CraftPointerSampler s = Sampler(got);
            s.Press(CraftPointerButton.Left, 1, V(0, 0), 0f);
            s.Drag(1, V(1, 0), 0.016f, 0.016f);
            s.Drag(1, V(1.5f, 0.5f), 0.032f, 0.016f);
            s.Drag(1, V(1, 0), 0.048f, 0.016f);
            Assert.AreEqual(1, got.Count, "the wobble of a held click (under 2 px) is no drag");
            s.Drag(1, V(3, 0), 0.064f, 0.016f);
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(CraftPointer.Drag, got[1].phase);
            Assert.AreEqual(3f, got[1].at.x, 1e-4f, "a sample once it is 2 px from the last one");
            got.Clear();
            s.Drag(1, V(3, 20), 0.08f, 0.016f);
            Assert.AreEqual(4, got.Count, "a 20 px jump is filled in at most 6 px apart");
            Assert.AreEqual(20f, got[3].at.y, 1e-4f, "the last sample is where the pointer is");
            for (int i = 0; i < got.Count; i++)
            {
                Assert.AreEqual(5f * (i + 1), got[i].at.y, 1e-3f, "evenly spaced along the way");
                Assert.Greater(got[i].time, 0.064f - 1e-6f, "each filled-in sample has its own time, within the frame");
                if (i > 0) Assert.Greater(got[i].time, got[i - 1].time);
            }
            Assert.AreEqual(0.08f, got[3].time, 1e-6f);
            Assert.AreEqual(0.08f, s.SampleTime, 1e-6f, "the time of the sample last reported");
        }

        [Test]
        public void Pointer_TheSameStrokeAtAnyFrameRate_GivesSamplesTwoToSixPixelsApart()
        {
            foreach (float hz in new[] { 30f, 60f, 144f, 240f })
            {
                var got = new List<(CraftPointer phase, Vector2 at, float time, float scroll)>();
                CraftPointerSampler s = Sampler(got);
                float speed = 600f, length = 300f, dt = 1f / hz;
                s.Press(CraftPointerButton.Left, 1, V(0, 0), 0f);
                float t = 0f;
                while (t * speed < length)
                {
                    t += dt;
                    s.Drag(1, V(Mathf.Min(length, t * speed), 0f), t, dt);
                }
                s.Release(CraftPointerButton.Left, 1, V(length, 0f), t, dt);
                List<Vector2> drags = got.Where(g => g.phase == CraftPointer.Drag).Select(g => g.at).ToList();
                Vector2 last = V(0, 0);
                foreach (Vector2 p in drags)
                {
                    float d = Vector2.Distance(last, p);
                    Assert.That(d, Is.InRange(CraftPointerSampler.MinStep - 1e-3f, CraftPointerSampler.MaxStep + 1e-3f), $"{hz} Hz: samples 2-6 px apart");
                    last = p;
                }
                Assert.That(drags.Count, Is.InRange((int)(length / CraftPointerSampler.MaxStep), (int)(length / CraftPointerSampler.MinStep)), $"{hz} Hz");
                Assert.AreEqual(CraftPointer.Up, got[got.Count - 1].phase);
            }
        }

        [Test]
        public void Pointer_AReleaseSamplesTheLastOfTheWay_ThenUp_AndMovesStartAfresh()
        {
            var got = new List<(CraftPointer phase, Vector2 at, float time, float scroll)>();
            CraftPointerSampler s = Sampler(got);
            s.Press(CraftPointerButton.Left, 1, V(0, 0), 0f);
            s.Release(CraftPointerButton.Left, 1, V(10, 0), 0.1f, 0.016f);
            CollectionAssert.AreEqual(new[] { CraftPointer.Down, CraftPointer.Drag, CraftPointer.Drag, CraftPointer.Up }, got.Select(g => g.phase).ToArray());
            Assert.AreEqual(10f, got[3].at.x, 1e-4f, "Up where the button came up");
            got.Clear();
            s.Hover(V(11, 0), 0.2f, 0.016f);
            Assert.AreEqual(0, got.Count, "a move under 2 px from where it came up: nothing");
            s.Hover(V(30, 0), 0.216f, 0.016f);
            Assert.That(got.All(g => g.phase == CraftPointer.Move) && got.Count == 4, "a move of 20 px: 4 Move samples");
            got.Clear();
            s.Press(CraftPointerButton.Left, 1, V(30, 0), 0.3f);
            s.Hover(V(80, 0), 0.32f, 0.016f);
            Assert.AreEqual(1, got.Count, "no Move while a gesture is under way");
            s.Release(CraftPointerButton.Left, 1, V(30, 0), 0.4f, 0.016f, false);
            got.Clear();
            s.Hover(V(200, 200), 0.5f, 0.016f);
            Assert.AreEqual(1, got.Count, "let go off the surface: the next move is one sample where the pointer came back");
            Assert.AreEqual(200f, got[0].at.x, 1e-4f);
            s.Exit();
            got.Clear();
            s.Hover(V(20, 20), 0.6f, 0.016f);
            Assert.AreEqual(1, got.Count, "nothing filled in across the outside of the surface");
        }

        [Test]
        public void Wheel_ComesInWholeNotches_OneAFrame()
        {
            var got = new List<(CraftPointer phase, Vector2 at, float time, float scroll)>();
            CraftPointerSampler s = Sampler(got);
            s.Scroll(6f, 6f, V(1, 1), 0f, 1);
            s.Scroll(6f, 6f, V(1, 1), 0f, 1);
            Assert.AreEqual(1, got.Count, "one notch a frame");
            Assert.AreEqual(1f, got[0].scroll);
            s.Tick(0.016f, 2);
            Assert.AreEqual(2, got.Count, "the notch left waiting comes the next frame");
            s.Tick(0.032f, 3);
            Assert.AreEqual(2, got.Count);
            got.Clear();
            s.Scroll(2f, 6f, V(1, 1), 0.1f, 4);
            s.Scroll(2f, 6f, V(1, 1), 0.11f, 5);
            Assert.AreEqual(0, got.Count, "a trackpad's small steps...");
            s.Scroll(2f, 6f, V(1, 1), 0.12f, 6);
            Assert.AreEqual(1, got.Count, "...add up to a notch");
            got.Clear();
            s.Scroll(3f, 6f, V(1, 1), 0.2f, 7);
            s.Scroll(-6f, 6f, V(1, 1), 0.21f, 8);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(-1f, got[0].scroll, "turned back: what was left of the other way is dropped");
            got.Clear();
            s.Scroll(60f, 6f, V(1, 1), 0.3f, 9);
            for (int f = 10; f < 30; f++) s.Tick(0.3f + f * 0.016f, f);
            Assert.AreEqual(1 + CraftPointerSampler.MaxNotchesWaiting, got.Count, "a fast spin does not run on");
            got.Clear();
            s.Scroll(3f, 6f, V(1, 1), 1f, 40);
            s.Scroll(3f, 6f, V(1, 1), 2f, 41);
            Assert.AreEqual(0, got.Count, "half a notch from long ago is forgotten");
            var learnt = new CraftPointerSampler();
            var notches = new List<float>();
            learnt.Sampled += (phase, at, time, scroll) => notches.Add(scroll);
            learnt.Scroll(-120f, 0f, V(1, 1), 0f, 1);
            learnt.Scroll(-60f, 0f, V(1, 1), 0.01f, 2);
            learnt.Scroll(-60f, 0f, V(1, 1), 0.02f, 3);
            CollectionAssert.AreEqual(new[] { -1f, -1f }, notches, "a notch's size not known: the largest single step is taken for one");
        }

        // ------------------------------------------------------------------ timing
        [Test]
        public void Messages_StayLongEnoughToRead()
        {
            Assert.AreEqual(6f, CraftWindowRules.MessageSeconds("Too hot to carry."), 1e-5f, "at least 6 s");
            Assert.AreEqual(2f + 0.06f * 200, CraftWindowRules.MessageSeconds(new string('a', 200)), 1e-4f, "a long one: 2 s and 60 ms a character");
            Assert.AreEqual(3, CraftWindowRules.VisibleLength("<color=#ffb070>abc</color>"), "tags are not read");
            Assert.AreEqual(5, CraftWindowRules.VisibleLength("a < b"), "a lone < is text");
            Assert.AreEqual(6f, CraftWindowRules.MessageSeconds(null), 1e-5f);
        }

        [Test]
        public void Confirm_AsksTwice_AndOnlyForTheSameThing()
        {
            var c = new CraftConfirm();
            Assert.IsFalse(c.Ask("scrap:a", 0f), "the first click asks");
            Assert.IsTrue(c.Ask("scrap:a", 1f), "the second, on the same thing, does it");
            Assert.IsFalse(c.Ask("scrap:a", 1.5f), "a third starts over");
            Assert.IsFalse(c.Ask("scrap:a", 4.5f), "too late: it asks again");
            Assert.IsTrue(c.Ask("scrap:a", 5f));
            Assert.IsFalse(c.Ask("scrap:a", 10f));
            Assert.IsFalse(c.Ask("scrap:b", 10.5f), "another piece is another question");
            Assert.IsFalse(c.Ask("scrap:a", 11f));
            c.Clear();
            Assert.IsFalse(c.Ask("scrap:a", 11.5f), "forgotten when the window closed");
        }

        [Test]
        public void Setting_SentAtOnce_ThenFiveTimesASecond_AndTheLastValueWhenLetGo()
        {
            var pace = new CraftSettingPace();
            var sent = new List<float>();
            pace.Grab();
            for (int frame = 0; frame <= 120; frame++)
            {
                float t = frame / 60f;
                pace.Changed(frame / 120f);
                if (pace.Due(t, out float v)) sent.Add(v);
            }
            Assert.AreEqual(0f, sent[0], 1e-6f, "the first change goes at once");
            Assert.That(sent.Count, Is.InRange(9, 11), "then at most 5 a second while the hand moves it (2 s)");
            Assert.IsFalse(pace.MayShow(2f, 0f, 0.001f), "the workshop's value is not shown under the hand");
            pace.Changed(0.9f);
            Assert.IsTrue(pace.LetGo(2.01f, out float last));
            Assert.AreEqual(0.9f, last, 1e-6f, "the value the hand let go at is sent at once");
            Assert.IsFalse(pace.MayShow(2.5f, 0.5f, 0.001f), "nor while the workshop still shows the old value");
            Assert.IsTrue(pace.MayShow(2.6f, 0.9f, 0.001f), "once the workshop shows it, it is shown");
            pace.Changed(0.3f);
            Assert.IsTrue(pace.Due(3f, out _));
            Assert.IsFalse(pace.MayShow(3.5f, 0.9f, 0.001f));
            Assert.IsTrue(pace.MayShow(4.6f, 0.9f, 0.001f), "or after 1.5 s (the workshop kept its own value)");
            Assert.IsFalse(pace.LetGo(5f, out _), "nothing waiting: nothing sent when let go");
        }

        [Test]
        public void VatFire_TurnedByAHair_RecordsNothing()
        {
            CraftWorkshop w = Workshop();
            int before = w.History.Count;
            Assert.IsTrue(w.SetVatHeat(0, 0.5, out string why), why);
            Assert.AreEqual(before + 1, w.History.Count);
            Assert.IsTrue(w.SetVatHeat(0, 0.503, out why), why);
            Assert.AreEqual(before + 1, w.History.Count, "a change under 0.005 is no change: the history (the replay proof) keeps only real ones");
            Assert.IsTrue(w.SetVatHeat(0, 0.52, out why), why);
            Assert.AreEqual(before + 2, w.History.Count);
            Assert.AreEqual(0.52, w.Vats[0].heat, 1e-6);
            Assert.IsFalse(w.SetVatHeat(9, 0.5, out _), "no such vat");
        }

        // ------------------------------------------------------------------ the workshop for the window
        [Test]
        public void Take_RefusesPiecesInsideAWorkingPlace_UntilTheyAreTakenOut()
        {
            CraftWorkshop w = Workshop();
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 25, 6, Metal("Mild steel")));
            Assert.IsTrue(CraftWorkshop.CanTake(w.Piece(bar), out string why), why);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out why), why);
            Assert.IsFalse(CraftWorkshop.CanTake(w.Piece(bar), out why), "not straight out of the tank");
            StringAssert.Contains("tank", why);
            Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out why), why);
            Assert.IsTrue(CraftWorkshop.CanTake(w.Piece(bar), out why), "lifted out, it can be taken");

            foreach (CraftLocation inside in new[] { CraftLocation.Furnace, CraftLocation.Oven, CraftLocation.Ashes, CraftLocation.Vat, CraftLocation.Steam, CraftLocation.Melt, CraftLocation.Assembled })
            {
                CraftPiece p = CraftDefaults.Bar(w.Rules, 100, 20, 5, Metal("Mild steel"));
                p.location = inside;
                Assert.IsFalse(CraftWorkshop.CanTake(p, out why), inside.ToString());
                Assert.IsNotEmpty(why, "and says why");
            }
            foreach (CraftLocation outside in new[] { CraftLocation.Air, CraftLocation.Frame, CraftLocation.Rack, CraftLocation.Stack })
            {
                CraftPiece p = CraftDefaults.Bar(w.Rules, 100, 20, 5, Metal("Mild steel"));
                p.location = outside;
                Assert.IsTrue(CraftWorkshop.CanTake(p, out why), outside + ": " + why);
            }
            CraftPiece hot = CraftDefaults.Bar(w.Rules, 100, 20, 5, Metal("Mild steel"));
            foreach (MetalSegment s in hot.segments) s.body.temp = s.edge.temp = 600;
            Assert.IsFalse(CraftWorkshop.CanTake(hot, out why), "too hot to carry");
            Assert.IsFalse(CraftWorkshop.CanTake(null, out _));
        }

        // ------------------------------------------------------------------ the stroke
        /// <summary>
        /// Strokes along the work for <paramref name="seconds"/>, sampled <paramref name="rate"/> times a second:
        /// x = 0.5 + <paramref name="amplitude"/> × sin(2π <paramref name="hz"/> t), y following x (the edge leads one way).
        /// </summary>
        private static CraftStroke Strokes(CraftHands hands, double rate, double seconds, double amplitude, double hz, double pressure = 0.6, CraftStroke into = null)
        {
            CraftStroke s = into ?? new CraftStroke();
            int n = (int)Math.Round(seconds * rate);
            for (int k = 0; k <= n; k++)
            {
                double t = k / rate, x = 0.5 + amplitude * Math.Sin(2 * Math.PI * hz * t);
                s.Add(t, x, x, 15, pressure, hands);
            }
            return s;
        }

        [Test]
        public void Stroke_TiresTheArmByTheTimeItWorks_NotByTheFrameRate()
        {
            var tired = new List<double>();
            foreach (double fps in new[] { 30.0, 60.0, 144.0 })
            {
                var hands = new CraftHands(3);
                Strokes(hands, fps, 5, 0.3, 1);
                tired.Add(hands.fatigue);
            }
            Assert.Greater(tired[1], 0.15, "five seconds of stroking tires the arm");
            Assert.AreEqual(tired[1], tired[0], 0.01, "30 samples a second tire it as much as 60");
            Assert.AreEqual(tired[1], tired[2], 0.01, "and 144 no more");

            // Work cleared and sent in short batches (the wheel sends every 0.2 s) tires it as one long stroke.
            var batched = new CraftHands(3);
            var stroke = new CraftStroke();
            for (int k = 0; k <= 300; k++)
            {
                if (k % 12 == 0) stroke.Clear();
                double t = k / 60.0;
                stroke.Add(t, 0.5 + 0.3 * Math.Sin(2 * Math.PI * t), 0.5, 15, 0.6, batched);
            }
            Assert.AreEqual(tired[1], batched.fatigue, 0.01);

            // A still hand sends no samples: a long pause counts as one movement at most, not as work.
            var paused = new CraftHands(3);
            var p = new CraftStroke();
            p.Add(0, 0.2, 0.5, 15, 0.6, paused);
            p.Add(30, 0.21, 0.5, 15, 0.6, paused);
            Assert.Less(paused.fatigue, 0.01);
        }

        [Test]
        public void Stroke_APassIsARealStroke_NotAWiggle()
        {
            foreach (double fps in new[] { 30.0, 60.0, 144.0 })
            {
                Assert.AreEqual(0, Strokes(null, fps, 1, 0.02, 10).Passes, $"a tiny fast wiggle is no stroke ({fps} samples a second)");
                Assert.AreEqual(5, Strokes(null, fps, 3, 0.15, 1).Passes, $"a sweep of 0.3 back and forth, six turns in three seconds ({fps} samples a second)");
            }
            // A quick scrub of real length counts no faster than three passes a second (and one to start with).
            CraftStroke scrub = Strokes(null, 60, 2, 0.15, 4);
            Assert.LessOrEqual(scrub.Passes, 1 + 3 * 2);
            Assert.GreaterOrEqual(scrub.Passes, 5);

            // Each pass has its own direction and hold: here the edge leads on the way up, trails on the way down.
            CraftStroke full = Strokes(null, 60, 3, 0.3, 0.5);
            Assert.AreEqual(4, full.Passes, "0.5 → 0.8 → 0.2 → 0.8 → 0.5: four runs long enough");
            Assert.AreEqual(3, full.PassRuns.Count, "three have turned; the fourth is under way");
            Assert.IsTrue(full.PassRuns[0].leading);
            Assert.IsFalse(full.PassRuns[1].leading);
            Assert.IsTrue(full.PassRuns[2].leading);
            Assert.AreEqual(0.6, full.PassRuns[1].Length, 0.01);
            Assert.AreEqual(15, full.PassRuns[1].angle, 1e-6);
            Assert.AreEqual(0, full.PassRuns[1].wobble, 1e-6);
            Assert.IsTrue(full.Current(out CraftPass last));
            Assert.IsFalse(last.leading);
            Assert.AreEqual(0.8, last.from, 0.01);

            // A turn needs the hand to come back a little: a stumble mid-stroke does not split it.
            var steady = new CraftStroke();
            double[] xs = { 0.1, 0.2, 0.3, 0.4, 0.5, 0.496, 0.6, 0.7, 0.8, 0.9 };
            for (int k = 0; k < xs.Length; k++) steady.Add(k * 0.1, xs[k], xs[k], 15, 0.5, null);
            Assert.AreEqual(1, steady.Passes);
            Assert.AreEqual(0, steady.PassRuns.Count, "it has not turned yet");
            Assert.IsTrue(steady.Current(out CraftPass one));
            Assert.AreEqual(0.8, one.Length, 1e-6);

            // A whetstone can ask for longer strokes.
            Assert.AreEqual(0, Strokes(null, 60, 3, 0.15, 1, 0.6, new CraftStroke(0.5)).Passes, "runs of 0.3 are not strokes of half the stone");
            var old = new CraftStroke();
            Strokes(null, 60, 3, 0.15, 1, 0.6, old);
            old.Clear();
            Assert.AreEqual(0, old.Passes);
            Assert.AreEqual(0, old.PassRuns.Count, "a new stroke starts with no passes");
        }

        // ------------------------------------------------------------------ practice
        [Test]
        public void Practice_WorkOnWoodIsWoodwork_AndTheFurnacesPourIsSmithing()
        {
            CraftWorkshop w = Workshop();
            string wood = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 400, 30, 30));
            string bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 20, 6, Metal("Mild steel")));
            foreach (CraftRequestKind k in new[] { CraftRequestKind.Split, CraftRequestKind.Coat, CraftRequestKind.Descale, CraftRequestKind.Grind })
            {
                Assert.AreEqual(CraftDiscipline.Woodwork, CraftMastery.OfRequest(w.Rules, w, new CraftRequest(k, wood)), $"{k} of wood");
                Assert.AreEqual(CraftDiscipline.Smithing, CraftMastery.OfRequest(w.Rules, w, new CraftRequest(k, bar)), $"{k} of metal");
            }
            Assert.AreEqual(CraftDiscipline.Smithing, CraftMastery.OfRequest(w.Rules, w, new CraftRequest(CraftRequestKind.TakeMelt)), "running the furnace's metal into a mould");
            Assert.AreEqual(CraftMastery.WorkExperience(new CraftRequest(CraftRequestKind.Pour)), CraftMastery.WorkExperience(new CraftRequest(CraftRequestKind.TakeMelt)),
                "a pour from the furnace teaches as much as one from the crucible");
            string far = w.SplitPiece(wood, 0.5, out string why);
            Assert.IsNotNull(far, why);
            CraftPiece sawn = w.Piece(wood);
            Assert.AreEqual("Sawn to length", sawn.record[sawn.record.Count - 1].text, "wood is sawn, not cut with a chisel");
        }

        [Test]
        public void Practice_TheRankUpLineReadsWell()
        {
            var notes = new List<CraftMasteryNote>();
            string line = CraftMastery.Practise(notes, CraftDiscipline.Tailoring, CraftMastery.ExperienceFor(CraftMastery.RankStart(CraftRank.Apprentice)) + 0.01, false, 0);
            const string head = "You are an apprentice tailor now.";
            StringAssert.StartsWith(head, line);
            if (line.Length > head.Length + 1)
                Assert.IsTrue(char.IsUpper(line[head.Length + 1]), "what it opens starts a sentence: " + line);
            double more = CraftMastery.ExperienceFor(CraftMastery.RankStart(CraftRank.Journeyman)) - notes[0].xp + 0.01;
            StringAssert.StartsWith("You are a journeyman tailor now.", CraftMastery.Practise(notes, CraftDiscipline.Tailoring, more, false, 0));
        }

        // ------------------------------------------------------------------ patterns
        private static List<(float x, float y)> Points(List<Vector2> loop) => loop.Select(v => (v.x, v.y)).ToList();

        [Test]
        public void PatternOutline_RunsAlongTheCellsEdges()
        {
            var strap = new CraftPattern { id = "strap", rows = { "XXX" } };
            List<List<Vector2>> o = strap.Outline();
            Assert.AreEqual(1, o.Count);
            Assert.AreEqual(8, o[0].Count, "a point at every cell corner round a 3 × 1 strip: 3 + 1 + 3 + 1 sides");
            Assert.AreEqual(0.5f, CraftPattern.NearestOnOutline(o, new Vector2(1.5f, 0.5f), out Vector2 near, out int loop, out int side), 1e-5f,
                "the middle of a one-cell strip is half a cell from the line, not on it");
            Assert.AreEqual(0, loop);
            Assert.GreaterOrEqual(side, 0);
            Assert.AreEqual(1.5f, near.x, 1e-5f);
            Assert.AreEqual(0f, CraftPattern.NearestOnOutline(o, new Vector2(2f, 0f), out _, out _, out _), 1e-5f, "on the line");
            Assert.AreEqual(1f, CraftPattern.NearestOnOutline(o, new Vector2(4f, 0.5f), out _, out _, out _), 1e-5f, "a cell beyond its end");

            // Rows run down from the top: the L's single cell is the top-left one.
            var l = new CraftPattern { rows = { "X.", "XX" } };
            List<(float x, float y)> lp = Points(l.Outline()[0]);
            Assert.AreEqual(8, lp.Count);
            CollectionAssert.Contains(lp, (1f, 0f), "the top row ends after one cell");
            CollectionAssert.Contains(lp, (2f, 2f), "the bottom row runs two");
            CollectionAssert.DoesNotContain(lp, (2f, 0f));
            // Turned a quarter, it is the same line on its side.
            Assert.AreEqual(8, l.Outline(1)[0].Count);

            Assert.AreEqual(2, new CraftPattern { rows = { "X.", ".X" } }.Outline().Count, "cells touching only at a corner are cut round each");
            List<List<Vector2>> ring = new CraftPattern { rows = { "XXX", "X.X", "XXX" } }.Outline();
            Assert.AreEqual(2, ring.Count, "a ring: its outer edge and its hole");
            CollectionAssert.AreEquivalent(new[] { 4, 12 }, ring.Select(r => r.Count).ToArray());
            CollectionAssert.IsEmpty(new CraftPattern().Outline());
        }

        // ------------------------------------------------------------------ heat
        [Test]
        public void Heat_TheGlowShowsWhereTheWordsSayItDoes()
        {
            foreach (bool dark in new[] { false, true })
            {
                float lastA = 0f, lastG = 0f;
                for (int t = 300; t <= 1400; t += 5)
                {
                    Color c = CraftMetallurgy.HeatColor(t, dark);
                    string name = CraftMetallurgy.HeatName(t, dark);
                    Assert.AreEqual(name == "black heat" || name == "", c.a <= 0f, $"{t} °C {(dark ? "in the dark" : "by day")}: '{name}', glow {c.a}");
                    Assert.GreaterOrEqual(c.a, lastA - 1e-6f, "brighter as it heats");
                    if (c.a > 0f) { Assert.GreaterOrEqual(c.g, lastG - 1e-6f, "and yellower"); lastG = c.g; }
                    lastA = c.a;
                }
            }
            Assert.AreEqual(0f, CraftMetallurgy.HeatColor(520).a, "by day 520 °C is black heat");
            Assert.Greater(CraftMetallurgy.HeatColor(520, true).a, 0f, "in the dark it shows");
            Assert.Greater(CraftMetallurgy.HeatColor(600, true).a, CraftMetallurgy.HeatColor(600).a, "the dark shows it brighter");
            Assert.AreEqual("a faint blood red", CraftMetallurgy.HeatName(560, false));
            Assert.AreEqual("white, throwing sparks", CraftMetallurgy.HeatName(1350, false));
        }

        // ------------------------------------------------------------------ supplies
        [Test]
        public void Supplies_AFluxSaysWhereItWorks()
        {
            CraftRules r = Rules();
            Assert.IsTrue(r.Supply("limestone").FluxFor(CraftFluxUse.Furnace));
            Assert.IsFalse(r.Supply("limestone").FluxFor(CraftFluxUse.Weld));
            Assert.IsFalse(r.Supply("limestone").FluxFor(CraftFluxUse.Cover));
            Assert.IsTrue(r.Supply("borax").FluxFor(CraftFluxUse.Weld));
            Assert.IsTrue(r.Supply("borax").FluxFor(CraftFluxUse.Cover));
            Assert.IsFalse(r.Supply("borax").FluxFor(CraftFluxUse.Furnace));
            Assert.IsTrue(r.Supply("sand").FluxFor(CraftFluxUse.Weld));
            Assert.IsFalse(r.Supply("sand").FluxFor(CraftFluxUse.Cover));
            Assert.IsTrue(r.Supply("arcane_flux").FluxFor(CraftFluxUse.Furnace));
            Assert.IsFalse(r.Supply("beeswax").FluxFor(CraftFluxUse.Any), "only a flux is a flux");
            // A flux from an older database (it has no such field) works wherever a flux did before.
            CraftSupplyDefinition old = JsonUtility.FromJson<CraftSupplyDefinition>("{\"id\":\"potash\",\"kind\":0,\"strength\":1}");
            Assert.AreEqual(CraftFluxUse.Any, old.fluxUse);
            Assert.IsTrue(old.FluxFor(CraftFluxUse.Furnace) && old.FluxFor(CraftFluxUse.Cover) && old.FluxFor(CraftFluxUse.Weld));
        }

        // ------------------------------------------------------------------ optional parts
        [Test]
        public void Assembly_AnOptionalPartLeftOff_HoldsNothingUp()
        {
            CraftWorkshop w = Workshop(CraftTools.Woodshop);
            string qs = w.StartAssembly("quarterstaff", out string why);
            Assert.IsNotNull(qs, why);
            string staff = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "ash", 1850, 34, 34));
            Assert.IsTrue(w.Attach(qs, "staff", 0, staff, out why), why);
            CraftPiece a = w.Piece(qs);
            CraftDesign d = w.Rules.Design("quarterstaff");
            CraftJointSpec grip = d.Joint("grip");
            Assert.IsFalse(CraftAssembly.JointApplies(w, a, d, grip), "the wrap was left off: there is no joint to make");
            CollectionAssert.IsEmpty(CraftAssembly.Missing(w, a));
            Assert.IsFalse(w.JointWork(qs, "grip", CraftJointWork.Wrap, 8, 0.65, 0.05, out why), "and none can be made without the wrap");
            Assert.IsTrue(w.Finish(qs, out why), why);

            // The rule: a joint is made when its parts are there; a supply always is.
            Assert.IsTrue(CraftAssembly.JointApplies(d, grip, role => true));
            Assert.IsFalse(CraftAssembly.JointApplies(d, grip, role => role != "grip"));
            CraftDesign ring = w.Rules.Design("bezel_ring");
            var solder = new CraftJointSpec { id = "x", kind = CraftJointKind.Solder, roleA = "band", roleB = "solder" };
            Assert.IsTrue(CraftAssembly.JointApplies(ring, solder, role => role == "band"), "the solder is a supply");

            // An optional part on the first side of a joint is skipped the same way when the item is rated.
            CraftDesign club = w.Rules.Design("club");
            var turned = new CraftDesign { id = "turned_club", displayName = "Club", kind = CraftDesignKind.Weapon, weaponCategory = club.weaponCategory };
            turned.components.AddRange(club.components);
            turned.joints.Add(new CraftJointSpec { id = "studs", displayName = "Studs driven in", kind = CraftJointKind.Rivet, roleA = "studs", roleB = "club", count = 10, importance = 0.6f });
            string body = w.AddPiece(CraftDefaults.WoodBlank(w.Rules, "oak", 700, 50, 50));
            var parts = new Dictionary<string, List<CraftPiece>> { ["club"] = new List<CraftPiece> { w.Piece(body) } };
            CraftItemReport report = CraftEvaluator.Item(w.Rules, turned, parts, new AssemblyState { design = turned.id });
            Assert.IsFalse(report.Notes.Any(n => n.StartsWith("Studs driven in")), string.Join("; ", report.Notes));
        }

        [Test]
        public void PieceList_ShowsFittedPartsUnderTheirAssembly()
        {
            CraftWorkshop w = Workshop();
            // a blade with a tang drawn down at its end, and a pommel with a hole drifted for it
            CraftPiece bar = CraftDefaults.Bar(w.Rules, 430, 30, 5, Metal("Mild steel"));
            for (int i = 0; i < bar.segments.Count * 46 / 100; i++) bar.segments[i].width = 12;
            string blade = w.AddPiece(bar);
            CraftPiece knob = CraftDefaults.Bar(w.Rules, 40, 30, 20, Metal("Mild steel"));
            knob.segments[knob.segments.Count / 2].hole = 11;
            string pommel = w.AddPiece(knob);
            string loose = w.AddPiece(CraftDefaults.Bar(w.Rules, 100, 20, 5, Metal("Mild steel")));
            string dagger = w.StartAssembly("dagger", out string why);
            Assert.IsNotNull(dagger, why);
            Assert.IsTrue(w.Attach(dagger, "blade", 0, blade, out why), why);
            Assert.IsTrue(w.Attach(dagger, "pommel", 0, pommel, out why), why);
            var listed = new List<(CraftPiece piece, int depth)>();
            w.ListPieces(listed);
            CollectionAssert.AreEqual(new[] { loose, dagger, blade, pommel }, listed.Select(x => x.piece.id).ToArray(), "each fitted part right under its assembly");
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, listed.Select(x => x.depth).ToArray());
            Assert.IsTrue(w.Detach(dagger, "pommel", 0, out why), why);
            w.ListPieces(listed);
            CollectionAssert.AreEqual(new[] { pommel, loose, dagger, blade }, listed.Select(x => x.piece.id).ToArray(), "taken off, it is a piece of its own again");
        }

        // ------------------------------------------------------------------ answers
        [Test]
        public void Answers_WaitForTheCopyToShowTheirOperations_InTheOrderTheyCame()
        {
            var q = new CraftAnswerQueue();
            q.Arrived(new CraftAnswer(1, true, "", 5, 7));
            q.Arrived(new CraftAnswer(2, false, "Too fast: one cut at a time."));
            q.Arrived(new CraftAnswer(3, true, "", 8, 8));
            Assert.IsFalse(q.Next(4, out _), "the copy does not show the first request's operations yet");
            Assert.IsTrue(q.Next(7, out CraftAnswer a));
            Assert.AreEqual(1, a.seq);
            Assert.IsTrue(q.Next(7, out a), "a refusal that made nothing comes at once after it");
            Assert.AreEqual(2, a.seq);
            Assert.IsFalse(a.ok);
            StringAssert.Contains("Too fast", a.reason);
            Assert.IsFalse(q.Next(7, out _), "the third waits for its operation");
            Assert.IsTrue(q.Next(8, out a));
            Assert.AreEqual(3, a.seq);
            q.Arrived(new CraftAnswer(4, true, "", 20, 21));
            q.Arrived(new CraftAnswer(5, false, "No."));
            Assert.IsFalse(q.Next(19, out _), "a refusal waits its turn behind an answer still waiting");
            Assert.IsTrue(q.Next(30, out a), "a snapshot restored past it");
            Assert.AreEqual(4, a.seq);
            Assert.IsTrue(q.Next(30, out a));
            Assert.AreEqual(5, a.seq);
            Assert.AreEqual(0, q.Count);

            // the events the copy raised while applying each operation: an answer has only its own
            var ring = new CraftEvent(CraftEventKind.Struck, 1, "bar", "ring", "");
            var cooled = new CraftEvent(CraftEventKind.Note, 2, "bar", "cool", "");
            var scale = new CraftEvent(CraftEventKind.ScaleFlaked, 3, "bar", "", "");
            var other = new CraftEvent(CraftEventKind.Struck, 4, "bar", "thud", "");
            q.Raised(5, ring);
            q.Raised(0, cooled); // the clock's
            q.Raised(7, scale);
            q.Raised(9, other);
            var into = new List<CraftEvent>();
            q.EventsOf(new CraftAnswer(1, true, "", 5, 7), into);
            CollectionAssert.AreEqual(new[] { "ring", "" }, into.Select(e => e.Id).ToArray());
            q.EventsOf(new CraftAnswer(2, false, "No."), into);
            Assert.IsEmpty(into, "a request that made nothing raised nothing");
            Assert.IsTrue(CraftRequest.IsSetting(CraftRequestKind.Damper) && CraftRequest.IsSetting(CraftRequestKind.VatHeat) && CraftRequest.IsSetting(CraftRequestKind.Oven));
            Assert.IsFalse(CraftRequest.IsSetting(CraftRequestKind.Strike));
        }

        [Test]
        public void Answers_ComeOnce_OrAreGivenUpAfterTenSeconds_OrWhenTheWindowCloses()
        {
            var waits = new CraftAnswerWaits();
            var got = new List<string>();
            int a = waits.Expect((ok, why) => got.Add($"a {ok} {why}"), 0);
            int b = waits.Expect((ok, why) => got.Add($"b {ok} {why}"), 1);
            int c = waits.Expect(null, 2);
            Assert.IsTrue(a > 0 && b > 0 && c > 0 && a != b && b != c && a != c, "numbered, never 0");
            Assert.IsTrue(waits.Answer(a, true, ""));
            Assert.IsFalse(waits.Answer(a, false, "again"), "answered once");
            CollectionAssert.AreEqual(new[] { "a True " }, got);
            Assert.AreEqual(0, waits.Expire(10.5), "sent at 1 s: not given up on at 10.5 s");
            Assert.AreEqual(1, waits.Expire(11));
            Assert.AreEqual($"b False {CraftAnswerWaits.NoAnswer}", got[1]);
            Assert.IsTrue(waits.Waiting(c));
            int d = waits.Expect((ok, why) => got.Add("d"), 11);
            waits.Forget(d);
            Assert.AreEqual(1, waits.DropAll(), "the window closes: what is still waited for is answered as unanswered");
            Assert.AreEqual(2, got.Count, "a request that was not sent after all is never answered");
            Assert.IsFalse(waits.Waiting(0));
            Assert.AreEqual(0, waits.Count);
        }

        // ------------------------------------------------------------------ the guard
        private static CraftRequest Weave(int rows) => new CraftRequest(CraftRequestKind.WeaveCloth, "y")
            { i0 = 6, i1 = rows, a = 0.9f, b = 0.5f, points = Enumerable.Repeat(0.8f, rows).ToList() };

        private static CraftRequest Tier(int facets) => new CraftRequest(CraftRequestKind.FacetGem, "g")
            { i0 = 1, count = facets, a = 41f, b = 0.1f, c = 0.2f, d = 1f, i2 = 200 };

        [Test]
        public void Guard_AWeaveOrATierIsAllOrNothing_AndARefusalUsesUpNothing()
        {
            var guard = new CraftRequestGuard(5, 0);
            guard.Touch(0); // the window opened
            CraftRequest early = Weave(30);
            Assert.IsFalse(guard.Check(ref early, 5, out string why), "thirty rows beaten in five seconds: not real hands");
            StringAssert.Contains("row", why);
            CraftRequest weave = Weave(30);
            Assert.IsTrue(guard.Check(ref weave, 16, out why), "thirty rows, a beat every half second since the window opened: " + why);
            Assert.AreEqual(30, weave.points.Count, "every row woven (more than the sixteen the loom used to allow)");
            CraftRequest again = Weave(30);
            Assert.IsFalse(guard.Check(ref again, 16.1, out _), "another thirty at once: no");
            CraftRequest next = Weave(2);
            Assert.IsTrue(guard.Check(ref next, 16.6, out why), "the refused weave used up nothing: " + why);

            CraftRequest tier = Tier(16);
            Assert.IsTrue(guard.Check(ref tier, 20, out why), why);
            CraftRequest rushed = Tier(16);
            Assert.IsFalse(guard.Check(ref rushed, 20.5, out why), "the next tier's hits cannot be made in half a second");
            StringAssert.Contains("time", why);
            CraftRequest made = Tier(16);
            Assert.IsTrue(guard.Check(ref made, 22.1, out why), "its hits made in time - the refused try used up nothing: " + why);

            // a round brilliant, tier after tier, each after its hits on the bar at two a second
            var cutter = new CraftRequestGuard(6, 0);
            double t = 0;
            foreach (int f in new[] { 8, 16, 16, 8, 8, 16, 1 })
            {
                t += CraftMastery.FacetHits(f, 0) / 2.0;
                CraftRequest r = Tier(f);
                Assert.IsTrue(cutter.Check(ref r, t, out why), $"{f} facets at {t:0.0} s: {why}");
            }
        }

        [Test]
        public void Guard_ChargesATierTheHitsOfThePlayersRank_ByTheRulesSettings()
        {
            var master = new CraftRequestGuard(7, 1);
            CraftRequest m1 = Tier(16), m2 = Tier(16);
            Assert.IsTrue(master.Check(ref m1, 0, out _));
            Assert.IsTrue(master.Check(ref m2, 0.05, out string why), "a master's tier takes three hits: " + why);
            var byRules = new CraftRequestGuard(8, 0.6) { Settings = new CraftMasterySettings { master = 0.5f } };
            var byDefault = new CraftRequestGuard(9, 0.6);
            CraftRequest a1 = Tier(16), a2 = Tier(16), b1 = Tier(16), b2 = Tier(16);
            Assert.IsTrue(byRules.Check(ref a1, 0, out _));
            Assert.IsTrue(byRules.Check(ref a2, 0.01, out why), "a master by the rules' own ranks: three hits a tier - " + why);
            Assert.IsTrue(byDefault.Check(ref b1, 0, out _));
            Assert.IsFalse(byDefault.Check(ref b2, 0.01, out _), "an expert by the default ranks: four");
        }

        [Test]
        public void Guard_TheNeedlesPierceAndPullAreHalfAStitchEach()
        {
            var guard = new CraftRequestGuard(5, 0);
            CraftRequest Needle(CraftJointWork w, int pulls) => new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)w, count = pulls, i1 = 1, a = 0.3f, b = 0.1f };
            double t = 0;
            for (int k = 0; k < 15; k++)
            {
                // sewing at one and a half pulls a second for a minute, flushed every six pulls
                t += 6 / 1.5;
                CraftRequest punch = Needle(CraftJointWork.Punch, 6), stitch = Needle(CraftJointWork.Stitch, 6);
                Assert.IsTrue(guard.Check(ref punch, t, out string why), why);
                Assert.IsTrue(guard.Check(ref stitch, t, out why), why);
                Assert.AreEqual(6, punch.count);
                Assert.AreEqual(6, stitch.count, "every pull the window drew is laid");
            }
            var burst = new CraftRequestGuard(6, 0);
            CraftRequest p16 = Needle(CraftJointWork.Punch, 16), s16 = Needle(CraftJointWork.Stitch, 16);
            Assert.IsTrue(burst.Check(ref p16, 0, out _));
            Assert.IsTrue(burst.Check(ref s16, 0, out _));
            Assert.AreEqual(16, s16.count, "sixteen pulls at once: the pierce did not use up the pull");
            // the pierce is never free: holes cannot be had for nothing
            int holes = 0;
            for (int k = 0; k < 60; k++)
            {
                CraftRequest hole = Needle(CraftJointWork.Punch, 1);
                if (burst.Check(ref hole, 0.01, out string why)) holes += hole.count;
                else Assert.IsNotEmpty(why);
            }
            Assert.Less(holes, 60);
        }

        [Test]
        public void Guard_JointCountsAreCapped_AndAHammersJointWorkIsABlow()
        {
            var guard = new CraftRequestGuard(5, 0);
            var wrap = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Wrap, count = 5000, a = 0.5f };
            Assert.IsTrue(guard.Check(ref wrap, 0, out string why));
            Assert.LessOrEqual(wrap.count, CraftRequestGuard.MaxCount);
            StringAssert.Contains("counted", why, "granted in part, and it says so");
            var peen = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Peen, count = 16, a = 4.8f };
            Assert.IsTrue(guard.Check(ref peen, 10, out why), why);
            Assert.AreEqual(1, peen.count, "one blow at a time");
            Assert.LessOrEqual(peen.a, 4 * CraftRequestGuard.MaxForce(99) + 1e-4);
            var quick = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Rivet, count = 1, a = 4.8f };
            Assert.IsTrue(guard.Check(ref quick, 10.05, out why), why);
            Assert.Less(quick.a, 3.5f, "a hammer barely raised strikes light");
            var third = new CraftRequest(CraftRequestKind.JointWork, "a") { i0 = (int)CraftJointWork.Wedge, count = 1, a = 2f };
            Assert.IsFalse(guard.Check(ref third, 10.1, out why), "and not a third blow before the hammer is up");
            StringAssert.Contains("hammer", why);
        }

        [Test]
        public void Guard_SandingIsCountedInTheWindowsStrokes()
        {
            var guard = new CraftRequestGuard(5, 0);
            double claimed = 0, granted = 0;
            for (int k = 0; k < 10; k++)
            {
                var sand = new CraftRequest(CraftRequestKind.SandWood, "b") { i0 = 0, i1 = 3, a = 0.5f, b = 6 * 1.5f, c = 0.9f };
                Assert.IsTrue(guard.Check(ref sand, k * 2.4, out _));
                claimed += 9;
                granted += sand.b;
            }
            Assert.AreEqual(claimed, granted, 1e-3, "ten six-pass scrubs, one every 2.4 s: all of it counts (a client used to get a third)");
            var frantic = new CraftRequest(CraftRequestKind.SandWood, "b") { a = 0.5f, b = 60f, c = 1f };
            Assert.IsTrue(guard.Check(ref frantic, 30, out string why));
            Assert.Less(frantic.b, 60f);
            StringAssert.Contains("paper", why);
        }

        [Test]
        public void Guard_BrushingKeepsUpWithBriskScrubbing()
        {
            var guard = new CraftRequestGuard(5, 0);
            var brisk = new CraftRequest(CraftRequestKind.BrushOre, "ore") { i0 = 0, i1 = 1, a = 0.7f, b = 0.6f };
            Assert.IsTrue(guard.Check(ref brisk, 0, out _));
            Assert.AreEqual(0.7f, brisk.a, 1e-5, "a small stone gone over in a brisk 0.6 s scrub: all of it counts");
            var claimed = new CraftRequest(CraftRequestKind.BrushOre, "ore") { i0 = 0, i1 = 1, a = 1f, b = 0.2f };
            Assert.IsTrue(guard.Check(ref claimed, 1, out _));
            Assert.AreEqual(0.2f * (float)CraftRequestGuard.BrushCoverPerSecond, claimed.a, 1e-5, "a whole stone in a fifth of a second: no");
        }

        [Test]
        public void Guard_ChecksEachKindsOwnList()
        {
            var guard = new CraftRequestGuard(5, 0);
            var clay = new CraftRequest(CraftRequestKind.Clay, "bar") { a = 2f, points = Enumerable.Repeat(1.5f, 9).ToList() };
            Assert.IsTrue(guard.Check(ref clay, 0, out string why), "a casting of nine segments is clayed like any other: " + why);
            var scrape = new CraftRequest(CraftRequestKind.Scrape, "h") { a = 0.5f, points = new List<float> { 1, 1, 2, 2, 3 } };
            Assert.IsFalse(guard.Check(ref scrape, 0.1, out why), "a path is x,y pairs");
            Assert.AreEqual(CraftRequestGuard.Garbled, why);
            var look = new CraftRequest(CraftRequestKind.EvaluateGem, "g") { a = 0.5f, points = new List<float> { 0.1f, 0.2f, 0.5f, -0.3f, 0.4f, 0.6f, 0.9f } };
            Assert.IsTrue(guard.Check(ref look, 0.2, out why), "two looks and a stray number: " + why);
            Assert.AreEqual(6, look.points.Count, "whole looks only");
            var clean = new CraftRequest(CraftRequestKind.CleanGem, "g") { a = 0.5f, points = Enumerable.Repeat(0.1f, CraftLapidary.CrustSectors + 1).ToList() };
            Assert.IsFalse(guard.Check(ref clean, 0.3, out why));
            Assert.IsNotEmpty(why);
            var polish = new CraftRequest(CraftRequestKind.PolishGem, "g") { id = "rouge", count = 2, a = 0.5f, b = 0.5f, points = Enumerable.Repeat(0.3f, CraftLapidary.PolishRegions).ToList() };
            Assert.IsTrue(guard.Check(ref polish, 0.4, out why), "one value a region, no padding: " + why);
            var none = new CraftRequest(CraftRequestKind.WeaveCloth, "y") { i0 = 6, i1 = 0, a = 1f };
            Assert.IsFalse(guard.Check(ref none, 0.5, out why), "a weave with no beats weaves nothing");
            StringAssert.Contains("Beat", why);
            var fewer = new CraftRequest(CraftRequestKind.WeaveCloth, "y") { i0 = 6, i1 = 4, a = 1f, points = new List<float> { 1, 1, 1 } };
            Assert.IsFalse(guard.Check(ref fewer, 0.6, out _), "four rows claimed with three beats");
            var odd = new CraftRequest(CraftRequestKind.Strike, "bar") { a = float.NaN };
            Assert.IsFalse(guard.Check(ref odd, 0.7, out why));
            Assert.AreEqual(CraftRequestGuard.Garbled, why);
            var coat = new CraftRequest(CraftRequestKind.Clay, "bar") { points = Enumerable.Repeat(1f, CraftRequestGuard.MaxClayValues + 1).ToList() };
            Assert.IsFalse(guard.Check(ref coat, 0.8, out why));
            Assert.AreEqual(CraftRequestGuard.TooMuch, why);
            // a stroke longer than a request carries keeps its first part (the station cuts it before a client sends it)
            var tooLong = new CraftRequest(CraftRequestKind.Scrape, "h") { a = 0.5f, points = Enumerable.Range(0, 1500).Select(i => i % 40 * 0.1f).ToList() };
            Assert.IsTrue(CraftRequestGuard.Fit(ref tooLong, out string note));
            Assert.AreEqual(CraftRequestGuard.MaxPathFloats, tooLong.points.Count);
            Assert.IsNotEmpty(note);
            Assert.IsTrue(CraftRequestGuard.WellFormed(tooLong));
        }

        /// <summary>A request of each kind as the window sends it.</summary>
        private static CraftRequest Typical(CraftRequestKind k)
        {
            var r = new CraftRequest(k, "p") { count = 3, a = 0.5f, b = 0.5f, c = 0.5f, i1 = 1 };
            switch (k)
            {
                case CraftRequestKind.Scrape: case CraftRequestKind.Stake: case CraftRequestKind.Slick:
                case CraftRequestKind.Skive: case CraftRequestKind.Dye: case CraftRequestKind.Condition:
                    r.points = new List<float> { 1, 1, 2, 2, 3, 3, 4, 4 }; break;
                case CraftRequestKind.GrindGem: r.points = new List<float> { 10, 0.5f, 20, 0.5f }; break;
                case CraftRequestKind.EvaluateGem: r.points = new List<float> { 0.1f, 0.1f, 0.5f }; break;
                case CraftRequestKind.CleanGem: r.points = Enumerable.Repeat(0.5f, CraftLapidary.CrustSectors).ToList(); break;
                case CraftRequestKind.PolishGem: r.points = Enumerable.Repeat(0.3f, CraftLapidary.PolishRegions).ToList(); break;
                case CraftRequestKind.WeaveCloth: r.i1 = 4; r.points = Enumerable.Repeat(0.8f, 4).ToList(); break;
                case CraftRequestKind.Clay: r.points = Enumerable.Repeat(1f, 9).ToList(); break;
                case CraftRequestKind.Routine: r.i0 = (int)CraftRoutine.WindSkein; break;
            }
            return r;
        }

        [Test]
        public void Guard_EveryRefusalSaysWhy()
        {
            foreach (CraftRequestKind k in Enum.GetValues(typeof(CraftRequestKind)))
            {
                var guard = new CraftRequestGuard(5, 0);
                for (int n = 0; n < 40; n++)
                {
                    CraftRequest r = Typical(k);
                    if (!guard.Check(ref r, n * 0.02, out string why))
                        Assert.IsNotEmpty(why, $"{k}: refused without a word");
                }
            }
        }

        [Test]
        public void Guard_TooManyStrokesTooFast_SaysSo()
        {
            var guard = new CraftRequestGuard(5, 0);
            List<float> stroke = Enumerable.Range(0, 900).Select(i => i % 30 * 0.2f).ToList(); // 450 samples
            var first = new CraftRequest(CraftRequestKind.Stake, "h") { a = 0.5f, points = new List<float>(stroke) };
            Assert.IsTrue(guard.Check(ref first, 0, out string why), why);
            Assert.AreEqual(stroke.Count, first.points.Count);
            var second = new CraftRequest(CraftRequestKind.Stake, "h") { a = 0.5f, points = new List<float>(stroke) };
            Assert.IsTrue(guard.Check(ref second, 0.1, out why));
            Assert.Less(second.points.Count, stroke.Count);
            StringAssert.Contains("first part", why);
            var third = new CraftRequest(CraftRequestKind.Stake, "h") { a = 0.5f, points = new List<float>(stroke) };
            Assert.IsFalse(guard.Check(ref third, 0.1, out why));
            Assert.AreEqual("Too many strokes too fast: give the arm a moment.", why);
        }

        [Test]
        public void Guard_OnlyClamps_AndTremblesOnlyAClientThatStripsIt()
        {
            var guard = new CraftRequestGuard(5, 0);
            var facet = new CraftRequest(CraftRequestKind.FacetGem, "g") { i0 = 0, count = 8, a = 43f, b = 0.2f, c = 0.3f, d = 1f, i2 = 300 };
            Assert.IsTrue(guard.Check(ref facet, 0, out _));
            Assert.AreEqual(43f, facet.a, "the quill where the hands set it");
            var cleave = new CraftRequest(CraftRequestKind.CleaveGem, "g") { a = 37.25f, b = 0.1f, c = 0.6f };
            Assert.IsTrue(guard.Check(ref cleave, 1, out _));
            Assert.AreEqual(37.25f, cleave.a, "the chisel where the hands put it");
            var seat = new CraftRequest(CraftRequestKind.SeatSetting, "s") { other = "g", a = 6.4f, b = 0.5f, c = 0.5f };
            Assert.IsTrue(guard.Check(ref seat, 2, out _));
            Assert.AreEqual(6.4f, seat.a, "the burr as true as the hands held it");
            for (int k = 0; k < CraftRequestGuard.TiltWindow; k++)
            {
                var blow = new CraftRequest(CraftRequestKind.Strike, "bar") { a = 0.5f, b = 0.5f, c = 0f };
                Assert.IsTrue(guard.Check(ref blow, 10 + k * 0.5, out string why), why);
            }
            Assert.IsTrue(guard.AddsTremble, "blows that never tremble are noticed");
            var stripped = new CraftRequest(CraftRequestKind.FacetGem, "g") { i0 = 1, count = 8, a = 43f, b = 0.2f, c = 0.3f, d = 1f };
            Assert.IsTrue(guard.Check(ref stripped, 30, out _));
            Assert.AreNotEqual(43f, stripped.a, "from then on its quill trembles as real hands do");
            Assert.AreEqual(guard.Floor, CraftRequestGuard.FloorOf(guard.Skill), 1e-12, "the hands' floor, for the window to apply too");
        }

        [Test]
        public void Guard_SpinsABobbinsWorth_AndAHandPressesNoHarderThanFully()
        {
            var guard = new CraftRequestGuard(5, 0);
            var spin = new CraftRequest(CraftRequestKind.SpinFibre, "f") { a = 0.8f, b = 0.5f, c = 0.5f, i0 = 500 };
            Assert.IsTrue(guard.Check(ref spin, 0, out _));
            Assert.AreEqual(30, spin.i0, "one bobbin of work spins one bobbin's grams");
            var oil = new CraftRequest(CraftRequestKind.Condition, "h") { id = "neatsfoot_oil", a = 100f, points = new List<float> { 1, 1, 3, 1 } };
            Assert.IsTrue(guard.Check(ref oil, 1, out _));
            Assert.AreEqual(1f, oil.a, "no harder than a hand presses");
            // the workshop holds it there too (a test station's requests go unchecked)
            CraftWorkshop a = Workshop(CraftTools.Leatherworks), b = Workshop(CraftTools.Leatherworks);
            string ha = a.AddPiece(CraftLeather.NewHide(a.Rules, "cow", 2)), hb = b.AddPiece(CraftLeather.NewHide(b.Rules, "cow", 2));
            var path = new List<float> { 2, 2, 6, 2, 6, 4 };
            Assert.IsTrue(a.Dye(ha, "dye_brown", path, 100, out string why), why);
            Assert.IsTrue(b.Dye(hb, "dye_brown", path, 1, out why), why);
            Assert.IsTrue(a.Skive(ha, path, 50, out why), why);
            Assert.IsTrue(b.Skive(hb, path, 1, out why), why);
            Assert.IsTrue(a.Condition(ha, "neatsfoot_oil", path, 100, out why), why);
            Assert.IsTrue(b.Condition(hb, "neatsfoot_oil", path, 1, out why), why);
            List<HideCell> ca = a.Piece(ha).hide.cells, cb = b.Piece(hb).hide.cells;
            Assert.AreEqual(cb.Sum(x => x.dye), ca.Sum(x => x.dye), 1e-9);
            Assert.AreEqual(cb.Sum(x => x.thickness), ca.Sum(x => x.thickness), 1e-9);
            Assert.AreEqual(cb.Sum(x => x.oil), ca.Sum(x => x.oil), 1e-9);
        }

        [Test]
        public void Guard_StopsTheClockAndScrapsOnlyAtATestStation()
        {
            var guard = new CraftRequestGuard(5, 0);
            foreach (CraftRequestKind k in new[] { CraftRequestKind.Suspend, CraftRequestKind.Resume, CraftRequestKind.ScrapPiece })
            {
                var r = new CraftRequest(k, "p");
                Assert.IsFalse(guard.Check(ref r, 0, out string why), k.ToString());
                Assert.AreEqual(CraftRequestGuard.TestStationOnly(k), why);
            }
            Assert.IsNull(CraftRequestGuard.TestStationOnly(CraftRequestKind.Strike));
        }

        [Test]
        public void Practice_PastTheMinutesAllowance_ChangesNothingToSend()
        {
            var notes = new List<CraftMasteryNote>();
            var s = new CraftMasterySettings();
            CraftMastery.Practise(notes, CraftDiscipline.Smithing, 0.01, true, 0, out bool changed, false, s);
            Assert.IsTrue(changed, "work done: practice");
            for (int k = 0; k < 200; k++)
                CraftMastery.Practise(notes, CraftDiscipline.Smithing, 0.01, true, 1, out changed, false, s);
            Assert.IsFalse(changed, "past the minute's allowance small work adds nothing: the journal is not sent again for it");
            CraftMastery.Practise(notes, CraftDiscipline.Smithing, 1, false, 1, out changed, true, s);
            Assert.IsTrue(changed, "a finished item always counts");
        }

        [Test]
        public void Weave_NeedsItsBeats()
        {
            CraftWorkshop w = Workshop();
            string f = w.AddPiece(CraftFibre.NewFibre(w.Rules, "wool_fibre"));
            string y = w.SpinFibre(f, 0.85, 0.5, 0.6, 100, out string why);
            Assert.IsNotNull(y, why);
            Assert.IsNull(w.WeaveCloth(y, 4, 0.9, 0.5, null, out why), "no beats, no cloth");
            StringAssert.Contains("Beat the rows", why);
            Assert.IsNotNull(w.WeaveCloth(y, 4, 0.9, 0.5, new List<float> { 0.8f, 0.8f }, out why), why);
        }

        // ------------------------------------------------------------------ settings on their way, work given back
        [Test]
        public void Settings_DraggedEveryFrame_GoTenTimesASecond_AndNeverHoldTheWorkBack()
        {
            // The window where the rules run: a slider dragged for 3 s at 60 frames a second sends a value every frame;
            // the queue sends the latest ten times a second, and all at once before each refresh (four a second), while
            // the bellows are pumped twice a second - all through the player's guard.
            var guard = new CraftRequestGuard(1, 0);
            var queue = new CraftSettingQueue();
            var due = new List<CraftRequest>();
            int sent = 0, refused = 0, pumpsRefused = 0;
            float last = -1f;
            string why = "";
            void Flush(double t, bool all)
            {
                if (!queue.Take(t, due, all))
                    return;
                foreach (CraftRequest d in due)
                {
                    CraftRequest r = d;
                    sent++;
                    if (guard.Check(ref r, t, out string w)) last = r.a;
                    else { refused++; why = w; }
                }
            }
            for (int i = 0; i < 180; i++)
            {
                double t = i / 60.0;
                queue.Put(new CraftRequest(CraftRequestKind.Damper) { a = i / 180f });
                if (i % 30 == 0)
                {
                    var pump = new CraftRequest(CraftRequestKind.Bellows) { a = 1 };
                    if (!guard.Check(ref pump, t, out string w)) { pumpsRefused++; why = w; }
                }
                Flush(t, false);
                if (i % 15 == 14) Flush(t, true); // the window's refresh
            }
            Flush(3.0, true); // the hands leave the work
            Assert.AreEqual(0, refused, "a setting dragged is never refused: " + why);
            Assert.AreEqual(0, pumpsRefused, "nor does it hold the bellows back: " + why);
            Assert.LessOrEqual(sent, 3 * 12 + 1, "about ten a second, not sixty");
            Assert.AreEqual(179 / 180f, last, 1e-6, "the last value turned is the one that stays");
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void Settings_EachKeepsItsOwnLatestValue_AndTheyGoTogether()
        {
            var queue = new CraftSettingQueue();
            var due = new List<CraftRequest>();
            queue.Put(new CraftRequest(CraftRequestKind.Damper) { a = 0.2f });
            queue.Put(new CraftRequest(CraftRequestKind.VatHeat) { i0 = 1, a = 0.5f });
            queue.Put(new CraftRequest(CraftRequestKind.Damper) { a = 0.4f });
            queue.Put(new CraftRequest(CraftRequestKind.Damper) { a = 0.9f, flag = true }); // the furnace's vent, not the forge's
            queue.Put(new CraftRequest(CraftRequestKind.VatHeat) { i0 = 0, a = 0.1f });
            queue.Put(new CraftRequest(CraftRequestKind.Oven) { a = 300 });
            Assert.AreEqual(5, queue.Count);
            Assert.IsTrue(queue.Take(0, due));
            CollectionAssert.AreEqual(new[] { "Damper False 0.4", "VatHeat False 0.5", "Damper True 0.9", "VatHeat False 0.1", "Oven False 300" },
                due.Select(r => $"{r.kind} {r.flag} {r.a:0.##}").ToArray(), "in the order first turned, each at its latest");
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 0, 0 }, due.Select(r => r.i0).ToArray());
            Assert.IsFalse(queue.Take(0.01, due), "nothing waiting");
            queue.Put(new CraftRequest(CraftRequestKind.Damper) { a = 0.6f });
            Assert.IsFalse(queue.Take(0.05, due), "a tenth of a second after the last went: not yet");
            Assert.IsTrue(queue.Take(0.05, due, true), "the hands leave the work: at once");
            Assert.AreEqual(0.6f, due.Single().a);
            queue.Put(new CraftRequest(CraftRequestKind.Oven) { a = 0 });
            queue.Clear();
            Assert.IsFalse(queue.Take(10, due, true), "forgotten with the station");
        }

        [Test]
        public void Guard_WorkTheStationRefused_IsGivenBack_SoTheRetryIsNotHeldBack()
        {
            // A weave the guard let through that the workshop refused (raw fibre is not yarn): the station gives back
            // what the guard took, and the honest retry a second later is woven.
            CraftWorkshop w = Workshop();
            string fibre = w.AddPiece(CraftFibre.NewFibre(w.Rules, "wool_fibre"));
            string yarn = w.SpinFibre(w.AddPiece(CraftFibre.NewFibre(w.Rules, "wool_fibre")), 0.85, 0.5, 0.6, 100, out string why);
            Assert.IsNotNull(yarn, why);
            var guard = new CraftRequestGuard(5, 0);
            guard.Touch(0);
            CraftRequest weave = Weave(30);
            weave.piece = fibre;
            Assert.IsTrue(guard.Check(ref weave, 16, out why), why);
            Assert.IsNull(w.WeaveCloth(weave.piece, weave.i0, weave.a, weave.b, weave.points, out why));
            guard.Refund();
            CraftRequest retry = Weave(30);
            retry.piece = yarn;
            Assert.IsTrue(guard.Check(ref retry, 17, out why), "the refused weave used up nothing: " + why);
            Assert.IsNotNull(w.WeaveCloth(retry.piece, retry.i0, retry.a, retry.b, retry.points, out why), why);

            // a tier of facets the lapidary would not cut: the retry is not held back by its hits
            CraftRequest tier = Tier(16), again = Tier(16);
            Assert.IsTrue(guard.Check(ref tier, 30, out why), why);
            guard.Refund();
            Assert.IsTrue(guard.Check(ref again, 30.3, out why), why);
            // a refusal by the guard itself takes nothing, and a refund after it gives back nothing it did not take
            CraftRequest rushed = Tier(16), still = Tier(16);
            Assert.IsFalse(guard.Check(ref rushed, 30.4, out _));
            guard.Refund();
            Assert.IsFalse(guard.Check(ref still, 30.5, out _), "the granted tier before it stays charged");

            // a blow the workshop refused: the hammer's rise is given back too
            var given = new CraftRequestGuard(6, 0);
            var kept = new CraftRequestGuard(6, 0);
            CraftRequest Blow() => new CraftRequest(CraftRequestKind.Strike, "bar") { a = 0.5f, b = 1f, c = 0.02f };
            CraftRequest b1 = Blow(), b2 = Blow(), k1 = Blow(), k2 = Blow();
            Assert.IsTrue(given.Check(ref b1, 0, out _) && kept.Check(ref k1, 0, out _));
            given.Refund();
            Assert.IsTrue(given.Check(ref b2, 0.05, out _) && kept.Check(ref k2, 0.05, out _));
            Assert.AreEqual(1f, b2.b, 1e-6, "the refused blow never came down: this one is a full blow");
            Assert.Less(k2.b, 0.8f, "a blow right after one that landed is weak");

            // the pace of requests is never given back: thirty pumps at once, each refused by the station and refunded,
            // and the thirty-first is still too many
            var pace = new CraftRequestGuard(7, 0);
            for (int k = 0; k < 30; k++)
            {
                var pump = new CraftRequest(CraftRequestKind.Bellows) { a = 1 };
                Assert.IsTrue(pace.Check(ref pump, 0, out why), $"pump {k}: {why}");
                pace.Refund();
            }
            var more = new CraftRequest(CraftRequestKind.Bellows) { a = 1 };
            Assert.IsFalse(pace.Check(ref more, 0, out why));
            Assert.AreEqual(CraftRequestGuard.SlowDown, why);
        }

        [Test]
        public void Guard_AtATestStation_OnlyThePaceOfRequestsIsKept()
        {
            var guard = new CraftRequestGuard(5, 0);
            for (int k = 0; k < 30; k++)
                Assert.IsTrue(guard.Paced(0, out string why), why);
            Assert.IsFalse(guard.Paced(0, out string slow), "thirty at once, then twenty a second");
            Assert.AreEqual(CraftRequestGuard.SlowDown, slow);
            Assert.IsTrue(guard.Paced(0.05, out _), "a twentieth of a second later, one more");
            // the shape is checked everywhere, a test station too (the station runs WellFormed for every request)
            var garbled = new CraftRequest(CraftRequestKind.Strike, "bar") { a = float.NaN };
            Assert.IsFalse(CraftRequestGuard.WellFormed(garbled, out string bad));
            Assert.AreEqual(CraftRequestGuard.Garbled, bad);
            var nowhere = new CraftRequest(CraftRequestKind.Move, "bar") { location = (CraftLocation)200 };
            Assert.IsFalse(CraftRequestGuard.WellFormed(nowhere, out _), "a place that does not exist");
        }
    }
}
#endif
