using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RuneMagic
{
    /// <summary>Why a grimoire action was refused.</summary>
    public enum GrimoireRefusal
    {
        None = 0,
        /// <summary>No such grimoire (it is not in the world's ledger).</summary>
        NoGrimoire = 1,
        /// <summary>Only the author writes in it.</summary>
        NotAuthor = 2,
        NoSuchPage = 3,
        PageFilled = 4,
        PageBlank = 5,
        PageSpent = 6,
        NoSuchRune = 7,
        /// <summary>The rune is made by synthesis and the balance does not allow them.</summary>
        RuneNotAllowed = 8,
        /// <summary>The rune does not hold the energy an imprint costs.</summary>
        NotEnoughEnergy = 9,
        /// <summary>The soul has no room for the print.</summary>
        NoRoom = 10,
        /// <summary>The rune is unknown to the database (a print of it could not be read).</summary>
        UnknownRune = 11,
        /// <summary>The balance does not allow erasing.</summary>
        ErasingOff = 12,
    }

    /// <summary>What an imprint will cost, or why it cannot be made.</summary>
    public struct ImprintPlan
    {
        public bool ok;
        public GrimoireRefusal refusal;
        public string reason;
        /// <summary>Energy the rune of the soul loses.</summary>
        public float cost;
        /// <summary>What the rune holds now.</summary>
        public float energyBefore;

        public static ImprintPlan No(GrimoireRefusal refusal, string reason, float cost = 0f, float energy = 0f) =>
            new ImprintPlan { ok = false, refusal = refusal, reason = reason, cost = cost, energyBefore = energy };
    }

    /// <summary>
    /// The grimoire's rules (pure; where the rules run they are applied to the soul and to the book of the world's ledger).
    /// <list type="bullet">
    /// <item><b>Imprint</b>: a rune of the soul is copied on a blank page. The rune stays and loses <see cref="ImprintCost"/> of its
    /// energy; it must have that much, or nothing is done.</item>
    /// <item><b>Absorb</b>: a print becomes a rune of the soul (a new rune, with no energy by default) if the soul has room; the page
    /// is blank again (spent for good if the balance says so). A print is absorbed once.</item>
    /// <item><b>Erase</b>: the author blanks a page (nothing is given back).</item>
    /// </list>
    /// </summary>
    public static class GrimoireRules
    {
        /// <summary>What imprinting <paramref name="r"/> costs it: a share of its maximum energy, at least the balance's minimum.</summary>
        public static float ImprintCost(RuneCatalog c, RuneInstance r)
        {
            GrimoireBalance g = c?.Grimoire;
            if (g == null || r == null) return 0f;
            float max = EnergyRules.MaxEnergy(c.Balance, r);
            return Math.Max(g.minImprintCost, g.imprintEnergyFraction * max);
        }

        /// <summary>Is <paramref name="playerKey"/> the author of <paramref name="book"/>? (An empty key is nobody's.)</summary>
        public static bool IsAuthor(GrimoireData book, string playerKey) =>
            book != null && !string.IsNullOrEmpty(playerKey) && string.Equals(book.authorKey, playerKey, StringComparison.Ordinal);

        /// <summary>May this player write in the book (the author, or anyone when the balance does not restrict it)?</summary>
        public static bool MayWrite(RuneCatalog c, GrimoireData book, string playerKey) =>
            book != null && (c?.Grimoire == null || !c.Grimoire.onlyAuthorImprints || IsAuthor(book, playerKey));

        // ------------------------------------------------------------------ imprint
        /// <summary>Can the rune <paramref name="runeInstanceId"/> be imprinted on <paramref name="page"/>, and at what cost?</summary>
        public static ImprintPlan PlanImprint(RuneCatalog c, RuneSoulState soul, GrimoireData book, string playerKey, int page, string runeInstanceId)
        {
            if (c?.Grimoire == null || soul == null || book == null) return ImprintPlan.No(GrimoireRefusal.NoGrimoire, "That grimoire cannot be found.");
            if (!MayWrite(c, book, playerKey)) return ImprintPlan.No(GrimoireRefusal.NotAuthor, "Only its author writes in this grimoire: you can read it and absorb from it.");
            GrimoirePage p = book.Page(page);
            if (p == null) return ImprintPlan.No(GrimoireRefusal.NoSuchPage, "There is no such page.");
            if (p.filled) return ImprintPlan.No(GrimoireRefusal.PageFilled, "That page already holds a print.");
            if (p.spent) return ImprintPlan.No(GrimoireRefusal.PageSpent, "That page is spent: nothing can be written on it again.");
            RuneInstance r = soul.Rune(runeInstanceId);
            if (r == null) return ImprintPlan.No(GrimoireRefusal.NoSuchRune, "You do not hold that rune.");
            if (r.IsComposite && !c.Grimoire.allowComposite)
                return ImprintPlan.No(GrimoireRefusal.RuneNotAllowed, "A rune made by synthesis cannot be imprinted.");
            if (!r.IsComposite && c.Rune(r.runeId) == null)
                return ImprintPlan.No(GrimoireRefusal.UnknownRune, "That rune is not in the Rune Magic Database.");
            float cost = ImprintCost(c, r);
            if (r.energy + 1e-3f < cost)
                return ImprintPlan.No(GrimoireRefusal.NotEnoughEnergy,
                    $"{RuneRules.DisplayName(c, r)} holds {r.energy:N0} energy: an imprint costs {cost:N0}. Give it more energy first.", cost, r.energy);
            return new ImprintPlan { ok = true, refusal = GrimoireRefusal.None, reason = "", cost = cost, energyBefore = r.energy };
        }

        /// <summary>
        /// Imprints the rune: the page now holds its print and the rune loses the cost. Nothing changes when it is refused.
        /// </summary>
        public static ImprintPlan Imprint(RuneCatalog c, RuneSoulState soul, GrimoireData book, string playerKey, string playerName,
                                          int page, string runeInstanceId, long utcTicks)
        {
            ImprintPlan plan = PlanImprint(c, soul, book, playerKey, page, runeInstanceId);
            if (!plan.ok) return plan;
            RuneInstance r = soul.Rune(runeInstanceId);
            GrimoirePage p = book.Page(page);
            r.energy = Math.Max(0f, r.energy - plan.cost);
            RuneInstance print = r.Clone();
            print.instanceId = "";
            print.energy = 0f;
            print.acquiredUtc = 0;
            print.history = new List<string>();     // the print is the rune, not the story of this copy of it
            print.origin = new RuneOrigin { kind = "grimoire", source = book.authorName, utc = utcTicks };
            p.print = print;
            p.filled = true;
            p.imprintedUtc = utcTicks;
            p.imprintedBy = playerName ?? "";
            return plan;
        }

        // ------------------------------------------------------------------ absorb
        /// <summary>Can the print on <paramref name="page"/> be absorbed by <paramref name="soul"/> (capacity <paramref name="capacity"/>)?</summary>
        public static bool CanAbsorb(RuneCatalog c, RuneSoulState soul, int capacity, GrimoireData book, int page, out GrimoireRefusal refusal, out string reason)
        {
            refusal = GrimoireRefusal.None;
            reason = "";
            if (c?.Grimoire == null || soul == null || book == null) { refusal = GrimoireRefusal.NoGrimoire; reason = "That grimoire cannot be found."; return false; }
            GrimoirePage p = book.Page(page);
            if (p == null) { refusal = GrimoireRefusal.NoSuchPage; reason = "There is no such page."; return false; }
            if (!p.filled) { refusal = GrimoireRefusal.PageBlank; reason = "That page is blank."; return false; }
            if (!p.print.IsComposite && c.Rune(p.print.runeId) == null)
            {
                refusal = GrimoireRefusal.UnknownRune;
                reason = "The rune of that print is not in the Rune Magic Database.";
                return false;
            }
            int used = RuneRules.UsedSlots(c, soul), cost = RuneRules.SlotCost(c, p.print);
            if (!CapacityRules.CanFit(capacity, used, cost))
            {
                refusal = GrimoireRefusal.NoRoom;
                reason = $"Your soul has no room for {RuneRules.DisplayName(c, p.print)} (needs {cost} slot{(cost == 1 ? "" : "s")}, " +
                         $"{CapacityRules.Free(capacity, used)} free of {capacity}). Free a slot first.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Takes the print off the page into the soul (no checks: call <see cref="CanAbsorb"/> first): a new rune with the print's
        /// rank, quality and profile and the energy the balance gives it (none by default). The page is blank (or spent).
        /// </summary>
        public static RuneInstance Absorb(RuneCatalog c, RuneSoulState soul, GrimoireData book, int page, string newInstanceId, long utcTicks)
        {
            GrimoirePage p = book?.Page(page);
            if (soul == null || p == null || !p.filled) return null;
            soul.EnsureLists();
            RuneInstance r = p.print.Clone();
            r.instanceId = string.IsNullOrEmpty(newInstanceId) ? Guid.NewGuid().ToString("N") : newInstanceId;
            r.acquiredUtc = utcTicks;
            r.origin = new RuneOrigin { kind = "grimoire", source = book.authorName, utc = utcTicks };
            r.energy = 0f;
            if (c?.Grimoire != null && c.Grimoire.absorbedEnergyFraction > 0f)
                r.energy = Math.Min(EnergyRules.MaxEnergy(c.Balance, r), c.Grimoire.absorbedEnergyFraction * EnergyRules.MaxEnergy(c.Balance, r));
            soul.runes.Add(r);
            soul.runesAbsorbed++;
            p.Clear(c?.Grimoire != null && !c.Grimoire.pageFreeAfterAbsorb);
            return r;
        }

        // ------------------------------------------------------------------ erase
        /// <summary>Blanks a page of the author's book (the print is lost; the page stays usable).</summary>
        public static bool Erase(RuneCatalog c, GrimoireData book, string playerKey, int page, out GrimoireRefusal refusal, out string reason)
        {
            refusal = GrimoireRefusal.None;
            reason = "";
            if (c?.Grimoire == null || book == null) { refusal = GrimoireRefusal.NoGrimoire; reason = "That grimoire cannot be found."; return false; }
            if (!c.Grimoire.allowErase) { refusal = GrimoireRefusal.ErasingOff; reason = "Prints cannot be erased."; return false; }
            if (!IsAuthor(book, playerKey)) { refusal = GrimoireRefusal.NotAuthor; reason = "Only the author can erase a print."; return false; }
            GrimoirePage p = book.Page(page);
            if (p == null) { refusal = GrimoireRefusal.NoSuchPage; reason = "There is no such page."; return false; }
            if (!p.filled) { refusal = GrimoireRefusal.PageBlank; reason = "That page is blank."; return false; }
            p.Clear(false);
            return true;
        }
    }

    // ====================================================================== ids and wire formats
    /// <summary>
    /// The ids of grimoire items: <c>grimoire:&lt;serial&gt;:&lt;author&gt;</c>. The serial names the book in the world's ledger; the author's
    /// name (letters and digits) is only there so the item can be named on every machine without the ledger.
    /// </summary>
    public static class GrimoireIds
    {
        public const string Prefix = "grimoire:";
        public const int MaxSerial = 16;
        public const int MaxTag = 16;

        public static bool IsId(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>A serial as the ledger keeps it: lowercase letters and digits, at most <see cref="MaxSerial"/>; empty when nothing is left.</summary>
        public static string CleanSerial(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (sb.Length >= MaxSerial) break;
                if (ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9') sb.Append(ch);
                else if (ch >= 'A' && ch <= 'Z') sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>An author's name as an id part: letters, digits, '_' and '-', at most <see cref="MaxTag"/>; "Mage" when nothing is left.</summary>
        public static string CleanTag(string name)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(name))
                foreach (char ch in name)
                {
                    if (sb.Length >= MaxTag) break;
                    if (ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || ch == '_' || ch == '-') sb.Append(ch);
                }
            return sb.Length == 0 ? "Mage" : sb.ToString();
        }

        public static string Make(string serial, string authorName) => Prefix + CleanSerial(serial) + ":" + CleanTag(authorName);

        /// <summary>Reads an item id: false when it is not a well-formed grimoire id.</summary>
        public static bool TryParse(string id, out string serial, out string tag)
        {
            serial = "";
            tag = "";
            if (!IsId(id) || id.Length > Prefix.Length + MaxSerial + 1 + MaxTag) return false;
            string[] parts = id.Substring(Prefix.Length).Split(':');
            if (parts.Length != 2) return false;
            string s = CleanSerial(parts[0]);
            if (s.Length == 0 || !string.Equals(s, parts[0], StringComparison.Ordinal)) return false;
            if (parts[1].Length == 0 || !string.Equals(CleanTag(parts[1]), parts[1], StringComparison.Ordinal)) return false;
            serial = s;
            tag = parts[1];
            return true;
        }

        /// <summary>The serial of a grimoire id (empty when it is not one).</summary>
        public static string SerialOf(string id) => TryParse(id, out string s, out _) ? s : "";
    }

    /// <summary>The texts of the grimoire's requests (the wire carries a request kind, a text and a number).</summary>
    public static class GrimoireRequestText
    {
        /// <summary>Imprint: the grimoire item's id and the soul rune's instance id; the page index travels as the amount.</summary>
        public static string Imprint(string itemId, string runeInstanceId) => (itemId ?? "") + "|" + (runeInstanceId ?? "");

        public static bool TrySplitImprint(string text, out string itemId, out string runeInstanceId)
        {
            itemId = "";
            runeInstanceId = "";
            if (string.IsNullOrEmpty(text)) return false;
            int bar = text.IndexOf('|');
            if (bar <= 0 || bar >= text.Length - 1 || text.IndexOf('|', bar + 1) >= 0) return false;
            itemId = text.Substring(0, bar);
            runeInstanceId = text.Substring(bar + 1);
            return GrimoireIds.IsId(itemId) && runeInstanceId.Length <= 64;
        }

        /// <summary>A page index off the wire's amount (-1 when it is not a whole number in range).</summary>
        public static int PageOf(float amount, int pageCount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount)) return -1;
            int i = (int)Math.Round(amount);
            return i >= 0 && i < pageCount && Math.Abs(amount - i) < 0.01f ? i : -1;
        }
    }

    // ====================================================================== what the owner sees
    /// <summary>One page as the owner's window shows it.</summary>
    [Serializable]
    public class GrimoirePageView
    {
        public int index;
        public bool filled;
        public bool spent;
        public string name = "";
        public int rank;
        public float quality;
        public string qualityName = "";
        public int slots = 1;
        public bool composite;
        public string by = "";
        public string note = "";
        /// <summary>The rune of the print, for the window's icon colour (empty for a composite rune).</summary>
        public string runeId = "";
    }

    /// <summary>
    /// An open grimoire as its reader sees it (sent by the server to that reader alone). Holds only what is written on the pages.
    /// </summary>
    [Serializable]
    public class GrimoireView
    {
        public string itemId = "";
        public string serial = "";
        public string title = "";
        public string author = "";
        /// <summary>The reader is the author (they may write and erase).</summary>
        public bool isAuthor;
        /// <summary>The reader may write in it (author, or anyone when the balance does not restrict it).</summary>
        public bool canWrite;
        public bool canErase;
        public int revision;
        /// <summary>Counts the times the reader asked to open a book (a window opens when it changes).</summary>
        public int openSeq;
        public List<GrimoirePageView> pages = new List<GrimoirePageView>();

        public void EnsureLists()
        {
            if (pages == null) pages = new List<GrimoirePageView>();
            pages.RemoveAll(p => p == null);
            if (itemId == null) itemId = "";
            if (serial == null) serial = "";
            if (title == null) title = "";
            if (author == null) author = "";
        }

        public int Filled
        {
            get
            {
                int n = 0;
                if (pages != null) foreach (GrimoirePageView p in pages) if (p != null && p.filled) n++;
                return n;
            }
        }

        public GrimoirePageView Page(int index) => pages != null && index >= 0 && index < pages.Count ? pages[index] : null;

        public bool SameAs(GrimoireView o) =>
            o != null && serial == o.serial && revision == o.revision && isAuthor == o.isAuthor && pages.Count == o.pages.Count && itemId == o.itemId &&
            openSeq == o.openSeq && canWrite == o.canWrite;

        public void CopyFrom(GrimoireView o)
        {
            if (o == null) return;
            itemId = o.itemId ?? ""; serial = o.serial ?? ""; title = o.title ?? ""; author = o.author ?? "";
            isAuthor = o.isAuthor; canWrite = o.canWrite; canErase = o.canErase; revision = o.revision; openSeq = o.openSeq;
            pages = new List<GrimoirePageView>(o.pages ?? new List<GrimoirePageView>());
        }

        /// <summary>The view of <paramref name="book"/> for a reader (<paramref name="isAuthor"/>, <paramref name="canWrite"/>).</summary>
        public static GrimoireView Build(RuneCatalog c, GrimoireData book, string itemId, bool isAuthor, bool canWrite)
        {
            var v = new GrimoireView();
            if (book == null) return v;
            GrimoireBalance g = c?.Grimoire;
            v.itemId = itemId ?? "";
            v.serial = book.serial;
            v.author = book.authorName;
            v.title = (g != null && !string.IsNullOrWhiteSpace(g.bookName) ? g.bookName : "Grimoire") +
                      (string.IsNullOrEmpty(book.authorName) ? "" : " of " + book.authorName);
            v.isAuthor = isAuthor;
            v.canWrite = canWrite;
            v.canErase = isAuthor && g != null && g.allowErase;
            v.revision = book.revision;
            foreach (GrimoirePage p in book.pages)
            {
                var pv = new GrimoirePageView { index = p.index, filled = p.filled, spent = p.spent };
                if (p.filled && p.print != null)
                {
                    RuneInstance r = p.print;
                    QualityTier tier = c?.Balance.QualityTierOf(r.quality);
                    pv.name = RuneRules.DisplayName(c, r);
                    pv.rank = r.rank;
                    pv.quality = r.quality;
                    pv.qualityName = tier != null ? tier.name : "";
                    pv.slots = RuneRules.SlotCost(c, r);
                    pv.composite = r.IsComposite;
                    pv.by = p.imprintedBy;
                    pv.runeId = r.IsComposite ? "" : r.runeId;
                    pv.note = r.flawed ? "Flawed" : "";
                }
                v.pages.Add(pv);
            }
            return v;
        }
    }
}
