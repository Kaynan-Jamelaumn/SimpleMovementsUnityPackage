#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using HubWorld;
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the hub world's network support: the duel book's copy of the server's duels, duel snapshots,
    /// the clock arithmetic a client's mirror relies on, the network delivery hook of the hub world messages, and the wire
    /// types of the hub world relays (with Fish-Net installed).
    /// </summary>
    public class HubWorldNetTests
    {
        private static DuelSnapshot View(int id, int a, int b, DuelState state, double deadline = 30d) =>
            new DuelSnapshot { id = id, challenger = a, challenged = b, state = state, deadline = deadline, winner = -1 };

        // ------------------------------------------------------------------ duel book copies

        [Test]
        public void DuelBookCopy_AddsUpdatesAndEndsDuels()
        {
            var book = new DuelBook();
            var changes = new List<DuelState>();
            book.Changed += d => changes.Add(d.State);

            book.ApplySnapshot(new[] { View(5, 1, 2, DuelState.Challenged, 30d) }, 0d);
            Assert.AreEqual(1, book.Open.Count);
            Assert.AreEqual(DuelState.Challenged, book.Of(1).State);
            Assert.AreEqual(30d, book.Of(2).Deadline);
            Assert.AreEqual(1, changes.Count, "a new duel is reported");

            book.ApplySnapshot(new[] { View(5, 1, 2, DuelState.Challenged, 30d) }, 1d);
            Assert.AreEqual(1, changes.Count, "an unchanged duel is not reported again");

            book.ApplySnapshot(new[] { View(5, 1, 2, DuelState.Fighting, 200d) }, 2d);
            Assert.IsTrue(book.AreFighting(1, 2));
            Assert.IsTrue(book.AreFighting(2, 1));
            Assert.AreEqual(200d, book.Of(1).Deadline);
            Assert.AreEqual(2, changes.Count);

            book.ApplySnapshot(Array.Empty<DuelSnapshot>(), 3d);
            Assert.AreEqual(0, book.Open.Count);
            Assert.IsFalse(book.AreFighting(1, 2));
            Assert.AreEqual(DuelState.Ended, changes[changes.Count - 1], "a duel missing from the server's list ends");
        }

        [Test]
        public void DuelBookCopy_IgnoresInvalidEntries()
        {
            var book = new DuelBook();
            book.ApplySnapshot(new[] { View(0, 1, 2, DuelState.Challenged), View(7, 3, 4, DuelState.Ended) }, 0d);
            Assert.AreEqual(0, book.Open.Count);
            book.ApplySnapshot(null, 0d);
            Assert.AreEqual(0, book.Open.Count);
        }

        [Test]
        public void DuelBookCopy_ReplacesAPlayersOlderDuel()
        {
            var book = new DuelBook();
            book.ApplySnapshot(new[] { View(1, 1, 2, DuelState.Challenged) }, 0d);
            book.ApplySnapshot(new[] { View(2, 1, 3, DuelState.Countdown) }, 1d);
            Assert.AreEqual(1, book.Open.Count);
            Assert.AreEqual(2, book.Of(1).Id);
            Assert.IsNull(book.Of(2), "player 2's challenge is gone");
        }

        [Test]
        public void DuelBookCopy_MatchesTheServersBook()
        {
            var server = new DuelBook { Countdown = 3d, MaxDuration = 60d };
            Duel d = server.Challenge(10, 20, 0d, out string error);
            Assert.IsNotNull(d, error);
            server.Accept(20, 1d);
            server.Update(5d); // past the countdown: fighting

            var sent = new List<DuelSnapshot>();
            foreach (Duel open in server.Open)
                sent.Add(DuelSnapshot.Of(open));
            var client = new DuelBook();
            client.ApplySnapshot(sent, 100d);

            Assert.AreEqual(DuelState.Fighting, client.Of(10).State);
            Assert.IsTrue(client.AreFighting(10, 20));
            Assert.AreEqual(server.Of(10).Deadline, client.Of(20).Deadline);
            Assert.AreEqual(server.Of(10).Id, client.Of(10).Id);
        }

        [Test]
        public void DuelSnapshot_CopiesTheDuel()
        {
            var book = new DuelBook { ChallengeTimeout = 15d };
            Duel d = book.Challenge(4, 9, 2d, out _);
            DuelSnapshot s = DuelSnapshot.Of(d);
            Assert.AreEqual(d.Id, s.id);
            Assert.AreEqual(4, s.challenger);
            Assert.AreEqual(9, s.challenged);
            Assert.AreEqual(DuelState.Challenged, s.state);
            Assert.AreEqual(17d, s.deadline);
            Assert.AreEqual(-1, s.winner);
            Assert.IsFalse(s.arena);
            Assert.AreEqual(default(DuelSnapshot), DuelSnapshot.Of(null));
            Assert.AreNotEqual(s, new DuelSnapshot { id = s.id, challenger = 4, challenged = 9, state = DuelState.Fighting, deadline = 17d, winner = -1 });
        }

        // ------------------------------------------------------------------ the mirrored clock

        [Test]
        public void MirroredClock_RestoresTheServersCountdown()
        {
            // What HubWorldDirector.ApplyServerCycle does with the server's "cycle 3, 42 s left".
            var clock = new WorldCycleClock(600d, new[] { 300f, 60f }, 0d, 0);
            double now = 100d, left = 42d;
            clock.Restore(3, now + left - clock.Duration, now);
            Assert.AreEqual(3, clock.CycleIndex);
            Assert.AreEqual(left, clock.Remaining(now), 1e-9);
            Assert.AreEqual(60f, clock.CurrentWarning(now), "the last warning is already due");
            Assert.AreEqual(left - 10d, clock.Remaining(now + 10d), 1e-9, "the countdown runs locally");
        }

        // ------------------------------------------------------------------ messages

        [Test]
        public void Messages_GoToTheNetworkAndToThisMachine()
        {
            var remote = new List<string>();
            var local = new List<string>();
            Action<CombatEntity, string, HubMessageKind> onSent = (to, text, kind) => local.Add(text);
            HubWorldMessages.Remote = (to, text, kind) => remote.Add(text);
            HubWorldMessages.Sent += onSent;
            try
            {
                HubWorldMessages.Broadcast("The city jumps!", HubMessageKind.Banner);
                HubWorldMessages.Send(null, "   ");
                HubWorldMessages.DeliverLocal(null, "From the server", HubMessageKind.Info);
            }
            finally
            {
                HubWorldMessages.Remote = null;
                HubWorldMessages.Sent -= onSent;
            }
            CollectionAssert.AreEqual(new[] { "The city jumps!" }, remote, "only sent messages are forwarded; blank ones never");
            CollectionAssert.AreEqual(new[] { "The city jumps!", "From the server" }, local, "delivered messages are raised here only");
        }

        [Test]
        public void Offline_TheDirectorIsNoMirror()
        {
            Assert.AreEqual(NetMode.Offline, NetAuthority.Mode, "Edit Mode tests run without a session");
            Assert.IsFalse(HubWorldDirector.IsMirror);
            Assert.AreSame(PlayerLocator.Local, HubWorldDirector.LocalPlayer, "single player: the usual local player");
            Assert.IsFalse(RealmGate.IsNear(null, RealmGate.GateKind.CityGate, 10f));
        }

        [Test]
        public void GroundInScene_NeedsALoadedScene()
        {
            Assert.IsFalse(RealmTravel.TryFindGroundIn(new Vector3(1f, 5f, 2f), default, out float height), "no scene, no ground");
            Assert.AreEqual(5f, height, "the height stays the asked one");
        }

        [Test]
        public void PlayerRealmState_StartsWithoutAnArrivalToWaitFor()
        {
            var go = new GameObject("Realm State");
            try
            {
                PlayerRealmState s = go.AddComponent<PlayerRealmState>();
                Assert.AreEqual(0, s.ArrivalToken);
                Assert.IsFalse(s.AwaitingArrival);
                Assert.AreEqual(RealmKind.None, s.Realm);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

#if FISHNET
        // ------------------------------------------------------------------ wire types

        [Test]
        public void RealmView_CarriesTheServersZone()
        {
            NetPlayerRealm.RealmView none = NetPlayerRealm.RealmView.Of(null);
            Assert.AreEqual(RealmKind.None, none.realm);
            Assert.AreEqual(-1, none.worldCycle);
            Assert.AreEqual(0, none.arrivalToken);

            var view = new NetPlayerRealm.RealmView
            {
                realm = RealmKind.World,
                worldCycle = 4,
                zoneIndex = 2,
                zoneName = "Dangerous Zone",
                pvp = true,
                dungeonDifficulty = 3.5f,
                mobStrength = 2f,
                rewardMultiplier = 1.5f,
                zoneColor = Color.red,
            };
            var zones = new ZoneMap(Vector2.zero, 100f, new[] { new ZoneBand("Safe", 500f, false, 1f, 1f, 1f, 1f, 1f, Color.green) });
            ZoneInfo z = view.ToZone(new Vector3(300f, 0f, 400f), zones);
            Assert.AreEqual(2, z.index);
            Assert.AreEqual("Dangerous Zone", z.name);
            Assert.IsTrue(z.pvp, "PvP is the server's verdict, not recomputed here");
            Assert.AreEqual(3.5f, z.dungeonDifficulty);
            Assert.AreEqual(500f, z.distance, 1e-3f, "the distance is measured locally");

            NetPlayerRealm.RealmView arriving = view;
            arriving.arrivalToken = 7;
            Assert.IsFalse(view.Equals(arriving), "an arrival to confirm is a change");
            Assert.IsTrue(view.Equals(view));
        }

        [Test]
        public void CycleState_ComparesEveryField()
        {
            var a = new NetHubWorld.CycleState { cycleIndex = 2, jumpAt = 120d, duration = 600f, worldSeed = 99, currentModifier = -1, nextModifier = 1, gateOpen = true };
            NetHubWorld.CycleState b = a;
            Assert.IsTrue(a.Equals(b));
            b.jumping = true;
            Assert.IsFalse(a.Equals(b));
            b = a;
            b.worldSeed = 100;
            Assert.IsFalse(a.Equals(b));
            Assert.GreaterOrEqual(NetHubWorld.NetworkTime(null), 0d, "no session: the local clock");
        }

        [Test]
        public void DuelView_ComparesEveryField()
        {
            var a = new NetDuels.DuelView { id = 1, challenger = 2, challenged = 3, state = DuelState.Countdown, deadline = 10d, winner = -1, arena = true, center = Vector3.one };
            NetDuels.DuelView b = a;
            Assert.IsTrue(a.Equals(b));
            b.center = Vector3.zero;
            Assert.IsFalse(a.Equals(b));
        }
#endif
    }
}
#endif
