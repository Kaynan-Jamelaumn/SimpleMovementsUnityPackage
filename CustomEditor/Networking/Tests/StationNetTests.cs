#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the always-compiled parts of networked crafting and alchemy stations: payloads (compressed,
    /// cut into parts, put back together, bounded), the copies clients keep of a workshop or a preparation following the
    /// server's exactly (late joiners, lost packets, a copy out of step), and the server's bounds on what a remote
    /// player's requests may claim.
    /// </summary>
    public class StationNetTests
    {
        private static CraftRules craftRules;
        private static CraftRules Craft() => craftRules ??= CraftDefaults.Rules();

        private static (string element, double fraction)[] Metal(string name) => CraftDefaults.StockMetals().First(m => m.name == name).composition;

        // ------------------------------------------------------------------ payloads
        [Test]
        public void Payload_RoundTripsSmallAndLargeText()
        {
            foreach (string text in new[] { "", "hello", string.Concat(Enumerable.Repeat("{\"segment\":[1.25,3.5,\"steel\"]},", 20000)) })
            {
                byte[] packed = NetPayload.Pack(text);
                List<byte[]> parts = NetPayload.Split(packed, 1000);
                var assembler = new NetPayloadAssembler();
                byte[] whole = null;
                for (int i = 0; i < parts.Count; i++)
                {
                    bool done = assembler.Add(7, i, parts.Count, parts[i], out whole);
                    Assert.AreEqual(i == parts.Count - 1, done);
                }
                Assert.IsTrue(NetPayload.TryUnpack(whole, out string back));
                Assert.AreEqual(text, back);
                if (text.Length > 1000)
                    Assert.Less(packed.Length, text.Length / 5, "repetitive JSON compresses");
            }
        }

        [Test]
        public void Payload_RefusesGarbageAndOversizedText()
        {
            Assert.IsFalse(NetPayload.TryUnpack(null, out _));
            Assert.IsFalse(NetPayload.TryUnpack(new byte[] { 9, 1, 2 }, out _));
            Assert.IsFalse(NetPayload.TryUnpack(new byte[] { 1, 0xff, 0xfe, 0x00, 0x13 }, out _));
            byte[] big = NetPayload.Pack(new string('a', 100000));
            Assert.IsFalse(NetPayload.TryUnpack(big, out _, 50000), "a payload that unpacks past the limit is refused");
            Assert.IsTrue(NetPayload.TryUnpack(big, out string ok, 200000));
            Assert.AreEqual(100000, ok.Length);
        }

        [Test]
        public void Assembler_ANewTransferReplacesAnUnfinishedOne()
        {
            var a = new NetPayloadAssembler();
            Assert.IsFalse(a.Add(1, 0, 3, new byte[] { 1 }, out _));
            Assert.IsTrue(a.Busy);
            Assert.IsFalse(a.Add(2, 1, 2, new byte[] { 2 }, out _), "a transfer must start with its first part");
            Assert.IsFalse(a.Add(2, 0, 2, new byte[] { 5 }, out _));
            Assert.IsTrue(a.Add(2, 1, 2, new byte[] { 6 }, out byte[] whole));
            CollectionAssert.AreEqual(new byte[] { 5, 6 }, whole);
            Assert.IsFalse(a.Add(3, 0, 1000, new byte[] { 1 }, out _), "too many parts");
        }

        // ------------------------------------------------------------------ crafting: following the server
        /// <summary>A forge session on the server: a bar heated, forged, quenched and tempered, a hide in the vat.</summary>
        private static IEnumerable<Action<CraftWorkshop>> Session()
        {
            string bar = null, hide = null;
            yield return w =>
            {
                bar = w.AddPiece(CraftDefaults.Bar(w.Rules, 200, 30, 6, Metal("Tool steel")));
                hide = w.AddPiece(CraftLeather.NewHide(w.Rules, "deer", 3));
                Assert.IsTrue(w.AddFuel("charcoal", 8, out string why), why);
                Assert.IsTrue(w.FillVat(0, "bark", 2, out why), why);
            };
            for (int i = 0; i < 30; i++)
                yield return w => w.Bellows(1);
            yield return w => Assert.IsTrue(w.MovePiece(bar, CraftLocation.Fire, out string why, 0.7), why);
            yield return w => Assert.IsTrue(w.MovePiece(hide, CraftLocation.Vat, out string why, 1, 0, 9999, 0), why);
            for (int i = 0; i < 40; i++)
                yield return w => w.Bellows(0.6);
            yield return w => Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out string why), why);
            for (int i = 0; i < 12; i++)
            {
                double at = 0.2 + i * 0.05;
                yield return w => w.Strike(bar, at, CraftStrikeTool.Flat, CraftStrikeFace.Flat, 0.8, 0.1, out _);
            }
            yield return w => Assert.IsTrue(w.MovePiece(bar, CraftLocation.Quench, out string why), why);
            yield return w => w.Agitate(bar, CraftQuenchMotion.UpDown, out _);
            yield return w => Assert.IsTrue(w.MovePiece(bar, CraftLocation.Air, out string why), why);
            yield return w => Assert.IsTrue(w.SetOven(220, out string why), why);
            yield return w => Assert.IsTrue(w.MovePiece(bar, CraftLocation.Oven, out string why), why);
            yield return w => w.StirVat(0, out _);
        }

        /// <summary>Runs <see cref="Session"/> on a server workshop in frames of uneven length, sending a packet each frame.</summary>
        private static List<string> RunServer(CraftWorkshop server, CraftWorkshopFeed feed, int framesBetween = 6, Func<int, bool> checksumAt = null)
        {
            var packets = new List<string>();
            var random = new Random(5);
            int frame = 0;
            foreach (Action<CraftWorkshop> op in Session())
            {
                op(server);
                for (int f = 0; f < framesBetween; f++)
                {
                    server.Advance(0.01 + random.NextDouble() * 0.06);
                    frame++;
                    packets.Add(feed.TakePacket(checksumAt == null ? frame % 5 == 0 : checksumAt(frame)).ToJson());
                }
            }
            return packets;
        }

        private static string StateOf(CraftWorkshop w)
        {
            CraftWorkshopState s = w.Capture(false);
            s.carry = 0; // the server's frame remainder; copies follow its steps
            return s.ToJson();
        }

        [Test]
        public void CraftMirror_FollowsTheServerExactly()
        {
            var server = new CraftWorkshop(Craft(), CraftTools.Everything, 18, 3, 3, "water", "srv");
            var feed = new CraftWorkshopFeed();
            feed.Attach(server);
            var client = new CraftWorkshopFollower(Craft());
            client.Restore(CraftMirrorSnapshot.FromJson(feed.Snapshot(new CraftStationMeta()).ToJson()));
            Assert.IsNotNull(client.Workshop);
            int events = 0;
            client.Workshop.EventRaised += e => events++;

            foreach (string json in RunServer(server, feed))
                Assert.AreEqual(FollowResult.Followed, client.Apply(CraftMirrorPacket.FromJson(json)));

            Assert.AreEqual(server.Step, client.Workshop.Step);
            Assert.AreEqual(server.Checksum(), client.Workshop.Checksum());
            Assert.AreEqual(StateOf(server), StateOf(client.Workshop), "the copy is the server's workshop, number for number");
            Assert.Greater(events, 10, "the copy raises its own events (sparks, the ring of the hammer, steam) for the visuals");
        }

        [Test]
        public void CraftMirror_ALateJoinerStartsFromASnapshot()
        {
            var server = new CraftWorkshop(Craft(), CraftTools.Everything, 18, 3, 3, "water", "srv");
            var feed = new CraftWorkshopFeed();
            feed.Attach(server);
            List<string> packets = RunServer(server, feed);
            int half = packets.Count / 2;
            // The joiner asks while half the session is over: what was sent before reaches it, then the snapshot.
            var client = new CraftWorkshopFollower(Craft());
            foreach (string json in packets.Take(half))
                Assert.AreEqual(FollowResult.Waiting, client.Apply(CraftMirrorPacket.FromJson(json)));

            // Replay the same session to the halfway point to take the snapshot there.
            var server2 = new CraftWorkshop(Craft(), CraftTools.Everything, 18, 3, 3, "water", "srv");
            var feed2 = new CraftWorkshopFeed();
            feed2.Attach(server2);
            var random = new Random(5);
            var all = new List<string>();
            int frame = 0;
            foreach (Action<CraftWorkshop> op in Session())
            {
                op(server2);
                for (int f = 0; f < 6; f++)
                {
                    server2.Advance(0.01 + random.NextDouble() * 0.06);
                    frame++;
                    if (frame == half)
                        client.Restore(CraftMirrorSnapshot.FromJson(feed2.Snapshot(new CraftStationMeta()).ToJson()));
                    string json = feed2.TakePacket(frame % 5 == 0).ToJson();
                    if (frame >= half)
                        Assert.AreNotEqual(FollowResult.NeedSnapshot, client.Apply(CraftMirrorPacket.FromJson(json)), $"frame {frame}");
                }
            }
            Assert.AreEqual(StateOf(server2), StateOf(client.Workshop));
        }

        [Test]
        public void CraftMirror_ALostPacketOrADriftAsksForASnapshot()
        {
            var server = new CraftWorkshop(Craft(), CraftTools.Everything, 18, 3, 3, "water", "srv");
            var feed = new CraftWorkshopFeed();
            feed.Attach(server);
            var client = new CraftWorkshopFollower(Craft());
            client.Restore(feed.Snapshot(null));

            string bar = server.AddPiece(CraftDefaults.Bar(server.Rules, 200, 30, 6, Metal("Mild steel")));
            feed.TakePacket(false); // lost on the way
            server.AddFuel("charcoal", 4, out _);
            Assert.AreEqual(FollowResult.NeedSnapshot, client.Apply(feed.TakePacket(false)), "a gap in the numbering");
            Assert.IsTrue(client.State.ShouldAsk(0f));
            Assert.IsFalse(client.State.ShouldAsk(0.5f), "not asked again at once");

            client.Restore(CraftMirrorSnapshot.FromJson(feed.Snapshot(null).ToJson()));
            server.MovePiece(bar, CraftLocation.Fire, out _, 0.6);
            server.RunSteps(30);
            Assert.AreEqual(FollowResult.Followed, client.Apply(feed.TakePacket(true)));

            client.Workshop.Piece(bar).segments[3].body.temp += 200; // out of step (a cosmic ray, a bug)
            server.RunSteps(5);
            Assert.AreEqual(FollowResult.NeedSnapshot, client.Apply(feed.TakePacket(true)), "the checksum catches it");
        }

        [Test]
        public void CraftMirror_AReplacedWorkshopIsSentWhole()
        {
            var server = new CraftWorkshop(Craft(), CraftTools.Smithy, 18, 1, 0, "water", "a");
            var feed = new CraftWorkshopFeed();
            feed.Attach(server);
            var client = new CraftWorkshopFollower(Craft());
            client.Restore(feed.Snapshot(null));
            server.AddPiece(CraftDefaults.Bar(server.Rules, 200, 30, 6, Metal("Mild steel")));
            Assert.AreEqual(FollowResult.Followed, client.Apply(feed.TakePacket(true)));

            CraftWorkshop replaced = CraftWorkshop.Restore(Craft(), new CraftWorkshop(Craft(), CraftTools.Smithy, 25, 2, 0, "water", "b").Capture(false));
            feed.Attach(replaced);
            replaced.AddFuel("charcoal", 3, out _);
            client.Restore(CraftMirrorSnapshot.FromJson(feed.Snapshot(null).ToJson()));
            Assert.AreEqual(0, client.Workshop.Pieces.Count);
            Assert.AreEqual(FollowResult.Followed, client.Apply(feed.TakePacket(true)), "operations already in the snapshot are skipped");
            Assert.AreEqual(replaced.Checksum(), client.Workshop.Checksum());
        }

        // ------------------------------------------------------------------ alchemy: following the server
        [Test]
        public void AlchemyMirror_FollowsTheServerExactly()
        {
            AlchemyRules rules = AlchemyDefaults.Rules();
            var server = new AlchemyBrew(rules, AlchemyEquipmentProfile.DefaultCauldron(), 20, 4);
            var feed = new AlchemyBrewFeed();
            feed.Attach(server);
            var client = new AlchemyBrewFollower(rules);
            client.Restore(AlchemyMirrorSnapshot.FromJson(feed.Snapshot(new AlchemyStationMeta()).ToJson()));

            var random = new Random(9);
            var steps = new List<Action>
            {
                () => Assert.IsTrue(server.AddIngredient(AlchemyDefaults.Spec("Spring Water"), 2, out string why), why),
                () => Assert.IsTrue(server.AddIngredient(AlchemyDefaults.Spec("Sunpetal"), 3, out string why), why),
                () => server.SetHeat(0.6),
                () => server.Stir(1),
                () => server.SetStirring(0.4),
                () => server.SetHeat(0.3),
                () => server.Bottle(1),
                () => server.SetStirring(0),
            };
            int frame = 0;
            foreach (Action step in steps)
            {
                step();
                for (int f = 0; f < 120; f++)
                {
                    server.Advance(0.01 + random.NextDouble() * 0.06);
                    frame++;
                    string json = feed.TakePacket(frame % 7 == 0).ToJson();
                    Assert.AreEqual(FollowResult.Followed, client.Apply(AlchemyMirrorPacket.FromJson(json)), $"frame {frame}");
                }
            }
            Assert.AreEqual(server.Step, client.Brew.Step);
            Assert.AreEqual(server.Checksum(), client.Brew.Checksum());
            Assert.AreEqual(server.Evaluate().OutcomeId, client.Brew.Evaluate().OutcomeId);
            Assert.AreEqual(server.Mixture.Temperature, client.Brew.Mixture.Temperature, 1e-9);
        }

        // ------------------------------------------------------------------ the server's bounds on requests
        private static CraftRequestGuard Guard(double skill = 0) => new CraftRequestGuard(1, skill);

        [Test]
        public void Guard_WorkTakesRealTime()
        {
            CraftRequestGuard g = Guard();
            var grind = new CraftRequest(CraftRequestKind.Grind, "p") { c = 60f };
            Assert.IsTrue(g.Check(ref grind, 0, out _));
            Assert.LessOrEqual(grind.c, 3.01f, "a minute of grinding claimed at once grinds for what the allowance holds");
            var again = new CraftRequest(CraftRequestKind.Grind, "p") { c = 1f };
            Assert.IsFalse(g.Check(ref again, 0.001, out _), "nothing left");
            again.c = 1f;
            Assert.IsTrue(g.Check(ref again, 1.0, out _));
            Assert.AreEqual(1f, again.c, 0.01f, "a second later, a second's worth");

            var stone = new CraftRequest(CraftRequestKind.Sharpen, "p") { count = 500, c = 0f };
            Assert.IsTrue(g.Check(ref stone, 2.0, out _));
            Assert.LessOrEqual(stone.count, 8);
            Assert.Greater(stone.c, 0f, "no stroke is perfectly steady");
        }

        [Test]
        public void Guard_ASpentArmCannotKeepStrikingFullBlows()
        {
            CraftRequestGuard g = Guard();
            double now = 0, last = 1;
            for (int i = 0; i < 80; i++)
            {
                var r = new CraftRequest(CraftRequestKind.Strike, "p") { a = 0.5f, b = 1.2f, c = 0.2f * (i % 3 - 1) };
                Assert.IsTrue(g.Check(ref r, now, out _));
                last = r.b;
                now += 0.45;
            }
            Assert.Less(last, 0.9, "the server's arm tires with the blows it lets through");
            var rested = new CraftRequest(CraftRequestKind.Strike, "p") { a = 0.5f, b = 1.2f };
            Assert.IsTrue(g.Check(ref rested, now + 60, out _));
            Assert.Greater(rested.b, last + 0.1, "and recovers when it rests");
        }

        [Test]
        public void Guard_BrushingCoversOnlyWhatTheSecondsAllow()
        {
            CraftRequestGuard g = Guard();
            var r = new CraftRequest(CraftRequestKind.BrushOre, "p") { a = 1f, b = 0.5f };
            Assert.IsTrue(g.Check(ref r, 10, out _));
            Assert.LessOrEqual(r.a, 0.2f + 1e-6f, "half a second of brushing does not scrub a whole stone");
        }

        [Test]
        public void Guard_TheHammerMustBeRaisedToStrikeHard()
        {
            CraftRequestGuard g = Guard();
            var first = new CraftRequest(CraftRequestKind.Strike, "p") { a = 0.5f, b = 1f, c = 0.3f };
            Assert.IsTrue(g.Check(ref first, 10, out _));
            Assert.AreEqual(1f, first.b, 1e-6f);
            var snatched = new CraftRequest(CraftRequestKind.Strike, "p") { a = 0.5f, b = 1f, c = 0.3f };
            Assert.IsTrue(g.Check(ref snatched, 10.1, out _));
            Assert.Less(snatched.b, 0.9f, "a tenth of a second cannot charge a full blow");
            var third = new CraftRequest(CraftRequestKind.Strike, "p") { a = 0.5f, b = 1f };
            Assert.IsFalse(g.Check(ref third, 10.15, out string why), "a third blow in the same instant");
            Assert.IsNotEmpty(why);
        }

        [Test]
        public void Guard_NoticesBlowsThatNeverTremble()
        {
            CraftRequestGuard honest = Guard(), cheat = Guard();
            var hands = new CraftHands(7);
            var hammer = new CraftHammer();
            double now = 0;
            for (int i = 0; i < 20; i++)
            {
                hammer.Raise();
                for (int t = 0; t < 50; t++) hammer.Tick(0.02);
                CraftBlow b = hammer.Release(0.5, 0, hands);
                hands.Rest(1);
                var r = new CraftRequest(CraftRequestKind.Strike, "p") { a = (float)b.position, b = (float)b.force, c = (float)b.tilt };
                Assert.IsTrue(honest.Check(ref r, now, out _));
                var perfect = new CraftRequest(CraftRequestKind.Strike, "p") { a = 0.5f, b = 1f, c = 0f };
                Assert.IsTrue(cheat.Check(ref perfect, now, out _));
                now += 1;
            }
            Assert.IsFalse(honest.AddsTremble, "real hands tremble");
            Assert.IsTrue(cheat.AddsTremble, "a dozen perfectly flat blows are not real hands");
            var next = new CraftRequest(CraftRequestKind.Strike, "p") { a = 0.5f, b = 1f, c = 0f };
            Assert.IsTrue(cheat.Check(ref next, now, out _));
            Assert.AreNotEqual(0f, next.c, "the server adds the tremble itself");
        }

        [Test]
        public void Guard_HandsAreNeverSteadierThanPracticeAllows()
        {
            CraftRequestGuard novice = Guard(0), master = Guard(1);
            var pour = new CraftRequest(CraftRequestKind.Pour) { a = 1f, b = 1f };
            Assert.IsTrue(novice.Check(ref pour, 0, out _));
            Assert.Less(pour.b, 1f);
            var masterPour = new CraftRequest(CraftRequestKind.Pour) { a = 1f, b = 1f };
            Assert.IsTrue(master.Check(ref masterPour, 0, out _));
            Assert.Greater(masterPour.b, pour.b, "a master pours steadier than a novice can");
            var poured = new CraftRequest(CraftRequestKind.Pour) { a = 1f, b = 0.5f };
            Assert.IsFalse(novice.Check(ref poured, 0.1, out _), "the metal is still running");
        }

        [Test]
        public void Guard_RefusesMalformedRequests()
        {
            CraftRequestGuard g = Guard();
            var nan = new CraftRequest(CraftRequestKind.Strike, "p") { b = float.NaN };
            Assert.IsFalse(g.Check(ref nan, 0, out _));
            var longId = new CraftRequest(CraftRequestKind.SetIntent, new string('x', 200));
            Assert.IsFalse(g.Check(ref longId, 0, out _));
            var odd = new CraftRequest(CraftRequestKind.Scrape, "h") { points = new List<float> { 1, 2, 3 } };
            Assert.IsFalse(g.Check(ref odd, 0, out _));
            var kind = new CraftRequest((CraftRequestKind)250, "p");
            Assert.IsFalse(g.Check(ref kind, 0, out _));
            var path = new CraftRequest(CraftRequestKind.Scrape, "h") { points = Enumerable.Range(0, 1000).Select(i => (float)(i % 30)).ToList() };
            Assert.IsTrue(g.Check(ref path, 0, out _));
            Assert.LessOrEqual(path.points.Count, 1000);
        }

        [Test]
        public void NetRequest_CarriesEveryField()
        {
            var r = new CraftRequest(CraftRequestKind.Sharpen, "ws-3")
            {
                other = "ws-4", id = "fine_stone", count = 3, i0 = 2, i1 = 9, i2 = 1, a = 17.5f, b = 0.4f, c = 1.2f, d = -2f, flag = true,
                location = CraftLocation.Quench, points = new List<float> { 1, 2, 3, 4 },
            };
            CraftRequest back = CraftNetRequest.From(r, "item-1").ToRequest();
            Assert.AreEqual(r.kind, back.kind);
            Assert.AreEqual(r.piece, back.piece);
            Assert.AreEqual(r.other, back.other);
            Assert.AreEqual(r.id, back.id);
            Assert.AreEqual("item-1", back.itemId);
            Assert.AreEqual(new[] { r.count, r.i0, r.i1, r.i2 }, new[] { back.count, back.i0, back.i1, back.i2 });
            Assert.AreEqual(new[] { r.a, r.b, r.c, r.d }, new[] { back.a, back.b, back.c, back.d });
            Assert.AreEqual(r.flag, back.flag);
            Assert.AreEqual(r.location, back.location);
            CollectionAssert.AreEqual(r.points, back.points);
            Assert.IsNull(back.item, "items travel by id only");
        }
    }
}
#endif
