using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RuneMagic
{
    /// <summary>
    /// One page of a grimoire: blank, or holding the print of a rune (a copy of the rune with no instance id and no energy,
    /// <see cref="GrimoireRules.Imprint"/>). A page whose print was absorbed is blank again, or spent for good when the balance says so.
    /// </summary>
    [Serializable]
    public class GrimoirePage
    {
        [Tooltip("Its place in the book (0 = the first page).")]
        public int index;
        [Tooltip("The page holds a print.")]
        public bool filled;
        [Tooltip("Nothing can be written on it again (its print was absorbed and the balance makes pages one-time).")]
        public bool spent;
        [Tooltip("The print (meaningful only while filled).")]
        public RuneInstance print;
        [Tooltip("Who imprinted it (the player's name).")]
        public string imprintedBy = "";
        [Tooltip("UTC ticks of the imprint.")]
        public long imprintedUtc;

        /// <summary>Blanks the page (its print is gone); <paramref name="spend"/>: it can never be written again.</summary>
        public void Clear(bool spend)
        {
            filled = false;
            print = null;
            imprintedBy = "";
            imprintedUtc = 0;
            if (spend) spent = true;
        }

        /// <summary>A deep copy.</summary>
        public GrimoirePage Clone()
        {
            var c = (GrimoirePage)MemberwiseClone();
            c.print = print?.Clone();
            return c;
        }

        /// <summary>Fills what an older ledger lacks; a page marked filled without a print is blank.</summary>
        public void EnsureLists()
        {
            if (imprintedBy == null) imprintedBy = "";
            if (print != null) print.EnsureLists();
            else filled = false;
        }
    }

    /// <summary>
    /// A grimoire (a book) as the world's ledger keeps it under its serial: its author (the player's lasting key and name), its pages
    /// and its audit log. Every item of the book (<c>grimoire:&lt;serial&gt;:&lt;author&gt;</c>) shows these pages; <see cref="revision"/>
    /// goes up with every change (<see cref="GrimoireLedger.Touch"/>), so the views follow.
    /// </summary>
    [Serializable]
    public class GrimoireData
    {
        [Tooltip("Lowercase letters and digits (GrimoireIds.CleanSerial): the book's name in the ledger and in its item id.")]
        public string serial = "";
        [Tooltip("The author's lasting player key (PlayerKeys), or a session key until it is known.")]
        public string authorKey = "";
        public string authorName = "";
        [Tooltip("UTC ticks.")]
        public long createdUtc;
        [Tooltip("Goes up with every change.")]
        public int revision;
        public List<GrimoirePage> pages = new List<GrimoirePage>();
        [Tooltip("Who imprinted, absorbed, erased or was refused, and when (the last Grimoire Balance's Log Entries).")]
        public List<GrimoireLogEntry> log = new List<GrimoireLogEntry>();

        /// <summary>The page at <paramref name="index"/> (null when there is none).</summary>
        public GrimoirePage Page(int index) => pages != null && index >= 0 && index < pages.Count ? pages[index] : null;

        /// <summary>Pages that hold a print.</summary>
        public int FilledCount()
        {
            int n = 0;
            if (pages != null) foreach (GrimoirePage p in pages) if (p != null && p.filled) n++;
            return n;
        }

        /// <summary>Pages that can be written on (neither filled nor spent).</summary>
        public int BlankCount()
        {
            int n = 0;
            if (pages != null) foreach (GrimoirePage p in pages) if (p != null && !p.filled && !p.spent) n++;
            return n;
        }

        /// <summary>
        /// Fills what an older ledger lacks and gives the book at least <paramref name="pageCount"/> pages. A page count only ever
        /// grows: a smaller balance never cuts a page (or its print) off.
        /// </summary>
        public void EnsureLists(int pageCount = 0)
        {
            if (serial == null) serial = "";
            if (authorKey == null) authorKey = "";
            if (authorName == null) authorName = "";
            if (pages == null) pages = new List<GrimoirePage>();
            if (log == null) log = new List<GrimoireLogEntry>();
            for (int i = 0; i < pages.Count; i++)
            {
                GrimoirePage p = pages[i];
                if (p == null) pages[i] = p = new GrimoirePage();
                p.index = i;
                p.EnsureLists();
            }
            while (pages.Count < pageCount) pages.Add(new GrimoirePage { index = pages.Count });
        }

        /// <summary>
        /// Adds <paramref name="entry"/> to the audit log, keeping the last <paramref name="keep"/> entries (0 or less: the balance keeps
        /// no log, nothing is added).
        /// </summary>
        public void AddLog(GrimoireLogEntry entry, int keep)
        {
            if (entry == null || keep <= 0) return;
            if (log == null) log = new List<GrimoireLogEntry>();
            log.Add(entry);
            if (log.Count > keep) log.RemoveRange(0, log.Count - keep);
        }

        /// <summary>A deep copy (pages, prints and log entries are its own).</summary>
        public GrimoireData Clone()
        {
            var c = (GrimoireData)MemberwiseClone();
            c.pages = new List<GrimoirePage>();
            if (pages != null) foreach (GrimoirePage p in pages) c.pages.Add(p?.Clone());
            c.log = new List<GrimoireLogEntry>();
            if (log != null) foreach (GrimoireLogEntry e in log) if (e != null) c.log.Add(e.Clone());
            return c;
        }
    }

    /// <summary>One line of a grimoire's audit log (<see cref="GrimoireAudit"/>).</summary>
    [Serializable]
    public class GrimoireLogEntry
    {
        [Tooltip("UTC ticks.")]
        public long utc;
        [Tooltip("imprint, absorb, erase or refused.")]
        public string action = "";
        public string playerKey = "";
        public string playerName = "";
        [Tooltip("The page (0 = the first).")]
        public int page;
        [Tooltip("The rune of the print (\"composite:<name>\" for a rune made by synthesis), empty when there was none.")]
        public string runeId = "";
        public int rank;
        public float quality;
        public string note = "";

        public GrimoireLogEntry Clone() => (GrimoireLogEntry)MemberwiseClone();
    }

    /// <summary>
    /// The world's grimoires (<see cref="GrimoireStore"/> keeps it in the world's store as JSON): one book per serial, a serial never
    /// taken twice. Plain data; <see cref="Version"/> counts the changes made in memory.
    /// </summary>
    [Serializable]
    public class GrimoireLedger
    {
        public List<GrimoireData> books = new List<GrimoireData>();

        [NonSerialized] private int changes;

        /// <summary>Goes up with every book made or changed (in memory, not saved).</summary>
        public int Version => changes;

        /// <summary>How many books the ledger holds.</summary>
        public int Count => books != null ? books.Count : 0;

        /// <summary>Every book.</summary>
        public IReadOnlyList<GrimoireData> All => books ?? (books = new List<GrimoireData>());

        /// <summary>The book with this serial (null when none).</summary>
        public GrimoireData Find(string serial)
        {
            if (string.IsNullOrEmpty(serial) || books == null) return null;
            string key = GrimoireIds.CleanSerial(serial);
            if (key.Length == 0) return null;
            foreach (GrimoireData b in books)
                if (b != null && string.Equals(b.serial, key, StringComparison.Ordinal)) return b;
            return null;
        }

        /// <summary>
        /// Makes a new book of <paramref name="pageCount"/> blank pages under <paramref name="serial"/> (cleaned): when that serial is taken
        /// or empty, under a new one (a serial that is taken is never reused). Returns the book, which is in the ledger.
        /// </summary>
        public GrimoireData Create(string serial, string authorKey, string authorName, int pageCount, long createdUtc)
        {
            if (books == null) books = new List<GrimoireData>();
            var book = new GrimoireData
            {
                serial = FreeSerial(serial), authorKey = authorKey ?? "", authorName = authorName ?? "", createdUtc = createdUtc,
            };
            book.EnsureLists(Math.Max(1, pageCount));
            books.Add(book);
            changes++;
            return book;
        }

        /// <summary>A book changed: its revision goes up (the views follow), and so does the ledger's <see cref="Version"/>.</summary>
        public void Touch(GrimoireData book)
        {
            if (book == null) return;
            book.revision++;
            changes++;
        }

        /// <summary>The ledger as JSON (the world's store keeps it).</summary>
        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>
        /// Reads a ledger. Never null and never throws: empty or broken text gives an empty ledger. A book without a usable serial is
        /// skipped, a serial is one book (the first), and every book has at least <paramref name="pageCount"/> pages.
        /// </summary>
        public static GrimoireLedger FromJson(string json, int pageCount)
        {
            var ledger = new GrimoireLedger();
            if (string.IsNullOrEmpty(json)) return ledger;
            GrimoireLedger read;
            try { read = JsonUtility.FromJson<GrimoireLedger>(json); }
            catch (Exception) { return ledger; }
            if (read?.books == null) return ledger;
            foreach (GrimoireData b in read.books)
            {
                if (b == null) continue;
                string serial = GrimoireIds.CleanSerial(b.serial);
                if (serial.Length == 0 || ledger.Find(serial) != null) continue;
                b.serial = serial;
                b.EnsureLists(pageCount);
                ledger.books.Add(b);
            }
            return ledger;
        }

        private string FreeSerial(string wanted)
        {
            string s = GrimoireIds.CleanSerial(wanted);
            if (s.Length > 0 && Find(s) == null) return s;
            if (s.Length > 0)
                for (int n = 2; n < 10000; n++)
                {
                    string suffix = n.ToString(CultureInfo.InvariantCulture);
                    string stem = s.Length + suffix.Length > GrimoireIds.MaxSerial ? s.Substring(0, GrimoireIds.MaxSerial - suffix.Length) : s;
                    if (Find(stem + suffix) == null) return stem + suffix;
                }
            string g;
            do g = Guid.NewGuid().ToString("N").Substring(0, 10); while (Find(g) != null);
            return g;
        }
    }
}
