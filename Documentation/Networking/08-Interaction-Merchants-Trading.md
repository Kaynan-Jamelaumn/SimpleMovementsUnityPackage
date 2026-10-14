# 08 - Interaction, merchants and trading

This module makes the interaction system, the NPCs and their shops, the players' money and player-to-player trading
server authoritative. Clients send what they want to do; the server checks it with its own positions, inventories,
wallets and stock, and does it.

| Component (namespace `GameNet`) | Where | What it does |
|---|---|---|
| `NetInteractor` | Player prefab root | Relays interactions, shows NPC sessions on the owner's screen, sends shop stock, performs purchases and sales |
| `NetWallet` | Player prefab root | Replicates the player's balances to the owner only |
| `NetInteractable` | Root of a networked interactable (door, merchant NPC, gate, lever) | Makes it addressable and replicates its state |
| `NetTrading` | "Network World" object of the Bootstrap scene (global relay) | Player-to-player trading: rules, atomic exchange, per-client state, trade window |

Always-compiled parts: `IInteractionRelay` and `IReplicatedInteractable` (Essentials/Interactables/InteractionNetworking.cs),
`INPCSessionRelay` (NPC/Core/NPCSessionRelay.cs), `TradeBook`, `TradeExchange`, `TradeView` and `InteractionPath`
(Networking/Core/Economy). Offline - with or without Fish-Net installed - every hook is null and everything works as
before.

## The interaction relay (used by every module)

On a client, the owner's `PlayerInteractor` still finds targets, shows the prompt and the highlight, and times holds - all
local. When the player interacts, it does **not** call `Interact` itself: it gives the target to its `Relay`
(`NetInteractor`), which sends an `InteractTargetRef` to the server:

- `root`: the `NetworkObject` the target belongs to (searched up the hierarchy), and `path`: the path from it down to the
  target (`InteractionPath`, "" for the root itself);
- or, for an object of a scene that has no network identity, `inScene = true` and `path` = its `SceneObjectPath` in the
  player's scene (the server and every client load the same scene files; a path never reaches into a networked object);
- `kind` + `index`: which interactable component on that object (an `IInteractable`, or an older `Interactable` /
  `Storage` used through `LegacyInteractableTarget`).

The server (`NetInteractor.ServerInteract`, rate limited) resolves the target on its side, refuses it unless it is in the
player's scene, enabled and not the player itself, checks the hold time, then calls
`PlayerInteractor.InteractFromRequest` on **its** copy of the player: alive, range from the server's positions (plus
`RangeTolerance` = 1 m for latency), line of sight and `CanInteract` - then `Interact(serverInteractor)`. A refusal
reason is shown to the player through the inventory's message line.

