# 13 - How the network code works (a guided tour)

Read page 00 first for the words (server, client, RPC, SyncVar, tick). This page walks through the code itself:

* which scripts are in charge;
* which script handles each part of the game;
* the order things happen in;
* the real class and method names, so you can open the file and follow along.

---

## 1. The three layers

The code is organised in three layers. A higher layer never calls into a lower one directly; it asks the layer in
between.

```
┌──────────────────────────────────────────────────────────────────────────┐
│ 1. GAMEPLAY (the single-player game, no Fish-Net inside)                 │
│    MovementStateMachine, InventoryManager, WeaponController,             │
│    AbilityCaster, CombatEntity / status managers, Mob, PlayerInteractor, │
│    DungeonSession, HubWorldDirector, EndlessTerrain...                   │
└───────────────▲──────────────────────────────────────────────────────────┘
                │ asks questions / sends through a "bridge"
┌───────────────┴──────────────────────────────────────────────────────────┐
│ 2. CORE BRIDGES  Networking/Core  (always compiled, no Fish-Net)         │
│    NetAuthority  NetSpawn  NetScenes  LagCompensation  IControlLock      │
│    + bridge interfaces declared next to the gameplay code:               │
│    IInventoryNetBridge  IWeaponNetBridge  IAbilityNetBridge ...          │
└───────────────▲──────────────────────────────────────────────────────────┘
                │ implemented / installed by
┌───────────────┴──────────────────────────────────────────────────────────┐
│ 3. FISH-NET LAYER  Networking/Runtime  (#if FISHNET)                     │
│    NetSession  NetBootstrap  NetRealmScenes  NetPlayerSpawner            │
│    NetPlayer  NetPlayerMovement  NetInventory  NetWeaponCombat           │
│    NetAbilityCaster  NetCombatStatus  NetMob  NetInteractor ...          │
└──────────────────────────────────────────────────────────────────────────┘
```

**Why it is built this way.** Without Fish-Net, layer 3 does not exist. Layer 2 then answers "offline": `IsServer` is
true and no bridge is installed, and the game runs exactly as in single player. With Fish-Net, layer 3 plugs into
layer 2 and the same gameplay code becomes multiplayer.

### The bridge pattern (the key idea)

Take the inventory. `InventoryManager` (gameplay) has a property:

```csharp
// Inventory/InventoryManager/InventoryManager.Network.cs
public interface IInventoryNetBridge
{
    void Send(InventoryManager inventory, InventoryRequest request);        // "please do this to my inventory"
    void SendWorld(InventoryManager inventory, InventoryRequest request, GameObject target); // pick up / open storage
}
public IInventoryNetBridge NetBridge { get; set; }
```

* **Offline:** `NetBridge` is null. When you drag an item, `InventoryManager` moves it itself.
* **Network client:** `NetInventory` (layer 3) sets `inventory.NetBridge = this` in `SetBridge(true)`. When you drag
  an item, `InventoryManager` calls `NetBridge.Send(...)`. `NetInventory` sends it to the server with a ServerRpc
  (`CmdInventoryRequest`), and the item does not move yet.
* **Server:** receives `CmdInventoryRequest`, runs the same `InventoryManager` code on its own copy, then sends the
  result back to the owner (`TargetInventorySnapshot`). The owner's client shows it.

Weapons (`WeaponController.NetBridge`, `IWeaponNetBridge`) and abilities (`AbilityCaster.NetBridge`,
`IAbilityNetBridge`) work the same way. Movement uses a different technique (prediction, section 5).

## 2. The main controllers (who is in charge)

These scripts sit on the **NetworkManager** in the Bootstrap scene. They run the session; they do not handle any one
player.

