# 07 - Inventory and items

The inventory is server-authoritative. The **server's copy** of each player's `InventoryManager` is the truth: what
the hotbar, the bag (slots or grid), the equipment slots and an open storage hold, the item in hand, the quickslots.
The owner's machine shows a **snapshot** of it and turns every action into a **request**; the server repeats the
request through the inventory's own code on its copy, so every existing rule applies (slot types, stacking, grid fit,
two-handed weapons, cooldowns, room, weight). Other players only see what a player **holds and wears**. Items lying in
the world, loot, harvests and storages are server objects too.

Code:
- `Networking/Core/Inventory/InventorySnapshot.cs` (always compiled, pure): `InventorySlotRef`, `InventoryStackData`,
  `InventorySnapshot`, `InventoryAppearance`, `InventoryRequest`, `InventoryNetRules` (validation), the send schedule
  and `InventorySnapshotDiff` (how a client shows a snapshot). `SceneObjectPath.cs`, `WorldItemState.cs`.
- `Inventory/InventoryManager/InventoryManager.Network.cs` (always compiled): `NetRole`, `IInventoryNetBridge`,
  snapshot capture / apply, appearance, `ApplyRequest` (the server repeating a request).
- `Inventory/WorldItems.cs` (always compiled): creating, publishing and removing world items.
- `Networking/Runtime/Inventory/NetInventory.cs` and `NetWorldItem.cs` (`#if FISHNET`).
- Tests: `CustomEditor/Networking/Tests/InventoryNetTests.cs`.

## Roles

`InventoryManager.NetRole` is read when acting (never cached):

| Role | Where | What the inventory does |
|---|---|---|
| `Local` | offline, the server (dedicated or host), the host's own player | exactly what it always did; it is the truth |
| `OwnerClient` | a client's copy of its own player | shows the server's snapshot; every action becomes a request |
| `RemoteCopy` | a client's copy of another player | shows only the item in hand and the equipment; no input, no rules |

The role comes from `NetBridge`: `NetInventory` installs itself as the bridge on client copies only. Offline, and on
the server, nothing is installed and every code path is the one that existed before. Input (Tab / I, the use key,
number keys, rotate, quickslot keys, the draw / sheathe key, pointer events) is read only by the player controlled from
this machine (`NetAuthority.IsLocallyControlled`), so a host's keys never reach the server copies of other players.
The hotbar selection is now per inventory (`HotbarHandler.State`); it used to be static, shared by every player.

## The snapshot (server -> owner)

```
server copy                                                   owning client
-----------                                                   -------------
InventoryChanged / HotbarSelectionChanged -> MarkDirty
every request (accepted or refused)       -> MarkDirty
poll (0.5 s): capture, compare with last sent (durability, rounds loaded change without an event)
send at most every 0.05 s  ------------- TargetRpc ------->   ack >= last request sent?  (else skip: a newer one comes)
  InventorySnapshot: ack, layout (hotbar / bag / grid size),   dragging? keep it until the drop
  every occupied slot (hotbar, bag, equipment, open storage),  layout -> storage window -> stacks -> selection -> quickslots
  selection, quickslots, storage size and name                  InventorySnapshotDiff.Plan: keep / update / move / remove / create
```

- **Full, not delta.** Only occupied slots are listed (an inventory is a few hundred bytes to ~2 KB), a lost or late
  update can never leave the client wrong, and a reconnecting client needs nothing else. A burst of changes (a drag that
  swaps, merges and re-equips) is one message.
- **Owner only**: `TargetRpc` to the owner; nobody else learns what is in someone's bag.
- **Showing it runs no gameplay**: no weight changes, no pickups or drops, no use effects. Stacks still in their slot
  are kept (only their numbers change), moved stacks are moved (the same object, so an equipped item is not unequipped
  and equipped again), the rest is created or removed. Per stack: item id (`ItemDatabase`), count, uses left of the
  unit in use and of every other unit, rounds loaded, grid turn, cooldown left.
- **Acknowledgements**: every request carries a sequence number; the snapshot carries the last one the server handled.
  A snapshot older than the client's last request is skipped, so a predicted move is never briefly undone.

## Requests (owner -> server)

