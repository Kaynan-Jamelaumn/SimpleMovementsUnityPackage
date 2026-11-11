using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// The Rune Soul window (design spec 2, 3.2, 4.2, 5): a rune user's runes and soul.
    /// <list type="bullet">
    /// <item>The soul: Soul Integrity and condition, slots used of the Soul Rune Capacity, magical energy waiting for a rune.</item>
    /// <item>Every absorbed rune: name and rank, quality, energy / maximum, slot cost.</item>
    /// <item>The selected rune: alignment, magical properties, known concepts (unknown ones stay hidden), synthesis
    /// history, origin. Give it pending energy (the player chooses the rune; a full rune takes none), or release it
    /// (confirmed; its energy is lost).</item>
    /// <item>The runes in the bag, ready to absorb when the soul has room.</item>
    /// </list>
    /// Opened by the input action named in Settings ▸ Rune Soul Action ("RuneSoul") of the Input Actions asset (no key is
    /// fixed in code), or <see cref="Toggle"/>. Escape closes it; it frees the cursor while open. Only rune users can open it.
    /// Created automatically.
    /// </summary>
    public class RuneSoulWindow : MonoBehaviour, IGameplayPanel
    {
        private static RuneSoulWindow instance;

        private GlobalInputBinding toggle;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform runeList, bagList;
        private TextMeshProUGUI header, details, message, energyLabel, bagHint;
        private Image energyFill;
        private Slider amount;
        private Button giveButton, giveAllButton, releaseButton;
        private TextMeshProUGUI releaseLabel;
        private SocialPanelHandle handle;
        private bool open;

        private RuneSoul soul;
        private string selected;
        private int shownVersion = -1, shownRunes = -1;
        private string shownBag = "";
        private float nextBagLook;
        private float confirmUntil;
        private int seenMessage = -1;
        private readonly List<InventoryItem> bagScratch = new List<InventoryItem>();

        public string PanelName => "Rune Soul";

        /// <summary>Is the window open?</summary>
        public static bool IsOpen => instance != null && instance.open;

        /// <summary>Opens or closes the window for the local player.</summary>
        public static void Toggle()
        {
            if (instance == null) return;
            if (instance.open) instance.Close();
            else instance.Open();
        }

        public void ClosePanel() => Close();

        /// <summary>Closes the window if it is open (another window takes its place).</summary>
        public static void CloseWindow()
        {
            if (instance != null && instance.open) instance.Close();
        }

        private void OnEnable()
        {
            instance = this;
            if (!Application.isBatchMode) // a headless server has no player to open it
                toggle = RuneMagicInput.Global(RuneMagicInput.RuneSoulAction, "Rune Soul", this);
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
                return;
            if (soul == null || !soul.View.runeUser)
            {
                Close();
                return;
            }
            Refresh(false);
        }

        // ------------------------------------------------------------------ open / close
        public void Open()
        {
            RuneSoul s = RuneSoul.Local;
            if (s == null || !s.View.runeUser)
            {
                PlayerActions.Say("Only rune users (Mages) have a Rune Soul.");
                return;
            }
            soul = s;
            if (canvas == null) Build();
            open = true;
            canvas.gameObject.SetActive(true);
            SocialUI.Show(group, true);
            if (handle == null) handle = new SocialPanelHandle(this);
            handle.Open();
            seenMessage = soul.View.messageSeq;
            message.text = "";
            Refresh(true);
        }

        public void Close()
        {
            open = false;
            if (group != null) SocialUI.Show(group, false);
            if (canvas != null) canvas.gameObject.SetActive(false);
            handle?.Close();
        }

        // ------------------------------------------------------------------ content
        private void Refresh(bool force)
        {
            RuneSoulView v = soul.View;
            RuneCatalog c = RuneMagicDatabase.Instance.Catalog;
            if (v.messageSeq != seenMessage)
            {
                seenMessage = v.messageSeq;
                message.text = v.message;
            }
            if (Time.unscaledTime >= nextBagLook || force)
            {
                nextBagLook = Time.unscaledTime + 0.5f;
                string bag = BagKey();
                if (bag != shownBag || force)
                {
                    shownBag = bag;
                    FillBag(c, v);
                }
            }
            if (!force && soul.Version == shownVersion && soul.RunesVersion == shownRunes && confirmUntil <= 0f)
                return;
            if (confirmUntil > 0f && Time.unscaledTime > confirmUntil)
            {
                confirmUntil = 0f;
                releaseLabel.text = "Release";
            }
            bool runesChanged = force || soul.RunesVersion != shownRunes;
            shownVersion = soul.Version;
            shownRunes = soul.RunesVersion;

            header.text = $"<b>Soul</b> {v.integrity:0.#} / {v.maxIntegrity:0.#} · {v.conditionName}" +
                          (v.collapseLocked ? " <color=#ff7a6b>(collapsed)</color>" : "") +
                          $"     <b>Slots</b> {v.usedSlots} / {v.capacity}" +
                          $"     <b>Magical energy</b> <color=#ffd34d>{v.pendingEnergy:N0}</color> waiting";
            if (runesChanged)
                FillRunes(c);
            FillDetails(c, v);
        }

        private void FillRunes(RuneCatalog c)
        {
            SocialUI.Clear(runeList);
            IReadOnlyList<RuneInstance> runes = soul.Runes;
            if (runes.Count == 0)
            {
                Note(runeList, "No runes absorbed yet. Defeat monsters to find runes, then absorb them below.");
                selected = null;
                return;
            }
            if (selected == null || soul.FindRune(selected) == null)
                selected = runes[0].instanceId;
            foreach (RuneInstance r in runes)
            {
                if (r == null) continue;
                RuneInstance captured = r;
                float max = c.Balance.MaxEnergy(r.rank, r.quality);
                QualityTier tier = c.Balance.QualityTierOf(r.quality);
                int slots = RuneRules.SlotCost(c, r);
                string label = $"{RuneRules.DisplayName(c, r)}  <size=80%><color=#9aa4b5>R{r.rank} · Q{r.quality:0} {tier?.name} · " +
                               $"{r.energy:N0}/{max:N0} · {slots} slot{(slots == 1 ? "" : "s")}</color></size>";
                Button b = InventoryUIFactory.Button("Rune", runeList, "", () =>
                {
                    selected = captured.instanceId;
                    confirmUntil = 0f;
                    releaseLabel.text = "Release";
                    FillRunes(RuneMagicDatabase.Instance.Catalog);
                    FillDetails(RuneMagicDatabase.Instance.Catalog, soul.View);
                });
                TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
                t.richText = true;
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.text = label;
                t.margin = new Vector4(10f, 0f, 6f, 0f);
                InventoryUIFactory.Size(b, -1f, 40f, 1f);
                if (r.instanceId == selected)
                    b.GetComponent<Image>().color = new Color(0.35f, 0.6f, 1f, 0.35f);
            }
        }

        private void FillDetails(RuneCatalog c, RuneSoulView v)
        {
            RuneInstance r = selected != null ? soul.FindRune(selected) : null;
            bool has = r != null;
            giveButton.interactable = giveAllButton.interactable = releaseButton.interactable = has;
            amount.interactable = has;
            if (!has)
            {
                details.text = "<color=#9aa4b5>Select a rune.</color>";
                energyLabel.text = "";
                energyFill.fillAmount = 0f;
                return;
            }
            RuneDef def = c.Rune(r.runeId);
            float max = c.Balance.MaxEnergy(r.rank, r.quality);
            QualityTier tier = c.Balance.QualityTierOf(r.quality);
            var sb = new StringBuilder();
            sb.Append($"<size=130%><b>{RuneRules.DisplayName(c, r)}</b></size>\n");
            sb.Append($"Rank {r.rank} · Quality {r.quality:0}{(tier != null ? $" ({tier.name})" : "")} · {RuneRules.SlotCost(c, r)} soul slot(s)" +
                      (def != null ? $" · {def.rarity}" : r.IsComposite ? " · Composite" : "") + "\n");
            if (def != null)
            {
                var al = new List<string>();
                foreach (string id in def.alignments)
                {
                    AlignmentDef a = c.Alignment(id);
                    al.Add(a != null ? (string.IsNullOrEmpty(a.domain) ? a.Name : $"{a.Name} — {a.domain}") : id);
                }
                if (al.Count > 0) sb.Append("<color=#9aa4b5>Alignment:</color> ").Append(string.Join("; ", al)).Append('\n');
                if (!string.IsNullOrEmpty(def.magicalProperties)) sb.Append("<color=#9aa4b5>Properties:</color> ").Append(def.magicalProperties).Append('\n');
                IReadOnlyList<string> known = soul.KnownConcepts(def.id);
                int total = def.concepts.Count, knownCount = 0;
                var lines = new StringBuilder();
                foreach (ConceptDef k in def.concepts)
                {
                    if (k == null) continue;
                    if (Contains(known, k.id))
                    {
                        knownCount++;
                        lines.Append($"  • <b>{k.Name}</b> — {k.description}\n");
                    }
                }
                sb.Append($"<color=#9aa4b5>Concepts:</color> {knownCount} known, {total - knownCount} unknown\n");
                sb.Append(lines);
                if (total - knownCount > 0) sb.Append($"  • <color=#8a8f99>??? ×{total - knownCount} (discover them in dungeons)</color>\n");
            }
            else if (r.IsComposite) AppendComposite(sb, c, r);
            else sb.Append("<color=#ff7a6b>This rune is not in the Rune Magic Database.</color>\n");
            sb.Append($"<color=#9aa4b5>Synthesis history:</color> {(r.history.Count == 0 ? "none yet" : r.history.Count + " synthesis(es)")}\n");
            var anchors = new List<string>();
            foreach (SpellRecord sp in soul.Spells)
                if (sp != null && sp.anchors.Contains(r.instanceId)) anchors.Add(sp.name);
            if (anchors.Count > 0) sb.Append($"<color=#9aa4b5>Anchors:</color> {string.Join(", ", anchors)} <color=#8a8f99>(dismiss the spell to use or release this rune)</color>\n");
            if (r.flawed) sb.Append($"<color=#ffb36b>Flawed:</color> {FlawNames(c, r.flaws)}\n");
            string from = string.IsNullOrEmpty(r.origin?.source) ? r.origin?.kind : $"{r.origin.kind}: {r.origin.source}";
            if (!string.IsNullOrEmpty(from)) sb.Append($"<color=#9aa4b5>Origin:</color> {from}\n");
            details.text = sb.ToString();

            bool full = r.energy >= max - 0.001f;
            energyFill.fillAmount = max > 0f ? Mathf.Clamp01(r.energy / max) : 0f;
            energyLabel.text = $"Energy {r.energy:N0} / {max:N0}" + (full ? "  <color=#ffd34d>FULL</color>" : "");
            float space = Mathf.Max(0f, max - r.energy);
            float canGive = Mathf.Min(space, v.pendingEnergy);
            amount.minValue = 0f;
            amount.maxValue = Mathf.Max(1f, Mathf.Floor(canGive));
            if (amount.value > amount.maxValue) amount.value = amount.maxValue;
            bool canAssign = canGive >= 1f;
            giveButton.interactable = giveAllButton.interactable = amount.interactable = canAssign;
        }

        /// <summary>What a composite rune is (made by synthesis, not in the database): its purpose, essence and ingredients.</summary>
        private static void AppendComposite(StringBuilder sb, RuneCatalog c, RuneInstance r)
        {
            CompositeProfile p = r.composite;
            if (!string.IsNullOrEmpty(p.description)) sb.Append(p.description).Append('\n');
            var al = new List<string>();
            foreach (string id in p.alignments)
            {
                AlignmentDef a = c.Alignment(id);
                al.Add(a != null ? a.Name : id);
            }
            if (al.Count > 0) sb.Append("<color=#9aa4b5>Alignment:</color> ").Append(string.Join("; ", al)).Append('\n');
            IntentionDef intention = c.Intention(p.intentionId);
            DesiredEffectDef effect = c.Effect(p.effectId);
            if (effect != null) sb.Append($"<color=#9aa4b5>Made for:</color> {(intention != null ? intention.Name + " — " : "")}{effect.Name}\n");
            if (p.aspects.Count > 0)
            {
                var essence = new List<string>();
                foreach (AspectWeight w in p.aspects)
                {
                    AspectDef a = c.Aspect(w.aspect);
                    essence.Add($"{(a != null ? a.Name : w.aspect)} {w.weight:0.##}");
                }
                sb.Append("<color=#9aa4b5>Essence:</color> ").Append(string.Join(", ", essence)).Append('\n');
            }
            var counts = new List<KeyValuePair<string, int>>();
            foreach (SynthesisIngredient ing in p.ingredients)
            {
                string name = RuneRules.DisplayName(c, ing.runeId, ing.rank);
                int at = counts.FindIndex(kv => kv.Key == name);
                if (at < 0) counts.Add(new KeyValuePair<string, int>(name, 1));
                else counts[at] = new KeyValuePair<string, int>(name, counts[at].Value + 1);
            }
            if (counts.Count > 0)
            {
                var parts = new List<string>();
                foreach (KeyValuePair<string, int> kv in counts) parts.Add(kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key);
                sb.Append("<color=#9aa4b5>Made from:</color> ").Append(string.Join(", ", parts)).Append('\n');
            }
            sb.Append($"<color=#9aa4b5>Synthesis score:</color> {p.score:0.#}%\n");
        }

        private static string FlawNames(RuneCatalog c, List<string> ids)
        {
            var names = new List<string>();
            foreach (string id in ids)
            {
                SynthesisFlawDef f = c.Flaw(id);
                names.Add(f != null ? f.Name : id);
            }
            return string.Join(", ", names);
        }

        private void FillBag(RuneCatalog c, RuneSoulView v)
        {
            SocialUI.Clear(bagList);
            InventoryManager inv = InventoryManager.For(soul);
            bagScratch.Clear();
            if (inv != null) inv.GetBagItems(bagScratch, true);
            var counts = new Dictionary<RuneItemSO, int>();
            foreach (InventoryItem it in bagScratch)
                if (it != null && it.itemScriptableObject is RuneItemSO ri)
                    counts[ri] = (counts.TryGetValue(ri, out int n) ? n : 0) + Mathf.Max(1, it.stackCurrent);
            string refusal = null;
            foreach (KeyValuePair<RuneItemSO, int> kv in counts)
            {
                RuneItemSO item = kv.Key;
                RectTransform row = InventoryUIFactory.Rect("Bag Rune", bagList);
                InventoryUIFactory.Horizontal(row.gameObject, 8f);
                InventoryUIFactory.Size(row, -1f, 34f, 1f);
                RuneDrop d = item.Drop;
                float max = c.Balance.MaxEnergy(d.rank, d.quality);
                TextMeshProUGUI t = InventoryUIFactory.Text("Name", row, $"{item.Name} ×{kv.Value}  <size=80%><color=#9aa4b5>Q{d.quality:0} · " +
                    $"{Mathf.Min(d.energy, max):N0}/{max:N0} · {RuneRules.SlotCost(c, d.runeId)} slot(s)</color></size>", 16f, InventoryUIFactory.TextColor,
                    TextAlignmentOptions.MidlineLeft);
                t.richText = true;
                InventoryUIFactory.Size(t, -1f, 34f, 1f);
                bool fits = soul.CanAbsorb(d, out string why);
                Button absorb = InventoryUIFactory.Button("Absorb", row, fits ? "Absorb" : "No room", () => soul.RequestAbsorb(item), 120f);
                absorb.interactable = fits;
                if (!fits && refusal == null) refusal = why;
            }
            bagHint.text = counts.Count == 0 ? "No runes in your bag."
                : refusal ?? $"{v.FreeSlots} free slot(s): absorbing binds the rune to your soul.";
        }

        private string BagKey()
        {
            InventoryManager inv = InventoryManager.For(soul);
            if (inv == null) return "";
            bagScratch.Clear();
            inv.GetBagItems(bagScratch, true);
            var sb = new StringBuilder();
            foreach (InventoryItem it in bagScratch)
                if (it != null && it.itemScriptableObject is RuneItemSO ri)
                    sb.Append(ItemDatabase.Id(ri)).Append('x').Append(it.stackCurrent).Append(';');
            sb.Append('|').Append(soul.View.usedSlots).Append('/').Append(soul.View.capacity);
            return sb.ToString();
        }

        private static bool Contains(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == id) return true;
            return false;
        }

        // ------------------------------------------------------------------ actions
        private void Give(bool all)
        {
            if (selected == null) return;
            soul.RequestAssignEnergy(selected, all ? 0f : Mathf.Max(1f, amount.value));
        }

        private void Release()
        {
            if (selected == null) return;
            if (confirmUntil <= 0f)
            {
                RuneInstance r = soul.FindRune(selected);
                confirmUntil = Time.unscaledTime + 4f;
                releaseLabel.text = r != null && r.energy > 0f ? $"Confirm: lose {r.energy:N0} energy?" : "Confirm release?";
                return;
            }
            confirmUntil = 0f;
            releaseLabel.text = "Release";
            soul.RequestRelease(selected);
        }

        // ------------------------------------------------------------------ building
        private static void Note(RectTransform list, string text)
        {
            TextMeshProUGUI t = InventoryUIFactory.Text("Note", list, text, 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Italic);
            InventoryUIFactory.Size(t, -1f, 48f, 1f);
        }

        private void Build()
        {
            canvas = UITheme.Canvas("Rune Soul Window", 72, null);
            if (!canvas.TryGetComponent(out CanvasScaler scaler)) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!canvas.TryGetComponent(out GraphicRaycaster _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
            InventoryEventSystems.EnsureSingle();
            DontDestroyOnLoad(canvas.gameObject);

            Image panel = InventoryUIFactory.Panel("Rune Soul", canvas.transform, InventoryUIFactory.PanelColor);
            SocialUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 720f));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(panel.gameObject, 18, 8f, false);

            TextMeshProUGUI title = InventoryUIFactory.Text("Title", panel.transform, "Rune Soul", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(title, -1f, 36f, 1f);
            header = InventoryUIFactory.Text("Header", panel.transform, "", 18f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
            header.richText = true;
            InventoryUIFactory.Size(header, -1f, 28f, 1f);

            RectTransform columns = InventoryUIFactory.Rect("Columns", panel.transform);
            InventoryUIFactory.Horizontal(columns.gameObject, 14f);
            LayoutElement cle = InventoryUIFactory.Size(columns, -1f, 380f, 1f);
            cle.flexibleHeight = 1f;
            runeList = Column(columns, "Runes in your soul", 1f, out _);

            // Details column: text, energy bar and controls, release.
            RectTransform detail = Column(columns, "Rune", 1.1f, out RectTransform detailColumn);
            details = InventoryUIFactory.Text("Details", detail, "", 16f, InventoryUIFactory.TextColor);
            details.richText = true;
            details.raycastTarget = false;
            var dfit = details.gameObject.AddComponent<ContentSizeFitter>();
            dfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image barBack = InventoryUIFactory.Panel("Energy", detailColumn, InventoryUIFactory.FieldColor);
            InventoryUIFactory.Size(barBack, -1f, 24f, 1f);
            energyFill = InventoryUIFactory.Panel("Fill", barBack.transform, new Color(1f, 0.83f, 0.3f, 0.85f));
            energyFill.sprite = SocialUI.WhiteSprite;
            energyFill.type = Image.Type.Filled;
            energyFill.fillMethod = Image.FillMethod.Horizontal;
            InventoryUIFactory.Stretch(energyFill.rectTransform);
            energyLabel = InventoryUIFactory.Text("Label", barBack.transform, "", 15f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            energyLabel.richText = true;
            InventoryUIFactory.Stretch(energyLabel.rectTransform);

            RectTransform give = InventoryUIFactory.Rect("Give", detailColumn);
            InventoryUIFactory.Horizontal(give.gameObject, 8f);
            InventoryUIFactory.Size(give, -1f, 32f, 1f);
            amount = InventoryUIFactory.Slider("Amount", give);
            giveButton = InventoryUIFactory.Button("Give", give, "Give", () => Give(false), 90f);
            giveAllButton = InventoryUIFactory.Button("Give All", give, "Give all", () => Give(true), 110f);
            amount.onValueChanged.AddListener(x => giveButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Give {x:N0}");
            releaseButton = InventoryUIFactory.Button("Release", detailColumn, "Release", Release);
            releaseLabel = releaseButton.GetComponentInChildren<TextMeshProUGUI>();

            bagHint = InventoryUIFactory.Text("Bag", panel.transform, "", 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Left, FontStyles.Italic);
            InventoryUIFactory.Size(bagHint, -1f, 22f, 1f);
            RectTransform bagColumns = InventoryUIFactory.Rect("Bag Columns", panel.transform);
            InventoryUIFactory.Horizontal(bagColumns.gameObject, 0f);
            InventoryUIFactory.Size(bagColumns, -1f, 150f, 1f);
            bagList = Column(bagColumns, "Runes in your bag", 1f, out _);

            message = InventoryUIFactory.Text("Message", panel.transform, "", 16f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center);
            InventoryUIFactory.Size(message, -1f, 24f, 1f);
            RectTransform buttons = InventoryUIFactory.Rect("Buttons", panel.transform);
            InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
            InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
            InventoryUIFactory.Button("Synthesis", buttons, "Synthesis", SynthesisWindow.Show);
            InventoryUIFactory.Button("Spellbook", buttons, "Spellbook", SpellbookWindow.Show);
            InventoryUIFactory.Button("Grimoire", buttons, "Grimoire", GrimoireWindow.Show);
            InventoryUIFactory.Button("Close", buttons, "Close", Close);
            SocialUI.Show(group, false);
            canvas.gameObject.SetActive(false);
        }

        /// <summary>A titled, scrolling column; returns its content (and the column itself).</summary>
        private static RectTransform Column(Transform parent, string title, float weight, out RectTransform column)
        {
            Image bg = InventoryUIFactory.Panel(title, parent, new Color(1f, 1f, 1f, 0.04f));
            column = bg.rectTransform;
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
            RectTransform content = InventoryUIFactory.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.padding = new RectOffset(4, 4, 4, 4);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return content;
        }
    }
}
