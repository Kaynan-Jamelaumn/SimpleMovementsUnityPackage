#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameNet.Tests
{
    /// <summary>
    /// Edit Mode tests for the always-compiled bridges the gameplay code asks: the authority of each mode (who decides,
    /// who has a player, who runs the rules of which object), realm separation of combat, the stable item ids and more
    /// command-line cases.
    /// </summary>
    public class AuthorityAndDatabaseTests
    {
        private readonly List<Object> created = new List<Object>();
        private Func<GameObject, bool> savedNetworked;
        private Func<GameObject, bool> savedOwner;
        private Func<CombatEntity, CombatEntity, bool> savedSeparation;
        private ItemDatabase savedDatabase;

        [SetUp]
        public void SetUp()
        {
            savedNetworked = NetAuthority.NetworkedObjectResolver;
            savedOwner = NetAuthority.LocalOwnerResolver;
            savedSeparation = CombatRules.Separation;
            savedDatabase = ItemDatabase.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            NetAuthority.SetMode(NetMode.Offline);
            NetAuthority.NetworkedObjectResolver = savedNetworked;
            NetAuthority.LocalOwnerResolver = savedOwner;
            CombatRules.Separation = savedSeparation;
            ItemDatabase.Override(savedDatabase);
            foreach (Object o in created)
                if (o != null)
                    Object.DestroyImmediate(o);
            created.Clear();
        }

        private GameObject Go(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        // ------------------------------------------------------------------ authority

        [TestCase(NetMode.Offline, true, false, true, false)]
        [TestCase(NetMode.Host, true, true, true, false)]
        [TestCase(NetMode.Server, true, true, false, false)]
        [TestCase(NetMode.Client, false, true, true, true)]
        public void EachModeKnowsWhoDecidesAndWhoPlays(NetMode mode, bool isServer, bool isNetworked, bool hasLocalPlayer, bool isClientOnly)
        {
            NetAuthority.SetMode(mode);
            Assert.AreEqual(mode, NetAuthority.Mode);
            Assert.AreEqual(isServer, NetAuthority.IsServer, "IsServer");
            Assert.AreEqual(isNetworked, NetAuthority.IsNetworked, "IsNetworked");
            Assert.AreEqual(hasLocalPlayer, NetAuthority.HasLocalPlayer, "HasLocalPlayer");
            Assert.AreEqual(isClientOnly, NetAuthority.IsClientOnly, "IsClientOnly");
        }

        [Test]
        public void ModeChangedFiresOnlyOnRealChanges()
        {
            NetAuthority.SetMode(NetMode.Offline);
            var seen = new List<NetMode>();
            Action<NetMode> listener = m => seen.Add(m);
            NetAuthority.ModeChanged += listener;
            try
            {
                NetAuthority.SetMode(NetMode.Offline);
                NetAuthority.SetMode(NetMode.Host);
                NetAuthority.SetMode(NetMode.Host);
                NetAuthority.SetMode(NetMode.Offline);
            }
            finally
            {
                NetAuthority.ModeChanged -= listener;
            }
            CollectionAssert.AreEqual(new[] { NetMode.Host, NetMode.Offline }, seen);
        }

        [Test]
        public void ObjectsAreOnlyNetworkedInASessionWithAResolver()
        {
            GameObject go = Go("Thing");
            NetAuthority.NetworkedObjectResolver = _ => true;
            NetAuthority.SetMode(NetMode.Offline);
            Assert.IsFalse(NetAuthority.IsNetworkedObject(go), "offline nothing is networked");
            NetAuthority.SetMode(NetMode.Client);
            Assert.IsTrue(NetAuthority.IsNetworkedObject(go));
            Assert.IsFalse(NetAuthority.IsNetworkedObject(null));
            NetAuthority.NetworkedObjectResolver = null;
            Assert.IsFalse(NetAuthority.IsNetworkedObject(go), "no resolver: nothing is networked");
        }

        [Test]
        public void ClientsOnlyRunTheRulesOfLocalObjects()
        {
            GameObject networked = Go("Mob");
            GameObject local = Go("Effect");
            NetAuthority.NetworkedObjectResolver = go => go == networked;

            NetAuthority.SetMode(NetMode.Client);
            Assert.IsFalse(NetAuthority.RunsRules(networked), "the server decides networked objects");
            Assert.IsTrue(NetAuthority.RunsRules(local), "local-only objects run here");

            NetAuthority.SetMode(NetMode.Server);
            Assert.IsTrue(NetAuthority.RunsRules(networked));
            NetAuthority.SetMode(NetMode.Host);
            Assert.IsTrue(NetAuthority.RunsRules(networked));
        }

        [Test]
        public void OnlyTheOwnerControlsAPlayerAndOthersSeeCopies()
        {
            GameObject mine = Go("Me");
            GameObject theirs = Go("Them");
            NetAuthority.NetworkedObjectResolver = _ => true;
            NetAuthority.LocalOwnerResolver = go => go == mine;

            NetAuthority.SetMode(NetMode.Client);
            Assert.IsTrue(NetAuthority.IsLocallyControlled(mine));
            Assert.IsFalse(NetAuthority.IsLocallyControlled(theirs));
            Assert.IsFalse(NetAuthority.IsRemoteCopy(mine));
            Assert.IsTrue(NetAuthority.IsRemoteCopy(theirs));

            NetAuthority.SetMode(NetMode.Host);
            Assert.IsFalse(NetAuthority.IsRemoteCopy(theirs), "the host decides: nothing is a copy there");

            NetAuthority.SetMode(NetMode.Server);
            NetAuthority.LocalOwnerResolver = _ => false;
            Assert.IsFalse(NetAuthority.IsLocallyControlled(mine), "nothing is controlled on a dedicated server");
            Assert.IsFalse(NetAuthority.IsLocallyControlled(null));
        }

        [Test]
        public void OfflineEveryObjectIsControlledHere()
        {
            NetAuthority.SetMode(NetMode.Offline);
            Assert.IsTrue(NetAuthority.IsLocallyControlled(Go("Player")));
            Assert.IsFalse(NetAuthority.IsRemoteCopy(Go("Other")));
        }

        // ------------------------------------------------------------------ realm separation

        [Test]
        public void EntitiesAreTogetherUnlessTheSeparationSaysOtherwise()
        {
            CombatEntity a = Go("A").AddComponent<CombatEntity>();
            CombatEntity b = Go("B").AddComponent<CombatEntity>();
            CombatRules.Separation = null;
            Assert.IsFalse(CombatRules.AreSeparated(a, b));
            Assert.IsTrue(CombatRules.CanInteract(a, b));

            CombatRules.Separation = (x, y) => true;
            Assert.IsTrue(CombatRules.AreSeparated(a, b));
            Assert.IsFalse(CombatRules.CanInteract(a, b));
            Assert.IsFalse(CombatRules.AreSeparated(a, a), "an entity is never separated from itself");
            Assert.IsFalse(CombatRules.AreSeparated(a, null));
        }

        // ------------------------------------------------------------------ item ids

        [Test]
        public void ItemsAreFoundByIdAndIdsByItem()
        {
            var db = Asset<ItemDatabase>();
            var sword = Asset<MiscItemSO>();
            var gem = Asset<MiscItemSO>();
            db.Register("sword-guid", sword);
            db.Register("gem-guid", gem);
            Assert.AreSame(sword, db.Find("sword-guid"));
            Assert.AreSame(gem, db.Find("gem-guid"));
            Assert.AreEqual("gem-guid", db.IdOf(gem));
            Assert.IsNull(db.Find("unknown"));
            Assert.IsNull(db.Find(null));
            Assert.IsNull(db.IdOf(null));
            Assert.IsNull(db.IdOf(Asset<MiscItemSO>()));
        }

        [Test]
        public void RegisteringAgainReplacesTheOldEntry()
        {
            var db = Asset<ItemDatabase>();
            var sword = Asset<MiscItemSO>();
            var axe = Asset<MiscItemSO>();
            db.Register("a", sword);
            Assert.AreSame(sword, db.Find("a"), "builds the index");
            db.Register("b", sword);
            Assert.IsNull(db.Find("a"), "the item's old id is gone (the index was rebuilt)");
            Assert.AreEqual("b", db.IdOf(sword));
            db.Register("b", axe);
            Assert.AreSame(axe, db.Find("b"), "the id now names the other item");
            Assert.IsNull(db.IdOf(sword));
            Assert.AreEqual(1, db.items.Count);
        }

        [Test]
        public void EmptyIdsAndNullItemsAreIgnored()
        {
            var db = Asset<ItemDatabase>();
            db.Register("", Asset<MiscItemSO>());
            db.Register(null, Asset<MiscItemSO>());
            db.Register("x", null);
            Assert.AreEqual(0, db.items.Count);
        }

        [Test]
        public void DuplicateEntriesKeepTheFirst()
        {
            var db = Asset<ItemDatabase>();
            var first = Asset<MiscItemSO>();
            var second = Asset<MiscItemSO>();
            db.items.Add(new ItemDatabase.Entry { id = "same", item = first });
            db.items.Add(new ItemDatabase.Entry { id = "same", item = second });
            db.items.Add(new ItemDatabase.Entry { id = "", item = second });
            Assert.AreSame(first, db.Find("same"));
            Assert.AreEqual("same", db.IdOf(second), "an item listed under a used id still gets that id");
        }

        [Test]
        public void TheDefaultDatabaseCanBeOverridden()
        {
            var db = Asset<ItemDatabase>();
            var potion = Asset<MiscItemSO>();
            db.Register("potion", potion);
            ItemDatabase.Override(db);
            Assert.AreSame(db, ItemDatabase.Instance);
            Assert.AreSame(potion, ItemDatabase.Item("potion"));
            Assert.AreEqual("potion", ItemDatabase.Id(potion));
            ItemDatabase.Override(null);
            Assert.IsNull(ItemDatabase.Item("potion"), "no database: no item");
            Assert.IsNull(ItemDatabase.Id(potion));
        }

        // ------------------------------------------------------------------ command line

        [Test]
        public void LastModeOnTheCommandLineWins()
        {
            NetLaunchOptions o = NetLaunchOptions.Parse(new[] { "-client", "-host" });
            Assert.AreEqual(NetMode.Host, o.mode);
        }

        [Test]
        public void PortsOutOfRangeAreIgnored()
        {
            Assert.AreEqual(NetLaunchOptions.DefaultPort, NetLaunchOptions.Parse(new[] { "-port", "70000" }).port);
            Assert.AreEqual(NetLaunchOptions.DefaultPort, NetLaunchOptions.Parse(new[] { "-port", "0" }).port);
            Assert.AreEqual(NetLaunchOptions.DefaultPort, NetLaunchOptions.Parse(new[] { "-port" }).port, "a flag without a value");
        }

        [Test]
        public void EveryModeSurvivesARoundTrip()
        {
            foreach (NetMode mode in Enum.GetValues(typeof(NetMode)))
            {
                string args = NetLaunchOptions.BuildArguments(mode, "127.0.0.1", 7790, "Bo", mode == NetMode.Server);
                NetLaunchOptions o = NetLaunchOptions.Parse(args.Split(' '));
                Assert.AreEqual(mode, o.mode, args);
                if (mode != NetMode.Offline)
                    Assert.AreEqual(7790, o.port, args);
            }
        }
    }
}
#endif
