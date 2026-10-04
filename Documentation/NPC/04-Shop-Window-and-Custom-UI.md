# 04 — Shop Window & Custom UI

**Scripts:** `NPC/Merchant/UI/MerchantWindow.cs`, `MerchantUISkin.cs`, `MerchantUIBuilder.cs`,
`MerchantItemEntryUI.cs`, `MerchantTabButton.cs`, `MerchantDetailsPanel.cs`, `MerchantConfirmDialog.cs`,
`MerchantDisplayEntry.cs`.

---

## 1. The window

```
┌───────────────────────────────────────────────────────────────────────────────┐
│ [portrait] Bram's Forge                                         1,250 g   [✕] │
│            Finest steel in the valley!                                        │
│ [ Buy ][ Sell ]                 [Search…      ] [Sort: Default] [Affordable] … │
│ [All 12] [Weapons 7] [Armor 4] [Consumables 1]                    (scrolls ⇆) │
│ [All Weapons] [Swords 3] [Axes 2] [Bows 2]                                    │
│ ┌──────────────────────────────────────────────┐ ┌──────────────────────────┐ │
│ │  items: slots, or a grid like the inventory   │ │ [icon] Iron Sword        │ │
│ │                                              │ │ Weapons ▸ Swords         │ │
│ │                                              │ │ description, stats…      │ │
│ │                                              │ │ Price 150 g · Stock 3    │ │
│ │                                              │ │ [−] [ 1 ] [+] [Max]      │ │
│ │                                              │ │ Total 150 g              │ │
│ │                                              │ │ [        Buy        ]    │ │
│ └──────────────────────────────────────────────┘ └──────────────────────────┘ │
│ Bought Iron Sword for 150 g.                                                  │
└───────────────────────────────────────────────────────────────────────────────┘
```

* **Buy / Sell** tabs (Sell hidden when the merchant buys nothing).
* **Category tabs** are made from the categories the items are in (with counts), in the order of the category
  database or of the merchant's *Shown Categories*. **Subcategory tabs** appear under a tab that has some. Nothing is
  hard-coded: new categories become new tabs.
* **Search** (name or category), **Sort** (Default, Name, Price ▲, Price ▼, Category), filters **Affordable** and
  **In stock** (buying).
