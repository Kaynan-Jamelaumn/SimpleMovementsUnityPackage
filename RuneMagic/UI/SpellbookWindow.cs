using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RuneMagic
{
    /// <summary>
    /// The Spellbook (design spec 16.4): the spells a rune user has synthesized, and which of the player's ability keys each one is
    /// on. Spells are put on the <b>same ability keys</b> as every other ability (there is no second set of slots): pick a spell, pick
    /// a key, and the spell is cast with that key's input (the Input System actions named Ability1, Ability2... in your Input Actions
    /// asset) and shown on the ability bar, with its cooldown and its costs in the tooltip. Choosing a key that holds another spell
    /// swaps them; "Take off key" returns a spell to the book; "Dismiss" frees the runes that anchor it.
    /// <para>The window only <b>asks</b>: assigning, unassigning and dismissing are requests the server checks again
    /// (<see cref="RuneSoul.RequestAssignSpell"/> and friends).</para>
    /// Opened from the Rune Soul window's Spellbook button, or by the input action named in Settings ▸ Spellbook Action ("Spellbook")
    /// of the Input Actions asset (no key is fixed in code). Only rune users can open it. Created automatically.
    /// </summary>
    public class SpellbookWindow : MonoBehaviour, IGameplayPanel
    {
        private static SpellbookWindow instance;

        private GlobalInputBinding toggle;
        private Canvas canvas;
        private CanvasGroup group;
        private SocialPanelHandle handle;
        private bool open;

        private TextMeshProUGUI header, message;
        private RectTransform listContent, rightContent;
        private Button removeButton, dismissButton;
        private TextMeshProUGUI dismissLabel;

        private RuneSoul soul;
        private string selected = "";
        private int shownRunes = -1;
        private string shownSignature = "";
        private int seenMessage;
        private float nextPoll;
        private float dismissUntil;
        private readonly List<SpellSlotInfo> keys = new List<SpellSlotInfo>();
        private readonly StringBuilder signature = new StringBuilder();

        public string PanelName => "Spellbook";

        /// <summary>Is the window open?</summary>
        public static bool IsOpen => instance != null && instance.open;

        /// <summary>Opens or closes the window for the local player.</summary>
        public static void Toggle()
        {
            if (instance == null) return;
            if (instance.open) instance.Close();
            else instance.Open();
        }

        /// <summary>Opens the window for the local player (the Rune Soul window's button).</summary>
        public static void Show()
        {
            if (instance != null && !instance.open) instance.Open();
        }

        public void ClosePanel() => Close();

        private static RuneCatalog Catalog => RuneMagicDatabase.Instance.Catalog;

        private void OnEnable()
        {
            instance = this;
            if (!Application.isBatchMode) // a headless server has no player to open it
                toggle = RuneMagicInput.Global(RuneMagicInput.SpellbookAction, "Spellbook", this);
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
            if (dismissUntil > 0f && Time.unscaledTime > dismissUntil)
            {
                dismissUntil = 0f;
                UpdateButtons();
            }
            if (Time.unscaledTime < nextPoll)
                return;
            nextPoll = Time.unscaledTime + 0.2f;
            Refresh(false);
        }

        // ------------------------------------------------------------------ open / close
        public void Open()
        {
            RuneSoul s = RuneSoul.Local;
            if (s == null || !s.View.runeUser)
            {
                PlayerActions.Say("Only rune users (Mages) keep a spellbook.");
                return;
            }
            soul = s;
            if (canvas == null) Build();
            RuneSoulWindow.CloseWindow();
            open = true;
            canvas.gameObject.SetActive(true);
            SocialUI.Show(group, true);
            if (handle == null) handle = new SocialPanelHandle(this);
            handle.Open();
            seenMessage = soul.View.messageSeq;
            message.text = "";
            dismissUntil = 0f;
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
            soul.GetSpellSlots(keys);
            signature.Clear();
            foreach (SpellSlotInfo k in keys)
                signature.Append(k.slot).Append(':').Append(k.spellId).Append(':').Append(k.holds).Append(k.locked ? 'L' : '-').Append('|');
            signature.Append(soul.View.conditionId).Append(selected);
            string sig = signature.ToString();

            RuneSoulView v = soul.View;
            if (v.messageSeq != seenMessage)
            {
                seenMessage = v.messageSeq;
                message.text = v.message;
            }
            header.text = $"<b>Soul</b> {v.integrity:0.#} / {v.maxIntegrity:0.#} · {v.conditionName}" +
                          (string.IsNullOrEmpty(v.castingDebuff) ? "" : $" · <color=#ffd34d>{v.castingDebuff}</color>") +
                          (v.collapseLocked ? " <color=#ff7a6b>(collapsed: you cannot cast)</color>" : "");

            if (!force && soul.RunesVersion == shownRunes && sig == shownSignature)
                return;
            shownRunes = soul.RunesVersion;
            shownSignature = sig;
            if (SpellRulesFind(selected) == null)
                selected = soul.Spells.Count > 0 ? soul.Spells[0].spellId : "";
            Rebuild();
        }

        private SpellRecord SpellRulesFind(string id) => string.IsNullOrEmpty(id) ? null : soul.FindSpell(id);

        private void Select(string spellId)
        {
            selected = spellId;
            dismissUntil = 0f;
            message.text = "";
            shownSignature = "";
            Refresh(true);
        }

        // ------------------------------------------------------------------ building
        private void Rebuild()
        {
            SocialUI.Clear(listContent);
            SocialUI.Clear(rightContent);

            IReadOnlyList<SpellRecord> spells = soul.Spells;
            if (spells.Count == 0)
            {
                Para(listContent, "Your spellbook is empty.\n\nSynthesize a spell: the Rune Synthesis window (the Synthesis button of the Rune Soul window) turns runes into spells. " +
                                  "A spell you make appears here, ready to be put on an ability key.", InventoryUIFactory.MutedColor, 15f);
                InventoryUIFactory.Button("Synthesis", listContent, "Open Rune Synthesis", () => { Close(); SynthesisWindow.Show(); });
            }
            foreach (SpellRecord sp in spells)
            {
                string id = sp.spellId;
                int slot = soul.SpellSlotOf(id);
                CompiledSpell cs = soul.CompiledSpellOf(id);
                SpellFormDef form = cs != null ? Catalog.SpellForm(cs.formId) : null;
                string key = slot >= 0 ? KeyName(slot) : "";
                string line = $"<b>{sp.name}</b>  <size=80%><color=#9aa4b5>rank {sp.rank}{(form != null ? " · " + form.Name : "")}</color></size>" +
                              (slot >= 0 ? $"  <color=#7bd88f>{key}</color>" : "") + (sp.flawed ? "  <color=#ffd34d>flawed</color>" : "");
                Button b = Choice(listContent, line, id == selected, true, () => Select(id), 44f);
                b.name = "Spell " + sp.name;
            }

            SpellRecord current = SpellRulesFind(selected);
            if (current != null) Para(rightContent, Detail(current), InventoryUIFactory.TextColor, 15f);
            else if (spells.Count > 0) Para(rightContent, "Choose a spell.", InventoryUIFactory.MutedColor, 15f);
            BuildKeys(current);
            UpdateButtons();
        }

        private string KeyName(int slot)
        {
            foreach (SpellSlotInfo k in keys)
                if (k.slot == slot) return string.IsNullOrEmpty(k.key) ? $"Slot {slot + 1}" : k.key;
            return $"Slot {slot + 1}";
        }

        private string Detail(SpellRecord sp)
        {
            RuneCatalog c = Catalog;
            IntentionDef intention = c.Intention(sp.intentionId);
            DesiredEffectDef effect = c.Effect(sp.effectId);
            var sb = new StringBuilder();
            sb.Append($"<b>{sp.name}</b>\n");
            sb.Append($"<color=#9aa4b5>{(intention != null ? intention.Name : sp.intentionId)} → {(effect != null ? effect.Name : sp.effectId)}</color>\n");
            sb.Append($"<color=#9aa4b5>Synthesis quality {sp.quality:0.#}% · power {sp.power:0.#}</color>\n\n");
            sb.Append(soul.DescribeSpell(sp.spellId));
            return sb.ToString();
        }

        private void BuildKeys(SpellRecord current)
        {
            if (current == null)
                return;
            Para(rightContent, "Put it on a key:", new Color(1f, 0.9f, 0.6f), 16f, FontStyles.Bold);
            if (keys.Count == 0)
                Para(rightContent, "This character has no ability keys (the player needs Ability State Machine components).", InventoryUIFactory.MutedColor, 14f);
            foreach (SpellSlotInfo k in keys)
            {
                SpellSlotInfo captured = k;
                string name = string.IsNullOrEmpty(k.key) ? $"Slot {k.slot + 1}" : k.key;
                SpellRecord on = string.IsNullOrEmpty(k.spellId) ? null : soul.FindSpell(k.spellId);
                string what = !string.IsNullOrEmpty(k.spellId) ? (on != null ? on.name : "a spell")
                    : !string.IsNullOrEmpty(k.holds) ? $"<color=#9aa4b5>{k.holds}{(k.locked ? " (an ability: cannot be replaced)" : "")}</color>"
                    : "<color=#9aa4b5>empty</color>";
                bool mine = k.spellId == current.spellId;
                Choice(rightContent, $"<b>[{name}]</b>  {what}", mine, !k.locked && !mine, () => Assign(current.spellId, captured.slot), 36f);
            }
        }

        private void Assign(string spellId, int slot)
        {
            message.text = "";
            soul.RequestAssignSpell(spellId, slot);
            shownSignature = "";
        }

        private void UpdateButtons()
        {
            int slot = soul.SpellSlotOf(selected);
            removeButton.gameObject.SetActive(!string.IsNullOrEmpty(selected));
            removeButton.interactable = slot >= 0;
            dismissButton.gameObject.SetActive(!string.IsNullOrEmpty(selected));
            dismissLabel.text = dismissUntil > 0f ? "Really dismiss?" : "Dismiss spell";
        }

        private void RemoveFromKey()
        {
            int slot = soul.SpellSlotOf(selected);
            if (slot < 0) return;
            message.text = "";
            soul.RequestUnassignSpell(slot);
            shownSignature = "";
        }

        private void Dismiss()
        {
            if (string.IsNullOrEmpty(selected)) return;
            if (dismissUntil <= 0f)
            {
                dismissUntil = Time.unscaledTime + 4f;
                message.text = "Dismissing frees the runes that anchor this spell (they keep their energy). The spell is lost. Click again to confirm.";
                UpdateButtons();
                return;
            }
            dismissUntil = 0f;
            message.text = "";
            soul.RequestDismissSpell(selected);
            shownSignature = "";
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
            canvas = UITheme.Canvas("Spellbook Window", 73, null);
            if (!canvas.TryGetComponent(out CanvasScaler scaler)) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!canvas.TryGetComponent(out GraphicRaycaster _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
            InventoryEventSystems.EnsureSingle();
            DontDestroyOnLoad(canvas.gameObject);

            Image panel = InventoryUIFactory.Panel("Spellbook", canvas.transform, InventoryUIFactory.PanelColor);
            SocialUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 700f));
            group = panel.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(panel.gameObject, 18, 8f, false);

            TextMeshProUGUI title = InventoryUIFactory.Text("Title", panel.transform, "Spellbook", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(title, -1f, 36f, 1f);
            header = InventoryUIFactory.Text("Header", panel.transform, "", 17f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
            header.richText = true;
            InventoryUIFactory.Size(header, -1f, 26f, 1f);

            RectTransform columns = InventoryUIFactory.Rect("Columns", panel.transform);
            InventoryUIFactory.Horizontal(columns.gameObject, 14f);
            LayoutElement cle = InventoryUIFactory.Size(columns, -1f, 380f, 1f);
            cle.flexibleHeight = 1f;
            listContent = Column(columns, "Spells", 1f);
            rightContent = Column(columns, "Selected spell", 1.3f);

            message = InventoryUIFactory.Text("Message", panel.transform, "", 15f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center);
            InventoryUIFactory.Size(message, -1f, 40f, 1f);
            RectTransform buttons = InventoryUIFactory.Rect("Buttons", panel.transform);
            InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
            InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
            removeButton = InventoryUIFactory.Button("Remove", buttons, "Take off key", RemoveFromKey);
            dismissButton = InventoryUIFactory.Button("Dismiss", buttons, "Dismiss spell", Dismiss);
            dismissLabel = dismissButton.GetComponentInChildren<TextMeshProUGUI>();
            InventoryUIFactory.Button("Synthesis", buttons, "Synthesis", () => { Close(); SynthesisWindow.Show(); });
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