| Script | Its job in one sentence | Key methods |
|---|---|---|
| `NetSession` | Keeps `NetAuthority.Mode` correct and is the doorway to Fish-Net for everyone else. | `Awake` → `InstallResolvers()` (teaches `NetAuthority` how to tell if an object is networked/owned); `RefreshMode()` on every server/client start or stop; events `ClientJoined`, `ClientLeft` (`OnRemoteState`). Helpers `NetObjects.Of(...)`, `OwnerOf(entity)`, `PlayerOf(connection)`. |
| `NetBootstrap` | Starts the game in a mode (menu or command line). | `Start()` reads `NetLaunchOptions`; `Launch(NetMode)` starts Fish-Net server/client (or loads the City for single player); `Disconnect()`; `OnGUI()` draws the menu and the F9 status line. |
| `NetRealmScenes` | Loads scenes over the network and moves players between them. | `LoadCityOnServer()`; `OnClientLoadedStartScenes()` / `OnPresenceChanged()` raise `ClientEnteredCity`; `MoveToScene(root, scene)`; `Load(...)` / `Unload(...)`; `RemoveOfflinePlayers()`. |
| `NetPlayerSpawner` | Creates each player's character. | `OnClientEnteredCity(conn)`: instantiates `playerPrefab`, moves it into the City, spawns it owned by `conn`. |
| `NetLagCompensation` | Remembers where everything was, for fair hit checks. | Records every tick; used through `LagCompensation.For(attacker)`. |

On the **Network World** object, five global relays handle things that belong to no single player: `NetHubWorld`
(cycle timer), `NetDuels`, `NetTrading`, `NetWorldChunks` (terrain changes and weather), `NetSocial` (parties, guilds,
inspection).

## 3. The scripts per part of the game

| Part of the game | Gameplay script (layer 1) | Network script (layer 3) | Lives on |
|---|---|---|---|
| Player identity, camera, input on/off | `PlayerLocator`, camera scripts | `NetPlayer` | Player prefab root |
| Movement | `MovementStateMachine` | `NetPlayerMovement` (+ `CameraTargetSmoother`) | Player root |
| Inventory and equipment | `InventoryManager` | `NetInventory` | Player root |
| Items on the ground | `ItemPickable` | `NetWorldItem` | World item prefabs |
| Money | `CurrencyWallet` | `NetWallet` | Player root |
| Melee, ranged, block | `WeaponController`, `BlockController` | `NetWeaponCombat` | Player root |
| Abilities and spells | `AbilityCaster` | `NetAbilityCaster` | Player, mob and summon roots |
| Health, mana, buffs, XP, death | `CombatEntity` + status managers | `NetCombatStatus` | Player and mob roots |
| Mobs | `Mob` (AI) | `NetMob` | Mob prefab root |
| Doors, chests, NPCs, merchants | `PlayerInteractor`, `Interactable`, `NPC`, `Merchant` | `NetInteractor` (player), `NetInteractable` (object) | Player root / object root |
| Realm (City, World, Dungeon) | `HubWorldDirector`, `PlayerRealmState` | `NetPlayerRealm`, `NetHubWorld` | Player root / Network World |
| Trading between players | `TradeBook` (core) | `NetTrading`, `TradeWindow` | Network World |
| Dungeons | `DungeonSession`, `DungeonManager` | `NetDungeonRun` | DungeonInstance scene |
| World terrain | `EndlessTerrain`, `WeatherSystem` | `NetWorldChunks`, `WorldTerrainNet` | Network World |

## 4. What happens from Play to "I can move"

Follow this with the files open. "S" = runs on the server, "C" = on the joining client.