So **any `IInteractable` on a networked object or in a scene works networked with no extra code**: hub world gates,
dungeon objects, `DuelChallengeTarget` on players (relayed to the server, where it challenges), your own objects. Write
`Interact` as server code (it runs on the server for clients' players, and locally offline / for the host).

- **Holds**: `PlayerInteractor` tells the relay when a hold starts and stops (`HoldChanged`); the server records the start
  and accepts the interaction only when `HoldDuration - 0.25 s` has passed. `Player.OnInteract` (older input path for
  doors, levers, shrines) uses `Player.NetInteractHandler`, installed by `NetInteractor`; it cannot report the start of a
  hold, so for those the server times the hold itself from the moment the request arrives.
- **Older `Interactable` components** (doors, levers, shrines, dungeon elements) take one route whether they are used
  through `Player.OnInteract` or through `PlayerInteractor` (Handle Legacy Interactables): `Player.NetInteractHandler`.
  Other modules put their own relay in front of it and chain to the previous handler - the dungeon sends its elements
  by element id (see 10) - and the chain ends with `NetInteractor`'s generic relay.
- **Items on the ground and storages** are not relayed: the inventory asks the server itself (see 07).
- **Targets that cannot be named** (objects only this machine created) are used locally as before.
- **Doors** (`DoorController`): the server toggles them; with a `NetworkObject` + `NetInteractable`, `IReplicatedInteractable`
  sends the open/closed state to every client (late joiners included). A client's copy of a networked door ignores direct
  `Interact()` calls. A door (any `IReplicatedInteractable`) without them changes on the server only: the client logs a
  warning naming it the first time it is used.
- **Replicated state** for your own interactables: implement `IReplicatedInteractable` (`ReplicatedState` read on the
  server, `ApplyReplicatedState` on clients) and put `NetInteractable` + `NetworkObject` on the root.
- Non-owner copies of a player (and every player on a dedicated server) do not target or read input.

## Results shown on the interacting player's screen (NPCs)

The pattern for interactions whose result is a window: **the server decides, a target RPC opens the window with the
server's data on the owner's machine, and the window sends requests**. NPCs use it:

1. The relayed interaction runs `NPC.Interact(serverInteractor)` on the server. The server's session
   (`NPCInteractionSession.IsRemote`) counts for "Busy", the cooldown, `onConversationStarted` / `onConversationEnded`,
   and ends when the player walks away, dies or leaves.
2. Instead of opening windows on the server, the NPC calls `NPC.NetRelay` (`INPCSessionRelay`):
   `ShowMenu(session, greeting)`, `OpenBehaviour(session, behaviour)`, `SessionEnded(session, reason)`.
   `NetInteractor` sends them to the owner (`TargetNpcMenu`, `TargetNpcBehaviour`, `TargetNpcEnded`), naming the NPC by an
   `InteractTargetRef` and the option by its index in `NPC.Behaviours`.
3. The owner's machine shows a **mirrored session** (`NPC.ShowServerSession`, `IsMirror`): the normal windows, the intro
   sound and animation, facing. Choosing an option, finishing one (reading the lines, closing the shop) and closing the
   conversation go back as requests (`ServerNpcPick`, `ServerNpcFinished`, `ServerNpcClose`); the server checks the session
   exists and the player is still in range, then opens the next thing or ends it. Gameplay events (UnityEvents such as
   `NPCDialogue.onFinished`) run on the server only (`NPCBehaviour.FinishedRemote`).

Custom NPC behaviours: `Begin` runs on the player's machine (as before offline). Keep gameplay results out of it when
`session.IsMirror`, and do them on the server in `BeginRemote` / `FinishedRemote` / `EndRemote`.

## Currency

`CurrencyWallet` on the server is the truth. A client's copy of a networked player's wallet (`IsReplica`) refuses
`TrySpend`, `TryAdd` and `SetBalance` (with one warning). `NetWallet` writes every number-currency balance into an
owner-only `SyncDictionary<string,int>` keyed by `CurrencyDefinition.NetworkKey` (the asset name) - other players never
learn it - and the owner shows it with `CurrencyWallet.ApplyReplicatedBalance` (raising `BalanceChanged`, so the shop
window and HUDs follow). Currencies with a Backing Item are items: they follow the replicated inventory. Hub world
cycle penalties spend currency on the server, which is authoritative there.

## Merchants

- **Stock**: the server owns each merchant's `MerchantInventory` (shared by all players) and restocks it (`CheckRestock`
  runs only where `NetAuthority.IsServer`). When a remote player opens the shop, and after every change of the stock
  (purchases by anyone, sales, restocks; at most every `stockSendInterval`), `NetInteractor` sends that player
  `MerchantSlotState`s: the server's line ids, item ids (`ItemDatabase`), quantities, origin and **the server's price for
  that player**. The client applies them with `Merchant.ApplyServerStock`; the window shows those prices
  (`MerchantStockSlot.ServerUnitPrice`).
