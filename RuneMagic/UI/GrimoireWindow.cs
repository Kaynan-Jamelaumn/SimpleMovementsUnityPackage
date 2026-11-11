using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// The Grimoire: the book a Mage starts with. Its pages hold <b>prints</b>, copies of runes of a Mage's soul.
    /// <list type="bullet">
    /// <item>On a blank page the book's author <b>imprints</b> a rune of their soul: a copy is written and the rune loses part of its energy
    /// (the window shows the cost; a rune that does not hold that much cannot be imprinted).</item>
    /// <item>A Mage who holds the book <b>absorbs</b> the print of a page: it becomes a rune of their soul with no energy, if the soul has
    /// room; the page is then used (a print is absorbed once).</item>
    /// <item>A Mage who holds <b>another Mage's grimoire</b> (a trade, a drop, a loot) reads it and absorbs from it the same way.</item>
    /// <item>The author may erase a print (nothing is given back).</item>
    /// </list>
    /// The window only <b>asks</b>: every action is a request the server checks again (<see cref="RuneSoul.RequestImprint"/> and friends).
    /// Opened by using a grimoire item, from the Rune Soul window's Grimoire button, or by the input action named in Settings ▸ Grimoire
    /// Action ("Grimoire") of the Input Actions asset (no key is fixed in code). Only rune users can open it. Created automatically.
    /// </summary>
    public class GrimoireWindow : MonoBehaviour, IGameplayPanel
    {
        private static GrimoireWindow instance;

        private GlobalInputBinding toggle;
        private Canvas canvas;
        private CanvasGroup group;
        private SocialPanelHandle handle;
        private bool open;

        private TextMeshProUGUI header, message;
        private RectTransform booksRow, pagesContent, rightContent;

        private RuneSoul soul;
        private int selectedPage = -1;
        private int shownVersion = -1, shownRunes = -1;
        private string shownSignature = "";
        private int seenOpenSeq;
        private int seenMessage;
        private float nextPoll;
        private float nextWatch;
        private float eraseUntil;
        private int lastFree = -1;
        private readonly List<GrimoireItemSO> held = new List<GrimoireItemSO>();
        private readonly StringBuilder signature = new StringBuilder();

        public string PanelName => "Grimoire";

        /// <summary>Is the window open?</summary>
        public static bool IsOpen => instance != null && instance.open;

        /// <summary>Opens or closes the window for the local player.</summary>
        public static void Toggle()
        {
            if (instance == null) return;
            if (instance.open) instance.Close();
            else instance.RequestOpen();
        }

        /// <summary>Opens the player's grimoire (the Rune Soul window's button): the book is asked for, and the window opens when it arrives.</summary>
        public static void Show()
        {
            if (instance != null && !instance.open) instance.RequestOpen();
        }

        public void ClosePanel() => Close();

        private static RuneCatalog Catalog => RuneMagicDatabase.Instance.Catalog;

        private void OnEnable()
        {
            instance = this;
            if (!Application.isBatchMode) // a headless server has no player to open it
                toggle = RuneMagicInput.Global(RuneMagicInput.GrimoireAction, "Grimoire", this);
        }

        private void OnDisable()
        {
            if (instance == this) instance = null;
            toggle?.Dispose();
            toggle = null;
            if (open) Close();
        }

        private void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        private void Update()
        {
            if (toggle != null && toggle.Pressed)
                Toggle();
            if (!open)
            {
                // A grimoire item was used (or the button asked): the server answered with the book. Looked for four times a second,
                // and never on a headless server (nobody there opens a window).
                if (Application.isBatchMode || Time.unscaledTime < nextWatch) return;
                nextWatch = Time.unscaledTime + 0.25f;
                RuneSoul s = RuneSoul.Local;
                if (s != null && s.GrimoireView.openSeq != seenOpenSeq)
                {
                    seenOpenSeq = s.GrimoireView.openSeq;
                    if (s.View.runeUser && s.GrimoireView.itemId.Length > 0) Open(s);
                }
                return;
            }
            // The book left the inventory (traded, dropped) or the player is no Mage any more: the server emptied the view.
            if (soul == null || !soul.View.runeUser || soul.GrimoireView.itemId.Length == 0)
            {
                Close();
                return;
            }
            if (eraseUntil > 0f && Time.unscaledTime > eraseUntil)
            {
                eraseUntil = 0f;
                shownSignature = "";
            }
            if (Time.unscaledTime < nextPoll)
                return;
            nextPoll = Time.unscaledTime + 0.2f;
            Refresh(false);
        }

        // ------------------------------------------------------------------ open / close
        private void RequestOpen()
        {
            RuneSoul s = RuneSoul.Local;
            if (s == null || !s.View.runeUser)
            {
                PlayerActions.Say("Only rune users (Mages) read a grimoire.");
                return;
            }
            s.HeldGrimoires(held);
            if (held.Count == 0)
            {
                PlayerActions.Say("You hold no grimoire.");
                return;
            }
            s.RequestOpenGrimoire(Preferred(s).RuntimeId);
        }

        /// <summary>The book to open: the one already open if still held, else the player's own (by the author's name), else the first.</summary>
        private GrimoireItemSO Preferred(RuneSoul s)
        {
            string current = s.GrimoireView.itemId;
            foreach (GrimoireItemSO g in held)
                if (g.RuntimeId == current) return g;
            CombatEntity e = s.Entity;
            string own = e != null ? GrimoireIds.CleanTag(PartySystem.NameOf(e)) : "";
            foreach (GrimoireItemSO g in held)
                if (g.Author == own) return g;
            return held[0];
        }

        private void Open(RuneSoul s)
        {
            soul = s;
            if (canvas == null) Build();
            RuneSoulWindow.CloseWindow();
            open = true;
            canvas.gameObject.SetActive(true);
            SocialUI.Show(group, true);
            if (handle == null) handle = new SocialPanelHandle(this);
            handle.Open();
            seenOpenSeq = soul.GrimoireView.openSeq;
            seenMessage = soul.View.messageSeq;
            message.text = "";
            eraseUntil = 0f;
            selectedPage = -1;
            shownSignature = "";
            Refresh(true);
        }

        public void Close()
        {
            open = false;
            if (group != null) SocialUI.Show(group, false);
            if (canvas != null) canvas.gameObject.SetActive(false);
            handle?.Close();
        }

        // ------------------------------------------------------------------ refreshing
        private void Refresh(bool force)
        {
            GrimoireView v = soul.GrimoireView;
            soul.HeldGrimoires(held);

            RuneSoulView sv = soul.View;
            if (sv.messageSeq != seenMessage)
            {
                seenMessage = sv.messageSeq;
                message.text = sv.message;
            }
            header.text = v.itemId.Length == 0 ? "<color=#9aa4b5>Opening the grimoire…</color>"
                : $"<b>{v.title}</b> · {v.Filled} of {v.pages.Count} pages written · " +
                  (v.isAuthor ? "<color=#7bd88f>your own book</color>" : "<color=#ffd34d>another Mage's book: you can read it and absorb from it</color>") +
                  $" · soul {soul.UsedSlots} / {soul.Capacity} slots";

            signature.Clear();
            signature.Append(soul.GrimoireVersion).Append('|').Append(selectedPage).Append('|').Append(v.itemId).Append('|');
            foreach (GrimoireItemSO g in held) signature.Append(g.RuntimeId).Append(',');
            string sig = signature.ToString();
            if (!force && sig == shownSignature && soul.RunesVersion == shownRunes && soul.FreeSlots == lastFree)
                return;
            shownSignature = sig;
            shownRunes = soul.RunesVersion;
            lastFree = soul.FreeSlots;
            if (selectedPage < 0 || selectedPage >= v.pages.Count) selectedPage = FirstUseful(v);
            Rebuild();
        }

        /// <summary>The page to show first: a blank one to write on for the author, else the first print.</summary>
        private static int FirstUseful(GrimoireView v)
        {
            for (int i = 0; i < v.pages.Count; i++)
                if (v.pages[i].filled) return i;
            if (v.canWrite)
                for (int i = 0; i < v.pages.Count; i++)
                    if (!v.pages[i].spent) return i;
            return v.pages.Count > 0 ? 0 : -1;
        }

        private void Select(int page)
        {
            selectedPage = page;
            eraseUntil = 0f;
            message.text = "";
            shownSignature = "";
            Refresh(true);
        }

        // ------------------------------------------------------------------ building
        private void Rebuild()
        {
            SocialUI.Clear(booksRow);
            SocialUI.Clear(pagesContent);
            SocialUI.Clear(rightContent);
            GrimoireView v = soul.GrimoireView;

            foreach (GrimoireItemSO g in held)
            {
                GrimoireItemSO captured = g;
                bool shown = g.RuntimeId == v.itemId;
                Button b = Choice(booksRow, $"<b>{g.Name}</b>", shown, !shown, () => soul.RequestOpenGrimoire(captured.RuntimeId), 34f);
                b.name = "Book " + g.Serial;
            }

            if (v.itemId.Length == 0)
            {
                Para(pagesContent, "Nothing is open yet.", InventoryUIFactory.MutedColor, 15f);
                return;
            }

            for (int i = 0; i < v.pages.Count; i++)
            {
                GrimoirePageView p = v.pages[i];
                int index = i;
                string line = p.filled
                    ? $"<b>{i + 1}.</b> {p.name}  <size=80%><color=#9aa4b5>rank {p.rank} · {p.qualityName}{(p.composite ? " · composite" : "")}{(string.IsNullOrEmpty(p.note) ? "" : " · " + p.note)}</color></size>"
                    : p.spent ? $"<b>{i + 1}.</b> <color=#8a8f99>spent page</color>" : $"<b>{i + 1}.</b> <color=#8a8f99>blank</color>";
                Button b = Choice(pagesContent, line, i == selectedPage, true, () => Select(index), 38f);
                b.name = "Page " + (i + 1);
            }

            GrimoirePageView current = v.Page(selectedPage);
            if (current == null)
            {
                Para(rightContent, "Choose a page.", InventoryUIFactory.MutedColor, 15f);
                return;
            }
            if (current.filled) BuildPrint(v, current);
            else BuildBlank(v, current);
        }

        private void BuildPrint(GrimoireView v, GrimoirePageView p)
        {
            GrimoireBalance g = Catalog.Grimoire;
            var sb = new StringBuilder();
            sb.Append($"<size=130%><b>{p.name}</b></size>\n");
            sb.Append($"Rank {p.rank} · Quality {p.quality:0}{(string.IsNullOrEmpty(p.qualityName) ? "" : " (" + p.qualityName + ")")} · {p.slots} soul slot(s)\n");
            if (!string.IsNullOrEmpty(p.by)) sb.Append($"<color=#9aa4b5>Imprinted by</color> {p.by}\n");
            sb.Append("\nA print is only what the rune is: absorbed, it becomes a rune of your soul <b>with no energy</b>; you must give it energy yourself. " +
                      (g != null && g.pageFreeAfterAbsorb ? "The page is blank again." : "The page is used up.") + " A print can be absorbed once.");
            Para(rightContent, sb.ToString(), InventoryUIFactory.TextColor, 15f);

            bool fits = soul.CanFit(p.slots);
            int index = p.index;
            Button absorb = InventoryUIFactory.Button("Absorb", rightContent, fits ? "Absorb into my soul" : $"No room (needs {p.slots} slot{(p.slots == 1 ? "" : "s")}, {soul.FreeSlots} free)",
                () => { message.text = ""; soul.RequestAbsorbPrint(v.itemId, index); shownSignature = ""; });
            absorb.interactable = fits;
            InventoryUIFactory.Size(absorb, -1f, 38f, 1f);

            if (v.canErase)
            {
                Button erase = InventoryUIFactory.Button("Erase", rightContent, eraseUntil > 0f ? "Really erase? (nothing is given back)" : "Erase this print", () =>
                {
                    if (eraseUntil <= 0f)
                    {
                        eraseUntil = Time.unscaledTime + 4f;
                        message.text = "Erasing destroys the print: the rune you imprinted does not get its energy back. Click again to confirm.";
                        shownSignature = "";
                        return;
                    }
                    eraseUntil = 0f;
                    message.text = "";
                    soul.RequestErasePrint(v.itemId, index);
                    shownSignature = "";
                });
                InventoryUIFactory.Size(erase, -1f, 34f, 1f);
            }
        }

        private void BuildBlank(GrimoireView v, GrimoirePageView p)
        {
            if (p.spent)
            {
                Para(rightContent, "This page is spent: nothing can be written on it again.", InventoryUIFactory.MutedColor, 15f);
                return;
            }
            if (!v.canWrite)
            {
                Para(rightContent, "This page is blank. Only the author writes in this grimoire: you can read it and absorb the prints on its pages.", InventoryUIFactory.MutedColor, 15f);
                return;
            }
            GrimoireBalance g = Catalog.Grimoire;
            Para(rightContent, "<b>Imprint a rune on this page.</b>\nA copy of the rune is written; the rune stays in your soul and <b>loses the energy shown</b>. " +
                               "A rune that does not hold that much energy cannot be imprinted: give it energy first.", InventoryUIFactory.TextColor, 15f);
            IReadOnlyList<RuneInstance> runes = soul.Runes;
            if (runes.Count == 0)
            {
                Para(rightContent, "You hold no runes in your soul.", InventoryUIFactory.MutedColor, 15f);
                return;
            }
            RuneCatalog c = Catalog;
            for (int i = 0; i < runes.Count; i++)
            {
                RuneInstance r = runes[i];
                if (r == null) continue;
                float cost = RuneSoul.ImprintCostOf(r);
                float max = c.Balance.MaxEnergy(r.rank, r.quality);
                bool allowed = !(r.IsComposite && g != null && !g.allowComposite);
                bool enough = r.energy + 1e-3f >= cost;
                string state = !allowed ? "<color=#ff7a6b>cannot be imprinted</color>"
                    : enough ? $"<color=#7bd88f>cost {cost:N0}</color>"
                    : $"<color=#ff7a6b>cost {cost:N0}: needs {cost - r.energy:N0} more</color>";
                string line = $"<b>{RuneRules.DisplayName(c, r)}</b>  <size=80%><color=#9aa4b5>rank {r.rank} · energy {r.energy:N0} / {max:N0}</color></size>  {state}";
                string runeId = r.instanceId;
                int page = p.index;
                Button b = Choice(rightContent, line, false, allowed && enough, () =>
                {
                    message.text = "";
                    soul.RequestImprint(v.itemId, page, runeId);
                    shownSignature = "";
                }, 40f);
                b.name = "Imprint " + r.runeId;
            }
        }

        // ------------------------------------------------------------------ widgets
        private static void Para(RectTransform parent, string text, Color color, float size = 16f, FontStyles style = FontStyles.Normal)
        {
            TextMeshProUGUI t = InventoryUIFactory.Text("Text", parent, text, size, color, TextAlignmentOptions.Left, style);
            t.richText = true;
        }

        private static Button Choice(RectTransform parent, string label, bool selected, bool enabled, Action onClick, float height)
        {
            Button b = InventoryUIFactory.Button("Choice", parent, "", () => onClick());
            TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
            t.richText = true;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = new Vector4(10f, 0f, 6f, 0f);
            t.text = label;
            InventoryUIFactory.Size(b, -1f, height, 1f);
            b.interactable = enabled;
            if (selected) b.GetComponent<Image>().color = new Color(0.35f, 0.6f, 1f, 0.35f);
            return b;
        }

        private void Build()
        {
            canvas = UITheme.Canvas("Grimoire Window", 74, null);
            if (!canvas.TryGetComponent(out CanvasScaler scaler)) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!canvas.TryGetComponent(out GraphicRaycaster _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
            InventoryEventSystems.EnsureSingle();
            DontDestroyOnLoad(canvas.gameObject);

            Image panel = InventoryUIFactory.Panel("Grimoire", canvas.transform, InventoryUIFactory.PanelColor);
            SocialUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 720f));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(panel.gameObject, 18, 8f, false);

            TextMeshProUGUI title = InventoryUIFactory.Text("Title", panel.transform, "Grimoire", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(title, -1f, 36f, 1f);
            header = InventoryUIFactory.Text("Header", panel.transform, "", 16f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
            header.richText = true;
            InventoryUIFactory.Size(header, -1f, 28f, 1f);

            booksRow = InventoryUIFactory.Rect("Books", panel.transform);
            InventoryUIFactory.Horizontal(booksRow.gameObject, 8f);
            InventoryUIFactory.Size(booksRow, -1f, 38f, 1f);

            RectTransform columns = InventoryUIFactory.Rect("Columns", panel.transform);
            InventoryUIFactory.Horizontal(columns.gameObject, 14f);
            LayoutElement cle = InventoryUIFactory.Size(columns, -1f, 380f, 1f);
            cle.flexibleHeight = 1f;
            pagesContent = Column(columns, "Pages", 1f);
            rightContent = Column(columns, "Page", 1.3f);

            message = InventoryUIFactory.Text("Message", panel.transform, "", 15f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center);
            InventoryUIFactory.Size(message, -1f, 40f, 1f);
            RectTransform buttons = InventoryUIFactory.Rect("Buttons", panel.transform);
            InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
            InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
            InventoryUIFactory.Button("Runes", buttons, "Rune Soul", () => { Close(); RuneSoulWindow.Toggle(); });
            InventoryUIFactory.Button("Close", buttons, "Close", Close);
            SocialUI.Show(group, false);
            canvas.gameObject.SetActive(false);
        }

        /// <summary>A titled, scrolling column; returns its content.</summary>
        private static RectTransform Column(Transform parent, string title, float weight)
        {
            Image bg = InventoryUIFactory.Panel(title, parent, new Color(1f, 1f, 1f, 0.04f));
            InventoryUIFactory.Vertical(bg.gameObject, 10, 6f, false);
            LayoutElement le = InventoryUIFactory.Size(bg, -1f, -1f, weight);
            le.flexibleHeight = 1f;
            TextMeshProUGUI head = InventoryUIFactory.Text("Header", bg.transform, title, 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(head, -1f, 26f, 1f);

            RectTransform viewport = InventoryUIFactory.Rect("List", bg.transform);
            LayoutElement vle = InventoryUIFactory.Size(viewport, -1f, 80f, 1f);
            vle.flexibleHeight = 1f;
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.12f);
            RectTransform scrolled = InventoryUIFactory.Rect("Content", viewport);
            scrolled.anchorMin = new Vector2(0f, 1f);
            scrolled.anchorMax = new Vector2(1f, 1f);
            scrolled.pivot = new Vector2(0.5f, 1f);
            scrolled.offsetMin = scrolled.offsetMax = Vector2.zero;
            VerticalLayoutGroup v = scrolled.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.padding = new RectOffset(4, 4, 4, 4);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = scrolled.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = scrolled;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return scrolled;
        }
    }
}
