using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuneMagic
{
    // The Grimoire on the soul: a Mage's own book (given once, as an item), and a Mage who holds any grimoire reading it,
    // imprinting a rune of their soul on a page of their own book, absorbing a print from a page, erasing. The rules are in
    // GrimoireRules (pure); this is where they meet the soul, the inventory and the world's ledger. Everything that changes
    // anything happens where the rules run (offline, the server); an owner's client asks with requests and sees the view.
    public partial class RuneSoul
    {
        /// <summary>Seconds after the soul is ready before the Mage's own book is looked for in the inventory (it may arrive with the inventory).</summary>
        private const float GrimoireCheckDelay = 3f;

        private readonly GrimoireView grimoireView = new GrimoireView();
        private readonly GrimoireView grimoireScratch = new GrimoireView();
        private int grimoireVersion;
        private string openGrimoireId = "";
        private int grimoireOpenSeq;
        private float grimoireReadyAt = float.NaN;
        private bool grimoireChecked;
        private float nextGrimoireTry;
        private float nextGrimoireView;
        private readonly GrimoireGuard grimoireGuard = new GrimoireGuard(); // how fast this Mage may copy runes (resets with the game)
        private readonly List<InventoryItem> bagScratch = new List<InventoryItem>(); // reused: the window looks twice a second

        /// <summary>A grimoire's content changed or was opened (the view the owner shows).</summary>
        public event Action<RuneSoul> GrimoireChanged;

        /// <summary>The grimoire the player has open (on the owner's client: as the server sent it).</summary>
        public GrimoireView GrimoireView => grimoireView;

        /// <summary>Changes whenever <see cref="GrimoireView"/> changes (the network layer sends it then).</summary>
        public int GrimoireVersion => grimoireVersion;

        private static GrimoireBalance GrimoireSettings => Catalog.Grimoire;

        // ------------------------------------------------------------------ what a player holds (any machine)
        /// <summary>The grimoires in the player's inventory (the bag and the hotbar). On the owner's client: its copy of the inventory.</summary>
        public List<GrimoireItemSO> HeldGrimoires(List<GrimoireItemSO> into = null)
        {
            into = into ?? new List<GrimoireItemSO>();
            into.Clear();
            InventoryManager inv = InventoryManager.For(this);
            if (inv == null) return into;
            List<InventoryItem> stacks = inv.GetBagItems(bagScratch);
            for (int i = 0; i < stacks.Count; i++)
                if (stacks[i].itemScriptableObject is GrimoireItemSO g && !into.Contains(g)) into.Add(g);
            return into;
        }

        // ------------------------------------------------------------------ requests (any machine: the UI's entry points)
        /// <summary>Opens the grimoire <paramref name="itemId"/> the player holds (offline / host: now; client: asks the server, which sends the book).</summary>
        public bool RequestOpenGrimoire(string itemId)
        {
            if (!GrimoireIds.IsId(itemId)) return false;
            if (!IsAuthority) return Send(RuneRequestKind.GrimoireOpen, itemId, 0f, false);
            return OpenGrimoire(itemId, out _);
        }

        /// <summary>Imprints the rune <paramref name="runeInstanceId"/> of the soul on <paramref name="page"/> of the grimoire (the author's).</summary>
        public bool RequestImprint(string itemId, int page, string runeInstanceId)
        {
            if (!GrimoireIds.IsId(itemId) || string.IsNullOrEmpty(runeInstanceId)) return false;
            if (!IsAuthority) return Send(RuneRequestKind.GrimoireImprint, GrimoireRequestText.Imprint(itemId, runeInstanceId), page, false);
            return ImprintToGrimoire(itemId, page, runeInstanceId, out _);
        }

        /// <summary>Absorbs the print on <paramref name="page"/> into the soul (once: the page is spent).</summary>
        public bool RequestAbsorbPrint(string itemId, int page)
        {
            if (!GrimoireIds.IsId(itemId)) return false;
            if (!IsAuthority) return Send(RuneRequestKind.GrimoireAbsorb, itemId, page, false);
            return AbsorbFromGrimoire(itemId, page, out _);
        }

        /// <summary>Erases the print on <paramref name="page"/> of the author's own grimoire.</summary>
        public bool RequestErasePrint(string itemId, int page)
        {
            if (!GrimoireIds.IsId(itemId)) return false;
            if (!IsAuthority) return Send(RuneRequestKind.GrimoireErase, itemId, page, false);
            return EraseGrimoirePage(itemId, page, out _);
        }

        private void HandleGrimoireRequest(RuneSoulRequest request)
        {
            int page = GrimoireRequestText.PageOf(request.amount, 4096);
            switch (request.kind)
            {
                case RuneRequestKind.GrimoireOpen:
                    OpenGrimoire(request.text, out _);
                    break;
                case RuneRequestKind.GrimoireImprint:
                    if (GrimoireRequestText.TrySplitImprint(request.text, out string itemId, out string runeId) && page >= 0)
                        ImprintToGrimoire(itemId, page, runeId, out _);
                    else
                        Say("That grimoire request could not be read.");
                    break;
                case RuneRequestKind.GrimoireAbsorb:
                    if (page >= 0) AbsorbFromGrimoire(request.text, page, out _);
                    else Say("There is no such page.");
                    break;
                case RuneRequestKind.GrimoireErase:
                    if (page >= 0) EraseGrimoirePage(request.text, page, out _);
                    else Say("There is no such page.");
                    break;
            }
        }

        // ------------------------------------------------------------------ limits and the audit log
        /// <summary>May the Mage do <paramref name="action"/> now? A refusal is said to them and written in the book's log.</summary>
        private bool GuardAllows(GrimoireAction action, GrimoireData book, int page, out string reason)
        {
            if (grimoireGuard.Allow(GrimoireSettings, action, Time.unscaledTime, out reason, out _)) return true;
            LogGrimoire(book, "refused", page, book.Page(page)?.print, action.ToString().ToLowerInvariant() + ": " + reason);
            Say(reason);
            return false;
        }

        private void LogGrimoire(GrimoireData book, string action, int page, RuneInstance print, string note = "")
        {
            GrimoireBalance g = GrimoireSettings;
            GrimoireLogEntry e = GrimoireAudit.Entry(action, GrimoireAuthorKey(), GrimoireAuthorName(), page, print, DateTime.UtcNow.Ticks, note);
            book.AddLog(e, g != null ? g.logEntries : 0);
            if (g != null && g.logToConsole)
                Debug.Log($"[Rune Magic] grimoire {book.serial}: {e.action} by {e.playerName} ({e.playerKey}) page {page + 1} {e.runeId} r{e.rank}{(string.IsNullOrEmpty(note) ? "" : " — " + note)}", this);
        }

        // ------------------------------------------------------------------ the rules (where they run)
        /// <summary>The key that says who wrote a book: the player's lasting key, or (no key at all) this soul's for the session.</summary>
        private string GrimoireAuthorKey()
        {
            string key = PlayerKeys.Of(Entity);
            return string.IsNullOrEmpty(key) ? "session:" + GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture) : key;
        }

        private string GrimoireAuthorName()
        {
            CombatEntity e = Entity;
            string n = e != null ? PartySystem.NameOf(e) : "";
            return string.IsNullOrWhiteSpace(n) ? "Mage" : n;
        }

        /// <summary>
        /// Checks that <paramref name="itemId"/> is a grimoire this player may use now: the soul is a rune user's, the grimoire is in the world's
        /// ledger, and the player holds its item. On success the book and its item are returned.
        /// </summary>
        private bool TryHeldGrimoire(string itemId, out GrimoireData book, out GrimoireItemSO item, out string reason, bool fresh = true)
        {
            book = null;
            item = null;
            reason = "";
            if (!IsAuthority) { reason = "Only the server reads grimoires."; return false; }
            if (GrimoireSettings == null) { reason = "Grimoires are off."; return false; }
            if (!CheckRuneUser(fresh)) { reason = "Only a Mage can read a grimoire."; return false; }
            if (!GrimoireIds.TryParse(itemId, out string serial, out _)) { reason = "That is not a grimoire."; return false; }
            item = GrimoireItems.Resolve(itemId);
            InventoryManager inv = InventoryManager.For(this);
            if (item == null || inv == null || inv.CountItem(item) < 1) { reason = "You do not hold that grimoire."; return false; }
            book = GrimoireStore.Find(serial);
            if (book == null) { reason = "The pages of that grimoire are lost."; return false; }
            book.EnsureLists(GrimoireSettings.pages);
            return true;
        }

        /// <summary>The player opens a grimoire they hold: its view is built (and sent to the owner), and its window opens.</summary>
        public bool OpenGrimoire(string itemId, out string reason)
        {
            if (!TryHeldGrimoire(itemId, out GrimoireData book, out _, out reason))
            {
                Say(reason);
                return false;
            }
            openGrimoireId = itemId;
            grimoireOpenSeq++;
            RefreshGrimoireView(true, true);
            return true;
        }

        /// <summary>Imprints a rune of the soul on a page of a grimoire the player holds (the energy cost comes off the rune).</summary>
        public bool ImprintToGrimoire(string itemId, int page, string runeInstanceId, out string reason)
        {
            if (!TryHeldGrimoire(itemId, out GrimoireData book, out _, out reason)) { Say(reason); return false; }
            if (!GuardAllows(GrimoireAction.Imprint, book, page, out reason)) return false;
            ImprintPlan plan = GrimoireRules.Imprint(Catalog, state, book, GrimoireAuthorKey(), GrimoireAuthorName(), page, runeInstanceId, DateTime.UtcNow.Ticks);
            if (!plan.ok) { reason = plan.reason; Say(reason); return false; }
            RuneInstance r = state.Rune(runeInstanceId);
            grimoireGuard.Record(GrimoireAction.Imprint, Time.unscaledTime);
            LogGrimoire(book, "imprint", page, book.Page(page).print);
            GrimoireStore.Changed(book);
            energyVersion++; // the rune lost energy: nothing else about the runes changed
            MarkSaveDirty(true);
            Say($"You imprint {RuneRules.DisplayName(Catalog, r)} on page {page + 1}: it loses {plan.cost:N0} energy.");
            if (logEvents) Debug.Log($"[Rune Magic] {name}: imprinted {r.runeId} on {book.serial} page {page + 1} (cost {plan.cost:0}).", this);
            RefreshGrimoireView(true, true);
            Refresh();
            return true;
        }

        /// <summary>Absorbs the print of a page of a grimoire the player holds: a new rune with no energy, if the soul has room. The page is spent.</summary>
        public bool AbsorbFromGrimoire(string itemId, int page, out string reason)
        {
            if (!TryHeldGrimoire(itemId, out GrimoireData book, out _, out reason)) { Say(reason); return false; }
            RuneBalance b = Balance;
            if (!GrimoireRules.CanAbsorb(Catalog, state, ComputeCapacity(b), book, page, out _, out reason)) { Say(reason); return false; }
            if (!GuardAllows(GrimoireAction.Absorb, book, page, out reason)) return false;
            GrimoirePage p = book.Page(page);
            RuneInstance printed = p.print.Clone(); // the page is blank once it is absorbed: the log keeps what it held
            RuneDef def = p.print.IsComposite ? null : Catalog.Rune(p.print.runeId);
            float strain = def != null ? RuneRules.AbsorptionDamage(Catalog, state, def) : 0f;
            string printName = RuneRules.DisplayName(Catalog, p.print);
            RuneInstance r = GrimoireRules.Absorb(Catalog, state, book, page, Guid.NewGuid().ToString("N"), DateTime.UtcNow.Ticks);
            if (r == null) { reason = "The print could not be absorbed."; Say(reason); return false; }
            grimoireGuard.Record(GrimoireAction.Absorb, Time.unscaledTime);
            LogGrimoire(book, "absorb", page, printed);
            GrimoireStore.Changed(book);
            runesVersion++;
            MarkSaveDirty(true);
            if (strain > 0f) Damage(strain, "incompatible_absorption", printName);
            int used = RuneRules.UsedSlots(Catalog, state), cap = ComputeCapacity(b);
            Say($"You absorb the print of {printName} into your soul ({used} / {cap} slots): it holds {r.energy:N0} energy." +
                (strain > 0f ? " Its alignment clashes with your runes: your soul is strained." : ""));
            if (logEvents) Debug.Log($"[Rune Magic] {this.name}: absorbed a print of {r.runeId} from {book.serial} page {page + 1}.", this);
            RuneAbsorbed?.Invoke(this, r);
            RefreshGrimoireView(true, true);
            Refresh();
            return true;
        }

        /// <summary>The author erases a print from a page of their own grimoire (nothing is given back).</summary>
        public bool EraseGrimoirePage(string itemId, int page, out string reason)
        {
            if (!TryHeldGrimoire(itemId, out GrimoireData book, out _, out reason)) { Say(reason); return false; }
            RuneInstance erased = book.Page(page)?.print?.Clone();
            if (!GrimoireRules.Erase(Catalog, book, GrimoireAuthorKey(), page, out _, out reason)) { Say(reason); return false; }
            LogGrimoire(book, "erase", page, erased);
            GrimoireStore.Changed(book);
            Say($"You erase page {page + 1}.");
            RefreshGrimoireView(true, true);
            return true;
        }

        // ------------------------------------------------------------------ the Mage's own book, and the open book's view
        /// <summary>Called every frame where the rules run, for a rune user.</summary>
        private void TickGrimoire()
        {
            GrimoireBalance g = GrimoireSettings;
            if (g == null) return;
            float now = Time.unscaledTime;
            if (!grimoireChecked && now >= nextGrimoireTry)
            {
                nextGrimoireTry = now + 1f;
                TryGiveGrimoire(g);
            }
            // A book that changed (the other holder's imprint or absorb) or that left the inventory: the owner's view follows.
            if (openGrimoireId.Length > 0 && now >= nextGrimoireView)
            {
                nextGrimoireView = now + 0.5f;
                RefreshGrimoireView(false, false);
            }
        }

        /// <summary>The player came back to life (a respawn may have cost them their items): the Mage's own book is looked for again, once.</summary>
        private void RearmGrimoire()
        {
            grimoireChecked = false;
            grimoireReadyAt = float.NaN;
        }

        /// <summary>Is the soul back from the world's store (so what it says about its grimoire is true)?</summary>
        private bool GrimoireSoulReady() => !RuneSoulSaves.Enabled || !WorldStore.IsOpen || RuneSoulSaves.IsKept(this);

        /// <summary>
        /// Gives the Mage their own grimoire: once (a new Mage "spawns with" one), and again once per visit when it is not in the
        /// inventory and the balance says the inventory is not kept (Reissue If Missing): once per visit and once after each respawn (which
        /// may cost items). Never twice in a row, so a Mage who hands the book to someone else keeps it handed.
        /// </summary>
        private void TryGiveGrimoire(GrimoireBalance g)
        {
            if (!g.giveOnSpawn && !g.reissueIfMissing) { grimoireChecked = true; return; }
            if (!GrimoireSoulReady()) return;
            InventoryManager inv = InventoryManager.For(this);
            if (inv == null || !inv.IsAuthoritative) return;
            if (float.IsNaN(grimoireReadyAt)) grimoireReadyAt = Time.unscaledTime;
            if (Time.unscaledTime - grimoireReadyAt < GrimoireCheckDelay) return;

            string key = GrimoireAuthorKey();
            GrimoireData own = string.IsNullOrEmpty(state.grimoireSerial) ? null : GrimoireStore.Find(state.grimoireSerial);
            if (own == null)
            {
                // A new Mage (or a world that lost the book): they get a new one, when the balance gives them out.
                if (!g.giveOnSpawn) { grimoireChecked = true; return; }
                own = GrimoireStore.Create(key, GrimoireAuthorName());
                state.grimoireSerial = own.serial;
                state.grimoireGiven = false;
                MarkSaveDirty(true);
            }
            else if (own.authorKey != key && own.authorKey.StartsWith("session:", StringComparison.Ordinal) && !key.StartsWith("session:", StringComparison.Ordinal))
            {
                // The player's key arrived after the book was made (it carried a session key): from now on the book is theirs.
                own.authorKey = key;
                GrimoireStore.Changed(own);
            }

            GrimoireItemSO item = GrimoireItems.For(own);
            if (item == null) { grimoireChecked = true; return; }
            if (inv.CountItem(item) >= 1)
            {
                grimoireChecked = true;
                if (!state.grimoireGiven) { state.grimoireGiven = true; MarkSaveDirty(true); }
                return;
            }
            bool firstGift = !state.grimoireGiven;
            if (!firstGift && !g.reissueIfMissing) { grimoireChecked = true; return; }
            if (inv.AddItem(item, 1) != 0) return; // no room in the bag now: tried again in a second
            grimoireChecked = true;
            state.grimoireGiven = true;
            MarkSaveDirty(true);
            Say(firstGift ? "You begin with a grimoire: use it to imprint your runes on its pages." : "Your grimoire is in your inventory.");
        }

        /// <summary>
        /// Rebuilds the view of the open grimoire (where the rules run) and raises the change when it differs from what was sent.
        /// <paramref name="force"/>: even when the book's revision is the one shown.
        /// </summary>
        private void RefreshGrimoireView(bool force, bool fresh)
        {
            if (openGrimoireId.Length == 0)
                return;
            if (!TryHeldGrimoire(openGrimoireId, out GrimoireData book, out _, out _, fresh))
            {
                // It left the inventory (traded, dropped) or the soul is not a rune user's any more: the view is emptied.
                openGrimoireId = "";
                grimoireScratch.CopyFrom(new GrimoireView { openSeq = grimoireOpenSeq });
                SetGrimoireView(grimoireScratch);
                return;
            }
            if (!force && grimoireView.serial == book.serial && grimoireView.revision == book.revision && grimoireView.openSeq == grimoireOpenSeq)
                return;
            string key = GrimoireAuthorKey();
            GrimoireView built = GrimoireView.Build(Catalog, book, openGrimoireId, GrimoireRules.IsAuthor(book, key), GrimoireRules.MayWrite(Catalog, book, key));
            built.openSeq = grimoireOpenSeq;
            grimoireScratch.CopyFrom(built);
            SetGrimoireView(grimoireScratch);
        }

        private void SetGrimoireView(GrimoireView v)
        {
            if (v.SameAs(grimoireView)) return;
            grimoireView.CopyFrom(v);
            grimoireVersion++;
            GrimoireChanged?.Invoke(this);
            Changed?.Invoke(this);
        }

        /// <summary>The grimoire the server is showing the owner (owner's client, network layer).</summary>
        public void ApplyReplicatedGrimoire(GrimoireView v)
        {
            if (v == null || IsAuthority) return;
            v.EnsureLists();
            grimoireView.CopyFrom(v);
            grimoireVersion++;
            GrimoireChanged?.Invoke(this);
            Changed?.Invoke(this);
        }

        /// <summary>What an imprint of <paramref name="r"/> would cost it (the window shows it; the same number the server uses).</summary>
        public static float ImprintCostOf(RuneInstance r) => GrimoireRules.ImprintCost(Catalog, r);
    }
}