- **Buy / sell**: on clients `Merchant.GlobalProcessor` is the `NetInteractor`, and it wins over a processor set on a
  single merchant (`Merchant.Processor`): only the server performs transactions. A custom processor therefore runs only
  where the transaction is performed by this machine (offline, the host's own player); remote players' requests run
  `MerchantTransactions.Execute` on the server. A request carries the merchant NPC, the
  Merchant's option index, the line id (buy) or the bag/hotbar slot (sell), the item id, the quantity and the total the
  player saw. The server checks: rate limit, alive, quantity 1..9999, the player has this shop open
  (`session.ActiveBehaviour`), distance to the merchant (session range + tolerance), the line still sells that item / the
  slot still holds that item, then runs `MerchantTransactions.Execute` with **its own** stock, price, wallet and inventory
  space. The client's total is only used to refuse a changed price (`PriceChanged`), never as the price. The result goes
  back to the waiting window (`TargetMerchantResult`); requests unanswered after 6 s fail. The inventory module sends the
  owner its new inventory, `NetWallet` the new balance.

## Player-to-player trading

- **Rules** (`TradeBook`, unit tested): one trade per player; a request the other player accepts or declines (asking
  someone who already asked you accepts); offers of item stacks by inventory slot (bag or hotbar, with the expected
  item id and a quantity) plus money; each side locks its offer; when both are locked, both confirm the same
  `Revision`; any change of an offer unlocks both sides and clears both confirmations; either side cancels; requests
  expire after 30 s, open trades after 180 s without a change; players must stay alive, in the same realm scene and
  within 8 m.
- **Server** (`NetTrading`): every request but a cancel is rate limited (a cancel is never lost); the caller is always
  the connection's own player. Offers are
  checked against the server's inventory and wallet (`TradeExchange.Validate`); locked offers are re-checked four times
  a second (an offered stack moved or used up unlocks both sides). When both confirm, `TradeExchange.Execute` swaps
  **atomically**: everything is checked first; the offered items leave both players (durabilities kept), both pay, room
  is checked per kind of item, each receives the other's items and money; if any step fails, every step is undone in
  reverse order and items that cannot go back into a bag are dropped at their owner's feet - never duplicated, never lost.
- **Replication**: each player receives only their own trade (`TradeView` via a target RPC, plus messages).
- **UI** (`TradeWindow`, built at runtime): a request prompt (Accept with the trade key T or a click, Decline / Cancel) and
  the trade window (your stacks - click to offer the whole stack; your offer - click to take back; money; the other
  side's offer; lock / confirm states; Lock, Confirm, Cancel). It registers as an open panel on the inventory (free
  cursor, Escape cancels). Turn off `createWindow` and use `NetTrading.View`, `ViewChanged`, `RequestTrade`, `Respond`,
  `SendOffer`, `SetLocked`, `Confirm` and `Cancel` for your own UI.
- **Starting**: press T while looking at another player (the interactor's focus, e.g. their duel target) or near one
  in front of you. Offline single player there is nobody to trade with: trading is unavailable.

## Prefab and scene setup

- Player prefab: `NetInteractor` and `NetWallet` next to the `NetworkObject` (Tools ▸ SimpleMovements ▸ Network ▸ Make
  Selected Prefab Networked ▸ Player adds them). The player needs its `PlayerInteractor` (NPC ▸ Add Player Interactor To
  Players).
- Interactables whose state everyone must see, or that should be addressable by reference (doors, merchant NPCs,
  gates): Make Selected Prefab Networked ▸ Interactable (`NetworkObject` + `NetInteractable`). NPCs and objects placed
  in a scene without a NetworkObject still work through their scene path (but replicate no state).
- Bootstrap scene: `NetTrading` on the "Network World" object (Create Bootstrap Scene adds it).
- Build the Item Database (Tools ▸ SimpleMovements ▸ Project Setup ▸ Build Item Database): shop lines, sales and trades name
  items by its ids; items without an id cannot be sold, bought or traded in a session.
- Keep currency asset names unique and identical in every build (`NetworkKey`).

## Testing locally

1. Start a host and a client on 127.0.0.1 (see 01). Walk both players to a merchant NPC in the City.
2. On the client, press E on the merchant: the shop opens with the server's stock. Buy an item: the gold goes down, the
   item appears, the line's quantity drops - and drops in the host's window too if it is open. Sell it back; it appears
   as a buy-back line for both.
3. Walk away while the shop is open: it closes on both machines. Try a limited item until it is sold out on both.
4. Open and close a networked door on the client: the host sees it move; a client joining later sees it open.
5. Trading: on the client look at the host's player and press T; on the host press T (or click Accept). Offer stacks and
   money on both sides, lock, confirm. Change an offer after locking: both sides unlock. Fill one player's bag and try to
   receive more than fits: the trade is refused and nothing moves.
6. Edit Mode tests: `GameNet.Tests.EconomyNetTests` (trade rules, views, interaction paths, merchant stock replication).

## Limitations

- Doors and other replicated interactables placed without a `NetworkObject` + `NetInteractable` open only on the
  server (a warning names them); give them both, scene objects of the City and World included (dungeon elements are
  replicated by the dungeon module instead).
- NPC facing and intro animations are local presentation: each machine turns the NPC towards its own player.
- An NPC without a `NetworkObject` replicates no "Busy" count: a client may see it as available and get the server's
  "Busy" message.
- Held interactions started through `Player.OnInteract` wait for the hold twice (once on the client, once timed by the
  server), because that input path cannot report the start of a hold.
- Custom price modifiers that depend on the player are evaluated on the server and shown as sent; sell prices in the
  window are computed on the client from the replicated inventory (the server's price is what is paid).
- Trading offers whole stacks from the built-in window (the protocol supports partial quantities).
- If an undo step of a failed trade cannot remove an item it just gave (extremely unlikely: it was added a moment
  before), an error is logged; counts are restored, but the exact units' durabilities may differ.