```
1. NetBootstrap.Start()                          reads command line, or shows the menu
2. NetBootstrap.Launch(NetMode.Host)             starts Fish-Net server + client
3. NetSession.RefreshMode()           S,C       NetAuthority.Mode = Host
4. NetRealmScenes.OnModeChanged → LoadCityOnServer()   S   City scene loaded through Fish-Net
5. client connects; NetSession.OnRemoteState    S   ClientJoined
6. NetRealmScenes makes the client load the City       C   (Fish-Net SceneManager)
7. NetRealmScenes.OnPresenceChanged             S   ClientEnteredCity(conn)
8. NetPlayerSpawner.OnClientEnteredCity(conn)   S   Instantiate(playerPrefab) → ServerManager.Spawn(..., conn)
9. Fish-Net creates the player on every client that observes the City
10. Each NetworkBehaviour on the player gets its callbacks (section 6):
      NetPlayer.OnStartClient / OnOwnershipClient   C  owner: camera, input, UI ON; others: OFF
      NetPlayerMovement.OnStartNetwork              S,C  ExternallyDriven = true, tick callbacks on
      NetInventory.OnStartServer / OnStartClient    S,C  bridge installed on the owner, snapshot asked
      NetCombatStatus, NetAbilityCaster, ...        S,C  same idea
11. Every tick: NetPlayerMovement.TimeManager_OnTick → PerformReplicate(...)   the player moves
```

## 5. Movement, step by step (`NetPlayerMovement`)

Fish-Net's prediction runs two special methods on every tick:

* **`[Replicate] PerformReplicate(MoveData md, ...)`** runs one movement step with one input.
* **`[Reconcile] PerformReconcile(MoveReconcile rd, ...)`** puts the player back to a state the server sent.

```
OWNER CLIENT, every tick                         SERVER, every tick
TimeManager_OnTick()                             TimeManager_OnTick()
  md = BuildMoveData()        ← reads keys        PerformReplicate(md from the owner)
       (machine.ReadDeviceInput())                  input.Sanitized()          ← never trusts numbers
  PerformReplicate(md)        ← predicts at once    machine.Simulate(input, TickDelta)
    machine.Simulate(input, TickDelta)           TimeManager_OnPostTick()
  Fish-Net sends md to the server                  CreateReconcile()
                                                     PerformReconcile(new MoveReconcile(machine.Capture()))
                                                     → sent to clients
OWNER CLIENT, when a reconcile arrives
  PerformReconcile(rd)  → machine.Restore(snapshot)   back to the server's truth
  Fish-Net replays every input sent since then: PerformReplicate(..., state = replayed)
                                                     → machine.IsReplaying = true (no sounds, no events)
```

* `MovementStateMachine.Simulate` (gameplay) is the only place movement happens. `Capture()` and `Restore()` save
  and load everything it changes (`MovementSnapshot`).
* Other players' clients also call `PerformReplicate` with the inputs they receive. When an input is late, they guess
  from the last real one (`spectatorFutureTicks`).
* Smoothing is only visual: the model or body is drawn between ticks (`SetupBodySmoothing`, `ShowBodySmoothed`,
  `CameraTargetSmoother`). Teleports snap (`NotifyTeleported` → `SnapAfterTeleport`).

## 6. The life of a NetworkBehaviour (callback order)

Every `Net...` component is a `NetworkBehaviour`. Fish-Net calls these methods; this is where the scripts switch
things on and off:

| Callback | When | Typical use in this package |
|---|---|---|
| `Awake()` | Unity, before the network | Find the gameplay component (`Resolve()`). |
| `OnStartNetwork()` | Object exists on the network (server and client) | `NetPlayerMovement`: take over the movement machine. |
| `OnStartServer()` | On the server | Subscribe to gameplay events (`HookServer(true)`): "health changed", "item moved"... |
| `OnStartClient()` | On each client | Subscribe to SyncVar changes; mark the gameplay copy as a replica. |
| `OnOwnershipClient(prev)` | When this machine becomes (or stops being) the owner | Install the bridge on the owner only (`SetBridge`), turn on input and UI (`NetPlayer.ApplyPresentation`). |
| `TimeManager_OnTick()` | Every network tick | Movement, polling server state (`NetCombatStatus.PollServer`, `NetMob.PollServer`). |
| `OnStopClient()` / `OnStopServer()` / `OnStopNetwork()` | Object removed or session ended | Undo everything (unhook, remove bridge). |

## 7. How each system talks: the RPCs

Read the arrows as "who sends to whom". **Cmd/Server…** = client → server (`[ServerRpc]`). **Rpc…/Observers…** = server
→ everyone who sees the object (`[ObserversRpc]`). **Target…** = server → one client (`[TargetRpc]`).

