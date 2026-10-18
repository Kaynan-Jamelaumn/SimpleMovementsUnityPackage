# 01 - Architecture and running everything locally

This page explains how the networked game is put together (Stages 2 and 3 of the hub world roadmap) and how to run
it on one machine. **No remote server is needed at any point**: the server and all clients talk over `127.0.0.1`.

## The one rule

> The client sends intentions; the server decides and validates everything that matters to gameplay.

Movement is predicted on the owning client and corrected by the server; attacks, casts, damage, loot, inventory,
currency, spawning, AI, the world cycle and dungeons are decided only by the server. The **dungeon seed is never
sent** to clients (they receive the layout of the floor they are on, nothing more). The **world seed is sent**: every
machine generates the same terrain from it.

## Three ways to run

| Mode | What it is | Use it for |
|---|---|---|
| **Single player** | No network at all. The Stage 1 game, unchanged. | Playing alone, working on content. |
| **Host** | Server and a player in one process (a "listen server"). | Quick tests in the editor; playing with friends on the same machine/LAN. |
| **Dedicated server** | A server without a player, usually headless (batch mode). | Testing what a real server would do; several local clients. |
| **Client** | Joins a server (by default `127.0.0.1:7770`). | Every additional player. |

Without Fish-Net installed (no `FISHNET` scripting define), only single player exists and none of the network code is
compiled. With Fish-Net installed, single player still works exactly as before when no session is started.

## How the pieces fit

```
Bootstrap scene (first scene of the build)
 └─ NetworkManager (Fish-Net: Tugboat on port 7770, TimeManager, SceneManager, ObserverManager, PredictionManager)
     ├─ NetSession          keeps NetAuthority.Mode in sync, installs the resolvers and spawn provider
     ├─ NetBootstrap        start menu / command line: Single player, Host, Join, Dedicated server
     ├─ NetRealmScenes      City loaded per connection; players moved between City / World / dungeon instances
     ├─ NetPlayerSpawner    gives each client a player when it arrives in the City
     ├─ NetLagCompensation  pose history of hittable objects; rewinds them for player hit queries
     └─ HeadlessServer      frame cap, no audio, status log on a dedicated server
 └─ Network World (scene NetworkObject seen by everyone): global relays - world cycle, duels, trading, messages, world chunks
City scene     (loaded by the server at start, by each client when it joins)
World scene    (loaded by the server when someone goes out; by a client when its player goes out)
DungeonInstance scene (empty template; one stacked copy per dungeon run, with its own physics)
```

### Always-compiled bridges (`Networking/Core`, namespace `GameNet`)

Gameplay scripts never reference Fish-Net. They ask small static bridges, which the Fish-Net layer configures:

- **`NetAuthority`** - `Mode` (Offline / Host / Server / Client), `IsServer` ("does this process decide?" - true
  offline, on a host and on a dedicated server), `IsClientOnly`, `HasLocalPlayer`, `IsHeadless`,
  `IsNetworkedObject(go)`, `RunsRules(go)`, `IsLocallyControlled(go)`.
- **`NetSpawn`** - `Spawn(go)` after `Instantiate` (a no-op offline; network-spawns on a server), `Destroy(go)`.
  Runtime objects are moved into the scene of whatever created them first: on a server the active scene is the City,
  but interest management and physics are per scene.
- **`NetScenes`** - load/unload realm and dungeon scenes and move players between them, through Fish-Net on a server.
- **`LagCompensation`** - `using (LagCompensation.For(attacker)) { hit query }`.
- **`IControlLock`** - freezes a networked player without switching components off (prediction keeps ticking).
- **`NetLaunchOptions`**, **`NetInput`**, **`ItemDatabase`** (stable item ids), **`PlayerIds`** (network-stable player ids).

### Interest management

`RealmObserverCondition` is the ObserverManager's default condition: a client only sees objects in a scene it is in
(the City, the World, or its own dungeon instance); in the open World only within 140 m of one of its objects; a client
always sees what it owns. The City and the World stand at overlapping coordinates, and every dungeon run has its own
scene, so scenes are what keep realms apart - for visibility, and on the server for combat too
(`CombatRules.Separation`: entities in two different realm scenes cannot harm or target each other).

### Lag compensation

Fish-Net's collider rollback is part of Fish-Net Pro, so the package ships its own: every server tick
`NetLagCompensation` records where each `LagCompensated` object (players, mobs, summons) is. Each attack or cast a
client sends carries the tick of the last state it received; the server derives that player's delay (clamped to
350 ms) and, while it runs that player's hit queries, moves everything else back to where that player saw it.

## Setting it up (once)

1. Install **Fish-Net** (4.x). It adds the `FISHNET` define.
2. Hub world set up as in [HubWorld/05-Setup-and-Configuration](../HubWorld/05-Setup-and-Configuration.md) (City and
   World scenes in the build).
3. **Tools ▸ SimpleMovements ▸ Network ▸ Create Bootstrap Scene** - creates `Assets/Scenes/Bootstrap.unity` with the
   NetworkManager configured as above (and the "Network World" object with the global relays), puts it first in the
   build, and creates `Resources/SimpleMovements/Networking/NetWorldItem.prefab`, the networked carrier of dropped items and loot.
4. **Create Dungeon Instance Scene** - the template every dungeon run stacks (with its NetworkObject and NetDungeonRun).
5. **Build Item Database** and **Build Ability Database** - stable ids for every item and ability.
6. Select your player prefab: **Make Selected Prefab Networked ▸ Player** (NetworkObject with prediction settings,
   NetPlayer, movement, combat, status, inventory, wallet, realm and interaction components). Assign it on the
   **Net Player Spawner** in the Bootstrap scene. Then **Make All Mob Prefabs Networked**, and use the other
   **Make Selected Prefab Networked** entries for summons, ability pickups, world portals, pickups/loot drops and
   scene interactables (merchants, chests, doors, gates).
7. **Validate Network Setup** lists anything missing.

## Running locally

- **In the editor:** **Play In Editor As ▸ Host** (or press Play in the Bootstrap scene and pick from the menu).
- **Several players on one machine:** **Build ▸ Server And Client**, then **Run ▸ Server And Two Clients** - a headless
  dedicated server plus two client windows, all on `127.0.0.1`. Logs go to `Builds/*.log`. **Run ▸ Stop Launched
  Processes** closes them. You can also join the built server from the editor with **Play In Editor As ▸ Client**.
- **Command line** (built game):
  - `Server.exe -batchmode -nographics -server -port 7770` (a server build starts a dedicated server by itself)
  - `Client.exe -client -address 127.0.0.1 -port 7770 -name Ana`
  - `Game.exe -host` / `-offline`; also `-maxplayers 16`, `-tickrate 30`, `-shard 1234` (shard seed of the cycles).
- **F9** toggles the connection status line (mode, clients, round-trip time, tick rate).

## Where each part is documented

See the [index](README.md): movement prediction, abilities, status and death, weapons and lag compensation, mobs,
inventory, interaction/merchants/trading, hub world sync, dungeons, world terrain.
