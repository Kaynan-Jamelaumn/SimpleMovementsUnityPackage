# 12 - Script and component reference

Every networking script in the package: where it goes, who runs it and what it does. "Added by" names the menu under
**Tools ▸ SimpleMovements ▸ Network** that puts it in place, so you rarely add these by hand.

* **Core** scripts (`Networking/Core`, namespace `GameNet`) are always compiled and contain no Fish-Net code. Gameplay
  code talks to them.
* **Runtime** scripts (`Networking/Runtime`) are compiled only with Fish-Net installed (`#if FISHNET`). They connect
  the core helpers to Fish-Net.

---

## 1. On the NetworkManager (Bootstrap scene)

Added by **Create Bootstrap Scene**.

| Component | Runs on | What it does |
|---|---|---|
| `NetworkManager`, `Tugboat`, `TimeManager`, `ServerManager`, `ClientManager`, `SceneManager`, `PredictionManager`, `ObserverManager` | All | Fish-Net itself. Tugboat listens on port 7770. The ObserverManager's default condition is `RealmObserverCondition`. |
| `NetSession` | All | Sets `NetAuthority.Mode` (Offline / Host / Server / Client) when Fish-Net starts or stops, and installs the lookups the core helpers use (is this object networked, owned here, its id). Raises client joined/left events on the server. |
| `NetBootstrap` | All | Start menu and command line. Fields: *Offline Scene* (empty = the City), *Default Address* (`127.0.0.1`), *Port*, *Max Players*, *Show Menu*, *Show Status* (F9). `Launch(mode)` and `Disconnect()` can be called from your own UI. |
| `NetRealmScenes` | Server, owner client | Loads the City on the server and for each joining client; moves players between City, World and dungeon instance scenes (`NetScenes.MoveToScene`); the client loads the destination and unloads what it left. Removes single-player players placed in scenes. *City Scene Name* (empty = Hub World Settings). |
| `NetPlayerSpawner` | Server | Spawns the **Player Prefab** for each client once it is in the City, owned by that client. Use it instead of Fish-Net's PlayerSpawner. |
| `NetLagCompensation` | Server | Records every `LagCompensated` object's position each tick and rewinds them during a player's hit checks. |
| `HeadlessServer` | Dedicated server | Caps the frame rate to the tick rate, mutes audio, logs a status line. |
| `NetSpawnProvider` | Server | Not a component: installed by `NetSession`. Network-spawns objects passed to `NetSpawn.Spawn` in the scene they were created in. |
| `RealmObserverCondition` (asset) | Server | Who sees what: same scene only; in the World within *World Distance* (140 m) with a small hysteresis; owners always see their objects. Options to apply the distance in the City or dungeons too. |

## 2. On the "Network World" object (Bootstrap scene)

A scene NetworkObject that every client observes (its observer ignores the conditions). It carries the global relays.
Added by **Create Bootstrap Scene**.