* **Details** of the selected item: icon, name, category, description, what it does (the item's own tooltip lines),
  value, price, stock or how many the player has, durability, weight, grid size, quantity, total, the Buy / Sell button
  and why it is disabled ("Not enough gold", "No room in your bag for Greatsword (2×4)", "Sold out").
* Unaffordable prices are red; sold-out goods and items the merchant will not buy are faded with a badge.
* **Confirmation** before buying / selling when the merchant asks for it (*Confirm Purchases / Sales*, optionally only
  above *Confirm Above*). Enter confirms; Escape cancels.
* **Status line** with the result of the last purchase or sale.
* Close with ✕, **Escape**, the **Interact** key, or by walking away.

It refreshes by itself when the stock, the player's inventory (from anywhere: pickups, the hotbar, another script), the
wallet, the selected tab, the filters or a transaction change. Refreshes are grouped into one per frame and every UI
object is pooled.

## 2. Slots or grid — following the inventory

*Merchant ▸ Layout*:

| Layout | Buy tab | Sell tab |
|---|---|---|
| **Follow Inventory** (default) | Slots when the player's inventory uses slots; a **grid** when it uses the grid inventory | The same as the player's inventory |
| Slots | One item per slot | The bag's slots |
| Grid | A grid where every item takes its **Grid Size** | The bag's grid |

**Grid layout:** items are drawn over the cells they take, exactly like the grid inventory (same icon fitting, Grid
Icon, Grid Icon Angle, turned items, cell size and colours from *Inventory Manager ▸ Grid Inventory*). Goods are placed
in the shop's order, each where it first fits, turned sideways when allowed and it helps — the same rules as the
inventory. *Grid Columns* (0 = as many as the player's grid). The cell size is the player's, made smaller when the
columns do not fit.

**The Sell tab mirrors the player's inventory** (*Skin ▸ Mirror Inventory When Selling*, on): in slot mode the bag's
slots in order (empty ones too) and the hotbar; in grid mode the bag's grid with every item **where it is** (turned
ones turned) and the hotbar. Filters fade what does not match instead of moving items; what the merchant will not buy
is faded with ✕. Off = a list of the sellable items only.

Switching the inventory between slots and grid in Play Mode switches the open shop too.

## 3. Your own look: the skin

*Assets ▸ Create ▸ SimpleMovements ▸ NPC ▸ Merchant UI Skin*, assigned to *Merchant ▸ Skin*. One skin can be shared by
any number of merchants. **Every field is optional** — what is empty keeps the default look.

| Group | Fields |
|---|---|
| Prefabs | **Window Prefab** (your complete window, §5), **Slot Entry Prefab**, **Grid Entry Prefab** (`MerchantItemEntryUI`), **Tab Prefab** (`MerchantTabButton`), **Button Prefab** (a Button with a TextMeshPro label) |
| Sprites | Window / panel / slot backgrounds, selected frame, button and tab backgrounds, currency icon, Background Image Type (Sliced) |
| Font | TMP Font Asset, Font Scale |
| Colours | Window, panel, slot, grid item, accent, text, muted text, price, unaffordable, selected, buttons (buy / sell), tabs, success / error, dimmed alpha |
| Sizes | Window size (shrinks on small screens), slot size, grid cell size, spacing, details width |
| Selling | Mirror Inventory When Selling |
| Sounds | Open, close, buy, sell, error, click, volume |

## 4. The generated (fallback) UI

Without a Window Prefab, `MerchantUIBuilder` builds the whole window at runtime: header with portrait, name, greeting
and wallet; Buy / Sell tabs; search field, sort button and filters; scrollable category and subcategory rows; the item
area (a scroll view with a scrollbar); the details panel with the quantity selector and the action button; the status
line; the confirmation dialog. Every button, tab, field and entry is connected to the window — nothing is wired by
hand. The parts use the skin's prefabs, sprites, font, colours and sizes when they are set, and the inventory's
generated look (`InventoryUIFactory`) otherwise, so the shop matches the inventory.

The window goes on the player's **inventory canvas** (else a generated overlay canvas). One window is made per canvas
and skin and reused by every merchant using that skin.

## 5. A window prefab of your own

Make a UI prefab (a `RectTransform` under no canvas) with a **`MerchantWindow`** and assign its fields. All are
optional; leave out what your design does not have:

| Field | What it needs |
|---|---|
| Portrait (+ Portrait Frame), Shop Name Text, Greeting Text, Close Button | Image, TMP texts, Button |
| Wallet Text, Wallet Icon | TMP text, Image |
| Buy Tab, Sell Tab | `MerchantTabButton`s |
| Category Tabs, Subcategory Tabs (+ Subcategory Row) | Empty `RectTransform`s with a Horizontal Layout Group (tabs are added to them) — put them in a horizontal Scroll Rect if many tabs |
| Search Field, Sort Button (+ Sort Label), Affordable Filter, In Stock Filter | TMP Input Field, Button, `MerchantTabButton`s |
| Items Scroll, Items Content | A vertical Scroll Rect and its content (the window sets the content's anchors and height and places the items itself — no layout group on it) |
| Empty Text, Status Text | TMP texts |
| Details | A `MerchantDetailsPanel` with its own fields (icon, texts, quantity row with −, +, Max and a field, total, reason, action button) |
| Confirm Dialog | A `MerchantConfirmDialog` (message, confirm, cancel) |
| Slot / Grid Entry Template, Tab Template | Optional inactive children copied for items and tabs (the skin's prefabs win; else generated) |

**Item entry prefab** (`MerchantItemEntryUI`): Background, Icon, Name (optional), Price (+ Price Strip), Quantity,
Badge texts, Selected and Hover frames, a Canvas Group (fading). For the grid layout the icon is laid out by the window;
*Tint Background* off keeps your own art's colours.

**Tab prefab** (`MerchantTabButton`): Button, Label, Icon, Background, Selected Indicator. With a Horizontal Layout
Group (and Content Size Fitter) on it, it sizes to its label.
