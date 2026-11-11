#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RuneMagic.Tests
{
    /// <summary>
    /// Edit Mode tests of the soul components (Phase 2) that need Unity's engine (game objects, assets): they run in the
    /// Unity Test Runner (category "RequiresEngine").
    /// </summary>
    [Category("RequiresEngine")]
    public class RuneSoulRuntimeTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
        }

        private T Make<T>(T o) where T : Object
        {
            made.Add(o);
            return o;
        }

        [Test]
        public void ARuneSoulBringsItsStatusAndIsInertWithoutARuneUser()
        {
            GameObject go = Make(new GameObject("Not a player"));
            var soul = go.AddComponent<RuneSoul>();
            var status = go.GetComponent<SoulIntegrityManager>();
            Assert.IsNotNull(status, "the Soul Integrity Manager comes with the Rune Soul");
            Assert.IsFalse(soul.IsRuneUser, "no player, no rune user");
            Assert.IsFalse(status.Shown, "the Soul gauge is hidden");
            SoulDamageResult r = soul.Damage(50f, "synthesis");
            Assert.AreEqual(0f, r.applied, "nothing to damage");
            Assert.AreEqual(0f, soul.Restore(10f));
            Assert.IsFalse(soul.CanMeditate(out _));
            Assert.AreSame(soul, RuneSoul.For(go));
            float before = status.CurrentValue;
            status.AddCurrentValue(-30f);
            status.ResetForRespawn(0f);
            Assert.AreEqual(before, status.CurrentValue, 0.0001f, "the status API changes nothing on an inert soul");
        }

        [Test]
        public void SoulRestorativesDescribeThemselves()
        {
            var item = Make(ScriptableObject.CreateInstance<SoulRestorativeSO>());
            item.restoreAmount = 15f;
            item.deepHealAmount = 5f;
            item.overSeconds = 10f;
            var lines = new List<string>();
            item.AppendTooltip(lines);
            Assert.IsTrue(lines.Exists(l => l.Contains("Soul Integrity +15 over 10 s")), string.Join("\n", lines));
            Assert.IsTrue(lines.Exists(l => l.Contains("deep soul damage 5")), string.Join("\n", lines));

            item.restoreAmount = 0f;
            item.deepHealAmount = 0f;
            var warnings = new List<string>();
            item.ValidateItem(new List<string>(), warnings);
            Assert.IsTrue(warnings.Exists(w => w.Contains("restores nothing")), string.Join("\n", warnings));

            // Used by someone without a soul: only the ordinary effects (none here), no error.
            GameObject nobody = Make(new GameObject("Nobody"));
            Assert.DoesNotThrow(() => item.UseItem(nobody, null));
        }

        [Test]
        public void SanctuariesContainByRadiusAndTheStrongestWins()
        {
            GameObject a = Make(new GameObject("Shrine"));
            var weak = a.AddComponent<SoulSanctuary>();
            GameObject b = Make(new GameObject("Great Shrine"));
            b.transform.position = new Vector3(4f, 0f, 0f);
            var strong = b.AddComponent<SoulSanctuary>();
            // Both use the database values; give the second its own, stronger ones.
            var so = new UnityEditor.SerializedObject(strong);
            so.FindProperty("useDatabaseValues").boolValue = false;
            so.FindProperty("multiplier").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(weak.Contains(new Vector3(3f, 0f, 0f)));
            Assert.IsFalse(weak.Contains(new Vector3(20f, 0f, 0f)));
            Assert.AreSame(strong, SoulSanctuary.Find(new Vector3(2f, 0f, 0f)), "overlapping: the strongest counts");
            Assert.AreSame(weak, SoulSanctuary.Find(new Vector3(-6f, 0f, 0f)));
            Assert.IsNull(SoulSanctuary.Find(new Vector3(50f, 0f, 50f)));
            b.SetActive(false);
            Assert.AreSame(weak, SoulSanctuary.Find(new Vector3(2f, 0f, 0f)), "a disabled sanctuary does not count");
        }

        [Test]
        public void AttunementsAreInertWithoutASoul()
        {
            GameObject go = Make(new GameObject("Attuned"));
            var attunement = go.AddComponent<SoulAttunement>();
            Assert.DoesNotThrow(() => attunement.Set(2f, 1f, 3, 10f));
            Assert.DoesNotThrow(() => Object.DestroyImmediate(attunement));
        }

        [Test]
        public void ARitualSiteIsHiddenFromPlayersWithoutASoul()
        {
            GameObject go = Make(new GameObject("Altar"));
            var site = go.AddComponent<SoulRitualSite>();
            Assert.IsFalse(site.CanInteract(null, out string reason));
            Assert.AreEqual("", reason, "no prompt, no reason");
            Assert.IsTrue(site.InRange(go.transform.position + new Vector3(3f, 0f, 0f)), "range plus a metre of slack");
            Assert.IsFalse(site.InRange(go.transform.position + new Vector3(10f, 0f, 0f)));
            Assert.AreEqual("Healing Ritual", site.DisplayName);
        }
    }
}
#endif
