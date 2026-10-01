# Inventory, Items, Equipment & Combat: Technical Documentation

**New here? Start with the [Setup Guide](08-Setup-Guide.md).** The player itself is in the [Player guide](../Player/README.md).

Documentation for the inventory and everything that is carried in it: items, armor and armor sets, weapons and
their attacks, and the equipment layer that turns worn/held items into stats, traits, passives and abilities.

| Chapter | What it covers |
|---|---|
| [01 — Architecture](01-Architecture.md) | The layers, who owns what, the equip flow, events |
| [02 — Items & Inventory](02-Items-and-Inventory.md) | `ItemSO` family, slots and slot rules, `InventoryManager` API, drag/drop, hotbar, pickup, storage |
| [03 — Equipment & Stats](03-Equipment-and-Stats.md) | `EquipmentManager`, `EquipmentEffect` catalogue, `CombatStats`, damage reduction and resistances |
| [04 — Armor & Armor Sets](04-Armor-and-Sets.md) | `ArmorSO`, `ArmorSet` tiers (2/4, 3/4, 4/4), upgrade groups, `ArmorSetManager` |
| [05 — Weapons & Attacks](05-Weapons-and-Attacks.md) | `WeaponSO`, attacks, chains, charge/hold, alternate attacks, behaviours, combos, the runtime |
| [06 — Editor Tools](06-Editor-Tools.md) | Inspectors, validation, UI Builder, Armor Set Wizard, weapon templates, tests |
| [07 — Changes & Migration](07-Changes-and-Migration.md) | Bugs fixed, behaviour changes, removed/renamed files, how to upgrade existing assets |
| [08 — Setup Guide](08-Setup-Guide.md) | **Step by step**: the inventory on the player, any item, consumables, armor, armor sets, weapons (templates, hit detection, a hammer with a ground slam) |
| [09 — Teams, Factions & Targeting](09-Teams-Factions-and-Targeting.md) | Layers vs relations vs filters, teams, factions, parties, friendly fire, recipes, multiplayer |

## Five-minute setup

1. Select the GameObject with the `InventoryManager` (or add one to the player).
2. Select the player, or open its prefab: the editor builds the whole UI by itself - canvas (inside the player),
   hotbar, inventory grid, equipment slots, storage panel, item info, drop zone, status bars and interaction
   prompt - with the slot and item prefabs, and wires every reference ([06 §3a](06-Editor-Tools.md)). The inventory
   inspector's **Build / Repair UI Now** does the same on demand; **UI Builder…** lets you choose sizes and slots.
3. The `InventoryManager` inspector now shows no problems; any that remain have a **Fix** button.
4. Create items with **Assets ▸ Create ▸ Scriptable Objects ▸ Item ▸ …**. For a weapon, start from
   **Assets ▸ Create ▸ Scriptable Objects ▸ Item ▸ Weapon From Template ▸ Sword** (or any other template) and adjust it.
5. Create an armor set with **Tools ▸ Inventory ▸ Armor Set Wizard**, then fill in its bonus tiers.
6. Press Play. In the `InventoryManager` inspector's **Live (Play Mode)** section, give yourself items and watch the equipment,
   stats and set bonuses update.

## Design rules

* **Data-driven.** Everything an item does is data on its asset: effects are picked from dropdowns
  (`[SerializeReference]` + SubclassSelector), the same way abilities and traits are built.
* **Assets are never modified at runtime.** Trait enhancements, weapon traits and set bonuses are applied to the
  character, not written into the ScriptableObjects (previously they were, and the changes stuck after play mode).
* **Everything applied can be removed exactly.** Every effect returns a handle that reverts precisely what it
  applied. Equipment is *reconciled* (the desired state is compared to the current one), so the same item is never
  applied twice, and unequipping always cleans up.
* **One source of truth.** Items in equipment slots (and the weapon in hand) are what is equipped. The
  `EquipmentManager` follows the slots; nothing else applies stats.