### Inventory (`NetInventory`)

| Method | Direction | What |
|---|---|---|
| `CmdInventoryRequest(InventoryRequest)` | owner → server | Move, split, equip, use, hotbar select... (numbered with a sequence) |
| `CmdInventoryWorldRequest(request, target)` | owner → server | Pick up a world item, open a storage |
| `ServerPickup(go, itemId)`, `ServerOpenStorage(go)` | server only | Check range and space, then do it |
| `TargetInventorySnapshot(conn, InventorySnapshot)` | server → owner | The whole inventory after a change (`SendSnapshot`) |
| `TargetInventoryMessage(conn, text, kind)` | server → owner | "Bag full" and other feedback |
| `RpcSceneItemTaken(path, remaining)` | server → everyone | An item placed in a scene was taken |
| SyncVar `appearance` → `OnAppearanceChanged` | server → everyone | What other players see you wearing and holding |

### Weapons (`NetWeaponCombat`)

| Method | Direction | What |
|---|---|---|
| `CmdWeaponInput(NetWeaponInput)` | owner → server | Attack pressed/released/tapped, reload, drop charge |
| `CmdWeaponAim(origin, direction, viewTick, ...)` | owner → server | Aim ray and the tick the player was seeing (lag compensation) |
| `CmdQuickThrow`, `CmdBlockIntent(bool)` | owner → server | Throw, raise or lower the block |
| `ServerUpdate()` | server only | Runs the real `WeaponController` with lag compensation |
| `RpcWeaponCharge/Attack/Stopped/Shot/Hit/Impact/Reload`, `RpcShieldBash` | server → everyone | Show what happened (animations, projectiles, impacts) |
| `RpcWeaponFailed(conn, input, reason)` | server → owner | Why an attack was refused |

### Abilities (`NetAbilityCaster`)

| Method | Direction | What |
|---|---|---|
| `CmdCast(NetAbilityCastRequest)` | owner → server | Cast slot N, aimed here |
| `CmdAim(...)`, `CmdCancelCasting()` | owner → server | Update aim while channelling, cancel |
| `OnServerCastStarted/Released/Interrupted` | server only | Gameplay events from the server's `AbilityCaster` |
| `RpcCastStart`, `RpcCastRelease`, `RpcCastInterrupted`, `RpcCastAim`, `RpcInstantCast` | server → everyone | Replay the cast visually ("mirrors") |
| `RpcCastRejected(conn, slot, seq, reason)`, `RpcSlotState(conn, state)` | server → owner | Refusals, cooldowns |
| SyncList `contents` → `OnContentsChanged` | server → everyone | Which ability is in which slot |

### Health and status (`NetCombatStatus`)

Mostly SyncVars: the server writes, every client applies.

| Piece | Direction | What |
|---|---|---|
| `OnServerHit`, `OnServerHeal`, `OnServerDied`, `OnServerRevived`, `PollServer()` | server only | Read the server's `CombatEntity` and write the SyncVars |
| SyncVars `health`, `dead`, `control`, `speed`, `weight`, `level`... → `OnHealthChanged`, `OnDeadChanged`... → `ApplyHealth`, `ApplyControl`... | server → clients | Clients copy the values into their replica |
| Owner-only SyncVars (`mana`, `staminaLimits`, `hunger`, `thirst`, `oxygen`... `survival`, `progress`) → `ApplyOwnerResources`, `ApplySurvival`, `ApplyProgress` | server → owner | Only the owner needs them |
| SyncLists `buffs`, `traits` → `OnBuffsChanged`, `OnTraitsChanged` | server → clients | Buff icons, movement traits |
| `ServerUpgradeStat(stat)`, `ServerSetSleeping(bool)` | owner → server | Spend a stat point, sleep |
| `RespawnRoutine()`, `RespawnNow()` | server only | Death and respawn |

### Mobs (`NetMob`)

