# 11 — Grid Inventory

An optional inventory where items take several cells (a 1×3 sword, a 2×2 helmet) and can be turned sideways.
It is turned on with **Inventory Manager ▸ Grid Inventory ▸ Use Grid Inventory**. Off (the default), the classic
inventory works exactly as before.

---

## 1. How it works

* The grid **is** the inventory's slots: `columns × rows` cells, row by row. An item is held by the cell of its
  **top-left corner** and covers `W × H` cells; the other cells it covers stay empty slots.
* The **slots remain the single source of truth**. Before every decision the occupancy is rebuilt from them
  (`GridInventoryModel<T>`, engine-free), so the view cannot create, lose or duplicate an item, and every placement —
  pickups, drags, rotation, splits, swaps, unequipping, mode switches — is checked for bounds and overlaps first.
* Everything that counts, removes, uses or equips items (crafting, ammo, quickslots, armour, weapons, storage)
  keeps working unchanged, because it still reads the same slots.
* The **hotbar, equipment and storage stay slots**. Items move between them and the grid as before.

| Class | Role |
|---|---|
| `GridInventoryModel<TItem>` | Placement rules: in bounds, no overlap, first fit (with rotation), move, swap, consistency check. No Unity code; unit-tested. |
| `GridInventory` | The grid over the inventory slots: cell ↔ position, anchor and owner cells, `PlaceAt`, `TryRotateInPlace`, `FindSpace`, `CountFit`, `PlanPacking`. |
| `GridInventoryView` | Draws it: lays the cells out, sizes and turns item icons over their cells, hover, origin and placement preview. Decides nothing. |
| `GridInventorySettings` | The inspector settings below. |
| `InventoryManager` | Input, drag and drop, rotation, mode switching, repair, and grid-aware adding / splitting / unequipping. |

---

## 2. Settings (Inventory Manager ▸ Grid Inventory)

| Setting | Default | Meaning |
|---|---|---|
| **Use Grid Inventory** | off | The mode. Can be switched in Play Mode (the items are re-packed, §5). |
| Columns / Rows | 10 × 6 | Grid size (2–30 each). |
| Cell Size | 56 | Pixels per cell. **0** = fit the cells to the inventory panel. |
| Spacing / Padding | 2 / 8 | Gap between cells, margin around the grid. |
| Resize Panel To Fit | on | With a fixed cell size, the inventory panel grows or shrinks to hold the grid. |
| Icon Fit Mode | Keep Proportions | *Keep Proportions*: as big as fits without stretching. *Fill Cells*: stretched over every cell (square pictures look squashed). |
| Icon Padding | 4 | Gap between the icon and the edge of its cells. |
| Rotate Action Names / Fallback Key | `RotateItem`, `Rotate` / R | Input that turns the dragged or hovered item. The fallback key works even if a gameplay action uses it, because it is only read while the inventory is open. |
| Show Rotate Hint | off | A line above the grid with the move and rotate controls of the device in use. |
| Right Click Rotates While Dragging | on | Right mouse turns the item being dragged. |
| Auto Rotate To Fit | on | Items being added (pickups, rewards) may be turned to fit. |
| Overflow | Refuse | Mode switch / resize when items would not fit: *Refuse* (nothing changes) or *Hotbar Then Drop*. |
| Colors | | Cells, item backdrop, hover, valid (green), stack / swap (yellow), invalid (red), origin. |

The **Inventory Slots** count is greyed out in grid mode: the grid's cells are used instead.

---

## 3. Item sizes

**Item ▸ Grid Inventory ▸ Grid Size** (cells across × down, 1–10) and **Can Rotate In Grid**. The item inspector
shows a drawing of the footprint, buttons for common sizes (1×1, 1×2, 2×2, 1×3, 2×3, 3×2) and the **usual size**
of its kind:

| Kind | Usual size |
|---|---|
| Potions, food, ammo, rings, amulets, trinkets, fist weapons, thrown weapons | 1×1 |
| Daggers, wands, bracers | 1×2 |
| Swords, maces, tools | 1×3 |
| Spears, staves | 1×4 |
| Axes, chest armour | 2×3 |
| Greatswords, hammers, bows | 2×4 |
| Crossbows | 3×2 |
| Helmets, leggings, boots, gloves, shoulders, cloaks, most shields | 2×2 (tower shields 2×3) |
| Belts | 2×1 |

**Tools ▸ SimpleMovements ▸ Inventory ▸ Grid Inventory ▸ Suggest Sizes For 1x1 Items** gives every item still at the default 1×1
its usual size, after showing the list (undoable). Weapon templates set it automatically.

