#if FISHNET
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameNet
{
    /// <summary>
    /// The built-in player trading window (built at runtime by <see cref="NetTrading"/> on clients with a player). It only
    /// shows the server's <see cref="TradeView"/> and sends requests; nothing changes here until the server says so.
    /// <list type="bullet">
    /// <item>A request: "Bram wants to trade" with Accept (or the trade key) and Decline; a request you made can be cancelled.</item>
    /// <item>The trade: your bag and hotbar stacks (click one to offer the whole stack), your offer (click to take it back)
    /// and money, the other player's offer, lock / confirm states, and Lock, Confirm and Cancel buttons.</item>
    /// </list>
    /// While the trade is open it is registered on the player's inventory as an open panel (free cursor, Escape cancels).
    /// </summary>
    public class TradeWindow : MonoBehaviour, IGameplayPanel
    {
        private static readonly Color OfferColor = new Color(0.35f, 0.6f, 1f, 0.22f);
        private static readonly Color LockedColor = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color ConfirmedColor = new Color(0.45f, 0.95f, 0.5f, 1f);

        private NetTrading trading;
        private Canvas canvas;
        private CanvasGroup requestGroup, tradeGroup;
        private TextMeshProUGUI requestText, titleText, statusText, myStateText, theirStateText, theirMoneyText, currencyLabel;
        private Button acceptButton, declineButton, lockButton, confirmButton;
        private TextMeshProUGUI lockLabel;
        private RectTransform inventoryList, myList, theirList;
        private TMP_InputField moneyField;
        private InventoryManager registeredWith;
        private InventoryManager watchedInventory;
        private bool dirty = true;
        private bool pausedForTyping;
        private float requestEndsAt;
        private readonly HashSet<InventoryItem> listed = new HashSet<InventoryItem>(ReferenceComparer<InventoryItem>.Instance);

        /// <summary>Connects the window to the trading relay.</summary>
        public void Bind(NetTrading relay)
        {
            if (trading != null)
            {
                trading.ViewChanged -= OnViewChanged;
                trading.Message -= OnMessage;
            }
            trading = relay;
            if (canvas == null)
                Build();
            if (trading != null)
            {
                trading.ViewChanged += OnViewChanged;
                trading.Message += OnMessage;
                OnViewChanged(trading.View);
            }
        }

        public string PanelName => "Trade";

        /// <summary>Escape, the pause menu or the inventory closing the window: the trade is cancelled.</summary>
        public void ClosePanel()
        {
            Unregister();
            if (trading != null && trading.View.State != TradeState.None)
                trading.Cancel();
        }

        private void OnDestroy()
        {
            if (trading != null)
            {
                trading.ViewChanged -= OnViewChanged;
                trading.Message -= OnMessage;
            }
            Unregister();
            WatchInventory(null);
            ResumeGameplayInput();
        }

        // ------------------------------------------------------------------ state
        private void OnViewChanged(TradeView v)
        {
            if (v.State == TradeState.Proposed)
                requestEndsAt = Time.unscaledTime + Mathf.Max(0f, v.secondsLeft);
            dirty = true;
        }

        private void OnMessage(string text)
        {
            if (statusText != null && trading != null && trading.View.State == TradeState.Open)
                statusText.text = text;
        }

        private void Update()
        {
            if (trading == null)
                return;
            TradeView v = trading.View;
            bool open = v.State == TradeState.Open;
            SetGroup(requestGroup, v.State == TradeState.Proposed);
            SetGroup(tradeGroup, open);
            if (open)
            {
                Register();
                WatchInventory(LocalInventory);
                UpdateTypingPause();
            }
            else
            {
                Unregister();
                WatchInventory(null);
                ResumeGameplayInput();
            }
            if (v.State == TradeState.Proposed)
            {
                int left = Mathf.CeilToInt(Mathf.Max(0f, requestEndsAt - Time.unscaledTime));
                string key = trading.TradeKey.ToString();
                requestText.text = v.incoming
                    ? $"{v.partnerName} wants to trade with you.  ({left}s)"
                    : $"Waiting for {v.partnerName} to answer...  ({left}s)";
                acceptButton.gameObject.SetActive(v.incoming);
                SetLabel(acceptButton, $"Accept  [{key}]");
                SetLabel(declineButton, v.incoming ? "Decline" : "Cancel");
            }
            if (dirty && open)
                Refresh(v);
            dirty = false;
        }

        private static InventoryManager LocalInventory
        {
            get
            {
                CombatEntity me = NetTrading.LocalPlayer;
                return me != null ? InventoryManager.For(me) : null;
            }
        }

        private void WatchInventory(InventoryManager inv)
        {
            if (watchedInventory == inv)
                return;
            if (watchedInventory != null)
                watchedInventory.InventoryChanged -= MarkDirty;
            watchedInventory = inv;
            if (watchedInventory != null)
                watchedInventory.InventoryChanged += MarkDirty;
            dirty = true;
        }

        private void MarkDirty() => dirty = true;

        private void Register()
        {
            if (registeredWith != null)
                return;
            InventoryManager inv = LocalInventory;
            if (inv == null)
                return;
            registeredWith = inv;
            inv.RegisterPanel(this);
        }

        private void Unregister()
        {
            if (registeredWith == null)
                return;
            InventoryManager inv = registeredWith;
            registeredWith = null;
            inv.UnregisterPanel(this);
        }

        // ------------------------------------------------------------------ refresh
        private void Refresh(TradeView v)
        {
            CurrencyDefinition currency = trading.Currency;
            titleText.text = $"Trading with {v.partnerName}";
            currencyLabel.text = currency.DisplayName;
            theirMoneyText.text = currency.Format(v.theirCurrency);
            if (!moneyField.isFocused)
                moneyField.SetTextWithoutNotify(v.myCurrency.ToString());
            myStateText.text = StateText(v.myLocked, v.myConfirmed, "You");
            myStateText.color = v.myConfirmed ? ConfirmedColor : v.myLocked ? LockedColor : InventoryUIFactory.MutedColor;
            theirStateText.text = StateText(v.theirLocked, v.theirConfirmed, v.partnerName);
            theirStateText.color = v.theirConfirmed ? ConfirmedColor : v.theirLocked ? LockedColor : InventoryUIFactory.MutedColor;
            lockLabel.text = v.myLocked ? "Unlock" : "Lock offer";
            confirmButton.interactable = v.myLocked && v.theirLocked && !v.myConfirmed;
            moneyField.interactable = !v.myLocked;

            Clear(myList);
            foreach (TradeItemOffer o in v.myItems)
            {
                TradeItemOffer offer = o;
                Row(myList, o.itemId, o.quantity, v.myLocked ? null : (Action)(() => RemoveOffer(offer)), OfferColor);
            }
            if (v.myItems.Count == 0)
                Note(myList, "Click your items to offer them.");

            Clear(theirList);
            foreach (TradeItemOffer o in v.theirItems)
                Row(theirList, o.itemId, o.quantity, null, OfferColor);
            if (v.theirItems.Count == 0)
                Note(theirList, "Nothing offered yet.");

            Clear(inventoryList);
            InventoryManager inv = LocalInventory;
            listed.Clear();
            if (inv != null && !v.myLocked)
            {
                AddStacks(inv, inv.HotbarSlots, v);
                AddStacks(inv, inv.Slots, v);
            }
            if (inventoryList.childCount == 0)
                Note(inventoryList, v.myLocked ? "Unlock your offer to change it." : "Nothing to offer.");
        }

        private static string StateText(bool locked, bool confirmed, string who) =>
            confirmed ? $"{who}: confirmed" : locked ? $"{who}: locked" : $"{who}: choosing";

        private void AddStacks(InventoryManager inv, GameObject[] slots, TradeView v)
        {
            if (slots == null)
                return;
            foreach (GameObject go in slots)
            {
                InventorySlot slot = go != null ? go.GetComponent<InventorySlot>() : null;
                InventoryItem stack = slot != null && slot.heldItem != null ? slot.heldItem.GetComponent<InventoryItem>() : null;
                if (stack == null || stack.itemScriptableObject == null || !listed.Add(stack))
                    continue;
                InventorySlotRef r = inv.SlotRefOf(slot);
                if (!r.IsValid || IsOffered(v, r))
                    continue;
                string id = ItemDatabase.Id(stack.itemScriptableObject);
                if (string.IsNullOrEmpty(id))
                    continue;
                var offer = new TradeItemOffer(r, id, stack.stackCurrent);
                Row(inventoryList, id, stack.stackCurrent, () => AddOffer(offer), InventoryUIFactory.FieldColor);
            }
        }

        private static bool IsOffered(TradeView v, InventorySlotRef r)
        {
            foreach (TradeItemOffer o in v.myItems)
                if (o.area == r.area && o.index == r.index)
                    return true;
            return false;
        }

        // ------------------------------------------------------------------ actions
        private void AddOffer(TradeItemOffer offer)
        {
            TradeView v = trading.View;
            if (v.State != TradeState.Open || v.myLocked)
                return;
            var items = new List<TradeItemOffer>(v.myItems) { offer };
            trading.SendOffer(items, v.myCurrency);
        }

        private void RemoveOffer(TradeItemOffer offer)
        {
            TradeView v = trading.View;
            if (v.State != TradeState.Open || v.myLocked)
                return;
            var items = new List<TradeItemOffer>(v.myItems);
            items.RemoveAll(o => o.area == offer.area && o.index == offer.index);
            trading.SendOffer(items, v.myCurrency);
        }

        private void OnMoneyEdited(string text)
        {
            TradeView v = trading.View;
            if (v.State != TradeState.Open || v.myLocked)
                return;
            if (!int.TryParse(text, out int amount) || amount < 0)
                amount = 0;
            if (amount != v.myCurrency)
                trading.SendOffer(new List<TradeItemOffer>(v.myItems), amount);
        }

        private void ToggleLock() => trading.SetLocked(!trading.View.myLocked);

        // ------------------------------------------------------------------ typing
        private void UpdateTypingPause()
        {
            bool typing = moneyField != null && moneyField.isFocused;
            if (typing && !pausedForTyping && !InputBindingStore.GameplayInputPaused)
            {
                InputBindingStore.SetGameplayInputEnabled(false);
                pausedForTyping = true;
            }
            else if (!typing)
            {
                ResumeGameplayInput();
            }
        }

        private void ResumeGameplayInput()
        {
            if (!pausedForTyping)
                return;
            pausedForTyping = false;
            if (InputBindingStore.GameplayInputPaused && (PauseMenuManager.Instance == null || !PauseMenuManager.Instance.IsPaused))
                InputBindingStore.SetGameplayInputEnabled(true);
        }

        // ------------------------------------------------------------------ building
        private void Build()
        {
            var root = new GameObject("Trade Canvas", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            Transform t = root.transform;

            // Request prompt.
            Image request = InventoryUIFactory.Panel("Trade Request", t, InventoryUIFactory.PanelColor);
            Place(request.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(600f, 120f));
            requestGroup = request.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(request.gameObject, 14, 10f, false);
            requestText = InventoryUIFactory.Text("Text", request.transform, "", 20f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(requestText, -1f, 34f, 1f);
            RectTransform requestButtons = InventoryUIFactory.Rect("Buttons", request.transform);
            InventoryUIFactory.Horizontal(requestButtons.gameObject, 12f);
            InventoryUIFactory.Size(requestButtons, -1f, 36f, 1f);
            acceptButton = InventoryUIFactory.Button("Accept", requestButtons, "Accept", () => trading?.Respond(true));
            declineButton = InventoryUIFactory.Button("Decline", requestButtons, "Decline", () => trading?.Respond(false));
            SetGroup(requestGroup, false);

            // Trade window.
            Image panel = InventoryUIFactory.Panel("Trade", t, InventoryUIFactory.PanelColor);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 640f));
            tradeGroup = panel.gameObject.AddComponent<CanvasGroup>();
            InventoryUIFactory.Vertical(panel.gameObject, 18, 10f, false);
            titleText = InventoryUIFactory.Text("Title", panel.transform, "Trade", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(titleText, -1f, 36f, 1f);

            RectTransform columns = InventoryUIFactory.Rect("Columns", panel.transform);
            InventoryUIFactory.Horizontal(columns.gameObject, 14f);
            LayoutElement colLayout = InventoryUIFactory.Size(columns, -1f, 460f, 1f);
            colLayout.flexibleHeight = 1f;

            RectTransform invColumn = Column(columns, "Your Items", "Your items");
            inventoryList = ScrollList(invColumn);

            RectTransform myColumn = Column(columns, "Your Offer", "Your offer");
            myList = ScrollList(myColumn);
            RectTransform moneyRow = InventoryUIFactory.Rect("Money", myColumn);
            InventoryUIFactory.Horizontal(moneyRow.gameObject, 8f);
            InventoryUIFactory.Size(moneyRow, -1f, 34f, 1f);
            moneyField = InventoryUIFactory.NumberField("Amount", moneyRow, 160f);
            moneyField.onEndEdit.AddListener(OnMoneyEdited);
            currencyLabel = InventoryUIFactory.Text("Currency", moneyRow, "", 18f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Left);
            InventoryUIFactory.Size(currencyLabel, -1f, 32f, 1f);
            myStateText = InventoryUIFactory.Text("State", myColumn, "", 18f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(myStateText, -1f, 28f, 1f);

            RectTransform theirColumn = Column(columns, "Their Offer", "Their offer");
            theirList = ScrollList(theirColumn);
            theirMoneyText = InventoryUIFactory.Text("Money", theirColumn, "", 18f, InventoryUIFactory.TextColor, TextAlignmentOptions.Center);
            InventoryUIFactory.Size(theirMoneyText, -1f, 34f, 1f);
            theirStateText = InventoryUIFactory.Text("State", theirColumn, "", 18f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(theirStateText, -1f, 28f, 1f);

            statusText = InventoryUIFactory.Text("Status", panel.transform, "", 17f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Italic);
            InventoryUIFactory.Size(statusText, -1f, 28f, 1f);

            RectTransform buttons = InventoryUIFactory.Rect("Buttons", panel.transform);
            InventoryUIFactory.Horizontal(buttons.gameObject, 12f);
            InventoryUIFactory.Size(buttons, -1f, 36f, 1f);
            lockButton = InventoryUIFactory.Button("Lock", buttons, "Lock offer", ToggleLock);
            lockLabel = lockButton.GetComponentInChildren<TextMeshProUGUI>();
            confirmButton = InventoryUIFactory.Button("Confirm", buttons, "Confirm trade", () => trading?.Confirm());
            InventoryUIFactory.Button("Cancel", buttons, "Cancel", () => trading?.Cancel());
            SetGroup(tradeGroup, false);
        }

        private static RectTransform Column(Transform parent, string name, string title)
        {
            Image bg = InventoryUIFactory.Panel(name, parent, new Color(1f, 1f, 1f, 0.04f));
            InventoryUIFactory.Vertical(bg.gameObject, 10, 6f, false);
            LayoutElement le = InventoryUIFactory.Size(bg, -1f, -1f, 1f);
            le.flexibleHeight = 1f;
            TextMeshProUGUI header = InventoryUIFactory.Text("Header", bg.transform, title, 19f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            InventoryUIFactory.Size(header, -1f, 28f, 1f);
            return bg.rectTransform;
        }

        /// <summary>A vertical scrolling list inside <paramref name="parent"/>; returns its content.</summary>
        private static RectTransform ScrollList(Transform parent)
        {
            RectTransform viewport = InventoryUIFactory.Rect("List", parent);
            LayoutElement le = InventoryUIFactory.Size(viewport, -1f, 120f, 1f);
            le.flexibleHeight = 1f;
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

        private static void Row(RectTransform list, string itemId, int quantity, Action onClick, Color color)
        {
            ItemSO item = ItemDatabase.Item(itemId);
            string label = item != null ? (quantity > 1 ? $"{item.Name}  ×{quantity}" : item.Name) : $"Unknown item ×{quantity}";
            Image bg = InventoryUIFactory.Panel("Row", list, color);
            InventoryUIFactory.Size(bg, -1f, 34f, 1f);
            HorizontalLayoutGroup h = InventoryUIFactory.Horizontal(bg.gameObject, 8f);
            h.padding = new RectOffset(6, 6, 2, 2);
            if (item != null && item.Icon != null)
            {
                Image icon = InventoryUIFactory.Panel("Icon", bg.transform, Color.white);
                icon.sprite = item.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                InventoryUIFactory.Size(icon, 28f, 28f, 0f);
            }
            TextMeshProUGUI text = InventoryUIFactory.Text("Name", bg.transform, label, 16f, InventoryUIFactory.TextColor);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            InventoryUIFactory.Size(text, -1f, 30f, 1f);
            if (onClick != null)
            {
                Button b = bg.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                b.onClick.AddListener(() => onClick());
            }
        }

        private static void Note(RectTransform list, string text)
        {
            TextMeshProUGUI t = InventoryUIFactory.Text("Note", list, text, 15f, InventoryUIFactory.MutedColor, TextAlignmentOptions.Center, FontStyles.Italic);
            InventoryUIFactory.Size(t, -1f, 30f, 1f);
        }

        private static void Clear(RectTransform list)
        {
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                GameObject child = list.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false); // out of the list now (Destroy waits for the end of the frame)
                Destroy(child);
            }
        }

        private static void SetLabel(Button b, string text)
        {
            TextMeshProUGUI t = b != null ? b.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (t != null && t.text != text)
                t.text = text;
        }

        private static void SetGroup(CanvasGroup g, bool visible)
        {
            if (g == null)
                return;
            g.alpha = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
#endif