| Client action | Request | Shown at once? | Server checks |
|---|---|---|---|
| drag and drop between slots, grid moves, merges, swaps, storage <-> bag | `Move` (from, item expected there, target slot or grid cell + turn) | yes | item still in that slot, storage open, then the normal drop code |
| drop into the world | `Move` with `flag` and a point | no (item stays until the server dropped it) | point kept within 4 m of the player; the item's prefab; network-spawned for everyone |
| split a stack | `Split` (slot, item, amount) | yes | amount below the stack; the normal split (free slot) |
| rotate a grid item in place | `Rotate` | yes | item is a grid item; the normal rotation |
| number keys / `SelectHotbarSlot` | `SelectHotbar` | yes | range; the server copy equips that weapon (the weapon layer attacks with it) |
| use the held item (potion, food) | `UseSelected` (slot, item) | no | not a weapon (weapons go through `NetWeaponCombat`), cooldown, alive |
| use a consumable from a quickslot | `UseItem` (item, include hotbar) | cosmetic cooldown only | not a throwable (those go through `NetWeaponCombat`), stunned, cooldown |
| assign / clear a quickslot | `AssignQuickSlot` | yes | item can go in a quickslot |
| pick up an item | world `Pickup` (target by NetworkObject, or scene path) | no | same scene, within 6 m, still there, same item, hold time (`PickupHold` sent when the key went down) |
| open / close a storage | world `OpenStorage` / `CloseStorage` | close yes, open no | same scene, within 6 m, not in use by another player |
| quick equip / unequip all / optimise armor | `QuickEquipArmor` / `UnequipAllArmor` / `OptimizeArmor` | no | the armor is in the inventory |
| draw / sheathe weapons | `SetDrawn` | yes | - |

Every request is rate limited (30/s, uses 8/s, world requests 10/s), malformed ones are dropped
(`InventoryNetRules.IsWellFormed`: kinds, slot ranges, id and path lengths, finite points), and only the owner may send
(`RequireOwnership`). Dead players may only change the selection, quickslots, close a storage or resync. Messages the
server's copy shows ("No room", "Someone else is using this") are forwarded to the owner's screen.

Gameplay code that mutates an inventory (`AddItem`, `RemoveItems`, `ConsumeAmmo`, `ConsumeItem`, `CreateItemInSlot`,
`PlaceInBag`, `InstantiateClassItems`) must run on the server: on a client copy those calls do nothing (logged once)
and the server's result arrives with the snapshot. Merchants, currency backing items, loot loss
(`CyclePenalties.ApplyLootLoss`) and weapons already run there, and the owner sees their result automatically because
the snapshot follows the inventory's own change events.

## What other players see (server -> everyone watching)

`NetInventory` replicates an `InventoryAppearance` SyncVar: the item in hand (and its hotbar slot), the item of every
equipment slot, drawn or sheathed. A remote copy shows it through the normal path - the items are placed in its own
(hidden) hotbar and equipment slots - so `EquipmentVisuals` draws the weapon in hand, the shield, the armour models and
the set visuals exactly as offline. The owner's draw / sheathe key is sent to the server (`SetDrawn`).

**Equipment effects** run where they belong (`EquipmentEffect.Scope`): `Simulation` effects (stats, combat stats,
traits, behaviours, abilities on keys: everything movement prediction and cooldown display need) run on every copy, the
server's values winning through the status module's replication; `Rules` effects (`OnHitEffect`, `WhenHitEffect`,
`PulseEffect`: they decide gameplay) run only where the character's rules run; `Cosmetic` effects (`AttachedVisualEffect`)
run everywhere. Damage, healing, item use and every number that matters are decided on the server.

## World items

- **Created only on the server** (`NetSpawn.ShouldSpawnHere`): `ItemHandler.SpawnWorldItem` (drops, merchants' overflow,
  re-pack overflow), `ItemSpawner.SpawnItem` (mob loot from `MobStatusController`, harvested `CollectableItem`s, chests
  of the dungeon module), `RegenerateCollectableItem`, recovered arrows and thrown weapons
  (`WeaponProjectile.RecoveredItemSpawner`, installed by `NetWorldItemService` on the server). Clients never create them.
- Each one is **moved into the scene of what it came from** (the player, mob, chest or projectile) and
  **network-spawned** (`WorldItems.Publish`): interest management and physics are per scene.
- **Which prefab**: when the item's own world prefab is a network prefab (Tools ▸ SimpleMovements ▸ Network ▸ Make
  Selected Prefab Networked ▸ Pickup or Loot Drop: NetworkObject + `NetWorldItem` + NetworkTransform), it is used.
  Otherwise the generic **carrier** is used: a prefab `Resources/SimpleMovements/Networking/NetWorldItem` with a NetworkObject and a
  `NetWorldItem` and no renderer; it builds the item's model (Equipment Visuals model, else its world prefab, stripped of
  scripts and physics) on every machine and fits a collider for the pickup raycast. Without either, the item exists on
  the server only (a warning says so).
