# 06 — Editor Tools

## 1. `InventoryManager` inspector

| Section | Contents |
|---|---|
| **Problems** | Everything `InventoryValidator` finds, with severity, explanation and a **Fix** button when it can be fixed automatically (see §2) |
| **References** | Item prefab, panels, hand, camera, player and player systems, grouped; **Auto-assign** finds them on the player and in the hierarchy |
| **Slots** | Hotbar / inventory slot counts, Slot Manager and Layout Manager settings |
| **Grid Inventory** | *Use Grid Inventory*, the grid settings, **Rebuild UI For This Mode**, **Suggest Item Sizes…**; in Play Mode the grid's state (items, free cells, consistency), **Switch To Grid / Slots** and **Resize And Re-pack** ([11](11-Grid-Inventory.md)) |
| **Hands, Visuals & Quickslots** | Equipment visuals, shield blocking, quickslots and messages on / off; in Play Mode what each hand holds (stowed, dual wielding) and the quickslots with their counts ([10](10-Hands-Shields-Ranged-and-Body-Parts.md)) |
| **Layout Presets** | One-click layout presets and grid settings |
| **Live (Play Mode)** | Give / remove any item, open/close the inventory, force an equipment sync, and see what is equipped, the combat stats, active set bonuses and the weapon's state (current attack, chain step, combo) |
| **Settings Export / Import** | Copies the manager's settings to / from JSON |

The old inspector (nine partial files plus a second `CustomEditor` for the same class, which Unity rejected) was
replaced by this single editor and the validator.

## 2. Validation (`InventoryValidator`)

Checked, with automatic fixes where possible:

* Item prefab: present, has `InventoryItem`, an `Image` for the icon, a `Text` for the stack count.
* Slot manager: slot prefab and the hotbar / inventory / equipment slot parents.
* Panels: inventory panel, equipment panel with equipment slots (not marked as hotbar), storage panel structure
  (child 0 background, child 1 slots).
* Player: assigned; status controller, weapon controller (with a hand), hand parent; missing runtime components
  (`EquipmentManager`, `CombatStats`, `ArmorSetManager`) are reported as info (they are added automatically).
* UI: under a Canvas with a `GraphicRaycaster`, the manager is a parent of the UI (so it receives clicks), an
  `EventSystem` exists, an item info panel and a drop zone exist.

`InventoryValidator.Validate(manager)` can also be called from your own tools or tests.

## 3. UI Builder — *Tools ▸ SimpleMovements ▸ Inventory ▸ UI Builder*

Generates and wires the full inventory UI:

```
Inventory UI (Canvas, CanvasScaler, GraphicRaycaster, InventoryPointerRelay)
├── Hotbar
│   └── HotbarSlots            (grid, N slots created at runtime)
├── InventoryPanel             (opened with the inventory key)
│   ├── Background             (+ DropItem: InventoryDropZone — releasing here drops into the world)
│   └── InventorySlots         (grid)
├── EquipmentPanel
│   └── EquipmentSlots         (one labelled row per chosen SlotType)
├── StoragePanel
│   ├── Background             (child 0)
│   └── Slots                  (child 1)
└── ItemInfo                   (name, type, quantity, weight, price, description, stats, Split Stack button)
```

Options: slot size and spacing, columns, hotbar/inventory/storage slot counts, which equipment slots, which
optional parts (equipment, storage, item info, drop zone), colours, and the folder where the **slot and item
prefabs** are saved. Every reference on the `InventoryManager`, its `SlotManager` and `ItemInfo` is assigned, an
`EventSystem` is created when missing, and the whole build is one Undo step. Re-running it finds and reuses the
objects it created before (adding what is missing), so it can be run again after changing options.

## 3a. Automatic setup (no button needed)

The UI is built automatically in the editor (`InventoryAutoSetup`) when you:

* add an `InventoryManager`, `Player`, `PlayerStatusController` or `ArmorSetUIManager` component,
* open a player prefab in **Prefab Mode**,
* select a player (or an inventory) whose UI is missing.

It creates only what is missing, once per object and editor session, as one undoable step:

| Part | Where | Wired to |
|---|---|---|
| Canvas (Screen Space Overlay, scaler 1920×1080) | **inside the player**, so the prefab carries its own UI | — |
| Hotbar with preview slots | bottom centre | Slot Manager ▸ Hotbar Slots Parent / Grid |
| Inventory panel with preview slots, drop zone | centre, closed | Inventory Parent, Inventory Slots Parent / Grid |
| Equipment panel: one typed, labelled slot per equipment slot (helmet, armor, rings…) | right, closed | Equippable Inventory, Equipment Slots Parent |
| Storage panel, item info, split button | centre, closed | Storage Parent, Item Info |
| **Status bars**: one per status manager of the player (health, mana, stamina, hunger, thirst, sleep, sanity, body heat, oxygen, weight) with "75 / 100" values | top left | each manager's **UI Image** |
| Interaction prompt (progress circle + text) | screen centre | `Player` ▸ Interaction UI, Filling Circle, Interaction Text, Inventory Manager |
| **Armor Sets window**: worn-set list, selected set (icon, name, progress bar, description), its pieces, active / next bonuses, close button; a **Sets** button on the inventory panel | centre, closed | every field of `ArmorSetUIManager` (player's `ArmorSetManager`, `InventoryManager`, canvas, audio source) and the inventory's Armor Set UI Manager |
| Slot and item prefabs, bar and circle sprites, armor set entry prefabs (`ArmorSetListItem`, `ArmorSetBonus`, `ArmorSetPieceSlot`) | `Assets/Inventory/Generated` | Item Prefab, Slot Prefab, the set UI's prefabs |

* The preview slots show the layout while editing; when the game starts the inventory replaces them with the
  configured **Number Of Hot Bar Slots / Number Of Inventory Slots**.
* An **EventSystem is never added to a prefab** (it belongs to the scene). In a scene the builder adds one only if
  the scene has none (inactive ones included), and at runtime the inventory keeps exactly one active
  (`InventoryEventSystems`).
* On a player that is a **prefab instance** in a scene, the UI is built **in the prefab asset** (every instance gets
  it from there) - never added to the instance as overrides, which used to build it twice (instance + prefab) and
  show two or three copies of the hotbar and status bars.
* One status bar per status *type*; existing bars are reused, duplicate bars are removed, and a second component of
  the same status type on the player is reported. If a player already has several inventory UIs, the inventory
  inspector shows it with a **Remove extras** button.
* The inventory inspector has **Build / Repair UI Now** (the same, on demand) and **UI Builder…** (choose sizes,
  counts, equipment slots and colours). Turn the automatic part off with
  *Tools ▸ SimpleMovements ▸ Inventory ▸ Auto-Build UI When Missing*.

### Armor Sets window (`ArmorSetUIManager`)

Its inspector lists what is not assigned and has **Build / Repair UI** (creates what is missing and assigns every
field; when the player has no canvas it creates one inside the player), **Select Window** and **Show / Hide
Window** (to arrange it while editing; it always starts hidden in game). Adding the component to an object with no
player or inventory builds its window (and a canvas) by itself. The window opens with the inventory's **Sets**
button, with an input action named `ArmorSetUI` / `ArmorSets` / `Sets` (*Armor Set Action Names* on the inventory),
and by itself when a set is completed (closing again after *Auto Hide Delay*).

## 4. Item inspectors

* **All items** (`ItemSOEditor`): problems with explanations, all fields, and a **tooltip preview** showing exactly
  what the item does in game (built from the same `AppendTooltip` the game uses).
* **Armor** (`ArmorSOEditor`): slot, defense summary, set membership with **Add to the set's pieces** when the set
  does not list the piece.
* **Weapons** (`WeaponSOEditor`): *Attacks at a glance* (duration, damage, reach, chain, charge; **Edit** and **▶** per
  input), then sections - **Item**, **Damage & Stats** (min–max on one line, average hit and damage per second),
  **Attacks** (one tab per input; each attack in foldouts: Animation & Timing with a startup / hits / recovery bar,
  Cost & Movement, Damage, **Hit Area & Targets** with a top-down drawing of the area, the lunge and the weapon range,
  On Hit, Charge, Behaviours, Sound & Visual, **Chain** - add / reorder / remove hits, ▶ per hit - and Trait
  Requirements), **Hit Area & Range** (what each attack reaches, **Set Max Range From Attacks**, the old Attack Cast),
  **Combos**, **Traits & While Wielded** (the difference spelled out; a warning when a trait is in both),
  **Animation & Sound**. Use Feedback is hidden (weapons do not use it). Every section explains its fields.
* **Attack Preview** (▶, or *Tools ▸ SimpleMovements ▸ Inventory ▸ Attack Preview*): plays or scrubs an attack (any input, any
  chain hit, any charge) on the character in the scene - the selected object, the prefab being edited or the
  player - without Play Mode. In the Scene view it draws, coloured by phase (yellow wind-up, red hitting, grey
  recovery): the Hit Shape (moving with the lunge, with its height band), or for **Weapon Blade** attacks a
  **temporary copy of the weapon in the animated hand** (never saved; removed on Stop / close) with its **hit
  volumes** (unused ones faint) and the **trail** they sweep; the **Impact** area where the contact volume reaches
  the ground (orange); the weapon's range and the old Attack Cast at the hand. Stop / closing the window restores
  the pose. In Play Mode the Weapon Controller's debug gizmo draws the active attack's real hit area or volumes.
* **Hit volumes**: *Hit Area & Range ▸ Add Volume ▾* presets; each volume shows only the fields of its shape; each
  attack has **Volumes Used** toggle buttons; problems (an attack naming a missing volume) are listed.
* **Faction inspector**: how the faction stands toward every other faction of the project and toward characters
  without one, with warnings for contradictory lists.
* **Hit Filter** fields (weapons and abilities) are four buttons - Self, Allies, Enemies, Neutral - with tooltips.
* **Combo trees** (`ComboTreeEditor`): problems and a readable branch list ("Heavy → Finisher when stamina ≥ 30").
* **Drawers**: one-line classic stats with units in the tooltip and a live description; one-line combat stat
  modifiers and resistances; combo conditions showing only the fields their type uses; set bonus headers
  ("(4 pieces) Warrior's Vigor II [vigor]"); attack effects without the unused Attack Cast.

### Quick Setup (every item)

At the top of every item inspector: the icon, a one-line summary and

* **Auto-Fill** - display name from the asset name; icon and prefab found by name (`IronSword`, `IronSword_Icon`…);
  armor slot, weapon category, food or potion guessed from the name (boots, dagger, bread…); stack size and
  durability defaults.
* **Icon From Prefab** - renders the prefab and saves `<item>_Icon.png` next to the item as its icon.
* **Presets…** - armor: **materials** Cloth, Leather, Chainmail, Iron, Steel, Mythril (scaled by slot; jewelry gives
  magic resistance only); weapons: **movesets** (the templates) and **tiers** Wooden → Legendary (damage, crit,
  durability, weight, value by category); consumables: minor/normal/greater health, regeneration, mana and stamina
  potions, bread, cooked meat, apple, water. Also *Create ▸ SimpleMovements ▸ Items ▸ Consumable Preset*.

Every field of the item assets has a tooltip (units and examples). The Armor Set Wizard has a **Material** option.

### Grid size, shields, ammo, hands and ranged

* **Every item:** a drawing of its grid footprint under the quick setup, buttons for common sizes and **Usual: W×H**.
* **Armor:** *Shield Defense* is shown only for shields; **Presets ▸ Shield Defense** (Buckler, Round, Kite, Tower)
  and a summary of what the shield blocks.
* **Ammo:** presets (arrows, broadheads, bolts, bullets, shells, darts, stones) and the weapons that fire it.
  *Assets ▸ Create ▸ SimpleMovements ▸ Items ▸ Ammo Preset* creates one.
* **Weapon:** *Hands & Guard* (grip, dual wielding, off-hand attack, Weapon Guard preset) and *Ranged Weapon*
  (mechanic, a summary, a warning for firing inputs without an attack, the matching ammo in the project with
  **Create Ammo**). Each attack has a *Body Part & Blocking* group (aimed part, guard damage, unblockable) and says
  when it fires a projectile. "Attacks at a glance" ends with the hands / guard / ranged summary.
* **Validator:** warns when the grid preview does not match the grid, when items are bigger than the grid, when the
  equipment panel has no Off Hand slot, and when the off hand has no bone on a non-Humanoid.

## 5. Armor sets

* **Armor Set inspector** (`ArmorSetEditor`): problems (including duplicate set names and pieces that point to the
  set without being listed), a piece list with **Link** buttons, **Add pieces that reference this set**, **Link all
  pieces**, **Remove empty**, a drop area for armor assets, and a **Bonus Simulator**: a slider for the number of
  worn pieces showing exactly which tiers are active (upgrade groups included) and the next threshold.
* **Armor Set Wizard** (*Tools ▸ SimpleMovements ▸ Inventory ▸ Armor Set Wizard*): name, colour, folder, which pieces, defense per
  piece, and which 2 / 3 / 4-piece tiers; creates the set and all pieces already linked both ways.

## 6. Weapon templates

*Assets ▸ Create ▸ SimpleMovements ▸ Items ▸ Weapon From Template*, or **Apply Template…** on a weapon:

| Template | Moveset |
|---|---|
| Sword | 3-hit slash chain, charged wide slash, shield bash (alternate: knockback, chance to stun, brief invulnerability), lunging thrust (special) |
| Greatsword | 2-hit cleave, charged ground slam with a stunning area burst |
| Dagger | Fast 4-hit stab chain with early cancel, poisoned strike, backstep with invulnerability |
| Spear | Thrust chain (line hits), charged impaling lunge, knockback sweep |
| Hammer | Smash chain with a chance to stun, charged earthquake (earth, slows) |
| Axe | Chop chain with a chance to bleed, charged overhead chop |
| Bow | Draw-and-release with the **Bow mechanic** (arrows = Ammo of type Arrow), knockback bow bash |
| Staff | 3-bolt magic chain casting at the target (assign the ability), charged knockback nova, weapon guard |
| Crossbow | Magazine of 1 bolt (slow reload, pierces 1), stock strike on Alternate |
| Throwing Knife | Throw mechanic (stack of 10, recoverable), slash chain on Alternate, dual-wieldable |
| Pistol | Magazine of 8 bullets, fire interval, bloom, damage falloff with distance, one-handed |

The Greatsword and Staff templates also turn on the weapon **Guard**. Every template sets the usual **grid size**
and clears a ranged mechanic or guard left from before.

Templates only fill data (timings, damage, shapes, charge, behaviours, on-hit effects); animations, sounds and
abilities are left for you. Applying a template can be undone.

## 7. Tests

`CustomEditor/Items/Tests/InventoryEquipmentTests.cs` (Edit Mode, *Window ▸ General ▸ Test Runner*; compiled only
with the Unity Test Framework):

| Test | Checks |
|---|---|
| Defense reduction | The curve, the half value and the cap; negative defense increases damage |
| Modifier tokens | Adding/removing modifiers reverts exactly; removing twice does nothing |
| Weapon-limited modifiers | Only count with the matching weapon category |
| Elemental resistance | Clamped (weaknesses down to −100%) |
| Equipment effect handles | Apply then revert leaves no trace |
| Upgrade groups | 2/3/4-piece tiers: only the highest reached tier of a group is active |
| Set piece counting | Distinct pieces only |
| Set completion | Highest tier vs. *Requires All Pieces* |
| Slot rules | Every armor type fits and equips only in its slot and converts back |
| Combo sequences | Match the end of the input history |
| Weapon templates | Every template produces a valid weapon |

`CustomEditor/Items/Tests/CombatInventoryFeatureTests.cs`:

| Test | Checks |
|---|---|
| Grid placement | No overlap, nothing out of bounds or at negative positions, `At`, free cells, consistency |
| Grid move / swap / remove | Moving over its own cells, swaps only when both fit (a refused swap changes nothing), freed cells |
| Grid rotation search | A 1×3 item fits a 2-row grid only turned |
| Hand rules | Two-handed from category and grip; off-hand rules; a two-handed weapon stows the shield; dual wielding |
| Shield coverage | Front arc only; True damage is never blocked |
| Body parts | Humanoid bands (head, torso, arms at the side, legs), multipliers, armour coverage |
| Bow | A full draw is stronger, faster and more accurate; over-holding shakes |
| Ammo | Matches by type, ignoring case |
| Grid sizes | Suggested size by kind; clamping to 1–10 |
