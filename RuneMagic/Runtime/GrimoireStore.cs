using System;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// The world's grimoires (<see cref="GrimoireLedger"/>), kept where the rules run (offline, the server) in the world's store
    /// (<see cref="WorldStore"/>, section "runeMagicGrimoires"), written with the rest of the world. A grimoire is an item that changes
    /// hands; its pages stay here under its serial. Each world keeps its own. Without an open world (tools, tests) the ledger lives in
    /// memory only.
    /// </summary>
    public static class GrimoireStore
    {
        /// <summary>The section of the world's store.</summary>
        public const string Section = "runeMagicGrimoires";

        private static GrimoireLedger ledger = new GrimoireLedger();
        private static bool fromWorld;
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ledger = new GrimoireLedger();
            fromWorld = false;
            hooked = false;
        }

        private static int PageCount => RuneMagicDatabase.Instance.Catalog.Grimoire != null ? Math.Max(1, RuneMagicDatabase.Instance.Catalog.Grimoire.pages) : 10;

        /// <summary>The ledger of this world (what the world's store holds once it is open; before that, the books made so far).</summary>
        public static GrimoireLedger Ledger
        {
            get
            {
                Hook();
                if (!fromWorld && WorldStore.IsOpen) LoadFromWorld();
                return ledger;
            }
        }

        private static void Hook()
        {
            if (hooked || !Application.isPlaying) return;
            hooked = true;
            WorldStore.Committing += Write;
            WorldStore.WorldChanged += OnWorldChanged;
        }

        private static void LoadFromWorld()
        {
            fromWorld = true;
            GrimoireLedger kept = GrimoireLedger.FromJson(WorldStore.Get(Section), PageCount);
            // Books made before the world opened are kept too (a serial the world already has wins).
            foreach (GrimoireData b in ledger.All)
                if (kept.Find(b.serial) == null)
                {
                    GrimoireData made = kept.Create(b.serial, b.authorKey, b.authorName, PageCount, b.createdUtc);
                    made.pages = b.pages;
                }
            ledger = kept;
        }

        /// <summary>The grimoire with this serial (null when the world has none).</summary>
        public static GrimoireData Find(string serial) => Ledger.Find(serial);

        /// <summary>Makes a new grimoire for the player <paramref name="authorKey"/> (a random serial).</summary>
        public static GrimoireData Create(string authorKey, string authorName)
        {
            string serial = Guid.NewGuid().ToString("N").Substring(0, 10);
            GrimoireData book = Ledger.Create(serial, authorKey, authorName, PageCount, DateTime.UtcNow.Ticks);
            Changed(book);
            return book;
        }

        /// <summary>A book changed: its revision goes up and the world is written in a moment.</summary>
        public static void Changed(GrimoireData book)
        {
            Ledger.Touch(book);
            if (fromWorld) WorldStore.MarkDirty();
        }

        private static void Write()
        {
            if (!fromWorld || ledger == null) return;
            WorldStore.Put(Section, ledger.ToJson());
        }

        private static void OnWorldChanged()
        {
            // Another world: what was read belongs to the old one; the new world's books are read when asked for.
            ledger = new GrimoireLedger();
            fromWorld = false;
        }

        /// <summary>An empty, unattached ledger (tests).</summary>
        internal static void ResetForTests()
        {
            ledger = new GrimoireLedger();
            fromWorld = false;
        }
    }
}
