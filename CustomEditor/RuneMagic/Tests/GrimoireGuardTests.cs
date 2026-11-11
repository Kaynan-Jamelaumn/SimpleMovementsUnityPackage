#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>The grimoire's guards against duplication and abuse: cooldowns and hourly limits per Mage, and the audit log of every book.</summary>
    public class GrimoireGuardTests
    {
        private static GrimoireBalance Balance(Action<GrimoireBalance> edit = null)
        {
            GrimoireBalance g = RuneDefaults.Grimoire();
            edit?.Invoke(g);
            return g;
        }

        [Test]
        public void ACooldownKeepsAMageFromImprintingTwiceInARow()
        {
            GrimoireBalance g = Balance(b => { b.imprintCooldownSeconds = 3f; b.maxImprintsPerHour = 0; });
            var guard = new GrimoireGuard();
            Assert.IsTrue(guard.Allow(g, GrimoireAction.Imprint, 100, out _, out _));
            guard.Record(GrimoireAction.Imprint, 100);
            Assert.IsFalse(guard.Allow(g, GrimoireAction.Imprint, 101, out string why, out float wait));
            Assert.AreEqual(2f, wait, 1e-4f);
            StringAssert.Contains("imprint again", why);
            Assert.IsTrue(guard.Allow(g, GrimoireAction.Imprint, 103.01, out _, out _), "after the cooldown");
            Assert.IsTrue(guard.Allow(g, GrimoireAction.Absorb, 101, out _, out _), "the actions have their own cooldowns");
        }

        [Test]
        public void ACountPerHourStopsAScriptAndTheWindowSlides()
        {
            GrimoireBalance g = Balance(b => { b.absorbCooldownSeconds = 0f; b.maxAbsorbsPerHour = 3; });
            var guard = new GrimoireGuard();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(guard.Allow(g, GrimoireAction.Absorb, 1000 + i, out _, out _), "absorb " + i);
                guard.Record(GrimoireAction.Absorb, 1000 + i);
            }
            Assert.IsFalse(guard.Allow(g, GrimoireAction.Absorb, 1500, out string why, out float wait));
            Assert.AreEqual(3100f, wait, 1f, "until the first one is an hour old");
            StringAssert.Contains("3 in an hour", why);
            Assert.AreEqual(3, guard.CountInLastHour(GrimoireAction.Absorb, 1500));
            Assert.IsTrue(guard.Allow(g, GrimoireAction.Absorb, 4600.5, out _, out _), "an hour later the first has left the window");
            Assert.AreEqual(2, guard.CountInLastHour(GrimoireAction.Absorb, 4600.5));
        }

        [Test]
        public void ZeroMeansNoLimitErasingIsNeverLimitedAndAMageDoesNotShareTheirLimits()
        {
            GrimoireBalance none = Balance(b => { b.imprintCooldownSeconds = 0f; b.absorbCooldownSeconds = 0f; b.maxImprintsPerHour = 0; b.maxAbsorbsPerHour = 0; });
            var guard = new GrimoireGuard();
            for (int i = 0; i < 500; i++)
            {
                Assert.IsTrue(guard.Allow(none, GrimoireAction.Imprint, i, out _, out _));
                guard.Record(GrimoireAction.Imprint, i);
            }
            GrimoireBalance strict = Balance(b => b.imprintCooldownSeconds = 60f);
            guard.Record(GrimoireAction.Erase, 10);
            Assert.IsTrue(guard.Allow(strict, GrimoireAction.Erase, 10, out _, out _), "erasing frees a page: never limited");
            Assert.IsTrue(guard.Allow(null, GrimoireAction.Imprint, 10, out _, out _), "no balance: no limit");
            Assert.IsTrue(new GrimoireGuard().Allow(strict, GrimoireAction.Imprint, 10, out _, out _), "another Mage's guard is their own");
            guard.Clear();
            Assert.AreEqual(0, guard.CountInLastHour(GrimoireAction.Imprint, 600));
        }

        [Test]
        public void AnActionMustWaitAfterARefusalToo()
        {
            // A refused request is not recorded: spamming a refused action does not push the next allowed one away.
            GrimoireBalance g = Balance(b => { b.imprintCooldownSeconds = 5f; b.maxImprintsPerHour = 0; });
            var guard = new GrimoireGuard();
            guard.Record(GrimoireAction.Imprint, 100);
            for (int i = 1; i < 5; i++) Assert.IsFalse(guard.Allow(g, GrimoireAction.Imprint, 100 + i, out _, out _));
            Assert.IsTrue(guard.Allow(g, GrimoireAction.Imprint, 105, out _, out _));
        }

        [Test]
        public void EveryBookKeepsALogOfWhatWasDoneWithItAndOnlyTheLastEntries()
        {
            RuneCatalog c = RuneDefaults.Catalog();
            GrimoireData book = new GrimoireLedger().Create("abc123", "k1", "Aria", 10, 1L);
            var rune = new RuneInstance { runeId = "wind", rank = 2, quality = 55f };
            for (int i = 0; i < 7; i++) book.AddLog(GrimoireAudit.Entry(i % 2 == 0 ? "imprint" : "absorb", "k" + i, "Mage" + i, i, rune, 1000 + i), 5);
            Assert.AreEqual(5, book.log.Count, "the oldest go");
            Assert.AreEqual("k2", book.log[0].playerKey);
            Assert.AreEqual("k6", book.log[4].playerKey);
            book.AddLog(GrimoireAudit.Entry("imprint", "x", "X", 0, rune, 1), 0);
            book.AddLog(null, 5);
            Assert.AreEqual(5, book.log.Count, "a balance of 0 keeps no log");

            GrimoireLedger ledger = new GrimoireLedger();
            GrimoireData b2 = ledger.Create("zz", "k", "A", 10, 1L);
            b2.AddLog(GrimoireAudit.Entry("refused", "k", "A", 3, rune, 5L, "imprint: too fast"), 10);
            GrimoireLedger back = GrimoireLedger.FromJson(ledger.ToJson(), 10);
            GrimoireLogEntry e = back.Find("zz").log[0];
            Assert.AreEqual("refused", e.action);
            Assert.AreEqual("wind", e.runeId);
            Assert.AreEqual(2, e.rank);
            Assert.AreEqual("imprint: too fast", e.note);
            Assert.AreEqual(1, b2.Clone().log.Count);
            Assert.AreNotSame(b2.log[0], b2.Clone().log[0]);
        }

        [Test]
        public void TheAuditTextSaysWhoDidWhatAndACompositeRuneIsNamed()
        {
            GrimoireData book = new GrimoireLedger().Create("abc123", "k1", "Aria", 10, 1L);
            var comp = new RuneInstance { runeId = "composite_x", rank = 1, quality = 70f, composite = new CompositeProfile { active = true, name = "Storm Lance" } };
            book.AddLog(GrimoireAudit.Entry("imprint", "k1", "Aria", 0, comp, new DateTime(2026, 11, 10, 20, 0, 0, DateTimeKind.Utc).Ticks), 10);
            book.AddLog(GrimoireAudit.Entry("refused", "k2", "Bram", 1, null, new DateTime(2026, 11, 10, 20, 1, 0, DateTimeKind.Utc).Ticks, "absorb: slow down"), 10);
            string text = GrimoireAudit.Text(book);
            StringAssert.Contains("Grimoire abc123 of Aria", text);
            StringAssert.Contains("2026-11-10 20:00:00 imprint by Aria (k1) page 1 composite:Storm Lance", text);
            StringAssert.Contains("refused by Bram (k2) page 2 — absorb: slow down", text);
            Assert.AreEqual("", GrimoireAudit.Text(null));
            Assert.IsFalse(GrimoireAudit.Text(book, 1).Contains("Aria (k1)"), "only the last entries");
        }

        [Test]
        public void TheBalanceWarnsWhenNothingLimitsCopyingOrNothingIsLogged()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            RuneDefaults.Grimoire().Validate(errors, warnings);
            Assert.IsEmpty(warnings, "the built-in numbers limit copying and keep a log");
            Balance(b => { b.imprintCooldownSeconds = 0; b.absorbCooldownSeconds = 0; b.maxImprintsPerHour = 0; b.maxAbsorbsPerHour = 0; b.logEntries = 0; }).Validate(errors, warnings);
            Assert.AreEqual(2, warnings.Count, string.Join("; ", warnings));
            Assert.IsEmpty(errors);
        }
    }
}
#endif
