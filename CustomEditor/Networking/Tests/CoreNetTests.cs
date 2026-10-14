#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the always-compiled networking core: command-line options, the pose history used by lag
    /// compensation, and the authority bridge offline.
    /// </summary>
    public class CoreNetTests
    {
        // ------------------------------------------------------------------ command line

        [Test]
        public void NoArgumentsMeansMenuAndLocalDefaults()
        {
            NetLaunchOptions o = NetLaunchOptions.Parse(new[] { "game.exe" }, 1);
            Assert.IsNull(o.mode);
            Assert.AreEqual(NetLaunchOptions.DefaultAddress, o.address);
            Assert.AreEqual(NetLaunchOptions.DefaultPort, o.port);
        }

        [Test]
        public void ParsesModesAddressPortAndName()
        {
            NetLaunchOptions o = NetLaunchOptions.Parse(new[] { "-client", "-address", "127.0.0.1", "-port=7780", "-name", "Ana", "-maxplayers", "8" });
            Assert.AreEqual(NetMode.Client, o.mode);
            Assert.AreEqual("127.0.0.1", o.address);
            Assert.AreEqual(7780, o.port);
            Assert.AreEqual("Ana", o.playerName);
            Assert.AreEqual(8, o.maxPlayers);

            Assert.AreEqual(NetMode.Server, NetLaunchOptions.Parse(new[] { "-batchmode", "-nographics", "-server" }).mode);
            Assert.AreEqual(NetMode.Host, NetLaunchOptions.Parse(new[] { "-HOST" }).mode);
            Assert.AreEqual(NetMode.Offline, NetLaunchOptions.Parse(new[] { "-offline" }).mode);
        }

        [Test]
        public void BadValuesKeepDefaultsAndUnityArgumentsAreSkipped()
        {
            NetLaunchOptions o = NetLaunchOptions.Parse(new[] { "-port", "abc", "-logFile", "out.log", "-tickrate", "0", "-shard", "42" });
            Assert.AreEqual(NetLaunchOptions.DefaultPort, o.port);
            Assert.AreEqual(0, o.tickRate);
            Assert.AreEqual(42, o.shardSeed);
            Assert.IsEmpty(o.ignored);
        }

        [Test]
        public void BuiltArgumentsParseBack()
        {
            string args = NetLaunchOptions.BuildArguments(NetMode.Client, "127.0.0.1", 7771, "Player Two", headless: false);
            NetLaunchOptions o = NetLaunchOptions.Parse(SplitArgs(args));
            Assert.AreEqual(NetMode.Client, o.mode);
            Assert.AreEqual(7771, o.port);
            Assert.AreEqual("127.0.0.1", o.address);
            Assert.AreEqual("Player Two", o.playerName);

            NetLaunchOptions s = NetLaunchOptions.Parse(SplitArgs(NetLaunchOptions.BuildArguments(NetMode.Server, null, 7770, null, headless: true)));
            Assert.AreEqual(NetMode.Server, s.mode);
        }

        /// <summary>Splits like a shell would for the simple quoting BuildArguments produces.</summary>
        private static string[] SplitArgs(string line)
        {
            var parts = new System.Collections.Generic.List<string>();
            var cur = new System.Text.StringBuilder();
            bool quoted = false;
            foreach (char c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    continue;
                }
                if (c == ' ' && !quoted)
                {
                    if (cur.Length > 0)
                        parts.Add(cur.ToString());
                    cur.Clear();
                    continue;
                }
                cur.Append(c);
            }
            if (cur.Length > 0)
                parts.Add(cur.ToString());
            return parts.ToArray();
        }

        // ------------------------------------------------------------------ pose history (lag compensation)

        [Test]
        public void PoseHistoryInterpolatesBetweenTicks()
        {
            var h = new PoseHistory(8);
            h.Record(10, new Vector3(0f, 0f, 0f), Quaternion.identity);
            h.Record(11, new Vector3(2f, 0f, 0f), Quaternion.identity);
            h.Record(12, new Vector3(4f, 0f, 0f), Quaternion.identity);
            Assert.IsTrue(h.Sample(10.5, out Vector3 p, out _));
            Assert.AreEqual(1f, p.x, 1e-4f);
            Assert.IsTrue(h.Sample(11.75, out p, out _));
            Assert.AreEqual(3.5f, p.x, 1e-4f);
        }

        [Test]
        public void PoseHistoryClampsOutsideItsRange()
        {
            var h = new PoseHistory(4);
            for (uint t = 1; t <= 10; t++)
                h.Record(t, new Vector3(t, 0f, 0f), Quaternion.identity);
            Assert.AreEqual(4, h.Count);
            Assert.AreEqual(7u, h.OldestTick);
            Assert.AreEqual(10u, h.NewestTick);
            h.Sample(2.0, out Vector3 old, out _);
            Assert.AreEqual(7f, old.x, 1e-4f, "older than the history: the oldest pose");
            h.Sample(99.0, out Vector3 future, out _);
            Assert.AreEqual(10f, future.x, 1e-4f, "newer than the history: the newest pose");
        }

        [Test]
        public void PoseHistoryRestartsWhenTicksGoBackwards()
        {
            var h = new PoseHistory(4);
            h.Record(100, Vector3.one, Quaternion.identity);
            h.Record(100, Vector3.zero, Quaternion.identity);
            Assert.AreEqual(1, h.Count, "the same tick replaces");
            h.Record(5, Vector3.up, Quaternion.identity);
            Assert.AreEqual(1, h.Count);
            Assert.AreEqual(5u, h.NewestTick);
        }

        // ------------------------------------------------------------------ authority bridge

        [Test]
        public void OfflineMeansThisMachineDecidesEverything()
        {
            NetAuthority.SetMode(NetMode.Offline);
            Assert.IsTrue(NetAuthority.IsServer);
            Assert.IsFalse(NetAuthority.IsNetworked);
            Assert.IsTrue(NetAuthority.HasLocalPlayer);
            Assert.IsFalse(NetAuthority.IsClientOnly);
            Assert.IsTrue(NetSpawn.ShouldSpawnHere);
        }

        [Test]
        public void LagCompensationScopeIsHarmlessOffline()
        {
            NetAuthority.SetMode(NetMode.Offline);
            using (LagCompensation.For((CombatEntity)null))
            using (LagCompensation.For((CombatEntity)null))
                Assert.IsFalse(LagCompensation.IsRewound);
            Assert.IsFalse(LagCompensation.IsRewound);
        }
    }
}
#endif