Stacks keep one footprint whatever their count (20 arrows take one 1×1 cell).

**Icons in long items.** A square picture cannot fill a 1×3 item without being stretched, so by default it keeps
its proportions. To fill the cells:

* **Grid Icon Angle**: most RPG icons draw swords, spears, staves and bows diagonally. *+45°* or *−45°* stands them
  upright along the item's length (the item inspector previews it; pick the angle that looks right).
* **Grid Icon**: a second picture drawn in the item's shape (a tall picture for a 1×3 sword). It is used in the grid
  only; the hotbar, equipment and tooltips keep the normal Icon.

---

## 4. Controls

| Action | How |
|---|---|
| Move | Drag with the left mouse. The item is held by the cell you grabbed; the preview shows where it lands. |
| Rotate while dragging | R (or the `RotateItem` action), or right click. Only items with *Can Rotate In Grid* and a non-square size turn. |
| Rotate in place | Hover the item and press R. Refused (red flash and a message) when the turned shape does not fit. |
| Stack | Drop onto a stack of the same item (yellow preview); what does not fit goes back. |
| Swap | Drop onto exactly one other item (yellow preview): it takes the dragged item's old place, upright or turned, when it fits there. |
| From hotbar / equipment / storage | Drag onto the grid: the item shows its grid size over the grid and is placed like any other. |
| To hotbar / equipment | Drop on the slot. If the slot is full, the item there comes back into the grid (where the dragged one was, else anywhere it fits) or the move is refused. |
| Drop in the world | Release outside the panels (the drop zone), as before. |
| Split | Right click a stack → the new stack goes where it fits (grid first). |

Invalid drops (out of bounds, overlapping several items, no room) flash red, show a message and return the item to
where it came from — never lost, never duplicated. Turning is remembered per item (`InventoryItem.gridRotated`).

---

## 5. Switching modes and resizing

* **In the editor:** tick *Use Grid Inventory*, then *Rebuild UI For This Mode* (or *Build / Repair UI Now*) so the
  panel previews the grid. The UI Builder reads the grid settings and builds `columns × rows` preview cells.
* **In Play Mode:** the toggle, *Switch To Grid / Switch To Slots*, *Resize And Re-pack*, or from code:

```csharp
bool ok = inventory.SetGridInventory(true);   // false = refused, nothing changed
bool ok2 = inventory.ResizeGrid(12, 8);
```

The switch **plans first**: the bag's items are packed (biggest first, turned when that helps) into an empty grid
of the new size. Only if everything fits is anything moved. With *Overflow = Refuse* a switch that would not fit is
refused with the list of items; with *Hotbar Then Drop* the rest goes to free hotbar slots, then is dropped next to
the player (logged). Switching back to slots keeps the order and clears the turns. Hotbar items are not touched.

**Repair:** if older code or a mod puts an item into a cell another item covers, the conflict is detected at the
next change and the item is moved to free space (or the hotbar, or dropped) — the grid never stays overlapped.

---

## 6. Adding items from code

Nothing changes: `AddItem`, `HasEnoughSpace`, `RemoveItems`, `GetItemCount`, pickups and rewards are grid-aware.
`HasEnoughSpace` simulates the placement (existing stacks first, then new footprints, then the hotbar). New helpers:

| Method | Use |
|---|---|
| `FindFreeSlotFor(item, preferHotbar, out rotated)` | Where a new stack would go. |
| `PlaceInBag(inventoryItem)` | Moves an existing item (e.g. unequipped armour) into the grid / bag. |
| `Grid` | The `GridInventory` (null in slot mode): `Model`, `TryGetRect`, `CanPlace`, `CellAt`. |

---

## 7. Setup checklist

1. Inventory Manager ▸ Grid Inventory ▸ **Use Grid Inventory**; set columns, rows and cell size.
2. **Rebuild UI For This Mode** (the validator offers it when the preview does not match).
3. Give items their sizes (item inspector, or *Suggest Sizes For 1x1 Items*).
4. Optional: add a `RotateItem` action to the player's input actions (R is used otherwise).
5. The validator warns about items larger than the grid in both orientations.

---

## 8. Limitations

* Storage containers and the hotbar stay single slots (an item of any size takes one slot there).
* Stacks have one footprint regardless of quantity.
* Rotation is 90° only (upright / sideways); items are rectangles.
* The grid needs the inventory slots' parent to be a UI `RectTransform` (it is, with the generated UI); otherwise the
  classic slots are used with a warning.
* Items larger than the grid can never enter it: they go to the hotbar or are dropped, with a warning.