- `NetWorldItem` replicates a `WorldItemState` (item id, quantity, uses left of each unit, spin) so every client shows
  the right model and prompt; a partial pickup updates it. Client copies are kinematic (the server simulates).
- **Pickup is a server decision** (range, same scene, still there, hold time, room): the item is despawned for everyone
  when it is all taken (`WorldItems.Remove`). Items placed in a scene file without a NetworkObject work too: the client
  names them by their scene path (`SceneObjectPath`), and when one is picked up every client watching the picker removes
  (or updates) its own copy.
- `CollectableItem` takes tool damage only where rules run and is despawned when harvested (give it a NetworkObject).

## Storage

A `Storage` (chest) is used by **one player at a time** (`Storage.User`): two windows on the same contents could both
take the same items. Opening it on the server fills that player's storage window; the snapshot carries the window's
size and slots, and the client opens a **view** (`StorageManager.OpenView`) that saves nothing when it closes. Moves
between the storage window and the bag are ordinary `Move` requests. The server closes (and saves) the storage when the
player walks more than 6 m away, dies, leaves the game or the storage disappears. Other fixes that also apply offline:
closing saves into the storage that is actually shown (it used to save into the one clicked), opening another storage
saves the first one, slots are saved by the window's size (a hidden panel no longer saves everything as empty), stored
items keep their uses left and rounds loaded, and random contents are rolled only where the rules run.

## Starting items

`PlayerStartItemController.SetPlayerClassItems` and `InventoryManager.InstantiateClassItems` only grant on the server
(or offline). Class items given as prefabs are added by item, quantity and durability (the prefab is never "picked up").

## Prefab setup

- **Player prefab**: `NetInventory` on the root next to the NetworkObject (the editor tool adds it). The inventory and
  its UI canvas must be part of the player prefab (`NetPlayer` hides the canvas of other players' copies).
- **Item database**: Tools ▸ SimpleMovements ▸ Project Setup ▸ Build Item Database. Items that are not in it cannot be sent
  (they are left out of snapshots, with a warning).
- **Carrier**: `Assets/Resources/SimpleMovements/Networking/NetWorldItem.prefab` - an empty GameObject with a NetworkObject and `NetWorldItem`
  (Fish-Net registers it as a network prefab automatically).
- **Networked pickups / loot / collectables**: Make Selected Prefab Networked ▸ Pickup or Loot Drop (adds the
  NetworkObject, `NetWorldItem` and a NetworkTransform). Scene chests and pickups can stay plain scene objects.

## Testing locally

1. Build the item database, create the carrier prefab, add `NetInventory` to the player prefab (Make Selected Prefab
   Networked ▸ Player).
2. Tools ▸ SimpleMovements ▸ Network ▸ Run ▸ Server And Two Clients (or Play In Editor As ▸ Host plus a client build).
3. On client 1: drag items between hotbar, bag, grid and equipment; split, rotate, use a potion, assign and use a
   quickslot; the server log shows no warnings and the result stays after a moment (the snapshot confirms it).
4. Equip a helmet and a sword on client 1 and select the sword: client 2 sees them on client 1's character; drawing and
   sheathing follows.
5. Drop an item on client 1: both clients see it; pick it up on client 2: it disappears for both and appears in client
   2's inventory only. Kill a mob: its loot appears for both in that scene only.
6. Open a chest on client 1, then try on client 2 ("Someone else is using this"); take an item on client 1, close, open
   on client 2: the item is gone from the chest, no duplicate.
7. Single player (Play In Editor As ▸ Single Player, or without Fish-Net): everything works as before.
8. Edit Mode tests: Window ▸ General ▸ Test Runner ▸ `GameNet.Tests.InventoryNetTests`.

## Limitations

- Requests that move items between slots are shown at once on the owner; uses, pickups, drops and opening a storage wait
  for the server (one round trip, a few milliseconds on 127.0.0.1).
- A scene item without a NetworkObject that was picked up is removed only on the clients watching the picker at that
  moment; a client that loads the scene later still shows it (it cannot be picked up again - the server has none).
  Give scene pickups a NetworkObject when that matters.
- Quickslot cooldown circles on the owner are a local estimate; the server's stack cooldown decides.
- Pulse and proc equipment effects show their visuals only on the server's machine (the host); their results (damage,
  healing) are replicated by the status module.
- Runtime layout changes (grid on/off, size, slot counts) are decided by the server and arrive with the snapshot; the
  client's inspector cannot change them during a session.
- Networked world items do not turn in place on the server or the host when a NetworkTransform syncs them (clients turn
  their own copy); the carrier turns everywhere.
