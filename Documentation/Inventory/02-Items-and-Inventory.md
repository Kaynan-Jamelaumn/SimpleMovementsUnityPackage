# 02 — Items & Inventory

## 1. The item family

| Class | Create menu | Adds |
|---|---|---|
| `ItemSO` (abstract) | — | Name, description, `ItemType`, icon, prefab, stack max, weight, price, durability, cooldown, hand pose, use animation/sound/particles, pick-up time |
| `ConsumableSO` | SimpleMovements ▸ Items ▸ Consumable | Its own `UseItem` |
| `EquippableSO` | SimpleMovements ▸ Items ▸ Equippable | Classic stats (`effects`), **equip effects** (any `EquipmentEffect`), armor set membership, set visuals |
| `ArmorSO` | SimpleMovements ▸ Items ▸ Armor | Armor slot, Defense, Magic Resistance, durability modifier, **elemental resistances**, inherent traits, model and sounds |
| `WeaponSO` | SimpleMovements ▸ Items ▸ Weapon (or *Weapon From Template*) | Category, damage range, crit, knockback, scaling attribute, attacks, alternate attack, combos, weapon traits, passive effects |
| `MaterialSO` | SimpleMovements ▸ Items ▸ Material | Crafting resource: material kind (ore, wood, herb, gem...), tier |
| `MiscItemSO` | SimpleMovements ▸ Items ▸ Miscellaneous | Keys, junk, valuables; *Quest Item* cannot be sold |

Every item also has a **Trading** header: an optional shop **Category** and **Can Be Sold** — see
[NPC 05](../NPC/05-Money-Categories-and-Transactions.md). Merchants price items from their **Price** (value).

Every item has three virtual hooks used by the rest of the system:

| Hook | Used by | Purpose |
|---|---|---|
| `CollectEquipEffects(List<EquipmentEffect>)` | `EquipmentManager` | What the item does while equipped. `ArmorSO` adds its Defense/resistances and inherent traits; `WeaponSO` adds its passives |
| `AppendTooltip(List<string>)` | `ItemInfo` panel, inspector preview | Human-readable lines ("+12 Defense", "(2) Warrior's Vigor: ...") |
| `ValidateItem(errors, warnings)` | Inspectors, `InventoryValidator` | Problems with an explanation |

## 2. Slots

`SlotType` is the kind of a slot: `Common` (inventory, hotbar, storage) or an equipment slot (`Helmet`, `Armor`,
`Leggings`, `Boots`, `Gloves`, `Shield`, `Ring`, `Trinket`, `Cloak`, `Belt`, `Shoulders`, `Wrist`, `Amulet`...).
**Always use `SlotTypeHelper`** to convert between `ItemType`, `ArmorSlotType` and `SlotType`. The three enums have
different orders; the old code cast one into another and put items in the wrong slots.

| Rule | Helper |
|---|---|
| Which slot does an item belong in? | `SlotTypeHelper.RequiredSlot(item)` (armor by its armor slot, others by item type; `Common` = no special slot) |
| Can this item be placed here? | `SlotTypeHelper.CanPlace(item, slotType)` — common slots accept everything; equipment slots accept only their items |
| Does holding it here equip it? | `SlotTypeHelper.Equips(item, slotType)` — an equippable item in its own equipment slot |

## 3. `InventoryManager`

The manager owns the slot lists and coordinates the handlers:

| Handler | Job |
|---|---|
| `SlotManager` / `SlotUtilityManager` | Creates hotbar and inventory slots, finds equipment slots, layout |
| `ItemHandler` | Moves items between slots (swap, merge stacks, placement rules), drops items into the world |
| `DragHandler` | Drag visuals; turns off the dragged item's raycast blocking so the slot under the cursor is found |
| `HotBarHandler` | Selection with keys **1–9**, puts the selected item in the player's hand |
| `ItemPickupHandler` | Picks up `CollectableItem`s, with partial pickup when the inventory is almost full |
| `SplitItemHandler` | Splits a stack into a free slot |
| `StackOperations` | Merge/split math, durability lists per stack |

### Public API (unchanged names kept, new ones added)

```csharp
int  remaining = inventory.AddItem(itemSO, quantity: 5);          // returns what did not fit
InventoryItem  created = inventory.CreateItemInSlot(slot, itemSO, 1);
int  removed   = inventory.RemoveItems(itemSO, 3);
bool enough    = inventory.HasEnoughItems(itemSO, 3);
int  count     = inventory.GetItemCount(itemSO);
inventory.OpenInventory(); inventory.CloseInventory();
inventory.UseSelectedItem();                                      // the hotbar item
inventory.NotifyInventoryChanged();                                // after moving items from custom code
inventory.InventoryChanged += RefreshMyUI;

IReadOnlyList<InventorySlot> eq = inventory.EquipmentSlots;       // equipment slots found under the equipment panel
IEnumerable<InventorySlot> all  = inventory.StorageSlots;          // inventory + hotbar
EquipmentManager equipment      = inventory.Equipment;
```

Armor queries (`GetEquippedArmor`, `GetTotalDefense`, `GetCompleteSets`, `GetEquippedArmorForSlot`,
`UnequipAllArmor`, `OptimizeArmorSets`, `GetArmorSummary`...) remain and now read the `EquipmentManager`.

### Keys

The inventory listens to the player's input actions itself (the shared `PlayerInput` used by movement and
abilities): the first action found among **Inventory Action Names** (`Inventory`, `OpenInventory`,
`ToggleInventory`) opens/closes it, and **Use Item Action Names** (`UseItem`) uses the item in hand. When the
input actions have no inventory action, **Tab** and **I** work (*Keyboard Fallback*). No `PlayerInput` event has
to be wired in the inspector any more; if one still is, a key is never handled twice.

### Item panel and splitting

* **Hover** a slot while the inventory is open: a panel beside the inventory shows the icon, name, type,
  description, what the item does, **durability** (bar + value of the item in use), **weight** (each and for the
  stack), stack and value. Created automatically (`ItemHoverPanel`); turn it off with *Show Hover Panel*.
* **Right-click** a stack to split it: **Half**, the slider, −/+ or type the amount, then **Split** (Enter) - the new
  stack goes to a free slot in the same part of the inventory. Escape or a click outside cancels
  (`SplitStackPopup`, created automatically; *Right Click Splits* off = the old Item Info panel).

### Using items

* Tap *Use Item*: uses the hotbar item (`ItemSO.UseItem`). For a weapon it equips it and performs its Normal attack.
* Hold *Use Item* with a weapon whose attack can be charged: charges, release to attack.
* Durability is consumed per use/attack; when it reaches 0 the item breaks (`HeldItemBroke`) and is removed if
  `shouldBeDestroyedOn0UsesLeft`.

### Drag & drop rules

* Dropping on a slot swaps, merges (same item, up to its stack max) or refuses (placement rules).
* Dropping on the **drop zone** (the panel background, generated by the UI Builder) drops the item into the world.
* Releasing anywhere else, or cancelling (inventory closed mid-drag), returns the item to where it came from.
  Previously an aborted drag could destroy the item.
* After any move the hand is refreshed if the selected hotbar item changed and the equipment is re-synced.

## 4. World items

| Component | Notes |
|---|---|
| `CollectableItem` | An item in the world. Also an `IWeaponHittable`: with a gathering requirement (e.g. `Tool`) it must be hit by that kind of weapon; `None` = anything collects it |
| `ItemSpawner` | Spawns collectables; quantity and durability are now respected (previously spawned one item with quantity ignored, and could spawn durability 0) |
| `Storage` | Chests: `OpenStorage/CloseStorage` fill the storage panel |