The AI runs only on the server. `PollServer()` writes SyncVars (`state`, `target`, `identity`). Clients apply them in
`ApplyState`, `ApplyTarget` and `ApplyIdentity`, and play animation events through `ObserversAnimationEvent`. The
position travels with Fish-Net's `NetworkTransform`.

### Interaction, NPCs and merchants (`NetInteractor`)

| Method | Direction | What |
|---|---|---|
| `ServerInteract(InteractTargetRef)`, `ServerHold(target, holding)` | owner → server | "I interact with this door/chest/NPC" (the target is named by its NetworkObject plus a path) |
| `ResolveUsable`, `Use` | server only | Find the object, check alive, scene, range and hold time, then run it |
| `ServerNpcPick`, `ServerNpcFinished`, `ServerNpcClose`, `ServerMerchantTrade` | owner → server | NPC menu choices, buy and sell |
| `TargetNpcMenu`, `TargetNpcBehaviour`, `TargetNpcEnded`, `TargetMerchantStock`, `TargetMerchantResult` | server → owner | Open menus, shop stock, purchase results |

### Scenes and players (`NetRealmScenes`, `NetPlayer`)

* `NetScenes.MoveToScene(player, scene)` (core) → `NetRealmScenes.MoveToScene`. The server moves the object at once;
  the owner's client loads the destination, follows, then unloads what it left (`LeaveScenes`).
* `NetPlayer.ServerSetName(name)` (owner → server) → `CleanName` → SyncVar `DisplayName` → `OnNameChanged` on
  everyone.

## 8. How to read any `Net...` script

They all follow the same template. Open one and look for these parts in this order:

1. **Fields and SyncVars** at the top: what state is shared.
2. **`Resolve()`**: which gameplay component it networks.
3. **`OnStartServer` / `HookServer(true)`**: which gameplay events the server listens to.
4. **`OnStartClient` / `OnOwnershipClient`**: what a client switches on (bridge for the owner, replica mode for others).
5. **`[ServerRpc]` methods (Cmd…/Server…)**: what a client may ask. Each one starts with checks (owner? alive?
   allowed?) before doing anything.
6. **`ServerUpdate` / `PollServer` / tick methods**: what the server sends regularly.
7. **`[ObserversRpc]` / `[TargetRpc]` methods (Rpc…/Target…)** and **`On…Changed`** SyncVar callbacks: how clients show
   the result.

## 9. Example: adding your own networked feature

Suppose a "Torch" component with `bool lit`, which every player should see.

```csharp
#if FISHNET
using FishNet.Object;
using FishNet.Object.Synchronizing;

// Put on the torch prefab root, next to a NetworkObject (and register the prefab with Fish-Net).
public class NetTorch : NetworkBehaviour
{
    private readonly SyncVar<bool> lit = new SyncVar<bool>();      // server writes, clients receive
    private Torch torch;                                         // the gameplay component

    private void Awake()
    {
        torch = GetComponent<Torch>();
        lit.OnChange += (prev, next, asServer) => torch.SetLit(next);   // every machine shows it
    }

    // A player asks. RequireOwnership = false: any player may ask, the server decides.
    [ServerRpc(RequireOwnership = false)]
    public void ServerToggle()
    {
        if (!GameNet.NetAuthority.IsServer) return;
        lit.Value = !lit.Value;                                  // checks (distance, permissions) go here
    }
}
#endif
```

The gameplay code stays offline-safe: call `torch.Toggle()` offline, and `netTorch.ServerToggle()` when
`GameNet.NetAuthority.IsNetworked`. Most of the package's real interactables go through `NetInteractor` and
`IReplicatedInteractable` instead, so look at `NetInteractable` before writing a new relay.

## 10. Where to go next

* Movement in depth: 02. Abilities: 03. Status: 04. Weapons and lag compensation: 05. Mobs: 06.
* Inventory: 07. Interaction and trading: 08. Hub world: 09. Dungeons: 10. Terrain: 11.
* Every script and the menu that adds it: 12.