| Component | What it does |
|---|---|
| `NetHubWorld` | Sends the server's dimensional cycle (timer, warnings, jump) to every client, and hub messages to the players they are for. Finds the `HubWorldDirector` by itself. |
| `NetDuels` | Duel requests (challenge, accept, decline, forfeit) and the open duels. The server's `DuelSystem` decides. |
| `NetTrading` | Player-to-player trading: request (T key), offers, lock, confirm. The server swaps items and money when both confirm. Builds the `TradeWindow` UI on clients. |
| `NetWorldChunks` | The open world's shared state: terrain objects removed on the server (chopped trees, mined rocks) and the weather clock. |
| `NetSocial` | Parties (the server's `PartySystem` decides; every client gets every party, so all machines know who is in whose party), every player's guild, alliance, fame, title and level, and inspection (the server builds the answer from its copy and sends it to the asker only). See Player 09. Added to old Bootstrap scenes by **Add Missing Relays To Network World**. |

## 3. On the player prefab root

Added by **Make Selected Prefab Networked ▸ Player**, next to a `NetworkObject` (with prediction settings).

| Component | What it does |
|---|---|
| `NetPlayer` | The player's identity. On the owner's machine: turns on camera, input, UI, audio listener and sets `PlayerLocator.Local`. On every other copy (and on a dedicated server): turns them off. Carries the player name (`-name`). Lists: *Owner Only* components, *Owner Only Objects*, *Others Only Objects* (e.g. a body hidden from your own first-person camera). |
| `NetPlayerMovement` | Predicted movement: drives `MovementStateMachine.Simulate` on the tick, sends inputs, reconciles to the server's snapshots, snaps on teleports. Options: *Smooth Camera Targets*, *Smooth Body*. See page 02. |
| `CameraTargetSmoother` | Added at runtime by `NetPlayerMovement`. Gives Cinemachine a smoothly moving target, so the camera does not step 30 times a second. |
| `NetAbilityCaster` | Cast, aim and cancel go to the server; the server validates and casts; every client replays the visuals. See page 03. |
| `NetWeaponCombat` | Weapon attacks, reload, throws, block. The server runs the weapon code with lag compensation; clients show it. See page 05. |
| `NetCombatStatus` | Health, death, crowd control, buffs, speed, weight to everyone who sees the player; mana, needs, XP and points to the owner only. See page 04. |
| `NetInventory` | The server's inventory is the truth. Every action is a request; the owner receives the whole inventory after changes. See page 07. |
| `NetWallet` | Money. Balances go to the owner only. |
| `NetCardTable` | Card tables (on the table, with a NetworkObject): the server runs the table and the game; each seated player gets their own view (their hand), everyone else the onlookers' view; seated players' actions go to the server. See [Minigames 01](../Minigames/01-Card-Games-and-Truco.md#5-multiplayer). |
| `NetCraftStation` | Crafting stations (on the station, with a NetworkObject): the server runs the workshop, takes materials, gives items and keeps the journal; every client follows a copy of the workshop; the working player's actions go to the server, bounded by a `CraftRequestGuard`. See [Crafting 08](../Crafting/08-Multiplayer.md). |
| `NetAlchemyStation` | Alchemy stations, the same way (`AlchemyRequestGuard`). See [Alchemy 06](../Alchemy/06-Scripting-and-Extending.md#8-networking). |
| `NetInjuries` | Injuries (when the injury system is on): the server's body-part condition, injuries, missing limbs and the treatment being applied, to everyone who sees the player; the owner's treatments go to the server as requests. See [Player 13](../Player/13-Injuries-and-Treatment.md#9-multiplayer). |
| `NetPlayerRealm` | The player's realm (City, World, Dungeon, travelling), zone and PvP flag. |
| `NetInteractor` | Interactions (talk, open, pick up, use) are sent to the server, which checks range and timing and runs them. See page 08. |

## 4. On other prefabs

| Prefab | Menu | Components |
|---|---|---|
| Mob | *Make All Mob Prefabs Networked* | `NetworkObject`, `NetMob` (AI runs on the server only), `NetCombatStatus`, `NetAbilityCaster`, `NetworkTransform` (mobs are not predicted). |
| Summon | *Make Selected Prefab Networked ▸ Summon* | Same as a mob. |
| Pickup or loot drop | *▸ Pickup or Loot Drop* | `NetworkObject`, `NetWorldItem` (which item, how many, uses left), `NetworkTransform`. |
| Ability pickup | *▸ Ability Pickup* | `NetworkObject`. |
| World portal | *▸ World Portal* | `NetworkObject`. The `Portal` reacts only on the server. |
| Interactable (merchant, chest, door, gate) | *▸ Interactable* | `NetworkObject`, `NetInteractable` (replicates its open/locked state). |
| Crafting station | *▸ Crafting Station* (or the station inspector's *Make Networked*) | `NetworkObject`, `NetCraftStation`. |
| Alchemy station | *▸ Alchemy Station* (or the station inspector's *Make Networked*) | `NetworkObject`, `NetAlchemyStation`. |
| `Resources/SimpleMovements/Networking/NetWorldItem.prefab` | *Create World Item Prefab* (also made by Create Bootstrap Scene) | The generic carrier used when an item has no networked world prefab of its own. |

## 5. In the DungeonInstance scene

Added by **Create Dungeon Instance Scene**.

| Component | What it does |
|---|---|
| `DungeonInstanceSceneMarker` | Marks the template scene; one copy is stacked per dungeon run. |
| `NetworkObject` + `NetDungeonRun` | The relay of one run. Server: sends the header and floors as players reach them (never the seed), element states, holds a new player's controls until their client built the floor. Client: builds a presentation-only copy of the dungeon. See page 10. |

## 6. Core helpers (always compiled)

| Script | What it is for |
|---|---|
| `NetAuthority` | The mode and questions every script asks: `IsNetworked`, `IsServer`, `IsClientOnly`, `HasLocalPlayer`, `IsHeadless`, `IsNetworkedObject(go)`, `RunsRules(go)`, `IsLocallyControlled(go)`. |
| `NetSpawn` | `Spawn(go)` after Instantiate, `Destroy(go)`, `ShouldSpawnHere` (true offline and on the server). |
| `NetScenes` | Load and unload realm and dungeon scenes, move a player to another scene. |
| `LagCompensation` | `using (LagCompensation.For(attacker)) { ... hit query ... }`. Does nothing offline. |
| `IControlLock` | Freezes a networked player without disabling components (travel, dungeon loading). |
| `NetLaunchOptions` | Parses the command line (`-server`, `-host`, `-client`, `-offline`, `-address`, `-port`, `-maxplayers`, `-name`, `-tickrate`, `-shard`). |
| `MobNetSpawn` | How mob spawners hand new mobs to the network (moved into the spawner's scene, then spawned). |
| `WorldTerrainNet` | Streams the terrain around the right players during a session, and spawns world objects (portals) from chunk spawners. |
| `WorldChunkMath`, `WeatherSyncState` | Pure helpers for terrain streaming and weather sync. |
| `StatusReplication` | Rounding, tick conversions and request validation for `NetCombatStatus`. |
| `InventorySnapshot`, `WorldItemState`, `SceneObjectPath` | Inventory messages, world item data, naming scene objects without a network identity. |
| `NetPayload`, `StationMirror` (`CraftWorkshopFeed`/`Follower`, `AlchemyBrewFeed`/`Follower`, snapshots, packets), `CraftNetRequest`, `AlchemyNetRequest`, `AlchemyRequestGuard` | Crafting and alchemy stations: compressed payloads in parts, clients' copies following the server's simulation, requests and their bounds. |
| `InteractionPath`, `TradeBook`, `TradeExchange`, `TradeView` | Interaction targets and the trading rules. |
| `DungeonLayoutCodec`, `DungeonBuildSeed` | Dungeon headers and floors sent to clients, and the per-floor build seeds (the dungeon seed stays secret). |
| `NetInput` | Keyboard checks for the network tools (F9). |

## 7. Editor tools (`CustomEditor/Networking`)

| Menu | What it does |
|---|---|
| Create Bootstrap Scene | Builds the Bootstrap scene described above. |
| Fix Scene Network Ids (open scenes) | Regenerates Fish-Net scene ids of the open scenes. |
| Create Dungeon Instance Scene | Builds the dungeon template scene. |
| Create World Item Prefab | Builds `Resources/SimpleMovements/Networking/NetWorldItem.prefab`. |
| Build Item Database / Build Ability Database | Stable ids for items and abilities. |
| Make Selected Prefab Networked ▸ ... | Adds the right components to a prefab (see sections 3 and 4). |
| Make All Mob Prefabs Networked | Every mob prefab in the project. |
| Build ▸ ... / Run ▸ ... | Builds and launches a local dedicated server and clients on `127.0.0.1`. |
| Play In Editor As ▸ Host / Client / Dedicated Server / Single Player | Chooses how the next Play in the editor starts. |
| Validate Network Setup | Lists missing pieces with fixes. |
| Help | Opens these pages. |

Tests: `CustomEditor/Networking/Tests` (Unity *Window ▸ General ▸ Test Runner ▸ Edit Mode*).
